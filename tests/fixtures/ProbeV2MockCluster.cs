using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Confluent.Kafka;

namespace Parchwell.Testing;

// Official pinned native test API and handle ownership:
// https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_mock.h
// https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka.h
// This owns an independent native producer solely to host localhost mock brokers.
internal sealed class ProbeV2MockCluster : IDisposable
{
    private readonly nint library;
    private readonly DestroyHandle destroyClient;
    private readonly DestroyHandle destroyCluster;
    private readonly CreateMockTopic createTopic;
    private nint owner;
    private nint cluster;

    internal ProbeV2MockCluster(int brokerCount)
    {
        // Library.VersionString initializes Confluent's platform-specific native loader.
        // Reopen that loaded module, including centos8-librdkafka.so on Debian;
        // do not mix managed handles with another native library instance.
        _ = Library.VersionString;
        using Process process = Process.GetCurrentProcess();
        ProcessModule module = process.Modules.Cast<ProcessModule>().Single(value =>
            value.ModuleName.Contains("rdkafka", StringComparison.OrdinalIgnoreCase));
        library = NativeLibrary.Load(module.FileName);
        try
        {
            destroyClient = Export<DestroyHandle>("rd_kafka_destroy");
            destroyCluster = Export<DestroyHandle>("rd_kafka_mock_cluster_destroy");
            createTopic = Export<CreateMockTopic>("rd_kafka_mock_topic_create");
            // A null configuration requests native defaults, so there is no separate
            // configuration allocation to leak if rd_kafka_new fails.
            byte[] error = new byte[512];
            owner = Export<NewClient>("rd_kafka_new")(0, nint.Zero, error, (nuint)error.Length);
            if (owner == nint.Zero)
                throw new InvalidOperationException("mock_owner: " + Encoding.UTF8.GetString(error).TrimEnd('\0'));
            cluster = Export<NewCluster>("rd_kafka_mock_cluster_new")(owner, brokerCount);
            if (cluster == nint.Zero) throw new InvalidOperationException("mock_cluster");
            BootstrapServers = Marshal.PtrToStringUTF8(Export<ClusterBootstraps>("rd_kafka_mock_cluster_bootstraps")(cluster))
                ?? throw new InvalidOperationException("mock_bootstraps");
        }
        catch
        {
            if (cluster != nint.Zero) destroyCluster!(cluster);
            if (owner != nint.Zero) destroyClient!(owner);
            NativeLibrary.Free(library);
            throw;
        }
    }

    internal string BootstrapServers { get; }

    internal void CreateTopic(string name, int partitions, int replication = 1)
    {
        ObjectDisposedException.ThrowIf(cluster == nint.Zero, this);
        int error = createTopic(cluster, name, partitions, replication);
        if (error != 0) throw new InvalidOperationException("mock_topic: " + error);
    }

    internal async Task<string> CaptureProducerStatisticsAsync(string topic, string clientId, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(cluster == nint.Zero, this);
        var delivered = new TaskCompletionSource<DeliveryReport<byte[], byte[]>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var statistics = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using IProducer<byte[], byte[]> producer = new ProducerBuilder<byte[], byte[]>(new ProducerConfig
        {
            BootstrapServers = BootstrapServers, ClientId = clientId, StatisticsIntervalMs = 1000,
            Acks = Acks.All, EnableIdempotence = true, MessageTimeoutMs = 5000
        }).SetStatisticsHandler((_, raw) =>
        {
            if (delivered.Task.IsCompletedSuccessfully) statistics.TrySetResult(raw);
        }).Build();
        using (IAdminClient admin = new DependentAdminClientBuilder(producer.Handle).Build())
        {
            Metadata metadata = admin.GetMetadata(topic, TimeSpan.FromSeconds(5));
            if (metadata.Topics.Count != 1 || metadata.Topics[0].Error.IsError)
                throw new InvalidOperationException("mock_metadata");
        }
        producer.Produce(topic, new Message<byte[], byte[]>
        {
            Key = Encoding.UTF8.GetBytes("synthetic-native-key"),
            Value = Encoding.UTF8.GetBytes("synthetic-native-value")
        }, report => delivered.TrySetResult(report));
        DeliveryReport<byte[], byte[]> report = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        if (report.Error.IsError || report.Status != PersistenceStatus.Persisted)
            throw new InvalidOperationException("mock_delivery: " + report.Error);
        return await statistics.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
    }

    public void Dispose()
    {
        if (cluster == nint.Zero) return;
        destroyCluster(cluster);
        cluster = nint.Zero;
        destroyClient(owner);
        owner = nint.Zero;
        NativeLibrary.Free(library);
    }

    private T Export<T>(string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint NewClient(int type, nint configuration, [Out] byte[] error, nuint errorSize);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint NewCluster(nint client, int brokerCount);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint ClusterBootstraps(nint cluster);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DestroyHandle(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CreateMockTopic(nint cluster, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int partitions, int replication);
}

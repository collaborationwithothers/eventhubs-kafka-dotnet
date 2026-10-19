using System.Reflection;
using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Verifier.Common.Kafka;

namespace Parchwell.Verifier.Tests.Support;

/// <summary>Uses the real shared OAuth state with existing native interfaces for isolated budget tests.</summary>
internal sealed class KafkaSessionTestSupport : IDisposable
{
    private readonly ServiceProvider services;
    private readonly CancellationTokenSource acquisition = new();
    public KafkaSessionTestSupport()
    {
        var registrations = new ServiceCollection();
        registrations.AddSingleton<TimeProvider>(Clock);
        registrations.AddSingleton<IKafkaTokenProvider>(new TokenProvider(this));
        registrations.AddParchwellKafka(KafkaClientRole.VerifierConsumer, options =>
        {
            options.NamespaceName = "synthetic-namespace";
            options.ClientId = "synthetic-verifier";
        });
        registrations.AddParchwellKafkaAuthentication(options =>
        {
            options.TenantId = "synthetic-tenant";
            options.ClientId = "synthetic-workload-client";
            options.TokenFilePath = "synthetic-file";
        });
        services = registrations.BuildServiceProvider();
        Authentication = services.GetRequiredService<KafkaAuthenticationFactory>().CreateSession(
            new KafkaAuthenticationIdentity("A01", "synthetic-run", KafkaClientRole.VerifierConsumer, "synthetic-verifier"),
            acquisition.Token);
        Consumer = NativeProxy.Create<IConsumer<byte[], byte[]>>((method, arguments) =>
        {
            switch (method.Name)
            {
                case "Consume":
                    PollTimeouts.Add((TimeSpan)arguments![0]!);
                    Poll?.Invoke();
                    return ReadRecord?.Invoke();
                case "Assign":
                    Assign?.Invoke((IEnumerable<TopicPartitionOffset>)arguments![0]!);
                    return null;
                case "Position":
                    return Position?.Invoke((TopicPartition)arguments![0]!) ?? Offset.Unset;
                case "QueryWatermarkOffsets":
                    QueryTimeouts.Add((TimeSpan)arguments![1]!);
                    return Query?.Invoke((TopicPartition)arguments[0]!) ?? new WatermarkOffsets(0, 0);
                case "Dispose":
                    Events.Add("consumer-dispose");
                    ConsumerDispose?.Invoke();
                    return null;
                default: throw new InvalidOperationException(method.Name);
            }
        });
        IAdminClient admin = NativeProxy.Create<IAdminClient>((method, arguments) =>
        {
            switch (method.Name)
            {
                case "GetMetadata":
                    MetadataTimeouts.Add((TimeSpan)arguments![1]!);
                    return ReadMetadata?.Invoke() ?? Metadata(0);
                case "Dispose":
                    Events.Add("admin-dispose");
                    AdminDispose?.Invoke();
                    return null;
                default: throw new InvalidOperationException(method.Name);
            }
        });
        Session = new VerifierKafkaSession(Consumer, admin, Authentication, Clock, Options, acquisition);
    }

    public ManualKafkaTimeProvider Clock { get; } = new();
    public VerifierHostOptions Options { get; } = new()
    {
        TokenTimeout = TimeSpan.FromMilliseconds(1000), PollSlice = TimeSpan.FromMilliseconds(100),
        MetadataTimeout = TimeSpan.FromMilliseconds(1000), WatermarkTimeout = TimeSpan.FromMilliseconds(1000), MaxPartitions = 2
    };
    public KafkaOAuthBearerSession Authentication { get; }
    public VerifierKafkaSession Session { get; }
    public IConsumer<byte[], byte[]> Consumer { get; }
    public List<string> Events { get; } = [];
    public List<TimeSpan> PollTimeouts { get; } = [];
    public List<TimeSpan> MetadataTimeouts { get; } = [];
    public List<TimeSpan> QueryTimeouts { get; } = [];
    public Action? Poll { get; set; }
    public Func<ConsumeResult<byte[], byte[]>?>? ReadRecord { get; set; }
    public Action<IEnumerable<TopicPartitionOffset>>? Assign { get; set; }
    public Func<TopicPartition, Offset>? Position { get; set; }
    public Func<Metadata>? ReadMetadata { get; set; }
    public Func<TopicPartition, WatermarkOffsets>? Query { get; set; }
    public Action? AdminDispose { get; set; }
    public Action? ConsumerDispose { get; set; }
    public Func<CancellationToken, AccessToken>? AcquireToken { get; set; }

    public void Refresh()
    {
        // Shared OAuth deliberately has no public test mutator. Invoke its actual callback body;
        // token acquisition still uses the public factory/DI and the supplied cancellation token.
        MethodInfo callback = typeof(KafkaOAuthBearerSession).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, types: [typeof(Action<string, long, string>), typeof(Action<string>)], modifiers: null)!;
        callback.Invoke(Authentication, [(Action<string, long, string>)((_, _, _) => { }), (Action<string>)(_ => { })]);
    }

    public void Install()
    {
        Poll = Refresh;
        Session.InstallToken();
        Poll = null;
    }

    public static Metadata Metadata(params int[] partitions) => new([], [new TopicMetadata("document-events",
        partitions.Select(partition => new PartitionMetadata(partition, 0, [], [], new Error(ErrorCode.NoError))).ToList(),
        new Error(ErrorCode.NoError))], 0, "synthetic-broker");

    public void Dispose()
    {
        try { Session.Cleanup(); }
        finally { services.Dispose(); }
    }

    private sealed class TokenProvider(KafkaSessionTestSupport owner) : IKafkaTokenProvider
    {
        public AccessToken GetToken(string scope, CancellationToken cancellationToken) =>
            owner.AcquireToken?.Invoke(cancellationToken) ?? new AccessToken("synthetic-token", owner.Clock.GetUtcNow().AddHours(1));
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1852", Justification = "DispatchProxy generates a subclass at runtime.")]
internal class NativeProxy : DispatchProxy
{
    private Func<MethodInfo, object?[]?, object?> invoke = null!;
    public NativeProxy() { }
    internal static T Create<T>(Func<MethodInfo, object?[]?, object?> invoke) where T : class
    {
        T proxy = Create<T, NativeProxy>();
        ((NativeProxy)(object)proxy).invoke = invoke;
        return proxy;
    }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => invoke(targetMethod!, args);
}

internal sealed class ManualKafkaTimeProvider : TimeProvider
{
    private long ticks;
    private readonly List<ManualTimer> timers = [];
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => ticks;
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(ticks);
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        timers.Add(timer);
        return timer;
    }
    public void Advance(int milliseconds)
    {
        ticks += TimeSpan.FromMilliseconds(milliseconds).Ticks;
        foreach (ManualTimer timer in timers.ToArray()) timer.FireIfDue();
    }
    private sealed class ManualTimer(ManualKafkaTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private long? due;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            due = dueTime == Timeout.InfiniteTimeSpan ? null : owner.ticks + dueTime.Ticks;
            return true;
        }
        public void FireIfDue()
        {
            if (due is not long deadline || owner.ticks < deadline) return;
            due = null;
            callback(state);
        }
        public void Dispose() => due = null;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}

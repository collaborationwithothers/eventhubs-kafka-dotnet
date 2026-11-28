using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Enrichment.ConsumerProcessing;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing;

internal sealed class ConsumerTestHarness : IDisposable
{
    internal ConsumerProcessingOptions Options { get; } = ValidOptions();
    internal ConsumerProcessingPlan Plan { get; set; } = new() { ConfigurationId = "C1", WindowMs = 1000 };
    internal ConsumerTestClock Clock { get; } = new();
    internal List<string> Calls { get; } = [];
    internal RecordingRecordStore Store { get; }
    internal IConsumer<byte[], byte[]> Consumer { get; }
    internal RecordingConsumer Recorded { get; }
    internal StringWriter Error { get; } = new(CultureInfo.InvariantCulture);
    internal ConsumerConfig? Configuration { get; private set; }
    internal ConsumerProcessingHandlers Handlers { get; private set; } = null!;
    internal int StoreFactories { get; private set; }
    internal int ConsumerFactories { get; private set; }
    internal List<int> ExitCodes { get; } = [];
    internal List<int> Signals { get; } = [];
    internal Func<int, (int ReturnValue, int Errno)>? Signal { get; set; }
    internal bool NoPlanSource { get; set; }
    internal Action? DuringTimerWait { get; set; }
    // The token the worker receives as its stop request; a test cancels it to stop the worker.
    internal CancellationTokenSource StopSource { get; } = new();

    internal ConsumerTestHarness()
    {
        Store = new RecordingRecordStore(Calls);
        Consumer = DispatchProxy.Create<IConsumer<byte[], byte[]>, RecordingConsumer>();
        Recorded = (RecordingConsumer)(object)Consumer;
        Recorded.Harness = this;
    }
    internal async Task<int> RunAsync()
    {
        var worker = new ConsumerProcessingWorker(Microsoft.Extensions.Options.Options.Create(Options),
            NoPlanSource ? null : new PlanSource(Plan), CreateBaseline,
            () => { StoreFactories++; return Store; },
            CreateConsumer,
            Clock, code => ExitCodes.Add(code), SendSignal, Error);
        Task<int> execution = Task.Run(() => worker.ExecuteAsync(StopSource.Token));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!execution.IsCompleted)
        {
            await Task.Delay(1, deadline.Token);
            if (execution.IsCompleted) break;
            DuringTimerWait?.Invoke();
            Clock.AdvanceProcessingTimer();
        }
        return await execution;
    }
    internal (int ReturnValue, int Errno) SendSignal(int signal)
    {
        Signals.Add(signal);
        Calls.Add("signal:" + signal.ToString(CultureInfo.InvariantCulture));
        return Signal?.Invoke(signal) ?? (0, 0);
    }
    internal IConsumer<byte[], byte[]> CreateConsumer(ConsumerConfig configuration, ConsumerProcessingHandlers handlers)
    { Calls.Add("construct"); ConsumerFactories++; Configuration = configuration; Handlers = handlers; return Consumer; }
    internal ConsumerConfig CreateBaseline()
    {
        var services = new ServiceCollection();
        services.AddParchwellKafka(KafkaClientRole.EnrichmentConsumer, options =>
        {
            options.NamespaceName = Options.NamespaceName;
            options.ClientId = Options.PodName;
            options.EnrichmentGroupId = Options.GroupId;
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<KafkaConfigurationFactory>().CreateEnrichmentConsumer().Configuration;
    }
    internal static ConsumerProcessingOptions ValidOptions(string runId = "run1", int repeat = 2)
    {
        string job = $"{runId}-c1-r{repeat.ToString(CultureInfo.InvariantCulture)}-consumer-subject";
        return new ConsumerProcessingOptions
        {
            RunId = runId, ScenarioId = "C03", Phase = "C1", RepeatNumber = repeat, Role = "subject",
            JobName = job, PodName = job + "-abcde", PodUid = "11111111-2222-3333-4444-555555555555",
            NodeName = "synthetic-node", SourceCommit = new string('a', 40), NamespaceName = "synthetic-eh",
            BlobEndpoint = "https://synthetic.blob.core.windows.net"
        };
    }
    internal static ConsumeResult<byte[], byte[]> Message(long offset, int partition = 0) => new()
    {
        Topic = "document-events", Partition = partition, Offset = offset,
        Message = new Message<byte[], byte[]>
        {
            Key = Encoding.UTF8.GetBytes("synthetic-key"), Value = [],
            Headers = new Headers
            {
                { "instance-id", Encoding.UTF8.GetBytes("synthetic-producer") },
                { "attempt-id", Encoding.UTF8.GetBytes("synthetic-attempt") },
                { "sequence", Encoding.UTF8.GetBytes(offset.ToString(CultureInfo.InvariantCulture)) }
            }
        }
    };
    internal void Assign(params int[] partitions)
    {
        Recorded.Assignment.Clear();
        Recorded.Assignment.AddRange(partitions.Select(partition => new TopicPartition("document-events", partition)));
        Handlers.Assigned(Consumer, Recorded.Assignment.ToList());
    }
    internal void Revoke(bool lost, params int[] partitions)
    {
        var removed = partitions.Select(partition => new TopicPartition("document-events", partition)).ToList();
        Recorded.Assignment.RemoveAll(partition => removed.Contains(partition));
        if (lost) Handlers.Lost(Consumer, removed); else Handlers.Revoked(Consumer, removed);
    }
    internal void RaiseError(Error error) => Handlers.ErrorReported!(Consumer, error);
    public void Dispose() { StopSource.Dispose(); Error.Dispose(); }
    internal sealed class PlanSource(ConsumerProcessingPlan plan) : IConsumerProcessingPlanSource
    {
        public ConsumerProcessingPlan? GetPlan(string scenarioId, string configurationId) => plan;
    }
}

internal sealed class RecordingRecordStore(List<string> calls) : IConsumerRecordStore
{
    internal JsonElement? Registration { get; private set; }
    internal JsonElement? Completion { get; private set; }
    internal JsonElement? StopReceipt { get; private set; }
    internal bool FailStopReceipt { get; set; }
    // Whether each token passed to AppendAsync could be cancelled, in call order.
    internal List<bool> AppendTokensCanBeCanceled { get; } = [];
    internal bool? StopReceiptTokenCanBeCanceled { get; private set; }
    internal List<JsonElement> Rows { get; } = [];
    internal Func<JsonElement, bool>? FailAppend { get; set; }
    internal bool FailRegistration { get; set; }
    internal Exception? RegistrationException { get; set; }
    internal bool FailCompletion { get; set; }
    internal int AppendAttempts { get; private set; }
    internal IEnumerable<JsonElement> Processed => Rows.Where(row => row.GetProperty("kind").GetString() == "processed");
    public Task RegisterAsync(byte[] json, CancellationToken cancellationToken)
    {
        calls.Add("registration");
        if (RegistrationException is not null) throw RegistrationException;
        if (FailRegistration) throw new InvalidOperationException("synthetic_registration_failure");
        Registration = Parse(json);
        return Task.CompletedTask;
    }
    public Task AppendAsync(byte[] jsonLine, CancellationToken cancellationToken)
    {
        AppendAttempts++;
        AppendTokensCanBeCanceled.Add(cancellationToken.CanBeCanceled);
        JsonElement row = Parse(jsonLine);
        string kind = row.GetProperty("kind").GetString()!;
        calls.Add("append:" + kind);
        if (FailAppend?.Invoke(row) == true) throw new InvalidOperationException("synthetic_append_failure");
        Rows.Add(row);
        return Task.CompletedTask;
    }
    public Task CompleteAsync(byte[] json, CancellationToken cancellationToken)
    {
        calls.Add("completion");
        if (FailCompletion) throw new InvalidOperationException("synthetic_completion_failure");
        Completion = Parse(json);
        return Task.CompletedTask;
    }
    public Task WriteStopReceiptAsync(byte[] json, CancellationToken cancellationToken)
    {
        calls.Add("stop-receipt");
        StopReceiptTokenCanBeCanceled = cancellationToken.CanBeCanceled;
        if (FailStopReceipt) throw new InvalidOperationException("synthetic_stop_receipt_failure");
        StopReceipt = Parse(json);
        return Task.CompletedTask;
    }
    private static JsonElement Parse(byte[] json) { using JsonDocument document = JsonDocument.Parse(json); return document.RootElement.Clone(); }
}

public class RecordingConsumer : DispatchProxy
{
    internal ConsumerTestHarness Harness { get; set; } = null!;
    internal Queue<Func<ConsumeResult<byte[], byte[]>?>> Polls { get; } = new();
    internal List<TopicPartition> Assignment { get; } = [];
    internal List<long> StoredOffsets { get; } = [];
    internal List<long> PollTimes { get; } = [];
    internal List<List<TopicPartition>> Pauses { get; } = [];
    internal List<List<TopicPartition>> Resumes { get; } = [];
    internal Action? BeforeClose { get; set; }
    internal Action? OnClose { get; set; }
    internal Action<ConsumeResult<byte[], byte[]>>? OnStore { get; set; }
    internal int CloseCalls { get; private set; }
    internal int CommitCalls { get; private set; }
    internal bool InClose { get; private set; }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        switch (targetMethod!.Name)
        {
            case "Subscribe":
                Harness.Calls.Add("subscribe");
                Harness.Assign(0, 1);
                return null;
            case "Consume":
                PollTimes.Add(Harness.Clock.GetTimestamp());
                Harness.Calls.Add("consume");
                Harness.Clock.Advance(100);
                // Invoke callbacks directly: v2.16.0 propagates a handler's original
                // exception from Consume and Close without a ConsumeException wrapper.
                return Polls.Count == 0 ? null : Polls.Dequeue()();
            case "get_Assignment": return Assignment.ToList();
            case "StoreOffset":
                var message = (ConsumeResult<byte[], byte[]>)args![0]!;
                Harness.Calls.Add("store:" + message.Offset.Value.ToString(CultureInfo.InvariantCulture));
                OnStore?.Invoke(message);
                StoredOffsets.Add(message.Offset.Value + 1);
                return null;
            case "Pause": Pauses.Add(((IEnumerable<TopicPartition>)args![0]!).ToList()); Harness.Calls.Add("pause"); return null;
            case "Resume": Resumes.Add(((IEnumerable<TopicPartition>)args![0]!).ToList()); Harness.Calls.Add("resume"); return null;
            case "Close":
                BeforeClose?.Invoke();
                CloseCalls++;
                InClose = true;
                Harness.Calls.Add("close");
                try { OnClose?.Invoke(); }
                finally { InClose = false; }
                Harness.Calls.Add("close-returned");
                return null;
            case "Commit": CommitCalls++; throw new InvalidOperationException("unexpected_explicit_commit");
            case "Unsubscribe": throw new InvalidOperationException("unexpected_unsubscribe");
            case "Dispose": Harness.Calls.Add("dispose"); return null;
            default: throw new InvalidOperationException(targetMethod.Name);
        }
    }
}

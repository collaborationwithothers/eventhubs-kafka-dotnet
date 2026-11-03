using System.Text;
using Parchwell.Verifier.Common.SingleAction;
using Parchwell.Verifier.VerifyRun.IdleObservation;
using Broker = (string Key, long NodeId, string Source, string State, long TxIdle, long RxIdle, long Connects, long Disconnects, long Timeouts, long Metadata);

namespace Parchwell.Verifier.Tests.IdleObservation;

/// <summary>Synthetic idle-observation captures. Every value is invented for the judge tests; none is a measurement.</summary>
internal static class IdleFixture
{
    internal const string CommitSha = "1111111111111111111111111111111111111111";
    internal static readonly long[] DefaultTimes = [.. Enumerable.Range(1, 29).Select(index => index * 15000L)];
    internal static readonly Dictionary<string, string> DefaultSettings = new(StringComparer.Ordinal) { ["client.id"] = "p-r1", ["acks"] = "-1" };

    internal static string[] PodKeys { get; } = ["A02C0", "A02C1", "A03C0", "A03C1", "A03C2", "A04C0", "A04C1"];

    internal static string CapturePod(string key) => $"run1-{key[..3].ToLowerInvariant()}-{key[3..].ToLowerInvariant()}-idle-abc";

    internal static string CapturePodsText(IEnumerable<string> keys) => string.Join(",", keys.Select(key => $"{key}:{CapturePod(key)}:uid-{key}"));

    internal static IdleObservationJudgeOptions JudgeOptions(string runKind = "batch") => new()
    {
        RunKind = runKind, RunId = "run1", SourceCommit = CommitSha, JobName = "run1-idle-judge", PodName = "run1-idle-judge-abc", PodUid = "judge-uid",
        NamespaceName = "synthetic-ns", ServiceAccount = "verifier", WorkloadIdentityClientId = "44444444-4444-4444-4444-444444444444", ScenarioRoot = "/app",
        BlobServiceUri = "https://syntheticstore.blob.core.windows.net/", LedgerContainer = "ledger", VerifierContainer = "verifier",
        LoadGenImage = "registry.test/parchwell/loadgen@sha256:" + new string('a', 64), VerifierImage = "registry.test/parchwell/verifier@sha256:" + new string('b', 64),
        CapturePods = CapturePodsText(PodKeys),
        Items = "A02,A03,A04",
        Limits = new()
        {
            ProcessLifetimeSeconds = 660, CaptureIoSeconds = 60, IoSeconds = 10, CleanupSeconds = 10, MaxCaptureBytes = 167772160, MaxRegistrationBytes = 65536,
            MaxCompletionBytes = 65536, MaxStatisticsReportBytes = 262144, MaxBrokerEntries = 64, MaxCandidateBytes = 16777216, MaxJudgeReceiptBytes = 65536
        },
        ScenarioLoadOptions = new() { MaxDocumentCharacters = 1048576, MaxNestingDepth = 32, MaxScalarCharacters = 65536, MaxParserEvents = 100000 }
    };

    internal static Broker Bootstrap() => ("b/-1", -1, "configured", "UP", 1000000, 1000000, 1, 0, 0, 0);
    internal static Broker Node8() => ("h:9093/8", 8, "learned", "UP", 1000000, 1000000, 1, 0, 0, 0);

    internal static string BrokersJson(params Broker[] brokers) =>
        "{\"ts\":1,\"brokers\":{" + string.Join(",", brokers.Select(b =>
            $"\"{b.Key}\":{{\"nodeid\":{b.NodeId},\"source\":\"{b.Source}\",\"state\":\"{b.State}\",\"txidle\":{b.TxIdle},\"rxidle\":{b.RxIdle},\"connects\":{b.Connects},\"disconnects\":{b.Disconnects},\"req_timeouts\":{b.Timeouts},\"req\":{{\"Produce\":0,\"Metadata\":{b.Metadata}}}}}")) + "}}";

    internal static SingleActionStatistic Raw(int sequence, long elapsed, string json) => new(sequence, elapsed, Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));

    internal static SingleActionStatistic Report(int sequence, long elapsed, params Broker[] brokers) => Raw(sequence, elapsed, BrokersJson(brokers));

    /// <summary>Reports at the given times, numbered from 1, with the brokers the function returns for each elapsed time.</summary>
    internal static SingleActionStatistic[] Series(Func<long, Broker[]>? brokers = null, IEnumerable<long>? times = null)
    {
        brokers ??= _ => [Bootstrap(), Node8()];
        return [.. (times ?? DefaultTimes).Select((elapsed, index) => Report(index + 1, elapsed, brokers(elapsed)))];
    }

    /// <summary>Reports where only broker 8 changes, by the given function of elapsed time.</summary>
    internal static SingleActionStatistic[] Node8Series(Func<long, Broker, Broker> change, IEnumerable<long>? times = null) =>
        Series(elapsed => [Bootstrap(), change(elapsed, Node8())], times);

    internal static IdleProduce Produce(int attempt, long started, long terminalAt, string result = "success") =>
        new(attempt, "k" + attempt, started, true, result, result == "success" ? null : new IdleError(29, false, "broker", "synthetic"), terminalAt, terminalAt);

    internal static IdleRepeat Repeat(SingleActionStatistic[]? statistics = null) => new(1, "p-r1", new Dictionary<string, string>(DefaultSettings, StringComparer.Ordinal), [], 0,
        Produce(1, 0, 1000), 2000, 422000, Produce(2, 422000, 422500), [], 0, statistics ?? Series(), []);

    internal static IdleRepeatResult JudgeOne(IdleRepeat repeat, int ordinal = 1, IReadOnlyDictionary<string, string>? settings = null, IReadOnlyList<string>? omissions = null,
        int maxBrokers = 64) =>
        IdleObservationJudgement.JudgeRepeat(repeat, ordinal, "p", settings ?? DefaultSettings, omissions ?? [], 420, 262144, maxBrokers);

    internal static SingleActionRegistration Registration(string item = "A02", string configuration = "C0") =>
        new(1, "idle-observation", Identity(item, configuration), Job(item, configuration), "p", "uid", CommitSha);

    internal static SingleActionCompletion Completion(string item = "A02", string configuration = "C0") =>
        new(1, "idle-observation", Identity(item, configuration), Job(item, configuration), "p", "uid", CommitSha, 5, 0, true);

    internal static IdleCapture Capture(string item = "A02", string configuration = "C0") =>
        new(1, "idle-observation", true, "batch", Identity(item, configuration), Job(item, configuration), "p", "uid", CommitSha, "2.16.0", "2.16.0",
            "document-events", 0, 1000, 420, 15000, 80, [.. Enumerable.Range(1, 5).Select(ordinal => Repeat() with { Repeat = ordinal })]);

    internal static SingleActionIdentity Identity(string item, string configuration) => new(item, configuration, "run1", "p");

    internal static string Job(string item, string configuration) => $"run1-{item.ToLowerInvariant()}-{configuration.ToLowerInvariant()}-idle";

    internal static IdleRepeatResult Result(string connectionClass, IdleWindows? windows = null, string status = "complete_valid") =>
        new(1, "p-r1", status, [], status == "complete_valid" ? connectionClass : null, null, null, windows, 2, [], [], 0);

    internal static IdleConfigurationResult Summary(string id, string? summary, string status = "complete_valid", IdleRepeatResult[]? repeats = null) =>
        new(id, status, summary, null, repeats ?? []);

    internal static IdleWindows CloseWindows(long? after, long? quiet) => new(0, 0, 0, 0, 0, after, quiet, 0, 0, 0, 0, false);
}

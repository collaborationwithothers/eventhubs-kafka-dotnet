using System.Text;
using System.Collections.Immutable;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.Tests.Support;

/// <summary>Provides fixed test inputs whose expected bytes do not come from production codecs.</summary>
internal static class RecordedEvidence
{
    /// <summary>Returns strict limits large enough for small synthetic parser cases.</summary>
    public static InputLimits SmallLimits { get; } = new(
        MaxDepth: 16,
        MaxArtifactBytes: 16_384,
        MaxTotalInputBytes: 32_768,
        MaxRowBytes: 4_096,
        MaxRowsPerArtifact: 16,
        MaxEntries: 512,
        MaxDiagnosticBytes: 256);

    /// <summary>Returns literal UTF-8 bytes for a minimal independent identity object.</summary>
    public static ReadOnlyMemory<byte> IdentityJson { get; } = Encoding.UTF8.GetBytes(
        "{\"scenario_id\":\"A01\",\"configuration_id\":\"C0\",\"run_id\":\"run-1\",\"instance_id\":\"instance-1\",\"expected_instance_id\":\"instance-1\",\"client_id\":\"producer-1\"}");

    /// <summary>Returns literal bytes with a nested duplicate property for parser regressions.</summary>
    public static ReadOnlyMemory<byte> NestedDuplicateJson { get; } = Encoding.UTF8.GetBytes(
        "{\"outer\":{\"field\":1,\"field\":2}}");

    /// <summary>Returns the path to an unchanged committed golden fixture file.</summary>
    public static string FixturePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return Path.Combine(AppContext.BaseDirectory, "fixtures", "verifier-v1", relativePath);
    }

    // These inputs are synthetic projections, including supplied window completion.
    internal static RunJudgementRequest SyntheticRequest()
    {
        var identity = new RunIdentity("A01", "C0", "run-1", "instance-1", "instance-1", "producer-1");
        AttemptEntry attempt = Attempt();
        var manifest = new SealedRunManifest(1, identity, "synthetic", 1, 1, [attempt],
            1_000_000, 0, "complete", true, ParsedField<long>.Null(), [],
            new Dictionary<string, string>(), new Dictionary<string, string>(),
            ["broker_lifecycle_inventory_unproven", "request_lifecycle_capture_unsupported"]);
        var capturedConfiguration = new Parchwell.Verifier.VerifyRun.Input.ResolvedConfiguration(0,
            new Dictionary<string, Parchwell.Verifier.VerifyRun.Input.ResolvedSetting>(StringComparer.Ordinal), []);
        var capabilities = new CapturedCapabilities(1, identity, [],
            ParsedField<Parchwell.Verifier.VerifyRun.Input.ResolvedConfiguration>.FromValue(capturedConfiguration),
            [], false, ["broker-a"], [], true,
            ParsedField<Completion>.FromValue(new(true, ParsedField<long>.FromValue(100), [], [])), [], false);
        ReportAssociation[] reports = [Report(90, 40), Report(100, 50), Report(110, 60)];
        return new(identity,
            ParseResult<ArtifactIndex>.Success(new(1, identity, "complete", true, true, true, [])),
            ParseResult<IReadOnlyList<LedgerRow>>.Success([LedgerRow(attempt)]),
            ParseResult<SealedRunManifest>.Success(manifest),
            ParseResult<IReadOnlyList<StatisticsEnvelope>>.Success(reports.Select(r => r.Envelope).ToArray()),
            ParseResult<IReadOnlyList<LifecycleEvent>>.Success([]),
            ParseResult<CapturedCapabilities>.Success(capabilities), reports, ["broker-a"],
            new ResolvedRoleConfiguration(KafkaClientRole.LoadGenProducer,
                ImmutableDictionary<string, Parchwell.Scenarios.ResolvedSetting>.Empty, []),
            [new("synthetic-topic", 0, 0, "k", "instance-1", "a1", 1, [])],
            Enumerable.Range(7, 5).Select(WindowPass).ToArray(), new OutputLimits(65_536, 16, 256));
    }

    internal static AttemptEntry Attempt(string id = "a1", string key = "k", long sequence = 1) =>
        new("instance-1", id, 0, "synthetic-tenant", "synthetic-document", key, sequence, "uploaded", 0, 0);

    internal static LedgerRow LedgerRow(AttemptEntry? attempt = null, bool delivered = true, string source = "callback") =>
        new(1, new("A01", "C0", "run-1", "instance-1", "instance-1", "producer-1"),
            attempt ?? Attempt(), source, delivered, 0, ParsedField<string>.Null(),
            source == "callback" ? ParsedField<int>.FromValue(2) : ParsedField<int>.Null(),
            source == "callback" ? "native_callback" : "pre_enqueue", 0,
            ParsedField<double>.Null(), ParsedField<double>.Null());

    internal static NativeBrokerReport Broker(string key = "broker-a", string source = "learned", string state = "UP") =>
        new(key, ParsedField<string>.FromValue(key), ParsedField<long>.FromValue(1),
            ParsedField<string>.FromValue(source), ParsedField<string>.FromValue(state),
            ParsedField<long>.FromValue(0), ParsedField<long>.FromValue(0), ParsedField<long>.FromValue(0),
            ParsedField<long>.FromValue(0), ParsedField<long>.FromValue(0),
            ParsedField<NativeWindow>.FromValue(new(ParsedField<long>.FromValue(0), ParsedField<long>.FromValue(0), ParsedField<long>.Missing())),
            ParsedField<NativeWindow>.FromValue(new(ParsedField<long>.FromValue(0), ParsedField<long>.Missing(), ParsedField<long>.FromValue(0))));

    internal static ReportAssociation Report(long timestamp, long age, IReadOnlyList<NativeBrokerReport>? brokers = null)
    {
        var identity = new RunIdentity("A01", "C0", "run-1", "instance-1", "instance-1", "producer-1");
        var envelope = new StatisticsEnvelope(1, identity, new("A01", "run-1", 0, "producer-1"),
            "synthetic-session", "synthetic-received", "synthetic-receipt", "{}", ParsedField<TypedSnapshot>.Null(), []);
        var native = new NativeClientReport(ParsedField<string>.Missing(), ParsedField<string>.Missing(),
            ParsedField<string>.Missing(), ParsedField<long>.FromValue(timestamp), ParsedField<long>.Missing(),
            ParsedField<long>.FromValue(age),
            ParsedField<IReadOnlyDictionary<string, NativeBrokerReport>>.FromValue(
                (brokers ?? [Broker()]).ToDictionary(b => b.DictionaryKey, StringComparer.Ordinal)));
        return new(envelope, ParseResult<NativeClientReport>.Success(native));
    }

    internal static CheckAssessment WindowPass(int check) => new(check, AssessmentEvaluation.Evaluated,
        AssessmentDisposition.Pass, null, null, null, null, null, null);

    internal static CheckAssessment WindowFail(int check, AssessmentClassification classification = AssessmentClassification.Incomplete) =>
        new(check, AssessmentEvaluation.Evaluated, AssessmentDisposition.Fail, classification,
            "synthetic-window", null, null, $"check_{check}_failed", null);

}

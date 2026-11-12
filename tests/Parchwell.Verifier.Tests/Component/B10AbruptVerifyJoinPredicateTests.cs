using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.VerifyRun.Abrupt;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707
public sealed class B10AbruptVerifyJoinPredicateTests
{
    private static readonly int[] IdentityReasons = [3];
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Given_TimeoutRowMatchesBelowAtOrAboveOne_When_Joining_Then_OnlyUniqueMatchIsRetained(int count)
    {
        TimeoutEvidence evidence = TimeoutInput(count);
        var record = new ConsumedRecord("document-events", 0, 0, "ordinary", B10AbruptVerifyEvidence.Timeout, "t1", 0, []);
        TimeoutRecordJoin result = TimeoutEvidenceReader.Join(evidence, [record], B10AbruptVerifyEvidence.Run);
        Assert.Equal(count != 1, result.Unjoined);
        Assert.Equal(count == 1 ? new[] { record } : [], result.Records);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("attempt-null")]
    [InlineData("key-null")]
    [InlineData("sequence-null")]
    [InlineData("problem")]
    public void Given_UniqueTimeoutRow_When_RecordFieldIsMissingOrMalformed_Then_OnlyWellFormedRecordJoins(string field)
    {
        TimeoutEvidence evidence = TimeoutInput(1);
        var record = new ConsumedRecord("document-events", 0, 0, field == "key-null" ? null : "ordinary", B10AbruptVerifyEvidence.Timeout,
            field == "attempt-null" ? null : "t1", field == "sequence-null" ? null : 0,
            field == "problem" ? [new StructuralProblem(null, "synthetic_record_problem", "record", null, null, null)] : []);
        TimeoutRecordJoin result = TimeoutEvidenceReader.Join(evidence, [record], B10AbruptVerifyEvidence.Run);
        Assert.Equal(field != "valid", result.Unjoined);
        Assert.Equal(field == "valid" ? new[] { record } : [], result.Records);
    }

    [Fact]
    public void Given_EmptyWindow_When_JoiningTimeoutRecords_Then_NoIdentityFailureIsInvented()
    {
        TimeoutRecordJoin result = TimeoutEvidenceReader.Join(TimeoutInput(0), [], B10AbruptVerifyEvidence.Run);
        Assert.False(result.Unjoined); Assert.Empty(result.Records);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Given_ReportLeaderWithBelowAtOrAboveOneMatchingBroker_When_FirstEvidenceIsRead_Then_OnlyUniqueLeaderAuthenticates(int count)
    {
        var evidence = new B10AbruptVerifyEvidence();
        const string name = "same_instance-pre-acceptance-report.json";
        evidence.Change("first/" + name, root =>
        {
            JsonObject brokers = root["brokers"]!.AsObject();
            if (count == 0) brokers[B10AbruptVerifyEvidence.Broker]!["nodeid"] = 2;
            if (count == 2) brokers["sasl_ssl://broker-b:9093/2"] = brokers[B10AbruptVerifyEvidence.Broker]!.DeepClone();
        });
        JsonNode report = evidence.Frames.Single(frame => frame["kind"]!.GetValue<string>() == "pre_acceptance_report" && frame["data"]!["name"]!.GetValue<string>() == name)["data"]!;
        report["length"] = evidence.Files["first/" + name].Length; report["sha256"] = B10AbruptVerifyEvidence.Hash(evidence.Files["first/" + name]);
        evidence.Rehash();
        AbruptFirstEvidence result = await ReadFirstAsync(evidence);
        Assert.Equal(count != 1, result.Invalid);
        Assert.Equal(count == 1 ? [] : IdentityReasons, result.Reasons.Order());
    }

    [Theory]
    [InlineData("same_instance-pre-acceptance-report.json")]
    [InlineData("control-post-drain-report.json")]
    public async Task Given_OneMissingJournalBoundReportWithConsistentManifest_When_FirstEvidenceIsRead_Then_OnlyIdentityReasonChanges(string name)
    {
        var evidence = new B10AbruptVerifyEvidence(); evidence.Files.Remove("first/" + name); evidence.Rehash();
        AbruptFirstEvidence result = await ReadFirstAsync(evidence);
        Assert.True(result.Invalid); Assert.Equal(IdentityReasons, result.Reasons.Order());
    }

    private static async Task<AbruptFirstEvidence> ReadFirstAsync(B10AbruptVerifyEvidence evidence)
    {
        var options = new VerifierHostOptions { RunId = B10AbruptVerifyEvidence.Run, VerifierCommit = B10AbruptVerifyEvidence.Commit, LoadGenImage = B10AbruptVerifyEvidence.Image };
        string prefix = B10AbruptVerifyEvidence.Prefix + B10AbruptVerifyEvidence.First + "/";
        Dictionary<string, byte[]> files = evidence.Files.Where(pair => pair.Key.StartsWith("first/", StringComparison.Ordinal))
            .ToDictionary(pair => prefix + pair.Key["first/".Length..], pair => pair.Value, StringComparer.Ordinal);
        var reader = new AbruptEvidenceReader(options, (path, _, _) => Task.FromResult<byte[]?>(files[path]));
        return await reader.ReadFirstAsync(B10AbruptVerifyEvidence.Prefix, B10AbruptVerifyEvidence.First, "uid-first", files.Keys.ToArray(), TestContext.Current.CancellationToken);
    }

    private static TimeoutEvidence TimeoutInput(int count)
    {
        var evidence = new B10AbruptVerifyEvidence();
        evidence.SetTimeoutRows(Enumerable.Range(0, count).Select(_ => B10AbruptVerifyEvidence.TimeoutRow("t1", "ordinary", 0)).ToArray());
        var limits = new InputLimits(32, 268435456, 2147483648, 1048576, 10000, 10000, 4096);
        var budget = new InputBudget(limits);
        var registration = PhaseWireCodec.ReadRegistration(evidence.Files["timeout/registration.json"]);
        string prefix = "synthetic/C1/r1/producer/" + B10AbruptVerifyEvidence.Timeout + "/";
        var registered = new TimeoutRegistration(B10AbruptVerifyEvidence.Timeout, registration,
            new BlobListing(evidence.Files.Keys.Where(name => name.StartsWith("timeout/", StringComparison.Ordinal)).Select(name => prefix + name["timeout/".Length..]).ToArray(), 1, true));
        byte[] Bytes(string name) => evidence.Files["timeout/publication/" + name];
        ParseResult<ArtifactIndex> index = ArtifactIndexReader.Read(Bytes("index.json"), limits, budget);
        ParseResult<IReadOnlyList<LedgerRow>> ledger = LedgerReader.ReadRows(Bytes("ledger.jsonl"), limits, budget);
        ParseResult<SealedRunManifest> manifest = ManifestReader.Read(Bytes("manifest.json"), limits, budget, limits.MaxEntries);
        ParseResult<IReadOnlyList<StatisticsEnvelope>> statistics = CapturedEvidenceReader.ReadStatistics(Bytes("statistics.jsonl"), "statistics.jsonl", limits, budget);
        ParseResult<IReadOnlyList<LifecycleEvent>> lifecycle = CapturedEvidenceReader.ReadLifecycle(Bytes("request-lifecycle.jsonl"), "request-lifecycle.jsonl", limits, budget);
        ParseResult<CapturedCapabilities> capabilities = CapturedEvidenceReader.ReadCapabilities(Bytes("capabilities.json"), "capabilities.json", limits, budget, captureOnly: true);
        Assert.True(index.IsSuccess); Assert.True(ledger.IsSuccess); Assert.True(manifest.IsSuccess);
        Assert.True(statistics.IsSuccess); Assert.True(lifecycle.IsSuccess); Assert.True(capabilities.IsSuccess);
        Assert.Equal(count, ledger.Value!.Count);
        ReportAssociation[] reports = statistics.Value!.Select(envelope => new ReportAssociation(envelope,
            NativeStatisticsReader.Read(System.Text.Encoding.UTF8.GetBytes(envelope.RawJson), "statistics.jsonl", limits, budget, limits.MaxArtifactBytes, 100))).ToArray();
        Assert.All(reports, report => Assert.True(report.Native.IsSuccess));
        return new(registered, index, ledger, manifest, statistics, lifecycle, capabilities, reports);
    }
}
#pragma warning restore CA1707

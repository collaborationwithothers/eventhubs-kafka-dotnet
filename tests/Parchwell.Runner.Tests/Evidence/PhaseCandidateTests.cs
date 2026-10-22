#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Evidence;
using Parchwell.Runner.Run.Execution;
using Parchwell.Runner.Tests.Cost;
using Parchwell.Verifier.Common.Results;
using Xunit;
using VerifierIdentity = Parchwell.Verifier.Common.Contracts.RunIdentity;

namespace Parchwell.Runner.Tests.Evidence;

/// <summary>Protects public summaries from inventing counts or promoting unaccepted worker evidence.</summary>
public sealed class PhaseCandidateTests
{
    private const string Commit = "1111111111111111111111111111111111111111";
    private const string Subscription = "01234567-89ab-cdef-0123-456789abcdef";

    [Fact]
    public async Task Given_FiveAcceptedCodecResults_When_CandidateBuilt_Then_PreservesCountsVersionsTimingsAndDiagnosticMaxima()
    {
        RepeatExecution[] executions = await RepeatsAsync();
        using JsonDocument candidate = await CandidateAsync(executions);
        JsonElement root = candidate.RootElement;
        Assert.Equal(55, root.GetProperty("counts").GetProperty("ok").GetInt64());
        Assert.Equal(10, root.GetProperty("counts").GetProperty("loss").GetInt64());
        Assert.Equal(15, root.GetProperty("counts").GetProperty("phantom").GetInt64());
        Assert.Equal(20, root.GetProperty("counts").GetProperty("reported_failure").GetInt64());
        Assert.Equal(25, root.GetProperty("counts").GetProperty("duplicate").GetInt64());
        Assert.Equal(30, root.GetProperty("counts").GetProperty("reorder").GetInt64());
        Assert.Equal("2.16.0", root.GetProperty("versions").GetProperty("confluent_kafka").GetString());
        Assert.Equal("2.16.0", root.GetProperty("versions").GetProperty("librdkafka").GetString());
        Assert.Equal("10.0.13", root.GetProperty("versions").GetProperty("dotnet").GetString());
        Assert.Equal("1", root.GetProperty("versions").GetProperty("workload_generator").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("versions").GetProperty("loadgen").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("versions").GetProperty("verifier").ValueKind);
        Assert.Equal(12.5m, root.GetProperty("timings").GetProperty("duration_seconds").GetDecimal());
        Assert.Equal("pass", root.GetProperty("gate").GetString());
        Assert.Equal(new[] { -184, 5 }, root.GetProperty("diagnostics").GetProperty("error_codes").EnumerateArray().Select(code => code.GetInt32()).ToArray());
        Assert.Equal(20001, root.GetProperty("diagnostics").GetProperty("max_throttle_ms").GetInt64());
        Assert.Equal(7, root.GetProperty("diagnostics").GetProperty("request_timeout_increment").GetInt64());
        Assert.Equal(9, root.GetProperty("diagnostics").GetProperty("disconnect_increment").GetInt64());
        Assert.Equal(1.501m, root.GetProperty("diagnostics").GetProperty("rtt_p99_ms").GetDecimal());
        Assert.Contains("diagnostics_window_maxima", Reasons(root));
        Assert.Contains("image_digests_unavailable", Reasons(root));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("costs").GetProperty("actual_gbp").ValueKind);
        Assert.False(root.GetProperty("costs").GetProperty("actual_available").GetBoolean());
        Assert.DoesNotContain("private-broker", root.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("private-snippet", root.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("incomplete")]
    public async Task Given_OneAcceptedDiagnosticResult_When_CandidateBuilt_Then_RetainsDiagnosticsButEveryCountIsNull(string status)
    {
        RepeatExecution[] executions = await RepeatsAsync(status);
        using JsonDocument candidate = await CandidateAsync(executions);
        Assert.All(candidate.RootElement.GetProperty("counts").EnumerateObject(), property => Assert.Equal(JsonValueKind.Null, property.Value.ValueKind));
        Assert.Equal(20001, candidate.RootElement.GetProperty("diagnostics").GetProperty("max_throttle_ms").GetInt64());
        Assert.Contains("counts_unavailable", Reasons(candidate.RootElement));
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("unfinished")]
    [InlineData("outcome")]
    [InlineData("reason")]
    [InlineData("missing")]
    public async Task Given_IncompleteDurableAcceptanceProof_When_CandidateBuilt_Then_NoPhaseCountsArePublished(string failure)
    {
        RepeatExecution[] executions = await RepeatsAsync();
        LedgerDocument document = Ledger(executions);
        PhaseRecord phase = document.Phases["C3"];
        RepeatRecord[] records = phase.Repeats.ToArray();
        records[0] = failure switch
        {
            "hash" => records[0] with { ResultHash = new string('f', 64) },
            "unfinished" => records[0] with { State = "interrupted" },
            "outcome" => records[0] with { Outcome = "invalid" },
            "reason" => records[0] with { Reason = "producer_history_not_unique" },
            _ => records[0]
        };
        document.Phases["C3"] = phase with { Repeats = failure == "missing" ? records[..4] : records };
        using JsonDocument candidate = await CandidateAsync(executions, document);
        Assert.All(candidate.RootElement.GetProperty("counts").EnumerateObject(), property => Assert.Equal(JsonValueKind.Null, property.Value.ValueKind));
        Assert.Contains("counts_unavailable", Reasons(candidate.RootElement));
    }

    [Fact]
    public async Task Given_CountTotalsOverflow_When_CandidateBuilt_Then_AllCountsStayUnavailable()
    {
        RepeatExecution[] executions = await RepeatsAsync(ok: long.MaxValue);
        using JsonDocument candidate = await CandidateAsync(executions);
        Assert.All(candidate.RootElement.GetProperty("counts").EnumerateObject(), property => Assert.Equal(JsonValueKind.Null, property.Value.ValueKind));
        Assert.Contains("counts_overflow", Reasons(candidate.RootElement));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_AggregateLongBoundary_When_CandidateBuilt_Then_PublishesExactTotalsOrAllNull(int neighbor)
    {
        long first = neighbor < 0 ? long.MaxValue - 1 : long.MaxValue;
        RepeatExecution[] executions = await RepeatsAsync(okCounts: [first, neighbor > 0 ? 1 : 0, 0, 0, 0]);
        using JsonDocument candidate = await CandidateAsync(executions);
        if (neighbor > 0)
        {
            Assert.All(candidate.RootElement.GetProperty("counts").EnumerateObject(), property => Assert.Equal(JsonValueKind.Null, property.Value.ValueKind));
            Assert.Contains("counts_overflow", Reasons(candidate.RootElement));
        }
        else Assert.Equal(first, candidate.RootElement.GetProperty("counts").GetProperty("ok").GetInt64());
    }

    [Theory]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(101)]
    public async Task Given_DistinctObservedCodeBoundary_When_CandidateBuilt_Then_PreservesAllOrMarksUnavailable(int count)
    {
        RepeatExecution[] executions = await RepeatsAsync(codes: Enumerable.Range(1, count).ToArray());
        using JsonDocument candidate = await CandidateAsync(executions);
        JsonElement codes = candidate.RootElement.GetProperty("diagnostics").GetProperty("error_codes");
        if (count <= 100) Assert.Equal(Enumerable.Range(1, count), codes.EnumerateArray().Select(code => code.GetInt32()));
        else
        {
            Assert.Equal(JsonValueKind.Null, codes.ValueKind);
            Assert.Contains("diagnostic_codes_unavailable", Reasons(candidate.RootElement));
        }
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("modified")]
    [InlineData("missing")]
    [InlineData("hash")]
    [InlineData("identity")]
    [InlineData("source")]
    public async Task Given_DurableCompletedRepeats_When_LocalOriginalsReplayed_Then_OnlyMatchingObservedEvidenceAuthorizesCounts(string state)
    {
        RepeatExecution[] executions = await RepeatsAsync();
        LedgerDocument document = Ledger(executions);
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (RepeatExecution execution in executions)
            {
                string repeatDirectory = Path.Combine(directory, execution.Identity.RunId);
                Directory.CreateDirectory(repeatDirectory);
                await File.WriteAllBytesAsync(Path.Combine(repeatDirectory, "result.json"), execution.Result, TestContext.Current.CancellationToken);
            }
            string path = Path.Combine(directory, executions[0].Identity.RunId, "result.json");
            if (state == "missing") File.Delete(path);
            if (state == "modified") await File.WriteAllBytesAsync(path, [0], TestContext.Current.CancellationToken);
            if (state == "hash") document.Phases["C3"].Repeats[0] = document.Phases["C3"].Repeats[0] with { ResultHash = new string('f', 64) };
            if (state is "identity" or "source")
            {
                JsonNode replaced = JsonNode.Parse(executions[0].Result)!;
                if (state == "identity") replaced["identity"]!["run_id"] = "other-run";
                else replaced["verifier_commit"] = new string('2', 40);
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(replaced);
                await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
                document.Phases["C3"].Repeats[0] = document.Phases["C3"].Repeats[0] with { ResultHash = Convert.ToHexStringLower(SHA256.HashData(bytes)) };
            }
            using JsonDocument candidate = await CandidateAsync([], document, directory);
            if (state == "valid")
            {
                Assert.Equal(new long[] { 55, 10, 15, 20, 25, 30 }, candidate.RootElement.GetProperty("counts").EnumerateObject().Select(field => field.Value.GetInt64()).ToArray());
                Assert.Equal(Commit, candidate.RootElement.GetProperty("source_commit").GetString());
                Assert.Equal("2.16.0", candidate.RootElement.GetProperty("versions").GetProperty("confluent_kafka").GetString());
            }
            else
            {
                Assert.All(candidate.RootElement.GetProperty("counts").EnumerateObject(), property => Assert.Equal(JsonValueKind.Null, property.Value.ValueKind));
                Assert.Contains("repeat_evidence_unavailable", Reasons(candidate.RootElement));
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("tampered")]
    [InlineData("missing")]
    [InlineData("source")]
    public async Task Given_PrivateImageReceipts_When_CandidateBuilt_Then_OnlyHashVerifiedMatchingSourceDigestsArePublished(string state)
    {
        RepeatExecution[] executions = await RepeatsAsync();
        LedgerDocument document = Ledger(executions);
        var store = new PersistentTestStore();
        var exporter = new EvidenceExporter(store, TimeProvider.System);
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            const string relative = "private-provenance/image-receipts.json";
            string path = Path.Combine(directory, "images.json");
            byte[] images = JsonSerializer.SerializeToUtf8Bytes(new
            {
                loadgen = new { source_commit = state == "source" ? new string('2', 40) : Commit, digest = "sha256:" + new string('a', 64), private_resource_id = "private-resource" },
                verifier = new { source_commit = Commit, digest = "sha256:" + new string('b', 64), client_id = "private-client" }
            });
            await File.WriteAllBytesAsync(path, images, TestContext.Current.CancellationToken);
            PrivateExportReceipt receipt = await exporter.ExportAsync("env", [new(relative, path)], TestContext.Current.CancellationToken);
            document.Phases["C3"] = document.Phases["C3"] with { PrivateExport = receipt };
            if (state == "tampered") store.Objects[("evidence", "A01/env/" + relative)] = new([0], "tampered");
            if (state == "missing") store.Objects.Remove(("evidence", "A01/env/" + relative));
            using JsonDocument candidate = await CandidateAsync(executions, document, exporter: exporter);
            JsonElement digests = candidate.RootElement.GetProperty("image_digests");
            if (state == "valid")
            {
                Assert.Equal("sha256:" + new string('a', 64), digests.GetProperty("loadgen").GetString());
                Assert.Equal("sha256:" + new string('b', 64), digests.GetProperty("verifier").GetString());
                Assert.DoesNotContain("image_digests_unavailable", Reasons(candidate.RootElement));
            }
            else Assert.All(digests.EnumerateObject(), field => Assert.Equal(JsonValueKind.Null, field.Value.ValueKind));
            Assert.DoesNotContain("private-resource", candidate.RootElement.GetRawText(), StringComparison.Ordinal);
            Assert.DoesNotContain("private-client", candidate.RootElement.GetRawText(), StringComparison.Ordinal);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(16_777_215, true)]
    [InlineData(16_777_216, true)]
    [InlineData(16_777_217, false)]
    public async Task Given_LocalOriginalAtResultByteBoundary_When_Replayed_Then_UsesExistingWorkerLimit(int length, bool allowed)
    {
        RepeatExecution[] executions = await RepeatsAsync();
        LedgerDocument document = Ledger(executions);
        byte[] padded = new byte[length];
        padded.AsSpan().Fill((byte)' ');
        executions[0].Result.CopyTo(padded, 0);
        WorkerIdentity worker = executions[0].Identity;
        var identity = new VerifierIdentity(worker.ScenarioId, worker.ConfigurationId, worker.RunId, worker.InstanceId, worker.ExpectedInstanceId, worker.ClientId);
        string receipt = Encoding.UTF8.GetString(await WorkerReceipts.WriteAsync("verify", identity, Commit, 0));
        Assert.Equal(allowed, WorkerOutcome.Reconcile(new(0, receipt, ""), padded, worker, Commit).CountsAccepted);
        document.Phases["C3"].Repeats[0] = document.Phases["C3"].Repeats[0] with { ResultHash = Convert.ToHexStringLower(SHA256.HashData(padded)) };
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (RepeatExecution execution in executions)
            {
                string repeatDirectory = Path.Combine(directory, execution.Identity.RunId);
                Directory.CreateDirectory(repeatDirectory);
                await File.WriteAllBytesAsync(Path.Combine(repeatDirectory, "result.json"), execution == executions[0] ? padded : execution.Result,
                    TestContext.Current.CancellationToken);
            }
            using JsonDocument candidate = await CandidateAsync([], document, directory);
            if (allowed) Assert.Equal(55, candidate.RootElement.GetProperty("counts").GetProperty("ok").GetInt64());
            else
            {
                Assert.All(candidate.RootElement.GetProperty("counts").EnumerateObject(), property => Assert.Equal(JsonValueKind.Null, property.Value.ValueKind));
                Assert.Contains("repeat_evidence_unavailable", Reasons(candidate.RootElement));
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Given_GateRecordedAfterPhaseCandidate_When_Rebuilt_Then_RefreshesGateFromDurableReceipt()
    {
        RepeatExecution[] executions = await RepeatsAsync();
        LedgerDocument document = Ledger(executions);
        GateReceipt? gate = document.Phases["probe"].GateReceipt;
        document.Phases["probe"] = document.Phases["probe"] with { GateReceipt = null };
        using JsonDocument before = await CandidateAsync(executions, document);
        Assert.Equal(JsonValueKind.Null, before.RootElement.GetProperty("gate").ValueKind);
        Assert.Contains("gate_unavailable", Reasons(before.RootElement));
        document.Phases["probe"] = document.Phases["probe"] with { GateReceipt = gate };
        using JsonDocument after = await CandidateAsync(executions, document);
        using JsonDocument retained = await CandidateAsync(executions, document);
        Assert.Equal("pass", after.RootElement.GetProperty("gate").GetString());
        Assert.Equal("pass", retained.RootElement.GetProperty("gate").GetString());
        Assert.DoesNotContain("gate_unavailable", Reasons(after.RootElement));
    }

    [Fact]
    public async Task Given_FailedRepeatWithSuccessfulCleanup_When_Refreshed_Then_ExecutionStatusStaysFailed()
    {
        RepeatExecution[] executions = await RepeatsAsync();
        LedgerDocument document = Ledger(executions);
        PhaseRecord phase = document.Phases["C3"] with { PrivateExport = new("manifest", "hash", 0, 0) };
        phase.Repeats[0] = phase.Repeats[0] with { State = "interrupted" };
        document.Phases["C3"] = phase;
        using JsonDocument candidate = await CandidateAsync(executions, document, status: PhaseCandidate.DurableStatus(phase));
        Assert.Equal("failed", candidate.RootElement.GetProperty("status").GetString());
        Assert.All(candidate.RootElement.GetProperty("counts").EnumerateObject(), property => Assert.Equal(JsonValueKind.Null, property.Value.ValueKind));
    }

    [Fact]
    public async Task Given_NoOriginalsOrExecutions_When_CandidateBuilt_Then_DurableLabelsAloneNeverInventMeasurements()
    {
        RepeatExecution[] executions = await RepeatsAsync();
        using JsonDocument candidate = await CandidateAsync([], Ledger(executions));
        Assert.All(candidate.RootElement.GetProperty("counts").EnumerateObject(), property => Assert.Equal(JsonValueKind.Null, property.Value.ValueKind));
        Assert.All(candidate.RootElement.GetProperty("diagnostics").EnumerateObject(), property => Assert.Equal(JsonValueKind.Null, property.Value.ValueKind));
        Assert.Contains("repeat_evidence_unavailable", Reasons(candidate.RootElement));
    }

    private static async Task<JsonDocument> CandidateAsync(RepeatExecution[] executions, LedgerDocument? document = null,
        string? outputDirectory = null, EvidenceExporter? exporter = null, string status = "completed")
    {
        var builder = new PhaseCandidate(exporter ?? new EvidenceExporter(new PersistentTestStore(), TimeProvider.System), TimeProvider.System);
        using JsonDocument runtime = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(new { subscription_guid = Subscription }));
        return JsonDocument.Parse(await builder.CreateAsync(runtime.RootElement, document ?? Ledger(executions), "C3", status, executions, outputDirectory,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    private static LedgerDocument Ledger(RepeatExecution[] executions)
    {
        var started = new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
        RepeatRecord[] records = executions.Select((execution, index) => new RepeatRecord(index + 1, execution.Identity.RunId, "completed",
            execution.Identity.InstanceId, ResultHash: Convert.ToHexStringLower(SHA256.HashData(execution.Result)), Outcome: execution.Outcome.Status, Reason: execution.Outcome.Reason)).ToArray();
        var phase = new PhaseRecord("destroyed", "reservation", "env", 1, 1, 2, null, null, started, started.AddHours(4), records,
            null, new("env", started.AddTicks(125_000_000), true, "destroyed"), null);
        return new(1, "A01", "GBP", 1, Commit, 0, [], new(StringComparer.Ordinal)
        { ["probe"] = phase with { GateReceipt = new("pass", new string('a', 64), Commit) }, ["C0"] = phase, ["C3"] = phase });
    }

    private static async Task<RepeatExecution[]> RepeatsAsync(string? diagnosticStatus = null, long ok = 11,
        IReadOnlyList<int>? codes = null, IReadOnlyList<long>? okCounts = null)
    {
        var repeats = new List<RepeatExecution>();
        for (int ordinal = 1; ordinal <= 5; ordinal++)
        {
            string run = "env-r" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var identity = new VerifierIdentity("A01", "C3", run, "private-pod", "private-pod", "private-pod");
            var worker = new WorkerIdentity(identity.ScenarioId, identity.ConfigurationId, identity.RunId, identity.InstanceId, identity.ExpectedInstanceId, identity.ClientId);
            VerifierResultStatus status = ordinal == 1 && diagnosticStatus is not null
                ? diagnosticStatus == "invalid" ? VerifierResultStatus.Invalid : VerifierResultStatus.Incomplete : VerifierResultStatus.CompleteValid;
            var result = new VerifierResult(1, identity, "1", new Dictionary<string, string>(StringComparer.Ordinal)
                { ["workload_generator"] = "1", ["confluent_kafka"] = "2.16.0", ["librdkafka"] = "2.16.0", ["dotnet"] = "10.0.13" },
                Commit, "2.16.0", "2.16.0", status, status == VerifierResultStatus.CompleteValid ? [] : [12], null, null, null, null,
                status == VerifierResultStatus.CompleteValid ? new(okCounts?[ordinal - 1] ?? ok, 2, 3, 4, 5, 6) : null, null, null, null,
                new(codes ?? [-184, 5, -184], [new("private-broker", 0, 1, 7, 9, 20001, 1501)]), [],
                [new(29, "increment_overflow", "private-artifact", 1, "private-field", "private-snippet")]);
            byte[] bytes = VerifierResultCodec.Write(result, new(65536, 10, 1024));
            int exit = status == VerifierResultStatus.CompleteValid ? 0 : 1;
            string receipt = Encoding.UTF8.GetString(await WorkerReceipts.WriteAsync("verify", identity, Commit, exit));
            AcceptedWorkerOutcome outcome = WorkerOutcome.Reconcile(new(exit, receipt, ""), bytes, worker, Commit);
            Assert.True(outcome.ExecutionCompleted);
            repeats.Add(new(worker, outcome, new(new Dictionary<string, byte[]>(StringComparer.Ordinal)), bytes, []));
        }
        return repeats.ToArray();
    }

    private static string?[] Reasons(JsonElement candidate) => candidate.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString()).ToArray();
}

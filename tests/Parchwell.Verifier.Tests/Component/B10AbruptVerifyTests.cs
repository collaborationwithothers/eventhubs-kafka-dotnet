using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Window;
using Xunit;
using Parchwell.Verifier.Tests.Support;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707
[Collection("B10 abrupt verifier")]
[Trait("Tier", "Docker")]
public sealed class B10AbruptVerifyTests(B10AbruptVerifyKafkaFixture kafka) : IClassFixture<B10AbruptVerifyKafkaFixture>
{
    private static readonly string[] Problems = ["b10_abrupt_verify_start_line_invalid", "b10_abrupt_verify_first_evidence_invalid",
        "b10_abrupt_verify_successor_absent", "b10_abrupt_verify_successor_evidence_invalid", "b10_abrupt_verify_window_incomplete", "b10_abrupt_verify_probe_record_malformed", "b10_abrupt_verify_timeout_evidence_invalid", "b10_abrupt_verify_record_unjoined", "b10_abrupt_verify_end_line_advanced"];
    private static readonly InputLimits Limits = new(32, 268435456, 2147483648, 65536, 4096, 10000, 4096);
    private static readonly string[] CopyCounts = ["copy_count", "original_copies", "resend_copies", "other_copies", "pair_count"];
    private static readonly string[] OriginalAndOther = ["original", "other"];
    private static readonly string[] EnvelopeMembers = ["data", "dialect", "identity", "kind", "purpose", "schema_version", "scientific_admissible", "synthetic"];
    private static readonly string[] IdentityMembers = ["configuration_id", "image_reference", "instance_id", "job_name", "phase", "pod_name", "pod_uid", "predecessor_instance_id", "repeat", "run_id", "scenario_id", "source_commit"];
    private static readonly string[] DataMembers = ["arms", "comparison", "eligibility", "eligibility_reasons", "end_line", "finality", "first_instance_id", "native_errors", "problem_details", "problems", "start_line", "successor_instance_id", "timeout", "timeout_instance_id", "window_complete", "window_limit"];
    private static readonly string[] ArmMembers = ["arm", "key", "lost_resends", "members", "originals_seen", "pair_count", "resend_invoked", "resends_seen", "unconfirmed_count", "unreported_attempts", "unreported_seen", "unreported_unseen", "witness"];
    private static readonly string[] MemberFields = ["copies", "copies_truncated", "copy_count", "original_attempt_id", "original_copies", "other_copies", "pair_count", "payload_sha256", "resend_attempt_id", "resend_copies", "resend_lost", "resend_terminal", "sequence"];
    private static readonly string[] CopyFields = ["attempt_id", "instance_id", "offset", "partition", "role"];
    private static readonly string[] TerminalFields = ["delivered", "error_code", "persistence_status"];
    private static readonly string[] CoordinateFields = ["offset", "partition"];
    private static readonly string[] ArmNames = ["same_instance", "cross_restart"];

    [Fact]
    public async Task Given_IndependentBuilderAndActualPublisher_When_ComparingEveryKind_Then_MemberSetsMatch_T0()
    {
        Dictionary<string, byte[]> real = await B10AbruptVerifyEvidence.ExportAsync();
        var built = new B10AbruptVerifyEvidence();
        Dictionary<string, string[][]> Shapes(Dictionary<string, byte[]> files)
        {
            var shapes = new Dictionary<string, string[][]>(StringComparer.Ordinal);
            foreach ((string name, byte[] bytes) in files)
            {
                if (name is not ("first/journal.jsonl" or "first/registration.json" or "first/abrupt-manifest.json" or "first/termination.json" or "successor/registration.json" or "successor/manifest.json" or "successor/completion.json")) continue;
                IEnumerable<string> documents = name.EndsWith(".jsonl", StringComparison.Ordinal)
                    ? Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries) : [Encoding.UTF8.GetString(bytes)];
                foreach (string document in documents)
                {
                    JsonObject root = JsonNode.Parse(document)!.AsObject();
                    string[][] shape = [Names(root), Names(root["identity"]!.AsObject()), Names(root["data"]!.AsObject())];
                    string kind = root["kind"]!.GetValue<string>();
                    if (shapes.TryGetValue(kind, out string[][]? previous))
                        for (int index = 0; index < shape.Length; index++) Assert.Equal(previous[index], shape[index]);
                    else shapes.Add(kind, shape);
                }
            }
            return shapes;
        }
        var expected = Shapes(real); var actual = Shapes(built.Files);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (string kind in expected.Keys)
            for (int index = 0; index < 3; index++) Assert.Equal(expected[kind][index], actual[kind][index]);
        Assert.Equal(LedgerMembers(real), LedgerMembers(built.Files));
        Dictionary<string, byte[]> ordinary = await B10AbruptVerifyEvidence.ExportTimeoutAsync();
        foreach ((string name, byte[] bytes) in ordinary)
        {
            byte[] independent = built.Files["timeout/" + name["producer/pod-first/".Length..]];
            string[] producedRows = Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string[] independentRows = Encoding.UTF8.GetString(independent).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (producedRows.Length == 0) { Assert.Empty(independentRows); continue; }
            // Empty timeout cohorts still use the ordinary row contract exercised by the three-row T2 case.
            if (independentRows.Length == 0 && name.EndsWith("ledger.jsonl", StringComparison.Ordinal)) independentRows = [B10AbruptVerifyEvidence.TimeoutRow("t1", "k1", 0).ToJsonString()];
            foreach (string row in independentRows) Assert.Equal(Names(JsonNode.Parse(producedRows[0])!.AsObject()), Names(JsonNode.Parse(row)!.AsObject()));
        }
    }

    [Fact]
    public async Task Given_RealPublisherBytesAndEmptyWindow_When_HostRuns_Then_AllMembersHaveKnownZeroCopies_T1()
    {
        await kafka.ResetAsync();
        Dictionary<string, byte[]> real = await B10AbruptVerifyEvidence.ExportAsync();
        Dictionary<string, byte[]> timeout = await B10AbruptVerifyEvidence.ExportTimeoutAsync();
        B10AbruptVerifyEvidence combined = B10AbruptVerifyEvidence.FromExports(real, timeout);
        using var run = new B10AbruptVerifyHarness(kafka.Bootstrap, combined.Files);
        run.Configuration["Verifier:NamespaceName"] = "unit-test";
        await SuccessAsync(run);
        Assert.Empty(run.Data["eligibility_reasons"]!.AsArray());
        JsonObject termination = JsonNode.Parse(real["first/termination.json"])!["data"]!.AsObject();
        JsonArray invocations = JsonNode.Parse(real["successor/completion.json"])!["data"]!["invocations"]!.AsArray();
        foreach ((string armName, string set) in new[] { ("same_instance", "control_unconfirmed_attempt_ids"), ("cross_restart", "kill_unconfirmed_attempt_ids") })
        {
            JsonObject arm = Arm(run, armName);
            Assert.Equal(Strings(termination[set]!), arm["members"]!.AsArray().Select(member => Text(member!["original_attempt_id"])));
            Assert.All(arm["members"]!.AsArray(), member =>
            {
                foreach (string count in CopyCounts) Assert.Equal(0, Count(member!, count));
                Assert.Empty(member!["copies"]!.AsArray()); Assert.False(member["copies_truncated"]!.GetValue<bool>());
            });
            Assert.Null(arm["witness"]); Assert.Equal(0, Count(arm, "originals_seen")); Assert.Equal(0, Count(arm, "resends_seen"));
        }
        Assert.Equal(invocations.Select(invocation => Text(invocation!["attempt_id"])), Arm(run, "cross_restart")["members"]!.AsArray().Select(member => Text(member!["resend_attempt_id"])));
        var frames = Encoding.UTF8.GetString(real["first/journal.jsonl"]).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonNode.Parse(line)!);
        Assert.Equal(frames.Where(frame => Text(frame["kind"]) == "prepared" && Text(frame["data"]!["role"]) == "resend").Select(frame => Text(frame["data"]!["attempt_id"])),
            Arm(run, "same_instance")["members"]!.AsArray().Select(member => Text(member!["resend_attempt_id"])));
        Assert.Equal(1, run.OutputReadbacks); Assert.Equal(1, run.Creates);
        JsonNode[] timeoutRows = Encoding.UTF8.GetString(timeout["producer/pod-first/publication/ledger.jsonl"]).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonNode.Parse(line)!).ToArray();
        Assert.Equal(0, Count(run.Data["timeout"]!, "ok"));
        Assert.Equal(timeoutRows.Count(row => row["delivered"]!.GetValue<bool>()), Count(run.Data["timeout"]!, "loss"));
        Assert.Equal(timeoutRows.Count(row => !row["delivered"]!.GetValue<bool>()), Count(run.Data["timeout"]!, "reported_failure"));
        Assert.Equal(termination["kill_unconfirmed_attempt_ids"]!.AsArray().Count, Count(Arm(run, "cross_restart"), "unreported_attempts"));
        Assert.Equal(0, Count(Arm(run, "cross_restart"), "unreported_seen"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_CrossRestartPairOrAbsentOriginal_When_HostRuns_Then_CountsAndWitnessMatch_T2_T3(bool original)
    {
        await kafka.ResetAsync(); using var run = NewRun();
        if (original) await run.ProduceAsync(0, 10);
        await run.ProduceAsync(0, 12, instance: B10AbruptVerifyEvidence.Second, attempt: "r4");
        await SuccessAsync(run, 8);
        JsonObject arm = Arm(run, "cross_restart"), member = Member(arm);
        AssertCounts(member, original ? 2 : 1, original ? 1 : 0, 1, 0, original ? 1 : 0);
        Assert.Equal(original ? 1 : 0, Count(arm, "originals_seen")); Assert.Equal(1, Count(arm, "resends_seen"));
        if (original) AssertWitness(arm, "c4", "r4", 0, 10, 0, 12); else Assert.Null(arm["witness"]);
    }

    [Fact]
    public async Task Given_TwoOriginalsAndOneResend_When_HostRuns_Then_ProductAndLexicographicWitnessAreComplete_T4()
    {
        await kafka.ResetAsync(); using var run = NewRun();
        await run.ProduceAsync(1, 5); await run.ProduceAsync(0, 9);
        await run.ProduceAsync(0, 12, instance: B10AbruptVerifyEvidence.Second, attempt: "r4");
        await SuccessAsync(run, 8);
        AssertCounts(Member(Arm(run, "cross_restart")), 3, 2, 1, 0, 2);
        AssertWitness(Arm(run, "cross_restart"), "c4", "r4", 0, 9, 0, 12);
    }

    [Fact]
    public async Task Given_ControlPairWarmupAndConfirmedOriginal_When_HostRuns_Then_OnlyUnconfirmedMemberMatches_T5()
    {
        await kafka.ResetAsync(); using var run = NewRun();
        await run.ProduceAsync(2, 0, "same_instance", attempt: "warmup", sequence: -1);
        await run.ProduceAsync(2, 1, "same_instance", attempt: "s3", sequence: 3);
        await run.ProduceAsync(2, 2, "same_instance", attempt: "s5", sequence: 5);
        await run.ProduceAsync(2, 3, "same_instance", attempt: "s4");
        await run.ProduceAsync(2, 4, "same_instance", attempt: "t4");
        await SuccessAsync(run, 8); AssertCounts(Member(Arm(run, "same_instance")), 2, 1, 1, 0, 1);
        AssertWitness(Arm(run, "same_instance"), "s4", "t4", 2, 3, 2, 4);
    }

    [Theory]
    [InlineData("absent", 3)]
    [InlineData("journal-hash", 4)]
    [InlineData("two-invocations", 4)]
    [InlineData("ledger-missing", 4)]
    [InlineData("predecessor", 4)]
    [InlineData("extra-file", 4)]
    public async Task Given_UnavailableSuccessor_When_HostRuns_Then_OriginalAndOtherStayKnownWhileResendFieldsAreNull_T6_T7(string defect, int problem)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        switch (defect)
        {
            case "absent": foreach (string name in evidence.Files.Keys.Where(name => name.StartsWith("successor/", StringComparison.Ordinal)).ToArray()) evidence.Files.Remove(name); break;
            case "journal-hash": evidence.Change("successor/completion.json", root => root["data"]!["journal"]!["sha256"] = new string('f', 64)); break;
            case "two-invocations": evidence.Change("successor/completion.json", root => root["data"]!["invocations"]!.AsArray().Add(root["data"]!["invocations"]![0]!.DeepClone())); break;
            case "ledger-missing": evidence.Files["successor/ledger.jsonl"] = B10AbruptVerifyEvidence.Encode(new { attempt_id = "unrelated", completion_source = "callback", delivered = true, error_code = 0, persistence_status = 2 }, newline: true); evidence.RehashSuccessor(); break;
            case "predecessor": evidence.Change("successor/registration.json", root => root["identity"]!["predecessor_instance_id"] = "wrong"); evidence.RehashSuccessor(); break;
            case "extra-file": evidence.Files["successor/extra.json"] = [123, 125]; break;
        }
        using var run = NewRun(evidence); await run.ProduceAsync(0, 10); await run.ProduceAsync(0, 12, instance: B10AbruptVerifyEvidence.Second, attempt: "r4");
        await SuccessAsync(run, problem, 8); Assert.Null(run.Data["successor_instance_id"]);
        JsonObject arm = Arm(run, "cross_restart"), member = Member(arm);
        foreach (string field in new[] { "resend_attempt_id", "resend_terminal", "resend_copies", "pair_count" }) Assert.Null(member[field]);
        foreach (string field in new[] { "resend_invoked", "resends_seen", "pair_count", "witness" }) Assert.Null(arm[field]);
        Assert.Equal(2, Count(member, "copy_count")); Assert.Equal(1, Count(member, "original_copies")); Assert.Equal(1, Count(member, "other_copies"));
        Assert.Equal(1, Count(arm, "originals_seen")); Assert.False(member["copies_truncated"]!.GetValue<bool>());
        Assert.Equal(OriginalAndOther, member["copies"]!.AsArray().Select(copy => Text(copy!["role"])));
        Assert.True(run.Data["window_complete"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("no-kill")]
    [InlineData("exit-136")]
    [InlineData("kill-set")]
    [InlineData("sequence-gap")]
    [InlineData("manifest-hash")]
    [InlineData("image")]
    [InlineData("two-first")]
    [InlineData("journal-oversized")]
    [InlineData("journal-4097")]
    [InlineData("unprepared-accepted")]
    [InlineData("cut-set")]
    [InlineData("incomplete-listing")]
    public async Task Given_OneInvalidFirstPredicate_When_HostRuns_Then_FirstProblemSuppressesSuccessorEvaluation_T8(string defect)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        switch (defect)
        {
            case "no-kill": evidence.Change("first/termination.json", root => root["data"]!["termination"] = "no_kill"); break;
            case "exit-136": evidence.Change("first/termination.json", root => root["data"]!["exit_code_local_observation"] = 136); break;
            case "kill-set": evidence.Change("first/termination.json", root => root["data"]!["kill_unconfirmed_attempt_ids"] = new JsonArray("c3")); break;
            case "sequence-gap": evidence.Frames[1]["sequence"] = 3L; evidence.Rehash(); break;
            case "manifest-hash": evidence.Change("first/termination.json", root => root["data"]!["manifest"]!["sha256"] = new string('f', 64)); break;
            case "image": evidence.Change("first/registration.json", root => root["identity"]!["image_reference"] = "registry.invalid/other@sha256:" + new string('d', 64)); break;
            case "two-first": break;
            case "journal-oversized":
                evidence.WriteJournal();
                byte[] oversized = new byte[16777217]; evidence.Files["first/journal.jsonl"].CopyTo(oversized, 0);
                evidence.Files["first/journal.jsonl"] = oversized; evidence.Rehash(writeJournal: false); break;
            case "journal-4097": PadJournal(evidence, 4097); break;
            case "unprepared-accepted": evidence.Frames.First(frame => Text(frame["kind"]) == "accepted")["data"]!["attempt_id"] = "unprepared"; evidence.Rehash(); break;
            case "cut-set": evidence.Frames.Single(frame => Text(frame["kind"]) == "cut")["data"]!["unconfirmed_attempt_ids"] = new JsonArray(); evidence.Rehash(); break;
        }
        evidence.RehashSuccessor();
        using var run = NewRun(evidence); run.IncompleteListing = defect == "incomplete-listing";
        if (defect == "two-first")
        {
            string prefix = B10AbruptVerifyEvidence.Prefix + B10AbruptVerifyEvidence.First + "/";
            foreach ((string name, byte[] bytes) in run.Ledger.Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
            {
                string copy = name.Replace(B10AbruptVerifyEvidence.First, "synthetic-third", StringComparison.Ordinal);
                run.Ledger[copy] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace(B10AbruptVerifyEvidence.First, "synthetic-third", StringComparison.Ordinal));
            }
        }
        await SuccessAsync(run, 2); AssertFirstUnavailable(run);
    }

    [Fact]
    public async Task Given_MissingStartCompletion_When_HostRuns_Then_MembersRemainAndEveryCopyFieldIsUnknown_T9()
    {
        await kafka.ResetAsync(); using var run = NewRun(); run.Verifier.Remove(PhasePaths.StartLineCompletion("synthetic", "C1", 1));
        await SuccessAsync(run, 1); Assert.Null(run.Data["start_line"]); Assert.Null(run.Data["end_line"]); AssertUnknownCopies(run);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task Given_RecordBudgetBelowAtOrAboveWindowSize_When_HostRuns_Then_OnlyIncompleteWindowLosesCounts_T10(int budget, bool complete)
    {
        await kafka.ResetAsync(); using var run = NewRun(); run.Configuration["Verifier:MaxConsumedRecords"] = budget.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await run.ProduceAsync(0, 0); await run.ProduceAsync(0, 1);
        await SuccessAsync(run, complete ? [] : [5]);
        if (complete) Assert.Equal(2, Count(Member(Arm(run, "cross_restart")), "copy_count")); else AssertUnknownCopies(run);
    }

    [Fact]
    public async Task Given_DuplicateHeaderAndNullProbeValue_When_HostRuns_Then_MalformedCopyIsOtherAndTombstoneIsExcluded_T11()
    {
        await kafka.ResetAsync(); using var run = NewRun();
        await run.ProduceAsync(0, 0, duplicateAttempt: true); await run.ProduceAsync(0, 1, nullValue: true);
        await SuccessAsync(run, 6); JsonObject member = Member(Arm(run, "cross_restart"));
        AssertCounts(member, 1, 0, 0, 1, 0);
        JsonNode copy = Assert.Single(member["copies"]!.AsArray())!; Assert.Null(copy["attempt_id"]); Assert.Equal("other", Text(copy["role"]));
    }

    [Fact]
    public async Task Given_RecordsAtFourWindowEdges_When_HostRuns_Then_HalfOpenBoundsIncludeOnlyStartAndEndMinusOne_T12()
    {
        await kafka.ResetAsync(); using var run = NewRun(); await run.ProduceAsync(0, 0);
        run.CaptureStart(); await run.ProduceAsync(0, 1); await run.ProduceAsync(0, 2);
        run.AfterEndQuery = () => run.ProduceAsync(0, 3).GetAwaiter().GetResult();
        await SuccessAsync(run, 9);
        Assert.Empty(run.Data["eligibility_reasons"]!.AsArray());
        foreach (JsonNode? arm in run.Data["arms"]!.AsArray())
        {
            Assert.Null(arm!["lost_resends"]);
            Assert.All(arm["members"]!.AsArray(), member => Assert.Null(member!["resend_lost"]));
        }
        Assert.Equal(new long[] { 1, 2 }, Member(Arm(run, "cross_restart"))["copies"]!.AsArray().Select(copy => Count(copy!, "offset")));
    }

    [Theory]
    [InlineData("ScenarioId", "B04")]
    [InlineData("ConfigurationId", "C0")]
    [InlineData("Phase", "C0")]
    [InlineData("Repeat", "2")]
    [InlineData("VerifierCommit", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    public async Task Given_InvalidFixedOption_When_HostStarts_Then_ExitOneBeforeAnyStorageAccess_T13(string field, string value)
    {
        await kafka.ResetAsync(); using var run = NewRun(); run.Configuration["Verifier:" + field] = value;
        if (field == "ScenarioId") run.Configuration["Telemetry:ScenarioId"] = value;
        Assert.Equal(1, await run.RunAsync()); Assert.Equal(0, run.Reads); Assert.Equal(0, run.Listings); Assert.Equal(0, run.Creates);
        Assert.False(run.Verifier.ContainsKey(B10AbruptVerifyHarness.Output));
    }

    [Fact]
    public async Task Given_DiagnosticResult_When_ReadByScienceOrPublishedTwice_Then_ReadersRejectAndSecondHostFails_T14()
    {
        await kafka.ResetAsync(); using var run = NewRun(); await run.ProduceAsync(0, 10); await run.ProduceAsync(0, 12, instance: B10AbruptVerifyEvidence.Second, attempt: "r4");
        await SuccessAsync(run, 8); byte[] bytes = run.Verifier[B10AbruptVerifyHarness.Output]; JsonObject root = run.Result;
        Assert.Equal(EnvelopeMembers, Names(root));
        Assert.Equal(3, root["schema_version"]!.GetValue<int>()); Assert.Equal("b10_abrupt", Text(root["dialect"])); Assert.Equal("diagnostic_trial", Text(root["purpose"]));
        Assert.True(root["synthetic"]!.GetValue<bool>()); Assert.False(root["scientific_admissible"]!.GetValue<bool>()); Assert.Equal("nonfinal_result", Text(root["kind"]));
        Assert.Equal("nonfinal", Text(run.Data["finality"])); Assert.Equal("copies_after_end_line_not_observed", Text(run.Data["window_limit"]));
        Assert.Equal("registry.invalid/verifier@sha256:" + new string('c', 64), Text(root["identity"]!["image_reference"]));
        Assert.Equal("synthetic-verifier", Text(root["identity"]!["instance_id"])); Assert.Null(root["identity"]!["predecessor_instance_id"]);
        JsonObject identity = root["identity"]!.AsObject();
        Assert.Equal(IdentityMembers, Names(identity)); Assert.Equal(DataMembers, Names(run.Data));
        Assert.Equal("synthetic", Text(identity["run_id"])); Assert.Equal("B10", Text(identity["scenario_id"]));
        Assert.Equal("C1", Text(identity["configuration_id"])); Assert.Equal("C1", Text(identity["phase"])); Assert.Equal(1, Count(identity, "repeat"));
        Assert.Equal(B10AbruptVerifyEvidence.Commit, Text(identity["source_commit"])); Assert.Equal("synthetic-c1-r1-b10-abrupt-verify", Text(identity["job_name"]));
        Assert.Equal("synthetic-verifier", Text(identity["pod_name"])); Assert.Equal("synthetic-verifier-uid", Text(identity["pod_uid"]));
        Assert.Equal(B10AbruptVerifyEvidence.First, Text(run.Data["first_instance_id"])); Assert.Equal(B10AbruptVerifyEvidence.Second, Text(run.Data["successor_instance_id"]));
        Assert.Equal(ArmNames, run.Data["arms"]!.AsArray().Select(arm => Text(arm!["arm"])));
        Assert.Empty(run.Data["problem_details"]!.AsArray());
        foreach (JsonNode? arm in run.Data["arms"]!.AsArray())
        {
            Assert.Equal(ArmMembers, Names(arm!.AsObject())); Assert.Equal(B10AbruptVerifyEvidence.Key(Text(arm["arm"])!), Text(arm["key"]));
            Assert.Equal(1, Count(arm, "unconfirmed_count")); Assert.Equal(1, Count(arm, "resend_invoked"));
            JsonObject member = Member(arm.AsObject()); Assert.Equal(MemberFields, Names(member)); Assert.Equal(4, Count(member, "sequence"));
            Assert.Equal(B10AbruptVerifyEvidence.Hash(B10AbruptVerifyEvidence.Payload), Text(member["payload_sha256"]));
            JsonObject terminal = member["resend_terminal"]!.AsObject(); Assert.Equal(TerminalFields, Names(terminal));
            Assert.True(terminal["delivered"]!.GetValue<bool>()); Assert.Equal(0, Count(terminal, "error_code")); Assert.Equal(2, Count(terminal, "persistence_status"));
            foreach (JsonNode? copy in member["copies"]!.AsArray()) Assert.Equal(CopyFields, Names(copy!.AsObject()));
        }
        foreach (string line in new[] { "start_line", "end_line" })
        {
            Assert.Equal(Enumerable.Range(0, 3).Select(value => (long)value), run.Data[line]!.AsArray().Select(coordinate => Count(coordinate!, "partition")));
            foreach (JsonNode? coordinate in run.Data[line]!.AsArray()) Assert.Equal(CoordinateFields, Names(coordinate!.AsObject()));
        }
        Assert.ThrowsAny<FormatException>(() => PhaseWireCodec.ReadRegistration(bytes)); Assert.ThrowsAny<FormatException>(() => PhaseWireCodec.ReadReceipt(bytes));
        Assert.False(VerifierResultCodec.Read(bytes, "diagnostic", Limits, 100).IsSuccess);
        Assert.ThrowsAny<FormatException>(() => B10SweepEvidenceCodec.ReadFinalization(bytes, Limits));
        Assert.NotEqual(0, await run.RunAsync()); Assert.Equal(bytes, run.Verifier[B10AbruptVerifyHarness.Output]);
        Assert.DoesNotContain(run.Verifier.Keys, name => name.EndsWith("/result.json", StringComparison.Ordinal) || name.EndsWith("/verify-completion.json", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(99, false)]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public async Task Given_CopiesBelowAtOrAboveDiagnosticCap_When_HostRuns_Then_TruncationPreservesCompleteCounts_T15(int count, bool truncated)
    {
        await kafka.ResetAsync(); using var run = NewRun();
        for (int index = 0; index < count; index++) await run.ProduceAsync(0, index);
        await SuccessAsync(run); JsonObject member = Member(Arm(run, "cross_restart"));
        Assert.Equal(count, Count(member, "copy_count")); Assert.Equal(Math.Min(count, 100), member["copies"]!.AsArray().Count);
        Assert.Equal(truncated, member["copies_truncated"]!.GetValue<bool>());
        Assert.Equal(Enumerable.Range(0, Math.Min(count, 100)).Select(value => (long)value), member["copies"]!.AsArray().Select(copy => Count(copy!, "offset")));
    }

    [Fact]
    public async Task Given_SelectedOtherAndNullValues_When_NewAndOldDrainsRun_Then_OnlySelectedNonNullValueGetsHash_T16()
    {
        await kafka.ResetAsync(); using var run = NewRun();
        await run.ProduceAsync(0, 0, key: "k", value: Encoding.UTF8.GetBytes("abc"));
        await run.ProduceAsync(0, 1, key: "j", value: Encoding.UTF8.GetBytes("abc")); await run.ProduceAsync(0, 2, key: "k", nullValue: true);
        var window = new RunWindow([], new Dictionary<int, long> { [0] = 3 }, [new PartitionWindow(0, 0, 3)], [], true);
        using var selected = ComponentRunHarness.Consumer(kafka.Bootstrap); using var legacy = ComponentRunHarness.Consumer(kafka.Bootstrap);
        WindowDrainResult hashed = WindowConsumer.Drain(selected, window, TimeProvider.System, TimeProvider.System.GetTimestamp(), TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(50), new ConsumedRecordBudget(3, 10000), new HashSet<string>(StringComparer.Ordinal) { "k" }, TestContext.Current.CancellationToken);
        Assert.Equal(new string?[] { "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", null, null }, hashed.Records.Select(record => record.ValueSha256));
        WindowDrainResult old = WindowConsumer.Drain(legacy, window, TimeProvider.System, TimeProvider.System.GetTimestamp(), TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(50), new ConsumedRecordBudget(3, 10000), TestContext.Current.CancellationToken);
        Assert.Equal(3, old.Records.Count); Assert.All(old.Records, record => Assert.Null(record.ValueSha256));
    }

    [Fact]
    public async Task Given_ChangedOutputReadback_When_HostPublishes_Then_ExitIsNonzero()
    {
        await kafka.ResetAsync(); using var run = NewRun(); run.ChangeReadback = true;
        Assert.NotEqual(0, await run.RunAsync()); Assert.Equal(1, run.Creates); Assert.Equal(1, run.OutputReadbacks);
    }

    private B10AbruptVerifyHarness NewRun(B10AbruptVerifyEvidence? evidence = null) => new(kafka.Bootstrap, (evidence ?? new()).Files);
    private static async Task SuccessAsync(B10AbruptVerifyHarness run, params int[] problems)
    {
        Assert.Equal(0, await run.RunAsync());
        Assert.Equal(problems.Select(number => Problems[number - 1]), Strings(run.Data["problems"]!));
    }
    private static JsonObject Arm(B10AbruptVerifyHarness run, string name) => run.Data["arms"]!.AsArray().Single(arm => Text(arm!["arm"]) == name)!.AsObject();
    private static JsonObject Member(JsonObject arm) => Assert.Single(arm["members"]!.AsArray())!.AsObject();
    private static long Count(JsonNode node, string field) => node[field]!.GetValue<long>();
    private static string? Text(JsonNode? node) => node?.GetValue<string>();
    private static IEnumerable<string?> Strings(JsonNode node) => node.AsArray().Select(Text);
    private static string[] Names(JsonObject root) => root.Select(pair => pair.Key).Order(StringComparer.Ordinal).ToArray();
    private static string[] LedgerMembers(Dictionary<string, byte[]> files) => Names(JsonNode.Parse(Encoding.UTF8.GetString(files["successor/ledger.jsonl"]).Split('\n')[0])!.AsObject());
    private static void AssertCounts(JsonObject member, long copies, long originals, long resends, long other, long pairs)
    {
        Assert.Equal(copies, Count(member, "copy_count")); Assert.Equal(originals, Count(member, "original_copies"));
        Assert.Equal(resends, Count(member, "resend_copies")); Assert.Equal(other, Count(member, "other_copies")); Assert.Equal(pairs, Count(member, "pair_count"));
    }
    private static void AssertWitness(JsonObject arm, string original, string resend, int originalPartition, long originalOffset, int resendPartition, long resendOffset)
    {
        JsonNode witness = arm["witness"]!;
        Assert.Equal(original, Text(witness["original_attempt_id"])); Assert.Equal(resend, Text(witness["resend_attempt_id"]));
        Assert.Equal(originalPartition, Count(witness["original"]!, "partition")); Assert.Equal(originalOffset, Count(witness["original"]!, "offset"));
        Assert.Equal(resendPartition, Count(witness["resend"]!, "partition")); Assert.Equal(resendOffset, Count(witness["resend"]!, "offset"));
    }
    private static void AssertFirstUnavailable(B10AbruptVerifyHarness run)
    { Assert.Null(run.Data["arms"]); Assert.NotNull(run.Data["end_line"]); Assert.True(run.Data["window_complete"]!.GetValue<bool>()); }
    private static void AssertUnknownCopies(B10AbruptVerifyHarness run)
    {
        Assert.False(run.Data["window_complete"]!.GetValue<bool>());
        foreach (JsonNode? arm in run.Data["arms"]!.AsArray())
        {
            foreach (string field in new[] { "originals_seen", "resends_seen", "pair_count", "witness" }) Assert.Null(arm![field]);
            foreach (JsonNode? member in arm!["members"]!.AsArray())
                foreach (string field in new[] { "copy_count", "original_copies", "resend_copies", "other_copies", "pair_count", "copies", "copies_truncated" }) Assert.Null(member![field]);
        }
    }
    private static void PadJournal(B10AbruptVerifyEvidence evidence, int count)
    {
        JsonObject last = evidence.Frames[^1]; evidence.Frames.RemoveAt(evidence.Frames.Count - 1);
        while (evidence.Frames.Count < count - 1)
            evidence.Add("native_error", new { capture_sequence = evidence.Frames.Count, error_code = -1, error_name = "synthetic", is_error = true, is_fatal = false, callback_monotonic_ticks = 1 });
        last["sequence"] = count; evidence.Frames.Add(last); evidence.Rehash();
    }
}
#pragma warning restore CA1707

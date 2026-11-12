using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

[CollectionDefinition("B10 abrupt verifier", DisableParallelization = true)]
public sealed class B10AbruptVerifyTestGroup { }

#pragma warning disable CA1707
[Collection("B10 abrupt verifier")]
[Trait("Tier", "Docker")]
public sealed class B10AbruptVerifyBoundaryTests(B10AbruptVerifyKafkaFixture kafka) : IClassFixture<B10AbruptVerifyKafkaFixture>
{
    private const string FirstProblem = "b10_abrupt_verify_first_evidence_invalid";
    private const string SuccessorProblem = "b10_abrupt_verify_successor_evidence_invalid";
    private static readonly string[] GuardReasons = ["b10_restart_guard_failed"];
    private static readonly string[] TwoKillOriginalIds = ["z3", "a4"];
    private static readonly string[] TwoKillResendIds = ["r3", "r4"];
    private static readonly string[] JournalAttemptFields = ["attempt_id", "cut_attempt_id"];

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public async Task Given_RepeatBelowAtOrAboveFixedValue_When_HostStarts_Then_OnlyOneReadsEvidence(int repeat, int exit)
    {
        await kafka.ResetAsync(); using var run = NewRun(new());
        run.Configuration["Verifier:Repeat"] = repeat.ToString(CultureInfo.InvariantCulture);
        Assert.Equal(exit, await run.RunAsync());
        if (exit == 0) AssertProblems(run);
        else { Assert.Equal(0, run.Reads); Assert.Equal(0, run.Listings); Assert.Equal(0, run.Creates); }
    }

    [Theory]
    [InlineData("Mode")]
    [InlineData("ScenarioId")]
    [InlineData("VerifierCommit")]
    [InlineData("MaxOutputBytes")]
    [InlineData("MaxDiagnosticsPerCheck")]
    [InlineData("MaxDiagnosticBytes")]
    [InlineData("CleanupTimeout")]
    [InlineData("ProcessLifetime")]
    [InlineData("RunId")]
    [InlineData("Phase")]
    [InlineData("ConfigurationId")]
    [InlineData("BlobServiceUri")]
    [InlineData("VerifierContainer")]
    [InlineData("LedgerContainer")]
    [InlineData("Repeat")]
    [InlineData("RepeatPrefix")]
    [InlineData("ExpectedInstanceId")]
    [InlineData("VerifierClientId")]
    [InlineData("NamespaceName")]
    [InlineData("TokenTimeout")]
    [InlineData("MetadataTimeout")]
    [InlineData("WatermarkTimeout")]
    [InlineData("PollSlice")]
    [InlineData("BlobIoTimeout")]
    [InlineData("IoTimeout")]
    [InlineData("SafetyStop")]
    [InlineData("MaxConsumedRecords")]
    [InlineData("MaxConsumedBytes")]
    [InlineData("MaxArtifactBytes")]
    [InlineData("MaxTotalInputBytes")]
    [InlineData("MaxPartitions")]
    [InlineData("MaxInputDepth")]
    [InlineData("StartLineInputName")]
    [InlineData("LoadGenImage")]
    [InlineData("VerifierImage")]
    public async Task Given_OneMissingRequiredOption_When_HostStarts_Then_ExitOneBeforeAnyStorageAccess(string option)
    {
        await kafka.ResetAsync(); using var run = NewRun(new());
        Assert.True(run.Configuration.Remove("Verifier:" + option));
        Assert.Equal(1, await run.RunAsync());
        Assert.Equal(0, run.Reads); Assert.Equal(0, run.Listings); Assert.Equal(0, run.Creates);
        Assert.False(run.Verifier.ContainsKey(B10AbruptVerifyHarness.Output));
    }

    public static TheoryData<string, long> PositiveOptionBoundaries()
    {
        var data = new TheoryData<string, long>();
        foreach (string name in new[]
        {
            "MaxOutputBytes", "MaxDiagnosticsPerCheck", "MaxDiagnosticBytes", "MaxConsumedRecords", "MaxConsumedBytes",
            "MaxArtifactBytes", "MaxTotalInputBytes", "MaxPartitions", "MaxInputDepth", "TokenTimeout", "MetadataTimeout",
            "WatermarkTimeout", "PollSlice", "BlobIoTimeout", "IoTimeout", "CleanupTimeout", "SafetyStop", "ProcessLifetime"
        })
            foreach (long value in new long[] { 0, 1, 2 }) data.Add(name, value);
        return data;
    }

    [Theory]
    [MemberData(nameof(PositiveOptionBoundaries))]
    public void Given_OptionBelowAtOrAbovePositiveBoundary_When_ValidatingAbruptMode_Then_OnlyPositiveValuesPass(string option, long value)
    {
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions("b10-abrupt-verify");
        options.ScenarioId = "B10"; options.ConfigurationId = "C1"; options.Phase = "C1"; options.Repeat = 1;
        options.ArtifactPrefix = "run-1/C1/r1/producer/";
        options.JobName = "run-1-c1-r1-b10-abrupt-verify";
        options.LoadGenImage = B10AbruptVerifyEvidence.Image; options.VerifierImage = B10AbruptVerifyEvidence.Image;
        var validator = new VerifierHostOptionsValidator();
        Assert.True(validator.Validate(null, options).Succeeded);
        var property = typeof(VerifierHostOptions).GetProperty(option)!;
        Type target = Nullable.GetUnderlyingType(property.PropertyType)!;
        property.SetValue(options, target == typeof(TimeSpan) ? TimeSpan.FromTicks(value) : Convert.ChangeType(value, target, CultureInfo.InvariantCulture));
        Assert.Equal(value > 0, validator.Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("no-first")]
    [InlineData("two-successors")]
    [InlineData("unknown-directory")]
    public async Task Given_EmptyOrAmbiguousDirectoryListing_When_HostRuns_Then_OnlyTheRelevantEvidenceProblemIsRecorded(string defect)
    {
        await kafka.ResetAsync(); using var run = NewRun(new());
        string first = B10AbruptVerifyEvidence.Prefix + B10AbruptVerifyEvidence.First + "/";
        if (defect == "empty")
            foreach (string name in run.Ledger.Keys.Where(name => name.StartsWith(B10AbruptVerifyEvidence.Prefix, StringComparison.Ordinal)).ToArray()) run.Ledger.Remove(name);
        else if (defect == "no-first")
            foreach (string name in run.Ledger.Keys.Where(name => name.StartsWith(first, StringComparison.Ordinal)).ToArray()) run.Ledger.Remove(name);
        else if (defect == "unknown-directory") run.Ledger[B10AbruptVerifyEvidence.Prefix + "unknown/unknown.json"] = [123, 125];
        else
        {
            string second = B10AbruptVerifyEvidence.Prefix + B10AbruptVerifyEvidence.Second + "/";
            foreach ((string name, byte[] bytes) in run.Ledger.Where(pair => pair.Key.StartsWith(second, StringComparison.Ordinal)).ToArray())
                run.Ledger[name.Replace(B10AbruptVerifyEvidence.Second, "synthetic-third", StringComparison.Ordinal)] = Encoding.UTF8.GetBytes(
                    Encoding.UTF8.GetString(bytes).Replace(B10AbruptVerifyEvidence.Second, "synthetic-third", StringComparison.Ordinal));
        }
        Assert.Equal(0, await run.RunAsync());
        AssertProblems(run, defect is "empty" or "no-first" ? FirstProblem : SuccessorProblem);
        if (defect is "empty" or "no-first") Assert.Null(run.Data["arms"]);
    }

    [Theory]
    [InlineData("receipt-source")]
    [InlineData("start-hash")]
    [InlineData("inventory-hash")]
    [InlineData("expected-jobs")]
    [InlineData("empty-partitions")]
    public async Task Given_OneInvalidStartBinding_When_HostRuns_Then_FirstEvidenceStillProducesUnknownCopyMembers(string defect)
    {
        await kafka.ResetAsync(); using var run = NewRun(new());
        string completion = PhasePaths.StartLineCompletion("synthetic", "C1", 1);
        string start = PhasePaths.StartLine("synthetic", "C1", 1), inventory = PhasePaths.BrokerInventory("synthetic", "C1", 1);
        if (defect == "receipt-source") Change(run.Verifier, completion, root => root["source_commit"] = new string('b', 40));
        else if (defect == "start-hash") run.Verifier[start] = [.. run.Verifier[start], 32];
        else if (defect == "inventory-hash") run.Verifier[inventory] = [.. run.Verifier[inventory], 32];
        else
        {
            Change(run.Verifier, start, root => root[defect == "expected-jobs" ? "expected_job_names" : "partition_highs"] = new JsonArray());
            run.RehashStartReceipt();
        }
        Assert.Equal(0, await run.RunAsync()); AssertProblems(run, "b10_abrupt_verify_start_line_invalid");
        Assert.NotNull(run.Data["arms"]); Assert.Null(run.Data["start_line"]); Assert.Null(run.Data["end_line"]);
        foreach (JsonNode? arm in run.Data["arms"]!.AsArray())
            foreach (JsonNode? member in arm!["members"]!.AsArray())
            { Assert.Null(member!["copies"]); Assert.Null(member["copies_truncated"]); }
    }

    [Theory]
    [InlineData("schema-2")]
    [InlineData("schema-3")]
    [InlineData("schema-4")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("trailing")]
    [InlineData("synthetic-false")]
    [InlineData("science-true")]
    [InlineData("purpose-missing")]
    public async Task Given_EnvelopeBoundaryOrClosedMemberFault_When_HostRuns_Then_OnlyExactDiagnosticEnvelopeAuthenticates(string defect)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence(); const string registration = "first/registration.json";
        if (defect.StartsWith("schema-", StringComparison.Ordinal)) evidence.Change(registration, root => root["schema_version"] = int.Parse(defect[7..], CultureInfo.InvariantCulture));
        else if (defect == "duplicate") evidence.Files[registration] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(evidence.Files[registration]).Replace("\"schema_version\":3", "\"schema_version\":3,\"schema_version\":3", StringComparison.Ordinal));
        else if (defect == "trailing") evidence.Files[registration] = [.. evidence.Files[registration], 123, 125];
        else evidence.Change(registration, root =>
        {
            if (defect == "unknown") root["unknown"] = true;
            else if (defect == "synthetic-false") root["synthetic"] = false;
            else if (defect == "science-true") root["scientific_admissible"] = true;
            else root.Remove("purpose");
        });
        using var run = NewRun(evidence); Assert.Equal(0, await run.RunAsync());
        AssertProblems(run, defect == "schema-3" ? [] : [FirstProblem]);
    }

    [Theory]
    [InlineData("run_id")]
    [InlineData("source_commit")]
    [InlineData("job_name")]
    [InlineData("pod_name")]
    [InlineData("instance_id")]
    [InlineData("predecessor_instance_id")]
    [InlineData("empty-uid")]
    [InlineData("inconsistent-uid")]
    [InlineData("successor-reuses-uid")]
    public async Task Given_OneIdentityBindingFault_When_HostRuns_Then_TheAffectedPodIsUnauthenticated(string field)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        if (field == "inconsistent-uid") evidence.Change("first/termination.json", root => root["identity"]!["pod_uid"] = "different");
        else if (field == "successor-reuses-uid")
        {
            evidence.Change("successor/registration.json", root => root["identity"]!["pod_uid"] = "uid-first");
            evidence.Change("successor/completion.json", root => root["identity"]!["pod_uid"] = "uid-first");
        }
        else evidence.Change("first/registration.json", root => root["identity"]![field == "empty-uid" ? "pod_uid" : field] = field == "empty-uid" ? "" : "wrong");
        evidence.RehashSuccessor(); using var run = NewRun(evidence); Assert.Equal(0, await run.RunAsync());
        AssertProblems(run, field == "successor-reuses-uid" ? SuccessorProblem : FirstProblem);
    }

    [Theory]
    [InlineData("first/same_instance-pre-acceptance-report.json", 1048575, true)]
    [InlineData("first/same_instance-pre-acceptance-report.json", 1048576, true)]
    [InlineData("first/same_instance-pre-acceptance-report.json", 1048577, false)]
    [InlineData("first/registration.json", 65535, true)]
    [InlineData("first/registration.json", 65536, true)]
    [InlineData("first/registration.json", 65537, false)]
    [InlineData("successor/request-lifecycle.jsonl", 0, true)]
    [InlineData("successor/ledger.jsonl", 0, false)]
    public async Task Given_FileLengthBelowAtOrAboveItsLimitOrAllowedEmpty_When_HostRuns_Then_OnlyContractLengthsAuthenticate(string file, int length, bool valid)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        byte[] bytes = new byte[length]; Array.Fill(bytes, (byte)' ');
        if (length > 0) evidence.Files[file].CopyTo(bytes, 0);
        evidence.Files[file] = bytes;
        if (file == "first/same_instance-pre-acceptance-report.json")
        {
            JsonNode report = evidence.Frames.Single(frame => Text(frame["kind"]) == "pre_acceptance_report" && Text(frame["data"]!["arm"]) == "same_instance")["data"]!;
            report["length"] = length; report["sha256"] = B10AbruptVerifyEvidence.Hash(bytes);
        }
        evidence.Rehash();
        using var run = NewRun(evidence); Assert.Equal(0, await run.RunAsync());
        AssertProblems(run, valid ? [] : [file.StartsWith("first/", StringComparison.Ordinal) ? FirstProblem : SuccessorProblem]);
    }

    [Theory]
    [InlineData("successor/ledger.jsonl", 268435455, true)]
    [InlineData("successor/ledger.jsonl", 268435456, true)]
    [InlineData("successor/ledger.jsonl", 268435457, false)]
    [InlineData("successor/manifest.json", 67108863, true)]
    [InlineData("successor/manifest.json", 67108864, true)]
    [InlineData("successor/manifest.json", 67108865, false)]
    [InlineData("successor/statistics.jsonl", 134217727, true)]
    [InlineData("successor/statistics.jsonl", 134217728, true)]
    [InlineData("successor/statistics.jsonl", 134217729, false)]
    [InlineData("successor/request-lifecycle.jsonl", 134217727, true)]
    [InlineData("successor/request-lifecycle.jsonl", 134217728, true)]
    [InlineData("successor/request-lifecycle.jsonl", 134217729, false)]
    [InlineData("first/journal.jsonl", 16777215, true)]
    [InlineData("first/journal.jsonl", 16777216, true)]
    [InlineData("first/journal.jsonl", 16777217, false)]
    public async Task Given_LargeArtifactBelowAtOrAboveItsByteCap_When_HostRuns_Then_OnlyWithinCapAuthenticates(string file, int length, bool valid)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        if (file == "first/journal.jsonl")
        {
            JsonObject kill = evidence.Frames[^1]; evidence.Frames.RemoveAt(evidence.Frames.Count - 1);
            int captures = 0;
            while (evidence.Frames.Count < 4095) evidence.Add("native_error", new
            { capture_sequence = ++captures, error_code = -1, error_name = "synthetic", is_error = false, is_fatal = false, callback_monotonic_ticks = 1 });
            kill["sequence"] = 4096; evidence.Frames.Add(kill);
            evidence.Change("first/termination.json", root => root["data"]!["native_error_count"] = captures);
            byte[][] lines = evidence.Frames.Select(frame => Encoding.UTF8.GetBytes(frame.ToJsonString())).ToArray();
            int padding = length - lines.Sum(line => line.Length + 1);
            Assert.True(padding >= 0);
            byte[] journal = new byte[length]; Array.Fill(journal, (byte)' ');
            int cursor = 0;
            for (int index = 0; index < lines.Length; index++)
            {
                byte[] line = lines[index]; line.CopyTo(journal, cursor); cursor += line.Length;
                int added = padding / (lines.Length - index); padding -= added;
                Assert.True(line.Length + added <= 65536);
                cursor += added; journal[cursor++] = 10;
            }
            Assert.Equal(length, cursor); Assert.Equal(0, padding);
            evidence.Files[file] = journal; evidence.Rehash(writeJournal: false);
        }
        else
        {
            byte[] original = evidence.Files[file]; byte[] bytes = new byte[length]; Array.Fill(bytes, (byte)' ');
            bool ledger = file == "successor/ledger.jsonl";
            original.AsSpan(0, ledger ? original.Length - 1 : original.Length).CopyTo(bytes);
            if (ledger) bytes[^1] = 10;
            evidence.Files[file] = bytes; evidence.RehashSuccessor();
        }
        using var run = NewRun(evidence); Assert.Equal(0, await run.RunAsync());
        AssertProblems(run, valid ? [] : [file.StartsWith("first/", StringComparison.Ordinal) ? FirstProblem : SuccessorProblem]);
        if (valid && file is "successor/statistics.jsonl" or "successor/request-lifecycle.jsonl")
            Assert.Equal(GuardReasons, run.Data["eligibility_reasons"]!.AsArray().Select(Text));
    }

    [Theory]
    [InlineData("records-4095")]
    [InlineData("records-4096")]
    [InlineData("records-4097")]
    [InlineData("missing-newline")]
    [InlineData("empty")]
    [InlineData("gap-record")]
    [InlineData("manifest-count-below")]
    [InlineData("manifest-count-above")]
    public async Task Given_JournalCountOrFramingBoundary_When_HostRuns_Then_OnlyCompleteBoundedSequenceAuthenticates(string defect)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        if (defect.StartsWith("records-", StringComparison.Ordinal))
        {
            int count = int.Parse(defect[8..], CultureInfo.InvariantCulture);
            JsonObject kill = evidence.Frames[^1]; evidence.Frames.RemoveAt(evidence.Frames.Count - 1);
            int captures = 0;
            while (evidence.Frames.Count < count - 1) evidence.Add("native_error", new
            { capture_sequence = ++captures, error_code = -1, error_name = "synthetic", is_error = false, is_fatal = false, callback_monotonic_ticks = 1 });
            kill["sequence"] = count; evidence.Frames.Add(kill);
            evidence.Change("first/termination.json", root => root["data"]!["native_error_count"] = captures); evidence.Rehash();
        }
        else if (defect == "gap-record")
        { evidence.Add("gap", new { reason = "writer_failed", first_missing_sequence = (int?)null, capture_status = "invalid" }); evidence.Rehash(); }
        else if (defect is "missing-newline" or "empty")
        {
            evidence.Files["first/journal.jsonl"] = defect == "empty" ? [] : evidence.Files["first/journal.jsonl"][..^1];
            evidence.Rehash(writeJournal: false);
        }
        else evidence.Change("first/abrupt-manifest.json", root => root["data"]!["journal_records"] = evidence.Frames.Count + (defect == "manifest-count-below" ? -1 : 1));
        if (defect.StartsWith("manifest-count-", StringComparison.Ordinal))
        { evidence.Change("first/termination.json", root => root["data"]!["manifest"] = B10AbruptVerifyEvidence.Descriptor("abrupt-manifest.json", evidence.Files["first/abrupt-manifest.json"])); evidence.RehashSuccessor(); }
        using var run = NewRun(evidence); Assert.Equal(0, await run.RunAsync());
        AssertProblems(run, defect is "records-4095" or "records-4096" ? [] : [FirstProblem]);
    }

    [Theory]
    [InlineData("accepted-before-prepared")]
    [InlineData("accepted-twice")]
    [InlineData("terminal-twice")]
    [InlineData("rejection-before-prepared")]
    [InlineData("original-missing")]
    [InlineData("wrong-key")]
    [InlineData("different-payload")]
    [InlineData("ordinal-minus-one")]
    [InlineData("ordinal-five")]
    [InlineData("hash-63")]
    [InlineData("hash-65")]
    [InlineData("hash-nonhex")]
    [InlineData("control-resend-missing")]
    [InlineData("control-resend-twice")]
    [InlineData("kill-empty")]
    [InlineData("producing-after-kill")]
    public async Task Given_OneAttemptOrSelectionFaultWithRehashedChain_When_HostRuns_Then_FirstEvidenceIsRejected(string defect)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        JsonObject firstPrepared = evidence.Frames.First(frame => Text(frame["kind"]) == "prepared");
        JsonObject firstAccepted = evidence.Frames.First(frame => Text(frame["kind"]) == "accepted");
        JsonObject resend = evidence.Frames.Single(frame => Text(frame["kind"]) == "prepared" && Text(frame["data"]!["attempt_id"]) == "t4");
        switch (defect)
        {
            case "accepted-before-prepared":
                evidence.Frames.Remove(firstAccepted); evidence.Frames.Insert(evidence.Frames.IndexOf(firstPrepared), firstAccepted); break;
            case "accepted-twice": evidence.Frames.Insert(evidence.Frames.IndexOf(firstAccepted) + 1, firstAccepted.DeepClone().AsObject()); break;
            case "terminal-twice":
                JsonObject terminal = evidence.Frames.First(frame => Text(frame["kind"]) == "terminal");
                evidence.Frames.Insert(evidence.Frames.IndexOf(terminal) + 1, terminal.DeepClone().AsObject()); break;
            case "rejection-before-prepared":
                JsonObject rejection = B10AbruptVerifyEvidence.Envelope("synchronous_rejection", new { attempt_id = "unprepared", prepared_sequence = 1, error_code = -1, error_reason = "synthetic" });
                rejection["sequence"] = 1; rejection["observed_utc"] = B10AbruptVerifyEvidence.Utc; rejection["monotonic_ticks"] = 1; rejection["clock_frequency"] = 1;
                evidence.Frames.Insert(1, rejection); break;
            case "original-missing":
                evidence.Frames.RemoveAll(frame => frame["data"] is JsonObject data && Text(data["attempt_id"]) == "c0"); break;
            case "wrong-key": firstPrepared["data"]!["key"] = "B10/wrong/C1/1/same_instance"; break;
            case "different-payload": firstPrepared["data"]!["payload_sha256"] = new string('a', 64); break;
            case "ordinal-minus-one": firstPrepared["data"]!["ordinal"] = -1; firstPrepared["data"]!["sequence"] = -1; break;
            case "ordinal-five": firstPrepared["data"]!["ordinal"] = 5; firstPrepared["data"]!["sequence"] = 5; break;
            case "hash-63": case "hash-65": case "hash-nonhex":
                string hash = defect == "hash-nonhex" ? new string('g', 64) : new string('a', defect == "hash-63" ? 63 : 65);
                foreach (JsonObject frame in evidence.Frames)
                    if (frame["data"] is JsonObject data && Text(data["arm"]) == "same_instance" && data.ContainsKey("payload_sha256")) data["payload_sha256"] = hash;
                break;
            case "control-resend-missing": evidence.Frames.RemoveAll(frame => frame["data"] is JsonObject data && Text(data["attempt_id"]) == "t4"); break;
            case "control-resend-twice":
                JsonObject duplicate = resend.DeepClone().AsObject(); duplicate["data"]!["attempt_id"] = "t5";
                evidence.Frames.Insert(evidence.Frames.IndexOf(resend) + 1, duplicate); break;
            case "kill-empty":
                JsonObject last = evidence.Frames[^1]; evidence.Frames.RemoveAt(evidence.Frames.Count - 1);
                evidence.Add("terminal", new { attempt_id = "c4", completion_source = "callback", delivered = true, error_code = 0, persistence_status = 2 }); evidence.Frames.Add(last);
                evidence.Change("first/termination.json", root => root["data"]!["kill_unconfirmed_attempt_ids"] = new JsonArray()); break;
            case "producing-after-kill": evidence.Add("control_drained", new { flush_returned = true, out_queue_length = 0 }); break;
        }
        Resequence(evidence); evidence.Rehash(); using var run = NewRun(evidence);
        Assert.Equal(0, await run.RunAsync()); AssertProblems(run, FirstProblem); Assert.Null(run.Data["arms"]);
    }

    [Theory]
    [InlineData("missing-file")]
    [InlineData("cleanup-false")]
    [InlineData("wrong-kill-set")]
    [InlineData("attempt-is-original")]
    [InlineData("wrong-key")]
    [InlineData("wrong-sequence")]
    [InlineData("wrong-hash")]
    [InlineData("ledger-duplicate")]
    [InlineData("ledger-extra")]
    [InlineData("ledger-unknown-member")]
    [InlineData("ledger-missing-newline")]
    public async Task Given_OneSuccessorChainOrLedgerFault_When_HostRuns_Then_OnlySuccessorAuthenticationFails(string defect)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        if (defect == "missing-file") evidence.Files.Remove("successor/statistics.jsonl");
        else if (defect == "cleanup-false") evidence.Change("successor/completion.json", root => root["data"]!["cleanup_completed"] = false);
        else if (defect == "wrong-kill-set") evidence.Change("successor/completion.json", root => root["data"]!["kill_unconfirmed_attempt_ids"] = new JsonArray("c3"));
        else if (defect.StartsWith("ledger-", StringComparison.Ordinal))
        {
            byte[] ledger = evidence.Files["successor/ledger.jsonl"];
            if (defect == "ledger-duplicate") evidence.Files["successor/ledger.jsonl"] = [.. ledger, .. ledger];
            else if (defect == "ledger-missing-newline") evidence.Files["successor/ledger.jsonl"] = ledger[..^1];
            else
            {
                JsonObject row = JsonNode.Parse(ledger)!.AsObject();
                if (defect == "ledger-extra") row["attempt_id"] = "unexpected"; else row["unexpected"] = true;
                evidence.Files["successor/ledger.jsonl"] = Encoding.UTF8.GetBytes(row.ToJsonString() + "\n");
            }
        }
        else evidence.Change("successor/completion.json", root =>
        {
            JsonNode invocation = root["data"]!["invocations"]![0]!;
            switch (defect)
            {
                case "attempt-is-original": invocation["attempt_id"] = "c4"; break;
                case "wrong-key": invocation["key"] = "wrong"; break;
                case "wrong-sequence": invocation["sequence"] = 3; break;
                case "wrong-hash": invocation["payload_sha256"] = new string('a', 64); break;
            }
        });
        evidence.RehashSuccessor(); using var run = NewRun(evidence); Assert.Equal(0, await run.RunAsync());
        AssertProblems(run, SuccessorProblem); Assert.NotNull(run.Data["arms"]); Assert.Null(run.Data["successor_instance_id"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_TwoKilledOriginalsWithUniqueOrDuplicateResendIds_When_HostRuns_Then_UniquePairsUseLexicographicWitnessAndDuplicateIdsRejectSuccessor(bool duplicate)
    {
        await kafka.ResetAsync(); B10AbruptVerifyEvidence evidence = CreateTwoKillEvidence();
        if (duplicate) evidence.Change("successor/completion.json", root => root["data"]!["invocations"]![1]!["attempt_id"] = "r3");
        using var run = NewRun(evidence);
        if (!duplicate)
        {
            await run.ProduceAsync(0, 0, attempt: "z3", sequence: 3);
            await run.ProduceAsync(0, 1, instance: B10AbruptVerifyEvidence.Second, attempt: "r3", sequence: 3);
            await run.ProduceAsync(1, 2, attempt: "a4");
            await run.ProduceAsync(1, 3, instance: B10AbruptVerifyEvidence.Second, attempt: "r4");
        }
        Assert.Equal(0, await run.RunAsync()); AssertProblems(run, duplicate ? [SuccessorProblem] : ["b10_abrupt_verify_record_unjoined"]);
        JsonNode arm = run.Data["arms"]!.AsArray().Single(value => Text(value!["arm"]) == "cross_restart")!;
        Assert.Equal(2, arm["unconfirmed_count"]!.GetValue<long>());
        if (!duplicate)
        {
            Assert.Equal(2, arm["pair_count"]!.GetValue<long>()); Assert.Equal(2, arm["originals_seen"]!.GetValue<long>()); Assert.Equal(2, arm["resends_seen"]!.GetValue<long>());
            Assert.Equal(TwoKillOriginalIds, arm["members"]!.AsArray().Select(member => Text(member!["original_attempt_id"])));
            Assert.Equal(TwoKillResendIds, arm["members"]!.AsArray().Select(member => Text(member!["resend_attempt_id"])));
            JsonNode witness = arm["witness"]!;
            Assert.Equal("a4", Text(witness["original_attempt_id"])); Assert.Equal("r4", Text(witness["resend_attempt_id"]));
            Assert.Equal(1, witness["original"]!["partition"]!.GetValue<int>()); Assert.Equal(2, witness["original"]!["offset"]!.GetValue<long>());
            Assert.Equal(1, witness["resend"]!["partition"]!.GetValue<int>()); Assert.Equal(3, witness["resend"]!["offset"]!.GetValue<long>());
        }
        else { Assert.Null(run.Data["successor_instance_id"]); Assert.Null(arm["resend_invoked"]); }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Given_TwoIndependentProblemsAndDetailCapBelowAtOrAboveCount_When_HostRuns_Then_AllProblemsSurviveBoundedDetails(int cap)
    {
        await kafka.ResetAsync(); using var run = NewTwoFailureRun();
        run.Configuration["Verifier:MaxDiagnosticsPerCheck"] = cap.ToString(CultureInfo.InvariantCulture);
        Assert.Equal(0, await run.RunAsync()); AssertProblems(run, "b10_abrupt_verify_start_line_invalid", FirstProblem);
        Assert.Equal(Math.Min(2, cap), run.Data["problem_details"]!.AsArray().Count); Assert.Null(run.Data["arms"]);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public async Task Given_DetailByteCapBelowAtOrAboveFirstLabel_When_HostRuns_Then_DetailsStayWithinUtf8LimitWithoutDroppingProblems(int cap)
    {
        await kafka.ResetAsync(); using var run = NewTwoFailureRun();
        run.Configuration["Verifier:MaxDiagnosticBytes"] = cap.ToString(CultureInfo.InvariantCulture);
        Assert.Equal(0, await run.RunAsync()); AssertProblems(run, "b10_abrupt_verify_start_line_invalid", FirstProblem);
        JsonArray details = run.Data["problem_details"]!.AsArray(); Assert.Equal(2, details.Count);
        const string firstLabel = "start_line";
        Assert.Equal(firstLabel[..Math.Min(cap, Encoding.UTF8.GetByteCount(firstLabel))], Text(details[0]!["detail"]));
        Assert.All(details, detail => Assert.True(Encoding.UTF8.GetByteCount(Text(detail!["detail"])!) <= cap)); Assert.Null(run.Data["arms"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_FirstFailureAndIndependentStartOrSuccessorFailure_When_HostRuns_Then_FirstFailureSuppressesOnlySuccessorProblems(bool invalidStart)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        evidence.Change("first/termination.json", root => root["data"]!["exit_code_local_observation"] = 136);
        foreach (string name in evidence.Files.Keys.Where(name => name.StartsWith("successor/", StringComparison.Ordinal)).ToArray()) evidence.Files.Remove(name);
        using var run = NewRun(evidence);
        if (invalidStart) run.Verifier.Remove(PhasePaths.StartLineCompletion("synthetic", "C1", 1));
        Assert.Equal(0, await run.RunAsync());
        AssertProblems(run, invalidStart ? ["b10_abrupt_verify_start_line_invalid", FirstProblem] : [FirstProblem]); Assert.Null(run.Data["arms"]);
    }

    private B10AbruptVerifyHarness NewRun(B10AbruptVerifyEvidence evidence) => new(kafka.Bootstrap, evidence.Files);
    private B10AbruptVerifyHarness NewTwoFailureRun()
    {
        var evidence = new B10AbruptVerifyEvidence(); evidence.Change("first/termination.json", root => root["data"]!["exit_code_local_observation"] = 136);
        evidence.RehashSuccessor();
        B10AbruptVerifyHarness run = NewRun(evidence); run.Verifier.Remove(PhasePaths.StartLineCompletion("synthetic", "C1", 1)); return run;
    }
    private static B10AbruptVerifyEvidence CreateTwoKillEvidence()
    {
        var evidence = new B10AbruptVerifyEvidence();
        evidence.Frames.RemoveAll(frame => Text(frame["kind"]) == "terminal" && Text(frame["data"]!["attempt_id"]) == "c3");
        foreach (JsonObject frame in evidence.Frames)
        {
            JsonObject data = frame["data"]!.AsObject();
            foreach (string field in JournalAttemptFields)
                if (Text(data[field]) is "c3" or "c4") data[field] = Text(data[field]) == "c3" ? "z3" : "a4";
        }
        evidence.Change("first/termination.json", root => root["data"]!["kill_unconfirmed_attempt_ids"] = new JsonArray("z3", "a4"));
        evidence.Change("successor/completion.json", root =>
        {
            JsonObject second = root["data"]!["invocations"]![0]!.DeepClone().AsObject();
            second["predecessor_attempt_id"] = "a4";
            JsonObject first = second.DeepClone().AsObject(); first["attempt_id"] = "r3"; first["predecessor_attempt_id"] = "z3"; first["sequence"] = 3;
            root["data"]!["invocations"] = new JsonArray(first, second); root["data"]!["kill_unconfirmed_attempt_ids"] = new JsonArray("z3", "a4");
        });
        evidence.Change("successor/manifest.json", root =>
        {
            JsonObject second = root["data"]!["inventory"]![0]!.DeepClone().AsObject();
            second["predecessor_attempt_id"] = "a4"; second["invocation_ordinal"] = 1;
            JsonObject first = second.DeepClone().AsObject(); first["attempt_id"] = "r3"; first["predecessor_attempt_id"] = "z3"; first["sequence"] = 3; first["invocation_ordinal"] = 0;
            root["data"]!["inventory"] = new JsonArray(first, second); root["data"]!["inventory_count"] = 2;
        });
        JsonObject row = JsonNode.Parse(evidence.Files["successor/ledger.jsonl"])!.AsObject();
        JsonObject earlier = row.DeepClone().AsObject(); earlier["attempt_id"] = "r3";
        evidence.Files["successor/ledger.jsonl"] = Encoding.UTF8.GetBytes(earlier.ToJsonString() + "\n" + row.ToJsonString() + "\n");
        Resequence(evidence); evidence.Rehash(); return evidence;
    }
    private static string? Text(JsonNode? value) => value?.GetValue<string>();
    private static void AssertProblems(B10AbruptVerifyHarness run, params string[] expected) =>
        Assert.Equal(expected, run.Data["problems"]!.AsArray().Select(Text));
    private static void Change(Dictionary<string, byte[]> files, string name, Action<JsonObject> change)
    { JsonObject root = JsonNode.Parse(files[name])!.AsObject(); change(root); files[name] = Encoding.UTF8.GetBytes(root.ToJsonString()); }
    private static void Resequence(B10AbruptVerifyEvidence evidence)
    {
        var prepared = new Dictionary<string, long>(StringComparer.Ordinal);
        for (int index = 0; index < evidence.Frames.Count; index++)
        {
            JsonObject frame = evidence.Frames[index]; frame["sequence"] = index + 1L;
            JsonNode data = frame["data"]!; string? id = Text(data["attempt_id"]);
            if (Text(frame["kind"]) == "prepared") prepared[id!] = index + 1L;
            else if ((Text(frame["kind"]) is "accepted" or "synchronous_rejection") && id is not null && prepared.TryGetValue(id, out long sequence)) data["prepared_sequence"] = sequence;
        }
    }
}
#pragma warning restore CA1707

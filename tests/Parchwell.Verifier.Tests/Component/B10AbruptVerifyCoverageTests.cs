using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707
[Collection("B10 abrupt verifier")]
public sealed class B10AbruptVerifyCoverageTests(B10AbruptVerifyKafkaFixture kafka) : IClassFixture<B10AbruptVerifyKafkaFixture>
{
    private const string FirstProblem = "b10_abrupt_verify_first_evidence_invalid";
    private const string SuccessorProblem = "b10_abrupt_verify_successor_evidence_invalid";

    [Theory]
    [InlineData("schema_version")]
    [InlineData("source_commit")]
    [InlineData("pod_uid")]
    public async Task Given_InvalidCompletionBoundRegistrationAndValidLaterRead_When_HostRuns_Then_SuccessorAuthenticationUsesBoundBytes(string field)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        const string file = "successor/registration.json";
        byte[] validRegistration = evidence.Files[file];
        evidence.Change(file, root =>
        {
            if (field == "schema_version") root[field] = 2;
            else root["identity"]![field] = "different";
        });
        evidence.RehashSuccessor(); using var run = NewRun(evidence);
        string registration = B10AbruptVerifyEvidence.Prefix + B10AbruptVerifyEvidence.Second + "/registration.json";
        int registrationReads = 0;
        bool boundRegistrationRead = false;
        run.BeforeRead = name =>
        {
            if (name != registration) return;
            registrationReads++;
            if (registrationReads == 2) boundRegistrationRead = true;
            if (registrationReads >= 3) run.Ledger[name] = validRegistration;
        };

        await AssertRunAsync(run, SuccessorProblem);
        Assert.True(boundRegistrationRead);
        Assert.Equal(B10AbruptVerifyEvidence.First, Text(run.Data["first_instance_id"]));
        Assert.Null(run.Data["successor_instance_id"]);
        Assert.True(run.Data["window_complete"]!.GetValue<bool>());
        Assert.NotNull(run.Data["arms"]);
        Assert.Null(run.Data["arms"]![1]!["resend_invoked"]);
    }

    [Theory]
    [InlineData("schema_version")]
    [InlineData("source_commit")]
    [InlineData("pod_uid")]
    public async Task Given_FirstRegistrationChangesAfterCandidateSelection_When_HostRuns_Then_LoadedRegistrationIsReauthenticated(string field)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        const string file = "first/registration.json";
        byte[] candidateRegistration = evidence.Files[file];
        evidence.Change(file, root =>
        {
            if (field == "schema_version") root[field] = 2;
            else root["identity"]![field] = "different";
        });
        byte[] changedRegistration = evidence.Files[file];
        evidence.Files[file] = candidateRegistration;
        using var run = NewRun(evidence);
        string registration = B10AbruptVerifyEvidence.Prefix + B10AbruptVerifyEvidence.First + "/registration.json";
        int registrationReads = 0;
        bool registrationSwapped = false;
        run.BeforeRead = name =>
        {
            if (name != registration || ++registrationReads != 2) return;
            run.Ledger[name] = changedRegistration;
            registrationSwapped = true;
        };

        await AssertRunAsync(run, FirstProblem);
        Assert.True(registrationSwapped);
        Assert.Null(run.Data["first_instance_id"]);
        Assert.Null(run.Data["successor_instance_id"]);
        Assert.Null(run.Data["arms"]);
        Assert.Null(run.Data["end_line"]);
        Assert.False(run.Data["window_complete"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("schema-2")]
    [InlineData("schema-3")]
    [InlineData("schema-4")]
    [InlineData("dialect")]
    [InlineData("purpose")]
    [InlineData("synthetic")]
    [InlineData("scientific_admissible")]
    [InlineData("kind")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("trailing")]
    [InlineData("missing-data-member")]
    [InlineData("unknown-data-member")]
    [InlineData("opaque-inventory")]
    public async Task Given_RehashedSuccessorManifestEnvelope_When_HostRuns_Then_OnlyExactDiagnosticStructureAuthenticates(string defect)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence(); const string file = "successor/manifest.json";
        if (defect == "duplicate") evidence.Files[file] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(evidence.Files[file]).Replace("\"schema_version\":3", "\"schema_version\":3,\"schema_version\":3", StringComparison.Ordinal));
        else if (defect == "trailing") evidence.Files[file] = [.. evidence.Files[file], 123, 125];
        else evidence.Change(file, root =>
        {
            if (defect.StartsWith("schema-", StringComparison.Ordinal)) root["schema_version"] = int.Parse(defect[7..], CultureInfo.InvariantCulture);
            else if (defect is "synthetic" or "scientific_admissible") root[defect] = defect != "synthetic";
            else if (defect == "unknown") root["unknown"] = true;
            else if (defect == "missing-data-member") root["data"]!.AsObject().Remove("cohort");
            else if (defect == "unknown-data-member") root["data"]!["unknown"] = true;
            else if (defect == "opaque-inventory")
            {
                root["data"]!["instance_id"] = "not-reconciled"; root["data"]!["cohort"] = "not-reconciled";
                root["data"]!["inventory_count"] = -1; root["data"]!["inventory"] = new JsonArray(); root["data"]!["bookkeeping_complete"] = false;
            }
            else root[defect] = "wrong";
        });
        evidence.RehashSuccessor(); using var run = NewRun(evidence);
        await AssertRunAsync(run, defect is "schema-3" or "opaque-inventory" ? [] : [SuccessorProblem]);
        Assert.NotNull(run.Data["arms"]);
    }

    [Theory]
    [InlineData("run_id", "wrong")]
    [InlineData("scenario_id", "B04")]
    [InlineData("configuration_id", "C0")]
    [InlineData("phase", "C0")]
    [InlineData("repeat", "0")]
    [InlineData("repeat", "1")]
    [InlineData("repeat", "2")]
    [InlineData("source_commit", "wrong")]
    [InlineData("image_reference", "wrong")]
    [InlineData("job_name", "wrong")]
    [InlineData("pod_name", "wrong")]
    [InlineData("pod_uid", "")]
    [InlineData("pod_uid", "different")]
    [InlineData("instance_id", "wrong")]
    [InlineData("predecessor_instance_id", "wrong")]
    public async Task Given_OneRehashedSuccessorManifestIdentityFault_When_HostRuns_Then_OnlySuccessorEvidenceFails(string field, string value)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        evidence.Change("successor/manifest.json", root => root["identity"]![field] = field == "repeat" ? JsonValue.Create(int.Parse(value, CultureInfo.InvariantCulture)) : JsonValue.Create(value));
        evidence.RehashSuccessor(); using var run = NewRun(evidence);
        await AssertRunAsync(run, field == "repeat" && value == "1" ? [] : [SuccessorProblem]);
        Assert.NotNull(run.Data["arms"]);
    }

    [Theory]
    [InlineData("registration", "first/registration.json")]
    [InlineData("abrupt_manifest", "first/abrupt-manifest.json")]
    [InlineData("termination", "first/termination.json")]
    [InlineData("successor_receipt", "successor/completion.json")]
    [InlineData("cohort_inventory", "successor/manifest.json")]
    public async Task Given_AuthenticatedNonJournalObjectKindInRehashedJournal_When_HostRuns_Then_JournalKindCheckRejectsFirstEvidence(string kind, string objectFile)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        JsonObject source = JsonNode.Parse(evidence.Files[objectFile])!.AsObject();
        evidence.Add(kind, source["data"]!.DeepClone()); evidence.Rehash();
        using var run = NewRun(evidence); await AssertRunAsync(run, FirstProblem);
        Assert.Null(run.Data["arms"]); Assert.Null(run.Data["end_line"]); Assert.False(run.Data["window_complete"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Given_OutputBudgetBelowAtAndAboveSerializedLength_When_HostPublishes_Then_OnlyFittingIdenticalBytesAreCreated()
    {
        await kafka.ResetAsync(); byte[] expected;
        using (var baseline = NewRun(new()))
        {
            await AssertRunAsync(baseline); expected = baseline.Verifier[B10AbruptVerifyHarness.Output];
        }
        foreach (int delta in new[] { -1, 0, 1 })
        {
            using var run = NewRun(new()); run.Configuration["Verifier:MaxOutputBytes"] = (expected.Length + delta).ToString(CultureInfo.InvariantCulture);
            Assert.Equal(delta < 0 ? 1 : 0, await run.RunAsync());
            if (delta < 0) { Assert.Equal(0, run.Creates); Assert.False(run.Verifier.ContainsKey(B10AbruptVerifyHarness.Output)); }
            else { Assert.Equal(expected, run.Verifier[B10AbruptVerifyHarness.Output]); Assert.Equal(1, run.Creates); Assert.Equal(1, run.OutputReadbacks); }
        }
    }

    [Theory]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    public async Task Given_EnvelopeDepthBelowAtOrAboveCodecLimit_When_HostRuns_Then_OnlyDepthAtMost32Authenticates(int depth)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        JsonNode nested = JsonValue.Create(0)!;
        for (int count = 0; count < depth - 2; count++) nested = new JsonArray(nested);
        evidence.Change("first/termination.json", root => root["data"]!["child"] = nested);
        evidence.RehashSuccessor(); using var run = NewRun(evidence);
        await AssertRunAsync(run, depth <= 32 ? [] : [FirstProblem]);
    }

    [Theory]
    [InlineData("exit_observed")]
    [InlineData("successor_permitted")]
    [InlineData("capture_gaps")]
    [InlineData("manifest-gaps")]
    public async Task Given_OneIndependentTerminationPredicateFailure_When_HostRuns_Then_OnlyFirstEvidenceIsRejected(string field)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        if (field == "manifest-gaps") evidence.Change("first/abrupt-manifest.json", root => root["data"]!["gaps"] = new JsonArray("synthetic-gap"));
        else evidence.Change("first/termination.json", root => root["data"]![field] = field == "capture_gaps" ? new JsonArray("synthetic-gap") : JsonValue.Create(false));
        evidence.Rehash(); using var run = NewRun(evidence); await AssertRunAsync(run, FirstProblem); Assert.Null(run.Data["arms"]);
    }

    public static TheoryData<string, int> DescriptorLengths()
    {
        var data = new TheoryData<string, int>();
        foreach (string binding in new[] { "first-manifest", "first-journal", "first-report", "successor-registration", "successor-first-termination", "successor-journal", "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json" })
            foreach (int delta in new[] { -1, 0, 1 }) data.Add(binding, delta);
        return data;
    }

    [Theory]
    [MemberData(nameof(DescriptorLengths))]
    public async Task Given_DescriptorLengthBelowAtOrAboveActualBytesWithCorrectHash_When_HostRuns_Then_OnlyExactLengthAuthenticates(string binding, int delta)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        void Adjust(JsonNode descriptor) => descriptor["length"] = descriptor["length"]!.GetValue<long>() + delta;
        if (binding == "first-manifest") evidence.Change("first/termination.json", root => Adjust(root["data"]!["manifest"]!));
        else if (binding is "first-journal" or "first-report")
        {
            evidence.Change("first/abrupt-manifest.json", root => Adjust(binding == "first-journal" ? root["data"]!["journal"]! : root["data"]!["report_files"]![0]!));
            evidence.Change("first/termination.json", root => root["data"]!["manifest"] = B10AbruptVerifyEvidence.Descriptor("abrupt-manifest.json", evidence.Files["first/abrupt-manifest.json"]));
        }
        if (binding.StartsWith("first-", StringComparison.Ordinal)) evidence.RehashSuccessor();
        else evidence.Change("successor/completion.json", root =>
        {
            JsonNode data = root["data"]!;
            JsonNode descriptor = binding switch
            {
                "successor-registration" => data["registration"]!, "successor-first-termination" => data["first_termination"]!, "successor-journal" => data["journal"]!,
                _ => data["artifacts"]!.AsArray().Single(node => Text(node!["name"]) == binding)!
            };
            Adjust(descriptor);
        });
        using var run = NewRun(evidence); await AssertRunAsync(run, delta == 0 ? [] : [binding.StartsWith("first-", StringComparison.Ordinal) ? FirstProblem : SuccessorProblem]);
    }

    [Theory]
    [InlineData("accepted", -1)]
    [InlineData("accepted", 0)]
    [InlineData("accepted", 1)]
    [InlineData("synchronous_rejection", -1)]
    [InlineData("synchronous_rejection", 0)]
    [InlineData("synchronous_rejection", 1)]
    public async Task Given_PreparedSequenceBelowAtOrAboveEarlierPrepared_When_HostRuns_Then_OnlyExactBindingAuthenticates(string kind, int delta)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        JsonObject frame = evidence.Frames.First(value => Text(value["kind"]) == "accepted");
        frame["kind"] = kind; frame["data"]!["prepared_sequence"] = frame["data"]!["prepared_sequence"]!.GetValue<long>() + delta;
        if (kind == "synchronous_rejection") { frame["data"]!["error_code"] = -1; frame["data"]!["error_reason"] = "synthetic"; }
        evidence.Rehash(); using var run = NewRun(evidence); await AssertRunAsync(run, delta == 0 ? [] : [FirstProblem]);
    }

    [Fact]
    public async Task Given_ComputedAndDeclaredEmptyControlSetsWithNoResends_When_HostRuns_Then_IndependentNonemptyControlGuardRejectsFirstEvidence()
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        evidence.Frames.RemoveAll(frame => Text(frame["data"]!["attempt_id"]) == "t4");
        JsonObject terminal = evidence.Frames.Single(frame => Text(frame["kind"]) == "terminal" && Text(frame["data"]!["attempt_id"]) == "s4");
        evidence.Frames.Remove(terminal); JsonObject cut = evidence.Frames.Single(frame => Text(frame["kind"]) == "cut");
        evidence.Frames.Insert(evidence.Frames.IndexOf(cut), terminal); cut["data"]!["unconfirmed_attempt_ids"] = new JsonArray();
        evidence.Change("first/termination.json", root => root["data"]!["control_unconfirmed_attempt_ids"] = new JsonArray());
        Resequence(evidence); evidence.Rehash(); using var run = NewRun(evidence); await AssertRunAsync(run, FirstProblem); Assert.Null(run.Data["arms"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_AllSuccessorObjectUidsEmptyOrEqualToFirstPod_When_HostRuns_Then_IndependentUidGuardRejectsSuccessor(bool equalFirst)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        foreach (string file in new[] { "successor/registration.json", "successor/manifest.json", "successor/completion.json" })
            evidence.Change(file, root => root["identity"]!["pod_uid"] = equalFirst ? "uid-first" : "");
        evidence.RehashSuccessor(); using var run = NewRun(evidence); await AssertRunAsync(run, SuccessorProblem); Assert.NotNull(run.Data["arms"]);
    }

    [Theory]
    [InlineData("JobName")]
    [InlineData("PodName")]
    [InlineData("PodUid")]
    [InlineData("SourceCommit")]
    [InlineData("TenantId")]
    [InlineData("WorkloadIdentityClientId")]
    [InlineData("FederatedTokenFilePath")]
    public async Task Given_MissingModeIdentityOrAuthenticationOption_When_HostStarts_Then_ExitOneBeforeEvidenceAccess(string option)
    {
        await kafka.ResetAsync(); using var run = NewRun(new());
        Assert.True(run.Configuration.Remove("Verifier:" + option));
        if (option == "SourceCommit") Assert.True(run.Configuration.Remove("PARCHWELL_SOURCE_COMMIT"));
        Assert.Equal(1, await run.RunAsync()); Assert.Equal(0, run.Reads); Assert.Equal(0, run.Listings); Assert.Equal(0, run.Creates);
    }

    [Theory]
    [InlineData("B10LastVerifierCompletedUtc")]
    [InlineData("B10LastLoadgenTerminatedUtc")]
    public async Task Given_SweepOnlyLifecycleOption_When_AbruptHostStarts_Then_EarlyValidationRejectsBeforeEvidenceAccess(string option)
    {
        await kafka.ResetAsync(); using var run = NewRun(new()); run.Configuration["Verifier:" + option] = B10AbruptVerifyEvidence.Utc;
        Assert.Equal(1, await run.RunAsync()); Assert.Equal(0, run.Reads); Assert.Equal(0, run.Listings); Assert.Equal(0, run.Creates);
    }

    [Fact]
    public async Task Given_MissingOutputReadback_When_HostPublishes_Then_ExitOneAfterCreate()
    {
        await kafka.ResetAsync(); using var run = NewRun(new()); run.MissingReadback = true;
        Assert.Equal(1, await run.RunAsync()); Assert.Equal(1, run.Creates); Assert.Equal(1, run.OutputReadbacks);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_ConsumedByteBudgetBelowAtOrAboveOneRecord_When_HostRuns_Then_OnlyCompleteWindowRetainsCounts(int delta)
    {
        await kafka.ResetAsync(); using var run = NewRun(new());
        int bytes = Encoding.UTF8.GetByteCount(B10AbruptVerifyEvidence.Key("cross_restart")) + B10AbruptVerifyEvidence.Payload.Length +
            Encoding.UTF8.GetByteCount("instance-id" + B10AbruptVerifyEvidence.First + "attempt-id" + "c4" + "sequence" + "4");
        run.Configuration["Verifier:MaxConsumedBytes"] = (bytes + delta).ToString(CultureInfo.InvariantCulture);
        await run.ProduceAsync(0, 0); await AssertRunAsync(run, delta < 0 ? ["b10_abrupt_verify_window_incomplete"] : []);
        JsonNode member = run.Data["arms"]![1]!["members"]![0]!;
        if (delta < 0) Assert.Null(member["copy_count"]); else Assert.Equal(1, member["copy_count"]!.GetValue<long>());
    }

    [Theory]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(101)]
    public async Task Given_ArtifactReadElapsedBelowAtOrAboveBudget_When_HostReadsBackOutput_Then_DeadlineBoundaryFailsPublication(int elapsed)
    {
        await kafka.ResetAsync(); using var run = NewRun(new()); var clock = new BoundaryClock(); run.Clock = clock;
        run.Configuration["Verifier:BlobIoTimeout"] = "00:00:00.100";
        run.BeforeRead = name => { if (name == B10AbruptVerifyHarness.Output) clock.Advance(elapsed); };
        Assert.Equal(elapsed < 100 ? 0 : 1, await run.RunAsync()); Assert.Equal(1, run.Creates); Assert.Equal(1, run.OutputReadbacks);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(101)]
    public async Task Given_KafkaCleanupElapsedBelowAtOrAboveBudget_When_HostFinishesWindow_Then_DeadlineBoundaryPreventsOutput(int elapsed)
    {
        await kafka.ResetAsync(); using var run = NewRun(new()); var clock = new BoundaryClock(); run.Clock = clock;
        run.Configuration["Verifier:CleanupTimeout"] = "00:00:00.100"; run.AfterCleanup = () => clock.Advance(elapsed);
        Assert.Equal(elapsed < 100 ? 0 : 1, await run.RunAsync()); Assert.Equal(elapsed < 100 ? 1 : 0, run.Creates);
    }

    [Fact]
    public async Task Given_IndependentCopyHashAndRoleMismatches_When_HostClassifies_Then_HashMismatchIsExcludedAndIdentityMismatchesAreOther()
    {
        await kafka.ResetAsync(); using var run = NewRun(new());
        await run.ProduceAsync(0, 0, value: Encoding.UTF8.GetBytes("different-payload"));
        await run.ProduceAsync(0, 1, instance: "wrong-instance");
        await run.ProduceAsync(0, 2, attempt: "wrong-attempt");
        await run.ProduceAsync(0, 3, instance: B10AbruptVerifyEvidence.Second, attempt: "wrong-attempt");
        await run.ProduceAsync(0, 4, instance: "wrong-instance", attempt: "r4");
        await AssertRunAsync(run); JsonNode member = run.Data["arms"]![1]!["members"]![0]!;
        Assert.Equal(4, member["copy_count"]!.GetValue<long>()); Assert.Equal(4, member["other_copies"]!.GetValue<long>());
        Assert.Equal(0, member["original_copies"]!.GetValue<long>()); Assert.Equal(0, member["resend_copies"]!.GetValue<long>());
        Assert.Equal(new long[] { 1, 2, 3, 4 }, member["copies"]!.AsArray().Select(copy => copy!["offset"]!.GetValue<long>()));
    }

    [Theory]
    [InlineData("missing-registration")]
    [InlineData("missing-manifest")]
    [InlineData("missing-termination")]
    [InlineData("missing-journal")]
    [InlineData("forbidden-report")]
    [InlineData("duplicate-report")]
    [InlineData("listed-report-missing")]
    [InlineData("extra-object")]
    [InlineData("empty-report-list")]
    [InlineData("descriptor-name")]
    public async Task Given_FirstObjectSetOrDescriptorNameFaultWithValidOuterHashes_When_HostRuns_Then_OnlyClosedBoundObjectsAuthenticate(string defect)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        if (defect.StartsWith("missing-", StringComparison.Ordinal))
        {
            string file = defect switch { "missing-registration" => "registration.json", "missing-manifest" => "abrupt-manifest.json", "missing-termination" => "termination.json", _ => "journal.jsonl" };
            evidence.Files.Remove("first/" + file);
        }
        else if (defect == "extra-object") evidence.Files["first/extra.json"] = [123, 125];
        else if (defect == "descriptor-name")
        {
            evidence.Change("first/termination.json", root => root["data"]!["manifest"]!["name"] = "wrong.json"); evidence.RehashSuccessor();
        }
        else
        {
            evidence.Change("first/abrupt-manifest.json", root =>
            {
                JsonArray reports = root["data"]!["report_files"]!.AsArray();
                if (defect == "forbidden-report")
                {
                    byte[] bytes = [123, 125]; evidence.Files["first/forbidden-report.json"] = bytes;
                    reports.Add(B10AbruptVerifyEvidence.Descriptor("forbidden-report.json", bytes));
                }
                else if (defect == "duplicate-report") reports.Add(reports[0]!.DeepClone());
                else if (defect == "listed-report-missing") evidence.Files.Remove("first/" + Text(reports[0]!["name"]));
                else
                {
                    foreach (JsonNode? descriptor in reports) evidence.Files.Remove("first/" + Text(descriptor!["name"]));
                    reports.Clear();
                }
            });
            evidence.Change("first/termination.json", root => root["data"]!["manifest"] = B10AbruptVerifyEvidence.Descriptor("abrupt-manifest.json", evidence.Files["first/abrupt-manifest.json"]));
            evidence.RehashSuccessor();
        }
        using var run = NewRun(evidence); await AssertRunAsync(run, defect == "empty-report-list" ? [] : [FirstProblem]);
    }

    [Theory]
    [InlineData("exit-138")]
    [InlineData("journal-sequence-minus-one")]
    [InlineData("prepared-arm")]
    [InlineData("prepared-role")]
    [InlineData("prepared-sequence-minus-one")]
    [InlineData("prepared-sequence-plus-one")]
    [InlineData("prepared-duplicate")]
    [InlineData("rejection-duplicate")]
    [InlineData("terminal-before-prepared")]
    [InlineData("cut-missing")]
    [InlineData("cut-duplicate")]
    [InlineData("termination-control-set")]
    [InlineData("resend-before-cut")]
    [InlineData("resend-wrong-arm")]
    [InlineData("resend-sequence")]
    [InlineData("resend-hash")]
    [InlineData("unknown-journal-kind")]
    [InlineData("identity-unknown")]
    [InlineData("identity-missing")]
    [InlineData("data-unknown")]
    [InlineData("data-missing")]
    public async Task Given_OneIndependentFirstBindingOrOrderingFaultWithRehashedChain_When_HostRuns_Then_FirstEvidenceIsRejected(string defect)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        JsonObject prepared = evidence.Frames.First(frame => Text(frame["kind"]) == "prepared");
        JsonObject accepted = evidence.Frames.First(frame => Text(frame["kind"]) == "accepted");
        JsonObject resend = evidence.Frames.Single(frame => Text(frame["kind"]) == "prepared" && Text(frame["data"]!["attempt_id"]) == "t4");
        JsonObject cut = evidence.Frames.Single(frame => Text(frame["kind"]) == "cut");
        switch (defect)
        {
            case "exit-138": evidence.Change("first/termination.json", root => root["data"]!["exit_code_local_observation"] = 138); break;
            case "prepared-arm": prepared["data"]!["arm"] = "wrong"; break;
            case "prepared-role": prepared["data"]!["role"] = "wrong"; break;
            case "prepared-sequence-minus-one": prepared["data"]!["sequence"] = -1; break;
            case "prepared-sequence-plus-one": prepared["data"]!["sequence"] = 1; break;
            case "prepared-duplicate": evidence.Frames.Insert(evidence.Frames.IndexOf(prepared) + 1, prepared.DeepClone().AsObject()); break;
            case "rejection-duplicate":
                accepted["kind"] = "synchronous_rejection"; accepted["data"]!["error_code"] = -1; accepted["data"]!["error_reason"] = "synthetic";
                evidence.Frames.Insert(evidence.Frames.IndexOf(accepted) + 1, accepted.DeepClone().AsObject()); break;
            case "terminal-before-prepared":
                JsonObject terminal = evidence.Frames.First(frame => Text(frame["kind"]) == "terminal"); evidence.Frames.Remove(terminal); evidence.Frames.Insert(evidence.Frames.IndexOf(prepared), terminal); break;
            case "cut-missing": evidence.Frames.Remove(cut); break;
            case "cut-duplicate": evidence.Frames.Insert(evidence.Frames.IndexOf(cut) + 1, cut.DeepClone().AsObject()); break;
            case "termination-control-set": evidence.Change("first/termination.json", root => root["data"]!["control_unconfirmed_attempt_ids"] = new JsonArray("s3")); break;
            case "resend-before-cut": evidence.Frames.Remove(resend); evidence.Frames.Insert(evidence.Frames.IndexOf(cut), resend); break;
            case "resend-wrong-arm": resend["data"]!["arm"] = "cross_restart"; resend["data"]!["key"] = B10AbruptVerifyEvidence.Key("cross_restart"); break;
            case "resend-sequence": resend["data"]!["ordinal"] = 3; resend["data"]!["sequence"] = 3; break;
            case "resend-hash": resend["data"]!["payload_sha256"] = new string('a', 64); break;
            case "unknown-journal-kind": evidence.Add("unknown", new { }); break;
            case "identity-unknown": prepared["identity"]!["unknown"] = true; break;
            case "identity-missing": prepared["identity"]!.AsObject().Remove("phase"); break;
            case "data-unknown": prepared["data"]!["unknown"] = true; break;
            case "data-missing": prepared["data"]!.AsObject().Remove("probe_id"); break;
        }
        Resequence(evidence);
        if (defect == "journal-sequence-minus-one") evidence.Frames[1]["sequence"] = 1L;
        evidence.Rehash(); using var run = NewRun(evidence); await AssertRunAsync(run, FirstProblem); Assert.Null(run.Data["arms"]);
    }

    [Theory]
    [InlineData("artifact-forbidden")]
    [InlineData("artifact-duplicate")]
    [InlineData("artifact-omitted")]
    [InlineData("artifact-unknown")]
    [InlineData("invocations-empty")]
    [InlineData("invocation-predecessor")]
    [InlineData("invocation-unknown-member")]
    [InlineData("invocation-sequence-above")]
    public async Task Given_OneIndependentSuccessorSetOrInvocationFault_When_HostRuns_Then_OnlySuccessorEvidenceIsRejected(string defect)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        evidence.Change("successor/completion.json", root =>
        {
            JsonArray artifacts = root["data"]!["artifacts"]!.AsArray(); JsonArray invocations = root["data"]!["invocations"]!.AsArray();
            switch (defect)
            {
                case "artifact-forbidden": artifacts.Add(B10AbruptVerifyEvidence.Descriptor("registration.json", evidence.Files["successor/registration.json"])); break;
                case "artifact-duplicate": artifacts.Add(artifacts[0]!.DeepClone()); break;
                case "artifact-omitted": artifacts.RemoveAt(0); break;
                case "artifact-unknown": artifacts.Add(B10AbruptVerifyEvidence.Descriptor("absent.json", [123, 125])); break;
                case "invocations-empty": invocations.Clear(); break;
                case "invocation-predecessor": invocations[0]!["predecessor_attempt_id"] = "c3"; break;
                case "invocation-unknown-member": invocations[0]!["unknown"] = true; break;
                case "invocation-sequence-above": invocations[0]!["sequence"] = 5; break;
            }
        });
        using var run = NewRun(evidence); await AssertRunAsync(run, SuccessorProblem); Assert.NotNull(run.Data["arms"]);
    }

    [Fact]
    public async Task Given_AcceptedThenRejectedOriginalAndAnotherUnconfirmedOriginal_When_HostReconciles_Then_OnlyUnrejectedOriginalIsSelected()
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        evidence.Frames.RemoveAll(frame => Text(frame["kind"]) == "terminal" && Text(frame["data"]!["attempt_id"]) == "c3");
        JsonObject kill = evidence.Frames[^1]; evidence.Frames.RemoveAt(evidence.Frames.Count - 1);
        evidence.Add("synchronous_rejection", new { attempt_id = "c4", prepared_sequence = 1, error_code = -1, error_reason = "synthetic" }); evidence.Frames.Add(kill);
        evidence.Change("first/termination.json", root => root["data"]!["kill_unconfirmed_attempt_ids"] = new JsonArray("c3"));
        evidence.Change("successor/completion.json", root =>
        {
            root["data"]!["kill_unconfirmed_attempt_ids"] = new JsonArray("c3");
            root["data"]!["invocations"]![0]!["predecessor_attempt_id"] = "c3"; root["data"]!["invocations"]![0]!["sequence"] = 3;
        });
        Resequence(evidence); evidence.Rehash(); using var run = NewRun(evidence); await AssertRunAsync(run);
        JsonNode arm = run.Data["arms"]![1]!; Assert.Equal(1, arm["unconfirmed_count"]!.GetValue<long>());
        Assert.Equal("c3", Text(Assert.Single(arm["members"]!.AsArray())!["original_attempt_id"]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_TwoSuccessorInvocationsInOriginalOrReversedOrder_When_HostRuns_Then_OnlyOriginalOrderAuthenticates(bool reverse)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        evidence.Frames.RemoveAll(frame => Text(frame["kind"]) == "terminal" && Text(frame["data"]!["attempt_id"]) == "c3");
        evidence.Change("first/termination.json", root => root["data"]!["kill_unconfirmed_attempt_ids"] = new JsonArray("c3", "c4"));
        evidence.Change("successor/completion.json", root =>
        {
            root["data"]!["kill_unconfirmed_attempt_ids"] = new JsonArray("c3", "c4");
            JsonObject later = root["data"]!["invocations"]![0]!.DeepClone().AsObject(); JsonObject earlier = later.DeepClone().AsObject();
            earlier["attempt_id"] = "r3"; earlier["predecessor_attempt_id"] = "c3"; earlier["sequence"] = 3;
            root["data"]!["invocations"] = reverse ? new JsonArray(later, earlier) : new JsonArray(earlier, later);
        });
        JsonObject row = JsonNode.Parse(evidence.Files["successor/ledger.jsonl"])!.AsObject(); JsonObject previous = row.DeepClone().AsObject(); previous["attempt_id"] = "r3";
        evidence.Files["successor/ledger.jsonl"] = Encoding.UTF8.GetBytes(previous.ToJsonString() + "\n" + row.ToJsonString() + "\n");
        Resequence(evidence); evidence.Rehash(); using var run = NewRun(evidence); await AssertRunAsync(run, reverse ? [SuccessorProblem] : []);
    }

    [Theory]
    [InlineData("PodName", "slash")]
    [InlineData("BlobServiceUri", "relative")]
    [InlineData("BlobServiceUri", "http")]
    [InlineData("BlobServiceUri", "userinfo")]
    [InlineData("BlobServiceUri", "query")]
    [InlineData("BlobServiceUri", "fragment")]
    [InlineData("BlobServiceUri", "path")]
    [InlineData("VerifierContainer", "invalid")]
    [InlineData("LedgerContainer", "invalid")]
    [InlineData("LoadGenImage", "prefix")]
    [InlineData("LoadGenImage", "hex")]
    [InlineData("VerifierImage", "prefix")]
    [InlineData("VerifierImage", "hex")]
    public async Task Given_SharedSyntaxGuardInputOnAbruptModeRoute_When_HostStarts_Then_RejectsBeforeEvidenceAccess(string option, string defect)
    {
        await kafka.ResetAsync(); using var run = NewRun(new());
        run.Configuration["Verifier:" + option] = defect switch
        {
            "slash" => "invalid/pod", "relative" => "relative", "http" => "http://synthetic.invalid/", "userinfo" => "https://name@synthetic.invalid/",
            "query" => "https://synthetic.invalid/?x=1", "fragment" => "https://synthetic.invalid/#x", "path" => "https://synthetic.invalid/path",
            "invalid" => "INVALID", "prefix" => "@sha256:" + new string('a', 64), _ => "registry.invalid/image@sha256:" + new string('g', 64)
        };
        Assert.Equal(1, await run.RunAsync()); Assert.Equal(0, run.Reads); Assert.Equal(0, run.Listings); Assert.Equal(0, run.Creates);
    }

    [Theory]
    [InlineData("PodName", 252, true)]
    [InlineData("PodName", 253, true)]
    [InlineData("PodName", 254, false)]
    [InlineData("VerifierContainer", 2, false)]
    [InlineData("VerifierContainer", 3, true)]
    [InlineData("VerifierContainer", 4, true)]
    [InlineData("VerifierContainer", 62, true)]
    [InlineData("VerifierContainer", 63, true)]
    [InlineData("VerifierContainer", 64, false)]
    [InlineData("LedgerContainer", 2, false)]
    [InlineData("LedgerContainer", 3, true)]
    [InlineData("LedgerContainer", 4, true)]
    [InlineData("LedgerContainer", 62, true)]
    [InlineData("LedgerContainer", 63, true)]
    [InlineData("LedgerContainer", 64, false)]
    [InlineData("LoadGenImage", 63, false)]
    [InlineData("LoadGenImage", 64, true)]
    [InlineData("LoadGenImage", 65, false)]
    [InlineData("VerifierImage", 63, false)]
    [InlineData("VerifierImage", 64, true)]
    [InlineData("VerifierImage", 65, false)]
    public async Task Given_SharedSyntaxLengthBelowAtOrAboveBoundaryOnAbruptRoute_When_HostStarts_Then_OnlyValidLengthsReadEvidence(string option, int length, bool valid)
    {
        await kafka.ResetAsync(); using var run = NewRun(new());
        run.Configuration["Verifier:" + option] = option.EndsWith("Image", StringComparison.Ordinal)
            ? (option == "LoadGenImage" ? "registry.invalid/loadgen@sha256:" + new string('b', length) : "registry.invalid/verifier@sha256:" + new string('c', length)) : new string('a', length);
        Assert.Equal(valid ? 0 : 1, await run.RunAsync());
        if (valid) { Assert.True(run.Reads > 0); Assert.Equal(1, run.Creates); }
        else { Assert.Equal(0, run.Reads); Assert.Equal(0, run.Listings); Assert.Equal(0, run.Creates); }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_TimeoutBelowAtOrAboveRepresentationLimitOnAbruptRoute_When_HostStarts_Then_OnlyRepresentableDurationReadsEvidence(int delta)
    {
        await kafka.ResetAsync(); using var run = NewRun(new());
        long maximum = (long)int.MaxValue * TimeSpan.TicksPerMillisecond;
        run.Configuration["Verifier:MetadataTimeout"] = TimeSpan.FromTicks(maximum + delta).ToString("c", CultureInfo.InvariantCulture);
        Assert.Equal(delta > 0 ? 1 : 0, await run.RunAsync());
        if (delta > 0) { Assert.Equal(0, run.Reads); Assert.Equal(0, run.Listings); Assert.Equal(0, run.Creates); }
        else { Assert.True(run.Reads > 0); Assert.Equal(1, run.Creates); }
    }

    [Fact]
    public async Task Given_ListedNameWithoutPodSeparator_When_HostListsEvidence_Then_FirstProblemPreventsDirectorySelection()
    {
        await kafka.ResetAsync(); using var run = NewRun(new()); run.Ledger[B10AbruptVerifyEvidence.Prefix + "orphan"] = [123, 125];
        await AssertRunAsync(run, FirstProblem); Assert.Null(run.Data["arms"]);
    }

    [Fact]
    public async Task Given_ListedObjectDisappearsBeforeRequiredRead_When_HostAuthenticatesFirstPod_Then_FirstEvidenceIsRejected()
    {
        await kafka.ResetAsync(); using var run = NewRun(new());
        string missing = B10AbruptVerifyEvidence.Prefix + B10AbruptVerifyEvidence.First + "/termination.json"; bool removed = false;
        run.BeforeRead = name => { if (name == missing) removed = run.Ledger.Remove(name); };
        await AssertRunAsync(run, FirstProblem); Assert.True(removed); Assert.Null(run.Data["arms"]);
    }

    [Theory]
    [InlineData("absent-control")]
    [InlineData("control-null-status")]
    [InlineData("successor-null-status")]
    [InlineData("successor-failure")]
    public async Task Given_AbsentControlTerminalOrNullableCapturedStatus_When_HostProjectsResends_Then_ObservedFieldsRetainTheirValues(string state)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        JsonObject terminal = evidence.Frames.Single(frame => Text(frame["kind"]) == "terminal" && Text(frame["data"]!["attempt_id"]) == "t4");
        if (state == "absent-control") evidence.Frames.Remove(terminal);
        else if (state == "control-null-status") terminal["data"]!["persistence_status"] = null;
        else
        {
            JsonObject row = JsonNode.Parse(evidence.Files["successor/ledger.jsonl"])!.AsObject(); row["persistence_status"] = null;
            if (state == "successor-failure") { row["delivered"] = false; row["error_code"] = -1; }
            evidence.Files["successor/ledger.jsonl"] = Encoding.UTF8.GetBytes(row.ToJsonString() + "\n");
        }
        Resequence(evidence); evidence.Rehash(); using var run = NewRun(evidence); await AssertRunAsync(run);
        JsonNode? projected = run.Data["arms"]![state.StartsWith("successor", StringComparison.Ordinal) ? 1 : 0]!["members"]![0]!["resend_terminal"];
        if (state == "absent-control") Assert.Null(projected);
        else
        {
            Assert.NotNull(projected); Assert.Null(projected["persistence_status"]);
            Assert.Equal(state != "successor-failure", projected["delivered"]!.GetValue<bool>());
            Assert.Equal(state == "successor-failure" ? -1 : 0, projected["error_code"]!.GetValue<int>());
        }
    }

    [Fact]
    public async Task Given_TotalInputBudgetBelowAtAndAboveMeasuredReads_When_HostRuns_Then_OnlyExactOrLargerBudgetAuthenticatesSuccessor()
    {
        await kafka.ResetAsync(); long bytes = 0;
        using (var baseline = NewRun(new()))
        {
            baseline.BeforeRead = name =>
            {
                if (name != B10AbruptVerifyHarness.Output) bytes += (baseline.Verifier.GetValueOrDefault(name) ?? baseline.Ledger.GetValueOrDefault(name))?.LongLength ?? 0;
            };
            await AssertRunAsync(baseline);
        }
        Assert.True(bytes > 0);
        foreach (int delta in new[] { -1, 0, 1 })
        {
            using var run = NewRun(new()); run.Configuration["Verifier:MaxTotalInputBytes"] = (bytes + delta).ToString(CultureInfo.InvariantCulture);
            await AssertRunAsync(run, delta < 0 ? [SuccessorProblem] : []); Assert.NotNull(run.Data["arms"]);
        }
    }

    private sealed class BoundaryClock : TimeProvider
    {
        private long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        internal void Advance(int milliseconds) => ticks += TimeSpan.FromMilliseconds(milliseconds).Ticks;
    }

    private B10AbruptVerifyHarness NewRun(B10AbruptVerifyEvidence evidence) => new(kafka.Bootstrap, evidence.Files);
    private static string? Text(JsonNode? node) => node?.GetValue<string>();
    private static async Task AssertRunAsync(B10AbruptVerifyHarness run, params string[] problems)
    {
        Assert.Equal(0, await run.RunAsync()); Assert.Equal(problems, run.Data["problems"]!.AsArray().Select(Text));
    }
    private static void Resequence(B10AbruptVerifyEvidence evidence)
    {
        var prepared = new Dictionary<string, long>(StringComparer.Ordinal);
        for (int index = 0; index < evidence.Frames.Count; index++)
        {
            JsonObject frame = evidence.Frames[index]; frame["sequence"] = index + 1L;
            string? id = Text(frame["data"]!["attempt_id"]);
            if (Text(frame["kind"]) == "prepared") prepared[id!] = index + 1L;
            else if (Text(frame["kind"]) is "accepted" or "synchronous_rejection") frame["data"]!["prepared_sequence"] = prepared[id!];
        }
    }
}
#pragma warning restore CA1707

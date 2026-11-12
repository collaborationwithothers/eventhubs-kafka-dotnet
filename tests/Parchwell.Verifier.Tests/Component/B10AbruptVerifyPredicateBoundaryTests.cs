using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.VerifyRun.Abrupt;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Publication;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707
[Collection("B10 abrupt verifier")]
[Trait("Tier", "Docker")]
public sealed class B10AbruptVerifyPredicateBoundaryTests(B10AbruptVerifyKafkaFixture kafka) : IClassFixture<B10AbruptVerifyKafkaFixture>
{
    private static readonly string[] MixedPublicationFailures = ["producer_completion_unavailable", "publication_membership_invalid"];

    [Theory]
    [InlineData("capture", 0)]
    [InlineData("capture", 1)]
    [InlineData("capture", 2)]
    [InlineData("count", 0)]
    [InlineData("count", 1)]
    [InlineData("count", 2)]
    [InlineData("fatal-count", -1)]
    [InlineData("fatal-count", 0)]
    [InlineData("fatal-count", 1)]
    public async Task Given_NativeCaptureAtEitherSideOfExactBinding_When_HostRuns_Then_OnlyExactCountAndSequencePass(string field, int value)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        JsonObject kill = evidence.Frames[^1]; evidence.Frames.RemoveAt(evidence.Frames.Count - 1);
        evidence.Add("native_error", new { capture_sequence = field == "capture" ? value : 1, error_code = 0, error_name = "synthetic", is_error = false, is_fatal = false, callback_monotonic_ticks = 1 });
        kill["sequence"] = evidence.Frames.Count + 1; evidence.Frames.Add(kill);
        evidence.Change("first/termination.json", root => { root["data"]!["native_error_count"] = field == "count" ? value : 1; root["data"]!["native_fatal_count"] = field == "fatal-count" ? value : 0; });
        evidence.Rehash(); using var run = NewRun(evidence);
        bool valid = value == (field == "fatal-count" ? 0 : 1); await AssertOnly(run, valid, "b10_abrupt_verify_first_evidence_invalid", "b10_restart_identity_unauthenticated");
    }

    [Theory]
    [InlineData("report", -1)]
    [InlineData("report", 0)]
    [InlineData("report", 1)]
    [InlineData("invocation", -1)]
    [InlineData("invocation", 0)]
    [InlineData("invocation", 1)]
    [InlineData("ready", -1)]
    [InlineData("ready", 0)]
    [InlineData("ready", 1)]
    public async Task Given_TimingOnEitherSideOfRequiredOrdering_When_HostRuns_Then_StrictAndInclusiveBoundsRemainDistinct(string field, int ticks)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        string utc = DateTimeOffset.Parse(B10AbruptVerifyEvidence.Utc, CultureInfo.InvariantCulture).AddTicks(ticks).ToString("O", CultureInfo.InvariantCulture);
        if (field == "report") evidence.Frames.First(frame => frame["kind"]!.GetValue<string>() == "pre_acceptance_report")["data"]!["report_capture_utc"] = utc;
        else evidence.Change("successor/completion.json", root => { if (field == "ready") root["data"]!["ready_utc"] = utc; else root["data"]!["invocations"]![0]!["invocation_utc"] = utc; });
        evidence.Rehash(); using var run = NewRun(evidence);
        bool valid = field == "report" ? ticks > 0 : field == "ready" ? ticks <= 0 : ticks >= 0;
        await AssertOnly(run, valid, field == "report" ? "b10_abrupt_verify_first_evidence_invalid" : null,
            field == "report" ? "b10_restart_identity_unauthenticated" : "b10_abrupt_successor_late");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Given_InventoryCountAroundInvocationCount_When_HostRuns_Then_OnlyExactCountAuthenticates(int count)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence(); evidence.Change("successor/manifest.json", root => root["data"]!["inventory_count"] = count); evidence.RehashSuccessor();
        using var run = NewRun(evidence); await AssertOnly(run, count == 1, "b10_abrupt_verify_successor_evidence_invalid", "b10_restart_successor_evidence_unavailable");
    }

    [Theory]
    [InlineData("txerrs", -1)]
    [InlineData("txerrs", 0)]
    [InlineData("txerrs", 1)]
    [InlineData("rxerrs", -1)]
    [InlineData("rxerrs", 0)]
    [InlineData("rxerrs", 1)]
    public async Task Given_FirstGuardCounterAroundZero_When_HostRuns_Then_OnlyZeroPasses(string field, int count)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        const string name = "same_instance-pre-acceptance-report.json";
        evidence.Change("first/" + name, root => root["brokers"]![B10AbruptVerifyEvidence.Broker]![field] = count);
        JsonObject descriptor = evidence.Frames.Single(frame => frame["kind"]!.GetValue<string>() == "pre_acceptance_report" && frame["data"]!["name"]!.GetValue<string>() == name)["data"]!.AsObject();
        descriptor["length"] = evidence.Files["first/" + name].Length; descriptor["sha256"] = B10AbruptVerifyEvidence.Hash(evidence.Files["first/" + name]); evidence.Rehash();
        using var run = NewRun(evidence); await AssertOnly(run, count == 0, null, "b10_restart_guard_failed");
    }

    [Theory]
    [InlineData("")]
    [InlineData("broker_lifecycle_inventory_unproven")]
    [InlineData("request_lifecycle_capture_unsupported")]
    [InlineData("unexpected")]
    public async Task Given_SuccessorGuardMarker_When_HostRuns_Then_OnlyEnumeratedMarkersArePermitted(string marker)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        evidence.Change("successor/capabilities.json", root => root["guard_observations"] = marker.Length == 0 ? new JsonArray() : new JsonArray(marker)); evidence.RehashSuccessor();
        using var run = NewRun(evidence); await AssertOnly(run, marker != "unexpected", null, "b10_restart_guard_failed");
    }

    [Theory]
    [InlineData("req_timeouts")]
    [InlineData("txretries")]
    public async Task Given_PermittedSuccessorCounters_When_HostRuns_Then_GuardStillPasses(string field)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        string[] reports = Encoding.UTF8.GetString(evidence.Files["successor/statistics.jsonl"]).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < reports.Length; index++)
        {
            JsonObject envelope = JsonNode.Parse(reports[index])!.AsObject(); JsonObject native = JsonNode.Parse(envelope["raw_json"]!.GetValue<string>())!.AsObject();
            native["brokers"]![B10AbruptVerifyEvidence.Broker]![field] = index + 1; envelope["raw_json"] = native.ToJsonString(); reports[index] = envelope.ToJsonString();
        }
        evidence.Files["successor/statistics.jsonl"] = Encoding.UTF8.GetBytes(string.Join('\n', reports) + "\n"); evidence.RehashSuccessor();
        using var run = NewRun(evidence); await AssertOnly(run, true, null, "b10_restart_guard_failed");
    }

    [Theory]
    [InlineData("pid", 0)]
    [InlineData("pid", 1)]
    [InlineData("pid", 2)]
    [InlineData("start_time_ticks", 0)]
    [InlineData("start_time_ticks", 1)]
    [InlineData("start_time_ticks", 2)]
    [InlineData("parent_observed_start_time_ticks", 0)]
    [InlineData("parent_observed_start_time_ticks", 1)]
    [InlineData("parent_observed_start_time_ticks", 2)]
    [InlineData("oom_kill", 0)]
    [InlineData("oom_kill", 1)]
    [InlineData("oom_kill", 2)]
    public async Task Given_ChildOrCgroupValueBelowAtOrAboveBinding_When_HostRuns_Then_OnlyEqualityPasses(string field, int value)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        evidence.Change("first/termination.json", root =>
        {
            if (field == "oom_kill") { root["data"]!["cgroup_before"]![field] = 1; root["data"]!["cgroup_after"]![field] = value; }
            else root["data"]!["child"]![field] = value;
        });
        evidence.RehashSuccessor(); using var run = NewRun(evidence); await AssertOnly(run, value == 1, "b10_abrupt_verify_first_evidence_invalid", "b10_restart_termination_unverified");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Given_ReportLeaderBelowAtOrAboveZero_When_MatchingBrokerIsUp_Then_NonnegativeLeaderPasses(int leader)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence(); const string name = "same_instance-pre-acceptance-report.json";
        evidence.Change("first/" + name, root => { root["topics"]!["document-events"]!["partitions"]!["0"]!["leader"] = leader; root["brokers"]![B10AbruptVerifyEvidence.Broker]!["nodeid"] = leader; });
        JsonNode descriptor = evidence.Frames.Single(frame => frame["kind"]!.GetValue<string>() == "pre_acceptance_report" && frame["data"]!["name"]!.GetValue<string>() == name)["data"]!;
        descriptor["length"] = evidence.Files["first/" + name].Length; descriptor["sha256"] = B10AbruptVerifyEvidence.Hash(evidence.Files["first/" + name]); evidence.Rehash();
        using var run = NewRun(evidence); await AssertOnly(run, leader >= 0, "b10_abrupt_verify_first_evidence_invalid", "b10_restart_identity_unauthenticated");
    }

    [Theory]
    [InlineData("pages", -1, false)]
    [InlineData("pages", 0, true)]
    [InlineData("pages", 15, true)]
    [InlineData("pages", 16, true)]
    [InlineData("pages", 17, false)]
    [InlineData("entries", 1023, true)]
    [InlineData("entries", 1024, true)]
    [InlineData("entries", 1025, false)]
    [InlineData("name-bytes", -1, true)]
    [InlineData("name-bytes", 0, true)]
    [InlineData("name-bytes", 1, false)]
    public async Task Given_TimeoutListingBelowAtOrAboveResourceBound_When_Reading_Then_OnlyWithinBoundAuthenticates(string field, int value, bool valid)
    {
        var evidence = new B10AbruptVerifyEvidence(); string registration = TimeoutRoot + B10AbruptVerifyEvidence.Timeout + "/registration.json";
        string[] names = [registration]; int pages = 1;
        if (field == "pages") pages = value;
        else if (field == "entries") names = Enumerable.Repeat(TimeoutRoot + B10AbruptVerifyEvidence.Timeout + "/entry", value - 1).Prepend(registration).ToArray();
        else
        {
            string prefix = TimeoutRoot + B10AbruptVerifyEvidence.Timeout + "/";
            names = [registration, prefix + new string('x', 1048576 + value - Encoding.UTF8.GetByteCount(registration) - Encoding.UTF8.GetByteCount(prefix))];
        }
        TimeoutEvidenceReader reader = ListingReader(evidence, new(names, pages, true));
        if (valid) Assert.Equal(B10AbruptVerifyEvidence.Timeout, (await reader.ReadListingAsync(TestContext.Current.CancellationToken)).InstanceId);
        else if (field == "name-bytes") await Assert.ThrowsAsync<InputLimitExceededException>(() => reader.ReadListingAsync(TestContext.Current.CancellationToken));
        else await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadListingAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("incomplete")]
    [InlineData("foreign-root")]
    [InlineData("no-directory")]
    [InlineData("directory-only")]
    [InlineData("second-directory")]
    [InlineData("duplicate-registration")]
    [InlineData("nested-registration")]
    [InlineData("run_id")]
    [InlineData("scenario_id")]
    [InlineData("configuration_id")]
    [InlineData("instance_id")]
    [InlineData("expected_instance_id")]
    [InlineData("pod_name")]
    [InlineData("phase")]
    [InlineData("repeat_number")]
    [InlineData("job_name")]
    [InlineData("predecessor_instance_id")]
    [InlineData("source_commit")]
    public async Task Given_TimeoutListingOrRegistrationBindingFault_When_Reading_Then_RejectsTheIndependentFault(string field)
    {
        var evidence = new B10AbruptVerifyEvidence(); string registration = TimeoutRoot + B10AbruptVerifyEvidence.Timeout + "/registration.json";
        string[] names = [registration]; bool complete = true;
        switch (field)
        {
            case "empty": names = []; break;
            case "incomplete": complete = false; break;
            case "foreign-root": names = ["foreign/registration.json"]; break;
            case "no-directory": names = [TimeoutRoot + "registration.json"]; break;
            case "directory-only": names = [TimeoutRoot + B10AbruptVerifyEvidence.Timeout + "/"]; break;
            case "second-directory": names = [registration, TimeoutRoot + "other/entry"]; break;
            case "duplicate-registration": names = [registration, registration]; break;
            case "nested-registration": names = [registration, TimeoutRoot + B10AbruptVerifyEvidence.Timeout + "/nested/registration.json"]; break;
            default:
                evidence.Change("timeout/registration.json", root =>
                {
                    if (field is "run_id" or "scenario_id" or "configuration_id" or "instance_id" or "expected_instance_id") root["identity"]![field] = "foreign";
                    else if (field == "repeat_number") root[field] = 2;
                    else root[field] = field == "source_commit" ? new string('b', 40) : "foreign";
                });
                break;
        }
        TimeoutEvidenceReader reader = ListingReader(evidence, new(names, 1, complete));
        Exception? exception = await Record.ExceptionAsync(() => reader.ReadListingAsync(TestContext.Current.CancellationToken));
        Assert.True(exception is InvalidDataException or FormatException or ArgumentException, exception?.ToString() ?? "listing unexpectedly accepted");
    }

    [Theory]
    [InlineData("object", -1)]
    [InlineData("object", 0)]
    [InlineData("object", 1)]
    [InlineData("aggregate", -1)]
    [InlineData("aggregate", 0)]
    [InlineData("aggregate", 1)]
    [InlineData("descriptors", -1)]
    [InlineData("descriptors", 0)]
    [InlineData("descriptors", 1)]
    [InlineData("descriptor-length", -1)]
    [InlineData("descriptor-length", 0)]
    [InlineData("descriptor-length", 1)]
    public void Given_TimeoutPublicationAtEitherSideOfExactOrMaximumBound_When_Accepting_Then_ReturnsOnlyThatFailure(string field, int delta)
    {
        TimeoutPublicationFixture fixture = PublicationFixture();
        long objectLimit = 1024, aggregateLimit = 1024;
        string expected = "";
        if (field == "object") { objectLimit = 1 + delta; if (delta < 0) expected = "publication_object_oversized"; }
        else if (field == "aggregate") { aggregateLimit = 6 + delta; if (delta < 0) expected = "publication_aggregate_oversized"; }
        else if (field == "descriptors")
        {
            fixture = fixture with { Index = fixture.Index! with { Artifacts = delta < 0 ? fixture.Index.Artifacts.Take(4).ToArray() : delta > 0 ? fixture.Index.Artifacts.Append(fixture.Index.Artifacts[0]).ToArray() : fixture.Index.Artifacts } };
            if (delta != 0) expected = "publication_descriptors_invalid";
        }
        else
        {
            ArtifactDescriptor[] descriptors = fixture.Index!.Artifacts.ToArray(); descriptors[0] = descriptors[0] with { Length = 1 + delta };
            fixture = fixture with { Index = fixture.Index with { Artifacts = descriptors } }; if (delta != 0) expected = "publication_reference_invalid";
        }
        PublicationReadOutcome result = Accept(fixture, objectLimit, aggregateLimit);
        Assert.Equal(expected.Length == 0 ? [] : new[] { expected }, result.RejectionCodes); Assert.Equal(expected.Length == 0, result.Accepted); Assert.False(result.Incomplete);
    }

    [Theory]
    [InlineData("object", 268435455L, long.MaxValue, true)]
    [InlineData("object", 268435456L, long.MaxValue, true)]
    [InlineData("object", 268435457L, long.MaxValue, false)]
    [InlineData("object", 268435456L, 268435455L, false)]
    [InlineData("object", 268435456L, 268435456L, true)]
    [InlineData("object", 268435456L, 268435457L, true)]
    [InlineData("aggregate", 1073741823L, long.MaxValue, true)]
    [InlineData("aggregate", 1073741824L, long.MaxValue, true)]
    [InlineData("aggregate", 1073741825L, long.MaxValue, false)]
    [InlineData("aggregate", 1073741824L, 1073741823L, false)]
    [InlineData("aggregate", 1073741824L, 1073741824L, true)]
    [InlineData("aggregate", 1073741824L, 1073741825L, true)]
    public void Given_TimeoutPublicationLengthOrConfiguredLimitAroundPhysicalCap_When_CheckingByteBound_Then_TheSmallerLimitIsInclusive(string field, long length, long configuredLimit, bool fits)
    {
        Assert.Equal(fits, field == "object" ? TimeoutPublicationAcceptance.FitsObjectBytes(length, configuredLimit) : TimeoutPublicationAcceptance.FitsAggregateBytes(length, configuredLimit));
    }

    [Theory]
    [InlineData("missing-member", "publication_incomplete", true)]
    [InlineData("extra-member", "publication_membership_invalid", false)]
    [InlineData("duplicate-member", "publication_incomplete", true)]
    [InlineData("nested-member", "publication_membership_invalid", false)]
    [InlineData("missing-payload", "publication_object_missing", true)]
    [InlineData("empty-descriptors", "publication_descriptors_invalid", false)]
    [InlineData("descriptor-order", "publication_descriptors_invalid", false)]
    [InlineData("descriptor-hash", "publication_reference_invalid", false)]
    [InlineData("index", "publication_index_unavailable", true)]
    [InlineData("index-identity", "publication_identity_invalid", false)]
    [InlineData("worker-status", "publication_completion_invalid", false)]
    [InlineData("bookkeeping", "publication_completion_invalid", false)]
    [InlineData("worker", "publication_completion_invalid", false)]
    [InlineData("publication", "publication_completion_invalid", false)]
    [InlineData("manifest", "publication_manifest_unavailable", true)]
    [InlineData("manifest-identity", "publication_manifest_identity_invalid", false)]
    [InlineData("restart", "publication_restart_role_mismatch", false)]
    [InlineData("receipt", "producer_completion_unavailable", true)]
    [InlineData("receipt-exit", "producer_completion_invalid", false)]
    [InlineData("receipt-schema", "producer_completion_invalid", false)]
    [InlineData("receipt-identity", "producer_completion_invalid", false)]
    [InlineData("receipt-phase", "producer_completion_invalid", false)]
    [InlineData("receipt-repeat", "producer_completion_invalid", false)]
    [InlineData("receipt-job", "producer_completion_invalid", false)]
    [InlineData("receipt-pod", "producer_completion_invalid", false)]
    [InlineData("receipt-uid", "producer_completion_invalid", false)]
    [InlineData("receipt-source", "producer_completion_invalid", false)]
    [InlineData("receipt-mode", "producer_completion_invalid", false)]
    [InlineData("receipt-cleanup", "producer_completion_invalid", false)]
    [InlineData("start", "start_line_unavailable", true)]
    [InlineData("start-names", "start_line_invalid", false)]
    [InlineData("start-identity", "start_line_invalid", false)]
    [InlineData("start-commit", "start_line_invalid", false)]
    [InlineData("inventory", "broker_inventory_unavailable", true)]
    [InlineData("inventory-commit", "broker_inventory_invalid", false)]
    [InlineData("inventory-identity", "broker_inventory_invalid", false)]
    [InlineData("source-commit", "publication_source_invalid", false)]
    [InlineData("source-reference", "publication_source_invalid", false)]
    [InlineData("reference", "publication_reference_unavailable", true)]
    public void Given_TimeoutPublicationIndependentFault_When_Accepting_Then_ReturnsExactCodesAndCompleteness(string field, string code, bool incomplete)
    {
        TimeoutPublicationFixture f = PublicationFixture();
        switch (field)
        {
            case "missing-member": f = f with { Listing = new(f.Listing.Names.Skip(1).ToArray(), 1, true) }; break;
            case "extra-member": f = f with { Listing = new(f.Listing.Names.Append(f.Prefix + "publication/extra").ToArray(), 1, true) }; break;
            case "duplicate-member": f = f with { Listing = new(f.Listing.Names.Append(f.Listing.Names[0]).ToArray(), 1, true) }; break;
            case "nested-member": f = f with { Listing = new(f.Listing.Names.Append(f.Prefix + "publication/nested/entry").ToArray(), 1, true) }; break;
            case "missing-payload": f.Payloads["index.json"] = null; break;
            case "empty-descriptors": f = f with { Index = f.Index! with { Artifacts = [] } }; break;
            case "descriptor-order": f = f with { Index = f.Index! with { Artifacts = f.Index.Artifacts.Reverse().ToArray() } }; break;
            case "descriptor-hash": ArtifactDescriptor[] descriptors = f.Index!.Artifacts.ToArray(); descriptors[0] = descriptors[0] with { Sha256 = new string('f', 64) }; f = f with { Index = f.Index with { Artifacts = descriptors } }; break;
            case "index": f = f with { Index = null }; break;
            case "index-identity": f = f with { Index = f.Index! with { Identity = f.Registration.Identity with { ClientId = "foreign" } } }; break;
            case "worker-status": f = f with { Index = f.Index! with { WorkerStatus = "incomplete" } }; break;
            case "bookkeeping": f = f with { Index = f.Index! with { BookkeepingComplete = false } }; break;
            case "worker": f = f with { Index = f.Index! with { WorkerComplete = false } }; break;
            case "publication": f = f with { Index = f.Index! with { PublicationComplete = false } }; break;
            case "manifest": f = f with { Manifest = null }; break;
            case "manifest-identity": f = f with { Manifest = f.Manifest! with { Identity = f.Registration.Identity with { ClientId = "foreign" } } }; break;
            case "restart": f = f with { Registration = f.Registration with { JobName = PhaseIdentity.RestartJobName(B10AbruptVerifyEvidence.Run, "C1", 1) } }; f = f with { Receipt = f.Receipt! with { JobName = f.Registration.JobName } }; break;
            case "receipt": f = f with { Receipt = null }; break;
            case "receipt-schema": f = f with { Receipt = f.Receipt! with { SchemaVersion = 2 } }; break;
            case "receipt-identity": f = f with { Receipt = f.Receipt! with { Identity = f.Registration.Identity with { ClientId = "foreign" } } }; break;
            case "receipt-phase": f = f with { Receipt = f.Receipt! with { Phase = "C0" } }; break;
            case "receipt-repeat": f = f with { Receipt = f.Receipt! with { RepeatNumber = 2 } }; break;
            case "receipt-job": f = f with { Receipt = f.Receipt! with { JobName = "foreign" } }; break;
            case "receipt-pod": f = f with { Receipt = f.Receipt! with { PodName = "foreign" } }; break;
            case "receipt-uid": f = f with { Receipt = f.Receipt! with { PodUid = "foreign" } }; break;
            case "receipt-source": f = f with { Receipt = f.Receipt! with { SourceCommit = new string('b', 40) } }; break;
            case "receipt-mode": f = f with { Receipt = f.Receipt! with { Mode = "verify" } }; break;
            case "receipt-cleanup": f = f with { Receipt = f.Receipt! with { CleanupCompleted = false } }; break;
            case "receipt-exit": f = f with { Receipt = f.Receipt! with { HandlerExitCode = 1 } }; break;
            case "start": f = f with { Start = null }; break;
            case "start-identity": f = f with { Start = f.Start! with { Identity = f.Start.Identity with { ClientId = "foreign" } } }; break;
            case "start-commit": f = f with { Start = f.Start! with { VerifierCommit = new string('b', 40) } }; break;
            case "start-names": f = f with { Start = f.Start! with { ExpectedJobNames = [] } }; break;
            case "inventory": f = f with { Inventory = null }; break;
            case "inventory-identity": f = f with { Inventory = f.Inventory! with { Identity = f.Inventory.Identity with { ClientId = "foreign" } } }; break;
            case "source-commit": f = f with { Registration = f.Registration with { SourceCommit = new string('b', 40) }, Receipt = f.Receipt! with { SourceCommit = new string('b', 40) } }; break;
            case "source-reference": f = f with { StartReference = f.StartReference with { Name = "foreign/C1/r1/start-line.json" } }; break;
            case "inventory-commit": f = f with { Inventory = f.Inventory! with { SourceCommit = new string('b', 40) } }; break;
            case "reference": f = f with { ProducerReference = null }; break;
        }
        PublicationReadOutcome result = Accept(f); Assert.Equal(new[] { code }, result.RejectionCodes); Assert.Equal(incomplete, result.Incomplete); Assert.False(result.Accepted);
    }

    [Fact]
    public void Given_TimeoutPublicationIntegrityFaultAndMissingEvidence_When_Accepting_Then_IntegrityKeepsPrecedence()
    {
        TimeoutPublicationFixture f = PublicationFixture(); f = f with { Listing = new(f.Listing.Names.Append(f.Prefix + "publication/extra").ToArray(), 1, true), Receipt = null };
        PublicationReadOutcome result = Accept(f); Assert.Equal(MixedPublicationFailures, result.RejectionCodes); Assert.False(result.Incomplete); Assert.False(result.Accepted);
    }

    [Theory]
    [InlineData("invocation_ordinal", -1)]
    [InlineData("invocation_ordinal", 0)]
    [InlineData("invocation_ordinal", 1)]
    [InlineData("sequence", -1)]
    [InlineData("sequence", 0)]
    [InlineData("sequence", 1)]
    public async Task Given_SuccessorInventoryEntryBelowAtOrAboveInvocationBinding_When_HostRuns_Then_OnlyExactValueAuthenticates(string field, int delta)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        evidence.Change("successor/manifest.json", root => root["data"]!["inventory"]![0]![field] = (field == "sequence" ? 4 : 0) + delta);
        evidence.RehashSuccessor(); using var run = NewRun(evidence);
        await AssertOnly(run, delta == 0, "b10_abrupt_verify_successor_evidence_invalid", "b10_restart_successor_evidence_unavailable");
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("missing")]
    [InlineData("corrupt")]
    [InlineData("extra")]
    public async Task Given_TimeoutPublicationEvidence_When_HostRuns_Then_OnlyInvalidPublicationAddsBookkeepingReason(string state)
    {
        await kafka.ResetAsync(); using var run = NewRun(new()); string prefix = TimeoutRoot + B10AbruptVerifyEvidence.Timeout + "/publication/";
        if (state == "missing") run.Ledger.Remove(prefix + "index.json");
        else if (state == "corrupt") run.Ledger[prefix + "ledger.jsonl"] = Encoding.UTF8.GetBytes("{}\n");
        else if (state == "extra") run.Ledger[prefix + "extra.json"] = [];
        await AssertOnly(run, state == "valid", "b10_abrupt_verify_timeout_evidence_invalid", "b10_restart_timeout_bookkeeping_unavailable");
        Assert.NotNull(run.Data["arms"]); Assert.NotEqual("unavailable", run.Data["comparison"]!.GetValue<string>());
        if (state == "valid") Assert.NotNull(run.Data["timeout"]); else Assert.Null(run.Data["timeout"]);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("resolved-null")]
    [InlineData("settings-below")]
    [InlineData("settings-above")]
    [InlineData("settings-key")]
    [InlineData("setting-value")]
    [InlineData("override-absent")]
    [InlineData("override-extra")]
    [InlineData("override-scenario")]
    [InlineData("override-configuration")]
    [InlineData("timeout-value")]
    [InlineData("timeout-override-missing")]
    [InlineData("timeout-override-present")]
    [InlineData("baseline-key")]
    [InlineData("baseline-value")]
    [InlineData("baseline-value-null")]
    [InlineData("baseline-role-below")]
    [InlineData("baseline-role-above")]
    [InlineData("baseline-disposition-below")]
    [InlineData("baseline-disposition-above")]
    [InlineData("baseline-inventory-empty")]
    [InlineData("baseline-inventory-value")]
    [InlineData("baseline-inventory-order")]
    [InlineData("baseline-reason")]
    [InlineData("baseline-sources-below")]
    [InlineData("baseline-sources-above")]
    [InlineData("baseline-source-url")]
    [InlineData("baseline-source-version")]
    public async Task Given_SuccessorConfigurationFidelityFault_When_HostRuns_Then_OnlyGuardReasonChanges(string field)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        evidence.Change("successor/capabilities.json", root =>
        {
            JsonObject settings = root["resolved_configuration"]!["settings"]!.AsObject();
            JsonObject timeout = settings["message.timeout.ms"]!.AsObject(), baseline = timeout["baseline"]!.AsObject();
            JsonNode provenance = settings["enable.idempotence"]!["override"]!;
            switch (field)
            {
                case "valid": break;
                case "resolved-null": root["resolved_configuration"] = null; break;
                case "settings-below": settings.Remove("linger.ms"); break;
                case "settings-above": settings["foreign"] = settings["linger.ms"]!.DeepClone(); break;
                case "settings-key": settings["foreign"] = settings["linger.ms"]!.DeepClone(); settings.Remove("linger.ms"); break;
                case "setting-value": settings["linger.ms"]!["value"] = "999"; break;
                case "override-absent": settings["enable.idempotence"]!["override"] = null; break;
                case "override-extra": settings["linger.ms"]!["override"] = provenance.DeepClone(); break;
                case "override-scenario": provenance["scenario_id"] = "foreign"; break;
                case "override-configuration": provenance["configuration_id"] = "foreign"; break;
                case "timeout-value": timeout["value"] = "100"; break;
                case "timeout-override-missing": timeout.Remove("override"); break;
                case "timeout-override-present": timeout["override"] = provenance.DeepClone(); break;
                case "baseline-key": baseline["key"] = "foreign"; break;
                case "baseline-value": baseline["value"] = "999"; break;
                case "baseline-value-null": baseline["value"] = null; break;
                case "baseline-role-below": baseline["role"] = baseline["role"]!.GetValue<int>() - 1; break;
                case "baseline-role-above": baseline["role"] = baseline["role"]!.GetValue<int>() + 1; break;
                case "baseline-disposition-below": baseline["disposition"] = baseline["disposition"]!.GetValue<int>() - 1; break;
                case "baseline-disposition-above": baseline["disposition"] = baseline["disposition"]!.GetValue<int>() + 1; break;
                case "baseline-inventory-empty": baseline["inventory_ids"] = new JsonArray(); break;
                case "baseline-inventory-value": baseline["inventory_ids"]![0] = "foreign"; break;
                case "baseline-inventory-order": baseline["inventory_ids"] = new JsonArray(baseline["inventory_ids"]!.AsArray().Reverse().Select(node => node!.DeepClone()).ToArray()); break;
                case "baseline-reason": baseline["selection_reason"] = "foreign"; break;
                case "baseline-sources-below": JsonArray below = baseline["sources"]!.AsArray(); below.RemoveAt(below.Count - 1); break;
                case "baseline-sources-above": JsonArray above = baseline["sources"]!.AsArray(); above.Add(above[0]!.DeepClone()); break;
                case "baseline-source-url": baseline["sources"]![0]!["url"] = "https://invalid.example/foreign"; break;
                case "baseline-source-version": baseline["sources"]![0]!["version_or_date"] = "foreign"; break;
            }
        });
        var limits = new InputLimits(32, 268435456, 2147483648, 1048576, 10000, 10000, 4096);
        Assert.Equal(field != "timeout-override-missing", CapturedEvidenceReader.ReadCapabilities(evidence.Files["successor/capabilities.json"], "capabilities.json", limits, new(limits), captureOnly: true).IsSuccess);
        evidence.RehashSuccessor(); using var run = NewRun(evidence); await AssertOnly(run, field == "valid", null, "b10_restart_guard_failed");
        Assert.NotNull(run.Data["arms"]); Assert.NotNull(run.Data["timeout"]); Assert.NotEqual("unavailable", run.Data["comparison"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("row-bytes", -1)]
    [InlineData("row-bytes", 0)]
    [InlineData("row-bytes", 1)]
    [InlineData("rows", -1)]
    [InlineData("rows", 0)]
    [InlineData("rows", 1)]
    [InlineData("inventory", -1)]
    [InlineData("inventory", 0)]
    [InlineData("inventory", 1)]
    public async Task Given_TimeoutParserLimitBelowAtOrAboveEvidence_When_HostRuns_Then_OnlyWithinLimitKeepsTimeoutCounts(string field, int delta)
    {
        await kafka.ResetAsync(); var evidence = new B10AbruptVerifyEvidence();
        JsonObject[] rows = Enumerable.Range(0, 4).Select(index => B10AbruptVerifyEvidence.TimeoutRow("t" + index.ToString(CultureInfo.InvariantCulture), new string('k', 8192) + index.ToString(CultureInfo.InvariantCulture), index)).ToArray(); evidence.SetTimeoutRows(rows);
        using var run = NewRun(evidence);
        if (field == "row-bytes") run.Configuration["Verifier:MaxRowBytes"] = (Encoding.UTF8.GetByteCount(rows.MaxBy(row => row.ToJsonString().Length)!.ToJsonString()) + delta).ToString(CultureInfo.InvariantCulture);
        else run.Configuration[field == "rows" ? "Verifier:MaxRowsPerArtifact" : "Verifier:MaxInventoryEntries"] = (4 + delta).ToString(CultureInfo.InvariantCulture);
        await AssertOnly(run, delta >= 0, "b10_abrupt_verify_timeout_evidence_invalid", "b10_restart_timeout_bookkeeping_unavailable");
        Assert.NotNull(run.Data["arms"]);
        if (delta >= 0) { Assert.NotNull(run.Data["timeout"]); Assert.Equal(4, run.Data["timeout"]!["loss"]!.GetValue<long>()); }
        else Assert.Null(run.Data["timeout"]);
    }

    private const string TimeoutRoot = "synthetic/C1/r1/producer/";
    private static TimeoutEvidenceReader ListingReader(B10AbruptVerifyEvidence evidence, BlobListing listing)
    {
        var limits = new InputLimits(32, 268435456, 2147483648, 1048576, 10000, 10000, 4096);
        var options = new VerifierHostOptions { RunId = B10AbruptVerifyEvidence.Run, VerifierCommit = B10AbruptVerifyEvidence.Commit, MaxArtifactBytes = limits.MaxArtifactBytes };
        return new(options, (_, _, _) => Task.FromResult<byte[]?>(evidence.Files["timeout/registration.json"]), (_, _) => Task.FromResult(listing), limits, new(limits));
    }
    private static TimeoutPublicationFixture PublicationFixture()
    {
        string job = PhaseIdentity.JobName(B10AbruptVerifyEvidence.Run, "C1", 1), prefix = TimeoutRoot + B10AbruptVerifyEvidence.Timeout + "/";
        var identity = new RunIdentity("B10", "C1", B10AbruptVerifyEvidence.Run, B10AbruptVerifyEvidence.Timeout, B10AbruptVerifyEvidence.Timeout, B10AbruptVerifyEvidence.Timeout);
        var startIdentity = new RunIdentity("B10", "C1", B10AbruptVerifyEvidence.Run, job, job, job);
        var registration = new ProducerRegistration(1, identity, "C1", 1, job, B10AbruptVerifyEvidence.Timeout, "uid-timeout", B10AbruptVerifyEvidence.Commit);
        var payloads = TimeoutPublicationAcceptance.PublicationNames.ToDictionary(name => name, name => (byte[]?)[(byte)name.Length], StringComparer.Ordinal);
        ArtifactDescriptor[] descriptors = TimeoutPublicationAcceptance.DescriptorNames.Select(name => new ArtifactDescriptor(name, 1, B10AbruptVerifyEvidence.Hash(payloads[name]!))).ToArray();
        var index = new ArtifactIndex(1, identity, "complete", true, true, true, descriptors);
        var manifest = new SealedRunManifest(1, identity, "generator", 0, 0, [], 1, 0, "complete", true, ParsedField<long>.Missing(), [], new Dictionary<string, string>(), new Dictionary<string, string>(), []);
        string[] names = [job, job + "2"];
        var start = new StartLineNote(1, startIdentity, "document-events", [], B10AbruptVerifyEvidence.Utc, B10AbruptVerifyEvidence.Commit, "managed", "native", names);
        var inventory = new BrokerInventory(1, startIdentity, [], "metadata", B10AbruptVerifyEvidence.Commit);
        var receipt = new ProducerCompletionReceipt(1, identity, "C1", 1, job, B10AbruptVerifyEvidence.Timeout, "uid-timeout", B10AbruptVerifyEvidence.Commit, "loadgen", 0, true);
        return new(new(payloads.Keys.Select(name => prefix + "publication/" + name).ToArray(), 1, true), prefix, payloads, registration, index, manifest, start, inventory, receipt,
            new("verifier", "synthetic/C1/r1/start-line.json", 1, B10AbruptVerifyEvidence.Hash([1])), new("verifier", "synthetic/C1/r1/broker-inventory.json", 1, B10AbruptVerifyEvidence.Hash([2])), new("ledger", prefix + "completion.json", 1, B10AbruptVerifyEvidence.Hash([3])), names);
    }
    private static PublicationReadOutcome Accept(TimeoutPublicationFixture f, long objectLimit = 1024, long aggregateLimit = 1024) => TimeoutPublicationAcceptance.Evaluate(f.Listing, f.Prefix, f.Payloads, f.Registration, f.Receipt, f.Index, f.Manifest, f.Start, f.Inventory, B10AbruptVerifyEvidence.Commit, f.StartReference, f.InventoryReference, f.ProducerReference, objectLimit, aggregateLimit, f.ExpectedNames);
    private sealed record TimeoutPublicationFixture(BlobListing Listing, string Prefix, Dictionary<string, byte[]?> Payloads, ProducerRegistration Registration, ArtifactIndex? Index, SealedRunManifest? Manifest, StartLineNote? Start, BrokerInventory? Inventory, ProducerCompletionReceipt? Receipt, ArtifactReference StartReference, ArtifactReference InventoryReference, ArtifactReference? ProducerReference, string[] ExpectedNames);

    private B10AbruptVerifyHarness NewRun(B10AbruptVerifyEvidence evidence) => new(kafka.Bootstrap, evidence.Files);
    private static async Task AssertOnly(B10AbruptVerifyHarness run, bool valid, string? problem, string reason)
    {
        Assert.Equal(0, await run.RunAsync());
        Assert.Equal(valid || problem is null ? [] : new[] { problem }, run.Data["problems"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Equal(valid ? [] : new[] { reason }, run.Data["eligibility_reasons"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Equal(valid ? "pending_sweep" : "ineligible", run.Data["eligibility"]!.GetValue<string>());
    }
}
#pragma warning restore CA1707

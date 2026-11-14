using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Xunit;
using Parchwell.Verifier.Tests.Support;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707
[Collection("B10 abrupt verifier")]
[Trait("Tier", "Docker")]
public sealed class B10ScientificThreeJobVerifyTests(B10AbruptVerifyKafkaFixture kafka) : IClassFixture<B10AbruptVerifyKafkaFixture>
{
    private const string Run = "b10-1-1";

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Given_ScientificRepeat_When_Verified_Then_OutputAndLastReceiptBindExactBytes(int repeat)
    {
        await kafka.ResetAsync(); using var run = NewRun(repeat);
        Assert.Equal(0, await run.RunAsync()); AssertValid(run);
        Assert.Equal(repeat, run.Result["identity"]!["repeat"]!.GetValue<int>());
        Assert.Equal("scientific", run.Result["purpose"]!.GetValue<string>());
        Assert.Equal(0, run.Data["earlier_repeat_records"]!.GetValue<long>());
        WorkerReceipt receipt = PhaseWireCodec.ReadReceipt(run.Verifier[run.ReceiptName]);
        Assert.Equal("b10-abrupt-verify", receipt.Mode); Assert.Null(receipt.Identity);
        Assert.Equal($"{Run}-c1-r{repeat}-b10-abrupt-verify", receipt.JobName);
        Assert.Equal(repeat, receipt.Repeat); Assert.Equal(0, receipt.HandlerExitCode); Assert.True(receipt.CleanupCompleted);
        Assert.Null(receipt.B10Intake); Assert.Null(receipt.PublicationAcceptance); Assert.Null(receipt.RepeatChecks);
        ArtifactReference artifact = Assert.Single(receipt.Artifacts);
        Assert.Equal("verifier", artifact.Container); Assert.Equal(run.ResultName, artifact.Name);
        Assert.Equal(run.Verifier[run.ResultName].LongLength, artifact.Length);
        Assert.Equal(B10AbruptVerifyEvidence.Hash(run.Verifier[run.ResultName]), artifact.Sha256);
        Assert.Equal([run.ResultName, run.ReceiptName], run.Writes);
        Assert.Equal(2 * (repeat - 1), run.ListedPrefixes.Count(prefix => !prefix.Contains($"/r{repeat}/", StringComparison.Ordinal)));
        byte[] resultBytes = run.Verifier[run.ResultName];
        Assert.Throws<FormatException>(() => PhaseWireCodec.ReadReceipt(resultBytes));
        Assert.Throws<FormatException>(() => B10SweepEvidenceCodec.ReadFinalization(resultBytes));
        Assert.False(VerifierResultCodec.Read(resultBytes, run.ResultName,
            new InputLimits(32, 268435456, 2147483648, 1048576, 10000, 10000, 4096), 100).IsSuccess);
    }

    [Fact]
    public async Task Given_EarlierTimeoutRecord_When_Verified_Then_RecordIsHeldOutOfEveryCountAndJoin()
    {
        await kafka.ResetAsync(); using var run = NewRun();
        RegisterEarlier(run, 2, "earlier-timeout");
        await run.ProduceAsync(0, 0, instance: "earlier-timeout", attempt: "unknown", key: "earlier-key");
        Assert.Equal(0, await run.RunAsync()); AssertValid(run);
        Assert.Equal(1, run.Data["earlier_repeat_records"]!.GetValue<long>());
        Assert.All(run.Data["timeout"]!.AsObject(), count => Assert.Equal(0, count.Value!.GetValue<long>()));
        Assert.All(run.Data["arms"]!.AsArray(), arm => Assert.Equal(0, arm!["originals_seen"]!.GetValue<long>()));
    }

    [Theory]
    [InlineData("listing")]
    [InlineData("invalid")]
    [InlineData("duplicate")]
    public async Task Given_UnavailableEarlierRegistration_When_Verified_Then_OnlyEarlierProblemAndReasonAreAdded(string defect)
    {
        await kafka.ResetAsync(); using var run = NewRun();
        if (defect == "listing") run.FailListingPrefix = $"{Run}/C1/r2/producer/";
        else if (defect == "invalid") run.Ledger[$"{Run}/C1/r2/producer/earlier/registration.json"] = Encoding.UTF8.GetBytes("{}");
        else RegisterEarlier(run, 1, Run + "-timeout");
        Assert.Equal(0, await run.RunAsync());
        Assert.Equal(["b10_abrupt_verify_earlier_repeat_unavailable"], Strings(run.Data["problems"]!));
        Assert.Equal(["b10_restart_identity_unauthenticated"], Strings(run.Data["eligibility_reasons"]!));
        Assert.Null(run.Data["earlier_repeat_records"]); Assert.True(run.Data["window_complete"]!.GetValue<bool>());
        Assert.NotNull(run.Data["timeout"]); Assert.NotNull(run.Data["arms"]);
    }

    [Fact]
    public async Task Given_FirstRegistrationReusesEarlierIdentityAndJournalIsInvalid_When_Verified_Then_BothIndependentChecksFail()
    {
        await kafka.ResetAsync(); using var run = NewRun();
        RegisterEarlier(run, 1, Run + "-first");
        run.Ledger[$"{Run}/C1/r3/b10-abrupt/{Run}-first/journal.jsonl"] = Encoding.UTF8.GetBytes("{}\n");
        Assert.Equal(0, await run.RunAsync());
        Assert.Equal(["b10_abrupt_verify_first_evidence_invalid", "b10_abrupt_verify_earlier_repeat_unavailable"], Strings(run.Data["problems"]!));
        Assert.Equal(["b10_restart_identity_unauthenticated"], Strings(run.Data["eligibility_reasons"]!));
        Assert.Null(run.Data["earlier_repeat_records"]);
        Assert.NotNull(run.Data["timeout"]); Assert.Null(run.Data["arms"]);
    }

    [Theory]
    [InlineData("journal.jsonl")]
    [InlineData("registration.json")]
    public async Task Given_SuccessorRegistrationReusesEarlierIdentityAndFirstEvidenceIsInvalid_When_Verified_Then_BothIndependentChecksFail(string firstFile)
    {
        await kafka.ResetAsync(); using var run = NewRun();
        RegisterEarlier(run, 1, Run + "-second");
        run.Ledger[$"{Run}/C1/r3/b10-abrupt/{Run}-first/{firstFile}"] = Encoding.UTF8.GetBytes("{}\n");
        Assert.Equal(0, await run.RunAsync());
        Assert.Equal(["b10_abrupt_verify_first_evidence_invalid", "b10_abrupt_verify_earlier_repeat_unavailable"], Strings(run.Data["problems"]!));
        string[] expectedReasons = firstFile == "registration.json"
            ? ["b10_restart_first_evidence_unavailable", "b10_restart_identity_unauthenticated"]
            : ["b10_restart_identity_unauthenticated"];
        Assert.Equal(expectedReasons, Strings(run.Data["eligibility_reasons"]!));
        Assert.True(run.Data["window_complete"]!.GetValue<bool>());
        Assert.Null(run.Data["earlier_repeat_records"]);
        Assert.NotNull(run.Data["timeout"]); Assert.Null(run.Data["arms"]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task Given_UndrainedRepeat_When_Verified_Then_EarlierCheckIsNotEvaluated(int repeat)
    {
        await kafka.ResetAsync(); using var run = NewRun(repeat);
        run.Verifier.Remove(PhasePaths.StartLineCompletion(Run, "C1", repeat));
        run.FailListingPrefix = $"{Run}/C1/r2/producer/";
        Assert.Equal(0, await run.RunAsync());
        if (repeat == 1) Assert.Equal(0, run.Data["earlier_repeat_records"]!.GetValue<long>());
        else Assert.Null(run.Data["earlier_repeat_records"]);
        Assert.Equal(["b10_abrupt_verify_start_line_invalid"], Strings(run.Data["problems"]!));
        Assert.DoesNotContain(run.FailListingPrefix, run.ListedPrefixes);
    }

    [Theory]
    [InlineData("existing")]
    [InlineData("write")]
    [InlineData("readback")]
    public async Task Given_ReceiptPublicationFails_When_Verified_Then_ExitIsNonzeroAndResultRemainsCreatedOnce(string defect)
    {
        await kafka.ResetAsync(); using var run = NewRun();
        byte[] existing = Encoding.UTF8.GetBytes("existing");
        if (defect == "existing") run.Verifier[run.ReceiptName] = existing;
        if (defect == "write") run.FailCreateSuffix = "verify-completion.json";
        if (defect == "readback") run.BadReadbackSuffix = "verify-completion.json";
        Assert.NotEqual(0, await run.RunAsync());
        AssertValid(run); Assert.Single(run.Writes, name => name == run.ResultName);
        if (defect == "existing") Assert.Same(existing, run.Verifier[run.ReceiptName]);
    }

    [Fact]
    public async Task Given_ResultReadbackFails_When_Verified_Then_NoReceiptIsWritten()
    {
        await kafka.ResetAsync(); using var run = NewRun(); run.ChangeReadback = true;
        Assert.NotEqual(0, await run.RunAsync()); Assert.False(run.Verifier.ContainsKey(run.ReceiptName));
    }

    [Theory]
    [InlineData("prefix")]
    [InlineData("repeat")]
    public async Task Given_InvalidScientificOptions_When_Verified_Then_NoEvidenceIsRead(string defect)
    {
        await kafka.ResetAsync(); using var run = NewRun();
        run.Configuration[defect == "prefix" ? "Verifier:ArtifactPrefix" : "Verifier:Repeat"] = defect == "prefix" ? $"{Run}/C1/r2/producer/" : "6";
        Assert.Equal(1, await run.RunAsync()); Assert.Equal(0, run.Reads); Assert.Equal(0, run.Listings); Assert.Equal(0, run.Creates);
    }

    private B10ScientificThreeJobHarness NewRun(int repeat = 3) => new(kafka.Bootstrap, new B10AbruptVerifyEvidence().Files, repeat: repeat);
    private static void RegisterEarlier(B10ScientificThreeJobHarness run, int repeat, string instance)
    {
        var identity = new Parchwell.Verifier.Common.Contracts.RunIdentity("B10", "C1", Run, instance, instance, instance);
        run.Ledger[PhasePaths.Registration(Run, "C1", repeat, instance)] = PhaseWireCodec.WriteRegistration(new ProducerRegistration(1,
            identity, "C1", repeat, PhaseIdentity.JobName(Run, "C1", repeat), instance, "earlier-uid", B10AbruptVerifyEvidence.Commit));
    }
    private static void AssertValid(B10ScientificThreeJobHarness run)
    {
        Assert.Empty(run.Data["problems"]!.AsArray()); Assert.Empty(run.Data["eligibility_reasons"]!.AsArray());
        Assert.Equal("pending_sweep", run.Data["eligibility"]!.GetValue<string>());
        Assert.False(run.Result["scientific_admissible"]!.GetValue<bool>());
    }
    private static string[] Strings(JsonNode node) => node.AsArray().Select(value => value!.GetValue<string>()).ToArray();
}
#pragma warning restore CA1707

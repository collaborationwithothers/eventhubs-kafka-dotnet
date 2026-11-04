using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Judgement;
using Xunit;

namespace Parchwell.Verifier.Tests.Judgement;

#pragma warning disable CA1707 // Test names follow the repository Given_When_Then convention.

public sealed class B10IntakeChecksTests
{
    private static readonly string[] PublicationCodes = ["b10_intake_only", "publication_first", "publication_second"];
    private const string SourceCommit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly string[] Jobs = ["synthetic-c5-c0-r1-loadgen", "synthetic-c5-c0-r1-loadgen2"];
    private static readonly string[] Instances = ["pod-first", "pod-second"];
    private static readonly OutputLimits Limits = new(262_144, 16, 256);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Given_RegistrationCountAroundTwo_When_Binding_Then_OnlyCoherentPairPasses(int count)
    {
        var registrations = Enumerable.Range(0, count).Select(index => Entry(index % 2)).ToArray();
        if (count == 2) Assert.Equal(Instances, Bind(registrations).Select(r => r.Identity.InstanceId));
        else Assert.Equal("b10_registration_count_invalid", Assert.Throws<InvalidOperationException>(() => Bind(registrations)).Message);
    }

    [Fact]
    public void Given_ReversedDiscoveryOrder_When_Binding_Then_ExpectedJobOrderWins()
    {
        Assert.Equal(Instances, Bind([Entry(1), Entry(0)]).Select(r => r.Identity.InstanceId));
    }

    [Theory]
    [InlineData("duplicate", "b10_registration_identity_duplicate")]
    [InlineData("two-first-jobs", "b10_registration_job_binding_invalid")]
    [InlineData("foreign-job", "b10_registration_job_binding_invalid")]
    [InlineData("swapped-roles", "b10_registration_predecessor_invalid")]
    [InlineData("path", "b10_registration_binding_invalid")]
    [InlineData("expected-instance", "b10_registration_binding_invalid")]
    [InlineData("pod", "b10_registration_binding_invalid")]
    [InlineData("scenario", "b10_registration_binding_invalid")]
    [InlineData("configuration", "b10_registration_binding_invalid")]
    [InlineData("run", "b10_registration_binding_invalid")]
    [InlineData("phase", "b10_registration_binding_invalid")]
    [InlineData("repeat-below", "b10_registration_binding_invalid")]
    [InlineData("repeat-above", "b10_registration_binding_invalid")]
    [InlineData("source", "b10_registration_binding_invalid")]
    [InlineData("first-predecessor", "b10_registration_predecessor_invalid")]
    [InlineData("second-predecessor-absent", "b10_registration_predecessor_invalid")]
    [InlineData("second-predecessor-wrong", "b10_registration_predecessor_invalid")]
    [InlineData("second-predecessor-self", "b10_registration_predecessor_invalid")]
    public void Given_OneRegistrationBindingMismatch_When_Binding_Then_DeterministicCodeIsReturned(string variation, string code)
    {
        var first = Entry(0);
        var second = Entry(1);
        ProducerRegistration value = second.Registration;
        switch (variation)
        {
            case "duplicate": value = value with { Identity = value.Identity with { InstanceId = Instances[0] } }; break;
            case "two-first-jobs": value = value with { JobName = Jobs[0] }; break;
            case "foreign-job": value = value with { JobName = "foreign-job" }; break;
            case "swapped-roles": first.Registration = first.Registration with { JobName = Jobs[1] }; value = value with { JobName = Jobs[0] }; break;
            case "path": second.PathInstance = "foreign-path"; break;
            case "expected-instance": value = value with { Identity = value.Identity with { ExpectedInstanceId = "foreign-instance" } }; break;
            case "pod": value = value with { PodName = "foreign-pod" }; break;
            case "scenario": value = value with { Identity = value.Identity with { ScenarioId = "A01" } }; break;
            case "configuration": value = value with { Identity = value.Identity with { ConfigurationId = "C1" } }; break;
            case "run": value = value with { Identity = value.Identity with { RunId = "foreign-run" } }; break;
            case "phase": value = value with { Phase = "C1" }; break;
            case "repeat-below": value = value with { RepeatNumber = 0 }; break;
            case "repeat-above": value = value with { RepeatNumber = 2 }; break;
            case "source": value = value with { SourceCommit = new string('b', 40) }; break;
            case "first-predecessor": first.Registration = first.Registration with { PredecessorInstanceId = Instances[1] }; break;
            case "second-predecessor-absent": value = value with { PredecessorInstanceId = null }; break;
            case "second-predecessor-wrong": value = value with { PredecessorInstanceId = "foreign-instance" }; break;
            case "second-predecessor-self": value = value with { PredecessorInstanceId = Instances[1] }; break;
            default: throw new ArgumentException("Unknown synthetic variation.", nameof(variation));
        }
        second.Registration = value;
        Assert.Equal(code, Assert.Throws<InvalidOperationException>(() => Bind([first, second])).Message);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)] [InlineData(4)]
    public void Given_InvalidExpectedJobList_When_Binding_Then_JobBindingFails(int variation)
    {
        string[] jobs = variation == 4 ? [Jobs[0], Jobs[0]] : Enumerable.Repeat(Jobs[0], variation).ToArray();
        Assert.Equal("b10_registration_job_binding_invalid", Assert.Throws<InvalidOperationException>(() =>
            B10IntakeChecks.BindRegistrations([Entry(0), Entry(1)], "B10", "C0", "synthetic-c5", "C0", 1, SourceCommit, jobs)).Message);
    }

    [Fact]
    public void Given_MultipleBindingFailures_When_Binding_Then_CountIdentityJobCoordinatesAndPredecessorPrecedenceHolds()
    {
        var first = Entry(0);
        var bad = Entry(1);
        bad.Registration = bad.Registration with { Identity = first.Registration.Identity, JobName = "foreign", Phase = "C1", PredecessorInstanceId = null };
        Assert.Equal("b10_registration_count_invalid", Assert.Throws<InvalidOperationException>(() => Bind([first, bad, Entry(1)])).Message);
        Assert.Equal("b10_registration_identity_duplicate", Assert.Throws<InvalidOperationException>(() => Bind([first, bad])).Message);
        bad.Registration = bad.Registration with { Identity = Entry(1).Registration.Identity };
        Assert.Equal("b10_registration_job_binding_invalid", Assert.Throws<InvalidOperationException>(() => Bind([first, bad])).Message);
        bad.Registration = bad.Registration with { JobName = Jobs[1] };
        Assert.Equal("b10_registration_binding_invalid", Assert.Throws<InvalidOperationException>(() => Bind([first, bad])).Message);
        bad.Registration = bad.Registration with { Phase = "C0" };
        Assert.Equal("b10_registration_predecessor_invalid", Assert.Throws<InvalidOperationException>(() => Bind([first, bad])).Message);
    }

    [Theory]
    [InlineData(null, null, "Incomplete")]
    [InlineData(null, "CompleteValid", "Incomplete")]
    [InlineData("CompleteValid", null, "Incomplete")]
    [InlineData("CompleteValid", "CompleteValid", "Incomplete")]
    [InlineData("Incomplete", "CompleteValid", "Incomplete")]
    [InlineData("CompleteValid", "Incomplete", "Incomplete")]
    [InlineData("Invalid", "Incomplete", "Invalid")]
    [InlineData("Incomplete", "Invalid", "Invalid")]
    [InlineData("Invalid", null, "Invalid")]
    [InlineData(null, "Invalid", "Invalid")]
    [InlineData("Invalid", "Invalid", "Invalid")]
    public void Given_InstanceStatuses_When_Aggregating_Then_InvalidWinsAndCountsAreAlwaysAbsent(
        string? firstStatus, string? secondStatus, string expected)
    {
        RunJudgementResult result = Aggregate(Result() with { Status = firstStatus is null ? null : Enum.Parse<VerifierResultStatus>(firstStatus) }, Result() with { Status = secondStatus is null ? null : Enum.Parse<VerifierResultStatus>(secondStatus) });
        Assert.Equal(Enum.Parse<VerifierResultStatus>(expected), result.Status);
        Assert.Null(result.Counts);
        Assert.Empty(result.FailedChecks);
        Assert.Equal<ResultDiagnostic>([new(0, "b10_intake_only", null, null, null, null)], result.Diagnostics);
    }

    [Theory]
    [InlineData("invalid", "incomplete", "first")]
    [InlineData("incomplete", "invalid", "second")]
    [InlineData("invalid", "invalid", "first")]
    [InlineData("incomplete", "incomplete", "first")]
    [InlineData("unavailable", "invalid", "second")]
    [InlineData("invalid", "unavailable", "first")]
    [InlineData("unavailable", "incomplete", "second")]
    [InlineData("incomplete", "unavailable", "first")]
    [InlineData("pass", "unavailable", "second")]
    [InlineData("unavailable", "pass", "first")]
    [InlineData("unavailable", "unavailable", "first")]
    [InlineData("pass", "pass", "first")]
    [InlineData("pass", "invalid", "second")]
    [InlineData("invalid", "pass", "first")]
    [InlineData("pass", "incomplete", "second")]
    [InlineData("incomplete", "pass", "first")]
    public void Given_AssessmentPrecedence_When_Aggregating_Then_StrongestAssessmentAndFirstTieSurvive(string firstKind, string secondKind, string selected)
    {
        CheckAssessment first = Assessment(6, firstKind) with { Artifact = "first" };
        CheckAssessment second = Assessment(6, secondKind) with { Artifact = "second" };
        RunJudgementResult result = Aggregate(WithAssessment(Result(), first), WithAssessment(Result(), second));
        Assert.Equal(selected == "first" ? first : second, result.Assessments.Single(a => a.CheckNumber == 6));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void Given_MissingRequiredAssessment_When_Aggregating_Then_OtherPassDoesNotMakeItAvailable(bool firstMissing)
    {
        RunJudgementResult missing = Result() with { Assessments = [] };
        RunJudgementResult result = firstMissing ? Aggregate(missing, Result()) : Aggregate(Result(), missing);
        Assert.All(result.Assessments, a => Assert.Equal(AssessmentEvaluation.NotEvaluated, a.Evaluation));
        Assert.Equal(VerifierResultStatus.Incomplete, result.Status);
        Assert.Null(result.Counts);
    }

    [Fact]
    public void Given_BothProducerFailuresAndCommonWindow_When_Aggregating_Then_UnionAttributionAndFirstRootMeasurementsSurvive()
    {
        ResultDiagnostic window = new(10, "check_10_failed", "start-line", null, "timeout", null);
        RunJudgementResult first = Result() with
        {
            FailedChecks = [3, 6, 10],
            Diagnostics = [new(3, "first_identity", "manifest.json", null, "identity", null), window,
                new(6, "first_record", null, 1, "header", null)]
        };
        RunJudgementResult second = Result() with
        {
            FailedChecks = [1, 6],
            Diagnostics = [new(1, "second_ledger", "ledger.jsonl", 2, "attempt", null), window,
                new(6, "second_record", "record", 3, "key", null)],
            BrokerCount = new("LoadGenProducer", "second-client", 99, 99, false),
            ConfigurationFidelity = new(Assessment(27, "pass") with { Artifact = "second" }, [])
        };
        RunJudgementResult result = Aggregate(first, second);
        Assert.Equal([1, 3, 6, 10], result.FailedChecks);
        Assert.Equal<ResultDiagnostic>([
            new(0, "b10_intake_only", null, null, null, null),
            new(3, "first_identity", "producer/pod-first/manifest.json", null, "identity", null), window,
            new(6, "first_record", "producer/pod-first", 1, "header", null),
            new(1, "second_ledger", "producer/pod-second/ledger.jsonl", 2, "attempt", null),
            new(6, "second_record", "producer/pod-second/record", 3, "key", null)
        ], result.Diagnostics);
        Assert.Same(first.BrokerCount, result.BrokerCount);
        Assert.Same(first.ConfigurationFidelity, result.ConfigurationFidelity);
    }

    [Theory]
    [InlineData(6, false)] [InlineData(7, true)] [InlineData(8, true)]
    [InlineData(9, true)] [InlineData(10, true)] [InlineData(11, true)] [InlineData(12, false)]
    public void Given_DiagnosticsAroundWindowCheckRange_When_Aggregating_Then_OnlySharedWindowDiagnosticsAreDeduplicated(int check, bool shared)
    {
        ResultDiagnostic diagnostic = new(check, "synthetic_failure", "evidence", null, null, null);
        RunJudgementResult result = Aggregate(Result() with { Diagnostics = [diagnostic] }, Result() with { Diagnostics = [diagnostic] });
        Assert.Equal(shared ? 2 : 3, result.Diagnostics.Count);
        Assert.Equal(shared ? "evidence" : "producer/pod-first/evidence", result.Diagnostics[1].Artifact);
        if (!shared) Assert.Equal("producer/pod-second/evidence", result.Diagnostics[2].Artifact);
    }

    [Theory]
    [InlineData(1, 1)] [InlineData(2, 2)] [InlineData(3, 2)]
    public void Given_DiagnosticsAroundCombinedCap_When_Aggregating_Then_FailureUnionIsIndependentOfRetention(int count, int retained)
    {
        RunJudgementResult first = Result() with { FailedChecks = [3, 6], Diagnostics = [new(6, "first", "ledger.jsonl", 1, null, "abcde")] };
        RunJudgementResult second = Result() with { FailedChecks = [1, 6], Diagnostics = Enumerable.Range(1, count - 1)
            .Select(n => new ResultDiagnostic(6, $"second_{n}", "ledger.jsonl", n + 1, null, "abcde")).ToArray() };
        RunJudgementResult result = Aggregate(first, second, Limits with { MaxDiagnosticsPerCheck = 2, MaxDiagnosticBytes = 4 });
        Assert.Equal([1, 3, 6], result.FailedChecks);
        Assert.Equal(retained, result.Diagnostics.Count(d => d.Check == 6));
        Assert.All(result.Diagnostics.Where(d => d.Check == 6), d => Assert.Equal("abcd", d.Snippet));
        Assert.Equal("producer/pod-first/ledger.jsonl", result.Diagnostics[1].Artifact);
        if (retained == 2) Assert.Equal("producer/pod-second/ledger.jsonl", result.Diagnostics[2].Artifact);
    }

    [Theory]
    [InlineData(1, 0)] [InlineData(2, 1)] [InlineData(3, 2)]
    public void Given_PublicationDiagnostics_When_Aggregating_Then_IntakeMarkerReservesFirstCheckZeroSlot(int cap, int extra)
    {
        RunJudgementResult first = Result() with { Diagnostics = [new(0, "publication_first", null, null, null, null)] };
        RunJudgementResult second = Result() with { Diagnostics = [new(0, "publication_second", null, null, null, null)] };
        RunJudgementResult result = Aggregate(first, second, Limits with { MaxDiagnosticsPerCheck = cap });
        Assert.Equal(new ResultDiagnostic(0, "b10_intake_only", null, null, null, null), result.Diagnostics[0]);
        Assert.Equal(extra + 1, result.Diagnostics.Count);
        Assert.Equal(PublicationCodes.Take(cap), result.Diagnostics.Select(d => d.Code));
    }

    [Theory]
    [InlineData(1, 0)] [InlineData(2, 1)] [InlineData(3, 1)]
    public void Given_SecondFidelityFailure_When_Aggregating_Then_ProjectionUsesRemainingCheckTwentySevenSlot(int cap, int retained)
    {
        CheckAssessment fidelity = Assessment(27, "invalid") with { Artifact = "capabilities.json", Row = 2, Field = "client.id", Snippet = "abcde" };
        RunJudgementResult first = Result();
        RunJudgementResult second = Result() with { FailedChecks = [27], ConfigurationFidelity = new(fidelity, []) };
        RunJudgementResult result = Aggregate(first, second, Limits with { MaxDiagnosticsPerCheck = cap, MaxDiagnosticBytes = 4 });
        Assert.Equal([27], result.FailedChecks);
        Assert.Same(first.ConfigurationFidelity, result.ConfigurationFidelity);
        Assert.Equal(retained, result.Diagnostics.Count(d => d.Check == 27));
        if (retained == 1) Assert.Contains(new ResultDiagnostic(27, "check_27_failed", "producer/pod-second/capabilities.json", 2, "client.id", "abcd"), result.Diagnostics);
    }

    [Fact]
    public void Given_ExistingFidelityDiagnosticAndSecondFailure_When_Aggregating_Then_FirstDiagnosticUsesRemainingEmbeddedSlot()
    {
        RunJudgementResult first = Result() with { Diagnostics = [new(27, "first_fidelity", "capabilities.json", null, null, null)] };
        RunJudgementResult second = Result() with
        {
            FailedChecks = [27],
            ConfigurationFidelity = new(Assessment(27, "invalid") with { Code = null }, [])
        };
        RunJudgementResult capped = Aggregate(first, second, Limits with { MaxDiagnosticsPerCheck = 2 });
        Assert.Equal([27], capped.FailedChecks);
        Assert.Equal("first_fidelity", Assert.Single(capped.Diagnostics, d => d.Check == 27).Code);
        RunJudgementResult available = Aggregate(first, second, Limits with { MaxDiagnosticsPerCheck = 3 });
        Assert.Equal(new ResultDiagnostic(27, "check_27_failed", "producer/pod-second", null, null, null), available.Diagnostics[^1]);
    }

    [Theory]
    [InlineData(null, null)] [InlineData("", "")] [InlineData("abc", "abc")]
    [InlineData("abcd", "abcd")] [InlineData("abcde", "abcd")]
    [InlineData("\u00e9\u00e9", "\u00e9\u00e9")] [InlineData("\u00e9\u00e9a", "\u00e9\u00e9")]
    [InlineData("a\ud83d\ude00", "a")] [InlineData("\ud83d\ude00a", "\ud83d\ude00")]
    public void Given_Utf8SnippetBoundary_When_Aggregating_Then_OnlyWholeScalarsWithinByteLimitSurvive(string? snippet, string? expected)
    {
        RunJudgementResult second = Result() with { Diagnostics = [new(6, "second", null, null, null, snippet)] };
        Assert.Equal(expected, Aggregate(Result(), second, Limits with { MaxDiagnosticBytes = 4 }).Diagnostics[1].Snippet);
    }

    [Theory]
    [InlineData("pass", "pass", "pass")]
    [InlineData("invalid", "pass", "fail")]
    [InlineData("pass", "invalid", "fail")]
    [InlineData("invalid", "unavailable", "fail")]
    [InlineData("unavailable", "invalid", "fail")]
    [InlineData("unavailable", "pass", "unavailable")]
    [InlineData("pass", "unavailable", "unavailable")]
    [InlineData("unavailable", "unavailable", "unavailable")]
    [InlineData("missing", "pass", "unavailable")]
    [InlineData("pass", "missing", "unavailable")]
    public void Given_PerProducerBrokerCoverage_When_Aggregating_Then_FailureWinsAndBothPassesAreRequired(string first, string second, string expected)
    {
        RunJudgementResult Coverage(string kind) => kind == "missing" ? Result() with { Assessments = [] }
            : WithAssessment(Result(), Assessment(24, kind));
        Assert.Equal(expected, B10IntakeChecks.BrokerInventoryCoverage(Coverage(first), Coverage(second)));
    }

    private static (ProducerRegistration Registration, string PathInstance) Entry(int index) =>
        (new(1, new("B10", "C0", "synthetic-c5", Instances[index], Instances[index], Instances[index]),
            "C0", 1, Jobs[index], Instances[index], $"uid-{index}", SourceCommit, index == 0 ? null : Instances[0]), Instances[index]);

    private static IReadOnlyList<ProducerRegistration> Bind(IReadOnlyList<(ProducerRegistration Registration, string PathInstance)> values) =>
        B10IntakeChecks.BindRegistrations(values, "B10", "C0", "synthetic-c5", "C0", 1, SourceCommit, Jobs);

    private static RunJudgementResult Aggregate(RunJudgementResult first, RunJudgementResult second, OutputLimits? limits = null) =>
        B10IntakeChecks.Aggregate(first, second, Instances, limits ?? Limits);

    private static RunJudgementResult Result() => new(VerifierResultStatus.CompleteValid, [],
        Enumerable.Range(1, 27).Select(check => Assessment(check, "pass")).ToArray(), new(1, 2, 3, 4, 5, 6),
        new("LoadGenProducer", "first-client", 1, 1, true), new(Assessment(27, "pass"), []), []);

    private static RunJudgementResult WithAssessment(RunJudgementResult value, CheckAssessment assessment) =>
        value with { Assessments = value.Assessments.Select(a => a.CheckNumber == assessment.CheckNumber ? assessment : a).ToArray() };

    private static CheckAssessment Assessment(int check, string kind) => kind switch
    {
        "pass" => new(check, AssessmentEvaluation.Evaluated, AssessmentDisposition.Pass, null, null, null, null, null, null),
        "unavailable" => new(check, AssessmentEvaluation.NotEvaluated, null, null, null, null, null, null, null),
        "invalid" or "incomplete" => new(check, AssessmentEvaluation.Evaluated, AssessmentDisposition.Fail,
            kind == "invalid" ? AssessmentClassification.Invalid : AssessmentClassification.Incomplete,
            null, null, null, $"check_{check}_failed", null),
        _ => throw new ArgumentException("Unknown synthetic assessment.", nameof(kind))
    };
}

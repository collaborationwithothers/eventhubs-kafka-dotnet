using System.Text;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Xunit;

namespace Parchwell.Verifier.Tests.Judgement;

#pragma warning disable CA1707 // Test names follow the repository's Given_When_Then convention.
/// <summary>Protects the independent inventory and consumed-record validity gates.</summary>
public sealed class BookkeepingChecksTests
{
    private static readonly string[] IdentityArtifacts = ["index", "manifest", "ledger", "statistics", "lifecycle", "capabilities"];
    [Fact]
    public void Given_MatchingSyntheticEvidence_When_Evaluated_Then_AllBookkeepingChecksPass()
    {
        IReadOnlyList<CheckResult> results = BookkeepingChecks.Evaluate(RecordedEvidence.SyntheticRequest());
        Assert.Equal(6, results.Count);
        foreach (int check in Enumerable.Range(1, 6)) AssertState(results, check, true);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public void Given_MissingOrMalformedBookkeeping_When_Evaluated_Then_OwningCheckFails(int check, bool malformed)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        ReadOnlyMemory<byte>? source = null;
        if (malformed) source = Encoding.UTF8.GetBytes("{\n");
        request = check == 1 ? request with
        {
            Ledger = LedgerReader.ReadRows(source, RecordedEvidence.SmallLimits, new(RecordedEvidence.SmallLimits))
        } : request with
        {
            Manifest = ManifestReader.Read(source, RecordedEvidence.SmallLimits, new(RecordedEvidence.SmallLimits), 16)
        };
        IReadOnlyList<CheckResult> results = BookkeepingChecks.Evaluate(request);
        AssertState(results, check, false);
        AssertState(results, 4, null);
        if (check == 1) AssertState(results, 5, null);
        Assert.Contains(results.Single(r => r.Assessment.CheckNumber == check).Diagnostics,
            diagnostic => diagnostic.Code == (malformed ? "malformed_json" : "missing_input"));
    }

    [Fact]
    public void Given_EmptyPresentLedgerAndInventory_When_Evaluated_Then_ReconciliationAndUniquenessPass()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = request with
        {
            Ledger = LedgerReader.ReadRows(ReadOnlyMemory<byte>.Empty, RecordedEvidence.SmallLimits, new(RecordedEvidence.SmallLimits)),
            Manifest = ParseResult<SealedRunManifest>.Success(request.Manifest.Value! with { AttemptCount = 0, Inventory = [] }),
            Records = []
        };
        IReadOnlyList<CheckResult> results = BookkeepingChecks.Evaluate(request);
        foreach (int check in Enumerable.Range(1, 6)) AssertState(results, check, true);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Given_DeclaredAttemptCountAroundActualCount_When_Evaluated_Then_ReconcilesExactly(long count, bool pass)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = request with { Manifest = ParseResult<SealedRunManifest>.Success(request.Manifest.Value! with { AttemptCount = count }) };
        AssertState(BookkeepingChecks.Evaluate(request), 4, pass);
    }

    [Fact]
    public void Given_TwoAttemptsForOneLogicalEvent_When_Evaluated_Then_PreservesBothAttemptsAndIgnoresPlannedCount()
    {
        RunJudgementRequest request = TwoAttempts();
        request = request with { Manifest = ParseResult<SealedRunManifest>.Success(request.Manifest.Value! with { PlannedAttemptCount = 1 }) };
        IReadOnlyList<CheckResult> results = BookkeepingChecks.Evaluate(request);
        AssertState(results, 4, true);
        AssertState(results, 5, true);
    }

    [Theory]
    [InlineData("missing_failed")]
    [InlineData("duplicate_row")]
    [InlineData("extra_row")]
    [InlineData("missing_inventory")]
    [InlineData("duplicate_inventory")]
    public void Given_InventoryMembershipMismatch_When_Evaluated_Then_ReconciliationFails(string variation)
    {
        RunJudgementRequest request = TwoAttempts();
        LedgerRow first = request.Ledger.Value![0];
        LedgerRow second = request.Ledger.Value[1];
        request = variation switch
        {
            "missing_failed" => request with { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success([first]) },
            "duplicate_row" => request with { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success([first, first, second]) },
            "extra_row" => request with { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success([first, second, RecordedEvidence.LedgerRow(RecordedEvidence.Attempt("a3"))]) },
            "missing_inventory" => request with { Manifest = ParseResult<SealedRunManifest>.Success(request.Manifest.Value! with { Inventory = [first.Attempt] }) },
            "duplicate_inventory" => request with { Manifest = ParseResult<SealedRunManifest>.Success(request.Manifest.Value! with { Inventory = [first.Attempt, first.Attempt] }) },
            _ => throw new ArgumentOutOfRangeException(nameof(variation))
        };
        IReadOnlyList<CheckResult> results = BookkeepingChecks.Evaluate(request);
        AssertState(results, 4, false);
        AssertState(results, 5, variation != "duplicate_row");
        ResultDiagnostic[] expected = variation switch
        {
            "missing_failed" =>
            [
                new(4, "check_4_failed", "ledger.jsonl", null, "attempt_count", null),
                new(4, "check_4_failed", "manifest.json", null, "inventory[1].attempt_id", null)
            ],
            "duplicate_row" =>
            [
                new(4, "check_4_failed", "ledger.jsonl", 2, "attempt.attempt_id", null),
                new(4, "check_4_failed", "ledger.jsonl", null, "attempt_count", null),
                new(4, "check_4_failed", "manifest.json", null, "inventory[0].attempt_id", null)
            ],
            "extra_row" =>
            [
                new(4, "check_4_failed", "ledger.jsonl", null, "attempt_count", null),
                new(4, "check_4_failed", "ledger.jsonl", 3, "attempt.attempt_id", null)
            ],
            "missing_inventory" =>
            [
                new(4, "check_4_failed", "manifest.json", null, "attempt_count", null),
                new(4, "check_4_failed", "ledger.jsonl", 2, "attempt.attempt_id", null)
            ],
            "duplicate_inventory" =>
            [
                new(4, "check_4_failed", "manifest.json", null, "inventory[1].attempt_id", null),
                new(4, "check_4_failed", "ledger.jsonl", 1, "attempt.attempt_id", null),
                new(4, "check_4_failed", "ledger.jsonl", 2, "attempt.attempt_id", null)
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(variation))
        };
        Assert.Equal(expected, results.Single(r => r.Assessment.CheckNumber == 4).Diagnostics);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Given_LedgerSequenceAroundInventorySequence_When_Evaluated_Then_RequiresExactSequence(long sequence, bool pass)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        LedgerRow row = request.Ledger.Value![0];
        request = request with { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success([row with { Attempt = row.Attempt with { Sequence = sequence } }]) };
        AssertState(BookkeepingChecks.Evaluate(request), 4, pass);
        ResultDiagnostic[] expected = pass ? [] : [new(4, "check_4_failed", "ledger.jsonl", 1, "attempt.sequence", null)];
        Assert.Equal(expected, BookkeepingChecks.Evaluate(request).Single(r => r.Assessment.CheckNumber == 4).Diagnostics);
    }

    [Theory]
    [InlineData("k", true)]
    [InlineData("K", false)]
    [InlineData("", false)]
    public void Given_LedgerKeyVariation_When_Evaluated_Then_RequiresOrdinalKeyEquality(string key, bool pass)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        LedgerRow row = request.Ledger.Value![0];
        request = request with { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success([row with { Attempt = row.Attempt with { Key = key } }]) };
        AssertState(BookkeepingChecks.Evaluate(request), 4, pass);
        ResultDiagnostic[] expected = pass ? [] : [new(4, "check_4_failed", "ledger.jsonl", 1, "attempt.key", null)];
        Assert.Equal(expected, BookkeepingChecks.Evaluate(request).Single(r => r.Assessment.CheckNumber == 4).Diagnostics);
    }

    [Fact]
    public void Given_UnrelatedAttemptMetadataDiffers_When_Evaluated_Then_ReconciliationStillPasses()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        LedgerRow row = request.Ledger.Value![0];
        AttemptEntry attempt = row.Attempt with
        {
            Ordinal = 99, TenantId = "other", DocumentId = "other", EventType = "other",
            PlannedMicroseconds = -1, InvokedMicroseconds = 500
        };
        request = request with { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success([row with
        {
            Attempt = attempt, CompletedMicroseconds = -100, PersistenceProvenance = "missing"
        }]) };
        AssertState(BookkeepingChecks.Evaluate(request), 4, true);
    }

    [Theory]
    [InlineData("instance")]
    [InlineData("attempt")]
    [InlineData("duplicate_instance")]
    [InlineData("duplicate_attempt")]
    [InlineData("key_missing")]
    [InlineData("sequence_missing")]
    [InlineData("unknown_attempt")]
    [InlineData("wrong_key")]
    [InlineData("wrong_sequence")]
    [InlineData("ambiguous_ledger")]
    public void Given_UnjoinableConsumedRecord_When_Evaluated_Then_CheckSixFails(string variation)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        ConsumedRecord record = request.Records[0];
        record = variation switch
        {
            "instance" => record with { InstanceId = null },
            "attempt" => record with { AttemptId = null },
            "duplicate_instance" => record with { Problems = [new(6, "duplicate_header", "consumed", 1, "instance_id", null)] },
            "duplicate_attempt" => record with { Problems = [new(6, "duplicate_header", "consumed", 1, "attempt_id", null)] },
            "key_missing" => record with { Key = null },
            "sequence_missing" => record with { Sequence = null },
            "unknown_attempt" => record with { AttemptId = "unknown" },
            "wrong_key" => record with { Key = "K" },
            "wrong_sequence" => record with { Sequence = 2 },
            "ambiguous_ledger" => record,
            _ => throw new ArgumentOutOfRangeException(nameof(variation))
        };
        request = request with { Records = [record] };
        if (variation == "ambiguous_ledger") request = request with
        {
            Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success([request.Ledger.Value![0], request.Ledger.Value[0]])
        };
        AssertState(BookkeepingChecks.Evaluate(request), 6, false);
        ResultDiagnostic[] expected = variation switch
        {
            "instance" => [new(6, "check_6_failed", "consumed", 1, "instance_id", null)],
            "attempt" => [new(6, "check_6_failed", "consumed", 1, "attempt_id", null)],
            "duplicate_instance" => [new(6, "duplicate_header", "consumed", 1, "instance_id", null)],
            "duplicate_attempt" => [new(6, "duplicate_header", "consumed", 1, "attempt_id", null)],
            "key_missing" => [new(6, "check_6_failed", "consumed", 1, "key", null)],
            "sequence_missing" => [new(6, "check_6_failed", "consumed", 1, "sequence", null)],
            "unknown_attempt" => [new(6, "check_6_failed", "consumed", 1, "attempt_id", null)],
            "wrong_key" => [new(6, "check_6_failed", "consumed", 1, "key", null)],
            "wrong_sequence" => [new(6, "check_6_failed", "consumed", 1, "sequence", null)],
            "ambiguous_ledger" => [new(6, "check_6_failed", "consumed", 1, "attempt_id", null)],
            _ => throw new ArgumentOutOfRangeException(nameof(variation))
        };
        Assert.Equal(expected, BookkeepingChecks.Evaluate(request).Single(r => r.Assessment.CheckNumber == 6).Diagnostics);
    }

    [Fact]
    public void Given_IdenticalConsumedRecordRepeated_When_Evaluated_Then_RecordStructureStillPasses()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = request with { Records = [request.Records[0], request.Records[0]] };
        AssertState(BookkeepingChecks.Evaluate(request), 6, true);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Given_ConsumedSequenceAroundLedgerSequence_When_Evaluated_Then_RequiresExactSequence(long sequence, bool pass)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = request with { Records = [request.Records[0] with { Sequence = sequence }] };
        AssertState(BookkeepingChecks.Evaluate(request), 6, pass);
    }

    public static IEnumerable<object[]> IdentityCases() =>
        from artifact in IdentityArtifacts
        from field in Enumerable.Range(0, 6)
        select new object[] { artifact, field };

    [Theory]
    [InlineData("index")]
    [InlineData("manifest")]
    [InlineData("ledger")]
    [InlineData("statistics")]
    [InlineData("lifecycle")]
    [InlineData("capabilities")]
    public void Given_OneIdentityPrerequisiteMissing_When_Evaluated_Then_CheckThreeIsNotEvaluated(string artifact)
    {
        // These synthetic parse failures isolate one prerequisite at a time.
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = artifact switch
        {
            "index" => request with { Index = ParseResult<ArtifactIndex>.Failure(new StructuralProblem(null, "missing_input", "index.json", null, null, null)) },
            "manifest" => request with { Manifest = ParseResult<SealedRunManifest>.Failure(new StructuralProblem(2, "missing_input", "manifest.json", null, null, null)) },
            "ledger" => request with { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Failure(new StructuralProblem(1, "missing_input", "ledger.jsonl", null, null, null)) },
            "statistics" => request with { Statistics = ParseResult<IReadOnlyList<StatisticsEnvelope>>.Failure(new StructuralProblem(17, "missing_input", "statistics.jsonl", null, null, null)) },
            "lifecycle" => request with { Lifecycle = ParseResult<IReadOnlyList<LifecycleEvent>>.Failure(new StructuralProblem(17, "missing_input", "request-lifecycle.jsonl", null, null, null)) },
            "capabilities" => request with { Capabilities = ParseResult<CapturedCapabilities>.Failure(new StructuralProblem(17, "missing_input", "capabilities.json", null, null, null)) },
            _ => throw new ArgumentOutOfRangeException(nameof(artifact))
        };
        IReadOnlyList<CheckResult> results = BookkeepingChecks.Evaluate(request);
        AssertState(results, 3, null);
        Assert.Empty(results.Single(r => r.Assessment.CheckNumber == 3).Diagnostics);
    }

    [Fact]
    public void Given_ConsumedInstanceMissing_When_Evaluated_Then_IdentityIsNotEvaluated()
    {
        // This synthetic consumed record has no instance identity.
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = request with { Records = [request.Records[0] with { InstanceId = null }] };
        IReadOnlyList<CheckResult> results = BookkeepingChecks.Evaluate(request);
        AssertState(results, 3, null);
        Assert.Empty(results.Single(r => r.Assessment.CheckNumber == 3).Diagnostics);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Given_ProblemFreeNullBookkeepingValue_When_Evaluated_Then_StructuralCheckIsNotEvaluated(int check)
    {
        // These synthetic DTOs have neither a parsed value nor a structural problem.
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = check == 1
            ? request with { Ledger = new ParseResult<IReadOnlyList<LedgerRow>>(null, []) }
            : request with { Manifest = new ParseResult<SealedRunManifest>(null, []) };
        IReadOnlyList<CheckResult> results = BookkeepingChecks.Evaluate(request);
        AssertState(results, check, null);
        Assert.Empty(results.Single(r => r.Assessment.CheckNumber == check).Diagnostics);
    }

    [Theory]
    [MemberData(nameof(IdentityCases))]
    public void Given_ArtifactRunIdentityFieldDiffers_When_Evaluated_Then_CheckThreeFails(string artifact, int field)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        RunIdentity identity = AlterIdentity(request.Identity, field);
        request = artifact switch
        {
            "index" => request with { Index = ParseResult<ArtifactIndex>.Success(request.Index.Value! with { Identity = identity }) },
            "manifest" => request with { Manifest = ParseResult<SealedRunManifest>.Success(request.Manifest.Value! with { Identity = identity }) },
            "ledger" => request with { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success([request.Ledger.Value![0] with { Identity = identity }]) },
            "statistics" => request with { Statistics = ParseResult<IReadOnlyList<StatisticsEnvelope>>.Success([request.Statistics.Value![0] with { Identity = identity }]) },
            "lifecycle" => request with { Lifecycle = ParseResult<IReadOnlyList<LifecycleEvent>>.Success([Error(identity)]) },
            "capabilities" => request with { Capabilities = ParseResult<CapturedCapabilities>.Success(request.Capabilities.Value! with { Identity = identity }) },
            _ => throw new ArgumentOutOfRangeException(nameof(artifact))
        };
        AssertState(BookkeepingChecks.Evaluate(request), 3, false);
        (string diagnosticArtifact, long? row) = artifact switch
        {
            "index" => ("index.json", null),
            "manifest" => ("manifest.json", null),
            "ledger" => ("ledger.jsonl", 1),
            "statistics" => ("statistics.jsonl", 1),
            "lifecycle" => ("request-lifecycle.jsonl", 1),
            "capabilities" => ("capabilities.json", null),
            _ => throw new ArgumentOutOfRangeException(nameof(artifact))
        };
        string diagnosticField = field switch
        {
            0 => "identity.scenario_id",
            1 => "identity.configuration_id",
            2 => "identity.run_id",
            3 => "identity.instance_id",
            4 => "identity.expected_instance_id",
            5 => "identity.client_id",
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        ResultDiagnostic[] expected = [new(3, "check_3_failed", diagnosticArtifact, row, diagnosticField, null)];
        Assert.Equal(expected, BookkeepingChecks.Evaluate(request).Single(r => r.Assessment.CheckNumber == 3).Diagnostics);
    }

    [Theory]
    [InlineData("inventory")]
    [InlineData("ledger")]
    [InlineData("consumed")]
    public void Given_AttemptInstanceDiffers_When_Evaluated_Then_CheckThreeFails(string artifact)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        AttemptEntry foreign = request.Manifest.Value!.Inventory[0] with { InstanceId = "INSTANCE-1" };
        request = artifact switch
        {
            "inventory" => request with { Manifest = ParseResult<SealedRunManifest>.Success(request.Manifest.Value with { Inventory = [foreign] }) },
            "ledger" => request with { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success([request.Ledger.Value![0] with { Attempt = foreign }]) },
            "consumed" => request with { Records = [request.Records[0] with { InstanceId = foreign.InstanceId }] },
            _ => throw new ArgumentOutOfRangeException(nameof(artifact))
        };
        AssertState(BookkeepingChecks.Evaluate(request), 3, false);
        ResultDiagnostic[] expected = artifact switch
        {
            "inventory" => [new(3, "check_3_failed", "manifest.json", null, "inventory[0].instance_id", null)],
            "ledger" => [new(3, "check_3_failed", "ledger.jsonl", 1, "attempt.instance_id", null)],
            "consumed" => [new(3, "check_3_failed", "consumed", 1, "instance_id", null)],
            _ => throw new ArgumentOutOfRangeException(nameof(artifact))
        };
        Assert.Equal(expected, BookkeepingChecks.Evaluate(request).Single(r => r.Assessment.CheckNumber == 3).Diagnostics);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Given_OnlyStatisticsRoleOrClientLabelsDiffer_When_Evaluated_Then_CheckThreePasses(bool role, bool client)
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        StatisticsEnvelope envelope = request.Statistics.Value![0];
        envelope = envelope with { StatisticsIdentity = envelope.StatisticsIdentity with
        {
            Role = role ? 99 : envelope.StatisticsIdentity.Role,
            ClientId = client ? "foreign" : envelope.StatisticsIdentity.ClientId
        } };
        request = request with { Statistics = ParseResult<IReadOnlyList<StatisticsEnvelope>>.Success([envelope]) };
        AssertState(BookkeepingChecks.Evaluate(request), 3, true);
    }

    [Fact]
    public void Given_EqualBareAttemptIdsOnDifferentInstances_When_Evaluated_Then_IdentityAndUniquenessFail()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        LedgerRow first = request.Ledger.Value![0];
        request = request with { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success(
            [first, first with { Attempt = first.Attempt with { InstanceId = "instance-2" } }]) };
        IReadOnlyList<CheckResult> results = BookkeepingChecks.Evaluate(request);
        AssertState(results, 3, false);
        AssertState(results, 5, false);
        ResultDiagnostic[] expected = [new(5, "check_5_failed", "ledger.jsonl", 2, "attempt.attempt_id", null)];
        Assert.Equal(expected, results.Single(r => r.Assessment.CheckNumber == 5).Diagnostics);
    }

    [Fact]
    public void Given_AttemptIdsDifferOnlyByCase_When_Evaluated_Then_OrdinalUniquenessPasses()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = request with { Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success(
            [RecordedEvidence.LedgerRow(), RecordedEvidence.LedgerRow(RecordedEvidence.Attempt("A1"))]) };
        AssertState(BookkeepingChecks.Evaluate(request), 5, true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_MissingOrMalformedIndex_When_Read_Then_RetainsExecutionFailureWithoutCheckNumber(bool malformed)
    {
        ReadOnlyMemory<byte>? bytes = null;
        if (malformed) bytes = Encoding.UTF8.GetBytes("{");
        ParseResult<ArtifactIndex> result = ArtifactIndexReader.Read(bytes, RecordedEvidence.SmallLimits, new(RecordedEvidence.SmallLimits));
        Assert.True(result.IsExecutionFailure);
        Assert.Null(result.Value);
        Assert.All(result.Problems, problem => Assert.Null(problem.CheckNumber));
        Assert.Contains(result.Problems, problem => problem.Code == (malformed ? "malformed_json" : "missing_input"));
    }

    [Fact]
    public void Given_WitnessedViolationAndMissingOtherPrerequisite_When_Evaluated_Then_FailureWins()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        request = request with
        {
            Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Failure(new StructuralProblem(1, "missing_input", "ledger.jsonl", null, null, null)),
            Index = ParseResult<ArtifactIndex>.Success(request.Index.Value! with { Identity = request.Identity with { RunId = "foreign" } }),
            Manifest = ParseResult<SealedRunManifest>.Success(request.Manifest.Value! with { AttemptCount = 0 }),
            Records = [request.Records[0] with { Problems = [new(6, "duplicate_header", "consumed", 1, "attempt_id", null)] }]
        };
        IReadOnlyList<CheckResult> results = BookkeepingChecks.Evaluate(request);
        AssertState(results, 3, false);
        AssertState(results, 4, false);
        AssertState(results, 6, false);
    }

    private static RunJudgementRequest TwoAttempts()
    {
        RunJudgementRequest request = RecordedEvidence.SyntheticRequest();
        AttemptEntry second = RecordedEvidence.Attempt("a2");
        return request with
        {
            Manifest = ParseResult<SealedRunManifest>.Success(request.Manifest.Value! with
            {
                AttemptCount = 2, Inventory = [request.Manifest.Value.Inventory[0], second]
            }),
            Ledger = ParseResult<IReadOnlyList<LedgerRow>>.Success([request.Ledger.Value![0], RecordedEvidence.LedgerRow(second, false, "synchronous_rejection")])
        };
    }

    private static RunIdentity AlterIdentity(RunIdentity identity, int field) => field switch
    {
        0 => identity with { ScenarioId = "foreign" },
        1 => identity with { ConfigurationId = "foreign" },
        2 => identity with { RunId = "foreign" },
        3 => identity with { InstanceId = "foreign" },
        4 => identity with { ExpectedInstanceId = "foreign" },
        5 => identity with { ClientId = "foreign" },
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };

    private static NativeError Error(RunIdentity identity) => new(1, identity, "synthetic", 0, "synthetic", 0,
        "synthetic", ParsedField<string>.Null(), false, false, "synthetic");

    private static void AssertState(IReadOnlyList<CheckResult> results, int check, bool? pass)
    {
        CheckAssessment assessment = Assert.Single(results, r => r.Assessment.CheckNumber == check).Assessment;
        Assert.Equal(check, assessment.CheckNumber);
        Assert.Equal(pass is null ? AssessmentEvaluation.NotEvaluated : AssessmentEvaluation.Evaluated, assessment.Evaluation);
        Assert.Equal(pass is null ? null : pass.Value ? AssessmentDisposition.Pass : AssessmentDisposition.Fail, assessment.Disposition);
        Assert.Equal(pass == false ? AssessmentClassification.Invalid : null, assessment.Classification);
    }
}

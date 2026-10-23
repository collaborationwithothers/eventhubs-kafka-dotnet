using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Execution;
using Parchwell.Runner.Tests.Deployment;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class GenerationGuardTests
{
    private static readonly string[] ExpectedDestroyTrace = ["read", "plan:run-a", "read", "apply-saved:run-a", "read"];
    private static readonly DateTimeOffset Started = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly EnvironmentGeneration GenerationA = new("run-a", Started.AddHours(4), "lineage-a", 7);
    private static readonly EnvironmentGeneration GenerationB = new("run-b", Started.AddHours(5), "lineage-a", 8);

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task Given_FourHourGeneration_When_SweepingAroundExpiry_Then_OnlyStrictlyOlderGenerationIsDestroyed(int secondsFromExpiry, bool destroyed)
    {
        var environment = new RecordingEnvironment(GenerationA, GenerationA, null);
        var guard = new GenerationGuard(environment, new FixedClock(GenerationA.ExpiresAt.AddSeconds(secondsFromExpiry)));
        bool result = await guard.DestroyAsync(GenerationA, onlyExpired: true, TestContext.Current.CancellationToken);
        Assert.Equal(destroyed, result);
        Assert.Equal(destroyed ? 1 : 0, environment.Plans.Count);
        Assert.Equal(destroyed ? 1 : 0, environment.Applied.Count);
        Assert.Equal(destroyed ? 3 : 0, environment.ReadCount);
        Assert.Equal(0, environment.OrdinaryApplies);
    }

    [Fact]
    public async Task Given_NormalCleanupBeforeExpiry_When_Destroying_Then_AppliesOnlyTheSavedMatchingPlan()
    {
        var environment = new RecordingEnvironment(GenerationA, GenerationA, null);
        var guard = new GenerationGuard(environment, new FixedClock(Started));
        Assert.True(await guard.DestroyAsync(GenerationA, onlyExpired: false, TestContext.Current.CancellationToken));
        Assert.Equal(ExpectedDestroyTrace, environment.Trace);
        Assert.Same(environment.SavedPlan, Assert.Single(environment.Applied));
        Assert.Equal(GenerationA, environment.Applied[0].Generation);
        Assert.Equal(0, environment.OrdinaryApplies);
    }

    [Theory]
    [InlineData("before-plan")]
    [InlineData("saved-plan")]
    [InlineData("after-plan")]
    public async Task Given_AgedAReplacedByB_When_StateOrSavedPlanChanges_Then_NeverAppliesDestroyForB(string transition)
    {
        var environment = transition == "before-plan"
            ? new RecordingEnvironment(GenerationB)
            : new RecordingEnvironment(GenerationA, transition == "after-plan" ? GenerationB : GenerationA);
        if (transition == "saved-plan") environment.PlannedGeneration = GenerationB;
        var guard = new GenerationGuard(environment, new FixedClock(GenerationA.ExpiresAt.AddSeconds(1)));
        await Assert.ThrowsAsync<InvalidDataException>(() => guard.DestroyAsync(GenerationA, true, TestContext.Current.CancellationToken));
        Assert.Empty(environment.Applied);
        Assert.Equal(transition == "before-plan" ? 0 : 1, environment.Plans.Count);
        Assert.Equal(0, environment.OrdinaryApplies);
    }

    [Theory]
    [InlineData("run")]
    [InlineData("expiry")]
    [InlineData("lineage")]
    [InlineData("serial")]
    public async Task Given_AnyGenerationMemberChanged_When_Rechecked_Then_SavedPlanIsNotApplied(string member)
    {
        EnvironmentGeneration changed = member switch
        {
            "run" => GenerationA with { RunId = "run-b" },
            "expiry" => GenerationA with { ExpiresAt = GenerationA.ExpiresAt.AddSeconds(1) },
            "lineage" => GenerationA with { Lineage = "other-lineage" },
            _ => GenerationA with { Serial = GenerationA.Serial + 1 }
        };
        var environment = new RecordingEnvironment(GenerationA, changed);
        var guard = new GenerationGuard(environment, new FixedClock(Started));
        await Assert.ThrowsAsync<InvalidDataException>(() => guard.DestroyAsync(GenerationA, false, TestContext.Current.CancellationToken));
        Assert.Empty(environment.Applied);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_AlreadyEmptyState_When_CleanupIsRepeated_Then_DoesNotCreateOrApplyAnotherPlan(bool emptyAfterPlanning)
    {
        var environment = emptyAfterPlanning
            ? new RecordingEnvironment(GenerationA, null)
            : new RecordingEnvironment((EnvironmentGeneration?)null);
        var guard = new GenerationGuard(environment, new FixedClock(Started));
        Assert.True(await guard.DestroyAsync(GenerationA, false, TestContext.Current.CancellationToken));
        Assert.Equal(emptyAfterPlanning ? 1 : 0, environment.Plans.Count);
        Assert.Empty(environment.Applied);
    }

    [Fact]
    public async Task Given_NonemptyStateAfterDestroy_When_ConfirmingCleanup_Then_DoesNotClaimDestructionCompleted()
    {
        var environment = new RecordingEnvironment(GenerationA, GenerationA, GenerationB);
        var guard = new GenerationGuard(environment, new FixedClock(Started));
        await Assert.ThrowsAsync<InvalidDataException>(() => guard.DestroyAsync(GenerationA, false, TestContext.Current.CancellationToken));
        Assert.Equal(GenerationA, Assert.Single(environment.Applied).Generation);
        Assert.DoesNotContain(environment.Applied, plan => plan.Generation == GenerationB);
    }

    [Fact]
    public async Task Given_CancellationBeforeCleanup_When_Destroying_Then_DoesNotPlanOrApply()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var environment = new RecordingEnvironment(GenerationA);
        var guard = new GenerationGuard(environment, new FixedClock(Started));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => guard.DestroyAsync(GenerationA, false, cancelled.Token));
        Assert.Empty(environment.Plans);
        Assert.Empty(environment.Applied);
    }

    [Theory]
    [InlineData("run-id", null)]
    [InlineData("run-id", "")]
    [InlineData("expires-at", null)]
    [InlineData("expires-at", "not-a-date")]
    [InlineData("expires-at", "2026-10-22T04:00:00+01:00")]
    [InlineData("repo", "other-repository")]
    [InlineData("layer", "persistent")]
    [InlineData("scenario", "A02")]
    public void Given_MissingOrUntrustedGenerationTags_When_Reading_Then_FailsClosed(string name, string? value)
    {
        var tags = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["repo"] = "eventhubs-kafka-dotnet", ["layer"] = "ephemeral", ["scenario"] = "A01",
            ["run-id"] = "run-a", ["expires-at"] = "2026-10-22T04:00:00Z"
        };
        if (value is null) tags.Remove(name); else tags[name] = value;
        Assert.ThrowsAny<Exception>(() => TerraformEnvironment.ReadTags(JsonSerializer.SerializeToElement(tags), "lineage-a", 7));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_RealTerraformAdapter_When_GenerationChangesImmediatelyBeforeSavedApply_Then_NewGenerationIsNeverDestroyed(bool replaceBeforeApply)
    {
        string output = Path.Combine(Path.GetTempPath(), "generation-plan-" + Guid.NewGuid().ToString("N"));
        var process = new TerraformProcess(replaceBeforeApply);
        var environment = new TerraformEnvironment(process, "/private/ephemeral", "/private/backend.hcl", "/private/runtime.tfvars", output);
        var guard = new GenerationGuard(environment, new FixedClock(Started));
        try
        {
            if (replaceBeforeApply)
                await Assert.ThrowsAsync<InvalidDataException>(() => guard.DestroyAsync(GenerationA, false, TestContext.Current.CancellationToken));
            else Assert.True(await guard.DestroyAsync(GenerationA, false, TestContext.Current.CancellationToken));
            Assert.DoesNotContain(process.Requests, request => request.Arguments.Contains("destroy", StringComparer.Ordinal));
            ProcessRequest plan = Assert.Single(process.Requests, request => request.Arguments[0] == "plan");
            Assert.Contains("-destroy", plan.Arguments);
            Assert.Contains("-lock=true", plan.Arguments);
            ProcessRequest[] applies = process.Requests.Where(request => request.Arguments[0] == "apply").ToArray();
            if (replaceBeforeApply) Assert.Empty(applies);
            else
            {
                ProcessRequest apply = Assert.Single(applies);
                string savedPath = Path.Combine(output, "destroy.tfplan");
                Assert.Equal(new[] { "apply", "-input=false", "-lock=true", savedPath }, apply.Arguments);
                Assert.Contains("-out=" + savedPath, plan.Arguments);
            }
        }
        finally { if (Directory.Exists(output)) Directory.Delete(output, recursive: true); }
    }

    [Theory]
    [InlineData(false, 900)]
    [InlineData(true, 1050)]
    public async Task Given_ConfiguredDestroyBudget_When_GuardedDestroyRuns_Then_PlanAndApplyUseDeclaredTimeout(bool configured, int expectedSeconds)
    {
        string output = Path.Combine(Path.GetTempPath(), "generation-plan-" + Guid.NewGuid().ToString("N"));
        var process = new TerraformProcess(false);
        using JsonDocument limits = JsonDocument.Parse(File.ReadAllText(Path.Combine(DeploymentFixture.RepositoryRoot, "docs/operations/a01-runtime-limits.json")));
        ExecutionBudgets? budgets = configured ? ExecutionBudgets.Read(limits.RootElement) : null;
        var environment = new TerraformEnvironment(process, "/private/ephemeral", "/private/backend.hcl", "/private/runtime.tfvars", output, budgets: budgets);
        var guard = new GenerationGuard(environment, new FixedClock(Started));
        try
        {
            Assert.True(await guard.DestroyAsync(GenerationA, false, TestContext.Current.CancellationToken));
            ProcessRequest plan = Assert.Single(process.Requests, request => request.Arguments[0] == "plan");
            ProcessRequest apply = Assert.Single(process.Requests, request => request.Arguments[0] == "apply");
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), plan.Timeout);
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), apply.Timeout);
            Assert.All(process.Requests.Where(request => request.Arguments[0] is not ("plan" or "apply")),
                request => Assert.Equal(TimeSpan.FromSeconds(60), request.Timeout));
            Assert.Contains("-destroy", plan.Arguments);
            Assert.Contains("-lock=true", plan.Arguments);
            string savedPath = Path.Combine(output, "destroy.tfplan");
            Assert.Equal(new[] { "apply", "-input=false", "-lock=true", savedPath }, apply.Arguments);
            Assert.Contains("-out=" + savedPath, plan.Arguments);
        }
        finally { if (Directory.Exists(output)) Directory.Delete(output, recursive: true); }
    }

    private sealed class TerraformProcess(bool replaceBeforeApply) : IProcessRunner
    {
        private static readonly string[] ExpectedCommands = ["init", "plan", "apply"];
        private int _stateReads;
        internal List<ProcessRequest> Requests { get; } = [];
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            if (request.Arguments[0] == "state")
            {
                _stateReads++;
                EnvironmentGeneration generation = replaceBeforeApply && _stateReads == 5 ? GenerationB : GenerationA;
                string state = JsonSerializer.Serialize(new
                {
                    lineage = generation.Lineage, serial = generation.Serial,
                    resources = _stateReads == 6 ? Array.Empty<object>() : new object[]
                    {
                        new { type = "azurerm_resource_group", instances = new[] { new { attributes = new { tags = Tags(generation) } } } }
                    }
                });
                return Task.FromResult(new ProcessResult(0, state, string.Empty));
            }
            if (request.Arguments[0] == "show")
                return Task.FromResult(new ProcessResult(0, JsonSerializer.Serialize(new
                {
                    prior_state = new { values = new { root_module = new { resources = new[]
                    {
                        new { type = "azurerm_resource_group", values = new { tags = Tags(GenerationA) } }
                    } } } }
                }), string.Empty));
            Assert.Contains(request.Arguments[0], ExpectedCommands);
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
        private static Dictionary<string, string> Tags(EnvironmentGeneration generation) => new(StringComparer.Ordinal)
        {
            ["repo"] = "eventhubs-kafka-dotnet", ["layer"] = "ephemeral", ["scenario"] = "A01",
            ["run-id"] = generation.RunId, ["expires-at"] = generation.ExpiresAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingEnvironment(params EnvironmentGeneration?[] generations) : ITerraformEnvironment
    {
        private readonly Queue<EnvironmentGeneration?> _generations = new(generations);
        internal List<string> Trace { get; } = [];
        internal List<EnvironmentGeneration> Plans { get; } = [];
        internal List<DestroyPlan> Applied { get; } = [];
        internal int OrdinaryApplies { get; private set; }
        internal int ReadCount { get; private set; }
        internal EnvironmentGeneration? PlannedGeneration { get; set; }
        internal DestroyPlan? SavedPlan { get; private set; }
        public Task<EnvironmentGeneration?> ReadGenerationAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCount++;
            Trace.Add("read");
            return Task.FromResult(_generations.Dequeue());
        }
        public Task<DestroyPlan> PlanDestroyAsync(EnvironmentGeneration expected, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Plans.Add(expected);
            Trace.Add("plan:" + expected.RunId);
            SavedPlan = new("/private/destroy-saved.tfplan", PlannedGeneration ?? expected);
            return Task.FromResult(SavedPlan);
        }
        public Task ApplyDestroyPlanAsync(DestroyPlan plan, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Trace.Add("apply-saved:" + plan.Generation.RunId);
            Applied.Add(plan);
            return Task.CompletedTask;
        }
        public Task ApplyAsync(string environmentRunId, DateTimeOffset startedAt, DateTimeOffset expiresAt, CancellationToken cancellationToken)
        {
            OrdinaryApplies++;
            throw new InvalidOperationException("ordinary_apply_not_expected");
        }
        public Task<JsonElement> ReadOutputsAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("output_not_expected");
        public Task PreflightAsync(JsonElement outputs, CancellationToken cancellationToken) => throw new InvalidOperationException("preflight_not_expected");
    }
}
#pragma warning restore CA1707

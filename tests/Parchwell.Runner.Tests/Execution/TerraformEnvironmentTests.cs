using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Execution;
using System.Text.Json;
using Xunit;

#pragma warning disable CA1707

namespace Parchwell.Runner.Tests.Execution;

public sealed class TerraformEnvironmentTests
{
    private static readonly string[] StatePullArguments = ["state", "pull"];
    private static readonly string[] OutputArguments = ["output", "-json"];
    private static readonly string[] ShowArguments = ["show", "-json"];

    [Fact]
    public async Task Given_FreshBackend_When_GenerationIsRead_Then_InitializedSuccessfulEmptyStateIsAbsent()
    {
        var process = new ScriptedProcess(new(0, "initialized", ""), new(0, "", ""));
        var environment = Create(process, Path.GetTempPath());

        Assert.Null(await environment.ReadGenerationAsync(CancellationToken.None));

        Assert.Equal("init", process.Requests[0].Arguments[0]);
        Assert.Equal(StatePullArguments, process.Requests[1].Arguments);
    }

    [Fact]
    public async Task Given_BackendAuthorizationFailure_When_GenerationIsRead_Then_ItIsNeverTreatedAsEmpty()
    {
        var process = new ScriptedProcess(new(0, "initialized", ""), new(1, "", "authorization failed"));

        await Assert.ThrowsAsync<IOException>(() => Create(process, Path.GetTempPath()).ReadGenerationAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Given_NewPhase_When_Applied_Then_PlanUsesTerraformExpiryContractAndApplyUsesSavedPlan()
    {
        string directory = Path.Combine(Path.GetTempPath(), "parchwell-tf-contract-" + Guid.NewGuid().ToString("N"));
        var process = new ScriptedProcess(new(0, "initialized", ""), new(0, "plan", ""), new(0, "apply", ""));
        DateTimeOffset start = new(2026, 10, 22, 1, 2, 3, TimeSpan.Zero);
        try
        {
            await Create(process, directory).ApplyAsync("a01-probe", start, start.AddHours(4), CancellationToken.None);

            ProcessRequest plan = process.Requests[1], apply = process.Requests[2];
            string saved = Path.Combine(directory, "apply.tfplan");
            Assert.Equal("plan", plan.Arguments[0]);
            Assert.Contains("-var=run_id=a01-probe", plan.Arguments);
            Assert.Contains("-var=expires_at=2026-10-22T05:02:03Z", plan.Arguments);
            Assert.Contains("-out=" + saved, plan.Arguments);
            Assert.Equal(new[] { "apply", "-input=false", "-lock=true", saved }, apply.Arguments);
            Assert.Equal(TimeSpan.FromSeconds(900), plan.Timeout);
            Assert.Equal(TimeSpan.FromSeconds(900), apply.Timeout);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static TerraformEnvironment Create(IProcessRunner process, string output) =>
        new(process, Path.Combine(Path.GetTempPath(), "infra", "ephemeral"), "private.tfbackend", "private.tfvars", output);

    [Fact]
    public async Task Given_AppliedEnvironment_When_OutputsAreRead_Then_OriginalStateIsCapturedPrivatelyWithinExistingApiBound()
    {
        string directory = Path.Combine(Path.GetTempPath(), "parchwell-state-capture-" + Guid.NewGuid().ToString("N"));
        const string originalState = "{\n  \"format_version\":\"1.0\", \"values\": {\"root_module\":{}}\n}\n";
        const string originalOutputs = "{\n  \"aks\":{\"value\":{\"id\":\"synthetic-cluster\"}}\n}\n";
        var process = new ScriptedProcess(new(0, originalOutputs, ""), new(0, originalState, ""));
        try
        {
            JsonElement outputs = await Create(process, directory).ReadOutputsAsync(TestContext.Current.CancellationToken);

            Assert.Equal("synthetic-cluster", outputs.GetProperty("aks").GetProperty("value").GetProperty("id").GetString());
            Assert.Equal(originalOutputs, await File.ReadAllTextAsync(Path.Combine(directory, "outputs.private.json"), TestContext.Current.CancellationToken));
            Assert.Equal(originalState, await File.ReadAllTextAsync(Path.Combine(directory, "state.private.json"), TestContext.Current.CancellationToken));
            Assert.Equal(OutputArguments, process.Requests[0].Arguments);
            Assert.Equal(ShowArguments, process.Requests[1].Arguments);
            Assert.All(process.Requests, request => Assert.Equal(TimeSpan.FromSeconds(60), request.Timeout));
            Assert.All(process.Requests, request => Assert.Equal(16777216, request.MaxOutputBytes));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Given_StateReadbackFails_When_OutputsAreRead_Then_IncompleteScopeCannotBeReturned()
    {
        string directory = Path.Combine(Path.GetTempPath(), "parchwell-state-failure-" + Guid.NewGuid().ToString("N"));
        const string originalOutputs = "{\n  \"synthetic\":true\n}\n";
        var process = new ScriptedProcess(new(0, originalOutputs, ""), new(1, "", "synthetic-state-failure"));
        try
        {
            await Assert.ThrowsAsync<IOException>(() => Create(process, directory).ReadOutputsAsync(TestContext.Current.CancellationToken));
            Assert.Equal(originalOutputs, await File.ReadAllTextAsync(Path.Combine(directory, "outputs.private.json"), TestContext.Current.CancellationToken));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("2026-10-22T05:02:03Z", true)]
    [InlineData("2026-10-22T05:02:03.0000000+00:00", true)]
    [InlineData("2026-10-22T05:02:03", false)]
    [InlineData("2026-10-22T05:02:03+01:00", false)]
    public void Given_ExpiryTag_When_GenerationIsRead_Then_ExplicitUtcIsRequired(string expiry, bool valid)
    {
        JsonElement tags = JsonSerializer.SerializeToElement(new Dictionary<string, string>
        {
            ["repo"] = "eventhubs-kafka-dotnet", ["layer"] = "ephemeral", ["scenario"] = "A01",
            ["run-id"] = "a01-probe", ["expires-at"] = expiry
        });
        if (valid) Assert.Equal("a01-probe", TerraformEnvironment.ReadTags(tags, "lineage", 1).RunId);
        else Assert.Throws<InvalidDataException>(() => TerraformEnvironment.ReadTags(tags, "lineage", 1));
    }

    private sealed class ScriptedProcess(params ProcessResult[] results) : IProcessRunner
    {
        private readonly Queue<ProcessResult> remaining = new(results);
        public List<ProcessRequest> Requests { get; } = [];

        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(remaining.Dequeue());
        }
    }
}

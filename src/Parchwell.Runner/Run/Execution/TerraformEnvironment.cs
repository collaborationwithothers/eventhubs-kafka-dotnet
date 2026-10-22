using System.Globalization;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;

namespace Parchwell.Runner.Run.Execution;

public sealed class TerraformEnvironment(IProcessRunner process, string root, string backendConfig,
    string tfvars, string outputDirectory, TimeProvider? timeProvider = null) : ITerraformEnvironment
{
    private bool initialized;
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<EnvironmentGeneration?> ReadGenerationAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        ProcessResult result = await RunAsync(["state", "pull"], 60, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(result.StandardOutput)) return null;
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        return ParseGeneration(document.RootElement);
    }

    private static EnvironmentGeneration? ParseGeneration(JsonElement state)
    {
        string lineage = WorkerIdentity.Text(state, "lineage");
        long serial = state.GetProperty("serial").GetInt64();
        List<JsonElement> groups = [];
        if (state.TryGetProperty("resources", out JsonElement resources))
            foreach (JsonElement resource in resources.EnumerateArray())
                if (resource.GetProperty("type").GetString() == "azurerm_resource_group")
                    foreach (JsonElement instance in resource.GetProperty("instances").EnumerateArray()) groups.Add(instance.GetProperty("attributes"));
        if (groups.Count == 0)
        {
            if (state.TryGetProperty("resources", out resources) && resources.GetArrayLength() > 0)
                throw new InvalidDataException("state_without_generation");
            return null;
        }
        if (groups.Count != 1) throw new InvalidDataException("ambiguous_environment_generation");
        return ReadTags(groups[0].GetProperty("tags"), lineage, serial);
    }

    public async Task<JsonElement> CaptureRecoverySnapshotAsync(EnvironmentGeneration expected, string directory, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        ProcessResult state = await RunAsync(["state", "pull"], 60, cancellationToken).ConfigureAwait(false);
        using JsonDocument stateDocument = JsonDocument.Parse(state.StandardOutput);
        GenerationGuard.EnsureSame(expected, ParseGeneration(stateDocument.RootElement) ?? throw new InvalidDataException("recovery_state_empty"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "state.private.json"), state.StandardOutput, cancellationToken).ConfigureAwait(false);
        ProcessResult outputs = await RunAsync(["output", "-json"], 60, cancellationToken).ConfigureAwait(false);
        using JsonDocument outputDocument = JsonDocument.Parse(outputs.StandardOutput);
        GenerationGuard.EnsureSame(expected, await ReadGenerationAsync(cancellationToken).ConfigureAwait(false) ?? throw new InvalidDataException("recovery_generation_disappeared"));
        await File.WriteAllTextAsync(Path.Combine(directory, "outputs.private.json"), outputs.StandardOutput, cancellationToken).ConfigureAwait(false);
        return outputDocument.RootElement.Clone();
    }

    public async Task ApplyAsync(string environmentRunId, DateTimeOffset startedAt, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        if (expiresAt != startedAt.AddHours(4)) throw new InvalidDataException("expiry_mismatch");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(900), clock);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        await InitializeAsync(operation.Token).ConfigureAwait(false);
        Directory.CreateDirectory(outputDirectory);
        string plan = Path.GetFullPath(Path.Combine(outputDirectory, "apply.tfplan"));
        await RunAsync(["plan", "-input=false", "-lock=true", "-var-file=" + tfvars,
            "-var=run_id=" + environmentRunId, "-var=expires_at=" + TerraformTimestamp(expiresAt), "-out=" + plan], 900, operation.Token).ConfigureAwait(false);
        await RunAsync(["apply", "-input=false", "-lock=true", plan], 900, operation.Token).ConfigureAwait(false);
    }

    public async Task<JsonElement> ReadOutputsAsync(CancellationToken cancellationToken)
    {
        ProcessResult result = await RunAsync(["output", "-json"], 60, cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(outputDirectory);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "outputs.private.json"), result.StandardOutput, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        // The pinned AKS module exports the independently assigned node resource group in state.
        ProcessResult state = await RunAsync(["show", "-json"], 60, cancellationToken).ConfigureAwait(false);
        using JsonDocument stateDocument = JsonDocument.Parse(state.StandardOutput);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "state.private.json"), state.StandardOutput, cancellationToken).ConfigureAwait(false);
        return document.RootElement.Clone();
    }

    public Task PreflightAsync(JsonElement outputs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (outputs.ValueKind != JsonValueKind.Object) throw new InvalidDataException("terraform_outputs_missing");
        return Task.CompletedTask;
    }

    public async Task<DestroyPlan> PlanDestroyAsync(EnvironmentGeneration expected, CancellationToken cancellationToken)
    {
        EnvironmentGeneration? before = await ReadGenerationAsync(cancellationToken).ConfigureAwait(false);
        GenerationGuard.EnsureSame(expected, before ?? throw new InvalidDataException("generation_absent_before_plan"));
        Directory.CreateDirectory(outputDirectory);
        string path = Path.GetFullPath(Path.Combine(outputDirectory, "destroy.tfplan"));
        await RunAsync(["plan", "-destroy", "-input=false", "-lock=true", "-var-file=" + tfvars,
            "-var=run_id=" + expected.RunId, "-var=expires_at=" + TerraformTimestamp(expected.ExpiresAt), "-out=" + path], 900, cancellationToken).ConfigureAwait(false);
        ProcessResult show = await RunAsync(["show", "-json", path], 60, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(show.StandardOutput);
        List<JsonElement> groups = [];
        FindGroupValues(document.RootElement.GetProperty("prior_state").GetProperty("values").GetProperty("root_module"), groups);
        if (groups.Count != 1) throw new InvalidDataException("destroy_plan_generation_missing");
        EnvironmentGeneration planned = ReadTags(groups[0].GetProperty("tags"), expected.Lineage, expected.Serial);
        GenerationGuard.EnsureSame(expected, planned);
        GenerationGuard.EnsureSame(expected, await ReadGenerationAsync(cancellationToken).ConfigureAwait(false) ?? throw new InvalidDataException("generation_absent_after_plan"));
        return new(path, planned);
    }

    public async Task ApplyDestroyPlanAsync(DestroyPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        GenerationGuard.EnsureSame(plan.Generation, await ReadGenerationAsync(cancellationToken).ConfigureAwait(false) ?? throw new InvalidDataException("generation_absent_before_apply"));
        await RunAsync(["apply", "-input=false", "-lock=true", plan.Path], 900, cancellationToken).ConfigureAwait(false);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (initialized) return;
        await RunAsync(["init", "-input=false", "-backend-config=" + backendConfig], 60, cancellationToken).ConfigureAwait(false);
        initialized = true;
    }

    private static string TerraformTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private async Task<ProcessResult> RunAsync(IReadOnlyList<string> arguments, int seconds, CancellationToken cancellationToken)
    {
        if (Path.GetFileName(Path.TrimEndingDirectorySeparator(root)) != "ephemeral") throw new InvalidOperationException("ephemeral_root_only");
        ProcessResult result = await process.RunAsync(new("terraform", arguments, TimeSpan.FromSeconds(seconds), WorkingDirectory: root), cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0 || result.TimedOut) throw new IOException("terraform_command_failed");
        return result;
    }

    public static EnvironmentGeneration ReadTags(JsonElement tags, string lineage, long serial)
    {
        if (WorkerIdentity.Text(tags, "repo") != "eventhubs-kafka-dotnet" || WorkerIdentity.Text(tags, "layer") != "ephemeral" || WorkerIdentity.Text(tags, "scenario") != "A01")
            throw new InvalidDataException("generation_tags");
        string expiryText = WorkerIdentity.Text(tags, "expires-at");
        if ((!expiryText.EndsWith('Z') && !expiryText.EndsWith("+00:00", StringComparison.Ordinal)) ||
            !DateTimeOffset.TryParse(expiryText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset expiry) || expiry.Offset != TimeSpan.Zero)
            throw new InvalidDataException("generation_expiry");
        return new(WorkerIdentity.Text(tags, "run-id"), expiry, lineage, serial);
    }

    private static void FindGroupValues(JsonElement module, List<JsonElement> groups)
    {
        if (module.TryGetProperty("resources", out JsonElement resources))
            foreach (JsonElement resource in resources.EnumerateArray())
                if (resource.GetProperty("type").GetString() == "azurerm_resource_group") groups.Add(resource.GetProperty("values"));
        if (module.TryGetProperty("child_modules", out JsonElement children))
            foreach (JsonElement child in children.EnumerateArray()) FindGroupValues(child, groups);
    }
}

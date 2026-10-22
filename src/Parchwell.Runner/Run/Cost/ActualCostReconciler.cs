using System.Text.Json;
using Parchwell.Runner.Run.Evidence;

namespace Parchwell.Runner.Run.Cost;

/// <summary>Settled cost scopes come independently from verified original Terraform output and state.</summary>
public sealed class ActualCostReconciler(RunLedger ledger, EvidenceExporter exporter)
{
    public async Task<LedgerDocument> ReconcileAsync(string phase, string recordFile, CancellationToken cancellationToken = default)
    {
        if (new FileInfo(recordFile).Length > 1_048_576) throw new InvalidDataException("actual_cost_record_limit");
        byte[] recordBytes = await ReadBoundedRecordAsync(recordFile, cancellationToken).ConfigureAwait(false);
        var actual = JsonSerializer.Deserialize<ActualCostRecord>(recordBytes, LedgerJson.Options)
            ?? throw new InvalidDataException("actual_cost_record_missing");
        var document = await ledger.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (!document.Phases.TryGetValue(phase, out var selected) || selected.EnvironmentRunId != actual.EnvironmentRunId || selected.PrivateExport?.Complete != true)
            throw new InvalidDataException("actual_cost_phase_scope_unavailable");
        var original = await exporter.ReadVerifiedFileAsync(actual.EnvironmentRunId, "terraform-outputs.json", selected.PrivateExport, cancellationToken).ConfigureAwait(false);
        using var outputs = JsonDocument.Parse(original);
        var stateBytes = await exporter.ReadVerifiedFileAsync(actual.EnvironmentRunId, "terraform/state.private.json", selected.PrivateExport, cancellationToken).ConfigureAwait(false);
        using var state = JsonDocument.Parse(stateBytes);
        (string scope, string nodeScope) = ReadScopes(outputs.RootElement, state.RootElement, actual.EnvironmentRunId);
        // Retain the received formatting and bytes before committing normalized cost fields.
        await exporter.UploadActualCostAsync(actual.EnvironmentRunId, recordBytes, cancellationToken).ConfigureAwait(false);
        return await ledger.ReconcileAsync(phase, actual, scope, nodeScope, cancellationToken).ConfigureAwait(false);
    }

    private static (string Scope, string NodeScope) ReadScopes(JsonElement outputs, JsonElement state, string environmentRunId)
    {
        var aks = outputs.GetProperty("aks");
        if (aks.TryGetProperty("value", out var wrapped)) aks = wrapped;
        var clusterId = aks.GetProperty("id").GetString() ?? throw new InvalidDataException("actual_cost_phase_scope_unavailable");
        var segments = clusterId.Split('/');
        if (segments.Length != 9 || segments[0].Length != 0 || !segments[1].Equals("subscriptions", StringComparison.OrdinalIgnoreCase) || !Guid.TryParse(segments[2], out _) ||
            !segments[3].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(segments[4]) ||
            !segments[5].Equals("providers", StringComparison.OrdinalIgnoreCase) || !segments[6].Equals("Microsoft.ContainerService", StringComparison.OrdinalIgnoreCase) ||
            !segments[7].Equals("managedClusters", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(segments[8]))
            throw new InvalidDataException("actual_cost_phase_scope_unavailable");
        string scope = string.Join('/', segments.Take(5));
        if (state.GetProperty("format_version").GetString() != "1.0") throw new InvalidDataException("actual_cost_state_format_unsupported");
        JsonElement values = state.GetProperty("values");
        if (!string.Equals(values.GetProperty("outputs").GetProperty("aks").GetProperty("value").GetProperty("id").GetString(), clusterId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("actual_cost_state_cluster_mismatch");
        var clusters = new List<JsonElement>();
        var groups = new List<JsonElement>();
        FindResources(values.GetProperty("root_module"), clusters, groups);
        if (clusters.Count != 1 || groups.Count != 1 ||
            !string.Equals(groups[0].GetProperty("id").GetString(), scope, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(clusters[0].GetProperty("id").GetString(), clusterId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("actual_cost_state_scope_mismatch");
        JsonElement tags = groups[0].GetProperty("tags");
        if (tags.GetProperty("run-id").GetString() != environmentRunId || tags.GetProperty("repo").GetString() != "eventhubs-kafka-dotnet" ||
            tags.GetProperty("layer").GetString() != "ephemeral" || tags.GetProperty("scenario").GetString() != "A01")
            throw new InvalidDataException("actual_cost_state_generation_mismatch");
        string nodeGroup = clusters[0].GetProperty("output").GetProperty("properties").GetProperty("nodeResourceGroup").GetString()
            ?? throw new InvalidDataException("actual_cost_node_scope_unavailable");
        if (string.IsNullOrWhiteSpace(nodeGroup) || nodeGroup.Contains('/', StringComparison.Ordinal) || nodeGroup.Equals(segments[4], StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("actual_cost_node_scope_unavailable");
        return (scope, string.Join('/', segments.Take(4)) + "/" + nodeGroup);
    }

    private static void FindResources(JsonElement module, List<JsonElement> clusters, List<JsonElement> groups)
    {
        if (module.TryGetProperty("resources", out JsonElement resources))
            foreach (JsonElement resource in resources.EnumerateArray())
            {
                if (resource.GetProperty("mode").GetString() != "managed") continue;
                JsonElement value = resource.GetProperty("values");
                if (resource.GetProperty("type").GetString() == "azurerm_resource_group") groups.Add(value);
                if (resource.GetProperty("type").GetString() == "azapi_resource" && value.TryGetProperty("type", out JsonElement type) &&
                    type.GetString()?.StartsWith("Microsoft.ContainerService/managedClusters@", StringComparison.OrdinalIgnoreCase) == true) clusters.Add(value);
            }
        if (module.TryGetProperty("child_modules", out JsonElement children))
            foreach (JsonElement child in children.EnumerateArray()) FindResources(child, clusters, groups);
    }

    private static async Task<byte[]> ReadBoundedRecordAsync(string path, CancellationToken cancellationToken)
    {
        const int limit = 1_048_576;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        if (stream.Length > limit) throw new InvalidDataException("actual_cost_record_limit");
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (output.Length + read > limit) throw new InvalidDataException("actual_cost_record_limit");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
}

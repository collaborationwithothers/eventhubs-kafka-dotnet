using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Publication;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.Run.Restart;

/// <summary>Finds and reads the published evidence of a B10 repeat's first load generator instance (ADR-021).</summary>
public static class PredecessorEvidenceLoader
{
    private const int MaximumLifecycleRecordBytes = 65536;
    private static readonly string[] PublicationNames =
        ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json", "index.json"];
    private static readonly string[] RegistrationNames =
        ["schema_version", "identity", "phase", "repeat_number", "job_name", "pod_name", "pod_uid", "source_commit"];
    private static readonly string[] CompletionNames = [.. RegistrationNames, "mode", "handler_exit_code", "cleanup_completed"];
    private static readonly string[] IdentityNames =
        ["scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id"];

    /// <summary>Requires exactly one earlier instance in the repeat, with valid lifecycle records and publication, and returns its manifest and rows.</summary>
    public static async Task<RunArtifactSet> LoadAsync(IPredecessorEvidenceSource source, ProducerRunContext successor,
        RunEvidenceExpectations expectations, long maximumArtifactBytes, long maximumAggregateArtifactBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(successor);
        ArgumentNullException.ThrowIfNull(expectations);
        if (!successor.IsRestartInstance) throw new InvalidDataException("predecessor_requires_restart_instance");
        string producerPrefix = successor.RepeatPrefix + "producer/";
        string ownSegment = Uri.EscapeDataString(successor.PodName);
        var members = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (string name in await source.ListAsync(producerPrefix, cancellationToken).ConfigureAwait(false))
        {
            int slash = name.StartsWith(producerPrefix, StringComparison.Ordinal) ? name.IndexOf('/', producerPrefix.Length) : -1;
            if (slash <= producerPrefix.Length) throw new InvalidDataException("predecessor_listing_invalid");
            string segment = name[producerPrefix.Length..slash];
            if (StringComparer.Ordinal.Equals(segment, ownSegment))
                throw new InvalidDataException("predecessor_successor_already_registered");
            if (!members.TryGetValue(segment, out HashSet<string>? set)) members[segment] = set = new HashSet<string>(StringComparer.Ordinal);
            set.Add(name[(slash + 1)..]);
        }
        if (members.Count != 1) throw new InvalidDataException("predecessor_instance_count_invalid");
        (string segmentName, HashSet<string> instanceMembers) = members.Single();
        string podName = Uri.UnescapeDataString(segmentName);
        if (!StringComparer.Ordinal.Equals(Uri.EscapeDataString(podName), segmentName))
            throw new InvalidDataException("predecessor_listing_invalid");
        if (!instanceMembers.SetEquals(["registration.json", "completion.json", .. PublicationNames.Select(name => "publication/" + name)]))
            throw new InvalidDataException("predecessor_member_set_invalid");

        var identity = new RunIdentity(successor.Identity.ScenarioId, successor.Identity.ConfigurationId,
            successor.Identity.RunId, podName, podName, podName);
        string instancePrefix = producerPrefix + segmentName + "/";
        using JsonDocument registration = await ReadRecordAsync(source, instancePrefix + "registration.json", RegistrationNames,
            "predecessor_registration_invalid", cancellationToken).ConfigureAwait(false);
        RequireLifecycleRecord(registration.RootElement, identity, successor, "predecessor_registration_invalid");
        using JsonDocument completion = await ReadRecordAsync(source, instancePrefix + "completion.json", CompletionNames,
            "predecessor_completion_invalid", cancellationToken).ConfigureAwait(false);
        JsonElement record = completion.RootElement;
        RequireLifecycleRecord(record, identity, successor, "predecessor_completion_invalid");
        if (!StringComparer.Ordinal.Equals(Text(record, "pod_uid"), Text(registration.RootElement, "pod_uid")) ||
            !StringComparer.Ordinal.Equals(Text(record, "mode"), "loadgen") || !IsInt32(record, "handler_exit_code", 0) ||
            record.GetProperty("cleanup_completed").ValueKind != JsonValueKind.True)
            throw new InvalidDataException("predecessor_completion_invalid");

        string publicationPrefix = instancePrefix + "publication/";
        async ValueTask<Stream> OpenAsync(string name, CancellationToken token) =>
            new MemoryStream(await source.ReadAsync(publicationPrefix + name, maximumArtifactBytes, token).ConfigureAwait(false), writable: false);
        RunArtifactIndex index;
        await using (Stream indexStream = await OpenAsync("index.json", cancellationToken).ConfigureAwait(false))
            index = await RunArtifactIndexReader.ReadAsync(indexStream, identity, maximumArtifactBytes, maximumArtifactBytes,
                maximumAggregateArtifactBytes, cancellationToken).ConfigureAwait(false);
        RunEvidenceExpectations predecessorExpectations = ForPredecessor(expectations, identity.ClientId);
        // The completion record checked above is the first Job's evidence that its worker exited with 0.
        return await RunArtifactIndexReader.ReadArtifactSetAsync(index, 0, predecessorExpectations, PublicationNames, OpenAsync,
            maximumArtifactBytes, maximumAggregateArtifactBytes, cancellationToken).ConfigureAwait(false);
    }

    private static RunEvidenceExpectations ForPredecessor(RunEvidenceExpectations expectations, string predecessorClientId)
    {
        var services = new ServiceCollection();
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, options =>
        {
            options.NamespaceName = "reader-validation";
            options.ClientId = predecessorClientId;
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        KafkaSettingMetadata clientMetadata = provider.GetRequiredService<KafkaConfigurationFactory>()
            .CreateLoadGenProducer().Settings.Single(setting => StringComparer.Ordinal.Equals(setting.Key, "client.id"));

        ResolvedRoleConfiguration selected = expectations.ResolvedConfiguration;
        ResolvedSetting selectedClient = selected.Settings["client.id"];
        ResolvedRoleConfiguration predecessorRole = selected with
        {
            Settings = selected.Settings.SetItem("client.id", selectedClient with
            {
                Value = predecessorClientId,
                Baseline = clientMetadata
            })
        };

        return expectations.RequiresIndependentBrokerCoverage
            ? new RunEvidenceExpectations(predecessorRole, expectations.RequireStatisticsEvidence, expectations.ExpectedBrokerNames)
            : RunEvidenceExpectations.CaptureOnly(predecessorRole, expectations.RequireStatisticsEvidence);
    }

    private static async Task<JsonDocument> ReadRecordAsync(IPredecessorEvidenceSource source, string name, string[] names,
        string error, CancellationToken cancellationToken)
    {
        byte[] bytes = await source.ReadAsync(name, MaximumLifecycleRecordBytes, cancellationToken).ConfigureAwait(false);
        JsonDocument document;
        try { document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 }); }
        catch (JsonException) { throw new InvalidDataException(error); }
        if (HasExactProperties(document.RootElement, names) &&
            HasExactProperties(document.RootElement.GetProperty("identity"), IdentityNames))
            return document;
        document.Dispose();
        throw new InvalidDataException(error);
    }

    // Both lifecycle records must describe the first Job of this repeat, built from the second instance's source commit.
    private static void RequireLifecycleRecord(JsonElement record, RunIdentity identity, ProducerRunContext successor, string error)
    {
        JsonElement recorded = record.GetProperty("identity");
        string? podUid = Text(record, "pod_uid");
        (JsonElement Element, string Name, string Expected)[] texts =
        [
            (recorded, "scenario_id", identity.ScenarioId), (recorded, "configuration_id", identity.ConfigurationId),
            (recorded, "run_id", identity.RunId), (recorded, "instance_id", identity.InstanceId),
            (recorded, "expected_instance_id", identity.ExpectedInstanceId), (recorded, "client_id", identity.ClientId),
            (record, "phase", successor.Phase), (record, "job_name", successor.FirstJobName),
            (record, "pod_name", identity.InstanceId), (record, "source_commit", successor.SourceCommit)
        ];
        if (!IsInt32(record, "schema_version", 1) || !IsInt32(record, "repeat_number", successor.RepeatNumber) ||
            texts.Any(text => !StringComparer.Ordinal.Equals(Text(text.Element, text.Name), text.Expected)) ||
            string.IsNullOrEmpty(podUid) || podUid.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw new InvalidDataException(error);
    }

    private static bool HasExactProperties(JsonElement element, string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
            if (!names.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name)) return false;
        return seen.Count == names.Length;
    }

    private static string? Text(JsonElement element, string name) =>
        element.GetProperty(name).ValueKind == JsonValueKind.String ? element.GetProperty(name).GetString() : null;

    private static bool IsInt32(JsonElement element, string name, int expected) =>
        element.GetProperty(name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out int actual) && actual == expected;
}

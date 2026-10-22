using System.Security.Cryptography;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;

namespace Parchwell.Runner.Run.Execution;

public static class PublicationAcceptance
{
    private static readonly string[] Required = ["ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json", "index.json"];

    public static void Validate(ProducerPublication publication, WorkerIdentity expected)
    {
        ArgumentNullException.ThrowIfNull(publication);
        if (publication.Objects.Count != Required.Length || Required.Any(name => !publication.Objects.ContainsKey(name)))
            throw new InvalidDataException("publication_object_set");
        if (publication.Objects.Values.Sum(value => (long)value.Length) > 1073741824 || publication.Objects.Values.Any(value => value.Length > 268435456))
            throw new InvalidDataException("publication_limit");
        using JsonDocument indexDocument = JsonDocument.Parse(publication.Objects["index.json"]);
        JsonElement index = indexDocument.RootElement;
        if (index.GetProperty("schema_version").GetInt32() != 1 || WorkerIdentity.Read(index.GetProperty("identity")) != expected ||
            WorkerIdentity.Text(index, "worker_status") != "complete" || !index.GetProperty("bookkeeping_complete").GetBoolean() ||
            !index.GetProperty("worker_complete").GetBoolean() || !index.GetProperty("publication_complete").GetBoolean())
            throw new InvalidDataException("publication_index_incomplete");
        JsonElement artifacts = index.GetProperty("artifacts");
        if (artifacts.GetArrayLength() != 5) throw new InvalidDataException("publication_index_set");
        int ordinal = 0;
        foreach (JsonElement descriptor in artifacts.EnumerateArray())
        {
            string name = WorkerIdentity.Text(descriptor, "name");
            if (name != Required[ordinal++]) throw new InvalidDataException("publication_index_order");
            byte[] bytes = publication.Objects[name];
            if (descriptor.GetProperty("length").GetInt64() != bytes.Length || WorkerIdentity.Text(descriptor, "sha256") != Convert.ToHexStringLower(SHA256.HashData(bytes)))
                throw new InvalidDataException("publication_hash");
        }
        using JsonDocument manifest = JsonDocument.Parse(publication.Objects["manifest.json"]);
        if (WorkerIdentity.Read(manifest.RootElement.GetProperty("identity")) != expected)
            throw new InvalidDataException("publication_manifest_identity");
    }
}

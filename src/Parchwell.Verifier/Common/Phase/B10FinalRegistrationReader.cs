using System.Text;
using System.Text.Json;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;

namespace Parchwell.Verifier.Common.Phase;

/// <summary>Reads scientific producer registrations without crossing the VerifyRun slice boundary.</summary>
internal static class B10FinalRegistrationReader
{
    internal static async Task<B10FinalRegistrations> ReadAsync(VerifierHostOptions options, int repeat, InputBudget budget,
        Func<string, CancellationToken, Task<BlobListing>> list,
        Func<string, long, CancellationToken, Task<byte[]?>> read, CancellationToken cancellationToken)
    {
        string run = options.RunId!, phase = options.Phase!;
        var ordinary = new List<ProducerRegistration>();
        var abrupt = new List<B10FinalAbruptRegistration>();
        await ReadPrefix(PhasePaths.ProducersPrefix(run, phase, repeat), false).ConfigureAwait(false);
        if (phase == "C1") await ReadPrefix($"{run}/C1/r{repeat}/b10-abrupt/", true).ConfigureAwait(false);
        B10FinalProjectionCodec.Require(ordinary.Count <= 1 && abrupt.Count <= 2);
        B10FinalAbruptRegistration? first = abrupt.SingleOrDefault(x => x.PredecessorInstanceId is null);
        B10FinalAbruptRegistration? successor = abrupt.SingleOrDefault(x => x.PredecessorInstanceId is not null);
        B10FinalProjectionCodec.Require(successor is null || first is not null && successor.PredecessorInstanceId == first.InstanceId);
        return new(ordinary.SingleOrDefault(), first, successor);

        async Task ReadPrefix(string prefix, bool isAbrupt)
        {
            BlobListing listing = await list(prefix, cancellationToken).ConfigureAwait(false);
            B10FinalProjectionCodec.Require(listing.IsComplete && listing.Names.Count <= BoundedBlobListing.MaximumEntries &&
                listing.Pages is >= 0 and <= BoundedBlobListing.MaximumPages);
            long nameBytes = listing.Names.Sum(x => (long)Encoding.UTF8.GetByteCount(x));
            B10FinalProjectionCodec.Require(nameBytes <= BoundedBlobListing.MaximumNameBytes);
            budget.ConsumeEntries(listing.Names.Count); budget.ConsumeBytes(nameBytes);
            foreach (string name in listing.Names)
            {
                B10FinalProjectionCodec.Require(name.StartsWith(prefix, StringComparison.Ordinal));
                if (!name.EndsWith("/registration.json", StringComparison.Ordinal)) continue;
                string relative = name[prefix.Length..]; string[] parts = relative.Split('/');
                B10FinalProjectionCodec.Require(parts.Length == 2 && parts[0].Length is > 0 and <= 128 &&
                    parts[0].All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
                string instance = parts[0];
                byte[] bytes = await read(name, PhaseWireCodec.MaximumBytes, cancellationToken).ConfigureAwait(false)
                    ?? throw new FormatException("Registered producer disappeared.");
                B10FinalProjectionCodec.Require(bytes.LongLength <= PhaseWireCodec.MaximumBytes); budget.ConsumeBytes(bytes.LongLength);
                if (isAbrupt) abrupt.Add(ReadAbrupt(bytes, options, repeat, instance));
                else
                {
                    ProducerRegistration registration = PhaseWireCodec.ReadRegistration(bytes);
                    B10FinalProjectionCodec.Require(registration.SchemaVersion == 1 && registration.Identity.ScenarioId == "B10" &&
                        registration.Identity.ConfigurationId == phase && registration.Identity.RunId == run &&
                        registration.Identity.InstanceId == instance && registration.Identity.ExpectedInstanceId == instance &&
                        registration.PodName == instance && registration.Phase == phase && registration.RepeatNumber == repeat &&
                        registration.JobName == PhaseIdentity.JobName(run, phase, repeat) &&
                        registration.PredecessorInstanceId is null && registration.SourceCommit == options.SourceCommit);
                    ordinary.Add(registration);
                }
            }
        }
    }

    private static B10FinalAbruptRegistration ReadAbrupt(byte[] bytes, VerifierHostOptions options, int repeat, string instance)
    {
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        JsonElement root = document.RootElement;
        B10FinalProjectionCodec.Exact(root, "schema_version", "dialect", "purpose", "synthetic", "scientific_admissible", "kind", "identity", "data");
        B10FinalProjectionCodec.Require(root.GetProperty("schema_version").GetInt32() == 3 &&
            B10FinalProjectionCodec.Text(root, "dialect") == "b10_abrupt" && B10FinalProjectionCodec.Text(root, "purpose") == "scientific" &&
            root.GetProperty("synthetic").ValueKind == JsonValueKind.True && root.GetProperty("scientific_admissible").ValueKind == JsonValueKind.False &&
            B10FinalProjectionCodec.Text(root, "kind") == "registration");
        JsonElement identity = root.GetProperty("identity");
        B10FinalProjectionCodec.Exact(identity, "run_id", "scenario_id", "configuration_id", "phase", "repeat", "source_commit", "image_reference", "job_name", "pod_name", "pod_uid", "instance_id", "predecessor_instance_id");
        string? predecessor = identity.GetProperty("predecessor_instance_id").ValueKind == JsonValueKind.Null ? null : B10FinalProjectionCodec.Text(identity, "predecessor_instance_id");
        string job = $"{options.RunId}-c1-r{repeat}-loadgen-" + (predecessor is null ? "restart" : "successor");
        B10FinalProjectionCodec.Require(B10FinalProjectionCodec.Text(identity, "run_id") == options.RunId &&
            B10FinalProjectionCodec.Text(identity, "scenario_id") == "B10" && B10FinalProjectionCodec.Text(identity, "configuration_id") == "C1" &&
            B10FinalProjectionCodec.Text(identity, "phase") == "C1" && identity.GetProperty("repeat").GetInt32() == repeat &&
            B10FinalProjectionCodec.Text(identity, "source_commit") == options.SourceCommit &&
            B10FinalProjectionCodec.Text(identity, "image_reference") == options.LoadGenImage &&
            B10FinalProjectionCodec.Text(identity, "job_name") == job && B10FinalProjectionCodec.Text(identity, "pod_name") == instance &&
            B10FinalProjectionCodec.Text(identity, "instance_id") == instance);
        _ = B10FinalProjectionCodec.Text(identity, "pod_uid");
        B10FinalProjectionCodec.Exact(root.GetProperty("data"), "registered_utc");
        _ = PhaseWireCodec.ParseUtc(B10FinalProjectionCodec.Text(root.GetProperty("data"), "registered_utc"));
        return new(instance, predecessor);
    }
}

internal sealed record B10FinalAbruptRegistration(string InstanceId, string? PredecessorInstanceId);
internal sealed record B10FinalRegistrations(ProducerRegistration? Timeout, B10FinalAbruptRegistration? First,
    B10FinalAbruptRegistration? Successor)
{
    internal IEnumerable<string> InstanceIds => new[] { Timeout?.Identity.InstanceId, First?.InstanceId, Successor?.InstanceId }.OfType<string>();
}

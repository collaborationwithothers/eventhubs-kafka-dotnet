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
        var abruptNames = new List<string>();
        await ReadPrefix(PhasePaths.ProducersPrefix(run, phase, repeat), false).ConfigureAwait(false);
        if (phase == "C1") await ReadPrefix($"{run}/C1/r{repeat}/b10-abrupt/", true).ConfigureAwait(false);
        B10FinalAbruptRegistration[] firsts = abrupt.Where(x => x.PredecessorInstanceId is null).ToArray();
        B10FinalAbruptRegistration[] successors = abrupt.Where(x => x.PredecessorInstanceId is not null).ToArray();
        B10FinalAbruptRegistration? first = firsts.Length == 1 ? firsts[0] : null;
        B10FinalAbruptRegistration? successor = successors.Length == 1 ? successors[0] : null;
        bool restartCountPass = firsts.Length == 1 && successors.Length <= 1 && abrupt.All(registration => registration.BindingPass) &&
            (successor is null || successor.PredecessorInstanceId == first!.InstanceId);
        return new(ordinary.Count == 1 ? ordinary[0] : null, first, successor, ordinary,
            abrupt.Select(x => x.InstanceId).ToArray(), abruptNames, restartCountPass);

        async Task ReadPrefix(string prefix, bool isAbrupt)
        {
            BlobListing listing = await list(prefix, cancellationToken).ConfigureAwait(false);
            B10FinalProjectionCodec.Require(listing.IsComplete && listing.Names.Count <= BoundedBlobListing.MaximumEntries &&
                listing.Pages is >= 0 and <= BoundedBlobListing.MaximumPages);
            long nameBytes = listing.Names.Sum(x => (long)Encoding.UTF8.GetByteCount(x));
            B10FinalProjectionCodec.Require(nameBytes <= BoundedBlobListing.MaximumNameBytes);
            budget.ConsumeEntries(listing.Names.Count); budget.ConsumeBytes(nameBytes);
            if (isAbrupt) abruptNames.AddRange(listing.Names);
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

    internal static async Task<bool> SuccessorPermittedAsync(VerifierHostOptions options, int repeat,
        B10FinalAbruptRegistration? first, IReadOnlyList<string> names, InputBudget budget,
        Func<string, long, CancellationToken, Task<byte[]?>> read, CancellationToken cancellationToken)
    {
        if (first is null || !first.BindingPass) return false;
        string prefix = $"{options.RunId}/C1/r{repeat}/b10-abrupt/{first.InstanceId}/";
        try
        {
            byte[]? terminationBytes = await Read("termination.json").ConfigureAwait(false);
            byte[]? manifestBytes = await Read("abrupt-manifest.json").ConfigureAwait(false);
            byte[]? journalBytes = await Read("journal.jsonl").ConfigureAwait(false);
            if (terminationBytes is null || manifestBytes is null || journalBytes is null) return false;
            using JsonDocument termination = JsonDocument.Parse(terminationBytes);
            using JsonDocument manifest = JsonDocument.Parse(manifestBytes);
            JsonElement terminationData = Envelope(termination.RootElement, "termination");
            JsonElement manifestData = Envelope(manifest.RootElement, "abrupt_manifest");
            Bind(terminationData.GetProperty("manifest"), "abrupt-manifest.json", manifestBytes);
            Bind(manifestData.GetProperty("journal"), "journal.jsonl", journalBytes);
            var expectedNames = new HashSet<string>(["registration.json", "termination.json", "abrupt-manifest.json", "journal.jsonl"], StringComparer.Ordinal);
            foreach (JsonElement report in manifestData.GetProperty("report_files").EnumerateArray())
            {
                string name = ScopeText(report, "name");
                B10FinalProjectionCodec.Require(name is "same_instance-pre-acceptance-report.json" or "control-post-drain-report.json" or "cross_restart-pre-acceptance-report.json" && expectedNames.Add(name));
                byte[]? bytes = await Read(name).ConfigureAwait(false);
                if (bytes is null) return false;
                Bind(report, name, bytes);
            }
            B10FinalProjectionCodec.Require(expectedNames.SetEquals(names.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).Select(name => name[prefix.Length..])));
            return terminationData.GetProperty("successor_permitted").ValueKind == JsonValueKind.True;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException or InputLimitExceededException)
        { return false; }

        async Task<byte[]?> Read(string name)
        {
            long maximum = name == "journal.jsonl" ? B10FinalProjectionCodec.MaximumBytes : name.EndsWith("-report.json", StringComparison.Ordinal) ? 1_048_576 : PhaseWireCodec.MaximumBytes;
            byte[]? bytes = await read(prefix + name, maximum, cancellationToken).ConfigureAwait(false);
            if (bytes is not null) { B10FinalProjectionCodec.Require(bytes.LongLength > 0 && bytes.LongLength <= maximum); budget.ConsumeBytes(bytes.LongLength); }
            return bytes;
        }
        JsonElement Envelope(JsonElement root, string kind)
        {
            B10FinalProjectionCodec.Exact(root, "schema_version", "dialect", "purpose", "synthetic", "scientific_admissible", "kind", "identity", "data");
            B10FinalProjectionCodec.Require(root.GetProperty("schema_version").GetInt32() == 3 && ScopeText(root, "kind") == kind &&
                ScopeText(root, "dialect") == "b10_abrupt" && ScopeText(root, "purpose") == "scientific" &&
                root.GetProperty("synthetic").ValueKind == JsonValueKind.True && root.GetProperty("scientific_admissible").ValueKind == JsonValueKind.False);
            JsonElement identity = root.GetProperty("identity");
            B10FinalProjectionCodec.Exact(identity, "run_id", "scenario_id", "configuration_id", "phase", "repeat", "source_commit", "image_reference", "job_name", "pod_name", "pod_uid", "instance_id", "predecessor_instance_id");
            B10FinalProjectionCodec.Require(ScopeText(identity, "run_id") == options.RunId &&
                ScopeText(identity, "scenario_id") == "B10" && ScopeText(identity, "configuration_id") == "C1" &&
                ScopeText(identity, "phase") == "C1" && identity.GetProperty("repeat").GetInt32() == repeat &&
                ScopeText(identity, "source_commit") == options.SourceCommit && ScopeText(identity, "image_reference") == options.LoadGenImage &&
                ScopeText(identity, "job_name") == $"{options.RunId}-c1-r{repeat}-loadgen-restart" &&
                ScopeText(identity, "pod_name") == first.InstanceId && ScopeText(identity, "instance_id") == first.InstanceId &&
                ScopeText(identity, "pod_uid") == first.PodUid && identity.GetProperty("predecessor_instance_id").ValueKind == JsonValueKind.Null);
            JsonElement data = root.GetProperty("data");
            if (kind == "abrupt_manifest") B10FinalProjectionCodec.Exact(data, "journal", "journal_records", "report_files", "gaps");
            else B10FinalProjectionCodec.Exact(data, "termination", "outcome", "detail", "signal_observation", "successor_permitted", "child", "exit_observed", "exit_code_local_observation", "runtime_version", "wait_bound_milliseconds", "cgroup_before", "cgroup_after", "control_unconfirmed_attempt_ids", "kill_unconfirmed_attempt_ids", "capture_gaps", "native_error_count", "native_fatal_count", "native_tail", "manifest");
            return data;
        }
        static void Bind(JsonElement descriptor, string name, byte[] bytes)
        {
            B10FinalProjectionCodec.Exact(descriptor, "name", "length", "sha256");
            B10FinalProjectionCodec.Require(ScopeText(descriptor, "name") == name && descriptor.GetProperty("length").GetInt64() == bytes.LongLength &&
                ScopeText(descriptor, "sha256") == Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
        }
    }

    internal static DateTimeOffset ParseUtc(string value)
    {
        if (!DateTimeOffset.TryParseExact(value, "O", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out DateTimeOffset parsed) || parsed.Offset != TimeSpan.Zero)
            throw new FormatException("Final B10 evidence timestamp must be a UTC round-trip value.");
        return parsed;
    }

    private static string ScopeText(JsonElement value, string name)
    {
        JsonElement field = value.GetProperty(name);
        B10FinalProjectionCodec.Require(field.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(field.GetString()));
        return field.GetString()!;
    }

    private static B10FinalAbruptRegistration ReadAbrupt(byte[] bytes, VerifierHostOptions options, int repeat, string instance)
    {
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        JsonElement root = document.RootElement;
        B10FinalProjectionCodec.Exact(root, "schema_version", "dialect", "purpose", "synthetic", "scientific_admissible", "kind", "identity", "data");
        B10FinalProjectionCodec.Require(root.GetProperty("schema_version").GetInt32() == 3 &&
            ScopeText(root, "dialect") == "b10_abrupt" && ScopeText(root, "purpose") == "scientific" &&
            root.GetProperty("synthetic").ValueKind == JsonValueKind.True && root.GetProperty("scientific_admissible").ValueKind == JsonValueKind.False &&
            ScopeText(root, "kind") == "registration");
        JsonElement identity = root.GetProperty("identity");
        B10FinalProjectionCodec.Exact(identity, "run_id", "scenario_id", "configuration_id", "phase", "repeat", "source_commit", "image_reference", "job_name", "pod_name", "pod_uid", "instance_id", "predecessor_instance_id");
        string? predecessor = identity.GetProperty("predecessor_instance_id").ValueKind == JsonValueKind.Null ? null : ScopeText(identity, "predecessor_instance_id");
        string job = $"{options.RunId}-c1-r{repeat}-loadgen-" + (predecessor is null ? "restart" : "successor");
        B10FinalProjectionCodec.Require(ScopeText(identity, "run_id") == options.RunId &&
            ScopeText(identity, "scenario_id") == "B10" && ScopeText(identity, "configuration_id") == "C1" &&
            ScopeText(identity, "phase") == "C1" && identity.GetProperty("repeat").GetInt32() == repeat &&
            ScopeText(identity, "pod_name") == instance && ScopeText(identity, "instance_id") == instance);
        string source = ScopeText(identity, "source_commit"), image = ScopeText(identity, "image_reference"), actualJob = ScopeText(identity, "job_name");
        bool bindingPass = source == options.SourceCommit && image == options.LoadGenImage && actualJob == job;
        _ = ScopeText(identity, "pod_uid");
        B10FinalProjectionCodec.Exact(root.GetProperty("data"), "registered_utc");
        _ = ParseUtc(ScopeText(root.GetProperty("data"), "registered_utc"));
        return new(instance, predecessor, ScopeText(identity, "pod_uid"), bindingPass);
    }
}

internal sealed record B10FinalAbruptRegistration(string InstanceId, string? PredecessorInstanceId, string PodUid, bool BindingPass);
internal sealed record B10FinalRegistrations(ProducerRegistration? Timeout, B10FinalAbruptRegistration? First,
    B10FinalAbruptRegistration? Successor, IReadOnlyList<ProducerRegistration> Ordinary,
    IReadOnlyList<string> AbruptInstances, IReadOnlyList<string> AbruptNames, bool RestartCountPass)
{
    internal IEnumerable<string> InstanceIds => Ordinary.Select(x => x.Identity.InstanceId).Concat(AbruptInstances);
}

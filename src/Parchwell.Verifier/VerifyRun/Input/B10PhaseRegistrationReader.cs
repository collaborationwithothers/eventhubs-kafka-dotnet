using System.Text;
using System.Text.Json;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.VerifyRun.Judgement;

namespace Parchwell.Verifier.VerifyRun.Input;

/// <summary>Authenticates ordered producer ownership for a bounded prefix of one B10 phase.</summary>
internal static class B10PhaseRegistrationReader
{
    internal static async Task<IReadOnlyList<IReadOnlyList<ProducerRegistration>>> ReadAsync(
        VerifierHostOptions options, int lastRepeat, IProducerArtifactStore store, InputBudget budget,
        Func<string, CancellationToken, Task<byte[]>> readBounded, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (options.ScenarioId != "B10" || lastRepeat is < 0 or > 5)
            throw new InvalidOperationException("b10_phase_registration_scope_invalid");
        var repeats = new List<IReadOnlyList<ProducerRegistration>>();
        var instances = new HashSet<string>(StringComparer.Ordinal);
        for (int repeat = 1; repeat <= lastRepeat; repeat++)
        {
            string prefix = PhasePaths.ProducersPrefix(options.RunId!, options.Phase!, repeat);
            long origin = clock.GetTimestamp();
            using var deadline = new CancellationTokenSource(options.BlobIoTimeout!.Value, clock);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
            BlobListing listing = await store.ListAsync(prefix, linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            if (clock.GetElapsedTime(origin) >= options.BlobIoTimeout.Value)
                throw new TimeoutException("b10_phase_registration_listing_timeout");
            if (!listing.IsComplete || listing.Pages is < 0 or > BoundedBlobListing.MaximumPages ||
                listing.Names.Count > BoundedBlobListing.MaximumEntries)
                throw new InvalidOperationException("b10_phase_registration_listing_incomplete");
            long nameBytes = listing.Names.Sum(name => (long)Encoding.UTF8.GetByteCount(name));
            if (nameBytes > BoundedBlobListing.MaximumNameBytes)
                throw new InputLimitExceededException("B10 registration listing exceeds its name-byte limit.");
            budget.ConsumeEntries(listing.Names.Count);
            budget.ConsumeBytes(nameBytes);
            string[] names = listing.Names.Where(name => name.EndsWith("/registration.json", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal).ToArray();
            bool oneProducer = options.ConfigurationId == "C0" && VerifierHostOptions.IsB10ScientificRunId(options.RunId);
            if (names.Length != (oneProducer ? 1 : 2)) throw new InvalidOperationException("b10_registration_count_invalid");
            var discovered = new List<(ProducerRegistration Registration, string PathInstance)>();
            foreach (string name in names)
            {
                string instance = RegistrationInstance(prefix, name);
                byte[] bytes = await readBounded(name, cancellationToken).ConfigureAwait(false);
                if (bytes.LongLength > options.MaxArtifactBytes!.Value)
                    throw new InputLimitExceededException("B10 registration exceeds the artifact-byte limit.");
                budget.ConsumeBytes(bytes.LongLength);
                var limits = new InputLimits(options.MaxInputDepth!.Value, options.MaxArtifactBytes.Value,
                    options.MaxTotalInputBytes!.Value, options.MaxRowBytes!.Value, options.MaxRowsPerArtifact!.Value,
                    options.MaxInputEntries!.Value, options.MaxDiagnosticBytes!.Value);
                ParseResult<JsonDocument> parsed = StrictJson.ParseDocument(bytes, name, limits, null);
                if (!parsed.IsSuccess) throw new FormatException("b10_phase_registration_malformed");
                using JsonDocument document = parsed.Value!;
                discovered.Add((PhaseWireCodec.ReadRegistration(bytes), instance));
            }
            IReadOnlyList<ProducerRegistration> pair = B10IntakeChecks.BindRegistrations(discovered,
                options.ScenarioId, options.ConfigurationId!, options.RunId!, options.Phase!, repeat, options.SourceCommit!,
                oneProducer ? [PhaseIdentity.JobName(options.RunId!, options.Phase!, repeat)] :
                    [PhaseIdentity.JobName(options.RunId!, options.Phase!, repeat),
                    PhaseIdentity.RestartJobName(options.RunId!, options.Phase!, repeat)]);
            foreach (ProducerRegistration registration in pair)
                if (!instances.Add(registration.Identity.InstanceId))
                    throw new InvalidOperationException("b10_phase_registration_identity_duplicate");
            repeats.Add(pair);
        }
        return repeats;
    }

    private static string RegistrationInstance(string prefix, string name)
    {
        if (!name.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("registration_prefix_escape");
        string[] parts = name[prefix.Length..].Split('/');
        if (parts.Length != 2 || parts[1] != "registration.json" || parts[0].Length is 0 or > 128 ||
            parts[0].Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new InvalidOperationException("registration_path_invalid");
        return parts[0];
    }
}

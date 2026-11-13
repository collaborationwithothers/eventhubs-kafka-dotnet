using System.Text;
using System.Text.Json;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.VerifyRun.Input;
using static Parchwell.Verifier.VerifyRun.Abrupt.AbruptEvidenceCodec;

namespace Parchwell.Verifier.VerifyRun.Abrupt;

internal static class EarlierRepeatEvidenceReader
{
    internal static async Task<IReadOnlySet<string>> ReadAsync(VerifierHostOptions options,
        IReadOnlySet<string> currentInstances, InputBudget budget,
        Func<string, CancellationToken, Task<BlobListing>> list,
        Func<string, long, CancellationToken, Task<byte[]?>> read, CancellationToken cancellationToken)
    {
        var instances = new HashSet<string>(StringComparer.Ordinal);
        for (int repeat = 1; repeat < options.Repeat!.Value; repeat++)
        {
            await ReadPrefix(PhasePaths.ProducersPrefix(options.RunId!, "C1", repeat), repeat, abrupt: false).ConfigureAwait(false);
            await ReadPrefix($"{options.RunId}/C1/r{repeat}/b10-abrupt/", repeat, abrupt: true).ConfigureAwait(false);
        }
        Require(!instances.Overlaps(currentInstances), "earlier_repeat_identity_duplicate");
        return instances;

        async Task ReadPrefix(string prefix, int repeat, bool abrupt)
        {
            BlobListing listing = await list(prefix, cancellationToken).ConfigureAwait(false);
            Require(listing.IsComplete && listing.Pages is >= 0 and <= BoundedBlobListing.MaximumPages &&
                listing.Names.Count <= BoundedBlobListing.MaximumEntries, "earlier_repeat_listing_incomplete");
            long nameBytes = listing.Names.Sum(name => (long)Encoding.UTF8.GetByteCount(name));
            Require(nameBytes <= BoundedBlobListing.MaximumNameBytes, "earlier_repeat_listing_name_bytes");
            budget.ConsumeEntries(listing.Names.Count);
            budget.ConsumeBytes(nameBytes);
            foreach (string name in listing.Names)
            {
                Require(name.StartsWith(prefix, StringComparison.Ordinal), "earlier_repeat_listing_path");
                string[] parts = name[prefix.Length..].Split('/');
                Require(parts.Length >= 2 && parts[0].Length is > 0 and <= 128 &&
                    parts[0].All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'), "earlier_repeat_listing_path");
                if (!name.EndsWith("/registration.json", StringComparison.Ordinal)) continue;
                Require(parts.Length == 2, "earlier_repeat_registration_path");
                string instance = parts[0];
                byte[] bytes = await read(name, PhaseWireCodec.MaximumBytes, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidDataException("earlier_repeat_registration_missing");
                Require(bytes.Length <= PhaseWireCodec.MaximumBytes, "earlier_repeat_registration_bytes");
                budget.ConsumeBytes(bytes.LongLength);
                if (abrupt)
                {
                    JsonElement root = Parse(bytes);
                    string? predecessor = NullableText(root.GetProperty("identity"), "predecessor_instance_id");
                    var earlier = new VerifierHostOptions
                    {
                        RunId = options.RunId, Repeat = repeat, VerifierCommit = options.VerifierCommit,
                        LoadGenImage = options.LoadGenImage
                    };
                    _ = Envelope(bytes, "registration", earlier, instance, predecessor);
                }
                else
                {
                    ProducerRegistration registration = PhaseWireCodec.ReadRegistration(bytes);
                    Require(registration.Identity.RunId == options.RunId && registration.Identity.ScenarioId == "B10" &&
                        registration.Identity.ConfigurationId == "C1" && registration.Phase == "C1" &&
                        registration.RepeatNumber == repeat && registration.Identity.InstanceId == instance &&
                        registration.Identity.ExpectedInstanceId == instance && registration.PodName == instance &&
                        registration.JobName == PhaseIdentity.JobName(options.RunId!, "C1", repeat) &&
                        registration.PredecessorInstanceId is null && registration.SourceCommit == options.VerifierCommit,
                        "earlier_repeat_registration_binding");
                }
                instances.Add(instance);
            }
        }
    }
}

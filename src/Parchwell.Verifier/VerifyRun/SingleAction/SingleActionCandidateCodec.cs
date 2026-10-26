using Parchwell.Verifier.Common.SingleAction;

namespace Parchwell.Verifier.VerifyRun.SingleAction;

internal static class SingleActionCandidateCodec
{
    private static readonly string[] ImageNames = ["enrichment", "loadgen", "verifier"];

    internal static byte[] Write(SingleActionCandidate candidate)
    {
        ValidateImageDigests(candidate.ImageDigests);
        return SingleActionWire.Write(candidate,16777216);
    }

    internal static SingleActionCandidate Read(byte[] bytes)
    {
        var candidate = SingleActionWire.Read<SingleActionCandidate>(bytes,16777216);
        ValidateImageDigests(candidate.ImageDigests);
        return candidate;
    }

    private static void ValidateImageDigests(IReadOnlyDictionary<string,string>? digests)
    {
        if (digests is null || !digests.Keys.Order(StringComparer.Ordinal).SequenceEqual(ImageNames,StringComparer.Ordinal) ||
            digests.Values.Any(value => value is not { Length: 71 } || !value.StartsWith("sha256:",StringComparison.Ordinal) ||
                value[7..].Any(character => !(character is >= '0' and <= '9' or >= 'a' and <= 'f'))))
            throw new FormatException("single_action_image_digests");
    }
}

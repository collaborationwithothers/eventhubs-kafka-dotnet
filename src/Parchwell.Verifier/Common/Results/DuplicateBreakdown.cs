using System.Text.Json;
using System.Text.Json.Serialization;

namespace Parchwell.Verifier.Common.Results;

internal sealed record DuplicateBreakdown(
    [property: JsonPropertyName("duplicate_same_attempt")] long DuplicateSameAttempt,
    [property: JsonPropertyName("duplicate_different_attempt")] long DuplicateDifferentAttempt)
{
    internal long Total => checked(DuplicateSameAttempt + DuplicateDifferentAttempt);

    internal bool IsValid(long? expectedDuplicate = null)
    {
        if (DuplicateSameAttempt < 0 || DuplicateDifferentAttempt < 0) return false;
        try { long total = Total; return expectedDuplicate is null || total == expectedDuplicate.Value; }
        catch (OverflowException) { return false; }
    }

    internal static bool TryRead(JsonElement value, out DuplicateBreakdown? result)
    {
        result = null;
        if (value.ValueKind != JsonValueKind.Object) return false;
        long? same = null, different = null;
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt64(out long number) || number < 0) return false;
            if (property.NameEquals("duplicate_same_attempt") && same is null) same = number;
            else if (property.NameEquals("duplicate_different_attempt") && different is null) different = number;
            else return false;
        }
        if (same is null || different is null) return false;
        var candidate = new DuplicateBreakdown(same.Value, different.Value);
        if (!candidate.IsValid()) return false;
        result = candidate;
        return true;
    }

    internal static DuplicateBreakdown? Sum(IEnumerable<DuplicateBreakdown?> values)
    {
        DuplicateBreakdown?[] all = values.ToArray();
        if (all.Any(value => value is null)) return null;
        long same = 0, different = 0;
        foreach (DuplicateBreakdown? value in all)
        {
            if (value is null) return null;
            if (!value.IsValid()) throw new ArgumentException("Invalid duplicate breakdown.", nameof(values));
            same = checked(same + value.DuplicateSameAttempt);
            different = checked(different + value.DuplicateDifferentAttempt);
        }
        var result = new DuplicateBreakdown(same, different);
        _ = result.Total;
        return result;
    }
}

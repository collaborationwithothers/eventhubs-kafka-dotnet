using System.Text.Json;
using System.Text.Json.Serialization;

namespace Parchwell.Runner.Run.Cost;

public sealed record PriceSource(string Url, DateTimeOffset RetrievedAt, string Unit, decimal Quantity, decimal UnitPriceGbp);
public sealed record PhaseEstimate(decimal PhaseEstimateGbp, decimal TeardownReserveGbp);
public sealed record CostInput(string Currency, string Item, decimal BaselineSpendGbp, string[] BaselineSources,
    Dictionary<string, PhaseEstimate> PhaseEstimates, decimal PersistentEstimateGbp, PriceSource[] Sources);

public static class PhaseReservation
{
    public const decimal LimitGbp = 15m;
    public static CostInput Read(ReadOnlySpan<byte> bytes)
    {
        var input = JsonSerializer.Deserialize<CostInput>(bytes, LedgerJson.Options)
            ?? throw new InvalidDataException("cost_input_missing");
        Validate(input);
        return input;
    }

    public static void Validate(CostInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Currency != "GBP" || input.Item != "A01" || input.BaselineSpendGbp < 0 || input.PersistentEstimateGbp < 0 ||
            input.BaselineSources is not { Length: > 0 } || input.BaselineSources.Any(string.IsNullOrWhiteSpace) ||
            input.Sources is not { Length: > 0 } || input.PhaseEstimates is null ||
            !input.PhaseEstimates.Keys.Order(StringComparer.Ordinal).SequenceEqual(RunLedger.PhaseNames.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("cost_input_invalid");
        foreach (var estimate in input.PhaseEstimates.Values)
            if (estimate is null || estimate.PhaseEstimateGbp <= 0 || estimate.TeardownReserveGbp <= 0)
                throw new InvalidDataException("cost_allowance_invalid");
        foreach (var source in input.Sources)
            if (source is null || !Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                source.RetrievedAt == default || string.IsNullOrWhiteSpace(source.Unit) || source.Quantity < 0 || source.UnitPriceGbp < 0)
                throw new InvalidDataException("price_source_invalid");
    }

    public static decimal CommittedSpend(LedgerDocument ledger) => checked(ledger.BaselineSpendGbp + ledger.Phases.Values.Sum(
        phase => phase.ReservationId is null ? 0m : phase.ActualCostGbp ?? phase.ReservedTotalGbp));
}

public static class LedgerJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 24,
        WriteIndented = false
    };
}

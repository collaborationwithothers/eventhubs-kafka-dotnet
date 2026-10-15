using System.Collections.Frozen;

namespace Parchwell.Kafka.Configuration;

internal static class FrozenInventory
{
    // docs/blueprint.md section 12, frozen inventory and amendment 1.
    private static readonly FrozenSet<string> Ids = new[]
    {
        "A01", "A02", "A03", "A04", "A05", "A06", "B01", "B02", "B03", "B04",
        "B05", "B06", "B07", "B08", "B09", "B10", "B11", "C01", "C02", "C03",
        "C04", "C05", "C06", "C07", "C08", "C09", "C10", "C11", "C12", "C13",
        "C14", "C15", "C16", "C17", "C18", "C19", "D01", "D02", "D03", "D04",
        "D05", "E01", "E02", "E03", "E04", "E05", "E06", "F01", "F02", "F03",
        "F04", "F05", "F06", "G01", "G02", "G03", "G04", "G05", "H01", "H02",
        "H03", "H04", "H05", "H06", "H07", "H08", "K01", "K02", "K03", "K04",
        "K05", "K06", "K07", "K08", "K09", "K10",
    }.ToFrozenSet(StringComparer.Ordinal);

    internal static bool Contains(string? id) => id is not null && Ids.Contains(id);
}

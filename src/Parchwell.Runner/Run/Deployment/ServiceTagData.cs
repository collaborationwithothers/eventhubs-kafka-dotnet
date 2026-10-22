using System.Net;
using System.Text.Json;

namespace Parchwell.Runner.Run.Deployment;

public static class ServiceTagData
{
    public static void Validate(JsonElement data)
    {
        RejectDuplicates(data);
        if (data.GetProperty("schema_version").GetInt32() != 1 || !data.TryGetProperty("source_url", out JsonElement source) ||
            !Uri.TryCreate(source.GetString(), UriKind.Absolute, out Uri? uri) || uri.Scheme != "https" || uri.Host != "download.microsoft.com" || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.Port != 443 || !uri.AbsolutePath.EndsWith(".json", StringComparison.Ordinal))
            throw new InvalidDataException("service_tags_source_invalid");
        if (!DateTimeOffset.TryParse(data.GetProperty("retrieved_utc").GetString(), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out _) || data.GetProperty("source_sha256").GetString() is not { Length: 64 } hash || hash.Any(c => !char.IsAsciiHexDigitLower(c)) ||
            !data.TryGetProperty("change_number", out JsonElement change) || !change.TryGetInt64(out long rootChange) || rootChange < 0) throw new InvalidDataException("service_tags_receipt_missing");
        JsonElement tags = data.GetProperty("tags");
        if (tags.EnumerateObject().Count() != 2) throw new InvalidDataException("service_tags_exact_set_required");
        foreach (string name in new[] { "AzureActiveDirectory", "AzureMonitor" })
        {
            JsonElement tag = tags.GetProperty(name);
            if (!tag.TryGetProperty("change_number", out JsonElement tagChange) || !tagChange.TryGetInt64(out long number) || number < 0) throw new InvalidDataException("service_tag_change_number_required");
            string[] prefixes = tag.GetProperty("address_prefixes").EnumerateArray().Select(item => item.GetString() ?? "").ToArray();
            if (prefixes.Length is < 1 or > 4096 || !prefixes.SequenceEqual(prefixes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)))
                throw new InvalidDataException("service_tag_prefix_set_invalid");
            foreach (string prefix in prefixes)
            {
                string[] parts = prefix.Split('/');
                if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out IPAddress? ip) || !int.TryParse(parts[1], out int bits) ||
                    parts[0].Contains('%', StringComparison.Ordinal) || parts[0] != ip.ToString() || bits < 1 || bits > (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128))
                    throw new InvalidDataException("service_tag_cidr_invalid");
                byte[] address = ip.GetAddressBytes();
                for (int index = bits; index < address.Length * 8; index++)
                    if ((address[index / 8] & (1 << (7 - index % 8))) != 0)
                        throw new InvalidDataException("service_tag_cidr_host_bits_invalid");
            }
        }
    }
    private static void RejectDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("service_tag_duplicate_property");
                RejectDuplicates(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in value.EnumerateArray()) RejectDuplicates(item);
    }
}

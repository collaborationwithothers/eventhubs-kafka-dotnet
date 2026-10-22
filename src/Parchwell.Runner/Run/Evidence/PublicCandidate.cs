using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Parchwell.Runner.Run.Evidence;

/// <summary>The only workflow artifact: a bounded projection with no raw evidence fields.</summary>
public static partial class PublicCandidate
{
    private static readonly string[] ScalarKeys = ["scenario_id", "configuration_id", "run_id", "source_commit", "status", "gate"];
    private static readonly Dictionary<string, string[]> ObjectKeys = new(StringComparer.Ordinal)
    {
        ["versions"] = ["runner", "loadgen", "verifier", "confluent_kafka", "librdkafka", "dotnet"],
        ["image_digests"] = ["loadgen", "verifier"],
        ["timings"] = ["started_utc", "ended_utc", "duration_seconds"],
        ["counts"] = ["attempted", "acknowledged", "consumed", "missing", "duplicates", "out_of_order"],
        ["costs"] = ["estimate_gbp", "actual_gbp", "actual_available"],
        ["diagnostics"] = ["error_codes", "max_throttle_ms", "request_timeout_increment", "disconnect_increment", "rtt_p99_ms"]
    };

    public static byte[] Create(ReadOnlySpan<byte> input, string subscriptionGuid)
    {
        if (subscriptionGuid is not { Length: 36 } || !Guid.TryParseExact(subscriptionGuid, "D", out _))
            throw new InvalidDataException("subscription_redaction_input_invalid");
        if (input.Length > 1_048_576) throw new InvalidDataException("candidate_size_exceeded");
        using var source = JsonDocument.Parse(input.ToArray(), new JsonDocumentOptions { MaxDepth = 12 });
        if (source.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("candidate_object_required");
        var projected = new Dictionary<string, object?>(StringComparer.Ordinal) { ["synthetic"] = true };
        foreach (var key in ScalarKeys)
            if (source.RootElement.TryGetProperty(key, out var value)) projected[key] = Scalar(value, subscriptionGuid);
        foreach (var (key, allowed) in ObjectKeys)
        {
            if (!source.RootElement.TryGetProperty(key, out var value)) continue;
            if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException("candidate_nested_object_required");
            var nested = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var field in allowed)
            {
                if (!value.TryGetProperty(field, out var child)) continue;
                if (key == "diagnostics" && field == "error_codes")
                {
                    if (child.ValueKind != JsonValueKind.Array || child.GetArrayLength() > 100) throw new InvalidDataException("candidate_codes_invalid");
                    nested[field] = child.EnumerateArray().Select(code => code.GetInt32()).ToArray();
                }
                else if (key is "counts" or "diagnostics" || key == "costs" && field != "actual_available" || key == "timings" && field == "duration_seconds")
                {
                    if (child.ValueKind == JsonValueKind.Null) nested[field] = null;
                    else if (child.ValueKind == JsonValueKind.Number && child.TryGetDecimal(out var number) && number >= 0) nested[field] = number;
                    else throw new InvalidDataException("candidate_number_invalid");
                }
                else nested[field] = Scalar(child, subscriptionGuid);
            }
            projected[key] = nested;
        }
        if (source.RootElement.TryGetProperty("reasons", out var reasons))
        {
            if (reasons.ValueKind != JsonValueKind.Array || reasons.GetArrayLength() > 100) throw new InvalidDataException("candidate_reasons_invalid");
            projected["reasons"] = reasons.EnumerateArray().Select(reason =>
            {
                var code = reason.GetString() ?? throw new InvalidDataException("candidate_reason_invalid");
                if (!SafeCode().IsMatch(code)) throw new InvalidDataException("candidate_reason_invalid");
                return code.Replace(subscriptionGuid, "[REDACTED_SUBSCRIPTION]", StringComparison.OrdinalIgnoreCase);
            }).ToArray();
        }
        var bytes = JsonSerializer.SerializeToUtf8Bytes(projected);
        if (Encoding.UTF8.GetString(bytes).Contains(subscriptionGuid, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("candidate_subscription_remains");
        return bytes;
    }

    private static object? Scalar(JsonElement value, string subscription)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.GetBoolean();
        if (value.ValueKind != JsonValueKind.String) throw new InvalidDataException("candidate_string_required");
        var text = value.GetString()!;
        if (text.Length > 256 || text.Any(char.IsControl) || text.Contains("/subscriptions/", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("connectionstring", StringComparison.OrdinalIgnoreCase) || text.Contains("AccountKey=", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("candidate_private_string");
        return text.Replace(subscription, "[REDACTED_SUBSCRIPTION]", StringComparison.OrdinalIgnoreCase);
    }
    [GeneratedRegex("^[A-Za-z0-9_.-]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeCode();
}

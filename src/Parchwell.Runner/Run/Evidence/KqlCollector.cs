using System.Globalization;
using System.Text;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;

namespace Parchwell.Runner.Run.Evidence;

public sealed record KqlParameters(string WorkspaceId, string ResourceId, DateTimeOffset StartUtc, DateTimeOffset EndUtc);
public sealed record KqlReceipt(string Status, string Reason, int Rows, byte[] OriginalOutput, string Query, byte[]? OriginalError = null);

public sealed class KqlCollector(IProcessRunner process, TimeProvider clock)
{
    public const int MaxBytes = 16 * 1024 * 1024;
    public static KqlParameters ReadPrivateParameters(byte[] bytes) => JsonSerializer.Deserialize<KqlParameters>(bytes, LedgerJson.Options)
        ?? throw new InvalidDataException("kql_parameters_missing");

    public async Task<KqlReceipt> CollectAsync(KqlParameters parameters, string querySource, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(parameters.WorkspaceId, out _) || parameters.StartUtc.Offset != TimeSpan.Zero || parameters.EndUtc.Offset != TimeSpan.Zero ||
            parameters.StartUtc >= parameters.EndUtc || !parameters.ResourceId.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase) ||
            parameters.ResourceId.Length > 2048 || parameters.ResourceId.Any(char.IsControl) || querySource.Length > 65536)
            throw new InvalidDataException("kql_parameters_invalid");
        var query = "let start_utc=datetime(" + parameters.StartUtc.ToString("O", CultureInfo.InvariantCulture) + ");\n" +
            "let end_utc=datetime(" + parameters.EndUtc.ToString("O", CultureInfo.InvariantCulture) + ");\n" +
            "let resource_id=" + JsonSerializer.Serialize(parameters.ResourceId) + ";\n" + querySource;
        using var timer = new CancellationTokenSource(TimeSpan.FromSeconds(300), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        KqlReceipt receipt = new("unavailable", "query_failed", 0, [], query);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0) await Task.Delay(TimeSpan.FromSeconds(30), clock, deadline.Token).ConfigureAwait(false);
            var result = await process.RunAsync(new ProcessRequest("az", ["monitor", "log-analytics", "query", "--workspace", parameters.WorkspaceId,
                "--analytics-query", query, "--output", "json", "--only-show-errors"], TimeSpan.FromSeconds(60), MaxBytes), deadline.Token).ConfigureAwait(false);
            if (result.ExitCode != 0 || result.TimedOut)
            {
                receipt = new("unavailable", result.TimedOut ? "query_timed_out" : "query_failed", 0,
                    Encoding.UTF8.GetBytes(result.StandardOutput), query, Encoding.UTF8.GetBytes(result.StandardError));
                continue;
            }
            receipt = Parse(Encoding.UTF8.GetBytes(result.StandardOutput), query);
            if (receipt.Status == "available") return receipt;
        }
        return receipt;
    }

    // Azure CLI 74172d7 log-analytics/_custom.py flattens tables to a row array;
    // each row has TableName and string-valued columns, including numeric counts.
    public static KqlReceipt Parse(byte[] output, string query)
    {
        if (output.Length > MaxBytes) return new("incomplete", "query_byte_limit", 0, [], query);
        try
        {
            using var json = JsonDocument.Parse(output);
            if (json.RootElement.ValueKind != JsonValueKind.Array) return new("incomplete", "query_shape_invalid", 0, output, query);
            var rows = json.RootElement.GetArrayLength();
            if (rows > 10000) return new("incomplete", "query_row_limit", rows, output, query);
            if (rows == 0) return new("incomplete", "logs_absent_or_delayed", 0, output, query);
            foreach (var row in json.RootElement.EnumerateArray())
                if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("TableName", out var table) || table.ValueKind != JsonValueKind.String ||
                    row.EnumerateObject().Any(property => property.Value.ValueKind != JsonValueKind.String))
                    return new("incomplete", "query_shape_invalid", rows, output, query);
            return new("available", "corroborating_only", rows, output, query);
        }
        catch (JsonException) { return new("incomplete", "query_json_invalid", 0, output, query); }
    }
}

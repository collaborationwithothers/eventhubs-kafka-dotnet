using System.Text;
using System.Text.Json;

namespace Parchwell.Enrichment.SeedF04;

public sealed record SeedF04Error(int Code, bool IsFatal, string Origin, string Reason);
public sealed record SeedF04Result(bool Delivered, SeedF04Error? Error, int ExitCode, bool CleanupCompleted);

public static class SeedF04Receipt
{
    public const string Prefix = "PARCHWELL_F04_SEED_RECEIPT ";
    public const string ValueSha256 = "68fe9c9f499dd1a6bf07c6f737c2b6a8118cca37877336ee0f2674209554d3c6";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public static string CreateLine(SeedF04Options options, SeedF04Result result)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(result);
        options.Validate();
        if (!result.CleanupCompleted || result.ExitCode is < 0 or > 1 ||
            (result.Delivered && result.Error is not null) ||
            (result.ExitCode == 0 && !result.Delivered) ||
            (result.Error is not null && Encoding.UTF8.GetByteCount(result.Error.Reason) > 4096))
            throw new InvalidOperationException("seed_receipt_result_invalid");
        string line = Prefix + JsonSerializer.Serialize(new
        {
            SchemaVersion = 1, Mode = "single-action-seed", options.RunId, options.SourceCommit,
            options.JobName, options.PodName, options.PodUid, ServiceAccount = "enrichment-consumer",
            Topic = "document-state", Partition = 0,
            KeyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("f04-seed:" + options.RunId)),
            ValueSha256, result.Delivered, result.Error, HandlerExitCode = result.ExitCode, result.CleanupCompleted
        }, JsonOptions);
        if (Encoding.UTF8.GetByteCount(line) > options.MaxReceiptBytes)
            throw new InvalidOperationException("seed_receipt_too_large");
        return line;
    }
}

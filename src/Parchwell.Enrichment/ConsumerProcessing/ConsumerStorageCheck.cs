using System.Net.Sockets;
using System.Text.RegularExpressions;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace Parchwell.Enrichment.ConsumerProcessing;

// One write check as the enrichment-consumer identity. The workflow runs it before the consumer repeats
// (infra/scripts/phase/consumer_repeat.py) to learn whether a fresh role assignment on consumer-record has
// taken effect. It writes one probe blob and deletes it. The probe lives under <run>/<phase>/storage-check/,
// outside every <run>/<phase>/r<n>/consumer/ prefix that the readiness poll, the archive and the verifier read.
// The result line carries a status code, an error code or a socket error name, never an exception message.
internal static partial class ConsumerStorageCheck
{
    public const string Container = "consumer-record";
    public const string ResultPrefix = "consumer_storage_check ";

    internal sealed record Options(string RunId, string Phase, string JobName, string BlobEndpoint)
    {
        public string ProbeBlob => $"{RunId}/{Phase}/storage-check/{JobName}.probe";

        public static Options Read(IConfiguration configuration)
        {
            var options = new Options(configuration["ConsumerStorageCheck:RunId"] ?? string.Empty,
                configuration["ConsumerStorageCheck:Phase"] ?? string.Empty,
                configuration["ConsumerStorageCheck:JobName"] ?? string.Empty,
                configuration["ConsumerStorageCheck:BlobEndpoint"] ?? string.Empty);
            if (!Label(options.RunId, 29) || options.Phase is not ("C0" or "C1") || !Label(options.JobName, 63) ||
                !options.JobName.StartsWith(options.RunId + "-", StringComparison.Ordinal) ||
                !Uri.TryCreate(options.BlobEndpoint, UriKind.Absolute, out Uri? endpoint) || endpoint.Scheme != Uri.UriSchemeHttps ||
                !StringComparer.Ordinal.Equals(options.JobName, configuration["PARCHWELL_JOB_NAME"]))
                throw new InvalidOperationException("consumer_storage_check_configuration_invalid");
            return options;
        }

        private static bool Label(string value, int maximum) => value.Length > 0 && value.Length <= maximum &&
            char.IsAsciiLetterOrDigit(value[0]) && char.IsAsciiLetterOrDigit(value[^1]) &&
            value.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'z' or '-');
    }

    // Exit 0: the write succeeded and the probe was deleted. Exit 3: the write succeeded but the delete failed,
    // so access is ready and the probe remains. Exit 1: the write failed. Exit 2: the configuration is invalid.
    public static async Task<int> RunAsync(IConfiguration configuration, Func<Options, BlobContainerClient> containerFactory,
        TextWriter output, CancellationToken cancellationToken)
    {
        Options options;
        try { options = Options.Read(configuration); }
        catch (InvalidOperationException)
        {
            await output.WriteLineAsync(ResultPrefix + "result=configuration_invalid").ConfigureAwait(false);
            return 2;
        }
        BlobClient probe;
        try
        {
            probe = containerFactory(options).GetBlobClient(options.ProbeBlob);
            using var content = new MemoryStream("storage-check\n"u8.ToArray(), writable: false);
            await probe.UploadAsync(content, new Azure.Storage.Blobs.Models.BlobUploadOptions
            {
                Conditions = new Azure.Storage.Blobs.Models.BlobRequestConditions { IfNoneMatch = ETag.All }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await output.WriteLineAsync(ResultPrefix + "result=failed " + Summarize(exception)).ConfigureAwait(false);
            return 1;
        }
        try
        {
            await probe.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await output.WriteLineAsync(ResultPrefix + "result=probe_delete_failed " + Summarize(exception)).ConfigureAwait(false);
            return 3;
        }
        await output.WriteLineAsync(ResultPrefix + "result=ok").ConfigureAwait(false);
        return 0;
    }

    // kind=http with status and the storage error code; kind=aadsts with the AADSTS code; kind=socket with the
    // socket error name; otherwise kind=other. Messages are never copied.
    internal static string Summarize(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is RequestFailedException failed && failed.Status != 0)
                return $"kind=http status={failed.Status}" + (SafeCode(failed.ErrorCode) is { } code ? " code=" + code : string.Empty);
            if (current is SocketException socket)
                return "kind=socket code=" + socket.SocketErrorCode;
        }
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            // Azure.Identity puts the AADSTS code in the message; only the code is kept.
            Match match = AadstsCode().Match(current.Message);
            if (match.Success) return "kind=aadsts code=" + match.Value;
        }
        return "kind=other";
    }

    private static string? SafeCode(string? value) => value is { Length: > 0 and <= 64 } && value.All(char.IsAsciiLetterOrDigit) ? value : null;

    [GeneratedRegex("AADSTS[0-9]{1,8}", RegexOptions.CultureInvariant)]
    private static partial Regex AadstsCode();
}

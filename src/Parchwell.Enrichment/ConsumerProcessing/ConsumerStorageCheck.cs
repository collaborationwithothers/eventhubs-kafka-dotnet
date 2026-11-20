using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Parchwell.Enrichment.ConsumerProcessing;

// One write check as the enrichment-consumer identity. The workflow runs it before the consumer repeats
// (infra/scripts/phase/consumer_repeat.py) to learn whether a fresh role assignment on consumer-record has
// taken effect. It writes one probe blob and deletes it. The probe lives under <run>/<phase>/storage-check/,
// outside every <run>/<phase>/r<n>/consumer/ prefix that the readiness poll, the archive and the verifier read.
// A failure is reported through ExceptionSummary, which never copies an exception message.
internal static class ConsumerStorageCheck
{
    public const string Container = "consumer-record";
    public const string ResultPrefix = "consumer_storage_check ";

    internal sealed class Options
    {
        public const string SectionName = "ConsumerStorageCheck";
        public string RunId { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty;
        public string JobName { get; set; } = string.Empty;
        public string BlobEndpoint { get; set; } = string.Empty;
        public string ProbeBlob => $"{RunId}/{Phase}/storage-check/{JobName}.probe";

        public void Validate(IConfiguration configuration)
        {
            if (!Label(RunId, 29) || Phase is not ("C0" or "C1") || !Label(JobName, 63) ||
                !JobName.StartsWith(RunId + "-", StringComparison.Ordinal) ||
                !Uri.TryCreate(BlobEndpoint, UriKind.Absolute, out Uri? endpoint) || endpoint.Scheme != Uri.UriSchemeHttps ||
                !StringComparer.Ordinal.Equals(JobName, configuration["PARCHWELL_JOB_NAME"]))
                throw new InvalidOperationException("consumer_storage_check_configuration_invalid");
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
        try
        {
            HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
            builder.Configuration.AddConfiguration(configuration);
            builder.Services.AddOptions<Options>()
                .Bind(builder.Configuration.GetSection(Options.SectionName), binding => binding.ErrorOnUnknownConfiguration = true)
                .Validate(value => { value.Validate(configuration); return true; }).ValidateOnStart();
            using IHost host = builder.Build();
            await host.StartAsync(cancellationToken).ConfigureAwait(false);
            options = host.Services.GetRequiredService<IOptions<Options>>().Value;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await output.WriteLineAsync(ResultPrefix + "result=configuration_invalid").ConfigureAwait(false);
            return 2;
        }
        BlobClient probe;
        try
        {
            probe = containerFactory(options).GetBlobClient(options.ProbeBlob);
            using var content = new MemoryStream("storage-check\n"u8.ToArray(), writable: false);
            await probe.UploadAsync(content, new BlobUploadOptions
            {
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await output.WriteLineAsync(ResultPrefix + "result=failed cause=" + ExceptionSummary.Describe(exception)).ConfigureAwait(false);
            return 1;
        }
        try
        {
            await probe.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await output.WriteLineAsync(ResultPrefix + "result=probe_delete_failed cause=" + ExceptionSummary.Describe(exception)).ConfigureAwait(false);
            return 3;
        }
        await output.WriteLineAsync(ResultPrefix + "result=ok").ConfigureAwait(false);
        return 0;
    }
}

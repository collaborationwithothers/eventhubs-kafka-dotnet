#pragma warning disable CA1707
using System.Globalization;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Enrichment.ConsumerProcessing;
using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing;

public sealed class ConsumerProcessingHostTests
{
    [Fact]
    public async Task Given_U24_synthetic_token_file_When_host_records_effective_settings_Then_token_contents_are_not_exposed()
    {
        // This host integration fixture uses only a temporary synthetic token file.
        // Kafka and Blob boundaries stay in memory; no credentials are requested.
        string tokenPath = Path.Combine(Path.GetTempPath(), "consumer-token-" + Guid.NewGuid().ToString("N"));
        const string syntheticToken = "synthetic-token-file-content";
        using var harness = new ConsumerTestHarness();
        try
        {
            await File.WriteAllTextAsync(tokenPath, syntheticToken, Encoding.UTF8, TestContext.Current.CancellationToken);
            Dictionary<string, string?> configuration = HostConfiguration(harness.Options);
            configuration["AZURE_FEDERATED_TOKEN_FILE"] = tokenPath;
            configuration["AZURE_TENANT_ID"] = "00000000-0000-0000-0000-000000000001";
            configuration["AZURE_CLIENT_ID"] = "00000000-0000-0000-0000-000000000002";
            int result = await Program.RunAsync(["--consumer-processing"], builder =>
            {
                builder.Configuration.Sources.Clear();
                builder.Configuration.AddInMemoryCollection(configuration);
                builder.Services.AddSingleton<TimeProvider>(harness.Clock);
                builder.Services.AddSingleton<IKafkaNativeMonitoringStore>(new MemoryMonitoringStore());
                builder.Services.AddSingleton<IConsumerProcessingPlanSource>(new ConsumerTestHarness.PlanSource(harness.Plan));
                builder.Services.AddSingleton<Func<IConsumerRecordStore>>(_ => () => harness.Store);
                builder.Services.AddSingleton<ConsumerProcessingConsumerFactory>(_ => harness.CreateConsumer);
                builder.Services.AddSingleton<Action<int>>(_ => code => harness.ExitCodes.Add(code));
            });
            Assert.Equal(0, result);
            Assert.True(File.Exists(tokenPath));
            string contents = await File.ReadAllTextAsync(tokenPath, TestContext.Current.CancellationToken);
            Assert.Equal(syntheticToken, contents);
            JsonElement settings = harness.Store.Registration!.Value.GetProperty("effective_settings");
            Assert.DoesNotContain(settings.EnumerateObject(), property => property.Name == "sasl.oauthbearer.token");
            Assert.DoesNotContain(settings.EnumerateObject(), property => property.Value.GetString() == contents);
        }
        finally { File.Delete(tokenPath); }
    }

    internal static Dictionary<string, string?> HostConfiguration(ConsumerProcessingOptions options)
    {
        Dictionary<string, string?> values = typeof(ConsumerProcessingOptions).GetProperties()
            .Where(property => property.CanWrite)
            .ToDictionary(property => "ConsumerProcessing:" + property.Name,
                property => Convert.ToString(property.GetValue(options), CultureInfo.InvariantCulture), StringComparer.Ordinal);
        values["PARCHWELL_SOURCE_COMMIT"] = options.SourceCommit;
        values["PARCHWELL_JOB_NAME"] = options.JobName;
        values["PARCHWELL_POD_NAME"] = options.PodName;
        values["PARCHWELL_POD_UID"] = options.PodUid;
        values["PARCHWELL_NODE_NAME"] = options.NodeName;
        values["Telemetry:ConnectionString"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://localhost/";
        values["Telemetry:ServiceName"] = "Parchwell.Enrichment";
        values["Telemetry:ServiceInstanceId"] = options.PodName;
        values["Telemetry:ScenarioId"] = options.ScenarioId;
        values["Telemetry:RunId"] = options.RunId;
        values["NativeMonitoring:BlobEndpoint"] = "https://storage.example/";
        values["NativeMonitoring:BlobContainer"] = "native-monitoring";
        values["NativeMonitoring:RunId"] = options.RunId;
        values["NativeMonitoring:Phase"] = options.Phase;
        values["NativeMonitoring:ScenarioId"] = options.ScenarioId;
        values["NativeMonitoring:ConfigurationId"] = options.Phase;
        values["NativeMonitoring:JobName"] = options.JobName;
        values["NativeMonitoring:PodUid"] = options.PodUid;
        values["NativeMonitoring:SourceCommit"] = options.SourceCommit;
        return values;
    }
    private sealed class MemoryMonitoringStore : IKafkaNativeMonitoringStore
    {
        public Task CreateAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
#pragma warning restore CA1707

using Azure.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenTelemetry;
using OpenTelemetry.Logs;
using Parchwell.Kafka.Telemetry;
using Xunit;

namespace Parchwell.Kafka.Tests.Telemetry;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Collection("Parchwell telemetry")]
[Trait("Category", "Unit")]
public sealed class ParchwellTelemetryRegistrationTests
{
    [Fact]
    public void Given_NullDependencies_When_Registering_Then_RejectsBeforeExportConstruction()
    {
        var credential = new SyntheticCredential();
        Assert.Throws<ArgumentNullException>(() => ParchwellTelemetryServiceCollectionExtensions.AddParchwellTelemetry(
            null!, credential, TelemetryTestPipeline.Configure));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddParchwellTelemetry(
            null!, TelemetryTestPipeline.Configure));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddParchwellTelemetry(credential, null!));
        Assert.Empty(credential.Scopes);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void Given_BlankConnection_When_Registering_Then_RejectsOptions(string connection)
    {
        Assert.Throws<OptionsValidationException>(() => new ServiceCollection().AddParchwellTelemetry(
            new SyntheticCredential(), options =>
            {
                TelemetryTestPipeline.Configure(options);
                options.ConnectionString = connection;
            }));
    }

    [Theory]
    [InlineData("ServiceName")]
    [InlineData("ServiceInstanceId")]
    [InlineData("ScenarioId")]
    [InlineData("RunId")]
    public void Given_InvalidIdentity_When_Registering_Then_RejectsWithoutDisclosingConnection(string field)
    {
        foreach (string value in new[] { "", " ", "has space", "has\ttab", "has\u0001control" })
        {
            var exception = Assert.Throws<OptionsValidationException>(() => new ServiceCollection().AddParchwellTelemetry(
                new SyntheticCredential(), options =>
                {
                    TelemetryTestPipeline.Configure(options);
                    SetIdentity(options, field, value);
                }));
            Assert.DoesNotContain(TelemetryTestPipeline.ConnectionString, exception.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Given_ValidSnapshot_When_CallerMutatesAndOptionsResolve_Then_ConfigureRunsOnceAndIdentityStaysFixed()
    {
        int calls = 0;
        ParchwellTelemetryOptions? supplied = null;
        using var pipeline = new TelemetryTestPipeline(options =>
        {
            calls++;
            supplied = options;
            TelemetryTestPipeline.Configure(options);
        }, services =>
        {
            Assert.NotNull(supplied);
            supplied.ConnectionString = "mutated-connection";
            supplied.ServiceName = "mutated-service";
            supplied.ServiceInstanceId = "mutated-instance";
            supplied.ScenarioId = "mutated-scenario";
            supplied.RunId = "mutated-run";
            Assert.Throws<InvalidOperationException>(() => services.AddParchwellTelemetry(
                new SyntheticCredential(), _ => calls++));
        });

        var resolved = pipeline.Services.GetRequiredService<IOptions<ParchwellTelemetryOptions>>().Value;
        Assert.Equal(TelemetryTestPipeline.ConnectionString, resolved.ConnectionString);
        Assert.Equal("Parchwell.LoadGen", resolved.ServiceName);
        Assert.Equal("pod-test", resolved.ServiceInstanceId);
        Assert.Equal("A01", resolved.ScenarioId);
        Assert.Equal("run-test", resolved.RunId);
        pipeline.Services.GetRequiredService<IStartupValidator>().Validate();
        Assert.Equal(1, calls);
        pipeline.AssertResources();

        var logging = pipeline.Services.GetRequiredService<IOptions<OpenTelemetryLoggerOptions>>().Value;
        Assert.True(logging.IncludeScopes);
        Assert.True(logging.IncludeFormattedMessage);
        Assert.True(logging.ParseStateValues);
    }

    [Fact]
    public void Given_OptionsInvalidatedAfterRegistration_When_StartupValidates_Then_RejectsWithTheSameValidation()
    {
        using var pipeline = new TelemetryTestPipeline(afterRegistration: services =>
            services.PostConfigure<ParchwellTelemetryOptions>(options => options.RunId = "invalid run"));
        var exception = Assert.Throws<OptionsValidationException>(() =>
            pipeline.Services.GetRequiredService<IStartupValidator>().Validate());
        Assert.DoesNotContain(TelemetryTestPipeline.ConnectionString, exception.ToString(), StringComparison.Ordinal);
        pipeline.AssertResources();
    }

    [Fact]
    public void Given_ProductionConfiguration_When_AppliedToDistro_Then_UsesTheExplicitSignalAndExportPolicy()
    {
        var options = new MicrosoftOpenTelemetryOptions();
        var snapshot = new ParchwellTelemetryOptions();
        TelemetryTestPipeline.Configure(snapshot);
        var credential = new SyntheticCredential();
        using var handler = new RecordingTelemetryHandler();
        using var client = new HttpClient(handler);
        using var transport = new HttpClientTransport(client);
        ParchwellTelemetryServiceCollectionExtensions.ConfigureDistro(options, snapshot, credential, transport);

        Assert.Equal(ExportTarget.AzureMonitor, options.Exporters);
        Assert.Equal(TelemetryTestPipeline.ConnectionString, options.AzureMonitor.ConnectionString);
        Assert.Same(credential, options.AzureMonitor.Credential);
        Assert.Same(transport, options.AzureMonitor.Transport);
        Assert.Null(options.AzureMonitor.TracesPerSecond);
        Assert.Equal(1.0F, options.AzureMonitor.SamplingRatio);
        Assert.False(options.AzureMonitor.EnableTraceBasedLogsSampler);
        Assert.False(options.AzureMonitor.EnableLiveMetrics);
        Assert.True(options.AzureMonitor.EnableStandardMetrics);
        Assert.False(options.AzureMonitor.EnablePerfCounters);
        Assert.True(options.AzureMonitor.DisableOfflineStorage);
        Assert.True(options.Instrumentation.EnableTracing);
        Assert.True(options.Instrumentation.EnableMetrics);
        Assert.True(options.Instrumentation.EnableLogging);
        Assert.True(options.Instrumentation.EnableHttpClientInstrumentation);
        Assert.True(options.Instrumentation.EnableAzureSdkInstrumentation);
        Assert.False(options.Instrumentation.EnableAspNetCoreInstrumentation);
        Assert.False(options.Instrumentation.EnableSqlClientInstrumentation);
        Assert.False(options.Instrumentation.EnableOpenAIInstrumentation);
        Assert.False(options.Instrumentation.EnableSemanticKernelInstrumentation);
        Assert.False(options.Instrumentation.EnableAgentFrameworkInstrumentation);
        Assert.False(options.Instrumentation.EnableAgent365Instrumentation);
        Assert.Equal("Parchwell.Workers", ParchwellTelemetrySources.WorkerActivitySourceName);
        Assert.Equal("Parchwell.Workers", ParchwellTelemetrySources.WorkerMeterName);
    }

    [Theory]
    [InlineData("telemetry.invalid", false)]
    [InlineData("169.254.169.254", true)]
    [InlineData("unrelated.invalid", true)]
    [InlineData(null, true)]
    public void Given_SyntheticRequestHost_When_Classifying_Then_OnlyTheFakeEndpointIsAllowed(string? host, bool unexpected)
    {
        Assert.Equal(unexpected, HttpRequestGuard.IsUnexpectedHost(host));
    }

    private static void SetIdentity(ParchwellTelemetryOptions options, string field, string value)
    {
        switch (field)
        {
            case "ServiceName": options.ServiceName = value; break;
            case "ServiceInstanceId": options.ServiceInstanceId = value; break;
            case "ScenarioId": options.ScenarioId = value; break;
            case "RunId": options.RunId = value; break;
            default: throw new ArgumentException("Unknown fixture field.", nameof(field));
        }
    }
}

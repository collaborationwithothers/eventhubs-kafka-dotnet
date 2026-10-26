#pragma warning disable CA1707
using System.Reflection;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Enrichment.SeedF04;
using Xunit;

namespace Parchwell.Enrichment.Tests.SeedF04;

public sealed class SeedF04HostTests
{
    private static readonly string[] CompleteLifecycleOrder = ["produce", "flush", "dispose", "host-stop", "host-dispose", "receipt"];
    [Fact]
    public async Task Given_valid_seed_configuration_When_validation_mode_runs_Then_no_native_client_or_credentials_are_needed()
    {
        Assert.Equal(0, await Program.RunAsync(["--validate-seed-configuration"], Configure));
    }

    [Theory]
    [InlineData("RunId")]
    [InlineData("SourceCommit")]
    [InlineData("JobName")]
    [InlineData("PodName")]
    [InlineData("PodUid")]
    [InlineData("NamespaceName")]
    [InlineData("ActionWindowMs")]
    [InlineData("ProcessLifetimeSeconds")]
    [InlineData("CleanupSeconds")]
    [InlineData("HostCleanupSeconds")]
    [InlineData("MaxReceiptBytes")]
    public async Task Given_missing_option_When_validation_mode_runs_Then_configuration_fails(string missing)
    {
        Assert.Equal(1, await Program.RunAsync(["--validate-seed-configuration"], builder =>
        {
            Dictionary<string, string?> configuration = Configuration();
            configuration.Remove("SeedF04:" + missing);
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(configuration);
        }));
    }

    [Theory]
    [InlineData("ActionWindowMs", "329999")]
    [InlineData("ActionWindowMs", "330001")]
    [InlineData("ProcessLifetimeSeconds", "389")]
    [InlineData("ProcessLifetimeSeconds", "391")]
    [InlineData("CleanupSeconds", "9")]
    [InlineData("CleanupSeconds", "11")]
    [InlineData("HostCleanupSeconds", "9")]
    [InlineData("HostCleanupSeconds", "11")]
    [InlineData("MaxReceiptBytes", "65535")]
    [InlineData("MaxReceiptBytes", "65537")]
    [InlineData("UnknownOption", "true")]
    [InlineData("SourceCommit", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    [InlineData("SourceCommit", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("RunId", "wrong-run")]
    [InlineData("PodUid", "invalid")]
    [InlineData("NamespaceName", "https://invalid.example")]
    public async Task Given_invalid_option_When_validation_mode_runs_Then_configuration_fails(string option, string value)
    {
        Assert.Equal(1, await Program.RunAsync(["--validate-seed-configuration"], builder =>
        {
            Dictionary<string, string?> configuration = Configuration();
            configuration["SeedF04:" + option] = value;
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(configuration);
        }));
    }

    [Fact]
    public async Task Given_wrong_source_commit_When_live_seed_starts_Then_no_receipt_can_claim_success()
    {
        using var output = new StringWriter();
        Assert.Equal(1, await Program.RunAsync(["--single-action-seed"], builder =>
        {
            Dictionary<string, string?> configuration = Configuration();
            configuration["SeedF04:SourceCommit"] = new string('b', 40);
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(configuration);
        }, output));
        Assert.Equal(string.Empty, output.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("--single-action")]
    [InlineData("--unknown")]
    public async Task Given_unknown_mode_When_process_runs_Then_exit_two_has_no_receipt(string mode)
    {
        using var output = new StringWriter();
        Assert.Equal(2, await Program.RunAsync(mode.Length == 0 ? [] : [mode], output: output));
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public async Task Given_extra_argument_When_process_runs_Then_exit_two_has_no_receipt()
    {
        using var output = new StringWriter();
        Assert.Equal(2, await Program.RunAsync(["--image-smoke", "--single-action-seed"], output: output));
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public async Task Given_smoke_mode_When_process_runs_Then_native_version_is_loaded_without_a_delivery_receipt()
    {
        using var output = new StringWriter();
        Assert.Equal(0, await Program.RunAsync(["--image-smoke"], output: output));
        Assert.Equal("2.16.0", Library.VersionString);
        using JsonDocument marker = JsonDocument.Parse(output.ToString());
        Assert.Equal("single-action-seed-smoke", marker.RootElement.GetProperty("mode").GetString());
        Assert.True(marker.RootElement.GetProperty("native_loaded").GetBoolean());
        Assert.Equal(1654, marker.RootElement.GetProperty("uid_expected").GetInt32());
        Assert.Equal(3, marker.RootElement.EnumerateObject().Count());
        Assert.DoesNotContain(SeedF04Receipt.Prefix, output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_successful_delivery_When_host_stops_Then_receipt_is_last_after_client_and_host_cleanup()
    {
        var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, SeedF04Tests.RecordingProducer>();
        var recorded = (SeedF04Tests.RecordingProducer)(object)producer;
        var host = CreateHost(producer, recorded.Events);
        using var output = new RecordingWriter(recorded.Events);
        Assert.Equal(0, await Program.RunAsync(["--single-action-seed"], output: output, hostFactory: () => host));
        Assert.Equal(CompleteLifecycleOrder, recorded.Events);
        Assert.Single(output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("dispose")]
    [InlineData("client-dispose")]
    public async Task Given_cleanup_failure_When_process_finishes_Then_no_success_receipt_is_emitted(string failure)
    {
        var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, SeedF04Tests.RecordingProducer>();
        var recorded = (SeedF04Tests.RecordingProducer)(object)producer;
        recorded.FailDispose = failure == "client-dispose";
        var host = CreateHost(producer, recorded.Events, failure);
        using var output = new StringWriter();
        Assert.Equal(1, await Program.RunAsync(["--single-action-seed"], output: output, hostFactory: () => host));
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public async Task Given_failed_delivery_When_host_cleans_up_Then_one_diagnostic_receipt_records_failure()
    {
        var producer = DispatchProxy.Create<IProducer<byte[], byte[]>, SeedF04Tests.RecordingProducer>();
        var recorded = (SeedF04Tests.RecordingProducer)(object)producer;
        recorded.DeliveryError = new Error(ErrorCode.TopicAuthorizationFailed);
        var host = CreateHost(producer, recorded.Events);
        using var output = new StringWriter();
        Assert.Equal(1, await Program.RunAsync(["--single-action-seed"], output: output, hostFactory: () => host));
        using JsonDocument receipt = JsonDocument.Parse(output.ToString()[SeedF04Receipt.Prefix.Length..]);
        Assert.False(receipt.RootElement.GetProperty("delivered").GetBoolean());
        Assert.Equal(1, receipt.RootElement.GetProperty("handler_exit_code").GetInt32());
        Assert.True(receipt.RootElement.GetProperty("cleanup_completed").GetBoolean());
        Assert.Equal((int)ErrorCode.TopicAuthorizationFailed, receipt.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }

    private static void Configure(HostApplicationBuilder builder)
    {
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(Configuration());
    }

    private static Dictionary<string, string?> Configuration() => new(StringComparer.Ordinal)
    {
        ["SeedF04:RunId"] = "synthetic-run", ["SeedF04:SourceCommit"] = new string('a', 40),
        ["SeedF04:JobName"] = "synthetic-run-f04-seed", ["SeedF04:PodName"] = "synthetic-run-f04-seed-abcde",
        ["SeedF04:PodUid"] = "11111111-2222-3333-4444-555555555555", ["SeedF04:NamespaceName"] = "synthetic-eh",
        ["SeedF04:ActionWindowMs"] = "330000", ["SeedF04:ProcessLifetimeSeconds"] = "390", ["SeedF04:CleanupSeconds"] = "10",
        ["SeedF04:HostCleanupSeconds"] = "10", ["SeedF04:MaxReceiptBytes"] = "65536",
        ["PARCHWELL_SOURCE_COMMIT"] = new string('a', 40), ["PARCHWELL_JOB_NAME"] = "synthetic-run-f04-seed",
        ["PARCHWELL_POD_NAME"] = "synthetic-run-f04-seed-abcde", ["PARCHWELL_POD_UID"] = "11111111-2222-3333-4444-555555555555"
    };

    private static RecordingHost CreateHost(IProducer<byte[], byte[]> producer, List<string> events, string? failure = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IOptions<SeedF04Options>>(Options.Create(SeedF04Tests.ValidOptions()));
        builder.Services.AddSingleton<Func<IProducer<byte[], byte[]>>>(() => producer);
        builder.Services.AddSingleton<SeedF04Worker>();
        return new RecordingHost(builder.Build(), events, failure);
    }

    private sealed class RecordingWriter(List<string> events) : StringWriter
    {
        public override Task WriteLineAsync(string? value)
        {
            events.Add("receipt");
            return base.WriteLineAsync(value);
        }
    }

    private sealed class RecordingHost(IHost inner, List<string> events, string? failure) : IHost
    {
        public IServiceProvider Services => inner.Services;
        public Task StartAsync(CancellationToken cancellationToken = default) => inner.StartAsync(cancellationToken);
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            events.Add("host-stop");
            return failure == "stop" ? Task.FromException(new InvalidOperationException("synthetic_stop_failure")) : inner.StopAsync(cancellationToken);
        }
        public void Dispose()
        {
            events.Add("host-dispose");
            inner.Dispose();
            if (failure == "dispose") throw new InvalidOperationException("synthetic_host_dispose_failure");
        }
    }
}
#pragma warning restore CA1707

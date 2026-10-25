using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Diagnostics.Metrics;
using Parchwell.Verifier;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Judge;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

[Collection("VerifierEnvironment")]
#pragma warning disable CA1707
public sealed class TelemetryWiringTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("handler-exception")]
    [InlineData("cancel")]
    public async Task GivenHandlerRunsForMeasuredTime_WhenWorkerTerminates_ThenDurationUsesInjectedClock(string terminal)
    {
        var clock = new AdvancingTimeProvider();
        double? duration = null;
        string? outcome = null;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == Parchwell.Kafka.Telemetry.ParchwellTelemetrySources.WorkerMeterName)
                current.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name != "parchwell.worker.duration_ms") return;
            duration = measurement;
            outcome = tags.ToArray().Single(tag => tag.Key == "outcome").Value?.ToString();
        });
        listener.Start();

        int exit = await Program.RunAsync(VerifierHostTests.ArgumentsFor("probe-gate", "unused"), services =>
        {
            services.RemoveAll<IHostedService>();
            foreach (var descriptor in services.Where(item => item.ServiceType.Name is
                "IConfigureTracerProviderBuilder" or "IConfigureMeterProviderBuilder" or "IConfigureLoggerProviderBuilder").ToArray())
                services.Remove(descriptor);
            services.RemoveAll<IVerifierModeHandler>();
            services.AddSingleton<IVerifierModeHandler>(provider => new Handler(
                terminal, clock, provider.GetRequiredService<IHostApplicationLifetime>()));
        }, clock).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(terminal == "success" ? 0 : 1, exit);
        Assert.Equal(25d, duration);
        Assert.Equal(terminal == "success" ? "success" : "error", outcome);
    }

    [Fact]
    public void GivenValidJudgeTelemetryIdentity_WhenHostIsBuilt_ThenRegistersJudgeWithoutKafkaClient()
    {
        using IHost host=Program.BuildHost(Arguments("Parchwell.Verifier"));
        Assert.NotNull(host.Services.GetRequiredService<IVerifierModeHandler>() as JudgeHandler);
        Assert.Null(host.Services.GetService<VerifierKafkaSessionFactory>());
    }

    [Fact]
    public void GivenWrongTelemetryServiceName_WhenJudgeHostIsBuilt_ThenFailsBeforeIo()
    {
        Assert.Throws<InvalidOperationException>(()=>Program.BuildHost(Arguments("Other.Service")));
    }

    [Fact]
    public void GivenJudgeWithoutPodIdentity_WhenHostIsBuilt_ThenFailsBeforeWritingCandidate()
    {
        string[] args=Arguments("Parchwell.Verifier").Where(x=>!x.StartsWith("--PARCHWELL_POD_NAME=",StringComparison.Ordinal)).ToArray();
        Assert.Throws<OptionsValidationException>(()=>Program.BuildHost(args));
    }

    private static string[] Arguments(string serviceName)=>[
        "--Verifier:Mode=judge","--Verifier:ScenarioId=A01","--Verifier:VerifierCommit="+new string('a',40),
        "--Verifier:SourceCommit="+new string('a',40),"--Verifier:ProcessLifetime=00:05:00","--Verifier:CleanupTimeout=00:00:10",
        "--Verifier:MaxOutputBytes=16777216","--Verifier:MaxDiagnosticsPerCheck=100","--Verifier:MaxDiagnosticBytes=65536",
        "--Verifier:ConfigurationId=C0","--Verifier:RunId=synthetic-c5","--Verifier:Phase=C0","--Verifier:JobName=synthetic-c5-c0-judge",
        "--Verifier:CandidateOutputName=synthetic-c5/C0/candidate.json","--PARCHWELL_POD_NAME=worker-pod","--Verifier:TenantId=tenant","--Verifier:WorkloadIdentityClientId=client",
        "--Verifier:FederatedTokenFilePath=/tmp/token","--Verifier:BlobServiceUri=https://account.blob.core.windows.net/",
        "--Verifier:VerifierContainer=verifier","--Verifier:LedgerContainer=ledger","--Verifier:IoTimeout=00:00:10",
        "--Verifier:LoadGenImage=loadgen:tag@sha256:"+new string('b',64),"--Verifier:VerifierImage=verifier:tag@sha256:"+new string('c',64),
        "--Verifier:MaxArtifactBytes=16777216","--Verifier:MaxTotalInputBytes=83886080",
        "--Verifier:ScenarioLoadOptions:MaxDocumentCharacters=16384","--Verifier:ScenarioLoadOptions:MaxNestingDepth=32",
        "--Verifier:ScenarioLoadOptions:MaxScalarCharacters=2048","--Verifier:ScenarioLoadOptions:MaxParserEvents=2000",
        "--Telemetry:ConnectionString=InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://localhost/",
        "--Telemetry:ServiceName="+serviceName,"--Telemetry:ServiceInstanceId=worker-pod","--Telemetry:ScenarioId=A01","--Telemetry:RunId=synthetic-c5"
    ];

    private sealed class AdvancingTimeProvider : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Volatile.Read(ref _timestamp);
        public void Advance(TimeSpan elapsed) => Interlocked.Add(ref _timestamp, (long)elapsed.TotalMilliseconds);
    }

    private sealed class Handler(
        string terminal,
        AdvancingTimeProvider clock,
        IHostApplicationLifetime lifetime) : IVerifierModeHandler
    {
        public VerifierMode Mode => VerifierMode.ProbeGate;
        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            clock.Advance(TimeSpan.FromMilliseconds(25));
            if (terminal == "handler-exception") throw new IOException("synthetic timed failure");
            if (terminal == "cancel")
            {
                lifetime.StopApplication();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            return 0;
        }
    }
}
#pragma warning restore CA1707

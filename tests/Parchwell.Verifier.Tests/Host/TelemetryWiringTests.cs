using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Verifier;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Judge;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707
public sealed class TelemetryWiringTests
{
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
}
#pragma warning restore CA1707

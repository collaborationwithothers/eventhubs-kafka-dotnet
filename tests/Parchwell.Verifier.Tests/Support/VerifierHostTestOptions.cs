namespace Parchwell.Verifier.Tests.Support;

internal static class VerifierHostTestOptions
{
    internal static VerifierHostOptions ValidOptions(string mode) => new()
    {
        Mode = mode,
        ScenarioId = "A01",
        VerifierCommit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        SourceCommit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        ProcessLifetime = TimeSpan.FromSeconds(10),
        CleanupTimeout = TimeSpan.FromSeconds(1),
        IoTimeout = TimeSpan.FromSeconds(1),
        MaxOutputBytes = 16_384,
        MaxDiagnosticsPerCheck = 4,
        MaxDiagnosticBytes = 512,
        ConfigurationId = "C0",
        RunId = "run-1",
        Phase = "C0",
        Repeat = 1,
        JobName = mode switch { "start-line" => "run-1-c0-r1-start-line", "verify" => "run-1-c0-r1-verify", _ => null },
        RepeatPrefix = "run-1/C0/r1/",
        PhasePrefix = "run-1/C0/",
        InstanceId = "run-1-c0-r1-loadgen",
        ExpectedInstanceId = "run-1-c0-r1-loadgen",
        ClientId = "run-1-c0-r1-loadgen",
        PodName = "verifier-pod",
        PodUid = "verifier-uid",
        VerifierClientId = "verifier-client",
        NamespaceName = "namespace-example",
        BlobServiceUri = "https://storage.example/",
        VerifierContainer = "verifier-output",
        TokenTimeout = TimeSpan.FromSeconds(1),
        MetadataTimeout = TimeSpan.FromSeconds(1),
        WatermarkTimeout = TimeSpan.FromSeconds(1),
        PollSlice = TimeSpan.FromMilliseconds(100),
        BlobIoTimeout = TimeSpan.FromSeconds(1),
        TenantId = "tenant-id",
        WorkloadIdentityClientId = "workload-client-id",
        FederatedTokenFilePath = "/tmp/token",
        StartLineOutputName = "run-1/C0/r1/start-line.json",
        BrokerInventoryOutputName = "run-1/C0/r1/broker-inventory.json",
        MaxPartitions = 8,
        MaxInputDepth = 16,
        LedgerContainer = "ledger",
        ArtifactPrefix = "run-1/C0/r1/producer/",
        StartLineInputName = "run-1/C0/r1/start-line.json",
        ResultOutputName = "run-1/C0/r1/result.json",
        ScenarioRoot = "/tmp/scenarios",
        ExpectedBrokerNames = null,
        SafetyStop = TimeSpan.FromSeconds(2),
        MaxArtifactBytes = 1_048_576,
        MaxTotalInputBytes = 4_194_304,
        MaxRowBytes = 65_536,
        MaxRowsPerArtifact = 1_000,
        MaxInventoryEntries = 1_000,
        MaxInputEntries = 1_000,
        MaxNativeReportBytes = 65_536,
        MaxBrokerEntries = 16,
        MaxConsumedRecords = 1000,
        MaxConsumedBytes = 1048576,
        ScenarioLoadOptions = new VerifierScenarioLoadOptions
        {
            MaxDocumentCharacters = 16_384,
            MaxNestingDepth = 32,
            MaxScalarCharacters = 2_048,
            MaxParserEvents = 2_000
        },
        GateOutputPath = TempPath(),
        ExpectedRepeats = Enumerable.Range(0, 5).Select(index => new ExpectedProbeRepeat
        {
            ScenarioId = "A01",
            ConfigurationId = "C0",
            RunId = $"probe-{index}",
            InstanceId = $"instance-{index}",
            ExpectedInstanceId = $"instance-{index}",
            ClientId = "loadgen-producer"
        }).ToList(),
        RepeatResultPaths = Enumerable.Range(0, 5).Select(index => $"probe-{index}.json").ToList()
    };

    internal static string[] ArgumentsFor(string mode, string outputPath)
    {
        var args = new List<string>
        {
            $"--PARCHWELL_SOURCE_COMMIT={(mode == "probe-gate" ? "test-commit" : "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")}",
            $"--Verifier:Mode={mode}",
            "--Verifier:ScenarioId=A01",
            $"--Verifier:VerifierCommit={(mode == "probe-gate" ? "test-commit" : "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")}",
            "--Verifier:LedgerContainer=ledger",
            "--Telemetry:ConnectionString=InstrumentationKey=synthetic-key",
            "--Telemetry:ServiceName=Parchwell.Verifier",
            "--Telemetry:ServiceInstanceId=verifier-pod",
            "--Telemetry:ScenarioId=A01",
            "--Telemetry:RunId=run-1",
            "--Verifier:ProcessLifetime=00:00:10",
            "--Verifier:CleanupTimeout=00:00:01",
            "--Verifier:IoTimeout=00:00:01",
            "--Verifier:MaxOutputBytes=16384",
            "--Verifier:MaxDiagnosticsPerCheck=4",
            "--Verifier:MaxDiagnosticBytes=512"
        };

        if (mode is "start-line" or "verify")
        {
            args.AddRange(
            [
                "--Verifier:ConfigurationId=C0",
                "--Verifier:RunId=run-1",
                "--Verifier:Phase=C0",
                "--Verifier:Repeat=1",
                $"--Verifier:JobName={(mode == "start-line" ? "run-1-c0-r1-start-line" : "run-1-c0-r1-verify")}",
                "--Verifier:RepeatPrefix=run-1/C0/r1/",
                "--Verifier:PhasePrefix=run-1/C0/",
                "--Verifier:InstanceId=run-1-c0-r1-loadgen",
                "--Verifier:ExpectedInstanceId=run-1-c0-r1-loadgen",
                "--Verifier:ClientId=run-1-c0-r1-loadgen",
                "--Verifier:PodName=verifier-pod",
                "--Verifier:PodUid=verifier-uid",
                "--Verifier:VerifierClientId=verifier-client",
                "--Verifier:NamespaceName=namespace-example",
                "--Verifier:BlobServiceUri=https://storage.example/",
                "--Verifier:VerifierContainer=verifier-output",
                "--Verifier:TokenTimeout=00:00:01",
                "--Verifier:MetadataTimeout=00:00:01",
                "--Verifier:WatermarkTimeout=00:00:01",
                "--Verifier:PollSlice=00:00:00.100",
                "--Verifier:BlobIoTimeout=00:00:01",
                "--Verifier:TenantId=tenant-id",
                "--Verifier:WorkloadIdentityClientId=workload-client-id",
                "--Verifier:FederatedTokenFilePath=/tmp/token"
            ]);
        }

        if (mode == "start-line")
        {
            args.Add("--Verifier:StartLineOutputName=run-1/C0/r1/start-line.json");
            args.Add("--Verifier:BrokerInventoryOutputName=run-1/C0/r1/broker-inventory.json");
            args.Add("--Verifier:MaxPartitions=8");
            args.Add("--Verifier:MaxInputDepth=16");
        }
        else if (mode == "verify")
        {
            args.AddRange(
            [
                "--Verifier:LedgerContainer=ledger",
                "--Verifier:ArtifactPrefix=run-1/C0/r1/producer/",
                "--Verifier:StartLineInputName=run-1/C0/r1/start-line.json",
                "--Verifier:ResultOutputName=run-1/C0/r1/result.json",
                "--Verifier:ScenarioRoot=/tmp/scenarios",
                "--Verifier:SafetyStop=00:00:02",
                "--Verifier:MaxArtifactBytes=1048576",
                "--Verifier:MaxTotalInputBytes=4194304",
                "--Verifier:MaxRowBytes=65536",
                "--Verifier:MaxRowsPerArtifact=1000",
                "--Verifier:MaxInventoryEntries=1000",
                "--Verifier:MaxInputEntries=1000",
                "--Verifier:MaxPartitions=8",
                "--Verifier:MaxInputDepth=16",
                "--Verifier:MaxNativeReportBytes=65536",
                "--Verifier:MaxBrokerEntries=16",
                "--Verifier:MaxConsumedRecords=1000",
                "--Verifier:MaxConsumedBytes=1048576"
            ]);
            AddScenarioLoadOptions(args);
        }
        else
        {
            args.AddRange(
            [
                $"--Verifier:GateOutputPath={outputPath}",
                "--Verifier:ScenarioRoot=/tmp/scenarios",
                "--Verifier:MaxArtifactBytes=1048576",
                "--Verifier:MaxTotalInputBytes=4194304",
                "--Verifier:MaxInputDepth=16"
            ]);
            AddScenarioLoadOptions(args);
            for (var index = 0; index < 5; index++)
            {
                args.Add($"--Verifier:ExpectedRepeats:{index}:ScenarioId=A01");
                args.Add($"--Verifier:ExpectedRepeats:{index}:ConfigurationId=C0");
                args.Add($"--Verifier:ExpectedRepeats:{index}:RunId=probe-{index}");
                args.Add($"--Verifier:ExpectedRepeats:{index}:InstanceId=instance-{index}");
                args.Add($"--Verifier:ExpectedRepeats:{index}:ExpectedInstanceId=instance-{index}");
                args.Add($"--Verifier:ExpectedRepeats:{index}:ClientId=loadgen-producer");
                args.Add($"--Verifier:RepeatResultPaths:{index}=probe-{index}.json");
            }
        }

        return args.ToArray();
    }

    private static void AddScenarioLoadOptions(List<string> args)
    {
        args.Add("--Verifier:ScenarioLoadOptions:MaxDocumentCharacters=16384");
        args.Add("--Verifier:ScenarioLoadOptions:MaxNestingDepth=32");
        args.Add("--Verifier:ScenarioLoadOptions:MaxScalarCharacters=2048");
        args.Add("--Verifier:ScenarioLoadOptions:MaxParserEvents=2000");
    }

    internal static string TempPath() => Path.Combine(Path.GetTempPath(), "verifier-host-" + Guid.NewGuid().ToString("N") + ".json");
}

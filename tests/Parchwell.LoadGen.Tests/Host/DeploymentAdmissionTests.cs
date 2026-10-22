using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Parchwell.LoadGen.Run.Input;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class DeploymentAdmissionTests : IDisposable
{
    private static readonly string[] BrokerNames = ["sasl_ssl://broker.example.invalid:9093/0"];
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "admission-" + Guid.NewGuid().ToString("N"));

    public DeploymentAdmissionTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task Given_ImageSmokeFlag_When_StartingProcess_Then_LoadsNativeLibraryAndRegularPackagedFilesWithoutHostComposition()
    {
        bool hostComposed = false;
        Assert.Equal(0, await Program.RunAsync(["--image-smoke"], _ => hostComposed = true));
        Assert.False(hostComposed);
    }

    [Fact]
    public async Task Given_NoAdmissionPath_When_Waiting_Then_ExistingCallersProceed()
    {
        Assert.Null(await DeploymentAdmission.WaitAsync(new ConfigurationBuilder().Build(), TimeProvider.System,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(65535)]
    [InlineData(65536)]
    public async Task Given_ReleaseWithinByteBound_When_Waiting_Then_IdentityAndIndependentBrokersOverridePreAdmissionInputs(int size)
    {
        IConfiguration configuration = CreateConfiguration();
        string release = Release();
        File.WriteAllText(AdmissionPath, release.PadRight(size), new UTF8Encoding(false));
        IReadOnlyDictionary<string, string?>? values = await DeploymentAdmission.WaitAsync(configuration, TimeProvider.System,
            TestContext.Current.CancellationToken);
        Assert.NotNull(values);
        Assert.Equal("producer-pod", values["LoadGen:ExpectedInstanceId"]);
        Assert.Equal("producer-pod", values["LoadGen:ActualInstanceId"]);
        Assert.Equal("producer-pod", values["LoadGen:PodName"]);
        Assert.Equal("producer-pod", values["LoadGen:ClientId"]);
        Assert.Equal("sasl_ssl://broker.example.invalid:9093/0", values["LoadGen:ExpectedBrokerNamesCsv"]);
    }

    [Theory]
    [InlineData("oversize")]
    [InlineData("run")]
    [InlineData("name")]
    [InlineData("uid")]
    [InlineData("hash")]
    [InlineData("missing-values")]
    [InlineData("schema")]
    [InlineData("empty-brokers")]
    [InlineData("duplicate-brokers")]
    [InlineData("malformed")]
    [InlineData("release-type")]
    public async Task Given_InvalidRelease_When_Waiting_Then_AdmissionFails(string invalid)
    {
        IConfiguration configuration = CreateConfiguration();
        string release = Release();
        release = invalid switch
        {
            "oversize" => release.PadRight(65537),
            "run" => release.Replace("run-1", "run-2", StringComparison.Ordinal),
            "name" => release.Replace("producer-pod", "other-pod", StringComparison.Ordinal),
            "uid" => release.Replace("pod-uid-1", "pod-uid-2", StringComparison.Ordinal),
            "hash" => release.Replace(Hash(), new string('0', 64), StringComparison.Ordinal),
            "schema" => release.Replace("\"schema_version\":1", "\"schema_version\":2", StringComparison.Ordinal),
            "empty-brokers" => release.Replace("[\"sasl_ssl://broker.example.invalid:9093/0\"]", "[]", StringComparison.Ordinal),
            "duplicate-brokers" => release.Replace("[\"sasl_ssl://broker.example.invalid:9093/0\"]", "[\"a\",\"a\"]", StringComparison.Ordinal),
            "malformed" => "{",
            "release-type" => release.Replace("\"release\":true", "\"release\":\"true\"", StringComparison.Ordinal),
            _ => release
        };
        File.WriteAllText(AdmissionPath, release);
        if (invalid == "missing-values") File.Delete(ValuesPath);
        await Assert.ThrowsAnyAsync<Exception>(() => DeploymentAdmission.WaitAsync(configuration, TimeProvider.System,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("initial")]
    [InlineData("unreleased")]
    public async Task Given_NoRelease_When_DeadlineExpires_Then_FailsWithoutConstructingHost(string state)
    {
        IConfiguration configuration = CreateConfiguration();
        if (state != "missing") File.WriteAllText(AdmissionPath, state switch
        {
            "empty" => string.Empty,
            "initial" => "{}",
            _ => "{\"release\":false}"
        });
        var clock = new ManualTimeProvider();
        Task<IReadOnlyDictionary<string, string?>?> waiting = DeploymentAdmission.WaitAsync(configuration, clock,
            TestContext.Current.CancellationToken);
        await clock.PollCreated.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(TimeSpan.FromSeconds(180), clock.Timers[0].Due);
        Assert.Equal(TimeSpan.FromSeconds(1), clock.Timers[1].Due);
        clock.Timers[0].Fire();
        await Assert.ThrowsAsync<TimeoutException>(() => waiting);
    }

    [Fact]
    public async Task Given_Cancellation_When_WaitingForRelease_Then_DoesNotRelease()
    {
        IConfiguration configuration = CreateConfiguration();
        var clock = new ManualTimeProvider();
        using var cancellation = new CancellationTokenSource();
        Task<IReadOnlyDictionary<string, string?>?> waiting = DeploymentAdmission.WaitAsync(configuration, clock, cancellation.Token);
        await clock.PollCreated.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }

    [Fact]
    public async Task Given_MismatchedDeploymentIdentity_When_StartingProcess_Then_HostCompositionNeverRuns()
    {
        IConfiguration configuration = CreateConfiguration();
        File.WriteAllText(AdmissionPath, Release().Replace("pod-uid-1", "other-uid", StringComparison.Ordinal));
        bool hostComposed = false;
        string[] args = configuration.AsEnumerable().Where(pair => pair.Value is not null)
            .Select(pair => "--" + pair.Key + "=" + pair.Value).ToArray();
        using var receipt = new StringWriter();
        int code = await Program.RunAsync(args, _ => hostComposed = true, receipt);
        Assert.Equal(1, code);
        Assert.False(hostComposed);
        Assert.Equal(string.Empty, receipt.ToString());
    }

    [Fact]
    public async Task Given_ProjectedDeploymentSymlinks_When_Released_Then_OnlyDeploymentInputsAllowLinks()
    {
        IConfiguration configuration = CreateConfiguration();
        File.WriteAllText(Path.Combine(_directory, "release-target.json"), Release());
        File.CreateSymbolicLink(AdmissionPath, Path.Combine(_directory, "release-target.json"));
        File.Move(ValuesPath, Path.Combine(_directory, "values-target.yaml"));
        File.CreateSymbolicLink(ValuesPath, Path.Combine(_directory, "values-target.yaml"));
        Assert.NotNull(await DeploymentAdmission.WaitAsync(configuration, TimeProvider.System, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_PendingProjectedGeneration_When_FinalComparisonAndPermitArriveAtomically_Then_WaitingWorkerAcceptsMatchingPodAndBrokers()
    {
        IConfiguration configuration = CreateConfiguration();
        string initial = Path.Combine(_directory, "..initial");
        string final = Path.Combine(_directory, "..final");
        Directory.CreateDirectory(initial);
        Directory.CreateDirectory(final);
        File.Move(ValuesPath, Path.Combine(initial, "generated-values.yaml"));
        File.WriteAllText(Path.Combine(initial, "generated-values.yaml"), "comparison for run-1-pending\n", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(initial, "admission.json"), "{}");
        string dataPath = Path.Combine(_directory, "..data");
        Directory.CreateSymbolicLink(dataPath, "..initial");
        File.CreateSymbolicLink(ValuesPath, Path.Combine("..data", "generated-values.yaml"));
        File.CreateSymbolicLink(AdmissionPath, Path.Combine("..data", "admission.json"));
        string pendingHash = Hash();
        byte[] finalValues = Encoding.UTF8.GetBytes("comparison for producer-pod\n");
        string finalHash = Convert.ToHexStringLower(SHA256.HashData(finalValues));
        File.WriteAllBytes(Path.Combine(final, "generated-values.yaml"), finalValues);
        File.WriteAllText(Path.Combine(final, "admission.json"), Release().Replace(pendingHash, finalHash, StringComparison.Ordinal));
        Assert.NotEqual(pendingHash, finalHash);

        var clock = new ManualTimeProvider();
        Task<IReadOnlyDictionary<string, string?>?> waiting = DeploymentAdmission.WaitAsync(configuration, clock,
            TestContext.Current.CancellationToken);
        await clock.PollCreated.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.False(waiting.IsCompleted);
        Assert.Equal("{}", File.ReadAllText(AdmissionPath));
        Assert.Equal(pendingHash, Hash());

        string replacement = Path.Combine(_directory, "..data_tmp");
        Directory.CreateSymbolicLink(replacement, "..final");
        using var rename = new System.Diagnostics.Process
        {
            StartInfo = new("mv")
            {
                UseShellExecute = false,
                ArgumentList = { "--force", "--no-target-directory", replacement, dataPath }
            }
        };
        Assert.True(rename.Start());
        await rename.WaitForExitAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(0, rename.ExitCode);
        Assert.Equal(Path.Combine("..data", "generated-values.yaml"), new FileInfo(ValuesPath).LinkTarget);
        Assert.Equal(Path.Combine("..data", "admission.json"), new FileInfo(AdmissionPath).LinkTarget);
        Assert.Equal(finalValues, File.ReadAllBytes(ValuesPath));
        using (JsonDocument permit = JsonDocument.Parse(File.ReadAllBytes(AdmissionPath)))
        {
            Assert.True(permit.RootElement.GetProperty("release").GetBoolean());
            Assert.Equal(Hash(), permit.RootElement.GetProperty("generated_values_sha256").GetString());
            Assert.NotEqual(pendingHash, permit.RootElement.GetProperty("generated_values_sha256").GetString());
        }
        clock.Timers[1].Fire();

        IReadOnlyDictionary<string, string?>? values = await waiting.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.NotNull(values);
        Assert.Equal("producer-pod", values["LoadGen:ExpectedInstanceId"]);
        Assert.Equal("producer-pod", values["LoadGen:ActualInstanceId"]);
        Assert.Equal("producer-pod", values["LoadGen:PodName"]);
        Assert.Equal("producer-pod", values["LoadGen:ClientId"]);
        Assert.Equal("sasl_ssl://broker.example.invalid:9093/0", values["LoadGen:ExpectedBrokerNamesCsv"]);
    }

    private IConfiguration CreateConfiguration()
    {
        File.WriteAllText(ValuesPath, "comparison\n", new UTF8Encoding(false));
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PARCHWELL_ADMISSION_FILE"] = AdmissionPath, ["PARCHWELL_GENERATED_VALUES_FILE"] = ValuesPath,
            ["PARCHWELL_POD_NAME"] = "producer-pod", ["PARCHWELL_POD_UID"] = "pod-uid-1", ["LoadGen:RunId"] = "run-1"
        }).Build();
    }

    private string Release() => JsonSerializer.Serialize(new
    {
        schema_version = 1, run_id = "run-1", pod_name = "producer-pod", pod_uid = "pod-uid-1",
        expected_broker_names = BrokerNames,
        generated_values_sha256 = Hash(), release = true
    });
    private string Hash() => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(ValuesPath)));
    private string AdmissionPath => Path.Combine(_directory, "admission.json");
    private string ValuesPath => Path.Combine(_directory, "generated-values.yaml");
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private sealed class ManualTimeProvider : TimeProvider
    {
        internal List<ManualTimer> Timers { get; } = [];
        internal TaskCompletionSource PollCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, dueTime);
            Timers.Add(timer);
            if (Timers.Count == 2) PollCreated.TrySetResult();
            return timer;
        }
    }
    private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan due) : ITimer
    {
        internal TimeSpan Due { get; } = due;
        internal void Fire() => callback(state);
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
#pragma warning restore CA1707

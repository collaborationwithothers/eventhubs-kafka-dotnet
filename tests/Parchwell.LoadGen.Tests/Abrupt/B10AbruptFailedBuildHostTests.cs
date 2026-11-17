using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
[Collection("B10AbruptEnvironment")]
public sealed class B10AbruptFailedBuildHostTests
{
    [Fact]
    public async Task Given_FailedNativeBuildAndBlockedMonitoringStore_When_ChildRuns_Then_OriginalNoKillOutcomeReturnsWithoutPublishing()
    {
        using var fixture = new B10AbruptProcessFixture("unused");
        var environment = new Dictionary<string, string>(fixture.EnvironmentValues, StringComparer.Ordinal)
        {
            ["B10Abrupt__Role"] = "child",
            ["Telemetry__ConnectionString"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://127.0.0.1:1/;LiveEndpoint=https://127.0.0.1:1/",
            ["AZURE_TENANT_ID"] = "synthetic-tenant",
            ["AZURE_CLIENT_ID"] = "synthetic-client",
            ["AZURE_FEDERATED_TOKEN_FILE"] = Path.Combine(fixture.Directory, "unused-token"),
            ["NativeMonitoring__BlobContainer"] = "native-monitoring"
        };
        foreach (string name in new[] { "BlobEndpoint", "RunId", "Phase", "ScenarioId", "ConfigurationId", "JobName", "PodUid", "SourceCommit" })
            environment["NativeMonitoring__" + name] = environment["B10Abrupt__" + name];
        var previous = environment.Keys.ToDictionary(name => name, Environment.GetEnvironmentVariable, StringComparer.Ordinal);
        var blockedStore = new BlockedStore();
        int factoryCalls = 0;
        try
        {
            foreach (var pair in environment) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            B10AbruptHost.ConfigureTestServices = services => services.AddSingleton<IKafkaNativeMonitoringStore>(blockedStore);
            B10AbruptHost.ProducerFactoryOverride = (_, _, _) =>
            {
                Interlocked.Increment(ref factoryCalls);
                throw new InvalidOperationException("b10_abrupt_guard_failed:synthetic_build_failure");
            };

            int exit = await B10AbruptHost.RunChildAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            Assert.Equal(1, factoryCalls);
            Assert.Equal(3, exit);
            Assert.Equal(0, blockedStore.CreateCalls);
            JsonElement[] journal = fixture.Records();
            Assert.Equal("no_kill", journal[^1].GetProperty("kind").GetString());
            Assert.Equal("b10_abrupt_guard_failed", journal[^1].GetProperty("data").GetProperty("reason").GetString());
            Assert.DoesNotContain(journal, item => item.GetProperty("kind").GetString() == "kill_intent");
        }
        finally
        {
            B10AbruptHost.ProducerFactoryOverride = null;
            B10AbruptHost.ConfigureTestServices = null;
            foreach (var pair in previous) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    private sealed class BlockedStore : IKafkaNativeMonitoringStore
    {
        private readonly TaskCompletionSource blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int createCalls;
        internal int CreateCalls => Volatile.Read(ref createCalls);
        public Task CreateAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref createCalls);
            return blocked.Task;
        }
    }
}

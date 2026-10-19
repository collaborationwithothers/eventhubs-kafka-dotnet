using System.Text;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class VerifierWindowComponentTests
{
    [Fact]
    public async Task Given_EmptyCapturedWindow_When_RealHandlersRun_Then_PositionsCompleteWithZeroCounts()
    {
        await using var fixture = await StartAsync();
        using var run = new ComponentRunHarness(fixture.ObserverBootstrapServers);
        await run.SetAttemptsAsync();
        Assert.Equal(0, await run.StartAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await run.VerifyAsync(TestContext.Current.CancellationToken));
        var result = Result(run);
        Assert.Equal("complete_valid", result["status"]!.GetValue<string>());
        foreach (string count in new[] { "ok", "loss", "phantom", "reported_failure", "duplicate", "reorder" })
            Assert.Equal(0, result[count]!.GetValue<long>());
        Assert.Equal(0, result["start_line"]![0]!["offset"]!.GetValue<long>());
        Assert.Equal(0, result["end_line"]![0]!["offset"]!.GetValue<long>());
    }

    [Fact]
    public async Task Given_PartitionAddedAfterStart_When_RealHandlerDrains_Then_EndOnlyPartitionStartsAtZero()
    {
        await using var fixture = await StartAsync();
        using var run = new ComponentRunHarness(fixture.ObserverBootstrapServers);
        Assert.Equal(0, await run.StartAsync(TestContext.Current.CancellationToken));
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = fixture.ObserverBootstrapServers }).Build();
        await admin.CreatePartitionsAsync([new PartitionsSpecification { Topic = "document-events", IncreaseTo = 2 }]);
        await ProduceAsync(fixture.ObserverBootstrapServers, run.Options.InstanceId!, "synthetic-end-only", "k", 1);
        await run.SetAttemptsAsync(run.Row("synthetic-end-only", "k"));
        Assert.Equal(0, await run.VerifyAsync(TestContext.Current.CancellationToken));
        var result = Result(run);
        Assert.Single(result["start_line"]!.AsArray());
        Assert.Equal(2, result["end_line"]!.AsArray().Count);
        Assert.Equal(1, result["ok"]!.GetValue<long>());
    }

    [Fact]
    public async Task Given_CorruptRecordAppendedAfterFixedEnd_When_RealHandlerDrains_Then_LaterRecordIsExcluded()
    {
        await using var fixture = await StartAsync();
        using var run = new ComponentRunHarness(fixture.ObserverBootstrapServers);
        Assert.Equal(0, await run.StartAsync(TestContext.Current.CancellationToken));
        await ProduceAsync(fixture.ObserverBootstrapServers, run.Options.InstanceId!, "synthetic-in-window", "k");
        await run.SetAttemptsAsync(run.Row("synthetic-in-window", "k"));
        run.AfterEndQuery = () => ProduceAsync(fixture.ObserverBootstrapServers, "foreign", "later", "outside", corrupt: true).GetAwaiter().GetResult();
        Assert.Equal(0, await run.VerifyAsync(TestContext.Current.CancellationToken));
        var result = Result(run);
        Assert.Equal(1, result["ok"]!.GetValue<long>());
        Assert.Equal(1, result["end_line"]![0]!["offset"]!.GetValue<long>());
        using var observer = ComponentRunHarness.Consumer(fixture.ObserverBootstrapServers);
        Assert.Equal(2, observer.QueryWatermarkOffsets(new TopicPartition("document-events", 0), TimeSpan.FromSeconds(10)).High.Value);
    }

    [Fact]
    public async Task Given_StartDeletedButRetainedStartControl_When_RealHandlersDrain_Then_OnlyDeletedStartFailsNine()
    {
        await using var fixture = await StartAsync();
        using var deleted = new ComponentRunHarness(fixture.ObserverBootstrapServers);
        using var retained = new ComponentRunHarness(fixture.ObserverBootstrapServers);
        Assert.Equal(0, await deleted.StartAsync(TestContext.Current.CancellationToken));
        await ProduceAsync(fixture.ObserverBootstrapServers, "prior", "prior", "prior");
        Assert.Equal(0, await retained.StartAsync(TestContext.Current.CancellationToken));
        await ProduceAsync(fixture.ObserverBootstrapServers, retained.Options.InstanceId!, "retained", "k");
        await retained.SetAttemptsAsync(retained.Row("retained", "k"));
        await deleted.SetAttemptsAsync(deleted.Row("retained", "k"));
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = fixture.ObserverBootstrapServers }).Build();
        await admin.DeleteRecordsAsync([new TopicPartitionOffset("document-events", 0, 1)]);
        Assert.Equal(0, await retained.VerifyAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, Result(retained)["ok"]!.GetValue<long>());
        Assert.Equal(1, await deleted.VerifyAsync(TestContext.Current.CancellationToken));
        AssertFailed(deleted, 9);
        Assert.Null(Result(deleted)["drain_time"]);
        Assert.Null(Result(deleted)["drain_elapsed_at_stop"]);
        Assert.NotEmpty(deleted.NativeErrors);
        Assert.All(deleted.NativeErrors, error => Assert.True(error is ErrorCode.Local_AutoOffsetReset or ErrorCode.OffsetOutOfRange));
        string directory = Environment.GetEnvironmentVariable("PARCHWELL_FIXTURE_PROOF_DIR") ?? Path.GetTempPath();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "retained-start-native-error.json"),
            System.Text.Json.JsonSerializer.Serialize(new { scope = "synthetic local Kafka only", errors = deleted.NativeErrors.Select(error => new { name = error.ToString(), code = (int)error }) }), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Given_SimulatedDeliveredLedgerRowForUnsentSyntheticAttempt_When_RealHandlerDrains_Then_SimulatedLossIsCounted()
    {
        await using var fixture = await StartAsync();
        using var run = new ComponentRunHarness(fixture.ObserverBootstrapServers);
        Assert.Equal(0, await run.StartAsync(TestContext.Current.CancellationToken));
        await ProduceAsync(fixture.ObserverBootstrapServers, run.Options.InstanceId!, "real-produced", "real");
        var simulated = run.Row("SIMULATED-LOSS-NEVER-SENT", "SIMULATED-LOSS-KEY");
        simulated["attempt"]!["ordinal"] = 1;
        await run.SetAttemptsAsync(run.Row("real-produced", "real"), simulated);
        Assert.Equal(0, await run.VerifyAsync(TestContext.Current.CancellationToken));
        var result = Result(run);
        Assert.Equal(1, result["ok"]!.GetValue<long>());
        Assert.Equal(1, result["loss"]!.GetValue<long>());
    }

    [Theory]
    [InlineData("dropped-ledger", 4)]
    [InlineData("missing-guard", 14)]
    [InlineData("safety-stop", 10)]
    public async Task Given_ADR016MustFailEvidence_When_RealHandlerDrains_Then_NoAuthoritativeTotals(string defect, int failedCheck)
    {
        await using var fixture = await StartAsync();
        using var run = new ComponentRunHarness(fixture.ObserverBootstrapServers);
        Assert.Equal(0, await run.StartAsync(TestContext.Current.CancellationToken));
        await ProduceAsync(fixture.ObserverBootstrapServers, run.Options.InstanceId!, "seen", "k");
        await run.SetAttemptsAsync(run.Row("seen", "k"));
        if (defect == "dropped-ledger")
        {
            var missing = run.Row("DROPPED-NEVER-SENT", "missing");
            missing["attempt"]!["ordinal"] = 1;
            await run.SetAttemptsAsync(run.Row("seen", "k"), missing);
            run.Inputs["ledger.jsonl"] = Encoding.UTF8.GetBytes(run.Row("seen", "k").ToJsonString() + "\n");
        }
        else if (defect == "missing-guard")
        {
            var capabilities = JsonNode.Parse(run.Inputs["capabilities.json"])!;
            capabilities["guard_observations_complete"] = false;
            run.Inputs["capabilities.json"] = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(capabilities);
        }
        else
        {
            run.Options.SafetyStop = TimeSpan.FromMilliseconds(1000);
            run.AfterEndQuery = () => Task.Delay(TimeSpan.FromMilliseconds(1001), TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        }
        Assert.Equal(1, await run.VerifyAsync(TestContext.Current.CancellationToken));
        AssertFailed(run, failedCheck, defect == "safety-stop" ? "incomplete" : "invalid");
    }

    internal static JsonNode Result(ComponentRunHarness run) => JsonNode.Parse(run.Outputs[run.Options.ResultOutputName!])!;
    internal static void AssertFailed(ComponentRunHarness run, int check, string status = "invalid")
    {
        var result = Result(run);
        Assert.Equal(status, result["status"]!.GetValue<string>());
        Assert.Contains(result["failed_checks"]!.AsArray(), value => value!.GetValue<int>() == check);
        foreach (string count in new[] { "ok", "loss", "phantom", "reported_failure", "duplicate", "reorder" })
            Assert.Null(result[count]);
    }
    private static async Task<LocalKafkaFixture> StartAsync()
    {
        var fixture = new LocalKafkaFixture();
        try
        {
            await fixture.StartAsync(TestContext.Current.CancellationToken);
            await DirectObserver.CreateTopicAsync(fixture.ObserverBootstrapServers, "document-events", TestContext.Current.CancellationToken);
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }
    private static async Task ProduceAsync(string bootstrap, string instance, string attempt, string key, int partition = 0, bool corrupt = false)
    {
        using var producer = new ProducerBuilder<byte[], byte[]>(new ProducerConfig { BootstrapServers = bootstrap, Acks = Acks.All, EnableIdempotence = false }).Build();
        var report = await producer.ProduceAsync(new TopicPartition("document-events", partition), new Message<byte[], byte[]>
        {
            Key = Encoding.UTF8.GetBytes(key), Value = Encoding.UTF8.GetBytes("synthetic-payload"),
            Headers = new Headers { { "instance-id", corrupt ? [0xff] : Encoding.UTF8.GetBytes(instance) },
                { "attempt-id", Encoding.UTF8.GetBytes(attempt) }, { "sequence", Encoding.UTF8.GetBytes("1") } }
        }, TestContext.Current.CancellationToken);
        Assert.Equal(PersistenceStatus.Persisted, report.Status);
    }
}
#pragma warning restore CA1707

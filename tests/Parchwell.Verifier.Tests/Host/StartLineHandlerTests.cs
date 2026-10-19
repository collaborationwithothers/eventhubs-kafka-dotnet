using System.Text.Json;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class StartLineHandlerTests
{
    [Fact]
    public async Task Given_SafetyBudgetShorterThanQuery_When_CapturingStartLine_Then_OnlyWatermarkPhaseBudgetApplies()
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Options.SafetyStop = TimeSpan.FromMilliseconds(10);
        fixture.Kafka.Query = _ => { fixture.Kafka.Clock.Advance(100); return new WatermarkOffsets(0, 0); };
        Assert.Equal(0, await fixture.StartLine().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal([TimeSpan.FromMilliseconds(1000)], fixture.Kafka.QueryTimeouts);
        Assert.Equal(1, fixture.Creates);
    }

    [Fact]
    public async Task Given_EmptyPartition_When_Capturing_Then_CleansNativeClientsBeforeOneSuccessfulNotePublication()
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Kafka.Query = _ => new WatermarkOffsets(0, 0);
        Assert.Equal(0, await fixture.StartLine().ExecuteAsync(TestContext.Current.CancellationToken));
        fixture.Events.Add("return");
        Assert.Equal(["admin-dispose", "consumer-dispose", "create", "return"], fixture.Events);
        Assert.Equal(1, fixture.Creates);
        Assert.Equal("notes/new.json", fixture.OutputName);
        using JsonDocument output = JsonDocument.Parse(fixture.Output!);
        Assert.Equal("golden-loadgen-client", output.RootElement.GetProperty("identity").GetProperty("client_id").GetString());
        Assert.Equal(0, output.RootElement.GetProperty("partition_highs")[0].GetProperty("high").GetInt64());
        Assert.Equal("1970-01-01T00:00:00.0000000+00:00", output.RootElement.GetProperty("captured_at").GetString());
        Assert.Equal(typeof(ConsumerConfig).Assembly.GetName().Version!.ToString(), output.RootElement.GetProperty("confluent_kafka_version").GetString());
        Assert.Equal(Library.VersionString, output.RootElement.GetProperty("librdkafka_version").GetString());
    }

    [Theory]
    [InlineData("token")]
    [InlineData("metadata")]
    [InlineData("watermark")]
    [InlineData("empty-metadata")]
    public async Task Given_TokenOrQueryFailure_When_Capturing_Then_CleansAndMakesZeroOutputCalls(string phase)
    {
        using var fixture = new HostEvidenceBuilder();
        if (phase == "token") fixture.Kafka.Poll = () => fixture.Kafka.Clock.Advance(1000);
        if (phase == "metadata") fixture.Kafka.ReadMetadata = () => { fixture.Kafka.Clock.Advance(1000); return KafkaSessionTestSupport.Metadata(0); };
        if (phase == "watermark") fixture.Kafka.Query = _ => { fixture.Kafka.Clock.Advance(1000); return new WatermarkOffsets(0, 0); };
        if (phase == "empty-metadata") fixture.Kafka.ReadMetadata = () => KafkaSessionTestSupport.Metadata();
        await Assert.ThrowsAsync<VerifierKafkaOperationException>(() => fixture.StartLine().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["admin-dispose", "consumer-dispose"], fixture.Events);
        Assert.Equal(0, fixture.Creates);
        Assert.Null(fixture.Output);
    }

    [Theory]
    [InlineData(99, true)]
    [InlineData(100, false)]
    [InlineData(101, false)]
    public async Task Given_CleanupNearDeadline_When_Capturing_Then_PublicationRequiresCompletedCleanupWithinBudget(int elapsed, bool succeeds)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.Kafka.AdminDispose = () => { fixture.Events.Add("admin-dispose"); fixture.Kafka.Clock.Advance(elapsed); };
        if (succeeds) Assert.Equal(0, await fixture.StartLine().ExecuteAsync(TestContext.Current.CancellationToken));
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.StartLine().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(succeeds ? 1 : 0, fixture.Creates);
        Assert.Equal(["admin-dispose", "consumer-dispose"], fixture.Events.Take(2));
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("consumer")]
    public async Task Given_NativeCleanupThrows_When_Capturing_Then_AllCleanupFinishesAndNoPublicationOccurs(string phase)
    {
        using var fixture = new HostEvidenceBuilder();
        if (phase == "admin") fixture.Kafka.AdminDispose = () => throw new IOException("synthetic admin cleanup");
        else fixture.Kafka.ConsumerDispose = () => throw new IOException("synthetic consumer cleanup");
        await Assert.ThrowsAsync<AggregateException>(() => fixture.StartLine().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["admin-dispose", "consumer-dispose"], fixture.Kafka.Events);
        Assert.Equal(0, fixture.Creates);
    }

    [Fact]
    public async Task Given_CancelledWork_When_Cleaning_Then_IndependentCleanupFinishesAndPublicationIsPrevented()
    {
        using var fixture = new HostEvidenceBuilder();
        using var work = new CancellationTokenSource();
        fixture.Kafka.Query = _ => { work.Cancel(); return new WatermarkOffsets(0, 0); };
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.StartLine().ExecuteAsync(work.Token));
        Assert.Equal(["admin-dispose", "consumer-dispose"], fixture.Events);
        Assert.Equal(0, fixture.Creates);
    }

    [Theory]
    [InlineData("partitions")]
    [InlineData("output")]
    public async Task Given_ExecutionLimit_When_Capturing_Then_CleansBeforeZeroOutputCalls(string limit)
    {
        using var fixture = new HostEvidenceBuilder();
        if (limit == "partitions") fixture.Kafka.ReadMetadata = () => KafkaSessionTestSupport.Metadata(0, 1, 2);
        else fixture.Options.MaxOutputBytes = 1;
        await Assert.ThrowsAsync<InputLimitExceededException>(() => fixture.StartLine().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, fixture.Creates);
        Assert.Equal(["admin-dispose", "consumer-dispose"], fixture.Events);
    }

    [Theory]
    [InlineData("cleanup", 99, true)]
    [InlineData("cleanup", 100, false)]
    [InlineData("cleanup", 101, false)]
    [InlineData("upload", 99, true)]
    [InlineData("upload", 100, false)]
    [InlineData("upload", 101, false)]
    public async Task Given_DelayedTimerCallbacks_When_MonotonicBudgetExpires_Then_CannotReturnSuccessfulPublication(string phase, int elapsed, bool succeeds)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.DelayHandlerTimerCallbacks();
        if (phase == "cleanup") fixture.Kafka.AdminDispose = () => fixture.Kafka.Clock.Advance(elapsed);
        else fixture.AfterCreate = _ => fixture.Kafka.Clock.Advance(elapsed);
        if (succeeds) Assert.Equal(0, await fixture.StartLine().ExecuteAsync(TestContext.Current.CancellationToken));
        else await Assert.ThrowsAsync<TimeoutException>(() => fixture.StartLine().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(succeeds || phase == "upload" ? 1 : 0, fixture.Creates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_PublicationFailureAfterPossibleCommit_When_Capturing_Then_AttemptsOnlyOnceAndPreservesObject(bool expires)
    {
        using var fixture = new HostEvidenceBuilder();
        fixture.AfterCreate = _ =>
        {
            if (expires) fixture.Kafka.Clock.Advance(100);
            else throw new IOException("synthetic uncertain commit");
        };
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.StartLine().ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, fixture.Creates);
        Assert.NotNull(fixture.Output);
        Assert.Equal(["admin-dispose", "consumer-dispose", "create"], fixture.Events);
    }
}
#pragma warning restore CA1707

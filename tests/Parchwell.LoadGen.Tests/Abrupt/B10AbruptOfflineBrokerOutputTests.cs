using System.Text;
using Docker.DotNet;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Parchwell.LoadGen.Tests.Component;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// Failure diagnostics must survive the fixture removing a broker whose startup did not complete.
public sealed class B10AbruptOfflineBrokerOutputTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(0, 0, 'x')]
    [InlineData(1, 1, 'x')]
    [InlineData(16383, 16383, 'x')]
    [InlineData(16384, 16384, 'x')]
    [InlineData(16385, 16384, 'x')]
    [InlineData(16385, 16384, '\u00e9')]
    public void Given_BrokerOutput_When_TakingFailureTail_Then_Last16384CharactersAreRetained(int length, int expectedLength, char character)
    {
        string value = length == 0 ? "" : new string(character, length - 1) + "z";
        string expected = expectedLength == 0 ? "" : new string(character, expectedLength - 1) + "z";

        string tail = LocalKafkaFixture.TailOutput(value);

        Assert.Equal(expectedLength, tail.Length);
        Assert.Equal(expected, tail);
    }

    [Fact]
    public async Task Given_BrokerStartupOutput_When_StartupIsCanceled_Then_OutputSurvivesFixtureFailureCleanup()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(60));
        using var stream = new CancelOnOutputStream(cancellation);
        using IOutputConsumer consumer = Consume.RedirectStdoutAndStderrToStream(stream, stream);
        await using var run = new B10OfflineImageRun("unused-for-broker-regression", new string('a', 40), output);

        Exception? failure = await Record.ExceptionAsync(() => run.StartBrokerAsync(cancellation.Token, consumer));

        Assert.True(failure is OperationCanceledException or TimeoutException, failure?.ToString());
        Assert.True(cancellation.IsCancellationRequested);
        Assert.False(string.IsNullOrEmpty(stream.FirstOutput), "broker_startup_output_was_not_observed");
        string retained = await run.OutputTailsAsync();
        Assert.StartsWith("broker:", retained, StringComparison.Ordinal);
        Assert.Contains(stream.FirstOutput!, retained, StringComparison.Ordinal);
        Assert.DoesNotContain("container_output_unavailable:", retained, StringComparison.Ordinal);
        await AssertBrokerRemovedAsync(retained);
        await run.CleanupAsync(passed: false);
        Assert.Equal(retained, run.LastOutput);
        output.WriteLine("canceled_startup_output_tail=" + retained);
    }

    [Fact]
    public async Task Given_StartedBroker_When_LaterFailureCollectsOutputAndCleanupRuns_Then_BrokerTailRemainsAvailable()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(60));
        await using var run = new B10OfflineImageRun("unused-for-broker-regression", new string('a', 40), output);
        await run.StartBrokerAsync(cancellation.Token);

        string live = await run.OutputTailsAsync();
        Assert.StartsWith("broker:", live, StringComparison.Ordinal);
        Assert.Contains("Transitioning from RECOVERY to RUNNING", live, StringComparison.Ordinal);
        Assert.DoesNotContain("container_output_unavailable:", live, StringComparison.Ordinal);

        await run.CleanupAsync(passed: false);

        Assert.StartsWith("broker:", run.LastOutput, StringComparison.Ordinal);
        Assert.Contains("Transitioning from RECOVERY to RUNNING", run.LastOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("container_output_unavailable:", run.LastOutput, StringComparison.Ordinal);
        await AssertBrokerRemovedAsync(run.LastOutput);
        output.WriteLine("successful_startup_output_tail=" + run.LastOutput);
    }

    private static async Task AssertBrokerRemovedAsync(string retained)
    {
        string name = retained["broker:".Length..retained.IndexOf('\n', StringComparison.Ordinal)];
        Assert.NotEmpty(name);
        using DockerClient docker = TestcontainersSettings.OS.DockerEndpointAuthConfig.GetDockerClientBuilder()
            .WithTimeout(TimeSpan.FromSeconds(15)).Build();
        await Assert.ThrowsAsync<DockerContainerNotFoundException>(() => docker.Containers.InspectContainerAsync(name,
            TestContext.Current.CancellationToken));
    }

    private sealed class CancelOnOutputStream(CancellationTokenSource cancellation) : MemoryStream
    {
        private string? firstOutput;
        internal string? FirstOutput => Volatile.Read(ref firstOutput);

        public override void Write(byte[] buffer, int offset, int count) => Observe(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer) => Observe(buffer);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Observe(buffer.AsSpan(offset, count));
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Observe(buffer.Span);
            return ValueTask.CompletedTask;
        }

        private void Observe(ReadOnlySpan<byte> buffer)
        {
            if (!buffer.IsEmpty && Interlocked.CompareExchange(ref firstOutput, Encoding.UTF8.GetString(buffer), null) is null)
                cancellation.Cancel();
        }
    }
}

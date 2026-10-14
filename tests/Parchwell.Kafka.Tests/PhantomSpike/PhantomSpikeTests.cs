using Parchwell.Kafka.Tests.PhantomSpike.Evidence;
using Parchwell.Kafka.Tests.PhantomSpike.Experiment;
using Xunit;

namespace Parchwell.Kafka.Tests.PhantomSpike;

[CollectionDefinition("Phantom spike", DisableParallelization = true)]
public sealed class PhantomSpikeGrouping;

[Collection("Phantom spike")]
public sealed class PhantomSpikeTests(ITestOutputHelper output)
{
    [Fact]
#pragma warning disable CA1707 // Required repository test naming convention.
    public async Task Given_ApprovedFaultMatrix_When_RealClientAttemptsAreObserved_Then_ExperimentalIntegrityIsEstablished()
#pragma warning restore CA1707
    {
        var evidence = await new SpikeRunner(TimeProvider.System).RunAsync(TestContext.Current.CancellationToken);
#pragma warning disable xUnit1051 // Preserve failure evidence even when the test cancellation deadline fired.
        var directory = await SpikeEvidenceWriter.WriteAsync(evidence, CancellationToken.None);
#pragma warning restore xUnit1051
        var decision = SpikeEvidence.Decide(evidence);
        output.WriteLine($"Synthetic diagnostic experiment finding: {decision.ExistenceFinding}. Evidence: {directory}");
        foreach (var failure in decision.IntegrityFailures)
            output.WriteLine(failure);
        Assert.True(decision.IntegritySucceeded,
            $"Experiment integrity was not established. Finding: {decision.ExistenceFinding}. Evidence: {directory}. "
            + string.Join("\n", decision.IntegrityFailures));
    }
}

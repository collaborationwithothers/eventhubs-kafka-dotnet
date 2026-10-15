namespace Parchwell.Kafka.Tests.PhantomSpike.Infrastructure;

/// <summary>
/// Broker startup failed. It names the exact owned containers and reports cleanup as not established,
/// so a container created late can still be found and removed (docs/operations/phantom-spike/README.md).
/// </summary>
internal sealed class BrokerStartupException : InvalidOperationException
{
    public BrokerStartupException(string proxyName, string kafkaName, string diagnostics, Exception innerException)
        : base($"Broker startup failed. Exact owned container names: {proxyName}, {kafkaName}. Cleanup was not established.\n{diagnostics}", innerException)
    {
        OwnedContainerNames = [proxyName, kafkaName];
        Diagnostics = diagnostics;
        CleanupEstablished = false;
    }

    public IReadOnlyList<string> OwnedContainerNames { get; }
    public string Diagnostics { get; }
    public bool CleanupEstablished { get; }
}

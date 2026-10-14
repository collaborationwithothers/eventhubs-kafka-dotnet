namespace Parchwell.Kafka.Tests.PhantomSpike.Infrastructure;

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

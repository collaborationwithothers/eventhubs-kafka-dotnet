namespace Parchwell.Kafka.Statistics;

/// <summary>Stores private native monitoring objects using conditional creation.</summary>
/// <remarks>
/// Implementations are owned by the host. A successful call means that the exact supplied bytes
/// were created at the supplied object name. An existing object must cause an exception; it must
/// never be overwritten. The caller supplies a bounded cancellation token for its cleanup budget.
/// </remarks>
public interface IKafkaNativeMonitoringStore
{
    /// <summary>Creates one object if its name does not already exist.</summary>
    /// <param name="path">The object name relative to the private monitoring container.</param>
    /// <param name="content">The exact bytes to store.</param>
    /// <param name="cancellationToken">The remaining cleanup budget.</param>
    /// <returns>A task that completes when the creation has finished.</returns>
    Task CreateAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
}

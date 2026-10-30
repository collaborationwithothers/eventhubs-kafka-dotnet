namespace Parchwell.Kafka.Authentication;

/// <summary>Describes one explicit metadata sample of a projected token file.</summary>
/// <param name="Sample">The one-based sample attempt, or the saturated sample-limit value.</param>
/// <param name="ObservedAt">The UTC sample time, when available.</param>
/// <param name="Timestamp">The monotonic sample timestamp, when available.</param>
/// <param name="TimestampFrequency">The injected time provider's monotonic timestamp frequency.</param>
/// <param name="Generation">The observed stable metadata generation, when available.</param>
/// <param name="Status">The bounded result status: observed, unavailable or sample_limit.</param>
public sealed record ProjectedTokenFileObservation(
    int Sample,
    DateTimeOffset? ObservedAt,
    long? Timestamp,
    long TimestampFrequency,
    int? Generation,
    string Status);

internal readonly record struct ProjectedTokenFileMetadata(string FullPath, long LastWriteTimeUtcTicks, long Length);

/// <summary>Explicitly samples projected token-file metadata without opening or reading token contents.</summary>
public sealed class ProjectedTokenFileObserver
{
    private const int MaximumSamples = 4096;
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly Func<ProjectedTokenFileMetadata> _readMetadata;
    private readonly long _timestampFrequency;
    private readonly bool _frequencyAvailable;
    private ProjectedTokenFileMetadata? _lastStableMetadata;
    private int? _generation;
    private int _sample;

    internal ProjectedTokenFileObserver(string tokenFilePath, TimeProvider timeProvider)
        : this(tokenFilePath, timeProvider, () => ReadProductionMetadata(tokenFilePath))
    {
    }

    internal ProjectedTokenFileObserver(
        string tokenFilePath,
        TimeProvider timeProvider,
        Func<ProjectedTokenFileMetadata> readMetadata)
    {
        ArgumentNullException.ThrowIfNull(tokenFilePath);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _readMetadata = readMetadata ?? throw new ArgumentNullException(nameof(readMetadata));
        try
        {
            _timestampFrequency = timeProvider.TimestampFrequency;
            _frequencyAvailable = true;
        }
        catch (Exception)
        {
            _frequencyAvailable = false;
        }
    }

    /// <summary>Takes one explicit, bounded pair of filesystem metadata snapshots.</summary>
    /// <returns>A sanitized result containing only sample, time, frequency, generation and status.</returns>
    public ProjectedTokenFileObservation Observe()
    {
        lock (_gate)
        {
            if (_sample >= MaximumSamples)
                return new ProjectedTokenFileObservation(MaximumSamples + 1, null, null, _timestampFrequency, null, "sample_limit");

            int sample = ++_sample;
            ProjectedTokenFileMetadata first;
            ProjectedTokenFileMetadata second;
            try
            {
                first = _readMetadata();
                second = _readMetadata();
            }
            catch (Exception)
            {
                return Unavailable(sample);
            }

            if (!first.Equals(second) || !_frequencyAvailable)
                return Unavailable(sample);

            DateTimeOffset observedAt;
            long timestamp;
            try
            {
                observedAt = _timeProvider.GetUtcNow().ToUniversalTime();
                timestamp = _timeProvider.GetTimestamp();
            }
            catch (Exception)
            {
                return Unavailable(sample);
            }

            if (_lastStableMetadata is null)
            {
                _generation = 1;
            }
            else if (!_lastStableMetadata.Value.Equals(second))
            {
                _generation = (_generation ?? 0) + 1;
            }

            _lastStableMetadata = second;
            return new ProjectedTokenFileObservation(
                sample, observedAt, timestamp, _timestampFrequency, _generation, "observed");
        }
    }

    private ProjectedTokenFileObservation Unavailable(int sample) =>
        new(sample, null, null, _timestampFrequency, null, "unavailable");

    private static ProjectedTokenFileMetadata ReadProductionMetadata(string tokenFilePath)
    {
        string resolved = ProjectedTokenPathResolver.Resolve(
            tokenFilePath,
            candidate => new FileInfo(candidate).LinkTarget,
            candidate =>
            {
                if (!Directory.Exists(candidate))
                    throw new DirectoryNotFoundException();
            });

        FileInfo targetFile = new(resolved);
        targetFile.Refresh();
        if (!targetFile.Exists || (targetFile.Attributes & FileAttributes.Directory) != 0)
            throw new FileNotFoundException();

        return new ProjectedTokenFileMetadata(
            resolved,
            targetFile.LastWriteTimeUtc.Ticks,
            targetFile.Length);
    }
}

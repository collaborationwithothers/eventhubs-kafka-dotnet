using Confluent.Kafka;
using Parchwell.Kafka.Authentication;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.Common.Kafka;

/// <summary>Owns one authenticated byte-array consumer and its dependent admin.</summary>
internal sealed class VerifierKafkaSession
{
    internal const string Topic = "document-events";
    private readonly IAdminClient admin;
    private readonly KafkaOAuthBearerSession authentication;
    private readonly TimeProvider clock;
    private readonly VerifierHostOptions options;
    private readonly CancellationTokenSource acquisition;
    private bool installed;
    private bool cleaned;

    internal VerifierKafkaSession(IConsumer<byte[], byte[]> consumer, IAdminClient admin,
        KafkaOAuthBearerSession authentication, TimeProvider clock, VerifierHostOptions options,
        CancellationTokenSource acquisition)
    {
        Consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
        this.admin = admin ?? throw new ArgumentNullException(nameof(admin));
        this.authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.acquisition = acquisition ?? throw new ArgumentNullException(nameof(acquisition));
    }

    public IConsumer<byte[], byte[]> Consumer { get; }

    public IReadOnlyList<BrokerMetadata>? MetadataBrokers { get; private set; }

    public void InstallToken(CancellationToken cancellationToken = default)
    {
        long origin = clock.GetTimestamp();
        TimeSpan budget = options.TokenTimeout!.Value;
        using ITimer timer = clock.CreateTimer(_ => acquisition.Cancel(), null, budget, Timeout.InfiniteTimeSpan);
        using CancellationTokenRegistration registration = cancellationToken.Register(acquisition.Cancel);
        try
        {
            while (true)
            {
                // A later successful callback cannot erase the first initial-acquisition failure.
                if (authentication.InitialFailure is not null)
                    throw new VerifierKafkaOperationException("Initial Kafka token installation failed.");
                TimeSpan remaining = Remaining(origin, budget, acquisition.Token);
                if (authentication.InstalledTokenExpiresOn is not null)
                {
                    installed = true;
                    return;
                }
                Consumer.Consume(remaining < options.PollSlice!.Value ? remaining : options.PollSlice.Value);
            }
        }
        catch (Exception exception) when (exception is KafkaException or TimeoutException or OperationCanceledException)
        {
            throw new VerifierKafkaOperationException("Kafka token installation failed or expired.", exception);
        }
        // Disposing the timer before returning prevents the initial deadline cancelling later refreshes.
    }

    public IReadOnlyDictionary<int, long> ReadHighWatermarks(Action? beforeFirstWatermark = null,
        Func<TimeSpan>? remainingSafetyTime = null, CancellationToken cancellationToken = default)
    {
        if (!installed)
            throw new VerifierKafkaOperationException("Kafka token installation has not completed.");
        try
        {
            long origin = clock.GetTimestamp();
            TimeSpan budget = options.MetadataTimeout!.Value;
            Metadata metadata = admin.GetMetadata(Topic, Remaining(origin, budget, cancellationToken));
            _ = Remaining(origin, budget, cancellationToken);
            TopicMetadata? topic = metadata.Topics.Find(candidate => candidate.Topic == Topic);
            if (topic is null || topic.Error.IsError || topic.Partitions.Count == 0)
                throw new VerifierKafkaOperationException("Kafka topic metadata is unavailable or empty.");
            if (topic.Partitions.Count > options.MaxPartitions!.Value)
                throw new InputLimitExceededException("Kafka partition count exceeds MaxPartitions.");
            if (topic.Partitions.Exists(partition => partition.Error.IsError))
                throw new VerifierKafkaOperationException("Kafka partition metadata failed.");

            var highs = new Dictionary<int, long>();
            origin = clock.GetTimestamp();
            budget = options.WatermarkTimeout!.Value;
            foreach (PartitionMetadata partition in topic.Partitions)
            {
                if (highs.Count == 0)
                    beforeFirstWatermark?.Invoke();
                TimeSpan remaining = RemainingWatermarkTime();
                WatermarkOffsets offsets = Consumer.QueryWatermarkOffsets(
                    new TopicPartition(Topic, partition.PartitionId), remaining);
                _ = RemainingWatermarkTime();
                highs.Add(partition.PartitionId, offsets.High.Value);
            }
            MetadataBrokers = metadata.Brokers.ToArray();
            return highs;

            TimeSpan RemainingWatermarkTime()
            {
                TimeSpan remaining = Remaining(origin, budget, cancellationToken);
                if (remainingSafetyTime is null) return remaining;
                TimeSpan safetyRemaining = remainingSafetyTime();
                if (safetyRemaining <= TimeSpan.Zero)
                    throw new TimeoutException("Kafka watermark safety budget expired.");
                return safetyRemaining < remaining ? safetyRemaining : remaining;
            }
        }
        catch (Exception exception) when (exception is KafkaException or TimeoutException or OperationCanceledException)
        {
            throw new VerifierKafkaOperationException("Kafka metadata or watermark query failed or expired.", exception);
        }
    }

    /// <summary>Finishes all native cleanup synchronously, even if the cleanup deadline expires.</summary>
    public void Cleanup(CancellationToken cancellationToken = default)
    {
        if (cleaned) return;
        cleaned = true;
        List<Exception> failures = [];
        // Never detach native cleanup: a noncooperative dispose remains under the process watchdog.
        try { admin.Dispose(); }
        catch (Exception exception) { failures.Add(exception); }
        try { Consumer.Dispose(); }
        catch (Exception exception) { failures.Add(exception); }
        acquisition.Dispose();
        if (failures.Count != 0) throw new AggregateException("Kafka cleanup failed.", failures);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private TimeSpan Remaining(long origin, TimeSpan budget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TimeSpan remaining = budget - clock.GetElapsedTime(origin);
        if (remaining <= TimeSpan.Zero) throw new TimeoutException("Kafka operation budget expired.");
        return remaining;
    }
}

/// <summary>Identifies a token or query failure owned by verifier check 11.</summary>
internal sealed class VerifierKafkaOperationException : Exception
{
    public VerifierKafkaOperationException(string message) : base(message) { }
    public VerifierKafkaOperationException(string message, Exception innerException) : base(message, innerException) { }
}

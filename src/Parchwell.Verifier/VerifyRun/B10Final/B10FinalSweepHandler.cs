using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.VerifyRun.Configuration;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Window;

namespace Parchwell.Verifier.VerifyRun.B10Final;

/// <summary>Authenticates five repeat boundaries and publishes their final phase-wide observation.</summary>
internal sealed class B10FinalSweepHandler(IOptions<VerifierHostOptions> configured, TimeProvider clock,
    VerifierKafkaSessionFactory sessions, IVerifierArtifactStore verifierStore, IProducerArtifactStore producerStore,
    ScenarioLoader scenarioLoader, RegisteredProducerConfigurationResolver configurationResolver,
    VerifierCompletionState? completion = null) : IVerifierModeHandler
{
    public VerifierMode Mode => VerifierMode.B10FinalSweep;

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        VerifierHostOptions options = configured.Value;
        if (options.ScenarioId != "B10" || options.Phase is not ("C0" or "C1") ||
            options.ConfigurationId != options.Phase || !VerifierHostOptions.IsB10ScientificRunId(options.RunId) ||
            options.B10SettleIntervalMilliseconds != 60000) return 1;
        DateTimeOffset started = clock.GetUtcNow();
        var limits = new InputLimits(options.MaxInputDepth!.Value, options.MaxArtifactBytes!.Value,
            options.MaxTotalInputBytes!.Value, options.MaxRowBytes!.Value, options.MaxRowsPerArtifact!.Value,
            options.MaxInputEntries!.Value, options.MaxDiagnosticBytes!.Value);
        var budget = new InputBudget(limits);
        var reader = new B10FinalRepeatEvidence(options, limits, budget, ReadVerifier, ReadProducer, ListProducer);
        var repeats = Enumerable.Range(1, 5).Select(number => new B10FinalInput(number)).ToArray();
        ArtifactReference? sweepReference = null;
        B10SweepLine? sweep = null;
        string? globalFailure = null;
        foreach (B10FinalInput repeat in repeats)
        {
            try { await reader.ReadAsync(repeat, cancellationToken).ConfigureAwait(false); }
            catch (Exception exception) when (EvidenceFailure(exception, cancellationToken))
            {
                repeat.Started = true;
                repeat.TimeoutInvalid = true;
                repeat.RestartInputInvalid = true;
            }
        }
        try
        {
            string name = PhasePaths.SweepLine(options.RunId!, options.Phase!);
            byte[] bytes = await ReadVerifier(name, cancellationToken).ConfigureAwait(false) ?? throw new FormatException("sweep_missing");
            sweep = B10SweepEvidenceCodec.ReadSweepLine(bytes, limits, options.MaxPartitions!.Value);
            if (!B10SweepEvidenceCodec.MatchesSweepLine(sweep, options, started, repeats.SelectMany(repeat => repeat.Completions)))
                throw new FormatException("sweep_binding");
            sweepReference = Reference("verifier", name, bytes);
        }
        catch (Exception exception) when (EvidenceFailure(exception, cancellationToken))
        { globalFailure = "b10_final_sweep_line_invalid"; }
        if (globalFailure is null && repeats.SelectMany(repeat => repeat.Instances.Select(instance => (repeat.Number, Instance: instance)))
            .GroupBy(pair => pair.Instance, StringComparer.Ordinal).Any(group => group.Select(pair => pair.Number).Distinct().Count() > 1))
            globalFailure = "b10_final_ownership_invalid";
        if (globalFailure is null && repeats.Any(repeat => repeat.Started && repeat.Starts is null))
            globalFailure = "b10_final_start_line_invalid";
        if (globalFailure is null)
        {
            try
            {
                B10FinalClassification.SetWindows(repeats, sweep!.PartitionHighs, options.MaxPartitions!.Value);
            }
            catch (Exception exception) when (EvidenceFailure(exception, cancellationToken))
            { globalFailure = "b10_final_vector_invalid"; }
        }
        IReadOnlyList<ConsumedRecord> records = [];
        VerifierKafkaSession? session = null;
        ITimer? safetyTimer = null;
        using var kafkaWork = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (globalFailure is null && repeats.Any(repeat => repeat.Starts is not null))
        {
            try
            {
                session = sessions.Create(kafkaWork.Token);
                session.InstallToken(cancellationToken);
                long origin = 0;
                IReadOnlyDictionary<int, long> current = session.ReadHighWatermarks(() =>
                {
                    origin = clock.GetTimestamp();
                    safetyTimer = clock.CreateTimer(_ => kafkaWork.Cancel(), null, options.SafetyStop!.Value, Timeout.InfiniteTimeSpan);
                }, () => options.SafetyStop!.Value - clock.GetElapsedTime(origin), kafkaWork.Token);
                Dictionary<int, long> first = repeats.First(repeat => repeat.Starts is not null).Starts!;
                Dictionary<int, long> end = sweep!.PartitionHighs.ToDictionary(high => high.Partition, high => high.High);
                if (end.Any(high => !current.TryGetValue(high.Key, out long highNow) || highNow < high.Value))
                    throw new InvalidOperationException("sweep_watermark_unavailable");
                var window = new RunWindow(first.OrderBy(high => high.Key).Select(high => new PartitionHigh(high.Key, high.Value)).ToArray(), end,
                    end.OrderBy(high => high.Key).Select(high => new PartitionWindow(high.Key, first[high.Key], high.Value)).ToArray(), [], true);
                HashSet<string> keys = repeats.Where(repeat => repeat.First is not null).SelectMany(repeat => repeat.First!.Attempts.Values)
                    .Select(attempt => attempt.Key).ToHashSet(StringComparer.Ordinal);
                session.Consumer.Unassign();
                WindowDrainResult drain = WindowConsumer.Drain(session.Consumer, window, clock, origin, options.SafetyStop!.Value,
                    options.PollSlice!.Value, new ConsumedRecordBudget(options.MaxConsumedRecords!.Value, options.MaxConsumedBytes!.Value),
                    keys, cancellationToken, kafkaWork.Token);
                if (drain.DrainDuration is null || drain.Assessments.Any(assessment => assessment.Disposition != AssessmentDisposition.Pass))
                    throw new InvalidOperationException("sweep_drain_incomplete");
                records = drain.Records;
                session.Consumer.Unassign();
            }
            catch (Exception exception) when (EvidenceFailure(exception, cancellationToken))
            { globalFailure = "b10_final_drain_incomplete"; }
            finally
            {
                safetyTimer?.Dispose();
                long cleanupStarted = clock.GetTimestamp();
                using var deadline = new CancellationTokenSource(options.CleanupTimeout!.Value, clock);
                session?.Cleanup(deadline.Token);
                CheckDeadline(cleanupStarted, options.CleanupTimeout.Value, deadline.Token);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        LoadedScenario? scenario = globalFailure is null
            ? await Timed(ct => scenarioLoader.LoadAsync(options.ScenarioId!, ct).AsTask(), cancellationToken, options.IoTimeout).ConfigureAwait(false) : null;
        IReadOnlyList<B10FinalRepeat> projected = repeats.Select(repeat => B10FinalClassification.Classify(repeat, repeats, records,
            globalFailure, options, scenario, configurationResolver)).ToArray();
        var projection = new B10FinalProjection(new(options.RunId!, "B10", options.ConfigurationId!, options.Phase!, options.SourceCommit!,
            options.VerifierImage!, options.JobName!, options.PodName!, options.PodUid!),
            new(sweepReference, globalFailure is null, B10SweepEvidence.ResidualRisk, projected));
        byte[] output = B10FinalProjectionCodec.Write(projection);
        if (output.LongLength > options.MaxOutputBytes!.Value) throw new InvalidDataException("Final sweep exceeds MaxOutputBytes.");
        string outputName = $"{options.RunId}/{options.Phase}/b10-final-sweep.json";
        await Timed(async ct => { await verifierStore.CreateAsync(outputName, output, ct).ConfigureAwait(false); return true; }, cancellationToken).ConfigureAwait(false);
        byte[]? readBack = await Timed(ct => verifierStore.ReadBoundedAsync(outputName, options.MaxOutputBytes.Value, ct), cancellationToken).ConfigureAwait(false);
        if (readBack is null || !output.AsSpan().SequenceEqual(readBack)) return 1;
        var receipt = new WorkerReceipt(1, "b10-final-sweep", "B10", options.ConfigurationId!, options.RunId!, options.Phase!, null,
            options.JobName!, options.PodName!, null, options.SourceCommit!, 0, true, started.ToString("O", CultureInfo.InvariantCulture),
            clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture), [Reference("verifier", outputName, output)], null, null);
        completion?.SetPendingBlobReceipt(options.VerifierContainer!, $"{options.RunId}/{options.Phase}/b10-final-sweep-completion.json", PhaseWireCodec.WriteReceipt(receipt));
        return 0;

        async Task<byte[]?> ReadVerifier(string name, CancellationToken ct) => await Read(name, false, limits.MaxArtifactBytes, ct).ConfigureAwait(false);
        async Task<byte[]?> ReadProducer(string name, long maximum, CancellationToken ct) => await Read(name, true, maximum, ct).ConfigureAwait(false);
        async Task<byte[]?> Read(string name, bool producer, long maximum, CancellationToken ct)
        {
            byte[]? bytes = await Timed(token => producer ? producerStore.ReadBoundedAsync(name, Math.Min(maximum, limits.MaxArtifactBytes), token) :
                verifierStore.ReadBoundedAsync(name, Math.Min(maximum, limits.MaxArtifactBytes), token), ct).ConfigureAwait(false);
            if (bytes is not null)
            {
                if (bytes.LongLength > Math.Min(maximum, limits.MaxArtifactBytes)) throw new InputLimitExceededException("Final sweep object exceeds its byte limit.");
                budget.ConsumeBytes(bytes.LongLength);
            }
            return bytes;
        }
        async Task<BlobListing> ListProducer(string prefix, CancellationToken ct)
        {
            BlobListing listing = await Timed(token => producerStore.ListAsync(prefix, token), ct).ConfigureAwait(false);
            if (!listing.IsComplete || listing.Pages is < 0 or > BoundedBlobListing.MaximumPages || listing.Names.Count > BoundedBlobListing.MaximumEntries ||
                listing.Names.Sum(name => (long)System.Text.Encoding.UTF8.GetByteCount(name)) > BoundedBlobListing.MaximumNameBytes)
                throw new InvalidDataException("Final sweep listing incomplete.");
            budget.ConsumeEntries(listing.Names.Count);
            return listing;
        }
    }

    internal static ArtifactReference Reference(string container, string name, byte[] bytes) =>
        new(container, name, bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(bytes)));

    internal static bool EvidenceFailure(Exception exception, CancellationToken caller) =>
        !caller.IsCancellationRequested && exception is FormatException or JsonException or InvalidOperationException or ArgumentException or
            InvalidDataException or KeyNotFoundException or OverflowException or TimeoutException or InputLimitExceededException or
            VerifierKafkaOperationException or KafkaException or IOException or Azure.RequestFailedException or OperationCanceledException;

    private async Task<T> Timed<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken, TimeSpan? maximum = null)
    {
        TimeSpan timeout = maximum ?? configured.Value.BlobIoTimeout!.Value;
        long origin = clock.GetTimestamp();
        using var deadline = new CancellationTokenSource(timeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        T result = await operation(linked.Token).ConfigureAwait(false);
        CheckDeadline(origin, timeout, linked.Token);
        return result;
    }

    private void CheckDeadline(long origin, TimeSpan timeout, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (clock.GetElapsedTime(origin) >= timeout) throw new TimeoutException("Final sweep operation expired.");
    }
}

using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;

namespace Parchwell.Verifier.StartLine;

internal sealed class B10SweepLineHandler(IOptions<VerifierHostOptions> configured, TimeProvider clock,
    VerifierKafkaSessionFactory sessions, IVerifierArtifactStore artifacts, VerifierCompletionState completion) : IVerifierModeHandler
{
    public VerifierMode Mode => VerifierMode.B10SweepLine;

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        VerifierHostOptions options = configured.Value;
        DateTimeOffset started = clock.GetUtcNow();
        VerifierKafkaSession? session = null;
        byte[] bytes;
        try
        {
            session = sessions.Create(cancellationToken);
            session.InstallToken(cancellationToken);
            IReadOnlyDictionary<int, long> highs = session.ReadHighWatermarks(cancellationToken: cancellationToken);
            var note = new B10SweepLine(1, options.ScenarioId!, options.ConfigurationId!, options.RunId!, options.Phase!,
                options.SourceCommit!, VerifierKafkaSession.Topic,
                highs.OrderBy(pair => pair.Key).Select(pair => new PartitionHigh(pair.Key, pair.Value)).ToArray(),
                clock.GetUtcNow(), ParseUtc(options.B10LastVerifierCompletedUtc!), ParseUtc(options.B10LastLoadgenTerminatedUtc!),
                options.B10SettleIntervalMilliseconds!.Value);
            bytes = B10SweepEvidenceCodec.WriteSweepLine(note, new OutputLimits(options.MaxOutputBytes!.Value,
                options.MaxDiagnosticsPerCheck!.Value, options.MaxDiagnosticBytes!.Value), options.MaxPartitions!.Value);
        }
        finally
        {
            long cleanupOrigin = clock.GetTimestamp();
            using var deadline = new CancellationTokenSource(options.CleanupTimeout!.Value, clock);
            session?.Cleanup(deadline.Token);
            CheckDeadline(cleanupOrigin, options.CleanupTimeout.Value, deadline.Token);
        }

        cancellationToken.ThrowIfCancellationRequested();
        string path = PhasePaths.SweepLine(options.RunId!, options.Phase!);
        long publicationOrigin = clock.GetTimestamp();
        using var publicationDeadline = new CancellationTokenSource(options.BlobIoTimeout!.Value, clock);
        using var publication = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, publicationDeadline.Token);
        await artifacts.CreateAsync(path, bytes, publication.Token).ConfigureAwait(false);
        CheckDeadline(publicationOrigin, options.BlobIoTimeout.Value, publication.Token);
        var reference = new ArtifactReference("verifier", path, bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var receipt = new WorkerReceipt(1, "b10-sweep-line", options.ScenarioId!, options.ConfigurationId!, options.RunId!,
            options.Phase!, null, options.JobName!, options.PodName!, null, options.SourceCommit!, 0, true,
            started.ToString("O", CultureInfo.InvariantCulture), clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
            [reference], null, null);
        completion.SetPendingBlobReceipt(options.VerifierContainer!, PhasePaths.B10SweepLineCompletion(options.RunId!, options.Phase!),
            PhaseWireCodec.WriteReceipt(receipt));
        return 0;
    }

    private static DateTimeOffset ParseUtc(string value) => DateTimeOffset.ParseExact(value, "O", CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind);

    private void CheckDeadline(long origin, TimeSpan budget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (clock.GetElapsedTime(origin) >= budget) throw new TimeoutException("The verifier operation budget expired.");
    }
}

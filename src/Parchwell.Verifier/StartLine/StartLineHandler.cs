using System.Globalization;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;

namespace Parchwell.Verifier.StartLine;

/// <summary>Captures and publishes a starting boundary after native cleanup completes.</summary>
internal sealed class StartLineHandler(IOptions<VerifierHostOptions> configured, TimeProvider clock,
    VerifierKafkaSessionFactory sessions, IVerifierArtifactStore artifacts) : IVerifierModeHandler
{
    public VerifierMode Mode => VerifierMode.StartLine;

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        VerifierHostOptions options = configured.Value;
        VerifierKafkaSession? session = null;
        byte[] bytes;
        try
        {
            session = sessions.Create(cancellationToken);
            session.InstallToken(cancellationToken);
            IReadOnlyDictionary<int, long> highs = session.ReadHighWatermarks(cancellationToken: cancellationToken);
            var identity = new RunIdentity(options.ScenarioId!, options.ConfigurationId!, options.RunId!,
                options.InstanceId!, options.ExpectedInstanceId!, options.ClientId!);
            var note = new StartLineNote(1, identity, VerifierKafkaSession.Topic,
                highs.OrderBy(p => p.Key).Select(p => new PartitionHigh(p.Key, p.Value)).ToArray(),
                clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture), options.VerifierCommit!,
                typeof(ConsumerConfig).Assembly.GetName().Version!.ToString(), Library.VersionString);
            bytes = StartLineNoteCodec.Write(note, new OutputLimits(options.MaxOutputBytes!.Value,
                options.MaxDiagnosticsPerCheck!.Value, options.MaxDiagnosticBytes!.Value));
        }
        finally
        {
            long cleanupOrigin = clock.GetTimestamp();
            using var deadline = new CancellationTokenSource(options.CleanupTimeout!.Value, clock);
            session?.Cleanup(deadline.Token);
            CheckDeadline(cleanupOrigin, options.CleanupTimeout.Value, deadline.Token);
        }

        cancellationToken.ThrowIfCancellationRequested();
        long publicationOrigin = clock.GetTimestamp();
        using var publicationDeadline = new CancellationTokenSource(options.BlobIoTimeout!.Value, clock);
        using var publication = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, publicationDeadline.Token);
        await artifacts.CreateAsync(options.StartLineOutputName!, bytes, publication.Token).ConfigureAwait(false);
        CheckDeadline(publicationOrigin, options.BlobIoTimeout.Value, publication.Token);
        return 0;
    }

    private void CheckDeadline(long origin, TimeSpan budget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (clock.GetElapsedTime(origin) >= budget) throw new TimeoutException("The verifier operation budget expired.");
    }
}

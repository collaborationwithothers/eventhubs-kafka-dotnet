using System.Globalization;
using Parchwell.Verifier.Common.Consumer;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.Common.Phase;
using System.Security.Cryptography;

namespace Parchwell.Verifier.StartLine;

/// <summary>Captures and publishes a starting boundary after native cleanup completes.</summary>
internal sealed class StartLineHandler(IOptions<VerifierHostOptions> configured, TimeProvider clock,
    VerifierKafkaSessionFactory sessions, IVerifierArtifactStore artifacts, VerifierCompletionState? completion = null, ConsumerArtifactReader? consumerRecords = null) : IVerifierModeHandler
{
    public VerifierMode Mode => VerifierMode.StartLine;

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        VerifierHostOptions options = configured.Value;
        DateTimeOffset started=clock.GetUtcNow();
        VerifierKafkaSession? session = null;
        byte[] bytes;
        byte[]? inventory = null;
        byte[]? snapshot = null;
        ConsumerArtifacts? firstRead = null;
        DateTimeOffset firstStarted = default;
        bool consumerScenario=ConsumerPlanValidation.IsConsumerScenario(options.ScenarioId);
        if(consumerScenario)
        {
            firstStarted=clock.GetUtcNow();
            firstRead=await ReadConsumerRecords().ConfigureAwait(false);
        }
        try
        {
            session = sessions.Create(cancellationToken);
            session.InstallToken(cancellationToken);
            IReadOnlyDictionary<int, long> highs = session.ReadHighWatermarks(cancellationToken: cancellationToken);
            if(consumerScenario)
            {
                ConsumerArtifacts secondRead=await ReadConsumerRecords().ConfigureAwait(false);
                DateTimeOffset secondEnded=clock.GetUtcNow();
                var members=firstRead!.Members.Select(member=>
                {
                    ConsumerRegistration registration=member.Registration!;
                    ConsumerMember? second=secondRead.Members.SingleOrDefault(candidate=>candidate.Registration!.InstanceId==registration.InstanceId);
                    return new ConsumerSnapshotMember(registration.InstanceId,registration.Role,
                        member.Rows.Count==0?-1:member.Rows[^1].RowSequence,second is null||second.Rows.Count==0?-1:second.Rows[^1].RowSequence);
                }).OrderBy(member=>member.InstanceId,StringComparer.Ordinal).ToArray();
                snapshot=ConsumerArtifactCodec.WriteSnapshot(new ConsumerStartSnapshot(ConsumerPlanValidation.GroupId(options),firstStarted,secondEnded,members));
            }
            string expectedLoadGenJob = PhaseIdentity.JobName(options.RunId!, options.Phase!, options.Repeat!.Value);
            var identity = new RunIdentity(options.ScenarioId!, options.ConfigurationId!, options.RunId!,
                expectedLoadGenJob, expectedLoadGenJob, expectedLoadGenJob);
            var note = new StartLineNote(1, identity, VerifierKafkaSession.Topic,
                highs.OrderBy(p => p.Key).Select(p => new PartitionHigh(p.Key, p.Value)).ToArray(),
                clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture), options.VerifierCommit!,
                typeof(ConsumerConfig).Assembly.GetName().Version!.ToString(), Library.VersionString,
                options.ScenarioId == "B10" ? options.ExpectedProducerJobNames.ToArray() : null);
            if (options.BrokerInventoryOutputName is not null)
                inventory = BrokerInventoryCodec.Write(identity, session.MetadataBrokers!, options.VerifierCommit!,
                    options.MaxOutputBytes!.Value);
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
        if (inventory is not null)
        {
            await artifacts.CreateAsync(options.BrokerInventoryOutputName!, inventory, publication.Token).ConfigureAwait(false);
            CheckDeadline(publicationOrigin, options.BlobIoTimeout.Value, publication.Token);
        }
        if(snapshot is not null)
        {
            await artifacts.CreateAsync(PhaseIdentity.RepeatPrefix(options.RunId!,options.Phase!,options.Repeat!.Value)+"consumer-start.json",snapshot,publication.Token).ConfigureAwait(false);
            CheckDeadline(publicationOrigin,options.BlobIoTimeout.Value,publication.Token);
        }
        var receiptIdentity=new RunIdentity(options.ScenarioId!,options.ConfigurationId!,options.RunId!,
            options.ExpectedInstanceId!,options.ExpectedInstanceId!,options.ExpectedInstanceId!);
        ArtifactReference Reference(string name,byte[] content)=>new("verifier",name,content.LongLength,Convert.ToHexStringLower(SHA256.HashData(content)));
        var receipt=new WorkerReceipt(1,"start-line",options.ScenarioId!,options.ConfigurationId!,options.RunId!,options.Phase!,options.Repeat,
            options.JobName!,options.PodName!,receiptIdentity,options.SourceCommit!,0,true,started.ToString("O",CultureInfo.InvariantCulture),
            clock.GetUtcNow().ToString("O",CultureInfo.InvariantCulture),[Reference(options.StartLineOutputName!,bytes),Reference(options.BrokerInventoryOutputName!,inventory!)],null,null);
        completion?.SetPendingBlobReceipt(options.VerifierContainer!,PhasePaths.StartLineCompletion(options.RunId!,options.Phase!,options.Repeat!.Value),PhaseWireCodec.WriteReceipt(receipt));
        return 0;

        async Task<ConsumerArtifacts> ReadConsumerRecords()
        {
            if(consumerRecords is null)throw new InvalidOperationException("consumer_record_reader_missing");
            var limits=new InputLimits(options.MaxInputDepth!.Value,options.MaxArtifactBytes??ConsumerRecordLimits.MaximumBlobBytes,
                options.MaxTotalInputBytes??long.MaxValue,options.MaxRowBytes??ConsumerRecordLimits.MaximumRowBytes,options.MaxRowsPerArtifact??ConsumerRecordLimits.MaximumRowsPerBlob,
                options.MaxInputEntries??long.MaxValue,options.MaxDiagnosticBytes!.Value);
            ConsumerArtifacts read=await consumerRecords.ReadAsync(PhaseIdentity.RepeatPrefix(options.RunId!,options.Phase!,options.Repeat!.Value),new InputBudget(limits),cancellationToken,false).ConfigureAwait(false);
            if(read.Unavailable||read.Members.Any(member=>member.RegistrationInvalid||member.RecordInvalid&&member.RecordBlobs!=0))
                throw new InvalidOperationException("consumer_start_record_unavailable");
            return read;
        }
    }

    private void CheckDeadline(long origin, TimeSpan budget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (clock.GetElapsedTime(origin) >= budget) throw new TimeoutException("The verifier operation budget expired.");
    }
}

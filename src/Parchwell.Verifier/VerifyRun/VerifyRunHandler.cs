using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Parchwell.Verifier.VerifyRun.A01;
using Parchwell.Verifier.VerifyRun.Storage;
using Parchwell.Verifier.VerifyRun.Window;

namespace Parchwell.Verifier.VerifyRun;

/// <summary>Judges fixed producer inputs and one fixed Kafka window before publishing once.</summary>
internal sealed class VerifyRunHandler(IOptions<VerifierHostOptions> configured, TimeProvider clock,
    VerifierKafkaSessionFactory sessions, IVerifierArtifactStore artifacts, ILedgerArtifactSource ledgerSource,
    ScenarioLoader scenarioLoader, ScenarioResolver scenarioResolver, HelmValuesGenerator valuesGenerator) : IVerifierModeHandler
{
    private static readonly HashSet<string> JudgedPayloads = new(StringComparer.Ordinal)
    {
        "ledger.jsonl", "manifest.json", "statistics.jsonl", "request-lifecycle.jsonl", "capabilities.json"
    };

    public VerifierMode Mode => VerifierMode.Verify;

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        VerifierHostOptions options = configured.Value;
        VerifierKafkaSession? session = null;
        using var kafkaWork = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ITimer? safetyTimer = null;
        byte[] bytes;
        VerifierResultStatus status;
        try
        {
            var limits = new InputLimits(options.MaxInputDepth!.Value, options.MaxArtifactBytes!.Value,
                options.MaxTotalInputBytes!.Value, options.MaxRowBytes!.Value, options.MaxRowsPerArtifact!.Value,
                options.MaxInventoryEntries!.Value, options.MaxDiagnosticBytes!.Value);
            var budget = new InputBudget(limits);
            var outputLimits = new OutputLimits(options.MaxOutputBytes!.Value,
                options.MaxDiagnosticsPerCheck!.Value, options.MaxDiagnosticBytes!.Value);
            var identity = new RunIdentity(options.ScenarioId!, options.ConfigurationId!, options.RunId!,
                options.InstanceId!, options.ExpectedInstanceId!, options.ClientId!);
            ParseResult<ArtifactIndex> index = ArtifactIndexReader.Read(await ReadAsync("index.json").ConfigureAwait(false), limits, budget);
            if (index.IsExecutionFailure) throw new InvalidOperationException("The producer artifact index is missing or malformed.");
            ParseResult<IReadOnlyList<LedgerRow>> ledger = LedgerReader.ReadRows(await ReadAsync("ledger.jsonl").ConfigureAwait(false), limits, budget);
            ParseResult<SealedRunManifest> manifest = ManifestReader.Read(await ReadAsync("manifest.json").ConfigureAwait(false), limits, budget, options.MaxInventoryEntries.Value);
            ParseResult<IReadOnlyList<StatisticsEnvelope>> statistics = CapturedEvidenceReader.ReadStatistics(
                await ReadAsync("statistics.jsonl").ConfigureAwait(false), "statistics.jsonl", limits, budget);
            ParseResult<IReadOnlyList<LifecycleEvent>> lifecycle = CapturedEvidenceReader.ReadLifecycle(
                await ReadAsync("request-lifecycle.jsonl").ConfigureAwait(false), "request-lifecycle.jsonl", limits, budget);
            ParseResult<CapturedCapabilities> capabilities = CapturedEvidenceReader.ReadCapabilities(
                await ReadAsync("capabilities.json").ConfigureAwait(false), "capabilities.json", limits, budget);
            ReportAssociation[] reports = statistics.Value?.Select(envelope => new ReportAssociation(envelope,
                NativeStatisticsReader.Read(Encoding.UTF8.GetBytes(envelope.RawJson), "statistics.jsonl", limits,
                    budget, options.MaxNativeReportBytes!.Value, options.MaxBrokerEntries!.Value))).ToArray() ?? [];
            ResolvedRoleConfiguration producer;
            long localOrigin = clock.GetTimestamp();
            using (var deadline = new CancellationTokenSource(options.IoTimeout!.Value, clock))
            using (var local = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token))
            {
                LoadedScenario scenario = await scenarioLoader.LoadAsync(options.ScenarioId!, local.Token).ConfigureAwait(false);
                CheckDeadline(localOrigin, options.IoTimeout.Value, local.Token);
                ResolvedScenario resolved = scenarioResolver.Resolve(scenario);
                if (options.GeneratedValuesFile is not null)
                    GeneratedValuesCompatibility.Validate(valuesGenerator, resolved, options.ConfigurationId!, options.GeneratedValuesFile);
                producer = resolved.Configurations[options.ConfigurationId!].Roles[KafkaClientRole.LoadGenProducer];
                CheckDeadline(localOrigin, options.IoTimeout.Value, local.Token);
            }
            ReadOnlyMemory<byte>? noteBytes;
            long noteOrigin = clock.GetTimestamp();
            using (var deadline = new CancellationTokenSource(options.BlobIoTimeout!.Value, clock))
            using (var read = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token))
            {
                byte[]? raw = await artifacts.ReadStartLineAsync(options.StartLineInputName!, read.Token).ConfigureAwait(false);
                CheckDeadline(noteOrigin, options.BlobIoTimeout.Value, read.Token);
                noteBytes = raw is null ? (ReadOnlyMemory<byte>?)null : new ReadOnlyMemory<byte>(raw);
            }
            if (noteBytes is not null) budget.ConsumeBytes(noteBytes.Value.Length);
            ParseResult<StartLineNote> start = noteBytes is null
                ? ParseResult<StartLineNote>.Failure(new StructuralProblem(7, "missing_input", options.StartLineInputName!, null, null, null))
                : StartLineNoteCodec.Read(noteBytes.Value, options.StartLineInputName!, limits, options.MaxPartitions!.Value);
            IReadOnlyDictionary<int, long>? ends = null;
            var assessments = new List<CheckAssessment>();
            long origin = 0;
            try
            {
                session = sessions.Create(kafkaWork.Token);
                session.InstallToken(cancellationToken);
                ends = session.ReadHighWatermarks(() =>
                {
                    origin = clock.GetTimestamp();
                    safetyTimer = clock.CreateTimer(_ => kafkaWork.Cancel(), null, options.SafetyStop!.Value, Timeout.InfiniteTimeSpan);
                }, () => options.SafetyStop!.Value - clock.GetElapsedTime(origin), kafkaWork.Token);
                assessments.Add(RunWindow.Pass(11));
            }
            catch (VerifierKafkaOperationException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                assessments.Add(RunWindow.Failure(11));
            }
            RunWindow window = RunWindow.Create(start, identity, ends);
            assessments.AddRange(window.Assessments);
            WindowDrainResult? drain = null;
            if (window.CanAssign)
            {
                drain = WindowConsumer.Drain(session!.Consumer, window, clock, origin, options.SafetyStop!.Value,
                    options.PollSlice!.Value, new ConsumedRecordBudget(options.MaxConsumedRecords!.Value,
                        options.MaxConsumedBytes!.Value), cancellationToken, kafkaWork.Token);
                assessments.AddRange(drain.Assessments);
            }
            var request = new RunJudgementRequest(identity, index, ledger,
                manifest, statistics, lifecycle, capabilities, reports, options.ExpectedBrokerNames!, producer,
                drain?.Records ?? [], assessments, outputLimits);
            RunJudgementResult judgement = RunJudgement.Evaluate(request);
            A01EvidenceRecording a01 = options.ConfigurationId == "C3"
                ? A01EvidenceRecorder.RecordC3(request, judgement)
                : A01EvidenceRecorder.Record(request, judgement);
            status = judgement.Status ?? throw new InvalidOperationException("The available evidence does not establish a verifier result status.");
            var result = new VerifierResult(1, identity, manifest.Value?.GeneratorVersion, manifest.Value?.RuntimeVersions,
                options.VerifierCommit!, typeof(ConsumerConfig).Assembly.GetName().Version!.ToString(), Library.VersionString,
                status, judgement.FailedChecks,
                start.Value?.PartitionHighs.Select(p => new WatermarkOffset(p.Partition, p.High)).ToArray(),
                ends?.OrderBy(p => p.Key).Select(p => new WatermarkOffset(p.Key, p.Value)).ToArray(),
                Milliseconds(drain?.DrainDuration), Milliseconds(drain?.StopDuration), judgement.Counts, judgement.BrokerCount,
                judgement.ConfigurationFidelity, a01.ProbeObservation, a01.RecordedEvidence,
                index.Value!.Artifacts.Where(a => JudgedPayloads.Contains(a.Name)).Select(a => new JudgedFileHash(a.Name, a.Sha256)).ToArray(),
                judgement.Diagnostics.Concat(a01.Diagnostics).ToArray());
            bytes = VerifierResultCodec.Write(result, outputLimits);

            async Task<ReadOnlyMemory<byte>?> ReadAsync(string name)
            {
                long readOrigin = clock.GetTimestamp();
                using var deadline = new CancellationTokenSource(options.BlobIoTimeout!.Value, clock);
                using var read = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
                byte[]? raw = await ledgerSource.ReadAsync(name, read.Token).ConfigureAwait(false);
                CheckDeadline(readOrigin, options.BlobIoTimeout.Value, read.Token);
                return raw is null ? (ReadOnlyMemory<byte>?)null : new ReadOnlyMemory<byte>(raw);
            }
        }
        finally
        {
            safetyTimer?.Dispose();
            long cleanupOrigin = clock.GetTimestamp();
            using var deadline = new CancellationTokenSource(options.CleanupTimeout!.Value, clock);
            session?.Cleanup(deadline.Token);
            CheckDeadline(cleanupOrigin, options.CleanupTimeout.Value, deadline.Token);
        }
        cancellationToken.ThrowIfCancellationRequested();
        long publicationOrigin = clock.GetTimestamp();
        using var publicationDeadline = new CancellationTokenSource(options.BlobIoTimeout!.Value, clock);
        using var publication = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, publicationDeadline.Token);
        await artifacts.CreateAsync(options.ResultOutputName!, bytes, publication.Token).ConfigureAwait(false);
        CheckDeadline(publicationOrigin, options.BlobIoTimeout.Value, publication.Token);
        return status == VerifierResultStatus.CompleteValid ? 0 : 1;
    }

    private void CheckDeadline(long origin, TimeSpan budget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (clock.GetElapsedTime(origin) >= budget) throw new TimeoutException("The verifier operation budget expired.");
    }

    private static decimal? Milliseconds(TimeSpan? duration) => duration is null ? null : duration.Value.Ticks / (decimal)TimeSpan.TicksPerMillisecond;
}

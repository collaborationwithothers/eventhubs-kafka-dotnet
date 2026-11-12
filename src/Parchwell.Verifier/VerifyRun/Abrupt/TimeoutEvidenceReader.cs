using System.Security.Cryptography;
using System.Text;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;

namespace Parchwell.Verifier.VerifyRun.Abrupt;

internal sealed record TimeoutRegistration(string InstanceId, ProducerRegistration Registration, BlobListing Listing);

internal sealed record TimeoutEvidence(TimeoutRegistration Registered,
    ParseResult<ArtifactIndex> Index, ParseResult<IReadOnlyList<LedgerRow>> Ledger,
    ParseResult<SealedRunManifest> Manifest, ParseResult<IReadOnlyList<StatisticsEnvelope>> Statistics,
    ParseResult<IReadOnlyList<LifecycleEvent>> Lifecycle, ParseResult<CapturedCapabilities> Capabilities,
    IReadOnlyList<ReportAssociation> Reports)
{
    internal string InstanceId => Registered.InstanceId;
}

internal sealed record TimeoutRecordJoin(IReadOnlyList<ConsumedRecord> Records, bool Unjoined);

/// <summary>Reads and judges the ordinary timeout instance independently of abrupt probe evidence.</summary>
internal sealed class TimeoutEvidenceReader(VerifierHostOptions options,
    Func<string, long, CancellationToken, Task<byte[]?>> readLedger,
    Func<string, CancellationToken, Task<BlobListing>> listLedger, InputLimits limits, InputBudget budget)
{
    internal async Task<TimeoutRegistration> ReadListingAsync(CancellationToken cancellationToken)
    {
        string root = PhasePaths.ProducersPrefix(options.RunId!, "C1", 1);
        BlobListing listing = await listLedger(root, cancellationToken).ConfigureAwait(false);
        Require(listing.IsComplete, "timeout_listing_incomplete");
        budget.ConsumeEntries(listing.Names.Count);
        string? instance = null;
        foreach (string name in listing.Names)
        {
            Require(name.StartsWith(root, StringComparison.Ordinal), "timeout_listing_path");
            string tail = name[root.Length..];
            int separator = tail.IndexOf('/', StringComparison.Ordinal);
            Require(separator > 0 && separator < tail.Length - 1, "timeout_listing_path");
            string candidate = tail[..separator];
            _ = PhasePaths.ProducerPrefix(options.RunId!, "C1", 1, candidate);
            Require(instance is null || instance == candidate, "timeout_registration_count");
            instance = candidate;
        }
        Require(instance is not null, "timeout_registration_count");
        string registrationName = PhasePaths.Registration(options.RunId!, "C1", 1, instance!);
        Require(listing.Names.Count(name => name == registrationName) == 1, "timeout_registration_count");
        byte[] bytes = await ReadRequired(registrationName, cancellationToken).ConfigureAwait(false);
        budget.ConsumeBytes(bytes.LongLength);
        ProducerRegistration registration = PhaseWireCodec.ReadRegistration(bytes);
        Require(registration.Identity.RunId == options.RunId && registration.Identity.ScenarioId == "B10" &&
            registration.Identity.ConfigurationId == "C1" && registration.Identity.InstanceId == instance &&
            registration.Identity.ExpectedInstanceId == instance && registration.PodName == instance &&
            registration.Phase == "C1" && registration.RepeatNumber == 1 &&
            registration.JobName == PhaseIdentity.JobName(options.RunId!, "C1", 1) &&
            registration.PredecessorInstanceId is null && registration.SourceCommit == options.VerifierCommit,
            "timeout_registration_binding");
        return new(instance!, registration, listing);
    }

    internal async Task<TimeoutEvidence> ReadPublicationAsync(TimeoutRegistration registered,
        StartLineEvidence start, CancellationToken cancellationToken)
    {
        string producerPrefix = PhasePaths.ProducerPrefix(options.RunId!, "C1", 1, registered.InstanceId);
        string completionName = PhasePaths.ProducerCompletion(options.RunId!, "C1", 1, registered.InstanceId);
        byte[] completionBytes = await ReadRequired(completionName, cancellationToken).ConfigureAwait(false);
        budget.ConsumeBytes(completionBytes.LongLength);
        ProducerCompletionReceipt completion = PhaseWireCodec.ReadProducerCompletion(completionBytes, "C1", 1, registered.Registration);
        var raw = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (string name in TimeoutPublicationAcceptance.PublicationNames)
            raw[name] = await readLedger(producerPrefix + "publication/" + name,
                options.MaxArtifactBytes!.Value, cancellationToken).ConfigureAwait(false);

        ParseResult<ArtifactIndex> index = ArtifactIndexReader.Read(raw["index.json"], limits, budget);
        ParseResult<IReadOnlyList<LedgerRow>> ledger = LedgerReader.ReadRows(Memory("ledger.jsonl"), limits, budget);
        ParseResult<SealedRunManifest> manifest = ManifestReader.Read(Memory("manifest.json"), limits, budget, options.MaxInventoryEntries!.Value);
        ParseResult<IReadOnlyList<StatisticsEnvelope>> statistics = CapturedEvidenceReader.ReadStatistics(Memory("statistics.jsonl"), "statistics.jsonl", limits, budget);
        ParseResult<IReadOnlyList<LifecycleEvent>> lifecycle = CapturedEvidenceReader.ReadLifecycle(Memory("request-lifecycle.jsonl"), "request-lifecycle.jsonl", limits, budget);
        ParseResult<CapturedCapabilities> capabilities = CapturedEvidenceReader.ReadCapabilities(Memory("capabilities.json"), "capabilities.json", limits, budget, captureOnly: true);
        ReportAssociation[] reports = statistics.Value?.Select(envelope => new ReportAssociation(envelope,
            NativeStatisticsReader.Read(Encoding.UTF8.GetBytes(envelope.RawJson), "statistics.jsonl", limits,
                budget, options.MaxNativeReportBytes!.Value, options.MaxBrokerEntries!.Value))).ToArray() ?? [];
        var acceptance = TimeoutPublicationAcceptance.Evaluate(registered.Listing, producerPrefix, raw,
            registered.Registration, completion, index.Value, manifest.Value, start.Start.Value,
            start.Inventory.Value, options.VerifierCommit!,
            start.Receipt is null || start.NoteBytes is null ? null : Reference("verifier", start.StartName, start.NoteBytes),
            start.Receipt is null || start.InventoryBytes is null ? null : Reference("verifier", start.InventoryName, start.InventoryBytes),
            Reference("ledger", completionName, completionBytes), options.MaxArtifactBytes!.Value,
            options.MaxTotalInputBytes!.Value, options.ExpectedProducerJobNames);
        Require(acceptance.Accepted, "timeout_publication_invalid");
        return new(registered, index, ledger, manifest, statistics, lifecycle, capabilities, reports);

        ReadOnlyMemory<byte>? Memory(string name) => raw[name] is { } bytes ? new ReadOnlyMemory<byte>(bytes) : null;
    }

    internal static TimeoutRecordJoin Join(TimeoutEvidence evidence, IReadOnlyList<ConsumedRecord> records, string runId)
    {
        var rows = evidence.Ledger.Value?.ToLookup(row => row.Attempt.AttemptId, StringComparer.Ordinal);
        var joined = new List<ConsumedRecord>();
        bool unjoined = false;
        string crossKey = $"B10/{runId}/C1/1/cross_restart";
        string sameKey = $"B10/{runId}/C1/1/same_instance";
        foreach (ConsumedRecord record in records.Where(record => record.InstanceId == evidence.InstanceId))
        {
            LedgerRow[] matches = record.AttemptId is null || rows is null ? [] : rows[record.AttemptId].ToArray();
            if (record.Problems.Count != 0 || matches.Length != 1 || record.Key is null || record.Sequence is null ||
                record.Key == crossKey || record.Key == sameKey ||
                matches[0].Attempt.Key != record.Key || matches[0].Attempt.Sequence != record.Sequence)
                unjoined = true;
            else joined.Add(record);
        }
        return new(joined, unjoined);
    }

    internal static OutcomeCounts? Judge(TimeoutEvidence evidence, IReadOnlyList<ConsumedRecord> records,
        ResolvedRoleConfiguration configuration, IReadOnlyList<string> expectedBrokerNames, OutputLimits outputLimits)
    {
        // The caller has established the diagnostic mode's start-line and drain
        // checks. Scientific two-producer start-line checks are not substituted.
        CheckAssessment[] window = Enumerable.Range(7, 5).Select(check => CheckResult.Pass(check).Assessment).ToArray();
        var request = new RunJudgementRequest(evidence.Registered.Registration.Identity, evidence.Index,
            evidence.Ledger, evidence.Manifest, evidence.Statistics, evidence.Lifecycle, evidence.Capabilities,
            evidence.Reports, expectedBrokerNames, configuration, records, window, outputLimits);
        RunJudgementResult result = RunJudgement.Evaluate(request);
        if (result.Status != VerifierResultStatus.CompleteValid) return null;
        var rows = evidence.Ledger.Value!.ToDictionary(row => (row.Attempt.InstanceId, row.Attempt.AttemptId));
        var latest = new Dictionary<(string Key, long Sequence), LedgerRow>();
        foreach (AttemptEntry attempt in evidence.Manifest.Value!.Inventory)
        {
            var key = (attempt.Key, attempt.Sequence);
            if (latest.TryGetValue(key, out LedgerRow? previous) && !B10RepeatChecks.IsPermittedTimeout(previous))
                return null;
            latest[key] = rows[(attempt.InstanceId, attempt.AttemptId)];
        }
        return OutcomeClassifier.Classify(evidence.Ledger.Value!, records);
    }

    private async Task<byte[]> ReadRequired(string name, CancellationToken cancellationToken) =>
        await readLedger(name, options.MaxArtifactBytes!.Value, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("timeout_object_missing");

    private static ArtifactReference Reference(string container, string name, byte[] bytes) =>
        new(container, name, bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(bytes)));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}

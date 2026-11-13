using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.VerifyRun.Configuration;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Window;
using Parchwell.Verifier.Common.Kafka;
using static Parchwell.Verifier.VerifyRun.Abrupt.AbruptEvidenceCodec;

namespace Parchwell.Verifier.VerifyRun.Abrupt;

internal sealed class B10AbruptVerifyHandler(IOptions<VerifierHostOptions> configured, TimeProvider clock,
    VerifierKafkaSessionFactory sessions, IVerifierArtifactStore verifierStore, IProducerArtifactStore producerStore,
    ScenarioLoader scenarioLoader, RegisteredProducerConfigurationResolver configurationResolver)
    : IVerifierModeHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private static readonly string[] ProblemNames =
    [
        "b10_abrupt_verify_start_line_invalid", "b10_abrupt_verify_first_evidence_invalid",
        "b10_abrupt_verify_successor_absent", "b10_abrupt_verify_successor_evidence_invalid",
        "b10_abrupt_verify_window_incomplete", "b10_abrupt_verify_probe_record_malformed",
        "b10_abrupt_verify_timeout_evidence_invalid", "b10_abrupt_verify_record_unjoined",
        "b10_abrupt_verify_end_line_advanced", "b10_abrupt_verify_earlier_repeat_unavailable"
    ];
    public VerifierMode Mode => VerifierMode.B10AbruptVerify;

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        VerifierHostOptions options = configured.Value;
        DateTimeOffset started = clock.GetUtcNow();
        bool scientific = VerifierHostOptions.IsB10ScientificRunId(options.RunId);
        long? earlierRepeatRecords = options.Repeat == 1 ? 0 : null;
        var limits = new InputLimits(options.MaxInputDepth!.Value, options.MaxArtifactBytes!.Value,
            options.MaxTotalInputBytes!.Value, options.MaxRowBytes!.Value, options.MaxRowsPerArtifact!.Value,
            options.MaxInputEntries!.Value, options.MaxDiagnosticBytes!.Value);
        var inputBudget = new InputBudget(limits);
        var readBudget = new InputBudget(limits);
        var problems = new HashSet<int>();
        var details = new List<object>();
        var reasons = new HashSet<int>();
        var outputLimits = new OutputLimits(options.MaxOutputBytes!.Value, options.MaxDiagnosticsPerCheck!.Value, options.MaxDiagnosticBytes!.Value);
        string prefix = $"{options.RunId}/C1/r{options.Repeat}/" + (scientific ? "b10-abrupt/" : "b10-abrupt-diagnostic/");
        var reader = new AbruptEvidenceReader(options, ReadLedger);
        StartLineEvidence? start = null;
        AbruptFirstEvidence? first = null;
        AbruptSuccessorEvidence? successor = null;
        TimeoutRegistration? timeoutRegistration = null;
        TimeoutEvidence? timeoutEvidence = null;
        OutcomeCounts? timeout = null;
        string? successorDirectory = null;
        bool lossVerified = true;
        bool drainedWindow = false;
        LoadedScenario? scenario = null;
        IReadOnlyList<string> expectedBrokers = [];
        var timeoutReader = new TimeoutEvidenceReader(options, ReadProducer, ListProducer, limits, inputBudget);
        IReadOnlyDictionary<int, long>? ends = null;
        IReadOnlyList<ConsumedRecord> records = [];
        bool complete = false;
        try
        {
            start = await StartLineEvidenceReader.ReadAsync(options, limits, inputBudget, ReadVerifier, cancellationToken).ConfigureAwait(false);
            RunWindow check = RunWindow.Create(start.Start, [], start.ExpectedIdentity, options.ExpectedProducerJobNames);
            if (start.Receipt is null || !start.Inventory.IsSuccess || check.Assessments.Any(assessment => assessment.Disposition == AssessmentDisposition.Fail))
            {
                Problem(0, "start_line");
                start = null;
            }
        }
        catch (Exception exception) when (EvidenceFailure(exception)) { Problem(0, "start_line"); start = null; }

        if (start is not null)
            expectedBrokers = start.Inventory.Value!.Brokers.Select(broker => broker.Name).Order(StringComparer.Ordinal).ToArray();
        try
        {
            timeoutRegistration = await timeoutReader.ReadListingAsync(cancellationToken).ConfigureAwait(false);
            if (start is not null)
                timeoutEvidence = await timeoutReader.ReadPublicationAsync(timeoutRegistration, start, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (EvidenceFailure(exception)) { Problem(6, "timeout_evidence"); }

        string[] directories = [];
        Dictionary<string, string[]> namesByPod = new(StringComparer.Ordinal);
        var registeredAbruptInstances = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            BlobListing listing = await Timed(ct => producerStore.ListAsync(prefix, ct), options.BlobIoTimeout!.Value, cancellationToken).ConfigureAwait(false);
            if (!listing.IsComplete) throw new AbruptEvidenceException("listing_incomplete", new HashSet<int> { 2 });
            Require(listing.Names.All(name => name.StartsWith(prefix, StringComparison.Ordinal) && name[prefix.Length..].Contains('/')), "listing_path");
            namesByPod = listing.Names.GroupBy(name => name[prefix.Length..].Split('/')[0], StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            directories = namesByPod.Keys.Order(StringComparer.Ordinal).ToArray();
            var candidates = new List<(string Pod, string Uid)>();
            foreach (string pod in directories)
            {
                try
                {
                    if (scientific)
                    {
                        var registration = await reader.ReadScientificRegistrationAsync(prefix, pod, cancellationToken).ConfigureAwait(false);
                        registeredAbruptInstances.Add(pod);
                        if (registration.Predecessor is null) candidates.Add((pod, registration.Uid));
                        continue;
                    }
                    string? uid = await reader.ReadRegistrationAsync(prefix, pod, null, cancellationToken).ConfigureAwait(false);
                    candidates.Add((pod, uid!));
                    registeredAbruptInstances.Add(pod);
                }
                catch (Exception exception) when (EvidenceFailure(exception)) { /* This directory may be the successor. */ }
            }
            if (candidates.Count != 1) throw new AbruptEvidenceException("first_registration_count", new HashSet<int> { 2 });
            (string firstPod, string firstUid) = candidates[0];
            first = await reader.ReadFirstAsync(prefix, firstPod, firstUid, namesByPod[firstPod], cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (EvidenceFailure(exception))
        {
            Problem(1, "first_evidence");
            AddEvidenceReasons(exception, 3);
        }
        if (first is not null)
        {
            reasons.UnionWith(first.Reasons);
            if (first.Invalid) Problem(1, "first_reconciliation");
        }

        if (first is not null && !problems.Contains(1))
        {
            string[] remaining = directories.Where(pod => pod != first.Pod).ToArray();
            if (remaining.Length == 0) Problem(2, "successor_absent");
            else
            {
                try
                {
                    Require(remaining.Length == 1, "successor_count");
                    await reader.ReadRegistrationAsync(prefix, remaining[0], first.Pod, cancellationToken).ConfigureAwait(false);
                    successorDirectory = remaining[0];
                    registeredAbruptInstances.Add(remaining[0]);
                    successor = await reader.ReadSuccessorAsync(prefix, remaining[0], first, namesByPod[remaining[0]], cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (EvidenceFailure(exception))
                {
                    Problem(3, "successor_evidence");
                    AddEvidenceReasons(exception, 9);
                }
                if (successor is not null)
                {
                    reasons.UnionWith(successor.Reasons);
                    if (successor.Invalid) Problem(3, "successor_reconciliation");
                }
            }
        }
        if (start is not null)
        {
            VerifierKafkaSession? session = null;
            using var kafkaWork = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ITimer? safetyTimer = null;
            try
            {
                session = sessions.Create(kafkaWork.Token);
                session.InstallToken(cancellationToken);
                long origin = 0;
                ends = session.ReadHighWatermarks(() =>
                {
                    origin = clock.GetTimestamp();
                    safetyTimer = clock.CreateTimer(_ => kafkaWork.Cancel(), null, options.SafetyStop!.Value, Timeout.InfiniteTimeSpan);
                }, () => options.SafetyStop!.Value - clock.GetElapsedTime(origin), kafkaWork.Token);
                RunWindow window = RunWindow.Create(start.Start, [], start.ExpectedIdentity, options.ExpectedProducerJobNames, ends);
                if (window.CanAssign)
                {
                    var keys = first?.Attempts.Values.Select(attempt => attempt.Key).ToHashSet(StringComparer.Ordinal)
                        ?? new HashSet<string>(StringComparer.Ordinal);
                    WindowDrainResult drained = WindowConsumer.Drain(session.Consumer, window, clock, origin, options.SafetyStop!.Value,
                        options.PollSlice!.Value, new ConsumedRecordBudget(options.MaxConsumedRecords!.Value, options.MaxConsumedBytes!.Value),
                        keys, cancellationToken, kafkaWork.Token);
                    records = drained.Records;
                    drainedWindow = true;
                    complete = drained.Assessments.All(assessment => assessment.Disposition == AssessmentDisposition.Pass);
                    if (scientific && options.Repeat >= 2)
                    {
                        try
                        {
                            var currentInstances = timeoutReader.DirectoryInstanceIds.ToHashSet(StringComparer.Ordinal);
                            currentInstances.UnionWith(registeredAbruptInstances);
                            IReadOnlySet<string> earlier = await EarlierRepeatEvidenceReader.ReadAsync(options,
                                currentInstances, inputBudget, ListProducer, ReadProducer, cancellationToken).ConfigureAwait(false);
                            earlierRepeatRecords = records.LongCount(record => record.InstanceId is not null && earlier.Contains(record.InstanceId));
                            records = records.Where(record => record.InstanceId is null || !earlier.Contains(record.InstanceId)).ToArray();
                        }
                        catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                            (EvidenceFailure(exception) || exception is TimeoutException or OperationCanceledException or IOException or Azure.RequestFailedException))
                        {
                            Problem(9, "earlier_repeat_evidence");
                        }
                    }
                    if (!problems.Contains(1) && records.Any(record => record.Key is not null && keys.Contains(record.Key) &&
                        (record.Problems.Count != 0 || record.ValueSha256 is null))) Problem(5, "probe_identity");
                }
                if (!complete) Problem(4, "window_incomplete");
                safetyTimer?.Dispose();
                safetyTimer = null;
                if (complete && first is not null && !problems.Contains(1))
                {
                    IReadOnlyList<AbruptArm> preliminary = AbruptClassification.Evaluate(first,
                        problems.Contains(3) ? null : successor, records, true, options.MaxDiagnosticsPerCheck!.Value);
                    if (preliminary.Any(arm => arm.Members.Any(member => member.ResendLost == true)))
                    {
                        try
                        {
                            IReadOnlyDictionary<int, long> reread = session.ReadHighWatermarks(cancellationToken: cancellationToken);
                            lossVerified = ends.Count == reread.Count && ends.All(end => reread.TryGetValue(end.Key, out long high) && high == end.Value);
                        }
                        catch (Exception exception) when (KafkaFailure(exception, cancellationToken)) { lossVerified = false; }
                        if (!lossVerified) Problem(8, "end_line_advanced_or_unreadable");
                    }
                }
            }
            catch (Exception exception) when (KafkaFailure(exception, cancellationToken))
            {
                Problem(4, "window_incomplete");
            }
            finally
            {
                safetyTimer?.Dispose();
                long cleanupOrigin = clock.GetTimestamp();
                using var cleanupDeadline = new CancellationTokenSource(options.CleanupTimeout!.Value, clock);
                session?.Cleanup(cleanupDeadline.Token);
                CheckCleanup(cleanupOrigin, options.CleanupTimeout.Value);
            }
        }
        if (drainedWindow)
        {
            if (timeoutEvidence is not null)
            {
                TimeoutRecordJoin joined = TimeoutEvidenceReader.Join(timeoutEvidence, records, options.RunId!, options.Repeat!.Value);
                if (joined.Unjoined) Problem(7, "timeout_record_identity");
                if (!problems.Contains(4))
                {
                    scenario = await Timed(ct => scenarioLoader.LoadAsync(options.ScenarioId!, ct).AsTask(), options.IoTimeout!.Value, cancellationToken).ConfigureAwait(false);
                    ResolvedRoleConfiguration expected = configurationResolver.Resolve(scenario, timeoutEvidence.Registered.Registration)
                        .Configurations["C1"].Roles[KafkaClientRole.LoadGenProducer];
                    timeout = TimeoutEvidenceReader.Judge(timeoutEvidence, joined.Records, expected, expectedBrokers, outputLimits);
                    if (timeout is null) Problem(6, "timeout_judgement");
                }
            }
            if (first is not null && !problems.Contains(1) &&
                AbruptRecordJoin.HasUnjoined(first, problems.Contains(3) ? null : successor, records,
                    timeoutReader.DirectoryInstanceIds, successorDirectory))
                Problem(7, "probe_record_identity");
        }
        if (start is not null && first is not null)
        {
            scenario ??= await Timed(ct => scenarioLoader.LoadAsync(options.ScenarioId!, ct).AsTask(), options.IoTimeout!.Value, cancellationToken).ConfigureAwait(false);
            string client = successor?.Pod ?? first.Pod;
            var registration = new ProducerRegistration(1, new("B10", "C1", options.RunId!, client, client, client),
                "C1", options.Repeat!.Value, client, client, successor is null ? first.Uid : client, options.VerifierCommit!);
            ResolvedRoleConfiguration expected = configurationResolver.Resolve(scenario, registration)
                .Configurations["C1"].Roles[KafkaClientRole.LoadGenProducer];
            if (!AbruptGuard.Evaluate(first, successor, options, expectedBrokers, expected, limits, inputBudget)) reasons.Add(13);
        }
        IReadOnlyList<AbruptArm>? arms = first is null || problems.Contains(1) ? null :
            AbruptClassification.Evaluate(first, problems.Contains(3) ? null : successor, records, complete,
                options.MaxDiagnosticsPerCheck!.Value, lossVerified);
        string comparison = AbruptDiagnostics.Comparison(arms, reasons);
        JsonObject resultObject = JsonSerializer.SerializeToNode(new
        {
            schema_version = 3, dialect = "b10_abrupt", purpose = scientific ? "scientific" : "diagnostic_trial", synthetic = true,
            scientific_admissible = false, kind = "nonfinal_result",
            identity = new
            {
                run_id = options.RunId, scenario_id = "B10", configuration_id = "C1", phase = "C1", repeat = options.Repeat,
                source_commit = options.VerifierCommit, image_reference = options.VerifierImage,
                job_name = options.JobName, pod_name = options.PodName, pod_uid = options.PodUid,
                instance_id = options.PodName, predecessor_instance_id = (string?)null
            },
            data = new
            {
                first_instance_id = problems.Contains(1) ? null : first?.Pod,
                successor_instance_id = problems.Contains(3) ? null : successor?.Pod,
                timeout_instance_id = timeoutRegistration?.InstanceId, timeout,
                comparison, native_errors = problems.Contains(1) ? null : first?.NativeErrors,
                eligibility = reasons.Count == 0 ? "pending_sweep" : "ineligible",
                eligibility_reasons = reasons.Order().Select(reason => AbruptDiagnostics.ReasonNames[reason - 1]).ToArray(),
                start_line = start?.Start.Value?.PartitionHighs.OrderBy(high => high.Partition).Select(high => new AbruptCoordinate(high.Partition, high.High)).ToArray(),
                end_line = ends?.OrderBy(high => high.Key).Select(high => new AbruptCoordinate(high.Key, high.Value)).ToArray(),
                window_complete = complete, window_limit = "copies_after_end_line_not_observed", arms,
                problems = problems.Order().Select(index => ProblemNames[index]).ToArray(), problem_details = details, finality = "nonfinal"
            }
        }, JsonOptions)!.AsObject();
        if (scientific) resultObject["data"]!["earlier_repeat_records"] = earlierRepeatRecords;
        byte[] result = JsonSerializer.SerializeToUtf8Bytes(resultObject, JsonOptions);
        if (result.LongLength > options.MaxOutputBytes!.Value) throw new InvalidDataException("Diagnostic result exceeds MaxOutputBytes.");
        string resultName = prefix + "nonfinal-result.json";
        await Timed(async ct => { await verifierStore.CreateAsync(resultName, result, ct).ConfigureAwait(false); return true; },
            options.BlobIoTimeout!.Value, cancellationToken).ConfigureAwait(false);
        byte[]? readBack = await ReadVerifier(resultName, cancellationToken).ConfigureAwait(false);
        if (readBack is null || !result.AsSpan().SequenceEqual(readBack)) return 1;
        if (scientific)
        {
            var reference = new ArtifactReference("verifier", resultName, result.LongLength,
                Convert.ToHexStringLower(SHA256.HashData(result)));
            var receipt = new WorkerReceipt(1, "b10-abrupt-verify", "B10", "C1", options.RunId!, "C1", options.Repeat,
                options.JobName!, options.PodName!, null, options.VerifierCommit!, 0, true,
                started.ToString("O", CultureInfo.InvariantCulture), clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
                [reference], null, null);
            byte[] receiptBytes = PhaseWireCodec.WriteReceipt(receipt);
            string receiptName = prefix + "verify-completion.json";
            await Timed(async ct => { await verifierStore.CreateAsync(receiptName, receiptBytes, ct).ConfigureAwait(false); return true; },
                options.BlobIoTimeout!.Value, cancellationToken).ConfigureAwait(false);
            byte[]? receiptReadBack = await Timed(ct => verifierStore.ReadAsync(receiptName, ct),
                options.BlobIoTimeout!.Value, cancellationToken).ConfigureAwait(false);
            if (receiptReadBack is null || !receiptBytes.AsSpan().SequenceEqual(receiptReadBack)) return 1;
        }
        return 0;

        void Problem(int index, string detail)
        {
            switch (index)
            {
                case 0: reasons.Add(1); break;
                case 2: reasons.Add(9); break;
                case 4: reasons.Add(12); break;
                case 5: case 7: case 9: reasons.Add(3); break;
                case 6: reasons.Add(7); break;
            }
            if (!problems.Add(index) || index == 7 || details.Count >= options.MaxDiagnosticsPerCheck!.Value) return;
            details.Add(new { problem = ProblemNames[index], detail = detail[..Math.Min(detail.Length, options.MaxDiagnosticBytes!.Value)] });
        }
        void AddEvidenceReasons(Exception exception, int fallback)
        {
            if (exception is AbruptEvidenceException evidence) reasons.UnionWith(evidence.Reasons);
            else reasons.Add(fallback);
        }
        async Task<BlobListing> ListProducer(string name, CancellationToken ct) =>
            await Timed(token => producerStore.ListAsync(name, token), options.BlobIoTimeout!.Value, ct).ConfigureAwait(false);
        async Task<byte[]?> ReadProducer(string name, long maximumBytes, CancellationToken ct)
        {
            byte[]? bytes = await Timed(token => producerStore.ReadBoundedAsync(name, maximumBytes, token), options.BlobIoTimeout!.Value, ct).ConfigureAwait(false);
            if (bytes is not null) readBudget.ConsumeBytes(bytes.LongLength);
            return bytes;
        }
        async Task<byte[]?> ReadVerifier(string name, CancellationToken ct)
        {
            byte[]? bytes = await Timed(token => verifierStore.ReadAsync(name, token), options.BlobIoTimeout!.Value, ct).ConfigureAwait(false);
            if (bytes is not null && name != prefix + "nonfinal-result.json") readBudget.ConsumeBytes(bytes.LongLength);
            return bytes;
        }
        Task<byte[]?> ReadLedger(string name, long maximumBytes, CancellationToken ct) => ReadProducer(name, maximumBytes, ct);
    }

    private async Task<T> Timed<T>(Func<CancellationToken, Task<T>> action, TimeSpan budget, CancellationToken cancellationToken)
    {
        long origin = clock.GetTimestamp();
        using var deadline = new CancellationTokenSource(budget, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        T result = await action(linked.Token).ConfigureAwait(false);
        linked.Token.ThrowIfCancellationRequested();
        if (clock.GetElapsedTime(origin) >= budget) throw new TimeoutException("Verifier artifact operation expired.");
        return result;
    }
    private void CheckCleanup(long origin, TimeSpan budget)
    {
        if (clock.GetElapsedTime(origin) >= budget) throw new TimeoutException("Kafka cleanup expired.");
    }
    private static bool KafkaFailure(Exception exception, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested && exception is
            VerifierKafkaOperationException or KafkaException or TimeoutException or OperationCanceledException or InputLimitExceededException;
    private static bool EvidenceFailure(Exception exception) => exception is FormatException or JsonException or
        InvalidOperationException or InvalidDataException or KeyNotFoundException or ArgumentException or InputLimitExceededException or OverflowException;
}

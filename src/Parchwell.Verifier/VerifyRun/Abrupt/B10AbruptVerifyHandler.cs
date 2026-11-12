using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Window;
using Parchwell.Verifier.Common.Kafka;
using static Parchwell.Verifier.VerifyRun.Abrupt.AbruptEvidenceCodec;

namespace Parchwell.Verifier.VerifyRun.Abrupt;

internal sealed class B10AbruptVerifyHandler(IOptions<VerifierHostOptions> configured, TimeProvider clock,
    VerifierKafkaSessionFactory sessions, IVerifierArtifactStore verifierStore, IProducerArtifactStore producerStore)
    : IVerifierModeHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private static readonly string[] ProblemNames =
    [
        "b10_abrupt_verify_start_line_invalid", "b10_abrupt_verify_first_evidence_invalid",
        "b10_abrupt_verify_successor_absent", "b10_abrupt_verify_successor_evidence_invalid",
        "b10_abrupt_verify_window_incomplete", "b10_abrupt_verify_probe_record_malformed"
    ];
    public VerifierMode Mode => VerifierMode.B10AbruptVerify;

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        VerifierHostOptions options = configured.Value;
        var limits = new InputLimits(options.MaxInputDepth!.Value, options.MaxArtifactBytes!.Value,
            options.MaxTotalInputBytes!.Value, int.MaxValue, long.MaxValue, long.MaxValue, options.MaxDiagnosticBytes!.Value);
        var inputBudget = new InputBudget(limits);
        var problems = new HashSet<int>();
        var details = new List<object>();
        string prefix = options.RunId + "/C1/r1/b10-abrupt-diagnostic/";
        var reader = new AbruptEvidenceReader(options, ReadLedger);
        StartLineEvidence? start = null;
        AbruptFirstEvidence? first = null;
        AbruptSuccessorEvidence? successor = null;
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

        string[] directories = [];
        Dictionary<string, string[]> namesByPod = new(StringComparer.Ordinal);
        try
        {
            BlobListing listing = await Timed(ct => producerStore.ListAsync(prefix, ct), options.BlobIoTimeout!.Value, cancellationToken).ConfigureAwait(false);
            Require(listing.IsComplete, "listing_incomplete");
            Require(listing.Names.All(name => name.StartsWith(prefix, StringComparison.Ordinal) && name[prefix.Length..].Contains('/')), "listing_path");
            namesByPod = listing.Names.GroupBy(name => name[prefix.Length..].Split('/')[0], StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            directories = namesByPod.Keys.Order(StringComparer.Ordinal).ToArray();
            var candidates = new List<(string Pod, string Uid)>();
            foreach (string pod in directories)
            {
                try
                {
                    string? uid = await reader.ReadRegistrationAsync(prefix, pod, null, cancellationToken).ConfigureAwait(false);
                    candidates.Add((pod, uid!));
                }
                catch (Exception exception) when (EvidenceFailure(exception)) { /* This directory may be the successor. */ }
            }
            Require(candidates.Count == 1, "first_registration_count");
            (string firstPod, string firstUid) = candidates[0];
            first = await reader.ReadFirstAsync(prefix, firstPod, firstUid, namesByPod[firstPod], cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (EvidenceFailure(exception)) { Problem(1, "first_evidence"); }

        if (first is not null)
        {
            string[] remaining = directories.Where(pod => pod != first.Pod).ToArray();
            if (remaining.Length == 0) Problem(2, "successor_absent");
            else
            {
                try
                {
                    Require(remaining.Length == 1, "successor_count");
                    successor = await reader.ReadSuccessorAsync(prefix, remaining[0], first, namesByPod[remaining[0]], cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (EvidenceFailure(exception)) { Problem(3, "successor_evidence"); }
            }
        }
        if (start is not null && first is not null)
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
                    var keys = first.Attempts.Values.Select(attempt => attempt.Key).ToHashSet(StringComparer.Ordinal);
                    WindowDrainResult drained = WindowConsumer.Drain(session.Consumer, window, clock, origin, options.SafetyStop!.Value,
                        options.PollSlice!.Value, new ConsumedRecordBudget(options.MaxConsumedRecords!.Value, options.MaxConsumedBytes!.Value),
                        keys, cancellationToken, kafkaWork.Token);
                    records = drained.Records;
                    complete = drained.Assessments.All(assessment => assessment.Disposition == AssessmentDisposition.Pass);
                    if (records.Any(record => record.Key is not null && keys.Contains(record.Key) &&
                        (record.Problems.Count != 0 || record.ValueSha256 is null))) Problem(5, "probe_identity");
                }
                if (!complete) Problem(4, "window_incomplete");
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                exception is VerifierKafkaOperationException or KafkaException or TimeoutException or OperationCanceledException or InputLimitExceededException)
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
        IReadOnlyList<AbruptArm>? arms = first is null ? null : AbruptClassification.Evaluate(first, successor, records, complete, options.MaxDiagnosticsPerCheck!.Value);
        byte[] result = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_version = 3, dialect = "b10_abrupt", purpose = "diagnostic_trial", synthetic = true,
            scientific_admissible = false, kind = "nonfinal_result",
            identity = new
            {
                run_id = options.RunId, scenario_id = "B10", configuration_id = "C1", phase = "C1", repeat = 1,
                source_commit = options.VerifierCommit, image_reference = options.VerifierImage,
                job_name = options.JobName, pod_name = options.PodName, pod_uid = options.PodUid,
                instance_id = options.PodName, predecessor_instance_id = (string?)null
            },
            data = new
            {
                first_instance_id = first?.Pod, successor_instance_id = successor?.Pod,
                start_line = start?.Start.Value?.PartitionHighs.OrderBy(high => high.Partition).Select(high => new AbruptCoordinate(high.Partition, high.High)).ToArray(),
                end_line = ends?.OrderBy(high => high.Key).Select(high => new AbruptCoordinate(high.Key, high.Value)).ToArray(),
                window_complete = complete, window_limit = "copies_after_end_line_not_observed", arms,
                problems = problems.Order().Select(index => ProblemNames[index]).ToArray(), problem_details = details, finality = "nonfinal"
            }
        }, JsonOptions);
        if (result.LongLength > options.MaxOutputBytes!.Value) throw new InvalidDataException("Diagnostic result exceeds MaxOutputBytes.");
        string resultName = prefix + "nonfinal-result.json";
        await Timed(async ct => { await verifierStore.CreateAsync(resultName, result, ct).ConfigureAwait(false); return true; },
            options.BlobIoTimeout!.Value, cancellationToken).ConfigureAwait(false);
        byte[]? readBack = await ReadVerifier(resultName, cancellationToken).ConfigureAwait(false);
        if (readBack is null || !result.AsSpan().SequenceEqual(readBack)) return 1;
        return 0;

        void Problem(int index, string detail)
        {
            if (!problems.Add(index) || details.Count >= options.MaxDiagnosticsPerCheck!.Value) return;
            details.Add(new { problem = ProblemNames[index], detail = detail[..Math.Min(detail.Length, options.MaxDiagnosticBytes!.Value)] });
        }
        async Task<byte[]?> ReadVerifier(string name, CancellationToken ct) =>
            await Timed(token => verifierStore.ReadAsync(name, token), options.BlobIoTimeout!.Value, ct).ConfigureAwait(false);
        async Task<byte[]?> ReadLedger(string name, long maximumBytes, CancellationToken ct)
        {
            byte[]? bytes = await Timed(token => producerStore.ReadBoundedAsync(name, maximumBytes, token), options.BlobIoTimeout!.Value, ct).ConfigureAwait(false);
            if (bytes is not null) inputBudget.ConsumeBytes(bytes.LongLength);
            return bytes;
        }
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
    private static bool EvidenceFailure(Exception exception) => exception is FormatException or JsonException or
        InvalidOperationException or InvalidDataException or KeyNotFoundException or ArgumentException or InputLimitExceededException or OverflowException;
}

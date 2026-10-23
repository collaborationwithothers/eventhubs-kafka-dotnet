using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Execution;

namespace Parchwell.Runner.Run.Evidence;

/// <summary>Projects observed phase evidence while preserving the durable worker acceptance decision.</summary>
public sealed class PhaseCandidate(EvidenceExporter exporter, TimeProvider clock)
{
    private static readonly string[] CountNames = ["ok", "loss", "phantom", "reported_failure", "duplicate", "reorder"];
    private static readonly string[] VersionNames = ["loadgen", "verifier", "workload_generator", "confluent_kafka", "librdkafka", "dotnet"];

    internal static string DurableStatus(PhaseRecord phase) => phase.Repeats.Length == 5 && phase.Repeats.All(repeat => repeat.State == "completed") &&
        phase.PrivateExport?.Complete == true && phase.DestroyReceipt?.Complete == true ? "completed" : "failed";

    public async Task<byte[]> CreateAsync(JsonElement runtime, LedgerDocument document, string phaseName, string status,
        IReadOnlyList<RepeatExecution>? executions = null, string? outputDirectory = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        PhaseRecord phase = document.Phases[phaseName];
        var reasons = new SortedSet<string>(StringComparer.Ordinal);
        using var timer = new CancellationTokenSource(TimeSpan.FromSeconds(300), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        var results = new List<JsonElement>();
        var acceptedCounts = new List<IReadOnlyDictionary<string, long>>();
        foreach (RepeatRecord repeat in phase.Repeats)
        {
            try
            {
                if (repeat.State != "completed" || repeat.RunId is null || repeat.AdmittedIdentity is null || repeat.Ordinal is < 1 or > 5 ||
                    repeat.RunId != phase.EnvironmentRunId + "-r" + repeat.Ordinal.ToString(CultureInfo.InvariantCulture) ||
                    repeat.Outcome is not ("complete_valid" or "invalid" or "incomplete") || repeat.Reason != repeat.Outcome)
                    throw new InvalidDataException("candidate_repeat_unavailable");
                RepeatExecution? execution = executions?.SingleOrDefault(item => item.Identity.RunId == repeat.RunId);
                byte[] bytes = execution is not null ? execution.Result : await PhaseResultReplay.ReadAsync(exporter, document,
                    phaseName, repeat, outputDirectory, deadline.Token).ConfigureAwait(false);
                if (bytes.Length > PrivateProvenanceCollector.MaxReceiptBytes || Hash(bytes) != repeat.ResultHash)
                    throw new InvalidDataException("candidate_result_hash");
                using JsonDocument parsed = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
                JsonElement result = parsed.RootElement;
                string admitted = repeat.AdmittedIdentity;
                var identity = new WorkerIdentity("A01", phaseName == "probe" ? "C0" : phaseName, repeat.RunId, admitted, admitted, admitted);
                if (result.GetProperty("schema_version").GetInt32() != 1 || WorkerIdentity.Read(result.GetProperty("identity")) != identity ||
                    WorkerIdentity.Text(result, "verifier_commit") != document.SourceCommit || WorkerIdentity.Text(result, "status") != repeat.Outcome ||
                    execution is not null && (execution.Identity != identity || !execution.Outcome.ExecutionCompleted ||
                        execution.Outcome.Status != repeat.Outcome || execution.Outcome.Reason != repeat.Reason))
                    throw new InvalidDataException("candidate_result_identity");
                results.Add(result.Clone());
                if (repeat.Outcome == "complete_valid")
                {
                    var counts = new Dictionary<string, long>(StringComparer.Ordinal);
                    foreach (string name in CountNames)
                    {
                        long count = result.GetProperty(name).GetInt64();
                        if (count < 0 || execution is not null && (!execution.Outcome.CountsAccepted || execution.Outcome.Counts is null ||
                            !execution.Outcome.Counts.TryGetValue(name, out long observed) || observed != count))
                            throw new InvalidDataException("candidate_counts_unavailable");
                        counts.Add(name, count);
                    }
                    acceptedCounts.Add(counts);
                }
            }
            catch (Exception exception) when (Unavailable(exception)) { FailureDiagnostics.Write(exception, Console.Error); reasons.Add("repeat_evidence_unavailable"); }
        }

        var totals = CountNames.ToDictionary(name => name, _ => (long?)null, StringComparer.Ordinal);
        if (PhaseResultReplay.HasExpectedOrdinals(phase) && acceptedCounts.Count == 5)
        {
            try
            {
                foreach (string name in CountNames) totals[name] = acceptedCounts.Aggregate(0L, (total, counts) => checked(total + counts[name]));
            }
            catch (OverflowException exception)
            {
                FailureDiagnostics.Write(exception, Console.Error);
                foreach (string name in CountNames) totals[name] = null;
                reasons.Add("counts_overflow");
            }
        }
        if (totals.Values.Any(value => value is null)) reasons.Add("counts_unavailable");

        var versions = new Dictionary<string, string?>(StringComparer.Ordinal)
        { ["runner"] = typeof(PhaseCandidate).Assembly.GetName().Version?.ToString() };
        foreach (string name in VersionNames)
        {
            // The workload generator version is a contract version; it is not a worker binary version.
            versions[name] = name is "loadgen" or "verifier" ? null : CommonVersion(results, name);
            if (versions[name] is null) reasons.Add(name + "_version_unavailable");
        }
        var digests = new Dictionary<string, string?>(StringComparer.Ordinal) { ["loadgen"] = null, ["verifier"] = null };
        try
        {
            byte[] bytes = await ReadOriginalAsync(phase, "private-provenance/image-receipts.json", deadline.Token).ConfigureAwait(false);
            if (bytes.Length > PrivateProvenanceCollector.MaxReceiptBytes) throw new InvalidDataException("candidate_image_receipt_limit");
            using JsonDocument receipt = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 12 });
            foreach (string role in digests.Keys.ToArray())
            {
                JsonElement image = receipt.RootElement.GetProperty(role);
                string digest = WorkerIdentity.Text(image, "digest");
                if (WorkerIdentity.Text(image, "source_commit") != document.SourceCommit || digest.Length != 71 ||
                    !digest.StartsWith("sha256:", StringComparison.Ordinal) || digest[7..].Any(character => !char.IsAsciiHexDigitLower(character)))
                    throw new InvalidDataException("candidate_image_receipt_identity");
                digests[role] = digest;
            }
        }
        catch (Exception exception) when (Unavailable(exception))
        {
            FailureDiagnostics.Write(exception, Console.Error);
            foreach (string role in digests.Keys.ToArray()) digests[role] = null;
            reasons.Add("image_digests_unavailable");
        }

        DateTimeOffset? started = phase.ApplyStartedUtc;
        DateTimeOffset? ended = phase.DestroyReceipt?.CompletedAt;
        decimal? duration = started is { } start && ended is { } end && end >= start ? (end - start).Ticks / (decimal)TimeSpan.TicksPerSecond : null;
        if (started is null || ended is null || duration is null) reasons.Add("timings_unavailable");
        string? gate = document.Phases["probe"].GateReceipt is { } receiptGate && receiptGate.SourceCommit == document.SourceCommit &&
            receiptGate.Outcome is "pass" or "untestable_at_build_scale" or "inconclusive" ? receiptGate.Outcome : null;
        if (gate is null) reasons.Add("gate_unavailable");
        reasons.Add(phase.ActualCostGbp is null ? "actual_cost_unavailable" : "actual_cost_received");
        Dictionary<string, object?> diagnostics = ProjectDiagnostics(results, reasons);
        return PublicCandidate.Create(JsonSerializer.SerializeToUtf8Bytes(new
        {
            scenario_id = "A01", configuration_id = phaseName == "probe" ? "C0" : phaseName,
            run_id = phase.EnvironmentRunId, source_commit = document.SourceCommit, status, gate,
            versions, image_digests = digests,
            timings = new { started_utc = started?.ToString("O", CultureInfo.InvariantCulture), ended_utc = ended?.ToString("O", CultureInfo.InvariantCulture), duration_seconds = duration },
            counts = totals, diagnostics,
            costs = new { estimate_gbp = phase.ReservedTotalGbp, actual_gbp = phase.ActualCostGbp, actual_available = phase.ActualCostGbp is not null },
            reasons
        }), WorkerIdentity.Text(runtime, "subscription_guid"));
    }

    private Task<byte[]> ReadOriginalAsync(PhaseRecord phase, string path, CancellationToken token)
    {
        if (phase.PrivateExport is not { Complete: true } receipt || phase.EnvironmentRunId is null)
            throw new InvalidDataException("candidate_private_export_unavailable");
        return exporter.ReadVerifiedFileAsync(phase.EnvironmentRunId, path, receipt, token);
    }

    private static string? CommonVersion(IReadOnlyList<JsonElement> results, string name)
    {
        string? common = null;
        foreach (JsonElement result in results)
        {
            if (!result.TryGetProperty("runtime_versions", out JsonElement versions) || versions.ValueKind != JsonValueKind.Object ||
                !versions.TryGetProperty(name, out JsonElement field) || field.ValueKind != JsonValueKind.String || field.GetString() is not { Length: > 0 and <= 256 } value ||
                value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-' or '+' or '_')))
                return null;
            if (name is "confluent_kafka" or "librdkafka" &&
                (!result.TryGetProperty("verifier_" + name + "_version", out JsonElement verifier) || verifier.ValueKind != JsonValueKind.String || verifier.GetString() != value))
                return null;
            if (common is not null && common != value) return null;
            common = value;
        }
        return common;
    }

    private static Dictionary<string, object?> ProjectDiagnostics(IReadOnlyList<JsonElement> results, SortedSet<string> reasons)
    {
        var projected = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["error_codes"] = null, ["max_throttle_ms"] = null, ["request_timeout_increment"] = null,
            ["disconnect_increment"] = null, ["rtt_p99_ms"] = null
        };
        var codes = new SortedSet<int>();
        bool codesAvailable = false, codesInvalid = false, windowsAvailable = false;
        foreach (JsonElement result in results)
        {
            if (result.TryGetProperty("a01_recorded_evidence", out JsonElement recorded) && recorded.ValueKind == JsonValueKind.Object)
            {
                if (recorded.TryGetProperty("delivery_error_codes", out JsonElement errors) && errors.ValueKind == JsonValueKind.Array)
                {
                    codesAvailable = true;
                    foreach (JsonElement error in errors.EnumerateArray())
                    {
                        if (error.ValueKind != JsonValueKind.Number || !error.TryGetInt32(out int code)) { codesInvalid = true; break; }
                        codes.Add(code);
                        if (codes.Count > 100) { codesInvalid = true; break; }
                    }
                }
                else reasons.Add("diagnostic_codes_unavailable");
                if (recorded.TryGetProperty("windows", out JsonElement windows) && windows.ValueKind == JsonValueKind.Array)
                {
                    windowsAvailable = true;
                    foreach (JsonElement window in windows.EnumerateArray())
                    {
                        Maximum(window, "throttle_maximum_ms", "max_throttle_ms", 1);
                        Maximum(window, "request_timeouts_increment", "request_timeout_increment", 1);
                        Maximum(window, "disconnects_increment", "disconnect_increment", 1);
                        Maximum(window, "round_trip_p99_us", "rtt_p99_ms", 1000);
                    }
                }
                else reasons.Add("diagnostic_windows_unavailable");
            }
            else reasons.Add("diagnostics_unavailable");
            if (result.TryGetProperty("probe_observation", out JsonElement probe) && probe.ValueKind == JsonValueKind.Object &&
                probe.TryGetProperty("usable", out JsonElement usable) && usable.ValueKind == JsonValueKind.True)
                Maximum(probe, "window_maximum", "max_throttle_ms", 1);
        }
        if (codesAvailable && !codesInvalid) projected["error_codes"] = codes.ToArray();
        else reasons.Add("diagnostic_codes_unavailable");
        if (windowsAvailable) reasons.Add("diagnostics_window_maxima");
        if (!windowsAvailable || projected.Where(pair => pair.Key != "error_codes").All(pair => pair.Value is null))
            reasons.Add("diagnostic_windows_unavailable");
        return projected;

        void Maximum(JsonElement window, string source, string target, decimal divisor)
        {
            if (window.ValueKind != JsonValueKind.Object || !window.TryGetProperty(source, out JsonElement field) || field.ValueKind == JsonValueKind.Null)
            { reasons.Add("diagnostic_value_unavailable"); return; }
            if (field.ValueKind != JsonValueKind.Number || !field.TryGetInt64(out long value) || value < 0)
            { reasons.Add("diagnostic_value_unavailable"); return; }
            decimal observed = value / divisor;
            if (projected[target] is not decimal existing || observed > existing) projected[target] = observed;
        }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static bool Unavailable(Exception exception) => exception is IOException or UnauthorizedAccessException or InvalidDataException or
        JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OperationCanceledException;
}

using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Contracts;
using static Parchwell.LoadGen.Run.Abrupt.B10AbruptDiagnostics;

namespace Parchwell.LoadGen.Run.Abrupt;

internal sealed class B10AbruptStatistics : IKafkaStatisticsEvidenceSink
{
    private readonly object gate = new();
    private readonly KafkaStatisticsIdentity identity;
    private readonly HashSet<string> expectedBrokers;
    private readonly Dictionary<string, KafkaBrokerStatistics> brokers = new(StringComparer.Ordinal);
    private readonly List<NumberedReport> reports = [];
    private readonly IKafkaStatisticsEvidenceSink? downstream;
    private long sequence;
    private long retainedBytes;
    private volatile bool overflow;
    private volatile bool guardFailed;
    private int guardReason;
    private bool trackNativeCapture;
    private readonly Queue<(int Length, string Sha256)> pendingNative = new();
    private string? nativeName;
    private long? nativeTimestamp;
    private Int128? nativeCreation;
    private HashSet<string>? qualifyingBrokers;
    private readonly HashSet<string> observedBrokers = new(StringComparer.Ordinal);
    private DateTimeOffset constructed;
    private DateTimeOffset drainUtc;
    private long? drainNativeBoundary;

    internal B10AbruptStatistics(KafkaStatisticsIdentity identity, IEnumerable<string> expectedBrokers,
        IReadOnlyDictionary<string, string> expectedSettings, IReadOnlyDictionary<string, string> actualSettings,
        IKafkaStatisticsEvidenceSink? downstream = null, long initialSequence = 0)
    {
        this.identity = identity;
        this.expectedBrokers = new HashSet<string>(expectedBrokers, StringComparer.Ordinal);
        this.downstream = downstream;
        sequence = initialSequence;
        if (this.expectedBrokers.Count is 0 or > 64 || identity.Role != KafkaClientRole.LoadGenProducer ||
            expectedSettings.Count != actualSettings.Count || expectedSettings.Any(pair =>
                !actualSettings.TryGetValue(pair.Key, out string? value) || value != pair.Value)) Guard(15);
    }

    internal static async Task ValidateCapturedAsync(string path, KafkaStatisticsIdentity expectedIdentity,
        IEnumerable<string> expectedBrokers, IReadOnlyDictionary<string, string> expectedSettings,
        IReadOnlyDictionary<string, string> actualSettings, CancellationToken cancellationToken)
    {
        byte[] bytes;
        try { bytes = await B10AbruptPublisher.ReadFileAsync(path, 134217728, cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { throw Attach(new InvalidDataException("b10_abrupt_capture_gap", exception)); }
        var available = new B10AbruptStatistics(expectedIdentity, expectedBrokers, expectedSettings, actualSettings);
        int start = 0;
        while (start < bytes.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int relativeEnd = bytes.AsSpan(start).IndexOf((byte)'\n');
            // The killed writer's incomplete last record remains in the explicitly unavailable native tail.
            if (relativeEnd < 0) break;
            if (relativeEnd is 0 or > 1048576) throw Attach(new InvalidDataException("b10_abrupt_capture_gap"), [Fact(B10AbruptFact.LineLength, relativeEnd)]);
            try
            {
                JsonElement row = B10AbruptWire.Parse(bytes.AsMemory(start, relativeEnd), 1048576);
                B10AbruptWire.ExactMembers(row, "schema_version", "identity", "statistics_identity", "session_id",
                    "received_at", "receipt_time", "raw_json", "typed_snapshot", "diagnostics");
                if (row.GetProperty("schema_version").GetInt32() != 1) throw Attach(new InvalidDataException("b10_abrupt_capture_gap"));
                var run = row.GetProperty("identity").Deserialize<RunIdentity>(B10AbruptWire.JsonOptions)!;
                run.Validate();
                var facts = new List<B10AbruptFactValue>();
                if (!Check(facts, B10AbruptFact.ScenarioMatches, run.ScenarioId == expectedIdentity.ScenarioId) ||
                    !Check(facts, B10AbruptFact.RunMatches, run.RunId == expectedIdentity.RunId) ||
                    !Check(facts, B10AbruptFact.ClientIdMatches, run.ClientId == expectedIdentity.ClientId))
                    throw Attach(new InvalidDataException("b10_abrupt_guard_failed"), [.. facts]);
                B10AbruptWire.ExactMembers(row.GetProperty("statistics_identity"), "scenario_id", "run_id", "role", "client_id");
                var identity = row.GetProperty("statistics_identity").Deserialize<KafkaStatisticsIdentity>(B10AbruptWire.JsonOptions)!;
                var snapshot = row.GetProperty("typed_snapshot").Deserialize<KafkaStatisticsSnapshot>(B10AbruptWire.JsonOptions);
                var diagnostics = row.GetProperty("diagnostics").Deserialize<ImmutableArray<KafkaStatisticsDiagnostic>>(B10AbruptWire.JsonOptions);
                available.Observe(new KafkaStatisticsReport(identity, row.GetProperty("session_id").GetString()!,
                    row.GetProperty("received_at").GetDateTimeOffset(), row.GetProperty("raw_json").GetString()!, snapshot, diagnostics));
            }
            catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException)
            { throw Attach(new InvalidDataException("b10_abrupt_capture_gap", exception)); }
            start = checked(start + relativeEnd + 1);
        }
        available.AssertAvailableGuards();
    }

    internal void AssertAvailableGuards()
    {
        lock (gate)
        {
            if (guardFailed || !expectedBrokers.IsSubsetOf(observedBrokers) && reports.Count > 0)
                throw Attach(new InvalidDataException("b10_abrupt_guard_failed"),
                    [Fact(B10AbruptFact.GuardFailed, guardFailed), Fact(B10AbruptFact.GuardReason, guardReason),
                        Fact(B10AbruptFact.ExpectedBrokersObserved, expectedBrokers.IsSubsetOf(observedBrokers)), Fact(B10AbruptFact.ReportCount, reports.Count)]);
        }
    }

    internal void ActivateNativeCaptureTracking()
    {
        lock (gate)
        {
            if (reports.Count != 0 || trackNativeCapture) throw Attach(new InvalidOperationException("b10_abrupt_configuration_invalid"));
            trackNativeCapture = true;
        }
    }

    internal void CaptureNative(string? raw)
    {
        if (overflow) return;
        if (raw is null || raw.Length > 1048576)
        {
            Guard(1);
            return;
        }
        int length = Encoding.UTF8.GetByteCount(raw);
        if (length is <= 0 or > 1048576)
        {
            Guard(2);
            return;
        }
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        if (!Monitor.TryEnter(gate))
        {
            Guard(3);
            return;
        }
        try
        {
            if (!trackNativeCapture || pendingNative.Count >= 2048) Guard(4);
            else pendingNative.Enqueue((length, hash));
        }
        finally { Monitor.Exit(gate); }
    }

    internal void MarkConstructed(DateTimeOffset value)
    {
        lock (gate) constructed = value;
    }

    private void Guard(int reason)
    {
        Interlocked.CompareExchange(ref guardReason, reason, 0);
        guardFailed = true;
    }

    internal int ReportCount
    {
        get { lock (gate) return reports.Count; }
    }

    public async ValueTask WriteAsync(KafkaStatisticsReport report, CancellationToken cancellationToken)
    {
        Observe(report);
        if (downstream is not null) await downstream.WriteAsync(report, cancellationToken).ConfigureAwait(false);
    }

    internal void Observe(KafkaStatisticsReport report)
    {
        lock (gate)
        {
            if (overflow) return;
            try { sequence = checked(sequence + 1); }
            catch (OverflowException) { overflow = true; return; }
            int length = Encoding.UTF8.GetByteCount(report.RawJson);
            if (length is <= 0 or > 1048576 || retainedBytes + length > 134217728)
            {
                Guard(5);
                return;
            }
            if (trackNativeCapture)
            {
                string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(report.RawJson)));
                if (!pendingNative.TryDequeue(out var native) || native.Length != length || native.Sha256 != hash)
                    Guard(6);
            }
            retainedBytes += length;
            reports.Add(new NumberedReport(sequence, report));
            if (report.Identity != identity || report.ReceivedAt.Offset != TimeSpan.Zero ||
                report.Snapshot is not { } snapshot || !report.Diagnostics.IsDefaultOrEmpty)
            {
                Guard(7);
                return;
            }
            if (snapshot.ClientId != identity.ClientId || snapshot.Type != "producer" ||
                (nativeName is not null && nativeName != snapshot.Name) || string.IsNullOrEmpty(snapshot.Name)) Guard(8);
            nativeName ??= snapshot.Name;
            Int128 creation = (Int128)snapshot.TimestampMicroseconds - snapshot.AgeMicroseconds;
            if (snapshot.TimestampMicroseconds < 0 || snapshot.AgeMicroseconds < 0 ||
                snapshot.TimestampMicroseconds <= nativeTimestamp || nativeCreation is { } priorCreation && priorCreation != creation)
                Guard(9);
            nativeTimestamp = snapshot.TimestampMicroseconds;
            nativeCreation ??= creation;
            var actual = snapshot.Brokers;
            try
            {
                using var raw = JsonDocument.Parse(report.RawJson, new JsonDocumentOptions { MaxDepth = 64 });
                JsonElement rawBrokers = raw.RootElement.GetProperty("brokers");
                var qualifying = new HashSet<string>(StringComparer.Ordinal);
                foreach (var broker in actual)
                {
                    JsonElement rawBroker = rawBrokers.GetProperty(broker.Name);
                    string? source = rawBroker.GetProperty("source").GetString();
                    if (string.IsNullOrWhiteSpace(source)) Guard(10);
                    if (source is "learned" or "configured" or "logical") qualifying.Add(broker.Name);
                    observedBrokers.Add(broker.Name);
                }
                if (qualifyingBrokers is not null && !qualifyingBrokers.SetEquals(qualifying)) Guard(11);
                qualifyingBrokers ??= qualifying;
            }
            catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                Guard(12);
            }
            foreach (var broker in actual)
            {
                if (broker.TransmissionErrors != 0 || broker.ReceiveErrors != 0 || broker.RequestTimeouts < 0 ||
                    broker.Disconnects < 0 || broker.TransmissionRetries < 0) Guard(13);
                if (brokers.TryGetValue(broker.Name, out KafkaBrokerStatistics? prior) &&
                    (broker.NodeId != prior.NodeId || broker.RequestTimeouts < prior.RequestTimeouts ||
                     broker.Disconnects < prior.Disconnects || broker.TransmissionRetries < prior.TransmissionRetries ||
                     broker.TransmissionErrors < prior.TransmissionErrors || broker.ReceiveErrors < prior.ReceiveErrors)) Guard(14);
                brokers[broker.Name] = broker;
            }
        }
    }

    internal NumberedReport? SelectPreAcceptance(DateTimeOffset acceptance)
    {
        lock (gate)
        {
            if (guardFailed || !expectedBrokers.IsSubsetOf(observedBrokers) && reports.Count > 0)
                throw Attach(new InvalidDataException("b10_abrupt_guard_failed"),
                    [Fact(B10AbruptFact.GuardFailed, guardFailed), Fact(B10AbruptFact.GuardReason, guardReason),
                        Fact(B10AbruptFact.ExpectedBrokersObserved, expectedBrokers.IsSubsetOf(observedBrokers)), Fact(B10AbruptFact.ReportCount, reports.Count)]);
            if (overflow) return null;
            return reports.LastOrDefault(report => report.Report.Snapshot is not null &&
                report.Report.ReceivedAt > constructed && report.Report.ReceivedAt < acceptance);
        }
    }

    internal void MarkDrainBoundary(DateTimeOffset drained, long timestamp, long frequency)
    {
        if (timestamp < 0 || frequency <= 0) throw Attach(new InvalidDataException("b10_abrupt_guard_failed"));
        lock (gate)
        {
            drainUtc = drained;
            drainNativeBoundary = checked((long)(((Int128)timestamp * 1_000_000 + frequency - 1) / frequency));
        }
    }

    internal NumberedReport? SelectPostDrain(DateTimeOffset drained)
    {
        lock (gate)
        {
            if (guardFailed || !expectedBrokers.IsSubsetOf(observedBrokers) && reports.Count > 0)
                throw Attach(new InvalidDataException("b10_abrupt_guard_failed"),
                    [Fact(B10AbruptFact.GuardFailed, guardFailed), Fact(B10AbruptFact.GuardReason, guardReason),
                        Fact(B10AbruptFact.ExpectedBrokersObserved, expectedBrokers.IsSubsetOf(observedBrokers)), Fact(B10AbruptFact.ReportCount, reports.Count)]);
            if (overflow || drained != drainUtc || drainNativeBoundary is null) return null;
            return reports.FirstOrDefault(report => report.Report.Snapshot is { } snapshot &&
                report.Report.ReceivedAt >= drained && snapshot.TimestampMicroseconds > drainNativeBoundary &&
                expectedBrokers.IsSubsetOf(snapshot.Brokers.Select(broker => broker.Name)) &&
                (qualifyingBrokers is null || qualifyingBrokers.IsSubsetOf(snapshot.Brokers.Select(broker => broker.Name))));
        }
    }

    internal B10AbruptFactValue[] PostDrainFacts(DateTimeOffset drained)
    {
        lock (gate)
        {
            return
            [
                Fact(B10AbruptFact.ReportCount, reports.Count),
                Fact(B10AbruptFact.SnapshotCount, reports.Count(report => report.Report.Snapshot is not null)),
                Fact(B10AbruptFact.AfterDrainUtcCount, reports.Count(report => report.Report.ReceivedAt >= drained)),
                Fact(B10AbruptFact.AfterNativeBoundaryCount, reports.Count(report => report.Report.Snapshot is { } snapshot &&
                    snapshot.TimestampMicroseconds > drainNativeBoundary)),
                Fact(B10AbruptFact.ExpectedBrokersCount, reports.Count(report => report.Report.Snapshot is { } snapshot &&
                    expectedBrokers.IsSubsetOf(snapshot.Brokers.Select(broker => broker.Name)))),
                Fact(B10AbruptFact.QualifyingBrokersCount, reports.Count(report => report.Report.Snapshot is { } snapshot &&
                    (qualifyingBrokers is null || qualifyingBrokers.IsSubsetOf(snapshot.Brokers.Select(broker => broker.Name))))),
                Fact(B10AbruptFact.Overflow, overflow),
                Fact(B10AbruptFact.DrainBoundaryMatches, drained == drainUtc && drainNativeBoundary is not null)
            ];
        }
    }

    internal JsonElement SealCohort(string cohort, DateTimeOffset drained)
    {
        var report = SelectPostDrain(drained) ?? throw Attach(new InvalidDataException("b10_abrupt_control_incomplete"));
        return JsonSerializer.SerializeToElement(new
        {
            cohort, statistics_complete = true, post_drain_report = report.Binding(),
            guard_coverage = "complete_for_available_reports", fatal_capture_status = "not_implemented_pr3"
        });
    }

    internal byte[] CapturedBytes()
    {
        lock (gate)
        {
            using var stream = new MemoryStream();
            foreach (var report in reports)
            {
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    report_sequence = report.Sequence, report_identity = report.Identity(),
                    report_capture_utc = report.Report.ReceivedAt.ToString("O", CultureInfo.InvariantCulture),
                    raw_json = report.Report.RawJson
                });
                if (stream.Length + bytes.Length + 1 > 134217728) throw Attach(new InvalidDataException("b10_abrupt_capture_gap"));
                stream.Write(bytes);
                stream.WriteByte((byte)'\n');
            }
            return stream.ToArray();
        }
    }

    internal sealed record NumberedReport(long Sequence, KafkaStatisticsReport Report)
    {
        internal object Identity() => new { scenario_id = Report.Identity.ScenarioId, run_id = Report.Identity.RunId,
            role = Report.Identity.Role.ToString(), client_id = Report.Identity.ClientId };
        internal JsonElement Binding() => JsonSerializer.SerializeToElement(new
        {
            length = Encoding.UTF8.GetByteCount(Report.RawJson),
            sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Report.RawJson))),
            report_identity = Identity(), report_sequence = Sequence,
            report_capture_utc = Report.ReceivedAt.ToString("O", CultureInfo.InvariantCulture)
        });
    }
}

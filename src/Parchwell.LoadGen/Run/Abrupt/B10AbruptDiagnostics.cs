using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Parchwell.LoadGen.Run.Abrupt;

internal enum B10AbruptFact
{
    Arm, Cohort, IsOriginal, AttemptPresent, PreparedKnown, PreparedSequenceMatches, Duplicate, ReportPresent, ArmFailed,
    ArmFailureCount, TerminalBeforeAccepted, ErrorCode, PersistenceStatus, Delivered, Permitted, AfterKill, GapReason,
    SuccessorDetail, JournalGapCount, AcceptedCount, SelectedCount, CandidateCount, TerminalExclusionCount,
    FramesDrainedAfterStop, FramesDrainedAfterKill, AcceptedToStopMilliseconds, ElapsedMilliseconds, BudgetMilliseconds,
    RemainingMilliseconds, StopObserved, ConfirmResult, ObservationPresent, WaitObserved, HasExited, ChildPresent,
    ProgressExists, ArmMatches, IdentityMatches, SequenceMatches, HashMatches, LengthMatches, CohortMatches, DrainComplete,
    GuardComplete, FatalStatusMatches, RowCount, SealCount, EntryCount, LineCount, InventoryCount, BookkeepingComplete,
    IdCount, NotAcceptedCount, NoTerminalCount, UnknownAttempt, RawMatches, ReportLength, FileLength, ReportSequence,
    ScenarioMatches, RunMatches, RoleMatches, ClientIdPresent, UtcValid, ExitCode, CleanupFailed, FrameCount,
    TimeoutSealed, ControlSealed, KillAdmitted, ProbeCount, PlannedCount, MaximumRetries, MaximumAttempts, LatenessMicroseconds, MaximumLatenessMicroseconds, PayloadLength, MaximumPayloadBytes, PreviousCount, PredecessorMatches, PreviousAccepted, ArmKnown, NameIndex, CapturedPresent, FileExists, ExpectedCount, OutstandingCount, ReportCount, SnapshotCount, AfterDrainUtcCount, AfterNativeBoundaryCount, ExpectedBrokersCount, QualifyingBrokersCount, DrainBoundaryMatches, Overflow, Faulted, FaultReason, EvidenceFaulted, GuardObservationsComplete, NativeErrorObserved, GuardFailed, GuardReason, ExpectedBrokersObserved, KeyMatches, SequenceZero, PayloadWithinLimit, InstanceDistinct, ArrayLength, AdmittedCount, RetryAdmittedCount, DeliveredCount, TimedOutCount, SynchronousRejectionCount, OtherErrorCount, ProduceToAcceptedMilliseconds, ProduceToTerminalMilliseconds, CompletionComplete, SchemaMatches, SourceMatches, SourceCommitMatches, ConfigurationMatches, BrokerCount, BlankCount, DistinctCount, KindMatches, GapPresent, ClientIdMatches, LineLength, ProduceTicks, CallbackTicks,
}

internal enum B10AbruptStage
{
    SupervisorRegistered, ChildStarted, FramePrepared, FrameReport, FrameAccepted, AckSent, FrameTerminal, SealLoaded,
    StopRequested, StopConfirmed, CheckpointWritten, ResumeRequested, ResumeConfirmed, KillIntent, KillRequested,
    ExitObserved, EofComplete, SelectionFinalized, PublicationComplete, EventStreamEnded, GraceEnded,
    FailureObserved, AbortComplete,
    ProducerConstructed, TimeoutScheduleComplete, TimeoutDrained, PostDrainReport, CohortSealed, ProbePrepared, ProbeProduced, ProbeReportSelected, ProbeAccepted, ProbeTerminal, ReleaseRead, ReleaseReached, CohortDrained, SuccessorReady,
}

internal readonly record struct B10AbruptFactValue(B10AbruptFact Key, long Value);

internal static class B10AbruptDiagnostics
{
    internal const int MaximumChainDepth = 8, MaximumSiteFacts = 16, MaximumStageFacts = 8, MaximumTimelineEntries = 48;
    internal sealed record Site(string File, string Member, int Line, ImmutableArray<B10AbruptFactValue> Facts);
    internal sealed record ChainEntry(string ExceptionType, string? ThrowerType, string? ThrowerMethod);
    internal sealed record Chain(IReadOnlyList<ChainEntry> Entries, bool Truncated);
    private static readonly ConditionalWeakTable<Exception, Site> Sites = new();

    internal static B10AbruptFactValue Fact(B10AbruptFact key, long value) => new(key, value);
    internal static B10AbruptFactValue Fact(B10AbruptFact key, bool value) => new(key, value ? 1 : 0);
    internal static B10AbruptFactValue Fact(B10AbruptFact key, TimeSpan value) => new(key, (long)value.TotalMilliseconds);
    internal static bool Check(List<B10AbruptFactValue> facts, B10AbruptFact key, bool value) { facts.Add(Fact(key, value)); return value; }

    internal static T Attach<T>(T exception, B10AbruptFactValue[]? facts = null, [CallerFilePath] string file = "",
        [CallerMemberName] string member = "", [CallerLineNumber] int line = 0) where T : Exception
    {
        var kept = ImmutableArray.CreateBuilder<B10AbruptFactValue>();
        foreach (B10AbruptFactValue fact in facts ?? [])
            if (kept.Count < MaximumSiteFacts && !kept.Any(item => item.Key == fact.Key)) kept.Add(fact);
        Sites.AddOrUpdate(exception, new Site(Path.GetFileName(file), member, line, kept.ToImmutable()));
        return exception;
    }

    internal static int ArmCode(string? arm) => arm switch { "timeout" => 0, "same_instance" => 1, "cross_restart" => 2, "cleanup" => 3, _ => -1 };
    internal static int GapReasonCode(string? reason) => Array.IndexOf(GapReasons, reason);
    internal static int SuccessorDetailCode(string? detail) => Array.IndexOf(SuccessorDetails, detail);
    private static readonly string[] GapReasons = ["frame_oversize", "queue_full", "journal_bytes_exceeded", "journal_entries_exceeded", "writer_failed", "malformed_frame", "sequence_gap"];
    private static readonly string[] SuccessorDetails = ["receipt_missing", "receipt_changed", "job_mismatch", "pod_uid_reused", "source_mismatch", "image_mismatch", "predecessor_mismatch", "selection_stale"];

    internal static long NumberOrMinusOne(JsonElement data, string name) =>
        data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number) ? number : -1;

    internal static Chain CaptureChain(Exception exception)
    {
        var entries = new List<ChainEntry>(MaximumChainDepth);
        Exception? current = exception;
        while (current is not null && entries.Count < MaximumChainDepth)
        {
            Type type = current.GetType();
            System.Reflection.MethodBase? site = current.TargetSite;
            entries.Add(new(type.FullName ?? type.Name, site?.DeclaringType?.FullName, site?.Name));
            current = current.InnerException;
        }
        return new(entries, current is not null);
    }

    internal static (Site? Outer, Site? Inner) FindSites(Exception exception)
    {
        Site? outer = null;
        Exception? current = exception;
        for (int depth = 0; current is not null && depth < MaximumChainDepth; depth++, current = current.InnerException)
        {
            if (!Sites.TryGetValue(current, out Site? site)) continue;
            if (outer is null) outer = site; else return (outer, site);
        }
        return (outer, null);
    }

    internal static Dictionary<string, long> FactsObject(IEnumerable<B10AbruptFactValue> facts) =>
        facts.ToDictionary(fact => JsonNamingPolicy.SnakeCaseLower.ConvertName(fact.Key.ToString()), fact => fact.Value, StringComparer.Ordinal);

    internal static object? SiteObject(Site? site) => site is null ? null :
        new { file = site.File, member = site.Member, line = site.Line, facts = FactsObject(site.Facts) };

    internal static object Describe(Exception exception, bool reduce = false)
    {
        try
        {
            (Site? outer, Site? inner) = FindSites(exception);
            return new { exception_chain = reduce ? null : CaptureChain(exception), detail = SiteObject(outer), inner_detail = SiteObject(inner), reduced = reduce };
        }
        catch (Exception) { return new { exception_chain = (Chain?)null, detail = (object?)null, inner_detail = (object?)null, reduced = true }; }
    }

    internal static object Bounded(Exception exception)
    {
        object full = Describe(exception);
        return System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(full, B10AbruptWire.JsonOptions)) > B10AbruptWire.MaximumBodyBytes
            ? Describe(exception, reduce: true) : full;
    }

    internal static string PlainSuffix(Exception exception) => " diagnostic=" + JsonSerializer.Serialize(Bounded(exception), B10AbruptWire.JsonOptions);
}

internal sealed class B10AbruptTimeline(TimeProvider clock, string role, TextWriter? output)
{
    private sealed record Entry(string Stage, long ElapsedMilliseconds, long Ticks, Dictionary<string, long> Facts);
    private readonly object gate = new();
    private readonly long origin = clock.GetTimestamp();
    private readonly string startUtc = clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
    private readonly List<Entry> entries = [];
    private int printed;
    private long dropped;

    // Mark records the entry and prints it with any held entries; Hold records without I/O.
    internal void Mark(B10AbruptStage stage, params B10AbruptFactValue[] facts) => Record(stage, facts, print: true);
    internal void Hold(B10AbruptStage stage, params B10AbruptFactValue[] facts) => Record(stage, facts, print: false);
    internal void Flush() { lock (gate) PrintHeld(); }

    private void Record(B10AbruptStage stage, B10AbruptFactValue[] facts, bool print)
    {
        try
        {
            long ticks = clock.GetTimestamp();
            var entry = new Entry(JsonNamingPolicy.SnakeCaseLower.ConvertName(stage.ToString()),
                (long)clock.GetElapsedTime(origin, ticks).TotalMilliseconds, ticks,
                B10AbruptDiagnostics.FactsObject(facts.DistinctBy(fact => fact.Key).Take(B10AbruptDiagnostics.MaximumStageFacts)));
            lock (gate)
            {
                if (entries.Count >= B10AbruptDiagnostics.MaximumTimelineEntries) dropped++;
                else entries.Add(entry);
                if (print) PrintHeld();
            }
        }
        catch (Exception) { lock (gate) dropped++; }
    }

    private void PrintHeld()
    {
        for (; printed < entries.Count; printed++)
        {
            try
            {
                Entry entry = entries[printed];
                output?.WriteLine("b10_abrupt_stage " + JsonSerializer.Serialize(new { role, stage = entry.Stage,
                    elapsed_ms = entry.ElapsedMilliseconds, ticks = entry.Ticks, facts = entry.Facts }, B10AbruptWire.JsonOptions));
            }
            catch (Exception) { }
        }
    }

    internal object Snapshot(bool omitEntries = false)
    {
        lock (gate)
            return new
            {
                role, start_utc = startUtc, origin_ticks = origin, timestamp_frequency = clock.TimestampFrequency,
                entries = omitEntries ? [] : entries.Select(entry => (object)new { stage = entry.Stage,
                    elapsed_ms = entry.ElapsedMilliseconds, ticks = entry.Ticks, facts = entry.Facts }).ToArray(),
                dropped, omitted_for_size = omitEntries
            };
    }
}

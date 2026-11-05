using System.Globalization;
using System.Text.Json;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;

namespace Parchwell.Verifier.Common.Phase;

/// <summary>Closed bounded B10 contracts and their shared identity and timing predicate.</summary>
internal static class B10SweepEvidenceCodec
{
    internal const int MaximumBytes = 65_536;
    private static readonly InputLimits Defaults = new(16, MaximumBytes, MaximumBytes, MaximumBytes, 10000, 10000, 256);
    private static readonly string[] CountNames = ["ok", "loss", "phantom", "reported_failure", "duplicate", "reorder"];

    internal static byte[] WriteSweepLine(B10SweepLine value, OutputLimits? limits = null, int maxPartitions = int.MaxValue)
    {
        ValidateLine(value, maxPartitions);
        return DeploymentJson.Write(Math.Min(MaximumBytes, limits?.MaxOutputBytes ?? MaximumBytes), w =>
        {
            w.WriteStartObject(); WriteCoordinates(w, value.SchemaVersion, value.ScenarioId, value.ConfigurationId, value.RunId, value.Phase, value.SourceCommit);
            w.WriteString("topic", value.Topic); w.WritePropertyName("partition_highs"); w.WriteStartArray();
            foreach (PartitionHigh p in value.PartitionHighs)
            { w.WriteStartObject(); w.WriteNumber("partition", p.Partition); w.WriteNumber("high", p.High); w.WriteEndObject(); }
            w.WriteEndArray(); w.WriteString("captured_at", Utc(value.CapturedAt));
            w.WriteString("last_verifier_completed_utc", Utc(value.LastVerifierCompletedUtc));
            w.WriteString("last_loadgen_terminated_utc", Utc(value.LastLoadgenTerminatedUtc));
            w.WriteNumber("settle_interval_ms", value.SettleIntervalMilliseconds); w.WriteEndObject();
        });
    }

    internal static B10SweepLine ReadSweepLine(ReadOnlyMemory<byte> bytes, InputLimits? limits = null, int maxPartitions = int.MaxValue)
    {
        using JsonDocument d = Parse(bytes, limits); JsonElement r = d.RootElement;
        Exact(r, ["schema_version", "scenario_id", "configuration_id", "run_id", "phase", "source_commit", "topic", "partition_highs", "captured_at", "last_verifier_completed_utc", "last_loadgen_terminated_utc", "settle_interval_ms"]);
        JsonElement a = Array(r, "partition_highs");
        if (a.GetArrayLength() > maxPartitions) throw new InputLimitExceededException("B10 sweep exceeds the partition limit.");
        var highs = new List<PartitionHigh>();
        foreach (JsonElement p in a.EnumerateArray()) { Exact(p, ["partition", "high"]); highs.Add(new(Int(p, "partition"), Long(p, "high"))); }
        var value = new B10SweepLine(Int(r, "schema_version"), Str(r, "scenario_id"), Str(r, "configuration_id"), Str(r, "run_id"),
            Str(r, "phase"), Str(r, "source_commit"), Str(r, "topic"), highs, Time(r, "captured_at"),
            Time(r, "last_verifier_completed_utc"), Time(r, "last_loadgen_terminated_utc"), Long(r, "settle_interval_ms"));
        ValidateLine(value, maxPartitions); return value;
    }

    internal static bool MatchesSweepLine(B10SweepLine note, VerifierHostOptions options, DateTimeOffset workerStartedUtc,
        IEnumerable<DateTimeOffset> verifyCompletions)
    {
        try { ValidateLine(note, options.MaxPartitions ?? 100_000); }
        catch (Exception ex) when (ex is FormatException or InputLimitExceededException or ArgumentException) { return false; }
        return options.ScenarioId == "B10" && note.ConfigurationId == options.ConfigurationId && note.RunId == options.RunId &&
            note.Phase == options.Phase && note.SourceCommit == options.SourceCommit &&
            options.B10SettleIntervalMilliseconds is > 0 and <= int.MaxValue &&
            note.SettleIntervalMilliseconds == options.B10SettleIntervalMilliseconds && note.CapturedAt <= workerStartedUtc &&
            verifyCompletions.All(time => time <= note.LastVerifierCompletedUtc);
    }

    internal static byte[] WriteFinalization(B10Finalization value, OutputLimits? limits = null)
    {
        ValidateFinalization(value);
        return DeploymentJson.Write(Math.Min(MaximumBytes, limits?.MaxOutputBytes ?? MaximumBytes), w =>
        {
            w.WriteStartObject(); WriteCoordinates(w, value.SchemaVersion, value.ScenarioId, value.ConfigurationId, value.RunId, value.Phase, value.SourceCommit);
            WriteReference(w, "sweep_line", value.SweepLine); w.WriteBoolean("scan_complete", value.ScanComplete);
            w.WriteString("residual_risk", value.ResidualRisk); w.WritePropertyName("repeats"); w.WriteStartArray();
            foreach (B10RepeatFinalization repeat in value.Repeats)
            {
                w.WriteStartObject(); w.WriteNumber("repeat", repeat.Repeat); WriteReference(w, "result", repeat.Result);
                WriteReference(w, "verify_receipt", repeat.VerifyReceipt); w.WriteBoolean("classification_final", repeat.ClassificationFinal);
                w.WriteString("status", Status(repeat.Status)); w.WritePropertyName("counts");
                if (repeat.Counts is not { } c) w.WriteNullValue();
                else { w.WriteStartObject(); long[] counts = [c.Ok, c.Loss, c.Phantom, c.ReportedFailure, c.Duplicate, c.Reorder];
                    for (int i = 0; i < counts.Length; i++) w.WriteNumber(CountNames[i], counts[i]); w.WriteEndObject(); }
                w.WritePropertyName("reasons"); w.WriteStartArray(); foreach (string reason in repeat.Reasons) w.WriteStringValue(reason);
                w.WriteEndArray(); w.WriteEndObject();
            }
            w.WriteEndArray(); w.WriteEndObject();
        });
    }

    internal static B10Finalization ReadFinalization(ReadOnlyMemory<byte> bytes, InputLimits? limits = null)
    {
        using JsonDocument d = Parse(bytes, limits); JsonElement r = d.RootElement;
        Exact(r, ["schema_version", "scenario_id", "configuration_id", "run_id", "phase", "source_commit", "sweep_line", "scan_complete", "residual_risk", "repeats"]);
        JsonElement a = Array(r, "repeats"); if (a.GetArrayLength() != 5) throw new FormatException("B10 finalization requires five repeats.");
        var repeats = new List<B10RepeatFinalization>();
        foreach (JsonElement p in a.EnumerateArray())
        {
            Exact(p, ["repeat", "result", "verify_receipt", "classification_final", "status", "counts", "reasons"]);
            OutcomeCounts? counts = null;
            if (p.GetProperty("counts") is { ValueKind: not JsonValueKind.Null } c)
            { Exact(c, CountNames); counts = new(Long(c, "ok"), Long(c, "loss"), Long(c, "phantom"), Long(c, "reported_failure"), Long(c, "duplicate"), Long(c, "reorder")); }
            string[] reasons = Array(p, "reasons").EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString()! : throw new FormatException("Reason must be text.")).ToArray();
            repeats.Add(new(Int(p, "repeat"), Reference(p, "result"), Reference(p, "verify_receipt"), Bool(p, "classification_final"),
                Str(p, "status") switch { "complete_valid" => VerifierResultStatus.CompleteValid, "invalid" => VerifierResultStatus.Invalid,
                    "incomplete" => VerifierResultStatus.Incomplete, _ => throw new FormatException("Unknown B10 status.") }, counts, reasons));
        }
        var value = new B10Finalization(Int(r, "schema_version"), Str(r, "scenario_id"), Str(r, "configuration_id"), Str(r, "run_id"),
            Str(r, "phase"), Str(r, "source_commit"), Reference(r, "sweep_line"), Bool(r, "scan_complete"), Str(r, "residual_risk"), repeats);
        ValidateFinalization(value); return value;
    }

    private static void ValidateLine(B10SweepLine value, int maxPartitions)
    {
        Coordinates(value.SchemaVersion, value.ScenarioId, value.ConfigurationId, value.RunId, value.Phase, value.SourceCommit);
        if (value.PartitionHighs.Count > maxPartitions) throw new InputLimitExceededException("B10 sweep exceeds the partition limit.");
        if (value.Topic != "document-events" || value.PartitionHighs.Count == 0 || value.PartitionHighs.Any(p => p.Partition < 0 || p.High < 0) ||
            value.PartitionHighs.Select(p => p.Partition).Distinct().Count() != value.PartitionHighs.Count ||
            value.SettleIntervalMilliseconds is <= 0 or > int.MaxValue || value.CapturedAt.Offset != TimeSpan.Zero ||
            value.LastVerifierCompletedUtc.Offset != TimeSpan.Zero || value.LastLoadgenTerminatedUtc.Offset != TimeSpan.Zero ||
            value.LastVerifierCompletedUtc > value.CapturedAt || value.LastLoadgenTerminatedUtc > value.CapturedAt ||
            (value.CapturedAt - value.LastLoadgenTerminatedUtc).Ticks / TimeSpan.TicksPerMillisecond < value.SettleIntervalMilliseconds)
            throw new FormatException("B10 sweep line has invalid bounds or timing.");
    }

    private static void ValidateFinalization(B10Finalization value)
    {
        Coordinates(value.SchemaVersion, value.ScenarioId, value.ConfigurationId, value.RunId, value.Phase, value.SourceCommit);
        if (value.ResidualRisk != B10SweepEvidence.ResidualRisk || value.Repeats.Count != 5 || value.ScanComplete && value.SweepLine is null)
            throw new FormatException("B10 finalization is missing required phase evidence.");
        ValidateReference(value.SweepLine, PhasePaths.SweepLine(value.RunId, value.Phase));
        for (int i = 0; i < value.Repeats.Count; i++)
        {
            B10RepeatFinalization r = value.Repeats[i];
            if (r.Repeat != i + 1 || !Enum.IsDefined(r.Status) || r.ClassificationFinal != (r.Status == VerifierResultStatus.CompleteValid) ||
                r.ClassificationFinal != (r.Counts is not null) || r.ClassificationFinal && (r.Result is null || r.VerifyReceipt is null) ||
                !value.ScanComplete && (r.Status != VerifierResultStatus.Invalid || r.ClassificationFinal || r.Counts is not null) ||
                r.Counts is { } c && new[] { c.Ok, c.Loss, c.Phantom, c.ReportedFailure, c.Duplicate, c.Reorder }.Any(n => n < 0) ||
                r.Reasons.Count > 100 || r.Reasons.Any(s => s.Length > 256 || s.Any(c => !char.IsAscii(c))) ||
                !r.Reasons.SequenceEqual(r.Reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)))
                throw new FormatException("B10 finalization repeat is inconsistent.");
            ValidateReference(r.Result, PhasePaths.Result(value.RunId, value.Phase, r.Repeat));
            ValidateReference(r.VerifyReceipt, PhasePaths.VerifyCompletion(value.RunId, value.Phase, r.Repeat));
        }
    }

    private static void Coordinates(int schema, string scenario, string configuration, string run, string phase, string source)
    {
        if (schema != 1 || scenario != "B10" || !PhaseIdentity.IsRunId(run) ||
            !ScenarioPhases.TryGetConfiguration(scenario, phase, out string? expected) || configuration != expected ||
            source.Length != 40 || source.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new FormatException("B10 phase coordinates are invalid.");
    }

    private static void ValidateReference(ArtifactReference? value, string path)
    {
        if (value is not null && (value.Container != "verifier" || value.Name != path || value.Length < 0 ||
            value.Sha256.Length != 64 || value.Sha256.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))))
            throw new FormatException("B10 artifact reference does not bind its fixed path.");
    }

    private static JsonDocument Parse(ReadOnlyMemory<byte> bytes, InputLimits? limits)
    {
        InputLimits bounded = (limits ?? Defaults) with { MaxArtifactBytes = Math.Min(MaximumBytes, (limits ?? Defaults).MaxArtifactBytes), MaxDepth = Math.Min(16, (limits ?? Defaults).MaxDepth) };
        ParseResult<JsonDocument> parsed = StrictJson.ParseDocument(bytes, "b10-sweep", bounded, null);
        return parsed.IsSuccess ? parsed.Value! : throw new FormatException("Malformed B10 sweep evidence.");
    }

    private static void Exact(JsonElement e, IEnumerable<string> names)
    {
        var fields = new HashSet<string>(names, StringComparer.Ordinal);
        if (e.ValueKind != JsonValueKind.Object || e.EnumerateObject().Any(p => !fields.Contains(p.Name)) || fields.Any(n => !e.TryGetProperty(n, out _)))
            throw new FormatException("B10 object has missing or unknown fields.");
    }
    private static string Utc(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset Time(JsonElement e, string n) => DateTimeOffset.TryParseExact(Str(e, n), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset value) && value.Offset == TimeSpan.Zero ? value : throw new FormatException("B10 time must be round-trip UTC.");
    private static string Str(JsonElement e, string n) => e.GetProperty(n).ValueKind == JsonValueKind.String ? e.GetProperty(n).GetString()! : throw new FormatException("Expected text.");
    private static long Long(JsonElement e, string n) => e.GetProperty(n).ValueKind == JsonValueKind.Number && e.GetProperty(n).TryGetInt64(out long value) ? value : throw new FormatException("Expected Int64.");
    private static int Int(JsonElement e, string n) => e.GetProperty(n).ValueKind == JsonValueKind.Number && e.GetProperty(n).TryGetInt32(out int value) ? value : throw new FormatException("Expected Int32.");
    private static bool Bool(JsonElement e, string n) => e.GetProperty(n).ValueKind is JsonValueKind.True or JsonValueKind.False ? e.GetProperty(n).GetBoolean() : throw new FormatException("Expected boolean.");
    private static JsonElement Array(JsonElement e, string n) => e.GetProperty(n).ValueKind == JsonValueKind.Array ? e.GetProperty(n) : throw new FormatException("Expected array.");
    private static string Status(VerifierResultStatus value) => value switch { VerifierResultStatus.CompleteValid => "complete_valid", VerifierResultStatus.Invalid => "invalid", VerifierResultStatus.Incomplete => "incomplete", _ => throw new FormatException("Unknown status.") };
    private static void WriteCoordinates(Utf8JsonWriter w, int schema, string scenario, string config, string run, string phase, string source)
    { w.WriteNumber("schema_version", schema); w.WriteString("scenario_id", scenario); w.WriteString("configuration_id", config); w.WriteString("run_id", run); w.WriteString("phase", phase); w.WriteString("source_commit", source); }
    private static void WriteReference(Utf8JsonWriter w, string name, ArtifactReference? value)
    { w.WritePropertyName(name); if (value is null) { w.WriteNullValue(); return; } w.WriteStartObject(); w.WriteString("container", value.Container); w.WriteString("name", value.Name); w.WriteNumber("length", value.Length); w.WriteString("sha256", value.Sha256); w.WriteEndObject(); }
    private static ArtifactReference? Reference(JsonElement e, string n)
    { JsonElement p = e.GetProperty(n); if (p.ValueKind == JsonValueKind.Null) return null; Exact(p, ["container", "name", "length", "sha256"]); return new(Str(p, "container"), Str(p, "name"), Long(p, "length"), Str(p, "sha256")); }
}

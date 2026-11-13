using System.Text.Json;
using System.Text.Json.Serialization;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.Common.Phase;

/// <summary>Reads only the closed schema-three final sweep dialect; validates before allocation into records.</summary>
internal static class B10FinalProjectionCodec
{
    internal const long MaximumBytes = 16_777_216;
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };
    private static readonly string[] RepeatReasons = ["b10_final_sweep_line_invalid", "b10_final_repeat_input_invalid",
        "b10_final_ownership_invalid", "b10_final_vector_invalid", "b10_final_drain_incomplete", "b10_final_owner_invalid",
        "b10_final_late_record_unjoined", "b10_final_window_mismatch", "b10_final_timeout_evidence_invalid",
        "b10_final_timeout_absent", "b10_final_repeat_not_started", "b10_final_start_line_invalid"];

    private static readonly string[] EligibilityReasons = ["b10_restart_configuration_invalid", "b10_restart_first_evidence_unavailable",
        "b10_restart_identity_unauthenticated", "b10_restart_termination_unverified", "b10_restart_native_error_captured",
        "b10_restart_fatal_captured", "b10_restart_timeout_bookkeeping_unavailable", "b10_abrupt_control_incomplete",
        "b10_restart_successor_evidence_unavailable", "b10_restart_resend_count_invalid", "b10_abrupt_successor_late",
        "b10_restart_window_incomplete", "b10_restart_guard_failed", "b10_restart_sweep_unavailable", "b10_restart_late_record_unjoined"];
    private static readonly string[] Outcomes = ["b10_abrupt_exposure_inconclusive", "b10_abrupt_control_incomplete", "kill_too_early",
        "b10_abrupt_cut_rejected", "b10_abrupt_sample_missing", "b10_abrupt_warmup_failed", "b10_abrupt_leader_not_up",
        "b10_abrupt_report_binding_invalid", "b10_abrupt_guard_failed", "b10_abrupt_signal_preflight_failed",
        "b10_abrupt_deadline_exhausted", "b10_abrupt_capture_gap", "kill_too_late"];

    internal static byte[] Write(B10FinalProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        byte[] bytes = DeploymentJson.Write(MaximumBytes, writer =>
        {
            writer.WriteStartObject(); writer.WriteNumber("schema_version", 3); writer.WriteString("dialect", "b10_abrupt");
            writer.WriteString("purpose", "scientific"); writer.WriteBoolean("synthetic", true);
            writer.WriteBoolean("scientific_admissible", projection.Data.ScanComplete);
            writer.WriteString("kind", "final_sweep_projection");
            writer.WritePropertyName("identity"); JsonSerializer.Serialize(writer, projection.Identity, JsonOptions);
            writer.WritePropertyName("data"); JsonSerializer.Serialize(writer, projection.Data, JsonOptions);
            writer.WriteEndObject();
        });
        _ = Read(bytes);
        return bytes;
    }

    internal static B10FinalProjection Read(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > MaximumBytes) throw new InputLimitExceededException("Final projection exceeds its byte limit.");
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        JsonElement root = document.RootElement;
        Exact(root, "schema_version", "dialect", "purpose", "synthetic", "scientific_admissible", "kind", "identity", "data");
        Require(root.GetProperty("schema_version").TryGetInt32(out int schema) && schema == 3 &&
            Text(root, "dialect") == "b10_abrupt" && Text(root, "purpose") == "scientific" &&
            root.GetProperty("synthetic").ValueKind == JsonValueKind.True && Text(root, "kind") == "final_sweep_projection");
        JsonElement identity = root.GetProperty("identity");
        Exact(identity, "run_id", "scenario_id", "configuration_id", "phase", "source_commit", "image_reference", "job_name", "pod_name", "pod_uid");
        foreach (JsonProperty property in identity.EnumerateObject()) _ = ScalarText(property.Value, false);
        string run = Text(identity, "run_id"), phase = Text(identity, "phase");
        Require(VerifierHostOptions.IsB10ScientificRunId(run) && Text(identity, "scenario_id") == "B10" &&
            phase is "C0" or "C1" && Text(identity, "configuration_id") == phase &&
            Text(identity, "job_name") == PhaseIdentity.B10FinalSweepJobName(run, phase));
        string commit = Text(identity, "source_commit");
        Require(commit.Length == 40 && commit.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'));
        JsonElement data = root.GetProperty("data");
        Exact(data, "sweep_line", "scan_complete", "residual_risk", "repeats");
        bool scan = Boolean(data.GetProperty("scan_complete"));
        Require(Boolean(root.GetProperty("scientific_admissible")) == scan && Text(data, "residual_risk") == B10SweepEvidence.ResidualRisk);
        Reference(data.GetProperty("sweep_line"), "verifier", PhasePaths.SweepLine(run, phase));
        Require(!scan || data.GetProperty("sweep_line").ValueKind != JsonValueKind.Null);
        ValidateRepeats(data.GetProperty("repeats"), phase, scan, run);
        return new(identity.Deserialize<B10FinalIdentity>(JsonOptions)!, data.Deserialize<B10FinalData>(JsonOptions)!);
    }

    internal static void ValidateRepeats(JsonElement repeats, string phase, bool scan, string run)
    {
        Require(repeats.ValueKind == JsonValueKind.Array && repeats.GetArrayLength() == 5);
        int ordinal = 0;
        foreach (JsonElement repeat in repeats.EnumerateArray())
        {
            Exact(repeat, "repeat", "source", "producer_versions", "classification_final", "status", "timeout", "reasons", "restart");
            Require(repeat.GetProperty("repeat").TryGetInt32(out int number) && number == ++ordinal);
            Reference(repeat.GetProperty("source"), "verifier", phase == "C0" ? PhasePaths.Result(run, phase, number) : $"{run}/{phase}/r{number}/b10-abrupt/nonfinal-result.json");
            bool final = Boolean(repeat.GetProperty("classification_final"));
            string status = Text(repeat, "status");
            Require(status is "complete_valid" or "invalid" or "incomplete" && final == (status == "complete_valid") && (!final || scan));
            JsonElement timeout = repeat.GetProperty("timeout");
            Require((timeout.ValueKind != JsonValueKind.Null) == final);
            if (final) { Exact(timeout, "ok", "loss", "phantom", "reported_failure", "duplicate", "reorder"); foreach (JsonProperty p in timeout.EnumerateObject()) Count(p.Value); }
            string[] reasons = Strings(repeat.GetProperty("reasons"));
            Require((!final || reasons.Length == 0) && reasons.Length <= 100 && reasons.SequenceEqual(reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) && reasons.All(x => RepeatReasons.Contains(x, StringComparer.Ordinal)));
            JsonElement versions = repeat.GetProperty("producer_versions");
            Require(versions.ValueKind == JsonValueKind.Array);
            string? prior = null;
            foreach (JsonElement version in versions.EnumerateArray())
            {
                Exact(version, "instance_id", "workload_generator", "confluent_kafka", "librdkafka", "dotnet");
                string instance = Text(version, "instance_id");
                Require(prior is null || StringComparer.Ordinal.Compare(prior, instance) < 0); prior = instance;
                foreach (string field in new[] { "workload_generator", "confluent_kafka", "librdkafka", "dotnet" }) _ = ScalarText(version.GetProperty(field), true, true);
            }
            JsonElement restart = repeat.GetProperty("restart");
            Require(phase == "C0" ? restart.ValueKind == JsonValueKind.Null : restart.ValueKind == JsonValueKind.Object);
            if (phase == "C1") ValidateRestart(restart, scan);
        }
    }

    private static void ValidateRestart(JsonElement restart, bool scan)
    {
        Exact(restart, "eligibility", "eligibility_reasons", "termination", "first_instance_id", "successor_instance_id", "timeout_instance_id", "comparison", "native_errors", "arms");
        string eligibility = Text(restart, "eligibility"); string[] reasons = Strings(restart.GetProperty("eligibility_reasons"));
        Require(eligibility is "eligible" or "ineligible" && reasons.Length <= 100 && reasons.Distinct(StringComparer.Ordinal).Count() == reasons.Length);
        int previousRank = -1;
        foreach (string reason in reasons)
        {
            int rank = Array.IndexOf(EligibilityReasons, reason);
            if (rank < 0) rank = Outcomes.Contains(reason, StringComparer.Ordinal) ? 15 : reason == "b10_restart_input_unavailable" ? 16 : -1;
            Require(rank >= previousRank && rank >= 0); previousRank = rank;
        }
        Require(scan != reasons.Contains("b10_restart_sweep_unavailable", StringComparer.Ordinal));
        string comparison = Text(restart, "comparison");
        Require(comparison is "both" or "cross_restart_only" or "same_instance_only" or "neither" or "unavailable");
        Require(eligibility != "eligible" || scan && reasons.Length == 0 && comparison != "unavailable");
        foreach (string field in new[] { "first_instance_id", "successor_instance_id", "timeout_instance_id" }) _ = ScalarText(restart.GetProperty(field), true);
        JsonElement termination = restart.GetProperty("termination"); Exact(termination, "class", "outcome");
        string? terminationClass = ScalarText(termination.GetProperty("class"), true);
        string? outcome = ScalarText(termination.GetProperty("outcome"), true);
        Require(terminationClass switch
        {
            null => outcome is null,
            "no_kill" => outcome is not null && Outcomes.Contains(outcome, StringComparer.Ordinal) && outcome != "kill_too_late",
            "intended_self_kill" => outcome is null or "kill_too_late",
            "unexpected_exit" => outcome is "b10_abrupt_signal_failed" or "b10_abrupt_unexpected_exit",
            "unknown" => outcome == "b10_abrupt_exit_unobserved",
            _ => false
        });
        Require(terminationClass is null or "intended_self_kill" or "no_kill" or "unexpected_exit" or "unknown");
        JsonElement native = restart.GetProperty("native_errors");
        if (native.ValueKind != JsonValueKind.Null) { Exact(native, "count", "fatal_count", "tail"); Count(native.GetProperty("count")); Count(native.GetProperty("fatal_count")); _ = Text(native, "tail"); Require(native.GetProperty("fatal_count").GetInt64() <= native.GetProperty("count").GetInt64()); }
        JsonElement arms = restart.GetProperty("arms");
        if (arms.ValueKind == JsonValueKind.Null) { Require(comparison == "unavailable"); return; }
        Require(terminationClass != "no_kill" && arms.ValueKind == JsonValueKind.Array && arms.GetArrayLength() == 2);
        int armIndex = 0;
        foreach (JsonElement arm in arms.EnumerateArray())
        {
            Exact(arm, "arm", "cut_attempt_id", "unconfirmed_attempt_ids", "unconfirmed_count", "resend_invoked", "originals_seen", "resends_seen", "pair_count", "unreported_attempts", "unreported_seen", "unreported_unseen", "lost_resends", "witness");
            Require(Text(arm, "arm") == (armIndex++ == 0 ? "same_instance" : "cross_restart"));
            _ = ScalarText(arm.GetProperty("cut_attempt_id"), true);
            string[] ids = Strings(arm.GetProperty("unconfirmed_attempt_ids")); Require(ids.Distinct(StringComparer.Ordinal).Count() == ids.Length);
            foreach (string field in new[] { "unconfirmed_count", "resend_invoked", "originals_seen", "resends_seen", "pair_count", "unreported_attempts", "unreported_seen", "unreported_unseen", "lost_resends" }) Count(arm.GetProperty(field), true);
            long? N(string field) => arm.GetProperty(field).ValueKind == JsonValueKind.Null ? null : arm.GetProperty(field).GetInt64();
            Require(N("unconfirmed_count") is null || N("unconfirmed_count") == ids.LongLength);
            Require(N("resend_invoked") is not long invoked || N("unconfirmed_count") is not long unconfirmed || invoked <= unconfirmed);
            Require(N("originals_seen") is not long originals || N("unconfirmed_count") is not long originalLimit || originals <= originalLimit);
            Require(N("resends_seen") is not long resends || N("resend_invoked") is not long resendLimit || resends <= resendLimit);
            Require(N("pair_count") is not >= 1 || N("originals_seen") is >= 1 && N("resends_seen") is >= 1);
            Require(N("unreported_seen") is not long seen || N("unreported_unseen") is not long unseen || N("unreported_attempts") is not long total || seen <= total && unseen == total - seen);
            Require(N("lost_resends") is not long lost || N("resend_invoked") is not long invokedLimit || lost <= invokedLimit);
            JsonElement witness = arm.GetProperty("witness");
            Require(N("pair_count") is null || (N("pair_count") >= 1) == (witness.ValueKind != JsonValueKind.Null));
            if (witness.ValueKind != JsonValueKind.Null)
            {
                Exact(witness, "original_attempt_id", "resend_attempt_id", "original", "resend"); _ = Text(witness, "original_attempt_id"); _ = Text(witness, "resend_attempt_id");
                foreach (string field in new[] { "original", "resend" }) { JsonElement coordinate = witness.GetProperty(field); Exact(coordinate, "partition", "offset"); Require(coordinate.GetProperty("partition").TryGetInt32(out int partition) && partition >= 0); Count(coordinate.GetProperty("offset")); }
                JsonElement original = witness.GetProperty("original"), resend = witness.GetProperty("resend");
                Require((original.GetProperty("partition").GetInt32(), original.GetProperty("offset").GetInt64()) !=
                    (resend.GetProperty("partition").GetInt32(), resend.GetProperty("offset").GetInt64()) &&
                    N("pair_count") is >= 1 && ids.Contains(Text(witness, "original_attempt_id"), StringComparer.Ordinal) &&
                    Text(witness, "original_attempt_id") != Text(witness, "resend_attempt_id"));
            }
            bool localUnjoined = reasons.Contains("b10_restart_late_record_unjoined", StringComparer.Ordinal);
            bool emptyCross = scan && terminationClass == "intended_self_kill" && outcome == "kill_too_late" &&
                Text(arm, "arm") == "cross_restart" && N("unconfirmed_count") == 0;
            if (!scan || localUnjoined)
                foreach (string field in new[] { "originals_seen", "resends_seen", "pair_count", "unreported_seen", "unreported_unseen", "lost_resends", "witness" })
                {
                    JsonElement value = arm.GetProperty(field);
                    Require(value.ValueKind == JsonValueKind.Null || localUnjoined && emptyCross && field != "witness" &&
                        value.ValueKind == JsonValueKind.Number && value.GetInt64() == 0);
                }
        }
        bool unavailable = reasons.Any(reason => reason is not ("b10_restart_native_error_captured" or "b10_restart_fatal_captured" or
            "b10_restart_timeout_bookkeeping_unavailable" or "b10_restart_guard_failed"));
        bool HasPair(int index) => arms[index].GetProperty("pair_count").ValueKind == JsonValueKind.Number && arms[index].GetProperty("pair_count").GetInt64() >= 1;
        string expectedComparison = unavailable ? "unavailable" : (HasPair(0), HasPair(1)) switch
        { (true, true) => "both", (true, false) => "same_instance_only", (false, true) => "cross_restart_only", _ => "neither" };
        Require(comparison == expectedComparison);
    }

    internal static void Exact(JsonElement value, params string[] fields)
    {
        Require(value.ValueKind == JsonValueKind.Object);
        string[] names = value.EnumerateObject().Select(x => x.Name).ToArray();
        Require(names.Length == fields.Length && names.Distinct(StringComparer.Ordinal).Count() == names.Length && fields.All(x => names.Contains(x, StringComparer.Ordinal)));
    }
    internal static void Require(bool condition) { if (!condition) throw new FormatException("Final B10 projection is malformed."); }
    internal static string Text(JsonElement value, string name) => ScalarText(value.GetProperty(name), false)!;
    private static string? ScalarText(JsonElement value, bool nullable, bool empty = false)
    {
        if (nullable && value.ValueKind == JsonValueKind.Null) return null;
        Require(value.ValueKind == JsonValueKind.String); string text = value.GetString()!;
        Require((empty || text.Length > 0) && text.Length <= 256 && text.All(char.IsAscii)); return text;
    }
    private static string[] Strings(JsonElement value) { Require(value.ValueKind == JsonValueKind.Array); return value.EnumerateArray().Select(x => ScalarText(x, false)!).ToArray(); }
    private static bool Boolean(JsonElement value) { Require(value.ValueKind is JsonValueKind.True or JsonValueKind.False); return value.GetBoolean(); }
    private static void Count(JsonElement value, bool nullable = false) { if (nullable && value.ValueKind == JsonValueKind.Null) return; Require(value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long n) && n >= 0); }
    private static void Reference(JsonElement value, string container, string path)
    {
        if (value.ValueKind == JsonValueKind.Null) return;
        Exact(value, "container", "name", "length", "sha256"); Count(value.GetProperty("length"));
        string sha = Text(value, "sha256"); Require(Text(value, "container") == container && Text(value, "name") == path && sha.Length == 64 && sha.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'));
    }
}

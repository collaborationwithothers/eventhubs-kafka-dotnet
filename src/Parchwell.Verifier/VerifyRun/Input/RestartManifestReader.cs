using System.Globalization;
using System.Text.Json;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.VerifyRun.Input;

internal static partial class ManifestReader
{
    private static readonly IReadOnlySet<string> RestartFields = Fields("predecessor_instance_id", "planned");
    private static readonly IReadOnlySet<string> RestartPlannedFields = Fields(
        "ordinal", "predecessor_attempt_id", "tenant_id", "document_id", "key", "sequence", "event_type", "planned_microseconds");
    private static readonly string[] RestartLifecycleEvents = ["uploaded", "ocr-started", "ocr-finished", "classified", "indexed"];

    private sealed partial class ManifestParser
    {
        private RestartManifest? ReadRestart(JsonElement root)
        {
            if (!root.TryGetProperty("restart", out JsonElement restart)) return null;
            if (restart.ValueKind != JsonValueKind.Object) { Add("wrong_json_kind", "restart"); return null; }
            int problemCount = Problems.Count;
            Validate(restart, "restart", RestartFields);
            string? predecessor = String(restart, "predecessor_instance_id", "restart.predecessor_instance_id");
            List<RestartPlannedEntry>? planned = ReadRestartPlanned(restart);
            return Problems.Count == problemCount && predecessor is not null && planned is not null
                ? new RestartManifest(predecessor, planned) : null;
        }

        private List<RestartPlannedEntry>? ReadRestartPlanned(JsonElement restart)
        {
            if (!restart.TryGetProperty("planned", out JsonElement array)) { Add("missing_property", "restart.planned"); return null; }
            if (array.ValueKind != JsonValueKind.Array) { Add("wrong_json_kind", "restart.planned"); return null; }
            if (array.GetArrayLength() > maxInventoryEntries)
                throw new InputLimitExceededException("manifest.json restart planned entries exceed the configured entry limit.");
            budget.ConsumeEntries(array.GetArrayLength());
            var result = new List<RestartPlannedEntry>();
            int index = 0;
            foreach (JsonElement value in array.EnumerateArray())
            {
                string path = $"restart.planned[{index++}]";
                if (value.ValueKind != JsonValueKind.Object) { Add("expected_object", path); continue; }
                Validate(value, path, RestartPlannedFields);
                long? ordinal = Int64(value, "ordinal", path + ".ordinal");
                string? predecessorAttempt = String(value, "predecessor_attempt_id", path + ".predecessor_attempt_id");
                string? tenant = String(value, "tenant_id", path + ".tenant_id");
                string? document = String(value, "document_id", path + ".document_id");
                string? key = String(value, "key", path + ".key");
                long? sequence = Int64(value, "sequence", path + ".sequence");
                string? eventType = String(value, "event_type", path + ".event_type");
                long? planned = Int64(value, "planned_microseconds", path + ".planned_microseconds");
                if (ordinal is not null && predecessorAttempt is not null && tenant is not null && document is not null &&
                    key is not null && sequence is not null && eventType is not null && planned is not null)
                {
                    result.Add(new RestartPlannedEntry(ordinal.Value, predecessorAttempt, tenant, document, key,
                        sequence.Value, eventType, planned.Value));
                }
            }
            return result;
        }

        // Only manifest-local restart checks belong here; predecessor-ledger reconciliation is a later boundary.
        private void ValidateRestartManifest(SealedRunManifest manifest)
        {
            if (manifest.Restart is not { } restart) return;
            if (!string.Equals(manifest.Identity.ScenarioId, "B10", StringComparison.Ordinal))
            {
                Add("restart_manifest_requires_b10", "identity.scenario_id");
                return;
            }
            if (!IsIdentityText(restart.PredecessorInstanceId) ||
                string.Equals(restart.PredecessorInstanceId, manifest.Identity.InstanceId, StringComparison.Ordinal))
            {
                Add("restart_predecessor_invalid", "restart.predecessor_instance_id");
                return;
            }
            if (manifest.PlannedAttemptCount != restart.Planned.Count)
            {
                Add("restart_planned_count_mismatch", "planned_attempt_count");
                return;
            }
            var predecessorAttempts = new HashSet<string>(StringComparer.Ordinal);
            long previousSlot = -1;
            for (int index = 0; index < restart.Planned.Count; index++)
            {
                RestartPlannedEntry planned = restart.Planned[index];
                if (planned.Ordinal != index || !IsSafeControlText(planned.PredecessorAttemptId) ||
                    !predecessorAttempts.Add(planned.PredecessorAttemptId) || planned.PlannedMicroseconds < 0 ||
                    !IsSafeControlText(planned.TenantId) || !IsSafeControlText(planned.DocumentId) ||
                    !IsSafeControlText(planned.Key) || !IsSafeControlText(planned.EventType) ||
                    !TryGetLogicalSlot(planned, out long slot) || slot <= previousSlot)
                {
                    Add("invalid_restart_planned_entry", $"restart.planned[{index}]");
                    return;
                }
                previousSlot = slot;
            }
            for (int index = 0; index < manifest.Inventory.Count; index++)
            {
                if (index >= restart.Planned.Count || !MatchesPlanned(restart.Planned[index], manifest.Inventory[index]))
                {
                    Add("restart_inventory_mismatch", $"inventory[{index}]");
                    return;
                }
            }
            bool hasUnexecuted = manifest.Inventory.Count < manifest.PlannedAttemptCount;
            bool suffixMatches = hasUnexecuted
                ? manifest.FirstUnexecutedOrdinal.Presence == FieldPresence.Value &&
                    manifest.FirstUnexecutedOrdinal.Value == manifest.Inventory.Count
                : manifest.FirstUnexecutedOrdinal.Presence == FieldPresence.Null;
            if (!suffixMatches || (manifest.WorkerStatus == "complete" && (hasUnexecuted || !manifest.BookkeepingComplete)))
                Add("manifest_unexecuted_suffix_mismatch", "first_unexecuted_ordinal");
        }

        private static bool IsSafeControlText(string value) =>
            !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);

        private static bool IsIdentityText(string value) =>
            !string.IsNullOrWhiteSpace(value) && !value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character));

        private static bool TryGetLogicalSlot(RestartPlannedEntry entry, out long slot)
        {
            slot = 0;
            if (entry.Sequence < 0 || entry.Sequence >= RestartLifecycleEvents.Length ||
                !string.Equals(entry.EventType, RestartLifecycleEvents[(int)entry.Sequence], StringComparison.Ordinal) ||
                entry.DocumentId.Length < 2 || entry.DocumentId[0] != 'd' ||
                !long.TryParse(entry.DocumentId.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out long ordinal) || ordinal < 0 ||
                !string.Equals(entry.DocumentId, string.Concat("d", ordinal.ToString(CultureInfo.InvariantCulture)), StringComparison.Ordinal) ||
                !string.Equals(entry.Key, string.Concat(entry.TenantId, "-", entry.DocumentId), StringComparison.Ordinal))
                return false;
            try
            {
                slot = checked(ordinal * 5 + entry.Sequence);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static bool MatchesPlanned(RestartPlannedEntry planned, AttemptEntry entry) =>
            string.Equals(planned.TenantId, entry.TenantId, StringComparison.Ordinal) &&
            string.Equals(planned.DocumentId, entry.DocumentId, StringComparison.Ordinal) &&
            string.Equals(planned.Key, entry.Key, StringComparison.Ordinal) && planned.Sequence == entry.Sequence &&
            string.Equals(planned.EventType, entry.EventType, StringComparison.Ordinal) &&
            planned.PlannedMicroseconds == entry.PlannedMicroseconds;
    }
}

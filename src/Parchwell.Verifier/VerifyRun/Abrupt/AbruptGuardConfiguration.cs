using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Input;
using CapturedSetting = Parchwell.Verifier.VerifyRun.Input.ResolvedSetting;
using SelectedSetting = Parchwell.Scenarios.ResolvedSetting;

namespace Parchwell.Verifier.VerifyRun.Abrupt;

internal static class AbruptGuardConfiguration
{
    internal static bool Matches(CapturedCapabilities capabilities, ResolvedRoleConfiguration expected)
    {
        if (capabilities.ResolvedConfiguration.Presence != FieldPresence.Value) return false;
        IReadOnlyDictionary<string, CapturedSetting> actual = capabilities.ResolvedConfiguration.Value!.Settings;
        if (actual.Count != expected.Settings.Count) return false;
        foreach ((string name, SelectedSetting selected) in expected.Settings)
        {
            if (!actual.TryGetValue(name, out CapturedSetting? observed)) return false;
            bool probeTimeout = name == "message.timeout.ms";
            if (observed.Value != (probeTimeout ? "300000" : selected.Value)) return false;
            if (probeTimeout)
            {
                if (observed.Override.Presence != FieldPresence.Null || !MetadataMatches(observed.Baseline, selected.Baseline)) return false;
            }
            else
            {
                bool hasOverride = observed.Override.Presence == FieldPresence.Value;
                if (hasOverride != (selected.Override is not null)) return false;
                if (hasOverride && (observed.Override.Value!.ScenarioId != selected.Override!.ScenarioId ||
                    observed.Override.Value.ConfigurationId != selected.Override.ConfigurationId)) return false;
            }
        }
        return true;
    }

    private static bool MetadataMatches(SettingMetadata actual, KafkaSettingMetadata expected) =>
        actual.Key == expected.Key && actual.Value.Presence == (expected.Value is null ? FieldPresence.Null : FieldPresence.Value) &&
        actual.Value.Value == expected.Value && actual.Role == (int)expected.Role && actual.Disposition == (int)expected.Disposition &&
        actual.InventoryIds.SequenceEqual(expected.InventoryIds, StringComparer.Ordinal) &&
        actual.SelectionReason == expected.SelectionReason && actual.Sources.Count == expected.Sources.Length &&
        actual.Sources.Zip(expected.Sources).All(pair => pair.First.Url == pair.Second.Url.ToString() &&
            pair.First.VersionOrDate == pair.Second.VersionOrDate);
}

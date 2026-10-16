using System.Collections.Immutable;
using System.Globalization;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.Run.Workload;

/// <summary>Streams the version 1 deterministic synthetic workload without retaining a run's events.</summary>
/// <remarks>
/// Selection uses SplitMix64. Its state starts at the unsigned 32-bit bit pattern of the profile seed.
/// Every draw adds 0x9e3779b97f4a7c15, then applies the SplitMix64 xor-shift/multiply steps. Weighted
/// choices use rejection sampling and declared bucket order. One tenant is selected per documents-per-burst
/// group, followed by one payload bucket per event. The payload byte stream is independent: its initial state
/// is the seed bit pattern XOR 0xd1b54a32d192ed03 XOR (event ordinal multiplied by 0x9e3779b97f4a7c15).
/// Payload bytes are emitted least-significant byte first from each SplitMix64 output. These rules and the
/// schema are synthetic harness behavior; they make no claim about a broker or cloud runtime.
/// </remarks>
public static class WorkloadGenerator
{
    public const int GeneratorVersion = 1;

    private const ulong Gamma = 0x9e3779b97f4a7c15UL;
    private const ulong PayloadDomain = 0xd1b54a32d192ed03UL;
    private const ulong MixMultiplier1 = 0xbf58476d1ce4e5b9UL;
    private const ulong MixMultiplier2 = 0x94d049bb133111ebUL;
    private static readonly ImmutableArray<string> EventTypes =
        ["uploaded", "ocr-started", "ocr-finished", "classified", "indexed"];

    /// <summary>Returns the exact number of event attempts in the fixed stage schedule.</summary>
    public static long GetPlannedAttemptCount(WorkloadProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateShape(profile);

        long count = 0;
        foreach (var stage in profile.Stages)
        {
            count = checked(count + checked((long)stage.OfferedAttemptsPerSecond * stage.DurationSeconds));
        }

        _ = GetTotalDurationMicroseconds(profile.Stages);

        return count;
    }

    /// <summary>
    /// Returns a bounded lazy stream of planned events. The complete count and largest declared payload are
    /// checked before the returned sequence can be enumerated.
    /// </summary>
    public static IEnumerable<PlannedEvent> Generate(WorkloadProfile profile, long maxAttempts,
        int maxPayloadBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "max_attempts_must_be_positive");
        }

        if (maxPayloadBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPayloadBytes), "max_payload_bytes_must_be_positive");
        }

        var plannedCount = GetPlannedAttemptCount(profile);
        if (plannedCount > maxAttempts)
        {
            throw new InvalidOperationException("planned_attempt_limit_exceeded");
        }

        var payloadChoices = new Dictionary<string, (WeightedChoice<int>[] Choices, long Total)>(StringComparer.Ordinal);
        foreach (var eventType in EventTypes)
        {
            var buckets = profile.Payloads[eventType];
            if (buckets.Any(bucket => bucket.SizeBytes > maxPayloadBytes))
            {
                throw new InvalidOperationException("payload_size_limit_exceeded");
            }

            var choices = buckets.Select(bucket => new WeightedChoice<int>(bucket.SizeBytes, bucket.Weight)).ToArray();
            payloadChoices.Add(eventType, (choices, SumWeights(choices)));
        }

        var totalDurationMicroseconds = GetTotalDurationMicroseconds(profile.Stages);
        return Enumerate(profile, plannedCount, totalDurationMicroseconds, payloadChoices, cancellationToken);
    }

    /// <summary>Creates exactly the planned number of deterministic synthetic value bytes.</summary>
    public static byte[] GeneratePayload(PlannedEvent plannedEvent, int seed)
    {
        ArgumentNullException.ThrowIfNull(plannedEvent);
        if (plannedEvent.Ordinal < 0 || plannedEvent.PayloadSizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(plannedEvent), "planned_event_values_must_be_nonnegative");
        }

        var seedBits = unchecked((ulong)(uint)seed);
        var eventBits = unchecked((ulong)plannedEvent.Ordinal);
        var random = new SplitMix64(seedBits ^ PayloadDomain ^ unchecked(eventBits * Gamma));
        var payload = new byte[plannedEvent.PayloadSizeBytes];
        ulong word = 0;
        var remaining = 0;
        for (var index = 0; index < payload.Length; index++)
        {
            if (remaining == 0)
            {
                word = random.Next();
                remaining = sizeof(ulong);
            }

            payload[index] = (byte)word;
            word >>= 8;
            remaining--;
        }

        return payload;
    }

    private static IEnumerable<PlannedEvent> Enumerate(WorkloadProfile profile, long plannedCount,
        long totalDurationMicroseconds,
        IReadOnlyDictionary<string, (WeightedChoice<int>[] Choices, long Total)> payloadChoices,
        CancellationToken cancellationToken)
    {
        var selectionRandom = new SplitMix64(unchecked((ulong)(uint)profile.Seed));
        var tenantWeights = profile.Tenants.Select(tenant => new WeightedChoice<string>(tenant.Id, tenant.Weight)).ToArray();
        var tenantTotal = SumWeights(tenantWeights);
        string? currentTenant = null;
        long ordinal = 0;
        long stageStartMicroseconds = 0;
        foreach (var stage in profile.Stages)
        {
            var stageCount = checked((long)stage.OfferedAttemptsPerSecond * stage.DurationSeconds);
            for (long slot = 0; slot < stageCount; slot++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var documentOrdinal = ordinal / EventTypes.Length;
                if (ordinal % EventTypes.Length == 0 && documentOrdinal % profile.DocumentsPerBurst == 0)
                {
                    currentTenant = Select(tenantWeights, tenantTotal, ref selectionRandom);
                }

                var sequence = (int)(ordinal % EventTypes.Length);
                var eventType = EventTypes[sequence];
                var distribution = payloadChoices[eventType];
                var payloadSize = Select(distribution.Choices, distribution.Total, ref selectionRandom);
                var documentId = string.Concat("d", documentOrdinal.ToString(CultureInfo.InvariantCulture));
                var key = string.Concat(currentTenant, "-", documentId);
                var wholeSeconds = slot / stage.OfferedAttemptsPerSecond;
                var remainingSlots = slot % stage.OfferedAttemptsPerSecond;
                var offsetMicroseconds = checked(checked(wholeSeconds * 1_000_000L) +
                    checked(remainingSlots * 1_000_000L) / stage.OfferedAttemptsPerSecond);
                var dueMicroseconds = checked(stageStartMicroseconds + offsetMicroseconds);
                yield return new PlannedEvent(ordinal, stage.Id, currentTenant!, documentId, key, sequence,
                    eventType, dueMicroseconds, payloadSize);
                ordinal = checked(ordinal + 1);
            }

            stageStartMicroseconds = checked(stageStartMicroseconds + checked((long)stage.DurationSeconds * 1_000_000L));
        }

        if (ordinal != plannedCount || stageStartMicroseconds != totalDurationMicroseconds)
        {
            throw new InvalidOperationException("planned_count_changed_during_enumeration");
        }
    }

    private static long GetTotalDurationMicroseconds(ImmutableArray<LoadStage> stages)
    {
        long total = 0;
        foreach (var stage in stages)
        {
            total = checked(total + checked((long)stage.DurationSeconds * 1_000_000L));
        }

        return total;
    }

    private static void ValidateShape(WorkloadProfile profile)
    {
        if (profile.SchemaVersion != 1 || profile.DocumentsPerBurst <= 0 || profile.Tenants.IsDefaultOrEmpty || profile.Stages.IsDefaultOrEmpty ||
            profile.Payloads is null)
        {
            throw new ArgumentException("invalid_workload_profile", nameof(profile));
        }

        _ = SumWeights(profile.Tenants.Select(tenant => new WeightedChoice<string>(tenant.Id, tenant.Weight)));
        foreach (var eventType in EventTypes)
        {
            if (!profile.Payloads.TryGetValue(eventType, out var buckets) || buckets.IsDefaultOrEmpty)
            {
                throw new ArgumentException("invalid_payload_distribution", nameof(profile));
            }

            _ = SumWeights(buckets.Select(bucket => new WeightedChoice<int>(bucket.SizeBytes, bucket.Weight)));
            if (buckets.Any(bucket => bucket.SizeBytes <= 0))
            {
                throw new ArgumentException("negative_payload_size", nameof(profile));
            }
        }

        if (profile.Payloads.Count != EventTypes.Length || profile.Tenants.Any(tenant => string.IsNullOrWhiteSpace(tenant.Id)))
        {
            throw new ArgumentException("invalid_workload_profile", nameof(profile));
        }

        var stageIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stage in profile.Stages)
        {
            if (string.IsNullOrWhiteSpace(stage.Id) || !stageIds.Add(stage.Id) ||
                stage.OfferedAttemptsPerSecond <= 0 || stage.DurationSeconds <= 0)
            {
                throw new ArgumentException("invalid_stage", nameof(profile));
            }
        }
    }

    private static long SumWeights<T>(IEnumerable<WeightedChoice<T>> choices)
    {
        long total = 0;
        foreach (var choice in choices)
        {
            if (choice.Weight <= 0)
            {
                throw new ArgumentException("weights_must_be_positive");
            }

            total = checked(total + choice.Weight);
        }

        return total;
    }

    private static T Select<T>(WeightedChoice<T>[] choices, long total, ref SplitMix64 random)
    {
        var draw = random.NextBounded(unchecked((ulong)total));
        ulong cumulative = 0;
        foreach (var choice in choices)
        {
            cumulative = checked(cumulative + (ulong)choice.Weight);
            if (draw < cumulative)
            {
                return choice.Value;
            }
        }

        throw new InvalidOperationException("weighted_selection_out_of_range");
    }

    private readonly record struct WeightedChoice<T>(T Value, int Weight);

    private struct SplitMix64(ulong state)
    {
        private ulong _state = state;

        public ulong Next()
        {
            unchecked
            {
                _state += Gamma;
                var value = _state;
                value = (value ^ (value >> 30)) * MixMultiplier1;
                value = (value ^ (value >> 27)) * MixMultiplier2;
                return value ^ (value >> 31);
            }
        }

        public ulong NextBounded(ulong bound)
        {
            if (bound == 0)
            {
            ArgumentOutOfRangeException.ThrowIfZero(bound);
            }

            var threshold = unchecked(0UL - bound) % bound;
            while (true)
            {
                var value = Next();
                if (value >= threshold)
                {
                    return value % bound;
                }
            }
        }
    }
}

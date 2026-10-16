using System.Collections.Immutable;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.Run.Evidence;

/// <summary>Trusted run context used to validate persisted evidence against the selected scenario.</summary>
/// <remarks>The selected configuration comes from the in-memory scenario resolver; run requirements and broker inventory
/// are supplied independently by the host. No expectation is loaded from evidence artifacts.</remarks>
public sealed record RunEvidenceExpectations
{
    public RunEvidenceExpectations(ResolvedRoleConfiguration resolvedConfiguration, bool requireStatisticsEvidence,
        ImmutableArray<string> expectedBrokerNames)
    {
        ArgumentNullException.ThrowIfNull(resolvedConfiguration);
        if (resolvedConfiguration.Role != KafkaClientRole.LoadGenProducer)
            throw new ArgumentException("evidence_expectations_require_loadgen_producer", nameof(resolvedConfiguration));
        if (resolvedConfiguration.Settings.IsEmpty || resolvedConfiguration.Omissions.IsDefault)
            throw new ArgumentException("evidence_expectations_require_resolved_configuration", nameof(resolvedConfiguration));
        if (expectedBrokerNames.IsDefault || expectedBrokerNames.Any(string.IsNullOrWhiteSpace) ||
            expectedBrokerNames.Distinct(StringComparer.Ordinal).Count() != expectedBrokerNames.Length)
            throw new ArgumentException("evidence_expectations_require_unique_nonempty_broker_names", nameof(expectedBrokerNames));
        if (requireStatisticsEvidence && expectedBrokerNames.IsEmpty)
            throw new ArgumentException("required_statistics_evidence_needs_expected_brokers", nameof(expectedBrokerNames));

        ResolvedConfiguration = resolvedConfiguration;
        RequireStatisticsEvidence = requireStatisticsEvidence;
        ExpectedBrokerNames = expectedBrokerNames;
    }

    /// <summary>Gets the immutable producer settings selected by the scenario resolver.</summary>
    public ResolvedRoleConfiguration ResolvedConfiguration { get; }

    /// <summary>Gets whether the selected run requires a complete persisted statistics proof.</summary>
    public bool RequireStatisticsEvidence { get; }

    /// <summary>Gets the immutable independently configured broker inventory for the selected run.</summary>
    public ImmutableArray<string> ExpectedBrokerNames { get; }
}

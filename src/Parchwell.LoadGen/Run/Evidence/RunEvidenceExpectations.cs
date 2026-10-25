using System.Collections.Immutable;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.Run.Evidence;

/// <summary>Trusted run context used to validate persisted evidence against the selected scenario.</summary>
/// <remarks>The selected configuration comes from the in-memory scenario resolver. Compatibility callers may supply an
/// independent broker inventory; production LoadGen uses capture-only expectations. No expectation is loaded from evidence artifacts.</remarks>
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
        RequiresIndependentBrokerCoverage = true;
    }

    private RunEvidenceExpectations(ResolvedRoleConfiguration resolvedConfiguration, bool requireStatisticsEvidence)
    {
        ArgumentNullException.ThrowIfNull(resolvedConfiguration);
        if (resolvedConfiguration.Role != KafkaClientRole.LoadGenProducer || resolvedConfiguration.Settings.IsEmpty || resolvedConfiguration.Omissions.IsDefault)
            throw new ArgumentException("evidence_expectations_require_resolved_loadgen_configuration", nameof(resolvedConfiguration));
        ResolvedConfiguration = resolvedConfiguration;
        RequireStatisticsEvidence = requireStatisticsEvidence;
        ExpectedBrokerNames = ImmutableArray<string>.Empty;
        RequiresIndependentBrokerCoverage = false;
    }

    /// <summary>Creates production expectations that validate capture freshness without claiming independent broker coverage.</summary>
    public static RunEvidenceExpectations CaptureOnly(ResolvedRoleConfiguration resolvedConfiguration, bool requireStatisticsEvidence) =>
        new(resolvedConfiguration, requireStatisticsEvidence);

    /// <summary>Gets the immutable producer settings selected by the scenario resolver.</summary>
    public ResolvedRoleConfiguration ResolvedConfiguration { get; }

    /// <summary>Gets whether the selected run requires a complete persisted statistics proof.</summary>
    public bool RequireStatisticsEvidence { get; }

    /// <summary>Gets the immutable independently configured broker inventory for the selected run.</summary>
    public ImmutableArray<string> ExpectedBrokerNames { get; }

    public bool RequiresIndependentBrokerCoverage { get; }
}

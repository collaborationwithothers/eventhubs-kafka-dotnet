using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.Run.Evidence;

/// <summary>Trusted run context used to validate persisted evidence against the selected scenario.</summary>
/// <remarks>This context comes from the in-memory scenario resolver and is never loaded from evidence artifacts.</remarks>
public sealed record RunEvidenceExpectations
{
    public RunEvidenceExpectations(ResolvedRoleConfiguration resolvedConfiguration, bool requireStatisticsEvidence)
    {
        ArgumentNullException.ThrowIfNull(resolvedConfiguration);
        if (resolvedConfiguration.Role != KafkaClientRole.LoadGenProducer)
            throw new ArgumentException("evidence_expectations_require_loadgen_producer", nameof(resolvedConfiguration));
        if (resolvedConfiguration.Settings.IsEmpty || resolvedConfiguration.Omissions.IsDefault)
            throw new ArgumentException("evidence_expectations_require_resolved_configuration", nameof(resolvedConfiguration));

        ResolvedConfiguration = resolvedConfiguration;
        RequireStatisticsEvidence = requireStatisticsEvidence;
    }

    /// <summary>Gets the immutable producer settings selected by the scenario resolver.</summary>
    public ResolvedRoleConfiguration ResolvedConfiguration { get; }

    /// <summary>Gets whether the selected run requires a complete persisted statistics proof.</summary>
    public bool RequireStatisticsEvidence { get; }
}

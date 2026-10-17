using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Parchwell.Kafka.Authentication;

/// <summary>Publishes sanitized OAuth bearer refresh-handler failures.</summary>
/// <remarks>
/// The counter records each failed handler invocation with the scenario, run, and Kafka client
/// context needed to investigate FM-06 (docs/blueprint.md). It never includes token or exception
/// material.
/// </remarks>
internal sealed class KafkaAuthenticationMetrics : IDisposable
{
    internal const string MeterName = "Parchwell.Kafka.Authentication";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _handlerErrors;

    /// <summary>Creates the authentication meter and its stable error counter.</summary>
    internal KafkaAuthenticationMetrics()
    {
        _handlerErrors = _meter.CreateCounter<long>(
            "parchwell.kafka.oauthbearer.refresh_handler.errors",
            "count");
    }

    /// <summary>Records one failed native refresh-handler invocation.</summary>
    /// <param name="failure">The sanitized failure and Kafka client identity.</param>
    internal void Record(KafkaAuthenticationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        var tags = new TagList
        {
            { "scenario", failure.Identity.ScenarioId },
            { "run", failure.Identity.RunId },
            { "role", failure.Identity.Role.ToString() },
            { "client_id", failure.Identity.ClientId },
            { "phase", failure.Phase },
            { "reason", failure.Reason }
        };

        _handlerErrors.Add(1, in tags);
    }

    /// <summary>Disposes the meter and its instruments.</summary>
    public void Dispose() => _meter.Dispose();
}

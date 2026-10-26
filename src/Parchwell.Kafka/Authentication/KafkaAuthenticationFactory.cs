using System.Diagnostics.CodeAnalysis;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Kafka.Authentication;

/// <summary>Creates OAuth bearer sessions and native Kafka builders with refresh handling attached.</summary>
public sealed class KafkaAuthenticationFactory
{
    private readonly IOptionsMonitor<KafkaBaselineOptions> baselineOptions;
    private readonly KafkaWorkloadIdentityOptions identityOptions;
    private readonly IKafkaTokenProvider tokenProvider;
    private readonly TimeProvider timeProvider;
    private readonly KafkaAuthenticationMetrics metrics;

    internal KafkaAuthenticationFactory(
        IOptionsMonitor<KafkaBaselineOptions> baselineOptions,
        IOptions<KafkaWorkloadIdentityOptions> identityOptions,
        IKafkaTokenProvider tokenProvider,
        TimeProvider timeProvider,
        KafkaAuthenticationMetrics metrics)
    {
        this.baselineOptions = baselineOptions ?? throw new ArgumentNullException(nameof(baselineOptions));
        ArgumentNullException.ThrowIfNull(identityOptions);
        this.identityOptions = identityOptions.Value;
        this.tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    /// <summary>Creates a session bound to the selected role's validated namespace and run cancellation.</summary>
    /// <param name="identity">The scenario, run, role and Kafka client identity for this native client.</param>
    /// <param name="cancellationToken">Cancellation forwarded to synchronous token acquisition.</param>
    /// <returns>A new session that should be attached to one native client.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The role's namespace or workload client ID is invalid.</exception>
    public KafkaOAuthBearerSession CreateSession(
        KafkaAuthenticationIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        string scope = identityOptions.TokenScopeOverride ?? KafkaAuthenticationConfiguration.GetScope(baselineOptions.Get(identity.Role.ToString()));
        string principalName = identityOptions.ClientId
            ?? throw new InvalidOperationException("Kafka workload identity client ID is missing.");
        return new KafkaOAuthBearerSession(
            identity, scope, principalName, tokenProvider, timeProvider, metrics, cancellationToken);
    }

    /// <summary>Validates the approved OAuth bearer producer configuration without acquiring a token or creating a client.</summary>
    /// <param name="configuration">The final producer configuration to validate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A required authentication baseline setting is invalid or an excluded credential setting is present.</exception>
    [SuppressMessage("Performance", "CA1822", Justification = "Configuration validation stays on the DI-registered factory under the repository dependency-injection rule.")]
    public void ValidateProducerConfiguration(ProducerConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        KafkaAuthenticationConfiguration.Validate(configuration);
    }

    /// <summary>Creates a native producer builder from a validated snapshot and attaches its OAuth refresh handler.</summary>
    /// <typeparam name="TKey">The producer message key type.</typeparam>
    /// <typeparam name="TValue">The producer message value type.</typeparam>
    /// <param name="configuration">The caller's final mutable producer configuration.</param>
    /// <param name="session">The session attached to the resulting native producer.</param>
    /// <returns>A Confluent producer builder using the validated configuration snapshot.</returns>
    /// <exception cref="ArgumentNullException">Configuration or session is null.</exception>
    /// <exception cref="InvalidOperationException">The configuration does not satisfy the authentication baseline, or the session was already attached to a builder.</exception>
    [SuppressMessage("Performance", "CA1822", Justification = "Native client construction stays on the DI-registered factory under the repository dependency-injection rule.")]
    public ProducerBuilder<TKey, TValue> CreateProducerBuilder<TKey, TValue>(
        ProducerConfig configuration,
        KafkaOAuthBearerSession session)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(session);
        var snapshot = new ProducerConfig(configuration.ToDictionary(
            setting => setting.Key, setting => setting.Value, StringComparer.Ordinal));
        KafkaAuthenticationConfiguration.Validate(snapshot);
        session.ClaimAttachment();
        return new ProducerBuilder<TKey, TValue>(snapshot)
            .SetOAuthBearerTokenRefreshHandler((client, _) => session.Refresh(client));
    }

    /// <summary>Creates a native consumer builder from a validated snapshot and attaches its OAuth refresh handler.</summary>
    /// <typeparam name="TKey">The consumer message key type.</typeparam>
    /// <typeparam name="TValue">The consumer message value type.</typeparam>
    /// <param name="configuration">The caller's final mutable consumer configuration.</param>
    /// <param name="session">The session attached to the resulting native consumer.</param>
    /// <returns>A Confluent consumer builder using the validated configuration snapshot.</returns>
    /// <exception cref="ArgumentNullException">Configuration or session is null.</exception>
    /// <exception cref="InvalidOperationException">The configuration does not satisfy the authentication baseline, or the session was already attached to a builder.</exception>
    [SuppressMessage("Performance", "CA1822", Justification = "Native client construction stays on the DI-registered factory under the repository dependency-injection rule.")]
    public ConsumerBuilder<TKey, TValue> CreateConsumerBuilder<TKey, TValue>(
        ConsumerConfig configuration,
        KafkaOAuthBearerSession session)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(session);
        var snapshot = new ConsumerConfig(configuration.ToDictionary(
            setting => setting.Key, setting => setting.Value, StringComparer.Ordinal));
        KafkaAuthenticationConfiguration.Validate(snapshot);
        session.ClaimAttachment();
        return new ConsumerBuilder<TKey, TValue>(snapshot)
            .SetOAuthBearerTokenRefreshHandler((client, _) => session.Refresh(client));
    }
}

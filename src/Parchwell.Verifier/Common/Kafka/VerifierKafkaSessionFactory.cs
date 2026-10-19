using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Verifier.Common.Kafka;

/// <summary>Constructs the verifier's authenticated native consumer and dependent admin.</summary>
internal sealed class VerifierKafkaSessionFactory
{
    private readonly Func<CancellationToken, VerifierKafkaSession> create;

    public VerifierKafkaSessionFactory(KafkaConfigurationFactory configurations,
        KafkaAuthenticationFactory authentication, IOptions<VerifierHostOptions> options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(configurations);
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        create = cancellationToken =>
        {
            VerifierHostOptions settings = options.Value;
            var acquisition = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IConsumer<byte[], byte[]>? consumer = null;
            try
            {
                var identity = new KafkaAuthenticationIdentity(settings.ScenarioId!, settings.RunId!,
                    KafkaClientRole.VerifierConsumer, settings.VerifierClientId!);
                KafkaOAuthBearerSession authenticationSession = authentication.CreateSession(identity, acquisition.Token);
                consumer = authentication.CreateConsumerBuilder<byte[], byte[]>(
                        configurations.CreateVerifierConsumer().Configuration, authenticationSession)
                    .SetKeyDeserializer(Deserializers.ByteArray)
                    .SetValueDeserializer(Deserializers.ByteArray)
                    .Build();
                IAdminClient admin = new DependentAdminClientBuilder(consumer.Handle).Build();
                return new VerifierKafkaSession(consumer, admin, authenticationSession, clock, settings, acquisition);
            }
            catch
            {
                try { consumer?.Dispose(); }
                finally { acquisition.Dispose(); }
                throw;
            }
        };
    }

    /// <summary>Supplies the native-client construction boundary for isolated handler tests.</summary>
    internal VerifierKafkaSessionFactory(Func<CancellationToken, VerifierKafkaSession> create) =>
        this.create = create ?? throw new ArgumentNullException(nameof(create));

    public VerifierKafkaSession Create(CancellationToken cancellationToken) => create(cancellationToken);
}

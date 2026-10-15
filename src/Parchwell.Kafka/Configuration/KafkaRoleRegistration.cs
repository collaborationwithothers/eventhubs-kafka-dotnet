namespace Parchwell.Kafka.Configuration;

/// <summary>Marks a role as registered, so the factory refuses roles the host never configured.</summary>
internal sealed record KafkaRoleRegistration(KafkaClientRole Role);

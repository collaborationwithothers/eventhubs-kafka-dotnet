using System.Globalization;
using Confluent.Kafka;

namespace Parchwell.Enrichment.ConsumerProcessing;

internal interface IConsumerProcessingPlanSource
{
    ConsumerProcessingPlan? GetPlan(string scenarioId, string configurationId);
}

internal sealed record ConsumerSlowMessage(int Ordinal, int DurationMs, string Mode);

internal sealed record ConsumerProcessingPlan
{
    public string ConfigurationId { get; init; } = string.Empty;
    public int ProcessingDelayMs { get; init; }
    public int WindowMs { get; init; }
    public ConsumerSlowMessage? SlowMessage { get; init; }
    public IReadOnlyDictionary<string, string> ConsumerSettings { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public void ApplyAndValidate(ConsumerProcessingOptions options, ConsumerConfig configuration)
    {
        if (!StringComparer.Ordinal.Equals(ConfigurationId, options.Phase) || ProcessingDelayMs is < 0 or > 60000 ||
            WindowMs is < 1000 or > 3600000 || ConsumerSettings is null ||
            SlowMessage is { } slow && (options.Role != "subject" || slow.Ordinal is < 1 or > 1000000 ||
                slow.DurationMs is < 1 or > 3600000 || slow.Mode is not ("inline" or "pause") || WindowMs <= slow.DurationMs))
            throw new InvalidOperationException("consumer_plan_invalid");
        foreach ((string key, string value) in ConsumerSettings)
        {
            bool valid = key switch
            {
                "session.timeout.ms" => IntegerInRange(value, 1, 3600000),
                "max.poll.interval.ms" => IntegerInRange(value, 1, 86400000),
                "enable.auto.offset.store" => value is "true" or "false",
                _ => throw new InvalidOperationException("consumer_setting_not_supported")
            };
            if (!valid) throw new InvalidOperationException("consumer_plan_invalid");
            configuration.Set(key, value);
        }
        if ((configuration.MaxPollIntervalMs ?? 300000) < (configuration.SessionTimeoutMs ?? 30000) ||
            SlowMessage?.Mode == "pause" && configuration.EnableAutoOffsetStore != false)
            throw new InvalidOperationException("consumer_plan_invalid");
        configuration.GroupId = options.GroupId;
        configuration.ClientId = options.PodName;
    }

    private static bool IntegerInRange(string value, int minimum, int maximum) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number >= minimum && number <= maximum;
}

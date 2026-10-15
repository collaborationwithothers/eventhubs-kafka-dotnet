namespace Parchwell.Kafka.Configuration;

internal sealed class KafkaRequiredInput
{
    internal static readonly KafkaRequiredInput NamespaceEndpoint = new(nameof(KafkaBaselineOptions.NamespaceName),
        options => options.NamespaceName, IsNamespaceLabel,
        "NamespaceName must be a single ASCII DNS label with alphanumeric boundaries.",
        value => $"{value}.servicebus.windows.net:9093");
    internal static readonly KafkaRequiredInput ClientId = new(nameof(KafkaBaselineOptions.ClientId),
        options => options.ClientId, IsIdentity,
        "ClientId must be non-empty and contain no whitespace or control characters.", value => value);
    internal static readonly KafkaRequiredInput EnrichmentGroupId = new(nameof(KafkaBaselineOptions.EnrichmentGroupId),
        options => options.EnrichmentGroupId, IsIdentity,
        "EnrichmentGroupId must be non-empty and contain no whitespace or control characters.", value => value);

    private readonly Func<KafkaBaselineOptions, string?> read;
    private readonly Func<string?, bool> isValid;
    private readonly Func<string, string> format;

    private KafkaRequiredInput(string name, Func<KafkaBaselineOptions, string?> read,
        Func<string?, bool> isValid, string validationMessage, Func<string, string> format)
    {
        Name = name;
        this.read = read;
        this.isValid = isValid;
        ValidationMessage = validationMessage;
        this.format = format;
    }

    internal string Name { get; }
    internal string ValidationMessage { get; }
    internal bool IsValid(KafkaBaselineOptions options) => isValid(read(options));

    internal string ResolveValue(KafkaBaselineOptions options, string key)
    {
        var value = read(options);
        if (!isValid(value))
            throw new InvalidOperationException($"Setting '{key}' requires valid input '{Name}'.");
        return format(value!);
    }

    private static bool IsNamespaceLabel(string? value)
    {
        // RFC 1035 section 2.3.4: DNS labels are limited to 63 octets.
        // https://www.rfc-editor.org/rfc/rfc1035#section-2.3.4
        return !string.IsNullOrEmpty(value) && value.Length <= 63 &&
            char.IsAsciiLetterOrDigit(value[0]) && char.IsAsciiLetterOrDigit(value[^1]) &&
            value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
    }

    private static bool IsIdentity(string? value) =>
        !string.IsNullOrEmpty(value) && !value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character));
}

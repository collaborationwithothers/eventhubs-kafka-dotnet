using Microsoft.Extensions.Options;

namespace Parchwell.Kafka.Configuration;

internal sealed class KafkaBaselineOptionsValidator : IValidateOptions<KafkaBaselineOptions>
{
    private readonly KafkaClientRole role;

    internal KafkaBaselineOptionsValidator(KafkaClientRole role)
    {
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));
        this.role = role;
    }

    public ValidateOptionsResult Validate(string? name, KafkaBaselineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!string.Equals(name, role.ToString(), StringComparison.Ordinal))
            return ValidateOptionsResult.Skip;

        var failures = new List<string>();
        if (!IsNamespaceLabel(options.NamespaceName))
            failures.Add("NamespaceName must be a single ASCII DNS label with alphanumeric boundaries.");
        if (!IsIdentity(options.ClientId))
            failures.Add("ClientId must be non-empty and contain no whitespace or control characters.");
        if (role == KafkaClientRole.EnrichmentConsumer && !IsIdentity(options.EnrichmentGroupId))
            failures.Add("EnrichmentGroupId must be non-empty and contain no whitespace or control characters.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsNamespaceLabel(string? value)
    {
        // RFC 1035 section 2.3.4 limits DNS labels to 63 octets. This is a DNS syntax
        // constraint, not an assertion about Azure namespace naming limits.
        // https://www.rfc-editor.org/rfc/rfc1035#section-2.3.4
        return !string.IsNullOrEmpty(value) && value.Length <= 63 &&
            char.IsAsciiLetterOrDigit(value[0]) && char.IsAsciiLetterOrDigit(value[^1]) &&
            value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
    }

    private static bool IsIdentity(string? value) =>
        !string.IsNullOrEmpty(value) && !value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character));
}

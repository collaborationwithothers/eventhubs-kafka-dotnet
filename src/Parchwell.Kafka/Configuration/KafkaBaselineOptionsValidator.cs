using Microsoft.Extensions.Options;

namespace Parchwell.Kafka.Configuration;

/// <summary>Checks one role's required caller inputs, at host start and on every factory call.</summary>
/// <remarks>
/// A role needs only the inputs its catalogue entries use. For example, a producer role does not
/// need an enrichment group ID. Options are named after the role, so one validator is registered
/// per role and ignores options with any other name.
/// </remarks>
internal sealed class KafkaBaselineOptionsValidator : IValidateOptions<KafkaBaselineOptions>
{
    private readonly KafkaClientRole role;
    private readonly KafkaRequiredInput[] requiredInputs;

    internal KafkaBaselineOptionsValidator(KafkaClientRole role)
    {
        this.role = role;
        requiredInputs = BaselineSettingsCatalog.GetEntries(role).Select(entry => entry.RequiredInput)
            .OfType<KafkaRequiredInput>().Distinct().ToArray();
    }

    public ValidateOptionsResult Validate(string? name, KafkaBaselineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!string.Equals(name, role.ToString(), StringComparison.Ordinal))
            return ValidateOptionsResult.Skip;

        var failures = requiredInputs.Where(input => !input.IsValid(options))
            .Select(input => input.ValidationMessage).ToArray();
        return failures.Length == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

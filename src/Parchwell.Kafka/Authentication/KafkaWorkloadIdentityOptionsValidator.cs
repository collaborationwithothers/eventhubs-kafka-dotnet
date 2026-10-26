using Microsoft.Extensions.Options;

namespace Parchwell.Kafka.Authentication;

/// <summary>Validates the required workload identity option values.</summary>
internal sealed class KafkaWorkloadIdentityOptionsValidator : IValidateOptions<KafkaWorkloadIdentityOptions>
{
    /// <summary>Returns failures for missing or whitespace-only workload identity values.</summary>
    public ValidateOptionsResult Validate(string? name, KafkaWorkloadIdentityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        List<string> failures = [];
        if (string.IsNullOrWhiteSpace(options.TenantId)) failures.Add("TenantId is required.");
        if (string.IsNullOrWhiteSpace(options.ClientId)) failures.Add("ClientId is required.");
        if (string.IsNullOrWhiteSpace(options.TokenFilePath)) failures.Add("TokenFilePath is required.");
        if (options.TokenScopeOverride is not (null or "https://storage.azure.com/.default"))
            failures.Add("TokenScopeOverride must be the approved storage scope or null.");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

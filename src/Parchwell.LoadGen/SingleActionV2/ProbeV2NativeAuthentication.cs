using System.Text.RegularExpressions;

namespace Parchwell.LoadGen.SingleActionV2;

internal static partial class ProbeV2NativeAuthentication
{
    internal static Dictionary<string, string> Apply(ProbeV2Options options, IReadOnlyDictionary<string, string> resolved)
    {
        var applied = new Dictionary<string, string>(resolved, StringComparer.Ordinal);
        string resource = "https://" + options.NamespaceName + ".servicebus.windows.net";
        switch (options.NativeAuthentication)
        {
            case "jwt-bearer":
                applied["sasl.oauthbearer.metadata.authentication.type"] = "none";
                applied["sasl.oauthbearer.grant.type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer";
                applied["sasl.oauthbearer.assertion.file"] = options.FederatedTokenFile;
                applied["sasl.oauthbearer.token.endpoint.url"] = "https://login.microsoftonline.com/" + options.TenantId + "/oauth2/v2.0/token";
                applied["sasl.oauthbearer.scope"] = resource + "/.default";
                applied["https.ca.location"] = "/etc/ssl/certs/ca-certificates.crt";
                break;
            case "azure-imds-workload":
                applied["sasl.oauthbearer.metadata.authentication.type"] = "azure_imds";
                applied["sasl.oauthbearer.config"] = "query=api-version=2018-02-01&resource=" + resource + "&client_id=" + options.WorkloadIdentityClientId;
                break;
            case "azure-imds-default":
                applied["sasl.oauthbearer.metadata.authentication.type"] = "azure_imds";
                applied["sasl.oauthbearer.config"] = "query=api-version=2018-02-01&resource=" + resource;
                break;
            default:
                throw new InvalidOperationException("v2_native_mode");
        }
        applied["sasl.oauthbearer.method"] = "oidc";
        applied["sasl.oauthbearer.sub.claim.name"] = "oid";
        applied["debug"] = "security";
        return applied;
    }

    internal static Dictionary<string, string> Recorded(ProbeV2Options options, IReadOnlyDictionary<string, string> applied)
    {
        var recorded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in applied)
        {
            string text = value;
            if (options.FederatedTokenFile.Length != 0) text = text.Replace(options.FederatedTokenFile, "[federated-token-file]", StringComparison.Ordinal);
            if (options.TenantId.Length != 0) text = text.Replace(options.TenantId, "[tenant]", StringComparison.Ordinal);
            if (options.WorkloadIdentityClientId.Length != 0) text = text.Replace(options.WorkloadIdentityClientId, "[client]", StringComparison.Ordinal);
            recorded[key] = text;
        }
        return recorded;
    }

    internal static string Redact(string text, ProbeV2Options options)
    {
        if (options.FederatedTokenFile.Length != 0) text = text.Replace(options.FederatedTokenFile, "[federated-token-file]", StringComparison.Ordinal);
        text = PrincipalPattern().Replace(text, match =>
            match.Groups[1].Length == 0 ? "principal=[empty]" :
            string.Equals(match.Groups[1].Value, options.NativePrincipalId, StringComparison.OrdinalIgnoreCase) ? "principal=[workload]" : "principal=[other]");
        text = JwtPattern().Replace(text, "[jwt]");
        return GuidPattern().Replace(text, "[guid]");
    }

    [GeneratedRegex(@"principal=([^)\s]*)", RegexOptions.CultureInvariant)]
    private static partial Regex PrincipalPattern();
    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*", RegexOptions.CultureInvariant)]
    private static partial Regex JwtPattern();
    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.CultureInvariant)]
    private static partial Regex GuidPattern();
}

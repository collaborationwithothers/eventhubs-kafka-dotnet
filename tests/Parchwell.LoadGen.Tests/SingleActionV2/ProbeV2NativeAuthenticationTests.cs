using Confluent.Kafka;
using Parchwell.LoadGen.SingleActionV2;
using Xunit;

namespace Parchwell.LoadGen.Tests.SingleActionV2;

#pragma warning disable CA1707
public sealed class ProbeV2NativeAuthenticationTests
{
    private static readonly Dictionary<string, string> Resolved = new(StringComparer.Ordinal)
    {
        ["bootstrap.servers"] = "127.0.0.1:1",
        ["security.protocol"] = "sasl_ssl",
        ["sasl.mechanism"] = "OAUTHBEARER",
        ["sasl.oauthbearer.method"] = "default",
        ["sasl.oauthbearer.metadata.authentication.type"] = "none"
    };
    private static readonly string[] ExcludedKeys =
    [
        "sasl.oauthbearer.client.id", "sasl.oauthbearer.client.secret", "sasl.oauthbearer.assertion.jwt.template.file", "sasl.oauthbearer.extensions"
    ];

    [Fact]
    public void GivenJwtBearer_When_Applying_Then_SetsExactlyTheListedKeys()
    {
        Dictionary<string, string> applied = ProbeV2NativeAuthentication.Apply(ProbeV2OptionsTests.NativeOptions("nativejwt", "jwt-bearer"), Resolved);
        var expected = new Dictionary<string, string>(Resolved, StringComparer.Ordinal)
        {
            ["sasl.oauthbearer.method"] = "oidc",
            ["sasl.oauthbearer.sub.claim.name"] = "oid",
            ["debug"] = "security",
            ["sasl.oauthbearer.metadata.authentication.type"] = "none",
            ["sasl.oauthbearer.grant.type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["sasl.oauthbearer.assertion.file"] = "/var/run/secrets/azure/tokens/azure-identity-token",
            ["sasl.oauthbearer.token.endpoint.url"] = "https://login.microsoftonline.com/11111111-1111-1111-1111-111111111111/oauth2/v2.0/token",
            ["sasl.oauthbearer.scope"] = "https://pwtest.servicebus.windows.net/.default"
        };
        Assert.Equal(expected.OrderBy(pair => pair.Key, StringComparer.Ordinal), applied.OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    [Fact]
    public void GivenAzureImdsWorkload_When_Applying_Then_SetsExactlyTheListedKeys()
    {
        Dictionary<string, string> applied = ProbeV2NativeAuthentication.Apply(ProbeV2OptionsTests.NativeOptions("nativeimds", "azure-imds-workload"), Resolved);
        var expected = new Dictionary<string, string>(Resolved, StringComparer.Ordinal)
        {
            ["sasl.oauthbearer.method"] = "oidc",
            ["sasl.oauthbearer.sub.claim.name"] = "oid",
            ["debug"] = "security",
            ["sasl.oauthbearer.metadata.authentication.type"] = "azure_imds",
            ["sasl.oauthbearer.config"] = "query=api-version=2018-02-01&resource=https://pwtest.servicebus.windows.net&client_id=22222222-2222-2222-2222-222222222222"
        };
        Assert.Equal(expected.OrderBy(pair => pair.Key, StringComparer.Ordinal), applied.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.DoesNotContain("sasl.oauthbearer.grant.type", applied.Keys);
        Assert.DoesNotContain("sasl.oauthbearer.token.endpoint.url", applied.Keys);
    }

    [Fact]
    public void GivenAzureImdsDefault_When_Applying_Then_SetsExactlyTheListedKeys()
    {
        Dictionary<string, string> applied = ProbeV2NativeAuthentication.Apply(ProbeV2OptionsTests.NativeOptions("nativeimdsnode", "azure-imds-default"), Resolved);
        var expected = new Dictionary<string, string>(Resolved, StringComparer.Ordinal)
        {
            ["sasl.oauthbearer.method"] = "oidc",
            ["sasl.oauthbearer.sub.claim.name"] = "oid",
            ["debug"] = "security",
            ["sasl.oauthbearer.metadata.authentication.type"] = "azure_imds",
            ["sasl.oauthbearer.config"] = "query=api-version=2018-02-01&resource=https://pwtest.servicebus.windows.net"
        };
        Assert.Equal(expected.OrderBy(pair => pair.Key, StringComparer.Ordinal), applied.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.DoesNotContain("sasl.oauthbearer.grant.type", applied.Keys);
        Assert.DoesNotContain("sasl.oauthbearer.token.endpoint.url", applied.Keys);
    }

    [Theory]
    [InlineData("nativejwt", "jwt-bearer")]
    [InlineData("nativeimds", "azure-imds-workload")]
    [InlineData("nativeimdsnode", "azure-imds-default")]
    public void GivenAnyMode_When_Applying_Then_NoExcludedKeyIsSet(string stage, string mode)
    {
        Dictionary<string, string> applied = ProbeV2NativeAuthentication.Apply(ProbeV2OptionsTests.NativeOptions(stage, mode), Resolved);
        Assert.All(ExcludedKeys, key => Assert.DoesNotContain(key, applied.Keys));
        Assert.DoesNotContain(applied.Keys, key => key.StartsWith("sasl.oauthbearer.assertion.private.key.", StringComparison.Ordinal) ||
            key.StartsWith("sasl.oauthbearer.assertion.claim.", StringComparison.Ordinal));
        Assert.Equal("127.0.0.1:1", applied["bootstrap.servers"]);
        Assert.Equal("sasl_ssl", applied["security.protocol"]);
        Assert.Equal("OAUTHBEARER", applied["sasl.mechanism"]);
        Assert.Equal("default", Resolved["sasl.oauthbearer.method"]);
    }

    [Fact]
    public void GivenUnknownMode_When_Applying_Then_Throws()
    {
        ProbeV2Options options = ProbeV2OptionsTests.NativeOptions("nativejwt", "other");
        Assert.Equal("v2_native_mode", Assert.Throws<InvalidOperationException>(() => ProbeV2NativeAuthentication.Apply(options, Resolved)).Message);
    }

    [Fact]
    public void GivenJwtBearerOutput_When_Recording_Then_TokenFileTenantAreReplaced()
    {
        ProbeV2Options options = ProbeV2OptionsTests.NativeOptions("nativejwt", "jwt-bearer");
        Dictionary<string, string> recorded = ProbeV2NativeAuthentication.Recorded(options, ProbeV2NativeAuthentication.Apply(options, Resolved));
        Assert.DoesNotContain(recorded.Values, value => value.Contains("11111111-", StringComparison.Ordinal) ||
            value.Contains("22222222-", StringComparison.Ordinal) ||
            value.Contains("/var/run/secrets/azure/tokens/azure-identity-token", StringComparison.Ordinal));
        Assert.Contains("[tenant]", string.Concat(recorded.Values), StringComparison.Ordinal);
        Assert.Contains("[federated-token-file]", string.Concat(recorded.Values), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAzureImdsWorkloadOutput_When_Recording_Then_ClientIsReplaced()
    {
        ProbeV2Options options = ProbeV2OptionsTests.NativeOptions("nativeimds", "azure-imds-workload");
        Dictionary<string, string> recorded = ProbeV2NativeAuthentication.Recorded(options, ProbeV2NativeAuthentication.Apply(options, Resolved));
        Assert.Contains("[client]", string.Concat(recorded.Values), StringComparison.Ordinal);
        Assert.DoesNotContain("22222222-", string.Concat(recorded.Values), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SASL OAUTHBEARER authentication successful (principal=33333333-3333-3333-3333-333333333333)", "SASL OAUTHBEARER authentication successful (principal=[workload])")]
    [InlineData("SASL OAUTHBEARER authentication failed (principal=44444444-4444-4444-4444-444444444444): x", "SASL OAUTHBEARER authentication failed (principal=[other]): x")]
    [InlineData("principal=)", "principal=[empty])")]
    [InlineData("token eyJa.eyJb.c end", "token [jwt] end")]
    [InlineData("Failed to retrieve JWT token from \"https://login.microsoftonline.com/11111111-1111-1111-1111-111111111111/oauth2/v2.0/token\": x (400)",
        "Failed to retrieve JWT token from \"https://login.microsoftonline.com/[guid]/oauth2/v2.0/token\": x (400)")]
    [InlineData("open /var/run/secrets/azure/tokens/azure-identity-token", "open [federated-token-file]")]
    public void GivenClientText_When_Redacting_Then_ValuesAreReplaced(string text, string expected)
    {
        Assert.Equal(expected, ProbeV2NativeAuthentication.Redact(text, ProbeV2OptionsTests.NativeOptions("nativejwt", "jwt-bearer")));
    }

    [Fact]
    public void GivenPrincipalOfDifferentCase_When_Redacting_Then_ItIsTheWorkloadPrincipal()
    {
        ProbeV2Options options = ProbeV2OptionsTests.NativeOptions("nativejwt", "jwt-bearer");
        options.NativePrincipalId = "abcdef00-0000-0000-0000-000000000000";
        Assert.Equal("SASL OAUTHBEARER authentication successful (principal=[workload])",
            ProbeV2NativeAuthentication.Redact("SASL OAUTHBEARER authentication successful (principal=ABCDEF00-0000-0000-0000-000000000000)", options));
    }

    [Fact]
    public void GivenEmptyTokenFileAndTenant_When_RedactingAndRecording_Then_NeitherThrows()
    {
        ProbeV2Options options = ProbeV2OptionsTests.NativeOptions("nativejwt", "jwt-bearer");
        options.FederatedTokenFile = "";
        options.TenantId = "";
        Assert.Equal("principal=[empty])", ProbeV2NativeAuthentication.Redact("principal=)", options));
        Dictionary<string, string> applied = ProbeV2NativeAuthentication.Apply(options, Resolved);
        Assert.Equal(applied.OrderBy(pair => pair.Key, StringComparer.Ordinal), ProbeV2NativeAuthentication.Recorded(options, applied).OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("nativejwt", "jwt-bearer")]
    [InlineData("nativeimds", "azure-imds-workload")]
    [InlineData("nativeimdsnode", "azure-imds-default")]
    public void GivenAppliedSettings_When_BuildingRealProducer_Then_PinnedBinaryAcceptsThem(string stage, string mode)
    {
        Dictionary<string, string> applied = ProbeV2NativeAuthentication.Apply(ProbeV2OptionsTests.NativeOptions(stage, mode), Resolved);
        using IProducer<byte[], byte[]> producer = new ProducerBuilder<byte[], byte[]>(new ProducerConfig(applied)).Build();
        Assert.NotNull(producer);
    }
}

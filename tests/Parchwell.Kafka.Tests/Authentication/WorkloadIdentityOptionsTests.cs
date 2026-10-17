using Microsoft.Extensions.Options;
using Parchwell.Kafka.Authentication;
using Xunit;

namespace Parchwell.Kafka.Tests.Authentication;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class WorkloadIdentityOptionsTests
{
    [Theory]
    [InlineData(null, "application", "/synthetic/token")]
    [InlineData("", "application", "/synthetic/token")]
    [InlineData(" ", "application", "/synthetic/token")]
    [InlineData("tenant", null, "/synthetic/token")]
    [InlineData("tenant", "", "/synthetic/token")]
    [InlineData("tenant", " ", "/synthetic/token")]
    [InlineData("tenant", "application", null)]
    [InlineData("tenant", "application", "")]
    [InlineData("tenant", "application", " ")]
    public void Given_MissingOrWhitespaceIdentityValue_When_Validating_Then_Rejects(string? tenant, string? client, string? path)
    {
        var validator = new KafkaWorkloadIdentityOptionsValidator();
        var result = validator.Validate(Options.DefaultName, new() { TenantId = tenant, ClientId = client, TokenFilePath = path });
        Assert.True(result.Failed);
    }

    [Fact]
    public void Given_NonblankSyntheticValues_When_Validating_Then_AcceptsWithoutReadingFile()
    {
        var validator = new KafkaWorkloadIdentityOptionsValidator();
        var result = validator.Validate(Options.DefaultName,
            new() { TenantId = "not-required-to-be-guid", ClientId = "synthetic-app", TokenFilePath = "relative/nonexistent.token" });
        Assert.True(result.Succeeded);
    }
}

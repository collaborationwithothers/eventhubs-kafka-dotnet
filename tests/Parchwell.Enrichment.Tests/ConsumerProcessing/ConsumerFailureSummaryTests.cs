using System.Net.Http;
using System.Net.Sockets;
using Azure;
using Azure.Identity;
using Confluent.Kafka;
using Parchwell.Enrichment.ConsumerProcessing;
using Xunit;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing;

#pragma warning disable CA1707
public class ConsumerFailureSummaryTests
{
    private const string Tenant = "11111111-2222-3333-4444-555555555555";

    [Fact]
    public async Task Given_registration_throws_403_When_executed_Then_status_and_error_code_are_logged_without_message()
    {
        using var harness = new ConsumerTestHarness();
        harness.Store.RegistrationException = new RequestFailedException(403, "secret-url https://acct.blob.core.windows.net/x " + Tenant, "AuthorizationPermissionMismatch", null);
        Assert.Equal(1, await harness.RunAsync());
        string output = harness.Error.ToString();
        Assert.StartsWith("consumer_processing_failed reason=consumer_registration_failed ", output, StringComparison.Ordinal);
        Assert.Contains("status=403", output, StringComparison.Ordinal);
        Assert.Contains("error_code=AuthorizationPermissionMismatch", output, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-url", output, StringComparison.Ordinal);
        Assert.DoesNotContain(Tenant, output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_authentication_failure_with_guid_When_executed_Then_aadsts_code_is_logged_and_guid_is_not()
    {
        using var harness = new ConsumerTestHarness();
        harness.Store.RegistrationException = new AuthenticationFailedException($"AADSTS700016: Application {Tenant} was not found in tenant.");
        Assert.Equal(1, await harness.RunAsync());
        string output = harness.Error.ToString();
        Assert.Contains("AADSTS700016", output, StringComparison.Ordinal);
        Assert.DoesNotContain(Tenant, output, StringComparison.Ordinal);
        Assert.DoesNotContain("not found", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_nested_chain_When_described_Then_every_type_appears_in_order()
    {
        var chain = new InvalidOperationException("outer-message",
            new HttpRequestException("http-message", new SocketException((int)SocketError.ConnectionRefused)));
        string summary = ExceptionSummary.Describe(chain);
        Assert.StartsWith("System.InvalidOperationException>System.Net.Http.HttpRequestException(http_error=", summary, StringComparison.Ordinal);
        Assert.Contains(">System.Net.Sockets.SocketException(socket_error=ConnectionRefused)", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("message", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_kafka_exception_When_described_Then_code_and_fatal_flag_appear()
    {
        string summary = ExceptionSummary.Describe(new KafkaException(new Error(ErrorCode.Local_Transport, "broker-host:9093 reason", true)));
        Assert.Equal("Confluent.Kafka.KafkaException(error_code=Local_Transport is_fatal=True)", summary);
    }

    [Fact]
    public void Given_authentication_exception_without_code_When_described_Then_type_only()
    {
        Assert.Equal("Azure.Identity.AuthenticationFailedException",
            ExceptionSummary.Describe(new AuthenticationFailedException("no code " + Tenant)));
    }

    [Fact]
    public void Given_unknown_exception_When_described_Then_type_only()
    {
        Assert.Equal("System.InvalidOperationException", ExceptionSummary.Describe(new InvalidOperationException("x " + Tenant)));
    }

    [Fact]
    public void Given_unsafe_error_code_When_described_Then_characters_are_replaced()
    {
        string summary = ExceptionSummary.Describe(new RequestFailedException(400, "m", "Bad Code\nhttps://x", null));
        Assert.DoesNotContain('\n', summary);
        Assert.DoesNotContain(' ', summary.Split("error_code=")[1]);
    }

    [Theory]
    [InlineData(ExceptionSummary.MaxChainDepth - 1, false)]
    [InlineData(ExceptionSummary.MaxChainDepth, false)]
    [InlineData(ExceptionSummary.MaxChainDepth + 1, true)]
    public void Given_chain_depth_When_described_Then_chain_is_cut_above_the_limit(int depth, bool truncated)
    {
        Exception chain = new InvalidOperationException();
        for (int index = 1; index < depth; index++) chain = new InvalidOperationException("m", chain);
        string summary = ExceptionSummary.Describe(chain);
        Assert.Equal(truncated, summary.EndsWith(">truncated", StringComparison.Ordinal));
        Assert.Equal(Math.Min(depth, ExceptionSummary.MaxChainDepth), summary.Split('>').Count(part => part.StartsWith("System.", StringComparison.Ordinal)));
    }

    [Fact]
    public void Given_long_type_name_When_described_Then_summary_stays_within_the_byte_bound()
    {
        Exception chain = new InvalidOperationException();
        for (int index = 0; index < 20; index++) chain = new RequestFailedException(500, "m", new string('A', 500), chain);
        Assert.True(ExceptionSummary.Describe(chain).Length <= ExceptionSummary.MaxBytes);
    }
}
#pragma warning restore CA1707

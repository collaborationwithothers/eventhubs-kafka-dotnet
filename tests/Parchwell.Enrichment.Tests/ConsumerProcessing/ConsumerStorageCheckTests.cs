using System.Net;
using System.Net.Sockets;
using System.Text;
using Azure;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Parchwell.Enrichment.ConsumerProcessing;
using Xunit;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class ConsumerStorageCheckTests
{
    private const string Job = "c03-1-c0-storage-check-a1";

    [Fact]
    public async Task Given_write_access_When_checked_Then_probe_is_created_once_and_deleted()
    {
        using var handler = new Handler();
        var (code, line) = await RunAsync(handler);
        Assert.Equal(0, code);
        Assert.Equal("consumer_storage_check result=ok", line);
        Assert.Equal(["PUT /consumer-record/c03-1/C0/storage-check/" + Job + ".probe", "DELETE /consumer-record/c03-1/C0/storage-check/" + Job + ".probe"], handler.Requests);
    }

    [Fact]
    public void Given_probe_name_When_built_Then_it_is_outside_every_repeat_consumer_prefix()
    {
        var options = ConsumerStorageCheck.Options.Read(Configuration());
        Assert.Equal("c03-1/C0/storage-check/" + Job + ".probe", options.ProbeBlob);
        Assert.DoesNotMatch("/r[1-5]/consumer/", options.ProbeBlob);
    }

    [Fact]
    public async Task Given_authorization_failure_When_checked_Then_only_status_and_error_code_are_reported()
    {
        using var handler = new Handler { PutStatus = HttpStatusCode.Forbidden, PutCode = "AuthorizationPermissionMismatch" };
        var (code, line) = await RunAsync(handler);
        Assert.Equal(1, code);
        Assert.Equal("consumer_storage_check result=failed kind=http status=403 code=AuthorizationPermissionMismatch", line);
        Assert.DoesNotContain("synthetic-secret-message", line, StringComparison.Ordinal);
        Assert.Equal(["PUT /consumer-record/c03-1/C0/storage-check/" + Job + ".probe"], handler.Requests);
    }

    [Fact]
    public async Task Given_delete_failure_After_write_When_checked_Then_it_is_reported_and_exit_is_3()
    {
        using var handler = new Handler { DeleteStatus = HttpStatusCode.Forbidden, DeleteCode = "AuthorizationPermissionMismatch" };
        var (code, line) = await RunAsync(handler);
        Assert.Equal(3, code);
        Assert.Equal("consumer_storage_check result=probe_delete_failed kind=http status=403 code=AuthorizationPermissionMismatch", line);
    }

    [Theory]
    [InlineData("ConsumerStorageCheck:RunId")]
    [InlineData("ConsumerStorageCheck:Phase")]
    [InlineData("ConsumerStorageCheck:JobName")]
    [InlineData("ConsumerStorageCheck:BlobEndpoint")]
    [InlineData("PARCHWELL_JOB_NAME")]
    public async Task Given_a_missing_setting_When_checked_Then_no_request_is_made(string missing)
    {
        using var handler = new Handler();
        var (code, line) = await RunAsync(handler, Configuration(missing));
        Assert.Equal(2, code);
        Assert.Equal("consumer_storage_check result=configuration_invalid", line);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("ConsumerStorageCheck:Phase", "C2")]
    [InlineData("ConsumerStorageCheck:BlobEndpoint", "http://synthetic.blob.test/")]
    [InlineData("ConsumerStorageCheck:JobName", "other-job")]
    [InlineData("ConsumerStorageCheck:RunId", "C03-1")]
    public async Task Given_an_invalid_setting_When_checked_Then_no_request_is_made(string key, string value)
    {
        using var handler = new Handler();
        var (code, _) = await RunAsync(handler, Configuration(overrides: (key, value)));
        Assert.Equal(2, code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void Given_exceptions_When_summarized_Then_only_codes_survive()
    {
        Assert.Equal("kind=aadsts code=AADSTS70011", ConsumerStorageCheck.Summarize(
            new AuthenticationFailedException("AADSTS70011: The provided request must include a client_assertion secret-token-text")));
        Assert.Equal("kind=socket code=HostNotFound", ConsumerStorageCheck.Summarize(
            new HttpRequestException("name resolution failed for synthetic-host", new SocketException((int)SocketError.HostNotFound))));
        Assert.Equal("kind=http status=409", ConsumerStorageCheck.Summarize(new RequestFailedException(409, "synthetic-secret-message")));
        Assert.Equal("kind=other", ConsumerStorageCheck.Summarize(new InvalidOperationException("synthetic-secret-message")));
    }

    private static IConfiguration Configuration(string? missing = null, (string Key, string Value)? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConsumerStorageCheck:RunId"] = "c03-1",
            ["ConsumerStorageCheck:Phase"] = "C0",
            ["ConsumerStorageCheck:JobName"] = Job,
            ["ConsumerStorageCheck:BlobEndpoint"] = "https://synthetic.blob.test/",
            ["PARCHWELL_JOB_NAME"] = Job,
        };
        if (missing is not null) values.Remove(missing);
        if (overrides is { } change) values[change.Key] = change.Value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static async Task<(int Code, string Line)> RunAsync(Handler handler, IConfiguration? configuration = null)
    {
        using var output = new StringWriter();
        int code = await ConsumerStorageCheck.RunAsync(configuration ?? Configuration(), options => new BlobContainerClient(
            new Uri(new Uri(options.BlobEndpoint), ConsumerStorageCheck.Container), new BlobClientOptions
            {
                Transport = new HttpClientTransport(new HttpClient(handler, disposeHandler: false)),
                Retry = { MaxRetries = 0 }
            }), output, TestContext.Current.CancellationToken);
        return (code, output.ToString().TrimEnd());
    }

    private sealed class Handler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public HttpStatusCode PutStatus { get; init; } = HttpStatusCode.Created;
        public string PutCode { get; init; } = string.Empty;
        public HttpStatusCode DeleteStatus { get; init; } = HttpStatusCode.Accepted;
        public string DeleteCode { get; init; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.Method + " " + request.RequestUri!.AbsolutePath);
            if (request.Method == HttpMethod.Put)
            {
                Assert.Equal("*", Assert.Single(request.Headers.GetValues("If-None-Match")));
                return Task.FromResult(Reply(PutStatus, PutCode));
            }
            return Task.FromResult(Reply(DeleteStatus, DeleteCode));
        }

        private static HttpResponseMessage Reply(HttpStatusCode status, string code)
        {
            var response = new HttpResponseMessage(status);
            if ((int)status >= 400)
            {
                response.Headers.Add("x-ms-error-code", code);
                response.Content = new StringContent("<Error><Code>" + code + "</Code><Message>synthetic-secret-message</Message></Error>", Encoding.UTF8, "application/xml");
            }
            return response;
        }
    }
}

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
using Xunit;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;

namespace Parchwell.Runner.Tests.Cost;

public sealed class AzureCliRunStoreTests
{
    [Fact]
    public async Task Given_Read_When_Downloaded_Then_UsesPropertyEtagAndExplicitEntraLogin()
    {
        var process = new RecordingProcess();
        var store = new AzureCliRunStore(process, "https://stexample.blob.core.windows.net", Path.GetTempPath());
        process.Handler = request =>
        {
            if (request.Arguments.Contains("show")) return new(0, "{\"properties\":{\"etag\":\"etag-a\",\"contentLength\":3}}", "");
            var arguments = request.Arguments.ToList();
            File.WriteAllBytes(arguments[arguments.IndexOf("--file") + 1], [1, 2, 3]);
            Assert.Equal("etag-a", arguments[arguments.IndexOf("--if-match") + 1]);
            return new(0, "", "");
        };
        var result = await store.ReadAsync("ledger", "A01/state.json", 1024, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Bytes);
        Assert.All(process.Requests, request =>
        {
            var arguments = request.Arguments.ToList();
            Assert.Equal("login", arguments[arguments.IndexOf("--auth-mode") + 1]);
            Assert.Equal(TimeSpan.FromSeconds(60), request.Timeout);
            Assert.DoesNotContain("--account-key", arguments);
            Assert.DoesNotContain("--sas-token", arguments);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_ConditionalWrite_When_Uploaded_Then_NoWriteRetryAndCorrectPrecondition(bool create)
    {
        var process = new RecordingProcess { Handler = _ => new(1, "", "ConditionNotMet (412)") };
        var store = new AzureCliRunStore(process, "https://stexample.blob.core.windows.net", Path.GetTempPath());
        var result = await store.WriteAsync("ledger", "A01/state.json", new byte[] { 1 }, create ? null : "etag-a", create, TestContext.Current.CancellationToken);
        Assert.Equal(ArtifactWriteOutcome.Conflict, result);
        var request = Assert.Single(process.Requests);
        Assert.Contains(create ? "--if-none-match" : "--if-match", request.Arguments);
        Assert.Contains(create ? "*" : "etag-a", request.Arguments);
    }

    [Theory]
    [InlineData("https://stexample.blob.core.windows.net/?sig=private")]
    [InlineData("http://stexample.blob.core.windows.net")]
    [InlineData("https://stexample.blob.core.windows.net.attacker.invalid")]
    [InlineData("https://user:secret@stexample.blob.core.windows.net")]
    public void Given_UnsafeAccountUrl_When_Constructed_Then_Rejects(string url) =>
        Assert.Throws<InvalidDataException>(() => new AzureCliRunStore(new RecordingProcess(), url, Path.GetTempPath()));

    [Fact]
    public async Task Given_UnsupportedConditionalFlag_When_CliVerified_Then_BlocksDeployment()
    {
        var process = new RecordingProcess { Handler = _ => new(0, "{}", "") };
        var store = new AzureCliRunStore(process, "https://stexample.blob.core.windows.net", Path.GetTempPath());
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.VerifyCliAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_MissingBlobOrUncertainUpload_When_Invoked_Then_ReturnsExplicitState()
    {
        var process = new RecordingProcess { Handler = _ => new(1, "", "BlobNotFound") };
        var store = new AzureCliRunStore(process, "https://stexample.blob.core.windows.net", Path.GetTempPath());
        Assert.Null(await store.ReadAsync("ledger", "A01/state.json", 1024, TestContext.Current.CancellationToken));
        process.Handler = _ => new(1, "", "connection ended after sending request");
        Assert.Equal(ArtifactWriteOutcome.Uncertain, await store.WriteAsync("ledger", "A01/state.json", new byte[] { 1 }, null, true, TestContext.Current.CancellationToken));
    }

    private sealed class RecordingProcess : IProcessRunner
    {
        public List<ProcessRequest> Requests { get; } = [];
        public Func<ProcessRequest, ProcessResult> Handler { get; set; } = _ => new(0, "", "");
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Handler(request));
        }
    }
}

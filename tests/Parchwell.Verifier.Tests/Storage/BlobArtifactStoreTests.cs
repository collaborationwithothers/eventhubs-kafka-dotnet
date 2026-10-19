using System.Net;
using System.Text;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.VerifyRun.Storage;
using Xunit;

namespace Parchwell.Verifier.Tests.Storage;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
[Trait("Category", "Unit")]
public sealed class BlobArtifactStoreTests
{
    [Theory]
    [InlineData("runs/fixed", "/ledger/runs/fixed/ledger.jsonl")]
    [InlineData("runs/fixed/", "/ledger/runs/fixed/ledger.jsonl")]
    [InlineData("runs//fixed/", "/ledger/runs//fixed/ledger.jsonl")]
    public async Task Given_ExplicitPrefix_When_ReadingLedger_Then_UsesOnlyScopedRead(string prefix, string expectedPath)
    {
        using var transport = new RecordingHandler(() => Success(HttpStatusCode.OK, [1, 2]));
        using var http = new HttpClient(transport);
        var credential = new SyntheticCredential();
        var source = new BlobLedgerArtifactSource(Container("ledger", http, credential), prefix, 1024);

        Assert.Equal(new byte[] { 1, 2 }, await source.ReadAsync("ledger.jsonl", TestContext.Current.CancellationToken));

        Request request = Assert.Single(transport.Requests);
        Assert.Equal("GET", request.Method);
        Assert.Equal(expectedPath, request.Path);
        Assert.Equal(["https://storage.azure.com/.default"], credential.Scopes);
    }

    [Theory]
    [InlineData("index.json")]
    [InlineData("ledger.jsonl")]
    [InlineData("manifest.json")]
    [InlineData("statistics.jsonl")]
    [InlineData("request-lifecycle.jsonl")]
    [InlineData("capabilities.json")]
    public async Task Given_FixedPayloadName_When_Reading_Then_AcceptsEachAllowedSuffix(string name)
    {
        using var transport = new RecordingHandler(() => Success(HttpStatusCode.OK, []));
        using var http = new HttpClient(transport);
        var source = new BlobLedgerArtifactSource(Container("ledger", http), "prefix", 1024);
        Assert.Empty((await source.ReadAsync(name, TestContext.Current.CancellationToken))!);
        Assert.Equal("/ledger/prefix/" + name, Assert.Single(transport.Requests).Path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("foreign.json")]
    [InlineData("../ledger.jsonl")]
    [InlineData("sub/ledger.jsonl")]
    public async Task Given_ArbitraryIndexDeclaredName_When_Reading_Then_RejectsBeforeRequest(string name)
    {
        using var transport = new RecordingHandler(() => Success(HttpStatusCode.OK, []));
        using var http = new HttpClient(transport);
        var source = new BlobLedgerArtifactSource(Container("ledger", http), "prefix", 1024);
        await Assert.ThrowsAsync<ArgumentException>(() => source.ReadAsync(name, TestContext.Current.CancellationToken));
        Assert.Empty(transport.Requests);
    }

    [Theory]
    [InlineData("notes/custom.json")]
    [InlineData("results/custom.json")]
    public async Task Given_ExplicitVerifierName_When_Creating_Then_SendsCreateOnlyToVerifierContainer(string name)
    {
        using var transport = new RecordingHandler(() => Success(HttpStatusCode.Created, []));
        using var http = new HttpClient(transport);
        var store = new BlobVerifierArtifactStore(Container("verifier", http), 1024, 1024);
        await store.CreateAsync(name, Encoding.UTF8.GetBytes("synthetic-identity-differs"), TestContext.Current.CancellationToken);
        Request request = Assert.Single(transport.Requests);
        Assert.Equal("PUT", request.Method);
        Assert.Equal("/verifier/" + name, request.Path);
        Assert.Equal("*", request.IfNoneMatch);
        Assert.Empty(request.Query);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1025)]
    public async Task Given_OutputAtByteBoundary_When_Creating_Then_RejectsOnlyOverLimitBeforeUpload(int length)
    {
        using var transport = new RecordingHandler(() => Success(HttpStatusCode.Created, []));
        using var http = new HttpClient(transport);
        var store = new BlobVerifierArtifactStore(Container("verifier", http), 1024, 1024);
        if (length > 1024)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => store.CreateAsync("result.json", new byte[length], TestContext.Current.CancellationToken));
            Assert.Empty(transport.Requests);
        }
        else
        {
            await store.CreateAsync("result.json", new byte[length], TestContext.Current.CancellationToken);
            Assert.Single(transport.Requests);
        }
    }

    [Theory]
    [InlineData(409, "BlobAlreadyExists")]
    [InlineData(403, "AuthorizationPermissionMismatch")]
    [InlineData(500, "InternalError")]
    [InlineData(503, "ServerBusy")]
    public async Task Given_OutputFailure_When_Creating_Then_PropagatesWithoutRetryOrDelete(int status, string code)
    {
        using var transport = new RecordingHandler(() => Error(status, code));
        using var http = new HttpClient(transport);
        var store = new BlobVerifierArtifactStore(Container("verifier", http), 1024, 1024);
        RequestFailedException error = await Assert.ThrowsAsync<RequestFailedException>(() =>
            store.CreateAsync("result.json", [1], TestContext.Current.CancellationToken));
        Assert.Equal(code, error.ErrorCode);
        Assert.Equal("PUT", Assert.Single(transport.Requests).Method);
    }

    [Theory]
    [InlineData(404, "BlobNotFound", true)]
    [InlineData(403, "BlobNotFound", false)]
    [InlineData(404, "UnknownError", false)]
    [InlineData(404, "ContainerNotFound", false)]
    [InlineData(403, "AuthorizationFailure", false)]
    [InlineData(500, "InternalError", false)]
    public async Task Given_ReadFailure_When_Reading_Then_OnlyMissingBlobReturnsNull(int status, string code, bool missing)
    {
        using var transport = new RecordingHandler(() => Error(status, code));
        using var http = new HttpClient(transport);
        var container = Container("verifier", http);
        var store = new BlobVerifierArtifactStore(container, 1024, 1024);
        if (missing) Assert.Null(await store.ReadStartLineAsync("notes/custom.json", TestContext.Current.CancellationToken));
        else await Assert.ThrowsAsync<RequestFailedException>(() => store.ReadStartLineAsync("notes/custom.json", TestContext.Current.CancellationToken));
        var source = new BlobLedgerArtifactSource(Container("ledger", http), "prefix", 1024);
        if (missing) Assert.Null(await source.ReadAsync("manifest.json", TestContext.Current.CancellationToken));
        else await Assert.ThrowsAsync<RequestFailedException>(() => source.ReadAsync("manifest.json", TestContext.Current.CancellationToken));
        Assert.Equal(2, transport.Requests.Count);
        Assert.All(transport.Requests, request => Assert.Equal("GET", request.Method));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1025)]
    public async Task Given_InputAtByteBoundary_When_Reading_Then_EnforcesLimitAndDisposesResponse(int length)
    {
        using var stream = new TrackingStream(new byte[length]);
        using var transport = new RecordingHandler(() => Success(HttpStatusCode.OK, stream));
        using var http = new HttpClient(transport);
        var source = new BlobLedgerArtifactSource(Container("ledger", http), "prefix", 1024);
        if (length > 1024)
            await Assert.ThrowsAsync<InvalidDataException>(() => source.ReadAsync("ledger.jsonl", TestContext.Current.CancellationToken));
        else Assert.Equal(length, (await source.ReadAsync("ledger.jsonl", TestContext.Current.CancellationToken))!.Length);
        Assert.True(stream.IsDisposed);
    }

    [Fact]
    public async Task Given_UnderstatedContentLength_When_Reading_Then_BoundsActualStreamAndDisposesIt()
    {
        using var stream = new TrackingStream(new byte[1025]);
        using var transport = new RecordingHandler(() =>
        {
            HttpResponseMessage response = Success(HttpStatusCode.OK, stream);
            response.Content.Headers.ContentLength = 1;
            return response;
        });
        using var http = new HttpClient(transport);
        var store = new BlobVerifierArtifactStore(Container("verifier", http), 1024, 1024);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadStartLineAsync("note.json", TestContext.Current.CancellationToken));
        Assert.True(stream.IsDisposed);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void Given_ConfiguredByteCeiling_When_Constructing_Then_RequiresPositiveLimits(long limit)
    {
        using var transport = new RecordingHandler(() => Success(HttpStatusCode.OK, []));
        using var http = new HttpClient(transport);
        BlobContainerClient container = Container("verifier", http);
        if (limit > 0)
        {
            Assert.NotNull(new BlobVerifierArtifactStore(container, limit, limit));
            Assert.NotNull(new BlobLedgerArtifactSource(container, "prefix", limit));
        }
        else
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BlobVerifierArtifactStore(container, limit, 1024));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BlobVerifierArtifactStore(container, 1024, limit));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BlobLedgerArtifactSource(container, "prefix", limit));
        }
    }

    [Fact]
    public async Task Given_CancelledToken_When_ReadingOrCreating_Then_MakesNoRequests()
    {
        using var transport = new RecordingHandler(() => Success(HttpStatusCode.OK, []));
        using var http = new HttpClient(transport);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var store = new BlobVerifierArtifactStore(Container("verifier", http), 1024, 1024);
        var source = new BlobLedgerArtifactSource(Container("ledger", http), "prefix", 1024);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.CreateAsync("result.json", [1], cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadStartLineAsync("note.json", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.ReadAsync("index.json", cancelled.Token));
        Assert.Empty(transport.Requests);
    }

    private static BlobContainerClient Container(string name, HttpClient http, TokenCredential? credential = null)
    {
        var options = new BlobClientOptions { Transport = new HttpClientTransport(http) };
        options.Retry.MaxRetries = 0;
        return new BlobContainerClient(new Uri("https://synthetic.example/" + name), credential ?? new SyntheticCredential(), options);
    }

    private static HttpResponseMessage Success(HttpStatusCode status, byte[] bytes) => Success(status, new MemoryStream(bytes));

    private static HttpResponseMessage Success(HttpStatusCode status, Stream stream)
    {
        var response = new HttpResponseMessage(status) { Content = new StreamContent(stream) };
        response.Headers.TryAddWithoutValidation("ETag", "\"synthetic\"");
        response.Headers.TryAddWithoutValidation("Last-Modified", "Mon, 19 Oct 2026 00:00:00 GMT");
        return response;
    }

    private static HttpResponseMessage Error(int status, string code)
    {
        // Pinned StorageRequestFailedDetailsParser selects XML parsing from Content-Type.
        var response = new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent($"<Error><Code>{code}</Code><Message>synthetic failure</Message></Error>",
                Encoding.UTF8, "application/xml")
        };
        response.Headers.TryAddWithoutValidation("x-ms-error-code", code);
        return response;
    }

    private sealed class SyntheticCredential : TokenCredential
    {
        public string[] Scopes { get; private set; } = [];
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Scopes = requestContext.Scopes;
            return new AccessToken("synthetic-token", DateTimeOffset.MaxValue);
        }
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed record Request(string Method, string Path, string Query, string? IfNoneMatch);

    private sealed class RecordingHandler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<Request> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(new Request(request.Method.Method, request.RequestUri!.AbsolutePath, request.RequestUri.Query,
                request.Headers.TryGetValues("If-None-Match", out IEnumerable<string>? values) ? string.Join(",", values) : null));
            return Task.FromResult(response());
        }
    }

    private sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public bool IsDisposed { get; private set; }
        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
#pragma warning restore CA1707

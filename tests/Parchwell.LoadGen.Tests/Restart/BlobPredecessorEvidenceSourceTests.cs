using System.Net;
using System.Text;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Parchwell.LoadGen.Run.Restart;
using Xunit;

namespace Parchwell.LoadGen.Tests.Restart;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class BlobPredecessorEvidenceSourceTests
{
    private const string Prefix = "run-1/C0/r1/producer/";

    [Fact]
    public async Task Given_TwoListingPages_When_Listing_Then_ReturnsEveryNameUnderThePrefix()
    {
        using var handler = new Handler(request => request.RequestUri!.Query.Contains("marker=m1", StringComparison.Ordinal)
            ? ListPage([Prefix + "pod-a/registration.json"], null) : ListPage([Prefix + "pod-a/completion.json"], "m1"));

        IReadOnlyList<string> names = await Source(handler).ListAsync(Prefix, TestContext.Current.CancellationToken);

        Assert.Equal([Prefix + "pod-a/completion.json", Prefix + "pod-a/registration.json"], names);
        Assert.All(handler.Requests, request => Assert.Contains("prefix=" + Prefix, Uri.UnescapeDataString(request.RequestUri!.Query), StringComparison.Ordinal));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public async Task Given_ListingAtAndAboveTheEntryLimit_When_Listing_Then_ReturnsOrRejectsIt(int count, bool accepted)
    {
        using var handler = new Handler(_ => ListPage([.. Enumerable.Range(0, count).Select(i => $"{Prefix}pod-a/{i:D3}")], null));
        BlobPredecessorEvidenceSource source = Source(handler);

        if (accepted) Assert.Equal(count, (await source.ListAsync(Prefix, TestContext.Current.CancellationToken)).Count);
        else Assert.Equal("predecessor_listing_incomplete", (await Assert.ThrowsAsync<InvalidDataException>(() =>
            source.ListAsync(Prefix, TestContext.Current.CancellationToken))).Message);
    }

    [Fact]
    public void Given_ContainerOtherThanLedger_When_Creating_Then_Rejects()
    {
        using var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new BlobServiceClient(new Uri("https://predecessor.blob.test/"), new Credential(),
            new BlobClientOptions { Transport = new HttpClientTransport(new HttpClient(handler, disposeHandler: false)) });

        Assert.StartsWith("predecessor_source_container_invalid", Assert.Throws<ArgumentException>(() =>
            new BlobPredecessorEvidenceSource(client, "verifier")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_ObjectWithinAndBeyondTheLimit_When_Reading_Then_ReturnsOrRejectsIt()
    {
        using var handler = new Handler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
            response.Headers.TryAddWithoutValidation("ETag", "\"synthetic\"");
            response.Headers.TryAddWithoutValidation("Last-Modified", "Mon, 19 Oct 2026 00:00:00 GMT");
            return response;
        });
        BlobPredecessorEvidenceSource source = Source(handler);

        Assert.Equal([1, 2, 3], await source.ReadAsync(Prefix + "pod-a/registration.json", 3, TestContext.Current.CancellationToken));
        Assert.Equal("predecessor_object_exceeds_byte_limit", (await Assert.ThrowsAsync<InvalidDataException>(() =>
            source.ReadAsync(Prefix + "pod-a/registration.json", 2, TestContext.Current.CancellationToken))).Message);
        Assert.Equal("/ledger/" + Prefix + "pod-a/registration.json", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    private static BlobPredecessorEvidenceSource Source(HttpMessageHandler handler) => new(new BlobServiceClient(
        new Uri("https://predecessor.blob.test/"), new Credential(),
        new BlobClientOptions { Transport = new HttpClientTransport(new HttpClient(handler, disposeHandler: false)), Retry = { MaxRetries = 0 } }),
        "ledger");

    // The listing response shape of tests/Parchwell.Verifier.Tests/Storage/BlobArtifactStoreTests.cs.
    private static HttpResponseMessage ListPage(string[] names, string? marker) => new(HttpStatusCode.OK)
    {
        Content = new StringContent("<?xml version=\"1.0\" encoding=\"utf-8\"?><EnumerationResults ServiceEndpoint=\"https://predecessor.blob.test/\" ContainerName=\"ledger\"><Blobs>" +
            string.Concat(names.Select(name => $"<Blob><Name>{name}</Name><Properties><Last-Modified>Mon, 19 Oct 2026 00:00:00 GMT</Last-Modified><Etag>\"synthetic\"</Etag><Content-Length>1</Content-Length><BlobType>BlockBlob</BlobType></Properties></Blob>")) +
            $"</Blobs><NextMarker>{marker}</NextMarker></EnumerationResults>", Encoding.UTF8, "application/xml")
    };

    private sealed class Credential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new("synthetic", DateTimeOffset.MaxValue);
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}

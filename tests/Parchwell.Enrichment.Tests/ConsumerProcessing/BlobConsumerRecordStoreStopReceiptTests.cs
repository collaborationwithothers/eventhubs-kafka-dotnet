using System.Net;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Parchwell.Enrichment.ConsumerProcessing;
using Xunit;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class BlobConsumerRecordStoreStopReceiptTests
{
    private const string Prefix = "run1/C0/r1/consumer/run1-c0-r1-consumer-subject-abcde/";

    [Fact]
    public async Task Given_stop_receipt_When_written_Then_it_is_one_create_only_blob_named_stop_receipt_json()
    {
        using var handler = new BlobHandler();
        var store = new BlobConsumerRecordStore(new BlobServiceClient(
            new Uri("https://synthetic.blob.test/"), new BlobClientOptions
            {
                Transport = new HttpClientTransport(new HttpClient(handler, disposeHandler: false)),
                Retry = { MaxRetries = 0 }
            }), Prefix);
        await store.WriteStopReceiptAsync("{}"u8.ToArray(), TestContext.Current.CancellationToken);
        Assert.Equal(["/consumer-record/" + Prefix + "stop-receipt.json"], handler.Uploads);
    }

    [Fact]
    public async Task Given_oversize_stop_receipt_When_written_Then_nothing_is_uploaded()
    {
        using var handler = new BlobHandler();
        var store = new BlobConsumerRecordStore(new BlobServiceClient(
            new Uri("https://synthetic.blob.test/"), new BlobClientOptions
            {
                Transport = new HttpClientTransport(new HttpClient(handler, disposeHandler: false)),
                Retry = { MaxRetries = 0 }
            }), Prefix);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            store.WriteStopReceiptAsync(new byte[65537], TestContext.Current.CancellationToken));
        Assert.Equal("consumer_record_too_large", error.Message);
        Assert.Empty(handler.Uploads);
    }

    private sealed class BlobHandler : HttpMessageHandler
    {
        public List<string> Uploads { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            // Create-only: the upload must carry If-None-Match: *.
            Assert.Equal("*", Assert.Single(request.Headers.GetValues("If-None-Match")));
            Uploads.Add(request.RequestUri!.AbsolutePath);
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.TryAddWithoutValidation("ETag", "\"synthetic\"");
            response.Headers.TryAddWithoutValidation("Last-Modified", "Sun, 15 Nov 2026 00:00:00 GMT");
            return Task.FromResult(response);
        }
    }
}

#pragma warning restore CA1707

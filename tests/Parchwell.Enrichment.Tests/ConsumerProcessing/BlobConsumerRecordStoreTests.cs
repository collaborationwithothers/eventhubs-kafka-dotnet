using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Parchwell.Enrichment.ConsumerProcessing;
using Xunit;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class BlobConsumerRecordStoreTests
{
    private const string Prefix = "run1/C0/r1/consumer/run1-c0-r1-consumer-subject-abcde/";

    [Fact, Trait("PlanTest", "U20")]
    public async Task Given_40001_rows_When_appended_Then_rollover_preserves_sequence_and_position()
    {
        using var handler = new BlobHandler();
        var store = Store(handler);
        for (int sequence = 0; sequence < 40001; sequence++)
            await store.AppendAsync(Encoding.UTF8.GetBytes("{\"row_sequence\":" +
                sequence.ToString(CultureInfo.InvariantCulture) + "}\n"), TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Created.Count);
        Assert.Equal("/consumer-record/" + Prefix + "record/000000.jsonl", handler.Created[0]);
        Assert.Equal("/consumer-record/" + Prefix + "record/000001.jsonl", handler.Created[1]);
        Assert.Equal(40000, handler.Appends.Count(row => row.Path.EndsWith("000000.jsonl", StringComparison.Ordinal)));
        Assert.Equal(1, handler.Appends.Count(row => row.Path.EndsWith("000001.jsonl", StringComparison.Ordinal)));
        long position = 0;
        for (int i = 0; i < handler.Appends.Count; i++)
        {
            var row = handler.Appends[i];
            if (i == 40000) position = 0;
            Assert.Equal(position, row.Position);
            Assert.Equal((byte)'\n', row.Bytes[^1]);
            using var json = JsonDocument.Parse(row.Bytes.AsMemory(0, row.Bytes.Length - 1));
            Assert.Equal(i, json.RootElement.GetProperty("row_sequence").GetInt32());
            position += row.Bytes.Length;
        }
    }

    [Theory, Trait("PlanTest", "U21")]
    [InlineData(65536, true)]
    [InlineData(65537, false)]
    public async Task Given_row_size_boundary_When_appended_Then_only_65536_bytes_is_accepted(int length, bool accepted)
    {
        using var handler = new BlobHandler();
        var store = Store(handler);
        byte[] row = Encoding.UTF8.GetBytes("{\"value\":\"" + new string('x', length - 13) + "\"}\n");
        Assert.Equal(length, row.Length);
        if (accepted)
        {
            await store.AppendAsync(row, TestContext.Current.CancellationToken);
            Assert.Equal(row, Assert.Single(handler.Appends).Bytes);
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => store.AppendAsync(row, TestContext.Current.CancellationToken));
            Assert.Equal("consumer_record_append_failed", error.Message);
            Assert.Empty(handler.Created);
            Assert.Empty(handler.Appends);
        }
    }

    [Fact]
    public async Task Given_lifecycle_markers_When_written_Then_both_are_create_only()
    {
        using var handler = new BlobHandler();
        var store = Store(handler);
        await store.RegisterAsync("{}"u8.ToArray(), TestContext.Current.CancellationToken);
        await store.CompleteAsync("{}"u8.ToArray(), TestContext.Current.CancellationToken);
        Assert.Equal(["/consumer-record/" + Prefix + "registration.json", "/consumer-record/" + Prefix + "completion.json"], handler.Uploads);
    }

    [Fact]
    public async Task Given_append_position_conflict_When_appending_Then_failure_is_not_retried()
    {
        using var handler = new BlobHandler { FailAppend = true };
        var store = Store(handler);
        var exception = await Assert.ThrowsAsync<RequestFailedException>(() =>
            store.AppendAsync("{}\n"u8.ToArray(), TestContext.Current.CancellationToken));
        Assert.Equal(412, exception.Status);
        Assert.Single(handler.Appends);
    }

    private static BlobConsumerRecordStore Store(BlobHandler handler) => new(new BlobServiceClient(
        new Uri("https://synthetic.blob.test/"), new BlobClientOptions
        {
            Transport = new HttpClientTransport(new HttpClient(handler, disposeHandler: false)),
            Retry = { MaxRetries = 0 }
        }), Prefix);

    private sealed class BlobHandler : HttpMessageHandler
    {
        public List<string> Created { get; } = [];
        public List<string> Uploads { get; } = [];
        public List<(string Path, long Position, byte[] Bytes)> Appends { get; } = [];
        public bool FailAppend { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            Assert.Equal(HttpMethod.Put, request.Method);
            if (request.RequestUri.Query.Contains("comp=appendblock", StringComparison.Ordinal))
            {
                long position = long.Parse(Assert.Single(request.Headers.GetValues("x-ms-blob-condition-appendpos")), CultureInfo.InvariantCulture);
                byte[] bytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
                Appends.Add((path, position, bytes));
                if (FailAppend)
                    return new HttpResponseMessage(HttpStatusCode.PreconditionFailed)
                    {
                        Content = new StringContent("<Error><Code>AppendPositionConditionNotMet</Code><Message>synthetic</Message></Error>", Encoding.UTF8, "application/xml")
                    };
            }
            else
            {
                Assert.Equal("*", Assert.Single(request.Headers.GetValues("If-None-Match")));
                if (request.Headers.GetValues("x-ms-blob-type").Single() == "AppendBlob") Created.Add(path);
                else Uploads.Add(path);
            }
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.TryAddWithoutValidation("ETag", "\"synthetic\"");
            response.Headers.TryAddWithoutValidation("Last-Modified", "Sun, 15 Nov 2026 00:00:00 GMT");
            return response;
        }
    }
}

#pragma warning restore CA1707

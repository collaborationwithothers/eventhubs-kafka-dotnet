using System.Text.Json;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Window;
using Xunit;

namespace Parchwell.Verifier.Tests.Window;

#pragma warning disable CA1707
public sealed class ConsumedMetadataCaptureTests
{
    private static RunWindow Window() => new([], new Dictionary<int, long> { [0] = 2 }, [new(0, 0, 2)], [], true);

    [Fact]
    public void ConsumedMetadata_RawKeyAndOrderedHeadersRoundTrip()
    {
        var options = VerifierHostTestOptions.ValidOptions("verify");
        var store = new Store();
        var capture = ConsumedMetadataCapture.TryStart(options, store, Window(), 0, "ordinary", 1)!;
        var record = new ConsumeResult<byte[], byte[]>
        {
            Topic = "document-events", Partition = new Partition(0), Offset = new Offset(1),
            Message = new Message<byte[], byte[]> { Key = [255], Value = [], Timestamp = new Timestamp(123, TimestampType.LogAppendTime),
                Headers = new Headers { { "unknown", null }, { "unknown", [] }, { "unicode-é", [0xff] } } }
        };
        capture.Observe(record); capture.Finish(true);
        string prefix = PhasePaths.ConsumedCapturePrefix("run-1", "C0", 1,
            PhasePaths.ConsumedCaptureId("verify", options.JobName!, options.PodUid!, 0));
        using var row = JsonDocument.Parse(store.Objects[prefix + "chunk-000001.jsonl"]);
        var root = row.RootElement;
        Assert.Equal("/w==", root.GetProperty("key_base64").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("key").ValueKind);
        Assert.Equal("log_append_time", root.GetProperty("timestamp").GetProperty("type").GetString());
        var headers = root.GetProperty("headers");
        Assert.Equal(3, headers.GetArrayLength());
        Assert.Equal(JsonValueKind.Null, headers[0].GetProperty("value_base64").ValueKind);
        Assert.Equal("", headers[1].GetProperty("value_base64").GetString());
        Assert.Equal("unicode-é", headers[2].GetProperty("name").GetString());
        Assert.Equal("/w==", headers[2].GetProperty("value_base64").GetString());
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", root.GetProperty("payload").GetProperty("sha256").GetString());
        Assert.False(root.TryGetProperty("value", out _));
        using var manifest = JsonDocument.Parse(store.Objects[prefix + "capture.json"]);
        Assert.True(manifest.RootElement.GetProperty("complete").GetBoolean());
    }

    [Theory]
    [InlineData(null, null, TimestampType.NotAvailable)]
    [InlineData("", "", TimestampType.CreateTime)]
    [InlineData("AAEC", "AAEC", TimestampType.LogAppendTime)]
    public void ConsumedMetadata_NullEmptyAndBinaryValuesRemainDistinct(string? keyBase64, string? valueBase64, TimestampType timestampType)
    {
        var options = VerifierHostTestOptions.ValidOptions("verify"); var store = new Store();
        byte[]? key = keyBase64 is null ? null : Convert.FromBase64String(keyBase64);
        byte[]? value = valueBase64 is null ? null : Convert.FromBase64String(valueBase64);
        var headers = new Headers { { "attempt-id", null }, { "attempt-id", [] }, { "extra", value } };
        var record = new ConsumeResult<byte[], byte[]> { Topic = "document-events", Partition = new Partition(0), Offset = new Offset(0),
            Message = new Message<byte[], byte[]> { Key = key!, Value = value!, Headers = headers, Timestamp = new Timestamp(123, timestampType) } };
        var capture = ConsumedMetadataCapture.TryStart(options, store, Window(), 0, "ordinary", 1)!;
        capture.Observe(record); capture.Finish(true);
        using var doc = JsonDocument.Parse(store.Objects.Single(p => p.Key.EndsWith(".jsonl", StringComparison.Ordinal)).Value);
        var row = doc.RootElement; Assert.Equal(keyBase64, row.GetProperty("key_base64").GetString());
        Assert.Equal(valueBase64, row.GetProperty("headers")[2].GetProperty("value_base64").GetString());
        Assert.Equal(value is null, row.GetProperty("payload").GetProperty("is_null").GetBoolean());
        var payload = row.GetProperty("payload");
        if (value is null)
        {
            Assert.Equal(JsonValueKind.Null, payload.GetProperty("length").ValueKind);
            Assert.Equal(JsonValueKind.Null, payload.GetProperty("sha256").ValueKind);
        }
        else
        {
            Assert.Equal(value.Length, payload.GetProperty("length").GetInt64());
            Assert.Equal(value.Length == 0 ? "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855" :
                "ae4b3280e56e2faf83f414a6e3dabe9d5fbe18976544c05fed121accb85b53fc", payload.GetProperty("sha256").GetString());
        }
        Assert.Equal(timestampType == TimestampType.NotAvailable ? JsonValueKind.Null : JsonValueKind.Number,
            row.GetProperty("timestamp").GetProperty("unix_milliseconds").ValueKind);
        Assert.Equal(timestampType switch { TimestampType.CreateTime => "create_time", TimestampType.LogAppendTime => "log_append_time", _ => "not_available" },
            row.GetProperty("timestamp").GetProperty("type").GetString());
        Assert.Contains(row.GetProperty("identity_problems").EnumerateArray(), problem => problem.GetProperty("field").GetString() == "attempt-id" && problem.GetProperty("code").GetString() == "duplicate_header");
    }

    [Fact]
    public void ConsumedMetadata_HugeKeyRejectedBeforeSerializationAndOverflowIsSticky()
    {
        var options = VerifierHostTestOptions.ValidOptions("verify"); options.ConsumedMetadata!.MaxRowBytes = 100;
        var store = new Store(); var capture = ConsumedMetadataCapture.TryStart(options, store, Window(), 0, "ordinary", 1)!;
        capture.Observe(new ConsumeResult<byte[], byte[]> { Topic = "document-events", Partition = new Partition(0), Offset = new Offset(0),
            Message = new Message<byte[], byte[]> { Key = new byte[1024 * 1024], Value = [] } });
        capture.Finish(true);
        using var doc = JsonDocument.Parse(store.Objects.Single(p => p.Key.EndsWith("capture.json", StringComparison.Ordinal)).Value);
        Assert.Equal("row_limit", doc.RootElement.GetProperty("gaps")[0].GetProperty("code").GetString());
        Assert.Equal(0, doc.RootElement.GetProperty("first_missing_ordinal").GetInt64());
        Assert.Empty(doc.RootElement.GetProperty("chunks").EnumerateArray());
        var overflowStore = new Store(); var overflow = ConsumedMetadataCapture.TryStart(options, overflowStore, Window(), 1, "ordinary", 1)!;
        typeof(ConsumedMetadataCapture).GetField("_observed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(overflow, long.MaxValue);
        overflow.Observe(new ConsumeResult<byte[], byte[]>()); overflow.Finish(false);
        using var overflowDoc = JsonDocument.Parse(overflowStore.Objects.Single(p => p.Key.EndsWith("capture.json", StringComparison.Ordinal)).Value);
        Assert.Equal("byte_limit", overflowDoc.RootElement.GetProperty("gaps")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void ConsumedMetadata_JsonEscapingAndDescriptorCountAreBounded()
    {
        var options = VerifierHostTestOptions.ValidOptions("verify"); options.ConsumedMetadata!.MaxRowBytes = 1024;
        var store = new Store(); var capture = ConsumedMetadataCapture.TryStart(options, store, Window(), 0, "ordinary", 1)!;
        capture.Observe(new ConsumeResult<byte[], byte[]> { Topic = "document-events", Partition = new Partition(0), Offset = new Offset(0),
            Message = new Message<byte[], byte[]> { Key = [], Value = [], Headers = new Headers { { new string('"', 1024), [] } } } });
        capture.Finish(true);
        using var failed = JsonDocument.Parse(store.Objects.Single(p => p.Key.EndsWith("capture.json", StringComparison.Ordinal)).Value);
        Assert.Equal("row_limit", failed.RootElement.GetProperty("gaps")[0].GetProperty("code").GetString());

        options = VerifierHostTestOptions.ValidOptions("verify");
        var sampleStore = new Store(); var record = new ConsumeResult<byte[], byte[]> { Topic = "document-events", Partition = new Partition(0), Offset = new Offset(0),
            Message = new Message<byte[], byte[]> { Key = [], Value = [] } };
        var sample = ConsumedMetadataCapture.TryStart(options, sampleStore, Window(), 0, "ordinary", 1)!;
        sample.Observe(record); sample.Finish(true);
        long size = sampleStore.Objects.Single(p => p.Key.EndsWith(".jsonl", StringComparison.Ordinal)).Value.LongLength;
        // Ordinals through 1021 add up to three bytes relative to ordinal zero.
        options.ConsumedMetadata!.MaxRowBytes = size + 3; options.ConsumedMetadata.MaxChunkBytes = size + 3;
        options.ConsumedMetadata.MaxBytes = (size + 3) * 1022;
        var boundedStore = new Store(); var bounded = ConsumedMetadataCapture.TryStart(options, boundedStore, Window(), 0, "ordinary", 1)!;
        for (int i = 0; i < 1022; i++) bounded.Observe(record);
        bounded.Finish(true);
        using var manifest = JsonDocument.Parse(boundedStore.Objects.Single(p => p.Key.EndsWith("capture.json", StringComparison.Ordinal)).Value);
        Assert.Equal(1021, manifest.RootElement.GetProperty("chunks").GetArrayLength());
        Assert.Equal(1021, manifest.RootElement.GetProperty("first_missing_ordinal").GetInt64());
        Assert.Equal("byte_limit", manifest.RootElement.GetProperty("gaps")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void ConsumedMetadata_ZeroAndPartialWindowsRemainDistinct()
    {
        var options = VerifierHostTestOptions.ValidOptions("verify"); var store = new Store();
        ConsumedMetadataCapture.TryStart(options, store, Window(), 0, "ordinary", 1)!.Finish(true);
        ConsumedMetadataCapture.TryStart(options, store, Window(), 1, "ordinary", 1)!.Finish(false);
        var captures = store.Objects.Where(p => p.Key.EndsWith("capture.json", StringComparison.Ordinal))
            .OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => JsonDocument.Parse(p.Value)).ToArray();
        using (captures[0]) using (captures[1])
        {
            Assert.True(captures[0].RootElement.GetProperty("complete").GetBoolean());
            Assert.Empty(captures[0].RootElement.GetProperty("chunks").EnumerateArray());
            Assert.False(captures[1].RootElement.GetProperty("complete").GetBoolean());
            Assert.Equal("source_window_incomplete", captures[1].RootElement.GetProperty("gaps")[0].GetProperty("code").GetString());
        }
    }

    [Fact]
    public void ConsumedMetadata_RowChunkAndTotalBounds()
    {
        var options = VerifierHostTestOptions.ValidOptions("verify"); var probe = new Store();
        var record = new ConsumeResult<byte[], byte[]> { Topic = "document-events", Partition = new Partition(0), Offset = new Offset(0),
            Message = new Message<byte[], byte[]> { Key = [], Value = null!, Headers = new Headers() } };
        var sample = ConsumedMetadataCapture.TryStart(options, probe, Window(), 0, "ordinary", 1)!;
        sample.Observe(record); sample.Finish(true);
        long size = probe.Objects.Single(p => p.Key.EndsWith(".jsonl", StringComparison.Ordinal)).Value.LongLength;
        foreach (long delta in new long[] { -1, 0, 1 })
        {
            options.ConsumedMetadata!.MaxRowBytes = size + delta;
            options.ConsumedMetadata.MaxChunkBytes = size * 2;
            options.ConsumedMetadata.MaxBytes = size * 2;
            var store = new Store(); var capture = ConsumedMetadataCapture.TryStart(options, store, Window(), 0, "ordinary", 1)!;
            capture.Observe(record); capture.Finish(true);
            using var manifest = JsonDocument.Parse(store.Objects.Single(p => p.Key.EndsWith("capture.json", StringComparison.Ordinal)).Value);
            Assert.Equal(delta >= 0, manifest.RootElement.GetProperty("complete").GetBoolean());
            Assert.Equal(delta >= 0 ? 1 : 0, manifest.RootElement.GetProperty("captured_rows").GetInt64());
        }
    }

    [Fact]
    public void ConsumedMetadata_ChunkRotationAndAggregateLimit()
    {
        var options = VerifierHostTestOptions.ValidOptions("verify");
        var record = new ConsumeResult<byte[], byte[]> { Topic = "document-events", Partition = new Partition(0), Offset = new Offset(0),
            Message = new Message<byte[], byte[]> { Key = [], Value = [] } };
        var sampleStore = new Store(); var sample = ConsumedMetadataCapture.TryStart(options, sampleStore, Window(), 0, "ordinary", 1)!;
        sample.Observe(record); sample.Finish(true);
        long size = sampleStore.Objects.Single(p => p.Key.EndsWith(".jsonl", StringComparison.Ordinal)).Value.LongLength;
        options.ConsumedMetadata!.MaxRowBytes = size; options.ConsumedMetadata.MaxChunkBytes = size; options.ConsumedMetadata.MaxBytes = size * 2;
        var store = new Store(); var capture = ConsumedMetadataCapture.TryStart(options, store, Window(), 0, "ordinary", 1)!;
        capture.Observe(record); capture.Observe(record); capture.Finish(true);
        using var manifest = JsonDocument.Parse(store.Objects.Single(p => p.Key.EndsWith("capture.json", StringComparison.Ordinal)).Value);
        Assert.True(manifest.RootElement.GetProperty("complete").GetBoolean());
        Assert.Equal(2, manifest.RootElement.GetProperty("chunks").GetArrayLength());
        Assert.All(manifest.RootElement.GetProperty("chunks").EnumerateArray(), chunk => Assert.Equal(size, chunk.GetProperty("bytes").GetInt64()));
        options.ConsumedMetadata.MaxBytes = size * 2 - 1;
        var limitedStore = new Store(); var limited = ConsumedMetadataCapture.TryStart(options, limitedStore, Window(), 0, "ordinary", 1)!;
        limited.Observe(record); limited.Observe(record); limited.Finish(true);
        using var incomplete = JsonDocument.Parse(limitedStore.Objects.Single(p => p.Key.EndsWith("capture.json", StringComparison.Ordinal)).Value);
        Assert.False(incomplete.RootElement.GetProperty("complete").GetBoolean());
        Assert.Equal(1, incomplete.RootElement.GetProperty("first_missing_ordinal").GetInt64());
        Assert.Equal("byte_limit", incomplete.RootElement.GetProperty("gaps")[0].GetProperty("code").GetString());
        options.ConsumedMetadata.MaxBytes = size * 2 + 1;
        var aboveStore = new Store(); var above = ConsumedMetadataCapture.TryStart(options, aboveStore, Window(), 0, "ordinary", 1)!;
        above.Observe(record); above.Observe(record); above.Finish(true);
        using var aboveManifest = JsonDocument.Parse(aboveStore.Objects.Single(p => p.Key.EndsWith("capture.json", StringComparison.Ordinal)).Value);
        Assert.True(aboveManifest.RootElement.GetProperty("complete").GetBoolean());
        foreach (long delta in new long[] { -1, 0, 1 })
        {
            options.ConsumedMetadata.MaxChunkBytes = size + delta; options.ConsumedMetadata.MaxBytes = size * 3;
            var boundaryStore = new Store(); var boundary = ConsumedMetadataCapture.TryStart(options, boundaryStore, Window(), 0, "ordinary", 1)!;
            boundary.Observe(record); boundary.Finish(true);
            using var boundaryManifest = JsonDocument.Parse(boundaryStore.Objects.Single(p => p.Key.EndsWith("capture.json", StringComparison.Ordinal)).Value);
            Assert.Equal(delta >= 0, boundaryManifest.RootElement.GetProperty("complete").GetBoolean());
        }
    }

    [Theory]
    [InlineData(-1, 2)] [InlineData(0, 1)] [InlineData(1, 1)]
    public void ConsumedMetadata_ValidChunkRotationAroundTwoWholeRows(int delta, int expectedChunks)
    {
        var options = VerifierHostTestOptions.ValidOptions("verify");
        var record = new ConsumeResult<byte[], byte[]> { Topic = "document-events", Partition = new Partition(0), Offset = new Offset(0),
            Message = new Message<byte[], byte[]> { Key = [], Value = [] } };
        var probeStore = new Store(); var probe = ConsumedMetadataCapture.TryStart(options, probeStore, Window(), 0, "ordinary", 1)!;
        probe.Observe(record); probe.Observe(record); probe.Finish(true);
        byte[] bothRows = probeStore.Objects.Single(pair => pair.Key.EndsWith(".jsonl", StringComparison.Ordinal)).Value;
        long rowBytes = bothRows.LongLength / 2;
        Assert.Equal(rowBytes * 2, bothRows.LongLength);
        Assert.Equal((byte)'\n', bothRows[(int)rowBytes - 1]); Assert.Equal((byte)'\n', bothRows[^1]);
        options.ConsumedMetadata!.MaxRowBytes = rowBytes;
        options.ConsumedMetadata.MaxChunkBytes = bothRows.LongLength + delta;
        options.ConsumedMetadata.MaxBytes = bothRows.LongLength + 1;
        Assert.True(options.ConsumedMetadata.MaxRowBytes <= options.ConsumedMetadata.MaxChunkBytes);
        Assert.True(options.ConsumedMetadata.MaxChunkBytes <= options.ConsumedMetadata.MaxBytes);
        var store = new Store(); var capture = ConsumedMetadataCapture.TryStart(options, store, Window(), 0, "ordinary", 1)!;
        capture.Observe(record); capture.Observe(record); capture.Finish(true);
        using var manifest = JsonDocument.Parse(store.Objects.Single(pair => pair.Key.EndsWith("capture.json", StringComparison.Ordinal)).Value);
        Assert.True(manifest.RootElement.GetProperty("complete").GetBoolean());
        Assert.Equal(expectedChunks, manifest.RootElement.GetProperty("chunks").GetArrayLength());
        Assert.Equal(2, manifest.RootElement.GetProperty("captured_rows").GetInt64());
        Assert.Equal(bothRows.LongLength, manifest.RootElement.GetProperty("chunks").EnumerateArray().Sum(chunk => chunk.GetProperty("bytes").GetInt64()));
    }

    [Fact]
    public void ConsumedMetadata_LegacySweepWindowsHaveIndependentCaptures()
    {
        var options = VerifierHostTestOptions.ValidOptions("b10-sweep"); options.JobName = "run-1-c0-b10-sweep";
        var store = new Store();
        for (int index = 0; index < 10; index++)
            ConsumedMetadataCapture.TryStart(options, store, Window(), index, index % 2 == 0 ? "original" : "late", index / 2 + 1)!.Finish(index != 7);
        Assert.Equal(10, store.Objects.Count);
        foreach (var (name, bytes) in store.Objects)
        {
            using var capture = JsonDocument.Parse(bytes); var window = capture.RootElement.GetProperty("window");
            int index = window.GetProperty("index").GetInt32();
            Assert.Contains($"-w{index:D4}/capture.json", name, StringComparison.Ordinal);
            Assert.Equal(index / 2 + 1, window.GetProperty("repeat").GetInt32());
            Assert.Equal(index % 2 == 0 ? "original" : "late", window.GetProperty("kind").GetString());
            Assert.Equal(index != 7, capture.RootElement.GetProperty("complete").GetBoolean());
        }
    }

    [Fact]
    public void ConsumedMetadata_PartialDrainRetainsSealedChunks()
    {
        var options = VerifierHostTestOptions.ValidOptions("verify"); var store = new Store { FailStream = true };
        var capture = ConsumedMetadataCapture.TryStart(options, store, Window(), 0, "ordinary", 1)!;
        capture.Observe(new ConsumeResult<byte[], byte[]> { Topic = "document-events", Partition = new Partition(0), Offset = new Offset(0), Message = new Message<byte[], byte[]> { Key = [], Value = [] } });
        capture.Finish(true);
        using var manifest = JsonDocument.Parse(store.Objects.Single(p => p.Key.EndsWith("capture.json", StringComparison.Ordinal)).Value);
        Assert.False(manifest.RootElement.GetProperty("complete").GetBoolean());
        Assert.Equal(0, manifest.RootElement.GetProperty("first_missing_ordinal").GetInt64());
        Assert.Equal("upload_failed", manifest.RootElement.GetProperty("gaps")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void ConsumedMetadata_WriterFailureSealsEarlierRowsAndRecordsGap()
    {
        var options = VerifierHostTestOptions.ValidOptions("verify"); var store = new Store(); var chunk = new FaultingStream();
        var capture = ConsumedMetadataCapture.TryStart(options, store, Window(), 0, "ordinary", 1, () => chunk)!;
        var record = new ConsumeResult<byte[], byte[]> { Topic = "document-events", Partition = new Partition(0), Offset = new Offset(0),
            Message = new Message<byte[], byte[]> { Key = [], Value = [] } };
        capture.Observe(record); chunk.FailWrites = true; capture.Observe(record); capture.Finish(true);
        using var manifest = JsonDocument.Parse(store.Objects.Single(p => p.Key.EndsWith("capture.json", StringComparison.Ordinal)).Value);
        Assert.False(manifest.RootElement.GetProperty("complete").GetBoolean());
        Assert.Equal(2, manifest.RootElement.GetProperty("observed_rows").GetInt64());
        Assert.Equal(1, manifest.RootElement.GetProperty("captured_rows").GetInt64());
        Assert.Equal(1, manifest.RootElement.GetProperty("first_missing_ordinal").GetInt64());
        Assert.Equal("write_failed", manifest.RootElement.GetProperty("gaps")[0].GetProperty("code").GetString());
        Assert.Single(manifest.RootElement.GetProperty("chunks").EnumerateArray());
        Assert.Single(store.Objects, p => p.Key.EndsWith(".jsonl", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, "upload_failed", 0L)]
    [InlineData(true, "write_failed", 1L)]
    public void ConsumedMetadata_SealAndCleanupFailuresRemainCaptureGaps(bool failDispose, string expectedCode, long missing)
    {
        var options = VerifierHostTestOptions.ValidOptions("verify"); var store = new Store();
        var capture = ConsumedMetadataCapture.TryStart(options, store, Window(), 0, "ordinary", 1, () => new FinalizeFaultStream(failDispose))!;
        capture.Observe(new ConsumeResult<byte[], byte[]> { Topic = "document-events", Partition = new Partition(0), Offset = new Offset(0),
            Message = new Message<byte[], byte[]> { Key = [], Value = [] } });
        capture.Finish(true);
        using var manifest = JsonDocument.Parse(store.Objects.Single(pair => pair.Key.EndsWith("capture.json", StringComparison.Ordinal)).Value);
        Assert.False(manifest.RootElement.GetProperty("complete").GetBoolean());
        Assert.Equal(expectedCode, manifest.RootElement.GetProperty("gaps")[0].GetProperty("code").GetString());
        Assert.Equal(missing, manifest.RootElement.GetProperty("first_missing_ordinal").GetInt64());
        Assert.Equal(failDispose ? 1 : 0, manifest.RootElement.GetProperty("captured_rows").GetInt64());
    }

    private sealed class FinalizeFaultStream(bool failDispose) : MemoryStream
    {
        public override void Flush() { if (!failDispose) throw new IOException("synthetic flush failure"); base.Flush(); }
        protected override void Dispose(bool disposing) { base.Dispose(disposing); if (failDispose) throw new IOException("synthetic disposal failure"); }
    }

    private sealed class FaultingStream : MemoryStream
    {
        internal bool FailWrites { get; set; }
        public override void Write(byte[] buffer, int offset, int count)
        { if (FailWrites) throw new IOException("synthetic disk failure"); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer)
        { if (FailWrites) throw new IOException("synthetic disk failure"); base.Write(buffer); }
        public override void WriteByte(byte value)
        { if (FailWrites) throw new IOException("synthetic disk failure"); base.WriteByte(value); }
    }

    [Fact]
    public void PhasePaths_ConsumedCaptureNamesAreConfined()
    {
        Assert.Throws<ArgumentException>(() => PhasePaths.ConsumedCaptureId("verify", "../job", "pod", 0));
        Assert.Throws<ArgumentException>(() => PhasePaths.ConsumedCapturePrefix("run-1", "C0", 1, "../capture"));
        Assert.NotEqual(PhasePaths.ConsumedCaptureId("b10-sweep", "job", "pod", 0), PhasePaths.ConsumedCaptureId("b10-sweep", "job", "pod", 1));
        Assert.Throws<ArgumentException>(() => PhasePaths.ConsumedCaptureId("verify", new string('a', 129), "pod", 0));
        Assert.Throws<ArgumentException>(() => PhasePaths.ConsumedCaptureId("verify", "job", "pod", -1));
        Assert.Throws<ArgumentException>(() => PhasePaths.ConsumedCaptureId("judge", "job", "pod", 0));
        Assert.Throws<ArgumentException>(() => PhasePaths.ConsumedCapturePrefix("run-1", "C0", 1, new string('a', 1025)));
        Assert.Equal("run-1/C0/r1/consumed-metadata/verify-run-1-c0-r1-verify-verifier-uid-w0000/",
            PhasePaths.ConsumedCapturePrefix("run-1", "C0", 1, PhasePaths.ConsumedCaptureId("verify", "run-1-c0-r1-verify", "verifier-uid", 0)));
        foreach (string mode in new[] { "verify", "b10-abrupt-verify", "b10-sweep", "b10-final-sweep" })
            Assert.EndsWith("-w10000", PhasePaths.ConsumedCaptureId(mode, "job", "pod", 10000), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(99, true)]
    [InlineData(100, false)]
    [InlineData(101, false)]
    public void Given_CaptureClockAdvancesDuringChunkUpload_When_Sealing_Then_TimeoutBoundaryApplies(int advance, bool complete)
    {
        var clock = new ManualKafkaTimeProvider();
        var options = VerifierHostTestOptions.ValidOptions("verify"); options.BlobIoTimeout = TimeSpan.FromMilliseconds(100);
        var store = new DelegateStore(clock, advance, name => name.EndsWith(".jsonl", StringComparison.Ordinal));
        var capture = ConsumedMetadataCapture.TryStart(options, store, Window(), 0, "ordinary", 1)!;
        capture.UseClock(clock);
        capture.Observe(OneRecord()); capture.Finish(true);
        using var manifest = JsonDocument.Parse(store.Objects.Single(p => p.Key.EndsWith("capture.json", StringComparison.Ordinal)).Value);
        var root = manifest.RootElement;
        Assert.Equal(complete, root.GetProperty("complete").GetBoolean());
        Assert.Equal(1, root.GetProperty("observed_rows").GetInt64());
        if (complete)
        {
            Assert.Equal(0, root.GetProperty("gaps").GetArrayLength());
            Assert.Equal(1, root.GetProperty("captured_rows").GetInt64());
            return;
        }
        Assert.Equal(0, root.GetProperty("captured_rows").GetInt64());
        Assert.Equal(0, root.GetProperty("first_missing_ordinal").GetInt64());
        var gap = Assert.Single(root.GetProperty("gaps").EnumerateArray());
        Assert.Equal("upload_failed", gap.GetProperty("code").GetString());
        Assert.Equal(0, gap.GetProperty("ordinal").GetInt64());
    }

    [Theory]
    [InlineData(99, true)]
    [InlineData(100, false)]
    [InlineData(101, false)]
    public void Given_CaptureClockAdvancesDuringRolloverUpload_When_Observing_Then_TimeoutBoundaryApplies(int advance, bool complete)
    {
        var record = OneRecord();
        var options = VerifierHostTestOptions.ValidOptions("verify");
        var sampleStore = new Store(); var sample = ConsumedMetadataCapture.TryStart(options, sampleStore, Window(), 0, "ordinary", 1)!;
        sample.Observe(record); sample.Finish(true);
        long size = sampleStore.Objects.Single(p => p.Key.EndsWith(".jsonl", StringComparison.Ordinal)).Value.LongLength;
        options.ConsumedMetadata!.MaxRowBytes = size; options.ConsumedMetadata.MaxChunkBytes = size; options.ConsumedMetadata.MaxBytes = size * 2;
        options.BlobIoTimeout = TimeSpan.FromMilliseconds(100);
        var clock = new ManualKafkaTimeProvider();
        var store = new DelegateStore(clock, advance, name => name.EndsWith("chunk-000001.jsonl", StringComparison.Ordinal));
        var capture = ConsumedMetadataCapture.TryStart(options, store, Window(), 0, "ordinary", 1)!;
        capture.UseClock(clock);
        capture.Observe(record); capture.Observe(record); capture.Finish(true);
        using var manifest = JsonDocument.Parse(store.Objects.Single(p => p.Key.EndsWith("capture.json", StringComparison.Ordinal)).Value);
        var root = manifest.RootElement;
        Assert.Equal(complete, root.GetProperty("complete").GetBoolean());
        Assert.Equal(2, root.GetProperty("observed_rows").GetInt64());
        if (complete)
        {
            Assert.Equal(2, root.GetProperty("chunks").GetArrayLength());
            Assert.Equal(2, root.GetProperty("captured_rows").GetInt64());
            return;
        }
        Assert.Equal(0, root.GetProperty("captured_rows").GetInt64());
        Assert.Equal(0, root.GetProperty("first_missing_ordinal").GetInt64());
        var gap = Assert.Single(root.GetProperty("gaps").EnumerateArray());
        Assert.Equal("upload_failed", gap.GetProperty("code").GetString());
        Assert.Equal(0, gap.GetProperty("ordinal").GetInt64());
    }

    [Theory]
    [InlineData(99, true)]
    [InlineData(100, false)]
    [InlineData(101, false)]
    public void Given_CaptureClockAdvancesDuringManifestWrite_When_Finishing_Then_TimeoutBoundaryApplies(int advance, bool written)
    {
        var clock = new ManualKafkaTimeProvider();
        var options = VerifierHostTestOptions.ValidOptions("verify"); options.BlobIoTimeout = TimeSpan.FromMilliseconds(100);
        var store = new DelegateStore(clock, advance, name => name.EndsWith("capture.json", StringComparison.Ordinal));
        var capture = ConsumedMetadataCapture.TryStart(options, store, Window(), 0, "ordinary", 1)!;
        capture.UseClock(clock);
        capture.Finish(true);
        var manifests = store.Objects.Where(p => p.Key.EndsWith("capture.json", StringComparison.Ordinal)).ToArray();
        if (!written) { Assert.Empty(manifests); return; }
        using var manifest = JsonDocument.Parse(Assert.Single(manifests).Value);
        Assert.True(manifest.RootElement.GetProperty("complete").GetBoolean());
    }

    [Fact]
    public void Given_NoClock_When_StoreSlowerThanBlobTimeout_Then_UploadFails()
    {
        var options = VerifierHostTestOptions.ValidOptions("verify"); options.BlobIoTimeout = TimeSpan.FromMilliseconds(10);
        var store = new DelegateStore(null, 0, _ => false) { WaitForToken = true };
        var capture = ConsumedMetadataCapture.TryStart(options, store, Window(), 0, "ordinary", 1)!;
        capture.Observe(OneRecord()); capture.Finish(true);
        using var manifest = JsonDocument.Parse(store.Objects.Single(p => p.Key.EndsWith("capture.json", StringComparison.Ordinal)).Value);
        var root = manifest.RootElement;
        Assert.False(root.GetProperty("complete").GetBoolean());
        Assert.Equal("upload_failed", root.GetProperty("gaps")[0].GetProperty("code").GetString());
        Assert.Equal(0, root.GetProperty("captured_rows").GetInt64());
    }

    private static ConsumeResult<byte[], byte[]> OneRecord() => new()
    {
        Topic = "document-events", Partition = new Partition(0), Offset = new Offset(0),
        Message = new Message<byte[], byte[]> { Key = [], Value = [] }
    };

    /// <summary>Store whose matching writes advance a manual clock (or wait for the token), then honour cancellation.</summary>
    internal sealed class DelegateStore(ManualKafkaTimeProvider? clock, int advanceMilliseconds, Func<string, bool> advancesFor) : IVerifierArtifactStore
    {
        private bool advanced;
        internal bool WaitForToken { get; init; }
        internal Dictionary<string, byte[]> Objects { get; } = new(StringComparer.Ordinal);
        public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) => Task.FromResult(Objects.GetValueOrDefault(name));
        public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) => Task.FromResult(new BlobListing([], 0, true));
        public Task CreateAsync(string name, byte[] content, CancellationToken cancellationToken)
        { Delay(name, cancellationToken); Objects.Add(name, content); return Task.CompletedTask; }
        public async Task CreateStreamAsync(string name, Stream content, CancellationToken cancellationToken)
        {
            Delay(name, cancellationToken);
            using var buffer = new MemoryStream(); await content.CopyToAsync(buffer, cancellationToken); Objects.Add(name, buffer.ToArray());
        }
        private void Delay(string name, CancellationToken cancellationToken)
        {
            if (WaitForToken && name.EndsWith(".jsonl", StringComparison.Ordinal)) cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(30));
            else if (!advanced && clock is not null && advancesFor(name)) { advanced = true; clock.Advance(advanceMilliseconds); }
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    internal sealed class Store : IVerifierArtifactStore
    {
        internal bool FailStream { get; init; }
        internal Dictionary<string, byte[]> Objects { get; } = new(StringComparer.Ordinal);
        public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) => Task.FromResult(Objects.GetValueOrDefault(name));
        public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) => Task.FromResult(new BlobListing([], 0, true));
        public Task CreateAsync(string name, byte[] content, CancellationToken cancellationToken) { Objects.Add(name, content); return Task.CompletedTask; }
        public async Task CreateStreamAsync(string name, Stream content, CancellationToken cancellationToken)
        { if (FailStream) throw new IOException("synthetic upload failure"); using var buffer = new MemoryStream(); await content.CopyToAsync(buffer, cancellationToken); Objects.Add(name, buffer.ToArray()); }
    }
}

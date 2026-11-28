// This file sits under Component/ only so the default mutation-only build drops it (the csproj removes Component/**/*.cs).
// The tests still run in the normal build and in the full-suite mutation build. Why: docs/operations/verifier-mutation.md, section "Issue 602 fast-suite equivalence check".
using System.Text.Json;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun.Window;
using Xunit;

namespace Parchwell.Verifier.Tests.Window;

#pragma warning disable CA1707
public sealed partial class ConsumedMetadataCaptureTests
{
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
}

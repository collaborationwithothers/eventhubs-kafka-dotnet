using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Execution;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Repository-required Given_When_Then names.

/// <summary>Exercises the real download guard and ETag contract with sparse local files instead of network payloads.</summary>
public sealed class WorkerBlobBoundaryTests
{
    [Theory]
    [InlineData(268_435_455L, true)]
    [InlineData(268_435_456L, true)]
    [InlineData(268_435_457L, false)]
    public async Task Given_ArtifactAtDownloadByteBoundary_When_Downloaded_Then_OnlyOversizeIsRejectedBeforeTransfer(long length, bool accepted)
    {
        string directory = Path.Combine(Path.GetTempPath(), "worker-blob-boundary-" + Guid.NewGuid().ToString("N"));
        string destination = Path.Combine(directory, "original.dat");
        var process = new SparseDownloadTransport(length);
        try
        {
            Task<long> download = WorkerBlobDownloader.DownloadAsync(process, "syntheticaccount", "ledger", "synthetic-original",
                destination, 268_435_456, TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
            if (accepted)
            {
                Assert.Equal(length, await download);
                Assert.Equal(length, new FileInfo(destination).Length);
                Assert.Equal(1, process.Downloads);
            }
            else
            {
                Assert.Equal("download_artifact_limit", (await Assert.ThrowsAsync<InvalidDataException>(() => download)).Message);
                Assert.Equal(0, process.Downloads);
                Assert.False(File.Exists(destination));
            }
            Assert.Equal(1, process.PropertyReads);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private sealed class SparseDownloadTransport(long length) : IProcessRunner
    {
        internal int Downloads { get; private set; }
        internal int PropertyReads { get; private set; }
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("az", request.FileName);
            Assert.Equal("storage", request.Arguments[0]);
            Assert.Equal("blob", request.Arguments[1]);
            Assert.Contains("login", request.Arguments);
            if (request.Arguments[2] == "show")
            {
                PropertyReads++;
                return Task.FromResult(new ProcessResult(0, JsonSerializer.Serialize(new { etag = "\"synthetic-original-etag\"", contentLength = length }), ""));
            }
            Assert.Equal("download", request.Arguments[2]);
            Assert.Equal("\"synthetic-original-etag\"", Value("--if-match"));
            Assert.Equal("false", Value("--overwrite"));
            Downloads++;
            using var file = new FileStream(Value("--file"), FileMode.CreateNew, FileAccess.Write);
            file.SetLength(length);
            return Task.FromResult(new ProcessResult(0, "", ""));

            string Value(string name) => request.Arguments[request.Arguments.ToList().IndexOf(name) + 1];
        }
    }
}
#pragma warning restore CA1707

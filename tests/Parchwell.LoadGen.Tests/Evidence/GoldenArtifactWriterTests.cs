using System.Security.Cryptography;
using System.Text.Json;
using Parchwell.LoadGen.Run.Publication;
using Xunit;

namespace Parchwell.LoadGen.Tests.Evidence;

#pragma warning disable CA1707 // Repository test naming uses Given_When_Then.
public sealed class GoldenArtifactWriterTests
{
    [Fact]
    public async Task Given_FixedSyntheticRun_When_ProductionWritersCloseArtifacts_Then_BytesMatchCommittedGoldenFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "golden-artifact-run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            GoldenArtifactFixture.GoldenRun run = await GoldenArtifactFixture.CreateAsync(directory,
                TestContext.Current.CancellationToken);
            string fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "fixtures", "verifier-v1", "captured-run");
            foreach (string name in GoldenArtifactFixture.ArtifactNames.Append("index.json"))
            {
                byte[] expected = await File.ReadAllBytesAsync(Path.Combine(fixtureDirectory, name),
                    TestContext.Current.CancellationToken);
                Assert.Equal(expected, run.Payloads[name]);
            }

            Assert.Equal(GoldenArtifactFixture.ArtifactNames.Append("index.json"),
                run.Requests.Select(request => request.Name));
            Assert.All(run.Requests, request =>
            {
                Assert.Equal(HttpMethod.Put, request.Method);
                Assert.Equal("*", request.IfNoneMatch);
            });
            VerifyIndexDescriptors(run.Payloads);

            using var indexStream = new MemoryStream(run.Payloads["index.json"], writable: false);
            RunArtifactIndex index = await RunArtifactIndexReader.ReadAsync(indexStream, run.Identity,
                maximumIndexBytes: 16 * 1024, maximumArtifactBytes: 2_000_000,
                maximumAggregateArtifactBytes: 8_000_000,
                cancellationToken: TestContext.Current.CancellationToken);
            await RunArtifactIndexReader.ValidateArtifactSetAsync(index, 0, run.Expectations,
                GoldenArtifactFixture.ArtifactNames.Append("index.json").ToArray(),
                (name, _) => ValueTask.FromResult<Stream>(new MemoryStream(run.Payloads[name], writable: false)),
                2_000_000, 8_000_000, TestContext.Current.CancellationToken);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static void VerifyIndexDescriptors(Dictionary<string, byte[]> payloads)
    {
        using JsonDocument document = JsonDocument.Parse(payloads["index.json"]);
        JsonElement artifacts = document.RootElement.GetProperty("artifacts");
        Assert.Equal(GoldenArtifactFixture.ArtifactNames, artifacts.EnumerateArray()
            .Select(item => item.GetProperty("name").GetString()));
        foreach (JsonElement descriptor in artifacts.EnumerateArray())
        {
            string name = descriptor.GetProperty("name").GetString()!;
            byte[] bytes = payloads[name];
            Assert.Equal(bytes.LongLength, descriptor.GetProperty("length").GetInt64());
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                descriptor.GetProperty("sha256").GetString());
        }
    }
}
#pragma warning restore CA1707

using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class B10TimeoutEvidenceExportTests
{
    [Fact]
    public async Task Given_SyntheticTimeoutWorker_When_OrdinaryHostCompletes_Then_ProductionEvidenceIsPresentAndOptionallyExported()
    {
        // Synthetic workload output is serialized by the ordinary production host and publisher.
        using var fixture = new B10SecondWorkerTests.Fixture("C1");
        Assert.Equal(0, await fixture.RunAsync(second: false));

        const string repeatPrefix = "run-1/C1/r1/";
        const string producerPrefix = repeatPrefix + "producer/pod-first/";
        foreach (string name in new[] { "registration.json", "completion.json", "publication/manifest.json", "publication/ledger.jsonl", "publication/index.json" })
            Assert.Contains(producerPrefix + name, fixture.Store.Objects.Keys);

        string? output = Environment.GetEnvironmentVariable("PARCHWELL_B10_TIMEOUT_EVIDENCE_OUTPUT");
        if (string.IsNullOrEmpty(output)) return;

        Directory.CreateDirectory(output);
        var objects = fixture.Store.Objects
            .Where(pair => pair.Key.StartsWith(producerPrefix, StringComparison.Ordinal))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new { name = pair.Key[repeatPrefix.Length..], bytes = pair.Value })
            .ToArray();
        foreach (var item in objects)
        {
            string path = Path.Combine(output, item.name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, item.bytes, TestContext.Current.CancellationToken);
        }

        byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            files = objects.Select(item => new
            {
                item.name,
                length = item.bytes.LongLength,
                sha256 = Convert.ToHexStringLower(SHA256.HashData(item.bytes))
            })
        });
        await File.WriteAllBytesAsync(Path.Combine(output, "export.json"), manifest, TestContext.Current.CancellationToken);
    }
}
#pragma warning restore CA1707

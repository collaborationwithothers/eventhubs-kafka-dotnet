using System.Text.Json;
using Parchwell.Kafka.Authentication;
using Xunit;

namespace Parchwell.Kafka.Tests.Authentication;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Integration")]
public sealed class ProjectedTokenFileObserverIntegrationTests
{
    [Fact]
    public void Given_MissingPath_When_ConstructedAndObserved_Then_ConstructionDoesNoFileRead()
    {
        using var directory = new TemporaryDirectory();
        string missing = Path.Combine(directory.Path, "missing-token-file-secret");
        var observer = NewObserver(missing);

        ProjectedTokenFileObservation result = observer.Observe();

        Assert.Equal(1, result.Sample);
        Assert.Equal("unavailable", result.Status);
        Assert.Null(result.Generation);
        Assert.DoesNotContain("missing-token-file-secret", JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_StableSymlinkTarget_When_ObservedTwice_Then_GenerationRemainsOne()
    {
        using var directory = new TemporaryDirectory();
        string target = directory.File("target-a", "not-a-jwt-secret-a");
        string link = Path.Combine(directory.Path, "token-link");
        File.CreateSymbolicLink(link, target);
        var observer = NewObserver(link);

        ProjectedTokenFileObservation first = observer.Observe();
        ProjectedTokenFileObservation second = observer.Observe();

        Assert.Equal(1, first.Generation);
        Assert.Equal(1, second.Generation);
        Assert.Equal("observed", second.Status);
    }

    [Fact]
    public void Given_AtomicSymlinkTargetSwitch_When_Observed_Then_GenerationAdvancesAndStableTargetIsRetained()
    {
        using var directory = new TemporaryDirectory();
        string targetA = directory.File("target-a", "not-a-jwt-secret-a");
        string targetB = directory.File("target-b", "not-a-jwt-secret-b-with-other-length");
        string link = Path.Combine(directory.Path, "token-link");
        File.CreateSymbolicLink(link, targetA);
        var observer = NewObserver(link);
        Assert.Equal(1, observer.Observe().Generation);

        string replacement = Path.Combine(directory.Path, "token-link-next");
        File.CreateSymbolicLink(replacement, targetB);
        File.Move(replacement, link, overwrite: true);

        ProjectedTokenFileObservation changed = observer.Observe();
        ProjectedTokenFileObservation stable = observer.Observe();

        Assert.Equal(2, changed.Generation);
        Assert.Equal(2, stable.Generation);
        Assert.Equal("observed", changed.Status);
        Assert.DoesNotContain("not-a-jwt", JsonSerializer.Serialize(changed), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_TargetDisappearsThenNewTargetAppears_When_Observed_Then_UnavailableRetainsPriorGeneration()
    {
        using var directory = new TemporaryDirectory();
        string targetA = directory.File("target-a", "not-a-jwt-secret-a");
        string targetB = directory.File("target-b", "not-a-jwt-secret-b");
        string link = Path.Combine(directory.Path, "token-link");
        File.CreateSymbolicLink(link, targetA);
        var observer = NewObserver(link);
        Assert.Equal(1, observer.Observe().Generation);
        File.Delete(targetA);

        ProjectedTokenFileObservation missing = observer.Observe();
        string replacement = Path.Combine(directory.Path, "token-link-next");
        File.CreateSymbolicLink(replacement, targetB);
        File.Move(replacement, link, overwrite: true);
        ProjectedTokenFileObservation recovered = observer.Observe();

        Assert.Equal("unavailable", missing.Status);
        Assert.Null(missing.Generation);
        Assert.Equal(2, recovered.Generation);
    }

    [Fact]
    public void Given_RegularFileMetadataChanges_When_Observed_Then_GenerationAdvancesWithoutReadingContents()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.File("regular-token", "not-a-jwt-secret");
        var observer = NewObserver(path);
        Assert.Equal(1, observer.Observe().Generation);
        File.AppendAllText(path, "-changed");

        ProjectedTokenFileObservation changed = observer.Observe();

        Assert.Equal(2, changed.Generation);
        Assert.Equal("observed", changed.Status);
        string json = JsonSerializer.Serialize(changed);
        Assert.DoesNotContain("not-a-jwt", json, StringComparison.Ordinal);
        Assert.DoesNotContain(path, json, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_RegularFile_When_Observed_Then_UsesFileMetadataWithoutSymlink()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.File("regular-token", "synthetic-invalid-jwt-content");
        var observer = NewObserver(path);

        ProjectedTokenFileObservation result = observer.Observe();

        Assert.Equal("observed", result.Status);
        Assert.Equal(1, result.Generation);
        Assert.DoesNotContain("synthetic-invalid-jwt-content", JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    private static ProjectedTokenFileObserver NewObserver(string path) =>
        new(path, new FixedTimeProvider());

    private sealed class FixedTimeProvider : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => 1000;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
        public override long GetTimestamp() => ++_timestamp;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"parchwell-token-observer-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        internal string File(string name, string contents)
        {
            string path = System.IO.Path.Combine(Path, name);
            System.IO.File.WriteAllText(path, contents);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}

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

    // A projected leaf link can remain constant while its intermediate directory target changes.
    [Fact]
    public void Given_IntermediateDirectoryTargetSwitchesWithEqualMetadata_When_Observed_Then_GenerationTracksResolvedPath()
    {
        using var directory = new TemporaryDirectory();
        (string tokenV1, string tokenV2) = CreateEqualMetadataVersions(directory);
        string data = Path.Combine(directory.Path, "..data");
        string next = Path.Combine(directory.Path, "..data-next");
        string token = Path.Combine(directory.Path, "token");
        Directory.CreateSymbolicLink(data, "v1");
        File.CreateSymbolicLink(token, "..data/token");
        var observer = NewObserver(token);

        ProjectedTokenFileObservation first = observer.Observe();
        ProjectedTokenFileObservation second = observer.Observe();
        ReplaceDirectoryLink(data, next, "v2");
        ProjectedTokenFileObservation third = observer.Observe();
        ProjectedTokenFileObservation fourth = observer.Observe();

        AssertEqualVersionMetadata(tokenV1, tokenV2);
        Assert.Equal("..data/token", new FileInfo(token).LinkTarget);
        Assert.Equal("v2", new DirectoryInfo(data).LinkTarget);
        AssertObservedPair(first, second, 1, 1, 2);
        AssertObservedPair(third, fourth, 2, 3, 4);
        AssertSanitized(third, directory.Path);
    }

    // A configured path without a leaf link still observes the intermediate directory target.
    [Fact]
    public void Given_ConfiguredPathThroughIntermediateDirectoryLink_When_Switched_Then_GenerationTracksResolvedPath()
    {
        using var directory = new TemporaryDirectory();
        (string tokenV1, string tokenV2) = CreateEqualMetadataVersions(directory);
        string data = Path.Combine(directory.Path, "..data");
        Directory.CreateSymbolicLink(data, "v1");
        var observer = NewObserver(Path.Combine(data, "token"));

        ProjectedTokenFileObservation first = observer.Observe();
        ProjectedTokenFileObservation second = observer.Observe();
        ReplaceDirectoryLink(data, Path.Combine(directory.Path, "..data-next"), "v2");
        ProjectedTokenFileObservation third = observer.Observe();
        ProjectedTokenFileObservation fourth = observer.Observe();

        AssertEqualVersionMetadata(tokenV1, tokenV2);
        AssertObservedPair(first, second, 1, 1, 2);
        AssertObservedPair(third, fourth, 2, 3, 4);
        AssertSanitized(third, directory.Path);
    }

    // Absolute targets at both link levels resolve to the same versioned file after the switch.
    [Fact]
    public void Given_AbsoluteTargetsAtBothSymlinkLevels_When_Switched_Then_GenerationTracksResolvedPath()
    {
        using var directory = new TemporaryDirectory();
        (string tokenV1, string tokenV2) = CreateEqualMetadataVersions(directory);
        string data = Path.Combine(directory.Path, "..data");
        string next = Path.Combine(directory.Path, "..data-next");
        string token = Path.Combine(directory.Path, "token");
        Directory.CreateSymbolicLink(data, Path.Combine(directory.Path, "v1"));
        File.CreateSymbolicLink(token, Path.Combine(data, "token"));
        var observer = NewObserver(token);

        ProjectedTokenFileObservation first = observer.Observe();
        ProjectedTokenFileObservation second = observer.Observe();
        ReplaceDirectoryLink(data, next, Path.Combine(directory.Path, "v2"));
        ProjectedTokenFileObservation third = observer.Observe();
        ProjectedTokenFileObservation fourth = observer.Observe();

        AssertEqualVersionMetadata(tokenV1, tokenV2);
        Assert.Equal(Path.Combine(data, "token"), new FileInfo(token).LinkTarget);
        Assert.Equal(Path.Combine(directory.Path, "v2"), new DirectoryInfo(data).LinkTarget);
        AssertObservedPair(first, second, 1, 1, 2);
        AssertObservedPair(third, fourth, 2, 3, 4);
        AssertSanitized(third, directory.Path);
    }

    // A broken target produces an unavailable sample and recovery advances from prior stable state.
    [Fact]
    public void Given_BrokenIntermediateTargetThenRecovery_When_Observed_Then_PriorGenerationIsPreserved()
    {
        using var directory = new TemporaryDirectory();
        _ = CreateEqualMetadataVersions(directory);
        string data = Path.Combine(directory.Path, "..data");
        string token = Path.Combine(directory.Path, "token");
        Directory.CreateSymbolicLink(data, "v1");
        File.CreateSymbolicLink(token, "..data/token");
        var observer = NewObserver(token);
        Assert.Equal(1, observer.Observe().Generation);

        ReplaceDirectoryLink(data, Path.Combine(directory.Path, "..data-next"), "missing");
        ProjectedTokenFileObservation broken = observer.Observe();
        ReplaceDirectoryLink(data, Path.Combine(directory.Path, "..data-next"), "v2");
        ProjectedTokenFileObservation recovered = observer.Observe();

        Assert.Equal("unavailable", broken.Status);
        Assert.Null(broken.Generation);
        Assert.Null(broken.ObservedAt);
        Assert.Null(broken.Timestamp);
        Assert.Equal(2, recovered.Generation);
        Assert.Equal("observed", recovered.Status);
    }

    // Real directory cycles, non-directory intermediates and final directories all stay unavailable.
    [Fact]
    public void Given_DirectoryLinkCyclesNonDirectoryIntermediateAndFinalDirectory_When_Observed_Then_UnavailableIsSanitized()
    {
        using var directory = new TemporaryDirectory();
        string selfLink = Path.Combine(directory.Path, "self");
        Directory.CreateSymbolicLink(selfLink, "self");
        ProjectedTokenFileObservation self = NewObserver(Path.Combine(selfLink, "token")).Observe();

        string firstCycle = Path.Combine(directory.Path, "cycle-a");
        string secondCycle = Path.Combine(directory.Path, "cycle-b");
        Directory.CreateSymbolicLink(firstCycle, "cycle-b");
        Directory.CreateSymbolicLink(secondCycle, "cycle-a");
        ProjectedTokenFileObservation cycle = NewObserver(Path.Combine(firstCycle, "token")).Observe();

        string ordinaryFile = directory.File("ordinary-file", "synthetic-file");
        ProjectedTokenFileObservation nonDirectory = NewObserver(Path.Combine(ordinaryFile, "token")).Observe();

        string finalDirectory = Path.Combine(directory.Path, "final-directory");
        Directory.CreateDirectory(finalDirectory);
        ProjectedTokenFileObservation final = NewObserver(finalDirectory).Observe();

        foreach (ProjectedTokenFileObservation result in new[] { self, cycle, nonDirectory, final })
        {
            Assert.Equal("unavailable", result.Status);
            Assert.Null(result.Generation);
            Assert.Null(result.ObservedAt);
            Assert.Null(result.Timestamp);
        }

        Assert.DoesNotContain("synthetic-file", JsonSerializer.Serialize(nonDirectory), StringComparison.Ordinal);
    }

    // The production filesystem path accepts 40 directory links and rejects the forty-first.
    [Theory]
    [InlineData(40, "observed", 1)]
    [InlineData(41, "unavailable", null)]
    public void Given_RealDirectoryLinkChainAtExpansionBoundary_When_Observed_Then_EnforcesFortyLinks(int count, string expectedStatus, int? expectedGeneration)
    {
        using var directory = new TemporaryDirectory();
        string version = Path.Combine(directory.Path, "version");
        Directory.CreateDirectory(version);
        directory.File(Path.Combine("version", "token"), "synthetic");
        for (int index = 1; index <= count; index++)
        {
            string link = Path.Combine(directory.Path, $"d{index}");
            string target = index == count ? "version" : $"d{index + 1}";
            Directory.CreateSymbolicLink(link, target);
        }

        ProjectedTokenFileObservation result = NewObserver(Path.Combine(directory.Path, "d1", "token")).Observe();

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(expectedGeneration, result.Generation);
    }

    private static ProjectedTokenFileObserver NewObserver(string path) =>
        new(path, new FixedTimeProvider());

    private static (string TokenV1, string TokenV2) CreateEqualMetadataVersions(TemporaryDirectory directory)
    {
        string tokenV1 = Path.Combine(directory.Path, "v1", "token");
        string tokenV2 = Path.Combine(directory.Path, "v2", "token");
        Directory.CreateDirectory(Path.GetDirectoryName(tokenV1)!);
        Directory.CreateDirectory(Path.GetDirectoryName(tokenV2)!);
        File.WriteAllText(tokenV1, "aaaa");
        File.WriteAllText(tokenV2, "bbbb");
        DateTime timestamp = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(tokenV1, timestamp);
        File.SetLastWriteTimeUtc(tokenV2, timestamp);
        AssertEqualVersionMetadata(tokenV1, tokenV2);
        return (tokenV1, tokenV2);
    }

    private static void AssertEqualVersionMetadata(string tokenV1, string tokenV2)
    {
        var first = new FileInfo(tokenV1);
        var second = new FileInfo(tokenV2);
        first.Refresh();
        second.Refresh();
        Assert.Equal(4, first.Length);
        Assert.Equal(first.Length, second.Length);
        Assert.Equal(first.LastWriteTimeUtc.Ticks, second.LastWriteTimeUtc.Ticks);
        Assert.Equal(new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc).Ticks, first.LastWriteTimeUtc.Ticks);
    }

    private static void ReplaceDirectoryLink(string data, string next, string target)
    {
        Directory.CreateSymbolicLink(next, target);
        File.Delete(data);
        Directory.Move(next, data);
    }

    private static void AssertObservedPair(
        ProjectedTokenFileObservation first,
        ProjectedTokenFileObservation second,
        int generation,
        int firstSample,
        int secondSample)
    {
        Assert.Equal("observed", first.Status);
        Assert.Equal("observed", second.Status);
        Assert.Equal(generation, first.Generation);
        Assert.Equal(generation, second.Generation);
        Assert.Equal(firstSample, first.Sample);
        Assert.Equal(secondSample, second.Sample);
    }

    private static void AssertSanitized(ProjectedTokenFileObservation result, string temporaryPath)
    {
        string json = JsonSerializer.Serialize(result);
        foreach (string privateValue in new[] { temporaryPath, "v1", "v2", "aaaa", "bbbb", "..data", "exception-sentinel" })
            Assert.DoesNotContain(privateValue, json, StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(
            ["Generation", "ObservedAt", "Sample", "Status", "Timestamp", "TimestampFrequency"],
            document.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
    }

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
            string? parent = System.IO.Path.GetDirectoryName(path);
            if (parent is not null)
                Directory.CreateDirectory(parent);
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

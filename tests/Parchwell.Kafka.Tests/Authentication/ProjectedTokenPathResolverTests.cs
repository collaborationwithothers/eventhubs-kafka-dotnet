using Parchwell.Kafka.Authentication;
using Xunit;

namespace Parchwell.Kafka.Tests.Authentication;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

[Trait("Category", "Unit")]
public sealed class ProjectedTokenPathResolverTests
{
    // Intermediate directory links must change the compared path even when a leaf link is unchanged.
    [Fact]
    public void Given_IntermediateDirectoryLinkChanges_When_ResolvingTokenPath_Then_ResolvedVersionPathChanges()
    {
        Assert.Equal("/r/v1/token", Resolve(
            "/r/token", [new("/r/token", "..data/token"), new("/r/..data", "v1")],
            ["/r", "/r/v1"], ["/r", "/r/token", "/r/..data", "/r/v1", "/r/v1/token"]));
        Assert.Equal("/r/v2/token", Resolve(
            "/r/token", [new("/r/token", "..data/token"), new("/r/..data", "v2")],
            ["/r", "/r/v2"], ["/r", "/r/token", "/r/..data", "/r/v2", "/r/v2/token"]));
        Assert.Equal("/r/v1/token", Resolve(
            "/r/..data/token", [new("/r/..data", "v1")],
            ["/r", "/r/v1"], ["/r", "/r/..data", "/r/v1", "/r/v1/token"]));
        Assert.Equal("/r/v2/token", Resolve(
            "/r/..data/token", [new("/r/..data", "v2")],
            ["/r", "/r/v2"], ["/r", "/r/..data", "/r/v2", "/r/v2/token"]));
        Assert.Equal("/versions/v1/token", Resolve(
            "/r/token", [new("/r/token", "..data/token"), new("/r/..data", "/versions/v1")],
            ["/r", "/versions", "/versions/v1"], ["/r", "/r/token", "/r/..data", "/versions", "/versions/v1", "/versions/v1/token"]));
        Assert.Equal("/r/token", Resolve("/r/token", [], ["/r"], ["/r", "/r/token"]));
    }

    // Raw target links must be expanded before their dot segments are consumed.
    [Fact]
    public void Given_RawTargetDotSegments_When_Resolving_Then_LinksAreProcessedBeforeDotSegments()
    {
        Assert.Equal("/else/token", Resolve(
            "/r/link", [new("/r/link", "a/../token"), new("/r/a", "/else/deep")],
            ["/r", "/else", "/else/deep"], ["/r", "/r/link", "/r/a", "/else", "/else/deep", "/else/token"]));
        Assert.Equal("/r/token", Resolve("/r/./token", [], ["/r"], ["/r", "/r/token"]));
        Assert.Equal("/token", Resolve("/../token", [], [], ["/token"]));
        Assert.Equal("/r/dir/token", Resolve(
            "/r/again/../again/token", [new("/r/again", "dir")], ["/r", "/r/dir"],
            ["/r", "/r/again", "/r/dir", "/r/again", "/r/dir", "/r/dir/token"]));
    }

    // Repeated links are bounded by finite budgets rather than a global seen-link ban.
    [Fact]
    public void Given_LinkCycle_When_Resolving_Then_FiniteBudgetsRejectWithoutGlobalRevisitRule()
    {
        var selfCandidates = new List<string>();
        Assert.Throws<IOException>(() => ProjectedTokenPathResolver.Resolve(
            "/r/token",
            candidate =>
            {
                selfCandidates.Add(candidate);
                return candidate switch
                {
                    "/r" => null,
                    "/r/token" => "token",
                    _ => throw new InvalidOperationException("Unexpected resolver probe.")
                };
            },
            candidate => Assert.Equal("/r", candidate)));
        int cycleLinkProbes = 0;
        var cycleCandidates = new List<string>();
        Assert.Throws<IOException>(() => ProjectedTokenPathResolver.Resolve(
            "/r/token",
            candidate =>
            {
                cycleCandidates.Add(candidate);
                if (candidate is "/r/token" or "/r/other")
                    cycleLinkProbes++;
                return candidate switch
                {
                    "/r" => null,
                    "/r/token" => "other",
                    "/r/other" => "token",
                    _ => throw new InvalidOperationException("Unexpected resolver probe.")
                };
            },
            candidate => Assert.Equal("/r", candidate)));

        Assert.Equal(41, selfCandidates.Count(candidate => candidate == "/r/token"));
        Assert.Equal(41, cycleLinkProbes);
        Assert.Equal(Enumerable.Repeat("/r/token", 41).Prepend("/r"), selfCandidates);
        Assert.Equal(Enumerable.Range(0, 41).Select(index => index % 2 == 0 ? "/r/token" : "/r/other").Prepend("/r"), cycleCandidates);
        Assert.True(selfCandidates.Count(candidate => candidate == "/r/token") <= ProjectedTokenPathResolver.MaximumComponentVisits);
        Assert.True(cycleLinkProbes <= ProjectedTokenPathResolver.MaximumComponentVisits);
    }

    // The inclusive expansion limit accepts 40 substitutions and rejects the next one before following it.
    [Theory]
    [InlineData(39, true)]
    [InlineData(40, true)]
    [InlineData(41, false)]
    public void Given_LinkChainAtExpansionBoundary_When_Resolving_Then_EnforcesFortySubstitutions(int count, bool succeeds)
    {
        var links = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 1; index <= count; index++)
            links[$"/r/l{index}"] = index == count ? "target" : $"l{index + 1}";
        var expectedProbes = new List<string> { "/r" };
        expectedProbes.AddRange(Enumerable.Range(1, count).Select(index => $"/r/l{index}"));
        if (succeeds)
            expectedProbes.Add("/r/target");
        var expectedProbeSet = expectedProbes.ToHashSet(StringComparer.Ordinal);
        var probes = new List<string>();

        if (succeeds)
        {
            string result = ProjectedTokenPathResolver.Resolve(
                "/r/l1",
                candidate =>
                {
                    Assert.Contains(candidate, expectedProbeSet);
                    probes.Add(candidate);
                    return links.GetValueOrDefault(candidate);
                },
                candidate => Assert.Equal("/r", candidate));
            Assert.Equal("/r/target", result);
        }
        else
        {
            Assert.Throws<IOException>(() => ProjectedTokenPathResolver.Resolve(
                "/r/l1",
                candidate =>
                {
                    Assert.Contains(candidate, expectedProbeSet);
                    probes.Add(candidate);
                    return links.GetValueOrDefault(candidate);
                },
                candidate => Assert.Equal("/r", candidate)));
        }

        Assert.Equal(expectedProbes, probes);
        if (succeeds)
            Assert.Equal("/r/target", probes[^1]);
        else
            Assert.DoesNotContain("/r/target", probes);
    }

    // A directory-link chain keeps its expansion budget while a suffix remains pending.
    [Fact]
    public void Given_FortyOneDirectoryLinksWithPendingSuffix_When_Resolving_Then_RejectsFortyFirstSubstitution()
    {
        var links = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 1; index <= 41; index++)
            links[$"/r/d{index}"] = index == 41 ? "final" : $"d{index + 1}";
        var expectedProbes = new List<string> { "/r" };
        expectedProbes.AddRange(Enumerable.Range(1, 41).Select(index => $"/r/d{index}"));
        var expectedProbeSet = expectedProbes.ToHashSet(StringComparer.Ordinal);
        var probes = new List<string>();

        Assert.Throws<IOException>(() => ProjectedTokenPathResolver.Resolve(
            "/r/d1/token",
            candidate =>
            {
                Assert.Contains(candidate, expectedProbeSet);
                probes.Add(candidate);
                return links.GetValueOrDefault(candidate);
            },
            candidate => Assert.Equal("/r", candidate)));

        Assert.Equal(expectedProbes, probes);
        Assert.DoesNotContain("/r/final", probes);
        Assert.DoesNotContain("/r/final/token", probes);
    }

    // Every normal component, including the final file, consumes one visit.
    [Theory]
    [InlineData(255, true)]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void Given_NormalComponentVisitBoundary_When_Resolving_Then_Enforces256Visits(int count, bool succeeds)
    {
        string path = "/" + string.Join('/', Enumerable.Repeat("a", count));
        var directories = Prefixes(path);
        var probes = new List<string>();
        string[] expectedProbes = Enumerable.Range(1, Math.Min(count, ProjectedTokenPathResolver.MaximumComponentVisits))
            .Select(visited => "/" + string.Join('/', Enumerable.Repeat("a", visited)))
            .ToArray();
        var expectedProbeSet = expectedProbes.ToHashSet(StringComparer.Ordinal);
        string? resolved = null;
        Action act = () => resolved = ProjectedTokenPathResolver.Resolve(
            path,
            candidate =>
            {
                Assert.Contains(candidate, expectedProbeSet);
                probes.Add(candidate);
                return null;
            },
            candidate => Assert.Contains(candidate, directories));

        if (succeeds)
            act();
        else
            Assert.Throws<IOException>(act);

        Assert.Equal(expectedProbes, probes);
        if (succeeds)
            Assert.Equal(path, resolved);
    }

    // Dot components consume visits even though they need no filesystem delegate.
    [Theory]
    [InlineData(255, true)]
    [InlineData(256, false)]
    [InlineData(257, false)]
    public void Given_DotComponentVisitBoundary_When_Resolving_Then_DotsConsumeVisits(int dots, bool succeeds)
    {
        string path = "/" + string.Join('/', Enumerable.Repeat(".", dots)) + "/token";
        var probes = new List<string>();
        string[] expectedProbes = succeeds ? ["/token"] : [];
        var expectedProbeSet = expectedProbes.ToHashSet(StringComparer.Ordinal);
        Action act = () => Assert.Equal("/token", ProjectedTokenPathResolver.Resolve(
            path,
            candidate =>
            {
                Assert.Contains(candidate, expectedProbeSet);
                probes.Add(candidate);
                return null;
            },
            _ => { }));

        if (succeeds)
            act();
        else
            Assert.Throws<IOException>(act);

        Assert.Equal(expectedProbes, probes);
    }

    // The absolute input bound is inclusive at exactly 4096 characters.
    [Theory]
    [InlineData(4095)]
    [InlineData(4096)]
    public void Given_AbsoluteInputAtPathLengthBoundary_When_Resolving_Then_AcceptsInclusiveLimit(int length)
    {
        string path = "/" + new string('a', length - 1);
        var probes = new List<string>();

        string result = ProjectedTokenPathResolver.Resolve(path, candidate => { probes.Add(candidate); return null; }, _ => { });

        Assert.Equal(length, path.Length);
        Assert.Equal(path, result);
        Assert.Equal([path], probes);
    }

    // Overlong configured input is rejected before any filesystem work.
    [Fact]
    public void Given_OverlongInput_When_Resolving_Then_RejectsBeforeFilesystemDelegates()
    {
        int probes = 0;
        Assert.Throws<IOException>(() => ProjectedTokenPathResolver.Resolve(
            "/" + new string('a', 4096),
            _ => { probes++; return null; },
            _ => { }));
        Assert.Equal(0, probes);
    }

    // Expanded pending paths are bounded before the expanded target is probed.
    [Theory]
    [InlineData(4059, 4095, true)]
    [InlineData(4060, 4096, true)]
    [InlineData(4061, 4097, false)]
    public void Given_ExpandedPendingPathAtLengthBoundary_When_Resolving_Then_ValidatesBeforeExpansionWork(int componentLength, int expectedLength, bool succeeds)
    {
        string leaf = new('a', componentLength);
        string linkTarget = new('x', 32);
        string path = "/r/link/" + leaf;
        string targetPath = "/r/" + linkTarget;
        string[] expectedProbes = succeeds
            ? new[] { "/r", "/r/link", targetPath, targetPath + "/" + leaf }
            : new[] { "/r", "/r/link" };
        var expectedProbeSet = expectedProbes.ToHashSet(StringComparer.Ordinal);
        var probes = new List<string>();
        Action act = () =>
        {
            string resolved = ProjectedTokenPathResolver.Resolve(path, candidate =>
            {
                Assert.Contains(candidate, expectedProbeSet);
                probes.Add(candidate);
                return candidate switch
                {
                    "/r/link" => linkTarget,
                    "/r" => null,
                    _ => null
                };
            }, candidate => Assert.Contains(candidate, new[] { "/r", targetPath }));
            Assert.Equal("/r/" + linkTarget + "/" + leaf, resolved);
        };

        Assert.Equal(expectedLength, ("/r/" + linkTarget + "/" + leaf).Length);
        if (succeeds)
            act();
        else
            Assert.Throws<IOException>(act);

        Assert.Equal(expectedProbes, probes);
    }

    // Raw link text is validated before it can create more traversal work.
    [Fact]
    public void Given_OverlongRawTarget_When_Resolving_Then_RejectsBeforeSubstitution()
    {
        var probes = new List<string>();
        Assert.Throws<IOException>(() => ProjectedTokenPathResolver.Resolve(
            "/r/link",
            candidate =>
            {
                probes.Add(candidate);
                return candidate switch
                {
                    "/r" => null,
                    "/r/link" => new string('a', 4097),
                    _ => throw new InvalidOperationException("Unexpected resolver probe.")
                };
            },
            candidate => Assert.Equal("/r", candidate)));
        Assert.Equal(["/r", "/r/link"], probes);
    }

    // Empty and NUL raw targets cannot enter the traversal queue.
    [Fact]
    public void Given_EmptyOrNulRawTarget_When_Resolving_Then_RejectsBeforeFollowingIt()
    {
        foreach (string target in new[] { "", "bad\0target" })
        {
            Assert.Throws<IOException>(() => ProjectedTokenPathResolver.Resolve(
                "/r/link",
                candidate => candidate switch
                {
                    "/r" => null,
                    "/r/link" => target,
                    _ => throw new InvalidOperationException("Unexpected resolver probe.")
                },
                candidate => Assert.Equal("/r", candidate)));
        }
    }

    // Invalid paths and partially qualified Windows roots fail instead of being guessed.
    [Fact]
    public void Given_InvalidAndUnsupportedPaths_When_Resolving_Then_RejectsWithoutGuessing()
    {
        foreach (string invalid in new[] { "", "\0" })
            Assert.ThrowsAny<ArgumentException>(() => ProjectedTokenPathResolver.Resolve(invalid, _ => null, _ => { }));
        Assert.Throws<IOException>(() => ProjectedTokenPathResolver.Resolve("/", _ => null, _ => { }));

        Assert.Throws<ArgumentNullException>(() => ProjectedTokenPathResolver.Resolve(null!, _ => null, _ => { }));
        if (OperatingSystem.IsWindows())
        {
            Assert.Throws<IOException>(() => ProjectedTokenPathResolver.Resolve("C:token", _ => null, _ => { }));
            Assert.Throws<IOException>(() => ProjectedTokenPathResolver.Resolve("\\token", _ => null, _ => { }));
            Assert.Throws<IOException>(() => ProjectedTokenPathResolver.Resolve(
                "C:\\r\\link",
                _ => "\\target",
                _ => { }));
        }
    }

    // Filesystem delegate errors remain failures for the observer to sanitize.
    [Fact]
    public void Given_FilesystemDelegatesFail_When_Resolving_Then_TheirFailuresPropagate()
    {
        Assert.Throws<UnauthorizedAccessException>(() => ProjectedTokenPathResolver.Resolve(
            "/r/token",
            candidate => candidate switch
            {
                "/r" => null,
                "/r/token" => throw new UnauthorizedAccessException(),
                _ => throw new InvalidOperationException("Unexpected resolver probe.")
            },
            candidate => Assert.Equal("/r", candidate)));
        Assert.Throws<DirectoryNotFoundException>(() => ProjectedTokenPathResolver.Resolve(
            "/r/dir/token",
            candidate => candidate switch
            {
                "/r" or "/r/dir" => null,
                _ => throw new InvalidOperationException("Unexpected resolver probe.")
            },
            candidate =>
            {
                if (candidate == "/r")
                    return;
                Assert.Equal("/r/dir", candidate);
                throw new DirectoryNotFoundException();
            }));
        Assert.Throws<DirectoryNotFoundException>(() => ProjectedTokenPathResolver.Resolve(
            "/r/missing/token",
            candidate => candidate switch
            {
                "/r" or "/r/missing" => null,
                _ => throw new InvalidOperationException("Unexpected resolver probe.")
            },
            candidate =>
            {
                if (candidate == "/r")
                    return;
                Assert.Equal("/r/missing", candidate);
                throw new DirectoryNotFoundException();
            }));
    }

    private static string Resolve(
        string path,
        (string Path, string Target)[] links,
        string[] directories,
        string[] expectedProbes)
    {
        var linkMap = links.ToDictionary(link => link.Path, link => link.Target, StringComparer.Ordinal);
        var directorySet = directories.ToHashSet(StringComparer.Ordinal);
        var linkSet = linkMap.Keys.ToHashSet(StringComparer.Ordinal);
        var expectedProbeSet = expectedProbes.ToHashSet(StringComparer.Ordinal);
        var probes = new List<string>();
        string result = ProjectedTokenPathResolver.Resolve(
            path,
            candidate =>
            {
                Assert.Contains(candidate, expectedProbeSet);
                probes.Add(candidate);
                return linkMap.GetValueOrDefault(candidate);
            },
            candidate =>
            {
                Assert.DoesNotContain(candidate, linkSet);
                Assert.Contains(candidate, directorySet);
            });
        Assert.Equal(expectedProbes, probes);
        return result;
    }

    private static HashSet<string> Prefixes(string path)
    {
        string[] components = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var prefixes = new HashSet<string>(StringComparer.Ordinal) { "/" };
        string current = "/";
        foreach (string component in components.SkipLast(1))
        {
            current = current == "/" ? "/" + component : current + "/" + component;
            prefixes.Add(current);
        }
        return prefixes;
    }
}

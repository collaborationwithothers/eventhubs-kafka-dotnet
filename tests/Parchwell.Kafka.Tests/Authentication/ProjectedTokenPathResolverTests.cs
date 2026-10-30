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
        Assert.Equal("/r/v1/token", Resolve("/r/token", [new("/r/token", "..data/token"), new("/r/..data", "v1")], ["/r", "/r/v1"]));
        Assert.Equal("/r/v2/token", Resolve("/r/token", [new("/r/token", "..data/token"), new("/r/..data", "v2")], ["/r", "/r/v2"]));
        Assert.Equal("/r/v1/token", Resolve("/r/..data/token", [new("/r/..data", "v1")], ["/r", "/r/v1"]));
        Assert.Equal("/versions/v1/token", Resolve("/r/token", [new("/r/token", "..data/token"), new("/r/..data", "/versions/v1")], ["/r", "/versions", "/versions/v1"]));
        Assert.Equal("/r/token", Resolve("/r/token", [], ["/r"]));
    }

    // Raw target links must be expanded before their dot segments are consumed.
    [Fact]
    public void Given_RawTargetDotSegments_When_Resolving_Then_LinksAreProcessedBeforeDotSegments()
    {
        Assert.Equal("/else/token", Resolve("/r/link", [new("/r/link", "a/../token"), new("/r/a", "/else/deep")], ["/r", "/else", "/else/deep"]));
        Assert.Equal("/r/token", Resolve("/r/./token", [], ["/r"]));
        Assert.Equal("/token", Resolve("/../token", [], []));
        Assert.Equal("/r/dir/token", Resolve("/r/again/../again/token", [new("/r/again", "dir")], ["/r", "/r/dir"]));
    }

    // Repeated links are bounded by finite budgets rather than a global seen-link ban.
    [Fact]
    public void Given_LinkCycle_When_Resolving_Then_FiniteBudgetsRejectWithoutGlobalRevisitRule()
    {
        int selfProbes = 0;
        Assert.Throws<IOException>(() => ProjectedTokenPathResolver.Resolve(
            "/r/token",
            candidate =>
            {
                selfProbes++;
                return candidate == "/r/token" ? "token" : null;
            },
            _ => { }));
        int cycleLinkProbes = 0;
        Assert.Throws<IOException>(() => ProjectedTokenPathResolver.Resolve(
            "/r/token",
            candidate =>
            {
                if (candidate is "/r/token" or "/r/other")
                    cycleLinkProbes++;
                return candidate switch
                {
                    "/r/token" => "other",
                    "/r/other" => "token",
                    _ => null
                };
            },
            _ => { }));

        Assert.Equal(42, selfProbes);
        Assert.Equal(41, cycleLinkProbes);
        Assert.True(selfProbes <= ProjectedTokenPathResolver.MaximumComponentVisits);
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
        var probes = new List<string>();

        if (succeeds)
        {
            string result = ProjectedTokenPathResolver.Resolve(
                "/r/l1",
                candidate =>
                {
                    probes.Add(candidate);
                    return links.GetValueOrDefault(candidate);
                },
                _ => { });
            Assert.Equal("/r/target", result);
        }
        else
        {
            Assert.Throws<IOException>(() => ProjectedTokenPathResolver.Resolve(
                "/r/l1",
                candidate =>
                {
                    probes.Add(candidate);
                    return links.GetValueOrDefault(candidate);
                },
                _ => { }));
        }

        Assert.Equal(succeeds ? count + 2 : count + 1, probes.Count);
        Assert.DoesNotContain("/r/target", probes.Where(candidate => links.ContainsKey(candidate)));
    }

    // A directory-link chain keeps its expansion budget while a suffix remains pending.
    [Fact]
    public void Given_FortyOneDirectoryLinksWithPendingSuffix_When_Resolving_Then_RejectsFortyFirstSubstitution()
    {
        var links = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 1; index <= 41; index++)
            links[$"/r/d{index}"] = index == 41 ? "final" : $"d{index + 1}";
        int probes = 0;

        Assert.Throws<IOException>(() => ProjectedTokenPathResolver.Resolve(
            "/r/d1/token",
            candidate =>
            {
                probes++;
                return links.GetValueOrDefault(candidate);
            },
            _ => { }));

        Assert.Equal(42, probes);
    }

    // Every normal component, including the final file, consumes one visit.
    [Theory]
    [InlineData(255, true)]
    [InlineData(256, false)]
    [InlineData(257, false)]
    public void Given_NormalComponentVisitBoundary_When_Resolving_Then_Enforces256Visits(int count, bool succeeds)
    {
        string path = "/" + string.Join('/', Enumerable.Repeat("a", count)) + "/token";
        var directories = Prefixes(path);
        int probes = 0;
        Action act = () => ProjectedTokenPathResolver.Resolve(
            path,
            _ =>
            {
                probes++;
                return null;
            },
            candidate => Assert.Contains(candidate, directories));

        if (succeeds)
            act();
        else
            Assert.Throws<IOException>(act);

        Assert.Equal(Math.Min(count + 1, ProjectedTokenPathResolver.MaximumComponentVisits), probes);
    }

    // Dot components consume visits even though they need no filesystem delegate.
    [Theory]
    [InlineData(255, true)]
    [InlineData(256, false)]
    [InlineData(257, false)]
    public void Given_DotComponentVisitBoundary_When_Resolving_Then_DotsConsumeVisits(int dots, bool succeeds)
    {
        string path = "/" + string.Join('/', Enumerable.Repeat(".", dots)) + "/token";
        int probes = 0;
        Action act = () => Assert.Equal("/token", ProjectedTokenPathResolver.Resolve(
            path,
            _ =>
            {
                probes++;
                return null;
            },
            _ => { }));

        if (succeeds)
            act();
        else
            Assert.Throws<IOException>(act);

        Assert.Equal(succeeds ? 1 : 0, probes);
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
        int probes = 0;
        Action act = () =>
        {
            string resolved = ProjectedTokenPathResolver.Resolve(path, candidate =>
            {
                probes++;
                return candidate == "/r/link" ? linkTarget : null;
            }, candidate => Assert.Contains(candidate, new[] { "/r", "/r/" + linkTarget }));
            Assert.Equal("/r/" + linkTarget + "/" + leaf, resolved);
        };

        Assert.Equal(expectedLength, ("/r/" + linkTarget + "/" + leaf).Length);
        if (succeeds)
            act();
        else
            Assert.Throws<IOException>(act);

        Assert.Equal(succeeds ? 4 : 2, probes);
    }

    // Raw link text is validated before it can create more traversal work.
    [Fact]
    public void Given_OverlongRawTarget_When_Resolving_Then_RejectsBeforeSubstitution()
    {
        int probes = 0;
        Assert.Throws<IOException>(() => ProjectedTokenPathResolver.Resolve(
            "/r/link",
            _ =>
            {
                probes++;
                return new string('a', 4097);
            },
            _ => { }));
        Assert.Equal(1, probes);
    }

    // Empty and NUL raw targets cannot enter the traversal queue.
    [Fact]
    public void Given_EmptyOrNulRawTarget_When_Resolving_Then_RejectsBeforeFollowingIt()
    {
        foreach (string target in new[] { "", "bad\0target" })
        {
            Assert.Throws<IOException>(() => ProjectedTokenPathResolver.Resolve(
                "/r/link",
                candidate => candidate == "/r/link" ? target : null,
                _ => { }));
        }
    }

    // Relative configured paths start from the current directory without pre-collapsing links.
    [Fact]
    public void Given_RelativeConfiguredPath_When_Resolving_Then_UsesCurrentDirectoryAsPrefix()
    {
        string expected = Path.Combine(Directory.GetCurrentDirectory(), "synthetic-token");
        var probes = new List<string>();

        string resolved = ProjectedTokenPathResolver.Resolve(
            "synthetic-token",
            candidate =>
            {
                probes.Add(candidate);
                return null;
            },
            _ => { });

        Assert.Equal(expected, resolved);
        Assert.Equal(expected, probes[^1]);
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
            _ => throw new UnauthorizedAccessException(),
            _ => { }));
        Assert.Throws<DirectoryNotFoundException>(() => ProjectedTokenPathResolver.Resolve(
            "/r/dir/token",
            _ => null,
            _ => throw new DirectoryNotFoundException()));
        Assert.Throws<DirectoryNotFoundException>(() => ProjectedTokenPathResolver.Resolve(
            "/r/missing/token",
            _ => null,
            _ => throw new DirectoryNotFoundException()));
    }

    private static string Resolve(string path, (string Path, string Target)[] links, string[] directories)
    {
        var linkMap = links.ToDictionary(link => link.Path, link => link.Target, StringComparer.Ordinal);
        var directorySet = directories.ToHashSet(StringComparer.Ordinal);
        var probes = new List<string>();
        return ProjectedTokenPathResolver.Resolve(
            path,
            candidate =>
            {
                probes.Add(candidate);
                return linkMap.GetValueOrDefault(candidate);
            },
            candidate => Assert.Contains(candidate, directorySet));
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

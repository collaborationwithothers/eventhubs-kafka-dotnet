using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

public sealed class B10AbruptCgroupTests
{
    [Theory]
    [InlineData("oom_kill 0\n", 0)]
    [InlineData("low 0\nhigh 3\noom_kill 9223372036854775807\n", long.MaxValue)]
    public void Given_LocalCounter_When_Parse_Then_PreservesIntegralCount(string text, long expected) => Assert.Equal(expected, B10AbruptCgroup.ParseLocalCounter(text));

    [Theory]
    [InlineData("")]
    [InlineData("oom 0\n")]
    [InlineData("oom_kill -1\n")]
    [InlineData("oom_kill 1.5\n")]
    [InlineData("oom_kill 9223372036854775808\n")]
    [InlineData("oom_kill 0\noom_kill 0\n")]
    public void Given_InvalidCounter_When_Parse_Then_FailsClosed(string text) =>
        Assert.Equal("b10_abrupt_cgroup_unavailable", Assert.Throws<InvalidOperationException>(() => B10AbruptCgroup.ParseLocalCounter(text)).Message);

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Given_ResetOrIncrement_When_Compare_Then_Rejects(long after) =>
        Assert.Equal("b10_abrupt_oom_conflict", Assert.Throws<InvalidOperationException>(() => B10AbruptCgroup.Compare(new("boot", "/cg", "1:2", 1), new("boot", "/cg", "1:2", after))).Message);

    [Fact]
    public void Given_UnchangedCounterAndIdentity_When_Compare_Then_PassesOnlyExclusionCheck() =>
        Assert.True(B10AbruptCgroup.Compare(new("boot", "/cg", "1:2", 1), new("boot", "/cg", "1:2", 1)));

    [Theory]
    [InlineData("other", "/cg", "1:2")]
    [InlineData("boot", "/different", "1:2")]
    [InlineData("boot", "/cg", "1:3")]
    public void Given_ChangedIdentity_When_Compare_Then_Rejects(string boot, string path, string directory) =>
        Assert.Equal("b10_abrupt_cgroup_unavailable", Assert.Throws<InvalidOperationException>(() => B10AbruptCgroup.Compare(new("boot", "/cg", "1:2", 1), new(boot, path, directory, 1))).Message);

    [Fact]
    public void Given_CgroupMountRoot_When_Resolve_Then_UsesLocalDirectory()
    {
        Assert.Equal("/sys/fs/cgroup/pod", B10AbruptCgroup.Resolve("0::/scope/pod\n", "7 3 0:30 /scope /sys/fs/cgroup rw - cgroup2 cgroup rw\n"));
        Assert.Equal("12:34", B10AbruptCgroup.ReadDirectoryIdentity("pos:\t0\nflags:\t0100000\nmnt_id:\t12\nino:\t34\n"));
    }

    [Fact]
    public void Given_ReadOnlyDirectoryHandle_When_LocalFileChangesOrDirectoryIsReplaced_Then_BindsInodeAndLocalCounter()
    {
        if (!OperatingSystem.IsLinux()) return;
        string root = Path.Combine(Path.GetTempPath(), "b10-cgroup-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "group");
        Directory.CreateDirectory(path);
        try
        {
            File.WriteAllText(Path.Combine(path, "memory.events.local"), "oom_kill 0\n");
            var before = B10AbruptCgroup.CaptureAfter(new(B10AbruptProc.ReadBootId(), path, "unobserved", 0));
            Assert.True(B10AbruptCgroup.Compare(before, B10AbruptCgroup.CaptureAfter(before)));
            Directory.Move(path, Path.Combine(root, "retained"));
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "memory.events.local"), "oom_kill 0\n");
            var changed = B10AbruptCgroup.CaptureAfter(before);
            Assert.NotEqual(before.DirectoryIdentity, changed.DirectoryIdentity);
            Assert.Equal("b10_abrupt_cgroup_unavailable", Assert.Throws<InvalidOperationException>(() => B10AbruptCgroup.Compare(before, changed)).Message);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Given_OnlyHierarchicalCounter_When_CaptureLocal_Then_DoesNotSubstitute()
    {
        if (!OperatingSystem.IsLinux()) return;
        string root = Path.Combine(Path.GetTempPath(), "b10-cgroup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "memory.events"), "oom_kill 0\n");
            Assert.Equal("b10_abrupt_cgroup_unavailable", Assert.Throws<InvalidOperationException>(() =>
                B10AbruptCgroup.CaptureAfter(new(B10AbruptProc.ReadBootId(), root, "unobserved", 0))).Message);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("1:memory:/pod\n")]
    [InlineData("0::/scope/../escape\n")]
    [InlineData("0::/scope/pod\n0::/scope/other\n")]
    [InlineData("0::/foreign/pod\n")]
    public void Given_UnresolvedMembership_When_Resolve_Then_Rejects(string membership) =>
        Assert.Equal("b10_abrupt_cgroup_unavailable", Assert.Throws<InvalidOperationException>(() => B10AbruptCgroup.Resolve(membership, "7 3 0:30 /scope /sys/fs/cgroup rw - cgroup2 cgroup rw\n")).Message);
}

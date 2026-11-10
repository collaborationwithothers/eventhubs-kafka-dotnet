using System.Globalization;
using System.Text;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

public sealed class B10AbruptLocalCgroupCaptureTests
{
    [Fact]
    public void Given_ActualLocalMembership_When_CapturedBeforeAndAfter_Then_RealIdentityAndCounterAgree()
    {
        var capture = new B10AbruptProcessHarness.LocalCgroupCapture();
        B10AbruptCgroup.Observation before = capture.Before();
        B10AbruptCgroup.Observation after = capture.After(before);
        Assert.Equal(before, after);
        Assert.Equal(B10AbruptProc.ReadBootId(), before.BootId);
        Assert.NotEmpty(before.DirectoryIdentity);
        Assert.True(before.OomKill >= 0);
        B10AbruptProcessHarness.LocalCgroupCapture.ValidateMembership(
            Encoding.ASCII.GetString(B10AbruptProc.ReadBounded(before.CgroupPath + "/cgroup.procs")),
            Encoding.ASCII.GetString(B10AbruptProc.ReadBounded(before.CgroupPath + "/cgroup.type")), Environment.ProcessId);
    }

    [Theory]
    [InlineData("", "domain\n")]
    [InlineData("999\n", "domain\n")]
    [InlineData("42\n", "domain threaded\n")]
    [InlineData("42\n", "threaded\n")]
    [InlineData("42\n", "domain invalid\n")]
    [InlineData("42\n", "")]
    [InlineData("42\ninvalid\n", "domain\n")]
    public void Given_UnprovenDomainMembership_When_Validated_Then_FailsClosed(string members, string type) =>
        AssertUnavailable(() => B10AbruptProcessHarness.LocalCgroupCapture.ValidateMembership(members, type, 42));

    [Fact]
    public void Given_UnorderedRepeatedOwnPid_When_Validated_Then_MembershipIsProven() =>
        B10AbruptProcessHarness.LocalCgroupCapture.ValidateMembership("99\n42\n7\n42\n", "domain\n", 42);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Given_NoOrAmbiguousMemberCandidate_When_Selected_Then_FailsClosed(bool rootMember, bool initMember)
    {
        using var files = new MembershipFiles();
        string root = files.Create("root", rootMember);
        string init = files.Create("init", initMember);
        AssertUnavailable(() => B10AbruptProcessHarness.LocalCgroupCapture.CaptureCandidates(files.Process, root, init));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_ExactlyOneMemberCandidate_When_Selected_Then_UsesItsRealCounter(bool rootMember)
    {
        using var files = new MembershipFiles();
        string root = files.Create("root", rootMember, 3);
        string init = files.Create("init", !rootMember, 7);
        B10AbruptCgroup.Observation observation = B10AbruptProcessHarness.LocalCgroupCapture.CaptureCandidates(files.Process, root, init);
        Assert.Equal(rootMember ? root : init, observation.CgroupPath);
        Assert.Equal(rootMember ? 3 : 7, observation.OomKill);
        Assert.Equal(files.Process.BootId, observation.BootId);
    }

    [Theory]
    [InlineData("membership", "b10_abrupt_cgroup_unavailable")]
    [InlineData("boot", "b10_abrupt_cgroup_unavailable")]
    [InlineData("inode", "b10_abrupt_cgroup_unavailable")]
    [InlineData("observer", "b10_abrupt_cgroup_unavailable")]
    [InlineData("counter", "b10_abrupt_oom_conflict")]
    public void Given_ChangedProofAfterBefore_When_CapturedAfter_Then_Rejects(string change, string failure)
    {
        using var files = new MembershipFiles();
        string path = files.Create("group", true, 2);
        B10AbruptCgroup.Observation before = B10AbruptProcessHarness.LocalCgroupCapture.CaptureMember(path, files.Process);
        B10AbruptProcess.ProcessIdentity observer = files.Process;
        switch (change)
        {
            case "membership": File.WriteAllText(Path.Combine(path, "cgroup.procs"), ""); break;
            case "boot": before = before with { BootId = "changed" }; break;
            case "inode":
                Directory.Move(path, path + "-retained");
                _ = files.Create("group", true, 2);
                break;
            case "observer": observer = observer with { StartIdentity = observer.StartIdentity + 1 }; break;
            case "counter": File.WriteAllText(Path.Combine(path, "memory.events.local"), "oom_kill 3\n"); break;
        }
        var capture = new B10AbruptProcessHarness.LocalCgroupCapture(observer);
        Assert.Equal(failure, Assert.Throws<InvalidOperationException>(() => capture.After(before)).Message);
    }

    [Fact]
    public void Given_PathReplacedDuringProof_When_Captured_Then_ProofAndCounterStayOnOpenedInode()
    {
        using var files = new MembershipFiles();
        string path = files.Create("group", true, 2);
        B10AbruptCgroup.Observation before = B10AbruptProcessHarness.LocalCgroupCapture.CaptureMember(path, files.Process);
        int validations = 0;
        B10AbruptCgroup.Observation captured = B10AbruptCgroup.Capture(path, files.Process.BootId, descriptor =>
        {
            B10AbruptProcessHarness.LocalCgroupCapture.ValidateMembership(
                Encoding.ASCII.GetString(B10AbruptProc.ReadBounded(descriptor + "/cgroup.procs")),
                Encoding.ASCII.GetString(B10AbruptProc.ReadBounded(descriptor + "/cgroup.type")), files.Process.Pid);
            if (++validations == 1)
            {
                Directory.Move(path, path + "-retained");
                _ = files.Create("group", false, 99);
            }
        });
        Assert.Equal(2, validations);
        Assert.Equal(before, captured);
    }

    [Fact]
    public void Given_NamespaceMountMismatch_When_ProductionResolves_Then_StillRejectsUnknownRoot()
    {
        const string mount = "60 55 0:24 /.. /sys/fs/cgroup rw - cgroup2 cgroup2 rw\n";
        Assert.True(B10AbruptProcessHarness.LocalCgroupCapture.IsNamespaceMismatch("0::/\n", mount));
        AssertUnavailable(() => B10AbruptCgroup.Resolve("0::/\n", mount));
        Assert.False(B10AbruptProcessHarness.LocalCgroupCapture.IsNamespaceMismatch("0::/foreign\n", mount));
        Assert.False(B10AbruptProcessHarness.LocalCgroupCapture.IsNamespaceMismatch("0::/\n", mount + mount));
        Assert.False(B10AbruptProcessHarness.LocalCgroupCapture.IsNamespaceMismatch("0::/\n", mount.Replace("/..", "/scope", StringComparison.Ordinal)));
    }

    private static void AssertUnavailable(Action action) =>
        Assert.Equal("b10_abrupt_cgroup_unavailable", Assert.Throws<InvalidOperationException>(action).Message);

    private sealed class MembershipFiles : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "b10-membership-" + Guid.NewGuid().ToString("N"));
        internal B10AbruptProcess.ProcessIdentity Process { get; } = B10AbruptProcess.ReadIdentity(Environment.ProcessId);
        internal string Create(string name, bool member, long counter = 0)
        {
            string path = Path.Combine(root, name);
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "cgroup.type"), "domain\n");
            File.WriteAllText(Path.Combine(path, "cgroup.procs"), member ? Process.Pid.ToString(CultureInfo.InvariantCulture) + "\n" : "");
            File.WriteAllText(Path.Combine(path, "memory.events.local"), "oom_kill " + counter.ToString(CultureInfo.InvariantCulture) + "\n");
            return path;
        }
        public void Dispose() => Directory.Delete(root, true);
    }
}

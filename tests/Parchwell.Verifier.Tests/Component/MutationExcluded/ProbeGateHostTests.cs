// This file sits under Component/ only so the default mutation-only build drops it (the csproj removes Component/**/*.cs).
// The tests still run in the normal build and in the full-suite mutation build. Why: docs/operations/verifier-mutation.md, section "Issue 602 fast-suite equivalence check".
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Parchwell.Verifier.ProbeGate;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.ProbeGate;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed partial class ProbeGateHostTests
{
    [Fact]
    public async Task Given_SynchronousBlockedGateRead_When_WatchdogExpires_Then_ProductionHostExits124()
    {
        using var fixture = new ProbeGateEvidenceBuilder();
        ProcessStartInfo start = TestChildStartInfo();
        start.Environment["PARCHWELL_PROBE_GATE_CHILD"] = fixture.Root;
        using var child = Process.Start(start)!;
        Task<string> stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(20));
        try { await child.WaitForExitAsync(limit.Token); }
        finally { if (!child.HasExited) child.Kill(entireProcessTree: true); }
        string log = await stdout + await stderr;
        Assert.True(child.ExitCode == 124, log);
        Assert.False(File.Exists(Path.Combine(fixture.Root, "blocked-gate.json")));
    }
}
#pragma warning restore CA1707

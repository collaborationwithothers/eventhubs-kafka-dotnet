using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Telemetry;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Input;
using Parchwell.LoadGen.Run.Publication;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.

// ADR-021: "loadgen2" is B10's second Job. B10 has no phases yet, so A01 identities reach the Job name check.
public sealed class RestartJobIdentityTests
{
    [Theory]
    [InlineData("child-run-c0-r1-loadgen2", "restart_instance_requires_b10")]
    [InlineData("child-run-c0-r1-loadgen3", "job_name_must_match_run_phase_repeat")]
    public void Given_A01WorkerWithAnotherJobName_When_ValidatingHostOptions_Then_RejectsIt(string jobName, string error)
    {
        string[] args = LoadGenHostProcessTests.CreateInputs(Path.Combine(Path.GetTempPath(), "restart-" + Guid.NewGuid().ToString("N")))
            .Select(pair => "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal) + "=" + pair.Value).ToArray();
        using IHost host = Program.BuildHostCore(args, _ => { }, (_, configure) => configure(new ParchwellTelemetryOptions()));
        LoadGenHostOptions options = host.Services.GetRequiredService<IOptions<LoadGenHostOptions>>().Value;

        options.JobName = jobName;

        Assert.Equal(error, Assert.Throws<InvalidOperationException>(options.Validate).Message);
    }

    [Theory]
    [InlineData("run-1-c3-r5-loadgen", null, null)]
    [InlineData("run-1-c3-r5-loadgen2", null, "restart_instance_requires_b10")]
    [InlineData("run-1-c3-r5-loadgen", "pod-zero", "producer_run_context_predecessor_invalid")]
    public void Given_A01RunContext_When_Validating_Then_OnlyItsFirstJobWithoutPredecessorPasses(string jobName, string? predecessor, string? error)
    {
        var context = new ProducerRunContext(new RunIdentity("A01", "C3", "run-1", "pod-one", "pod-one", "pod-one"), "C3", 5, jobName,
            "pod-one", "pod-uid-1", new string('a', 40)) { PredecessorInstanceId = predecessor };

        Assert.Equal("run-1-c3-r5-loadgen", context.FirstJobName);
        Assert.Equal(jobName.EndsWith('2'), context.IsRestartInstance);
        Assert.Equal(error, Record.Exception(context.Validate)?.Message);
    }
}

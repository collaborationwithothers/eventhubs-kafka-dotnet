using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Consumer;

[Collection("VerifierEnvironment")]
#pragma warning disable CA1707 // Repository test naming convention.
public sealed class HostOptionsTests
{
    public static TheoryData<string, bool> PlanCases => new()
    {
        { "kill-zero", false }, { "kill-one", true }, { "kill-million", true }, { "kill-above-million", false },
        { "deadline-below", false }, { "deadline-minimum", true }, { "deadline-maximum", true }, { "deadline-above", false },
        { "reserved-subject-setting", false }, { "witness-missing-isolation", false }, { "different-isolation", false },
        { "no-plan", false }, { "no-source", false },
        { "lifetime-below", false }, { "lifetime-equal", false }, { "lifetime-above", true }
    };

    [Theory]
    [MemberData(nameof(PlanCases))]
    public async Task Given_ConsumerPlanBoundary_U15_When_Starting_Then_RejectsInvalidPlanBeforeClients(string input, bool valid)
    {
        VerifierHostOptions options = ConsumerOptions();
        ConsumerVerificationPlan plan = Plan();
        switch (input)
        {
            case "kill-zero": plan = plan with { KillCount = 0 }; break;
            case "kill-one": plan = plan with { KillCount = 1 }; break;
            case "kill-million": plan = plan with { KillCount = 1000000 }; break;
            case "kill-above-million": plan = plan with { KillCount = 1000001 }; break;
            case "deadline-below": plan = plan with { CompletionDeadlineMs = 999 }; break;
            case "deadline-minimum": plan = plan with { CompletionDeadlineMs = 1000 }; break;
            case "deadline-maximum": plan = plan with { CompletionDeadlineMs = 3600000 }; break;
            case "deadline-above": plan = plan with { CompletionDeadlineMs = 3600001 }; break;
            case "reserved-subject-setting": plan = plan with { SubjectSettings = new Dictionary<string, string> { ["group.id"] = "synthetic-group", ["isolation.level"] = "read_committed" } }; break;
            case "witness-missing-isolation": plan = plan with { WitnessSettings = new Dictionary<string, string>() }; break;
            case "different-isolation": plan = plan with { WitnessSettings = new Dictionary<string, string> { ["isolation.level"] = "read_uncommitted" } }; break;
            case "lifetime-below": options.ProcessLifetime = RequiredBudget(options) + TimeSpan.FromMilliseconds(600000) - TimeSpan.FromTicks(1); break;
            case "lifetime-equal": options.ProcessLifetime = RequiredBudget(options) + TimeSpan.FromMilliseconds(600000); break;
            case "lifetime-above": options.ProcessLifetime = RequiredBudget(options) + TimeSpan.FromMilliseconds(600000) + TimeSpan.FromTicks(1); break;
        }
        bool absent = input is "no-source" or "no-plan";
        Assert.Equal(valid, ConsumerPlanValidation.IsValid(absent ? null : plan, options));
        var constructed = 0;
        void Configure(IServiceCollection services)
        {
            services.RemoveAll<IConsumerVerificationPlanSource>();
            if (input != "no-source") services.AddSingleton<IConsumerVerificationPlanSource>(new TestPlanSource(input == "no-plan" ? null : plan));
            GuardClients(services, () => constructed++);
        }
        string[] args = Arguments(options);
        if (valid)
        {
            using IHost host = Program.BuildHost(args, Configure);
            Assert.Equal(plan, host.Services.GetRequiredService<ConsumerVerificationPlan>());
            Assert.Equal(0, constructed);
            return;
        }
        OptionsValidationException failure = Assert.Throws<OptionsValidationException>(() => Program.BuildHost(args, Configure));
        Assert.Contains("consumer_plan_invalid", failure.Failures);
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        TextWriter previous = Console.Error;
        try
        {
            Console.SetError(errors);
            Assert.Equal(1, await Program.RunAsync(args, Configure));
        }
        finally { Console.SetError(previous); }
        Assert.Contains("consumer_plan_invalid", errors.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(0, constructed);
    }

    public static TheoryData<string, string, string, string, string, bool> ModeCases => new()
    {
        { "consumer-verify", "B10", "C0", "C0", "consumer-record", false },
        { "consumer-verify", "C03", "C3", "C3", "consumer-record", false },
        { "consumer-verify", "C03", "C2", "C2", "consumer-record", false },
        { "consumer-verify", "C03", "probe", "probe", "consumer-record", false },
        { "consumer-verify", "C03", "c1", "c1", "consumer-record", false },
        { "consumer-verify", "C03", "C1", "C0", "consumer-record", false },
        { "consumer-verify", "C03", "C0", "C0", "ledger", false },
        { "verify", "C03", "C0", "C0", "consumer-record", false },
        { "consumer-verify", "C03", "C0", "C0", "consumer-record", true }
    };

    [Theory]
    [MemberData(nameof(ModeCases))]
    public async Task Given_ConsumerModeIdentity_U16_When_Starting_Then_OnlyTheApprovedPairPasses(string mode, string scenario,
        string phase, string configuration, string container, bool valid)
    {
        VerifierHostOptions options = ConsumerOptions();
        options.Mode = mode; options.ScenarioId = scenario; options.Phase = phase;
        options.ConfigurationId = configuration; options.ConsumerRecordContainer = container;
        string lower = phase.ToLowerInvariant();
        options.JobName = $"run-1-{lower}-r1-verify";
        options.ExpectedInstanceId = $"run-1-{lower}-r1-loadgen";
        options.RepeatPrefix = $"run-1/{phase}/r1/";
        options.PhasePrefix = $"run-1/{phase}/";
        options.ArtifactPrefix = $"run-1/{phase}/r1/producer/";
        options.StartLineInputName = $"run-1/{phase}/r1/start-line.json";
        options.ResultOutputName = $"run-1/{phase}/r1/result.json";
        ValidateOptionsResult result = new VerifierHostOptionsValidator().Validate(null, options);
        Assert.Equal(valid, result.Succeeded);
        if (!valid)
        {
            string expectedFailure = mode == "verify" || scenario == "B10"
                ? mode == "verify" ? "Verifier:ScenarioId" : "Consumer verify requires"
                : container != "consumer-record" ? "Verifier:ConsumerRecordContainer" : "Verifier:ConfigurationId";
            Assert.Contains(result.Failures ?? [], failure => failure.Contains(expectedFailure, StringComparison.Ordinal));
        }
        var constructed = 0;
        void Configure(IServiceCollection services)
        {
            services.AddSingleton<IConsumerVerificationPlanSource>(new TestPlanSource(Plan()));
            GuardClients(services, () => constructed++);
        }
        if (valid)
        {
            using IHost host = Program.BuildHost(Arguments(options), Configure);
            Assert.NotNull(host.Services.GetRequiredService<ConsumerVerificationPlan>());
        }
        else Assert.Equal(1, await Program.RunAsync(Arguments(options), Configure));
        Assert.Equal(0, constructed);
    }

    private static ConsumerVerificationPlan Plan() => new(null, 600000,
        new Dictionary<string, string> { ["isolation.level"] = "read_committed" },
        new Dictionary<string, string> { ["isolation.level"] = "read_committed" });

    private static VerifierHostOptions ConsumerOptions()
    {
        VerifierHostOptions options = VerifierHostTestOptions.ValidOptions("verify");
        options.Mode = "consumer-verify"; options.ScenarioId = "C03";
        options.ConsumerRecordContainer = "consumer-record";
        options.ProcessLifetime = RequiredBudget(options) + TimeSpan.FromMilliseconds(3600000) + TimeSpan.FromTicks(1);
        return options;
    }

    private static TimeSpan RequiredBudget(VerifierHostOptions options) => options.CleanupTimeout!.Value + options.IoTimeout!.Value +
        options.TokenTimeout!.Value + options.MetadataTimeout!.Value + options.WatermarkTimeout!.Value +
        options.BlobIoTimeout!.Value + options.SafetyStop!.Value;

    private static string[] Arguments(VerifierHostOptions options) => VerifierHostTestOptions.ArgumentsFor("verify", "unused.json").Concat(
    [
        $"--Verifier:Mode={options.Mode}", $"--Verifier:ScenarioId={options.ScenarioId}",
        $"--Telemetry:ScenarioId={options.ScenarioId}", $"--Verifier:Phase={options.Phase}",
        $"--Verifier:ConfigurationId={options.ConfigurationId}", $"--Verifier:JobName={options.JobName}",
        $"--Verifier:ExpectedInstanceId={options.ExpectedInstanceId}", $"--Verifier:RepeatPrefix={options.RepeatPrefix}",
        $"--Verifier:PhasePrefix={options.PhasePrefix}", $"--Verifier:ArtifactPrefix={options.ArtifactPrefix}",
        $"--Verifier:StartLineInputName={options.StartLineInputName}", $"--Verifier:ResultOutputName={options.ResultOutputName}",
        $"--Verifier:ConsumerRecordContainer={options.ConsumerRecordContainer}",
        $"--Verifier:ProcessLifetime={options.ProcessLifetime!.Value.ToString("c", CultureInfo.InvariantCulture)}"
    ]).ToArray();

    private static void GuardClients(IServiceCollection services, Action constructed)
    {
        services.AddSingleton<VerifierKafkaSessionFactory>(_ => { constructed(); throw new InvalidOperationException("Unexpected Kafka construction."); });
        services.AddSingleton<IVerifierArtifactStore>(_ => { constructed(); throw new InvalidOperationException("Unexpected verifier Blob construction."); });
        services.AddSingleton<IProducerArtifactStore>(_ => { constructed(); throw new InvalidOperationException("Unexpected producer Blob construction."); });
        services.AddSingleton<ConsumerArtifactReader>(_ => { constructed(); throw new InvalidOperationException("Unexpected consumer Blob construction."); });
    }

    private sealed class TestPlanSource(ConsumerVerificationPlan? plan) : IConsumerVerificationPlanSource
    {
        public bool TryGetPlan(string scenarioId, string configurationId, out ConsumerVerificationPlan? found)
        { found = scenarioId == "C03" && configurationId == "C0" ? plan : null; return found is not null; }
    }
}
#pragma warning restore CA1707

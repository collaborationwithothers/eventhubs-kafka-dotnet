using System.Collections;
using Azure.Identity;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Telemetry;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.SingleActionV2;

internal static class ProbeV2Host
{
    internal static IHost BuildHost(Action<IServiceCollection>? configureServices = null, bool telemetry = true,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        environment ??= EnvironmentValues();
        ProbeV2Options options = ProbeV2Options.Read(environment);
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.Configure<ConsoleLifetimeOptions>(value => value.SuppressStatusMessages = true);
        builder.Services.AddOptions<ProbeV2Options>().Configure(value => Copy(options, value))
            .Validate(value => { value.Validate(); return true; }).ValidateOnStart();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.Configure<HostOptions>(value => value.ShutdownTimeout = TimeSpan.FromSeconds(options.Limits.HostCleanupSeconds));
        builder.Services.AddParchwellScenarios(value =>
        {
            value.MaxDocumentCharacters = options.ScenarioLoadOptions.MaxDocumentCharacters;
            value.MaxNestingDepth = options.ScenarioLoadOptions.MaxNestingDepth;
            value.MaxScalarCharacters = options.ScenarioLoadOptions.MaxScalarCharacters;
            value.MaxParserEvents = options.ScenarioLoadOptions.MaxParserEvents;
        });
        builder.Services.AddSingleton<IScenarioSource>(_ => new ProbeV2ScenarioSource(options.ScenarioRoot));
        foreach (KafkaClientRole role in Enum.GetValues<KafkaClientRole>())
            builder.Services.AddParchwellKafka(role, value =>
            {
                value.NamespaceName = options.NamespaceName;
                value.ClientId = options.PodName;
                value.EnrichmentGroupId = $"probe-v2-{options.RunId}-{options.ScenarioId.ToLowerInvariant()}-{options.ConfigurationId.ToLowerInvariant()}";
            });
        builder.Services.AddParchwellKafkaAuthentication(value =>
        {
            value.TenantId = environment["AZURE_TENANT_ID"];
            value.ClientId = environment["AZURE_CLIENT_ID"];
            value.TokenFilePath = environment["AZURE_FEDERATED_TOKEN_FILE"];
        });
        if (telemetry)
            builder.Services.AddParchwellTelemetry(new WorkloadIdentityCredential(), value =>
            {
                value.ConnectionString = environment.GetValueOrDefault("Telemetry__ConnectionString") ?? "";
                value.ServiceName = "Parchwell.LoadGen";
                value.ServiceInstanceId = options.PodName;
                value.ScenarioId = options.ScenarioId;
                value.RunId = options.RunId;
            });
        builder.Services.AddSingleton(provider => new ProbeV2SessionFactory(provider.GetRequiredService<KafkaAuthenticationFactory>(), options));
        builder.Services.AddSingleton(provider => new ProbeV2Inputs(options, provider.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton<ProbeV2Worker>();
        configureServices?.Invoke(builder.Services);
        return builder.Build();
    }
    internal static async Task<int> RunAsync(Action<IServiceCollection>? configureServices = null, Func<IHost>? hostFactory = null,
        TextWriter? output = null, TextWriter? logs = null)
    {
        output ??= Console.Out;
        logs ??= Console.Error;
        IHost? host = null;
        ProbeV2Options? options = null;
        ProbeV2Worker? worker = null;
        ITimer? watchdog = null;
        TimeProvider clock = TimeProvider.System;
        int exit = 1;
        bool hostCleanup = true;
        try
        {
            host = hostFactory?.Invoke() ?? BuildHost(configureServices);
            options = host.Services.GetRequiredService<IOptions<ProbeV2Options>>().Value;
            clock = host.Services.GetRequiredService<TimeProvider>();
            watchdog = clock.CreateTimer(_ => Environment.Exit(124), null, TimeSpan.FromSeconds(options.ProcessLifetimeSeconds), Timeout.InfiniteTimeSpan);
            await host.StartAsync().ConfigureAwait(false);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.ProcessLifetimeSeconds), clock);
            using var stopping = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
            LoadedScenario loaded = await host.Services.GetRequiredService<ScenarioLoader>().LoadAsync(options.ScenarioId, stopping.Token).ConfigureAwait(false);
            ResolvedScenario resolved = host.Services.GetRequiredService<ScenarioResolver>().Resolve(loaded);
            if (resolved.Definition.ProbeV2 is null || !resolved.Definition.ProbeV2.Configurations.ContainsKey(options.ConfigurationId))
                throw new InvalidOperationException("v2_declaration");
            worker = host.Services.GetRequiredService<ProbeV2Worker>();
            await worker.ExecuteAsync(resolved, output, logs, stopping.Token).ConfigureAwait(false);
            exit = worker.CleanupSucceeded && !worker.Repeats.Any(value => value.Diagnostics.Contains("capture_limit", StringComparer.Ordinal)) ? 0 : 1;
        }
        catch (Exception exception) { await ReportFailure(logs, "probe_v2_failed", exception, options).ConfigureAwait(false); }
        finally
        {
            if (host is not null)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(options?.Limits.HostCleanupSeconds ?? 10), clock);
                try { await host.StopAsync(cleanup.Token).WaitAsync(cleanup.Token).ConfigureAwait(false); }
                catch (Exception exception) { hostCleanup = false; await ReportFailure(logs, "probe_v2_host_stop_failed", exception, options).ConfigureAwait(false); }
                try { await Task.Run(host.Dispose).WaitAsync(cleanup.Token).ConfigureAwait(false); }
                catch (Exception exception) { hostCleanup = false; await ReportFailure(logs, "probe_v2_host_dispose_failed", exception, options).ConfigureAwait(false); }
            }
        }
        try { watchdog?.Dispose(); }
        catch (Exception exception) { hostCleanup = false; await ReportFailure(logs, "probe_v2_watchdog_cleanup_failed", exception, options).ConfigureAwait(false); }
        if (!hostCleanup) exit = 1;
        if (worker is not null && options is not null && worker.Repeats.Count != 0)
        {
            try
            {
                ProbeV2Stage stage = ProbeV2Stage.For(options.ScenarioId, options.ConfigurationId, options.Stage);
                while (worker.Repeats.Count < 5) worker.Repeats.Add(ProbeV2Wire.Skipped(worker.Repeats.Count + 1, stage.Action, "not_attempted_cleanup"));
                var capture = new ProbeV2CaptureFile(2, "single-action-v2", true, options.RunId, options.ScenarioId, options.ConfigurationId,
                    options.Stage, options.SourceCommit, options.JobName, options.PodName, options.PodUid, options.ServiceAccount,
                    typeof(ProducerConfig).Assembly.GetName().Version!.ToString(), Library.VersionString, worker.Repeats,
                    exit, worker.CleanupSucceeded, hostCleanup);
                await worker.WriteAsync(output, "PARCHWELL_PROBE_V2_CAPTURE ", capture, options.Limits.MaxCaptureBytes, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) { exit = 1; await ReportFailure(logs, "probe_v2_capture_failed", exception, options).ConfigureAwait(false); }
        }
        return exit;
    }
    private static Task ReportFailure(TextWriter logs, string operation, Exception exception, ProbeV2Options? options) =>
        logs.WriteLineAsync($"{operation}:{exception.GetType().Name} scenario_id={options?.ScenarioId ?? "unknown"} run_id={options?.RunId ?? "unknown"} configuration_id={options?.ConfigurationId ?? "unknown"} stage={options?.Stage ?? "unknown"}" +
            (exception is InvalidDataException && exception.Data["observed_bytes"] is int observed && exception.Data["allowed_bytes"] is int allowed
                ? FormattableString.Invariant($" limit=MaxSerializedBytes observed={observed} allowed={allowed}") : ""));
    internal static int ValidateConfiguration()
    {
        try
        {
            _ = ProbeV2Options.Read(EnvironmentValues());
            Console.Out.WriteLine("{\"single_action_v2_configuration_valid\":true}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("single_action_v2_configuration_invalid:" + exception.GetType().Name);
            return 1;
        }
    }
    internal static Dictionary<string, string> EnvironmentValues() => Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
        .ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!, StringComparer.Ordinal);
    private static void Copy(ProbeV2Options source, ProbeV2Options target)
    {
        foreach (System.Reflection.PropertyInfo property in typeof(ProbeV2Options).GetProperties()) property.SetValue(target, property.GetValue(source));
    }
}

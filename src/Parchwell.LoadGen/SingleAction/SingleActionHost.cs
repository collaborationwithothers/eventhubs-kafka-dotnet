using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Telemetry;
using Parchwell.LoadGen.Common.SingleAction;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.SingleAction;

internal static class SingleActionHost
{
    internal static IHost BuildHost(Action<IServiceCollection>? configureServices = null, bool registerTelemetry = true)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.Configure<ConsoleLifetimeOptions>(value => value.SuppressStatusMessages = true);
        IConfigurationSection section = builder.Configuration.GetSection("SingleAction");
        var options = new SingleActionHostOptions();
        section.Bind(options, value => value.ErrorOnUnknownConfiguration = true);
        options.Validate();
        foreach ((string name, string expected) in new[] { ("SOURCE_COMMIT", options.SourceCommit), ("JOB_NAME", options.JobName), ("POD_NAME", options.PodName), ("POD_UID", options.PodUid) })
            if (builder.Configuration["PARCHWELL_" + name] is string actual && !StringComparer.Ordinal.Equals(actual, expected))
                throw new InvalidOperationException("single_action_deployment_identity_mismatch");
        builder.Services.AddOptions<SingleActionHostOptions>().Bind(section, value => value.ErrorOnUnknownConfiguration = true)
            .Validate(value => { value.Validate(); return true; }).ValidateOnStart();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton(options);
        builder.Services.Configure<HostOptions>(value => value.ShutdownTimeout = TimeSpan.FromSeconds(options.HostCleanupSeconds));
        builder.Services.AddParchwellScenarios(value =>
        {
            value.MaxDocumentCharacters = options.ScenarioLoadOptions.MaxDocumentCharacters;
            value.MaxNestingDepth = options.ScenarioLoadOptions.MaxNestingDepth;
            value.MaxScalarCharacters = options.ScenarioLoadOptions.MaxScalarCharacters;
            value.MaxParserEvents = options.ScenarioLoadOptions.MaxParserEvents;
        });
        builder.Services.AddSingleton<IScenarioSource>(_ => new SingleActionScenarioSource(options.ScenarioRoot));
        // A definition can declare more than one role. Resolution needs each declared baseline,
        // while the selected row constructs only its own single client role.
        foreach (KafkaClientRole role in Enum.GetValues<KafkaClientRole>())
            builder.Services.AddParchwellKafka(role, value =>
            {
                value.NamespaceName = options.NamespaceName;
                value.ClientId = options.PodName;
                value.EnrichmentGroupId = $"single-action-{options.RunId}-{options.ScenarioId.ToLowerInvariant()}-{options.ConfigurationId.ToLowerInvariant()}";
            });
        builder.Services.AddParchwellKafkaAuthentication(value =>
        {
            value.TenantId = builder.Configuration["AZURE_TENANT_ID"];
            value.ClientId = builder.Configuration["AZURE_CLIENT_ID"];
            value.TokenFilePath = builder.Configuration["AZURE_FEDERATED_TOKEN_FILE"];
            value.TokenScopeOverride = options.ScenarioId == "F04" && options.ConfigurationId == "C3" ? "https://storage.azure.com/.default" : null;
        });
        if (registerTelemetry)
            builder.Services.AddParchwellTelemetry(new WorkloadIdentityCredential(), value =>
            {
                value.ConnectionString = builder.Configuration["Telemetry:ConnectionString"] ?? "";
                value.ServiceName = "Parchwell.LoadGen";
                value.ServiceInstanceId = options.PodName;
                value.ScenarioId = options.ScenarioId;
                value.RunId = options.RunId;
            });
        builder.Services.AddSingleton<ISingleActionPublication>(provider => new SingleActionPublication(
            new BlobServiceClient(new Uri(options.BlobEndpoint), new WorkloadIdentityCredential(), new BlobClientOptions { Retry = { MaxRetries = 0 } }),
            provider.GetRequiredService<SingleActionHostOptions>()));
        builder.Services.AddSingleton(provider => new SingleActionSessionFactory(provider.GetRequiredService<KafkaAuthenticationFactory>(), options,
            SelectedRole(options)));
        builder.Services.AddSingleton<SingleActionWorker>();
        configureServices?.Invoke(builder.Services);
        return builder.Build();
    }

    internal static async Task<int> RunAsync(Action<IServiceCollection>? configureServices = null, Func<IHost>? hostFactory = null)
    {
        IHost? host = null;
        SingleActionHostOptions? options = null;
        TimeProvider clock = TimeProvider.System;
        ISingleActionPublication? publication = null;
        ITimer? watchdog = null;
        bool captureComplete = false;
        int exit = 1;
        try
        {
            host = hostFactory?.Invoke() ?? BuildHost(configureServices);
            options = host.Services.GetRequiredService<IOptions<SingleActionHostOptions>>().Value;
            clock = host.Services.GetRequiredService<TimeProvider>();
            publication = host.Services.GetRequiredService<ISingleActionPublication>();
            watchdog = clock.CreateTimer(_ => Environment.Exit(124), null, TimeSpan.FromSeconds(options.ProcessLifetimeSeconds), Timeout.InfiniteTimeSpan);
            await host.StartAsync().ConfigureAwait(false);
            using var process = new CancellationTokenSource(TimeSpan.FromSeconds(options.ProcessLifetimeSeconds), clock);
            using var stopping = CancellationTokenSource.CreateLinkedTokenSource(process.Token, host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
            LoadedScenario loaded = await host.Services.GetRequiredService<ScenarioLoader>().LoadAsync(options.ScenarioId, stopping.Token).ConfigureAwait(false);
            ResolvedScenario resolved = host.Services.GetRequiredService<ScenarioResolver>().Resolve(loaded);
            if (resolved.Definition.SingleAction is null || !resolved.Definition.SingleAction.Configurations.ContainsKey(options.ConfigurationId))
                throw new InvalidOperationException("single_action_declaration_missing");
            await host.Services.GetRequiredService<SingleActionWorker>().ExecuteAsync(resolved, stopping.Token).ConfigureAwait(false);
            captureComplete = true;
            exit = 0;
        }
        catch (Exception exception) { ReportFailure("single_action_failed", exception, options); }
        finally
        {
            if (host is not null)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(options?.HostCleanupSeconds ?? 10), clock);
                try { await host.StopAsync(cleanup.Token).WaitAsync(cleanup.Token).ConfigureAwait(false); }
                catch (Exception exception) { ReportFailure("single_action_host_stop_failed", exception, options); exit = 1; }
                try { await Task.Run(host.Dispose).WaitAsync(cleanup.Token).ConfigureAwait(false); }
                catch (Exception exception) { ReportFailure("single_action_host_dispose_failed", exception, options); exit = 1; }
            }
        }
        try { watchdog?.Dispose(); }
        catch (Exception exception) { ReportFailure("single_action_watchdog_cleanup_failed", exception, options); exit = 1; }
        // Retained independent publication state intentionally outlives the disposed DI host.
        if (captureComplete && exit == 0 && publication is not null && options is not null)
        {
            try
            {
                using var receipt = new CancellationTokenSource(TimeSpan.FromSeconds(options.ReceiptUploadSeconds), clock);
                await publication.WriteAsync(SingleActionPublication.Prefix(options) + $"instances/{options.PodName}/completion.json",
                    SingleActionWire.Encode(SingleActionPublication.Completion(options), options.MaxCompletionBytes), receipt.Token).WaitAsync(receipt.Token).ConfigureAwait(false);
            }
            catch (Exception exception) { ReportFailure("single_action_completion_failed", exception, options); exit = 1; }
        }
        return exit;
    }
    private static void ReportFailure(string operation, Exception exception, SingleActionHostOptions? options) =>
        Console.Error.WriteLine($"{operation}:{exception.GetType().Name} scenario_id={options?.ScenarioId ?? "unknown"} run_id={options?.RunId ?? "unknown"} configuration_id={options?.ConfigurationId ?? "unknown"}");

    internal static int ValidateConfiguration()
    {
        try
        {
            using IHost host = BuildHost(registerTelemetry: false);
            _ = host.Services.GetRequiredService<IOptions<SingleActionHostOptions>>().Value;
            Console.Out.WriteLine("{\"single_action_configuration_valid\":true}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("single_action_configuration_invalid:" + exception.GetType().Name + ":" + exception.Message);
            return 1;
        }
    }
    private static KafkaClientRole SelectedRole(SingleActionHostOptions options) => options.ScenarioId switch
    {
        "C08" or "C12" => KafkaClientRole.EnrichmentConsumer,
        "F04" when options.ConfigurationId is "C2" or "C4" => KafkaClientRole.VerifierConsumer,
        _ => KafkaClientRole.LoadGenProducer
    };
}

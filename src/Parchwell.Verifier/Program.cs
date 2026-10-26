using Azure.Identity;
using Azure.Storage.Blobs;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Telemetry;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Scenario;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.StartLine;
using Parchwell.Verifier.ProbeGate;
using Parchwell.Verifier.VerifyRun;
using Parchwell.Verifier.VerifyRun.Storage;
using Parchwell.Verifier.VerifyRun.Configuration;
using Parchwell.Verifier.Judge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Parchwell.Verifier;

/// <summary>Composes and runs the selected verifier mode as a Generic Host application.</summary>
internal static partial class Program
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Verifier host failed for scenario {ScenarioId} run {RunId}.")]
    private static partial void LogHostFailed(ILogger logger, Exception exception, string? scenarioId, string? runId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Verifier startup failed for scenario {ScenarioId} run {RunId}.")]
    private static partial void LogStartupFailed(ILogger logger, Exception exception, string? scenarioId, string? runId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Verifier cleanup failed for scenario {ScenarioId} run {RunId}.")]
    private static partial void LogCleanupFailed(ILogger logger, Exception exception, string? scenarioId, string? runId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Verifier disposal failed for scenario {ScenarioId} run {RunId}.")]
    private static partial void LogDisposalFailed(ILogger logger, Exception exception, string? scenarioId, string? runId);

    public static Task<int> Main(string[] args) => args.Length==1&&args[0]=="--validate-single-action-configuration"
        ? Task.FromResult(ValidateSingleActionConfiguration())
        : args.Length == 1 && args[0] == "--image-smoke"
        ? ImageSmoke.RunAsync(Console.Out, AppContext.BaseDirectory)
        : RunAsync(args);

    private static int ValidateSingleActionConfiguration()
    {
        try
        {
            var builder=Host.CreateApplicationBuilder([]);
            var options=new VerifierHostOptions();new VerifierHostOptionsSetup(builder.Configuration).Configure(options);
            if(options.Mode!="single-action-judge"||!new VerifierHostOptionsValidator().Validate(null,options).Succeeded)return 2;
            foreach(var entry in builder.Configuration.GetSection("Verifier").AsEnumerable())
            {
                if(entry.Value is null)continue;
                string[] segments=entry.Key.Split(':')[1..];
                Type type=typeof(VerifierHostOptions);
                foreach(string segment in segments)
                {
                    var property=type.GetProperty(segment,System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.IgnoreCase);
                    if(property is null||!property.CanWrite)return 2;
                    type=property.PropertyType;
                }
            }
            Console.WriteLine("{\"single_action_judge_configuration_valid\":true}");
            return 0;
        }
        catch(Exception){return 2;}
    }

    internal static IHost BuildHost(
        string[] args,
        Action<IServiceCollection>? configureForTests = null) =>
        BuildHostCore(args, configureForTests, timeProvider: null);

    private static IHost BuildHostCore(
        string[] args,
        Action<IServiceCollection>? configureForTests,
        TimeProvider? timeProvider)
    {
        ArgumentNullException.ThrowIfNull(args);

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services
            .AddOptions<VerifierHostOptions>()
            .ValidateOnStart();
        builder.Services.AddSingleton<IConfigureOptions<VerifierHostOptions>>(
            new VerifierHostOptionsSetup(builder.Configuration));
        builder.Services.AddSingleton<IValidateOptions<VerifierHostOptions>, VerifierHostOptionsValidator>();
        builder.Services.AddSingleton(timeProvider ?? TimeProvider.System);
        builder.Services.AddSingleton<VerifierExecutionState>();
        builder.Services.AddSingleton<VerifierCompletionState>();
        builder.Services.AddSingleton<ICompletionReceiptPublisher, BlobCompletionReceiptPublisher>();

        RegisterSelectedMode(builder.Services, builder.Configuration);
        configureForTests?.Invoke(builder.Services);
        builder.Services.AddHostedService<VerifierWorker>();
        return builder.Build();
    }

    private static void RegisterSelectedMode(IServiceCollection services, ConfigurationManager configuration)
    {
        string? mode = configuration["Verifier:Mode"];
        if (mode == "probe-gate")
        {
            services.AddOptions<ScenarioLoadOptions>()
                .Configure(options => configuration.GetSection("Verifier:ScenarioLoadOptions").Bind(options))
                .Validate(options => options.MaxDocumentCharacters > 0, "MaxDocumentCharacters must be positive.")
                .Validate(options => options.MaxNestingDepth > 0, "MaxNestingDepth must be positive.")
                .Validate(options => options.MaxScalarCharacters > 0, "MaxScalarCharacters must be positive.")
                .Validate(options => options.MaxParserEvents > 0, "MaxParserEvents must be positive.")
                .ValidateOnStart();
            services.AddSingleton<ScenarioLoader>();
            services.AddSingleton<IScenarioSource>(provider => new FileSystemScenarioSource(
                provider.GetRequiredService<IOptions<VerifierHostOptions>>().Value.ScenarioRoot!));
            services.AddSingleton<IProbeGateFileStore, LocalProbeGateFileStore>();
            services.AddSingleton<IVerifierModeHandler, ProbeGateHandler>();
            return;
        }
        if(mode=="single-action-judge")
        {
            var selected=new VerifierHostOptions();new VerifierHostOptionsSetup(configuration).Configure(selected);
            var validation=new VerifierHostOptionsValidator().Validate(null,selected);
            if(!validation.Succeeded)throw new OptionsValidationException(Options.DefaultName,typeof(VerifierHostOptions),validation.Failures??[]);
            RegisterTelemetry(services,configuration);
            services.AddOptions<ScenarioLoadOptions>().Configure(options=>configuration.GetSection("Verifier:ScenarioLoadOptions").Bind(options));
            services.AddSingleton<ScenarioLoader>();
            services.AddSingleton<IScenarioSource>(provider=>new FileSystemScenarioSource(provider.GetRequiredService<IOptions<VerifierHostOptions>>().Value.ScenarioRoot!));
            services.AddSingleton<IProducerArtifactStore>(provider=>new BlobProducerArtifactStore(CreateBlobContainer(selected,selected.CaptureContainer!),134217728));
            services.AddSingleton<IVerifierArtifactStore>(provider=>new BlobVerifierArtifactStore(CreateBlobContainer(selected,"verifier"),16777216,16777216));
            services.AddSingleton<VerifyRun.SingleAction.ISingleActionSeedSource,VerifyRun.SingleAction.SingleActionSeedSource>();
            services.AddSingleton<VerifyRun.SingleAction.SingleActionScenarioResolver>();
            services.AddSingleton<IVerifierModeHandler,VerifyRun.SingleAction.SingleActionHandler>();
            return;
        }
        if (mode is not ("start-line" or "verify" or "judge")) return;

        var preflight=new VerifierHostOptions();
        new VerifierHostOptionsSetup(configuration).Configure(preflight);
        ValidateOptionsResult preflightResult=new VerifierHostOptionsValidator().Validate(null,preflight);
        if(!preflightResult.Succeeded)throw new OptionsValidationException(Options.DefaultName,typeof(VerifierHostOptions),preflightResult.Failures??[]);
        RegisterTelemetry(services,configuration);

        if (mode is "start-line" or "verify")
        {
            services.AddParchwellKafka(KafkaClientRole.VerifierConsumer, options =>
            {
                options.NamespaceName = configuration["Verifier:NamespaceName"]!;
                options.ClientId = configuration["Verifier:VerifierClientId"]!;
            });
            services.AddParchwellKafkaAuthentication(options =>
            {
                options.TenantId = configuration["Verifier:TenantId"] ?? configuration["AZURE_TENANT_ID"];
                options.ClientId = configuration["Verifier:WorkloadIdentityClientId"] ?? configuration["AZURE_CLIENT_ID"];
                options.TokenFilePath = configuration["Verifier:FederatedTokenFilePath"] ?? configuration["AZURE_FEDERATED_TOKEN_FILE"];
            });
            services.AddSingleton<VerifierKafkaSessionFactory>();
        }
        services.AddSingleton<IVerifierArtifactStore>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<VerifierHostOptions>>().Value;
            return new BlobVerifierArtifactStore(
                CreateBlobContainer(options, options.VerifierContainer!),
                options.MaxArtifactBytes ?? options.MaxOutputBytes!.Value,
                options.MaxOutputBytes!.Value);
        });
        if (mode == "start-line")
        {
            services.AddSingleton<IVerifierModeHandler, StartLineHandler>();
            return;
        }
        services.AddParchwellScenarios(options =>
            configuration.GetSection("Verifier:ScenarioLoadOptions").Bind(options));
        services.AddSingleton<IScenarioSource>(provider => new FileSystemScenarioSource(
            AppContext.BaseDirectory));
        if (mode == "judge")
        {
            services.AddSingleton<IProducerArtifactStore>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<VerifierHostOptions>>().Value;
                return new BlobProducerArtifactStore(CreateBlobContainer(options, options.LedgerContainer!), options.MaxArtifactBytes!.Value);
            });
            services.AddSingleton<IVerifierModeHandler, JudgeHandler>();
            return;
        }
        services.AddSingleton<ILedgerArtifactSource>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<VerifierHostOptions>>().Value;
            return new BlobLedgerArtifactSource(
                CreateBlobContainer(options, options.LedgerContainer!),
                options.ArtifactPrefix!, options.MaxArtifactBytes!.Value);
        });
        services.AddSingleton<IProducerArtifactStore>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<VerifierHostOptions>>().Value;
            return new BlobProducerArtifactStore(CreateBlobContainer(options, options.LedgerContainer!), options.MaxArtifactBytes!.Value);
        });
        services.AddSingleton<RegisteredProducerConfigurationResolver>();
        services.AddSingleton<IVerifierModeHandler, VerifyRunHandler>();
    }

    private static void RegisterTelemetry(IServiceCollection services,ConfigurationManager configuration)
    {
        var credential=new WorkloadIdentityCredential(new WorkloadIdentityCredentialOptions
        {
            TenantId=configuration["Verifier:TenantId"]??configuration["AZURE_TENANT_ID"],
            ClientId=configuration["Verifier:WorkloadIdentityClientId"]??configuration["AZURE_CLIENT_ID"],
            TokenFilePath=configuration["Verifier:FederatedTokenFilePath"]??configuration["AZURE_FEDERATED_TOKEN_FILE"]
        });
        string? mode=configuration["Verifier:Mode"];
        services.AddParchwellTelemetry(credential,options=>
        {
            options.ConnectionString=configuration["Telemetry:ConnectionString"]??string.Empty;
            options.ServiceName=configuration["Telemetry:ServiceName"]??string.Empty;
            options.ServiceInstanceId=configuration["Telemetry:ServiceInstanceId"]??configuration["PARCHWELL_POD_NAME"]??string.Empty;
            options.ScenarioId=configuration["Telemetry:ScenarioId"]??string.Empty;
            options.RunId=configuration["Telemetry:RunId"]??string.Empty;
        });
        if(configuration["Telemetry:ServiceName"]!="Parchwell.Verifier"||configuration["Telemetry:ScenarioId"]!=configuration["Verifier:ScenarioId"]||
            configuration["Telemetry:RunId"]!=configuration["Verifier:RunId"]||string.IsNullOrWhiteSpace(configuration["Telemetry:ServiceInstanceId"]))
            throw new InvalidOperationException($"Telemetry identity is invalid for mode {mode}.");
    }

    internal static BlobContainerClient CreateBlobContainer(VerifierHostOptions options, string container)
    {
        var runnerWorkloadIdentityCredential = new WorkloadIdentityCredential(new WorkloadIdentityCredentialOptions
        {
            TenantId = options.TenantId,
            ClientId = options.WorkloadIdentityClientId,
            TokenFilePath = options.FederatedTokenFilePath
        });
        var clientOptions = new BlobClientOptions();
        clientOptions.Retry.MaxRetries = 0;
        var containerUri = new Uri(new Uri(options.BlobServiceUri!), container);
        return new BlobContainerClient(containerUri, runnerWorkloadIdentityCredential, clientOptions);
    }

    internal static async Task<int> RunAsync(
        string[] args,
        Action<IServiceCollection>? configureForTests = null,
        TimeProvider? timeProvider = null,
        TextWriter? completionOutputForTests = null)
    {
        IHost? host = null;
        ProcessWatchdog? watchdog = null;
        VerifierExecutionState? state = null;
        ILogger? logger = null;
        IConfiguration? configuration = null;
        VerifierCompletionState? completion = null;
        ICompletionReceiptPublisher? receiptPublisher=null;
        VerifierHostOptions? hostOptions=null;
        bool cleanupCompleted = true;
        try
        {
            host = BuildHostCore(args, configureForTests, timeProvider);
            var clock = timeProvider ?? TimeProvider.System;
            state = host.Services.GetRequiredService<VerifierExecutionState>();
            completion = host.Services.GetRequiredService<VerifierCompletionState>();
            receiptPublisher=host.Services.GetRequiredService<ICompletionReceiptPublisher>();
            hostOptions=host.Services.GetRequiredService<IOptions<VerifierHostOptions>>().Value;
            logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(Program));
            configuration = host.Services.GetRequiredService<IConfiguration>();
            watchdog = TryStartWatchdog(host, clock);

            try
            {
                await host.StartAsync().ConfigureAwait(false);
                await WaitForApplicationStopAsync(host.Services.GetRequiredService<IHostApplicationLifetime>())
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                cleanupCompleted = false;
                state!.MarkFailed();
                LogHostFailed(
                    logger!,
                    exception,
                    configuration!["Verifier:ScenarioId"],
                    configuration["Verifier:RunId"]);
            }
        }
        catch (Exception exception)
        {
            cleanupCompleted = false;
            if (host is not null)
            {
                logger ??= host.Services.GetService<ILoggerFactory>()?.CreateLogger(nameof(Program));
                configuration ??= host.Services.GetService<IConfiguration>();
                state ??= host.Services.GetService<VerifierExecutionState>();
                if (logger is not null)
                {
                    LogStartupFailed(
                        logger,
                        exception,
                        configuration?["Verifier:ScenarioId"],
                        configuration?["Verifier:RunId"]);
                }

                state?.MarkFailed();
            }
        }

        if (host is not null)
        {
            using var singleActionCleanup=hostOptions?.Mode=="single-action-judge"?new CancellationTokenSource(TimeSpan.FromSeconds(10),timeProvider??TimeProvider.System):null;
            try
            {
                await host.StopAsync(singleActionCleanup?.Token??CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                if (logger is not null)
                {
                    LogCleanupFailed(
                        logger,
                        exception,
                        configuration?["Verifier:ScenarioId"],
                        configuration?["Verifier:RunId"]);
                }

                cleanupCompleted = false;
                state?.MarkFailed();
            }

            try
            {
                if (host is IAsyncDisposable asyncDisposable)
                {
                    if(singleActionCleanup is not null)await asyncDisposable.DisposeAsync().AsTask().WaitAsync(singleActionCleanup.Token).ConfigureAwait(false);
                    else await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    host.Dispose();
                }
            }
            catch (Exception exception)
            {
                if (logger is not null)
                {
                    LogDisposalFailed(
                        logger,
                        exception,
                        configuration?["Verifier:ScenarioId"],
                        configuration?["Verifier:RunId"]);
                }

                cleanupCompleted = false;
                state?.MarkFailed();
            }
        }

        try
        {
            if(cleanupCompleted&&completion is not null&&receiptPublisher is not null&&hostOptions is not null&&
                completion.TryGetBlobReceipt(out string receiptContainer,out string receiptName,out byte[] receiptBytes))
            {
                try
                {
                    using var receiptDeadline=new CancellationTokenSource(FinalReceiptBudget(hostOptions),timeProvider??TimeProvider.System);
                    await receiptPublisher.PublishAsync(hostOptions,receiptContainer,receiptName,receiptBytes,receiptDeadline.Token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    completion.MarkFailed();state?.MarkFailed();
                    return 1;
                }
            }
            else if(cleanupCompleted&&completion is not null&&hostOptions?.Mode=="probe-gate")
            {
                try { await completion.WriteReceiptAsync(completionOutputForTests??Console.Out).ConfigureAwait(false); }
                catch (Exception) { completion.MarkFailed(); state?.MarkFailed(); return 1; }
            }
            return state?.ExitCode ?? 1;
        }
        finally
        {
            watchdog?.Dispose();
        }
    }

    /// <summary>Selects the publication budget for the final Blob completion receipt.</summary>
    /// <remarks>Start-line never binds IoTimeout, so it always uses the fixed 10-second budget.</remarks>
    internal static TimeSpan FinalReceiptBudget(VerifierHostOptions options) => options.IoTimeout ?? TimeSpan.FromSeconds(10);

    private static async Task WaitForApplicationStopAsync(IHostApplicationLifetime lifetime)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, lifetime.ApplicationStopping).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifetime.ApplicationStopping.IsCancellationRequested)
        {
            // The worker (or an external host stop request) has initiated the single cleanup path.
        }
    }

    private static ProcessWatchdog? TryStartWatchdog(IHost host, TimeProvider timeProvider)
    {
        try
        {
            var options = host.Services.GetRequiredService<IOptions<VerifierHostOptions>>().Value;
            if (options.ProcessLifetime is not { } lifetime || lifetime <= TimeSpan.Zero)
            {
                return null;
            }

            var watchdog = new ProcessWatchdog(timeProvider, lifetime);
            watchdog.Start();
            return watchdog;
        }
        catch (OptionsValidationException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}

/// <summary>Binds only budgets used by the configured mode so inactive values are ignored.</summary>
internal sealed class VerifierHostOptionsSetup : IConfigureOptions<VerifierHostOptions>
{
    private static readonly HashSet<string> AlwaysReadBudgets = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(VerifierHostOptions.ProcessLifetime),
        nameof(VerifierHostOptions.CleanupTimeout)
    };

    private static readonly HashSet<string> KafkaBudgets = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(VerifierHostOptions.TokenTimeout),
        nameof(VerifierHostOptions.MetadataTimeout),
        nameof(VerifierHostOptions.WatermarkTimeout),
        nameof(VerifierHostOptions.PollSlice),
        nameof(VerifierHostOptions.BlobIoTimeout)
    };

    private readonly IConfiguration _configuration;

    public VerifierHostOptionsSetup(IConfiguration configuration) => _configuration = configuration;

    public void Configure(VerifierHostOptions options)
    {
        string? mode = _configuration["Verifier:Mode"];
        if(mode=="single-action-judge")ValidateSingleActionFields();
        var usedBudgets = new HashSet<string>(AlwaysReadBudgets, StringComparer.OrdinalIgnoreCase);
        if (mode is "start-line" or "verify")
        {
            usedBudgets.UnionWith(KafkaBudgets);
        }

        if (mode is "probe-gate" or "verify" or "judge")
        {
            usedBudgets.Add(nameof(VerifierHostOptions.IoTimeout));
        }

        if (mode == "verify")
        {
            usedBudgets.Add(nameof(VerifierHostOptions.SafetyStop));
        }

        var values = _configuration
            .AsEnumerable()
            .Where(item => item.Key?.StartsWith("Verifier:", StringComparison.OrdinalIgnoreCase) == true
                && KeepValue(item.Key, usedBudgets, mode))
            .Select(item => new KeyValuePair<string, string?>(
                item.Key!["Verifier:".Length..],
                item.Value));
        IConfiguration filtered = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        filtered.Bind(options);
        if(mode=="single-action-judge")
        {
            options.ProcessLifetime=TimeSpan.FromSeconds(options.SingleActionLimits?.ProcessLifetimeSeconds??0);
            options.CleanupTimeout=TimeSpan.FromSeconds(options.SingleActionLimits?.CleanupSeconds??0);
            options.IoTimeout=TimeSpan.FromSeconds(options.SingleActionLimits?.IoSeconds??0);
        }
        if(mode!="single-action-judge")
        {
            options.JobName ??= _configuration["PARCHWELL_JOB_NAME"];
            options.PodName ??= _configuration["PARCHWELL_POD_NAME"];
            options.PodUid ??= _configuration["PARCHWELL_POD_UID"];
        }
        options.SourceCommit = _configuration["PARCHWELL_SOURCE_COMMIT"] ?? options.SourceCommit;
        options.TenantId ??= _configuration["AZURE_TENANT_ID"];
        options.WorkloadIdentityClientId ??= _configuration["AZURE_CLIENT_ID"];
        options.FederatedTokenFilePath ??= _configuration["AZURE_FEDERATED_TOKEN_FILE"];
    }

    private void ValidateSingleActionFields()
    {
        string[] required=["Mode","ScenarioId","ConfigurationId","RunId","VerifierCommit","JobName","PodName","PodUid","BlobServiceUri","VerifierContainer","NamespaceName","ScenarioRoot","LoadGenImage","VerifierImage","EnrichmentImage","CaptureContainer","CapturePrefix"];
        var allowed=new HashSet<string>(required,StringComparer.OrdinalIgnoreCase){"SingleActionLimits","ScenarioLoadOptions"};
        if(_configuration["Verifier:ScenarioId"]=="F04")
        {
            allowed.Add("SeedReceiptPath");allowed.Add("SeedPodIdentityPath");
            required=[..required,"SeedReceiptPath","SeedPodIdentityPath"];
        }
        var errors=new List<string>();
        foreach(string key in required)
            if(_configuration["Verifier:"+key] is null)errors.Add("Missing Verifier:"+key);
        foreach(var child in _configuration.GetSection("Verifier").GetChildren())
        {
            if(!allowed.Contains(child.Key)||child.Value is not null&&child.GetChildren().Any()||child.Key is not ("SingleActionLimits" or "ScenarioLoadOptions")&&child.GetChildren().Any())errors.Add("Unknown Verifier:"+child.Key);
        }
        CheckGroup("SingleActionLimits",typeof(Common.SingleAction.SingleActionLimits));
        CheckGroup("ScenarioLoadOptions",typeof(VerifierScenarioLoadOptions));
        if(errors.Count!=0)throw new OptionsValidationException(Options.DefaultName,typeof(VerifierHostOptions),errors);
        void CheckGroup(string group,Type type)
        {
            var fields=type.GetProperties().Select(property=>property.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach(string field in fields)if(_configuration[$"Verifier:{group}:{field}"] is null)errors.Add($"Missing Verifier:{group}:{field}");
            foreach(var child in _configuration.GetSection("Verifier:"+group).GetChildren())if(!fields.Contains(child.Key)||child.GetChildren().Any())errors.Add($"Unknown Verifier:{group}:{child.Key}");
        }
    }

    private static bool KeepValue(string key, HashSet<string> usedBudgets, string? mode)
    {
        string property = key["Verifier:".Length..];
        if (property.Equals(nameof(VerifierHostOptions.MaxConsumedRecords), StringComparison.OrdinalIgnoreCase)
            || property.Equals(nameof(VerifierHostOptions.MaxConsumedBytes), StringComparison.OrdinalIgnoreCase))
        {
            return mode == "verify";
        }

        return !IsBudget(property) || usedBudgets.Contains(property);
    }

    private static bool IsBudget(string property) =>
        property.Equals(nameof(VerifierHostOptions.ProcessLifetime), StringComparison.OrdinalIgnoreCase)
        || property.Equals(nameof(VerifierHostOptions.CleanupTimeout), StringComparison.OrdinalIgnoreCase)
        || property.Equals(nameof(VerifierHostOptions.IoTimeout), StringComparison.OrdinalIgnoreCase)
        || property.Equals(nameof(VerifierHostOptions.TokenTimeout), StringComparison.OrdinalIgnoreCase)
        || property.Equals(nameof(VerifierHostOptions.MetadataTimeout), StringComparison.OrdinalIgnoreCase)
        || property.Equals(nameof(VerifierHostOptions.WatermarkTimeout), StringComparison.OrdinalIgnoreCase)
        || property.Equals(nameof(VerifierHostOptions.PollSlice), StringComparison.OrdinalIgnoreCase)
        || property.Equals(nameof(VerifierHostOptions.BlobIoTimeout), StringComparison.OrdinalIgnoreCase)
        || property.Equals(nameof(VerifierHostOptions.SafetyStop), StringComparison.OrdinalIgnoreCase);
}

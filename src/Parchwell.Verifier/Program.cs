using Azure.Identity;
using Azure.Storage.Blobs;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Scenario;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.StartLine;
using Parchwell.Verifier.ProbeGate;
using Parchwell.Verifier.VerifyRun;
using Parchwell.Verifier.VerifyRun.Storage;
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

    public static Task<int> Main(string[] args) => RunAsync(args);

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
        if (mode is not ("start-line" or "verify")) return;

        services.AddParchwellKafka(KafkaClientRole.VerifierConsumer, options =>
        {
            options.NamespaceName = configuration["Verifier:NamespaceName"]!;
            options.ClientId = configuration["Verifier:VerifierClientId"]!;
        });
        services.AddParchwellKafkaAuthentication(options =>
        {
            options.TenantId = configuration["Verifier:TenantId"];
            options.ClientId = configuration["Verifier:WorkloadIdentityClientId"];
            options.TokenFilePath = configuration["Verifier:FederatedTokenFilePath"];
        });
        services.AddSingleton<VerifierKafkaSessionFactory>();
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

        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, options =>
        {
            options.NamespaceName = configuration["Verifier:NamespaceName"]!;
            options.ClientId = configuration["Verifier:ClientId"]!;
        });
        services.AddParchwellScenarios(options =>
            configuration.GetSection("Verifier:ScenarioLoadOptions").Bind(options));
        services.AddSingleton<IScenarioSource>(provider => new FileSystemScenarioSource(
            provider.GetRequiredService<IOptions<VerifierHostOptions>>().Value.ScenarioRoot!));
        services.AddSingleton<ILedgerArtifactSource>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<VerifierHostOptions>>().Value;
            return new BlobLedgerArtifactSource(
                CreateBlobContainer(options, options.LedgerContainer!),
                options.ArtifactPrefix!, options.MaxArtifactBytes!.Value);
        });
        services.AddSingleton<IVerifierModeHandler, VerifyRunHandler>();
    }

    private static BlobContainerClient CreateBlobContainer(VerifierHostOptions options, string container)
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
        TimeProvider? timeProvider = null)
    {
        IHost? host = null;
        ProcessWatchdog? watchdog = null;
        VerifierExecutionState? state = null;
        ILogger? logger = null;
        IConfiguration? configuration = null;
        try
        {
            host = BuildHostCore(args, configureForTests, timeProvider);
            var clock = timeProvider ?? TimeProvider.System;
            state = host.Services.GetRequiredService<VerifierExecutionState>();
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
            try
            {
                await host.StopAsync().ConfigureAwait(false);
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

                state?.MarkFailed();
            }

            try
            {
                if (host is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
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

                state?.MarkFailed();
            }
        }

        try
        {
            return state?.ExitCode ?? 1;
        }
        finally
        {
            watchdog?.Dispose();
        }
    }

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
        var usedBudgets = new HashSet<string>(AlwaysReadBudgets, StringComparer.OrdinalIgnoreCase);
        if (mode is "start-line" or "verify")
        {
            usedBudgets.UnionWith(KafkaBudgets);
        }

        if (mode is "probe-gate" or "verify")
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

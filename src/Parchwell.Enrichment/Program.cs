using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Parchwell.Enrichment.SeedF04;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Enrichment;

internal static class Program
{
    private static Task<int> Main(string[] args) => RunAsync(args);

    internal static async Task<int> RunAsync(string[] args, Action<HostApplicationBuilder>? configure = null,
        TextWriter? output = null, Func<IHost>? hostFactory = null)
    {
        output ??= Console.Out;
        if (args is ["--image-smoke"])
        {
            try
            {
                _ = Library.VersionString;
                await output.WriteLineAsync("{\"mode\":\"single-action-seed-smoke\",\"native_loaded\":true,\"uid_expected\":1654}").ConfigureAwait(false);
                await output.FlushAsync().ConfigureAwait(false);
                return 0;
            }
            catch (Exception) { return 1; }
        }
        bool validation = args is ["--validate-seed-configuration"];
        if (!validation && args is not ["--single-action-seed"]) return 2;
        IHost? host = null;
        ITimer? watchdog = null;
        SeedF04Options? options = null;
        SeedF04Result? result = null;
        TimeProvider clock = TimeProvider.System;
        bool hostCleaned = true;
        int exitCode = 1;
        try
        {
            host = hostFactory?.Invoke() ?? BuildHost(configure, validation);
            options = host.Services.GetRequiredService<IOptions<SeedF04Options>>().Value;
            clock = host.Services.GetRequiredService<TimeProvider>();
            if (!validation)
                watchdog = clock.CreateTimer(_ => Environment.Exit(124), null,
                    TimeSpan.FromSeconds(options.ProcessLifetimeSeconds), Timeout.InfiniteTimeSpan);
            await host.StartAsync().ConfigureAwait(false);
            if (validation) exitCode = 0;
            else
            {
                CancellationToken stopping = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
                result = await host.Services.GetRequiredService<SeedF04Worker>().ExecuteAsync(stopping).ConfigureAwait(false);
                exitCode = result.ExitCode;
            }
        }
        catch (Exception)
        {
            await Console.Error.WriteLineAsync($"seed_startup_or_execution_failed scenario_id=F04 run_id={options?.RunId ?? "unknown"}").ConfigureAwait(false);
        }
        finally
        {
            if (host is not null)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(options?.HostCleanupSeconds ?? 10), clock);
                try { await host.StopAsync(cleanup.Token).WaitAsync(cleanup.Token).ConfigureAwait(false); }
                catch (Exception) { hostCleaned = false; }
                try { await Task.Run(host.Dispose, CancellationToken.None).WaitAsync(cleanup.Token).ConfigureAwait(false); }
                catch (Exception) { hostCleaned = false; }
            }
        }
        try { watchdog?.Dispose(); }
        catch (Exception) { hostCleaned = false; }
        try
        {
            if (!hostCleaned) exitCode = 1;
            if (!validation && hostCleaned && result is { CleanupCompleted: true } && options is not null)
            {
                using var receiptDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.HostCleanupSeconds), clock);
                await output.WriteLineAsync(SeedF04Receipt.CreateLine(options, result))
                    .WaitAsync(receiptDeadline.Token).ConfigureAwait(false);
                await output.FlushAsync().WaitAsync(receiptDeadline.Token).ConfigureAwait(false);
            }
        }
        catch (Exception) { exitCode = 1; }
        return exitCode;
    }

    internal static IHost BuildHost(Action<HostApplicationBuilder>? configure = null, bool validationOnly = false)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Logging.ClearProviders();
        configure?.Invoke(builder);
        ConfigurationManager configuration = builder.Configuration;
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddOptions<SeedF04Options>()
            .Bind(configuration.GetSection(SeedF04Options.SectionName), binding => binding.ErrorOnUnknownConfiguration = true)
            .Validate(options =>
            {
                options.Validate();
                if (!StringComparer.Ordinal.Equals(options.SourceCommit, configuration["PARCHWELL_SOURCE_COMMIT"]) ||
                    !StringComparer.Ordinal.Equals(options.JobName, configuration["PARCHWELL_JOB_NAME"]) ||
                    !StringComparer.Ordinal.Equals(options.PodName, configuration["PARCHWELL_POD_NAME"]) ||
                    !StringComparer.Ordinal.Equals(options.PodUid, configuration["PARCHWELL_POD_UID"]))
                    throw new InvalidOperationException("seed_deployment_identity_mismatch");
                return true;
            }).ValidateOnStart();
        builder.Services.AddParchwellKafka(KafkaClientRole.EnrichmentProducer, options =>
        {
            options.NamespaceName = configuration["SeedF04:NamespaceName"] ?? string.Empty;
            options.ClientId = configuration["SeedF04:PodName"] ?? string.Empty;
        });
        if (!validationOnly)
        {
            builder.Services.AddParchwellKafkaAuthentication(options =>
            {
                options.TenantId = configuration["AZURE_TENANT_ID"];
                options.ClientId = configuration["AZURE_CLIENT_ID"];
                options.TokenFilePath = configuration["AZURE_FEDERATED_TOKEN_FILE"];
            });
            builder.Services.AddSingleton<Func<IProducer<byte[], byte[]>>>(services => () =>
            {
                SeedF04Options options = services.GetRequiredService<IOptions<SeedF04Options>>().Value;
                KafkaAuthenticationFactory authentication = services.GetRequiredService<KafkaAuthenticationFactory>();
                CancellationToken stopping = services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
                KafkaOAuthBearerSession session = authentication.CreateSession(new KafkaAuthenticationIdentity(
                    "F04", options.RunId, KafkaClientRole.EnrichmentProducer, options.PodName), stopping);
                ProducerConfig producer = services.GetRequiredService<KafkaConfigurationFactory>().CreateEnrichmentProducer().Configuration;
                return authentication.CreateProducerBuilder<byte[], byte[]>(producer, session).Build();
            });
            builder.Services.AddSingleton<SeedF04Worker>();
        }
        return builder.Build();
    }
}

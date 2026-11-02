using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Parchwell.Kafka.Telemetry;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Scenario;
using Parchwell.Verifier.Common.SingleAction;
using Parchwell.Verifier.Common.Storage;

namespace Parchwell.Verifier.VerifyRun.IdleObservation;

internal static class IdleObservationJudgeHost
{
    internal static int ValidateConfiguration()
    {
        try
        {
            var builder = Host.CreateApplicationBuilder([]);
            _ = IdleObservationJudgeOptions.Read(builder.Configuration, Environment.GetEnvironmentVariables());
            Console.WriteLine("{\"idle_observation_judge_configuration_valid\":true}");
            return 0;
        }
        catch (Exception) { Console.Error.WriteLine("idle_observation_judge_configuration_invalid"); return 2; }
    }

    internal static IHost BuildHost(Action<IServiceCollection>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder([]);
        var options = IdleObservationJudgeOptions.Read(builder.Configuration, Environment.GetEnvironmentVariables());
        builder.Services.Configure<ConsoleLifetimeOptions>(value => value.SuppressStatusMessages = true);
        builder.Services.AddOptions<IdleObservationJudgeOptions>().Bind(builder.Configuration.GetSection("IdleObservationJudge"), value => value.ErrorOnUnknownConfiguration = true)
            .Validate(value => { value.Validate(); return true; }).ValidateOnStart();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.Configure<HostOptions>(value => value.ShutdownTimeout = TimeSpan.FromSeconds(options.Limits.CleanupSeconds));
        builder.Services.AddParchwellScenarios(value =>
        {
            value.MaxDocumentCharacters = options.ScenarioLoadOptions.MaxDocumentCharacters; value.MaxNestingDepth = options.ScenarioLoadOptions.MaxNestingDepth;
            value.MaxScalarCharacters = options.ScenarioLoadOptions.MaxScalarCharacters; value.MaxParserEvents = options.ScenarioLoadOptions.MaxParserEvents;
        });
        builder.Services.AddSingleton<IScenarioSource>(_ => new FileSystemScenarioSource(options.ScenarioRoot));
        builder.Services.AddSingleton(_ => new BlobServiceClient(new Uri(options.BlobServiceUri), new WorkloadIdentityCredential(), new BlobClientOptions { Retry = { MaxRetries = 0 } }));
        builder.Services.AddSingleton<IIdleLedger>(provider => new BlobIdleLedger(provider.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(options.LedgerContainer)));
        builder.Services.AddSingleton<IVerifierArtifactStore>(provider => new BlobVerifierArtifactStore(
            provider.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(options.VerifierContainer),
            options.Limits.MaxCandidateBytes, options.Limits.MaxCandidateBytes));
        builder.Services.AddParchwellTelemetry(new WorkloadIdentityCredential(), value =>
        {
            value.ConnectionString = builder.Configuration["Telemetry:ConnectionString"] ?? ""; value.ServiceName = "Parchwell.Verifier";
            value.ServiceInstanceId = options.PodName; value.ScenarioId = "idle-observation"; value.RunId = options.RunId;
        });
        builder.Services.AddSingleton<IdleObservationResolver>();
        builder.Services.AddSingleton<IdleObservationJudgeHandler>();
        configure?.Invoke(builder.Services);
        return builder.Build();
    }

    internal static async Task<int> RunAsync(Func<IHost>? factory = null)
    {
        IHost? host = null; IdleObservationJudgeOptions? options = null; IVerifierArtifactStore? store = null;
        TimeProvider clock = TimeProvider.System; ITimer? watchdog = null; IdleCandidateReference[]? candidates = null;
        bool cleanup = true; int exit = 1;
        try
        {
            host = factory?.Invoke() ?? BuildHost();
            options = host.Services.GetRequiredService<IdleObservationJudgeOptions>(); clock = host.Services.GetRequiredService<TimeProvider>();
            store = host.Services.GetRequiredService<IVerifierArtifactStore>();
            watchdog = clock.CreateTimer(_ => Environment.Exit(124), null, TimeSpan.FromSeconds(options.Limits.ProcessLifetimeSeconds), Timeout.InfiniteTimeSpan);
            using var process = new CancellationTokenSource(TimeSpan.FromSeconds(options.Limits.ProcessLifetimeSeconds), clock);
            using var stopping = CancellationTokenSource.CreateLinkedTokenSource(process.Token, host.Services.GetService<IHostApplicationLifetime>()?.ApplicationStopping ?? CancellationToken.None);
            await host.StartAsync(stopping.Token).ConfigureAwait(false);
            candidates = await host.Services.GetRequiredService<IdleObservationJudgeHandler>().ExecuteAsync(stopping.Token).ConfigureAwait(false);
            exit = 0;
        }
        catch (Exception exception) { Console.Error.WriteLine("idle_observation_judge_failed:" + exception.GetType().Name); }
        finally
        {
            if (host is not null)
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(options?.Limits.CleanupSeconds ?? 10), clock);
                try { await host.StopAsync(deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false); }
                catch (Exception) { cleanup = false; }
                try { await Task.Run(host.Dispose).WaitAsync(deadline.Token).ConfigureAwait(false); }
                catch (Exception) { cleanup = false; }
            }
        }
        try
        {
            if (!cleanup || exit != 0 || candidates is null || store is null || options is null) return 1;
            int code = IdleObservationJudgement.ExitCode(candidates);
            var receipt = new IdleObservationJudgeReceipt(1, "idle-observation-judge", options.RunId, options.SourceCommit, options.JobName,
                options.PodName, options.PodUid, candidates, code, true);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.Limits.IoSeconds), clock);
            await store.CreateAsync(IdleObservationPaths.Receipt(options.RunId), SingleActionWire.Write(receipt, options.Limits.MaxJudgeReceiptBytes), deadline.Token)
                .WaitAsync(deadline.Token).ConfigureAwait(false);
            return code;
        }
        catch (Exception exception) { Console.Error.WriteLine("idle_observation_judge_receipt_failed:" + exception.GetType().Name); return 1; }
        finally { watchdog?.Dispose(); }
    }
}

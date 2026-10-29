using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Parchwell.Kafka.Telemetry;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Scenario;
using Parchwell.Verifier.Common.Storage;

namespace Parchwell.Verifier.VerifyRun.SingleAction;

internal static class ProbeV2Host
{
    internal static int ValidateConfiguration()
    {
        try
        {
            var builder = Host.CreateApplicationBuilder([]);
            _ = ProbeV2Options.Read(builder.Configuration,Environment.GetEnvironmentVariables());
            Console.WriteLine("{\"single_action_v2_judge_configuration_valid\":true}");
            return 0;
        }
        catch (Exception) { Console.Error.WriteLine("single_action_v2_judge_configuration_invalid"); return 2; }
    }

    internal static IHost BuildHost(Action<IServiceCollection>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder([]);
        var options = ProbeV2Options.Read(builder.Configuration,Environment.GetEnvironmentVariables());
        builder.Services.Configure<ConsoleLifetimeOptions>(value => value.SuppressStatusMessages = true);
        builder.Services.AddOptions<ProbeV2Options>().Bind(builder.Configuration.GetSection("ProbeV2Judge"),value => value.ErrorOnUnknownConfiguration = true)
            .Validate(value => { value.Validate(); return true; }).ValidateOnStart();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.Configure<HostOptions>(value => value.ShutdownTimeout = TimeSpan.FromSeconds(options.JudgeLimits.CleanupSeconds));
        builder.Services.AddParchwellScenarios(value =>
        {
            value.MaxDocumentCharacters = options.ScenarioLoadOptions.MaxDocumentCharacters; value.MaxNestingDepth = options.ScenarioLoadOptions.MaxNestingDepth;
            value.MaxScalarCharacters = options.ScenarioLoadOptions.MaxScalarCharacters; value.MaxParserEvents = options.ScenarioLoadOptions.MaxParserEvents;
        });
        builder.Services.AddSingleton<IScenarioSource>(_ => new FileSystemScenarioSource(options.ScenarioRoot));
        builder.Services.AddSingleton<IProbeV2Inputs,ProbeV2Inputs>();
        builder.Services.AddSingleton<ProbeV2Resolver>();
        builder.Services.AddSingleton<IVerifierArtifactStore>(_ => new BlobVerifierArtifactStore(
            new BlobServiceClient(new Uri(options.BlobServiceUri),new WorkloadIdentityCredential(),new BlobClientOptions { Retry = { MaxRetries = 0 } }).GetBlobContainerClient(options.VerifierContainer),
            options.JudgeLimits.MaxCandidateBytes,options.JudgeLimits.MaxCandidateBytes));
        builder.Services.AddParchwellTelemetry(new WorkloadIdentityCredential(),value =>
        {
            value.ConnectionString = builder.Configuration["Telemetry:ConnectionString"] ?? ""; value.ServiceName = "Parchwell.Verifier";
            value.ServiceInstanceId = options.PodName; value.ScenarioId = options.ScenarioId; value.RunId = options.RunId;
        });
        builder.Services.AddSingleton<ProbeV2Handler>();
        configure?.Invoke(builder.Services);
        return builder.Build();
    }

    internal static async Task<int> RunAsync(Func<IHost>? factory = null)
    {
        IHost? host = null; ProbeV2Options? options = null; IVerifierArtifactStore? store = null;
        TimeProvider clock = TimeProvider.System; ITimer? watchdog = null; ProbeV2Publication? publication = null;
        bool cleanup = true; int exit = 1;
        try
        {
            host = factory?.Invoke() ?? BuildHost();
            options = host.Services.GetRequiredService<ProbeV2Options>(); clock = host.Services.GetRequiredService<TimeProvider>();
            store = host.Services.GetRequiredService<IVerifierArtifactStore>();
            watchdog = clock.CreateTimer(_ => Environment.Exit(124),null,TimeSpan.FromSeconds(options.JudgeLimits.ProcessLifetimeSeconds),Timeout.InfiniteTimeSpan);
            using var process = new CancellationTokenSource(TimeSpan.FromSeconds(options.JudgeLimits.ProcessLifetimeSeconds),clock);
            using var stopping = CancellationTokenSource.CreateLinkedTokenSource(process.Token,host.Services.GetService<IHostApplicationLifetime>()?.ApplicationStopping ?? CancellationToken.None);
            await host.StartAsync(stopping.Token).ConfigureAwait(false);
            publication = await host.Services.GetRequiredService<ProbeV2Handler>().ExecuteAsync(stopping.Token).ConfigureAwait(false);
            exit = 0;
        }
        catch (Exception exception) { Console.Error.WriteLine("probe_v2_judge_failed:" + exception.GetType().Name); }
        finally
        {
            if (host is not null)
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(options?.JudgeLimits.CleanupSeconds ?? 10),clock);
                try { await host.StopAsync(deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false); }
                catch (Exception) { cleanup = false; }
                try { await Task.Run(host.Dispose).WaitAsync(deadline.Token).ConfigureAwait(false); }
                catch (Exception) { cleanup = false; }
            }
        }
        try
        {
            if (!cleanup || exit != 0 || publication is null || store is null || options is null) return 1;
            var receipt = new ProbeV2JudgeReceipt(2,"single-action-v2-judge",options.RunId,options.ScenarioId,options.ConfigurationId,
                options.SourceCommit,options.JobName,options.PodName,options.PodUid,publication.CandidateName,publication.CandidateSha256,0,true);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.JudgeLimits.IoSeconds),clock);
            await store.CreateAsync(ProbeV2Matrix.Prefix(options.RunId,options.ScenarioId,options.ConfigurationId) + "judge-completion.json",
                ProbeV2Wire.Write(receipt,options.JudgeLimits.MaxJudgeReceiptBytes),deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine("probe_v2_judge_receipt_failed:" + exception.GetType().Name); return 1; }
        finally { watchdog?.Dispose(); }
    }
}

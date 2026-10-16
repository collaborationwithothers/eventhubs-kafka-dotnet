using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Input;
using Parchwell.LoadGen.Run.Publication;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.Run.Execution;

/// <summary>Loads one scenario, resolves its declared settings, and executes the admitted worker run.</summary>
public sealed class LoadGenWorker : BackgroundService
{
    private static readonly Action<ILogger, string, string, Exception?> WorkerCancelled =
        LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(1, nameof(WorkerCancelled)),
            "Load generator worker cancelled for scenario {ScenarioId} run {RunId}");
    private static readonly Action<ILogger, string, string, Exception?> WorkerFailed =
        LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(2, nameof(WorkerFailed)),
            "Load generator worker failed for scenario {ScenarioId} run {RunId}");
    private readonly LoadGenHostOptions _hostOptions;
    private readonly ScenarioLoader _loader;
    private readonly ScenarioResolver _resolver;
    private readonly RunSessionFactory _runSessionFactory;
    private readonly IWorkerAdmission _admission;
    private readonly IRunArtifactPublisher _publisher;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<LoadGenWorker> _logger;
    public LoadGenWorker(IOptions<LoadGenHostOptions> hostOptions, ScenarioLoader loader,
        ScenarioResolver resolver, RunSessionFactory runSessionFactory, IWorkerAdmission admission,
        IRunArtifactPublisher publisher, IHostApplicationLifetime lifetime,
        ILogger<LoadGenWorker> logger)
    {
        _hostOptions = hostOptions.Value;
        _loader = loader;
        _resolver = resolver;
        _runSessionFactory = runSessionFactory;
        _admission = admission;
        _publisher = publisher;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <summary>Gets the process exit status after the worker stops.</summary>
    public int ExitCode { get; private set; } = 1;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ExecuteRunAsync(stoppingToken).ConfigureAwait(false);
            ExitCode = 0;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            ExitCode = 1;
            WorkerCancelled(_logger, _hostOptions.ScenarioId, _hostOptions.RunId, null);
        }
        catch (Exception exception)
        {
            ExitCode = 1;
            WorkerFailed(_logger, _hostOptions.ScenarioId, _hostOptions.RunId, exception);
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    private async Task ExecuteRunAsync(CancellationToken cancellationToken)
    {
        LoadGenHostOptions options = _hostOptions;
        RunIdentity identity = new(options.ScenarioId, options.ConfigurationId, options.RunId,
            options.ActualInstanceId, options.ExpectedInstanceId, options.ClientId);
        identity.Validate();

        LoadedScenario loaded = await _loader.LoadAsync(options.ScenarioId, cancellationToken).ConfigureAwait(false);
        if (string.Equals(identity.InstanceId, loaded.Profile.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal))
            throw new InvalidOperationException("worker_identity_must_be_distinct_from_workload_seed");
        ResolvedScenario resolved = _resolver.Resolve(loaded);
        if (!resolved.Configurations.TryGetValue(options.ConfigurationId, out ResolvedConfiguration? configuration))
            throw new InvalidOperationException("selected_configuration_not_declared_by_scenario");
        if (!configuration.Roles.TryGetValue(KafkaClientRole.LoadGenProducer, out ResolvedRoleConfiguration? role))
            throw new InvalidOperationException("scenario_does_not_declare_loadgen_producer");

        ProducerConfig producerConfig = _runSessionFactory.ResolveProducerConfiguration(role);

        // ADR-003 selects a custom OAUTHBEARER refresh handler. No production handler exists yet.
        // This admission check runs after pure configuration resolution and before any native client.
        _admission.EnsureProducerAuthenticationAvailable(producerConfig);
        if (!_publisher.IsAvailable)
            throw new InvalidOperationException("artifact_publication_unavailable");

        bool requiresStatistics = resolved.Definition.Measures.Any(measure => measure.Role == KafkaClientRole.LoadGenProducer &&
            measure.Id.StartsWith("stats.", StringComparison.Ordinal));
        bool requiresRequests = resolved.Definition.Measures.Any(measure => measure.Role == KafkaClientRole.LoadGenProducer &&
            string.Equals(measure.Id, "producer.request_timeout", StringComparison.Ordinal));
        await using RunSession session = await _runSessionFactory.CreateAsync(identity, role, producerConfig,
            requiresStatistics, requiresRequests);
        RunExecutionResult result = await session.ExecuteAsync(resolved.Profile, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0) throw new InvalidOperationException("worker_run_incomplete");
        await _publisher.PublishAsync(result, new RunPublicationOptions(options.BlobEndpoint,
            options.BlobContainer, options.UploadTimeout, options.MaxArtifactBytes,
            options.MaxAggregateArtifactBytes), cancellationToken).ConfigureAwait(false);
    }

}

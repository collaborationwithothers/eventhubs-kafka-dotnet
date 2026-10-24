using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using System.Runtime.ExceptionServices;
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
    private static readonly Action<ILogger, string, string, long, long, string, Exception?> WorkerIncomplete =
        LoggerMessage.Define<string, string, long, long, string>(LogLevel.Error, new EventId(3, nameof(WorkerIncomplete)),
            "Load generator run incomplete for scenario {ScenarioId} run {RunId}: attempted {AttemptCount} of {PlannedAttemptCount}; diagnostics {Diagnostics}");
    private static readonly Action<ILogger, string, string, string, Exception?> DiagnosticPublicationFailed =
        LoggerMessage.Define<string, string, string>(LogLevel.Error, new EventId(4, nameof(DiagnosticPublicationFailed)),
            "LoadGen diagnostic publication failed for scenario {ScenarioId} run {RunId}: {Failure}");
    private readonly LoadGenHostOptions _hostOptions;
    private readonly ScenarioLoader _loader;
    private readonly ScenarioResolver _resolver;
    private readonly RunSessionFactory _runSessionFactory;
    private readonly IWorkerAdmission _admission;
    private readonly IRunArtifactPublisher _publisher;
    private readonly IProducerRunLifecycle _lifecycle;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _clock;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<LoadGenWorker> _logger;
    public LoadGenWorker(IOptions<LoadGenHostOptions> hostOptions, ScenarioLoader loader,
        ScenarioResolver resolver, RunSessionFactory runSessionFactory, IWorkerAdmission admission,
        IRunArtifactPublisher publisher, IProducerRunLifecycle lifecycle, IConfiguration configuration,
        TimeProvider clock, IHostApplicationLifetime lifetime, ILogger<LoadGenWorker> logger)
    {
        _hostOptions = hostOptions.Value;
        _loader = loader;
        _resolver = resolver;
        _runSessionFactory = runSessionFactory;
        _admission = admission;
        _publisher = publisher;
        _lifecycle = lifecycle;
        _configuration = configuration;
        _clock = clock;
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
            stoppingToken.ThrowIfCancellationRequested();
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

        // Authentication preflight validates the baseline before registration and native client creation.
        _admission.EnsureProducerAuthenticationAvailable(producerConfig);
        if (!_publisher.IsAvailable)
            throw new InvalidOperationException("artifact_publication_unavailable");

        ProducerRunContext runContext = new(identity, options.Phase, options.RepeatNumber, options.JobName,
            options.PodName, options.PodUid, _configuration["PARCHWELL_SOURCE_COMMIT"] ?? string.Empty);
        runContext.Validate();
        var lifecycleOptions = new ProducerLifecycleOptions(options.BlobEndpoint, options.BlobContainer,
            options.CleanupTimeout, options.UploadTimeout, options.MaxArtifactBytes, options.MaxAggregateArtifactBytes, options.OutputRoot);

        using (var registrationActivity = LoadGenTelemetry.Begin(_clock, identity.ScenarioId, identity.RunId, identity.ClientId, "registration"))
        using (var registrationDeadline = new CancellationTokenSource(options.CleanupTimeout, _clock))
        using (var registrationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, registrationDeadline.Token))
        {
            await _lifecycle.RegisterAsync(runContext, lifecycleOptions, registrationToken.Token).ConfigureAwait(false);
            registrationActivity.Succeed();
        }

        bool requiresStatistics = resolved.Definition.Measures.Any(measure => measure.Role == KafkaClientRole.LoadGenProducer &&
            measure.Id.StartsWith("stats.", StringComparison.Ordinal));
        bool requiresRequests = resolved.Definition.Measures.Any(measure => measure.Role == KafkaClientRole.LoadGenProducer &&
            string.Equals(measure.Id, "producer.request_timeout", StringComparison.Ordinal));
        RunExecutionResult? result = null;
        try
        {
            using var runActivity = LoadGenTelemetry.Begin(_clock, identity.ScenarioId, identity.RunId, identity.ClientId, "run");
            RunSession session = await _runSessionFactory.CreateAsync(identity, role, producerConfig,
                requiresStatistics, requiresRequests, cancellationToken).ConfigureAwait(false);
            Exception? executionFailure = null;
            try
            {
                result = await session.ExecuteAsync(resolved.Profile, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                executionFailure = exception;
            }
            Exception? cleanupFailure = null;
            using (var sessionCleanupDeadline = new CancellationTokenSource(options.CleanupTimeout, _clock))
            {
                try { await session.DisposeAsync().AsTask().WaitAsync(sessionCleanupDeadline.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { cleanupFailure = new TimeoutException("loadgen_session_cleanup_timeout"); }
                catch (Exception exception) { cleanupFailure = exception; }
            }
            if (executionFailure is not null && cleanupFailure is not null)
                throw new AggregateException(executionFailure, cleanupFailure);
            if (executionFailure is not null) ExceptionDispatchInfo.Capture(executionFailure).Throw();
            if (cleanupFailure is not null) ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
            RunExecutionResult completedResult = result ?? throw new InvalidOperationException("run_session_returned_no_result");
            if (completedResult.ExitCode != 0)
            {
                WorkerIncomplete(_logger, identity.ScenarioId, identity.RunId, completedResult.Manifest.AttemptCount,
                    completedResult.Manifest.PlannedAttemptCount, string.Join(';', completedResult.Manifest.Diagnostics), null);
                throw new InvalidOperationException("worker_run_incomplete");
            }
            cancellationToken.ThrowIfCancellationRequested();
            using var publicationDeadline = new CancellationTokenSource(options.UploadTimeout, _clock);
            using var publicationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, publicationDeadline.Token);
            using var publicationActivity = LoadGenTelemetry.Begin(_clock, identity.ScenarioId, identity.RunId, identity.ClientId, "publication");
            await _publisher.PublishAsync(completedResult, new RunPublicationOptions(options.BlobEndpoint,
                options.BlobContainer, options.UploadTimeout, options.MaxArtifactBytes,
                options.MaxAggregateArtifactBytes) { Context = runContext }, publicationToken.Token).ConfigureAwait(false);
            publicationActivity.Succeed();
            cancellationToken.ThrowIfCancellationRequested();
            runActivity.Succeed();
        }
        catch
        {
            using var diagnosticsActivity = LoadGenTelemetry.Begin(_clock, identity.ScenarioId, identity.RunId, identity.ClientId, "diagnostics");
            try
            {
                await _lifecycle.PublishDiagnosticsAsync(runContext, result, lifecycleOptions,
                    CancellationToken.None).ConfigureAwait(false);
                diagnosticsActivity.Succeed();
            }
            catch (Exception exception)
            {
                string failure = exception is BlobProducerRunLifecycle.DiagnosticPublicationException diagnostic
                    ? diagnostic.Message
                    : "producer_diagnostics_failed";
                DiagnosticPublicationFailed(_logger, identity.ScenarioId, identity.RunId, failure, null);
            }
            throw;
        }
    }

}

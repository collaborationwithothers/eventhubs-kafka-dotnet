using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Statistics;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Input;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.LoadGen.Run.Workload;
using Parchwell.Scenarios;

namespace Parchwell.LoadGen.Run.Execution;

/// <summary>Builds the bounded resources for one admitted run through the composition root's DI registration.</summary>
public sealed class RunSessionFactory
{
    private readonly LoadGenHostOptions _options;
    private readonly RunEvidenceSinkRouter _sinkRouter;
    private readonly KafkaStatisticsBridgeFactory _statisticsFactory;
    private readonly KafkaConfigurationFactory _configurationFactory;
    private readonly TimeProvider _timeProvider;
    private readonly KafkaAuthenticationFactory _authenticationFactory;
    private readonly Func<ProducerConfig, KafkaStatisticsSession, RunEvidenceSession, KafkaOAuthBearerSession, IProducer<byte[], byte[]>> _producerFactory;
    private readonly Func<Task>? _beforeEvidenceWriterDrain;

    public RunSessionFactory(IOptions<LoadGenHostOptions> options, RunEvidenceSinkRouter sinkRouter,
        KafkaStatisticsBridgeFactory statisticsFactory, KafkaConfigurationFactory configurationFactory,
        TimeProvider timeProvider, KafkaAuthenticationFactory authenticationFactory,
        Func<ProducerConfig, KafkaStatisticsSession, RunEvidenceSession, KafkaOAuthBearerSession, IProducer<byte[], byte[]>> producerFactory)
        : this(options, sinkRouter, statisticsFactory, configurationFactory, timeProvider, authenticationFactory,
            producerFactory, beforeEvidenceWriterDrain: null)
    {
    }

    internal RunSessionFactory(IOptions<LoadGenHostOptions> options, RunEvidenceSinkRouter sinkRouter,
        KafkaStatisticsBridgeFactory statisticsFactory, KafkaConfigurationFactory configurationFactory,
        TimeProvider timeProvider, KafkaAuthenticationFactory authenticationFactory,
        Func<ProducerConfig, KafkaStatisticsSession, RunEvidenceSession, KafkaOAuthBearerSession, IProducer<byte[], byte[]>> producerFactory,
        Func<Task>? beforeEvidenceWriterDrain)
    {
        _options = options.Value;
        _sinkRouter = sinkRouter;
        _statisticsFactory = statisticsFactory;
        _configurationFactory = configurationFactory;
        _timeProvider = timeProvider;
        _authenticationFactory = authenticationFactory;
        _producerFactory = producerFactory;
        _beforeEvidenceWriterDrain = beforeEvidenceWriterDrain;
    }

    /// <summary>Creates output writers, statistics capture, and executor after admission has passed.</summary>
    public ProducerConfig ResolveProducerConfiguration(ResolvedRoleConfiguration configuration)
    {
        var baseline = _configurationFactory.CreateLoadGenProducer();
        ProducerConfig result = baseline.Configuration;
        foreach (KeyValuePair<string, ResolvedSetting> setting in configuration.Settings)
            result.Set(setting.Key, setting.Value.Value);
        return result;
    }

    public async Task<RunSession> CreateAsync(RunIdentity identity, ResolvedRoleConfiguration configuration, ProducerConfig producerConfig,
        bool requiresStatisticsEvidence, bool requiresRequestEvidence, CancellationToken cancellationToken)
    {
        LoadGenHostOptions options = _options;
        var authentication = _authenticationFactory.CreateSession(new KafkaAuthenticationIdentity(
            identity.ScenarioId, identity.RunId, KafkaClientRole.LoadGenProducer,
            producerConfig.ClientId!), cancellationToken);
        Directory.CreateDirectory(options.OutputRoot);
        var expectations = RunEvidenceExpectations.CaptureOnly(configuration,
            requiresStatisticsEvidence || options.RequireStatisticsEvidence!.Value);
        RunEvidenceSession? evidence = new RunEvidenceSession(identity, new RunEvidenceOptions(options.OutputRoot,
            options.EvidenceQueueCapacity, options.MaxReportBytes, options.MaxEvidenceFileBytes,
            options.MaxDiagnostics, options.EvidenceCompletionTimeout), _timeProvider, expectations, _beforeEvidenceWriterDrain);
        LocalLedgerStore? ledger = null;
        KafkaStatisticsSession? statistics = null;
        try
        {
            _sinkRouter.Attach(evidence);
            ledger = new LocalLedgerStore(new LedgerStoreOptions(
                Path.Combine(options.OutputRoot, "ledger.jsonl"), Path.Combine(options.OutputRoot, "manifest.json"),
                options.LedgerQueueCapacity, options.MaxRowBytes, options.MaxLedgerBytes,
                options.MaxManifestBytes, options.LedgerDrainTimeout));
            statistics = _statisticsFactory.Create(new KafkaStatisticsIdentity(
                identity.ScenarioId, identity.RunId, KafkaClientRole.LoadGenProducer, identity.ClientId));
            var executionOptions = new RunExecutionOptions(options.MaxAttempts, options.MaxPayloadBytes,
                options.MaxScheduleLateness, options.ProducerDrainTimeout, options.CleanupTimeout,
                options.MaxRowBytes, options.MaxManifestBytes, options.MaxLedgerReadbackBytes,
                options.OutputRoot, "document-events", [],
                requiresRequestEvidence || options.RequireRequestEvidence!.Value,
                requiresStatisticsEvidence || options.RequireStatisticsEvidence!.Value,
                options.LedgerDrainTimeout, options.StatisticsCompletionTimeout, options.EvidenceCompletionTimeout);
            var executor = new RunExecutor(() => _producerFactory(
                producerConfig, statistics, evidence, authentication), ledger, _timeProvider, statistics, evidence, authentication);
            return new RunSession(identity, executor, statistics, ledger, evidence, executionOptions);
        }
        catch (Exception failure)
        {
            List<Exception> cleanupFailures = [];
            if (statistics is not null)
            {
                try { await statistics.DisposeAsync().ConfigureAwait(false); }
                catch (Exception exception) { cleanupFailures.Add(exception); }
            }
            if (ledger is not null)
            {
                try { await ledger.DisposeAsync().ConfigureAwait(false); }
                catch (Exception exception) { cleanupFailures.Add(exception); }
            }
            try { await evidence.DisposeAsync().ConfigureAwait(false); }
            catch (Exception exception) { cleanupFailures.Add(exception); }
            if (cleanupFailures.Count != 0) throw new AggregateException([failure, .. cleanupFailures]);
            throw;
        }
    }

}

/// <summary>Owns the evidence writers and native session for exactly one worker invocation.</summary>
public sealed class RunSession : IAsyncDisposable
{
    private readonly RunIdentity _identity;
    private readonly RunExecutor _executor;
    private readonly KafkaStatisticsSession _statistics;
    private readonly LocalLedgerStore _ledger;
    private readonly RunEvidenceSession _evidence;
    private readonly RunExecutionOptions _executionOptions;
    internal RunSession(RunIdentity identity, RunExecutor executor,
        KafkaStatisticsSession statistics, LocalLedgerStore ledger, RunEvidenceSession evidence,
        RunExecutionOptions executionOptions)
    {
        _identity = identity;
        _executor = executor;
        _statistics = statistics;
        _ledger = ledger;
        _evidence = evidence;
        _executionOptions = executionOptions;
    }

    public Task<RunExecutionResult> ExecuteAsync(WorkloadProfile profile, CancellationToken cancellationToken) =>
        _executor.ExecuteAsync(profile, _identity, _executionOptions, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        List<Exception> failures = [];
        try { await _statistics.DisposeAsync().ConfigureAwait(false); }
        catch (Exception exception) { failures.Add(exception); }
        try { await _ledger.DisposeAsync().ConfigureAwait(false); }
        catch (Exception exception) { failures.Add(exception); }
        try { await _evidence.DisposeAsync().ConfigureAwait(false); }
        catch (Exception exception) { failures.Add(exception); }
        if (failures.Count != 0) throw new AggregateException(failures);
    }
}

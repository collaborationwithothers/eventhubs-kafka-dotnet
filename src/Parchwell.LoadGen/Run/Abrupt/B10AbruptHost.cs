using Azure.Identity;
using Azure.Storage.Blobs;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.Kafka.Statistics;
using Parchwell.Kafka.Telemetry;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Input;
using Parchwell.LoadGen.Run.Ledger;
using Parchwell.Scenarios;
using System.Globalization;
using System.Text;
using System.Text.Json;

using static Parchwell.LoadGen.Run.Abrupt.B10AbruptDiagnostics;

namespace Parchwell.LoadGen.Run.Abrupt;

internal static class B10AbruptHost
{
    internal static Action<IServiceCollection>? ConfigureTestServices { get; set; }
    internal static Func<ProducerConfig, Action<IProducer<byte[], byte[]>, string>, Action<IProducer<byte[], byte[]>, Error>, IProducer<byte[], byte[]>>? ProducerFactoryOverride { get; set; }
    internal static Func<(int ReturnValue, int Errno)>? SelfKillOverride { get; set; }
    internal static Func<B10AbruptCgroup.Observation>? CaptureBeforeOverride { get; set; }
    internal static Func<B10AbruptCgroup.Observation, B10AbruptCgroup.Observation>? CaptureAfterOverride { get; set; }

    internal static int ValidateConfiguration()
    {
        try
        {
            B10AbruptOptions.Read().Validate();
            Console.Out.WriteLine("{\"b10_abrupt_configuration_valid\":true}");
            return 0;
        }
        catch (Exception exception)
        {
            return ReportFailure(exception, Console.Error);
        }
    }
    internal static Task<int> RunParentAsync() => RunAsync("parent");
    internal static Task<int> RunChildAsync() => RunAsync("child");
    internal static Task<int> RunSuccessorAsync() => RunAsync("successor");

    private static async Task<int> RunAsync(string role)
    {
        long processStarted = TimeProvider.System.GetTimestamp();
        try
        {
            B10AbruptOptions options = B10AbruptOptions.Read();
            options.Validate(role);
            using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(900), TimeProvider.System);
            using var watchdog = TimeProvider.System.CreateTimer(_ => Environment.Exit(124), null, TimeSpan.FromSeconds(900), Timeout.InfiniteTimeSpan);
            using IHost host = BuildServices(options);
            _ = host.Services.GetRequiredService<IOptions<B10AbruptOptions>>().Value;
            await host.StartAsync(lifetime.Token).ConfigureAwait(false);
            TimeProvider clock = host.Services.GetRequiredService<TimeProvider>();
            B10AbruptPublisher publisher = host.Services.GetRequiredService<B10AbruptPublisher>();
            if (role == "parent")
            {
                var parent = new B10AbruptParent(options, clock, publisher, validateCapturedStatistics: async token =>
                {
                    LoadedScenario loaded = await host.Services.GetRequiredService<ScenarioLoader>().LoadAsync("B10", token).ConfigureAwait(false);
                    var resolved = ProbeSettings(host.Services.GetRequiredService<ScenarioResolver>().Resolve(loaded).Configurations["C1"].Roles[KafkaClientRole.LoadGenProducer], true);
                    var settings = resolved.Settings.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal);
                    await B10AbruptStatistics.ValidateCapturedAsync(Path.Combine(options.OutputRoot, "capture", "statistics.jsonl"),
                        new KafkaStatisticsIdentity("B10", options.RunId, KafkaClientRole.LoadGenProducer, options.PodName),
                        await ReadExpectedBrokersAsync(options, token).ConfigureAwait(false), settings, settings, token).ConfigureAwait(false);
                }, parentStartedTimestamp: processStarted, captureBefore: CaptureBeforeOverride, captureAfter: CaptureAfterOverride);
                await parent.RunAsync(lifetime.Token).ConfigureAwait(false);
                return 0;
            }
            if (role == "successor")
                return await RunValidatedSuccessorAsync(options, publisher, input => RunProducerAsync(input), lifetime.Token).ConfigureAwait(false);
            return await RunProducerAsync(null).ConfigureAwait(false);

            async Task<int> RunProducerAsync(B10AbruptPublisher.SuccessorInput? input)
            {
                if (input is not null) await publisher.RegisterDiagnosticAsync(options.Identity(input.FirstIdentity.InstanceId), lifetime.Token).ConfigureAwait(false);
                B10AbruptJournal? journal = input is null ? new B10AbruptJournal(Path.Combine(options.OutputRoot, "abrupt", "journal.jsonl"), options.Identity(), clock, purpose: options.Purpose) : null;
                await using var journalScope = journal;
                var runtime = await CreateProducerAsync(host.Services, options, journal, input, lifetime.Token).ConfigureAwait(false);
                var identity = new RunIdentity(options.ScenarioId, options.ConfigurationId, options.RunId, options.PodName, options.PodName, options.PodName);
                if (input is null)
                {
                    return await runtime.Producer.RunFirstAsync(runtime.Profile, identity, runtime.Execution, lifetime.Token).ConfigureAwait(false);
                }
                B10AbruptProducer.SuccessorArtifacts result = await runtime.Producer.RunSuccessorAsync(runtime.Profile, identity, runtime.Execution,
                    input.Entries, input.ReleaseUtc, lifetime.Token).ConfigureAwait(false);
                using (var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10), clock))
                {
                    await runtime.Statistics.DisposeAsync().AsTask().WaitAsync(cleanup.Token).ConfigureAwait(false);
                    await runtime.Evidence.DisposeAsync().AsTask().WaitAsync(cleanup.Token).ConfigureAwait(false);
                    await host.StopAsync(cleanup.Token).WaitAsync(cleanup.Token).ConfigureAwait(false);
                    await Task.Run(host.Dispose, cleanup.Token).WaitAsync(cleanup.Token).ConfigureAwait(false);
                }
                var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (var pair in result.Paths)
                    payloads.Add(pair.Key, await B10AbruptPublisher.ReadFileAsync(pair.Value, 268435456, lifetime.Token).ConfigureAwait(false));
                await publisher.PublishSuccessorAsync(options.Identity(input.FirstIdentity.InstanceId), payloads, input.FirstTermination, input.Journal, input.KillUnconfirmedAttemptIds,
                    result.ReadyUtc, result.ReleaseUtc, result.Invocations, cleanupCompleted: true, lifetime.Token).ConfigureAwait(false);
                return 0;
            }
        }
        catch (Exception exception)
        {
            return ReportFailure(exception, Console.Error);
        }
    }

    internal static async Task<int> RunValidatedSuccessorAsync(B10AbruptOptions options, B10AbruptPublisher publisher,
        Func<B10AbruptPublisher.SuccessorInput, Task<int>> continuation, CancellationToken cancellationToken)
    {
        B10AbruptPublisher.SuccessorInput input = await publisher.LoadSelectionAsync(options, cancellationToken).ConfigureAwait(false);
        return await continuation(input).ConfigureAwait(false);
    }

    internal static int ReportFailure(Exception exception, TextWriter error)
    {
        string failure = B10AbruptParent.FailureName(exception);
        string exceptionType = exception.GetType().FullName ?? exception.GetType().Name;
        if (exception is B10AbruptProcess.SignalException signalFailure)
        {
            error.WriteLine(JsonSerializer.Serialize(new { failure, exception_type = exceptionType, diagnostic = B10AbruptDiagnostics.Bounded(exception), signal_observation = signalFailure.Observation }, B10AbruptWire.JsonOptions));
            return 1;
        }
        if (failure == "kill_too_late" && exception.Message == "kill_too_late:no_unconfirmed_attempt") failure = "kill_too_late:no_unconfirmed_attempt";
        error.WriteLine((failure == "b10_abrupt_configuration_invalid"
            ? failure + " validation_detail=" + B10AbruptOptions.ReadValidationDetail(exception) + " exception_type=" + exceptionType
            : failure + " exception_type=" + exceptionType) + B10AbruptDiagnostics.PlainSuffix(exception));
        return 1;
    }

    internal static IHost BuildServices(B10AbruptOptions options)
    {
        options.Validate();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddOptions<B10AbruptOptions>().Configure(value => value.CopyFrom(options))
            .Validate(value => { value.Validate(); return true; }).ValidateOnStart();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddParchwellTelemetry(new WorkloadIdentityCredential(), value =>
        {
            value.ConnectionString = Environment.GetEnvironmentVariable("Telemetry__ConnectionString") ?? "";
            value.ServiceName = "Parchwell.LoadGen"; value.ServiceInstanceId = options.PodName;
            value.ScenarioId = options.ScenarioId; value.RunId = options.RunId;
        });
        builder.Services.AddSingleton<IB10AbruptStorage>(_ => new B10AbruptBlobStorage(
            new BlobServiceClient(new Uri(options.BlobEndpoint), new WorkloadIdentityCredential())));
        builder.Services.AddSingleton(services => new B10AbruptPublisher(services.GetRequiredService<IB10AbruptStorage>(), services.GetRequiredService<TimeProvider>(), options.Purpose));
        builder.Services.AddParchwellScenarios(value => { value.MaxDocumentCharacters = 1048576; value.MaxNestingDepth = 32; value.MaxScalarCharacters = 65536; value.MaxParserEvents = 100000; });
        builder.Services.AddSingleton<IScenarioSource>(_ => new FileSystemScenarioSource(options.ScenarioSourceRoot));
        AddB10KafkaRoles(builder.Services, options);
        if (options.Role != "parent")
        {
            builder.Services.AddSingleton<RunEvidenceSinkRouter>();
            builder.Services.AddSingleton<IKafkaStatisticsEvidenceSink>(services => services.GetRequiredService<RunEvidenceSinkRouter>());
            builder.Services.AddParchwellKafkaAuthentication(value =>
            {
                value.TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
                value.ClientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
                value.TokenFilePath = Environment.GetEnvironmentVariable("AZURE_FEDERATED_TOKEN_FILE");
            });
            builder.Services.AddParchwellKafkaStatistics(value =>
            { value.MaxSnapshotCharacters = 1048576; value.QueueCapacity = 2048; value.MaxBrokerSeries = 64; value.CompletionTimeout = TimeSpan.FromSeconds(20); });
        }
        ConfigureTestServices?.Invoke(builder.Services);
        return builder.Build();
    }

    // ScenarioResolver.Resolve builds a baseline for every role that scenarios/B10/scenario.yaml declares,
    // so every host role registers both. Registration maps options only: no consumer is created here.
    // The verifier consumer's baseline is resolved and discarded; its client.id is a fixed unused label.
    private static void AddB10KafkaRoles(IServiceCollection services, B10AbruptOptions options)
    {
        services.AddParchwellKafka(KafkaClientRole.LoadGenProducer, value =>
        { value.NamespaceName = options.NamespaceName; value.ClientId = options.PodName; value.EnrichmentGroupId = "a01-unused-enrichment"; });
        services.AddParchwellKafka(KafkaClientRole.VerifierConsumer, value =>
        { value.NamespaceName = options.NamespaceName; value.ClientId = "b10-abrupt-unused-verifier"; });
    }

    internal static ResolvedRoleConfiguration ProbeSettings(ResolvedRoleConfiguration role, bool child)
    {
        var timeout = role.Settings["message.timeout.ms"];
        var settings = role.Settings.SetItem("message.timeout.ms", timeout with { Value = "300000", Override = null });
        if (child) settings = settings.SetItem("linger.ms", settings["linger.ms"] with { Value = "0" });
        return role with { Settings = settings };
    }

    private static async Task<ProducerRuntime> CreateProducerAsync(IServiceProvider services, B10AbruptOptions options,
        B10AbruptJournal? journal, B10AbruptPublisher.SuccessorInput? input, CancellationToken cancellationToken)
    {
        TimeProvider clock = services.GetRequiredService<TimeProvider>();
        LoadedScenario loaded = await services.GetRequiredService<ScenarioLoader>().LoadAsync("B10", cancellationToken).ConfigureAwait(false);
        ResolvedRoleConfiguration role = ProbeSettings(services.GetRequiredService<ScenarioResolver>().Resolve(loaded).Configurations["C1"].Roles[KafkaClientRole.LoadGenProducer], options.Role == "child");
        ProducerConfig config = services.GetRequiredService<KafkaConfigurationFactory>().CreateLoadGenProducer().Configuration;
        foreach (var setting in role.Settings) config.Set(setting.Key, setting.Value.Value);
        string[] brokers = await ReadExpectedBrokersAsync(options, cancellationToken).ConfigureAwait(false);
        var identity = new RunIdentity("B10", "C1", options.RunId, options.PodName, options.PodName, options.PodName);
        string evidencePath = Path.Combine(options.OutputRoot, "capture");
        Directory.CreateDirectory(evidencePath);
        var evidence = new RunEvidenceSession(identity, new RunEvidenceOptions(evidencePath, 2048, 1048576, 134217728, 100, TimeSpan.FromSeconds(20)),
            clock, RunEvidenceExpectations.CaptureOnly(role, true));
        var statisticsIdentity = new KafkaStatisticsIdentity("B10", options.RunId, KafkaClientRole.LoadGenProducer, options.PodName);
        var settings = role.Settings.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal);
        var actual = settings.Keys.ToDictionary(key => key, key => config.Get(key), StringComparer.Ordinal);
        var observations = new B10AbruptStatistics(statisticsIdentity, brokers, settings, actual, evidence);
        observations.ActivateNativeCaptureTracking();
        services.GetRequiredService<RunEvidenceSinkRouter>().Attach(observations);
        KafkaStatisticsSession statistics = services.GetRequiredService<KafkaStatisticsBridgeFactory>().Create(statisticsIdentity);
        KafkaAuthenticationFactory authFactory = services.GetRequiredService<KafkaAuthenticationFactory>();
        authFactory.ValidateProducerConfiguration(config);
        KafkaOAuthBearerSession authentication = authFactory.CreateSession(new KafkaAuthenticationIdentity("B10", options.RunId, KafkaClientRole.LoadGenProducer, options.PodName), cancellationToken);
        var statisticsHandler = StatisticsHandler(observations, statistics);
        var errorHandler = CreateErrorHandler(evidence, journal, clock);
        IProducer<byte[], byte[]> Factory() => ProducerFactoryOverride is { } factory ? factory(config, statisticsHandler, errorHandler) : authFactory.CreateProducerBuilder<byte[], byte[]>(config, authentication)
            .SetStatisticsHandler(statisticsHandler).SetLogHandler((_, log) => evidence.CaptureLog(log)).SetErrorHandler(errorHandler).Build();
        var producer = new B10AbruptProducer(Factory, clock, observations, journal, evidence, options.Identity(input?.FirstIdentity.InstanceId),
            SuccessorCompletion(statistics, evidence, brokers, evidencePath), new B10AbruptTimeline(clock, options.Role, Console.Error), selfKill: SelfKillOverride, purpose: options.Purpose);
        var execution = new RunExecutionOptions(186000, 900000, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(10),
            16384, 67108864, 268435456, options.OutputRoot, "document-events", brokers, false, true,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20), ApplicationRetryPolicy.ForScenario("B10"));
        return new(producer, loaded.Profile, execution, authentication, statistics, evidence);
    }

    internal static Action<IProducer<byte[], byte[]>, Error> CreateErrorHandler(RunEvidenceSession evidence, B10AbruptJournal? journal, TimeProvider clock) =>
        (_, error) =>
        {
            journal?.TryCaptureNativeError((int)error.Code, error.Code.ToString(), error.IsError, error.IsFatal, clock.GetTimestamp());
            evidence.CaptureError(error);
        };

    // Native-capture tracking must see a report before the session can reject it, or the B10 adapter
    // could not match a report the session dropped.
    internal static Action<IProducer<byte[], byte[]>, string> StatisticsHandler(B10AbruptStatistics observations, KafkaStatisticsSession statistics) =>
        (_, raw) => { observations.CaptureNative(raw); statistics.Capture(raw); };

    // The successor completes statistics while its client is live. The producer disposes the client after that,
    // and only then completes evidence, so disposal callbacks reach the evidence session before it is finalized.
    internal static B10AbruptProducer.SuccessorCompletion SuccessorCompletion(KafkaStatisticsSession statistics, RunEvidenceSession evidence,
        string[] brokers, string evidencePath) => new(
        token => statistics.CompleteAsync(brokers, token),
        async (completion, token) =>
        {
            await evidence.CompleteAsync(completion, token).ConfigureAwait(false);
            if (!completion.IsComplete || evidence.IsFaulted)
                throw Attach(new InvalidDataException("b10_abrupt_guard_failed"), CompletionFacts(completion, evidence.IsFaulted));
            return evidence.ArtifactNames.ToDictionary(name => name, name => Path.Combine(evidencePath, name), StringComparer.Ordinal);
        });

    // One flag per diagnostic code KafkaStatisticsSession can emit. The fact order is part of the failure line; B10AbruptSuccessorCompletionTests pins it.
    private static readonly (B10AbruptFact Fact, string Code)[] CompletionCodeFacts =
    [
        (B10AbruptFact.DiagnosticClockUnverified, "clock_unverified"),
        (B10AbruptFact.DiagnosticLateCapture, "late_capture"),
        (B10AbruptFact.DiagnosticCaptureContention, "capture_contention"),
        (B10AbruptFact.DiagnosticOversizedSnapshot, "oversized_snapshot"),
        (B10AbruptFact.DiagnosticCaptureQueueOverflow, "capture_queue_overflow"),
        (B10AbruptFact.DiagnosticDisposedBeforeCompletion, "disposed_before_completion"),
        (B10AbruptFact.DiagnosticCompletionCancelled, "completion_cancelled"),
        (B10AbruptFact.DiagnosticSinkDrainTimeout, "sink_drain_timeout"),
        (B10AbruptFact.DiagnosticCompletionTimeout, "completion_timeout"),
        (B10AbruptFact.DiagnosticDisposedDuringCompletion, "disposed_during_completion"),
        (B10AbruptFact.DiagnosticAbandonedWithoutCompletion, "abandoned_without_completion"),
        (B10AbruptFact.DiagnosticProcessingFailure, "processing_failure"),
        (B10AbruptFact.DiagnosticMetricsRecordingFailure, "metrics_recording_failure"),
        (B10AbruptFact.DiagnosticEvidenceDeliveryCancelled, "evidence_delivery_cancelled"),
        (B10AbruptFact.DiagnosticEvidenceSinkFailure, "evidence_sink_failure"),
        (B10AbruptFact.DiagnosticClientIdMismatch, "client_id_mismatch"),
        (B10AbruptFact.DiagnosticClientTypeMismatch, "client_type_mismatch"),
        (B10AbruptFact.DiagnosticClientNameChanged, "client_name_changed"),
        (B10AbruptFact.DiagnosticNonIncreasingTimestamp, "non_increasing_timestamp"),
        (B10AbruptFact.DiagnosticClientInstanceChanged, "client_instance_changed"),
        (B10AbruptFact.DiagnosticLateAttachment, "late_attachment"),
        (B10AbruptFact.DiagnosticFutureNativeTimestamp, "future_native_timestamp"),
        (B10AbruptFact.DiagnosticBrokerSeriesLimitExceeded, "broker_series_limit_exceeded"),
        (B10AbruptFact.DiagnosticCounterReset, "counter_reset"),
        (B10AbruptFact.DiagnosticCaptureLoss, "capture_loss"),
        (B10AbruptFact.DiagnosticWorkerCancellationFailure, "worker_cancellation_failure"),
        (B10AbruptFact.DiagnosticInvalidJson, "invalid_json"),
        (B10AbruptFact.DiagnosticDuplicateProperty, "duplicate_property"),
        (B10AbruptFact.DiagnosticInvalidRoot, "invalid_root"),
        (B10AbruptFact.DiagnosticInvalidOrMissingField, "invalid_or_missing_field"),
        (B10AbruptFact.DiagnosticBrokerNameMismatch, "broker_name_mismatch"),
    ];

    internal static B10AbruptFactValue[] CompletionFacts(KafkaStatisticsCompletion completion, bool evidenceFaulted)
    {
        var codes = completion.Diagnostics.Select(diagnostic => diagnostic.Code).ToHashSet(StringComparer.Ordinal);
        var facts = new List<B10AbruptFactValue>(35)
        {
            Fact(B10AbruptFact.CompletionComplete, completion.IsComplete), Fact(B10AbruptFact.EvidenceFaulted, evidenceFaulted),
            Fact(B10AbruptFact.MissingBrokerCount, completion.MissingBrokers.Length), Fact(B10AbruptFact.DiagnosticCount, codes.Count)
        };
        foreach ((B10AbruptFact fact, string code) in CompletionCodeFacts) facts.Add(Fact(fact, codes.Contains(code)));
        return [.. facts];
    }

    private static async Task<string[]> ReadExpectedBrokersAsync(B10AbruptOptions options, CancellationToken cancellationToken)
    {
        string path = options.Role == "successor" ? options.PredecessorBindingPath : options.StartLineBindingPath;
        JsonElement binding = B10AbruptWire.Parse(await B10AbruptPublisher.ReadFileAsync(path, 65536, cancellationToken).ConfigureAwait(false));
        if (options.Role == "successor") binding = binding.GetProperty("start_line_binding");
        B10AbruptWire.ExactMembers(binding, "start_line", "broker_inventory", "completion");
        JsonElement inventory = B10AbruptWire.Parse(Encoding.UTF8.GetBytes(binding.GetProperty("broker_inventory").GetString()!));
        B10AbruptWire.ExactMembers(inventory, "schema_version", "identity", "brokers", "source", "source_commit");
        JsonElement identity = inventory.GetProperty("identity");
        var facts = new List<B10AbruptFactValue>();
        if (!(Check(facts, B10AbruptFact.SchemaMatches, inventory.GetProperty("schema_version").GetInt32() == 1) &&
            Check(facts, B10AbruptFact.SourceMatches, inventory.GetProperty("source").GetString() == "kafka_metadata") &&
            Check(facts, B10AbruptFact.SourceCommitMatches, inventory.GetProperty("source_commit").GetString() == options.SourceCommit) &&
            Check(facts, B10AbruptFact.RunMatches, identity.GetProperty("run_id").GetString() == options.RunId) &&
            Check(facts, B10AbruptFact.ScenarioMatches, identity.GetProperty("scenario_id").GetString() == options.ScenarioId) &&
            Check(facts, B10AbruptFact.ConfigurationMatches, identity.GetProperty("configuration_id").GetString() == options.ConfigurationId)))
            throw Attach(new InvalidDataException("b10_abrupt_guard_failed"), [.. facts]);
        var brokers = inventory.GetProperty("brokers");
        if (brokers.GetArrayLength() is < 1 or > 64) throw Attach(new InvalidDataException("b10_abrupt_guard_failed"), [Fact(B10AbruptFact.BrokerCount, brokers.GetArrayLength())]);
        string[] names = brokers.EnumerateArray().Select(broker => broker.GetProperty("name").GetString()!).ToArray();
        if (names.Any(string.IsNullOrWhiteSpace) || names.Distinct(StringComparer.Ordinal).Count() != names.Length) throw Attach(new InvalidDataException("b10_abrupt_guard_failed"), [Fact(B10AbruptFact.BrokerCount, names.Length), Fact(B10AbruptFact.BlankCount, names.Count(string.IsNullOrWhiteSpace)), Fact(B10AbruptFact.DistinctCount, names.Distinct(StringComparer.Ordinal).Count())]);
        return names;
    }
    private sealed record ProducerRuntime(B10AbruptProducer Producer, WorkloadProfile Profile, RunExecutionOptions Execution,
        KafkaOAuthBearerSession Authentication, KafkaStatisticsSession Statistics, RunEvidenceSession Evidence);
}

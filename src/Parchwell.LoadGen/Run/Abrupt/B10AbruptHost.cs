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
    internal static Task<int> RunSupervisorAsync() => RunAsync("supervisor");
    internal static Task<int> RunChildAsync() => RunAsync("child");
    internal static Task<int> RunSuccessorAsync() => RunAsync("successor");

    private static async Task<int> RunAsync(string role)
    {
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
            if (role == "supervisor")
            {
                string directory = Path.Combine(options.OutputRoot, "abrupt");
                Directory.CreateDirectory(directory);
                await using var journal = new B10AbruptJournal(Path.Combine(directory, "journal.jsonl"), options.Identity(), clock);
                var supervisor = new B10AbruptSupervisor(options, clock, journal, publisher,
                    () => new B10AbruptOwnedProcess(OwnedProducerProcess.Start(ChildCommand.ForRunningApphost(), options)),
                    async token =>
                    {
                        LoadedScenario loaded = await host.Services.GetRequiredService<ScenarioLoader>().LoadAsync("B10", token).ConfigureAwait(false);
                        var resolved = host.Services.GetRequiredService<ScenarioResolver>().Resolve(loaded).Configurations["C1"].Roles[KafkaClientRole.LoadGenProducer];
                        var settings = resolved.Settings.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal);
                        await B10AbruptStatistics.ValidateCapturedAsync(Path.Combine(options.OutputRoot, "capture", "statistics.jsonl"),
                            new KafkaStatisticsIdentity("B10", options.RunId, KafkaClientRole.LoadGenProducer, options.PodName),
                            await ReadExpectedBrokersAsync(options, token).ConfigureAwait(false), settings, settings, token).ConfigureAwait(false);
                    }, new B10AbruptTimeline(clock, "supervisor", Console.Error));
                await supervisor.RunAsync(lifetime.Token).ConfigureAwait(false);
                return 0;
            }
            if (role == "successor")
                return await RunValidatedSuccessorAsync(options, input => RunProducerAsync(input), lifetime.Token).ConfigureAwait(false);
            return await RunProducerAsync(null).ConfigureAwait(false);

            async Task<int> RunProducerAsync(B10AbruptPublisher.SuccessorInput? input)
            {
                if (input is not null) await publisher.RegisterDiagnosticAsync(options.Identity(input.FirstIdentity.InstanceId), lifetime.Token).ConfigureAwait(false);
                B10AbruptChildChannel? channel = role == "child"
                    ? B10AbruptChildChannel.Connect(options.EventsPipeHandle, options.AckPipeHandle, options.IdentityChallenge) : null;
                await using var channelScope = channel;
                var runtime = await CreateProducerAsync(host.Services, options, channel, input, lifetime.Token).ConfigureAwait(false);
                var identity = new RunIdentity(options.ScenarioId, options.ConfigurationId, options.RunId, options.PodName, options.PodName, options.PodName);
                if (input is null)
                {
                    await runtime.Producer.RunFirstAsync(runtime.Profile, identity, runtime.Execution, lifetime.Token).ConfigureAwait(false);
                    return 1; // The first child only completes by the supervisor's observed kill.
                }
                B10AbruptProducer.SuccessorArtifacts result = await runtime.Producer.RunSuccessorAsync(runtime.Profile, identity, runtime.Execution,
                    input.Selection.GetProperty("data").GetProperty("entries"), input.ReleaseUtc, lifetime.Token).ConfigureAwait(false);
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
                await publisher.PublishSuccessorAsync(options.Identity(input.FirstIdentity.InstanceId), payloads, input.Termination, input.Selection,
                    result.ReadyUtc, result.ReleaseUtc, result.Invocations, cleanupCompleted: true, lifetime.Token).ConfigureAwait(false);
                return 0;
            }
        }
        catch (Exception exception)
        {
            return ReportFailure(exception, Console.Error);
        }
    }

    internal static async Task<int> RunValidatedSuccessorAsync(B10AbruptOptions options,
        Func<B10AbruptPublisher.SuccessorInput, Task<int>> continuation, CancellationToken cancellationToken)
    {
        B10AbruptPublisher.SuccessorInput input = await B10AbruptPublisher.LoadSelectionAsync(options, cancellationToken).ConfigureAwait(false);
        return await continuation(input).ConfigureAwait(false);
    }

    internal static int ReportFailure(Exception exception, TextWriter error)
    {
        string failure = B10AbruptSupervisor.FailureName(exception);
        string exceptionType = exception.GetType().FullName ?? exception.GetType().Name;
        if (exception is OwnedProducerProcess.StartupException startupFailure)
        {
            error.WriteLine(JsonSerializer.Serialize(new
            {
                failure,
                exception_type = exceptionType,
                diagnostic = B10AbruptDiagnostics.Bounded(exception),
                cleanup_failure = startupFailure.CleanupFailure is { } cleanupFailure ? B10AbruptSupervisor.FailureName(cleanupFailure) : null,
                cleanup_exception_type = startupFailure.CleanupFailure is { } cleanupException
                    ? cleanupException.GetType().FullName ?? cleanupException.GetType().Name : null,
                cleanup_diagnostic = startupFailure.CleanupFailure is { } cleanupCause ? B10AbruptDiagnostics.Bounded(cleanupCause) : null,
                cleanup_signal_observation = (startupFailure.CleanupFailure as OwnedProducerProcess.SignalException)?.Observation
            }, B10AbruptWire.JsonOptions));
            return 1;
        }
        if (exception is OwnedProducerProcess.SignalException signalFailure)
        {
            error.WriteLine(JsonSerializer.Serialize(new
            {
                failure,
                exception_type = exceptionType,
                diagnostic = B10AbruptDiagnostics.Bounded(exception),
                signal_observation = signalFailure.Observation
            }, B10AbruptWire.JsonOptions));
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
        builder.Services.AddSingleton<B10AbruptPublisher>();
        builder.Services.AddParchwellScenarios(value => { value.MaxDocumentCharacters = 1048576; value.MaxNestingDepth = 32; value.MaxScalarCharacters = 65536; value.MaxParserEvents = 100000; });
        builder.Services.AddSingleton<IScenarioSource>(_ => new FileSystemScenarioSource(options.ScenarioSourceRoot));
        AddB10KafkaRoles(builder.Services, options);
        if (options.Role != "supervisor")
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

    private static async Task<ProducerRuntime> CreateProducerAsync(IServiceProvider services, B10AbruptOptions options,
        B10AbruptChildChannel? channel, B10AbruptPublisher.SuccessorInput? input, CancellationToken cancellationToken)
    {
        TimeProvider clock = services.GetRequiredService<TimeProvider>();
        LoadedScenario loaded = await services.GetRequiredService<ScenarioLoader>().LoadAsync("B10", cancellationToken).ConfigureAwait(false);
        ResolvedRoleConfiguration role = services.GetRequiredService<ScenarioResolver>().Resolve(loaded).Configurations["C1"].Roles[KafkaClientRole.LoadGenProducer];
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
        long syntheticSequence = 0;
        var producer = new B10AbruptProducer(() => authFactory.CreateProducerBuilder<byte[], byte[]>(config, authentication)
            .SetStatisticsHandler(StatisticsHandler(observations, statistics)).SetLogHandler((_, log) => evidence.CaptureLog(log))
            .SetErrorHandler((_, error) => evidence.CaptureError(error)).Build(), clock, observations,
            cohort =>
            {
                string path = Path.Combine(options.OutputRoot, cohort);
                Directory.CreateDirectory(path);
                return new LocalLedgerStore(new LedgerStoreOptions(Path.Combine(path, "ledger.jsonl"), Path.Combine(path, "manifest.json"),
                    8192, 16384, 268435456, 67108864, TimeSpan.FromSeconds(10)));
            },
            (kind, data, token) => channel is not null ? channel.SendAsync(kind, data, token) : Task.FromResult(Interlocked.Increment(ref syntheticSequence)),
            terminal => channel?.TryCaptureTerminal(terminal) ?? true,
            (arm, token) => ReadReleaseAsync(options, arm, clock, token),
            (cohort, data, token) => SaveSealAsync(options, cohort, data, token), evidence, options.Identity(input?.FirstIdentity.InstanceId),
            SuccessorCompletion(statistics, evidence, brokers, evidencePath), new B10AbruptTimeline(clock, options.Role, Console.Error));
        var execution = new RunExecutionOptions(186000, 900000, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(10),
            16384, 67108864, 268435456, options.OutputRoot, "document-events", brokers, false, true,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20), ApplicationRetryPolicy.ForScenario("B10"));
        if (channel is not null) channel.TerminalWritten = producer.MarkTerminals;
        return new(producer, loaded.Profile, execution, authentication, statistics, evidence);
    }

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

    // One flag per diagnostic code KafkaStatisticsSession can emit, in the order the contract lists them.
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
    internal static async Task<B10AbruptProducer.ReleaseInfo> ReadReleaseAsync(B10AbruptOptions options, string arm, TimeProvider clock, CancellationToken cancellationToken)
    {
        string path = Path.Combine(options.OutputRoot, "abrupt", arm + "-checkpoint.json");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        while (!File.Exists(path)) await Task.Delay(TimeSpan.FromMilliseconds(10), clock, linked.Token).ConfigureAwait(false);
        var frame = B10AbruptWire.ReadFrame(await B10AbruptPublisher.ReadFileAsync(path, 65536, linked.Token).ConfigureAwait(false));
        var facts = new List<B10AbruptFactValue>();
        if (!(Check(facts, B10AbruptFact.KindMatches, frame.Kind == "checkpoint") &&
            Check(facts, B10AbruptFact.IdentityMatches, frame.Identity == options.Identity()) &&
            Check(facts, B10AbruptFact.ArmMatches, frame.Data.GetProperty("arm").GetString() == arm)))
            throw Attach(new InvalidDataException("b10_abrupt_control_incomplete"), [.. facts]);
        string? selectedAttemptId = null;
        if (arm == "same_instance")
        {
            JsonElement selected = frame.Data.GetProperty("selected_attempt_ids");
            if (selected.GetArrayLength() == 0) throw Attach(new InvalidDataException("b10_abrupt_control_incomplete"));
            selectedAttemptId = selected[0].GetString();
        }
        string progressPath = Path.Combine(options.OutputRoot, "abrupt-child", "same_instance-resumed.json");
        await using (var file = new FileStream(progressPath + ".pending", FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new { arm, identity = options.Identity(), checkpoint_sequence = frame.Sequence,
                checkpoint_sha256 = B10AbruptPublisher.Hash(B10AbruptWire.WriteFrame(frame)) }, B10AbruptWire.JsonOptions), linked.Token).ConfigureAwait(false);
            file.Flush(true);
        }
        File.Move(progressPath + ".pending", progressPath, overwrite: false);
        return new(DateTimeOffset.ParseExact(frame.Data.GetProperty("release_utc").GetString()!, "O", CultureInfo.InvariantCulture), selectedAttemptId);
    }
    private static async Task SaveSealAsync(B10AbruptOptions options, string cohort, JsonElement data, CancellationToken cancellationToken)
    {
        string name = cohort == "same_instance" ? "control" : cohort;
        string path = Path.Combine(options.OutputRoot, "abrupt-child", name + "-seal.json");
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await file.WriteAsync(B10AbruptWire.SerializeEnvelope("cohort_seal", options.Identity(), data), cancellationToken).ConfigureAwait(false);
        file.Flush(true);
    }
    private sealed record ProducerRuntime(B10AbruptProducer Producer, WorkloadProfile Profile, RunExecutionOptions Execution,
        KafkaOAuthBearerSession Authentication, KafkaStatisticsSession Statistics, RunEvidenceSession Evidence);
}

# Kafka statistics bridge

The statistics bridge captures librdkafka reports and produces broker evidence and .NET monitoring metrics. The harness reads the evidence directly for scenario results and the throttle probe. A complete statistics capture does not establish the full late-write guard or individual request-timeout ordering.

The [agreed design][design] and [implementation plan][implementation] define this library delivery. It is reference-slice step 5a of issue 9. Worker integration, durable evidence storage, probe evaluation and exporter registration follow separately.

## Register and attach

Register an `IKafkaStatisticsEvidenceSink` implemented by the consuming harness, then call `AddParchwellKafkaStatistics`. Its required options have no defaults: `MaxSnapshotCharacters` bounds UTF-16 characters per JSON string, `QueueCapacity` bounds waiting reports, `MaxBrokerSeries` bounds distinct tracked broker keys per session, and `CompletionTimeout` bounds completion. Invalid or missing budgets fail startup validation.

The following values are synthetic configuration examples, not measured sizing recommendations or production defaults:

```csharp
services.AddSingleton<IKafkaStatisticsEvidenceSink>(evidenceSink);
services.AddParchwellKafkaStatistics(options =>
{
    options.MaxSnapshotCharacters = 256_000;
    options.QueueCapacity = 32;
    options.MaxBrokerSeries = 16;
    options.CompletionTimeout = TimeSpan.FromSeconds(45);
});

var factory = provider.GetRequiredService<KafkaStatisticsBridgeFactory>();
await using var session = factory.Create(new KafkaStatisticsIdentity(
    scenarioId, runId, KafkaClientRole.LoadGenProducer, podName));

using var producer = new ProducerBuilder<string, string>(configuration)
    .SetStatisticsHandler((_, json) => session.Capture(json))
    .Build();
```

Create a fresh session before each client is built. The baseline factory still supplies native configuration; the composition root attaches the callback. No client wrapper is introduced. The native report's client ID and producer/consumer role must agree with the supplied identity, and a session cannot change native client lifetimes.

The existing baseline interval is `15000` ms under [ADR-007 and the baseline contract][baseline]. Setting that interval alone does not collect evidence. The pinned [producer][producer] and [consumer][consumer] builders allow one statistics handler; it feeds both outputs through this bridge.

## Capture and evidence

`Capture` timestamps callback receipt through `TimeProvider` and attempts a bounded enqueue without waiting. A separate worker parses each accepted report once, copies supported fields into immutable records and delivers the original JSON with parsed samples and diagnostics to the sink. The callback does no sink I/O or metric export work.

The parser requires native client `name`, `client_id`, `type`, `ts`, `time`, `age` and `brokers`. Each broker supplies its identity/state, `req_timeouts`, `disconnects`, `txretries`, `txerrs`, `rxerrs`, throttle `cnt/max` and RTT `cnt/p99`. Selected-field type, range and presence errors reject the typed snapshot. Ordinary additional native fields are allowed. Duplicate JSON properties and disagreement between a broker dictionary key and its name are rejected. Native JSON definitions, units and asynchronous consistency limitations are in [librdkafka v2.16.0][statistics].

Original reports within the snapshot budget remain available even when parsing fails. Oversized reports are rejected without retaining their excessive contents. A callback that cannot immediately acquire the session's state gate records `capture_contention` rather than waiting. Contention, queue/series overflow, parse failures, counter resets and sink failures leave persistent evidence-quality diagnostics. Later good reports do not erase a gap. The completion result carries failures even if a failed sink could not save a diagnostic report. Raw JSON belongs in evidence storage rather than ordinary operator logs.

The sink must honor cancellation. Completion remains bounded if a sink does not cooperate, but cannot claim that blocked evidence was saved. The host owns storage and the Kafka client; the session owns capture processing.

## Catalogue and metrics

The [closed scenario catalogue][catalogue] remains in `Parchwell.Scenarios`. A later harness adapter maps its validated IDs and roles to these typed fields. `Parchwell.Kafka` takes no dependency on the scenarios library.

Inventory traceability follows the [frozen inventory and amendment][inventory]: A01 uses the broker timeout observations, H01 uses throttle and disconnect observations, H02 requires pod-derived client identity for correlation, and B10 shares producer guard inputs. H02 consumer-lag collection remains later work.

| Scenario ID | Typed broker field | Instrument | Unit |
|---|---|---|---|
| `stats.broker.throttle.max` | `Throttle.Value`, when `Throttle.Count > 0` | `kafka.broker.throttle.max_ms`, histogram | ms |
| `stats.broker.req_timeouts` | `RequestTimeouts` | `kafka.broker.req_timeouts`, counter | count |
| `stats.broker.disconnects` | `Disconnects` | `kafka.broker.disconnects`, counter | count |
| `stats.broker.rtt.p99` | `RoundTripTime.Value`, when `RoundTripTime.Count > 0` | `kafka.broker.rtt.p99_us`, histogram | us |

The probe reads `stats.broker.throttle.max` from captured evidence, with the role and threshold in its declaration. Missing or unusable evidence makes it inconclusive and blocks gated execution under the [scenario contract][scenario]. KQL is not the probe's evidence source.

The meter is `Parchwell.Kafka.Statistics`. Tags are `scenario`, `run`, `role`, `client_id` and `broker`. Session IDs and native timestamps remain in evidence. Topic, partition, tenant and message dimensions are excluded from this initial bridge.

Evidence preserves cumulative native counts. Metrics receive increments, including the first cumulative value from zero for the fresh client lifetime. A decreasing counter is permanently quarantined from delta export for that series. Guard fields `TransmissionRetries`, `TransmissionErrors` and `ReceiveErrors` retain `txretries`, `txerrs` and `rxerrs`; they introduce no selectable IDs or monitoring instruments.

Each nonempty native window contributes its summary once to a histogram. Empty windows have a null typed summary value and emit no fabricated zero. Histogram counts count window observations. RTT window p99 values cannot reconstruct a run-wide request p99.

The library uses the [.NET 10 metrics API][histogram] without an OpenTelemetry package. Services later register the meter with their SDK/exporter and retain histogram min/max. The reference [OpenTelemetry configuration][otel] and [Azure exporter mapping][azure] show how min/max can be retained; they are cited reference versions, not dependency additions or verified ingestion results for this repository. The blueprint's queries still require integration validation.

## Drain, completion and clock proof

After the producer is drained, call `CompleteAsync(expectedBrokerNames)` while keeping the client alive and its callbacks served. The producer's [Flush contract][flush] concerns outstanding requests and delivery callbacks; it promises no fresh statistics report. Completion waits for reports generated strictly after its post-drain boundary for the supplied broker keys, then stops capture and finishes accepted work within its configured deadline.

Callback receipt after drain is insufficient: a queued report may have been generated earlier. Native [report generation][generation] records `ts` from [rd_clock][native-clock] while formatting the JSON. On Linux, this is `CLOCK_MONOTONIC` in microseconds. The .NET 10 [system TimeProvider][time-provider] delegates to [Unix Stopwatch][stopwatch], through [System.Native][pal-time], to the [Linux monotonic clock][dotnet-clock]. Its timestamps are nanoseconds on this path.

Production freshness comparison is therefore supported only on Linux with `TimeProvider.System`. Convert the boundary upward to microseconds and require a strictly later native `ts`; same-microsecond ambiguity cannot pass. Other providers/platforms return an unverified-clock/incomplete result. Tests model the native clock through an internal seam, without exposing a caller-controlled assertion of clock compatibility in the public interface.

Missing broker keys, stale reports, source-clock uncertainty, prior capture failures, cancellation and deadline expiry prevent complete statistics status. Disposing the session without successful completion does not establish final evidence. The harness must inspect the returned status before accepting evidence. Completion freezes at an atomic boundary after diagnostics are collected: capture loss before that boundary makes the result incomplete even if its diagnostic arrives later. Calls after the freeze cannot revise the immutable completion result.

`expectedBrokerNames` describes native report keys within the bound client session. The load generator must independently retain every broker instance used, including instances missed between reports; identical names cannot establish continuity of recreated instances. Statistics completeness is only one input to [ADR-013][blueprint]. Nonzero guard counters, lifecycle uncertainty and delivery/request ambiguity remain reasons for the harness/verifier to reject authoritative outcome totals.

`producer.request_timeout` remains a separate request-level evidence requirement. A broker timeout count has no individual request identity or event ordering. Revised issue 14 and load-generator step 6 must settle and implement that channel before A01 can claim which timeout fired first.

## Verification

Run `./scripts/verify-dotnet.sh` with the pinned SDK and the repository's documented Docker setup. The bridge tests use in-memory sinks, `MeterListener`, controlled time and independent expectations. Selected real producer reports originate in the [existing v2.16.0 phantom-spike evidence][fixtures]; synthetic fixtures explicitly cover throttle, consumer and failure cases.

The delivery adds no Azure run, result, verdict, scenario prediction, request collector or exporter setup. The [implementation plan][implementation] identifies later harness integration and the app-review limit for this work.

[design]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/59#issuecomment-6193879131
[implementation]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/59#issuecomment-6195827324
[baseline]: kafka-baseline.md
[scenario]: scenario-contract.md
[catalogue]: ../../src/Parchwell.Scenarios/Contracts/MeasurementCatalog.cs
[blueprint]: ../blueprint.md
[inventory]: ../blueprint.md#12-scope-cuts
[statistics]: https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md
[producer]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ProducerBuilder.cs
[consumer]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ConsumerBuilder.cs
[flush]: https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/IProducer.cs
[generation]: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka.c#L1892-L1915
[native-clock]: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdtime.h#L85-L111
[time-provider]: https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/Common/src/System/TimeProvider.cs#L83-L97
[stopwatch]: https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Stopwatch.Unix.cs
[pal-time]: https://github.com/dotnet/runtime/blob/v10.0.13/src/native/libs/System.Native/pal_time.c
[dotnet-clock]: https://github.com/dotnet/runtime/blob/v10.0.13/src/native/minipal/time.c
[histogram]: https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/Metrics/Histogram.cs
[otel]: https://github.com/open-telemetry/opentelemetry-dotnet/blob/core-1.12.0/src/OpenTelemetry/Metrics/View/HistogramConfiguration.cs
[azure]: https://github.com/Azure/azure-sdk-for-net/blob/Azure.Monitor.OpenTelemetry.Exporter_1.4.0/sdk/monitor/Azure.Monitor.OpenTelemetry.Exporter/src/Customizations/Models/MetricDataPoint.cs
[fixtures]: phantom-spike/evidence/2026-10-14/experiment.json.gz

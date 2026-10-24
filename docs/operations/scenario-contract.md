# Scenario declaration contract

Parchwell.Scenarios loads scenario declarations and profiles and composes Kafka settings. Under [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates), workers use these packaged inputs in-process; the chart receives identifiers and infrastructure data. A valid declaration is not evidence that deployment or measurement collection has run.

## Input and ownership

The caller registers an IScenarioSource, required positive ScenarioLoadOptions budgets and the Kafka roles used by the scenario. AddParchwellScenarios registers startup validation and the concrete loader and resolver. Sources return fresh TextReaders; the loader owns and disposes each reader, including on failure or cancellation. Workers supply the image's scenario/profile source under [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates).

LoadAsync(id) requests only scenarios/<id>/scenario.yaml and its validated profile under scenarios/profiles/. It never requests expected.yaml. IDs must match the requested frozen inventory item. Profile references are profiles/<slug>.yaml, with no absolute paths, URLs, backslashes or parent traversal.

Each document is bounded by MaxDocumentCharacters (UTF-16 reader characters), MaxNestingDepth (mapping/sequence nesting), MaxScalarCharacters and MaxParserEvents (all YAML parsing events). There are no default budgets. A reader is bounded before event parsing, and limit excess is rejected rather than truncated. Cancellation applies to opening, reading and parsing. Ordinary block and flow YAML is accepted. Duplicate/unknown/case-mismatched fields, multiple documents, anchors, aliases, merge keys and explicit tags are rejected. Diagnostics include logical source, field path, stable code and YAML location when available. Code comparisons must follow the [documented migration](#diagnostic-code-migration-issue-61) when upgrading.

## Diagnostic code migration (issue 61)

[Issue 61](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/61) requires one diagnostic code convention across the declaration reader and resolver. This implementation uses snake_case, matching the declaration reader. Consumers upgrading from [PR 60](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/60) must update comparisons and stored-code translations using the mapping below. The resolver no longer emits the old hyphenated spellings; it does not emit compatibility aliases.

| Previous resolver code | Replacement code |
|---|---|
| `baseline-metadata-coverage` | `baseline_metadata_coverage` |
| `baseline-metadata-value-mismatch` | `baseline_metadata_value_mismatch` |
| `cannot-activate-omitted-setting` | `cannot_activate_omitted_setting` |
| `invalid-baseline-metadata` | `invalid_baseline_metadata` |
| `invalid-baseline-omission` | `invalid_baseline_omission` |
| `invalid-baseline-snapshot` | `invalid_baseline_snapshot` |
| `invalid-baseline-value` | `invalid_baseline_value` |
| `no-op-override` | `no_op_override` |
| `setting-not-in-baseline` | `setting_not_in_baseline` |
| `setting-out-of-range` | `setting_out_of_range` |
| `unsupported-setting` | `unsupported_setting` |
| `wrong-role-setting` | `wrong_role_setting` |

This table covers each retained resolver code renamed by issue 61. The source comparison is the [previous resolver](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/d8b6efcab004f9c41e80a005591df61c522ab612/src/Parchwell.Scenarios/Resolution/ScenarioResolver.cs) against the [updated resolver](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/47bf0401830d9d51c57ebfbed82ee1d06fe9ba57/src/Parchwell.Scenarios/Resolution/ScenarioResolver.cs). Declaration-reader and YAML parser code spellings are unchanged. Structural declaration errors remain the loader's responsibility; the resolver no longer repeats that validation.

This is an explicit diagnostic API migration. The [YAML schema](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/47bf0401830d9d51c57ebfbed82ee1d06fe9ba57/src/Parchwell.Scenarios/Contracts/ScenarioContracts.cs) remains unchanged by this redesign; the historical generated Helm values interface is removed by C7 under [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates). Callers should treat diagnostic codes as stable outside documented migrations.

## Scenario schema

The initial schema_version is the contract literal 1. All named mappings are case-sensitive. Required root fields are schema_version, id, baseline, roles, profile, configurations, fault, repeats, gate and measures. No opaque extension maps are accepted.

- baseline is exactly Parchwell.Kafka.
- roles is a nonempty unique list of exact KafkaClientRole names.
- configurations is a nonempty map of identifier to role map to Kafka-key/integer replacement. An empty role map, including an empty configuration, is a valid baseline. Config/tenant/stage identifiers are ASCII slugs starting with a letter and containing letters, digits, underscore or hyphen.
- repeats is a plain decimal integer obeying the factory minimum. The policy literal is at least five; it is a repository rule, not a measurement ([factory plan template](../agents/factory.md#plan-template)).
- measures is a nonempty unique list of maps with exactly id and role. The role must be declared and applicable to the closed logical catalogue. Native availability does not imply implemented collection.
- fault has exactly live and component. Either can be {kind: none}. A live declaration otherwise has kind: throttling and a nonempty unique configurations list. A component declaration otherwise has kind: toxiproxy_latency, configurations, direction: downstream and positive plain integer latency_ms. All configuration references must exist.
- gate is {kind: none}, or has kind: probe, configuration, repeats, predicate and configurations. The predicate has id: stats.broker.throttle.max, a declared applicable role, operator: greater_than and positive plain integer threshold_ms. The gate repeats obeys the factory minimum independently of full-run repeats. configurations is the nonempty unique list of gated full configurations, and the predicate measurement must also be requested in measures.

### A01 probe policy

A probe is a separate phase preceding full runs. Its C0 reference does not replace the later full C0 repeats. A01 attempts all five probe slots before phase judgement, with zero replacements. All five must supply usable evidence before any qualifying observation can pass the phase. With all five usable, any attributed throttle sample strictly greater than 20000 ms passes; five usable repeats without such a sample yield `untestable_at_build_scale`. Missing or unusable required evidence yields `inconclusive`. Only pass permits the gated full C0/C3 phases; controlled component execution remains independent.

Under [decision 6384660536](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6384660536), complete, attributed statistics coverage with no positive throttle sample is usable and nonqualifying. This means no positive sample observed, not a measured zero. Absent optional sample values and references remain absent. An actually observed zero value remains observed zero. Missing or incomplete coverage remains inconclusive.

Preserve the existing usability prerequisites: evaluated/pass checks 3, 27 and 17-26, independent expected-broker coverage and post-drain completion, as identified by the [approved J8 contract](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6383206899). Only the requirement for a positive sample is removed. Message validity and authoritative counts remain independently governed by [ADR-012](../blueprint.md#adr-012-the-oracle-is-a-per-message-ledger-joined-into-four-outcome-classes)/[013](../blueprint.md#adr-013-a-run-is-the-window-between-high-watermarks-recorded-before-and-after-the-load-generator) and the all-five-complete-valid phase rule; usable throttle evidence never manufactures counts.

This is the approved target policy. [C5/#162](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/162) owns its executable judge/verifier implementation and the matching verifier-contract text; C0 does not claim current code already implements it. The library itself is not a probe executor. expected.yaml assesses outcomes and never drives deployment.

## Profile schema

Required fields are schema_version, id, seed, tenants, documents_per_burst, payloads and stages. Version is the contract literal 1. The profile id is the filename slug. Seed is a plain signed decimal Int32; other numeric inputs below are positive plain decimal Int32 values. Quoted numbers, booleans, nulls, fractions and overflow are rejected.

- tenants is a nonempty list of {id, weight}, with unique identifiers.
- documents_per_burst is positive.
- payloads contains exactly uploaded, ocr-started, ocr-finished, classified and indexed. Each value is a nonempty list of {size_bytes, weight}, with unique sizes.
- stages is a nonempty ordered list of {id, offered_attempts_per_second, duration_seconds}, with unique identifiers. Weight sums and aggregate planned attempt arithmetic must not overflow Int64.

The seed, traffic weights, burst size and discrete payload distributions describe synthetic input. Stage schedules are finite and predetermined. Runtime observations cannot change rates, durations or worker count. A01 parameters remain separately approved planning inputs, never measured saturation claims. See the [blueprint workload and ADR-008](../blueprint.md).

## Composition and output

ScenarioResolver receives KafkaConfigurationFactory in its constructor. Resolve(LoadedScenario) invokes only declared roles' existing factory methods. It returns immutable string settings, baseline metadata and omissions. It does not mutate or expose the mutable native baseline configuration. Each replacement additionally records scenario/configuration identity and source location; baseline metadata continues to describe initial emission.

Only LoadGenProducer request.timeout.ms and socket.timeout.ms replacements are supported initially. Require plain decimal integers within the pinned native ranges, invariant formatting and a value numerically different from the baseline. Reject other keys, aliases, wrong roles, removal and activating omissions. Empty configurations remain valid. Do not impose timeout-ordering constraints. Sources: [request timeout](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_conf.c#L1797-L1802), [socket timeout](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_conf.c#L576-L585), [merged baseline contract](kafka-baseline.md).

Workers consume the resolved scenario/profile and effective Kafka configuration in-process under [ADR-014](../blueprint.md#adr-014-scenarios-are-a-definition-file-and-an-expected-outcome-file-per-item-workers-resolve-deployment-settings-in-process) and [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates). There is no values-generator program or generated comparison contract in the target design. The chart's interface is the identifier, infrastructure, image and bounds interface fixed by the [#138 item plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6383206899). C7 removes the obsolete generator implementation. expected.yaml is never a resolver input.

## Measurements and review boundaries

The declaration catalogue includes ledger.delivery_error_code, ledger.delivery_latency_ms, verifier.ok/loss/phantom/reported_failure/duplicate/reorder, stats.broker.throttle.max/req_timeouts/disconnects/rtt.p99 and producer.request_timeout. Ledger entries target LoadGenProducer, verifier counts target VerifierConsumer, broker statistics apply to declared Kafka roles, and request evidence applies to producer roles. Source/type/unit/scope are recorded in the catalogue. Throttle windows use milliseconds, RTT windows microseconds, broker counters are cumulative, and verifier totals require complete valid runs. [Pinned statistics reference](https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md#brokers).

producer.request_timeout is a declaration of required request-level evidence, not an existing managed callback. [Native request timeout logging](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c#L1043-L1049) is distinct from [message delivery timeout translation](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_request.c#L4766-L4770). Each approved item plan names the collection channel and ordering evidence. Bridge/export/result support is separately planned in [#59](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/59).

ADR-013 remains binding: timeout/retry ambiguity or incomplete guard evidence prevents authoritative outcome totals. Missing counts never become zero or falsify A01 H3. Unit fixtures are synthetic and in-memory.

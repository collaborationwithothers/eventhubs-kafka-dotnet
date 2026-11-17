Revision: 1

# C03 scenario: contract-grade plan (issue 508)

This plan adds the C03 scenario and its expected outcomes. C03 asks what happens when one message takes longer to process than `max.poll.interval.ms`. It has two cases. In the first, the consumer keeps heartbeating but makes no `Consume` call, so it should lose its partitions. In the second, it pauses and keeps calling `Consume`, so it should keep them. The work is two pull requests. The first supplies the inputs that let a C03 dispatch run end to end, and it enables the live smoke run. The second adds the verifier observation that the decision rules need. Nothing in this plan publishes a result.

Base commit: `main` at `10cacff23129bf891e73e1fa5d824e30a4ec360e`.
Short name of the set: `c03-scenario`. Pull requests, in order: `c03-inputs` (section 2.1), then `c03-observation` (section 2.2).
Implementer: Codex, in sessions Hari starts ([decision 6894614673 on #499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894614673), point 2). Each pull request gets the Claude review gate of [decision 6894587233 on #499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894587233), point 2.

## 1. Goal and authority

Goal: a C03 dispatch of `scenario.yml` on `main` that runs phases C0 and C1, five repeats each, whose verifier and judge output let the expected outcomes in section 5.3 be applied, with a smoke run first.

Authority, in creation order (factory.md "Item lifecycle" step 3):

1. The issue body of #508: split entry 5 of #499; the two pause cases of decision 6894564479; a smoke run; `expected.yaml` is protected and Hari approves it with the plan.
2. #499 decisions, through the child's body and its adopting decisions: [6894534441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441) (batch; K-55 and K-28 tested with C03; K-47 not tested), [6894564479](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894564479) (both pause cases; the README and report state the heartbeat and poll-limit distinction; the heartbeat claim must be verified), [6894587233](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894587233) (split approved; Claude review gate), [6894614673](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894614673) (Claude plans; Codex implements), rounds 1 to 3 ([6895299617](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895299617), [6895328446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895328446), [6895343227](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895343227)), which ADR-022 now records.
3. [#508 decision 6895537629](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/508#issuecomment-6895537629), adopting the [#499 standing decision 6895535291](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291). Its point 4 reserves expected outcomes, Azure dispatch, money, protected-path merges and scope beyond the approved split to Hari.
4. [#508 decision 6899940416](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/508#issuecomment-6899940416), adopting [#499 decision 6899939624](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6899939624): #508 adds the production settings source and the scenario-schema and profile changes it needs; #510 extends them for C10 and C01; the consumer phase judge (#521) gates #508's smoke run.
5. [#499 decision 6926376436](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6926376436): the consumer evidence archive (#531) gates #508's C03 batch dispatch.

Assigned work recorded on #508 by the control session, with their sources: the override-policy entries for the settings C03 varies ([control action 6900154862](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/508#issuecomment-6900154862), from the #505 plan check); load generator acceptance of C03 ([control action 6900831605](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/508#issuecomment-6900831605), from [#506 decision 6900831355](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/506#issuecomment-6900831355) point 2); the batch dispatch gate on #531 ([control action 6926377393](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/508#issuecomment-6926377393)); the heartbeat source to read ([control action 6897727430](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/508#issuecomment-6897727430)).

Deferred items assigned to #508 by approved predecessor plans:
- Production `IConsumerProcessingPlanSource` (#505 plan at c617dfba, section 10a item 1; "Gate it blocks: #508's smoke run").
- Production `IConsumerVerificationPlanSource`, the scenario schema fields and the `Content` link of the C03 file into the verifier (#506 plan at a4c5ea1, section 10a item 1).
- Load generator acceptance of the consumer pairs (#506 plan at a4c5ea1, section 10a item 6).
- Event Hubs proof of #506 DA1 and DA2 at #508's smoke stage (#506 plan section 10a item 3; exhaustion action `record the gap and continue`).
- The live smoke of the consumer chart and workflow design, with #507 DA1 to DA5 (#507 plan at b67cf7f, section 7 and deferred item 1; [#507 decision 6925863575](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6925863575) point 2).
- Consumer windows under which five repeats fit the 7200 s phase budget (#507 decision 6925863575 point 1; #507 plan line 91).

Predecessors: the #507 pull request is merged (pull request 536, merge commit `10cacff23129bf891e73e1fa5d824e30a4ec360e`). The #521 judge is merged (pull request 533).

Not authorized by this plan: any Azure dispatch (section 9 states who may start the smoke and batch dispatches), any change to `expected.yaml` after approval, any verdict, anything under `results/`, and any change to ADR text in `docs/blueprint.md`.

## 2. Scope

### 2.1 Pull request `c03-inputs`

Purpose: make a C03 dispatch runnable. After it merges, the smoke run of section 9 can be dispatched.

Files allowed to change:
- `scenarios/C03/scenario.yaml` (new; section 3.1).
- `scenarios/C03/expected.yaml` (new, protected; section 5.3, byte for byte as approved).
- `scenarios/profiles/c03-consumer.yaml` (new; section 3.2).
- `src/Parchwell.Scenarios/Contracts/ScenarioContracts.cs`, `src/Parchwell.Scenarios/Loading/DefinitionReader.cs`, `src/Parchwell.Scenarios/Loading/ProfileReader.cs`, `src/Parchwell.Scenarios/Loading/ScenarioLoader.cs`, and one new file `src/Parchwell.Scenarios/Loading/ConsumerProcessingDefinitionReader.cs`.
- `src/Parchwell.Kafka/Configuration/KafkaScenarioOverrideSetting.cs` (one policy entry; section 3.4).
- `src/Parchwell.Enrichment/Parchwell.Enrichment.csproj` (protected: one `ProjectReference` to `../Parchwell.Scenarios/Parchwell.Scenarios.csproj`, and two `Content` items), `src/Parchwell.Enrichment/Program.cs`, new files under `src/Parchwell.Enrichment/ConsumerProcessing/` for the production plan source and its scenario file source.
- `src/Parchwell.Verifier/Parchwell.Verifier.csproj` (protected: two `Content` items), `src/Parchwell.Verifier/Program.cs`, and new files under `src/Parchwell.Verifier/Common/Consumer/` for the production verification plan source.
- `src/Parchwell.LoadGen/Parchwell.LoadGen.csproj` (protected: two `Content` items), `src/Parchwell.LoadGen/Run/Input/LoadGenHostOptions.cs`, `src/Parchwell.LoadGen/Run/Publication/ProducerRunContext.cs`.
- `packages.lock.json` files that `dotnet restore` changes because of the new project reference, and nothing else in them.
- `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`, only through `c5_runtime.py renew` under requirement R1.20.
- Tests under `tests/Parchwell.Scenarios.Tests/`, `tests/Parchwell.Kafka.Tests/`, `tests/Parchwell.Enrichment.Tests/`, `tests/Parchwell.Verifier.Tests/`, `tests/Parchwell.LoadGen.Tests/`, under the test map and the standing fixture rule (section 7.1).
- `docs/operations/scenario-contract.md`: a new section "Consumer processing declarations", one sentence in "Profile schema", and one sentence in "Composition and output" (section 3.6).
- `docs/operations/consumer-workflow.md`: only line 1's clause "nothing here runs live until the child issue that supplies the C03 plan sources and load generator input merges", replaced as section 3.6 states.
- `items/508/state.md` (the item state file, factory.md "Item state file").

### 2.2 Pull request `c03-observation`

Purpose: add the per-repeat membership observation of section 4.3 to the verifier's consumer block and to the judge's candidate, so the C03 decision rules can be applied. It must merge before the C03 batch dispatch.

Files allowed to change:
- `src/Parchwell.Verifier/Common/Consumer/ConsumerVerificationPlan.cs`, the production verification plan source added by `c03-inputs`, `src/Parchwell.Verifier/VerifyRun/Consumer/Judgement/ConsumerJudgement.cs`, `src/Parchwell.Verifier/Judge/ConsumerPhaseEvidence.cs`, and the candidate codec file that rejects unknown consumer projection properties (found by the implementer; named in the implementation notes).
- `docs/operations/verifier-contract.md`: the J3 block rule and the candidate projection paragraph (lines 128 and 136 at the base commit), and the consumer block description where it lists the block's properties.
- `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`, only through `c5_runtime.py renew` under requirement R2.9.
- Tests under `tests/Parchwell.Verifier.Tests/`, under the test map and the standing fixture rule.
- `items/508/state.md`.

### 2.3 Paths that must not change, in both pull requests

`src/Parchwell.Scenarios/Contracts/ScenarioPhases.cs`; everything under `.github/`, `charts/`, `infra/`, `results/`; `docs/blueprint.md`; `scenarios/` outside `scenarios/C03/` and `scenarios/profiles/c03-consumer.yaml`; `Directory.Packages.props` and `Directory.Build.props`; the worker's `ConsumerProcessingOptions.cs`, `ConsumerProcessingPlan.cs` and `ConsumerProcessingWorker.cs`; the verifier's `PhaseWireCodec.cs` and `VerifierHostOptionsValidator.cs`; `tests/Parchwell.ArchitectureTests/`. No new project, no new NuGet package. The one new assembly reference is the Enrichment-to-Scenarios project reference of section 2.1, which is open question Q3.

## 3. External contracts: pull request `c03-inputs`

### 3.1 `scenarios/C03/scenario.yaml`

Exactly this content, with LF line endings and a final newline:

```yaml
schema_version: 1
id: C03
baseline: Parchwell.Kafka
roles:
  - LoadGenProducer
  - EnrichmentConsumer
  - VerifierConsumer
profile: profiles/c03-consumer.yaml

configurations:
  C0:
    EnrichmentConsumer:
      max.poll.interval.ms: 60000
  C1:
    EnrichmentConsumer:
      max.poll.interval.ms: 60000

fault:
  live: {kind: none}
  component: {kind: none}

repeats: 5

gate: {kind: none}

measures:
  - {id: ledger.delivery_error_code, role: LoadGenProducer}
  - {id: client.fatal_error, role: LoadGenProducer}
  - {id: verifier.ok, role: VerifierConsumer}
  - {id: verifier.loss, role: VerifierConsumer}
  - {id: verifier.phantom, role: VerifierConsumer}
  - {id: verifier.reported_failure, role: VerifierConsumer}
  - {id: verifier.duplicate, role: VerifierConsumer}
  - {id: verifier.reorder, role: VerifierConsumer}

consumer_processing:
  window_ms: 360000
  completion_deadline_ms: 30000
  configurations:
    C0:
      slow_message: {ordinal: 20, duration_ms: 90000, mode: inline}
    C1:
      slow_message: {ordinal: 20, duration_ms: 90000, mode: pause}
```

Meaning, for every reader of this file:
- Phase `C0` runs configuration `C0`, the inline case of decision 6894564479 point 1 (no `Consume` call during the slow message). Phase `C1` runs `C1`, the pause case (all partitions paused, `Consume` still called).
- In a consumer scenario, the `EnrichmentConsumer` overrides of a configuration apply to the subject member only. The witness runs the `EnrichmentConsumer` baseline, as ADR-022 "Configuration fidelity" requires (docs/blueprint.md line 775).
- The values are policy, chosen in this plan and listed under open question Q1. They are not measurements. Basis for the choices: the slow message (90000 ms) is longer than the subject's `max.poll.interval.ms` (60000 ms), which is longer than `session.timeout.ms` (30000 ms, baseline). So in the inline case heartbeats outlive the session timeout while the poll limit is exceeded, which is the distinction decision 6894564479 asks for. Microsoft's guidance requires `max.poll.interval.ms` to be greater than `session.timeout.ms` (source 4).

### 3.2 `scenarios/profiles/c03-consumer.yaml`

Exactly this content, with LF line endings and a final newline:

```yaml
schema_version: 1
id: c03-consumer
seed: 1401
tenants:
  - {id: tenant01, weight: 40}
  - {id: tenant02, weight: 20}
  - {id: tenant03, weight: 10}
  - {id: tenant04, weight: 8}
  - {id: tenant05, weight: 6}
  - {id: tenant06, weight: 5}
  - {id: tenant07, weight: 4}
  - {id: tenant08, weight: 3}
  - {id: tenant09, weight: 2}
  - {id: tenant10, weight: 2}
documents_per_burst: 2000
payloads:
  uploaded:
    - {size_bytes: 1024, weight: 1}
  ocr-started:
    - {size_bytes: 1024, weight: 1}
  ocr-finished:
    - {size_bytes: 150000, weight: 99}
    - {size_bytes: 900000, weight: 1}
  classified:
    - {size_bytes: 1024, weight: 1}
  indexed:
    - {size_bytes: 1024, weight: 1}
stages:
  - {id: steady4, offered_attempts_per_second: 4, duration_seconds: 60}
consumer:
  processing_delay_ms: 200
```

The workload is synthetic (AGENTS.md hard safety rule 11). 200 ms is the lower bound of the blueprint's enrichment processing time, "enrichment 200 ms to 5 s" (docs/blueprint.md line 47). That bound is a design input, not a measurement. Offered load: 4 attempts per second for 60 s, 240 planned attempts. With 200 ms per message, one member can process at most 5 messages per second before Blob append time, which is not measured (ADR-022 "Consequences"). So the two members' capacity is above the offered load. This is an estimate, its basis is that arithmetic, and it was made on 2026-11-17.

### 3.3 Scenario and profile schema

`consumer_processing` is a new optional root field of `scenario.yaml`. It is added to the allowed-field list in `DefinitionReader.cs` line 27 and read by the new `ConsumerProcessingDefinitionReader`. It is a closed declaration with fixed literals per item, following `IdleObservationDefinitionReader`. Enforced checks, numbered. Each failure is a `ScenarioValidationException` diagnostic with the code and field path given:

1. `consumer_processing` present for an item other than C03: `consumer_processing_unexpected`, path `consumer_processing`. C03 without it: `consumer_processing_required`, path `consumer_processing`. #510 adds C10 and C01 to the item table.
2. Combined with `single_action`, `probe_v2` or `idle_observation`: `consumer_processing_combined`, path `consumer_processing`.
3. Not a mapping with exactly `window_ms`, `completion_deadline_ms` and `configurations`: `consumer_processing_shape`, path `consumer_processing`.
4. `window_ms` not the plain scalar `360000`, or `completion_deadline_ms` not the plain scalar `30000`: `consumer_processing_literal`, path `consumer_processing.window_ms` or `consumer_processing.completion_deadline_ms`.
5. `configurations` keys not exactly the scenario's configuration keys: `consumer_processing_configurations`, path `consumer_processing.configurations`.
6. A configuration entry not a mapping with exactly `slow_message`, or `slow_message` not a mapping with exactly `ordinal`, `duration_ms` and `mode`: `consumer_processing_shape`, path `consumer_processing.configurations.<id>`.
7. For C03, `ordinal` not the plain scalar `20`, `duration_ms` not the plain scalar `90000`, or `mode` not `inline` for `C0` and `pause` for `C1`: `consumer_processing_literal`, path `consumer_processing.configurations.<id>.slow_message.<field>`.
8. For C03, the rest of the file not equal to the fixed experiment: `repeats` 5; `fault.live.kind` and `fault.component.kind` `none`; gate `none`; roles exactly `LoadGenProducer`, `EnrichmentConsumer`, `VerifierConsumer` in that order; measures exactly the eight of section 3.1; each configuration's overrides exactly `EnrichmentConsumer:max.poll.interval.ms=60000`. Failure: `consumer_processing_experiment`, path `consumer_processing`.

The validated value is a new optional property of `ScenarioDefinition`, holding the window, the completion deadline and, per configuration, the slow message's ordinal, duration and mode. Names are the implementer's.

`consumer` is a new optional root field of the profile schema, added to the field list in `ProfileReader.cs` line 26. Enforced checks:

9. `consumer` not a mapping with exactly `processing_delay_ms`: `profile_consumer_shape`, path `consumer`.
10. `processing_delay_ms` not a plain decimal integer from 0 to 60000: `profile_consumer_range`, path `consumer.processing_delay_ms`. 60000 is the worker's existing upper bound (`ConsumerProcessingPlan.cs` line 24 at the base commit).

The validated value is a new optional property of `WorkloadProfile`. One cross-file check runs in `ScenarioLoader.LoadAsync` after both files are read:

11. A definition with `consumer_processing` whose profile has no `consumer`: `consumer_profile_required`, source the profile path, path `consumer`.

Every existing scenario and profile on `main` keeps loading and resolving exactly as before. No existing diagnostic code, path or message changes.

### 3.4 Override policy entry

One new entry in `KafkaScenarioOverridePolicy.Entries` (`KafkaScenarioOverrideSetting.cs`, the list at lines 130 to 155 at the base commit), placed after the `PartitionAssignmentStrategy` entry:

```csharp
new(KafkaSettingKeys.MaxPollIntervalMs, KafkaScenarioOverrideValueKind.Integer, 1, 86_400_000, [KafkaClientRole.EnrichmentConsumer], ["C03"], [NativeConfiguration]),
```

Bounds 1 to 86400000 are librdkafka v2.16.0's range for `max.poll.interval.ms` (source 1, line 137). No entry for `enable.auto.offset.store` or `session.timeout.ms` is added: C03 varies neither. #510 adds what C10 and C01 vary.

### 3.5 Production plan sources and admission

Worker (`Parchwell.Enrichment`):
- `BuildConsumerProcessingHost` registers a production `IConsumerProcessingPlanSource` with `TryAddSingleton`, after `configure` runs, so a test registration made in `configure` still wins.
- The source loads `scenarios/<ScenarioId>/scenario.yaml` and its profile from files under `AppContext.BaseDirectory`, using `ScenarioLoader`, and resolves them with `ScenarioResolver`. Both files are packaged into the image by `Content` items in the csproj, with the same `Link`, `CopyToOutputDirectory` and `CopyToPublishDirectory` form as `Parchwell.Verifier.csproj` lines 11 to 29. The container working directory is already `/app/` (`Parchwell.Enrichment.csproj` line 7).
- `GetPlan("C03", "C0" or "C1")` returns, for the member whose `ConsumerProcessingOptions.Role` is `subject`: `ConfigurationId` equal to the phase; `ProcessingDelayMs` 200 (from the profile); `WindowMs` 360000; `SlowMessage` (ordinal 20, duration 90000 ms, mode `inline` for C0 or `pause` for C1); `KillCount` null; `ConsumerSettings` exactly {`max.poll.interval.ms`: `60000`}, taken from the resolved `EnrichmentConsumer` settings whose override origin is the configuration. For the `witness` it returns the same, except `SlowMessage` null and `ConsumerSettings` empty.
- `GetPlan` returns null for any other scenario or configuration, and when loading or resolving fails. The worker then exits with its existing `consumer_plan_invalid` reason (`ConsumerProcessingWorker.cs` lines 34 to 35). No new failure name.

Verifier (`Parchwell.Verifier`):
- `Program.cs` registers a production `IConsumerVerificationPlanSource` before `ValidateConsumerPlan` runs (line 286 at the base commit), for `consumer-verify`.
- `TryGetPlan("C03", "C0" or "C1")` returns `KillCount` null, `CompletionDeadlineMs` 30000, `SubjectSettings` equal to the configuration's resolved `EnrichmentConsumer` effective settings, and `WitnessSettings` equal to the resolved `EnrichmentConsumer` baseline effective settings. Both maps exclude `bootstrap.servers`, `client.id` and `group.id`. With those three keys added back as `ConsumerJudgement.CheckSettings` adds them, each map must equal the `effective_settings` the worker registers for that role: the ordinal-sorted `CreateEnrichmentConsumer().Configuration` of `KafkaConfigurationFactory`, with the subject's one override applied. Test V2 pins this.
- `TryGetPlan` returns false for any other pair. The existing `consumer_plan_invalid` path then applies.
- Both C03 files are linked as `Content` into `Parchwell.Verifier.csproj`. `consumer-verify` must load and resolve C03, including its `EnrichmentConsumer` role. If the verifier's `KafkaConfigurationFactory` has no `EnrichmentConsumer` registration in that mode, the implementer registers one there, with the factory inputs derived as `ConsumerVerificationPlan.cs` line 28 derives the group ID.

Load generator (`Parchwell.LoadGen`):
- `LoadGenHostOptions.cs` lines 94 to 97 and `ProducerRunContext.cs` lines 40 to 42 accept a pair when `ScenarioPhases.TryGetConfiguration` accepts it, or when the scenario is `C03`, the phase is `C0` or `C1`, and the configuration equals the phase. The C03 rule is local to the load generator, following the verifier's consumer-pair rule (`PhaseWireCodec.cs` lines 349 to 351). `ScenarioPhases` does not change, because adding C03 there would make every existing verifier mode accept it ([#506 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/a4c5ea1bec8630359a31d9b096416b2204f85dc7/items/506/plan-consumer-verifier.md) section 3.1; `docs/operations/verifier-contract.md` line 97).
- Every pair rejected today stays rejected with its current failure name, including `phase_configuration_pair_invalid`, `producer_run_context_invalid` and `restart_instance_requires_b10`. C10 and C01 stay rejected until #510.
- Both C03 files are linked as `Content` into `Parchwell.LoadGen.csproj`.

### 3.6 Owning documentation text

`docs/operations/scenario-contract.md` gains a section "Consumer processing declarations", after "Idle observation declarations". It states checks 1 to 8 of section 3.3, the subject-only rule for `EnrichmentConsumer` overrides in a consumer scenario, and the C03 literals. "Profile schema" gains one sentence: "An optional `consumer` mapping holds exactly `processing_delay_ms`, a plain decimal integer from 0 to 60000; a scenario with `consumer_processing` requires it." "Composition and output" gains one sentence: "EnrichmentConsumer replacements also support `max.poll.interval.ms` from 1 to 86400000 (C03)."

`docs/operations/consumer-workflow.md` line 1: the clause "nothing here runs live until the child issue that supplies the C03 plan sources and load generator input merges" becomes "C03 has its scenario, plan sources and load generator input; C10 and C01 do not run live until the child issue that supplies theirs merges".

## 4. External contracts: pull request `c03-observation`

### 4.1 Verification plan

The production verification plan for C03 also carries the scenario's `window_ms` (360000) and the configuration's slow-message mode (`inline` or `pause`). For every other plan both are absent. Names and representation are the implementer's. `ConsumerPlanValidation.IsValid` additionally rejects a plan that has exactly one of the two.

### 4.2 New invalid-run reason

`consumer_slow_message_invalid`. The verifier adds it to the consumer block's `reasons` (status `invalid`) when the plan carries a slow-message mode and either the subject's record does not have exactly one `processed` row with `slow` true, or the witness's record has any. ADR-022 lets an item plan add checks its decision rules need (docs/blueprint.md line 786). It joins the closed reason list of `docs/operations/verifier-contract.md` line 128 and the judge's reason names (`ConsumerPhaseEvidence.cs` line 29).

### 4.3 `membership` object

The consumer block in `result.json` gains a property `membership`, after `counts`. It is present exactly when the block has `counts` (status `complete_valid`, final) and the plan carries a slow-message mode. It has exactly these properties, in this order:

| Property | Type | Value |
|---|---|---|
| `slow_partition` | integer | `partition` of the subject's one `slow` processed row |
| `slow_offset` | Int64 | `offset` of that row |
| `slow_processed_rows` | integer | number of `processed` rows, both members, with that partition and offset |
| `subject_lost_rows` | integer | subject `partition_event` rows with `event` `lost`, `row_sequence` above the subject's `FirstReadLastRowSequence` in the start snapshot, and event time before the boundary B |
| `subject_revoked_rows` | integer | the same, for `event` `revoked` |
| `witness_assigned_rows` | integer | witness `partition_event` rows with `event` `assigned`, `row_sequence` above the witness's `FirstReadLastRowSequence`, and event time before B |
| `witness_assigned_slow_partition_during_slow` | boolean | true when a witness `assigned` row whose partitions include `slow_partition` has an event time at or after the slow row's `processing_started_utc` and at or before its `processing_ended_utc` |
| `subject_commit_entries_after_lost` | integer | see below |

Boundary B is the subject's `registered_utc` plus the plan's `window_ms`. The worker starts its window after it registers (`ConsumerProcessingWorker.cs` lines 45 to 55), so every row a member writes when it closes the consumer has an event time at or after B for the subject. The witness starts after the subject (`consumer_repeat.py` lines 94 to 101), so its close rows are later still. This is a reading of the code, not a measurement.

`subject_commit_entries_after_lost`: take the subject's first `lost` row counted in `subject_lost_rows`, L. Count the successful entries, as ADR-022 defines success (docs/blueprint.md line 754), in subject `commit` rows whose `row_sequence` is above L's and below that of the subject's first `assigned` row after L. If there is no such `assigned` row, count up to the subject's last row with event time before B. Count only entries whose partition is in L's partitions. With no counted `lost` row, the value is 0.

The judge's consumer projection (`ConsumerPhaseEvidence.cs` line 37, `ProjectedNames`) includes `membership` when the block has it, after `counts`, as the block stated it. The judge rejects a block or projection where `membership` is present without `counts`, is absent when the scenario is C03 and `counts` is present, or breaks the table above. No phase-level aggregate of `membership` is published. The root `consumer` object of the candidate is unchanged.

`docs/operations/verifier-contract.md` lines 128 and 136 state these rules in the same style.

## 5. Requirements, hypotheses and expected outcomes

### 5.1 Requirements: `c03-inputs`

- R1.1 WHEN `ScenarioLoader` loads `C03` THEN it returns a definition whose `consumer_processing` holds window 360000, deadline 30000 and the two slow messages of section 3.1, and whose profile holds `processing_delay_ms` 200.
- R1.2 WHEN `ScenarioResolver` resolves C03 THEN `C0` and `C1` each have `EnrichmentConsumer` `max.poll.interval.ms` `60000` with an override origin, and every other setting of every role equals its baseline.
- R1.3 WHEN any of checks 1 to 11 of section 3.3 fails THEN loading fails with exactly the code and path given there.
- R1.4 WHEN any scenario on `main` other than C03 is loaded and resolved THEN the result is unchanged from the base commit.
- R1.5 WHEN a scenario declares `EnrichmentConsumer` `max.poll.interval.ms` THEN resolution accepts integers 1 to 86400000 for C03 only, and rejects 0, 86400001, a quoted number, the role `LoadGenProducer`, and any other item with the existing failure names.
- R1.6 WHEN the worker's production source is asked for `C03`/`C0` or `C03`/`C1` THEN it returns the subject and witness plans of section 3.5.
- R1.7 WHEN it is asked for any other scenario or configuration, or loading fails THEN it returns null and the worker exits 1 with `consumer_plan_invalid`.
- R1.8 WHEN `BuildConsumerProcessingHost` runs with no `configure` registration THEN `IConsumerProcessingPlanSource` resolves to the production source; WHEN `configure` registers one THEN that one resolves.
- R1.9 WHEN the verifier's production source is asked for `C03`/`C0` or `C03`/`C1` THEN it returns the plan of section 3.5, and `ConsumerPlanValidation.IsValid` accepts it with the verifier's existing `consumer-verify` budgets.
- R1.10 WHEN it is asked for any other pair THEN `TryGetPlan` returns false and `consumer-verify` fails at startup with `consumer_plan_invalid`.
- R1.11 WHEN the subject or witness registers the settings the worker applies for C03 THEN the verifier plan for that role, with the three reserved keys added, equals them key for key and value for value.
- R1.12 WHEN the load generator validates `C03` with phase `C0` and configuration `C0`, or `C1` and `C1` THEN it accepts the pair in `LoadGenHostOptions` and in `ProducerRunContext`.
- R1.13 WHEN it validates `C03` with a mismatched configuration, a phase other than `C0` or `C1`, the B10 restart Job name suffix `loadgen2`, or the scenario `C10` or `C01` THEN it rejects with the failure name it uses today.
- R1.14 WHEN the load generator loads and resolves `C03` THEN its `LoadGenProducer` settings for `C0` and `C1` equal the `LoadGenProducer` baseline.
- R1.15 WHEN the three images are published THEN each contains `scenarios/C03/scenario.yaml` and `scenarios/profiles/c03-consumer.yaml` under its application directory.
- R1.16 The Enrichment project's only new reference is the project reference to `Parchwell.Scenarios`. The architecture tests pass unchanged: `Parchwell.Scenarios` is not a service (`tests/Parchwell.ArchitectureTests/ProjectModel.cs` lines 53 to 67).
- R1.17 WHEN the worker plan for C03 is applied THEN the worker's existing validation passes: `WindowMs` 360000 is greater than `DurationMs` 90000; `pause` has effective `enable.auto.offset.store` `false` (baseline); effective `max.poll.interval.ms` 60000 is at least `session.timeout.ms` 30000 (`ConsumerProcessingPlan.cs` lines 24 to 44).
- R1.18 The #507 bounds hold: `WindowMs` 360000 does not exceed 1620000, and `CompletionDeadlineMs` 30000 is below 40000 (#507 plan section 3.7).
- R1.19 `scenarios/C03/expected.yaml` equals section 5.3 byte for byte.
- R1.20 WHEN `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check` reports the pull request as relevant to the mutation workload THEN the implementer runs `c5_runtime.py renew` with a basis naming this plan, at most once. If `check` then passes, continue. If it reports that new measurement is needed, push -wip and hand off to the next session (cap 1; exhaustion action `push -wip and hand off to the next session`).

### 5.2 Requirements: `c03-observation`

- R2.1 WHEN a C03 repeat's consumer block is `complete_valid` and final THEN it has `membership` with exactly the section 4.3 properties and values.
- R2.2 WHEN the plan has no slow-message mode (C10 and C01 later; every test plan without one today) THEN no block has `membership`, and every existing block is byte for byte unchanged.
- R2.3 WHEN the plan has a slow-message mode and the subject has zero or two `slow` processed rows, or the witness has one THEN status is `invalid` with `consumer_slow_message_invalid`.
- R2.4 WHEN status is `invalid` or `incomplete` THEN no `membership` is written, as no `counts` is written today.
- R2.5 WHEN the judge reads a C03 block or projection with `counts` THEN it requires `membership` and validates it under section 4.3. WHEN `membership` is present without `counts`, or in a non-C03 block THEN the slot fails J3.
- R2.6 WHEN the judge writes a C03 repeat projection THEN `membership` follows `counts`, copied as the block states it.
- R2.7 The candidate root `consumer` object, the J10 phase aggregate and every non-consumer candidate stay unchanged.
- R2.8 `ConsumerPlanValidation.IsValid` rejects a plan with a window and no slow-message mode, or a mode and no window.
- R2.9 Mutation qualification follows R1.20 with the same cap and exhaustion action.

### 5.3 Hypotheses and `scenarios/C03/expected.yaml`

The verdict vocabulary is confirmed, contradicted, undocumented and untestable (factory.md "Evidence pack"). Hypothesis outcomes follow the B04 form (`holds_when`, `contradicted_when`, `otherwise: inconclusive`). No code reads this file (ADR-014). The verdict pull request applies it.

This is the exact content of `scenarios/C03/expected.yaml`. Hari approves it with this plan (issue body; decision 6895535291 point 4). It is open question Q5.

```yaml
schema_version: 1
id: C03
subject: >-
  librdkafka v2.16.0 documents that if max.poll.interval.ms is exceeded the
  consumer is considered failed and the group rebalances, and lists KIP-62
  background heartbeats as supported. Microsoft keeps max.poll.interval.ms at
  300000 for Event Hubs and requires it to exceed session.timeout.ms.

assessment_rules:
  counted_repeats: complete_valid only
  repeats_per_configuration: 5
  replacements: 0
  evidence: >-
    The verifier consumer block of each repeat: status, counts and membership
    (docs/operations/verifier-contract.md). A repeat that is not
    complete_valid has no counts and no membership.
  scaled_limit: >-
    The subject runs max.poll.interval.ms 60000, not the 300000 baseline, and
    its slow message lasts 90000 ms, not 6 minutes. session.timeout.ms stays
    30000 and heartbeat.interval.ms stays 3000. The witness runs the baseline.

configurations:
  C0:
    H1_poll_limit_loses_partitions:
      question: >-
        Inline slow message, no Consume call for 90000 ms: does the subject
        lose its partitions while its heartbeats continue, and are messages
        processed again?
      holds_when: >-
        All 5 repeats complete_valid, and in each: membership.subject_lost_rows
        >= 1, membership.witness_assigned_slow_partition_during_slow is true,
        and counts.processed_again >= 1.
      contradicted_when: >-
        All 5 repeats complete_valid, and in at least one:
        membership.subject_lost_rows = 0 and membership.subject_revoked_rows = 0.
      otherwise: inconclusive
    H3_no_commit_on_lost:
      question: >-
        After the subject's partitions are lost, is any offset committed for
        them before the subject is assigned partitions again (K-28)?
      holds_when: >-
        H1 holds, and in each repeat membership.subject_commit_entries_after_lost = 0.
      contradicted_when: >-
        All 5 repeats complete_valid, and in at least one with
        membership.subject_lost_rows >= 1:
        membership.subject_commit_entries_after_lost >= 1.
      otherwise: inconclusive
    consumer_loss:
      mode: recorded_only
      value: counts.committed_never_processed per repeat
  C1:
    H2_pause_keeps_partitions:
      question: >-
        Paused for 90000 ms while still calling Consume: does the subject keep
        its partitions (K-55)?
      holds_when: >-
        All 5 repeats complete_valid, and in each: membership.subject_lost_rows
        = 0, membership.subject_revoked_rows = 0,
        membership.witness_assigned_rows = 0, membership.slow_processed_rows = 1
        and counts.processed_again = 0.
      contradicted_when: >-
        All 5 repeats complete_valid, and in at least one:
        membership.subject_lost_rows >= 1 or membership.subject_revoked_rows >= 1.
      otherwise: inconclusive
    consumer_loss:
      mode: recorded_only
      value: counts.committed_never_processed per repeat

not_tested:
  K-47:
    question: Does consumer token refresh run only inside Consume?
    reason: >-
      Dropped by decision 6894534441 point 3: the poll limit removes the
      consumer from the group long before an Entra token expires, and F01
      covers the refresh handler on consumers that call Consume.
    publication: published as not tested
    decision: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441

verdict:
  confirmed_when: H1 holds and H2 holds.
  contradicted_when: H1 is contradicted or H2 is contradicted.
  no_verdict_when: neither of the above; the run is investigated, not published.
  smoke_run: never published as a result; it gives no verdict.
  separate_lines: H3 and consumer_loss are published on their own lines and do not change the verdict.
  text_must_state:
    - >-
      Heartbeats come from librdkafka's background thread and keep the member
      alive against session.timeout.ms whether or not the application calls
      Consume. max.poll.interval.ms needs Consume calls. A member that
      heartbeats but does not call Consume within that interval leaves the
      group; a paused member that keeps calling Consume stays.
    - The scaled limit in assessment_rules.scaled_limit.
    - K-47 was not tested.

sources:
  - https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md
  - https://github.com/confluentinc/librdkafka/blob/v2.16.0/INTRODUCTION.md
  - https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka.c
  - https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_cgrp.c
  - https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_int.h
  - https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations
```

Why each rule observes what decision 6894564479 point 1 asks:
- Inline case. A `lost` row is the client's own report that it lost its partitions. librdkafka's poll-limit timer leaves the group and revokes with `lost` true (source 3, `rdkafka_cgrp.c` lines 5735 to 5807). The witness taking the slow message's partition during the slow message shows the group rebalanced while the subject was still alive and heartbeating.
- Pause case. Zero `lost` and `revoked` rows, and zero witness assignments before B, show that no rebalance took place. One processed row for the slow message shows it was not handed to the witness.
- K-28. This inference is not documented. On a lost assignment librdkafka has no assigned partitions left to auto-commit, so a successful commit entry for a lost partition, after L and before the next assignment, would be a commit on lost. The ordering of callbacks within one `Consume` call is not documented, so this rule rests on member-local row order (ADR-022: "The member-local row sequence orders durable observations only"). It is listed under Q2.

## 6. Invariants and safety rules

- The produce side keeps ADR-012 and ADR-013. No change to the load generator's produce behaviour, ledger or evidence recorders.
- `expected.yaml` is never a code input (ADR-014). No code, test or script reads it, apart from the byte-equality test X1.
- No process sends a signal. C03 has no kill count (ADR-022 "Self-termination"; ADR-020).
- No commit is made to produce evidence (ADR-022 "Record").
- No change to the chart, workflow, phase scripts, identities, roles, network or infrastructure. This plan does not change live infrastructure, so the rule in factory.md "Item lifecycle" step 1 on live smoke before approval does not apply to it. Its smoke run is the #507 design's live smoke.
- No secret, key, connection string, tenant ID or subscription ID enters the repository.
- Hard safety rules 1 to 9 are unchanged. No agent dispatches `scenario.yml`; section 9 states who does.

## 7. Test map

### 7.1 Standing fixture rule

When an existing test's fixture is rejected by a check this plan adds, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The pull request's implementation notes list each such change with the check that forces it. The cold review and the Codex review check each listed change. Model: [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.

Known cases: any test that pins the full `KafkaScenarioOverridePolicy.Settings` list, and any test that enumerates the scenario directories or the `Content` items. These take option (a): the new entry or row is added and nothing is removed.

### 7.2 `c03-inputs`

Test names follow `Given_<state>_When_<action>_Then_<outcome>` (docs/engineering/dotnet.md). Literals are exact.

| ID | Requirement | Project | Input | Expected |
|---|---|---|---|---|
| S1 | R1.1 | Scenarios | committed C03 files | window 360000; deadline 30000; C0 (20, 90000, `inline`); C1 (20, 90000, `pause`); profile delay 200 |
| S2 | R1.2 | Scenarios | resolve committed C03 | C0 and C1 `EnrichmentConsumer` `max.poll.interval.ms` = `60000` with origin C03/C0 or C03/C1; all other settings equal baseline |
| S3 | R1.3 checks 1 to 8 | Scenarios | in-memory variants of C03: `consumer_processing` on B04; C03 without it; with `idle_observation` added; extra key `extra: 1`; `window_ms: 359999`, `360001` and `"360000"`; `completion_deadline_ms: 29999`; configurations missing `C1`; `slow_message` missing `mode`; `ordinal: 19` and `21`; `duration_ms: 89999`; C0 mode `pause`; `repeats: 6`; roles without `VerifierConsumer`; C1 overrides `max.poll.interval.ms: 59999` | in order: `consumer_processing_unexpected`, `consumer_processing_required`, `consumer_processing_combined`, `consumer_processing_shape`, `consumer_processing_literal` (three times), `consumer_processing_literal`, `consumer_processing_configurations`, `consumer_processing_shape`, `consumer_processing_literal` (twice), `consumer_processing_literal`, `consumer_processing_literal`, `consumer_processing_experiment` (three times), each with the section 3.3 path |
| S4 | R1.3 checks 9 to 11 | Scenarios | profile `consumer: {processing_delay_ms: 0}`; `60000`; `-1`; `60001`; `"200"`; `consumer: {}`; `consumer: {processing_delay_ms: 200, x: 1}`; C03 pointing at `saturation-ramp` | load; load; `profile_consumer_range`; `profile_consumer_range`; `profile_consumer_range`; `profile_consumer_shape`; `profile_consumer_shape`; `consumer_profile_required` |
| S5 | R1.4 | Scenarios | every committed scenario except C03 | load and resolve with the same results as at the base commit (the existing tests stay unchanged and pass) |
| K1 | R1.5 | Kafka or Scenarios | C03 C0 `max.poll.interval.ms` 1, 86400000, 0, 86400001, `"60000"`; the key under `LoadGenProducer`; the key on B04 | accept; accept; `setting_out_of_range`; `setting_out_of_range`; the existing plain-scalar failure; the existing wrong-role failure; the existing unsupported-item failure |
| E1 | R1.6 | Enrichment | production source over the committed files; `C03`/`C0`, role `subject` | delay 200; window 360000; slow (20, 90000, `inline`); kill null; settings {`max.poll.interval.ms`: `60000`} |
| E2 | R1.6 | Enrichment | `C03`/`C1`, role `subject`; then `C03`/`C0` and `C03`/`C1`, role `witness` | slow (20, 90000, `pause`), same settings; then slow null and settings empty, delay 200, window 360000 |
| E3 | R1.7 | Enrichment | `C10`/`C0`; `C01`/`C0`; `C03`/`C2`; source root with no C03 file | null each time; the host run exits 1 and writes `consumer_processing_failed reason=consumer_plan_invalid` |
| E4 | R1.8 | Enrichment | build the host without `configure`; then with a test source in `configure` | resolves the production type; resolves the test source |
| E5 | R1.17 | Enrichment | each E1 and E2 plan through `ApplyAndValidate` on the baseline config | no exception |
| V1 | R1.9 | Verifier | production source, `C03`/`C0` and `C03`/`C1` | kill null; deadline 30000; subject map has `max.poll.interval.ms` `60000`; witness map has `300000`; neither has the three reserved keys; `IsValid` true with the chart's `consumer-verify` budgets |
| V2 | R1.11 | Verifier | for each role and configuration: the plan map plus the three reserved keys; and `CreateEnrichmentConsumer().Configuration` from a factory with the same inputs, with the subject override applied | equal key for key and value for value |
| V3 | R1.10 | Verifier | `C10`/`C0`, `C01`/`C1`, `C03`/`C2`, `B04`/`C0` | `TryGetPlan` false; `consumer-verify` startup fails with `consumer_plan_invalid` |
| V4 | R1.9 | Verifier | `consumer-verify` host setup for C03/C0 with the committed files | the scenario loads and resolves with no exception, `LoadGenProducer` included |
| L1 | R1.12 | LoadGen | host options and run context: `C03` `C0`/`C0`; `C03` `C1`/`C1` | accepted |
| L2 | R1.13 | LoadGen | `C03` `C0`/`C1`; `C03` `C2`/`C2`; `C03` `probe`/`C0`; `C10` `C0`/`C0`; `C01` `C0`/`C0`; `C03` `C0`/`C0` with Job name suffix `loadgen2` | `phase_configuration_pair_invalid` (options) and `producer_run_context_invalid` (context) for the first five; `restart_instance_requires_b10` for the last |
| L3 | R1.14 | LoadGen | the load generator's existing scenario-to-run-plan path for C03 C0 and C1 | a plan whose `LoadGenProducer` settings equal the baseline; no `selected_configuration_not_declared_by_scenario` and no `scenario_does_not_declare_loadgen_producer` |
| X1 | R1.19 | Scenarios | committed `scenarios/C03/expected.yaml` | SHA-256 equal to the value the implementer computes from section 5.3's block with LF endings and a final newline, recorded in the test |
| P1 | R1.15 | build | `dotnet publish` of the three services | each output has both C03 files; acceptance command A3 |

Boundaries: S3 covers 1 below and 1 above each literal. S4 covers 0, 60000, -1 and 60001. K1 covers 1, 86400000, 0 and 86400001.

### 7.3 `c03-observation`

Fixtures are verifier unit-test records, built with the existing consumer test helpers. Times are UTC. Baseline fixture F: the subject registers at `2026-01-01T00:00:00.0000000Z`, so B is `2026-01-01T00:06:00.0000000Z`; subject row 0 is `assigned` [0..15] and witness row 0 is `assigned` [16..31], both at `00:00:30`; the start snapshot has `FirstReadLastRowSequence` 0 for both members; the subject's one `slow` processed row is partition 3, offset 10, `processing_started_utc` `00:02:00`, `processing_ended_utc` `00:03:30`. Times below are on 2026-01-01.

| ID | Requirement | Case | Expected `membership` or status |
|---|---|---|---|
| O1 | R2.1 | F, inline: subject `lost` [0..15] at `00:03:31` (after the slow row), `assigned` [0..7] at `00:03:40`; witness `assigned` [0..31] at `00:03:01`, `revoked` at `00:03:39`, `assigned` [8..31] at `00:03:40`; witness also processes partition 3 offset 10; no subject commit rows between its `lost` and `assigned` rows | `slow_partition` 3, `slow_offset` 10, `slow_processed_rows` 2, `subject_lost_rows` 1, `subject_revoked_rows` 0, `witness_assigned_rows` 2, `witness_assigned_slow_partition_during_slow` true, `subject_commit_entries_after_lost` 0 |
| O2 | R2.1 | O1 plus one subject commit row between its `lost` and `assigned` rows with entries partition 3 offset 5 (error 0) and partition 20 offset 7 (error 0), overall error 0 | `subject_commit_entries_after_lost` 1 (partition 20 is not in L's partitions) |
| O3 | R2.1 | O2 with the partition 3 entry error code 25 | `subject_commit_entries_after_lost` 0 |
| O4 | R2.1 | F, pause: no subject `lost` or `revoked` rows before B; subject `revoked` [0..15] at exactly `00:06:00` (B) and witness `assigned` [0..31] at `00:06:01`; slow row processed only by the subject | `slow_processed_rows` 1, `subject_lost_rows` 0, `subject_revoked_rows` 0, `witness_assigned_rows` 0, `witness_assigned_slow_partition_during_slow` false |
| O5 | R2.1 boundary | O4 with the subject `revoked` row at `00:05:59.9999999Z` | `subject_revoked_rows` 1 |
| O6 | R2.1 boundary | O1 with the witness `assigned` [0..31] row at exactly `00:02:00` (start) and again at exactly `00:03:30` (end); then at `00:03:30.0000001Z` only | true; true; false |
| O7 | R2.3 | plan with mode `inline`; subject with no `slow` row; subject with two; witness with one | `invalid`, reasons [`consumer_slow_message_invalid`], no `membership` |
| O8 | R2.2 | every existing consumer judgement test (plans without a mode) | unchanged expected bytes; no `membership` |
| O9 | R2.4 | O1 with the committed-offset read `kafka_exception` | `incomplete`, no `counts`, no `membership` |
| J1 | R2.5 | judge: C03 block with `counts` and valid `membership`; without `membership`; `membership` with an extra property; `membership` on a C10 block; `membership` without `counts` | accepted; J3 fails; J3 fails; J3 fails; J3 fails |
| J2 | R2.6, R2.7 | judge candidate from five O1 slots | each repeat projection has `membership` after `counts`, equal to the block's; the root `consumer` has only `status`, `reasons`, `counts` |
| P2 | R2.8 | `IsValid` with window and no mode; mode and no window; both; neither | false; false; true; true |

## 8. Deployment assumptions

Each is an assumption about the deployed runtime that no offline test in this plan covers. Each is a blocking finding until Hari accepts it by name (factory.md "Item lifecycle" step 1). All are listed under Q4. No evidence has been taken for any of them. The smoke run of section 9 takes it on fresh connections: every repeat has new member pods, a new group ID and new clients, under the C03 load profile.

- DA1. The Event Hubs Kafka endpoint accepts the subject's `LeaveGroup` after the poll limit, and the group rebalances so that the witness is assigned the subject's partitions while the subject is still running. Offline evidence: none for Event Hubs. Code evidence: librdkafka leaves the group on poll-limit expiry for the classic protocol (source 3, `rdkafka_cgrp.c` lines 5780 to 5807), which the baseline uses (`group.protocol` `classic`, `BaselineSettingsCatalog.cs` line 145).
- DA2. On Event Hubs, a consumer that calls `Consume` every 100 ms while all its partitions are paused keeps its membership past `max.poll.interval.ms`. Code evidence: every `rd_kafka_consumer_poll` call updates the last-poll time whether or not partitions are paused (source 3, `rdkafka.c` lines 5082 to 5132 and 5183 to 5192; `rdkafka_int.h` lines 1405 to 1425). Confluent.Kafka 2.16.0 `Consume(int)` calls `ConsumerPoll` (source 5, `Consumer.cs` line 911).
- DA3. Start readiness. Each repeat's first produced message is fetched by its member. The load generator starts only after the readiness poll sees both members' `assigned` rows and the start-line Job completes (`consumer_repeat.py` lines 102 to 109). It does not wait for the initial `auto.offset.reset=latest` lookup. The #505 component run saw that lookup finish after the `assigned` row ([observation 6903715635](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505#issuecomment-6903715635)). If that happens live, messages produced before the lookup finishes are skipped and count as `committed_never_processed`. Smoke check: section 9, check 6.
- DA4. Fidelity across processes. The `effective_settings` a live member registers equal the verifier plan maps. Test V2 shows it offline against the same factory. A live run could differ only if the image's factory inputs differ, which `consumer_configuration_mismatch` would show.
- DA5. Time. A repeat with a 360000 ms window, plus member collection and `consumer-verify`, averages at most 525 s, so repeat 5 starts within the 2100 s bound (#507 plan line 91). Estimate: about 450 s per repeat, made on 2026-11-17. Basis: the 360 s window, plus an assumed 90 s for collection and `consumer-verify`, which no run has measured. If repeat 5 is recorded `not_attempted` with `orchestration_deadline`, the conflict goes to Hari; the budget is not raised (#507 decision 6925863575 point 1).
- DA6. Window coverage. Within 360000 ms from each member's start, readiness, the start line, load generator start, the 60 s load, the 90 s slow message and the witness's catch-up all finish, and every partition's commit reaches the end line. Estimate, made on 2026-11-17: about 250 s of the window is used, assuming readiness and both Jobs start within 120 s; none of these durations has been measured. A shortfall shows as `consumer_commit_frontier_short`.
- DA7. The #507 assumptions DA1 to DA4 (workflow read of `consumer-record`, pod fit on the workload node, identity webhook injection, role assignments) and #506 DA1 and DA2 (the verifier's committed-offset read without joining the group, and the start-line consumer reads). Hari accepted the #507 ones by name for #507 ([decision 6925863575](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6925863575) point 3); their first live evidence is this smoke. #507 DA5 (self-kill) is not exercised by C03 and stays with #510.

## 9. Acceptance commands and evidence

Before each pull request opens:
- A1. `./scripts/verify-dotnet.sh`. Evidence: exit 0; warnings as errors; all tests pass, including every test in section 7 for that pull request.
- A2. `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check` against the base. Evidence: pass, after at most one `renew` under R1.20 or R2.9.
- A3 (`c03-inputs` only). `dotnet publish` of `src/Parchwell.Enrichment`, `src/Parchwell.Verifier` and `src/Parchwell.LoadGen`, then list `scenarios/C03/scenario.yaml` and `scenarios/profiles/c03-consumer.yaml` in each publish directory. Evidence: six paths listed.
- A4 (`c03-inputs` only). `sha256sum scenarios/C03/expected.yaml` equals the value test X1 pins.
- A5. The required checks pass on the final head; the Codex review and security review of the head commit are complete with no P0, P1 or High finding open; the Claude review gate of decision 6894587233 point 2 has passed. Both pull requests touch protected paths, so Hari merges.

After `c03-inputs` merges, the smoke run. It is an Azure dispatch, which decision 6895535291 point 4 reserves to Hari: Hari dispatches it, or names who does. Form: `scenario.yml` on `main`, input `scenario=C03`. This runs phases C0 and C1, five repeats each (#507 plan line 14). Prerequisites: `c03-inputs` merged; no other dispatch or ephemeral environment active (hard safety rule 5). The smoke run is never published. Its evidence is the run's phase report, candidate and pod artifacts. Smoke checks, read by the session Hari names:
1. Every repeat has the six records `consumer-subject`, `consumer-witness`, `consumer-readiness`, `start-line`, `loadgen` and `verify`, and none is `not_attempted` (#507 deferred item 1; DA5).
2. Readiness is reached in every repeat (#507 DA1 to DA4).
3. `consumer-verify` writes a consumer block for every repeat whose producer side is `complete_valid`, and `offset_read.outcome` is `success` (#506 DA1).
4. No block has `consumer_configuration_mismatch` (DA4).
5. Every block's status is `complete_valid`, or the session reports each other status with its reasons (DA6).
6. `counts.committed_never_processed` is 0 in every `complete_valid` block. A nonzero count is reported with the partitions and offsets from the collected records, as possible DA3 evidence, before any batch dispatch (DA3).
7. The judge step runs and `phase.py candidate` accepts the candidate (#521 plan line 219).

Exhaustion action for a failed smoke check: `record the gap and continue` for #506 DA1 and DA2, as the #506 plan sets. For every other check: hand off to Hari with the run link and the failed check; no batch dispatch.

The batch dispatch is the same workflow input. Prerequisites: smoke checks 1 to 7 pass, or Hari accepts each failure by name; `c03-observation` merged; #531 merged ([#499 decision 6926376436](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6926376436)). Hari dispatches it, or names who does.

Mutation acceptance (factory.md "Evidence pack"). Both pull requests change mutation inputs: Kafka and Scenarios sources, lock files, and verifier sources. One passing `verifier-c5-mutation.yml` run on `main`, at a commit that includes both merges and the #521 merge, satisfies all three. The implementing session of `c03-observation` dispatches it once after that merge. This workflow runs on GitHub runners and reaches no Azure resource; it is not a scenario dispatch. Cap: 1 dispatch. Exhaustion action: `record the gap and continue`; open a fix issue if none covers the failure; C03 evidence-pack publication stays blocked. That run gates the evidence pack, not the smoke or batch run.

## 10. Deferred work

1. Smoke run. On the verdict path. Deferral permitted because it can only run after `c03-inputs` merges. Responsible: Hari, or whoever he names. Stage: after `c03-inputs` merges. Prerequisites and checks: section 9. Workload bound: one dispatch; exhaustion action as section 9. Completion evidence: the run link and the seven checks reported on #508. Gate it blocks: the batch dispatch.
2. Batch dispatch. On the verdict path. Permitted as above. Responsible: Hari, or whoever he names. Stage: after the smoke checks, `c03-observation` and #531. Bound: one dispatch. Completion evidence: the run link. Gate it blocks: the C03 verdict pull request (#499 split entry 8).
3. Mutation acceptance run. On the verdict path. Permitted because it must run on `main` after merge. Responsible: the implementing session of `c03-observation`. Bound and exhaustion action: section 9. Completion evidence: `summary.json` with `measurement_complete: true` and `break_passed: true`. Gate it blocks: the C03 evidence pack.
4. Override entries and plan-source extension for C10 and C01. Not on C03's verdict path. Responsible: #510 ([decision 6899939624](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6899939624) point 1).
5. README row and report text. On the verdict path. Responsible: the C03 verdict pull request, which applies `text_must_state`.

## 11. Plan-check questions (factory.md "Item lifecycle" step 1)

(a) Effort that scales with a number unknown at planning time: the mutation qualification renewal (R1.20, R2.9: cap 1 renew each; exhaustion `push -wip and hand off to the next session`); the post-merge mutation run (cap 1; `record the gap and continue`); the smoke and batch dispatches (one each, by Hari); fixture changes under the standing fixture rule, which are bounded by the existing tests that pin the policy list or the scenario inventory. The implementer counts them and lists each one.

(b) Stricter than the parent plan: the parent is the #499 proposal, which pins no checks. Against the predecessor contracts this plan is stricter in three places. The `consumer_processing` declaration pins literals, following `IdleObservationDefinitionReader`, so the approved values cannot drift without a plan amendment. The verifier adds `consumer_slow_message_invalid`, because the decision rules depend on one slow message. The judge requires `membership` on C03 blocks, so a candidate without the observation cannot be used for a C03 verdict.

(c) Tests or checks that depend on code, data or behaviour neither on `main` nor in the pull request: the decision rules depend on live Event Hubs behaviour (DA1, DA2, DA3, DA5, DA6). Each is listed in section 8 and under Q4. Offline tests depend only on code on `main` or in the pull request. `c03-observation`'s tests depend on the verification plan source that `c03-inputs` adds, which merges first.

## 12. Sources

Tools: the librdkafka and Confluent.Kafka files were fetched at their release tags with `curl` from `raw.githubusercontent.com` in this session. The Microsoft page was read with the Microsoft Learn documentation fetch tool, on 2026-11-17.

1. librdkafka v2.16.0 CONFIGURATION.md, https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md. Line 131: `session.timeout.ms`, default 45000; "If no hearts are received by the broker for a group member within the session timeout, the broker will remove the consumer from the group". Line 132: `heartbeat.interval.ms`, default 3000. Line 137: `max.poll.interval.ms`, range 1 to 86400000, default 300000; "If this interval is exceeded the consumer is considered failed and the group will rebalance"; "The interval is checked two times per second".
2. librdkafka v2.16.0 INTRODUCTION.md, line 2529: "KIP-62 - max.poll.interval and background heartbeats | 0.10.1.0 | Supported" (the control session's pointer, [6897727430](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/508#issuecomment-6897727430), read at the tag).
3. librdkafka v2.16.0 source. The heartbeat is not on the application's thread: `rd_kafka_thread_main` (`src/rdkafka.c` line 2434) is the internal main thread, created at line 3003, and it calls `rd_kafka_cgrp_serve` (line 2486). `rd_kafka_cgrp_serve` sends `rd_kafka_cgrp_heartbeat` every `heartbeat.interval.ms` in the steady state (`src/rdkafka_cgrp.c` lines 6808 to 6816). The poll-limit check is a timer on the same main thread (`rdkafka_cgrp.c` lines 5735 to 5807): on expiry it logs "leaving group", raises `RD_KAFKA_RESP_ERR__MAX_POLL_EXCEEDED`, and for the classic protocol calls `rd_kafka_cgrp_leave` and `rd_kafka_cgrp_revoke_all_rejoin_maybe` with `lost` true. `rd_kafka_max_poll_exceeded` measures from `rk_ts_last_poll` (`src/rdkafka_int.h` lines 1333 to 1355), which `rd_kafka_app_polled` sets on each poll (lines 1405 to 1425). `rd_kafka_consumer_poll` (`rdkafka.c` line 5183) reaches that through `rd_kafka_consume0` (lines 5082 to 5132), with no condition on paused partitions. This is my reading of the source. librdkafka's documentation does not state the thread model in these words.
4. Microsoft Learn, "Apache Kafka client configurations for Azure Event Hubs", https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations, librdkafka consumer table: `session.timeout.ms` recommended 30000, range 6000 to 300000; `max.poll.interval.ms` 300000 (keep default), "Must be > `session.timeout.ms`"; `heartbeat.interval.ms` 3000.
5. Confluent.Kafka v2.16.0 `src/Confluent.Kafka/Consumer.cs`, https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Consumer.cs: line 911, `Consume(int)` calls `kafkaHandle.ConsumerPoll`; lines 288 to 292, a lost assignment calls the partitions-lost handler when one is set. `ErrorCode.cs` line 296: `Local_MaxPollExceeded = -147`.
6. Repository files on `main` at the base commit, cited inline by path and line.

## 13. Open questions for Hari

Each has a recommended answer. Q1 to Q3 are design choices of the kind the standing decision lets the control session adopt (decision 6895535291 point 2). Q4 to Q6 touch expected outcomes, Azure dispatch or named risk, which point 4 reserves to Hari.

- Q1. Scaled poll limit. Recommended: the subject runs `max.poll.interval.ms` 60000 with a 90000 ms slow message, as section 3.1 states. Alternative: keep the 300000 baseline with a 360000 ms slow message (the blueprint's 6-minute outlier). That needs a window of about 540 s, so five repeats would not fit the 2100 s start bound, and the 7200 s phase budget would have to rise. This is an estimate with that arithmetic as its basis, made on 2026-11-17. Effect on the verdict: the recommended run tests the mechanism at a scaled limit, and the published text says so (`scaled_limit`). It does not show the 300000 value itself.
- Q2. Observation and K-28. Recommended: `c03-observation` adds the `membership` object to the verifier and judge inside this issue, as the second pull request of this plan, and H3 uses the member-local order rule of section 5.3. Alternatives: (a) a new split entry for the observation, which would leave #508 able to run the smoke but unable to reach a verdict; (b) decision rules on the existing outputs only (`processed_again`). Option (b) cannot show the pause case keeping its partitions, because no existing output reports partition events (`ConsumerPhaseEvidence.cs` line 37). The added scope is verifier and judge work beyond split row 5's wording, which is why this needs Hari.
- Q3. How the worker gets C03 data. Recommended: a project reference from `Parchwell.Enrichment` to `Parchwell.Scenarios`, with the C03 files packaged as `Content`, so the worker resolves the scenario in-process as ADR-014 and `scenario-contract.md` "Composition and output" describe. The Code Review Rules flag a new assembly reference as P1, so Codex review will raise it, and the csproj is protected. Alternative: the chart passes the plan values as environment variables. That needs chart, options and chart-test changes, which #507 pinned (chart test T21), and it puts scenario data outside the scenario file.
- Q4. Accept deployment assumptions DA1 to DA7 of section 8 by name, with the smoke run as their first live evidence.
- Q5. Approve `scenarios/C03/expected.yaml` exactly as section 5.3 gives it.
- Q6. Dispatches. Confirm that Hari dispatches the smoke and batch runs, or name who may. Confirm that the smoke is not published. Confirm the single post-merge mutation run in section 9.

## 14. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope. That includes any literal in sections 3.1 to 3.4, any check in section 3.3, any property or rule in section 4, the content of `expected.yaml`, any path in section 2.3, or a test's expected value outside the standing fixture rule. It also stops under R1.20 and R2.9, and when a test shows that a requirement cannot hold as written.

## 15. Size estimate

Estimates, made on 2026-11-17 by comparison with the #505 and #509 pull requests, which touched similar files. They are reported, not enforced (no code is trimmed to meet them).
- `c03-inputs`: about 25 files and about 1100 changed lines, including tests and lock files.
- `c03-observation`: about 8 files and about 500 changed lines, including tests.

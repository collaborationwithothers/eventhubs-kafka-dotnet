Revision: 2

# C10 and C01 scenarios: contract-grade plan (issue 510)

This plan adds the C10 and C01 consumer scenarios and their expected outcomes, in one pull request. C10 asks what is lost and what is processed twice when a consumer kills itself, with offsets stored after processing (the baseline) versus the default automatic offset store. C01 asks how long the group takes to give a killed member's partitions to the other member, with `session.timeout.ms` 30000 (Microsoft) versus 45000 (librdkafka default). Nothing in this plan publishes a result, and two of its design choices (where K-57 is tested, and how C10's automatic-store case is judged) are open questions for Hari in section 13.

Base commit: `main` at `1e3b299d5d73a59e5f4c53275d9e5e7cbed5b3d5`.
Changes in revision 2: the findings of [plan check 7001168119](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-7001168119) and the archive question that the control session raised from [#531 handoff 6956490346](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6956490346), item D2. Section 3.4 pins the shape of the validated value; section 7.2 lists four more superseded assertions; section 3.1's "Why 110" figures are corrected; R17 and R19 are in WHEN/THEN form; C10's `expected.yaml` gains a per-repeat precondition; DA1 names the shared process namespace; Q6 is new.
Short name of the set: `c10-c01-scenarios`. One pull request, named `c10-c01-inputs`.
Implementer: Codex, in a session Hari starts (issue body; [decision 6894614673 on #499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894614673), point 2). The pull request gets the Claude review gate of [decision 6894587233 on #499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894587233), point 2.

Recommended answers. Under [#510 decision 6895538516](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-6895538516), adopting the [#499 standing decision 6895535291](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291) point 2, each question the `grilling` skill would ask is written in section 13 with its recommended answer. This revision is written as if every recommended answer is adopted. Where an answer is not adopted, the sections that question names change in a later revision.

## 1. Goal and authority

Goal: a C10 dispatch and a C01 dispatch of `scenario.yml` on `main`, each running phases C0 and C1 with five repeats, whose verifier and judge output let the expected outcomes of section 5.2 be applied, with a smoke run of each first.

Authority, in creation order (docs/agents/factory.md "Item lifecycle" step 3):

1. The issue body of #510: split entry 7 of #499; `scenarios/C10/` and `scenarios/C01/` definitions and expected outcomes, with a smoke run; "C10 includes K-57 (crash before the first auto-commit with `auto.offset.reset` at its baseline value)"; `expected.yaml` files are protected; planning by Claude with the `grilling` skill and then `plan-pr`; implementation by Codex.
2. #499 decisions named in the issue body: [6894534441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441) (batch; point 2 the self-kill crash mechanism; point 3 K-57 tested with C10), [6894564479](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894564479) (C03 only), [6894587233](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894587233) (split approved; Claude review gate), [6894614673](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894614673) (Claude plans; Codex implements; control session).
3. [#510 decision 6895329362](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-6895329362), adopting [#499 decision 6895328446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895328446) (grilling round 2): outcome classes, completion, independent commit evidence, C01 timing from the subject's last record to the witness's assigned row with both members on one node, the witness runs the baseline.
4. [#510 decision 6895344271](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-6895344271), adopting [#499 decision 6895343227](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895343227) (grilling round 3): SIGKILL at a processed-message count fixed in the scenario, a kill-intent row before the kill, and "Entry 7 (#510) chooses the counts, including one before the first auto-commit for K-57" (point 1).
5. [#510 decision 6895538516](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-6895538516), adopting the standing decision [6895535291](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291). Its point 4 reserves money, Azure dispatch, expected outcomes and verdicts, protected-path merges, scope beyond the approved split and `ready-for-agent` to Hari.
6. [#510 decision 6899940730](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-6899940730), adopting [#499 decision 6899939624](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6899939624): #508 adds the production settings source and scenario-schema and profile changes; "#510 extends them for C10 and C01".
7. [#510 decision 6958522754](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-6958522754), adopting [#499 decision 6958521790](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6958521790): agents merge by merge commit.

Assigned work recorded on #510 by the control session: the override-policy entries for the settings C10 and C01 vary ([control action 6900155071](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-6900155071)).

Design authority: ADR-022 (docs/blueprint.md lines 736 to 797), which records the #499 grilling rounds 1 to 3. In particular: the kill is "When `Consume` returns the subject's Nth message of the repeat, before processing it" (line 757); the C01 timing rule (line 774); the invalid-run conditions (lines 776 to 786); and "An item plan adds only the checks its decision rules need" (line 786).

Deferred items assigned to #510 by approved predecessor plans: the C10 and C01 override entries and plan-source extension ([#508 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/ac335c89168ab054e8e56d642d2ed1465ad84ad9/items/508/plan-c03-scenario.md) section 10 item 4); #507 DA5, the live self-kill ([#508 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/ac335c89168ab054e8e56d642d2ed1465ad84ad9/items/508/plan-c03-scenario.md) DA7: "#507 DA5 (self-kill) is not exercised by C03 and stays with #510").

Predecessors (issue body "Predecessor PRs"): the #507 pull request merged (pull request 536, 2026-11-17T08:35:27Z) and the #509 pull request merged (pull request 527, 2026-11-16T11:08:31Z). Also on `main`: the C03 inputs and observation (pull requests 545 and 547), the consumer phase judge (533) and the consumer evidence archive (539).

What `main` already provides, so this plan does not add it: the worker's kill path (`src/Parchwell.Enrichment/ConsumerProcessing/ConsumerProcessingWorker.cs` lines 39 to 43 and 152 to 169) and its acceptance of `session.timeout.ms` and `enable.auto.offset.store` in a plan (`ConsumerProcessingPlan.cs` lines 30 to 41); the verifier's stop rule for a kill count (`src/Parchwell.Verifier/VerifyRun/Consumer/Judgement/ConsumerJudgement.cs` line 49) and its `reassignment` object (lines 97 and 161 to 168); the judge's `reassignment` rule (`src/Parchwell.Verifier/Judge/ConsumerPhaseEvidence.cs` lines 89, 109 and 145 to 153); the verifier's consumer-pair acceptance of C10 and C01 (`ConsumerVerificationPlan.cs` line 36; `PhaseWireCodec.cs` line 350); the worker identity's acceptance of C10 and C01 (`ConsumerProcessingOptions.cs` line 27); the workflow's two phases C0 and C1 for C10 and C01 (`infra/scripts/phase/phase.py` lines 26 to 27).

Not authorized by this plan: any Azure dispatch (section 9 names who may dispatch only after Hari answers Q5), any change to `expected.yaml` after approval, any verdict, anything under `results/`, and any change to ADR text in `docs/blueprint.md`.

## 2. Scope

### 2.1 Files allowed to change in `c10-c01-inputs`

- `scenarios/C10/scenario.yaml` (new; section 3.1).
- `scenarios/C10/expected.yaml` (new, protected; section 5.2, byte for byte as approved).
- `scenarios/C01/scenario.yaml` (new; section 3.2).
- `scenarios/C01/expected.yaml` (new, protected; section 5.2, byte for byte as approved).
- `scenarios/profiles/c01-consumer.yaml` (new; section 3.3).
- `src/Parchwell.Scenarios/Contracts/ScenarioContracts.cs` and `src/Parchwell.Scenarios/Loading/ConsumerProcessingDefinitionReader.cs` (section 3.4).
- `src/Parchwell.Kafka/Configuration/KafkaScenarioOverrideSetting.cs` (two entries; section 3.5).
- `src/Parchwell.Enrichment/Parchwell.Enrichment.csproj` (protected: three `Content` items), `src/Parchwell.Enrichment/ConsumerProcessing/ScenarioConsumerProcessingPlanSource.cs` (section 3.6).
- `src/Parchwell.Verifier/Parchwell.Verifier.csproj` (protected: three `Content` items), `src/Parchwell.Verifier/Common/Consumer/ScenarioConsumerVerificationPlanSource.cs` (section 3.6).
- `src/Parchwell.LoadGen/Parchwell.LoadGen.csproj` (protected: three `Content` items), `src/Parchwell.LoadGen/Run/Input/LoadGenHostOptions.cs` (section 3.6).
- `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`, only through `c5_runtime.py renew` under requirement R16.
- Tests under `tests/Parchwell.Scenarios.Tests/`, `tests/Parchwell.Kafka.Tests/`, `tests/Parchwell.Enrichment.Tests/`, `tests/Parchwell.Verifier.Tests/`, `tests/Parchwell.LoadGen.Tests/`, under the test map, section 7.1 and section 7.2. The test project files of those five projects (protected) may change only to add `EmbeddedResource` or `Content` items for the new scenario and profile files, in the form their existing items use (for example `tests/Parchwell.Scenarios.Tests/Parchwell.Scenarios.Tests.csproj` lines 13 to 15).
- `docs/operations/scenario-contract.md`: the "Consumer processing declarations" section (lines 147 to 175) and one sentence appended to line 73 (section 3.7).
- `docs/operations/consumer-workflow.md`: one clause of line 1 (section 3.7).
- `docs/blueprint.md` (protected): only the section 12 fold row at line 1275, and only if Hari adopts the recommended answer to Q1 (section 3.7).
- `items/510/state.md` (the item state file, factory.md "Item state file").

`packages.lock.json` files do not change: the plan adds no project or package reference. If `dotnet restore` changes a lock file, the implementer stops (section 14).

### 2.2 Paths that must not change

`src/Parchwell.Scenarios/Contracts/ScenarioPhases.cs`; `src/Parchwell.Scenarios/Loading/DefinitionReader.cs` (`consumer_processing` is already an allowed root field) and `ProfileReader.cs`; the worker's `ConsumerProcessingOptions.cs`, `ConsumerProcessingPlan.cs` and `ConsumerProcessingWorker.cs`; everything under `src/Parchwell.Verifier/` other than the two files in section 2.1, so `ConsumerVerificationPlan.cs`, `ConsumerJudgement.cs`, `ConsumerPhaseEvidence.cs`, `PhaseWireCodec.cs` and `VerifierHostOptionsValidator.cs` stay unchanged; `src/Parchwell.LoadGen/Run/Publication/ProducerRunContext.cs` (it calls `LoadGenHostOptions.IsAcceptedPair`, line 40); everything under `.github/`, `charts/`, `infra/`, `results/`; `scenarios/C03/` and `scenarios/profiles/c03-consumer.yaml`; every other file under `scenarios/`; `Directory.Packages.props` and `Directory.Build.props`; `tests/Parchwell.ArchitectureTests/`; ADR text in `docs/blueprint.md`. No new project, no new NuGet package, no new assembly reference.

## 3. External contracts

### 3.1 `scenarios/C10/scenario.yaml`

Exactly this content, with LF line endings and a final newline:

```yaml
schema_version: 1
id: C10
baseline: Parchwell.Kafka
roles:
  - LoadGenProducer
  - EnrichmentConsumer
  - VerifierConsumer
profile: profiles/c03-consumer.yaml

configurations:
  C0: {}
  C1:
    EnrichmentConsumer:
      enable.auto.offset.store: true

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
      kill_count: 110
    C1:
      kill_count: 110
```

Meaning:
- Phase `C0` runs the baseline: `enable.auto.offset.store` `false`, so the worker stores each offset after it records the message (`ConsumerProcessingWorker.cs` lines 193 to 197; baseline `BaselineSettingsCatalog.cs` line 183). Phase `C1` runs the librdkafka default, `enable.auto.offset.store` `true`, on the subject only.
- In both, the subject sends SIGKILL to itself when `Consume` returns its 110th message, before processing it (ADR-022 "Self-termination", line 757). The witness runs the baseline with no kill.
- The profile is the C03 profile, reused unchanged, so the offline partition evidence of the C03 test W1 applies (section 8, DA3).
- Why 110. In the C03 profile each partition range of 16 partitions receives 120 messages, and every non-empty partition of either range has its first message by that range's 86th message (#508 plan test W1, on `main` as the C03 workload test). A kill at the 110th message leaves 24 messages after the last partition's first message: by the generator's due times, 9.75 s in partitions 0 to 15 (86th message due at 43.75 s, 110th at 53.5 s) and 12.25 s in partitions 16 to 31 (42.5 s and 54.75 s), computed by script in the plan check of 2026-11-19. With `auto.commit.interval.ms` 5000 (baseline, `BaselineSettingsCatalog.cs` line 181), at least one auto-commit timer tick falls in that time if the subject keeps up with arrivals, so every subject partition should have a committed offset when the subject dies. Without that, the witness would start such a partition at the latest offset (`auto.offset.reset` `latest`, line 185), and C10 C0 would count a K-57 loss that is not a store-mode effect. This is an inference from the documented timer and the offline workload, not a measurement; smoke check 9 tests it.

### 3.2 `scenarios/C01/scenario.yaml`

Exactly this content, with LF line endings and a final newline:

```yaml
schema_version: 1
id: C01
baseline: Parchwell.Kafka
roles:
  - LoadGenProducer
  - EnrichmentConsumer
  - VerifierConsumer
profile: profiles/c01-consumer.yaml

configurations:
  C0: {}
  C1:
    EnrichmentConsumer:
      session.timeout.ms: 45000

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
      kill_count: 1
    C1:
      kill_count: 1
```

Meaning:
- Phase `C0` runs the baseline `session.timeout.ms` 30000, the Microsoft value (`BaselineSettingsCatalog.cs` line 149; source 4). Phase `C1` gives the subject 45000, the librdkafka default (source 1, line 131). The witness runs the baseline in both.
- In both, the subject kills itself when `Consume` returns its first message, before processing it, so it has stored and committed nothing. This is the kill "before the first auto-commit" of K-57 (Q1).
- C01's question is the reassignment time, which ADR-022 measures from the subject's last row, its kill-intent row, to the witness's `assigned` row that holds the subject's partitions (line 774; `ConsumerJudgement.cs` lines 161 to 168). That time does not depend on the kill count. So C01's kill point can carry K-57 without changing C01's measurement.
- K-57's effect: the witness takes the subject's partitions with no committed offset, so it starts them at the latest offset (source 1, line 206). Messages produced on those partitions between the kill and that reset are never processed, and the witness's later commits pass them. ADR-022 classes them "committed but never processed" (line 765).

### 3.3 `scenarios/profiles/c01-consumer.yaml`

Exactly this content, with LF line endings and a final newline:

```yaml
schema_version: 1
id: c01-consumer
seed: 1402
tenants:
  - {id: tenant01, weight: 1}
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
  - {id: steady4, offered_attempts_per_second: 4, duration_seconds: 150}
consumer:
  processing_delay_ms: 200
```

The workload is synthetic (AGENTS.md hard safety rule 11): 600 planned attempts, 120 documents of five consecutive messages, keys `tenant01-d0` to `tenant01-d119`. With one tenant, the keys and their due times do not depend on the seed; the seed changes only payload sizes (`WorkloadGenerator.cs` lines 124 to 150).

Why this profile. ADR-022 completion needs, for every partition with messages in the run window, a committed offset at or past its end line (line 773). After a K-57 kill the witness starts each subject partition at the latest offset, so it commits on that partition only if a message arrives there after it takes over. With the C03 profile (60 s of load) most partitions receive their last message in the first 30 s, so C01 repeats would end `incomplete` with `consumer_commit_frontier_short` and no counts and no `reassignment`. With this profile, computed in this planning session with zlib CRC-32 modulo 32 of each UTF-8 key (source 7) and the generator's due times: each 16-partition range receives 300 messages; all 16 partitions of each range are non-empty; and every partition receives a message at or after 101.0 s from the load start (range 0 to 15: earliest last message 102.25 s, on partition 14; range 16 to 31: 101.0 s, on partition 24). The expected takeover is about 33 s (C0) or 48 s (C1) after the kill, and the kill is at the subject's first message, within 1.25 s of the load start (range 0 to 15: first message at 0 s on partition 11; range 16 to 31: at 1.25 s on partition 29). Those takeover times are estimates from the session timeouts plus one heartbeat interval, made on 2026-11-19, not measurements. Test W2 pins the offline values. 200 ms is the blueprint's lower enrichment bound (docs/blueprint.md line 49), as for C03.

### 3.4 Scenario schema: the consumer-processing declaration

`ConsumerProcessingDefinitionReader` gains rows for C10 and C01. Each configuration entry holds exactly one of two forms: `slow_message` (C03, unchanged) or `kill_count` (C10 and C01). The enforced checks are a closed list. Checks 1 to 5 keep their codes, paths and C03 behaviour; the C03 literals of checks 4 and 7 are unchanged and also apply to C10 and C01 where stated. Each failure is a `ScenarioValidationException` diagnostic with the code and path given:

1. `consumer_processing` present for an item other than C03, C10 or C01: `consumer_processing_unexpected`, path `consumer_processing`. C03, C10 or C01 without it: `consumer_processing_required`, path `consumer_processing`.
2. Combined with `single_action`, `probe_v2` or `idle_observation`: `consumer_processing_combined`, path `consumer_processing`.
3. Not a mapping with exactly `window_ms`, `completion_deadline_ms` and `configurations`: `consumer_processing_shape`, path `consumer_processing`.
4. `window_ms` not the plain scalar `360000`, or `completion_deadline_ms` not the plain scalar `30000`, for all three items: `consumer_processing_literal`, path `consumer_processing.window_ms` or `consumer_processing.completion_deadline_ms`.
5. `configurations` keys not exactly the scenario's configuration keys: `consumer_processing_configurations`, path `consumer_processing.configurations`.
6. For C03, an entry not a mapping with exactly `slow_message`, or `slow_message` not a mapping with exactly `ordinal`, `duration_ms` and `mode`. For C10 and C01, an entry not a mapping with exactly `kill_count`. Failure: `consumer_processing_shape`, path `consumer_processing.configurations.<id>`.
7. For C03, the slow-message literals of the base commit (ordinal `100`, duration `90000`, mode `inline` for `C0` and `pause` for `C1`), path `consumer_processing.configurations.<id>.slow_message.<field>`. For C10, `kill_count` not the plain scalar `110`; for C01, not the plain scalar `1`; path `consumer_processing.configurations.<id>.kill_count`. Failure: `consumer_processing_literal`.
8. The rest of the file not equal to the item's fixed experiment: `repeats` 5; both fault kinds `none`; gate `none`; roles exactly `LoadGenProducer`, `EnrichmentConsumer`, `VerifierConsumer` in that order; measures exactly the eight of section 3.1; and the replacements of each configuration exactly as this table gives, in the form `<role>:<key>=<value>` that `OverridesAreFixed` compares today (`ConsumerProcessingDefinitionReader.cs` lines 98 to 101). Failure: `consumer_processing_experiment`, path `consumer_processing`.

| Item | `C0` replacements | `C1` replacements |
|---|---|---|
| C03 | `EnrichmentConsumer:max.poll.interval.ms=60000` | `EnrichmentConsumer:max.poll.interval.ms=60000` |
| C10 | none | `EnrichmentConsumer:enable.auto.offset.store=true` |
| C01 | none | `EnrichmentConsumer:session.timeout.ms=45000` |

The validated value keeps its existing `Configurations` property with its type, `ImmutableDictionary<string, ConsumerSlowMessageDeclaration>` (`ScenarioContracts.cs` line 155): it holds C03's two slow messages exactly as today and is empty for C10 and C01. `ConsumerProcessingDefinition` gains one property, an `ImmutableDictionary<string, int>` of kill counts by configuration, empty for C03 and holding `C0` and `C1` for C10 and C01. Its name is the implementer's. So the existing C03 assertions on `Configurations` (`tests/Parchwell.Scenarios.Tests/ConsumerProcessingDefinitionTests.cs` lines 25 to 27) stay unchanged. The cross-file check `consumer_profile_required` (`ScenarioLoader.cs`) and the profile checks are unchanged. Every scenario and profile on `main` keeps loading and resolving exactly as before, and C03's validated slow messages are unchanged.

### 3.5 Override policy entries

In `KafkaScenarioOverridePolicy.Entries` (`KafkaScenarioOverrideSetting.cs` lines 130 to 155):

- Line 143, `new(KafkaSettingKeys.SessionTimeoutMs, [KafkaClientRole.EnrichmentConsumer], ["C08"], [NativeConfiguration, NativeSource]),`, becomes, in the same position:

  ```csharp
  new(KafkaSettingKeys.SessionTimeoutMs, 1, 3_600_000, [KafkaClientRole.EnrichmentConsumer], ["C01", "C08"], ["C08"], [NativeConfiguration, NativeSource]),
  ```

  This is the integer-with-omission form that `connections.max.idle.ms` uses for A04 (line 152; constructor at lines 64 to 73). One key has one entry, so the existing omission entry for C08 cannot stay beside a new one. Bounds 1 to 3600000 are librdkafka v2.16.0's range (source 1, line 131).
- After the `MaxPollIntervalMs` entry (line 146), one new entry:

  ```csharp
  new(KafkaSettingKeys.EnableAutoOffsetStore, KafkaScenarioOverrideValueKind.Boolean, 0, 1, [KafkaClientRole.EnrichmentConsumer], ["C10"], [NativeConfiguration]),
  ```

Resolution effects, from `ScenarioResolver.cs` lines 63 to 115 and 210 to 212:
- C08's `session.timeout.ms: omit` resolves exactly as at the base commit: an omission entry and an integer entry that lists C08 among its omission items take the same omission path (lines 64 to 66 and 99 to 103).
- Any scenario may now give `EnrichmentConsumer` an integer `session.timeout.ms` from 1 to 3600000, because the resolver checks the inventory list only for string, omission and activation entries (line 210), as for `max.poll.interval.ms` (C03). An integer outside the range fails with `setting_out_of_range`; a value equal to the baseline 30000 fails with `no_op_override` (line 111).
- One existing diagnostic changes, for invalid input only: `session.timeout.ms: omit` in a scenario other than C08 fails with `setting_out_of_range` (the integer parse fails) instead of `unsupported_setting`. Section 7.1 rule (b) governs any test that pins the old code.
- `enable.auto.offset.store` accepts the plain literals `true` and `false` (lines 68 to 70); the baseline is `false`, so `false` fails with `no_op_override`.

### 3.6 Production plan sources and admission

Worker (`ScenarioConsumerProcessingPlanSource`):
- `GetPlan` accepts scenario `C03`, `C10` or `C01` with configuration `C0` or `C1`, and returns null for every other pair, as today for non-C03 pairs (line 24).
- For C10 and C01 it returns, for the `subject`: `ConfigurationId` equal to the configuration; `ProcessingDelayMs` 200; `WindowMs` 360000; `SlowMessage` null; `KillCount` 110 (C10) or 1 (C01); `ConsumerSettings` exactly the configuration's `EnrichmentConsumer` settings whose override origin is the configuration: empty for `C0`, {`enable.auto.offset.store`: `true`} for C10 `C1`, {`session.timeout.ms`: `45000`} for C01 `C1`. For the `witness` it returns the same, except `KillCount` null and `ConsumerSettings` empty.
- C03 plans are byte for byte the plans of the base commit.
- Loading and resolving keep the existing method: the packaged files under the source root, the four parsing-budget constants (lines 17 to 20), and the three-role service collection (lines 55 to 69). A load or resolve failure returns null, and the worker exits with its existing `consumer_plan_invalid` (`ConsumerProcessingWorker.cs` lines 34 to 36).
- `Parchwell.Enrichment.csproj` gains three `Content` items, for `../../scenarios/C10/scenario.yaml`, `../../scenarios/C01/scenario.yaml` and `../../scenarios/profiles/c01-consumer.yaml`, each in the form of lines 11 and 12. C10 reuses the C03 profile, which is already packaged.

Verifier (`ScenarioConsumerVerificationPlanSource`):
- `TryGetPlan` accepts `C03`, `C10` or `C01` with `C0` or `C1`, and returns false for every other pair.
- For C10 and C01 it returns `KillCount` 110 or 1, `CompletionDeadlineMs` 30000, `SubjectSettings` the configuration's resolved `EnrichmentConsumer` effective settings, `WitnessSettings` the resolved `EnrichmentConsumer` baseline, both without `bootstrap.servers`, `client.id` and `group.id` (line 15), and `WindowMs` and `SlowMessageMode` both null. `ConsumerPlanValidation.IsValid` accepts such a plan (`ConsumerVerificationPlan.cs` lines 21 to 23).
- C03 plans are unchanged.
- `Parchwell.Verifier.csproj` gains the same three `Content` items, in the form of its existing C03 items (line 28 and the profile item beside it).

Load generator:
- `LoadGenHostOptions.IsAcceptedPair` (lines 65 to 67) accepts a pair when `ScenarioPhases.TryGetConfiguration` accepts it, or when the scenario is `C03`, `C10` or `C01`, the phase is `C0` or `C1`, and the configuration equals the phase. Its doc comment (lines 60 to 64) names the three scenarios. `ProducerRunContext` follows through its existing call.
- Every pair rejected today other than C10 and C01 with matching `C0` or `C1` stays rejected with its current failure name, including `phase_configuration_pair_invalid`, `producer_run_context_invalid` and `restart_instance_requires_b10`.
- `Parchwell.LoadGen.csproj` gains the same three `Content` items, in the form of line 28.

### 3.7 Owning documentation text

`docs/operations/scenario-contract.md`, "Consumer processing declarations" (lines 147 to 175): the opening paragraph names C03, C10 and C01 and this plan; the entry-form paragraph states the two forms of section 3.4; the value table gains rows for C10 (`kill_count` `110` in both configurations) and C01 (`kill_count` `1` in both); line 162's subject-only rule is kept and followed by "C10 replaces `enable.auto.offset.store` with `true` in C1. C01 replaces `session.timeout.ms` with `45000` in C1."; the closed check list becomes checks 1 to 8 of section 3.4, with the replacement table.

Line 73 gains, after its last sentence: "EnrichmentConsumer replacements also support `session.timeout.ms` from 1 to 3600000 (C01; C08 may still omit it) and the boolean `enable.auto.offset.store` (C10)."

`docs/operations/consumer-workflow.md` line 1: the clause "and C03 has its scenario, plan sources and load generator input; C10 and C01 do not run live until the child issue that supplies theirs merges." becomes "and C03, C10 and C01 have their scenarios, plan sources and load generator input."

`docs/blueprint.md` line 1275, only if Hari adopts Q1's recommended answer: the text "tested under C10 only for #499 ([decision 6894534441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441), point 3)" becomes "tested under C01 only for #499, with the subject killed at its first message ([decision 6894534441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441), point 3, as changed by [#510 decision](<link to Hari's decision answering Q1>))". The implementer inserts the link to the `## Decision` comment on #510 that records Hari's answer to Q1. This is a section 12 fold row, not ADR text.

## 4. Requirements

- R1 WHEN `ScenarioLoader` loads `C10` THEN its consumer-processing value has window 360000, deadline 30000, and kill count 110 with no slow message in `C0` and `C1`, and its profile has `processing_delay_ms` 200.
- R2 WHEN it loads `C01` THEN window 360000, deadline 30000, and kill count 1 with no slow message in `C0` and `C1`, and its profile `c01-consumer` has `processing_delay_ms` 200.
- R3 WHEN `ScenarioResolver` resolves C10 THEN `C1` has `EnrichmentConsumer` `enable.auto.offset.store` `true` with an override origin, and every other setting of every role in `C0` and `C1` equals its baseline. WHEN it resolves C01 THEN `C1` has `EnrichmentConsumer` `session.timeout.ms` `45000` with an override origin, and every other setting equals its baseline.
- R4 WHEN any check of section 3.4 fails THEN loading fails with exactly the code and path given there.
- R5 WHEN C03 or any other scenario on `main` is loaded and resolved THEN the result is unchanged from the base commit.
- R6 WHEN a scenario declares `EnrichmentConsumer` `session.timeout.ms` or `enable.auto.offset.store` THEN resolution behaves as section 3.5 states, and C08 resolves as at the base commit.
- R7 WHEN the worker's production source is asked for C10 or C01 with `C0` or `C1` THEN it returns the plans of section 3.6.
- R8 WHEN it is asked for any other pair, or its files are missing THEN it returns null, and a worker run with that source exits 1 with `consumer_plan_invalid`.
- R9 WHEN each R7 plan is applied by `ConsumerProcessingPlan.ApplyAndValidate` to the baseline configuration THEN it passes: `KillCount` is set for the subject only and `SlowMessage` is null (line 28); `session.timeout.ms` 45000 is within 1 to 3600000 and below the effective `max.poll.interval.ms` 300000 (lines 34 and 42); `enable.auto.offset.store` `true` is accepted (line 36).
- R10 WHEN the verifier's production source is asked for C10 or C01 with `C0` or `C1` THEN it returns the plans of section 3.6, and `IsValid` accepts each with the chart's `consumer-verify` budgets.
- R11 WHEN it is asked for any other pair THEN it returns false.
- R12 WHEN a member registers the settings the worker applies for C10 or C01 THEN the verifier plan for that role, with the three reserved keys added, equals them key for key and value for value.
- R13 WHEN the load generator validates C10 or C01 with phase `C0` and configuration `C0`, or `C1` and `C1` THEN it accepts the pair in `LoadGenHostOptions` and in `ProducerRunContext`; WHEN the configuration does not match the phase, the phase is not `C0` or `C1`, or the Job name suffix is `loadgen2` THEN it rejects with today's failure names.
- R14 WHEN the load generator resolves C10 or C01 THEN its `LoadGenProducer` settings for `C0` and `C1` equal the baseline.
- R15 WHEN the three images are published THEN each contains `scenarios/C10/scenario.yaml`, `scenarios/C01/scenario.yaml`, `scenarios/profiles/c01-consumer.yaml` and `scenarios/profiles/c03-consumer.yaml` under its application directory.
- R16 WHEN `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check` reports the pull request as relevant to the mutation workload THEN the implementer runs `c5_runtime.py renew` with a basis naming this plan, at most once. If `check` then passes, continue. If it reports that new measurement is needed, push -wip and hand off to the next session (cap 1; exhaustion action `push -wip and hand off to the next session`).
- R17 WHEN the pull request head is checked THEN `scenarios/C10/expected.yaml` and `scenarios/C01/expected.yaml` each equal section 5.2 byte for byte.
- R18 WHEN `WorkloadGenerator.Generate` runs over the committed `c01-consumer.yaml`, with each message's partition the CRC-32 of its UTF-8 key modulo 32 THEN the values of test W2 hold.
- R19 WHEN C10 or C01 is loaded THEN the #507 upper bounds hold: `WindowMs` 360000 does not exceed 1620000, and `CompletionDeadlineMs` 30000 is below 40000 (#507 plan section 3.7 rules 1 and 2), as for C03.

## 5. Hypotheses and expected outcomes

### 5.1 Basis

The verdict vocabulary is confirmed, contradicted, undocumented and untestable (factory.md "Evidence pack"). Hypothesis outcomes follow the C03 form (`holds_when`, `contradicted_when`, `otherwise: inconclusive`). No code reads these files (ADR-014). The verdict pull requests (#499 split entries 9 and 10) apply them. Evidence is the verifier consumer block of each repeat (`docs/operations/verifier-contract.md`); a repeat that is not `complete_valid` has no `counts` and no `reassignment` (`ConsumerJudgement.cs` lines 82 to 98).

C10. librdkafka stores an offset automatically when it hands a message to the application, if `enable.auto.offset.store` is `true` (source 1, line 140; source 3: `rd_kafka_consume0` calls `rd_kafka_fetch_op_app_prepare`, `rdkafka.c` line 5128, which calls `rd_kafka_update_app_pos`, `rdkafka_op.c` line 1155, which stores the offset when the setting is on, `rdkafka_offset.c` lines 1549 to 1564). The auto-commit timer runs on librdkafka's main thread every `auto.commit.interval.ms` and commits the stored offsets (`rdkafka_cgrp.c` lines 552 to 557 and 4791 to 4803). librdkafka recommends `enable.auto.offset.store=false` for long processing, with offsets stored after processing (source 1, line 137). So with store-after-process, a kill before processing cannot leave a committed offset past an unprocessed message; the witness can only process again what the subject processed after its last commit (ADR-022 "Consequences", line 791). With the automatic store, a commit can pass a message that was handed to the application but not yet recorded. Under ADR-022's kill point, the only such message is the 110th, and only if a timer tick commits between `Consume` returning it and the SIGKILL, which is the time of one kill-intent append (`ConsumerProcessingWorker.cs` lines 152 to 160). With a 5000 ms timer and an append that takes far less, that is unlikely in five repeats (estimate, basis: those two durations, made on 2026-11-19; the append time is not measured, ADR-022 line 790). Also, a commit made in that window whose result the subject did not record can make the repeat invalid with `consumer_commit_record_disagreement` when the witness commits nothing later on that partition (ADR-022 lines 770 and 792). So C10 C1 can show the documented risk only by chance, and a zero count does not show the automatic store is safe (Q2).

C01. The broker removes a member when it receives no heartbeat within `session.timeout.ms` (source 1, line 131). The subject's last heartbeat was sent at most one `heartbeat.interval.ms` (3000, baseline) before its kill-intent row, so removal comes between `session.timeout.ms` minus 3000 ms and `session.timeout.ms` after that row. Reassignment then needs the witness to learn of the rebalance, rejoin and run its assigned callback. The lower bound of each window is that inference; the upper bound adds a 15000 ms policy margin for rebalance time, which is not measured (Q3). The verifier reports no timing for a repeat whose members ran on different nodes (`different_nodes`, `ConsumerJudgement.cs` line 164).

### 5.2 Exact content

`scenarios/C10/expected.yaml`, exactly, with LF line endings and a final newline:

```yaml
schema_version: 1
id: C10
subject: >-
  librdkafka v2.16.0 documents enable.auto.offset.store (default true) as
  storing the offset of the last message provided to the application, and
  recommends false with offsets stored after processing for long-time
  processing. The baseline stores after processing.

assessment_rules:
  counted_repeats: complete_valid only, meeting commit_precondition
  repeats_per_configuration: 5
  replacements: 0
  evidence: >-
    The verifier consumer block of each repeat: status and counts
    (docs/operations/verifier-contract.md). A repeat that is not
    complete_valid has no counts.
  kill_point: >-
    The subject sends SIGKILL to itself when Consume returns its 110th
    message, before processing it (ADR-022). The witness runs the baseline.
  commit_precondition: >-
    A repeat counts only when, in the archived consumer records, every
    partition with a subject processed row has a successful subject commit
    entry for it with a row sequence below the subject's kill-intent row.
    Otherwise the witness starts that partition at the latest offset, and a
    loss there is not an offset-store effect. A repeat that fails it is
    reported, not counted.

configurations:
  C0:
    H1_store_after_process_loses_nothing:
      question: >-
        With offsets stored after processing, does a crash leave any message
        committed but never processed?
      holds_when: >-
        All 5 repeats counted, and in each:
        counts.committed_never_processed = 0.
      contradicted_when: >-
        All 5 repeats counted, and in at least one:
        counts.committed_never_processed >= 1.
      otherwise: inconclusive
    processed_again:
      mode: recorded_only
      value: counts.processed_again and counts.processed_again_extra_rows per repeat
  C1:
    H2_auto_store_can_pass_an_unprocessed_message:
      question: >-
        With the default automatic offset store, does a crash leave a message
        committed but never processed?
      holds_when: >-
        All 5 repeats counted, and in at least one:
        counts.committed_never_processed >= 1.
      contradicted_when: never; a zero count does not contradict a possibility.
      otherwise: inconclusive
      published_text_must_state: >-
        Under this kill point a commit can pass an unprocessed message only if
        an auto-commit lands between Consume returning the 110th message and
        the kill. A zero count does not show that the automatic store is safe.
    processed_again:
      mode: recorded_only
      value: counts.processed_again and counts.processed_again_extra_rows per repeat

verdict:
  confirmed_when: H1 holds.
  contradicted_when: H1 is contradicted.
  no_verdict_when: neither of the above; the run is investigated, not published.
  smoke_run: never published as a result; it gives no verdict.
  separate_lines: H2 and processed_again are published on their own lines and do not change the verdict.
  text_must_state:
    - The kill point in assessment_rules.kill_point.
    - Each repeat that failed commit_precondition, with its partitions.
    - H2's published_text_must_state.
    - K-57 is tested under C01, not C10.

sources:
  - https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md
  - https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka.c
  - https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_op.c
  - https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_offset.c
  - https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_cgrp.c
```

`scenarios/C01/expected.yaml`, exactly, with LF line endings and a final newline:

```yaml
schema_version: 1
id: C01
subject: >-
  librdkafka v2.16.0 documents that the broker removes a group member when it
  receives no heartbeat within session.timeout.ms (default 45000). Microsoft
  recommends 30000 for Event Hubs, within 6000 to 300000.

assessment_rules:
  counted_repeats: complete_valid only, with reassignment.microseconds not null
  repeats_per_configuration: 5
  replacements: 0
  evidence: >-
    The verifier consumer block of each repeat: status, counts and
    reassignment (docs/operations/verifier-contract.md). Reassignment runs
    from the subject's kill-intent row to the witness's assigned row that
    holds the subject's partitions. It is null when the members ran on
    different nodes.
  kill_point: >-
    The subject sends SIGKILL to itself when Consume returns its first
    message, before processing it, so it has committed nothing (ADR-022).
    heartbeat.interval.ms stays 3000. The witness runs the baseline.
  window_policy: >-
    The lower bound is session.timeout.ms minus heartbeat.interval.ms, because
    the last heartbeat can precede the kill by up to 3000 ms. The upper bound
    adds 15000 ms for rebalance time, a policy value, not a measurement.

configurations:
  C0:
    H1_removal_follows_30000:
      question: >-
        With session.timeout.ms 30000, how long after the subject is killed
        does the witness hold its partitions?
      holds_when: >-
        All 5 repeats complete_valid, and in each reassignment.microseconds is
        not null, >= 27000000 and < 45000000.
      contradicted_when: >-
        All 5 repeats complete_valid, and in at least one
        reassignment.microseconds is not null and < 27000000 or >= 45000000.
      otherwise: inconclusive
    H3_k57_latest_reset_skips_messages:
      question: >-
        Killed before its first auto-commit, with auto.offset.reset at its
        baseline latest, are messages on the subject's partitions committed
        but never processed (K-57)?
      holds_when: >-
        All 5 repeats complete_valid, and in each:
        counts.committed_never_processed >= 1.
      contradicted_when: >-
        All 5 repeats complete_valid, and in at least one:
        counts.committed_never_processed = 0.
      otherwise: inconclusive
    reassignment:
      mode: recorded_only
      value: reassignment.microseconds per repeat
  C1:
    H2_removal_follows_45000:
      question: >-
        With session.timeout.ms 45000 on the subject, how long after it is
        killed does the witness hold its partitions?
      holds_when: >-
        All 5 repeats complete_valid, and in each reassignment.microseconds is
        not null, >= 42000000 and < 60000000.
      contradicted_when: >-
        All 5 repeats complete_valid, and in at least one
        reassignment.microseconds is not null and < 42000000 or >= 60000000.
      otherwise: inconclusive
    H3_k57_latest_reset_skips_messages:
      question: >-
        As in C0, with session.timeout.ms 45000 on the subject.
      holds_when: >-
        All 5 repeats complete_valid, and in each:
        counts.committed_never_processed >= 1.
      contradicted_when: >-
        All 5 repeats complete_valid, and in at least one:
        counts.committed_never_processed = 0.
      otherwise: inconclusive
    reassignment:
      mode: recorded_only
      value: reassignment.microseconds per repeat

verdict:
  confirmed_when: H1 holds and H2 holds.
  contradicted_when: H1 is contradicted or H2 is contradicted.
  no_verdict_when: neither of the above; the run is investigated, not published.
  smoke_run: never published as a result; it gives no verdict.
  separate_lines: H3 in each configuration and the recorded reassignment values are published on their own lines and do not change the verdict.
  text_must_state:
    - The kill point and window policy in assessment_rules.
    - The workload is synthetic and its load lasts 150 s.
    - K-57 is tested here because C10's two phases compare offset-store modes.

sources:
  - https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md
  - https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations
```

Why H3's `contradicted_when` is fair. The subject's killed message is in the run window, has no processed row, and lies on a partition whose last message comes after 101.0 s (test W2), so the witness commits past it. Under `latest` reset it is committed but never processed in every complete repeat. A zero count would mean the witness started the subject's partitions below their end at takeover, which `latest` excludes (source 1, line 206).

## 6. Invariants and safety rules

- The produce side keeps ADR-012 and ADR-013. No change to the load generator's produce behaviour, ledger or evidence recorders.
- `expected.yaml` is never a code input (ADR-014). No code, test or script reads it, apart from the byte-equality tests X1 and X2.
- Only the subject sends a signal, SIGKILL to itself, once per repeat, through the existing worker path (ADR-020 as amended by ADR-022, line 640). This plan changes no signal code.
- No commit is made to produce evidence (ADR-022 "Record").
- No change to the chart, workflow, phase scripts, identities, roles, network or infrastructure. This plan does not change live infrastructure, so the factory.md "Item lifecycle" step 1 rule on live smoke before approval does not apply. Its smoke runs are the first live runs of the #507 design with a self-kill.
- C03's scenario, profile, plans and verifier output are unchanged.
- No secret, key, connection string, tenant ID or subscription ID enters the repository.
- Hard safety rules 1 to 9 are unchanged. No agent dispatches `scenario.yml` without the authority of section 9.

## 7. Test map

### 7.1 Standing fixture rule

When an existing test's fixture is rejected by a check this plan adds, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The pull request's implementation notes list each such change with the check that forces it. The cold review and the Codex review check each listed change. Model: [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.

Known case: a test that enumerates scenario directories or `Content` items takes option (a). The override-policy list test is not a fixture case; section 7.2 states its change. Cap: at most 10 existing test files changed under this rule. Exhaustion action: `push -wip and hand off to the next session`.

### 7.2 Assertions this plan supersedes

These existing assertions pin behaviour that this plan changes on purpose. Each is changed exactly as stated, and the implementation notes list them. They are not fixture changes under section 7.1.

- `tests/Parchwell.Kafka.Tests/Configuration/ScenarioOverrideMetadataTests.cs`, `Given_OverridePolicy_When_ReadingSettings_Then_ExposesEachSupportedEntry`: line 18 becomes `Assert.Equal(23, settings.Length);`; the `session.timeout.ms` element (line 31) asserts instead value kind `Integer`, role `EnrichmentConsumer`, minimum 1, maximum 3600000, inventory [`C01`, `C08`] and omission inventory [`C08`], in the form the file uses for `connections.max.idle.ms`; and after the `max.poll.interval.ms` element (line 34) one element is added: `setting => AssertNewEntry(setting, "enable.auto.offset.store", KafkaScenarioOverrideValueKind.Boolean, KafkaClientRole.EnrichmentConsumer, "C10", 0, 1),`.
- `tests/Parchwell.Verifier.Tests/Consumer/HostOptionsTests.cs`, `Given_ConsumerPlanBoundary_U15_When_Starting_Then_RejectsInvalidPlanBeforeClients`: the `no-source` case (line 37) no longer sets `ScenarioId` to `C10`. It keeps the default scenario and instead registers, in the test's service configuration, an `IConsumerVerificationPlanSource` whose `TryGetPlan` returns false with a null plan. Its expected values (`valid` false, exit 1, no client constructed) are unchanged.
- The same file, `Given_C10_or_C01_with_no_registered_plan_source_When_starting_Then_the_production_source_fails_with_consumer_plan_invalid` (from line 157): replaced by test V5, because the production source now serves those pairs.
- `tests/Parchwell.Enrichment.Tests/ConsumerProcessing/ScenarioPlanSourceTests.cs`: in the theory `Given_a_pair_that_is_not_C03_C0_or_C1_When_asked_for_the_plan_Then_it_returns_null` (line 66), the rows (`C10`, `C0`) and (`C01`, `C0`) become (`C10`, `C2`) and (`C08`, `C0`); (`C03`, `C2`) stays. The theory `Given_C10_or_C01_with_phase_C0_When_the_worker_runs_with_the_production_source_Then_it_exits_1_with_consumer_plan_invalid` (line 81) keeps its assertion and its two scenarios, and builds its source over an empty root, as `Given_a_root_without_the_C03_files_When_asked_for_the_plan_Then_it_returns_null` (line 70) does, so the source returns null for a missing file. The theory `Given_C10_or_C01_with_phase_C0_When_the_host_runs_without_a_configured_source_Then_it_exits_1_and_writes_consumer_plan_invalid` (line 95) is replaced by test E4, because the production source now returns a plan for those pairs; the `consumer_plan_invalid` exit stays covered by the worker-run theory.
- `tests/Parchwell.LoadGen.Tests/Host/C03AdmissionTests.cs`: rows (`C10`, `C0`, `C0`) and (`C01`, `C0`, `C0`) of the theory `Given_an_unaccepted_pair_When_validating_Then_each_check_keeps_its_failure_name` (line 53) move to the acceptance tests of L1; the rejection theory gains (`C10`, `C0`, `C1`), (`C10`, `C2`, `C2`) and (`C01`, `probe`, `C0`).
- `tests/Parchwell.Verifier.Tests/Consumer/ScenarioVerificationPlanSourceTests.cs`: rows (`C10`, `C0`) and (`C01`, `C1`) of the theory `Given_any_other_pair_When_the_production_source_is_asked_Then_it_returns_false` (line 84) become (`C10`, `C2`) and (`C01`, `C2`); (`C03`, `C2`) and (`B04`, `C0`) stay.

### 7.3 Tests

Test names follow `Given_<state>_When_<action>_Then_<outcome>` (docs/engineering/dotnet.md). Literals are exact.

| ID | Requirement | Project | Input | Expected |
|---|---|---|---|---|
| S1 | R1 | Scenarios | committed C10 files | window 360000; deadline 30000; C0 and C1 kill count 110, no slow message; profile `c03-consumer`, delay 200 |
| S2 | R2 | Scenarios | committed C01 files | window 360000; deadline 30000; C0 and C1 kill count 1, no slow message; profile `c01-consumer`, delay 200 |
| S3 | R3 | Scenarios | resolve committed C10 and C01 | C10 C1 `EnrichmentConsumer` `enable.auto.offset.store` = `true`, origin C10/C1; C01 C1 `session.timeout.ms` = `45000`, origin C01/C1; every other setting of every role in both configurations equals the baseline |
| S4 | R4 | Scenarios | in-memory variants, each one change from the committed file: C10 `C0` `kill_count: 109`; `111`; `"110"`; C01 `C1` `kill_count: 0`; `2`; C10 `C0` entry `slow_message: {ordinal: 100, duration_ms: 90000, mode: inline}` in place of `kill_count`; C10 `C0` entry with both `kill_count: 110` and that `slow_message`; C03 `C0` entry `kill_count: 110` in place of `slow_message`; C10 `C0: {EnrichmentConsumer: {enable.auto.offset.store: true}}`; C10 `C1: {}`; C01 `C1` `session.timeout.ms: 44999`; C10 `repeats: 6`; C01 without `consumer_processing`; C10 with `idle_observation` added from the committed idle-observation item | in order: `consumer_processing_literal` (three times, path `consumer_processing.configurations.C0.kill_count`); `consumer_processing_literal` (twice, path `...C1.kill_count`); `consumer_processing_shape` (three times, path `consumer_processing.configurations.C0`); `consumer_processing_experiment` (four times, path `consumer_processing`); `consumer_processing_required`; `consumer_processing_combined` |
| S5 | R5 | Scenarios | every committed scenario other than C10 and C01, C03 included | load and resolve as at the base commit; the existing tests stay unchanged and pass |
| K1 | R6 | Scenarios | the in-memory definition D of the #508 plan (C02, roles `LoadGenProducer` and `EnrichmentConsumer`, saturation-ramp profile), with C0 `EnrichmentConsumer` `session.timeout.ms`: 1; 3600000; 0; 3600001; 30000; `omit`; `"45000"` | accept; accept; `setting_out_of_range`; `setting_out_of_range`; `no_op_override`; `setting_out_of_range`; `int32_required` |
| K2 | R6 | Scenarios | D with C0 `EnrichmentConsumer` `enable.auto.offset.store`: `true`; `false`; then `true` under `LoadGenProducer` | accept; `no_op_override`; `unsupported_setting` |
| K3 | R6 | Scenarios | committed C08 | resolves as at the base commit: `session.timeout.ms` omitted in C1 with an omission origin (the existing C08 tests stay unchanged and pass) |
| E1 | R7 | Enrichment | production source over the committed files, role `subject`: C10/C0; C10/C1; C01/C0; C01/C1 | delay 200; window 360000; slow null; kill 110, 110, 1, 1; settings {}, {`enable.auto.offset.store`: `true`}, {}, {`session.timeout.ms`: `45000`} |
| E2 | R7 | Enrichment | the same four pairs, role `witness` | delay 200; window 360000; slow null; kill null; settings {} |
| E3 | R8 | Enrichment | direct `GetPlan` for (`C10`, `C2`), (`C01`, `C2`), (`C08`, `C0`); C10/C0 over an empty root | null each time |
| E4 | R7, R8 | Enrichment | host built from a configuration holding only the 31 names the chart sets (`charts/parchwell/templates/_consumer.tpl` lines 74 to 120), for C10/C0 and C01/C1, role `subject` | the registered production source returns the E1 plan; no `ScenarioLoadOptions` key is read from configuration |
| E5 | R9 | Enrichment | each E1 and E2 plan through `ApplyAndValidate` on the baseline configuration | no exception; for C01/C1 subject, effective `SessionTimeoutMs` 45000; for C10/C1 subject, effective `EnableAutoOffsetStore` true |
| V1 | R10 | Verifier | production source for the four pairs | kill 110, 110, 1, 1; deadline 30000; `WindowMs` null; `SlowMessageMode` null; C10/C1 subject map `enable.auto.offset.store` = `true`; C01/C1 subject map `session.timeout.ms` = `45000`; each C0 subject map equals its witness map; witness maps have `enable.auto.offset.store` `false` and `session.timeout.ms` `30000`; no map has the three reserved keys; `IsValid` true with the chart's `consumer-verify` budgets |
| V2 | R12 | Verifier | for each of the eight role and configuration pairs: the plan map plus the three reserved keys; and `CreateEnrichmentConsumer().Configuration` from a factory with the same inputs, with the subject override applied | equal key for key and value for value |
| V3 | R11 | Verifier | `TryGetPlan` for (`C10`, `C2`), (`C01`, `C2`), (`C03`, `C2`), (`B04`, `C0`) | false each time |
| V5 | R10 | Verifier | host built with no test plan source for (`C10`, `C0`) and (`C01`, `C1`), with the options of the replaced test at `HostOptionsTests.cs` line 157 | `Program.BuildHost` succeeds; the registered `ConsumerVerificationPlan` has kill count 110 and 1, deadline 30000; no client constructed |
| V4 | R10 | Verifier | `RegisteredProducerConfigurationResolver.Resolve` with the committed C10 files and a C10 producer registration; the same for C01 | each resolves with no exception, `EnrichmentConsumer` group ID `<run>-c0-r<repeat>-consumers` |
| L1 | R13 | LoadGen | host options and run context: C10 `C0`/`C0`, `C1`/`C1`; C01 `C0`/`C0`, `C1`/`C1` | accepted |
| L2 | R13 | LoadGen | C10 `C0`/`C1`; C10 `C2`/`C2`; C01 `probe`/`C0`; C01 `C0`/`C0` with Job name suffix `loadgen2` | `phase_configuration_pair_invalid` (options) and `producer_run_context_invalid` (context) for the first three; `restart_instance_requires_b10` for the last |
| L3 | R14 | LoadGen | the existing scenario-to-run-plan path for C10 and C01, C0 and C1 | `LoadGenProducer` settings equal the baseline; no `selected_configuration_not_declared_by_scenario` and no `scenario_does_not_declare_loadgen_producer` |
| W2 | R18 | LoadGen | `WorkloadGenerator.Generate` over the committed C01 profile; CRC-32 written in the test as the C03 test W1 does, no new package (check value: ASCII `123456789` gives `0xCBF43926`); partition = CRC-32 of the UTF-8 key modulo 32 | 600 events; partitions 0 to 15: 300 messages, all 16 non-empty, first message ordinal 0 due at 0 microseconds on partition 11, every partition has a message due at or after 102250000 microseconds, partition 14's last message is due at 102250000 microseconds; partitions 16 to 31: 300 messages, all 16 non-empty, first message ordinal 5 due at 1250000 microseconds on partition 29, every partition has a message due at or after 101000000 microseconds, partition 24's last message is due at 101000000 microseconds |
| X1 | R17 | Scenarios | committed `scenarios/C10/expected.yaml` | SHA-256 equal to the value the implementer computes from section 5.2's block with LF endings and a final newline, recorded in the test |
| X2 | R17 | Scenarios | committed `scenarios/C01/expected.yaml` | as X1 |
| P1 | R15 | build | acceptance A3 | each output has the four files |

R16 is covered by acceptance A2. R19 is covered by tests S1 and S2's literal window and deadline. Boundaries: S4 covers one below and one above each kill literal, and the quoted form; K1 covers 1, 3600000, 0 and 3600001. S4 and K1 assert that the named code is among the diagnostics, as the existing `Reject` helper does (`IdleObservationDefinitionTests.cs` line 219).

## 8. Deployment assumptions

Each is an assumption about the deployed runtime that no offline test in this plan covers. Each stays under section 13 (Q4) until Hari accepts it by name. Live evidence for the shared consumer design exists from the C03 smoke run [44248514485](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/44248514485) ([#508 report 6973117302](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/508#issuecomment-6973117302)): ten repeats, each on fresh connections (new member pods, a new group ID and new clients per repeat) under the C03 load; readiness reached, `offset_read` `success` and every block `complete_valid` in all ten; phase durations 1929 s and 1932 s. No run has exercised a self-kill. Each smoke run of section 9 takes its evidence the same way, on fresh connections under the item's load.

- DA1. Live self-kill (#507 DA5). The member pod shares its process namespace (`charts/parchwell/templates/_consumer.tpl` line 46, `shareProcessNamespace: true`), so the worker is not the namespace's first process, and a SIGKILL it sends to its own process ID ends that process and its container. That is my reading of the Linux and Kubernetes behaviour, not verified in a live pod. The subject's SIGKILL ends only its own process; its Job ends failed; the workflow records `failed`, which is not a bad outcome (`consumer_repeat.py` lines 14 and 158), and continues with the witness, load generator and verifier. Code evidence: the worker checks it can signal itself before registering (`ConsumerProcessingWorker.cs` lines 39 to 43). Live evidence: none. Smoke check 1.
- DA2. Same node. Both members run on one node in each repeat, so `reassignment.microseconds` is not null. If they do not, that repeat's timing is unavailable and C01's hypotheses are inconclusive (ADR-022 line 774). Live evidence: none for node placement. Smoke check 10.
- DA3. C10 commits before the kill. Every subject partition with a processed row has a successful committed entry before the kill-intent row (section 3.1 "Why 110"). Offline evidence: the C03 profile's partition spread (#508 test W1). If it fails, a K-57 loss that is not a store-mode effect would falsely contradict H1 and falsely make H2 hold. Smoke check 9 tests it in the smoke run; in the batch, `commit_precondition` in `scenarios/C10/expected.yaml` excludes each repeat where it fails (plan check 7001168119, finding 6).
- DA4. C01 takeover before the last message. In every repeat the witness takes the subject's partitions before 101.0 s after the load start (section 3.3). Offline evidence: test W2 for the workload; the takeover time is an estimate. If it fails, the repeat ends `incomplete` with `consumer_commit_frontier_short`. Smoke check 10.
- DA5. Window. Within 360000 ms from each member's start, readiness, the start line, the load (60 s for C10, 150 s for C01), the takeover and the witness's catch-up finish, and every partition's commit reaches the end line. Estimate, made on 2026-11-19: C10 uses about the same share of the window as C03; C01 about 25 s more, because its load is 90 s longer and it has no 90 s slow message. Basis: the C03 smoke run completed every repeat inside the same window. A shortfall shows as `consumer_commit_frontier_short`.
- DA6. Phase time. Repeat 5 starts within the 2100 s bound (#507 plan line 91). Evidence: C03 phases of 1929 s and 1932 s with the same window. Estimate, made on 2026-11-19: C10 and C01 phases similar. If repeat 5 is recorded `not_attempted` with `orchestration_deadline`, the conflict goes to Hari; the budget is not raised.
- DA7. Witness capacity in C01. After the takeover the witness alone receives 4 attempts per second, each taking 200 ms plus one Blob append, whose time is not measured (ADR-022 line 790). A backlog that forms drains after the load ends; DA5 covers the time.
- DA8. Fidelity. The `effective_settings` a live member registers equal the verifier plan maps for the two new keys. Test V2 shows it offline against the same factory; C03 smoke check 4 passed for its key. A difference would show as `consumer_configuration_mismatch`.
- DA9. Archive. A self-killed subject writes no native-monitoring index, because the index is written only at shutdown, so every C10 and C01 repeat records an archive monitoring gap that holds its phase's publication (`docs/operations/consumer-workflow.md` line 42; [#531 handoff 6956490346](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6956490346), D2). Q6 asks how to handle it. Smoke check 8 reports it.

## 9. Acceptance commands and evidence

Before the pull request opens:
- A1. `./scripts/verify-dotnet.sh`. Evidence: exit 0; warnings as errors; all tests pass, including every test in section 7.
- A2. `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check` against the base. Evidence: pass, after at most one `renew` under R16.
- A3. For each of `Enrichment`, `Verifier` and `LoadGen`, the locked publish form `phase.py` uses (lines 589 to 593), without the container target: `dotnet publish src/Parchwell.<project>/Parchwell.<project>.csproj --configuration Release -r linux-x64 --self-contained false -p:RestoreLockedMode=true -p:NuGetLockFilePath=packages.linux-x64.lock.json -o <directory>`, then list the four files of R15 in each output directory. Evidence: three exits 0 with no `NU1004`, and twelve paths listed.
- A4. `sha256sum scenarios/C10/expected.yaml scenarios/C01/expected.yaml` equal the values tests X1 and X2 pin.
- A5. `git diff --stat origin/main -- '*.lock.json'` is empty.
- A6. The required checks pass on the final head; the Codex review and security review of the head commit are complete with no P0, P1 or High finding open; the Claude review gate of decision 6894587233 point 2 has passed. The pull request touches protected paths, so Hari merges.

After the pull request merges, one smoke run per item. Each is an Azure dispatch, reserved to Hari by decision 6895535291 point 4; Q5 asks whether the batch control session may dispatch them. Form: `scenario.yml` on `main`, input `scenario=C10`, then `scenario=C01`, one at a time (hard safety rule 5). Each runs phases C0 and C1, five repeats each. Prerequisites: this pull request merged; no other dispatch or ephemeral environment active. Smoke runs are never published. Smoke checks, read from the run's phase report, candidate, pod and archive artifacts:
1. Every repeat has the six records `consumer-subject`, `consumer-witness`, `consumer-readiness`, `start-line`, `loadgen` and `verify`; none is `not_attempted`; the subject record's outcome is `failed` and the witness's `complete` (DA1, DA6).
2. Readiness is reached in every repeat.
3. `consumer-verify` writes a consumer block for every repeat whose producer side is `complete_valid`, and `offset_read.outcome` is `success`.
4. No block has `consumer_configuration_mismatch` (DA8).
5. Every block's status is `complete_valid`, or each other status is reported with its reasons (DA5).
6. Every `complete_valid` block's members show the subject ending `kill_intent` with 109 (C10) or 0 (C01) processed rows, and the witness ending `completion_receipt`.
7. The judge step runs and `phase.py candidate` accepts the candidate.
8. The archive receipt is reported, with each gap kind and count (DA9).
9. C10 only: in every repeat, each partition with a subject `processed` row has a successful subject commit entry for that partition with a row sequence below the subject's kill-intent row. Each exception is reported with the repeat and partition (DA3).
10. C01 only: no block has `consumer_commit_frontier_short`, and every `complete_valid` block's `reassignment.microseconds` is not null (DA2, DA4).

Exhaustion action for a failed check: hand off to Hari with the run link and the failed check; no batch dispatch. Checks 8, 9 and 10 go to Hari even when they pass, with their values, before any batch dispatch.

The batch dispatch of each item is the same workflow input. Prerequisites: its smoke checks pass, or Hari accepts each failure by name; the Q6 change merged, or Hari's other answer to Q6 applied. It waits for Hari, after he has seen the smoke results.

Mutation acceptance (factory.md "Evidence pack"). This pull request changes mutation inputs (Kafka and Scenarios sources and verifier sources). One passing `verifier-c5-mutation.yml` run on `main` at a commit that includes this merge satisfies it. The implementing session dispatches it once after the merge. It runs on GitHub runners and reaches no Azure resource; it is not a scenario dispatch. Cap: 1 dispatch. Exhaustion action: `record the gap and continue`; open a fix issue if none covers the failure; C10 and C01 evidence-pack publication stays blocked. That run gates the evidence packs, not the smoke or batch runs.

## 10. Deferred work

1. Smoke runs. On the verdict path. Deferral permitted because they can only run after this pull request merges. Responsible: whoever Hari names under Q5. Stage: after merge. Prerequisites and checks: section 9. Workload bound: one dispatch per item; exhaustion action as section 9. Completion evidence: run links and the ten checks reported on #510. Gate it blocks: the batch dispatches.
2. Batch dispatches. On the verdict path. Permitted as above. Responsible: Hari. Stage: after the smoke checks and the Q6 change. Bound: one dispatch per item. Completion evidence: run links. Gate it blocks: the C10 and C01 verdict pull requests (#499 split entries 9 and 10).
3. Mutation acceptance run. On the verdict path. Permitted because it must run on `main` after merge. Responsible: the implementing session. Bound and exhaustion action: section 9. Completion evidence: `summary.json` with `measurement_complete: true` and `break_passed: true`. Gate it blocks: the C10 and C01 evidence packs.
4. The archive monitoring gap of a self-killed subject (#531 D2; Q6). On the verdict path. Deferral permitted because the fix is outside this plan's files under the recommended answer. Responsible: the session Hari names under Q6, in its own pull request. Prerequisites: Hari's answer to Q6. Completion evidence: that pull request merged. Gate it blocks: the C10 and C01 batch dispatches.
5. README rows and reports. On the verdict path. Responsible: the verdict pull requests, which apply `text_must_state`.

## 11. Plan-check questions (factory.md "Item lifecycle" step 1)

(a) Effort that scales with a number unknown at planning time: the mutation qualification renewal (R16: cap 1; exhaustion `push -wip and hand off to the next session`); the post-merge mutation run (cap 1; `record the gap and continue`); the smoke and batch dispatches (one each per item, by Hari or whom he names); fixture changes under section 7.1 (cap 10 files; `push -wip and hand off to the next session`). The superseded assertions of section 7.2 are a fixed list of five files.

(b) Stricter than the parent plan: the parent is the #499 proposal, which pins no checks. Against the predecessor contracts, the declaration pins the kill counts and each configuration's replacements, so they cannot drift without a plan amendment. Looser in one place: the `session.timeout.ms` entry admits integers for any scenario, as the resolver does for every integer entry, and changes one diagnostic for invalid input (section 3.5). Different from the parent in one place, pending Q1: K-57 is tested under C01, not C10.

(c) Tests or checks that depend on code, data or behaviour neither on `main` nor in the pull request: the decision rules depend on live Event Hubs and cluster behaviour (DA1 to DA9), each listed in section 8 and open under Q4. The workload properties behind DA3 and DA4 are pinned offline (the existing C03 test W1, and test W2). Offline tests depend only on code on `main` or in the pull request.

## 12. Sources

Tools: librdkafka v2.16.0 files were fetched at the release tag with `curl` from `raw.githubusercontent.com` in this planning session on 2026-11-19. The Microsoft page was read with the Microsoft Learn documentation search tool on 2026-11-19. Workload values were computed in this session with Python's `zlib.crc32` over keys built as `WorkloadGenerator.cs` lines 145 to 150 build them, with due times as lines 146 to 150 compute them.

1. librdkafka v2.16.0 CONFIGURATION.md, https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md. Line 131: `session.timeout.ms`, range 1 to 3600000, default 45000; "If no hearts are received by the broker for a group member within the session timeout, the broker will remove the consumer from the group and trigger a rebalance." Line 132: `heartbeat.interval.ms`, default 3000. Line 137: `max.poll.interval.ms`; "It is recommended to set `enable.auto.offset.store=false` for long-time processing applications and then explicitly store offsets (using offsets_store()) *after* message handling". Line 139: `auto.commit.interval.ms`, default 5000. Line 140: `enable.auto.offset.store`, default true, "Automatically store offset of last message provided to application." Line 206: `auto.offset.reset`, default `largest`; "'largest','latest' - automatically reset the offset to the largest offset".
2. Not used.
3. librdkafka v2.16.0 source. `src/rdkafka.c` line 5128: `rd_kafka_consume0` calls `rd_kafka_fetch_op_app_prepare` before returning a message. `src/rdkafka_op.c` lines 1140 to 1156: that function sets the position to the message offset plus 1 through `rd_kafka_update_app_pos`. `src/rdkafka_offset.c` lines 1549 to 1564: `rd_kafka_update_app_pos` calls `rd_kafka_offset_store0` when `enable_auto_offset_store` is set. `src/rdkafka_cgrp.c` lines 552 to 557: the auto-commit timer starts with period `auto_commit_interval_ms`; lines 4791 to 4803: its callback, on the main thread, commits assigned offsets unless the group is not in the steady state. This is my reading of the source.
4. Microsoft Learn, "Apache Kafka client configurations for Azure Event Hubs", https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations, librdkafka "Consumer only" table: `heartbeat.interval.ms` 3000 (keep default); `session.timeout.ms` default 45000, recommended 30000, constraint 6000 to 300000; `max.poll.interval.ms` 300000, "Must be > `session.timeout.ms`".
5. Repository files on `main` at the base commit, cited inline by path and line.
6. The C03 smoke run [44248514485](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/44248514485), as reported in [#508 comment 6973117302](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/508#issuecomment-6973117302). I did not read the run artifacts myself.
7. librdkafka v2.16.0 consistent partitioner: CRC-32 of the key modulo the partition count, as cited in the #508 plan's source 7 (`src/rdkafka_msg.c` line 1069). I did not refetch that file in this session.

## 13. Open questions for Hari

Each has a recommended answer that this revision applies. Under decision 6895535291 point 2 Hari adopts recommended answers in advance, but expected outcomes, Azure dispatch and scope are reserved to him (point 4), and Q1 and Q6 change an earlier decision or add a split entry, so all six need his answer.

- Q1. Where is K-57 tested? C10 has only two phases (`phase.py` lines 26 to 27), and both are needed to compare the offset-store modes at the same kill point. K-57 needs a kill before any commit, which is a different kill point. Recommended: test K-57 under C01, with the subject killed at its first message in both C01 phases. C01's timing does not depend on the kill point (section 3.2), so C01 carries K-57 for free, with a 150 s profile so its repeats can complete after a `latest` reset (section 3.3). This changes [decision 6894534441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441) point 3 and the issue body's "C10 includes K-57", and edits blueprint line 1275. Alternatives: (b) add a third C10 phase, which changes `phase.py`, `scenario.yml` and the chart, protected and outside split entry 7; (c) drop K-57 and publish it as not tested.
- Q2. How is C10's automatic-store case judged? Under ADR-022's kill point ("before processing it", line 757), the automatic store can lose a message only if an auto-commit lands within one Blob append before the kill (section 5.1). So C1 will very likely show zero loss, and its result says little about the documented risk. Recommended: keep the approved kill point; base the C10 verdict on H1 (store-after-process loses nothing) only; publish H2 on its own line as `inconclusive` unless a loss is seen, with the text of its `published_text_must_state`. Alternative: amend ADR-022 so the C10 subject is killed after processing its Nth message for longer than `auto.commit.interval.ms` and before recording it. That would make the C1 loss expected rather than rare, but it changes ADR text, the worker's kill path and the verifier's stop rule, so it needs a new split entry before C10 can run. My opinion: the alternative gives a more useful published C10 result; the recommendation is the one that fits this child's scope.
- Q3. Approve both `expected.yaml` files as section 5.2 gives them, including these policy values: C10 kill count 110; C01 kill count 1; the C01 windows [27 s, 45 s) and [42 s, 60 s), whose lower bounds subtract one heartbeat interval and whose upper bounds add a 15 s margin that is not measured; and C10's `commit_precondition`, which the verdict session checks from the archived records (recommended over accepting the DA3 risk by name, the alternative in plan check 7001168119 finding 6).
- Q4. Accept DA1 to DA9 of section 8 by name.
- Q5. Who dispatches the two smoke runs? Recommended: the #499 batch control session, one item at a time, with the section 9 checks reported on #510, as Hari authorized for C03 ([#508 decision 6955995326](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/508#issuecomment-6955995326) point 7). Batch dispatches stay with Hari.
- Q6. How is the archive monitoring gap of a self-killed subject handled (#531 D2)? Today every C10 and C01 repeat records it, so no phase could be published. Recommended: change the archive rule so that, for a C10 or C01 subject whose record ends at its kill-intent row in a configuration with a kill count, a missing native-monitoring index is recorded as an expected absence with the reason `configured_self_kill`, not as a gap; every other missing index stays a gap. It is a change to the archive script and `docs/operations/archive-smoke.md`, outside this plan's files, so it is its own pull request, a new split entry or a #531 follow-up, and must merge before the C10 and C01 batch dispatches. The published C10 and C01 results then state that the killed subject has no native-monitoring evidence. Alternatives: (b) the worker writes its native-monitoring index before the SIGKILL, which adds a step between the kill-intent row and the kill and so changes ADR-022 "Self-termination" (line 757); (c) keep the gap and have Hari waive it by name for each published phase.

## 14. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope. That includes any literal in sections 3.1 to 3.5, any check in section 3.4, any assertion change outside sections 7.1 and 7.2, the content of either `expected.yaml`, any path in section 2.2, or a change to a lock file. It also stops under R16, and when a test shows that a requirement cannot hold as written.

## 15. Size estimate

Estimate, made on 2026-11-19 by comparison with the `c03-inputs` pull request (545), which touched the same kinds of files and also added a reader, sources and schema. Reported, not enforced: no code is trimmed to meet it.
- `c10-c01-inputs`: about 25 files and about 900 changed lines, including tests and the two `expected.yaml` files.

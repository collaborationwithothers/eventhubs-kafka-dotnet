Revision: 1

# c18-scenario: the C18 scenario, its expected outcome and its admission

This plan adds the C18 scenario file and its expected outcome. It also teaches the scenario loader, the worker, the load generator and the chart to accept C18. After it merges, a C18 member Job renders and the worker maps the scenario's stop to the stop path that pull request 2 built. Nothing can dispatch C18 yet: the workflow admission and the pod delete are pull request 4, and the verifier is pull request 5.

The expected outcome, every decision rule and every policy value below are proposals. Each is listed under "Open questions for Hari" with a recommendation. This plan decides none of them.

Base commit: `main` at `fd55d88adaa00342deb91a956e5691c1101ef647`. Every repository citation is at that commit, except those marked "PR 617", which are at commit `cc732a7079f88aa2d09b4d63e3ad35a87dfd5c40` of pull request 617 (open).

## 1. Goal and authority

Goal: pull request 3 of the approved C18 split. The split was approved by #613 [decision 7237226936](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7237226936) point 1, in the order of section 12 of the approved c18-stop-smoke plan. It was amended by #613 [decision 7238011332](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7238011332) point 1, confirmed by Hari's thumbs-up at 2026-11-28T10:37:25Z, which moved "the worker and chart admit C18" into this pull request. Its content is therefore:
- scenario C18 with its `expected.yaml`;
- the load generator admission;
- the worker and chart admission.

Authority, in creation order:
- the #613 issue body;
- #613 decision 7236669224, which adopts the four #597 decisions;
- #597 decision 7237271679, adopted by #613 decision 7237273680, with Hari's thumbs-up;
- #613 decisions 7237177975, 7237226936 and 7238011332;
- the approvals 7237680139 (c18-stop-smoke) and 7238130366 (c18-worker-stop);
- docs/blueprint.md ADR-022 with "Amendment 1" (lines 800 to 840), ADR-014, ADR-019 and ADR-020;
- the C18 row of section 12 "Folded into existing items" (line 1322).

What the C18 row tests, as the merged amendment and fold row state it:
- `Close` against `Dispose` (K-05);
- commit on revoke against lost (K-28);
- the time from the observed stop to process exit against the 30 s grace (K-03, recorded only).

Evidence used: the C18 smoke, run [46142138991](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/46142138991), whose evidence is #613 comment 7239231113. I read the artifact `c18-stop-smoke-46142138991-1` (`c18-stop-smoke.json`) myself on 2026-11-28. In 3 of 3 trials the container ended with exit code 1, about 1 to 2 s after the delete. The watch read the end time in 3 of 3 trials and the 1 s poll in 1 of 3. No replacement pod was started. The member was idle.

Gate on implementation: this pull request uses the plan inputs `StopConfigured`, `ShutdownMode` and `RecordsConsumerErrors`. Pull request 617 adds them (PR 617, `ConsumerProcessingPlan.cs` lines 19 to 24). Implementation starts only after pull request 617 has merged on `main`. If those inputs differ there from this plan's text, the implementer stops (section 13).

## 2. Scope

Files allowed to change:
1. `scenarios/C18/scenario.yaml` (new; 3.1).
2. `scenarios/C18/expected.yaml` (new, protected; section 5, byte for byte as approved).
3. `src/Parchwell.Scenarios/Contracts/ScenarioContracts.cs` and `src/Parchwell.Scenarios/Loading/ConsumerProcessingDefinitionReader.cs` (3.2).
4. `src/Parchwell.Enrichment/ConsumerProcessing/ScenarioConsumerProcessingPlanSource.cs` and `ConsumerProcessingOptions.cs` (3.3).
5. `src/Parchwell.Enrichment/Parchwell.Enrichment.csproj` and `src/Parchwell.LoadGen/Parchwell.LoadGen.csproj` (protected): one `Content` item each for `scenarios/C18/scenario.yaml`, in the form of the existing C01 item (`Parchwell.Enrichment.csproj` line 14; `Parchwell.LoadGen.csproj` line 31). The C01 profile they need is already packaged (line 15 and line 32).
6. `src/Parchwell.LoadGen/Run/Input/LoadGenHostOptions.cs`: line 67 and its summary comment (3.4).
7. `charts/parchwell/templates/_consumer.tpl` lines 2, 13 and 138, and `charts/parchwell/values.schema.json` lines 30, 1188 and 1221 (3.5).
8. Tests under `tests/Parchwell.Scenarios.Tests/`, `tests/Parchwell.Enrichment.Tests/`, `tests/Parchwell.LoadGen.Tests/`, `tests/Parchwell.ArchitectureTests/DeploymentContracts/ChartContractTests.cs` and `charts/parchwell/tests/`, per section 7. Their project files (protected) may change only to add `Content` or `EmbeddedResource` items for `scenarios/C18/scenario.yaml`, in the form their existing C01 items use.
9. `docs/operations/scenario-contract.md`, section "Consumer processing declarations" (lines 147 to 175), and `docs/operations/chart-contract.md` lines 11, 155 and 167 (3.6).

Paths that must not change:
- `src/Parchwell.Verifier*/` and `tests/Parchwell.Verifier.Tests/`. The verifier image does not package C18, and its plan sources still accept only C01, C03 and C10. Pull request 5 changes that.
- `src/Parchwell.Kafka/`: C18 replaces no Kafka setting.
- `src/Parchwell.Scenarios/Contracts/ScenarioPhases.cs`.
- `src/Parchwell.Enrichment/ConsumerProcessing/ConsumerProcessingWorker.cs`, `ConsumerProcessingPlan.cs`, `ConsumerRecordCodec.cs` and `Program.cs`.
- `.github/`, `infra/`, `results/`, `docs/blueprint.md`, every other file under `scenarios/`, `Directory.*.props` and `*.sln*`. No new project, package or reference.

## 3. External contracts

### 3.1 `scenarios/C18/scenario.yaml`

```yaml
schema_version: 1
id: C18
baseline: Parchwell.Kafka
roles:
  - LoadGenProducer
  - EnrichmentConsumer
  - VerifierConsumer
profile: profiles/c01-consumer.yaml

configurations:
  C0: {}
  C1: {}

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
      stop: {after_start_line_ms: 60000, shutdown_mode: close}
    C1:
      stop: {after_start_line_ms: 60000, shutdown_mode: dispose}
```

- The profile is C01's: 4 synthetic messages per second for 150 s, with a processing delay of 200 ms (`scenarios/profiles/c01-consumer.yaml`). That keeps the workload equal to C01's, whose takeover times are the comparison in section 5.
- `after_start_line_ms` is the stop time T of Amendment 1 "Stop". The workflow (pull request 4) deletes the subject's pod T after the start line; the worker never reads T.
- No configuration replaces a Kafka setting. Both members run the baseline: `session.timeout.ms` 30000, `heartbeat.interval.ms` 3000, `enable.auto.commit` true, `auto.commit.interval.ms` 5000, `enable.auto.offset.store` false and `auto.offset.reset` latest (`BaselineSettingsCatalog.cs` lines 149 to 186).

### 3.2 Loader

`ConsumerProcessingDefinition` gains one property, declared outside its positional parameters with a default of an empty map, so its existing constructor and every caller stay unchanged. The verifier's `ScenarioConsumerVerificationPlanSource.cs` constructs no definition, so it still compiles.
```
public ImmutableDictionary<string, ConsumerStopDeclaration> Stops { get; init; }
public sealed record ConsumerStopDeclaration(int AfterStartLineMs, string ShutdownMode);
```
`Stops` holds C18's stop of each configuration and is empty for C03, C10 and C01.

`ConsumerProcessingDefinitionReader` admits C18 as a fourth declaring item. For C18, each configuration entry is a mapping with exactly `stop`, a mapping with exactly `after_start_line_ms` and `shutdown_mode`. The checks, in the existing closed list of scenario-contract.md, become:
- Check 1: only C03, C10, C01 and C18 may declare; each must.
- Check 6: for C18, the entry shape is above (`consumer_processing_shape`, path `consumer_processing.configurations.<id>`).
- Check 7: for C18, `after_start_line_ms` is the plain scalar `60000` in both configurations. `shutdown_mode` is the plain scalar `close` for C0 and `dispose` for C1 (`consumer_processing_literal`, path `consumer_processing.configurations.<id>.stop.<field>`).
- Checks 4 and 8 apply to C18 as to the other three: window `360000`, deadline `30000`, five repeats, no fault, no gate, the three roles in order and the eight measures. Its replacements are none in both configurations, which the existing default branch of `ExpectedOverrides` returns (`ConsumerProcessingDefinitionReader.cs` lines 113 to 119).
- Diagnostic codes are the existing ones; no new code.

### 3.3 Worker admission and plan

- `ConsumerProcessingOptions.Validate` (line 27) accepts `C18` beside C03, C10 and C01. Nothing else in it changes.
- `ScenarioConsumerProcessingPlanSource.GetPlan` (line 24) accepts `C18` with C0 or C1. For C18 it returns:
  - subject: `StopConfigured` true, `ShutdownMode` the configuration's `shutdown_mode`, `RecordsConsumerErrors` true, `KillCount` null, `SlowMessage` null, no consumer settings;
  - witness: `StopConfigured` false, `ShutdownMode` null, `RecordsConsumerErrors` true, no consumer settings.
  - Both get `ProcessingDelayMs` from the profile (200) and `WindowMs` 360000, as today.
- For C03, C10 and C01 the returned plan is unchanged, with `RecordsConsumerErrors` false. Their records therefore stay byte-identical (c18-worker-stop plan, 3.2).

### 3.4 Load generator admission

`LoadGenHostOptions.IsAcceptedPair` (line 67) accepts `C18` with phase C0 or C1 and the configuration of the same name. Its summary comment names C18 with the others.

### 3.5 Chart admission

- `parchwell.consumerScenarios` (`_consumer.tpl` line 2) becomes `C01 C03 C10 C18`.
- The failure messages at lines 13 and 138 name `C01, C03, C10 or C18`.
- `values.schema.json` gains `C18`:
  - in the root `scenario_id` enum after `C10` (line 30);
  - in the enum of the block at line 1188, after `C10`;
  - in the consumer branch's `if` enum at line 1221, giving `["C01", "C03", "C10", "C18"]`.
- No other template or schema change. With this, a C18 render admits the modes `start-line`, `loadgen`, `verify`, `judge`, `consumer-subject`, `consumer-witness` and `consumer-storage-check`, as for C01 (chart-contract.md line 11). Until pull request 5, a C18 `verify` or `judge` Job fails at verifier startup, because the verifier rejects C18 (`VerifierHostOptionsValidator.cs` line 47). Nothing dispatches one before pull request 4.

### 3.6 Owning documentation text

- scenario-contract.md "Consumer processing declarations":
  - the first paragraph names C18 and its experiment ("the workflow deletes the subject's pod T after the start line; the declaration gives T and the shutdown mode");
  - the field table gains `stop.after_start_line_ms`, C18, both configurations, `60000`, and `stop.shutdown_mode`, `close` for C0 and `dispose` for C1;
  - checks 1, 6 and 7 read as in 3.2;
  - the replacements table gains the row C18, none, none.
- chart-contract.md lines 11, 155 and 167 name C18 beside C01, C03 and C10.

## 4. Requirements

R1. WHEN `scenarios/C18/scenario.yaml` is loaded, THEN it loads with no diagnostic, and `Stops` holds C0 `(60000, "close")` and C1 `(60000, "dispose")`.

R2. WHEN a C18 file breaks a check of 3.2, THEN the loader reports that check's code and path, and no other.

R3. WHEN C03, C10 or C01 is loaded, THEN `Stops` is empty and every existing loader test passes unchanged.

R4. WHEN the worker is asked for C18 C0 or C1 as the subject, THEN its plan is the subject plan of 3.3. WHEN asked as the witness, THEN its plan is the witness plan of 3.3.

R5. WHEN the worker is asked for C03, C10 or C01, THEN its plan equals the plan on `main`, field for field, with `RecordsConsumerErrors` false.

R6. WHEN `ConsumerProcessingOptions.Validate` runs with scenario `C18`, THEN it passes for otherwise valid options; with `C19`, THEN it fails `consumer_identity_invalid`.

R7. WHEN `IsAcceptedPair` gets (`C18`, `C0`, `C0`) or (`C18`, `C1`, `C1`), THEN it returns true; with (`C18`, `probe`, `C0`) or (`C18`, `C2`, `C2`), THEN false.

R8. WHEN the chart renders mode `consumer-subject` for C18 C0, THEN it renders one Job named `<run_id>-c0-r<repeat>-consumer-subject`, identical to the C01 render except the scenario identifiers. WHEN it renders C18 with configuration C2, THEN it fails.

R9. WHEN the Enrichment and LoadGen images are published, THEN each contains `scenarios/C18/scenario.yaml`; the Verifier image does not.

## 5. Hypotheses and expected outcome

### 5.1 Basis

- Confluent.Kafka v2.16.0 `IConsumer.cs` lines 670 to 685: `Close` commits offsets if auto commit is enabled and alerts the coordinator that the consumer is leaving. `Consumer.cs` lines 615 to 625: `Dispose` releases resources without committing offsets or alerting the coordinator (S1).
- librdkafka v2.16.0 `INTRODUCTION.md` lines 980 and 992 to 994: close leaves the group and commits final offsets. Without it, "Final offsets are not committed and the consumer will not actively leave the group, it will be kicked out of the group after the `session.timeout.ms` expires" (S2).
- C01's published result (`results/C01/2026-11-23.md`, run 45124695288): a killed member that sent no leave request was replaced 31.8 to 38.6 s after the kill at `session.timeout.ms` 30000. A closed connection alone did not cause early removal there.

### 5.2 Exact content of `scenarios/C18/expected.yaml`

```yaml
schema_version: 1
id: C18
subject: >-
  Confluent.Kafka v2.16.0 documents that Close commits offsets when auto
  commit is enabled and tells the group coordinator the consumer is leaving,
  and that Dispose without Close does neither. librdkafka v2.16.0 adds that
  without close the consumer is removed only after session.timeout.ms
  expires. Microsoft recommends session.timeout.ms 30000 for Event Hubs.

assessment_rules:
  counted_repeats: complete_valid only; H1 and H2 also require reassignment.microseconds not null
  repeats_per_configuration: 5
  replacements: 0
  evidence: >-
    The verifier consumer block of each repeat: status, counts, reassignment
    and shutdown_margin (docs/operations/verifier-contract.md). Reassignment
    runs from the subject's stop_observed row to the first assigned row,
    from the witness, that holds the subject's partitions. It is null when
    the members ran on different nodes. A repeat whose subject wrote no
    stop receipt is incomplete (consumer_stop_receipt_missing) and is not
    counted.
  stop_point: >-
    The workflow deletes the subject's pod 60000 ms after the start line,
    with the chart's 30 s termination grace. The subject treats the stop
    request as ApplicationStopping, finishes the message in progress,
    appends a stop_observed row, then calls Close and Dispose (C0) or
    Dispose only (C1), and writes a stop receipt (ADR-022 Amendment 1).
    Both members run the baseline, including session.timeout.ms 30000,
    heartbeat.interval.ms 3000, enable.auto.commit true,
    auto.commit.interval.ms 5000 and enable.auto.offset.store false.
  window_policy: >-
    27000000 microseconds is session.timeout.ms minus heartbeat.interval.ms,
    because the subject's last heartbeat can precede its stop by up to
    3000 ms. The C1 upper bound adds 15000 ms for rebalance time, the same
    policy value as C01, not a measurement.

configurations:
  C0:
    H1_close_hands_over_before_session_timeout:
      question: >-
        With Close, does the witness hold the subject's partitions before
        the session timeout could have removed the subject?
      holds_when: >-
        All 5 repeats complete_valid, and in each reassignment.microseconds is
        not null and < 27000000.
      contradicted_when: >-
        All 5 repeats counted, and in at least one reassignment.microseconds is
        >= 27000000.
      otherwise: inconclusive
    H3_close_commits_final_offsets:
      question: >-
        With Close, does the witness start after the subject's last processed
        message, so that no message is processed twice?
      holds_when: >-
        All 5 repeats complete_valid, and in each counts.processed_again = 0.
      contradicted_when: >-
        All 5 repeats complete_valid, and in at least one
        counts.processed_again >= 1.
      otherwise: inconclusive
  C1:
    H2_dispose_waits_for_session_timeout:
      question: >-
        With Dispose only, does the witness hold the subject's partitions only
        after the session timeout has removed the subject?
      holds_when: >-
        All 5 repeats complete_valid, and in each reassignment.microseconds is
        not null, >= 27000000 and < 45000000.
      contradicted_when: >-
        All 5 repeats counted, and in at least one reassignment.microseconds is
        < 27000000 or >= 45000000.
      otherwise: inconclusive
    H4_dispose_skips_final_commit:
      question: >-
        With Dispose only, are messages the subject processed after its last
        automatic commit processed again by the witness?
      holds_when: >-
        All 5 repeats complete_valid, and the sum of counts.processed_again
        over the 5 repeats >= 1.
      contradicted_when: >-
        All 5 repeats complete_valid, and in each counts.processed_again = 0.
      otherwise: inconclusive
  both:
    committed_never_processed:
      mode: finding
      when: >-
        In at least one counted repeat, counts.committed_never_processed >= 1.
      text: >-
        A message was marked done without being processed. With
        enable.auto.offset.store false this is not expected in either mode;
        the cause is not verified. It is reported on its own line.
    shutdown_margin:
      mode: recorded_only
      value: >-
        Per repeat, shutdown_margin.receipt_microseconds (stop_observed row to
        stop receipt written) and shutdown_margin.process_end_microseconds
        (stop_observed row to the end of the subject's process, from the
        workflow's watch of the deleted pod), each against the 30 s grace.
    reassignment:
      mode: recorded_only
      value: reassignment.microseconds per repeat
    consumer_errors:
      mode: recorded_only
      value: the number of consumer_error rows per member per repeat

verdict:
  confirmed_when: H1, H2, H3 and H4 each hold.
  contradicted_when: any of H1, H2, H3 and H4 is contradicted.
  no_verdict_when: neither of the above; the run is investigated, not published.
  smoke_run: never published as a result; it gives no verdict.
  separate_lines: the committed_never_processed finding, the shutdown margins, the reassignment values and the consumer-error counts are published on their own lines and do not change the verdict.
  text_must_state:
    - The stop point and window policy in assessment_rules.
    - The workload is synthetic and its load lasts 150 s, the same as C01's.
    - A grace period shorter than the close, a shell-form ENTRYPOINT,
      producer flush on shutdown and shutdown commit with auto-commit off
      are not tested (docs/blueprint.md section 12, C18 row).
    - Each counted repeat's two shutdown margins against the 30 s grace.
    - The C18 smoke measured an idle member only; the margins here are the
      first measurement with a message in flight.

sources:
  - https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/IConsumer.cs
  - https://github.com/confluentinc/librdkafka/blob/v2.16.0/INTRODUCTION.md
  - https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations
```

The field names `reassignment.microseconds`, `counts.processed_again` and `counts.committed_never_processed` exist today (verifier-contract.md line 128). `shutdown_margin.receipt_microseconds`, `shutdown_margin.process_end_microseconds`, the consumer-error count and `consumer_stop_receipt_missing` do not exist yet. Pull request 5 adds them under exactly these names; this file pins them.

## 6. Invariants and safety rules

- `expected.yaml` is assessment-only: no code reads it (ADR-014; `ScenarioLoader.cs` line 9).
- C01, C03 and C10 behaviour, records and loader output are unchanged (R3, R5).
- No verifier file changes, so the verifier still refuses C18.
- No workflow change, so no run of C18 is possible from this pull request.
- No Kafka setting changes for any scenario.

## 7. Test map

### 7.1 Standing fixture rule

When an existing test's fixture is rejected by a new check, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The pull request's implementation notes list each change with the check that forces it. The cold review and Codex review check each listed change.

### 7.2 Assertions this plan supersedes

Each assertion here pins a list that this plan extends. The implementer changes it exactly as stated and lists it in the implementation notes:
- `ChartContractTests.cs` lines 18 to 39: the root `scenario_id` enum gains a final `value => Assert.Equal("C18", value.GetString())`.
- Any test in `tests/Parchwell.Scenarios.Tests/` or `charts/parchwell/tests/` that asserts the exact admitted set C03, C10 and C01, or the chart's failure message text, gains C18, and the message text of 3.5. The implementer finds them by searching for `consumer_processing_unexpected` and for `require scenario C01, C03 or C10`.

### 7.3 Tests

| Requirement | Test | Inputs | Literal expectation |
|---|---|---|---|
| R1 | `StopConsumerProcessingDefinitionTests.Loads_C18` | the committed `scenarios/C18/scenario.yaml` | no diagnostic; `Stops["C0"]` = (60000, `close`); `Stops["C1"]` = (60000, `dispose`); `KillCounts` and `Configurations` empty |
| R2 | `StopConsumerProcessingDefinitionTests.Rejects` (one case per row) | the C18 file with: `after_start_line_ms: 59999`; `shutdown_mode: drain` in C0; C0 and C1 modes swapped; a `kill_count: 1` field added beside `stop`; `stop` missing; a C1 override `EnrichmentConsumer: {session.timeout.ms: 45000}` | `consumer_processing_literal` at `consumer_processing.configurations.C0.stop.after_start_line_ms`; `consumer_processing_literal` at `.C0.stop.shutdown_mode`; `consumer_processing_literal` at both mode paths; `consumer_processing_shape` at `.C0`; `consumer_processing_shape` at `.C0`; `consumer_processing_experiment` at `consumer_processing` |
| R2 | `StopConsumerProcessingDefinitionTests.Admission` | a C19 file that declares `consumer_processing` | `consumer_processing_unexpected` |
| R3 | existing `ConsumerProcessingDefinitionTests` and `KillConsumerProcessingDefinitionTests` | as on `main` | pass unchanged, except 7.2 |
| R1 | `StopConsumerProcessingDefinitionTests.File_is_pinned` | SHA-256 of the committed file | equals the hash the implementer records, as `KillConsumerProcessingDefinitionTests.cs` line 149 does for C10 and C01 |
| R4 | `ScenarioStopPlanSourceTests.C18_plans` | (`subject`, C0), (`subject`, C1), (`witness`, C0), (`witness`, C1) | subject C0: `StopConfigured` true, `ShutdownMode` `close`, `RecordsConsumerErrors` true, `KillCount` null, `SlowMessage` null, `ConsumerSettings` empty, `ProcessingDelayMs` 200, `WindowMs` 360000; subject C1: the same with `dispose`; witness: `StopConfigured` false, `ShutdownMode` null, `RecordsConsumerErrors` true, the rest as the subject |
| R5 | existing `ScenarioPlanSourceTests` and `ScenarioKillPlanSourceTests`, plus one new assertion per existing case that `RecordsConsumerErrors` is false and `StopConfigured` false | as on `main` | as on `main` |
| R6 | `ConsumerProcessingOptions` validation test, new cases | `ScenarioId` `C18` with a valid job name; `C19` | passes; `consumer_identity_invalid` |
| R7 | `C03AdmissionTests`, new cases | (`C18`, `C0`, `C0`), (`C18`, `C1`, `C1`), (`C18`, `probe`, `C0`), (`C18`, `C2`, `C2`) | true, true, false, false |
| R8 | `charts/parchwell/tests/test_consumer_chart.py`, new cases | render `consumer-subject` and `consumer-witness` for C18 C0 and C1; render C18 C2 | the Jobs equal the C01 renders with `C01` replaced by `C18` in every value; C2 fails with the schema's error |
| R9 | the existing image packaging check for scenario content, if one exists; otherwise acceptance command 4 | published outputs | the Enrichment and LoadGen outputs hold `scenarios/C18/scenario.yaml`; the Verifier output does not |

## 8. Deployment assumptions

None for this pull request: no run of C18 is possible until pull requests 4 and 5. The live facts the expected outcome relies on come from the C18 smoke and from C01's result (section 5.1). Each is stated with how its evidence was taken:
- SIGTERM reaches the worker: smoke run 46142138991, fresh connection, idle member, 3 trials.
- No replacement pod: the same run.
- Removal after the session timeout when no leave request is sent: C01 run 45124695288, fresh connection, under C01's load of 4 messages per second.

The shutdown time with a message in progress is not measured. The expected outcome records it (`shutdown_margin`); it decides nothing.

## 9. Acceptance commands

1. `dotnet build -warnaserror` -> 0 warnings, 0 errors.
2. `dotnet test` over the solution -> all pass.
3. `bash infra/scripts/lifecycle/check-offline.sh` -> exit 0, including the chart render and kubeconform checks.
4. `dotnet publish` of Enrichment, LoadGen and Verifier, listing `scenarios/` in each output -> C18 in the first two only.
5. `sha256sum scenarios/C18/expected.yaml` -> equals the hash of section 5.2's block, saved without the fence, with a final newline. The implementer records the hash in the pull request.
6. `git diff --name-only origin/main...HEAD` -> only files of section 2.
7. An ASCII scan of the changed files -> no character outside ASCII.

## 10. Plan-check answers

(a) Effort that scales with a number unknown at planning time: none. The inputs are fixed files and fixed test cases.

(b) Stricter than the parent: the amendment lets a scenario omit the shutdown mode (default `close`). This plan requires C18 to name it in both configurations, so the file states what each configuration tests. It also proposes H3 and H4, which turn K-05's commit half into decision rules. The fold row names K-05 and K-28 as tested but gives no rule.

(c) Tests that depend on code, data or behaviour neither on `main` nor in this pull request:
- R4's plan inputs exist only in pull request 617 (PR 617, `ConsumerProcessingPlan.cs` lines 19 to 24). Checked against that source at `cc732a70`. Implementation waits for its merge (section 1).
- The expected outcome's new evidence fields come from pull request 5. No test in this pull request reads them.

## 11. Open questions for Hari

1. Approve the expected outcome of section 5.2 as written, including H1 to H4, the verdict rule and the evidence field names. Recommendation: approve. H1 and H2 test the takeover half of K-05, with C01's windows so the two results compare. H3 and H4 test the commit half. H4's rule needs only one reprocessed message across five repeats, because a stop that lands just after an automatic commit can leave nothing to reprocess (inference from the 5000 ms commit interval).
2. Policy values. Recommendation: approve all of them.
   - Stop time `after_start_line_ms` 60000. It lands mid-load: 90 s of the 150 s load remain, enough for C1's expected 30 to 45 s takeover.
   - Window 360000 and completion deadline 30000, as C01's.
   - Reuse of C01's profile: 4 messages per second for 150 s, processing delay 200 ms.
   - The C0 threshold of 27000000 microseconds, and the C1 window from 27000000 to 45000000, whose 15000 ms allowance is C01's policy value.
3. A hung shutdown live. Recommendation: accept this risk by name, so the confirmation rule applies. The pod's grace is 30 s (`values.yaml` line 105). The worker's `Close` bound is also 30 s, but starts only after the stop is observed (c18-worker-stop plan, 3.6 step 4). So a `Close` that hangs is killed before its bound expires. So is an append that hangs inside a `Close` callback, which is Codex's P2 on pull request 617 (review comment 4726939688). Either way the subject writes no receipt, and the repeat is incomplete (`consumer_stop_receipt_missing`) rather than `timed_out`. The expected outcome counts such a repeat as not counted, so it can make a hypothesis inconclusive but never wrong. The smoke's idle closes ended about 1 to 2 s after the delete. The alternatives are a longer grace for C18, which is a chart contract change, or a shorter `Close` bound, which amends the approved worker plan. Neither is recommended.

## 12. Deferred work

- Pull request 4, workflow admission and the pod delete. On the verdict path; deferred by the approved split order. Prerequisites it must meet, from the smoke and the checks:
  - Read the deleted pod's container end time with a watch started before the delete. The poll read it in only 1 of 3 smoke trials (#613 comment 7239231113).
  - Delete the subject's pod `after_start_line_ms` after the start line.
  - Confirm the subject's process has terminated before the verifier reads offsets (Amendment 1).
  - Keep the 30 s grace (question 3).

  Responsible: the `plan-pr` session for pull request 4. Gate it blocks: any C18 dispatch. Completion evidence: pull request 4 merged on `main`.
- Pull request 5, the verifier. On the verdict path; deferred by the split order. It must emit the fields section 5.2 pins: `reassignment` from `stop_observed`, `shutdown_margin` with its two fields, the consumer-error count and `consumer_stop_receipt_missing`. It also needs mutation acceptance on `main` under docs/agents/factory.md "Evidence pack". Responsible: the `plan-pr` session for pull request 5. Gate it blocks: any C18 verdict. Completion evidence: pull request 5 merged on `main`.

## 13. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" in any of these cases:
- it needs to change a contract in section 3, a requirement in section 4, the scope in section 2, or one byte of section 5.2;
- pull request 617 has not merged;
- the plan inputs on `main` differ from 3.3.

## 14. Size estimate

Estimate, dated 2026-11-28, from #510's comparable pull request (two scenarios, the reader and three admissions): about 400 to 700 changed lines in 15 to 22 files, most of them tests. Not measured; reported, not enforced.

## 15. Sources

Tools: `curl` of raw files at pinned tags, the GitHub REST API for the smoke artifact and pull request 617, and repository files at the base commit. All read 2026-11-28.
- S1. Confluent.Kafka v2.16.0: `src/Confluent.Kafka/IConsumer.cs` lines 670 to 685; `src/Confluent.Kafka/Consumer.cs` lines 615 to 625.
- S2. librdkafka v2.16.0 `INTRODUCTION.md` lines 978 to 994 ("High-level KafkaConsumer" termination).
- `results/C01/2026-11-23.md` (C01 takeover times).
- The C18 smoke artifact `c18-stop-smoke-46142138991-1`, read by the GitHub artifacts API.

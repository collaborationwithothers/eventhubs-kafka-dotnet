# PR3b classification contract-grade plan

Revision 2. Issue 404, pull request short name `PR3b classification`. Status: not posted, not checked, not approved. Implementing tool: Codex ([decision 6761524200](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761524200) point 1).

This pull request adds one diagnostic-only verifier mode, `b10-abrupt-verify`. It reads one B10 C1 abrupt repeat's first-instance and successor objects from the `ledger` container, reconciles them independently, reads that repeat's window of `document-events` back with a value hash for probe records, and writes one `nonfinal_result` object with per-cohort and per-arm counts, a comparison and eligibility reasons. It never selects a verdict, never publishes a result and never makes the abrupt dialect admissible to any scientific reader.

## 0. Changes since Revision 1

Revision 1 (`b4ece136cb823504f76d0f47ab380bed59223610`) failed its fresh reproduction check. Each finding and its fix:

| Finding | Fix in Revision 2 |
|---|---|
| B1: R3 overlapped R5, R11, R12 and R14 with no precedence | New section 5.1 states the stages and their check order. R3 now lists its checks itself and no longer cites the whole `b10_abrupt.py` range; seal cohort, drain and guard checks belong to R11 and R12; termination checks belong to R5. R14 states that a wrong invocation count wins over the inventory check. |
| B2: T5 "successor directory absent" literal did not follow from R13 | R13 holds successor records out only when the successor directory was identified. T5 now expects `[3, 9]` with window A and `[9]` without the successor record, as the prototype gives. |
| B3: T5 late case contradicted R9 | T5 now expects the late case to keep its cross-restart resend members (`pair_count` 1). The "resend members null" sentence applies only to the other T5 cases. |
| B4: five surviving mutants; successor read limits not pinned | Section 4.2 pins the successor read limits and the per-pod 1073741824-byte total, from `b10_abrupt.py` lines 396 to 398. T3 and T5 gain cases for `is_faulted` and `guard_observations_complete`, `cross_host_timing`, `release_utc`, `cgroup_after`, a 65537-byte and a 65536-byte `capabilities.json`, the probe key and `probe_id` rule, the control `inventory_count`, `invocation_utc`, the kill intent and exit after the last signal. Section 8.2 adds each mutant; all are caught. |
| N1: `resend_seen` undefined | R9 defines it and bounds it by `resend_invoked`. T7 adds a duplicated-resend case. |
| N2: `native_errors` on a join failure | R9 states it stays present; T7 h pins it. |
| N3: a successor name in R2's first-instance exemption | Removed. T1's boundary is now an empty `journal.jsonl`. |
| N4: R12 "member for member" compared unequal member sets | R12 names the seven shared members. |
| N5: a directory without `registration.json` | R2 states the outcome. |
| Never-ran inventory, G1, U1, DA2 | Updated for trials 8 and 9 (section 7). |
| DA1 documentation | Section 9 quotes the container-scope and role pages. |
| Hari's answers | Decisions [6761524200](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761524200) and [6761542603](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761542603) are folded in; section 13.1 records each answer. No fix changes a literal, reason name, path or option those answers cover. |
| Missing question on ADR-021 protocol rule 13 | New Q13. |

## 1. Goal and authority

Goal: a one-repeat diagnostic observation of the restart question, read back and classified per repeat as a nonfinal observation ([decision 6749995101](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749995101), option A), as the second of three slices ([split decision 6750021224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750021224) item 2: "Verifier (protected): readers, independent reconciliation, topic read-back and per-arm classification for one repeat, as a nonfinal diagnostic result. Predecessor: PR3a.").

Authority, in creation order, as recognized under docs/agents/factory.md "Item lifecycle" step 3:
- Issue 404 body and the decisions listed in `items/404/state.md` on `main` (covers comment 6756884940).
- [6705495747](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6705495747): "Tool: Codex", posted before `ready-for-agent`.
- [6708991894](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6708991894): PR3 "need[s] its own plan, rechecked against merged predecessors. Hari merges every pull request here."
- [6749565231](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749565231) (option 2): the verifier plan carries a never-ran inventory and each stage's test "runs it for real". "A further trial needs Hari's new explicit approval and his gate approval."
- [6749995101](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749995101) (option A): "It is not a verdict, not a published result, and not an eligibility finding (eligibility needs at least 3 of 5 repeats)."
- [6750021224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750021224) item 2, quoted above. "The five-repeat sweep, judge, verdict and candidate work, and scientific admission, are not split or planned by this decision."
- [6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614): standing rules for four named pull requests only; "Nothing else."
- [6761052971](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761052971): "Start planning PR3b now, on `main` at `aadb0c6d`", with a never-ran inventory; "this decision does not extend the standing decisions to PR3b. PR3b changes protected verifier paths, so Hari approves its plan and merges it."
- [6761095907](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761095907), [6761380716](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761380716), [6761394550](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761394550): trials 8 and 9 (context for section 7; no scope change).
- [6761524200](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761524200) and [6761542603](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761542603): Hari's answers to Revision 1's Q1 to Q12. The second states: "If the reproduction check or a plan check changes a literal, path or option these answers cover, the changed choice returns to Hari as an open question."
- [6761783572](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761783572): no further B10 trial until two race fixes merge (replacing 6761380716's rerun); plan a partial-frame race fix and a storage readiness race fix in their own pull requests.
- docs/blueprint.md ADR-012 and ADR-021 (lines 659 to 730 on `main` at `aadb0c6d`), in particular "Permitted conditions" (line 681), decision rules 2, 3, 5 and 6 (lines 693, 694, 696, 697) and protocol rules 5, 12, 13, 14 and 15 (lines 708, 715 to 718).

Catch-up: Revision 1 read 184 comments. Refresh for Revision 2 (2026-11-10): 190 comments; new since then 6761095907, 6761380716, 6761394550, 6761524200, 6761542603, 6761783572, each read in full; no comment edited or deleted. Issue body `updated_at` 2026-11-10T07:37:13Z.

Baseline: `main` at `aadb0c6d5252d6ca9638d116c1bbf7339c17037e`. Merged predecessors: successor fix (PR 421), capture contention race fix (PR 422), PR3a evidence completeness (PR 423).

Scope record used as input, not authority: `items/404/plan-verifier.md` Revision 1 on `claude/404-plan`. Its findings were rechecked here. F1 and F2 are resolved on `main` by PR3a. F3 and F5 remain and stay out of scope (PR3c and later slices). F4 is this plan's R8. F6 does not apply: no C5 target changes (section 3).

## 2. Findings this plan rests on

Each was checked on `main` at `aadb0c6d` or on the trial artifacts.

- G1. No verifier mode past `start-line` has run in any diagnostic trial. All nine `b10-abrupt-trial.yml` artifacts were downloaded and their `phase-status.json` read by script. Trials 1 to 6 and 8 record `start-line complete, loadgen failed`. Trial 7 records `start-line complete, loadgen complete, loadgen2 failed`. Trial 9 records `start-line failed`. No other mode appears.
- G2. PR3a's 15 first-instance members and the successor's 7 members have never been published together by a live trial. PR3a merged after trial 7, and trial 7 published 7 first-instance members and only the successor's `registration.json`. Trial 8 ran on `aadb0c6d` and failed at `first_termination` with `b10_abrupt_publication_failed`; its artifact holds no first-instance object. Trial 9 failed at `start-line` and its artifact holds only `phase-status.json`.
- G3. The real LoadGen export path on `main` produces all 22 files. `B10AbruptWireProducerTests.Given_WireOutputDirectory_When_Run_Then_WritesProducedArtifacts` with `PARCHWELL_B10_ABRUPT_WIRE_OUTPUT` set wrote `export.json` and 22 files. Its successor runs "on the real evidence, adapter and statistics chain with the host's completion behaviour" (`B10AbruptWireProducerTests.cs` lines 368 to 371, the successor fix's `B10AbruptSuccessorCompletionTests.Chain`). Its bytes carry wall-clock timestamps, so two runs differ; tests pin structure and counts, never these hashes.
- G4. The control cohort files are not schema one. `control-manifest.json` is a schema 2 envelope of kind `cohort_inventory`, and `control-ledger.jsonl` rows have exactly the members `attempt_id,completion_source,delivered,error_code,persistence_status`. The ordinary `ManifestReader.Read` and `LedgerReader.ReadRows` reject both (scratch run: `unsupported_schema_version:schema_version` and `missing_property:attempt`). The timeout cohort files are schema one and both ordinary readers accept them (scratch run: success, 1 inventory entry, 1 row). `docs/operations/b10-abrupt-contract.md` line 71 says "The cohort files are schema-one ordinary bytes"; that is false for two of the eight files.
- G5. The successor's `manifest.json` and `ledger.jsonl` use the same two abrupt formats as the control cohort files, so one reader serves both.
- G6. `MessageIdentityReader.Read` reads the key and the `instance-id`, `attempt-id` and `sequence` headers only; it never reads the value (`src/Parchwell.Verifier/VerifyRun/Window/MessageIdentityReader.cs` lines 13 to 29). ADR-021 decision rule 3 requires "identical logical key/sequence/payload".
- G7. Trial 7's first-instance bytes are the pre-PR3a dialect: its seals have no `native_error_count`, its manifest has no `cohort_files`, and its `termination.json` has `fatal_capture_status` `not_implemented_pr3`. The prototype rejects them with `b10_restart_first_evidence_unavailable` (8 cohort files missing). With placeholder cohort files added, it rejects them with `b10_restart_identity_unauthenticated` at the seal data members.
- G8. Neither `WindowConsumer` nor `MessageIdentityReader` is a C5 mutation target (`tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` has no match for either name).

## 3. Scope

### 3.1 Files allowed to change

1. New code under `src/Parchwell.Verifier/VerifyRun/Abrupt/` (new folder; file split and private names are the implementer's choice): the mode handler, the readers of section 4.2, the reconciliation and classification of section 5, and the `nonfinal_result` codec.
2. `src/Parchwell.Verifier/Program.cs`, `VerifierHostOptions.cs` (`VerifierMode` and `VerifierModeParser`) and `VerifierHostOptionsValidator.cs`: register mode `b10-abrupt-verify` with the options of section 4.1 only.
3. `src/Parchwell.Verifier/VerifyRun/Input/ConsumedRecord.cs`, `VerifyRun/Window/MessageIdentityReader.cs` and `VerifyRun/Window/WindowConsumer.cs`: the optional value hash of R8, in the shape of decision 6761542603 (Q5 option B): a new `WindowConsumer.Drain` overload taking an optional key set.
4. New test files under `tests/Parchwell.Verifier.Tests/Component/`, every class named with prefix `B10AbruptVerify`, and the trial 7 fixture directory `tests/Parchwell.Verifier.Tests/Component/Fixtures/b10-abrupt-trial-42679791413/` (section 8, T2). They go under `Component/` because the mutation-only build already removes `Component/**/*.cs` (`Parchwell.Verifier.Tests.csproj` lines 20 to 22), and these tests run the LoadGen test assembly or Docker. A new folder would need a csproj change, which section 3.2 forbids.
5. `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptWireProducerTests.cs`: only the environment switch `PARCHWELL_B10_ABRUPT_WIRE_NATIVE_ERRORS` (decision 6761542603, Q11), so the export can also produce PR3a's native-error variant.
6. `docs/operations/verifier-contract.md` lines 3 and 5; a new section "Nonfinal abrupt result" in `docs/operations/b10-abrupt-contract.md`; and line 71 of that file, corrected to state that `control-manifest.json` and `control-ledger.jsonl` are abrupt schema 2 bytes (decision 6761542603, Q9).
7. `items/404/state.md` and a new receipt `items/404/pr3b-receipt.md`.

Protected paths touched: `src/Parchwell.Verifier/**` and `tests/Parchwell.Verifier.Tests/**` (`Parchwell.Verifier*/` in `.github/CODEOWNERS`). Hari merges.

### 3.2 Paths and behaviour that must not change

- `tests/Parchwell.Verifier.Tests/Host/B10AbruptRejectionTests.cs`; it passes unchanged.
- `OutcomeClassifier.cs`, `BookkeepingChecks.cs`, `B10RepeatChecks.cs`, `B10IntakeChecks.cs`, `RestartManifestReader.cs`, `B10SweepHandler.cs`, `B10LateWriteChecks.cs`, every file under `src/Parchwell.Verifier/Judge/` and `Common/Phase/`, `PhaseWireCodec.cs`, `ManifestReader.cs`, `LedgerReader.cs`, `ArtifactIndexReader.cs`, `VerifierResultCodec.cs`. Every existing mode's behaviour and output bytes.
- Every C5 target and `.github/workflows/verifier-c5-mutation.yml`; every workflow; `infra/**`, `charts/**`, `scenarios/**`.
- `src/Parchwell.LoadGen/**`, `src/Parchwell.Kafka/**`.
- `scenarios/B10/expected.yaml` (does not exist; must not be created), everything under `results/`, `docs/blueprint.md`, `docs/agents/**`, `AGENTS.md`, `CLAUDE.md`, `README.md`, any `*.csproj`, `Directory.*.props`, `*.sln*`. No new project, assembly reference or NuGet package.
- Every existing test assertion; none is loosened, skipped or deleted.

### 3.3 Files shared with other open work

- The capture-loss race fix (approved Revision 7, being implemented) changes `src/Parchwell.Kafka/Statistics/KafkaStatisticsSession.cs`, new tests under `tests/Parchwell.Kafka.Tests/Statistics/`, `docs/operations/statistics-bridge.md` line 87, `items/404/state.md` and its own receipt. The only shared file is `items/404/state.md`; whichever merges later rebases it. Indirect dependency: PR3b's tests consume the successor's `statistics.jsonl` and `capabilities.json` produced through `KafkaStatisticsSession`.
- The partial-frame race fix and the storage readiness race fix ([6761783572](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761783572) points 2 and 3) are not planned yet. The first is expected to change `B10AbruptWire.WriteMessageAsync` in `src/Parchwell.LoadGen`, which this plan does not edit but whose export bytes T1 to T8 read. The second's file set is unknown.

The stop rule (section 14) covers a change any of them makes to the formats, members or export file set this plan reads.

## 4. External contracts

### 4.1 Mode and configuration (new; PR3c depends on it)

- `Verifier:Mode` value `b10-abrupt-verify`. `VerifierModeParser.TryParse` accepts it; every existing mode keeps its value.
- Required options, each validated as the existing modes validate them: `ScenarioId` exactly `B10`, `ConfigurationId` exactly `C1`, `Phase` exactly `C1`, `Repeat` exactly `1`, `RunId`, `VerifierCommit` equal to `PARCHWELL_SOURCE_COMMIT`, `JobName`, `PodName`, `PodUid`, `LoadGenImage`, `VerifierImage`, `BlobServiceUri`, `LedgerContainer`, `VerifierContainer` (different from `LedgerContainer`), `NamespaceName`, `VerifierClientId`, `TokenTimeout`, `MetadataTimeout`, `WatermarkTimeout`, `PollSlice`, `BlobIoTimeout`, `IoTimeout`, `SafetyStop`, `CleanupTimeout`, `ProcessLifetime`, `MaxConsumedRecords`, `MaxConsumedBytes`, `MaxArtifactBytes`, `MaxTotalInputBytes`, `MaxPartitions`, `MaxInputDepth`, `MaxOutputBytes`, `MaxDiagnosticsPerCheck`, `MaxDiagnosticBytes`. Any other value of the fixed fields fails validation, and the host exits nonzero before any read.
- Inputs read: from the verifier container, `PhasePaths.StartLine`, `PhasePaths.StartLineCompletion` and `PhasePaths.BrokerInventory` for `(RunId, C1, 1)`, with the checks `VerifyRunHandler` applies to them; from the ledger container, the listing of `<RunId>/C1/r1/b10-abrupt-diagnostic/` and the objects under it; and `document-events`.
- Output: one create-only object in the verifier container at `<RunId>/C1/r1/b10-abrupt-diagnostic/nonfinal-result.json`. No `WorkerReceipt`, no `result.json`, no `verify-completion.json` (decision 6761542603, Q10).
- Exit code: `0` exactly when the `nonfinal_result` object was created and read back byte-identical, whatever its eligibility; nonzero otherwise (Q10).

### 4.2 Inputs fixed on `main` (pinned; a change is a stop-rule event)

- Wire envelope, identity members, journal frame members and per-kind `data` members: `docs/operations/b10-abrupt-contract.md` "Wire and state machine" (lines 26 to 42), as `infra/scripts/phase/b10_abrupt.py` lines 26 to 45 list them, including `native_error`, `cohort_seal.native_error_count`, `abrupt_manifest.cohort_files` and `termination.native_error_count,native_fatal_count`.
- First-instance members (15) and their order, read limits and binding roles: same file, "First-instance publication" (lines 56 to 69). The six envelope members `registration.json`, `timeout-seal.json`, `control-seal.json`, `kill-selection.json`, `abrupt-manifest.json` and `termination.json` are limited to 65536 bytes each, and `journal.jsonl` to 16777216 bytes (`b10_abrupt.py` lines 13 and 15).
- Successor members (7): `registration.json, ledger.jsonl, manifest.json, statistics.jsonl, request-lifecycle.jsonl, capabilities.json, completion.json` (`b10_abrupt.py` line 24). Read limits, as `b10_abrupt.py` `download` sets them (lines 396 to 398): `ledger.jsonl` 268435456, `manifest.json` 67108864, `statistics.jsonl` 134217728, `request-lifecycle.jsonl` 134217728, every other member 65536 bytes. Only `request-lifecycle.jsonl` may be empty.
- Per pod directory, the members read total at most 1073741824 bytes (`b10_abrupt.py` lines 398 and 403).
- `cohort_inventory` data members `instance_id,cohort,inventory_count,inventory,bookkeeping_complete`. Inventory entry members `attempt_id,invocation_ordinal,predecessor_attempt_id,key,sequence,payload_length,payload_sha256` (export, G4). Probe ledger row members `attempt_id,completion_source,delivered,error_code,persistence_status`.
- Successor `capabilities.json` members read: `statistics_completion.is_complete`, `guard_observations_complete`, `is_faulted` (export).
- Probe Kafka record: key UTF-8 `B10/{run_id}/C1/1/{same_instance|cross_restart}`; value the payload bytes; headers `instance-id`, `attempt-id`, `sequence`. Timeout record: ordinary key and the same three headers plus `tenant-id`, `document-id`, `event-type` (scratch Kafka run, section 8.1).
- Persistence status numbers: `NotPersisted=0`, `PossiblyPersisted=1`, `Persisted=2`, as `B10RepeatChecks.IsPermittedTimeout` uses them ([PersistenceStatus](https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.PersistenceStatus.html), cited in ADR-021).

### 4.3 Output: `nonfinal_result` (new; closed)

Envelope: exact members `schema_version` `2`, `dialect` `"b10_abrupt"`, `purpose` `"diagnostic_trial"`, `synthetic` `true`, `scientific_admissible` `false`, `kind` `"nonfinal_result"`, `identity`, `data`. `identity` has the 12 identity members: `run_id` RunId, `scenario_id` `"B10"`, `configuration_id` `"C1"`, `phase` `"C1"`, `repeat` `1`, `source_commit` VerifierCommit, `image_reference` VerifierImage, `job_name` JobName, `pod_name` PodName, `pod_uid` PodUid, `instance_id` PodName, `predecessor_instance_id` `null` (Q10).

`data` exact members:
- `first_instance_id`, `successor_instance_id`: strings, or `null` when that pod directory was not identified.
- `start_line`, `end_line`: arrays of `{partition, offset}` sorted by partition, or `null` when not read.
- `window_complete`: boolean.
- `timeout`: `{ok, loss, phantom, reported_failure, duplicate, reorder}` from the unchanged `OutcomeClassifier.Classify` over the timeout ledger rows and the window's timeout records, or `null` (R11).
- `arms`: exactly two arm objects, `same_instance` then `cross_restart` (R9).
- `comparison`: one of `both`, `cross_restart_only`, `same_instance_only`, `neither`, `unavailable` (R10).
- `native_errors`: `{count, fatal_count, tail}` with `tail` `"unknown_due_to_intended_kill"`, or `null` (R6).
- `eligibility`: `"ineligible"` when `eligibility_reasons` is nonempty, otherwise `"pending_sweep"`; never `"eligible"` (Q3).
- `eligibility_reasons`: array of distinct reason literals of section 4.4, in that list's order.
- `finality`: `"nonfinal"`.

Arm object exact members: `arm`, `accepted_originals`, `checkpoint_unconfirmed`, `selected_attempt_id`, `selected_sequence`, `originals_without_report`, `originals_seen`, `unreported_seen`, `unreported_unseen`, `selected_original_seen`, `resend_attempt_id`, `resend_invoked`, `resend_terminal` (`{delivered, error_code, persistence_status}` or `null`), `resend_seen`, `pair_count`, `witness` (`{original_attempt_id, resend_attempt_id, original: {partition, offset}, resend: {partition, offset}}` or `null`), `lost_resend` (boolean or `null`). Counts are JSON integers from 0 to 9223372036854775807. Absence rules are R9's. Whether ADR-021 protocol rule 13 permits the members not named in that rule is Q13.

Writer and reader limits: the existing `MaxOutputBytes`, depth 32, no duplicate member, no unknown member, no fractional or negative count. Old readers reject it (R7).

### 4.4 Eligibility reason literals (new; decision 6761542603, Q2)

In this order:
1. `b10_restart_configuration_invalid`
2. `b10_restart_first_evidence_unavailable`
3. `b10_restart_identity_unauthenticated`
4. `b10_restart_termination_unverified`
5. `b10_restart_native_error_captured`
6. `b10_restart_fatal_captured`
7. `b10_restart_timeout_bookkeeping_unavailable`
8. `b10_abrupt_control_incomplete` (existing name, same meaning as the contract's failure table)
9. `b10_restart_successor_evidence_unavailable`
10. `b10_restart_resend_count_invalid`
11. `b10_abrupt_successor_late` (existing name)
12. `b10_restart_window_incomplete`

Comparison-blocking reasons (R10): every reason above except 5, 6 and 7.

Not in the list, and why: `kill_too_early`, `kill_too_late` and `b10_abrupt_exposure_inconclusive` cannot reach the verifier with a complete publication. The supervisor writes no termination receipt and no selection without a nonempty kill selection (`b10_abrupt.py` lines 507 and 508 reject an entry count other than 1 with `kill_too_late`), and it blocks the kill arm on an empty control selection (contract line 154). If such an object set is ever published, the verifier's own recomputation (R4) fails it with reason 3.

## 5. Requirements

Every observation below happens at the real `b10-abrupt-verify` handler, given real store objects and a real Kafka window, through the entry point of section 4.1. It is read from the `nonfinal_result` the handler writes. Private helpers are examples, not the place of observation. Each requirement keeps unrelated evidence valid in its tests.

### 5.1 Stages and precedence

The handler runs these stages. Each stage adds at most one reason, from the first check that fails in it, in the order the stage's requirements list them. Stages after a failed stage still run unless stated.

1. Start line (R1). On failure no other stage runs.
2. Listing and first instance (R2, then R3, then R5, then R4). On failure, stages 3 to 6 and the record join do not run; the window stage still runs, and its records are not joined.
3. Native errors (R6), after stage 2 passes. It may add reasons 5 and 6 together.
4. Timeout cohort (R11), after stage 2 passes.
5. Control cohort (R12), after stage 2 passes.
6. Successor (R14), after stage 2 passes.
7. Window (R15).
8. Record join (R13), after stages 2 and 7 pass.

### 5.2 Requirements

R1. WHEN the start-line note or its completion receipt fails the checks `VerifyRunHandler` applies to them (lines 84 to 147), or the note's expected job names differ from `PhaseIdentity.JobName` and `PhaseIdentity.RestartJobName` for `(RunId, C1, 1)`, THEN the reasons are exactly `["b10_restart_configuration_invalid"]`. `start_line`, `end_line`, `timeout` and every arm member except `arm` are `null`, and no Kafka records are consumed.

R2. Listing and identification. Every name under `<RunId>/C1/r1/b10-abrupt-diagnostic/` must be `<directory>/<member>`. The first pod is the one directory whose `registration.json` identity `job_name` is `PhaseIdentity.JobName(RunId, C1, 1)`; the successor is the one whose `job_name` is `PhaseIdentity.RestartJobName(RunId, C1, 1)`.
- WHEN the listing is incomplete, or no directory is identified as the first pod, or the first pod lacks any of the 15 members, or a member is empty or exceeds its read limit (section 4.2), or the first pod's members exceed 1073741824 bytes in total, THEN the reason is `b10_restart_first_evidence_unavailable`.
- WHEN a name is not of that form, or a member name is not in its directory's set, or a directory has no `registration.json`, or its registration names neither Job, or two directories claim the same Job, THEN the reason is `b10_restart_identity_unauthenticated`.
- In either case `native_errors`, `timeout` and every arm member except `arm` are `null`.

R3. First-instance authentication. WHEN any check below fails, THEN the reason is `b10_restart_identity_unauthenticated` and R2's `null` rule applies. In order:
- each of `registration.json`, `timeout-seal.json`, `control-seal.json`, `kill-selection.json`, `abrupt-manifest.json`, `termination.json` and `control-manifest.json` is strict JSON with the exact envelope members and values of section 4.2 for its kind, and the exact `data` members of its kind;
- each identity equals RunId, `B10`, `C1`, `C1`, `1`, VerifierCommit, LoadGenImage, the first Job, the pod directory name as `pod_name` and `instance_id`, and a `null` predecessor, with one `pod_uid` across all of them;
- `termination.manifest` binds `abrupt-manifest.json`, and the manifest binds `registration.json`, `journal.jsonl`, both seals and `kill-selection.json` by length and lowercase SHA-256;
- `journal.jsonl` ends with a newline and has no other trailing content, holds 1 to 4096 frames, and the count equals `journal_entry_count`;
- every frame has the exact frame members, `sequence` equal to its 1-based line number, the first instance's identity and a known kind with its exact `data` members;
- `gaps` is `[]` and no `gap` frame exists;
- no attempt has two `prepared` frames; every `accepted` names a prepared attempt with its `prepared_sequence`, after it, at most once; no `synchronous_rejection` frame; every `terminal` names a prepared attempt, at most once; every prepared attempt is accepted;
- `native_error` frames have `capture_sequence` 1 to n in journal order, and `termination` `native_error_count` is n and `native_fatal_count` the number with `is_fatal` true;
- each probe `prepared` has key and `probe_id` equal to its arm's key (section 4.2) and `payload_sha256` equal to the selection entry's;
- `cohort_files` has the eight names of the contract table in order, each with `length` and `sha256` equal to its seal or checkpoint binding descriptor and to the published bytes;
- both seals have `fatal_capture_status` `journal` and a `native_error_count` from 0 to n.

R4. Checkpoint and selection recomputation, after R5. WHEN the journal disagrees with a checkpoint or the selection, THEN the reason is `b10_restart_identity_unauthenticated`.
- The checkpoint frames are exactly one per arm, `same_instance` first.
- Per arm, the five originals are the arm's `prepared` frames with `predecessor_attempt_id` `null`, in `invocation_ordinal` order, with sequences 0 to 4, each accepted before the checkpoint frame. `accepted_attempt_ids` equals that list. `selected_attempt_ids` equals its members with no `terminal` frame earlier than the checkpoint frame, in the same order.
- The kill selection's `checkpoint` equals the `cross_restart` checkpoint frame, and its `checkpoint_descriptor` binds that frame's journal line. `candidate_attempt_ids` equals that checkpoint's `selected_attempt_ids`. Each terminal exclusion names a candidate's `terminal` frame by sequence. The single entry is the first candidate with no `terminal` frame anywhere in the journal, with key, sequence, payload length and hash equal to its `prepared` data, and a base64 payload of that length and hash.

R5. Termination chain, after R3. WHEN any check below fails, THEN the reason is `b10_restart_termination_unverified` and R2's `null` rule applies. In order:
- `mechanical_status` `complete`, `eof_complete` true, `fatal_capture_status` `journal_through_eof`, `native_tail` `unknown_due_to_intended_kill`;
- `termination.signals` equals the journal's `signal` frame data, with arm and signal pairs exactly `(same_instance, 19), (same_instance, 18), (cross_restart, 19), (cross_restart, 9), (cross_restart, 9)`;
- each signal's process equals the termination process, its `return_value` is `null` for the fourth (the intent) and `0` otherwise, and the fifth's `intent_sequence` is the fourth's journal sequence;
- exactly one `exit` frame, after the last signal frame, with `wait_for_exit_observed` true and the termination process, and `termination.exit.wait_for_exit_observed` true;
- `cgroup_before` equals `cgroup_after`, each with exact members `boot_id,cgroup_path,directory_identity,oom_kill`, and `boot_id` equal to the termination process's;
- the same-instance checkpoint lies between the first and second signal frames, and the cross-restart checkpoint between the third and fourth.

R6. WHEN the authenticated journal holds a `native_error` frame with `is_fatal` true, THEN the reasons include `b10_restart_fatal_captured`. WHEN it holds one with `is_error` true and `is_fatal` false, THEN they include `b10_restart_native_error_captured`. This follows ADR-021 "Permitted conditions" (docs/blueprint.md line 681: "error-level native callback ... still invalidate the repeat") and decision 6761524200 point 2. Both may be present. `native_errors.count` is the number of `native_error` frames and `fatal_count` the number with `is_fatal` true. `native_errors` is present whenever stage 2 passed, including when the record join later fails; it is `null` only when stage 2 failed. Neither reason blocks the comparison.

R7. WHEN `PhaseWireCodec.ReadRegistration`, `PhaseWireCodec.ReadProducerCompletion`, `ArtifactIndexReader.Read`, `ManifestReader.Read`, `B10SweepEvidenceCodec.ReadFinalization` or `PhaseCandidateCodec.Read` meets a `nonfinal_result` written by the handler, THEN each rejects it as `B10AbruptRejectionTests` asserts for the other abrupt kinds. `B10AbruptRejectionTests` passes unchanged.

R8. WHEN the window consumer reads a record whose decoded key is one of the repeat's two probe keys, THEN it records the lowercase SHA-256 of the value bytes. A `null` value adds a structural problem with code `null_value` and field `value`. Records of other keys, and every existing caller of the existing `Drain` signature, are unchanged in behaviour and in the `ConsumedRecord` values they produce.

R9. Per arm, after stage 2 passes:
- `accepted_originals` is the count of the arm's five originals (R4). `checkpoint_unconfirmed` is the length of its checkpoint's `selected_attempt_ids`.
- `selected_attempt_id` is, for the same instance, the first of the control checkpoint's `selected_attempt_ids`, and for the cross restart, the kill selection entry. `selected_sequence` is its `prepared` sequence.
- `originals_without_report` counts originals with no `terminal` frame anywhere in the journal. `unreported_seen` counts those consumed at least once; `unreported_unseen` counts the rest. Their sum equals `originals_without_report` (Q6).
- `originals_seen` counts originals consumed at least once. `selected_original_seen` is 1 when the selected original was consumed, else 0.
- The resend is, for the same instance, the one journal `prepared` frame with cohort `same_instance` and a non-null predecessor, and for the cross restart, the successor's single invocation. `resend_invoked` is 1. `resend_terminal` comes from the journal `terminal` frame (same instance) or the successor ledger row (cross restart), or is `null` when none exists.
- `resend_seen` is 1 when at least one consumed record joins the resend, else 0. It never exceeds `resend_invoked`.
- `pair_count`: when the resend names the selected original as predecessor, the number of distinct `(partition, offset)` of the selected original times that of the resend; otherwise 0. Copies of one attempt are never a pair (Q12).
- `witness`: when `pair_count` is at least 1, the lexicographically smallest `(original coordinate, resend coordinate)`; otherwise `null`.
- `lost_resend` is true when `resend_terminal.delivered` is true and `resend_seen` is 0, and false otherwise.
- Absence:
  - When stage 2 fails, or the record join adds reason 3 (R13), every arm member except `arm` is `null`.
  - When the window is incomplete, `originals_seen`, `unreported_seen`, `unreported_unseen`, `selected_original_seen`, `resend_seen`, `pair_count`, `witness` and `lost_resend` are `null`.
  - When the arm's resend is unknown, `resend_attempt_id`, `resend_invoked`, `resend_terminal`, `resend_seen`, `pair_count`, `witness` and `lost_resend` are `null`. For the same instance, unknown means other than one control resend. For the cross restart, it means stage 6 added reason 9 or 10. Reason 11 alone keeps them present.
  - Unknown is never zero.

R10. WHEN no comparison-blocking reason (section 4.4) is present, THEN `comparison` is `both` when both arms have `pair_count` at least 1, `cross_restart_only` or `same_instance_only` when only that arm has, and `neither` otherwise. WHEN one is present, THEN `comparison` is `unavailable`.

R11. Timeout cohort. WHEN any check below fails, THEN the reason is `b10_restart_timeout_bookkeeping_unavailable` and `timeout` is `null`. In order:
- the timeout seal has cohort `timeout`, and `drain_complete` and `guard_complete` true;
- the manifest, read by the unchanged `ManifestReader.Read`, is `bookkeeping_complete` with `worker_status` `complete`, `attempt_count` equal to its inventory length, and the seal's `count` equal to `attempt_count`;
- the ledger, read by the unchanged `LedgerReader.ReadRows`, has exactly one row per inventory attempt with equal instance, key and sequence, and every row's instance is the first instance;
- every non-delivered row satisfies `B10RepeatChecks.IsPermittedTimeout`.

Otherwise `timeout` holds the six counts from `OutcomeClassifier.Classify` over those rows and the window's records with non-probe keys. Probe records never enter these counts. `timeout` is also `null` when the window is incomplete or the record join added reason 3.

R12. Control cohort. In order:
- the control seal has cohort `same_instance`, and `drain_complete` and `guard_complete` true;
- `control-manifest.json` has the first instance, cohort `same_instance`, `bookkeeping_complete` true, and `inventory_count` equal to its inventory length and to the seal's `count`;
- its inventory lists the journal's `same_instance` prepared attempts in `invocation_ordinal` order, and each entry's seven members (`attempt_id`, `invocation_ordinal`, `predecessor_attempt_id`, `key`, `sequence`, `payload_length`, `payload_sha256`) equal the same members of that attempt's `prepared` data;
- `control-ledger.jsonl` has exactly one row per inventory attempt, and each row equals that attempt's journal `terminal` data;
- the journal holds exactly one control resend;
- that resend names the first of the control checkpoint's `selected_attempt_ids`, with equal key, sequence and payload hash.

WHEN the fifth check fails, THEN the reason is `b10_restart_resend_count_invalid`. WHEN any other fails, THEN it is `b10_abrupt_control_incomplete`.

R13. Record join, when stages 2 and 7 pass:
- A probe-key record with no structural problem joins `(instance_id, attempt_id)` to a first-instance `prepared` attempt, or to the successor inventory entry, with equal key, sequence and value hash.
- A non-probe record joins the timeout inventory with equal key and sequence.
- A record with a structural problem, an unknown instance or attempt, or a mismatch gives `b10_restart_identity_unauthenticated`.
- Exception: when the successor directory was identified (R2) and stage 6 added reason 9 or 10, structurally valid records carrying that directory's instance ID are held out of the join and add no reason. When no successor directory was identified, no instance ID is held out.

R14. Successor. Each WHEN below gives `b10_restart_successor_evidence_unavailable` unless it names another reason. The checks run in order, and the first that fails gives the stage's only reason:
- no successor directory exists;
- a member is missing, over its limit (section 4.2), empty other than `request-lifecycle.jsonl`, or the members exceed 1073741824 bytes in total;
- the registration, receipt or inventory envelope fails: identity the successor Job, the successor pod directory, predecessor the first instance; one `pod_uid`, different from the first's; pod name different from the first's;
- the receipt does not bind `registration.json` and, in this order, `ledger.jsonl`, `manifest.json`, `statistics.jsonl`, `request-lifecycle.jsonl` and `capabilities.json`;
- its `first_termination` or `selection` descriptors differ from the first-instance bytes;
- `release_utc` differs from the kill checkpoint's, `cross_host_timing` is not `not_verified`, or `cleanup_completed` is not true;
- `capabilities.json` has `statistics_completion.is_complete` not true, `guard_observations_complete` not true or `is_faulted` not false;
- the receipt holds other than one invocation, giving `b10_restart_resend_count_invalid`;
- the invocation does not keep the selection entry's key, sequence and payload hash with a new attempt ID and that entry as predecessor, or its `invocation_utc` precedes `release_utc`;
- the inventory is not one entry equal to the invocation;
- the ledger is not one row for it;
- `ready_utc` is later than `release_utc`, giving `b10_abrupt_successor_late`.

R15. WHEN the end-line read or the drain fails, or the safety stop expires before every partition reaches its end offset, THEN the reason is `b10_restart_window_incomplete`.

R16. WHEN the handler finishes reading, THEN it writes exactly one `nonfinal_result` (section 4.3), create-only, reads it back and exits 0. WHEN the write or the read-back fails, THEN it exits nonzero and writes nothing else.

## 6. Invariants and safety

- Verdict integrity (AGENTS.md GOVERNANCE): nothing here edits, proposes values for or reads live results into `expected.yaml` or `results/`. The nonfinal result is never a published result (factory.md "Evidence pack").
- Hard safety rules: no Azure credential, no apply or destroy, no workflow dispatch, no trial. No B10 trial may run until the two race fixes of [6761783572](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761783572) merge, and any trial needs Hari's explicit approval and environment-gate approval.
- Missing evidence is `null`, never zero; unknown never becomes a persistence status (ADR-021 protocol rules 13 and 14, decision rule 5).
- Ordinary scenarios: every existing mode, reader and output is unchanged (section 3.2, R7, R8).
- No change to any protocol sequence, gate, policy value, verdict rule, timeout, existing failure name or design text beyond the decided literals of sections 4.3 and 4.4.

## 7. Never-ran inventory

Evidence is the trial artifacts. Runs and artifacts, all on `main`, all concluded `failure`, all downloaded with `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/<id>/zip` into new empty scratch directories. Zip SHA-256 values for trials 1 to 7 match the table in `items/404/plan-verifier.md` section 7 (trial 7: `bee17231882ffeb7ddc3134ba10a00ec4590f20e96dad409c050decf1f22cdcf`). Trial 8: run 42771287992 on `aadb0c6d`, artifact 13742900343, zip `2c516f2485cf1fa250494040ba3298daf43c6e91b9fb7b2e546a9471dab49e34`. Trial 9: run 42773789291 on `aadb0c6d`, artifact 13742914191, zip `ff855570ab036fc85c1891b3a555e0f83c89197b34c59bd30dd1b2089fbf9097`. Recorded modes: G1.

Upstream stages this plan reads:

| Stage | Ran live | Evidence | Offline test that runs it for real |
|---|---|---|---|
| U1 Verifier `start-line` for B10 | Yes, 8 of 9 | trials 1 to 8 `phase-status.json`: `start-line complete`; trial 9: `start-line failed`, `b10_abrupt_publication_failed` at boundary `metadata` | T7 runs the real `StartLineHandler` |
| U2 First-instance 7 pre-PR3a members | Yes, trial 7 | 7 objects in artifact 13704155185 | T2 on those bytes |
| U3 PR3a cohort-file publication and `cohort_files` | No | trial 8 failed at `first_termination` before publication; G2 | T1, T4 on the real export path |
| U4 PR3a `native_error` frames and counts | No | G2; trial 7 seals lack `native_error_count`; trial 8 published no journal | T6 on the real export path with native errors |
| U5 Successor publication of 7 members | No | trial 7: `b10_abrupt_publication_ambiguous` at `successor_publication`; trials 8 and 9 started no successor | T5 on the real export path (real statistics chain) |

Stages this pull request adds:

| Stage | Ran live | Test that runs it for real |
|---|---|---|
| V1 Listing and download of the abrupt prefix by the verifier identity | No | None offline (Azure Blob). Deployment assumption DA1; T1 runs the real handler over an in-memory store holding the real export bytes |
| V2 First-instance envelope, identity, binding | No | T1, T2, T3 |
| V3 Journal reconciliation and checkpoint recomputation | No | T3 |
| V4 Termination chain | No | T3 |
| V5 Timeout cohort through ordinary readers | No | T4 |
| V6 Control cohort through the abrupt inventory and row readers | No | T4 |
| V7 Native error reasons | No | T6 |
| V8 Successor reader and receipt binding | No | T5 |
| V9 End-line read and window drain in this mode | No (the ordinary `verify` drain never ran in a B10 trial) | T7 on Testcontainers Kafka |
| V10 Probe value hash | No | T7 |
| V11 Record join | No | T7 |
| V12 Per-arm classification, pairs, witness, loss, comparison | No | T7 |
| V13 Eligibility reasons and `nonfinal_result` write and read-back | No | T7, T8 |
| V14 Host mode registration and option validation | No | T9 |

## 8. Test map

All new classes are named `B10AbruptVerify*`, so command A4 selects them.

Inputs:
- "Export" means the 22 files produced in-test by running the LoadGen test assembly as `B10AbruptRejectionTests.ProduceExportAsync` does (`B10AbruptRejectionTests.cs` lines 89 to 116), into a fresh directory. "Native export" is the same with `PARCHWELL_B10_ABRUPT_WIRE_NATIVE_ERRORS=1`.
- Export identities: run `synthetic`, first pod `synthetic-first`, successor pod `synthetic-second`, source `a` x 40, image `registry.invalid/loadgen@sha256:` + `b` x 64.
- "Rebound" means a semantic edit followed by recomputing every dependent descriptor, so only the predicate under test is wrong. The dependent descriptors are the kill selection's embedded `cross_restart` checkpoint and `checkpoint_descriptor`, the manifest, the termination receipt and the successor receipt. For a cohort file they also include its seal and `cohort_files` entry.

Unless a row says otherwise, the handler runs with the export in its ledger store, the real `StartLineHandler` output in its verifier store, and a complete window of record set A (T7).

- T1 (R2, R16). Export, window A. Expected: exit 0; `eligibility` `pending_sweep`; `eligibility_reasons` `[]`; `first_instance_id` `synthetic-first`; `successor_instance_id` `synthetic-second`; `finality` `nonfinal`. Boundaries:
  - each of the 15 first members removed in turn gives `["b10_restart_first_evidence_unavailable"]`, as do an empty `journal.jsonl` and a `journal.jsonl` of 16777217 bytes;
  - an extra object `synthetic-first/extra.json`, a directory `synthetic-x/` holding only `ledger.jsonl`, and a second directory whose registration claims the first Job each give `["b10_restart_identity_unauthenticated"]`.
- T2 (R2, R3, G7). Trial 7 fixtures: the 7 first-instance objects and the successor `registration.json` of artifact 13704155185, byte-identical, with SHA-256 pinned in the test:

  | File | SHA-256 |
  |---|---|
  | `abrupt-manifest.json` | `3a25132454c41c9cbc70950f9ae5e80f9974ee48b9c03fc73b29aad098a9747a` |
  | `control-seal.json` | `31537beac65841212c34c74311aa69e608167f4b98915f0983f2d47de029e8da` |
  | `journal.jsonl` | `6fa8d06ecf5170d446e490733338b811aac94c6d1dd9103b28d8c2f69582d77f` |
  | `kill-selection.json` | `0f1bff4a0581e2af2775666c06d0f21fb4f59abdc4405845153bbfcda2a90059` |
  | `registration.json` | `8407008cd88b92f734b21c03385e54a48786478999b810da34c96990c3721454` |
  | `termination.json` | `eba48bdf255faa4a7db86a6de492de91d6c70740844897749ac57a97a77af9a9` |
  | `timeout-seal.json` | `97bd832ef06681b1dcdcbd7a70bd13a9d4390bd803e9481d3a605b48faf36a7f` |
  | successor `registration.json` | `40656c1d1105dd00d4a2b0cb5d25e89a769b47cf19565acd99e9b1bceb735d1a` |

  Options come from the fixture's registration identity. Expected with an empty complete window: reasons exactly `["b10_restart_first_evidence_unavailable"]`, `native_errors` `null`. With 8 placeholder cohort files of 1 byte each: `["b10_restart_identity_unauthenticated"]`. The test also asserts that no fixture contains any of `AZURE_`, `tenant`, `subscription`, `AccountKey`, `SharedAccessSignature`, `sig=`, `Bearer `, `password` or `secret` (case-insensitive). The scratch scan found none of these in the fixtures. It found the tenant and client ID values in the artifact's `pods.json` files and container logs, so those files are never fixtures.
- T3 (R3, R4, R5). Export variants, each giving exactly one reason, with every arm member except `arm` `null`.
  - Not rebound: one byte of `journal.jsonl` changed gives 3.
  - Rebound, giving 3: `registration.json` `source_commit` changed; `kill-selection.json` `schema_version` 1; `termination.json` `scientific_admissible` true; an unknown member in `control-seal.json` `data`; journal frame 20 removed; journal frame 15 renumbered 115; the control checkpoint's first `selected_attempt_ids` member dropped; a `gap` frame appended; `timeout-seal.json` `post_drain_statistics.sha256` set to 64 zeros; the `probe_id` of journal frame 1 set to `other`.
  - Rebound, giving 4: `mechanical_status` `incomplete`; the signal list reversed; `cgroup_after.oom_kill` 1; the last signal's `intent_sequence` plus 1, in both the journal and the receipt; the last signal and exit frames swapped and renumbered.
- T4 (R11, R12). Export variants.
  - Window A: `timeout` `{ok 1, loss 0, phantom 0, reported_failure 0, duplicate 0, reorder 0}`.
  - Timeout seal `count` 2 (rebound): `["b10_restart_timeout_bookkeeping_unavailable"]`, `timeout` `null`, `comparison` `both`.
  - Timeout row rebound to `delivered false, error_code -1, persistence_status 0`: the same reason.
  - The same row with `error_code -192`: no reason; `timeout` `{0, 0, 1, 0, 0, 0}`.
  - Each of these gives `["b10_abrupt_control_incomplete"]`, `comparison` `unavailable`, with arm counts still present: control seal `drain_complete` false (rebound); the last control ledger row dropped (rebound); the first control ledger row's `persistence_status` set to 1 (rebound); control manifest `inventory_count` 7 with seal `count` 7 (rebound).
- T5 (R13, R14). Export successor variants, rebound.

  | Variant | Reasons | Cross-restart resend members | Same-instance `pair_count` | Comparison |
  |---|---|---|---|---|
  | `ready_utc` after `release_utc` | `["b10_abrupt_successor_late"]` | present, `pair_count` 1 | 1 | `unavailable` |
  | invocation `attempt_id` equal to its predecessor | `["b10_restart_successor_evidence_unavailable"]` | `null` | 1 | `unavailable` |
  | two invocations | `["b10_restart_resend_count_invalid"]` | `null` | 1 | `unavailable` |
  | `cleanup_completed` false | `["b10_restart_successor_evidence_unavailable"]` | `null` | 1 | `unavailable` |
  | the first pod's `pod_uid` in all successor envelopes | the same | `null` | 1 | `unavailable` |
  | `statistics_completion.is_complete` false | the same | `null` | 1 | `unavailable` |
  | `is_faulted` true | the same | `null` | 1 | `unavailable` |
  | `guard_observations_complete` false | the same | `null` | 1 | `unavailable` |
  | `cross_host_timing` `verified` | the same | `null` | 1 | `unavailable` |
  | receipt `release_utc` 100 ns earlier | the same | `null` | 1 | `unavailable` |
  | `invocation_utc` `2026-01-01T00:00:00.0000000+00:00` | the same | `null` | 1 | `unavailable` |
  | `capabilities.json` padded to 65537 bytes | the same | `null` | 1 | `unavailable` |

  `capabilities.json` padded to exactly 65536 bytes gives no reason and `comparison` `both`. In the variants above, window A's successor record adds no reason (R13 hold-out). With no successor directory: window A gives `["b10_restart_identity_unauthenticated", "b10_restart_successor_evidence_unavailable"]` with every arm member `null`; A without the successor record gives `["b10_restart_successor_evidence_unavailable"]` with cross-restart resend members `null` and same-instance `pair_count` 1.
- T6 (R6). Native export, window A. Expected: reasons `["b10_restart_native_error_captured", "b10_restart_fatal_captured"]`; `native_errors` `{count 3, fatal_count 1, tail "unknown_due_to_intended_kill"}`; `comparison` `both`. The native export's journal holds `native_error` frames at journal sequences 1, 24 and 39, with `capture_sequence` 1, 2 and 3 and `is_fatal` false, true and false (scratch run).
- T7 (R1, R8 to R10, R13, R15). Component test on the existing `LocalKafkaFixture` (`confluentinc/cp-kafka:7.5.12@sha256:997883c60e6f1207fb5f779c7a8fdae57312a17f446fd5ea3eb8b73f52d4a1cf`), topic `document-events` with 1 partition. The real `StartLineHandler` writes the start line. A real Confluent.Kafka producer writes record set A, in this order:
  1. the timeout attempt `synthetic-first-timeout-0`: key `tenant-d0`, sequence 0, the three timeout headers;
  2. each export `prepared` attempt in journal order, with its key, sequence, headers and the selection payload;
  3. the successor invocation `synthetic-second-replay-0`.

  Then the real handler runs. Observed on the prototype (section 8.1):

  | Case (records) | Reasons | Comparison | Same instance | Cross restart |
  |---|---|---|---|---|
  | a: A | none, `pending_sweep` | `both` | selected `synthetic-first-same_instance-0`, `originals_without_report` 0, unreported 0/0, `resend_seen` 1, `pair_count` 1, witness offsets (1, 6), `lost_resend` false | selected `synthetic-first-cross_restart-6`, `originals_without_report` 5, unreported 5/0, `resend_seen` 1, `pair_count` 1, witness offsets (7, 12), `lost_resend` false |
  | b: A without the successor record | none | `same_instance_only` | as a | `resend_seen` 0, `pair_count` 0, witness `null`, `lost_resend` true |
  | c: A without the five kill originals | none | `same_instance_only` | as a | unreported 0/5, `pair_count` 0 |
  | c2: A without `cross_restart-6` | none | `same_instance_only` | as a | unreported 4/1, `selected_original_seen` 0, `pair_count` 0 |
  | d: A without the control resend | none | `cross_restart_only` | `resend_seen` 0, `pair_count` 0, `lost_resend` true | as a |
  | e: empty window | none | `neither` | `lost_resend` true | unreported 0/5, `lost_resend` true; `timeout` `{0,1,0,0,0,0}` |
  | f: A plus a second copy of `cross_restart-6` | none | `both` | as a | `pair_count` 2 |
  | f2: A plus a second copy of the successor record | none | `both` | as a | `resend_seen` 1, `pair_count` 2 |
  | g: case b plus a second copy of `cross_restart-6` | none | `same_instance_only` | as a | `pair_count` 0 |
  | h: case b plus the successor record with value byte 0 flipped | `b10_restart_identity_unauthenticated` | `unavailable` | all `null` | all `null`; `timeout` `null`; `native_errors` `{0, 0, "unknown_due_to_intended_kill"}` |
  | i: A plus a probe record with no `attempt-id` header | as h | as h | as h | as h |
  | j: A plus `cross_restart-7` with a null value | as h | as h | as h | as h |
  | k: A plus a copy of the successor record with instance `synthetic-third` | as h | as h | as h | as h |
  | l: A without the timeout record | none | `both` | as a | as a; `timeout` `{0,1,0,0,0,0}` |
  | m: A plus a timeout-key record for unknown attempt `synthetic-first-timeout-9` | as h | as h | as h | as h |
  | n: safety stop 1 ms before any record is read | `b10_restart_window_incomplete` | `unavailable` | seen family `null` | seen family `null` |
  | cfg: start-line note with a third expected job name | `b10_restart_configuration_invalid` only | `unavailable` | all `null` | all `null` |

  The same test asserts R8 directly at the drain:
  - every probe record in A carries value hash `87b7f8decf017e82301b20dec010123c468144e3bb521b88eb0d755d49be288d` (the export payload, 4 bytes, base64 `L2P4Dw==`);
  - the timeout record carries none;
  - case j's record carries problem `null_value` on field `value`;
  - case i's record carries `missing_header` on `attempt-id`.

  Case cfg is specified, not prototyped. Its note bytes must be written by hand in the test, because `StartLineNoteCodec.Write` rejects a third job name (lines 45 to 48).
- T8 (R7, R16). The `nonfinal_result` written in T7 case a is read back byte-identical by the new reader. One unknown member, one missing member, a count of 1.5, a count of -1 and `eligibility` `"eligible"` are each rejected. Each of the six scientific readers of R7 rejects the written bytes; on `main` they already reject every abrupt envelope, so this is a regression guard, not a behaviour change. A store whose create throws makes the handler exit nonzero.
- T9 (section 4.1, R16). `Program.BuildHost` with the configuration of section 4.1 resolves the `b10-abrupt-verify` handler. Each of `ScenarioId` `A01`, `Phase` `C0`, `Repeat` 2 and a missing `LoadGenImage` fails validation. Mode `verify` still resolves `VerifyRunHandler`.
- T10 (R8, existing callers). `WindowConsumerTests` and `MessageIdentityReaderTests` pass unchanged. A new test drains a record with a probe key through the existing `WindowConsumer.Drain` signature and asserts it carries no value hash. This test compiles only after this pull request adds the value-hash member.

Must keep passing unchanged: `B10AbruptRejectionTests` (2 tests), the Component classes (13 tests on `main`), the full Verifier suite, `test_b10_abrupt.py` and `test_c5_workflow.py`.

Mutation-only build: every new test file sits under `Component/`, which the existing exclusion removes when `ParchwellVerifierMutationOnly=true` (`Parchwell.Verifier.Tests.csproj` lines 20 to 22); the csproj is unchanged. A5's class filter `*Component*` matches class names, not folders, so its total stays 13.

### 8.1 Prototype

A scratch prototype, never committed, pinned every value above:
- A Python reader, reconciliation and classification (`proto.py`, SHA-256 `6547bbab1e0ec1436eba269d4964e10ecaf3e61f90e6c90b10dd1778623f90fd`), its case runner (`cases.py`, `f5d4ad28c44b3500f9b226d1d8d70292196ffa2ac3b302b3c97e64cad0ea61bd`) and its mutation runner (`mutate.py`, `5506a3faefdaa5aca88a96cdeb0edabe3dce7a853f5bb7fe4d8d86d8a6300189`). They ran on the export, the native export and the trial 7 bytes. The Revision 2 run printed 114 lines for 57 cases.
- A scratch xunit class in the Verifier test project (`ScratchAbruptProtoTests.cs`, `d8b97decef2729b63fb079b5d4117956d38ba1a1a77051d643d5b43826dba9d5`), with the value-hash overload of Q5 option B applied to the three files of section 3.1 item 3:
  - it started `LocalKafkaFixture`, ran the real `StartLineHandler` for run `synthetic`, and produced record set A and the deviation records with a real producer;
  - it built the window with the real `RunWindow.Create` and drained it with the real `WindowConsumer.Drain`;
  - window A held 13 records at offsets 0 to 12, and checks 9 and 10 passed;
  - the Python classifier consumed its output. Cases f2 and k reuse a consumed record with a changed offset or instance, offline; T7 writes them for real.

### 8.2 Mutation table

Each mutation was applied to the prototype and the cases rerun. "Caught by" names the planned test whose literal expectation changed.

| Requirement | Mutation | Caught by |
|---|---|---|
| R8, R13 | value hash not compared at the join | T7 h |
| R9 | copies of the selected original counted as a pair without a resend | T7 g |
| R9 | unreported counts include originals that have a report | T7 a (same instance 5/0 instead of 0/0) |
| R9 | control selects the last unconfirmed member | T7 a (selected ID, witness) |
| R15 | window completeness ignored | T7 n |
| R6 | fatal frames ignored | T6 |
| R6 | error-level non-fatal frames ignored | T6 |
| R3 | cohort descriptor not compared with the seal | T3 (seal `post_drain_statistics` zeros) |
| R12 | control ledger not reconciled with journal terminals | T4 (row `persistence_status` 1) |
| R11 | permitted-timeout rule loosened | T4 (`error_code -1`) |
| R14 | successor readiness ignored | T5 (`ready_utc`) |
| R10 | control incomplete not comparison-blocking | T4 (control `drain_complete`) |
| R4 | checkpoint selection trusted instead of recomputed | T3 (control selection) |
| R3 | journal sequence check removed | T3 (frame 15 renumbered) |
| R13 | unknown instance records ignored | T7 k |
| R14 | successor pod UID reuse accepted | T5 (pod UID) |
| R13 | unknown timeout attempt accepted | T7 m |
| R14 | successor statistics completion ignored | T5 (`is_complete`) |
| R9 | counts written as 0 instead of absent | T7 h, i, j |
| R14 | `guard_observations_complete` and `is_faulted` ignored | T5 (`is_faulted`, `guard_observations_complete`) |
| R14 | `cross_host_timing` check removed | T5 (`verified`) |
| R14 | receipt `release_utc` not compared with the kill checkpoint | T5 (100 ns earlier) |
| R5 | cgroup before equal to after not enforced | T3 (`oom_kill` 1) |
| R14 | successor read limit ignored | T5 (65537 bytes) |
| R3 | probe key and `probe_id` rule removed | T3 (`probe_id` `other`) |
| R12 | `inventory_count` against length removed | T4 (`inventory_count` 7) |
| R14 | `invocation_utc` before `release_utc` accepted | T5 (`invocation_utc`) |
| R5 | kill intent check removed | T3 (`intent_sequence` plus 1) |
| R5 | exit after the last signal not enforced | T3 (frames swapped) |
| R13 | successor hold-out applied with no successor directory | T5 (no directory, window A) |

Equivalent mutant, recorded: counting a pair when the resend does not name the selected original survived. R12 and R14 already reject such a resend before classification. The predicate stays as defence in depth; no reachable input distinguishes it.

## 9. Deployment assumptions

- DA1. The verifier Job's workload identity can list and read `<RunId>/C1/r1/b10-abrupt-diagnostic/` in the `ledger` container. Accepted by name in [6761524200](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761524200) point 4. Basis:
  - `infra/ephemeral/identity.tf` line 13 assigns `Storage Blob Data Reader` to the verifier at scope `module.storage.containers.ledger.id`, with no condition, the same assignment the ordinary `verify` mode lists through.
  - Microsoft: "An individual container. At this scope, a role assignment applies to all of the blobs in the container, and to the container properties and metadata." ([Authorize access to blobs by using Microsoft Entra ID](https://learn.microsoft.com/azure/storage/blobs/authorize-access-azure-active-directory#resource-scope), "Resource scope", read 2026-11-10).
  - Microsoft: Storage Blob Data Reader: "Read and list Blob storage containers and blobs." ([Access control model in Azure Data Lake Storage](https://learn.microsoft.com/azure/storage/blobs/data-lake-storage-access-control-model#role-based-access-control-azure-rbac), read 2026-11-10).
  - No offline test covers the live listing. Failure gives `b10_restart_first_evidence_unavailable`, not a wrong result.
- DA2. A live trial publishes the 22 files in the formats the export produces. PR3a's own deployment assumptions, accepted in [6750940329](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750940329) and [6751703425](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6751703425), cover the publication; this plan adds none. Neither trial 8 nor trial 9 published any of these files (G2), so the assumption remains unobserved live.
- The scope record's DA2 (a verifier Job in the trial namespace) belongs to PR3c, and its DA3 (native fatal delivery) to PR3a; neither is assumed here.

## 10. Acceptance commands

Run in this session on a clean worktree of `main` at `aadb0c6d`, .NET SDK 10.0.402, with a local Docker daemon (A2 and A5 need it). Observed output on `main` is quoted; the expected output after this pull request follows it.

- A0 build: `dotnet restore Parchwell.slnx --locked-mode && dotnet build Parchwell.slnx --configuration Release --no-restore`. Observed: `Build succeeded.` `0 Warning(s)` `0 Error(s)`. After: the same.
- A1 export: `PARCHWELL_B10_ABRUPT_WIRE_OUTPUT=<new empty directory> dotnet tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptWireProducerTests`. Observed: `Total: 8, Errors: 0, Failed: 0, Skipped: 0`; the directory holds `export.json` and 22 files. After: the same. With `PARCHWELL_B10_ABRUPT_WIRE_NATIVE_ERRORS=1` also set, the journal holds three `native_error` frames (observed with the scratch switch).
- A2 full Verifier suite: `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --no-build`. Observed: `Test run summary: Passed!` `total: 7389` `failed: 0` `succeeded: 7389` `skipped: 0`. After: exit 0, `failed: 0`, `skipped: 0`, total equal to the `main` total plus the new tests.
- A3 abrupt classes: `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --no-build -- --filter-class "*B10Abrupt*"`. Observed: `total: 2` `failed: 0` `succeeded: 2` `skipped: 0`. After: exit 0, `failed: 0`, total 2 plus the A4 total.
- A4 new tests: `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --no-build -- --filter-class "*B10AbruptVerify*"`. Observed: `succeeded: 0` and `Test run completed with non-success exit code: 8`, the testing platform's code for zero tests run. After: exit 0, `failed: 0`, `skipped: 0`, every test of T1 to T10 run.
- A5 component tests: `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --no-build -- --filter-class "*Component*"`. Observed: `total: 13` `failed: 0` `succeeded: 13` `skipped: 0`. After: exit 0, the same total. A run with no Docker daemon fails and is not a pass.
- A6 phase script: `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_b10_abrupt.py'`. Observed: `Ran 12 tests` `OK`. After: the same.
- A7 C5 workflow: `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_workflow.py'`. Observed: `Ran 107 tests` `OK`. After: the same.
- A8 repository check: `./scripts/verify-dotnet.sh`, then `npm ci --ignore-scripts && npm run check:docs` and `git diff --check`, under docs/agents/pull-requests.md rule 8. Not run for a plan file; the implementer records them.

## 11. Plan-check questions (factory.md "Item lifecycle" step 1)

(a) Runtime-sized effort: none in the tests. The export is 22 files, the record sets are fixed by section 8, and the trial 7 fixture set is 8 files. The item plan's disposition caps apply per pull request (contract "Policy" table): 20 documentation-warning, 20 verification-failure and 20 review-finding root causes, exhaustion `push -wip and hand off to the next session`. There is no C5 cut computation (G8, section 3.2).

(b) Stricter than the parent:
- The never-ran inventory and the real-input tests come from decision 6749565231.
- R12's comparison of control ledger rows with journal terminals, and R14's capability checks, go beyond what `b10_abrupt.py` checks, under ADR-021 protocol rules 12 and 14 ("conflicting terminals ... reject authoritative evidence").
- R6 makes a journaled error-level callback ineligible, which PR3a's first instance does not gate (PR3a recorded point). That follows ADR-021 "Permitted conditions" and decision 6761524200 point 2.

(c) Code or data neither on `main` nor in this pull request: the live publication (DA2, covered by PR3a's accepted assumptions) and the live blob listing (DA1, accepted by name). The capture-loss, partial-frame and storage readiness fixes are not dependencies; section 3.3 and the stop rule cover them.

## 12. Sources

- docs/blueprint.md ADR-012 and ADR-021 at `aadb0c6d`.
- docs/operations/b10-abrupt-contract.md and docs/operations/verifier-contract.md at `aadb0c6d`.
- docs/agents/factory.md "Item lifecycle" and "Authority reads"; docs/agents/pull-requests.md.
- Code at `aadb0c6d`:
  - `infra/scripts/phase/b10_abrupt.py`;
  - `src/Parchwell.Verifier/Program.cs`, `VerifierHostOptions.cs`, `VerifierHostOptionsValidator.cs`, `VerifyRun/VerifyRunHandler.cs`, `VerifyRun/Window/*`, `VerifyRun/Input/ConsumedRecord.cs`, `ManifestReader.cs`, `LedgerReader.cs`, `Judgement/OutcomeClassifier.cs`, `B10RepeatChecks.cs`, `Common/Phase/PhasePaths.cs`, `PhaseIdentity.cs`, `Common/Window/StartLineNoteCodec.cs`;
  - `tests/Parchwell.Verifier.Tests/Host/B10AbruptRejectionTests.cs`, `Component/ComponentRunHarness.cs`, `Component/LocalKafkaFixture.cs`;
  - `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptWireProducerTests.cs`, `B10AbruptSuccessorCompletionTests.cs`;
  - `infra/ephemeral/identity.tf`; `.github/CODEOWNERS`.
- Trial artifacts 13609836611, 13615377394, 13629179730, 13639442741, 13668809132, 13699980854, 13704155185, 13742900343 and 13742914191.
- [Confluent.Kafka PersistenceStatus](https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.PersistenceStatus.html); [Authorize access to blobs by using Microsoft Entra ID](https://learn.microsoft.com/azure/storage/blobs/authorize-access-azure-active-directory#resource-scope); [Access control model in Azure Data Lake Storage](https://learn.microsoft.com/azure/storage/blobs/data-lake-storage-access-control-model#role-based-access-control-azure-rbac).

## 13. Open questions for Hari

- Q13. Does ADR-021 protocol rule 13 ("Publish only checked fields consumed by these rules") bind the nonfinal result? Section 4.3 includes fields the rule does not name: `accepted_originals`, `checkpoint_unconfirmed`, `selected_sequence`, `originals_without_report`, `resend_terminal`, `window_complete`, `start_line` and `end_line`.
  - Options: A keep them; the nonfinal result is a diagnostic object, never a publication (factory.md "Evidence pack"), and each field is checked. B keep only fields rule 13 names, and drop the eight. C keep them, and require the later final sweep projection to drop them.
  - Recommendation: C. Each of the eight supports a check the rules require. `accepted_originals` and `checkpoint_unconfirmed` show exposure (decision rules 2 and 5). `originals_without_report` is the bound in protocol rule 14. `resend_terminal` is decision rule 4's "failed resends keep their terminal classifications". The window fields show that the window was complete.
  - Strongest counterargument: rule 13 says "Publish only", and a later slice could copy the nonfinal object into published evidence unchanged. B removes that risk now, at the cost of a diagnostic that cannot be cross-checked by eye.

### 13.1 Decided questions (Revision 1 Q1 to Q12)

| Revision 1 question | Answer | Source |
|---|---|---|
| Q1 implementing tool | Codex | [6761524200](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761524200) point 1 |
| Q2 reason literals | the 12 of section 4.4 | [6761542603](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761542603) |
| Q3 eligibility literal | `pending_sweep` or `ineligible`, no sweep reason | 6761542603 |
| Q4 DA1 | accepted by name | 6761524200 point 4 |
| Q5 value hash | option B, optional key set through a new `Drain` overload | 6761542603 |
| Q6 unreported counts | option A, all originals without a report, plus `selected_original_seen` | 6761542603 |
| Q7 statistics reports | option A, binding only in this slice; guard re-evaluation before scientific admission | 6761524200 point 3 |
| Q8 non-fatal native error | ineligible, `b10_restart_native_error_captured`; also ADR-021 "Permitted conditions", docs/blueprint.md line 681 | 6761524200 point 2 |
| Q9 contract line 71 | correct it in this pull request | 6761542603 |
| Q10 mode, path, exit, identity | option A as section 4.1 and 4.3 state them | 6761542603 |
| Q11 LoadGen env switch | allowed | 6761542603 |
| Q12 pair counting | product of coordinates, smallest witness | 6761542603 |

Revision 2 changes none of these: the reason list, paths, mode name, exit rule, envelope identity, value-hash shape and count definitions are as Revision 1 stated them. Revision 2's additions are precedence, read limits copied from `b10_abrupt.py`, the directory rule of R2 (which uses existing reasons 2 and 3), and test literals.

## 14. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" in any of these cases:
- it needs to change a contract in section 4, a requirement in section 5 or the scope in section 3;
- Q13's answer differs from the plan;
- any format in section 4.2 differs on the branch base from `main` at `aadb0c6d`;
- the capture-loss race fix, the partial-frame race fix, the storage readiness race fix or any other merged change alters the successor `statistics.jsonl` or `capabilities.json` members of section 4.2, any wire format of section 4.2, the export's file set, or any literal in section 8.

It also stops at each disposition cap of section 11 with that cap's exhaustion action.

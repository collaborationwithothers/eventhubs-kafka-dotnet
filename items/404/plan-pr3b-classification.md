# PR3b classification contract-grade plan

Revision 1. Issue 404, pull request short name `PR3b classification`. Status: not posted, not checked, not approved. Implementing tool: open (question Q1).

This pull request adds one diagnostic-only verifier mode, `b10-abrupt-verify`. It reads one B10 C1 abrupt repeat's first-instance and successor objects from the `ledger` container, reconciles them independently, reads that repeat's window of `document-events` back with a value hash for probe records, and writes one `nonfinal_result` object with per-cohort and per-arm counts, a comparison and eligibility reasons. It never selects a verdict, never publishes a result and never makes the abrupt dialect admissible to any scientific reader.

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
- [6761052971](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761052971): "Start planning PR3b now, on `main` at `aadb0c6d`", with a never-ran inventory; "Questions the plan raises go to Hari; this decision does not extend the standing decisions to PR3b. PR3b changes protected verifier paths, so Hari approves its plan and merges it."
- docs/blueprint.md ADR-012, ADR-021 (lines 659 to 730 on `main` at `aadb0c6d`), in particular "Permitted conditions" (line 681), decision rules 2, 3, 5 and 6 (lines 693, 694, 696, 697) and protocol rules 5, 12, 13, 14 and 15 (lines 708, 715 to 718).

Catch-up (this session, read 2026-11-10): 184 comments on two pages. New since the state file: 6758253501 (handoff), 6760104002, 6760120861, 6760139065, 6760170847, 6760180716, 6760201403 (decisions on other pull requests), 6760218433 (handoff), 6760242955 (decision on the successor fix), 6760646338 (capture-loss plan pointer), 6760749353 (its plan check), 6760770654 (its approval), 6761052971 (this plan's decision). Issue body `updated_at` 2026-11-10T06:50:45Z. Each decision named in the planning prompt was read in full and says what the prompt says.

Baseline: `main` at `aadb0c6d5252d6ca9638d116c1bbf7339c17037e`. Merged predecessors: successor fix (PR 421), capture contention race fix (PR 422), PR3a evidence completeness (PR 423).

Scope record used as input, not authority: `items/404/plan-verifier.md` Revision 1 on `claude/404-plan`. Its findings were rechecked here. F1 and F2 are resolved on `main` by PR3a. F3 and F5 remain and stay out of scope (PR3c and later slices). F4 is this plan's R8 and question Q5. F6 does not apply: no C5 target changes (section 3).

## 2. Findings this plan rests on

Each was checked on `main` at `aadb0c6d` or on the trial 7 artifact.

- G1. No verifier mode has run in any diagnostic trial. All seven `b10-abrupt-trial.yml` artifacts were downloaded and their `phase-status.json` read by script: trials 1 to 6 record `start-line complete, loadgen failed`; trial 7 records `start-line complete, loadgen complete, loadgen2 failed`. No other mode appears.
- G2. PR3a's 15 first-instance members and the successor's 7 members have never been published together by a live trial: PR3a merged after trial 7, and trial 7 published 7 first-instance members and only the successor's `registration.json`.
- G3. The real LoadGen export path on `main` produces all 22 files. `B10AbruptWireProducerTests.Given_WireOutputDirectory_When_Run_Then_WritesProducedArtifacts` with `PARCHWELL_B10_ABRUPT_WIRE_OUTPUT` set wrote `export.json` and 22 files. Its successor runs "on the real evidence, adapter and statistics chain with the host's completion behaviour" (`B10AbruptWireProducerTests.cs` lines 368 to 371, the successor fix's `B10AbruptSuccessorCompletionTests.Chain`). Its bytes carry wall-clock timestamps, so two runs differ; tests pin structure and counts, never these hashes.
- G4. The control cohort files are not schema one. `control-manifest.json` is a schema 2 envelope of kind `cohort_inventory`, and `control-ledger.jsonl` rows have exactly the members `attempt_id,completion_source,delivered,error_code,persistence_status`. The ordinary `ManifestReader.Read` and `LedgerReader.ReadRows` reject both (scratch run: `unsupported_schema_version:schema_version` and `missing_property:attempt`). The timeout cohort files are schema one and both ordinary readers accept them (scratch run: success, 1 inventory entry, 1 row). `docs/operations/b10-abrupt-contract.md` line 71 says "The cohort files are schema-one ordinary bytes". That sentence is false for two of the eight files (question Q9).
- G5. The successor's `manifest.json` and `ledger.jsonl` use the same two abrupt formats as the control cohort files, so one reader serves both.
- G6. `MessageIdentityReader.Read` reads the key and the `instance-id`, `attempt-id` and `sequence` headers only; it never reads the value (`src/Parchwell.Verifier/VerifyRun/Window/MessageIdentityReader.cs` lines 13 to 29). ADR-021 decision rule 3 requires "identical logical key/sequence/payload".
- G7. Trial 7's first-instance bytes are the pre-PR3a dialect: its seals have no `native_error_count`, its manifest has no `cohort_files`, and its `termination.json` has `fatal_capture_status` `not_implemented_pr3`. The prototype rejects them with `b10_restart_first_evidence_unavailable` (8 cohort files missing); with placeholder cohort files added, it rejects them with `b10_restart_identity_unauthenticated` at the seal data members.
- G8. Neither `WindowConsumer` nor `MessageIdentityReader` is a C5 mutation target (`tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` has no match for either name).

## 3. Scope

### 3.1 Files allowed to change

1. New code under `src/Parchwell.Verifier/VerifyRun/Abrupt/` (new folder; file split and private names are the implementer's choice): the mode handler, the readers of section 4.2, the reconciliation and classification of section 5, and the `nonfinal_result` codec.
2. `src/Parchwell.Verifier/Program.cs`, `VerifierHostOptions.cs` (`VerifierMode` and `VerifierModeParser`) and `VerifierHostOptionsValidator.cs`: register mode `b10-abrupt-verify` with the options of section 4.1 only.
3. `src/Parchwell.Verifier/VerifyRun/Input/ConsumedRecord.cs`, `VerifyRun/Window/MessageIdentityReader.cs` and `VerifyRun/Window/WindowConsumer.cs`: the optional value hash of R8, only in the shape question Q5 approves.
4. New test files under `tests/Parchwell.Verifier.Tests/Component/`, every class named with prefix `B10AbruptVerify`, and the trial 7 fixture directory `tests/Parchwell.Verifier.Tests/Component/Fixtures/b10-abrupt-trial-42679791413/` (section 8, T2). They go under `Component/` because the mutation-only build already removes `Component/**/*.cs` (`Parchwell.Verifier.Tests.csproj` lines 20 to 22), and these tests run the LoadGen test assembly or Docker; a new folder would need a csproj change, which section 3.2 forbids.
5. `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptWireProducerTests.cs`: only the environment switch of question Q11, so the export can also produce PR3a's native-error variant.
6. `docs/operations/verifier-contract.md` lines 3 and 5, and a new section "Nonfinal abrupt result" in `docs/operations/b10-abrupt-contract.md`, plus line 71 if question Q9 approves.
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

The capture-loss race fix (approved Revision 7, being implemented on `main`) changes `src/Parchwell.Kafka/Statistics/KafkaStatisticsSession.cs`, new tests under `tests/Parchwell.Kafka.Tests/Statistics/`, `docs/operations/statistics-bridge.md` line 87, `items/404/state.md` and its own receipt. The only shared file is `items/404/state.md`; whichever merges later rebases it. Indirect dependency: PR3b's tests consume the successor's `statistics.jsonl` and `capabilities.json` produced through `KafkaStatisticsSession`. The stop rule (section 14) covers a change it makes to the members PR3b reads.

## 4. External contracts

### 4.1 Mode and configuration (new; PR3c depends on it)

- `Verifier:Mode` value `b10-abrupt-verify`. `VerifierModeParser.TryParse` accepts it; every existing mode keeps its value.
- Required options, each validated as the existing modes validate them: `ScenarioId` exactly `B10`, `ConfigurationId` exactly `C1`, `Phase` exactly `C1`, `Repeat` exactly `1`, `RunId`, `VerifierCommit` equal to `PARCHWELL_SOURCE_COMMIT`, `JobName`, `PodName`, `PodUid`, `LoadGenImage`, `VerifierImage`, `BlobServiceUri`, `LedgerContainer`, `VerifierContainer` (different from `LedgerContainer`), `NamespaceName`, `VerifierClientId`, `TokenTimeout`, `MetadataTimeout`, `WatermarkTimeout`, `PollSlice`, `BlobIoTimeout`, `IoTimeout`, `SafetyStop`, `CleanupTimeout`, `ProcessLifetime`, `MaxConsumedRecords`, `MaxConsumedBytes`, `MaxArtifactBytes`, `MaxTotalInputBytes`, `MaxPartitions`, `MaxInputDepth`, `MaxOutputBytes`, `MaxDiagnosticsPerCheck`, `MaxDiagnosticBytes`. Any other value of the fixed fields fails validation and the host exits nonzero before any read.
- Inputs read: verifier container `PhasePaths.StartLine`, `PhasePaths.StartLineCompletion` and `PhasePaths.BrokerInventory` for `(RunId, C1, 1)`, with the checks `VerifyRunHandler` applies to them; ledger container listing of `<RunId>/C1/r1/b10-abrupt-diagnostic/` and the objects under it; `document-events`.
- Output: one create-only object in the verifier container at `<RunId>/C1/r1/b10-abrupt-diagnostic/nonfinal-result.json`. No `WorkerReceipt`, no `result.json`, no `verify-completion.json` (question Q10).
- Exit code: `0` exactly when the `nonfinal_result` object was created and read back byte-identical, whatever its eligibility; nonzero otherwise (question Q10).

### 4.2 Inputs fixed on `main` (pinned; a change is a stop-rule event)

- Wire envelope, identity members, journal frame members and per-kind `data` members: `docs/operations/b10-abrupt-contract.md` "Wire and state machine" (lines 26 to 42), as `infra/scripts/phase/b10_abrupt.py` lines 26 to 45 list them, including `native_error`, `cohort_seal.native_error_count`, `abrupt_manifest.cohort_files` and `termination.native_error_count,native_fatal_count`.
- First-instance members (15) and their order, read limits and binding roles: same file, "First-instance publication" (lines 56 to 69). Successor members (7): `registration.json, ledger.jsonl, manifest.json, statistics.jsonl, request-lifecycle.jsonl, capabilities.json, completion.json` (`b10_abrupt.py` line 24); `request-lifecycle.jsonl` may be empty.
- `cohort_inventory` data members `instance_id,cohort,inventory_count,inventory,bookkeeping_complete`; inventory entry members `attempt_id,invocation_ordinal,predecessor_attempt_id,key,sequence,payload_length,payload_sha256` (export, G4). Probe ledger row members `attempt_id,completion_source,delivered,error_code,persistence_status`.
- Successor `capabilities.json` members read: `statistics_completion.is_complete`, `guard_observations_complete`, `is_faulted` (export).
- Probe Kafka record: key UTF-8 `B10/{run_id}/C1/1/{same_instance|cross_restart}`; value the payload bytes; headers `instance-id`, `attempt-id`, `sequence`. Timeout record: ordinary key and the same three headers plus `tenant-id`, `document-id`, `event-type` (scratch Kafka run, section 8.1).
- Persistence status numbers: `NotPersisted=0`, `PossiblyPersisted=1`, `Persisted=2`, as `B10RepeatChecks.IsPermittedTimeout` uses them ([PersistenceStatus](https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.PersistenceStatus.html), cited in ADR-021).

### 4.3 Output: `nonfinal_result` (new; closed)

Envelope: exact members `schema_version` `2`, `dialect` `"b10_abrupt"`, `purpose` `"diagnostic_trial"`, `synthetic` `true`, `scientific_admissible` `false`, `kind` `"nonfinal_result"`, `identity`, `data`. `identity` has the 12 identity members: `run_id` RunId, `scenario_id` `"B10"`, `configuration_id` `"C1"`, `phase` `"C1"`, `repeat` `1`, `source_commit` VerifierCommit, `image_reference` VerifierImage, `job_name` JobName, `pod_name` PodName, `pod_uid` PodUid, `instance_id` PodName, `predecessor_instance_id` `null` (question Q10).

`data` exact members:
- `first_instance_id`, `successor_instance_id`: strings, or `null` when that pod directory was not identified.
- `start_line`, `end_line`: arrays of `{partition, offset}` sorted by partition, or `null` when not read.
- `window_complete`: boolean.
- `timeout`: `{ok, loss, phantom, reported_failure, duplicate, reorder}` from the unchanged `OutcomeClassifier.Classify` over the timeout ledger rows and the window's timeout records, or `null` (R11).
- `arms`: exactly two arm objects, `same_instance` then `cross_restart` (R9).
- `comparison`: one of `both`, `cross_restart_only`, `same_instance_only`, `neither`, `unavailable` (R10).
- `native_errors`: `{count, fatal_count, tail}` with `tail` `"unknown_due_to_intended_kill"`, or `null` when the first-instance objects were not authenticated.
- `eligibility`: `"ineligible"` when `eligibility_reasons` is nonempty, otherwise `"pending_sweep"`; never `"eligible"` (question Q3).
- `eligibility_reasons`: array of distinct reason literals of section 4.4, in that list's order.
- `finality`: `"nonfinal"`.

Arm object exact members: `arm`, `accepted_originals`, `checkpoint_unconfirmed`, `selected_attempt_id`, `selected_sequence`, `originals_without_report`, `originals_seen`, `unreported_seen`, `unreported_unseen`, `selected_original_seen`, `resend_attempt_id`, `resend_invoked`, `resend_terminal` (`{delivered, error_code, persistence_status}` or `null`), `resend_seen`, `pair_count`, `witness` (`{original_attempt_id, resend_attempt_id, original: {partition, offset}, resend: {partition, offset}}` or `null`), `lost_resend` (boolean or `null`). Counts are JSON integers from 0 to 9223372036854775807. Absence rules are R9's.

Writer and reader limits: the existing `MaxOutputBytes`, depth 32, no duplicate member, no unknown member, no fractional or negative count. Old readers reject it (R7).

### 4.4 Eligibility reason literals (new; question Q2)

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

Not in the list, and why: `kill_too_early`, `kill_too_late` and `b10_abrupt_exposure_inconclusive` cannot reach the verifier with a complete publication, because the supervisor writes no termination receipt and no selection without a nonempty kill selection (`b10_abrupt.py` lines 507 and 508 reject an entry count other than 1 with `kill_too_late`) and blocks the kill arm on an empty control selection (contract line 154). If such an object set is ever published, the verifier's own recomputation (R4) fails it with reason 3.

## 5. Requirements

Every observation below happens at the real `b10-abrupt-verify` handler, given real store objects and a real Kafka window, through the entry point of section 4.1, and is read from the `nonfinal_result` it writes. Private helpers are examples, not the place of observation. Each requirement keeps unrelated evidence valid in its tests.

R1. WHEN the start-line note or its completion receipt fails the checks `VerifyRunHandler` applies to them (lines 84 to 147), or the note's expected job names differ from `PhaseIdentity.JobName` and `PhaseIdentity.RestartJobName` for `(RunId, C1, 1)`, THEN the reasons are exactly `["b10_restart_configuration_invalid"]`, `start_line`, `end_line`, `timeout` and every arm count are `null`, and no Kafka records are consumed.

R2. WHEN the listing of `<RunId>/C1/r1/b10-abrupt-diagnostic/` is incomplete, or the first pod's directory lacks any of the 15 members, or a member exceeds its read limit (contract table; 65536 bytes for the six envelope members), or a member other than `request-lifecycle.jsonl` is empty, THEN the reason is `b10_restart_first_evidence_unavailable`, and `native_errors`, `timeout` and every arm member except `arm` are `null`. The first pod is the one directory whose `registration.json` identity `job_name` is `PhaseIdentity.JobName(RunId, C1, 1)`; the successor is the one whose `job_name` is `PhaseIdentity.RestartJobName(RunId, C1, 1)`. A name under the prefix that is not `<pod>/<member>` with a member of its set, or a second directory claiming either Job, gives `b10_restart_identity_unauthenticated`.

R3. WHEN any first-instance envelope, identity, binding or journal check fails, THEN the reason is `b10_restart_identity_unauthenticated` and R2's `null` rule applies. The checks are those of `b10_abrupt.py` `accept_diagnostic_publication`, `accept_fatal_coverage` and `accept_cohort_files` (lines 407 to 588 of `b10_abrupt.py`), done independently in .NET, plus: identity equals RunId, `B10`, `C1`, `C1`, `1`, VerifierCommit, LoadGenImage, the first Job, the pod directory name as `pod_name` and `instance_id`, `null` predecessor, one `pod_uid` across all objects; the journal ends with a newline and has no other content; no `synchronous_rejection` frame; every `prepared` attempt has exactly one `accepted`; every `terminal` names a prepared attempt, at most once; each probe `prepared` has key and `probe_id` equal to its arm key and `payload_sha256` equal to the selection entry's.

R4. WHEN the journal disagrees with a checkpoint, THEN the reason is `b10_restart_identity_unauthenticated`. The verifier recomputes, per arm: the five originals are the arm's `prepared` frames with `predecessor_attempt_id` `null`, in `invocation_ordinal` order, with sequences 0 to 4, each accepted before the checkpoint frame; `accepted_attempt_ids` equals that list; `selected_attempt_ids` equals its members with no `terminal` frame earlier than the checkpoint frame, in the same order. For the kill arm, the selection entry is the first candidate with no `terminal` frame anywhere in the journal.

R5. WHEN the termination receipt fails (status, signal inventory and order, signal results, kill intent, single exit after the last signal, cgroup before equal to after, checkpoint position between signals), THEN the reason is `b10_restart_termination_unverified` and R2's `null` rule applies.

R6. WHEN the authenticated journal holds a `native_error` frame with `is_fatal` true, THEN the reasons include `b10_restart_fatal_captured`; WHEN it holds one with `is_error` true and `is_fatal` false, THEN they include `b10_restart_native_error_captured` (question Q8). Both may be present. `native_errors.count` is the number of `native_error` frames and `fatal_count` the number with `is_fatal` true. Neither reason blocks the comparison.

R7. WHEN `PhaseWireCodec.ReadRegistration`, `PhaseWireCodec.ReadProducerCompletion`, `ArtifactIndexReader.Read`, `ManifestReader.Read`, `B10SweepEvidenceCodec.ReadFinalization` or `PhaseCandidateCodec.Read` meets a `nonfinal_result` written by the handler, THEN each rejects it as `B10AbruptRejectionTests` asserts for the other abrupt kinds, and `B10AbruptRejectionTests` passes unchanged.

R8. WHEN the window consumer reads a record whose decoded key is one of the repeat's two probe keys, THEN it records the lowercase SHA-256 of the value bytes; a `null` value adds a structural problem with code `null_value` and field `value`. Records of other keys, and every existing caller, are unchanged in behaviour and in the `ConsumedRecord` values they produce (question Q5).

R9. Per arm, after R1 to R5 pass:
- `accepted_originals` is the arm's five originals (R4); `checkpoint_unconfirmed` the length of its checkpoint's `selected_attempt_ids`.
- `selected_attempt_id`: same instance, the first of the control checkpoint's `selected_attempt_ids`; cross restart, the kill selection entry. `selected_sequence` is its `prepared` sequence.
- `originals_without_report`: originals with no `terminal` frame anywhere in the journal. `unreported_seen` counts those consumed at least once; `unreported_unseen` the rest; their sum equals `originals_without_report` (question Q6).
- `originals_seen`: originals consumed at least once. `selected_original_seen`: 1 when the selected original was consumed, else 0.
- Resend: same instance, the one journal `prepared` frame with cohort `same_instance` and a non-null predecessor; cross restart, the successor's single invocation. `resend_invoked` 1. `resend_terminal` from the journal `terminal` frame (same instance) or the successor ledger row (cross restart), or `null` when none.
- `pair_count`: when the resend names the selected original as predecessor, the number of distinct `(partition, offset)` of the selected original times that of the resend; otherwise 0. Copies of one attempt are never a pair. `witness`: when `pair_count` is at least 1, the lexicographically smallest `(original coordinate, resend coordinate)`; otherwise `null` (question Q12).
- `lost_resend`: true when `resend_terminal.delivered` is true and `resend_seen` is 0; false otherwise.
- Absence: when R2 or R3 fails (including R3 failing from a record join, R13), every arm member except `arm` is `null`. When the window is incomplete, `originals_seen`, `unreported_seen`, `unreported_unseen`, `selected_original_seen`, `resend_seen`, `pair_count`, `witness` and `lost_resend` are `null`. When the arm's resend is unknown (same instance: not exactly one control resend; cross restart: successor evidence unavailable or invalid), `resend_attempt_id`, `resend_invoked`, `resend_terminal`, `resend_seen`, `pair_count`, `witness` and `lost_resend` are `null`. Unknown is never zero.

R10. WHEN no comparison-blocking reason (section 4.4) is present, THEN `comparison` is `both` when both arms have `pair_count` at least 1, `cross_restart_only` or `same_instance_only` when only that arm has, and `neither` otherwise. WHEN one is present, THEN `comparison` is `unavailable`.

R11. Timeout cohort, after R3: WHEN the timeout seal has cohort `timeout`, `drain_complete` and `guard_complete` true, its `count` equals the manifest's `attempt_count`, the manifest (read by the unchanged `ManifestReader.Read`) is `bookkeeping_complete` with `worker_status` `complete` and `attempt_count` equal to its inventory length, the ledger (read by the unchanged `LedgerReader.ReadRows`) has exactly one row per inventory attempt with equal instance, key and sequence, every row's instance is the first instance, and every non-delivered row satisfies `B10RepeatChecks.IsPermittedTimeout`, THEN `timeout` holds the six counts from `OutcomeClassifier.Classify` over those rows and the window's records with non-probe keys. Otherwise the reason is `b10_restart_timeout_bookkeeping_unavailable` and `timeout` is `null`. Probe records never enter these counts. `timeout` is also `null` when the window is incomplete or R3 failed.

R12. Control cohort, after R3: WHEN the control seal has cohort `same_instance` and `drain_complete` and `guard_complete` true, `control-manifest.json` is a `cohort_inventory` with the first instance, cohort `same_instance`, `bookkeeping_complete` true, `inventory_count` equal to its length and to the seal's `count`, its inventory equals the journal's `same_instance` prepared attempts in `invocation_ordinal` order member for member, `control-ledger.jsonl` has exactly one row per inventory attempt and each row equals that attempt's journal `terminal` data, and the one control resend names the first of the control checkpoint's `selected_attempt_ids` with equal key, sequence and payload hash, THEN no control reason is added. Otherwise the reason is `b10_abrupt_control_incomplete`, or `b10_restart_resend_count_invalid` when the journal holds other than one control resend.

R13. Record join, when the window is complete: a probe-key record with no structural problem joins `(instance_id, attempt_id)` to a first-instance `prepared` attempt or to the successor inventory entry, with equal key, sequence and value hash; a non-probe record joins the timeout inventory with equal key and sequence. A record with a structural problem, an unknown instance or attempt, or a mismatch gives `b10_restart_identity_unauthenticated`. Exception: when the successor evidence is unavailable or invalid, structurally valid records carrying the successor's instance ID are held out of the join and add no reason.

R14. Successor, after R3: WHEN any of the 7 members is missing, or over its limit, or the registration, receipt or inventory envelope fails (identity: the successor Job, the successor pod directory, predecessor the first instance; one `pod_uid` different from the first's; pod name different from the first's), or the receipt fails to bind registration and the five payloads in order, or its `first_termination` or `selection` descriptors differ from the first-instance bytes, or `release_utc` differs from the kill checkpoint's, or `cross_host_timing` is not `not_verified`, or `cleanup_completed` is not true, or `capabilities.json` has `statistics_completion.is_complete` not true, `guard_observations_complete` not true or `is_faulted` not false, or the invocation does not keep the selection entry's key, sequence and payload hash with a new attempt ID and its predecessor, or `invocation_utc` precedes `release_utc`, or the inventory is not one entry equal to the invocation, or the ledger is not one row for it, THEN the reason is `b10_restart_successor_evidence_unavailable`. WHEN the receipt holds other than one invocation, THEN it is `b10_restart_resend_count_invalid`. WHEN `ready_utc` is later than `release_utc` and all else passes, THEN it is `b10_abrupt_successor_late`. WHEN no successor directory exists, THEN it is `b10_restart_successor_evidence_unavailable`.

R15. WHEN the end-line read or the drain fails, or the safety stop expires before every partition reaches its end offset, THEN the reason is `b10_restart_window_incomplete`.

R16. WHEN the handler finishes reading, THEN it writes exactly one `nonfinal_result` (section 4.3), create-only, reads it back, and exits 0. WHEN the write or read-back fails, THEN it exits nonzero and writes nothing else.

## 6. Invariants and safety

- Verdict integrity (AGENTS.md GOVERNANCE): nothing here edits, proposes values for or reads live results into `expected.yaml` or `results/`. The nonfinal result is never a published result (factory.md "Evidence pack").
- Hard safety rules: no Azure credential, no apply or destroy, no workflow dispatch, no trial. A trial after this pull request needs Hari's explicit approval and environment-gate approval ([6749565231](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749565231)).
- Missing evidence is `null`, never zero; unknown never becomes a persistence status (ADR-021 protocol rules 13 and 14, decision rule 5).
- Ordinary scenarios: every existing mode, reader and output is unchanged (section 3.2, R7, R8).
- No change to any protocol sequence, gate, policy value, verdict rule, timeout, existing failure name or design text. The new literals of sections 4.3 and 4.4 wait for Hari (Q2, Q3).

## 7. Never-ran inventory

Evidence is the trial artifacts. Runs and artifacts, all on `main`, all downloaded with `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/<id>/zip` into new empty scratch directories; the zip SHA-256 of each matched the table in `items/404/plan-verifier.md` section 7 (trial 7: `bee17231882ffeb7ddc3134ba10a00ec4590f20e96dad409c050decf1f22cdcf`). Recorded modes: G1.

Upstream stages this plan reads:

| Stage | Ran live | Evidence | Offline test that runs it for real |
|---|---|---|---|
| U1 Verifier `start-line` for B10 | Yes, all 7 | each `phase-status.json`: `start-line complete` | T7 runs the real `StartLineHandler` |
| U2 First-instance 7 pre-PR3a members | Yes, trial 7 | 7 objects in artifact 13704155185 | T2 on those bytes |
| U3 PR3a cohort-file publication and `cohort_files` | No; merged after trial 7 | G2 | T1, T4 on the real export path |
| U4 PR3a `native_error` frames and counts | No | G2; trial 7 seals lack `native_error_count` | T6 on the real export path with native errors |
| U5 Successor publication of 7 members | No | trial 7 `phase-status.json`: `b10_abrupt_publication_ambiguous` at `successor_publication` | T5 on the real export path (real statistics chain) |

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

All new classes are named `B10AbruptVerify*` so command A4 selects them. Real inputs: "export" means the 22 files produced in-test by running the LoadGen test assembly as `B10AbruptRejectionTests.ProduceExportAsync` does (`B10AbruptRejectionTests.cs` lines 89 to 116), into a fresh directory; "native export" is the same with `PARCHWELL_B10_ABRUPT_WIRE_NATIVE_ERRORS=1` (Q11). Export identities: run `synthetic`, first pod `synthetic-first`, successor pod `synthetic-second`, source `a` x 40, image `registry.invalid/loadgen@sha256:` + `b` x 64. "Rebound" means a semantic edit followed by recomputing every dependent descriptor (manifest, termination, successor receipt, and for a cohort file its seal and `cohort_files` entry), so only the predicate under test is wrong.

Unless a row says otherwise, the handler runs with the export in its ledger store, the real `StartLineHandler` output in its verifier store and a complete window of record set A (T7).

- T1 (R2, R16). Export, window A. Expected: exit 0; `eligibility` `pending_sweep`; `eligibility_reasons` `[]`; `first_instance_id` `synthetic-first`; `successor_instance_id` `synthetic-second`; `finality` `nonfinal`. Boundaries: each of the 15 first members removed in turn gives `["b10_restart_first_evidence_unavailable"]`; `request-lifecycle.jsonl` empty is accepted; `journal.jsonl` at 16777217 bytes is rejected with reason 2; an extra object `synthetic-first/extra.json` gives reason 3.
- T2 (R2, R3, G7). Trial 7 fixtures: the 7 first-instance objects and successor `registration.json` of artifact 13704155185, byte-identical, SHA-256 pinned in the test: `abrupt-manifest.json` `3a25132454c41c9cbc70950f9ae5e80f9974ee48b9c03fc73b29aad098a9747a`, `control-seal.json` `31537beac65841212c34c74311aa69e608167f4b98915f0983f2d47de029e8da`, `journal.jsonl` `6fa8d06ecf5170d446e490733338b811aac94c6d1dd9103b28d8c2f69582d77f`, `kill-selection.json` `0f1bff4a0581e2af2775666c06d0f21fb4f59abdc4405845153bbfcda2a90059`, `registration.json` `8407008cd88b92f734b21c03385e54a48786478999b810da34c96990c3721454`, `termination.json` `eba48bdf255faa4a7db86a6de492de91d6c70740844897749ac57a97a77af9a9`, `timeout-seal.json` `97bd832ef06681b1dcdcbd7a70bd13a9d4390bd803e9481d3a605b48faf36a7f`, successor `registration.json` `40656c1d1105dd00d4a2b0cb5d25e89a769b47cf19565acd99e9b1bceb735d1a`. Options from the fixture's registration identity. Expected with an empty complete window: reasons exactly `["b10_restart_first_evidence_unavailable"]`, `native_errors` `null`. With 8 placeholder cohort files of 1 byte each: `["b10_restart_identity_unauthenticated"]`. The test also asserts no fixture contains any of `AZURE_`, `tenant`, `subscription`, `AccountKey`, `SharedAccessSignature`, `sig=`, `Bearer `, `password`, `secret` (case-insensitive); the scratch scan found none, and found that the artifact's `pods.json` files and container logs carry the tenant and client ID values, so they are never fixtures.
- T3 (R3, R4, R5). Export variants, each giving exactly one reason. Not rebound: one byte of `journal.jsonl` changed gives 3. Rebound, giving 3: `registration.json` `source_commit` changed; `kill-selection.json` `schema_version` 1; `termination.json` `scientific_admissible` true; unknown member in `control-seal.json` `data`; journal frame 20 removed; journal frame 15 renumbered 115; control checkpoint `selected_attempt_ids` first member dropped; `gap` frame appended; `timeout-seal.json` `post_drain_statistics.sha256` changed to 64 zeros. Rebound, giving 4: `mechanical_status` `incomplete`; signal list reversed. In each case every arm member except `arm` is `null`.
- T4 (R11, R12). Export variants. Window A: `timeout` `{ok 1, loss 0, phantom 0, reported_failure 0, duplicate 0, reorder 0}`. Rebound `timeout-seal.json` `count` 2: `["b10_restart_timeout_bookkeeping_unavailable"]`, `timeout` `null`, `comparison` `both`. Timeout row rebound to `delivered false, error_code -1, persistence_status 0`: same reason. Same row with `error_code -192`: no reason, `timeout` `{0, 0, 1, 0, 0, 0}`. Rebound `control-seal.json` `drain_complete` false: `["b10_abrupt_control_incomplete"]`, `comparison` `unavailable`, arm counts still present. Control ledger last row dropped (rebound): same. Control ledger first row `persistence_status` 1 (rebound): same.
- T5 (R14). Export successor variants, rebound: `ready_utc` after `release_utc` gives `["b10_abrupt_successor_late"]`; invocation `attempt_id` equal to its predecessor gives `["b10_restart_successor_evidence_unavailable"]`; two invocations give `["b10_restart_resend_count_invalid"]`; `cleanup_completed` false, the first pod's `pod_uid` in all successor envelopes, and `statistics_completion.is_complete` false each give `["b10_restart_successor_evidence_unavailable"]`; successor directory absent gives the same. In each, `comparison` `unavailable`, cross-restart resend members `null`, same-instance `pair_count` 1, and window A's successor record adds no reason.
- T6 (R6). Native export, window A. Expected: reasons `["b10_restart_native_error_captured", "b10_restart_fatal_captured"]`, `native_errors` `{count 3, fatal_count 1, tail "unknown_due_to_intended_kill"}`, `comparison` `both`. The native export's journal holds `native_error` frames at journal sequences 1, 24 and 39 with `capture_sequence` 1, 2, 3, `is_fatal` false, true, false (scratch run).
- T7 (R1, R8 to R10, R13, R15). Component test on the existing `LocalKafkaFixture` (`confluentinc/cp-kafka:7.5.12@sha256:997883c60e6f1207fb5f779c7a8fdae57312a17f446fd5ea3eb8b73f52d4a1cf`), topic `document-events` with 1 partition. The real `StartLineHandler` writes the start line; a real Confluent.Kafka producer writes, in this order, record set A: the timeout attempt `synthetic-first-timeout-0` (key `tenant-d0`, sequence 0, the three timeout headers), then each export `prepared` attempt in journal order with its key, sequence, headers and the selection payload, then the successor invocation `synthetic-second-replay-0`. Then the real handler runs. Observed on the prototype (section 8.1):

  | Case (records) | Reasons | Comparison | Same instance | Cross restart |
  |---|---|---|---|---|
  | a: A | none, `pending_sweep` | `both` | selected `synthetic-first-same_instance-0`, `originals_without_report` 0, unreported 0/0, `pair_count` 1, witness offsets (1, 6), `lost_resend` false | selected `synthetic-first-cross_restart-6`, `originals_without_report` 5, unreported 5/0, `pair_count` 1, witness offsets (7, 12), `lost_resend` false |
  | b: A without the successor record | none | `same_instance_only` | as a | `resend_seen` 0, `pair_count` 0, witness `null`, `lost_resend` true |
  | c: A without the five kill originals | none | `same_instance_only` | as a | unreported 0/5, `pair_count` 0 |
  | c2: A without `cross_restart-6` | none | `same_instance_only` | as a | unreported 4/1, `selected_original_seen` 0, `pair_count` 0 |
  | d: A without the control resend | none | `cross_restart_only` | `pair_count` 0, `lost_resend` true | as a |
  | e: empty window | none | `neither` | `lost_resend` true | unreported 0/5, `lost_resend` true; `timeout` `{0,1,0,0,0,0}` |
  | f: A plus a second copy of `cross_restart-6` | none | `both` | as a | `pair_count` 2 |
  | g: case b plus a second copy of `cross_restart-6` | none | `same_instance_only` | as a | `pair_count` 0 |
  | h: case b plus the successor record with value byte 0 flipped | `b10_restart_identity_unauthenticated` | `unavailable` | all `null` | all `null` |
  | i: A plus a probe record with no `attempt-id` header | as h | as h | as h | as h |
  | j: A plus `cross_restart-7` with a null value | as h | as h | as h | as h |
  | k: A plus a copy of the successor record with instance `synthetic-third` | as h | as h | as h | as h |
  | l: A without the timeout record | none | `both` | as a | as a; `timeout` `{0,1,0,0,0,0}` |
  | m: A plus a timeout-key record for unknown attempt `synthetic-first-timeout-9` | as h | as h | as h | as h |
  | n: safety stop 1 ms before any record is read | `b10_restart_window_incomplete` | `unavailable` | seen family `null` | seen family `null` |
  | cfg: start-line note with a third expected job name | `b10_restart_configuration_invalid` only | `unavailable` | all `null` | all `null` |

  The same test asserts R8 directly at the drain: every probe record in A carries value hash `87b7f8decf017e82301b20dec010123c468144e3bb521b88eb0d755d49be288d` (the export payload, 4 bytes, base64 `L2P4Dw==`), the timeout record carries none, case j's record carries problem `null_value` on field `value`, and case i's carries `missing_header` on `attempt-id`. Case cfg was specified, not prototyped.
- T8 (R7, R16). The `nonfinal_result` written in T7 case a is read back byte-identical by the new reader; one unknown member, one missing member, a count of 1.5, a count of -1 and `eligibility` `"eligible"` are each rejected; each of the six scientific readers of R7 rejects the written bytes. A store whose create throws makes the handler exit nonzero.
- T9 (section 4.1, R16). `Program.BuildHost` with the configuration of section 4.1 resolves the `b10-abrupt-verify` handler; each of `ScenarioId` `A01`, `Phase` `C0`, `Repeat` 2 and missing `LoadGenImage` fails validation; mode `verify` still resolves `VerifyRunHandler`.
- T10 (R8, existing callers). `WindowConsumerTests` and `MessageIdentityReaderTests` pass unchanged; a new test drains a record with a probe key through the existing `WindowConsumer.Drain` signature and asserts it carries no value hash.

Must keep passing unchanged: `B10AbruptRejectionTests` (2 tests), the Component classes (13 tests on `main`), the full Verifier suite, `test_b10_abrupt.py`, `test_c5_workflow.py`.

Mutation-only build: every new test file sits under `Component/`, which the existing exclusion removes when `ParchwellVerifierMutationOnly=true` (`Parchwell.Verifier.Tests.csproj` lines 20 to 22); the csproj is unchanged. A5's class filter `*Component*` matches class names, not folders, so its total stays 13.

### 8.1 Prototype

A scratch prototype, never committed, pinned every value above:
- Python reader, reconciliation and classification (`proto.py`, SHA-256 `8d696be01d21652b3125c7a8323249b0b4a1da805e67c5d6a42bf14a34830c32`), its case runner (`cases.py`, `7dbfa386fdf376cfb442fe9397e993394372658f9a0b06c960ceb80c623153a5`) and mutation runner (`mutate.py`, `df7fe6e1b1879a729ae5224f141bdb2905f9e31acab90e65b0fd35114fd240bd`), run on the export, the native export and the trial 7 bytes.
- A scratch xunit class in the Verifier test project (`ScratchAbruptProtoTests.cs`, `d8b97decef2729b63fb079b5d4117956d38ba1a1a77051d643d5b43826dba9d5`) with the value-hash overload of Q5 option B applied to the three files of section 3.1 item 3: it started `LocalKafkaFixture`, ran the real `StartLineHandler` for run `synthetic`, produced record set A and the deviation records with a real producer, built the window with the real `RunWindow.Create` and drained it with the real `WindowConsumer.Drain`. Window A: 13 records at offsets 0 to 12, checks 9 and 10 pass. The Python classifier consumed its output.

### 8.2 Mutation table

Each mutation was applied to the prototype and the cases rerun; "caught by" names the planned test whose literal expectation changed.

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

Equivalent mutant, recorded: counting a pair when the resend does not name the selected original survived, because R12 and R14 already reject such a resend before classification. The predicate stays as defence in depth; no reachable input distinguishes it.

## 9. Deployment assumptions

- DA1. The verifier Job's workload identity can list and read `<RunId>/C1/r1/b10-abrupt-diagnostic/` in the `ledger` container. Basis: `infra/ephemeral/identity.tf` line 13 assigns `Storage Blob Data Reader` to the verifier at scope `module.storage.containers.ledger.id`, the same scope the ordinary `verify` mode lists. Microsoft states that "Storage Blob Data Reader" grants "read-only permissions to Blob storage resources" and that a role assignment's scope can be a container ([Authorize access to blobs by using Microsoft Entra ID](https://learn.microsoft.com/azure/storage/blobs/authorize-access-azure-active-directory#assign-azure-roles-for-access-rights), read 2026-11-10). That a container-scoped assignment covers every blob path under it is inferred; the page section read in this session did not quote the container item. No offline test covers it. Blocking until Hari accepts it by name (question Q4). Failure gives a failed listing, recorded as `b10_restart_first_evidence_unavailable`, not a wrong result.
- DA2. A live trial publishes the 22 files in the formats the export produces. PR3a's own deployment assumptions, accepted in [6750940329](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750940329) and [6751703425](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6751703425), cover the publication; this plan adds none.
- The scope record's DA2 (a verifier Job in the trial namespace) belongs to PR3c, and its DA3 (native fatal delivery) to PR3a; neither is assumed here.

## 10. Acceptance commands

Run in this session on a clean worktree of `main` at `aadb0c6d`, .NET SDK 10.0.402, local Docker daemon. Observed output on `main` is quoted; the expected output after this pull request follows it.

- A0 build: `dotnet restore Parchwell.slnx --locked-mode && dotnet build Parchwell.slnx --configuration Release --no-restore`. Observed: `Build succeeded.` `0 Warning(s)` `0 Error(s)`. After: the same.
- A1 export: `PARCHWELL_B10_ABRUPT_WIRE_OUTPUT=<new empty directory> dotnet tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptWireProducerTests`. Observed: `Total: 8, Errors: 0, Failed: 0, Skipped: 0`; the directory holds `export.json` and 22 files. After: the same; with `PARCHWELL_B10_ABRUPT_WIRE_NATIVE_ERRORS=1` also set, the journal holds three `native_error` frames (observed with the scratch switch).
- A2 full Verifier suite: `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --no-build`. Observed: `Test run summary: Passed!` `total: 7389` `failed: 0` `succeeded: 7389` `skipped: 0`. After: exit 0, `failed: 0`, `skipped: 0`, total equal to the `main` total plus the new tests.
- A3 abrupt classes: `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --no-build -- --filter-class "*B10Abrupt*"`. Observed: `total: 2` `failed: 0` `succeeded: 2` `skipped: 0`. After: exit 0, `failed: 0`, total 2 plus the A4 total.
- A4 new tests: `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --no-build -- --filter-class "*B10AbruptVerify*"`. Observed: `succeeded: 0` and `Test run completed with non-success exit code: 8`, the testing platform's code for zero tests run. After: exit 0, `failed: 0`, `skipped: 0`, every test of T1 to T10 run.
- A5 component tests: `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --no-build -- --filter-class "*Component*"`. Observed: `total: 13` `failed: 0` `succeeded: 13` `skipped: 0`. After: exit 0, the same total; a run with no Docker daemon fails and is not a pass.
- A6 phase script: `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_b10_abrupt.py'`. Observed: `Ran 12 tests` `OK`. After: the same.
- A7 C5 workflow: `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_workflow.py'`. Observed: `Ran 107 tests` `OK`. After: the same.
- A8 repository check: `./scripts/verify-dotnet.sh`, then `npm ci --ignore-scripts && npm run check:docs` and `git diff --check`, under docs/agents/pull-requests.md rule 8. Not run for a plan file; the implementer records them.

## 11. Plan-check questions (factory.md "Item lifecycle" step 1)

(a) Runtime-sized effort: none in the tests; the export is 22 files, the record sets are fixed by section 8, and the trial 7 fixture set is 8 files. Disposition caps from the item plan apply per pull request (contract "Policy" table): 20 documentation-warning, 20 verification-failure and 20 review-finding root causes, exhaustion `push -wip and hand off to the next session`. No C5 cut computation (G8, section 3.2).

(b) Stricter than the parent: the never-ran inventory and real-input tests (decision 6749565231); R12's comparison of control ledger rows with journal terminals, and R14's capability checks, go beyond what `b10_abrupt.py` checks, under ADR-021 protocol rules 12 and 14 ("conflicting terminals ... reject authoritative evidence"). R6 makes a journaled error-level callback ineligible, which PR3a's first instance does not gate (PR3a recorded point); that follows ADR-021 "Permitted conditions" and protocol rule 5, and is question Q8.

(c) Code or data neither on `main` nor in this pull request: the live publication (DA2, covered by PR3a's accepted assumptions) and the live blob listing (DA1). The capture-loss fix is not a dependency; section 3.3 and the stop rule cover it.

## 12. Sources

- docs/blueprint.md ADR-012 and ADR-021 at `aadb0c6d`.
- docs/operations/b10-abrupt-contract.md and docs/operations/verifier-contract.md at `aadb0c6d`.
- docs/agents/factory.md "Item lifecycle", "Authority reads"; docs/agents/pull-requests.md.
- Code at `aadb0c6d`: `infra/scripts/phase/b10_abrupt.py`; `src/Parchwell.Verifier/Program.cs`, `VerifierHostOptions.cs`, `VerifierHostOptionsValidator.cs`, `VerifyRun/VerifyRunHandler.cs`, `VerifyRun/Window/*`, `VerifyRun/Input/ConsumedRecord.cs`, `ManifestReader.cs`, `LedgerReader.cs`, `Judgement/OutcomeClassifier.cs`, `B10RepeatChecks.cs`, `Common/Phase/PhasePaths.cs`, `PhaseIdentity.cs`; `tests/Parchwell.Verifier.Tests/Host/B10AbruptRejectionTests.cs`, `Component/ComponentRunHarness.cs`, `Component/LocalKafkaFixture.cs`; `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptWireProducerTests.cs`, `B10AbruptSuccessorCompletionTests.cs`; `infra/ephemeral/identity.tf`; `.github/CODEOWNERS`.
- Trial artifacts 13609836611, 13615377394, 13629179730, 13639442741, 13668809132, 13699980854, 13704155185.
- [Confluent.Kafka PersistenceStatus](https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.PersistenceStatus.html); [Authorize access to blobs by using Microsoft Entra ID](https://learn.microsoft.com/azure/storage/blobs/authorize-access-azure-active-directory#assign-azure-roles-for-access-rights).

## 13. Open questions for Hari

- Q1. Implementing tool. The only tool-naming comment, [6705495747](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6705495747), says Codex; standing decision 6752430614 replaced it with Claude Code for four other pull requests only, and 6761052971 does not extend it. Options: A Codex, as recorded; B Claude Code. Recommendation: name it explicitly at approval; A if nothing is said. Counterargument to A: every recent slice of this item ran on Claude Code, whose sessions know the abrupt code and fixtures.
- Q2. The 12 reason literals of section 4.4, two of them reused existing names. Options: A approve as listed; B rename; C fold them into fewer reasons. Recommendation: A, because each maps to one ADR-021 decision rule 2 condition and they appear only in diagnostic output now. Counterargument: these words will later appear in published B10 evidence; choosing them before the five-repeat judge exists may force a rename then.
- Q3. Eligibility literal. A one-repeat run can never be eligible, because sweep finality is required. Options: A `ineligible` or `pending_sweep`, with no sweep reason (planned); B always add a reason `b10_restart_sweep_not_final` and use only `ineligible`. Recommendation: A, so a clean diagnostic observation is visible at a glance. Counterargument: B states the missing gate as a reason, the way ADR-021 rule 2 asks every ineligibility reason to be published.
- Q4. Accept DA1 (verifier identity lists and reads the abrupt prefix of `ledger`) by name, or name a test. Recommendation: accept; failure gives a named failed listing, not a wrong result. Counterargument: the container-scope coverage is inferred, not quoted.
- Q5. Value hash placement. Options: A hash every record's value in the shared reader; B an optional set of keys passed through a new `WindowConsumer.Drain` overload, so existing callers and their `ConsumedRecord` values are unchanged (prototyped); C a B10-only drain loop. Recommendation: B. Counterargument: it still edits two files every scenario uses; C touches nothing shared at the cost of a duplicated drain loop.
- Q6. Unreported counts. ADR-021 rule 14 requires "unreported-seen plus unseen equal to originals without reports", and the rule 15 amendment says decision rule 3's `unreported_seen >= 1` "counts the selected member only". Options: A count all burst originals without a report in `unreported_seen` and `unreported_unseen`, and publish the selected member separately as `selected_original_seen` (planned); B count only the selected member. Recommendation: A, which satisfies both sentences. Counterargument: two members with similar names invite the later judge to read the wrong one for the positive test.
- Q7. Guard re-evaluation. The four published statistics reports (two post-drain, two pre-acceptance) are checked for binding only; the seals' `guard_complete` is trusted. Options: A binding only (planned); B also parse each report with the existing `NativeStatisticsReader` and re-check `txerrs`, `rxerrs` and expected brokers. Recommendation: A for this slice, B before scientific admission. Counterargument: ADR-021 rule 12 says each cohort "passes independent checks"; A trusts the supervisor's guard verdict.
- Q8. A journaled `native_error` with `is_error` true and `is_fatal` false. Options: A ineligible with `b10_restart_native_error_captured` (planned; ADR-021 "Permitted conditions": "error-level native callback ... still invalidate the repeat"); B record it without affecting eligibility. Recommendation: A. Counterargument: PR3a lets the first instance continue on such a frame, so a trial can complete and still be ineligible here.
- Q9. `docs/operations/b10-abrupt-contract.md` line 71 says all eight cohort files are schema one; two are not (G4). Options: A correct the sentence in this pull request; B a separate documentation pull request. Recommendation: A, since this pull request adds the reader that depends on the difference. Counterargument: it edits PR3a's contract text in a verifier pull request.
- Q10. New external contract for PR3c: mode name, output path, no worker receipt, exit 0 whenever the result is written, and the result envelope carrying the verifier Job's identity with `instance_id` set to the verifier pod. Options: A as planned; B also write a schema-one `WorkerReceipt`; C exit nonzero when ineligible. Recommendation: A. Counterargument to A: other modes publish a completion receipt, and PR3c must then read the result object to know the outcome; and `instance_id` names a producer instance everywhere else.
- Q11. One line outside the verifier: an environment switch in `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptWireProducerTests.cs` so the export can run PR3a's native-error variant (prototyped). Options: A allow it; B produce native-error variants by editing and rebinding the export journal in verifier tests. Recommendation: A, so T6 uses real frames. Counterargument: B keeps the pull request inside protected verifier paths only.
- Q12. Pair counting and witness. Options: A `pair_count` as the product of distinct coordinates of the selected original and its resend, witness the smallest coordinate pair (planned); B `pair_count` 0 or 1. Recommendation: A, following rule 13 "Count all pairs". Counterargument: a product can exceed the number of physical records and may be read as a duplicate count.

## 14. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 4, a requirement in section 5 or the scope in section 3; when an answer to a question in section 13 differs from the plan; when any format in section 4.2 differs on the branch base from `main` at `aadb0c6d`; or when the capture-loss race fix, or any other merged change, alters the successor `statistics.jsonl` or `capabilities.json` members of section 4.2, the export's file set, or any literal in section 8. It also stops at each disposition cap of section 11 with that cap's exhaustion action.

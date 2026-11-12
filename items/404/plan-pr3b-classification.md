# PR3b classification contract-grade plan

Revision 4. Issue 404, pull request short name `PR3b classification`. Status: not approved. Implementing tool: Codex ([6761524200](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761524200) point 1, kept by [6816117386](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816117386) point 4). Hari merges.

This pull request adds a diagnostic-only verifier mode, `b10-abrupt-verify`, and runs it in the B10 diagnostic trial before the ephemeral layer is destroyed. For each unconfirmed probe message, it reports how many copies `document-events` holds and which instance and attempt wrote each copy. It selects no verdict, computes no eligibility and makes nothing admissible to science.

## 0. Changes

Since Revision 3 (`35fdefae`), after plan check [6816525517](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816525517) and decision [6816733258](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816733258):
- B1: check 2 keeps the two start-line names under 6816733258 point 1, which extends the gap of 6787849492 point 1 to the later full-reconciliation pull request of this mode. The authority line for 6787849492 is corrected. 2.1 item 8 pins the new text for contract line 66 and verifier-contract.md line 5.
- B2: LA-V2 to LA-V5 are accepted by name for the next diagnostic trial only (6816733258 point 2). Open questions: none.
- B3: the LA-V2 and LA-V5 evidence now cites the A01 final run, read on `main`.
- P2-A: R4 names every `cross_restart` field for problems 3 and 4; T6 and T7 assert them.
- P2-B: the ledger double uses `IProducerArtifactStore`.
- P2-C: T8 gains subcases for checks 5, 8, 9 and 10.
- P2-D: R1 and T13 pin exit code 1.
- P2-E: check 3 names the result of an incomplete listing; answer (a) adds the listing bound.
- P2-F: three citations corrected.
- P2-G: 11.1 states Q7.
- P2-H: the pointer comment link is fixed.

Since Revision 2:

Revision 2 (`37128767`) was written for the superseded stop/checkpoint protocol and was paused by [6762516530](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6762516530). It was never posted. Revision 3 replaces it in full:
- Inputs follow the redesign on `main`. They are the journal, abrupt manifest, termination receipt and successor files, not seals, cohort files or `kill-selection.json`.
- Scope is the narrow first slice of 6816117386 point 1. Revision 2's eligibility reasons, timeout-cohort classification and comparison are removed.
- New: trial wiring in `infra/scripts/phase/` and `charts/parchwell/` ([6815988248](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6815988248) point 2; 6816117386 point 3).
- Revision 2's delta-check findings F1 to F3 were never posted. They are superseded by the redesign (6816117386 point 4).

## 1. Goal and authority

Goal: the next diagnostic trial records whether each killed-and-resent message exists once or twice in `document-events`, by key, `sequence` header and value SHA-256 (6815988248; 6816117386 points 1 and 4). Trial run 43159084941 indicated a cross-restart duplicate by per-minute count only. Its records were destroyed with the ephemeral layer ([6815918816](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6815918816)).

Authority, in creation order, under docs/agents/factory.md "Item lifecycle" step 3:
- The issue body and the decisions listed in `items/404/state.md` on `main` at `08da10a66586f647dcc574ecb99fe3e79ebac538`, which covers comment 6806815301. Those decisions include ADR-021 on `main` (docs/blueprint.md lines 659 to 735), the redesign, execution parts 1 and 2, and the warm-leader change.
- [6750021224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750021224) item 2: PR3b is the verifier slice. 6816117386 point 1 narrows it for this pull request.
- [6761524200](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761524200) and [6761542603](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761542603): Revision 1 answers. Section 11.1 records which still apply.
- [6762055013](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6762055013), then 6762516530, which found 6762055013's question misframed. 6816117386 point 2 now answers it.
- [6787849492](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787849492) point 1: the trial start line keeps the two names `{run}-c1-r1-loadgen` and `{run}-c1-r1-loadgen2`, a gap against ADR-021 "Consequences" accepted "until the verifier classification pull request (PR3b) carries the three names". 6816733258 corrects the summary in 6815988248 point 2 and extends that gap (below).
- [6815212407](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6815212407): one trial, now run. It authorizes no further trial.
- 6815988248: resume and revise this plan, and wire the reader into the trial.
- 6816117386: Q1 B, Q2 A, Q3 as recorded, and the defaults.
- [6816733258](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816733258): point 1 extends the two-name gap to the later full-reconciliation pull request of `b10-abrupt-verify`, before any science; point 2 accepts LA-V2 to LA-V5 by name for the next diagnostic trial only.

Catch-up: 281 comments, 3 pages, at the last refresh. New since the state file: 6814884415, 6815212407, 6815641800, 6815918816, 6815988248, 6816117386, 6816316853 (this plan's pointer), 6816525517 (its check) and 6816733258, each read in full. No comment the state file lists was edited or deleted. Issue `updated_at` 2026-11-12T06:07:49Z. Baseline: `main` at `a5c2403271a65d0bf53d4747631ff8cebae4ec89`; since `08da10a6` it changes only AGENTS.md (one rule under "Truth and verification"), so every cited line is unchanged.

## 2. Scope

### 2.1 Files allowed to change

1. New code under `src/Parchwell.Verifier/VerifyRun/Abrupt/`. File split and private names are the implementer's choice.
2. `src/Parchwell.Verifier/Program.cs`, `VerifierHostOptions.cs` (`VerifierMode`, `VerifierModeParser`) and `VerifierHostOptionsValidator.cs`: register the mode with the options of 3.1.
3. `src/Parchwell.Verifier/VerifyRun/Input/ConsumedRecord.cs`, `VerifyRun/Window/MessageIdentityReader.cs` and `VerifyRun/Window/WindowConsumer.cs`: the optional value hash (3.2 W1). Shape: a new `WindowConsumer.Drain` overload taking an optional key set (Revision 1 Q5, decision 6761542603).
4. `src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs`, only to share its start-line read with the new mode. Its behaviour and output bytes stay unchanged.
5. New tests under `tests/Parchwell.Verifier.Tests/Component/`, each class prefixed `B10AbruptVerify`. They go there because the mutation-only build already removes `Component/**/*.cs` (`Parchwell.Verifier.Tests.csproj` line 21), so no csproj change is needed.
6. `infra/scripts/phase/b10_abrupt.py` and `infra/scripts/phase/tests/test_b10_abrupt.py`.
7. `charts/parchwell/templates/jobs.yaml`, `charts/parchwell/values.schema.json`, `charts/parchwell/tests/test_chart.py` and `charts/parchwell/tests/test_b10_abrupt_chart.py`.
8. `docs/operations/b10-abrupt-contract.md`, which owns the formats of 3.3 and 3.4. Also `docs/operations/verifier-contract.md` line 5 and `docs/operations/b10-workflow.md` lines 59 and 65, which cite that contract. Pinned text:
   - Contract line 66, last sentence, becomes: "[Decision 6787849492, point 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787849492) accepted that gap until the verifier classification pull request; [decision 6816733258, point 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6816733258) extends it to the later full-reconciliation pull request of mode `b10-abrupt-verify`, before any science."
   - verifier-contract.md line 5, first two sentences, become: "B10 abrupt diagnostic artifacts (`schema_version 3`, `dialect b10_abrupt`, `purpose diagnostic_trial`) are rejected by every verifier reader except mode `b10-abrupt-verify`, which reads them for a nonfinal diagnostic result only. The rest of the verifier support that ADR-021 requires, including the start line's three C1 Job names, is planned for the later full-reconciliation pull request of that mode under decision 6787849492, point 1, as extended by decision 6816733258, point 1."
9. `items/404/state.md` and a new receipt `items/404/pr3b-receipt.md`.

### 2.2 Paths that must not change

- Every file under `.github/` and `docs/operations/workflows/`. No workflow change is needed: the trial step runs `phase.sh run-b10-abrupt-trial` (`.github/workflows/b10-abrupt-trial.yml` line 161) before collect (line 166) and destroy (line 186).
- `infra/ephemeral/**`, `infra/persistent/**` and `infra/cluster/**`. The verifier identity already has `Azure Event Hubs Data Receiver` on `document-events` and `Storage Blob Data Reader` on the ledger container (`infra/ephemeral/identity.tf` lines 12 and 13).
- `src/Parchwell.LoadGen/**`, `tests/Parchwell.LoadGen.Tests/**` and `src/Parchwell.Kafka/**`.
- `tests/Parchwell.Verifier.Tests/Host/B10AbruptRejectionTests.cs`, which passes unchanged, and `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`.
- Every existing verifier mode's behaviour and output bytes.
- `docs/blueprint.md`. No ADR-021 amendment (6816117386 point 2).
- `docs/operations/a01-runtime-limits.json`, `scenarios/**` and `results/**`. `scenarios/B10/expected.yaml` must not be created.
- `AGENTS.md`, `CLAUDE.md`, `docs/agents/**`, any `*.csproj`, `Directory.*.props` and `*.sln*`. No new project, assembly reference or NuGet package.
- Every existing test assertion, except the three phase and chart tests named in 2.3, which are extended.

### 2.3 Protected paths, CI and existing tests

- Protected: `src/Parchwell.Verifier/**` and `tests/Parchwell.Verifier.Tests/**` match `Parchwell.Verifier*/` in `.github/CODEOWNERS`, so Hari merges. `/docs/operations/` has no owner in CODEOWNERS. `charts/` and `infra/scripts/` are unprotected (docs/agents/factory.md "Modes of work").
- Required check: `.NET verification` is the only required status check, read from the branch rules API on 2026-11-12. Its steps `./scripts/verify-dotnet.sh`, Kafka mapping coverage and worker image start must pass.
- Also must pass: the `Lifecycle verification` job. Its step "Verify lifecycle with stub commands" runs the phase suite through `infra/scripts/tests/test_phase_bridge.py`. Its step "Verify shell, workflows and rendered Kubernetes schemas" runs `charts/parchwell/tests/check.sh`, through `check-offline.sh` line 198. `Terraform verification` is unaffected. `docs-advisory` is advisory.
- Existing tests that change, each extended and none loosened:
  - `test_timeout_failure_name_is_closed` (`test_b10_abrupt.py` line 82): the set gains `b10_abrupt_verify_failed` and the count becomes 30.
  - `test_trial_runs_timeout_job_before_restart` (line 1218): the order and mode lists gain `b10-abrupt-verify` after `loadgen-successor`.
  - `test_chart.py` `VERIFIER_REQUIRED` gains the entry of 3.5.
- Existing tests that must pass unchanged: `B10AbruptRejectionTests`, `tests/Parchwell.ArchitectureTests` (including `CompiledArchitectureTests`), every other `test_b10_abrupt.py` and chart test, and `VerifierWindowComponentTests`.
- Mutation: none of `WindowConsumer`, `MessageIdentityReader` or `ConsumedRecord` is a C5 target (no match in `stryker-c5-config.json`). The new folder is not a C5 target. No mutation run is part of this pull request.

## 3. External contracts

### 3.1 Mode and options

- `Verifier:Mode` value `b10-abrupt-verify`. Every existing mode keeps its value.
- Fixed options: `ScenarioId` `B10`, `ConfigurationId` `C1`, `Phase` `C1`, `Repeat` `1`. `VerifierCommit` must equal `PARCHWELL_SOURCE_COMMIT`. Any other value exits 1 before any read, with no output object.
- Required option names: the `VERIFIER_COMMON` and `VERIFIER_REPEAT` names of `test_chart.py` lines 33 and 35, plus `IoTimeout`, `SafetyStop`, `MaxConsumedRecords`, `MaxConsumedBytes`, `MaxArtifactBytes`, `MaxTotalInputBytes`, `MaxPartitions`, `MaxInputDepth`, `StartLineInputName`, `LoadGenImage` and `VerifierImage`. `JobName`, `PodName` and `PodUid` come from the existing `PARCHWELL_*` fallbacks (`Program.cs` lines 506 to 508).
- Output: one create-only object in container `verifier` at `<RunId>/C1/r1/b10-abrupt-diagnostic/nonfinal-result.json`. There is no `WorkerReceipt`, `result.json` or `verify-completion.json` (Revision 1 Q10).
- Exit code: `0` exactly when that object was created and read back byte-identical, whatever problems it records. Otherwise nonzero (Q10).

### 3.2 Inputs and enforced checks

Inputs are fixed on `main`: `docs/operations/b10-abrupt-contract.md` lines 68 to 113 (envelope, identity, journal kinds and members) and lines 161 to 184 (termination, publication, successor), and `infra/scripts/phase/b10_abrupt.py` lines 13 to 44 and 570 to 586. A change to them is a stop-rule event.

Enforced checks, numbered. Each failure maps to the problem of 3.4 shown in brackets.
1. Options as in 3.1. Failure exits nonzero and writes no output.
2. Start line: the checks the `verify` mode applies to `PhasePaths.StartLineCompletion`, `PhasePaths.StartLine` and `PhasePaths.BrokerInventory` for `(RunId, C1, 1)`, with expected job names exactly `[{RunId}-c1-r1-loadgen, {RunId}-c1-r1-loadgen2]` (contract line 66; `RunWindow.cs` lines 48 to 67). The two names stay under 6816733258 point 1. [1]
3. Listing: list the ledger prefix `<RunId>/C1/r1/b10-abrupt-diagnostic/`. The first pod is the only directory whose `registration.json` passes check 4 with job `{RunId}-c1-r1-loadgen-restart`. Zero or more than one such directory gives [2]. Of the remaining directories, none gives [3]. Exactly one that passes check 4 with job `{RunId}-c1-r1-loadgen-successor` and predecessor equal to the first pod is the successor. Anything else gives [4]. A listing that `BoundedBlobListing` reports incomplete (`listing_incomplete`, at 1024 entries, its name-byte bound or beyond 16 pages; `BlobVerifierArtifactStore.cs` lines 57 to 84, used by `BlobProducerArtifactStore.cs` line 9) gives [2].
4. Envelope: exact members, `schema_version` 3, `dialect` `b10_abrupt`, `purpose` `diagnostic_trial`, `synthetic` true and `scientific_admissible` false. No duplicate or unknown member, no trailing content, depth at most 32. Identity has the 12 members, with `run_id` RunId, `B10`, `C1`, `C1`, repeat 1, `source_commit` VerifierCommit, `image_reference` LoadGenImage, the expected `job_name`, `pod_name` equal to the directory name and `instance_id` equal to `pod_name`. `predecessor_instance_id` is null for the first pod and the first pod's name for the successor. `pod_uid` is nonempty, equal across one pod's objects, and different between the two pods. [2] for the first pod, [4] for the successor.
5. Read limits, as `b10_abrupt.py` lines 577 to 583 set them: `journal.jsonl` 16777216, `ledger.jsonl` 268435456, `manifest.json` 67108864, `statistics.jsonl` and `request-lifecycle.jsonl` 134217728 each, report files 1048576, every other member 65536 bytes. At most 1073741824 bytes per pod. Only `request-lifecycle.jsonl` may be empty. [2] or [4]
6. First hash chain: `termination.data.manifest` binds `abrupt-manifest.json`; `manifest.journal` binds `journal.jsonl`; each `manifest.report_files` descriptor binds its file. The object set is the four first files plus the listed report names, each one of the three names at `b10_abrupt.py` lines 16 to 17. [2]
7. Termination: `termination` `intended_self_kill`, `exit_observed` true, `exit_code_local_observation` 137, `capture_gaps` [], manifest `gaps` [] and `successor_permitted` true. [2]
8. Journal: ends with byte 0x0A and has at most 4096 records. Each record is an envelope whose kind and `data` members are exactly those of contract lines 76 to 93. Sequence runs 1..n with no gap, `journal_records` equals n, and there is no `gap` record. [2]
9. Attempt binding: every `accepted`, `synchronous_rejection` and `terminal` follows its attempt's `prepared`, at most one each. Per arm, originals have ordinals 0 to 4, `sequence` equal to ordinal, key `B10/{RunId}/C1/1/{arm}` and one shared `payload_sha256`. [2]
10. Control set: recomputed under contract line 109. It equals `cut.unconfirmed_attempt_ids` and `termination.control_unconfirmed_attempt_ids` and is nonempty. Exactly one `same_instance` resend follows per member, in invocation order, with the member's key, sequence and `payload_sha256`. [2]
11. Kill set: recomputed under contract line 111. It equals `termination.kill_unconfirmed_attempt_ids` and is nonempty. The last producing-thread record (`b10_abrupt.py` lines 45 to 46) is `kill_intent`. [2]
12. Successor chain: all seven files of `b10_abrupt.py` line 18 are present. `completion.json` (kind `successor_receipt`) binds `registration` and the five artifacts, and `first_termination` and `journal` bind the first pod's `termination.json` and `journal.jsonl` bytes. `cleanup_completed` is true and `kill_unconfirmed_attempt_ids` equals check 11's set. There is one invocation per member, in order, with `predecessor_attempt_id` equal to the member, a unique `attempt_id` different from the member's, and the member's key, sequence and `payload_sha256`. [4]
13. Successor ledger: each `ledger.jsonl` row ends in 0x0A and has exactly `attempt_id`, `completion_source`, `delivered`, `error_code` and `persistence_status` (`B10AbruptProducer.cs` line 415 writes the terminal objects). There is exactly one row per invocation attempt and no other row. [4]
14. Window: high watermarks are read once at verify start for every start-line partition. The drain completes within `SafetyStop` and the consumed-record budget (`WindowConsumer.cs` lines 33 to 37; `ConsumedRecordBudget`). [5]
15. Probe record identity: a window record whose key is an arm key and that has any `MessageIdentityReader` problem, or a null value, counts as malformed. [6]

Not checked in this slice (6816117386 point 1): native-error and fatal coverage, cgroup and child identity, report leader binding, statistics and capabilities, warm-up ordering beyond check 8, the timeout cohort, registrations of other instances, eligibility and the sweep.

W1. Value hash: `ConsumedRecord` gains an optional `ValueSha256`, lowercase hexadecimal SHA-256 of the value bytes. The new `Drain` overload sets it only for records whose key is in the given set. The existing overload leaves it null. Consumed-byte accounting is unchanged.

### 3.3 Output `nonfinal_result`

Envelope: exact members `schema_version` 3, `dialect` `"b10_abrupt"`, `purpose` `"diagnostic_trial"`, `synthetic` true, `scientific_admissible` false, `kind` `"nonfinal_result"`, `identity` and `data`. Schema 3 follows ADR-021 protocol rule 14 and contract line 70. `identity` is `run_id` RunId, `scenario_id` `"B10"`, `configuration_id` `"C1"`, `phase` `"C1"`, `repeat` 1, `source_commit` VerifierCommit, `image_reference` VerifierImage, `job_name` JobName, `pod_name` PodName, `pod_uid` PodUid, `instance_id` PodName and `predecessor_instance_id` null (Q10).

`data` has exactly these members:
- `first_instance_id` and `successor_instance_id`: a string, or null when not identified and authenticated.
- `start_line` and `end_line`: arrays of `{partition, offset}` sorted by partition, or null.
- `window_complete`: boolean.
- `window_limit`: the literal `"copies_after_end_line_not_observed"` (6816117386 point 4).
- `arms`: null, or exactly two arm objects, `same_instance` then `cross_restart`.
- `problems`: distinct literals of 3.4, in that list's order.
- `problem_details`: array of `{problem, detail}`, at most `MaxDiagnosticsPerCheck` entries, each `detail` at most `MaxDiagnosticBytes` UTF-8 bytes. Detail text is the implementer's choice.
- `finality`: `"nonfinal"`.

An arm object has exactly these members:
- `arm` and `key`.
- `unconfirmed_count`: the size of check 10's or check 11's set.
- `resend_invoked`: an integer, or null for `cross_restart` without an authenticated successor.
- `originals_seen` and `resends_seen`: the number of members with at least one copy of that role.
- `pair_count`: the sum of member `pair_count`s.
- `witness`.
- `members`: in invocation order.

Each member has exactly these members:
- `original_attempt_id`, `sequence` and `payload_sha256`.
- `resend_attempt_id`.
- `resend_terminal`: `{delivered, error_code, persistence_status}` or null. For control resends it comes from the journal `terminal`. For cross-restart resends it comes from check 13's row.
- `copy_count`, `original_copies`, `resend_copies` and `other_copies`.
- `pair_count`: `original_copies` times `resend_copies` (Q12).
- `copies`: at most `MaxDiagnosticsPerCheck` entries sorted by `(partition, offset)`. Each entry is `{partition, offset, instance_id, attempt_id, role}`.
- `copies_truncated`: boolean.

A copy is a window record with the member's key, `sequence` header and `ValueSha256` equal to its `payload_sha256`. The warm-up has sequence -1, so it never matches. Copy roles:
- `original`: headers equal the first pod's instance and the member's attempt ID.
- `resend`: headers equal the authenticated resend's instance and attempt ID. That is the first pod for `same_instance` and the successor for `cross_restart`.
- `other`: anything else, with header values or null.

Witness: the smallest `(original_attempt_id, resend_attempt_id, original coordinate, resend coordinate)` over all original and resend copy pairs (ADR-021 protocol rule 13), as `{original_attempt_id, resend_attempt_id, original: {partition, offset}, resend: {partition, offset}}`, or null. Counts are JSON integers from 0 to 9223372036854775807. Counts and copies are null where the table in R4 says so, never zero for unknown.

This table and the diagnostic extras are permitted in this object only. ADR-021 protocol rule 13 binds the later final sweep projection, whose plan must drop them, with a test (6816117386 point 2).

### 3.4 Problem literals

In this order:
1. `b10_abrupt_verify_start_line_invalid`
2. `b10_abrupt_verify_first_evidence_invalid`
3. `b10_abrupt_verify_successor_absent`
4. `b10_abrupt_verify_successor_evidence_invalid`
5. `b10_abrupt_verify_window_incomplete`
6. `b10_abrupt_verify_probe_record_malformed`

### 3.5 Chart

- Mode `b10-abrupt-verify` renders one Job named `{run_id}-c1-r1-b10-abrupt-verify`, through `parchwell.jobName`'s default branch (`_helpers.tpl` line 24). Its role is `verifier`, with the verifier service account and node selector `workload`. `activeDeadlineSeconds` is `verify_job_seconds` (`jobs.yaml` line 29 gains the mode). It mounts no `b10-abrupt-binding` volume.
- Environment: the non-phase verifier branch of `jobs.yaml` lines 147 to 201, plus `Verifier__LoadGenImage` and `Verifier__VerifierImage` from the runtime images. `test_chart.py` `VERIFIER_REQUIRED['b10-abrupt-verify']` lists the names of 3.1.
- `values.schema.json`: the mode joins the enum at line 99 and the B10 enum at line 934. A new `allOf` entry requires, for this mode: `scenario_id` `B10`, `configuration_id` `C1`, `runtime.phase` `C1`, `runtime.repeat` 1, `runtime.run_id` matching `^b10d-[0-9]+-[0-9]+$`, and no `b10_abrupt_diagnostic` or `b10_abrupt_binding_configmap`. The pattern keeps the reader off scientific `b10` runs (contract line 42).

### 3.6 Phase

- New failure name `b10_abrupt_verify_failed`, added to `FAILURE_NAMES` and to the contract's list (lines 188 to 220).
- New boundaries `verify_job` and `verify_collection`.
- The state record has mode `b10-abrupt-verify`.
- Artifact file: `b10-abrupt-diagnostic/nonfinal-result.json`.

## 4. Requirements

Verifier requirements are observed at the real handler through the entry point of 3.1, reading the object it writes.

- R1. WHEN an option of 3.1 is wrong, THEN the host exits 1, reads no blob and writes no object.
- R2. WHEN every check passes, THEN `problems` is `[]`, both arms carry all members, and the counts follow 3.3.
- R3. WHEN check 2 fails, THEN problem 1 is recorded and checks 3 to 13 still run. With first evidence valid, `arms` lists members with `copy_count`, the role counts, `pair_count`, `copies`, `originals_seen`, `resends_seen` and `witness` null. `start_line`, `end_line` and `window_complete` are null, null and false.
- R4. Field rules by problem:

| Problem | `arms` | Member copies and counts | Window |
|---|---|---|---|
| 2 | null | none | not drained; `start_line` per check 2, `end_line` null, `window_complete` false |
| 3 or 4 | present | In `cross_restart`: each member's `resend_attempt_id`, `resend_terminal`, `resend_copies` and `pair_count` are null; the arm's `resend_invoked`, `resends_seen`, `pair_count` and `witness` are null; `copy_count`, `original_copies`, `other_copies`, `copies`, `copies_truncated` and `originals_seen` are computed, with successor-headed copies counted in `other_copies` | drained |
| 5 | present | all copy fields and the counts derived from them null | `window_complete` false; `end_line` as read, or null |
| 6 | present | the record is counted when its sequence and value hash parse, with null headers and role `other` | drained |

- R5. WHEN problem 2 applies, THEN problems 3 and 4 are not evaluated.
- R6. WHEN more than `MaxDiagnosticsPerCheck` copies match one member, THEN `copies` keeps the first that many by `(partition, offset)`, `copies_truncated` is true and every count stays complete.
- R7. WHEN the output object already exists, or the read-back differs, THEN the host exits nonzero.
- R8. WHEN an existing reader (`PhaseWireCodec`, `VerifierResultCodec`, `B10SweepEvidenceCodec`) reads the produced bytes, THEN it rejects them.
- R9. A window record is in scope only when `start <= offset < end` for its partition (`WindowConsumer.cs` lines 50 to 52).

Phase requirements (`b10_abrupt.py` `run()` and `collect()`):
- P1. WHEN `accept_diagnostic_publication` returned a binding, THEN after the successor step `run()` runs `b10-abrupt-verify` through `job`. That holds whether the successor step completed, failed, was unknown or raised (6816117386 point 3).
- P2. WHEN no binding was returned, which covers rejected publication, no-kill, empty kill set and unknown termination, THEN no verify Job is rendered.
- P3. WHEN the verify outcome is `failed`, `unknown`, `apply_failed` or `render_failed`, THEN the failure `b10_abrupt_verify_failed` is recorded at `verify_job`, `safety_stop` keeps its prior value and `run()` returns 1, as recorded failures already do (lines 921 to 926). WHEN the outcome is `complete`, THEN `run()`'s return value is the one it would have had without the verify step.
- P4. WHEN `collect()` sees a verify record with outcome `complete`, THEN it downloads the output object of 3.1 from container `verifier`, at most 16777216 bytes, to the artifact file of 3.6. A missing or ambiguous download records `b10_abrupt_verify_failed` at `verify_collection`. The phase makes no content check.
- P5. WHEN `collect()` sees any applied verify record, THEN it runs `collect-job` for it and copies `artifacts/<job>/`, as for the three producer Jobs.
- P6. The verify Job does not change the timeout, restart or successor paths, their failures or their ordering.

## 5. Invariants and safety

- No producer Job starts after the verify Job. The verify Job is a consumer, so `safety_stop` semantics for producers are unchanged.
- Cleanup, upload, delete and destroy keep their `always()` conditions. The workflow is unchanged.
- The output is `scientific_admissible` false with `finality` `nonfinal`. No reader of science accepts it (R8). The chart pattern keeps the mode off `b10` runs.
- No workflow dispatch, trial, `results/` or expected-outcome change (6816117386, closing line).
- No Azure credentials in any test. Kafka tests use the existing `LocalKafkaFixture`.

## 6. Test map

Verifier component tests. They use the existing `LocalKafkaFixture` (topic `document-events`) and in-memory doubles of the ledger and verifier stores at the existing `IProducerArtifactStore` (lists and reads the ledger container; registered at `Program.cs` lines 215 to 219) and `IVerifierArtifactStore` seams. Two evidence sources:
- E: real bytes from `B10AbruptWireProducerTests` run through `PARCHWELL_B10_ABRUPT_WIRE_OUTPUT`, as `B10AbruptRejectionTests.ProduceExportAsync` does. Run ID `synthetic`, pods `synthetic-first` and `synthetic-second`.
- B: a test builder that writes the same formats with chosen payload bytes. E carries hashes only, so copies need B. T0 pins B to E.

| Test | Inputs | Expected |
|---|---|---|
| T0 parity | E and B | Per kind, the envelope, identity and `data` member-name sets are equal, and so are the successor ledger row members |
| T1 real bytes (R2) | E, empty window | `problems` []. `same_instance` members equal `termination.control_unconfirmed_attempt_ids`; `cross_restart` members equal `kill_unconfirmed_attempt_ids`. Resend IDs equal the invocations. Every `copy_count` 0, `pair_count` 0, `witness` null. Exit 0, read-back identical |
| T2 cross pair | B, kill set `[c4]` seq 4. Records: (p0,o10) first/c4; (p0,o12) successor/r4 | `copy_count` 2, `original_copies` 1, `resend_copies` 1, `other_copies` 0, `pair_count` 1, witness `{c4, r4, {0,10}, {0,12}}`, arm `originals_seen` 1, `resends_seen` 1 |
| T3 original absent | B, only (p0,o12) successor/r4 | `copy_count` 1, `original_copies` 0, `pair_count` 0, witness null |
| T4 product (Q12) | B, two originals (p1,o5) and (p0,o9), one resend (p0,o12) | `pair_count` 2, witness original `{0,9}` |
| T5 control pair, exclusions | B. Control member s4: original (p2,o3), resend (p2,o4). Same key: warm-up seq -1, confirmed original seq 3 | `same_instance` `pair_count` 1. Warm-up and seq 3 are not copies |
| T6 successor absent | B, no successor directory, plus record successor/r4 | `problems` [3]. The R4 row for 3 or 4, field by field: member `resend_attempt_id`, `resend_terminal`, `resend_copies`, `pair_count` null; arm `resend_invoked`, `resends_seen`, `pair_count`, `witness` null; `copy_count` 2, `original_copies` 1, `other_copies` 1, `originals_seen` 1 |
| T7 successor invalid | B, one change per subcase: completion `journal` hash; invocation count 2 for a 1-member set; ledger row missing; predecessor wrong; extra file | `problems` [4], the same literals as T6, `successor_instance_id` null |
| T8 first invalid | B, one change per subcase: class `no_kill`; exit 136; kill set differs from termination list with the chain rehashed; sequence gap; manifest hash; image differs from LoadGenImage; two restart directories; a `journal.jsonl` of 16777217 bytes; a journal of 4097 records; an `accepted` whose attempt has no earlier `prepared`; `cut.unconfirmed_attempt_ids` without its first member. Each is rehashed so that only the named predicate fails. The receipt records the rest of the predicate map (below, at and above each boundary) | `problems` [2], `arms` null, `end_line` null, `window_complete` false, exit 0 |
| T9 start line invalid | B, completion receipt missing | `problems` [1], members present, copy fields null |
| T10 window incomplete | B, 2 in-window records, `MaxConsumedRecords` 1; at the boundary, budget 2 | `problems` [5] with copy fields null; at budget 2, `problems` [] |
| T11 malformed | B, a copy with a duplicate `attempt-id` header; another arm-key record with a null value | `problems` [6]. The first is counted with role `other` and `attempt_id` null; the second is not a copy |
| T12 window bounds (R9) | B, records at start-1, start, end-1, end | Only start and end-1 are counted |
| T13 options (R1) | each of ScenarioId `B04`, ConfigurationId `C0`, Phase `C0`, Repeat `2`, VerifierCommit mismatch | Exit code 1, no store read, no object |
| T14 output (R7, R8) | T2 output; then a pre-existing object | Envelope literals of 3.3. The existing readers reject the bytes. A second run with the object present exits nonzero |
| T15 copies cap (R6) | B, 101 matching copies, `MaxDiagnosticsPerCheck` 100 | 100 entries, `copies_truncated` true, `copy_count` 101 |
| T16 value hash (W1) | Records with key k (value "abc"), key j, key k with null value | New overload with {k}: SHA-256 `ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad`, then null, then null. The old overload gives null for all three |

Phase tests (`test_b10_abrupt.py`):
- TP1 (P1): the extended `test_trial_runs_timeout_job_before_restart` has order `[..., 'loadgen-successor', 'b10-abrupt-verify']` and `run()` returns 0.
- TP2 (P1): successor outcomes `failed` and `unknown` and a raised `b10_abrupt_successor_late` each apply the verify Job. The gaps equal the existing expectation and nothing more.
- TP3 (P2): a rejected first publication, as in `test_failed_publication_blocks_successor_in_real_trial`, records no `b10-abrupt-verify` mode.
- TP4 (P3): each of `failed`, `unknown`, `apply_failed` and `render_failed` gives gap `['b10_abrupt_verify_failed']` at `verify_job`, `safety_stop` False before and after, and return 1.
- TP5 (P4, P5): a complete record downloads from `verifier` at the 3.1 path with maximum 16777216 and runs `collect-job`. A None download gives the gap at `verify_collection`.
- TP6: the extended `test_timeout_failure_name_is_closed` has 30 names.

Chart tests:
- TC1: a rendered `b10-abrupt-verify` gives one Job with the name and deadline 750 of 3.5, role `verifier`, no binding volume, and env names that include `VERIFIER_REQUIRED['b10-abrupt-verify']` and are all known properties.
- TC2: each of scenario `B04`, configuration `C0`, phase `C0`, repeat 2, run_id `b10-1-1`, and `b10_abrupt_diagnostic` true fails rendering.

## 7. Deployment assumptions (live-only)

"Cold" means the cluster and the ephemeral layer were started in that run.

| ID | Assumption | Evidence |
|---|---|---|
| LA-V1 | The verifier identity can list and read the abrupt ledger prefix | Accepted by name in 6761524200 point 4 (same container and prefix). Never exercised live in this mode |
| LA-V2 | The verifier pod can assign and consume `document-events` over the private endpoint | Accepted by name, next trial only (6816733258 point 2). Cold: the B10 start-line Job read metadata and watermarks in run 43159084941. A01 final run 40227770436 at source `fa417d78` (`results/A01/2026-10-25.md` line 11) recorded 930000 `ok` outcomes (line 26); `ok` means delivered and seen in the topic (`docs/blueprint.md` line 397). That the A01 `verify` Jobs consumed `document-events` live is inferred from those outcomes. Earlier source, other mode and scenario |
| LA-V3 | The trial's records remain readable when the verify Job runs | Accepted by name, next trial only (6816733258 point 2). `message_retention = 7` (`infra/ephemeral/eventhubs.tf` line 15). Run 43159084941's trial step ended 05:25:47Z and destroy started 05:26:06Z (jobs API), so the verify Job runs before destroy. No live read yet |
| LA-V4 | A resend reported delivered before the successor Job ends is below the high watermark read afterwards | Accepted by name, next trial only (6816733258 point 2). Inferred, not verified on Event Hubs. If false, `resend_copies` undercounts, and `window_limit` states the limit |
| LA-V5 | The verify Job finishes within 750 s | Accepted by name, next trial only (6816733258 point 2). Not measured for this mode. The A01 verify Jobs of run 40227770436 each read a window of 186000 attempts (`results/A01/2026-10-25.md` line 17) and completed within that run's verify Job deadline, inferred from the valid repeats; this plan did not read that deadline's value at that source. If exceeded, the Job fails, `b10_abrupt_verify_failed` is recorded and cleanup still runs |

LA-V2 to LA-V5 have no offline test. Their acceptance covers the next diagnostic trial only.

## 8. Acceptance commands

| Command | Evidence |
|---|---|
| `./scripts/verify-dotnet.sh` | `Verification passed.`; the per-project reports include T0 to T16 |
| `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` | `OK`; includes TP1 to TP6 |
| `bash infra/scripts/lifecycle/check-offline.sh` | exit 0; includes TC1 and TC2 |
| `npm run check:docs` | each warning fixed or explained (docs/agents/pull-requests.md rule 8) |
| `git diff --name-only origin/main...HEAD` | a subset of 2.1, checked by script against 2.2 |

## 9. Caps and plan-check answers

Caps per implementing session: 20 verification-failure root causes and 20 review-finding root causes. Exhaustion: `push -wip and hand off to the next session` (6816117386 point 4).

(a) Steps whose effort scales with a number unknown at planning time:
- Window records: bounded by `MaxConsumedRecords` 372000 and `MaxConsumedBytes` (a01-runtime-limits.json lines 67 and 68). Check 14 and problem 5 apply.
- Copies per member: capped at `MaxDiagnosticsPerCheck` 100 (R6).
- Journal records: at most 4096 (check 8).
- Ledger listing: at most 1024 entries, its name-byte bound and 16 pages (`BlobVerifierArtifactStore.cs` lines 57 to 84); beyond that, problem 2 (check 3).
- Implementer root causes: the caps above.

(b) Where this plan is stricter than its parent (item plan [Revision 5](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6705594392) and ADR-021):
- The chart restricts the mode to `b10d-` run IDs (3.5), so a diagnostic reader cannot run on a scientific run.
- It is narrower, not stricter, under 6816117386 point 1: no eligibility.

(c) Tests that depend on code, data or behaviour outside `main` and this pull request: none offline. E is produced by `main`'s LoadGen test code. B is pinned to E by T0. Live behaviour is LA-V2 to LA-V5.

## 10. Sources

- ADR-021 protocol rules 6, 7, 10, 13 and 14 and classification (docs/blueprint.md lines 686 to 719 on `main` at `08da10a6`).
- `docs/operations/b10-abrupt-contract.md` lines 42 to 66, 68 to 113, 161 to 222.
- `infra/scripts/phase/b10_abrupt.py` lines 13 to 44, 570 to 586, 677 to 857, 882 to 1015.
- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProducer.cs` lines 121, 252 to 259, 343 to 353, 410 to 416, 530 to 541.
- `src/Parchwell.Verifier/VerifyRun/Window/WindowConsumer.cs`, `MessageIdentityReader.cs`, `RunWindow.cs`, and `VerifyRunHandler.cs` lines 84 to 186.
- `charts/parchwell/templates/jobs.yaml` lines 13 to 29 and 140 to 245, `_helpers.tpl` lines 5 to 25, `values.schema.json` lines 95 to 110 and 645 to 660 and 925 to 945.
- `infra/ephemeral/identity.tf` lines 12 to 14 and `infra/ephemeral/eventhubs.tf` line 15.
- `.github/workflows/b10-abrupt-trial.yml` lines 158 to 197.
- Trial evidence [6815641800](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6815641800) and 6815918816. Actions jobs API for run 43159084941, read 2026-11-12.

## 11. Open questions for Hari

None.

### 11.1 Earlier answers (decision 6761542603 rule)

- Still apply, with unchanged choices: Q1 (Codex), Q5 (Drain overload with key set), Q10 (mode name, output path, exit rule, envelope identity, no `WorkerReceipt`) and Q12 (pair product, smallest witness). Under Q10, `schema_version` becomes 3, as ADR-021 protocol rule 14 and contract line 70 require. That is not a choice.
- Void, because their subject is outside this slice (6816117386 point 1): Q2 (eligibility reasons), Q3 (eligibility values), Q6 (selected-original counting) and Q8 (native-error eligibility).
- Void, because their subject is gone: Q9 (the contract sentence no longer exists), Q11 (no native-error export is needed) and Q4 (now LA-V1, accepted).
- Q7 applies in part: check 6 verifies each bound report's hash and length; no statistics content or guard result is read in this slice; guard re-evaluation stays required before any scientific admission.
- Revision 2's Q13 is answered by 6816117386 point 2.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change:
- a contract of section 3 or a requirement of section 4;
- the scope of section 2;
- a pinned input of 3.2, including a LoadGen or phase format change on `main`.

Hitting a section 9 cap pushes `-wip` and hands off to the next session.

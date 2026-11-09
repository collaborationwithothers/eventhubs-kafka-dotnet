# PR3a evidence completeness contract-grade plan

Revision 1. Issue 404, pull request short name `PR3a evidence completeness`. Implementing tool: Codex, as `items/404/state.md` records.

This pull request makes the first B10 abrupt producer publish the cohort evidence the verifier must reconcile on its own, and adds fatal-error capture to the supervisor journal. It changes the load generator, the trial phase script, their tests and the abrupt contract; it adds no verifier logic. Two points need Hari before approval: the extra uploads push the configured worst-case first-pod budget sum past the Job deadline (Q2), and the produced-byte export list sits in one protected verifier test (Q1).

## 1. Goal and authority

Goal: after a B10 C1 abrupt first pod is killed, the ledger container holds every object the per-repeat verifier (PR3b) needs to check the timeout and control cohorts independently, and the journal carries every native error callback the first child saw through the end of its event stream.

Authority, in creation order (docs/agents/factory.md "Item lifecycle" step 3):
- Issue 404 body.
- [6705801800](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6705801800) point 3: "Fatal-error capture is included in this work, so the verifier can tell an intended kill from a crash. No claim of zero fatal errors is allowed until it exists."
- [6708991894](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6708991894): PR3 "needs its own plan, rechecked against merged predecessors. Hari merges every pull request here."
- docs/blueprint.md ADR-012 (B10 exceptions), ADR-021 protocol rules 4, 9, 12, 13 and 14 and decision rule 6, at `main` `f5cc496fa3cf845e68e400b5b65e557f121dd928`.
- The decisions listed in `items/404/state.md` "Decisions in force".
- [6749565231](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749565231): the successor fix and the verifier are planned in parallel; both plans carry a never-ran inventory.
- [6749995101](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749995101): the next step aims at a one-repeat diagnostic observation; "not a verdict, not a published result, and not an eligibility finding".
- [6750021224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750021224) item 1: "PR3a, abrupt evidence completeness. Load generator, phase script and contract docs: publish the timeout and control cohort ledgers, inventories and post-drain reports and both arm pre-acceptance reports, and add fatal capture to the journal. Predecessor: the successor fix."

Catch-up: state file covers comment 6746177383. Comments read after it: 6747016131 (completion report, not a decision), 6749565231, 6749995101 and 6750021224 (decisions, read in full). Snapshot before writing: 142 comments, issue `updated_at` 2026-11-09T19:59:55Z. The scope record for the whole verifier work is `items/404/plan-verifier.md` at `12adae761f268dbf20842f7c568c82000263422b`; this plan does not revise it.

Predecessor: the successor fix (decision 6749565231 item 1), planned in parallel by another session and not merged when this revision was written. This plan is pinned against `main` at `f5cc496f`. Section 3.3 names the files both plans may touch; section 14's stop rule covers a successor-fix change to them.

## 2. Problem, with evidence

- The first instance publishes exactly seven objects: `registration.json, journal.jsonl, timeout-seal.json, control-seal.json, kill-selection.json, abrupt-manifest.json, termination.json` (`src/Parchwell.LoadGen/Run/Abrupt/B10AbruptPublisher.cs:35`).
- The two cohort seals bind three local files each by `{name,length,sha256}`: `inventory` (`manifest.json`), `ledger` (`ledger.jsonl`) and `post_drain_statistics` (`post-drain-report.json`), in `<OutputRoot>/timeout/` and `<OutputRoot>/control/` (`B10AbruptProducer.cs:234-246` and `SealProbeCohortAsync`, lines 525-557). None is published. Trial 7 descriptors (`timeout-seal.json`): ledger 26441293 bytes, inventory 9635312 bytes, post-drain report 58764 bytes; (`control-seal.json`): inventory 2639, ledger 942, post-drain report 57119.
- Each probe arm's checkpoint binds `abrupt-child/<arm>-pre-acceptance-report.json` (`docs/operations/b10-abrupt-contract.md` "Wire and state machine", `checkpoint`). Neither report is published. In trial 7 the same-instance report has the same length and SHA-256 as the timeout post-drain report (58764, `a4a0f813...`), and the cross-restart report the same as the control post-drain report (57119, `de41bc24...`), read by script from the journal checkpoints and seals. Two published objects can therefore hold identical bytes; binding is by role, not by hash alone.
- The supervisor already reads and checks all six cohort files when it loads each seal (`B10AbruptSupervisor.LoadSealAsync`, lines 603-652), and it reads each pre-acceptance report when it binds the report message (`AcceptReportAsync`, lines 379-408). It discards the bytes afterwards.
- The first child's error callback goes only to `RunEvidenceSession.CaptureError` (`B10AbruptHost.cs:233`), which writes `capture/request-lifecycle.jsonl` in the child's local directory through a background writer. That file is not published; the supervisor reads it once after EOF to fail the guard on an error-level row (`CheckCapturedErrorsAsync`, lines 655-667). Rows still buffered when the child is killed are lost. Every first-instance object carries `fatal_capture_status: "not_implemented_pr3"` (trial 7 `termination.json`, both seals).

## 3. Scope

### 3.1 Files allowed to change

- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptPublisher.cs`, `B10AbruptSupervisor.cs`, `B10AbruptWire.cs`, `B10AbruptChildChannel.cs`, `B10AbruptJournal.cs`, `B10AbruptHost.cs`, `B10AbruptProducer.cs`, `B10AbruptDiagnostics.cs` (new fact names only).
- `tests/Parchwell.LoadGen.Tests/Abrupt/*.cs` (new test classes and changes to existing assertions listed in section 8).
- `infra/scripts/phase/b10_abrupt.py`, `infra/scripts/phase/tests/test_b10_abrupt.py`, `infra/scripts/phase/tests/check_b10_abrupt_producers.py`.
- `docs/operations/b10-abrupt-contract.md`.
- `tests/Parchwell.Verifier.Tests/Host/B10AbruptRejectionTests.cs`, `ExportNames` array only, if Hari answers Q1 with option (i). This file is protected.
- `items/404/state.md` and the receipt `items/404/pr3a-receipt.md`.

### 3.2 Paths that must not change

Everything under `src/Parchwell.Verifier/`, every other file under `tests/Parchwell.Verifier.Tests/`, `.github/workflows/**`, `docs/operations/workflows/**`, `charts/**`, `scenarios/**`, any `expected.yaml`, `results/**`, `docs/blueprint.md`, `docs/agents/**`, `AGENTS.md`, `CLAUDE.md`, `README.md`, any `*.csproj`, `Directory.*.props`, `*.sln*`, `src/Parchwell.LoadGen/Run/Evidence/**`, `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptStatistics.cs`, `OwnedProducerProcess.cs`, `B10AbruptCgroup.cs`, `B10AbruptProc.cs`, `B10AbruptOptions.cs`. No new project, assembly reference or NuGet package.

### 3.3 Files the successor fix may also touch

From decision 6749565231 item 1 ("completes statistics while the producer is still live, then disposes it, then completes evidence, and adds completion diagnostics to the guard facts"), inferred, not read from its plan, which was not on the branch when this revision was written: `B10AbruptHost.cs` (`CreateProducerAsync` evidence completion), `B10AbruptProducer.cs` (`RunSuccessorAsync`, `CleanupAsync`), `B10AbruptDiagnostics.cs` (`B10AbruptFact` members), `docs/operations/b10-abrupt-contract.md`, `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProducerTests.cs`, `B10AbruptWireProducerTests.cs`. PR3a changes the first five of these in different members: `B10AbruptHost.CreateProducerAsync` error handler only; `B10AbruptProducer` timeout and control seal objects only; `B10AbruptDiagnostics` added facts only; the contract's wire, publication and outcome-label text only.

## 4. External contracts

All names below are exact. Any change needs a plan revision (stop rule).

### 4.1 Published first-instance members

`B10AbruptPublisher.FirstMembers` becomes these 15 names, in this order, published in this order after `registration.json`, `termination.json` last:

`registration.json`, `journal.jsonl`, `timeout-seal.json`, `control-seal.json`, `timeout-manifest.json`, `timeout-ledger.jsonl`, `timeout-post-drain-report.json`, `control-manifest.json`, `control-ledger.jsonl`, `control-post-drain-report.json`, `same_instance-pre-acceptance-report.json`, `cross_restart-pre-acceptance-report.json`, `kill-selection.json`, `abrupt-manifest.json`, `termination.json`.

Container `ledger`, prefix `<run_id>/C1/r1/b10-abrupt-diagnostic/<pod_name>/`, unchanged. The eight new members ("cohort files") and their sources:

| Published name | Local source under `OutputRoot` | Bound by | Read limit (bytes) |
|---|---|---|---|
| `timeout-manifest.json` | `timeout/manifest.json` | `timeout-seal.json` `data.inventory` | 67108864 |
| `timeout-ledger.jsonl` | `timeout/ledger.jsonl` | `timeout-seal.json` `data.ledger` | 268435456 |
| `timeout-post-drain-report.json` | `timeout/post-drain-report.json` | `timeout-seal.json` `data.post_drain_statistics` | 1048576 |
| `control-manifest.json` | `control/manifest.json` | `control-seal.json` `data.inventory` | 67108864 |
| `control-ledger.jsonl` | `control/ledger.jsonl` | `control-seal.json` `data.ledger` | 268435456 |
| `control-post-drain-report.json` | `control/post-drain-report.json` | `control-seal.json` `data.post_drain_statistics` | 1048576 |
| `same_instance-pre-acceptance-report.json` | `abrupt-child/same_instance-pre-acceptance-report.json` | journal `checkpoint` frame with `arm` `same_instance`, `data.pre_acceptance_report` | 1048576 |
| `cross_restart-pre-acceptance-report.json` | `abrupt-child/cross_restart-pre-acceptance-report.json` | journal `checkpoint` frame with `arm` `cross_restart`, `data.pre_acceptance_report` | 1048576 |

The read limits are the ones the supervisor already applies to these files (`LoadSealAsync` lines 618-622; `AcceptReportAsync` line 401). The binding descriptors keep their existing `name` values; the match is on `length` and `sha256` under the role mapping above.

### 4.2 `abrupt_manifest` member `cohort_files`

`abrupt_manifest` data gains exactly one member, `cohort_files`: a JSON array of exactly eight descriptors `{name,length,sha256}`, in the table order of 4.1, each `name` the published name, `length` the byte count, `sha256` lowercase hex of the published bytes. Data members become `journal, journal_entry_count, timeout_seal, control_seal, selection, gaps, registration, cohort_files`.

### 4.3 `native_error` record

New IPC message kind and journal frame kind `native_error`, child to supervisor, not acknowledged. Data exact members:

| Member | JSON type | Source and bound |
|---|---|---|
| `capture_sequence` | integer | Child counter: first callback 1, each later callback previous plus 1, checked arithmetic; 1 to 9223372036854775807 |
| `error_code` | integer | `(int)Error.Code`, Int32 range |
| `error_name` | string | `Error.Code.ToString()`, 1 to 128 characters |
| `is_error` | boolean | `Error.IsError` |
| `is_fatal` | boolean | `Error.IsFatal` |
| `callback_monotonic_ticks` | integer | The child's monotonic timestamp taken in the callback, 0 to 9223372036854775807 |

No reason text, broker name or other free text is carried. The journal frame's `data` equals the IPC `data`; the supervisor assigns `sequence`, `observed_utc` and `monotonic_ticks` as for every frame.

### 4.4 Fatal coverage fields

- `cohort_seal`: `fatal_capture_status` is exactly `journal`; new member `native_error_count` (integer, 0 to 9223372036854775807) equals the highest `capture_sequence` the child had assigned when it built the seal object, 0 when none.
- `termination`: `fatal_capture_status` is exactly `journal_through_eof`; new members `native_error_count` and `native_fatal_count` (integers) equal the number of `native_error` frames in the sealed journal and the number of those with `is_fatal` true. `native_tail` stays `unknown_due_to_intended_kill`.
- `failed_diagnostic`: `fatal_capture_status` is exactly `journal`; no member is added.
- `capabilities` (successor fallback object, `B10AbruptProducer.cs:511-515`): unchanged, `not_implemented_pr3`. The successor keeps ordinary capture (ADR-021 rule 10, "Second producer retains ordinary registration/drain/ledger/manifest/completion").

### 4.5 Phase script

- `FIRST_FILES` is unchanged (seven names): it is the pre-release acceptance set. New tuple `COHORT_FILES` holds the eight cohort-file names in 4.1 order.
- `DATA_FIELDS` gains `native_error` with the members of 4.3, and the `cohort_seal`, `abrupt_manifest` and `termination` entries gain the members of 4.2 and 4.4.
- New phase-status boundary literal `first_cohort_publication`.
- The predecessor binding written to `predecessor-binding.json` and mounted for the successor keeps exactly its current `files` members: the six names of `FIRST_FILES` without `journal.jsonl`.

## 5. Requirements

Publication:
- R1. WHEN the supervisor publishes the first instance, THEN it publishes the 15 members of 4.1 in that order, and each cohort file's published bytes are the local file's bytes whose `length` and `sha256` equal the binding descriptor in 4.1. WHEN a local cohort file is missing, unreadable, over its read limit, or differs in length or SHA-256 from its binding descriptor at publication time, THEN publication fails with the existing `b10_abrupt_publication_failed`, no later member is published, and `termination.json` is not published.
- R2. WHEN `abrupt-manifest.json` is built, THEN it carries `cohort_files` exactly as 4.2, and `termination.json` binds it by its existing `manifest` descriptor.
- R3. WHEN the successor loads its binding (`B10AbruptPublisher.LoadSelectionAsync`), THEN the accepted `files` member set is unchanged: the six names of `FIRST_FILES` without `journal.jsonl`. A binding that also carries any cohort file is rejected with the existing `b10_abrupt_successor_binding_invalid:receipt_changed`.
- R4. WHEN `b10_abrupt.py` accepts the first publication before successor release, THEN it downloads only `FIRST_FILES`, as today, and additionally requires: `cohort_files` has exactly eight entries with the names of `COHORT_FILES` in order; each entry's `length` is an integer within the 4.1 read limit and equals the length in its binding descriptor; each entry's `sha256` equals its binding descriptor's `sha256`. Failure raises `ValueError` at boundary `first_publication` with failure name `b10_abrupt_publication_failed`, and no successor is released.
- R5. WHEN `b10_abrupt.py collect()` runs and `predecessor-binding.json` exists, THEN, before it handles any `loadgen2` record, it downloads the eight `COHORT_FILES` from the first pod's prefix with the 4.1 limits into the first pod's collection directory, and requires each to match its `cohort_files` descriptor. A missing object records `b10_abrupt_publication_ambiguous`; a mismatch records `b10_abrupt_publication_failed`; both at boundary `first_cohort_publication`, and collection continues with the successor and Job collection as today.

Fatal capture:
- R6. WHEN the first child's producer error callback fires, THEN the callback assigns the next `capture_sequence` and enqueues one `native_error` message without blocking and without file or network I/O, as one step that a seal's count read cannot split. The existing `RunEvidenceSession.CaptureError` call stays. WHEN the enqueue fails, THEN the channel latches the existing gap `queue_full`. The successor and the supervisor process do not install this path.
- R7. WHEN the supervisor receives `native_error`, before or after the kill request, THEN it validates the members of 4.3 and appends the frame to the journal. WHEN `capture_sequence` is not exactly the previous one plus 1 (the first must be 1), THEN it latches gap `sequence_gap` and raises the existing `b10_abrupt_capture_gap`. Receiving `native_error` adds no other failure; existing guard checks are unchanged.
- R8. WHEN the supervisor loads a cohort seal, THEN it requires `fatal_capture_status` `journal` and that the journal already holds `native_error` frames with `capture_sequence` 1 to `native_error_count`. Otherwise it raises the existing `b10_abrupt_control_incomplete`.
- R9. WHEN the supervisor builds `termination.json`, THEN `fatal_capture_status`, `native_error_count` and `native_fatal_count` are as 4.4, counted from the sealed journal.
- R10. WHEN `b10_abrupt.py` accepts the first publication, THEN it also requires: the journal's `native_error` frames have `capture_sequence` 1 to n in journal order; `termination` `native_error_count` equals n and `native_fatal_count` equals the number with `is_fatal` true; both seals have `fatal_capture_status` `journal` and `native_error_count` at most n; `termination` `fatal_capture_status` is `journal_through_eof`. Failure as R4.

Contract:
- R11. `docs/operations/b10-abrupt-contract.md` states 4.1 to 4.5 once, in "Wire and state machine" and the publication text, replaces the sentence "Fatal coverage beyond existing ordinary capture is explicitly unavailable until PR3." with one stating what the journal now covers (every first-child error callback through event-stream EOF; the native tail after the kill remains unknown), and changes the outcome-labels line to the literals of 4.4. It claims no absence of fatal errors.

## 6. Invariants and safety

- No change to any protocol sequence, gate, policy value, failure name, verdict rule, timeout or ADR text. The only new literals are those in section 4. Q2 asks Hari about the budget arithmetic; PR3a changes no budget.
- No change to pre-release timing work: the eight cohort files are not downloaded before successor release (R4, R5).
- The successor's mounted binding does not grow (R3); a ConfigMap carrying the 26441293-byte trial 7 timeout ledger would not be possible.
- The timeout cohort's `timeout-ledger.jsonl` and `timeout-manifest.json` are schema-one ordinary bytes. They are published only under `b10-abrupt-diagnostic/`, never under `producer/`, and carry no index or schema-one completion receipt. The ordinary verifier path lists only `producer/` (`VerifyRunHandler.cs:41,47`), so no reader admits them; PR3a changes no verifier code.
- Verdict integrity, hard safety rules: no trial, dispatch, Azure access, `expected.yaml` or `results/` change.
- Missing evidence is absent, never zero: a native error count is published only from a sealed journal with no gap.

## 7. Never-ran inventory

Evidence: the seven trial runs and artifact zips recorded in `items/404/plan-verifier.md` section 7 (trial 7: run 42679791413, artifact 13704155185, zip SHA-256 `bee17231882ffeb7ddc3134ba10a00ec4590f20e96dad409c050decf1f22cdcf`, source `f5cc496f`). Log lines are from the trial 7 first-pod container log in that artifact.

| Stage | Ran live | Evidence | Test in this plan that runs it for real |
|---|---|---|---|
| E1 Child writes timeout and control cohort files and seals | Yes, trial 7 | Child `"stage":"cohort_sealed","facts":{"arm":0,"entry_count":31240}` and `{"arm":1,"entry_count":6}` | Unchanged; exercised by T1 through the real `LocalLedgerStore` |
| E2 Supervisor loads seals and checks cohort files | Yes, trial 7 | Supervisor `"stage":"seal_loaded","facts":{"cohort":0,"seal_count":31240}` and `{"cohort":1,"seal_count":6}` | T1, T8 |
| E3 Child writes pre-acceptance reports; supervisor binds them | Yes, trial 7 | Supervisor `"stage":"frame_report","facts":{"arm":1,"arm_failed":0}` and `{"arm":2,"arm_failed":0}` | T1 |
| E4 Publication of the seven existing members | Yes, trial 7 | Supervisor `"stage":"publication_complete"`; seven objects in the artifact | T1 (real publisher, file storage) |
| E5 Publication of the eight cohort files | No; not implemented | No such object in any artifact | T1, T2 with the real publisher and file storage. Blob upload of these sizes is deployment assumption DA1 |
| E6 `abrupt-manifest.json` `cohort_files` | No; not implemented | Trial 7 `abrupt-manifest.json` has no such member | T1, T4 |
| E7 Python pre-release check of `cohort_files` | No; not implemented | Trial 7 acceptance ran (U3 of the scope record) without it | T4 on produced bytes through the real `accept_diagnostic_publication` |
| E8 Python post-run download of cohort files | No; not implemented | No line shows it | T5 with the real `collect()` and the existing stubbed blob download |
| E9 Error callback to channel enqueue in the child | No; not implemented | No `native_error` record exists; trial 7 shows no error callback at all (guard passed) | T6: real Confluent.Kafka producer and a real native fatal error |
| E10 `native_error` through the real pipe | No | No line shows it | T6: real anonymous pipe and real `B10AbruptChildChannel` |
| E11 Supervisor journal append of `native_error`, before and after kill | No | No line shows it | T6 (real `B10AbruptSupervisor.HandleAsync`, real `B10AbruptJournal` on disk), T7 |
| E12 Seal coverage check | No | No line shows it | T8 |
| E13 Termination fatal coverage counts | No | Trial 7 `termination.json` has `not_implemented_pr3` | T10, T1 |
| E14 Native fatal error delivered by the deployed producer | No line shows it | No fatal error occurred in any trial | T6 raises a real librdkafka fatal error offline; broker-caused delivery on the pinned image is DA2 |

Fixtures: PR3a uses produced bytes from the existing export (`B10AbruptWireProducerTests`, acceptance A3) and fresh files written by its own tests. It commits no trial artifact.

## 8. Test map

New test classes: `B10AbruptFatalCaptureTests` and `B10AbruptPublicationTests` in `tests/Parchwell.LoadGen.Tests/Abrupt/`. Existing assertions that change, each to the new literal, none removed or loosened: `B10AbruptSupervisorTests.cs:832`, `926`, `1320` and `B10AbruptProducerTests.cs:713` (`not_implemented_pr3` becomes `journal` or `journal_through_eof` as 4.4); `B10AbruptWireProducerTests.cs:390` `Assert.Equal(14, exported.Length)` becomes 22. `B10AbruptProducerTests.cs:635` (`B10AbruptStatistics.SealCohort`, no production caller) stays.

- T1 (R1, R2, R9). Produced-byte export (`PARCHWELL_B10_ABRUPT_WIRE_OUTPUT`, real `B10AbruptSupervisor`, real `B10AbruptPublisher` with file storage, real `LocalLedgerStore`). Expected: export has 22 files: the 15 `first/` names of 4.1 and the 7 unchanged `successor/` names. For each cohort file, `length` and `sha256` equal its binding descriptor. `cohort_files` names equal the 4.1 list in order. `termination.json` `fatal_capture_status` is `journal_through_eof`, `native_error_count` 0 and `native_fatal_count` 0 (the export's producer raises no error callback); both seals `fatal_capture_status` `journal`, `native_error_count` 0.
- T2 (R1). One byte of the local `timeout/ledger.jsonl` changed after the timeout seal is written and before publication: `b10_abrupt_publication_failed`; storage holds no `abrupt-manifest.json` and no `termination.json`. Same with `abrupt-child/cross_restart-pre-acceptance-report.json` changed after the kill checkpoint. Same with `control/manifest.json` deleted.
- T3 (R3). Binding with the six names loads; the same binding plus `timeout-ledger.jsonl` fails with `b10_abrupt_successor_binding_invalid:receipt_changed`. Python: `predecessor-binding.json` `files` keys equal `['abrupt-manifest.json', 'control-seal.json', 'kill-selection.json', 'registration.json', 'termination.json', 'timeout-seal.json']` when sorted.
- T4 (R4, R10). `ProducedBytesCases` on the new export: acceptance passes. Each mutation, applied alone with all other bytes valid and the termination descriptor rebound so only the targeted check can fail, raises `ValueError` and records `b10_abrupt_publication_failed` at `first_publication`: `cohort_files` with seven entries; two entries swapped; one `length` plus 1; one `sha256` with one hex digit changed; termination `fatal_capture_status` `not_implemented_pr3`; termination `native_error_count` plus 1; a seal `native_error_count` greater than the journal count. A further case adds two journal `native_error` frames with `capture_sequence` 1 and 3 (journal re-sealed and rebound): fails.
- T5 (R5). `collect()` with stubbed downloads returning the produced cohort files: the eight files land in the first pod's directory and no failure is recorded. One file with one byte changed: one `b10_abrupt_publication_failed` at `first_cohort_publication`. One download returning nothing: one `b10_abrupt_publication_ambiguous` at `first_cohort_publication`. In both failure cases the successor collection still runs.
- T6 (R6, R7). `B10AbruptFatalCaptureTests`: a real `Confluent.Kafka` 2.16.0 producer, `EnableIdempotence = true` and no `bootstrap.servers`, built with the same error-handler wiring the host uses; a real anonymous pipe pair; a real `B10AbruptChildChannel`; a real `B10AbruptSupervisor.HandleAsync`; a real `B10AbruptJournal` on disk. The test reads the producer's `Handle` non-public property `LibrdkafkaHandle` by reflection, resolves `rd_kafka_test_fatal_error` from the librdkafka file the process loaded (found in `/proc/self/maps`), and calls it with error 45 and reason `offline test`. Expected: return 0; the journal file holds exactly one `native_error` frame with `capture_sequence` 1, `error_code` 45, `error_name` `OutOfOrderSequenceNumber`, `is_error` true, `is_fatal` true, and members exactly as 4.3. Scratch reproduction in section 10, A10.
- T7 (R7). Supervisor receives `native_error` with `capture_sequence` 1 then 3: gap `sequence_gap` latched and `b10_abrupt_capture_gap`. A `native_error` with `capture_sequence` 1 handled with `afterKill: true` is journaled.
- T8 (R8). Real `LoadSealAsync` on seal files on disk: timeout seal `native_error_count` 1 with no `native_error` frame journaled: `b10_abrupt_control_incomplete`; the same after one frame with `capture_sequence` 1 is journaled: `seal_loaded`. Seal with `fatal_capture_status` `not_implemented_pr3`: `b10_abrupt_control_incomplete`.
- T9 (R6). 1000 error callbacks on 8 threads while another thread repeatedly builds a seal count and then enqueues a marker message: for every marker, the channel's written stream holds `native_error` frames 1 to the read count before the marker. Expected: 0 violations, and `capture_sequence` values 1 to 1000 each written once.
- T10 (R9). Supervisor run with two `native_error` frames before the kill, one with `is_fatal` true, and one after the kill: `termination.json` `native_error_count` 3, `native_fatal_count` 1, `fatal_capture_status` `journal_through_eof`, `native_tail` `unknown_due_to_intended_kill`.
- T11 (R6). A `native_error` enqueue with the channel's 256-message queue full latches `queue_full`; the next written message is `capture_gap` with reason `queue_full`.
- T12. `check_b10_abrupt_producers.py` on the new export prints exactly `b10_abrupt_produced_bytes_checked`.
- T13. `B10AbruptRejectionTests` passes with the export of T1 (Q1).

## 9. Deployment assumptions

Each blocks approval until Hari accepts it by name or names a test (factory.md "Item lifecycle" steps 1 and 3); all are under "Open questions for Hari".
- DA1. The pod's blob upload of each cohort file completes within the existing 30 s per-upload budget. Trial 7 sizes: 36311952 bytes in total for the eight files, the largest 26441293. Policy limits allow 268435456 per object and 1073741824 in aggregate (`B10AbruptPublisher.PublishAsync`). One live observation: the existing post-selection uploads took 159 ms from `selection_finalized` to `publication_complete` in trial 7 for objects totalling 53981 bytes, computed by script from the supervisor log; that is not a measurement for the larger files.
- DA2. A fatal error that the broker causes in the deployed producer reaches the error handler as T6's fabricated one does. T6 uses librdkafka's documented test hook ("this method allows an application to trigger fabricated fatal errors in tests to check its error handling code", `src/rdkafka.h` at v2.16.0, lines 852-866). Offline evidence: Ubuntu 24.04 with .NET runtime 10.0.13 loaded the `centos8-librdkafka.so` variant; the LoadGen image base is `mcr.microsoft.com/dotnet/aspnet:10.0.13-noble` (`Parchwell.LoadGen.csproj:8`). Which variant the image loads is not verified.
- DA3. The added upload time before first-pod completion leaves the successor ready before the 120 s release boundary. Not measured.

## 10. Acceptance commands

Each command was run in this planning session on a clean worktree of `main` at `f5cc496f`, .NET SDK 10.0.402, a local Docker daemon, Ubuntu 24.04. Observed output on `main` is quoted, with the expected output after PR3a.

- A0 build: `dotnet restore Parchwell.slnx --locked-mode && dotnet build Parchwell.slnx --configuration Release --no-restore`. Observed: `Build succeeded.` `0 Warning(s)` `0 Error(s)`. After: the same.
- A1 abrupt LoadGen tests:
  ```
  dotnet publish src/Parchwell.LoadGen/Parchwell.LoadGen.csproj --configuration Release --no-build --no-restore --output <publish directory>
  PARCHWELL_B10_ABRUPT_PUBLISHED_APPHOST=<publish directory>/Parchwell.LoadGen dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj -c Release --no-build -- --filter-class "*B10Abrupt*"
  ```
  Observed: `total: 457` `failed: 0` `skipped: 0`, exit 0. After: exit 0, `failed: 0`, `skipped: 0`, total 457 plus the new test methods.
- A2 new fatal-capture tests: `dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj -c Release --no-build -- --filter-class "*B10AbruptFatal*"`. Observed: zero tests, `Test run completed with non-success exit code: 8`. After: exit 0, `failed: 0`, `skipped: 0`.
- A3 produced-byte export and checker:
  ```
  PARCHWELL_B10_ABRUPT_WIRE_OUTPUT=<new empty directory> dotnet tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptWireProducerTests
  python3 infra/scripts/phase/tests/check_b10_abrupt_producers.py <that directory>
  ```
  Observed: `Total: 8, Errors: 0, Failed: 0, Skipped: 0`; 14 files plus `export.json`; checker `Ran 12 tests` `OK` then `b10_abrupt_produced_bytes_checked`, exit 0. After: 22 files plus `export.json`; checker exit 0 with the same final line.
- A4 phase script tests: `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_b10_abrupt.py'`. Observed: `Ran 12 tests` `OK`. After: `OK`, more than 12 tests.
- A5 verifier rejection tests: `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --no-build -- --filter-class "*B10Abrupt*"`. Observed: `total: 2` `failed: 0`. After: the same.
- A6 architecture: `PARCHWELL_REPOSITORY_ROOT=$PWD dotnet test --project tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj -c Release --no-build -- --filter-class "*B10Abrupt*"`. Observed: `total: 1` `failed: 0`. Without the variable it fails with "Run ./scripts/verify-dotnet.sh to identify the repository root."; that is not a pass. After: the same pass.
- A7 documentation: `npm ci --ignore-scripts && npm run check:docs` and `git diff --check`. Observed: `8 warnings`, in `docs/agents/factory.md`, `docs/operations/evidence/issue-124/README.md`, `issue-140/README.md` and `issue-211/receipt.md`, none in a file PR3a changes. After: no warning in a changed file.
- A8 remaining placeholder literal: `grep -rn not_implemented_pr3 src/Parchwell.LoadGen infra/scripts/phase/b10_abrupt.py`. Observed on `main`: 9 lines. After: exactly 3, in `B10AbruptWire.cs` (the `capabilities` check), `B10AbruptProducer.cs` (successor fallback `capabilities`) and `B10AbruptStatistics.cs` (`SealCohort`).
- A9 budget arithmetic (the contract's own script with `uploads` 6 and 14): observed `6 1030 130 110` and `14 1270 370 -130`, meaning worst case, excess over the 900 s lifetime, margin below the 1140 s Job deadline.
- A10 offline fatal reproduction (scratch program, not committed; `Program.cs` SHA-256 `9a089406f4c85d1b62f4496a17fda7851daeacff534363caa62495ca668578db`, project SHA-256 `20351c8a80ac19d8c6492ab651cf2f6c5e5d2cf04c8c36e7ed45679091c95774`, run three times with no `bootstrap.servers`). Observed each time: `loaded=centos8-librdkafka.so`, `rc=0 member=property getter_public=False type=Confluent.Kafka.Impl.SafeKafkaHandle`, then the error handler's only callback `code=45 name=OutOfOrderSequenceNumber is_error=True is_fatal=True`. With `bootstrap.servers` `127.0.0.1:1` the handler also received `-195 Local_Transport` and `-187 Local_AllBrokersDown` (not fatal), so T6 sets no bootstrap server.

## 11. Plan-check questions

(a) Runtime-sized effort: none new. T9 is fixed at 1000 callbacks on 8 threads; the export has a fixed member count. Disposition caps from the item plan apply: 20 documentation-warning, 20 verification-failure and 20 review-finding root causes, exhaustion `push -wip and hand off to the next session`.

(b) Stricter than the parent: the never-ran inventory and real-input tests (decision 6749565231); T6's real native fatal error, where the item plan required only production-byte synthetic tests.

(c) Dependencies neither on `main` nor in the pull request: the successor fix (section 3.3, stop rule); DA1 to DA3.

## 12. Sources

- docs/blueprint.md ADR-012, ADR-021 at `f5cc496f`; docs/operations/b10-abrupt-contract.md at `f5cc496f`.
- Code at `f5cc496f`: `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptPublisher.cs`, `B10AbruptSupervisor.cs` (lines 82-160, 290-408, 603-667), `B10AbruptProducer.cs` (lines 225-247, 384-405, 443-557), `B10AbruptHost.cs` (lines 40-94, 210-250), `B10AbruptChildChannel.cs`, `B10AbruptWire.cs` (lines 34-56, 170-240), `B10AbruptJournal.cs`; `infra/scripts/phase/b10_abrupt.py`; `infra/scripts/phase/tests/test_b10_abrupt.py`, `check_b10_abrupt_producers.py`; `tests/Parchwell.Verifier.Tests/Host/B10AbruptRejectionTests.cs` (lines 24-30, 115-136); `charts/parchwell/tests/test_b10_abrupt_host.py` (lines 96-109); `src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs` (lines 41, 47).
- librdkafka `src/rdkafka.h` at tag v2.16.0, `rd_kafka_test_fatal_error`, lines 851-868, read 2026-11-09.
- Trial artifacts as section 7.

## 13. Open questions for Hari

- Q1. Protected test file. The produced-byte export is checked by `tests/Parchwell.Verifier.Tests/Host/B10AbruptRejectionTests.cs`, which requires the export directory to hold exactly 14 named files (lines 24-30, 115-125). Decision 6750021224 lists PR3a as load generator, phase script and contract docs. Options: (i) PR3a also changes that file's `ExportNames` array to the 22 names, a protected path that Hari merges (recommended: one list, and the export keeps carrying every published member); (ii) the export writes the cohort files to a second directory named by a new environment variable, so the protected file is untouched, at the cost of test plumbing that the scope record's PR3b must then also read.
- Q2. Budget arithmetic. Eight more 30 s upload budgets raise the configured first-pod worst-case sum from 1030 s to 1270 s, 130 s past the 1140 s Job deadline (A9). The sum is policy arithmetic, not a measured duration, and smaller enclosing deadlines already win. Options: (i) accept, and record the new sum in the contract as the existing text records 1030 s; (ii) give the eight cohort files one shared upload budget, a policy change; (iii) another choice. PR3a changes no budget until Hari decides.
- Q3. DA1 (cohort file upload within 30 s each), DA2 (broker-caused fatal delivery like the test hook, and the image's librdkafka variant) and DA3 (successor still ready before release): accept each by name or name a test.
- Q4. T6 reads a non-public Confluent.Kafka property (`Handle.LibrdkafkaHandle`) by reflection and calls librdkafka's test hook. AGENTS.md "Engineering standards" puts seams "at our own boundaries, not around Confluent.Kafka"; this adds no seam in production code, but the test depends on an internal member of the pinned 2.16.0 package. Accept this test technique, or require another.

## 14. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change anything in section 4, a requirement in section 5 or the scope in section 3. It also stops when the merged successor fix has changed, in a file of section 3.3, any member that PR3a changes, `FirstMembers`, `SuccessorMembers`, the seal, manifest or termination objects, or the predecessor binding; this plan is then revised and rechecked against it before implementation. The disposition caps in section 11 stop with their stated exhaustion action.

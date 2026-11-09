# Successor fix contract-grade plan

Revision 3. Issue #404. Implementing tool: Claude Code, under [standing decision 6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614), point 3, replacing Codex for this pull request. Base: `main` at `f5cc496fa3cf845e68e400b5b65e557f121dd928`, the source of diagnostic trial 7.

The successor producer in trial 7 disposed its native client before it asked the statistics session to complete. No statistics report could then arrive after the completion boundary, so the evidence guard failed. This pull request completes statistics while the client is still live, then disposes it, then completes evidence. It also gives the guard one fact per statistics diagnostic code the session can emit, which raises the per-site fact cap from 16 to 35 (decision 6751449502). It changes no protocol sequence, gate, timeout, interval, failure name or wire member, touches no protected path, and authorizes no trial.

This plan states requirements and tests as observable behaviour. Code text, member names, private structure and line numbers are the implementer's. Line numbers below cite `main` only to locate today's behaviour. A technique named in a test is a permitted example unless the row says it is required.

### Changes since Revision 2 (commit `18301978`)

From the fresh delta reproduction check relayed by the #347 control session, and from Hari's decisions on 2026-11-09:

- B1: T1 no longer asserts a non-existent `expectations` member. It asserts `resolved_configuration` is null instead.
- m1: no path is protected (`docs/operations/` is agent-owned); the merge-rule wording is corrected.
- m2: the failure-report sizes are recomputed.
- m3: `invalid_broker_series_limit` cannot be emitted through the session. It is dropped, giving 31 flags and a cap of 35; the reachability evidence is in E2.
- m4: T2 and T5 now assert the capture-directory capabilities. Mutation X2 is killed; X1 survives and is covered by acceptance command 13.
- m5: every mutation is defined precisely, including code-string swap variants.
- m6: the R8 coverage statement is corrected.
- m7: the 11(c) wording and the T9 timings are updated.
- m8: T7's report `client_id` is pinned.
- m9: the facts seam is added to the "left open" list.
- m10: F5 to F14 are itemised below.
- Requirements are rewritten as behaviour, per the control session's instruction.
- Decision 6752430614 makes Claude Code the implementing tool.
- Decisions 6752059755 and 6752078378 move both D6 races to their own pull requests, which answers Q3.
- Decisions 6752454732 and 6752554218 put this pull request first in the order.

### Changes in Revision 2 (commit `18301978`), from the check of Revision 1 (commit `d5b119a3`)

- F1: T8 added for R7.
- F2 and decision 6751449502: one flag per code, a raised cap, and the contract line updated.
- F3: D6 rewritten.
- F4: T9 added, with a real librdkafka client.
- F5: acceptance 1 expects exit 0.
- F6: acceptance 11 runs with Docker.
- F7: the acceptance 4 grep pattern and case count are pinned, and T4 moves into the gate's class.
- F8: T4 asserts the capture lifecycle file and the missing `error_capture_unavailable`.
- F9: the test clock wraps `TimeProvider.System` and uses floor microseconds.
- F10: T1's capabilities literal is pinned.
- F11: T7's session options are pinned and its free-running feed removed.
- F12: the slack is now a range across the observed lags.
- F13: the seal-to-exit bound is derived, and its D2 inference added.
- F14: R2 and R3 are merged, and R8 and R9 rewritten as WHEN/THEN.
- F15: never-ran inventory rows added.

## 1. Goal and authority

Goal: the minimum change that lets the B10 abrupt successor complete its evidence and write its five publication payloads. Every changed stage is tested against real statistics, evidence and adapter objects and, where it runs offline, a real librdkafka client.

Authority, in creation order:

- Issue body of #404.
- The decisions listed in `items/404/state.md` "Decisions in force", up to the [burst test-gap approval 6746177383](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6746177383). None of them changes the successor completion order.
- [Decision 6749565231](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749565231), option 2:
  - this pull request "completes statistics while the producer is still live, then disposes it, then completes evidence, and adds completion diagnostics to the guard facts";
  - the plan carries a never-ran inventory;
  - a further trial needs Hari's new explicit approval and gate approval.
- [Decision 6749995101](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749995101) and [decision 6750021224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750021224): PR3a, PR3b and PR3c come "each after the successor fix".
- Decisions [6750864341](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750864341), [6750879393](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750879393), [6750891952](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750891952), [6750940329](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750940329) and [6751703425](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6751703425) govern PR3a only. [Decision 6753263792](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6753263792), made under standing rule 1, governs the contention race-fix plan only.
- [Decision 6751199288](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6751199288), option 1: "Keep both values: statistics interval 15 s and successor completion timeout 20 s."
- [Decision 6751449502](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6751449502), option 3:
  - one flag for "every diagnostic code `KafkaStatisticsSession` can emit", parser and snapshot-validation codes included;
  - the code list, fact order and cap are pinned, and the cap is "a policy choice with no measured basis";
  - "no other site's facts change"; the other caps are unchanged.
- [Decision 6752059755](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752059755): the capture-loss race between closing capture and the completion seal is fixed in its own pull request. This plan "stays as scoped, does not change `src/Parchwell.Kafka`, and names the race-fix pull request as the fix for this race".
- [Decision 6752078378](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752078378): the `B10AbruptStatistics` lock-contention race (guard reason 3) is fixed in its own pull request too.
- [Standing decision 6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614):
  - point 1, the control session may decide narrow open questions;
  - point 2, standing plan approval;
  - point 3, Claude Code implements;
  - point 4, merging.
- [Decision 6752454732](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752454732): this pull request is first in the order.
- [Decision 6752554218](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752554218): review re-request, and stacked implementation of later pull requests.
- Evidence, not authority: [Codex P1 finding r4505115034 on PR 406](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/406#discussion_r4505115034). Native error callbacks during successor disposal must be captured before evidence is finalized. Commit `54d9065` fixed that finding by disposing before both completions, and that move caused the trial 7 failure. This plan keeps the finding fixed (R5).

Design text this plan depends on and does not change:

- docs/blueprint.md line 708 (ADR-021 item 5): "Timeout/control and successor retain ordinary post-drain coverage"; "`statistics.interval.ms` stays unchanged".
- docs/operations/b10-abrupt-contract.md line 51 (timeout and control post-drain checks do not call the ordinary completion) and line 160 (a fact is "a closed snake_case name and a 64-bit integer").

Design text this plan changes: that file's line 188, the cap number only (E5).

## 2. Scope

### 2.1 Files allowed to change

1. `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProducer.cs`
2. `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs`
3. `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptDiagnostics.cs`: new facts for E2, and the per-site cap (E4). Nothing else.
4. `docs/operations/b10-abrupt-contract.md`: line 188 only, as E5 pins.
5. `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProducerTests.cs`: the existing disposal-callback test moves out (it becomes T4); the hand-written completion delegate and its disposal counter go if nothing else uses them.
6. `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptWireProducerTests.cs`: the successor half of the exporter (T7).
7. `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptDiagnosticsTests.cs`: the existing cap test (T11).
8. New file `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptSuccessorCompletionTests.cs`: T1, T2, T4, T5, T6, T8, T9 and T10.
9. `items/404/state.md` (factory.md "Item state file") and the new receipt `items/404/successor-fix-receipt.md`.

No protected path is touched: `docs/operations/` is agent-owned (factory.md "Ruleset": "`/docs/` is protected except `docs/operations/`, which agents own"; `.github/CODEOWNERS` agrees). AGENTS.md GOVERNANCE "Merge rule" applies.

No new source file under `src/`. The failure-site architecture test scans a fixed list of 12 source files, so the new guard site must be in a file on that list (`tests/Parchwell.ArchitectureTests/B10AbruptFailureSiteTests.cs`).

### 2.2 Paths and values that must not change

Paths: everything not in 2.1, in particular:

- `src/Parchwell.Kafka/**` (decision 6752059755) and `src/Parchwell.LoadGen/Run/Evidence/**`;
- `B10AbruptStatistics.cs` (decision 6752078378 gives its race its own pull request), `B10AbruptPublisher.cs` and `B10AbruptSupervisor.cs`;
- `infra/**`, `charts/**`, `scenarios/**`, `results/**`, `.github/**`;
- every other `docs/**` file;
- `AGENTS.md`, `CLAUDE.md`, any `*.csproj`, `Directory.*.props` and `*.sln*`.

No new project, assembly reference or NuGet package.

Values (decision 6751199288 and decision 6751449502):

| Value | Setting on `main` |
|---|---|
| Statistics completion timeout | 20 s |
| Evidence completion timeout | 20 s |
| Producer cleanup timeout | 10 s |
| Successor statistics completion budget | 20 s |
| Host cleanup | 10 s |
| Process lifetime and watchdog | 900 s |
| `statistics.interval.ms` | 15000 |
| Post-drain poll | 10 ms |
| Chain entries per diagnostic | 8 |
| Facts per timeline entry | 8 |
| Timeline entries per process | 48 |
| Failure report limit | 65536 bytes |

## 3. External contracts

E1. Failure name. When, at evidence completion, statistics completion is incomplete or the evidence session is faulted, the successor fails with the existing closed name `b10_abrupt_guard_failed`, the message of an `InvalidDataException`. There is no new failure name. The failure-line format is unchanged.

E2. Guard facts at that failure site: exactly 35 facts, in this order, each a 64-bit integer. The names are pinned; how the implementer names its enum members is not.

| # | Fact name | Value |
|---|---|---|
| 1 | `completion_complete` | 1 when the statistics completion is complete, else 0 |
| 2 | `evidence_faulted` | 1 when the evidence session is faulted after its completion, else 0 |
| 3 | `missing_broker_count` | number of expected brokers without a qualifying post-boundary report |
| 4 | `diagnostic_count` | number of distinct diagnostic codes in the completion |
| 5 to 35 | `diagnostic_<code>` | 1 when the completion's diagnostics contain `<code>`, else 0, for the 31 codes below in this order |

The 31 codes are every code the session can emit. A planning-session script enumerated them, and the fresh check's independent enumeration matched exactly. They are listed in order of first appearance: `KafkaStatisticsSession.cs` ("session" lines) first, then `KafkaStatisticsParser.cs` ("parser" lines). Parser codes reach the session's set at session line 358.

| Fact | Code | Emitted at |
|---|---|---|
| 5 | `clock_unverified` | session 74 |
| 6 | `late_capture` | session 97, session 117 |
| 7 | `capture_contention` | session 110 |
| 8 | `oversized_snapshot` | session 123 |
| 9 | `capture_queue_overflow` | session 129 |
| 10 | `disposed_before_completion` | session 200 |
| 11 | `completion_cancelled` | session 243, 260, 270, 637 |
| 12 | `sink_drain_timeout` | session 244, 261, 307 |
| 13 | `completion_timeout` | session 244, 261, 642 |
| 14 | `disposed_during_completion` | session 289 |
| 15 | `abandoned_without_completion` | session 294 |
| 16 | `processing_failure` | session 332 |
| 17 | `metrics_recording_failure` | session 391 |
| 18 | `evidence_delivery_cancelled` | session 428 |
| 19 | `evidence_sink_failure` | session 436 |
| 20 | `client_id_mismatch` | session 451 |
| 21 | `client_type_mismatch` | session 457 |
| 22 | `client_name_changed` | session 463 |
| 23 | `non_increasing_timestamp` | session 469 |
| 24 | `client_instance_changed` | session 477 |
| 25 | `late_attachment` | session 486 |
| 26 | `future_native_timestamp` | session 493 |
| 27 | `broker_series_limit_exceeded` | session 519, parser 79 |
| 28 | `counter_reset` | session 540 |
| 29 | `capture_loss` | session 632 |
| 30 | `worker_cancellation_failure` | session 733, 751 |
| 31 | `invalid_json` | parser 27, parser 42 |
| 32 | `duplicate_property` | parser 50 |
| 33 | `invalid_root` | parser 55 |
| 34 | `invalid_or_missing_field` | parser 65, 70, 112 |
| 35 | `broker_name_mismatch` | parser 128 |

Excluded codes:

- The literal `consumer` at session 448 is a client type, not a code.
- `invalid_broker_series_limit` (parser 32) cannot be emitted through the session, for three reasons:
  - the session constructor rejects invalid options (session lines 61-63);
  - option validation rejects `MaxBrokerSeries` at or below 0 (`KafkaStatisticsOptions.cs` lines 32-35);
  - the bridge factory passes a validated private copy.
  If that ever changes, the code still appears in `diagnostic_count` (reading rule below). `invalid_json` stays, because parser line 42 is reachable.

Reading rule, stated in this plan and the receipt only: `diagnostic_count` minus the sum of facts 5 to 35 counts codes not in this list.

E3. Successor outputs are unchanged: the five payload names `ledger.jsonl`, `manifest.json`, `statistics.jsonl`, `request-lifecycle.jsonl` and `capabilities.json`, the `SuccessorReceipt` members, the stage names and the timeline format. There is no new stage. No Kafka client setting is added or changed.

E4. The per-site fact cap is 35, up from 16. This is a policy choice with no measured basis (decision 6751449502): 4 summary facts plus 31 codes, the smallest cap that keeps E2 whole. It applies to every abrupt failure site, and no other site's facts change.

E5. docs/operations/b10-abrupt-contract.md line 188. Only its opening words change.

- Before: `Policy caps, not measurements: 8 chain entries, 16 facts per site, 8 facts per timeline entry`
- After: `Policy caps, not measurements: 8 chain entries, 35 facts per site ([decision 6751449502](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6751449502)), 8 facts per timeline entry`

The rest of the line is byte-identical.

## 4. Requirements

Behaviour of the successor process on the runtime producer and statistics session that the host builds.

R1. WHEN the successor has sealed its `cross_restart` cohort (`cohort_sealed` written), THEN it requests statistics completion exactly once. It passes the broker inventory from the predecessor binding, and the native producer has not yet been disposed.

R2. WHEN that statistics completion returns, complete or not, THEN the successor disposes the native producer exactly once within the existing cleanup timeout. Only after disposal returns does it complete the evidence session exactly once with that completion. The evidence session's `capabilities.json` is therefore written in every case where statistics completion returned.

R3. WHEN, after R2, the completion is incomplete or the evidence session is faulted, THEN the successor fails with E1 and exactly the E2 facts. It writes none of `statistics.jsonl`, `request-lifecycle.jsonl` or `capabilities.json` under `<OutputRoot>/successor/`.

R4. WHEN, after R2, the completion is complete and evidence is not faulted, THEN the existing behaviour follows unchanged: the guard checks, the copy of the three evidence artifacts into `successor/` (read limit 134217728 bytes), the guard checks again, and the successor artifacts returned to the host.

R5. WHEN a native error callback arrives during disposal, THEN the evidence session captures it, with no `error_capture_unavailable`. For an error other than `NoError`, the successor then fails with E1 and `native_error_observed` 1.

R6. WHEN a statistics callback arrives after statistics completion and before or during disposal, THEN the successor still succeeds. The completion is unchanged, evidence is not faulted, and the B10 statistics adapter raises no guard.

R7. WHEN statistics completion throws, THEN the same exception propagates from the successor run. The producer is disposed exactly once, and the evidence session is not completed, so no `capabilities.json` exists in the capture directory.

R8. WHEN the first instance runs, including its timeout and control cohorts, THEN it requests neither statistics completion nor evidence completion (docs/operations/b10-abrupt-contract.md line 51). Coverage: acceptance command 12 only. The existing tests `Given_TimeoutInDrain_When_RunningCohort_Then_RetriesOnceWithSeparateInventory`, `Given_CompletedControlOriginal_When_ReleaseArrives_Then_ResendsOnceAndAdmitsKillWithoutCleanup` and `Given_ControlArm_When_Sealed_Then_InventoryHasSixEntriesWithMemberSequences` show only that the cohorts still run.

R9. WHEN a successor producer is built without completion behaviour (test construction only), THEN it writes the test-only successor files as it does on `main`. Covered by the existing `Given_SuccessorEntrySequence_When_Bound_Then_InBurstOnly` and `Given_SealedKillSelection_When_Replaying_Then_InvokesOnceWithoutRetryAndDrains`.

R10. WHEN the native client delivers a statistics report, THEN the host first passes it to the B10 statistics adapter's native-capture tracking, then to the statistics session. The host and T1 to T9 use the same production handler for this. No test can observe the order without relying on timing (mutation X1), so acceptance command 13 prints the handler for review.

R11. WHEN a failure site attaches up to 35 distinct facts, THEN all are kept in order, and a 36th distinct fact is dropped.

## 5. Invariants and safety

- I1. On the successor path the order is statistics completion, then disposal, then evidence completion, then guard. The precedent is `RunExecutor.cs` on `main`: it completes while live at lines 368 and 388, disposes at line 419, and completes evidence at line 438.
- I2. Statistics completion happens at most once per session: a second request throws (`KafkaStatisticsSession.cs` lines 187-190).
- I3. AGENTS.md hard safety rules hold: no Azure credential, no workflow dispatch, no `.github/` change, no trial. Verdict integrity holds: nothing under `results/`, and no expected outcome changes.
- I4. No number from section 8 enters code or docs.

## 6. Never-ran inventory

Evidence is the trial logs, not the code. Seven B10 abrupt diagnostic runs exist (the run list of `b10-abrupt-trial.yml`, read 2026-11-09). Each artifact was downloaded with `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/<id>/zip` into an empty scratch directory and treated as untrusted data. Both fresh checks reproduced all seven zip hashes.

| Run | Source | Artifact | Zip SHA-256 | Successor job started |
|---|---|---|---|---|
| [42460966764](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42460966764) | `2d4792ee` | 13609836611 | `bbc4030f30fceef33c156c7b00d8533342c7415887b9d59afab7f52db68a78e7` | no |
| [42475024856](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42475024856) | `b4d6f9e8` | 13615377394 | `270597b9f683416aa72ef5230e90bf896973ac7b3fb9c9030bcbbcd1f6a75c83` | no |
| [42512105140](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42512105140) | `25cc301a` | 13629179730 | `a5b9c345121eecfc9b836706101ee776cbd3269d57e4150a04a4a632917ef9ad` | no |
| [42536075365](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42536075365) | `ee36f1c6` | 13639442741 | `0564049fa591fdfcdd8d4cfbac36794d43498cac72aeccec12666e800979af69` | no |
| [42608381540](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42608381540) | `4163cd1a` | 13668809132 | `0c22924e47ed6b455e228f98be5016c6e3c97faab541d9ed2f9853bf1005eb30` | no |
| [42670600125](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42670600125) | `fa73aa08` | 13699980854 | `b2e2c270f6133c86341eafd1aa5c95a4f0c391a9ecce2b6134707d19a4341bd1` | no |
| [42679791413](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42679791413) (trial 7) | `f5cc496f` | 13704155185 | `bee17231882ffeb7ddc3134ba10a00ec4590f20e96dad409c050decf1f22cdcf` | yes |

"Successor job started" is no where an artifact has no `loadgen2` directory and no `"role":"successor"` line (checked by script). Run 42670600125 is not linked from issue #404; it is listed because the workflow ran it.

Trial 7's successor log is `b10-42679791413-1-c1-r1-loadgen2/b10-42679791413-1-c1-r1-loadgen2-prrgv.containers.loadgen.log`, SHA-256 `fce88054fccf70591da6f334b7299e446a7ec66cd7b6814fec8740cd17e7209f`. "L<n>" is a line in that log.

| Stage on the successor path | Ran live? Evidence | Test in this plan that runs it for real |
|---|---|---|
| S1. Predecessor binding load and validation in the host | Yes, trial 7 only. L25 `producer_constructed` follows it | Not changed. Existing production-binding cases in the wire-producer tests |
| S2. Selected-entry validation in the producer (`B10AbruptProducer.cs` lines 449-466) | Yes: L25 follows it, because `producer_constructed` is marked after these checks (line 104) | Not changed. Existing `Given_SuccessorEntrySequence_When_Bound_Then_InBurstOnly` |
| S3. Successor registration upload | Yes: artifact file `b10-42679791413-1-c1-r1-loadgen2-prrgv/registration.json` | Not changed |
| S4. Producer and statistics session construction | Yes: L25 | T1 to T10 build a real session. T9 builds a real librdkafka client |
| S5. Release wait | Yes: L51 `release_reached` | T1 |
| S6. Resend produce and terminal | Yes: L52 `probe_terminal` | T1 (fake native producer) |
| S7. Drain | Yes: L53 `cohort_drained` | T1 |
| S8. Post-drain report wait in the B10 adapter | Yes: L54 `post_drain_report`, after 10896 ms, with 7 reports. Inference: the adapter accepted a live report covering the expected brokers, with a native timestamp above the drain boundary. That is live evidence for D1's report content | T1 |
| S9. Guard checks before the seal (line 484) | Yes: L55 follows them | Not changed |
| S10. Cohort seal | Yes: L55 `cohort_sealed` | T1 |
| S11. Statistics completion while live (changed) | Never. Trial 7 completed only after disposal. L56 shows the result, `completion_complete:0`, at `B10AbruptHost.cs` line 249 | T1 (complete), T2 (timeout), T8 (throws), T9 (real client, complete while live) |
| S12. Disposal after statistics completion (changed position) | Never in this position. Disposal before completion ran in trial 7 (code order), but no line records it | T1, T4, T5 (fake `Dispose`). T9: a real client disposed after completion |
| S13. Evidence completion after disposal (changed position) | Once in trial 7, with an incomplete completion. L56 was thrown after host line 248 completed evidence (inferred from code order) | T1, T2, T4, T5, T9 |
| S14. Guard with the E2 facts (changed) | The old two-fact guard ran: L56 `{"completion_complete":0,"evidence_faulted":0}`. The new facts never ran | T2, T5, T6, T10 |
| S15. Guard checks after evidence completion (line 491) | Never | T4 `Local_Fail` |
| S16. Copy of the three evidence artifacts into `successor/` | Never | T1, T7 |
| S17. Failure report | Yes: L56 is its output | Not changed |
| S18. Production wiring inside the host's producer construction: completion behaviour, broker inventory, statistics handler | Never. No offline test builds the host runtime with a native client | Partly: T1 to T9 use the same production handler and completion behaviour, and T9 drives them from a real client. That the host passes exactly these, with the inventory it reads, is deployment assumption D7 |
| S19. Host cleanup: session and evidence disposal, host stop and dispose within 10 s (lines 95-101) | Never: trial 7 failed before it | Partly: T1 disposes the real session and evidence after a completed run. Host stop and dispose: D5 |
| S20. Read of successor payloads and successor publication (lines 102-106) | Never. `phase-status.json` records `b10_abrupt_publication_ambiguous` at boundaries `successor_job` (`b10_abrupt.py` line 591) and `successor_publication` (line 394) | T7 runs the real publisher against the test file storage. The blob upload itself: D3 |
| S21. Successor process exit 0 and Job completion | Never: `loadgen2-pods.json` shows exit code 1, started 19:20:51Z, finished 19:22:58Z | D4 |
| S22. Workflow download of `SUCCESSOR_FILES` | Ran and failed (S20). The same `download` succeeded live for `FIRST_FILES` (trial 7 reached boundary `successor_admission`, `b10_abrupt.py` lines 583-587) | D4 |
| S23. Workflow successor receipt validation (`b10_abrupt.py` lines 604-633) | Never | T7, then `check_b10_abrupt_producers.py`, whose `test_produced_first_and_successor_transport` calls `collect` on the produced bytes |

Why the existing tests missed S11:

- Every successor test on `main` that supplies completion behaviour uses a hand-written delegate. It completes evidence with no statistics session (`B10AbruptProducerTests.cs` lines 1289-1294).
- The produced-byte exporter supplies none, so it takes the test-only branch (`B10AbruptWireProducerTests.cs` lines 371-373). Its exported successor `statistics.jsonl` and `capabilities.json` are therefore not production evidence files.

## 7. Test map

### 7.1 Common inputs

All tests are in `tests/Parchwell.LoadGen.Tests/Abrupt/`, and names follow docs/engineering/dotnet.md line 62.

"Real chain" (required) means all of the following:

- a real `RunEvidenceSession`, with the options at `B10AbruptHost.cs` line 218 and no expectations;
- a real `B10AbruptStatistics`, with native-capture tracking active and that evidence session downstream;
- a real `RunEvidenceSinkRouter` attached to it;
- a real `KafkaStatisticsSession` from `KafkaStatisticsBridgeFactory` with native clock compatibility, and a completion-boundary observer where a test needs one;
- the production completion behaviour and the production statistics handler that the host uses.

T1 to T8 drive these through the successor run of a `B10AbruptProducer` built with that production behaviour. T9 calls the same production completion behaviour directly, in the I1 order. Only the native producer (a fake in T1 to T8, real in T9) and blob storage (test file storage, T7) are not production objects. A hand-written completion delegate does not count.

To do this, the implementer adds a seam (left open, section 14): a way to build the E2 facts from a completion and the evidence state, separate from the throw site. T6 and T10 need it.

Clock for T1 to T5 and T8 (required):

- a test `TimeProvider` wraps `TimeProvider.System`; timestamps, frequency and UTC time pass through;
- timer creation passes through, except that the first timer created with a 10 ms due time first delivers report P1 synchronously;
- T is the clock's timestamp at delivery, in whole microseconds rounded down;
- `ReleaseBoundaryClock` must not be used, because it never advances.

Statistics report:

```
{"name":"native","client_id":"client","type":"producer","ts":T,"time":100,"age":T-C,"brokers":{"broker":{"name":"broker","nodeid":0,"source":"learned","state":"UP","stateage":0,"req_timeouts":0,"disconnects":0,"txretries":0,"txerrs":0,"rxerrs":0,"throttle":{"cnt":1,"max":0},"rtt":{"cnt":1,"p99":0}}}}
```

C is T captured once, when the native producer is constructed. The expected brokers are `["broker"]`. Every report goes through the production handler.

Delivery points (required; there is no free-running feed):

- P1: one report from the clock hook, when the post-drain poll creates its first 10 ms timer.
- P2: one report from the completion-boundary observer, only while the fake producer has not been disposed. This models a disposed client emitting no statistics.
- P3: where stated, one report from the fake producer's dispose.

Reason, from this session's prototype: a free-running feed at 2 ms or 20 ms failed intermittently, through guard reason 3 contention and session capture contention. With P1 to P3, the gate passed 50 of 50 runs.

Session options in T1 to T5 and T8: `MaxSnapshotCharacters` 1048576, `QueueCapacity` 2048, `MaxBrokerSeries` 64, `CompletionTimeout` 2 s.

Successor inputs:

- release: the clock's UTC now plus 200 ms;
- selection: one entry, attempt `predecessor-cross_restart-0`, key `B10/run-1/C1/1/cross_restart`, sequence 0, payload `[1,2,3]`.

### 7.2 Tests

T1. `Given_LiveProducerAfterSeal_When_SuccessorCompletes_Then_StatisticsCompleteBeforeDisposalAndEvidenceAfter`. Covers R1, R2, R4, R6 and I1.
- Input: P1, P2, P3.
- Expected:
  - no exception;
  - one dispose, and the boundary observer had run before it;
  - evidence not faulted;
  - all five successor paths exist;
  - `successor/statistics.jsonl` has exactly 2 lines (P1 and P2; P3 is a late capture);
  - `successor/request-lifecycle.jsonl` has length 0;
  - in `successor/capabilities.json`, `statistics_completion` matches `^\{"is_complete":true,"drain_boundary_microseconds":[0-9]+,"diagnostics":\[\],"missing_brokers":\[\]\}$`, and `resolved_configuration` is null (no expectations supplied).
- In production, expectations from `RunEvidenceExpectations.CaptureOnly(role, true)` add `first_post_drain_timestamp_microseconds` (`RunEvidenceSession.cs` lines 562-569).

T2. `Given_NoPostBoundaryReport_When_SuccessorCompletes_Then_GuardFailsWithTimeoutFacts`. Covers R2, R3, E1 and E2.
- Input: P1 only.
- Expected:
  - `b10_abrupt_guard_failed`;
  - facts in E2 order: 0, 0, 1, 1, then `diagnostic_completion_timeout` (fact 13) 1 and every other flag 0;
  - one dispose;
  - elapsed at least 2000 ms;
  - `successor/statistics.jsonl` absent;
  - `capture/capabilities.json` exists, with `statistics_completion` matching `^\{"is_complete":false,"drain_boundary_microseconds":[0-9]+,"diagnostics":\[\{"code":"completion_timeout","broker":null,"field":null\}\],"missing_brokers":\["broker"\]\}$` and `is_faulted` false.

T4. `Given_DisposalErrorCallback_When_CompletingSuccessor_Then_CapturesBeforeFinalizingAndAppliesGuard`, moved from `B10AbruptProducerTests.cs` onto the real chain. Covers R5.
- Input: P1, P2; dispose captures `new Error(code, "synthetic disposal callback")`.
- Expected, `NoError`: success, and evidence not faulted.
- Expected, `Local_Fail`: `b10_abrupt_guard_failed` from the post-completion guard checks, with `native_error_observed` 1 and `evidence_faulted` 0; `successor/capabilities.json` and `successor/request-lifecycle.jsonl` absent.
- Expected, both: one dispose; evidence diagnostics have no `error_capture_unavailable`; `capture/request-lifecycle.jsonl` contains `synthetic disposal callback`.

T5. `Given_EvidenceFaultDuringDisposal_When_SuccessorCompletes_Then_GuardFailsWithEvidenceFaulted`. Covers R2, R3 and E2.
- Input: P1, P2; dispose records a blank diagnostic, which faults the evidence session with `invalid_diagnostic`.
- Expected:
  - `b10_abrupt_guard_failed`;
  - facts 1 to 4 are 1, 1, 0, 0; facts 5 to 35 are 0;
  - `capture/capabilities.json` exists, with `is_faulted` true and `statistics_completion.is_complete` true.

T6. `Given_StatisticsCompletion_When_GuardFactsAreBuilt_Then_EachFlagMapsOneCode`, 34 cases. Covers E2 and the reading rule.
- Input: a completion with `IsComplete` false, built directly:
  - (a) no diagnostics, no missing brokers, evidence faulted;
  - (b) the one code `synthetic_unknown_code`, missing `["a","b"]`;
  - (c) all 31 codes plus `synthetic_unknown_code`, missing `["a","b"]`;
  - (d1) to (d31): exactly the k-th E2 code, missing `["a","b"]`.
- Expected facts 1 to 4: (a) 0, 1, 0, 0; (b) 0, 0, 2, 1; (c) 0, 0, 2, 32; (dk) 0, 0, 2, 1.
- Expected flags: none in (a) and (b); all in (c); only fact 4+k in (dk).
- Every case: exactly 35 facts, with names in E2 order.

T7. Existing exporter `Given_SyntheticProducer_When_SupervisorAndSuccessorRun_Then_ActualBytesBindBothPublications`, successor half moved onto the real chain. Covers R1 to R4, S16, S20 and S23.
- Input:
  - P1, through a wrapper of the exporter's clock with the same 10 ms hook, and P2;
  - session `CompletionTimeout` 20 s;
  - the report's `client_id` is the successor options' pod name, because otherwise the session records `client_id_mismatch`;
  - remove the existing 2 ms free-running feed and the flush-time direct observation. Inference: a direct observation without native capture trips guard reason 6 under native-capture tracking (`B10AbruptStatistics.cs` lines 177-182).
- Expected:
  - exporter class: 8 passed;
  - `check_b10_abrupt_producers.py`: 12 tests OK and `b10_abrupt_produced_bytes_checked`;
  - exported `successor/statistics.jsonl`: 2 lines, members `diagnostics, identity, raw_json, receipt_time, received_at, schema_version, session_id, statistics_identity, typed_snapshot`;
  - exported `successor/capabilities.json`: `statistics_completion.is_complete` true.

T8. `Given_StatisticsCompletionThrows_When_SuccessorCompletes_Then_ExceptionPropagatesAndProducerDisposedOnce`. Covers R7.
- Input: P1; the boundary observer throws `new InvalidOperationException("synthetic_boundary_failure")`. `KafkaStatisticsSession.cs` lines 205-207 document that it fails closed through the caller.
- Expected: the successor run throws `InvalidOperationException` with that message, not `b10_abrupt_guard_failed`; one dispose; `capture/capabilities.json` absent; `successor/statistics.jsonl` absent.

T9. `Given_RealNativeProducerWithoutBroker_When_CompletionMembersRun_Then_StatisticsCompleteWhileLiveAndEvidenceAfterDisposal`. Covers R1, R2, R10, I1, S11 and S12.
- Input:
  - the real chain on `TimeProvider.System`;
  - a real `ProducerBuilder<byte[], byte[]>` client: `bootstrap.servers` `127.0.0.1:1`, `client.id` `client`, `statistics.interval.ms` 200 (test only);
  - the production handler, with the log and error handlers the host uses;
  - session `CompletionTimeout` 20 s;
  - expected brokers `["127.0.0.1:1/bootstrap"]`;
  - wait 500 ms, then statistics completion, dispose, evidence completion.
- Expected: no exception; `capture/capabilities.json` `statistics_completion` matches the T1 pattern; evidence not faulted.
- Guard observations are not asserted: the refused connection adds `native_error_callback_observed`.

T10. `Given_AllStatisticsCodes_When_GuardFactsAreAttached_Then_CapKeepsAllThirtyFive`. Covers R11 and E4.
- Input: the E2 facts for a completion carrying all 31 codes, plus one extra distinct fact, attached at one site.
- Expected: exactly the 35 E2 names, in order; the extra fact is dropped.

T11. Existing `Given_ExcessFactsAndFractionalMilliseconds_When_Attached_Then_FirstSixteenAndWholeMillisecondsRemain`, renamed `...FirstThirtyFiveAndWholeMillisecondsRemain`. Covers R11.
- Input: the first 36 fact keys in enum order.
- Expected: 35 kept, equal to the first 35 names in enum order; the millisecond assertion unchanged (1999).
- The cap value follows decision 6751449502, and no other assertion is loosened.

Observations, not bounds:

- T9 in this prototype, 3 of 3 runs, and the fresh checks (port 1 refused, and with a listener on port 1): completion wait 92 to 106 ms, dispose 95 to 102 ms, 3 reports per run.
- Failure-report size, computed by script on the Revision 3 prototype (decision 6751449502):
  - all 35 facts at `long.MinValue`: facts object 1874 UTF-8 bytes; plain failure line with a one-entry chain 2244 bytes;
  - all facts at 0 or 1: facts object 1210 bytes;
  - the fresh check reported 1936 bytes for the Revision 2 layout of 36 facts.
  - All are under 65536, and no other site's size changes.

### 7.3 Mutations

Each mutation is scratch only and never committed, and the receipt records the result of each. M1 to M9 are the fresh checker's list; M10 to M15, M6s, M14s, X1 and X2 are this planner's. The prototype was run with 42 cases in the new class plus the diagnostics tests. Every mutation built with 0 errors, and production was restored byte-identical afterwards. In the table, "T6 (all 34)" means every T6 case.

| Id | Precise mutation | Killed by (observed) |
|---|---|---|
| M1 | Statistics completion moved after disposal | T1, T4 x2, T5 |
| M2 | Evidence completion moved before disposal | T4 x2, T5 |
| M3 | Producer disposed first; statistics completion performed at the start of evidence completion (the `main` shape) | T1, T4 x2, T5, T9 |
| M4 | The R3 guard removed: facts computed, no throw | T2, T5 |
| M5 | Guard condition ignores the evidence fault | T5 |
| M6 | The fact members mapped to codes `late_capture` and `capture_contention` swapped, so the emitted names change order | T6 (all 34), T2, T5, T10 |
| M6s | The code strings mapped to facts 6 and 7 swapped; names and order unchanged | T6 d2, d3 |
| M7 | Guard checks after evidence completion removed | T4 `Local_Fail` |
| M8 | Statistics-completion exception caught; completion replaced by `new KafkaStatisticsCompletion(false, [], [])` | T8 |
| M9 | `diagnostic_count` counts only listed codes present | T6 b, c |
| M10 | Capture-only completion requested, without the broker inventory | T2 |
| M11 | Handler skips native-capture tracking | T1, T2, T4 x2, T5, T8 |
| M12 | Per-site cap left at 16 | T2, T5, T10, T11 |
| M13 | Last flag (`broker_name_mismatch`) removed | T6 (all 34), T2, T5, T10 |
| M14 | The fact members mapped to codes `invalid_json` and `duplicate_property` swapped | T6 (all 34), T2, T5, T10 |
| M14s | The code strings mapped to facts 31 and 32 swapped; names and order unchanged | T6 d27, d28 |
| M15 | `missing_broker_count` fixed at 0 | T6 b, c, d1 to d31, T2 |
| X1 | Handler calls the session before native-capture tracking (reverses R10) | Survives. No test observes the order without relying on timing. Covered by acceptance command 13 |
| X2 | The R3 guard evaluated before evidence completion, so evidence is never completed on the failure path | T1, T2, T4, T5, T9 |

## 8. Timing and margin (estimates)

Estimate, 2026-11-09, computed by script. Basis: the trial 7 stage lines, and the hypothesis that librdkafka emits a report every 15000 ms counted from producer construction.

| Process | Construction (ms) | Report number at `post_drain_report` | Predicted (ms) | Observed (ms) | Lag (ms) |
|---|---|---|---|---|---|
| Successor | 66 (L25) | 7 | 105066 | 105079 (L54) | 13 |
| Child, timeout arm | 67 | 3 | 45067 | 45079 | 12 |
| Child, control arm | 67 | 12 | 180067 | 180087 | 20 |

Under the fix, statistics completion starts at about `cohort_sealed` (105093 ms, L55). The next report is predicted at 120066 ms plus lag, so the slack is 20000 - (120066 - 105093) - lag:

- about 5027 ms at zero lag;
- about 5007 to 5015 ms at the observed lags of 12 to 20 ms;
- in the worst case, completion starts just after a report and the slack is about 5000 ms minus lag.

This is an inference from three samples, not a measured bound. Hari kept both values (decision 6751199288).

Trial 7's guard line has no timestamp. The bounds below come from the pod's start and finish times (19:20:51Z and 19:22:58Z, to the whole second) and two MSAL lines around the timeline origin:

- MSAL line 24 (19:20:52Z) precedes L25 (origin plus 66 ms), so the origin is at or after 19:20:51.934.
- An MSAL line at 19:20:53Z follows L26 (origin plus 74 ms), so the origin is at or before 19:20:53.925.
- Therefore `cohort_sealed` to container exit lay between 18.982 s and 21.972 s, which is consistent with the 20 s completion timeout.

The cause is established by the offline reproduction (T1 fails under M1 and M3), not by these bounds.

Inference: on `main`'s order, the same bounds limit disposal, evidence completion, failure reporting and exit together to at most about 1.97 s beyond the 20 s timeout. That is evidence for D2.

## 9. Deployment assumptions

No offline test runs these for real. Each blocks approval until it is accepted by name. Under standing decision 6752430614 point 1, the control session may accept one only if its failure gives a failed trial with logs that name it.

- D1. After the successor's seal, the live client delivers within 20 s a report naming every expected broker. Basis: section 8 (estimate), S8 (inference) and T9 (real client, no broker). Failure is named by `diagnostic_completion_timeout` and `missing_broker_count`.
- D2. Native disposal after statistics completion returns within the 10 s cleanup timeout. Basis: T9 (about 100 ms) and the section 8 inference (about 1.97 s live).
- D3. Azure Blob create-only uploads of the five successor payloads and `completion.json` succeed under the successor pod's identity. The same publisher and storage uploaded the first pod's files and the successor's `registration.json` live in trial 7.
- D4. The successor exits 0 within the Job deadline (`loadgen_job_seconds` 1140, `charts/parchwell/values.yaml` line 98), the workflow records `loadgen2` complete, and `download` retrieves `SUCCESSOR_FILES`. That includes an empty `request-lifecycle.jsonl`, for which `b10_abrupt.py` line 392 sets minimum 0.
- D5. Host stop and dispose of the successor finish within 10 s.
- D6. The two capture races:
  - The lock-contention race (guard reason 3) exists on `main` and is unchanged here. Its fix is the contention race-fix pull request (decision 6752078378), which comes next in the order.
  - The capture-loss race is not reachable on `main`'s successor path, because there the producer is disposed before statistics completion. This pull request makes it reachable there, as it already is in `RunExecutor`.
    - Mechanism: a callback between closing capture and sealing completion records capture loss and `late_capture` (`KafkaStatisticsSession.cs` lines 95-118). Completion is then incomplete, with `capture_loss` appended (line 632); `capture_contention` (line 110) behaves alike.
    - The window is the worker drain between those two steps, measured against the 15 s interval.
    - Its fix is the capture-loss race-fix pull request (decision 6752059755), which must merge before any further B10 trial.
  - Neither race's live probability is measured. If either fires, the E2 flags name it.
- D7. The host passes the production completion behaviour, with the broker inventory it reads, and the production handler to the successor producer (S18). Code review and acceptance commands 12 and 13 check this.

## 10. Acceptance commands

Run from the repository root. "Observed" is what this planning session saw on 2026-11-09, on unmodified `main` `f5cc496` or on a scratch prototype of Revision 3 that was never pushed. The implementer records its own results in the receipt.

1. `dotnet restore tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --locked-mode`.
   - Observed: exit 0.
   - Expected after: exit 0.
2. `dotnet build tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj -c Release --no-restore`.
   - Observed on `main` and on the prototype: `0 Warning(s)`, `0 Error(s)`.
   - Expected after: the same.
3. `dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj -c Release --no-build -- --filter-class "*B10Abrupt*"`.
   - Observed on `main`: total 457, succeeded 456, skipped 1.
   - Observed on the prototype: total 497, succeeded 496, skipped 1, failed 0. That is 457, minus T4's 2 moved cases, plus the 42 cases of the new class.
   - Expected after: total 497, succeeded 496, skipped 1, failed 0.
4. Repetition gate, 50 runs (a plan choice). The class includes T4, so the gate repeats the disposal-callback path.
   - `for i in $(seq 1 50); do dotnet tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptSuccessorCompletionTests 2>&1 | grep -o 'Total: [0-9]*, Errors: [0-9]*, Failed: [0-9]*'; done | sort | uniq -c`
   - Observed on the prototype: `50 Total: 42, Errors: 0, Failed: 0`.
   - Expected after: the same.
5. `W=$(mktemp -d)`, then `PARCHWELL_B10_ABRUPT_WIRE_OUTPUT="$W" dotnet tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptWireProducerTests`.
   - Observed on the prototype, 10 of 10 runs: `Total: 8, Errors: 0, Failed: 0, Skipped: 0`.
   - Expected after: the same.
6. On the same `$W`: `python3 infra/scripts/phase/tests/check_b10_abrupt_producers.py "$W"`.
   - Observed, 10 of 10 runs: `Ran 12 tests`, `OK`, last line `b10_abrupt_produced_bytes_checked`, with the T7 file shapes.
   - Expected after: the same.
7. `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_b10_abrupt.py'`.
   - Observed: `Ran 12 tests`, `OK`.
   - Expected after: the same.
8. `dotnet build tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj -c Release`, then `PARCHWELL_REPOSITORY_ROOT=$PWD dotnet test --project tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj -c Release --no-build -- --filter-class "*B10Abrupt*"`.
   - Observed on the prototype: 0 errors; total 1, succeeded 1.
   - Expected after: the same.
9. Mutations in section 7.3.
   - Observed: as tabled.
   - Expected after: each mutation except X1 is killed by at least its listed tests, and production is restored byte-identical.
10. `npm run check:docs`.
    - Observed on `main`: `8 warnings`, all in files this plan does not change. With the E5 line applied: that file reports `no issues found`.
    - Expected after: no new warning; the 8 take one blanket line under docs/agents/pull-requests.md rule 8.
11. `./scripts/verify-dotnet.sh` with `dockerd` running.
    - Observed: exit 0 on `main`, on the Revision 2 prototype and on the Revision 3 prototype (`Verification passed.`). A first Revision 3 attempt failed only the Docker-dependent PhantomSpike test because `dockerd` had stopped; after restarting it the run passed.
    - Expected after: exit 0. Final-head CI (`dotnet-verification.yml`) remains the authority.
12. A `grep -n` of `src/Parchwell.LoadGen/Run/Abrupt/` for the completion behaviour and the handler.
    - Expected: completion is requested only on the successor path, after the cohort seal, and is constructed and passed only in the host's producer construction. The handler is passed to the native client's statistics handler. The receipt records the output.
13. Print the production statistics handler's body.
    - Expected: native-capture tracking is called, then the session, with nothing else.
    - This is checked by review, because mutation X1 survives every test.

## 11. Plan-check questions (factory.md "Item lifecycle" step 1)

- (a) Effort that scales with a number unknown at planning time: review findings, verification failures and documentation warnings.
  - Caps: 20 root causes each per pull request, exhaustion action `push -wip and hand off to the next session` (docs/operations/b10-abrupt-contract.md line 79). The cold-review budget is under docs/agents/pull-requests.md rule 8.
  - The repetition gate is fixed at 50 runs and the mutation list at 19, so neither scales.
  - A failure traced to D6's races stops the work and hands off.
- (b) Where the plan is stricter than its parents:
  - the real chain, with a real client where one runs offline (decision 6749565231);
  - the 50-run gate and the deterministic delivery points, because a free-running feed proved flaky;
  - the mutation table.
- (c) Dependencies outside `main` and this pull request: T9 depends on the bundled librdkafka emitting statistics for an unreachable bootstrap broker. It passed with port 1 refused, and with a listener on port 1 (fresh check, 2026-11-09). Behaviour no test settles is D1 to D7.

## 12. Sources

Files on `main` at `f5cc496fa3cf845e68e400b5b65e557f121dd928`:

- `B10AbruptProducer.cs`: lines 100-106, 444-523, 559-580.
- `B10AbruptHost.cs`: lines 50-51, 76-77, 93-106, 190, 218, 226-256.
- `B10AbruptStatistics.cs`: lines 114-140, 164-200, 258-272.
- `B10AbruptDiagnostics.cs`: lines 8-20, 39, 50-58, 96-97.
- `KafkaStatisticsSession.cs`: lines 61-63, 93-141, 177-274, 610-663, and the E2 lines.
- `KafkaStatisticsParser.cs`: lines 22-128.
- `KafkaStatisticsOptions.cs`: lines 32-35.
- `KafkaStatisticsBridgeFactory.cs`.
- `RunEvidenceSession.cs`: lines 248-253, 319-362, 530-575.
- `RunExecutor.cs`: lines 360-438.
- `B10AbruptProducerTests.cs`, `B10AbruptWireProducerTests.cs` and `B10AbruptDiagnosticsTests.cs`, at the tests named.
- `B10AbruptFailureSiteTests.cs`.
- `b10_abrupt.py`: lines 16-18, 383-399, 583-591, 600-645.
- `test_b10_abrupt.py`: lines 220-244.
- `check_b10_abrupt_producers.py`.
- docs/agents/factory.md "Ruleset".
- `.github/CODEOWNERS`.
- docs/blueprint.md line 708.
- docs/operations/b10-abrupt-contract.md lines 51, 79, 160, 166 and 188.

Other sources:

- Commit `54d9065`.
- The trial artifacts in section 6.
- The fresh reproduction checks of Revision 1 (`d5b119a3`) and Revision 2 (`18301978`), relayed by the #347 control session on 2026-11-09. Their scratch files were not reachable from this session.
- librdkafka v2.16.0 `CLOCK_MONOTONIC` statistics timestamps, as cited in `KafkaStatisticsServiceCollectionExtensions.cs` on `main`. Not re-read by this session.

## 13. Open questions for Hari

- Q2. Accept deployment assumptions D1 to D7 by name, or name the test or trial evidence that should settle them.
  - Each one's failure gives a failed trial whose logs name it: D1 by its facts; D2 and D5 by the existing timeout failures; D3 and D4 by `b10_abrupt_publication_*` at a boundary; D6 by its E2 flags; D7 by `b10_abrupt_guard_failed` or a missing file.
  - The control session may therefore decide them under standing decision 6752430614 point 1. Planner's recommendation: accept all seven.

Already answered: Q1 (decision 6751199288), Q3 (decisions 6752059755 and 6752078378) and Q4 (decision 6751449502).

## 14. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" in any of these cases:

- it needs to change a contract in section 3, a requirement in section 4, the scope in section 2 or a value in 2.2;
- the source has a reachable diagnostic code not in E2;
- a repetition, mutation or CI failure traces to either D6 race.

Left open to the implementer, and recorded in the receipt with reasons:

- code text and member names;
- the shape of the production completion behaviour and handler;
- the facts seam that T6 and T10 use;
- the test `TimeProvider` wrapper.

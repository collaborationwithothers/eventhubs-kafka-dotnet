# Successor fix contract-grade plan

Revision 1. Issue #404. Implementing tool: Codex (state file `items/404/state.md`, "Implementing tool"). Base: `main` at `f5cc496fa3cf845e68e400b5b65e557f121dd928`, the source of diagnostic trial 7.

The successor producer in trial 7 disposed its native client before it asked the statistics session to complete, so no statistics report could arrive after the completion boundary and the evidence guard failed. This pull request completes statistics while the client is still live, then disposes it, then completes evidence, and adds completion diagnostics to the guard's facts. It changes no protocol sequence, gate, policy value, failure name, wire member or design text, and it authorizes no trial.

## 1. Goal and authority

Goal: the minimum change that lets the B10 abrupt successor complete its evidence and write its five publication payloads, with tests that run every changed stage against a real `KafkaStatisticsSession`, a real `RunEvidenceSession` and the real `B10AbruptStatistics` adapter.

Authority, in creation order:

- Issue body of #404.
- The decisions listed in `items/404/state.md` "Decisions in force", up to the [burst test-gap approval 6746177383](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6746177383). None of them changes the successor completion order.
- [Decision 6749565231](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749565231) (option 2): plan this "successor fix" pull request in parallel with the verifier pull request; "completes statistics while the producer is still live, then disposes it, then completes evidence, and adds completion diagnostics to the guard facts"; both plans carry a never-ran inventory; a further trial needs Hari's new explicit approval and gate approval. Read in full by this session on 2026-11-09; it is the newest comment on the issue at this plan's catch-up (comment IDs and `updated_at` values saved in the planning session's scratch snapshot).
- [Decision 6749995101](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749995101) (one-repeat diagnostic observation first) and [decision 6750021224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750021224) (verifier work split into PR3a, PR3b, PR3c, "each after the successor fix"; PR3a's predecessor is "the successor fix"). They make this pull request the first to merge and leave its scope unchanged. Both read in full at the pre-push refresh on 2026-11-09.
- Evidence, not authority: [Codex P1 finding r4505115034 on PR 406](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/406#discussion_r4505115034): native error callbacks during successor disposal must be captured before evidence is finalized; "The ordinary `RunExecutor` avoids this by disposing the producer before completing evidence." Commit `54d9065` fixed that finding by moving disposal before both completions, which caused the trial 7 failure. This plan keeps that finding fixed.

Design text this plan depends on and does not change: docs/blueprint.md ADR-021 item 5 (line 708 on `main`: "Timeout/control and successor retain ordinary post-drain coverage"; "`statistics.interval.ms` stays unchanged"); docs/operations/b10-abrupt-contract.md line 51 (timeout/control post-drain checks "do not call the ordinary one-shot statistics/evidence Complete method"), line 160 (a fact is "a closed snake_case name and a 64-bit integer") and line 188 ("16 facts per site", a policy cap).

## 2. Scope

### 2.1 Files allowed to change

1. `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProducer.cs`
2. `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs`
3. `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptDiagnostics.cs`: new `B10AbruptFact` members only (section 3, E2).
4. `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProducerTests.cs`
5. `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptWireProducerTests.cs`
6. New file `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptSuccessorCompletionTests.cs`.
7. `items/404/state.md` (factory.md "Item state file") and new receipt `items/404/successor-fix-receipt.md`.

No new source file under `src/`: `tests/Parchwell.ArchitectureTests/B10AbruptFailureSiteTests.cs` scans a fixed list of 12 source files, so the new guard site must stay in `B10AbruptHost.cs` or `B10AbruptProducer.cs` to stay covered.

### 2.2 Paths that must not change

Everything not in 2.1, in particular: `src/Parchwell.Kafka/**` (including `KafkaStatisticsSession`), `src/Parchwell.LoadGen/Run/Evidence/**`, `B10AbruptStatistics.cs`, `B10AbruptPublisher.cs`, `B10AbruptSupervisor.cs`, `infra/**`, `charts/**`, `scenarios/**`, `results/**`, `docs/**`, `.github/**`, `AGENTS.md`, `CLAUDE.md`, any `*.csproj`, `Directory.*.props`, `*.sln*`. No new project, assembly reference or NuGet package. No protected path is needed.

Policy values that must not change: statistics `CompletionTimeout` 20 s (`B10AbruptHost.cs` line 190), `RunEvidenceOptions` completion timeout 20 s (line 218), `CleanupTimeout` 10 s and `StatisticsCompletionTimeout` 20 s (lines 252-254), host cleanup 10 s (line 95), the 900 s lifetime and watchdog (lines 50-51), `statistics.interval.ms` 15000 (docs/blueprint.md ADR-021 item 5), `MaximumSiteFacts` 16 (`B10AbruptDiagnostics.cs` line 39), and the 10 ms post-drain poll (`B10AbruptProducer.cs` line 570).

## 3. External contracts

- E1. Failure name. An incomplete statistics completion or a faulted evidence session at evidence completion fails with the existing closed name `b10_abrupt_guard_failed` (message of an `InvalidDataException`). No new failure name. The failure line format of `B10AbruptHost.ReportFailure` is unchanged.
- E2. Guard facts at that failure site: exactly these 16 facts, in this order, each a 64-bit integer. Fact names come from new `B10AbruptFact` members through the existing snake-case conversion (`B10AbruptDiagnostics.cs` line 97).

  | # | Fact name | Value |
  |---|---|---|
  | 1 | `completion_complete` | 1 when `KafkaStatisticsCompletion.IsComplete`, else 0 (existing member) |
  | 2 | `evidence_faulted` | 1 when `RunEvidenceSession.IsFaulted` after its completion, else 0 (existing member) |
  | 3 | `missing_broker_count` | `MissingBrokers.Length` |
  | 4 | `diagnostic_count` | `Diagnostics.Length` (distinct codes, as the session returns them) |
  | 5 | `diagnostic_completion_timeout` | 1 when `Diagnostics` contains code `completion_timeout`, else 0 |
  | 6 | `diagnostic_completion_cancelled` | same, code `completion_cancelled` |
  | 7 | `diagnostic_sink_drain_timeout` | same, code `sink_drain_timeout` |
  | 8 | `diagnostic_capture_loss` | same, code `capture_loss` |
  | 9 | `diagnostic_late_capture` | same, code `late_capture` |
  | 10 | `diagnostic_capture_contention` | same, code `capture_contention` |
  | 11 | `diagnostic_capture_queue_overflow` | same, code `capture_queue_overflow` |
  | 12 | `diagnostic_processing_failure` | same, code `processing_failure` |
  | 13 | `diagnostic_evidence_sink_failure` | same, code `evidence_sink_failure` |
  | 14 | `diagnostic_disposed_before_completion` | same, code `disposed_before_completion` |
  | 15 | `diagnostic_disposed_during_completion` | same, code `disposed_during_completion` |
  | 16 | `diagnostic_clock_unverified` | same, code `clock_unverified` |

  Reading rule, stated here and in the receipt only: any code without its own flag is visible as `diagnostic_count` minus the sum of facts 5 to 16. The 12 flagged codes are a planner's choice among the codes `KafkaStatisticsSession` emits, picked as the ones most directly about capture, completion and drain (emitted at `KafkaStatisticsSession.cs` lines 74, 97, 110, 117, 129, 200, 242-244, 259-261, 289, 307, 332, 436, 632, 642). Other session codes (for example `evidence_delivery_cancelled`, `worker_cancellation_failure`, `abandoned_without_completion`, `counter_reset`) and the parser and snapshot-validation codes stay unflagged because the 16-fact cap leaves no room (open question Q4).
- E3. Successor outputs unchanged: the five payload names `ledger.jsonl`, `manifest.json`, `statistics.jsonl`, `request-lifecycle.jsonl`, `capabilities.json` (`B10AbruptProducer.cs` line 45), `SuccessorReceipt` members, stage names and timeline format. No new stage (the child stage list is design text in docs/operations/b10-abrupt-contract.md line 166).
- E4. Kafka client settings: none added or changed.

## 4. Requirements

Successor path only (`B10AbruptProducer.RunSuccessorAsync` and the host members it calls). Line numbers refer to `RunSuccessorAsync` on `main` (`B10AbruptProducer.cs` lines 444-523).

- R1. WHEN the successor has sealed its `cross_restart` cohort (after `cohort_sealed`, line 486), THEN it requests statistics completion exactly once, through `KafkaStatisticsSession.CompleteAsync(expectedBrokerNames, token)` with the broker names `ReadExpectedBrokersAsync` returned, while the native producer has not been disposed.
- R2. WHEN that statistics completion returns, complete or not, THEN the successor disposes the native producer exactly once through the existing `CleanupAsync` (unchanged `CleanupTimeout`), and only after disposal returns
- R3. THEN it calls `RunEvidenceSession.CompleteAsync(completion, token)` exactly once, with the completion object from R1.
- R4. WHEN, after R3, `completion.IsComplete` is false or `evidence.IsFaulted` is true, THEN it throws `b10_abrupt_guard_failed` with exactly the E2 facts, and writes none of `statistics.jsonl`, `request-lifecycle.jsonl`, `capabilities.json` under `<OutputRoot>/successor/`.
- R5. WHEN, after R3, the completion is complete and evidence is not faulted, THEN the existing steps run unchanged: `CheckGuards`, the copy loop of lines 492-506 with the read limit 134217728 bytes, `CheckGuards`, and the return of `SuccessorArtifacts`.
- R6. WHEN a native error callback reaches `RunEvidenceSession.CaptureError` during disposal, THEN it is captured (no `error_capture_unavailable`, `RunEvidenceSession.cs` lines 248-253), and for an error code other than `NoError` the post-completion `CheckGuards` throws `b10_abrupt_guard_failed` with `native_error_observed` 1. This keeps the PR 406 finding fixed.
- R7. WHEN a statistics callback arrives after statistics completion and before or during disposal, THEN the successor still succeeds: the completion object is unchanged, evidence is not faulted, and `B10AbruptStatistics` raises no guard.
- R8. WHEN statistics completion throws, THEN the exception propagates unchanged, the existing `finally` (line 522) disposes the producer, and no new catch is added.
- R9. The first instance (`RunFirstAsync`) and the timeout and control cohorts never call statistics or evidence completion (docs/operations/b10-abrupt-contract.md line 51); unchanged.
- R10. The test-only branch where no completion callback is supplied (lines 507-516) is unchanged.

## 5. Invariants and safety

- I1. Order on the successor path: statistics completion, then disposal, then evidence completion, then guard. No other order is allowed (precedent: `RunExecutor.cs` lines 360-438 on `main`).
- I2. The statistics session completes at most once per process (`KafkaStatisticsSession.cs` lines 187-190 throw on a second call).
- I3. AGENTS.md hard safety rules: no Azure credential, no workflow dispatch, no `.github/` change, no trial. Verdict integrity: nothing under `results/`, no expected outcome.
- I4. No number enters code or docs from this plan's estimates (section 8); estimates stay in this plan and the receipt.

## 6. Never-ran inventory

Evidence is the trial logs, not the code. Seven B10 abrupt diagnostic runs exist (workflow `b10-abrupt-trial.yml` runs list, read 2026-11-09). Artifacts were downloaded with `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/<id>/zip` into empty scratch directories and treated as untrusted data.

| Run | Source | Artifact | Zip SHA-256 | Successor job started |
|---|---|---|---|---|
| [42460966764](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42460966764) | `2d4792ee` | 13609836611 | `bbc4030f30fceef33c156c7b00d8533342c7415887b9d59afab7f52db68a78e7` | no |
| [42475024856](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42475024856) | `b4d6f9e8` | 13615377394 | `270597b9f683416aa72ef5230e90bf896973ac7b3fb9c9030bcbbcd1f6a75c83` | no |
| [42512105140](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42512105140) | `25cc301a` | 13629179730 | `a5b9c345121eecfc9b836706101ee776cbd3269d57e4150a04a4a632917ef9ad` | no |
| [42536075365](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42536075365) | `ee36f1c6` | 13639442741 | `0564049fa591fdfcdd8d4cfbac36794d43498cac72aeccec12666e800979af69` | no |
| [42608381540](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42608381540) | `4163cd1a` | 13668809132 | `0c22924e47ed6b455e228f98be5016c6e3c97faab541d9ed2f9853bf1005eb30` | no |
| [42670600125](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42670600125) | `fa73aa08` | 13699980854 | `b2e2c270f6133c86341eafd1aa5c95a4f0c391a9ecce2b6134707d19a4341bd1` | no |
| [42679791413](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42679791413) (trial 7) | `f5cc496f` | 13704155185 | `bee17231882ffeb7ddc3134ba10a00ec4590f20e96dad409c050decf1f22cdcf` | yes |

"Successor job started" is no for the first six because none of their artifacts contains a `loadgen2` directory or any `"role":"successor"` line (checked by script). Run 42670600125 is not linked from issue #404; it is listed because the workflow ran it. Trial 7's successor log is `b10-42679791413-1-c1-r1-loadgen2/b10-42679791413-1-c1-r1-loadgen2-prrgv.containers.loadgen.log`, SHA-256 `fce88054fccf70591da6f334b7299e446a7ec66cd7b6814fec8740cd17e7209f`; "L<n>" below is its line number.

| Stage on the successor path | Ran live? Evidence | Test in this plan that runs it for real |
|---|---|---|
| S1. Predecessor binding load and validation | Yes, trial 7 only: L25 `producer_constructed` follows it (`B10AbruptHost.cs` lines 76-77, 116-121) | Not changed; existing `B10AbruptWireProducerTests` production-binding cases |
| S2. Successor registration upload | Yes: artifact file `b10-42679791413-1-c1-r1-loadgen2-prrgv/registration.json` (kind `registration`) | Not changed |
| S3. Producer and statistics session construction | Yes: L25 | T1 to T5 construct a real session through `KafkaStatisticsBridgeFactory` |
| S4. Release wait | Yes: L51 `release_reached` | T1 (real `WaitForReleaseAsync`) |
| S5. Resend produce and terminal | Yes: L52 `probe_terminal` | T1 (fake native producer) |
| S6. Drain | Yes: L53 `cohort_drained` | T1 |
| S7. Post-drain report wait through `B10AbruptStatistics` | Yes: L54 `post_drain_report`, 10896 ms, 7 reports | T1 (real adapter, real session sink chain) |
| S8. Cohort seal | Yes: L55 `cohort_sealed` | T1 |
| S9. Statistics completion while the producer is live (changed) | Never. Trial 7 ran `CompleteAsync` only after disposal; no line shows the call itself; L56 shows its result `completion_complete:0` at `B10AbruptHost.cs` line 249 | T1 (complete), T2 (timeout), mutation M1 |
| S10. Producer disposal after statistics completion (changed position) | Never in this position. Disposal before completion ran in trial 7 (code order, lines 488-490); no stage line records disposal | T1, T4, T5 (fake native `Dispose`) |
| S11. Evidence completion after disposal (changed position) | Ran once in trial 7 with an incomplete statistics completion: L56 was thrown at host line 249, after line 248 completed evidence (inferred from code order; no line records it directly) | T1, T2, T4, T5 with a real `RunEvidenceSession` |
| S12. Guard with the E2 facts (changed) | Old two-fact guard ran: L56 `{"completion_complete":0,"evidence_faulted":0}`. The new facts never ran | T2, T5, T6 |
| S13. Post-completion `CheckGuards` (line 491) | Never; no line | T4 (Local_Fail case) |
| S14. Copy of the three evidence artifacts into `successor/` | Never; no line | T1, T7 |
| S15. Host cleanup: statistics and evidence `DisposeAsync`, host stop and dispose within 10 s (lines 95-101) | Never; no line. Trial 7 failed before it | Partly: T1 disposes the real session and evidence after a completed run. Host stop and dispose are not run by any offline test in this plan: deployment assumption D5 |
| S16. Read of successor payloads and `PublishSuccessorAsync` (lines 102-106) | Never. `phase-status.json` records `b10_abrupt_publication_ambiguous` at boundaries `successor_job` (`b10_abrupt.py` line 591) and `successor_publication` (line 394): the objects were missing | T7 runs the real `PublishSuccessorAsync` against the test `FileStorage`. Blob upload itself: deployment assumption D3 |
| S17. Successor process exit 0 and Job completion | Never: `loadgen2-pods.json` shows exit code 1, started 19:20:51Z, finished 19:22:58Z | Deployment assumption D4 |
| S18. Workflow download of `SUCCESSOR_FILES` | Ran and failed (S16 evidence). The same `download` function succeeded live for `FIRST_FILES`, because trial 7 reached boundary `successor_admission` (`b10_abrupt.py` lines 583-587) | Deployment assumption D4 |
| S19. Workflow successor receipt validation `collect(files, observation)` (`b10_abrupt.py` lines 604-633) | Never | T7 then `check_b10_abrupt_producers.py`, whose `test_produced_first_and_successor_transport` calls `collect` on the produced bytes (`test_b10_abrupt.py` lines 239-244) |

Why the existing tests missed S9: every successor test on `main` that supplies a completion callback uses a hand-written one that calls `Evidence.CompleteAsync(null, token)` and never a statistics session (`B10AbruptProducerTests.cs` lines 1289-1294), and the produced-byte exporter passes no callback at all, so it takes the test-only branch (`B10AbruptWireProducerTests.cs` lines 371-373; `B10AbruptProducer.cs` lines 507-516). The exported successor `statistics.jsonl` and `capabilities.json` on `main` are therefore not the production evidence files.

## 7. Test map

All tests are in `tests/Parchwell.LoadGen.Tests/Abrupt/`. Names follow docs/engineering/dotnet.md line 62. "Real chain" means: a real `RunEvidenceSession`; a real `B10AbruptStatistics` with `ActivateNativeCaptureTracking()` and that evidence session as downstream; a real `RunEvidenceSinkRouter` attached to it; a real `KafkaStatisticsSession` from `new KafkaStatisticsBridgeFactory(options, clock, router, metrics, nativeClockCompatible: true, completionBoundaryObserver)`; and the production completion members that `B10AbruptHost.CreateProducerAsync` passes to `B10AbruptProducer`, invoked by `RunSuccessorAsync`. Only the native `IProducer` is a fake (`B10AbruptProducerTests.NativeProducer`), and only blob storage is a fake (`FileStorage`, T7). A hand-written completion delegate does not count.

Statistics report input, every test: JSON `{"name":"native","client_id":<statistics client id>,"type":"producer","ts":T,"time":100,"age":T-C,"brokers":{"broker":{"name":"broker","nodeid":0,"source":"learned","state":"UP","stateage":0,"req_timeouts":0,"disconnects":0,"txretries":0,"txerrs":0,"rxerrs":0,"throttle":{"cnt":1,"max":0},"rtt":{"cnt":1,"p99":0}}}}`, where T is the test clock's timestamp in whole microseconds at delivery and C is that value captured once when the native producer is constructed. Expected brokers `["broker"]`. Each report is delivered as the production statistics handler delivers it (`B10AbruptHost.cs` line 232): `B10AbruptStatistics.CaptureNative(raw)`, then `KafkaStatisticsSession.Capture(raw)`.

Delivery points, deterministic, no free-running timer: (P1) one report, delivered synchronously when the successor's post-drain poll creates its first 10 ms delay timer (a test `TimeProvider` that wraps the clock and intercepts `CreateTimer` with due time 10 ms once); (P2) one report from the completion boundary observer, only while the fake producer's `Disposals` is 0, which models that a disposed client emits no statistics; (P3) where a test says so, one report from the fake producer's `Dispose`. Reason, observed in this planning session's scratch prototype: a free-running 2 ms or 20 ms feed made the real chain fail intermittently with `B10AbruptStatistics` guard reason 3 (`Monitor.TryEnter` contention at `B10AbruptStatistics.cs` lines 129-133 against the 10 ms `SelectPostDrain` poll) or session capture contention; with P1 to P3 the prototype passed 50 of 50 runs.

Statistics session options in T1 to T5: `MaxSnapshotCharacters` 1048576, `QueueCapacity` 2048, `MaxBrokerSeries` 64, `CompletionTimeout` 2 s. Evidence options as production (`B10AbruptHost.cs` line 218). Release is the test clock's UTC now plus 200 ms. Selection: one entry, attempt `predecessor-cross_restart-0`, key `B10/run-1/C1/1/cross_restart`, sequence 0, payload `[1,2,3]`.

| Test | Requirement | Input | Literal expected values |
|---|---|---|---|
| T1 `Given_LiveProducerAfterSeal_When_SuccessorCompletes_Then_StatisticsCompleteBeforeDisposalAndEvidenceAfter` | R1, R2, R3, R5, R7, I1 | P1, P2, P3 | No exception. `Disposals` 1. The boundary observer ran before `Dispose` (recorded flag true at `Dispose`). `evidence.IsFaulted` false. All five `SuccessorArtifacts.Paths` exist. `successor/statistics.jsonl` has exactly 2 lines (P1 and P2; P3 is a late capture and is not written). `successor/request-lifecycle.jsonl` length 0. `successor/capabilities.json` member `statistics_completion` equals `{"is_complete":true,...,"diagnostics":[],"missing_brokers":[]}` |
| T2 `Given_NoPostBoundaryReport_When_SuccessorCompletes_Then_GuardFailsWithTimeoutFacts` | R2, R3, R4, E1, E2 | P1 only | `InvalidDataException` message `b10_abrupt_guard_failed`. Site facts exactly `{"completion_complete":0,"evidence_faulted":0,"missing_broker_count":1,"diagnostic_count":1,"diagnostic_completion_timeout":1}` followed by facts 6 to 16 all 0, in E2 order. `Disposals` 1. Elapsed at least 2000 ms. `successor/statistics.jsonl` does not exist |
| T3 boundary of T1/T2 | R1 | covered by T1 (one post-boundary report: complete) and T2 (zero: timeout) | as T1 and T2 |
| T4 existing `Given_DisposalErrorCallback_When_CompletingSuccessor_Then_CapturesBeforeFinalizingAndAppliesGuard`, both cases, moved onto the real chain | R6 | P1, P2; `Dispose` calls `CaptureError(new Error(code, "synthetic disposal callback"))` | `NoError`: success; `successor/request-lifecycle.jsonl` contains `synthetic disposal callback`; evidence not faulted; no `error_capture_unavailable`. `Local_Fail`: `b10_abrupt_guard_failed` thrown at `CheckGuards` with `native_error_observed` 1 and `evidence_faulted` 0; `successor/capabilities.json` and `successor/request-lifecycle.jsonl` absent. Both: `Disposals` 1 and disposal happened before evidence completion |
| T5 `Given_EvidenceFaultDuringDisposal_When_SuccessorCompletes_Then_GuardFailsWithEvidenceFaulted` | R4, E2 | P1, P2; `Dispose` calls `RecordDiagnostic(" ")` (latches `invalid_diagnostic`) | `b10_abrupt_guard_failed`; facts `completion_complete` 1, `evidence_faulted` 1, `missing_broker_count` 0, `diagnostic_count` 0, facts 5 to 16 all 0 |
| T6 `Given_StatisticsCompletion_When_GuardFactsAreBuilt_Then_EachFlagMapsOneCode` (3 cases) | E2, reading rule | A `KafkaStatisticsCompletion` built directly: (a) no diagnostics, no missing brokers, evidence faulted; (b) one unflagged code `late_attachment`, missing `["a","b"]`; (c) all 12 flagged codes plus `late_attachment`, missing `["a","b"]` | (a) facts 1 to 4 = 0,1,0,0, flags all 0. (b) 0,0,2,1, flags all 0. (c) 0,0,2,13, flags all 1. Exactly 16 facts in E2 order in every case |
| T7 existing exporter `Given_SyntheticProducer_When_SupervisorAndSuccessorRun_Then_ActualBytesBindBothPublications`, successor half moved onto the real chain with the production completion members | R1 to R5, S14, S16, S19 | P1, P2 on the exporter's clock | Exporter class: 8 passed. Then `check_b10_abrupt_producers.py`: 12 tests OK and marker `b10_abrupt_produced_bytes_checked`. Exported `successor/statistics.jsonl`: 2 lines with members `diagnostics, identity, raw_json, receipt_time, received_at, schema_version, session_id, statistics_identity, typed_snapshot`. Exported `successor/capabilities.json` has `statistics_completion.is_complete` true |

Mutation checks, scratch only, never committed, recorded in the receipt:

- M1. Move statistics completion after disposal (the `main` order). T1 must fail with `b10_abrupt_guard_failed` and facts `completion_complete` 0, `missing_broker_count` 1, `diagnostic_count` 1, `diagnostic_completion_timeout` 1.
- M2. Complete evidence before disposal. T4 `NoError` must fail.

## 8. Timing and margin (estimates)

Estimate, 2026-11-09, basis trial 7 stage lines and the hypothesis that librdkafka emits a report every 15000 ms counted from producer construction. Computed by script:

| Process | Construction (ms) | Report number at `post_drain_report` | Predicted (ms) | Observed (ms) | Lag (ms) |
|---|---|---|---|---|---|
| Successor | 66 (L25) | 7 | 105066 | 105079 (L54) | 13 |
| Child, timeout arm | 67 | 3 | 45067 | 45079 | 12 |
| Child, control arm | 67 | 12 | 180067 | 180087 | 20 |

Under the fix, statistics completion would start at about `cohort_sealed` (105093 ms, L55, 14 ms after L54). The next report is predicted at 120066 ms, so the wait is about 14973 ms and the slack against the 20000 ms completion timeout about 5027 ms. In the worst case, completion starts just after a report and waits one full interval plus lag: slack about 5000 ms minus lag. This is an inference from three samples, not a measured bound.

Trial 7's guard line has no timestamp. Bounds by script from pod times (start 19:20:51Z, finish 19:22:58Z) and the MSAL log seconds around the timeline origin: `cohort_sealed` to container exit lay between 18.982 s and 21.906 s, consistent with the 20 s completion timeout. The cause is established by the offline reproduction (M1 and the scratch run in section 10), not by these bounds.

## 9. Deployment assumptions

No offline test runs these for real. Each is an open question until Hari accepts the risk by name (factory.md "Item lifecycle" steps 1 and 3).

- D1. After the successor's seal, the live librdkafka client delivers at least one statistics report naming every expected broker within the 20 s completion timeout. Basis: section 8 (estimate).
- D2. Native disposal of the successor producer after statistics completion returns within `CleanupTimeout` 10 s. Disposal before completion returned in trial 7 (no timeout failure is recorded), but its duration is not logged.
- D3. Azure Blob create-only uploads of the five successor payloads and `completion.json` succeed under the successor pod's workload identity. The same publisher and storage class uploaded the first pod's files and the successor's `registration.json` live in trial 7 (S2, S18).
- D4. The successor container exits 0 within the Job deadline (`loadgen_job_seconds` 1140, `charts/parchwell/values.yaml` line 98), the workflow records `loadgen2` complete, and `download` retrieves `SUCCESSOR_FILES`, including an empty `request-lifecycle.jsonl` (`b10_abrupt.py` line 392 sets minimum 0 for that name).
- D5. Host stop and dispose of the successor (S15) finish within 10 s.
- D6. The two existing capture races are not hit live: `B10AbruptStatistics.CaptureNative` contention with the 10 ms poll (guard reason 3), and a statistics callback landing between `CloseCapture` and the completion seal (`capture_loss`). Both exist on `main` and are unchanged here. Their live probability is not measured.

## 10. Acceptance commands

Run from the repository root. "Observed" is what this planning session saw on 2026-11-09: on unmodified `main` `f5cc496`, or on a scratch prototype of this plan (worktree of `f5cc496` plus a prototype of R1 to R4, E2, T1, T2, T4, T5, T6 and T7 shapes; never pushed). The implementer records its own results in the receipt.

1. `dotnet restore tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --locked-mode`. Observed on `main`: `All projects are up-to-date for restore.`, exit 0. Expected after: same.
2. `dotnet build tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj -c Release --no-restore`. Observed on `main` and on the prototype: `0 Warning(s)`, `0 Error(s)`, exit 0. Expected after: same.
3. `dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj -c Release --no-build -- --filter-class "*B10Abrupt*"`. Observed on `main` and on the prototype (whose test class did not match the filter): total 457, succeeded 456, skipped 1 (`B10AbruptPublishedApphostTests`, reserved to CI). Expected after, computed as 457 plus 6 new cases (T1, T2, T5, three T6 cases; T4 and T7 keep their counts): total 463, succeeded 462, skipped 1, failed 0.
4. Repetition gate, 50 runs (a plan choice, not a measurement): `for i in $(seq 1 50); do dotnet tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptSuccessorCompletionTests 2>&1 | grep -o 'Failed: [0-9]*'; done | sort | uniq -c`. Observed on the prototype class with P1 to P3: `50` lines of `Failed: 0` (4 cases per run). Expected after: `50 Failed: 0`.
5. `W=$(mktemp -d)`; `PARCHWELL_B10_ABRUPT_WIRE_OUTPUT="$W" dotnet tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptWireProducerTests`. Observed on the prototype, 10 of 10 runs: `Total: 8, Errors: 0, Failed: 0, Skipped: 0`. Expected after: same.
6. Immediately on the same `$W`: `python3 infra/scripts/phase/tests/check_b10_abrupt_producers.py "$W"`. Observed on the prototype, 10 of 10 runs: `Ran 12 tests`, `OK`, last line `b10_abrupt_produced_bytes_checked`; successor `statistics.jsonl` 2 lines with the T7 members; `capabilities.json` `statistics_completion` = `{"is_complete": true, "drain_boundary_microseconds": <n>, "diagnostics": [], "missing_brokers": []}`. Expected after: same.
7. `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_b10_abrupt.py'`. Observed: `Ran 12 tests`, `OK`. Expected after: same (no Python change).
8. `dotnet build tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj -c Release`, then `PARCHWELL_REPOSITORY_ROOT=$PWD dotnet test --project tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj -c Release --no-build -- --filter-class "*B10Abrupt*"`. Observed on the prototype: build 0 warnings 0 errors; total 1, succeeded 1. Expected after: same.
9. Mutations M1 and M2 (section 7). Observed on the prototype: M1 (statistics completion after disposal) failed with `{"completion_complete":0,"evidence_faulted":0,"missing_broker_count":1,"diagnostic_count":1,"diagnostic_completion_timeout":1,...}` after 2219 ms; M2 (evidence before disposal) failed the `NoError` disposal-callback case. Expected after: each mutation makes its named test fail; production restored byte-identical afterwards.
10. `npm run check:docs`. Observed on `main`: `8 warnings`, all in files this plan does not change. Expected after: no new warning in `items/404/`; the 8 take one blanket line under docs/agents/pull-requests.md rule 8.
11. `./scripts/verify-dotnet.sh`. Observed on unmodified `main` in this planning container: `Parchwell.Kafka.Tests tests: failed (exit 2)`, one failure, `PhantomSpikeTests...ExperimentalIntegrityIsEstablished`, a `NullReferenceException` at `ExperimentDockerAuthentication.get_Endpoint()`; the container has no Docker. Expected after: final-head CI (`dotnet-verification.yml`) green is the authority for the full suite, including the published-apphost and abrupt-host checks that only CI runs; a local run records the same Docker limitation if it recurs.

## 11. Plan-check questions (factory.md "Item lifecycle" step 1)

- (a) Effort that scales with a number unknown at planning time: review findings, verification failures and documentation warnings. Caps: 20 root causes each per pull request, exhaustion action `push -wip and hand off to the next session` (docs/operations/b10-abrupt-contract.md line 79); cold-review budget under docs/agents/pull-requests.md rule 8. The repetition gate is fixed at 50 runs and the mutation list at 2; neither scales. If a repetition run fails because of D6's existing races (guard reason 3 or `capture_loss`), that is outside this scope: stop and hand off to Hari.
- (b) Stricter than the parent plans: every changed stage must run against the real chain (decision 6749565231); the 50-run repetition gate and the deterministic delivery points P1 to P3 are added because the prototype showed a free-running feed is flaky; no parent plan required them.
- (c) Tests that depend on code, data or behaviour neither on `main` nor in this pull request: none for T1 to T7, which use only `main` code plus this pull request. Behaviour no test settles is listed as D1 to D6.

## 12. Sources

- `main` at `f5cc496fa3cf845e68e400b5b65e557f121dd928`: `B10AbruptProducer.cs` lines 45, 444-523, 559-580, 626-632; `B10AbruptHost.cs` lines 50-51, 76-77, 93-106, 190, 218, 226-256, 259-281; `B10AbruptStatistics.cs` lines 114-140, 258-272; `B10AbruptDiagnostics.cs` lines 8-20, 39, 50-58, 96-97; `KafkaStatisticsSession.cs` lines 93-141, 177-274, 547-570, 610-663; `KafkaStatisticsCompletion.cs`; `RunEvidenceSession.cs` lines 203-208, 248-253, 319-362; `RunExecutor.cs` lines 360-438; `B10AbruptProducerTests.cs` lines 1039-1078, 1236-1325, 1472-1500; `B10AbruptWireProducerTests.cs` lines 351-391; `B10AbruptFailureSiteTests.cs`; `b10_abrupt.py` lines 16-18, 383-399, 392, 583-591, 600-645; `test_b10_abrupt.py` lines 220-244; `check_b10_abrupt_producers.py`; docs/blueprint.md line 708; docs/operations/b10-abrupt-contract.md lines 51, 79, 160, 166, 188.
- Commit `54d9065` (diff of `B10AbruptProducer.cs` and its test).
- Trial artifacts and hashes in section 6.
- librdkafka v2.16.0 statistics timestamps use `CLOCK_MONOTONIC` on Linux, as cited in `KafkaStatisticsServiceCollectionExtensions.cs` on `main`; not re-read by this session.

## 13. Open questions for Hari

- Q1. Margin. The successor's statistics completion would wait about one 15 s interval against a 20 s timeout: estimated slack about 5.0 s (section 8). Options: (a) keep both values; (b) raise the successor's statistics `CompletionTimeout` (a policy value); (c) start statistics completion at the drain boundary so the post-drain report also satisfies it (a protocol sequence change). Planner's recommendation: (a), because a timeout would now be named by `diagnostic_completion_timeout`. Hari decides.
- Q2. Accept deployment assumptions D1 to D6 by name, or name the test or trial evidence that should settle them.
- Q3. D6's existing races: open a separate issue, or accept. Not fixed here; out of scope.
- Q4. The 12 flagged codes in E2 fill the 16-fact policy cap. Confirm this closed list, or name a different one; raising the cap is a policy change.

No protected path is needed. No question is decided by this plan.

## 14. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, the scope in section 2, a policy value in 2.2, or when a repetition or CI failure traces to D6. Choices this plan leaves open (code text, member names, the shape of the production completion members, the test `TimeProvider` wrapper) are recorded in the receipt with their reasons.

# Successor fix contract-grade plan

Revision 2. Issue #404. Implementing tool: Codex (state file `items/404/state.md`, "Implementing tool"). Base: `main` at `f5cc496fa3cf845e68e400b5b65e557f121dd928`, the source of diagnostic trial 7.

The successor producer in trial 7 disposed its native client before it asked the statistics session to complete. No statistics report could then arrive after the completion boundary, so the evidence guard failed. This pull request completes statistics while the client is still live, then disposes it, then completes evidence. It also gives the guard one fact per statistics diagnostic code, which needs the per-site fact cap raised from 16 to 36 under Hari's decision 6751449502. It changes no protocol sequence, gate, timeout, interval, failure name or wire member, and it authorizes no trial.

Changes since Revision 1 (commit `d5b119a3`), from the fresh reproduction check relayed by the #347 control session and from Hari's decisions 6751199288 and 6751449502:

- F1: new test T8 for R8; the swallowed-exception mutation is now killed.
- F2 and decision 6751449502: one flag for each of all 32 session codes; cap 16 to 36; contract doc line 188 updated; T6 now has cases d1 to d32; new cap test T10; existing cap test updated.
- F3: D6 rewritten, because this pull request makes the capture-loss race reachable on the successor path.
- F4: new test T9 with a real librdkafka client.
- F5 to F14: acceptance and test-map corrections.
- F15: new never-ran inventory rows.
- Q1 and Q4 are answered and recorded; the mutation table is in section 7.

## 1. Goal and authority

Goal: the minimum change that lets the B10 abrupt successor complete its evidence and write its five publication payloads. Every changed stage is tested against a real `KafkaStatisticsSession`, a real `RunEvidenceSession`, the real `B10AbruptStatistics` adapter and, where it runs offline, a real librdkafka client.

Authority, in creation order:

- Issue body of #404.
- The decisions listed in `items/404/state.md` "Decisions in force", up to the [burst test-gap approval 6746177383](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6746177383). None of them changes the successor completion order.
- [Decision 6749565231](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749565231), option 2.
  - It plans this "successor fix" pull request.
  - It "completes statistics while the producer is still live, then disposes it, then completes evidence, and adds completion diagnostics to the guard facts".
  - The plan carries a never-ran inventory, and the fresh checker reproduces it.
  - A further trial needs Hari's new explicit approval and gate approval.
- [Decision 6749995101](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749995101) and [decision 6750021224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750021224): PR3a, PR3b and PR3c come "each after the successor fix". This pull request therefore merges first, and its scope is unchanged.
- Decisions [6750864341](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750864341), [6750879393](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750879393), [6750891952](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750891952) [6750940329](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6750940329) and [6751703425](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6751703425) govern PR3a only. They do not change this plan.
- [Decision 6751199288](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6751199288), option 1: "Keep both values: statistics interval 15 s and successor completion timeout 20 s. No timeout, interval or protocol sequence changes."
- [Decision 6751449502](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6751449502), option 3.
  - Raise the per-site fact cap "just enough" for the four summary facts plus one flag for every code `KafkaStatisticsSession` can emit, parser and snapshot-validation codes included.
  - Pin the code list, the fact order and the cap. The new value "is a policy choice with no measured basis".
  - The contract sentence states the new cap.
  - "no other site's facts change"; the other caps are unchanged.
- Evidence, not authority: [Codex P1 finding r4505115034 on PR 406](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/406#discussion_r4505115034). Native error callbacks during successor disposal must be captured before evidence is finalized. Commit `54d9065` fixed that finding by moving disposal before both completions, and that move caused the trial 7 failure. This plan keeps the finding fixed (R5).

All decisions above were read in full at the 2026-11-09 refreshes of this planning session.

Design text this plan depends on and does not change:

- docs/blueprint.md ADR-021 item 5 (line 708 on `main`): "Timeout/control and successor retain ordinary post-drain coverage"; "`statistics.interval.ms` stays unchanged".
- docs/operations/b10-abrupt-contract.md line 51: timeout and control post-drain checks "do not call the ordinary one-shot statistics/evidence Complete method".
- The same file, line 160: a fact is "a closed snake_case name and a 64-bit integer".

Design text this plan changes: that file's line 188, the cap number only (E5).

## 2. Scope

### 2.1 Files allowed to change

1. `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProducer.cs`
2. `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs`
3. `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptDiagnostics.cs`: 34 new `B10AbruptFact` members (E2) and `MaximumSiteFacts` from 16 to 36 (E4). Nothing else.
4. `docs/operations/b10-abrupt-contract.md`: line 188 only, as E5 pins. `/docs/` is a protected path (docs/agents/pull-requests.md, opening paragraph), so Hari merges this pull request.
5. `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProducerTests.cs`: remove `Given_DisposalErrorCallback_When_CompletingSuccessor_Then_CapturesBeforeFinalizingAndAppliesGuard` (lines 1038-1078), which moves into the new file as T4. Remove the fixture's hand-written completion delegate and its `DisposalsAtEvidenceCompletion` (lines 1246, 1289-1294) if nothing else uses them.
6. `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptWireProducerTests.cs`: the successor half of the exporter (T7).
7. `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptDiagnosticsTests.cs`: the existing cap test (lines 70-77) changes from 16 to 36, as T11 pins.
8. New file `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptSuccessorCompletionTests.cs`: T1, T2, T4, T5, T6, T8, T9 and T10.
9. `items/404/state.md` (factory.md "Item state file") and the new receipt `items/404/successor-fix-receipt.md`.

No new source file under `src/`. `tests/Parchwell.ArchitectureTests/B10AbruptFailureSiteTests.cs` scans a fixed list of 12 source files. The new guard site must therefore stay in `B10AbruptHost.cs` or `B10AbruptProducer.cs`.

### 2.2 Paths and values that must not change

Paths: everything not in 2.1, in particular:

- `src/Parchwell.Kafka/**`, including `KafkaStatisticsSession` and its codes;
- `src/Parchwell.LoadGen/Run/Evidence/**`;
- `B10AbruptStatistics.cs`, `B10AbruptPublisher.cs` and `B10AbruptSupervisor.cs`;
- `infra/**`, `charts/**`, `scenarios/**`, `results/**`, `.github/**`;
- every `docs/**` file except the one line in 2.1;
- `AGENTS.md`, `CLAUDE.md`, any `*.csproj`, `Directory.*.props` and `*.sln*`.

No new project, assembly reference or NuGet package.

Values: these timeouts and intervals keep their `main` values (decision 6751199288):

| Value | Setting on `main` | Source |
|---|---|---|
| Statistics `CompletionTimeout` | 20 s | `B10AbruptHost.cs` line 190 |
| Evidence completion timeout | 20 s | line 218 |
| `CleanupTimeout` | 10 s | line 252 |
| `StatisticsCompletionTimeout` | 20 s | line 254 |
| Host cleanup | 10 s | line 95 |
| Process lifetime and watchdog | 900 s | lines 50-51 |
| `statistics.interval.ms` | 15000 | docs/blueprint.md line 708 |
| Post-drain poll | 10 ms | `B10AbruptProducer.cs` line 570 |

The other diagnostic caps also stay as they are: `MaximumChainDepth` 8, `MaximumStageFacts` 8, `MaximumTimelineEntries` 48 (`B10AbruptDiagnostics.cs` line 39), and the 65536-byte report limit.

## 3. External contracts

E1. Failure name. Two conditions at evidence completion fail with the existing closed name `b10_abrupt_guard_failed`, as the message of an `InvalidDataException`: an incomplete statistics completion, or a faulted evidence session. There is no new failure name. The failure-line format of `B10AbruptHost.ReportFailure` is unchanged.

E2. Guard facts at that failure site: exactly 36 facts, in this order, each a 64-bit integer.

- Each name is the snake-case conversion of a new `B10AbruptFact` member (`B10AbruptDiagnostics.cs` line 97).
- Facts 1 and 2 reuse the existing members `CompletionComplete` and `EvidenceFaulted`.
- The members for facts 3 to 36 are new. Each one is the PascalCase form of its name, for example `MissingBrokerCount` and `DiagnosticClockUnverified`.

| # | Fact name | Value |
|---|---|---|
| 1 | `completion_complete` | 1 when `KafkaStatisticsCompletion.IsComplete`, else 0 |
| 2 | `evidence_faulted` | 1 when `RunEvidenceSession.IsFaulted` after its completion, else 0 |
| 3 | `missing_broker_count` | `MissingBrokers.Length` |
| 4 | `diagnostic_count` | `Diagnostics.Length` (distinct codes, as the session returns them) |
| 5 to 36 | `diagnostic_<code>` | 1 when `Diagnostics` contains `<code>`, else 0, for the 32 codes below in this order |

The 32 codes are every diagnostic code the session can emit. A planning-session script enumerated them, and they are listed in order of first appearance, `KafkaStatisticsSession.cs` ("session" lines) first, then `KafkaStatisticsParser.cs` ("parser" lines). Parser codes reach the session's set through `AddDiagnostic(diagnostic.Code)` at session 358.

| Fact | Code | Emitted at |
|---|---|---|
| 5 | `clock_unverified` | session 74 |
| 6 | `late_capture` | session 97, session 117 |
| 7 | `capture_contention` | session 110 |
| 8 | `oversized_snapshot` | session 123 |
| 9 | `capture_queue_overflow` | session 129 |
| 10 | `disposed_before_completion` | session 200 |
| 11 | `completion_cancelled` | session 243, session 260, session 270, session 637 |
| 12 | `sink_drain_timeout` | session 244, session 261, session 307 |
| 13 | `completion_timeout` | session 244, session 261, session 642 |
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
| 30 | `worker_cancellation_failure` | session 733, session 751 |
| 31 | `invalid_json` | parser 27, parser 42 |
| 32 | `invalid_broker_series_limit` | parser 32 |
| 33 | `duplicate_property` | parser 50 |
| 34 | `invalid_root` | parser 55 |
| 35 | `invalid_or_missing_field` | parser 65, parser 70, parser 112 |
| 36 | `broker_name_mismatch` | parser 128 |

The enumeration matched every `AddDiagnostic`, `AddDiagnosticThreadSafe` and `AddReportDiagnostic` call, the two completion ternaries, the `capture_loss` append, and every parser `Failure(...)` and `code = ...` site. One false positive was dropped: the literal `consumer` at session 448, which is a client type, not a code. `invalid_broker_series_limit` is listed because the parser can emit it. Whether `KafkaStatisticsOptions` validation makes it unreachable through the session was not checked.

Reading rule, stated here and in the receipt only: `diagnostic_count` minus the sum of facts 5 to 36 counts codes that are not in this list, for example codes added to the session later.

E3. Successor outputs are unchanged:

- the five payload names `ledger.jsonl`, `manifest.json`, `statistics.jsonl`, `request-lifecycle.jsonl` and `capabilities.json` (`B10AbruptProducer.cs` line 45);
- the `SuccessorReceipt` members;
- the stage names and the timeline format.

There is no new stage, because the child stage list is design text (docs/operations/b10-abrupt-contract.md line 166). No Kafka client setting is added or changed.

E4. `MaximumSiteFacts` changes from 16 to 36. This is a policy choice with no measured basis (decision 6751449502). It is 4 summary facts plus 32 codes, the smallest cap that keeps E2 whole. It applies to every abrupt failure site. No other site's facts change.

E5. docs/operations/b10-abrupt-contract.md line 188. Only its opening words change. Before: `Policy caps, not measurements: 8 chain entries, 16 facts per site, 8 facts per timeline entry`. After: `Policy caps, not measurements: 8 chain entries, 36 facts per site ([decision 6751449502](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6751449502)), 8 facts per timeline entry`. The rest of the line is byte-identical.

## 4. Requirements

The requirements cover the successor path only: `B10AbruptProducer.RunSuccessorAsync` (`B10AbruptProducer.cs` lines 444-523 on `main`) and the host members it calls.

R1. Statistics completion while live.
- WHEN the successor has sealed its `cross_restart` cohort (after `cohort_sealed`, line 486),
- THEN it requests statistics completion exactly once, through `KafkaStatisticsSession.CompleteAsync(expectedBrokerNames, token)`,
- with the broker names that `ReadExpectedBrokersAsync` returned,
- while the native producer has not been disposed.

R2. Disposal, then evidence.
- WHEN that statistics completion returns, complete or not,
- THEN the successor disposes the native producer exactly once through the existing `CleanupAsync` (`CleanupTimeout` unchanged),
- and only after disposal returns does it call `RunEvidenceSession.CompleteAsync(completion, token)` exactly once, with the completion object from R1.

R3. Guard failure.
- WHEN, after R2, `completion.IsComplete` is false or `evidence.IsFaulted` is true,
- THEN it throws `b10_abrupt_guard_failed` with exactly the E2 facts,
- and writes none of `statistics.jsonl`, `request-lifecycle.jsonl` or `capabilities.json` under `<OutputRoot>/successor/`.

R4. Success path unchanged.
- WHEN, after R2, the completion is complete and evidence is not faulted,
- THEN the existing steps run unchanged: `CheckGuards`; the copy loop of lines 492-506 with its read limit of 134217728 bytes; `CheckGuards` again; the return of `SuccessorArtifacts`.

R5. Disposal-time error callbacks.
- WHEN a native error callback reaches `RunEvidenceSession.CaptureError` during disposal,
- THEN it is captured, with no `error_capture_unavailable` (`RunEvidenceSession.cs` lines 248-253).
- For an error code other than `NoError`, the post-completion `CheckGuards` then throws `b10_abrupt_guard_failed` with `native_error_observed` 1.

R6. Late statistics callback.
- WHEN a statistics callback arrives after statistics completion and before or during disposal,
- THEN the successor still succeeds: the completion object is unchanged, evidence is not faulted, and `B10AbruptStatistics` raises no guard.

R7. Statistics completion throws.
- WHEN statistics completion throws,
- THEN the same exception propagates unchanged, and the existing `finally` (line 522) disposes the producer exactly once.
- Evidence completion is not called, so no `capabilities.json` is written under the capture directory.
- No catch is added.

R8. First instance and earlier cohorts untouched.
- WHEN the first instance runs `RunFirstAsync`, or the timeout and control cohorts run,
- THEN neither statistics completion nor evidence completion is called (docs/operations/b10-abrupt-contract.md line 51).
- Covered by:
  - the existing tests `Given_TimeoutInDrain_When_RunningCohort_Then_RetriesOnceWithSeparateInventory` (line 691), `Given_CompletedControlOriginal_When_ReleaseArrives_Then_ResendsOnceAndAdmitsKillWithoutCleanup` (line 778) and `Given_ControlArm_When_Sealed_Then_InventoryHasSixEntriesWithMemberSequences` (line 814);
  - acceptance command 12, which shows the completion members are invoked only inside `RunSuccessorAsync`.

R9. Test-only branch untouched.
- WHEN a test supplies no completion members,
- THEN the test-only branch (lines 507-516) runs unchanged.
- Covered by the existing tests `Given_SuccessorEntrySequence_When_Bound_Then_InBurstOnly` (line 896) and `Given_SealedKillSelection_When_Replaying_Then_InvokesOnceWithoutRetryAndDrains` (line 1009).

R10. One production statistics handler.
- WHEN the host builds the successor's statistics handler,
- THEN it uses one internal member that calls `B10AbruptStatistics.CaptureNative(raw)` and then `KafkaStatisticsSession.Capture(raw)`.
- `CreateProducerAsync` (line 232) and tests T1 to T9 use that same member.

R11. Facts kept whole.
- WHEN a failure site attaches up to 36 distinct facts,
- THEN all of them are kept in order; a 37th distinct fact is dropped (E4).

## 5. Invariants and safety

- I1. Order on the successor path: statistics completion, then disposal, then evidence completion, then guard. No other order is allowed. The precedent is `RunExecutor.cs` on `main`: it completes while live at lines 368 and 388, disposes at line 419, and completes evidence at line 438.
- I2. The statistics session completes at most once per process: a second call throws (`KafkaStatisticsSession.cs` lines 187-190).
- I3. AGENTS.md hard safety rules hold: no Azure credential, no workflow dispatch, no `.github/` change, no trial. Verdict integrity holds: nothing under `results/` and no expected outcome.
- I4. No number from this plan's estimates (section 8) enters code or docs. The estimates stay in this plan and the receipt.

## 6. Never-ran inventory

Evidence is the trial logs, not the code. Seven B10 abrupt diagnostic runs exist (the run list of workflow `b10-abrupt-trial.yml`, read 2026-11-09). Each artifact was downloaded with `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/<id>/zip` into an empty scratch directory and treated as untrusted data. The fresh check reproduced all seven zip hashes.

| Run | Source | Artifact | Zip SHA-256 | Successor job started |
|---|---|---|---|---|
| [42460966764](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42460966764) | `2d4792ee` | 13609836611 | `bbc4030f30fceef33c156c7b00d8533342c7415887b9d59afab7f52db68a78e7` | no |
| [42475024856](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42475024856) | `b4d6f9e8` | 13615377394 | `270597b9f683416aa72ef5230e90bf896973ac7b3fb9c9030bcbbcd1f6a75c83` | no |
| [42512105140](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42512105140) | `25cc301a` | 13629179730 | `a5b9c345121eecfc9b836706101ee776cbd3269d57e4150a04a4a632917ef9ad` | no |
| [42536075365](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42536075365) | `ee36f1c6` | 13639442741 | `0564049fa591fdfcdd8d4cfbac36794d43498cac72aeccec12666e800979af69` | no |
| [42608381540](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42608381540) | `4163cd1a` | 13668809132 | `0c22924e47ed6b455e228f98be5016c6e3c97faab541d9ed2f9853bf1005eb30` | no |
| [42670600125](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42670600125) | `fa73aa08` | 13699980854 | `b2e2c270f6133c86341eafd1aa5c95a4f0c391a9ecce2b6134707d19a4341bd1` | no |
| [42679791413](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42679791413) (trial 7) | `f5cc496f` | 13704155185 | `bee17231882ffeb7ddc3134ba10a00ec4590f20e96dad409c050decf1f22cdcf` | yes |

"Successor job started" is no for the first six runs: none of their artifacts contains a `loadgen2` directory or any `"role":"successor"` line (checked by script). Run 42670600125 is not linked from issue #404; it is listed because the workflow ran it.

Trial 7's successor log is `b10-42679791413-1-c1-r1-loadgen2/b10-42679791413-1-c1-r1-loadgen2-prrgv.containers.loadgen.log`, SHA-256 `fce88054fccf70591da6f334b7299e446a7ec66cd7b6814fec8740cd17e7209f`. "L<n>" below is a line number in that log.

| Stage on the successor path | Ran live? Evidence | Test in this plan that runs it for real |
|---|---|---|
| S1. Predecessor binding load and validation in the host | Yes, trial 7 only. L25 `producer_constructed` follows it (`B10AbruptHost.cs` lines 76-77, 116-121) | Not changed. Existing production-binding cases in `B10AbruptWireProducerTests` |
| S2. Selected-entry validation in the producer (`B10AbruptProducer.cs` lines 449-466) | Yes. L25 follows it, because `producer_constructed` is marked at line 104 inside `Initialize`, after these checks | Not changed. Existing `Given_SuccessorEntrySequence_When_Bound_Then_InBurstOnly` |
| S3. Successor registration upload | Yes: artifact file `b10-42679791413-1-c1-r1-loadgen2-prrgv/registration.json` (kind `registration`) | Not changed |
| S4. Producer and statistics session construction | Yes: L25 | T1 to T10 build a real session through `KafkaStatisticsBridgeFactory`. T9 builds a real librdkafka client |
| S5. Release wait | Yes: L51 `release_reached` | T1 (real `WaitForReleaseAsync`) |
| S6. Resend produce and terminal | Yes: L52 `probe_terminal` | T1 (fake native producer) |
| S7. Drain | Yes: L53 `cohort_drained` | T1 |
| S8. Post-drain report wait through `B10AbruptStatistics` | Yes: L54 `post_drain_report`, after 10896 ms, with 7 reports. Inference: `SelectPostDrain` (`B10AbruptStatistics.cs` lines 258-271) accepted a live report that covered the expected brokers, with a native timestamp above the drain boundary. That is live evidence for D1's report content | T1 (real adapter, real session sink chain) |
| S9. `CheckGuards` before the seal (line 484) | Yes: L55 follows it | Not changed |
| S10. Cohort seal | Yes: L55 `cohort_sealed` | T1 |
| S11. Statistics completion while the producer is live (changed) | Never. Trial 7 ran `CompleteAsync` only after disposal. No line shows the call; L56 shows its result, `completion_complete:0`, at `B10AbruptHost.cs` line 249 | T1 (complete), T2 (timeout), T8 (throws), T9 (real client, complete while live) |
| S12. Producer disposal after statistics completion (changed position) | Never in this position. Disposal before completion ran in trial 7 (code order, lines 488-490), but no stage line records it | T1, T4, T5 (fake native `Dispose`). T9 disposes a real client after completion: about 100 ms observed |
| S13. Evidence completion after disposal (changed position) | Ran once in trial 7, with an incomplete statistics completion. L56 was thrown at host line 249, after line 248 completed evidence (inferred from code order; no line records it) | T1, T2, T4, T5, T9 with a real `RunEvidenceSession` |
| S14. Guard with the E2 facts (changed) | The old two-fact guard ran: L56 `{"completion_complete":0,"evidence_faulted":0}`. The new facts never ran | T2, T5, T6, T10 |
| S15. Post-completion `CheckGuards` (line 491) | Never; no line | T4 (`Local_Fail` case) |
| S16. Copy of the three evidence artifacts into `successor/` | Never; no line | T1, T7 |
| S17. Failure report `ReportFailure` | Yes: L56 is its output | Not changed |
| S18. Production wiring inside `CreateProducerAsync`: the completion members with `brokers`, and the statistics handler at line 232 | Never; no offline test calls `CreateProducerAsync` with a native client | Partly. The handler member is shared with T1 to T9 (R10), and T9 drives it from a real client. The members `CreateProducerAsync` passes, and the broker names it reads, are run by no offline test: deployment assumption D7 |
| S19. Host cleanup: statistics and evidence `DisposeAsync`, host stop and dispose within 10 s (lines 95-101) | Never; no line. Trial 7 failed before it | Partly: T1 disposes the real session and evidence after a completed run. Host stop and dispose: deployment assumption D5 |
| S20. Read of successor payloads and `PublishSuccessorAsync` (lines 102-106) | Never. `phase-status.json` records `b10_abrupt_publication_ambiguous` at boundaries `successor_job` (`b10_abrupt.py` line 591) and `successor_publication` (line 394): the objects were missing | T7 runs the real `PublishSuccessorAsync` against the test `FileStorage`. The blob upload itself is deployment assumption D3 |
| S21. Successor process exit 0 and Job completion | Never. `loadgen2-pods.json` shows exit code 1, started 19:20:51Z, finished 19:22:58Z | Deployment assumption D4 |
| S22. Workflow download of `SUCCESSOR_FILES` | Ran and failed (S20 evidence). The same `download` function succeeded live for `FIRST_FILES`: trial 7 reached boundary `successor_admission` (`b10_abrupt.py` lines 583-587) | Deployment assumption D4 |
| S23. Workflow successor receipt validation, `collect(files, observation)` (`b10_abrupt.py` lines 604-633) | Never | T7, then `check_b10_abrupt_producers.py`. Its `test_produced_first_and_successor_transport` calls `collect` on the produced bytes (`test_b10_abrupt.py` lines 239-244) |

Why the existing tests missed S11:

- Every successor test on `main` that supplies a completion callback uses a hand-written one. It calls `Evidence.CompleteAsync(null, token)` and never a statistics session (`B10AbruptProducerTests.cs` lines 1289-1294).
- The produced-byte exporter passes no callback at all, so it takes the test-only branch (`B10AbruptWireProducerTests.cs` lines 371-373; `B10AbruptProducer.cs` lines 507-516). The exported successor `statistics.jsonl` and `capabilities.json` on `main` are therefore not the production evidence files.

## 7. Test map

### 7.1 Common inputs

All tests are in `tests/Parchwell.LoadGen.Tests/Abrupt/`, and names follow docs/engineering/dotnet.md line 62.

"Real chain" means all of the following:

- a real `RunEvidenceSession`, with the options from `B10AbruptHost.cs` line 218 and no expectations;
- a real `B10AbruptStatistics`, with `ActivateNativeCaptureTracking()` and that evidence session as its downstream;
- a real `RunEvidenceSinkRouter` attached to it;
- a real `KafkaStatisticsSession` from `new KafkaStatisticsBridgeFactory(options, clock, router, metrics, nativeClockCompatible: true, completionBoundaryObserver)`;
- the production completion members that `CreateProducerAsync` passes to `B10AbruptProducer`, invoked by `RunSuccessorAsync` (or, in T9, called directly in the I1 order);
- the production statistics handler member (R10).

Only the native `IProducer` (`B10AbruptProducerTests.NativeProducer`; real in T9) and blob storage (`FileStorage`, in T7) are fakes. A hand-written completion delegate does not count as the real chain.

Clock, T1 to T5 and T8:
- A test `TimeProvider` wraps `TimeProvider.System`. `GetTimestamp`, `TimestampFrequency` and `GetUtcNow` pass through.
- `CreateTimer` passes through. Once, when a timer with a due time of 10 ms is created, it first delivers report P1 synchronously.
- T is the clock's timestamp at delivery, converted to microseconds with floor rounding.
- `ReleaseBoundaryClock` is not usable here: it never advances, so P1 and P2 could never qualify.

Statistics report input: JSON `{"name":"native","client_id":"client","type":"producer","ts":T,"time":100,"age":T-C,"brokers":{"broker":{"name":"broker","nodeid":0,"source":"learned","state":"UP","stateage":0,"req_timeouts":0,"disconnects":0,"txretries":0,"txerrs":0,"rxerrs":0,"throttle":{"cnt":1,"max":0},"rtt":{"cnt":1,"p99":0}}}}`. C is T captured once, when the native producer is constructed. The expected brokers are `["broker"]`. Each report goes through the production handler member (R10).

Delivery points are deterministic; there is no free-running timer:
- P1: one report, from the clock hook above, when the successor's post-drain poll creates its first 10 ms delay timer.
- P2: one report from the completion boundary observer, delivered only while the fake producer's `Disposals` is 0. This models a disposed client that emits no statistics.
- P3: where a test says so, one report from the fake producer's `Dispose`.

Why deterministic delivery is required: in this planning session's scratch prototype, a free-running feed at 2 ms or 20 ms made the real chain fail intermittently. The causes were `B10AbruptStatistics` guard reason 3 (`Monitor.TryEnter` contention at lines 129-133 against the 10 ms `SelectPostDrain` poll) and session capture contention. With P1 to P3, the prototype passed 50 of 50 runs.

Session options, T1 to T5 and T8: `MaxSnapshotCharacters` 1048576, `QueueCapacity` 2048, `MaxBrokerSeries` 64, `CompletionTimeout` 2 s.

Successor inputs, T1 to T5 and T8:
- Release: the clock's UTC now plus 200 ms.
- Selection: one entry, attempt `predecessor-cross_restart-0`, key `B10/run-1/C1/1/cross_restart`, sequence 0, payload `[1,2,3]`.

### 7.2 Tests

| Test | Covers | Input | Literal expected values |
|---|---|---|---|
| T1 `Given_LiveProducerAfterSeal_When_SuccessorCompletes_Then_StatisticsCompleteBeforeDisposalAndEvidenceAfter` | R1, R2, R4, R6, I1 | P1, P2, P3 | No exception. `Disposals` 1. The boundary observer ran before `Dispose` (a flag recorded at `Dispose` is true). `evidence.IsFaulted` false. All five `SuccessorArtifacts.Paths` exist. `successor/statistics.jsonl` has exactly 2 lines: P1 and P2. P3 is a late capture and is not written. `successor/request-lifecycle.jsonl` has length 0. `successor/capabilities.json` member `statistics_completion` matches `^\{"is_complete":true,"drain_boundary_microseconds":[0-9]+,"diagnostics":\[\],"missing_brokers":\[\]\}$` and `expectations` is null. In production, `RunEvidenceExpectations.CaptureOnly(role, true)` adds a fifth member, `first_post_drain_timestamp_microseconds` (`RunEvidenceSession.cs` lines 562-569) |
| T2 `Given_NoPostBoundaryReport_When_SuccessorCompletes_Then_GuardFailsWithTimeoutFacts` | R2, R3, E1, E2 | P1 only | `InvalidDataException`, message `b10_abrupt_guard_failed`. Site facts exactly the 36 E2 facts in order, with values 0, 0, 1, 1, then `diagnostic_completion_timeout` (fact 13) 1 and every other flag 0. `Disposals` 1. Elapsed at least 2000 ms. `successor/statistics.jsonl` absent |
| T4 `Given_DisposalErrorCallback_When_CompletingSuccessor_Then_CapturesBeforeFinalizingAndAppliesGuard`, both cases, moved from `B10AbruptProducerTests.cs` onto the real chain | R5 | P1, P2. `Dispose` calls `CaptureError(new Error(code, "synthetic disposal callback"))` | `NoError`: success; evidence not faulted. `Local_Fail`: `b10_abrupt_guard_failed` thrown in `CheckGuards`, with `native_error_observed` 1 and `evidence_faulted` 0; `successor/capabilities.json` and `successor/request-lifecycle.jsonl` absent. Both cases: `Disposals` 1; `evidence.Diagnostics` has no `error_capture_unavailable`; `capture/request-lifecycle.jsonl` contains `synthetic disposal callback` |
| T5 `Given_EvidenceFaultDuringDisposal_When_SuccessorCompletes_Then_GuardFailsWithEvidenceFaulted` | R3, E2 | P1, P2. `Dispose` calls `RecordDiagnostic(" ")`, which latches `invalid_diagnostic` | `b10_abrupt_guard_failed`. Facts 1 to 4 are 1, 1, 0, 0, and facts 5 to 36 are all 0 |
| T6 `Given_StatisticsCompletion_When_GuardFactsAreBuilt_Then_EachFlagMapsOneCode` (35 cases) | E2, reading rule | A `KafkaStatisticsCompletion` built directly with `IsComplete` false. (a) no diagnostics, no missing brokers, evidence faulted. (b) one code `synthetic_unknown_code`, missing `["a","b"]`. (c) all 32 codes plus `synthetic_unknown_code`, missing `["a","b"]`. (d1 to d32) exactly one listed code, the k-th in E2 order, missing `["a","b"]` | Facts 1 to 4: (a) 0, 1, 0, 0; (b) 0, 0, 2, 1; (c) 0, 0, 2, 33; (dk) 0, 0, 2, 1. Flags: (a) and (b) all 0; (c) all 1; (dk) only fact 4+k is 1. Exactly 36 facts, with names in E2 order, in every case |
| T7 existing exporter `Given_SyntheticProducer_When_SupervisorAndSuccessorRun_Then_ActualBytesBindBothPublications`, successor half moved onto the real chain | R1 to R4, S16, S20, S23 | P1 (through a wrapper of the exporter's `ReleaseClock` with the same 10 ms hook), P2. Session options as above, but `CompletionTimeout` 20 s. Remove the existing 2 ms `secondReports` feed and the `OnFlush` direct `Observe`. Inference: a direct `Observe` without `CaptureNative` trips guard reason 6 under `ActivateNativeCaptureTracking` (`B10AbruptStatistics.cs` lines 177-182) | Exporter class: 8 passed. Then `check_b10_abrupt_producers.py`: 12 tests OK and marker `b10_abrupt_produced_bytes_checked`. Exported `successor/statistics.jsonl` has 2 lines, with members `diagnostics, identity, raw_json, receipt_time, received_at, schema_version, session_id, statistics_identity, typed_snapshot`. Exported `successor/capabilities.json` has `statistics_completion.is_complete` true |
| T8 `Given_StatisticsCompletionThrows_When_SuccessorCompletes_Then_ExceptionPropagatesAndProducerDisposedOnce` | R7 | P1. The boundary observer throws `new InvalidOperationException("synthetic_boundary_failure")`. `KafkaStatisticsSession.cs` lines 205-207 document that seam as failing closed through the caller | `RunSuccessorAsync` throws `InvalidOperationException` with message `synthetic_boundary_failure`, not `b10_abrupt_guard_failed`. `Disposals` 1. `capture/capabilities.json` absent. `successor/statistics.jsonl` absent |
| T9 `Given_RealNativeProducerWithoutBroker_When_CompletionMembersRun_Then_StatisticsCompleteWhileLiveAndEvidenceAfterDisposal` | R1, R2, R10, I1, S11, S12 | Real chain on `TimeProvider.System`. Real `ProducerBuilder<byte[], byte[]>` with `bootstrap.servers` `127.0.0.1:1` (nothing listening), `client.id` `client`, `statistics.interval.ms` 200 (test only). Session `CompletionTimeout` 20 s. Expected brokers `["127.0.0.1:1/bootstrap"]`. Production handler member; log and error handlers as at `B10AbruptHost.cs` lines 232-233. Wait 500 ms, then call the production completion members in the I1 order: statistics completion, `Dispose()`, evidence completion | No exception. `capture/capabilities.json` `statistics_completion` matches the T1 pattern. `evidence.IsFaulted` false. `evidence.GuardObservations` is not asserted: the connection-refused error callback adds `native_error_callback_observed` here |
| T10 `Given_AllStatisticsCodes_When_GuardFactsAreAttached_Then_CapKeepsAllThirtySix` | R11, E4 | The facts for a completion carrying all 32 codes, plus one extra distinct fact (`Arm` 2), attached at one site | `MaximumSiteFacts` is 36. The described site has exactly the 36 E2 names, in order, and no `arm` fact |
| T11 existing `B10AbruptDiagnosticsTests.Given_ExcessFactsAndFractionalMilliseconds_When_Attached_Then_FirstSixteenAndWholeMillisecondsRemain`, renamed to `...FirstThirtySixAndWholeMillisecondsRemain` | R11 | The first 37 `B10AbruptFact` values | 36 facts are kept, equal to the first 36 names in enum order. The millisecond assertion is unchanged (1999). The change follows decision 6751449502; no assertion is loosened beyond the cap value |

Observed in the scratch prototype, 3 of 3 runs of T9:
- completion wait 98 to 102 ms;
- `Dispose()` 96 to 102 ms;
- 3 reports reached the adapter;
- `statistics_completion` `{"is_complete":true,"drain_boundary_microseconds":<n>,"diagnostics":[],"missing_brokers":[]}`.
The fresh check reported 103 to 105 ms and 96 to 97 ms. These are observations, not bounds.

Failure-report size (decision 6751449502), computed by script in the prototype with every one of the 36 facts at `long.MinValue`:
- the facts object is 1253 UTF-8 bytes;
- the successor's plain failure line with a one-entry exception chain is 2306 bytes;
- both are under the 65536-byte limit, so the line was not reduced.
No other site's facts change, so `failed_diagnostic` sizes elsewhere are unchanged.

### 7.3 Mutations

Each mutation is scratch only and never committed. The receipt records the result of each. M1 to M9 are the fresh checker's list, relayed verbatim by the control session; M10 to M15 are this planner's. Observed on the prototype (43 cases in the new class plus `B10AbruptDiagnosticsTests`). The production files were restored byte-identical after the run.

| Id | Mutation | Killed by (observed) |
|---|---|---|
| M1 | Statistics completion moved after disposal | T1, T4 x2, T5 |
| M2 | Evidence completion before disposal | T4 x2, T5 |
| M3 | Dispose, then statistics completion inside the evidence member, then evidence (the `main` shape) | T1, T4 x2, T5, T9 |
| M4 | R3 guard removed | T2, T5 |
| M5 | Guard ignores `evidence.IsFaulted` | T5 |
| M6 | Flags for `late_capture` and `capture_contention` swapped | T6 (all 35), T2, T5, T10 |
| M7 | `CheckGuards` after evidence completion removed | T4 `Local_Fail` |
| M8 | Statistics-completion exception swallowed (catch returns `new KafkaStatisticsCompletion(false, [], [])`) | T8 |
| M9 | `diagnostic_count` computed as the number of listed codes present | T6 b, c |
| M10 (planner) | Capture-only `CompleteAsync` overload, without the broker inventory | T2 |
| M11 (planner) | Handler drops `CaptureNative` | T1, T2, T4 x2, T5, T8 |
| M12 (planner) | `MaximumSiteFacts` left at 16 | T2, T5, T10, T11 |
| M13 (planner) | Last flag (`broker_name_mismatch`) removed | T6 (all 35), T2, T5, T10 |
| M14 (planner) | Parser flags `invalid_json` and `invalid_broker_series_limit` swapped | T6 (all 35), T2, T5, T10 |
| M15 (planner) | `missing_broker_count` fixed at 0 | T6 b, c, d1 to d32, T2 |

## 8. Timing and margin (estimates)

Estimate, 2026-11-09. Basis: the trial 7 stage lines, and the hypothesis that librdkafka emits a report every 15000 ms counted from producer construction. Computed by script:

| Process | Construction (ms) | Report number at `post_drain_report` | Predicted (ms) | Observed (ms) | Lag (ms) |
|---|---|---|---|---|---|
| Successor | 66 (L25) | 7 | 105066 | 105079 (L54) | 13 |
| Child, timeout arm | 67 | 3 | 45067 | 45079 | 12 |
| Child, control arm | 67 | 12 | 180067 | 180087 | 20 |

Under the fix, statistics completion would start at about `cohort_sealed` (105093 ms, L55, 14 ms after L54). The next report is predicted at 120066 ms plus lag. The slack against the 20000 ms timeout is therefore 20000 - (120066 - 105093) - lag: about 5027 ms at zero lag, and 5007 to 5015 ms at the observed lags of 12 to 20 ms. In the worst case, completion starts just after a report: slack is about 5000 ms minus lag. This is an inference from three samples, not a measured bound. Hari kept both values (decision 6751199288).

Trial 7's guard line has no timestamp. Bounds, computed by script:

- Pod start 19:20:51Z and finish 19:22:58Z, at whole-second resolution.
- MSAL line 24 (19:20:52Z) precedes L25 (origin + 66 ms), so the timeline origin is at least 19:20:51.934.
- A 19:20:53Z MSAL line follows L26 (origin + 74 ms), so the origin is at most 19:20:53.925.
- Therefore `cohort_sealed` to container exit lay between 18.982 s and 21.972 s.

That is consistent with the 20 s completion timeout. The cause is established by the offline reproduction (M1, M3, and T1 failing on `main`), not by these bounds.

Inference: on `main`'s order (dispose, 20 s completion timeout, evidence completion, `ReportFailure`, exit), the same bounds limit disposal, evidence completion, `ReportFailure` and exit together to at most about 1.97 s beyond the 20 s timeout. This is live evidence for D2.

## 9. Deployment assumptions

No offline test runs these for real. Each is an open question until Hari accepts the risk by name (factory.md "Item lifecycle" steps 1 and 3).

- D1. After the successor's seal, the live librdkafka client delivers at least one report naming every expected broker within the 20 s completion timeout. Basis: section 8 (estimate); S8's L54 as live evidence of report content (inference); T9 with a real client but no broker.
- D2. Native disposal of the successor producer, after statistics completion, returns within `CleanupTimeout` 10 s. Basis: T9 observed about 100 ms with no broker; section 8 bounds disposal and the following steps together to about 1.97 s live, on `main`'s order (inference).
- D3. Azure Blob create-only uploads of the five successor payloads and `completion.json` succeed under the successor pod's workload identity. The same publisher and storage class uploaded the first pod's files and the successor's `registration.json` live in trial 7 (S3, S22).
- D4. The successor container exits 0 within the Job deadline (`loadgen_job_seconds` 1140, `charts/parchwell/values.yaml` line 98), the workflow records `loadgen2` complete, and `download` retrieves `SUCCESSOR_FILES`. That includes an empty `request-lifecycle.jsonl`: `b10_abrupt.py` line 392 sets minimum 0 for that name.
- D5. Host stop and dispose of the successor (S19) finish within 10 s.
- D6. Two capture races.
  - First race. `B10AbruptStatistics.CaptureNative` uses `Monitor.TryEnter`, and it can collide with the 10 ms `SelectPostDrain` poll (guard reason 3, lines 129-133). This exists on `main` in every post-drain wait and is unchanged here.
  - Second race. On `main`'s successor path, `CleanupAsync` (`B10AbruptProducer.cs` line 488) runs before statistics completion, so no statistics callback can land after `CloseCapture`. This pull request makes the race reachable there, as it already is in `RunExecutor` (completion while live at lines 368 and 388, disposal at line 419).
  - In the second race, a callback that lands between `CloseCapture` and `SealFlags` meets `!_captureOpen`. It records capture loss and `late_capture` (`KafkaStatisticsSession.cs` lines 95-118). `BuildCompletion` then appends `capture_loss` (line 632), and the completion is incomplete. `capture_contention` (line 110) behaves the same way.
  - The window is the worker drain between `CloseCapture` and `SealFlags`, measured against the 15 s statistics interval.
  - The live probability of neither race is measured. If either fires, the new facts name it (`diagnostic_capture_loss`, `diagnostic_capture_contention`, `diagnostic_late_capture`).
- D7. `CreateProducerAsync` passes the production completion members with the broker names from `ReadExpectedBrokersAsync`, and the handler member, to the producer (S18). No offline test calls `CreateProducerAsync` with a native client. Code review and acceptance command 12 check the wiring.

## 10. Acceptance commands

Run from the repository root. "Observed" is what this planning session saw on 2026-11-09, in one of two places:
- unmodified `main` `f5cc496`;
- a scratch prototype of Revision 2: a worktree of `f5cc496` plus prototypes of R1 to R11, E2, E4, E5 and tests T1, T2, T4 to T11, laid out in the 2.1 files. It was never pushed.
The implementer records its own results in the receipt.

1. Restore.
   - Command: `dotnet restore tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --locked-mode`
   - Observed: exit 0. A fresh worktree prints `Restored` lines; a warm one prints `All projects are up-to-date for restore.`
   - Expected after: exit 0.
2. Build.
   - Command: `dotnet build tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj -c Release --no-restore`
   - Observed on `main` and on the prototype: `0 Warning(s)`, `0 Error(s)`, exit 0.
   - Expected after: the same.
3. Burst suite.
   - Command: `dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj -c Release --no-build -- --filter-class "*B10Abrupt*"`
   - Observed on `main`: total 457, succeeded 456, skipped 1 (`B10AbruptPublishedApphostTests`, reserved to CI).
   - Observed on the prototype: total 498, succeeded 497, skipped 1, failed 0. That is 457, minus T4's 2 cases moved, plus the 43 cases of the new class.
   - Expected after: total 498, succeeded 497, skipped 1, failed 0.
4. Repetition gate, 50 runs of the new class. The count of 50 is a plan choice, not a measurement. The class includes T4, so the gate repeats the disposal-callback path.
   - Command: `for i in $(seq 1 50); do dotnet tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptSuccessorCompletionTests 2>&1 | grep -o 'Total: [0-9]*, Errors: [0-9]*, Failed: [0-9]*'; done | sort | uniq -c`
   - Observed on the prototype: `50 Total: 43, Errors: 0, Failed: 0`. The earlier 22-case layout also gave `50 Total: 22, Errors: 0, Failed: 0` while the full `main` verification ran in parallel.
   - Expected after: `50 Total: 43, Errors: 0, Failed: 0`.
5. Exporter.
   - Command: `W=$(mktemp -d)`; `PARCHWELL_B10_ABRUPT_WIRE_OUTPUT="$W" dotnet tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptWireProducerTests`
   - Observed on the prototype, 10 of 10 runs: `Total: 8, Errors: 0, Failed: 0, Skipped: 0`.
   - Expected after: the same.
6. Produced-byte check.
   - Command, immediately on the same `$W`: `python3 infra/scripts/phase/tests/check_b10_abrupt_producers.py "$W"`
   - Observed on the prototype, 10 of 10 runs: `Ran 12 tests`, `OK`, last line `b10_abrupt_produced_bytes_checked`. Successor `statistics.jsonl` has 2 lines with the T7 members. `capabilities.json` has `statistics_completion` = `{"is_complete": true, "drain_boundary_microseconds": <n>, "diagnostics": [], "missing_brokers": []}`.
   - Expected after: the same.
7. Phase-script tests.
   - Command: `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_b10_abrupt.py'`
   - Observed: `Ran 12 tests`, `OK`.
   - Expected after: the same (no Python change).
8. Architecture test.
   - Command: `dotnet build tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj -c Release`, then `PARCHWELL_REPOSITORY_ROOT=$PWD dotnet test --project tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj -c Release --no-build -- --filter-class "*B10Abrupt*"`
   - Observed on the prototype: build 0 warnings, 0 errors; total 1, succeeded 1.
   - Expected after: the same.
9. Mutations M1 to M15 (section 7.3).
   - Observed on the prototype: every mutation built with 0 errors and was killed by the tests listed.
   - Expected after: each mutation is killed by at least its listed tests, and production is restored byte-identical afterwards.
10. Documentation check.
    - Command: `npm run check:docs`
    - Observed on `main`: `8 warnings`, all in files this plan does not change. With the E5 line applied: `docs/operations/b10-abrupt-contract.md: no issues found`, still `8 warnings`.
    - Expected after: no new warning. The 8 take one blanket line under docs/agents/pull-requests.md rule 8.
11. Full verification.
    - Command: `./scripts/verify-dotnet.sh`, with `dockerd` running. The PhantomSpike test needs Docker. Without Docker, `main` fails that one test with a `NullReferenceException` at `ExperimentDockerAuthentication.get_Endpoint()`.
    - Observed on `main` with Docker: exit 0, every stage `passed`.
    - Observed on the prototype with Docker: exit 0, every stage `passed` (LoadGen 1718 tests, Kafka, Scenarios 340, Enrichment 56, Architecture 17, Verifier); `Verification passed.`
    - Expected after: exit 0. Final-head CI (`dotnet-verification.yml`) remains the authority, including the published-apphost and abrupt-host checks that only CI runs.
12. Wiring.
    - Command: `grep -n` for the production completion members and the handler member in `src/Parchwell.LoadGen/Run/Abrupt/`.
    - Expected: the completion members are invoked only inside `RunSuccessorAsync`, and constructed and passed only in `CreateProducerAsync`; the handler member is used at the `SetStatisticsHandler` call. The receipt records the output. The member names are the implementer's.

## 11. Plan-check questions (factory.md "Item lifecycle" step 1)

- (a) Effort that scales with a number unknown at planning time: review findings, verification failures and documentation warnings.
  - Caps: 20 root causes each per pull request, with the exhaustion action `push -wip and hand off to the next session` (docs/operations/b10-abrupt-contract.md line 79). The cold-review budget falls under docs/agents/pull-requests.md rule 8.
  - The repetition gate is fixed at 50 runs, and the mutation list at 15. Neither scales.
  - If a repetition run or CI fails because of D6's races, that is outside this scope: stop and hand off to Hari.
- (b) Where the plan is stricter than its parent plans:
  - every changed stage runs against the real chain, with a real client where one runs offline (decision 6749565231);
  - the 50-run repetition gate and the deterministic delivery points P1 to P3 are added, because the prototype showed a free-running feed is flaky;
  - the 15-mutation table is added.
  No parent plan required these.
- (c) Tests that depend on code, data or behaviour neither on `main` nor in this pull request: T9 depends on the librdkafka bundled with the pinned Confluent.Kafka (on `main`) and on loopback TCP refusing port 1 in the test host. Behaviour that no test settles is listed as D1 to D7.

## 12. Sources

Files on `main` at `f5cc496fa3cf845e68e400b5b65e557f121dd928`:

- `B10AbruptProducer.cs`: lines 45, 100-106, 444-523, 559-580, 626-632.
- `B10AbruptHost.cs`: lines 50-51, 76-77, 93-106, 190, 218, 226-256, 259-281.
- `B10AbruptStatistics.cs`: lines 114-140, 164-200, 258-272.
- `B10AbruptDiagnostics.cs`: lines 8-20, 39, 50-58, 96-97.
- `KafkaStatisticsSession.cs`: lines 74-751 at the E2 citations, 93-141, 177-274, 547-570, 610-663.
- `KafkaStatisticsParser.cs`: lines 27-128.
- `KafkaStatisticsCompletion.cs`.
- `RunEvidenceSession.cs`: lines 203-208, 248-253, 319-362, 559-575.
- `RunExecutor.cs`: lines 360-438.
- `B10AbruptProducerTests.cs`: lines 691, 778, 814, 896, 1009, 1038-1078, 1236-1325, 1472-1500.
- `B10AbruptWireProducerTests.cs`: lines 351-391.
- `B10AbruptDiagnosticsTests.cs`: lines 70-77.
- `B10AbruptFailureSiteTests.cs`.
- `b10_abrupt.py`: lines 16-18, 383-399, 583-591, 600-645.
- `test_b10_abrupt.py`: lines 220-244.
- `check_b10_abrupt_producers.py`.
- docs/blueprint.md: line 708.
- docs/operations/b10-abrupt-contract.md: lines 51, 79, 160, 166, 188.

Other sources:

- Commit `54d9065`: the diff of `B10AbruptProducer.cs` and its test.
- The trial artifacts and hashes in section 6.
- The fresh reproduction check of Revision 1 at `d5b119a3`, relayed by the #347 control session on 2026-11-09. Its scratch files were not reachable from this session.
- librdkafka v2.16.0 statistics timestamps use `CLOCK_MONOTONIC` on Linux, as cited in `KafkaStatisticsServiceCollectionExtensions.cs` on `main`. Not re-read by this session.

## 13. Open questions for Hari

- Q1, the margin: answered by decision 6751199288. Both values stay.
- Q2. Accept deployment assumptions D1 to D7 by name, or name the test or trial evidence that should settle them.
- Q3. The D6 races, as rewritten in Revision 2. The first is unchanged on `main`; this pull request makes the second reachable on the successor path, as in `RunExecutor`. Open a separate issue, or accept them. Neither is fixed here; that is out of scope.
- Q4, the flagged codes: answered by decision 6751449502. All 32 codes are flagged, and the cap is 36 (E2, E4, E5).

No protected path is touched except docs/operations/b10-abrupt-contract.md line 188, which decision 6751449502 requires. No question is decided by this plan.

## 14. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" in any of these cases:

- it needs to change a contract in section 3, a requirement in section 4, the scope in section 2, or a value in 2.2;
- the source has a diagnostic code not in E2;
- a repetition, mutation or CI failure traces to either D6 race: the `B10AbruptStatistics` guard reason 3 collision, or a callback between `CloseCapture` and `SealFlags`, which this pull request makes reachable on the successor path.

Choices this plan leaves open are recorded in the receipt with their reasons: code text, member names, the shape of the production completion and handler members, and the test `TimeProvider` wrapper.

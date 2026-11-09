# Capture contention race fix contract-grade plan

Revision 2. Issue #404, pull request short name `capture contention race fix`. Implementing tool: Claude Code, under [standing decision 6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614) point 3, which replaces Codex for this pull request. Planned and reproduced against `main` at `f5cc496fa3cf845e68e400b5b65e557f121dd928`, the source of diagnostic trial 7. Implemented second, after the successor fix ([order decision 6752454732](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752454732)), on `main` after it merges or stacked on its unmerged head ([decision 6752554218](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752554218) point 2), and rechecked against it under the stop rule (section 15).

The B10 abrupt producer's statistics adapter records each native statistics report on the callback thread, but only if it wins a non-waiting try for its lock. When another thread holds that lock, for example the 10 ms post-drain poll, the report's record is skipped and the adapter's guard fails with guard reason 3. This pull request lets the callback thread record the report without that lock, so nothing is skipped and the order is kept. It changes one source file and adds one test class. It changes no protocol sequence, gate, policy value, timeout, interval, failure name, fact name, stage name or wire member, and it authorizes no trial.

Changes since Revision 1 (commit `c30bb3a624c55d942d6624921dbf942877199a0a`), answering the fresh reproduction check of Revision 1 relayed by the issue-347 control session:

- B1: T7 also accepts guard reason 7 with `duplicate_property` or `invalid_or_missing_field` in the completion diagnostics ([decision 6754092685](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6754092685) point 1); the masking cost is recorded; the duplicate-key deployment assumption is added (DA2); the stop-rule bullet on reason 7 follows.
- B2: acceptance command 7 no longer runs the architecture tests directly, which fails on `main` without the manifests that `scripts/verify-dotnet.sh` prepares; its result is pinned inside command 8.
- B3: [decision 6753263792](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6753263792) folded in: guard reason 3 retired (section 4), session-side contention accepted as DA1 (section 9), open questions None, the Q1 stop-rule bullet removed.
- B4: new regression test T8 for R3's empty-queue case; new mutation M10; class totals 9.
- B5: required kill sets are the deterministic tests only; T7 kills are listed as possible, not required; the check's X6 and X7 are recorded as equivalent mutants.
- m1: section 13 cites PR3a Revision 8 at `d293b4b6` (approved), successor fix Revision 4 and the capture-loss race fix Revision 2.
- m2: T7's guard failure may come from the scanner or from the final guard check, which runs after `CompleteAsync`.
- m3: N8 states that R7 has no killing mutation.

## 1. Goal and authority

Goal: the minimum B10 load-generator change that removes the guard failure caused by `B10AbruptStatistics.CaptureNative` losing the adapter lock, without dropping or reordering a native statistics report the evidence depends on. Every changed stage is tested with the real `B10AbruptStatistics`, real threads and, in one test, a real `KafkaStatisticsSession` fed by a real librdkafka client.

Authority, in creation order (docs/agents/factory.md "Item lifecycle" step 3):

- Issue #404 body, and the decisions listed in `items/404/state.md` "Decisions in force" up to the [burst test-gap approval 6746177383](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6746177383). None of them names this race.
- [Decision 6749565231](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749565231): plans for this work carry a never-ran inventory, "every stage ... that has not yet executed live, and for each stage the test that runs it for real (a real statistics session, not a fake)". "A further trial needs Hari's new explicit approval and his gate approval."
- [Decision 6752059755](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752059755): the capture-loss race between `CloseCapture` and the completion seal is fixed in its own pull request, in `src/Parchwell.Kafka`. It states that this race (`CaptureNative` against the 10 ms poll, guard reason 3) "is not decided by this comment".
- [Decision 6752078378](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752078378), option 2: "Fix this race too, in its own small pull request, separate from the successor fix and from the capture-loss race fix of decision 6752059755. It gets its own contract-grade plan, fresh reproduction check, plan check and Hari's approval, and must merge before any further B10 trial. No protocol sequence, gate, policy value or failure name changes unless the plan asks Hari." "Their order is fixed in their plans and Hari's approvals." "No trial, dispatch, results or expected-outcome change is authorized by this decision."
- [Standing decision 6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614), for the four pull requests before the next B10 trial, this one included: rule 1 lets the control session decide open questions that choose only between test techniques, fixtures or harness details, log wording or fields, file placement within allowed scope, or certain deployment assumptions; protocol, gate, policy value, timeout, failure name, verdict, design text, safety, protected-path and scope questions wait for Hari. Point 2 is standing plan approval. Point 3 names Claude Code as the implementing tool. Point 4 lets the control session merge a pull request of the four that touches no protected path, under AGENTS.md GOVERNANCE "Merge rule" in full.
- [Order decision 6752454732](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752454732): "1. Successor fix. 2. Capture contention race fix (shares B10 load-generator files with the successor fix). 3. PR3a evidence completeness". "each later one is implemented on `main` after its predecessors have merged, with its plan rechecked against them under its stop rule".
- [Decision 6752554218](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752554218): point 1, a stalled Codex review is re-requested once after 15 minutes; point 2, "The next pull request in the order may be implemented on a branch based on its predecessor's unmerged head", "not merged before its predecessor", then rebased onto `main`, rechecked under its stop rule and reviewed again on the new head.
- [Decision 6753263792](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6753263792), under standing rule 1; Hari has not seen it. Point 1: "Retire guard reason 3 ... the value is not reused for another condition." Point 2: the session-side lock contention in `KafkaStatisticsSession.Capture` is accepted as a deployment assumption for the next trial; whether to fix it is left for Hari.
- [Decision 6754092685](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6754092685), under standing rule 1; Hari has not seen it. Point 1: T7 also accepts guard reason 7 when the completion diagnostics contain `duplicate_property` or `invalid_or_missing_field`, the second labelled "cause not identified"; "Any guard reason 3 still fails it"; the plan records that these outcomes can mask a concurrency-only regression in those runs. Point 2: "live librdkafka statistics in the next B10 trial contain no duplicate keys" is accepted as a deployment assumption. It also corrects decision 6753263792 point 2: a trial hit by session-side contention is named in the log by guard reason 6, but `capture_contention` may not appear there.

Decisions 6749995101, 6750021224, 6750864341, 6750879393, 6750891952, 6750940329, 6751199288, 6751449502, 6751703425 and 6753364128, and approval 6753938298 (PR3a Revision 8), govern the verifier split, PR3a or the successor fix. None of them changes `B10AbruptStatistics.cs`.

Catch-up for this revision: the state file covers comment 6746177383. Comments after it, all read in full: 6747016131 (completion report for the burst test-gap follow-up, not a decision) and the fifteen decisions 6749565231, 6749995101, 6750021224, 6750864341, 6750879393, 6750891952, 6750940329, 6751199288, 6751449502, 6751703425, 6752059755, 6752078378, 6752430614, 6752454732, 6752554218, 6753263792, 6753364128 and 6754092685; approval 6753938298; and, by heading only, the PR3a plan comment 6753635457 and its plan check 6753915132. Snapshot for Revision 2: 160 comments, none edited or deleted since the earlier reads; issue `updated_at` 2026-11-09T23:17:54Z. Approval 6708991894 was also read in full, for the merge statement in 3.1.

Design text this plan depends on and does not change:

- docs/operations/b10-abrupt-contract.md line 74: the "Guards" policy row, "Block arm/seal; no relaxed guard after trial".
- The same file, line 130: failure name `b10_abrupt_guard_failed`, "A guard of the policy table fails".
- The same file, line 106: guard and capture gates are policy; after a failed trial only implementation bugs and mechanical ceilings change, each by Hari's recorded decision. Decision 6752078378 is that decision for this race.

Numeric guard reasons are not in any document. A repository search on `main` for `guard_reason`, `GuardReason` and `reason 3` in `*.md` finds only the successor fix plan. They appear only in `B10AbruptStatistics.cs` and as the value of the `guard_reason` fact in failure lines.

## 2. The race, with evidence

All line numbers are on `main` at `f5cc496f`.

- `B10AbruptHost.cs` line 224 activates native capture tracking; line 232 sets the statistics handler to `observations.CaptureNative(raw); statistics.Capture(raw);`, in that order, on the thread that runs the callback.
- `CaptureNative` (`B10AbruptStatistics.cs` lines 114-140) validates the raw report (guard reasons 1 and 2), hashes it, then calls `Monitor.TryEnter(gate)` (line 129). If that fails, it calls `Guard(3)` and returns (lines 129-133) without recording the pending entry. Otherwise, under the lock, it records `(length, sha256)` in `pendingNative` or fails with reason 4 when tracking is off or 2048 entries are pending (line 136).
- `Observe` (lines 164-232) runs on the statistics session's worker, under `lock (gate)`. With tracking on, it dequeues the oldest pending entry and fails with reason 6 when none exists or it does not match (lines 177-182).
- Other members take the same lock: `AssertAvailableGuards`, `ActivateNativeCaptureTracking`, `MarkConstructed`, `ReportCount`, `SelectPreAcceptance`, `MarkDrainBoundary`, `SelectPostDrain`, `PostDrainFacts` and `CapturedBytes`.
- `WaitForPostDrainAsync` (`B10AbruptProducer.cs` lines 559-580) calls `SelectPostDrain` every 10 ms (line 570) until a qualifying report arrives. It runs in the timeout arm (line 238), the control arm (line 279) and the successor (line 483). `SelectPostDrain` throws `b10_abrupt_guard_failed` with facts `guard_failed`, `guard_reason`, `expected_brokers_observed` and `report_count` once the guard has failed (lines 262-265).
- `Guard` keeps the first reason only (`Interlocked.CompareExchange`, line 149). After a reason-3 failure, the next `Observe` also finds no pending entry (reason 6), but reason 3 is the one reported.

So any member holding `gate` when a native report arrives fails the guard. The 10 ms poll is the most frequent holder during a post-drain wait, but it is not the only one.

Reproduced on `main`, in this planning session's scratch workspace, with real components (section 7):

- Deterministic: with `gate` held by another thread, `CaptureNative` returns at once and the guard fails with reason 3 (T1 to T4).
- Free-running: a real librdkafka client feeding a real `KafkaStatisticsSession` and the real adapter, with a second thread scanning `SelectPostDrain` continuously, failed with `guard_reason=3` in 20 of 20 runs (T7).

Not observed live: the seven diagnostic trials (section 6) contain no `guard_reason` value in any log, checked by script. Its live probability is not measured.

## 3. Scope

### 3.1 Files allowed to change

1. `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptStatistics.cs`: the native capture path (`CaptureNative`) and the pending-report match inside `Observe`, with whatever private state they need. On `main` these are at lines 26-27, 114-140 and 177-182; the line numbers locate the code and pin nothing. No other behaviour of the file changes.
2. New file `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptStatisticsContentionTests.cs`: T1 to T8.
3. `items/404/state.md` (docs/agents/factory.md "Item state file") and the new receipt `items/404/contention-race-fix-receipt.md`.

No protected path is in this list (`.github/CODEOWNERS`: `/src/`, `/tests/Parchwell.LoadGen.Tests/` and `/items/` are not listed; `/*.md` covers root files only). [Approval 6708991894](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6708991894) said "Hari merges every pull request here"; for this pull request the later standing decision 6752430614 point 4 lets the control session merge it under AGENTS.md GOVERNANCE "Merge rule" in full, after the successor fix has merged (decision 6752554218 point 2).

### 3.2 Paths and values that must not change

Paths: everything not in 3.1, in particular:

- `src/Parchwell.Kafka/**`, including `KafkaStatisticsSession.Capture` and its own `Monitor.TryEnter` at line 106 (section 9, R-OUT);
- every other member of `B10AbruptStatistics.cs`, including `SelectPostDrain`, `SelectPreAcceptance`, `PostDrainFacts`, `SealCohort` and `CapturedBytes`;
- `B10AbruptProducer.cs`, `B10AbruptHost.cs` (including the handler at line 232 and its call order), `B10AbruptDiagnostics.cs`, and every other file under `src/`;
- every existing test file, including the existing native-capture tests in `B10AbruptProducerTests.cs` (methods at lines 588, 609 and 623);
- `docs/**`, `infra/**`, `charts/**`, `scenarios/**`, `results/**`, `.github/**`, any `expected.yaml`, `AGENTS.md`, `CLAUDE.md`, any `*.csproj`, `Directory.*.props` and `*.sln*`.

No new project, assembly reference or NuGet package.

Values that keep their `main` values:

| Value | On `main` | Source |
|---|---|---|
| Post-drain poll interval | 10 ms | `B10AbruptProducer.cs` line 570 |
| Pending native entry bound | 2048 | `B10AbruptStatistics.cs` line 136 |
| Raw report limit | 1048576 UTF-16 characters and 1048576 UTF-8 bytes | lines 117 and 123 |
| Guard reasons 1, 2, 4 to 15 | Same conditions | lines 47 to 228 |
| `statistics.interval.ms` | 15000 | docs/blueprint.md line 708 |

## 4. External contracts

This pull request changes no external contract. Pinned as unchanged:

- Failure name `b10_abrupt_guard_failed`, at the same three throw sites (`B10AbruptStatistics.cs` lines 99, 239, 263 on `main`).
- Fact names and order at those sites: `guard_failed`, `guard_reason`, `expected_brokers_observed`, `report_count`.
- The meaning of `guard_reason` values 1, 2 and 4 to 15.
- `statistics.jsonl` (`CapturedBytes`) members and report sequence numbering.
- Stage names, wire members, publication objects and the phase script.

One observable change: after this pull request no code path sets `guard_reason` 3. The value is retired and not reused for another condition (decision 6753263792 point 1).

## 5. Requirements

Each requirement is observable behaviour of the real `B10AbruptStatistics` that `B10AbruptHost.CreateProducerAsync` builds for every abrupt producer: tracking activated (`ActivateNativeCaptureTracking`), attached to the real `RunEvidenceSinkRouter`, fed by the production statistics handler, and read by the producer through `SelectPostDrain`, `SelectPreAcceptance`, `PostDrainFacts`, `ReportCount` and `CapturedBytes`. The guard is observed where the producer observes it: a `b10_abrupt_guard_failed` exception from those members or from `AssertAvailableGuards`, with its `guard_reason` fact.

"Pending report" means a raw report `CaptureNative` has accepted and `Observe` has not yet matched. "Adapter lock" means the lock that the adapter's reading members, such as `SelectPostDrain`, hold while they run. How it is implemented, named or structured is the implementer's choice.

- R1. WHEN `CaptureNative` receives a raw report that passes the reason 1 and 2 checks, tracking is active, and fewer than 2048 reports are pending, THEN the report becomes pending and the guard does not fail, whether or not another thread holds the adapter lock.
- R2. WHEN another thread holds the adapter lock, THEN `CaptureNative` returns before that lock is released.
- R3. WHEN `Observe` runs with tracking active, THEN it matches the report against the oldest pending report, in the order `CaptureNative` accepted them, and that report is no longer pending. No pending report, or a length or SHA-256 mismatch, fails the guard with reason 6 (unchanged).
- R4. WHEN 2048 reports are pending, THEN a further `CaptureNative` fails the guard with reason 4 and that report does not become pending (unchanged). WHEN exactly 2048 reports are pending and no further capture happens, THEN the guard has not failed. A matched report no longer counts toward the 2048.
- R5. WHEN tracking is not active, THEN `CaptureNative` fails the guard with reason 4 (unchanged).
- R6. Reasons 1 and 2 are checked first, as on `main`; a report failing them does not become pending.
- R7. WHEN `CaptureNative` runs on more than one thread at once, THEN each passing call makes exactly one report pending, and the bound in R4 holds.
- R8. WHEN any of R1 to R7 applies, THEN the guard never fails with reason 3 (decision 6753263792 point 1).

## 6. Never-ran inventory

Evidence is the trial logs, not the code. The seven B10 abrupt diagnostic runs are those listed in `items/404/plan-successor-fix.md` section 6 at `acadeaa` (unchanged since `c12d4ff`). Each artifact was downloaded with `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/<id>/zip` into a new empty scratch directory and treated as untrusted data. All seven zip SHA-256 values matched that table, including trial 7 (run 42679791413, artifact 13704155185, `bee17231882ffeb7ddc3134ba10a00ec4590f20e96dad409c050decf1f22cdcf`).

A script scanned every file of every artifact for stage lines and for the `guard_reason` fact. Results:

- Runs 42460966764, 42475024856, 42512105140 and 42536075365: no stage line from the producer at all.
- Run 42608381540 (`4163cd1a`), child log `b10-42608381540-1-c1-r1-loadgen-zdc8s.containers.loadgen.log`, SHA-256 `9f711d428e58a350cad78dc5c2fc2741d50f61b7f90c33f879b8e86abc10b00e`: line 72 `post_drain_report` (arm 0, `elapsed_milliseconds` 3699, `report_count` 3), line 76 `probe_report_selected` (arm 1).
- Run 42670600125 (`fa73aa08`): `producer_constructed` only. Its `b10_abrupt_guard_failed` line (line 70) comes from `CheckGuards` with `fault_reason` 3, a producer fault for a delivery error (set at `B10AbruptProducer.cs` lines 171 and 400 on `main`), not the adapter's `guard_reason` 3.
- Run 42679791413 (trial 7), child log `b10-42679791413-1-c1-r1-loadgen-h9svx.containers.loadgen.log`, SHA-256 `19e738b1d83de514681a62dca4467ad8b5d8ce328cc42e052d4ee3561a5a122f`: line 72 `post_drain_report` (arm 0, 3771 ms, 3 reports), line 76 `probe_report_selected` (arm 1), line 99 `post_drain_report` (arm 1, 13012 ms, 12 reports), line 103 `probe_report_selected` (arm 2). Successor log (SHA-256 `fce88054fccf70591da6f334b7299e446a7ec66cd7b6814fec8740cd17e7209f`) line 54: `post_drain_report` (arm 2, 10896 ms, 7 reports).
- No file in any artifact contains a `guard_reason` value.

Inference from those lines: each `post_drain_report` is marked only after `SelectPostDrain` returned a report without throwing (`B10AbruptProducer.cs` lines 567-579), and `SelectPostDrain` throws once the guard has failed (lines 262-265 of `B10AbruptStatistics.cs`). So up to each of those points no capture had lost the lock, and every report observed so far had matched its pending entry. That is live evidence for the uncontended path only.

| Stage on the changed path | Ran live? Evidence | Test in this plan that runs it for real |
|---|---|---|
| N1. Native callback to `CaptureNative`, uncontended, record entry | Yes: runs 42608381540 and 42679791413, the four `post_drain_report` lines above (inference) | T7 (real librdkafka client, real session, real adapter), T6 |
| N2. `CaptureNative` while another thread holds the lock (changed) | Never: no `guard_reason` in any log; on `main` this branch fails the guard | T1, T2, T3, T4 (lock held by a real thread); T7 (real scanning thread, real native callbacks) |
| N3. `Observe` matching and removing the oldest entry (changed: also releases one bound slot) | Matching ran live (N1). The slot release never ran: it is new. The empty-queue failure never ran | T2, T3, T6, T7, T8 |
| N4. Order kept across contention | Never | T2 (in order), T3 (out of order fails, reason 6), T7 |
| N5. Bound reached (reason 4) | Never: no `guard_reason` in any log | T4 (2048 passes, 2049 fails) |
| N6. Tracking not active (reason 4) | Never: production always activates (`B10AbruptHost.cs` line 224) | T5 |
| N7. `SelectPostDrain` and `PostDrainFacts` scanning while reports arrive | Yes: the post-drain waits above (inference), unchanged code | T7 |
| N8. Concurrent `CaptureNative` on two callback threads (R7) | Unknown: no log records the callback thread | No killing test. R7 has no killing mutation here: M11 (unsynchronized queue) and M12 (non-atomic count) each survived the class (7.3); the reproduction check reports its X4 surviving and its X9 (unsynchronized queue) caught in 1 of 10 runs. The cold review checks R7. Inference: two statistics callbacks overlap only if one takes longer than the 15000 ms statistics interval |

Size of live reports: the changed statements read only the length and hash, which are computed before them exactly as on `main` (lines 117-128). Report size and content do not reach the changed code. No deployment assumption is needed for the changed path (section 9).

## 7. Test map

### 7.1 Common inputs

All tests are in a new class `Parchwell.LoadGen.Tests.Abrupt.B10AbruptStatisticsContentionTests`. Names follow docs/engineering/dotnet.md "Testing" (`Given_<state>_When_<action>_Then_<outcome>`), with `#pragma warning disable CA1707` as in the other Abrupt test files. Test names, inputs and literal expected values below are pinned; helper shapes are not.

- Identity: `new KafkaStatisticsIdentity("B10", "run-1", KafkaClientRole.LoadGenProducer, "client")`.
- Tracked adapter: a real `B10AbruptStatistics(identity, ["broker"], {}, {})` after `ActivateNativeCaptureTracking()` and `MarkConstructed(DateTimeOffset.MinValue)`, as `CreateProducerAsync` prepares it (`B10AbruptHost.cs` lines 223-224 on `main`).
- Report `R(ts)`: one producer report from client `client`, native name `native`, one broker `broker` with `source` `learned` and all error counters 0, with `ts` and `age` both equal to `ts`. Its raw JSON and typed snapshot carry the same values, as `B10AbruptProducerTests.StatisticsReport` builds them. Distinct `ts` values give distinct raw JSON and strictly rising native timestamps.
- Lock held: a second real thread holds the adapter lock until the test releases it. Permitted example: the prototype read the lock object by reflection and held it with `Monitor.Enter`, as the existing test at `B10AbruptProducerTests.cs` line 438 reads a private field. Any technique is allowed if T1 to T4 still fail on the base `CaptureNative` (acceptance command 5).
- Capture thread: a third real thread calls `CaptureNative` for each raw report, in order. "Returned while held" means that thread finished within 5 s while the lock was still held.
- Guard reason: the `guard_reason` fact attached to the `b10_abrupt_guard_failed` exception, read through `B10AbruptDiagnostics` as the failure report reads it.

### 7.2 Tests

| Test | Covers | Input | Literal expected values | On `main` |
|---|---|---|---|---|
| T1 `Given_GateHeldByAnotherThread_When_NativeReportCaptured_Then_CaptureReturnsAndReportStillMatches` | R1, R2, N2 | Tracked adapter. Lock held. Capture `R(1000)`. Release. `Observe(R(1000))` | Returned while held: true. `AssertAvailableGuards()` does not throw. `SelectPreAcceptance(DateTimeOffset.MaxValue)` returns sequence 1 | Fails: guard reason 3 |
| T2 `Given_ReportsCapturedWhileGateHeld_When_ObservedInCaptureOrder_Then_AllMatchInOrder` | R1, R3, N4 | Lock held. Capture `R(1000)`, `R(2000)`, `R(3000)`. Release. Observe the same three in order | Returned while held: true. No guard failure. `SelectPreAcceptance(MaxValue)` returns sequence 3. `CapturedBytes()` has 3 lines with `report_sequence` 1, 2, 3 | Fails: guard reason 3 |
| T3 `Given_ReportsCapturedWhileGateHeld_When_ObservedOutOfCaptureOrder_Then_GuardReasonSix` | R3, N4 | Lock held. Capture `R(1000)`, `R(2000)`. Release. `Observe(R(2000))` only | `AssertAvailableGuards()` throws `InvalidDataException` `b10_abrupt_guard_failed`, guard reason 6 | Fails: reason 3, not 6 |
| T4 `Given_PendingCapturesWhileGateHeld_When_BoundReached_Then_OnlyAboveBoundFails`, cases (2048, none) and (2049, 4) | R4, N5 | Lock held. Capture `R(1000)` 2048 or 2049 times. Release. No `Observe` | Returned while held: true. 2048: `AssertAvailableGuards()` does not throw. 2049: throws `b10_abrupt_guard_failed`, guard reason 4 | Both cases fail: reason 3 |
| T5 `Given_TrackingNotActivated_When_NativeReportCaptured_Then_GuardReasonFour` | R5, N6 | Adapter without `ActivateNativeCaptureTracking`. Capture `R(1000)` | `AssertAvailableGuards()` throws, guard reason 4 | Passes (regression guard) |
| T6 `Given_MoreReportsThanBoundCapturedAndObservedInTurn_When_Checked_Then_NoGuardFailure` | R3, R4 release, N3 | Tracked adapter. For i = 1 to 2049: capture `R(i*1000)`, then `Observe(R(i*1000))` | `AssertAvailableGuards()` does not throw. `ReportCount` is 2049 | Passes (regression guard for the release) |
| T7 `Given_RealNativeReportsAndContinuousPostDrainScan_When_ReportsArrive_Then_NoGuardFailureAndEveryReportMatched` | R1, R2, R3, R8, N1, N2, N3, N7 | The production chain of `CreateProducerAsync`, built from real objects: tracked adapter with expected broker `127.0.0.1:1/bootstrap`; a real `RunEvidenceSinkRouter` with the adapter attached; a real `KafkaStatisticsSession` from `new KafkaStatisticsBridgeFactory(options, TimeProvider.System, router, new KafkaStatisticsMetrics(), nativeClockCompatible: true)` with `MaxSnapshotCharacters` 1048576, `QueueCapacity` 2048, `MaxBrokerSeries` 64, `CompletionTimeout` 1 s; and a real `ProducerBuilder<byte[], byte[]>` with `bootstrap.servers` `127.0.0.1:1` (nothing listening), `client.id` `client`, `statistics.interval.ms` 20 (test only), no-op log and error handlers, and as statistics handler the production handler of the successor fix's R10: the handler the host installs in the native client it builds, which that plan's T13 observes and its T1 to T9 use. `MarkDrainBoundary(DateTimeOffset.MaxValue, now, frequency)`. A scanning thread loops with no delay: `SelectPostDrain(DateTimeOffset.MaxValue)` (asserted null), then `PostDrainFacts`. Wait until `ReportCount` is at least 50 or the scanner fails, at most 30 s. Dispose the producer, stop the scanner, then `await session.CompleteAsync()` (capture-only overload), which ends at its 1 s timeout and returns the session diagnostics. Only then run the final guard check, `AssertAvailableGuards()`. "The guard failure" is the scanner's `b10_abrupt_guard_failed` if it threw one, otherwise the final check's | Scan count above 0. Then exactly one of these outcomes passes. Normal: no guard failure, and `ReportCount` at least 50. Outside A (DA1): guard reason 6, and the completion diagnostics contain `capture_contention`. Outside B (DA2): guard reason 7, and the completion diagnostics contain `duplicate_property`. Outside C: guard reason 7, and the completion diagnostics contain `invalid_or_missing_field`; cause not identified. Any other outcome fails, in particular any guard reason 3 | Fails 20 of 20 runs: guard reason 3 (in each of two 20-run sets) |
| T8 `Given_TrackedAdapterWithNoPendingReport_When_Observed_Then_GuardReasonSix` | R3 (no pending report), N3 | Tracked adapter. No `CaptureNative`. `Observe(R(1000))` | `AssertAvailableGuards()` throws `InvalidDataException` `b10_abrupt_guard_failed`, guard reason 6 | Passes (regression guard) |

The outside outcomes come from causes outside this pull request (section 9). A: the session's own capture contention drops a report the adapter already made pending, which fails the adapter guard with reason 6. B: librdkafka emitted the bootstrap broker key twice in one report (the reproduction check captured one with `connects` 0 and 1), the session parser rejects it with `duplicate_property`, and the adapter fails with reason 7 (`B10AbruptStatistics.cs` lines 185-190). C: the check saw one reason-7 run with `invalid_or_missing_field`, cause not identified. T7 still fails on any guard reason 3, on reason 6 or 7 without its named diagnostic, and on any other reason.

Cost of the outside outcomes (decision 6754092685 point 1): in a run that ends in an outside outcome, T7 cannot see a concurrency-only regression of this fix, because the run stops at the outside failure. T7's mutation kills are therefore probabilistic. The required kills are the deterministic tests T1 to T6 and T8 (7.3).

The prototype ran on `f5cc496`, which has no R10 member. There T7 used the two calls of `B10AbruptHost.cs` line 232, in the same order. With the successor fix as base, T7 uses the R10 member instead (stop rule, section 15).

Observed in this planning session on 2026-11-09 (scratch only, never pushed):

- On `main` plus the 9-case class: `Total: 9, Errors: 0, Failed: 6`; T5, T6 and T8 passed. T7 alone, 20 runs: every run failed with `guard_reason=3`. (Revision 1's 8-case class: 3 class runs, each 6 failed; T7 alone 20 of 20 failed with reason 3, the first after 2 reports.)
- On the prototype fix with Revision 1's T7 (no outside outcomes), in order:
  - Class repeated 50 times: 49 passed, 1 failed; the failing test and its guard reason were not recorded.
  - Class repeated 100 times: 1 failure, T7 with guard reason 7, diagnostics not recorded.
  - T7 alone, 300 runs, with a scratch spy on report diagnostics: 10 failures, all guard reason 6.
  - T7 alone, 200 runs, the spy also reading the session's diagnostic set: 9 failures, all guard reason 6 with `capture_contention`.
  - Class repeated 150 times with that spy: 3 failures, all guard reason 6 with `capture_contention`.
- The reproduction check, on its own prototype: guard reason 7 in about 4 of 683 runs (its estimate), with `duplicate_property` from a duplicated bootstrap broker key, and one more reason-7 run with `invalid_or_missing_field`.
- On the prototype fix with T7 as pinned above:
  - T7 alone, 220 runs, with a scratch-only line logging each outcome: 220 passed; 205 normal; 15 outside A, each with completion diagnostics `capture_contention,capture_loss,completion_timeout`; no reason 7. Every normal run had completion diagnostics `completion_timeout` only.
  - Class repeated 50 times: `50 Total: 9, Errors: 0, Failed: 0`.
  - So 15 of those 220 runs could not have shown a concurrency-only regression (estimate of the masking rate from this one sample, 2026-11-09).

### 7.3 Mutations

Each mutation is a scratch edit of the prototype fix, never committed. Observed on the prototype with the 9-case class as pinned, one class run per mutation: each built with 0 errors and production was restored byte-identical afterwards. "Required" lists the deterministic tests the implementer's run must show failing. "May also kill" lists T7, whose kills depend on timing and on the outside outcomes; it is not required.

| Id | Requirement | Mutation | Required kills | May also kill (observed here) |
|---|---|---|---|---|
| M1 | R1, R2, R8 | `main` shape: `Monitor.TryEnter` on the adapter lock, `Guard(3)` on failure | T1, T2, T3, T4 (both cases) | T7 (killed) |
| M2 | R2 | Blocking lock around the record | T1, T2, T3, T4 (both cases) | T7 (not killed) |
| M3 | R1 | Contended capture dropped silently (lock try fails, no guard) | T1, T2, T4 (2049) | T7 (killed) |
| M4 | R3 | Pending reports matched last in, first out | T2, T3 | T7 (killed here; the check saw it survive T7 in 1 suite run and 3 of 15 repeats) |
| M5 | R4 | Bound removed | T4 (2049) | none |
| M6 | R4 | Bound off by one (2047) | T4 (2048) | none |
| M7 | R5 | Tracking check removed | T5 | none |
| M8 | R4 release | Matched report not released from the bound | T6 | none |
| M9 | R4 | Bound guard reason 4 changed to 3 | T4 (2049) | none |
| M10 | R3 | No guard failure when no report is pending | T8 | none |
| M11 | R7 | Pending queue not thread-safe | none (survived) | none |
| M12 | R7 | Bound count not atomic | none (survived) | none |

Observed failing sets: M1 T1, T2, T3, T4 both, T7; M2 T1, T2, T3, T4 both; M3 T1, T2, T4 (2049), T7; M4 T2, T3, T7; M5 T4 (2049); M6 T4 (2048); M7 T5; M8 T6; M9 T4 (2049); M10 T8; M11 and M12 none, 9 of 9 passed.

The reproduction check's X6 and X7, as relayed by the control session, change behaviour only after the guard has already failed. The guard keeps its first failure (I3), so they are equivalent mutants and need no kill. This session did not run them.

R6 is unchanged code; the existing `Given_NativeOversizeOrQueueOverflow_When_Selecting_Then_NoLaterReportRepairsCapture` (`B10AbruptProducerTests.cs` lines 606-620) covers reason 1 and must keep passing. R7 has no killing mutation (N8); the cold review checks it.

## 8. Invariants and safety

- I1. The production statistics handler (`B10AbruptHost.cs` line 232 on `main`; the successor fix's R10 member on the base) calls `CaptureNative`, then `KafkaStatisticsSession.Capture`, on the same thread. This pull request does not change it. A report is therefore pending before the session can deliver it to `Observe`.
- I2. Pending reports are matched first in, first out, and each is matched at most once, by `Observe`.
- I3. The guard keeps its first reason (`Interlocked.CompareExchange`), unchanged.
- I4. AGENTS.md hard safety rules hold: no Azure credential, no workflow dispatch, no `.github/` change, no trial. Verdict integrity holds: nothing under `results/`, no expected outcome.
- I5. No number in section 2 or 6 enters code or docs; they stay in this plan and the receipt.

## 9. Deployment assumptions

The changed path needs none: every changed stage runs offline with real threads and, in T7, a real librdkafka client and a real session (section 6). Two assumptions outside the changed path are accepted for the next trial by the control session under standing rule 1. Hari has not seen either acceptance.

- DA1 (R-OUT). The session-side lock contention in `KafkaStatisticsSession.Capture` does not occur in the next trial. Accepted by [decision 6753263792](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6753263792) point 2, with the log correction of [decision 6754092685](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6754092685). Mechanism: `Capture` tries the session lock without waiting (`KafkaStatisticsSession.cs` lines 106-111 on `main`). If it loses, the session drops the report and records `capture_contention`, while `CaptureNative` has already made it pending, so the next `Observe` fails the adapter guard with reason 6. If it happens live, the failure line names guard reason 6; `capture_contention` may not appear in it. Offline observation: T7 outcome A, 15 of 220 runs here. It is in `src/Parchwell.Kafka`, which this pull request must not change. Whether to fix it is left for Hari.
- DA2. Live librdkafka statistics in the next trial contain no duplicate keys. Accepted by decision 6754092685 point 2. If they do, the session attaches `duplicate_property` and the adapter guard fails with reason 7. None of the seven diagnostic trials shows any guard reason (section 6). Offline observation: T7 outcome B, seen by the reproduction check with an unreachable bootstrap address, not seen in this session's 220 runs. Whether the parser or the adapter should tolerate duplicate broker keys is left for Hari.

## 10. Acceptance commands

Run from the repository root. "Observed" is what this planning session saw on 2026-11-09, in one of two places:
- `main`: a worktree of `f5cc496`, with the new test class added where stated;
- the prototype: a worktree of `f5cc496` plus a prototype of R1 to R8 in `B10AbruptStatistics.cs` and the 9-case test class. It was never pushed.
The implementer records its own results in the receipt. "Base" means the commit the pull request branches from; if another issue-404 pull request merges first, the base totals change and the expected values are "base plus 9".

1. Restore.
   - Command: `dotnet restore tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --locked-mode`
   - Observed: exit 0.
   - Expected after: exit 0.
2. Build.
   - Command: `dotnet build tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj -c Release --no-restore`
   - Observed on `main` and the prototype: `0 Warning(s)`, `0 Error(s)`.
   - Expected after: the same.
3. Burst suite.
   - Command: `dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj -c Release --no-build -- --filter-class "*B10Abrupt*"`
   - Observed on `main`: total 457, succeeded 456, skipped 1.
   - Observed on the prototype: total 466, succeeded 465, skipped 1, failed 0 (Revision 1's 8 cases: 465, 464, 1).
   - Expected after: total base plus 9, failed 0, skipped as base (1 on `f5cc496`, the published-apphost test reserved to CI).
4. Repetition gate, 50 runs of the new class. The count of 50 is a plan choice, not a measurement.
   - Command: `for i in $(seq 1 50); do dotnet tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptStatisticsContentionTests 2>&1 | grep -o 'Total: [0-9]*, Errors: [0-9]*, Failed: [0-9]*'; done | sort | uniq -c`
   - Observed on the prototype, T7 as pinned: `50 Total: 9, Errors: 0, Failed: 0`.
   - Expected after: `50 Total: 9, Errors: 0, Failed: 0`.
5. Reproduction on the base. Check out the base's `B10AbruptStatistics.cs` in a scratch worktree with the new test class, build, and run the class once.
   - Observed on `main`: `Total: 9, Errors: 0, Failed: 6`; failing T1, T2, T3, T4 (both cases), T7; T7's message contains `guard_reason=3`; T5, T6 and T8 pass.
   - Expected: the same six fail on the base. The receipt records it.
6. Mutations M1 to M12 (section 7.3).
   - Observed on the prototype: each built with 0 errors; the failing sets are those listed in 7.3.
   - Expected after: M1 to M10 each fail at least their required tests; M11 and M12 are recorded, not required; production restored byte-identical.
7. Architecture test, run inside command 8 only.
   - A direct `dotnet test` of `tests/Parchwell.ArchitectureTests` fails on `main` with `Missing evaluated project manifests. Run ./scripts/verify-dotnet.sh.`, because that script sets `PARCHWELL_PROJECT_MANIFEST_DIRECTORY` (line 23). Observed here on `main`.
   - Observed on the prototype inside command 8: `Parchwell.ArchitectureTests: 17 passed; checked report.`
   - Expected after: command 8 prints `Parchwell.ArchitectureTests: 17 passed` (base count; 17 on `f5cc496`).
8. Full verification.
   - Command: `./scripts/verify-dotnet.sh`, with `dockerd` running.
   - Observed on the prototype (with Revision 1's first T7 design, 8 cases): first attempt exit 2, stopped at `Parchwell.Kafka.Tests` because Docker Hub answered `429 Too Many Requests` when the PhantomSpike test pulled `confluentinc/cp-kafka`; a retry exited 0, `Verification passed.` (Architecture 17, Enrichment 56, Kafka 486, LoadGen 1685, Scenarios 340, Verifier 7389). Separately, one direct `dotnet test` of the whole LoadGen project, run before that retry, failed two Docker-backed integration tests (`BlobPublicationIntegrationTests`, `ProbeV2ComponentTests`), whose 29 cases then passed when rerun alone; both are outside the changed path.
   - Expected after: exit 0, with `Parchwell.ArchitectureTests: 17 passed` (command 7). Final-head CI (`dotnet-verification.yml`) remains the authority.
9. Wiring and guard-reason check.
   - Command: `grep -n "CaptureNative(" src/Parchwell.LoadGen/Run/Abrupt/*.cs; grep -n "Guard(3)" src/Parchwell.LoadGen/Run/Abrupt/B10AbruptStatistics.cs; grep -n "TryEnter" src/Parchwell.LoadGen/Run/Abrupt/B10AbruptStatistics.cs`
   - Observed on `main`: `B10AbruptHost.cs:232` (handler, `observations.CaptureNative(raw); statistics.Capture(raw);`) and `B10AbruptStatistics.cs:114` (definition); `Guard(3)` at line 131; `TryEnter` at line 129.
   - Observed on the prototype: the same two `CaptureNative(` lines; no `Guard(3)`; no `TryEnter`.
   - Expected after: no `Guard(3)` and no `TryEnter` in `B10AbruptStatistics.cs`. `CaptureNative(` appears at its definition and, on the successor-fix base, only in the production statistics handler of that plan's R10, ahead of the session `Capture(` call. The receipt records the output.
10. Documentation check.
    - Command: `npm run check:docs`
    - Observed on this plan branch with this file staged: `items/404/plan-capture-contention-race-fix.md: no issues found`; 10 warnings in total, all in other files (8 in `docs/`, 2 in `items/404/plan-pr3a-evidence-completeness.md` at that time).
    - Expected after: no warning in `items/404/state.md` or the new receipt.

## 11. Plan-check questions (factory.md "Item lifecycle" step 1)

- (a) Effort that scales with a number unknown at planning time: review findings, verification failures and documentation warnings. Caps: 20 root causes each per pull request, exhaustion action `push -wip and hand off to the next session` (docs/operations/b10-abrupt-contract.md line 79). The cold-review budget falls under docs/agents/pull-requests.md rule 8. The repetition gate is fixed at 50 runs, the T7 outcome sample at 220 runs and the mutation list at 12; none scales.
- (b) Where the plan is stricter than its parents: decision 6752078378 asks only for the fix and a plan. This plan adds the reproduction on the base (command 5), the 50-run repetition gate, the real-client test T7 and the mutation table, as the planning prompt from the issue-347 control session required. R2 (no waiting on the callback thread) is stricter than "do not lose the lock"; its basis is in section 12, note 1.
- (c) Tests that depend on code, data or behaviour neither on `main` nor in this pull request: T7 depends on the librdkafka bundled with the pinned Confluent.Kafka 2.16.0 (`Directory.Packages.props`, on `main`), on loopback refusing TCP port 1 in the test host, and on librdkafka naming the bootstrap broker `127.0.0.1:1/bootstrap` with error counters at 0 in its statistics. All three were observed in the prototype runs; the last also matches the successor fix plan's T9. The duplicate bootstrap key the reproduction check saw is DA2 and T7 outcome B. T7's outcomes A and C depend on `KafkaStatisticsSession` behaviour on `main` (`Capture` lines 106-111 and the parser), which the capture-loss race fix may change (section 13). T7 also depends on the successor fix's R10 production handler, which is neither on `main` nor in this pull request. It will be on the base, because this pull request is implemented after the successor fix (decisions 6752454732 and 6752554218); the stop rule covers its absence. The prototype checked the same call order with the two calls of `B10AbruptHost.cs` line 232.

## 12. Sources

Files on `main` at `f5cc496fa3cf845e68e400b5b65e557f121dd928`:

- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptStatistics.cs`: lines 15-35, 94-140, 147-151, 164-232, 234-293, 305-324.
- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProducer.cs`: lines 171, 238, 279, 357, 400, 483, 559-580, 634-643.
- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs`: lines 222-233.
- `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptDiagnostics.cs`: lines 39-55, 74-84.
- `src/Parchwell.Kafka/Statistics/KafkaStatisticsSession.cs`: lines 77-141, 320-440.
- `src/Parchwell.LoadGen/Run/Execution/RunEvidenceSinkRouter.cs`.
- `src/Parchwell.Kafka/Statistics/KafkaStatisticsBridgeFactory.cs`: lines 26-47.
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProducerTests.cs`: lines 26, 438, 586-636, 1213-1229.
- `tests/Parchwell.ArchitectureTests/B10AbruptFailureSiteTests.cs`: line 16.
- docs/operations/b10-abrupt-contract.md: lines 74, 79, 106, 130.
- docs/engineering/dotnet.md: "Testing" (test names).
- `.github/CODEOWNERS`; `Directory.Packages.props` line 9.

Other sources:

- The seven trial artifacts and log hashes in section 6.
- `items/404/plan-successor-fix.md` and `items/404/plan-pr3a-evidence-completeness.md` on `claude/404-plan` at `acadeaa`, for section 13.

Notes:

1. R2's basis. `KafkaStatisticsSession.Capture` is documented as recording "a native callback report without blocking the Kafka callback thread" (`KafkaStatisticsSession.cs` line 87), and `main`'s `CaptureNative` already avoided waiting by using `TryEnter`. Inference: the design intends the callback thread never to wait on adapter work. A blocking lock would make that thread wait behind `Observe`'s JSON parse or `CapturedBytes` serialization; whether that would delay other librdkafka callbacks is not verified.

## 13. Order and files shared with the other issue-404 pull requests

Order (decision 6752454732): successor fix, then this pull request, then PR3a. The capture-loss race fix runs alongside on its own branch. This pull request is implemented on `main` after the successor fix merges, or on a branch from the successor fix's unmerged head; it does not merge before the successor fix (decision 6752554218 point 2).

Read on `claude/404-plan` at `5eaa316`:

- Successor fix (`items/404/plan-successor-fix.md`, Revision 4). Its files include `B10AbruptProducer.cs`, `B10AbruptHost.cs`, `B10AbruptDiagnostics.cs`, `docs/operations/b10-abrupt-contract.md`, existing Abrupt test files, new `B10AbruptSuccessorCompletionTests.cs`, `items/404/state.md` and its receipt. It lists `B10AbruptStatistics.cs` as must not change. Its R10: "the host's handler first passes it to the B10 statistics adapter's native-capture tracking, then to the statistics session. Observed by T13 on the handler installed in that client. T1 to T9 use the same production handler." T7 uses that handler.
- PR3a evidence completeness, Revision 8 at `d293b4b659261c7b4d4c3c5691328fd79a07b00e`, approved by [6753938298](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6753938298) under standing approval. It lists `B10AbruptStatistics.cs` as must not change, keeps `not_implemented_pr3` in its `SealCohort` (command A8), and states that the only file it shares with this plan is `items/404/state.md`. That still holds for Revision 2: this revision adds only a test case in this plan's own new file.
- Capture-loss race fix (`items/404/plan-capture-loss-race-fix.md`, Revision 2): changes `src/Parchwell.Kafka/Statistics/KafkaStatisticsSession.cs`, a new `tests/Parchwell.Kafka.Tests/Statistics/KafkaStatisticsLateCaptureTests.cs`, `docs/operations/statistics-bridge.md` line 87, `items/404/state.md` and its receipt. It keeps `capture_contention` reachable while capture is open. It does not change `B10AbruptStatistics.cs`.

Files this plan and another plan both change: `items/404/state.md` (all four), and the directory `tests/Parchwell.LoadGen.Tests/Abrupt/` under PR3a's wildcard only; this plan's test file is new and named in no other plan. No source file is shared. This plan reads three things other plans change: the production statistics handler (successor fix R10, used by T7), the base test totals, and `KafkaStatisticsSession.Capture` and its diagnostic names (capture-loss race fix; T7's outside outcomes).

Effects on later plans: the burst-suite totals rise by 9 after this pull request. The successor fix's plan names guard reason 3 as an outside race; after this pull request reason 3 cannot occur.

## 14. Open questions for Hari

None. The order question is answered by decision 6752454732; the guard reason 3 question by decision 6753263792 point 1. No protected path is touched. No question is decided by this plan.

## 15. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" in any of these cases:

- it needs to change a contract in section 4, a requirement in section 5, the scope in section 3, or a value in 3.2;
- decision 6753263792 or 6754092685 is reversed or changed in a way that changes a requirement, a test outcome or a deployment assumption here;
- the successor fix, merged or as the stacked base, has no production statistics handler that tests can use as its R10 and T1 to T9 do, or that handler does not call `CaptureNative` and then `KafkaStatisticsSession.Capture` on the callback thread (I1);
- the successor fix, or any other issue-404 pull request merged before this one, changes `B10AbruptStatistics.cs`, `KafkaStatisticsSession.Capture`, the session diagnostic names T7 reads (`capture_contention`, `duplicate_property`, `invalid_or_missing_field`), or the report shape `R(ts)` relies on, in a way that changes a requirement or a literal expected value here other than the base totals of command 3;
- a repetition, mutation or CI failure has any cause other than T7's accepted outside outcomes A, B and C, including a T7 guard reason 7 failure whose completion diagnostics contain neither `duplicate_property` nor `invalid_or_missing_field`.

Textual conflicts in `items/404/state.md` are resolved by the implementer without changing the other pull request's lines.

Choices this plan leaves to the implementer, recorded in the receipt with reasons: code text, private names, the concurrent structure used for pending reports, how the tests hold the adapter lock, and the test helper shapes.

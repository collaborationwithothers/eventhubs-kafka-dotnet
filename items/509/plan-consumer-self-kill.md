# Plan: consumer self-termination at a fixed point (#509)

Issue: [#509](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509), child of [#499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499), split entry 6. Pull request name: `consumer-self-kill`. This is the contract-grade plan for that one pull request under docs/agents/factory.md "Item lifecycle" step 3.

Revision 2. Base: `main` at 81d880cb901246b43043253d313a12173cd31316, the merge of #505's pull request #524. File paths and line numbers below are on that commit unless a line says otherwise.

Changes since revision 1, from the [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6909175276) and [decision 6909196149](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6909196149):
- Check findings 1 to 3 (`apply in revision`): their exact replacement texts are applied in section 6 row K2, section 3.5 step 4 and section 10 (b).
- Check finding 4: [decision 6909196149](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6909196149) adopts the recommended answers. Q1: D1 to D3 are accepted by name and stay in section 7 with a link to it. Q2: the mutual exclusion of `KillCount` and `SlowMessage` in section 3.1 is now decided, not open. Section 11 reads `None`.
- Section 1 lists [decision 6909196149](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6909196149) as authority 5.

This pull request lets the subject member of a C10 or C01 repeat end its own process with SIGKILL when `Consume` returns its Nth message, after it records a kill-intent row. It adds a kill count to the consumer processing plan, a check that the signal call works before the member registers, and the kill itself. It changes nothing for a plan without a kill count, so C03 and every existing test behave as today.

Design authority: ADR-022 in `docs/blueprint.md` on `main`, lines 735 to 794, and the ADR-020 exception it adds (line 639). docs/blueprint.md is the design authority (AGENTS.md).

## 1. Goal and authority

Goal: a subject member that, under a plan with kill count N, processes its first N - 1 messages as #505 does, then writes the `kill_intent` row for message N and sends SIGKILL to its own process, with no close, no commit call, no offset store and no receipt after that row. This is the fault C10 and C01 need (#510), and it produces the record that #506's rule 8 ("Stop") accepts.

Authorities, in creation order (factory.md "Item lifecycle" step 3, child issues):

1. #509 body: `Parent items: #499`, `Split entry: 6 (Consumer self-termination at a fixed point)`, `Predecessor PRs: the pull request of #505 (entry 2)`. Scope: "The subject consumer ends its own process once per repeat at a point fixed in the scenario, under the ADR-020 exception ADR-022 adds. No signal to any other process." Planning by Claude with `grilling` then `plan-pr`; Codex implements; the Claude review gate of decision 6894587233 applies.
2. #509 [decision 6895300516](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6895300516) adopts #499 [decision 6895299617](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895299617) (grilling round 1). Point 5: "only the subject consumer may end its own process, once per repeat, at a point fixed in the scenario, and it sends no signal to any other process." Point 7: no consumer outcome counts when "the subject ends other than at the fixed point".
3. #509 [decision 6895344090](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6895344090) adopts #499 [decision 6895343227](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895343227) (round 3). Point 1: SIGKILL, "so there is no consumer close and no final commit"; the kill point "is a count of processed messages fixed in the scenario"; "The subject writes a kill-intent row to its record immediately before the kill"; #510 chooses the counts.
4. #509 [decision 6895538069](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6895538069) adopts the standing #499 [decision 6895535291](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291): the #347 control session coordinates; grilling questions are written with recommended answers and adopted in advance; plan approval on a fresh check with `Blocking findings: none` and section 11 reading `None`; point 4 limits.

5. #509 [decision 6909196149](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6909196149) records Hari's adoption, under point 2 of decision 6895535291, of the recommended answers to revision 1's Q1 (deployment assumptions D1, D2 and D3 accepted by name) and Q2 (`KillCount` and `SlowMessage` mutually exclusive in one plan).

Round 2 ([6895328446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895328446)) is not adopted on #509; it does not name entry 6. This plan uses its content only through ADR-022's merged text.

Parent decisions read as context (#509 body names them): #499 [6894534441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441) point 2 (the consumer ends its own process at a point fixed before the run) and point 3 (K-57 under C10), [6894564479](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894564479), [6894587233](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894587233) (split approved; Claude review gate), [6894614673](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894614673) (Claude plans; Codex implements). None conflicts with this plan.

Ordinary choices under factory.md "Act or ask", each reversible and inside the approved split:
- Kill count source. #499 [decision 6899939624](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6899939624) point 1, Hari's own answer, gives the production plan source to #508 "and #510 extends them for C10 and C01". It is not adopted on #509. This plan follows it: the kill count is added to the plan seam that #505 shipped, with no production source. A `## Decision` on #509 adopting 6899939624 would close the gap.
- Record format. The `kill_intent` row is the #506 plan's section 3.2 row, which #505 already serializes (`ConsumerRecordCodec.cs` lines 68 to 72). #506's approved plan is revision 5 at a4c5ea1bec8630359a31d9b096416b2204f85dc7 ([approval 6901013311](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/506#issuecomment-6901013311)). That approval binds #506, not #509; following it keeps one owner of the format, as #505 did.

Evidence, not authority: [proposal revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894560135), "Proposed split" row 6; the merged #505 plan, revision 4, `items/505/plan-consumer-worker.md` at c617dfba872ff10b89cf248dfd044af7ec135a74.

Predecessor. `Predecessor PRs:` names the pull request of #505. That is #524, merged on 2026-11-16 as 81d880cb901246b43043253d313a12173cd31316 (factory.md step 3, "verify every entry in its `Predecessor PRs:` has merged").

There is no item state file for #509 (`items/509/` does not exist on the base) and no parent plan: #499 holds a proposal. Implementing tool: Codex (decision 6894614673 point 2).

## 2. Scope

Files this pull request may add or change:
- `src/Parchwell.Enrichment/ConsumerProcessing/**`.
- `src/Parchwell.Enrichment/Program.cs`: in `BuildConsumerProcessingHost` only, the registration of the production signal seam (3.4), with `TryAddSingleton` so tests can replace it, as `Action<int>` is on line 225.
- `tests/Parchwell.Enrichment.Tests/ConsumerProcessing/**`.
- `items/509/state.md` (factory.md "Item state file").

Must not change: `src/Parchwell.Enrichment/SeedF04/**` and every other mode of `Program.cs`; `src/Parchwell.Enrichment/Parchwell.Enrichment.csproj` and every lock file (no new package, project or assembly reference); `src/Parchwell.Kafka/**`, `src/Parchwell.Scenarios/**`, `src/Parchwell.LoadGen/**`, `src/Parchwell.Verifier/**`; `Directory.Packages.props`, `Directory.Build.props`; `scenarios/`, `results/`, `docs/`, `charts/`, `infra/`, `.github/`, `scripts/`; `AGENTS.md`, `CLAUDE.md`, `README.md`.

Out of scope, owned elsewhere: the production plan source and the scenario field for the kill count (#510, decision 6899939624 point 1); the counts themselves (#510, round 3 point 1); the verifier's stop rule (#506 plan rule 8); the subject Job's restart policy and the workflow's handling of a killed subject Job (#507); the judge's handling of a subject with no completion receipt (#521). Section 10a lists what each must do.

No protected path and no P1 item under AGENTS.md "Code Review Rules" is touched.

## 3. External contracts

Everything in this section is read by #506, #507, #510 or #521, or is behaviour the verifier judges. The implementer copies it exactly.

### 3.1 Processing plan field

`ConsumerProcessingPlan` (`ConsumerProcessingPlan.cs` lines 13 to 49) gains one field:

| Field | Rule |
|---|---|
| `KillCount` | absent (`null`), or integer 1 to 1000000 |

The range equals `KillCount` in the #506 plan's section 3.4 (line 124 at a4c5ea1bec8630359a31d9b096416b2204f85dc7), so a plan both components accept means the same N.

Rules across fields, added to the checks `ApplyAndValidate` makes before the registration marker is written (lines 21 to 45):
- `KillCount` present only when `Role` is `subject`.
- `KillCount` and `SlowMessage` never both present.

A failure of either rule, or a `KillCount` outside 1 to 1000000, is `consumer_plan_invalid`, as for the existing rules. A plan source that serves both members of a repeat must therefore resolve the member's role, which it can read from `ConsumerProcessingOptions.Role`; the same already holds for `SlowMessage` (line 25).

### 3.2 Exit codes

Unchanged from #505 plan section 3.1 for every path that exists today. New paths:
- The kill itself: the process is ended by SIGKILL and returns no exit code of its own.
- Every new failure in 3.3: exit 1, with the 3.3 line on standard error.

### 3.3 Failure names

Written to standard error in the existing format, `consumer_processing_failed reason=<name> scenario_id=<ScenarioId or unknown> run_id=<RunId or unknown> role=<Role or unknown>` (`ConsumerProcessingWorker.cs` line 99), before exit 1. New names:
- `consumer_self_kill_unavailable`: the preflight signal call (3.5 step 1) did not return 0, or threw.
- `consumer_self_kill_failed`: the SIGKILL call (3.5 step 4) returned.
- `consumer_kill_count_not_reached`: the window ended before `Consume` returned the Nth message.

Existing names keep their meaning. When an existing failure happens first, its name is written, not a new one.

### 3.4 Signal seam

The worker reaches the operating system through one seam at our own boundary (docs/engineering/dotnet.md "Seams and dependencies"): a call that sends a given signal number to the current process and returns the call's return value and `errno`. The production implementation calls libc `kill(Environment.ProcessId, signal)` through `DllImport("libc", EntryPoint = "kill", ExactSpelling = true, SetLastError = true, CallingConvention = CallingConvention.Cdecl)` and reads `errno` with `Marshal.GetLastPInvokeError()`, as `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProcess.cs` lines 47 to 56 do, including its `SYSLIB1054` suppression. The worker passes only signal 0 and signal 9, and only its own process ID. The seam's names and shape are the implementer's; tests observe the signal numbers passed in order.

### 3.5 Kill protocol

With `KillCount` N present, the member runs #505's protocol (#505 plan section 3.7) with these changes:

1. After the plan is validated and before the registration marker is written, call the seam with signal 0. If it returns other than 0, or throws, exit 1 with `consumer_self_kill_unavailable`: no blob is written and no Kafka client is constructed. kill(2): "If sig is 0, then no signal is sent, but existence and permission checks are still performed" (S2).
2. Count every message `Consume` returns, from 1, as #505's step 3 does (`ConsumerProcessingWorker.cs` line 60, `++ordinal`). Messages 1 to N - 1 are processed under #505's step 4, unchanged.
3. When `Consume` returns message N, before any processing: take the time from `TimeProvider`, then append one `kill_intent` row with that message's `partition` and `offset`, that time as `event_utc`, and the next `row_sequence`. No processing delay is waited, no `processed` row is written and no `StoreOffset` call is made for message N.
4. Immediately after that append returns, call the seam with signal 9. Between the append and that call the member makes no `Consume`, `StoreOffset`, `Commit`, `Close`, `Dispose`, `Pause` or `Resume` call and appends no row. Confluent.Kafka documents the assigned, revoked, lost and offsets-committed handlers as executing as a side-effect of `Consume`, `Close` or `Dispose` (S3). That they run inside no other call, so that no handler row can follow the `kill_intent` row, is inferred from that documentation.
5. If the seam call returns (with any value), write the 3.3 line with `consumer_self_kill_failed`, invoke the existing exit seam (`Action<int>`, `Program.cs` line 225) with 1, and return 1. On this path the member makes no `Consume`, `StoreOffset`, `Commit`, `Close` or `Dispose` call, appends no row and writes no receipt.
6. If the `kill_intent` append fails, #505's append-failure path applies unchanged (#505 plan requirement 6): no signal 9, `Close` attempted within 30000 ms, no receipt, exit 1 with `consumer_record_append_failed`.
7. If the window ends before `Consume` returns message N, close as #505's step 6 does, including the rows of the handlers `Close` invokes, then write no receipt and exit 1 with `consumer_kill_count_not_reached`.

The watchdog (#505 plan section 3.7 step 2) is unchanged.

With `KillCount` absent, the member makes no seam call and behaves exactly as on the base.

The resulting record for a completed kill: rows with `row_sequence` 0 to K, exactly N - 1 `processed` rows, the last row `kind` `kill_intent`, and no completion receipt. This is the shape the #506 plan's rule 8 accepts ("Stop", line 197 at a4c5ea1bec8630359a31d9b096416b2204f85dc7: with `KillCount` N, the subject's last row is its only `kill_intent` row, it has no receipt, and it has N - 1 `processed` rows).

## 4. Requirements

1. WHEN a plan has `KillCount` outside 1 to 1000000, or `KillCount` with `Role` `witness`, or `KillCount` together with `SlowMessage` THEN the member exits 1 with `consumer_plan_invalid`, makes no seam call, writes no blob and constructs no Kafka client.
2. WHEN a plan has `KillCount` THEN the member calls the seam with signal 0 once, after plan validation and before the registration write.
3. WHEN that call returns other than 0, or throws THEN the member exits 1 with `consumer_self_kill_unavailable`, writes no blob and constructs no Kafka client.
4. WHEN `Consume` returns message number N, counted across all partitions THEN the member appends a `kill_intent` row with that message's partition and offset, before processing it, and then calls the seam with signal 9, with no other consumer call or append in between.
5. WHEN message N arrives THEN messages 1 to N - 1 each have one `processed` row, and message N has none and no `StoreOffset` call, whatever the effective `enable.auto.offset.store`.
6. WHEN the signal 9 call returns THEN the member writes `consumer_self_kill_failed`, requests exit 1 through the exit seam and returns 1, with no further consumer call, row or receipt.
7. WHEN the `kill_intent` append fails THEN the member makes no signal 9 call and follows #505's append-failure path, ending with `consumer_record_append_failed`.
8. WHEN the window ends before message N THEN the member closes as on the base, writes no receipt and exits 1 with `consumer_kill_count_not_reached`, with no signal 9 call.
9. WHEN a plan has no `KillCount` THEN the member makes no seam call, and every existing test in `tests/Parchwell.Enrichment.Tests/` passes with its assertions unchanged.
10. WHEN the production seam is called with signal 0 for the test process THEN it returns 0.

## 5. Invariants and safety rules

- The only signal the member sends is to its own process ID, signal 0 or 9, at most once each per run (ADR-020 line 639; #509 body: "No signal to any other process").
- No process is started, no shell is invoked and no Kubernetes API is used (ADR-020 line 639).
- Only the subject sends signal 9, and only under a plan with `KillCount` (ADR-022 "Self-termination").
- No explicit `Commit` in any path, as on the base (ADR-022 "Record": "No extra commit is made to generate evidence").
- No new package, project or assembly reference; no change to the csproj or lock files.
- Time from `TimeProvider` only.
- No test sends signal 9 to any process. Tests replace the seam with a fake; the one real seam call in tests sends signal 0 to the test process (requirement 10).

## 6. Test map

Unit tests use the existing harness (`tests/Parchwell.Enrichment.Tests/ConsumerProcessing/ConsumerTestHarness.cs`): fake consumer, fake record store and test clock, with a fake signal seam that records each signal number in the harness's call log and returns `(0, 0)` unless a row says otherwise. Base fixture unless a row says otherwise: Role `subject`, `ConfigurationId` `C1`, `WindowMs` 1000, `ProcessingDelayMs` 0, `KillCount` 3, effective `enable.auto.offset.store` `false`, and one `assigned` row for partitions [0, 1] at `row_sequence` 0. Names follow `Given_<state>_When_<action>_Then_<outcome>`.

| Req | Test | Input | Literal expected values |
|---|---|---|---|
| 1 | K1 | `KillCount` 0, 1, 1000000, 1000001 | invalid, valid, valid, invalid; each invalid case: exit 1, standard error line starts `consumer_processing_failed reason=consumer_plan_invalid`, seam calls 0, store calls 0, consumer factory calls 0 |
| 1 | K2 | `KillCount` 3 with Role `witness`, JobName `run1-c1-r2-consumer-witness` and PodName `run1-c1-r2-consumer-witness-abcde`, as the existing `witness-slow` case sets them (`ConsumerProcessingWorkerTests.cs` lines 99 to 104); `KillCount` 3 with `SlowMessage` (Ordinal 1, DurationMs 500, Mode `inline`) | each: exit 1, `reason=consumer_plan_invalid`, seam calls 0, store calls 0, consumer factory calls 0 |
| 2 | K3 | valid base plan; `Consume` returns nothing until the window ends | the first seam call is signal 0 and precedes the registration write in the call log; then K10's outcome |
| 3 | K4 | the fake returns `(-1, 1)` for signal 0; separately it throws `EntryPointNotFoundException`; separately `DllNotFoundException` | each: exit 1, `reason=consumer_self_kill_unavailable`, seam calls exactly [0], store calls 0, consumer factory calls 0 |
| 4, 5, 6 | K5 | `Consume` returns, in order, partition 0 offset 10, partition 1 offset 20, partition 0 offset 11, then partition 1 offset 21 if called again | rows: `assigned` 0; `processed` p0 o10 at 1; `processed` p1 o20 at 2; `kill_intent` `partition` 0, `offset` 11, at `row_sequence` 3; no row after it. `StoreOffset` calls for offsets 10 and 20 only. Seam calls exactly [0, 9]. In the call log, the `kill_intent` append is immediately followed by signal 9, and no `Consume` call follows the call that returned p0 o11. `Close` calls 0, `Dispose` calls 0, `Commit` calls 0, no receipt. Exit seam calls [1]; return 1; standard error line starts `consumer_processing_failed reason=consumer_self_kill_failed` |
| 4, 5 | K6 | `KillCount` 1, effective `enable.auto.offset.store` `true`, `ProcessingDelayMs` 5000; `Consume` returns partition 1 offset 7 | rows: `assigned` 0; `kill_intent` `partition` 1, `offset` 7, at `row_sequence` 1; no `processed` row; `StoreOffset` calls 0; the `kill_intent` `event_utc` equals the test clock's time when `Consume` returned (no 5000 ms wait); seam calls exactly [0, 9] |
| 4 | K7 | the `kill_intent` row's JSON | its property set equals the #506 plan's section 3.2 `kill_intent` row exactly: `schema_version` 1, `kind` `kill_intent`, `instance_id` equal to `PodName`, `row_sequence`, `partition`, `offset`, `event_utc` matching `^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$` |
| 7 | K8 | the fake store fails the append of the `kill_intent` row in K5's input | seam calls exactly [0]; `Close` called once; no receipt; exit 1; `reason=consumer_record_append_failed` |
| 8 | K9 | `Consume` returns 2 messages, then nothing until the window ends | 2 `processed` rows; no `kill_intent` row; `Close` called once; no receipt; seam calls exactly [0]; exit 1; `reason=consumer_kill_count_not_reached` |
| 8 | K10 | as K3 | no `kill_intent` row; `Close` called once; no receipt; seam calls exactly [0]; exit 1; `reason=consumer_kill_count_not_reached` |
| 9 | K11 | the harness's default plan (no `KillCount`), 3 messages | seam calls 0; exit 0; receipt written |
| 9 | existing | every test in `tests/Parchwell.Enrichment.Tests/` | passes; the harness and the component tests' `StartMember` gain only the seam argument (section 6, fixture rule) |
| 10 | K12 | the production seam, signal 0, current process | return value 0 |

No component test is added. A member runs inside the test process in the component tests (`ConsumerProcessingComponentTests.cs` line 187), so a real SIGKILL there would end the test run. A fake kill leaves the member's librdkafka handle alive and heartbeating, which is not the behaviour C10 and C01 measure. Group behaviour after a real kill is the live measurement of #510's smoke run. The B10 precedent for a real-process kill test (`tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptRealProcessTests.cs`) needs the worker to start as a child process; the consumer mode has no production plan source on the base (`Program.cs` line 154), so such a test would need production test hooks this plan does not add.

Existing tests: the worker's constructor gains the seam, so `ConsumerTestHarness.cs` line 40, `ConsumerProcessingComponentTests.cs` line 187 and the host registration change. Standing fixture rule: when an existing test's fixture is rejected by a new check, the implementer does exactly one of (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; or (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce. It never removes a case, loosens an assertion or changes a check. The pull request's implementation notes list each such change with the check that forces it; the cold review and Codex review check each one. Model: [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2. Adding the seam argument to a constructor call is a fixture change under (a) and is listed the same way.

`Given_U23_normal_worker_When_completed_Then_exact_completion_format_has_no_kill_intent` (`ConsumerRecordCodecTests.cs` line 65) runs the default plan, which has no `KillCount`, so its assertion that no `kill_intent` row is written stays true and unchanged.

## 7. Deployment assumptions

No test in this pull request sends SIGKILL or runs on AKS. Each item below is accepted by name by [decision 6909196149](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6909196149) point 1, under point 2 of decision 6895535291.

- D1. The production seam resolves libc `kill` in the enrichment-consumer image and SIGKILL ends that process. Evidence: the enrichment and load generator images use the same base image digest (`src/Parchwell.Enrichment/Parchwell.Enrichment.csproj` line 8 and `src/Parchwell.LoadGen/Parchwell.LoadGen.csproj` line 8). The B10 scientific run 43689822806 recorded `termination` class `intended_self_kill` for its first restart producer (`results/B10/2026-11-15.json` line 792), which uses the same `DllImport` path (`B10AbruptProcess.cs` lines 47 to 56). That evidence was taken in the load generator image, on a fresh pod, under producer load, not in the enrichment image. The preflight (3.5 step 1) turns a missing entry point into a registration-free exit before any consumer is constructed.
- D2. Kubernetes does not restart the killed subject. ADR-020 sets `backoffLimit: 0` and `restartPolicy: Never` for each Job (`docs/blueprint.md` line 548), which #507 implements. Evidence: none for the consumer Jobs, which do not exist yet. If a second subject pod ran, it would register under a new pod name, and the verifier would reject the repeat for "other than two consumer members registered" (ADR-022 "Invalid run").
- D3. A returned signal 9 call (3.5 step 5) leaves a record that ends at its `kill_intent` row, as a real kill does, so the verifier's rule 8 cannot tell the two apart. The process then ends through the exit seam with code 1 and the `consumer_self_kill_failed` line on standard error, which are pod diagnostics only (ADR-020). Evidence: none that the call can return; kill(2) lists errors only for an invalid signal, a missing process or no permission (S2), and the preflight checks the last two for the same process.

## 8. Acceptance commands

Run on the pull request branch.

1. Design-authority check: `git fetch origin main && git diff 81d880cb901246b43043253d313a12173cd31316 origin/main -- docs/blueprint.md`. Evidence: empty output, or a statement in the implementation notes that no changed line is in ADR-020 line 639 or ADR-022 (lines 735 to 794 at the base). If either changed, stop (section 12).
2. `./scripts/verify-dotnet.sh --project Parchwell.Enrichment.Tests` while work moves (docs/agents/pull-requests.md "Verification gates" step 1). Evidence: pass counts, cited as a partial run.
3. Cold review under docs/agents/pull-requests.md rule 8 (gate step 2).
4. `./scripts/verify-dotnet.sh --tier sandbox` once on the reviewed source (gate step 3). Evidence: the summary, recorded as a partial run excluding `Tier=CGroupV2`.
5. `git diff --name-only origin/main...HEAD`. Evidence: only section 2's paths.
6. A script that lists every row ID K1 to K12 with the test in `tests/Parchwell.Enrichment.Tests/ConsumerProcessing/` that covers it and that test's pass result from the step 4 run. Evidence: 12 lines, all passed.
7. A script that searches `src/` for calls to the signal seam and for `kill` `DllImport` declarations. Evidence: the production `DllImport` appears once in `src/Parchwell.Enrichment/ConsumerProcessing/`, and the worker passes only the literals 0 and 9 and only `Environment.ProcessId`.
8. `npm run check:docs`. Evidence: the summary line.
9. Hosted `.NET verification` on the final head (gate step 4), Codex review, and the Claude review gate of decision 6894587233 point 2.

## 9. Sources

External behaviour (plan-pr "Plan rules" rule 1). Tool: raw fetches of the pinned files with `curl` on 2026-11-16, read in this session.

- S1. librdkafka v2.16.0 [CONFIGURATION.md](https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md): line 131 `session.timeout.ms` ("If no hearts are received by the broker for a group member within the session timeout, the broker will remove the consumer from the group and trigger a rebalance"); line 140 `enable.auto.offset.store` ("Automatically store offset of last message provided to application"); line 138 `enable.auto.commit` ("Automatically and periodically commit offsets in the background"). These are the behaviours C10 and C01 measure after the kill; this pull request relies on none of them for its tests.
- S2. Linux man-pages [kill(2)](https://man7.org/linux/man-pages/man2/kill.2.html): "If pid is positive, then signal sig is sent to the process with the ID specified by pid"; "If sig is 0, then no signal is sent, but existence and permission checks are still performed"; its ERRORS section lists only `EINVAL` ("An invalid signal was specified"), `EPERM` and `ESRCH`. [signal(7)](https://man7.org/linux/man-pages/man7/signal.7.html): `SIGKILL` default action `Term`; "The signals SIGKILL and SIGSTOP cannot be caught, blocked, or ignored." That the .NET runtime runs no managed code after SIGKILL follows from "cannot be caught" (inference).
- S3. Confluent.Kafka v2.16.0 [ConsumerBuilder.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ConsumerBuilder.cs): the assigned handler "Executes as a side-effect of the Consumer.Consume call (on the same thread)" (line 464); the revoked handler "Executes as a side-effect of the Consumer.Consume/Close/Dispose call (on the same thread)" (line 552); the offsets-committed handler "Executes as a side-effect of the Consumer.Consume call (on the same thread)" (line 712); the lost handler "May execute as a side-effect of the Consumer.Consume/Close/Dispose call (on the same thread)" (lines 645 and 646).
- S4. `docs/blueprint.md` at the base: ADR-020 line 639 (the second exception: "the subject consumer of a C03, C10 or C01 repeat whose configuration names a kill count may also send SIGKILL only to its own process, once per repeat, and starts no process"); ADR-022 "Self-termination" line 756, "Record" line 753, "Invalid run" line 775 and its list, "Consequences" line 787 and its third bullet (store-after-process versus auto offset store at the kill).
- S5. #506 plan, revision 5, `items/506/plan-consumer-verifier.md` at a4c5ea1bec8630359a31d9b096416b2204f85dc7: line 94 (`kill_intent` fields), line 124 (`KillCount` range), line 197 (rule 8, "Stop").

Repository: `src/Parchwell.Enrichment/ConsumerProcessing/*.cs`, `Program.cs` lines 143 to 232; `tests/Parchwell.Enrichment.Tests/ConsumerProcessing/**`; `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProcess.cs` lines 47 to 56; `results/B10/2026-11-15.json` line 792; the two csproj files' line 8.

## 10. Plan-check questions (factory.md "Item lifecycle" step 1)

(a) Effort that scales with a number unknown at planning time: none. The tests are fixed by section 6, and the kill counts are #510's.

(b) Where this plan is stricter than, or goes beyond, its authorities:
- The signal 0 preflight before registration. ADR-022 does not require it. It makes a missing entry point fail before the member registers, so the repeat is invalid under "other than two consumer members registered" instead of producing a subject that cannot reach its fixed point.
- `KillCount` and `SlowMessage` are mutually exclusive. ADR-022 does not forbid both. C10 and C01 use self-termination as their crash mechanism (#499 [decision 6894534441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441) point 2; blueprint section 12 rows C01 line 1159 and C10 line 1168) and C03 tests a slow message (row C03 line 1161); that none of them needs both is inferred from those sources, and with both the paused buffer would make "the Nth message `Consume` returns" depend on pause timing. It constrains #510; [decision 6909196149](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6909196149) adopts it (revision 1's Q2).
- `KillCount` on the witness is rejected, matching `SlowMessage` (`ConsumerProcessingPlan.cs` line 25) and ADR-022 "only the subject".
- `consumer_kill_count_not_reached` and `consumer_self_kill_failed` exits. ADR-022 leaves these paths to the verifier ("the subject ended other than at its kill-intent row"); the names make the cause visible in pod diagnostics without changing the record format.

(c) Tests or checks that depend on code, data or behaviour neither on `main` nor in this pull request:
- The verifier's rule 8 (#506, approved, not merged): K5's record shape is built to it; no test here runs the verifier.
- The production plan source with `KillCount` (#510): no test here needs it.
- D1 to D3 in section 7, accepted by name by decision 6909196149.

## 10a. Deferred work

1. Production plan source field `KillCount`. Evidence: no production `IConsumerProcessingPlanSource` exists on the base (`Program.cs` line 154). On the verdict path: yes. Why deferral is permitted: the counts are #510's (round 3 point 1) and the source is #508's, extended by #510 (decision 6899939624 point 1). Responsible: #510. Stage: before #510's smoke run. Completion evidence: a merged source that serves `KillCount` to the subject only. Gate it blocks: #510's smoke run.
2. Workflow and judge handling of a killed subject. Evidence: ADR-020 makes receipts the completion authority (`docs/blueprint.md` line 551), and a killed subject has none by design. On the verdict path: yes. Why deferral is permitted: the workflow is #507's and the judge is #521's (split entries 4 and 11), and neither can be tested before this record shape exists. Responsible: #507 (the subject Job ending by signal does not fail the dispatch before the verifier runs) and #521 (a kill-configured subject without a receipt is not a missing-receipt failure). Stage: before #510's smoke run. Completion evidence: each child's plan cites 3.5's record shape. Gate it blocks: #510's smoke run.

## 11. Open questions for Hari

None. Revision 1's Q1 and Q2 are answered by [decision 6909196149](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6909196149).

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it would need to change any contract in section 3, a requirement, the file scope, a test's literal expected value, or when acceptance command 1 finds ADR-020 line 639 or ADR-022 changed since the base. It also stops when #506's approved plan revision has a `kill_intent` row, `KillCount` range or rule 8 that differs from lines 94, 124 and 197 at a4c5ea1bec8630359a31d9b096416b2204f85dc7.

## 13. Size estimate

Estimate, 2026-11-16, from the size of the code it extends (`ConsumerProcessingWorker.cs` 183 lines, `ConsumerProcessingPlan.cs` 49 lines) and the B10 signal class (`B10AbruptProcess.cs` 57 lines): about 80 added or changed lines of production code in 3 to 4 files, about 350 added lines of tests in 2 to 3 files, plus `items/509/state.md`. About 8 files. Size is reported, not enforced.

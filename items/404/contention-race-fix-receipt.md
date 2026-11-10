# Capture contention race fix receipt

This change lets the B10 abrupt statistics adapter record each native statistics report without taking the adapter lock, so a reader holding that lock can no longer make it skip a report and fail the guard with reason 3. It changes one source file and adds one test class. It changes no protocol sequence, gate, policy value, timeout, interval, failure name, fact name, stage name or wire member.

## Execution card

- Deliverable and predicate map: requirements R1 to R8, tests T1 to T8 and mutations M1 to M12 of the [approved Revision 3 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/6373f8efbea7f47ad4239af1c43d6de3b449368d/items/404/plan-capture-contention-race-fix.md), sections 5, 7.2 and 7.3. That test map is the predicate map of docs/agents/pull-requests.md. Only the plan's section 3.1 files change.
- Authorization: [plan pointer](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6754905155), [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6755073034) (`Blocking findings: none`; P2-1 applied) and [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6755102182), posted under standing decision [6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614) point 2. Hari has not reviewed that approval. Implementing tool: Claude Code, decision 6752430614 point 3. Later decisions that alter a change in the plan: none; [6753263792](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6753263792) and [6754092685](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6754092685) are already folded into Revision 3.
- Mandatory gates: acceptance commands 1 to 10, the 50-run repetition gate, the base reproduction, mutations M1 to M10 killed by their required tests, cold review, final-head CI, Codex code and security review, the merge rule. No trial, dispatch, Azure access or results change.
- Verification commands: the plan's section 10, listed with results below.
- Publication checkpoint: after local acceptance, documentation check and applicable cold-review coverage, publish the tested pull request.
- Budgets and exhaustion: review findings, verification failures and documentation warnings capped at 20 root causes each, exhaustion action `push -wip and hand off to the next session` (plan section 11(a)). Cold review follows docs/agents/pull-requests.md rule 8 (two comprehensive and two focused rounds across all sessions); a stalled Codex review is re-requested once after 15 minutes (decision [6752554218](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752554218) point 1).

## Base and ordering

Branch `claude/404-capture-contention-race-fix`, stacked on the successor fix's unmerged head `9c84ba4174e2c7b40f0805b3e6cfe6efd26ba818` (branch `claude/404-successor-fix-wip`), which is based on `main` at `f5cc496fa3cf845e68e400b5b65e557f121dd928`. Decision [6752554218](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752554218) point 2 allows stacking on a predecessor's unmerged head; the 347 control session directed this base after the first cold review found that `main` was not an allowed base. This pull request does not merge before the successor fix. After it merges, it is rebased onto `main`, the plan's stop rule (section 15) is rechecked against the merged files, and it is reviewed again.

Stop-rule bullets 3 and 4 against this base: the successor fix has a production statistics handler member, `B10AbruptHost.StatisticsHandler`, which calls `CaptureNative` and then `KafkaStatisticsSession.Capture` on the callback thread (invariant I1), and T7 uses it. The successor fix does not change `B10AbruptStatistics.cs`, `KafkaStatisticsSession.Capture` or the diagnostic names T7 reads (`git diff` of the base against `main` lists no such file). The review diff for this pull request is its changes relative to `9c84ba4`.

## Implementation choices left to the implementer

- Pending structure: a `ConcurrentQueue` of `(length, SHA-256)` entries plus an `int` count reserved with a compare-and-swap loop. `CaptureNative` takes no lock; the reservation is what enforces the 2048 bound (R4, R7). `Observe` still dequeues under the adapter lock and decrements the count after a successful dequeue, so a matched report no longer counts toward the bound (R3, R4).
- `trackNativeCapture` became `volatile`, because `CaptureNative` now reads it without the lock.
- Reasons 1, 2 and the oversize checks are unchanged and run first (R6). The reason 4 site now covers both "tracking off" and "bound reached", as on `main`. No code sets reason 3 (R8).
- Test technique for the held lock: the test reads the private `gate` field by reflection, as `B10AbruptProducerTests` does at its line 438, and holds it with `lock` on a real thread released by an event. "Returned while held" means the capture thread was joined within 5 s while the lock was still held.
- T7 installs the successor fix's production handler, `B10AbruptHost.StatisticsHandler(stats, session)`, in the real client, as the plan's T7 input requires.
- T7 accepts the plan's outside outcomes by reading the guard reason from the exception's recorded site facts and the session's completion diagnostics.
- Plan-check P2-1 (`apply in implementation`) is applied: every test that holds the lock, T3 included, asserts "returned while held" before the lock is released. T3 therefore kills M2.

## Verification

SDK `10.0.402`. Source base `9c84ba4` plus the working tree this receipt describes. dockerd was started for command 8.

| # | Command | Result |
|---|---|---|
| 1 | locked restore of the LoadGen test project | exit 0, all projects up to date |
| 2 | Release build, no restore | `0 Warning(s)`, `0 Error(s)` |
| 3 | burst suite `*B10Abrupt*` | total 508, succeeded 507, skipped 1 (published-apphost case), failed 0; 508 minus the 9 new cases is 499, the implied base total, which was not run separately |
| 4 | 50 runs of the new class | `50 Total: 9, Errors: 0, Failed: 0` |
| 5 | new class against the base `B10AbruptStatistics.cs` | `Total: 9, Errors: 0, Failed: 6`; failing T1, T2, T3, T4 (both cases), T7; T7 message `guard_reason=3 diagnostics=completion_timeout`; T5, T6, T8 pass |
| 6 | mutations M1 to M12, scratch edits of the fixed file, one class run each | see below; production restored byte-identical (`cmp`) |
| 7 and 8 | `./scripts/verify-dotnet.sh` | exit 0, `Verification passed`; `Parchwell.ArchitectureTests: 17 passed`, Enrichment 56, Kafka 486, LoadGen 1728, Scenarios 340, Verifier 7389 |
| 9 | grep of `CaptureNative(`, `Guard(3)`, `TryEnter` | `CaptureNative(` at `B10AbruptHost.cs:232` (handler, ahead of `statistics.Capture(raw)`) and its definition at `B10AbruptStatistics.cs:117`; no `Guard(3)`; no `TryEnter` |
| 10 | `npm run check:docs` | no warning in `items/404/state.md` or this receipt once this file exists; nine warnings, all in other files and unchanged by this pull request |

Mutations, each built with 0 errors. Failed tests of the 9-case class:

| Id | Failed tests | Required kills met |
|---|---|---|
| M1 | T1, T2, T3, T4 (both), T7 | yes |
| M2 | T1, T2, T3, T4 (both) | yes |
| M3 | T1, T2, T4 (2049), T7 | yes |
| M4 | T2, T3, T7 | yes |
| M5 | T4 (2049) | yes |
| M6 | T4 (2048) | yes |
| M7 | T5 | yes |
| M8 | T6 | yes |
| M9 | T4 (2049) | yes |
| M10 | T8 | yes |
| M11 | none | recorded, not required (plan N8) |
| M12 | none | recorded, not required (plan N8) |

The mutation script's fixture lines are scratch work and are not committed. The failing-test list per mutation was read from the runner's `[FAIL]` lines. M11 (unsynchronized queue) and M12 (non-atomic count) survive, as the plan states in N8; R7 is left to the cold review.

## Stop-rule check

No contract, requirement, scope or 3.2 value changed. Decisions 6753263792 and 6754092685 are unchanged. The successor fix has not merged, so its handler, the files it may change and its effect on `KafkaStatisticsSession` cannot be checked yet; that check is repeated after rebase. No repetition, mutation or verification failure occurred outside T7's accepted outcomes.

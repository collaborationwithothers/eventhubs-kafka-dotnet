# Capture-loss race fix receipt

This change makes `KafkaStatisticsSession` ignore a statistics callback that starts after completion has closed capture, so such a callback no longer fails a run whose accepted reports all drained. A callback admitted before the close keeps main's handling, so a real capture gap is still reported. No diagnostic code, public member, wire field, timeout, gate or failure name changes, and no trial is authorized.

## Execution card

- Deliverable: the `Capture` behaviour of E1 and R1 to R6, the E3 remarks and the E4 sentence, with tests T1 to T3 and the plan-check P2-2 test, of the approved [Revision 7 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/d93859b251bcbe6c5039e06805c4064508c152e3/items/404/plan-capture-loss-race-fix.md). Only the files of its section 4.1 change.
- Authorization: [plan pointer 6760646338](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6760646338), [plan check 6760749353](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6760749353) (`Blocking findings: none`) and [approval 6760770654](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6760770654), posted by the control session under standing decision 6752430614 point 2, not after Hari's review. The plan file's SHA-256, `315b70fdc93947f26727a05882441e857b1d53e542786819f9023ff948b3b3a6`, was verified at commit `d93859b251bcbe6c5039e06805c4064508c152e3`. Later decisions applied: 6760139065, 6760170847, 6760180716, 6760201403 (all folded into the plan), 6760242955 (state-file misquote) and 6761095907 (the merge by the control session). 6761052971 (PR3b planning) does not touch this pull request.
- Mandatory gates: acceptance commands 1 to 8 of the plan, the nine mutations M1 to M7, M9, M10 (M8 is review-only), `npm run check:docs`, cold review, final-head CI, hosted Codex code and security review, and the merge rule. No trial, no dispatch, no Azure.
- Verification commands: plan section 11; mutations per class under `timeout 240`.
- Publication checkpoint: after local acceptance, mutation evidence, the documentation check and the applicable cold-review coverage.
- Budgets and exhaustion: repetitions are fixed at 25 and 20; each mutation class runs under `timeout 240` and exit 124 is recorded as "killed by hang" before continuing (plan 12(a)). Cold review: at most two comprehensive passes and two focused rounds across all sessions (docs/agents/pull-requests.md rule 8); exhaustion means save under rule 12 and hand off to Hari.

## Implementation and choices

Base: `origin/main` at `aadb0c6d5252d6ca9638d116c1bbf7339c17037e` (PR 421, 422 and 423 merged). The plan was written against `f5cc496f`. `git diff --name-only cb731bb aadb0c6d` touches nothing under `src/Parchwell.Kafka/`, `tests/Parchwell.Kafka.Tests/` or `docs/operations/statistics-bridge.md`, and `B10AbruptSuccessorCompletionTests.cs` still lists `late_capture`, `capture_contention` and `capture_loss` only in its code list. The stop rule's first two bullets are therefore not triggered.

Choices the plan leaves to the implementer:

- Admission: a private flag, `_admissionClosed`, is written with `Volatile.Write` followed by `Interlocked.MemoryBarrier()` inside the gate section of `CloseCapture`, before `_captureOpen` changes (I1). `Capture` reads it with `Volatile.Read` before `TryBeginCapture`. The barrier makes the order visible to a test thread that reads `_captureOpen` without the gate.
- `DisposeAsync` does not set the flag (R6); the P2-2 test pins this.
- Capture-closed observation: the reflection read of `_captureOpen` the plan names as a permitted example, polled every 1 ms under the 5 s watchdog; a missing field throws. The gate-free wait of T3 reflects `_gate` and takes it with a 5 s timeout. Both live in `SessionProbe`.
- Test doubles in `LateCaptureTestSupport.cs`: `HoldingStatisticsSink`, `PausingClock` (the next `GetTimestamp` read pauses) and `SessionProbe`. The sessions of T1, T3 and the P2-2 test come from `KafkaStatisticsBridgeFactory` through its internal constructor with a test clock and native compatibility asserted; T2 uses `AddParchwellKafkaStatistics`.
- T2 output: the runner hides the output of a passing test, so T2 also appends its outcome line to the file named by the environment variable `PARCHWELL_T2_OUTCOME_FILE` when it is set. This lets a repeated run count each outcome by script (plan 9: "the receipt counts each"). It has no effect when the variable is unset.
- The M1 mutant keeps the field written but writes `false`, because deleting the write fails the build with CS0649; behaviour is main's.
- Line numbers in the plan refer to `f5cc496f`; none is pinned.

Plan check findings applied (docs/agents/factory.md "Item lifecycle" step 1):

- P2-1: `items/404/state.md` line for decision 6753263792 now reads "accept as a deployment assumption that session-side lock contention in KafkaStatisticsSession.Capture does not occur in the next trial", without "before capture closes" or "while capture is open". Decision 6760242955 and the decisions the plan lists are recorded under "Decisions in force".
- P2-2: test `Given_SessionDisposedWithoutCompletion_When_CaptureFollows_Then_LateCaptureAndLossAreReported` added to `KafkaStatisticsLateCaptureTests`, with the input and literal values the check gives. It passes on `main` and on the branch (below).
- P2-3 and P2-4: informational. The state file's implementing-tool line is updated here; PR 422 and PR 423 have merged, so no rebase of the state file between pull requests remains.

## Predicate map

The plan's test map (section 9) is the map; this receipt adds the row for R6.

| Requirement | Test | Boundary |
|---|---|---|
| R1, E1, I3 | T1 `...CallbackAfterCaptureClosed...` (`capture-only`, `inventory`), T2 | just after the close point |
| R2, I1 | T3 | just before the close point (admitted, resumed after close) |
| R3, R4, R5 | existing `KafkaStatisticsBoundaryTests`, `KafkaStatisticsCaptureCompletionTests` | as on main |
| R6 | P2-2 test | disposal without completion, then a call |

## Results

All results are from this workspace (Linux, .NET SDK 10.0.402, Docker daemon started with `dockerd`), source SHA-256 `aa094c79b12939617007d6b842e9098d331f0b3e14df1bbb0fdd8338af40ba0f`, counted by script. "Under load" means 4 busy-loop processes on the 4 cores for the whole run.

- Acceptance 2, `main` plus the new test files only, under load: `KafkaStatisticsLateCaptureTests` 25 runs, every run `failed: 2` (both T1 cases; T3 and the P2-2 test pass). Diagnostics over the 25 runs: `capture_loss,late_capture` 48 times and `capture_contention,capture_loss` 2 times. `KafkaStatisticsNativeLateCaptureTests` 20 runs, every run `failed: 1` with `capture_loss,late_capture` 20 times.
- Acceptance 3, branch, under load: `KafkaStatisticsLateCaptureTests` 25 of 25 runs `failed: 0`; `KafkaStatisticsNativeLateCaptureTests` 20 of 20 runs `failed: 0`. T2 outcomes in a second 20-run series (rebased head): `complete` 20, accepted duplicate-key outcomes 0.
- Acceptance 4: namespace `Parchwell.Kafka.Tests.Statistics`, `total: 100`, `failed: 0` (99 of the plan plus the P2-2 test).
- Acceptance 1 and 5: `./scripts/verify-dotnet.sh` passed every step with the Docker daemon up: Kafka tests 491 passed, LoadGen 1719, Scenarios 340, Verifier 7389, Architecture 17, Enrichment 56; solution build 0 warnings, 0 errors.
- Acceptance 6: `npm run check:docs`: `docs/operations/statistics-bridge.md: no issues found`; 8 warnings in total, none in a file this pull request changes. The file's SHA-256 is `c525786e47467759bec4074bfd016cb5931c47441e0517456202a3536b560864`, as the plan records for the E4 text.
- Acceptance 7: `git diff --name-only origin/main...HEAD` lists only paths of plan section 4.1.

## Mutations (acceptance 8)

Each mutant was applied to the committed source, built, and run one Statistics test class at a time under `timeout 240`; the source was restored and its SHA-256 checked after each. `hang` is exit 124.

| Id | Killed by (this run) |
|---|---|
| M1 | T1 both cases, T2 |
| M2 | T1 both cases, T2 |
| M3 | T1 both cases, T2 |
| M4 | T3, P2-2 test |
| M5 | T1 both cases, T3, T2; 4 `KafkaStatisticsBoundaryTests`, 19 `KafkaStatisticsCaptureCompletionTests` cases; hang: `KafkaStatisticsCounterTests`, `KafkaStatisticsLifetimeTests`, `KafkaStatisticsSessionTests` |
| M6 | 2 `KafkaStatisticsBoundaryTests` cases, 1 `KafkaStatisticsCaptureCompletionTests` case |
| M7 | `Given_CaptureContendsWithCompletionGate_When_FreshReportFollows_Then_LossStaysLatched` |
| M8 | survives by construction; the cold review checks it (I1) |
| M9 | T3, P2-2 test |
| M10 | 3 `KafkaStatisticsBoundaryTests`, 19 `KafkaStatisticsCaptureCompletionTests`, 1 `KafkaStatisticsCounterTests`, 5 `KafkaStatisticsLifetimeTests` cases, T1 `capture-only`, T3, P2-2 test; `KafkaStatisticsSessionTests` hang with 3 failures |

Every mutant except M8 is killed, so the stop rule's mutation bullet is not triggered.

## Review record

Cold review rounds, complete reports and dispositions are recorded in the pull request description under AGENTS.md "Before opening a pull request" rule 8; this receipt does not describe its own head.

Cold review input, round 1 (comprehensive, cumulative counts before it: 0 comprehensive, 0 focused):

- Mode: comprehensive, the entire authored diff against `origin/main` at `aadb0c6d5252d6ca9638d116c1bbf7339c17037e`.
- Specification sources: the approved plan, readable with `git show origin/claude/404-plan:items/404/plan-capture-loss-race-fix.md` (commit `d93859b251bcbe6c5039e06805c4064508c152e3`, SHA-256 above); the plan check and approval linked above; decisions 6760139065, 6760170847, 6760180716, 6760201403 and 6760242955, linked from the plan and the state file. Plan check P2-1 and P2-2 are `apply in implementation`.
- Points the plan asks the cold review to check: mutation M8 (admission closes inside the same gate section that closes capture, no later than the capture-open state change, I1); E3 and E4 text byte-equality with the plan; the state file misquote correction.

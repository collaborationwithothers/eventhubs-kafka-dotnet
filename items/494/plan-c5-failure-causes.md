# C5 failure causes

This change set removes two causes that made paired C5 mutation runs on `main` fail without any mutation being at fault. The first is an intermittent verifier test failure: the consumed-metadata capture times its Blob writes on the real clock while the test drives the handler with a manual clock. The second is the companion evidence download deadline of 240 seconds, which one pair exhausted.

Revision R2. Base `main` commit `4ea8fae3d52111e37028c39033430bc0a3c40776`. One pull request, named `494-c5-failure-causes`.

## Goal and authority

Hari decided on 2026-11-24 that Claude Code plans and implements one change set that fixes both causes, accepts the re-qualification cost, and wants the plan to state what the change does to the qualified C5 layout and to do that work in the same change set ([decision 7142132171](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7142132171)). Claude Code owns the remaining work of this issue ([decision 7139344144](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7139344144)). The intermittent test is tracked as [issue 593](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/593), which defers its plan to this issue. Plan approval follows the standing rule named in decision 7142132171: a clean fresh check. Hari applies `ready-for-agent` and merges the protected pull request.

The campaign gates stay as set: 324 physical receipts, a 60% minimum mutation score and a 3,000-second paired limit from the first run's creation to the coordinator's completion ([decision 7123218757](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7123218757)).

### Diagnosis of the intermittent test

Evidence: [issue 593](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/593) records that Stryker's unmutated initial run in shard `b10-final-projection-codec-p2-a` of [run 45459740602](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45459740602) failed `B10SweepHandlerTests.Given_FiveEligibleRepeats_When_Swept_Then_ImmutableResultsReceiveFinalCountsAfterCleanup(phase: "C1")` at line 54, `Assert.True(capture.RootElement.GetProperty("complete").GetBoolean())`. The same shard passed on the same commit in run 45449734959.

Source facts, read on `main` at the base commit:

1. `ConsumedMetadataCapture.Seal` (chunk upload) and `ConsumedMetadataCapture.Finish` (`capture.json` write) each create `new CancellationTokenSource(_options.BlobIoTimeout!.Value)` at `src/Parchwell.Verifier/VerifyRun/Window/ConsumedMetadataCapture.cs` lines 179 and 218. That constructor counts on the system clock.
2. Every other Blob or cleanup deadline in the verifier handlers uses the injected clock, for example `B10SweepHandler.Bounded` at `src/Parchwell.Verifier/VerifyRun/B10SweepHandler.cs` lines 305-313 and the cleanup deadline at line 121.
3. `WindowConsumer.Drain` receives that clock as `timeProvider` but calls `capture?.Observe(record)` and `capture?.Finish(complete, interrupted)` without it (`src/Parchwell.Verifier/VerifyRun/Window/WindowConsumer.cs` lines 31, 73, 88 and 93).
4. The test's options come from `HostEvidenceBuilder`, which sets `BlobIoTimeout = TimeSpan.FromMilliseconds(100)` (`tests/Parchwell.Verifier.Tests/Support/HostEvidenceBuilder.cs` line 177). The handler runs on `ManualKafkaTimeProvider`, which never advances in this test (`tests/Parchwell.Verifier.Tests/Support/KafkaSessionTestSupport.cs` lines 149-185).
5. The test store copies the chunk with `content.CopyToAsync(buffer, cancellationToken)` (`tests/Parchwell.Verifier.Tests/Host/B10SweepHandlerTests.cs` lines 557-564). When the real 100 ms expires first, the copy throws, `Seal` records gap `upload_failed`, and `capture.json` is written with `complete` false. A failure of the later `capture.json` write would instead leave no `capture.json` and fail the count assertion at line 46, not line 54.

Reproduction, run in this planning session on 2026-11-24 in a scratch worktree at the base commit, Release build, one run per setting, with a temporary real-time `Thread.Sleep` added before the store's copy:

| Store delay | Without fix | With prototype fix |
|---|---|---|
| none | 2 of 2 cases pass | not run |
| 0 ms | 2 of 2 pass | not run |
| 50 ms | 2 of 2 pass | not run |
| 150 ms | 2 of 2 fail, at line 54 | 2 of 2 pass |
| 1,500 ms | not run | 2 of 2 pass |

The prototype passed the capture's deadlines the clock that `Drain` receives. Conclusion: the cause is that the capture's Blob deadlines use the system clock while the test's handler uses a manual clock with a 100 ms Blob budget, so any real delay of 100 ms or more inside a chunk upload fails the capture. This is established by source and reproduction. Which gap code the CI run recorded is not observable from its log; `upload_failed` is inferred, because it is the only gap path in `Seal` that depends on elapsed time. What delayed the CI run by 100 ms or more is not known; CPU contention from Stryker's parallel test workers is a hypothesis, not a finding.

The fix is in the product, at the cause, not in the test: the capture's deadlines use the clock its caller already uses for every other deadline. Production behaviour does not change, because the verifier host registers `TimeProvider.System` (`src/Parchwell.Verifier/Program.cs` line 104). The assertion at line 54 and the test stay unchanged.

### Companion download measurements

`c5_companion_artifacts.py` downloads the 146 companion archives in sorted name order under one transport deadline, `DEADLINE = 240` (line 27). Durations were taken by script from the aggregate job logs, from the start of the download step to the line `Verified 146 companion evidence archives.`, on 2026-11-24. Every run used the same transport constants as the base commit.

| Coordinator run | Commit | Download | Campaign | Campaign before the download step |
|---|---|---|---|---|
| [45028998990](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45028998990/job/137241580092) | `9c7fd04e` | 142.5 s | 3,208 s | 3,025 s |
| [45270881762](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45270881762/job/138057244045) | `e62bdc9b` | 137.5 s | 3,737 s | 3,551 s |
| [45374369955](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45374369955/job/138401169628) | `4ea8fae3` | 92.7 s | 2,889 s | 2,751 s |
| [45449734959](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45449734959/job/138672406117) | `4ea8fae3` | failed after 240.3 s | 3,063 s | 2,780 s |
| [45459740602](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45459740602/job/138709053522) | `4ea8fae3` | 167.9 s | 3,160 s | 2,946 s |

In the failed run, 143 of 146 archives had finished when the 144th request (`workload-p1`, index 143 in sorted order) timed out once and the deadline ended the step. At the same rate the 146 archives would have taken about 245.3 s; that is an estimate from the run's own rate, dated 2026-11-24. The four successful downloads took between 92.7 s and 167.9 s.

New value: `DEADLINE = 600`. Basis: it is 3.57 times the longest successful download and 2.4 times the estimate for the failed one. The deadline only ends a download that is still running; a download that finishes earlier takes the same time as before.

Effect on the 3,000-second campaign limit: none for a download that finishes within 240 s. A download that takes between 240 and 600 s now completes, and up to 360 more seconds count against the limit. The terminal checker then reports the campaign as `met` or `missed` with complete evidence, instead of the aggregate reporting it incomplete. On the current layout, once every step except the download is counted, run 45374369955 left 204 s of the limit for its download, and run 45459740602 left 8 s. So the failed run 45449734959 would very likely have missed 3,000 seconds even with the new deadline: it reached the download step at 2,780 s and needed about 245 s more for downloads alone. This change removes a failure mode; it does not create campaign time.

### Material uncertainty for the next pair

Of the three campaigns on the current layout, one finished within 3,000 seconds (2,889 s); the other two took 3,063 s and 3,160 s. This change set does not change shard timing. The paired limit, not these two causes, may decide the next acceptance. That is outside this plan's scope and decided nothing here.

## Scope

Files allowed to change:

- `src/Parchwell.Verifier/VerifyRun/Window/ConsumedMetadataCapture.cs`
- `src/Parchwell.Verifier/VerifyRun/Window/WindowConsumer.cs`
- `tests/Parchwell.Verifier.Tests/Window/ConsumedMetadataCaptureTests.cs`
- `tests/Parchwell.Verifier.Tests/Window/WindowConsumerTests.cs`
- `tests/Parchwell.Verifier.Tests/Mutation/c5_companion_artifacts.py`
- `tests/Parchwell.Verifier.Tests/Mutation/test_c5_companion_artifacts.py`
- `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`, changed only by the `renew` command (requirement 7)
- `docs/operations/verifier-mutation.md`

Paths that must not change: the 31 C5 target sources listed under `targets` in `c5-runtime-qualification.json`, which include the capture's callers `VerifyRun/B10SweepHandler.cs`, `VerifyRun/B10Final/B10FinalSweepHandler.cs` and `VerifyRun/VerifyRunHandler.cs`; the other caller `src/Parchwell.Verifier/VerifyRun/Abrupt/B10AbruptVerifyHandler.cs`, which is not a target; `tests/Parchwell.Verifier.Tests/Host/B10SweepHandlerTests.cs`; every nonrenewable C5 input in `NON_RENEWABLE` at `tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py` lines 337-338 (`c5_workflow.py`, both C5 mutation workflows, `c5_campaign.py`, `c5_projection.py`, `stryker-c5-config.json`, `.config/dotnet-tools.json`); everything under `.github/`, `docs/operations/workflows/`, `scenarios/`, `results/` and `infra/`.

Size estimate, 2026-11-24, based on the prototype diffs made in this session (capture and consumer about 15 lines, the renewed qualification 89 lines for two renewed paths) and the test map below: 8 files and about 300 changed lines. Size is reported, not enforced.

## External contracts

- **Capture deadlines.** When `WindowConsumer.Drain` is given a `TimeProvider` and a capture, the capture's chunk-upload deadline and its `capture.json` write deadline are each `BlobIoTimeout` measured on that `TimeProvider`. A capture used without a `TimeProvider` keeps the system clock. The `capture.json` wire format, gap codes (`upload_failed`, `write_failed`, `byte_limit`, `row_limit`, `interrupted`, `source_window_incomplete`) and the `complete` rule `sourceComplete && gaps.Count == 0 && observed == captured` are unchanged. How the clock reaches the capture (an optional parameter, a field set at start, or another shape) is the implementer's choice, provided the existing direct callers in `ConsumedMetadataCaptureTests.cs` compile and keep the system clock.
- **Download deadline.** `DEADLINE = 600` in `c5_companion_artifacts.py`. `SPACING = 0.2`, `TIMEOUT = 15` and `ATTEMPTS = 4`, the request, retry, catalogue and verification behaviour, the CLI and its messages are unchanged. The aggregate workflow and its command line are unchanged.
- **Qualification.** `c5-runtime-qualification.json` keeps schema version 2, every target record, measured map, timing evidence entry and estimate. Only the top-level `shared_inputs` hashes of the changed shared inputs and their per-context v2 assessments change, as `renew` writes them.

## Requirements

1. WHEN `WindowConsumer.Drain` drains a window with a capture and its `TimeProvider` does not advance, THEN a store write that takes longer than `BlobIoTimeout` in real time does not cancel the capture's chunk upload or `capture.json` write, and the capture is written with `complete` true.
2. WHEN the `TimeProvider` given to the capture advances by `BlobIoTimeout` or more during a chunk upload, THEN the upload's token is cancelled and a store that honours it leaves gap `upload_failed` at the chunk's first ordinal, as today on the system clock.
3. WHEN the `TimeProvider` given to the capture advances by `BlobIoTimeout` or more during the `capture.json` write, THEN a store that honours the token writes no `capture.json`, as today.
4. WHEN a capture is used without a `TimeProvider`, THEN its deadlines run on the system clock, so a store write that takes longer than `BlobIoTimeout` in real time leaves gap `upload_failed`.
5. WHEN the companion downloader starts, THEN its transport deadline is 600 seconds after construction; a request that would start at or after it raises `transport deadline exceeded` without opening a connection.
6. WHEN an existing test's fixture is rejected by a changed check, THEN the implementer does exactly one of: (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce. This rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it, and the cold review and Codex review check each listed change. Two such changes are already known: `test_error_body_is_not_read_at_the_deadline` and `test_error_body_stops_between_fragments_at_the_deadline` in `test_c5_companion_artifacts.py` set `clock[0] = 240` (lines 179 and 197) to reach the transport deadline; under (a) each becomes `clock[0] = 600`. With `DEADLINE = 600` and no fixture change, they fail and error respectively (observed in this session on the prototype).
7. WHEN the product, test and documentation edits are committed, THEN the implementer runs `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py renew --head <that commit> --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json --basis "<basis>"` with this basis text: `Issue 494 decision 7142132171 point 2 accepts the re-qualification cost of the C5 failure-cause repair (plan items/494/plan-c5-failure-causes.md). The change makes the consumed-metadata capture's Blob deadlines use the caller's clock and adds capture deadline tests; it adds no mutation target or selector. Its effect on mutation runtime is not measured.` It commits the result, and `check --base 4ea8fae3d52111e37028c39033430bc0a3c40776 --head <new commit>` returns `qualified`. WHEN `renew` refuses or `check` rejects, THEN the implementer stops and hands off; it does not edit the qualification by hand.
8. WHEN the documentation is updated, THEN `docs/operations/verifier-mutation.md` line 42 says `A 600-second transport deadline limits requests and reads.` with a link to this plan, and one short paragraph after the timing-margin paragraph records this repair: the cause, the renewed paths and that their runtime effect is not measured.
9. WHEN the pull request has merged, THEN the control session dispatches one pair on the merge's `main` commit under `docs/operations/verifier-mutation.md` steps 1-3 and runs the terminal check. This is the mutation acceptance that docs/agents/factory.md "Evidence pack" requires for a change to mutation inputs.

## Invariants and safety rules

- No test assertion is loosened, deleted or skipped. `B10SweepHandlerTests.cs` is not edited.
- No C5 target source, selector, cut, matrix, nonrenewable input, threshold, score floor or the 3,000-second limit changes.
- No new project, assembly reference, package or tool.
- No workflow trigger or workflow file changes. Dispatches run only on `main` (AGENTS.md hard safety rule 8).
- No Azure resource is touched.

## Test map

All new C# tests are in `ConsumedMetadataCaptureTests.cs` or `WindowConsumerTests.cs`, using `ManualKafkaTimeProvider` unless stated. Each uses a window and records the existing tests already build, so that only the clock decides the outcome.

| Requirement | Test | Inputs | Expected |
|---|---|---|---|
| 1 | `WindowConsumerTests.Given_StoreSlowerThanBlobTimeoutInRealTime_When_DrainClockStill_Then_CaptureIsComplete` | `BlobIoTimeout` 10 ms; one in-window record; the store sleeps 60 ms real time, then calls `ThrowIfCancellationRequested()`, then copies, in both `CreateStreamAsync` and `CreateAsync`; the drain clock is not advanced | `capture.json` present; `complete` true; `observed_rows` 1; `captured_rows` 1; `gaps` empty; one chunk |
| 2 | `ConsumedMetadataCaptureTests.Given_CaptureClockAdvancesDuringChunkUpload_When_Sealing_Then_TimeoutBoundaryApplies` (theory) | `BlobIoTimeout` 100 ms; one record observed and finished with the manual clock; the store advances that clock by N ms inside `CreateStreamAsync`, then calls `ThrowIfCancellationRequested()` | N = 99: `complete` true, `gaps` empty, `captured_rows` 1. N = 100 and N = 101: `complete` false, `gaps` `[{"code":"upload_failed","ordinal":0}]`, `observed_rows` 1, `captured_rows` 0, `first_missing_ordinal` 0 |
| 2 | `ConsumedMetadataCaptureTests.Given_CaptureClockAdvancesDuringRolloverUpload_When_Observing_Then_TimeoutBoundaryApplies` (theory) | `BlobIoTimeout` 100 ms; the record and the `MaxRowBytes = MaxChunkBytes = size`, `MaxBytes = size * 2` settings of the existing `ConsumedMetadata_ChunkRotationAndAggregateLimit`; two `Observe` calls and `Finish(true)` with the manual clock; the store advances that clock by N ms inside the first `CreateStreamAsync` call only, then calls `ThrowIfCancellationRequested()` | N = 99: `complete` true, two chunks, `captured_rows` 2. N = 100 and N = 101: `complete` false, `gaps` `[{"code":"upload_failed","ordinal":0}]`, `observed_rows` 2, `captured_rows` 0, `first_missing_ordinal` 0 |
| 3 | `ConsumedMetadataCaptureTests.Given_CaptureClockAdvancesDuringManifestWrite_When_Finishing_Then_TimeoutBoundaryApplies` (theory) | `BlobIoTimeout` 100 ms; no record; `Finish(true)` with the manual clock; the store advances that clock by N ms inside `CreateAsync` for the name ending `capture.json`, then calls `ThrowIfCancellationRequested()` | N = 99: one `capture.json`, `complete` true. N = 100 and N = 101: no `capture.json` |
| 4 | `ConsumedMetadataCaptureTests.Given_NoClock_When_StoreSlowerThanBlobTimeout_Then_UploadFails` | `BlobIoTimeout` 10 ms; one record; `Observe` and `Finish(true)` without a clock; in `CreateStreamAsync` the store calls `cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(30))`, then `ThrowIfCancellationRequested()` | `complete` false; `gaps[0].code` `upload_failed`; `captured_rows` 0 |
| 5 | `test_c5_companion_artifacts.TransportTests.test_transport_deadline_is_600_seconds_from_construction` | `time.monotonic` returns 0 at construction | `transport.deadline` 600. At clock 590 a request opens with `timeout` 10. At clock 600 a request raises `DownloadError` matching `deadline exceeded` and `opener.open` is not called |
| 6 | `test_error_body_is_not_read_at_the_deadline`, `test_error_body_stops_between_fragments_at_the_deadline` | fixture `clock[0] = 600` instead of 240 | existing assertions unchanged and passing |
| 7 | `.NET verification` hosted check, which runs `c5_runtime.py check` against the base | the renewed qualification | `qualified` |

Tests 1 and 4 together show the clock source: a store slower than `BlobIoTimeout` in real time passes with the drain's still clock and fails on the system clock. Test 4's store waits for the token instead of sleeping a fixed time, so its outcome does not depend on how quickly the system timer fires under load. The only added real wall time in new tests is test 1's 60 ms sleep and test 4's wait, which ends when the 10 ms system-clock deadline cancels the token.

Controlled pre-fix comparison, recorded in the pull request and not committed: on the base commit and on the head, add the temporary 150 ms real-time sleep before the copy in `B10SweepHandlerTests` `PhaseFixture.CreateStreamAsync` and run `ImmutableResultsReceiveFinalCountsAfterCleanup`. Expected: base 2 of 2 fail at line 54; head 2 of 2 pass. Discard the temporary edit. Also run test 1 on the base with the new test file: expected `complete` false with `upload_failed`.

## Acceptance commands

1. `python3 -m unittest test_c5_companion_artifacts` from `tests/Parchwell.Verifier.Tests/Mutation`: all pass.
2. `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base 4ea8fae3d52111e37028c39033430bc0a3c40776 --head HEAD --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` on a full-history clone: `status` `qualified`, and `assessed_inputs` includes the changed shared inputs. A shallow clone returns `invalid_input` (observed in this session); unshallow first.
3. The verification gates of docs/agents/pull-requests.md "Verification gates", with hosted CI covering what it covers under [decision 6909056894](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6909056894). `npm run check:docs` before the cold review.
4. After merge, requirement 9: the pair's `campaign.json` and `summary.json`, and `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_campaign.py terminal --repository collaborationwithothers/eventhubs-kafka-dotnet --commit <main-sha> --manifest campaign.json --summary summary.json --output performance.json`. Closure needs both workflows successful, 324 physical receipts, a verified score of at least 60% and `status` `met`.

## Deployment assumptions

None accepted yet. The companion download time assumption is under "Open questions for Hari" until he accepts its risk by name; a non-design revision then moves it back here with a link to that decision.

No assumption about a deployed container, image, chart or Azure runtime is made: nothing here is deployed.

## Plan-check questions

1. **Work that scales with a number unknown at planning time.** `renew` writes one assessment per changed shared input per timing context; the qualification has 7 contexts and the change has at most 4 renewable paths, so at most 28 entries, written by script. The post-merge acceptance is one pair of 324 physical jobs and one download of 146 archives bounded by 600 s. Review rounds follow docs/agents/pull-requests.md rule 8. No other step scales.
2. **Where this is stricter than the parent plan, and why.** The parent is the approved observability-evidence specification R3 ([plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/f96eab914d00c06484431ff67a1808fb331c7e13/items/494/plan-observability-evidence.md)), which introduced the consumed-metadata capture. It did not pin which clock the capture's deadlines use. This plan pins it to the caller's clock, to remove the nondeterminism diagnosed above. Production behaviour is unchanged.
3. **Checks that depend on code, data or behaviour outside `main` and the pull request.** The hosted download times and the post-merge pair's outputs. The download times were checked against the recorded aggregate logs linked above; they are listed under "Open questions for Hari" as question 1. The pair's outputs do not exist yet; requirement 9 and the stop rule say what happens on a miss.

## Stop rule and caps

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope, including any path outside the allowed list, or when `renew` refuses or `check` rejects.

Post-merge execution: one pair. Exhaustion action: `record the gap and continue` for independent work, and the control session reports the outcome on this issue. A failed or incomplete pair does not authorize a second dispatch under this plan.

## Deferred work

1. **Post-merge mutation acceptance.** Evidence: docs/agents/factory.md "Evidence pack" requires a passing run on `main` after a change to mutation inputs merges. On the verdict path for the issue's closure. Deferral is required, because the run must measure the merged commit. Responsible: the control session. Prerequisite: Hari's merge. Bound: one pair. Exhaustion: record the gap and report. Completion evidence: the terminal report with `status` `met`. Gate it blocks: the closure of this issue. The third pair on `4ea8fae3`, still running when this plan was written ([companion 45469594281](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45469594281), [coordinator 45469622107](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45469622107)), continues under decision 7142132171 point 3 and is not this item.

## Sources

- [Decision 7142132171](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7142132171), [decision 7139344144](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7139344144), [decision 7123218757](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7123218757), [decision 6909056894](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6909056894).
- [Pair result 7140680669](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7140680669), [pair result 7142033635](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7142033635), [issue 593](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/593).
- .NET behaviour, verified with the Microsoft Learn documentation tool on 2026-11-24: the `CancellationTokenSource(TimeSpan, TimeProvider)` constructor takes "The TimeProvider with which to interpret the delay", and the `CancellationTokenSource(TimeSpan)` countdown "starts during the call to the constructor" ([CancellationTokenSource constructors, .NET 10](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtokensource.-ctor?view=net-10.0)). That the parameterless-provider overload uses the system clock is inferred from that page, which names no provider for it. No Kafka, librdkafka, Confluent.Kafka or Event Hubs behaviour is relied on.
- Renewal procedure: `docs/operations/verifier-mutation.md` lines 84-86 and 94 at the base commit; `input_maps` at `c5_runtime.py` lines 487-529, which shows `c5_companion_artifacts.py` is not a qualification input. A prototype commit in this session confirmed `check` returns `not_applicable` for the deadline change alone and `qualified` after `renew` for the capture change.

## Open questions for Hari

1. **Accept the companion download time risk by name.** Deployment assumption: the 146 companion archives download within 600 seconds on a standard GitHub-hosted runner. Evidence: the five aggregate jobs in "Companion download measurements", each on a fresh runner with a fresh HTTPS connection per request, under GitHub's ordinary shared load, which no run controlled. Four finished in 92.7 to 167.9 s; one stopped at 240 s with 143 archives done. No offline test can cover hosted throughput; test 5 covers the value. Decision 7142132171 point 1 sets the value from these measurements but does not accept the risk of a slower download. Consequence if the risk occurs: the aggregate reports incomplete evidence, as it does today at 240 s, and the one post-merge pair does not close the issue. Recommendation: accept it. The alternative is a larger value, which only lengthens a campaign that would already miss 3,000 seconds.

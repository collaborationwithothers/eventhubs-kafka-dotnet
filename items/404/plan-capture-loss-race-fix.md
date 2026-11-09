# Capture-loss race fix contract-grade plan

Revision 2. Issue #404. Implementing tool: Claude Code, under [standing decision 6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614) point 3, which replaces Codex for this pull request. The state file on `main` (`items/404/state.md`, "Implementing tool") still names Codex; the implementing session updates it. Base: `main` at `f5cc496fa3cf845e68e400b5b65e557f121dd928`.

A statistics callback that starts after the statistics session closes capture, and before completion seals its result, makes the completion incomplete on `main`. Such a report was never going to be written to evidence, so the failure hides nothing; it only fails a good run. This pull request makes the session ignore a callback that starts after capture closes, and keeps every outcome that marks a real capture gap. It changes no diagnostic code, public type, wire member, timeout, gate or failure name, and it authorizes no trial.

Changes since Revision 1 (commit `d1a977ef`), from two control-session messages received after that push:

- Requirements, contracts and tests state observable behaviour only. No private member, field or line number is pinned. Section 2 still cites `main`'s private code as evidence of the race.
- Every test now builds its session through the factory production uses: T2 through `AddParchwellKafkaStatistics` and the service provider; T1 and T3 through `KafkaStatisticsBridgeFactory.Create` with a test clock.
- The way a test observes that capture has closed is now a permitted example, not a requirement. Its one constraint is stated in section 9.
- New finding: on `main`, a callback after capture closes can also lose the gate to the close itself and record `capture_contention` (observed 2 of 20 runs). The fix removes that case too. Section 2 says how the fix relates to the session's lock try, and Q2 lists fixing contention while capture is open as a separate option with its cost estimate.
- The prototype closes admission before the capture-open state changes, so a test observing that state cannot run ahead of the close. All observations were rerun on the revised tests.

## 1. Goal and authority

Goal: the minimum change to `src/Parchwell.Kafka` `KafkaStatisticsSession` that stops a callback starting after capture closes from making completion incomplete, without hiding a real capture gap.

Authority, in creation order:

- Issue body of #404, and the decisions listed in `items/404/state.md` "Decisions in force", up to the [burst test-gap approval 6746177383](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6746177383). None of them concerns `KafkaStatisticsSession`.
- [Decision 6749565231](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749565231): the successor fix completes statistics while the producer is live; plans carry a never-ran inventory; a further trial needs Hari's new explicit approval and gate approval.
- Decisions 6749995101, 6750021224, 6750864341, 6750879393, 6750891952, 6750940329, 6751199288, 6751449502 and 6751703425 govern the verifier, PR3a and the successor fix. They do not change this plan. Decision 6751449502 is relevant only through E2 below: the successor fix pins one guard flag per session diagnostic code.
- [Decision 6752059755](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752059755): fix "the capture-loss race between `CloseCapture` and the completion seal" rather than accept it, in its own pull request, with its own contract-grade plan, fresh reproduction check, plan check and Hari's approval. "It must merge before any further B10 trial." The plan "states the effect on every scenario that uses `KafkaStatisticsSession`". The successor fix "does not change `src/Parchwell.Kafka`".
- [Decision 6752078378](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752078378): the `B10AbruptStatistics.CaptureNative` contention race is fixed in a separate pull request. This plan does not touch it.
- [Standing decision 6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614), for the four pull requests including this one: standing rule 1 lets the control session decide some open questions on its own recommendation; point 2 standing plan approval requires "Open questions for Hari" to state `None`; point 3 makes Claude Code the implementing tool; point 4 lets the control session merge a pull request that touches no protected path under GOVERNANCE "Merge rule" in full.
- [Order decision 6752454732](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752454732): the capture-loss race fix "runs alongside the sequence on its own branch from `main`. If its plan shows a shared file with any of the three, it joins the sequence after the successor fix." See 4.3 and Q5.
- [Decision 6752554218](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752554218): review re-request after 15 minutes, and stacked implementation of the next pull request in the order.
- [Decision 6753263792](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6753263792), standing rule 1, not seen by Hari: for the contention race fix, the session-side contention in `KafkaStatisticsSession.Capture` (lines 106-111) is accepted as a deployment assumption for the next trial; "Whether to **fix** it is a scope change and is left for Hari." This plan does not fix it (Q2).
- [Decision 6753364128](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6753364128), standing rule 1, not seen by Hari: the successor fix's D6 (the two capture races) is accepted because each is "fixed by its own pull request ... before any trial".

All decisions above were read in full in this planning session on 2026-11-09. The newest comment read is 6753364128 (created 2026-11-09T22:38:22Z); the issue body `updated_at` was 2026-11-09T22:38:22Z.

## 2. The race on `main`, verified against the code

Line numbers are `src/Parchwell.Kafka/Statistics/KafkaStatisticsSession.cs` on `main` unless another file is named.

- `CompleteCoreAsync` waits for post-drain coverage (lines 218-247), then calls `CloseCapture` (line 249). `CloseCapture` sets `_captureOpen = false` and completes the queue under the gate (lines 601-608).
- It then waits for the worker to drain accepted reports (lines 250-265) and calls `BuildCompletion` (line 273). `BuildCompletion` copies the diagnostic codes (line 621) and seals the completion state (line 626).
- A callback in that window passes `TryBeginCapture` (line 95), because only the seal sets `Finalized` (lines 935-956). It takes the gate (line 106), finds `!_captureOpen` (line 114), records capture loss (line 116) and adds `late_capture` (line 117).
- `BuildCompletion` then sees `CaptureLost` and appends `capture_loss` (lines 630-633). Any diagnostic makes the result incomplete (lines 650-655).
- A callback that starts after the seal is refused by `TryBeginCapture` (line 95). Its `late_capture` (line 97) is added after the copy at line 621, so it never reaches a returned result. The window between `CloseCapture` and the seal is therefore the only place where a callback after capture closes changes the result.
- The report of such a callback is never queued (line 127 is not reached), on `main` or after this fix. The evidence content is the same either way; only the completion status differs.

`capture_contention` (lines 106-111): a callback that cannot take the gate records loss. Before capture closes, the gate holders are completion start (lines 185-204), the coverage reads (lines 221-241) and the worker (lines 354-438). A callback refused there drops a report delivered while capture is open, which the bridge contract counts as a gap (docs/operations/statistics-bridge.md line 42: "Contention ... leave[s] persistent evidence-quality diagnostics. Later good reports do not erase a gap.").

How the fix relates to that lock try: it does not change the lock try. It adds a check before it, at admission. A callback that starts after capture closes returns there and never tries the gate, so it can no longer record contention against the close, the worker drain or the seal. A callback admitted before capture closes reaches the lock try exactly as on `main`. Contention while capture is open is therefore unchanged. Decision 6753263792 accepted it for the next trial and left fixing it to Hari (Q2).

Reproduction on `main` with the real session (section 9, T1 and T2, observed in this planning session):

- T1 holds the session's worker inside a sink write of a report accepted before capture closed, waits until capture is closed, delivers one callback, then releases the worker. On `main` both cases return `IsComplete` false.
- T2 does the same with a real librdkafka producer feeding `Capture` from its own statistics thread, on a session built by the production service registration. On `main` it returns `IsComplete` false.
- 20 repeated runs of the test class on `main`: 3 failed and 1 passed (T3, which pins unchanged behaviour) every time. The diagnostics, counted by script over the 20 runs:
  - T1 `capture-only`: `capture_loss,late_capture` 20 times.
  - T1 `inventory`: `capture_loss,late_capture` 18 times; `capture_contention,capture_loss` 2 times.
  - T2: `capture_loss,late_capture` 20 times.
- The `capture_contention` case is a callback after capture closes that tried the gate while the close still held it (inferred from the code: the prototype's test saw capture as closed at the point the close sets its state inside the gate, lines 603-607). It is the same race through the lock try at lines 106-111.

## 3. Effect on every caller and scenario

Production callers on `main` (all others under `src/` have none):

| Caller | `Capture` | `CompleteAsync` | Native client live at completion? | Race reachable on `main`? |
|---|---|---|---|---|
| `RunExecutor` through `LoadGenWorker` | `src/Parchwell.LoadGen/Program.cs` line 411 | Capture-only overload, `RunExecutor.cs` line 368. `RunSessionFactory.cs` line 91 passes `[]` as the expected brokers, so the overload at line 388 is not reached in production | Yes: disposal is at `RunExecutor.cs` line 419, after completion | Yes |
| `B10AbruptHost` successor | `B10AbruptHost.cs` line 232 | Expected-broker overload, line 247, called only from `B10AbruptProducer.RunSuccessorAsync` lines 489-490 | No on `main`: `CleanupAsync` disposes the producer first (line 488; lines 626-632). Yes after the successor fix (`items/404/plan-successor-fix.md` R1) | No on `main`; yes after the successor fix |

Scenarios, from `infra/scripts/phase/phase.py` lines 20-22 (`SCENARIOS`) and line 46 (`LEDGER_ITEMS`), and the chart's mode routing (`charts/parchwell/templates/jobs.yaml` lines 1-9 and 77-79):

- A01, B04, B06, B07 and B10's ordinary phase run `RunExecutor`. For them, a run in which a callback lands in the window now completes; on `main` it failed with `statistics_completion_incomplete` (`RunExecutor.cs` lines 370-374), worker exit 1 (line 529) and verifier check 19 failing on `statistics_completion.is_complete` (`src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` line 38). Every other run is unchanged.
- B10's abrupt diagnostic (`b10_abrupt_diagnostic`) runs `B10AbruptHost`. Only its successor completes statistics. The fix matters there once the successor fix merges.
- B01, B03, B05, C08, C12, F04 (single action), B08, E01, F06 (single action v2) and A02, A03, A04 (idle observation) create no `KafkaStatisticsSession`. No effect.

Published results (read only; this pull request changes nothing under `results/`):

- From the `RunExecutor` path: `results/A01/2026-10-25.*`, `results/B04/2026-10-26.*`, `results/B06/2026-11-04.*` and `results/B07/2026-11-04.*`.
- A `grep -rlE` search of all 49 files under `results/` for `statistics_completion`, `is_complete`, `capture_loss`, `late_capture`, `capture_contention` and `statistics_completion_incomplete` found zero matches.
- No published result depended on the old behaviour. Inferred, not verified per run: an incomplete completion fails the worker (`RunExecutor.cs` lines 370-374 and 529), and publication requires a complete worker (`src/Parchwell.Verifier/VerifyRun/Publication/PublicationAcceptance.cs` line 57). A published run therefore had a complete completion, and the fix returns the same result for it. A01's text states "complete attributed statistics coverage" (`results/A01/2026-10-25.md` line 3).
- Not checked: whether an earlier, unpublished failed run was caused by this race. A published result shows the final run only (AGENTS.md GOVERNANCE "Verdict integrity"), so such runs are not in `results/`.

## 4. Scope

### 4.1 Files allowed to change

1. `src/Parchwell.Kafka/Statistics/KafkaStatisticsSession.cs`: the behaviour of E1 and R1 to R6, and the `Capture` XML remarks (E3). Code structure, private and internal names are the implementer's choice; internal types may be added in this file.
2. New file `tests/Parchwell.Kafka.Tests/Statistics/KafkaStatisticsLateCaptureTests.cs`: T1 to T3.
3. `docs/operations/statistics-bridge.md`: line 87 only, as E4 pins. `docs/operations/` is not a protected path: `.github/CODEOWNERS` lists `/docs/operations/` with no owner after `/docs/`.
4. `items/404/state.md` (factory.md "Item state file") and the new receipt `items/404/capture-loss-race-fix-receipt.md`.

This pull request touches no protected path.

### 4.2 Paths and behaviour that must not change

- Every file not in 4.1, in particular every other file under `src/Parchwell.Kafka/**`, all of `src/Parchwell.LoadGen/**` (including `B10AbruptStatistics.cs`), `src/Parchwell.Verifier/**`, `infra/**`, `charts/**`, `scenarios/**`, `results/**`, `.github/**`, `docs/blueprint.md`, every other `docs/**` file and line, `AGENTS.md`, `CLAUDE.md`, `*.csproj`, `Directory.*.props`, `*.sln*`.
- Every existing test file. No existing assertion is changed, loosened, skipped or deleted (docs/engineering/dotnet.md line 60).
- No new project, assembly reference or NuGet package. T2 uses `Confluent.Kafka`, which the test project already uses (for example `tests/Parchwell.Kafka.Tests/Authentication/KafkaOAuthBearerNativeClientTests.cs`).
- No public member is added, removed or changed in signature.
- `KafkaStatisticsOptions` values, `CompletionTimeout`, and every caller's timeouts and statistics intervals stay as they are.

### 4.3 Files other `items/404` plans also change

Read on `claude/404-plan` at `ac5a29d98c59e80025eba89b43f52f27f7d9f583`: successor fix Revision 3, capture contention race fix Revision 1 (`items/404/plan-capture-contention-race-fix.md`, added in `c30bb3a`), PR3a Revision 6, verifier Revision 1.

- `items/404/state.md` is the only file another plan also changes: the successor fix (its 2.1 item 9), the contention race fix (its 3.1 item 3) and PR3a (its 3.1). Under order decision 6752454732 a shared file puts this pull request after the successor fix; whether the state file counts is Q5.
- No source, test or documentation file is shared. The successor fix and the contention race fix both forbid `src/Parchwell.Kafka/**` (successor 2.2; contention 3.2). PR3a and the verifier plan name no `src/Parchwell.Kafka` file and not `statistics-bridge.md`.
- Semantic dependencies:
  - The successor fix's E2 (Revision 3) pins guard flags for 31 session diagnostic codes, including `late_capture`, `capture_contention` and `capture_loss`. This plan adds and removes no code and keeps all three reachable (E2, R2 to R4), so that list stays whole. Its T1 delivers report P3 during disposal, after the seal; that path is unchanged here (R5).
  - The contention race fix's I1 relies on the production handler calling `CaptureNative` and then `KafkaStatisticsSession.Capture` on the callback thread. This plan changes neither the handler nor that call.

## 5. External contracts

E1. `KafkaStatisticsSession.Capture(string)` behaviour, public:
- A call that starts after completion has closed capture is ignored: it is not queued, adds no diagnostic, records no capture loss and does not change the completion result.
- Capture closes at one atomic point in `CompleteAsync` (either overload): after its post-drain wait ends, or at once when the clock is unverified or the session was disposed, and before it drains accepted work. A call "starts after" that point when the session's admission of the call happens after it. Calls admitted before that point keep `main`'s behaviour exactly (R2 to R4).

E2. Diagnostic codes: unchanged. No code is added or removed. `late_capture`, `capture_contention` and `capture_loss` keep their names and remain reachable (R2 to R5).

E3. XML remarks of `Capture`, replacing the second sentence of the existing remarks (line 90 on `main`, "A call after the completion freeze is diagnosed as a late capture."). The new remarks are exactly:

```
/// <remarks>
/// Reports beyond the configured UTF-16 length or queue capacity are rejected and latch
/// incomplete evidence. A call that starts after completion closes capture is ignored: it is
/// not queued and does not change the completion result. A call that started before capture
/// closed and is refused because capture has closed records capture loss.
/// </remarks>
```

E4. docs/operations/statistics-bridge.md line 87. The existing line is kept byte-identical, and this text is appended to it after one space, on the same line:

`Completion closes capture before it drains accepted work: when its post-drain wait ends, or at once when the clock is unverified or the session was disposed. A callback that starts after capture closes is outside the session's evidence. It is not queued, adds no diagnostic and does not make the result incomplete. A callback that started before capture closed and is refused because capture has closed records capture loss.`

The clause "or at once when the clock is unverified or the session was disposed" describes `main`'s code, which this plan keeps: the close (line 249) follows the coverage wait only when the clock is native-compatible and the session was not disposed (line 219).

E5. Unchanged: `KafkaStatisticsCompletion` and its members, `capabilities.json` and every evidence file, every LoadGen and verifier diagnostic and failure name, the guard facts of the successor fix.

## 6. Requirements

R1. Callback after capture closes.
- WHEN `Capture` is called after completion has closed capture (E1), before or after the completion result is sealed,
- THEN the report is not written to the sink, no diagnostic is added, capture loss is not recorded,
- AND a completion whose coverage was reached and whose accepted reports drained returns `IsComplete` true with empty `Diagnostics` and empty `MissingBrokers`, in both overloads.

R2. Callback admitted before close, refused at the gate after close.
- WHEN a call was admitted before capture closed and reaches the gate after capture closed,
- THEN it records capture loss and `late_capture` as on `main`, and the completion is incomplete with diagnostics exactly `capture_loss`, `late_capture`.

R3. Callback still in flight at the seal.
- WHEN a call admitted before capture closed has not finished at the seal,
- THEN the returned completion includes `capture_loss` and is incomplete, as on `main`.

R4. Contention before close.
- WHEN a call admitted before capture closed cannot take the gate,
- THEN it records capture loss and `capture_contention` as on `main`.

R5. After the seal.
- WHEN `Capture` is called after the seal,
- THEN the returned completion is not revised (as on `main`), and no report is written.

R6. Disposal.
- WHEN the session is disposed without completion, or during completion,
- THEN behaviour is as on `main`: `DisposeAsync` keeps its behaviour, and disposal is not a close in the sense of E1.

## 7. Invariants and safety

- I1. Capture closes at exactly one atomic point. A call is either admitted before it (and keeps `main`'s handling) or starts after it (and is ignored). No call is both.
- I2. The seal still counts every call admitted before capture closed and not yet finished as capture loss. Calls that start after capture closes are never counted as in flight.
- I3. The evidence written to the sink is identical to `main` for every interleaving: a call after capture closes was never queued on `main` either.
- I4. AGENTS.md hard safety rules hold: no Azure credential, no workflow dispatch, no `.github/` change, no trial. Verdict integrity holds: nothing under `results/`, no expected outcome.
- I5. `Parchwell.Kafka` keeps its public surface. Seams stay at our own boundaries: the tests use the existing sink interface `IKafkaStatisticsEvidenceSink`, a `TimeProvider`, and the real Confluent.Kafka producer, not a wrapper around it.

## 8. Never-ran inventory

Evidence for "ran live" is the B10 abrupt diagnostic trial logs, as listed in `items/404/plan-successor-fix.md` section 6 at `ac5a29d9` (seven runs, with artifact IDs and zip hashes). This session downloaded trial 7, run 42679791413, artifact 13704155185, into an empty scratch directory with `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/13704155185/zip`. Its zip SHA-256 is `bee17231882ffeb7ddc3134ba10a00ec4590f20e96dad409c050decf1f22cdcf`, as pinned. Its 21 files contain zero matches for `late_capture`, `capture_loss` and `capture_contention`. That absence proves little: the guard line logs only two facts (successor log `b10-42679791413-1-c1-r1-loadgen2-prrgv.containers.loadgen.log`, SHA-256 `fce88054fccf70591da6f334b7299e446a7ec66cd7b6814fec8740cd17e7209f`, line 56). The "never" entries below rest on code order: in every trial, `CompleteAsync` ran only in the successor, after disposal (`B10AbruptProducer.cs` line 488), and the first six runs started no successor job (successor plan section 6).

| Stage on the changed path | Ran live? | Test in this plan that runs it for real |
|---|---|---|
| N1. Completion closing capture while the native client is live | Never in any trial (code order above). It runs in `RunExecutor` scenario runs, which are not trial evidence | T1 (real session), T2 (real session, real librdkafka producer, real threads) |
| N2. `Capture` that starts after capture closes, before the seal (changed) | Never in any trial | T1 both overloads; T2 with a callback from the real librdkafka statistics thread |
| N3. Completion seal and result after the new close point (changed) | Never | T1, T2, T3, and every existing completion test in `tests/Parchwell.Kafka.Tests/Statistics/` |
| N4. `Capture` admitted before close and refused at the gate after close (unchanged behaviour, changed neighbours) | Never | T3 |
| N5. Seal with a call admitted before close still in flight (unchanged) | Never | Existing `KafkaStatisticsBoundaryTests.Given_CallbackPausesBeforeAdmissionGate_When_CompletionSeals_Then_ResultIncludesCaptureLoss` (2 cases) and `KafkaStatisticsCaptureCompletionTests.Given_CallbackPausedBeforeGate_When_CompletionSeals_Then_ReturnedLossCannotBeRewritten` |
| N6. Contention before close (unchanged) | Never | Existing `KafkaStatisticsCaptureCompletionTests.Given_CaptureContendsWithCompletionGate_When_FreshReportFollows_Then_LossStaysLatched` |
| N7. B10 successor completion while live | Never; it is the successor fix's change | The successor fix's T1 and T9 (its plan), which run on this code once both merge |

No stage of this path needs a deployed container, image or chart. The deployed image runs the same `KafkaStatisticsSession` code; which thread or library build invokes `Capture` does not change E1.

## 9. Test map

All new tests are in `tests/Parchwell.Kafka.Tests/Statistics/KafkaStatisticsLateCaptureTests.cs`, trait `Category` `Unit`, named under docs/engineering/dotnet.md line 62.

Common inputs:
- Where each test observes: on a session built the way production builds one, never on a helper called directly.
  - T2 builds the session exactly as production registration does: `AddParchwellKafkaStatistics` (public, `KafkaStatisticsServiceCollectionExtensions.cs` lines 14-43 on `main`) with the test's sink registered as `IKafkaStatisticsEvidenceSink`, then `KafkaStatisticsBridgeFactory.Create` (public) from the service provider. Production code picks `TimeProvider.System` and decides clock compatibility (Linux), as in LoadGen (`B10AbruptHost.cs` line 226; `RunSessionFactory.cs` line 85).
  - T1 and T3 need a controlled clock, which the production registration treats as not native-compatible. They build the same factory class with the test clock and native compatibility asserted, through its internal constructor, and call the public `Create`. Every capture and completion call goes through the public `Capture` and `CompleteAsync`.
  - The sink is a test implementation of the public `IKafkaStatisticsEvidenceSink`. The production sink is in LoadGen, which `Parchwell.Kafka.Tests` does not reference.
- Options for T1 and T3: `MaxSnapshotCharacters` 16384, `QueueCapacity` 8, `MaxBrokerSeries` 4, `CompletionTimeout` 5 s (`StatisticsTestSupport.Options`). T2: the same, with `CompletionTimeout` 20 s. Identity: scenario `A01`, run `run-test`, role `LoadGenProducer`, client ID `test-client` (T2: `client`).
- Reports: `StatisticsTestSupport.Report("test-client", ts, ts - 9_000_000)`, one broker `broker/1`.
- Clock: `ManualTimeProvider(initialTimestamp: 9_000_000)` (frequency 1000000 per second, from `StatisticsTestSupport.cs`), advanced 1 s before `CompleteAsync`, so the drain boundary is 10000000 us; advanced 1 ms after.
- Holding sink: a test `IKafkaStatisticsEvidenceSink` that blocks its first and second qualifying writes until the test releases each, then records the report. In T2 a write qualifies only when the report's native `ts` is above a threshold armed after `CompleteAsync` returns; in T1 and T3 every write qualifies.
- Capture-closed wait. The test delivers its late callback only after it has observed that completion closed capture. The technique is the implementer's choice, with two constraints:
  - the observation must not run ahead of the E1 close point, or a callback "after close" could still be admitted before it;
  - if the technique cannot find what it observes, the test fails; it never passes vacuously.
  - Permitted example, used by the prototype: poll every 1 ms, under a 5 s watchdog, a reflection read of the session's private capture-open state, with the fix closing admission before that state changes. On `main` this is the only way found to observe the close without a new seam. Another permitted example is an internal observation hook; the test then cannot run on `main`, and mutation M1 is the evidence that it catches `main`'s behaviour.
- Watchdog: every wait is bounded at 5 s (20 s for T2's completion).

| Test | Covers | Input | Literal expected values |
|---|---|---|---|
| T1 `Given_CallbackAfterCaptureClosed_When_CompletionSeals_Then_CompletionStaysComplete`, cases `capture-only` and `inventory` | R1, E1, I3 | `capture-only`: `CompleteAsync(token)`; capture ts 10000001 (held write 1), capture ts 10000002 (queued before close), release 1; wait for held write 2; wait until capture closed; capture ts 10000003; release 2. `inventory`: `CompleteAsync(["broker/1"], token)`; first capture ts 9999999, second ts 10000001, otherwise the same | `string.Join(",", Diagnostics codes)` is `""`. `IsComplete` true. `MissingBrokers` empty. Sink wrote exactly ts `[10000001, 10000002]` (`capture-only`) or `[9999999, 10000001]` (`inventory`), in that order; ts 10000003 is not written. On `main`: `IsComplete` false, with diagnostics `capture_loss,late_capture` or `capture_contention,capture_loss` |
| T2 `Given_RealProducerCallbackAfterCaptureClosed_When_CompletionSeals_Then_CompletionStaysComplete` | R1, E1, N1, N2 | Session from the production registration (above). Real `ProducerBuilder<byte[], byte[]>`: `bootstrap.servers` `127.0.0.1:1` (nothing listening), `client.id` `client`, `statistics.interval.ms` 100 (test only), no-op log and error handlers. Statistics handler: increment `started`, call `Capture`, increment `ended`. Wait for one written report; `CompleteAsync(token)`; arm the threshold at the current monotonic time in microseconds; wait for held write 1; wait for one more ended callback (queued before close); release 1; wait for held write 2; wait until capture closed; read `started`; wait until a callback started after that read has ended; release 2 | `string.Join(",", Diagnostics codes)` is `""`. `IsComplete` true. On `main`: `IsComplete` false (observed `capture_loss,late_capture`). Requires Linux, where `TimeProvider.System` and librdkafka `ts` share `CLOCK_MONOTONIC` (docs/operations/statistics-bridge.md lines 81-83); CI runs on `ubuntu-24.04` (`.github/workflows/dotnet-verification.yml` line 15) |
| T3 `Given_CallbackAdmittedBeforeCaptureClosed_When_ItReachesGateAfterClose_Then_LossIsReported` | R2, I1 | Capture-only. A clock that pauses the next timestamp read, which `Capture` makes after admission and before the gate (on `main`, line 104; the implementer keeps a pause point between admission and the gate, or uses another permitted technique that holds an admitted call there). Capture ts 10000001 (held write 1); capture ts 10000002 (queued); pause next timestamp; start capture ts 10000003 on another thread and wait until it pauses (admitted before close); release 1; wait for held write 2; wait until capture closed; resume; wait for that capture to return; release 2 | `string.Join(",", Diagnostics codes)` is `"capture_loss,late_capture"`. `IsComplete` false. Sink wrote exactly ts `[10000001, 10000002]` |
| Existing, unchanged | R3, R4, R5 | As on `main` | `KafkaStatisticsBoundaryTests.Given_CallbackPausesBeforeAdmissionGate_When_CompletionSeals_Then_ResultIncludesCaptureLoss` (2 cases): diagnostics exactly `capture_loss`. `KafkaStatisticsCaptureCompletionTests.Given_CallbackPausedBeforeGate_When_CompletionSeals_Then_ReturnedLossCannotBeRewritten`: diagnostics exactly `capture_loss`, unrevised after a later capture. `...Given_CaptureContendsWithCompletionGate_When_FreshReportFollows_Then_LossStaysLatched`: contains `capture_contention` and `capture_loss` |

Boundaries: the change has one predicate, "admitted before capture closed". T1 and T2 test just after the close point; T3 and the in-flight tests test just before it; the existing completion tests test well before it. There is no numeric boundary.

Why T2 cannot be fully deterministic: the late callback comes from librdkafka's own timer. The test waits for it, so a missing callback fails by watchdog, not by a false pass. The test waits for a callback whose handler started after the capture-closed observation, so that callback was admitted after the close.

### 9.1 Observed on the prototype

The prototype is a scratch worktree of `main` with the fix (source SHA-256, with E3 applied, `18136698a36fcc102668c062a729d611f0375b0bc36349e3c625b7e772f7c7df`; test file SHA-256 `ea16169bca71ec64fa6d1b04c06ead15ef44784c5312d8c86d9dec65a5662064`). Nothing was committed.

- `main` plus the new test file: T1 (both cases) and T2 fail and T3 passes, in 20 of 20 runs; diagnostics as counted in section 2.
- Prototype: all 4 pass, in 50 of 50 runs.
- Prototype, namespace `Parchwell.Kafka.Tests.Statistics`: 99 passed of 99, in about 5 s.
- In Revision 1's prototype, which set its close flag just after the capture-open state changed, 40 of 40 runs passed. Revision 2's order removes the window that `main`'s 2 contention runs show such an observation can hit.
- T2 took 529 to 580 ms in the runs that printed a duration (Revision 1 runs).

### 9.2 Mutations

Each mutation is scratch only, applied one at a time to the prototype source and restored byte-identical afterwards (SHA-256 checked). Killers are the tests that failed in the namespace `Parchwell.Kafka.Tests.Statistics` (99 tests), observed in this session on the Revision 2 tests.

| Id | Requirement | Mutation | Killed by (observed) |
|---|---|---|---|
| M1 | R1, I1 | Admission is never closed (the `main` behaviour) | T1 both cases, T2 |
| M2 | R1 | A call after capture closes adds `late_capture` | T1 both cases, T2 |
| M3 | R1 | A call after capture closes records capture loss | T1 both cases, T2 |
| M4 | R2 | A call refused at the gate after close records no capture loss | T3 |
| M5 | I1 | Admission closes when completion starts instead of at the E1 point | T1 both cases, T2, T3 (class run: 4 of 4 failed). The namespace run did not finish within 300 s: an existing test has no watchdog and waits for coverage that never comes. Recorded as a hang, not a pass |
| M6 | R3, I2 | The seal no longer marks calls in flight as lost | `Given_CallbackPausesBeforeAdmissionGate_When_CompletionSeals_Then_ResultIncludesCaptureLoss` (2 cases), `Given_CallbackPausedBeforeGate_When_CompletionSeals_Then_ReturnedLossCannotBeRewritten` |
| M7 | R4 | Contention records no capture loss | `Given_CaptureContendsWithCompletionGate_When_FreshReportFollows_Then_LossStaysLatched` |

The receipt records the same table from the implementer's own run.

## 10. Deployment assumptions

None. The change is in-process logic in one class. Which thread, library build or container invokes `Capture` does not change E1 or R1 to R6, and T2 exercises the trigger with a real librdkafka client offline. Not proven offline, and not needed for this pull request's correctness: how often a live callback lands in the window. Its live probability is not measured (decision 6752059755).

## 11. Acceptance commands

Run from the repository root with .NET SDK 10.0.402. Each was run in this planning session on the prototype unless marked `main`.

1. `dotnet build Parchwell.slnx --configuration Release` (inside `./scripts/verify-dotnet.sh`). Evidence: 0 warnings, 0 errors.
2. On `main` with only the new test file added: `dotnet test --project tests/Parchwell.Kafka.Tests --configuration Release --no-build -- --filter-class Parchwell.Kafka.Tests.Statistics.KafkaStatisticsLateCaptureTests`. Evidence, observed: `total: 4`, `failed: 3`, `succeeded: 1`; each failure shows `Actual: "capture_loss,late_capture"`. The receipt records the same from a scratch checkout of `main`.
3. The same command on the branch, 20 times. Evidence, observed: 20 of 20 runs `failed: 0`, `succeeded: 4`.
4. `dotnet test --project tests/Parchwell.Kafka.Tests --configuration Release --no-build -- --filter-namespace Parchwell.Kafka.Tests.Statistics`. Evidence, observed: `total: 99`, `failed: 0`.
5. `./scripts/verify-dotnet.sh`. Evidence: every step passes, on a workspace with a Docker daemon (CI has one). Observed in this session: section 11.1.
6. `npm run check:docs`. Evidence: `docs/operations/statistics-bridge.md: no issues found`. Observed: section 11.1.
7. `git diff --name-only origin/main...HEAD`. Evidence: exactly the four paths of 4.1 (the receipt and state file count as item 4).
8. The mutation table of 9.2, run by the implementer, each killed by the listed tests.

### 11.1 Observed results of commands 5 and 6

This workspace has no Docker daemon (`docker info` exit 1). Every test that failed below fails at Testcontainers' Docker endpoint (`ExperimentDockerAuthentication.get_Endpoint` or `LocalKafkaFixture.BoundedDockerAuthentication.get_Endpoint`). The phantom-spike test fails the same way on `main` (observed: `main` plus the new file, `total: 490`, `failed: 4`, the three new tests and that one).

- `./scripts/verify-dotnet.sh` on the prototype, with the E3 and E4 text applied: locked restore, solution build, Kafka XML inventory consistency, architecture tests (17 passed) and Enrichment tests (56 passed) passed. Kafka tests: `total: 490`, `failed: 1` (the phantom-spike test, Docker), `succeeded: 489`. The script stops at the first failing project.
- The remaining projects, run directly with `dotnet test --project tests/<project> --configuration Release --no-build` on the same build:
  - LoadGen: `total: 1677`, `failed: 4`, `succeeded: 1672`, `skipped: 1`. Failures: `RunExecutorComponentTests`, `ProbeV2ComponentTests` (2), `BlobPublicationIntegrationTests`, all Docker. The skip is `B10AbruptPublishedApphostTests`, which needs the published apphost the script provides.
  - Scenarios: `total: 340`, `failed: 0`.
  - Verifier: `total: 7389`, `failed: 13`, all in `Parchwell.Verifier.Tests.Component` (Docker).
- `npm run check:docs` on the prototype: `docs/operations/statistics-bridge.md: no issues found`; 8 warnings in total, none in a file this pull request changes.
- Prototype file SHA-256 after E3 and E4: `statistics-bridge.md` `4107f6626922e7cd0989d35e0257f54612c8ff4433fac535f2ba279bd69ca0a9`. The source was then revised to close admission first (SHA-256 `18136698a36fcc102668c062a729d611f0375b0bc36349e3c625b7e772f7c7df`). On that source: solution build 0 warnings and 0 errors; Kafka tests `total: 490`, `failed: 1` (phantom spike, Docker); LoadGen tests `total: 1677`, `failed: 4`, `skipped: 1`, the same Docker tests as above.

## 12. Plan-check questions (factory.md "Item lifecycle" step 1)

(a) Effort that scales with a runtime count: none. The mutation list is fixed at 7, the repetition count at 20 runs, and every test wait has a watchdog.

(b) Stricter than the parent: the item plan does not cover this pull request; decision 6752059755 is its authority. The plan is stricter than that decision in one place: it requires T3 and the existing loss tests to stay green, so the fix cannot hide a real gap. The decision says "rather than accept it" and does not define benign; Q1 asks Hari.

(c) Dependencies not on `main` or in this pull request: none for the tests. The successor fix's live-completion path (N7) is not on `main`; this plan's tests do not depend on it.

## 13. Sources

- `src/Parchwell.Kafka/Statistics/KafkaStatisticsSession.cs` at `f5cc496f`, lines cited above.
- `src/Parchwell.LoadGen/Run/Execution/RunExecutor.cs` lines 368, 370-374, 388, 419, 529; `RunSessionFactory.cs` line 91; `src/Parchwell.LoadGen/Program.cs` line 411; `src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs` lines 232, 247; `B10AbruptProducer.cs` lines 488-490, 626-632; all at `f5cc496f`.
- `src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` line 38; `src/Parchwell.Verifier/VerifyRun/Publication/PublicationAcceptance.cs` line 57.
- `infra/scripts/phase/phase.py` lines 20-22 and 46; `charts/parchwell/templates/jobs.yaml` lines 1-9 and 77-79.
- docs/operations/statistics-bridge.md lines 42, 81-83 and 87; docs/engineering/dotnet.md lines 28, 35, 60 and 62.
- At `ac5a29d9` on `claude/404-plan`: `items/404/plan-successor-fix.md` Revision 3, sections 2, 3 (E2) and 6; `items/404/plan-capture-contention-race-fix.md` Revision 1, sections 3 and 9; `items/404/plan-pr3a-evidence-completeness.md` Revision 6, section 3. The trial list used in section 8 is the successor fix's section 6.
- Trial 7 artifact as in section 8.

## 14. Open questions for Hari

- Q1. Which late callbacks are benign. Decision 6752059755 does not say. The options and their effect on what the evidence claims:
  - A (this plan): a callback that starts after capture closes is ignored, with no diagnostic. The evidence claim is unchanged: the session's evidence covers reports admitted before capture closed, which on `main` was already the case, because such reports were never written. Completion can now be complete in a run where the client kept emitting statistics after capture closed. Nothing records that those reports existed.
  - B: as A, plus a non-failing count of ignored callbacks on the completion. This adds a public member to `KafkaStatisticsCompletion` and a member to `capabilities.json`, which the verifier reads. It is a larger change and would need a plan revision.
  - C: also treat a callback admitted before close and refused at the gate after close as benign. Its report was delivered while capture was open and was dropped, so C hides a real gap. Not recommended.
  - D: accept the race. Decision 6752059755 rules it out.
- Q2. Contention while capture is open. A callback that cannot take the gate while completion start (lines 185-204), the coverage reads (lines 221-241) or the worker (lines 354-438) holds it drops a report delivered while capture is open. The bridge contract counts it as a gap, so this plan leaves it as is. Decision 6753263792 (standing rule 1, not seen by Hari) accepted it as a deployment assumption for the next trial and left "whether to fix it" to Hari as a scope change; the contention race fix's planning session saw it 9 times in 200 free-running scratch runs, a scratch observation, not a live measurement. Options, each separate from this pull request's fix:
  - Q2-a. Leave it (this plan). Cost: none. Risk: a report delivered while capture is open can still be dropped; decision 6753263792 accepts that for the next trial, and the successor fix's guard flags name it.
  - Q2-b. Let `Capture` wait for the gate instead of trying it. Cost, estimate (basis: this prototype's size and the session code at `main` `f5cc496f`, 2026-11-09): a few lines of code, but it changes two contracts. The public summary of `Capture` says it records a report "without blocking the Kafka callback thread" (line 87), and docs/operations/statistics-bridge.md line 42 says it "records `capture_contention` rather than waiting". The librdkafka statistics thread would then wait while the worker validates a report under the gate; that hold time is not measured. It needs new tests for the wait and a plan revision with a fresh check.
  - Q2-c. Admit reports without the gate, using an atomic admission state like the one this plan adds, and the queue's own thread safety (the channel allows multiple writers, lines 77-83). Cost, estimate on the same basis: a larger change to admission, close and disposal ordering, and new race tests for each; a separate pull request. This plan's close flag would be a building block for it, but this plan does not depend on that.
- Q3. A residual false loss, unchanged. A callback that has already queued its report but not yet run its end-of-capture step (line 139) when the seal runs counts as in flight and gives `capture_loss`. Its window is a few instructions. It was not reproduced and its live probability is not measured. Leave it, or move the end-of-capture step inside the gate in this or a separate pull request.
- Q4. E3 and E4 change contract wording in the public XML remarks and in docs/operations/statistics-bridge.md line 87. Approval of this plan approves that exact text.
- Q5. Order. Order decision 6752454732 runs this pull request alongside the sequence unless "its plan shows a shared file with any of the three". The only shared file is `items/404/state.md`, which every implementing session must update (factory.md "Item state file"). Options:
  - (a) The state file counts: this pull request joins the sequence after the successor fix, and is implemented on `main` after it merges, or stacked on it under decision 6752554218 point 2.
  - (b) The state file does not count, because its lines are per-pull-request bookkeeping and a later merge rebases them without changing behaviour: this pull request runs alongside, on its own branch from `main`.
  - Either way it touches no protected path, so standing decision 6752430614 point 4 lets the control session merge it under GOVERNANCE "Merge rule" in full, and decision 6752059755 requires it to merge before any further B10 trial. Planner's recommendation: (b). This is a sequencing choice, which standing rule 1 does not list; it is left to Hari or the control session as the authorities allow.

## 15. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract (E1 to E5), a requirement, the scope of 4.1, or any existing test assertion. It also stops when:

- the successor fix, PR3a or the contention race fix, merged or in a newer plan revision, changes `KafkaStatisticsSession.cs`, `statistics-bridge.md`, `KafkaStatisticsLateCaptureTests.cs` or the diagnostic-code list the successor fix pins;
- a newer revision of the successor fix's tests relies on a callback between capture close and the seal producing `late_capture` or `capture_loss`;
- T1, T2 or T3 fails, or passes on `main`, in a way this plan does not predict;
- any mutation of 9.2 survives;
- Hari answers Q1 with anything but A, or Q2 with anything but Q2-a.

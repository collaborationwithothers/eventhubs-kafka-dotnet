# Owned-flow request guard: contract-grade plan

Revision: r1. Base commit: `6be908bf1a46910d0ad80b574073364a1fae3e40` on `main`.

One pull request makes the offline chart-host test count only HTTP requests made in its own execution flow, so a background request from another component in the same test process no longer fails it. The check that the host under test makes no outbound request stays, and two new tests show both sides with real requests. The change alters a mutation input, so the runtime qualification is renewed, which needs Hari to accept a small timing risk (question Q1); Q2 asks him to accept one named limit of the attribution method.

## 1. Goal and authority

Goal: `GivenChartVerifyKeysWithoutCallerProducerIdentity_WhenProductionHostRuns_ThenDiscoversProducerAndPreservesFullFidelity` still fails when the host under test makes an HTTP request to anything but `127.0.0.1`, and no longer fails when another thread or component of the test process makes one. This removes the cause of the failed verifier mutation shard that the issue records.

Authority, in creation order:

1. The body of issue #621 (Evidence, Effect, Acceptance, Out of scope), labelled `ready-for-agent` by `haripraghash` at 2026-11-28T16:38:25Z.

The issue has no comments at the time of this revision (catch-up at 2026-11-28, `scripts/decision-log.py --issue 621`: "No decision or approval comments"). There is no item plan and no parent plan: this is a test-harness fix issue, and the issue body is the specification.

Context, not authority: [#602 decision 7224014847](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/602#issuecomment-7224014847) points 3 and 5 (who dispatches acceptance and daily campaigns), and the renewal precedent of [#601 decision 7213032814](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/601#issuecomment-7213032814) point 1.

## 2. Scope

The pull request is named "Owned-flow request guard". Its branch is `claude/621-owned-flow-guard`.

Files allowed to change:

| File | Protected | Change |
|---|---|---|
| `tests/Parchwell.Verifier.Tests/Host/ChartEnvironmentVerifierHostTests.cs` | yes (`Parchwell.Verifier*/` in `.github/CODEOWNERS`) | R1 to R6 |
| `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` | yes (same entry) | R7, written only by the `renew` command |

Paths that must not change: everything under `src/`; every other file under `tests/`, including `tests/Parchwell.Verifier.Tests/Mutation/*.py`, `stryker-c5-config.json` and `Parchwell.Verifier.Tests.csproj`; `.github/**`; `.config/dotnet-tools.json`; `scripts/**`; `docs/**`; `results/**`; every `scenarios/*/expected.yaml`; AGENTS.md and CLAUDE.md.

Out of scope, as the issue sets: the mutation workflows, the shard layout, Stryker settings, and the other users of `OfflineRequestGuard` (the five synthetic-only tests and `GivenRealRequestFromAnotherComponent_WhenSyntheticGuardLives_ThenSyntheticGuardIgnoresIt`, whose code stays as it is). Also out of scope: any hosted dispatch, and turning off the test platform's telemetry (section 11, D2).

Because both files are protected, the merge is Hari's (AGENTS.md GOVERNANCE "Merge rule").

## 3. Cause, as read on `main` at the base commit

- The host test creates `new OfflineRequestGuard()` (`ChartEnvironmentVerifierHostTests.cs` line 35). That guard enables the process-wide `System.Net.Http` event source (line 292) and records every `RequestStart` event, id 1 (lines 319 to 324), from any thread of the process. Any recorded payload without `127.0.0.1` sets the sticky `UnexpectedRequest` flag (lines 331 and 332), which line 56 asserts false.
- The failed request in the issue went to `dc.services.visualstudio.com/v2/track` from `Microsoft.ApplicationInsights.Channel.InMemoryTransmitter.Runner()`. In `tests/Parchwell.Verifier.Tests/packages.lock.json`, `Microsoft.ApplicationInsights` 2.23.0 (line 124) is reached only through `Microsoft.Testing.Extensions.Telemetry` 2.5.1 (lines 403 to 408), which `xunit.v3.core.mtp-v2` 4.0.2 references (line 597, dependency at line 602). `src/Parchwell.Verifier/packages.lock.json` holds no `Microsoft.ApplicationInsights` entry (0 matches, `grep -c`). Microsoft documents that the test platform's telemetry extension uses an Application Insights provider and is registered automatically for xUnit runners that use `Microsoft.Testing.Platform.MSBuild` (section 9, S4). Inferred, not confirmed: the request came from the test platform's own telemetry rather than from another test. The fix does not depend on which of the two sent it.
- Commit `a59f241d6615bbd0ffcb3a670455f9bcee5a944c` (2026-11-14) fixed the same class of failure for synthetic-only guards only.

## 4. Design

The guard gains a third mode, the owned-flow guard. It listens to the process-wide `System.Net.Http` source like the default guard, but it counts a request only when the event callback runs in an execution flow that this guard has marked as its own. The host test marks its own flow before it starts the host, so every request the host makes, on the test's flow or on work the host starts from it, is counted, and a request from a flow the test did not start is not.

This rests on documented .NET behaviour (section 9): `System.Net.Http` telemetry callbacks "happen within the same async scope as the caller that initiated the request", and an `AsyncLocal` value "will be null if the request was sent by a component that didn't set the `AsyncLocal.Value` in the first place" (S1). `Task.Run`, `ThreadPool.QueueUserWorkItem` and the other APIs that fork work capture the execution context (S2), and `Thread.Start` captures it while `Thread.UnsafeStart` does not (S3). The tests in section 7 check this behaviour on the real runtime, so the plan does not rely on the documentation alone.

The named limit (Q2): a request that the host makes from a flow that does not carry the mark is not counted. That happens only when code suppresses the flow (`ExecutionContext.SuppressFlow`, `Thread.UnsafeStart`, `ThreadPool.UnsafeQueueUserWorkItem` and similar) or hands work to a thread that already existed before the mark. `grep -rn "SuppressFlow\|UnsafeStart\|UnsafeQueueUserWorkItem\|UnsafeRegister\|new Thread(" --include=*.cs src` returns no match at the base commit; third-party libraries the host uses were not searched.

Rejected alternatives:

- Ignore only requests whose callback stack contains `Microsoft.ApplicationInsights`. It keeps process-wide detection, but another background sender (the earlier `a59f241d` case was a different one) would still fail the test, so the issue's second acceptance point would not hold.
- Turn off the test platform's telemetry with `TESTINGPLATFORM_TELEMETRY_OPTOUT` (S4). It needs a workflow or test-run environment change, which the issue puts out of scope, and it removes one sender only. Listed as optional D2.
- A third host run that makes a deliberate request. Every mutant runs every test (`"coverage-analysis": "off"` in `stryker-c5-config.json`), so a host run per mutant costs more than the guard-level tests of section 7, which the issue's chosen style (`GivenRealRequestFromAnotherComponent_WhenSyntheticGuardLives_ThenSyntheticGuardIgnoresIt`) also tests at guard level.

## 5. External contracts

This pull request changes no wire format, failure name, stage name, configuration key, CLI flag, Kafka client setting or verifier predicate. It changes no existing test input or expected value. It touches these contracts:

- **Qualification file.** `c5-runtime-qualification.json` changes only by running `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py renew --head <HEAD_SHA> --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json --basis "<BASIS>"` (docs/operations/verifier-mutation.md "Runtime qualification diagnostics", version 2 `renew`; `c5_runtime.py` `renew` line 2554 and `renew_v2` line 2609). `<HEAD_SHA>` is the commit that holds the test change. `<BASIS>` is exactly the following text, with the five measured values of R8 and Hari's decision URL that answers Q1 filled in, and nothing else changed:

  `Issue 621 makes the offline chart-host request guard in ChartEnvironmentVerifierHostTests count only HTTP requests made in the test's own execution flow, and adds 3 test cases that make 4 HTTP requests in all to a closed loopback port. No target, runner or workflow changed. Local mutation-only suite wall, <CORES>-core container, idle, fresh process per run, 2 runs each on <DATE>: <B1> s and <B2> s before, <A1> s and <A2> s after. Timing risk accepted by <DECISION_URL>.`

  `<CORES>` is the output of `nproc`, `<DATE>` the UTC date of the runs as `YYYY-MM-DD`, and `<B1>`, `<B2>`, `<A1>`, `<A2>` the walls in seconds with two decimals, in run order.
- **Diagnostic output of the guard.** `WriteDiagnostics` JSON (record types `RequestDiagnostic`, `PhaseDiagnostic`, `GuardDiagnostic`, lines 382 to 389) keeps its shape. Nothing outside this test file reads it.

## 6. Requirements

R1. WHEN an owned-flow guard is created THEN it enables `System.Net.Http` events at `EventLevel.Informational` exactly as the default guard does (line 292), and records its `guard_created` phase as every guard does (line 317).

R2. WHEN the test has marked its current execution flow as owned by an owned-flow guard and a `System.Net.Http` event with `EventId == 1` is written in that flow, or in work forked from it after the mark by `Task.Run` or `Thread.Start` THEN the guard passes the event to `RecordRequest` with the same arguments as the default guard (lines 322 and 323). The existing predicate is unchanged: a payload with no value containing `127.0.0.1` sets `UnexpectedRequest` to true, sticky; `LastRequest` and the diagnostic records update as they do today.

R3. WHEN a `System.Net.Http` event is written in a flow that does not carry this guard's mark (a thread started before the mark, or any other component's flow) THEN the owned-flow guard does not call `RecordRequest`: `UnexpectedRequest` stays false, `LastRequest` stays null, and `Snapshot().Requests` gains no entry. A missing or unreadable mark counts as not owned only because no host runs before the mark is set; the mark is set before `Program.RunAsync` is called (R4).

R4. WHEN `GivenChartVerifyKeysWithoutCallerProducerIdentity_WhenProductionHostRuns_ThenDiscoversProducerAndPreservesFullFidelity` runs THEN line 35 creates an owned-flow guard instead of `new OfflineRequestGuard()`, and the test method marks its own flow as owned before it calls `Program.RunAsync`, in the test method body and not inside the `Program.RunAsync` lambda. Every other line of that test method stays as it is at the base commit: both data rows, every phase record, the line 56 assertion and its message text `The offline chart-host proof attempted an HTTP request: {requestGuard.LastRequest}`, and every later assertion.

R5. WHEN the default guard (`new OfflineRequestGuard()`) or the synthetic-only guard (`new OfflineRequestGuard(syntheticOnly:true)`) is used THEN it behaves exactly as at the base commit, including its constructor call shape at every existing call site (lines 103, 149, 150, 171, 205, 222), `Bound`, `RecordLimit`, `ScalarLimit`, `StackLimit`, and the direct `RecordRequest` and `RecordPhase` calls of the synthetic tests. A direct `RecordRequest` call on an owned-flow guard records as it does on the other guards, because ownership filters only the event callback.

R6. WHEN the pull request merges THEN the code comments of `SuppressibleListener` and `OfflineRequestGuard` (lines 285, 286 and 314 to 316) describe the three modes truthfully, including the Q2 limit in one sentence ([Code comments](../../docs/engineering/code-comments.md) "Review", false-comment exception).

R7. WHEN the pull request is opened THEN the qualification file has been renewed with the command and basis of section 5, as a separate commit after the test change, and `c5_runtime.py check --base <merge base> --head <HEAD>` does not report `rejected`. If a later commit changes the test file again, `renew` runs again on that commit with the same basis and the measurements of R8 are taken again on that commit for the "after" values.

R8. WHEN the implementer prepares the renewal THEN it measures the mutation-only suite wall locally, as the #601 and #602 renewals did: build `tests/Parchwell.Verifier.Tests` in Release with `-p:ParchwellVerifierMutationOnly=true` into an artifacts directory outside the repository, once at the base commit and once at the commit that holds the test change, then run each built test executable twice with no test filter, each run a fresh process, on the same idle machine, timing the whole process wall. It records the four walls, `nproc` and the date in the basis. If a test fails for an environment reason in a base run, the same filter excluding it is used for all four runs and the PR implementation notes name it.

R9. WHEN any of R4 to R8 cannot be met without changing a file outside section 2, an existing assertion, or the 127.0.0.1 predicate THEN the implementer stops under section 12.

## 7. Test map

All new tests go in `ChartEnvironmentVerifierHostTests`, inside the existing `VerifierEnvironment` collection. Names follow the file's existing style. Each request goes to `http://localhost:<port>/<path>`, where `<port>` is the port of a `TcpListener` on `IPAddress.Loopback` port 0 that the test started and stopped just before, so the connection is refused at once instead of waiting. `localhost` is used for the reason the comment at line 157 gives. Each request uses an `HttpClient` with `Timeout` of 300 ms, as line 156 does, and catches only `HttpRequestException` or `TaskCanceledException`, as line 159 does. Every thread a test starts is joined with a 5-second timeout and the test asserts the join returned true.

| Req | Test | Inputs | Expected, literal |
|---|---|---|---|
| R1, R2, R3 | `GivenRequestsFromForeignAndOwnedFlows_WhenOwnedFlowGuardLives_ThenRecordsOnlyTheOwnedRequest` (Fact) | 1. `observing = new OfflineRequestGuard()`. 2. A foreign thread is created and started with `Thread.Start` before step 3; it waits up to 5 s on a signal, then sends one request to path `/foreign-thread`. 3. Create the owned-flow guard and mark the test's flow. 4. Signal the foreign thread and join it. 5. Assert the foreign expectations. 6. The test flow sends one request to path `/owned-direct`. 7. Assert the owned expectations. | After step 4: `observing.UnexpectedRequest` is true; `observing.Snapshot().Requests` contains an entry whose `Payload` contains `pathAndQuery=/foreign-thread` (ordinal); owned guard: `UnexpectedRequest` false, `LastRequest` null, `Snapshot().Requests` empty. After step 6: owned guard `UnexpectedRequest` true; `LastRequest` contains `host=localhost;` and `pathAndQuery=/owned-direct` (ordinal); `Snapshot().Requests` has exactly one entry, whose `Payload` contains `pathAndQuery=/owned-direct`. |
| R2 | `GivenRequestFromWorkStartedInOwnedFlow_WhenOwnedFlowGuardLives_ThenRecordsIt` (Theory) | `[InlineData("task")]`: after the mark, `await Task.Run(...)` whose body sends one request to `/owned-task`. `[InlineData("thread")]`: after the mark, a thread created and started with `Thread.Start` sends one request to `/owned-thread`, then is joined. | Owned guard `UnexpectedRequest` true; `LastRequest` contains `host=localhost;` and `pathAndQuery=/owned-<start>`; `Snapshot().Requests` has exactly one entry, whose `Payload` contains `pathAndQuery=/owned-<start>`, where `<start>` is the data row value. |
| R4 | `GivenChartVerifyKeysWithoutCallerProducerIdentity_WhenProductionHostRuns_ThenDiscoversProducerAndPreservesFullFidelity(mismatch: False)` and `(mismatch: True)` | unchanged | unchanged; both rows pass. |
| R5 | `GivenSyntheticRequestEvents_WhenRecorded_ThenPreservesPredicateAndAttribution`, `GivenRealRequestFromAnotherComponent_WhenSyntheticGuardLives_ThenSyntheticGuardIgnoresIt`, `GivenCurrentActivity_WhenRecorded_ThenRetainsCorrelationWithoutChangingAcceptance`, `GivenScalarBoundary_WhenFormatted_ThenMarksTruncationWithinLimit` (3 rows), `GivenStackBoundary_WhenFormatted_ThenMarksTruncationWithinLimit` (3 rows), `GivenDiagnosticRecordBoundary_WhenRecorded_ThenKeepsSeparateDropCounts` (3 rows), `GivenLongPayloadAndFailingDiagnosticOutput_WhenRecorded_ThenPreservesRawGuardEvidence` | unchanged | unchanged; all pass, with no change to their text. |
| R7 | `.NET verification` job step "Qualify verifier mutation runtime" (`.github/workflows/dotnet-verification.yml` lines 139 to 152) | PR base and head | not `rejected`; the `test_c5_*.py` unit tests pass. |

Boundaries: the foreign case is the one the issue records (a sender whose flow began before the test's mark); the owned cases cover the test's own flow and the two documented ways the host could fork work that captures the flow. The 127.0.0.1 predicate itself is unchanged and stays covered by `GivenSyntheticRequestEvents_WhenRecorded_ThenPreservesPredicateAndAttribution` (lines 107 to 115), so no new boundary row is added for it. The new tests send 4 requests in 3 test cases in all (2 in the Fact, 1 per Theory row).

Standing fixture rule (modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2): when an existing test's fixture is rejected by a check this plan adds or changes, the implementer does exactly one of: (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it, and the cold review and Codex review check each listed change. The only expected fixture change is line 35 under R4.

## 8. Invariants and safety rules

- No assertion in the test file is added to an existing test, loosened, deleted or skipped (AGENTS.md "Code Review Rules"; the issue's fourth acceptance point).
- The owned-flow guard never counts fewer requests from the owned flow than the default guard would count from that flow; it only stops counting other flows.
- No file under `src/` changes, so no verifier logic or mutation target changes; `renew` refuses a changed target source (`c5_runtime.py` lines 2564 and 2565).
- No workflow dispatch, Azure access or infrastructure step. Hard safety rules 1 to 3, 5, 6 and 8 are not engaged.
- No number is stated in the PR or the basis that was not measured, except estimates labelled as such (hard safety rule 11).

## 9. Sources

Documentation tool: the Microsoft Learn MCP server (`microsoft_docs_search`, `microsoft_docs_fetch`), read 2026-11-28. No Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour is relied on.

- S1. [Networking events in .NET](https://learn.microsoft.com/dotnet/fundamentals/networking/telemetry/events), "In-process correlation": "As telemetry events (and their corresponding callbacks) occur as part of the underlying operation, they happen within the same async scope as the caller that initiated the request." Step 3: "`AsyncLocal.Value` will be `null` if the request was sent by a component that didn't set the `AsyncLocal.Value` in the first place." The same page's EventListener output shows the `RequestStart` payload `scheme=https host=httpbin.org port=443 pathAndQuery=/get versionMajor=1 versionMinor=1 versionPolicy=0`.
- S2. [ExecutionContext and SynchronizationContext](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/executioncontext-synchronizationcontext), "How ExecutionContext flows": "All asynchronous APIs in .NET that fork work (Run, QueueUserWorkItem, BeginRead, and others) capture ExecutionContext and use the stored context when invoking your callback." "ExecutionContext always flows": `SuppressFlow` "suppresses ExecutionContext capture for work that you queue while suppression is active."
- S3. [Thread.UnsafeStart](https://learn.microsoft.com/dotnet/api/system.threading.thread.unsafestart?view=net-11.0), Remarks: "Unlike Start(Object), which captures the current ExecutionContext and uses that context to invoke the thread's delegate, UnsafeStart(Object) explicitly avoids capturing the current context and flowing it to the invocation."
- S4. [Microsoft.Testing.Platform telemetry](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-telemetry): the feature "requires the Microsoft.Testing.Extensions.Telemetry NuGet package"; with `Microsoft.Testing.Platform.MSBuild` ("included transitively by MSTest, NUnit, and xUnit runners") "this extension is auto-registered"; manual registration is `builder.AddAppInsightsTelemetryProvider()`; "To disable telemetry, set either `TESTINGPLATFORM_TELEMETRY_OPTOUT` or `DOTNET_CLI_TELEMETRY_OPTOUT` environment variable to `1`."
- S5. [URI query and fragment redaction in HttpClient EventSource events](https://learn.microsoft.com/dotnet/core/compatibility/networking/9.0/query-redaction-events): from .NET 9 the query part of `pathAndQuery` in `RequestStart` is replaced by `*`; the new tests use paths with no query, so the path appears unchanged.
- Repository, at the base commit: `tests/Parchwell.Verifier.Tests/Host/ChartEnvironmentVerifierHostTests.cs` lines 18 to 23, 29 to 94, 146 to 166, 285 to 389; `tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj` lines 20 to 25; `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` (`"coverage-analysis": "off"`); `tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py` lines 365 and 366 (`NON_RENEWABLE`), 2500 (`check`), 2554 to 2591 (`renew`), 2609 to 2637 (`renew_v2`); `c5-runtime-qualification.json` line 289 (this file's hash `48e4ff2bc0a7692c57ad50c1af881f5f3cb4d427f9aa1c4610577c762430b040` in `shared_inputs`, equal to `sha256sum` of the file); `.github/workflows/dotnet-verification.yml` lines 139 to 152; docs/operations/verifier-mutation.md "Runtime qualification diagnostics"; docs/agents/factory.md "Evidence pack" (mutation acceptance); docs/engineering/dotnet-verification.md (mutation-only stages); the two lock files cited in section 3.

## 10. Estimates

- Suite time. Estimate, 2026-11-28, basis: the 4 new requests fail at connect on a closed loopback port instead of waiting for the 300 ms timeout, and the owned-flow check adds one `AsyncLocal` read per `System.Net.Http` event: well under 1 s added to a mutation-only suite whose recent local walls were 30.50 s and 33.50 s (the #602 renewal basis in `c5-runtime-qualification.json`, measured 2026-11-28). Not measured; R8 measures it. Every mutant runs the whole suite (`"coverage-analysis": "off"`), so any addition applies per mutant.
- Size. Estimate, 2026-11-28, basis: the shape of sections 6 and 7. One pull request, 2 files: the test file about 90 changed lines; `c5-runtime-qualification.json` about 40 to 80 changed lines (an assessment of this path in each of the seven `timing_inputs` contexts, two of which already hold one, plus its `shared_inputs` hash). Size is reported, not enforced.

## 11. Deferred work

- D1. Mutation acceptance on `main`. Evidence: docs/agents/factory.md "Evidence pack" requires, for a change to any mutation input, a passing `verifier-c5-mutation.yml` run dispatched on `main` after the merge, whose `summary.json` reports `measurement_complete: true` and `break_passed: true` and names a commit that includes the merge. On the verdict path: yes, it gates evidence-pack publication. Why deferred: the rule permits the run only after merge. Responsible: the control session that dispatches the batched acceptance and daily campaigns ([#602 decision 7224014847](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/602#issuecomment-7224014847) points 3 and 5). Stage: after this pull request merges; the next acceptance campaign on a `main` commit that includes it counts, with no extra dispatch. Prerequisite: the merge. Workload bound: one campaign, under that decision. Completion evidence: that run's `summary.json` values above. Blocks: publication of any evidence pack that relies on verifier mutation acceptance. A timing overrun of the renewed estimates would show in that campaign and returns to Hari.
- D2. Optional: turn off the test platform's telemetry in the mutation runs with `TESTINGPLATFORM_TELEMETRY_OPTOUT=1` (S4). Not on the verdict path: this plan removes the failure without it. Out of scope by the issue. Responsible: none unless Hari opens an issue. No gate.

## 12. Acceptance commands

| # | Command | Evidence it must produce |
|---|---|---|
| A1 | `./scripts/verify-dotnet.sh --tier fast --project Parchwell.Verifier.Tests` while the change moves (docs/agents/pull-requests.md "Verification gates" step 1; a partial run) | Exit 0; the three new test cases and both host-test rows pass in the normal and mutation-only stages. |
| A2 | `git diff --name-only origin/main...HEAD` | Exactly the two files of section 2. |
| A3 | `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_*.py'` and `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base $(git merge-base origin/main HEAD) --head HEAD --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` | Unit tests pass; `check` status is not `rejected` and lists `tests/Parchwell.Verifier.Tests/Host/ChartEnvironmentVerifierHostTests.cs` among `assessed_inputs`. |
| A4 | The R8 measurement | Four walls, `nproc` and date, recorded in the basis and in the PR description. |
| A5 | Cold review under docs/agents/pull-requests.md rule 8, then `./scripts/verify-dotnet.sh --tier sandbox` once on the cleared source (gate step 3; a partial run excluding `Tier=CGroupV2`) | Clean review round recorded; exit 0. |
| A6 | Hosted `.NET verification` on the final head (gate step 4) and Codex review | Pass; recorded in the PR description. |

## 13. Plan-check questions

(a) Steps whose effort scales with a number unknown at planning time: none uncapped. R8 is fixed at two builds and four runs; cap: each run at most 600 s; exhaustion action: `push -wip and hand off to the next session`. `renew` runs once per commit that changes the test file. Fixture changes are bounded by the one guard construction in R4.

(b) Where the plan is stricter than its parent: there is no parent plan. Against the issue body, the plan fixes the mechanism (execution-flow attribution), pins two new tests with literal expected values, adds owned-flow cases for `Task.Run` and `Thread.Start`, uses refused connections to bound suite time, and pins the renewal basis and measurement. These follow from the issue's acceptance points and the qualification gate it does not mention.

(c) Tests or checks that depend on code, data or behaviour neither on `main` nor in the pull request: the .NET 10 runtime's execution-context flow and `System.Net.Http` event delivery, checked against S1 to S3 and exercised by the two new tests on the real runtime; the test platform's telemetry sender, checked against the lock files and S4, which the fix does not need to be present. No deployment assumption about a container, image, chart or cluster runtime: the change runs only in test processes.

## 14. Deployment assumptions

None. The change affects only test processes on CI runners and local workspaces, and every runtime behaviour it relies on is exercised by a test in section 7.

## 15. Open questions for Hari

- Q1. Accept the timing risk of renewing the mutation runtime qualification for this change, without a hosted timing diagnostic before merge? Evidence: section 10 estimate; R8 records the measured walls in the basis before the pull request opens. Recommendation: accept, as for #601. Alternative: a hosted `verifier-c5-sizing.yml` diagnostic before merge, which costs a dispatch and does not change the fix. Effect on the verdict route: without acceptance the renewal has no authorized basis and the required qualification check cannot pass, so the pull request cannot merge.
- Q2. Accept by name the attribution limit of section 4: a request the host makes from a flow that does not carry the test's mark (flow suppressed, or work handed to a thread that existed before the mark) is not counted by the host test. Evidence: section 4 search of `src/` (no match); S1 to S3. Recommendation: accept. Alternatives: the stack-based exclusion or the telemetry opt-out of section 4, each of which leaves other background senders able to fail the test. Effect on the verdict route: none on verdicts; it bounds what the offline host proof shows.

## 16. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope: for example when an existing assertion or test text would have to change beyond R4, when the 127.0.0.1 predicate would have to change, when `renew` refuses the change or `check` rejects it, when a file outside section 2 would have to change, or when R8 cannot be completed within its cap.

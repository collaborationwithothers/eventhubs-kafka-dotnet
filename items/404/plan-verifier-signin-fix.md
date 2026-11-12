# Verifier sign-in fix contract-grade plan

Revision 1. Issue 404, pull request short name `verifier sign-in fix`. Status: not approved. Implementing tool: Claude Code ([decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192) point 2). Hari merges.

When the diagnostic verifier mode `b10-abrupt-verify` fails to install its first Kafka sign-in token within `TokenTimeout`, it gives up the window read today. This pull request makes that mode try once more, on a new Kafka client with a fresh `TokenTimeout`, before it records the window as incomplete. No check, problem, reason, output member, configuration key or deadline changes.

## 0. Changes

Revision 1 is the first revision.

## 1. Goal and authority

Goal, from [decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192) point 1: one slow token-endpoint call no longer fails the verify Job, while the Job still ends within its existing deadline and every check and published format stays unchanged. The mechanism is for this plan to propose.

Authority, in creation order:
- The [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404) and the decisions listed in `items/404/state.md` on `main` at `675f8372`, which this pull request does not change except as below.
- [Decision 6837706862](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837706862): one diagnostic trial, run 43318281268, on `main` at `675f8372`.
- [Evidence reading 6837900667](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837900667) (evidence, not a decision): in run 43318281268 the verifier's problems were only `b10_abrupt_verify_window_incomplete`; a token acquisition began about 22:37:30-32Z and failed at 22:37:40Z with `TaskCanceledException`; librdkafka logged "Failed to acquire SASL OAUTHBEARER token". Which token, and why the call was slow, is not established.
- [Decision 6837940546](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837940546): one further trial with no code change. It does not govern this pull request.
- [Decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192): this pull request, its scope and goal (point 1), one session under docs/agents/factory.md "Item lifecycle" step 3 "Standing approval", Claude Code, no `results/`, expected outcome or verdict, no workflow dispatch, Hari merges (point 2), and the caps in section 9 (point 3). It authorizes no trial.

Parent items #347 and #348 have no decision or approval comment created on 2026-11-12 (read by script at planning time); no parent decision is adopted for this pull request.

## 2. Scope

### 2.1 Cause in code (read on `main` at `675f8372`)

- `src/Parchwell.Verifier/VerifyRun/Abrupt/B10AbruptVerifyHandler.cs` lines 146 to 154: inside `if (start is not null)`, the handler calls `sessions.Create(kafkaWork.Token)` once and then `session.InstallToken(cancellationToken)`. Lines 194 to 197: any `KafkaFailure` exception, including an install failure, records problem 5 `window_incomplete`. Lines 198 to 205: the `finally` block cleans the session under `CleanupTimeout` and calls `CheckCleanup`.
- `src/Parchwell.Verifier/Common/Kafka/VerifierKafkaSession.cs` lines 35 to 62: `InstallToken` starts a timer of `TokenTimeout` that cancels the session's own `acquisition` source, then polls until a token is installed. Lines 45 to 47: a first initial-acquisition failure is sticky for that session, so the same native client cannot recover.
- `src/Parchwell.Verifier/Common/Kafka/VerifierKafkaSessionFactory.cs` lines 20 to 44: each `Create` builds a new linked `acquisition` source, a new `KafkaOAuthBearerSession`, a new consumer and a new dependent admin client.
- `src/Parchwell.Kafka/Authentication/KafkaOAuthBearerSession.cs` lines 232 and 235 to 238: the refresh callback passes the session's cancellation token to the token provider, and any exception from it becomes an `initial` `acquisition` failure.
- `charts/parchwell/values.yaml` line 63: `TokenTimeout: 00:00:10`.

### 2.2 Files allowed to change

- `src/Parchwell.Verifier/VerifyRun/Abrupt/B10AbruptVerifyHandler.cs`, and new internal files under `src/Parchwell.Verifier/VerifyRun/Abrupt/` if the implementer chooses.
- Test files under `tests/Parchwell.Verifier.Tests/Component/`: a new test file for section 6, and `B10AbruptVerifyHarness.cs` for the hooks those tests need. Existing hooks keep their current behaviour for existing callers.
- `docs/operations/b10-abrupt-contract.md`: check 14 only, with the text in section 3.2.
- `items/404/state.md` (docs/agents/factory.md "Item state file") and a new receipt `items/404/verifier-signin-fix-receipt.md`.

### 2.3 Paths that must not change

- `src/Parchwell.Kafka/**`, `src/Parchwell.Verifier/Common/**`, `src/Parchwell.Verifier/Program.cs`, `src/Parchwell.Verifier/VerifierHostOptions.cs`, `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs`, and every other verifier mode's handler.
- `charts/**`, `docs/operations/a01-runtime-limits.json`, `docs/operations/chart-contract.md`, `docs/operations/verifier-contract.md`, `docs/blueprint.md`.
- `.github/workflows/**`, `docs/operations/workflows/**`, `infra/**`, `scripts/**`, `results/**`, every `expected.yaml`, every `*.csproj`, `Directory.*.props`, `*.sln*`.
- `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`.

### 2.4 Protected paths, CI and existing tests

- `src/Parchwell.Verifier/**` and `tests/Parchwell.Verifier.Tests/**` match `Parchwell.Verifier*/` in `.github/CODEOWNERS`, so Hari merges.
- `B10AbruptVerifyHandler.cs` is not in the `mutate` list of `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` on `main` at `675f8372`, so docs/agents/factory.md "Evidence pack" mutation acceptance does not apply.
- This plan adds no check and tightens none, so `plan-pr` plan rule 2 (standing fixture rule) does not apply. No existing test's expected value changes. If an existing test fails, that is a stop under section 12, not a fixture change.

## 3. External contracts

### 3.1 Unchanged

The mode name, every option name and value, every problem and reason literal, the `nonfinal_result` envelope and `data` members, check numbering, exit codes, the Job deadline (`verify_job_seconds` 750) and `ProcessLifetime` (00:12:00) are unchanged. No log line is added or pinned.

### 3.2 `docs/operations/b10-abrupt-contract.md` check 14

Check 14 currently reads (line 222 at `675f8372`): "14. Read high watermarks once at verify start for every start-line partition. Complete the drain within `SafetyStop` and the consumed-record budget, retaining existing consumed-byte accounting. Only records with `start <= offset < end` for their partition are in scope. [5]"

Insert these sentences after "every start-line partition.", leaving the rest of the check unchanged:

"Before that read, the handler installs the Kafka OAUTHBEARER token on a new verifier Kafka client within `TokenTimeout`. When installation fails and the host is not stopping, it cleans up that client within `CleanupTimeout`, as it cleans up the final client, and makes exactly one further installation on a new client with a fresh `TokenTimeout` ([decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192)). A failure of that second installation is a failure of this check. A cleanup failure of the first client ends the mode as a cleanup failure of the final client does, without a second installation."

This is the one statement of the retry rule. Other files do not restate it.

## 4. Requirements

The "first install" is the first `InstallToken` call in the handler's Kafka block; the "second install" is the retry.

- R1. WHEN the handler reaches its Kafka block, THEN it creates a session through the injected `VerifierKafkaSessionFactory` with `kafkaWork.Token` and calls `InstallToken` with the handler's `cancellationToken`, as today.
- R2. WHEN the first install throws an exception for which the existing `KafkaFailure(exception, cancellationToken)` is true, THEN the handler cleans up that session under the same rule as the `finally` block (a `CleanupTimeout` deadline source on the handler's clock, `Cleanup`, then `CheckCleanup`), then creates exactly one new session through the same factory with the same `kafkaWork.Token`, and calls `InstallToken` on it. The second install gets a full `TokenTimeout` of its own, because `InstallToken` starts its budget on each call.
- R3. WHEN the second install succeeds, THEN the handler continues exactly as after a successful first install, using the second session for the watermark reads, the drain, check 29's re-read and the final cleanup. No output member, problem, reason or problem detail is added because a retry happened.
- R4. WHEN the second install fails with a `KafkaFailure` exception, THEN problem 5 `b10_abrupt_verify_window_incomplete` is recorded through the existing catch, no third session is created, and the second session is cleaned up by the existing `finally` block.
- R5. WHEN the first session's cleanup throws, or `CheckCleanup` finds the cleanup took at least `CleanupTimeout`, THEN no second session is created and the exception leaves the handler without being recorded as problem 5, so the outcome is the one an expired final cleanup has today: exit code 1 and no output object created.
- R6. WHEN the first install fails and `cancellationToken` is cancelled (`KafkaFailure` is false), THEN no second session is created and the existing behaviour for that exception is unchanged.
- R7. WHEN `sessions.Create` throws, or the watermark, metadata, drain or re-read work fails after an install succeeded, THEN no retry happens; behaviour is unchanged.
- R8. WHEN the first install succeeds, THEN exactly one session is created, as today.

Worst-case added time, from configured values only (computed by script): one `TokenTimeout` (10 s) plus one `CleanupTimeout` (10 s), 20 s. The sum of the mode's configured budgets that `VerifierHostOptionsValidator.ValidateAbrupt` collects (lines 237 to 241: `TokenTimeout`, `MetadataTimeout`, `WatermarkTimeout`, `BlobIoTimeout`, `IoTimeout`, `CleanupTimeout`, `SafetyStop`) is 680 s today and 700 s with the retry, below `ProcessLifetime` 720 s, which is below the Job deadline 750 s. `ValidateAbrupt` does not call `ValidateBudgetSum`, and this plan does not add that call. These are configured bounds, not measured durations. The existing process watchdog (`src/Parchwell.Verifier/Program.cs` lines 435 to 457) still ends the process at `ProcessLifetime`, so the Job ends within its existing deadline whatever the token endpoint does.

## 5. Invariants and safety

- The verifier stays the oracle: no check is weakened. A retry changes only whether the window can be read; once read, every check runs unchanged.
- No new configuration key, chart value, schema entry or environment variable.
- No change to `Parchwell.Kafka`, which the load generator under test also uses.
- No workflow dispatch, trial, `results/`, expected outcome or verdict change ([decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192) point 2).
- At most two sessions exist over the handler's life, and never two at once: the first is cleaned before the second is created.

## 6. Test map

A new test file under `tests/Parchwell.Verifier.Tests/Component/`, trait `Tier` = `Docker` like the other abrupt component tests, using `B10AbruptVerifyHarness` against the local Kafka fixture with default evidence (`new B10AbruptVerifyEvidence()`) after `ResetAsync`, which the existing helper `AssertRunAsync(run)` shows ends with exit code 0 and `problems` `[]` (`B10AbruptVerifyCoverageTests.cs` lines 684 to 695). Each factory call must build a real `VerifierKafkaSession` over a real `KafkaOAuthBearerSession` whose token provider the test scripts per call, so the sticky initial failure and the `TokenTimeout` timer are the production code. The test sets `Verifier:TokenTimeout` to `00:00:01` unless stated. "Creates" below counts factory calls; "output creates" is the harness's existing `Creates` count of output objects.

| Test | Requirement | Inputs | Expected |
|---|---|---|---|
| T1 | R2, R3 | Factory call 1's provider blocks until its cancellation token is cancelled, then throws `TaskCanceledException`. Call 2's provider returns a token expiring 1 hour after the clock's now. A baseline run with no failure runs first in the same test after the same reset. | Exit 0; `problems` `[]`; `window_complete` true; factory calls 2; session 1's consumer and admin are disposed before factory call 2; `data` is `JsonNode.DeepEquals` to the baseline run's `data`. |
| T2 | R2, R3 | Call 1's provider throws `InvalidOperationException` at once (an `acquisition` failure without delay). Call 2's provider returns a valid token. | Exit 0; `problems` `[]`; factory calls 2. |
| T3 | R4 | Both calls' providers block until cancelled, then throw `TaskCanceledException`. | Exit 0; `problems` `["b10_abrupt_verify_window_incomplete"]`; `window_complete` false; `end_line` null; factory calls 2; both sessions' consumers disposed. |
| T4 | R5 | Call 1's provider blocks until cancelled. `Verifier:CleanupTimeout` `00:00:00.100`. The run clock advances by `elapsed` ms when session 1's consumer is disposed, as `Given_KafkaCleanupElapsedBelowAtOrAboveBudget_When_HostFinishesWindow_Then_DeadlineBoundaryPreventsOutput` does for the final cleanup. Theory over `elapsed` 99, 100, 101. | 99: exit 0, `problems` `[]`, factory calls 2. 100 and 101: exit 1, output creates 0, factory calls 1. |
| T5 | R6 | Call 1's provider calls `IHostApplicationLifetime.StopApplication()`, which cancels the token `VerifierWorker` passes to the handler (`src/Parchwell.Verifier/VerifierWorker.cs` line 81), then blocks until its own cancellation token is cancelled and throws `TaskCanceledException`. | Factory calls 1; output creates 0. |
| T6 | R7 | Call 1 installs a token; the first `QueryWatermarkOffsets` throws `KafkaException(Local_TimedOut)`. | Exit 0; `problems` `["b10_abrupt_verify_window_incomplete"]`; factory calls 1. |
| T7 | R1, R8 | Call 1 installs a token at once. | Exit 0; `problems` `[]`; factory calls 1. |

Every existing test in `Parchwell.Verifier.Tests` passes unchanged.

## 7. Deployment assumptions

- DA-1. In the deployed verifier pod, a second token request made after the first is cancelled reaches the token endpoint as a new request and completes within `TokenTimeout` (10 s) when the first was slow. No offline test can show the endpoint's behaviour; T1 shows only that the verifier makes the second request. Evidence: run 43318281268 ([6837900667](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837900667)), one fresh pod at start, idle apart from the verifier's own work: two acquisitions completed in 276 ms and 38 ms, then one did not complete within 10 s. Whether those used an already-open or a fresh connection is not recorded. That a later request is a new request is supported by the `TokenCredential.GetToken` remarks (section 10, source 2), and by the order R2 pins (the first request is cancelled and its client cleaned before the second request starts); that it is faster is not established. Listed under section 11.

## 8. Acceptance commands

- `./scripts/verify-dotnet.sh`: the full gate passes with warnings as errors, including T1 to T7 and every existing test. Evidence: the command's summary line in the receipt.
- `./scripts/verify-dotnet.sh --project Parchwell.Verifier.Tests` run first while iterating; it is not the full gate.
- `npm run check:docs`: no new warning in `docs/operations/b10-abrupt-contract.md`; dispositions under docs/agents/pull-requests.md rule 8.
- `git diff --name-only origin/main...HEAD` lists only paths in section 2.2. Evidence: the list in the receipt.
- A script check that `docs/operations/b10-abrupt-contract.md` differs from `main` only inside check 14.

## 9. Caps and plan-check answers

Caps ([decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192) point 3): 20 verification-failure root causes and 20 review-finding root causes, each with exhaustion action `push -wip and hand off to the next session` (AGENTS.md "Before opening a pull request" rule 11).

(a) Steps whose effort scales with a number unknown at planning time: verification-failure root causes and review-finding root causes, capped as above. Test count is fixed at seven tests (T4 has three cases).

(b) Stricter than the parent plan: the item plan (Revision 5) and the decisions before 6837975192 say nothing about token installation. This plan adds a retry the decision asked for and changes no parent rule. It is not stricter.

(c) Tests or checks that depend on code, data or behaviour neither on `main` nor in the pull request: the token endpoint's live behaviour (DA-1). The tests use a scripted token provider and the local Kafka fixture already on `main`; the refresh callback is triggered by `Consume`, as `VerifierKafkaSession.InstallToken` already relies on (section 10, source 4).

## 10. Sources

Documentation tools used: Microsoft Learn MCP (`microsoft_docs_fetch`, `microsoft_docs_search`) and the confluent-kafka-dotnet source at its pinned tag, read 2026-11-12.

1. Azure.Core `RetryOptions` defaults: `MaxRetries` 3, `NetworkTimeout` 100 seconds, "The timeout applied to an individual network operation" (https://learn.microsoft.com/en-us/azure/storage/common/storage-srp-manage-account-dotnet#configure-armclient-options, and the class page https://learn.microsoft.com/en-us/dotnet/api/azure.core.retryoptions?view=azure-dotnet). The Kafka credential in `KafkaAuthenticationServiceCollectionExtensions.cs` lines 36 to 41 sets no retry options. Inferred, not stated by the source: a stalled request is not abandoned by the SDK within 10 s, so the 10 s `TokenTimeout` cancels it first; a retry inside the SDK cannot help within that budget, which is why the retry is the verifier's.
2. `TokenCredential.GetToken(TokenRequestContext, CancellationToken)`: the token "to use" for cancellation; remarks: "Caching and management of the lifespan for the AccessToken is considered the responsibility of the caller: each call should request a fresh token being requested." (https://learn.microsoft.com/en-us/dotnet/api/azure.core.tokencredential.gettoken?view=azure-dotnet, Azure.Core v1.63.0 page). Whether `WorkloadIdentityCredential` at the repository's pin opens a new HTTP connection for the second call is not stated; DA-1 carries it.
3. `TaskCanceledException` at about 10 s after the acquisition began matches the `TokenTimeout` timer in `VerifierKafkaSession.cs` line 39. This match is inferred from the evidence reading; the evidence does not establish which token failed.
4. Confluent.Kafka v2.16.0 (the pin in `Directory.Packages.props` line 9), `ConsumerBuilder.SetOAuthBearerTokenRefreshHandler` remarks: "The SASL/OAUTHBEARER token refresh callback is triggered via IConsumer.Consume(int) (or any of its overloads) whenever OAUTHBEARER is the SASL mechanism and a token needs to be retrieved" (https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ConsumerBuilder.cs lines 249 to 264). A new consumer therefore runs its own initial callback when `InstallToken` polls it.

## 11. Open questions for Hari

- Q1. Accept DA-1 by name, or say what evidence it needs. No offline test covers it (docs/agents/factory.md "Item lifecycle" step 1, deployment assumptions).

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, the scope in section 2, or any existing test's expected value, or when a cap in section 9 is exhausted (its own action applies).

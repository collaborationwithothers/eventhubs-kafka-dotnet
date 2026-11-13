# Verifier sign-in fix contract-grade plan

Revision 4. Issue 404, pull request short name `verifier sign-in fix`. Status: not approved. Implementing tool: Claude Code ([decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192) point 2). Hari merges.

In run 43318281268 one HTTP request to the Microsoft Entra token endpoint got no response, and the verifier's 10 s `TokenTimeout` cancelled it before the Azure SDK's own retry could act, because the SDK's per-request timeout is 100 s by default. This pull request gives the Kafka credential of every verifier mode that opens a Kafka session its own retry settings: a 2.0 s per-request timeout, one retry and a fixed 0.2 s delay, so a single stalled request is abandoned and sent again well inside `TokenTimeout`. Those modes are `start-line`, `verify`, `b10-sweep-line`, `b10-sweep` and `b10-abrupt-verify` (section 2.1). No check, problem, reason, output member, configuration key, deadline or load generator behaviour changes.

## 0. Changes since Revision 3

Revision 4 follows [decision 6846551008](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846551008), paragraph "Verifier sign-in fix: option A, scope widened": the fix applies to every verifier mode that opens a Kafka session, not only `b10-abrupt-verify`; the values are unchanged; DA-1 and DA-2 accepted at 2.0 s apply to each such mode; load generator sign-in stays unchanged; Revision 4 lists the affected modes cited from source and gets one fresh plan check.

- Scope widened from one mode to five. Section 2.1 adds the table of verifier modes, each cited from source on `main` at `675f8372`: the five that open a Kafka session and the three, plus two separate hosts, that open none.
- Mechanism placement moved. The one replacing `IKafkaTokenProvider` registration is now made in the shared Kafka block of `Program.cs` (lines 151 to 165), right after `AddParchwellKafkaAuthentication`, instead of inside the `b10-abrupt-verify` branch. The provider file moves from `src/Parchwell.Verifier/VerifyRun/Abrupt/` to `src/Parchwell.Verifier/Common/Kafka/`, because five slices now use it (docs/engineering/dotnet.md lines 27 and 68). Section 3.4 states this as a contract for the other open plans of this issue.
- Requirements: R1 to R3 name the verifier Kafka provider for the five modes; R5 states each mode's existing failure path; R8 now covers the modes with no Kafka session (`judge`, `probe-gate`). The section 3.2 values and R4, R6 and R7 are unchanged.
- Test map: T1 and T2 become theories over the five modes, with each mode's literal identity values; T3 covers `judge` and `probe-gate`. T4 to T7 are unchanged. Folded from the Revision 3 check [6845555307](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845555307): P2-A (the `AZID0002` pragma around the `IsAzureProxyEnabled` read in T2) and P2-B (where the sweep-mode arguments come from), and N1 (citation `HttpClient.cs` lines 577 to 624).
- Section 3.3 (e) adds each mode's budget source; the worst-case table is unchanged, because it is per acquisition and the values are unchanged.
- Section 7: DA-1 and DA-2 are stated for each of the five modes, as decision 6846551008 applies them.
- Section 1 adds decisions 6845612916, 6846521676 and 6846551008, plan checks 6845555307 and 6846466122 as evidence, and removes Revision 3's single-mode scope reading. Sections 2.2, 2.3, 2.4, 5, 8 and 9 follow the new scope.

### Revision 3 changes since Revision 2

Revision 3 followed [decision 6845385609](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845385609): `NetworkTimeout` 2.0 s instead of 1.5 s; DA-1 and DA-2 restated at 2.0 s and accepted by name in its points 2 and 3; the section 3.3 (e) table recomputed by the same script.

### Revision 2 changes since Revision 1

Revision 2 followed [decision 6844950677](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6844950677), which did not approve Revision 1 and did not accept its DA-1. Revision 1 retried the whole Kafka client install in `B10AbruptVerifyHandler`; Revision 2 instead set `RetryOptions` on the `WorkloadIdentityCredential` that the Kafka token provider uses, answered decision point 3 (a) to (e) in section 3.3, and used the Application Insights evidence [6844977688](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6844977688).

## 1. Goal and authority

Goal, from [decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192) point 1, widened by [decision 6846551008](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846551008) to every verifier mode that opens a Kafka session: one slow token-endpoint call no longer fails the verifier Job, while the Job still ends within its existing deadline and every check and published format stays unchanged.

Authority, in creation order:
- The [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404) and the decisions listed in `items/404/state.md` on `main` at `675f8372`, which this pull request does not change.
- [Decision 6837706862](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837706862): one diagnostic trial, run 43318281268, on `main` at `675f8372`.
- [Evidence reading 6837900667](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837900667) (evidence, not a decision): in run 43318281268 a token acquisition began about 22:37:30-32Z and failed at 22:37:40Z with `TaskCanceledException`; problems were only `b10_abrupt_verify_window_incomplete`.
- [Decision 6837940546](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837940546): one further trial with no code change. It does not govern this pull request.
- [Decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192): this pull request and its goal (point 1); one session under docs/agents/factory.md "Item lifecycle" step 3 "Standing approval", Claude Code, no `results/`, expected outcome or verdict, no workflow dispatch, Hari merges (point 2); the caps in section 9 (point 3). Its scope, the `b10-abrupt-verify` mode only, is widened by decision 6846551008 below.
- [Evidence reading 6838450660](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6838450660) (evidence): rerun 43322351104 had no token failure; one run cannot show whether the timeout recurs.
- [Decision 6844950677](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6844950677): Revision 1 not approved and DA-1 not accepted (point 1); the credential's own retry settings, verifier only, load generator sign-in unchanged (point 2); the facts (a) to (e) established before proposing values (point 3); telemetry evidence first, if Hari supplies it (point 4). It is not plan approval.
- [Application Insights evidence 6844977688](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6844977688) (evidence, not a decision), for run 43318281268's verify pod: a discovery GET (105.2 ms) and two token POSTs (40.6 ms, 36.7 ms), all 200; then one `WorkloadIdentityCredential.GetToken` span of 10014.2 ms and one token POST of 10004.9 ms, failed, result code 0. Exactly one POST and one `GetToken` span: neither Azure.Core nor MSAL retried within 10 s. Its closing statement, that a per-request `NetworkTimeout` of a few seconds would have let the SDK retry inside `TokenTimeout`, is labelled there as an inference, not established. This plan does not rely on it as fact; DA-1 carries it.
- [Plan check 6845326893](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845326893) of Revision 2 (evidence, not a decision): blocking findings B1 (DA-1) and B2 (DA-2).
- [Decision 6845385609](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845385609): `NetworkTimeout` 2.0 s, with `MaxRetries` 1, `Mode` `Fixed` and `Delay` 0.2 s (point 1); DA-1 accepted by name at 2.0 s (point 2); DA-2 accepted by name at 2.0 s, with the 30-day `AppDependencies` table as evidence (point 3); approval then follows the standing approval of decision 6837975192 point 2 if the fresh check states `Blocking findings: none`.
- [Plan check 6845555307](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845555307) of Revision 3 (evidence, not a decision): `Blocking findings: none`; P2-A and P2-B marked `apply in implementation` for Revision 3 only, and notes N1 and N2. Revision 4 folds P2-A, P2-B and N1 into the plan text (section 0).
- [Decision 6845612916](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845612916): two parallel planning sessions for the scientific three-Job path and the final B10 judgement; point 3, plan branch hygiene: each planner commits only its own file on top of the remote tip of `claude/404-plan`, with no rebase or force-push, and verifies its file hash on the remote by script. This plan follows point 3 too.
- [Plan check 6846466122](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846466122) of the final judgement plan, Revision 2 (evidence, not a decision): its B6 names token acquisition in `b10-sweep-line` and in the C0 `verify` Jobs, which keep the unfixed 10 s token path under Revision 3 of this plan.
- [Decision 6846521676](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846521676): answers on the scientific three-Job plan. It does not govern this pull request.
- [Decision 6846551008](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846551008), paragraph "Verifier sign-in fix: option A, scope widened": the fix of decision 6837975192, as set by decision 6845385609, applies to every verifier mode that opens a Kafka session, including at least the start-line, `b10-sweep-line` and C0 `verify` paths of check 6846466122 B6; values unchanged (`NetworkTimeout` 2.0 s, `MaxRetries` 1, `Fixed`, `Delay` 0.2 s); DA-1 and DA-2 accepted at 2.0 s apply to each such mode; load generator sign-in unchanged; Revision 4 lists the affected modes cited from source and gets one fresh plan check; standing approval of decision 6837975192 point 2 still applies if that check states `Blocking findings: none` and open questions are `None`. Options not taken: B, a follow-up pull request; C, accept the risk. It authorizes no trial, dispatch, `results/` or expected-outcome change.

Parent items #347 and #348 have no `## Decision` or `## Approval` comment created or edited after 2026-11-12T20:28:03Z (read by script when Revision 4 was written); no parent decision is adopted for this pull request.

## 2. Scope

### 2.1 Cause in code and source (read on `main` at `675f8372` and at the pinned tags in section 10)

- `src/Parchwell.Kafka/Authentication/KafkaAuthenticationServiceCollectionExtensions.cs` lines 30 to 43: `AddParchwellKafkaAuthentication` registers `IKafkaTokenProvider` with `TryAddSingleton`, as a `WorkloadIdentityTokenProvider` over `new WorkloadIdentityCredential(new WorkloadIdentityCredentialOptions { TenantId, ClientId, TokenFilePath })` with no retry options. Lines 44 to 49: `KafkaAuthenticationFactory` takes `IKafkaTokenProvider` from the container.
- `src/Parchwell.Kafka/Authentication/WorkloadIdentityTokenProvider.cs` lines 11 to 14 and 24 to 30: `GetToken(scope, cancellationToken)` calls `credential.GetToken(new TokenRequestContext([scope]), cancellationToken)`.
- `src/Parchwell.Kafka/Authentication/KafkaOAuthBearerSession.cs` lines 221 to 233: the observed-provider path is used only when a test expiry cap is set; otherwise the session calls `IKafkaTokenProvider.GetToken(_scope, _cancellationToken)`. `KafkaAuthenticationFactory.cs` lines 80 to 92: the verifier's `CreateSession(identity, token)` sets no cap.
- `src/Parchwell.Verifier/Common/Kafka/VerifierKafkaSessionFactory.cs` lines 20 to 36: the one place the verifier builds a Kafka client. It creates the OAUTHBEARER session through `KafkaAuthenticationFactory.CreateSession` (line 29), the consumer (lines 30 to 34) and a dependent admin client on the consumer's handle (line 35). A script search of `src/Parchwell.Verifier` on `main` for `ConsumerBuilder`, `ProducerBuilder`, `AdminClientBuilder` and `AddParchwellKafkaAuthentication` finds no other client builder and no other authentication registration (`Program.cs` line 158 is the only registration).
- `src/Parchwell.Verifier/Common/Kafka/VerifierKafkaSession.cs` lines 35 to 62: `InstallToken` cancels the session's acquisition token after `TokenTimeout`. `charts/parchwell/values.yaml` lines 62 and 63: `verifier_kafka` `TokenTimeout: 00:00:10`. `charts/parchwell/templates/jobs.yaml` lines 162 to 166 pass every `verifier_kafka` bound to every verifier Job whose mode is not `judge`.
- Azure.Core 1.63.0 (the pin in `Directory.Packages.props` line 15; it carries the `Azure.Identity` credentials): `RetryOptions.NetworkTimeout` defaults to 100 s (section 3.3), so the SDK cannot abandon a stalled request before the 10 s `TokenTimeout` cancels the caller's token. The caller-cancelled request is not retried (section 3.3 (b)). This matches the evidence: one POST, cancelled at about 10 s.

Verifier modes on `main` at `675f8372`. `Program.RegisterSelectedMode` (`src/Parchwell.Verifier/Program.cs` lines 108 to 237) selects by `Verifier:Mode`; the valid names are the eight of `VerifierHostOptions.cs` lines 151 to 162. The shared block at `Program.cs` lines 151 to 165 registers `AddParchwellKafka`, `AddParchwellKafkaAuthentication` and `VerifierKafkaSessionFactory` for exactly `start-line`, `verify`, `b10-sweep-line`, `b10-sweep` and `b10-abrupt-verify` (line 151).

| Mode | Handler registration (`Program.cs`) | Opens a Kafka session | Source of the session |
|---|---|---|---|
| `start-line` | lines 174 to 178, `StartLineHandler` | Yes | `src/Parchwell.Verifier/StartLine/StartLineHandler.cs` line 30, `session.InstallToken` |
| `b10-sweep-line` | lines 179 to 183, `B10SweepLineHandler` | Yes | `src/Parchwell.Verifier/StartLine/B10SweepLineHandler.cs` line 26, `session.InstallToken` |
| `b10-sweep` | lines 184 to 193, `B10SweepHandler` | Yes | `src/Parchwell.Verifier/VerifyRun/B10SweepHandler.cs` line 58, `session.InstallToken` |
| `b10-abrupt-verify` | lines 194 to 208, `B10AbruptVerifyHandler` | Yes | `src/Parchwell.Verifier/VerifyRun/Abrupt/B10AbruptVerifyHandler.cs` line 154, `session.InstallToken` |
| `verify` (every configuration's verify Job, C0 included) | lines 223 to 236, `VerifyRunHandler`, the only mode left after line 143 and the earlier returns | Yes | `src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs` line 121, `sessions.Create` then `session.InstallToken` |
| `judge` | lines 213 to 222, `JudgeHandler` | No | Passes line 143 but not line 151, so no Kafka authentication is registered |
| `probe-gate` | lines 111 to 126, `ProbeGateHandler` | No | Returns before line 143 |
| `single-action-judge` | lines 127 to 142, `SingleActionHandler` | No | Returns before line 143 |
| Hosts `--idle-observation-judge` and `--single-action-v2-judge` | `Program.cs` lines 39 to 46 | No | `IdleObservationJudgeHandler.cs` lines 30 to 38 and `ProbeV2Handler.cs` lines 49 to 57 call `AddParchwellKafka` on a private `ServiceCollection` only to resolve configuration; they register no authentication and build no client |

So the five affected modes are `start-line`, `verify`, `b10-sweep-line`, `b10-sweep` and `b10-abrupt-verify`. They include the start-line, `b10-sweep-line` and C0 `verify` paths that decision 6846551008 names.

### 2.2 Files allowed to change

- `src/Parchwell.Verifier/Program.cs`: inside the shared Kafka block `if (mode is "start-line" or "verify" or "b10-sweep-line" or "b10-sweep" or "b10-abrupt-verify")` only (lines 151 to 165 at `675f8372`), the one registration of R1, after the `AddParchwellKafkaAuthentication` call. Nothing else in the file changes.
- One new internal file under `src/Parchwell.Verifier/Common/Kafka/`, namespace `Parchwell.Verifier.Common.Kafka`, holding the verifier Kafka token provider and its credential options (R2, R3). Its file name, type name and code shape are the implementer's.
- New test files: one under `tests/Parchwell.Verifier.Tests/Host/` for T1 to T3, and one under `tests/Parchwell.Verifier.Tests/Host/` or a new folder `tests/Parchwell.Verifier.Tests/Authentication/` for T4 to T7.
- `items/404/state.md` (docs/agents/factory.md "Item state file") and a new receipt `items/404/verifier-signin-fix-receipt.md`.

### 2.3 Paths that must not change

- `src/Parchwell.Kafka/**`, `src/Parchwell.LoadGen/**`, `src/Parchwell.Enrichment/**`, every file under `src/Parchwell.Verifier/Common/` that exists at `675f8372` (including `VerifierKafkaSession.cs` and `VerifierKafkaSessionFactory.cs`), `src/Parchwell.Verifier/VerifierHostOptions.cs`, `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs`, `src/Parchwell.Verifier/VerifierWorker.cs`, every file under `src/Parchwell.Verifier/VerifyRun/` and `src/Parchwell.Verifier/StartLine/`, and every other mode handler.
- `charts/**`, `docs/operations/**` (including `b10-abrupt-contract.md`, `verifier-contract.md`, `chart-contract.md` and `a01-runtime-limits.json`), `docs/blueprint.md`.
- `.github/workflows/**`, `docs/operations/workflows/**`, `infra/**`, `scripts/**`, `results/**`, every `expected.yaml`, every `*.csproj`, `Directory.*.props`, every `packages*.lock.json`, `*.sln*`.
- Every existing file under `tests/**`, including `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`.

### 2.4 Protected paths, CI and existing tests

- `src/Parchwell.Verifier/**` and `tests/Parchwell.Verifier.Tests/**` match `Parchwell.Verifier*/` in `.github/CODEOWNERS` line 16, so Hari merges.
- The `mutate` list of `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` at `675f8372` (lines 17 to 35) has neither `Program.cs` nor any file under `Common/Kafka/`; the one listed handler among the five modes, `VerifyRun/B10SweepHandler.cs` (line 32), does not change. Mutation acceptance does not apply.
- `SourceNamespaceRules` (`tests/Parchwell.ArchitectureTests/SourceNamespaceRules.cs` lines 32 to 57) requires each type's namespace to match its folder, so the new file's namespace is `Parchwell.Verifier.Common.Kafka`. docs/engineering/dotnet.md line 27 puts behaviour shared by two or more slices in the service's `Common/` folder, and line 68 bars a type in one slice folder from referencing another slice's types; five slices use the provider, so it goes under `Common/Kafka/`, beside `VerifierKafkaSessionFactory`.
- No new project, assembly reference or package: `WorkloadIdentityCredential`, `WorkloadIdentityCredentialOptions`, `RetryOptions`, `RetryMode` and `HttpClientTransport` are in Azure.Core 1.63.0, which both `Parchwell.Verifier` and `Parchwell.Verifier.Tests` already resolve (`src/Parchwell.Verifier/packages.lock.json`; `Program.cs` lines 241 and 263 already construct `WorkloadIdentityCredential`).
- Existing test overrides of the provider are registered later, through `configureForTests` (`Program.cs` line 103), with `AddSingleton` (`tests/Parchwell.Verifier.Tests/Component/VerifierOAuthComponentTests.cs` line 38; `tests/Parchwell.Verifier.Tests/Support/KafkaSessionTestSupport.cs` line 20), so they still win. A script search of `tests/Parchwell.Verifier.Tests` and `tests/Parchwell.ArchitectureTests` on `main` finds no assertion on the type of the registered `IKafkaTokenProvider`.
- This plan adds no check and tightens none, so `plan-pr` plan rule 2 (standing fixture rule) does not apply. No existing test changes. If an existing test fails, that is a stop under section 12, not a fixture change.

## 3. External contracts

### 3.1 Unchanged

Every mode name, option name and value, problem and reason literal, output envelope and member, check numbering and text, exit code, `TokenTimeout` 00:00:10, every mode's `ProcessLifetime` and every Job deadline (`charts/parchwell/values.yaml` lines 70 to 100, including `verify_job_seconds` 750 and `start_line_job_seconds` 150) are unchanged. No configuration key, chart value, environment variable or log line is added or pinned. The credential's tenant ID, client ID and token file path are the same values `AddParchwellKafkaAuthentication` receives (`Program.cs` lines 158 to 163). Load generator and enrichment keep the Parchwell.Kafka credential with Azure.Core defaults. The verifier modes with no Kafka session (section 2.1) are unchanged. The verifier's telemetry and blob credentials (`Program.cs` lines 241 to 246 and 263 to 272) are unchanged.

### 3.2 Pinned retry settings (policy, not measured)

The verifier Kafka credential, used by the five modes of section 2.1, is a `WorkloadIdentityCredential` whose `WorkloadIdentityCredentialOptions.Retry` has exactly:

| Setting | Value | Default it replaces |
|---|---|---|
| `NetworkTimeout` | `00:00:02` | `00:01:40` |
| `MaxRetries` | `1` | `3` |
| `Mode` | `RetryMode.Fixed` | `RetryMode.Exponential` |
| `Delay` | `00:00:00.200` | `00:00:00.800` |
| `MaxDelay` | `00:01:00`, unchanged | `00:01:00` |

`RetryPolicy` stays null, so these values build the standard `RetryPolicy` (section 3.3). The values are constants in the new file, with a comment naming decisions 6844950677, 6845385609 and 6846551008; they are not configuration. This table is the one statement of the values; other files cite it.

Why these values: Hari chose `NetworkTimeout` 2.0 s in decision 6845385609 point 1, and kept `MaxRetries` 1, `Mode` `Fixed` and `Delay` 0.2 s from Revision 2; decision 6846551008 keeps all four for every mode. In the 30-day Application Insights query that decision 6845385609 gives as evidence, no healthy call to the token endpoint took over 1500 ms, and only the stalled request of run 43318281268 took over 2000 ms (section 7). Two attempts of every request in a first acquisition still end 1.6 s before `TokenTimeout` (section 3.3 (e)). `Fixed` mode keeps each delay at or below `Delay` exactly. The values are policy; none is a measured optimum.

### 3.3 Facts required by decision 6844950677 point 3

Sources are in section 10. Line numbers are at Azure SDK for .NET tag `Azure.Core_1.63.0` (commit `117f784c4102d93b65978d3b3d72de36c8824105`, the commit Microsoft Learn's `RetryOptions` page links for Azure.Core v1.63.0), MSAL.NET tag `4.90.0` (commit `c34d5b49f88808479661267d142ae3ff02d6027f`; the image loads MSAL 4.90.0, `src/Parchwell.Verifier/packages.lock.json`) and dotnet/runtime tag `v10.0.13` (the verifier image base is `aspnet:10.0.13`, `src/Parchwell.Verifier/Parchwell.Verifier.csproj` line 8). These facts do not depend on the verifier mode: every one of the five modes acquires its token through the same `VerifierKafkaSessionFactory` and `IKafkaTokenProvider` (section 2.1).

How the settings reach the token call: `WorkloadIdentityCredential` clones its options into `ClientAssertionCredentialOptions`, and the clone copies `Retry.MaxRetries`, `Delay`, `MaxDelay`, `Mode` and `NetworkTimeout` and a non-default `Transport` (`WorkloadIdentityCredential.cs` lines 50 to 68; `TokenCredentialOptions.cs` lines 119 to 143). `ClientAssertionCredential` builds a `CredentialPipeline` from those options (`ClientAssertionCredential.cs` lines 56 and 57; `CredentialPipeline.cs` lines 18 to 23, 31 to 39). `MsalConfidentialClient` gives MSAL an `HttpPipelineClientFactory` over that pipeline (`MsalConfidentialClient.cs` line 95), whose `HttpClient` sends every MSAL request through the Azure.Core pipeline (`HttpPipelineClientFactory.cs` lines 30 to 33; `HttpPipelineMessageHandler.cs` lines 30 to 37). This agrees with run 43318281268's stack trace through `RetryPolicy` and `ResponseBodyPolicy` under MSAL's `HttpManager` (decision 6844950677 point 2). `HttpPipelineBuilder.cs` places `RetryPolicy` (lines 157 to 164) before `ResponseBodyPolicy` (line 186), so the network timeout applies to each attempt, not to the whole call.

(a) Minimum allowed values.
- `RetryOptions` has plain auto-properties with no range check (`RetryOptions.cs` lines 74 to 97). Confirmed.
- `NetworkTimeout`: `ResponseBodyPolicy` passes it to `CancellationTokenSource.CancelAfter` (`ResponseBodyPolicy.cs` line 47). That method throws `ArgumentOutOfRangeException` when the delay is less than -1 ms or greater than `Int32.MaxValue` ms (or `UInt32.MaxValue - 1` on some versions) (Microsoft Learn, `CancelAfter(TimeSpan)`). So the lower bound enforced elsewhere is -1 ms, which is `Timeout.InfiniteTimeSpan`, meaning no timeout (`ResponseBodyPolicy.cs` lines 81 and 113 treat it so). `TimeSpan.Zero` is allowed and cancels every attempt at once, so the smallest useful value is any positive value. The same value restarts before each read of a buffered response body (`ResponseBodyPolicy.cs` line 126).
- `MaxRetries`: `RetryPolicy` retries while `message.RetryNumber < _maxRetries` (`RetryPolicy.cs` line 200). No check; 0 or any negative value means no retry.
- `Delay`: no check. A computed delay is waited only when greater than zero (`RetryPolicy.cs` line 126), so 0 or a negative value means no wait.
- `Mode`: `Exponential` or `Fixed` (`HttpPipelineBuilder.cs` lines 162 to 164).
- The proposed values are above every bound.

(b) Retry after a `NetworkTimeout` expiry. When the per-attempt timer fires, `ResponseBodyPolicy` throws a `TaskCanceledException` naming `ClientOptions.Retry.NetworkTimeout` and restores the caller's token on the message (`ResponseBodyPolicy.cs` lines 60 to 68, 168 to 180; `CancellationHelper.cs` line 36). `RetryPolicy` catches every exception and asks `ResponseClassifier.IsRetriable` (`RetryPolicy.cs` lines 88 to 120, 198 to 205). `IsRetriable` is true for `IOException`, for `RequestFailedException` with status 0, and for any `OperationCanceledException` while the caller's token is not cancelled (`ResponseClassifier.cs` lines 39 to 53). So a network-timeout expiry is retried; a cancellation by the caller's token (the verifier's `TokenTimeout`) is not. Responses with status 408, 429, 500, 502, 503 or 504 are retried too (lines 20 to 34). When more than one attempt failed with an exception, `RetryPolicy` throws an `AggregateException` (lines 149 to 160).

(c) Delay formula, jitter and `Retry-After`. `DelayStrategy.GetNextDelay` returns `Max(retryAfter, Min(jittered, MaxDelay))`, where the jitter factor is uniform between 0.8 and 1.2 (`DelayStrategy.cs` lines 14, 27 to 34, 75 to 96). `Exponential` gives `2^(n-1) * Delay` for retry `n` (`ExponentialDelayStrategy.cs` lines 18 and 19). `Fixed` gives `Delay` and passes `Delay` itself as the maximum (`FixedDelayStrategy.cs` lines 12 and 17), so a fixed delay lies between 0.8 and 1.0 times `Delay`: 0.16 s to 0.2 s here. `retryAfter` comes from a response's `retry-after-ms`, `x-ms-retry-after-ms` or `Retry-After` header (`ResponseHeaders.cs` lines 17 to 19, 81 to 105), so a token-endpoint response with such a header can lengthen a delay beyond `Delay` and `MaxDelay`. The wait is cancelled by the caller's token (`RetryPolicy.cs` lines 128 to 135, 168 to 176), so `TokenTimeout` still ends it. A timeout has no response, so no `Retry-After` applies to it.

(d) MSAL's own retry. MSAL 4.90.0's `HttpManager` retries only when internal retries are enabled (`HttpManager.cs` lines 117 to 141). `MsalConfidentialClient` calls `WithHttpClientFactory(factory, false)` (`MsalConfidentialClient.cs` line 95); the second argument is `retryOnceOn5xx`, and `false` sets `DisableInternalRetries` true (`BaseAbstractApplicationBuilder.cs` lines 85 to 90), which reaches the `HttpManager` (`ServiceBundle.cs` lines 32 to 35; `HttpManager.cs` lines 48 to 55). So with this credential MSAL does not retry, and the attempts do not multiply. Had it been enabled, its STS policy would retry once after 1000 ms (`DefaultRetryPolicy.cs` lines 14, 17, 34 to 37). MSAL's instance-discovery call never retries in any case (`NetworkMetadataProvider.cs` lines 93 to 103, 143 to 154). Instance discovery results are cached in a static cache for the process (`NetworkCacheMetadataProvider.cs` line 14), and a failed discovery that the caller did not cancel lets MSAL continue with known metadata (`InstanceDiscoveryManager.cs` lines 205 to 218). In `HttpClient`, an exception from the handler that is neither cancellation nor `HttpRequestException` passes through unchanged, and a cancellation not caused by the caller or the client's own timeout is not rewritten (`HttpClient.cs` lines 577 to 624 at `v10.0.13`).

(e) Worst case, computed by script (`python3 -I` on the script in the receipt; output below). Configured values only; not measured. One HTTP request through the pipeline takes at most `(MaxRetries + 1) * NetworkTimeout + MaxRetries * Delay`. A later acquisition sends one token POST. The first acquisition in the process may also send one instance-discovery GET before it, so it takes at most twice that. The bound is per acquisition and holds in each of the five modes, because each acquisition runs under the same `TokenTimeout` (section 2.1).

| Settings | Attempts per request | Later acquisition (s) | First acquisition (s) | Margin to `TokenTimeout` 10 s |
|---|---|---|---|---|
| Pinned: 2.0 s, 1, Fixed, 0.2 s | 2 | 4.2 | 8.4 | 1.6 s |
| Revision 2: 1.5 s, 1, Fixed, 0.2 s | 2 | 3.2 | 6.4 | 3.6 s |
| 1.0 s, 1, Fixed, 0.2 s | 2 | 2.2 | 4.4 | 5.6 s |
| 1.0 s, 2, Fixed, 0.2 s | 3 | 3.4 | 6.8 | 3.2 s |
| Defaults: 100 s, 3, Exponential, 0.8 s | 4 | 406.72 | 813.44 | none |

The script used for this table, run with `python3 -I` on 2026-11-13, is below. The implementer copies it and its output into the receipt (section 8). For `Exponential` it takes the upper jitter factor 1.2 on each delay; for `Fixed` the delay is at most `Delay` (section 3.3 (c)).

```python
from decimal import Decimal as D
T = D("10")  # TokenTimeout seconds
rows = [
    ("Pinned: 2.0 s, 1, Fixed, 0.2 s", D("2.0"), 1, "fixed", D("0.2")),
    ("Revision 2: 1.5 s, 1, Fixed, 0.2 s", D("1.5"), 1, "fixed", D("0.2")),
    ("1.0 s, 1, Fixed, 0.2 s", D("1.0"), 1, "fixed", D("0.2")),
    ("1.0 s, 2, Fixed, 0.2 s", D("1.0"), 2, "fixed", D("0.2")),
    ("Defaults: 100 s, 3, Exponential, 0.8 s", D("100"), 3, "exp", D("0.8")),
]
J = D("1.2")  # upper jitter for Exponential; Fixed caps at Delay (factor 1.0)
for name, nt, r, mode, d in rows:
    if mode == "fixed":
        delays = d * r
    else:
        delays = sum((2 ** (n - 1)) * d * J for n in range(1, r + 1))
    per = (r + 1) * nt + delays
    later, first = per, 2 * per
    margin = T - first
    print(f"| {name} | {r+1} | {later.normalize():f} | {first.normalize():f} | " + (f"{margin.normalize():f} s |" if margin >= 0 else "none |"))
```

The bound holds when each attempt ends within `NetworkTimeout`: a response with a `Retry-After` header (c), or a body delivered in many slow reads (a), can take longer. In those cases, and in every other case, `TokenTimeout` still cancels the call at 10 s, exactly as today. Each mode's configured budgets are unchanged. `VerifierHostOptionsValidator` adds `TokenTimeout` to the required budgets of `start-line`, `verify`, `b10-sweep-line` and `b10-sweep` (lines 66 to 68) and checks their sum against `ProcessLifetime` (line 157); for `b10-abrupt-verify`, `ValidateAbrupt` collects `TokenTimeout` 10, `MetadataTimeout` 10, `WatermarkTimeout` 10, `BlobIoTimeout` 30, `IoTimeout` 10, `CleanupTimeout` 10 and `SafetyStop` 600 seconds (lines 237 to 241), 680 s in all, below `ProcessLifetime` 720 s and the Job deadline 750 s. This pull request adds no session and no wait in any mode, so every such sum and every process watchdog's end at `ProcessLifetime` are unchanged.

### 3.4 Contract for the other open plans of this issue

The final judgement plan (Revision 2, `51a34df2`) plans a new mode `b10-final-sweep` that registers this fix's token provider, and refers to that provider under `VerifyRun/Abrupt/` as Revision 3 of this plan placed it. From Revision 4:
- The provider is an internal type in namespace `Parchwell.Verifier.Common.Kafka`, in a new file under `src/Parchwell.Verifier/Common/Kafka/`.
- It is registered once, in the shared Kafka block of `Program.cs` (lines 151 to 165 at `675f8372`), after `AddParchwellKafkaAuthentication`. A mode added later to that block's mode list resolves the same provider with the section 3.2 values without a registration of its own.
- The `b10-abrupt-verify` branch of `Program.cs` (lines 194 to 208) does not change in this pull request.

This plan does not decide how another plan uses this contract; each plan check of those plans checks it against this plan's current revision (decision 6845612916 point 2).

## 4. Requirements

- R1. WHEN the verifier host is built with `Verifier:Mode` `start-line`, `verify`, `b10-sweep-line`, `b10-sweep` or `b10-abrupt-verify`, THEN the service collection has exactly one `IKafkaTokenProvider` registration, and it resolves to the verifier Kafka provider of R2. The registration is made inside the shared Kafka block of `Program.cs`, after `AddParchwellKafkaAuthentication`; registrations made later by `configureForTests` still take precedence, as today.
- R2. WHEN the verifier Kafka provider's `GetToken(scope, cancellationToken)` is called, THEN it returns `credential.GetToken(new TokenRequestContext([scope]), cancellationToken)` of one `WorkloadIdentityCredential` created once per provider, as `WorkloadIdentityTokenProvider` does. Exceptions propagate unchanged.
- R3. WHEN the provider's credential is created, THEN its `WorkloadIdentityCredentialOptions` carry `TenantId`, `ClientId` and `TokenFilePath` equal to the registered `KafkaWorkloadIdentityOptions` values, the `Retry` values of section 3.2, a null `RetryPolicy`, `IsAzureProxyEnabled` false, and no other property changed from its default. An internal seam lets a test supply a `Transport` on those same options; it cannot change the retry values.
- R4. WHEN one token POST gets no response within `NetworkTimeout` and the caller's token is not cancelled, THEN the credential sends exactly one more POST after a delay of 0.16 s to 0.2 s, and returns that POST's token if it succeeds.
- R5. WHEN both POST attempts get no response, THEN `GetToken` throws after exactly two POSTs, before the caller's token is cancelled. In each of the five modes, that mode's existing handling of a failed token installation follows unchanged, at the call sites of section 2.1 (for `b10-abrupt-verify`, problem 5 `b10_abrupt_verify_window_incomplete`).
- R6. WHEN the token endpoint answers 5xx to both attempts, THEN `GetToken` throws after exactly two POSTs; MSAL adds no further attempt.
- R7. WHEN the caller's token is cancelled during an attempt, THEN no further POST is sent and `GetToken` throws.
- R8. WHEN the verifier host is built for `judge` or `probe-gate`, THEN the service collection has no `IKafkaTokenProvider` registration, as today.

## 5. Invariants and safety

- The verifier stays the oracle: no check, problem, reason or output changes in any mode. A retry changes only whether a token arrives within `TokenTimeout`.
- `src/Parchwell.Kafka/**` and every load generator file are unchanged, so the client under test signs in exactly as before (decisions 6844950677 point 2 and 6846551008).
- The same identity (tenant, client ID, token file) signs in; no new identity, scope, audience or network exposure.
- No new configuration key, chart value, schema entry, environment variable, project, package or assembly reference.
- No workflow dispatch, trial, `results/`, expected outcome or verdict change ([decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192) point 2; decision 6846551008).

## 6. Test map

T1 to T3 build the host through `Program.BuildHost` and count descriptors through its `configureForTests` callback. Each mode uses the valid arguments an existing test already uses for it, each pair formatted as `--{Key}={Value}`. A shared helper for those arguments may be added in the new test file; existing test files do not change.
- `b10-abrupt-verify`: the key-value pairs that `tests/Parchwell.Verifier.Tests/Component/B10AbruptVerifyHarness.cs` sets (identity keys at lines 132 and 133 at `675f8372`, default `runId` `synthetic`), copied into the new test file when the harness itself needs Docker. They include `TenantId` `synthetic-tenant`, `WorkloadIdentityClientId` `synthetic-client` and `FederatedTokenFilePath` `/tmp/synthetic-token`.
- `start-line` and `verify`: `VerifierHostTestOptions.ArgumentsFor(mode, <temporary path>)` with `--Verifier:ScenarioRoot=<temporary directory>` appended, as `tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` lines 628 and 629 do. They include `TenantId` `tenant-id`, `WorkloadIdentityClientId` `workload-client-id` and `FederatedTokenFilePath` `/tmp/token` (`tests/Parchwell.Verifier.Tests/Support/VerifierHostTestOptions.cs` lines 129 to 131).
- `b10-sweep-line`: `B10SweepLineOptionsTests.Configuration(B10SweepLineOptionsTests.CaptureOptions("C1"))` (internal static, `tests/Parchwell.Verifier.Tests/Host/B10SweepLineOptionsTests.cs` lines 187 and 203).
- `b10-sweep`: `SweepOptions()` and the private `Configuration(options)` of `tests/Parchwell.Verifier.Tests/Host/B10SweepOptionsTests.cs` (lines 236 to 255 and 279 onward), copied unchanged into the new file.
- `judge`: `JudgeOptions()` of the same file (lines 257 to 277), copied unchanged with `SweepOptions()` and `Configuration(options)`.
- `probe-gate`: `VerifierHostTestOptions.ArgumentsFor("probe-gate", <temporary path>)` with `--Verifier:ScenarioRoot=<temporary directory>` appended, as `VerifierHostTests.cs` lines 615 and 616 do.
- The sweep modes take their identity from `VerifierHostTestOptions.ValidOptions` (lines 38 to 40): `TenantId` `tenant-id`, `WorkloadIdentityClientId` `workload-client-id`, `FederatedTokenFilePath` `/tmp/token`.

T2 reads `IsAzureProxyEnabled`, which is `[Experimental("AZID0002")]` in Azure.Core 1.63.0 (`WorkloadIdentityCredentialOptions.cs` lines 53 and 54). In the new test file only, the read is wrapped as `#pragma warning disable AZID0002 // IsAzureProxyEnabled is experimental in Azure.Core 1.63.0`, then `Assert.False(options.IsAzureProxyEnabled);`, then `#pragma warning restore AZID0002`.

T4 to T7 call the verifier Kafka provider of R2, built through the R3 seam with a `Transport` of `new HttpClientTransport(new HttpClient(handler))`, where `handler` is a test `HttpMessageHandler`. Their common inputs: tenant `11111111-1111-1111-1111-111111111111`, client ID `22222222-2222-2222-2222-222222222222` (both synthetic), a temporary token file containing `synthetic-assertion`, scope `https://eventhubs.azure.net/.default`, and a caller token cancelled after 10 s (the `TokenTimeout` value). The handler answers every GET at once with 200 and a body the implementer chooses (MSAL accepts it or continues with known metadata under section 3.3 (d)). It counts POSTs whose path ends `/oauth2/v2.0/token` and answers them as each test scripts. A successful answer is 200 with JSON `{"token_type":"Bearer","expires_in":3600,"access_token":"<token>"}`. "Blocks" means the handler waits until its own cancellation token is cancelled, then throws `TaskCanceledException` for that token. Elapsed time is measured with `Stopwatch` around `GetToken`. T4 to T7 use real time and carry trait `Tier` = `Slow`.

| Test | Requirement | Inputs | Expected |
|---|---|---|---|
| T1 | R1 | Theory over modes `start-line`, `verify`, `b10-sweep-line`, `b10-sweep`, `b10-abrupt-verify`. | Exactly 1 `IKafkaTokenProvider` descriptor; the resolved instance is the R2 provider type, whose namespace is `Parchwell.Verifier.Common.Kafka`. |
| T2 | R3 | The same five modes; the resolved provider's credential options, read through the R3 seam, in the T1 host. | For every mode: `Retry.NetworkTimeout` `00:00:02`; `Retry.MaxRetries` 1; `Retry.Mode` `Fixed`; `Retry.Delay` `00:00:00.200`; `Retry.MaxDelay` `00:01:00`; `RetryPolicy` null; `IsAzureProxyEnabled` false. For `b10-abrupt-verify`: `TenantId` `synthetic-tenant`, `ClientId` `synthetic-client`, `TokenFilePath` `/tmp/synthetic-token`. For the other four: `TenantId` `tenant-id`, `ClientId` `workload-client-id`, `TokenFilePath` `/tmp/token`. |
| T3 | R8 | Theory over modes `judge`, `probe-gate`. | 0 `IKafkaTokenProvider` descriptors. |
| T4 | R2, R4 | POST 1 blocks. POST 2 answers 200 with `access_token` `synthetic-2`. | Returns a token whose `Token` is `synthetic-2`; POST count 2; elapsed at least 1.9 s and less than 10 s; the caller token is not cancelled on return. |
| T5 | R5 | Every POST blocks. | Throws; POST count 2; elapsed at least 3.9 s and less than 10 s; the caller token is not cancelled on return; POST count is still 2 after a further 2 s. |
| T6 | R6 | Every POST answers 503 at once, with no `Retry-After` header. | Throws; POST count 2; POST count is still 2 after a further 2 s. |
| T7 | R7 | POST 1 blocks. The caller token is cancelled 0.5 s after `GetToken` starts. | Throws; POST count 1; POST count is still 1 after a further 2 s. |

Lower time bounds come from section 3.3: T4 is at least one `NetworkTimeout` (2.0 s) less 0.1 s of timer tolerance; T5 is two `NetworkTimeout`s (4.0 s) less 0.1 s. The 0.1 s tolerance is a test choice, not a measurement. The lower delay bound 0.16 s is not asserted, because a timer test cannot separate it reliably from scheduling noise. R5's per-mode handling is existing behaviour that this pull request does not change; the existing tests of each handler cover it and pass unchanged.

Every existing test in every test project passes unchanged.

## 7. Deployment assumptions

None is covered by an offline test. Both are accepted by name, at 2.0 s, in [decision 6845385609](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845385609) points 2 and 3, and [decision 6846551008](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846551008) applies both to each of the five modes of section 2.1: `start-line`, `verify`, `b10-sweep-line`, `b10-sweep` and `b10-abrupt-verify`.

- DA-1. In the deployed verifier pod of each of the five modes, after a token POST gets no response within 2.0 s, the one retry gets a response within 2.0 s. Accepted by name in decision 6845385609 point 2 and applied to each mode by decision 6846551008. T4 shows only that the credential sends the retry. Evidence: run 43318281268 ([6844977688](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6844977688)), `b10-abrupt-verify` only: one fresh pod at start, idle apart from the verifier's own work (about 30 blob reads between 22:37:28.26 and 22:37:30.83). One POST got no response in 10 s (result code 0), while two POSTs 2.6 s earlier took 40.6 ms and 36.7 ms and storage calls before and after succeeded. Whether the earlier calls used an already-open or a fresh connection is not recorded. Whether the stalled request's connection is closed when it is abandoned, and whether the retry uses a new connection, is not established from the evidence or from the sources read. Run 43322351104 had no token failure ([6838450660](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6838450660)). No evidence was read for a stall in the other four modes. The 30-day table under DA-2 does not show whether any request was retried.
- DA-2. In the deployed verifier pod of each of the five modes, a healthy token POST or instance-discovery GET completes within 2.0 s. Accepted by name in decision 6845385609 point 3 and applied to each mode by decision 6846551008. If it does not, a call that would have succeeded in 2.0 s to 10 s today fails after two attempts, and the mode takes its existing failed-installation path (R5) where it does not today. Evidence, as decision 6845385609 gives it: Application Insights `AppDependencies` in the persistent Log Analytics workspace, last 30 days, `Target == "login.microsoftonline.com"`, all pods (identifiers omitted). The table does not separate verifier modes, pods, or connection state (fresh or already open), and does not say whether the namespace was idle or under load at each call.

| Call | Calls | P50 (ms) | P99 (ms) | P99.9 (ms) | Max (ms) | Over 1500 ms | Over 2000 ms | Failed |
|---|---|---|---|---|---|---|---|---|
| GET (instance discovery) | 515 | 109.9 | 172.8 | 193.8 | 193.8 | 0 | 0 | 0 |
| POST (token) | 1057 | 76.2 | 349.4 | 1207.0 | 10004.9 | 1 | 1 | 1 |

The decision states that the single POST over 1500 ms is the stalled request of run 43318281268, and that no healthy call exceeded 1500 ms.

## 8. Acceptance commands

- `./scripts/verify-dotnet.sh`: the full gate passes with warnings as errors, including T1 to T7 and every existing test. Evidence: the command's summary line in the receipt.
- `./scripts/verify-dotnet.sh --project Parchwell.Verifier.Tests` run first while iterating; it is not the full gate.
- `git diff --name-only origin/main...HEAD` lists only paths in section 2.2. Evidence: the list in the receipt.
- A script check that `src/Parchwell.Verifier/Program.cs` differs from `main` only inside the shared Kafka block (lines 151 to 165 at `675f8372`). Evidence: the script and its output in the receipt.
- The section 3.3 (e) script run with `python3 -I`, its text and output copied into the receipt.

## 9. Caps and plan-check answers

Caps ([decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192) point 3): 20 verification-failure root causes and 20 review-finding root causes, each with exhaustion action `push -wip and hand off to the next session` (AGENTS.md "Before opening a pull request" rule 11).

(a) Steps whose effort scales with a number unknown at planning time: verification-failure root causes and review-finding root causes, capped as above. Test count is fixed at seven tests: T1 and T2 with five cases each, T3 with two.

(b) Stricter than the parent plan: the item plan (Revision 5) and the decisions before 6837975192 say nothing about token acquisition. This plan sets retry values for the verifier's Kafka credential in the five modes that use it and changes no parent rule. It is not stricter.

(c) Tests or checks that depend on code, data or behaviour neither on `main` nor in the pull request: (1) the token endpoint's live behaviour on a retry, DA-1, and its healthy latency, DA-2, at 2.0 s, in each of the five modes; neither is settled by source, and the recorded output (run 43318281268 and the 30-day table in section 7) does not settle DA-1. Both are accepted by name in decision 6845385609 points 2 and 3, and applied to each mode by decision 6846551008. (2) Azure.Core 1.63.0 and MSAL 4.90.0 behaviour, which T4 to T7 exercise through the real packages already resolved by the test project, and which section 3.3 reads at their pinned sources. (3) `HttpClient` behaviour at runtime 10.0.13 in the image, read at its source (section 3.3 (d)); the test host runs the SDK in `global.json` (10.0.402), whose runtime patch is not checked against 10.0.13 here. (4) The chart passes `TokenTimeout` 00:00:10 to every non-`judge` verifier Job (`jobs.yaml` lines 162 to 166); this is source on `main`, unchanged here.

## 10. Sources

Documentation tools used: Microsoft Learn MCP (`microsoft_docs_fetch`), and source read at pinned tags by `git clone --depth 1 --filter=blob:none --sparse` and `curl` of raw files, on 2026-11-13. Revision 4 adds only repository source on `main` at `675f8372`, read with `git show` and `git grep`.

1. Microsoft Learn, `RetryOptions` class, Azure.Core v1.63.0: properties `Delay`, `MaxDelay`, `MaxRetries`, `Mode`, `NetworkTimeout` ("The timeout applied to individual network operations"), source link at commit `117f784c4102d93b65978d3b3d72de36c8824105`. https://learn.microsoft.com/en-us/dotnet/api/azure.core.retryoptions?view=azure-dotnet
2. Microsoft Learn, `CancellationTokenSource.CancelAfter(TimeSpan)`, .NET 10: `ArgumentOutOfRangeException` when "`delay.TotalMilliseconds` is less than -1 or greater than Int32.MaxValue (or UInt32.MaxValue - 1 on some versions of .NET)". https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokensource.cancelafter?view=net-10.0
3. Azure SDK for .NET, tag `Azure.Core_1.63.0`, `sdk/core/Azure.Core/src/`: `RetryOptions.cs`, `ResponseClassifier.cs`, `DelayStrategy.cs`, `ExponentialDelayStrategy.cs`, `FixedDelayStrategy.cs`, `ResponseHeaders.cs`, `Pipeline/RetryPolicy.cs`, `Pipeline/HttpPipelineBuilder.cs`, `Pipeline/Internal/ResponseBodyPolicy.cs`, `Shared/CancellationHelper.cs`, `Identity/CredentialPipeline.cs`, `Identity/HttpPipelineClientFactory.cs`, `Identity/HttpPipelineMessageHandler.cs`, `Identity/MsalConfidentialClient.cs`, `Identity/Credentials/WorkloadIdentityCredential.cs`, `Identity/Credentials/WorkloadIdentityCredentialOptions.cs`, `Identity/Credentials/ClientAssertionCredential.cs`, `Identity/Credentials/TokenCredentialOptions.cs`, at the lines cited in sections 3.3 and 6. https://github.com/Azure/azure-sdk-for-net/tree/117f784c4102d93b65978d3b3d72de36c8824105/sdk/core/Azure.Core/src
4. MSAL.NET, tag `4.90.0`, `src/client/Microsoft.Identity.Client/`: `Http/HttpManager.cs`, `Http/Retry/DefaultRetryPolicy.cs`, `Http/Retry/HttpRetryConditions.cs`, `Http/Retry/RetryPolicyFactory.cs`, `AppConfig/BaseAbstractApplicationBuilder.cs`, `Internal/ServiceBundle.cs`, `Instance/Discovery/NetworkMetadataProvider.cs`, `Instance/Discovery/NetworkCacheMetadataProvider.cs`, `Instance/Discovery/InstanceDiscoveryManager.cs`, `OAuth2/OAuth2Client.cs` line 136, at the lines cited in section 3.3. https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/tree/c34d5b49f88808479661267d142ae3ff02d6027f/src/client/Microsoft.Identity.Client
5. dotnet/runtime, tag `v10.0.13`, `src/libraries/System.Net.Http/src/System/Net/Http/HttpClient.cs` lines 577 to 624 (`HandleFailure`). https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/System.Net.Http/src/System/Net/Http/HttpClient.cs
6. Repository at `main` `675f8372`: the files and lines cited in sections 2, 3 and 6.

## 11. Open questions for Hari

None. Decision 6846551008 sets the scope, keeps the values and applies DA-1 and DA-2 to each mode; decision 6845385609 points 1 to 3 answered Revision 2's Q1 and Q2.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, the scope in section 2, or any existing test, or when a cap in section 9 is exhausted (its own action applies).

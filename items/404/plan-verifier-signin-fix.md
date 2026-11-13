# Verifier sign-in fix contract-grade plan

Revision 2. Issue 404, pull request short name `verifier sign-in fix`. Status: not approved. Implementing tool: Claude Code ([decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192) point 2). Hari merges.

In run 43318281268 one HTTP request to the Microsoft Entra token endpoint got no response, and the verifier's 10 s `TokenTimeout` cancelled it before the Azure SDK's own retry could act, because the SDK's per-request timeout is 100 s by default. This pull request gives the `b10-abrupt-verify` mode's Kafka credential its own retry settings: a 1.5 s per-request timeout, one retry and a fixed 0.2 s delay, so a single stalled request is abandoned and sent again well inside `TokenTimeout`. No check, problem, reason, output member, configuration key, deadline or load generator behaviour changes.

## 0. Changes since Revision 1

Revision 2 follows [decision 6844950677](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6844950677), which did not approve Revision 1 and did not accept its DA-1.

- Mechanism replaced. Revision 1 retried the whole Kafka client install in `B10AbruptVerifyHandler`. Revision 2 removes that retry and instead sets `RetryOptions` on the `WorkloadIdentityCredential` that the abrupt mode's Kafka token provider uses (decision point 2). The handler, check 14 and `docs/operations/b10-abrupt-contract.md` no longer change.
- Scope moved: `src/Parchwell.Verifier/Program.cs` (abrupt-mode branch only) and one new internal file under `src/Parchwell.Verifier/VerifyRun/Abrupt/` are allowed; `B10AbruptVerifyHandler.cs` and the contract file are now frozen.
- Section 3.3 answers decision point 3 (a) to (e) from pinned source and official documentation, with a scripted worst case.
- Section 1 and DA-1 use the Application Insights evidence [6844977688](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6844977688) (decision point 4).
- Requirements, test map, deployment assumptions and open questions are rewritten for the new mechanism. Revision 1's check findings P2-A, P2-B and N1 to N3 were about the removed handler retry and no longer apply.

## 1. Goal and authority

Goal, from [decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192) point 1: one slow token-endpoint call no longer fails the verify Job, while the Job still ends within its existing deadline and every check and published format stays unchanged.

Authority, in creation order:
- The [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404) and the decisions listed in `items/404/state.md` on `main` at `675f8372`, which this pull request does not change.
- [Decision 6837706862](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837706862): one diagnostic trial, run 43318281268, on `main` at `675f8372`.
- [Evidence reading 6837900667](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837900667) (evidence, not a decision): in run 43318281268 a token acquisition began about 22:37:30-32Z and failed at 22:37:40Z with `TaskCanceledException`; problems were only `b10_abrupt_verify_window_incomplete`.
- [Decision 6837940546](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837940546): one further trial with no code change. It does not govern this pull request.
- [Decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192): this pull request, its scope (the `b10-abrupt-verify` mode's initial Kafka OAUTHBEARER token acquisition) and goal (point 1); one session under docs/agents/factory.md "Item lifecycle" step 3 "Standing approval", Claude Code, no `results/`, expected outcome or verdict, no workflow dispatch, Hari merges (point 2); the caps in section 9 (point 3).
- [Evidence reading 6838450660](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6838450660) (evidence): rerun 43322351104 had no token failure; one run cannot show whether the timeout recurs.
- [Decision 6844950677](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6844950677): Revision 1 not approved and DA-1 not accepted (point 1); Revision 2 uses the credential's own retry settings, verifier only, load generator sign-in unchanged (point 2); the facts (a) to (e) established before proposing values (point 3); telemetry evidence first, if Hari supplies it (point 4). It is not plan approval.
- [Application Insights evidence 6844977688](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6844977688) (evidence, not a decision), supplied under decision point 4 for run 43318281268's verify pod: a discovery GET (105.2 ms) and two token POSTs (40.6 ms, 36.7 ms), all 200; then one `WorkloadIdentityCredential.GetToken` span of 10014.2 ms and one token POST of 10004.9 ms, failed, result code 0; storage calls before and after succeeded, including an upload at 22:37:41. Exactly one POST and one `GetToken` span: neither Azure.Core nor MSAL retried within 10 s. Its closing statement, that a per-request `NetworkTimeout` of a few seconds would have let the SDK retry inside `TokenTimeout`, is labelled there as an inference, not established. This plan does not rely on it as fact; DA-1 carries it.

Parent items #347 and #348 have no decision or approval comment created or edited after 2026-11-12T20:28:03Z (read by script at planning time); no parent decision is adopted for this pull request.

Scope reading: decision 6844950677 point 2 says the values "apply to the verifier only". Decision 6837975192 point 1 limits this pull request to the `b10-abrupt-verify` mode. The plan therefore applies the settings in that mode only; the other verifier modes keep their current credential. This follows from the two decisions and is not an open question.

## 2. Scope

### 2.1 Cause in code and source (read on `main` at `675f8372` and at the pinned tags in section 10)

- `src/Parchwell.Kafka/Authentication/KafkaAuthenticationServiceCollectionExtensions.cs` lines 30 to 43: `AddParchwellKafkaAuthentication` registers `IKafkaTokenProvider` with `TryAddSingleton`, as a `WorkloadIdentityTokenProvider` over `new WorkloadIdentityCredential(new WorkloadIdentityCredentialOptions { TenantId, ClientId, TokenFilePath })` with no retry options. Line 44 to 49: `KafkaAuthenticationFactory` takes `IKafkaTokenProvider` from the container.
- `src/Parchwell.Kafka/Authentication/WorkloadIdentityTokenProvider.cs` lines 11 to 14 and 24 to 30: `GetToken(scope, cancellationToken)` calls `credential.GetToken(new TokenRequestContext([scope]), cancellationToken)`.
- `src/Parchwell.Kafka/Authentication/KafkaOAuthBearerSession.cs` lines 221 to 233: the observed-provider path is used only when a test expiry cap is set; otherwise the session calls `IKafkaTokenProvider.GetToken(_scope, _cancellationToken)`. `KafkaAuthenticationFactory.cs` lines 80 to 92: the verifier's `CreateSession(identity, token)` sets no cap.
- `src/Parchwell.Verifier/Program.cs` lines 151 to 165: modes `start-line`, `verify`, `b10-sweep-line`, `b10-sweep` and `b10-abrupt-verify` call `AddParchwellKafkaAuthentication` with tenant, client ID and token file from configuration. Lines 194 to 208: the `b10-abrupt-verify` branch.
- `src/Parchwell.Verifier/Common/Kafka/VerifierKafkaSession.cs` lines 35 to 62: `InstallToken` cancels the session's acquisition token after `TokenTimeout`; `charts/parchwell/values.yaml` line 63: `TokenTimeout: 00:00:10`.
- Azure.Core 1.63.0 (the pin in `Directory.Packages.props` line 15; it carries the `Azure.Identity` credentials): `RetryOptions.NetworkTimeout` defaults to 100 s (section 3.3), so the SDK cannot abandon a stalled request before the 10 s `TokenTimeout` cancels the caller's token. The caller-cancelled request is not retried (section 3.3 (b)). This matches the evidence: one POST, cancelled at about 10 s.

### 2.2 Files allowed to change

- `src/Parchwell.Verifier/Program.cs`: inside the `if (mode == "b10-abrupt-verify")` branch only (lines 194 to 208 at `675f8372`), the one registration of R1.
- One new internal file under `src/Parchwell.Verifier/VerifyRun/Abrupt/` holding the abrupt-mode Kafka token provider and its credential options (R2, R3). Its name and code shape are the implementer's.
- New test files: one under `tests/Parchwell.Verifier.Tests/Host/` for T1 to T3, and one under `tests/Parchwell.Verifier.Tests/Host/` or a new folder `tests/Parchwell.Verifier.Tests/Authentication/` for T4 to T7.
- `items/404/state.md` (docs/agents/factory.md "Item state file") and a new receipt `items/404/verifier-signin-fix-receipt.md`.

### 2.3 Paths that must not change

- `src/Parchwell.Kafka/**`, `src/Parchwell.LoadGen/**`, `src/Parchwell.Enrichment/**`, `src/Parchwell.Verifier/Common/**`, `src/Parchwell.Verifier/VerifierHostOptions.cs`, `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs`, `src/Parchwell.Verifier/VerifierWorker.cs`, every file under `src/Parchwell.Verifier/VerifyRun/Abrupt/` that exists at `675f8372` (including `B10AbruptVerifyHandler.cs`), and every other verifier mode's handler.
- `charts/**`, `docs/operations/**` (including `b10-abrupt-contract.md`, `verifier-contract.md`, `chart-contract.md` and `a01-runtime-limits.json`), `docs/blueprint.md`.
- `.github/workflows/**`, `docs/operations/workflows/**`, `infra/**`, `scripts/**`, `results/**`, every `expected.yaml`, every `*.csproj`, `Directory.*.props`, every `packages*.lock.json`, `*.sln*`.
- Every existing file under `tests/**`, including `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`.

### 2.4 Protected paths, CI and existing tests

- `src/Parchwell.Verifier/**` and `tests/Parchwell.Verifier.Tests/**` match `Parchwell.Verifier*/` in `.github/CODEOWNERS` line 16, so Hari merges.
- Neither `Program.cs` nor any `VerifyRun/Abrupt/` file is in the `mutate` list of `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` at `675f8372` (lines 17 to 35), so mutation acceptance does not apply.
- No new project, assembly reference or package: `WorkloadIdentityCredential`, `WorkloadIdentityCredentialOptions`, `RetryOptions`, `RetryMode` and `HttpClientTransport` are in Azure.Core 1.63.0, which both `Parchwell.Verifier` and `Parchwell.Verifier.Tests` already resolve (`src/Parchwell.Verifier/packages.lock.json`; `Program.cs` lines 241 and 263 already construct `WorkloadIdentityCredential`).
- This plan adds no check and tightens none, so `plan-pr` plan rule 2 (standing fixture rule) does not apply. No existing test changes. If an existing test fails, that is a stop under section 12, not a fixture change.

## 3. External contracts

### 3.1 Unchanged

The mode name, every option name and value, every problem and reason literal, the `nonfinal_result` envelope and `data` members, check numbering and text, exit codes, `TokenTimeout` 00:00:10, `ProcessLifetime` 00:12:00 and the Job deadline `verify_job_seconds` 750 are unchanged. No configuration key, chart value, environment variable or log line is added or pinned. The credential's tenant ID, client ID and token file path are the same values `AddParchwellKafkaAuthentication` receives (`Program.cs` lines 158 to 163). Load generator, enrichment and every other verifier mode keep the Parchwell.Kafka credential with Azure.Core defaults.

### 3.2 Pinned retry settings (policy proposal, not measured)

The abrupt-mode Kafka credential is a `WorkloadIdentityCredential` whose `WorkloadIdentityCredentialOptions.Retry` has exactly:

| Setting | Value | Default it replaces |
|---|---|---|
| `NetworkTimeout` | `00:00:01.500` | `00:01:40` |
| `MaxRetries` | `1` | `3` |
| `Mode` | `RetryMode.Fixed` | `RetryMode.Exponential` |
| `Delay` | `00:00:00.200` | `00:00:00.800` |
| `MaxDelay` | `00:01:00`, unchanged | `00:01:00` |

`RetryPolicy` stays null, so these values build the standard `RetryPolicy` (section 3.3). The values are constants in the new file, with a comment naming decision 6844950677; they are not configuration. This table is the one statement of the values; other files cite it.

Why these values: 1.5 s is about 14 times the slowest successful token-endpoint call in the evidence (105.2 ms), while two attempts of every request in a first acquisition still end 3.6 s before `TokenTimeout` (section 3.3 (e)). `Fixed` mode keeps each delay at or below `Delay` exactly. Alternatives computed by the same script are in section 3.3 (e). These are design choices for Hari's approval; none is a measured optimum.

### 3.3 Facts required by decision 6844950677 point 3

Sources are in section 10. Line numbers are at Azure SDK for .NET tag `Azure.Core_1.63.0` (commit `117f784c4102d93b65978d3b3d72de36c8824105`, the commit Microsoft Learn's `RetryOptions` page links for Azure.Core v1.63.0), MSAL.NET tag `4.90.0` (commit `c34d5b49f88808479661267d142ae3ff02d6027f`; the image loads MSAL 4.90.0, `src/Parchwell.Verifier/packages.lock.json`) and dotnet/runtime tag `v10.0.13` (the verifier image base is `aspnet:10.0.13`, `src/Parchwell.Verifier/Parchwell.Verifier.csproj` line 8).

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

(d) MSAL's own retry. MSAL 4.90.0's `HttpManager` retries only when internal retries are enabled (`HttpManager.cs` lines 117 to 141). `MsalConfidentialClient` calls `WithHttpClientFactory(factory, false)` (`MsalConfidentialClient.cs` line 95); the second argument is `retryOnceOn5xx`, and `false` sets `DisableInternalRetries` true (`BaseAbstractApplicationBuilder.cs` lines 85 to 90), which reaches the `HttpManager` (`ServiceBundle.cs` lines 32 to 35; `HttpManager.cs` lines 48 to 55). So with this credential MSAL does not retry, and the attempts do not multiply. Had it been enabled, its STS policy would retry once after 1000 ms (`DefaultRetryPolicy.cs` lines 14, 17, 34 to 37). MSAL's instance-discovery call never retries in any case (`NetworkMetadataProvider.cs` lines 93 to 103, 143 to 154). Instance discovery results are cached in a static cache for the process (`NetworkCacheMetadataProvider.cs` line 14), and a failed discovery that the caller did not cancel lets MSAL continue with known metadata (`InstanceDiscoveryManager.cs` lines 205 to 218). In `HttpClient`, an exception from the handler that is neither cancellation nor `HttpRequestException` passes through unchanged, and a cancellation not caused by the caller or the client's own timeout is not rewritten (`HttpClient.cs` lines 577 to 609 at `v10.0.13`).

(e) Worst case, computed by script (`python3 -I` on the script in the receipt; output below). Configured values only; not measured. One HTTP request through the pipeline takes at most `(MaxRetries + 1) * NetworkTimeout + MaxRetries * Delay`. A later acquisition sends one token POST. The first acquisition in the process may also send one instance-discovery GET before it, so it takes at most twice that.

| Settings | Attempts per request | Later acquisition (s) | First acquisition (s) | Margin to `TokenTimeout` 10 s |
|---|---|---|---|---|
| Proposed: 1.5 s, 1, Fixed, 0.2 s | 2 | 3.2 | 6.4 | 3.6 s |
| 1.0 s, 1, Fixed, 0.2 s | 2 | 2.2 | 4.4 | 5.6 s |
| 2.0 s, 1, Fixed, 0.2 s | 2 | 4.2 | 8.4 | 1.6 s |
| 1.0 s, 2, Fixed, 0.2 s | 3 | 3.4 | 6.8 | 3.2 s |
| Defaults: 100 s, 3, Exponential, 0.8 s | 4 | 406.72 per request | | none |

The bound holds when each attempt ends within `NetworkTimeout`: a response with a `Retry-After` header (c), or a body delivered in many slow reads (a), can take longer. In those cases, and in every other case, `TokenTimeout` still cancels the call at 10 s, exactly as today. The verify Job's configured budgets are unchanged: the sum that `VerifierHostOptionsValidator.ValidateAbrupt` collects (lines 237 to 241: `TokenTimeout` 10, `MetadataTimeout` 10, `WatermarkTimeout` 10, `BlobIoTimeout` 30, `IoTimeout` 10, `CleanupTimeout` 10, `SafetyStop` 600 seconds) is 680 s, below `ProcessLifetime` 720 s and the Job deadline 750 s. This pull request adds no session and no wait, so that sum and the process watchdog's end at `ProcessLifetime` are unchanged.

## 4. Requirements

- R1. WHEN the verifier host is built with `Verifier:Mode` `b10-abrupt-verify`, THEN the service collection has exactly one `IKafkaTokenProvider` registration, and it resolves to the abrupt-mode provider of R2. The registration is made inside the abrupt branch of `Program.cs`; registrations made later by `configureForTests` still take precedence, as today.
- R2. WHEN the abrupt-mode provider's `GetToken(scope, cancellationToken)` is called, THEN it returns `credential.GetToken(new TokenRequestContext([scope]), cancellationToken)` of one `WorkloadIdentityCredential` created once per provider, as `WorkloadIdentityTokenProvider` does. Exceptions propagate unchanged.
- R3. WHEN the provider's credential is created, THEN its `WorkloadIdentityCredentialOptions` carry `TenantId`, `ClientId` and `TokenFilePath` equal to the registered `KafkaWorkloadIdentityOptions` values, the `Retry` values of section 3.2, a null `RetryPolicy`, `IsAzureProxyEnabled` false, and no other property changed from its default. An internal seam lets a test supply a `Transport` on those same options; it cannot change the retry values.
- R4. WHEN one token POST gets no response within `NetworkTimeout` and the caller's token is not cancelled, THEN the credential sends exactly one more POST after a delay of 0.16 s to 0.2 s, and returns that POST's token if it succeeds.
- R5. WHEN both POST attempts get no response, THEN `GetToken` throws after exactly two POSTs, before the caller's token is cancelled. In the verifier, the existing behaviour for a failed initial acquisition follows unchanged (problem 5 `b10_abrupt_verify_window_incomplete`).
- R6. WHEN the token endpoint answers 5xx to both attempts, THEN `GetToken` throws after exactly two POSTs; MSAL adds no further attempt.
- R7. WHEN the caller's token is cancelled during an attempt, THEN no further POST is sent and `GetToken` throws.
- R8. WHEN the verifier host is built for `start-line`, `verify`, `b10-sweep-line` or `b10-sweep`, THEN `IKafkaTokenProvider` resolves to `Parchwell.Kafka.Authentication.WorkloadIdentityTokenProvider`, as today.

## 5. Invariants and safety

- The verifier stays the oracle: no check, problem, reason or output changes. A retry changes only whether a token arrives within `TokenTimeout`.
- `src/Parchwell.Kafka/**` and every load generator file are unchanged, so the client under test signs in exactly as before (decision 6844950677 point 2).
- The same identity (tenant, client ID, token file) signs in; no new identity, scope, audience or network exposure.
- No new configuration key, chart value, schema entry, environment variable, project, package or assembly reference.
- No workflow dispatch, trial, `results/`, expected outcome or verdict change ([decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192) point 2).

## 6. Test map

T1 to T3 build the host through `Program.BuildHost` and count descriptors through its `configureForTests` callback. Each mode uses the valid arguments an existing test already uses for it: the key-value pairs that `B10AbruptVerifyHarness` sets for `b10-abrupt-verify` (copied into the new test file when the harness itself needs Docker; they include `TenantId` `synthetic-tenant`, `WorkloadIdentityClientId` `synthetic-client` and `FederatedTokenFilePath` `/tmp/synthetic-token`), `VerifierHostTestOptions.ArgumentsFor` for `start-line` and `verify`, and the arguments of `B10SweepLineHandlerTests` and `B10SweepHandlerTests` for the two sweep modes. A shared helper for those arguments may be added in the new test file; existing test files do not change. T4 to T7 call the abrupt-mode provider of R2, built through the R3 seam with a `Transport` of `new HttpClientTransport(new HttpClient(handler))`, where `handler` is a test `HttpMessageHandler`. Their common inputs: tenant `11111111-1111-1111-1111-111111111111`, client ID `22222222-2222-2222-2222-222222222222`, a temporary token file containing `synthetic-assertion`, scope `https://eventhubs.azure.net/.default`, and a caller token cancelled after 10 s (the `TokenTimeout` value). The handler answers every GET at once with 200 and a body the implementer chooses (MSAL accepts it or continues with known metadata under section 3.3 (d)). It counts POSTs whose path ends `/oauth2/v2.0/token` and answers them as each test scripts. A successful answer is 200 with JSON `{"token_type":"Bearer","expires_in":3600,"access_token":"<token>"}`. "Blocks" means the handler waits until its own cancellation token is cancelled, then throws `TaskCanceledException` for that token. Elapsed time is measured with `Stopwatch` around `GetToken`. T4 to T7 use real time and carry trait `Tier` = `Slow`.

| Test | Requirement | Inputs | Expected |
|---|---|---|---|
| T1 | R1 | Mode `b10-abrupt-verify`. | Exactly 1 `IKafkaTokenProvider` descriptor; the resolved instance is the R2 provider type. |
| T2 | R3 | The resolved provider's credential options, read through the R3 seam, in the T1 host. | `Retry.NetworkTimeout` `00:00:01.500`; `Retry.MaxRetries` 1; `Retry.Mode` `Fixed`; `Retry.Delay` `00:00:00.200`; `Retry.MaxDelay` `00:01:00`; `RetryPolicy` null; `IsAzureProxyEnabled` false; `TenantId` `synthetic-tenant`; `ClientId` `synthetic-client`; `TokenFilePath` `/tmp/synthetic-token`. |
| T3 | R8 | Theory over modes `start-line`, `verify`, `b10-sweep-line`, `b10-sweep`. | Exactly 1 `IKafkaTokenProvider` descriptor; the resolved instance's type full name is `Parchwell.Kafka.Authentication.WorkloadIdentityTokenProvider`. |
| T4 | R2, R4 | POST 1 blocks. POST 2 answers 200 with `access_token` `synthetic-2`. | Returns a token whose `Token` is `synthetic-2`; POST count 2; elapsed at least 1.4 s and less than 10 s; the caller token is not cancelled on return. |
| T5 | R5 | Every POST blocks. | Throws; POST count 2; elapsed at least 2.9 s and less than 10 s; the caller token is not cancelled on return; POST count is still 2 after a further 2 s. |
| T6 | R6 | Every POST answers 503 at once, with no `Retry-After` header. | Throws; POST count 2; POST count is still 2 after a further 2 s. |
| T7 | R7 | POST 1 blocks. The caller token is cancelled 0.5 s after `GetToken` starts. | Throws; POST count 1; POST count is still 1 after a further 2 s. |

Lower time bounds come from section 3.3: T4 is at least one `NetworkTimeout` (1.5 s) less 0.1 s of timer tolerance; T5 is two `NetworkTimeout`s (3.0 s) less 0.1 s. The 0.1 s tolerance is a test choice, not a measurement. The lower delay bound 0.16 s is not asserted, because a timer test cannot separate it reliably from scheduling noise.

Every existing test in every test project passes unchanged.

## 7. Deployment assumptions

None is covered by an offline test. Each is listed under section 11 until a decision or approval by Hari accepts it by name.

- DA-1. In the deployed verifier pod, after a token POST gets no response within 1.5 s, the one retry gets a response within 1.5 s. T4 shows only that the credential sends the retry. Evidence: run 43318281268 ([6844977688](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6844977688)): one fresh pod at start, idle apart from the verifier's own work (about 30 blob reads between 22:37:28.26 and 22:37:30.83). One POST got no response in 10 s (result code 0), while two POSTs 2.6 s earlier took 40.6 ms and 36.7 ms and storage calls before and after succeeded. Whether the earlier calls used an already-open or a fresh connection is not recorded. Whether the stalled request's connection is closed when it is abandoned, and whether the retry uses a new connection, is not established from the evidence or from the sources read. Run 43322351104 had no token failure ([6838450660](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6838450660)); its call durations were not reported.
- DA-2. In the deployed verifier pod, a healthy token POST or instance-discovery GET completes within 1.5 s. If it does not, a call that would have succeeded in 1.5 s to 10 s today fails after two attempts, and the mode records problem 5 where it does not today. Evidence: the same run and conditions as DA-1: discovery GET 105.2 ms, token POSTs 40.6 ms and 36.7 ms, all on one fresh, otherwise idle pod at start; connection state not recorded. Three calls from one run are not a distribution.

## 8. Acceptance commands

- `./scripts/verify-dotnet.sh`: the full gate passes with warnings as errors, including T1 to T7 and every existing test. Evidence: the command's summary line in the receipt.
- `./scripts/verify-dotnet.sh --project Parchwell.Verifier.Tests` run first while iterating; it is not the full gate.
- `git diff --name-only origin/main...HEAD` lists only paths in section 2.2. Evidence: the list in the receipt.
- A script check that `src/Parchwell.Verifier/Program.cs` differs from `main` only inside the `b10-abrupt-verify` branch. Evidence: the script and its output in the receipt.
- The section 3.3 (e) script run with `python3 -I`, its text and output copied into the receipt.

## 9. Caps and plan-check answers

Caps ([decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192) point 3): 20 verification-failure root causes and 20 review-finding root causes, each with exhaustion action `push -wip and hand off to the next session` (AGENTS.md "Before opening a pull request" rule 11).

(a) Steps whose effort scales with a number unknown at planning time: verification-failure root causes and review-finding root causes, capped as above. Test count is fixed at seven tests, T3 with four cases.

(b) Stricter than the parent plan: the item plan (Revision 5) and the decisions before 6837975192 say nothing about token acquisition. This plan sets retry values for one verifier mode's credential and changes no parent rule. It is not stricter.

(c) Tests or checks that depend on code, data or behaviour neither on `main` nor in the pull request: (1) the token endpoint's live behaviour on a retry, DA-1, and its healthy latency, DA-2; neither is settled by recorded output (the evidence has three successful calls and one stalled call from one run) nor by source. (2) Azure.Core 1.63.0 and MSAL 4.90.0 behaviour, which T4 to T7 exercise through the real packages already resolved by the test project, and which section 3.3 reads at their pinned sources. (3) `HttpClient` behaviour at runtime 10.0.13 in the image, read at its source (section 3.3 (d)); the test host runs the SDK in `global.json` (10.0.402), whose runtime patch is not checked against 10.0.13 here.

## 10. Sources

Documentation tools used: Microsoft Learn MCP (`microsoft_docs_fetch`), and source read at pinned tags by `git clone --depth 1 --filter=blob:none --sparse` and `curl` of raw files, on 2026-11-13.

1. Microsoft Learn, `RetryOptions` class, Azure.Core v1.63.0: properties `Delay`, `MaxDelay`, `MaxRetries`, `Mode`, `NetworkTimeout` ("The timeout applied to individual network operations"), source link at commit `117f784c4102d93b65978d3b3d72de36c8824105`. https://learn.microsoft.com/en-us/dotnet/api/azure.core.retryoptions?view=azure-dotnet
2. Microsoft Learn, `CancellationTokenSource.CancelAfter(TimeSpan)`, .NET 10: `ArgumentOutOfRangeException` when "`delay.TotalMilliseconds` is less than -1 or greater than Int32.MaxValue (or UInt32.MaxValue - 1 on some versions of .NET)". https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokensource.cancelafter?view=net-10.0
3. Azure SDK for .NET, tag `Azure.Core_1.63.0`, `sdk/core/Azure.Core/src/`: `RetryOptions.cs`, `ResponseClassifier.cs`, `DelayStrategy.cs`, `ExponentialDelayStrategy.cs`, `FixedDelayStrategy.cs`, `ResponseHeaders.cs`, `Pipeline/RetryPolicy.cs`, `Pipeline/HttpPipelineBuilder.cs`, `Pipeline/Internal/ResponseBodyPolicy.cs`, `Shared/CancellationHelper.cs`, `Identity/CredentialPipeline.cs`, `Identity/HttpPipelineClientFactory.cs`, `Identity/HttpPipelineMessageHandler.cs`, `Identity/MsalConfidentialClient.cs`, `Identity/Credentials/WorkloadIdentityCredential.cs`, `Identity/Credentials/ClientAssertionCredential.cs`, `Identity/Credentials/TokenCredentialOptions.cs`, at the lines cited in section 3.3. https://github.com/Azure/azure-sdk-for-net/tree/117f784c4102d93b65978d3b3d72de36c8824105/sdk/core/Azure.Core/src
4. MSAL.NET, tag `4.90.0`, `src/client/Microsoft.Identity.Client/`: `Http/HttpManager.cs`, `Http/Retry/DefaultRetryPolicy.cs`, `Http/Retry/HttpRetryConditions.cs`, `Http/Retry/RetryPolicyFactory.cs`, `AppConfig/BaseAbstractApplicationBuilder.cs`, `Internal/ServiceBundle.cs`, `Instance/Discovery/NetworkMetadataProvider.cs`, `Instance/Discovery/NetworkCacheMetadataProvider.cs`, `Instance/Discovery/InstanceDiscoveryManager.cs`, `OAuth2/OAuth2Client.cs` line 136, at the lines cited in section 3.3. https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/tree/c34d5b49f88808479661267d142ae3ff02d6027f/src/client/Microsoft.Identity.Client
5. dotnet/runtime, tag `v10.0.13`, `src/libraries/System.Net.Http/src/System/Net/Http/HttpClient.cs` lines 577 to 609 (`HandleFailure`); identical at `v10.0.0`. https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/System.Net.Http/src/System/Net/Http/HttpClient.cs
6. Repository at `main` `675f8372`: the files and lines cited in sections 2 and 3.

## 11. Open questions for Hari

- Q1. Accept DA-1 by name (the one retry gets a response within 1.5 s after a stalled POST), or say what evidence it needs. No offline test covers it (docs/agents/factory.md "Item lifecycle" step 1).
- Q2. Accept DA-2 by name (healthy token-endpoint calls in the pod complete within 1.5 s), or choose other values from section 3.3 (e) or elsewhere. No offline test covers it.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, the scope in section 2, or any existing test, or when a cap in section 9 is exhausted (its own action applies).

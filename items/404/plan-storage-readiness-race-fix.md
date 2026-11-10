# Storage readiness race fix: contract-grade plan

Revision 3. Issue 404. Plan branch `claude/404-plan`. Baseline: `main` at `0f769fb240d1de50192f4c8eefaf12698002efc7` (Revision 1 was written on `aadb0c6d5252d6ca9638d116c1bbf7339c17037e`; section 17 lists what changed in Revisions 2 and 3). Not posted; awaiting a fresh reproduction check.

In trial 9 the first blob write of the `start-line` verifier job got `404` with error code `ResourceNotFound`, so the run failed before any producer started. This plan makes the verifier retry that one write, only on that exact answer, inside the Blob I/O budget it already has. Every other answer, and every other write, fails exactly as on `main`; a condition that does not clear within the budget still fails with the same answer.

## 1. Goal and authority

- Authority: Hari's [decision 6761783572](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761783572), items 3 and 4: plan the storage readiness race fix in its own pull request; state its effect on every scenario that uses the same phase path; every gate, timeout, failure name, protocol change and the implementing tool goes to Hari.
- Item 1 of the same decision set the trial gate. [Decision 6762516530](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6762516530) amends it: no further B10 trial until this storage readiness race fix merges and the B10 restart redesign is implemented. The capture-loss race fix requirement is met: it merged as #425 (merge commit `0f769fb2`). This plan authorizes no trial and no dispatch.
- Catch-up: `items/404/state.md` on `main` (covers comment 6756884940), then every comment created after it, read in full: 6758253501, 6760104002, 6760120861, 6760139065, 6760170847, 6760180716, 6760201403, 6760218433, 6760242955, 6760646338, 6760749353, 6760770654, 6761052971, 6761095907, 6761380716, 6761394550, 6761524200, 6761542603, 6761783572. Revision 2 refresh, read in full: 6761981257 (offline end-to-end test plan, option a0, in parallel), 6762055013 (PR3b Q13, option C), 6762156869 (capture-loss race fix #425 merged) and 6762516530 (B10 restart redesign: partial-frame plan stopped, PR3b paused at Revision 2 `37128767`, storage readiness fix continues, trial gate amended). Newest comment read: 6762516530 (created 2026-11-10T08:19:11Z). 194 comments in total; no comment was edited or deleted since the Revision 1 read. The decision says what the control session's prompt says; no conflict was found.
- No earlier decision covers storage readiness, the start-line write or `StartLineHandler`. The item plan (Revision 5, 6705594392) does not cover this pull request.

## 2. The failure and what is known about its cause

### 2.1 Facts from public logs (computed by script from the GitHub Actions job logs)

| | Trial 8, run 42771287992 | Trial 9, run 42773789291 |
|---|---|---|
| Verifier role assignment `verifier_results_contributor` created | 07:02:11.35Z | 07:23:15.64Z |
| `Apply complete! Resources: 26 added` | 07:02:35.54Z | 07:23:16.27Z |
| `verifier` container created | 07:00:53.60Z | 07:21:20.75Z |
| Blob private endpoint DNS zone group created | 07:01:45.84Z | 07:22:12.79Z |
| Start-line Job created | 07:02:50.44Z (role + 39.1 s) | 07:23:30.46Z (role + 14.8 s) |
| Start-line outcome (`phase-status.json`) | `complete` | `failed`; `b10_abrupt_publication_failed`, boundary `metadata` (`b10_abrupt.py` line 623) |

- Trial 9's storage account was created 15.2 minutes after trial 8's account deletion completed.
- The trial 9 Job reported `Job Failed` at 07:23:42.49Z, 12.0 s after the Job was created.
- Artifact hashes checked: 13742914191 `ff855570...bf9097`, 13742900343 `2c516f24...49e34`, 13704155185 `bee17231...cdcf`, each matching the decision's value in full. Trial 7's `phase-status.json` records `start-line complete`.

### 2.2 Facts not seen by this session

The `404` and `x-ms-error-code: ResourceNotFound` come from the verifier container log that Hari read in Log Analytics (`ContainerLogV2`). This session holds no Azure access and has not seen that log. Section 9 tests the exact answer the decision quotes.

### 2.3 Search of earlier runs for the same failure

Script `fetch.sh` listed every run of `scenario.yml` (49), `cluster-smoke.yml` (2) and `b10-abrupt-trial.yml` (9), and fetched the job log of every job whose "Apply the saved ephemeral plan" step ran: 38 jobs in 36 runs. `verifier-c5-mutation.yml`, `sweeper.yml`, `dotnet-verification.yml` and `docs-advisory.yml` never apply the ephemeral layer.

- 36 of the 38 jobs started a worker Job; 18 of those first Jobs were `start-line`.
- Only one first Job warned `wait exit`: trial 9's start-line.
- Public job logs do not hold container logs, so a `ResourceNotFound` that did not fail a Job would be invisible here.
- 4 of the 18 start-line first Jobs started within 20 s of the verifier role assignment: trial 9 (14.8 s, failed), run 42189889469 C1 (15.7 s, passed), trial 7 (16.0 s, passed), run 41866017867 (18.1 s, passed).
- Run 42189889469 C1 recreated the account 5.4 minutes after the previous deletion, sooner than trial 9's 15.2 minutes, and passed.

So neither a young role assignment nor a fast recreation explains trial 9 by itself. One failure in 18 start-line first Jobs is an observation, not a rate (synthetic and live counts both too small).

### 2.4 Official documentation

- [Common REST API error codes](https://learn.microsoft.com/rest/api/storageservices/common-rest-api-error-codes) (read 2026-11-10): `ResourceNotFound`, Not Found (404), "The specified resource doesn't exist." It is a code common to all storage services, not a Blob-specific code.
- [Blob Storage error codes](https://learn.microsoft.com/rest/api/storageservices/blob-service-error-codes) (read 2026-11-10): `ContainerNotFound`, 404, "The specified container does not exist."; `BlobNotFound`, 404.
- [Create Container, Remarks](https://learn.microsoft.com/rest/api/storageservices/create-container#remarks) (read 2026-11-10): "Containers are created immediately within the storage account." This is the data-plane Create Container call. Our containers are created through Azure Resource Manager (`azapi_resource`), so the page does not settle it.
- [Authorize access to blobs by using Microsoft Entra ID](https://learn.microsoft.com/azure/storage/blobs/authorize-access-azure-active-directory#role-assignment-propagation-delays-for-blob-data-access) (read 2026-11-10). Under "Role assignment propagation delays for blob data access": "When you assign roles or remove role assignments, it can take up to 10 minutes for changes to take effect." The same section continues: "If you set the appropriate allow permissions to access data via Microsoft Entra ID and can't access the data, for example you're getting an "AuthorizationPermissionMismatch" error, be sure to allow enough time for the permissions changes you made in Microsoft Entra ID to replicate." Under "Azure built-in roles for blobs", the same page says: "Azure role assignments can take up to 30 minutes to propagate." So the page gives two figures, 10 and 30 minutes, and names `AuthorizationPermissionMismatch` as an example of what a request gets while a new grant replicates. That code does not appear on the Blob Storage error codes or Common REST API error codes pages. [Shared access signature error codes](https://learn.microsoft.com/rest/api/storageservices/sas-error-codes) (read 2026-11-10) lists `AuthorizationPermissionMismatch` with new status code "403 (Forbidden)" and message "This request is not authorized to perform this operation using this permission.", for shared access signatures. [Troubleshoot 403 errors in Azure Blob Storage](https://learn.microsoft.com/troubleshoot/azure/azure-storage/blobs/authentication/storage-troubleshoot-403-errors) (read 2026-11-10) lists, under "Common 403 errors related to authorization failures", error code `AuthorizationPermissionMismatch` with causes including "**Insufficient Azure RBAC role**: The user or service principal lacks the required role for the operation." So a request whose role grant has not yet taken effect is documented to get a 403, not a 404.
- [Troubleshoot private endpoint creation](https://learn.microsoft.com/troubleshoot/azure/private-link/troubleshoot-private-endpoint-creation-failed#resolution-b) (read 2026-11-10): deletion is asynchronous; "The network adapter, DNS records, and the connection on the target resource are cleaned up after the `delete` call returns"; "Wait at least 20 minutes before re-creating." Its symptom is `PrivateEndpointAlreadyExists` at creation, which trial 9 did not show (the apply succeeded).
- No Microsoft page was found that confirms or refutes a data-plane delay after an ARM-created storage account or container, or after a private endpoint becomes ready. Searched 2026-11-10 with the Microsoft Learn search for storage account propagation, `ResourceNotFound` after creation and private endpoint recreation.

Inference, not verified: the storage front end did not yet serve the recreated account, or its private endpoint connection, to the verifier's request. Role propagation is a weaker candidate for trial 9: the Entra ID page names `AuthorizationPermissionMismatch` (a 403) as the example answer while a grant replicates, and Hari read a 404. A later run whose verifier grant is young may still get that 403; the predicate of E1 does not retry it (Q2). The fix in this plan does not depend on which cause is right: it acts on the observed answer.

## 3. Where the fix belongs

- Option V, recommended: the verifier's start-line handler retries its first write on the exact observed answer. It runs with the real verifier identity, through the real cluster network path, at the moment of the failure. It is testable offline with the real handler, store and Azure SDK. It touches protected paths: `src/Parchwell.Verifier/` and `tests/Parchwell.Verifier.Tests/` match `Parchwell.Verifier*/` in `.github/CODEOWNERS`, so Hari merges. The contract file `docs/operations/verifier-contract.md` is not protected (`/docs/operations/` is listed with no owner, CODEOWNERS line 6; docs/agents/factory.md line 198).
- Option R: a runner-side data-plane probe added to `check_dns` in `infra/scripts/phase/phase.py`, which every Azure workflow already calls after the apply. It needs no workflow change and covers every scenario. But it uses the runner's identity, not the verifier's; the runner's data-plane role is not in this repository (it reads the verifier container, shown by trial 8's successful `az storage blob show` and `download`, but its role and list permission are not verified); and no offline test can run the probe against real storage.
- A workflow step is not needed by either option. `.github/workflows/` is not changed.

Q1 asks Hari to choose. The rest of this plan specifies option V.

### 3.1 Candidates compared, including the two Hari raised

Measured context (section 2.3): across 18 start-line first Jobs, the Job was created 14.0 to 17.5 s after `Apply complete`. Trial 9's Job was created 14.2 s after it and had failed by 26.2 s after it, so its first write fell in that window. Trial 8's Job was created 14.9 s after it and passed. Across all 36 first worker Jobs the gap was 14.0 to 234.2 s (median 15.7 s).

| | V: retry inside the Blob budget (this plan) | H1: retry with waits 10, 20, 40 s | H2: fixed delay after the apply | P: bounded readiness poll |
|---|---|---|---|---|
| Where | `StartLineHandler`, protected | same | `phase.py` (for example after `check_dns`), unprotected | runner: `phase.py`; or cluster: a new verifier mode and Job |
| Scenarios covered | every start-line Job: A01, B04, B06, B07, B10 | same | every workflow that applies the ephemeral layer, every scenario's first Job | runner: every scenario; cluster: only where the new Job runs |
| What it hides | a `ResourceNotFound` that clears within 28 s; it is logged (E3) | one that clears within 70 s; logged | nothing is retried; but a delay that happens to be long enough leaves no trace that the condition ever existed | nothing: it logs the time taken and fails by name at the ceiling |
| Cost per run | 0 s when storage is ready; up to 28 s when not | 0 s when ready; up to 70 s when not | the full delay on every run, ready or not | the time until the first successful read, bounded by the ceiling |
| What a failure looks like | the same 404 and the same phase failure as `main`, about 28 s later, after 14 log lines | the same 404, about 70 s later | exactly as `main`: the same 404 and phase failure | a new named failure before any Job runs (a new failure name for Hari) |
| Timeouts and sums | none change (section 7, I2) | waits total 70 s, more than the 30 s `BlobIoTimeout` that bounds both writes. A Blob budget of 30 + 70 = 100 s makes the validated sum 10+10+10+10+100 = 140 s, above `ProcessLifetime` 120 s; with the 10 s receipt the path reaches 150 s, the Job deadline. So `ProcessLifetime`, `BlobIoTimeout` or a new budget, `start_line_job_seconds` and the phase-side waits that depend on them must change: policy values in `docs/operations/a01-runtime-limits.json`. That file is not a protected path, but its values are policy, which goes to Hari | none in the verifier; the phase orchestration deadlines (`b10_abrupt.py` absolute deadlines, scenario budgets) lose the delay | a new ceiling value; runner poll adds no verifier change |
| Identity and path tested | the verifier identity, through the cluster network, at the real write | same | none | runner: the runner identity, whose data-plane role is not in this repository; cluster: the verifier identity |
| Offline test | real handler, store and SDK (section 8) | same shape | only that the delay is called | runner: only with a fake `az`; no offline test reaches real storage |

Basis for a delay or ceiling length: none documented for storage readiness (section 2.4). The only documented figures are for role propagation, "up to 10 minutes" and "up to 30 minutes" on the same page; they apply only if the cause is role propagation, which fits a 404 poorly. The measured data give one failure at about 15 to 26 s after the apply and passes at 14.0 to 17.5 s; they say nothing about how long the condition lasts. Any length chosen now is policy, not a measurement.

A readiness poll from the runner can only prove the runner's view. A poll that passes does not show the verifier identity can write, and a poll that fails on the runner's own permissions would block runs that would have passed. A poll from the cluster needs a new verifier mode or Job, which is a larger change than this pull request's scope.

## 4. Scope

### 4.1 Files allowed to change

1. `src/Parchwell.Verifier/StartLine/StartLineHandler.cs`: the retry of the start-line note write (section 5), and an optional `ILogger<StartLineHandler>` constructor parameter for the retry log line.
2. New file `tests/Parchwell.Verifier.Tests/Host/StartLineStorageReadinessTests.cs`: tests T1 to T7 of section 8.
3. `docs/operations/verifier-contract.md`: one new paragraph, E4, after the paragraph that begins "Budget requirements are mode-specific." (line 302 on `main`).
4. `items/404/state.md` (decisions added since its covered comment) and a new receipt `items/404/storage-readiness-receipt.md`.

### 4.2 Paths and behaviour that must not change

- `src/Parchwell.Verifier/Common/Storage/BlobVerifierArtifactStore.cs` and `Program.cs`: no change. Native SDK retry stays disabled (`Program.cs` line 255, `Retry.MaxRetries = 0`).
- `tests/Parchwell.Verifier.Tests/Storage/BlobArtifactStoreTests.cs` and `tests/Parchwell.Verifier.Tests/Host/StartLineHandlerTests.cs`: no change, and both stay green. In particular `Given_OutputFailure_When_Creating_Then_PropagatesWithoutRetryOrDelete` (409, 403, 500, 503 propagate with one PUT) keeps its assertions.
- The broker inventory write, the final start-line receipt and every write of every other mode: no retry.
- `infra/`, `charts/`, `.github/workflows/`, `docs/operations/workflows/`, `docs/operations/a01-runtime-limits.json`, `docs/blueprint.md`, `scenarios/`, `results/`, every `expected.yaml`: no change.
- No timeout value, failure name, phase-status field or verdict rule changes. `BlobIoTimeout` stays 30 s, start-line `ProcessLifetime` 120 s, `start_line_job_seconds` 150.

### 4.3 Files shared with other open work on issue 404

- Capture-loss race fix: merged as #425 (`0f769fb2`). It changed `src/Parchwell.Kafka/Statistics/`, its tests, `docs/operations/statistics-bridge.md`, `items/404/state.md` and its receipt. Of the files in 4.1 it changed only `items/404/state.md`. None of `StartLineHandler.cs`, `Program.cs` (line 255), `BlobVerifierArtifactStore.cs`, `verifier-contract.md` (line 302), `VerifierHostOptionsValidator.cs`, `a01-runtime-limits.json` or `b10_abrupt.py` (lines 619 to 623) changed between `aadb0c6d` and `0f769fb2` (`git diff --name-only`).
- PR3b classification plan: paused at Revision 2 (`37128767`) by decision 6762516530. It shares `items/404/state.md` and `docs/operations/verifier-contract.md` (PR3b changes its lines 3 and 5; this plan adds a paragraph after line 302). PR3b also changes `src/Parchwell.Verifier/Program.cs`, which this plan must not change.
- Partial-frame race fix: stopped by decision 6762516530. No shared file.
- Offline end-to-end test plan (decision 6761981257, option a0): its test is in `tests/Parchwell.LoadGen.Tests`; no file in 4.1 is known to be shared.
- B10 restart redesign (decision 6762516530): its design pull request changes `docs/blueprint.md`, which this plan does not change. Its later implementation may change `infra/scripts/phase/b10_abrupt.py` and the B10 Job sequence, which section 11 depends on.
- Whichever merges later rebases `items/404/state.md`. The stop rule (section 16) names the changes that stop this work.

## 5. External contracts

- E1, retry predicate. The start-line note write (`StartLineHandler`, the `CreateAsync` of `StartLineOutputName`) is retried only when it throws `Azure.RequestFailedException` with `Status == 404` and `ErrorCode == "ResourceNotFound"` (ordinal comparison). Any other exception, status or code propagates at once, unchanged. This includes a `RequestFailedException` with status 0, which is how the SDK reports a transport failure such as a refused connection, a code that differs only in letter case, and any `OperationCanceledException`, including a transport timeout.
- E2, budget. Attempts share the existing publication budget: the origin and `BlobIoTimeout` that `main` already applies to both start-line writes (`StartLineHandler.cs` lines 55 to 64). The wait between attempts is 2 s, on the handler's `TimeProvider`. A retry happens only if `elapsed + 2 s < BlobIoTimeout`; otherwise the last `RequestFailedException` is rethrown unchanged: the same instance the SDK raised, not a new exception wrapping it. With the deployed 30 s this allows at most 15 attempts, at 0, 2, ..., 28 s.
- E3, log line. Each retried answer logs one warning, event ID 1, category `Parchwell.Verifier.StartLine.StartLineHandler`, text exactly: `start_line_storage_not_ready attempt <n> status 404 error_code ResourceNotFound; retrying the start-line write.` where `<n>` is the failed attempt's number from 1. No line is logged for the final, rethrown answer.
- E4, contract text appended to `docs/operations/verifier-contract.md` as its own paragraph after line 302:

  > Start-line retries only its first write, the start-line note, and only when that write is answered HTTP 404 with error code `ResourceNotFound`. Trial 9 got that answer about 20 seconds after the ephemeral apply ended ([decision 6761783572](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761783572)); its cause is not verified. Attempts wait 2 seconds apart inside the existing `BlobIoTimeout` budget. When another wait would reach that budget, the last answer fails the mode as before. Each retried answer logs `start_line_storage_not_ready`. Every other answer, including `ContainerNotFound` and every 403, fails at once, and the broker inventory and final receipt writes are not retried.

- E5, unchanged externals: the start-line note and inventory bytes, blob names, `If-None-Match: *` on every PUT, the receipt, exit codes, the phase-side failure `b10_abrupt_publication_failed` at boundary `metadata`, and every scenario's phase-status fields.

## 6. Requirements

- R1. WHEN the note write is answered 404 `ResourceNotFound` and a later attempt within the budget succeeds, THEN the mode returns 0, writes the note once successfully and the inventory once, every PUT carries `If-None-Match: *`, and one E3 line is logged per retried answer. Observed in T1 at the HTTP transport and the logger.
- R2. WHEN the note write is answered anything else (404 `ContainerNotFound`, 404 `BlobNotFound`, 403 `AuthorizationPermissionMismatch`, 403 `AuthorizationFailure`, 409 `BlobAlreadyExists`, 500 `InternalError`, `ResourceNotFound` with a status other than 404, or `resourcenotfound` in lower case with status 404), THEN the mode throws that `RequestFailedException` with the same status and code after exactly one PUT, as on `main`. Observed in T2.
- R3. WHEN every attempt is answered 404 `ResourceNotFound` at once, THEN the mode throws the last `RequestFailedException` (404, `ResourceNotFound`), not a cancellation, after exactly 15 PUTs with the deployed 30 s budget, the last at 28 s after the first, with 14 E3 lines. Observed in T3. If the last attempt has not returned when the 30 s budget ends, the linked publication token cancels it and the mode throws a cancellation instead; the phase outcome is the same (Q3).
- R4. WHEN the note succeeds and the inventory write is answered 404 `ResourceNotFound`, THEN the mode throws it after one inventory PUT. Observed in T4.
- R7. WHEN the note write fails in transport (the handler throws `HttpRequestException`, or a transport timeout), THEN the mode throws what `main` throws (a `RequestFailedException` with status 0 and the `HttpRequestException` inside, or an `OperationCanceledException`) after one PUT, with no E3 line. Observed in T6 and T7.
- R5. WHEN the host composes `start-line` mode, THEN the handler receives the host's `ILogger<StartLineHandler>`. Observed in T5.
- R6. Everything else `main` does in `start-line` is unchanged: existing `StartLineHandlerTests` and `BlobArtifactStoreTests` pass unchanged.

## 7. Invariants and safety

- I1. No retry of any write other than the start-line note.
- I2. No change to any timeout, deadline or process-lifetime sum. The retry spends only budget `main` already grants. The start-line budget sum that `VerifierHostOptionsValidator` checks (lines 45 and 67 to 71: cleanup 10, token 10, metadata 10, watermark 10, Blob I/O 30) stays 70 s, below `ProcessLifetime` 120 s; the fixed 10 s receipt budget is outside that sum; the Job deadline stays 150 s (`a01-runtime-limits.json`).
- I3. A persistent `ResourceNotFound` still fails the run, at most about 30 s later than on `main`, with the same phase failure. The exception is the same 404 when the last attempt returns before the budget ends, and a cancellation otherwise (R3).
- I4. No hard safety rule is touched: no Azure credentials in the session, no infrastructure change, no workflow or trigger change, no identity or network change.

## 8. Test map

All tests are in `StartLineStorageReadinessTests` (`[Trait("Category", "Unit")]`). Each runs the real `StartLineHandler`, built through `ActivatorUtilities` as the host builds it, over the real `BlobVerifierArtifactStore` and a real `Azure.Storage.Blobs` 12.30.1 `BlobContainerClient` (`Retry.MaxRetries = 0`). Only the HTTP transport is scripted: each PUT gets the next scripted response. Kafka uses the existing `HostEvidenceBuilder` fakes with `Query = _ => new WatermarkOffsets(0, 0)`. `BlobIoTimeout` is set to 30 s, the deployed value, except in T3's 10 s case. Log entries are captured by a test `ILoggerProvider` registered with `AddLogging`, so the category is the one the host's pipeline gives. The test advances `ManualKafkaTimeProvider` by 1000 ms only while the handler waits after an error answer. Error responses carry `x-ms-error-code: <code>` and an XML body `<Error><Code><code></Code>...`, as `BlobArtifactStoreTests.Error` does.

| Test | Requirement | Script | Expected |
|---|---|---|---|
| T1 `Given_FirstWriteAnsweredResourceNotFound_When_StorageBecomesReady_Then_StartLinePublishesBothArtifacts` | R1 | 404 `ResourceNotFound`, 201, 201 | returns 0; requests `["PUT note","PUT note","PUT inventory"]`; every `If-None-Match` is `*`; request times `[0, 2000, 2000]` ms from the first; log lines exactly `["Warning start_line_storage_not_ready attempt 1 status 404 error_code ResourceNotFound; retrying the start-line write."]`; that one entry has event ID 1 and category `Parchwell.Verifier.StartLine.StartLineHandler`, as delivered through a logging provider registered with `AddLogging` |
| T2 `Given_FirstWriteAnsweredOtherFailure_When_Publishing_Then_FailsOnFirstAttemptUnchanged` (8 cases) | R2 | (404 `ContainerNotFound`), (404 `BlobNotFound`), (403 `AuthorizationPermissionMismatch`), (403 `AuthorizationFailure`), (409 `BlobAlreadyExists`), (500 `InternalError`), (400 `ResourceNotFound`), (404 `resourcenotfound`), then 201, 201 | throws `RequestFailedException` with that status and code; requests `["PUT note"]` |
| T3 `Given_ResourceNotFoundThroughoutBudget_When_Publishing_Then_LastResponsePropagatesAfterBoundedAttempts` (2 cases) | R3, I3, E2 | 40 x 404 `ResourceNotFound`; `BlobIoTimeout` 30 s (deployed) and 10 s (not deployed) | throws `RequestFailedException` 404 `ResourceNotFound` with no inner exception; all requests `PUT note`. 30 s: 15 requests, last at 28000 ms, log lines exactly attempts 1 to 14. 10 s: 5 requests, last at 8000 ms, log lines exactly attempts 1 to 4. Each line reads `Warning start_line_storage_not_ready attempt <n> status 404 error_code ResourceNotFound; retrying the start-line write.` |
| T4 `Given_InventoryWriteAnsweredResourceNotFound_When_NoteSucceeded_Then_InventoryFailsWithoutRetry` | R4, I1 | 201, 404 `ResourceNotFound`, 201 | throws 404 `ResourceNotFound`; requests `["PUT note","PUT inventory"]` |
| T6 `Given_FirstWriteConnectionFailure_When_Publishing_Then_FailsOnFirstAttemptWithoutRetry` | R7 | the handler throws `HttpRequestException("synthetic connection refused")`, then 201, 201 | throws `RequestFailedException` with `Status` 0 and an `HttpRequestException` inner exception; requests `["PUT note"]`; no log line |
| T7 `Given_FirstWriteTransportTimeout_When_Publishing_Then_FailsOnFirstAttemptWithoutRetry` | R7 | the handler throws `TaskCanceledException("synthetic transport timeout")`, then 201, 201 | throws an `OperationCanceledException`; requests `["PUT note"]`; no log line |
| T5 `Given_StartLineMode_When_HostComposed_Then_HandlerReceivesHostLogger` | R5 | `Program.BuildHost` with `ArgumentsFor("start-line", ...)` | exactly one `IVerifierModeHandler`, of type `StartLineHandler`; its single private instance field of type `ILogger<StartLineHandler>` is non-null (reflection, test code only) |
| Existing `StartLineHandlerTests`, `BlobArtifactStoreTests` | R6, I1 | unchanged | unchanged, all pass |

The scripted handler lets the test clock move after a thrown failure too, so a mutant that wrongly retries a transport failure fails the test instead of hanging it.

T2, T4, T6 and T7 pass on `main` by design: they are preservation tests. They hold R2, R4 and R7, which say the fix must leave those paths as `main` has them. Their power is shown by the mutants they kill (M1 to M3, M7, X1, X2 and X4 in 8.2).

Boundaries: T1 is one attempt below the bound; T3 is at the bound (`28 + 2 = 30` is not `< 30`, so the 15th answer is rethrown); mutant M5 (`<=`) shows the above-bound case. T2 covers the predicate's two parts separately: right code with wrong status (400), right status with wrong code (404 `ContainerNotFound`, `BlobNotFound`).

### 8.1 Reproduction on `main`

The final test file, copied unchanged into a clean `main` worktree at `0f769fb2`, builds with 0 warnings and gives `total: 15, failed: 4, succeeded: 11`. T1 fails with `Azure.RequestFailedException : synthetic failure` and `x-ms-error-code: ResourceNotFound`, which is trial 9's answer, raised from the real handler's line 58. Both T3 cases fail at their first failing assertion, the request count: `Expected: 15, Actual: 1` and `Expected: 5, Actual: 1`. T5 fails with `System.InvalidOperationException : Sequence contains no matching element` (no logger field). T2, T4, T6 and T7 pass on `main`, as R2, R4 and R7 require. The reproduction is deterministic: no real time or network is involved. Revision 1's file gave 3 failures of 11 on `aadb0c6d`; Revision 2's gave 3 of 14 on `0f769fb2`.

### 8.2 Mutation table

Each mutant was applied to the final prototype on `0f769fb2` by script, the test project was rebuilt and the class run under `timeout 240`. All 17 were killed (exit 2, none by hang). X1, X2 and X4 came from the Revision 1 reproduction check and Y1 to Y4 from the Revision 2 delta check, where they survived.

How mutants are written so that they compile under warnings as errors (for the receipt): M3 removes the status and code conditions and keeps only the budget condition in the exception filter; M8 keeps the log call but guards it with `logger is not null && attempt < 0`; Y3 keeps `budget` in the filter as `&& budget > TimeSpan.Zero` so it is not an unused parameter; Y4 adds a second catch clause after the first. The reproduction check wrote M3 as `when (error is not null && budget)` and M8 as `if (logger is null) ...` with a null-forgiving call; either form is acceptable if it builds with 0 warnings.

| Mutant | Break | Caught by |
|---|---|---|
| M1 | retry any 404 | T2 (`ContainerNotFound`, `BlobNotFound`) |
| M2 | retry `ResourceNotFound` at any status | T2 (400 case) |
| M3 | retry any `RequestFailedException` | T2, T6 |
| M4 | no budget check | T3 |
| M5 | budget `<=` instead of `<` | T3 |
| M6 | no wait between attempts | T1, T3 |
| M7 | inventory write also retried | T4 |
| M8 | retry log never written | T1, T3 |
| M9 | fix removed | T1, T3 |
| M10 | logger parameter not host-resolvable (`ILogger` instead of `ILogger<StartLineHandler>`) | T1, T3, T5 |
| X1 | also retry `RequestFailedException` with status 0 (transport failure) | T6 |
| X2 | compare the error code ignoring case | T2 (`resourcenotfound` case) |
| X4 | also retry an `OperationCanceledException` inside the budget | T7 |
| Y1 | log `attempt 1` on every retry | T3 |
| Y2 | log with event ID 2 | T1 |
| Y3 | bound hard-coded to 30 s instead of `BlobIoTimeout` | T3 (10 s case) |
| Y4 | rethrow a new `RequestFailedException` wrapping the last one | T3 (inner exception) |

## 9. Never-ran inventory

Evidence: trials 7, 8 and 9 (artifacts and job logs above) and the 38 job logs of section 2.3.

| Stage on the changed path | Ran live? | Offline test that runs it for real |
|---|---|---|
| Start-line note write answered success on the first attempt | Yes: trials 1 to 8 and every passing start-line in section 2.3 | Existing `StartLineHandlerTests`; T4's first response |
| Note write answered 404 `ResourceNotFound` | Yes, once: trial 9 (as read by Hari) | T1, T3 |
| Predicate match, wait on `TimeProvider`, second attempt | No | T1 (real handler, store and SDK; scripted transport) |
| Second attempt succeeds and inventory follows | No | T1 |
| Exhaustion and rethrow of the last answer | No | T3 |
| Non-matching answer on the first attempt | No live instance seen | T2 |
| Transport failure or timeout on the first attempt | No live instance seen | T6, T7 |
| E3 line through the host's logger into the container log | No | T5 (host injects the logger); delivery to `ContainerLogV2` is DA2 |
| Live storage clears `ResourceNotFound` within 28 s | No | None possible offline: DA1 |

## 10. Deployment assumptions

- DA1. When live storage answers the start-line note write 404 `ResourceNotFound` after the ephemeral apply, a later attempt within 28 s is answered success. Not verified: section 2.4 found no documentation, and trial 9 is the only live instance. If wrong, the run fails as on `main`, with the same answer, about 28 s later, plus E3 lines. Needs Hari's acceptance by name (Q3, Q9).
- DA2. Verifier warnings written through the host logger reach the container's stdout and `ContainerLogV2`. Support, not proof: Hari read trial 9's verifier exception in `ContainerLogV2`. Failure only loses the E3 lines, not a result.
- DA3. `Azure.Storage.Blobs` 12.30.1 sets `RequestFailedException.ErrorCode` from the live `x-ms-error-code` header as it does from the scripted one. T1 to T3 run the pinned SDK; the live header's presence is as Hari read it.

## 11. Effect on workflows and scenarios

- `b10-abrupt-trial.yml` (B10 abrupt diagnostic): on `0f769fb2` its first Job is start-line (`b10_abrupt.py` lines 619 to 623); covered. Decision 6762516530 redesigns the B10 restart path and may change this sequence. It keeps registration and authenticated hashes, but does not state whether start-line stays the first Job. Its journal upload by the parent and the successor's journal read are new early blob writes and reads that this fix does not cover; the redesign should take the same readiness choice (Q1, Q6).
- `scenario.yml`: start-line runs in every repeat of A01, B04 and B10 (`phase.py` `run_repeats`, `run_b10_repeats`) and in B06 and B07 (`ledger_batch.py`); each start-line Job gets the retry, not only the first after the apply. The probe batch items (B01, B03, B05, C08, C12, F04), idle observation (A02 to A04), B08 and F06 run no start-line; their first writers (seed, action, idle Jobs) are unchanged and not covered. None of their first Jobs failed in section 2.3.
- `cluster-smoke.yml`: started no worker Job in either run; no effect.
- `dotnet-verification.yml`: runs the new tests. `verifier-c5-mutation.yml`: `StartLineHandler.cs` is not named in `docs/operations/verifier-mutation.md`'s targets; no effect expected (to confirm in implementation).
- `sweeper.yml`, `docs-advisory.yml`: no effect.
- No published result changes; no scenario's expected outcome changes.

## 12. Acceptance commands

Each was run on the prototype: a scratch worktree of `main` at `0f769fb2` with the section 4.1 code and test changes and nothing else (source diff SHA-256 `bac1f3632e29470c86714f4864978e0cf8851228c37fe3f51cdf897711c78b77`, unchanged from Revision 1; test file SHA-256 `a618f278f91a90aca157fc2825c4e7f4ec8dca9150c7bbd0479922a99bbdb33e`). The prototype is not committed; the implementer writes its own code to the contracts and reruns every command, recording output in the receipt.

1. On a clean `main` worktree at `0f769fb2` with only the new test file: `dotnet build tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --no-restore` then `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --no-build -- --filter-class Parchwell.Verifier.Tests.Host.StartLineStorageReadinessTests`. Observed: build 0 warnings; `total: 15, failed: 4, succeeded: 11`; failed T1, both T3 cases, T5.
2. On the branch, the same filter plus `--filter-class Parchwell.Verifier.Tests.Host.StartLineHandlerTests --filter-class Parchwell.Verifier.Tests.Storage.BlobArtifactStoreTests`. Observed: `total: 85, failed: 0, succeeded: 85`.
3. Repetition: the readiness class 20 times in a loop, each under `timeout 240`. Observed: 20 of 20 runs `succeeded: 15`.
4. Mutations M1 to M10, X1, X2, X4 and Y1 to Y4 of 8.2, one at a time, class run under `timeout 240`; exit 124 is recorded as "killed by hang" and the work continues. Observed: 17 of 17 killed.
5. `./scripts/verify-dotnet.sh` with Docker running. Observed on the final prototype at `0f769fb2`: exit 0, "Verification passed"; Architecture 17, Enrichment 56, Kafka 491, LoadGen 1748, Scenarios 340 and Verifier 7404 tests passed (Verifier 7389 plus the 15 new tests). Earlier attempts failed only in `PhantomSpikeTests` because the Docker daemon was not running (no Docker endpoint), an environment fault; each passed after the daemon was started.
6. `npm run check:docs`. Observed on the plan branch with this revision: exit 0, `items/404/plan-storage-readiness-race-fix.md: no issues found`. The implementer runs it on the changed files.

## 13. Sources

- Decision 6761783572 and the catch-up comments of section 1.
- On `main` at `aadb0c6d`, each unchanged at `0f769fb2` (`git diff --quiet` over these paths): `src/Parchwell.Verifier/StartLine/StartLineHandler.cs` lines 14 to 80; `VerifierHostOptionsValidator.cs` lines 44 to 71 and 475 to 491; `Common/Storage/BlobVerifierArtifactStore.cs` lines 38 to 50; `Program.cs` lines 245 to 258 and 176; `infra/ephemeral/storage.tf`; `infra/ephemeral/identity.tf` lines 3 to 27; `infra/scripts/phase/phase.py` lines 23 to 24, 604 to 614, 669 to 776, 1225 to 1253; `infra/scripts/phase/b10_abrupt.py` line 621; `charts/parchwell/templates/jobs.yaml` line 29; `docs/operations/a01-runtime-limits.json`; `docs/operations/verifier-contract.md` lines 124, 290 and 302; `docs/blueprint.md` lines 544 to 546 (the Job retry rule, `backoffLimit: 0`, is unchanged by an in-process write retry); `.github/CODEOWNERS`; `Directory.Packages.props` lines 14 and 15.
- GitHub Actions job logs of the 38 jobs of section 2.3; artifacts 13742914191, 13742900343, 13704155185.
- Microsoft pages of section 2.4, read 2026-11-10.

## 14. Plan-check questions

- (a) Runtime-sized effort: none uncapped. Mutations are fixed at 17; repetition at 20; the retry itself is capped at 15 attempts by E2. The log search of section 2.3 was a planning read, already done.
- (b) Stricter than the parent: decision 6761783572 asks for the minimum change that stops this failure. The plan is stricter in two ways: it retries one write only, and only on one exact answer, so a real missing container (`ContainerNotFound`) or access failure (403) still fails at once (T2).
- (c) Dependencies not on `main` or in the pull request: live storage behaviour (DA1), log delivery (DA2) and the live header (DA3). The SDK behaviour is exercised with the pinned package.

## 15. Open questions for Hari

- Q1. Which candidate of section 3.1. Options: V, retry inside the Blob budget (this plan); H1, retry with waits 10, 20 and 40 s; H2, fixed delay after the apply, length open; P, bounded readiness poll from the runner or from the cluster; R, runner-side probe in `check_dns` (the runner form of P). Recommendation: V. Strongest counterargument: H2 and the runner form of P cover every scenario's first writer and live in an unprotected path, while V changes protected verifier code and covers start-line only; and V's 28 s may be too short, which H1 addresses at the cost of changing `ProcessLifetime` and the Job deadline.
- Q2. Retry predicate. Options: exactly 404 `ResourceNotFound` (recommended); also 403 `AuthorizationPermissionMismatch`, for role propagation. For widening: the Entra ID page says role assignments take "up to 10 minutes" and, in another section, "up to 30 minutes", and names `AuthorizationPermissionMismatch` as what a request may get meanwhile (section 2.4). A future run whose verifier grant is young may get that 403, and this predicate will not retry it. Trial 9 is not such a case: Hari read a 404. Against: no 403 has been observed in any run, and retrying a 403 delays every real access failure by up to 30 s and hides a missing grant for that long.
- Q3. Bound. Options: inside the existing 30 s `BlobIoTimeout` with a 2 s wait (recommended, no timeout changes); a separate larger readiness budget, which adds to the validated 70 s sum and, past 50 s more, reaches `ProcessLifetime` 120 s, so it changes policy values. Counterargument: 30 s may be too short; nothing measured says how long the condition lasts. Two effects of the recommended option, disclosed: (1) "the same answer" on exhaustion holds only when the last attempt, started at about 28 s, returns before 30 s; otherwise the linked publication token cancels it and the mode throws a cancellation, not the 404. The phase outcome is the same (`b10_abrupt_publication_failed` at boundary `metadata`, `b10_abrupt.py` lines 619 to 623) but the exception text in the container log differs. (2) The note and the inventory share one 30 s budget on `main`. A note that succeeds late leaves the inventory write only what remains. The configured values and the validated sum are unchanged (I2), but the inventory's effective budget shrinks, to about 2 s after a note that succeeds at 28 s.
- Q4. Log line E3: its name `start_line_storage_not_ready`, level Warning and text. Recommendation: as written. Counterargument: a new operator-visible name.
- Q5. Failure name on exhaustion. Recommendation: unchanged (`b10_abrupt_publication_failed` at boundary `metadata` for B10 abrupt; the existing outcome elsewhere). Option: a new name for exhausted readiness. Counterargument: a distinct name would tell a later reader that the retry ran out.
- Q6. Scope across repeats. Recommendation: every start-line Job (all repeats; A01, B04, B06, B07, B10). Option: only the first start-line after the apply, which needs a new input from the phase script. Counterargument: in later repeats the condition is not expected, and a retry there could mask a new fault for up to 28 s, though it still fails if it persists.
- Q7. Contract text E4 in `docs/operations/verifier-contract.md` (not a protected path; it is published contract wording). Recommendation: as written.
- Q8. Implementing tool: not named for this pull request.
- Q9. Accept DA1 by name, or require something else first.
- Q10. Protected paths: `src/Parchwell.Verifier/` and `tests/Parchwell.Verifier.Tests/` match `Parchwell.Verifier*/`, so Hari merges. `docs/operations/` is not protected.

Non-blocking note, nothing decided. Collecting the start-line Job's log into the public artifact is not needed for this fix: Container Insights already holds it privately, and container logs carry tenant and client IDs, which the public artifact must not carry. If it were wanted, it would need its own redaction design.

## 16. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when any of these holds:
- It would need to change a contract (E1 to E5), a requirement, the scope of 4.1 and 4.2, a literal expected value of section 8, or a timeout.
- A test on `main` or the branch gives a result other than section 8 states, or a mutant of 8.2 survives.
- Before this pull request merges, another change on `main` or in a pull request about to merge changes any of: `src/Parchwell.Verifier/StartLine/StartLineHandler.cs`; `src/Parchwell.Verifier/Program.cs` at the start-line registration (line 176) or the Blob client options (lines 245 to 258); `src/Parchwell.Verifier/Common/Storage/BlobVerifierArtifactStore.cs`; `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` start-line budgets; `tests/Parchwell.Verifier.Tests/Host/StartLineHandlerTests.cs`, `tests/Parchwell.Verifier.Tests/Storage/BlobArtifactStoreTests.cs` or `tests/Parchwell.Verifier.Tests/Support/HostEvidenceBuilder.cs`; the paragraph at line 302 of `docs/operations/verifier-contract.md`; the start-line values of `docs/operations/a01-runtime-limits.json` or `charts/parchwell/`; or the start-line step of `infra/scripts/phase/b10_abrupt.py` (lines 619 to 623), `phase.py` (`run_repeats`, `run_b10_repeats`) or `ledger_batch.py`.
- A change to `items/404/state.md` alone does not stop the work; the implementer rebases it.

## 17. Changes since Revision 1

### 17.1 Revision 3, from the delta check of Revision 2 (`224a5099`)

| Finding | Change |
|---|---|
| B1: E3's attempt number not held (mutant Y1 survived) | T3 asserts the exact lines, attempts 1 to 14 (and 1 to 4 in the 10 s case); Y1 added to 8.2, killed by T3 |
| B2: E3's event ID not held (Y2 survived) | T1 asserts event ID 1 and category `Parchwell.Verifier.StartLine.StartLineHandler` through an `AddLogging` provider; Y2 killed by T1 |
| B3: E2's bound only tested at 30 s (Y3 survived) | T3 gains a 10 s case: 5 PUTs, last at 8000 ms, 4 lines; Y3 killed |
| B4: "rethrown unchanged" not held (Y4 survived) | E2 says the same instance; T3 asserts no inner exception; Y4 killed |
| N-a: wrong citation for `AuthorizationPermissionMismatch` | Section 2.4 cites and quotes the SAS error codes page and the 403 troubleshooting page instead |
| N-b: `docs/operations/` is not protected | Section 3, the H1 column of 3.1, Q7 and Q10 corrected: Hari merges because of the verifier source and test paths |
| N-c: 8.1 depended on assertion order | 8.1 states the first failing assertion of each failing test on `main` |
| Mutant forms for the receipt | 8.2 records how M3, M8, Y3 and Y4 are written to compile |

Nothing else changed: no contract, requirement, open question or other expected value. Counts that follow from the added cases: the class has 15 tests; `main` gives 4 failures of 15; 17 mutants; the combined run is 85.

### 17.2 Revision 2, from the reproduction check of Revision 1

Revision 1 is commit `ffbb38b9`. Each finding of its fresh reproduction check, and each catch-up item, maps to these changes:

| Finding | Change |
|---|---|
| B1 (blocking): mutant X1, which also retries status 0 transport failures, survived | E1 names status 0, case and cancellation; new R7; new tests T6 (connection failure) and T7 (transport timeout); X1 and X4 added to 8.2 and killed by T6 and T7; the scripted handler lets the clock move after a thrown failure |
| N1: the Entra ID page does name `AuthorizationPermissionMismatch` and gives "up to 30 minutes" | Section 2.4 quotes both; the inference in 2.4, the basis paragraph of 3.1 and Q2 corrected |
| N2: `main` moved to `0f769fb2`; the capture-loss fix merged; the stop rule fired on the state file | Baseline updated; 4.3 rewritten; stop rule narrowed to code, test, contract and phase files; every command rerun on `0f769fb2`; Kafka count now 491 |
| N3: decision 6762516530 | Section 1 and 4.3 record it; section 11's B10 row says the B10 path may change and whether start-line stays first is not stated |
| N4: exhaustion can end in a cancellation; the inventory's effective budget shrinks | R3 and I3 corrected; both disclosed under Q3 |
| N5: T2 and T4 cannot fail on `main` | Section 8 states they are preservation tests and names the mutants that show their power |
| N6: mutant X2 (case-insensitive code) survived | T2 gains a 404 `resourcenotfound` case; X2 added to 8.2 and killed by T2 |
| Catch-up items 1 to 5 | Partial-frame plan removed (stopped); PR3b recorded as paused at `37128767`; trial gate amendment added; the B10 first-Job assumption added to section 11 and the stop rule; the redesign's journal blob writes noted as not covered |

No contract, literal or expected value of Revision 1 changed except: T2 has 8 cases, not 7; the class has 14 tests, not 11; the reproduction on `main` gives 3 failures of 14, not 3 of 11; 13 mutants, not 10. The open questions are the same ten, with Q2 and Q3 corrected.

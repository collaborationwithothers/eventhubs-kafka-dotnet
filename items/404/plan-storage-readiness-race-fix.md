# Storage readiness race fix: contract-grade plan

Revision 4. Issue 404. Plan branch `claude/404-plan`. Baseline: `main` at `0f769fb240d1de50192f4c8eefaf12698002efc7`. Revision 3 was posted as [pointer 6764508653](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6764508653) and checked in [6764643941](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6764643941). This revision is not posted yet; it awaits a fresh reproduction check.

In trial 9 the first blob write of the run, about 20 seconds after the ephemeral apply ended, was answered `404 ResourceNotFound`, and the run failed before any producer started. Hari chose a fixed 2-minute wait after the ephemeral apply and before the first worker Job ([decision 6765011642](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6765011642)). This plan puts that wait at the end of the phase script's private DNS check, which every workflow job that applies the ephemeral layer and then runs Blob-writing work already calls in between, so no workflow file changes.

## 1. Goal and authority

- [Decision 6761783572](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6761783572) items 3 and 4: plan the storage readiness race fix in its own pull request; state its effect on every scenario that uses the same phase path; every gate, timeout, failure name, protocol change and the implementing tool goes to Hari.
- [Decision 6762516530](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6762516530) amends the trial gate: no further B10 trial until this fix merges and the B10 restart redesign is implemented. This plan authorizes no trial and no dispatch.
- [Decision 6765011642](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6765011642) (Q1 of Revision 3): "After the ephemeral apply and before the first worker Job, wait a fixed 2 minutes for storage to be ready." Hari chose this over the retry Revision 3 recommended, having been told its costs: no documented basis for any length, the delay is paid on every run, and a run that would have hit the condition leaves no trace that it did. The retry questions fall away. "The 2-minute value is a policy value Hari set on 2026-11-10 without measurement; it is not a measured readiness time (AGENTS.md hard safety rule 11)."
- Catch-up: `items/404/state.md` on `main` (covers comment 6756884940), then every later comment, read in full; newest read 6765011642 (created 2026-11-10T10:29:28Z), 197 comments, none edited or deleted since the previous read. Comments read for the first time in this revision: 6764508653 (Revision 3 pointer), 6764643941 (its plan check) and 6765011642.

## 2. What this plan changes, in one paragraph

`Phase.check_dns` in `infra/scripts/phase/phase.py` (lines 604 to 614 on `main`) resolves the Event Hubs and Blob private endpoint names and returns 0. After this change, when both names resolve as required, it prints one log line and then waits `STORAGE_READINESS_WAIT_SECONDS` = 120 through `Phase.sleep`, then returns 0. When a name does not resolve, it fails as on `main`, without waiting. The verifier, the charts, the Terraform and the workflows do not change.

## 3. The failure and what is known about its cause

### 3.1 Facts from public logs (computed by script from the GitHub Actions job logs)

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

### 3.2 Facts not seen by this session

The `404` and `x-ms-error-code: ResourceNotFound` come from the verifier container log that Hari read in Log Analytics (`ContainerLogV2`). This session holds no Azure access and has not seen that log. No offline test can produce that answer through this plan's change; it is the reason for the wait, not something the wait observes.

### 3.3 Search of earlier runs for the same failure

Script `fetch.sh` listed every run of `scenario.yml` (49), `cluster-smoke.yml` (2) and `b10-abrupt-trial.yml` (9), and fetched the job log of every job whose "Apply the saved ephemeral plan" step ran: 38 jobs in 36 runs. `verifier-c5-mutation.yml`, `sweeper.yml`, `dotnet-verification.yml` and `docs-advisory.yml` never apply the ephemeral layer.

- 36 of the 38 jobs started a worker Job; 18 of those first Jobs were `start-line`.
- Only one first Job warned `wait exit`: trial 9's start-line.
- Public job logs do not hold container logs, so a `ResourceNotFound` that did not fail a Job would be invisible here.
- 4 of the 18 start-line first Jobs started within 20 s of the verifier role assignment: trial 9 (14.8 s, failed), run 42189889469 C1 (15.7 s, passed), trial 7 (16.0 s, passed), run 41866017867 (18.1 s, passed).
- Run 42189889469 C1 recreated the account 5.4 minutes after the previous deletion, sooner than trial 9's 15.2 minutes, and passed.

So neither a young role assignment nor a fast recreation explains trial 9 by itself. One failure in 18 start-line first Jobs is an observation, not a rate (synthetic and live counts both too small).

### 3.4 Official documentation

- [Common REST API error codes](https://learn.microsoft.com/rest/api/storageservices/common-rest-api-error-codes) (read 2026-11-10): `ResourceNotFound`, Not Found (404), "The specified resource doesn't exist." It is a code common to all storage services, not a Blob-specific code.
- [Blob Storage error codes](https://learn.microsoft.com/rest/api/storageservices/blob-service-error-codes) (read 2026-11-10): `ContainerNotFound`, 404, "The specified container does not exist."; `BlobNotFound`, 404.
- [Create Container, Remarks](https://learn.microsoft.com/rest/api/storageservices/create-container#remarks) (read 2026-11-10): "Containers are created immediately within the storage account." This is the data-plane Create Container call. Our containers are created through Azure Resource Manager (`azapi_resource`), so the page does not settle it.
- [Authorize access to blobs by using Microsoft Entra ID](https://learn.microsoft.com/azure/storage/blobs/authorize-access-azure-active-directory#role-assignment-propagation-delays-for-blob-data-access) (read 2026-11-10). Under "Role assignment propagation delays for blob data access": "When you assign roles or remove role assignments, it can take up to 10 minutes for changes to take effect." The same section continues: "If you set the appropriate allow permissions to access data via Microsoft Entra ID and can't access the data, for example you're getting an "AuthorizationPermissionMismatch" error, be sure to allow enough time for the permissions changes you made in Microsoft Entra ID to replicate." Under "Azure built-in roles for blobs", the same page says: "Azure role assignments can take up to 30 minutes to propagate." So the page gives two figures, 10 and 30 minutes, and names `AuthorizationPermissionMismatch` as an example of what a request gets while a new grant replicates. That code does not appear on the Blob Storage error codes or Common REST API error codes pages. [Shared access signature error codes](https://learn.microsoft.com/rest/api/storageservices/sas-error-codes) (read 2026-11-10) lists `AuthorizationPermissionMismatch` with new status code "403 (Forbidden)" and message "This request is not authorized to perform this operation using this permission.", for shared access signatures. [Troubleshoot 403 errors in Azure Blob Storage](https://learn.microsoft.com/troubleshoot/azure/azure-storage/blobs/authentication/storage-troubleshoot-403-errors) (read 2026-11-10) lists, under "Common 403 errors related to authorization failures", error code `AuthorizationPermissionMismatch` with causes including "**Insufficient Azure RBAC role**: The user or service principal lacks the required role for the operation." Inferred, not stated by any one page: a request whose role grant has not yet taken effect gets a 403, not a 404. The SAS error codes page also lists an old status "404 (Not Found)" for `AuthorizationPermissionMismatch` on service versions before 2015-04-05, so this holds for current service versions only.
- [Troubleshoot private endpoint creation](https://learn.microsoft.com/troubleshoot/azure/private-link/troubleshoot-private-endpoint-creation-failed#resolution-b) (read 2026-11-10): deletion is asynchronous; "The network adapter, DNS records, and the connection on the target resource are cleaned up after the `delete` call returns"; "Wait at least 20 minutes before re-creating." Its symptom is `PrivateEndpointAlreadyExists` at creation, which trial 9 did not show (the apply succeeded).
- No Microsoft page was found that confirms or refutes a data-plane delay after an ARM-created storage account or container, or after a private endpoint becomes ready. Searched 2026-11-10 with the Microsoft Learn search for storage account propagation, `ResourceNotFound` after creation and private endpoint recreation.

Inference, not verified: the storage front end did not yet serve the recreated account, or its private endpoint connection, to the verifier's request. Role propagation is a weaker candidate for trial 9: the Entra ID page names `AuthorizationPermissionMismatch` (a 403) as the example answer while a grant replicates, and Hari read a 404. The fixed wait of this plan does not depend on which cause is right, but nothing above says how long either condition lasts: the documented role figures are 10 and 30 minutes, longer than the 2-minute wait (DA1).

## 4. Where the wait runs

The wait must run after `Apply the saved ephemeral plan` and before the first worker Job. Three places can hold it:

- P1, recommended: the end of `Phase.check_dns` (`infra/scripts/phase/phase.py`, not protected). Every workflow job that applies the ephemeral layer and runs Blob-writing work already calls `phase.sh check-dns` between the apply and its first worker step (section 12, test T5). No workflow changes. The wait runs only after the private endpoint names resolve, so a DNS failure still fails at once.
- P2: `apply-ephemeral` in `infra/scripts/lifecycle/lifecycle.sh` (not protected). It would also delay `cluster-smoke.yml`, whose smoke Job writes no blob, and it mixes a readiness policy into the Terraform step.
- P3: a new workflow step, for example `phase.sh wait-storage`, in each of the five jobs that call `check-dns` today. `.github/workflows/` is protected and each workflow has a committed copy under `docs/operations/workflows/` that must change byte for byte (AGENTS.md hard safety rule 8), so Hari merges, and the step's name and place become visible in each run's step list.

Q1 asks Hari to choose. The rest of this plan specifies P1.

## 5. Scope

### 5.1 Files allowed to change

1. `infra/scripts/phase/phase.py`: one module constant, `STORAGE_READINESS_WAIT_SECONDS = 120`, with a comment that it is Hari's policy value from decision 6765011642 and not a measurement; and, at the end of `check_dns`, after the loop and before `return 0`, the E2 log line and `self.sleep(STORAGE_READINESS_WAIT_SECONDS)`.
2. `infra/scripts/phase/tests/test_phase.py`: tests T1 to T4 in class `PhaseTests`, after `test_check_dns`, and module constants for their expected values.
3. New file `infra/scripts/phase/tests/test_storage_readiness_workflow.py`: test T5.
4. `docs/operations/` text only if Hari answers Q4 with contract text; none is planned.
5. `items/404/state.md` and a new receipt `items/404/storage-readiness-receipt.md`.

### 5.2 Paths and behaviour that must not change

- `.github/workflows/**` and `docs/operations/workflows/**` (protected; P1 needs no change there).
- `src/**`, `tests/Parchwell.*/**`, `charts/**`, `infra/ephemeral/**`, `infra/persistent/**`, `infra/cluster/**`, `infra/scripts/lifecycle/**`, `scenarios/**`, `results/**`, every `expected.yaml`, `docs/blueprint.md`.
- `docs/operations/verifier-contract.md`: no change. Its line 470 ("Note/result uploads are create-only, with no application retry, overwrite, delete or container creation.") stays true, because no application retry is added.
- `docs/operations/a01-runtime-limits.json`: no change unless Hari answers Q3 with that file.
- The existing test `test_check_dns` keeps its inputs and expected exit codes.
- No other phase command, deadline, timeout, failure name or `phase-status.json` field changes.

### 5.3 Shared with other open work on issue 404

- PR3b classification (paused at Revision 2 `37128767`): shares `items/404/state.md`; it changes `infra/scripts/phase/b10_abrupt.py` and phase tests, not `phase.py`'s `check_dns`.
- Offline end-to-end test plan (`items/404/plan-offline-e2e-test.md`, Revision 1 at `f75d6ac`): shares `items/404/state.md` only; it lists `infra/**` as must-not-change.
- B10 restart redesign (decision 6762516530): its later implementation may change `b10_abrupt.py` and the B10 Job sequence; section 12 depends on `b10-abrupt-trial.yml` keeping `check-dns` before its run step, which T5 checks.
- Whichever merges later rebases `items/404/state.md`.

## 6. External contracts

- E1, value. `STORAGE_READINESS_WAIT_SECONDS = 120` in `infra/scripts/phase/phase.py`. Hari's policy value (decision 6765011642), not a measurement. Where the value lives is Q3.
- E2, log line. When the DNS check passes, `check_dns` prints exactly one line to standard output, before the wait: `storage_readiness_wait seconds=120 policy=decision-6765011642`. It appears in the public job log of the `Require private endpoint DNS answers` step. Whether to log, and its text, is Q2.
- E3, order. The wait runs once, after both names resolve as required and before `check_dns` returns 0. A DNS failure raises `Failure("private DNS check failed: <host>", 1)` as on `main`, with no log line and no wait.
- E4, mechanism. The wait goes through `Phase.sleep`. `check_dns` sets no command deadline, so `Phase.sleep` passes the full 120 s to `time.sleep` (`phase.py` lines 354 to 358 at the prototype).
- E5, unchanged externals: every workflow file, step name, step order and `if:` condition; the `check-dns` step's exit codes; every `phase-status.json` field; the verifier, its failure names and its contract.

## 7. Requirements

- R1. WHEN `phase.sh check-dns` runs and both private endpoint names resolve to exactly their recorded addresses, THEN it runs the two `resolve` commands, prints the E2 line once, waits 120 s once, and exits 0, in that order. Observed in T1 (ordered calls and standard output) and T4 (the process-level sleep).
- R2. WHEN a name resolves to anything else or the `resolve` command fails, THEN `check-dns` exits 1 after that command, with no wait and no E2 line. Observed in T2.
- R3. WHEN the existing `test_check_dns` inputs run (`["10.0.0.4"]` exits 0; `["10.0.0.4", "20.1.1.1"]` exits 1; exit 124 exits 1), THEN the exit codes are unchanged. Observed in `test_check_dns`.
- R4. The value is the named constant `STORAGE_READINESS_WAIT_SECONDS`, equal to 120. Observed in T3.
- R5. WHEN any workflow job applies the ephemeral layer and then starts Blob-writing work, THEN that job runs `phase.sh check-dns` exactly once, after the apply and before its first worker step; and the cluster smoke job, which writes no blob, has no such step. Observed in T5, which reads the real workflow files.

## 8. Invariants and safety

- I1. No workflow, trigger, identity, network, Terraform or chart change (AGENTS.md hard safety rules 8 and 9 untouched).
- I2. The wait adds 120 s of wall time to each job in section 12 and changes no deadline: the B10 orchestration clock (`b10_deadline(start=True)`, `b10_abrupt.py` line 617) and the batch admission clock (`start_batch_clock`, `phase.py` line 1158; `idle_observation.py` line 155; `ledger_batch.py` line 76; `probe_v2.py` line 833) all start inside the run steps, after `check-dns`.
- I3. A run that still meets the storage condition after the wait fails exactly as trial 9 did; the wait hides nothing it did not already hide, but it also records nothing about whether the condition occurred (decision 6765011642's stated cost).
- I4. The ephemeral environment lives about 120 s longer per job. Cleanup is unchanged: destroy runs with `if: always()` as before (AGENTS.md hard safety rule 6).

## 9. Test map

All tests are offline. T1 to T4 run the real `phase.main(['check-dns'])` against the existing `PhaseTests.prepare()` fixture, with only the external `resolve` commands scripted by `ScriptedCommands`. Its default answers are `["10.0.0.4"]` for the Event Hubs name and `["10.0.0.5"]` for the Blob name, which match the fixture's recorded addresses.

| Test | Requirement | Input | Expected |
|---|---|---|---|
| T1 `test_check_dns_answers_then_fixed_storage_readiness_wait` | R1, E2, E3 | default answers; `Phase.run` and `Phase.sleep` recorded into one ordered list | exit 0; events exactly `[('run', ('infra/scripts/phase/phase.py', 'resolve')), ('run', ('infra/scripts/phase/phase.py', 'resolve')), ('sleep', 120)]`; the only stdout line starting `storage_readiness_wait` is `storage_readiness_wait seconds=120 policy=decision-6765011642` |
| T2 `test_check_dns_failure_fails_without_storage_readiness_wait` | R2, E3 | Event Hubs answer `["10.0.0.4", "20.1.1.1"]` | exit 1; events exactly `[('run', ('infra/scripts/phase/phase.py', 'resolve'))]`; no `storage_readiness_wait` in stdout |
| T3 `test_storage_readiness_wait_is_the_policy_constant` | R4, E1 | none | `phase.STORAGE_READINESS_WAIT_SECONDS == 120` |
| T4 `test_check_dns_storage_readiness_wait_reaches_the_process_sleep` | R1, E4 | default answers; `Phase.run` scripted; `phase.time.sleep` recorded, `Phase.sleep` real | exit 0; `time.sleep` called exactly once, with 120 |
| T5 `test_wait_runs_between_ephemeral_apply_and_first_worker_job` | R5, E5 | the real `.github/workflows/*.yml`, parsed with PyYAML | for each job with a `lifecycle.sh apply-ephemeral` step: exactly one first-work step (`phase.sh run-repeats`, `phase.sh run-b10-abrupt-trial`, `phase.sh run-single-action-batch` or `lifecycle.sh install-smoke`); where `phase.sh check-dns` exists, exactly one, with apply < check-dns < first work; the observed map is exactly `{(b10-abrupt-trial.yml, trial): wait, (cluster-smoke.yml, smoke): no wait, (scenario.yml, batch): wait, (scenario.yml, full-C0): wait, (scenario.yml, full-C3): wait, (scenario.yml, probe): wait}` |
| `test_check_dns` (existing) | R3 | unchanged | unchanged |

T2, T5 and `test_check_dns` pass on `main` by design: they hold behaviour the change must keep. Their power is shown by the mutants they kill (W3 and W9 in 9.2).

### 9.1 Reproduction on `main`

On a clean `main` worktree at `0f769fb2` with only the test changes of 5.1 items 2 and 3, the six tests give `Ran 6 tests`, `FAILED (failures=2, errors=1)`:
- T1 fails at its first assertion on the event list: `Lists differ: [...('sleep', 120)] != [...]`. On `main`, `check-dns` returns straight after the second `resolve`, so the first worker Job starts with no wait. This is the offline form of trial 9's path: nothing between the apply and the first Blob write waits for storage.
- T4 fails: `time.sleep` is never called.
- T3 errors: `module 'phase' has no attribute 'STORAGE_READINESS_WAIT_SECONDS'`.
- T2, T5 and `test_check_dns` pass.

The full lifecycle suite on that worktree, `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`, gives `Ran 338 tests`, `FAILED (failures=2, errors=1)`, with exactly T1, T4 and T3 failing.

What cannot be reproduced offline: storage answering `404 ResourceNotFound`, and whether 120 s is enough (DA1).

### 9.2 Mutation table

Each mutant was applied to the prototype by script, and the six tests were run under `timeout 240`. Exit 124 counts as killed by hang. All 9 were killed.

| Mutant | Break | Caught by |
|---|---|---|
| W1 | no wait | T1, T4 |
| W2 | wait 60 s | T1, T3, T4 |
| W3 | log and wait before the DNS loop | T1, T2 |
| W4 | wait inside the DNS loop, once per name | T1, T4 |
| W5 | no log line | T1 |
| W6 | log text `storage_readiness_wait wait=...` | T1 |
| W7 | call `time.sleep` directly, bypassing `Phase.sleep` | killed by hang (the tests would really sleep 120 s each) |
| W8 | wait only when `PARCHWELL_SCENARIO` is `B10` | T1, T4 |
| W9 | scratch copy of `scenario.yml` with the probe job's `check-dns` step replaced by `run: "true"` | T5 |

## 10. Never-ran inventory

Evidence: trials 7, 8 and 9 and the 38 job logs of section 3.3.

| Stage on the changed path | Ran live? | Offline test that runs it for real |
|---|---|---|
| `check-dns` resolving both names and returning 0 | Yes: every job in section 3.3 that reached its run step | `test_check_dns`, T1 |
| `check-dns` failing on a wrong answer | No live instance seen | T2, `test_check_dns` |
| The E2 line and the 120 s wait after a passing DNS check | No | T1, T4 (real `Phase.sleep`; only `time.sleep` is recorded) |
| The wait in a real workflow step on the private runner | No | None possible offline: DA2 |
| Storage serving the first Blob write about 2 minutes after the apply | No | None possible offline: DA1 |

## 11. Deployment assumptions

- DA1. Two minutes after the ephemeral apply, storage serves the first Blob write. Not verified: section 3.4 found no documented storage readiness time; the only documented figures are for role propagation (10 and 30 minutes), which are longer. The 120 s is Hari's policy choice, not a measurement. If it is wrong, a run fails as trial 9 did, after the wait. Needs Hari's acceptance by name (Q6).
- DA2. The `check-dns` step on the private runner has no step timeout shorter than the wait. Checked in the workflow files: none of the five `check-dns` steps sets `timeout-minutes`, and each job's `timeout-minutes` is 360. That GitHub Actions applies no other per-step limit is not verified here (Q6).
- DA3. Standard output of the `check-dns` step reaches the public job log, as the existing `::group::` and `::warning::` lines of `phase.py` do. Failure only loses the E2 line (Q6).

## 12. Effect on workflows and scenarios

Added time is 120 s of wall time per job that runs `check-dns`, plus that much more ephemeral environment lifetime. It is estimated as the wait itself; the step's other work took under 2 s in the trial 8 and 9 logs (07:02:36 to 07:02:37 and 07:23:17 to 07:23:17).

| Workflow, job | What runs after the wait | Effect |
|---|---|---|
| `b10-abrupt-trial.yml`, `trial` | the B10 abrupt diagnostic trial (`run-b10-abrupt-trial`), first Job `start-line` | +120 s; the B10 orchestration budget starts later and is unchanged; job timeout 360 min |
| `scenario.yml`, `probe` | `run-repeats` for the scenario the dispatch selects | +120 s before its first worker Job |
| `scenario.yml`, `full-C0` | `run-repeats` | +120 s |
| `scenario.yml`, `full-C3` | `run-repeats` | +120 s |
| `scenario.yml`, `batch` | `run-single-action-batch` | +120 s before its first worker Job; the batch admission clock starts later and is unchanged |
| `cluster-smoke.yml`, `smoke` | the cluster smoke Job | none: no `check-dns` step; the smoke chart takes no storage values |
| `sweeper.yml`, `dotnet-verification.yml`, `docs-advisory.yml`, `verifier-c5-mutation.yml` | nothing | none: they apply no ephemeral layer (`dotnet-verification.yml` runs the new tests) |

Every scenario that `scenario.yml` routes to one of its four jobs pays 120 s for each selected job. The first Jobs of section 3.3 name F04, B08, F06, A02, B06 and B10; the other start-line first Jobs there do not name their scenario. Which scenario reaches which job is decided by `scenario.yml`'s job conditions; this plan does not restate them, and the per-job wait does not depend on them.

Deadline fit: the wait sits outside every Job's `activeDeadlineSeconds` (it runs before any Job exists) and outside every phase budget (I2). The only deadline that contains it is each job's 360-minute `timeout-minutes`.

## 13. Acceptance commands

Each was run on the prototype: a scratch worktree of `main` at `0f769fb2` with the 5.1 items 1 to 3 changes only (`phase.py` diff SHA-256 `7a58a6aa15a5eaf1591fbbaf6cb763f0632da71cea5597cd1390bc9bb17947de`; whole diff SHA-256 `baa51af49aa55d0bdc53a56b69fe9679647fd0956281cf1ad4151eeec60e19a5`; T5 file SHA-256 `7a1703a1934717cf50ea9c4dfc785adba4c64fe0dda9af9d49de337f07380839`). The pinned tools were installed with `bash infra/scripts/lifecycle/install-tools.sh <dir> ci`, which needs Python 3.12 first on `PATH`. The prototype is not committed; the implementer writes its own code to the contracts, reruns every command and records the output in the receipt.

1. On a clean `main` worktree with only the test changes, from `infra/scripts/phase/tests`: `python3 -m unittest test_phase.PhaseTests.test_check_dns test_phase.PhaseTests.test_check_dns_answers_then_fixed_storage_readiness_wait test_phase.PhaseTests.test_check_dns_failure_fails_without_storage_readiness_wait test_phase.PhaseTests.test_check_dns_storage_readiness_wait_reaches_the_process_sleep test_phase.PhaseTests.test_storage_readiness_wait_is_the_policy_constant test_storage_readiness_workflow`. Observed: `Ran 6 tests`, `FAILED (failures=2, errors=1)`, failing T1, T4 and T3.
2. The same command on the branch. Observed: `Ran 6 tests`, `OK`.
3. Repetition: command 2, 20 times, each under `timeout 120`. Observed: 20 of 20 passed.
4. Mutations W1 to W9 of 9.2. Observed: 9 of 9 killed (W7 by hang).
5. The CI lifecycle step, with the pinned tools on `PATH`: `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`. Observed on the branch: `Ran 338 tests`, `OK`, exit 0. On `main` with only the tests: `Ran 338 tests`, `FAILED (failures=2, errors=1)`, exit 1.
6. `PARCHWELL_TOOLS_DIRECTORY=<dir> python3 infra/scripts/lifecycle/command.py 600 -- bash infra/scripts/lifecycle/check-offline.sh`. Observed on the branch: exit 0.
7. `npm run check:docs`. Observed on the plan branch with this revision: exit 0, `items/404/plan-storage-readiness-race-fix.md: no issues found`.

`./scripts/verify-dotnet.sh` is not an acceptance command: no .NET file changes.

Environment-fault reruns: if a command fails only because of the environment (a stopped Docker daemon, a registry rate limit, a missing pinned tool), the implementer may rerun it at most twice. After that it records the gap in the receipt and continues (AGENTS.md "Before opening a pull request" rule 11).

## 14. Sources

- Decisions 6761783572, 6762516530 and 6765011642; the Revision 3 pointer 6764508653 and plan check 6764643941.
- On `main` at `0f769fb2`: `infra/scripts/phase/phase.py` lines 20 to 60, 337 to 354 (`run`, `sleep`), 604 to 614 (`check_dns`), 1158 (`start_batch_clock` call), 1534 (`check-dns` command entry); `infra/scripts/phase/b10_abrupt.py` lines 617 and 619 to 623; `idle_observation.py` line 155; `ledger_batch.py` line 76; `probe_v2.py` line 833; `infra/scripts/phase/tests/test_phase.py` lines 47 to 160, 690 to 694 and 1313 to 1320; `infra/scripts/tests/test_phase_bridge.py`; `.github/workflows/scenario.yml`, `b10-abrupt-trial.yml`, `cluster-smoke.yml` (steps and `timeout-minutes`), `dotnet-verification.yml` lines 55 to 81; `charts/smoke/`; `docs/operations/verifier-contract.md` line 470; `.github/CODEOWNERS`.
- GitHub Actions job logs of the 38 jobs of section 3.3; artifacts 13742914191, 13742900343 and 13704155185.
- The Microsoft pages of section 3.4, read 2026-11-10.

## 15. Plan-check questions

- (a) Effort that scales with a runtime count: none uncapped. Mutations are fixed at 9 and repetitions at 20. Environment-fault reruns are capped at 2 per command, with "record the gap and continue". The wait itself is a fixed 120 s per job.
- (b) Stricter than the parent: decision 6765011642 asks for a fixed 2-minute wait after the ephemeral apply and before the first worker Job. The plan adds that the wait runs only after the private DNS check passes, so a DNS failure still fails at once (R2), and it pins, by test, that every Blob-writing job keeps `check-dns` between the apply and its first worker step (R5).
- (c) Dependencies on neither `main` nor the pull request: storage readiness timing (DA1), the runner's step limits (DA2) and job log delivery (DA3). The workflow files T5 reads are on `main`.

## 16. Open questions for Hari

- Q1. Where the wait runs. Options: P1, the end of `check_dns` in `phase.py`, no workflow change (recommended); P2, `apply-ephemeral` in `lifecycle.sh`; P3, a new workflow step in each job (protected, Hari merges, committed copies change). Strongest counterargument to P1: a wait hidden inside a step named "Require private endpoint DNS answers" is easy to miss when reading a run, and a later edit to `check_dns` could drop it; P3 makes it visible as its own step.
- Q2. Log line. Options: E2 as written, `storage_readiness_wait seconds=120 policy=decision-6765011642` (recommended); a different name or text; no line. Counterargument: the line says only that the run waited, not whether storage was ready, so it adds a name without evidence.
- Q3. Where the 120 s lives. Options: a constant in `phase.py` with a comment naming decision 6765011642 (recommended); a new key in `docs/operations/a01-runtime-limits.json` (not protected; holds other runtime policy values); a workflow environment variable (protected). Counterargument: the runtime-limits file is where other policy values are reviewed together, and a constant in a script is easier to change without noticing.
- Q4. Contract text. Recommendation: none. No contract describes the order of workflow steps, and `docs/operations/verifier-contract.md` line 470 stays true because no application retry is added. Option: one sentence in a workflow document such as `docs/operations/b10-workflow.md`. Counterargument: an undocumented wait surprises the next reader of the run timeline.
- Q5. Which workflows and scenarios. Options: every job that calls `check-dns` (all of section 12 except cluster smoke; recommended, because trial 9's failure is not specific to B10); only B10 (`b10-abrupt-trial.yml` and B10 dispatches of `scenario.yml`), which needs a scenario condition in `check_dns`. Counterargument: in the 36 runs searched, no other run's first worker Job failed this way, and every A01, B04 and batch dispatch now pays 120 s per job.
- Q6. Deployment assumptions, each to accept by name: DA1 (two minutes is enough; not measured, and shorter than the documented role propagation figures), DA2 (no step limit shorter than the wait) and DA3 (the log line reaches the job log).
- Q7. Implementing tool: not named for this pull request.
- Q8. Merge: P1 touches no protected path, so under the standing rules the control session could merge it; whether Hari wants to merge it himself, given that it changes every Azure run's timing, is his call.

Non-blocking note, nothing decided: collecting the start-line Job's log into the public artifact is not needed for this fix; Container Insights holds it privately, and container logs carry tenant and client IDs.

## 17. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when any of these holds:
- It would need to change a contract (E1 to E5), a requirement, the scope of 5.1 and 5.2, or a literal expected value of section 9.
- A test on `main` or the branch gives a result other than section 9 states, or a mutant of 9.2 survives.
- Before this pull request merges, another change on `main` or in a pull request about to merge changes `Phase.check_dns`, `Phase.sleep`, `Phase.run`, the `check-dns` command entry, the `PhaseTests` fixture, the step order around `check-dns` in any workflow file, or the start of the B10 or batch clocks.
- A change to `items/404/state.md` alone does not stop the work; the implementer rebases it.

## 18. Changes since Revision 3

Revision 3 is commit `b5167340`. Revision 4 is a rewrite around Hari's decision 6765011642; it keeps section 3 (evidence, run search and citations) and replaces everything about the retry.

| Source | Change |
|---|---|
| Decision 6765011642, Q1: fixed 2-minute wait | The plan now specifies the wait at the end of `check_dns`; the verifier change, its tests, the retry contracts E1 to E5, the 10/20/40 s comparison and the retry questions (predicate, bound, retry log line, failure name on exhaustion, retry scope) are removed. |
| Control-session instruction (1): reproduce on `main` | 9.1: on `main`, `check-dns` returns with no wait; T1, T3 and T4 fail there and pass after the change. |
| (2): keep evidence and citations | Section 3 is Revision 3's section 2, with N4 applied. |
| (4): open questions | Q1 to Q8: placement, log line, where the value lives, contract text (line 470 needs no change), scope with per-job effect, DA1 to DA3 by name, implementing tool, merge. |
| (5): value is policy | Sections 1, 6 (E1), 11 (DA1) and 12 say so; section 12 and I2 state the deadline fit. |
| (6): plan-check N1 | Section 15 (b) no longer quotes "minimum change"; it quotes decision 6765011642. |
| (6): plan-check N3 | The header no longer says "Not posted"; it names the Revision 3 pointer and check. |
| (6): plan-check N4 | Section 3.4 marks "a 403, not a 404" as inferred and limits it to current service versions. |
| (6): environment-fault rerun cap of 2 | Section 13, with "record the gap and continue". |
| (7): mutation table | 9.2, 9 mutants on the prototype, all killed. |
| Revision 3 plan check BF1 (verifier contract line 470) | Moot: no retry is added, and 5.2 says line 470 stays. |
| Revision 3 plan check BF2 (DA2 and DA3 by name) | The new DA1 to DA3 are all put to Hari by name in Q6. |

Earlier history: Revision 1 (`ffbb38b9`) planned a bounded retry; Revision 2 (`224a5099`) and Revision 3 (`b5167340`) corrected it after a fresh reproduction check and a delta check. Their sections 17 are not carried forward; they remain in the Git history of this file.

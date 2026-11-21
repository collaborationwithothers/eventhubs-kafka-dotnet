The shared scenario workflow runs the consumer scenarios C03, C10 and C01 when its scenario input is one of them. Each phase runs five repeats, and each repeat starts a subject and a witness consumer member Job before the start line, load generator and verifier. This page records workflow behaviour, not a result, and C03, C10 and C01 have their scenarios, plan sources and load generator input.

## Route

The three scenarios take the same route as [B04](b04-workflow.md): job `probe` runs phase C0, job `full-C0` is skipped and job `full-C3` runs phase C1. The C0 candidate does not gate C1. Artifacts are named `c03-C0-candidate-<run_id>-<run_attempt>`, `c03-C0-pods-<run_id>-<run_attempt>` and `c03-C0-archive-<run_id>-<run_attempt>`, and likewise for C1, `c10` and `c01`. For these scenarios the image step builds and reads back three workers in this order: `loadgen`, `verifier`, `enrichment`. Phase C1 is admitted only when all three digests from the initial job are immutable SHA-256 digests.

## Storage write check

Before repeat 1 of each phase, the step checks that the consumer identity can write to the container `consumer-record`. A role assignment that was created in the same Terraform apply can take up to 10 minutes to take effect for blob data access ([Assign an Azure role for access to blob data](https://learn.microsoft.com/azure/storage/blobs/assign-azure-role-data-access#assign-an-azure-role)). Without the check, a repeat whose members cannot write fails at its first write, and the phase spends its budget on dead repeats. That is the likely cause, not proven, of the failed repeats in C03 run 44461695288, phase C1.

Each attempt is one Job of the Enrichment image in the mode `--consumer-storage-check`, run under the `enrichment-consumer` service account, so it uses the member Jobs' identity, network path and private DNS. It creates one probe blob, `<run>/<phase>/storage-check/<job>.probe`, and deletes it. That name is outside every `<run>/<phase>/r<n>/consumer/` prefix, so the readiness poll, the archive and the verifier never see it. The Job is named `<run>-<phase>-storage-check-a<attempt>` and is deleted and confirmed gone after each attempt; a failed delete sets the safety stop. If the write succeeds and the probe delete fails, the pod exits 3, the attempt counts as ready, and the state records `probe_delete_failed: true`.

- Attempts start immediately and then 60 s after the previous attempt ended. The step stops at the first success. It gives up when 1800 s have passed since the first attempt. Both values were set by Hari on 2026-11-19 ([issue 556 decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/556#issuecomment-7001933928)); they are policy values, not measurements. The 1800 s is about three times Microsoft's documented 10 minutes.
- The check runs before the consumer phase clock starts, so it uses none of the 7200 s phase budget or the 5100 s repeat reserve.
- The result is the object `consumer_storage_access` in `phase-status.json`: `outcome` (`ready` or `not_ready`), `attempts`, `elapsed_seconds`, `last_failure` and `probe_delete_failed`. It is not one of the six records of a repeat.
- On exhaustion no member Job is applied. All five repeats are recorded with their six records `not_attempted` and reason `consumer_storage_access_not_ready`, the step exits 1 as for any `not_attempted` record, and the job goes on to its judge, archive and destroy steps. A safety stop wins over this reason.
- `last_failure` is read from the pod's result line and never copies an exception message. For a failed write it is the pod's `cause=` text from `ExceptionSummary` (for example `Azure.RequestFailedException(status=403 error_code=AuthorizationPermissionMismatch)`, an `aadsts=AADSTS<number>` detail, or a `socket_error=<name>` detail). The step itself writes `render_failed` or `apply_failed` when the attempt's Job could not be rendered or applied, `wait_exit_<code>_no_result` when the pod log has no valid result line, and `configuration_invalid` when the pod rejected its own settings.

## Order of one repeat

The orchestration is `infra/scripts/phase/consumer_repeat.py`. `R` is `<run_id>/<phase>/r<n>/`.

| Step | Record `mode` | Action |
|---|---|---|
| S1 | `consumer-subject` | render and apply the subject Job; do not wait |
| S2 | `consumer-witness` | render and apply the witness Job; do not wait |
| S3 | `consumer-readiness` | poll the member records |
| S4 | `start-line` | render, apply, wait, collect |
| S5 | `loadgen` | render, apply, wait, collect |
| S6 | `consumer-subject` | wait for the subject Job to end, then collect |
| S7 | `consumer-witness` | wait for the witness Job to end, then collect |
| S8 | `verify` | the verifier in mode `consumer-verify` |

Every repeat appends exactly six records, in this order: `consumer-subject`, `consumer-witness`, `consumer-readiness`, `start-line`, `loadgen`, `verify`.

- If the subject's render or apply fails, the witness is not started. If the subject started and the witness's render or apply fails, the subject is collected first. The readiness, start-line, load generator and verifier records are `not_attempted` with reason `consumer_apply_failed`. Each member whose apply succeeded is collected, then both member Jobs are deleted and confirmed gone.
- If readiness is not observed, the start line, load generator and verifier are `not_attempted` with reason `consumer_assignment_not_observed`, and the members are collected, deleted and confirmed.
- The wait for each member is anchored to that member's apply: its argument is the remaining part of the member's Job deadline, at least 1 s, and the command timeout is 90 s more. A member that ends Failed is recorded `failed` and the verifier still runs. A member whose wait gives any other result is recorded `unknown`, collected, deleted and confirmed, and the verifier is not run: its record is `not_attempted` with reason `consumer_wait_exhausted`.
- Once a safety stop is set, every later record is `not_attempted` with reason `safety_stop` and its step runs no command. This wins over the phase budget.
- A repeat starts only while at least 5100 s remain of a 7200 s consumer phase budget. Otherwise it and every later repeat are recorded `not_attempted` with reason `orchestration_deadline`. Both values are policy values set by Hari in [decision 6925863575](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6925863575), not measurements. The 5100 s is the configured worst case of one repeat, 5042 s, rounded up. The sum is in section 14 (Q3) of the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b67cf7fdc068fd7a62eab1b2aa3b144904412ce5/items/507/plan-consumer-jobs.md); it is computed from command timeouts, not measured. No repeat starts after 2100 s into the phase, so five repeats fit only when the first four average at most 525 s.

- The step exits 1 when any record is `not_attempted`, `render_failed`, `apply_failed`, `unknown` or `collect_error`. That includes a readiness that was not observed. A member recorded `failed` does not make it exit 1.

## Readiness poll

The workflow lists the container `consumer-record` under `R + "consumer/"` for at most 300 s after the witness apply. Polls are 5 s apart, and a new poll starts while less than 300 s have passed. The workflow's command wrapper refuses a command with 5 s or less left, so the poll that starts at 295 s runs no command and is recorded `list_failed`; when that happens the readiness record's `last_condition` hides the last real unmet condition, and the member records and pod logs are the place to look. A poll is ready when exactly two members appear, each with a `registration.json`; each member's last `partition_event` row is `assigned`; the two partition lists are disjoint; and together they are exactly partitions 0 to 31. The readiness record carries the number of polls, the elapsed seconds and the last unmet condition (`list_failed`, `download_failed`, `row_invalid`, `member_count`, `not_assigned`, `overlap` or `coverage`), never blob contents. The workflow principal reads the container with a container-scoped Storage Blob Data Reader role.

## Bounds for the scenario data

A consumer window must not exceed 1620000 ms: the 1800 s member Job deadline, minus the worker's 120 s watchdog margin after the window (`ConsumerProcessingWorker.cs` lines 53 to 59), minus 60 s for startup (a policy allowance, section 3.7 of the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/b67cf7fdc068fd7a62eab1b2aa3b144904412ce5/items/507/plan-consumer-jobs.md)). It must also cover readiness, the start-line Job and the load generator Job. A completion deadline must be below 40000 ms: the verifier's 720 s process lifetime (`ProcessLifetime: 00:12:00`, `charts/parchwell/values.yaml` line 78) must exceed its cleanup, I/O, token, metadata, watermark, blob I/O and safety-stop times plus the deadline (`ConsumerVerificationPlan.cs` lines 3 to 26). These are bounds derived from policy values, not measurements. A scenario that needs larger bounds changes the chart values in its own plan.

## Not yet in place

The `judge` step accepts these scenarios since the consumer phase judge merged. The evidence archive admits C01, C03 and C10 with their consumer records, as the [phase evidence retention](archive-smoke.md#phase-evidence-retention) section describes. A repeat whose subject kills itself writes no native-monitoring index; for C10 and C01 the archive records that as the expected absence `configured_self_kill`, as [phase evidence retention](archive-smoke.md#phase-evidence-retention) describes.

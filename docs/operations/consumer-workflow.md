The shared scenario workflow runs the consumer scenarios C03, C10 and C01 when its scenario input is one of them. Each phase runs five repeats, and each repeat starts a subject and a witness consumer member Job before the start line, load generator and verifier. This page records workflow behaviour, not a result, and nothing here runs live until the child issue that supplies the C03 plan sources and load generator input merges.

## Route

The three scenarios take the same route as [B04](b04-workflow.md): job `probe` runs phase C0, job `full-C0` is skipped and job `full-C3` runs phase C1. The C0 candidate does not gate C1. Artifacts are named `c03-C0-candidate-<run_id>-<run_attempt>`, `c03-C0-pods-<run_id>-<run_attempt>` and `c03-C0-archive-<run_id>-<run_attempt>`, and likewise for C1, `c10` and `c01`. For these scenarios the image step builds and reads back three workers in this order: `loadgen`, `verifier`, `enrichment`. Phase C1 is admitted only when all three digests from the initial job are immutable SHA-256 digests.

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

- If the subject's render or apply fails, the witness is not started. The readiness, start-line, load generator and verifier records are `not_attempted` with reason `consumer_apply_failed`. Each member whose apply succeeded is collected, then both member Jobs are deleted and confirmed gone.
- If readiness is not observed, the start line, load generator and verifier are `not_attempted` with reason `consumer_assignment_not_observed`, and the members are collected, deleted and confirmed.
- The wait for each member is anchored to that member's apply: its argument is the remaining part of the member's Job deadline, at least 1 s, and the command timeout is 90 s more. A member that ends Failed is recorded `failed` and the verifier still runs. A member whose wait gives any other result is recorded `unknown`, collected, deleted and confirmed, and the verifier is not run: its record is `not_attempted` with reason `consumer_wait_exhausted`.
- Once a safety stop is set, every later record is `not_attempted` with reason `safety_stop` and its step runs no command. This wins over the phase budget.
- A repeat starts only while at least 5100 s remain of a 7200 s consumer phase budget. Otherwise it and every later repeat are recorded `not_attempted` with reason `orchestration_deadline`. Both values are policy values set by Hari in [decision 6925863575](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6925863575), not measurements. The 5100 s is the configured worst case of one repeat, 5042 s, rounded up; it is computed from command timeouts, not measured. No repeat starts after 2100 s into the phase, so five repeats fit only when the first four average at most 525 s.

- The step exits 1 when any record is `not_attempted`, `render_failed`, `apply_failed`, `unknown` or `collect_error`. That includes a readiness that was not observed. A member recorded `failed` does not make it exit 1.

## Readiness poll

The workflow lists the container `consumer-record` under `R + "consumer/"` every 5 s for at most 300 s after the witness apply. A poll starts only while more than 5 s of that time remain, because the workflow's command wrapper refuses a command with 5 s or less left; the last poll can therefore start at 295 s and be recorded `list_failed`. A poll is ready when exactly two members appear, each with a `registration.json`; each member's last `partition_event` row is `assigned`; the two partition lists are disjoint; and together they are exactly partitions 0 to 31. The readiness record carries the number of polls, the elapsed seconds and the last unmet condition (`list_failed`, `download_failed`, `row_invalid`, `member_count`, `not_assigned`, `overlap` or `coverage`), never blob contents. The workflow principal reads the container with a container-scoped Storage Blob Data Reader role.

## Bounds for the scenario data

A consumer window must not exceed 1620000 ms: the 1800 s member Job deadline, minus the worker's 120 s watchdog margin after the window (`ConsumerProcessingWorker.cs` lines 53 to 59), minus 60 s for startup. It must also cover readiness, the start-line Job and the load generator Job. A completion deadline must be below 40000 ms: the verifier's 720 s process lifetime must exceed its cleanup, I/O, token, metadata, watermark, blob I/O and safety-stop times plus the deadline (`ConsumerVerificationPlan.cs` lines 3 to 26). These are bounds derived from policy values, not measurements. A scenario that needs larger bounds changes the chart values in its own plan.

## Not yet in place

The `judge` step fails for these scenarios until the phase judgement of the consumer block merges. The evidence archive does not yet admit C01, C03 or C10, so a consumer dispatch records an archive failure in its report summary until archive support merges.

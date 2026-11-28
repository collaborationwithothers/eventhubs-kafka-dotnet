# C18 stop smoke

The C18 stop smoke deletes an idle consumer member's pod three times and records what Kubernetes and today's worker do. It is a diagnostic, not a scientific run: it writes nothing under `results/`, has no `expected.yaml`, and produces no archive, judge or candidate. Its evidence settles two runtime questions that [ADR-022 "Amendment 1"](../blueprint.md) leaves to a smoke before the C18 plan is approved, and a third that the approved plan adds.

The questions are these. First, whether the TERM signal of a pod deletion reaches the `enrichment-consumer` worker, which is not process 1 because the pod shares its process namespace. Second, whether the workflow can read the deleted pod's container end time, and by which method. Third, which the plan adds because Amendment 1 makes a second subject registration an invalid repeat, whether the Job starts a replacement pod after the delete. The design is the approved [plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/18f21941329437cbd767559f559f1de02f7bbf2b/items/613/plan-c18-stop-smoke.md) for issue 613, revision 4.

## Workflow

[C18 stop smoke](../../.github/workflows/c18-stop-smoke.yml) has one trigger, `workflow_dispatch`, with one input, `confirm_stop_smoke`. A committed copy is at [workflows/c18-stop-smoke.yml.txt](workflows/c18-stop-smoke.yml.txt) and the two files are byte-identical. The `smoke` job applies and destroys the ephemeral environment in the shared `parchwell-ephemeral` concurrency group, and the `stop` job stops AKS, as the [B10 abrupt trial](b10-workflow.md) does. The sweeper counts a running smoke as an active dispatch.

The smoke borrows the identity of scenario C03, configuration C0, because the worker and chart admit only C01, C03 and C10 today. Its run ID starts with `c18s`, so its records and group never touch a C03 result. Its telemetry in the persistent Application Insights still carries `scenario_id` C03, so a query must filter on the run ID. Hari accepted that risk by name in [decision 7237226936](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7237226936), point 4, confirmed in [decision 7237271679](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7237271679).

Only a merged workflow on `main` may be dispatched, and one dispatch is authorized, by the control session of issue 613.

## One trial

After the existing consumer storage write check, the run does three trials in sequence. Each trial runs one subject member, with no witness, load generator, start line or verifier. The member should consume no message because the topic has no producer (an inference from the environment being created by the same job).

1. Apply the member Job, then poll its record every 5 s, for at most 300 s, until its registration exists and its last partition event is `assigned` with partitions 0 to 31.
2. Read the one running pod. Start `kubectl logs -f` and a `kubectl get pod --watch --output-watch-events` in the background.
3. Wait 30 s after readiness. Record the runner time, then delete the pod with `--wait=false` and no `--grace-period`, so the pod's own 30 s grace applies.
4. Poll the pod every second until NotFound or 120 s after the delete. Every 5 s, list the Job's pods.
5. Stop the streams, list the pods once more, read the Job, then delete and confirm the Job. A failed delete or a remaining pod sets the safety stop and every later trial is `not_attempted` with reason `safety_stop`.

The bounds are policy values set by Hari in decision 7237226936, point 3, apart from the 5 s pod listing, which plan section 3.3 step 7 sets. They are not measurements.

## Evidence

The artifact `c18-stop-smoke-<run id>-<attempt>` holds one strict JSON file, `c18-stop-smoke.json`, with the keys of the plan's section 3.4. It holds no blob content, token, exception message or log text other than one captured reason string.

- `watch.events` counts the watch events that parsed. `watch.terminated`, `watch.terminated_event_type` and `watch.terminated_received_at` come from the first parsed event whose `enrichment-consumer` container has `state.terminated`. `watch.deleted_received_at` is the receive time of the first event of type `DELETED`. `poll.terminated` and `poll.terminated_read_at` come from the first poll that returned that state, and `poll.not_found_at` is the runner time at which the first NotFound poll returned.
- `delete.deletion_timestamp` and `delete.deletion_grace_period_seconds` come from the first pod object received after the delete, from the watch or the poll, that carries `deletionTimestamp`.
- `log.failure_line_reason` is the captured group of the first log line that matches `^consumer_processing_failed reason=([a-z_]{1,64}) `.
- A trial that did not run is `not_attempted` with one reason of `run_not_started`, `consumer_storage_access_not_ready`, `safety_stop`, `render_failed`, `apply_failed`, `readiness_not_observed`, `pod_not_running` or `delete_failed`.

Each observed trial gets three labels. They decide no validity and no verdict.

- `sigterm` is the first of these that matches. `reached_worker`: the container end time is not earlier than the delete time cut to whole seconds and is earlier than the pod's deletion deadline, and the exit code is not 137. `reached_but_killed_at_grace`: exit code 137 and a worker failure line. `not_reached`: exit code 137 and no failure line. Otherwise `undetermined`. A missing deadline only stops `reached_worker`.
- The label compares times from three clocks: the runner's (`delete.issued_at`), the node's (`finished_at`) and the API server's (`deletionTimestamp`). Their agreement is not measured by this smoke. A node clock behind the runner can turn a real early exit into `undetermined`; read the raw times beside the label.
- `end_time` is `read` when the container's `finishedAt` was read, and `end_time_source` says whether the watch, the poll or both read it.
- `replacement_pod` is true when a pod other than the deleted one appears in `pods_seen`.

The exit code 137 as 128 plus SIGKILL is an inference from container-runtime practice, not read from a Kubernetes source; the `signal` field is recorded beside it so that the C18 plan can check it.

`run-c18-stop-smoke` exits 1 when any trial is `not_attempted` or has `sigterm` `undetermined`, so a negative answer to the second question shows in the job status. `not_reached`, `reached_but_killed_at_grace` and a replacement pod are findings, not failures. `collect-c18-stop-smoke` writes the file from saved state even after a failed run, and exits 1 only when it cannot write it.

## Offline checks

`infra/scripts/phase/tests/test_c18_stop_smoke.py` runs the module against stub commands and a fake clock. `infra/scripts/tests/test_c18_stop_smoke_workflow.py` checks the workflow's shape and its committed copy. `infra/scripts/lifecycle/check-offline.sh` compares the copy and runs actionlint on the workflow.

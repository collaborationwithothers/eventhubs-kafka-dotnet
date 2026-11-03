# Idle-observation capture contract

The idle-observation worker runs the A02, A03 and A04 experiment. Five fresh producers produce one record each, stay idle with nothing to send, and produce again; the worker records both outcomes and every native statistics report and judges nothing. The verifier judges it under the [verifier contract](verifier-contract.md#idle-observation-judgement).

## Inputs and identity

The LoadGen image runs the worker with `--idle-observation`. `--validate-idle-observation-configuration` loads and validates the same settings, prints `{"idle_observation_configuration_valid":true}` and exits without contacting Azure. The declaration and its literals are defined in the [scenario contract](scenario-contract.md#idle-observation-declarations).

The worker admits only these item and configuration pairs: A02 C0 and C1, A03 C0, C1 and C2, A04 C0 and C1. The run kind is `smoke` or `batch`. The Job name is `<run>-<item>-<configuration>-idle` in lowercase, and evidence goes to the `ledger` Blob container. Each client's `client.id` is `<pod>-r<repeat>`. The Job runs under the loadgen identity.

Startup failures are `idle_observation_identity_invalid`, `idle_observation_fixed_bounds_invalid`, `idle_observation_declaration_missing` and `idle_observation_statistics_interval_mismatch`. The last applies when the resolved `statistics.interval.ms` differs from the declaration's `statistics_interval_ms`.

## Sequence

The worker registers once, then runs five clients at the same time in one pod. Each client does the following, in order.

1. Construct the producer. A construction failure becomes the first produce's terminal error with origin `construction`, and the client does nothing further.
2. Produce the first record and wait for its delivery report, for at most the action window.
3. Stay idle for `idle_seconds`, calling no producer method other than the delivery-report and statistics callback drain. The managed background poll (`dotnet.producer.enable.background.poll`, `true` in the [baseline](kafka-baseline.md)) serves callbacks while the client is idle ([`EnableBackgroundPoll`, Confluent.Kafka v2.16.0](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Config_gen.cs#L1508-L1517)).
4. Produce the second record and wait for its delivery report, for at most the action window.
5. Keep the client open for the post-terminal statistics period so later reports are captured.
6. Dispose the client.

Each record has topic `document-events`, partition 0, a 1000-byte value of ASCII `s` bytes and the key `<item>:<configuration>:<run>:r<repeat>:p<attempt>`. The record shape and the 330000-millisecond action window are harness literals from the [#317 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/317), not measurements. Elapsed values are milliseconds from the start of client construction.

## Paths and envelopes

All paths are under `<run>/idle-observation/<item>/<configuration>/` in the `ledger` container, and every write is create-only.

| Path | Written | Content |
|---|---|---|
| `registrations/<pod>.json` | Before any client is constructed | Registration |
| `instances/<pod>/capture.json` | After all five clients are disposed | Capture |
| `instances/<pod>/completion.json` | Last, only after capture, client cleanup, host stop and disposal | Completion |

Registration and completion follow the [single-action contract](single-action-contract.md#capture-envelopes) with `mode` `idle-observation`; the completion records five repeats attempted.

The capture has exactly `schema_version`, `mode`, `synthetic`, `run_kind`, `identity`, `job_name`, `pod_name`, `pod_uid`, `source_commit`, `managed_version`, `native_version`, `topic`, `partition`, `payload_bytes`, `idle_seconds`, `statistics_interval_ms`, `max_statistics_reports` and `repeats`. Each repeat has exactly `repeat`, `client_id`, `settings`, `omitted_keys`, `client_started_unix_ms`, `first`, `idle_started_elapsed_ms`, `idle_ended_elapsed_ms`, `second`, `client_errors`, `client_errors_dropped`, `statistics` and `diagnostics`. Each produce record has exactly `attempt`, `key`, `started_elapsed_ms`, `terminal`, `result`, `error`, `terminal_elapsed_ms` and `terminal_unix_ms`.

`result` and `error` follow the single-action contract's capture envelopes (`origin` is `construction`, `local` or `broker`). A produce with no recorded delivery report has `terminal` false (no report arrived inside the window, or the repeat had already set `capture_limit` or the report's error reason exceeded `MaxErrorBytes`, so the worker dropped it; in both limit cases the worker exits 1) and null `result`, `error`, `terminal_elapsed_ms` and `terminal_unix_ms`, and its repeat holds the diagnostic `action_window_elapsed:p<attempt>` unless the diagnostic cap had already set `capture_limit`, which drops that diagnostic. A broker error on either produce is a captured outcome, not a worker failure.

## Bounds

[idle-observation-runtime-limits.json](idle-observation-runtime-limits.json) owns every bound, and the chart, the worker and the tests all compare against it. They are harness budgets from the #317 plan, not measurements. Derivations, computed by script:

- Client worst case: 330.1 + 420.1 + 330.1 + 20.1 + 10 = 1110.4 seconds (the first action window, the idle period, the second action window and the post-terminal statistics period, each with one 0.1-second poll slice, plus 10 seconds of cleanup).
- Process worst case: 30 registration + 1110.4 + 60 capture upload + 10 host cleanup + 30 completion = 1240.4 seconds, so `ProcessLifetimeSeconds` is 1260 and the Job deadline is 1320 seconds.
- Reports: 1110.4 seconds at one report per 15 seconds is at most 75 reports, and the cap is 80.
- Statistics bytes: 5 clients x 80 reports x 349528 (the base64 length of 262144 bytes) = 139811200, which is 27960960 below `MaxCaptureBytes` (167772160).

A limit on a statistics report count or size, a client error size, a delivery error reason size, a diagnostic count or size, the callback queue or the settings marks `capture_limit` in that repeat. The worker then still writes the capture, exits 1 and writes no completion. Client errors past `MaxClientErrors` are counted in `client_errors_dropped` and do not set the limit. A client cleanup failure also exits 1 with no completion.

## Phase selection

The phase module `infra/scripts/phase/idle_observation.py` runs the selected configurations one at a time, in the order A02 C0, A02 C1, A03 C0, A03 C1, A03 C2, A04 C0, A04 C1. The selected items must be a nonempty list in item order. Before each Job it requires 1860 seconds to remain inside the 14400-second batch window; otherwise it skips the configuration with `start_cap`. The 1860 seconds are 60 apply + 1410 wait + 210 collect + 120 delete + 30 confirm + 25 command reserve = 1855, rounded up. Seven configurations reserve 13020 seconds in total. These are planning budgets, not measurements.

After the selected configurations, the module runs one judge Job, `<run>-idle-judge`, when 1255 seconds remain. Its `Items` option names the selected items, so it judges those items only. The 1255 seconds are 60 apply + 810 wait + 210 collect + 120 delete + 30 confirm + 25 command reserve = 1255 (`JUDGE_RESERVATION`). Seven captures and the judge reserve 13020 + 1255 = 14275 seconds. The 1255 and 14275 seconds are planning budgets, not measured judge durations. Whatever the judge exit code, the module downloads the judge receipt and the candidates of the selected items and checks their identity and hashes. The run's evidence passes only when every capture is complete, the judge exits 0 and every candidate is accepted.

The module transports only. A configuration passes when its Job outcome is complete, its evidence collection has no gaps, and the one loadgen pod terminated with exit code 0, which the worker reaches only after the completion upload. The module writes `idle-observation-<run_kind>.json` in the artifacts directory. [Workflow admission](#workflow-admission) says how a dispatch reaches this module.

## Workflow admission

The Scenario workflow offers the choice `idle-observation`. `phase.py` admits it only with `run_kind` `smoke` or `batch` and `items` either `all` or a nonempty comma-separated list of A02, A03 and A04 in that order with no repeats, such as `A02,A04`, with `capture_authentication_metadata`, `test_handler_refresh_path` and `test_native_authentication` all false. It rejects any other selection with `unsupported idle observation selection` before private inputs or Azure login.

The existing `batch` job runs the dispatch. Its `validate-batch` and `prepare-batch` commands check and freeze the selection in `phase.py`. `single-action-seed` re-checks the frozen document through the module above and starts no F04 seed. `run-single-action-batch` runs the module. Preparation freezes the selected items, where `all` means A02, A03 and A04, and their configurations, under the run ID `idle-<run_id>-<run_attempt>`. The pod evidence artifact is `idle-observation-pods-<run_id>-<run_attempt>` and holds `idle-observation-<run_kind>.json`, which is `idle-observation-smoke.json` for a smoke run and `idle-observation-batch.json` for a batch run. The candidates go to the artifact `idle-observation-candidates-<run_id>-<run_attempt>`; a missing file fails the upload. The stop-only `batch-stop` job validates the same selection and stops AKS when the dispatch ends.

A smoke result is not published ([split decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563452228), point 4).

## Boundaries

The worker makes no content checks (ADR-019 in the [blueprint](../blueprint.md)). It does not test K-37. It decides nothing about whether the idle period caused a failure. The judge, not the worker, applies the decision rules.

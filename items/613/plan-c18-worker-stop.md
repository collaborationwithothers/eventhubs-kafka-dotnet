Revision: 3

Changes since revision 2, applying #613 [decision 7238011332](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7238011332), which carries `Confirmation: thumbs-up required`. `scripts/decision-log.py` reports Hari's thumbs-up at 2026-11-28T10:37:25Z, after the comment's last edit at 10:36:57Z.
- Point 1: the C18 admission is in pull request 3, with the scenario file. Section 11 states `None`, and section 11a keeps the question and its answer. Sections 2 and 12 say so.
- Point 2: option (a). `Program.cs` may move the consumer factory's handler-setting chain into one internal method, which a new test checks through a `ConsumerBuilder` subclass (section 2 item 6, 3.7, R16 and the test map).
- Point 3: the revision 2 check's B3 (completion evidence in section 12, written to match point 1) and N1 (the R11 and R12 rows set the stop inputs).
- The base moves to `89f646d9`. The only change since `9c98aa1b` is one line of `AGENTS.md`, and no file this plan changes or cites.

Changes in revision 2 since revision 1, applying the [revision 1 plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7237760956):
- B1: the deferral of the C18 admission to pull request 3 is now an open question for Hari (section 11, question 1), with the evidence; the rest of the plan is unchanged by it.
- B2: every append on the stop path, including the in-progress message's processed row, passes `CancellationToken.None` (3.6 step 3), with a token check in the R5 test.
- B3: after a `failed` or `timed_out` shutdown, `Dispose` is not called, the record closes before `shutdown_ended_utc`, and native monitoring completes with `false` (3.6 step 4, R7 test).
- B4: section 12's verifier item gains its responsible session and gate.
- N1 to N3 (the check's `apply in implementation` P2s) are written in with their exact text: the R2 slow-message case at window 120000, the new test `Message_returned_during_stop_is_processed`, and R11's literal expectation.
- N4 notes: the R4 clock is advanced from the test clock's base; section 2 item 7 names the harness changes; section 7 names the SIGTERM-to-`Main` assumption.
- The ADR-022 amendment is cited as lines 800 to 840.

# c18-worker-stop: the enrichment worker's stop path for C18

This plan changes the enrichment consumer worker so that a subject told to stop ends validly. It finishes the message in progress, records that it saw the stop request, shuts its consumer down by `Close` or by `Dispose` only, and writes a stop receipt. Members also record every consume error. It is pull request 2 of the C18 split and changes no workflow, chart, scenario, expected outcome or verifier file.

Nothing changes for C01, C03 or C10: their records stay byte-identical. The new behaviour is reached only through plan inputs that no plan source sets in this pull request. Pull request 3 sets them from the C18 scenario file.

Base commit: `main` at `89f646d9df0df86db25886ef6892bdc9b58549e5`. Revisions 1 and 2 were written against `9c98aa1be09ff76b6fc19fed71bc8cc2f3706259`. Between the two commits only one line of `AGENTS.md` changed, and no file this plan changes or cites changed. Every repository citation below holds at both.

## 1. Goal and authority

Goal. Implement, in `src/Parchwell.Enrichment/` only, the worker half of ADR-022 "Amendment 1: C18, C06 and C04" (docs/blueprint.md lines 800 to 840):
- the "Stop" bullet: stop-observed row, the two shutdown modes, and the stop receipt;
- the "Consumer errors" bullet: one consumer-error row per `ConsumeException` and per error-handler call;
- the shutdown-mode half of "Configuration fidelity": the registration marker records the shutdown mode.

Authority, in creation order:
- The #613 issue body.
- #613 decision 7236669224, which adopts the #597 decisions.
- #613 [decision 7237226936](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7237226936) point 1. It approves the C18 split in the order of the c18-stop-smoke plan's section 12, and lets this pull request be planned, approved and implemented in parallel with the smoke. Its limit: "Pull request 2 changes no workflow, scenario, expected outcome or verifier file; if its plan needs one, it stops and comes back to Hari."
- #597 decision 7237271679, adopted on #613 by 7237273680. It carries `Confirmation: thumbs-up required`, and its point 4 is that parallel route. `scripts/decision-log.py` reports Hari's thumbs-up on it at 2026-11-28T09:56:02Z, after its last edit at 09:29:20Z.
- #613 [decision 7238011332](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7238011332), with `Confirmation: thumbs-up required` and Hari's thumbs-up at 2026-11-28T10:37:25Z. It moves the C18 admission to pull request 3, sets the wiring-test approach, and adopts the revision 2 check's B3 and N1.
- docs/blueprint.md ADR-022 with "Amendment 1", ADR-017 and ADR-020.

The c18-stop-smoke plan (`items/613/plan-c18-stop-smoke.md` on this branch) is a sibling. This plan neither depends on it nor changes it.

## 2. Scope

Files allowed to change:
1. `src/Parchwell.Enrichment/ConsumerProcessing/ConsumerProcessingPlan.cs`: three plan inputs and their validation (section 3.1).
2. `src/Parchwell.Enrichment/ConsumerProcessing/ConsumerProcessingWorker.cs`: the stop path and the consumer-error rows.
3. `src/Parchwell.Enrichment/ConsumerProcessing/ConsumerRecordCodec.cs`: the `stop_observed` and `consumer_error` rows, the stop receipt, and the registration field.
4. `src/Parchwell.Enrichment/ConsumerProcessing/IConsumerRecordStore.cs` and `BlobConsumerRecordStore.cs`: one method that writes `stop-receipt.json`.
5. `src/Parchwell.Enrichment/ConsumerProcessing/ConsumerProcessingHandlers.cs`: one optional error callback.
6. `src/Parchwell.Enrichment/Program.cs`: the `ConsumerProcessingConsumerFactory` registration (lines 277 to 297). Its handler-setting chain (lines 288 to 293: statistics, assigned, revoked, lost and offsets-committed handlers) moves into one internal method that takes the builder, the handlers and the statistics handler (today `NativeClientCapture.ConsumerHandler(lifetime)`, line 289) and returns the builder ([decision 7238011332](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7238011332) point 2). That method sets the same handlers in the same way as on `main`, and also sets the error handler when the callback is not null. The factory calls it before `Build`. Nothing else in the file changes.
7. `tests/Parchwell.Enrichment.Tests/ConsumerProcessing/`: the new test files of section 6; the test doubles in `ConsumerTestHarness.cs`, which gain the new store method in `RecordingRecordStore` with a record of each token's `CanBeCanceled`, a token the test can cancel passed by `RunAsync` (today `CancellationToken.None`, line 47), a way to invoke the error callback, and a way to make `Close` throw or block; and existing tests only under the fixture rule of section 6.

Paths that must not change:
- `src/Parchwell.Enrichment/ConsumerProcessing/ScenarioConsumerProcessingPlanSource.cs` and `ConsumerProcessingOptions.cs`, and `charts/`: C18 is admitted in pull request 3, with its scenario file ([decision 7238011332](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7238011332) point 1).
- `src/Parchwell.Scenarios/`, `src/Parchwell.Verifier*/`, `src/Parchwell.LoadGen/`, `src/Parchwell.Kafka/`.
- `scenarios/`, every `expected.yaml`, `.github/`, `docs/`, `infra/`, `results/`.
- Every `*.csproj`, `Directory.*.props` and `*.sln*`. No new package or project.

## 3. External contracts

### 3.1 Plan inputs

`ConsumerProcessingPlan` gains three inputs. Their property names are the implementer's; this plan calls them:
- `StopConfigured` (bool, default false): the configuration names a stop time. The workflow owns the time; the worker only needs to know one is set.
- `ShutdownMode` (string or null, default null): `close` or `dispose`.
- `RecordsConsumerErrors` (bool, default false): the member writes consumer-error rows.

`ApplyAndValidate` throws `consumer_plan_invalid` (the existing failure name) when any of these holds, in addition to its existing checks at lines 24 to 29:
- V1. `StopConfigured` is true and the role is not `subject`.
- V2. `StopConfigured` is true and `KillCount` or `SlowMessage` is not null (Amendment 1 "Stop": "A configuration names a stop time or a kill count, not both"; this plan also excludes a slow message, see 3.6).
- V3. `StopConfigured` is true and `ShutdownMode` is not `close` or `dispose`.
- V4. `StopConfigured` is false and `ShutdownMode` is not null.

Mapping a scenario that names no shutdown mode to `close` is pull request 3's plan source (Amendment 1: "With no shutdown mode named, the mode is `close`").

### 3.2 Records unchanged when the inputs are off

When `StopConfigured` and `RecordsConsumerErrors` are both false, every byte the worker writes is as on `main`:
- the registration JSON;
- every row;
- the completion JSON;
- the standard error line;
- the exit code;
- the calls to the store.

The verifier parses the registration and each row kind with a closed key set (`src/Parchwell.Verifier/Common/Consumer/ConsumerArtifactCodec.cs` lines 14, 24 and 31 to 52), so any new key there would invalidate C01, C03 and C10 runs.

### 3.3 Registration

When `ShutdownMode` is not null, the registration JSON gains one key, `shutdown_mode`, whose value is `ShutdownMode`. It has no other change. When `ShutdownMode` is null, the key is absent.

### 3.4 New rows

Each is one JSON line, written by the existing `Line` helper, so the existing 65536-byte limit applies. Key order is as listed; `event_utc` uses the existing `Time` format, `yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'`.

`stop_observed`:
```json
{"schema_version":1,"kind":"stop_observed","instance_id":"<pod>","row_sequence":<n>,"event_utc":"<time>"}
```
`event_utc` is the clock reading taken in a callback registered on the stop token, at the moment it fires. That is "the time it observed the stop request" (Amendment 1 "Stop").

`consumer_error`:
```json
{"schema_version":1,"kind":"consumer_error","instance_id":"<pod>","row_sequence":<n>,"source":"consume","error_code":<int>,"is_fatal":<bool>,"reason":"<text>","event_utc":"<time>"}
```
- `source` is `consume` for a `ConsumeException` raised by `Consume`, and `error_handler` for a call of the error handler.
- `error_code` is `(int)Error.Code` and `is_fatal` is `Error.IsFatal`.
- `reason` is `Error.Reason`, cut to its first 1024 UTF-16 code units when longer. That keeps the row well inside the line limit.

### 3.5 Stop receipt

The blob `<prefix>stop-receipt.json`, where `<prefix>` is the member's existing record prefix (`ConsumerProcessingOptions.Prefix`, line 22). It is written once with `IfNoneMatch = ETag.All` and size limit 65536, as `completion.json` is (`BlobConsumerRecordStore.cs` lines 52 to 63). Shape, keys in this order:
```json
{"schema_version":1,"kind":"consumer_stop_receipt","run_id":"...","scenario_id":"...","configuration_id":"...","phase":"...","repeat_number":1,"role":"subject","instance_id":"<pod>","job_name":"...","pod_uid":"...","source_commit":"...","rows":<n>,"record_blobs":<n>,"messages_processed":<n>,"stop_observed_utc":"<time>","shutdown_mode":"close","shutdown_started_utc":"<time>","shutdown_ended_utc":"<time>","shutdown_outcome":"ok","receipt_written_utc":"<time>"}
```
- `rows`, `record_blobs` and `messages_processed` are computed as in `Completion` (`ConsumerRecordCodec.cs` lines 25 to 33).
- `shutdown_outcome` is `ok`, `failed` or `timed_out`, as 3.6 step 4 sets.
- `receipt_written_utc` is the clock reading taken just before the upload.

### 3.6 Stop path

Applies only when `StopConfigured` is true. That implies a subject with no kill count and no slow message (V1, V2).
1. Stop request. The stop request is the cancellation token the worker already receives, `ApplicationStopping` (`Program.cs` lines 202 to 203). It counts only after `Subscribe` has returned. A stop before that takes the existing failure path, unchanged.
2. Finish the message in progress. A message whose processing has started completes its full `ProcessingDelayMs`: the delay is not cancelled by the stop token. Its processed row is appended, and its offset is stored when `enable.auto.offset.store` is false, as today (lines 198 to 207). Then the loop ends and `Consume` is not called again. The stop token is checked only before each `Consume` call, so a message returned by the call during which the stop fired is processed as the message in progress.
3. Stop-observed row. Append `stop_observed` (3.4). When `StopConfigured` is true, every append, including the processed row of step 2 and the callback rows of step 4, and the receipt upload pass `CancellationToken.None`. That way the already-cancelled stop token cannot fail them.
4. Shutdown. Record `shutdown_started_utc`, then:
   - mode `close`: call `Close` under the existing 30 s bound (line 86), then `Dispose`;
   - mode `dispose`: call `Dispose` only, unbounded as `Dispose` is today.

   Then record `shutdown_ended_utc`. `shutdown_outcome` is `timed_out` when the 30 s bound expires, `failed` when the call throws, and `ok` otherwise. Partition-event and commit callbacks raised by the call are appended as rows, as today. When `shutdown_outcome` is `failed` or `timed_out`, `Dispose` is not called, as on `main` (lines 85 to 88). In every outcome the record closes before `shutdown_ended_utc` is taken: no later callback appends a row, so the receipt's `rows` counts every appended row. Native monitoring is completed with `true` when `shutdown_outcome` is `ok` and with `false` otherwise, where line 95 completes it today.
5. Receipt. If no append failed, write the stop receipt (3.5) and return exit code 0. Write no completion receipt and no standard error line.
6. Failures, which are not an `ok` stop:
   - an append failure at any point after the stop request ends the member exactly as today, with reason `consumer_record_append_failed`, exit 1 and no receipt;
   - a receipt upload that throws prints `consumer_processing_failed reason=consumer_stop_receipt_failed ...` in the existing format (line 117) and returns 1.
7. Window end before any stop request. The existing completion path runs, unchanged. Amendment 1 makes that repeat invalid; the worker does not judge it.
8. The 120 s watchdog after the window (lines 57 to 61) is unchanged.

### 3.7 Consumer-error rows

Applies only when `RecordsConsumerErrors` is true, to every role:
- In `Poll` (lines 156 to 160), a `ConsumeException` first appends a `consumer_error` row with `source` `consume`. A non-fatal error then returns null, as today. A fatal one propagates, as today, and so ends the member through the existing path with its existing reason.
- `ConsumerProcessingHandlers` gains an optional error callback. The worker sets it only when `RecordsConsumerErrors` is true, and it appends a `consumer_error` row with `source` `error_handler`. Confluent.Kafka ignores exceptions thrown by an error handler (S1), so an append failure inside it sets the existing `appendFailed` flag. The loop's existing `ThrowIfAppendFailed` then ends the member with `consumer_record_append_failed`.
- The internal method of section 2 item 6 calls `SetErrorHandler` on the builder only when the callback is not null. With no error handler set, Confluent.Kafka invokes none (S1, `Consumer.cs` line 110), as on `main`.

## 4. Requirements

R1. WHEN both new inputs are off, THEN the worker's writes, standard error output and exit code equal those on `main` for the same inputs (3.2).

R2. WHEN any of V1 to V4 holds, THEN the worker fails with `consumer_plan_invalid`, before it registers.

R3. WHEN `ShutdownMode` is set and the plan is valid, THEN `registration.json` carries `shutdown_mode` with that value, and no other new key.

R4. WHEN `StopConfigured` is true and the stop token fires after `Subscribe` while the worker is polling, THEN it appends `stop_observed` with `event_utc` equal to the clock reading at the token's callback. It then calls the mode's shutdown, writes the stop receipt, writes no `completion.json` and returns 0.

R5. WHEN the stop token fires while a message is being processed, THEN that message's delay runs in full and its processed row precedes `stop_observed`. No later message is consumed.

R6. WHEN the mode is `close` and `Close` returns, THEN `Close` and then `Dispose` are called. WHEN it is `dispose`, THEN `Dispose` is called and `Close` is not.

R7. WHEN `Close` throws, THEN `shutdown_outcome` is `failed`. WHEN `Close` exceeds 30 s, THEN it is `timed_out`. In both cases `Dispose` is not called, no row is appended after the record closes, the receipt is still written and the exit code is 0.

R8. WHEN revoke or commit callbacks run during `Close` after the stop, THEN their rows are appended, even though the stop token is cancelled.

R9. WHEN the `stop_observed` append fails, THEN the member exits 1 with reason `consumer_record_append_failed` and writes no receipt.

R10. WHEN the receipt upload throws, THEN the member prints `consumer_processing_failed reason=consumer_stop_receipt_failed` with the existing fields and returns 1.

R11. WHEN the stop token fires before `Subscribe` returns, THEN the existing failure path runs, unchanged, and no `stop_observed` row is written.

R12. WHEN the window ends before any stop request and `StopConfigured` is true, THEN `completion.json` is written as today, and no `stop_observed` row or receipt is written.

R13. WHEN `RecordsConsumerErrors` is true and `Consume` raises a non-fatal `ConsumeException`, THEN one `consumer_error` row with `source` `consume` and `is_fatal` false is appended, and the member continues.

R14. WHEN `RecordsConsumerErrors` is true and `Consume` raises a fatal `ConsumeException`, THEN one `consumer_error` row with `is_fatal` true is appended before the member ends with its existing fatal reason.

R15. WHEN `RecordsConsumerErrors` is true and the error handler is called, THEN one `consumer_error` row with `source` `error_handler` is appended.

R16. WHEN `RecordsConsumerErrors` is false, THEN the builder sets no error handler and no `consumer_error` row is written.

R17. WHEN `reason` is longer than 1024 code units, THEN the row holds its first 1024.

## 5. Invariants and safety rules

- No new package, project or reference (AGENTS.md "Code Review Rules"; section 2).
- No Kubernetes API use from .NET code (ADR-020). The worker sends no signal in the stop path.
- Confluent.Kafka is not wrapped behind a new seam (AGENTS.md "Engineering standards"); the existing consumer factory delegate is the only seam used.
- Warnings are errors and nullable is on (docs/engineering/dotnet.md).
- The verifier is unchanged, so C18 records are not yet accepted by any verifier. Pull request 5 does that.

## 6. Test map

All tests are in `tests/Parchwell.Enrichment.Tests/ConsumerProcessing/`, using the existing `ConsumerTestHarness`, `RecordingConsumer` and `ConsumerTestClock`.

| Requirement | Test | Inputs | Literal expectation |
|---|---|---|---|
| R1 | existing `ConsumerProcessingWorkerTests`, `ConsumerSelfKillTests`, `ConsumerRecordCodecTests`, unchanged | as on `main` | pass with no edit |
| R2 | `ConsumerStopPlanTests.Rejects_invalid_stop_inputs` | V1: witness with `StopConfigured`; V2: with kill count 1; V2: with slow message (100, 90000, `inline`) and `WindowMs` 120000, `StopConfigured` true, mode `close`; V3: mode `drain`; V3: mode null; V4: mode `close` without `StopConfigured` | each throws, and the worker returns 1 with `reason=consumer_plan_invalid` and no `RegisterAsync` call |
| R3 | `ConsumerRecordCodecStopTests.Registration_carries_shutdown_mode` | mode `dispose`; then mode null | the key set equals the `main` key set plus `shutdown_mode`, value `dispose`; then equals the `main` key set exactly |
| R4 | `ConsumerStopPathTests.Stop_while_polling_closes_and_writes_receipt` | mode `close`; no messages; `ConsumerTestClock` advanced from its base `2026-11-15T00:00:00Z` (`ConsumerTestClock.cs` line 13) to 2026-11-28T10:00:05.0000000Z, and the stop token cancelled there | rows end with `stop_observed`, `event_utc` `2026-11-28T10:00:05.0000000Z`; `Close` then `Dispose`; a `stop-receipt.json` with `shutdown_mode` `close` and `shutdown_outcome` `ok`; no `CompleteAsync`; exit 0; empty standard error |
| R5 | `ConsumerStopPathTests.Message_in_progress_finishes` | `ProcessingDelayMs` 200; one message; stop at 50 ms into its delay; a second message available | the processed row has `processing_ended_utc` 200 ms after `processing_started_utc` and precedes `stop_observed`; the second message is never returned by `Consume` (call count after stop is 0); `RecordingRecordStore` records `CanBeCanceled` of each token it receives, and it is false for the processed row's `AppendAsync` |
| R5 | `ConsumerStopPathTests.Message_returned_during_stop_is_processed` | mode `close`; the stop token is cancelled inside the `Consume` call that returns offset 7 | a `processed` row for offset 7 before `stop_observed`; no `Consume` call after that one; exit 0 |
| R6 | `ConsumerStopPathTests.Dispose_mode_skips_close` | mode `dispose` | `Dispose` called once, `Close` never; receipt `shutdown_mode` `dispose` |
| R7 | `ConsumerStopPathTests.Close_failure_and_timeout_are_recorded` | `Close` throws `KafkaException`; then `Close` blocks past 30 s on the test clock | `shutdown_outcome` `failed`; then `timed_out`; receipt written; exit 0; in both cases `Dispose` is not called; for `timed_out`, a revoke that the blocked `Close` raises after the 30 s bound appends no row, and the receipt's `rows` equals the number of rows the store holds |
| R8 | `ConsumerStopPathTests.Callbacks_during_close_are_recorded` | `Close` invokes the revoked handler with partitions 0 and 1, then the commit handler | `partition_event` `revoked` [0, 1] and a `commit` row after `stop_observed`; every `AppendAsync` after the stop receives `CancellationToken.None` |
| R9 | `ConsumerStopPathTests.Stop_row_append_failure` | the store throws on the `stop_observed` append | exit 1; standard error starts `consumer_processing_failed reason=consumer_record_append_failed`; no receipt |
| R10 | `ConsumerStopPathTests.Receipt_failure` | the store throws on the receipt | exit 1; standard error starts `consumer_processing_failed reason=consumer_stop_receipt_failed scenario_id=` |
| R11 | `ConsumerStopPathTests.Stop_before_subscribe_uses_existing_path` | `StopConfigured` true, mode `close`; stop token cancelled before `Subscribe` | exit 1; standard error starts `consumer_processing_failed reason=consumer_fatal_error`; no `stop_observed` row; no `stop-receipt.json` |
| R12 | `ConsumerStopPathTests.Window_end_before_stop_completes` | `StopConfigured` true, mode `close`; window 1000 ms; no stop | `completion.json` written; no `stop_observed`; no receipt; exit 0 |
| R13 | `ConsumerErrorRowTests.Non_fatal_consume_error` | `Consume` throws `ConsumeException` with `ErrorCode.Local_Transport`, not fatal, then returns null | one row `source` `consume`, `error_code` -195, `is_fatal` false; member continues to its completion |
| R14 | `ConsumerErrorRowTests.Fatal_consume_error` | fatal `ConsumeException` | one row with `is_fatal` true, then the member's existing failure reason `consumer_fatal_error` and exit 1 |
| R15 | `ConsumerErrorRowTests.Error_handler_row` | the harness invokes the error callback with `ErrorCode.Local_AllBrokersDown` | one row `source` `error_handler`, `error_code` -187 |
| R16 | `ConsumerErrorRowTests.Off_by_default` | `RecordsConsumerErrors` false, non-fatal `ConsumeException` | no `consumer_error` row; the handlers' error callback is null |
| R15, R16 | `ConsumerErrorRowTests.Factory_sets_error_handler_only_when_callback_set` | a test subclass of `ConsumerBuilder<byte[], byte[]>` that exposes its `ErrorHandler` (`internal protected` in Confluent.Kafka v2.16.0, `ConsumerBuilder.cs` line 37) is passed to the internal method of section 2 item 6, first with handlers whose error callback is null, then with one set | `ErrorHandler` null; then not null. No reflection on a Confluent.Kafka private member |
| R17 | `ConsumerRecordCodecStopTests.Reason_is_cut` | reason of 1025 `a` characters; then 1024 | `reason` length 1024; then 1024 |
| 3.4, 3.5 | `ConsumerRecordCodecStopTests.Shapes` | fixed inputs | each JSON's keys, in order, equal 3.4 and 3.5 exactly |

`Local_Transport` is -195 and `Local_AllBrokersDown` is -187 in Confluent.Kafka v2.16.0 `ErrorCode.cs` (S2).

Existing tests. When an existing test's fixture is rejected by a new check, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The pull request's implementation notes list each change with the check that forces it. The cold review and Codex review check each listed change. Expected known change: `RecordingRecordStore` gains the new store method, which is a test-double addition, not an assertion change.

## 7. Deployment assumptions

One runtime assumption is named, not tested here: after SIGTERM the process keeps running until `Main` returns, because `ConsoleLifetime` handles the signal and "the POSIX signal handler returns with `Cancel = true`" (S4). No test in this plan covers it in the container. The c18-stop-smoke plan observes it live, and #597 decision 7237271679 point 4, confirmed by Hari's thumbs-up, accepts by name the risk of reworking this pull request if SIGTERM does not reach the worker. Otherwise this pull request has no live effect: no plan source sets the new inputs, so C01, C03 and C10 runs are unchanged (R1), and C18 cannot run until pull requests 3 and 4. The live questions belong to the c18-stop-smoke plan (whether SIGTERM reaches the worker; the pod end time) and to pull request 3 (the scenario values).

Inference, recorded for pull request 3: the OAuth session is created with the stop token (`Program.cs` lines 281 to 283). A token refresh that falls during the shutdown call would therefore be cancelled. Microsoft states a default access-token lifetime of 60 to 90 minutes, which "varies depending on the client application" (S3). The member Job lasts at most 1800 s (`charts/parchwell/values.yaml` line 106). If a refresh were cancelled, `Close` would end `failed` or `timed_out`, and R7 records that.

## 8. Acceptance commands

1. `dotnet build -warnaserror` -> 0 warnings, 0 errors.
2. `dotnet test tests/Parchwell.Enrichment.Tests` -> all pass, including the new classes.
3. `dotnet test tests/Parchwell.ArchitectureTests` -> all pass.
4. `dotnet test` over the solution -> all pass, showing that the verifier, scenarios and load-generator suites are unaffected.
5. `git diff --name-only origin/main...HEAD` -> only the files of section 2.
6. An ASCII scan of the changed files -> no character outside ASCII.

## 9. Sources

Tools: `curl` of raw files from confluentinc/confluent-kafka-dotnet at tag `v2.16.0` (the pinned version, `Directory.Packages.props` line 9); the Microsoft Learn MCP server; repository files on `main` at the base commit. All read 2026-11-28.
- S1. Confluent.Kafka v2.16.0:
  - `src/Confluent.Kafka/ConsumerBuilder.cs` lines 196 to 214: the error handler "Executes as a side-effect of the Consume method (on the same thread)", "Any exception thrown by your error handler will be silently ignored", and may be set only once.
  - `Consumer.cs` line 110: the handler is invoked only when set (`errorHandler?.Invoke`).
  - `IConsumer.cs` lines 670 to 685: `Close` "Commits offsets (if auto commit is enabled), alerts the group coordinator ... the partition assignment and partitions revoked handlers may be called as a side-effect".
  - `Consumer.cs` lines 615 to 625: `Dispose` "Releases all resources used by this Consumer without committing offsets and without alerting the group coordinator".
- S2. Confluent.Kafka v2.16.0 `src/Confluent.Kafka/ErrorCode.cs`: `Local_Transport = -195`, `Local_AllBrokersDown = -187`. Read at lines 56 and 96; the tests use the enum members, and the literals in section 6 restate their values.
- S3. Access tokens in the Microsoft identity platform, "Token lifetime" (https://learn.microsoft.com/entra/identity-platform/access-tokens#token-lifetime): "a random value ranging between 60-90 minutes". Configurable token lifetimes: the default "varies depending on the client application requesting the token" (https://learn.microsoft.com/entra/identity-platform/configurable-token-lifetimes).
- S4. .NET Generic Host, "Host shutdown" (https://learn.microsoft.com/dotnet/core/extensions/generic-host#host-shutdown): `ConsoleLifetime` handles SIGTERM; "the POSIX signal handler returns with `Cancel = true`", and `Main` continues to exit gracefully. The worker relies on this to finish its stop path (docs/blueprint.md S55).

## 10. Plan-check answers

(a) Effort that scales with a number unknown at planning time: none. The tests use fixed inputs, and the worker adds no loop. Its one wait, the 30 s `Close` bound, already exists.

(b) Stricter than the parent. Five points. A stop counts only after `Subscribe` returns (3.6 step 1), because before that the worker has no consumer to shut down. `consumer_error` rows carry a `source` field, so the two origins Amendment 1 names stay distinguishable. A `failed` or `timed_out` shutdown still writes the receipt and exits 0 (R7), because the receipt is the evidence of what happened, and Amendment 1 lists the shutdown outcome as a receipt field. V2 also excludes a slow message with a stop time, which Amendment 1 does not mention. Reason: the "finish the message in progress" rule is defined only for an inline delay; a paused slow message would mix two endings. C18 needs neither. The 1024-unit cut of `reason` (R17) is also added, to keep a row inside the existing line limit.

(c) Tests that depend on code, data or behaviour neither on `main` nor in this pull request: none. The `ErrorCode` values are read from the enum (S2).

## 11. Open questions for Hari

None.

## 11a. Questions answered by Hari

Answered by [decision 7238011332](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7238011332): point 1, the admission moves to pull request 3; point 2, the wiring test is option (a), recorded in section 2 item 6 and the R15/R16 test row. The question as put, for the record:

1. Move "the worker and chart admit C18" from pull request 2 to pull request 3? The approved split ([decision 7237226936](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7237226936) point 1, in the order of the c18-stop-smoke plan's section 12) puts it in pull request 2. That entry's text was this planning session's own wording. Recommendation: move it to pull request 3. Evidence:
   - The chart's consumer-scenario switch, `parchwell.isConsumerScenario` (`_consumer.tpl` lines 2 to 5), also routes the start-line, load generator, verifier and judge Jobs (`jobs.yaml` line 22) and the service accounts (`serviceaccounts.yaml` line 3). The schema's consumer branch (`values.schema.json` line 1221) admits the modes `start-line`, `loadgen`, `verify` and `judge` for every listed scenario. Admitting C18 there admits those modes too, whose load generator and verifier admission the split puts in pull requests 3 and 5.
   - The worker alone could accept `C18` in `ConsumerProcessingOptions.Validate` (line 27). But `ScenarioConsumerProcessingPlanSource.GetPlan` (line 24) would still return null for it until pull request 3 adds the scenario file, so a C18 member would fail `consumer_plan_invalid` at once. That change is reachable by no run.

   Alternative: keep the admission here, adding the options, chart and schema changes, with tests, in a revision of this plan. Either way the stop path above is unchanged.

## 12. Deferred work

- Plan-source mapping. Mapping the C18 scenario's stop time, shutdown mode (default `close`) and consumer-error setting to these inputs, and admitting C18 in `ConsumerProcessingOptions.Validate` and the chart. Both are pull request 3: the scenario mapping under the approved split, and the admission under [decision 7238011332](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7238011332) point 1. It is on the verdict path. The deferral is required by decision 7237226936 point 1's limit, which keeps scenario files out of this pull request, and by [decision 7238011332](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/613#issuecomment-7238011332) point 1. Responsible: the `plan-pr` session for pull request 3, after the smoke's evidence (c18-stop-smoke plan, section 12). Gate it blocks: any C18 run. Completion evidence: pull request 3 merged on `main`.
- Verifier acceptance of the new rows and receipt. This is pull request 5. It is deferred for the same reason (no verifier files here). Responsible: the `plan-pr` session for pull request 5, after the smoke's evidence (c18-stop-smoke plan, section 12). Gate it blocks: acceptance of any C18 record by the verifier, and so the C18 verdict. Completion evidence: pull request 5 merged on `main`.

## 13. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4 or the scope in section 2. That includes any need to touch a workflow, scenario, expected outcome or verifier file, or a file outside section 2, which decision 7237226936 point 1 sends back to Hari.

## 14. Size estimate

Estimate, dated 2026-11-28, from the sizes of the touched files (worker 219 lines, codec 106): about 150 to 250 changed production lines and 450 to 650 test lines, in 10 to 12 files. Not measured; reported, not enforced.

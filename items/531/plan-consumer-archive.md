# consumer-archive contract-grade plan

Revision: 3

This plan completes archive support for the consumer scenarios C01, C03 and C10. `main` already admits these scenarios to the phase evidence archive and plans their member Jobs. It still does not archive the consumer record, the consumer start snapshot or the consumer image, and it still fails on a real readiness record and on consumer verification. ADR-020 holds publication on an archive gap, and the archive cannot be taken after a dispatch.

One pull request. Base: `main` at `3f46ccc06c39ac35510dc49f60f4c0a9081107a4`, after pull request 538 (native telemetry, issue 494) merged. Changes since revision 2 are in section 16. Line numbers below are at that base unless another commit is named.

## 1. Goal and authority

Goal: a C01, C03 or C10 phase whose evidence is all present produces `archive_complete: true`. A phase with missing or mismatched consumer evidence produces a named gap. Existing scenarios keep their archive contract unchanged.

Authority, in creation order:
- Issue 531 body: archive support for the three consumer scenarios, so that their evidence is archived like the producer scenarios'. Predecessor: the pull request of issue 507 (merged). This pull request must merge before issue 508's C03 batch dispatch.
- [Decision 6926375790 on issue 507](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6926375790), point 3, adopting Q8 of [handoff 6926216145](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6926216145): "a new split entry that admits the three scenarios to the archive and adds the consumer record to its inventory".
- [Decision 6926376436 on issue 499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6926376436): adds this split entry; the batch's decisions apply to it.
- [Decision 6926376950 on issue 531](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6926376950) adopts the [standing decision 6895535291](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291), [decision 6894614673](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894614673) points 1 and 2, and the entry decision.
- [Decision 6943488058](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6943488058) on issue 531, under standing decision point 2: Q1 (record blobs probed by name up to the first missing one; a missing `completion.json` is not a gap) and Q3 (`enrichment` in `run.images` and `archive_pack._context`, for C01, C03 and C10 only).
- [Decision 6943512296](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6943512296) on issue 531, Hari: Q2 (the archive expects no consumed-metadata capture for `consumer-verify`; archive-smoke.md states it as a limit; a departure from ADR-020 line 651 for consumer scenarios only; no new split entry) and Q4 option A (the risk that a consumer phase's archive overruns `ArchiveDeadlineSeconds` 600 s is accepted by name and measured at issue 508's smoke run).
- [Approval 6943704305](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6943704305) of revision 2 at `c26d43e0553472d277bb9d5ac7176c3f0f0272f6`. This revision is an amendment of that plan and needs approval under standing decision point 3.
- [Decision 6955981131](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6955981131): Claude Code implements this issue; Codex reviews.
- [Handoff 6956107567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6956107567), evidence only: revision 2 no longer fits `main` after pull request 538.
- ADR-020 (`docs/blueprint.md` lines 624 to 662), line 651; ADR-022 (lines 736 to 798); `docs/operations/archive-smoke.md` "Phase evidence retention", line 95 (inventory comes from published contracts; a listing cannot establish completeness) and line 103 (the native-monitoring amendment: missing or partial monitoring evidence records a gap).

## 2. Scope

Files allowed to change:
- `infra/scripts/phase/archive.py`
- `infra/scripts/phase/archive_pack.py`
- `infra/scripts/phase/archive_consumer.py` (new, optional). The implementer may put the consumer adapter here, or inside `archive.py`.
- `infra/scripts/phase/tests/test_archive_consumer.py` (new)
- `infra/scripts/phase/tests/test_archive_inventory_integration.py`, only the fixture change named in section 6 under the fixture rule
- `docs/operations/archive-smoke.md`, the section "Phase evidence retention" only
- `docs/operations/consumer-workflow.md`, the section "Not yet in place" only
- `items/531/state.md` (the item state file, written by the implementing session)

Paths that must not change: every other file. In particular:
- every other file under `infra/scripts/phase/tests/`, except as the fixture rule in section 6 allows;
- `infra/scripts/phase/phase.py`, `consumer_repeat.py`, `archive_json.py`, `archive_nonledger.py`, `archive_native_monitoring.py`;
- the function `collect_native_monitoring` and `NATIVE_MONITORING_MODES` in `archive.py` (lines 1875 to 1973);
- `src/`, `charts/`, `infra/ephemeral/`, `infra/persistent/`, `.github/workflows/`, `docs/blueprint.md`, `scenarios/`, `results/`.

No protected path is in scope. `.github/CODEOWNERS` assigns no owner to `/docs/operations/`, and `infra/scripts/` has no rule.

## 3. External contracts

### 3.1 Inputs the archive reads (fixed by merged code; this pull request does not change them)

Phase identity, from `phase.json`, written by `phase.py` `prepare` and `prepare_shared`:
- `scenario` is `C01`, `C03` or `C10`; `phase` and `configuration` are each `C0` or `C1`, and are equal.
- `run_id` is `<lower scenario>-<GITHUB_RUN_ID>-<GITHUB_RUN_ATTEMPT>`, for example `c03-123-1`.
- No `run_kind` and no `purpose` key.
- `images` has `loadgen`, `verifier` and `enrichment`, each `{"digest", "reference"}`, with reference `<login_server>/parchwell/<worker>@<digest>`.

Phase status, written by `consumer_repeat.py`. Each repeat appends exactly six records, in this order (`docs/operations/consumer-workflow.md` "Order of one repeat"): `consumer-subject`, `consumer-witness`, `consumer-readiness`, `start-line`, `loadgen`, `verify`. The phase then appends one `judge` record.
- Job names: `<run>-<phase lower>-r<n>-<mode>` per repeat, and `<run>-<phase lower>-judge`.
- A member record carries `job`, `repeat`, `mode`, `render`, `apply`, `wait`, `collect`, `delete`, `confirm`, `deadline` and `outcome`, and may carry `reason` and `collect_gap` (`consumer_repeat.py` lines 39 to 42, 154 to 155).
- A readiness record (`consumer_repeat.py` lines 80 to 84) has exactly the keys `job`, `repeat`, `mode`, `polls`, `elapsed_seconds`, `last_condition` and `outcome`, plus optional `reason`.
  - `outcome` is `ready`, `not_observed` or `not_attempted`.
  - `polls` and `elapsed_seconds` are integers of at least 0, or `null` in a `not_attempted` record.
  - `last_condition` is `null` or one of `ready`, `list_failed`, `download_failed`, `row_invalid`, `member_count`, `not_assigned`, `overlap`, `coverage` (`consumer_repeat.py` lines 220 to 295).
  - It has no `apply` key and no Kubernetes Job.

Member Job collection: `lifecycle.sh collect-job` writes `<artifacts>/<job>/collection-status.json`, `pods.json` and `<pod>.<kind>.<container>.log`. The member pod has one container, `enrichment-consumer`, and no init containers (`charts/parchwell/templates/_consumer.tpl`).

Ephemeral output `blob.containers` has exactly the keys `ledger`, `verifier`, `claim_check`, `single_action` and `native_monitoring` (`infra/ephemeral/outputs.tf` line 13), plus `archive_smoke_source` in archive smoke runs only. It has no `consumer_record` key (decision 6926375790 point 1).

Consumer record, in the ephemeral container `consumer-record`. The archive names this container literally, as `consumer_repeat.py` line 17 and `BlobConsumerRecordStore.cs` line 13 do. It neither reads nor requires a `consumer_record` key, and `load_context` keeps its current container checks (lines 779 to 784). The prefix is `<run_id>/<phase>/r<n>/consumer/<pod>/` (`ConsumerProcessingOptions.cs` line 22). `<pod>` is the member's pod name, a DNS label, so URI escaping leaves it unchanged.
- `registration.json`: a block blob, at most 65536 bytes. Fields (`ConsumerRecordCodec.cs` lines 15 to 22): `schema_version` 1, `kind` `consumer_registration`, `run_id`, `scenario_id`, `configuration_id`, `phase`, `repeat_number`, `role`, `instance_id`, `job_name`, `pod_name`, `pod_uid`, `node_name`, `source_commit`, `group_id`, `effective_settings`, `registered_utc`.
- `record/NNNNNN.jsonl`: append blobs, numbered from `000000` with six digits (`BlobConsumerRecordStore.cs` line 29). The worker creates blob k+1 only when blob k holds 40000 rows (line 27).
- `completion.json`: a block blob, at most 65536 bytes (`ConsumerRecordCodec.cs` lines 28 to 32). Fields: `schema_version` 1, `kind` `consumer_completion`, `run_id`, `scenario_id`, `configuration_id`, `phase`, `repeat_number`, `role`, `instance_id`, `job_name`, `pod_uid`, `source_commit`, `rows`, `record_blobs`, `messages_processed`, `closed_utc`. `record_blobs` is `(rows + 39999) / 40000`. The worker writes it only after a clean close; a self-killed subject, a failed member or a watchdog end writes none (`ConsumerProcessingWorker.cs` line 95, lines 157 to 158).

Native monitoring for member Jobs, on `main` and unchanged here: `collect_native_monitoring` (lines 1884 to 1973) requires an index at `<run>/<phase>/<job>/<pod_uid>/native-monitoring/index.json` for every started unit whose mode is in `NATIVE_MONITORING_MODES` (lines 1875 to 1881), which includes `consumer-subject`, `consumer-witness`, `start-line`, `loadgen` and `verify`. The consumer host publishes its chunks and index only at shutdown (`KafkaNativeMonitoringCapture.CompleteAsync`, `src/Parchwell.Kafka/Statistics/KafkaNativeMonitoringCapture.cs` lines 306 to 335), so a SIGKILLed subject leaves no index. See section 12, D2.

Verifier outputs for consumer scenarios, in the `verifier` container:
- start-line: `start-line.json`, `broker-inventory.json`, `start-line-completion.json`, and `<run>/<phase>/r<n>/consumer-start.json` (`StartLineHandler.cs` line 90). The start-line receipt does not reference `consumer-start.json`.
- verify (verifier mode `consumer-verify`): `result.json` and `verify-completion.json`, and no consumed-metadata capture (`ConsumedMetadataCapture.cs` lines 49 to 53; `PhasePaths.cs` lines 28 to 35). Decision 6943512296 point 1 accepts this for consumer scenarios.
- judge: `<run>/<phase>/candidate.json` and `judge-completion.json`.

### 3.2 Already on `main` at the base (not changed by this pull request)

- `manifest_run` admits C01, C03 and C10 with phases C0 and C1 and run prefixes `c01`, `c03`, `c10` (lines 1976 to 1990).
- `collect_ordinary` plans `consumer-subject`, `consumer-witness`, `start-line`, `loadgen` and `verify` per repeat and `judge` once, and accepts one `consumer-readiness` record per repeat whose `job` matches (lines 1781 to 1797).
- `ordinary_units(..., consumer=True)` plans the same units and checks the readiness job name (lines 708 to 734); `execute` passes the flag (lines 2121 to 2122).
- Native monitoring of member Jobs (section 3.1).

### 3.3 Outputs this pull request changes

Archive manifest `run` object (`manifest_run`, `validate_manifest` line 606):
- For C01, C03 and C10 only, `images` has exactly the keys `loadgen`, `verifier`, `probe` and `enrichment`. `probe` is `null`. `enrichment` matches `[a-z0-9.-]+/parchwell/enrichment@sha256:[0-9a-f]{64}` or is `null`. For every other scenario, `images` keeps exactly `loadgen`, `verifier` and `probe` (decision 6943488058 point 2).
- `archive_pack.py` `_context` (lines 23 to 42) accepts the same per-scenario key sets.

Archive manifest `objects[*].source.container` may also be `consumer-record` (the allowlist at line 649). The destination name is `<prefix>objects/consumer-record/<source name>`, by the existing rule at line 650.

New object `kind` values: `consumer_registration`, `consumer_completion`, `consumer_record`. The verifier object `consumer-start.json` uses the existing kind `verifier_output`.

Phase status projection (`phase-status.json` runner object, `project_phase_status` lines 1645 to 1669):
- A record whose `mode` is `consumer-readiness` projects exactly these keys: `job`, `mode`, `repeat`, `outcome`, `render`, `apply`, `wait`, `collect`, `delete`, `confirm` (each `null`), `polls`, `elapsed_seconds` and `last_condition`. Its `outcome` must be `ready`, `not_observed` or `not_attempted`; `polls` and `elapsed_seconds` must be present and be integers of at least 0 or `null`; `last_condition` must be present and be `null` or a value from section 3.1.
- A record of any other mode keeps its current projected keys and outcome set; `ready` and `not_observed` are rejected for it.

New gap boundaries, each with code `monitoring_incomplete`:
- `consumer registration identity`
- `consumer completion contract`
- `consumer record inventory`
- `missing readiness status`

No new gap code. `GAP_CODES` is unchanged.

## 4. Requirements

R is the repeat prefix `<run>/<phase>/r<n>/`, P a member pod name, J a member Job and U its pod UID.

1. Already on `main` (section 3.2): `manifest_run` admits the consumer phases. This pull request changes only their `images` (requirements 2 and 3).
2. WHEN the scenario is C01, C03 or C10 and `images.enrichment` is absent or `null`, THEN the collector records gap `monitoring_incomplete`, boundary `image identity unavailable`, the existing gap for `loadgen` and `verifier` (line 2091).
3. WHEN `validate_manifest` or `archive_pack._context` sees a consumer scenario whose `images` key set is not `{loadgen, verifier, probe, enrichment}`, or a non-consumer scenario whose key set is not `{loadgen, verifier, probe}`, THEN it rejects the manifest or context.
4. WHEN a phase status is projected, THEN readiness records project as section 3.3 sets. WHEN a readiness record breaks section 3.3, or a record of any other mode has outcome `ready` or `not_observed`, THEN `project_phase_status` raises `InvalidArchiveConfig`, and the inventory records the existing `inventory construction` gap.
5. Already on `main` (section 3.2): the planned records of a consumer phase, and `InvalidArchiveConfig` for an unplanned, duplicate or misnamed record.
6. WHEN a planned readiness record of a consumer phase is missing, THEN gap `missing readiness status` with expected unit `<run>-<phase lower>-r<n>-consumer-readiness`. `consumer-readiness` stays out of `expected_units`, as on `main`.
7. WHEN a record of mode `consumer-subject` or `consumer-witness` exists with `apply` null, THEN gap `required job not started`. This extends the existing rule at line 1808 to the two member modes. The fixed objects of an unstarted start-line or verify stay `not_started`, as today.
8. For each started member Job J, after `read_job_pods` as today, for each pod P with UID U the archive archives:
   1. `R + "consumer/" + P + "/registration.json"`, kind `consumer_registration`, required.
      - WHEN its JSON is not an object, or `kind` is not `consumer_registration`, or any of these differ, THEN gap `consumer registration identity`: `run_id`, `phase`, `scenario_id`, `configuration_id` (from `phase.json`); `repeat_number` (the repeat); `role` (`subject` for mode `consumer-subject`, `witness` for `consumer-witness`); `job_name` (J); `pod_name` and `instance_id` (P); `pod_uid` (U); `source_commit`.
      - The object stays archived either way.
   2. `R + "consumer/" + P + "/completion.json"`, kind `consumer_completion`, optional: archived only when its properties show it exists, as `optional_source` does (lines 1176 to 1185).
      - WHEN it exists and its JSON is not an object, or `kind` is not `consumer_completion`, or any identity field of 8.1 except `pod_name` differs, or `rows`, `record_blobs` or `messages_processed` is not an integer of at least 0, or `record_blobs` is not `(rows + 39999) // 40000`, THEN gap `consumer completion contract`, and the record blobs are found as in 8.3b.
   3. Record blobs `R + "consumer/" + P + "/record/" + "%06d" % k + ".jsonl"`, kind `consumer_record`, each required:
      - (a) WHEN a valid completion gives `record_blobs` K, THEN k runs from 0 to K - 1. WHEN the blob with index K exists, THEN gap `consumer record inventory`, naming that blob; the extra blob is not archived. A required blob that is absent gets the existing gap `source_missing`.
      - (b) WHEN there is no completion, or it is invalid, THEN the archive checks blob 0, 1, 2 and so on by properties, and archives each one that exists, stopping at the first absent index. Missing completion is not a gap by itself (decision 6943488058 point 1). WHEN blob 0 is absent, THEN gap `consumer record inventory`, naming blob 0.
      - Probing also stops at the existing `object_limit` gap. WHEN a properties probe raises a `Gap`, THEN the archive records that gap's own code and boundary for that name, as `optional_source` does at lines 1181 to 1183, and probing stops.
   4. Native monitoring for J stays as on `main` (section 3.1); this pull request does not change it.
9. WHEN mode is `start-line` in a consumer phase and the Job started, THEN `R + "consumer-start.json"` is a required `verifier_output`. When it did not start, the object is `not_started`. The fixed-object cross-check (`ordinary_fixed_inventory`, lines 376 to 405, called at line 2120) lists it for consumer phases; that function has no scenario argument today, so it gains one.
10. WHEN mode is `verify` in a consumer phase, THEN `result.json` and `verify-completion.json` are archived as today, and the capture call of `collect_ordinary` (lines 1863 to 1872) is skipped, so no consumed-metadata capture is expected or recorded (decision 6943512296 point 1). The window inspection of `result.json` (line 968) is unchanged.
11. Loadgen and judge in a consumer phase follow the ordinary path unchanged.
12. WHEN every object of requirements 8 to 11 and every native-monitoring object of section 3.1 is verified and no gap is recorded, THEN `archive_complete` is `true` and `execute` returns 0. WHEN any gap is recorded, THEN `archive_complete` is `false` and `execute` returns 1.
13. A01, B04, B10 and batch scenarios produce byte-identical manifests to `main` at the base for the same inputs.

## 5. Invariants and safety rules

- The archive never lists a container. It reads, by name, the objects a contract names (archive-smoke.md line 95). Probing in 8.3b reads properties of fixed names one at a time, bounded by the existing object limit.
- No policy value changes: `ArchiveMaxBytes`, `ArchiveMaxObjectBytes`, `ArchiveMaxObjects`, `ArchiveDeadlineSeconds`, `ArchiveTransferPasses`, `MetadataChunkMaxBytes`, `MetadataRowMaxBytes`, `_common.POLICY` and the limits in `archive_native_monitoring.py` stay as at the base.
- No native-monitoring rule changes. A missing or incomplete member index stays a gap (archive-smoke.md line 103).
- No identity, role or network change. The workflow principal already reads `consumer-record` (`infra/ephemeral/identity.tf` lines 43 to 49).
- No result, verdict, expected outcome or scientific classification changes. An archive gap stays separate from the science (ADR-020 line 651).
- Message bodies stay excluded. The consumer record holds keys, offsets and identities, not message values (ADR-022 "Record", line 754).

## 6. Test map

All new tests are in `infra/scripts/phase/tests/test_archive_consumer.py`. They run offline with the `BlobCLI` (lines 74 to 104) and `Harness` (lines 107 to 190, including `Harness.job` and `Harness.run`) of `test_archive_inventory_integration.py`, imported or copied without changing that file. `Harness.job` writes a native-monitoring index for every native unit, and `Harness.run` writes `ephemeral-output.json` with the five live `blob.containers` keys of section 3.1.

Fixture: run `c03-42-1`, phase `C0`, `GITHUB_RUN_ID` `42`, attempt `1`, source commit `a` times 40, run kind `scientific`. Images `loadgen`, `verifier` and `enrichment` have digest `sha256:` plus `b` times 64. Pod names and UIDs are those `Harness.job` writes (`<job>-pod`, `uid`). Readiness records carry `{polls: 1, elapsed_seconds: 5, last_condition: "ready", outcome: "ready"}` unless a test says otherwise. Each member has a registration whose fields match requirement 8.1.

| Test | Requirement | Input | Expected |
|---|---|---|---|
| T1 `test_consumer_manifest_images` | 2, 3 | each of C01, C03, C10, phases C0 and C1, complete fixture | `run.images` keys `{loadgen, verifier, probe, enrichment}`; `probe` `null`; `enrichment` is the fixture reference |
| T3 `test_image_key_sets_by_scenario` | 2, 3 | `validate_manifest` and `archive_pack._context` with C03 and 4 keys; C03 and 3 keys; B04 and 4 keys; B04 and 3 keys; C03 with `enrichment` `x.azurecr.io/parchwell/loadgen@sha256:` plus `b` times 64 | accept, reject, reject, accept, reject. A C03 `execute` with `enrichment` null records gap `image identity unavailable` |
| T4 `test_readiness_projection` | 4 | readiness records `{polls: 3, elapsed_seconds: 12, last_condition: "ready", outcome: "ready"}`, `{polls: 60, elapsed_seconds: 300, last_condition: "list_failed", outcome: "not_observed"}` and `{polls: 0, elapsed_seconds: 0, last_condition: null, outcome: "not_attempted"}`, each with `job` `c03-42-1-c0-r1-consumer-readiness`, `repeat` 1 | each projects to exactly `{"job": "c03-42-1-c0-r1-consumer-readiness", "mode": "consumer-readiness", "repeat": 1, "outcome": <outcome>, "render": null, "apply": null, "wait": null, "collect": null, "delete": null, "confirm": null, "polls": <polls>, "elapsed_seconds": <elapsed>, "last_condition": <condition>}`. `polls: -1`, `polls: true`, `elapsed_seconds: 1.5`, `last_condition: "other"`, outcome `complete`, a missing `polls` key, and a `verify` record with outcome `ready` each raise `InvalidArchiveConfig` |
| T5 `test_complete_consumer_phase` | 6 to 12 | five full repeats; each member has registration, completion with `rows` 40001 and `record_blobs` 2, blobs `000000` and `000001`, no blob `000002`; start-line writes `consumer-start.json`; judge complete | `archive_complete` `true`; return 0; `gaps` empty; `expected_units` has 26 entries; `captures` empty; objects include `objects/consumer-record/c03-42-1/C0/r1/consumer/c03-42-1-c0-r1-consumer-subject-pod/record/000001.jsonl` (kind `consumer_record`) and `objects/verifier/c03-42-1/C0/r1/consumer-start.json` (kind `verifier_output`) |
| T6 `test_killed_subject_without_completion` | 8.3b, 8.4, 12 | as T5, but each subject has no completion, blob `000000` only, and no native-monitoring index | no gap with a `consumer_*` boundary; one `consumer_record` object and no `consumer_completion` object per subject; for each subject Job, gap `source_missing` naming its index and gap `monitoring_incomplete`, `native index unavailable`, with that Job as unit; no other gap; `archive_complete` `false` (section 12, D2) |
| T7 `test_extra_record_blob_after_completion_count` | 8.3a | repeat 1 witness completion `record_blobs` 1, `rows` 5, blob `000001` present | gap `monitoring_incomplete`, `consumer record inventory`, object name `c03-42-1/C0/r1/consumer/c03-42-1-c0-r1-consumer-witness-pod/record/000001.jsonl`; no object for that blob; `archive_complete` `false` |
| T8 `test_missing_record_blob_named_by_completion` | 8.3a | completion `rows` 40001, `record_blobs` 2, blob `000001` absent | gap `source_missing` for `.../record/000001.jsonl`; that object's state is `missing` |
| T9 `test_registration_identity_mismatch` | 8.1 | repeat 2 subject registration `pod_uid` `other`; then `role` `witness` | each records gap `consumer registration identity`; the registration object is still `verified` |
| T9b `test_completion_wrong_kind` | 8.2 | repeat 2 witness completion with `kind` `consumer_registration`, blob `000000` present | gap `consumer completion contract`; blob `000000` archived by probing |
| T10 `test_invalid_completion_falls_back_to_probe` | 8.2, 8.3b | completion `record_blobs` 3 with `rows` 5; blob `000000` present, `000001` absent | gap `consumer completion contract`; blob `000000` archived; no `source_missing` gap for `000001` or `000002` |
| T11 `test_missing_first_record_blob` | 8.3b | no completion, no blob `000000` | gap `consumer record inventory` naming `.../record/000000.jsonl` |
| T11b `test_probe_gap_stops` | 8.3b | no completion; `show` of blob `000001` fails with a non-missing error | the gap `transport`, boundary `properties`, naming `.../record/000001.jsonl`; no probe of `000002` |
| T12 `test_missing_readiness_record` | 6 | repeat 1 status without its readiness record | gap `missing readiness status`, expected unit `c03-42-1-c0-r1-consumer-readiness` |
| T13 `test_unstarted_member_job` | 7 | repeat 5 `consumer-subject` and `consumer-witness` records with `apply` null and outcome `not_attempted`, reason `orchestration_deadline`, and the other four r5 records as `skip_repeat` writes them | gaps `required job not started` for units `c03-42-1-c0-r5-consumer-subject`, `-consumer-witness`, `-start-line` and `-verify`; the fixed verifier objects of r5 are `not_started` |
| T15 `test_non_consumer_manifests_unchanged` | 13 | the existing suite under `infra/scripts/phase/tests/` | passes; the only changed existing file is the fixture change below |

Boundaries: T5 uses 40001 rows to put `record_blobs` at the 40000 boundary plus one. T7 and T8 cover one above and one below the count. T11 covers zero blobs. T4 covers `polls` 0 as accepted.

Existing test fixture rule. When an existing test's fixture is rejected by a new check in this plan, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it. The cold review and Codex review check each listed change. Modelled on [issue 404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.

Expected change, the only one known: `test_archive_inventory_integration.py` lines 870 to 871, in `test_consumer_members_require_native_indexes_and_readiness_is_not_a_job`, write a readiness record with outcome `complete` and no `polls`, `elapsed_seconds` or `last_condition`. Requirement 4 rejects it. Under (a), the record becomes `{'mode': 'consumer-readiness', 'repeat': repeat, 'job': f'c03-42-1-c0-r{repeat}-consumer-readiness', 'outcome': 'ready', 'polls': 1, 'elapsed_seconds': 5, 'last_condition': 'ready'}`, and every assertion of that test stays.

## 7. Deployment assumptions

- DA1. A consumer phase's archive fits the existing bounds: 1024 objects, 256 MiB per object, 16 GiB in total, and a 600 s deadline (`archive.py` lines 5 to 8).
  - Objects, estimate of 2026-11-17 computed by script from the inventory in section 4 and section 3.1, not measured: 246 per phase without record blobs, leaving 776 objects for record blobs. Basis: 142 source objects (per repeat 6 verifier, 8 producer, 4 member record controls, 5 native indexes and 5 native chunks; plus 2 judge objects), 79 runner files, and 25 optional producer diagnostics. One native chunk per client is an inference from `KafkaNativeMonitoringCapture.cs` lines 216 to 217; the client count per Job (one per member, start-line and loadgen, two for verify, with loadgen statistics referenced as existing captures) is an inference from the roles in `archive_native_monitoring.py`.
  - Per object: a record blob holds at most 40000 rows. It exceeds 256 MiB only if rows average more than 6710 bytes, an inference from the codec's fields.
  - Deadline: no phase archive has run live. Estimate of 2026-11-17, not measured: archive smoke run 43744609382 spent 20.3 s in its archive step for one data object and its controls, about 1 s per storage CLI call. `Transfer.copy` makes 6 calls per source object and `Transfer.copy_local` 4 per runner file. For the 246 objects above that is about 1193 calls, about 1200 s before record blobs, which exceeds the 600 s deadline. Revision 2 estimated about 900 s; the rise is the native-monitoring objects that pull request 538 added. The basis is one run and call counts read from code.
  - How evidence will be taken: the archive receipt and Actions step duration of issue 508's smoke run, on fresh connections, after the load generator ends.
  - An exceeded bound is a gap, not a silent loss: `object_limit`, `source_too_large`, `byte_limit` or `deadline`. No offline test covers live size or time.
  - Accepted by name in [decision 6943512296](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6943512296) point 2 (option A): the risk that a consumer phase's archive overruns 600 s, measured at issue 508's smoke run; if its receipt shows a `deadline` gap, Hari decides the response before the C03 batch dispatch.

## 8. Acceptance commands

```sh
python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_archive_consumer.py' -v
python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'
npm run check:docs
```

- The first must show every test of section 6 except T15 passing.
- The second is the CI step of `dotnet-verification.yml` line 207. It reaches the phase suite through `infra/scripts/tests/test_phase_bridge.py` and must pass with only the existing-file change named in section 6 (T15).
- The third must report no new warning in the two changed docs.

Final-head CI: the `dotnet-verification.yml` run on the pull request.

## 9. Documentation changes

- `docs/operations/archive-smoke.md` "Phase evidence retention": add one paragraph that states the consumer inventory of requirements 6 to 10. It says where the record blob count comes from (the completion receipt, or sequential probing when there is none); that consumer verification has no consumed-metadata capture, stated as a limit under [decision 6943512296](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6943512296) point 1; that consumer phases carry the `enrichment` image; and that a self-killed subject leaves no native-monitoring index, so its repeat records a monitoring gap under the native-monitoring amendment. The paragraph ends with this exact sentence: "For C01, C03 and C10, the manifest's four-key `images` object, the source container `consumer-record` and the object kinds `consumer_registration`, `consumer_completion` and `consumer_record` are set by [the issue 531 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6943199291), section 3.3, which extends the approved specification's manifest schema for those scenarios only."
- `docs/operations/consumer-workflow.md` "Not yet in place": replace the archive sentence with one that says the archive admits C01, C03 and C10 with their consumer records, links the archive-smoke section, and says that a repeat whose subject kills itself records a native-monitoring gap until Hari decides section 12 D2.

## 10. Plan-check questions

- (a) Steps whose effort scales with a number unknown at planning time:
  - Record-blob probing in 8.3b scales with the record blob count, capped by `ArchiveMaxObjects`, whose exhaustion records gap `object_limit` and continues.
  - Archive run time scales with object count, capped by `ArchiveDeadlineSeconds` 600, whose exhaustion records `deadline` gaps (DA1, accepted by name).
  - Tests use fixed fixture sizes. No implementation step has an uncapped runtime count.
- (b) Where this plan is stricter than its parent: the parent (Q8 of handoff 6926216145, adopted by decision 6926375790) names only "admits the three scenarios ... adds the consumer record". This plan also binds registration identity (8.1), cross-checks the completion count (8.2, 8.3a), adds `consumer-start.json` and the enrichment image, requires the readiness record (6), adds strict readiness projection checks (4), and extends the not-started rule to member Jobs (7). Each follows an existing pattern: producer registration (line 1216), publication index (line 1374), image identity (line 2091), the strict projection (lines 1645 to 1669) and the not-started rule (line 1808).
- (c) Tests that depend on code or data neither on `main` nor in this pull request: none. Every input shape in section 3.1 is read from merged code at the base. Live sizes and time are DA1. The killed-subject native index follows `main` behaviour (D2).

## 11. Sources

Tool: Microsoft Learn documentation tool, read 2026-11-17.
- [Azure Blob Storage REST API](https://learn.microsoft.com/rest/api/storageservices/blob-service-rest-api), operations table: Get Blob and Get Blob Properties apply to "Block, append, and page blobs". The archive's existing `show` and ETag-conditional `download` therefore apply to the append record blobs.

No Kafka, librdkafka or Confluent.Kafka behaviour is relied on.

Repository sources: all at `3f46ccc06c39ac35510dc49f60f4c0a9081107a4`, cited by file and line above. The native-monitoring amendment is [issue 494 comment 6926572789](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6926572789), point 5: "Intentional process kill may leave monitoring capture incomplete and the final exporter tail unknown; do not invent successful drain/flush or extend the process window."

## 12. Deferred work

- D1. Confirm DA1 live.
  - Evidence: none yet (section 7).
  - On the verdict path: yes, because an archive gap holds C03 publication.
  - Why deferral is permitted: no consumer phase can run before this pull request and issue 508's scenario merge.
  - Responsible: issue 508's smoke run, under its plan.
  - Completion evidence: an `archive-receipt.json` with `archive_complete: true` from that run.
  - Gate it blocks: issue 508's C03 batch dispatch.
  - Exhaustion action: a gap in that receipt is brought to Hari before the batch dispatch, not fixed by raising a policy value.
- D2. Native-monitoring index of a self-killed subject.
  - Evidence: the consumer host writes its native index only at shutdown (section 3.1), and SIGKILL has no shutdown. The native-monitoring amendment (point 5) keeps a missing index a gap, and `collect_native_monitoring` records `native index unavailable` (line 1919). So every C10 or C01 repeat with a configured kill records an archive gap, and ADR-020 holds that phase's publication. C03 has no kill and is not affected.
  - On the verdict path: yes, for C10 and C01 (issue 510's batch and its verdict).
  - Why deferral is permitted: resolving it needs a change to the approved native-monitoring rule or to the consumer worker, neither of which this split entry may make (section 5 and stop rule). It does not affect C03, whose batch this pull request gates.
  - Responsible: Hari, through the control session, before issue 510's plan is approved.
  - Completion evidence: a decision on issue 510 or 499.
  - Gate it blocks: issue 510's C10 and C01 batch dispatch.

## 13. Open questions for Hari

None. Revision 1's Q1 to Q4 are decided (decisions 6943488058 and 6943512296). D2 is a deferred decision for a later gate, not a question this pull request depends on.

## 14. Size estimate

Estimate, 2026-11-17, from the requirements above, not measured: about 200 changed lines in `archive.py` (or 80 there plus 150 in a new `archive_consumer.py`), 10 in `archive_pack.py`, 450 in the new test file, 1 in `test_archive_inventory_integration.py`, and 20 in the two docs. That is five to seven files. Size is reported, not enforced.

## 15. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope of this plan. That includes changing any file or function outside section 2, any policy value in section 5, any native-monitoring rule, or any gap code.

## 16. Changes since revision 2

- Base moved from `10cacff2` to `3f46ccc0` after pull request 538 ([handoff 6956107567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6956107567)). Every line citation is updated.
- New section 3.2 lists what `main` already does. Requirements 1 and 5 now point there; revision 2's T2 and T14 are dropped because they test behaviour already on `main` and covered by its tests.
- Section 3.1 adds the live `native_monitoring` container key and native monitoring of member Jobs. Requirement 8.4 and requirement 12 include native-monitoring objects without changing their rules. The test fixtures use the `main` `Harness`, which writes native indexes.
- New deferred item D2: a self-killed subject leaves no native index, which is a gap under the native-monitoring amendment. T6 now pins that outcome. Section 9 states it in both docs.
- DA1 recounted: 246 objects and about 1200 s, up from 196 and about 900 s. The accepted risk (overrun of 600 s, decision 6943512296 point 2) is unchanged.
- Requirement 4 now pins the full readiness projection (the revision 2 check's P2 3), allows `not_attempted`, and rejects `complete` for readiness records. This forces one named fixture change in `test_archive_inventory_integration.py`, now in scope under the fixture rule.
- Requirement 6 keeps the new `missing readiness status` gap; `main` records none for a missing readiness record.
- Requirement 7 is narrowed to the two member modes; the start-line and verify part is already on `main`.
- Applies the revision 2 check's other P2s: 8.3b records the probe's own gap as `optional_source` does (P2 1); T8 gives `rows` 40001 (P2 2); the helper line numbers are corrected (P2 4). Its implementer notes are now requirement text: `ordinary_fixed_inventory` gains a scenario argument (9); line 649 takes `consumer-record` (3.3); the capture call is skipped for consumer phases (10).
- Authority adds approval 6943704305, decision 6955981131 and handoff 6956107567.

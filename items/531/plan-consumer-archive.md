# consumer-archive contract-grade plan

Revision: 1

This plan admits the consumer scenarios C01, C03 and C10 to the phase evidence archive. It also adds the consumer record, the consumer start snapshot and the consumer member Jobs to the archive's expected inventory. Today `infra/scripts/phase/archive.py` rejects these scenarios, so every consumer dispatch ends with an archive gap. ADR-020 holds publication on an archive gap, and the archive cannot be taken after a dispatch.

One pull request. Base: `main` at `10cacff23129bf891e73e1fa5d824e30a4ec360e`. Planned against the predecessor, the merged pull request 536 of issue 507.

## 1. Goal and authority

Goal: a C01, C03 or C10 phase whose evidence is all present produces `archive_complete: true`. A phase with missing or mismatched consumer evidence produces a named gap. Existing scenarios keep their archive contract unchanged.

Authority, in creation order:
- Issue 531 body: archive support for the three consumer scenarios, so that their evidence is archived like the producer scenarios'. Predecessor: the pull request of issue 507. This pull request must merge before issue 508's C03 batch dispatch.
- [Decision 6926375790 on issue 507](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6926375790), point 3, adopting Q8 of [handoff 6926216145](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507#issuecomment-6926216145): "a new split entry that admits the three scenarios to the archive and adds the consumer record to its inventory".
- [Decision 6926376436 on issue 499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6926376436): adds this split entry. The batch's decisions apply to it.
- [Decision 6926376950 on issue 531](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6926376950) adopts these: the [standing decision 6895535291](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291); [decision 6894614673](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894614673) points 1 and 2 (Claude plans, Codex implements); and the entry decision.
- ADR-020 (`docs/blueprint.md` lines 624 to 662), line 651: the bounded expected evidence inventory is archived before ephemeral cleanup, and an archive gap holds publication.
- ADR-022 (`docs/blueprint.md` lines 736 to 798): the consumer record format, registration and self-termination.
- `docs/operations/archive-smoke.md` "Phase evidence retention", line 95: "Inventory comes from the selected run/phase and published registration/index/reference contracts. An absent expected publication or capture is an explicit gap; a listing of the available files cannot establish completeness."

Implementing tool: Codex (decision 6894614673 point 2). The control session confirms the tool with Hari before he applies `ready-for-agent` (standing decision point 5).

## 2. Scope

Files allowed to change:
- `infra/scripts/phase/archive.py`
- `infra/scripts/phase/archive_pack.py`
- `infra/scripts/phase/archive_consumer.py` (new, optional). The implementer may put the consumer adapter here, or inside `archive.py`.
- `infra/scripts/phase/tests/test_archive_consumer.py` (new)
- `docs/operations/archive-smoke.md`, the section "Phase evidence retention" only
- `docs/operations/consumer-workflow.md`, the section "Not yet in place" only
- `items/531/state.md` (the item state file, written by the implementing session)

Paths that must not change: every other file. In particular:
- every existing file under `infra/scripts/phase/tests/`, except as the fixture rule in section 6 allows;
- `infra/scripts/phase/phase.py`, `consumer_repeat.py`, `archive_json.py`, `archive_nonledger.py`;
- `src/`, `charts/`, `infra/ephemeral/`, `infra/persistent/`, `.github/workflows/`, `docs/blueprint.md`, `scenarios/`, `results/`.

No protected path is in scope. `.github/CODEOWNERS` assigns no owner to `/docs/operations/`, and `infra/scripts/` has no rule.

## 3. External contracts

### 3.1 Inputs the archive reads (fixed by merged code; this pull request does not change them)

Phase identity, from `phase.json`, written by `phase.py` `prepare` (lines 431 to 436) and `prepare_shared` (line 479):
- `scenario` is `C01`, `C03` or `C10`.
- `phase` and `configuration` are each `C0` or `C1`, and are equal (`phase.py` lines 22 to 27).
- `run_id` is `<lower scenario>-<GITHUB_RUN_ID>-<GITHUB_RUN_ATTEMPT>`, for example `c03-123-1`.
- No `run_kind` and no `purpose` key.
- `images` has `loadgen`, `verifier` and `enrichment`, each `{"digest", "reference"}`, with reference `<login_server>/parchwell/<worker>@<digest>` (`phase.py` line 610).

Phase status, written by `consumer_repeat.py`. Each repeat appends exactly six records, in this order (`docs/operations/consumer-workflow.md` "Order of one repeat"): `consumer-subject`, `consumer-witness`, `consumer-readiness`, `start-line`, `loadgen`, `verify`. The phase then appends one `judge` record.
- Job names, from `phase.py` `job_name` (lines 216 to 228): `<run>-<phase lower>-r<n>-<mode>` per repeat, and `<run>-<phase lower>-judge`.
- A member record carries the keys `job`, `repeat`, `mode`, `render`, `apply`, `wait`, `collect`, `delete`, `confirm`, `deadline` and `outcome`. It may also carry `reason` and `collect_gap` (`consumer_repeat.py` lines 39 to 42, 154 to 155).
- A readiness record (`consumer_repeat.py` lines 80 to 84) has the keys `job`, `repeat`, `mode`, `polls`, `elapsed_seconds`, `last_condition` and `outcome`, plus optional `reason`.
  - `outcome` is `ready`, `not_observed` or `not_attempted`.
  - `last_condition` is `null` or one of `ready`, `list_failed`, `download_failed`, `row_invalid`, `member_count`, `not_assigned`, `overlap`, `coverage` (`consumer_repeat.py` lines 220 to 295).
  - It has no `apply` key and no Kubernetes Job.

Member Job collection: `lifecycle.sh collect-job` writes `<artifacts>/<job>/collection-status.json`, `pods.json` and `<pod>.<kind>.<container>.log`. The member pod has one container, `enrichment-consumer`, and no init containers (`charts/parchwell/templates/_consumer.tpl` lines 46 to 61).

Consumer record, in the ephemeral container `consumer-record`. The prefix is `<run_id>/<phase>/r<n>/consumer/<pod>/` (`ConsumerProcessingOptions.cs` line 22). `<pod>` is the member's pod name, a DNS label, so URI escaping leaves it unchanged.
- `registration.json`: a block blob, at most 65536 bytes (`BlobConsumerRecordStore.cs` lines 19 to 20, 55 to 62). Its fields (`ConsumerRecordCodec.cs` lines 15 to 22): `schema_version` 1, `kind` `consumer_registration`, `run_id`, `scenario_id`, `configuration_id`, `phase`, `repeat_number`, `role`, `instance_id`, `job_name`, `pod_name`, `pod_uid`, `node_name`, `source_commit`, `group_id`, `effective_settings`, `registered_utc`.
- `record/NNNNNN.jsonl`: append blobs, numbered from `000000` with six digits (`BlobConsumerRecordStore.cs` line 29). The worker creates blob k+1 only when blob k holds 40000 rows (line 27).
- `completion.json`: a block blob, at most 65536 bytes (`ConsumerRecordCodec.cs` lines 28 to 32). Its fields: `schema_version` 1, `kind` `consumer_completion`, `run_id`, `scenario_id`, `configuration_id`, `phase`, `repeat_number`, `role`, `instance_id`, `job_name`, `pod_uid`, `source_commit`, `rows`, `record_blobs`, `messages_processed`, `closed_utc`. `record_blobs` is `(rows + 39999) / 40000`.
  - The worker writes it only after a clean close. A self-killed subject, a failed member or a watchdog end writes none (`ConsumerProcessingWorker.cs` lines 93 to 97, 150 to 157).

Verifier outputs for consumer scenarios, in the `verifier` container:
- start-line: `start-line.json`, `broker-inventory.json`, `start-line-completion.json`, and `<run>/<phase>/r<n>/consumer-start.json` (`StartLineHandler.cs` line 90). The start-line receipt does not reference `consumer-start.json`.
- verify (verifier mode `consumer-verify`): `result.json` and `verify-completion.json`. It writes no consumed-metadata capture. `ConsumedMetadataCapture.TryStart` catches the exception that `PhasePaths.ConsumedCaptureId` throws for mode `consumer-verify` and returns null (`ConsumedMetadataCapture.cs` lines 49 to 53; `PhasePaths.cs` lines 26 to 33). See Q2.
- judge: `<run>/<phase>/candidate.json` and `judge-completion.json`.

### 3.2 Outputs this pull request changes

Archive manifest `run` object (`archive.py` `manifest_run` line 1814, `validate_manifest` line 606):
- For C01, C03 and C10: `scenario_id` is the scenario, `configuration_id` is the phase, `run_kind` is `scientific`, `purpose` is `scientific`, and `scientific_admissible` is `true`. These are the existing defaults in `manifest_run` for a phase with no `run_kind`.
- For C01, C03 and C10 only, `images` has exactly the keys `loadgen`, `verifier`, `probe` and `enrichment`. `probe` is `null`. `enrichment` matches `[a-z0-9.-]+/parchwell/enrichment@sha256:[0-9a-f]{64}` or is `null`. For every other scenario, `images` keeps exactly `loadgen`, `verifier` and `probe` (Q3).
- `archive_pack.py` `_context` accepts the same per-scenario key sets.

Archive manifest `objects[*].source.container` may also be `consumer-record`. The destination name is `<prefix>objects/consumer-record/<source name>`, by the existing rule at `archive.py` line 650.

New object `kind` values: `consumer_registration`, `consumer_completion`, `consumer_record`. The verifier object `consumer-start.json` uses the existing kind `verifier_output`.

Phase status projection (`phase-status.json` runner object, `project_phase_status` line 1598):
- `outcome` also accepts `ready` and `not_observed`.
- A record whose `mode` is `consumer-readiness` also carries `polls` (an integer of at least 0, or `null`), `elapsed_seconds` (an integer of at least 0, or `null`) and `last_condition` (a value from section 3.1, or `null`).
- Records of every other mode keep their current projected keys.

New gap boundaries, each with code `monitoring_incomplete`:
- `consumer registration identity`
- `consumer completion contract`
- `consumer record inventory`
- `missing readiness status`

No new gap code. `GAP_CODES` is unchanged.

## 4. Requirements

Section 3.1 defines R, P, J and U. R is the repeat prefix `<run>/<phase>/r<n>/`, P is a member pod name, J is a member Job and U is the pod UID.

1. WHEN `manifest_run` gets scenario C01, C03 or C10 with phase `C0` or `C1`, configuration equal to the phase, and `run_id` equal to `<lower scenario>-<github_run_id>-<github_run_attempt>`, THEN it returns the run object of section 3.2. WHEN the phase is anything else, the configuration differs from the phase, or the run prefix differs, THEN it raises `InvalidArchiveConfig`.
2. WHEN the scenario is C01, C03 or C10 and `images.enrichment` is absent or `null`, THEN the collector records gap `monitoring_incomplete`, boundary `image identity unavailable`. This is the existing gap for `loadgen` and `verifier` (line 1927).
3. WHEN `validate_manifest` or `archive_pack._context` sees a consumer scenario whose `images` key set is not `{loadgen, verifier, probe, enrichment}`, or a non-consumer scenario whose key set is not `{loadgen, verifier, probe}`, THEN it rejects the manifest or context.
4. WHEN a consumer phase status is projected, THEN readiness records project as section 3.2 sets. WHEN `polls` or `elapsed_seconds` is negative, a boolean or a non-integer, or `last_condition` is outside the vocabulary, THEN `project_phase_status` raises `InvalidArchiveConfig`, and the inventory records the existing `inventory construction` gap.
5. The planned records of a consumer phase are, for each repeat 1 to 5: `consumer-subject`, `consumer-witness`, `consumer-readiness`, `start-line`, `loadgen`, `verify`; then `judge` once. WHEN a status record's `(repeat, mode)` is outside that plan or appears twice, or its `job` differs from the name in section 3.1, THEN the archive raises `InvalidArchiveConfig`, as it does today for ordinary phases.
6. Expected units are one per Kubernetes Job: the five Job modes of each repeat and the judge. `consumer-readiness` is not a unit.
   - WHEN a planned Job record is missing, THEN gap `missing job status` with that Job as `expected_unit`.
   - WHEN a planned readiness record is missing, THEN gap `missing readiness status` with expected unit `<run>-<phase lower>-r<n>-consumer-readiness`.
7. WHEN a record of mode `consumer-subject`, `consumer-witness`, `start-line`, `verify` or `judge` exists with `apply` null, THEN gap `required job not started`. This is the existing rule (line 1747), extended to the two member modes. The fixed verifier objects of an unstarted start-line or verify are recorded `not_started`, as today.
8. For each started member Job J, the archive reads its pods with `read_job_pods`, as for other Jobs. For each pod P with UID U it archives:
   1. `R + "consumer/" + P + "/registration.json"`, kind `consumer_registration`, required.
      - WHEN its JSON is not an object, or `kind` is not `consumer_registration`, or any of these differ, THEN gap `consumer registration identity`: `run_id`, `phase`, `scenario_id`, `configuration_id` (from `phase.json`); `repeat_number` (the repeat); `role` (`subject` for mode `consumer-subject`, `witness` for `consumer-witness`); `job_name` (J); `pod_name` and `instance_id` (P); `pod_uid` (U); `source_commit`.
      - The object stays archived either way.
   2. `R + "consumer/" + P + "/completion.json"`, kind `consumer_completion`, optional: archived only when its properties show it exists, as `optional_source` does.
      - WHEN it exists and any identity field of 8.1 except `pod_name` differs, or `rows`, `record_blobs` or `messages_processed` is not an integer of at least 0, or `record_blobs` is not `(rows + 39999) // 40000`, THEN gap `consumer completion contract`, and the record blobs are found as in 8.3b.
   3. Record blobs `R + "consumer/" + P + "/record/" + "%06d" % k + ".jsonl"`, kind `consumer_record`, each required:
      - (a) WHEN a valid completion gives `record_blobs` K, THEN k runs from 0 to K - 1. WHEN the blob with index K exists, THEN gap `consumer record inventory`, naming that blob. A required blob that is absent gets the existing gap `source_missing`.
      - (b) WHEN there is no completion, or it is invalid, THEN the archive checks blob 0, 1, 2 and so on by properties, and archives each one that exists, stopping at the first absent index. Missing completion is not a gap by itself (Q1). WHEN blob 0 is absent, THEN gap `consumer record inventory`, naming blob 0.
      - Probing also stops at the existing `object_limit` gap.
9. WHEN mode is `start-line` in a consumer phase and the Job started, THEN `R + "consumer-start.json"` is a required `verifier_output`. When it did not start, the object is `not_started`. `ordinary_fixed_inventory`'s cross-check lists it for consumer phases.
10. WHEN mode is `verify` in a consumer phase, THEN `result.json` and `verify-completion.json` are archived as today, and no consumed-metadata capture is expected or recorded (Q2). The window inspection of `result.json` (line 957) is unchanged.
11. Loadgen and judge in a consumer phase follow the ordinary path unchanged: producer registration, completion, publication index and publications, optional producer diagnostics, `candidate.json` and `judge-completion.json`.
12. WHEN every object of requirements 8 to 11 is verified and no gap is recorded, THEN `archive_complete` is `true` and `execute` returns 0. WHEN any gap is recorded, THEN `archive_complete` is `false` and `execute` returns 1.
13. A01, B04, B10 and batch scenarios produce byte-identical manifests to `main` for the same inputs.

## 5. Invariants and safety rules

- The archive never lists a container. It reads, by name, the objects that a contract names (archive-smoke.md line 95). Probing in 8.3b reads properties of fixed names, one at a time, and is bounded by the existing object limit.
- No policy value changes: `ArchiveMaxBytes`, `ArchiveMaxObjectBytes`, `ArchiveMaxObjects`, `ArchiveDeadlineSeconds`, `ArchiveTransferPasses`, `MetadataChunkMaxBytes`, `MetadataRowMaxBytes` and `_common.POLICY` stay as on `main`.
- No identity, role or network change. The workflow principal already reads `consumer-record` (`infra/ephemeral/identity.tf` lines 43 to 49).
- No result, verdict, expected outcome or scientific classification changes. An archive gap stays separate from the science (ADR-020 line 651).
- Message bodies stay excluded. The consumer record holds keys, offsets and identities, not message values (ADR-022 "Record", line 754).

## 6. Test map

All tests are in `infra/scripts/phase/tests/test_archive_consumer.py`. They run offline with the `BlobCLI` and `Harness` pattern of `test_archive_inventory_integration.py` (lines 64 to 120). The implementer may copy those helpers, or import them without changing that file.

Fixture: run `c03-123-1`, phase `C0`, `GITHUB_RUN_ID` `123`, attempt `1`, source commit `a` times 40. Images `loadgen`, `verifier` and `enrichment` have digest `sha256:` plus `b` times 64. Pod names are `<job>-abcde`, and UIDs are distinct valid GUID strings.

| Test | Requirement | Input | Expected |
|---|---|---|---|
| T1 `test_manifest_run_admits_consumer_phases` | 1 | each of C01, C03, C10 with phase C0 and C1, run `<lower>-123-1` | `scenario_id` is the scenario; `configuration_id`, `run_kind` `scientific` and `scientific_admissible` `true`; `images` keys `{loadgen, verifier, probe, enrichment}`; `probe` `null` |
| T2 `test_manifest_run_rejects_consumer_mismatches` | 1 | C03 with phase `probe`; with phase `C3`; with phase C0 and configuration C1; with run `b04-123-1` | each raises `InvalidArchiveConfig` |
| T3 `test_image_key_sets_by_scenario` | 2, 3 | `validate_manifest` and `archive_pack._context` with C03 and 4 keys; C03 and 3 keys; B04 and 4 keys; B04 and 3 keys; C03 with `enrichment` reference `x.azurecr.io/parchwell/loadgen@sha256:` plus `b` times 64 | accept, reject, reject, accept, reject. A C03 `execute` with `enrichment` null records gap `image identity unavailable` |
| T4 `test_readiness_projection` | 4 | readiness records `{polls: 3, elapsed_seconds: 12, last_condition: "ready", outcome: "ready"}` and `{polls: 60, elapsed_seconds: 300, last_condition: "list_failed", outcome: "not_observed"}` | projected with exactly those three fields plus the common fields; `polls: -1`, `polls: true`, `elapsed_seconds: 1.5` and `last_condition: "other"` each raise `InvalidArchiveConfig` |
| T5 `test_complete_consumer_phase` | 5, 6, 8, 9, 10, 11, 12 | five full repeats; each member has registration, completion with `rows` 40001 and `record_blobs` 2, blobs `000000` and `000001`, no blob `000002`; judge complete | `archive_complete` `true`; return 0; `expected_units` has 26 entries; `captures` empty; objects include `objects/consumer-record/c03-123-1/C0/r1/consumer/<pod>/record/000001.jsonl` (kind `consumer_record`) and `objects/verifier/c03-123-1/C0/r1/consumer-start.json` |
| T6 `test_killed_subject_without_completion` | 8.3b, 12 | as T5, but each subject has no completion and blobs `000000` only | `archive_complete` `true`; no `consumer_completion` object for any subject; one `consumer_record` object per subject |
| T7 `test_extra_record_blob_after_completion_count` | 8.3a | repeat 1 witness completion `record_blobs` 1, `rows` 5, blob `000001` present | gap `monitoring_incomplete`, `consumer record inventory`, object name `c03-123-1/C0/r1/consumer/<witness pod>/record/000001.jsonl`; `archive_complete` `false` |
| T8 `test_missing_record_blob_named_by_completion` | 8.3a | completion `record_blobs` 2, blob `000001` absent | gap `source_missing` for `.../record/000001.jsonl`; that object's state is `missing` |
| T9 `test_registration_identity_mismatch` | 8.1 | repeat 2 subject registration `pod_uid` differs from U; then `role` `witness` | each records gap `consumer registration identity`; the registration object is still `verified` |
| T10 `test_invalid_completion_falls_back_to_probe` | 8.2, 8.3b | completion `record_blobs` 3 with `rows` 5; blobs `000000` present, `000001` absent | gap `consumer completion contract`; blob `000000` archived; no `source_missing` gap for `000001` or `000002` |
| T11 `test_missing_first_record_blob` | 8.3b | no completion, no blob `000000` | gap `consumer record inventory` naming `.../record/000000.jsonl` |
| T12 `test_missing_readiness_record` | 6 | repeat 1 status without its readiness record | gap `missing readiness status`, expected unit `c03-123-1-c0-r1-consumer-readiness` |
| T13 `test_skipped_repeat_after_deadline` | 7 | repeat 5's six records `not_attempted` with reason `orchestration_deadline`, as `skip_repeat` writes them | gaps `required job not started` for units `c03-123-1-c0-r5-consumer-subject`, `-consumer-witness`, `-start-line` and `-verify`; the fixed verifier objects of r5 are `not_started` |
| T14 `test_unplanned_consumer_record_rejected` | 5 | a status record with mode `consumer-subject` and repeat 6; a duplicate `consumer-readiness` for repeat 1 | each `InvalidArchiveConfig`, recorded as the existing `inventory construction` gap |
| T15 `test_non_consumer_manifests_unchanged` | 13 | the existing suite under `infra/scripts/phase/tests/` | passes with no changed file |

Boundaries: T5 uses 40001 rows to put `record_blobs` at the 40000 boundary plus one. T7 and T8 cover one above and one below the count. T11 covers zero blobs. T4 covers `polls` 0 as accepted (add `{polls: 0, elapsed_seconds: 0, last_condition: null, outcome: "not_attempted"}`).

Existing test fixture rule. When an existing test's fixture is rejected by a new check in this plan, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it. The cold review and Codex review check each listed change. Modelled on [issue 404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2. Expected count of such changes: none, because no existing fixture uses a consumer scenario (`grep` of `C01|C03|C10` in `test_archive_*.py` on the base finds none).

## 7. Deployment assumptions

- DA1. A consumer phase's archive fits the existing bounds: 1024 objects, 256 MiB per object, 16 GiB in total, and a 600 s deadline (`archive.py` lines 5 to 8).
  - Objects, estimate of 2026-11-17 computed by script from the inventory in section 4: 196 per phase without record blobs, leaving 826 objects for record blobs. Basis: 38 objects per repeat without record blobs, assuming one container per pod.
  - Per object: a record blob holds at most 40000 rows. It exceeds 256 MiB only if rows average more than 6710 bytes. That is an inference from the codec's fields, not a measurement.
  - Deadline: no consumer phase has been archived. No archive of any phase has a recorded duration in this plan's sources. This is not measured.
  - How evidence will be taken: the archive receipt and Actions step duration of issue 508's smoke run. That run uses fresh connections, after the load generator ends.
  - An exceeded bound is a gap, not a silent loss: `object_limit`, `source_too_large`, `byte_limit` or `deadline` (existing behaviour). No offline test covers live size or time. Listed under Q4.

## 8. Acceptance commands

```sh
python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_archive_consumer.py' -v
python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'
npm run check:docs
```

- The first must show T1 to T14 passing.
- The second is the CI step of `dotnet-verification.yml` line 207. It reaches the phase suite through `infra/scripts/tests/test_phase_bridge.py` and must pass with no existing test file changed (T15).
- The third must report no new warning in the two changed docs.

Final-head CI: the `dotnet-verification.yml` run on the pull request.

## 9. Documentation changes

- `docs/operations/archive-smoke.md` "Phase evidence retention": add one paragraph that states the consumer inventory of requirements 6 to 10. It says where the record blob count comes from (the completion receipt, or sequential probing when there is none), that consumer verification has no consumed-metadata capture, and that consumer phases carry the `enrichment` image.
- `docs/operations/consumer-workflow.md` "Not yet in place": replace the archive sentence with one that says the archive admits C01, C03 and C10, and links the archive-smoke section.

## 10. Plan-check questions

- (a) Steps whose effort scales with a number unknown at planning time:
  - Record-blob probing in 8.3b scales with the record blob count. It is capped by the existing `ArchiveMaxObjects`, whose exhaustion records gap `object_limit` and continues.
  - Tests use fixed fixture sizes.
  - No implementation step has an uncapped runtime count.
- (b) Where this plan is stricter than its parent: issue 499's split row and Q8 name only "admits the three scenarios ... adds the consumer record". This plan also binds registration identity (8.1), cross-checks the completion count (8.2, 8.3a), adds `consumer-start.json` and the enrichment image, and requires the readiness record. Each follows the existing pattern for producer registration (line 1180), publication index (line 1327) and image identity (line 1927).
- (c) Tests that depend on code or data neither on `main` nor in this pull request: none. Every input shape in section 3.1 is read from merged code at the base. Live sizes and time are DA1.

## 11. Sources

Tool: Microsoft Learn documentation tool, read 2026-11-17.
- [Azure Blob Storage REST API](https://learn.microsoft.com/rest/api/storageservices/blob-service-rest-api), operations table: Get Blob and Get Blob Properties apply to "Block, append, and page blobs". The archive's existing `show` and ETag-conditional `download` therefore apply to the append record blobs.

No Kafka, librdkafka or Confluent.Kafka behaviour is relied on.

Repository sources: all at `10cacff23129bf891e73e1fa5d824e30a4ec360e`, cited by file and line above.

## 12. Deferred work

- Confirm DA1 live.
  - Evidence: none yet (section 7).
  - On the verdict path: yes, because an archive gap holds C03 publication.
  - Why deferral is permitted: no consumer phase can run before this pull request and issue 508's scenario merge.
  - Responsible: issue 508's smoke run, under its plan.
  - Completion evidence: an `archive-receipt.json` with `archive_complete: true` from that run.
  - Gate it blocks: issue 508's C03 batch dispatch.
  - Exhaustion action: a gap in that receipt is brought to Hari before the batch dispatch, not fixed by raising a policy value.

## 13. Open questions for Hari

Each has a recommended answer. Point 2 of the standing decision adopts recommended answers in advance, and the control session records the adoption.

- Q1. Record blob inventory without a completion receipt. A self-killed C10 or C01 subject never writes `completion.json`, by design (ADR-022 "Self-termination"). archive-smoke.md line 95 says that completeness comes from published contracts, not listings.
  - Recommended: probe blobs 0, 1, 2 and so on by name until the first absent index (requirement 8.3b). The worker contract creates blob k+1 only after blob k holds 40000 rows, and every member Job has ended before the archive step runs. So the first absent index ends the record. Missing completion is not an archive gap.
  - Alternative A: treat a missing completion as a gap. Every C10 and C01 repeat with a kill would then hold publication.
  - Alternative B: list the container prefix. That contradicts line 95.
- Q2. Consumed-metadata capture for consumer verification. ADR-020 line 651 and archive-smoke.md line 95 retain "distinct consumed-metadata captures". The verifier in mode `consumer-verify` makes none (section 3.1), although its ordinary verification consumer drains the run window.
  - Recommended: the archive expects no capture for consumer verify (requirement 10). archive-smoke.md states the limit. Publication is not held for it.
  - Alternative: a new split entry before issue 508's batch dispatch that enables the capture in `consumer-verify`. It would touch `src/Parchwell.Verifier/` (protected), with the mutation acceptance that path carries.
  - Scope: the alternative is beyond the approved split, so it is Hari's (standing decision point 4).
- Q3. Consumer image identity. The manifest's `images` names three workers and drops the enrichment image, which is the consumer under test.
  - Recommended: add `enrichment` to `images` for C01, C03 and C10 only, and to `archive_pack._context` for the same scenarios (section 3.2). Existing scenarios keep three keys, so existing manifests stay valid.
  - Alternative: add `enrichment`, `null` where unused, for every scenario. That changes every manifest and the pack contract for existing runs.
- Q4. Accept DA1 by name: a consumer phase's archive fits the existing object, size, byte and 600 s bounds, with live evidence deferred to issue 508's smoke run (section 12). An exceeded bound is a recorded gap.

## 14. Size estimate

Estimate, 2026-11-17, from the inventory above, not measured: about 250 changed lines in `archive.py` (or 120 there plus 150 in a new `archive_consumer.py`), 10 in `archive_pack.py`, 450 in the new test file, and 15 in the two docs. That is five to six files. Size is reported, not enforced.

## 15. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope of this plan. That includes changing any file outside section 2, any policy value in section 5, or any gap code.

# archive-concurrency contract-grade plan

Revision: 3

This plan raises the phase evidence archive deadline from 600 seconds to 1800 seconds, as Hari decided on 2026-11-18. It changes the constant, the copy of the value in the archive policy block that every archive manifest records, the archive readiness smoke's own deadline, the operations document and the tests that pin 600. Nothing else about the archive changes: every object is still verified as today.

One pull request. The plan keeps its short name, `archive-concurrency`, because the `plan-pr` skill step 2 keeps a plan's name across revisions; the concurrency design of revision 2 is withdrawn. Base: `main` at `08c708aae6a8cc27c87110013540089bb5553a43`. Line numbers are at that base.

## 1. Goal and authority

Goal: an archive command, in a scenario phase or in the archive readiness smoke, has an 1800-second monotonic deadline instead of 600 seconds, and every manifest, document and test that states the deadline states 1800.

Authority, in creation order:
- Issue 548 body. It records the C03 smoke run [44248514485](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/44248514485), whose "Archive phase evidence" step hit `ArchiveDeadlineSeconds` (600 s) in both phases ([smoke report 6973117302](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/508#issuecomment-6973117302), check 8). It records that a `deadline` gap holds publication and that the issue gates the #508 batch dispatch. Its route (concurrent transfer, no larger deadline) is replaced by decision 6976180742 below.
- [Decision 6973159782](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548#issuecomment-6973159782): Claude Code implements this issue, on the implementation model in CLAUDE.md "Model bindings"; Codex reviews; the plan needs Hari's approval and `ready-for-agent`.
- [Decision 6973214094](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548#issuecomment-6973214094) and [decision 6973250625](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548#issuecomment-6973250625): the server-side copy evaluation and its verification rules. Decision 6976180742 point 2 says the server-side copy requirement lapses "because no copy mechanism changes". Point 3 of decision 6973214094 (the workflow identity does the copy; workloads get no archive access) describes today's design, which this plan does not touch.
- [Decision 6976180742](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548#issuecomment-6976180742), Hari's words "raise to 1800":
  1. `ArchiveDeadlineSeconds` (`infra/scripts/phase/archive.py` line 8) changes from 600 to 1800. It is a policy value. Its basis is an estimate, not a measurement: 141 of 202 objects verified in 600 s, about 4.3 s per object, about 230 objects per consumer phase, about 990 s. The next C03 smoke measures it.
  2. The revision 2 concurrency design is withdrawn, with its open questions and deployment assumptions D1 to D4. Every archived object is still verified, as today.
  3. The change applies to every scenario that archives. Every place that states the 600 s value changes with it, including `docs/operations/archive-smoke.md` line 52 and any test that pins it.
  4. Cost: each phase may keep the ephemeral environment up longer before teardown, by up to 20 minutes more than today.

The approved observability specification ([items/494/plan-observability-evidence.md at f96eab914d00c06484431ff67a1808fb331c7e13](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/f96eab914d00c06484431ff67a1808fb331c7e13/items/494/plan-observability-evidence.md)) states the old value at line 76 ("Archive command | 600 s aggregate monotonic deadline, including enumeration/retries/hash/readback | Record gap and continue cleanup") and line 185 (`ArchiveDeadlineSeconds=600`). It is a document pinned at a commit and is not edited. Decision 6976180742 amends that value; section 9 makes the operations document say so.

### 1.1 Where 600 is stated on `main`

Found by `grep -rn` over the repository, excluding `.git` and `node_modules`, for `ArchiveDeadlineSeconds`, `deadline_seconds`, `DEADLINE`, `600-second`, `600 s` and the word `600`; each hit was read in context. The archive deadline is stated in exactly these places:

| Place | Text on `main` | Role |
|---|---|---|
| `infra/scripts/phase/archive.py` line 8 | `ArchiveDeadlineSeconds = 600` | Phase archive deadline. Read by `phase.py` line 280 (`deadline=started + module.ArchiveDeadlineSeconds`) and by `archive.py` line 2156 (default when `execute` gets no deadline). |
| `infra/scripts/archive/smoke.py` line 22 | `DEADLINE = 600` | Archive readiness smoke deadline, read at line 662 (`deadline = time.monotonic() + DEADLINE`). |
| `infra/scripts/archive/smoke.py` line 25 | `'deadline_seconds': 600` inside `POLICY` | The policy block. The phase archive imports this module as `_common` (`archive.py` lines 58-61), writes `dict(_common.POLICY)` into every manifest (line 2171) and rejects any manifest whose `policy` is not equal to `_common.POLICY` (line 642). The smoke writes it into its own manifest (line 598). |
| `docs/operations/archive-smoke.md` line 52 | "An archive command has a 600-second monotonic deadline, including SDK requests/backoff and hashing." | Operations document. |
| `infra/scripts/phase/tests/test_archive_config.py` line 117 | `'deadline_seconds': 600` in a manifest dict | Test fixture. |
| `infra/scripts/phase/tests/test_archive_config.py` lines 243-246, 251 | Literal manifest bytes with `"deadline_seconds":600`, its length 911 and SHA-256 `6c5fcc28...`, and the marker that binds them (length 500, SHA-256 `f1500fca...`) | Golden byte test. |
| `infra/scripts/phase/tests/test_archive_pack.py` lines 24, 25, 64 | The same manifest bytes, the same marker and the manifest SHA-256 | Golden byte test. |

Every other `600` hit is a different bound and must not change: command wrapper timeouts (`.github/workflows/dotnet-verification.yml` lines 207-281, `docs/operations/lifecycle.md` lines 67 and 134), token lifetimes in tests (`scripts/tests/test_azure_cli_session.py`, `infra/scripts/tests/test_archive_smoke.py` lines 107 and 129), the F06 audit (`docs/operations/single-action-contract.md` lines 238 and 254 and the C# code and tests that pin `audit_seconds`), mutation profiles (`tests/Parchwell.Verifier.Tests/Mutation/`, `docs/operations/verifier-mutation.md`), file modes `0o600`, and historical evidence under `docs/operations/evidence/` and `results/`.

The workflows give the archive steps no step timeout. `.github/workflows/scenario.yml` lines 237-242, 463-468, 725-730 and 955-960 run `phase.sh archive` without `timeout-minutes`, inside jobs with `timeout-minutes: 360` (lines 62, 294, 520, 788). `.github/workflows/archive-smoke.yml` line 113 runs `smoke.py run` without a step timeout inside a job with `timeout-minutes: 360` (line 15). No workflow changes.

### 1.2 Measured job time

Read on 2026-11-18 from the jobs API of run 44248514485: the C0 job (`probe`) ran 06:04:01 to 07:03:44 UTC, 3583 s, and the C1 job (`full-C3`) 07:22:27 to 08:15:14 UTC, 3167 s. Each "Archive phase evidence" step ran 600 s (06:51:07 to 07:01:07, and 08:02:07 to 08:12:07). Adding at most 1200 s to each archive step gives at most 4783 s and 4367 s, under the 21600-second (360-minute) job timeout. Differences computed by script. This is arithmetic on one run, not a measurement of a longer archive.

## 2. Scope

Files allowed to change:
- `infra/scripts/phase/archive.py`: line 8 only.
- `infra/scripts/archive/smoke.py`: lines 22 and 25 only.
- `docs/operations/archive-smoke.md`: lines 52 and 101 only, as section 9 pins.
- `infra/scripts/phase/tests/test_archive_config.py`: the fixture lines of section 1.1 and the new tests T1 and T2.
- `infra/scripts/phase/tests/test_archive_pack.py`: lines 24, 25 and 64.
- `infra/scripts/phase/tests/test_phase.py`: the new test T3 only.
- `items/548/state.md`: created by the implementing session under docs/agents/factory.md "Item state file".

Paths that must not change: `.github/workflows/**` and `docs/operations/workflows/**`; `infra/persistent/**`, `infra/ephemeral/**`, `infra/cluster/**`; `charts/**`; `src/**`; `results/**`; `docs/blueprint.md`; `items/494/**`; every `600` listed as a different bound in section 1.1; every other archive limit in `POLICY` (`max_bytes`, `max_object_bytes`, `max_objects`, `transfer_passes`, `metadata_chunk_bytes`, `metadata_row_bytes`) and the other constants in `archive.py` lines 5-11.

## 3. External contracts

- **Manifest policy block.** Every archive manifest written after this change, by a phase archive or by the readiness smoke, carries exactly `"policy":{"max_bytes":17179869184,"max_object_bytes":268435456,"max_objects":1024,"deadline_seconds":1800,"transfer_passes":1,"metadata_chunk_bytes":134217728,"metadata_row_bytes":1048576}` in that key order. Key set, key order and the other six values are unchanged.
- **Consequence for stored manifests.** `validate_manifest` (`archive.py` line 642) requires `policy == _common.POLICY`. After the change, a manifest archived before it (with `deadline_seconds` 600) fails `validate_manifest` and therefore `verify_controls` (line 1604). On `main`, `verify_controls` is called only through `archive_pack.verify_pack` (`archive_pack.py` line 118), and no production code calls `verify_pack`; it is reached only from `infra/scripts/phase/tests/test_archive_pack.py`. So no current workflow reads an older manifest. Decision 6976180742 point 3 covers this value in the policy block; the plan adds no compatibility path. Any later work that packs an archive written before this change must handle the older policy; section 11 records it.
- **Deadline value.** `archive.ArchiveDeadlineSeconds`, `smoke.DEADLINE` and `smoke.POLICY['deadline_seconds']` are each the integer `1800`.
- Gap codes, boundaries, receipt fields, artifact names and the deadline's monotonic clock are unchanged. Running out of time still records `deadline` gaps, which hold publication (`docs/blueprint.md` line 651).

## 4. Requirements

- R1. WHEN `infra/scripts/phase/archive.py` is loaded THEN `ArchiveDeadlineSeconds` is the `int` `1800`.
- R2. WHEN `infra/scripts/archive/smoke.py` is loaded THEN `DEADLINE` is the `int` `1800` and `POLICY['deadline_seconds']` is the `int` `1800`, and `POLICY` keeps the key order and other values of section 3.
- R3. WHEN `Phase.archive` runs with `time.monotonic()` returning `1000.0` at its start THEN it calls the archive module's `execute` with `deadline=2800.0`.
- R4. WHEN `validate_manifest` gets a manifest whose `policy` equals section 3's block THEN it returns the identity. WHEN `deadline_seconds` in that block is `600`, `1799` or `1801` THEN it raises `InvalidArchiveConfig`.
- R5. WHEN the golden manifest of section 6 is encoded THEN its length is 912 bytes and its SHA-256 is `37041710d72f19bc31914711eaf10d78dcb8643e6cda98032ea375f1159471de`, and the golden marker that binds it is 500 bytes with SHA-256 `660f6115d897491a6116485e329c7a107c3f7238debbbc3bb5a8ff645bbc7461`.
- R6. WHEN the archive runs out of time THEN behaviour is unchanged: the remaining objects become `deadline` gaps and the receipt is incomplete. No test changes for this path except the fixture changes of section 6.

## 5. Invariants and safety rules

- Every archived object is still verified as today: source etag bound, destination SHA-256 equal to the source, completion confirmed (decision 6973250625 point 2, and decision 6976180742 point 2).
- The archive step still runs before "Destroy ephemeral infrastructure" in each phase job (`scenario.yml` lines 237 and 261 for C0, 725 and 749 for C1). Teardown still runs whether the archive passes or fails (AGENTS.md hard safety rule 6).
- No workflow, Terraform, chart or identity changes. AGENTS.md hard safety rules 1, 2, 3, 8 and 9 are untouched.
- No verdict, expected outcome or file under `results/` changes.

## 6. Test map

The golden manifest is the existing literal in `test_archive_config.py` line 243 and `test_archive_pack.py` line 24, with `"deadline_seconds":600` replaced by `"deadline_seconds":1800` and nothing else. The golden marker is the existing literal in `test_archive_config.py` line 244 and `test_archive_pack.py` line 25, with `"bytes":911` replaced by `"bytes":912` and `6c5fcc28afea6d49840fc75d06c28551a4a45afe119d822eb1b43b5872d0cbba` replaced by `37041710d72f19bc31914711eaf10d78dcb8643e6cda98032ea375f1159471de`. These values were computed on 2026-11-18 by applying exactly those replacements to the literals on `main` and hashing the result.

| Test | Requirement | Inputs | Literal expected values |
|---|---|---|---|
| T1 `ArchiveConfigTests.test_archive_deadline_is_1800_seconds` (new, `test_archive_config.py`) | R1, R2 | The loaded `archive` module and `archive._common` | `(1800, 1800, 1800) == (archive.ArchiveDeadlineSeconds, archive._common.DEADLINE, archive._common.POLICY['deadline_seconds'])`; each value has `type` `int`; `list(archive._common.POLICY) == ['max_bytes', 'max_object_bytes', 'max_objects', 'deadline_seconds', 'transfer_passes', 'metadata_chunk_bytes', 'metadata_row_bytes']` |
| T2 `CaptureInventoryTests.test_manifest_policy_deadline_boundary` (new, `test_archive_config.py`, the class at line 88 that holds the test it reuses) | R4 | The manifest dict of `test_manifest_zero_data_and_object_completion_rejected` (line 110), after the fixture change below, with `policy['deadline_seconds']` set to each of `1800`, `600`, `1799`, `1801` | `1800`: returns the identity of line 108. `600`, `1799`, `1801`: each raises `archive.InvalidArchiveConfig` |
| T3 `PhaseTests.test_archive_deadline_is_start_plus_1800` (new, `test_phase.py`) | R3 | `Phase.archive` with the phase module's `time.monotonic` returning `1000.0` and the loaded archive module's `execute` replaced by a recorder | `execute` called once, with keyword `deadline` equal to `2800.0` |
| `test_manifest_zero_data_and_object_completion_rejected` (`test_archive_config.py` line 107, fixture change) | R4 | Line 117: `'deadline_seconds': 600` becomes `'deadline_seconds': 1800` | Assertions unchanged |
| `LiteralControlTests.test_literal_zero_data_bindings_and_mutations` (`test_archive_config.py` line 240, fixture change) | R5 | Line 243: the golden manifest. Line 244: the golden marker | Line 245: `(912, '37041710d72f19bc31914711eaf10d78dcb8643e6cda98032ea375f1159471de')`. Line 246: `(500, '660f6115d897491a6116485e329c7a107c3f7238debbbc3bb5a8ff645bbc7461')`. Line 251: `'bytes': 500, 'sha256': '660f6115d897491a6116485e329c7a107c3f7238debbbc3bb5a8ff645bbc7461'`. Every other assertion unchanged |
| `test_archive_pack.py` (fixture change) | R5 | Line 24: `MANIFEST` becomes the golden manifest. Line 25: `MARKER` becomes the golden marker | Line 64: `'37041710d72f19bc31914711eaf10d78dcb8643e6cda98032ea375f1159471de'`. Every other assertion unchanged |
| Existing suites that read `archive._common.POLICY` (`test_archive_transfer.py` line 94, `test_archive_boundaries.py` line 29) and every other existing archive test | R6 | Unchanged | Pass unchanged |

Standing fixture rule (modelled on [issue 404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2). The changed policy value makes `validate_manifest` reject fixtures that carry 600. When an existing test's fixture is rejected by that changed check, the implementer does exactly one of:
- (a) changes the fixture so it passes, keeping every assertion and the boundary the test exercises;
- (b) where the input's bytes are the test's purpose, as in the two golden tests above, keeps the input except the one changed value and changes the expected values to exactly what the changed input produces.

The rule never permits removing a case, loosening an assertion or changing a check. The pull request's implementation notes list each fixture change with the check that forces it. The cold review and Codex review check each listed change. The rows above are the changes this plan expects; any other fixture the suite rejects is listed the same way.

## 7. Deployment assumptions

- D1. A C03 consumer phase archive completes inside 1800 s. Evidence: an estimate in decision 6976180742 point 1 (about 990 s), from one run's rate taken on the live archive under its normal phase load, not a measurement of a full phase. No offline test can cover it. Hari accepted it by name in [decision 6976180742](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548#issuecomment-6976180742) point 1 ("That is an estimate ... The next C03 smoke measures it"). If the next C03 smoke still records `deadline` gaps, that is a finding for Hari, not a reason to change the value in this pull request.
- D2. The Azure CLI session wrapper keeps the workflow identity usable for an archive step of up to 1800 s. Evidence: the wrapper re-checks freshness before every command and refreshes the federated assertion when stale (`scripts/azure-cli-session/az` lines 256-266, 315-363 and 430-437). The existing offline test `test_18_synthetic_long_phase_renews_distinct_assertions` (`scripts/tests/test_azure_cli_session.py` line 1072) covers renewal on a synthetic clock. No live evidence exists of one archive step longer than 600 s: the only live archive steps stopped at 600 s. Covered offline, so not a blocking assumption.
- D3. The longer step fits the job timeout. Evidence: section 1.2, from the jobs API of one completed run, not under a longer archive.

## 8. Acceptance commands

- `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` passes. It includes the phase suite through `infra/scripts/tests/test_phase_bridge.py` line 9. Evidence: the log of the required `.NET verification` check at the head commit (`dotnet-verification.yml` line 207).
- `python3 -m unittest -v infra/scripts/phase/tests/test_archive_config.py infra/scripts/phase/tests/test_archive_pack.py` and `python3 -m unittest -v infra/scripts/phase/tests/test_phase.py` list T1, T2, T3 and the changed tests as `ok`. Evidence: the command output in the pull request's validation notes.
- `git grep -n -E "ArchiveDeadlineSeconds = |^DEADLINE = |'deadline_seconds': |\"deadline_seconds\":" -- infra docs ':!infra/scripts/phase/tests'` shows only `1800`. Test files are excluded because T2 uses 600, 1799 and 1801 as rejected inputs. Evidence: the command output in the validation notes.
- An ASCII scan of the changed files finds no non-ASCII byte.

After merge (section 11): the next C03 smoke run's two archive receipts and the duration of each "Archive phase evidence" step.

## 9. Documentation changes (`docs/operations/archive-smoke.md`)

Line 52, first sentence. Replace:

> An archive command has a 600-second monotonic deadline, including SDK requests/backoff and hashing.

with:

> An archive command has an 1800-second monotonic deadline, including SDK requests/backoff and hashing. Hari raised it from 600 seconds on 2026-11-18 ([decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548#issuecomment-6976180742)), for the readiness smoke and every phase archive.

The rest of line 52 is unchanged. This line is where the value is stated.

Line 101, after its first sentence ("The fixed limits and exact wire schemas are owned by the [approved specification](...).") insert:

> The archive command deadline is the one exception: it is the value stated in "Controls and public evidence" above, not the specification's 600 seconds.

The rest of line 101 is unchanged.

## 10. Plan-check questions

(a) Steps whose effort scales with a number unknown at planning time: none in the implementation; the files and values are fixed. The archive itself handles a runtime object count, bounded by the 1800 s deadline; its exhaustion action is the existing one, `deadline` gaps that hold publication.

(b) Where the plan is stricter than its parent: there is no item plan. Against the approved specification, the plan is less strict on one value (1800 s instead of 600 s), by decision 6976180742. It adds tests T1 to T3, which pin the value in all three places and its use by `Phase.archive`; the specification already required the exact policy match that T2 exercises.

(c) Tests or checks that depend on code, data or behaviour neither on `main` nor in the pull request: none offline. The new golden values were computed from the literals on `main` (section 6). D1 depends on live archive timing and is accepted by decision 6976180742 point 1.

## 11. Deferred work

- Measure a full C03 phase archive. Evidence: D1 is an estimate. On the verdict path: yes, it gates the #508 batch dispatch (issue body). Deferral is permitted because it needs a paid live run after merge, under decision 6976180742 point 1. Responsible: the control session for issue 347, which owns the next C03 smoke dispatch. Stage: after this pull request merges. Bound and exhaustion action: one C03 smoke run; any `deadline` gap is a finding for Hari. Completion evidence: both archive receipts of that run with `archive_complete` true and the step durations. Gate it blocks: the #508 batch dispatch.
- Older manifests. Evidence: section 3. On the verdict path: not today, because no production code reads a stored manifest. Responsible: any later plan that adds a reader of archived manifests. Completion evidence: that plan states how a manifest with `deadline_seconds` 600 is treated. Gate it blocks: none now.

## 12. Sources

- Repository files on `main` at `08c708aae6a8cc27c87110013540089bb5553a43`, at the lines cited.
- [items/494/plan-observability-evidence.md at f96eab914d00c06484431ff67a1808fb331c7e13](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/f96eab914d00c06484431ff67a1808fb331c7e13/items/494/plan-observability-evidence.md), lines 76 and 185.
- GitHub jobs API for run 44248514485, read 2026-11-18 (section 1.2).
- The decisions in section 1.

The plan relies on no Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour, and on no new Azure behaviour, so no documentation tool was needed.

## 13. Open questions for Hari

None.

## 14. Size estimate

Estimate, 2026-11-18, basis: the line counts of section 1.1 plus three new tests of about 10 to 20 lines each: about 6 files changed (7 with `items/548/state.md`), about 60 lines added and 10 removed. Not measured; not enforced.

## 15. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope, including any file outside section 2, any value other than the deadline, or a workflow.

## 16. Changes since revision 2

Revision 2 (`9dc75f31d2d87efb08d93ed6f56bd8b18f4262e1`) is replaced in full by decision 6976180742:
- The SDK transport, the concurrent consumer workers, the resident spool, the server-side copy evaluation, deployment assumptions D1 to D4 and open questions 1 to 4 are removed (decision point 2).
- The two pull requests become one, which raises the deadline to 1800 s everywhere it is stated (decision points 1 and 3).
- The revision 2 check findings and `apply in implementation` texts applied to the withdrawn design and lapse with it.

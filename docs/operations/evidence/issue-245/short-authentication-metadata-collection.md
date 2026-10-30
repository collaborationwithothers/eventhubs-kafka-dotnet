# Issue #245 implementation receipt

Issue #245 adds a default-off V2 route that selects only F06 C6 and C9 for bounded authentication metadata collection. This receipt records the approved offline implementation, its acceptance evidence and its review handoff boundary. No live collection is authorized.

## Execution card

- **Deliverable:** Implement approved Revision 3 of the short authentication metadata collection wiring plan: default-off dispatch input, fixed V2 smoke selection, frozen selector validation, selected-control orchestration and diagnostics, cleanup guards, offline tests and linked operational documentation. The approved plan defines exact files, inputs, outputs, exclusions and acceptance.
- **Authorization:** Hari's [approval, comment 6526348889](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6526348889) approves the current [Revision 3 plan, comment 6525824679](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6525824679) for offline implementation by Codex. The approved revision is updated 2026-10-30T15:11:34Z. Its latest checks [6526196967](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6526196967) and [6526259257](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6526259257) report no blocking findings. This authorizes no dispatch or live collection. The selected-control collection requires a separate future execution decision.
- **Base:** `c4a1724f9165397dd04f4e8df22acd9c8498ebd8` on `codex/245-short-authentication-metadata-collection`.
- **Mandatory gates:** Preserve the protected workflow and committed workflow copy byte-identically; pass every offline acceptance command; run documentation checks and record dispositions for warnings in changed files; obtain applicable cold-review coverage before publication; pass required PR CI and hosted final-head Codex review. Hari merges protected workflow changes. No live, Azure, dispatch, credentials or token-content operations.
- **Exact verification commands:** `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'`; `bash infra/scripts/lifecycle/check-offline.sh`; `cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt`; `npm run check:docs`; `git diff --check`.
- **Earliest authorized publication checkpoint:** After implementation and all offline acceptance pass, changed-file documentation warnings have dispositions, and the required cold-review coverage is complete. The coordinator owns publication and final handoff.
- **Implementation disposition caps:** At most ten changed-file documentation warnings and ten distinct failing-test root causes. At either cap, stop expanding work, record the remaining gap and push WIP / hand off to the next session as the approved plan directs. Runtime collection is not authorized.
- **Review budget:** 0/2 comprehensive and 0/2 focused cold reviews used before review. Do not initiate cold review in this implementation session. Under AGENTS.md rule 8, if another required review would exceed either cumulative budget, or focus is unsafe, stop, preserve unpublished work under rule 12 and hand off to Hari with the unresolved gate, findings, reviewed revisions, cumulative counts and proposed next action. A third comprehensive pass requires Hari's explicit decision. The coordinator owns cumulative review accounting and reviews.

## Predicate-to-test coverage map

Scientific verifier predicate coverage is not applicable: this change adds no verifier judgement or expected outcome. The orchestration predicate map below names actual tests and their boundary cases.

| Predicate or boundary | Test evidence |
| --- | --- |
| Capture text is absent or exactly `true` / `false`; reject uppercase, empty, `1`, and padded text. Legacy routes require false; only V2/smoke/all/false and V2/smoke/authentication-controls/true pass. Selected controls with false/absent and all other scenarios/run kinds/items fail before private materialization. | `PhaseTests.test_v2_selection_values_are_frozen_and_exact`, `PhaseTests.test_batch_selection_and_strict_command_arguments`, `WorkflowTests.test_capture_validation_and_actual_stop_prerequisites`, `WorkflowTests.test_v2_selector_rejects_full_runs_and_partial_smokes_before_login`. |
| Validated selection, items list, run kind and Boolean are saved exactly; later environment changes do not replace frozen V2 execution values. Missing saved selector/capture keys retain all/false defaults; malformed selector, items, run kind or non-Boolean capture fails before scope transport or Jobs. | `PhaseTests.test_v2_selection_values_are_frozen_and_exact`, `ProbeV2Tests.test_selection_is_smoke_all_only_and_legacy_seed_is_noop`, `ProbeV2Tests.test_phase_entrypoints_use_frozen_v2_selection_after_environment_changes`, `ProbeV2Tests.test_corrupt_frozen_selection_fails_before_scope_or_jobs`. |
| Selected execution includes exactly C6 seed/action/judge then C9 action/judge, with unchanged roles, dependencies, immutable images and actor-only capture flag. Full V2 matrix remains 49 Jobs; selected render is five Jobs. String `"true"` fails chart schema; false/absent emits no worker option. | `ProbeV2Tests.test_selected_batch_is_fixed_dependency_closed_and_separate_from_full_smoke`, `ProbeV2Tests.test_actual_phase_values_render_all_stages_through_helm`. |
| Selected stage/candidate completion requires exactly five stage records and two accepted, smoke-valid candidate/receipt pairs; missing, duplicate or extra Job/candidate, rejected candidate or safety stop fails transport. Exhausted admission retains all five gaps without commands. | `ProbeV2Tests.test_selected_completeness_rejects_missing_duplicate_extra_candidates_jobs_and_safety_stop`, `ProbeV2Tests.test_selected_transport_failure_records_all_stages_and_gap_summary`. |
| Actor admission count accepts inclusive 0 and 5; rejects -1 and 6, Boolean, missing/incomplete capture, duplicate/unexpected actor, or wrong actor set. | `ProbeV2Tests.test_selected_admission_count_requires_exact_actor_set_and_inclusive_bounds`. |
| Batch stop truth table requires false on each existing route and true only for the selected route; rejected inputs invoke no stop preparation, login or stop; valid stop path survives a failed batch. A01/B04 true fails existing full-C3 selection before stop preparation/login; stop prerequisites remain unmet. | `WorkflowTests.test_batch_topology_and_exact_steps`, `WorkflowTests.test_capture_validation_and_actual_stop_prerequisites`. |
| Report names the fixed selected configuration. Existing upload, deletion, ephemeral destroy, full-C3 stop, workflow trigger, identity and full-matrix assertions remain in force. | `PhaseTests.test_selected_metadata_summary_line`, `WorkflowTests.test_reports_and_cleanup`, `WorkflowTests.test_final_workload_predicates_and_exact_job_order`, `WorkflowTests.test_batch_upload_includes_candidates_receipts_and_public_pods`, `WorkflowTests.test_mirror_and_dispatch_contract`. |

The selected route has a fixed five-stage set, so no larger valid stage count exists: a duplicate or unexpected stage fails exact-set/count checks. Likewise, the actor count must be exactly three; a missing or additional actor is an incomplete or unexpected actor, not an alternate valid boundary. Malformed tuples are rejected before resource/private preparation, so no Azure or Job behavior is reachable for them. The approved plan's step 6 defines these requirements; scientific verifier boundary/empty-input coverage is not applicable.

## Implementation record

| Command | Result |
| --- | --- |
| `bash infra/scripts/lifecycle/install-tools.sh /tmp/issue-245/tools ci` | Exit 0. Verified the repository-pinned CI tool versions and PyYAML 6.0.3. |
| `PYTHONDONTWRITEBYTECODE=1 PARCHWELL_TOOLS_DIRECTORY=/tmp/issue-245/tools PATH=/tmp/issue-245/tools:/tmp/issue-245/tools/venv/bin:$PATH python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'` | Exit 0; 120 tests passed, including 49 full-matrix and 5 selected Helm renders. |
| `PARCHWELL_TOOLS_DIRECTORY=/tmp/issue-245/tools PATH=/tmp/issue-245/tools:/tmp/issue-245/tools/venv/bin:$PATH bash infra/scripts/lifecycle/check-offline.sh` | Exit 0. ShellCheck, both workflow mirrors, actionlint, chart acceptance (25 positive, 32 negative, 4 worker-setting regressions), single-action acceptance (66 positive, 14 negative), V2 chart acceptance (99 positive, 117 negative), and two strict kubeconform smoke renders passed. |
| `npm ci --ignore-scripts` | Exit 0; lockfile dependencies installed. npm printed its existing `glob@10.5.0` deprecation notice. No dependency file changed. |
| `npm run check:docs` | Exit 0. Zero warnings in changed Markdown files; eight existing warnings in four unchanged files have the blanket disposition below. |
| `cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt` | Exit 0; copies are byte-identical. |
| `git diff --check` | Exit 0. |

Cold-review reports: none initiated by this implementation session. Comprehensive/focused budget used before review: 0/0. The coordinator owns cold reviews, cumulative budget, publication, hosted review and final handoff. No generated program output belongs in the authored diff.

## Source verification

Before implementation, I read the current plan and authority snapshot under `/tmp/issue245-authority`, `.agents/skills/implement-plan/SKILL.md`, all of `AGENTS.md`, `docs/agents/factory.md`, `docs/blueprint.md`, and the applicable engineering and documentation-check standards. The base commit was `c4a1724f9165397dd04f4e8df22acd9c8498ebd8`. Scope and behavior were checked against that pinned source: workflow and mirror, phase validator/preparer/report, V2 stage matrix and dependency logic, chart schema and template, verifier C6/C9 dependencies, existing lifecycle observer/metadata output and runtime limits. The selected configuration totals and job bounds are source-derived; no token lifetime or Azure behavior was measured.

The plan-derived collection bounds are five Jobs, three actor Jobs, fourteen unselected configurations, two candidate/receipt pairs, fifteen maximum client rows, 262144 bytes per row and 3932160 bytes total. I calculated them from the pinned `CONFIGURATIONS` and `stages` with this script:

```python
import importlib.util
from pathlib import Path
spec = importlib.util.spec_from_file_location("probe_v2", Path("infra/scripts/phase/probe_v2.py"))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
selected = module.AUTHENTICATION_CONTROLS
stages = [(item, config, stage, seconds) for item, config in selected
          for stage, _, seconds in module.stages(item, config)]
actors = sum(stage != "judge" for _, _, stage, _ in stages)
reservations = {f"{item}-{config}": sum(seconds + 120 for i, c, _, seconds in stages if (i, c) == (item, config))
                for item, config in selected}
print(len(stages), actors, len(module.CONFIGURATIONS) - len(selected), len(selected) * 5,
      actors * 5, actors * 5 * 262144, reservations, sum(reservations.values()))
```

The run printed `5 3 14 10 15 3932160 {'F06-C6': 3360, 'F06-C9': 1260} 4620`.

Documentation scan disposition: zero warnings in changed Markdown files. The scan reported eight warnings in four unchanged files (`docs/agents/factory.md`, `docs/operations/evidence/issue-124/README.md`, `docs/operations/evidence/issue-140/README.md`, and `docs/operations/evidence/issue-211/receipt.md`); one blanket disposition applies to those untouched files. The ten-warning changed-file disposition cap was not approached.

Implementation failure disposition: two distinct code/test root causes surfaced during offline development and were corrected: the phase entrypoint fixture initially did not provide a successful frozen V2 delegation, and the selected admission fixture did not persist transferred slot counts into phase state. The initial pre-bootstrap test run also showed a single missing-pinned-tools environment prerequisite (`helm`, `kubectl` and `az` were absent from the session PATH); after installing the documented pinned CI tools, the full suite passed. The pre-bootstrap tool failure is not counted as a code root cause. No unrelated failure was repaired and no tracked dependency was added.

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
- **Review budget:** 1/2 comprehensive and 0/2 focused cold reviews used after the first report. Do not initiate cold review in this implementation session. Under AGENTS.md rule 8, if another required review would exceed either cumulative budget, or focus is unsafe, stop, preserve unpublished work under rule 12 and hand off to Hari with the unresolved gate, findings, reviewed revisions, cumulative counts and proposed next action. A third comprehensive pass requires Hari's explicit decision. The coordinator owns cumulative review accounting and reviews.

## Predicate-to-test coverage map

Scientific verifier predicate coverage is not applicable: this change adds no verifier judgement or expected outcome. The orchestration predicate map below names actual tests and their boundary cases.

| Predicate or boundary | Test evidence |
| --- | --- |
| Capture text is absent or exactly `true` / `false`; reject uppercase, empty, `1`, and padded text. Legacy routes require false; only V2/smoke/all/false and V2/smoke/authentication-controls/true pass. Selected controls with false/absent and all other scenarios/run kinds/items fail before private materialization. | `PhaseTests.test_v2_selection_values_are_frozen_and_exact`, `PhaseTests.test_batch_selection_and_strict_command_arguments`, `WorkflowTests.test_capture_validation_and_actual_stop_prerequisites`, `WorkflowTests.test_v2_selector_rejects_full_runs_and_partial_smokes_before_login`. |
| Validated selection, scenario, items list, run kind and Boolean are saved exactly; later environment changes do not replace frozen V2 execution values. Seed remains a no-op from valid frozen V2 state even when the same scenario's current environment is invalid. Missing saved selector/capture keys retain all/false defaults; malformed saved scenario, selector, items, run kind or non-Boolean capture fails before scope transport or Jobs. | `PhaseTests.test_v2_selection_values_are_frozen_and_exact`, `ProbeV2Tests.test_selection_is_smoke_all_only_and_legacy_seed_is_noop`, `ProbeV2Tests.test_phase_entrypoints_use_frozen_v2_selection_after_environment_changes`, `ProbeV2Tests.test_corrupt_frozen_selection_fails_before_scope_or_jobs`. |
| Selected execution includes exactly C6 seed/action/judge then C9 action/judge, with unchanged roles, dependencies, immutable images and actor-only capture flag. Full V2 matrix remains 49 Jobs; selected render is five Jobs. Real candidate and receipt download pairs run for both selected configurations. Explicit Boolean false and absent chart values both emit no worker option; string `"true"` fails chart schema. | `ProbeV2Tests.test_selected_batch_is_fixed_dependency_closed_and_separate_from_full_smoke`, `ProbeV2Tests.test_actual_phase_values_render_all_stages_through_helm`. |
| Selected stage/candidate completion requires exactly five stage records and two accepted, smoke-valid candidate/receipt pairs; missing, duplicate or extra Job/candidate, rejected candidate or safety stop fails transport. Exhausted admission retains all five gaps without commands. | `ProbeV2Tests.test_selected_completeness_rejects_missing_duplicate_extra_candidates_jobs_and_safety_stop`, `ProbeV2Tests.test_selected_transport_failure_records_all_stages_and_gap_summary`. |
| Actor admission count accepts inclusive 0 and 5; rejects -1 and 6, Boolean, missing/incomplete capture, duplicate/unexpected actor, or wrong actor set. Selected valid transport with one skipped client slot reports fourteen admissions. | `ProbeV2Tests.test_selected_admission_count_requires_exact_actor_set_and_inclusive_bounds`, `ProbeV2Tests.test_selected_valid_capture_with_one_skipped_slot_reports_fourteen_admissions`. |
| Batch stop truth table requires false on each existing route and true only for the selected route. The test evaluates the actual job gate and declared step conditions and records eligible stub invocations from workflow declarations. Rejected inputs invoke no stop preparation, login or stop; the valid stop path survives a failed batch. A01/B04 true fails existing full-C3 selection before stop preparation/login; A01/B04 false admits the cleanup path. | `WorkflowTests.test_batch_topology_and_exact_steps`, `WorkflowTests.test_capture_validation_and_actual_stop_prerequisites`. |
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

The first comprehensive cold review was completed by the coordinator on `120f161c817deef7e5fb06f8a83805e960e61f09`; its complete report and accepted dispositions are attached below. It found four P1s grouped into two root causes: frozen V2 state was not authoritative in both entrypoints, and the planned tests did not cross the actual workflow/transport boundaries. All four findings were accepted and corrected in this revision. The coordinator owns the next cold review, cumulative budget, publication, hosted review and final handoff. No generated program output belongs in the authored diff.

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

Correction failure disposition: the first unpinned ad hoc test invocation omitted the documented tools PATH; missing Helm errors from that invocation were a tool prerequisite and are not counted as code failures. During pinned regression correction, three distinct test-root causes were found and fixed: the synthetic admission fixture lacked required admitted slots, the legacy no-private-input check inherited the private-directory variable, and the workflow simulator did not apply the actual batch job-level gate. These are grouped by root cause, not by failing test, for 3/10 failing-test root causes. No unrelated failure was repaired and no tracked dependency was added.

## Correction validation after comprehensive review 1

All four accepted P1 findings from the complete report below are corrected in this revision. The frozen selection now requires saved `scenario == probe-batch-2`; `single_action_seed` consults and validates a valid frozen V2 phase before current-environment validation; the workflow test evaluates the declared batch job gate and cleanup-step conditions while recording only synthetic stub invocations, including valid A01/B04 false paths; and the selected integration test exercises real actor capture transport and both candidate/receipt download pairs. Added exact boundary checks cover fourteen admissions with one skipped slot, explicit chart Boolean false, an absent capture variable despite inherited environment values, the real seed entrypoint under same-scenario environment changes, and the actual saved-scenario fallback path before scope.

| Command | Corrected revision result |
| --- | --- |
| `PYTHONDONTWRITEBYTECODE=1 PARCHWELL_TOOLS_DIRECTORY=/tmp/issue-245/tools PATH=/tmp/issue-245/tools:/tmp/issue-245/tools/venv/bin:$PATH python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'` | Exit 0; 121 tests passed. |
| `PARCHWELL_TOOLS_DIRECTORY=/tmp/issue-245/tools PATH=/tmp/issue-245/tools:/tmp/issue-245/tools/venv/bin:$PATH bash infra/scripts/lifecycle/check-offline.sh` | Exit 0; ShellCheck, actionlint, workflow mirrors, chart acceptance (25 positive, 32 negative, 4 worker-setting regressions), single-action acceptance (66 positive, 14 negative), V2 acceptance (99 positive, 117 negative), and strict kubeconform smoke renders passed. |
| `npm run check:docs` | Exit 0; zero warnings in changed Markdown files and eight existing warnings in four unchanged files, covered by the blanket disposition above. |
| `cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt` | Exit 0; copies remain byte-identical. |
| `git diff --check` | Exit 0. |

The offline acceptance and full phase discovery passed after the code and regression corrections. The receipt and its attached complete review report are authored records. The cumulative cold-review budget is 1/2 comprehensive and 0/2 focused; the coordinator owns the next required comprehensive review before publication. Hosted PR CI, hosted final-head Codex review, and Hari's protected-path merge remain mandatory later gates.

## Complete first comprehensive cold-review report and dispositions

The report below is retained in full as authored review evidence. All four P1 findings were accepted. Findings 1 and 2 shared the frozen-state authority root cause and are fixed in `probe_v2.py` and `phase.py`; findings 3 and 4 shared the missing real-boundary coverage root cause and are fixed in the workflow, phase and V2 test suites. The updated coverage map above identifies the corrected test methods.

```text
# Short authentication metadata wiring: cold review 1

Mode: comprehensive, first pass. Report: complete.

Reviewed revision: `120f161c817deef7e5fb06f8a83805e960e61f09`. Comparison revision: `origin/main` at `c4a1724f9165397dd04f4e8df22acd9c8498ebd8`. Inputs were the complete authored diff `/tmp/issue245-review-1.diff` and the authored receipt `docs/operations/evidence/issue-245/short-authentication-metadata-collection.md`. The checked-out HEAD matched the frozen reviewed revision. Cumulative cold-review use including this report is 1/2 comprehensive and 0/2 focused. No source edits, GitHub writes, extra delegated reviews, live operations or Azure commands were performed. Review-created Python bytecode was removed; the final repository status is clean.

The implementation substantially follows the approved offline design, including the default-off input, fixed selected configuration sequence, actor-only chart wiring, separate diagnostic summary, existing cleanup predicates and bounded future collection card. It has two frozen-input defects and incomplete required regression coverage. No scope creep or new scientific judgement was found.

## Authority and applicable rules

The selected specification is [short-authentication-metadata-collection, Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6525824679), GitHub `updated_at: 2026-10-30T15:11:34Z`. The [current approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6526348889), created after that edit, identifies and approves that exact revision for offline Codex implementation. The [next-collection decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6525717035) and [wiring clarification](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6525819641) define its scope. The issue body and recognized Decision/Approval comments were checked independently using REST reads; proposals and plan-check recommendations were not treated as decisions. The two current-revision checks named in the receipt do not supply live authorization. No later decision was present in the fetched issue comment set.

The review applies `.agents/skills/repo-review/SKILL.md`; all of AGENTS.md GOVERNANCE, Code Review Rules, Before opening a pull request, Skills and Writing standards; CLAUDE.md; docs/agents/factory.md; and the ADRs in docs/blueprint.md, including scope and deferred inventory. These governing files and the skill are unchanged by this diff, so the checked-out versions are their applicable `origin/main` versions. The workflow additionally receives the hard safety rules 8 and 9 review. docs/engineering/dotnet.md Testing was read for the predicate/test comparison, and docs/engineering/documentation-checks.md for the receipt's documentation evidence. No .NET, Terraform, Kafka-setting, scenario-definition, expected-outcome or results file changes invoke an additional rubric row.

## Complete touched-file coverage

Every file below received the universal rubric above. The full workflow copy was also checked byte for byte against the protected workflow.

| Touched file | Coverage and outcome |
| --- | --- |
| `.github/workflows/scenario.yml` | Dispatch input/default/type, exact items description, all five environment bindings, pre-login validation, exact batch-stop condition, main guards, protected environment, permissions, concurrency, uploads, deletion, destroy and stop reviewed. Triggers and identity/network scope remain unchanged. Workflow matches plan step 1. Required path-test evidence is partial, as reported below. |
| `charts/parchwell/templates/_probe-v2.tpl` | Non-judge-only literal true worker option; existing roles, dependency mounts, immutable images and bounds reviewed against the matrix. Matches plan step 4. |
| `charts/parchwell/values.schema.json` | Optional Boolean property and retained closed runtime schema reviewed. Matches plan step 4. |
| `docs/operations/authentication-lifecycle-observation.md` | Entire appended collection card checked against plan selection, identities, metadata writer/lifecycle/file-observer shapes, actual verifier dependencies and runtime limits. Counts and reservations trace to the cited pinned implementation. Separate execution authorization, service controls, missing-evidence treatment and deferred provenance/fault work are retained. Matches plan step 8. |
| `docs/operations/evidence/issue-245/short-authentication-metadata-collection.md` | Authorization/current revision, execution card, commands/results, caps, review handoff, source-derived counts and predicate map reviewed. The test map overstates the incomplete cases described in the findings. No generated output is committed. |
| `docs/operations/probe-v2-smoke-contract.md` | Exact revised dispatch paragraph and guard, full-smoke distinction, card link and retained full-smoke acceptance/history reviewed. Matches plan step 7. |
| `docs/operations/workflows/scenario.yml.txt` | Complete reviewed workflow change; byte-identical to `.github/workflows/scenario.yml`. |
| `infra/scripts/lifecycle/check-offline.sh` | Only the approved literal workflow-input contract changes; existing exact event audit retained. Matches plan step 5. |
| `infra/scripts/phase/phase.py` | Parser, validators, pre-materialization checks, frozen saved fields, both execution entry points, report and their callers reviewed. Plan step 2 is partial because seed execution still prioritizes current environment validation over frozen state. |
| `infra/scripts/phase/probe_v2.py` | Closed selection, actor values, unchanged scope/transfer/candidate paths, both orchestration branches, selected exact-set completion, admission counting, omitted configurations and diagnostic shape reviewed. Plan step 3 is partial because the previous saved-scenario guard was removed. |
| `infra/scripts/phase/tests/test_phase.py` | Added summary, preparation and invalid-input cases and their fixtures reviewed. The purported absent-capture fixture retains an unrelated invalid capture value. |
| `infra/scripts/phase/tests/test_probe_v2.py` | Added selected-order/count/completeness/admission/frozen-input/render cases, changed full-route candidate mock and all affected existing transport tests reviewed. Required selected download and skipped-slot integration cases are missing; explicit false rendering is not exercised. Existing assertions were not loosened. |
| `infra/scripts/phase/tests/test_workflow.py` | Added exact input/guard assertions, tuple truth table, selection subprocesses and cleanup tests reviewed alongside existing order/cleanup/upload assertions. The new cleanup invocation assertions manufacture the outcome instead of exercising the declared step prerequisites. |

## Specification comparison and verification

Plan steps 1, 4, 5, 7 and 8 are implemented within their named files and exclusions. Steps 2 and 3 are partial for the frozen execution boundary. Step 6 is partial for the required regression cases. The fixed selection is F06 C6 seed/action/judge followed by F06 C9 action/judge; the unchanged verifier matrix consumes C6 seed/action as the C9 dependency, and aggregate prior-candidate collection is confined to the item's last configuration, C11. The source-derived selected totals were independently recalculated: five stages, three actors, fourteen omitted configurations and 4620 seconds of stage reservations. The full-route report remains separate from selected metadata transport. No worker/library/resolver/verifier/scenario/Terraform/package/project/lifecycle-limit change or unsupported live claim was introduced.

The scientific predicate map is correctly marked not applicable because verifier judgement and expected outcomes do not change. The orchestration map is useful but incomplete: it misses the saved-scenario and same-scenario environment-change failures, and some claimed cases do not execute the promised boundary. The exact missing checks below are already in plan step 6; they are not requests for additional hardening outside the approved contract.

Independent offline verification on the frozen head:

- `PYTHONDONTWRITEBYTECODE=1 PARCHWELL_TOOLS_DIRECTORY=/tmp/issue-245/tools PATH=/tmp/issue-245/tools:/tmp/issue-245/tools/venv/bin:$PATH python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'`: exit 0, 120 tests passed, including actual Helm renders.
- `PARCHWELL_TOOLS_DIRECTORY=/tmp/issue-245/tools PATH=/tmp/issue-245/tools:/tmp/issue-245/tools/venv/bin:$PATH bash infra/scripts/lifecycle/check-offline.sh`: exit 0. ShellCheck, workflow audit/mirrors, actionlint, chart suites and strict kubeconform renders passed. Chart acceptance reported 25 positive/32 negative/4 worker-setting regressions, single-action 66 positive/14 negative, and V2 99 positive/117 negative.
- `npm run check:docs`: exit 0; eight warnings in the same unchanged files listed by the receipt, no changed-Markdown warning.
- Workflow `cmp` and `git diff --check c4a1724 120f161`: exit 0.
- Bounded synthetic probes reproduced both frozen-input defects without invoking commands. A separate in-memory workflow mutation demonstrated that the new cleanup test still passes after both `stop_prepare` steps acquire `if: always()`.

Hosted CI, hosted final-head Codex review and Hari's protected-path merge remain subsequent gates; this cold review does not assert their completion or authorize a dispatch.

## Findings

P1  infra/scripts/phase/probe_v2.py:683  Approved Revision 3, Exact offline implementation step 3 and Enforced check 2; AGENTS.md Before opening a pull request rule 8  Restore validation that saved scenario is probe-batch-2 before scope transport. The replaced run_batch guard previously checked scenario, but selection checks only items/run_kind/selector/capture. A selected saved phase with scenario=A01 is accepted; with the valid V2 environment, the real Phase.run_single_action_batch fallback reaches scope transport.  Evidence: selection at lines 145-156 never reads scenario; Phase lines 783-788 route by environment when the saved scenario is not V2; the synthetic probe reached the patched scope sentinel rather than rejecting the malformed saved tuple.

P1  infra/scripts/phase/phase.py:750  Approved Revision 3, Exact offline implementation steps 2 and 6 (frozen execution inputs); AGENTS.md Before opening a pull request rule 8  Consult frozen V2 state before validating current environment in single_action_seed. The new first branch validates changed environment and returns before reading phase.json whenever the scenario environment remains probe-batch-2. A valid frozen selected phase followed by environment run_kind=batch/items=all/capture=false fails the seed step, preventing the following batch step despite its valid frozen selection.  Evidence: the synthetic fixture produced Failure unsupported probe batch selection instead of the required V2 seed no-op; the new regression changes the environment scenario to A01 and therefore misses this earlier branch.

P1  infra/scripts/phase/tests/test_workflow.py:376  Approved Revision 3, Exact offline implementation step 6 (literal workflow-path cases and recorded synthetic/stubbed commands); AGENTS.md Before opening a pull request rule 8  Exercise actual declared stop-step prerequisites and record stub invocations instead of constructing expected invocation lists and skipped outcomes. The batch test unconditionally appends stop_prepare/login/stop when the job guard is true, and the full-C3 test assigns skipped outcomes itself; neither shows those steps are skipped or admitted by the workflow conditions, and valid A01/B04 false cleanup paths are not exercised by this new coverage.  Evidence: lines 374-378 manufacture invocations, lines 386-392 manufacture skip outcomes, and the entire new cleanup test still passes when both real stop_prepare definitions are mutated in memory to if: always(), which violates the rejected full-C3 tuple requirement.

P1  infra/scripts/phase/tests/test_probe_v2.py:218  Approved Revision 3, Exact offline implementation step 6 (selected-path command/render tests and literal expected values); AGENTS.md Before opening a pull request rule 8  Complete the required selected-path regression cases rather than treating manufactured capture/candidate records as their evidence. The selected successful run stubs both transfer and candidate, so it does not exercise or assert the two selected candidate/receipt download pairs; no selected valid-capture skipped-slot case asserts 14 admissions. The chart's file named false-selected-chart-values removes the option instead of testing Boolean false, and the absent-input case in test_phase does not remove the inherited malformed value.  Evidence: test_probe_v2 lines 204-219 bypass transfer/downloads; lines 303-323 assert 15 and 10 but never 14; lines 448-453 cover absence only; test_phase lines 694-710 leave PARCHWELL_CAPTURE_AUTHENTICATION_METADATA='false ' in the outer environment when capture=None. These are four explicit plan cases, not new checks.

Findings: 4 at P0 or P1

Pre-existing (not blocking):

None identified within the reviewed and affected context. The documentation scan's unchanged-file warnings are already covered by the receipt's blanket disposition and are not findings against this diff.
```

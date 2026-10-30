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
- **Review budget:** 2/2 comprehensive and 0/2 focused cold reviews used after the second report. Do not initiate cold review in this implementation session. Under AGENTS.md rule 8, if another required review would exceed either cumulative budget, or focus is unsafe, stop, preserve unpublished work under rule 12 and hand off to Hari with the unresolved gate, findings, reviewed revisions, cumulative counts and proposed next action. A third comprehensive pass requires Hari's explicit decision. The coordinator owns cumulative review accounting and reviews.

## Continuation execution card: merge PR #259 with full experiment PR #258

- **Deliverable:** Merge `origin/main` into this approved issue #245 branch and resolve only integration conflicts. Incoming main contains the merged issue #240 full experiment; preserve its `batch`/`all`/B08,E01,F06 contracts and scientific verifier/access-audit implementation unchanged.
- **Authority:** Hari's approval of [Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6525824679), with [independent checks](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6526259257). This continuation authorizes conflict resolution; publication and review ownership remain with the coordinator.
- **Merge inputs:** issue #245 head `cf273d4b27ff8695afda0a00fb031bc7133abce1`; merge `origin/main` at `32babefac185b8e8a4855e4f3892c1772e431d29`; common base `c4a1724f9165397dd04f4e8df22acd9c8498ebd8`. Create a merge commit; do not rebase or force-push.
- **Resolution boundary:** retain the default-off V2/smoke/authentication-controls/true tuple, frozen selection inputs, selection-aware transport/reporting and cleanup guard, alongside main's full-experiment batch route, `run_kind` schema and F06 access-check prerequisites. Keep `.github/workflows/scenario.yml` and `docs/operations/workflows/scenario.yml.txt` byte-identical. No scientific judgment, expected outcome, identity permission, credential or live behavior changes.
- **Offline gates:** after resolution run the approved phase unittest discovery, pinned `check-offline.sh`, workflow mirror `cmp`, `npm run check:docs` and `git diff --check`; record exact commands and results in this receipt. Only one focused review remains across sessions, owned by the coordinator; this session does not initiate review.
- **Restrictions:** no GitHub writes, push, labels, dispatch, Azure access, credentials or token-content reads. Do not edit results, expected outcomes or verifier claims.

## Conflict resolution, validation and frozen review handoff

The merge was performed from cf273d4b27ff8695afda0a00fb031bc7133abce1 with origin/main at 32babefac185b8e8a4855e4f3892c1772e431d29, common base c4a1724f9165397dd04f4e8df22acd9c8498ebd8. Git reported ten conflicted paths: .github/workflows/scenario.yml, charts/parchwell/values.schema.json, docs/operations/probe-v2-smoke-contract.md, docs/operations/workflows/scenario.yml.txt, infra/scripts/lifecycle/check-offline.sh, infra/scripts/phase/phase.py, infra/scripts/phase/probe_v2.py, and infra/scripts/phase/tests/test_phase.py, test_probe_v2.py, and test_workflow.py. The original combined conflict hunks and unmerged index stages are retained in /tmp/issue245-conflict-hunks.diff and /tmp/issue245-unmerged-index.txt.

The workflow dispatch description now names both approved V2 smoke selectors and main's scientific batch selectors. Its stop expression composes three closed routes: legacy probe batch with capture disabled, full smoke with capture disabled or authentication-controls smoke with capture enabled, and main's canonical scientific batch selectors with capture disabled. Existing batch stop preparation still validates the route before Azure login. The chart runtime schema retains both the optional Boolean metadata flag and main's required run_kind enum under the existing closed object schema.

The phase adapter keeps smoke all and authentication-controls tuple validation and frozen selector fields, and admits main's ordered V2 scientific batch subsets only with capture disabled. Effective-scope confirmation remains smoke-only; namespace role selection and the workflow's smoke-only effective-scope secret binding remain as merged on main. ProbeV2 dispatches the fixed five-job metadata assessment only for authentication-controls smoke. Full smoke keeps its existing report and acceptance path; batch keeps the merged main collector, F06 permission-audit admission/freshness checks, scientific candidate handling, selected item report, and batch summary. The chart's run_kind propagation, conditional F06 access-check mount, access collector, and verifier behavior are preserved from main. The test changes exercise the composed selection/stop contracts and retain both the selected smoke tests and scientific batch tests. No verifier implementation, scientific expected outcome, identity permission, results file, runtime bound or live action was authored by this merge resolution; these paths present from origin/main remain incoming merge content.

### Offline acceptance ledger

| Approved command | Result |
| --- | --- |
| PYTHONDONTWRITEBYTECODE=1 PARCHWELL_TOOLS_DIRECTORY=/tmp/issue-245/tools PATH=/tmp/issue-245/tools:/tmp/issue-245/tools/venv/bin:$PATH python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py' | Exit 0; 167 tests passed, including actual Helm rendering. |
| PARCHWELL_TOOLS_DIRECTORY=/tmp/issue-245/tools PATH=/tmp/issue-245/tools:/tmp/issue-245/tools/venv/bin:$PATH bash infra/scripts/lifecycle/check-offline.sh | Exit 0. ShellCheck, workflow audit/mirrors, actionlint, chart checks and strict kubeconform renders passed. Chart acceptance: 25 positive, 32 negative, 4 worker-setting regressions; single-action: 66 positive, 14 negative; probe V2: 148 positive, 117 negative. |
| cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt | Exit 0; protected workflow and committed copy are byte-identical. |
| npm run check:docs | Exit 0; zero warnings in changed Markdown; eight warnings in four unchanged files receive the existing blanket disposition. |
| git diff --check | Exit 0. |

The first phase-suite attempt reported three failing test cases from two distinct roots: the selected metadata fixture retained run_kind=batch while setting a smoke tuple, and the selected-only report branch intercepted the scientific batch report path. The fixture now sets its declared smoke kind; the metadata report is guarded by the exact selected smoke mode, leaving batch candidate validation and reporting intact. Batch test candidates now carry the accepted transported candidate records required by the existing completeness contract. All 167 tests passed on rerun. The previous implementation receipt recorded 3/10 distinct failing-test roots; this merge added 2/10, for 5/10 cumulatively. The ten-root stop action remains the approved push -wip and hand off to the next session; it was not reached. Changed-file warning count is zero and the ten-warning cap was not approached.

### Frozen review inputs

The committed merge head is the frozen review input. Compare the issue #245 reviewed branch revision cf273d4b27ff8695afda0a00fb031bc7133abce1 to this merge head, with origin/main at 32babefac185b8e8a4855e4f3892c1772e431d29 as the incoming context. Supply the complete PR diff against main and the focused delta since cf273d4, plus this receipt and the pre-resolution conflict hunks in /tmp/issue245-conflict-hunks.diff. The review scope is the authored conflict adapter in the ten paths above and its affected surrounding contracts/callers/tests; incoming issue #240 verifier, access-audit, expected-outcome and evidence implementation is source context from main, not newly authored issue #245 work. No review is requested or initiated in this session.

Before this handoff, cumulative cold review use remains 2/2 comprehensive and 1/2 focused; one focused round remains across sessions and is owned by the coordinator. This merge changes only a bounded composition of already approved tuples with merged main contracts, so the coordinator can apply the remaining focused review against these frozen inputs. Hosted final-head CI/review and protected-path merge remain pending gates.

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

The coordinator completed a comprehensive review on `120f161c817deef7e5fb06f8a83805e960e61f09` and a consolidated comprehensive review on `4addc705473ea3dc349781177d1cf545341b441b`; both complete reports and dispositions are attached below. The first found four P1s, all accepted and corrected. The second found one prose-only P2, corrected below. Cumulative review use is now 2/2 comprehensive and 0/2 focused; the coordinator owns the required P2-correction focused review, publication, hosted review and final handoff. No generated program output belongs in the authored diff.

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

The offline acceptance and full phase discovery passed after the code and regression corrections. The receipt and its attached complete review reports are authored records. Cumulative cold-review use after the second report is 2/2 comprehensive and 0/2 focused; the coordinator owns the required P2-correction focused review that reports P0/P1 findings only. Hosted PR CI, hosted final-head Codex review, and Hari's protected-path merge remain mandatory later gates.

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

## Complete second comprehensive cold-review report and disposition

Review mode: comprehensive, second and final pass. Reviewed revision: `4addc705473ea3dc349781177d1cf545341b441b`; comparison revision: `origin/main` at `c4a1724f9165397dd04f4e8df22acd9c8498ebd8`. Cumulative review counts after this report are 2/2 comprehensive and 0/2 focused. The sole P2 finding was accepted and corrected only in the already changed collection-card prose: token values remain prohibited, while token-file paths, identifiers and raw request bodies are excluded from the authored summary. The summary qualifier now preserves raw sanitized public pod projection evidence required by the card. No implementation behavior, test or scope changed. The coordinator owns the one required P2-correction focused review, reporting P0/P1 findings only.

The report below is retained in full as authored review evidence.

```text
# Short authentication metadata wiring: cold review 2

Mode: comprehensive, consolidated second/final pass.

Report: complete

Reviewed revision: `4addc705473ea3dc349781177d1cf545341b441b`. Comparison revision: `origin/main` at `c4a1724f9165397dd04f4e8df22acd9c8498ebd8`. Author inputs were the entire authored diff `/tmp/issue245-review-2.diff` and `docs/operations/evidence/issue-245/short-authentication-metadata-collection.md`. The checked-out HEAD matched the frozen reviewed revision. Cumulative use including this report is 2/2 comprehensive and 0/2 focused. This review made no repository edits, GitHub writes, additional delegations, Azure calls or live operations.

The offline implementation meets the approved selected-route behavior and corrects the four prior P1 findings. One prose-only P2 remains in the future collection instructions. No P0 or P1, unsupported live conclusion, changed scientific judgement, identity/network expansion or scope creep was found.

## Authority and governing rules

I independently fetched the current issue body and all 45 comments through GitHub REST. The governing selected specification remains [short-authentication-metadata-collection, Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6525824679), updated `2026-10-30T15:11:34Z`. The [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6526348889), created `2026-10-30T15:21:46Z`, explicitly approves that current revision for offline Codex implementation. The [next-collection decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6525717035) and [wiring clarification](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6525819641) define the candidate and exclusions. Decision/Approval comments were recognized in creation order under factory Item lifecycle step 3; earlier candidate approvals, plans, checks and handoffs were not substituted for current authority. No later Decision/Approval was present. This is no authorization for collection or dispatch.

Every touched file received the universal rubric: AGENTS.md GOVERNANCE, including scope/deferred items, Code Review Rules, Before opening a pull request, Skills and Writing standards; CLAUDE.md; docs/agents/factory.md; and all blueprint ADRs. I verified that those files and `.agents/skills/repo-review/SKILL.md` are unchanged from `origin/main`, so their checked-out versions are the applicable main versions. Workflow changes additionally received hard safety rules 8 and 9 review. I read docs/engineering/dotnet.md Testing for the predicate/test comparison and docs/engineering/documentation-checks.md for the evidence contract. No changed engineering-standard, .NET, Terraform, Kafka-setting, scenario-definition, expected-outcome or results path invokes another rubric row. No fallback to a branch-only governing document was needed.

## Every touched file and specification coverage

| File | Review and specification result |
| --- | --- |
| `.github/workflows/scenario.yml` | Plan step 1: exact default-off Boolean input and revised items description; all five capture bindings; pre-login selection validation; literal batch-stop guard; unchanged main guards, triggers, protected environment, permissions, concurrency, image publishing, step order, uploads, deletion, destroy and stop predicates. The selected route uses existing jobs. |
| `docs/operations/workflows/scenario.yml.txt` | Same complete workflow review; byte-for-byte comparison with the protected workflow passed. |
| `charts/parchwell/templates/_probe-v2.tpl` | Plan step 4: only the existing non-judge branch emits literal `CaptureAuthenticationMetadata=true`; false/absence and judges emit none. Existing image, role, dependency, node-placement and deadline behavior remains intact. |
| `charts/parchwell/values.schema.json` | Plan step 4: capture is an optional Boolean and the runtime object retains `additionalProperties: false`; no arbitrary environment or lifecycle override. |
| `infra/scripts/lifecycle/check-offline.sh` | Plan step 5: only the expected dispatch-input map and approved description change; exact event/trigger auditing remains. |
| `infra/scripts/phase/phase.py` | Plan step 2: strict absent/true/false parsing, closed route combinations, validation before materialization, exact saved V2 selector/Boolean/items/run kind, frozen-state routing in seed and batch entrypoints, seed no-op and literal selected summary. Current-environment changes do not replace a valid saved V2 tuple. Existing role enabling, publishing and cleanup remain. |
| `infra/scripts/phase/probe_v2.py` | Plan step 3: saved scenario/type/value validation before scope or stages, fixed C6/C9 sequence, actor-only flag, unchanged transfer and candidate judgement, selection-derived admission actor set, exact Job/candidate completeness, ordered omitted configurations and separate diagnostic output. Full-route smoke schema and acceptance remain distinct. |
| `infra/scripts/phase/tests/test_phase.py` | Plan step 6: exact persisted selector/Boolean/items, malformed-text and invalid-route failures, actual absent-input removal, no materialization/commands on rejection and literal report line. Existing assertions remain. |
| `infra/scripts/phase/tests/test_probe_v2.py` | Plan step 6: five ordered selected stages, real actor transfer and candidate/receipt validation with downloads stubbed at the I/O boundary; both download pairs asserted; exact summary keys/constants; fourteen omissions; exhausted-admission gaps; 15/14 admission integrations; integer 0/5 and -1/6/Boolean boundaries; missing/duplicate/unexpected actors, stages and candidates; safety-stop rejection; frozen-state entrypoints and corrupt-state rejection. Actual-value Helm tests retain all 49 full-matrix renders and add the selected five, dependency mounts, accounts, immutable images, three actor flags, absent/Boolean-false omission and string rejection. |
| `infra/scripts/phase/tests/test_workflow.py` | Plan step 6: exact dispatch contract and stop expression, selection subprocesses and truth-table cases, declared cleanup conditions with recorded synthetic invocations, rejected A01/B04 opt-ins and valid false cleanup paths. Existing upload/order/identity/destroy/stop assertions remain. The corrected test now evaluates the batch job-level guard and the actual full-C3 prerequisites. |
| `docs/operations/probe-v2-smoke-contract.md` | Plan step 7: approved dispatch paragraph and exact stop expression, explicit separation of selected collection from full-smoke acceptance, linked collection card, unchanged full-smoke criteria/history. |
| `docs/operations/authentication-lifecycle-observation.md` | Plan step 8: fixed selection/table, ordinary versus exception roles, service/dependency evidence, row/snapshot/file-sample acceptance, installed-expiry source, actual admitted projection context, bounded inspection, gaps, existing execution/cleanup limits and separate future authorization. Counts and dependency claims match pinned sources. One summary-publication qualifier was lost; see P2 below. |
| `docs/operations/evidence/issue-245/short-authentication-metadata-collection.md` | Plan step 8 and AGENTS.md: execution card, current authority/base, exact acceptance commands/results, disposition caps, scientific predicate non-applicability, orchestration test map, complete first report and consolidated dispositions. Review/publication/hosted gates and cumulative budget ownership are recorded. The receipt appropriately records the first review count before this second review. No program output file is included in the authored diff. |

Plan steps 1 through 8 and enforced checks 1 through 4 have implementation and test coverage. The first report's frozen-scenario omission is corrected by selection's explicit saved-scenario guard; the actual batch fallback under a valid V2 environment now reaches that guard before scope. The seed entrypoint consults valid frozen V2 state before current-environment validation. The workflow regression evaluates declared prerequisites instead of manufacturing skipped outcomes. The selected integration crosses transfer and candidate/receipt parsing, includes the 14-admission case, and renders explicit Boolean false separately from absence.

The test map correctly distinguishes orchestration predicates from scientific verifier predicates. No verifier judgement or expectation changes make scientific boundary coverage applicable. Invalid saved selector/items/run-kind/capture/scenario fixtures reject before scope/stages; exact selected Job/candidate sets and actor counts bound the successful state. Existing scientific assertions were neither loosened nor replaced.

## Source checks and verification

I read the cited pinned executor, phase bridge, chart, runtime-limit document, verifier dependency matrix and aggregation handler. Source-derived counts were recalculated from the base executor using a script: five selected stages, three actors, fourteen omitted configurations, ten action slots, fifteen maximum metadata rows, 3932160 maximum JSON bytes, reservations C6=3360 seconds and C9=1260 seconds, totaling 4620 seconds. C9 consumes C6 seed/action; final-item aggregation applies to F06 C11, not the selected judges. I checked the metadata collector/writer shape against the card's marker, schema, sample boundaries, first successful installation and byte ceiling. The implementation does not convert callback return, token installation, generation changes or transport success into service acceptance, exchange provenance, renewal or rotation evidence.

Independent checks on the frozen head:

- Eleven targeted tests covering corrected preparation/frozen state, selected actor values, ordered transfer/candidate download pairs, exhaustion, 14 admissions, completeness/count boundaries, malformed saved state, actual cleanup prerequisites and report output: exit 0, `Ran 11 tests ... OK`.
- `cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt`: exit 0.
- `git diff --check c4a1724f9165397dd04f4e8df22acd9c8498ebd8 4addc705473ea3dc349781177d1cf545341b441b`: exit 0.
- Repository status remained clean; imports used `PYTHONDONTWRITEBYTECODE=1`.

The receipt records the complete corrected 121-test discovery, passed pinned lifecycle/actionlint/ShellCheck/chart/kubeconform matrix and documentation scan. I inspected that evidence and relevant test implementations; no unresolved concern justified repeating the expensive complete lifecycle matrix. Its eight unchanged-document warnings retain the authored blanket disposition, with zero changed-Markdown warnings. Hosted final-head CI/review and Hari's protected-workflow merge remain subsequent gates. No dispatch is validated or authorized by this report.

## Findings

P2  docs/operations/authentication-lifecycle-observation.md:99  Approved Revision 3, Later collection card, Measures and retained evidence; AGENTS.md Code Review Rules P2 reader bar  Restore the phrase `in the authored summary` after the publication prohibition. The plan scopes omission of token-file paths and identifiers to the authored summary, while this sentence reads as a ban on their publication anywhere. A future collection operator could consequently redact or reject the raw public pod projection evidence that this same card requires preserving. Evidence: approved plan says `Do not publish ... in the authored summary`; card line 93 retains existing public pod uploads, line 99 requires actual token-file environment/mount/source inspection, and unchanged `probe_v2.py` public_pod retains token-file environment/mount/projection paths. This is a prose-only clarification in an already changed file and adds no check or scope.

Findings: none at P0 or P1

Pre-existing (not blocking):

The eight documentation-scan warnings in four unchanged files are already covered by the receipt's blanket disposition. No additional pre-existing blocking issue was identified in the reviewed scope.
```


## Final focused review report, unresolved findings and handoff

The following complete focused report is retained as authored. Its two P1 findings are accepted and unresolved. This report supersedes the earlier continuation-card look-ahead that one focused round remained: cumulative cold-review use is now 2/2 comprehensive and 2/2 focused, with the review gate exhausted.

```text
# Authentication metadata wiring: focused conflict-resolution review

Mode: focused correction/integration review. Report: complete.

Reviewed revision: `de20b3df9d489dd1e48a913e901c5c353d27fdee`. Comparison revision: previously reviewed `cf273d4b27ff8695afda0a00fb031bc7133abce1`. Incoming main and second merge parent: `32babefac185b8e8a4855e4f3892c1772e431d29`; common base: `c4a1724f9165397dd04f4e8df22acd9c8498ebd8`. The checked-out HEAD and origin/main matched these frozen revisions. Cumulative use including this report is 2/2 comprehensive and 2/2 focused rounds.

Inputs supplied by the author were `/tmp/issue245-conflict-full.diff`, `/tmp/issue245-conflict-since-reviewed.diff`, `/tmp/issue245-conflict-combined.diff`, and the authored receipt `docs/operations/evidence/issue-245/short-authentication-metadata-collection.md`. The full PR diff supplied context; this report does not repeat a comprehensive PR review. The focused scope is the authored conflict adapter, its changed receipt, and affected surrounding callers, tests and contracts. Incoming main's scientific implementation, access collector, expected outcomes and evidence are context rather than newly authored issue-245 changes. Earlier unchanged reviewed work retains coverage. The earlier receipt records no unresolved P0/P1 finding carried into this round.

The scope can be reviewed safely as a bounded integration change. The two findings below are concrete adapter regressions; neither requires a new scientific design or a comprehensive repeat to identify. No source edits, GitHub writes, delegation, live operations, Azure commands, credential acquisition or token-content reads were performed. Review files and synthetic fixtures remained outside the repository; final repository status was clean.

## Authority and rules

The selected specification remains [short-authentication-metadata-collection, Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6525824679), GitHub updated_at `2026-10-30T15:11:34Z`, under [the explicit offline approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6526348889). The current issue body and comment pages were fetched with REST reads; recognized Decision/Approval comments were distinguished from proposals, checks and handoffs under `docs/agents/factory.md` Item lifecycle step 3. The latest comment is still that approval. The [selected-control decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6525819641) and [current independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6526259257) retain the offline scope and separate later execution authorization. The receipt's continuation card requires preservation of incoming main's scientific batch selectors, permission-audit prerequisites and runtime behavior.

Every scoped file received the universal rubric: AGENTS.md GOVERNANCE including scope/deferred work, Code Review Rules, Before opening a pull request, Skills and Writing standards; CLAUDE.md; docs/agents/factory.md; and docs/blueprint.md ADRs. Governing source versions are those on origin/main at the incoming main revision, including the current blueprint's scoped permission-audit exception. The repository-owned repo-review skill is unchanged against that main. Workflow files additionally received hard safety rules 8 and 9 review. The predicate/test comparison used docs/engineering/dotnet.md Testing. No authored .NET, Terraform, Kafka-setting, scenario-definition, expected-outcome or results change adds another rubric row. Incoming changes in those areas were not relabelled as issue-245 findings.

## Focused file coverage

| File | Scope and outcome |
| --- | --- |
| `.github/workflows/scenario.yml` | Composed dispatch description, closed capture/selector cleanup gate, unchanged step prerequisites and batch callers checked. Main's strict prepare-stop validation remains before login, including case-insensitive expression false positives. Trigger, identity, concurrency and cleanup policies are preserved. |
| `charts/parchwell/values.schema.json` | Optional Boolean capture flag coexists with main's required smoke/batch run_kind enum and closed runtime object. The affected template retains actor-only capture and judge-only RunKind/access-check propagation. |
| `docs/operations/probe-v2-smoke-contract.md` | Smoke and scientific-batch admission descriptions and cleanup distinction checked against actual validators/workflow. Its advertised explicit scientific selectors are broken by finding 1. |
| `docs/operations/workflows/scenario.yml.txt` | Same adapter as protected workflow; independently compared byte for byte. |
| `infra/scripts/lifecycle/check-offline.sh` | Combined literal dispatch input description and retained Boolean input checked against workflow declaration; exact event audit retained. |
| `infra/scripts/phase/phase.py` | Composed parser/preparation checks, frozen selector producer, seed/batch execution consumers and mixed item/configuration report entries checked. Finding 1 occurs across preparation and the execution helper. Smoke-only effective-scope materialization and conditional namespace-role settings retain main's contracts. |
| `infra/scripts/phase/probe_v2.py` | Selection, values, orchestration, scope/permission-admission callers and separate smoke/metadata/scientific reports checked. Findings 1 and 2 regress scientific execution. Selected five-stage and full-smoke output branches remain separated from batch reporting. |
| `infra/scripts/phase/tests/test_phase.py` | Actual preparation, invalid-input, role/confirmation and summary cases checked. Preparation tests stop before consuming the newly saved selector, so they miss finding 1. |
| `infra/scripts/phase/tests/test_probe_v2.py` | Selected route/frozen-state tests and affected incoming scientific stage-count, permission and render tests checked. `batch_phase` writes run_kind/items directly and omits probe_v2_selection, masking finding 1. Instant mocked audit collection masks finding 2. Required selected download/count/completeness assertions remain. |
| `infra/scripts/phase/tests/test_workflow.py` | Composed exact expression, case-insensitive truth table and actual declared cleanup-step prerequisite simulation checked. Existing rejected capture routes and valid legacy/smoke cleanup behavior remain covered. |
| `docs/operations/evidence/issue-245/short-authentication-metadata-collection.md` | New continuation authorization/boundary, merge rationale, frozen inputs, acceptance ledger and budget handoff checked. The assertion that scientific batch behavior and runtime reservations are preserved is incorrect for the two findings. Earlier reports are context, not new comprehensive review targets. |

The affected chart template, runtime limits, phase state/clock and stop preparation, scientific contract, existing scientific batch test fixtures, and main's pre-merge run_batch implementation were read as surrounding contracts. The scientific verifier and access collector implementation are unchanged against incoming main. No identity permission, public result or scientific judgement change was found in the authored resolution.

## Specification comparison and verification

Approved Revision 3's default-off selected smoke tuple, five dependency-closed stages, frozen inputs, actor-only option, separate diagnostic output and cleanup boundary remain represented. The merged chart/schema and workflow expose both already approved routes without introducing a new route. Integration is nevertheless incomplete: explicit scientific item selections accepted by main's contract cannot execute, and the first F06 configuration no longer receives main's full post-audit reservation. These violate the continuation's preservation boundary and Revision 3's requirement to preserve existing routes and execution limits. No additional scientific requirement, expectation or deferred experiment is proposed.

The receipt's 167-test phase-suite and full pinned offline acceptance results were read as authored evidence. Those expensive suites were not repeated. New bounded, synthetic probes independently checked the two material concerns:

- Using the existing `PhaseTests` synthetic preparation fixture and real `Phase.prepare_batch`, the selectors `all`, `B08`, `F06`, `B08,F06`, and `B08,E01,F06` each prepared successfully. The saved selector exactly equalled its input. The real ProbeV2 selection helper accepted only `all`; each explicit selector raised `ValueError: v2_smoke_selection`. No commands were invoked. Both real seed and batch entrypoints call this helper on the frozen phase.
- Using the existing `ProbeV2Tests` fixture with a valid `batch/all` frozen phase, a monotonic clock, stubbed scope/stages/transfers/candidates, and a synthetic access collector that advances that clock by 300 seconds, the first F06 C0 stage was handed only 3060 seconds of its 3360-second configuration reservation. Main at `32babef` performs collection before computing that deadline, retaining all 3360 seconds after collection. These durations are synthetic reproduction inputs and source-derived bounds, not live measurements.
- `cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt`: exit 0.
- `git diff --check 32babef HEAD`: exit 0.
- `git status --short`: empty after the probes.

The synthetic probes used `PYTHONDONTWRITEBYTECODE=1` and the existing pinned-tools Python environment. They did not render or submit Jobs, execute the access collector, or contact Azure. Hosted final-head CI/review and Hari's protected-path merge remain separate pending gates.

## Findings

P1  infra/scripts/phase/probe_v2.py:152  Approved Revision 3 Exact offline implementation steps 2/6 and Enforced check 2; receipt Continuation execution card Resolution boundary; AGENTS.md Before opening a pull request rule 8  Accept the canonical frozen scientific selector produced by preparation. The new batch arm requires saved probe_v2_selection == all, but Phase.prepare_batch saves the literal input such as B08, F06 or B08,F06. Every explicit valid scientific selection therefore passes pre-login validation/preparation and then raises v2_smoke_selection from the seed/batch execution entrypoint, preventing the already merged scientific route from running. Evidence: phase.py lines 281-284 save the raw selector, ProbeV2.selection lines 152-160 reject it, and real synthetic preparation reproduced successful preparation followed by rejection for B08, F06, B08,F06 and B08,E01,F06; test_probe_v2.py batch_phase omits the selector and masks the mismatch.
P1  infra/scripts/phase/probe_v2.py:825  Approved Revision 3 Finite work/exhaustion execution-limit preservation; receipt Continuation execution card Resolution boundary; AGENTS.md Before opening a pull request rule 8  Compute the first scientific F06 configuration deadline after the permission audit, as incoming main does. The resolution moves reservation/reason/deadline computation before collect_access, so the separately reserved audit consumes the client-stage and collection allowance. A permitted audit taking 300 seconds leaves 3060 of the existing 3360 seconds for F06 C0, and longer permitted audits can cut off otherwise in-bound stages or their evidence collection. Evidence: lines 822-828 set deadline before collection; incoming main run_batch sets it afterward; a valid synthetic batch/all run with a 300-second clock advance in the collector reproduced a 3060-second first-F06 budget. The scientific batch tests use an instantaneous audit and do not exercise this integration boundary.

Findings: 2 at P0 or P1

Pre-existing (not blocking):

None identified within the focused and affected context. Existing unchanged-file documentation warnings remain covered by the receipt's blanket disposition.

## Budget and handoff

The applicable focused scope is complete, but the cold-review gate is unresolved because of the two P1 findings. This is the final allowed focused round: cumulative use is 2/2 comprehensive and 2/2 focused. Under AGENTS.md Before opening a pull request rule 8, further required review of corrections would exceed the budget. The coordinator must preserve unpublished work under rule 12 and hand off to Hari with these findings, frozen revisions and exhausted budget, requesting a decision on correction/review continuation. This report supplies no additional review allowance and authorizes no publication or live action.
```

### Required handoff

The reviewed merge remains unpublished and unpushed. Under AGENTS.md Before opening a pull request rule 8, no further correction review can be initiated within the current budget. The coordinator must hand off to Hari, preserving the findings, reviewed revisions, full reports and exhausted 2/2 comprehensive plus 2/2 focused counts, and request an explicit decision authorizing the smallest continuation: (1) make the frozen scientific selector accept the canonical selector saved by Phase.prepare_batch, with regressions that run real preparation through the frozen selection helper and both execution entrypoints for all, B08, F06, B08,F06 and B08,E01,F06; (2) compute the F06 scientific configuration reservation after the permission collector, with a synthetic clock-advance regression proving the full reserved stage budget remains available; then (3) obtain Hari's explicit authorization for one focused correction review beyond the exhausted budget before proceeding to hosted CI/review and the protected-path merge. No implementation correction, push, review, or publication is authorized by this handoff itself.

The coordinator also identified a preservation check for continuation: verify that main's full-smoke report retains its selected_items field in the current report path. The focused reviewer did not list this as a separate finding; check it as part of preserving main's output contract while correcting the two accepted findings.

The documentation scan was rerun after adding this focused report: `npm run check:docs` exited 0, found no issue in the changed receipt, and reported the same eight warnings confined to four unchanged files.

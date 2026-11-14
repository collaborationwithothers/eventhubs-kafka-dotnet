# Verifier mutation runtime specification

Bring the complete verifier mutation workflow to a 30-minute dispatch-to-result goal without changing what a passing mutation result proves. Qualify future changes to its workload before merge, using bounded profiling and explicit estimates. This specification is for GPT-6.1 Terra at high effort; it does not authorize this planning session to implement or run a mutation campaign.

Revision: 2. Base main: `5ac85af17f4b7dbc1a1396932fb3554d363752fb`.
Issue: [474](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474).
Plan branch: `codex/474-plan`.

Changes from revision 1: bind each shard estimate to its exact generated Stryker configuration; include runtime-read limits and profile-directory membership in freshness checks. These address the [first independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6877125630) and require a fresh check of this revision.

## 1. Goal and authority

Authority, in order:

1. The issue body: reduce current runtime and prevent regression; retain complete fresh coverage at the merged commit.
2. [Cancellation decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6876575032): the cancelled run supplies no mutation acceptance.
3. [Interactive design decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6876901367): Hari's answers from the grilling session, including the 30-minute shard ceiling, bounded profiling, blocking qualification, and unchanged mutation method.

The last decision records shared understanding, not approval of this plan at its commit SHA. Obtain the approval required by `docs/agents/factory.md`, "Item lifecycle", before implementation. The existing tool assignment is [Tool: Codex](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6876574470). The label alone does not approve an unwritten plan.

This is tooling work, not a scenario experiment. No hypothesis, expected-outcome file, scientific verdict, live fault, Azure deployment, or five-repeat scenario run is introduced. The parent issue is 347. Its broad control-session authority is not adopted here; it grants this implementing session no extra merge or dispatch authority.

Success has separate conditions:

- Mutation acceptance: the existing complete fresh main-commit measurement and break score pass.
- Performance acceptance: one complete main workflow attempt, from creation to completion including queues, setup, uploads and aggregation, takes at most 1800 seconds. This is a policy goal, not a measured capability.
- Prevention: the required pre-merge verification fails for missing, stale or oversized qualification when a change affects this mutation workload. Unrelated changes are not blocked.

A complete passing mutation measurement that exceeds 1800 seconds remains valid under the existing publication gate. It does not complete this issue. A local probe or estimate never satisfies mutation acceptance.

## 2. Scope and ordered pull requests

Use one plan with two ordered pull requests. Both are reviewable and green independently. Do not create child issues unless Hari directs it.

### PR 1: bounded runtime qualification tooling

Add the diagnostic profile, evidence/model reader, input fingerprinting, estimate validator and regression tests. Existing production shard selection and required checks remain unchanged in this PR. Provide the new commands for PR 2. Do not run the bounded profiling until the commands needed for it are ready; count every real probe across both PRs against section 6's shared budget.

Allowed files:

- `tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py` (new CLI/module).
- `tests/Parchwell.Verifier.Tests/Mutation/test_c5_runtime.py` (new tests).
- `tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py`, only diagnostic reuse needed by the new module; preserve existing public commands and production behavior.
- `tests/Parchwell.Verifier.Tests/Mutation/test_c5_workflow.py`, only corresponding diagnostic regressions.
- `docs/operations/verifier-mutation.md`, a new focused section describing these commands; leave historical records intact.
- `items/474/state.md`, created by the implementer under the factory's item-state rule.

Size estimate, 2026-11-14: 500-900 changed lines in 4-6 files. Basis: one Python CLI/model module, its behavioral tests, limited reuse and operator documentation. This is not a limit.

### PR 2: measured rebalance and required prevention

After PR 1 merges, reuse the current evidence, execute only the approved bounded measurements still needed, and rebalance all configured targets. Wire the qualification validator into the existing required `.NET verification` check. This couples the new merge requirement to a layout qualified under that requirement; an initial failing layout is never grandfathered as qualified.

Allowed files:

- The runtime module and tests from PR 1, for integration fixes within the same contracts.
- `tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py`: target part counts, UTF-16 cuts, layout/model validation and recorded runtime advisories.
- `tests/Parchwell.Verifier.Tests/Mutation/test_c5_workflow.py`: exact layout assertions and ownership/regression cases. Existing judging and completeness assertions stay enforced.
- `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` (new authored configuration; schema in section 4).
- `.github/workflows/verifier-c5-mutation.yml`: matrix layout only; optionally one artifact step for the separately generated performance report. Keep its dispatch trigger, permissions, runner type, fail-fast behavior and emergency execution limits.
- `.github/workflows/dotnet-verification.yml`: a qualification step in the existing final aggregate job, before its successful completion, using the PR base and checked-out head. Keep triggers and check names.
- Byte-identical copies under `docs/operations/workflows/` if a copy of either changed workflow exists when implementation starts. Neither has a copy at this plan's base; adding copies is not required.
- `docs/operations/verifier-mutation.md`, the current-method/operator section and the new qualification section; no historical campaign rewrite.
- `docs/agents/factory.md`, "Evidence pack": add the pre-merge runtime-qualification rule with a pointer to its single operational definition; preserve the existing post-merge mutation-acceptance paragraph.
- `items/474/state.md`.

Size estimate, 2026-11-14: 500-1100 changed lines in 8-10 files. Basis: layout/configuration data, integration, predicate tests and focused documentation. Do not reduce coverage to meet this estimate.

Paths and behaviors excluded from both PRs:

- Production C# and scenario code; ordinary C# judging tests and fixtures; mutation target list and Stryker configuration; package/tool pins; new projects, packages or assembly references.
- `AGENTS.md`, skills, `results/`, `expected.yaml`, verdicts, infrastructure, Azure credentials and paid/larger runners.
- Workflow trigger changes, coverage-mode changes, baseline/since reuse, test-set pruning, assertion relaxation, retry-to-pass behavior and a separate 20-minute shard limit.

## 3. Verified starting evidence

Source citations below refer to the base main SHA unless a different pinned source is linked. Facts were checked on 2026-11-14; timings describe the cited run, not a prediction.

- `c5_workflow.py:26-62` declares 24 targets and measured cuts for older targets. The seven final-judgement additions are singletons. Its `BASE_CONFIG` retains coverage off and break 60. The committed Stryker version is 5.0.0 in `.config/dotnet-tools.json`.
- [Run 43575009980](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43575009980), Actions jobs REST API: 68 mutation jobs; 63 succeeded and five were cancelled. The 61 older jobs alone consumed 183253 job-seconds; 47 exceeded 1800 seconds. These values were computed from each job's `started_at` and `completed_at`, not test-duration sums. Rebalancing only new targets cannot meet the goal.
- All 68 mutation jobs started between 08:16:23 and 08:16:25 UTC, 4-6 seconds after run creation. This proves observed availability for that dispatch, not guaranteed account concurrency.
- The five cancelled artifacts contain `command.log` and projected source/configuration but no native `mutation-report.json` or wrapper `receipt.json`. Artifact IDs: repeat-evidence 14111653842, classification 14111723636, projection-codec 14111558997, judge 14111392757, registrations 14111204685. Each ZIP was inspected. Partial test-start/completion pairs are diagnostic timing evidence only.
- `c5_workflow.py:490-524` kills the process group on timeout/interruption. Pinned [JsonReporter.cs](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/Reporters/Json/JsonReporter.cs) writes JSON at `OnAllMutantsTested`; its per-mutant callbacks do not write a checkpoint. Do not expect a native partial report after a kill.
- A completed positive-span `local-check` can retain the whole assigned file's mutant geometry while testing the selected span. This is established from pinned [BroadcastMutantFilter](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/MutantFilters/BroadcastMutantFilter.cs) and [SourceFile](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/Reporters/Json/SourceFiles/SourceFile.cs). It is not a newly measured probe result. Filtered mutants retain geometry; compile errors and other prior dispositions require separate treatment. A placeholder file or missing report is not a full inventory.
- Existing `local-check` does not execute production static-field isolation. Its outcomes cannot replace the acceptance runner's isolated invocations.
- Mutation-included tests read runtime data outside their project files: `Host/VerifyInputCapacityTests.cs:27-28`, `Window/ConsumedRecordBudgetTests.cs:165-167` and `Host/B10SweepHandlerTests.cs:310-312` read `docs/operations/a01-runtime-limits.json`. `Scenario/FileSystemScenarioSourceTests.cs:27-29` copies every top-level `scenarios/profiles/*.yaml` file. These are execution inputs, including directory membership, not documentation-only changes.
- `items/397/investigation.md:77-115,184-194` records the earlier expensive-test measurements and deliberate coverage-off workaround. The new profile must measure current cost; historical summed test durations cannot be subtracted from current wall time.
- Pinned [configuration documentation](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/docs/configuration.md) documents per-test selection and experimental baseline reuse, including possible false positives/negatives. Reusing old statuses changes fresh-run acceptance; enabling selection changes which tests execute for each mutant. Both are excluded by the adopted decision, not silently substituted for sharding.
- GitHub's [Actions limits](https://docs.github.com/en/actions/reference/limits) document a 256-job matrix limit. Keep at most 256 mutation matrix entries; other independent workflow jobs are not matrix entries. More shards can increase repeated setup and total compute.

## 4. External contracts

### Existing contracts retained

Retain the CLI and artifact interfaces of `c5_workflow.py run-shard`, `aggregate`, `local-check` and `member-boundaries`. Preserve native report format, current receipt keys, source/projection binding, static isolation, raw population reconciliation, positive target denominators, mutation summary fields and score formula. New layouts retain existing target keys and `KEY-pN`/`KEY-crossing` naming; an unchanged singleton retains its key. Per-target part counts may change.

The 16200-second child bound and 300-minute hosted-job emergency bound remain execution safeguards. The new 1800-second planning and performance budgets do not automatically cancel an otherwise valid measurement or change these safeguards.

### New CLI

From the repository root:

```text
python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py profile --output-dir DIR --timeout 600
python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check [--base BASE_SHA] --head HEAD_SHA --qualification FILE
python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py performance --run-json FILE --jobs-json FILE --output-dir DIR
```

`profile` runs the mutation-only test population once, Release/net10.0, locked restore, pinned SDK and `ParchwellVerifierMutationOnly=true`, with no tier/test-case filter. Use the test invocation already represented by `scripts/verify-dotnet.sh:486-498`, adding the existing runner's `--report-xunit-xml --report-xunit-xml-filename results.xml` reporting flags from that script's normal test invocation. Retain command/log, source and machine facts, XML test cases/durations, total wall time and termination status outside the repository. Timeout includes build and tests. Existing nonempty output is an error. No automatic retry, partial score or fabricated test report.

`check` is offline, starts no mutation/test process and makes no network requests. Exit 0: qualified or not applicable. Exit 1: rejected input/qualification. With `--base`, evaluate the PR diff; without it, validate the whole current head with no not-applicable bypass. Its final stdout line is one JSON object: `{"status":"qualified|not_applicable|rejected","reasons":["CODE",...]}`. Rejection codes, in stable sorted order: `invalid_input`, `missing_qualification`, `stale_inputs`, `missing_measurement`, `invalid_estimate`, `shard_over_budget`, `matrix_over_capacity`, `layout_mismatch`. Malformed JSON, unsupported schema and invalid git refs are `invalid_input`, never not applicable.

`performance` reads one saved Actions REST run object and one `{"jobs":[...]}` object combining all explicit job pages for its declared attempt, starts no network request and emits `performance.json` with `run_id`, `run_attempt`, `head_sha`, `budget_seconds:1800`, `workflow_elapsed_seconds`, `mutation_job_seconds`, `peak_mutation_jobs`, and `status` (`met`, `missed`, `incomplete`). Wall time is run `created_at` to run `updated_at` for a completed attempt, cross-checked against the latest job completion; record any API timestamp discrepancy as incomplete rather than silently use a too-early result. Sum mutation jobs' start-to-completion intervals for compute, without adding overlapping test-case durations. A cancelled/missing/running job or noncompleted run yields incomplete and no claimed performance pass. Multiple attempts must be identified explicitly, not mixed. Exit 0 for met, 1 for missed/incomplete; this command's exit is not the mutation aggregator's exit and must not turn a scientifically passing hosted mutation job red.

### Authored qualification configuration

`c5-runtime-qualification.json` is reviewed configuration, not committed native reports or program output. Schema version 1 has exactly these top-level keys:

- `schema_version`: integer 1.
- `budget_seconds`: integer 1800.
- `shared_inputs`: map of repository-relative file paths to lowercase SHA-256 values.
- `targets`: map whose keys exactly equal the committed mutation target paths. Each value has `source_sha256`, `evidence`, and `shards`.

Each `evidence` entry has `source_commit`, `locator`, `sha256`, `kind` (`hosted_complete`, `local_probe`, `partial_timing`, or `suite_profile`), and `note`. Locators identify retained evidence or the PR's authored measurement notes, including raw-file hashes and reproducible commands. A locator alone is not proof of a complete report. Reusing complete old geometry requires the target bytes to match. Reusing timings also requires comparable shared inputs and an explicit hardware/calibration note. Partial timing may contribute a rate but cannot stand for full geometry, static outcomes or acceptance. The reviewer checks the raw source for the measurements it relies on; CI checks freshness, arithmetic and declared budgets rather than authenticating a local measurement.

Each `shards` entry is keyed by the production shard ID and has `selection_sha256`, `main_mutants`, `main_work_seconds`, `parallelism`, `setup_seconds`, `isolated_seconds`, `estimated_seconds`, and `basis`. Counts are nonnegative integers; parallelism is a positive measured integer, bounded by the production command's observed workers; all seconds are finite nonnegative numbers. `estimated_seconds` must equal `ceil(setup_seconds + main_work_seconds / parallelism + isolated_seconds)`. `basis` states measurement locators, how untested mutants were weighted, and any local-to-hosted calibration. Positive-work shards cannot use zero setup/work without measured support. Reject booleans as numbers, NaN, Infinity, negative values and arbitrary downward rounding.

`selection_sha256` is the lowercase SHA-256 of the exact production `shard_config(shard_id, original_target_bytes)` result, serialized by `json.dumps(config, sort_keys=True, separators=(',', ':'), ensure_ascii=True).encode('ascii')`. Regenerate that configuration from the current head before comparison. This binds estimates to positive spans, primary cuts, crossing exclusions, the source-end clamp, static-field exclusions and other Stryker configuration. Source bytes are independently bound by the target hash. A cut change with identical shard IDs/counts and old estimates must fail `layout_mismatch` until the new selection hash and its reassessed estimates are supplied. Static isolated ownership remains part of the existing geometry proof; this hash does not replace it.

The configuration is authored from reviewed evidence and contains curated estimates, not a copied/generated full inventory. Raw reports, command logs, generated plans and benchmark output stay in temporary/artifact storage. Record otherwise unavailable measurement facts in PR implementation notes under `docs/agents/pull-requests.md`, rule 8.

### Invalidation boundary

Build the relevant shared-input set from these explicit roots and dependencies:

- All C# and project/build inputs of `Parchwell.Verifier`, `Parchwell.Kafka` and `Parchwell.Scenarios`, plus project references subsequently introduced into that closure.
- The mutation-included verifier test sources and their linked/copied fixtures, including `tests/fixtures/ProbeV2MockCluster.cs` and `tests/fixtures/verifier-v1/`. Honor the current mutation-only exclusions; unknown MSBuild inclusion shapes fail as unsupported input instead of silently omitting a dependency.
- Data those tests read directly at runtime, regardless of directory: explicitly `docs/operations/a01-runtime-limits.json`, the scenario YAML read by `HostEvidenceBuilder` and `FileSystemScenarioSourceTests`, and every top-level `scenarios/profiles/*.yaml` file. Include the sorted member set and each member's bytes so additions/deletions invalidate the record even without a project-file change. A prose file under `docs/` is excluded only when it is not a build/execution input; the runtime-limits JSON is always included. The current runtime data roots are supported inputs, not a demand for a general static analyzer. When changed test code introduces a runtime data root outside them, its source change invalidates shared qualification and the supported dependency set must be explicitly reassessed before renewal; silently retaining the old closure is invalid.
- Root .NET SDK/build/package/restore inputs, Stryker pin/configuration, projection/shard execution code, and mutation-workflow execution steps. Track file additions/deletions as well as byte changes. A mixed project file remains relevant even when its changed property looks production-only; use conservative invalidation rather than a general MSBuild semantic solver. Include any scenario/profile data copied by those projects and used by this test population.

Configured target C# files get their own per-target source hashes rather than being duplicated in `shared_inputs`. Qualification data itself and declarative matrix/cut data are checked against the per-shard `selection_sha256` bindings, current layout and arithmetic, not included in a self-referential content hash. For a file mixing layout constants and execution logic, compare the execution portion independently or conservatively refresh qualification; do not exempt its executable changes. Preserve an explicit test for that separation.

Exclude documentation, item notes, unrelated services/tests, and separate production-only container/deployment files that cannot feed the mutation build or execution. A target byte change requires that target's geometry/rate reassessment; a relevant shared-input change invalidates timing qualification for all targets until reassessed. This is scope based, not an assertion that every verifier edit makes tests slower.

## 5. Numbered requirements and enforced checks

R1. WHEN the profile command completes, THEN it records the current full mutation-only test population, failures/skips and per-test durations plus wall time. Compare costly new final-judgement test classes with the existing capacity cases, distinguishing current cost from causal before/after slowdown. A historical duration is not a current measurement. Failure, timeout or missing XML is an explicit gap, never permission to omit tests.

R2. WHEN a probe or retained native report is used for geometry, THEN bind it to the unchanged assigned target bytes and pinned configuration. Retain every native identity and distinguish mutation-filter Ignored from non-filter Ignored/CompileError. Validate UTF-16 coordinates, original source and reporter inclusion. An interrupted probe is timing-only evidence and consumes its budget slot.

R3. WHEN deriving a layout, THEN account for every target, every primary, every crossing complement and all isolated static invocations. Use complete measured identity timings where available. For untested eligible identities, use at least the maximum comparable observed per-mutant duration for that target; if none exists, obtain a permitted probe or record missing evidence. Account separately for instrumentation/build/initial tests/reporting and each isolated invocation. Do not divide sequential isolated work by main-run worker count. Local hardware must be recorded; calibrate with comparable hosted data where available, otherwise explicitly retain the uncertainty in the estimate and PR notes.

R4. WHEN qualified layout data is checked, THEN every shard estimate must be at most 1800 seconds, the matrix must contain at most 256 unique entries, and the Python layout, configuration and workflow matrix must match exactly. Compare each `selection_sha256` to the current canonical generated shard configuration; matching names/counts alone do not establish matching cuts. Minimize job count subject to those constraints using deterministic measured cuts; no required 1200-second ceiling or fixed 600-second reserve. A source-length-only split with no runtime basis is not qualified.

R5. WHEN a source-node mutation is reconciled across split shards, THEN it has exactly one tested owner or its original global final exclusion, just as before. Preserve disconnected crossing complements, source-end report inclusion, whole static-field ownership and existing isolated invocations. A large indivisible/crossing owner that cannot meet the budget is an explicit design gap, not a reason to omit it. Keep all current ownership, score, projection and malformed-evidence regressions.

R6. WHEN the PR changes no relevant workload or layout inputs relative to its base, THEN `check` returns not_applicable without requiring raw profiling artifacts. WHEN target or relevant shared inputs/layout change, THEN it requires complete current authored qualification for the affected targets and validates the whole resulting layout. Missing/stale evidence, extra/missing shards, invalid input shapes, unmatched hashes or over-budget estimates reject the PR. A new target cannot silently inherit singleton status.

R7. WHEN hosted PR verification runs, THEN the qualification command is part of the existing required `.NET verification` aggregate job, so a rejection fails the already-required check. Compare to `github.event.pull_request.base.sha` and the checked-out head; fetch the needed commits safely with read-only permissions. On push/main or manual runs, omit `--base` and validate current qualification directly rather than using an empty-diff bypass. Run the new runtime helper tests and retained shard-helper regressions in this step before invoking the validator. Preserve workflow triggers, existing tests and required-check names. No repository ruleset update or additional privileged token is needed.

R8. WHEN the main mutation workflow measures the layout, THEN every configured target is tested freshly with the current method and full existing mutation population. Preserve the current mutation acceptance and exit behavior. Set the recorded-only rebalance advisory threshold to 1800 seconds; expose a speed miss without rewriting `measurement_complete` or `break_passed`.

R9. WHEN performance is reported, THEN include the full workflow wall time, queue observations, mutation job-time sum, peak observed concurrency, commit, run and attempt. A shard ceiling does not guarantee total wall time. Report performance missed if the full run exceeds 1800 seconds even when every shard is individually below it. The issue remains open after a speed miss.

R10. WHEN a change would require another mutation method, more runners than existing capacity, over 256 matrix entries, altered tests/assertions, budget expansion or scope/contract changes, THEN preserve the evidence and apply section 10's stop rule. Never hide the limitation in a post-merge follow-up after claiming qualification passed.

## 6. Bounded procedure and completion criteria

These are policy work limits adopted in the interactive decision, not runtime estimates. Keep an authored running tally in PR notes, with commands, source SHA, termination and slots remaining. A new session or PR does not replenish this issue's measurement allowance.

1. Reuse evidence first. Read the metadata of the 68 shard artifacts of run 43575009980 once; inspect at most those 68 primary evidence ZIPs, plus that run's aggregate artifact if available. Do not download separate duplicate Stryker-log artifacts unless an inspected primary explicitly lacks a required diagnostic, in which case record the gap and continue. Bind reusable inputs to the candidate's relevant bytes; code changes between the run SHA and base must be inspected. Done when reusable geometry/timing is indexed and remaining gaps are named. Artifact reads for writing/checking this plan are authority research, not mutation executions.
2. Run at most one current mutation-only test-suite profile, total timeout 600 seconds including build. Done with a complete test-duration report or a named measurement gap. On timeout/failure, record the gap and continue only to independent evidence analysis; no automatic repeat.
3. Run at most 24 real `local-check` invocations, each timeout 600 seconds including setup. Pick small, nonempty spans using existing member boundaries and evidence; use completed native reports for whole-file geometry. No full unsplit mutation campaign is a substitute for this budget. No automatic retries. Done when the missing rates/geometry are measured or all slots are consumed. A failed/interrupted invocation counts. Do not mistake its static outcomes for production isolation results.
4. Derive and validate one candidate full layout. Estimate every owner, including complements and isolated work; account for potentially queued waves instead of assuming that 256 entries means 256 available runners. Concurrency beyond that observed is an explicit estimate, not a promise. Done when the candidate fits the declared ceilings and has auditable estimates, or a gap is recorded. No extra mutation execution is permitted by finding a difficult cut.
5. Exhaustion action for steps 1-4: `record the gap and continue` to offline tests, diagnostics and a handoff only. If complete qualified data cannot be produced within these bounds, do not merge PR 2 with a bypass. Save work under AGENTS.md rule 12 and hand off under rule 11. Implementing the diagnostic tooling alone does not complete the runtime fix.
6. After PR 2 merges, hand the main-only validation procedure to the control session/Hari. This plan grants no workflow dispatch authorization to the implementing session. The owner of an authorized complete run exports the run and jobs REST JSON with explicit pages/attempts, invokes `performance`, and records acceptance. There is no automatic campaign retry. A failed/incomplete run blocks mutation acceptance as before; a slow complete pass preserves its scientific validity and records performance missed.

Offline tests over the fixed configured target set and generated synthetic fixtures do not consume mutation-probe slots. Do not expand into survivor fixing, equivalent-mutant adjudication, repeated performance campaigns or optimizing C# fixtures. If survivor work becomes necessary, measure its count and apply AGENTS.md rule 11; this plan authorizes no survivor-fix batch.

## 7. Test map

Use behavioral tests in `test_c5_runtime.py` and retain existing `test_c5_workflow.py` tests. These fixtures are synthetic and their numbers are test inputs, not measurements. Each row states literal expectations; keep unrelated fields valid so they cannot mask the predicate under test.

| Requirement | Named test and inputs | Required result |
| --- | --- | --- |
| R1 | `profile_population_and_wall_time`: complete XML with case durations 8 and 8, wall 10, no failures/skips | 2 cases, sum 16, wall 10; never claim 6 seconds can be subtracted from wall |
| R1 | `profile_failure_is_not_measurement`: failing test, missing XML, timeout at 600; separate cases | explicit incomplete diagnostic; zero automatic retries; no fabricated counts |
| R2 | `probe_geometry_is_not_outcomes`: 3 native identities, one selected Killed, one filter-Ignored, one CompileError | geometry includes all 3; only 1 measured execution; no acceptance claim |
| R2 | `reject_placeholder_or_mismatched_source`: placeholder source/zero native inventory versus expected real source; wrong hash | reject as geometry evidence |
| R3 | `estimate_main_and_isolated_work`: setup 100, main work 2000, parallelism 2, isolated 300 | estimated seconds exactly 1400; not 1250 |
| R3 | `unobserved_mutant_weight`: comparable measured durations 20 and 40; one untested identity | unknown gets at least 40, not 20 or 30; missing comparable rates is missing_measurement |
| R3 | `local_rate_requires_calibration_note`: local source-bound probe with no hosted comparison | explicit hardware/uncertainty basis required; cannot label it a measured hosted duration |
| R4 | `shard_budget_boundary`: otherwise valid estimates 1799, 1800, 1801 | first two qualify; third rejects with shard_over_budget |
| R4 | `matrix_capacity_boundary`: otherwise valid unique layouts 255, 256, 257 | first two qualify; third rejects with matrix_over_capacity |
| R4 | `no_twenty_minute_gate`: estimate 1500 | qualifies; no hidden 1200-second restriction |
| R4,R6 | `same_ids_changed_cut`: qualify a two-primary synthetic layout with cut 10, then move it to 20 with identical source bytes, shard IDs/counts and old estimates | old selection hashes reject with layout_mismatch; records bound to the new generated configurations must also pass reassessed estimate arithmetic and budget checks |
| R4 | `estimate_arithmetic_and_numeric_types`: inconsistent ceil; boolean; negative; NaN/Infinity; zero workers | invalid_estimate; no rounding down |
| R4,R5 | `layout_ownership`: node wholly in left/right primary, touching cut, crossing one/multiple cuts, source-end node, equal-span distinct identities, static region | exactly one eligible owner per identity; static region never split; crossing report includes real source |
| R4,R5 | Existing UTF-16/BOM/astral and clamped crossing-exclusion regression fixtures | identical native population/score reconciliation, including zero-tested complements |
| R6 | `irrelevant_change_does_not_profile`: README/item-note edit and unrelated loadgen-only C# edit | not_applicable; no measurement/process starts |
| R6 | `changed_target_invalidates_only_target`: two valid target records, one target byte changed | stale_inputs until affected target refreshed; unchanged geometry may be reused with valid shared inputs |
| R6 | `shared_input_invalidates_all_rates`: included test/helper, linked fixture, relevant shared project/build/package input; additions/deletions separately | stale_inputs for all affected timing records |
| R6 | `runtime_limits_json_is_execution_data`: change only docs/operations/a01-runtime-limits.json with otherwise valid inputs | stale_inputs, never not_applicable despite its docs path |
| R6 | `runtime_profile_directory_membership`: add, change or delete a top-level scenarios/profiles/*.yaml file with no project-file edit; separate cases | stale_inputs for all affected timing records; ordinary unrelated prose remains not_applicable |
| R6 | `mutation_excluded_test_and_unknown_inclusion`: excluded component-test body versus unsupported new inclusion form | excluded body alone not_applicable; unknown inclusion invalid_input |
| R6 | `new_target_has_no_default_pass`: configured target added with no qualification | missing_qualification; no singleton fallback |
| R6 | `layout_and_execution_hash_separation`: pure cuts change with matching estimates; then executable shard-runner edit with same cuts | first checked against layout; second invalidates execution evidence |
| R6 | `malformed_inputs_precede_scope_bypass`: docs-only diff plus malformed schema/ref | invalid_input, not not_applicable |
| R7 | `required_check_propagates_rejection`: simulated PR base/head and failed checker | existing .NET verification cannot succeed; no trigger/check-name change |
| R7 | `main_validates_without_diff`: manual/main invocation with stale qualification | rejection; no empty-diff bypass |
| R8 | Existing raw-score, source binding, missing report/owner and mutation-population tests | unchanged scientific verdicts; administrative expected shard count updates to actual new layout |
| R9 | `performance_boundary`: completed attempt lasting 1799, 1800, 1801; mutation pass in all cases | met, met, missed; mutation pass unchanged |
| R9 | `queue_and_aggregation_count`: latest shard finishes at 1790, workflow finishes at 1810 | workflow 1810 and missed, regardless of individual shard ceilings |
| R9 | `compute_and_peak_not_wall_sum`: jobs [0,10] and [5,15], whole run [0,20] | job seconds 20, peak 2, workflow wall 20 |
| R9 | `cancelled_missing_or_mixed_attempt`: cancelled job, missing completion, mixed attempt IDs, final API timestamp before last job | incomplete, no performance pass |
| R10 | `bounded_launcher`: exhausted slots; timed-out invocation; existing output directory | no retry/new child launch; failed probe counted; output never overwritten |

Standing fixture rule: if a new administrative check rejects an existing fixture, change only the fixture to satisfy that check while keeping its assertions and exercised boundary; or, if invalid input is the fixture's purpose, keep it and set its expected result to this table's rejection. List each change and forcing check in PR notes. This never permits removing a case, relaxing an assertion or changing scientific inputs.

## 8. Acceptance commands and evidence

PR 1 focused verification:

```bash
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_runtime.py' -v
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_workflow.py' -v
npm run check:docs
```

These must cover the synthetic predicates without real mutation subprocesses. Use an injected fake child only for launch/termination contract tests. No benchmark saving is inferred from helper tests.

PR 2 additionally runs:

```bash
python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base BASE_SHA --head HEAD_SHA --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json
```

Use actual base/head refs and the current authored configuration. Required evidence: qualified status, exact matrix/layout equality, every current target represented, estimates within budget, fresh relevant input bindings and complete ownership proof. The validator must also be exercised with the section 7 rejected/not-applicable fixtures.

For both PRs follow `docs/agents/pull-requests.md`, "Verification gates", in its defined order. The applicable pre-PR local command is `./scripts/verify-dotnet.sh --tier sandbox`; label its exclusion as partial. Required final-head hosted `.NET verification`, cold-review budget and hosted Codex review remain mandatory. This spec supplies no gate waiver and creates no authored receipt requirement.

Main-run acceptance is a later operational step, not a local acceptance command. It requires the current native summary to have `measurement_complete:true` and `break_passed:true` on a main commit containing the changes, plus a separate `performance` result of met for the complete workflow attempt. A slow pass leaves only performance unresolved; missing mutation evidence leaves publication blocked. Record the source/run/attempt and links in the originating issue.

## 9. Assumptions, strictness and sources

Deployment assumptions: no live deployment or connection is introduced, so fresh/open-connection and idle/load distinctions are not applicable. Standard GitHub runner availability is observed at dispatch time and cannot be guaranteed offline. Local-to-hosted speed equivalence is not assumed. The adopted decision accepts bounded estimates followed by actual main validation, with a handoff if existing capacity cannot meet the goal. No offline test proves a 30-minute hosted duration.

This plan is stricter than the earlier target-addition process in exactly the adopted areas: a pre-merge qualification requirement, stale-input invalidation, an 1800-second estimated shard ceiling, and explicit full-run performance acceptance. It preserves the publication gate's meaning and ordinary test assertions. A slow run does not become scientifically invalid.

The effort that scales with runtime counts is bounded in section 6. The layout checks cover the current configured set within the 256-entry ceiling; unknown input geometry or unsupported dependency forms are explicit rejections. This work has no unimplemented external producer contract: Actions JSON is read from actual REST responses, while the test map includes representative saved synthetic fixtures; native Stryker geometry behavior is pinned to the official source cited in section 3. Test actual saved inputs as well as the synthetic fixtures before claiming qualification.

Authority and source files:

- `AGENTS.md`, GOVERNANCE and "Before opening a pull request".
- `docs/agents/factory.md`, "Authority reads", "Item lifecycle", "Item state file", "Evidence pack".
- `docs/agents/pull-requests.md`, rules 1, 2, 4, 8 and "Verification gates".
- `docs/engineering/dotnet.md`, "Testing".
- `tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py`, `c5_projection.py`, current helper tests and `stryker-c5-config.json`.
- `.github/workflows/verifier-c5-mutation.yml` and `.github/workflows/dotnet-verification.yml`.
- `src/Parchwell.Verifier/Parchwell.Verifier.csproj` and the verifier test project: dependency and mutation-only population boundaries.
- `items/397/investigation.md` and `items/397/rebalance.md`: historical test cost and ownership/report-inclusion proof. Do not reuse their historical counts as current measurements.
- Official pinned Stryker and GitHub sources linked in section 3. They were read through HTTPS/source inspection, not recalled from memory. No Azure or Kafka capability claim is added by this work.

## 10. Open questions and stop rule

Open questions for Hari: None about the chosen policy. Exact new cuts, rates, number of jobs and achieved hosted duration are measurement outputs, not numbers this planning session may invent. If the prescribed evidence cannot produce a feasible layout, that becomes a named gap requiring a later decision; it is not permission to change the method.

Stop when a contract, requirement, scope, test population/assertion, coverage mode, pin, budget or safety property would need to change. Apply AGENTS.md GOVERNANCE "Handoffs" and "Before opening a pull request" rules 11-13, and `docs/agents/pull-requests.md` rule 8's cumulative review limits. Save unfinished work as those rules prescribe. A resumed session inherits completed measurements and review counts. Protected workflow changes are merged by Hari. No workflow dispatch is authorized here.

## 11. Terra handoff

Select model `gpt-6.1-terra` with reasoning effort `high`. Give it this prompt after the plan's recorded approval and independent check:

```text
Use the implement-plan skill on issue 474 in collaborationwithothers/eventhubs-kafka-dotnet.
Use GPT-6.1 Terra at high effort. Read the approved contract-grade plan pointer and
items/474/plan-mutation-runtime.md at its approved commit, plus recognized later
decisions and the newest applicable plan check. Follow docs/agents/factory.md
"Item lifecycle" and AGENTS.md in full. Select the earliest unmerged PR in the
approved ordered set; implement only that PR. Preserve the shared measurement
budget across sessions and both PRs. Do not reuse mutation results, change coverage
mode, alter tests/assertions, upgrade tools, dispatch workflows or merge protected
paths. If evidence cannot produce a qualified layout within the adopted limits,
preserve the evidence and hand off under the plan's stop rule.
```

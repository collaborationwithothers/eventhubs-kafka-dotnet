# Verifier mutation: pre-merge sizing and reuse of unchanged results

This plan settles the two open questions of issue 474. Question 1 asks how a new or changed mutation target gets a shard layout that holds before merge. Question 3 asks whether Stryker.NET can skip targets that have not changed. The plan recommends a measured, hosted baseline for pre-merge sizing (PR A). For question 3 it recommends no incremental mode. PR B is an optional gate correction that Hari may adopt or reject.

Revision: 1. Base main: `22bcce4b53f4031ec467c2fd6d2b2ff314291fb0`.
Issue: [474](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474).
Plan branch: `claude/474-plan`. Plan file: `items/474/plan-sizing-and-reuse.md`.
Split name: `sizing-and-reuse`, an ordered set of PR A `hosted-baseline-qualification` and PR B `mutation-acceptance-inputs`.

## 1. Goal and authority

Authority, in creation order:

1. The [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474), questions 1 and 3, and its constraints. A passing run must still cover every mutated file at the merged commit unless Hari decides otherwise. No test or assertion is loosened.
2. [Design decision 6876901367](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6876901367). Point 1 keeps a 30-minute shard sizing ceiling and rejects a 20-minute one. Point 2 preserves fresh mutation testing and excludes "baseline-result reuse" in that fix. Point 4 requires bounded profiling and conservative estimates before merge. Point 5 makes missing or stale qualification block an affecting pull request.
3. [Timing decision 6877623126](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6877623126): every shard estimate is invalidated when any configured target or relevant shared input changes.
4. [Static-owner decision 6880280289](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6880280289) and [clarification 6880342017](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6880342017): two source-bound minor overruns are accepted.
5. [Decision 6880732047](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6880732047): 2400 seconds is the practical workflow goal for the initial live qualification.
6. Later rebalance decisions up to [6887981346](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6887981346). They set the current 256-job layout.
7. [Decision 6893688070](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6893688070), 2026-11-15. It scopes this plan to questions 1 and 3. It states that the wall time "is fixed for now". It starts this work in Claude Code although the issue body named Codex.

The approved [mutation-runtime plan, revision 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/1cdbcdf0407c1b897c7bcec29ef97d261109b473/items/474/plan-mutation-runtime.md) is the item plan this plan builds on. Its PR 1 and PR 2 have merged. This plan replaces none of its decisions silently. Each change to an adopted decision is under section 10 for Hari.

This is tooling and governance work. It adds no hypothesis, expected outcome, verdict, Azure deployment or live fault. It dispatches no workflow.

## 2. What the evidence shows

All figures were computed by script on 2026-11-15 from the Actions REST API (run object and three pages of attempt-1 jobs) and the committed qualification file at the base commit. The scripts read `started_at` and `completed_at` per job.

- Run [43670258319](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43670258319), attempt 1, head `1e01aea24a457fffe0dc970703af2de608d524e3`, concluded `success`. Created 02:59:13Z, updated 03:42:30Z: 2597 seconds. 257 jobs, of which 256 are mutation jobs. All 256 mutation jobs started within 19 seconds of run creation. Peak overlap was 256 jobs. Sum of mutation job durations: 324552 seconds.
- 38 of the 256 mutation jobs ran longer than 1800 seconds. The longest was `single-action-configuration-p35` at 2505 seconds; its committed estimate is 1566.
- 222 shards carry a numeric estimate; 34 carry null estimates (the provisional targets). In 77 of the 222, the job ran longer than its estimate. The largest ratio of actual to estimate is 2.2549019607843137; the median is 0.90310442144873.
- 62 mutation jobs ran 900 seconds or less.
- The matrix holds 256 entries (`c5_workflow.SHARDS`, 24 targets). GitHub documents: "A job matrix can generate a maximum of 256 jobs per workflow run" ([Actions limits](https://docs.github.com/en/actions/reference/limits), "Existing system limits"). A new target therefore gets no new job unless other jobs merge.
- At the base commit, `c5_runtime.py check --head HEAD` returns `provisional` with pending targets `Common/Phase/B10FinalProjectionCodec.cs`, `Judge/B10FinalJudge.cs` and `VerifyRun/B10Final/B10FinalRepeatEvidence.cs`.
- Reproduction in a scratch worktree on 2026-11-15: appending `// probe` to `src/Parchwell.Verifier/AssemblyInfo.cs`, which is not a target, makes `check --base HEAD~1 --head HEAD` return `{"reasons": ["stale_inputs"], "status": "rejected"}`.
- The rejection has no renewal path short of new code. `c5_runtime.py:484` and `:828` pin `PROVISIONAL_TIMING_INPUTS_HASH` and `CODEC_TIMING_INPUTS_HASH` to `e15cd682a2731538bfde90aa333a9742bccb40e3db957f855d71a25a9e520463`. `validate` (`c5_runtime.py:1026`) and the `approved_*` functions compare the current timing digest with those constants. No documented procedure says how to reassess and rebind the 256 estimates, and the issue-wide measurement allowance of the item plan (section 6) is not replenished for later pull requests.

Answer to question 1, as fact and inference. Fact: a required check already rejects a new target without qualification (`missing_qualification`, `c5_runtime.py:1005`). Fact: the estimates it accepts under-predicted 77 of 222 measured jobs, by up to 2.25 times. Inference: any verifier pull request that touches the project closure is now blocked until someone edits code constants, and the check cannot tell a sound renewal from a mechanical rehash. The B10 failure mode therefore has two parts today: estimates are not predictive, and renewal is undefined.

Answer to question 3. Stryker.NET 5.0.0 documents two options. The repository pins `dotnet-stryker` 5.0.0 (`.config/dotnet-tools.json:5-6`). Tag `dotnet-stryker@5.0.0` peels to commit `6e77a3451bac4793e9c839c3ff4055c1b30ca3af` (`git ls-remote`, 2026-11-15). Quotes are from [configuration.md at that commit](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/docs/configuration.md):

- `since` ("Optimization"): "Stryker will only report on mutants within the changed code. All other mutants will not have a result." Also: "For changes on test project files all mutants covered by tests in that file will be seen as changed."
- `with-baseline` ("Baseline"): "For unchanged mutants the previous result is reused." It sits after the heading "Experimental": "Results can contain false positives and false negatives." Also: "This feature implicitly enables the since feature for now." Providers are Disk (the `StrykerOutput` folder), Dashboard (an API key), Azure File Storage (a SAS token) and S3 (AWS credentials).

Consequences, labelled. Fact: `since` leaves mutants without a result, so the raw population reconciliation in `c5_workflow.py aggregate` cannot report `measurement_complete: true` for a partial run (inference about the aggregator's response, from the plan's retained contract "raw population reconciliation"). Fact: the C5 configuration sets `"coverage-analysis": "off"` (`stryker-c5-config.json:43`, `c5_workflow.py:254`). The docs do not say how `since` decides which mutants a changed test file covers when coverage analysis is off; that is undocumented, not verified. Fact: Dashboard, Azure File Storage and S3 baselines need a secret or an Azure credential; AGENTS.md hard safety rules 3 and 7 bar the Azure route and committed secrets. Inference: with coverage off, every test runs against every mutant, so a change anywhere in the tested program can change the status of a mutant in an unchanged file. Reusing an unchanged target's result is then an approximation, which is what the docs' warning describes.

A related gap that question 3 exposes. docs/agents/factory.md "Evidence pack" (`factory.md:198`) triggers a new mutation run only for changes to the `mutate` list paths. A change to a mutation-included test, or to non-target verifier code the targets call, already reuses the last passing result without a rerun. That is the "skip unchanged targets" behaviour in another form, and it covers test changes too. PR B offers the correction; Hari decides.

## 3. Scope and ordered pull requests

Two pull requests, in order. Each is reviewable and green on its own. No child issues unless Hari directs.

### PR A: hosted-baseline qualification

Replace the authored-estimate model with a measured hosted baseline and three bounded renewal paths. Record the question 3 answer in the operational document. Planned on the recommended answers to open questions 1 to 4; another answer is a revision of this plan.

Allowed files:

- `tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py`
- `tests/Parchwell.Verifier.Tests/Mutation/test_c5_runtime.py`
- `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`
- `docs/operations/verifier-mutation.md`, section "Runtime qualification diagnostics" only.
- `items/474/state.md`, by the implementing session under docs/agents/factory.md "Item state file".

Size estimate, 2026-11-15: 600 to 1100 changed lines of code and tests in 3 files, plus a rewrite of the 4354-line qualification file, 5 files in all. Basis: one schema change, one new subcommand, about 20 test cases, and one data file regenerated from saved API reads. This is an estimate, not a limit.

### PR B: mutation acceptance inputs (only if Hari adopts open question 5)

Change the trigger of mutation acceptance in docs/agents/factory.md "Evidence pack" from the `mutate` list to the mutation input digest. If Hari rejects question 5, a later revision removes PR B.

Allowed files:

- `docs/agents/factory.md`, "Evidence pack", the paragraph at line 198 only. This is a protected path; Hari merges.
- `docs/operations/verifier-mutation.md`, one definition paragraph in "Runtime qualification diagnostics".
- `items/474/state.md`.

Size estimate, 2026-11-15: 5 to 15 changed lines in 3 files. Basis: one paragraph replaced, one paragraph added, one state line.

### Paths that must not change in either pull request

- `.github/workflows/` and `docs/operations/workflows/`. The check step at `dotnet-verification.yml:137-151` already runs `check` with full history (`fetch-depth: 0`, line 129); PR A keeps its command line.
- `tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py`, `c5_projection.py`, `stryker-c5-config.json`, `test_c5_workflow.py`, `test_c5_cohort_*.py`. The layout, cuts, cohorts and mutation method stay as they are.
- Production C#, C# tests and fixtures, `.config/dotnet-tools.json`, `AGENTS.md`, `CLAUDE.md`, skills, `results/`, any `expected.yaml`, infrastructure.

## 4. External contracts

### Retained

`check` keeps its command line, its final stdout line `{"status": ..., "reasons": [...]}` and its exit codes (0 for `qualified`, `provisional` or `not_applicable`; 1 for `rejected`). It keeps the reason codes `invalid_input`, `missing_qualification`, `stale_inputs`, `missing_measurement`, `invalid_estimate`, `shard_over_budget`, `matrix_over_capacity`, `layout_mismatch`, sorted. It adds none. `profile` and `performance` keep their command lines and outputs. The relevance test for `not_applicable` (`c5_runtime.py:1129-1138`) is unchanged.

### Qualification file, schema version 2

Top-level keys, exactly: `schema_version` (integer 2), `budget_seconds` (integer 1800), `estimate_error_factor_percent` (integer 226), `baseline`, `suite_scaling`, `shared_inputs`, `targets`.

`baseline` keys, exactly: `run_id` (integer), `run_attempt` (integer), `head_sha` (40 lowercase hex), `timing_inputs_sha256` (64 lowercase hex), `jobs_sha256` (64 lowercase hex, SHA-256 of the saved `{"jobs":[...]}` export for that attempt), `shards`. `shards` maps each mutation job's shard ID in that run to `{"selection_sha256": HEX64, "seconds": INT}`. `seconds` is `completed_at` minus `started_at` of that job, in whole seconds, as the API reports them.

`suite_scaling` is `null` or an object with exactly: `baseline_test_ms` (positive integer), `head_test_ms` (positive integer), `head_timing_inputs_sha256` (HEX64), `hardware` (nonempty string), `evidence_sha256` (HEX64 of the two saved `profile.json` files' bytes concatenated, baseline first). Both values come from `c5_runtime.py profile` on the same machine, one at `baseline.head_sha` and one at the pull request head, each with `status: complete`. Each value is `ceil(test_duration_seconds * 1000)` from its `profile.json` (`c5_runtime.py:148`). The sum of test-case durations is used, not `wall_seconds`, because `wall_seconds` includes the build (`c5_runtime.py:141-145`, timing around the whole `dotnet test` launch).

`shared_inputs` and each target's `source_sha256` keep their current meaning and are checked against the head as now.

Each target keeps `source_sha256` and `evidence`, and drops `geometry_status` and `native_geometry`. Each shard entry has exactly: `selection_sha256`, `timing_inputs_sha256`, `kind`, `estimated_seconds` (integer), `limit_seconds` (integer), `basis` (nonempty string), and the fields its kind requires:

| `kind` | Extra fields | `estimated_seconds` must equal |
|---|---|---|
| `hosted` | none | `baseline.shards[id].seconds` |
| `scaled` | none | `ceil(B * max(H, Bw) / Bw)` with `B = baseline.shards[id].seconds`, `H = suite_scaling.head_test_ms`, `Bw = suite_scaling.baseline_test_ms` |
| `merged` | `merged_from`: list of 2 or more baseline shard IDs | `ceil(S * max(H, Bw) / Bw)` with `S` the sum of `seconds` of `merged_from`; with `suite_scaling` null, `S` |
| `probe` | `setup_seconds`, `main_mutants`, `per_mutant_seconds`, `workers`, `isolated_seconds`, all nonnegative integers, `workers` positive | `ceil(((setup_seconds + isolated_seconds) * workers + main_mutants * per_mutant_seconds) * F / (workers * 100))` with `F = estimate_error_factor_percent` |

All arithmetic is integer; `ceil(a / b)` is `-(-a // b)`.

`limit_seconds` must equal `max(budget_seconds, baseline.shards[id].seconds)` for `hosted` and `scaled`, and `budget_seconds` for `merged` and `probe`.

### `rebaseline` subcommand (new)

```text
python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py rebaseline --run-json RUN --jobs-json JOBS --summary-json SUMMARY --qualification FILE --output FILE
```

Offline. Reads one saved run object, one saved `{"jobs":[...]}` export of one attempt, that run's downloaded `summary.json`, and the current qualification file. Writes a new qualification file to `--output`, which must not exist. It sets `baseline` from the run, sets every shard whose current `selection_sha256` equals the baseline job's selection to `kind: hosted` (when the current timing digest equals `baseline.timing_inputs_sha256`) and leaves every other shard unchanged. It refuses, exit 1 and no output, when: the run is not `completed` with conclusion `success`; any mutation job is not `completed` with conclusion `success`; `summary.json` lacks `measurement_complete: true` or `break_passed: true`; the summary's `commit` differs from `run.head_sha`; the jobs export's mutation job count differs from the number of shards at `run.head_sha`. Its output is a draft for review, not a committed result: the committing session states in the PR notes the command, the input SHA-256 values and that it read the summary itself.

## 5. Numbered requirements

R1. WHEN `check` validates a shard of kind `hosted`, THEN the current timing digest must equal `baseline.timing_inputs_sha256` and the shard's `selection_sha256` must equal `baseline.shards[id].selection_sha256`; otherwise `stale_inputs` or `layout_mismatch` respectively.

R2. WHEN the current timing digest differs from `baseline.timing_inputs_sha256`, THEN every shard must be `scaled`, `merged` or `probe`; a `hosted` shard rejects `stale_inputs`. `suite_scaling` must be non-null and its `head_timing_inputs_sha256` must equal the current digest; otherwise `missing_measurement` (null) or `stale_inputs` (mismatch).

R3. WHEN `check` runs with full history, THEN it recomputes the timing digest at `baseline.head_sha` with the head's `input_maps` and compares it with `baseline.timing_inputs_sha256`; a mismatch or an unknown commit is `invalid_input`.

R4. WHEN a shard of kind `scaled` or `merged` names a baseline shard ID absent from `baseline.shards`, THEN `layout_mismatch`. A `merged` shard's `merged_from` IDs must not be current shard IDs of kind `hosted` or `scaled`.

R5. WHEN `estimated_seconds` differs from the formula for its kind, or `limit_seconds` differs from its rule, or a required field is missing, extra, boolean, negative or non-integer, THEN `invalid_estimate`.

R6. WHEN `estimated_seconds` exceeds `limit_seconds`, THEN `shard_over_budget`. No other exception exists; the `approved_*` special cases and the provisional status are removed, and the static-owner and codec acceptances survive as `hosted` shards whose limit is their measured baseline.

R7. WHEN `estimate_error_factor_percent` is not 226, `budget_seconds` is not 1800 or `schema_version` is not 2, THEN `invalid_input`.

R8. WHEN the matrix exceeds 256 entries, or the qualification's shard set differs from `c5_workflow.SHARDS`, THEN `matrix_over_capacity` or `layout_mismatch`, as now.

R9. WHEN a pull request adds a target or changes a cut, THEN each new or changed shard must be `probe` or `merged`; its evidence must include a `local_probe` entry whose note carries `hardware=` and `calibration=` (current rule at `c5_runtime.py:1043-1047`).

R10. WHEN `rebaseline` is given inputs that fail any refusal condition in section 4, THEN it exits 1, writes nothing and prints the failed condition to stderr.

R11. WHEN `rebaseline` succeeds, THEN `check --head <run head>` on its output, at the run's head commit, returns `qualified`.

R12. WHEN PR A is merged, THEN the committed qualification file is the `rebaseline` output from run 43670258319 attempt 1, unedited except for formatting, and `check --head HEAD` returns `qualified` with no pending targets. Before that run's head, no relevant input changed: at the base commit `check` reports no `stale_inputs` against the committed timing digest, which equals the provisional constant.

Per-pull-request measurement allowance (procedure, in the operational document, not enforced by `check`): at most 2 `profile` runs and at most 4 bounded `local-check` probes per pull request, each with a 600-second timeout including build, no automatic retries. Exhaustion action: `push -wip and hand off to the next session`.

## 6. Invariants and safety rules

- The mutation method, test population, assertions, coverage mode, Stryker pin, break threshold and 256-job layout are unchanged by PR A.
- No estimate replaces mutation acceptance. A `qualified` result is a sizing claim only.
- `rebaseline` and `check` start no test, mutation or network process.
- No workflow dispatch, no Azure credential, no secret. Hard safety rules 3, 7, 8 and 9 hold.
- The qualification file is authored configuration produced by a reviewed command from cited API reads, as the item plan section 4 already treats it. No native report, job log or jobs export is committed.

## 7. Test map

Fixtures are synthetic qualification documents over a two-target synthetic layout, as existing `test_c5_runtime.py` cases build them. Standing fixture rule for existing tests: when an existing test's fixture is rejected by a new check, the implementer does exactly one of (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises, or (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it; the cold review and Codex review check each listed change. Tests of the removed `approved_*` functions and provisional status are replaced, case for case, by the `hosted` limit tests below, and each replacement is listed.

| Test | Requirement | Input | Expected |
|---|---|---|---|
| `test_hosted_equal_baseline` | R1, R6 | baseline 2505, kind hosted, estimated 2505, limit 2505 | `qualified` |
| `test_hosted_estimate_off_by_one` | R5 | as above, estimated 2506 | `rejected`, `["invalid_estimate"]` |
| `test_hosted_after_input_change` | R2 | timing digest differs from baseline, kind hosted, suite_scaling valid | `rejected`, `["stale_inputs"]` |
| `test_hosted_selection_mismatch` | R1 | selection differs from baseline selection | `rejected`, `["layout_mismatch"]` |
| `test_scaled_ratio_up` | R2, R5 | B 1000, Bw 80000, H 100000 | estimated 1250 qualified; 1249 `invalid_estimate` |
| `test_scaled_ratio_down_floors_at_one` | R5 | B 1000, Bw 80000, H 76000 | estimated 1000 qualified; 950 `invalid_estimate` |
| `test_scaled_over_limit_below_budget_baseline` | R6 | B 1700, Bw 100000, H 110000, limit 1800 | estimated 1870, `["shard_over_budget"]` |
| `test_scaled_accepted_overrun_boundary` | R6 | B 2505, limit 2505, Bw 100000; H 100000 / H 101000 | 2505 qualified; 2531 `["shard_over_budget"]` |
| `test_scaling_missing` | R2 | digest changed, suite_scaling null, kind scaled | `["missing_measurement"]` |
| `test_scaling_stale` | R2 | `head_timing_inputs_sha256` not current | `["stale_inputs"]` |
| `test_merged_sum` | R4, R5 | merged_from seconds 600 and 700, suite_scaling null | estimated 1300 qualified; 1299 `invalid_estimate` |
| `test_merged_unknown_id` | R4 | merged_from names an ID absent from baseline | `["layout_mismatch"]` |
| `test_probe_at_and_over_budget` | R5, R6 | setup 100, isolated 0, workers 2, per_mutant 30, F 226; mutants 40 / 50 | 1582 qualified; 1921 `["shard_over_budget"]` |
| `test_probe_needs_local_probe_note` | R9 | probe shard, evidence note lacks `calibration=` | `["missing_measurement"]` |
| `test_factor_policy_pinned` | R7 | F 225 | `["invalid_input"]` |
| `test_schema_version_1_rejected` | R7 | schema_version 1 | `["invalid_input"]` |
| `test_baseline_digest_mismatch` | R3 | baseline digest not equal to recomputed digest at baseline commit | `["invalid_input"]` |
| `test_non_target_change_renews` | R2 | git fixture: base, then a non-target `.cs` change; qualification with all shards scaled and fresh suite_scaling | `qualified` (without the scaled renewal: `["stale_inputs"]`) |
| `test_boolean_and_negative_fields` | R5 | `true` as seconds; -1 as mutants | `["invalid_estimate"]` |
| `test_matrix_257` | R8 | 257 synthetic shards | includes `matrix_over_capacity` |
| `test_rebaseline_refusals` | R10 | one case each: run conclusion `failure`; one job `cancelled`; `break_passed: false`; summary commit differs; job count off by one; output exists | exit 1, no output file |
| `test_rebaseline_round_trip` | R11 | synthetic successful run over the synthetic layout | output passes `check` as `qualified`; unchanged-selection shards become `hosted` |

## 8. Deployment assumptions

- A1. The mutation-only test population runs to completion under `profile` within 600 seconds on the machine the implementer uses. Evidence: the shortest hosted mutation job in run 43670258319 (`b10-final-projection`) took 110 seconds including build and Stryker's initial test run. Inference: the full suite's wall time is under 110 seconds on a hosted `ubuntu-24.04` runner. That job ran on an already-provisioned hosted runner, idle except for itself. Not measured locally. The first `profile` in PR A is the measurement; if it exceeds 600 seconds the allowance's exhaustion action applies.
- A2. With coverage analysis off, per-mutant time scales with the summed test-case duration of the suite, so `max(H, Bw) / Bw` bounds the change in shard time. This is an inference from `"coverage-analysis": "off"` and the docs' "All unit tests are run against all mutants" (configuration.md, "coverage-analysis"). No test covers it. It is listed under section 10, question 2, until Hari accepts the risk by name.
- A3. Two `profile` runs on one local machine are comparable. No test covers it; noise can only raise estimates (the ratio has a floor of 1) or cause false rejections, never hide a slowdown below the floor. Listed under question 2.

## 9. Acceptance commands

From the repository root, on the PR A head:

```bash
python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p test_c5_runtime.py -v
python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p test_c5_workflow.py -v
python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --head HEAD --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json
npm run check:docs
```

Evidence: every test passes; `check` prints `{"reasons": [], "status": "qualified"}` with no `pending_targets`; the docs check reports no new warning in changed files. The PR notes record the `rebaseline` command, the SHA-256 of the saved run, jobs and summary inputs, and the first `profile` result for A1. The required `.NET verification` check passes on the PR head.

PR B: `npm run check:docs` and the required checks pass.

## 10. Open questions for Hari

1. Pre-merge sizing model (question 1). Recommended: the hosted baseline of PR A, with no-regression limits (`max(1800, measured baseline)` per unchanged shard). It replaces the special-case approvals with measured limits and treats every current shard at or under its measured time as qualified. Alternatives: (a) keep the authored-estimate model and write a renewal procedure for it; (b) a size-only default split for new targets with no runtime basis. Strongest counterargument: the no-regression limit accepts today's 38 shards over 1800 seconds as the standing ceiling, which your decision of 2026-11-15 ("fixed for now") supports but the 30-minute policy of decision 6876901367 point 1 does not. Effect on the verdict route: none directly; it unblocks verifier pull requests that today reject with `stale_inputs`.
2. Estimate error factor and scaling (question 1). Recommended: 226 percent for shards with no hosted timing, from the measured worst ratio 2.2549 in run 43670258319, and acceptance of assumptions A2 and A3 by name. Alternative: 100 percent (no factor), which repeats the under-prediction measured above. Counterargument: 2.26 times may split new targets into more jobs than needed, at a matrix already at 256.
3. Per-pull-request measurement allowance (question 1). Recommended: 2 `profile` runs and 4 probes, 600 seconds each, no retries, exhaustion action `push -wip and hand off to the next session`. This is a policy value, not a measurement.
4. Incremental runs (question 3). Recommended: do not use `since` or `with-baseline`; keep the full fresh run on `main`. `since` leaves mutants without a result. `with-baseline` is documented as experimental with false positives and negatives, and its non-disk stores need a secret or Azure credential. "Evidence pack" stays unchanged under this answer. Strongest counterargument: each target change costs a full run, 324552 job-seconds in run 43670258319.
5. Acceptance trigger (gap found under question 3). Recommended: adopt PR B, so that a change to any mutation input (the timing digest's `shared_inputs` and target sources, which exclude the qualification file) requires a new passing run on `main`. Today a change to a mutation-included test reuses the last result without a rerun. Counterargument: more full runs, for example after a test-only change. Proposed replacement text for the first sentence at `factory.md:198`: "Mutation acceptance for a pull request that changes any mutation input, as the [operational definition](../operations/verifier-mutation.md#runtime-qualification-diagnostics) defines it, is a passing `verifier-c5-mutation.yml` run dispatched on `main` after the change merges." And for the fifth sentence: "If a later main change modifies any mutation input, acceptance for that change requires a later passing run whose measured commit includes it."
6. Implementing tool. The issue body named Codex; decision 6893688070 starts planning in Claude Code. Name the implementing tool for this plan.

## 11. Sources

- Actions REST API reads of run 43670258319 and its attempt-1 jobs, pages 1 to 3, read 2026-11-15 with `gh api`.
- [Stryker.NET configuration.md at 6e77a3451bac4793e9c839c3ff4055c1b30ca3af](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/docs/configuration.md), headings "since", "Experimental", "with-baseline", "baseline.fallback-version", "baseline.provider", "coverage-analysis". Read through a web-fetch subagent; the quoted sentences were then checked by `grep` on the raw file, 2026-11-15.
- [GitHub Actions limits](https://docs.github.com/en/actions/reference/limits), "Existing system limits", read 2026-11-15; the quoted sentence was checked by `grep` on the fetched page.
- Repository files at the base commit, cited by line above.

## 12. Plan-check answers

(a) Effort that scales with a runtime count: the probes and profiles of a pull request, capped at 2 profiles and 4 probes of 600 seconds each, exhaustion action `push -wip and hand off to the next session`. The `rebaseline` input size is fixed by the 256-job matrix.

(b) Stricter than the item plan: the 226-percent factor on unmeasured shards, because the item plan's estimates under-predicted measured jobs by up to 2.25 times. Looser than the item plan: the no-regression limit lets measured shards above 1800 seconds qualify; this is open question 1.

(c) Dependencies on code, data or behaviour neither on `main` nor in the pull request: the saved run, jobs and summary inputs to `rebaseline`, checked against the API and the artifact digest the summary download returns; the local suite timing (A1 to A3).

## 13. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope of this plan, when `rebaseline` refuses run 43670258319, or when `check` on the regenerated file is not `qualified`.

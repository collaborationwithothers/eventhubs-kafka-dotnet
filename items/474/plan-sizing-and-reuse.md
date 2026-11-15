# Verifier mutation: pre-merge sizing and reuse of unchanged results

This plan settles the two open questions of issue 474 under Hari's decisions of 2026-11-15. New mutation targets are split by source size before merge. Measured hosted job times are used only to choose which existing jobs merge and to renew existing shards (PR A). Stryker.NET's incremental modes are not used, and mutation acceptance is triggered by any mutation input, not only a mutated file (PR B).

Revision: 2. Base main: `22bcce4b53f4031ec467c2fd6d2b2ff314291fb0`.
Issue: [474](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474).
Plan branch: `claude/474-plan`. Plan file: `items/474/plan-sizing-and-reuse.md`.
Split name: `sizing-and-reuse`, an ordered set of PR A `size-split-and-hosted-renewal` and PR B `mutation-acceptance-inputs`.

## Changes since revision 1

Revision 1 was checked in [plan check 6893892580](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6893892580). Revision 2 applies [decision 6894078013](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894078013) points 2 to 7 and [decision 6894120017](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894120017), which replaces point 1 with option B. It is design-changing and needs a new check.

- The suite-scaling model, the 226 percent factor, assumptions A2 and A3 and the per-pull-request allowance are removed (decision 6894078013 points 2 and 3). This disposes of check findings 1, 3 (scaling part) and 12.
- PR A no longer replaces the authored-estimate model. It keeps it as the shard kind `authored`, with every current check and test, and adds three kinds: `hosted`, `merged` and `size`. Decision 6894078013 point 7 keeps the ownership checks and their tests. Finding 6 is disposed of: no check or test is removed.
- Finding 2: a target whose source changed since the baseline cannot renew by hosted time beyond a growth allowance (R6, open question 2).
- Finding 3: a change to tooling inputs blocks hosted renewal unless Hari accepts it by name (R7, open question 4).
- Finding 4: the baseline records its own target set, so a target added after it is a `size` target, not `invalid_input` (R9). No digest is recomputed at the baseline commit.
- Finding 5: `rebaseline` is pinned in section 4, including job-name parsing and the checkout it runs from. `jobs_sha256` is replaced by per-job IDs.
- Finding 7: PR A's documentation statements are pinned (R14).
- Finding 8: hosted-time representativeness and runner variance are deployment assumptions under open question 5.
- Finding 9: PR B pins the whole replacement paragraph, and defines a mutation input to include the layout tables and the matrix.
- Findings 10 and 11 (`apply in revision`): the median is corrected; the citations now read `dotnet-verification.yml:130` and `c5_runtime.py:1131-1140`. The exit-code clause keeps `provisional`, because revision 2 keeps the provisional status (R12); finding 11's third replacement assumed revision 1's R6, which revision 2 drops.
- The two P2 tests marked `apply in implementation` are in the test map (`test_merged_budget_boundary`, `test_rebaseline_refusals`).
- New: a shadowed duplicate test name in `test_c5_runtime.py` is renamed so both definitions run (section 7).

## 1. Goal and authority

Authority, in creation order:

1. The [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474), questions 1 and 3, and its constraints: no loosened, deleted or skipped test or assertion without Hari's decision; a passing run still covers every mutated file at the merged commit.
2. [Design decision 6876901367](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6876901367): point 1, a 30-minute shard sizing ceiling; point 5, missing or stale qualification blocks an affecting pull request, mutation tooling included.
3. [Timing decision 6877623126](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6877623126). It continues to bind `authored` shards.
4. [Static-owner decision 6880280289](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6880280289), [clarification 6880342017](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6880342017) and [decision 6880732047](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6880732047), point 3 of which keeps the native ownership checks.
5. Later rebalance decisions up to [6887981346](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6887981346), which set the current 256-job layout.
6. [Decision 6893688070](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6893688070): scope is questions 1 and 3.
7. [Decision 6894078013](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894078013): points 2 and 3 lapse; point 4, no `since` or `with-baseline`; point 5, adopt PR B with check finding 9's wording; point 6, Claude Code implements; point 7, the ownership checks and their tests stay.
8. [Decision 6894120017](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894120017): option B replaces point 1. Any policy value option B needs that the decision does not set is an open question.

The approved [mutation-runtime plan, revision 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/1cdbcdf0407c1b897c7bcec29ef97d261109b473/items/474/plan-mutation-runtime.md) is the item plan. Its two pull requests have merged.

This is tooling and governance work. It adds no hypothesis, expected outcome, verdict, Azure deployment or live fault, and dispatches no workflow.

## 2. Evidence

All figures were computed by script on 2026-11-15. Sources: the Actions REST API run object and attempt-1 jobs, three pages, of run [43670258319](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43670258319); the committed qualification file; and `c5_workflow.shard_config` at the base commit. Job time is `completed_at` minus `started_at`.

- Run 43670258319, head `1e01aea24a457fffe0dc970703af2de608d524e3`, concluded `success` in 2597 seconds. It ran 256 mutation jobs, all started within 19 seconds of run creation, with a peak overlap of 256. 38 jobs ran longer than 1800 seconds; the longest was `single-action-configuration-p35` at 2505 seconds. 62 jobs ran 900 seconds or less.
- 222 shards carry a numeric estimate. In 77 of them the job ran longer than estimated. The largest ratio of actual to estimate is 2.2549019607843137; the median is 0.9018994329465873.
- Seconds per UTF-16 character, over the 212 shards whose selection is one span with no exclusion: median 1.0264187866927592, 90th percentile 1.9315403422982884, 95th percentile 2.23828125, maximum 4.7740112994350286 (`single-action-configuration-p30`, 354 characters, 1690 seconds). Percentiles are the nearest-rank value.
- A cap fitting 1800 seconds at the 95th-percentile rate is 804 characters (1800 / 2.23828125 = 804.19). Re-cutting every current target at that cap would need 463 jobs; today's layout has 256. So a size cap can apply to new and much-grown targets only.
- The matrix holds 256 entries. GitHub documents: "A job matrix can generate a maximum of 256 jobs per workflow run" ([Actions limits](https://docs.github.com/en/actions/reference/limits), "Existing system limits"). A new target gets jobs only if existing jobs merge.
- The longest shard ID is 36 characters. A job name is `mutation (<shard>, <target>)`, which GitHub truncates (run 43670258319 has names ending in `...`). The shard ID ends at character 48 or earlier, so it is never cut.
- At the base commit, `check --head HEAD` returns `provisional` for `Common/Phase/B10FinalProjectionCodec.cs`, `Judge/B10FinalJudge.cs` and `VerifyRun/B10Final/B10FinalRepeatEvidence.cs`. The current timing digest equals `e15cd682a2731538bfde90aa333a9742bccb40e3db957f855d71a25a9e520463`, the constant at `c5_runtime.py:484` and `:828`.
- In a scratch worktree, appending `// probe` to `src/Parchwell.Verifier/AssemblyInfo.cs`, which is not a target, makes `check --base HEAD~1 --head HEAD` return `{"reasons": ["stale_inputs"], "status": "rejected"}`. Renewal today needs new code constants.

Question 3, Stryker.NET. The repository pins `dotnet-stryker` 5.0.0 (`.config/dotnet-tools.json:5-6`). Tag `dotnet-stryker@5.0.0` peels to `6e77a3451bac4793e9c839c3ff4055c1b30ca3af` (`git ls-remote`, 2026-11-15). From [configuration.md at that commit](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/docs/configuration.md):

- `since`: "Stryker will only report on mutants within the changed code. All other mutants will not have a result." Also: "For changes on test project files all mutants covered by tests in that file will be seen as changed."
- `with-baseline`: "For unchanged mutants the previous result is reused." It sits after the heading "Experimental": "Results can contain false positives and false negatives." Its non-disk providers need a Dashboard API key, an Azure File Storage SAS or AWS credentials.
- The C5 configuration sets `"coverage-analysis": "off"` (`stryker-c5-config.json:43`). The docs do not say how `since` decides which mutants a changed test covers with coverage off; this is not documented.

Decision 6894078013 point 4 adopts the answer: neither option is used. Today's "Evidence pack" trigger (`factory.md:198`) covers only `mutate` paths, so a test-only or non-target code change already reuses the last result without a rerun. PR B closes that gap (point 5).

## 3. Scope and ordered pull requests

### PR A: size split and hosted renewal

Allowed files:

- `tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py`
- `tests/Parchwell.Verifier.Tests/Mutation/test_c5_runtime.py`
- `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`
- `docs/operations/verifier-mutation.md`, section "Runtime qualification diagnostics" only.
- `items/474/state.md`, by the implementing session under docs/agents/factory.md "Item state file".

Size estimate, 2026-11-15: 500 to 900 changed lines of code and tests in 2 files, plus the regenerated qualification file (4354 lines today), 5 files in all. Basis: one schema extension with three shard kinds, two subcommands and about 30 test cases. Not a limit.

### PR B: mutation acceptance inputs

Allowed files:

- `docs/agents/factory.md`, "Evidence pack", the paragraph at line 198 only. Protected path; Hari merges.
- `docs/operations/verifier-mutation.md`, one paragraph in "Runtime qualification diagnostics".
- `items/474/state.md`.

Size estimate, 2026-11-15: 5 to 15 changed lines in 3 files. Not a limit.

PR B depends on PR A only through the operational document section; it may merge first if PR A is blocked.

### Paths that must not change

- `.github/workflows/` and `docs/operations/workflows/`. The check step at `dotnet-verification.yml:137-151` runs `check` with full history (`fetch-depth: 0`, line 130); PR A keeps that command line.
- `c5_workflow.py`, `c5_projection.py`, `stryker-c5-config.json`, `test_c5_workflow.py`, `test_c5_cohort_*.py`. The layout, cuts, cohorts and mutation method stay as they are in both pull requests.
- Production C#, C# tests and fixtures, `.config/dotnet-tools.json`, `AGENTS.md`, `CLAUDE.md`, skills, `results/`, any `expected.yaml`, infrastructure.

## 4. External contracts

### Retained

`check` keeps its command line, its final stdout line `{"status": ..., "reasons": [...]}` (with `pending_targets` for `provisional`), and its exit codes (0 for `qualified`, `provisional` or `not_applicable`; 1 for `rejected`). It keeps its eight reason codes and adds none. `profile` and `performance` are unchanged. The relevance test for `not_applicable` (`c5_runtime.py:1131-1140`) is unchanged. `native_geometry_valid` (`c5_runtime.py:960-999`) and the provisional status are unchanged.

### Qualification file, schema version 2

Schema 2 is schema 1 plus these top-level keys: `baseline` (object or `null`), `size_cap_chars` (integer, 804), `tooling_acceptance` (object or `null`). `schema_version` is 2 and `budget_seconds` stays 1800.

`baseline`, when not null, has exactly: `run_id`, `run_attempt` (integers), `head_sha` (40 lowercase hex), `tooling_sha256` (64 lowercase hex), `targets` and `shards`. `targets` maps each target path at the baseline to `{"source_sha256": HEX64, "utf16_length": INT, "cuts": [INT, ...]}`; `cuts` is the target's `PRIMARY_CUTS` tuple at the baseline, or `[]`. `shards` maps each shard ID at the baseline to `{"job_id": INT, "selection_sha256": HEX64, "span": [INT, INT] or null, "seconds": INT}`. `span` is the shard's primary span with its end clamped to `utf16_length`, or `null` for a crossing or singleton shard.

The tooling digest is the SHA-256 of the canonical JSON (`c5_runtime.py` `canonical`) of `{path: shared_inputs[path]}` over the paths that are present among: `tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py`, `.github/workflows/verifier-c5-mutation.yml`, `tests/Parchwell.Verifier.Tests/Mutation/c5_projection.py`, `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`, `.config/dotnet-tools.json`, `global.json`. The runner and workflow entries are the execution-portion digests `input_maps` already computes, so cut and matrix changes do not change the tooling digest.

`tooling_acceptance`, when not null, has exactly `decision_url` (matching `^https://github\.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/[0-9]+#issuecomment-[0-9]+$`) and `tooling_sha256` (HEX64).

Each shard entry has a `kind`:

| `kind` | Fields besides `kind` | Meaning |
|---|---|---|
| `authored` | exactly the schema 1 shard fields | today's model and checks, unchanged |
| `hosted` | `selection_sha256`, `estimated_seconds`, `basis` | renewed from the baseline job with the same selection |
| `merged` | `selection_sha256`, `merged_from` (list of 2 or more baseline shard IDs), `estimated_seconds`, `basis` | adjacent baseline primaries joined to free a slot |
| `size` | `selection_sha256`, `span_chars` (integer, or `null` for a crossing shard), `basis` | a new or much-grown target split by size |

A target's crossing shard after a merge in that target is kind `hosted` with `selection_sha256` equal to its current selection; R5 sets its rule.

### `rebaseline` subcommand (new)

```text
python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py rebaseline --run-json RUN --jobs-json JOBS --summary-json SUMMARY --output FILE
```

Offline. It runs from a clean checkout in which no mutation input changed since the run's head: it refuses unless `git status --porcelain` is empty, `input_maps(run.head_sha)` equals `input_maps(HEAD)` (computed with the checkout's code), and the bytes of `c5_workflow.py` and `.github/workflows/verifier-c5-mutation.yml` are equal at `run.head_sha` and `HEAD`. A failure to read any path at `run.head_sha` is a refusal. `JOBS` is `{"jobs": [...]}` combining every page of `/actions/runs/{run_id}/attempts/{run_attempt}/jobs`. A mutation job is one whose name starts with `mutation (`; its shard ID is the text from character 10 up to the first `, `. Job seconds are `int((completed_at - started_at).total_seconds())`.

It writes to `--output`, which must not exist, a complete schema 2 file valid at that checkout: `shared_inputs` and target hashes from `input_maps(HEAD)`, `baseline` from the run and the checkout's `c5_workflow`, every shard as `hosted` with `estimated_seconds` equal to its job's seconds, `size_cap_chars` 804 and `tooling_acceptance` null. The provisional targets keep their `geometry_status` and `native_geometry` fields from the current qualification file, which it reads at the checkout.

It refuses, with exit 1, no output file and the failed condition on stderr, when any of these holds: the run's `status` is not `completed` or its `conclusion` is not `success`; any mutation job's `status` is not `completed` or its `conclusion` is not `success`; two jobs parse to the same shard ID; the parsed ID set differs from `c5_workflow.SHARDS`; `summary.json` `commit` differs from `run.head_sha`; `measurement_complete` or `break_passed` is not `true`; the checkout condition fails; the output exists. (Revision 2 replaces revision 1's head-equality rule so that PR A's new code can rebaseline a run whose head predates it.)

### `refresh-inputs` subcommand (new)

```text
python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py refresh-inputs --qualification FILE --output FILE
```

Offline. It writes a copy of the qualification with `shared_inputs` and each target's `source_sha256` recomputed by `input_maps(HEAD)`. It changes no other field. It refuses when the output exists or the input fails schema 2 shape checks.

## 5. Numbered requirements

R1. WHEN a shard is `authored`, THEN every current check applies to it unchanged, including the timing-digest binding.

R2. WHEN a shard is `hosted` and not a crossing shard after a merge, THEN `baseline` is non-null, its ID is in `baseline.shards`, its current generated selection equals both its `selection_sha256` and `baseline.shards[id].selection_sha256`, and `estimated_seconds` equals `baseline.shards[id].seconds`. Otherwise `missing_measurement` (no baseline or ID absent), `layout_mismatch` (selection) or `invalid_estimate` (seconds).

R3. WHEN a shard is `hosted` or `merged`, THEN its target is in `baseline.targets`, and the target's current UTF-16 length minus `baseline.targets[path].utf16_length` is at most `size_cap_chars`. A target absent from the baseline gives `missing_measurement`; growth above the cap gives `stale_inputs`. A target whose source changed within that growth keeps hosted renewal (open question 2).

R4. WHEN a shard is `merged`, THEN its `merged_from` IDs are baseline primaries of its target, none is a current shard ID, their spans are contiguous, and their union equals the shard's current primary span. Its `estimated_seconds` equals the sum of their `seconds` plus the `seconds` of the target's baseline crossing shard (0 when it has none), and is at most `budget_seconds`. Otherwise `layout_mismatch`, `invalid_estimate` or `shard_over_budget`.

R5. WHEN a target's current cuts are a strict subset of `baseline.targets[path].cuts`, THEN its crossing shard may be `hosted` with a selection that differs from the baseline's, and its `estimated_seconds` equals the baseline crossing shard's `seconds`. Otherwise `layout_mismatch`.

R6. WHEN a target is absent from `baseline.targets`, or grew by more than `size_cap_chars` since it, THEN each of its shards is `size`. Each primary span, with its end clamped to the current UTF-16 length, is at most `size_cap_chars` characters, and a target with no cuts is at most `size_cap_chars` characters long. `span_chars` equals the clamped length, or `null` for the crossing shard. Otherwise `shard_over_budget` (too long), `invalid_estimate` (wrong `span_chars`) or `layout_mismatch` (a `size` shard on any other target).

R7. WHEN the current tooling digest differs from `baseline.tooling_sha256`, THEN every `hosted` and `merged` shard rejects `stale_inputs`, unless `tooling_acceptance` is non-null and its `tooling_sha256` equals the current tooling digest. A malformed `decision_url` is `invalid_input`.

R8. WHEN `size_cap_chars` is not 804, `budget_seconds` is not 1800, `schema_version` is not 2, or a field is missing, extra, boolean, negative or of the wrong type, THEN `invalid_input` for top-level keys and `invalid_estimate` for shard fields. `merged_from` with fewer than 2 IDs is `invalid_estimate`.

R9. WHEN a pull request adds a target, THEN the check does not read the new target at the baseline commit; the target's absence from `baseline.targets` makes it a `size` target (R6).

R10. WHEN the matrix exceeds 256 entries, or the qualification's shard set differs from `c5_workflow.SHARDS`, THEN `matrix_over_capacity` or `layout_mismatch`, as now.

R11. WHEN `rebaseline` meets any refusal condition in section 4, THEN it exits 1, writes nothing and prints the condition to stderr. WHEN it succeeds, `check --head HEAD` on its output at that checkout returns `qualified` or `provisional`.

R12. WHEN the committed qualification has the provisional targets as `hosted` shards, THEN `native_geometry_valid` and the provisional result still apply to them. Only two provisional-path checks change: the comparison of the timing digest with `PROVISIONAL_TIMING_INPUTS_HASH` (`c5_runtime.py:1026-1027`) and the `partial_timing` evidence check (`:1037-1039`) apply only when that target has an `authored` shard. The geometry, source-hash, layout-hash and cohort checks (`:1024-1025`, `:1028-1036`) apply as now.

R13. WHEN PR A merges, THEN the committed qualification is the `rebaseline` output from run 43670258319 attempt 1, made at a clean checkout of the PR A head. At the base commit no mutation input differs from the run head `1e01aea24a457fffe0dc970703af2de608d524e3`: the timing digest equals the provisional constant, and `c5_workflow.py` and the mutation workflow are byte-identical (`git diff --quiet`, 2026-11-15). `check --head HEAD` returns `provisional` with the same three pending targets as today. The PR notes give the commands, the SHA-256 of the saved run, jobs and summary inputs, and the summary artifact `c5-summary-43670258319-1` expiry, 2027-02-13.

R14. WHEN PR A merges, THEN "Runtime qualification diagnostics" in `docs/operations/verifier-mutation.md` contains these statements, in this wording or a plain-English equivalent that keeps every condition:
- "A new target, or a target that grew by more than 804 UTF-16 characters since the baseline run, is split by size: each primary span is at most 804 characters. No runtime estimate is made for it before merge."
- "Other shards are renewed from the job times of the baseline run. A merge that frees a slot charges the joined jobs plus the target's crossing job, and must fit 1800 seconds."
- "A change to the mutation tooling stops hosted renewal until a decision by Hari, linked in `tooling_acceptance`, accepts it."
- "Stryker.NET's `since` and `with-baseline` options are not used: `since` leaves other mutants without a result, and `with-baseline` is documented as experimental, with false positives and false negatives."
- "After a complete passing run on `main`, `rebaseline` may refresh the baseline. When a pull request cannot free enough slots by merges that fit 1800 seconds, it stops and hands off to Hari."
The sentences that describe only the authored model (the static-owner exception, `observed_workers=N`) stay, marked as applying to `authored` shards.

R15. PR B replaces the paragraph at `factory.md:198` with exactly this text:

> Mutation acceptance for a pull request that changes any mutation input, as the [operational definition](../operations/verifier-mutation.md#runtime-qualification-diagnostics) defines it, is a passing `verifier-c5-mutation.yml` run dispatched on `main` after the change merges. The run's `summary.json` must identify the commit actually tested, report `measurement_complete: true` and `break_passed: true`, and name a commit on `main` whose history includes the merge commit of that change. A passing run on a later main commit is acceptable when it includes the change under review. If a later main change modifies any mutation input, acceptance for that change requires a later passing run whose measured commit includes it. A missing, failed or incomplete latest applicable run blocks evidence-pack publication; if the run fails or is incomplete, open a fix issue. Do not use the deleted local sample as a mutation check.

and adds to "Runtime qualification diagnostics" exactly:

> A mutation input is any file whose change makes `c5_runtime.py check --base BASE --head HEAD` report a status other than `not_applicable`, except `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` changed alone. This covers the configured target sources, the shared inputs the check lists, and the runner and workflow files, including their layout tables and matrix.

## 6. Invariants and safety rules

- The mutation method, test population, assertions, coverage mode, Stryker pin, break threshold and layout are unchanged by both pull requests.
- No check is removed and no test is deleted or loosened. Existing test fixtures change only under the standing fixture rule in section 7.
- A `qualified` or `provisional` result is a sizing claim, never mutation acceptance.
- `check`, `rebaseline` and `refresh-inputs` start no test, mutation or network process.
- No workflow dispatch, Azure credential or secret. Hard safety rules 3, 7, 8 and 9 hold.
- No native report, job log or jobs export is committed. The qualification file is reviewed configuration made by a reviewed command from cited API reads, as the item plan section 4 treats it.

## 7. Test map

Fixtures are synthetic, as existing `test_c5_runtime.py` cases build them. Standing fixture rule: when an existing test's fixture is rejected by a new check, the implementer does exactly one of (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises, for example adding `"kind": "authored"`, `schema_version` 2 and the three new top-level keys; or (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it; the cold review and Codex review check each listed change. All 69 existing test definitions remain. Two of them share the name `test_excluded_component_source_does_not_change_inputs` in class `AdditionalProjectAndLauncherTests` (`test_c5_runtime.py:854` and `:874`), so Python keeps only the second and the first never runs. PR A renames the first to `test_excluded_component_source_does_not_change_inputs_first` with its body unchanged, so both run.

Unless a row says otherwise, the baseline has target `T` with `utf16_length` 4000, cuts `[2000]`, primaries `t-p1` span `[0, 2000]` 600 s and `t-p2` span `[2000, 4000]` 700 s, crossing `t-crossing` 400 s; tooling unchanged; `size_cap_chars` 804.

| Test | Requirement | Input | Expected |
|---|---|---|---|
| `test_hosted_equal_baseline` | R2 | `t-p1` hosted, estimated 600 | `qualified` |
| `test_hosted_seconds_mismatch` | R2 | estimated 599 | `["invalid_estimate"]` |
| `test_hosted_selection_mismatch` | R2 | baseline selection differs | `["layout_mismatch"]` |
| `test_hosted_without_baseline` | R2 | `baseline` null | `["missing_measurement"]` |
| `test_hosted_over_budget_is_not_limited` | R2 | baseline 2505, estimated 2505 | `qualified` (open question 3) |
| `test_hosted_after_shared_change` | R2 | non-target `.cs` change, `refresh-inputs` applied | `qualified` |
| `test_authored_after_shared_change_still_stale` | R1 | same change, one `authored` shard | `["stale_inputs"]` |
| `test_growth_at_and_over_cap` | R3, R6 | `T` length 4804 / 4805, hosted | `qualified` / `["stale_inputs"]` |
| `test_shrink_keeps_hosted` | R3 | `T` length 3000, hosted | `qualified` |
| `test_merged_sum` | R4 | cut removed; merged `[t-p1, t-p2]` 1700 (600+700+400), crossing hosted 400 | `qualified`; 1699 `["invalid_estimate"]` |
| `test_merged_budget_boundary` | R4 | no crossing; merged seconds 900+900 / 900+901 | 1800 `qualified` / 1801 `["shard_over_budget"]` |
| `test_merged_gap_or_unknown` | R4 | non-contiguous spans; an ID absent from baseline | `["layout_mismatch"]` each |
| `test_merged_single_id` | R8 | `merged_from` one ID | `["invalid_estimate"]` |
| `test_crossing_after_merge` | R5 | crossing estimated 400 / 401; cuts not a subset | `qualified` / `["invalid_estimate"]` / `["layout_mismatch"]` |
| `test_size_new_target_boundary` | R6, R9 | new target `N`, length 1608, cut `[804]` / cut `[805]` | `qualified` / `["shard_over_budget"]` |
| `test_size_singleton_boundary` | R6 | new target length 804 / 805, no cuts | `qualified` / `["shard_over_budget"]` |
| `test_size_span_chars_mismatch` | R6 | `span_chars` 803 for an 804 span | `["invalid_estimate"]` |
| `test_size_on_baseline_target` | R6 | `size` shard on unchanged `T` | `["layout_mismatch"]` |
| `test_new_target_hosted` | R3 | new target as `hosted` | `["missing_measurement"]` |
| `test_new_target_not_read_at_baseline` | R9 | git fixture adding a target after the baseline commit | not `invalid_input`; `qualified` with `size` shards |
| `test_tooling_change_blocks` | R7 | `stryker-c5-config.json` changed, acceptance null | `["stale_inputs"]` |
| `test_tooling_change_accepted` | R7 | acceptance digest equal / different | `qualified` / `["stale_inputs"]` |
| `test_tooling_bad_url` | R7 | `decision_url` `https://example.com/x` | `["invalid_input"]` |
| `test_cut_change_not_tooling` | R7 | only `PRIMARY_CUTS` changed | tooling digest unchanged |
| `test_policy_values_pinned` | R8 | `size_cap_chars` 805; `schema_version` 1 | `["invalid_input"]` each |
| `test_boolean_and_negative_fields` | R8 | `true` as seconds; -1 as `span_chars` | `["invalid_estimate"]` |
| `test_matrix_257` | R10 | 257 synthetic shards | includes `matrix_over_capacity` |
| `test_provisional_hosted_keeps_geometry` | R12 | provisional target hosted, valid geometry / tampered `report_sha256` | `provisional` / `["missing_measurement"]` |
| `test_rebaseline_refusals` | R11 | one case each: run conclusion `failure`; one job `cancelled`; duplicate shard ID; ID set off by one; summary `commit` differs; `measurement_complete: false`; `break_passed: false`; a mutation input differs between the run head and `HEAD`; dirty checkout; output exists | exit 1, no output file |
| `test_rebaseline_parses_truncated_names` | R11 | job name `mutation (single-action-configuration-p35, VerifyRun/SingleAction/SingleActionConfigurationJudgem...` | shard ID `single-action-configuration-p35` |
| `test_rebaseline_round_trip` | R11 | synthetic successful run at a synthetic checkout | output gives `qualified` |
| `test_refresh_inputs_only_hashes` | section 4 | refresh after a change | only `shared_inputs` and `source_sha256` differ |
| `test_assemblyinfo_reproduction` | R2 | git fixture: comment appended to a non-target `.cs`, refreshed | `qualified` (before PR A: `["stale_inputs"]`) |

## 8. Deployment assumptions

None of these has an offline test. Each is under open question 5 until Hari accepts it by name.

- D1. A shard's hosted job time from one run stands for later runs of the same selection after a change outside the tooling inputs, including a change that adds tests. Evidence: one run, 43670258319; already-provisioned hosted runners, all 256 jobs under concurrent load. No repeat was measured. If a change slows the test suite, the first run after merge shows it; the `performance` report records it.
- D2. Hosted runner speed varies little between runs. Not measured: one sample per shard.
- D3. Removing a cut cannot add mutants to the target's crossing shard. A merged primary costs at most the joined primaries plus the old crossing job. Inference from the crossing selection (`c5_workflow.py:621-626`): the complement excludes the primary spans, and joining two adjacent spans keeps their union. Stryker's handling of nodes that straddle a span end is not verified at the pinned version in this plan.
- D4. All jobs start together within account capacity: observed once, peak 256. It affects workflow time, not the `check` result.

## 9. Acceptance commands

From the repository root, on the PR A head:

```bash
python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p test_c5_runtime.py -v
python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p test_c5_workflow.py -v
python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --head HEAD --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json
npm run check:docs
```

Evidence: every test passes, and all 69 existing test definitions run under 69 distinct names. `check` prints `provisional` with the three pending targets of section 2. The docs check shows no new warning in changed files. The required `.NET verification` check passes. The PR notes record R13's inputs. A comment-only change to `src/Parchwell.Verifier/AssemblyInfo.cs` in a scratch worktree, after `refresh-inputs`, is not `stale_inputs`.

PR B: `npm run check:docs` and the required checks pass; the paragraph at `factory.md:198` equals R15's text byte for byte.

## 10. Open questions for Hari

Option B needs these policy values and acceptances, which decision 6894120017 does not set.

1. Size cap. Recommended: 804 UTF-16 characters per primary, from the 95th-percentile measured rate; at the worst observed rate a full 804-character shard would run about 3838 seconds (804 x 4.774; inference). Alternatives: 377 (worst rate; 974 jobs for today's targets), 1753 (median rate; about half of such shards over 1800 seconds, inferred from the median). Counterargument: at 804, a new target the size of `VerifyRun/B10Final/B10FinalRepeatEvidence.cs` (49863 characters) needs 63 jobs, which merges may not free.
2. Changed targets. Recommended: a target keeps hosted renewal while it has grown by at most 804 characters since the baseline; beyond that it is re-cut by size. Alternatives: (a) re-cut on any source change, which would turn the one-job `VerifyRun/B10SweepHandler.cs` (24960 characters) into 32 jobs; (b) renew regardless of change. Counterargument: code moved within a file, or added within the allowance, is not detected before merge.
3. Limit for renewed shards. Recommended: none; a renewed shard keeps its measured time, including the 38 over 1800 seconds today, and only merges must fit 1800. Alternative: 1800 for every shard, which rejects every relevant pull request until those 38 are re-cut. Counterargument: the 30-minute ceiling of decision 6876901367 point 1 is not enforced before merge for existing shards.
4. Tooling changes. Recommended: hosted renewal stops when the tooling digest changes, until a decision by Hari, linked in `tooling_acceptance`, accepts that tooling state. Alternative: renew regardless. Counterargument: one more named decision per tooling change.
5. Accept deployment assumptions D1, D2, D3 and D4 by name.

## 11. Sources

- Actions REST API: run 43670258319 and its attempt-1 jobs, pages 1 to 3, read 2026-11-15 with `gh api`.
- [Stryker.NET configuration.md at 6e77a3451bac4793e9c839c3ff4055c1b30ca3af](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/docs/configuration.md), headings "since", "Experimental", "with-baseline", "baseline.provider", "coverage-analysis". Read through a web-fetch subagent; the quoted sentences were checked by `grep` on the raw file, 2026-11-15.
- [GitHub Actions limits](https://docs.github.com/en/actions/reference/limits), "Existing system limits", read 2026-11-15; the quoted sentence was checked by `grep` on the fetched page.
- Repository files at the base commit, cited by line above.

## 12. Plan-check answers

(a) Effort that scales with a runtime count. The `rebaseline` input is fixed by the 256-job matrix. Existing fixture changes are bounded by the 69 existing test definitions. Choosing merges in a later target-adding pull request scales with the slots it needs; the documented procedure (R14) stops and hands off to Hari when merges that fit 1800 seconds cannot free them. No probes or profiles run in either pull request.

(b) Stricter than the item plan: a target added after the baseline must be split to 804-character primaries, whereas the item plan allowed authored estimates. Looser: renewed shards are not held to 1800 seconds (open question 3), and renewal uses one run's hosted times without a per-pull-request measurement (D1). The item plan's issue-wide measurement allowance is not used.

(c) Dependencies on code, data or behaviour neither on `main` nor in the pull request: the saved run, jobs and summary inputs to `rebaseline` (the summary artifact expires 2027-02-13); the commit `1e01aea24a457fffe0dc970703af2de608d524e3`, an ancestor of `main`, which `rebaseline` reads; D1 to D4.

## 13. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope of this plan, when `rebaseline` refuses run 43670258319, or when `check` on the committed file is not `provisional` with the three pending targets.

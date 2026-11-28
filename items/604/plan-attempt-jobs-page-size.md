# Attempt jobs page size: contract-grade plan for issue 604

Revision: r1
Base commit: `15b09147ed4e7c62ea08620b24c14cd049f07562` (`main`)
Pull request: one, named "attempt jobs page size".

The C5 campaign validator reads each run attempt's job list 100 jobs at a time. On a re-run attempt GitHub answered that request with HTTP 502, so lost-shard recovery failed. This plan changes the page size to 30, which the failing attempt served on every page, pins it with a test, and re-pins the validator's reviewed hash so the committed runtime qualification still applies.

## Goal and authority

Goal: `c5_campaign.py` reads every job of a re-run attempt, so the aggregate job of a recovered C5 campaign can prove each shard's origin attempt.

Authority, in creation order:

1. The body of issue 604 (updated 2026-11-28T05:43:36Z). Its scope items 1 to 4 and its constraints are binding.
2. [Decision 7234963381](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/604#issuecomment-7234963381) on issue 604 (2026-11-28T05:54:53Z), which links and adopts [standing decision 7234963031 on issue 499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-7234963031). With `ready-for-agent` applied, a plan revision whose newest fresh check states `Blocking findings: none`, with open questions None, is approved; literal in-scope fixes may go to the next revision with one fresh check; Claude Code implements on `claude-sonnet-5-5`. This plan has one open question, so it is not approvable under that rule until Hari answers it.

The issue is not a child issue and has no item plan; the issue body is the specification. `ready-for-agent` was applied at 2026-11-28T05:43:35Z by Hari's account.

Related: issue 602's plan relies on this recovery. The `## Decision` comment 7234844190 on issue 602 (2026-11-28T05:43:20Z) mentions issue 604, so its planner is told. This plan changes nothing on issue 602.

## Scope

Files allowed to change:

- `tests/Parchwell.Verifier.Tests/Mutation/c5_campaign.py`: function `jobs` only.
- `tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py`: the value of `CAMPAIGN_VALIDATOR_PIN` only.
- `tests/Parchwell.Verifier.Tests/Mutation/test_c5_campaign.py`: `CampaignTests.test_job_pagination` under the fixture rule below, and one new test method in `CampaignTests`.

Paths that must not change:

- `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`.
- `CAMPAIGN_VALIDATOR_BASE` (`5dd294fcb0b3bb9ff2de8a5df5ac71242e775066a91e444091d382cb3406b46f`) and `RUNNER_EXECUTION_PIN` (`5e123dc8552d72e9e424f292ebcc5b4b695ff39fed026470f2879d8b5a234552`) in `c5_runtime.py`.
- `tests/Parchwell.Verifier.Tests/Mutation/c5_companion_artifacts.py`, `c5_workflow.py`, every file under `.github/workflows/` and `docs/operations/workflows/`, `results/`, `scenarios/`, `src/`.

### Inventory of `per_page=100` calls (scope item 1)

Found by `grep -rn per_page` over `tests/Parchwell.Verifier.Tests/Mutation/`, `scripts/` and `.github/workflows/` at the base commit:

| Location | Call | Reads a re-run attempt? | Disposition |
|---|---|---|---|
| `c5_campaign.py:216` | `actions/runs/{run_id}/attempts/{attempt}/jobs?per_page=100&page={page}` | Yes: the coordinator's and companion's current attempt (line 324) and each origin attempt (line 253) | Change to 30 |
| `c5_companion_artifacts.py:166` | `/repos/{repository}/actions/runs/{run_id}/artifacts?per_page=100&page={page}` | No: it lists a run's artifacts, not an attempt's jobs | Unchanged |
| `scripts/decision-log.py:72`, `scripts/cleanup-wip-branches.py:26` | issue comments, branch rules | No; not C5 scripts | Unchanged |

No workflow file under `.github/workflows/` contains `per_page`. The other `api` calls in `c5_campaign.py` (lines 283, 287, 295, 312) read a single run or attempt document and take no page size.

Evidence for leaving the artifacts call unchanged: in the failing aggregate job 140684256258 (attempt 2 of run 46039061248), the step running `c5_companion_artifacts.py` logged `Verified 146 companion evidence archives.` against companion run 46039038805, attempt 1. Whether that call works for a companion run that was itself re-run is not observed; see "Deferred work".

## External contracts

1. GitHub REST request made by `jobs(repository, run_id, attempt)`, page N, for N = 1, 2, ...: `gh api repos/{repository}/actions/runs/{run_id}/attempts/{attempt}/jobs?per_page=30&page={N}`. The suffix passed to `api` is exactly `actions/runs/{run_id}/attempts/{attempt}/jobs?per_page=30&page={N}`.
2. Return value of `jobs` unchanged: `{'total_count': <int>, 'jobs': <list of every job, in page order>}`.
3. Failure messages of `jobs` unchanged, each raised as `ValueError` by `require`: `malformed jobs response`, `job count changed while paging`, `extra job pages`, `missing job pages`.
4. `CAMPAIGN_VALIDATOR_PIN` equals the SHA-256 hex digest of the committed `c5_campaign.py` bytes. `input_maps` keeps recording `CAMPAIGN_VALIDATOR_BASE` for that file while `campaign_validator_is_reviewed` holds (`c5_runtime.py:534-547`, unchanged).
5. The qualification check `c5_runtime.py check` in the `Qualify verifier mutation runtime` step of `.github/workflows/dotnet-verification.yml` (lines 139-152) prints `"status": "qualified"` with `"reasons": []` for the pull request head, with and without `--base`.

## Requirements

1. WHEN `jobs` requests page N THEN the suffix is exactly contract 1, with `per_page=30`.
2. WHEN `jobs` starts THEN its first request is page 1, and each later request is the previous page plus 1.
3. WHEN the rows collected equal `total_count` THEN `jobs` returns them and makes no further request.
4. WHEN a page leaves the rows below `total_count` and that page held a number of jobs other than 30 THEN `jobs` raises `ValueError('missing job pages')`.
5. WHEN a response is not a dict with an int `total_count` and a list `jobs` THEN `jobs` raises `ValueError('malformed jobs response')` (unchanged).
6. WHEN `total_count` differs between pages THEN `jobs` raises `ValueError('job count changed while paging')` (unchanged).
7. WHEN the rows collected exceed `total_count` THEN `jobs` raises `ValueError('extra job pages')` (unchanged).
8. WHEN `c5_campaign.py` is committed THEN `CAMPAIGN_VALIDATOR_PIN` is the SHA-256 of its bytes, and no other constant in `c5_runtime.py` changes.
9. WHEN the qualification check runs on the pull request head THEN contract 5 holds and `c5-runtime-qualification.json` is unchanged.

## Qualification route (scope item 3)

The route is the one issue 494's plan `items/494/plan-c5-failure-causes.md` requirement 15 set up, cited at `c5_runtime.py:337-340`. Shard jobs never run `c5_campaign.py`; only the coordinator's aggregate job does. So `input_maps` records the reviewed validator at its measured hash `CAMPAIGN_VALIDATOR_BASE` when the file's hash equals `CAMPAIGN_VALIDATOR_PIN`, the runner equals `RUNNER_EXECUTION_PIN`, and no shard job mentions the validator. The pull request moves `CAMPAIGN_VALIDATOR_PIN` to the new file's hash and changes nothing else in that route. No new timing measurement and no renewal is needed, because the recorded shared inputs do not change.

Dry run in this planning session, 2026-11-28, in a scratch copy of the base commit with the change of requirements 1 and 8 committed locally (validator hash in that copy `47ee18cd4ee3d5dd391bd3bdc6070f8a52da462b1ba48dc42c2387082a8cab84`; the implementer's bytes will differ, so the pin is computed from the committed file, never copied from here):

- `c5_runtime.py check --base origin/main --head HEAD --qualification .../c5-runtime-qualification.json`: `"status": "qualified"`, `"reasons": []`, exit 0.
- the same without `--base`: `"status": "qualified"`, `"reasons": []`, exit 0.

With `--base`, the result is `qualified`, not `not_applicable`. The base's validator hash (`28aeac53...`) no longer equals the new pin, so `input_maps(base)` records it as it is, the maps differ, and `check` runs the full `validate`, which passes.

## Invariants and safety rules

- No workflow file changes; no dispatch of any workflow (issue 604 scope item 4). AGENTS.md hard safety rules 2, 3 and 8 are not touched.
- No expected outcome, verdict or file under `results/` changes.
- `tests/Parchwell.Verifier.Tests/**` is protected (`.github/CODEOWNERS`: `Parchwell.Verifier*/`) and a mutation input; Hari merges.
- No assertion is loosened. The fixture rule below governs the one existing test whose fixture the new page size rejects.

### Standing fixture rule

When an existing test's fixture is rejected by a new check, the implementer does exactly one of:

- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The pull request implementation notes list each change with the check that forces it. The cold review and Codex review check each listed change. Model: [issue 404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.

Known case: `CampaignTests.test_job_pagination` (`test_c5_campaign.py:113-120`) builds a first page of 100 jobs with `total_count` 101. Under requirement 4 that page is short, so `jobs` raises `missing job pages` (observed in the dry run). Apply (a): the fixture becomes a first page of 30 jobs (`list(range(30))`) and a second page `[30]`, both with `total_count` 31; the assertions become `31` jobs and `page=2` in the last call's suffix; the second case keeps `return_value={'total_count': 31, 'jobs': [1]}` and the `missing job pages` assertion. The boundary kept is "one full page plus one job". In the dry run, with this fixture, the whole C5 suite (`python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_*.py'`, from the repository root) ran 418 tests, all passing, in 597 seconds. That run did not yet contain the new test of the test map.

## Test map

All tests run from the repository root, as CI does: `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_*.py' -v`. `api` is patched with `unittest.mock.patch.object(campaign, 'api', ...)`, as the existing test does.

| Requirement | Test | Inputs | Expected |
|---|---|---|---|
| 1, 2, 3 | new `CampaignTests.test_job_page_size_and_paging` | `run_id` 7, `attempt` 2; `api` returns, in order, five pages of 30 jobs and one page of 29, each with `total_count` 179 (jobs numbered 0 to 178) | returns `total_count` 179 and `jobs == list(range(179))`; exactly 6 calls; call k's suffix is exactly `actions/runs/7/attempts/2/jobs?per_page=30&page=k` for k = 1 to 6 |
| 3 (boundary: exact multiple) | same test, second case | two pages of 30, `total_count` 60 | 60 jobs; exactly 2 calls; no `page=3` request |
| 3 (boundary: empty) | same test, third case | one page `{'total_count': 0, 'jobs': []}` | returns `{'total_count': 0, 'jobs': []}`; exactly 1 call, suffix ends `per_page=30&page=1` |
| 4 (boundary: one short) | same test, fourth case | first page of 29 jobs, `total_count` 179 | `ValueError` matching `missing job pages`; exactly 1 call |
| 4 | `test_job_pagination` (fixture under rule (a)) | as in "Known case" | 31 jobs, `page=2`; then `missing job pages` |
| 6 | same new test, fifth case | page 1: 30 jobs, `total_count` 179; page 2: 30 jobs, `total_count` 180 | `ValueError` matching `job count changed while paging` |
| 7 | same new test, sixth case | one page of 30 jobs, `total_count` 29 | `ValueError` matching `extra job pages` |
| 5 | same new test, seventh case | `api` returns `{'jobs': []}` | `ValueError` matching `malformed jobs response` |
| 8 | existing `CampaignValidatorRecoveryTests.test_pins_match_the_committed_files` (`test_c5_runtime.py`, unchanged) | committed files | `CAMPAIGN_VALIDATOR_PIN == sha256(c5_campaign.py)`; `CAMPAIGN_VALIDATOR_BASE` equals the committed qualification's shared input for the validator; `RUNNER_EXECUTION_PIN` unchanged |
| 9 | CI step `Qualify verifier mutation runtime` | pull request head and base | contract 5 |

The cases of the new test may be subtests of one method or separate methods; the names are the implementer's.

## Deployment assumptions

None accepted yet. D1 is listed under "Open questions for Hari" until Hari accepts it by name.

- D1. GitHub's `List jobs for a workflow run attempt` endpoint serves `per_page=30` on every page for a re-run attempt of the C5 coordinator. No offline test can cover it. Evidence, taken by the issue 499 control session on 2026-11-27 to 28 with `gh api` (each call a fresh process and so a fresh connection), against completed run 46039061248, which was idle, not under load: `attempts/2/jobs?per_page=30&page=N` returned pages 1 to 6, 179 jobs in total; `per_page=100` returned HTTP 502 five of five tries; `per_page=50&page=1` returned 50 jobs. The issue body records it; the cause is an inference, not verified, and GitHub's documentation lists only 200 and 404 for this endpoint. A 502 at 30 for a larger attempt or under load is not excluded.

## Acceptance commands

1. From the repository root: `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_*.py' -v`. Evidence: every test passes, including the new test and `test_pins_match_the_committed_files`.
2. `python3 -c "import hashlib;print(hashlib.sha256(open('tests/Parchwell.Verifier.Tests/Mutation/c5_campaign.py','rb').read()).hexdigest())"` equals `CAMPAIGN_VALIDATOR_PIN`.
3. `git diff --name-only origin/main...HEAD` lists exactly the three allowed files; `git diff origin/main...HEAD -- tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py` changes only the `CAMPAIGN_VALIDATOR_PIN` line.
4. `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base origin/main --head HEAD --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` and the same without `--base`: each prints `"status": "qualified"` and `"reasons": []`. The full clone history is needed, because `check` reads historical commits (`historical_qualification`, `c5_runtime.py:800`); a shallow clone gives `invalid_input`.
5. The `Dotnet verification` required checks pass on the pull request head, including `Qualify verifier mutation runtime`.

## Deferred work

1. End-to-end recovery on `main` (issue 604 scope item 4). On the verdict path: C5 campaigns that lose a shard depend on it, including issue 602's plan. Deferral is permitted because it needs a real lost shard after merge, and the issue forbids a dispatch to provoke one. Responsible: the control session of the issue whose C5 campaign next loses a shard to a runner fault. Stage: after merge, at the next such loss, when that session re-runs the failed jobs. Workload bound: no dispatch is added for this; it rides on an authorized campaign. Completion evidence: that attempt's `c5-summary-<run>-<attempt>` with `measurement_complete` true and `campaign.json` holding the shard's origin attempt. Exhaustion action: if the read fails again, record the failure and the response code in the evidence pack and hand off to Hari with it. Gate it blocks: closing issue 604.
2. Companion re-run artifact listing (`c5_companion_artifacts.py:166`, `per_page=100`). Off this pull request's scope by issue 604 scope item 1, because it reads a run's artifacts, not an attempt. Not observed for a re-run companion. Responsible: the same control session, at a recovery that re-runs the companion. Completion evidence: the downloader step logs `Verified <n> companion evidence archives.` on that attempt. Exhaustion action: record the failure and hand off to Hari with a proposed issue. Gate it blocks: none now.
3. Retry on HTTP 5xx in `api`. Optional; not in issue 604's scope. Responsible: none unless D1 fails in item 1, which then hands off.

## Plan-check questions

- (a) Steps whose effort scales with a number unknown at planning time: none in the pull request. Deferred item 1 waits for an event and adds no dispatch.
- (b) Where the plan is stricter than its authority: the issue asks for "a page size that the re-run attempt's jobs API serves"; the plan fixes 30, the only size observed to serve every page of the failing attempt (D1), and GitHub's documented default for the endpoint.
- (c) Checks that depend on behaviour neither on `main` nor in the pull request: GitHub's response for a re-run attempt at `per_page=30`. It is D1, under "Open questions for Hari".

## Size estimate

Estimate, 2026-11-28, basis: the dry run's diff of 9 inserted and 6 deleted lines in three files, plus the new test. About 3 files and 50 to 70 changed lines.

## Sources

- Issue 604 body (2026-11-28T05:43:36Z).
- GitHub REST documentation, "List jobs for a workflow run attempt", https://docs.github.com/en/rest/actions/workflow-jobs?apiVersion=2022-11-28, read 2026-11-28 with a web-fetch subagent: `per_page` "The number of results per page (max 100)", "Default: `30`"; `page` "Default: `1`"; response has required `total_count` and `jobs`; status codes listed are 200 and 404 only; nothing about carried-over jobs or 502.
- GitHub REST documentation, "Using pagination in the REST API", https://docs.github.com/en/rest/using-the-rest-api/using-pagination-in-the-rest-api, section "Changing the number of items per page", read 2026-11-28.
- Job log of aggregate job 140684256258, read 2026-11-28 with the GitHub MCP `get_job_logs` tool: `gh: Server Error (HTTP 502)` in the `c5_campaign.py prepare` step; `Verified 146 companion evidence archives.`.
- Base commit files: `c5_campaign.py:206-227`, `:253`, `:324`; `c5_runtime.py:329-343`, `:494-547`, `:2464-2500`; `test_c5_campaign.py:113-120`; `test_c5_runtime.py` `CampaignValidatorRecoveryTests`; `.github/workflows/dotnet-verification.yml:139-152`; `c5_companion_artifacts.py:166`.

No Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour is relied on.

## Open questions for Hari

1. Accept deployment assumption D1 by name: GitHub serves `per_page=30` on every page of a re-run C5 coordinator attempt. Evidence and how it was taken are under "Deployment assumptions". Recommendation: accept. The alternative is an `api` retry or a smaller size, neither in the issue's scope, and neither better evidenced.

## Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope, including any file outside "Scope", any constant in `c5_runtime.py` other than `CAMPAIGN_VALIDATOR_PIN`, or the qualification file.

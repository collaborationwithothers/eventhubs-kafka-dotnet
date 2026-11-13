# Backup cleanup contract-grade plan

This pull request adds a manual cleanup workflow for redundant backup branches. It keeps backups containing commits that are absent from both their matching branch and `main`, and records cleanup refusals without changing repository protection.

Revision: r1
PR name: Backup cleanup
Item: [#448](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/448)
Base: `2077964baaa918ccf7c671b5c2370511a10cc66f`, the current `main` read on 2026-11-13.

## Goal and authority

Authority in creation order is the issue body, updated `2026-11-13T08:39:54Z`. No decision or approval comment exists in the initial snapshot. The tool-naming comment `6848100009` assigns Codex. The label was reapplied after that comment, at `2026-11-13T08:39:54Z` (event `36008968438`). The earlier handoff `6848172156` is continuation evidence, not authority.

There is no parent item, split, state file or earlier contract-grade plan. Select the whole issue as one pull request. Its deliverable is the workflow, safe Git cleanup implementation, offline tests and the rule 12 amendment. This is operational governance work, with no scientific hypothesis, scenario configuration, fault, metric, repeat count, harness capability or expected-outcome file.

## Scope

Allowed authored implementation files:

- `AGENTS.md`, only "Before opening a pull request" rule 12.
- `.github/workflows/cleanup-wip-branches.yml`, new.
- `docs/operations/workflows/cleanup-wip-branches.yml.txt`, new byte-for-byte workflow copy.
- `scripts/cleanup-wip-branches.py`, new Python standard-library implementation.
- `infra/scripts/tests/test_cleanup_wip_branches.py`, new tests discovered by existing lifecycle verification.
- `items/448/state.md` and `items/448/receipt.md`, normal implementation records.

The plan file belongs on `codex/448-plan`, not in the implementation diff. Do not change other workflow files, existing workflow triggers, rulesets, bypass actors, credentials, Azure configuration, infrastructure, scenario definitions, `expected.yaml`, verifier code or `results/`. A schedule is a follow-up for Hari after an observed dispatch refusal, not part of this pull request. No new package, project or assembly reference is needed.

The existing changed file was read on current `main`: `AGENTS.md:118` owns rule 12. New files have no existing text. Existing lifecycle CI discovers `infra/scripts/tests/test_*.py` at `.github/workflows/dotnet-verification.yml:76-78`; its actionlint invocation covers every other workflow at `infra/scripts/lifecycle/check-offline.sh:155-163`. Neither file needs an edit.

## External contracts

### Workflow

- Path: `.github/workflows/cleanup-wip-branches.yml`; display name: `Cleanup backup branches`.
- Sole trigger: `workflow_dispatch`, with no inputs. Global permissions exactly `contents: write`; no job widens them. No Azure identity or private-network runner is used.
- One job on `ubuntu-24.04`, with `timeout-minutes: 20`. The job executes only when `github.repository == 'collaborationwithothers/eventhubs-kafka-dotnet'` and `github.ref == 'refs/heads/main'`.
- Concurrency group `cleanup-wip-branches`, `cancel-in-progress: false`, separate from infrastructure workflows.
- Checkout uses `actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1`, `ref: ${{ github.sha }}`, `fetch-depth: 0`, and `persist-credentials: true`. Persisted credentials are needed for this workflow's Git push and are removed by checkout's post-job cleanup [E4]. They are the workflow token, never Azure credentials or a new secret.
- Invoke `python3 scripts/cleanup-wip-branches.py`. No user-provided shell fragment, ref, command or repository is accepted. The script operates on `origin` in the checkout and writes its report to standard output and, when present, the file named by `GITHUB_STEP_SUMMARY`.
- The committed workflow copy is byte-for-byte equal to the workflow, including after review fixes.

### Eligibility and deletion

Let `W` be the full remote branch ref ending with exactly the suffix `-wip`. Let `B` be its name with exactly one final `-wip` removed. Use complete commit history and immutable object IDs, never textual prefix matching, timestamps, patch equivalence or merge status.

`W` is eligible precisely when its observed head is an ancestor of the observed head of either the existing branch `B` or `refs/heads/main`. Equal commits count as ancestry. A missing `B` leaves only the `main` comparison. A failed comparison is an error, not a negative result. `git merge-base --is-ancestor` returns 0 for ancestry, 1 for its absence, and another nonzero status for an error [E1].

Deletion addresses exactly one fully qualified `refs/heads/...-wip` ref using `git push --force-with-lease=<full-W-ref>:<observed-W-SHA> origin :<full-W-ref>`. The explicit lease protects the observed backup head [E2]. Never use an unqualified force, a wildcard push/deletion, `--mirror`, or a REST deletion without an expected-head guard.

Before deleting, re-read the current remote backup and the chosen preserving ref and require their heads to equal the object IDs used for ancestry. If either changed or disappeared, retain the backup as `retained_changed` without retry. The lease additionally catches a backup update after that re-read. The preserving-ref check is a fresh comparison, not a transactional lock on a matching branch: deletion demonstrates ancestry to the recorded preserving head. The workflow makes no claim that it prevents another writer from subsequently rewriting or deleting that preserving branch.

### Report and exit status

Use heading `Backup branch cleanup` and a table with columns `Branch`, `Head`, `Status`, `Preserved by`, `Reason`. Escape repository-provided text for Markdown and never print credentials. Every enumerated backup has exactly one row, with one of these literal statuses:

- `deleted`: deletion succeeded; preserving full ref and head are recorded.
- `retained_unique`: both available ancestry comparisons were negative; reason `head is not an ancestor of matching branch or main`. This describes uniqueness relative to those refs only.
- `retained_changed`: a ref changed/disappeared before deletion, or an exact-head lease was rejected; reason names the observed change without claiming deletion.
- `retained_error`: listing, fetching, ancestry or deletion could not be established; reason names the failed operation.
- `deferred_limit`: beyond the run's branch cap; reason `100-branch processing limit reached`.

Print computed totals for every status and the enumerated total; an empty run reports zero branches. A global listing failure has a report-level error and no invented branch rows. Exit 0 when all rows are `deleted`, `retained_unique`, `retained_changed` or `deferred_limit`; exit 1 for a report-level error or any `retained_error`. A generic deletion refusal is an error unless an independent current-ref read establishes a changed or already absent backup. Do not turn authentication, transport or protection errors into a unique-commit finding.

### Rule 12 wording

In `AGENTS.md`, preserve rule 12's existing wording and append these sentences immediately after "and deletes it once its own branch is pushed.":

> If the session proxy refuses deletion, the session records the remaining backup branch in its handoff or completion comment. The manual `cleanup-wip-branches.yml` workflow checks remaining backup branches and deletes only those whose head is an ancestor of their matching branch or `main`; backups with unique commits are retained and listed in its run summary.

This is the only normative governance amendment. Other files may cite rule 12 rather than restating its session obligations. The existing backup exemption, no-backup-PR requirement and all safety/merge rules remain in force.

## Requirements and enforced checks

1. WHEN the workflow is invoked outside the named repository or outside `refs/heads/main`, THEN its cleanup job is skipped. WHEN the entry point runs, THEN it also checks `GITHUB_REPOSITORY`, `GITHUB_REF` and `origin`'s HTTPS URL against this repository before any remote mutation; absent/mismatched context exits 1 without deletion. Private test seams may use local bare repositories, but the production entry point accepts no local-remote override.
2. WHEN remote branch heads are listed, THEN only refs in `refs/heads/` with the final `-wip` suffix become candidates, sorted by full ref name. Tags, similarly named non-backup branches and other refs are untouched. An empty derived matching branch cannot authorize deletion through a matching ref, but may still qualify through `main`.
3. WHEN a candidate is examined, THEN fetch complete history for its observed head and available comparison heads and prove the ancestry contract above. Reject a shallow or incomplete comparison; never equate an error with a safe deletion. If `main` is missing or unreadable, retain as error rather than silently operating against another repository state.
4. WHEN ancestry is proven, THEN re-read the backup and preserving head, and delete only with the exact-head lease. Changed refs are retained without retry. Normal branches, tags and `main` are never push targets.
5. WHEN no available comparison contains the backup, THEN retain it with `retained_unique`. WHEN matching branch is absent but `main` contains it, THEN deletion is allowed. WHEN the backup equals a preserving head, THEN deletion is allowed.
6. WHEN an operation errors or times out, THEN fail closed, retain the affected unconfirmed backup and report the error. Continue other bounded candidates after a per-branch error, and exit 1 at the end. Do not retry failed mutations or use broader permissions, a bypass, or direct session deletion as a workaround.
7. WHEN there are more than 100 backup candidates, THEN examine only the first 100 and mark all others `deferred_limit`. This is `record the gap and continue`: report the count and defer the remaining work. Bound each Git subprocess to 60 seconds and the entire script to 15 minutes; on overall exhaustion mark unprocessed candidates `retained_error`, emit the available report and exit 1. These are policy bounds, not measured performance claims.
8. WHEN a run finishes or encounters a controlled failure, THEN emit the specified report and computed totals. A timeout or failure before the script can start remains a failed Actions job, not a success report.
9. WHEN offline tests run, THEN they exercise real Git ancestry and leased deletion against temporary bare repositories, without network or GitHub credentials. Use command stubs only for injected errors, timeouts, ref races and production-context checks.
10. WHEN the change is published, THEN the workflow copy matches exactly, the rule amendment has its literal wording, and all existing triggers remain unchanged. The new test module is automatically included in lifecycle test discovery.
11. WHEN Hari merges the protected pull request and the workflow exists on `main`, THEN the owning implementation session attempts exactly one cloud-session REST dispatch against `main` and records its response. On refusal, it records that evidence and hands the follow-up daily-schedule decision to Hari. It never adds a schedule or changes a trigger itself. On acceptance, observe that single run and record its summary and outcome; never infer cleanup success from dispatch acceptance.

## Invariants and safety rules

Keep backup creation. Never delete a branch based on a name alone. Never execute a backup branch's content. Branches are data passed as subprocess argument arrays, with no shell interpolation. Inspect full ref names so even valid names containing shell metacharacters cannot invoke commands.

The workflow modifies only eligible backup refs. It neither updates preserving refs nor weakens their protection. No session holds Azure credentials, applies Terraform, dispatches an off-main workflow, or changes public results. Hari merges this pull request because it changes protected governance, scripts and workflow paths. No agent creates a bypass actor or edits the ruleset.

## Test map

Use named unittest cases below in `infra/scripts/tests/test_cleanup_wip_branches.py`; implementation may organize helpers freely. Commit labels `A`, `B` and `C` below mean actual distinct synthetic Git commits with parent edges explicitly described, not literal hashes. Assert the actual generated SHA and the stated literal status, counts and exit code.

| Test | Requirements | Inputs and literal expected results |
|---|---|---|
| `test_production_context_fails_closed` | 1 | Repository missing/wrong; ref missing, tag or feature branch; origin local, unrelated HTTPS or credential-bearing URL. Each exits 1 and issues zero push commands. Exact HTTPS repository URL, with or without `.git`, is accepted in a stubbed valid context. |
| `test_only_final_wip_heads_are_candidates` | 2 | Head `codex/a-wip`, head `codex/a-wip-extra`, head `codex/wip`, tag `tag-wip`. Exactly `refs/heads/codex/a-wip` is a candidate and the other refs remain. |
| `test_ancestor_of_matching_branch_is_deleted` | 3-5 | `A -> B`; backup A, matching B, main a divergent C. Backup absent afterwards; `deleted=1`, exit 0; main and matching heads unchanged. |
| `test_equal_matching_head_is_deleted` | 3-5 | Backup and matching at B; main divergent C. Status `deleted`, exit 0. |
| `test_merged_backup_without_matching_branch_is_deleted` | 3-5 | `A -> B`; backup A, matching absent, main B. Status `deleted`, preserved by `refs/heads/main` and B, exit 0. |
| `test_unique_backup_is_retained` | 3,5 | A forks to B and C; backup B, matching C, main A. Exact backup B remains; status `retained_unique`, deleted 0, exit 0. Repeat with matching absent. |
| `test_missing_main_or_history_is_error` | 3,6 | Main absent; fetch exit 128; shallow comparison; missing commit; ancestry exit 128. Each affected backup remains, status `retained_error`, zero pushes for it, exit 1. Ancestry exit 1 alone is a negative comparison. |
| `test_backup_changed_before_delete_is_retained` | 4 | Start with eligible A; move backup to unique C before re-read. Status `retained_changed`, C remains, exit 0. |
| `test_preserving_ref_changed_is_retained` | 4 | Backup A is eligible through matching B; move or delete matching before re-read. Backup A remains, status `retained_changed`, zero pushes, exit 0. |
| `test_exact_lease_preserves_racing_backup` | 4 | Eligible backup A passes re-read; update backup to C just before actual leased push into the bare remote. Push is rejected; C remains; status `retained_changed`, exit 0. Assert full ref and expected A in the push arguments. |
| `test_permission_transport_and_timeout_errors_are_visible` | 6,8 | Inject deletion refusal, listing failure and 60-second subprocess timeout. No fallback deletion; exit 1; report contains operation error and affected branch where known. Another independent eligible candidate still runs after a branch error. |
| `test_branch_processing_boundaries` | 7 | Synthetic lists of 0, 99, 100 and 101 backup refs. Process respectively 0, 99, 100 and 100; deferred counts 0, 0, 0 and 1. Exactly one row per enumerated ref. Inject overall elapsed time at 899, 900 and 901 seconds; no work starts at/after 900, unprocessed rows are `retained_error`, exit 1. No real sleep. |
| `test_report_is_complete_and_escaped` | 8 | One row of each status and valid branch text containing `&`, `<` or backtick. Totals equal row counts; report escapes text, records preserving SHA and never includes the synthetic credential marker. Zero-candidate run reports 0, exit 0. |
| `test_workflow_contract_and_mirror` | 1,10 | Load new YAML using the existing offline-tool PyYAML dependency with string-preserving keys. Assert sole dispatch trigger, no inputs, exact permissions, job condition, runner, timeouts, concurrency, checkout pin/options, script invocation and byte equality of the mirror. Existing workflow trigger expectations retain their existing tests. |

For static published rule wording, verify the exact appended sentences by review and a source comparison in the receipt; do not add a test that merely mirrors prose. If any existing fixture is rejected by a new check, apply the plan-pr standing fixture rule: either change only its fixture while preserving every assertion and boundary, or, when invalid input is its purpose, keep the input and set its expectations exactly to this plan's output. Never remove a case, loosen an assertion or change the check. List each such fixture change and its forcing check in the receipt.

## Deployment assumptions and plan-check answers

- Git's ancestry and exact-head deletion behavior is covered offline by actual Git/bare-remote tests, including a racing writer. Python 3.12 and Git are checked in the hosted entry path; unsupported/missing tools fail before deletion. No container, image, chart, AKS runtime, live connection or load state is involved.
- Checkout authentication follows its pinned source [E4]. Hosted token/proxy write access is not assumed to succeed: requirement 11 measures one dispatch and its resulting run after merge; errors remain reported failures. There is no pre-merge live operation or external producer dependency. The offline workflow test covers the requested authority and main-only execution guard.
- Ruleset `24907776` was read through `gh api` on 2026-11-13. Its condition is exactly `include: ["~DEFAULT_BRANCH"]`, `exclude: []`; its deletion rule therefore does not target backup branches. The effective-rules endpoint for `codex/448-cleanup-wip` returned `[]`. This is a dated configuration observation, not authority to change future protection. Re-read effective rules before the post-merge dispatch; any refusal remains a failure and a ruleset decision belongs to Hari.
- Unknown-size work is bounded: 100 candidates per run, subprocess and overall timeouts, one live dispatch, one observed workflow run, and the existing cumulative cold-review budget. For branch-cap exhaustion, record the gap and continue; for overall timeout, fail closed and record unprocessed rows. For a required authored scope or review-budget expansion, use the stop rule.
- There is no parent plan. Exact-head protection, context guards, bounded processing and fail-closed errors make the issue body's ancestor criterion executable; they do not relax it or grant a new deletion category. The concurrent preserving-ref observation limitation is stated explicitly above.
- No tests depend on code, data or behavior outside current `main` or this pull request except Git, the pinned checkout action and GitHub's workflow/dispatch APIs, cited in E1-E4 and tested or measured as stated. No deployment assumption about a container, image, chart or live runtime remains uncovered.

## Acceptance commands and evidence

Run from the implementation checkout. Record commands, actual source SHA, result summaries and warning dispositions in `items/448/receipt.md`; do not commit generated output.

1. `python3 -m unittest discover -s infra/scripts/tests -p test_cleanup_wip_branches.py -v`, using the existing pinned offline-tool environment when PyYAML is needed: all mapped cases pass without network or live mutation.
2. `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`: existing lifecycle cases plus new cases pass. Existing required hosted CI remains the final verification gate; no new .NET behavior is introduced.
3. `bash infra/scripts/lifecycle/check-offline.sh` with the repository's existing installed pinned tools: workflow parsing, actionlint and existing offline checks pass. New workflow lint is included by the existing glob. If tools are unavailable, install them through the existing repository installer rather than skipping the check.
4. `cmp .github/workflows/cleanup-wip-branches.yml docs/operations/workflows/cleanup-wip-branches.yml.txt` and `git diff --check`: no differences or whitespace errors.
5. `npm ci --ignore-scripts` if locked dependencies are absent; `npm run check:docs`: scan completes, changed-file warnings are fixed or given the rule 8 disposition. Run `npm run test:docs` only if a failure or required existing gate justifies it; no docs tooling changes are planned.
6. Before publication, perform the required cold review under AGENTS.md rule 8. Budget: at most two comprehensive and two focused rounds cumulatively, with the rule's precise exhaustion and P2 treatment. Push `-wip` and hand off to Hari if the budget is exhausted or focus is unsafe. Required hosted checks and final-head Codex review remain mandatory. Protected merge waits for Hari.
7. After merge, refresh issue authority and effective backup-branch rules. Attempt exactly once: `gh api -X POST repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/workflows/cleanup-wip-branches.yml/dispatches -f ref=main`. Capture response status/body without secrets and do not retry a denied POST. If accepted, identify and observe that one run using REST APIs, and link its summary. If rejected, record the response and the remaining backup refs; hand off the daily-schedule follow-up to Hari, without changing the trigger. Dispatch success alone does not complete cleanup verification.

## Sources

Repository sources are pinned to base `2077964baaa918ccf7c671b5c2370511a10cc66f`:

- [Issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/448), updated at the time recorded above; initial metadata and comment snapshot retained outside Git. Tool assignment is [comment 6848100009](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/448#issuecomment-6848100009).
- [AGENTS.md](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/2077964baaa918ccf7c671b5c2370511a10cc66f/AGENTS.md#L118), rule 12; governance safety and protected merge requirements in the same file.
- [Factory lifecycle and authority](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/2077964baaa918ccf7c671b5c2370511a10cc66f/docs/agents/factory.md#L100), plus its Authority reads section.
- [Blueprint scenario-runner restrictions](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/2077964baaa918ccf7c671b5c2370511a10cc66f/docs/blueprint.md#L155), workflow infrastructure ownership at ADR-020 and scope cuts at section 12. This workflow does not execute an inventory experiment or add a deferred feature.
- [Lifecycle test discovery](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/2077964baaa918ccf7c671b5c2370511a10cc66f/.github/workflows/dotnet-verification.yml#L76), [workflow validation](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/2077964baaa918ccf7c671b5c2370511a10cc66f/infra/scripts/lifecycle/check-offline.sh#L155), and [existing PyYAML test dependency](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/2077964baaa918ccf7c671b5c2370511a10cc66f/infra/scripts/tests/test_tools_offline.py#L10).
- [Documentation check contract](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/2077964baaa918ccf7c671b5c2370511a10cc66f/docs/engineering/documentation-checks.md#L3), and [pull-request rules 1, 2 and 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/2077964baaa918ccf7c671b5c2370511a10cc66f/docs/agents/pull-requests.md#L18).
- Ruleset observation: `GET /repos/collaborationwithothers/eventhubs-kafka-dotnet/rulesets/24907776`; effective-rules observation: `GET /repos/collaborationwithothers/eventhubs-kafka-dotnet/rules/branches/codex%2F448-cleanup-wip`, read 2026-11-13 using `gh api`.

Official external documentation was read on 2026-11-13 using the `search_service.web_run` documentation tool:

- E1: [Git merge-base 2.43.0, --is-ancestor](https://git-scm.com/docs/git-merge-base/2.43.0#Documentation/git-merge-base.txt---is-ancestor), documented return values and ancestry predicate.
- E2: [Git push 2.55.0, --force-with-lease and deletion refspecs](https://git-scm.com/docs/git-push/2.55.0), explicit expected-value lease and empty-source ref deletion. The offline suite verifies their combination rather than assuming a REST delete offers an equivalent guard.
- E3: [GitHub workflow syntax, permissions and manual triggers](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#permissions) documents that unspecified token permissions are none; [create workflow dispatch](https://docs.github.com/en/rest/actions/workflows#create-a-workflow-dispatch-event) documents the POST route and `ref`. Repository policy narrows its allowed ref to `main`.
- E4: [Pinned checkout README](https://github.com/actions/checkout/blob/3d3c42e5aac5ba805825da76410c181273ba90b1/README.md#usage) documents full-history fetch and persisted Git authentication with post-job removal.

No Kafka, librdkafka, Confluent.Kafka or Azure behavior is relied on or asserted.

## Open questions for Hari

None.

## Stop rule

Stop and hand off under AGENTS.md GOVERNANCE "Handoffs" when implementation needs to change a contract, requirement or scope. Do not use failed cleanup as authority to widen permissions, weaken ancestry, rewrite rulesets or add a schedule. A post-merge dispatch refusal is the issue's specified handoff to Hari, with measured evidence and no retry loop. A plan check by a Codex subagent does not supply standing approval under the current factory restriction; the calling session applies that approval rule separately.

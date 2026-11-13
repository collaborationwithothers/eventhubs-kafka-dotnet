# Plan: order the verification gates and add the pre-PR local tier (gate-order)

Issue: [#440](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440). This contract-grade plan covers the whole issue as one pull request, `gate-order`. It writes the adopted gate order into `docs/agents/pull-requests.md`, makes `CLAUDE.md` and the `implement-plan` skill cite that one statement, and adds the `verify-dotnet.sh` tier that the adopted pre-PR gate needs, because no existing option expresses it. Nothing is open for Hari.

Revision: r1. Planned against `main` at `3cb6875b8af8daa0269bb6495134b83e91353153`.

## 1. Goal and authority

Goal: a full local test run starts only on source that cold review has cleared, and that run excludes only the tests a managed sandbox cannot host. Today `CLAUDE.md` "Before opening a pull request" item 1 asks for the full local suite before the cold review of item 2, so a review finding after the run throws the run away. The issue records at least about 40 minutes of such thrown-away runtime on pull request 439.

Authority, in creation order:

1. The issue body (created 2026-11-13T07:05:50Z, `updated_at` 2026-11-13T08:27:59Z). Hari applied `ready-for-agent` at 2026-11-13T07:12:10Z; no comment on the issue names a tool, so Claude Code implements (docs/agents/factory.md "Item lifecycle" step 2, AGENTS.md "Implementer and reviewer"). The body states the evidence and proposes four changes.
2. Decision [6847972415](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6847972415) (created 2026-11-13T08:27:59Z), headed "## Decision". It adopts proposals 1 and 2 of the body with this wording: gate order "focused tests, then cold review, then the pre-PR local gate once, on the reviewed source. No local gate starts while a review round is open on the same source."; pre-PR local gate "`./scripts/verify-dotnet.sh` with every test except `Tier=CGroupV2` (named `Tier=Host` until [#445](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445) merges), recorded in the receipt as a partial run with that exclusion. Docker-tier tests stay in the local gate."; pre-merge gate "the hosted `.NET verification` full gate on the final head, every test including the cgroup tier. The merge rule is unchanged."; and the implementing scope "amends CLAUDE.md "Before opening a pull request" item 1, `docs/agents/pull-requests.md` and the `implement-plan` skill to match, and adds the script option the exclusion needs, if `--project` and `--tier` cannot express it". It closes: "Proposals 3 and 4 of the body are not decided here."

No approval comment exists on the issue. No item state file exists (`items/440/state.md` is absent on `main`). There is no parent item and no split, so the whole issue is the candidate (docs/agents/factory.md "Item lifecycle" step 3).

Issue #445 has merged: on `main` at `3cb6875b`, the trait value is `CGroupV2` (`tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptEndToEndTests.cs` line 21, `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptLocalCgroupCaptureTests.cs` line 12, `scripts/verify-dotnet.sh` line 58). The decision's parenthesis about `Tier=Host` is therefore spent, and this plan uses `Tier=CGroupV2` throughout.

Why a new option is needed. `scripts/verify-dotnet.sh` lines 56 to 63 offer two tiers: `all` adds no filter, `fast` passes `--filter-not-trait Tier=Docker Tier=Slow Tier=CGroupV2`. `fast` also removes the Docker tests, which the decision keeps in the local gate, so it cannot express the adopted gate. `--project` selects test projects, not traits (lines 73 to 88, 341 to 349), and the two `Tier=CGroupV2` tests sit in `Parchwell.LoadGen.Tests` beside tests the gate must run, so dropping that project would drop far more than the exclusion. The condition in the decision ("if `--project` and `--tier` cannot express it") is therefore met.

## 2. Scope

Files this pull request may change:

- `docs/agents/pull-requests.md`: one new section, "Verification gates" (R1), plus the two cross-reference sentences of R2. No rule text changes otherwise.
- `CLAUDE.md`: item 1 of "Before opening a pull request" only (R3).
- `.agents/skills/implement-plan/SKILL.md`: steps 2 and 3 only (R4). `.claude/skills/implement-plan` is a symlink to this folder (`ls -la .claude/skills`), so no second copy is edited.
- `scripts/verify-dotnet.sh`: the usage text, the tier `case`, the unknown-tier message and the spike-evidence condition (R5 to R8).
- `scripts/test-verify-dotnet.sh`: the new cases and added assertions of section 6.
- `docs/engineering/dotnet-verification.md`: "Tiers, partial runs and failure reporting" and the `tier` row of the `summary.json` key table (R9).
- `items/440/`: this plan file, the authored receipt and the item state file.

Must not change:

- `AGENTS.md`. Its "Before opening a pull request" already says that rules 1 to 10 live in `docs/agents/pull-requests.md`, and this plan adds a named section, not a numbered rule, so no renumbering is needed and rules 11 to 13 keep their meaning.
- Anything under `.github/workflows/` and `docs/operations/workflows/` (AGENTS.md hard safety rule 8). The hosted gate is unchanged.
- `scripts/check-dotnet-matrix.py` and `scripts/tests/test_check_dotnet_matrix.py`. The aggregate checker requires `tier == "all"` in each hosted job's `summary.json` (`scripts/check-dotnet-matrix.py` line 57); hosted jobs pass only `--project`, so the new tier never reaches it.
- Any .NET source, test, `.csproj`, `Directory.*.props`, lock file, `global.json`, `scripts/dotnet-verification.proj`, `scripts/verify-kafka-coverage.sh`, `scripts/verify-worker-images.sh`.
- `results/`, `scenarios/`, any `expected.yaml`, `docs/blueprint.md`, `skills-lock.json`, any other skill.

Non-goals, from the decision's last line:

- Proposal 3 of the issue body (plan-check lists the existing assertions and fixtures the planned checks will break) is not implemented. The `plan-check` skill and `docs/agents/factory.md` are not changed. This plan's own standing fixture rule in section 6 is the existing rule of docs/agents/factory.md "Item lifecycle" step 1 practice, not that proposal.
- Proposal 4 of the issue body (control-session rules on analyzer-only builds, per-test triage and resuming agents after an environment transition) is not implemented.

Protected paths: every file this pull request changes except `items/440/` is protected. `.github/CODEOWNERS` on `main` at `3cb6875b` lists `/docs/`, `/.*/` (which covers `.agents/`), `/*.md` (which covers `CLAUDE.md`) and `/scripts/`, and mirrors the ruleset's required reviewers rule (docs/agents/factory.md "Ruleset"). The pull request therefore waits for Hari's approval and merge under AGENTS.md GOVERNANCE "Merge rule"; the implementing session never merges it.

## 3. External contracts

### 3.1 The published gate order

`docs/agents/pull-requests.md` gains one section, placed after rule 10 at the end of the file. This is the single statement of the rule; `CLAUDE.md`, the `implement-plan` skill and `docs/engineering/dotnet-verification.md` cite it and never restate it (docs/agents/factory.md "Item lifecycle" step 3). Exact text:

```markdown
## Verification gates

This section owns the order of the verification gates and what each one is. Other files cite it rather than restating it. Basis: [decision 6847972415](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6847972415) on issue #440, which adopts proposals 1 and 2 of that issue's body. The order is a governance choice; the saving it is expected to produce is not measured.

1. Focused tests. Run the tests that cover the change, and only those, while the change is still moving. `./scripts/verify-dotnet.sh --tier fast` and `./scripts/verify-dotnet.sh --project <name>` are partial runs for this step, and are cited as partial runs.
2. Cold review, under rule 8.
3. The pre-PR local gate, once, on the source the cold review cleared: `./scripts/verify-dotnet.sh --tier sandbox`, which runs every test except those marked with the xUnit trait `Tier=CGroupV2`. The Docker tests stay in it. The authored receipt records it as a partial run with that exclusion, never as the full gate.
4. The pre-merge gate: the hosted `.NET verification` check on the pull request's final head, which runs every test, the `Tier=CGroupV2` tests included. GOVERNANCE "Merge rule" is unchanged.

No local gate starts while a review round is open on the same source. A review round is open from the moment its inputs are frozen under rule 5 until its complete report arrives. A change made after step 3, including a correction from a review finding or a hosted finding, is authored work: it needs the coverage rule 8 requires, and step 3 then runs again on the source that coverage cleared.

Steps 1 and 3 apply to a pull request that changes .NET source or tests. For any other change, rule 13 decides which verification suits it, and rule 8's documentation checks apply as they do to every pull request.
```

### 3.2 `verify-dotnet.sh` command line

The script accepts `--tier sandbox` and `--tier=sandbox`, alongside the existing `all` and `fast`. Its behaviour:

| Property | Value for `sandbox` |
|---|---|
| Filter passed to every `dotnet test` call | `--filter-not-trait Tier=CGroupV2`, and no other filter argument |
| Spike-evidence publication | runs, as for `all` |
| Final line | the partial line, `Partial verification passed (tier sandbox, projects: ...). This is not the full gate: it does not satisfy a requirement to run ./scripts/verify-dotnet.sh with no options.` |
| `summary.json` `tier` | `"sandbox"` |
| `summary.json` `outcome` on a clean run | `"partial_passed"` |

The unknown-tier message becomes exactly `Unknown tier: <value> (expected all, fast or sandbox).` on stderr, with exit status 2 and no `dotnet` call, as today.

`--filter-not-trait` is the xUnit v3 runner option already used by the `fast` tier. Its help text on SDK 10.0.402, read in this session (section 9), is "Do not run any methods with a given trait value. Pass one or more name/value pairs (i.e., 'name=value')", so one pair is a valid argument list.

## 4. Requirements

- **R1 Gates section.** WHEN the pull request is complete, THEN `docs/agents/pull-requests.md` ends with the section of 3.1, byte for byte, and no other rule text in that file has changed.
- **R2 Gates section is reachable.** THEN the paragraph of `docs/agents/pull-requests.md` that begins "Before implementation, put a short execution card" and the "Cold-review protocol and cumulative budget" paragraph of rule 8 each gain one sentence citing "Verification gates" by name: in the execution card, that the gates and their order are in that section; in rule 8, that the local gate runs after the review round closes, as that section sets. Neither sentence restates the order.
- **R3 CLAUDE.md item 1.** THEN item 1 of `CLAUDE.md` "Before opening a pull request" reads exactly:

  `1. Run the verification gates in the order docs/agents/pull-requests.md "Verification gates" sets: focused tests, then the cold review of item 2, then the pre-PR local gate once, on the reviewed source. That section names each gate's command. Warnings are errors for builds; documentation warning dispositions follow AGENTS.md "Before opening a pull request" rule 8. Verification repetition follows that section's rule 13.`

- **R4 implement-plan skill.** THEN step 2 of `.agents/skills/implement-plan/SKILL.md` requires the verification gates in the order of docs/agents/pull-requests.md "Verification gates", naming that section and no gate command, and keeps its existing sentences on subagents, readers, rule 13, the state file, the plan file, the stop rule and the receipt. Step 3 states that the pre-merge gate is the hosted check of step 4 of that section. No other step changes.
- **R5 New tier accepted.** WHEN the script is run with `--tier sandbox` or `--tier=sandbox`, THEN every `dotnet test` call receives `--filter-not-trait Tier=CGroupV2` and no other filter argument, and the run is otherwise the run that `--tier all` performs for the same `--project` selection.
- **R6 Unknown tier.** WHEN `--tier` names any value other than `all`, `fast` or `sandbox`, THEN the script prints `Unknown tier: <value> (expected all, fast or sandbox).` to stderr, makes no `dotnet` call, creates no results directory and exits 2.
- **R7 Spike evidence.** WHEN the Kafka test project runs and the tier is `all` or `sandbox`, THEN the script publishes the spike evidence as it does today for `all`. WHEN the tier is `fast`, THEN it does not. The comment above that condition states the reason: the evidence comes from the Docker spike test, which only the fast tier leaves out.
- **R8 Usage text.** THEN the usage text lists `--tier fast|sandbox|all` in its synopsis and gives `sandbox` one description line: it leaves out only the tests marked `Tier=CGroupV2`, which need the host's cgroup v2 memory files, and it is the pre-PR local gate of docs/agents/pull-requests.md "Verification gates". `--help` still exits 0, creates no results directory and keeps the `summary.json` sentence the existing test asserts.
- **R9 Engineering doc.** THEN `docs/engineering/dotnet-verification.md` states, in "Tiers, partial runs and failure reporting": that `--tier sandbox` passes `--filter-not-trait Tier=CGroupV2` to every test project and leaves the Docker and Slow tests in; that it publishes the spike evidence, as `--tier all` does, and that `--tier fast` does not; that a `sandbox` run prints the partial line and is cited as a partial run; and that it is the pre-PR local gate defined in docs/agents/pull-requests.md "Verification gates", linked, which is where the rule lives. The `tier` row of the `summary.json` key table becomes `"all"`, `"fast"` or `"sandbox"`. The sentence that the local full gate stays `./scripts/verify-dotnet.sh` with no options is kept.
- **R10 Item state file.** THEN `items/440/state.md` exists in the fields and order of docs/agents/factory.md "Item state file", listing the issue body and decision 6847972415 under "Decisions in force", "Open questions: None", and the implementing tool Claude Code with the source "no comment on the issue or its listed parents before `ready-for-agent`; Claude Code by default".

## 5. Invariants and safety rules

- The hosted pre-merge gate is unchanged: no file under `.github/workflows/` is touched, so AGENTS.md hard safety rules 8 and 9 are not engaged, and the required check keeps its name and its full-test run.
- No gate is weakened. The set of tests the pre-PR local gate runs grows against today's practical alternative on a sandbox (`--tier fast`), and every test still runs in the hosted gate before merge. The only tests the new tier never runs locally are the two marked `Tier=CGroupV2`.
- The rule is stated once, in `docs/agents/pull-requests.md`; `CLAUDE.md`, the skill and the engineering doc cite it (docs/agents/factory.md "Item lifecycle" step 3).
- No number in the new text is a measurement. The only figures cited stay in the issue, and the published text calls the order a governance choice (AGENTS.md hard safety rule 11).
- No secret, credential, tenant or subscription identifier enters any changed file (hard safety rule 7). No Azure access is involved (hard safety rules 1 to 3).
- The pull request touches protected paths, so the implementing session does not merge it; it hands off to Hari under AGENTS.md GOVERNANCE "Handoffs" after Codex review.

## 6. Test map

All automated tests are cases of `scripts/test-verify-dotnet.sh`, which runs against the stub `dotnet` already in that file and needs no SDK and no Docker. Each new case changes one thing against the existing fixtures, so no other behaviour can mask it.

| Test | Requirement | Input | Expected |
|---|---|---|---|
| T1 | R5 | `run_case sandbox -- --tier sandbox` | exit 0; 6 `TEST ` lines; each of the 6 contains `--filter-not-trait Tier=CGroupV2`; the log contains neither `Tier=Docker` nor `Tier=Slow` |
| T2 | R5 | the T1 run | stdout contains `Partial verification passed (tier sandbox, projects: all). This is not the full gate` and not `Verification passed. Per-project reports` |
| T3 | R5 | the T1 run's `summary.json` | `.outcome == "partial_passed" and .tier == "sandbox" and .projects == null` |
| T4 | R5 | `run_case sandbox_equals -- --tier=sandbox` | exit 0; the same 6 filtered `TEST ` lines as T1; `.tier == "sandbox"` |
| T5 | R7 | the T1 run | the log holds exactly 1 line matching `msbuild scripts/dotnet-verification.proj -t:PublishSpikeEvidence` |
| T6 | R7 | the existing `default` case (tier `all`) | the same count is exactly 1 |
| T7 | R7 | the existing `fast` case | the same count is exactly 0 |
| T8 | R6 | the existing `unknown_tier` case, `--tier slow` | exit 2; no `dotnet` call; no results directory; stderr is exactly `Unknown tier: slow (expected all, fast or sandbox).` |
| T9 | R8 | the existing `help` case | exit 0; output contains `--tier fast|sandbox|all` and a line naming `Tier=CGroupV2`; the existing `summary.json` assertion still passes |
| T10 | R5 | `run_case sandbox_project -- --tier sandbox --project Parchwell.Scenarios.Tests` | exit 0; exactly 1 `TEST ` line, for `Parchwell.Scenarios.Tests`, carrying the `Tier=CGroupV2` filter; `.tier == "sandbox" and .projects == ["Parchwell.Scenarios.Tests"]` |

T8 and T9 add assertions to cases that exist on `main` (lines 154 to 165 of `scripts/test-verify-dotnet.sh`); T6 and T7 add assertions to the `default` and `fast` cases (lines 106 to 134). No existing assertion is removed or loosened.

R1 to R4, R9 and R10 are text requirements with no automated test. They are checked by the acceptance commands A3 and A5 and by the cold review against this plan.

Standing fixture rule. When an existing test's fixture is rejected by a check this plan adds, the implementer does exactly one of: (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The authored receipt lists each change with the check that forces it. The cold review and Codex review check each listed change. (Modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.) No case on `main` is expected to need it: the new tier adds a value and changes no existing path except the spike condition, whose current behaviour for `all` and `fast` T6 and T7 pin.

## 7. Deployment assumptions

No container, image, chart or cluster is involved. Two assumptions about environments outside this pull request remain, and recorded output settles both, so neither is open.

| Assumption | Recorded evidence |
|---|---|
| The hosted gate runs and passes the `Tier=CGroupV2` tests, so step 4 of section 3.1 is true as published | Run [43426723220](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43426723220) on `main` at `112a63ba`, the merge of issue #445 that introduced the trait: job 131889819833 `.NET verification` concluded `success` and its only .NET step ran `./scripts/verify-dotnet.sh` with no options (`.github/workflows/dotnet-verification.yml` at `112a63ba`, line 25), which adds no filter. Evidence taken from a fresh GitHub-hosted runner at the start of the job, with no other load applied by the run. |
| A managed cloud sandbox can fail the `Tier=CGroupV2` tests, which is why the pre-PR gate excludes them | `items/445/receipt.md` command 6: `./scripts/verify-dotnet.sh --project Parchwell.LoadGen.Tests` reported 2 failures out of 1810, both carrying `Tier=CGroupV2` and printing `b10_abrupt_cgroup_unavailable`; no failing test lacked the trait. Evidence taken from a full local run of that project in a cloud session, after the Docker daemon was already started, with no other load applied. |

Acceptance command A2 runs on the implementing session's own workspace. Whether that workspace has a reachable Docker daemon is not assumed: `docs/engineering/dotnet-verification.md` line 70 records that the `SessionStart` hook starts `dockerd` and that `docker info` fails until it is ready. A2's disposition below covers the case where it is not.

## 8. Acceptance commands

| # | Command | Evidence it must produce |
|---|---|---|
| A1 | `./scripts/test-verify-dotnet.sh` | ends with `verify-dotnet.sh checks passed.`; T1 to T10 among its cases; output in the receipt |
| A2 | `./scripts/verify-dotnet.sh --tier sandbox` | the last stage line, the partial line naming tier `sandbox`, and `summary.json` with `tier` `"sandbox"`, `outcome` `"partial_passed"`, `failed_stages` `[]`; by script over each `results.xml`, 0 executed tests carrying the trait `Tier=CGroupV2` and more than 0 executed tests in `Parchwell.LoadGen.Tests`; the exclusion recorded in the receipt as a partial run |
| A3 | `npm run check:docs` | each warning in a file this pull request changes fixed or explained; warnings in other files covered by one blanket line (rule 8) |
| A4 | `git status --porcelain` and `git diff --stat origin/main...HEAD` before the push | no path under `.github/workflows/`, `docs/operations/workflows/`, `results/`, `scenarios/` or `AGENTS.md` in the diff; output in the receipt |
| A5 | a script that extracts the new section of `docs/agents/pull-requests.md` and compares it with the block in section 3.1 of this plan | byte-equal; output in the receipt |

A2 is this pull request's own pre-PR local gate under the rule it publishes, run after the cold review clears the source. If the workspace has no reachable Docker daemon when A2 runs, the implementer starts it as `docs/engineering/dotnet-verification.md` line 70 describes and waits for `docker info` to succeed before A2; if it still cannot be reached, A2 is recorded as blocked with the exact error and the session hands off under AGENTS.md GOVERNANCE "Handoffs", because a partial run that silently drops the Docker tests is not the gate this plan publishes.

## 9. Sources

Read 2026-11-13 in this session. Tools: `gh api` REST routes for the issue, its comments, its events and the Actions runs and jobs; `git show` and file reads for `main` at `3cb6875b`; `dotnet test --help` on the workspace SDK; `curl` for the pinned Microsoft Testing Platform page.

- Issue #440 body and decision [6847972415](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6847972415), quoted in section 1. The `ready-for-agent` label event is 2026-11-13T07:12:10Z; the issue has exactly one comment.
- `main` at `3cb6875b`: `scripts/verify-dotnet.sh` lines 7 to 21 (usage), 56 to 63 (tier case and unknown-tier message), 73 to 88 and 341 to 349 (`--project`), 306 to 339 (`run_tests`, with the spike condition at 325 to 330), 449 to 455 (final lines); `scripts/test-verify-dotnet.sh` lines 17 to 61 (stub), 106 to 134 (`default` and `fast` cases), 145 to 165 (`select`, `unknown_project`, `unknown_tier`, `bad_cpu`, `help`); `scripts/check-dotnet-matrix.py` line 57; `docs/agents/pull-requests.md` whole file; `CLAUDE.md` lines 28 to 36; `.agents/skills/implement-plan/SKILL.md` whole file; `docs/engineering/dotnet-verification.md` lines 48 to 97 and 155 to 169; `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptEndToEndTests.cs` line 21; `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptLocalCgroupCaptureTests.cs` line 12; `.github/CODEOWNERS`.
- `dotnet test --help`, SDK 10.0.402 on this workspace, 2026-11-13: `--filter-not-trait` "Do not run any methods with a given trait value. Pass one or more name/value pairs (i.e., 'name=value'). Wildcard '*' is supported at the beginning and/or end of the trait name and/or value. Note: Specifying more than one is an AND operation."
- [The pinned Microsoft Testing Platform command page](https://github.com/dotnet/docs/blob/b6c9cb41353de8cd352c499be9e49ea6b4825bae/docs/core/tools/dotnet-test-mtp.md), fetched with `curl` at that commit: it does not mention `--filter-not-trait`, which is a runner option passed after `--`, so the help output above is this plan's source for that option, as `docs/engineering/dotnet-verification.md` line 58 already records for the `fast` tier.
- Run [43426723220](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43426723220) and its job 131889819833, and `.github/workflows/dotnet-verification.yml` at `112a63ba` line 25, for the hosted assumption in section 7.
- `items/445/receipt.md` commands 6 and 7, for the sandbox assumption in section 7.
- Governance read for this plan: AGENTS.md GOVERNANCE "Hard safety rules" 7 to 11, "Merge rule" and "Handoffs"; docs/agents/pull-requests.md rules 5, 8, 10 and 13; docs/agents/factory.md "Authority reads", "Item state file", "Item lifecycle" steps 1 to 3 and "Ruleset".

## 10. Plan-check questions

(a) Effort that scales with a number unknown at planning time: the number of test failures A2 reports, and the number of `npm run check:docs` warnings in changed files (A3). Cap for A2: the implementer triages the failures once by script into those carrying `Tier=CGroupV2` (which R5 makes impossible) and the rest; a failure this pull request did not cause is not fixed here. Exhaustion action: record the count and an estimate of the remaining time under AGENTS.md hard safety rule 11, push -wip and hand off to the next session (AGENTS.md "Before opening a pull request" rule 11). Cap for A3: every warning in a Markdown file this pull request changes is disposed of; warnings elsewhere take one blanket line. No other step scales: the requirements, the text blocks and the 10 test cases are fixed.

(b) Stricter than the parent: there is no parent plan. Against the decision, this plan is stricter in three places, each recorded here: the published section states when a review round counts as open (rule 5's frozen inputs), which the decision leaves to the reader; it states that a change after the gate needs rule 8 coverage and a fresh gate run, which follows from rule 8 and is written down so the order cannot be read as one-way; and it names the new tier `sandbox` and pins its output, which the decision delegates ("adds the script option the exclusion needs"). It is not stricter than the decision anywhere else, and it decides nothing the decision reserves: proposals 3 and 4 are untouched.

(c) Dependencies on code, data or behaviour that is neither on `main` nor in this pull request: the hosted runner's behaviour and a cloud sandbox's missing cgroup v2 files. Both are in section 7 with recorded output that settles them, so neither is a deployment assumption left open. Everything else the plan touches is on `main` at `3cb6875b` and cited by file and line in section 9. The `--filter-not-trait` behaviour is the runner's, verified from its own help output in this session and already relied on by the `fast` tier on `main`.

## 11. Open questions for Hari

None.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, or the scope in section 2.

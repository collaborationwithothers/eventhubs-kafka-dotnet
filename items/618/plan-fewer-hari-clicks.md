# Fewer Hari clicks: contract-grade plan (issue #618)

Revision: R2.

This plan turns the four changes Hari chose on issue #618 into four pull requests, in order: the dispatch decision replaces the environment approval, a merge queue with auto-merge replaces the manual merge wait, the control session decides engineering choices, and an agent may merge most protected paths under conditions. Each pull request also updates the factory overview diagrams. Several points that only Hari can settle, including two combined safety risks, are listed under "Open questions for Hari"; nothing here is approved until Hari's `## Approval` names this revision and commit.

Base commit: `main` at `7639d40826b2222c7dcc39dc14ab8145a9cf784c`.

Changes since R1, from [plan check 7240041048](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7240041048): file counts by script (finding 13); the reviewer removal waits for Q1.2 and #609 is on the verdict path (2); re-runs count as dispatches (3); R1.4 covers the string and list forms of `on` (4); the protected-path auto-merge flow R2.9 (5); the tool binding moves to CLAUDE.md, with Q2.4 for Codex (6); the two rule exceptions of pull request 2 are named (7); R3.1 and R3.3 resolve the conflict with the control-session skill (8); only the control session posts an engineering choice, with Q3.2 (9); R4.2 and R4.9 amend the contradicting rules (10); Q4.2 lists every path that leaves Hari's gate (11); alternative texts for keeping workflows with Hari (12); the seven P2 findings, including the three marked `apply in implementation` (S7, R2.6, R4.1, R4.7, C2.1, the tracked skill path, two literal test expectations).

## 1. Goal and authority

Goal: remove the steps where Hari acts a second time on something he already decided, or waits on GitHub, without removing a decision that is his (issue #618 body, "What this proposal does not touch").

Authority, in creation order (docs/agents/factory.md "Item lifecycle" step 3):

1. Issue #618 body, updated 2026-11-28T13:15:16Z (a proposal; it authorizes nothing by itself).
2. [Decision 7239274039](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7239274039): every pull request that adopts a change also updates the factory overview diagrams, through `generate-diagrams.py`, with the SVG files regenerated.
3. [Decision 7239287704](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7239287704), confirmed by Hari's thumbs-up at 2026-11-28T13:12:25Z: all four changes are pursued; bounds, design points and rule text are for this plan and Hari; settings on GitHub are changed by Hari.
4. [Decision 7239788015](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7239788015), confirmed by Hari's thumbs-up at 2026-11-28T13:16:50Z (read from the reactions API in this session; its last edit is 13:13:56Z): change 3 is option (b), with the four conditions and the list of paths kept with Hari as the proposal words them.
5. `ready-for-agent` applied by `haripraghash` at 2026-11-28T13:14:25Z. No comment names a tool, so Claude Code implements (AGENTS.md "Implementer and reviewer").

The [control action 7239804179](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7239804179) records that the #597 standing decision is not adopted here, so this plan is approved only by Hari's `## Approval` naming its revision and commit.

There is no parent item plan; this is governance work, so the plan template's scientific fields do not apply.

## 2. Split by scope

One plan, one plan check, one approval for an ordered set of four pull requests (docs/agents/factory.md "Item lifecycle" step 3 "Split by scope"). Each is a coherent piece: one gate change with its tests and diagrams.

| Order | Short name | Change | Branch |
|---|---|---|---|
| 1 | `dispatch-gate` | Change 1 | `claude/618-dispatch-gate` |
| 2 | `merge-queue` | Change 2 | `claude/618-merge-queue` |
| 3 | `control-choices` | Change 4 | `claude/618-control-choices` |
| 4 | `agent-protected-merge` | Change 3 (b) | `claude/618-agent-protected-merge` |

The order follows the proposal's recommendation ("Changes 1 and 2 first ... Change 4 next. Change 3 only if Hari chooses (b) or (c) knowingly"). Each pull request branches from `main` after its predecessor merges, because pull requests 2 and 4 edit the same AGENTS.md sections as pull request 1. Every pull request touches AGENTS.md or docs/agents/, which stay Hari's to merge in every option, so Hari merges all four.

Size estimates (estimate; file counts computed by script on 2026-11-28 from the scope lists in sections 4.1, 5.1, 6.1 and 7.1 and the SVG files whose generator calls each pull request changes; changed lines estimated from the edits listed below; not enforced):

| Pull request | Files | Changed lines |
|---|---|---|
| 1 | 11 | about 170 |
| 2 | 12 | about 110 |
| 3 | 5 | about 140 |
| 4 | 10 | about 170 |

## 3. Shared rules for all four pull requests

S1. Diagrams. The pull request edits `docs/operations/factory-overview/generate-diagrams.py` only at the calls this plan names, runs it, and commits every SVG file it rewrites. It never edits an SVG file by hand (decision 7239274039).

S2. Receipt. The pull request adds or extends a final section `## Amendments` in `docs/operations/factory-overview/receipt.md`, with one bullet per pull request, of exactly this form: `- Issue 618, <short name>: <the claims table rows replaced, by their old first cell>. Diagrams regenerated by generate-diagrams.py.` It replaces the claims table rows this plan names, in place. It changes no other part of the receipt, which records issue 394's history.

S3. Where a rule changes, its text is stated once, in the file that owns it, and other files cite it (docs/agents/factory.md "Item lifecycle" step 3).

S4. ASCII punctuation only, metric units, no em or en dashes (AGENTS.md "Writing standards").

S5. Standing fixture rule. When an existing test's fixture is rejected by a check this plan adds or tightens, the implementer does exactly one of: (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it. The cold review and Codex review check each listed change. Basis: [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2. The pinned workflow hash in `infra/scripts/phase/tests/test_workflow.py` (pull request 2) is not a fixture under this rule; R2.6 sets it.

S7. Status lines. A pull request that changes docs/agents/factory.md appends to its status paragraph (line 5) exactly: ` Amended in the pull request for issue #618 (<short name>): <the factory.md sections it changes, in file order, quoted>; Hari's merge of that pull request is the approval.` A pull request that adds or amends an ADR in docs/blueprint.md adds, after the header paragraph that ends on line 9, one paragraph: `The <ADR-023 addition | ADR-018 amendment> follows issue #618 [decision 7239287704](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7239287704)<, and [decision 7239788015](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7239788015) for the ADR-018 amendment>; Hari's merge of its pull request records approval.`

S6. Settings. No session changes a GitHub setting. Each pull request's description lists the settings Hari changes, in the order section 8 gives, and the session's handoff after merge repeats them.

## 4. Pull request 1: `dispatch-gate` (change 1)

### 4.1 Scope

Files allowed to change: `AGENTS.md`, `docs/agents/factory.md`, `docs/blueprint.md`, `infra/scripts/lifecycle/check-offline.sh`, `infra/scripts/tests/test_tools_offline.py`, `docs/operations/factory-overview/generate-diagrams.py`, the SVG files it rewrites, `docs/operations/factory-overview/receipt.md`.

Must not change: any file under `.github/`, `scripts/`, `infra/persistent/`, `infra/ephemeral/`, `results/`, any `expected.yaml`, any other `docs/operations/` contract. Approved contracts that name the environment approval are not edited; ADR-023 states how they read (R1.3).

### 4.2 External contracts

- C1.1 Environment `parchwell-scenarios`: no required reviewer; deployment branches "Selected branches and tags" with exactly the branch name pattern `main`. Set by Hari (section 8). The environment name, its secrets and variables, and the federated credential subject are unchanged, so no identity file changes.
- C1.2 The offline check message `<file>: scenario environment job on a trigger other than workflow_dispatch` (R1.4).
- C1.3 The rule and ADR wording in R1.1 to R1.3.

### 4.3 Requirements

R1.1 WHEN the pull request merges THEN AGENTS.md hard safety rule 8 gains, after its sentence "An agent never dispatches a workflow against any ref but `main`.", exactly:

> An agent dispatches a workflow with a job that uses the `parchwell-scenarios` environment only when a `## Decision` or `## Approval` comment, recognized under docs/agents/factory.md "Item lifecycle" step 3, authorizes that dispatch: it names the workflow, its inputs and the number of dispatches, and the agent dispatches no more than that number. A re-run of any job of such a run counts as one more dispatch under that number. That decision is the gate for the run; the environment admits only `main` and needs no required reviewer (docs/blueprint.md ADR-023). A workflow with such a job triggers only on `workflow_dispatch`.

If Hari answers Q1.1 "thumbs-up required", the second sentence instead reads: "That decision carries the line `Confirmation: thumbs-up required` and is acted on only once `scripts/decision-log.py` reports Hari's thumbs-up, under docs/agents/factory.md "Item lifecycle" step 3; it is the gate for the run; the environment admits only `main` and needs no required reviewer (docs/blueprint.md ADR-023)."

R1.2 WHEN the pull request merges THEN docs/agents/factory.md changes in exactly these places, besides its status line under S7:
- the "Decisions taken" row `| Live Azure runs | ... |` reads `| Live Azure runs | Started by agents through the repository's workflows on main, only as Hari's recorded dispatch decision authorizes (AGENTS.md hard safety rule 8; docs/blueprint.md ADR-023) |`;
- in "Hard safety rule 8: basis and decision history", the sentence beginning "The Azure identity stays gated by the two GitHub environments" is replaced by: "The Azure identity stays gated by the two GitHub environments, `parchwell-scenarios` and `parchwell-sweeper`, each with a `main`-only deployment branch policy, so a branch workflow gets no Azure token. Since docs/blueprint.md ADR-023, `parchwell-scenarios` has no required reviewer, and Hari's recorded dispatch decision is the gate for a run (hard safety rule 8). That the branch policy refuses a job on another ref is GitHub's documented rule ("Only branches and tags that match your specified name patterns can deploy to the environment"; https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments, read 2026-11-28); the page does not state what the refused job sees, and no branch dispatch has tested it."

R1.3 WHEN the pull request merges THEN docs/blueprint.md:
- gains `### ADR-023: Hari's recorded dispatch decision is the gate for a scenario run`, after ADR-022, in the ADR format (context, options, decision, consequences), with this text for its three required parts:
  - Context: "Each job that references `parchwell-scenarios` waited for Hari's approval, so a two-configuration run needed two approvals of a dispatch he had already decided (issue #618 body, change 1). Hari chose to make the dispatch decision the gate ([decision 7239287704](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7239287704), point 1)."
  - Options: "1. Keep required reviewers on every job. 2. One approval per dispatch and a wait timer (issue #609). 3. No required reviewer; the environment admits only `main`; Hari's recorded dispatch decision is the gate."
  - Decision: "Option 3. AGENTS.md hard safety rule 8 owns the dispatch rule. Contracts approved before this ADR that require the protected environment approval read it as that recorded dispatch decision. Hari removes the required reviewers only once the gap between configuration jobs is handled, or he accepts that risk by name (issue #618, open question Q1.2)."
  - Consequences: "Nothing technical stops an agent session from dispatching a run on `main` that Hari did not decide; the rule is text. A dispatch spends money without a click at run time. Workflow files stay as protected as GOVERNANCE "Merge rule" keeps them; agents dispatch only on `main`; at most one ephemeral environment exists (hard safety rule 5). The `main`-only branch policy keeps a branch copy of a workflow from deploying to the environment."
- in section 8 "Re-validation", the bullet "Scheduled runs wait for Hari's approval on a protected GitHub environment before any apply, so a schedule never spends money unattended." reads "A workflow that uses `parchwell-scenarios` triggers only on `workflow_dispatch` (AGENTS.md hard safety rule 8), so a schedule never spends money unattended; a scheduled re-validation is dispatched under a recorded decision (ADR-023)."
- in section 9 "Control plane", the sentence "Terraform apply runs through the protected scenario environment requiring Hari's approval." reads "Terraform apply runs through the main-only scenario environment, gated by Hari's recorded dispatch decision (ADR-023)."

R1.4 WHEN `check_workflows` in `infra/scripts/lifecycle/check-offline.sh` reads a workflow any of whose jobs has `environment: parchwell-scenarios`, and its triggers include any event other than `workflow_dispatch`, THEN it raises `ValueError` with exactly `<file name>: scenario environment job on a trigger other than workflow_dispatch`. The check runs for every `*.yml` file, after the existing `expected_triggers` comparison. The triggers are the keys when `on` is a mapping, the one value when `on` is a string, and the items when `on` is a list. A job's `environment` given as a mapping is read from its `name` key.

R1.5 WHEN `generate-diagrams.py` runs THEN these calls produce exactly these strings (every other call unchanged):
- d01 subtitle (line 91): `Sessions are started by hand. GitHub is the shared memory. Azure is reached only through workflows on main, after Hari decides each dispatch.`
- d01 aria label (line 92): replace `behind GitHub environment gates.` with `on main, after Hari decides each dispatch.`
- d01 Hari box (line 98): lines `["Approves item plans, verdicts", "and each scenario dispatch"]`.
- d01 box at line 138: title `Main-only environment`, lines `["no reviewer; admits", "only the main branch"]`, kind `github`.
- d01 label at line 142: `main only`.
- d01 label at line 148: `dispatches a decided run, main only`.
- d02 steps 11 and 12 (lines 184 and 185): `step(y, "hari", "gh", "11. Hari decides the dispatch (recorded as a Decision)")` and `step(y, "cc", "az", "12. dispatch the decided run (main only); run, then destroy")`.
- d02 aria label (line 158): replace `an agent dispatches the Azure run, Hari approves it,` with `Hari decides the dispatch, an agent dispatches the Azure run,`.
- d03 col1 card at line 207: `("Azure access gates", ["Both environments admit only main;", "neither has a reviewer"])`.
- d03 col3 card at line 220: `("Every scenario dispatch", ["a recorded decision by Hari"])`.
- d03 known gaps box (line 229): add a fourth line, `"Nothing technical stops an agent from dispatching a run Hari did not decide; the rule is text."`.
- d05 aria label (line 279) reads `Azure run flow: Hari decides the dispatch, an agent dispatches the workflow on main, the job applies and destroys the ephemeral layer, AKS stops when the dispatch ends, and the sweeper cleans up if a job dies.`. The d05 subtitle (line 278) is unchanged.
- d05 items 1 and 2 (lines 282 and 283): `("1. Decision", ["Hari decides", "this dispatch"], "human")` and `("2. Dispatch", ["agent starts the", "workflow, main only"], "claude")`; notes (line 290) `["recorded", "OIDC token", "", "success or failure", "last job"]`.

R1.6 WHEN the generator runs on the result THEN it prints `ok` as its last line and no line starting `WARN`, and a second run changes no file.

R1.7 WHEN the pull request merges THEN receipt.md's claims row whose first cell is "Scenario runs need Hari's approval in the `parchwell-scenarios` environment" reads `| A scenario dispatch needs Hari's recorded decision; the parchwell-scenarios environment admits only main and has no reviewer | AGENTS.md hard safety rule 8; docs/blueprint.md ADR-023 | Confirmed as a rule; the environment settings are Hari's and agents cannot read them |`, and S2's bullet names it.

### 4.4 Invariants

I1.1 Hard safety rules 1 to 3, 5 to 7 and 9 to 11 keep their text. I1.2 No workflow file changes; all five workflows that use `parchwell-scenarios` trigger only on `workflow_dispatch` on the base commit (read by script in this session), so R1.4 passes on `main` unchanged. I1.3 `parchwell-sweeper` is unchanged.

### 4.5 Test map

| Requirement | Test (in `infra/scripts/tests/test_tools_offline.py`) | Input | Expected |
|---|---|---|---|
| R1.4 pass | existing `test_committed_scenario_items_map_passes_check_workflows` | committed workflows | no exception |
| R1.4 schedule | `test_scenario_environment_job_on_schedule_fails_check_workflows` | a new file `synthetic.yml` with `on: {workflow_dispatch: '', schedule: [{cron: '0 0 * * *'}]}` and one job with `environment: parchwell-scenarios` | `ValueError` matching `^synthetic\.yml: scenario environment job on a trigger other than workflow_dispatch$` |
| R1.4 push | `test_scenario_environment_job_on_push_fails_check_workflows` | as above with `on: {workflow_dispatch: '', push: {branches: [main]}}` | same message |
| R1.4 string form | `test_scenario_environment_job_on_string_push_fails_check_workflows` | `synthetic.yml` with `on: push` and the same job | same message |
| R1.4 list form | `test_scenario_environment_job_on_list_schedule_fails_check_workflows` | `synthetic.yml` with `on: [workflow_dispatch, schedule]` and the same job | same message |
| R1.4 list boundary | `test_scenario_environment_job_on_list_dispatch_passes_check_workflows` | `synthetic.yml` with `on: [workflow_dispatch]` and the same job | no exception |
| R1.4 mapping form | `test_scenario_environment_mapping_on_schedule_fails_check_workflows` | as the schedule case with `environment: {name: parchwell-scenarios}` | same message |
| R1.4 boundary | `test_dispatch_only_scenario_environment_job_passes_check_workflows` | `synthetic.yml` with `on: {workflow_dispatch: ''}` and the same job | no exception |
| R1.4 other environment | `test_sweeper_environment_job_on_schedule_passes_check_workflows` | `synthetic.yml` with the schedule triggers and `environment: parchwell-sweeper` | no exception |

The synthetic file is written into a temporary copy of `.github/workflows` as `scenario_workflow_fixture` does today, with the mirror copies that `check_workflows` reads.

### 4.6 Acceptance

| Command | Evidence |
|---|---|
| `python3 -m unittest discover -s infra/scripts/tests -p test_tools_offline.py -v` | all tests pass, the nine above by name |
| `python3 docs/operations/factory-overview/generate-diagrams.py` twice, then `git status --porcelain docs/operations/factory-overview` | `ok`, no `WARN`; second run adds no change |
| `npm ci --ignore-scripts && npm run check:docs` | warnings disposed under AGENTS.md "Before opening a pull request" rule 8 |
| `LC_ALL=C grep -nP '[^\x00-\x7F]'` over every changed file | no output |
| hosted `.NET verification` on the final head | success |

## 5. Pull request 2: `merge-queue` (change 2)

### 5.1 Scope

Files allowed to change: `AGENTS.md`, `CLAUDE.md`, `docs/agents/factory.md`, `.github/workflows/dotnet-verification.yml`, `infra/scripts/lifecycle/check-offline.sh`, `infra/scripts/tests/test_tools_offline.py`, `infra/scripts/phase/tests/test_workflow.py`, `docs/operations/factory-overview/generate-diagrams.py`, the SVG files it rewrites, `docs/operations/factory-overview/receipt.md`.

Must not change: `.github/workflows/docs-advisory.yml` (not a required check: the ruleset's only required status check is `.NET verification`, read from `GET /repos/.../rules/branches/main` in this session), any other workflow, `scripts/`, `results/`, any `expected.yaml`.

### 5.2 External contracts

- C2.1 The `on` block of `.github/workflows/dotnet-verification.yml` becomes exactly:

```yaml
on:
  pull_request:
  push:
    branches: [main]
  merge_group:
    types: [checks_requested]
  workflow_dispatch:
```

  GitHub: "You **must** use the `merge_group` event to trigger your GitHub Actions workflow when a pull request is added to a merge queue. Otherwise, status checks will not be triggered ... The merge will fail as the required status check will not be reported." `checks_requested` is the only activity type; `GITHUB_SHA` is the "SHA of the merge group". (Managing a merge queue, and Events that trigger workflows, `merge_group`; read 2026-11-28.) No job body changes: each checkout uses `github.event.pull_request.head.sha || github.sha`, which is the merge-group SHA for this event, and the mutation qualification step passes `--base` only for `pull_request` (workflow lines 51, 110, 128, 173 and 227 for the checkouts, and 139 to 150, on the base commit).

Rule exceptions Hari approves by name with this plan: an agent changes the triggers of an existing workflow, `dotnet-verification.yml`, which AGENTS.md hard safety rule 8 forbids today ("An agent never changes an existing workflow's triggers"); and R2.1 amends hard safety rule 9, which the issue body's "What this proposal does not touch" lists as unchanged. Both are proposals under decision 7239287704 point 2 and need his approval of this revision.
- C2.2 Ruleset `main-protection`: rule "Require merge queue" with merge method "Merge commit"; repository setting "Allow auto-merge" on. Set by Hari (section 8); other queue values are Q2.2.
- C2.3 The merge procedure in R2.2.

### 5.3 Requirements

R2.1 WHEN the pull request merges THEN AGENTS.md changes:
- hard safety rule 8: "and `dotnet-verification.yml` and `docs-advisory.yml` keep theirs." reads "`dotnet-verification.yml` keeps its triggers as amended for issue #618, which added `merge_group`, and `docs-advisory.yml` keeps its own.";
- hard safety rule 9 reads: "No workflow that reaches Azure may trigger on `pull_request`, `pull_request_target` or `merge_group`. The repository is public; fork pull requests must never run inside the network.";
- "Code Review Rules": "adds a `pull_request` or `pull_request_target` trigger" reads "adds a `pull_request`, `pull_request_target` or `merge_group` trigger".

R2.2 WHEN the pull request merges THEN the AGENTS.md "GitHub issues and pull requests" bullet beginning "Merge only through the REST merge route" reads exactly:

> Merge only under GOVERNANCE "Merge rule", verified on the head `sha` you record; do not take that `sha` from a fresh read afterwards. Merge by enabling auto-merge, with the merge-commit method, through your tool's GitHub connector (CLAUDE.md "Merging" names Claude Code's tools); with a merge queue required, GitHub adds the pull request to the queue once its requirements are met. Immediately before enabling it, read the pull request head again and enable it only when the head equals the recorded `sha`. Before any later push to the branch, disable auto-merge; after the push, verify the merge rule again on the new head before enabling it again. A session whose tool cannot enable or disable auto-merge, or whose tool refuses, does not merge by another route: the pull request waits for Hari. Agents merge with a merge commit, as Hari decided on 2026-11-17 ([decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6958521790)); a change of method is his decision. The merge-group commit the queue builds is checked by the required checks, not by Codex; the reviewed commit is the pull request head.

R2.9 WHEN the pull request merges THEN AGENTS.md GOVERNANCE "Merge rule" gains, after its list and before "Anything else waits for Hari.", exactly: "An agent may enable auto-merge on its pull request that touches a protected path when every other condition above holds on the recorded head. The required reviewers rule then holds the merge until Hari approves; his approval is his merge decision, and a push after it dismisses it (docs/agents/factory.md "Ruleset", "Dismiss stale approvals on push")." The control session reports such a pull request to Hari as ready only after the agent has enabled auto-merge on that head.

R2.10 WHEN the pull request merges THEN CLAUDE.md gains, between the sections "Identity" and "Before opening a pull request", a section `## Merging` with exactly: "Claude Code enables auto-merge with the GitHub MCP server's `enable_pr_auto_merge` tool, with `mergeMethod` `MERGE`, and disables it with `disable_pr_auto_merge` (AGENTS.md "GitHub issues and pull requests"). Source: the tools' descriptions as they appeared in one cloud session, read 2026-11-28: "Enable auto-merge for a pull request. The PR will merge automatically once all required checks pass and approvals are met. Fails gracefully if auto-merge is not enabled for the repository or if the PR is already mergeable (clean status)." and "Disable auto-merge for a pull request that currently has it enabled." It is tool metadata, not official documentation. Neither tool takes an expected head SHA, so the head check AGENTS.md requires is a read made just before the call."

R2.3 WHEN the pull request merges THEN docs/agents/factory.md, besides its status line under S7:
- "Ruleset" table gains, after the row "Dismiss stale approvals on push", the rows `| Required status checks | .NET verification; branches must be up to date | Read from the ruleset on 2026-11-28 |` and `| Merge queue | Required; merge method merge commit | Brings each pull request up to date and runs its checks before merging, so merged pull requests stop invalidating each other's checks (issue #618, change 2). The checks run on the merge_group event |`;
- "Sequence to start the factory" step 6: "sessions use AGENTS.md's REST merge procedure" reads "sessions use the merge procedure of AGENTS.md "GitHub issues and pull requests"".

R2.4 WHEN `check_workflows` reads a workflow whose `on` mapping has `merge_group` THEN the existing Azure-identity checks for `pull_request` and `pull_request_target` apply to it with their existing messages (`<file>: PR workflow requests Azure identity`, `<file>: PR job requests Azure identity`).

R2.5 WHEN `check_workflows` compares triggers THEN `expected_triggers['dotnet-verification.yml']` is exactly `{'pull_request': '', 'push': {'branches': ['main']}, 'merge_group': {'types': ['checks_requested']}, 'workflow_dispatch': ''}`.

R2.6 WHEN the workflow changes THEN `BASE_WORKFLOWS['.github/workflows/dotnet-verification.yml']` in `infra/scripts/phase/tests/test_workflow.py` is the SHA-256 of the new file bytes, computed by script, and line 851 of the comment above `BASE_WORKFLOWS` reads exactly `    # the coordinated C5 campaign of issue #494, the full-suite mode of issue #602, and the merge_group trigger of issue #618.` No other hash or line changes.

R2.7 Diagrams:
- d01 box at line 114: lines `["merge queue only;", "protected paths: Hari"]`.
- d02 step 10 (line 183): `10. fix, then queue for merge or hand off`.
- d03 col1 card at line 206: `("Ruleset on main", ["Merge queue; no direct or force pushes;", "protected paths need Hari's approval"])`.
- R1.6 applies.

R2.8 Receipt: no claims row states the merge route, so S2's bullet reads "no claims row replaced".

### 5.4 Invariants

I2.1 The REST merge route is no longer the agent merge path; no rule tells an agent to bypass the queue. I2.2 GOVERNANCE "Merge rule" conditions are unchanged. I2.3 "Dismiss stale approvals on push" stays as Q2.1 decides; this pull request does not describe it as changed.

### 5.5 Test map

| Requirement | Test | Input | Expected |
|---|---|---|---|
| R2.5 | existing `test_committed_scenario_items_map_passes_check_workflows` | committed workflows with C2.1 | no exception |
| R2.5 | `test_dotnet_verification_without_merge_group_fails_check_workflows` (test_tools_offline.py) | copy with `merge_group` removed from dotnet-verification.yml | `ValueError` matching `^dotnet-verification\.yml: unexpected workflow triggers$` |
| R2.4 | `test_merge_group_workflow_with_identity_fails_check_workflows` | `synthetic.yml`, `on: {merge_group: {types: [checks_requested]}}`, top-level `permissions: {id-token: write}` | `^synthetic\.yml: PR workflow requests Azure identity$` |
| R2.4 | `test_merge_group_job_with_azure_login_fails_check_workflows` | same triggers, one job with a step `uses: azure/login@v2` | `^synthetic\.yml: PR job requests Azure identity$` |
| R2.4 boundary | `test_merge_group_workflow_without_identity_passes_check_workflows` | same triggers, `permissions: {contents: read}`, no login step | no exception |
| R2.6 | existing `B10AbruptWorkflowTests` hash test | committed workflows | passes with the new hash |

### 5.6 Acceptance

As 4.6, plus `python3 -m unittest discover -s infra/scripts/phase/tests -p test_workflow.py -v` (all pass). The hosted `.NET verification` run on the pull request shows the workflow still runs on `pull_request`.

## 6. Pull request 3: `control-choices` (change 4)

### 6.1 Scope

Files allowed to change: `docs/agents/factory.md`, `.agents/skills/control-session/SKILL.md` (the tracked file; `.claude/skills/control-session` is a symlink to its folder), `scripts/decision-log.py`, `scripts/tests/test_decision_log.py`, `docs/operations/factory-overview/receipt.md` (S2 bullet only). No diagram draws who answers a plan question (searched by `grep -n "decision\|question\|finding"` over generate-diagrams.py on the base commit), so the generator and SVG files do not change; the S2 bullet says so.

Must not change: AGENTS.md, CLAUDE.md, any other skill, any workflow.

### 6.2 External contracts

- C3.1 The comment form: heading `## Control action`, and the line `Engineering choice: decided by the control session` on its own line.
- C3.2 The decision-log entry kind `Control decision`.

### 6.3 Requirements

R3.1 WHEN the pull request merges THEN docs/agents/factory.md "Control session" gains, after its paragraph, exactly:

> ### Engineering choices
>
> The control session of an issue decides a plan-check finding, or a question a planning or implementing session raises, when the answer changes none of these: an expected outcome, a verdict rule, a hypothesis, published wording, a gate, a cap, a policy value, the scope of what is tested, a protected path beyond those the plan already names, spend, or anything on the ask list of the `control-session` skill's "Act or ask". Where the two lists differ, the longer exclusion wins. Only the control session of the issue posts such a comment; a planning, plan-check, implementing or review session never does, for its own work or another's. It records each such answer in one comment on the issue headed `## Control action` that carries the line `Engineering choice: decided by the control session` on its own line, links the finding or question, states the answer, and states why each item of that list is unchanged. "Item lifecycle" step 3 recognizes that comment as authority for that answer only. A later `## Decision` or `## Approval` that differs wins; Hari reverses an answer that way. Every other finding or question goes to Hari. This rule grants no authority to approve a plan, post `## Approval` or merge, and needs no thumbs-up. Basis: [decision 7239287704](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7239287704), point 4.

R3.2 WHEN the pull request merges THEN docs/agents/factory.md "Item lifecycle":
- step 1: "An open question in the plan is a finding that requires a decision by Hari." reads "An open question in the plan is a finding that requires a decision by Hari, or an answer by the control session under "Control session" "Engineering choices" where that rule covers it.";
- step 3, the authority bullet: after "A comment changes that authority only when its heading starts "## Decision" or "## Approval" and it records Hari's decision." insert "A `## Control action` comment under "Control session" "Engineering choices" also changes it, for its answer only.";
- "Authority reads" catch-up, the second list item reads "the full body of each new comment whose heading starts "## Decision" or "## Approval", each `## Control action` comment that carries the line `Engineering choice: decided by the control session`, and every decision such a comment links and adopts;";
- "Decision log": after "the line must start with "## Decision" or "## Approval"." insert "It also prints, as `Control decision`, a comment from those logins whose first non-empty line starts with "## Control action" and that has the line `Engineering choice: decided by the control session` on its own line."

R3.3 WHEN the pull request merges THEN `.agents/skills/control-session/SKILL.md` "Act or ask" condition 1 reads "The step only starts, stops or redirects a planning, plan-check, implementation or diagnosis session, dispatches a workflow on `main` that does not reach Azure, posts a status note, or decides an engineering choice under docs/agents/factory.md "Control session" "Engineering choices"." and the section gains, after "When you ask, post a comment ...", exactly: "When a finding or question falls under docs/agents/factory.md "Control session" "Engineering choices", decide it yourself and record it as that rule sets, instead of asking." The sentence "That heading carries no authority under docs/agents/factory.md "Item lifecycle" step 3." reads "That heading carries no authority under docs/agents/factory.md "Item lifecycle" step 3, except as "Engineering choices" there sets."

R3.4 WHEN `scripts/decision-log.py` reads a comment from `haripraghash` or `haripraghash-bot` whose first non-empty line starts `## Control action` and whose body has a line equal, after stripping spaces, to `Engineering choice: decided by the control session` THEN `kind()` returns `Control decision`; its entry renders as `### <n>. Control decision #<issue> comment <id>`, in creation order with the other entries, with its body verbatim and its links handled as for decisions. A `## Control action` comment without that line, or from another login, returns `None`. The rendered "Not printed" text for a linked comment of no kind is unchanged.

### 6.4 Invariants

I3.1 The thumbs-up rule (docs/agents/factory.md "Item lifecycle" step 3) is unchanged. I3.2 The ask list in the control-session skill is unchanged, and an item on it is never an engineering choice (R3.1). I3.3 Existing decision-log output for comments with no `## Control action` heading is byte-identical.

### 6.5 Test map (in `scripts/tests/test_decision_log.py`)

| Test | Input | Expected |
|---|---|---|
| `test_marked_control_action_is_a_control_decision` | bot comment `## Control action\n\nEngineering choice: decided by the control session\n\nAnswer.` | entry heading `### 1. Control decision #<issue> comment <id>`, body verbatim |
| `test_unmarked_control_action_is_not_printed` | `## Control action\n\nStarted a session.` | `No decision or approval comments.` |
| `test_control_marker_must_be_its_own_line` | `## Control action\n\nSee: Engineering choice: decided by the control session` as the only comment | output contains the line `No decision or approval comments.` and no line starting `### 1.` |
| `test_control_decision_from_other_login_is_not_printed` | marked comment by `someone-else` as the only comment | output contains the line `No decision or approval comments.` and no line starting `### 1.` |
| `test_control_decision_orders_with_decisions` | a `## Decision` at 10:00:00Z and a marked control action at 09:00:00Z | control decision is entry 1, decision is entry 2 |
| existing `test_recognizes_only_decision_and_approval_headings` | unchanged inputs | unchanged expectations (none of its five inputs starts `## Control action`; read on the base commit, lines 77 to 93) |

### 6.6 Acceptance

`python3 -m unittest discover -s scripts/tests -p test_decision_log.py -v` (all pass), `npm run check:docs`, the ASCII scan, and the hosted `.NET verification` run, whose aggregate job runs this test (workflow line 133).

## 7. Pull request 4: `agent-protected-merge` (change 3, option (b))

### 7.1 Scope

Files allowed to change: `AGENTS.md`, `docs/agents/factory.md`, `docs/blueprint.md`, `.github/CODEOWNERS`, `.agents/skills/control-session/SKILL.md`, `docs/operations/factory-overview/generate-diagrams.py`, the SVG files it rewrites, `docs/operations/factory-overview/receipt.md`.

Must not change: workflows, `scripts/`, `results/`, any `expected.yaml`, `infra/`.

### 7.2 External contracts

- C4.1 Ruleset `main-protection` required reviewers rule: team `repo-owners`, 1 approval, exactly the Hari-only patterns: `expected.yaml`, `/results/`, `/infra/persistent/`, `/infra/ephemeral/identity*.tf`, `/infra/ephemeral/network*.tf`, `/AGENTS.md`, `/CLAUDE.md`, `/docs/agents/`, plus any Hari adds under Q4.2. Set by Hari (section 8). This is the route Q4.1 recommends; under another answer, this contract and R4.1 change before implementation.
- C4.2 The term "Hari-only path" for those patterns; "protected path" keeps meaning every CODEOWNERS path.
- C4.3 The merge-rule wording in R4.1.

### 7.3 Requirements

R4.1 WHEN the pull request merges THEN AGENTS.md GOVERNANCE "Merge rule" reads, in place of the bullet "no protected path is touched (...)":

> - no Hari-only path is touched, and the pull request is not labelled `verdict`. Hari-only paths are the required reviewers rule's patterns in the `main-protection` ruleset, listed in docs/agents/factory.md "Ruleset"; that rule blocks the merge until Hari approves;
> - when a path listed in `.github/CODEOWNERS` that is not a Hari-only path is touched: the plan the pull request implements was approved under docs/agents/factory.md "Item lifecycle" step 3; the control session of its issue posted a `## Control action` comment recording that the diff at the recorded head `sha` matches that plan's scope and literal text; and Codex's security review completed on that head;

The Codex bullets stay. After the list, the sentence "Anything else waits for Hari. Never ask for the gate to be relaxed." is unchanged.

R4.2 WHEN the pull request merges and Hari has not kept `.github/workflows/` with him (Q4.2, Q4.3) THEN AGENTS.md hard safety rule 8's first sentence reads "Agents change files under `.github/workflows/` only through a pull request merged under GOVERNANCE "Merge rule"; the path is protected." and its sentence "An agent may add a workflow whose only trigger is `workflow_dispatch`; Hari merges it." reads "An agent may add a workflow whose only trigger is `workflow_dispatch`; it is merged under GOVERNANCE "Merge rule"." WHEN Hari keeps `.github/workflows/` with him THEN rule 8 is unchanged. The rest of rule 8 is unchanged either way. "Before opening a pull request" and pull-requests.md are not edited.

R4.9 WHEN the pull request merges THEN these sentences change, and no others outside R4.1 to R4.8:
- AGENTS.md "Skills": "They are not in `skills-lock.json` and change only through a pull request to Hari." reads "They are not in `skills-lock.json` and change only through a pull request merged under GOVERNANCE "Merge rule"." (unchanged if Hari keeps the skill folders with him under Q4.2);
- docs/agents/factory.md "Modes of work", the paragraph below the table: "Protected paths are those in the ruleset and CODEOWNERS;" reads "Protected paths are those in CODEOWNERS, and Hari-only paths the subset in the ruleset;";
- docs/agents/factory.md "Escalation" layer 1, "Agent merge is enabled for unprotected paths under" reads "Agent merge is enabled under GOVERNANCE "Merge rule" and";
- docs/agents/factory.md "Ruleset" row `| Required approvals | 0 | Agent pull requests on unprotected paths merge without a human |` reads `| Required approvals | 0 | Agent pull requests that touch no Hari-only path merge without a human approval |`;
- docs/agents/factory.md "Ruleset": "any other document under `docs/` comes to Hari." reads "any other document under `docs/` is a protected path, and `docs/agents/` is a Hari-only path.";
- docs/blueprint.md line 487: "and `.github/CODEOWNERS` mirrors the list" reads "and `.github/CODEOWNERS` lists it too".

R4.3 WHEN the pull request merges THEN docs/agents/factory.md, besides its status line under S7:
- "Modes of work": the rows "Verifier logic" and "Persistent infrastructure, identity, network, workflows, governance, ADRs" become three rows: `| Verifier logic | Agent-merged under the protected-path conditions of AGENTS.md GOVERNANCE "Merge rule" | It is the oracle for every result; its judgement logic is also covered by mutation testing (docs/engineering/dotnet.md "Testing"). Hari chose to drop the human look ([decision 7239788015](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7239788015)) |`, `| Workflows, ADRs, skills and other protected paths | Agent-merged under the same conditions | Large blast radius; same decision |` and `| Persistent infrastructure, identity, network, governance (AGENTS.md, CLAUDE.md, docs/agents/) | Human-reviewed (Hari-only path) | Large blast radius, long-lived decisions |`; WHEN Hari keeps `.github/workflows/` with him, the second row reads `| ADRs, skills and other protected paths | Agent-merged under the same conditions | Large blast radius; same decision |` and the third reads `| Persistent infrastructure, identity, network, workflows, governance (AGENTS.md, CLAUDE.md, docs/agents/) | Human-reviewed (Hari-only path) | Large blast radius, long-lived decisions |`; each path Hari adds under Q4.2 moves to the third row's first cell; the paragraph below the table gains "Hari-only paths are the subset in the ruleset's required reviewers rule ("Ruleset")."
- "Escalation" layer 1, first sentence reads "The `main-protection` ruleset's required reviewers rule lists every Hari-only path and requires one approval from the `repo-owners` team (Hari) when a pull request touches one."
- "Ruleset" table row "Required reviewers" reads `| Required reviewers | Team repo-owners, 1 approval, the Hari-only patterns: expected.yaml, /results/, /infra/persistent/, /infra/ephemeral/identity*.tf, /infra/ephemeral/network*.tf, /AGENTS.md, /CLAUDE.md, /docs/agents/ | The gate for Hari-only paths. GitHub caps the list at 15 patterns per reviewer. Other protected paths stay in .github/CODEOWNERS for review requests |` (with any Q4.2 additions);
- "Sequence to start the factory" step 6 sentence "Hari merges every pull request touching a protected path." reads "Hari merges every pull request touching a Hari-only path; agents merge other protected-path pull requests under GOVERNANCE "Merge rule"."

R4.4 WHEN the pull request merges THEN, besides its header paragraph under S7, docs/blueprint.md ADR-018 consequence "Codex-authored pull requests have no reviewer from another vendor. Protected paths and the verifier still need Hari's approval, so the oracle and the public claims are never exposed to same-vendor review alone." reads "Codex-authored pull requests have no reviewer from another vendor. Hari-only paths still need Hari's approval, so the public claims are never exposed to same-vendor review alone. Since [decision 7239788015](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7239788015), the verifier is agent-merged, so a Codex-authored verifier change can reach `main` with Codex as its only reviewer." If Hari answers Q4.4 "Claude-implemented only", the last sentence instead reads "Since that decision, an agent merges a protected-path pull request only when Claude Code implemented it, so a verifier change keeps a reviewer from another vendor." and R4.1's second bullet adds "and no commit carries a `Co-Authored-By: Codex` trailer".

R4.5 WHEN the pull request merges THEN `.github/CODEOWNERS` header comment reads "# Review requests only. The merge gate is the required reviewers rule in the main-protection ruleset, which lists only the Hari-only paths (docs/agents/factory.md, "Ruleset"). This file lists every protected path. A path listed with no owner has no owner." The path lines are unchanged.

R4.6 WHEN the pull request merges THEN the control-session skill (`.agents/skills/control-session/SKILL.md`) ask list "protected-path merges" reads "merges of Hari-only paths", and its first condition gains, after "posts a status note": ", or posts the `## Control action` that GOVERNANCE "Merge rule" requires after checking by script that the diff equals the approved plan's scope and literal text".

R4.7 Diagrams:
- d01 box at line 114: lines `["merge queue only;", "Hari-only paths: Hari"]`.
- d03 col1 card at line 206: `("Ruleset on main", ["Merge queue; no direct or force pushes;", "Hari-only paths need his approval"])`. Card at line 208: WHEN workflows are agent-merged, `("Workflow files", ["Branch copies get no Azure token;", "outside contributors need approval"])`; WHEN Hari keeps them, `("Workflow files", ["Hari-only paths; runs from outside", "contributors need Hari's approval"])`.
- d03 col3 card at line 222: `("Hari-only paths (list: the ruleset)", ["expected outcomes, results, persistent", "infra, identity, network, governance"])`.
- d03 known gaps box (line 229): height `116` in place of `100`, and a fifth line: WHEN workflows are agent-merged, `"Workflows and the verifier can merge with no human look; the merge conditions are text."`; WHEN Hari keeps them, `"The verifier can merge with no human look; the merge conditions are text."`.
- d06 check at line 331: `("Hari-only path touched?", ["ruleset requires Hari", "(built)"], "human", False)`; box at line 346: lines `["all but Hari-only paths,", "under the merge rule"]`.
- R1.6 applies.

R4.8 Receipt: the claims rows whose first cells are "Agent merge is enabled for pull requests touching no protected path" and "`main` blocks deletions and force pushes; protected paths need `repo-owners` approval" read `| Agents merge pull requests that touch no Hari-only path, under the merge rule | AGENTS.md GOVERNANCE "Merge rule"; docs/agents/factory.md "Sequence to start the factory" step 6 | Confirmed as a rule |` and `| main blocks deletions and force pushes; Hari-only paths need repo-owners approval | docs/agents/factory.md "Ruleset" | Confirmed as a rule; the ruleset is set by Hari |`.

### 7.4 Invariants

I4.1 The four conditions of decision 7239788015 are each in R4.1 or in the existing Merge rule bullets (plan approved, control-session diff record, required checks, Codex code and security review). I4.2 Verdict pull requests always go to Hari (GOVERNANCE "Verdict integrity" unchanged). I4.3 docs/agents/pull-requests.md rule 8's third paragraph (work only Hari can approve) is unchanged.

### 7.5 Test map

No code changes. Checks: a script compares the pattern list in R4.3's "Required reviewers" row with C4.1 and with the ruleset read from `GET /repos/collaborationwithothers/eventhubs-kafka-dotnet/rules/branches/main` after Hari changes it (section 8), and prints any difference; the generator acceptance of R1.6.

### 7.6 Acceptance

As 4.6 without the unit tests, plus the pattern comparison script's empty difference, recorded in the pull request description with the read time.

## 8. Deployment assumptions and settings order

Each assumption is evidence-supported only as far as stated; none has an offline test, so each is listed under "Open questions for Hari" until he accepts it by name.

DA1 (pull request 1). A `main`-only deployment branch policy refuses a job on another ref, so it gets no environment secret and no OIDC token for `parchwell-scenarios`. Evidence: GitHub documentation only (R1.2's quotation), read 2026-11-28 through a fetch subagent; no live connection, idle or loaded, was used. What the refused job sees is not stated. The existing `parchwell-sweeper` environment relies on the same rule.

DA2 (pull request 1). Hari applies the settings in this order: (1) set the deployment branch policy of `parchwell-scenarios` to `main` only, which only narrows access and can be done before merge; (2) after merge, and only once Q1.2 is settled, remove the required reviewers. Agents cannot read the environment settings: `GET /repos/.../environments` returned HTTP 403 "Access to this GitHub API path is not permitted through this proxy" in this session. Evidence of the settings is Hari's statement on the issue.

DA3 (pull request 2). With the merge queue required, `enable_pr_auto_merge` adds a pull request to the queue. Evidence: GitHub, "If the pull request can't be added to the queue immediately, GitHub enables auto-merge and adds the pull request to the queue when requirements are met" (Merging a pull request with a merge queue, read 2026-11-28). The connector tool's own description says it "Fails gracefully ... if the PR is already mergeable (clean status)"; whether a queue-required pull request ever shows clean status is not known. Not tested from a session; no connection of any kind was made. Failure is safe: R2.2 makes the pull request wait for Hari. Whether the REST merge route is refused when a queue is required is not stated by GitHub; R2.2 does not use it.

DA4 (pull request 2). The `.NET verification` workflow passes on a merge-group commit as it does on a push to `main`. Evidence: source reading of the workflow on the base commit (C2.1); no run.

DA5 (pull request 4). After Hari's ruleset change, the required reviewers rule blocks a bot pull request that touches a Hari-only path and does not block one that touches only other protected paths. Evidence: none yet; the pattern list is readable by agents (the `rules/branches/main` read in this session returned the 15 patterns), so 7.6 checks the list, not the behaviour. "Require review from Code Owners" is on and was recorded as "not relied on" because its effect with 0 required approvals was not isolated (docs/agents/factory.md "Ruleset"); if it does enforce, agent merges of other protected paths stay blocked, which is safe.

Settings Hari changes (S6), with the pull request each belongs to: environment branch policy, then reviewer removal once Q1.2 is settled (pull request 1); "Allow auto-merge" on, merge queue rule with merge method "Merge commit", queue values (pull request 2, after merge, because the queue waits for a `merge_group` check that exists only after merge); required reviewers patterns (pull request 4, after merge).

## 9. Plan-check questions (docs/agents/factory.md "Item lifecycle" step 1)

(a) Steps whose effort scales with a number unknown at planning time: none. The files and generator calls are listed above; the diagram search covers one 367-line file. Cold-review rounds follow docs/agents/pull-requests.md rule 8's budget.

(b) Where the plan is stricter than its authority: there is no parent plan. Beyond the decisions, pull request 1 adds the offline check R1.4 and the dispatch-only rule in R1.1, because without a reviewer a scheduled trigger on a scenario-environment workflow would spend money unattended (the blueprint's section 8 bullet relied on the reviewer). Pull request 2 extends rule 9 and the identity check to `merge_group` (R2.1, R2.4), because a queued pull request's code runs on that event. Pull request 3 also excludes the control-session skill's ask list from engineering choices and bars any session other than the control session from posting one (R3.1), because without that the skill would say both "decide it yourself" and "always wait for Hari", and any session could answer its own question. Both are proposals under decision 7239287704 point 1 ("bounds ... are for the plan to propose") and are Hari's to approve with this plan.

(c) Tests or checks that depend on code, data or behaviour neither on `main` nor in the pull request: GitHub settings and behaviour, DA1 to DA5. The connector tool behaviour in DA3. No test depends on Azure.

## 10. Deferred work

| Item | On the verdict path | Why deferral is permitted | Who and when | Completion evidence | Gate it blocks |
|---|---|---|---|---|---|
| Hari's settings changes (section 8) | No (governance) | Only Hari can change them (decision 7239287704) | Hari, after each merge; the implementing session's completion comment lists them | Hari's statement on the issue; for the ruleset, the 7.6 read | The rules describe settings that are not yet in place until he does |
| First live use of the queue procedure (DA3) | No | It cannot run before the queue exists | The first agent merge after Hari enables the queue; one attempt, exhaustion action `record the gap and continue` (the pull request waits for Hari) | The merged pull request and its queue entry, linked on issue #618 | None; failure falls back to Hari |
| Gap between configuration jobs (issue #609) | Yes: today Hari's second approval is where the 20-minute wait after the first destroy happens (#609 body, citing #491), and without a reviewer `full-C3` would start about 120 s after `full-C0`'s destroy (`.github/workflows/scenario.yml` lines 515 to 519; `infra/scripts/phase/phase.py` line 20) | Not deferred past the gate change: ADR-023 and DA2 keep the reviewer until Q1.2 is settled | Hari answers Q1.2; a mechanism, if he picks one, is planned on #609 | His answer on #618, or the merged #609 pull request | Removal of the required reviewers (DA2 step 2) |

## 11. Sources

- GitHub, Deployments and environments: https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments, sections "Deployment branches and tags", "Required reviewers", "Wait timer", "Environment secrets", read 2026-11-28.
- GitHub, Managing a merge queue: https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/configuring-pull-request-merges/managing-a-merge-queue, read 2026-11-28.
- GitHub, Merging a pull request with a merge queue: https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/incorporating-changes-from-a-pull-request/merging-a-pull-request-with-a-merge-queue, read 2026-11-28.
- GitHub, Automatically merging a pull request: https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/incorporating-changes-from-a-pull-request/automatically-merging-a-pull-request, read 2026-11-28.
- GitHub, Events that trigger workflows, `merge_group`: https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows, read 2026-11-28.
- GitHub, Available rules for rulesets, "Require a pull request before merging": https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets, read 2026-11-28.
- Tool used for the GitHub pages: a web-fetch subagent of this planning session, which quoted each page; the quotations above are its report. GitHub's merge-queue availability for public organization repositories is quoted only in the issue body, not re-read here.
- Repository, on `main` at the base commit: `.github/workflows/dotnet-verification.yml` lines 1 to 7, 51, 118 to 150; `infra/scripts/lifecycle/check-offline.sh` lines 72 to 112; `infra/scripts/tests/test_tools_offline.py` lines 28 to 56; `infra/scripts/phase/tests/test_workflow.py` lines 848 to 860 and 989 to 990; `scripts/decision-log.py` lines 53 to 60 and 142 to 195; `docs/operations/factory-overview/generate-diagrams.py`; `docs/agents/factory.md` "Decisions taken", "Modes of work", "Escalation", "Ruleset", "Sequence to start the factory", "Hard safety rule 8: basis and decision history"; AGENTS.md hard safety rules 8 and 9, "Merge rule", "GitHub issues and pull requests", "Code Review Rules"; docs/blueprint.md lines 582, 1008 and 1026; `.github/CODEOWNERS`; `.claude/skills/control-session/SKILL.md`.
- Ruleset and repository settings read in this session through `gh api` on 2026-11-28: `rules/branches/main` (required status check `.NET verification`, strict; 15 required-reviewer patterns; dismiss stale reviews on; require code owner review on) and the repository (`allow_auto_merge` false, `allow_update_branch` false).

## 12. Open questions for Hari

Pull request 1:
- Q1.1 Should a dispatch decision need your thumbs-up? Recommendation: yes. Agents post decisions from chat as `haripraghash-bot`, so once the reviewer is gone your reaction is the only mark on GitHub that you made the decision; it is one click you can give any time, not at run time. R1.1 gives both texts.
- Q1.2 The gap between configuration jobs. Today your second approval is where the wait of at least 20 minutes after the first job's destroy happens; a 2 minute 42 second gap once made every blob request of the second configuration return 404 (#609 body, citing #491; the 20 minutes is a practice, not a measured bound). Without a reviewer, the second job starts about 120 s after the first destroy. Options: (a) keep the reviewers until a gap mechanism is planned and merged on #609, then remove them; (b) remove them now and accept by name that a two-configuration run may lose its second configuration to the 404 failure; (c) a wait timer on `parchwell-scenarios` alone, which GitHub applies "after the job is initially triggered", so it would delay every job, the first included (inferred from that sentence), and would not space the jobs by itself. Recommendation: (a). Until then you still approve each job, but no rule asks you for anything else at run time. Under (a) the diagrams of pull request 1 show no reviewer while one is still set; the alternative is to hold pull request 1's diagram changes until you remove it, which leaves the diagrams showing a reviewer the rules no longer need.
- Q1.3 A dispatch cap checked by the workflow. Recommendation: none now; it is a workflow change, and R1.1 caps dispatches at the number each decision names. The cap would be text only, like the rest of R1.1.
- Q1.4 Do you accept by name these risks: nothing technical stops an agent dispatching an undecided run on `main` (ADR-023 consequences), and DA1 and DA2?

Pull request 2:
- Q2.1 "Dismiss stale approvals on push": keep it on? Recommendation: keep it on. Your approval then needs to be on the final head, which is when the control session reports ready; the queue removes the second wait, because it does not push to the pull request branch. Turning it off lets an early approval cover later commits (issue body, change 2 risk).
- Q2.2 Merge queue values (build concurrency, minimum and maximum group size, wait time, check timeout) and whether the "branches must be up to date" setting stays on beside the queue. Recommendation: group size 1 and GitHub's defaults for the rest, so each pull request's checks run on its own merge group. These are policy values.
- Q2.3 Do you accept by name DA3 and DA4?
- Q2.4 Codex sessions. AGENTS.md binds both tools, and no Codex route to enable auto-merge is known; under R2.2 a Codex-implemented pull request then waits for you to merge it. Accept that, or ask for a Codex route to be found first? Recommendation: accept it; Codex implements only issues you assign to it.
- Q2.5 R2.9 relies on "Dismiss stale approvals on push" staying on (Q2.1). If you turn it off, your approval would cover commits pushed after it once the agent re-enables auto-merge. Confirm R2.9 together with your Q2.1 answer.

Pull request 3:
- Q3.1 The comment form: `## Control action` with the marker line, as your decision words it, or a new heading such as `## Control decision`. Recommendation: the marker line, as planned.
- Q3.2 Every agent session posts as `haripraghash-bot`, so nothing technical shows which session posted a marked `## Control action`. R3.1 and R4.1 forbid other sessions to post one, but that is text: an implementing session could answer its own question, or post the diff-match record its own merge needs under R4.1. Do you accept that risk by name? Recommendation: accept it for pull request 3, whose answers you can reverse; for pull request 4 see Q4.3.

Pull request 4:
- Q4.1 How the bot account may merge protected paths. Recommendation: shrink the ruleset's required reviewers patterns to the Hari-only list (C4.1). The alternatives are a bypass for the bot, which also bypasses the Hari-only paths, or your approval on each pull request, which is option (a).
- Q4.2 Paths to add to the Hari-only list. Compared by script with the ruleset's 15 patterns on 2026-11-28, your list leaves these agent-merged: from `/docs/`, `docs/blueprint.md` (design authority and ADRs), `docs/engineering/` (binding standards), `docs/traces/` (write-ups that traced verdicts link), `docs/research/` and `docs/networking/`; from `/.*/`, `.github/workflows/` (the path to Azure), `.github/CODEOWNERS`, `.github/pull_request_template.md`, `.claude/agents/` and `.codex/agents/` (the plan-checker and cold-reviewer definitions), `.claude/hooks/` and `.claude/settings.json`, the skills under `.agents/skills/` (including the five repository-owned ones that run planning, plan checks, cold review and the control session), and tool configuration files; from `/*.md`, `README.md` (the published results table) and `CONTEXT.md`; all of `/scripts/` (26 files, including `decision-log.py`, which decides which comments count as authority, and `verify-dotnet.sh` and `check-dotnet-matrix.py`, which the required check runs); `skills-lock.json`; `Parchwell.Verifier*/` (the oracle); and the project files `*.csproj`, `*.sln*` and `Directory.*.props`. Recommendation: add at least `/.github/workflows/`, `/README.md`, `/scripts/`, `/.agents/skills/`, `/.claude/agents/`, `/.codex/agents/` and `/docs/blueprint.md`. GitHub caps the list at 15 patterns; your eight plus those seven make 15, so any further path means merging patterns or a second team (docs/agents/factory.md "Ruleset").
- Q4.3 Combined risk with pull request 1. If workflows are agent-merged and `parchwell-scenarios` has no reviewer, an agent-written workflow on `main` can reach Azure, apply infrastructure and spend money with no human look at any step. Do you accept that by name, or keep `/.github/workflows/` with you (Q4.2)? Recommendation: keep workflows with you.
- Q4.4 Same-vendor review. Under option (b), a Codex-implemented verifier change would be written, reviewed and merged by Codex alone, against ADR-018's consequence. Restrict agent merges of protected paths to Claude-implemented pull requests? Recommendation: yes; R4.4 gives both texts.
- Q4.5 Do you accept by name DA5?

## 13. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope of this plan, when a predecessor pull request in section 2 has not merged, or when Hari's answer to an open question changes a contract here and no revision of this plan carries it.

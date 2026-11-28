# Fewer Hari clicks: contract-grade plan (issue #618)

Revision: R7.

This plan turns the four changes Hari chose on issue #618 into five pull requests. A small test, which reaches no Azure, comes first and shows whether a GitHub wait timer can space a scenario's configuration jobs. Then come, in order: approved plans become the gate for scenario runs, a merge queue replaces the manual merge wait, the control session decides engineering choices, and an agent may merge most protected paths under conditions. Each pull request updates the factory overview diagrams where a drawn gate changes. Hari's decisions settle every design point, every rule exception and every named risk. The last pull request states what agents may merge as a short closed list; every other protected path stays with Hari. Two points remain for his `## Approval` of this revision (section 14).

Base commit: `main` at `7639d40826b2222c7dcc39dc14ab8145a9cf784c`. `main` moved to `6be908bf1a46910d0ad80b574073364a1fae3e40` (pull request #619) before R5; a script found that none of its changed files is a file this plan changes or cites, so the base stands.

Changes since R6 (commit `699460d4aadfab24d398d71aaf389d359af5e562`). R7 applies [decision 7242812866](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242812866), answering [plan check 7242573921](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242573921), and nothing else:
- `docs/blueprint.md` and `docs/research/` leave the closed list, so C4.1 has 20 patterns (points 1 and 2);
- R3.1 and I3.1 exclude accepting a risk by name (point 3);
- C4.1's reason for the `/**` form is corrected with the check's text (finding 3);
- the verifier's two lock files are listed for the approval (section 14).

Changes since R5 (commit `6af772557a5559bcf948344f4fb63753030b71db`). R6 applied [decision 7242375979](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242375979), answering [plan check 7242337701](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242337701), and nothing else:
- the Hari-only list becomes today's ruleset list plus a negation per path on a closed agent-mergeable list (point 4; C4.1, C4.2, R4.1, R4.3, R4.5, R4.7 to R4.9, 8.5), which keeps `/scripts/` and `/docs/traces/` with Hari (points 1 and 2);
- `README.md` and the verifier's tests stay Hari-only under point 4's own rule, with the evidence in C4.2;
- `check-offline.sh`, `docs/research/`, `docs/networking/`, `CONTEXT.md` and the merge-method point are recorded as accepted (points 1 to 3);
- P2-l ("matches" in R4.6).

Changes since R4 (commit `d7d8286fab907ca3541219ce036239e7ca6f7c58`). R5 applied [decision 7242174247](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242174247), answering [plan check 7241932902](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7241932902), and nothing else:
- `/.github/` replaces `/.github/workflows/` on the Hari-only list, and `/.config/` and `/skills-lock.json` join it (point 1; C4.1, R4.3, R4.8);
- an agent merges a protected path only under a plan Hari approved with `## Approval` (point 2; R4.1);
- Q1, Q2 and Q3 are answered (points 3 to 11; sections 9 and 14);
- the P2 fixes P2-g to P2-k (R1.6, R2.3, R2.8, R2.12, R4.8, R4.9);
- the two points the decision leaves for the approval (section 14).

Changes since R3 (commit `64e990f311386be23367270f0e97ef5b9c4abff2`). R4 applied [decision 7241742553](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7241742553), answering [plan check 7240867481](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7240867481), and nothing else:
- text that holds whichever way the wait-timer test goes: R1.1, ADR-023, R1.6 and R1.8 (point 4, check finding 1);
- the exemption from the control-session skill's condition 2 for a dispatch that condition 1 allows (point 5, finding 2, R1.5);
- the d05 label `["control session", "starts it, main only"]` (point 7, finding 3);
- the whole `/.github/workflows/` folder, `docs/engineering/` and the build files stay Hari-only, and the scripted Azure-workflow check is dropped (points 1 and 2, finding 4, C4.1, C4.2, R4.1 to R4.3, R4.7, 8.5);
- `Lifecycle verification` becomes a required check, set by Hari (point 3, section 9);
- every agent commit carries a Claude trailer (point 6, finding 5, R4.1);
- `.agents/skills/repo-review/SKILL.md` step 2 joins pull request 5 (point 7, finding 6, R4.9);
- the six P2 fixes P2-a to P2-f (point 8: 6.5, R2.2, R4.8, 7.5, R1.2, 5.6 and 6.6);
- Hari's thumbs-up time on decision 7240549272 is 14:20:53Z (point 8);
- DA10 (finding 7).

Changes since R2 (commit `30f0ba60e68ca7a150f7f7686d959d569aa297e1`). R3 applied [decision 7240549272](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7240549272) and the findings of [plan check 7240235569](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7240235569):
- new pull request `wait-timer-test` (decision point 1);
- dispatches of plan-named runs by the control session (point 2, R1.1);
- the REST merge route moves to `merge-async` with the recorded `sha`, because the old route "does not support ... merging with a merge queue" (point 3, R2.2);
- auto-merge only for pull requests Hari's approval holds (point 3, R2.9);
- the control-session skill joins pull request `merge-queue` (check finding 6);
- the exclusion of plan amendments, contracts and test values (finding 7, R3.1);
- the relaxed Hari-only list (point 5, C4.1);
- literal replacements for the five sentences that would become false (finding 8, R4.9);
- the live ruleset comparison moves to after merge (finding 9);
- the pattern cap is marked unverified (finding 10);
- the stacked pull request evaluation (point 7, section 11);
- the four adopted plan-text fixes (point 6);
- the P2 fixes for docs/engineering/dotnet-verification.md, the receipt's source pin and plan-check answer (b);
- the deployment assumptions DA6 to DA9 (findings 3, 4 and 9, and the test).

## 1. Goal and authority

Goal: remove the steps where Hari acts a second time on something he already decided, or waits on GitHub, without removing a decision that is his (issue #618 body, "What this proposal does not touch").

Authority, in creation order (docs/agents/factory.md "Item lifecycle" step 3). Each confirmation was read with `scripts/decision-log.py --issue 618`, exit 0, on 2026-11-28:

1. Issue #618 body, updated 2026-11-28T13:15:16Z (a proposal; it authorizes nothing by itself).
2. [Decision 7239274039](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7239274039): every pull request that adopts a change also updates the factory overview diagrams, through `generate-diagrams.py`, with the SVG files regenerated.
3. [Decision 7239287704](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7239287704), confirmed by Hari's thumbs-up: all four changes are pursued.
4. [Decision 7239788015](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7239788015), confirmed: change 3 is option (b).
5. `ready-for-agent` applied by `haripraghash` at 2026-11-28T13:14:25Z. No comment names a tool, so Claude Code implements.
6. [Decision 7240502296](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7240502296): withdrawn by the next decision and never confirmed; nothing here rests on it.
7. [Decision 7240549272](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7240549272), confirmed by Hari's thumbs-up at 2026-11-28T14:20:53Z (reactions API): his answers to R2's questions, points 1 to 7.
8. [Decision 7241742553](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7241742553): his answers to R3's findings, points 1 to 8. It carries no confirmation line, because it adds or keeps gates and otherwise applies decision 7240549272; it authorized R4 and one fresh check beyond the `plan-pr` check cap.
9. [decision 7242174247](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242174247), confirmed by Hari's thumbs-up at 2026-11-28T16:21:28Z (reactions API; created 16:21:03Z): his answers to R4's findings, points 1 to 11. It approves the two rule exceptions, accepts seven risks by name and authorized R5 with one fresh check.
10. [Decision 7242375979](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242375979), confirmed by Hari's thumbs-up at 2026-11-28T16:37:42Z (reactions API; created 16:36:45Z): `/scripts/` and `/docs/traces/` stay with Hari; `check-offline.sh`, `docs/research/`, `docs/networking/`, `CONTEXT.md` and the merge-method point are accepted by name; the agent-mergeable set becomes a closed list; R6 with one fresh check was authorized.
11. [Decision 7242812866](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242812866): `docs/blueprint.md` and `docs/research/` stay Hari-only, and a control-session choice may not accept a risk by name. It carries no confirmation line, because each point keeps a path with Hari or adds a limit; it authorizes R7 with one fresh check.

The #597 standing decision is not adopted here ([control action 7239804179](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7239804179); decision 7240549272), so this plan is approved only by Hari's `## Approval` naming its revision and commit. There is no parent item plan; the plan template's scientific fields do not apply.

## 2. Split by scope

One plan, one plan check and one approval cover an ordered set of five pull requests (docs/agents/factory.md "Item lifecycle" step 3 "Split by scope"). Requirement numbers keep R2's: RT for the test, R1 to R4 for the others.

| Order | Short name | Change | Requirements | Branch |
|---|---|---|---|---|
| 1 | `wait-timer-test` | Decision 7240549272 point 1 | RT | `claude/618-wait-timer-test` |
| 2 | `dispatch-gate` | Change 1 | R1 | `claude/618-dispatch-gate` |
| 3 | `merge-queue` | Change 2 | R2 | `claude/618-merge-queue` |
| 4 | `control-choices` | Change 4 | R3 | `claude/618-control-choices` |
| 5 | `agent-protected-merge` | Change 3 (b) | R4 | `claude/618-agent-protected-merge` |

Each pull request branches from `main` after its predecessor merges, because they edit the same sections in turn. `wait-timer-test` adds a workflow, which Hari merges under AGENTS.md hard safety rule 8. Every other pull request touches AGENTS.md or docs/agents/, which stay Hari's in every option, so Hari merges all five.

Size estimates. These are estimates, not enforced. File counts were computed by script on 2026-11-28 from the scope lists in sections 4 to 8 and the SVG files whose generator calls each pull request changes. Changed lines are estimated from the edits listed below.

| Pull request | Files | Changed lines |
|---|---|---|
| 1 | 4 | about 90 |
| 2 | 12 | about 170 |
| 3 | 14 | about 130 |
| 4 | 5 | about 140 |
| 5 | 12 | about 190 |

## 3. Shared rules for all five pull requests

S1. Diagrams. The pull request edits `docs/operations/factory-overview/generate-diagrams.py` only at the calls this plan names, runs it, and commits every SVG file it rewrites. It never edits an SVG file by hand (decision 7239274039). A pull request whose section names no generator change states in its receipt bullet that no drawn gate changes.

S2. Receipt. The pull request adds or extends a final section `## Amendments` in `docs/operations/factory-overview/receipt.md`, with one bullet per pull request, of exactly this form: `- Issue 618, <short name>: <the claims table rows replaced, by their old first cell, or "no claims row replaced">. Rows replaced here cite files on main after that pull request merged, not at commit 374bbf402e1529bc74e2e11a34352524020138b8. Diagrams regenerated by generate-diagrams.py.` It replaces the claims table rows this plan names, in place, and changes no other part of the receipt.

S3. Where a rule changes, its text is stated once, in the file that owns it, and other files cite it.

S4. ASCII punctuation only, metric units, no em or en dashes.

S5. Standing fixture rule. When an existing test's fixture is rejected by a check this plan adds or tightens, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it. The cold review and Codex review check each listed change. Basis: [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2. The pinned workflow hash in `infra/scripts/phase/tests/test_workflow.py` is not a fixture under this rule; R2.6 sets it.

S6. Settings. No session changes a GitHub setting. Each pull request's description lists the settings Hari changes (section 9), and its completion comment repeats them.

S7. Status lines. A pull request that changes docs/agents/factory.md appends to its status paragraph (line 5) exactly: ` Amended in the pull request for issue #618 (<short name>): <the factory.md sections it changes, in file order, quoted>; Hari's merge of that pull request is the approval.` A pull request that adds or amends an ADR in docs/blueprint.md adds one paragraph after the paragraph on line 16 that begins "ADR-022's amendment 1 for C18, C06 and C04", before "Conventions used in this document:": `The <ADR-023 addition | ADR-018 amendment> follows issue #618 [decision 7240549272](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7240549272); Hari's merge of its pull request records approval.`

S8. Control-session skill. Its tracked file is `.agents/skills/control-session/SKILL.md`; `.claude/skills/control-session` is a symlink to that folder. Its "Act or ask" condition 1 changes in pull requests 2, 4 and 5; each states the full text after it.

## 4. Pull request 1: `wait-timer-test` (decision point 1)

### 4.1 Scope

Files allowed to change: `.github/workflows/wait-timer-test.yml` (new), `docs/operations/wait-timer-test.md` (new), `infra/scripts/lifecycle/check-offline.sh`, `infra/scripts/tests/test_tools_offline.py`. No diagram draws this test.

Must not change: any other workflow, any `docs/agents/` file, AGENTS.md, `infra/` Terraform, `results/`.

### 4.2 External contracts

CT.1 The workflow, exactly:

```yaml
name: Wait timer test
on:
  workflow_dispatch:
permissions:
  contents: read
jobs:
  first:
    runs-on: ubuntu-24.04
    environment: parchwell-wait-timer-test
    timeout-minutes: 10
    steps:
      - name: Record start
        run: echo "first_start_epoch=$(date -u +%s)"
      - name: Run longer than the timer
        run: sleep 90
      - name: Record end
        run: echo "first_end_epoch=$(date -u +%s)"
  second:
    needs: first
    runs-on: ubuntu-24.04
    environment: parchwell-wait-timer-test
    timeout-minutes: 10
    steps:
      - name: Record start
        run: echo "second_start_epoch=$(date -u +%s)"
```

It has no `id-token` permission, no Azure login, no checkout and no secret, so it reaches no Azure.

CT.2 Environment `parchwell-wait-timer-test`: wait timer 1 minute, no required reviewers, no secrets, no variables. Hari creates it before the dispatch (decision point 1).

CT.3 The evidence lines `first_start_epoch=<n>`, `first_end_epoch=<n>`, `second_start_epoch=<n>` in the job logs.

### 4.3 Requirements

RT.1 WHEN the pull request merges THEN the workflow is CT.1, byte for byte, and `expected_triggers` in `check_workflows` gains `'wait-timer-test.yml': {'workflow_dispatch': ''}`.

RT.2 WHEN the pull request merges THEN `docs/operations/wait-timer-test.md` states, in this order: what the test measures (RT.3), the dispatch rule (RT.4), the decision rule (RT.5), and the evidence comment (RT.6). It cites GitHub, "Use a wait timer to delay a job for a specific amount of time after the job is initially triggered." (Deployments and environments, "Wait timer", https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments, read 2026-11-28). It states that GitHub does not define "initially triggered" for a job with `needs:`, which is why the test exists.

RT.3 What is measured: `gap = second_start_epoch - first_end_epoch` and `first_wait = first_start_epoch - run_created_at`, in seconds, computed by script from the job logs and the run's `created_at` from `GET /repos/{owner}/{repo}/actions/runs/{run_id}`. The first job runs 90 s, longer than the 60 s timer. So if the second job's timer started with the run, it has expired when the first job ends, and the gap is runner start-up only. If the second job's timer starts when the job becomes ready, the gap is at least 60 s.

RT.4 Dispatch: after Hari states on #618 that he has created CT.2, the control session dispatches the workflow once, on `main`. The workflow reaches no Azure, so the control-session skill's "Act or ask" condition 1 covers it. Cap: one run. Exhaustion action: `record the gap and continue`: a failed, cancelled or `inconclusive` run is recorded under RT.6, the reviewers stay on `parchwell-scenarios`, and a second run needs Hari's decision (AGENTS.md hard safety rule 8 does not cover it, because the workflow reaches no Azure, so the control session asks him under its skill).

RT.5 Decision rule, in this order:
1. `first_wait < 50`: the timer was not applied; the result is `inconclusive`.
2. `gap >= 50`: `the timer spaces dependent jobs`. Then Hari sets a wait timer on `parchwell-scenarios`, with his policy value, and removes its required reviewers (section 9).
3. `gap <= 20`: `the timer does not space dependent jobs`. Then option (a) of R2's Q1.2 applies: the reviewers stay until issue #609 delivers a gap mechanism.
4. Otherwise: `inconclusive`.

An `inconclusive` result goes to Hari with the evidence. The thresholds are policy choices of this plan, not measurements. The 20 s bound assumes that runner start-up is under 20 s (DA9).

RT.6 Evidence: the control session posts one `## Control action` on #618 with the run link, the three epochs, `created_at`, `gap`, `first_wait`, the classification under RT.5, and the script it used. No file is committed with the results (AGENTS.md "Before opening a pull request" rule 8: agents commit no program output).

### 4.4 Test map (`infra/scripts/tests/test_tools_offline.py`)

| Test | Input | Expected |
|---|---|---|
| existing `test_committed_scenario_items_map_passes_check_workflows` | committed workflows | no exception |
| `test_wait_timer_test_extra_trigger_fails_check_workflows` | copy with `push: {branches: [main]}` added to wait-timer-test.yml | `ValueError` matching `^wait-timer-test\.yml: unexpected workflow triggers$` |

### 4.5 Acceptance

`python3 -m unittest discover -s infra/scripts/tests -p test_tools_offline.py -v` passes; `npm run check:docs`; the ASCII scan; hosted `.NET verification` on the final head; the hosted `Lifecycle verification` result on the final head, recorded in the pull request description.

## 5. Pull request 2: `dispatch-gate` (change 1)

### 5.1 Scope

Files allowed to change: `AGENTS.md`, `docs/agents/factory.md`, `docs/blueprint.md`, `.agents/skills/control-session/SKILL.md`, `infra/scripts/lifecycle/check-offline.sh`, `infra/scripts/tests/test_tools_offline.py`, `docs/operations/factory-overview/generate-diagrams.py`, the SVG files it rewrites, `docs/operations/factory-overview/receipt.md`.

Must not change: any file under `.github/`, `scripts/`, `infra/persistent/`, `infra/ephemeral/`, `results/`, any `expected.yaml`, or any other `docs/operations/` contract. Approved contracts that name the environment approval are not edited; ADR-023 states how they read.

### 5.2 External contracts

- C1.1 Environment `parchwell-scenarios`: deployment branches "Selected branches and tags" with exactly the pattern `main`. Its reviewers and wait timer follow RT.5 (section 9). Its name, secrets, variables and federated credential subject are unchanged.
- C1.2 The offline check message `<file name>: scenario environment job on a trigger other than workflow_dispatch`.
- C1.3 The rule, skill and ADR wording below.

### 5.3 Requirements

R1.1 WHEN the pull request merges THEN AGENTS.md hard safety rule 8 gains, after its sentence "An agent never dispatches a workflow against any ref but `main`.", exactly:

> A workflow with a job that uses the `parchwell-scenarios` environment is dispatched only in two ways. The control session of an issue dispatches a run that an approved plan of that issue names, within the plan's cap on runs, and records each dispatch in a `## Control action` comment with the run link. Any other such run needs a `## Decision` or `## Approval` comment, recognized under docs/agents/factory.md "Item lifecycle" step 3, that names the workflow, its inputs and the number of runs. That covers a run no approved plan names, a run beyond a plan's cap, a re-run of any job, and a second attempt after a failed run; when the decision relaxes a cap, it carries `Confirmation: thumbs-up required`. The approved plan or that decision is the gate for the run. The environment admits only `main`; a required reviewer, where Hari keeps one, approves in addition (docs/blueprint.md ADR-023). A workflow with such a job triggers only on `workflow_dispatch`.

R1.2 WHEN the pull request merges THEN docs/agents/factory.md changes in exactly these places, besides its status line under S7:
- "Item lifecycle" step 4: "The agent starts the scenario workflow." reads "The agent starts the scenario workflow as AGENTS.md hard safety rule 8 allows.";
- the "Decisions taken" row `| Live Azure runs | ... |` reads `| Live Azure runs | Started on main through the repository's workflows, for runs an approved plan names or Hari decides (AGENTS.md hard safety rule 8; docs/blueprint.md ADR-023) |`;
- in "Hard safety rule 8: basis and decision history", the sentence beginning "The Azure identity stays gated by the two GitHub environments" is replaced by: "The Azure identity stays gated by the two GitHub environments, `parchwell-scenarios` and `parchwell-sweeper`, each with a `main`-only deployment branch policy, so a branch workflow gets no Azure token. Since docs/blueprint.md ADR-023, an approved plan or Hari's decision is the gate for a scenario run (hard safety rule 8). That the branch policy refuses a job on another ref is GitHub's documented rule ("Only branches and tags that match your specified name patterns can deploy to the environment"; https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments, read 2026-11-28); the page does not state what the refused job sees, and no branch dispatch has tested it."

R1.3 WHEN the pull request merges THEN docs/blueprint.md, besides its header paragraph under S7:
- gains `### ADR-023: An approved plan or Hari's decision is the gate for a scenario run`, after ADR-022, with:
  - Context: "Each job that references `parchwell-scenarios` waited for Hari's approval, so a run he had already approved in a plan needed one approval per configuration job (issue #618 body, change 1)."
  - Options: "1. Keep required reviewers on every job. 2. One approval per dispatch and a wait timer (issue #609). 3. No required reviewer; the environment admits only `main`; the approved plan, or Hari's decision for any other run, is the gate."
  - Decision: "Option 3 ([decision 7239287704](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7239287704) point 1; [decision 7240549272](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7240549272) points 1 and 2). AGENTS.md hard safety rule 8 owns the dispatch rule. Contracts approved before this ADR that require the protected environment approval read it as that gate. Hari removes the required reviewers only when the wait-timer test (docs/operations/wait-timer-test.md) shows that a wait timer spaces dependent jobs, and then sets the timer; otherwise the reviewers stay until issue #609 delivers a gap mechanism."
  - Consequences: "Nothing technical stops an agent session from dispatching a run that no approved plan names; the rule is text. Once the reviewers are removed, a run spends money without a click at run time; until then, Hari's reviewer approval stays a second gate. The `main`-only branch policy keeps a branch copy of a workflow from deploying to the environment. At most one ephemeral environment exists (hard safety rule 5)."
- in section 8 "Re-validation", the bullet "Scheduled runs wait for Hari's approval on a protected GitHub environment before any apply, so a schedule never spends money unattended." reads "A workflow that uses `parchwell-scenarios` triggers only on `workflow_dispatch` (AGENTS.md hard safety rule 8), so a schedule never spends money unattended; a re-validation run is dispatched under an approved plan or a decision (ADR-023)."
- in section 9 "Control plane", "Terraform apply runs through the protected scenario environment requiring Hari's approval." reads "Terraform apply runs through the main-only scenario environment, gated by an approved plan or Hari's decision (ADR-023)."

R1.4 WHEN `check_workflows` in `infra/scripts/lifecycle/check-offline.sh` reads a workflow any of whose jobs has `environment: parchwell-scenarios`, and its triggers include any event other than `workflow_dispatch`, THEN it raises `ValueError` with exactly `<file name>: scenario environment job on a trigger other than workflow_dispatch`. The triggers are the keys when `on` is a mapping, the one value when `on` is a string, and the items when `on` is a list. A job's `environment` given as a mapping is read from its `name` key. The check runs for every `*.yml` file, after the existing `expected_triggers` comparison.

R1.5 WHEN the pull request merges THEN `.agents/skills/control-session/SKILL.md` "Act or ask":
- condition 2 reads exactly "It can be undone before merge, unless it is a dispatch that condition 1 allows.";
- condition 1 reads exactly "The step only starts, stops or redirects a planning, plan-check, implementation or diagnosis session, dispatches a workflow on `main` that does not reach Azure, dispatches a run that AGENTS.md hard safety rule 8 lets the control session dispatch, or posts a status note.";
- the paragraph that quotes the ask list gains, after "Waiving or changing a gate or cap is also on it.", exactly: "A run that an approved plan names, within its cap, was decided with that plan, so dispatching it is not on the ask list (AGENTS.md hard safety rule 8)."

R1.6 WHEN `generate-diagrams.py` runs THEN these calls produce exactly these strings; every other call is unchanged:
- d01 subtitle (line 91): `Sessions are started by hand. GitHub is the shared memory. Azure is reached only through workflows on main, for runs an approved plan names.`
- d01 aria label (line 92): replace `behind GitHub environment gates.` with `on main, for runs an approved plan names.`
- d01 Hari box (line 98): lines `["Approves item plans, verdicts", "and the runs a plan names"]`.
- d01 box at line 138: title `Main-only environment`, lines `["main only; reviewer", "until the timer test"]`, kind `github`.
- d01 label at line 142: `main only`.
- d01 label at line 148: `dispatches a run Hari decided, main only`.
- d02 steps 11 and 12 (lines 184 and 185): `step(y, "ctl", "az", "11. dispatch a run the approved plan names (main only; reviewer until the timer test); run, then destroy")` and `step(y, "ctl", "gh", "12. record the dispatch as a Control action")`.
- d02 aria label (line 158): replace `an agent dispatches the Azure run, Hari approves it,` with `the control session dispatches the Azure run the plan names,`.
- d03 col1 card at line 207: `("Azure access gates", ["Both environments admit only main;", "Hari sets any reviewer or wait timer"])`.
- d03 col3 card at line 220: `("Scenario runs", ["named by a plan Hari approved"])`.
- d03 known gaps box (line 229): add a fourth line, `"Nothing technical stops an agent from dispatching a run no approved plan names; the rule is text."`.
- d05 aria label (line 279): `Azure run flow: Hari approves a plan that names the run, the control session dispatches the workflow on main, a reviewer approves where Hari keeps one, the job applies and destroys the ephemeral layer, AKS stops when the dispatch ends, and the sweeper cleans up if a job dies.` The d05 subtitle (line 278) is unchanged.
- d05 items 1 and 2 (lines 282 and 283): `("1. Plan", ["Hari approved a plan", "that names this run"], "human")` and `("2. Dispatch", ["control session", "starts it, main only"], "claude")`; notes (line 290) `["names the run", "token, after any reviewer", "", "success or failure", "last job"]`.

R1.7 WHEN the generator runs on the result THEN it prints `ok` as its last line and no line starting `WARN`, and a second run changes no file. This requirement applies to every pull request that changes the generator.

R1.8 WHEN the pull request merges THEN the receipt's claims row whose first cell is "Scenario runs need Hari's approval in the `parchwell-scenarios` environment" reads `| A scenario run is dispatched only as an approved plan names it, or as Hari decides; the parchwell-scenarios environment admits only main and keeps its reviewer until the wait-timer test says otherwise | AGENTS.md hard safety rule 8; docs/blueprint.md ADR-023 | Confirmed as a rule; the environment settings are Hari's and agents cannot read them |`.

### 5.4 Invariants

I1.1 Hard safety rules 1 to 3, 5 to 7 and 9 to 11 keep their text. I1.2 No workflow file changes. All five workflows that use `parchwell-scenarios` trigger only on `workflow_dispatch` on the base commit (read by script), so R1.4 passes on `main` unchanged. I1.3 `parchwell-sweeper` is unchanged.

### 5.5 Test map (`infra/scripts/tests/test_tools_offline.py`)

Each synthetic case writes `synthetic.yml` into a temporary copy of `.github/workflows`, as `scenario_workflow_fixture` does, with the mirror copies `check_workflows` reads. The job is `{runs-on: ubuntu-24.04, environment: parchwell-scenarios, steps: [{run: 'true'}]}` unless stated.

| Test | `on` | Expected |
|---|---|---|
| existing `test_committed_scenario_items_map_passes_check_workflows` | committed workflows | no exception |
| `test_scenario_environment_job_on_schedule_fails_check_workflows` | `{workflow_dispatch: '', schedule: [{cron: '0 0 * * *'}]}` | `ValueError` matching `^synthetic\.yml: scenario environment job on a trigger other than workflow_dispatch$` |
| `test_scenario_environment_job_on_push_fails_check_workflows` | `{workflow_dispatch: '', push: {branches: [main]}}` | same message |
| `test_scenario_environment_job_on_string_push_fails_check_workflows` | `push` | same message |
| `test_scenario_environment_job_on_list_schedule_fails_check_workflows` | `[workflow_dispatch, schedule]` | same message |
| `test_scenario_environment_mapping_on_schedule_fails_check_workflows` | schedule case, job `environment: {name: parchwell-scenarios}` | same message |
| `test_dispatch_only_scenario_environment_job_passes_check_workflows` | `{workflow_dispatch: ''}` | no exception |
| `test_scenario_environment_job_on_list_dispatch_passes_check_workflows` | `[workflow_dispatch]` | no exception |
| `test_sweeper_environment_job_on_schedule_passes_check_workflows` | schedule case, `environment: parchwell-sweeper` | no exception |

### 5.6 Acceptance

| Command | Evidence |
|---|---|
| `python3 -m unittest discover -s infra/scripts/tests -p test_tools_offline.py -v` | all pass, the nine above by name |
| `python3 docs/operations/factory-overview/generate-diagrams.py` twice, then `git status --porcelain docs/operations/factory-overview` | R1.7 |
| `npm ci --ignore-scripts && npm run check:docs` | warnings disposed under AGENTS.md "Before opening a pull request" rule 8 |
| `LC_ALL=C grep -nP '[^\x00-\x7F]'` over every changed file | no output |
| hosted `.NET verification` and `Lifecycle verification` on the final head | success; the `Lifecycle verification` result is recorded in the pull request description, because it runs this pull request's offline tests and is not required until Hari's section 9 step 2 |

## 6. Pull request 3: `merge-queue` (change 2)

### 6.1 Scope

Files allowed to change: `AGENTS.md`, `CLAUDE.md`, `docs/agents/factory.md`, `.agents/skills/control-session/SKILL.md`, `docs/engineering/dotnet-verification.md`, `.github/workflows/dotnet-verification.yml`, `infra/scripts/lifecycle/check-offline.sh`, `infra/scripts/tests/test_tools_offline.py`, `infra/scripts/phase/tests/test_workflow.py`, `docs/operations/factory-overview/generate-diagrams.py`, the SVG files it rewrites, `docs/operations/factory-overview/receipt.md`.

Must not change: `.github/workflows/docs-advisory.yml`, which is not a required check (the ruleset's only required status check is `.NET verification`, read from `GET /repos/.../rules/branches/main` on 2026-11-28); any other workflow; `scripts/`; `results/`.

Rule exceptions this pull request needs, which Hari approved by name in decision 7242174247 point 3: an agent changes the triggers of an existing workflow, `dotnet-verification.yml`, which AGENTS.md hard safety rule 8 forbids today ("An agent never changes an existing workflow's triggers"); and R2.1 amends hard safety rule 9, which the issue body lists as untouched. 

### 6.2 External contracts

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

  GitHub states: "You **must** use the `merge_group` event to trigger your GitHub Actions workflow when a pull request is added to a merge queue." `checks_requested` is the only activity type, and `GITHUB_SHA` is the "SHA of the merge group" (Managing a merge queue, and Events that trigger workflows, `merge_group`; read 2026-11-28). No job body changes. Every checkout uses `github.event.pull_request.head.sha || github.sha` (workflow lines 51, 110, 128, 173 and 227), and the mutation qualification step passes `--base` only for `pull_request` (lines 139 to 150).
- C2.2 Ruleset `main-protection`: rule "Require merge queue", merge method "Merge commit", maximum group size 1, other values GitHub's defaults (decision 7240549272 point 3). "Dismiss stale approvals on push" stays on. Repository setting "Allow auto-merge" on. Hari sets these after merge (section 9), once he has confirmed in the ruleset screen that the merge queue is available (DA7).
- C2.3 The merge procedure in R2.2 and R2.9.

### 6.3 Requirements

R2.1 WHEN the pull request merges THEN AGENTS.md changes:
- hard safety rule 8: "and `dotnet-verification.yml` and `docs-advisory.yml` keep theirs." reads "`dotnet-verification.yml` keeps its triggers as amended for issue #618, which added `merge_group`, and `docs-advisory.yml` keeps its own.";
- hard safety rule 9 reads: "No workflow that reaches Azure may trigger on `pull_request`, `pull_request_target` or `merge_group`. The repository is public; fork pull requests must never run inside the network.";
- "Code Review Rules": "adds a `pull_request` or `pull_request_target` trigger" reads "adds a `pull_request`, `pull_request_target` or `merge_group` trigger".

R2.2 WHEN the pull request merges THEN the AGENTS.md "GitHub issues and pull requests" bullet that begins "Merge only through the REST merge route" reads exactly:

> Merge only under GOVERNANCE "Merge rule", verified on the head `sha` you record; do not take that `sha` from a fresh read afterwards. Merge through the REST route `PUT /repos/{owner}/{repo}/pulls/{pull_number}/merge-async` with that `sha`, `merge_action` `default` and `merge_method` `merge`, which adds the pull request to the merge queue, and which GitHub cancels if the head moves ("SHA that pull request head must match to allow merge"; REST API endpoints for pull requests, read 2026-11-28). The older route `PUT /repos/{owner}/{repo}/pulls/{pull_number}/merge` "does not support stacked pull requests or merging with a merge queue" (same page). A 202 answer is pending: read its result with `GET /repos/{owner}/{repo}/pulls/{pull_number}/merge-async/{uuid}`. An `enqueued` result is not a merge, so confirm the merge on the pull request afterwards. The queue merges with a merge commit, as Hari decided on 2026-11-17 ([decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6958521790)); a change of method is his decision. For a pull request that waits for Hari's approval, GOVERNANCE "Merge rule" sets the route. The merge-group commit that the queue builds is checked by the required checks, not by Codex; the reviewed commit is the pull request head. If the route is refused, do not merge by another route: the pull request waits for Hari.

R2.3 WHEN the pull request merges THEN docs/agents/factory.md, besides its status line under S7:
- "Ruleset" table gains, after the row "Dismiss stale approvals on push", the rows `| Required status checks | .NET verification, and Lifecycle verification once Hari adds it (issue #618, decision 7241742553 point 3); branches must be up to date | .NET verification and strict mode read from the ruleset on 2026-11-28 |` and `| Merge queue | Required; merge method merge commit; maximum group size 1 | Brings each pull request up to date and runs its checks before merging (issue #618, change 2). The checks run on the merge_group event |`;
- "Ruleset" row "Dismiss stale approvals on push" reason cell reads `A push after Hari's approval clears it, and so does a change of the merge base, for example when another pull request merges first ("Available rules for rulesets", read 2026-11-28); Hari accepts the extra approval ([decision 7240549272](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7240549272) point 3)`;
- "Sequence to start the factory" step 6: "sessions use AGENTS.md's REST merge procedure" reads "sessions use the merge procedure of AGENTS.md "GitHub issues and pull requests"".

R2.4 WHEN `check_workflows` reads a workflow whose triggers include `merge_group` THEN the existing Azure-identity checks for `pull_request` and `pull_request_target` apply to it, with their existing messages (`<file>: PR workflow requests Azure identity`, `<file>: PR job requests Azure identity`).

R2.5 WHEN `check_workflows` compares triggers THEN `expected_triggers['dotnet-verification.yml']` is exactly `{'pull_request': '', 'push': {'branches': ['main']}, 'merge_group': {'types': ['checks_requested']}, 'workflow_dispatch': ''}`.

R2.6 WHEN the workflow changes THEN `BASE_WORKFLOWS['.github/workflows/dotnet-verification.yml']` in `infra/scripts/phase/tests/test_workflow.py` is the SHA-256 of the new file bytes, computed by script. Line 851 of the comment above `BASE_WORKFLOWS` reads exactly `    # the coordinated C5 campaign of issue #494, the full-suite mode of issue #602, and the merge_group trigger of issue #618.` No other hash or line changes.

R2.7 Diagrams:
- d01 box at line 114: lines `["merge queue only;", "protected paths: Hari"]`.
- d02 step 10 (line 183): `10. fix, then queue for merge or hand off`.
- d03 col1 card at line 206: `("Ruleset on main", ["Merge queue; no direct or force pushes;", "protected paths need Hari's approval"])`.

R2.8 Receipt: the row whose first cell is "`main` blocks deletions and force pushes; protected paths need `repo-owners` approval" reads `| main requires a merge queue and blocks deletions and force pushes; protected paths need repo-owners approval | docs/agents/factory.md "Ruleset" | Confirmed as a rule; the ruleset is set by Hari |`, and the S2 bullet names that row.

R2.9 WHEN the pull request merges THEN AGENTS.md GOVERNANCE "Merge rule" gains, after its list and before "Anything else waits for Hari.", exactly: "An agent may enable auto-merge on its pull request that touches a protected path when every other condition above holds on the recorded head. The required reviewers rule then holds the merge until Hari approves, so his approval is his merge decision and the last step. A push after his approval, or a change of the merge base, dismisses it (docs/agents/factory.md "Ruleset")."

R2.10 WHEN the pull request merges THEN CLAUDE.md gains a section `## Merging`, between the sections "Identity" and "Before opening a pull request", with exactly: "Claude Code enables auto-merge with the GitHub MCP server's `enable_pr_auto_merge` tool, with `mergeMethod` `MERGE`, and disables it with `disable_pr_auto_merge` (AGENTS.md GOVERNANCE "Merge rule"). Source: the tools' descriptions as they appeared in one cloud session, read 2026-11-28: "Enable auto-merge for a pull request. The PR will merge automatically once all required checks pass and approvals are met. Fails gracefully if auto-merge is not enabled for the repository or if the PR is already mergeable (clean status)." and "Disable auto-merge for a pull request that currently has it enabled." It is tool metadata, not official documentation. A Codex session has no known route to auto-merge, so a Codex-implemented pull request waits for Hari to merge it ([decision 7240549272](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7240549272) point 3)."

R2.11 WHEN the pull request merges THEN `.agents/skills/control-session/SKILL.md` "Working method", after the bullet "Check the head, every check and Codex state for the exact head before saying "ready".", gains the bullet: `- For a pull request that waits for Hari's approval, say "ready" only after auto-merge is enabled on that head (AGENTS.md GOVERNANCE "Merge rule").`

R2.12 WHEN the pull request merges THEN docs/engineering/dotnet-verification.md line 173: "for pull requests, pushes to `main` and manual dispatch." reads "for pull requests, pushes to `main`, merge queue groups (`merge_group`) and manual dispatch.", and line 177: "`.NET verification`, the required check, is an aggregate job." reads "`.NET verification`, a required check, is an aggregate job."

### 6.4 Invariants

I2.1 No rule tells an agent to bypass the queue. I2.2 The conditions of GOVERNANCE "Merge rule" are unchanged; R2.9 adds a route, not a condition. I2.3 The head pin is kept for every agent merge: `merge-async` takes the recorded `sha`. Auto-merge has no head pin; there, Hari's approval is the pin, and a push dismisses it.

### 6.5 Test map

| Requirement | Test | Input | Expected |
|---|---|---|---|
| R2.5 | existing `test_committed_scenario_items_map_passes_check_workflows` | committed workflows with C2.1 | no exception |
| R2.5 | `test_dotnet_verification_without_merge_group_fails_check_workflows` | copy with `merge_group` removed from dotnet-verification.yml | `ValueError` matching `^dotnet-verification\.yml: unexpected workflow triggers$` |
| note | the three synthetic cases below | job `{runs-on: ubuntu-24.04, steps: [{run: 'true'}]}` with no `environment`; the login case adds the step `{uses: azure/login@v2}` | as listed |
| R2.4 | `test_merge_group_workflow_with_identity_fails_check_workflows` | `synthetic.yml`, `on: {merge_group: {types: [checks_requested]}}`, top-level `permissions: {id-token: write}` | `^synthetic\.yml: PR workflow requests Azure identity$` |
| R2.4 | `test_merge_group_job_with_azure_login_fails_check_workflows` | same triggers, one job with a step `uses: azure/login@v2` | `^synthetic\.yml: PR job requests Azure identity$` |
| R2.4 boundary | `test_merge_group_workflow_without_identity_passes_check_workflows` | same triggers, `permissions: {contents: read}`, no login step | no exception |
| R2.6 | existing `B10AbruptWorkflowTests` hash test | committed workflows | passes with the new hash |

### 6.6 Acceptance

As 5.6, including the `Lifecycle verification` record, plus `python3 -m unittest discover -s infra/scripts/phase/tests -p test_workflow.py -v` (all pass). The hosted `.NET verification` run on the pull request shows that the workflow still runs on `pull_request`.

## 7. Pull request 4: `control-choices` (change 4)

### 7.1 Scope

Files allowed to change: `docs/agents/factory.md`, `.agents/skills/control-session/SKILL.md`, `scripts/decision-log.py`, `scripts/tests/test_decision_log.py`, `docs/operations/factory-overview/receipt.md` (S2 bullet only). No diagram draws who answers a plan question (searched with `grep -n "decision\|question\|finding"` over generate-diagrams.py on the base commit).

Must not change: AGENTS.md, CLAUDE.md, any other skill, any workflow.

### 7.2 External contracts

- C3.1 The comment form: heading `## Control action`, with the line `Engineering choice: decided by the control session` on its own line (decision 7240549272 point 4).
- C3.2 The decision-log entry kind `Control decision`.

### 7.3 Requirements

R3.1 WHEN the pull request merges THEN docs/agents/factory.md "Control session" gains, after its paragraph, exactly:

> ### Engineering choices
>
> The control session of an issue decides a plan-check finding, or a question a planning or implementing session raises, when the answer changes none of these: an expected outcome, a verdict rule, a hypothesis, published wording, a gate, a cap, a policy value, the scope of what is tested, a protected path beyond those the plan already names, spend, an approved plan, a contract, requirement or test-map value of a contract-grade plan, a risk accepted by name, or anything on the ask list of the `control-session` skill's "Act or ask". Where these lists differ, the longer exclusion wins. Only the control session of the issue posts such a comment; a planning, plan-check, implementing or review session never does, for its own work or another's. It records each answer in one comment on the issue headed `## Control action` that carries the line `Engineering choice: decided by the control session` on its own line, links the finding or question, states the answer, and states why each item of that list is unchanged. "Item lifecycle" step 3 recognizes that comment as authority for that answer only. A later `## Decision` or `## Approval` that differs wins; Hari reverses an answer that way. Every other finding or question goes to Hari. This rule grants no authority to approve or amend a plan, post `## Approval` or merge, and needs no thumbs-up. Basis: [decision 7239287704](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7239287704) point 4 and [decision 7240549272](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7240549272) point 4.

R3.2 WHEN the pull request merges THEN docs/agents/factory.md, besides its status line under S7:
- "Item lifecycle" step 1: "An open question in the plan is a finding that requires a decision by Hari." reads "An open question in the plan is a finding that requires a decision by Hari, or an answer by the control session under "Control session" "Engineering choices" where that rule covers it.";
- step 3, the authority bullet: after "A comment changes that authority only when its heading starts "## Decision" or "## Approval" and it records Hari's decision.", insert "A `## Control action` comment under "Control session" "Engineering choices" also changes it, for its answer only.";
- "Authority reads" catch-up, the second list item reads "the full body of each new comment whose heading starts "## Decision" or "## Approval", each `## Control action` comment that carries the line `Engineering choice: decided by the control session`, and every decision such a comment links and adopts;";
- "Decision log": after "the line must start with "## Decision" or "## Approval".", insert "It also prints, as `Control decision`, a comment from those logins whose first non-empty line starts with "## Control action" and that has the line `Engineering choice: decided by the control session` on its own line."

R3.3 WHEN the pull request merges THEN `.agents/skills/control-session/SKILL.md` "Act or ask":
- condition 1 reads exactly "The step only starts, stops or redirects a planning, plan-check, implementation or diagnosis session, dispatches a workflow on `main` that does not reach Azure, dispatches a run that AGENTS.md hard safety rule 8 lets the control session dispatch, posts a status note, or decides an engineering choice under docs/agents/factory.md "Control session" "Engineering choices".";
- after "When you ask, post a comment ...", the section gains exactly: "When a finding or question falls under docs/agents/factory.md "Control session" "Engineering choices", decide it yourself and record it as that rule sets, instead of asking. An item on the ask list is never an engineering choice.";
- "That heading carries no authority under docs/agents/factory.md "Item lifecycle" step 3." reads "That heading carries no authority under docs/agents/factory.md "Item lifecycle" step 3, except as "Engineering choices" there sets."

R3.4 WHEN `scripts/decision-log.py` reads a comment from `haripraghash` or `haripraghash-bot` whose first non-empty line starts `## Control action`, and whose body has a line equal, after stripping spaces, to `Engineering choice: decided by the control session`, THEN `kind()` returns `Control decision`. Its entry renders as `### <n>. Control decision #<issue> comment <id>`, in creation order with the other entries, with its body verbatim and its links handled as for decisions. A `## Control action` comment without that line, or from another login, returns `None`. The rendered "Not printed" text for a linked comment of no kind is unchanged.

### 7.4 Invariants

I3.1 The thumbs-up rule of docs/agents/factory.md "Item lifecycle" step 3 is unchanged, and no engineering choice can reach a decision it covers: amending an approved plan, relaxing a gate or cap, and accepting a risk by name are all excluded by R3.1 ([decision 7242812866](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242812866) point 3). I3.2 The ask list is unchanged. I3.3 Existing decision-log output for comments with no `## Control action` heading is byte-identical.

### 7.5 Test map (`scripts/tests/test_decision_log.py`)

| Test | Input | Expected |
|---|---|---|
| `test_marked_control_action_is_a_control_decision` | bot comment `## Control action\n\nEngineering choice: decided by the control session\n\nAnswer.` | entry heading `### 1. Control decision #<issue> comment <id>`, body verbatim |
| `test_unmarked_control_action_is_not_printed` | `## Control action\n\nStarted a session.` as the only comment | output contains the line `No decision or approval comments.` and no line starting `### 1.` |
| `test_control_marker_must_be_its_own_line` | `## Control action\n\nSee: Engineering choice: decided by the control session` as the only comment | output contains the line `No decision or approval comments.` and no line starting `### 1.` |
| `test_control_decision_from_other_login_is_not_printed` | the marked comment by `someone-else` as the only comment | output contains the line `No decision or approval comments.` and no line starting `### 1.` |
| `test_indented_control_marker_is_a_control_decision` | bot comment `## Control action\n\n  Engineering choice: decided by the control session  \n\nAnswer.` as the only comment | entry heading `### 1. Control decision #<issue> comment <id>`, body verbatim |
| `test_control_decision_orders_with_decisions` | a `## Decision` at 10:00:00Z and a marked control action at 09:00:00Z | control decision is entry 1, decision is entry 2 |
| existing `test_recognizes_only_decision_and_approval_headings` | unchanged inputs | unchanged expectations (none of its five inputs starts `## Control action`; read on the base commit, lines 77 to 93) |

### 7.6 Acceptance

`python3 -m unittest discover -s scripts/tests -p test_decision_log.py -v` (all pass), `npm run check:docs`, the ASCII scan, and the hosted `.NET verification` run, whose aggregate job runs this test (workflow line 133).

## 8. Pull request 5: `agent-protected-merge` (change 3, option (b))

### 8.1 Scope

Files allowed to change: `AGENTS.md`, `docs/agents/factory.md`, `docs/agents/pull-requests.md`, `docs/blueprint.md`, `.github/CODEOWNERS`, `.agents/skills/control-session/SKILL.md`, `.agents/skills/repo-review/SKILL.md`, `docs/operations/factory-overview/generate-diagrams.py`, the SVG files it rewrites, `docs/operations/factory-overview/receipt.md`.

Must not change: workflows, `scripts/`, `results/`, any `expected.yaml`, `infra/`.

### 8.2 External contracts

- C4.1 Ruleset `main-protection` required reviewers rule: team `repo-owners`, 1 approval, exactly these 20 patterns, in this order: `/docs/**`, `!/docs/operations/`, `!/docs/operations/**`, `!/docs/networking/`, `!/docs/networking/**`, `/.*/`, `/*.md`, `!/CONTEXT.md`, `/skills-lock.json`, `/infra/persistent/`, `/infra/ephemeral/identity*.tf`, `/infra/ephemeral/network*.tf`, `/scripts/`, `/results/`, `expected.yaml`, `**/Parchwell.Verifier*/**`, `!/src/Parchwell.Verifier/**`, `/Directory.*.props`, `*.sln*`, `*.csproj`. This is today's list of 15 (read from `rules/branches/main` on 2026-11-28), in its order, with `/docs/` written as `/docs/**` and `Parchwell.Verifier*/` as `**/Parchwell.Verifier*/**`, plus negations for each path on the closed agent-mergeable list of [decision 7242375979](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242375979) point 4, placed after the pattern it overrides. GitHub: "A pattern starting with an exclamation mark (`!`) is a negation. This will cause paths matching earlier patterns to *not* require approvals." and "Patterns are matched in order, so negated patterns can "unmatch" files which matched previous rules." (Available rules for rulesets, "Required reviewers", read verbatim 2026-11-28). `!/src/Parchwell.Verifier/**` comes before `*.csproj`, so the verifier's project file stays Hari-only. The `/**` form and the paired directory negations make the list mean the same under Git's `.gitignore` rules, where a file under an excluded directory cannot be re-included, and under GitHub's documented rule above; with today's `/docs/` form, Git would keep `docs/operations/` matched. GitHub states that "The format of this file list is the same as a standard `.gitignore` file" (same page, read verbatim 2026-11-28); whether today's ruleset leaves `docs/operations/` unmatched is not verified (docs/agents/factory.md "Escalation", "Verification still tracked by #17") (8.5). That the two forms match the same files on GitHub is GitHub's documented rule applied to file paths, not a test (DA5). Every path that earlier decisions keep with Hari is covered by a pattern already in today's list: `/docs/traces/`, `/docs/engineering/` and `/docs/agents/` by `/docs/`; `/scripts/` by itself; `/.github/`, `/.config/`, `/.agents/`, `/.claude/` and `/.codex/` by `/.*/`; AGENTS.md, CLAUDE.md and README.md by `/*.md`; `/skills-lock.json` by itself. If the ruleset screen refuses 20 patterns or the `**` form, Hari says so on the issue, and the next session proposes another form; no agent merges a path on the closed list until the list is set. Hari sets this after merge (section 9).
- C4.2 Terms: the "closed list" of agent-mergeable protected paths is exactly `docs/networking/`, `CONTEXT.md` and `src/Parchwell.Verifier/` without its project file; `docs/operations/` stays unprotected, as today. A "Hari-only path" is any path the patterns of C4.1 match: every protected path except the closed list. Four paths named in point 4 stay Hari-only under its own rule, because they hold a public claim or a check. `docs/blueprint.md` holds the basis that 13 published `untestable` verdicts cite and the ADR-021 rules that select the B10 restart verdict, and `docs/research/` is linked from the published A02 result ([decision 7242812866](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242812866) points 1 and 2). `README.md` holds the published results table with a Verdict column (lines 9 to 14), and `tests/Parchwell.Verifier.Tests/` holds the mutation qualification that the required `.NET verification` runs (`.github/workflows/dotnet-verification.yml` lines 139 to 150). So only the verifier's source is on the list. Nothing is added to the closed list without a decision.
- C4.3 The merge-rule wording in R4.1.

### 8.3 Requirements

R4.1 WHEN the pull request merges THEN AGENTS.md GOVERNANCE "Merge rule" reads, in place of the bullet "no protected path is touched (...)":

> - no Hari-only path is touched, and the pull request is not labelled `verdict`. Hari-only paths are every protected path except the closed list of agent-mergeable paths in docs/agents/factory.md "Ruleset"; the ruleset's required reviewers rule blocks the merge of any other until Hari approves;
> - when a path on that closed list is touched: the plan the pull request implements was approved by Hari's `## Approval` under docs/agents/factory.md "Item lifecycle" step 3; the control session of its issue posted a `## Control action` comment recording that the diff at the recorded head `sha` matches that plan's scope and literal text; Codex's security review completed on that head; and every commit, other than merge commits and commits authored by Hari's account, carries a `Co-Authored-By: Claude` trailer, and none carries a `Co-Authored-By: Codex` trailer;

The Codex bullets stay. "Anything else waits for Hari. Never ask for the gate to be relaxed." is unchanged. R2.9's sentence reads, in place of "on its pull request that touches a protected path": "on its pull request that touches a Hari-only path".

R4.2 AGENTS.md hard safety rule 8 is unchanged: `/.github/workflows/` stays a Hari-only path, so "Agents change files under `.github/workflows/` only through a pull request that Hari merges" stays true (decision 7241742553 point 2).

R4.3 WHEN the pull request merges THEN docs/agents/factory.md, besides its status line under S7:
- "Modes of work": the rows "Verifier logic" and "Persistent infrastructure, identity, network, workflows, governance, ADRs" become three rows:
  - `| Verifier source (src/Parchwell.Verifier/, without its project file) | Agent-merged under the protected-path conditions of AGENTS.md GOVERNANCE "Merge rule" | It is the oracle for every result; its judgement logic is also covered by mutation testing (docs/engineering/dotnet.md "Testing"). Hari chose to drop the human look ([decision 7239788015](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7239788015)). Its tests, which hold the mutation qualification the required check runs, stay Hari-only ([decision 7242375979](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242375979) point 4) |`
  - `| docs/networking/, CONTEXT.md | Agent-merged under the same conditions | The closed list ([decision 7242375979](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242375979) point 4) |`
  - `| Every other protected path: expected outcomes, results, README.md, docs/blueprint.md (ADRs), docs/research/, persistent infrastructure, identity, network, the dot folders (workflows, CODEOWNERS, tool pins, skills, agent definitions), skills-lock.json, scripts, build files, docs/ outside the closed list (governance, engineering standards, traces), AGENTS.md and CLAUDE.md | Human-reviewed (Hari-only path) | Public claims, the checks and path list that gate an agent's own merge, and long-lived decisions; Hari-only by default ([decision 7242375979](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242375979) point 4) |`
- the paragraph below the table: "Protected paths are those in the ruleset and CODEOWNERS;" reads "Protected paths are those in CODEOWNERS; the ruleset keeps every one with Hari except the closed list of agent-mergeable paths in "Ruleset";";
- "Escalation" layer 1, first sentence: "The `main-protection` ruleset's required reviewers rule lists every Hari-only path and requires one approval from the `repo-owners` team (Hari) when a pull request touches one.";
- "Ruleset" row "Required reviewers": `| Required reviewers | Team repo-owners, 1 approval, these 20 patterns in this order: /docs/**, !/docs/operations/, !/docs/operations/**, !/docs/networking/, !/docs/networking/**, /.*/, /*.md, !/CONTEXT.md, /skills-lock.json, /infra/persistent/, /infra/ephemeral/identity*.tf, /infra/ephemeral/network*.tf, /scripts/, /results/, expected.yaml, **/Parchwell.Verifier*/**, !/src/Parchwell.Verifier/**, /Directory.*.props, *.sln*, *.csproj | The gate for Hari-only paths: every protected path except the closed list of agent-mergeable paths, which the negations name: docs/networking/, CONTEXT.md and src/Parchwell.Verifier/ without its project file (decision 7242375979 point 4; decision 7242812866 points 1 and 2). A path not on that list stays with Hari by default. GitHub states "You can specify up to 15 different teams" and states no limit on patterns ("Available rules for rulesets", read 2026-11-28); the limit of 15 patterns recorded here in October 2026 is not verified |`;
- "Sequence to start the factory" step 6: "Hari merges every pull request touching a protected path." reads "Hari merges every pull request touching a Hari-only path; agents merge other protected-path pull requests under GOVERNANCE "Merge rule"."

R4.4 WHEN the pull request merges THEN, besides its header paragraph under S7, the docs/blueprint.md ADR-018 consequence "Codex-authored pull requests have no reviewer from another vendor. Protected paths and the verifier still need Hari's approval, so the oracle and the public claims are never exposed to same-vendor review alone." reads "Codex-authored pull requests have no reviewer from another vendor. Hari-only paths still need Hari's approval, and an agent merges another protected path only when Claude Code implemented the pull request ([decision 7240549272](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7240549272) point 5), so the oracle and the public claims are never exposed to same-vendor review alone."

R4.5 WHEN the pull request merges THEN the `.github/CODEOWNERS` header comment reads "# Review requests only. The merge gate is the required reviewers rule in the main-protection ruleset, which keeps every protected path with Hari except the closed list of agent-mergeable paths (docs/agents/factory.md, "Ruleset"). This file lists every protected path. A path listed with no owner has no owner." The path lines are unchanged.

R4.6 WHEN the pull request merges THEN `.agents/skills/control-session/SKILL.md`:
- in the ask list paragraph, "and protected-path merges" reads "and merges of Hari-only paths";
- condition 1 reads exactly "The step only starts, stops or redirects a planning, plan-check, implementation or diagnosis session, dispatches a workflow on `main` that does not reach Azure, dispatches a run that AGENTS.md hard safety rule 8 lets the control session dispatch, posts a status note, posts the `## Control action` that AGENTS.md GOVERNANCE "Merge rule" requires after checking by script that the diff matches the approved plan's scope and literal text, or decides an engineering choice under docs/agents/factory.md "Control session" "Engineering choices"."

R4.7 Diagrams:
- d01 box at line 114: lines `["merge queue only;", "Hari-only paths: Hari"]`.
- d03 col1 card at line 206: `("Ruleset on main", ["Merge queue; no direct or force pushes;", "Hari-only paths need his approval"])`; card at line 208: `("Workflow files", ["Hari-only paths; runs from outside", "contributors need Hari's approval"])`.
- d03 col3 card at line 222: `("Hari-only paths (all but a short list)", ["expected outcomes, results, infra,", "governance, skills, workflows, builds"])`.
- d03 known gaps box (line 229): height `116` in place of `100`, and a fifth line `"The verifier source can merge with no human look; the merge conditions are text."`.
- d06 check at line 331: `("Hari-only path touched?", ["ruleset requires Hari", "(built)"], "human", False)`; box at line 346: lines `["all but Hari-only paths,", "under the merge rule"]`.

R4.8 Receipt: the row whose first cell is "Protected paths include workflows, governance documents and ADRs, the verifier, scripts, results and project files; the full list is in CODEOWNERS" (line 90) reads `| Hari-only paths are every protected path except a closed list: docs/networking/, CONTEXT.md and the verifier's source; the list is in docs/agents/factory.md "Ruleset" | docs/agents/factory.md "Ruleset" row "Required reviewers"; AGENTS.md GOVERNANCE "Merge rule" | Confirmed as a rule; the ruleset is set by Hari |` (P2-c, with "the workflows that reach Azure" replaced as decision 7241742553 point 2 requires). The claims rows whose first cells are "Agent merge is enabled for pull requests touching no protected path" and "main requires a merge queue and blocks deletions and force pushes; protected paths need repo-owners approval" (as pull request 3 leaves it) read `| Agents merge pull requests that touch no Hari-only path, under the merge rule | AGENTS.md GOVERNANCE "Merge rule"; docs/agents/factory.md "Sequence to start the factory" step 6 | Confirmed as a rule |` and `| main requires a merge queue and blocks deletions and force pushes; Hari-only paths need repo-owners approval | docs/agents/factory.md "Ruleset" | Confirmed as a rule; the ruleset is set by Hari |`.

R4.9 WHEN the pull request merges THEN these sentences change, and no others outside R4.1 to R4.8:
- AGENTS.md line 13: "AGENTS.md is a protected path, so each governance change reaches `main` only through Hari's merge." reads "AGENTS.md is a Hari-only path (GOVERNANCE "Merge rule"), so each governance change reaches `main` only through Hari's merge.";
- docs/agents/factory.md "Escalation" layer 1: "`.github/CODEOWNERS` mirrors the same list so GitHub requests Hari's review automatically, but it is not the gate" reads "`.github/CODEOWNERS` lists every protected path, a longer list, so GitHub requests Hari's review automatically, but it is not the gate";
- docs/agents/factory.md "Escalation" layer 1: "Agent merge is enabled for unprotected paths under" reads "Agent merge is enabled under GOVERNANCE "Merge rule" and";
- docs/agents/factory.md "Ruleset" Bypass row: "A protected-path pull request it opens therefore needs an approval from `repo-owners`, or Hari's bypass" reads "A pull request it opens that touches a Hari-only path therefore needs an approval from `repo-owners`, or Hari's bypass";
- docs/agents/factory.md "Ruleset" row `| Required approvals | 0 | Agent pull requests on unprotected paths merge without a human |` reads `| Required approvals | 0 | Agent pull requests that touch no Hari-only path merge without a human approval |`;
- docs/agents/factory.md "Ruleset": "any other document under `docs/` comes to Hari." reads "any other document under `docs/` comes to Hari, except `docs/networking/`, which is on the closed list of agent-mergeable paths (row "Required reviewers").";
- docs/agents/factory.md "Escalation", "Verification still tracked by #17": "that a pull request opened by the factory's GitHub App on a protected path is blocked until Hari approves" reads "that a pull request opened by the factory's GitHub App on a Hari-only path is blocked until Hari approves", and "that a pull request touching only `docs/operations/` is not blocked (the negation pattern);" reads "that a pull request touching no Hari-only path is not blocked;";
- docs/agents/pull-requests.md line 5: "`/docs/` is a protected path (GOVERNANCE "Merge rule"; `.github/CODEOWNERS` mirrors the list), so a change to this file reaches `main` only through Hari's merge" reads "`/docs/agents/` is a Hari-only path (GOVERNANCE "Merge rule"), so a change to this file reaches `main` only through Hari's merge";
- docs/blueprint.md line 487: "and `.github/CODEOWNERS` mirrors the list" reads "and `.github/CODEOWNERS` lists it too";
- `.agents/skills/repo-review/SKILL.md` step 2: "Those files are protected paths (the ruleset's required reviewers rule, which `/.*/` in `.github/CODEOWNERS` mirrors; docs/agents/factory.md "Ruleset"), so such a change reaches `main` only through Hari's merge." reads "Those files are Hari-only paths (the ruleset's required reviewers rule lists `/.claude/` and `/.agents/`; docs/agents/factory.md "Ruleset"), so such a change reaches `main` only through Hari's merge.";
- AGENTS.md "Skills": "They are not in `skills-lock.json` and change only through a pull request to Hari." is unchanged, because `.agents/` stays Hari-only.

### 8.4 Invariants

I4.1 Each of the four conditions of decision 7239788015 is in R4.1 or in the existing Merge rule bullets: an approved plan, the control-session diff record, the required checks, and Codex's code and security reviews. I4.2 Verdict pull requests always go to Hari. I4.3 docs/agents/pull-requests.md rule 8's third paragraph is unchanged.

### 8.5 Test map and acceptance

No code changes. Before merge, a script compares the ordered pattern list in R4.3's "Required reviewers" row with C4.1, order included, and prints any difference; the pull request description records an empty difference. The same script checks, with `git check-ignore --no-index` on a temporary `.gitignore` holding the C4.1 patterns (this session ran that check on 2026-11-28 and got the results listed), that each of these paths is matched (Hari-only): `README.md`, `AGENTS.md`, `docs/traces/x.md`, `docs/engineering/x.md`, `docs/agents/x.md`, `scripts/x.py`, `.github/CODEOWNERS`, `.github/workflows/x.yml`, `.config/dotnet-tools.json`, `src/Parchwell.Verifier/Parchwell.Verifier.csproj`, `tests/Parchwell.Verifier.Tests/x.cs`, `results/x.md`, `scenarios/A01/expected.yaml`, `docs/operations/evidence/Parchwell.Verifier.Tests/x.txt`, `docs/blueprint.md`, `docs/research/x.md`, `docs/research/sub/z.md`; and that each of these is not matched: `docs/networking/x.md`, `docs/networking/sub/z.md`, `CONTEXT.md`, `src/Parchwell.Verifier/x.cs`, `src/Parchwell.Verifier/VerifyRun/y.cs`, `docs/operations/x.md`. It prints each path with its result. GitHub's pattern matching may differ from Git's, so this checks the list's intent, not GitHub's behaviour (DA5).

## 9. Deployment assumptions and settings

Each assumption below has no offline test. Hari accepted DA1 to DA7, DA9 and DA10 by name in [decision 7242174247](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242174247) points 5 to 10, and DA8 in decision 7240549272 point 3. The addition to DA6 is accepted in [decision 7242375979](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242375979) point 3. None was taken on a connection to a deployed service, fresh or already open, idle or under load, unless stated.

- DA1 (pull request 2). A `main`-only deployment branch policy refuses a job on another ref, so that job gets no secret and no OIDC token for `parchwell-scenarios`. Evidence: GitHub documentation only, read 2026-11-28; the page does not state what the refused job sees. `parchwell-sweeper` relies on the same rule today.
- DA2 (pull request 2). Agents cannot read environment settings: `GET /repos/.../environments` returned HTTP 403 through the session proxy on 2026-11-28, a repository API read. The evidence of each setting is Hari's statement on the issue.
- DA3 (pull request 3). `enable_pr_auto_merge` puts a protected-path pull request into the queue once Hari approves. Evidence: GitHub, "If the pull request can't be added to the queue immediately, GitHub enables auto-merge and adds the pull request to the queue when requirements are met" (Merging a pull request with a merge queue, read 2026-11-28), and the tool metadata in R2.10. Untested. A failure is safe: the pull request waits for Hari.
- DA4 (pull request 3). `.NET verification` passes on a merge-group commit as it does on a push to `main`. Evidence: source reading of the workflow on the base commit; no run.
- DA5 (pull request 5). After Hari's ruleset change, the rule blocks a bot pull request that touches a Hari-only path and does not block one that touches only other protected paths. Evidence: none yet; the pattern list is readable (repository metadata, read 2026-11-28). "Require review from Code Owners" is on and its effect was never isolated (docs/agents/factory.md "Ruleset"); if it enforces, agent merges of other protected paths stay blocked, which is safe.
- DA6 (pull request 3). `PUT .../merge-async` is reachable through the session proxy and behaves as documented. Addition, accepted by name in decision 7242375979 point 3: R2.2 sends `merge_method` `merge` with `merge_action` `default`, and GitHub documents `merge_method` as "Only supported for direct merges"; what GitHub does with it when the request goes to the queue is not documented. Evidence: the REST page, read verbatim on 2026-11-28; the call was not made. If the proxy refuses it, agent merges wait for Hari until a route is found.
- DA7 (pull request 3). A merge queue is available for this repository. GitHub's merge-queue pages read on 2026-11-28 state no availability rule; only the issue body quotes one. Hari confirms it in the ruleset screen before pull request 3 merges (decision 7240549272 point 3).
- DA8 (pull request 3). An approval is also dismissed when the merge base changes. GitHub: "because a related pull request is merged into the target branch ... the approving review is dismissed as stale" (Available rules for rulesets, read 2026-11-28). Hari accepted the extra approval (decision 7240549272 point 3); it is listed for completeness.
- DA10 (pull request 3). `enable_pr_auto_merge` works for `haripraghash-bot` on a branch that requires a merge queue. Evidence: the tool metadata in R2.10 only; not called. A failure is safe: the pull request waits for Hari.
- DA9 (pull request 1). Runner start-up for `ubuntu-24.04` is under 20 s, so RT.5's lower bound separates the outcomes. This is an assumption, not a measurement; a slower start shows as `inconclusive`, not as a wrong classification, while the gap stays under 50 s.

Settings Hari changes (S6), in order:
1. Before the `wait-timer-test` dispatch: create `parchwell-wait-timer-test` (CT.2).
2. At any time, because each only narrows access: set the `parchwell-scenarios` branch policy to `main` only, and add `Lifecycle verification` to the ruleset's required status checks (decision 7241742553 point 3). That job runs on every pull request: `.github/workflows/dotnet-verification.yml` triggers on `pull_request` with no paths or types filter (lines 3 and 4), the job has no `if:` condition (lines 165 to 168), and it runs `check-offline.sh` and the `infra/scripts/tests` suite (lines 207 to 215). After pull request 3 it also runs on `merge_group`, by the same reading. Its added time per pull request is not measured.
3. After `dispatch-gate` merges and the test result is known: under RT.5, set a wait timer with his value and remove the reviewers, or leave the reviewers.
4. After `merge-queue` merges and DA7 is confirmed: "Allow auto-merge", then the merge queue rule (C2.2).
5. After `agent-protected-merge` merges: the required reviewers patterns (C4.1).

## 10. Deferred work

| Item | On the verdict path | Why deferral is permitted | Who and when | Bound and exhaustion action | Completion evidence | Gate it blocks |
|---|---|---|---|---|---|---|
| Wait-timer test run (RT.4) | Yes: it decides how the gap between configuration jobs is kept | It can only run after pull request 1 merges and Hari creates CT.2 | Control session, after both | One run; `record the gap and continue` (RT.4) | RT.6's `## Control action` | Removal of the reviewers (section 9 step 3) |
| Gap mechanism on #609, if the test shows the timer does not space jobs | Yes | Planned only if needed; until then the reviewers stay, so no verdict loses the gap | Hari decides; a plan on #609 | Not applicable | Merged #609 pull request | Removal of the reviewers |
| Hari's settings (section 9) | No (governance) | Only Hari changes them | Hari | Not applicable | His statement on the issue; for the ruleset, the read below | The rules describe settings not yet in place |
| Live ruleset comparison (check finding 9) | No | The ruleset changes only after pull request 5 merges | The control session, after Hari's step 5 | One read | `GET /repos/.../rules/branches/main`, compared by script with C4.1, posted on #618 | None; a difference goes to Hari |
| First live merge through the queue (DA3, DA6) | No | The queue exists only after Hari's step 4 | The first agent merge after it | One attempt, `record the gap and continue` | The merged pull request, linked on #618 | None; a failure falls back to Hari |
| Stacked pull requests (section 11) | No (optional) | Not needed for any gate here; decision 7242174247 point 11 | A follow-up issue, opened after the merge queue has merged its first pull request | Not applicable | Its own plan | None |

## 11. Stacked pull requests (decision 7240549272 point 7)

Evaluation. All quotations come from GitHub pages read on 2026-11-28, verbatim unless marked.

- What they are: "Stacked pull requests are two or more pull requests in the same repository, where:" the bottom one targets the trunk and each later one "targets the branch of the pull request below it" (About stacked pull requests).
- Merge queue: "Stacks fully support merge queues. All pull requests in the stack are added to the queue in the correct order." (Merging stacked pull requests). Merge methods: "Stacks support merge commit, squash, and rebase merge methods, and they are merge-queue aware." (About stacked pull requests). So Hari's merge-commit decision can stand.
- Linear history: a stack can merge only when "The stack has a linear history"; otherwise "you'll need to rebase the stack before you can merge" (Merging stacked pull requests). A rebase force-pushes every branch of the stack. One page says that a stack rebase generates signed commits, and another that commits from a server-side rebase are not signed; the pages contradict each other. Rebased commits keep their messages, so the trailers of AGENTS.md "Implementer and reviewer" should survive (inferred, not stated).
- API: "This is the required API for merging stacked pull requests." (REST, `merge-async`), which R2.2 already adopts.
- Not stated on any page read: whether auto-merge works for stacks, whether the feature is in preview, and for which plans. The page links carry the campaign tag `stacked-prs-gtm-public-preview-2026`, which suggests a public preview (inferred).
- Conflict with the factory: docs/agents/factory.md "Item lifecycle" step 3 has a pull request of a split planned and built only after its predecessors merge. Using stacks needs that rule changed, and that change is a governance change.

What stacks would buy here, by inference: the next pull request of a split could be built and reviewed before the one below merges, and Hari could merge a whole split with one action. That saves waiting time, not approvals. Every pull request in a stack still needs its own checks and reviews ("Branch protection rules, such as CODEOWNER approvals, are enforced on every pull request in the stack").

Decided (decision 7242174247 point 11): stacks are not added to this split. A follow-up issue is opened once pull request 3's queue has merged its first pull request, because two of the facts above are unknown and one needs a factory rule change.

## 12. Plan-check questions (docs/agents/factory.md "Item lifecycle" step 1)

(a) Steps whose effort scales with a number unknown at planning time: none uncapped. The wait-timer test is one run (RT.4). The first queue merge is one attempt. The live ruleset comparison is one read. Cold-review rounds follow docs/agents/pull-requests.md rule 8's budget.

(b) Where the plan is stricter than its authority, which is the decisions, since there is no parent plan:
- R1.4 and the last sentence of R1.1 bind scenario-environment workflows to dispatch only;
- R1.1 has a decision name the workflow, its inputs and the number of runs, and counts a re-run of any job as needing a decision;
- R2.1 and R2.4 bar `merge_group` on Azure workflows;
- R2.11 orders the ready report after auto-merge is enabled;
- R3.1 excludes the skill's ask list, plan amendments, contracts and test values, and bars every other session from posting an engineering choice;
- R1.1 lets only the control session dispatch a plan-named run without a decision; decision 7240549272 point 2 names the control session but does not bar other sessions;
- RT.5 sets the test's thresholds.

Each one closes a gap that the decisions' own wording would leave open, as stated where it appears. Each is Hari's to approve with this plan.

(c) Dependence on code or behaviour neither on `main` nor in a pull request: GitHub settings and behaviour, DA1 to DA10. No test depends on Azure.

## 13. Sources

- GitHub, Deployments and environments, https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments: "Deployment branches and tags", "Required reviewers", "Wait timer", "Environment secrets"; read 2026-11-28 through fetch subagents.
- GitHub, Managing a merge queue, https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/configuring-pull-request-merges/managing-a-merge-queue; Merging a pull request with a merge queue, https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/incorporating-changes-from-a-pull-request/merging-a-pull-request-with-a-merge-queue; Automatically merging a pull request, https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/incorporating-changes-from-a-pull-request/automatically-merging-a-pull-request; Events that trigger workflows, `merge_group`, https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows; Available rules for rulesets, https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets; all read 2026-11-28.
- GitHub, REST API endpoints for pull requests, "Merge a pull request" and "Merge a pull request asynchronously", https://docs.github.com/en/rest/pulls/pulls (API version 2026-03-10). Read verbatim on 2026-11-28 by this session from `https://docs.github.com/api/article/body?pathname=/en/rest/pulls/pulls`.
- GitHub, About stacked pull requests, https://docs.github.com/en/pull-requests/get-started/about-stacked-prs; Merging stacked pull requests, https://docs.github.com/en/pull-requests/how-tos/merge-and-close-pull-requests/merging-stacked-pull-requests, and its Enterprise Cloud version, whose text is the same; Managing stacked pull requests, https://docs.github.com/en/pull-requests/how-tos/create-pull-requests/managing-stacked-pull-requests. Read 2026-11-28 through a fetch subagent and, for the merge and linear-history sentences, verbatim by this session.
- GitHub MCP server tool descriptions for `enable_pr_auto_merge` and `disable_pr_auto_merge`, as loaded in this session on 2026-11-28; tool metadata, not documentation.
- Repository, on `main` at the base commit: the files and lines each requirement names. Ruleset and repository settings were read with `gh api` on 2026-11-28: `rules/branches/main` (required status check `.NET verification`, strict; 15 required-reviewer patterns; dismiss stale reviews on; require code owner review on) and the repository (`allow_auto_merge` false, `allow_update_branch` false).

## 14. Open questions for Hari

Answered by [decision 7242174247](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242174247) (Q1, Q2 and Q3), by [decision 7242375979](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242375979) and by [decision 7242812866](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/618#issuecomment-7242812866).

Two points for Hari's `## Approval` of this revision:
- **The verifier's lock files.** `src/Parchwell.Verifier/packages.lock.json` and `src/Parchwell.Verifier/packages.linux-x64.lock.json` become agent-mergeable with the verifier's source (the `git check-ignore` run in 8.5). Other projects' lock files are unprotected today. The verifier's `.csproj` and `/Directory.*.props`, which declare its packages, stay Hari-only. Recommendation: accept.
- **`README.md` stays Hari-only.** Decision 7242812866's summary told you that `README.md` outside a verdict pull request stays agent-mergeable. Under decision 7242375979 point 4's rule, a listed path that holds a public claim stays Hari-only, and `README.md` holds the published results table with a Verdict column (lines 9 to 14). So this plan keeps it with you, as R6 already did. Recommendation: confirm that reading in your approval. If you want `README.md` agent-mergeable instead, say so, and a revision adds `!/README.md` after `/*.md`.

## 15. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope of this plan, when a predecessor pull request in section 2 has not merged, or when the wait-timer test or a deployment assumption turns out otherwise than this plan states and no revision carries the result.

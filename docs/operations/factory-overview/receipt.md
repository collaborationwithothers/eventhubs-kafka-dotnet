# Factory overview diagrams: authored receipt

This receipt belongs to the pull request that adds six diagrams of how the agent factory works today and its target state. It records what was authorized, how the diagrams were checked and the source of each factual claim. The diagrams are not a design authority: where they disagree with AGENTS.md or docs/agents/factory.md, those files win.

## Execution card

- **Deliverable:** six SVG files in docs/operations/factory-overview/ and this receipt. No code, workflow, expected outcome, verdict or `results/` file changes.
- **Authorization:** Hari asked in an interactive session on 2026-11-06 for an issue that describes the factory (start, guardrails, shared state, context) with diagrams, then chose hand-drawn SVG files committed under docs/operations/, then asked for the full pre-pull-request process. No approved item plan and no issue exist yet; the issue the diagrams illustrate has not been created. This is directly authorized work, not an approved plan.
- **Path check:** docs/operations/ is not a protected path (docs/agents/factory.md "Ruleset", "Protected paths use `.gitignore` syntax").
- **Mandatory gates:** `npm run check:docs`; cold review under AGENTS.md "Before opening a pull request" rule 8; Codex review of the head commit under GOVERNANCE "Merge rule".
- **Verification commands:** `npm ci --ignore-scripts`, `npm run test:docs`, `npm run check:docs`, and a scan for non-ASCII characters in the six SVG files and this receipt.
- **Earliest publication checkpoint:** after one comprehensive cold review reports no P0 or P1.
- **Review budget:** at most two comprehensive passes and two focused correction rounds (rule 8). Exhaustion action: save unpublished work to the backup branch under rule 12 and hand off to Hari.
- **Cold review, round 1:** mode comprehensive; comparison baseline is `origin/main` at `374bbf402e1529bc74e2e11a34352524020138b8`; the input revision is the staged tree at review time.
- **Merge:** the pull request is left to Hari. It touches no protected path, but it is documentation work with no approved plan.

## Claims and their sources

Sources are files in this repository at commit `374bbf402e1529bc74e2e11a34352524020138b8`, read on 2026-11-06, unless the row says otherwise.

| Diagram claim | Source | Status |
|---|---|---|
| A person starts sessions by hand; a control session creates plan, check, implement and review sessions | Hari's statements in the session of 2026-11-06 | Stated by Hari, not in the repository |
| Codex sessions are started by hand and only for issues assigned to Codex | Hari's statements; docs/agents/factory.md "Decisions taken", row "Where sessions run" | Confirmed |
| `create_session` sets the model per role | CLAUDE.md "Model bindings" | Confirmed (a rule) |
| Hari applies `ready-for-agent`, which approves the plan and names the tool | docs/agents/factory.md "Item lifecycle" step 2 | Confirmed |
| A passing plan check approves a per-pull-request plan without Hari under standing approval | docs/agents/factory.md "Item lifecycle" step 3 "Standing approval" | Confirmed as a rule; whether every item uses it is not known |
| A cold review session is given only the diff and the receipt | AGENTS.md "Before opening a pull request" rule 8; CLAUDE.md "Before opening a pull request" item 2 | Confirmed as a rule |
| The Codex connector reviews every pull request; `@codex review` is the permitted manual form | docs/agents/factory.md "Escalation" layer 2; AGENTS.md hard safety rule 10; Hari's statement that it reviews automatically | Confirmed |
| Agent merge is enabled for pull requests touching no protected path | docs/agents/factory.md "Sequence to start the factory" step 6 | Confirmed |
| `main` blocks deletions and force pushes; protected paths need `repo-owners` approval | docs/agents/factory.md "Ruleset" | Confirmed |
| Workflow files are protected paths; outside contributors' workflow runs need approval | docs/agents/factory.md "Ruleset" (`/.*/` pattern) and "Hard safety rule 8: basis and decision history" | Confirmed |
| Hari approves each scenario run in the `parchwell-scenarios` environment; the sweeper environment is limited to `main` | docs/agents/factory.md "Hard safety rule 8: basis and decision history" | Confirmed |
| Agents dispatch workflows only against `main` | AGENTS.md hard safety rule 8 | Confirmed |
| No Azure workflow triggers on `pull_request`; at most one ephemeral environment | AGENTS.md hard safety rules 5 and 9 | Confirmed |
| Each job destroys its ephemeral layer; AKS stops once when the dispatch ends; the sweeper destroys a leftover environment on its first sweep and stops AKS 60 minutes after the last dispatch ended | AGENTS.md hard safety rule 6 | Confirmed; the 60 minutes is design policy there, not a measurement |
| The persistent layer holds the spoke network, registry, log workspace and identities; only Hari applies it | docs/blueprint.md "Persistent layer"; AGENTS.md hard safety rule 1 | Confirmed |
| CI workflows `.NET verification` and `docs-advisory` run on pull requests | .github/workflows/dotnet-verification.yml and docs-advisory.yml, `on:` blocks | Confirmed |
| Documentation checks are advisory | docs/agents/pull-requests.md rule 5 | Confirmed |
| The state file is `items/<issue number>/state.md` on `main` and indexes the issue | docs/agents/factory.md "Item state file" | Confirmed |
| The catch-up read lists comment IDs and edit times by script and selects comments by role; sessions re-check before publishing a plan, pushing or merging | docs/agents/factory.md "Authority reads" | Confirmed |
| The Stop hook acts only where `GITHUB_ACTIONS=true` or `PARCHWELL_HANDOFF=enforce`, requires a comment starting `**Decision needed:**`, and is not verified in `claude-code-action` | CLAUDE.md "Handoffs" | Confirmed |
| Backup branches are named `<branch>-wip`; no pull request is opened from them | AGENTS.md "Before opening a pull request" rule 12 | Confirmed |
| Backup branches are the recovery path when a session disappears or credits run out | Hari's statement; AGENTS.md rule 11 and rule 12 for the branch itself | Purpose stated by Hari; a session that dies cannot write a handoff is inferred |
| The same-vendor label is applied by a planned merge workflow when a Codex or untrailed commit is present; spot-check samples start with labelled pull requests | docs/agents/factory.md "Escalation" layer 1 and "Measuring the gate" | Confirmed |
| Review-unavailable notice after 60 minutes without a Codex review | docs/agents/factory.md "Escalation" layer 1 | Confirmed; 60 minutes is a governance choice |
| The risk classifier (Jev) is deferred, runs in shadow mode first, can only add escalations, and needs about 50 labelled pull requests, a measured problem and published pricing | docs/agents/factory.md "Deferred: Jev as the second escalation layer" | Confirmed; "or another decision model" is Hari's wording, not the document's |
| The weekly spot-check samples about 5 agent-merged pull requests; it has not started | docs/agents/factory.md "Measuring the gate"; Hari's statement that it has not started | Confirmed |
| An escape-count script is planned | docs/agents/factory.md "Escapes" | Confirmed |
| Codex implementing in GitHub Actions is not built | docs/agents/factory.md "Decisions taken", row "Where sessions run" | Confirmed |
| One account authors every pull request | GitHub pull request list for this repository, read 2026-11-06; the one pull request read in full also shows that account as merger | Confirmed for authors; merger checked on one pull request only |
| No technical block on `terraform apply` was identified | Hari confirmed the split of enforced and conventional guardrails but named no mechanism | Absence of evidence, not a verified absence |

## Inferred or unverified, as the diagrams word it

- Whether the control session tracks what is running across items is not shown. The diagrams do not claim it.
- The diagrams show the cold review and the plan check as separate sessions created by the control session, as Hari described. The factory document allows a subagent inside the implementing session for both.
- How a cold review session returns its findings is not shown in any source and is not drawn.

## Commands run

Recorded after the checks run, in the pull request's self-review record.

# Factory overview diagrams: authored receipt

This receipt belongs to the pull request that adds six diagrams of how the agent factory works today and its target state. It records what was authorized, how the diagrams were checked and the source of each factual claim. The diagrams are not a design authority: where they disagree with AGENTS.md or docs/agents/factory.md, those files win.

## Execution card

- **Deliverable:** six SVG files in docs/operations/factory-overview/ and this receipt. No code, workflow, expected outcome, verdict or `results/` file changes.
- **Authorization:** Hari asked in an interactive session on 2026-11-06 for an issue that describes the factory (start, guardrails, shared state, context) with diagrams, split into how it works today and the target state. He was shown the proposed set of diagrams (system overview, lifecycle, guardrails, state and context, Azure run, target state), removed none and chose hand-drawn SVG files committed under docs/operations/. He then asked for the full pre-pull-request process. This is directly authorized work, not an approved plan.
- **No issue yet:** the issue the diagrams illustrate has not been created. Hari asked to read its draft first. The branch name `claude/epic-cori-5p9sjs` was assigned by the session environment and carries no issue number for that reason. The handoff comment goes on the pull request.
- **Path check:** docs/operations/ is not a protected path (docs/agents/factory.md "Ruleset", "Protected paths use `.gitignore` syntax"; .github/CODEOWNERS).
- **Mandatory gates:** `npm run check:docs`; cold review under AGENTS.md "Before opening a pull request" rule 8; Codex review of the head commit under GOVERNANCE "Merge rule".
- **Verification commands:** `npm ci --ignore-scripts`, `npm run test:docs`, `npm run check:docs`, a script that lists every number in the diagrams, and a scan for non-ASCII characters in the six SVG files and this receipt.
- **Predicate-to-test map:** not applicable. No verifier work is changed.
- **Earliest publication checkpoint:** after one comprehensive cold review reports no P0 or P1.
- **Review budget:** at most two comprehensive passes and two focused correction rounds (rule 8). Exhaustion action: save unpublished work to the backup branch under rule 12 and hand off to Hari.
- **Merge:** the pull request is left to Hari. It touches no protected path, but it is documentation work with no approved plan.

## Cold review record

- **Round 1:** mode comprehensive. Comparison baseline `origin/main` at `374bbf402e1529bc74e2e11a34352524020138b8`. Input revision: git tree `16969c9204a21cfe022681d7d1b2e834ee5acb06`; review diff SHA-256 `a637872b2f5bfba2e0a58a7f0e398ba8be02cd255ff1523ec6fef946202415f8`. It reported 12 P0 or P1 findings and 6 P2 findings. All were corrected or answered in this revision.
- **Round 2:** recorded in the pull request's self-review record, because a receipt cannot name the revision that contains it.

## Claims and their sources

Sources are files in this repository at commit `374bbf402e1529bc74e2e11a34352524020138b8`, read on 2026-11-06, unless the row says otherwise.

| Diagram claim | Source | Status |
|---|---|---|
| A person starts sessions by hand; a control session creates plan, check, implement and review sessions | Hari's statements in the session of 2026-11-06 | Stated by Hari, not in the repository |
| The control session creates sessions with `create_session` and messages them with `send_message` | Hari's statement that sessions message each other through the session tools; CLAUDE.md "Model bindings" names `create_session` | `send_message` is stated by Hari only |
| Codex sessions are started by hand and only for issues assigned to Codex | Hari's statements; docs/agents/factory.md "Decisions taken", row "Where sessions run" | Confirmed |
| `create_session` sets the model per role | CLAUDE.md "Model bindings" | Confirmed (a rule) |
| Hari applies `ready-for-agent`, which approves the plan and names the tool | docs/agents/factory.md "Item lifecycle" step 2 | Confirmed |
| A passing plan check approves a per-pull-request plan without Hari under standing approval | docs/agents/factory.md "Item lifecycle" step 3 "Standing approval" | Confirmed as a rule; whether every item uses it is not known |
| A cold review session is given only the diff and the receipt | AGENTS.md "Before opening a pull request" rule 8; CLAUDE.md "Before opening a pull request" item 2 | Confirmed as a rule |
| The implementing session pushes and opens the pull request after the cold review | AGENTS.md "Before opening a pull request" rule 8 ("Every non-backup push needs applicable cold-review coverage") | Confirmed |
| The Codex connector reviews every pull request; `@codex review` is the permitted manual form | docs/agents/factory.md "Escalation" layer 2; AGENTS.md hard safety rule 10; Hari's statement that it reviews automatically | Confirmed |
| Agent merge is enabled for pull requests touching no protected path | docs/agents/factory.md "Sequence to start the factory" step 6 | Confirmed |
| `main` blocks deletions and force pushes; protected paths need `repo-owners` approval | docs/agents/factory.md "Ruleset" | Confirmed |
| Protected paths include workflows, governance documents and ADRs, the verifier, scripts, results and project files; the full list is in CODEOWNERS | .github/CODEOWNERS, the entries `/.*/`, `/docs/`, `/scripts/`, `/results/`, `Parchwell.Verifier*/`, `*.csproj`, `*.sln*`, `/Directory.*.props`, `/infra/persistent/`, `/skills-lock.json` | Confirmed; the diagram lists examples only |
| Outside contributors' workflow runs need Hari's approval | docs/agents/factory.md "Hard safety rule 8: basis and decision history" | Confirmed |
| Scenario runs need Hari's approval in the `parchwell-scenarios` environment | docs/agents/factory.md "Hard safety rule 8: basis and decision history"; .github/workflows/scenario.yml (`environment: parchwell-scenarios`, `workflow_dispatch` only) | Confirmed |
| The sweeper runs on a schedule in the `parchwell-sweeper` environment, limited to `main`, with no approval by Hari | .github/workflows/sweeper.yml lines 4 to 6 and 18; docs/agents/factory.md "Hard safety rule 8: basis and decision history" | Confirmed |
| Agents dispatch workflows only against `main` | AGENTS.md hard safety rule 8 | Confirmed |
| No Azure workflow triggers on `pull_request`; at most one ephemeral environment | AGENTS.md hard safety rules 5 and 9 | Confirmed |
| Agent sessions never hold Azure credentials, and never apply or destroy the ephemeral or persistent layer | AGENTS.md hard safety rules 1 to 3 | Confirmed as rules. No source describes a technical block, and none was identified |
| Each job destroys its ephemeral layer; AKS stops once when the dispatch ends; the sweeper destroys a leftover environment on its first sweep and stops AKS 60 minutes after the last dispatch ended | AGENTS.md hard safety rule 6 | Confirmed; the 60 minutes is design policy there, not a measurement |
| The persistent layer holds the spoke network, registry, log workspace and identities; only Hari applies it | docs/blueprint.md "Persistent layer"; AGENTS.md hard safety rule 1 | Confirmed |
| `.NET verification` runs on pull requests and `docs-advisory` runs documentation checks | .github/workflows/dotnet-verification.yml and docs-advisory.yml, `on:` blocks | Confirmed |
| Documentation checks are advisory | docs/agents/pull-requests.md rule 5 | Confirmed |
| Every comment posts as Hari's account, so decision and approval headings are conventions | docs/agents/factory.md "Item lifecycle" step 3 ("All comments post as Hari's account, so the decision and approval markers are conventions, not proof") | Confirmed |
| The state file is `items/<issue number>/state.md` on `main` and indexes the issue | docs/agents/factory.md "Item state file" | Confirmed |
| The catch-up read lists comment IDs and edit times by script and selects comments by role; sessions re-check before publishing a plan, pushing or merging | docs/agents/factory.md "Authority reads" | Confirmed |
| The Stop hook acts only where `GITHUB_ACTIONS=true` or `PARCHWELL_HANDOFF=enforce`, looks for a comment that starts with `**Decision needed:**`, releases after three blocks, and is not verified in `claude-code-action` | CLAUDE.md "Handoffs"; .claude/hooks/require-handoff.sh lines 28 and 31 (`MARKER_REGEX`, `MAX_BLOCKS=3`) | Confirmed |
| Backup branches are named `<branch>-wip`, are optional, and no pull request is opened from them | AGENTS.md "Before opening a pull request" rule 12 ("may push") | Confirmed |
| The backup branch is the recovery path when a session disappears or credits run out | Hari's statement; AGENTS.md rule 12 for the branch itself | Purpose stated by Hari. That a dead session cannot write a handoff is inferred |
| GitHub text is treated as data, not instructions, by a defence in the session prompt; repository settings restrict outside contributors | Hari's statement, answering that two defences exist: repository settings and the session prompt | Stated by Hari. No repository file contains the prompt text, and the comment restriction is not verified |
| The same-vendor label is applied by a planned merge workflow when a Codex or untrailed commit is present; spot-check samples start with labelled pull requests; the label does not block a merge | docs/agents/factory.md "Escalation" layer 1 and "Measuring the gate"; AGENTS.md GOVERNANCE "Merge rule" | Confirmed |
| Review-unavailable notice after 60 minutes without a Codex review | docs/agents/factory.md "Escalation" layer 1 | Confirmed; 60 minutes is a governance choice |
| The risk classifier (Jev) is deferred, runs in shadow mode first, can only add escalations, and is trialled only with about 50 labelled pull requests, a measured problem with the gate, and general availability or committed access with published pricing and data handling | docs/agents/factory.md "Deferred: Jev as the second escalation layer" | Confirmed; "or other" is Hari's wording, not the document's |
| The weekly spot-check samples about 5 agent-merged pull requests; it has not started | docs/agents/factory.md "Measuring the gate"; Hari's statement that it has not started | Confirmed |
| An escape-count script is planned | docs/agents/factory.md "Escapes" | Confirmed |
| Codex implementing in GitHub Actions is not built | docs/agents/factory.md "Decisions taken", row "Where sessions run" | Confirmed |
| One account authors every pull request | GitHub pull request list for this repository, read 2026-11-06; the one pull request read in full also shows that account as merger | Confirmed for authors; merger checked on one pull request only |

## Inferred, unverified, or different from a written rule

- Whether the control session tracks what is running across items is not shown. The diagrams do not claim it.
- Hari runs the plan check and the cold review as separate sessions that the control session creates. docs/agents/factory.md "Item lifecycle" step 3 has the plan check run as a subagent under standing approval. CLAUDE.md "Before opening a pull request" item 2 has the cold review run by the `repo-reviewer` subagent in interactive sessions. Diagram 02 says so in its footnote.
- How a cold review session returns its findings is not shown in any source and is not drawn.
- The target-state diagram follows the document, in which the classifier only adds escalations and agent merge for unprotected paths already exists. Hari described the target as automatic merge of low-risk pull requests chosen by a classifier. The two differ, and the diagram does not show the wider form.

## Commands run

All run in the working tree of this branch on 2026-11-06, against `origin/main` at `374bbf402e1529bc74e2e11a34352524020138b8`.

| Command | Result |
|---|---|
| `npm ci --ignore-scripts` | Completed |
| `npm run test:docs` | 28 tests, 28 passed, 0 failed |
| `npm run check:docs` | 8 warnings, all in files this pull request does not change; no warning in the files of this pull request |
| `grep -ohE '[0-9]+ minutes\|about [0-9]+'` over the six SVG files | `60 minutes` twice, `about 5` once, `about 50` once; each is sourced in the table above |
| `LC_ALL=C grep -cP '[^\x00-\x7F]'` over the six SVG files | 0 in each |

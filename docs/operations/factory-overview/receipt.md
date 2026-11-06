# Factory overview diagrams: authored receipt

This receipt belongs to the pull request that adds six diagrams of how the agent factory works today and its target state. It records what was authorized, how the diagrams were checked and the source of each factual claim. The diagrams are not a design authority: where they disagree with AGENTS.md or docs/agents/factory.md, those files win.

## Execution card

- **Deliverable:** six SVG files, the script that generates them, and this receipt, all in docs/operations/factory-overview/. No workflow, expected outcome, verdict or `results/` file changes.
- **Program output:** the SVG files are produced by `generate-diagrams.py` from hand-written coordinates. The script is committed so that the review diff holds all authored work (AGENTS.md "Before opening a pull request" rule 8). Rule 8 also says agents commit no program output. Whether these SVG files count as program output is Hari's decision. He replied "Yes" in chat to a message that recommended accepting them, because the issue needs the files on `main` to embed them. He later wrote "Accept the SVG files and proceed". Neither reply is a `## Decision` comment on the issue, so no authority read would find them. The finding stays open: the pull request description lists it as escalated, and the merge is left to Hari, whose merge records the decision.
- **Authorization:** Hari asked in an interactive session on 2026-11-06 for an issue that describes the factory (start, guardrails, shared state, context) with diagrams, split into how it works today and the target state. He was shown the proposed set of diagrams (system overview, lifecycle, guardrails, state and context, Azure run, target state), removed none and chose SVG files committed under docs/operations/, which the author described to him as hand-drawn. He then asked for the full pre-pull-request process. This is directly authorized work, not an approved plan.
- **Issue:** [issue 394](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/394), created on 2026-11-06 from the draft after Hari replied "Yes" to a message that asked him to approve creating it. The pull request is part of that issue and does not close it. The work started on the session-assigned branch `claude/epic-cori-5p9sjs`. With Hari's approval it moved to `claude/394-factory-overview-diagrams`, which carries the issue number. The earlier backup commits were pushed to `-wip` branches, which AGENTS.md "Before opening a pull request" rule 12 has the session delete once its own branch is pushed.
- **Path check:** docs/operations/ is not a protected path (docs/agents/factory.md "Ruleset", "Protected paths use `.gitignore` syntax"; .github/CODEOWNERS).
- **Mandatory gates:** `npm run check:docs`; cold review under AGENTS.md "Before opening a pull request" rule 8; Codex review of the head commit under GOVERNANCE "Merge rule".
- **Verification commands:** `python3 docs/operations/factory-overview/generate-diagrams.py` followed by a check that no file changes, `npm ci --ignore-scripts`, `npm run test:docs`, `npm run check:docs`, a script that lists every number in the diagrams, and a scan for non-ASCII characters in every file of this pull request.
- **Predicate-to-test map:** not applicable. No verifier work is changed.
- **Earliest publication checkpoint:** after one comprehensive cold review reports no P0 or P1.
- **Review budget:** at most two comprehensive passes and two focused correction rounds (rule 8). Exhaustion action: save unpublished work to the backup branch under rule 12 and hand off to Hari. Hari authorized one further review round beyond this budget on 2026-11-06 ("I authorise the review"), to cover the diagram 06 change that he asked for; no other round is authorized.
- **Merge:** the pull request is left to Hari. It touches no protected path, but it is documentation work with no approved plan.

## Cold review record

Complete reports are in the pull request's self-review record. The dispositions are here, so that the review diff holds them.

- **Round 1:** mode comprehensive. Comparison baseline `origin/main` at `374bbf402e1529bc74e2e11a34352524020138b8`. Input revision: git tree `16969c9204a21cfe022681d7d1b2e834ee5acb06`; review diff SHA-256 `a637872b2f5bfba2e0a58a7f0e398ba8be02cd255ff1523ec6fef946202415f8`. Result: 12 P0 or P1 and 6 P2.
- **Round 2:** mode comprehensive, the second of two allowed. Same baseline. Input revision: git tree `95971f3133dacb492897fadc0058eec6742815cf`; review diff SHA-256 `7d87a0a2252da6ce941c87e1723a9b21bf99877c6f7e6cb8bb49093af035af97`. Result: 5 P0 or P1 and 6 P2.
- **Round 3:** mode focused, the first of two allowed. Comparison revision: git tree `95971f3133dacb492897fadc0058eec6742815cf` (the round 2 input). Input revision: the tree that contains this receipt, which a receipt cannot name; the pull request's self-review record gives its hash. Scope: every change since the comparison revision, the unresolved findings and their surroundings, with the full pull request diff as context. Counts after this round: 2 comprehensive, 1 focused.
- **Round 3 result:** 1 P0 or P1 and 4 P2. The P1 is the program-output finding, open and escalated to Hari (see the round 2 table). The four P2 findings are fixed: backup-branch wording in diagram 04, the same-vendor label exception in diagram 06 and this receipt, the "interactive sessions" condition in diagram 02, and the stale backup-branch note above.
- **Round 4:** mode focused, the second and last allowed. Comparison revision: git tree `be6c225e41e7c5e9609834ab5b670c4521edd863` (the round 3 input). Input revision: the tree that contains this receipt; the pull request's self-review record gives its hash. Counts after this round: 2 comprehensive, 2 focused. Result: 1 P0 or P1 (program output, still open and escalated to Hari) and 1 P2: the claims table listed a spot-check sampling claim that no diagram draws. The P2 is fixed in the round 5 changes.
- **Round 5:** authorized by Hari beyond the budget (see the execution card). Mode focused. Comparison revision: git tree `2b7de3f71c0fdf84850c9e2ee04742b101e7fba3` (the round 4 input; the pull request head differs from it only by a newer base, and `git diff origin/main HEAD` is byte-identical to the round 4 review diff). Input revision: the tree that contains this receipt; the pull request's self-review record gives its hash. Scope: the diagram 06 rewrite, the generator change that supports it, and these receipt changes, with the full pull request diff as context. Counts after this round: 2 comprehensive, 3 focused.
- **Later rounds:** none authorized; any further round needs Hari's explicit decision.

Round 1 dispositions (no `Not valid:` answers):

| Finding | Disposition |
|---|---|
| "Hari approves every Azure run" false for the sweeper | Fixed: diagrams 01, 03 and 05 say "scenario run" and describe the sweeper gate |
| Stop hook released after three blocks; marker is the bold form | Fixed in diagrams 03 and 04 |
| "Only the left column holds" overstated | Fixed: new subtitle; CI shown as advisory |
| Diagram 05 and 03 contradicted on credentials | Fixed: diagram 05 subtitle |
| "The classifier would widen this" contradicted the source | Removed |
| Same-vendor check routed to escalation | Fixed in round 2: moved out of the merge routing |
| Diagram 02 pushed before the cold review | Fixed: steps reordered |
| Five claims had no source row | Fixed: rows added below |
| Backup branch wording contradicted the catch-up rule | Fixed; wording corrected again after round 2 |
| No predicate-to-test map | Fixed: "not applicable" recorded |
| Commands not in the receipt | Fixed: "Commands run" table |
| No issue number | Fixed in round 2 follow-up: issue 394 created |
| P2: protected-path list read as complete | Fixed: examples, with CODEOWNERS named |
| P2: "never run terraform" went beyond the rule | Fixed: "never apply or destroy" |
| P2: Jev conditions incomplete | Fixed in round 2 |
| P2: receipt named the wrong source for the review subagent | Fixed |
| P2: input revision not checkable | Fixed: tree and diff hashes recorded |
| P2: diagrams 05 and 06 not in the brief | Fixed: authorization records that Hari was shown the set |

Round 2 dispositions (no `Not valid:` answers):

| Finding | Disposition |
|---|---|
| SVG files are program output of an uncommitted script; receipt said hand-drawn | Fixed: script committed, reproducibility checked, receipt corrected. Open, escalated to Hari: AGENTS.md rule 8 has no carve-out for program output. Hari said "Yes" in chat; the merge is left to him |
| Footnote said step 4 follows standing approval | Fixed: footnote and source row now say it does not |
| Jev conditions misstated | Fixed: all three conditions, with "general availability, or committed access" |
| Round 2 had no mode or revisions in the receipt | Fixed: this section |
| No issue and no issue number | Fixed: issue 394 created, linked above, and the branch renamed to carry it |
| P2: cold review shown after the pull request opens in diagram 06 | Fixed: cold review precedes the pull request; "Codex review" box |
| P2: classifier presented as settled | Fixed: "on trial" in title and box |
| P2: same-vendor label description incomplete | Fixed: Codex trailer or no tool trailer |
| P2: backup branch drawn as optional and the only trace | Fixed |
| P2: unlabelled arrow from implement to review contradicted diagram 02 | Removed from diagram 01 |
| P2: no source for the orchestration workflow rows | Fixed: rows added below |

## Claims and their sources

Sources are files in this repository at commit `374bbf402e1529bc74e2e11a34352524020138b8`, read on 2026-11-06, unless the row says otherwise.

| Diagram claim | Source | Status |
|---|---|---|
| A person starts sessions by hand; a control session creates plan, check, implement and review sessions | Hari's statements in the session of 2026-11-06 | Stated by Hari, not in the repository |
| The control session creates sessions with `create_session` and messages them with `send_message` | Hari's statement that sessions message each other through the session tools; CLAUDE.md "Model bindings" names `create_session` | `send_message` is stated by Hari only |
| Codex sessions are started by hand and only for issues assigned to Codex | Hari's statements; docs/agents/factory.md "Decisions taken", row "Where sessions run" | Confirmed |
| `create_session` sets the model per role | CLAUDE.md "Model bindings" | Confirmed (a rule) |
| Hari applies `ready-for-agent`, which approves the plan and names the tool | docs/agents/factory.md "Item lifecycle" step 2 | Confirmed |
| Standing approval lets a passing plan check approve a plan without Hari only when the `implement-plan` session itself spawns the planner and checker; a plan approved otherwise needs Hari's `## Approval` comment | docs/agents/factory.md "Item lifecycle" step 3 "Standing approval" | Confirmed. Diagram 02 draws separate sessions, so its documented approval is Hari's comment; which one applies per item is not known |
| A GitHub Actions workflow that starts the sessions; Codex implementing in Actions | Hari's statement that this is target state; docs/agents/factory.md "Decisions taken" row "Where sessions run" and "Sequence to start the factory" step 4 (an implementation workflow is to be added) | Confirmed as planned. "Replaces the control session" is inferred, because no document describes the control session |
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
| The same-vendor label is applied by a planned merge workflow when a remaining commit carries a Codex trailer or no tool trailer, ignoring merge commits and untrailed commits authored by Hari's account; the label does not block a merge | docs/agents/factory.md "Escalation" layer 1 and "Measuring the gate"; AGENTS.md GOVERNANCE "Merge rule" | Confirmed |
| Review-unavailable notice after 60 minutes without a Codex review | docs/agents/factory.md "Escalation" layer 1 | Confirmed; 60 minutes is a governance choice |
| The risk classifier (Jev) is deferred, runs in shadow mode first, can only add escalations, and is trialled only with about 50 labelled pull requests, a measured problem with the gate, and general availability or committed access with published pricing and data handling | docs/agents/factory.md "Deferred: Jev as the second escalation layer" | Confirmed; "or other" is Hari's wording, not the document's |
| A low classifier score lets a pull request merge automatically: the target, drawn highlighted in diagram 06 | Hari's statement on 2026-11-06 ("Automatic merge is target state provided by the classifier") | Stated by Hari only. It differs from docs/agents/factory.md "Deferred: Jev", where an adopted classifier only adds escalations; diagram 06 states the difference in its own box |
| The weekly spot-check samples about 5 agent-merged pull requests; it has not started | docs/agents/factory.md "Measuring the gate"; Hari's statement that it has not started | Confirmed |
| An escape-count script is planned | docs/agents/factory.md "Escapes" | Confirmed |
| Codex implementing in GitHub Actions is not built | docs/agents/factory.md "Decisions taken", row "Where sessions run" | Confirmed |
| One account authors every pull request | GitHub pull request list for this repository, read 2026-11-06; the one pull request read in full also shows that account as merger | Confirmed for authors; merger checked on one pull request only |

## Inferred, unverified, or different from a written rule

- Whether the control session tracks what is running across items is not shown. The diagrams do not claim it.
- Hari runs the plan check and the cold review as separate sessions that the control session creates. docs/agents/factory.md "Item lifecycle" step 3 has the plan check run as a subagent under standing approval. CLAUDE.md "Before opening a pull request" item 2 has the cold review run by the `repo-reviewer` subagent in interactive sessions. Diagram 02 says both in its footnote.
- How a cold review session returns its findings is not shown in any source and is not drawn.
- Diagram 06 keeps the Codex review beside the classifier, as the merge rule in AGENTS.md requires. docs/agents/factory.md "Deferred: Jev" says an adopted classifier would replace Codex in the second escalation layer. The two read differently, and the diagram follows the merge rule.
- Diagram 02 draws no step where Hari posts a per-pull-request `## Approval`; the footnote states the documented approval for separate sessions.
- Diagram 06 shows Hari's target, automatic merge on a low classifier score, highlighted as the target, and states in its own box that docs/agents/factory.md does not say this. Reconciling the two is a governance change for Hari, and is an open question in issue 394.

## Commands run

All run in the working tree of this branch on 2026-11-06, against `origin/main` at `374bbf402e1529bc74e2e11a34352524020138b8`.

| Command | Result |
|---|---|
| `python3 docs/operations/factory-overview/generate-diagrams.py`, run twice, then a recursive diff of the folder | The second run changed no file |
| `npm ci --ignore-scripts` | Completed |
| `npm run test:docs` | 28 tests, 28 passed, 0 failed |
| `npm run check:docs` | 8 warnings, all in files this pull request does not change; no warning in the files of this pull request |
| `grep -ohE '[0-9]+ minutes\|about [0-9]+'` over the six SVG files | `60 minutes` twice, `about 5` once, `about 50` once; each is sourced in the table above |
| `LC_ALL=C grep -cP '[^\x00-\x7F]'` over the six SVG files | 0 in each |

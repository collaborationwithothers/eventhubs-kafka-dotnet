# Factory operating model

How agents build this repository, and where Hari's judgment stays.

Status: approved by Hari on 2026-10-12. Amended for ADR-018 in the pull request for issue #40 (2026-10-13): the "Decisions taken" date range and its "Tools" and "Where sessions run" rows, "Item lifecycle" step 2, "Escalation" layers 1 and 2, "Measuring the gate", the Copilot paragraph under "Ruleset", and "Sequence to start the factory" step 4. Hari's merge of that pull request is the approval. Amended in the pull request for issue #72 (2026-10-16): "Item lifecycle" step 1 and the plan template's "Enforced checks" field; amended again in the follow-up pull request for issue #72 (2026-10-17): "Item lifecycle" step 1 and the plan template's "Hypothesis" and "Decision rules" fields. Amended in the pull request for issue #85 (2026-10-18): "Item lifecycle" steps 1 and 3, "Modes of work" and "Escapes". Amended in the pull request for issue #104 (2026-10-21): "Item lifecycle" steps 1 and 3, standing approval of implementation-grade plans. Amended in the verdict pull request for issue #215 (2026-10-26): "Item lifecycle" step 3, reading a posted plan back. Amended in the pull request for issue #264 (2026-10-31): "Authority reads", the new "Item state file", and "Item lifecycle" steps 1 and 3.

The #211 amendment to "Evidence pack" follows [the adopted S0 decisions](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/211#issuecomment-6423734358). Hari's merge of its pull request records approval of this amendment.

The #224 amendment to "Evidence pack", for traced items, follows [#224 decision 5](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6438353573). Hari's merge of its pull request records approval of this amendment.

The factory exists to minimise Hari's hours per inventory item, not to minimise calendar time. Generating code is cheap; verifying it is the constraint. So Hari's attention goes into two decisions per item, and everything between them is checked by machines that are hard to fool.

## Decisions taken (2026-10-11 to 2026-10-13)

| Decision | Choice |
|---|---|
| Goal | Fewest of Hari's hours per inventory item |
| Hari's touchpoints per item | Approve the plan; approve the verdict from the evidence pack |
| Live Azure runs | Started by agents through the repository's workflows; protected environment approval remains required |
| Merging | Agents merge their own pull requests under the merge rule in AGENTS.md |
| Escalation | Deterministic rules decide; the reviewer can add an escalation, never remove one |
| Tools | Either Claude Code or Codex implements, chosen per issue; Codex reviews every pull request, including its own. Both run on Hari's plans (Claude Max, ChatGPT Pro), not on metered API keys. Decided 2026-10-13, ADR-018 |
| Where sessions run | GitHub Actions, unattended, after one reference slice built interactively with Hari. Claude Code only, until a Codex path in Actions exists; Codex implements in sessions Hari starts himself (ADR-018) |
| Concurrency | 4 implementation sessions, 1 ephemeral environment; [#138 A15](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6383206899), under its [adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6383731545) |
| Architecture | Vertical slices per service; `Parchwell.Kafka` shared library; scenarios as data |

## Modes of work

Following Osmani's distinction between loops a human reviews and loops nobody reads:

| Work | Mode | Why |
|---|---|---|
| Item plan | Human-approved | Cheapest place for Hari's judgment: a page, not a diff |
| Harness code, Terraform for infra/cluster and the ephemeral layer, KQL, scenario definitions | Unattended, merged by agents | Checked by build, analyzers, architecture tests, offline checks and live smoke gates, Codex review |
| Verifier logic | Human-reviewed (protected path) | It is the oracle for every result; its judgement logic is also covered by mutation testing (docs/engineering/dotnet.md "Testing") |
| Expected outcomes, verdicts, `results/` | Human-only approval | They are the public claims |
| Persistent infrastructure, identity, network, workflows, governance, ADRs | Human-reviewed (protected path) | Large blast radius, long-lived decisions |

Protected paths are those in the ruleset and CODEOWNERS; infra/cluster and infra/scripts are unprotected. Basis: [#138 A3/A4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6383206899), under its [adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6383731545).

## Authority reads

Each step reads a defined list, not the whole issue thread. Authority, its recognition and its order stay as "Item lifecycle" step 3 defines them. The item state file ("Item state file" below) is an index to that authority, not a source of it. Use AGENTS.md "GitHub issues and pull requests" for REST reads and pagination. Basis: [#264 first governance change](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6544062669), part b.

| Step | Reads |
|---|---|
| Planning (`plan-pr`) | The item state file and its catch-up, the approved item plan ("Item lifecycle" steps 1 and 2) or, for a child issue, the parent plan its body or approval links, the docs/blueprint.md sections the plan depends on, and on `main` every file the plan will change. For a revision, also the current plan comment and its newest `## Plan check`. |
| Plan check (`plan-check`) | The selected plan comment, the item state file and its catch-up, the approved item plan or parent plan as in the planning row, every file the plan changes and every source the plan cites. Not the rest of the thread and not the author's handoffs. |
| Implementation (`implement-plan`) | The approved plan comment, the `## Approval` comment that approves it if any, the newest `## Plan check` that identifies that plan revision, whichever way it was approved, for its `apply in implementation` findings ("Item lifecycle" step 1), each comment on the issue, or for a child issue on its listed parent items, that names the implementing tool under "Item lifecycle" step 2, found by script, the time `ready-for-agent` was applied, from the events of the issue or, for a child issue, of its listed parent items, the item state file and its catch-up, and every file the plan changes. |
| Cold review | Unchanged: the review diff and the authored receipt (AGENTS.md "Before opening a pull request" rule 8). |

Every step also reads the AGENTS.md and factory.md sections its skill cites. Where its work depends on the exact text of a decision the state file lists, it reads that decision in full.

**Catch-up.** List the issue body's `updated_at` and every comment's ID, `created_at` and `updated_at`, across every page. A comment is new when it was created after the comment the state file covers, or edited after the file's read time. With no state file, every comment is new. Filter comment bodies by script, so only the bodies this paragraph selects enter the session's context. A script may also extract, without the body entering context, the heading, the linked plan comment, the revision label and the recorded `updated_at` of each plan and `## Plan check` comment, as candidate selection in "Item lifecycle" step 3 needs. Every session reads:
- the issue body, if there is no state file or the body was edited after the file's read time;
- the full body of each new comment whose heading starts "## Decision" or "## Approval", and every decision such a comment links and adopts;
- the comments its row in the table above names.

Planning and implementation sessions also read the full body of every other new comment when a state file exists. With no state file they read only the newest comment that is not a decision, approval, plan or plan check, because a handoff to the next session carries the continuation (AGENTS.md GOVERNANCE "Handoffs"). A plan check reads no other comment body, with one exception for every session: it reads the full body of each comment that the state file links, or that this session already read as authority, when that comment was edited after the last read, whatever its heading now. If such a comment no longer qualifies as a decision or approval under "Item lifecycle" step 3, or no longer exists, treat its line as void and recompute authority and candidate selection. This catch-up is the initial read of a fresh session. Save the issue body's `updated_at` and the full set of comment IDs and `updated_at` values, together with the text read.

Before publishing a plan or plan check, and before a push, pull request creation or merge, fetch the issue metadata and the full comment ID and `updated_at` set again, across every page. Compare them with the last read. Check the metadata of the linked decision comments already read as well. Apply the catch-up rule above to every added or edited comment and to new or edited linked decisions. Read the issue body again if its `updated_at` changed since the last read. A missing comment ID counts as a change of authority: remove that comment from the saved text and recompute authority and candidate selection. Recompute candidate selection and affected work for every addition, edit or deletion before proceeding, then save the new snapshot. Before a push this implements #85, decision of 2026-10-20, decision 10 (https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6284948629).

For a child issue of a split, its state file lists the parent items the child's body names. Read the bodies of those parent items, their decision and approval comments and, for implementation, their tool-naming comments and `ready-for-agent` label events, and include them in the saved read and change comparison, because "Item lifecycle" step 3 applies item approval and split checks using the listed parent items. A parent decision changes the child's plan only when a decision or approval on the child links and adopts it (step 3). A fresh session with no saved snapshot performs the catch-up above; an unavailable snapshot is not evidence that nothing changed.

## Item state file

Each issue in progress has at most one state file, at `items/<issue number>/state.md` on `main`. It first appears when the issue's first implementation pull request merges; before then, a missing file is the normal case, not an error. It tells a session the current state of the item without replaying the thread. The path is outside the protected paths. Basis: [#264 first governance change](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6544062669), part a. Hari chose the path and the single writer below on 2026-10-31, in the session that implemented that change; his merge of its pull request records approval.

The file holds these fields, in this order:

```
# State: #<issue number> <issue title>
Covers: comment <ID> created <created_at>; read at <UTC timestamp>
Parent items: <links, for a child issue; otherwise "none">
Item plan: <link to the approved item plan, or for a child issue the parent plan; or "none">
Implementing tool: <Claude Code or Codex>. Source: <link to the comment naming it, or "no comment on the issue or its listed parents before `ready-for-agent`; Claude Code by default">
Approved plan: <link to the plan comment and its revision label; or "none">
Decisions in force:
- <one line>. Source: <link>
Open questions:
- <one line>. Source: <link>; or "None"
Next action: <one line>
```

"Decisions in force" starts with the issue body, then lists each decision or approval recognized under "Item lifecycle" step 3, in creation order, that still applies. Each line states only what its source says and links that source. A line does not replace reading its source when work depends on the source's exact text.

The implementing session is the only writer. It creates or updates the file in its implementation pull request, after the catch-up of `implement-plan` step 1 and before the cold review, so the file goes through that pull request's cold review and Codex review. A later change to the file is an authored change under AGENTS.md "Before opening a pull request" rule 8. Planning and plan-check sessions do not write it; their comments reach the next session through the catch-up. After the issue closes, any later pull request may delete its file.

Accepted risk: the comment check catches a stale file, not a wrong one. A wrong line misleads every later session until someone checks it against its linked source.

## Item lifecycle

1. **Plan.** An agent drafts the plan as a comment on the item's issue, using the template below. Plan text follows AGENTS.md "Before opening a pull request" rules 1, 2 and 4. Before approval, a session other than the drafting one checks the plan against the template and its sources. For an implementation-grade plan under step 3 "Standing approval", a fresh-context subagent given only the issue number and the words "standing approval" counts as that other session. It reports any field that is empty or open-ended, such as a validator without a numbered "Enforced checks" list, as a comment on the issue before Hari applies `ready-for-agent` (#72). Every independent plan check, including a check of an implementation-grade plan under step 3, answers three questions in its comment: (a) which steps have an effort that scales with a number unknown at planning time, and the cap or sample size of each; (b) where the plan is stricter than its parent plan, and why; (c) which tests or checks depend on code, data or behaviour that is neither on `main` nor in the pull request, each checked against the real output of the code that produces it (#85 decision 2). A plan check reads every file the plan changes, on `main`, at each place the plan changes it. It checks that each change fits the text around it, for example that a term appended to an existing expression keeps that expression's grouping. Basis: [#264 first governance change](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6544062669), part c. A check comment is headed `## Plan check` and links the plan comment, names its revision label when present, and records its `updated_at` timestamp from GitHub. A check counts for selection only when those details identify the current plan revision. The `plan-check` skill runs this check (AGENTS.md "Skills"). Every check comment states `Blocking findings: none` on its own line when no finding requires a plan revision or a decision by Hari; otherwise it lists each such finding under `Blocking findings:`. An open question in the plan is a finding that requires a decision by Hari. A plan check may mark a P2 `apply in implementation` only when its fix stays inside the plan's own files and changes no enforced check. Each such finding identifies the file, section or member, and exact replacement text, code shape or test with literal expected values, so the implementer need not choose the fix. The check explains why both conditions hold. The marker applies only to the plan revision identified by that check. A qualifying marked P2 is not a blocking finding and does not require a plan revision. Other findings retain the check and approval treatment above. Basis: [#184 decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/184#issuecomment-6397456399), A1.
   A plan that changes live infrastructure is not approvable until a live smoke run of that design exists. A smoke-only pull request may precede plan approval for this purpose. This rule applies to plans approved after the #156 C0 pull request merges. For #138 itself, gate 1 is that smoke run: C1, C2 and CT may merge before it; C3 to C6 are planned only after it passes. Basis: [#138 A17](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6383206899), under its [adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6383731545).

2. **Approve.** Hari applies `ready-for-agent`. This is approval of the plan and the trigger for implementation. Before applying the label, he names the implementing tool, Claude Code or Codex, in a comment on the issue; no such comment at the time the label is applied means Claude Code, and a session of the other tool does not claim the issue (ADR-018).
3. **Implement.** Each pull request is planned and built in two sessions, run by the `plan-pr` and `implement-plan` skills (#85 decision 6; AGENTS.md "Skills"), or in one session under "Standing approval" below (#104).
   - Each pull request of a split has its own child issue. The child holds that pull request's plan, plan check, decisions and handoffs. Its body lists the parent items to read. A parent decision affecting an open child is posted on or linked from the child. Read and refresh authority under "Authority reads". Basis: #85, decision of 2026-10-20, decision 11 (https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6284948629). For a child, apply this step's item approval and split checks using the listed parent items; recognize a parent decision as changing the child's plan only when a decision or approval on the child links and adopts it, at that child comment's place in creation order.

   After Hari approves a split, Hari or a session he directs creates one child per pull request, with Parent items:, Split entry: and Predecessor PRs: in the body. The session recording a parent decision posts it on or links it from each affected open child.
   - A prompt names the skill and the issue number; the session derives everything else from the issue. Select current revisions before filtering by check or approval status. For each pull request, only its newest implementation-grade plan comment is current; earlier revisions cannot supply a candidate or substitute check or approval evidence. `plan-pr` selects the earliest eligible unmerged pull request in the split approved on the issue whose current implementation-grade plan is missing or not approved, or the whole issue under the same criterion when no split exists. For a child issue, dependency eligibility is determined only by the entries named in that child's `Predecessor PRs:`. It does not depend on an unrelated earlier split entry. It names a new pull request itself and retains that name when revising its plan. Unapproved or rejected plans remain eligible for revision. A revision updates the existing plan comment and changes its revision label; the check and approval requirements apply to the edited revision. Before planning a child against `main`, verify every entry in its `Predecessor PRs:` has merged. If any has not, hand off under AGENTS.md GOVERNANCE "Handoffs", naming those unmerged predecessors. `plan-check` selects the newest plan on the issue whose current revision has no check under step 1, whether an item plan under step 1 or an implementation-grade plan under step 3. Before `implement-plan` accepts a candidate, it verifies the item-level approval and implementing-tool assignment under step 2 and AGENTS.md "Implementer and reviewer"; if those requirements are not satisfied, it hands off under AGENTS.md GOVERNANCE "Handoffs". It then selects the newest approved implementation-grade plan whose pull request is not merged. If selection leaves no candidates or more than one, the skill hands off, except as "Standing approval" below provides, under AGENTS.md GOVERNANCE "Handoffs", naming the candidates (or stating that there are none); it does not guess.
   - Authority starts with the issue body. A comment changes that authority only when its heading starts "## Decision" or "## Approval" and it records Hari's decision. Apply those comments in creation order; a later one wins where they differ. Draft plans, plan-check recommendations, review findings and handoffs are proposals or evidence until a decision or approval explicitly adopts them. A decision posted elsewhere binds a skill only when a decision or approval comment on the issue links and adopts it; apply it at that issue comment's place in the order. Account identity alone does not make a comment a decision.
   - A planning session, or a planning subagent under "Standing approval" (next bullet), writes or revises an implementation-grade plan for one pull request, maintaining it as one comment on the issue. It edits no files, creates no branch, commits nothing and opens no pull request. A plan is implementation-grade when a lower-reasoning model can execute it without judgement. For every change it gives the file, the section or member, and the exact text, code shape or test, with literal expected values. Where it changes a rule, it states the rule once, in the file that owns it, and the other files cite it. It gives acceptance as exact commands and the evidence each must produce. After posting or editing the plan comment, the session reads the comment back from GitHub and compares it with the text it sent, by script. In this repository's cloud sessions on 2026-10-26, posted comments came back with each blob URL into this repository that carried a `#` anchor wrapped in runs of backticks, which broke the pinned text of a plan ([#215 plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/215#issuecomment-6421943805)). Anchored blob URLs into another repository, such as librdkafka's, came back whole in the same plan. The cause is not known. A plan that pins exact text containing an anchored blob URL into this repository carries each such URL as parts, with a script that joins them and checks the result, as [#215 plan Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/215#issuecomment-6421780147) does. It answers the three plan-check questions in step 1. It lists under "Open questions for Hari" everything its authorities leave open, and decides none of them.
   - Standing approval (#104). An `implement-plan` session verifies item-level approval and tool assignment under step 2 first; these are unchanged. If its selection then has no candidate because the pull request that `plan-pr` would select has no current plan, or has a current plan with no plan check, it does not hand off for that reason. A current plan whose newest check lists blocking findings is not revised this way; the session hands off. It spawns a subagent, given only the issue number and the words "standing approval", that runs `plan-pr` and returns instead of handing off; in Claude Code that subagent uses the plan-drafting model in CLAUDE.md "Model bindings", selected as CLAUDE.md "Subagents" describes. It then spawns a fresh-context subagent that runs `plan-check` under step 1, and selects again. Only a check from that subagent can approve a plan under this bullet. A current implementation-grade plan revision is approved without a `## Approval` comment when all of these hold: the newest `## Plan check` comment identifies that revision under step 1, was posted after its latest edit and came from the check subagent of this bullet; that check states `Blocking findings: none`; and the plan's "Open questions for Hari" states `None`. Any other outcome is a handoff under AGENTS.md GOVERNANCE "Handoffs". A plan revised after it is approved is an amendment and needs Hari's approval. Basis: Hari's decision of 2026-10-21 on #104 (https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/104).
   - After Hari approves the plan in a comment on the issue, or the plan is approved under "Standing approval", a session implements that plan, with the later decisions recognized above and the `apply in implementation` P2s permitted by step 1. Under Hari's approval it is a fresh session; under standing approval it is the `implement-plan` session that spawned the planning and check subagents. An approval is a comment on the issue whose heading starts "## Approval" and that links the plan comment it approves. All comments post as Hari's account, so the decision and approval markers are conventions, not proof. Before implementation, the session verifies that the approval, or for standing approval the plan check, identifies the current plan revision and was posted after that plan comment's latest edit. Amendments also need Hari's approval. It uses subagents and follows AGENTS.md in full, including "Before opening a pull request" rules 8, 11 and 13. Authority reads and refreshes follow "Authority reads". It writes the item state file under "Item state file". An open question that affects the pull request and has no answer blocks it, and the session hands off. It builds the pull request under "Escalation", requests Codex's review in the form AGENTS.md hard safety rule 10 allows, and merges or escalates under the merge rule. Its handoff or completion report links the pull request, the reviewed head commit and the validation evidence when each exists. If work stops before any of these artifacts exists, the handoff names what has not been produced and links the available candidate-selection or blocking-decision evidence instead.

   **#138 entry.** #138 may enter implementation on [failed run 40089161816](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40089161816) and the [#154 source diagnosis](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/154) without a file under results/A01/. Its C0 governance exception is limited to the already adopted #138 rule/design changes. Before implementation, verify that no current dispatch or ephemeral environment remains active. If cleanup cannot be verified, use only existing authorized cleanup/recovery and keep new infrastructure execution blocked. No #154 diagnostic sequence or terminal handoff is required. This does not waive gate 1, gate 2, the final A01 protocol, protected merges, or the freeze for unrelated governance work. Basis: the [entry decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6384675649) and [#85 record](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6384844065). For documentation/policy-only C0, [#156 decision 6384901513](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/156#issuecomment-6384901513) waives the pre-edit cleanup-evidence prerequisite; it neither verifies cleanup nor authorizes infrastructure execution. Other children retain their prerequisites.

4. **Run.** The agent starts the scenario workflow. Infrastructure lifecycle and cleanup follow AGENTS.md GOVERNANCE "Hard safety rules" and docs/blueprint.md ADR-020.
5. **Verdict.** The agent opens a pull request labelled `verdict` with the evidence pack and the proposed verdict. Hari approves or rejects it.

When an item closes, Hari records his own time in one line on its issue, for example `Hari time: 1.5 h`. No measurement script is added now: comment history does not expire, and factory-outcome counts join the escape script planned with #17. Basis: #85, decision of 2026-10-20, decision 12 (https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6284948629).

## Execution and review record

AGENTS.md "Before opening a pull request" owns the execution card and predicate-to-test preparation, frozen review inputs and complete reports, consolidated corrections, publication checkpoint, verification repetition and cost-complaint response. Its rules 5, 8, 11 and 13 own the review protocol, cumulative budget, stopping and handoff actions. Use those rules rather than treating a new session as a fresh review allowance.

The single authored receipt connects the selected plan and recognized later decisions from "Item lifecycle" step 3 to the implementation and review reports. For directly authorized governance work without an approved item plan, it records the actual authorization, task brief and issue. Keep the PR description short and link the receipt; the final-head CI and last cold-review record belong there under AGENTS.md "Before opening a pull request" rule 8.

For [the review-cost retrospective](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/247), adoption alone does not complete the issue. Record pilot evidence from the next implementation started after adoption: coverage prepared before review, complete reports before corrections, consolidated corrections, earliest authorized publication, cumulative comprehensive/focused and hosted review counts, and any surfaced budget conflicts. Close only after adoption and that pilot evidence; review limits are governance choices, with no claimed token savings.

## Plan template

```
Item: <ID> <title>
Hypothesis: <one or more labelled hypotheses: what the documentation claims, and what we expect to observe>
Decision rules: <for each hypothesis the item decides: the evidence, when observation ends, when that evidence counts as complete, and which outcome supports each verdict in "Evidence pack"; "recorded only" for a hypothesis with no verdict. The same rules are written into expected.yaml, the protected file that holds the hypotheses (ADR-014)>
Configurations: <settings varied, one line each>
Fault injected: <none, or which harness capability>
Measures: <metrics, verifier counts, KQL queries>
Repeats: <at least 5 per configuration>
Harness changes needed: <none, or the capability to add>
Enforced checks: <for a component that validates or rejects input, a numbered list of every check it enforces; "none" otherwise>
Expected outcome file: scenarios/<ID>/expected.yaml (approved with this plan)
```

## Evidence pack

Every verdict pull request contains:
- the scenario definition and expected outcome as approved;
- candidate-derived results per repeat in `results/<ID>/<utc-date>.json`;
- per-repeat run validity and completion status, reasons for any invalid or incomplete result, with links to the workflow's per-repeat pod logs and pod JSON for worker exit outcomes; for single-action probe items, link one pod log and one pod JSON per configuration Job, with each repeat's lines tagged by repeat number; retain the separate F04 seed Job's available pod evidence with its prerequisite evidence ([adopted S0 decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/211#issuecomment-6423734358));
- verifier counts for every complete, valid repeat: `ok`, `loss`, `phantom`, `reported_failure`, `duplicate` and `reorder` (ADR-012); for single-action probe items, record these counts as not applicable and leave them absent, never zero ([approved counts exception](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/210#issuecomment-6415081194)); invalid or incomplete repeats retain diagnostic records with these authoritative counts absent, never substituted with zeros;
- per-repeat broker connection counts for every Kafka client whose statistics the run captures (ADR-019 check 2), using the checked measurements defined in [verifier-contract.md](../operations/verifier-contract.md#statistics-and-configuration); unavailable measurements are not substituted with zeros;
- Confluent.Kafka and librdkafka versions, PU count, node size, region, run timestamps;
- the proposed verdict (confirmed, contradicted, undocumented, untestable) and a three-sentence summary.

For a traced item, typed D in docs/blueprint.md section 12 "Frozen inventory" or "Inventory amendment 1", nothing runs. Its verdict pull request contains the report `results/<ID>/<utc-date>.md`, dated the UTC day the report is written. The report links the item's write-up under `docs/traces/` as merged on `main`, whose sources are each pinned to a commit or tag. It proposes the verdict `untestable` with the item's section 12 reason, and opens with a three-sentence summary. The run evidence listed above is recorded as not applicable and left absent, never zero: the scenario definition and expected outcome, per-repeat results, validity, completion and pod evidence, verifier counts, broker counts, client versions, PU count, node size, region and run timestamps ([#224 decision 5](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6438353573)).

Mutation acceptance for a pull request that changes any path in the `mutate` list of `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` is a passing `verifier-c5-mutation.yml` run dispatched on `main` after the change merges. The run's `summary.json` must identify the commit actually tested, report `measurement_complete: true` and `break_passed: true`, and name a commit on `main` whose history includes the target-changing merge commit. A passing run on a later main commit is acceptable when it includes the target changes under review. If a later main change modifies any configured target, acceptance for that change requires a later passing run whose measured commit includes it. A missing, failed or incomplete latest applicable run blocks evidence-pack publication; if the run fails or is incomplete, open a fix issue. Do not use the deleted local sample as a mutation check.

Retention follows docs/blueprint.md ADR-020. The candidate carries checked results and diagnostics; links to Actions logs and public pod artifacts do not make those logs authoritative outcome counts.

## Escalation

Layer 1 decides; layer 2 can only add.

There is no numeric pull request size limit; AGENTS.md "Before opening a pull request" rule 6 and Codex review govern review-driven splitting. Basis: [#85 decision 6318070052](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6318070052) item 1.

1. **Deterministic rules.** The `main-protection` ruleset's required reviewers rule lists every protected path and requires one approval from the `repo-owners` team (Hari) when a pull request touches one. `.github/CODEOWNERS` mirrors the same list so GitHub requests Hari's review automatically, but it is not the gate (see "Ruleset" below). Rules that paths cannot express are checked by a required status check: new NuGet packages, and changes to assertions in tests. Two more deterministic rules belong to the merge workflow planned under issue #17 (ADR-018):
   - Implementer. The merge workflow, planned under issue #17 and added by Hari at step 4 of "Sequence to start the factory", will read the `Co-Authored-By` trailers on the pull request's commits, ignoring merge commits and untrailed commits authored by Hari's account, and label the pull request `same-vendor-review` when any remaining commit carries a Codex trailer or no tool trailer, re-checking on every push and removing the label when no remaining commit does (AGENTS.md "Implementer and reviewer" defines the trailers and the ignored commits). Agents never apply or remove that label. Neither the label nor the workflow exists yet.
   - Review unavailable. The same workflow will post `**Review unavailable:**` on the pull request when Codex has completed no review of the head commit within 60 minutes of the push; the pull request then waits for Hari. This covers a Codex usage-limit refusal (ADR-018, S35) and any other silence. The marker differs from the session handoff marker `**Decision needed:**` so that the workflow's comment never satisfies, or doubles, a session's Stop hook (CLAUDE.md "Handoffs"). 60 minutes is a governance choice, not a measurement; revisit it with the data recorded under issue #17. The check depends on Codex posting a completed review on a clean pull request, which the pilot verifies (third bullet below). Agent merge is enabled for unprotected paths under "Sequence to start the factory" step 6; a session whose pull request waits for a review that does not come hands off under GOVERNANCE "Handoffs". If the pilot finds that Codex posts only a reaction on a clean pull request, the merge workflow must obtain a completed review, for example by commenting `@codex review`, before the merge rule can pass; that is a design item under issue #17.
2. **Codex review.** Any unresolved P0 or P1 code review finding, or High security review finding, blocks agent merge. Codex reads docs/threat-model.md for security reviews once it exists; until then it generates its own threat model on every review. Codex reviews every pull request, whichever tool implemented it (ADR-018).

Verification still tracked by #17:
- that a pull request opened by the factory's GitHub App on a protected path is blocked until Hari approves (tested so far only with Hari as author);
- that a pull request touching only `docs/operations/` is not blocked (the negation pattern);
- whether Codex posts anything when it finds no issues, and whether its reviews can block a merge, so the merge step cannot race ahead of the review.

## Ruleset

`main-protection` on the default branch. Settings and the reason for each, as tested on 2026-10-12 with throwaway pull requests #1 and #2:

| Setting | Value | Reason |
|---|---|---|
| Bypass | Repository admin and organization admin, "for pull requests only" | Hari cannot approve his own pull requests, so his governance changes merge through bypass; nobody can push to `main` directly |
| Restrict deletions, block force pushes | On | |
| Required approvals | 0 | Agent pull requests on unprotected paths merge without a human |
| Required reviewers | Team `repo-owners`, 1 approval, 15 file patterns | The gate for protected paths. GitHub caps the list at 15 patterns per reviewer, so the list is full; a new protected path means merging patterns or adding a second team. `/.*/` covers every root dot-folder (`.github`, `.agents`, `.claude`); `/*.md` covers every root Markdown file, including README.md |
| Require review from Code Owners | On, but not relied on | Test: with 0 required approvals, a code-owned pull request was mergeable without review. Cause not isolated (zero approvals, or author was the only code owner) |
| Dismiss stale approvals on push | On | A push after Hari's approval clears it |
| Require approval of the most recent push | **Off** | Test: it required an approval from someone other than the last pusher on every pull request, so agents could never merge |
| Require conversation resolution | On | Review threads must be resolved. Not a hard gate: the bot can resolve threads |
| Additional approval for unattributed Copilot pull requests | On | Preview setting; affects only pull requests Copilot authors |

Protected paths use `.gitignore` syntax. `/docs/` is protected except `docs/operations/`, which agents own. Runbooks go in `docs/operations/`; any other document under `docs/` comes to Hari.

GitHub Copilot code review is not part of the factory. It reviewed the test pull requests although no ruleset requested it (most likely a personal setting) and was switched off on 2026-10-12, because its threads would block agent merges through conversation resolution. The second reason recorded on 2026-10-12, that it answers to none of the rules in AGENTS.md, was wrong: Copilot code review has read AGENTS.md since 2026-06-18 (ADR-018, S38). It stays off because of the conversation-resolution problem and because of cost: reviews of bot-authored pull requests "bill the usage directly to your organization" (S40). Inferred, not stated by S40 or S44: organization billing needs AI-credit paid usage enabled, as S44 requires for its Organization billing option. Either way it is spend beyond the one seat Hari already pays for (ADR-018).

## Measuring the gate

- Every pull request's gate decisions are logged: which rules fired, Codex's findings, who merged.
- Each week Hari spot-checks about 5 agent-merged pull requests and records yes or no: should this have come to me? (About 10 minutes.) Pull requests labelled `same-vendor-review` come first in the sample, but the sample keeps at least one unlabelled pull request when the week has one.
- Metrics: Hari's minutes per item, cost per merged pull request, gate misses, false escalations, review-unavailable escalations. Gate misses and escapes are reported split by `same-vendor-review` and the rest (ADR-018).

### Escapes

An escape is a valid P0 or P1 finding that the cold review should have caught but did not. It shows whether self-review is improving. The cases below also count findings on pushes that required a cold review under AGENTS.md "Before opening a pull request" rule 8 but skipped it. Such a skipped review is a self-review failure too. Backup pushes under rule 12 are not process breaches or escape events; the eventual pull request is assessed on its reviewed branch.

- Pre-merge escape: a valid P0 or P1 that Codex raises on a commit pushed to the pull request's branch.
- A push that violates AGENTS.md "Before opening a pull request" rule 8 is recorded as a process breach in the pull request description, and Codex's findings on it still count as escapes.
- Not an escape: a finding the author pushed knowingly under the escalation exception in AGENTS.md "Before opening a pull request" rule 8, listed in the pull request description.
- Post-merge escape: a P0 or P1 defect on `main` that a cold review of its pull request should have caught. It is recorded as an issue labelled `escape` that links the pull request that introduced it. The label does not exist yet. Who applies it is open (issue #17).
- Only valid findings count. An author's `Not valid:` reply makes a finding disputed, not invalid. A disputed finding counts as an escape unless Hari agrees with the dispute in the same thread.
- A `Not valid:` reply does not clear a P0 or P1 for merge. The merge rule in AGENTS.md still applies, so a disputed finding waits for Hari.
- P2 findings do not count.
- A pull request's count is provisional, because post-merge escapes can appear weeks later.
- Escapes are not counted by hand. A script will compute them from the Codex review threads and their replies, the pull request descriptions (escalation exceptions and process breaches) and the `escape` issues. The script and the normalisation (per inventory item or per finding) will be designed with the merge workflow (issue #17). Until then, the threads are the record.

## Deferred: Jev as the second escalation layer

Jev (TypeSafe AI) is deferred. It is trialled only when all of these hold:
1. at least about 50 labelled pull requests exist from the spot-checks;
2. the current gate shows a measured problem: one miss, or false escalations costing Hari more than about 30 minutes a week over a month;
3. Jev is generally available, or access is committed with published pricing and data handling.

The trial runs in shadow mode, logging scores without gating. Jev is adopted only if, on the shadow set, it catches every high-risk pull request the current gate missed without missing any it caught, raises fewer false escalations than Codex, and its probabilities are calibrated. Even then it only adds escalations, replacing Codex in that layer.

## Sequence to start the factory

1. Hari approves this document, AGENTS.md's GOVERNANCE section, CLAUDE.md model bindings, and CODEOWNERS. Done 2026-10-12.
2. Hari merges them to `main` and creates the ruleset. Done 2026-10-12; tested as recorded under "Ruleset".
3. The reference slice (A01) is built interactively with Hari, at code level. It sets the patterns agents copy.
4. Workflows are added: CI, implementation, scenario run with cost check, sweeper, and the merge workflow (`same-vendor-review` label, review-unavailable escalation; issue #17).
5. Historical pilot: 3 items with agent merge disabled, to verify the open questions above.
6. Agent merge is enabled for pull requests touching no protected path, under AGENTS.md GOVERNANCE "Merge rule". Hari merges every pull request touching a protected path. The merge workflow, same-vendor-review automation and review-unavailable timeout automation remain deferred to #17; sessions use AGENTS.md's REST merge procedure. Basis: [#85 decision 6318070052](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6318070052) item 1.

## Hard safety rule 8: basis and decision history

[AGENTS.md hard safety rule 8](../../AGENTS.md#hard-safety-rules) owns the workflow restrictions. This record preserves the supporting quotations, accepted risk and decision history previously held with that rule.

GitHub states that a `workflow_dispatch` event "will only trigger a workflow run if the workflow file exists on the default branch" and that "Once a workflow has run at least once, you can dispatch it against any branch or tag via the GitHub API or GitHub CLI" (https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#workflow_dispatch, read 2026-10-23); inferred from that: a branch copy of `scenario.yml` or `sweeper.yml` can run on the in-network runner before any merge. Hari accepts that: the runner group is open to all workflows in the organisation and is not pinned (https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6360524575). The Azure identity stays gated by the two GitHub environments, `parchwell-scenarios` with Hari's required approval and `parchwell-sweeper` with its `main`-only deployment branch policy, so a branch workflow gets no Azure token without Hari's approval. Fork pull requests reach no runner at all without his approval: the repository requires approval for all external contributors' workflow runs (Hari's decision of 2026-10-23 on #85, supplementing 6360524575). Rule 9 is unchanged. Basis: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6360340793, the one exception to the governance freeze in https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6318070052 item 3, after actions run 39916733004 on 2026-10-23 failed before login on a tool the runner image lacks. The new-workflow amendment follows #138 A10 in its [adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6383731545).

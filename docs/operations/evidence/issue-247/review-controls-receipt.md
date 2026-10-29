# Review controls receipt

This governance proposal implements the bounded review and preparation controls agreed with Hari in the current chat for [the review-cost retrospective](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/247). It changes documentation only; the rules on source main govern this pull request until adoption.

## Execution card (prepared before implementation)

- Deliverable: one governance PR updating preparation, specification comparison, frozen review inputs, consolidated corrections and bounded review, plus this single authored receipt.
- Current authorization: Hari directly authorized implementation, a feature branch, a fresh-session implementation subagent and PR publication in this chat. The implementation task brief is `/tmp/issue247-implementation-brief.md`; it refines the issue's proposed controls. There is no approved issue-plan comment or `ready-for-agent` label. Direct chat authorization covers this governance work; it is not represented as item-plan approval.
- Mandatory gates: current-main AGENTS.md, blueprint and engineering standards; docs checks and warning dispositions; current-main cold review before each non-backup push; hosted Codex review of final head and applicable CI; Hari merges protected governance paths. No runtime, verifier, test, workflow or Azure changes.
- Exact verification commands: `npm ci --ignore-scripts` if dependencies are absent; `npm run check:docs`; `git diff --check`. Manually walk the policy cases below. No .NET build or Azure workflow is appropriate to this documentation-only change.
- Publication checkpoint: parent publishes the tested PR after the required current-main cold review, at the earliest authorized checkpoint. Independent hosted review and CI may then run together.
- Review budget/exhaustion: this PR uses source-main review rules, with no retroactive adoption of the proposed limits. Parent records actual review rounds and any unresolved gate; current-main backup/handoff rules apply. The proposed protocol and its handoff action are checked below.

## Sources and agreed specification

- Source main: `752ff6e60a0fc32c310df4c9d2fb2fb477361326` (`origin/main` when implementation began).
- Issue body read through GitHub REST, updated `2026-10-29T12:33:05Z`; comments endpoint returned an empty list. The issue proposes controls and explicitly says no rule was changed by its creation.
- Design authority: `docs/blueprint.md`; process authority: `AGENTS.md` and `docs/agents/factory.md`; engineering context: `docs/engineering/dotnet.md`, `docs/engineering/terraform.md` and `docs/engineering/documentation-checks.md`.
- Task-brief specification: keep repository-owned `repo-review` and explicitly compare requirements with recognized plans/specs and later decisions; prepare an execution card and verifier predicate map; freeze inputs and wait for complete reports; audit findings by root cause and consolidate fixes; publish at the earliest authorized checkpoint; narrow discretionary work on a cost complaint.
- Task-brief review policy: initial comprehensive cold review, one consolidated comprehensive re-review if corrections are needed, then bounded focused correction reviews; unsafe focus or exhaustion saves work and goes to Hari with cumulative history. Preserve hosted/final-head gates, severity dispositions, substantive-finding split and prose-only P2 rollback. Preserve the historical five-PR trial. Policy limits are choices, not measured savings.
- Verifier predicate-to-test map: not applicable. No verifier code, predicates, tests or scientific contracts change.

## Validation

Commands run on the source-main working tree plus the authored proposal:

- `npm ci --ignore-scripts`: passed; installed locked dependencies. npm reported the existing `glob@10.5.0` deprecation; no dependency files changed.
- `npm run check:docs`: passed (exit 0), with eight advisory warnings and no new warnings. The final pre-review scan also includes this completed receipt.
- `git diff --check` and `git diff --cached --check`: passed (exit 0).

Warning dispositions for the changed factory document: its two existing undefined `S0` references are the adopted single-action probe decision labels in existing citations, not new source definitions in this proposal. Leave them unchanged to preserve that decision history; they do not affect this policy. All other warnings are in unchanged historical evidence files and are left as pre-existing under AGENTS.md rule 8. Parent must copy these dispositions into the PR description. Program output remains outside the repository at `/tmp/issue247-check-docs.log`.

Manual policy walkthrough (proposed rules, not a claim of actual review runs):

| Case | Walkthrough result |
|---|---|
| Clean initial comprehensive review | One full authored-diff review completes; with no required corrections, publish at the authorized checkpoint. Hosted review and independent CI may run together. Final-head merge requirements still apply. |
| Initial findings and consolidated broad correction | Wait for complete reports on the frozen revision; group causes, audit affected instances, make one correction set and run the second comprehensive pass. Later required corrections use remaining focused review. |
| Hosted findings after comprehensive passes | Dispose by severity; review corrected changes, unresolved findings and affected code/callers/tests/contracts in focused scope, with the full PR as context. Record hosted reviews separately; require coverage before each non-backup push. |
| Unsafe focused scope | A substantial design, scope or assumption change prevents safe focus: save unpublished work on the existing backup branch and hand off to Hari with the unresolved gate and history. No gate is weakened. |
| Exhausted budget across a handoff | With both comprehensive passes and both focused rounds used, a required further review cannot be started by a new session. Preserve the counters and revisions; save work and hand off to Hari. A third comprehensive pass needs his explicit decision. A clean result at the limit does not itself create another review requirement. |
| Prose-only P2 after clean initial review | A prose-only correction in existing diff files uses one focused round, without a needless second comprehensive pass. That round reports only P0/P1 and does not repeat. If it raises a regression, revert those P2 fixes and publish the previously clean reviewed diff if the finding disappears; otherwise use the Hari handoff. |
| Prose-only P2 with no focused round remaining | The applicable review gate cannot fit the budget: use the handoff before making the correction, rather than obtaining a free extra review. Reverted and other P2 findings retain the existing severity dispositions. |
| Substantive-finding split and historical trial | Cold rounds remain excluded from the existing consecutive-substantive-finding split rule; the rule still applies to reviews it counts. The five-PR prose-only trial and its linked decision evidence remain intact. |

The walkthrough found no contradiction among the proposed budget, P2 rollback, split rule and final-head gates. This is authored policy validation, not the mandatory independent cold review.

## Review and publication evidence

The initial fresh-context cold review completed against source main and the full authored diff (SHA-256 `6efce5ae497847780f1b32eb40eef75d60bdd66040ee599275c3d512b3c502b0`). It reported `Findings: none at P0 or P1` and no reportable P2. Parent review then identified a report-placement loop: requiring the final complete report in the authored receipt would change that report's own reviewed diff. The prose correction places reports, dispositions and cumulative counts in the PR self-review record, while keeping review input identification in the receipt.

The current review mode is the current-main prose-only P2 follow-up, reporting only P0/P1 against the full corrected diff. Its comparison input is the initial reviewed diff identified above. The final report belongs in the PR description; this receipt does not need a further edit to describe its own final review. Hosted Codex review, final-head CI and publication remain pending. No CI success is claimed here.

## Adoption and pilot

Keep the retrospective issue open after this PR. Completion requires merged adoption and evidence from the next implementation started after adoption: coverage complete before first review, complete reports before corrections, consolidated corrections, earliest authorized publication, cumulative broad/focused/hosted review counts and any budget conflict surfaced before another review. Record that pilot on the issue; no token usage or savings is promised.

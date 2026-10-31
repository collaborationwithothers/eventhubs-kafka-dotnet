# Rule 13 measurement link: receipt

This receipt records a one-sentence change to AGENTS.md "Before opening a pull request" rule 13. The rule pointed per-item measurement only at the original #264 decision rules, whose Codex method a later decision replaced. The sentence now also links that later decision, so a session that follows the rule finds the method in force.

## Execution card

- Deliverable: in rule 13, extend the sentence "Per-item measurement follows the [#264 decision rules](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6542232891)." so it also links the [Codex measurement decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6546378413), which replaces the decision rules' Codex method with CLI replays. No other rule text changes.
- Authorization: Hari's answer in an interactive Claude Code session on 2026-10-31, choosing "Rule 13 link now" from the open follow-ups of issue #264. The follow-up is listed in the [rule 13 correction completion comment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6547223722), "Follow-ups, not blocking", item 1. Directly authorized governance work without an approved item plan; no plan approval is claimed.
- Order of changes: the [decision rules](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6542232891), "Order of changes after the results", ask that the next real item be measured before the next change. This change is a citation correction, not one of the four listed changes. It still precedes the measurement because Hari chose it after being told it does; the session had recommended batching it with the next governance change.
- Mandatory gates: `git diff --check`, `npm run check:docs`, one fresh-context cold review under AGENTS.md "Before opening a pull request" rule 8, CI, hosted Codex review, and Hari's merge, because AGENTS.md is a protected path.
- Publication checkpoint: open the pull request as a draft after the first cold review with no P0 or P1.
- Review budget: rule 8 as written, no rounds used before this pull request. Exhaustion action: rule 8's review-budget handoff to Hari.
- Predicate-to-test map: not applicable; no verifier work.

## Claims and sources

1. The Codex measurement decision amends the decision rules' "Order of changes after the results" and "Everything else in those rules stands": stated in its opening paragraph.
2. It replaces "Codex credits before and after each session" with Codex CLI replays of the item's plan check and implementation: stated in its sections "Why" and "Replacement", point 1.
3. The decision rules' measurement sentence is "Codex credits before and after each session, and Claude token totals from transcripts": stated in "Order of changes after the results".

## Verification

| Command | Observed result |
| --- | --- |
| `grep -rn "6542232891\|6546378413" --include=*.md .` outside docs/operations/evidence, before the change | One match: AGENTS.md rule 13, linking 6542232891 only. |
| `git diff --check` | Exit 0, no output. |
| `npm run check:docs` | 8 warnings, the same count and files as on the source commit (docs/agents/factory.md and three evidence files under issues 124, 140 and 211), none in files this change touches. |

Source commit: `19fbb5e`.

## Cold review

Comprehensive round 1: input is the staged diff against `origin/main` at `19fbb5e`. Report and dispositions are in the pull request's self-review record.

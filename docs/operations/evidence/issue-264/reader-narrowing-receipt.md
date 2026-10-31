# Reader subagent narrowing: receipt

This receipt records the second governance change of issue #264. It limits what each reader subagent reads in the planning, plan-check and implementing sessions, and leaves how many readers run to the session. It also points AGENTS.md rule 13 at the decision that amended per-item measurement.

## Execution card

- Deliverable: a "Reader subagents" rule in docs/agents/factory.md "Authority reads", which owns the reading lists. The `plan-pr`, `plan-check` and `implement-plan` skills cite it where they spawn readers. AGENTS.md "Before opening a pull request" rule 13 also links the [second-change decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6548603494), because its decision 3 amends the per-item measurement that rule 13 points to.
- Authorization: [decision 6548603494](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6548603494), decision 2: "one governance pull request linked to this issue, merged by Hari, which defines the narrowing". The session prompt: "plan and implement the second governance change defined in decision 6548603494". Directly authorized governance work without an approved item plan; no plan approval is claimed.
- Codex cost: the decision's "Not decided here" leaves it to this pull request to propose whether the change targets Codex sessions. Proposal: it does, so both Codex replays run. See "Codex proposal" below. Hari accepted the proposal in an interactive Claude Code session on 2026-10-31 ("I authorise and accept your proposal"), in reply to the session's summary of the [review-budget handoff](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6548849045), which proposed both replays.
- Mandatory gates: `git diff --check`, `npm run check:docs`, the cold review under AGENTS.md "Before opening a pull request" rule 8, CI, hosted Codex review, and Hari's merge, because every changed file except this receipt is a protected path (`.github/CODEOWNERS` lists `/docs/operations/` with no owner).
- Publication checkpoint: open the pull request as a draft after the first cold review round with no P0 or P1.
- Review budget: rule 8 as written, no rounds used before this pull request. Exhaustion action: rule 8's review-budget handoff to Hari. The budget ran out after focused round 2, and the session handed off ([comment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6548849045)). In the same reply on 2026-10-31, Hari authorized one more focused round for the prose-only fix to the "Codex proposal" basis that the handoff proposed.
- Predicate-to-test map: not applicable; no verifier work.

## Design

The decision fixes the lever: narrow what each reader reads, not how many readers run, "because readers carry findings". The rule therefore keeps the reader count and changes five things.

1. The session does the catch-up. A reader never lists or reads the issue thread, so the thread is read once per session, not once per reader.
2. Each reader gets a written assignment: named inputs and one question. A file the plan changes is assigned at each changed place with its enclosing section or member. This keeps factory.md "Item lifecycle" step 1's source-reading rule, which exists because both #245 replays missed the workflow-selector defect that the live check found by reading the workflow file ([first change](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6544062669), part c).
3. Each input goes to one reader or the session, not to two readers.
4. A reader starts with a fresh context. It reads beyond its assignment only when an assigned input points there and the question needs it, and it reports each such read.
5. Reports cite locations and quote only what a finding needs. The session checks each claim it relies on.

The cold reviewer, the standing-approval planning and check subagents, and subagents that edit files are excluded. The first two already have defined inputs: AGENTS.md "Before opening a pull request" rule 8 for the cold reviewer, and "given only the issue number and the words 'standing approval'" in factory.md "Item lifecycle" step 3 for the others. Editing subagents, such as an implementer, have no defined input in any repository text; they are excluded only because decision 2 narrows readers. So this rule does not reduce implementer cost. In the #245 run 4 replay the Luna implementer used 41,688,164 tokens and the only other subagent 1,330,349 ([results](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6543624566)).

## Codex proposal

Proposal: the change targets Codex cost, so the next real item's Codex plan check and implementation are both replayed under the [CLI replay decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6546378413). The rule applies to readers in all three sessions, implementation included.

Basis: in the #245 run 1 plan check replay, the main thread carried 25% of tokens, with two Sol subagents ([results](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6543624566)). In the #268 plan check replay, subagents carried 60% of credits ([measurement](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6548235865)). Every credit share in this paragraph is an estimate: the measurement prices measured tokens at the [repricing](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6544062669) rates, on 2026-10-31. The implementation replays are mixed. In #245 run 4, the Luna implementer used 41,688,164 of 43,018,513 subagent tokens (96.9%, by script; results comment). In #268 replay B, the Luna implementer carried 7% of credits and the Sol main thread 51% (measurement comment). The remaining 42%, inferred by subtraction, belongs to replay B's two Sol subagents, which the measurement calls "readers/reviewers" without splitting them. Replay B ran "no cold review", per the same comment. So how much implementation cost this rule can reach is not measured. On the Claude side of #268, the [plan-check session](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/268#issuecomment-6547445507) spawned no subagents, and the [planning session](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/268#issuecomment-6547449128)'s two subagents carried 18.6% of its tokens, computed by script from that comment's table (a snapshot that includes later non-planning turns). Inference: on the Claude side the change can show only if Claude sessions spawn readers, so the Claude numbers alone may not show it.

## Claims and sources

1. Decision 2 sets the lever, its reason and that Hari merges: decision 6548603494, "Decisions", item 2.
2. Decision 3 requires every Claude session of the next real item to record token totals by script, and runs Codex CLI replays only for a change that targets Codex cost: same comment, item 3.
3. Whether the change targets Codex is for this pull request to propose: same comment, "Not decided here".
4. The three skills spawn readers with the sentence "Use subagents at high effort for reading.": `.agents/skills/plan-pr/SKILL.md` and `.agents/skills/plan-check/SKILL.md` step 1, on `main` at the source commit. `implement-plan` step 2 says "Use subagents at high effort, including for the cold review".
5. Factory.md "Authority reads" owns the per-step reading lists: its opening sentence, "Each step reads a defined list, not the whole issue thread."

## Verification

| Command | Observed result |
| --- | --- |
| `grep -rn "subagents at high effort" .agents/skills`, before the change | Three matches: `plan-pr` step 1, `plan-check` step 1, `implement-plan` step 2. |
| `git diff --check` | Exit 0, no output. |
| `npm run check:docs` | 8 warnings, the same count and files as on the source commit. Two are undefined-identifier warnings in docs/agents/factory.md, at its line 7 and in "Evidence pack", outside the lines this change adds; they stand because this change does not touch that text. The other six are in evidence files under issues 124, 140 and 211, which this change does not touch. |

Source commit: `622620c`.

## Cold review

Comprehensive round 1: input is the staged diff against `origin/main` at `622620c`. Report and dispositions are in the pull request's self-review record.

Comprehensive round 2, the consolidated re-review: input is the staged diff against `origin/main` at `622620c`, after the fixes for round 1's one P1 and three P2 findings, all fixed. Cumulative: 2 comprehensive, 0 focused.

Focused round 1: input is commit `ae590fa`, which is 4d9be55 (round 2's reviewed content) plus the fixes for round 2's one P1 and three P2 findings, all fixed, against `origin/main` at `622620c`. Cumulative: 2 comprehensive, 1 focused. It reported one P1: the round 2 fix dropped the implementation replay on a basis the #268 measurement contradicts. Fixed by restoring both replays with a sourced basis.

Focused round 2, the last round the budget allows: input is commit `1c44d7b`, against `origin/main` at `622620c`. Cumulative: 2 comprehensive, 2 focused. It reported two P1 findings and one P2, all on the "Codex proposal" basis: a cold reviewer was assumed in replay B, which ran none; credit shares were not labelled as estimates; and the opening sentence set implementation evidence aside before using it. All three were fixed.

Focused round 3, authorized by Hari beyond the rule 8 budget: input is the commit that carries this line, against `origin/main` at `622620c`. Cumulative: 2 comprehensive, 3 focused.

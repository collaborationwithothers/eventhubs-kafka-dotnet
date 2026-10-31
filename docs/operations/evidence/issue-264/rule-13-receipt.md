# Rule 13 measurement line: receipt

This receipt records a one-sentence correction to AGENTS.md "Before opening a pull request" rule 13. The rule said per-chat usage on the Codex dashboard measures the vendor allowance. The 2026-10-31 measurement showed that this does not hold, so the sentence now says no routine per-session measure exists and cites the observation.

## Execution card

- Deliverable: replace "and per-chat usage on the Codex dashboard is its measurement" in rule 13 with a statement supported by the measurement. No other rule text changes.
- Authorization: Hari's instruction in an interactive Claude Code session on 2026-10-31, "fix the rule 13 measurement line". The correction is listed as needed in the [issue #264 body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264), section "Measurement", item "Correction needed". Directly authorized governance work without an approved item plan; no plan approval is claimed.
- Mandatory gates: `git diff --check`, `npm run check:docs`, one fresh-context cold review under AGENTS.md "Before opening a pull request" rule 8, CI, hosted Codex review, and Hari's merge, because AGENTS.md is a protected path.
- Publication checkpoint: open the pull request after the first cold review with no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: rule 8's review-budget handoff to Hari.
- Predicate-to-test map: not applicable; no verifier work.

## Claims and sources

Each claim in the new sentence is stated in the [measurement results comment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6543624566) of 2026-10-31:

1. "Codex credits did not move": the results say "credits did not move", and the readings table shows 62,217 credits before the prechecks and 62,217 during run 4.
2. "while plan allowance was used": the same table shows weekly allowance left falling from 55% to 53%.
3. "the allowance display moves in whole percentage points": the results say "The allowance shows whole percentages, so per-run deltas are not resolvable."

"No routine per-session measure of it exists" is inferred from those observations for the Codex side. For the Claude side, the issue body states that cloud containers are deleted at session end, so transcript totals must be captured first, and no capture exists in this repository.

## Verification

| Command | Observed result |
| --- | --- |
| `git diff --check` | Exit 0, no output. |
| `npm run check:docs` | 8 warnings, the same count as on the source commit, all in files this change does not touch. |
| `grep -rn "Codex dashboard\|per-chat usage"` over Markdown outside docs/operations/evidence, before the change | One match: AGENTS.md rule 13. |

Source commit: `2cc756b`.

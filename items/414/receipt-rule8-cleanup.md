# Receipt: removal of expired rule 8 measurement text

This is the single authored receipt for the pull request that removes two expired measurement paragraphs from docs/agents/pull-requests.md rule 8 and one expired line from the pull request template. Both measurements have been decided, so the text no longer instructs anything.

## Execution card

- **Deliverable:** delete the rule 8 paragraph that starts "Count the first five pull requests merged after the #184 pull request merged"; delete the rule 8 paragraph that starts "Measure the cold-review rubric changed for [issue #414]"; delete the template line "Historical prose-only P2 trial evidence (AGENTS.md rule 8), if applicable:"; add this receipt. No other change.
- **Authorization:** directly authorized governance work with no approved item plan. Hari asked for it in an interactive session on 2026-11-10, recorded as point 4 of [decision 6776023893](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/414#issuecomment-6776023893).
- **Specification sources:** that decision; [issue 184 decision 6401912768](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/184#issuecomment-6401912768) of 2026-10-25 ("B stays. Rule 8 is unchanged."), which ended the prose-only P2 measurement; [measurement 6775913589](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/414#issuecomment-6775913589), which ended the rubric measurement.
- **Mandatory gates:** `npm run check:docs`; cold review under rule 8; hosted Codex review of the head commit; Hari's merge, because both changed rule files are protected paths.
- **Earliest publication checkpoint:** a draft pull request after the first cold-review round with no P0 or P1.
- **Review budget:** rule 8's cumulative budget of two comprehensive and two focused rounds. On exhaustion: push to `-wip` and hand off to Hari.
- **Predicate map:** none. No verifier predicate changes.

## Implementer choices

1. The prose-only P2 round itself stays in rule 8; only its expired measurement paragraph is removed. Issue 184's decision kept that round.
2. Branch: `claude/414-rule8-cleanup`, as CLAUDE.md "Identity" requires. The session was assigned `claude/busy-darwin-eoaoly`, and the harness forbids pushing to another branch without permission. Hari gave that permission in the interactive session on 2026-11-10, after cold-review rounds 1 and 2 flagged the assigned name.
3. Receipt path: `items/414/receipt-rule8-cleanup.md`, next to the issue's existing receipt, which this pull request does not change.

## Results

Run from the repository root on a working tree based on `origin/main` `2ed497d`.

| Check | Result |
|---|---|
| `git diff --stat origin/main` before this receipt | 2 files, 5 deletions: `.github/pull_request_template.md` 1, `docs/agents/pull-requests.md` 4 |
| `grep -rn -i -E 'first (five\|three) pull requests\|prose-only P2 round\|rubric stays\|issue #414'` outside `results/`, `items/` and evidence folders, before the change | only the two removed paragraphs matched |
| `npm run check:docs` | 8 warnings, all in files this pull request does not change (`docs/agents/factory.md`, evidence READMEs for issues 124 and 140, receipt for issue 211) |

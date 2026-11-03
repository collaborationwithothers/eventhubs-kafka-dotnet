# Receipt: leakage correction notes for 14 published results

This pull request adds a dated correction note to the 14 published results whose mutation gate cited a C5 campaign that ran before pull request 341. No verdict word, expected outcome, scenario, run data or other wording changes. Hari reviews and merges it.

## Execution card

- Deliverable: 28 result files (14 `.md`, 14 `.json`) with insert-only changes, the script that makes and verifies them, the item state file and this receipt.
- Authorization: [issue 342](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342) body points 1 to 4, [decision 6612103873](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6612103873), [decision 6612352786](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6612352786) and [decision 6612892439](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6612892439), in creation order. `ready-for-agent` was applied 2026-11-03T18:26:32Z; no comment names Codex, so the implementer is Claude Code.
- Plan: [Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6612715816), updated_at 2026-11-03T19:12:33Z, approved under standing approval by [plan check 6613306689](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6613306689). Its one P2 (the branch did not yet exist, only `-wip`) needs no plan change.
- Mandatory gates: the plan's acceptance commands 1 to 7; cold review under rule 8; Codex review of the head commit. The merge is Hari's: `results/` is a protected path and the label is `verdict`.
- Predicate map: not applicable (no verifier code changes).
- Earliest publication checkpoint: after the first comprehensive cold review has no open P0 or P1.
- Review budget: two comprehensive passes, then at most two focused rounds. Exhaustion action: push -wip and hand off to Hari (rule 8).

## Commands run (source commit `a5bbb24bd9156c8c15d246c85782edf36724f16c`, full-history clone)

1. `check-artifacts` printed `marker lines` 0 for A01, A03, B01, B03, B04, B05, C08, C12 and F04, 24 for B08 and E01, 6 for F01, 12 for F02, 42 for F06, then `artifacts ok`.
2. `apply 2026-11-03` exited 0 with no output.
3. `verify 2026-11-03` printed `verify ok 14`.
4. `git diff --numstat origin/main -- results/` gave 28 files, 701 insertions, 0 deletions. Per `.md`: 13 lines for A01, B01, B03, B04, B05, C08, C12 and F04, 12 for the others. Per `.json`: 37 or 38.
5. `sha256sum` of `make_notes.py`: `d08b18eed3d407adda4825dddfddfa2da483d129201bb50267a50ad106e309c5`, as the plan pins.
6. `npm ci --ignore-scripts && npm run check:docs` exited 0 with 8 warnings, none in a file this pull request changes (no warning under `results/` or this directory, none for the `#correction-note-2026-11-03` anchor).

## Scope notes

- All 14 re-judges are recorded as gaps, as decision 6612892439 point 1 directs. No offline driver was written, no workflow was dispatched and no Azure was used.
- The script pins the values it writes (gate runs, source commits, blob comparisons); `check-artifacts` and the blob comparison re-check the blob and artifact facts, and the plan check verified the `.json` gate keys against `main`.
- Retaining judge inputs so results can be re-judged later is a separate issue and is not part of this pull request.
- Work on A02 and A04 ([issue 320](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/320), [issue 322](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/322)) is untouched.

## Cold review

- Round 1: comprehensive review of the whole authored diff. Input: the staged index against merge base `a5bbb24bd9156c8c15d246c85782edf36724f16c` (`origin/main`). No comparison revision (first round). Specification sources: the plan, plan check and decisions listed under the execution card. Counts after this round are recorded in the pull request self-review record.

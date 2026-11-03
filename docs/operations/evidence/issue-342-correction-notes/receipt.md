# Receipt: leakage correction notes for 14 published results

This pull request adds a dated correction note to the 14 published results whose mutation gate cited a C5 campaign that ran before pull request 341. No verdict word, expected outcome, scenario, run data or other wording changes. Hari reviews and merges it.

## Execution card

- Deliverable: 28 result files (14 `.md`, 14 `.json`) with insert-only changes, the script that makes and verifies them, the item state file and this receipt.
- Authorization: [issue 342](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342) body points 1 to 4, [decision 6612103873](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6612103873), [decision 6612352786](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6612352786) and [decision 6612892439](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6612892439), in creation order. `ready-for-agent` was applied 2026-11-03T18:26:32Z; no comment names Codex, so the implementer is Claude Code.
- Plan: [Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6612715816), updated_at 2026-11-03T20:07:19Z, approved under standing approval by [plan check 6614404898](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6614404898), which applies [decision 6614157548](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6614157548). Its one P2 (acceptance 7 missing from this receipt) is fixed below.
- Mandatory gates: the plan's acceptance commands 1 to 7; cold review under rule 8; Codex review of the head commit. The merge is Hari's: `results/` is a protected path and the label is `verdict`.
- Predicate map: not applicable (no verifier code changes).
- Earliest publication checkpoint: after the first comprehensive cold review has no open P0 or P1.
- Review budget: two comprehensive passes, then at most two focused rounds. Exhaustion action: push -wip and hand off to Hari (rule 8).

## Commands run (source commit `d714d34`, full-history clone)

1. `check-artifacts` printed `marker lines` 0 for A01, A03, B01, B03, B04, B05, C08, C12 and F04, 24 for B08 and E01, 6 for F01, 12 for F02, 42 for F06, then `artifacts ok`.
2. `apply 2026-11-03` exited 0 with no output.
3. `verify 2026-11-03` printed `verify ok 14`.
4. `git diff --numstat origin/main -- results/` gave 28 files, 701 insertions, 0 deletions. Per `.md`: 13 lines for A01, B01, B03, B04, B05, C08, C12 and F04, 12 for the others. Per `.json`: 37 or 38.
5. `sha256sum` of `make_notes.py`: `64d770aa5ddd8c8f631ba407ec26a4062654edcd298a524a31a150de49549a27`, as the plan pins.
6. `npm ci --ignore-scripts && npm run check:docs` exited 0 with 8 warnings, none in a file this pull request changes (no warning under `results/` or this directory, none for the `#correction-note-2026-11-03` anchor).

7. `git diff --name-only origin/main`: 31 files, exactly the 28 result files, `items/342/state.md`, and the two files under `docs/operations/evidence/issue-342-correction-notes/` (checked by the cold-review round 1 reviewer and again at head).

## Scope notes

- Revision 3 changes two things against Revision 2: the "did not measure exactly" sentence is written only where a judge file differs ([decision 6614157548](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6614157548)), and the script's classification skips A02 and A04, which `main` gained after Revision 2 and which already cite the corrected campaign.

- All 14 re-judges are recorded as gaps, as decision 6612892439 point 1 directs. No offline driver was written, no workflow was dispatched and no Azure was used.
- The script pins the values it writes (gate runs, source commits, blob comparisons); `check-artifacts` and the blob comparison re-check the blob and artifact facts, and the plan check verified the `.json` gate keys against `main`.
- Retaining judge inputs so results can be re-judged later is a separate issue and is not part of this pull request.
- Work on A02 and A04 ([issue 320](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/320), [issue 322](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/322)) is untouched.

## Cold review

- Round 1 (comprehensive, 1 of 2): input the staged index at `ec3285f` against merge base `a5bbb24bd9156c8c15d246c85782edf36724f16c`; no comparison revision. Reported one P1 (the "did not measure exactly" sentence, answered by decision 6614157548), two P2s (acceptance 7 result, state-file next action; both fixed) and the expected P0 on `results/` under a `verdict` label.
- Round 2 (comprehensive re-review, 2 of 2): input the staged index against merge base `origin/main` (`d714d34`). Mode: comprehensive; no comparison revision. Specification sources: Revision 3 of the plan, plan check 6614404898 and decisions 6612103873, 6612352786, 6612892439 and 6614157548, listed under the execution card. Cumulative counts: 2 comprehensive, 0 focused.

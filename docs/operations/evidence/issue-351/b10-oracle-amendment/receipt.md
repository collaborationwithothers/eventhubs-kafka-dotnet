# Receipt: B10 oracle amendment

This pull request adds ADR-021 to docs/blueprint.md. It lets B10 repeats stay valid under timed-out sends, application retries and one sequential producer restart in a second load generator Job, and it classifies late appends up to a phase sweep line. It changes no code, test, scenario, workflow or result.

## Execution card

- Deliverable: the ten changes of the approved plan, the two `apply in implementation` P2 edits, the item state file and this receipt, and the two ADR-013 pointers of [decision 6625271946](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6625271946), which amends the plan.
- Authorization: [issue 351](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351) body, [decision 6617622676](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6617622676), [decision 6623554949](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6623554949), [decision 6623766068](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6623766068), the [#347 standing authority](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6617257796), [decision 6617344472](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617344472) [decision 6625271946](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6625271946) and the [split adoption 6617607468](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617607468).
- Plan: [Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6617796257), updated_at 2026-11-04T06:30:53Z, checked by [plan check 6624472543](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6624472543) and approved by Hari's [approval 6624892579](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6624892579), which accepts both P2 fixes.
- Mandatory gates: the plan's acceptance steps 1 to 6; cold review under rule 8; final-head CI; Codex review of the head commit; Hari's merge (protected path).
- Predicate map: not applicable (no verifier work).
- Earliest publication checkpoint: after the first comprehensive cold review has no open P0 or P1.
- Review budget: two comprehensive passes, then at most two focused rounds. Exhaustion action: push -wip and hand off to Hari (rule 8).

## Commands run (base `cbbff70`, working tree that became commit `0433212`)

1. Plan step 1: the two blocks extracted from the plan comment matched their sha256 values; output `blocks ok`.
2. Plan step 2 with the approval URL above: `applied 10 edits`. Then the P2 script of plan check 6624472543: `applied 2 check edits`.
3. `git diff --numstat` before this receipt and the state file: `44	8	docs/blueprint.md`.
4. `grep -c "ADR-021" docs/blueprint.md`: `10`. `grep -c "S48" docs/blueprint.md`: `3`.
5. ASCII scan of added lines: `0`.
6. `npm ci && npm run check:docs`: exit 0, `8 warnings`, none in docs/blueprint.md (`docs/blueprint.md: no issues found`). With the change stashed the run gives the same 8 warnings, so none are new.

## Scope notes

- Authority refreshed at 2026-11-04T07:13:14Z: comment IDs and updated_at values on #351 (14 comments), #347 (1) and #348 (4) were unchanged since the first read.
- No comment on #351, #347 or #348 names an implementing tool; Claude Code by default.

## Amendment by decision 6625271946 (commit `c00f78a`)

- Change: append "ADR-021 amends this for B10 repeats." to the ADR-013 sentence containing "excluded from classification" and to the one containing "applies to one repeat's window". No other text changes.
- Commands: `git diff --numstat HEAD~1 HEAD` gave `2	2	docs/blueprint.md`; the ASCII scan of added lines gave `0`; `grep -c ADR-021 docs/blueprint.md` gave `12`; `npm run check:docs` exited 0 with the same 8 warnings and `docs/blueprint.md: no issues found`.
- Review: one focused cold-review round, input revision the head that contains this receipt update, comparison `e540435` (the revision the comprehensive pass 1 reviewed). Cumulative counts are in the pull request description.

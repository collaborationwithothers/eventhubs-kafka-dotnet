# Issue 304 receipt

This pull request narrows the F01 row of docs/blueprint.md "Inventory amendment 1", "Folded into existing items", to what the handler-refresh test checked. Two removed hypotheses get new homes and three catalogue cells change; no verdict, expected outcome or result changes.

## Execution card

- Deliverable: one pull request with changes 0 to 6 of the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/304#issuecomment-6560185796), Revision 3: four edits in docs/blueprint.md, three catalogue cells in docs/research/kubernetes-kafka-clients.md, `items/304/state.md` and this receipt.
- Authorization: plan Revision 3 (`updated_at` 2026-11-01T16:20:19Z) approved by [comment 6560751571](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/304#issuecomment-6560751571) after [plan check 6560654165](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/304#issuecomment-6560654165) (`Blocking findings: none`), with decisions 6560271529 and 6560557177. The check's one `apply in implementation` P2 (this receipt's wording) is applied.
- Mandatory gates: acceptance commands 1 to 4 of the plan, the cold review under AGENTS.md "Before opening a pull request" rule 8, hosted CI, Codex review and Hari's merge, because `docs/` is a protected path.
- Publication checkpoint: open the pull request after the acceptance commands pass and the first cold review reports no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: its review-budget handoff to Hari.

## Predicate map

Not applicable: no verifier work.

## Commands and results

Branch cut from `main` at `831a2e7e57f36f65a8e84a7e218c1c81a93384bd`. Each entry names the commit it ran on.

1. Commit `7c27cde` (changes 0 to 5).
   - Acceptance command 1 (exact text of changes 0 to 4) printed `OK`, exit 0.
   - Acceptance command 3 (state file shape) printed `OK`, exit 0.
   - Acceptance command 2: `git diff --name-only origin/main...HEAD` listed the three committed files; with this receipt staged, `git diff --name-only --cached origin/main` listed exactly the plan's four files. The stat for `docs/blueprint.md` printed `1 file changed, 4 insertions(+), 4 deletions(-)` and for `docs/research/kubernetes-kafka-clients.md` `1 file changed, 3 insertions(+), 3 deletions(-)`. `git diff --check origin/main...HEAD` printed nothing, exit 0.
   - Acceptance command 4: `npm ci --ignore-scripts && npm run check:docs` printed `8 warnings`, none in the edited documents or `items/304/state.md`. The 8 are in files this change does not touch (`docs/agents/factory.md`, `docs/operations/evidence/issue-124/README.md`, `docs/operations/evidence/issue-140/README.md`, `docs/operations/evidence/issue-211/receipt.md`). A rerun with this receipt present is recorded in the pull request.
2. Commit `9ec48bd` (adds this receipt). Acceptance command 2 as the plan writes it, after `git fetch origin main`: `git diff --name-only origin/main...HEAD` printed exactly `docs/blueprint.md`, `docs/operations/evidence/issue-304/receipt.md`, `docs/research/kubernetes-kafka-clients.md` and `items/304/state.md`, in that order. The two stat commands printed `1 file changed, 4 insertions(+), 4 deletions(-)` and `1 file changed, 3 insertions(+), 3 deletions(-)`. `git diff --check origin/main...HEAD` printed nothing, exit 0.
3. No build or test run: documentation only (CLAUDE.md "Before opening a pull request" item 1).

## Cold review

- Round 1: comprehensive, 1 of at most 2 comprehensive passes; cumulative focused rounds at its start: 0. Input revision: `7c27cde` plus this receipt as staged. Comparison: `origin/main` at `831a2e7`.
- Specification sources: the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/304#issuecomment-6560185796) (changes 0 to 6, acceptance), its [plan check 6560654165](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/304#issuecomment-6560654165), [approval 6560751571](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/304#issuecomment-6560751571), [decision 6560271529](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/304#issuecomment-6560271529), decision 6560557177, the [#304 body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/304), and #302 decisions [6558920636](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/302#issuecomment-6558920636) and [6560081575](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/302#issuecomment-6560081575).
- Round 1 report (reviewed revision `7c27cde` plus this receipt; reviewer model `claude-opus-5-5`): no P0 or P1. One P2: the receipt did not record command 2 as the plan writes it. Disposition: fixed, entry 2 above. Pre-existing, not blocking: the catalogue finding 4 heading (line 18), which the plan leaves unchanged and plan check 6560654165 raised for Hari.
- Round 2: focused P2-correction review under rule 8, 1 of at most 2 focused rounds; comprehensive passes used: 1. Reports only P0 or P1. Input revision: the commit that holds this receipt edit. Comparison: `9ec48bd`.

# Issue 315 receipt

This pull request narrows the A02 row of docs/blueprint.md "Inventory amendment 1", "Folded into existing items", and adds an A03 row and an A04 clause there. Two catalogue cells change in docs/research/kubernetes-kafka-clients.md; no verdict, expected outcome or result changes.

## Execution card

- Deliverable: one pull request with changes 0 to 6 of the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/315#issuecomment-6563631886), Revision 1: four edits in docs/blueprint.md, two catalogue cells in docs/research/kubernetes-kafka-clients.md, `items/315/state.md` and this receipt.
- Authorization: plan Revision 1 (`updated_at` 2026-11-01T20:44:15Z) approved under standing approval by [plan check 6563740367](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/315#issuecomment-6563740367) (`Blocking findings: none`, no `apply in implementation` findings), with [#315 decision 6563545115](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/315#issuecomment-6563545115) and the #301 decisions it adopts.
- Mandatory gates: acceptance commands 1 to 4 of the plan, the cold review under AGENTS.md "Before opening a pull request" rule 8, hosted CI, Codex review and security review under AGENTS.md GOVERNANCE "Merge rule"; `docs/` is a protected path, so the merge is Hari's or the #301 control session's under the [override decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563515099), point 1, and the [bypass decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563540913), point 2.
- Publication checkpoint: open the pull request after the acceptance commands pass and the first cold review reports no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: its review-budget handoff to Hari.

## Predicate map

Not applicable: no verifier work.

## Commands and results

Branch cut from `main` at `4bc7ae3560d4b292afb0556ce658a6596bf3abdb`. Each entry names the commit it ran on.

1. Commit `c663b53` (changes 0 to 5).
   - Acceptance command 1 (exact text of changes 0 to 4) printed `OK`, exit 0.
   - Acceptance command 3 (state file shape) printed `OK`, exit 0.
   - Acceptance command 2: `git diff --name-only origin/main...HEAD` listed the three committed files; this receipt is the fourth. `--stat` ended `1 file changed, 4 insertions(+), 3 deletions(-)` for docs/blueprint.md and `1 file changed, 2 insertions(+), 2 deletions(-)` for docs/research/kubernetes-kafka-clients.md. `git diff --check` printed nothing.
   - Acceptance command 4: `npm ci --ignore-scripts && npm run check:docs` printed `8 warnings`, none in the edited documents or `items/315/state.md`. The 8 are in files this change does not touch.
2. No build or test run: documentation only (CLAUDE.md "Before opening a pull request" item 1).

## Cold review

(Filled in per round below.)

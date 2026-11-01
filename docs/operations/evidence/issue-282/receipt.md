# Issue 282 receipt

This records the tests added for the 94 shard p4 survivors of C5 run 41232286130 and the disposition of each. It claims no C5 score, because the replay is diagnostic and the next complete hosted C5 run on `main` measures the target.

## Execution card

- Deliverable: file changes 1 to 5 of the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/282#issuecomment-6557763377): the test file, `replay.py`, the disposition section in docs/operations/verifier-mutation.md, this receipt and `items/282/state.md`.
- Authorization: plan `Revision 1` (`updated_at` 2026-11-01T12:11:58Z) approved by [comment 6557963769](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/282#issuecomment-6557963769) after [the plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/282#issuecomment-6557949954), whose `apply in implementation` P2 (mutator name `Logical not` for row 12502) is applied. Scope from [split decision 6556141000](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6556141000) as adopted by the issue body. Later decision: [6557346336](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/282#issuecomment-6557346336), nothing under `results/`.
- Mandatory gates: the acceptance commands below, the cold review under AGENTS.md "Before opening a pull request" rule 8, hosted CI, Codex review and Hari's merge, because `tests/Parchwell.Verifier.Tests/` is a protected path (`.github/CODEOWNERS` line 16).
- Publication checkpoint: open the pull request after the first cold review with no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: its review-budget handoff to Hari.

## Predicate map

Predicate map: no verifier predicate changes. The table in [Issue 282 survivor dispositions](../../verifier-mutation.md#issue-282-survivor-dispositions) maps each surviving mutant to the test that kills it.

## Commands and results

Branch cut from `main` at `0db2375a23605ea2ce9c7d6185886d7436d51d00`, SDK 10.0.402.

- `git diff --quiet 2dc9715e3de176f7e1cd55eee9c251e43c29c746 HEAD -- src/Parchwell.Verifier && echo unchanged`: printed `unchanged`.
- `ParchwellVerifierMutationOnly=true dotnet test ... -c Release` on the branch with file changes 1 and 2 committed: `total: 4064`, `failed: 0`, `succeeded: 4064`.
- Artifact 13056294399 ZIP SHA-256 `698ee772ab75425a57661b5255f92c7acd569aed05600b22c2ac670226784895`; report SHA-256 `682a5d7b2f0b46ad10dd1652dcf1edde00fd7b3956ff4c20425543f769c3cc08`. Both equal the plan.
- `replay.py run`, three workers, 12:34:04Z to 13:04:34Z: 30.5 minutes of wall time on 4 CPUs.
- `replay.py check` over the three outputs: `rows 94 results 94 problems 0`, exit 0. No timing exception and no rerun was needed.
- After `git merge origin/main` (a textual conflict in docs/operations/verifier-mutation.md, resolved by keeping the issue 281 section before this one): the same test command reported `total: 4100`, `failed: 0`, `succeeded: 4100`. This is 89 above `main`'s 4011 for the same command, with the issue 281 tests merged.
- `./scripts/verify-dotnet.sh` was not run: this cloud session has no Docker daemon. The hosted `dotnet-verification` run on the pull request is the evidence.
- `git diff --name-only origin/main` lists exactly the five files named by the plan; `git diff --check origin/main` exits 0.
- `npm ci --ignore-scripts && npm run check:docs`: `8 warnings`, all in files this change does not touch; `docs/operations/verifier-mutation.md`, this receipt and `items/282/state.md` each report no issues. Baseline before the change was also 8.

## Cold review

- Mode: comprehensive, round 1 of at most 2 comprehensive passes. Cumulative focused rounds: 0.
- Input revision: commit `0ffcd88` plus the staged receipt, state file and this section. Comparison: `origin/main` at `b680b8d`.
- Specification sources: the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/282#issuecomment-6557763377) (file changes 1 to 5, acceptance), its [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/282#issuecomment-6557949954) P2 applied, [decision 6557346336](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/282#issuecomment-6557346336), the issue body and [split decision 6556141000](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6556141000).

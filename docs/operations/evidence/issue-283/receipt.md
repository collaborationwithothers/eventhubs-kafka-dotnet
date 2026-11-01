# Issue 283 receipt

This records the tests added for the 103 shard p5 and p6 survivors of C5 run 41232286130 and the disposition of each. It claims no C5 score, because the replay is diagnostic and the next complete hosted C5 run on `main` measures the target.

## Execution card

- Deliverable: file changes 1 to 5 of the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/283#issuecomment-6557767125): the test file, `replay.py`, the disposition section in docs/operations/verifier-mutation.md, this receipt and `items/283/state.md`.
- Authorization: plan `Revision 2` (`updated_at` 2026-11-01T13:10:10Z) approved by [comment 6558820630](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/283#issuecomment-6558820630) after [the plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/283#issuecomment-6558791096), whose three `apply in implementation` P2s are applied (state file names Revision 2; two EQ3 citations corrected; acceptance diffs use `--merge-base`). Scope from [split decision 6556141000](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6556141000) as restated by the issue body. [Decision 6557346529](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/283#issuecomment-6557346529) adopts [#277 decision 6557335536](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6557335536): nothing changes under `results/`.
- Mandatory gates: the acceptance commands below, the cold review under AGENTS.md "Before opening a pull request" rule 8, hosted CI, Codex review and Hari's merge, because `tests/Parchwell.Verifier.Tests/` is a protected path (`.github/CODEOWNERS` line 16).
- Publication checkpoint: open the pull request after the first cold review with no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: its review-budget handoff to Hari.

## Predicate map

Predicate map: no verifier predicate changes. The table in [Issue 283 survivor dispositions](../../verifier-mutation.md#issue-283-survivor-dispositions) maps each surviving mutant to the test that kills it or to its equivalence proof.

## Commands and results

Branch cut from `main` at `2e285ac464139488d671190c39e80896b1b49fc4`, SDK 10.0.402, installed with `dotnet-install.sh --version 10.0.402`. `D` is a directory outside the repository. Each entry names the commit it ran on.

1. Commit `3a251fd` (file changes 1 to 3), before the merge of `origin/main` at `3086b06`.
   - `ParchwellVerifierMutationOnly=true dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release -- --filter-class '*ProbeV2AccessEvidenceAuditTests'` printed `total: 56`, `failed: 0`, `succeeded: 56`.
2. Commit `11b9d56` (merge of `origin/main` at `3086b06`, which brought in the merged #282 pull request; one textual conflict in docs/operations/verifier-mutation.md, resolved by keeping the issue 282 section before the issue 283 section).
   - `git diff --quiet 2dc9715e3de176f7e1cd55eee9c251e43c29c746 origin/main -- src/Parchwell.Verifier && git diff --quiet --diff-filter=MDR 2dc9715e3de176f7e1cd55eee9c251e43c29c746 origin/main -- tests/Parchwell.Verifier.Tests && echo unchanged` printed `unchanged`.
   - The full `dotnet test` command (same as above without the filter) printed `total: 4156`, `failed: 0`, `succeeded: 4156`. The plan's baseline total is 4067; the 89 cases of #282 account for the difference.
   - `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/<id>/zip > "$D/<shard>.zip"` then `sha256sum` printed `34dd9c1ba2f720c65afe687e4be3647a2beea91da62343bd532dd11cde0638ab` (p5, artifact 13056620559) and `42bd09145839fa70c039a8f4dec2d72a77904b06d0cfd43d4c8fbc8d84660751` (p6, artifact 13054686889). Both equal the plan.
   - `for k in 0 1 2; do python3 docs/operations/evidence/issue-283/replay.py run "$D/p5.json" "$D/p6.json" "$D/out$k.jsonl" $k/3 & done; wait` ran at `HEAD` `11b9d56`, with the working tree otherwise unchanged. Wall time 13:52:21Z to 14:23:30Z, 31.2 minutes, on 4 CPUs. The three output files hold 103 records: 97 `killed`, 6 `survived`.
3. Commit `c25ddf4` (state file added; the tree `check` read had the table of `11b9d56`, identical here).
   - `python3 docs/operations/evidence/issue-283/replay.py check "$D"/out*.jsonl` printed `rows 103 results 103 problems 0` and exited 0. No rerun was made and no disposition was changed.
   - `git diff --check --merge-base origin/main` exited 0 with no output. `git diff --name-only --merge-base origin/main`, with this receipt staged, lists exactly the plan's five files; without it, the other four.
   - `npm ci --ignore-scripts && npm run check:docs` reported `9 warnings` before this receipt existed: the 8 baseline warnings (`docs/agents/factory.md` 2, `docs/operations/evidence/issue-124/README.md` 1, `docs/operations/evidence/issue-140/README.md` 2, `docs/operations/evidence/issue-211/receipt.md` 3) and one for the link to this receipt. The 8 are in files this change does not touch.
4. `./scripts/verify-dotnet.sh` was not run: this cloud session has no Docker daemon. The hosted `dotnet-verification` run on the pull request is the evidence.

## Cold review

- Round 1: comprehensive, 1 of at most 2 comprehensive passes; cumulative focused rounds at its start: 0. Input revision: `c25ddf4` plus this receipt and the staged changes in the review diff. Comparison: `origin/main` at `3086b06`.
- Specification sources: the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/283#issuecomment-6557767125) (file changes 1 to 5, acceptance), its [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/283#issuecomment-6558791096) P2s as applied, [approval 6558820630](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/283#issuecomment-6558820630), [decision 6557346529](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/283#issuecomment-6557346529), the issue body and [split decision 6556141000](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6556141000).

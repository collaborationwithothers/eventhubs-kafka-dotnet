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

Branch cut from `main` at `0db2375a23605ea2ce9c7d6185886d7436d51d00`, SDK 10.0.402, installed with `dotnet-install.sh --version 10.0.402`. Commits are on the branch `claude/issue-282-c5-item-judgement`. `D` is a directory outside the repository. The acceptance commands are those of the plan's "Acceptance commands and required evidence"; each entry below names the commit it ran on.

1. Commit `8d9f911` (file changes 1 and 2).
   - `git diff --quiet 2dc9715e3de176f7e1cd55eee9c251e43c29c746 HEAD -- src/Parchwell.Verifier && echo unchanged` printed `unchanged`.
   - `ParchwellVerifierMutationOnly=true dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release` printed `total: 4064`, `failed: 0`, `succeeded: 4064`.
   - `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/13056294399/zip > "$D/p4.zip"`, then `sha256sum "$D/p4.zip"` printed `698ee772ab75425a57661b5255f92c7acd569aed05600b22c2ac670226784895`, and `unzip -p "$D/p4.zip" stryker/reports/mutation-report.json > "$D/p4.json"`, then `sha256sum "$D/p4.json"` printed `682a5d7b2f0b46ad10dd1652dcf1edde00fd7b3956ff4c20425543f769c3cc08`. Both equal the plan.
   - `for k in 0 1 2; do python3 docs/operations/evidence/issue-282/replay.py run "$D/p4.json" "$D/out$k.jsonl" $k/3 & done; wait` ran at `HEAD` `8d9f911`. Wall time 12:34:04Z to 13:04:34Z, 30.5 minutes, on 4 CPUs. The three output files hold 94 records in all.
2. Commit `5af0542` (file changes 3 and 5 added; the tree `check` read was this commit's).
   - `python3 docs/operations/evidence/issue-282/replay.py check "$D"/out*.jsonl` printed `rows 94 results 94 problems 0` and exited 0. No timing exception was needed and no rerun was made.
3. Commit `0ffcd88` (`git fetch origin main && git merge origin/main`; one textual conflict in docs/operations/verifier-mutation.md, resolved by keeping the issue 281 section before the issue 282 section).
   - The same `dotnet test` command printed `total: 4100`, `failed: 0`, `succeeded: 4100`.
   - `main` at `2e285ac`, in a separate worktree with the same command, printed `total: 4011`, `failed: 0`. The branch total is 89 above it.
   - `git diff --name-only origin/main` listed exactly the five files of the plan. `git diff --check origin/main` exited 0 with no output.
   - `npm ci --ignore-scripts && npm run check:docs` on this commit plus the then-uncommitted receipt: `8 warnings`, all in files this change does not touch (`docs/agents/factory.md` 2, `docs/operations/evidence/issue-124/README.md` 1, `docs/operations/evidence/issue-140/README.md` 2, `docs/operations/evidence/issue-211/receipt.md` 3). `docs/operations/verifier-mutation.md`, this receipt and `items/282/state.md` each reported no issues. The baseline was also 8.
4. `./scripts/verify-dotnet.sh` was not run: this cloud session has no Docker daemon. The hosted `dotnet-verification` run on the pull request is the evidence.
5. After `0ffcd88`, `origin/main` moved to `2e285ac`; the merge of it (`aaf68ec`) brought in only another item's verdict files and a README change, so no test command was repeated.

## Cold review

- Mode: comprehensive, round 1 of at most 2 comprehensive passes. Cumulative focused rounds: 0.
- Input revision: commit `aaf68ec` (the merge of `origin/main` into the branch). Comparison: `origin/main` at `2e285ac`. The merge brought in only verdict files for another item and a README change, so the test and replay results above still hold.
- Specification sources: the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/282#issuecomment-6557763377) (file changes 1 to 5, acceptance), its [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/282#issuecomment-6557949954) P2 applied, [decision 6557346336](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/282#issuecomment-6557346336), the issue body and [split decision 6556141000](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6556141000).
- Round 1 result (comprehensive, reviewed tree `c045bb8`): complete report, one P1. The receipt's "Commands and results" named neither the exact commands nor the source commit of each, and its figure 4011 for `main` was derived by subtraction instead of measured. Disposition: fixed in the commit after `c045bb8`, which lists each command with its commit and records a measured `total: 4011` for `main` at `2e285ac`. No P0, no other P1, no P2 beyond pre-existing warnings.
- Round 2 (focused, cumulative: 1 comprehensive, 1 focused): reviews the change since `c045bb8`, supplied with the full pull request diff as context. Input revision: the branch commit after `c045bb8`. Comparison revision: `c045bb8`. Specification sources as above.

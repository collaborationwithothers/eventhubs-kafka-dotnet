# Issue 281 receipt

This receipt records the tests added for the 63 shard p3 survivors of C5 run 41232286130 and the disposition of each. It claims no C5 score, because the replay is diagnostic and the next complete hosted C5 run on `main` measures the target.

Source commit for the commands below: `6af54492ea699719bbb93525c6379e7501cc362e` (the tests, `replay.py` and the dispositions section) on `origin/main` `b358c58`. The replay tested exactly that commit. The branch was then rebased onto `origin/main` `f231fa6`, which leaves `src/Parchwell.Verifier` and `tests/Parchwell.Verifier.Tests` unchanged since `2dc9715e3de176f7e1cd55eee9c251e43c29c746` (the `unchanged` check below). The rebased commit hashes differ.

## Execution card

- Deliverable: file changes 1 to 5 of the [c5-candidate-projection plan, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/281#issuecomment-6556794609).
- Authorization: that plan revision (`updated_at` 2026-11-01T10:35:00Z), approved by [approval 6557319182](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/281#issuecomment-6557319182). The plan check [6557170501](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/281#issuecomment-6557170501) states `Blocking findings: none`; its P2-1 and P2-2, marked `apply in implementation`, are applied in the dispositions section. [Decision 6557346129](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/281#issuecomment-6557346129) adopts [decision 6557335536 on #277](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6557335536): this pull request changes nothing under `results/`. The split decision [6556141000](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6556141000) applies through the issue body.
- Mandatory gates: the acceptance commands below; the cold review under AGENTS.md "Before opening a pull request" rule 8; hosted CI; Codex review; Hari's merge, because `tests/Parchwell.Verifier.Tests/` is a protected path (`.github/CODEOWNERS` line `Parchwell.Verifier*/`).
- Publication checkpoint: open the pull request after the first cold review with no P0 or P1.
- Review budget: rule 8 as written; exhaustion action is its review-budget handoff to Hari.

## Predicate map

Predicate map: no verifier predicate changes. The table in [Issue 281 survivor dispositions](../../verifier-mutation.md#issue-281-survivor-dispositions) maps each surviving mutant to the test that kills it or to its equivalence proof.

## Commands and results

Run from the repository root with SDK 10.0.402 on 2026-11-01. The acceptance commands are those of the plan's "Acceptance commands and required evidence" block. Files 1 and 2 were committed before `run`, as that block requires. `D` is `/tmp/r281`.

| Command | Literal result |
| --- | --- |
| `git diff --quiet 2dc9715e3de176f7e1cd55eee9c251e43c29c746 origin/main -- src/Parchwell.Verifier tests/Parchwell.Verifier.Tests && echo unchanged` | `unchanged` |
| `ParchwellVerifierMutationOnly=true dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release` | `total: 4011`, `failed: 0`, `succeeded: 4011`, `skipped: 0` |
| `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/13056428027/zip > "$D/p3.zip"; sha256sum "$D/p3.zip"` | `f0168953c73bd791b2b68a8cef0b7c175dcfd6d28826b67c746315e7e7cd88e1  /tmp/r281/p3.zip` |
| `unzip -p "$D/p3.zip" stryker/reports/mutation-report.json > "$D/p3.json"; sha256sum "$D/p3.json"` | `2ff2f83ba833de08d4652fe83f24ef4041554b30e689bd2e47864d777c76ff8b  /tmp/r281/p3.json` |
| `for k in 0 1 2; do python3 docs/operations/evidence/issue-281/replay.py run "$D/p3.json" "$D/out$k.jsonl" $k/3 & done; wait` | 63 records in `out0.jsonl` to `out2.jsonl`: `killed` 61, `survived` 2 (`11923`, `12277`). Wall time 1043 s, from 11:37:36Z to 11:54:59Z, on a 4-CPU container. |
| `python3 docs/operations/evidence/issue-281/replay.py check "$D"/out*.jsonl` | `rows 63 results 63 problems 0`, exit 0 |
| `git add -N docs/operations/evidence/issue-281 items/281 && git diff --name-only origin/main` | `docs/operations/evidence/issue-281/receipt.md`, `docs/operations/evidence/issue-281/replay.py`, `docs/operations/verifier-mutation.md`, `items/281/state.md`, `tests/Parchwell.Verifier.Tests/SingleAction/ProbeV2CandidateProjectionAuditTests.cs` |
| `git diff --check origin/main` | exit 0, no output |
| `npm ci --ignore-scripts && npm run check:docs` | `8 warnings`, none in a changed file: `docs/agents/factory.md` 2, `docs/operations/evidence/issue-124/README.md` 1, `docs/operations/evidence/issue-140/README.md` 2, `docs/operations/evidence/issue-211/receipt.md` 3 |
| `./scripts/verify-dotnet.sh` | not run: Docker is not running in this session. The hosted `dotnet-verification` run on the pull request is the evidence. |

Correction (issue 312): the rows for mutants 11986 and 11988 in the replay above replayed different expressions from the ones Stryker measured, because the printed replacement was inserted with outer parentheses only. [The issue 312 receipt](../issue-312/receipt.md) records their replay with the syntax-tree grouping.

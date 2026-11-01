# Issue 312 receipt

This receipt records a replay of C5 mutants 11986 and 11988 with the inner grouping of their syntax tree. The issue 281 replay inserted Stryker's printed replacement with outer parentheses only, so C# parsed both as different expressions from the ones Stryker measured. It claims no C5 score, because the replay is diagnostic.

## Execution card

- Deliverable: file changes 1 to 4 of the [c5-replay-grouping plan, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/312#issuecomment-6562947711).
- Authorization: standing approval: plan check https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/312#issuecomment-6562989534, `Blocking findings: none`, open questions `None`. Later decision: [6562872500](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/312#issuecomment-6562872500): no evidence pack note when neither disposition changes. The plan check's P2-1 (`apply in implementation`) is applied in `items/312/state.md`.
- Mandatory gates: the acceptance commands below, the cold review under AGENTS.md "Before opening a pull request" rule 8, hosted CI and Codex review. No protected path is touched.
- Publication checkpoint: open the pull request after the first cold review with no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: its review-budget handoff to Hari.

## Predicate map

Predicate map: not applicable. No verifier predicate or test changes.

## Commands and results

Branch `claude/issue-312-c5-replay-grouping`, cut from `main` at `002c3f8`. File change 1 is commit `922a6c7f19b570a6d2e04b67b44eca1306911e36`; the replay ran that commit's `replay.py`. The replayed tree is `40ba3b81310e1de1004b6838eecf096dc85ff487`, the merge of issue 281, which holds the tests from before issue 281 and the issue 281 tests and no later test. SDK `10.0.402`, on 2026-11-01. `D` and `W` are directories outside the repository.

| Command | Literal result |
| --- | --- |
| 1. `git merge-base --is-ancestor 40ba3b81310e1de1004b6838eecf096dc85ff487 origin/main && echo ancestor` | `ancestor` |
| 2. `git diff --quiet 40ba3b8 origin/main -- src/Parchwell.Verifier global.json Directory.Build.props Directory.Packages.props tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj && echo same; git diff --name-only 40ba3b8 origin/main -- tests/Parchwell.Verifier.Tests` (full hash as in the plan) | `same`, then `tests/Parchwell.Verifier.Tests/SingleAction/ProbeV2AccessEvidenceAuditTests.cs` and `tests/Parchwell.Verifier.Tests/SingleAction/ProbeV2ItemJudgementAuditTests.cs` |
| 3. Download artifact 13056428027, `sha256sum` of the zip and of `stryker/reports/mutation-report.json` | `f0168953c73bd791b2b68a8cef0b7c175dcfd6d28826b67c746315e7e7cd88e1` and `2ff2f83ba833de08d4652fe83f24ef4041554b30e689bd2e47864d777c76ff8b` |
| 4. `python3 -I $D/lines.py docs/operations/evidence/issue-281/replay.py $D/p3.json` | `11986 ok`, `11988 ok`, `overrides ['11986', '11988']`, exit 0 |
| 5. `git status --porcelain -- <replay.py>; git log -1 --format=%H -- <replay.py>` | no status line, then `922a6c7f19b570a6d2e04b67b44eca1306911e36` |
| 6. `dotnet --version`; `git worktree add --detach $W 40ba3b8...` | `10.0.402`; `HEAD is now at 40ba3b8 Merge pull request #295 from collaborationwithothers/claude/issue-281-c5-candidate-projection` |
| 7. `ParchwellVerifierMutationOnly=true dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release` in `$W` | `Passed!`, `total: 4011`, `failed: 0`, `succeeded: 4011`, `skipped: 0`; wall time 1m6.886s |
| 8. `python3 $R/docs/operations/evidence/issue-281/replay.py run $D/p3.json $D/out.jsonl 0/1 11986 11988` in `$W` | the `NOTICE` line, `11986 killed 1`, `11988 killed 1`, exit 0; wall time 1m27.613s |
| 9. `python3 -I $D/classify.py $D/out.jsonl` | `11986 killed O1 CP4`, `11988 killed O1 CP4`, exit 0 |
| 10. `git worktree remove --force $W` | `removed` (`--force` because the build left untracked output in the worktree) |
| 11. Scope, whitespace, ASCII and docs checks after file changes 2 to 4 | `git diff --name-only origin/main` lists exactly the four files `docs/operations/evidence/issue-281/receipt.md`, `docs/operations/evidence/issue-281/replay.py`, `docs/operations/evidence/issue-312/receipt.md` and `items/312/state.md`; `git diff --check origin/main` exits 0 with no output; the non-ASCII grep prints nothing; `npm ci --ignore-scripts && npm run check:docs` reports 8 warnings, all in files this pull request does not change (`docs/agents/factory.md`, `docs/operations/evidence/issue-124/README.md`, `docs/operations/evidence/issue-140/README.md`, `docs/operations/evidence/issue-211/receipt.md`), none in the four files above |

## Result

- 11986: killed, failed test alias CP4, outcome O1.
- 11988: killed, failed test alias CP4, outcome O1.

Rows 11986 and 11988 in [Issue 281 survivor dispositions](../../verifier-mutation.md) are unchanged: `missing test`, CP4. The scripted counts of that section and the evidence packs under `results/` are unchanged, as decision 6562872500 provides.

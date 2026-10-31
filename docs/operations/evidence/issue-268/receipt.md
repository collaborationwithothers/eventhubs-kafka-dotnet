# Issue 268 survivor sort: receipt

This receipt records the tests added for the 149 C5 survivors of run 41183628251 and the disposition given to each survivor. It claims no C5 score: the replay below is diagnostic evidence, and the next complete hosted C5 run on `main` measures these targets.

## Execution card

- Deliverable: file changes 1 to 8 of the [c5-survivor-sort implementation-grade plan, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/268#issuecomment-6544994859) (`updated_at` 2026-10-31T14:54:07Z), plus the item state file `items/268/state.md` under docs/agents/factory.md "Item state file".
- Authorization: that plan and [Hari's approval 6546005921](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/268#issuecomment-6546005921), posted after the plan's latest edit. Later decisions recognized under docs/agents/factory.md "Item lifecycle" step 3, in creation order:
  - [Decision 6545451062](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/268#issuecomment-6545451062): review cap 149 with `record the gap and continue`; no wait for #267; one pull request; "killed on replay" accepted for the five R1 mutants.
  - [Decision 6546002143](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/268#issuecomment-6546002143): mutants 14286 and 14288 stay "equivalent", with the inferred step stated.
  - The two P2 findings marked `apply in implementation` in [the plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/268#issuecomment-6545984900), applied with its exact text: the "Equivalence proofs" line in docs/operations/verifier-mutation.md, and the "Predicate map" line below.
- Branch: `claude/issue-268-c5-survivor-sort`, as the plan names it. Hari chose it over the cloud session's assigned branch in the implementing session on 2026-10-31.
- Mandatory gates: the acceptance commands below, the cold review under AGENTS.md "Before opening a pull request" rule 8, hosted CI, Codex review, and Hari's merge, because `tests/Parchwell.Verifier.Tests/` is a protected path.
- Publication checkpoint: open a draft pull request after the first cold review with no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: rule 8's review-budget handoff to Hari.

## Predicate map

Predicate map: no verifier predicate changes. The table in [Issue 268 survivor dispositions](../../verifier-mutation.md#issue-268-survivor-dispositions) maps each surviving mutant to the test that kills it or to its equivalence proof.

## Implementation notes

- File change 7 names "the three alias bullets above", but the plan's "Test aliases used in the table" list has four bullets (PJ1 to PJ4; PA1; A1; SA1 to SA14 and SB1). All four are copied, because the table cites every alias.
- File changes 1 to 6 were inserted by script from the plan comment's code blocks, byte for byte, immediately before each named anchor. Each anchor occurs once in its file on the source commit.

## Commands and results

Source commit: `2cc756be989b1f6352446c3e1c52dd47d4e6cef1` (`main`). Its diff from the plan's baseline `b79acb256d09ea29e493acc460ebbe2c9c59aa4f` touches no file under `src/Parchwell.Verifier`, `tests/Parchwell.Verifier.Tests`, docs/operations/verifier-mutation.md or docs/operations/evidence/issue-268. Tests and replay ran on commit `99a540a`, which holds file changes 1 to 7. SDK 10.0.402, 4-CPU Linux container.

| Command | Result |
| --- | --- |
| `ParchwellVerifierMutationOnly=true dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release` | `total: 3975`, `failed: 0`, `succeeded: 3975`, `skipped: 0`. |
| `./scripts/verify-dotnet.sh` | Not run: Docker is not available in this container. The hosted `dotnet-verification` run on the pull request is the evidence, as the plan allows. |
| Artifact download loop (plan "Acceptance commands") | Four reports. SHA-256 `51c2b2bf4c26262cabd2f0da5c9f8db910f6b84c614d3d85b3ec2a5f8e4c2be7` (phase), `262ffb95e6a1a1f8362632d4b258b69354ff63013957ac47b2aab3a5fa241ac2` (publication), `86184deea45521b85e3f16a50c42f6ae463c6fb9be3b9aa21cd0c79a3c3eb967` (a01), `9056569722f55b72f3f58ff8ffc6c51ffe5cb84f5e803b9e3a612a6a54ff22eb` (repeat), equal to the values pinned in `replay.py`. |
| `replay.py run` with three workers, then `replay.py check` | `check` printed `rows 149 results 149 problems 0` and exited 0. Wall time 39.4 minutes, 16:26:13Z to 17:05:36Z on 2026-10-31. |
| `git diff --check origin/main` | Exit 0, no output. |
| `npm run check:docs` | 8 warnings, none in a file this change touches. Before the receipt existed, docs/operations/verifier-mutation.md had one more, `Cannot find file evidence/issue-268/receipt.md`, which this file clears. |

No Stryker command ran and no workflow was dispatched.

## Replay

Replay statuses, counted by script against the disposition table:

| Disposition | Rows | Replay status |
| --- | ---: | --- |
| missing test | 119 | 119 killed, each by its named new test and by no existing test |
| equivalent | 25 | 24 survived; 1 compile error (14288, E11) |
| killed on replay | 5 | 5 killed by existing tests |

Totals: 124 killed, 24 survived, 1 compile error, as the plan states. The output files stay outside the repository. Their SHA-256 values:

- `out0.jsonl` `9c5118f6fbee1d34161448a12f5e7640b8ec92dbf36bc7d474636b9c3960effe`
- `out1.jsonl` `affab82506057a2bd22f88ff7bc845b9e53074c23df6233c36a0277dc94be2bd`
- `out2.jsonl` `cc8bb9fe378603e0824c7223ba5e9697ce76603d80ce6810c50b3d5412ec2b66`

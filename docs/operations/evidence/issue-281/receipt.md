# Issue 281 receipt

This receipt records the tests added for the 63 shard p3 survivors of C5 run 41232286130 and the disposition of each. It claims no C5 score, because the replay is diagnostic and the next complete hosted C5 run on `main` measures the target.

Source commit for the commands below: `6af54492ea699719bbb93525c6379e7501cc362e` (the tests, `replay.py` and the dispositions section) on `origin/main` `b358c58`. The replay tested exactly that commit.

## Execution card

- Deliverable: file changes 1 to 5 of the [c5-candidate-projection plan, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/281#issuecomment-6556794609).
- Authorization: that plan revision (`updated_at` 2026-11-01T10:35:00Z), approved by [approval 6557319182](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/281#issuecomment-6557319182). The plan check [6557170501](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/281#issuecomment-6557170501) states `Blocking findings: none`; its P2-1 and P2-2, marked `apply in implementation`, are applied in the dispositions section. [Decision 6557346129](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/281#issuecomment-6557346129) adopts [decision 6557335536 on #277](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6557335536): this pull request changes nothing under `results/`. The split decision [6556141000](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/277#issuecomment-6556141000) applies through the issue body.
- Mandatory gates: the acceptance commands below; the cold review under AGENTS.md "Before opening a pull request" rule 8; hosted CI; Codex review; Hari's merge, because `tests/Parchwell.Verifier.Tests/` is a protected path (`.github/CODEOWNERS` line `Parchwell.Verifier*/`).
- Publication checkpoint: open the pull request after the first cold review with no P0 or P1.
- Review budget: rule 8 as written; exhaustion action is its review-budget handoff to Hari.

## Predicate map

Predicate map: no verifier predicate changes. The table in [Issue 281 survivor dispositions](../../verifier-mutation.md#issue-281-survivor-dispositions) maps each surviving mutant to the test that kills it or to its equivalence proof.

## Commands and results

Run from the repository root with SDK 10.0.402 on 2026-11-01.

| Command | Literal result |
| --- | --- |
| `git diff --quiet 2dc9715e3de176f7e1cd55eee9c251e43c29c746 origin/main -- src/Parchwell.Verifier tests/Parchwell.Verifier.Tests && echo unchanged` | `unchanged` |
| `ParchwellVerifierMutationOnly=true dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release` | `total: 4011`, `failed: 0`, `succeeded: 4011`, `skipped: 0` |
| `sha256sum` of the artifact ZIP 13056428027 | `f0168953c73bd791b2b68a8cef0b7c175dcfd6d28826b67c746315e7e7cd88e1` |
| `sha256sum` of `mutation-report.json` | `2ff2f83ba833de08d4652fe83f24ef4041554b30e689bd2e47864d777c76ff8b` |
| `replay.py run` (3 workers) | 63 records: 61 killed, 2 survived (11923 and 12277). Wall time 1043 s, from 11:37:36Z to 11:54:59Z, on a 4-CPU container. |
| `replay.py check` | `rows 63 results 63 problems 0`, exit 0 |
| `git diff --name-only origin/main` (after `git add -N`) | the five files the plan lists |
| `git diff --check origin/main` | exit 0, no output |
| `npm run check:docs` | `8 warnings`, none in a changed file: the baseline set (`docs/agents/factory.md` 2, `issue-124/README.md` 1, `issue-140/README.md` 2, `issue-211/receipt.md` 3) |
| `./scripts/verify-dotnet.sh` | not run: Docker is not running in this session. The hosted `dotnet-verification` run on the pull request is the evidence. |

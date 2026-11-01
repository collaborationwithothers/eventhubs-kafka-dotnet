# Issue 275 receipt

This receipt records the gate update of eight evidence packs and the dispositions of the 30 survivors and the one Timeout of C5 run 41232286130. It changes no verdict and claims no new C5 measurement.

Source commit for the commands below: `a3b9e1088b21d791f8ebd2c1996959f6da2f3443` (`origin/main`) plus the working tree of this branch.

## Execution card

- Deliverable: file changes 1 to 6 of the [c5-gate-update plan, Revision 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/275#issuecomment-6549790804).
- Authorization: that plan revision (`updated_at` 2026-11-01T06:42:12Z), approved by [approval 6554913488](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/275#issuecomment-6554913488), whose wording change is applied in `packs.py`. The plan check [6554689147](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/275#issuecomment-6554689147) states `Blocking findings: none`; its P2 F1, marked `apply in implementation`, is applied in `packs.py` `BLOCK`. Decisions [6549570595](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/275#issuecomment-6549570595), [6550543272](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/275#issuecomment-6550543272), [6551231489](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/275#issuecomment-6551231489), [6554536413](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/275#issuecomment-6554536413) and the tool assignment [6554921033](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/275#issuecomment-6554921033).
- Mandatory gates: the acceptance commands below; the cold review under AGENTS.md "Before opening a pull request" rule 8; hosted CI; Codex review; Hari's merge (`results/` is protected, verdict pull requests go to Hari).
- Publication checkpoint: open the pull request after the first cold review with no P0 or P1.
- Review budget: rule 8 as written; exhaustion action is its review-budget handoff to Hari.

## Predicate map

Predicate map: not applicable; no verifier predicate changes.

## Commands and results

Run from the repository root with SDK 10.0.402. Artifacts were downloaded through the REST artifact route into a directory outside the repository.

| Command | Result |
| --- | --- |
| `git diff --quiet 2dc9715... origin/main -- src tests .github/workflows/verifier-c5-mutation.yml results` | `unchanged` |
| `sha256sum` of the nine ZIPs | equal to the plan's "Inputs" table for all nine |
| `summary.json` line | `2dc9715e3de176f7e1cd55eee9c251e43c29c746 41232286130 1 True [] True 3076 3366 13` |
| `replay.py map` | exit 0; 31 id lines; last line `problems 0` |
| `replay.py run` (3 workers) | 31 records |
| `replay.py check` | `rows 31 results 31 problems 0` |
| runs API, four verdict runs | `head_sha`, `main`, attempt 1 and `.github/workflows/scenario.yml` as the plan states |
| `packs.py report` | the eight lines of the plan, byte for byte (compared by script) |
| `packs.py apply`, then `verify` | `packs 8 problems 0` |
| `git diff --check origin/main` | no output |

## Identity and replay

- `map` last line: `problems 0`.
- Replay counts: see the `check` line above; the 4863 record is below.
- 4863 record: `{"failed_methods": ["Parchwell.Verifier.Tests.Judge.JudgeHandlerTests.Given_B04OptionsWithA01RepeatEvidence_When_Judged_Then_RejectsCrossScenarioEvidence"], "id": "4863", "phase_judge_out_of_memory": true, "status": "killed"}`. The failed method differs from the planning sessions' records; the plan allows this.
- SHA-256 of the replay output files, which stay outside the repository: `out0.jsonl` `cbbf2d243cda48abe497613cf12b876496c89cfd60c61f1d3b75c9012295f9a6`, `out1.jsonl` `9004d8b1d51e9348815bc94ca0a389bac5ae7c9b58f839f7ab608d2b21cd40b2`, `out2.jsonl` `fd5b846430096c19a50611eed900816acbb535dcb494aac6c581a463d5273010`.

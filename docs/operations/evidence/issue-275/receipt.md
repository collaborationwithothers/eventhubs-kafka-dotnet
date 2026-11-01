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

| Command | Literal result |
| --- | --- |
| `git diff --quiet 2dc9715e3de176f7e1cd55eee9c251e43c29c746 origin/main -- src tests .github/workflows/verifier-c5-mutation.yml results && echo unchanged` | `unchanged` |
| `sha256sum` of the nine ZIPs (ZIP: SHA-256) | old-phase `e5f7d5bbdedbcf4a4ccffa69aca1762324f831872702b190c5db52125ace4e47`; old-publication `abcde8f5e8f6c7e91779cd52ca6500bb328022740eb5bd7c424280cba392557f`; old-repeat `8c354bce572decdc97f42e3455648483204188a8140398a40f2d7f888f19b11b`; phase `30ee41906b695893da4e4ff6fe51c9ec8446c53b3ed565f6d02cde81374b4067`; publication `0958c1cbe3aa2b2b0b1e3fc6a4827689ca56253b423958c2ffb031164fe1a715`; repeat-p1 `d46046521cd7dec9fe61d62c14b19d97377734ae57a4858cbb3b9de303fa5fb4`; repeat-p2 `982718362f8adfd6a4ab9da3c5113642e5f0788f2f953d121f63310da190acb4`; repeat-p3 `24f7de948380c665a4d026ff99c9cbc3ecfe578af8fcd736acfeb6dc50da9500`; summary `5c3ee705059465c250fea44cfbb859c963a2a310e13bc5bb55739c984e4e8218` |
| `summary.json` line | `2dc9715e3de176f7e1cd55eee9c251e43c29c746 41232286130 1 True [] True 3076 3366 13` |
| `replay.py map` | exit 0; the 31 id lines and the last line are under "Literal replay output" |
| `replay.py run` (3 workers) | 31 records: 24 survived, 6 killed, 1 compile_error; per-mutant lines under "Literal replay output" |
| `replay.py check` | `rows 31 results 31 problems 0` |
| runs API, four verdict runs | `40227770436 fa417d783e833ad222ab5173978433448e4aefbe main 1 .github/workflows/scenario.yml`; `40315155000 cb83254f1c2645acd86fe734b24e542e0956edd3 main 1 .github/workflows/scenario.yml`; `40415641139 7b6834c5e8668e89376597bc2b524c0725326daf main 1 .github/workflows/scenario.yml`; `40722187672 20f17794330b60d77a75bf5788ee442cc8baa1f6 main 1 .github/workflows/scenario.yml` |
| `packs.py report` | the eight lines recorded in plan file change 4, equal byte for byte (compared by script: `True`) |
| `packs.py apply`, then `verify` | `packs 8 problems 0` |
| `git add -N docs/operations/evidence/issue-275 items/275 && git diff --name-only origin/main` | 13 names: the 8 packs, `docs/operations/verifier-mutation.md`, the three files under `docs/operations/evidence/issue-275/` and `items/275/state.md` |
| `git diff --check origin/main` | no output |
| `npm ci --ignore-scripts && npm run check:docs` | `8 warnings`, none in a changed file. Blanket line: the 8 baseline warnings are in unchanged files (`docs/agents/factory.md` 2, `docs/operations/evidence/issue-124/README.md` 1, `docs/operations/evidence/issue-140/README.md` 2, `docs/operations/evidence/issue-211/receipt.md` 3) |

## Identity and replay

- `map` last line: `problems 0`.
- Replay counts per disposition: 25 equivalent rows survived, except 14288, which was a compile_error as proof E11 expects (24 survived, 1 compile_error); 6 killed on replay (9917, 9919, 9922, 9924, 9926 and the Timeout 4863).
- 4863 record: `{"failed_methods": ["Parchwell.Verifier.Tests.Judge.JudgeHandlerTests.Given_B04OptionsWithA01RepeatEvidence_When_Judged_Then_RejectsCrossScenarioEvidence"], "id": "4863", "phase_judge_out_of_memory": true, "status": "killed"}`. The failed method differs from the planning sessions' records; the plan allows this.
- SHA-256 of the replay output files, which stay outside the repository: `out0.jsonl` `cbbf2d243cda48abe497613cf12b876496c89cfd60c61f1d3b75c9012295f9a6`, `out1.jsonl` `9004d8b1d51e9348815bc94ca0a389bac5ae7c9b58f839f7ab608d2b21cd40b2`, `out2.jsonl` `fd5b846430096c19a50611eed900816acbb535dcb494aac6c581a463d5273010`.

## Literal replay output

`replay.py map` (exit 0):

```text
4992 run 41183628251 status Survived
4994 run 41183628251 status Survived
4995 run 41183628251 status Survived
4998 run 41183628251 status Survived
5013 run 41183628251 status Survived
5014 run 41183628251 status Survived
5015 run 41183628251 status Survived
5016 run 41183628251 status Survived
5017 run 41183628251 status Survived
5018 run 41183628251 status Survived
5028 run 41183628251 status Survived
5030 run 41183628251 status Survived
5032 run 41183628251 status Survived
5034 run 41183628251 status Survived
5036 run 41183628251 status Survived
5038 run 41183628251 status Survived
9917 run 41183628251 status Killed
9919 run 41183628251 status Killed
9922 run 41183628251 status Survived
9924 run 41183628251 status Survived
9926 run 41183628251 status Survived
10008 run 41183628251 status Survived
13447 run 41183628251 status Survived
13530 run 41183628251 status Survived
13836 run 41183628251 status Survived
14123 run 41183628251 status Survived
14129 run 41183628251 status Survived
14193 run 41183628251 status Survived
14286 run 41183628251 status Survived
14288 run 41183628251 status Survived
4863 run 41183628251 status Timeout
problems 0
```

`replay.py run`, lines `id status failed-method-count`, sorted by id:

```text
4863 killed 1
4992 survived 0
4994 survived 0
4995 survived 0
4998 survived 0
5013 survived 0
5014 survived 0
5015 survived 0
5016 survived 0
5017 survived 0
5018 survived 0
5028 survived 0
5030 survived 0
5032 survived 0
5034 survived 0
5036 survived 0
5038 survived 0
9917 killed 37
9919 killed 37
9922 killed 37
9924 killed 37
9926 killed 37
10008 survived 0
13447 survived 0
13530 survived 0
13836 survived 0
14123 survived 0
14129 survived 0
14193 survived 0
14286 survived 0
14288 compile_error 0
```

## Amendment to file change 2

Hari approved this amendment in the implementing session on 2026-11-01 ("yes, do it"), after [Codex's P2 on `replay.py`](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/279#discussion_r4434836185). Plan Revision 4 pins `replay.py` byte for byte. The amendment changes one thing: `run` no longer compares `src` and `tests` with the measured commit. It checks that each of the three target files equals the `source` text recorded in the report, and stops with a message if one differs. No commit is named in the script, so a merge elsewhere in the repository does not break it. The acceptance output is unchanged.

Checked from the head that contains the #278 changes: with a line appended to `Judge/PhaseJudge.cs`, `run` stops with `['Judge/PhaseJudge.cs'] differ from the source the report measured; the mutant locations no longer apply`; with the file restored, `run ... 1/31` printed `4994 survived 0`, as disposition E1 expects. The 31-mutant replay recorded above ran before the amendment, at the plan baseline.

# Probe batch verdict publication receipt

This receipt records the evidence and checks behind the proposed verdicts for B01, B03, B05 and C12 from live run 40415641139. It records results only for the source commits and commands stated below. C08 and F04 are not published here.

## Authority and scope

The work follows [Hari's decision 6436044243](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/210#issuecomment-6436044243): one verdict pull request for B01, B03, B05 and C12, with C08 and F04 left out and followed up in [#221](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/221). [Decision 6436169472](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/210#issuecomment-6436169472) assigns this pull request to Claude Code. Both apply the [approved parent plan, Revision 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/210#issuecomment-6415356278) (`updated_at` `2026-10-26T18:09:30Z`, [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/210#issuecomment-6425470036)) and its decision rules, read with the earlier #210 decisions in creation order. No separate implementation-grade plan exists for this verdict pull request; the decision itself names its content.

The issue body and all 20 comments were read at the start of the session and again before the push. The comment IDs and `updated_at` values were equal by script, and the body's `updated_at` stayed `2026-10-27T07:24:06Z`.

C08 C1, F04 C2 and F04 C3 were incomplete in all five repeats, with reasons `p4_no_terminal_outcome` and `p6_broker_count`, plus `p2_completion` for F04 C3. The workflow did not accept those three candidates. C08 and F04 each need every configuration for a verdict, so neither has one. No rerun was dispatched, and no expected outcome, scenario or verdict rule was changed.

## Source and commands

The branch is `claude/issue-210-probe-batch-verdict`. Its base is `7b6834c5e8668e89376597bc2b524c0725326daf`, the head of `main` and the source of the run. Commits on this branch were also pushed as backups to `claude/issue-210-probe-batch-verdict-wip` under AGENTS.md "Before opening a pull request" rule 12. The first generation, the artifact checks, the scripted checks below and both `./scripts/verify-dotnet.sh` runs ran with `git rev-parse HEAD` reporting `7b6834c5e8668e89376597bc2b524c0725326daf` and the authored files uncommitted; the second .NET run finished after the backup commit `458616f`, whose tree is that base plus the authored files. The round 1 fixes were applied and checked with `HEAD` at `d6b488f` and committed as `155d416`. The round 2 fixes were applied with `HEAD` at `155d416` and committed as `01efa82`; the negative check ran on that working tree. The round 3 prose fixes were applied with `HEAD` at `01efa82`; the final generation, the `--check` run whose hashes are listed below and the last `npm run check:docs` ran on that working tree. Only the authored files differ from the base in every tree named here.

| Artifact ID | Name | ZIP SHA-256 |
| --- | --- | --- |
| 12715895816 | `probe-batch-candidates-40415641139-1` | `832f02676887a6eb42a210a08cbeebf49a83fbf11f6f43392b95a172059d83b3` |
| 12715517221 | `probe-batch-pods-40415641139-1` | `1adc429586a758b5790fc662cb89676ce8cdaf271f165b0e1f5e832023dcb39b` |
| 12705444729 | `c5-summary-40415248275-1` | `688f85cc82160d93027c506049c09ab2ec60a0b931fad5bafc68c701ea721c04` |

Each was downloaded with `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/<id>/zip`, exit 0 at the first attempt. Each ZIP SHA-256 equals the artifact API `digest`. The run's two artifacts expire at `2027-01-24T22:51:47Z`. Raw artifacts, job logs and run metadata stay outside git.

The candidate and judge-receipt members, and their SHA-256 values, are pinned in `PINNED` in [make_results.py](make_results.py). Commands and results:

- `python3 -I docs/operations/evidence/issue-210/make_results.py --candidates <candidates> --pods <pods>`: exit 0. It verified every pinned hash, every candidate field it relies on, each judge receipt and each pod's collection status, service account, exit code and log. It checked each `expected.yaml` against its pinned SHA-256, derived each item's hypotheses and verdict from the decision rules transcribed from that file into `derive()`, and found them equal to the verifier's `item_assessment`. It wrote the four `results/<ID>/2026-10-27.json` files.
- The same command with `--check`: exit 0. It printed SHA-256 `6fe94007b4dbf45b2dd24cde5dd57be733fab91ea084fa689f109b5bba698177` (B01), `3419736bc1b21e5539c69ecbf795a393235fceff5930be457cdf237ed5c62e46` (B03), `a352b738e73493dc9cf7b9cec9f84ca108ff24f94aa9d856924b2263f54fd5f3` (B05) and `d99cd8ca2dce0ab7c4a1531c4d9998e9721becfda60c1552e2be68ac86212758` (C12).
- Negative check: in a scratch copy of `scenarios/` and `results/` with one line appended to `scenarios/B05/expected.yaml`, `--check` exited 1 with `B05 expected.yaml differs from the one these decision rules were transcribed from`.
- The four report tables were generated by a script from those JSON files.
- A script measured the C12 `group.id` values in `scenarios/C12/scenario.yaml` at 256 and 257 characters.
- A script compared the `imageID` of all 18 in-scope pods with the candidates' digests: 9 loadgen pods, including both `probe` capture pods, and 9 verifier pods, all equal.

Sources read on 2026-10-27: the Microsoft [quotas page](https://learn.microsoft.com/en-us/azure/event-hubs/event-hubs-quotas), [troubleshooting guide](https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-troubleshooting-guide), [configuration page](https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations) and [transactions page](https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-transactions); and librdkafka v2.16.0 [rdkafka.h](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka.h) and [rdkafka.c](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka.c) for codes -192, 24 and 43, and [rdkafka_broker.c L630-L677](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c#L630-L677) for suppression of identical broker errors, [rdkafka_msg.c L242-L248](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_msg.c#L242-L248) for the produce-time size check, and [rdkafka_conf.c](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_conf.c) L4579-L4588, L4604-L4616 and L4772-L4781 for B05's construction constraints, and [STATISTICS.md L87](https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md?plain=1#L87) for the broker sources. Every quotation in the reports was compared with those pages.

## Mutation publication gate

C5 publication status: passed. [Run 40415248275](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40415248275), attempt 1, `workflow_dispatch` on `main`, conclusion `success`. Its `summary.json` (SHA-256 `1df2cb8061f8e09f00f65c478b6b8d9aab5e1aedbac9af9894558c035d420659`) names commit `7b6834c5e8668e89376597bc2b524c0725326daf` and tree `062fc243b27c0743c3d8868f1de33b6e2ad808a6`, which equals `git rev-parse 7b6834c^{tree}`. It records `measurement_complete: true`, no incomplete reasons, `break_passed: true` and 1302 / 1302 across the six shards in the committed `stryker-c5-config.json` `mutate` list. `survivors.json` lists no survivor. That commit is the merge of the pull request that changed the targets, and no target has changed since, because it is also this branch's base.

## Checks

- `npm ci --ignore-scripts`: exit 0.
- `npm run check:docs`: exit 0. No issue in the authored files. Blanket disposition: its 8 warnings are all in files this pull request does not change, the same 8 as on `main`.
- `./scripts/verify-dotnet.sh`, with .NET SDK 10.0.402 installed in the session: exit 2. Locked restore, solution build, Kafka XML inventory, architecture tests, the unit scaffold inventory and two test projects passed; the script then stopped at the first failing test project. It ran again from a temporary copy that changed one line, so that a failing unit-test project is recorded and the loop continues. That copy was removed afterwards and is not committed. The rerun also passed the warning-as-error control. Counts from each project's xunit report, by script:

| Project | Total | Passed | Failed |
| --- | ---: | ---: | ---: |
| `Parchwell.ArchitectureTests` | 12 | 12 | 0 |
| `Parchwell.Enrichment.Tests` | 55 | 55 | 0 |
| `Parchwell.Kafka.Tests` | 402 | 401 | 1 |
| `Parchwell.LoadGen.Tests` | 484 | 482 | 2 |
| `Parchwell.Scenarios.Tests` | 149 | 149 | 0 |
| `Parchwell.Verifier.Tests` | 3342 | 3330 | 12 |
| Total | 4444 | 4429 | 15 |

All 15 failures are component or integration tests that start a local Kafka through Testcontainers. Each failed in its fixture's Docker endpoint lookup (`ExperimentDockerAuthentication.get_Endpoint`) or with `DockerUnavailableException`, before any Kafka container started. Three of them report it through their per-trial `proof.json` files. The session container has no Docker daemon. This pull request's own CI is the authoritative check.
- Manual inspection against AGENTS.md hard safety rule 7: no secret, key, connection string, tenant ID or subscription ID in the authored files. The job logs mask subscription IDs; none was copied.

No Azure operation and no workflow dispatch was run. This pull request changes no code, scenario, expected outcome or contract.

## Cold review

Cold-review rounds and their dispositions are recorded here as each round completes. The final head's CI run and the last round are recorded in the pull request description under AGENTS.md "Before opening a pull request" rule 8.

Round 1 applied the `repo-review` rubric to the ten-file diff on `458616f`. The reviewer downloaded both run artifacts itself, reran `make_results.py --check` with exit 0, and checked the quotes, line citations, job-log lines and C5 summary. Its result line was `Findings: 10 at P0 or P1`. Dispositions:

- P0, all eight files under `results/`: new `results/` files outside a pull request labelled `verdict`. Kept under AGENTS.md "Before opening a pull request" rule 8, third paragraph, because the verdict step requires them. The pull request is labelled `verdict`, lists the finding, and leaves the merge to Hari.
- P1, B01 report: the statement that librdkafka folds identical errors had no citation. Fixed: it now cites [rdkafka_broker.c L630-L677](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c#L630-L677), checked in this session, and states the 30-second interval.
- P1, B05 report: the report never labelled the workload synthetic (hard safety rule 11). Fixed in its summary and in `SUMMARIES["B05"]`; the regenerated B05 JSON hash is above.
- P2, B05 report: the verdict rule was quoted without its H2 half. Fixed: the full rule is quoted.
- P2, all four reports: "unchanged" and "the receipt records their SHA-256 values" were inexact, because the embedded objects are re-serialized and the member hashes are in `PINNED`. Fixed in each report and in the script's docstring.

Round 1 was not clean, so rule 8's additional P2 round does not follow it.

Round 2 reviewed the diff at `155d416`. It downloaded the three artifacts itself, reran `--check` and rechecked every table row, citation, quote and job-log line. Its result line was `Findings: 10 at P0 or P1`. Dispositions:

- P0, all eight files under `results/`: the same finding as round 1, kept on the same grounds.
- P1, B01 report: the claim that raising `message.max.bytes` lets the event leave the client had no citation, the same class as round 1's P1. Fixed: it cites the baseline value and [rdkafka_msg.c L242-L248](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_msg.c#L242-L248), checked in this session. Every other librdkafka behaviour claim in the four reports was checked under rule 5; B05's construction constraints, which cited only the plan's fact F1, now cite rdkafka_conf.c directly.
- P1, this receipt: the source-commit statement did not cover the commits at which the round 1 fixes and their `--check` ran. Fixed in "Source and commands".
- P2, C12 report: "No Microsoft source names the error code" read as a universal negative. Fixed: it is limited to the four pages cited.
- P2, `make_results.py` and the published `derivation` field: they said the rules were re-derived from `expected.yaml`, which the script never read. Fixed: the script pins each `expected.yaml` SHA-256 and stops on a mismatch, as the negative check shows. The docstring, the `derivation` field and the four reports now say that the rules are transcribed.

Round 2 was not clean, so rule 8's additional P2 round does not follow it.

Round 3 reviewed the diff at `01efa82`. Its only P0 or P1 findings were the eight kept `results/` P0s, so it is the first clean round under rule 8. Its two P2s touch only prose in files already in the diff, so both were fixed:

- P2, all four reports: the broker count was described as all brokers in state UP, but verifier contract check 6 and `SingleActionRepeatJudgement.cs` L102 count only sources learned, configured and logical. Fixed: each report states the filter and that `internal` entries are excluded, citing STATISTICS.md L87.
- P2, B01 summary in the report and in `SUMMARIES["B01"]`: "resets by the broker" stated as observed what the log reports as "reset by peer". Fixed: the summary says "by the peer" and names the broker as the inferred cause. Only the B01 JSON hash changed.

Under rule 8, one more round followed that reports only P0 or P1.
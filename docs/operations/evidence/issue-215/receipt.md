# B04 verdict publication receipt

This receipt records the evidence and publication checks for the proposed B04 verdict from final run 40315155000. It records measured results only for the source commits and commands stated below.

## Source and commands

The branch is `claude/issue-215-implement-plan-k3gt1z`. Its base, `ISSUE215_BASE`, is `cb83254f1c2645acd86fe734b24e542e0956edd3`, the head of `main` when the branch was created. The step 2 to step 5 commands, the first `npm run check:docs`, the build and the first `./scripts/verify-dotnet.sh` ran with `git rev-parse HEAD` reporting that same SHA, plus the three authored files staged. The full test-suite rerun below and every later rerun of the validator, `npm run check:docs` and the diff checks ran with `git rev-parse HEAD` reporting a commit on this branch. The full test-suite rerun ran at `584943731a49be9d1c7deb58f4c87d38992502f3`, whose tree is that base plus the three authored files.

The work follows [plan Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/215#issuecomment-6421780147), `updated_at` `2026-10-26T17:31:36Z`. Its [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/215#issuecomment-6423136651), posted at `2026-10-26T17:40:51Z` by the check subagent under docs/agents/factory.md "Item lifecycle" step 3 "Standing approval", reports `Blocking findings: none`. Its one P2, marked `apply in implementation`, was applied: `mkdir -p results/B04 docs/operations/evidence/issue-215` ran from the repository root before the step 3 command.

The publication object, report template, report script and validator were extracted by script from the posted plan comment, without edits, to `/tmp/issue-215-publication.json`, `/tmp/issue-215-report-template.md`, `/tmp/issue-215-make-report.py` and `/tmp/issue-215-acceptance.py`. Commands and results:

- Step 2 acquisition commands, as written in the plan: each exited 0 on its first attempt. The five ZIP SHA-256 values, four in the "Final-run evidence" table and the C5 summary in "Mutation publication gate", match the artifact API digests.
- Step 3 generation command, as written in the plan: exited 0. `results/B04/2026-10-26.json` has SHA-256 `bb6e41cf76773c9088c467081caa2faee9b4eea7ff088420fb9c87ac9d686efb`, the plan's pinned value.
- `python3 -I /tmp/issue-215-make-report.py /tmp/issue-215-report-template.md`: exited 0 and printed `d00f53d0697a6f8366dd664680f8e76e8a8f0d926edc69043230b37503a5abfc`, the plan's pinned report hash.
- `python3 -I /tmp/issue-215-acceptance.py --evidence /tmp/issue-215-evidence --base "$ISSUE215_BASE"`: exited 0 and printed `b04 publication: pass`.
- `npm ci --ignore-scripts`: exited 0; it installed the locked documentation-check dependencies.
- `git add results/B04/2026-10-26.json results/B04/2026-10-26.md docs/operations/evidence/issue-215/receipt.md`: exited 0; it staged only the three planned files.
- `npm run check:docs`: exited 0; no issue in the three files, and the warnings below.
- `git diff --check` and `git diff --cached --check`: both exited 0 with no whitespace errors.

Hari's [decision of 2026-10-26](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/215#issuecomment-6423903531) amended the plan after round 3. This pull request also adds a note to `docs/agents/factory.md` "Item lifecycle" step 3 and records the amendment in its status line. Enforced check 7 now expects four changed files. The validator copy `/tmp/issue-215-acceptance-amended.py` differs from the plan's validator only in that `FILES` also lists `docs/agents/factory.md`. With factory.md staged and `git rev-parse HEAD` at `0792ab968a4f2ddcae216df3524cec47c0bc935b`, it exited 0 and printed `b04 publication: pass`. The unamended validator exited 1 on the fourth file, as expected. `npm run check:docs` then reported no issue in `docs/agents/factory.md` or the three publication files, and the same 3 warnings in unchanged files. The three publication files and their pinned hashes are unchanged.

Manual inspection of the three files against AGENTS.md hard safety rule 7 found no secret, key, connection string, tenant ID or subscription ID. This pull request changes no code. In this session, with .NET SDK 10.0.402, `dotnet build Parchwell.slnx -c Release` exited 0 with 0 warnings and 0 errors. `./scripts/verify-dotnet.sh` exited 2 because it stops at the first failing test project. So it ran again against this working tree, using a copy of the script that changed one line: a failing unit-test project is recorded and the loop continues. That run passed locked restore, solution build, Kafka XML inventory consistency, the unit scaffold inventory and the warning-as-error control. Test results by project, from each project's xunit report:

| Project | Total | Passed | Failed |
| --- | ---: | ---: | ---: |
| `Parchwell.ArchitectureTests` | 11 | 11 | 0 |
| `Parchwell.Kafka.Tests` | 360 | 359 | 1 |
| `Parchwell.LoadGen.Tests` | 371 | 369 | 2 |
| `Parchwell.Scenarios.Tests` | 117 | 117 | 0 |
| `Parchwell.Verifier.Tests` | 2747 | 2735 | 12 |
| Total | 3606 | 3591 | 15 |

`Parchwell.Enrichment.Tests` is an empty scaffold and claims no coverage. All 15 failures are component or integration tests that start a local Kafka through Testcontainers. Each failed in its fixture's Docker endpoint lookup, or with `DockerUnavailableException`, before any Kafka container started: the session container has no Docker daemon. The [.NET verification run 40314240017](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40314240017) succeeded on `cb83254`, and this pull request's own CI is the authoritative check. No Azure operation, B04 dispatch or C5 dispatch was run.

## Final-run evidence

The sole source is [run 40315155000](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40315155000), attempt 1, on `main` at `cb83254f1c2645acd86fe734b24e542e0956edd3`, adopted by the [final-run decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/215#issuecomment-6421289549).

| Artifact ID | Name | ZIP SHA-256 | Member SHA-256 |
| --- | --- | --- | --- |
| 12648552841 | `b04-C0-candidate-40315155000-1` | `0c64b498ab23d632611bd4eed5f4c3fb91b98a944c9a0e90d5055c0bcd5e1980` | `candidate.json` `bb2f0329a6f9712a962fe55dc21198742d3aa2caafa681933523ef7c9fa40b93` |
| 12655032300 | `b04-C1-candidate-40315155000-1` | `9902975b54ec01785eb17822111717091b20f66d99df026a097e4a27bb16ad05` | `candidate.json` `801f4bb5d9a9878376cd2f884bcc92d99770328271db1cb618b3882da468043a` |
| 12648832668 | `b04-C0-pods-40315155000-1` | `1817b1f3ee64ce5807bedcd386c01340eb56c73d4c51ae15c800af591c641cf3` | |
| 12654947747 | `b04-C1-pods-40315155000-1` | `46ae04317fa78912191bd88b7f96761dacc0b0066b0d393f15839c5f03a09685` | |

All four run artifacts have observed expiry `2027-01-24T11:27:05Z`. Raw artifacts, run and job metadata, job logs, the validator and the report script stay outside git.

## Mutation publication gate

C5 publication status: passed. Run 40315208371, attempt 1, source cb83254f1c2645acd86fe734b24e542e0956edd3, tree 45dfd22d1782244cf8592112c12c9810ca321ba5, raw score 555/555; complete measurement passes thresholds.break 60.

The summary is [artifact 12651523793](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40315208371/artifacts/12651523793), named `c5-summary-40315208371-1`. Its ZIP SHA-256 is `20d51237c280c886e58c6a39b985a55631dbb3038e11e541274492fa99f03560`, matching the artifact API digest, and its `summary.json` SHA-256 is `cbd444b66db3a29baba6659994fb27419440e14ee00d7ccd32db409e93b01188`. The summary tree equals `git rev-parse cb83254^{tree}`.

Full score: **100.0%**, computed as (408 Killed + 147 Timeout) / 555 * 100. Killed-only score: **73.5%**, computed as 408 / 555 * 100 and rounded to one decimal place. The killed-only score is supplemental and does not replace the adopted gate. The [#201 decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/201#issuecomment-6409664176) adopted the post-merge C5 requirement this run meets.

## Target identity

A script computed each target's SHA-256 from the bytes at `cb83254f1c2645acd86fe734b24e542e0956edd3` and at the publication source. The publication source is that same commit plus the three authored files, so each pair is equal.

| Target | Source SHA-256 at cb83254 and at the publication source |
| --- | --- |
| `src/Parchwell.Verifier/Judge/PhaseJudge.cs` | `73c63607cf278eaf38306863760072d14eb2e5754a234d8426d6df40b9c241ef` |
| `src/Parchwell.Verifier/VerifyRun/Publication/PublicationAcceptance.cs` | `8363cdebab24d14e3b65deb3d6147fee71dad6d0bebbc1b1e5332303aa94eb38` |
| `src/Parchwell.Verifier/VerifyRun/A01/A01EvidenceRecorder.cs` | `d01f8995692a84cd8bfd908b9874bd72eba08d2f737529c05c1a5d8973780806` |
| `src/Parchwell.Verifier/VerifyRun/A01/WorkloadRecorder.cs` | `065cde4de7cd27b41946b665121589f9e5b333f0d6c4814346fd4a2cc082f808` |

The summary holds mutation-report hashes, not these source-file hashes.

## Documentation and cold review

`npm run check:docs` reported no issue in the three authored files. Blanket disposition: its 3 warnings are all in files this pull request does not change, and they stay out of its scope.

Cold-review rounds are recorded below for the source they reviewed. The final head's CI run and the last cold-review round are recorded in the pull request description under AGENTS.md "Before opening a pull request" rule 8; this receipt need not describe its own head commit.

Round 1 applied the `repo-review` rubric to the three-file diff on `cb83254f1c2645acd86fe734b24e542e0956edd3`. It reran the validator and confirmed both pinned hashes, the four target hashes, the summary tree, the cited verifier lines and the external quotes. Its result line was `Findings: 2 at P0 or P1`. Dispositions:

- P0, both files under `results/B04/`: a new `results/` file outside a pull request labelled `verdict`. Kept under AGENTS.md "Before opening a pull request" rule 8, third paragraph, because the verdict step requires the files. The pull request is labelled `verdict`, lists the finding, and leaves the merge to Hari.
- P1, this receipt: the full test suite had not run locally, because `./scripts/verify-dotnet.sh` stopped at the first failing project. Fixed: the suite ran past that point, and the table above records every project.
- P2, this receipt: the cold-review record was missing. Fixed by this paragraph.
- P2, this receipt: "the five ZIP SHA-256 values match the table below" pointed at a four-row table. Fixed: the sentence now names where each of the five values is.

Round 1 was not clean, so rule 8's additional P2 round did not follow it. Round 2 reviewed the diff at `c0c716ce76308201021604e85079bdf768a2a07e`. It confirmed both pinned hashes, the test table against each project's xunit report, the cited verifier and librdkafka lines, the Microsoft quotes and the job-log lines. Its result line was `Findings: 2 at P0 or P1`. Dispositions:

- P0, both files under `results/B04/`: the same finding as round 1, kept on the same grounds.
- P1, this receipt: it said every check group ran with `HEAD` at the base, but the full test-suite rerun ran at `5849437`. Fixed in "Source and commands", which also now says the modified script ran against this working tree.
- P2, this receipt: "before any test body ran" was wrong for 5 of the 15 failures, whose test methods started before the Docker lookup failed. Fixed to "before any Kafka container started".
- P2, this receipt: the record did not say whether the additional P2 round ran. Fixed by the first sentence of this paragraph.

Round 3 reviewed the diff at `0792ab968a4f2ddcae216df3524cec47c0bc935b`. Its only findings were the two kept `results/` P0s, and it raised no P2, so no additional P2 round followed. The factory.md amendment came after round 3. Round 4 reviewed it and reported, besides the two kept `results/` P0s, one P1 and one P2. The P1: the note claimed every anchored blob URL was wrapped, but the posted Revision 2 still has three anchored librdkafka blob URLs whole. Fixed: the note now names blob URLs into this repository, and says anchored URLs into another repository came back whole. The P2: this receipt did not record the docs check of factory.md. Fixed in "Source and commands". Round 4 is the last round the plan's cap allows, so the fixes after it are not cold-reviewed; the plan's exhaustion action applies.

The #184 first-five-pull-request review window does not apply to this pull request. It covered #188, #189, #191, #193 and #194, as recorded in `docs/operations/evidence/issue-196/receipt.md`.

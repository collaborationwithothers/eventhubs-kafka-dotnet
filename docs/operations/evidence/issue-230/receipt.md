# S3 C08 and F04 remedies implementation receipt

This receipt records the implementation and verification of the C08/F04 single-action amendment. Local checks use the authored worktree based on source commit `fd4981620d8ea79ea22fbaff49b39d4d4d1ac804`, before the implementation commit is created.

## Authority and scope

The work implements [S3 plan Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/230#issuecomment-6454780607), `updated_at: 2026-10-27T22:06:22Z`. Its [independent standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/230#issuecomment-6461193893) identifies that revision and states `Blocking findings: none`. The parent is [#210 plan Revision 7](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/210#issuecomment-6415356278), approved by [6454346600](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/210#issuecomment-6454346600). [Decision 6452910517](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/210#issuecomment-6452910517) adopts the [#221 remedies](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/221#issuecomment-6452902336) and assigns Codex.

The implementation branch is `codex/issue-230-c08-f04-remedies`. This pull request adds the approved terminal rules, debug activation, dropped-error field, bounded client logs and whole-item filter, with their contract and assessment text. The parent remains the owner of scientific decision rules. The expected-outcome edits are specifically required by the approved plan and need Hari's protected merge under AGENTS.md "Before opening a pull request" rule 8. No result is published here.

The other four batch definitions and expected outcomes, published results, MeasurementCatalog and C5 configuration remain unchanged. [Decision 6454209278](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/210#issuecomment-6454209278) authorizes no smoke dispatch.

## Commands and results

The local environment uses .NET SDK 10.0.402, dotnet-coverage 18.12.0 and the pinned CI tools installed by `infra/scripts/lifecycle/install-tools.sh`. Docker's managed local socket was checked before component verification. Local reports remain outside git. The final head's CI run is recorded in the pull request description.

Checks completed on the implementation worktree:

| Command | Result |
| --- | --- |
| `./scripts/verify-dotnet.sh` | Final fresh run exited 0; 4660 tests passed, no skips; warning-as-error control passed. |
| `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'` | Exit 0; 78 tests passed. |
| `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` | Exit 0; 126 tests passed. |
| `bash infra/scripts/lifecycle/check-offline.sh` | Exit 0; shell, workflow and chart checks passed. |
| `bash charts/parchwell/tests/check-loadgen-host.sh` | Exit 0. |
| `terraform fmt -check -recursive infra tests/terraform` | Exit 0. |
| `bash infra/scripts/lifecycle/check-offline.sh --terraform-test-paths` | Exit 0. |
| `cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt` | Exit 0; byte-identical. |
| `./scripts/verify-kafka-coverage.sh /tmp/implement-230/coverage/dotnet-coverage` | Exit 0; 166 configuration tests passed, no skips; all 404 instrumented mapping source lines covered. |

The six-file YAML walk printed `ok`. In a temporary copy, replacing B01 with `verdict: {confirmed_when: C1, C2 hold}` exited 1 without `ok`; the real B01 file was unchanged. The exact joined Debug XML href assertions passed. The approved preservation diff and `git diff --check` passed.

The item-filter tests use real local Helm rendering and synthetic command/receipt fixtures. They require the literal 15-Job C08/F04 sequence, seven candidate transports, all nine unselected entries and no excluded Job or gap artifact. F04-only and C08-only tests separately check seed inclusion and clock behavior. These are harness observations; they establish no Event Hubs outcome, authorization result or debug cause.

The first full .NET run exited 2: 4659 of 4660 tests passed, with one failure in the unchanged `VerifierWindowComponentTests.Given_PartitionAddedAfterStart_When_RealHandlerDrains_Then_EndOnlyPartitionStartsAtZero`. Its write immediately after `CreatePartitionsAsync` returned `Local: Unknown partition`, before verifier judgement. That test and its produce helper match the base commit. The isolated rerun passed, then a fresh full `./scripts/verify-dotnet.sh` run passed without a code or assertion change. Both outcomes are retained in this record; no claim about the cause is needed.

Final full-run counts, computed from the xUnit reports:

| Project | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| `Parchwell.ArchitectureTests` | 12 | 0 | 0 |
| `Parchwell.Enrichment.Tests` | 55 | 0 | 0 |
| `Parchwell.Kafka.Tests` | 403 | 0 | 0 |
| `Parchwell.LoadGen.Tests` | 556 | 0 | 0 |
| `Parchwell.Scenarios.Tests` | 200 | 0 | 0 |
| `Parchwell.Verifier.Tests` | 3434 | 0 | 0 |

The final .NET reports are in `/tmp/parchwell-dotnet-verification.E0Dx2bPt`; the first run's reports are in `/tmp/parchwell-dotnet-verification.ByHPESyt`. Coverage reports are in `/tmp/parchwell-kafka-coverage.5dNqsnUO`. These are local evidence locations, not committed artifacts. The final CI link belongs in the pull request description.

Documentation advisory scans exit 0. Blanket disposition under AGENTS.md rule 8: warnings in files outside this pull request are inherited and left unchanged; the authored contract documents and this receipt have no reported issue.

## Cold review

Round 1 reviewed the complete staged diff, including this receipt, in a fresh context using `repo-review`. It reported:

- P0, `scenarios/C08/expected.yaml:26`: expected-outcome edits without a visible verdict label. Retained under AGENTS.md "Before opening a pull request" rule 8 because the approved S3 plan specifically requires these edits. Disclose in the pull request; Hari merges.
- P0, `scenarios/F04/expected.yaml:26`: the same expected-outcome rule. Retained on the same approved-plan basis; Hari merges.

Its exact result line was `Findings: 2 at P0 or P1`. Both findings fall under rule 8's approved-plan exception before counting; no other finding was reported. This receipt records that round. A fresh review of the final authored diff follows this record update; the last round and final head's CI are recorded in the pull request description.

## Remaining acceptance

Hari merges the protected paths. After merge, the existing `verifier-c5-mutation.yml` must measure a main commit containing S3 and report `measurement_complete: true` and `break_passed: true`. Older C5 evidence cannot accept this judgement change. No C5 or scenario workflow was dispatched in this session.

Only after the parent and child approvals, S3 merge and that passing C5 acceptance may the parent's at-most-two filtered live dispatches run with `scenario=probe-batch`, `run_kind=batch`, `items=C08,F04` and `ref=main`. Exhaustion hands off to Hari under parent Decision rule 11. Debug volume, broker outcomes and causes remain unobserved; a capped or unreadable log cannot supply an inferred cause.

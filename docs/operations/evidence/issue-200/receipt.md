# Broker-count export validation receipt

The checks below verify that accepted per-client broker measurements survive the phase-candidate export. No live experiment was run and no published A01 result was changed.

## Source and authority

Tested source: `3452bb362d885b1faef115842ed35ea021fae027`, based on main `93153faaed8b04b739bea3e3a6d1a52ba106cecc`. This receipt is the only later authored file; it does not change the tested source or tests.

Implementation follows [Broker-count export plan, revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/200#issuecomment-6409035927), updated `2026-10-26T05:36:04Z`, and its [independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/200#issuecomment-6409075126). The check reports `Blocking findings: none`. Its permitted P2 was applied: the writer rejects a supplied false lower-bound marker, and a codec regression proves rejection. [Hari's assignment decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/200#issuecomment-6409066496) resolves the approval-label ordering mismatch for this Codex session.

[Hari's mutation-acceptance amendment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/200#issuecomment-6409433947), posted `2026-10-26T06:07:38Z`, removes the local sample from pre-merge acceptance and requires the hosted C5 mutation result after merge. It supersedes the earlier mutation-cap handoff and the proposed retained local campaign. No second local mutation campaign was run.

The export preserves the existing [check-28 measurement definition](../../verifier-contract.md#statistics-and-configuration), including its pinned librdkafka source and sampled lower-bound qualification. It adds no scientific rejection predicate, phase sum, consumer capture or zero substitution. Issue #200 stays open until the next published scenario includes the retained measurements.

## Build and test evidence

Commands ran from the repository root with `PATH=/tmp/issue-200-dotnet:$PATH` and `DOTNET_ROOT=/tmp/issue-200-dotnet`, selecting the pinned SDK `10.0.402`.

- `dotnet restore Parchwell.slnx --locked-mode`: passed.
- `dotnet build Parchwell.slnx --configuration Release --no-restore`: passed with zero warnings and errors.
- `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-build --minimum-expected-tests 1 -- --report-xunit-xml --report-xunit-xml-filename issue-200.xml`: passed, with all tests executed. The report was moved outside the checkout to `/tmp/issue-200-final-TestResults/issue-200.xml`.
- `./scripts/verify-dotnet.sh`: passed, including locked restore, solution build, Kafka XML consistency, effective project settings, architecture checks, selected suites, report validation and the warning-as-error control. Logs and reports remain at `/tmp/parchwell-dotnet-verification.GedhO9zy`.

The XML reports were parsed with Python's ElementTree. Every selected suite has zero failed, skipped, not-run and error entries:

| Suite | Passed / total |
| --- | ---: |
| Architecture | 11 / 11 |
| Kafka | 349 / 349 |
| LoadGen | 359 / 359 |
| Scenarios | 91 / 91 |
| Verifier | 2633 / 2633 |

The Enrichment test project is an empty scaffold; the verification script makes no unit-coverage claim for it. Kafka container tests characterize the harness, not Event Hubs.

During implementation, an initial unfiltered Verifier run passed 2632 tests and failed one new projection test because it expected a generated client name instead of the fixture's registered `pod-c5`. Luna corrected that expectation to preserve the actual registered identity. A new test's array allocation also produced analyzer CA1861 and was moved to a static field. The corrected focused Judge namespace command, using `--filter-namespace Parchwell.Verifier.Tests.Judge`, passed 174 / 174; the later full-source results above supersede the initial failed run. Initial and focused reports remain under `/tmp/issue-200-agent-TestResults`, outside git. No existing assertion was loosened, skipped or removed.

## Mutation observation and post-merge evidence

`dotnet tool restore` passed for Stryker.NET `5.0.0`. The single authorized `python3 tests/Parchwell.Verifier.Tests/Mutation/run-c5-sample.py` command exited 1. Its receipt reports `failure: generated_mutant_cap_exceeded`: the observed generated count was 9992, above the approved 2000 cap. The runner terminated Stryker with exit code 143 after 104.755 seconds and 3090 captured output bytes. Time and output stayed within their 1800-second and 16-MiB bounds; the generated-count bound did not.

This exhausted run is an observation, **not mutation evidence**. The receipt reports `measurement_complete:false`, `generated_count_observed_within_cap:false`, `source_unchanged:true`, and an empty `report_sha256` map. Python independently compared every recorded source SHA-256 with the tested checkout and found agreement. The changed PhaseJudge source SHA-256 is `3c2b674f62acc80e69c1a35ca4b8e9be2c1cc01a8c8131a3f1178ab183eda2ac`. The execution receipt remains at `/tmp/issue-200-mutation.json`; it is not committed program output. No mutation score, survivor population or compile-error count is claimed, and no second sample or survivor investigation was run.

Under the amendment, this pull request's mutation evidence is the [C5 verifier mutation workflow](../../../../.github/workflows/verifier-c5-mutation.yml), dispatched on `main` after merge. Its result must have `measurement_complete:true` and `break_passed:true`. That result follows the merge and is pending; the passing build and tests do not replace it. If the run fails or is incomplete, a fix issue is opened. No evidence pack, including B04's, publishes until a post-merge C5 run on the changed targets passes. No workflow was dispatched by this session.

## Documentation, scope and review

`npm ci --ignore-scripts` passed. `npm run check:docs` exited 0 with three advisory warnings, all in unchanged historical evidence documents. Blanket disposition: those historical links remain outside this export change. No changed Markdown file had a warning. `git diff --check` passed. A Python assertion compared `git diff --cached --name-only --merge-base origin/main` with the approved path allowlist and matched exactly ten files, including this receipt. No results, scenarios, expectations, workflows, infrastructure, projects, references or packages changed.

The earlier backup push used the explicit cold-review exception in AGENTS.md "Before opening a pull request" rule 12. The remaining pre-merge acceptance commands have passed. Cold-review round 1 used a fresh Codex subagent at high effort and the repo-review rubric over the complete ten-file diff and this receipt. It verified the linked plan decisions, measured results, source agreement and mutation hashes, reported no actionable findings, and returned `Findings: none at P0 or P1`. No P2 fix or follow-up issue was required. The final cold-review round and final-head CI run are recorded in the pull request description. The verifier and governance paths still require Hari's merge.

Initial implementation and test drafting used the Codex parent model; its model identifier is not exposed to this session. At Hari's request, GPT-6 Luna (`gpt-6-luna`), high effort, checked the existing edits, completed implementation and ran the build and focused tests. The parent session coordinates acceptance and authors this receipt. Planning and independent plan checking used fresh Codex subagents at high effort.

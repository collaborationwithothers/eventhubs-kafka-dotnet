# C5 isolated-failure diagnostic receipt

This change implements the [Revision 1 diagnostic plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/389#issuecomment-6672079081) and the literal fixture clarification in its [independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/389#issuecomment-6672140441). It retains failure evidence and records diagnostic observations without changing mutation acceptance.

## Execution card

- Deliverable and authorization: the linked plan's Exact changes sections 1-5, approved under standing approval by the linked fresh-context check. The current user directly assigned Codex. The issue's tool comment followed its label event; no earlier tool assignment is claimed.
- Source baseline: `3d21683cdd95cdb5d74bf532f5785e29b1a5370a`.
- Mandatory gates: the plan's Acceptance commands and evidence, cold review under [pull-request rules](../../docs/agents/pull-requests.md), final-head CI and hosted Codex review. The user requests readiness after those final-head gates pass; merge is outside this task.
- Exact verification commands: `python3 tests/Parchwell.Verifier.Tests/Mutation/test_c5_workflow.py`; `./scripts/verify-dotnet.sh`; `npm run check:docs`; `git diff --check`; scripted changed-path, original-assertion, timeout and mutation-target comparisons against the source baseline.
- Earliest publication checkpoint: tested authored diff with applicable complete cold-review coverage, after authority refresh.
- Review budget and exhaustion: [pull-request rules, rule 8](../../docs/agents/pull-requests.md#rules) applies cumulatively across sessions. Exhaustion saves unpublished work on the backup branch and hands off to Hari. The PR self-review record holds the complete reports and final-head CI evidence.
- Execution caps: the plan's Bounded execution section applies. Collection-cap exhaustion records the gap and continues. Troubleshooting and changed-file documentation-disposition exhaustion pushes work in progress and hands off as that section specifies.

## Predicate map

No production verifier predicate changes. The existing HTTP request classification and sticky failure flag remain unchanged. The diagnostic checks below implement plan section 4 and its plan-check clarification; they do not authorize mutation evidence.

| Affected behavior | Regression evidence |
| --- | --- |
| Loopback allowance and sticky unexpected-request flag | Synthetic loopback, external telemetry host, then loopback sequence keeps the literal original payload and false/true/true flag states. Collection exhaustion and output failure cannot clear the flag. |
| Diagnostic strings and unavailable Activity | Scalar lengths 2047/2048/2049 and stack lengths 8191/8192/8193 preserve below/at values and append an in-cap truncation marker above. Null Activity fields say `unavailable`; supplied event IDs and current Activity values survive. |
| Independent request and phase limits | Attempted totals 15/16/17 retain 15/16/16 and record 0/0/1 dropped observations, counting construction in phase totals. |
| Original chart-host proof | Both original theory rows retain every assertion and the five-second wait expression. Diagnostics emit after success or failure through the same finalization path. |
| Raw log retention and resource collection | The Python synthetic regressions cover baseline failure before report creation, byte-identical retained logs, bounded collection, unavailable resources, sequential invocation order and unchanged execution results. |

## Verification

Source: the pinned baseline plus this authored diff. No generated logs or reports are committed.

| Command or check | Measured result |
| --- | --- |
| `dotnet build Parchwell.slnx --configuration Release --no-restore` | Passed with SDK 10.0.402, no warnings or errors. |
| Focused `dotnet test` for `*ChartEnvironmentVerifierHostTests`, Release, no build, XML report | All 14 tests passed; no failures or skips. Report: `/tmp/issue389-chart/results.xml`. |
| `python3 tests/Parchwell.Verifier.Tests/Mutation/test_c5_workflow.py` | All 92 tests passed. Log: `/tmp/issue389-python-test.log`. |
| `npm run check:docs` | Passed; eight warnings in unchanged files, none in either new item file. |
| Scripted source comparison against the pinned baseline | Exactly the five plan paths changed. All 17 configured targets, every workflow, scenario and result file remain byte-identical. All 18 original C# assertion lines and 306 original Python assertion lines remain, with the original chart-host timeout expression. |
| `git diff --check` | Passed. |
| `./scripts/verify-dotnet.sh` (tracked foreground run) | Exit 0: all 8891 tests passed with no failures or skips, including 6818 verifier tests. Locked restore, Release/analyzers, architecture, XML report validation and warning-as-error controls passed. Reports: `/tmp/parchwell-dotnet-verification.risESCMW`; log: `/tmp/issue389-verification-final.log`. |
| Captured xUnit output inspection | Both original chart-host theory rows retain `diagnostic_only`, `guard_disposed` and the correlation/attribution caveat in their XML output. |

The first full `./scripts/verify-dotnet.sh` run was interrupted by the environment reconnect during verifier tests. Its checked architecture, enrichment, Kafka, load-generator and scenario reports had passed, but it produced no verifier report or final success marker. The original log remains at `/tmp/issue389-verification.log`, with partial reports at `/tmp/parchwell-dotnet-verification.xbQrftJo`. This is incomplete verification evidence, never a passing run. The detached recovery launch also stopped without a verifier report or final success marker; `/tmp/issue389-verification-recovered.log` remains a second incomplete record. The required full command then completed as a tracked foreground job, writing `/tmp/issue389-verification-final.log`; `/tmp/issue389-verification-final.exit` records exit 0. Its passing result is listed above.

Troubleshooting root-cause groups used: environment interruption and the unsuccessful detached recovery launch. The foreground launch corrects the latter. Both incomplete logs are retained. No assertion, timeout or configuration was changed for recovery. No implementation test failure was observed before the interruption.

No mutation acceptance, workflow dispatch, Azure access or live attribution result is claimed. The collectors are exercised by synthetic fixtures. A later passing main-only C5 run is required before claiming mutation acceptance; Hari merges any workflow-file changes.

## Review inputs

Comprehensive cold-review mode applies to the complete authored diff against the source baseline. The frozen revision is the committed HEAD and index supplied to the reviewer; the reviewer records its full hash. Comparison revision: the pinned source baseline. This is the first comprehensive pass; no focused pass has run. The complete report, specification comparison, dispositions and cumulative comprehensive/focused counts are recorded in the PR self-review record. Later authored changes require applicable coverage under the linked review rules.

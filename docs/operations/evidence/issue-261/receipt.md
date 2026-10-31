# Mutation execution repair receipt

The failed hosted measurement repeatedly timed out while an unrelated full-capacity test was still running. This repair suppresses mutant-control branches in source already excluded from each shard, using an auditable temporary source projection. It preserves the assigned target and every test; post-repair instrumented runtime and complete mutation acceptance remain unmeasured.

## Execution card and authority

- Deliverable: the projection helper, existing runner/aggregation integration, regression coverage and operations contract for [the mutation repair](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/261).
- Authorization: Hari's direct instruction to "then fix it in a fresh context subagent", recorded in [the maintenance decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/261#issuecomment-6540343950). This is the actual repair brief, not approval of a newly written experimental plan. The originating [Revision 7](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6490062145), its [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6525928916), and [single-campaign allowance](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6528164329) retain their scientific gates. That campaign allowance is consumed.
- Mandatory gates: meaningful local verification, documentation checks, cold review, final-head hosted CI/Codex review and Hari's protected-path review/merge. Publication waits for separately authorized passing hosted mutation acceptance.
- Earliest publication checkpoint: the tested tooling repair after applicable cold review. It claims no scientific result or performance success.
- Review budget: AGENTS.md "Before opening a pull request" rule 8; at most two comprehensive and two focused passes across sessions, with its exhaustion/unsafe-focus handoff to Hari. No new allowance is inferred.
- Verification: `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_workflow.py'`, actual exported mutation-only project build/test, `git diff --check` and `npm run check:docs`. The real export/build/test is implementation acceptance, not another Stryker diagnostic. Each .NET acceptance command has an 1800-second bound; no Stryker command follows the two exhausted diagnostics.

## Retained failure and bounded reproduction

The [failed hosted run](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41092511387), attempt 1 at `32babefac185b8e8a4855e4f3892c1772e431d29`, remains the latest applicable complete-campaign attempt. Both failed command logs match their retained receipt hashes. Their measured process deadlines were 16200.021 and 16200.014 seconds. The [prior diagnosis](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/261#issuecomment-6540310026) links retained artifacts and official hosted-runner limits. Extending the current process deadline is not this repair.

Diagnostic source was main `8cbde949d8fca424dda33accbd01c1a995495678`, with SDK 10.0.402 and Stryker 5.0.0. A scripted diff confirmed its verifier source/tests, SDK and tool inputs match the failed measured commit. Both invocations selected only characters 1123 through 1147 of `SingleActionRepeatJudgement.cs`, the `"p4_no_terminal_outcome"` literal. Each scheduled exactly one mutant, with at most four allowed and a 300-second process cap. No whole target or survivor audit ran.

| Observation | Original execution | Tiered JIT disabled |
| --- | ---: | ---: |
| Total diagnostic elapsed seconds | 227.117627291 | 236.621470725 |
| Initial unmutated tests passed | 3908 | 3908 |
| Initial capacity-test duration milliseconds | 13273.9999 | 12643 |
| Selected-mutant RPC deadline milliseconds | 60434 | 62744 |
| RPC timeouts / server restarts | 1 / 1 | 1 / 1 |
| Latest test states at deadline | 3748 passed, 16 failed, 1 in progress | 3748 passed, 16 failed, 1 in progress |

The full-capacity regression was in progress in both unfinished requests. Both tiny reports call the selected mutant killed from the observed assertion failures; their displayed 100 percent score does not establish healthy execution or acceptance. The JIT override is rejected and does not ship. During the second mutant run, the host mapped its mutant-control file and its observed I/O counters did not show per-control file reads. This does not isolate every source of runtime overhead.

The separate ordinary diagnostic allowance was two invocations with 180-second bounds: an incorrect filter selected no tests and failed; the corrected direct runner invocation passed all eight capacity tests in 11.017 seconds. Stryker had restored the original assemblies before that corrected invocation, so it measures ordinary execution. Both ordinary and both Stryker diagnostic slots are consumed; implementation acceptance does not reset them.

Local raw logs, RPC records, XML, comparison metadata, exact diagnostic wrapper and config remain under `/tmp/repair-261`. They are not committed program output or independently downloadable hosted evidence. The two command-log SHA256 values are `e25ed979526c5a1950a7f93dee0e4e6ebfbfac25c056ba4873e6de744303b3d0` and `94f32f79aa51e9c55f2a96c6e5051ea936ba9435b5d9e67825d665eacc6fd22f`. The rejected setting's documented meaning was checked against [official .NET compilation settings](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/compilation), read 2026-10-31.

## Repair and predicate map

The [mutation record](../../verifier-mutation.md#issue-261-excluded-source-instrumentation-repair) cites the pinned source proving that disabled mutants are removed from injection before compilation. Static coverage markers may remain. The repair changes the runner's handling of already-excluded source, not the six target definitions, coverage-off policy, tests, assertions, aggregate numerator/denominator or break 60. No judgement or expected outcome changes.

The temporary export uses only committed files. `projection.json` retains original and projected hashes, its fixed policy/target and exact excluded-source list; `projection-src/` retains verifier input bytes. Source before/after receipts still describe the original measured checkout. Aggregation checks the projection and binds the native report's assigned source to that original target hash. No generated output is committed.

| Affected predicate | Regression evidence |
| --- | --- |
| Assigned target and all tests stay byte-identical; original checkout stays intact | Actual child reads its copied target/test file and projected excluded source; original snapshots match; temporary copy is removed |
| Original/projected maps, policy, target and exact excluded list agree | Positive six-shard fixtures plus isolated policy/target/list, missing/hash, drift, target/excluded byte and report-source failures |
| Test or other input drift cannot pass | Isolated changed test hash; source-changing child; added compile-source child |
| Existing disabling/restoring directives cannot re-enable excluded source | Committed restore-directive fixture is rejected before child launch |
| Export bounds and source inventories | Below/at/above file-count, source-count and byte bounds; empty maps/lists and unsafe path/hash cases |
| Original byte sequence, BOM and line count survive prefix removal | Empty, LF and BOM/CRLF cases, plus missing-prefix rejection |
| Existing process and score rules still apply | Existing exit/error/timeout/descendant, scope/config/source, complete/incomplete and aggregate threshold regressions |

Numeric export bounds are maintenance policy choices owned by the helper: 4096 tracked files, 268435456 raw file bytes, 128 verifier source files, and a 60-second archive command deadline. Exceeding one fails preparation without invoking Stryker. These do not raise the existing mutation-process or hosted-job limits. Existing timeout/error tests retain the child deadline behavior; the archive timeout is subprocess enforcement rather than a verifier numerical judgement.

## Verification and review

Implementation verification is pending at this receipt revision. The final receipt update records tested source, commands and outcomes before cold review. The PR description holds complete cold-review reports, cumulative counts, final-head CI and hosted reviews; no receipt-only commit is required for its own head.

The incomplete hosted measurement continues to block scientific evidence-pack publication. This repair has no measured post-repair instrumented runtime, whole-target completion estimate or passing campaign. A future campaign must independently establish all configured populations, `measurement_complete:true` and `break_passed:true` on main containing this repair. No campaign, Azure/scientific dispatch, paid runner, permission change, result or verdict is authorized or produced here.

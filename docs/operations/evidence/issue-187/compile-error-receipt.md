# C5 foreign compile-error repair receipt

This repair accepts out-of-target CompileError records while preserving assigned-target scoring and rejection of other foreign statuses. Synthetic tests verify the parser change. A new complete hosted mutation result remains pending.

## Authority and tested source

This repair follows [Revision 1 of the repair plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/187#issuecomment-6403082410), updated 2026-10-25T20:49:49Z, its [fresh independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/187#issuecomment-6403132560), and [Hari's aggregation decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/187#issuecomment-6403024676). The original [PR #194 receipt](receipt.md) remains historical evidence for that PR's own inputs.

The branch starts from main `bb6a86ed393ef61cfa706e7d700a7079a67cc6a5`. Validation used that commit plus the authored predicate replacement, two additive regression methods and this receipt. The excluded-file sentinel and assigned-target validation remain unchanged. Accepted foreign CompileError and Ignored records do not enter assigned-target score or status populations. All other foreign statuses still fail completeness. The failed hosted aggregate remains incomplete; its unofficial figures are not reused as a result.

## Score semantics

Stryker's mutant-state documentation states that Compile error is not represented in the mutation score, and that Timeout counts as detected. Its metrics define detected as killed + timeout, valid as detected + undetected, and mutation score as detected / valid * 100. The pinned 5.0.0 ProjectComponent implementation independently classifies CompileError/RuntimeError as invalid, Killed/Timeout as detected, and computes the score over valid mutants. This repair changes neither that score population nor survivor disposition.

Sources: [official mutant states and metrics](https://stryker-mutator.io/docs/mutation-testing-elements/mutant-states-and-metrics/), checked 2026-10-25, and [ProjectComponent.cs at the pinned 5.0.0 revision](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/ProjectComponents/ProjectComponent.cs).

## Timeout settings and limitations

The workflow and C5 configuration do not override additional-timeout or timeout-ratio. The pinned Stryker.NET 5.0.0 input implementations default these values to 10000 milliseconds and 2 respectively; StrykerInputs.ValidateAll assigns their validated values to StrykerOptions. The configuration documentation at the same revision lists 3000 and 1.5, which disagrees with those input implementations. The effective defaults reported here follow the code that produces the options. The resulting per-mutant timeout is calculated from measured initial test timings; it is not a fixed 10000-millisecond total. Timeout mutants count as detected under the documented score rule; a Timeout status alone does not prove an assertion killed that mutant. The separate wrapper limit remains 16200 seconds per shard inside the existing 300-minute hosted job.

Pinned producer references:

- [AdditionalTimeoutInput.cs](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Configuration/Options/Inputs/AdditionalTimeoutInput.cs).
- [TimeoutRatioInput.cs](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Configuration/Options/Inputs/TimeoutRatioInput.cs).
- [StrykerInputs.cs](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Configuration/Options/StrykerInputs.cs).
- [Timeout configuration documentation](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/docs/configuration.md#additional-timeout-number).

The existing [runner](../../../../tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py), [workflow](../../../../.github/workflows/verifier-c5-mutation.yml) and [configuration](../../../../tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json) retain these settings and bounds. No particular calculated per-mutant duration or cause of a previous timeout is inferred.

## Validation

From the repository root:

```bash
timeout 600s python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_workflow.py'
timeout 600s git diff --check
timeout 600s npm run check:docs
```

The Python suite exited 0 on its first execution: 24 tests passed in 1.002 seconds. Both new regression methods were included. Four synthetic shards containing foreign CompileError/Ignored records produce a complete 4/4 score, Killed 4 with all other counted statuses 0, and empty survivor/sample lists. Eight independent synthetic cases reject foreign Killed, Survived, Timeout, NoCoverage, RuntimeError, Pending, NotRun and Unknown records, omit the rejected phase and retain the specified incomplete reason. Existing assertions and test bodies remain unchanged. These are synthetic parser observations, not mutation results.

The docs scan exited 0 and reported no issues in this new receipt. Its three warnings concern unchanged historical documents after Runner retirement; they receive one blanket disposition in the PR description. No changed-file warning required a disposition. The new receipt was staged before scanning. The whitespace check exited 0; the staged diff is also checked with `timeout 600s git diff --cached --check` before review, within the same two-execution whitespace-check allowance.

The source-scope inspection uses `git diff --name-only bb6a86ed393ef61cfa706e7d700a7079a67cc6a5...HEAD` together with `git diff --cached --name-only` and `git diff --name-only`. The actual review diff checks that only the two Python files and this new receipt change: one predicate/diagnostic replacement and additive regression methods. No configuration, workflow, production verifier, exclusion, package or score formula changes.

Raw validation output is retained locally under `/tmp/issue-187-compileerror-validation/`, outside git. The PR description records final head CI, the latest cold-review result and any dispositions. No .NET suite, local Stryker campaign, old-artifact download, reaggregation or survivor review is part of this repair's acceptance.

## Continuation

Hari merges the protected test paths. After REST confirms the repair is merged and present on main, the existing C5 workflow is dispatched once on main under the decision. The agent does not retry, wait on or poll that measurement. Existing artifacts are not reaggregated. The run-to-publication target hash comparison remains part of #196's evidence pack under the [hash decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/187#issuecomment-6402343589). Neither this receipt nor its synthetic tests satisfies the complete hosted measurement requirement.

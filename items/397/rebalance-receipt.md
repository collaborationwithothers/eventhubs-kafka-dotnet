# C5 measured span rebalance receipt

This receipt records the second implementation pull request for the mutation runtime item. It preserves the investigation as historical evidence and records no measured optimized speedup.

## Execution card

- Deliverable: the seven authored paths and exact allocation in the [Revision 1 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703130075).
- Authorization: [Hari's narrow approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703287109), adopting both exact corrections in the [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703193224). Bookkeeping expectations and legacy fixtures may change; judging assertions and gates may not.
- Baseline: main `8e2ab2fbb1ca7fe1a02396af4a07ac9626c13c73`. The [investigation receipt](receipt.md) belongs to merged PR 398 and is not this PR's review record.
- Mandatory evidence: full helper unittest discovery, explicit retained-inventory suite, documentation check with dispositions, actionlint, whitespace and exact changed-path review. Coverage off, break 60, static isolation, every judging test, native identity reconciliation and acceptance remain required. No dispatch or diagnostic mutation run is authorized.
- Exact verification commands: the six commands in the plan's Acceptance and publication section, from repository root. Their outcomes are recorded below after execution.
- Publication checkpoint: after those commands and applicable cold review pass, publish the tested PR. Hosted review and CI may run together. Hari merges the workflow change after the required code/security/Claude reviews; this session never merges it.
- Review budget: at most two comprehensive and two focused cold rounds for this PR under docs/agents/pull-requests.md rule 8. On exhaustion or unsafe focus, push this branch with `-wip` appended and hand off to Hari. Documentation warning and diagnostic correction caps/exhaustion actions are exactly the approved plan's, not a new allowance.

## Verification and predicate coverage

Implementation is partial and its mandatory Python/documentation verification is pending. The final receipt will name the orchestration predicates and their tests before the first cold review. No .NET verifier predicate is changed.

## Report-inclusion finding and pause

During implementation, the source read behind the runbook's report-inventory claim exposed a second Stryker behavior beyond per-mutant filtering. At pinned commit `6e77a3451bac4793e9c839c3ff4055c1b30ca3af`, [ProjectComponentsExtensions.IsComponentExcluded](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/ProjectComponents/ProjectComponentsExtensions.cs) flattens all matching negative patterns, reduces their intervals together and removes that union from the included intervals. [JsonReport.GenerateFileReportComponents](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/Reporters/Json/JsonReport.cs) then emits [SourceFile.Ignored](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/Reporters/Json/SourceFiles/SourceFile.cs), with no mutant inventory, when the remainder is empty. Separate negative strings do not prevent this report-level reduction.

Revision 1's complement excludes the full interval `0..2147483647`. Its mutant ownership calculation is valid, but its report would be incomplete. All implementation and Python tests stopped when this was found. The partial selectors and tests in this backup are not ready for a pull request; no passing end-to-end proof is claimed.

The proposed amendment ends only the final complement exclusion at the actual UTF-16 source length, keeping each negative string separate. This leaves an unoccupied suffix in the report-inclusion geometry while retaining all mutant-level decisions. A read-only calculation against all 6636 retained positive native node spans confirmed unchanged selection for all eight complements; every node ends at or before its source length. This is source/data verification, not an instrumented run or a new mutation measurement. The amended tests must independently exercise both reporter inclusion and mutant ownership before this becomes implementation evidence.

On the partial working tree, pinned actionlint 1.7.12 with ShellCheck 0.11.0 passed the C5 workflow. Python AST parsing and an assertion-subsequence audit confirmed existing helper assertions remain outside the specifically approved administrative contract changes. The audit permits new assertions and the approved committed-span test rename. These checks do not replace the two mandatory Python commands, documentation scan, cold review or final-head CI, none of which has run for this partial implementation.

## Review record

Cold reviews started: zero comprehensive, zero focused. Hosted code/security review and the separate Claude review have not started. No claim of final-head CI or merge readiness is made.

The [Revision 2 independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703401722) supports the correction and finds only the required amendment approval blocking. The earlier bookkeeping approval is retained; it does not authorize the later selector amendment. This work is backup-only under AGENTS.md rule 12, with zero comprehensive/focused cold reviews and no PR.

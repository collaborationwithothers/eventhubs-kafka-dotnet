# Packet-summary file receipt

This PR adds local staging ownership and bounded file finalization around the merged packet-summary core. It acquires no packets and publishes no experimental result.

## Execution card

- Deliverable: the exact source, tests, contract and state changes in [Finalize bounded packet-summary files, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6617689106), updated 2026-11-03T23:02:02Z.
- Authorization: the [standing investigation decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6596595315), adopted design and [design approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6615675298), and [independent standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6617754758). The check identifies this revision and states `Blocking findings: none`; the plan has no open questions for this component. Its exact flush-evidence correction applies only to the new contract.
- Source main: `179667e6c2be5123763459090fe45470fe2cab98`, the squash-merged core. No existing source/test/project file or core contract is changed.
- Mandatory gates: the plan's Acceptance and publication checkpoint; AGENTS.md GOVERNANCE and Before opening a pull request; docs/agents/pull-requests.md; factory.md Authority reads, Item lifecycle and Escalation.
- Commands: `./scripts/verify-dotnet.sh` with pinned SDK 10.0.402 on PATH; `npm run check:docs`; `git diff --check`; the development-time Python scope/ASCII/count assertion recorded with the results below. Locked npm dependencies are present. Linux and the existing local Docker prerequisites remain required.
- Earliest normal push/PR: successful acceptance, applicable cold-review coverage and refreshed authority. Final-head CI and hosted code/security reviews remain separate required gates. No Azure dispatch belongs to this PR.
- Execution budget: at most 20 distinct compiler/analyzer/test failure root causes and 12 changed-file documentation-warning dispositions, cumulatively for this component; exhaustion action `push -wip and hand off to the next session`. Unchanged-file warnings take the rule-8 blanket disposition. Run only the plan's fixed matrix; repeat verification only for rule-13 causes.
- Review budget: docs/agents/pull-requests.md rule 8 says "At most two comprehensive passes run per PR across all sessions" and then "at most two focused correction-review rounds", including its P2 exception. "When another required review would exceed the budget, or when focus is unsafe, save unpublished work under rule 12 and hand off to Hari." This component has used no cold review. The merged core's separate cumulative record remains one comprehensive and zero focused reviews, with one completed hosted code and one security review.
- Predicate map: not applicable to verifier work; no verifier predicate changes. The file-state, ownership, byte and failure-order tests are mapped below before review.
- Models: Codex implementing session with separate high-effort production and test agents. The planner and independent checker were separate fresh-context high-effort agents. Exact model identifiers were not exposed.

## Specification and source comparison

Source and test commit: `4e8b65101634d5162926849f54ab9483c1f626a6`, based on the merged core above. The implementation adds only the planned file wrapper, filesystem boundary, Linux rename adapter and their new tests. The core's identity/schema and successful-write-only accounting are unchanged. The new [file contract](../../../idle-connection-capture-file-contract.md) records the check's exact flush-evidence correction, pinned .NET/Linux/glibc sources, inferred name-visibility property and trusted-directory limit.

The first build reported two CA2101 diagnostics on the native pathname parameters, one root cause. Both parameters retain explicit UTF-8 marshaling; the correction explicitly sets `BestFitMapping = false` on the declaration. No assertion, gate, ABI argument, existing source file or test was weakened. The corrected solution build passes. This consumes one of the approved 20 failure-root-cause dispositions; the corrected full verification also passed without another failure root cause.

File existence and successful local completion are not evidence-completeness receipts. Inputs and fault messages are synthetic; actual-image and AKS compatibility, capture privacy and power-loss durability remain unproved.

## Test map

The test files are [writer tests](../../../../../tests/Parchwell.LoadGen.Tests/IdleConnectionCapture/PacketSummaryFileWriterTests.cs), [boundary tests](../../../../../tests/Parchwell.LoadGen.Tests/IdleConnectionCapture/PacketSummaryFileBoundaryTests.cs) and [local integration tests](../../../../../tests/Parchwell.LoadGen.Tests/Integration/PacketSummaryFileIntegrationTests.cs).

| Contract | Test evidence |
|---|---|
| Empty/two-event schema and completion ordering | Writer `Given_EmptyOrTwoEvents` checks exact trace and Active state at flush/close/finalize; integration counterpart checks partial/final visibility, exact JSON and preserved final bytes. |
| No-create validation and precedence | Writer invalid path plus unsupported platform, invalid identity plus unsupported platform, null identity, and unsupported-platform cases. |
| Core failure preservation | Writer prefix/event/tail fault, cancellation and partial-consumption matrix; invalid event/end/offset and reduced event-count cases. |
| Stage failures and bounded cleanup | Writer create/flush/close/finalize and missing-native cases assert exact fixed code and operation trace; close/delete cleanup faults preserve the first failure without retries. |
| Terminal states and diagnostics | Writer abandonment, repeated disposal and post-completion calls; shared fault assertions prove no later I/O and no synthetic path/payload/address sentinel exposure. |
| Independent pre-write cap | Boundary array/span cases cover single writes and cumulative 7/8/9 at cap 8, terminal rejection, partial-inner-write fault, and constructor limits 0/1/20971520/20971521. Real-file case proves rejection leaves length 8. |
| Exclusive ownership/no replacement | Integration partial file/directory/symlink collisions preserve entries; final file/directory/dangling symlink and a late-created final file reject rename and preserve destinations. Abandonment/invalid append never creates final. |

The fixed tests use no raw captures, credentials or Azure calls. Linux acceptance exercises the real native adapter; the non-Linux branches assert unsupported-platform/no-create behavior rather than skipping. Source/contract comparison leaves acquisition, worker control, runtime mounts, verifier receipts and public publication outside this component.

## Verification and review

On 2026-11-03, `PATH=/workspace/scratch/dotnet:$PATH ./scripts/verify-dotnet.sh` passed against source/test commit `4e8b65101634d5162926849f54ab9483c1f626a6` on Linux. SDK 10.0.402, locked restore, Release build/analyzers, Kafka XML inventory, effective project settings, architecture and test-report validation, spike-evidence validation and warning-as-error control all passed. The intentional warning fixture was rejected and its valid counterpart built.

Python parsed the fresh xUnit XML reports in `/tmp/parchwell-dotnet-verification.QLS4myqN` and checked zero failed, skipped, not-run and error counts across all suites:

| Suite | Passed |
|---|---:|
| Parchwell.ArchitectureTests | 15 |
| Parchwell.Enrichment.Tests | 56 |
| Parchwell.Kafka.Tests | 482 |
| Parchwell.LoadGen.Tests | 995 |
| Parchwell.Scenarios.Tests | 297 |
| Parchwell.Verifier.Tests | 5005 |
| Total, computed | 6850 |

Filtering report test types measured 37 writer, 16 boundary and 12 real Linux integration cases, all passing: 65 new cases total. The Linux success/collision tests invoke the real native adapter, without proving the later actual-image or AKS environment. The initial failed build is retained locally at `/tmp/parchwell-dotnet-verification.0OndFkuy`; its two diagnostics share the single corrected CA2101 root cause described above.

`npm run check:docs` completed with eight advisory warnings, all in unchanged files, and zero in this component's changed files. Those warnings stand under the unchanged-file blanket disposition; no changed-file warning budget is consumed. External URLs are not checked by that scanner; the pinned source reads above provide source verification. The final docs-only receipt/state edits are scanned before review.

`git diff --check` passed. The development-time invocation `python3 /workspace/scratch/issue334/file-scope.py` compared the full diff from `179667e6c2be5123763459090fe45470fe2cab98` with the exact planned path set: nine paths, with only `items/334/state.md` already present on the base. It checked documentation ASCII, slice namespace/imports and unchanged existing source/tests/projects/routes/core contract, and computed `20 * 1024 * 1024 == 20971520` and `1 << 0 == 1`. A separate Python report parser computed the table and class counts above; a log parser counted warnings and checked their paths against the diff.

Cold review mode: comprehensive, comparison revision `179667e6c2be5123763459090fe45470fe2cab98`, input revision the commit containing this receipt and the final state/contract. No prior review has run for this component; the first pass consumes comprehensive round one, with the cumulative caps and exhaustion action in the execution card. Freeze the entire authored diff before that pass. The complete cold report and final-head hosted CI/reviews belong in the PR self-review record, not in this receipt's own reviewed input. No program output is committed.

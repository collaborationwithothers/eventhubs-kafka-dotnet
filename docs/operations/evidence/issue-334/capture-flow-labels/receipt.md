# Private flow-label storage receipt

This PR adds bounded private identity storage with stable instance-local labels. It makes no TCP lifecycle, complete-correlation or publishable-evidence judgment.

## Execution card

- Deliverable: exactly five paths and the fixed synthetic matrix in [Bound private flow-label storage, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6626802577), updated 2026-11-04T08:57:11Z.
- Authorization: [standing investigation decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6596595315), [design approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6615675298), and [fresh independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6626855188). The check identifies this revision, states `Blocking findings: none`, and has no apply-in-implementation findings. The plan has no open questions for this component.
- Base: `f122d30669b92b80f6c0fef31a3dd01ce9a38258`, containing the merged counter PR. Existing source, tests, projects and runtime routes stay unchanged.
- Mandatory gates: plan Acceptance and publication checkpoint; AGENTS.md GOVERNANCE and Before opening a pull request; docs/agents/pull-requests.md; factory.md Authority reads, Item lifecycle and Escalation.
- Exact commands: `PATH=/workspace/scratch/dotnet:$PATH ./scripts/verify-dotnet.sh`; `npm run check:docs` after staging new Markdown; `git diff --check`; `python3 /workspace/scratch/issue334/flow-scope.py`, containing the plan's exact scope/ASCII/arithmetic check. Existing SDK, locked npm dependencies and local Docker are available. No Azure or live capture command belongs to this component.
- Earliest normal push/PR: successful acceptance, applicable cold review and refreshed authority. Final-head CI and hosted code/security review remain mandatory; prior PR-specific merge choices do not transfer.
- Execution budget: at most 12 distinct compiler/analyzer/test failure root-cause corrections and 8 changed-file documentation-warning dispositions; each exhaustion action is `push -wip and hand off to the next session`. Unchanged-file warnings take rule 8's blanket disposition. No expanding corpus, mutation or live work; verification repetition follows rule 13.
- Review budget: docs/agents/pull-requests.md rule 8 says "At most two comprehensive passes run per PR across all sessions" and "at most two focused correction-review rounds". "When another required review would exceed the budget, or when focus is unsafe, save unpublished work under rule 12 and hand off to Hari." No prior review exists for this component. Prior PR budgets are retained separately.
- Predicate map: not applicable; no verifier change. A behavior-to-test map is prepared before cold review.
- Models: Codex implementing session and high-effort source/test subagent; fresh high-effort planning and independent checking subagents. Exact model identifiers were not exposed.

## Specification comparison

Source/test commit: `ce9bc53a201edef35f25b6b6b7d7307dd0b6df83`, based on the main commit in the execution card. The [flow-label contract](../../../idle-connection-capture-flow-label-contract.md) describes the approved private storage boundary. Source inspection confirmed a fixed Entry array, five private readonly scalar fields per entry, exact field comparison, failure before lookup, healthy lookup before the capacity guard, and insertion only below capacity. There is no logger, serializer, key export, entry enumeration, header retention or runtime caller. Ordinary object formatting exposes only the type name.

The implementing session checked the approved design's identity and bound at lines 47 and 89 on pinned main, and the existing decoder/fixture/encoder contract. Tests supply the real decoder's normalized tuple and explicitly provide the opening sequence; they pass returned labels into the real encoder. Exact event strings contain only allowlisted fields. None of this admits a real connection or proves lifecycle validity, packet-to-generation resolution, native containment, complete collection or publication eligibility.

## Behavior-to-test map

All cases are in [PacketFlowLabelTableTests.cs](../../../../../tests/Parchwell.LoadGen.Tests/IdleConnectionCapture/PacketFlowLabelTableTests.cs). Verifier predicate mapping is not applicable.

| Plan behavior | Proving cases |
|---|---|
| Initial state, first allocation and repeated identity | `Given_NewTable` checks all observable state at each call. |
| Every field participates in exact equality | `Given_OneChangedIdentityScalar` uses each of the five literal variants and revisits both keys. |
| No scalar network-validation rule | `Given_ZeroAndMaximumScalars` checks both extremes and stable labels. |
| Below, at and above capacity; lookup precedence; terminal failure | `Given_FullTable` inserts through 63, inserts 64, revisits the first/last keys, rejects 65 and rejects all later calls without changing Count/code. |
| Instance isolation | `Given_IndependentTables` checks independent allocation and unchanged peer-instance state. |
| Existing decoder/encoder boundary | `Given_SyntheticDirectionalHeaders` uses both real directional decodes and exact JSON output. Opening identity is explicit, not inferred. |
| No identity-revealing formatting | `Given_StoredPrivateIdentity` requires the ordinary type name. |

## Verification

On 2026-11-04, `PATH=/workspace/scratch/dotnet:$PATH ./scripts/verify-dotnet.sh` passed against the source/test commit above. SDK 10.0.402, locked restore, Release build/analyzers, architecture/effective-settings checks, Kafka XML inventory, fresh report/spike-evidence validation and warning controls passed. The valid warning-control counterpart built and the intentional CS1030 fixture was rejected.

Python parsed fresh xUnit reports in `/tmp/parchwell-dotnet-verification.f73s1d23`, checked zero failures/skips/not-run/errors, and computed these results:

| Suite | Passed |
|---|---:|
| Parchwell.ArchitectureTests | 15 |
| Parchwell.Enrichment.Tests | 56 |
| Parchwell.Kafka.Tests | 486 |
| Parchwell.LoadGen.Tests | 1072 |
| Parchwell.Scenarios.Tests | 331 |
| Parchwell.Verifier.Tests | 5005 |
| Total, computed | 6965 |

Filtering test types measured 11 flow-label cases, all passed. The initial verification required zero compiler/analyzer/test root-cause corrections, consuming zero of the cap of 12. These are synthetic software checks, not live capture or scientific results.

`python3 /workspace/scratch/issue334/flow-scope.py` ran the plan's exact check after staging authored files. It required exactly the five planned paths, ASCII text in each, and the literal port/ordinal boundary arithmetic. It passed; `git diff --check` also passed. The state file is the only previously existing path changed. No program output is committed.

`npm run check:docs` completed with eight advisory warnings in unchanged files and zero warnings in changed files, measured by a Python log parser against the five-path diff. These warnings stand under rule 8's blanket disposition, consuming zero of the cap of eight changed-file warning dispositions. The scan checks local links and supported text rules, not external sources or scientific correctness.

The first cold review is comprehensive against `f122d30669b92b80f6c0fef31a3dd01ce9a38258`, reviewing the commit containing this final receipt/state. This component has no prior review; the first pass consumes comprehensive round one under the execution-card limits. Freeze the complete authored diff before review. The final report, dispositions, cumulative counts and final-head hosted evidence belong in the PR self-review record, not in this receipt's own reviewed input.

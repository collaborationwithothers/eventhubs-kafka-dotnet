# Packet-summary event queue receipt

This PR adds a bounded in-memory handoff for already-derived events. It acquires no packets and makes no completeness or public-result judgment.

## Execution card

- Deliverable: the exact five paths and fixed test matrix in [Bound the packet-summary event queue, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6623743554), updated 2026-11-04T05:54:20Z.
- Authorization: the [standing investigation decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6596595315), [approved design](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6615675298) and [fresh independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6623779978). The check identifies this revision, states `Blocking findings: none` and has no apply-in-implementation findings; the plan has no open questions.
- Base: `cbbff703d2d996d1311a19c673b73265146a5082`, containing the merged core and file components. Existing source, tests, projects, contracts and runtime routes remain unchanged.
- Mandatory gates: the plan's Acceptance and publication checkpoint; AGENTS.md GOVERNANCE and Before opening a pull request; docs/agents/pull-requests.md; factory.md Authority reads, Item lifecycle and Escalation.
- Exact commands: `PATH=/workspace/scratch/dotnet:$PATH ./scripts/verify-dotnet.sh`; `npm run check:docs` with locked dependencies already installed; `git diff --check`; `python3 /workspace/scratch/issue334/queue-scope.py` for exact paths, ASCII and source/count calculations. Required Linux/Docker prerequisites remain present. No live or Azure command belongs to this component.
- Earliest normal push/PR: successful acceptance, applicable cold-review coverage and refreshed authority. Hosted final-head CI and code/security reviews remain required. Earlier merge-method choices do not select a method for this PR.
- Execution budget: at most 12 distinct compiler/analyzer/test failure root causes and 8 changed-file documentation-warning dispositions; each exhaustion action is `push -wip and hand off to the next session`. Unchanged-file warnings use the rule-8 blanket disposition. Execute only the fixed matrix; repeat verification only for the causes allowed by rule 13.
- Cold-review budget: docs/agents/pull-requests.md rule 8 says "At most two comprehensive passes run per PR across all sessions" and "at most two focused correction-review rounds". "When another required review would exceed the budget, or when focus is unsafe, save unpublished work under rule 12 and hand off to Hari." This queue component has used no review yet; previous components' budgets do not reset or transfer here.
- Predicate map: verifier mapping is not applicable; no verifier predicates change. The queue behavior-to-test map is recorded before cold review.
- Models: Codex implementing session, separate high-effort production/test agents, and fresh high-effort planning/checking agents. Exact model identifiers were not exposed.

## Specification comparison and verification

Source/test commit: `17d0c2d4e6cbfa3a6a1c486431cf654e013fe42b`, based on the main commit in the execution card. The queue implements the selected fixed ring and exact state/result API, delegates admission to the unchanged real encoder, and preserves first-failure clearing with no downstream Item after fault. The [queue contract](../../../idle-connection-capture-queue-contract.md) records these software boundaries and leaves scientific interpretation with the approved design/verifier.

All source claims were compared against the approved plan and the source at the base: `PacketCaptureTypes.cs` supplies the immutable derived value; `PacketSummaryEncoding.TryEncodeEvent` supplies validation and byte-bounded encoding; `PacketSummaryStreamWriter` supplies the separate total-event limit. The approved design's Selected resource and capture bounds supplies the resident-entry policy. No .NET/library/platform behavior beyond the implementation's own contract is newly claimed.

Tests use synthetic derived events and in-memory streams only. Filtering, acquisition, flow generation, runtime shutdown/drain ordering, actual-image/live proof and verifier-controlled publication remain later work. No packet, credential, filesystem or network input is used by the new queue tests.

## Behavior-to-test map

All references below are in [PacketSummaryEventQueueTests.cs](../../../../../tests/Parchwell.LoadGen.Tests/IdleConnectionCapture/PacketSummaryEventQueueTests.cs).

| Plan behavior | Proving tests |
|---|---|
| Default/accepted/rejected capacity and empty behavior | `Given_DefaultQueue`, `Given_AllowedTestCapacity`, `Given_DisallowedTestCapacity`. |
| FIFO and wraparound | `Given_FullRingWithRemovedPrefix` asserts each exact event and count through removal/reuse. |
| Resident saturation, terminal clearing and capacity reuse | `Given_ProductionResidentBoundary`, `Given_CapacityOne_When_AppendingSecondResidentEvent`, and `Given_CapacityTwo` cover admission below/at/above the resident limit and repeated reuse. |
| Syntax/state/capacity precedence and first failure | `Given_QueuedValidEvent`, `Given_FullQueue`, `Given_SealedQueue`, `Given_ExistingEncodingFailure`, and the shared `AssertTerminal` require no later Item, unchanged first code and zero count. |
| Seal, drain final Item, repeated seal and Abort | `Given_EmptyQueue`, `Given_TwoQueuedEvents`, `Given_UnfinishedQueue`, `Given_CompletedQueue` assert separate Item and state transitions. |
| Exact value/encoding preservation | `Given_ValidDerivedValues` includes minimum/maximum flow, both directions, zero/all flags and maximum elapsed value; actual encoded bytes match before/after queueing. A legal event need not reach the policy byte ceiling. |
| Real writer composition and exact schema | `Given_SealedSyntheticEvents` drains to the real stream writer, verifies queue completion while writer remains Active, then completes/parses the writer output and asserts every envelope/event key and synthetic value. |
| Cross-task coordination | `Given_CapacityOne_When_TasksExchangeEightEvents` uses the planned bounded sequence, shared cancellation and paired test-only permits, joining workers before disposing synchronizers. It proves no exhaustive scheduler or throughput property. |

## Verification

On 2026-11-04, `PATH=/workspace/scratch/dotnet:$PATH ./scripts/verify-dotnet.sh` passed against source/test commit `17d0c2d4e6cbfa3a6a1c486431cf654e013fe42b`. SDK 10.0.402, locked restore, Release build/analyzers, architecture/effective-settings checks, Kafka XML inventory, fresh report/spike-evidence validation and warning control all passed. The intentional compiler-warning fixture failed as required, and its valid counterpart built.

Python parsed the fresh xUnit reports in `/tmp/parchwell-dotnet-verification.rvXFH7J5`, checked zero failed/skipped/not-run/errors, and summed the passed counts:

| Suite | Passed |
|---|---:|
| Parchwell.ArchitectureTests | 15 |
| Parchwell.Enrichment.Tests | 56 |
| Parchwell.Kafka.Tests | 486 |
| Parchwell.LoadGen.Tests | 1027 |
| Parchwell.Scenarios.Tests | 331 |
| Parchwell.Verifier.Tests | 5005 |
| Total, computed | 6920 |

Filtering report test types measured 32 queue cases, all passed. This was the initial verification attempt; no compiler/analyzer/test root-cause corrections were needed, so zero of the approved 12 dispositions are consumed. The tests establish the stated synthetic software behavior, not runtime capture completeness.

`python3 /workspace/scratch/issue334/queue-scope.py` compares the full diff from the base with the exact five-path set, checks that only the state file already existed, checks authored Markdown ASCII, and reads the source constants to confirm 256 resident entries, 256 serialized event bytes and the separate 65536 writer-event bound. It passed, as did `git diff --check`. No program output is committed.

`npm run check:docs` completed after receipt/state preparation with eight advisory warnings, all in unchanged files, and zero changed-file warnings. A Python log parser counted the warnings and compared their file paths with the diff. They stand under rule 8's blanket disposition; zero of the approved eight changed-file warning dispositions are consumed. External URLs are not checked by that scanner. The first cold review is comprehensive against `cbbff703d2d996d1311a19c673b73265146a5082`, reviewing the commit containing this finalized receipt and state. There is no prior queue review; this first pass consumes comprehensive round one under the execution-card limits. Freeze all authored input before review. Complete reports, dispositions, cumulative counts and final-head hosted evidence belong in the PR self-review record rather than this receipt's own reviewed input.

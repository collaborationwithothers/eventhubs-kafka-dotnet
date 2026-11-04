# Packet-capture counter receipt

This PR accumulates submitted packet-statistics samples and preserves partial diagnostic totals after failure. It reads no socket and makes no completeness or public-result judgment.

## Execution card

- Deliverable: exact five paths and fixed synthetic test matrix in [Accumulate packet-capture statistics, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6626175155), updated 2026-11-04T08:23:04Z.
- Authorization: [standing investigation decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6596595315), [design approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6615675298), and [fresh independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6626231782). The check identifies this revision, states `Blocking findings: none`, and has no apply-in-implementation findings. The plan has no open questions.
- Base: `b23314903a37e57294e1f417155a0f0450edcbec`, containing the merged queue. Existing source, tests, projects, contracts and runtime routes stay unchanged.
- Mandatory gates: plan Acceptance and publication checkpoint; AGENTS.md GOVERNANCE and Before opening a pull request; docs/agents/pull-requests.md; factory.md Authority reads, Item lifecycle and Escalation.
- Exact commands: `PATH=/workspace/scratch/dotnet:$PATH ./scripts/verify-dotnet.sh`; `npm run check:docs` with locked dependencies present; `git diff --check`; `python3 /workspace/scratch/issue334/counters-scope.py` for path/ASCII/arithmetic acceptance. Existing local Linux/Docker prerequisites are available. No Azure or live-acquisition command belongs to this component.
- Earliest normal push/PR: successful acceptance, applicable cold review and refreshed authority. Hosted final-head CI and code/security reviews remain required. Prior merge choices do not select this PR's method.
- Execution budget: at most 12 distinct compiler/analyzer/test failure root-cause corrections and 8 changed-file documentation-warning dispositions. Both exhaustion actions are `push -wip and hand off to the next session`. Unchanged-file warnings take the rule-8 blanket disposition. Run only the fixed matrix; verification repeats require a rule-13 cause.
- Review budget: docs/agents/pull-requests.md rule 8 says "At most two comprehensive passes run per PR across all sessions" and "at most two focused correction-review rounds". "When another required review would exceed the budget, or when focus is unsafe, save unpublished work under rule 12 and hand off to Hari." This component has no prior cold review. Earlier component budgets remain separate and do not reset.
- Predicate map: verifier mapping is not applicable; this is capture-time arithmetic. The behavior-to-test map is prepared before cold review.
- Models: Codex implementing session with a high-effort source/test subagent; separate fresh high-effort planner and checker. Exact model identifiers were not exposed.

## Specification comparison and verification

Source/test commit: `9ba1449446b39438e115721bc23bbda46693e734`, based on the main commit in the execution card. The implementation follows the plan's exact scalar API and shared addition helper: state first, then independent subtraction guards before any update, then a single sample-count increment and optional completion. FailRead and Abort retain accepted diagnostic totals and the first fixed failure.

The [counter contract](../../../idle-connection-capture-counter-contract.md) cites the approved design and both pinned Linux sources. The implementing session independently read `packet_getsockopt` in Linux commit `adc218676eef25575469234709c2d87185ca223a`, `net/packet/af_packet.c`: lines 4142-4157 copy/reset counters, exchange drops and add them to returned packets; lines 4240-4243 can fail copy-out afterward. In the same commit, `include/uapi/linux/if_packet.h` lines 77-80 define the ordinary statistics fields. This supports independent reported-field summation and the missing-read distinction. It proves neither deployed-kernel behavior nor native decoding/layout.

Samples are synthetic caller inputs, not captured/native evidence. Sampling cadence, socket ownership, native decoding, runtime integration, actual-image/live proof and verifier-controlled publication remain later work.

## Behavior-to-test map

All references are in [PacketCaptureCountersTests.cs](../../../../../tests/Parchwell.LoadGen.Tests/IdleConnectionCapture/PacketCaptureCountersTests.cs).

| Plan behavior | Proving cases |
|---|---|
| Initial state and positive constructor ceilings | `Given_DefaultCounters`, `Given_PositiveCeilings`, `Given_ZeroCeiling`. |
| Independent reported fields and repeated/zero samples | `Given_IndependentSamples`, `Given_IdenticalSamples`, `Given_MoreDropsThanPackets`, `Given_ZeroSamples`. |
| Production accumulation beyond one native field's range | `Given_MaximumUnsignedSamples` asserts literal unsigned totals before and after explicit completion. |
| Each arithmetic ceiling below/at/above with unrelated counters valid | `Given_PacketCeiling`, `Given_DropCeiling`, `Given_SampleCeiling`. |
| Final-add atomicity and exactly-at-ceiling completion | `Given_AcceptedSample`, `Given_MinimumCeilings_When_FinalSampleReachesAllCeilings`, `Given_MinimumCeilings_When_FirstFinalSampleExceedsCeiling`. |
| Explicit final zero and completed-state precedence | `Given_NoPriorSample`, `Given_CompletedCountersAtCeilings`, `Given_CompletedCounters_When_AbortingRepeatedly`. |
| Failed/missing read and abandonment preserve partial totals | `Given_ActiveCounters_When_ReadFails`, `Given_ActiveCounters_When_Aborting`. |
| Every first failure survives all subsequent operations | `Given_FirstFailureAtCeilings` and shared `AssertTerminal` assert state/code and all totals after each later operation. |
| No unrelated queue-capacity limit on sample traffic | `Given_257Samples` uses the fixed bounded accumulation loop and final sample. |

## Verification

On 2026-11-04, `PATH=/workspace/scratch/dotnet:$PATH ./scripts/verify-dotnet.sh` passed against source/test commit `9ba1449446b39438e115721bc23bbda46693e734`. SDK 10.0.402, locked restore, Release build/analyzers, architecture/effective-settings checks, Kafka XML inventory, fresh report/spike-evidence validation and warning control all passed. The intentional compiler-warning fixture was rejected; its valid counterpart built.

Python parsed the fresh xUnit reports in `/tmp/parchwell-dotnet-verification.ir1LTnnK`, checked zero failed/skipped/not-run/errors, and summed the passed counts:

| Suite | Passed |
|---|---:|
| Parchwell.ArchitectureTests | 15 |
| Parchwell.Enrichment.Tests | 56 |
| Parchwell.Kafka.Tests | 486 |
| Parchwell.LoadGen.Tests | 1061 |
| Parchwell.Scenarios.Tests | 331 |
| Parchwell.Verifier.Tests | 5005 |
| Total, computed | 6954 |

Filtering report test types measured 34 counter cases, all passed. This initial full verification needed no compiler/analyzer/test root-cause correction, so zero of the approved 12 dispositions are consumed. Inputs remain synthetic; software acceptance is not native-read or capture-completeness evidence.

`python3 /workspace/scratch/issue334/counters-scope.py` compared the base-to-working-tree diff with exactly the five planned paths, required the state file to be the only previously existing path changed, checked authored Markdown ASCII, and calculated the synthetic expected values. Twice the maximum unsigned 32-bit sample is 8589934590; the fixed 257-addition loop plus final sample is 258; the sample sequence produces `(10,2,1)`, `(13,3,2)`, `(13,3,3)` and `(15,3,4)`. The scope/arithmetic checks and `git diff --check` passed. No program output is committed.

`npm run check:docs` completed after receipt/state preparation with eight advisory warnings, all in unchanged files, and zero in changed files. A Python log parser counted warnings and compared their paths against the diff. They stand under rule 8's blanket disposition, consuming zero of the approved eight changed-file warning dispositions. External URLs are not checked by the scanner; the source reads above verify the pinned claims. The first cold review is comprehensive against `b23314903a37e57294e1f417155a0f0450edcbec`, reviewing the commit that contains this final receipt/state. There is no prior counter-component review; this pass consumes comprehensive round one under the execution-card limits. Freeze the full authored diff before review. Complete reports, dispositions, cumulative counts and final-head hosted evidence belong in the PR self-review record rather than this receipt's own reviewed input.

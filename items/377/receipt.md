# B10 two-instance judge receipt

This change lets the phase judge bind both B10 producer instances and retain their independently accepted intake evidence. B10 remains invalid or incomplete, with outcome counts absent until the later judgement and sweep work lands.

## Execution card

- Deliverable: changes 1-8 of [plan Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/377#issuecomment-6644650353), based on source `4b47da2fcc078303122a79e84a0034d815983cf2`.
- Authorization: [standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/377#issuecomment-6644760751), with no implementation corrections; adopted decisions and tool assignment are indexed in [state](state.md).
- Mandatory gates: plan acceptance commands; applicable cold review; final-head CI and hosted Codex review. The control session obtains the separate Claude review; Hari merges the workflow change. Postmerge C5 on main remains required before scientific evidence publication.
- Exact verification commands: `./scripts/verify-dotnet.sh`; `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_*.py' -v`; `npm ci`; `npm run check:docs`; `git diff --check`; the plan's static workflow/target comparison. Development verification follows the plan and AGENTS.md rule 13.
- Publication checkpoint: publish the tested change after applicable clean cold review, run hosted CI/review, then mark ready after the final-head gates pass. This session does not merge.
- Runtime limits: plan Bounded work caps changed-file documentation dispositions at 20 and finding root causes at 30, with its stated backup/handoff action. Cold review permits at most two comprehensive and two focused rounds across sessions; exhaustion or unsafe focus follows the rule-8 handoff to Hari. No local mutation campaign or live run is authorized here.

## Verification

Local acceptance completed on the authored changes based on source `4b47da2fcc078303122a79e84a0034d815983cf2`. SDK 10.0.402 is selected with `PATH=/tmp/dotnet:$PATH DOTNET_ROOT=/tmp/dotnet`; installed actionlint is 1.7.12. Logs remain outside the repository in `/tmp/issue377/`.

| Command | Result |
| --- | --- |
| `./scripts/verify-dotnet.sh` | Exit 0; locked restore, solution build, XML inventory, scaffold inventory and warning-as-error control passed. The XML reports total 7947 passed, zero failed/skipped/errors: Architecture 15, Enrichment 56, Kafka 486, LoadGen 1180, Scenarios 336 and Verifier 5874. |
| `npm run check:docs` | Exit 0; eight warnings in unchanged files only. |
| `git diff --check` and `git diff --cached --check` | Exit 0. |
| Initial `dotnet restore Parchwell.slnx --locked-mode` | Exit 0. |
| `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_*.py' -v` | Exit 0; 78 tests passed. Synthetic tooling only, not mutation acceptance. |
| `npm ci` | Exit 0. |
| `/tmp/issue377/actionlint .github/workflows/verifier-c5-mutation.yml` | Exit 0. |
| Approved static workflow/target comparison | Exit 0: 11 targets and 22 shards; the workflow differs only by the appended matrix row. Configuration and documentation inventories agree. |

The initial mutation-tooling run exposed the default projection fixture's old source cardinality. Updating its below/at/above bounds from 9/10/11 to 10/11/12, as the plan permits for derived fixture populations, restored the same rejection/acceptance boundary. All 78 tooling tests passed on the corrective run. Scripted evaluation of the explicit fixture populations confirmed the plan's numerator/denominator values. This is one correction root within the plan's cap; no production scoring or assertion purpose changed.

The central development verifier run passed 5873 tests with zero failures, skips and runner errors, including the Kafka/Toxiproxy component cases (`/tmp/issue377/development-3/results.xml`). Earlier development builds stopped before tests for local-name collisions and CA1861 constant-array fixtures; both were corrected. Pure helper fixture identities and C0/C1, repeat 1/5 coverage were aligned with the plan. A listed-but-unavailable registration-blob case was then added and passed in the full verification. The full reports are under `/tmp/parchwell-dotnet-verification.eCwqJfcq/`; counts were computed from their assembly attributes. All 5 correction roots are resolved within the plan cap.

`npm run check:docs` exited 0 with eight warnings, all in unchanged files. These existing warnings remain outside this change. The static workflow comparison and actionlint passed. Full repository .NET verification and documentation/whitespace checks passed. No live B10 run or postmerge mutation acceptance is claimed.

## Predicate map

All test paths below are relative to `tests/Parchwell.Verifier.Tests`. Fixtures are synthetic; each mutation keeps unrelated evidence valid, with enclosing receipt lengths and hashes refreshed where needed.

| Plan check | Tests and isolated boundaries |
| --- | --- |
| 1-2: independent registration discovery and binding | `Judge/B10JudgeChecksTests.cs` covers zero/one/two/three markers, complete/incomplete listings, malformed evidence, duplicate identity, role ambiguity, both discovery orders and each coordinate/source/predecessor mismatch. `Judge/JudgeHandlerTests.cs` exercises missing/malformed/excess wire evidence, a third marker without reading it, excess plus incomplete listing, and independent failure with missing receipt/read failure. The listed-but-unavailable registration row preserves the two-marker listing while returning null bytes, so missing-blob behavior is not masked by a changed registration count. |
| 3: ordered start-line Jobs | `Judge/JudgeHandlerTests.cs`: `Given_B10StartJobList` covers the derived pair, omission, one/three entries, reversal, duplicate and foreign second Job with current receipt hashes. |
| 4-6: pair receipt/result equality and independent publication acceptance | `Judge/B10JudgeChecksTests.cs` exercises separately allocated equal lists, both-null/one-null snapshots, every snapshot/reference/object comparison, reversed roles and producer counts 0/1/2/3. `Judge/JudgeHandlerTests.cs`: `Given_B10ReceiptResultDisagreement` denies J3/status projection for mismatched pair evidence; `Given_OneB10PublicationUnavailable` independently denies J2 for each producer's null snapshot, missing/mismatched completion or bad start/inventory/completion references. The five-real-verifier-output case establishes J1-J6 success with two snapshots and nonfinal exit 1; read-failure cases preserve independently known false. |
| 7: explicit-scenario nonfinality and evidence projection | `Judge/PhaseJudgeTests.cs`: `Given_B10WithStaleCounts` covers C0/C1, intake present/absent, stale counts and invalid precedence. `Given_B10UntrustedPredicate` covers each start/publication/receipt false and each Job/count/inventory false/null. `Given_B10SlotOnlyFailureOrDuplicate` covers retained intake on a slot-only failure and omitted intake on duplicate selection. `Given_B10FirstOnlyObservations` suppresses every first-only measurement and prevents intake projection for non-B10. |
| 8: closed candidate intake and nonfinal wire contract | `Judge/PhaseCandidateCodecTests.cs`: pair roundtrip covers C0/C1 and ordinals 1/5 with both snapshots; omission remains valid. Read/write finality cases reject root/repeat complete status and literal zero counts with intake present/absent. Typed/wire malformed cases cover enclosing coordinates/identity, producer counts 0/1/3 around the valid pair, roles/predecessor, fixed paths/source, explicit null and unknown/private fields. Nested duplicate cases cover intake, producer and snapshot. Existing size/privacy/non-B10 assertions remain. |
| 9: C5 integration | `Mutation/test_c5_workflow.py` checks exact target/config/matrix agreement, aggregate population arithmetic and source-cardinality boundaries below/at/above 11. The static acceptance script additionally compares every non-matrix workflow value with the source commit. |

B10 phase/configuration mapping is C0/C0 or C1/C1. A mismatched intake phase can therefore fail shared configuration validation before the enclosing-phase comparison; isolated success past that earlier gate is unreachable. Valid C0 and C1 pairs round-trip, and pure registration/intake comparison tests cover coordinate disagreement without wire-parser masking.

## Specification comparison

Plan Revision 1 changes 1-8 are implemented: independent B10 registration judgement; two-producer handler acceptance; explicit-scenario nonfinal phase aggregation; closed candidate intake transport; synthetic pure and real verify-to-judge tests; the new C5 target and matrix entry; the approved verifier-contract updates; and the item records. The standing-approval check supplied no implementation corrections. No later decision changed the plan at the implementation refresh.

The runtime changes stay in the existing Judge slice. No new project, reference, package, expected outcome, scenario, result, verdict or workflow trigger is introduced. Existing assertions remain; mutation-tooling literals change only to include the additional target population and preserve their original boundary/purpose. B10-specific guard/retry judgement, final classification, earlier-repeat exclusion and the late-write scan remain successor work. Final-head hosted CI and Codex review, the separate Claude review, Hari's workflow merge and postmerge mutation acceptance remain distinct gates.

## Cold review

Round 1 is a comprehensive review of the full authored diff. Its input revision is the frozen staged tree identified in `/tmp/issue377/review-1-tree.txt`, compared with source `4b47da2fcc078303122a79e84a0034d815983cf2`; the corresponding diff is `/tmp/issue377/review-1.diff`. Cumulative comprehensive rounds at launch: 1; focused rounds: 0. The complete final report will be recorded in the PR self-review record, outside its own reviewed receipt.

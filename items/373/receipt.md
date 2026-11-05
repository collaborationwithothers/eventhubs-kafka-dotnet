# B10 per-repeat judgement receipt

This change applies the approved B10 timeout and retry rules to both producer publications. Published results remain nonfinal and omit authoritative outcome counts until the later phase sweep.

## Execution card

- Deliverable: exact changes 1-7 of [plan Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/373#issuecomment-6651019638), based on source `eb0fd9bf6563daf8dd44d20569090b0eb6746549`.
- Authorization: [standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/373#issuecomment-6651118233), including both exact apply-in-implementation corrections, and [Hari's scoped test decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/373#issuecomment-6650981348). Authority is indexed in [state](state.md).
- Mandatory gates: the plan's local acceptance commands, applicable cold review, final-head hosted CI and completed Codex review. The separate Claude review precedes merge; Hari merges the workflow change. Postmerge hosted C5 acceptance on main remains required before scientific evidence publication.
- Verification commands: `./scripts/verify-dotnet.sh`; `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p test_c5_workflow.py`; `npm run check:docs`; `git diff --check`; `actionlint .github/workflows/verifier-c5-mutation.yml`; and the plan's scripted target/matrix/source comparison.
- Earliest publication checkpoint: after local acceptance and clean applicable cold review, publish a draft and run hosted CI and Codex review together; mark ready only when the final-head gates pass. This session does not merge the protected workflow change.
- Workload limits: plan Acceptance commands and evidence permits 12 distinct mandatory-verification root causes and 2 focused reproductions per cause, plus 20 changed-file documentation warning dispositions. Either exhaustion pushes `-wip` and hands off to the next session. Cold review has the cumulative rule-8 budget of at most two comprehensive and two focused rounds; exhaustion or unsafe focus hands off to Hari. No mutation campaign or live run is included.

## Specification comparison

Plan Revision 1 changes 1-7 are implemented. The new rule owner validates timeout and guard-marker backing, latest-earlier retry eligibility and independent predecessor-plan selection, then composes both ledgers' in-window classifications. The handler publishes only the nonfinal aggregate, and the passive codec accepts a supplied finality marker only as false for exact B10. Existing receipt binding and independent broker coverage remain the source of `RepeatChecks`.

Both plan-check corrections are applied: new check-4 invalidity refreshes producer status while preserving publication rejection; the first producer's persistence-0 acceptance fixture removes its now-unbacked persistence marker. The named interim rejection theory is replaced under Hari's explicit decision, while every assertion in the other existing named host tests remains byte-identical by scripted comparison. Synthetic population arithmetic changes preserve each mutation-tooling test's existing purpose.

The code adds no project, assembly reference, NuGet package, scenario, expected outcome or verdict. Workflow triggers are byte-preserved; only the approved mutation matrix row is appended. Earlier-repeat exclusion, the phase sweep and final outcome publication remain successor work.

## Verification

Local acceptance passed on the authored changes based on source `eb0fd9bf6563daf8dd44d20569090b0eb6746549`. SDK 10.0.402 is selected with `PATH=/tmp/issue373/dotnet:$PATH DOTNET_ROOT=/tmp/issue373/dotnet`; actionlint 1.7.12 is `/tmp/issue373/actionlint`. Logs and generated reports remain outside the repository in `/tmp/issue373/` and `/tmp/parchwell-dotnet-verification.an1EkD31`.

| Command | Measured result |
| --- | --- |
| `./scripts/verify-dotnet.sh` | Exit 0. Locked restore, Release build/analyzers, XML inventory, effective settings, mandatory reports and warning-as-error control passed. XML reports total 8302 passed, zero failures/skips/runner errors: Architecture 15, Enrichment 56, Kafka 486, LoadGen 1180, Scenarios 336 and Verifier 6229. Counts were computed from assembly report attributes by script. |
| `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p test_c5_workflow.py` | Exit 0; 78 synthetic tooling tests passed. This is not mutation acceptance. |
| `/tmp/issue373/actionlint .github/workflows/verifier-c5-mutation.yml` | Exit 0. |
| Plan static target/matrix/source comparison | Exit 0: 12 targets, 23 shards; workflow differs from the source only by the appended matrix row. |
| Synthetic population arithmetic script | Exact prescribed target totals and aggregation boundaries agree with the fixture populations. |
| Scripted original-host-assertion comparison | Every existing named host-test assertion remains byte-identical except the expressly authorized replacement. |

Development verification first stopped at test-code analyzer diagnostics CA1861 and CA1859; the repeated constant arrays and helper parameter type were corrected without suppressions. A new test's prerequisite expectation was aligned with the existing bookkeeping gate before tests ran. The affected run then passed 989 of 991 cases; the two failures were new outside-B10 fixtures missing the required final JSONL newline. After correcting that writer and auditing the other new JSONL writes, both cases passed. The full mandatory entry point above then passed. These are four resolved correction roots, within the plan's cap; no root consumed more than one focused reproduction.

Main refreshed during verification to `17233dd7deb380b26eb15e17f4615255c306ef10`. Its only difference from the pinned source is the unrelated `items/381/state.md` update. The branch fast-forwarded before review; no verified code or test input changed. No live B10 result or postmerge mutation acceptance is claimed.

`npm run check:docs` exited 0 with eight warnings, all in unchanged files; those pre-existing warnings remain outside this PR. Both changed operations documents and the item receipt/state reported no issues. Final unstaged/staged whitespace checks passed before the review snapshot.

## Predicate map

Test paths below are relative to `tests/Parchwell.Verifier.Tests`. Fixtures are synthetic; pure requests deliberately isolate the owning predicate, and host mutations refresh descriptors and references.

| Changed predicate | Tests and boundaries |
| --- | --- |
| Timeout conjunction and exact scenario | `Judgement/B10RepeatChecksTests.cs`: `Given_TimeoutConjunction`, `Given_IdenticalTimeoutAcrossScenarioIds` and empty/missing-ledger cases cover callback/source case, delivered flag, errors -193/-192/-191 and missing/null/-1/0/1/2 persistence. `Host/B10TwoInstanceVerifyTests.cs` replaces the named interim theory for both producers and statuses 0/1. `Host/B10RepeatVerifyTests.cs`: `Given_IdenticalPermittedTimeoutOutsideExactB10` proves the identical row still fails check 12 in A01; pure lowercase-b10 cases prove exact matching. |
| Marker backing and guard completeness | Pure `Given_RowGuardMarker`, `Given_ExactCapturedCounterMarker`, `Given_CounterMarkerWithoutExactReadableBacking` and `Given_BackedMarkersAndIncompleteCapture` cover absent/empty/mixed evidence, exact native names including embedded separators, observed values 0/1/2, wrong values/names/prefixes, standing markers and incomplete observations. Host counter/marker and incomplete-capability cases exercise either producer. |
| Counter and native-error boundaries | Pure `Given_B10CountersAroundZero`, `Given_MissingOrNullCounter`, earlier-nonzero/later-zero and late-nonqualifying-report cases cover -1/0/1 and independent structure/history. Native `IsError` false/true remains check 16. Host cases keep txerrs/rxerrs and error-level native callbacks invalid with otherwise permitted timeout evidence. |
| Retry invocation order and prerequisites | Pure three-invocation, within-either-instance, cross-instance/reversed-clock, synchronous-rejection predecessor, different-key/sequence and shuffled-ledger cases require the immediately preceding permitted timeout. Prerequisite-boundary and malformed-bookkeeping cases prevent unsafe joins when any check 1-6 is unavailable/failing. New retry failures retain check 4, aggregate invalidity and null internal counts. |
| Restart selection and logical slots | Pure expected/actual count cases cover 0/1/2, every copied field, shared/role identity, reversed lists, latest-attempt replacement and ascending logical-slot order. Canonical-slot cases cover sequence -1 through 5, exact event/key/document shape and checked Int64 slot overflow below/at/above the boundary. Host predecessor-ID and planned-time mismatches remain parser-valid and independently fail check 4. |
| Combined classification and status precedence | Pure `Given_CombinedOutcomeOnlyFixture` checks `OutcomeCounts(2,1,1,1,1,2)` through the existing classifier over both ledgers. Separate same-bare-attempt-ID and empty-pair cases exercise `Evaluate`, composite joins, duplicate/tie behavior, six internal zeros and absent published counts. Publication-status retention and existing check-4 retention cases cover invalid/incomplete/unavailable precedence. |
| Diagnostic limits and independent receipt evidence | Pure capped retry diagnostics cover caps 1/2/3, both producer paths, common window diagnostics, embedded fidelity and UTF-8 boundaries without dropping failed numbers. Host `Given_OnlyOneProducerLacksPassingBrokerCoverage` verifies fail/unavailable from either producer cannot be replaced by the peer's pass; permitted-counter cases establish pair coverage pass, cleanup-before-create and receipt availability only after return. |
| False-only finality codec | `Results/VerifierResultCodecTests.cs` covers B10 invalid/incomplete false round trips; historical omission; rejection of true/null/number/string/array/object; non-B10 presence; complete-valid/count rejection; and byte/entry budgets below/at/above the marker's contribution. |
| Mutation integration | `Mutation/test_c5_workflow.py` compares configured targets and ordered matrix rows, fixture-derived source-cardinality boundaries and exact synthetic score arithmetic. The source comparison additionally proves the workflow differs only by the appended row. |

Unreachable or layered cases: a wire ledger omitting persistence status is rejected by parser check 1 before guard evaluation; the host test asserts that invalidity, while a typed missing field isolates check 13 and explicit JSON null reaches checks 12/13. An existing failed or unavailable bookkeeping check 4 prevents retry/restart evaluation, so new retry invalidity cannot coexist with that prerequisite state; retention tests instead preserve the original strongest outcome and an unavailable additional assessment. Defensive logical-slot and malformed planned-list cases that parsing would reject earlier use typed requests with supplied pass assessments to exercise the new helper; host tests retain parser-local check-2 ownership. The mixed-classification literal fixture is outcome-only, as the plan permits; separate reconciliation cases prove internal-count eligibility.

## Cold review

Round 1 is a comprehensive review of the full authored staged diff against `17233dd7deb380b26eb15e17f4615255c306ef10`. The frozen tree is recorded outside the repository in `/tmp/issue373/review-1-tree.txt`; its diff is `/tmp/issue373/review-1.diff`. Cumulative comprehensive rounds at launch: 1. Focused rounds: 0. The complete report will be recorded in the PR self-review record, outside its own reviewed receipt. Required final-head hosted gates remain separate.

# B10 two-instance verify receipt

This change binds and checks both B10 producer publications independently. Every B10 result remains invalid or incomplete until the successor judgement and sweep work lands.

## Execution card

- Deliverable: [approved plan Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/376#issuecomment-6639646060), changes 1-8, on source `3f4b29e79883ec520eaed78bbf4390b3b7abeb5b`.
- Authorization: [standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/376#issuecomment-6639746016), including its exact fixture correction; issue tool assignment and adopted decisions indexed in [state](state.md).
- Mandatory gates: plan Verification and acceptance; cold review under docs/agents/pull-requests.md rule 8; final-head CI and hosted Codex review. The control session starts the separate Claude review. Hari merges the mutation workflow change. Postmerge C5 acceptance is pending and cannot be replaced by local tooling checks.
- Exact verification commands: `./scripts/verify-dotnet.sh`; `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_*.py'`; `npm run test:docs`; `npm run check:docs`; `actionlint .github/workflows/verifier-c5-mutation.yml`; `git diff --check`; the plan's exact static integration script.
- Publication checkpoint: publish after local acceptance and applicable clean cold review, then run hosted CI and Codex review together. Mark ready after those final-head gates pass. No merge by this implementation session.
- Runtime work limits and exhaustion: plan Bounded work, including 20 warning roots, 20 defect roots, and three failing reruns per command after its initial run; exhaustion pushes `-wip` and hands off. Cold review has at most two comprehensive and two focused rounds across sessions, with the rule-8 handoff to Hari on exhaustion or unsafe focus.

## Verification

Before the first cold review, all local acceptance commands passed on the authored implementation based on source `3f4b29e79883ec520eaed78bbf4390b3b7abeb5b`, before the first cold review. SDK 10.0.402 was selected explicitly with `PATH=/tmp/dotnet:$PATH` and `DOTNET_ROOT=/tmp/dotnet`; `MSBUILDDISABLENODEREUSE=1` disabled reuse after one environmental build-server failure.

| Command | Result |
| --- | --- |
| `./scripts/verify-dotnet.sh` | Exit 0: locked restore, Release warnings-as-errors build, Kafka XML inventory, effective project manifests, all test reports and intentional CS1030 warning-control rejection passed. |
| `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_*.py'` | Exit 0; 78 tests passed. Synthetic tooling only, not mutation acceptance. |
| `npm run test:docs` | Exit 0; 28 tests passed. |
| `npm run check:docs` | Exit 0; eight advisory warnings in unchanged files, none in changed files. These unchanged-file warnings remain outside this change. |
| `actionlint .github/workflows/verifier-c5-mutation.yml` | Exit 0 with repository-pinned actionlint 1.7.12. |
| `git diff --check` | Exit 0. |
| Approved static integration script | Exit 0: ten targets, 21 shards, workflow-only matrix addition verified. |

The full script's checked reports contain 7752 passing tests, zero failures and zero skips:

| Assembly | Passed |
| --- | ---: |
| `Parchwell.ArchitectureTests` | 15 |
| `Parchwell.Enrichment.Tests` | 56 |
| `Parchwell.Kafka.Tests` | 486 |
| `Parchwell.LoadGen.Tests` | 1180 |
| `Parchwell.Scenarios.Tests` | 336 |
| `Parchwell.Verifier.Tests` | 5679 |

The verifier report includes all 400 B10 cases. A targeted initial run passed 398 and failed two entry-budget cases because a per-document limit masked the aggregate boundary. The corrected synthetic fixture preserves the assertions, and all 400 passed on rerun. A separate full verifier run also passed all 5679 cases. Test names were aligned with the repository convention and the test project rebuilt successfully before the full script reached the verifier suite.

Other corrections before acceptance: synthetic mutation source cardinality after adding the target; constant-array and single-item-assertion analyzer fixes; timeout fixture persistence; contract wording consistency. The first solution build had analyzer errors; the next build failed with an MSBuild child-node termination; after shutting down build servers, the build passed with zero warnings/errors. The full verification script passed its initial run. No command exceeded the plan's three-failing-rerun cap. Counting the build-server recovery as a separate root, eight correction roots and zero changed-document warning roots remain within the plan's caps.

Local logs and XML are retained outside the repository in `/tmp/issue376/` and `/tmp/parchwell-dotnet-verification.rb3WMeKc/`; they are not committed program output. Final-head hosted CI and review evidence will be linked in the PR description. No live B10 run or postmerge C5 mutation acceptance is claimed.

After the first cold review's retained-buffer coverage correction, the verifier test project rebuilt with zero warnings/errors. The following targeted command passed all 406 B10 cases, including the six added retention boundaries, with zero failures/skips:

```sh
dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-build --minimum-expected-tests 1 --results-directory /tmp/issue376/b10-retention -- --filter-class '*B10*' --report-xunit-xml --report-xunit-xml-filename results.xml
```

The prior full-suite evidence above remains for unchanged production code and existing tests. The correction raises the cumulative defect-root count to nine, within the approved cap. Documentation and whitespace checks were repeated before the consolidated re-review, with the same unchanged-file warning disposition.

## Predicate map

All paths below are under `tests/Parchwell.Verifier.Tests`. The host fixture keeps each producer's descriptors, completion, manifest and captured evidence consistent except for the named mutation. The entry-budget fixture uses valid monotonic statistics and asserts every document's structural-entry count is below the aggregate boundary, so the per-document parser cannot mask that test.

| Changed or affected predicate | Tests and boundaries |
| --- | --- |
| Exactly two registrations; expected Job order; distinct identity; path, pod, coordinates, source and predecessor binding | `Judgement/B10IntakeChecksTests.cs`: `RegistrationCountAroundTwo` covers 0/1/2/3; `ReversedDiscoveryOrder`, `OneRegistrationBindingMismatch`, `InvalidExpectedJobList`, and `MultipleBindingFailures` cover every field and count/duplicate/Job/coordinate/predecessor precedence. |
| Completion binds each registered Job independently; old context-free restriction remains | `Phase/B10IntakeWireTests.cs`: `EachRegisteredJob`, `CompletionBindingMismatch`, `UnsuccessfulOrMalformedCompletion`, phase/repeat boundaries and context-free second-Job rejection. |
| Start note's ordered pair and shared first-Job identity | `Window/B10RunWindowTests.cs`: round trip; missing/empty/one/two/three, duplicate/reversed/foreign/case Job lists; malformed values; duplicate properties; every original required field; length 0/1/252/253/254 and invalid characters; singleton omission and foreign-scenario rejection. |
| Window identity and failure precedence; copied ordered identities on every return | `Window/B10RunWindowTests.cs`: `EachReturnPath`, `IdentityAndJobDisagreement`, `EndAroundStart` (end below/at/above start). Existing window tests retain partition and half-open interval predicates. |
| Independent publication acceptance, manifest roles and physical predecessor prerequisites | `Host/B10TwoInstanceVerifyTests.cs`: coherent pair; second-only hash/length/completion/manifest/role mutations; missing second payload/index; missing first manifest or ledger with/without diagnostic copies; exact role rejection for first restart, second missing restart and wrong predecessor. |
| Each instance's bookkeeping checks 1-6 and record ownership | Same host file: `OneInstanceBookkeepingFault` exercises both instances and each check; `BothInstancesReuseBareAttemptId` verifies separate joins; foreign, earlier-repeat and missing instance records remain visible for rejection. First-producer acceptance cannot authorize the second. |
| Existing checks 12-27 remain active; no premature final result | Same host file: `PermittedTimeoutRow` fails check 12 for either instance; coherent pair stays incomplete with absent counts and both snapshots; incomplete window plus invalid second ledger keeps invalid precedence. Existing judgement suites run unchanged in full verification. |
| Shared parser-input bytes/entries and consumed records/bytes | Same host file: `SharedInputBudgetAtMeasuredPairBoundary` and `CommonConsumedBudgetAtPairBoundary` test configured limits one below, exactly at and one above measured pair usage, asserting individual producers fit. Input accounting includes diagnostic copies from both producers. Empty second restart is the coherent baseline; unavailable whole publication fails before judgement. |
| Retained publication and diagnostic buffers | Same host file: `RetainedPairBuffersAtTheirOwnBoundary` tests the sum of retained buffers at limit minus one, exact limit and limit plus one, for publications alone and both producers' diagnostic fallbacks. The below-total limit must report the exact retention-guard failure; the other cases reach the later parser-byte guard. The tests assert every retained read in order, individual-object and producer bounds, and that the parser charge before the last retained read fits. Overall success at this retention boundary is unreachable because the same total-input cap also charges registration, completion and start evidence; exact/above retention success is observed by reaching the distinct later parser guard. |
| Aggregated status, assessment precedence, failed-check union and broker coverage | `Judgement/B10IntakeChecksTests.cs`: all status combinations including null; all invalid/incomplete/unavailable/pass orderings and ties; missing assessments; union/attribution; broker failure overrides unavailable and both passes are required. Counts are always absent. |
| Shared window diagnostics once; producer attribution; check-0 marker; check-27 reserved fidelity slot | Same pure test file: window range 6/7/8/9/10/11/12; diagnostic counts below/at/above cap; marker limits 1/2/3; second fidelity and existing first fidelity diagnostics; union independent of truncation. Zero diagnostic slots is unreachable through validated `OutputLimits`. |
| UTF-8 diagnostic truncation | Same pure test file: null/empty; ASCII below/at/above four-byte cap; accented and supplementary scalars crossing the cap. |
| Dual result/receipt wire shape and independent acceptance snapshots | `Phase/B10IntakeWireTests.cs`: strict fields, missing/null/duplicate/unknown extensions; 0/1/2/3 producers; reference order; role/predecessor, identity/source/path/descriptor/status corruption; accepted and null snapshots; B10 complete-valid rejection and verify exit 1; extension forbidden on other scenarios/modes; non-B10 omission. Repeat 0/1/2/4/5/6, instance lengths 0/127/128/129, client lengths 0/252/253/254, acceptance-object counts 0/5/6/7. |
| Cleanup, cancellation and publication ordering | Host tests cover input/admin/consumer cleanup, read/drain cancellation and output failure with no early publication or retry; singleton complete-valid output and two-registration singleton failure remain unchanged. |
| Mutation target/shard integration | `Mutation/test_c5_workflow.py` covers target selection, ten-source projection and 21-shard plans; the approved static script asserts exact whole-file target and byte-for-byte workflow equality after removing the added matrix row. |

## Specification comparison

Approved Revision 1 changes 1-8 are implemented: contextual completion and dual wire evidence; shared B10 start/window; ordered registration binding and nonfinal aggregation; independent publication acceptance; one shared-budget handler and Kafka drain; synthetic tests; new C5 intake target; contract and item-state documentation. The plan-check's apply-in-implementation correction is in the initial `HostEvidenceBuilder` start-note construction. No scenario, expected outcome, verdict, results file, package, project, workflow trigger or authority rule changed. The successor judge, timeout exclusion/sweep and postmerge mutation acceptance remain pending as the approved plan requires.

## Cold review

Round 1 was a complete comprehensive review of tree `2aa83d5f6a6ba27d7d46e624284a3572032a4807` against source `3f4b29e79883ec520eaed78bbf4390b3b7abeb5b`. It reported one P1: the initial byte-budget tests exercised parser totals rather than the separate retained-buffer boundary. The finding is valid. The correction adds isolated retention cases for both publications and both diagnostic fallbacks, with exact guard messages, measured arithmetic and read-order evidence; the predicate map above now distinguishes these budgets. Production code and all existing assertions are unchanged. The complete earlier report is retained at `/tmp/issue376/cold-review-1-report.md` and will be published in the PR self-review record.

Current review mode: comprehensive, round 2, consolidated re-review. Input revision: frozen staged tree identified by `/tmp/issue376/cold-2-tree.txt`; comparison revision: source `3f4b29e79883ec520eaed78bbf4390b3b7abeb5b`. Full authored diff: `/tmp/issue376/cold-2.diff`. This receipt is part of that diff. Cumulative budget after this pass: comprehensive 2; focused 0. The complete report and dispositions will be linked in the PR self-review record; the final report will not be added to its own reviewed receipt. Hosted review remains a separate gate.

# Batching evidence implementation receipt

This change records delivery latency and batching evidence for B06 and B07 and admits their five-repeat batch. It implements the approved third revision without publishing a live result or claiming mutation acceptance.

## Execution card

- Deliverable: changes 1 to 18 of the [approved Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/359#issuecomment-6629880004).
- Authorization: [Hari's approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/359#issuecomment-6634082914), including the exact private phase.json assertion from [the plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/359#issuecomment-6633831114). Later process decisions and parent adoptions are indexed in [the state file](../../../../items/359/state.md).
- Source: origin/main at 2a92098; predecessor pull request 363 merged at d093204ea0f937c9cc665edb9ef9bbb0818b8312. [The successful smoke](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/358#issuecomment-6629380046) satisfies the prerequisite.
- Mandatory gates: acceptance commands below, docs dispositions, predicate map, cold review, final-head CI, hosted Codex review and the control session's Claude review. Hari merges the workflow and expected-outcome changes. Mutation acceptance and the live batch follow merge on main; no dispatch belongs to this implementation session.
- Verification commands: the approved plan's Acceptance steps 1 to 10, unchanged. Exact commands and results are recorded below after execution.
- Earliest publication: the tested diff with applicable cold-review coverage, then independent CI and hosted review together.
- Budget: at most two comprehensive and two focused cold-review rounds across sessions, with the rule 8 handoff to Hari at exhaustion or unsafe focus. Documentation dispositions capped at 10, then push -wip and hand off to the next session. Post-merge survivor review cap 50, record the gap and continue.

## Conflict-resolution continuation

- Current request: resolve pull request 379 conflicts with current main in a fresh implementation context and retain the approved Revision 3 behavior. The user subsequently directed, "Push it first and then review"; this continuation follows that publication order.
- Input revisions: reviewed branch head `ddb6a0abde28759d60735fe445fc4a5ab3062571`; incoming main `e844587dfbcb4493b89eb844ec916097c31ae5ed`. Merge main without rebasing or replacing branch history.
- Authority catch-up: the control session refreshed issue 359 through handoff comment 6634891128 and parent issues 347 and 349 at 2026-11-04T15:48:24Z; no later decision changes Revision 3.
- Scope: reconcile the shared phase-contract paragraph, preserve incoming B10 phase mappings and packet-flow label work, and update this receipt and the item state. No design, expectation or verifier-predicate change is planned.
- Verification: run the Scenarios test project; the LoadGen restart-identity and packet-flow label test classes; `npm run check:docs`; workflow-copy comparison; staged whitespace, conflict-marker and path checks. Exact commands and measured results follow below.
- Gates and publication: the user explicitly directs pushing this resolved merge before its focused cold review, superseding the earlier pre-push review ordering for this continuation. The control session commits and pushes, then obtains focused conflict-resolution review against `ddb6a0abde28759d60735fe445fc4a5ab3062571`, with the full PR diff as context. Final-head CI, hosted review, the cross-vendor review and Hari's merge remain required.
- Cumulative review budget entering this continuation: two comprehensive passes and zero focused rounds used. Focused round 1 is next; two focused rounds remain. Unsafe focus or budget exhaustion requires the rule 8 handoff to Hari.

### Resolution and affected scope

Git reported one content conflict, in `docs/operations/verifier-contract.md` at the first C5 phase-contract paragraph. The merged paragraph keeps main's B10 C0/C1 phase mapping and its statement that B10 has no committed scenario file, while retaining this PR's removal of the stale B06/B07 packaging statement. `Parchwell.Verifier.csproj` still packages both B06 and B07 scenario definitions. The batching-evidence paragraph is unchanged from the previously reviewed branch.

The other incoming paths are copied unchanged from main: the private packet-flow label source, tests, contract and receipt; item 334 state; the shared scenario phase table and tests; LoadGen restart-identity tests; LoadGen and scenario contracts; and item 374 receipt and state. Their source/test changes were inspected with the phase-table callers in `LoadGenHostOptions` and `ProducerRunContext`. No incoming runtime change intersects this PR's recorder or judgement implementation. The only newly authored changes beyond the composed contract are this continuation receipt and the issue 359 catch-up state.

### Continuation verification

Commands ran on 2026-11-04 against the staged merge of the two input revisions above, before its merge commit. All synthetic test counts come from the test runner; path counts come from the comparison script.

- `PATH=/tmp/dotnet-359:$PATH DOTNET_PROCESSOR_COUNT=2 dotnet test --project tests/Parchwell.Scenarios.Tests/Parchwell.Scenarios.Tests.csproj --configuration Release`: exit 0, 336 passed, zero failed or skipped. Coverage includes B06/B07 mappings and the incoming B10 positive and invalid-phase/case cases.
- `PATH=/tmp/dotnet-359:$PATH DOTNET_PROCESSOR_COUNT=2 dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --configuration Release -- --filter-class '*RestartJobIdentityTests' --filter-class '*PacketFlowLabelTableTests'`: exit 0, 22 passed, zero failed or skipped. These are the incoming restart-identity and private-label boundaries. Parallel project builds caused one MSB3026 file-copy retry in the shared Scenarios project; it recovered and both commands succeeded.
- A Python comparison against both Git trees verified all 12 nonconflicting incoming paths are byte-identical to main; the PR still changes exactly the original 32 approved paths against main; all 29 approved paths other than the shared contract, receipt and item state are byte-identical to the last reviewed branch. It also required the contract to equal the prior branch plus exactly the two B10 text additions, the workflow and its committed copy to match byte for byte, no unmerged index entries, and no conflict markers in any staged changed file. Every assertion passed.
- `npm run check:docs`: exit 0, eight advisory warnings, all in unchanged files; the existing blanket disposition remains. No changed-file documentation warning needed a disposition.
- `git diff --cached --check`: exit 0.

The prior full runtime and phase verification above remains applicable to unchanged batching code. Final-head CI must validate the merged commit. No verifier predicate changed in this continuation; the existing predicate map remains applicable.

### Focused review input

Mode: focused conflict-resolution round 1. Comparison revision: `ddb6a0abde28759d60735fe445fc4a5ab3062571`, which completed comprehensive pass 2. Input revision: the merged commit that the control session publishes under the user's push-first instruction, containing this frozen staged merge tree. Review the delta against that comparison, the composed contract and affected phase-table callers/tests/contracts; use the full PR diff against `e844587dfbcb4493b89eb844ec916097c31ae5ed` as context. The incoming main work does not change the batching design, scope or assumptions, so focused review remains appropriate. The control session records the frozen tree identifier, complete report, dispositions and resulting cumulative counts in the PR self-review record. This receipt does not claim a review result before that review runs.

## Source verification

Read on 2026-11-04: librdkafka v2.16.0 CONFIGURATION.md (acks -1, linger.ms 5), STATISTICS.md (window gauges reset), rdkafka.c (producer req.Produce emission and topic batchcnt), rdkafka_broker.c (request counter increment), rdkafka_request.c (one batch sample per request built), and rdavg.h (window rollover). The contract links the pinned sources. Microsoft's current Kafka client configurations page places acks and linger guidance in the Java table and its lowest-latency row has no client label. The page describes replication configuration as managed by Event Hubs. Completeness of the post-drain sum and captured batch windows remains explicitly inferred. Callback latency follows the existing load generator contract; it is not wire latency.

## Verification

Commands run from the repository root on the implementation based on `2a92098067b4a93ba478c41a9b43a9570a0ab097`. Installed pinned CI tools with `bash infra/scripts/lifecycle/install-tools.sh /tmp/tools-359 ci`; Python checks use `/tmp/tools-359` on PATH. The SDK is 10.0.402 at `/tmp/dotnet-359`.

- `PATH=/tmp/dotnet-359:$PATH DOTNET_PROCESSOR_COUNT=2 ./scripts/verify-dotnet.sh`: exit 0 on the implementation at `a6c4521`. All 7168 tests passed with zero failures or skips: Architecture 15, Enrichment 56, Kafka 486, LoadGen 1163, Scenarios 331 and Verifier 5117. Docker components ran. Locked restore, Release build, XML inventory, evaluated manifests, scaffold inventory, report validation and warning-as-error control passed. The first full build had MSB4166 worker-process failures; bounded fanout resolved the execution failure. The standalone verifier run also passed 5117 tests, including 112 new batching cases.
- `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py' -v`: 242 tests passed, including the five new batch tests. Initial execution lacked Helm; the passing run uses the pinned tools.
- `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`: 290 tests passed with pinned tools. Initial execution lacked required tools.
- `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_*.py'`: 78 tests passed. This checks campaign plumbing, not mutation acceptance.
- `PARCHWELL_TOOLS_DIRECTORY=/tmp/tools-359 bash infra/scripts/lifecycle/check-offline.sh`: exit 0; `chart acceptance passed: 47 positive, 35 negative, 4 worker-setting regressions`. The other chart suites and smoke schemas also passed.
- `cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt`: exit 0. A script counted two changed scenario workflow content lines and exactly the approved 32 paths in the staged diff against origin/main; no added non-ASCII lines. The committed HEAD checks at `a6c4521` matched those values.
- `python3 -c "import yaml,sys; [yaml.safe_load(open(p)) for p in sys.argv[1:]]" scenarios/B06/expected.yaml scenarios/B07/expected.yaml`: exit 0.
- `npm ci --ignore-scripts` and `npm run check:docs`: exit 0, eight warnings in unchanged files, left as existing documentation debt. The scan includes the staged new state and receipt files.
- `git diff --check`: passed. A script compared the recorder and B06 expected-outcome file byte for byte with the approved plan blocks; both match.

After review corrections, `PATH=/tmp/dotnet-359:$PATH DOTNET_PROCESSOR_COUNT=2 dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release -- --filter-method '*Batching*'` passed 116 tests with no failures or skips. Both YAML files parse and the B07 structural comparison against the approved plan passes. The changes are confined to expected YAML, test data and this receipt, so the full runtime suite is not repeated.

No program output is committed. Final-head CI is recorded on the pull request.

## Predicate map

The following tests use synthetic input. Recorder tests keep the other input domains usable so that absence of one measurement cannot mask another predicate.

| Predicate | Tests and boundaries |
|---|---|
| Scenario selection | `Given_OtherScenario_When_Recording_Then_ReturnsNoEvidence` (A01, B04); `Given_DeliveredRowsAndPassingStatistics_When_Recording_Then_RecordsEveryMeasure` (B06, B07). |
| Ledger success, delivered rows, latency field presence | `Given_UnusableLatencyInput_When_Recording_Then_LeavesLatencyAbsent`: malformed ledger, empty ledger, no delivered rows, missing and null latency alongside a good row. The normal measurement test includes a non-delivered row. |
| Finite and bounded latency, microsecond conversion | `Given_LatencyLimits_When_Recording_Then_RecordsOnlyBoundedValues`: maximum and maximum plus one, negative, NaN and infinity. `Given_LatencyRounding_When_Recording_Then_RoundsHalfAwayFromZeroToMicroseconds`: zero, below and at half a microsecond, and a positive value. |
| Nearest-rank ceiling and ordering | `Given_NearestRankBoundary_When_Recording_Then_SelectsCeilingRank`: descending inputs of 20 and 21 rows; singleton and four-row cases in the other recorder tests. Empty input returns before indexing. |
| Checks 18 through 26 | `Given_StatisticsCheckNotPassed_When_Recording_Then_LeavesStatisticsAbsent`: each check missing, failed or unevaluated; `Given_AdjacentCheckNotPassed_When_Recording_Then_KeepsStatistics`: failed 17 and 27. |
| Empty reports; first post-drain selection | `Given_NoReports_When_Recording_Then_LeavesStatisticsAbsent`; `Given_SeveralReports_When_Recording_Then_UsesFirstPostDrainReportAndSumsWindows`: timestamps 99, 100 and 101 against boundary 100, later reports ignored for Produce, no qualifying report leaves Produce absent. |
| Parse and Produce shape/count/addition | `Given_UnreadableReport_When_Recording_Then_LeavesStatisticsAbsent`; `Given_MalformedPostDrainBrokers_When_Recording_Then_LeavesOnlyProduceAbsent`: empty/wrong broker object, missing/wrong req and Produce, negative/fraction/string, valid broker followed by missing counter, overflowing sum. `Given_ZeroOrLargestProduceTotal_When_Recording_Then_RecordsIt`: zero and Int64 maximum. |
| Batch shape/count/addition and sampled-window maximum | `Given_MalformedBatchWindow_When_Recording_Then_LeavesOnlyBatchesAbsent`: missing/wrong topics, topic, window and counters; negative/fraction/string, positive count with zero maximum, overflow. Each runs alone and before a valid report. `Given_IdleWindows_When_Recording_Then_CountsOnlySampledBatches`: empty topics, zero count with nonzero raw maximum, and multiple sampled maxima. |
| Passive codec shape | `MalformedBatchingEvidence`, shared by result and candidate readers: unknown/wrong type, negative and overflowing counts, partial latency or batch fields, zero delivered count, negative minimum, each reversed ordering pair, zero/positive batch mismatch. The review correction adds count -1 with maximum 1 and count 1 with maximum -1, independently isolating both negative batch-field guards in both readers. Result round trips preserve absent objects, empty objects, zero and Int64 maximum; writer tests reject invalid objects. |
| Trust precedence and projection | The three `PhaseJudgeTests` batching tests cover each of six trust predicates, nullable predicates, unrelated message invalidity, extra and duplicate slots. The handler test preserves per-repeat values; the host test verifies B07 valid counts and evidence-object presence without inventing statistics measurements. |

The capabilities and native-report null-forgiving reads rely on the existing successful checks 18 through 26, as the approved recorder does. Those unusable inputs cannot reach the reads with genuine passing assessments. Int64 values beyond representation are rejected by the reader before `HasValidShape`; they cannot be constructed as a record member.

## Cold review

Comprehensive pass 1 reviewed `a6c4521c82e6c5724de717e28f084a3e8f7a1ac6` against `2a92098067b4a93ba478c41a9b43a9570a0ab097`. Report complete; four P0/P1 findings. The full report and dispositions are recorded in the pull request's self-review record.

- Two P0 expected-outcome flags: retain under rule 8 because approved changes 16 and 17 explicitly require the files. Publish with the verdict label and leave merge to Hari.
- P1 B07 structure: corrected the block assembly to split on the complete top-level heading, restoring the approved assessment rules and hypotheses header. A script checks B06 and B07 share identical assessment rules and sources and that B07's hypotheses and verdict equal the approved literal block. Audited the other generated plan-block replacements; B06 and the recorder still match their literal blocks.
- P1 masked negative batch-field tests: added independent negative-count and negative-maximum cases to the shared malformed evidence data, retaining every existing assertion. Both codecs consume those cases. The predicate map above now names the isolation.

Review mode: comprehensive pass 2, the entire diff against origin/main, including these consolidated corrections. Its snapshot is the commit containing this receipt. Cumulative budget at entry: two comprehensive passes started, zero focused passes. The final report belongs in the pull request, not in its own reviewed receipt.

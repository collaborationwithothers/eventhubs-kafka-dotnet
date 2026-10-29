# Probe smoke statistics repair receipt

This repair investigates the failed probe smoke and preserves incomplete evidence as failure. Local verification does not establish live acceptance.

## Execution card

- Deliverable: diagnose original run 40872311163 attempt 1, reproduce native statistics exhaustion locally, repair the affected evidence budgets and add regressions, then publish a tested PR linked to issue #240.
- Source main: `d9492581855902f87b252bae2085c8ac42dfdc34`; failed source: `752ff6e60a0fc32c310df4c9d2fb2fb477361326` (merged PR #246).
- Authorization: Hari's direct task brief on 2026-10-29 authorizes investigation, implementation, local checks, branch push and PR creation. It explicitly authorizes a measured budget revision if needed and forbids a new live dispatch. The [smoke plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6494160519), incorporated [technical specification](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6490062145) and recognized decisions remain the behavioral baseline; no new scientific approval is claimed.
- Mandatory gates: existing verdict/security rules, protected-path merge by Hari, final-head CI and hosted review. No live or mutation dispatch is authorized. Later live acceptance needs separate authorization and the existing environment approval.
- Scope: original two artifacts and the run's job/step evidence; prior issues #221/#230/#154 and PR #246; pinned native statistics for the supported single-topic 4/32-partition stages. Up to twelve distinct local native configurations, with these regressions repeated only by required verification, spanning producer/consumer, 4/32 partitions and the finite supported topic matrix; record any remaining gap rather than launch paid work. No general harness redesign.
- Verification commands: `./scripts/verify-dotnet.sh`; `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'`; `python3 infra/scripts/lifecycle/command.py 600 -- bash infra/scripts/lifecycle/check-offline.sh`; `python3 infra/scripts/lifecycle/command.py 900 -- bash charts/parchwell/tests/check-loadgen-host.sh`; fresh actor/judge export integration through `infra/scripts/phase/tests/check_probe_v2_producers.py`; `npm run check:docs`; `git diff --check`. Targeted tests first establish old failure and repaired behavior. At most two final acceptance attempts per command after implementation; a failing second attempt records the exact gap for handoff.
- Earliest publication: the first complete clean cold-review checkpoint after relevant checks. Publish then; independent CI and hosted review can run together.
- Review budget: AGENTS.md "Before opening a pull request" rule 8 on this source main: at most two comprehensive passes and two focused correction rounds across this PR. Freeze inputs and wait for a complete report before corrections. Exhaustion or unsafe focused scope saves work and hands off to Hari; a new session does not reset the budget.

## Original evidence and diagnosis

The inspected run is [40872311163, attempt 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40872311163), using source `752ff6e60a0fc32c310df4c9d2fb2fb477361326`. The downloaded originals are identified below. Hashes identify the complete ZIP bytes; no raw program output is committed with this receipt.

| Artifact | ZIP bytes | SHA-256 |
| --- | ---: | --- |
| [Candidates, artifact 12900455635](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40872311163/artifacts/12900455635) | 26697 | `b638a2bb2d22e7a67a396dd948ec431fe45aebe57f10bc73167b95c929eca34c` |
| [Pods, artifact 12900007179](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40872311163/artifacts/12900007179) | 268000 | `929d0448d7fb013a549bad2a52321e6c8c97a954e7c091fcfe75e17fbdba2dec` |

All 16 configuration candidates were inspected: four are `complete_valid` (F06 C2, C4, C5 and C8), and twelve are incomplete. All 49 pod collections report no collection gap. Nine actor processes exit 1 and the other forty actor/judge processes exit 0. The actor envelopes describe 165 slots: 80 required, 75 dependency skips and 10 not applicable. These declared admissions do not prove 165 native clients were constructed.

The initiating failures are the nine producer stages below. Across their 45 repeats, delivery is successful with `Persisted` status and no client error, followed by `capture_limit` with no retained statistics. Their downstream helpers preserve dependency skips; those skips do not identify another initiating defect.

| Stage family | Initiating stages | Affected repeats |
| --- | --- | ---: |
| Partition selection | B08 C0/C1 action | 10 |
| Null transport setup | E01 C0/C1 seed | 10 |
| Permission send controls | F06 C0/C1/C3 action | 15 |
| Permission read setup | F06 C6/C7 seed | 10 |

F06 C11 records error 29 in all five slots as a valid observed client outcome. Its overall configuration remains incomplete because its required earlier aggregate/control evidence is incomplete. It is not an independent transport or statistics-size defect. The retained artifacts contain 35 native reports, 3522 to 14252 bytes, totaling 370446 bytes. The discarded native reports and their exact original sizes are unavailable: the original diagnostic recorded only `capture_limit`. No initiating defect independent of this capture path was established by the inspected originals.

The source path narrows the initiating rejection: `ProbeV2Capture.Statistics` takes the first postterminal report and rejects strict UTF-8 lengths above 32768 bytes before storing it. Successful delivery, empty errors and missing statistics are consistent with that path. The local native experiment below reproduces it with unmodified native output. This establishes the repair mechanism; it does not recover or claim the exact sizes of the discarded live reports.

The candidate projections contain only the following failure reason classes. Their counts describe projected reasons, which can carry source-stage failures into dependent configuration judgement; they are not counts of independent defects.

| Reason | Projected count | Producing observation and verifier consumer |
| --- | ---: | --- |
| `v2_capture_limit` | 55 | Actor [capture](../../../../src/Parchwell.LoadGen/SingleActionV2/ProbeV2Capture.cs) retains `capture_limit`; [repeat judgement](../../../../src/Parchwell.Verifier/VerifyRun/SingleAction/SingleActionRepeatJudgement.cs) marks that evidence incomplete. |
| `v2_statistics` | 55 | First native report was not retained; repeat judgement leaves broker count/reference absent rather than supplying zero. |
| `v2_completion` | 55 | [Actor host](../../../../src/Parchwell.LoadGen/SingleActionV2/ProbeV2Host.cs) emits handler exit 1 after capture exhaustion; [configuration judgement](../../../../src/Parchwell.Verifier/VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs) rejects unsuccessful completion. Cleanup itself succeeded. |
| `v2_location` | 55 | [Helper admission](../../../../src/Parchwell.LoadGen/SingleActionV2/ProbeV2Inputs.cs) preserves `not_attempted_dependency` after failed source completion. Configuration judgement cannot establish the required independent readback. |
| `v2_seed` | 10 | E01's failed seed completion prevents independent seed read and target construction. Configuration judgement cannot establish the seed chain. |
| `v2_controls` | 10 | F06's dependent narrow reads lack complete broad seed/control evidence. Configuration judgement preserves the control gap. |

Root diagnostics include `smoke_only` in all 16 candidates, `smoke_controls_failed` in 10, and `v2_controls` in three final aggregate candidates. Those latter diagnostics come from positive-control and prior-candidate aggregation in configuration judgement; they do not demonstrate additional failed Kafka operations. All final judge receipts bind the exact candidate SHA-256. Every candidate has `security_finding=false` and `item_assessment=null`; skipped narrow actions establish no permission result. The run's worker deletion, ephemeral destruction and dispatch-end cluster stop succeeded.

## Relationship to prior failures

- [Issue 221's investigation](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/221#issuecomment-6441687847) found repeated authentication error callbacks exhausting a 64-error record cap before a delivery outcome. Its [adopted remedy](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/221#issuecomment-6452902336) permits counted-and-dropped callbacks only for its named old configuration. It does not authorize dropping V2 statistics or errors. Its missing broker counts were secondary to terminal validation.
- [Issue 230's later diagnosis](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/230#issuecomment-6477388888) separates unsupported KIP-848's debug-only path from coordinator SASL transport failures. Neither is this report-size exhaustion. The silently consuming configuration's capped debug logs left its cause unknown; no authorization conclusion transfers here.
- [Issue 154's source diagnosis](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/154#issuecomment-6384163398) concerns a shared cleanup cancellation token and failed-worker publication gap. Its escalation plan was [never approved](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/154#issuecomment-6384675864). The present V2 size rejection is a separate defect, and the abandoned plan's proposed limits supply no authority.
- [PR 246](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/246) introduced this V2 path, its 32768-byte report and 524288-byte capture ceilings. Its smoke support merged as the failed run's source above. Its offline fixtures did not establish realistic native statistics capacity for the supported partition matrix.

## Native reproduction and measured bounds

The regression uses Confluent.Kafka/librdkafka 2.16.0 with the pinned native mock cluster, a real produced record, topic metadata and a real assigned read. The table records the final whole-solution verification measurements on 2026-10-29, extracted from its LoadGen xUnit report and raw exports. They are synthetic local harness measurements, not Event Hubs measurements. Timer fields can change serialized sizes on later runs.

| Topic partitions / mock brokers | Producer report bytes | Consumer report bytes | Producer five-slot envelope bytes |
| --- | ---: | ---: | ---: |
| 4 / 4 | 11849 | 13530 | 84645 |
| 32 / 32 | 81629 | 83235 | 552460 |
| 32 / 64 | 137092 | 138600 | 924780 |

The five-slot envelopes repeat the same actual producer report to test serialization capacity; they do not claim five independent operations. The native topic report includes the unassigned partition, so 32 partitions produce 33 partition entries. Consumer reports also include a logical GroupCoordinator broker: the 64-broker mock consumer reports 65 broker objects and remains outside the unchanged 64-entry verifier limit. That sample informs byte sizing, not a claim of valid supported broker-count evidence. The 64-broker producer has 64 entries and exercises real verifier acceptance. The existing 10000-entry parser ceiling covers the measured producer/consumer maxima of 8905/9086 entries and is unchanged.

The native statistics format includes broker and topic/partition objects, as documented by [STATISTICS.md at native commit c6c9973cf1b25394c58e0cade13a36f386856f77](https://github.com/confluentinc/librdkafka/blob/c6c9973cf1b25394c58e0cade13a36f386856f77/STATISTICS.md). Its [emitter](https://github.com/confluentinc/librdkafka/blob/c6c9973cf1b25394c58e0cade13a36f386856f77/src/rdkafka.c) iterates brokers and topic partitions with a growing buffer. The former 32768-byte ceiling is a harness limit, not a documented native maximum. The regression feeds identical original report bytes to former and repaired capture limits: 32-partition native reports exhaust the former bound; repaired capture retains every byte. A real 64-broker producer report passes the repaired verifier with its original SHA-256 and fails the former verifier limit.

The user's direct task brief explicitly permits a measured budget revision. It amends the inherited smoke plan's literal byte bounds for this repair; the behavioral checks remain the inherited specification. The derivation is computed with integer arithmetic:

| Boundary | Former / repaired bytes | Basis and enclosing constraint |
| --- | --- | --- |
| Decoded native report | 32768 / 262144 | Next power of two above the largest measured report, 138600; one report per repeat remains fixed. This finite bound is not a universal native-size guarantee. |
| Whole five-repeat capture | 524288 / 2097152 | Five maximum base64 reports require `5 * 4 * ceil(262144 / 3) = 1747640`; 349512 bytes remain for the bounded envelope. Full host output and maximum-bound fixtures test the actual serializer. |
| Collected V2 container log | 1048576 / 4194304 | 2097152 capture + 65536 registration + five 16384-byte client-log budgets = 2244608 bytes before bounded markers/status lines. |
| Each immutable ConfigMap | 1048576 / unchanged | Four ordered capture parts, each at most 524288 decoded bytes, use `binaryData`. Part zero also carries at most two 65536-byte envelopes. Independently base64-encoded fields total at most 873820 bytes before the checked manifest overhead. |
| Final F06 judge inputs | 268435456 / unchanged | Conservative revised aggregate 192086016 bytes remains below this ceiling. 32 logical input reads become at most 41 physical files with multipart capture, below the unchanged 256-file limit. |

All supported stages still use one topic with four or 32 partitions. Reports, queues, parser depth, diagnostics, input files and deadlines remain bounded. The raw report and whole-capture limits are enforced at capture, serialization, helper reconstruction, phase transport and verifier parsing. A report above the repaired ceiling is still incomplete; missing data, malformed data and independent security failures still fail their existing predicates.

Four immutable binary ConfigMaps avoid the Kubernetes [1 MiB ConfigMap data limit](https://kubernetes.io/docs/concepts/configuration/configmap/). Helpers and judges reconstruct four fixed parts in order under the same aggregate byte limit, preserving original bytes and hashes. Existing primary-file input takes precedence; malformed primary evidence cannot fall back to valid parts. Missing, oversized or canceled multipart reads never return a partial accepted capture. There is no new orchestration service or package.

## Failure diagnostics

Capture rejection retains `capture_limit` and a bounded detail such as `capture_rejected:MaxStatisticsReportBytes:observed=262145:allowed=262144`. Invalid UTF-16 has a distinct `capture_rejected:StatisticsUtf8` diagnostic. The first specific rejection survives later failures and shared diagnostic-budget pressure. Serialization failures carry numeric observed/allowed sizes; phase transport records a closed numeric limit gap. These diagnostics contain limit names and sizes, not the discarded payload, token, error-reason contents or private role identifiers. No diagnostic turns an incomplete capture into successful completion.

## Direct brief and inherited-specification comparison

| Direct requirement | Implementation/evidence |
| --- | --- |
| 1. Retrieve originals and distinguish initiating, dependency and independent failures | Artifact hashes, all 16 candidates, 49 collections and the initiating-stage table above; F06 C11 and unavailable original report sizes explicitly separated. |
| 2. Reproduce with the pinned native client and realistic 32-partition output | `ProbeV2MockCluster.cs` and `ProbeV2NativeStatisticsTests.cs` exercise real metadata, produce/read callbacks and original report bytes locally. |
| 3. Establish the root cause and follow contrary evidence | Successful persisted deliveries, no errors, former/repaired byte-bound comparison and prior-failure distinctions; no paid rerun or invented live byte count. |
| 4. Repair every enclosing evidence boundary using measurements | Shared runtime JSON, actor/judge options, full serialization, multipart helpers/transport, chart and verifier parser; derivation above. No truncated report, bypass or expectation change. |
| 5. Make future exhaustion specific, bounded and private | Numeric rejection diagnostics, distinct encoding failure and preserved first cause, tested under shared-budget exhaustion. |
| 6. Add real-native and boundary regressions without weakening acceptance | Predicate map below; raw hashes and independent identity/time/wire/security rejection remain checked. |
| 7. Run appropriate checks and bounded review | Validation record below and execution-card attempt/review budgets. No general harness redesign or new live/mutation dispatch. |
| 8. Record diagnosis, history, measurements, repair and validation, then publish linked PR/handoff | This single authored receipt precedes cold review; final head, CI/review and issue/PR handoff are recorded at publication. |

The inherited smoke plan still owns five fresh action slots, independent controls/readback, exact identity/settings, final receipts, no verdict from incomplete evidence and smoke-only candidates. ADR-019 leaves captured-content decisions with the verifier; actor changes enforce capture/byte controls only. Permission scope, security classification, expected outcomes, verdicts and results are not amended by the byte-budget repair. A later scientific evidence pack still needs applicable hosted mutation acceptance for changed configured targets; this task authorizes no such dispatch.

## Predicate-to-test map prepared before cold review

The map names changed or affected predicates, including empty inputs and precedence. Existing fixtures keep other evidence valid so one invalid field does not hide the tested predicate.

| Predicate | Boundary, empty and interaction evidence |
| --- | --- |
| Actor decoded report ceiling / strict UTF-8 | `ProbeV2StatisticsBudgetTests.Given_StatisticsByteBoundary...`: 0, 262143, 262144, 262145 bytes. Empty bytes survive capture for the verifier to reject; UTF-8 multibyte and invalid UTF-16 tests separate byte size from encoding. Rejected first report cannot be replaced by a later small report. |
| First rejection and shared diagnostic ceiling | `Given_ExhaustedSharedDiagnostics...` fills all 100 entries, rejects each of five reports, then adds another failure; the first specific cause survives within 100 entries. Existing capture tests cover callback count, diagnostic, error/record/setting and postterminal timing limits. |
| Serialized whole capture ceiling | `Given_SerializedCaptureBoundary...`: 2097151, 2097152 and 2097153 bytes. `ProbeV2LifecycleTests.Given_FiveMaximumSizedNativeStatistics...` sends five real native reports padded with valid JSON whitespace to the ceiling through host cleanup and final export, above the former aggregate bound. |
| Verifier native report ceiling / validity | Verifier `ProbeV2StatisticsBudgetTests`: 0, 262143, 262144, 262145 bytes; empty is unusable, exact maximum valid, excess incomplete with both limit/statistics reasons. Actual native 64-broker producer passes with original hash and fails at old limit. Identity, negative sequence, native emission time and malformed base64 remain rejected for a large otherwise valid report. |
| Fixed-part reconstruction / primary precedence | Actor `ProbeV2MultipartInputsTests` and verifier `ProbeV2MultipartInputTests`: part lengths 524287/524288/524289; aggregate below/at/above configured bound; empty and maximum captures; each missing part; oversize part; malformed/empty/oversize primary; canceled read. Valid primary takes precedence; invalid primary cannot fall back. Actor missing/invalid input skips dependency; verifier never returns accepted partial evidence. |
| Exact phase transport / aggregate / ConfigMap bound | `test_probe_v2.py`: five maximum reports round-trip above 1 MiB with unchanged bytes/hash; exact aggregate below/at/above; over-limit decoded report reports safe numeric details; first binary map at all decoded maxima stays below serialized 1 MiB. UTF-8 remains exact; invalid UTF-8 has no lossy fallback. Existing owner/image/marker order/duplicate/final-receipt rejection remains. The collector must use the revised runtime log ceiling; a real log above the old 1 MiB boundary is included in the final collector regression. |
| Final receipt and independent failure precedence | Existing phase tests transport diagnostic completion=false without success; existing actor lifecycle and verifier evidence-gap/security tests remain. No successful fixture replaces original missing controls. |
| Runtime literal agreement / mounted-part layout | Chart tests cover 99 positive and 117 negative V2 cases; published-host validation covers all 49 stage environments plus 10 rejected input cases. The shared JSON, options and rendered chart carry matching revised limits. |

Numeric zero is unreachable as an admitted report-size option because existing options require the exact positive contract value; empty report payload is explicitly covered above. Four fixed parts have no unbounded part-count loop. The unchanged 64-broker/10000-parser-entry predicates retain their existing boundaries; the observed 65-broker consumer is disclosed as over-limit, not accepted.

## Validation and publication state

Targeted checks passed: nine new verifier statistics cases; 17 actor budget/lifecycle cases; 26 actor input cases; 26 verifier multipart/original input cases; 26 V2 transport tests; all 105 phase tests after the collector-bound correction; chart acceptance (99 positive, 117 negative); offline lifecycle/workflow validation; published-host validation (49 V2 environments, 10 negative cases). Fresh actor output also exercised real producer-to-transport integration. The pre-review audit also found and corrected the collector's former 1 MiB literal. Its regression now checks the actual kubectl limit and retained output at 4194303/4194304/4194305 bytes, plus above-1-MiB actual host output through collection and transfer. Both ordinary and 1759177-byte capture exports pass the producer integration checker, including original-byte reconstruction and matching judge receipt validation.

The first full `./scripts/verify-dotnet.sh` attempt passed locked restore but failed build with `MSB4166` child-node termination, with no established out-of-memory diagnosis. A subsequent serial Release build completed with zero errors. The second full attempt used `DOTNET_PROCESSOR_COUNT=2 MSBUILDDISABLENODEREUSE=1`. Locked restore, solution build/analyzers, architecture 12, enrichment 55, Kafka 405, LoadGen 643 and scenarios 239 tests passed, with no skips. Verifier ran 4252 tests: 4250 passed and two failed because their input builders still used the former whole-capture boundary. One purportedly oversized file was now within the revised bound; another padded the former bound instead of the configured bound. The correction changes only these fixture lengths/serialization inputs to derive from `MaxCaptureBytes`; every assertion and malformed/incomplete expectation remains unchanged.

After that fixture correction, the Release verifier build passed with zero warnings/errors and all 1499 SingleAction regression tests passed, with no skips. This includes the two corrected cases, the new native verifier test and all existing security/identity/freshness/aggregate predicates. Both final whole-solution actor exports and the fresh corrected judge export pass `check_probe_v2_producers.py`; the large host capture is 1759177 bytes with five exact 262144-byte native reports padded with valid JSON whitespace. The two whole-command attempts are exhausted: the receipt does not claim a passing third whole-suite command or its unreached final warning-control step. Final-head CI remains the complete standard-command gate. Raw local logs/reports remain outside the repository under `/tmp/issue240-*final*.log` and `/tmp/parchwell-dotnet-verification.lk0gaQTt/`.

Exact focused correction commands, after loading `/tmp/parchwell-tooling/env.sh`:

```sh
DOTNET_PROCESSOR_COUNT=2 MSBUILDDISABLENODEREUSE=1 dotnet build tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false
PARCHWELL_V2_CONTRACT_EXPORT=/tmp/issue240-corrected-judge dotnet tests/Parchwell.Verifier.Tests/bin/Release/net10.0/Parchwell.Verifier.Tests.dll -namespace 'Parchwell.Verifier.Tests.SingleAction' -noLogo
python3 infra/scripts/phase/tests/check_probe_v2_producers.py /tmp/issue240-final-actor /tmp/issue240-corrected-judge
python3 infra/scripts/phase/tests/check_probe_v2_producers.py /tmp/issue240-final-large-actor /tmp/issue240-corrected-judge
```

The phase discovery passed 105 tests after the collection correction. Offline checks passed shellcheck, actionlint, workflow mirrors, lifecycle and legacy/V2 chart suites. Published-host checks passed all 49 V2 stage environments and ten rejections, plus legacy regressions. The final documentation scan and whitespace check are performed before freezing this authored revision; their warning dispositions are recorded in the PR self-review.

Cold review has not started at this authored revision: comprehensive 0/2, focused 0/2. The full authored diff and this receipt must be frozen for the first fresh-context comprehensive review. Complete reports precede corrections; later corrections consume the remaining budget. Final CI, hosted review, reviewed head and PR link are not yet produced. No replacement smoke, scientific run, mutation run, result publication or protected merge occurred in this repair session.

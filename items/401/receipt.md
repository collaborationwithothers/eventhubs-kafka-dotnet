# B10 producer provenance implementation receipt

This change carries both B10 producers' provenance and broker counts through verification into the candidate. Local synthetic tests establish the projection behavior; they establish no live B10 result.

## Current correction and review inputs

Hari's interactive instruction, "Fix Claude comments", authorizes the [oversize-version amendment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6704292129) to the selected plan. It adopts the omission recommendation in the [complete Claude report](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/403#issuecomment-6704093578): the real verify handler omits only an individual generator/runtime observation value above the existing bound, allowing normal result and receipt publication. Supplied observation documents still face strict codec validation. The original scope decision and all other checks remain in force.

Review mode for this correction: focused, comparing against reviewed/publication head `b7876dce0d8f8f3c3332049821d18283b2c5df4b`. The full PR diff is context; correction scope is the version-capture bound, its shared constant, synthetic regression tests, contract and authored receipt/state. Review the changed path and affected codec, manifest reader, common-version aggregation and publication lifecycle, without a third comprehensive pass.

Cumulative history before this focused round: the [initial Astra report in the PR self-review record](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/403) and the linked Claude report are two comprehensive passes. Claude explicitly identifies its report as a comprehensive cold review under rule 8; it consumes the remaining comprehensive pass. Focused usage is zero. Rule 8 permits two focused correction rounds; exhaustion or unsafe focus saves unpublished work under rule 12 and hands off to Hari. The earlier initial-review wording below describes the historical first round, not a reset.

Correction acceptance: first demonstrate the publication regression with synthetic manifests at 255, 256 and 257 characters for each producer and selected version field; preserve every existing codec rejection assertion. Then build Release with warnings as errors, rerun the affected test classes, run `./scripts/verify-dotnet.sh`, run the staged `npm run check:docs` and whitespace validation. Record the source commit and actual results before focused review. No workflow dispatch, B10 run-data read or merge is authorized in this session. Prior caps on verification-failure and changed-file-warning root causes remain unchanged.

## Execution card

- Deliverable and exact scope: [approved implementation-grade plan, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703603871), updated 2026-11-07T06:54:09Z.
- Authorization: [scope decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703489646), [adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703517852), and the fresh-context [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703637652), posted after that plan edit with `Blocking findings: none`. The plan states no open question. Hari explicitly selected Codex in the task and [issue tool comment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703476599), confirmed the assignment and ready label, and directed continuation.
- Mandatory gates: the plan's local acceptance, predicate map, documentation-warning dispositions, applicable cold review, final-head CI and hosted Codex review. Adopted Claude review and control-session merge remain downstream gates. This session does not merge or dispatch workflows.
- Exact acceptance commands: `./scripts/verify-dotnet.sh`, `npm ci --ignore-scripts`, `npm run test:docs`, `npm run check:docs`, `git diff --check`. SDK 10.0.402 is installed at `/tmp/issue-401-dotnet`; verification uses that directory on `PATH`.
- Earliest publication: after local acceptance and applicable cold-review coverage, as the plan's publication section specifies.
- Review budget: AGENTS.md "Before opening a pull request" rule 8, cumulative across sessions. At most two comprehensive passes and two focused correction rounds; unsafe focus or exhaustion saves unpublished work to the backup branch and hands off to Hari. Initial cumulative usage: no comprehensive or focused review.
- Runtime-sized work: the plan caps changed-file documentation-warning root causes and verification-failure root causes at 20 each; exhaustion saves to `-wip` and hands off to the next session. Required authority reads are exempt.

## Correction verification and predicate map

Correction production source: `40f04a251206368daf12451869e04ed59d746c15`. The new synthetic capture-boundary tests first ran against the prior implementation: 26 cases executed, with 16 passing and 10 failing specifically on `FormatException` from observation write validation. The failing cases were over-bound versions; the in-bound controls passed. The red report is `/tmp/issue-401-claude-red/results.xml` and its command output is `/tmp/issue-401-claude-red.log`.

After the capture fix, the affected result-codec, two-instance verify, handler, phase-judge and candidate-codec classes passed all 869 cases with no failures or skips. The Release test-project build had no warnings or errors. The green report is `/tmp/issue-401-claude-green/results.xml`.

The full `./scripts/verify-dotnet.sh` run passed on correction production commit `40f04a251206368daf12451869e04ed59d746c15`: locked restore, clean Release solution build, architecture and every selected suite/report gate, Kafka XML inventory and warning-as-error control. Scripted aggregation of `/tmp/parchwell-dotnet-verification.DiubzihZ/*/results.xml` reports 9497 passed, zero failed and zero skipped, including 7379 verifier tests. Command output is `/tmp/issue-401-claude-full.log`.

Final source commit `b849dc2d0a9aba69a14f1935a642f0dc3fa4c0ed` adds only the zero-length capture rows to the new boundary dataset. A clean Release test-project build and the affected-class command below then passed 877 cases, zero failed/skipped; the report is `/tmp/issue-401-claude-final/results.xml`. This supplements unchanged full-suite evidence under rule 13; no production code changed after the full run. No generated report is committed.

```bash
dotnet build tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-restore -m:2
dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-build --minimum-expected-tests 1 --results-directory /tmp/issue-401-claude-final -- --filter-class '*B10ProducerObservationsCodecTests' --filter-class '*B10TwoInstanceVerifyTests' --filter-class '*JudgeHandlerTests' --filter-class '*PhaseJudgeTests' --filter-class '*PhaseCandidateCodecTests' --report-xunit-xml --report-xunit-xml-filename results.xml
./scripts/verify-dotnet.sh
npm run check:docs
git diff --check
```

The red run uses the same project/configuration/report arguments, `/tmp/issue-401-claude-red` as results directory, and `--filter-method '*Given_CapturedVersionAtObservationBoundary*' --filter-method '*Given_AllCapturedVersionsOversizeForOneProducer*'` instead of class filters. The green affected-class run uses `/tmp/issue-401-claude-green`. Environment and Docker-selector handling are the same as the initial verification below. Staged documentation and whitespace checks cover the final correction and receipt; unchanged-file advisory warnings retain the blanket disposition.

The additional capture predicates are tested at zero length and below, at and above the unchanged string ceiling by `Given_CapturedVersionAtObservationBoundary_When_Verifying_Then_OnlyOversizeValueIsOmittedAndReceiptRemainsAvailable`, for each producer and generator/runtime field. The controls keep both publications accepted and all other fields valid. It checks normal nonfinal status, empty failed checks, window eligibility, both intake acceptances, ordered identities, preservation of other version values and broker counts, unchanged legacy root values, cleanup preceding result creation, and an available final receipt whose result length/hash match the actual result bytes. `Given_AllCapturedVersionsOversizeForOneProducer_When_Verifying_Then_EmptyObservationProvenanceStillPublishesReceipt` covers an empty resulting runtime map and absent generator value. Existing missing-map/manifest capture tests cover unavailable input, and the original codec zero-length and strict over-bound read/write tests remain intact. Existing judge tests turn a missing individual observation value into only that common version's null plus `version_unavailable`.

The audit of this failure class covers both generator capture and every selected runtime key. Other observation strings come from already-bound instance/client identities and the recorder's producer role, so this correction adds no unrelated identity or broker-content gate. The shared ceiling constant replaces the codec's literal without changing its strict wire/typed validation. Input/output budgets, legacy provenance, authorization and finality remain as before.

## Initial source and verification

Baseline: `8e2ab2fbb1ca7fe1a02396af4a07ac9626c13c73`. Production implementation commit: `33c752f67265b9ebd07d83f35370170b3706d057`. The complete acceptance run began on the same source bytes before that local commit was created. Final source commit `1590a3c866c425158cba8236aaf98f562a4ae6fe` strengthens only the new candidate-scope test's control; production code is unchanged. Receipt and state changes do not change those source bytes.

Before implementation, locked restore passed with SDK 10.0.402; `npm ci --ignore-scripts` passed; `npm run test:docs` passed all 28 tests with no failures or skips. No dependency file changed.

The initial full verification stopped at the Release build on CA1861 for an array allocated in the producer selector. A static readonly key array corrected it. An interim build retry reported MSB4166 child-node exits; its advertised diagnostic directory was absent, so the cause is unknown. A subsequent build exposed CS8602 in the new test's nullable access; asserting the parsed observation list's type corrected it. The constrained Release build then passed with zero warnings or errors. These are three distinct observed failure classes, counted by script against the plan's cap; no failure evidence was replaced or test assertion weakened.

The targeted correction check was:

```bash
dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-build --minimum-expected-tests 1 --results-directory /tmp/issue-401-targeted -- --filter-class '*B10ProducerObservationsCodecTests' --filter-class '*B10TwoInstanceVerifyTests' --filter-class '*JudgeHandlerTests' --filter-class '*PhaseJudgeTests' --filter-class '*PhaseCandidateCodecTests' --report-xunit-xml --report-xunit-xml-filename results.xml
```

It passed 835 tests with no failures or skips before the final case-only version rows were added. The full acceptance run includes those additional rows. Builds and tests use `PATH=/tmp/issue-401-dotnet:$PATH`, `DOTNET_PROCESSOR_COUNT=2` and `MSBUILDDISABLENODEREUSE=1`; the Docker endpoint/context/TLS selector variables are unset so component tests use the managed local daemon. No gate or test is skipped.

`./scripts/verify-dotnet.sh` passed on the implementation source above: locked restore, Release build, effective-setting manifests, architecture tests, all selected suites and report gates, XML inventory, and the warning-as-error control with its valid counterpart. The machine-readable reports in `/tmp/parchwell-dotnet-verification.WAWrjq0H` report 9471 passed, zero failed and zero skipped, including 7353 verifier tests. Totals were computed from the assembly XML reports by script. Full command output remains at `/tmp/issue-401-full-verification-fixed.log`; no generated output is committed.

After that compilation, self-check identified that the new non-B10 candidate negative fixture also carried legacy B10-only fields. The final source commit starts that case from a valid ordinary candidate, proves the control reads, then adds only the new extension and checks reader/writer rejection. The correction changes no production code or existing assertion. A Release test-project build with locked dependencies and zero warnings/errors, followed by the command below, passed all 308 candidate-codec cases with no failures or skips. Unchanged suites retain the full-run evidence above under rule 13; final-head hosted CI remains mandatory.

```bash
dotnet build tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-restore -m:2
dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-build --minimum-expected-tests 1 --results-directory /tmp/issue-401-final-candidate -- --filter-class '*PhaseCandidateCodecTests' --report-xunit-xml --report-xunit-xml-filename results.xml
```

`npm run check:docs` passed its advisory scan. Existing warnings in unchanged files stand under rule 8's blanket disposition; no changed-file warning root cause was identified. External URLs were not checked by that scanner. `git diff --check` passed. The final pre-review scan covers the completed receipt and state as well.

## Specification comparison

- Plan sections 1 and 2: [shared record](../../src/Parchwell.Verifier/Common/Results/VerifierResult.cs), [passive codec](../../src/Parchwell.Verifier/Common/Results/B10ProducerObservationsCodec.cs), [result integration](../../src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs), and [real verifier construction](../../src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs). Root compatibility fields and receipt/intake schemas remain as specified.
- Plan section 3: [judge ingestion and common versions](../../src/Parchwell.Verifier/Judge/JudgeHandler.cs), [repeat authorization](../../src/Parchwell.Verifier/Judge/PhaseJudge.cs), and [candidate codec](../../src/Parchwell.Verifier/Judge/PhaseCandidateCodec.cs). No validity, finality or outcome rule changes.
- Plan section 4: [verifier contract](../../docs/operations/verifier-contract.md) owns the additive schema and candidate provenance rule.
- Plan section 5: the tests below exercise the real verify output, passive readers/writers and phase judge with synthetic inputs. Case-only mismatches additionally prove the specified ordinal equality. No new scientific check, package, project, reference or test framework is introduced.
- Decisions: the scope decision's evidence authorization, pair-wide versions, separate counts and downstream execution order are retained. The adopted Claude review and control-session merge requirements are pending, not represented as completed by local evidence.

## Predicate map

The following map covers changed predicates and affected authorization interactions. All negative wire fixtures begin with a valid result produced by the real verifier; changed result bytes in handler fixtures receive updated length/hash references except where reference failure is the predicate under test.

| Predicate | Evidence and boundaries |
|---|---|
| Ordered pair and optional legacy extension | `B10ProducerObservationsCodecTests.Given_ObservationCardinality_When_ReadOrWritten_Then_RequiresExactlyTwo` covers 0, 1, 2 and 3; malformed-wire and typed cases cover reverse, duplicate and foreign IDs. Legacy/optional cases preserve absence. Candidate integration repeats the shared malformed-wire cases and checks absent legacy evidence. |
| Closed objects, required fields, duplicate names, JSON kinds and B10 scope | `InvalidWireCases` enumerates array, entry, version string, runtime map/value and broker object/scalar kinds, missing required fields, unknown and duplicate keys at each new object level. Both result and candidate readers consume these cases. Typed malformed cases and `Given_ObservationsWithoutB10Intake_When_ReadOrWritten_Then_Rejects` cover writer and scope failures; candidate typed cases cover missing intake. |
| Text and numeric bounds, including empty input | Result text cases cover lengths 0, 255, 256 and 257 for versions and broker strings. Measurement cases cover omitted, -1, 0, 1, Int64.MaxValue and raw Int64.MaxValue+1 for each optional count. Candidate cases share invalid boundaries and positive 0/1/maximum values. The instance text ceiling cannot be reached through valid intake: `PhaseWireCodec.ValidateB10Intake` calls `Segment`, whose bound is 128, before the new helper; matching intake order is tested independently. |
| Copy each producer's own provenance and measurement | `B10TwoInstanceVerifyTests.Given_IndependentProducerManifests_When_Verifying_Then_RecordsEachInstancesOwnProvenanceAndBrokerCount` uses distinct first/second values, missing second .NET value and an unreadable second manifest. It checks actual 1/1 fixture measurements for both instances, root compatibility provenance, count absence and the unchanged receipt/intake schema. |
| J2-J6 evidence authorization and precedence | `PhaseJudgeTests.Given_B10ObservationTrustPredicates_When_Judged_Then_UsesOnlyIntakeAuthorization` independently negates each predicate, including null and false for nullable checks. J1-only failure, message invalidity, trusted incompleteness and failed finality retain observations; non-B10 omits them. Handler authorization cases reject each producer completion in turn, a bad result reference, missing start acceptance, failed/unavailable Job/count/inventory checks, and contradictory check-24 evidence. Other repeats remain valid authorization controls. |
| Complete authorized version cohort and per-key ordinal agreement | Handler tests cover all five pairs, absent pair in the last repeat, no pairs, empty runtime map, each missing/different/case-only second-instance value in the first and last repeats, and all producers agreeing on an empty version. Each affected key becomes null independently; the reason is deduplicated and verifier provenance remains separate. The cohort has either 0 or an even number of tuples up to 10: five fixed slots each contribute exactly two. Odd and above-10 cohort sizes are unreachable; pair-size rejection is tested at the codec boundary. |
| Preserve separate measurements and omissions | Handler, result and candidate tests retain first 3/1 and second 7/0 measurements, never a pair sum; omitted second object and either omitted measurement stay absent. Observed zero remains a numeric zero. Unrelated message invalidity and missing/failed sweep retain authorized observations without authorizing outcome counts. |
| Existing budgets, privacy, status and finality | The additive fields use the existing bounded output and input readers, and candidate recursive privacy/duplicate/scalar checks. Existing codec, handler, phase-judge, architecture and component tests run through full acceptance; their assertions are retained. |

## Initial review inputs and downstream acceptance

Review mode: comprehensive; reviewed source is the implementation commit above plus the staged receipt/state. The frozen staged tree and complete report will be identified in the pull request's self-review record. No prior pull request or cold-review round exists for this issue. The first review starts with cumulative usage zero comprehensive and zero focused; its complete report is kept out of its own reviewed receipt and linked or reproduced in the PR self-review record under rule 8.

The plan changes configured C5 mutation targets. Acceptance remains a passing post-merge main workflow whose measured commit includes this change and whose summary has `measurement_complete: true` and `break_passed: true`. The control session coordinates that run before fresh B10 verdict evidence. No live run result, artifact or log is an input to this implementation.

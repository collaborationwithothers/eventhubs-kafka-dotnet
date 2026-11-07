# B10 producer provenance implementation receipt

This change carries both B10 producers' provenance and broker counts through verification into the candidate. Local synthetic tests establish the projection behavior; they establish no live B10 result.

## Execution card

- Deliverable and exact scope: [approved implementation-grade plan, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703603871), updated 2026-11-07T06:54:09Z.
- Authorization: [scope decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703489646), [adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703517852), and the fresh-context [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703637652), posted after that plan edit with `Blocking findings: none`. The plan states no open question. Hari explicitly selected Codex in the task and [issue tool comment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703476599), confirmed the assignment and ready label, and directed continuation.
- Mandatory gates: the plan's local acceptance, predicate map, documentation-warning dispositions, applicable cold review, final-head CI and hosted Codex review. Adopted Claude review and control-session merge remain downstream gates. This session does not merge or dispatch workflows.
- Exact acceptance commands: `./scripts/verify-dotnet.sh`, `npm ci --ignore-scripts`, `npm run test:docs`, `npm run check:docs`, `git diff --check`. SDK 10.0.402 is installed at `/tmp/issue-401-dotnet`; verification uses that directory on `PATH`.
- Earliest publication: after local acceptance and applicable cold-review coverage, as the plan's publication section specifies.
- Review budget: AGENTS.md "Before opening a pull request" rule 8, cumulative across sessions. At most two comprehensive passes and two focused correction rounds; unsafe focus or exhaustion saves unpublished work to the backup branch and hands off to Hari. Initial cumulative usage: no comprehensive or focused review.
- Runtime-sized work: the plan caps changed-file documentation-warning root causes and verification-failure root causes at 20 each; exhaustion saves to `-wip` and hands off to the next session. Required authority reads are exempt.

## Source and verification

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

## Review inputs and downstream acceptance

Review mode: comprehensive; reviewed source is the implementation commit above plus the staged receipt/state. The frozen staged tree and complete report will be identified in the pull request's self-review record. No prior pull request or cold-review round exists for this issue. The first review starts with cumulative usage zero comprehensive and zero focused; its complete report is kept out of its own reviewed receipt and linked or reproduced in the PR self-review record under rule 8.

The plan changes configured C5 mutation targets. Acceptance remains a passing post-merge main workflow whose measured commit includes this change and whose summary has `measurement_complete: true` and `break_passed: true`. The control session coordinates that run before fresh B10 verdict evidence. No live run result, artifact or log is an input to this implementation.

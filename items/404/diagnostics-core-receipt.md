# B10 abrupt diagnostics core and supervisor receipt

PR A implements diagnostic metadata for the supervisor and phase-script failures. Protocol, C1 and scientific admission stay unchanged; PR B follows only after PR A merges.

## Execution card

- Deliverable and scope: approved Revision 3, "PR A: core and supervisor" file allowlist; appendix A1-A3 and PR A fixes in A7. No PR B implementation.
- Authorization: [plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6735133465), updated 2026-11-09T08:00:45Z, exact REST body SHA-256 `41decdae9cc90eba48197c03e141f6b62390ded66157f8835f450a9b5a9dde41`; [appendix](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6735131020), updated 2026-11-09T08:00:35Z, SHA-256 `2637d71c85f6f3f35be5e2668d3c8541f331bfba20728f390411683595dcf094`; [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6735505476). Both identities matched before implementation.
- Decisions: scope/process [6733448366](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6733448366), unchanged protocol/C1 [6734201273](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6734201273), held ack stage [6734951856](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6734951856), caller-info/scan and ordered split [6734964238](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6734964238). [Revision 3 check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6735392763) P2-H requires Hold after original supervisor line 94; appendix A7 supplies mechanical fixes from the [Revision 2 check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6734851668).
- Mandatory gates: exact plan identities; red/green and acceptance commands in the plan; scope and failure-literal proof; documentation dispositions; fresh-context repo-review coverage, exact-head cold Claude review and final-head CI/hosted code/security review.
- Verification commands: the plan's "Acceptance and bounds" command block, plus targeted diagnostic, supervisor, architecture and produced-byte checks recorded below.
- Earliest publication: tested candidate with applicable cold-review coverage. Hari explicitly directed this session on 2026-11-09 to create the PR in ready state when done and perform the Claude review separately. That exact-head review remains a merge gate; it is not claimed here.
- Caps: 20 verification-failure root causes, 20 changed-file documentation-warning root causes, 20 review-finding root causes and two consolidated correction rounds; exhaustion: push -wip and hand off to the next session with a dated basis-labelled remaining-time estimate. Cold review separately allows two comprehensive and two focused rounds cumulatively; exhaustion or unsafe focus goes to Hari. Bounded diagnostic caps and reduction follow the approved plan with record-the-gap-and-continue behavior.
- Source: `ee36f1c65dce4df42ba61d87c5a70e2d25c3fc8e` (`origin/main`). Branch: `codex/404-diagnostics-pr-a`.
- Predicate map: not applicable; no verifier logic changes. C5 not applicable: no configured mutation target changes.

## Implementation verification (db409b57)

Results measured in this session on 2026-11-09; program output stays outside Git under `/tmp/issue404`.

- Installed SDK 10.0.402 using the official installer; locked restore and Release builds passed with zero warnings/errors. An intermediate concurrent MSBuild attempt exited MSB4166; serialized builds and `DOTNET_PROCESSOR_COUNT=2` cleared it without source changes.
- Red architecture scan on baseline: 12 matching lines, 12 violations (supervisor 4, wire 2, publisher 6). Green: the same 12 lines, zero violations; the new architecture test passed. Full architecture report: 17 passed.
- Diagnostic unit tests: 13 passed. Phase red: 12 ran, 4 missing-site errors. Phase green: 12 passed.
- Scratch negative control: copied the candidate outside Git and removed only the held AckSent call. Release build passed; both R2 rows failed exactly because expected ack_sent became stop_requested. Candidate production source was unchanged by the control.
- First complete LoadGen run: 1639 total, 1636 passed, 3 failed, zero skipped. The abrupt-class run independently measured 419 total, 415 passed, 3 failed, 1 skipped (published apphost unset for that run). Both runs found the same two new-test setup root causes: the R2b clock wait consumed the registration timer, and raw message exclusion matched the required synthetic Boolean member name. Consolidated correction round 1 waits for the progress poll's 10 ms timer and excludes the serialized message as a JSON string value in the whole file, using unrelated fixture identity values. Existing assertions remain intact. Corrected full LoadGen validation passed: 1639 total, all passed, zero failures/skips. The five targeted cases also passed (both ack stage rows, resume timeout, both cleanup size rows).
- `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`: 333 tests passed, including phase/chart tests with pinned Helm installed.
- `bash charts/parchwell/tests/check-loadgen-host.sh`: exit 0, including the produced-byte bridge. Explicit exporter: 8 tests passed; `check_b10_abrupt_producers.py`: 10 tests passed and printed b10_abrupt_produced_bytes_checked.
- `npm ci`, `npm run test:docs` (28 passed), `npm run check:docs`: exit 0. Eight advisory warnings are in unchanged files and stand as pre-existing. The staged-candidate scan also exited 0 with the same eight unchanged-file warnings and no changed-file warning.
- Scope proof: exactly 16 allowlisted paths; FailureName body and all 28 failure-table names unchanged; pre-existing C# exception-message literal sets unchanged. The guard condition split repeats an existing literal but adds no name. Python failure literal multiset unchanged. Core A1 and contract A3 are copied exactly; A7 PR A fixes and P2-H applied. No C5 mutation target or protected path changed.
- Final `./scripts/verify-dotnet.sh`: exit 0, SDK 10.0.402. Parsed all six XML reports by script: 9927 total and passed, zero failures/skips. Architecture 17; Enrichment 56; Kafka 486; LoadGen 1639; Scenarios 340; Verifier 7389. Locked restore, Release build, analyzers, published LoadGen fixture, XML inventory, project settings and warning-as-error controls passed. Logs/reports: `/tmp/parchwell-dotnet-verification.5D9IzDda`. The earlier full-script run had loaded the pre-correction test assembly and failed the same three test-setup cases; this final run supersedes it. No production/test code changed between comprehensive review and this final run.
- Final diff whitespace and exact allowlist checks passed; `git diff --name-only origin/main...HEAD` identifies the same approved 16 paths. Program output is not committed.
- Caller-information, TargetSite and ConditionalWeakTable documentation were read from the official .NET 10 pages cited in the contract; message.timeout.ms was checked at librdkafka v2.16.0 CONFIGURATION.md. No new Kafka or Azure behavior is asserted.

Implementation verification-failure root causes: 3 (one concurrent-build infrastructure cause, two new-test setup causes). Changed-file documentation-warning root causes: 0. Consolidated correction rounds: 1 of 2. Review-finding root causes: 0 in comprehensive round 1.

## Implementation review record

Comprehensive round 1 reviewed `7c86cb3620e72315a528a3d6082cb984a8de7c92` against `ee36f1c65dce4df42ba61d87c5a70e2d25c3fc8e` through the fresh-context repo-review skill. Complete report:

Review mode: comprehensive, round 1
Reviewed revision: `7c86cb3620e72315a528a3d6082cb984a8de7c92`
Comparison revision: `ee36f1c65dce4df42ba61d87c5a70e2d25c3fc8e`

Report: complete

Reviewed every authored change in these 16 files:

```text
docs/operations/b10-abrupt-contract.md
infra/scripts/phase/b10_abrupt.py
infra/scripts/phase/tests/test_b10_abrupt.py
items/404/diagnostics-core-receipt.md
items/404/state.md
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptDiagnostics.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptPublisher.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptSupervisor.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptWire.cs
tests/Parchwell.ArchitectureTests/B10AbruptFailureSiteTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptDiagnosticsTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptOptionsTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProcessTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProducerTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptSupervisorTests.cs
```

Applied the repository review rubric, AGENTS.md governance/review/publication/writing rules, docs/agents/pull-requests.md, CLAUDE.md, factory authority and lifecycle rules, blueprint ADRs and scope, and .NET engineering/testing standards. Every changed path matches the general rubric; all C# production and test paths additionally match the .NET row.

Specification coverage:

| Requirement source | Review result |
|---|---|
| Revision 3 plan, "PR A: core and supervisor" and file allowlist; approval comment `6735505476` | All 16 paths are allowlisted. No PR B implementation or unrelated scope change found. |
| Approved plan `6735133465` and appendix `6735131020` identities | Both fetched REST bodies match the receipt's exact SHA-256 values. Approval identifies those revisions and was posted afterward. |
| Appendix A1, diagnostics core | New core file matches the approved text exactly. Chain traversal, caller sites, fact limits, timeline limits, held output and reduction behavior are covered by the added tests. |
| Plan supervisor site/stage table; appendix A7; approval's P2-H correction | Supervisor instrumentation, held `ack_sent`, decisive facts, retained arm/cleanup causes and report reduction match the specified changes. `EventStreamEnded` uses `Hold`. |
| Plan host, wire and publisher sections | Caller information reaches helper callers; direct failures are attached; host report formats gain the specified diagnostic objects. |
| Appendix A2, phase failure sites | Implementation matches the prescribed changes. Tests cover direct, converted-cause, collection and unfinished-first-job sites. |
| Appendix A3 and plan contract edits | Observability replacement matches A3 exactly; the specified configuration, capture-gap and empty-control-selection rows are updated. |
| Plan tests and acceptance; docs/engineering/dotnet.md "Testing" | Required new cases are present. Existing assertions retain their previous prefix, value and exclusion checks while accepting the expressly authorized additive schema. No verifier predicate or configured mutation target changes occur, so the receipt's non-applicability statements are appropriate. |
| Decisions `6734201273`, `6734951856`, `6734964238`; blueprint ADR-021 | Protocol, C1 configuration, admission gates, wire/journal members and scientific behavior remain unchanged. |
| Plan "State and receipts"; factory "Item state file" | State and receipt identify the approved scope, decisions, review round and outstanding publication gates without claiming pending verification succeeded. |

No missing, partial or incorrect PR A requirement, or scope creep, was identified.

Independent checks completed: phase unit suite passed 12 tests; diff whitespace check passed; architecture-pattern recount confirmed 12 matching lines and zero candidate violations versus 12 baseline violations. Scripted comparison confirmed unchanged `FailureName` and failure-table names. No scenario, results, workflow, project/package or verifier files changed. Official .NET 10 documentation and pinned librdkafka configuration documentation support the changed factual claims.

The receipt still identifies full verification and later head-specific reviews as outstanding gates. This report supplies cold-review coverage for the identified revision; it does not claim those gates passed.

No P0, P1 or reportable P2 findings.

Findings: none at P0 or P1

Pre-existing (not blocking): None identified.

Disposition at the reviewed revision: no correction required. The final acceptance/receipt update received [focused round 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/410) on db409b57 before publication, with no P0/P1/reportable P2. No production or test source changed after the comprehensive review. Historical counters at that checkpoint: comprehensive 1; focused 1; hosted requests 0 before publication. The PR description records the last complete focused report, final-head CI and hosted requests without adding a review report to its own reviewed receipt. No prose-only P2 correction round or follow-up issue was needed.

Models: Codex author and implementation subagents (model/effort not exposed by this interface); fresh-context cold reviewer at high effort. Hari performs the exact-head Claude review separately after ready-state PR creation, by his interactive instruction.

## Claude-review correction execution card

Hari's current task directly authorizes one commit changing only this receipt, state.md and B10AbruptSupervisorTests.cs to dispose [Claude comprehensive review 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/410#issuecomment-6736385602) of `db409b57f1b5638022029487b7dab9d8e4575c29`. This is prose and tests only; production code and existing assertions stay unchanged. PR A scope and the PR B dependency remain in force.

- P2-2: state now indexes Revision 3 check 6735392763; approval 6735505476 at `2026-11-09T08:22:57Z`, exact-body SHA-256 `2034b7d3f1db94d440394338777a2d7491b53635a21c39510fe17282b59d3741`; and decision 6733448366 item 4 as the current tool assignment. Audit both item files: older decisions and the original review report remain historical evidence; the current next action, checkpoint labels and counters are updated here. No additional stale current authority source was found.
- P2-1: three tests beyond the approved plan's list are authorized by Hari here. They pin the supervisor acknowledgement-time bound, the held EOF stage required by approval P2-H, and the measured grace duration. This prevents the diagnostic timing from misleading a later trial reader. Rule 10 would otherwise permit an Out of contract disposition; this explicit instruction authorizes the test-only addition.
- P3-1: no action, as directed. The justified oversize-fixture deviation remains unchanged.
- Verification: repeat the plan's acceptance command block and the three named scratch mutants, one each, with no production mutation in the repository. Record measured results below. C5/predicate map remain not applicable.
- Publication checkpoint: one tested correction commit, focused coverage against db409b57, refreshed authority, then push and one Codex review request of the new head. No merge or trial.
- Cumulative budget at entry: comprehensive 2/2 (local comprehensive 1 and Claude comprehensive 2), focused 1/2 complete. This correction uses focused round 2/2; no third comprehensive pass. Exhaustion/unsafe focus: save -wip and hand off to Hari under rule 8. The outage did not reset counters. Prior consolidated verification correction rounds: 1; this consolidated correction is round 2/2. Prior review-finding root causes: 0; this review adds the two named P2 root causes, within the 20-root-cause cap.

Correction verification measured on 2026-11-09, based on `db409b57f1b5638022029487b7dab9d8e4575c29` plus this three-file correction delta:

- Added exactly three Fact tests. Release test-project build: zero warnings/errors; targeted green: 3 total, all passed, no failures/skips.
- Scratch-only named mutants, one each: ack held before AckAsync, EOF Hold changed to Mark, and graceBegin moved after the wait. Each built successfully; each named test ran once and failed on its intended ordering/output/duration assertion (3 mutants killed). The unmodified candidate passed all three. Scratch source was restored; repository production source is byte-unchanged.
- Phase suite: 12 passed. Lifecycle discovery: 333 passed. Host/chart command: exit 0, including the production-byte bridge. npm ci and documentation tests passed (28 tests, zero failures).
- Correction scope proof: exactly state.md, diagnostics-core-receipt.md and B10AbruptSupervisorTests.cs changed; exactly three added Fact tests, no removed existing assertion, no production diff. A metadata check initially assumed a cached remote-tracking ref; direct git ls-remote verified the actual branch still at db409b57. This adds one infrastructure verification root cause, so cumulative verification root causes are 4 of 20. Expected mutant failures are the red evidence, not unresolved failures. Correction documentation-warning root causes remain 0; review-finding root causes are 2 of 20.
- `./scripts/verify-dotnet.sh`: exit 0 with SDK 10.0.402. Six XML reports parsed by script: 9930 total and passed, zero failures/skips. Architecture 17; Enrichment 56; Kafka 486; LoadGen 1642; Scenarios 340; Verifier 7389. Restore, Release build/analyzers, published apphost, report validation and warning-as-error controls passed. Evidence: `/tmp/parchwell-dotnet-verification.PAoXohEs` and `/tmp/issue404/review-fix/verify-dotnet.log`.
- Correction test-file SHA-256: `a4c88ed1cc73c3caafd7632a3de62126a336e8a3eeaaf104c9043ae94137df42`. Production and existing assertion preservation were checked against db409b57. Both approved plan identities and the approval identity still matched the authority reads. Final whitespace/scope checks passed. npm run check:docs exited 0 on the completed candidate: eight advisory warnings, all in unchanged files; both changed item files reported no issues.

Focused round 2 result: pending; no success claimed before the review finishes. The complete final focused report will be recorded in the PR self-review record, avoiding a report in its own reviewed receipt.

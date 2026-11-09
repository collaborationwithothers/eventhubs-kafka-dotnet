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

## Verification

Results measured in this session on 2026-11-09; program output stays outside Git under `/tmp/issue404`.

- Installed SDK 10.0.402 using the official installer; locked restore and Release builds passed with zero warnings/errors. An intermediate concurrent MSBuild attempt exited MSB4166; serialized builds and `DOTNET_PROCESSOR_COUNT=2` cleared it without source changes.
- Red architecture scan on baseline: 12 matching lines, 12 violations (supervisor 4, wire 2, publisher 6). Green: the same 12 lines, zero violations; the new architecture test passed. Full architecture report: 17 passed.
- Diagnostic unit tests: 13 passed. Phase red: 12 ran, 4 missing-site errors. Phase green: 12 passed.
- Scratch negative control: copied the candidate outside Git and removed only the held AckSent call. Release build passed; both R2 rows failed exactly because expected ack_sent became stop_requested. Candidate production source was unchanged by the control.
- First complete LoadGen run: 1639 total, 1636 passed, 3 failed, zero skipped. The abrupt-class run independently measured 419 total, 415 passed, 3 failed, 1 skipped (published apphost unset for that run). Both runs found the same two new-test setup root causes: the R2b clock wait consumed the registration timer, and raw message exclusion matched the required synthetic Boolean member name. Consolidated correction round 1 waits for the progress poll's 10 ms timer and excludes the serialized message as a JSON string value in the whole file, using unrelated fixture identity values. Existing assertions remain intact. Corrected full LoadGen validation is running; it must pass before publication.
- `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`: 333 tests passed, including phase/chart tests with pinned Helm installed.
- `bash charts/parchwell/tests/check-loadgen-host.sh`: exit 0, including the produced-byte bridge. Explicit exporter: 8 tests passed; `check_b10_abrupt_producers.py`: 10 tests passed and printed b10_abrupt_produced_bytes_checked.
- `npm ci`, `npm run test:docs` (28 passed), `npm run check:docs`: exit 0. Eight advisory warnings are in unchanged files and stand as pre-existing. A staged-candidate scan remains before publication.
- Scope proof: exactly 16 allowlisted paths; FailureName body and all 28 failure-table names unchanged; pre-existing C# exception-message literal sets unchanged. The guard condition split repeats an existing literal but adds no name. Python failure literal multiset unchanged. Core A1 and contract A3 are copied exactly; A7 PR A fixes and P2-H applied. No C5 mutation target or protected path changed.
- `./scripts/verify-dotnet.sh` is running. Locked restore, solution build, Kafka XML inventory, project settings, architecture (17) and Enrichment (56) passed so far. The full run remains a publication gate; no final success is claimed yet.
- Caller-information, TargetSite and ConditionalWeakTable documentation were read from the official .NET 10 pages cited in the contract; message.timeout.ms was checked at librdkafka v2.16.0 CONFIGURATION.md. No new Kafka or Azure behavior is asserted.

Verification-failure root causes so far: 3 (one concurrent-build infrastructure cause, two new-test setup causes). Changed-file documentation-warning root causes: 0. Consolidated correction rounds: 1 of 2. No review finding has yet been reported.

## Review record

Comprehensive round 1 is requested on the frozen initial candidate commit containing this receipt, compared with origin/main ee36f1c65dce4df42ba61d87c5a70e2d25c3fc8e. The reviewer receives only that diff and this receipt and applies repo-review. Its complete report and disposition will be recorded after it returns. Cumulative comprehensive: 1; focused: 0; hosted requests: 0. Final acceptance evidence and any consolidated corrections will receive applicable further coverage before push.

Models: Codex author and implementation subagents (model/effort not exposed by this interface); fresh-context cold reviewer at high effort. Hari performs the exact-head Claude review separately after ready-state PR creation, by his interactive instruction.

# PR2 receipt: inactive B10 restart execution

This pull request implements only the diagnostic restart execution approved for issue 404. It cannot authorize scientific publication; Hari merges it and separately approves the post-merge trial environment.

## Execution card

- Deliverable and exact scope: [Revision 6 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6709851444), file map items 1-20 and acceptance E1-E12. PR3 is excluded.
- Authorization: [PR2-only approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6711868908), with [cap decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6711519349). REST verification matched updated_at `2026-11-07T18:45:47Z`, 126836 characters, SHA-256 `4a6a093a00b909d8bbea623983c8aebd404773b3a81fe2c7bb116e4e76596376`.
- Base: `74be825cb6ac71c2c70a6612f3fa0910dcdc52ef`, merged PR1. The plan's Authority and predecessor snapshot identifies binding decisions and ADR dependencies.
- Mandatory gates: exact plan acceptance below, source and policy equality, default-off/scientific rejection, Linux process proof, cold review under pull-requests.md rule 8, hosted Codex review and Claude head review checking every cap-covered choice, final-head CI, and Hari's merge. No workflow dispatch, Azure execution or live B10 evidence access is authorized in this session.
- Earliest publication checkpoint: tested implementation after applicable cold-review coverage; CI and hosted review may run together.
- Review budget and exhaustion: plan section Mutation, review budget and bounded work and docs/agents/pull-requests.md rule 8. At most two comprehensive passes and two focused correction rounds cumulatively. Budget exhaustion or unsafe focused review requires a backup push and handoff to Hari. Plan disposition caps apply unchanged.

Exact verification commands, from the plan:

```sh
./scripts/verify-dotnet.sh
python3 -m unittest infra.scripts.phase.tests.test_b10_abrupt infra.scripts.phase.tests.test_phase infra.scripts.phase.tests.test_workflow
python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'
bash charts/parchwell/tests/check.sh
bash charts/parchwell/tests/check-loadgen-host.sh
bash infra/scripts/lifecycle/check-offline.sh
npm ci --ignore-scripts
npm run test:docs
npm run check:docs
git diff --check
```

The mutation-only Compile item comparison and scripted preservation checks follow the plan's Acceptance commands paragraph. Whole-script host acceptance elapsed time must be measured before and after changes; exceeding its existing CI bound requires handoff to Hari.

## Cap-covered choices

- C1: the unknown-arm hand-off test supplies valid reports for both recognized arms as separate valid input. The unattributable report raises `b10_abrupt_report_binding_invalid`; neither recognized arm spuriously lacks a report. This is the approval's explicit first option and preserves per-arm failure precedence.
- C2: the sentence about no message is scoped to the hand-off cases. Absence of a message raises only `b10_abrupt_sample_missing`; invalid binding messages raise only `b10_abrupt_report_binding_invalid`. Overflow is tested separately as specified, with no report message.
- C3: after the counter overflows, selection is unavailable, including any earlier numbered report. This follows the plan's explicit overflow consequence that that arm's accepted message raises `b10_abrupt_sample_missing`; no fallback repairs overflow.
- Report length: a value outside 1 to 1048576 raises `b10_abrupt_report_binding_invalid`. This applies the carried exact raise-list correction and preserves E4's zero and oversized boundaries.
- Raw report encoding: write `Encoding.UTF8.GetBytes(report.RawJson)` without a byte-order mark. This applies the carried correction and matches the existing UTF-8 byte bound.

## Mechanical representation choices

- The successor binding carries the original authenticated start-line, broker inventory and completion strings in `start_line_binding`. This gives the successor the existing expected-broker inputs through its sole permitted binding path; the complete input remains subject to the fixed byte cap.
- Diagnostic collection permits a zero-byte `request-lifecycle.jsonl` because an empty capture has no events. The shared download helper retains its existing nonempty default for every other caller.
- The cgroup directory is retained with a narrow Linux `open` call and a safe handle. Ordinary managed file opening rejects directories on this platform. Directory identity is checked through the retained descriptor without altering the cgroup/OOM exclusion gate.

- Selection includes the full checkpoint frame and its `{name,length,sha256}` descriptor. The runtime binds the exact serialized frame bytes; phase compares the descriptor with the authenticated raw journal line and compares the parsed frame, avoiding a cross-language reserialization assumption.

- Resume progress uses a create-only local child acknowledgement after reading the authenticated control checkpoint. The supervisor checks the checkpoint and identity binding within the existing arm budget. This implements `b10_abrupt_resume_unconfirmed` without adding an IPC kind, signal or publication member.

- Deadline-name alignment: after stopped state is observed, exhaustion during drain/checkpoint uses the existing `b10_abrupt_deadline_exhausted` outcome for the enclosing arm deadline. `b10_abrupt_stop_unconfirmed` remains for missing/late stopped observation. A successful native KILL observed outside its budget likewise reports deadline exhaustion, while `b10_abrupt_signal_failed` remains an actual native failure. Sticky journal gaps and actual signal observations are retained; the budgets and admission comparisons are unchanged.

## Verification and review status

Local verification passed on the initial candidate `cb6d0cfa52e1ae7ad3d20af72133c19975c75a2b`. Its first cold review found six P1 root causes; the consolidated corrections have passed affected verification and await the second comprehensive review. Linux CI remains pending. No live trial has run in this session.

- `./scripts/verify-dotnet.sh` passed with locked restore, Release build (zero warnings/errors), architecture checks, all project tests, checked reports and the warning-as-error control. Measured report counts: Architecture 16, Enrichment 56, Kafka 486, LoadGen 1429, Scenarios 340 and Verifier 7389. Logs remained outside the repository.
- Baseline, before chart changes, source `74be825cb6ac71c2c70a6612f3fa0910dcdc52ef`: `bash charts/parchwell/tests/check-loadgen-host.sh` passed. Bash `time -p` measured real 121.04 s, user 90.88 s, system 21.96 s. Initial candidate post-change: the same whole script passed, including E10/E11, with Bash `time -p` measuring real 114.19 s, user 89.66 s and system 29.36 s, within the unchanged 900 s bound. A local `timeout 900` enforced that bound.
- `npm ci --ignore-scripts` completed; `npm run test:docs` passed 28 tests with no failures or skips.
- `bash charts/parchwell/tests/check.sh` passed; measured elapsed time was 264.13 s. `bash infra/scripts/lifecycle/check-offline.sh` passed with exit 0.
- `npm run check:docs` reported eight warnings, all in unchanged files.
- The phase, diagnostic and workflow Python command passed 125 tests in 31.212 s on the final phase implementation after loading the installed tool environment. Its first invocation omitted that PATH and failed at Helm execution; this is one environment failure root cause shared with the script-discovery invocation, whose retry passed 326 tests in 88.403 s.
- Scripted preservation found 45 authored changed paths within the approved file map, no non-ASCII punctuation, all six existing workflows unchanged, an identical new workflow mirror, unchanged helper/host-runner files and exactly the authorized project-file replacement.
- The final targeted transport run passed 79 tests, zero failures/skips, in 2.915 s on Debian 13, .NET 10.0.13 and kernel 6.18.57. Production process tests observed inherited pipes, stop, lack of progress while stopped, resumed progress, kill, wait/reap and EOF. The observed exit code 137 is labelled local evidence, not a death predicate. Linux GitHub Actions proof on the reviewed head remains pending.
- E11 checked fresh production export `/workspace/scratch/b10-export-first`: `python3 infra/scripts/phase/tests/check_b10_abrupt_producers.py /workspace/scratch/b10-export-first` passed seven produced-byte tests and emitted exactly `b10_abrupt_produced_bytes_checked` plus newline. The mandatory host script regenerated and checked its own fresh export successfully.
- Scripted section comparison confirms the copied wire, policy and failure text matches Revision 6 exactly apart from the authorized report_length raise-list corrections.
- A scripted comparison confirmed all 11 runtime policy values equal `runtime.bounds.b10_abrupt` in the chart.
- All 13 current Program/abrupt runtime source SHA-256 byte hashes were found in the Release PDB after the solution build, including the final publisher failure-mapping edit.
- The scripted MSBuild Compile-item check found zero occurrences of `Host/B10AbruptRejectionTests.cs` under `ParchwellVerifierMutationOnly=true` and exactly one in the normal build.
- A final Release E9 worker invocation with fresh output and `-showliveoutput` passed three facts, zero failures/skips, in 1.781 s. Its measured versions were Confluent.Kafka assembly 2.16.0.0, librdkafka 2.16.0 and .NET runtime 10.0.13.
- Initial targeted Verifier.Tests build found CA1859 on the new export reader's return type. Its concrete Dictionary return type was corrected; the full Release build and Verifier suite now pass. This is a verification-failure root cause, not a waived warning.

## Verification failure dispositions

The list below contains 17 distinct failure groups across all implementing agents, counted by script and conservatively charged against the root-cause cap; repeated manifestations appear only once. Uncertain causes are identified below. The plan cap is 20; no warning is waived.

- A broad native-runner LoadGen rerun executed 1492 tests with one failure in unchanged ProducerRegistrationTests: evidence_readback_failed:TaskCanceledException during its existing cleanup budget. The test and ordinary worker bytes match the base. The standard repository-runner rerun without a competing test process passed all 1490 tests with no skips, and its report validated. The scheduling cause is not established; no test or budget was changed.
- Correction tests exposed deadline cancellation wrapped by the journal as writer_failed instead of the operation outcome. The corrected deadline handling retains the sticky capture gap; its new boundary tests pass.
- The reap-at-deadline test failed with the TimeSpan-based WaitAsync path. The correction uses the TimeProvider cancellation-token path plus an elapsed-completion check; the corrected boundary tests pass.
- The first timing wrapper could not launch because `/usr/bin/time` is absent. No host test ran in that invocation; Bash `time -p` is used instead.
- The scratch policy comparison initially addressed `bounds` at the chart root. Correcting it to `runtime.bounds` made the comparison pass; no repository setting changed.
- The first Python invocations omitted the installed tools from PATH. Loading the shared tool environment made both mandatory suites pass.
- CA1859 required concrete Dictionary types in the verifier export test; corrected.
- CA1826 required direct indexing for indexable collections in the journal/supervisor; corrected.
- CA1861 required a retained constant prefix array in options; corrected.
- CA1707 required the repository's test-name suppression in new transport tests; corrected.
- xUnit1051 required the test cancellation token in new async tests; corrected.
- MSB4166 occurred during overlapping/shared-node builds; the reported diagnostic file was absent. Serialized builds with shared compilation disabled passed. Concurrent-build interference is a diagnosis, not a proven cause.
- Managed File.OpenHandle rejected a directory on Linux. The checked native directory-open implementation and its tests passed.
- KafkaOAuthBearerSession has no DisposeAsync API. The incorrect call was removed; existing host-owned authentication cleanup is retained.
- LedgerValidator.ReadValidatedAsync was initially called with the wrong argument order. Its call now matches the existing API.
- DeliveryReport.Status is nonnullable. The synthetic invalid-status test now uses an undefined enum value, which the guard rejects.
- InvalidDataException is not caught by an IOException arm. Adding it to the report-binding catch restored the required first-failure latch; the affected tests passed.

The original candidate passed the full .NET and timed host commands. The correction build and all abrupt-path tests pass; broader affected verification is recorded below.

## Cold-review input

Mode for the next review: consolidated comprehensive re-review. Input revision: the frozen HEAD on `codex/404-pr2-abrupt-restart`, identified exactly by the supplied diff filename. Comparison revision: `74be825cb6ac71c2c70a6612f3fa0910dcdc52ef`. The supplied diff contains every authored file, including this receipt and item state. Cumulative completed reviews: one comprehensive, zero focused. The first complete report follows. The second report and final cumulative counts belong in the pull request self-review record, not its own reviewed receipt.

Both the Codex and Claude reviews must explicitly check each choice under "Cap-covered choices": C1, C2, C3, report_length and UTF-8 RawJson. Hosted review and final-head Linux CI remain separate gates. This environment has no available Claude review tool or CLI; that review requires a Claude session before Hari merges.

## Predicate coverage

No verifier production predicate or mutation target is changed. E9 feeds fresh production diagnostic exports and their purpose-only, schema-only, source, image and dialect mutations into the existing registration/completion readers, artifact-index and manifest readers, publication acceptance, B10 registration intake, finalization reader and phase candidate reader. The publication test keeps its listing and unrelated completion/start-line inputs valid so the failed diagnostic index/manifest parsing remains visible. Existing schema-one writer/reader controls remain, with new registration/completion and finalization controls. Runtime and transport boundary coverage is specified by E1-E12 and recorded after execution below.

## Claim sources checked

Read on 2026-11-07:

- [Linux v6.11 x86 signal header](https://github.com/torvalds/linux/blob/v6.11/arch/x86/include/uapi/asm/signal.h) defines SIGKILL 9, SIGCONT 18 and SIGSTOP 19. These are platform constants, not measurements; unsupported architectures fail closed.
- [.NET 10 pipe handle disposal](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.anonymouspipeserverstream.disposelocalcopyofclienthandle?view=net-10.0) documents the local-client-handle disposal needed for server notice; [Flush(Boolean)](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0) documents clearing intermediate buffers. Neither proves node-loss durability.
- [.NET 10 Process.Start](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.start?view=net-10.0) and [WaitForExit](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?view=net-10.0) support the selected APIs and redirected-output limitation; actual Linux interoperability remains an E2 observation.
- [Linux v6.11 generic fcntl header](https://github.com/torvalds/linux/blob/v6.11/include/uapi/asm-generic/fcntl.h) supplies the checked directory-open constants; [open(2)](https://man7.org/linux/man-pages/man2/open.2.html) documents `O_DIRECTORY` and `O_CLOEXEC`.
- [POSIX kill](https://pubs.opengroup.org/onlinepubs/9799919799/functions/kill.html) states that signal zero checks errors without sending a signal and that a positive PID selects that process.
- [proc_pid_stat(5)](https://man7.org/linux/man-pages/man5/proc_pid_stat.5.html) identifies field 22 as start time in clock ticks; [cgroup v2](https://docs.kernel.org/admin-guide/cgroup-v2.html) identifies local, non-hierarchical memory events and group-level OOM kill counts. Neither establishes deployed availability or victim identity.

- [Confluent.Kafka v2.16.0 IProducer](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/IProducer.cs), [librdkafka v2.16.0 statistics](https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md), [introduction](https://github.com/confluentinc/librdkafka/blob/v2.16.0/INTRODUCTION.md) and [configuration](https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md) were checked for Produce, callback, Flush, counter and permitted-timeout semantics. They do not prove wire exposure or Event Hubs persistence.

## First cold-review report

Mode: initial comprehensive cold review (repo-review).
Reviewed revision: cb6d0cfa52e1ae7ad3d20af72133c19975c75a2b.
Comparison revision: 74be825cb6ac71c2c70a6612f3fa0910dcdc52ef.
Report: complete

Specification: issue 404 PR2 implementation-grade plan Revision 6, comment 6709851444, as approved by comment 6711868908; cap decision 6711519349. Applied AGENTS.md, docs/agents/pull-requests.md, CLAUDE.md, docs/agents/factory.md, blueprint ADRs (especially ADR-012/013/019/020/021), and docs/engineering/dotnet.md. The entire authored diff was reviewed; the file inventory follows below. No implementation file was edited during review.

Scope comparison: the changed paths fit the PR2 file/member map. The new workflow and mirror agree; existing workflow bytes are unchanged. Default-off routing, diagnostic isolation, scientific-reader rejection, ordinary project structure and the narrowly approved mutation exclusion are present. The new contract's wire/policy/failure sections match the approved plan except the authorized report_length raise-list correction. No PR3 implementation, scientific result, expected-outcome change, new package/project/reference, identity/network expansion or existing workflow-trigger change was found. Implementation and acceptance are incomplete in the findings below. Local evidence does not discharge the explicitly pending reviewed-head Linux CI, hosted review or Claude head-review gates.

Cap choices checked explicitly:
- C1: valid reports for both recognized arms accompany the unknown-arm case in B10AbruptSupervisorTests; the unattributable report has its own binding-invalid latch. This is the approved first option.
- C2: hand-off absence produces sample_missing, while invalid bindings latch binding_invalid without adding sample_missing for that arm. Overflow remains a separate no-message case.
- C3: SelectPreAcceptance returns null after overflow, including when a previously numbered report exists; the producer test covers loss of that earlier fallback.
- report_length: explicit 1..1048576 checks and zero/oversize tests implement the carried correction.
- RawJson: Encoding.UTF8.GetBytes writes the selected raw report without a BOM, and the test compares those actual bytes.
These choices fit the approval; they do not excuse the separate unreadable-file failure in finding 4.

Review validation: git diff --check passed. A scratch harness outside the repository called the reviewed Release implementation and reproduced three cases: stop confirmation at 29 s followed by a 2 s checkpoint flush still completed the checkpoint and sent KILL at 31 s; an unreadable fixed report path threw UnauthorizedAccessException with no arm failure and mapped to capture_gap; a NoError/PossiblyPersisted terminal was accepted by the production supervisor handler. The scratch source is /workspace/scratch/cold1-proof/Program.cs, runnable with /workspace/scratch/dotnet/dotnet run --project /workspace/scratch/cold1-proof/proof.csproj. These are isolated synthetic boundary observations, not a live trial. Source inspection also checked the production exception consumers and all added runtime tests. Pinned signal/fcntl headers, Confluent.Kafka IProducer, librdkafka statistics/configuration/introduction, and the cited Microsoft/POSIX/proc/cgroup API statements were checked for the narrow claims used here.

P1  src/Parchwell.LoadGen/Run/Abrupt/B10AbruptSupervisor.cs:308  Revision 6 "Policy fixed before any run" Arm/stop row, E4; ADR-021 protocol 6  The 30 s arm deadline is checked before draining queued frames and flushing the checkpoint, but not enforced through those operations or checked after completion. A checkpoint can therefore complete after the authorized deadline and still authorize KILL. The scratch production-method probe confirms STOP at 29 s plus a 2 s checkpoint flush results in KILL at 31 s. Bound the complete arm/checkpoint operation by its original deadline and reject late completion; add the specified below/at/above deadline cases.
P1  src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProducer.cs:323  Revision 6 item 9 (reuse existing guard meanings), policy Guards; docs/blueprint.md ADR-021 "Permitted conditions"  PossiblyPersisted is accepted whenever Error.IsError is false, even though ADR-021 permits that status only on its named timeout outcome. The same omission exists in the timeout callback and supervisor terminal validation (B10AbruptSupervisor.cs:214-218). A synthetic delivered=true/error_code=0/persistence_status=1 terminal passes the production supervisor, and the producer predicate likewise leaves faulted false. Reject PossiblyPersisted outside ApplicationRetryPolicy's permitted-timeout combination across all three paths.
P1  src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs:107  Revision 6 item 4 and failure rows b10_abrupt_signal_preflight_failed/b10_abrupt_signal_failed  Failed native-call observations are retained only on SignalException.Observation and then discarded by the production exception handlers. Preflight happens outside the supervisor try block, so its only output is this message; arm calls throw before RecordSignalAsync can record the return/errno, and failure.json also omits the exception observation. Thus the trial cannot retain the required actual return value/errno or native-load exception type on the very failure paths it must diagnose. Persist these observations for preflight and arm failures, including unavailable errno on import failure; existing tests inspect the exception object only and do not prove emitted evidence.
P1  src/Parchwell.LoadGen/Run/Abrupt/B10AbruptSupervisor.cs:255  Revision 6 wire pre_acceptance_report and failure row b10_abrupt_report_binding_invalid  The expected unreadable-file path is not normalized into the per-arm binding-invalid latch because UnauthorizedAccessException is absent from the catch filter. It escapes with ArmFailures empty; the supervisor maps it to capture_gap and the host ultimately prints configuration_invalid. A fixed report path that is a directory reproduced this using production AcceptReportAsync. Catch expected access failures as binding_invalid, preserve its first-failure exclusivity, and test the unreadable-file case required by the wire contract.
P1  infra/scripts/phase/b10_abrupt.py:503  Revision 6 "Failure and outcome names" and "Diagnostic trial and safety"  The runner publishes arbitrary inner validation strings as gap names, e.g. diagnostic descriptor mismatch, diagnostic identity and release boundary, although the approved contract gives the exhaustive emitted failure names. The collection path additionally swallows such failures at lines 551-552 without recording any named gap. Map boundary failures to their applicable approved b10_abrupt_* outcome and preserve a named collection failure, retaining subordinate detail separately if needed; otherwise a failed trial lacks the prescribed diagnostic outcome.
P1  tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptWireProducerTests.cs:148  Revision 6 E4/E6/E8/E9; docs/engineering/dotnet.md "Testing"  Required negative runtime acceptance is absent. This valid export load is the only test invocation of LoadSelectionAsync, so there is no successor-reader test for missing/changed receipt, wrong Job/pod UID/source/image/predecessor, stale selection, same pod identity or legacy complete-predecessor bytes, and no assertion that these fail before producer construction. The added tests also omit the specified virtual 30 s/10 s deadline boundaries and supervisor reap/EOF failure cases (the synthetic process always reports successful stop/exit and completes its event stream). Python publication mutations exercise a different reader and cannot substitute for the required successor checks. Complete these explicitly required cases before treating E1-E12 acceptance as met; this requests planned coverage, not additional hardening.

Findings: 6 at P0 or P1

Pre-existing (not blocking): none identified for this review.

Touched-file inventory (45, computed from the frozen diff; every path matched to the anything rubric, .NET paths additionally to dotnet standards, and workflow paths additionally to hard safety rules 8/9):
- .github/workflows/b10-abrupt-trial.yml
- charts/parchwell/templates/jobs.yaml
- charts/parchwell/tests/check.sh
- charts/parchwell/tests/test_b10_abrupt_chart.py
- charts/parchwell/tests/test_b10_abrupt_host.py
- charts/parchwell/tests/test_loadgen_host.py
- charts/parchwell/values.schema.json
- charts/parchwell/values.yaml
- docs/operations/b10-abrupt-contract.md
- docs/operations/b10-workflow.md
- docs/operations/loadgen-contract.md
- docs/operations/verifier-contract.md
- docs/operations/workflows/b10-abrupt-trial.yml.txt
- infra/scripts/lifecycle/check-offline.sh
- infra/scripts/lifecycle/dispatch-state.py
- infra/scripts/phase/b10_abrupt.py
- infra/scripts/phase/phase.py
- infra/scripts/phase/tests/check_b10_abrupt_producers.py
- infra/scripts/phase/tests/test_b10_abrupt.py
- infra/scripts/phase/tests/test_workflow.py
- infra/scripts/tests/test_dispatch_state.py
- items/404/pr2-receipt.md
- items/404/state.md
- src/Parchwell.LoadGen/Program.cs
- src/Parchwell.LoadGen/Run/Abrupt/B10AbruptCgroup.cs
- src/Parchwell.LoadGen/Run/Abrupt/B10AbruptChildChannel.cs
- src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs
- src/Parchwell.LoadGen/Run/Abrupt/B10AbruptJournal.cs
- src/Parchwell.LoadGen/Run/Abrupt/B10AbruptOptions.cs
- src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProc.cs
- src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProducer.cs
- src/Parchwell.LoadGen/Run/Abrupt/B10AbruptPublisher.cs
- src/Parchwell.LoadGen/Run/Abrupt/B10AbruptStatistics.cs
- src/Parchwell.LoadGen/Run/Abrupt/B10AbruptSupervisor.cs
- src/Parchwell.LoadGen/Run/Abrupt/B10AbruptWire.cs
- src/Parchwell.LoadGen/Run/Abrupt/OwnedProducerProcess.cs
- tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptCgroupTests.cs
- tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptJournalTests.cs
- tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptOptionsTests.cs
- tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProcessTests.cs
- tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProducerTests.cs
- tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptSupervisorTests.cs
- tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptWireProducerTests.cs
- tests/Parchwell.Verifier.Tests/Host/B10AbruptRejectionTests.cs
- tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj

## First-review dispositions

All six P1 findings are accepted as implementation or required-coverage defects. Corrections preserve the approved protocol, policy values, guards and failure vocabulary; no finding is waived.

- R1: retain the original arm deadline through queued-frame drain and checkpoint completion, checking it before experimental KILL. Apply the existing kill, reap and EOF budgets to observed completion and add virtual boundary cases.
- R2: use one callback error/status predicate in timeout, probe, successor and supervisor paths. PossiblyPersisted is permitted only for the existing actual-timeout combination; the supervisor also checks delivered/error agreement.
- R3: the host emits the actual SignalException observation, including null unavailable values on native-load failure. The supervisor retains that observation in its failed diagnostic. Tests inspect emitted evidence.
- R4: normalize expected access failures on the fixed report file into the existing per-arm binding-invalid latch; preserve first-failure exclusivity.
- R5: emit only approved phase failure names, retain subordinate validation detail separately, and record collection failures. Unknown termination always preserves safety_stop, including malformed nested pod data caught by outer handlers.
- R6: add actual successor-reader negative cases using newly produced bytes and the production host's validation-before-continuation boundary. Rejected input never enters the continuation that registers/constructs the producer. Add the required virtual deadline and supervisor reap/EOF failures. No test contacts Azure.

The first review accounts for six review-finding root causes against the policy cap of 20. Correction verification:

- Release LoadGen.Tests build passed with zero warnings/errors in 25.51 s. All 270 abrupt-path tests passed with no failures/skips in 12.101 s.
- The fresh production worker passed eight facts in 11.508 s and produced `/workspace/scratch/b10-export-cold1-fixed`. The Python consumer passed ten produced-byte tests in 0.154 s with its exact success marker. Loaded versions remain the measured values above.
- The phase/diagnostic/workflow regression command passed 128 tests in 22.576 s.
- The Verifier rejection class passed both tests in 12.963 s against the corrected worker, including scientific rejection of actual newly generated bytes.
- All 13 runtime source hashes and all seven abrupt test source hashes match bytes present in their current Release PDBs.
- The complete LoadGen suite passed through the standard repository command: 1490 succeeded, zero failures/skips, duration 1m 22s 358ms; the repository report validator passed. The corrected whole host acceptance also passed: Bash time measured real 132.11 s, user 85.18 s and system 26.94 s, within the unchanged 900 s bound. It regenerated the production-byte bridge inputs and checked them successfully.

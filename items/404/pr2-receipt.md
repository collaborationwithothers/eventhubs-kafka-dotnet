# PR2 receipt: inactive B10 restart execution

This pull request implements only the diagnostic restart execution approved for issue 404. It cannot authorize scientific publication; Hari merges it and separately approves the post-merge trial environment.

## Execution card

- Deliverable and exact scope: [Revision 6 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6709851444), file map items 1-20 and acceptance E1-E12. PR3 is excluded.
- Authorization: [PR2-only approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6711868908), with [cap decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6711519349). REST verification matched updated_at `2026-11-07T18:45:47Z`, 126836 characters, SHA-256 `4a6a093a00b909d8bbea623983c8aebd404773b3a81fe2c7bb116e4e76596376`.
- Base: `74be825cb6ac71c2c70a6612f3fa0910dcdc52ef`, merged PR1. The plan's Authority and predecessor snapshot identifies binding decisions and ADR dependencies.
- Mandatory gates: exact plan acceptance below, source and policy equality, default-off/scientific rejection, Linux process proof, cold review under pull-requests.md rule 8, hosted Codex review and Claude head review checking every cap-covered choice, final-head CI, and Hari's merge. No workflow dispatch, Azure execution or live B10 evidence access is authorized in this session.
- Earliest publication checkpoint: tested implementation after applicable cold-review coverage; CI and hosted review may run together.
- Original review budget and exhaustion: plan section Mutation, review budget and bounded work and docs/agents/pull-requests.md rule 8. The original two comprehensive passes and two focused rounds were used before the recorded handoff. The bounded continuation authorized by the later user instruction is recorded below; prior counts are not reset.

### Authorized hosted-P1 continuation

The preceding correction head `0a90574b54ea97c17b7524bb2e917b8d7e44b374` has passing [.NET/lifecycle/Terraform CI](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42449092565) and [documentation CI](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42449092559). Hosted code review still found the defects below; passing tests did not discharge them.

After the [hosted-review handoff](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/406#issuecomment-6717558668), the user replied on 2026-11-08: "yes ifx them". This directly authorizes correcting the [successor disposal capture](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/406#discussion_r4505115034) and [stopped checkpoint synchronization](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/406#discussion_r4505115039) findings, their regression tests and verification, and one focused correction review before publication. It does not reset the cumulative two comprehensive and three focused reviews, authorize another comprehensive pass, or expand PR3. The deliverable is faithful execution of the existing disposal/capture and drain-before-checkpoint requirements; protocol sequences, gates, policy values, failure names, scientific text and safety properties remain unchanged.

Verification commands: `./scripts/verify-dotnet.sh`, the Release LoadGen runner filtered to the affected abrupt classes, `bash charts/parchwell/tests/check-loadgen-host.sh`, `npm run test:docs` and `npm run check:docs`, using the pinned local tools. Existing unchanged chart/Python coverage is retained and mandatory final-head Linux CI remains required. The earliest publication checkpoint is consolidated corrections, passing affected verification and complete focused review, after authority refresh. Additional findings outside this bounded set, unsafe review focus or a required change to the approved protocol stops for Hari. Hosted final-head review, Claude confirmation and Hari's protected-path merge remain separate gates.

### Authorized review-comment continuation

After the [budget handoff](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/406#issuecomment-6713457687), the user instructed this implementing session on 2026-11-08: "fix the comments". This resumes the bounded corrections in [Claude part A](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/406#issuecomment-6713375550) and [Claude part B](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/406#issuecomment-6713787031), their affected verification and the required focused correction review. The reported finding count is eight, computed from the six part-A and two part-B entries. This is direct task authorization, not a new issue-plan approval, a reset of historical counts or permission for another comprehensive review. A finding requiring a protocol, gate, policy, scientific or safety change still stops for Hari. Further work outside this correction set requires a new handoff; no open-ended correction loop is authorized.

The prior full local verification and final-head CI are historical evidence for `7217ce4d955e3a77a26ec31278a82f61d5d31841`. This continuation will run the warning-as-error build and full .NET verification, the affected abrupt/process/publication tests and produced-byte bridge, whole host acceptance, and documentation checks. Unchanged chart/Python/workflow behavior keeps its prior passing coverage and final-head CI runs again automatically after the reviewed push. No workflow dispatch is authorized.

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

The review-comment continuation also records these failure-name choices under the cap decision:

- Publication storage exceptions leave the create/read-back outcome unconfirmed and are reported as `b10_abrupt_publication_ambiguous`; duplicate publication is also ambiguous. Local validation/size failures and an observed read-back byte mismatch remain `b10_abrupt_publication_failed`. Both outcomes block successful publication without overwrite or retry.
- Successor termination collection retains the closed name `b10_abrupt_first_termination_unknown` and its existing safety stop. At boundary `successor_termination`, it describes the successor observation gap. The contract now tells operators to read the recorded boundary with the gap name; no new failure name or safety behavior is introduced.

## Mechanical representation choices

- The successor binding carries the original authenticated start-line, broker inventory and completion strings in `start_line_binding`. This gives the successor the existing expected-broker inputs through its sole permitted binding path; the complete input remains subject to the fixed byte cap.
- Diagnostic collection permits a zero-byte `request-lifecycle.jsonl` because an empty capture has no events. The shared download helper retains its existing nonempty default for every other caller.
- The cgroup directory is retained with a narrow Linux `open` call and a safe handle. Ordinary managed file opening rejects directories on this platform. Directory identity is checked through the retained descriptor without altering the cgroup/OOM exclusion gate.

- Selection includes the full checkpoint frame and its `{name,length,sha256}` descriptor. The runtime binds the exact serialized frame bytes; phase compares the descriptor with the authenticated raw journal line and compares the parsed frame, avoiding a cross-language reserialization assumption.

- Resume progress uses a create-only local child acknowledgement after reading the authenticated control checkpoint. The supervisor checks the checkpoint and identity binding within the existing arm budget. This implements `b10_abrupt_resume_unconfirmed` without adding an IPC kind, signal or publication member.

- Deadline-name alignment: after stopped state is observed, exhaustion during drain/checkpoint uses the existing `b10_abrupt_deadline_exhausted` outcome for the enclosing arm deadline. `b10_abrupt_stop_unconfirmed` remains for missing/late stopped observation. A successful native KILL observed outside its budget likewise reports deadline exhaustion, while `b10_abrupt_signal_failed` remains an actual native failure. Sticky journal gaps and actual signal observations are retained; the budgets and admission comparisons are unchanged.

## Local file hand-offs and supervisor exit

The two anonymous pipes carry the closed IPC messages and acknowledgements. The implementation also shares local files within the first pod's `OutputRoot` (`/work/loadgen`); it is not a pipes-only exchange. These existing hand-offs are implemented by [B10AbruptHost](../../src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs), [B10AbruptProducer](../../src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProducer.cs) and [B10AbruptSupervisor](../../src/Parchwell.LoadGen/Run/Abrupt/B10AbruptSupervisor.cs).

| Local hand-off | Writer and reader | Binding and limit of the observation |
| --- | --- | --- |
| `abrupt-child/<arm>-pre-acceptance-report.json` | Child writes; supervisor reads before accepting the report hand-off | The IPC descriptor binds the raw UTF-8 bytes, identity, sequence and capture time. |
| `abrupt-child/timeout-seal.json` and `abrupt-child/control-seal.json` | Child writes; supervisor reads before the next probe | The supervisor checks envelope identity, cohort, drain/guard status and artifact descriptors. The timeout ledger, inventory and seal originate in the child; their agreement is self-consistency, not an independent broker observation. Control ledger rows are also compared with the supervisor's journal terminal frames. |
| `timeout/` and `control/` ledger, manifest and `post-drain-report.json` | Child writes; supervisor reads when validating each seal | The seal binds these bytes by descriptor. They remain separate cohort artifacts. |
| `abrupt-child/<arm>-payload.bin` | Child writes each probe payload; supervisor reads `cross_restart-payload.bin` when finalizing selection | The cross-restart payload's length and SHA-256 must equal its prepared frame. |
| `abrupt/<arm>-checkpoint.json` | Supervisor writes; resumed control child reads `same_instance-checkpoint.json` for `release_utc` | The child checks frame kind, identity and arm. The kill-arm checkpoint is carried to the successor in the authenticated selection binding, not read by a resumed first child. |
| `abrupt-child/same_instance-resumed.json` | Control child writes; supervisor reads after CONT | The create-only acknowledgement binds identity, arm, checkpoint sequence and hash. It confirms child progress through the checkpoint read, not broker acceptance. |

The supervisor's configured process watchdog calls `Environment.Exit(124)` after 900 s. This call is not the managed `AbortAsync` cleanup path and does not itself signal the child. [.NET 10 Environment.Exit](https://learn.microsoft.com/en-us/dotnet/api/system.environment.exit?view=net-10.0) states that `finally` blocks do not execute when Exit is called from a try/catch block. Cleanup therefore cannot rely on managed disposal after this abrupt process exit.

For a deployed pod, ending a child after the supervisor exits abruptly depends on the container/PID-namespace lifecycle outside that managed path. [Linux pid_namespaces(7)](https://man7.org/linux/man-pages/man7/pid_namespaces.7.html), "The namespace init process", states that the kernel sends SIGKILL to the namespace's processes when its init process terminates. Whether the deployed supervisor is that init process has not been verified in a trial. A standalone host launch has no such established containment guarantee and may leave its child alive. Neither the watchdog nor a supervisor exit code proves child death, reaping or EOF. The existing observation requirements and unknown-termination safety stop remain binding; no lifetime, signal, policy or protocol change is made here.

## Verification and review status

The original implementation completed two comprehensive and two focused cold reviews, with the last focused review finding no P0 or P1 on `7217ce4d955e3a77a26ec31278a82f61d5d31841`. Its [final .NET, lifecycle and Terraform CI](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42416876916) and [documentation CI](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42416876910) passed. The resumed review-comment corrections require their own affected verification and focused review. No live trial has run in this session. The following results describe the original implementation and its earlier correction commits.

- `./scripts/verify-dotnet.sh` passed with locked restore, Release build (zero warnings/errors), architecture checks, all project tests, checked reports and the warning-as-error control. Measured report counts: Architecture 16, Enrichment 56, Kafka 486, LoadGen 1429, Scenarios 340 and Verifier 7389. Logs remained outside the repository.
- Baseline, before chart changes, source `74be825cb6ac71c2c70a6612f3fa0910dcdc52ef`: `bash charts/parchwell/tests/check-loadgen-host.sh` passed. Bash `time -p` measured real 121.04 s, user 90.88 s, system 21.96 s. Initial candidate post-change: the same whole script passed, including E10/E11, with Bash `time -p` measuring real 114.19 s, user 89.66 s and system 29.36 s, within the unchanged 900 s bound. A local `timeout 900` enforced that bound.
- `npm ci --ignore-scripts` completed; `npm run test:docs` passed 28 tests with no failures or skips.
- `bash charts/parchwell/tests/check.sh` passed; measured elapsed time was 264.13 s. `bash infra/scripts/lifecycle/check-offline.sh` passed with exit 0.
- `npm run check:docs` reported eight warnings, all in unchanged files.
- The phase, diagnostic and workflow Python command passed 125 tests in 31.212 s on the final phase implementation after loading the installed tool environment. Its first invocation omitted that PATH and failed at Helm execution; this is one environment failure root cause shared with the script-discovery invocation, whose retry passed 326 tests in 88.403 s.
- Scripted preservation found 45 authored changed paths within the approved file map, no non-ASCII punctuation, all six existing workflows unchanged, an identical new workflow mirror, unchanged helper/host-runner files and exactly the authorized project-file replacement.
- The final targeted transport run passed 79 tests, zero failures/skips, in 2.915 s on Debian 13, .NET 10.0.13 and kernel 6.18.57. Production process tests observed inherited pipes, stop, lack of progress while stopped, resumed progress, kill, wait/reap and EOF. The observed exit code 137 is labelled local evidence, not a death predicate. The subsequent reviewed-head Linux GitHub Actions proof is recorded below.
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

Mode for the next review: focused correction review authorized by the user's 2026-11-08 "fix the comments" instruction after the prior handoff. Input revision: the frozen HEAD identified by the supplied diff filename. Full comparison revision: `74be825cb6ac71c2c70a6612f3fa0910dcdc52ef`. Focused comparison revision: `7217ce4d955e3a77a26ec31278a82f61d5d31841`. Review the eight Claude findings, the corresponding runtime/test and additive explanation changes, affected callers/contracts and this receipt/state update; the full PR diff is context. Historical cumulative reviews: two comprehensive and two focused. This is an additional focused round under the resumed task authorization, not a third comprehensive pass or a counter reset. Stop if safe focus is impossible or new work exceeds the resumed correction set.

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

## Second cold-review report

Review mode: consolidated comprehensive re-review (second comprehensive pass).
Reviewed revision: cd3bae9df5dea3e03372a0ad42896f34d83b54f6.
Comparison revision: 74be825cb6ac71c2c70a6612f3fa0910dcdc52ef.
Input diff: /workspace/scratch/404-cold-2-cd3bae9df5dea3e03372a0ad42896f34d83b54f6.diff.
Authored receipt: items/404/pr2-receipt.md.
Report: complete

Applied the repository-owned repo-review skill to the entire supplied diff. All 45 changed files were matched to the general rubric; .NET files additionally to docs/engineering/dotnet.md, Kafka settings to the pinned workload inventory, and the workflow to the workflow safety rules. Read the applicable AGENTS.md, CLAUDE.md, pull-request and factory governance, blueprint ADRs and engineering rules. No changed governance source weakens the review rules.

Specification comparison: read the current approved Revision 6 plan (issue comment 6709851444), Hari's pinned approval (6711868908), and the recognized cap decisions (6711519349). The changed paths fit the approved PR2 map. Default-off routing, diagnostic-only registration/publication, scientific-reader rejection, ownership of the direct producer child, durable checkpoints, independent workflow acceptance, the separate trial workflow, and the limited mutation-mode exclusion are implemented. The workflow mirror is byte-identical. The contract policy and failure table match the approved plan after the authorized report_length clarification; the wire section has only that same authorized insertion. No PR3 implementation, result/verdict or expected-outcome change, new project/reference/package, identity/network expansion, or existing workflow trigger change was found. The two requirements below remain incomplete. Linux CI, hosted review, final-head review, merge, and a live trial remain separate gates; this review does not discharge them.

P1  src/Parchwell.LoadGen/Run/Abrupt/B10AbruptPublisher.cs:126  Approved Revision 6 items 6 and 14, E6, and successor_binding_invalid failure row; docs/agents/pull-requests.md rule 8 specification comparison  The successor's independent pre-producer check never reconciles entries[].attempt_id with the authenticated checkpoint's selected_attempt_ids, candidate_attempt_ids and terminal_exclusions. Its release/count/payload checks accept an original attempt that was never selected. Starting from fresh production exports, changing only the entry attempt_id to synthetic-first-timeout-0 and rebinding the selection, manifest and termination descriptors reached the actual RunValidatedSuccessorAsync continuation while the checkpoint still selected synthetic-first-cross_restart-2. The Python workflow check does reject this inconsistency, but that does not satisfy the separately required independent successor boundary. Reconcile the selected original identity and checkpoint membership/exclusions before the continuation and test this negative with all unrelated hashes and identities valid.
P1  src/Parchwell.LoadGen/Run/Abrupt/B10AbruptSupervisor.cs:506  Approved Revision 6 item 7 (cleanup signal is labelled cleanup), item 4/measures (actual signal return and errno), and b10_abrupt_signal_failed failure row; docs/agents/pull-requests.md rule 5 all-instances correction  Cleanup KILL failures still discard their actual native-call observation. Kill() is evaluated before RecordSignalAsync, so a SignalException produces no cleanup signal record; the blanket catch retains only writer_failed, and RunAsync's failure.json emits only the original exception's SignalObservation. A fake owned child returning KILL failure -1/errno 13 through the production AbortAsync produced zero frames and only writer_failed. Thus a report/guard failure followed by cleanup signal failure loses the cleanup errno and misidentifies it as a writer failure. Retain a labelled cleanup failure observation independently of the original abort cause, including native-load failure details, and verify emitted evidence. This is a remaining instance of the first review's failed-signal-observation root cause, whose primary preflight and arm paths were corrected.

Findings: 2 at P0 or P1

Pre-existing (not blocking):
None identified during this review.

Verification performed for this review:
- git diff --check over the reviewed base/head passed.
- Fresh production wire exports were generated by the Release B10AbruptWireProducerTests class: 8 tests passed, zero failures/errors/skips, measured 11.488 s.
- The Python produced-byte checker consumed those fresh exports: 10 tests passed, measured 0.213 s, and printed b10_abrupt_produced_bytes_checked.
- An isolated scratch harness exercised the production successor boundary and AbortAsync, reproducing both findings. Its output was: CONTINUATION ENTERED: selected=synthetic-first-timeout-0; checkpoint=synthetic-first-cross_restart-2; CLEANUP: frames=0 gaps=writer_failed. Both reviewed production source checksums matched the Release portable PDB. Harness: /workspace/scratch/cold2-proof/Program.cs.
- Read the complete new test scope and the authored predicate map, including the first-review corrections. Deadline/checkpoint boundaries, permitted persistence predicates, unauthorized-file failure naming, phase acceptance failures, and actual successor/reap/EOF coverage materially address the earlier findings; the two gaps above remain.
- Checked the pinned/native reference material used for Linux signal numbers, open flags, process start identity and exit observation, local cgroup events, pipe EOF ownership, durable Flush semantics, and the pinned producer callback/Flush API. The review does not claim a live Kafka/Azure experiment.

Cumulative cold-review budget after this report: 2 comprehensive passes, 0 focused rounds. Any correction should receive a focused review of changes since this reviewed revision, unresolved findings and affected callers/tests/contracts, with the full PR diff as context. At most 2 focused rounds remain. If safe focus is impossible or another comprehensive pass is needed, use the mandated Hari handoff; do not start a third comprehensive pass without his explicit decision.

Complete changed-file inventory:
.github/workflows/b10-abrupt-trial.yml
charts/parchwell/templates/jobs.yaml
charts/parchwell/tests/check.sh
charts/parchwell/tests/test_b10_abrupt_chart.py
charts/parchwell/tests/test_b10_abrupt_host.py
charts/parchwell/tests/test_loadgen_host.py
charts/parchwell/values.schema.json
charts/parchwell/values.yaml
docs/operations/b10-abrupt-contract.md
docs/operations/b10-workflow.md
docs/operations/loadgen-contract.md
docs/operations/verifier-contract.md
docs/operations/workflows/b10-abrupt-trial.yml.txt
infra/scripts/lifecycle/check-offline.sh
infra/scripts/lifecycle/dispatch-state.py
infra/scripts/phase/b10_abrupt.py
infra/scripts/phase/phase.py
infra/scripts/phase/tests/check_b10_abrupt_producers.py
infra/scripts/phase/tests/test_b10_abrupt.py
infra/scripts/phase/tests/test_workflow.py
infra/scripts/tests/test_dispatch_state.py
items/404/pr2-receipt.md
items/404/state.md
src/Parchwell.LoadGen/Program.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptCgroup.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptChildChannel.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptJournal.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptOptions.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProc.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProducer.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptPublisher.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptStatistics.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptSupervisor.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptWire.cs
src/Parchwell.LoadGen/Run/Abrupt/OwnedProducerProcess.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptCgroupTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptJournalTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptOptionsTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProcessTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProducerTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptSupervisorTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptWireProducerTests.cs
tests/Parchwell.Verifier.Tests/Host/B10AbruptRejectionTests.cs
tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj

## Second-review dispositions

Both P1 findings are accepted; neither is waived. There are seven distinct review-finding root causes: the second cleanup finding is another instance of R3, and successor membership is new.

- Successor membership: require the single selected original to match the checkpoint selection and candidate list, with no terminal exclusions, before the producer continuation. These are the existing fixed single-probe reconciliation rules already enforced by the phase reader. Production-byte mutations alter the entry, candidates or exclusions while rebinding the outer descriptors and preserving payload/identity checks; the actual host continuation must remain unentered.
- Cleanup observation: retain a separately labelled cleanup observation and failure in the existing failed diagnostic, independently of the original abort cause. Preserve actual return/errno and native-import exception values even when journal capture fails. Only actual journal failures contribute capture failure. The cleanup request remains single-shot and success wire members are unchanged. Actual RunAsync tests cover native errno/import failures with healthy and failed journals and reap failures.

Correction verification (working tree based on `cd3bae9df5dea3e03372a0ad42896f34d83b54f6`, before the focused-review commit):

- Serialized Release LoadGen.Tests build with warnings as errors passed with zero warnings/errors in 35.95 s.
- All 284 abrupt-path tests passed with zero failures/skips in 12.501 s.
- Fresh production-byte worker: eight tests passed in 12.038 s; independent Python checker: ten tests passed in 0.136 s with `b10_abrupt_produced_bytes_checked`. Export path: `/workspace/scratch/b10-export-cold2-fixed`.
- Loaded versions measured by the worker: Confluent.Kafka assembly 2.16.0.0, librdkafka 2.16.0, .NET 10.0.13.
- No new verification-failure group; the cumulative count remains 17 against the approved cap of 20. Scripted review-root grouping gives seven distinct roots across the two comprehensive reports.

## First focused cold-review report

Review mode: focused correction review (first focused round).
Reviewed revision: 0de1514e040b0fe25fbd5786b8d1b47d808e41ed.
Focused comparison revision: cd3bae9df5dea3e03372a0ad42896f34d83b54f6.
Full-PR comparison revision: 74be825cb6ac71c2c70a6612f3fa0910dcdc52ef.
Focused input: /workspace/scratch/404-focused-focused1-0de1514e040b0fe25fbd5786b8d1b47d808e41ed.diff.
Full context: /workspace/scratch/404-full-focused1-0de1514e040b0fe25fbd5786b8d1b47d808e41ed.diff.
Authored receipt: items/404/pr2-receipt.md.
Report: complete

Applied the repository-owned .agents/skills/repo-review/SKILL.md. This round covers the changes since the second comprehensive review, both unresolved findings, and their affected callers, tests and contracts. It does not repeat a comprehensive review of the full PR. The two supplied diffs were compared byte-for-byte with git diff of the stated revisions and matched. The focused diff contains five paths; the full contextual diff contains 45 paths (script-computed).

Focused file/rubric coverage:
- items/404/pr2-receipt.md: general rubric, including governance, review protocol/budget, authority recognition, writing and claim provenance.
- src/Parchwell.LoadGen/Run/Abrupt/B10AbruptPublisher.cs: general rubric and .NET engineering standards.
- src/Parchwell.LoadGen/Run/Abrupt/B10AbruptSupervisor.cs: general rubric and .NET engineering standards.
- tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptSupervisorTests.cs: general rubric and .NET engineering/testing standards.
- tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptWireProducerTests.cs: general rubric and .NET engineering/testing standards.

Applied AGENTS.md, CLAUDE.md, docs/agents/pull-requests.md, docs/agents/factory.md, docs/blueprint.md's ADRs and scope, and docs/engineering/dotnet.md. The skill and these governing sources are unchanged from origin/main. Affected context includes B10AbruptHost.RunValidatedSuccessorAsync and its producer continuation, the native signal/exit boundary in OwnedProducerProcess, journal failure handling and wire serialization, the phase reader's selection reconciliation, the abrupt contract, and the retained producer/statistics/report-binding tests. No changed governance document relaxes this review.

Specification comparison:
Read the approved Revision 6 plan at issue comment 6709851444, approval 6711868908 and cap decision 6711519349 through GitHub REST. The plan still matches the approval's updated_at (2026-11-07T18:45:47Z), character count (126836) and SHA-256 (4a6a093a00b909d8bbea623983c8aebd404773b3a81fe2c7bb116e4e76596376), computed from the fetched body. The corrections fit file-map items 6, 7 and 14, the closed Selection bookkeeping, E6, and the named signal/capture/successor failure rows. They preserve the fixed diagnostic-only scope, policy values, protocol sequence and single cleanup KILL request. No scientific claim, expected outcome, result, workflow, package, project/reference or identity/network permission changes in this focused diff. No missing, partial or incorrect requirement or scope expansion was identified in the reviewed correction scope.

Disposition of the second comprehensive review's unresolved findings:
1. Successor membership: resolved. LoadSelectionAsync now requires exactly one checkpoint-selected original, equality of candidate IDs with that checkpoint selection, no terminal exclusions, and an entry attempt ID equal to the selected original. These checks run before RunValidatedSuccessorAsync enters the continuation that registers and constructs the producer. The added production-byte mutations alter the entry identity, candidates or exclusions while rebinding selection and manifest descriptors; they assert the exact selection_stale failure and zero continuation entries. A valid production binding still enters once. This addresses the independently required successor boundary, alongside the existing workflow reader.
2. Cleanup signal observations: resolved. AbortAsync retains cleanup's labelled observation and failure independently of the original abort cause, records actual signal return/errno when available, and retains native-import exception details with unavailable values left null. Journal failure is recorded separately as capture_failure. Native signal and reap failures no longer manufacture writer_failed. RunAsync emits the failed diagnostic to stderr before opening the local diagnostic file, preserving an output route if that file cannot be opened. Tests exercise actual RunAsync failure diagnostics for errno failure and each supported native-import failure with healthy, already-failed and cleanup-failing journals; they also cover false and throwing reap observations. They assert the original cause, labelled cleanup evidence, appropriate capture gaps, no successful termination publication and no second KILL.

Cap-covered choices checked explicitly against approval 6711868908:
- C1: the unknown-arm test supplies valid reports for both recognized arms, and retains only the unknown arm's binding-invalid latch.
- C2: missing hand-off remains sample_missing; invalid binding remains binding_invalid with first-failure exclusivity. Overflow is a separate no-message case.
- C3: overflow prevents SelectPreAcceptance from falling back to a previously numbered report; the retained producer test covers that behavior.
- report_length: values outside 1..1048576 map to binding_invalid, with zero and oversized negative cases and file-size boundary coverage retained.
- RawJson: the producer uses Encoding.UTF8.GetBytes on the selected raw report, and the retained test compares the actual file bytes and hash with that encoding.
These choices remain within the recorded approval and are not changed by this correction round.

Findings: none at P0 or P1

Pre-existing (not blocking):
None identified in the affected review context.

Verification performed:
- git diff --check cd3bae9df5dea3e03372a0ad42896f34d83b54f6 0de1514e040b0fe25fbd5786b8d1b47d808e41ed passed.
- Ran the Release B10AbruptSupervisorTests and B10AbruptWireProducerTests classes together using /workspace/scratch/dotnet/dotnet tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptSupervisorTests -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptWireProducerTests. Result: 79 tests, zero errors/failures/skips/not-run, measured 11.403 s. Log: /workspace/scratch/404-focused1-tests.log.
- SHA-256 checksums of both changed runtime sources and both changed test sources occur in their Release portable PDBs. The runtime DLL/PDB loaded from the test output are byte-identical to the source project's Release output. This ties the executed tests to the reviewed correction bytes.
- Read the changed tests for masking failures: successor mutations rebind unrelated outer hashes, retain valid identities/payloads, and use a valid counterpart; cleanup tests inspect emitted failure.json rather than only the thrown exception.

Cumulative cold-review budget after this complete report: two comprehensive passes and one focused correction round. One focused round remains under docs/agents/pull-requests.md rule 8; no additional comprehensive pass is authorized by this report. The earlier unchanged work retains its existing coverage. Hosted Codex review, Claude head review including the cap-covered choices, final-head Linux CI, Hari's merge, and any separately approved post-merge trial remain distinct gates. No Azure operation or live experiment was performed in this review.

## Linux CI and shallow-checkout correction

Reviewed source: `0de1514e040b0fe25fbd5786b8d1b47d808e41ed`, [PR 406](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/406).

- [Ubuntu-24.04 .NET verification job](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42415691021/job/128533153955) passed. The unchanged workflow checks out the PR head and runs `./scripts/verify-dotnet.sh`. The checked reports record Architecture 16, Enrichment 56, Kafka 486, LoadGen 1504, Scenarios 340 and Verifier 7389 passing tests. The Linux real-process facts are included in the LoadGen assembly and cannot skip on Linux; this is E2's required Linux CI proof. The warning-as-error control, Kafka mapping coverage and worker image checks also passed.
- Terraform verification passed in that run. [Documentation CI](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42415691059) passed. The overall .NET workflow conclusion was failure because its separate [lifecycle job](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42415691021/job/128533154141) failed; no overall CI pass is claimed.
- The lifecycle job ran 329 tests in 54.737 s with one error: `test_mirror_and_all_preexisting_workflows_unchanged` invoked `git ls-tree` for the pinned base commit, absent from Actions' shallow checkout. A local reproduction with full history passed 329 tests in 64.256 s; it did not reproduce the missing-history condition. The completed-run archive supplied the diagnosis after the job-log redirect returned HTTP 403.
- Correction: the same preservation test compares current workflow bytes to SHA-256 values measured from all six existing workflows at the pinned base. It retains the new workflow's exact mirror assertion and requires every baseline file to exist. It needs no history, network fetch, workflow edit or skipped assertion. The constants were recomputed against the original base bytes; existing workflows remain byte-identical.
- [Hosted review summary](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/406#issuecomment-6713205151) reports completed code and security reviews for this head; code review reports no findings and no inline finding was posted. The fresh-context focused review explicitly checked all five cap-covered choices. Claude review remains unavailable in this session and required before Hari merges. The final head still needs its own CI and hosted review, recorded in the PR description.

Disposition accounting: the shallow-checkout test dependency adds one verification-failure root, and a corrected receipt trailing-blank-line `git diff --check` warning is conservatively counted as another. Scripted cumulative verification-failure count is 19 against the cap of 20. The GitHub log-download access issue is recorded as a read-access limitation, resolved by the completed-run archive, not a test failure. The review-finding root count remains seven.

Correction verification before final focused review:

- `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` passed all 329 tests in 72.837 s; wrapper wall time 73.212 s.
- A scratch tree containing the current workflow test and its required files, with no `.git` directory or parent repository, passed all six B10AbruptWorkflowTests in 0.089 s. `git rev-parse` returned 128 there, confirming the missing-history condition. No repository workflow was edited.
- A script recomputed all six pinned hashes from the original base and verified both the complete baseline file set and current byte equality. `git diff --check` passed.

## Prior second focused cold-review report

Review mode: focused correction review (second and final focused round).
Reviewed revision: 7217ce4d955e3a77a26ec31278a82f61d5d31841.
Focused comparison revision: 0de1514e040b0fe25fbd5786b8d1b47d808e41ed.
Full-PR comparison revision: 74be825cb6ac71c2c70a6612f3fa0910dcdc52ef.
Focused input: /workspace/scratch/404-focused-focused2-7217ce4d955e3a77a26ec31278a82f61d5d31841.diff.
Full context: /workspace/scratch/404-full-focused2-7217ce4d955e3a77a26ec31278a82f61d5d31841.diff.
Authored receipt: items/404/pr2-receipt.md.
Report: complete

Applied the repository-owned .agents/skills/repo-review/SKILL.md. This is the focused review of the shallow-checkout preservation-test correction and the receipt/state updates since the first focused review. No unresolved finding was carried into this round. The full PR is context; this report does not repeat a comprehensive review. Both supplied diffs match git diff of their identified revisions byte for byte. Script-computed scope: three focused paths and 45 full-context paths.

File/rubric coverage:
- infra/scripts/phase/tests/test_workflow.py: general rubric, governance, assertion integrity, specification and affected test contracts.
- items/404/pr2-receipt.md: general rubric, review protocol/budget, authority recognition, claim provenance and writing rules.
- items/404/state.md: general rubric plus factory.md Item state file and authority/next-action requirements.

Read and applied AGENTS.md, CLAUDE.md, docs/agents/pull-requests.md, docs/agents/factory.md and docs/blueprint.md's ADRs and scope. Also checked docs/engineering/dotnet.md Testing for the receipt's Linux evidence and unchanged process-test context. These governing documents and the skill are unchanged from origin/main. Affected context includes the complete B10AbruptWorkflowTests class, its duplicate-key loader and workflow/mirror inputs, the baseline workflow file set, dotnet-verification.yml's head checkout and verification command, the actual Linux process test, the prior review record and the five cap-covered choices. No new workflow edit, trigger, runtime behavior, verifier predicate, project/reference/package, result, expectation, verdict, identity or network permission occurs in this focused diff.

Specification comparison:
Fetched approved Revision 6 (issue comment 6709851444), PR2-only approval 6711868908 and cap decision 6711519349 through GitHub REST. Computed plan updated_at 2026-11-07T18:45:47Z, 126836 characters and SHA-256 4a6a093a00b909d8bbea623983c8aebd404773b3a81fe2c7bb116e4e76596376 match the approval. Applied authority recognition under factory.md Item lifecycle step 3.

The correction implements the existing file-map item 16 and E12 preservation requirement: compare the new workflow and mirror bytes and require every pre-existing workflow to remain byte-identical to the pinned base. It substitutes measured SHA-256 constants for runtime Git-history reads. An independent script enumerated the pinned base tree and proved exact set equality with all six constants, recomputed each hash from original base bytes, and compared current bytes with those original bytes. The constant mapping is nonempty and complete. The removed nonempty-list assertion is satisfied by that fixed, reviewed mapping; no baseline file or preservation outcome is dropped. The mirror assertion remains exact byte equality. This removes a checkout-history prerequisite without changing workflow bytes or the preservation contract.

Receipt/state updates accurately distinguish the reviewed runtime's successful Ubuntu .NET job from the overall failed workflow, and retain final-head CI, hosted review, Claude review, Hari's merge and separately approved trial as distinct gates. PR3 remains unapproved. No missing, partial or incorrect requirement, scope creep or unsafe focus was identified in this correction scope.

CI and claim verification:
- GitHub REST identifies run 42415691021 with head 0de1514e040b0fe25fbd5786b8d1b47d808e41ed and conclusion failure. Its .NET job 128533153955 and Terraform job succeeded; lifecycle job 128533154141 failed. Documentation run 42415691059 succeeded on the same head.
- The completed-run log archive records ubuntu-24.04, checkout of that exact head with depth 1, and checked passing reports of Architecture 16, Enrichment 56, Kafka 486, LoadGen 1504, Scenarios 340 and Verifier 7389. The actual Linux process test exercises production stop/resume/kill/exit/EOF and has no Linux skip path. The receipt's E2 claim is scoped to that reviewed runtime, not a live experiment or final corrected head.
- The lifecycle log records 329 tests in 54.737 s and the git ls-tree subprocess failure for the unavailable pinned base. The local reproduction logs substantiate 329 tests in 64.256 s before correction and 72.837 s after correction. These logs are evidence read, not tests rerun by this reviewer.
- Hosted summary comment 6713205151 identifies completed code and security reviews for that head and explicitly reports no code findings. The receipt does not claim that this clears the final head or replaces Claude review.
- The receipt preserves the earlier 17 verification-failure groups as a historical list and adds the two named new groups for the cumulative 19 against the approved cap of 20. No new review root was found here.

Cap-covered choices checked explicitly against approval 6711868908 and the retained sources/tests:
- C1: the unknown-arm test supplies valid hand-offs for both real arms and asserts only the unknown binding-invalid latch.
- C2: absent hand-off produces sample_missing; invalid binding uses the first-failure binding_invalid latch; counter overflow is a separately tested no-report case.
- C3: SelectPreAcceptance returns no selection after overflow, preventing fallback to an earlier numbered report; the retained invocation test asserts no hand-off.
- report_length: the supervisor rejects values outside 1..1048576 as report_binding_invalid; retained negative tests include zero, negative and oversized lengths.
- RawJson: the producer writes Encoding.UTF8.GetBytes of the selected raw report, and the retained test compares the actual bytes, length and SHA-256.
These choices remain unchanged by this correction and within the recorded approval. This check does not substitute for the required Claude head review.

Findings: none at P0 or P1

Pre-existing (not blocking):
None identified in the affected review context.

Verification performed by this reviewer:
- Both supplied diffs matched the stated Git revisions byte for byte.
- git diff --check over the focused comparison/reviewed revisions passed.
- Independently recomputed all six workflow hashes and verified the complete pinned baseline set and current byte equality.
- Copied the reviewed workflow-test module and required workflow/mirror files into a temporary tree outside any Git repository. git rev-parse returned 128. All six B10AbruptWorkflowTests passed there in 0.082 s.
- In that isolated tree, changing a baseline workflow's bytes caused the preservation assertion to fail; deleting it caused FileNotFoundError. No repository workflow or test was modified by these probes.

Cumulative cold-review budget after this complete report: two comprehensive passes and two focused correction rounds. No focused rounds remain. Earlier unchanged work retains its applicable coverage. If another correction needs cold review, docs/agents/pull-requests.md rule 8 requires saving unpublished work under rule 12 and handing off to Hari; a new session does not reset the budget. Final-head CI and hosted review, Claude head review including the cap-covered choices, Hari's merge and a separately approved post-merge diagnostic trial remain mandatory separate gates. No Azure operation, live evidence access or live experiment was performed.

## Claude review-comment dispositions

All eight comments are addressed within the approved execution scope. No existing workflow, protocol sequence, policy value, verdict rule or scientific text changes.

- A1: replace the replay theory's wall-clock readiness race with virtual time, retaining the actual replay assertions and separate late-readiness boundaries.
- A2: distinguish ambiguous storage outcomes/duplicate publication from local validation and observed read-back mismatch, using only the existing failure names.
- A3: retain the startup identity-mismatch cause if cleanup also fails; report the cleanup observation independently without adding a signal request.
- A4: document that abrupt watchdog exit bypasses managed cleanup and that deployed child cleanup depends on container/PID-namespace lifecycle not verified in the trial. No new containment claim or safety mechanism is added.
- A5: list all existing shared-file hand-offs and their integrity limits, including the control checkpoint/release path and the child-originated timeout artifacts.
- A6: make the non-Linux preflight-errno test assert platform rejection, rather than returning without an assertion.
- B1: add a reference table before the approved wire text, resolving item 2, the item-5 file, item 9 and item 11 to implementation files. The approved verbatim sections remain unchanged.
- B2: explain the successor collection boundary's existing termination-unknown label and safety stop; preserve the closed failure-name list and safety handling.

Resumed verification on the working tree based on `7217ce4d955e3a77a26ec31278a82f61d5d31841`:

- Pinned .NET SDK 10.0.402 restored from official release metadata with SHA-512 verification. The repository's pinned offline tool installer and `npm ci --ignore-scripts` succeeded. Managed Docker was available; no Azure identity was used.
- Release LoadGen.Tests build passed with zero warnings/errors in 11.62 s. All 301 abrupt-path tests passed with zero errors, failures, skips or not-run cases in 11.644 s. The publication cases cover existing object, transport/cancellation, missing read-back, unknown invalid data, known size rejection, byte mismatch, duplicate publication and successful read-back. Startup cases retain the original cause alongside target/native cleanup failures; the replay assertion uses virtual time.
- The fresh worker passed eight tests; its independent Python checker passed ten tests in 0.255 s and printed `b10_abrupt_produced_bytes_checked`. These bytes preceded the final narrow read-back exception filter; the whole host acceptance below regenerates them against the final correction. A first checker launch used an incorrect local interpreter path; the explicit pinned interpreter succeeded. This is the previously recorded tool-path failure class, not a product/test failure.
- Documentation tests passed all 28 cases with zero failures/skips. Documentation checks reported the same eight unchanged-file warnings and no changed-file warning.
- Scripted checks found nine correction paths, all within the already approved file map. Existing workflow/mirror, scientific and mutation-project bytes are unchanged. The approved wire/policy/failure sections are byte-identical to the prior reviewed head. ASCII punctuation and `git diff --check` passed.

Source checks for the new explanations and classification tests: [.NET 10 Environment.Exit](https://learn.microsoft.com/en-us/dotnet/api/system.environment.exit?view=net-10.0), [Linux pid_namespaces(7)](https://man7.org/linux/man-pages/man7/pid_namespaces.7.html), [Azure.Storage.Blobs 12.30.1 UploadAsync](https://github.com/Azure/azure-sdk-for-net/blob/Azure.Storage.Blobs_12.30.1/sdk/storage/Azure.Storage.Blobs/src/BlobClient.cs#L855-L898), and [Azure.Core 1.63.0 RequestFailedException](https://github.com/Azure/azure-sdk-for-net/blob/Azure.Core_1.63.0/sdk/core/Azure.Core/src/RequestFailedException.cs#L47-L89). The synthetic duplicate/missing status-code examples use the [Blob service error-code table](https://learn.microsoft.com/en-us/rest/api/storageservices/blob-service-error-codes). The SDK source confirms create-only upload throws for an existing object and uses `IfNoneMatch`; the correction does not claim that an arbitrary transport exception proves the remote write outcome.

- `./scripts/verify-dotnet.sh` passed on the corrected tree with locked restore, the Release solution build, architecture checks, checked reports and warning-as-error control. Measured passing counts: Architecture 16, Enrichment 56, Kafka 486, LoadGen 1521, Scenarios 340 and Verifier 7389. The verifier report records zero failures/skips and duration 2m 57s 839ms. Logs and reports are outside the repository at `/tmp/parchwell-dotnet-verification.nP3uC0l8`.
- The first whole-host launch stopped before tests because the restored tool environment omitted `PARCHWELL_TOOLS_DIRECTORY`. Supplying the already installed pinned-tools directory resolves the launcher prerequisite; no repository script or assertion changed. This is the same recorded tool-environment failure class as the earlier missing tool path.

- The corrected whole host command passed: `time -p timeout 900 bash charts/parchwell/tests/check-loadgen-host.sh`, with the pinned tools environment loaded. Bash measured real 84.61 s, user 54.99 s and system 15.91 s. The abrupt host checks accepted the two rendered environments, rejected five invalid inputs and completed the newly generated production-byte bridge. The unchanged 900 s limit was respected.
- No new product verification-failure root was observed in this continuation. The two tool-environment launch errors above belong to the existing tool-path/configuration root; they were corrected without changing repository commands or assertions. Historical cap accounting and the prior handoff remain recorded; this continuation is bounded by the user's correction request, not a reset of that ledger.

## Prior resumed focused-review report

This report covers `0a90574b54ea97c17b7524bb2e917b8d7e44b374`, before the hosted-P1 corrections. Its limited scope and the later hosted findings are distinct.

<details><summary>Complete focused report and cap-choice addendum</summary>

```text
Review mode: focused correction review under the resumed review-comment task.
Reviewed revision: 0a90574b54ea97c17b7524bb2e917b8d7e44b374.
Focused comparison revision: 7217ce4d955e3a77a26ec31278a82f61d5d31841.
Full-PR comparison revision: 74be825cb6ac71c2c70a6612f3fa0910dcdc52ef.
Focused input: /workspace/scratch/406-claude-focused-0a90574b54ea97c17b7524bb2e917b8d7e44b374.diff.
Full context: /workspace/scratch/406-claude-full-0a90574b54ea97c17b7524bb2e917b8d7e44b374.diff.
Authored receipt: items/404/pr2-receipt.md.
Report: complete

Applied .agents/skills/repo-review/SKILL.md to the corrections since the last reviewed revision, the eight Claude findings and affected code, tests and contracts. The full PR supplies context; this is not another comprehensive pass. Both supplied diffs match git diff of the stated revisions byte for byte. Script-computed scope is nine focused paths and 45 full-context paths.

Authority and budget

Read the receipt's execution card, review mode, prior complete reports, dispositions and recorded 2026-11-08 user instruction, "fix the comments", following the budget handoff. This report covers that bounded continuation and does not reset its historical counters or approve a new implementation plan. Read the actual Claude part-A and part-B comments (6713375550 and 6713787031), the prior handoff (6713457687), the current Revision 6 plan (6709851444), PR2-only approval (6711868908) and cap decision (6711519349) through GitHub REST.

The plan's updated_at is 2026-11-07T18:45:47Z, its script-computed character count is 126836, and its SHA-256 is 4a6a093a00b909d8bbea623983c8aebd404773b3a81fe2c7bb116e4e76596376. These match the approval. Authority recognition follows docs/agents/factory.md, Item lifecycle step 3. The review comments are correction inputs, not independent authority to change policy. The failure-name decisions fit the recorded cap; no correction changes a protocol sequence, gate, policy value, scientific text, safety property or existing workflow.

File and rubric coverage

- docs/operations/b10-abrupt-contract.md: general rubric, writing standards, source-backed claims, approved verbatim contract and file-map references.
- items/404/pr2-receipt.md: general rubric, review protocol and cumulative budget, authority and evidence provenance, affected acceptance and failure-name choices.
- items/404/state.md: general rubric and factory.md Item state file, including the next action and retained trial/PR3 prerequisites.
- src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs: general rubric and dotnet.md; startup-failure reporting and its supervisor call chain.
- src/Parchwell.LoadGen/Run/Abrupt/B10AbruptPublisher.cs: general rubric and dotnet.md; create-only publication, read-back classification, bounded storage adapter and callers.
- src/Parchwell.LoadGen/Run/Abrupt/OwnedProducerProcess.cs: general rubric and dotnet.md; startup, cleanup, owned-target validation and actual signal observations.
- tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProcessTests.cs: general rubric and dotnet.md; platform rejection, original-cause preservation, cleanup observations and real-process tests.
- tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProducerTests.cs: general rubric and dotnet.md; replay assertions, virtual clock, report fixture and retained release-boundary tests.
- tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptWireProducerTests.cs: general rubric and dotnet.md; added publication fault cases, valid publication control and existing produced-byte tests.

Applied AGENTS.md, CLAUDE.md, docs/agents/pull-requests.md, docs/agents/factory.md, docs/blueprint.md ADRs and scope, docs/engineering/dotnet.md and the code-comment review exceptions. The governing files and repo-review skill are byte-identical to origin/main. No changed verifier predicate or mutation target is present. Relevant unchanged context includes supervisor startup and abort handling, shared-file writers/readers, successor replay readiness, bounded Blob reads and workflow diagnostic collection.

Specification and correction comparison

1. A1: the replay theory now uses ReleaseBoundaryClock at the release instant. It preserves its assertions for exactly one invocation, original payload, disposal, emitted artifacts, inventory and new attempt identity. The separate readiness theory still checks one tick below, at and above release. This removes the initialization/wall-clock race without weakening replay or late-readiness behavior (plan item 8, E4 and E9).

2. A2: publication now reports an unconfirmed create or read-back outcome as b10_abrupt_publication_ambiguous, including duplicate publication, transport/cancellation and unknown storage InvalidDataException. A known bounded-read size rejection and an actual read-back byte mismatch remain b10_abrupt_publication_failed. Local validation still occurs before storage. The changed code adds no retry or overwrite and records an object in _published only after exact read-back equality. Tests check cause preservation, create/read call counts, before/after-write transport cases, missing objects, cancellation, unknown invalid data, known size rejection, changed bytes and duplicate local publication. This implements the existing failure table and plan item 11 within the cap-covered failure-name choice.

3. A3: startup cleanup now preserves child_start_failed or child_identity_mismatch as the primary outcome and retains the original exception. A separate cleanup exception and actual native signal observation reach ReportFailure. The cleanup action is still performed once; signal, ownership and kill-budget behavior is unchanged. The new tests verify the serialized diagnostic, not just an exception type, for successful cleanup, target rejection, errno and native-load failure (plan items 3-4 and E2).

4. A4: the receipt explicitly distinguishes Environment.Exit from managed abort/disposal, states the unverified container/PID-namespace dependency and disclaims standalone-host containment. It does not claim that watchdog expiry or an exit code proves death, reaping or EOF. The cited .NET 10 Environment.Exit documentation and Linux pid_namespaces(7), The namespace init process, support the stated API/namespace behavior; deployment confirmation remains unavailable.

5. A5: the shared-file table agrees with the actual host, producer and supervisor paths. It covers raw pre-acceptance reports, timeout/control seals and cohort artifacts, probe payloads, checkpoint/release input and the resumed acknowledgement. It distinguishes the child-originated timeout self-consistency check from the control ledger's journal-terminal comparison. No new transport or trust claim is introduced.

6. A6: the preflight-errno test now asserts platform rejection on unsupported OS/architecture instead of returning without an assertion. The supported Linux/x64 path retains its exact preflight failure and own-PID null-signal call assertion. Production platform admission is unchanged.

7. B1: the additive reference table resolves plan items 2, 5, 9 and 11 to the actual options, report path, statistics selector and publisher. A script confirmed that the existing contract from "## Wire and state machine" through its end is byte-for-byte unchanged from the previously reviewed revision. Approved wire, policy and failure text remains intact.

8. B2: the new explanation matches Trial.collect's successor_termination boundary and observe_first_termination helper. It tells readers that the existing first_termination_unknown name can identify a successor observation gap at that boundary, and retains safety_stop. The closed failure list and Python behavior are unchanged.

No missing, partial or incorrect requirement, scope creep, assertion weakening or unsafe focus was identified in this correction scope. No existing workflow, source policy, scenario expectation, result, verdict, identity/network permission, project, package or reference is changed. PR3 and scientific publication remain excluded. Earlier unchanged work retains its existing applicable review coverage.

Source and evidence checks

- Read the pinned Azure.Storage.Blobs 12.30.1 BlobClient.UploadAsync source, which documents throwing for an existing object and implements overwrite:false with IfNoneMatch; read Azure.Core 1.63.0 RequestFailedException constructor semantics and the official Blob error table's BlobAlreadyExists/409 and BlobNotFound/404 entries. These support the changed synthetic cases without treating a transport exception as proof that no write occurred.
- Checked the receipt's new build/test measurements against the local source logs, including the 11.62 s build, 301 abrupt cases at 11.644 s, ten produced-byte checker cases, and the documentation results. The full-verification XML reports substantiate passing counts of Architecture 16, Enrichment 56, Kafka 486, LoadGen 1521, Scenarios 340 and Verifier 7389, with zero failures/errors/skips/not-run. The verifier test log records the cited overall duration of 2m 57s 839ms.
- The whole-host log substantiates real 84.61 s, user 54.99 s and sys 15.91 s, including the two valid rendered environments, five rejected inputs and production-byte bridge. The full verification log records locked restore/build, checked reports and the warning-as-error control. These are source evidence inspected, not full suites rerun by this reviewer.
- The documentation log records eight advisory warnings; the correction introduces no new warning disposition requirement. Historical results are distinguished from pending final-head CI and reviews.

Findings: none at P0 or P1

Pre-existing (not blocking):
The explicitly documented watchdog/standalone-host child-lifetime limitation remains. Deployed PID-namespace behavior and the diagnostic trial are not verified by these local corrections or this review; the receipt now accurately states that limit.

Verification performed by this reviewer

- Compared both supplied diffs with their identified Git revisions, counted scoped paths, checked governing-file equality with origin/main, checked retained contract-section equality, and ran git diff --check over the focused revisions successfully.
- Verified SHA-256 source checksums for all six changed C# runtime/test files occur in the Release portable PDBs used by the tests.
- Ran /workspace/scratch/dotnet/dotnet tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptPublisherTests -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptProcessTests -class Parchwell.LoadGen.Tests.Abrupt.B10AbruptProducerTests. Result: 111 tests, zero errors/failures/skips/not-run, measured 2.291 s. Log: /workspace/scratch/406-cold-correction-tests.log. This includes the actual Linux process stop/resume/kill/exit/EOF test. Unsupported-platform behavior was read and checked against production admission, not executed on another OS.

Cumulative cold reviews after this report: two comprehensive passes and three focused rounds, the third being this explicitly resumed bounded correction review. No further review or correction allowance is inferred. Additional work outside the recorded correction set requires the stated handoff. Final-head CI, hosted Codex code/security review, required Claude head confirmation of the cap-covered choices, Hari's merge and separate post-merge environment/trial approval remain distinct gates. This report is not a Claude-vendor review or merge approval. No Azure operation, workflow dispatch, live evidence access or live experiment was performed.

Completion addendum: explicit original cap-covered choices on the same frozen head

This addendum completes the existing focused report for 0a90574b54ea97c17b7524bb2e917b8d7e44b374. It is not a new review round. The five choices required by approval 6711868908 were checked explicitly against their receipt descriptions, the approval, current source and retained tests. No finding was identified; the result line above is unchanged.

- C1, unknown arm: B10AbruptSupervisorTests.cs:127-139 prepares both recognized arms, supplies a valid report for each, then supplies the unknown-arm report. It accepts both real arms and requires exactly one failure, keyed "unknown", with b10_abrupt_report_binding_invalid. B10AbruptSupervisor.AcceptReportAsync at lines 225-248 stores that failure separately; acceptance at lines 192-196 does not invent a missing sample for the two recognized arms. This is the approval's explicit option to supply valid reports for both real arms.
- C2, missing message versus invalid binding: acceptance uses TryAdd to latch b10_abrupt_sample_missing only when the original has no valid report. AcceptReportAsync retains the first per-arm failure. The retained missing/late-report and duplicate-report tests at B10AbruptSupervisorTests.cs:25-46 require exactly one applicable failure name; the invalid-binding theory at lines 49-111 likewise preserves binding_invalid and prevents a signal/checkpoint. Counter overflow is a separate no-handoff case in B10AbruptProducerTests.cs:110-120. The receipt correctly scopes the hand-off sentence without claiming overflow emits an invalid message.
- C3, selection after overflow: B10AbruptStatistics.Observe at lines 146-153 uses checked increment and permanently latches overflow. SelectPreAcceptance at lines 216-225 returns null after overflow, so an earlier numbered report cannot be selected as fallback. B10AbruptProducer.InvokeProbe at lines 286-300 emits no pre_acceptance_report for a null selection but retains accepted. The overflow test starts at Int64.MaxValue - 1, observes an earlier numbered report and then the overflowing report, and requires no hand-off. This matches the recorded choice and the specified missing-sample consequence; independent guard failures retain their own precedence.
- Out-of-range report_length: B10AbruptSupervisor.cs:234-235 rejects lengths below 1 or above 1048576 as b10_abrupt_report_binding_invalid. The contract's wire raise list at line 37 and failure table at line 124 both explicitly include that out-of-range case. The retained theory tests zero, negative and 1048577; the separate byte-boundary theory tests 1048575, 1048576 and 1048577 while keeping descriptors otherwise valid. This matches the carried raise-list choice.
- UTF-8 RawJson item-5 file: B10AbruptProducer.cs:289-295 writes Encoding.UTF8.GetBytes(selected.Report.RawJson) directly to abrupt-child/<arm>-pre-acceptance-report.json and binds that same byte array's length and SHA-256 in the hand-off. No preamble is prepended. B10AbruptProducerTests.cs:88-106 reads the actual file, compares it to Encoding.UTF8.GetBytes(report.RawJson), checks exact length and hash, and asserts prepared -> pre_acceptance_report -> accepted ordering. This matches the receipt's carried encoding choice.

A script confirmed B10AbruptStatistics.cs, B10AbruptSupervisor.cs, B10AbruptProducer.cs and B10AbruptSupervisorTests.cs are byte-identical between the prior reviewed head and this frozen head. The only B10AbruptProducerTests.cs correction is the unrelated replay-clock change already reviewed above. The existing full-verification XML for the corrected tree records Pass for every case of the seven test methods cited here: the unknown-arm, missing/late-report, duplicate-report, invalid-binding, byte-boundary, overflow and raw-file tests. The producer class was also included in this reviewer's 111-test run. No repeat execution, repository edit, new budget consumption or expanded review scope was needed for this addendum. Required Claude-vendor confirmation remains separate.
```

</details>

## Hosted-P1 correction dispositions

- Disposal capture: `RunSuccessorAsync` now completes its existing bounded producer cleanup before invoking the existing statistics/evidence completion delegate. It checks guards after completion and before copying artifacts. The real-evidence-session regression injects a callback from Dispose: a non-error observation is retained in the copied evidence, while an error observation is retained at the capture source and blocks successful artifact completion. Original cleanup budgets, error guards and the first-child no-disposal rule remain unchanged. An audit found no other abrupt path that finalized evidence before producer disposal and then returned successful artifacts; the ordinary executor already disposes before evidence completion.
- Stopped checkpoint: the pump and stopped-boundary drain share a serialized incremental reader. The stopped drain waits for any in-flight read, parse and enqueue, then processes available transport bytes before consuming the channel and sealing the checkpoint. Partial headers/bodies remain buffered during ordinary pumping; a partial frame at the stopped checkpoint fails the existing malformed-frame gate, as does partial EOF. The strict parser, size bounds, sequence checks, queue-full failure and sticky capture gaps remain. Regressions cover transport-buffered terminals and delayed enqueue for both arms, with terminal-before-checkpoint ordering and no selected original or subsequent CONT/KILL. Existing real Linux E2 now uses the production incremental reader/readiness check and retains its heartbeat, pause, resume, kill, exit and EOF assertions.

The read-only native readiness call uses zero-timeout `poll` on the existing owned events descriptor under the sole-reader lock. It adds no wire kind, signal, protocol step or policy gate. The pump's cancellable one millisecond idle delay schedules work only; the stopped drain does not wait it, and the existing arm deadline remains authoritative. [Linux poll(2)](https://man7.org/linux/man-pages/man2/poll.2.html) states that readiness means the requested operation will not block, timeout zero returns immediately, and a hangup reaches read EOF only after buffered data is consumed. [Linux read(2)](https://man7.org/linux/man-pages/man2/read.2.html) permits short reads and defines zero as EOF. The lock excludes a competing reader between readiness and read; actual Linux interoperability is tested, not inferred from those documents alone.

Native layout/constants were checked against [Linux v6.11 poll.h](https://raw.githubusercontent.com/torvalds/linux/v6.11/include/uapi/asm-generic/poll.h), its [generic Kbuild](https://raw.githubusercontent.com/torvalds/linux/v6.11/include/uapi/asm-generic/Kbuild) and [x86 Kbuild](https://raw.githubusercontent.com/torvalds/linux/v6.11/arch/x86/include/uapi/asm/Kbuild), and [glibc 2.39 poll.h](https://raw.githubusercontent.com/bminor/glibc/glibc-2.39/io/sys/poll.h). These define the int/short/short descriptor, unsigned-long descriptor count and the readiness/error flags used under the existing Linux/x64 restriction. The installed [.NET runtime v10.0.13 PipeStream source](https://raw.githubusercontent.com/dotnet/runtime/v10.0.13/src/libraries/System.IO.Pipes/src/System/IO/Pipes/PipeStream.Unix.cs) routes synchronous Read to a single receive rather than ReadExactly. Poll/import/read errors fail capture; they never establish an empty successful drain. This is an implementation correction to the already approved drain-before-checkpoint requirement, not a new exposure or scientific claim.

Hosted-P1 verification on the working tree based on `0a90574b54ea97c17b7524bb2e917b8d7e44b374`:

- Locked Release LoadGen.Tests build passed with zero warnings/errors in 21.18 s. All 318 abrupt tests passed with zero errors, failures, skips or not-run cases in 11.868 s, including the actual Linux process proof.
- Documentation tests passed all 28 cases with zero failures/skips. The scan retained eight advisory warnings in unchanged files only.

The next cold review is focused on this hosted-P1 correction against `0a90574b54ea97c17b7524bb2e917b8d7e44b374`, with the full PR diff against `74be825cb6ac71c2c70a6612f3fa0910dcdc52ef` as context. The reviewed input is the frozen commit containing this receipt. Prior unchanged work retains coverage. The reviewer must explicitly check C1, C2, C3, the report_length raise list and UTF-8 RawJson choice, and the affected cleanup, capture and stopped-drain contracts. Before this review, cumulative counts remain two comprehensive and three focused; completion consumes the additional focused round authorized by the user's reply above. The complete report and final-head CI belong in the PR description, not a receipt-only follow-up commit.

Preservation checks: the correction touches 11 paths, all within the existing 45-path approved implementation map. Changed C# source hashes match the built Release PDBs. The contract, workflow/mirror and mutation-project bytes match the preceding head; no scenario, result, verdict, existing workflow or scientific text was edited. `git diff --check` passed.

The first full verification attempt passed build, Architecture (16), Enrichment (56), Kafka (486), LoadGen (1538) and Scenarios (340). Verifier ran 7389 cases: 7388 passed and one failed before its assertions when Docker/RootlessKit could not bind local port 32856 because the address was already in use. The failing case was `VerifierWindowComponentTests.Given_ADR016MustFailEvidence_When_RealHandlerDrains_Then_NoAuthoritativeTotals`, dropped-ledger input. That test and `LocalKafkaFixture` match the prior head. No test or infrastructure setting was changed; the failed run is retained and full verification is rerun after completing the partial-frame checkpoint correction. These counts describe that initial attempt, not the final correction tree.

Final-tree `./scripts/verify-dotnet.sh` passed locked restore, Release solution build, all checked test reports and the warning-as-error control. Measured passing counts: Parchwell.ArchitectureTests: 16; Parchwell.Enrichment.Tests: 56; Parchwell.Kafka.Tests: 486; Parchwell.LoadGen.Tests: 1546; Parchwell.Scenarios.Tests: 340; Parchwell.Verifier.Tests: 7389. All reports have zero failures, errors, skips and not-run cases. The Docker port collision did not recur. Logs/reports remain outside the repository at `/workspace/scratch/406-p1-verify-final.log` and `/tmp/parchwell-dotnet-verification.B0QohgFc`.

Final-tree whole-host acceptance passed under `timeout 900`: real 85.97 s, user 56.41 s, sys 16.43 s. The preceding-head measurement was real 84.61 s. The final run includes two accepted abrupt rendered environments, five rejected inputs and the mandatory fresh production-byte bridge. No host bound or workflow was changed.

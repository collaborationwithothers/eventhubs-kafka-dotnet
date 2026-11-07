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

## Verification and review status

Local implementation verification is complete. Cold review and Linux CI remain pending; neither is claimed as passed. No live trial has run in this session.

- `./scripts/verify-dotnet.sh` passed with locked restore, Release build (zero warnings/errors), architecture checks, all project tests, checked reports and the warning-as-error control. Measured report counts: Architecture 16, Enrichment 56, Kafka 486, LoadGen 1429, Scenarios 340 and Verifier 7389. Logs remained outside the repository.
- Baseline, before chart changes, source `74be825cb6ac71c2c70a6612f3fa0910dcdc52ef`: `bash charts/parchwell/tests/check-loadgen-host.sh` passed. Bash `time -p` measured real 121.04 s, user 90.88 s, system 21.96 s. Post-change: the same whole script passed, including E10/E11, with Bash `time -p` measuring real 114.19 s, user 89.66 s and system 29.36 s, within the unchanged 900 s bound. A local `timeout 900` enforced that bound.
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

The list below contains 14 distinct measured root causes across all implementing agents, counted by script; repeated manifestations appear only once. The plan cap is 20; no warning is waived.

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

All listed causes are resolved. The full .NET and timed host acceptance commands passed after these corrections.

## Cold-review input

Mode: initial comprehensive review. Input revision: the frozen HEAD on `codex/404-pr2-abrupt-restart`; the review diff filename identifies its exact commit. Comparison revision: `74be825cb6ac71c2c70a6612f3fa0910dcdc52ef`. The supplied diff contains every authored file, including this receipt and item state. At this freeze there are no completed cold-review reports or focused rounds. The pull request self-review record will hold the complete report, dispositions and cumulative counts; a report is not added to its own reviewed receipt.

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

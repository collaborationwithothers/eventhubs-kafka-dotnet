# B10 warm leader implementation receipt

This change implements the approved warm leader plan for issue #404, including per-arm warm-up, leader readiness, bounded child lifetime and offline proof. The continuation restored the saved implementation and passed the mandatory full .NET gate without source changes. Publication still requires the qualifying cold review and final-head hosted gates; Hari merges the protected blueprint amendment.

## Execution card

- Deliverable and scope: Revision 3, sections 1 to 6 of [the approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/34aae8485053429a5b183e86a5f2c39dda570d25/items/404/plan-b10-warm-leader.md). Baseline `e74849ea716b6c4b8fa6c5143c6c4175c7038eea`.
- Authorization: [plan pointer](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6798220099), [Hari's approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6801162806), and its recognized decisions. Plan commit and exact-body SHA-256 `4f76d94238a1fc5efdcc27ea69f808d81eb553758eae58bcdea48ac9bdf66b64` verified. The approval retains [the Revision 2 check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6801044256); Revision 3 incorporates its P2-A to P2-D and [the accepted margin and test bounds](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6801095762).
- Mandatory gates and exact verification commands: plan section 8: `./scripts/verify-dotnet.sh`; `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'`; `bash charts/parchwell/tests/check-loadgen-host.sh`; `npm run check:docs`; two Docker image-test runs and one run each of M1, M2 and M3 using the existing workflow's build/run commands by hand. No agent dispatch.
- Earliest publication: after required local verification and clean applicable cold-review coverage. Required CI and hosted Codex code/security review on the final head follow publication. Hari merges the protected blueprint change.
- Runtime fix budgets: plan section 8 permits 20 verification-failure roots and 20 review-finding roots per implementing session; either exhaustion pushes `-wip` and hands off to the next session.
- Cold-review budget: docs/agents/pull-requests.md rule 8: at most two comprehensive and two focused passes cumulatively; exhaustion or unsafe focus pushes `-wip` and hands off to Hari.
- Predicate map: plan section 6 covers the changed journal and phase predicates. No verifier predicate changes.
- Models: Codex control session; fresh-context high-effort Codex subagents for implementation and cold review. Exact model identity is not exposed by the harness.

## Verification and implementation choices

The effective probe settings are composed inside the abrupt host, preserving the ordinary resolver and the C1 timeout Job. The child settings guard uses its effective values; the successor selects the same baseline-valued, unoverridden timeout that it publishes. Warm-up callbacks use the existing bounded journal queue; durable journal frames, rather than callback arrival alone, release the producing thread.

The deadline is anchored to the actual `child_started` frame's monotonic timestamp. Every child wait uses the remaining deadline, and the final no-kill flush uses the full remaining time, clamped to zero, rather than a renewed drain budget. The leader predicate accepts JSON integer leader/node IDs without adding a width contract; partition and offset use their specified signed widths.

For M3, the mutation bypasses the cross-restart warm-up and binds the earlier report, preserving the valid control arm so the existing journal-publication assertions observe the missing-warm-up gap. This selects the gate under test without introducing another mutation.

Policy timing was recomputed by script from waits `[20, 90, 20, 120, 300, 20, 90, 20]`: cumulative seconds `[20, 110, 130, 250, 550, 570, 660, 680]`; the deadline plus cleanup is 700 s and the parent margin is 30 s. These are policy bounds, not runtime measurements.

Verification below identifies the committed source tested. Raw reports and generated artifacts stay outside the repository.

Corrective verification is scoped to affected behavior. The journal identity correction affects `B10AbruptJournalTests`, covered by its 101-test Release run and clean build. The synthetic `RecordingProducer` scheduling correction affects its real-process lifecycle cases; the final mandatory .NET command covers those callers. The sleeping child exits before constructing a producer, and the producer output-collision case also stops before construction. The real Kafka and offline-image branches bypass that recorder, and the Python and chart inputs are unchanged, so their completed evidence is retained. Receipt and state changes require a documentation scan and applicable cold review. The mandatory macro has no resume interface; separately passed segments cannot be represented as its required exit 0.

## Local verification evidence

The LoadGen image and offline C# test inputs are from `a0ced1128135be53757cd34b64f4825e82c6b735`. Fixture-only commit `687b9070dd8a13b7a4b50e8f61f0c23b609603a3` changes the Python relationship mutants and the unit pacing-clock setup. Commit `5dfdab8ab94b3a52342650a3823dac5769d92746` limits synthetic process-clock acceleration to release-sized timers and preserves no-kill fixture artifacts. Production, offline-image, proxy, real-broker end-to-end and broker-fixture behavior is unchanged from the measured image revision; the process-clock edit is confined to synthetic process tests and the real-time path remains unchanged; this was checked with `git diff`. Commit `472e5c3919764e0f525e5c20d2a29149d500aae3` corrects new journal vectors to the approved run identity. Commit `ff399b7c2e8f0896bd76fe74748dcb753256fc7e` supplies loss-free synthetic statistics for process lifecycle tests. The final full .NET gate runs on `ff399b7c2e8f0896bd76fe74748dcb753256fc7e`. All image observations describe synthetic traffic against a local Kafka broker in Docker. Live assumptions LA1 to LA6 remain scoped to Hari's accepted next-trial risks in plan section 7; no trial was run.

- Full .NET gate on `5dfdab8ab94b3a52342650a3823dac5769d92746`: failed in LoadGen, with 1807 passed, one failed and no skips. The sole failure was the synthetic host-cross process fixture's statistics guard (V13). Locked restore, the zero-warning solution build, inventory/settings checks, architecture, Enrichment and Kafka tests passed before it. The initial remaining Scenarios and Verifier project gates were run independently with existing Release artifacts: 340 and 7389 passed respectively, no failures/skips, and both reports validated. These results do not claim that the macro exited 0. Publication remains blocked on its required successful invocation. The exact command is `./scripts/verify-dotnet.sh`; the shell function below scopes the environment workaround to build/evaluation commands while `dotnet test` and its descendants use the default runtime. Log: `/tmp/404-verify-dotnet-final-scoped.log`.

```sh
dotnet() {
  if [[ "${1-}" == test ]]; then
    env -u DOTNET_PROCESSOR_COUNT /home/agent/.parchwell/dotnet/dotnet "$@"
  else
    DOTNET_PROCESSOR_COUNT=1 /home/agent/.parchwell/dotnet/dotnet "$@"
  fi
}
export -f dotnet
```

The invocation also supplies the installed `DOTNET_ROOT`, Helm path and retained `MSBUILDDEBUGPATH`; warnings, assertions, selected projects and report validation remain unchanged. The earlier whole-run processor override reached real-clock children and is no longer used for tests.

- Full .NET attempt on `ff399b7c2e8f0896bd76fe74748dcb753256fc7e`: exit 2, with 10100 passed, one failed and no skips across the selected projects. Architecture 17, Enrichment 56, Kafka 491, LoadGen 1808 and Scenarios 340 passed and validated. Verifier passed 7388 and failed one Docker fixture startup/cleanup case (V14), so its report validation and the macro warning control were not reached. This does not satisfy plan section 8. The protected `B10AbruptRejectionTests.cs` remains unchanged.

- Python: `PATH=/tmp/404-tools/bin:$PATH python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'` passed 299 tests in 42.539 s after the final fixture correction.
- Production-byte bridge: the fixed `B10AbruptWireProducerTests` class exported its unchanged file set in 4.944 s with 49 passed and no skips; `check_b10_abrupt_producers.py` then passed 17 tests, including the two unchanged exact rejection assertions. The protected verifier rejection test is unchanged; its final full-suite result remains part of the publication gate.
- Chart gate: passed again on `687b9070dd8a13b7a4b50e8f61f0c23b609603a3` after both fixture corrections, with `PARCHWELL_TOOLS_DIRECTORY=/tmp/404-tools`, `DOTNET_ROOT=/home/agent/.parchwell/dotnet` and `DOTNET_PROCESSOR_COUNT=1`. Helm 4.3.1 and PyYAML 6.0.3 use the repository pins.
- Documentation: `npm run check:docs` passed; nine warnings are in unchanged files. The scan on this frozen authored receipt precedes the cold review; later authored corrections require applicable coverage.
- Image: the workflow's publish, load, test-build, filtered test and report-validation steps were run by hand. Both normal runs passed exactly one test, no skips; `ValidateReport` passed. The consumer window is 60 s and both proxy delays are 500 ms test policy. The initial untested image builds were superseded before any image test.

The two required observation lines are recorded verbatim below. The registration-to-spawn interval covers the parent's stage marks only; it does not prove the complete AKS exit margin (LA6).


Run 1:

```text
b10_offline_image source_commit=a0ced1128135be53757cd34b64f4825e82c6b735 image_id=sha256:1698d3e8ea846ec2ccd9ab70941a44fdd6229ea6c84c4e8cb67a3da38a4fdd3e runtime=10.0.13 kernel=6.18.57 parent_exit=0 exit_code_local_observation=137 termination=intended_self_kill journal_records=51 kill_unconfirmed=5 cut_accepted_to_kill_intent_microseconds=4356.486 kill_intent_to_exit_observed_microseconds=17640.855 successor_exit=0 consumed_resends=5 warmup_records=2 kill_originals_consumed=5 parent_registered_to_child_started_microseconds=23188.72 connect_delay_ms=500 response_delay_ms=500
```

Run 2:

```text
b10_offline_image source_commit=a0ced1128135be53757cd34b64f4825e82c6b735 image_id=sha256:1698d3e8ea846ec2ccd9ab70941a44fdd6229ea6c84c4e8cb67a3da38a4fdd3e runtime=10.0.13 kernel=6.18.57 parent_exit=0 exit_code_local_observation=137 termination=intended_self_kill journal_records=51 kill_unconfirmed=5 cut_accepted_to_kill_intent_microseconds=3742.489 kill_intent_to_exit_observed_microseconds=28073.661 successor_exit=0 consumed_resends=5 warmup_records=2 kill_originals_consumed=5 parent_registered_to_child_started_microseconds=46349.622 connect_delay_ms=500 response_delay_ms=500
```

## Failure-root ledger

The approved limits are 20 distinct verification roots and 20 distinct review roots per implementing session. Counts remain below those limits; review-budget accounting is separate.

| Root | Observation and correction |
| --- | --- |
| V1 | CA1861 in a new offline assertion; use a static expected array. |
| V2 | Parallel MSBuild worker exits (MSB4166), including a repeated image-test build failure; serialized builds and the processor-count override pass. No source change for this environment failure. |
| V3 | Required Helm/Python tools were not prepared; install only the existing repository pins outside the repository. |
| V4 | Multiple class filters selected zero tests; run separate filtered invocations. No assertion or source change. |
| V5 | A new advancing-clock fixture waited for its own reporting task; use a pause flag instead of a synchronous self-wait. |
| V6 | Deadline at release-wait entry was labelled control-incomplete; check the deadline before the lateness guard. |
| V7 | An existing disposal assertion observed zero disposals while the artificial clock continued through no-kill cleanup; stop the reporting loop's 10 ms artificial clock steps at no-kill, retain the disposal assertion, and recheck the case successfully. |
| V8 | The chart apphost could not locate the user-installed SDK; supply its actual `DOTNET_ROOT`. |
| V9 | The new warm-up control-completeness gate masked two older mutation targets. Retain completed control evidence for the orphan-terminal case, and remove the cross-restart acceptance for the missing-acceptance case. Recompute the derived kill set and hashes. Both exact assertions are retained and pass on actual producer bytes. Production code is unchanged by this correction. |
| V10 | The initial full .NET run found two custom-clock pacing cases with no later warm-up report and another stalled fixture. Its interrupted report records 1433 passed and 2 failed, with incomplete coverage. Supply a clock-advance hook only to the affected fake warm-up producer, await durable terminal capture, then publish a later report. Isolated pacing tests measure from warm-up completion and offset journal hooks from that setup; all assertions, 52 ms/50 ms/250 us literals and the original clock behavior remain. Policy 11, warm leader 25 and producer 61 cases pass in Debug, with no skips; the complete Release gate is rerun. |

| V11 | A full run with the processor override inherited by tests ended with 1800 passed and 8 failed. The E2E report arrived 22.602936 s after warm-up, beyond its 20 s policy wait. One focused run with default runtime passed (1 test, no skips, 433.238 s). Isolation and processor configuration both changed, so the processor setting is not proved to be the sole cause. Scope the build workaround instead of changing any runtime bound. |
| V12 | Synthetic `ProcessClock.CreateTimer` advanced every timer at least 100 s, including the new 690 s deadline. Only one-shot release-sized waits from 100 to 120 s now advance virtual time. Debug build passes; the real-process class passed five cases and exposed the separate V13 guard observation. No production or assertion change. |
| V13 | Synthetic real-process statistics capture returned guard-failed in the `incomplete` and host-cross paths; the earlier incomplete case did not retain its exact guard facts. The host-cross failure recurred in the complete default-runtime suite and focused diagnostics. A retained first-throw trace identified guard reason 6, the native-fingerprint/report mismatch; one diagnostic then captured session diagnostics containing only `capture_contention` (one failed test, no skips, 123.938 s). The fake 20 ms timer raced the real session worker. Commit `ff399b7c2e8f0896bd76fe74748dcb753256fc7e` serializes synthetic emissions with that session gate, deliberately supplying loss-free statistics to lifecycle tests. It uses test-only reflection and fails if the private seam changes; native/image branches bypass this recorder. Temporary diagnostic code was removed. Production guards, callback flow, assertions and timing literals remain unchanged. The Debug build passed with zero warnings/errors, the unchanged host-cross fact passed once (one test, no skips, 123.897 s), and its report validated. Evidence: `/tmp/404-hostcross-serialized-results/results.xml`. The subsequent mandatory full run passed and validated all 1808 LoadGen tests, then exposed the separate V14 Docker environment failure in Verifier. |

| V14 | The mandatory full run at `ff399b7c2e8f0896bd76fe74748dcb753256fc7e` exited 2: 10100 passed, one failed, no skips. The sole failure was `VerifierWindowComponentTests.Given_CorruptRecordAppendedAfterFixedEnd_When_RealHandlerDrains_Then_LaterRecordIsExcluded` during Docker fixture startup/cleanup, before its verdict assertions. Testcontainers had no ID for its requested `verifier-proof-kafka-61416253391b42f59edd130a5901b00a`; that exact container later existed in Created state. The underlying create-response failure is unconfirmed. The daemon Created timestamp was 0.427 s after test start, but does not establish when the create request returned. Exact owned-container inspect was retained and that stopped container was removed; no daemon restart or configuration change was made. All preceding projects, including LoadGen, passed and validated. Verifier validation and warning control were not reached. Verifier code is unchanged and frozen. After owned-container cleanup, one unchanged focused rerun passed and validated (one test, no skips; 222.36 s versus 35.72 s in the earlier independent pass). This is not a proved repair and does not replace the macro gate. One further mandatory macro invocation retained its command, assertions, selection, deadlines and runtime processor policy, but ended at the separate V15 finding. No reliable environmental repair is claimed. Evidence: `/tmp/404-dotnet-acceptance-evidence.json`. |

| V15 | The final authorized macro attempt, still on `ff399b7c2e8f0896bd76fe74748dcb753256fc7e`, exited 2 in Kafka: architecture 17 and Enrichment 56 passed and validated; Kafka passed 490 and failed one, with no skips. `PhantomSpikeTests.Given_ApprovedFaultMatrix_When_RealClientAttemptsAreObserved_Then_ExperimentalIntegrityIsEstablished` failed at line 32: experiment integrity was inconclusive because the healthy control matrix was incomplete and the fault matrix had not started. Direct observation reported `Local_TimedOutQueue`; diagnostics were canceled. Cause remains unconfirmed. Spike evidence reports `CleanupSucceeded=true`. Later projects and warning control were not reached. No assertion, expected outcome, Kafka/verifier source or fixture was changed to obtain a pass. Evidence: `/tmp/404-dotnet-retry-evidence.json`. Subsequent focused verification ran the unchanged spike fact: one passed test, no skips; all 40 trials actually executed and sealed with complete scans (10 healthy controls and 30 fault trials), no experiment failures and successful cleanup. Test, spike publication and report validation exited 0. Evidence: `/tmp/404-kafka-v15-focused-evidence.json`. No specific environmental repair or necessary frozen-source correction is proven. |


One pre-cold author check found new runtime tests using legacy fixture identity/topic values. The new tests now use the plan's common inputs and explicit warm-up/provenance literals; existing fixture defaults and assertions remain unchanged. Conservatively this consumes one review root, before any cold-review finding.

## Image command record

The build and run steps follow [the unchanged workflow at the measured source](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/a0ced1128135be53757cd34b64f4825e82c6b735/.github/workflows/b10-offline-image.yml). The publish command used locked linux-x64 restore, `--self-contained false`, `/t:PublishContainer`, `ContainerImageTag=offline-a0ced1128135be53757cd34b64f4825e82c6b735` and `ContainerArchiveOutputPath=/tmp/404-offline-a0ced112-image.tar`; Docker loaded that archive. The Release test build used `-p:RestoreLockedMode=true`. Both builds used `DOTNET_PROCESSOR_COUNT=1`.

For each label `image-pass1` and `image-pass2`, the filtered command was:

```sh
PARCHWELL_B10_OFFLINE_IMAGE=parchwell/loadgen:offline-a0ced1128135be53757cd34b64f4825e82c6b735 \
PARCHWELL_B10_OFFLINE_SOURCE_COMMIT=a0ced1128135be53757cd34b64f4825e82c6b735 \
PARCHWELL_B10_OFFLINE_EVIDENCE=/tmp/404-${label}-evidence.txt \
dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj \
  --configuration Release --no-build --minimum-expected-tests 1 \
  --results-directory /tmp/404-${label}-results -- \
  --filter-method 'Parchwell.LoadGen.Tests.Abrupt.B10AbruptOfflineImageTests.Given_BuiltImage_When_ParentChildAndSuccessorRunInContainers_Then_JournalExitAndResendsAreObserved' \
  --report-xunit-xml --report-xunit-xml-filename results.xml
```

`DOCKER_HOST` selected `unix:///var/run/docker.sock`; `DOCKER_CONTEXT`, `DOCKER_TLS`, `DOCKER_TLS_VERIFY` and `DOCKER_CERT_PATH` were unset. After each normal run, `dotnet msbuild scripts/dotnet-verification.proj -t:ValidateReport -p:Report=/tmp/404-${label}-results/results.xml -p:ExpectedAssembly=Parchwell.LoadGen.Tests -nologo` passed. The workflow's XML check confirmed exactly one passed test.

## Fixed image mutations

Each mutation was applied once in its own scratch worktree from the measured image revision. Each image was rebuilt and the unchanged offline test ran once. All three failed at the specified assertion, and their diffs, XML, logs and image IDs were retained outside the repository.

| Mutation | Change and observed rejection |
| --- | --- |
| M1 | At the start of `ProbeSettings`, M1 inserts `if (!child) return role;`, restoring the successor's original C1 100 ms timeout and provenance. The successor completes and the added ledger assertion rejects `error_code` -192 (`Assert.NotEqual`, expected not -192, actual -192). |
| M2 | After the baseline timeout assignment, M2 inserts `if (child) settings = settings.SetItem("message.timeout.ms", timeout);`, retaining child linger 0. The existing assertion rejects `b10_offline_image_exit_code:3`; published termination is `no_kill`, outcome `b10_abrupt_warmup_failed`, with an empty capture-gap list. |
| M3 | Cross-restart arm skips its warm-up and binds the earlier report, leaving the control intact. The existing `successor_permitted` assertion fails. Termination records intended self-kill, exit 137, permission false, and `malformed_frame` plus `b10_abrupt_report_binding_invalid`. |

The non-137 M2 exit belongs only to the prescribed mutant. Both normal children exited 137. No extra image test or workflow dispatch was performed.

## Cold review input and publication gate

R1 applied `.agents/skills/repo-review/SKILL.md` as a fresh-context comprehensive review of the entire authored diff. Its input was the frozen staged revision based on `5dfdab8ab94b3a52342650a3823dac5769d92746`, compared with `origin/main` at `e74849ea716b6c4b8fa6c5143c6c4175c7038eea`. The exact frozen tree and diff hash are retained in `/tmp/404-r1-inputs.json`; the complete review report identifies them. Corrections followed the complete report.

R1 completed with two P1 findings. The first identifies a sequencing departure: plan section 8 requires all acceptance commands before the cold review, but R1 began while the full .NET command was still running. R1 consumes a comprehensive pass and does not satisfy that approved checkpoint. Complete the required gate before the subsequent qualifying review; no waiver or budget reset is claimed.

The second finding identifies legacy run identity in the newly added journal vectors. Commit `472e5c3919764e0f525e5c20d2a29149d500aae3` supplies the approved `b10d-1-1` identity to the new vectors while preserving existing fixture defaults and assertions. An isolated Release locked restore and build passed with zero warnings/errors; all 101 journal tests passed, none skipped. Debug also passed all 101. The verified file SHA-256 is `070ba53f9ffb73b22e251f0d836e8ec845962a966ca41a294c735214946a0b52`. Evidence: `/tmp/404-journal-r1-release-evidence/verification-summary.txt`. This belongs to the common-input finding root already observed by the author, rather than a new distinct root.

Cumulative cold-review budget: one comprehensive pass complete, zero focused passes. Two distinct review roots are recorded: common test inputs and premature review sequencing. Limit: two comprehensive and two focused passes across sessions; exhaustion or unsafe focus hands off to Hari. The full R1 report, dispositions, final-head CI and hosted code/security review evidence belong to the pull request's self-review record; the final report is not added to its own reviewed receipt.


## Continuation acceptance and qualifying review

The continuation restored `codex/404-b10-warm-leader` from backup commit `9dbbe951a3abc8e0d6a9b0ca4c46c0ae6f443011`. The managed local Docker socket reported server version 28.4.0. No daemon restart, source correction, test relaxation or acceptance-procedure change was made. Authority catch-up read handoff [6806815301](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6806815301); the pinned plan, approval and retained check still identify the same revision. Refreshes included the issue and both listed parents.

All commands below tested clean source `9dbbe951a3abc8e0d6a9b0ca4c46c0ae6f443011`, whose C# content is unchanged from `ff399b7c2e8f0896bd76fe74748dcb753256fc7e`. The full command used the existing build-only processor workaround recorded above; tests and descendants used the default runtime. Exact scripts and generated reports remain outside the repository under `/tmp/404-recovered-verification/`.

| Check | Measured result |
| --- | --- |
| Locked restore and Release build of the Verifier test project | Exit 0; zero warnings and errors. |
| Unchanged focused `VerifierWindowComponentTests.Given_CorruptRecordAppendedAfterFixedEnd_When_RealHandlerDrains_Then_LaterRecordIsExcluded` | One passed, zero failed or skipped; `ValidateReport` passed. |
| First `./scripts/verify-dotnet.sh` | Exit 2; Architecture 17 and Enrichment 56 passed and validated; Kafka 490 passed, one failed, zero skipped. Later projects and warning control were not reached. |
| Unchanged focused `PhantomSpikeTests.Given_ApprovedFaultMatrix_When_RealClientAttemptsAreObserved_Then_ExperimentalIntegrityIsEstablished` | One passed, zero failed or skipped; `PublishSpikeEvidence` and `ValidateReport` passed. All 40 trials executed and all 40 target scans completed; no trial or experiment failures; cleanup succeeded. |
| Second `./scripts/verify-dotnet.sh`, completed 2026-11-11T21:55:35Z | Exit 0; all six project reports validated: Architecture 17, Enrichment 56, Kafka 491, LoadGen 1808, Scenarios 340 and Verifier 7389. Scripted total: 10101 passed, zero failed or skipped. Warning control built its valid counterpart and rejected the intended CS1030 diagnostic. `B10AbruptRejectionTests.cs` and every other Verifier path are unchanged. |

The first full run is retained at `/tmp/parchwell-dotnet-verification.iCMTZpRm`; the passing full run is at `/tmp/parchwell-dotnet-verification.MK9Tx07i`. Its exact command is still `./scripts/verify-dotnet.sh`. The focused results do not substitute for that successful whole invocation.

The two observed Kafka failures are conservatively counted as two verification roots in this continuation's 20-root budget. They differ from the preceding session's V15: all 40 trials executed and all 10 initial controls passed, but two fault trials lacked complete observation. Trial `7ec5a2731df240fabb7a4774869d6845` (retries 0, Timeout, repeat 3) failed its initial direct watermark query with all broker connections down, before warm-up and fault activation. Trial `6c4409284f474241b586e5f3f5773ca6` (retries 0, ResetPeer, repeat 3) failed the target end-watermark query with `Local_UnknownPartition`. Cleanup succeeded for the experiment and every trial. Retained broker logs show fencing and overloaded-controller heartbeat messages; their underlying cause is unconfirmed. No assertion or frozen fixture was changed to obtain the later pass.

The single focused rerun also retained 31 read-only resource samples across 240.463386 s. It recorded no OOM event. Sampling misses short spikes, adds some load and has no time-correlated failure-run baseline; it does not prove a repair or a CPU, storage or heap cause. Raw logs, scripts, trial records and sampling limits remain in the external evidence directory. The verification and diagnostic subagents were fresh contexts at high effort; the verification model was `gpt-6.1-sol`, and the diagnostic agent inherited the control model, whose exact identifier is not exposed.

The earlier image, mutation, Python and chart evidence above is retained: the executed image paths are unchanged, and the full gate covers the later C# fixture corrections. No workflow, trial or merge ran in this continuation. The nine unchanged-file documentation warnings take one blanket disposition in the pull request; the final documentation scan is run after these authored updates.

Qualifying R2 is comprehensive, comparing the full frozen authored diff with `origin/main` at `e74849ea716b6c4b8fa6c5143c6c4175c7038eea`. Its input revision is the frozen committed head after this continuation's receipt/state update on `9dbbe951a3abc8e0d6a9b0ca4c46c0ae6f443011`; the exact reviewed SHA and complete report will be recorded in the pull request self-review record. R2 starts only after the successful full command and final documentation scan. Budget entering R2: one comprehensive review used, zero focused. The cumulative limit remains two comprehensive and two focused reviews across sessions; the prior premature R1 still counts. Review results and any correction-round counts belong in the pull request, not a receipt-only commit to include its own head.

## Previous session checkpoint

The following checkpoint records the earlier environment blocker and handoff. The continuation's successful whole-command result above supersedes its pending acceptance status; its failure evidence and R1 report remain historical evidence.

The implementation is not at the publication checkpoint. The C# source is committed at `ff399b7c2e8f0896bd76fe74748dcb753256fc7e` on the local `codex/404-b10-warm-leader` branch; the backup destination is `codex/404-b10-warm-leader-wip`. No pull request or hosted final-head CI/code/security review exists. The required full command has not exited 0, and qualifying comprehensive R2 has not started. No workflow was dispatched and nothing was merged.

Both failed cases passed unchanged in focused verification. The subsequent full invocation passed Kafka 491, LoadGen 1808 and the other preceding projects, but failed another verifier startup/cleanup row: 10100 passed, one failed, no skips; exit 2. It is the same V14 failure pattern, not a new distinct root. Verifier validation and warning control were not reached. Evidence: `/tmp/404-dotnet-after-focused-evidence.json`. Preserve the existing image, Python and chart evidence while their executed paths remain unchanged. Diagnose the outstanding Kafka healthy-control finding with the smallest affected check, then complete the unchanged `./scripts/verify-dotnet.sh` acceptance gate with no skips and warnings as errors. Run the final documentation check after authored corrections, then use the remaining consolidated comprehensive review before any non-backup push. The plan's frozen paths and section 10 stop rule still apply: a needed Kafka/verifier fixture or other out-of-scope change returns to Hari rather than being implemented here.

Managed Docker recovery is now the concrete blocker. For the latest exact owned resource, the failed verifier row ended at `2026-11-11T20:29:59.3712374Z`, then Docker emitted its create event at `20:30:48.216943908Z`, 48.845706508 s later. It emitted no start event and was subsequently destroyed; the investigator removed nothing. This establishes a delayed daemon lifecycle event without assigning its underlying cause. Current endpoint/context/TLS selectors are absent, the managed socket is correct, and no task-owned configuration correction was found. The task is UID 1000, while the preexisting daemon and logs are root-owned. The managed runtime Docker reference says: "A non-root task user has socket access but no permission to start or restart the daemon; no privileged startup helper is provided. In that case, report the diagnostic and request environment recovery." Source: `skill://plugin_connector_1p_c5b7d5df5d7081918f2c4be5a633ed5d/cloud-environment-runtime/references/docker.md`. Evidence: `/tmp/404-docker-runtime-recovery.md`, `/tmp/404-runtime-events.json` and `/tmp/404-runtime-owned-lifecycle.json`.

Preserve this checkpoint on `codex/404-b10-warm-leader-wip` before environment recovery. Resume unchanged focused verification and the full mandatory command only after platform/operator recovery or a healthy replacement workspace. This is an environment blocker, not a request to relax the gate or a finding that frozen source changes are necessary. No daemon restart, competing daemon, storage-driver change or timeout increase was attempted.

This session recorded 15 distinct verification roots and two distinct review roots; neither 20-root limit was exhausted. Cold-review use remains one comprehensive and zero focused rounds cumulatively. One comprehensive and two focused rounds remain across all sessions; a continuation does not reset that budget. Publication still requires applicable cold coverage, then final-head CI and hosted code/security reviews. Hari merges the protected blueprint change.

The complete earlier R1 report follows to preserve its findings and cumulative budget without relying on temporary files. Its sequencing finding remains unresolved until the mandatory gate and subsequent qualifying review complete. Its journal-identity finding is fixed and independently verified as recorded above.

<details><summary>Complete comprehensive R1 report</summary>

Report: complete

Comprehensive R1 reviewed source `5dfdab8ab94b3a52342650a3823dac5769d92746`, including frozen staged tree `8b6ae0d5dcf1040556695e89e1e03100c64bae22`, against `e74849ea716b6c4b8fa6c5143c6c4175c7038eea`. Diff SHA-256: `799188ecb021dc0aa89e1dae19884a1385dfa3e33f84f0a4f0bff92626377ea9`.

Applied `.agents/skills/repo-review/SKILL.md`, AGENTS.md governance/review/writing rules, docs/agents/pull-requests.md, docs/agents/factory.md, docs/engineering/dotnet.md, and applicable baseline ADRs. Used the `origin/main` blueprint as the governing baseline and the approved Revision 3 plan at `34aae8485053429a5b183e86a5f2c39dda570d25` for its authorized amendment. Read the approval, retained plan check, and recognized decisions identified in the receipt. Checked added external claims against the pinned official source files.

All 25 touched files reviewed:

```text
docs/blueprint.md
docs/operations/b10-abrupt-contract.md
infra/scripts/phase/b10_abrupt.py
infra/scripts/phase/tests/test_b10_abrupt.py
items/404/b10-warm-leader-receipt.md
items/404/state.md
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptHost.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptJournal.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptParent.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProducer.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptStatistics.cs
src/Parchwell.LoadGen/Run/Abrupt/B10AbruptWire.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptEndToEndTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptHostRegistrationTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptJournalTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptKafkaProxy.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptKafkaProxyTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptOfflineImageTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProcessHarness.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProducerPolicyTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProducerTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptRealProcessTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptSupervisorTests.cs
tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptWarmLeaderTests.cs
tests/Parchwell.LoadGen.Tests/Component/LocalKafkaFixture.cs
```

Specification comparison: R1-R11 are represented by the effective producer settings and provenance, durable warm-up queue capture, leader-report selection and binding, child deadline and cleanup bounds, journal/phase validation, delaying proxy, offline pair assertions, and design/contract amendments. C0, the C1 timeout Job, verifier code, expected outcomes, projects/packages, and workflows remain outside the changes. No production behavior defect or scope expansion was found. The following approval-contract departures remain.

P1  items/404/b10-warm-leader-receipt.md:32  Approved Revision 3 plan, section 8 "Acceptance commands"  The comprehensive cold review started without the required successful full .NET gate. Section 8 says "Run by the implementer before the cold review" and requires `./scripts/verify-dotnet.sh` to exit 0 with no skips; this receipt instead explicitly runs it concurrently, following unsuccessful earlier complete-suite attempts. Blocking publication is appropriate but does not satisfy that earlier checkpoint. Complete the gate and record the sequencing departure and final evidence before treating a subsequent review as satisfying the approved checkpoint.

P1  tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptJournalTests.cs:621  Approved Revision 3 plan, section 6 "Common inputs"; docs/agents/factory.md "Item lifecycle", contract-grade test inputs  The new warm-up reader vectors use `B10/test/C1/1/{arm}` and the legacy `run_id` `test`, whereas the approved test map fixes the common run ID as `b10d-1-1`. The newly added R8 cases call `CompleteJournal()` and therefore retain this different identity throughout. The plan pins test input literals and section 10 does not delegate changes to them. Supply the approved identity to the new vectors while preserving existing fixture behavior where needed.

Findings: 2 at P0 or P1

Pre-existing (not blocking): None reported.

</details>

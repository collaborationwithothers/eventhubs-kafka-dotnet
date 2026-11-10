# PR3a evidence completeness receipt

After a B10 abrupt first pod is killed, the ledger container now holds the eight cohort files the verifier needs to check the timeout and control cohorts without the child, and the supervisor journal carries every native error callback the first child saw until its event stream ended. The change adds no verifier logic, gate, failure name or budget. It touches one protected test file (the `ExportNames` array only), so Hari merges it.

This receipt is the single authored receipt of the pull request. It records the plan and decisions that govern it, the execution card, the choices the plan left to the implementer, the commands run with their results, and the review record.

## Execution card

- Deliverable: the change that [PR3a evidence completeness plan Revision 8](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6753635457) specifies, `items/404/plan-pr3a-evidence-completeness.md` at `d293b4b659261c7b4d4c3c5691328fd79a07b00e` (SHA-256 `b5d857b78747e45daed325a7910fc8e73cd64be6416e03c31510642a53ecb858`, verified against the file at that commit). Files and requirements are the plan's sections 3 and 5; they are not copied here.
- Current authorization: [approval 6753938298](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6753938298) under standing decision [6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614) point 2 (posted by the control session, not after Hari's review); [plan check 6753915132](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6753915132), `Blocking findings: none`, whose P2-1 to P2-4 are `apply in implementation`. Implementing tool: Claude Code (6752430614 point 3). Order and stacking: [6752454732](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752454732) and [6752554218](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752554218) point 2: this branch starts from the unmerged successor fix head `9c84ba4174e2c7b40f0805b3e6cfe6efd26ba818`, is not merged before it, and is rebased, rechecked under the plan's stop rule and reviewed again after it merges. No trial, dispatch, Azure access, `expected.yaml` or `results/` change is authorized.
- Mandatory gates: the plan's acceptance commands A0 to A9 (below), `./scripts/verify-dotnet.sh`, `npm run check:docs`, the cold review of docs/agents/pull-requests.md rule 8, final-head CI and hosted Codex code and security reviews, and Hari's merge.
- Exact verification commands: those of the plan's section 10 (A0 to A9), run as written; and `./scripts/verify-dotnet.sh`.
- Earliest authorized publication checkpoint: the control session tells this session that the successor fix pull request exists. Until then only backup pushes to `claude/404-pr3a-evidence-completeness-wip` (rule 12).
- Review budget: rule 8, at most two comprehensive passes and two focused rounds across all sessions; at exhaustion the work is saved under rule 12 and handed to Hari.

## Decisions and stop rule

Decisions read in full: 6752430614, 6752454732, 6752554218, and every decision the plan cites (6705801800, 6708991894, 6749565231, 6749995101, 6750021224, 6750864341, 6750879393, 6750891952, 6750940329, 6751703425, 6752059755, 6752078378). Decisions made after the plan was approved (6753263792, 6753364128, 6754092685, 6754970055, 6755339632) concern the successor fix, the capture races and their tests; none changes a PR3a contract. Catch-up: the issue has comments up to 6756884940 (2026-11-10T02:05:05Z, the successor fix's handoff, not a decision); the state file covers it.

Stop rule against the successor fix. The plan pins the successor fix plan Revision 3. The branch it was built on, `9c84ba4`, differs from `main` `f5cc496` in `B10AbruptDiagnostics.cs` (added `B10AbruptFact` members and `MaximumSiteFacts` 35), `B10AbruptHost.cs` (successor completion wiring), `B10AbruptProducer.cs` (`SuccessorCompletion`), `B10AbruptOptions.cs`, the contract's cap sentence, and tests. It does not change `FirstMembers`, `SuccessorMembers`, the seal, manifest or termination objects, the predecessor binding, `B10AbruptPublisher.cs`, `B10AbruptSupervisor.cs`, `B10AbruptWire.cs`, the `B10AbruptStage` list or the exporter's first-instance half. Checked by `git diff f5cc496 9c84ba4`. No conflict with the plan's contracts or requirements, so the stop rule did not fire. The capture contention race fix is not yet on this branch (it changes only `B10AbruptStatistics.cs` and the state file).

## Choices the plan left to the implementer

Plan check P2-1 to P2-4 are applied exactly as the check gives them: P2-1 the stage-line rule on contract line 162, P2-2 the two "Raised when" cells, P2-3 the fatal-coverage sentence (which replaces the sentence on contract line 51), P2-4 the export checker's expected set. Plan check notes N3 and N6 and the following are implementer choices, each checked by the cold review:

1. N3: the supervisor re-reads and checks all eight cohort files before it builds the manifest, so a changed file fails publication before `PublishFirstAsync` writes its first member (T2: storage holds only `registration.json`).
2. N6: `B10AbruptWire.ReadFrame`'s kind allow-list also gains `native_error`. R7 names only the message reader's list. The journal reader had no caller for the new kind; it is added so the journal's own frame kind is readable by the journal's own reader.
3. The wire layer accepts any non-empty `fatal_capture_status` in a cohort seal and the supervisor's seal load owns the comparison with `journal`, because T8 requires a seal with the old literal to raise `b10_abrupt_control_incomplete`, not `malformed_frame`. The termination and `capabilities` literals stay wire-checked.
4. R8's coverage check is folded into the existing `fatal_status_matches` fact. The plan allows one new fact name (`upload_succeeded`); a second would break section 3.1.
5. A cohort file's length must be at least 1 and at most its read limit, in the checker and in the runner download (the plan says "within the 4.1 read limit"; the download helper already requires at least 1 byte).
6. The host's error handler calls the channel first, then the evidence session, so a slow evidence capture cannot delay the journal copy.
7. The channel's native-error members take primitives, not `Confluent.Kafka.Error`, so the channel keeps no Kafka client reference; the host handler does the mapping.
8. T8's positive case ("after one frame is journaled the seal loads") runs on the control seal, because a loadable timeout seal needs a real ledger. The negative cases run on both the control and the timeout seal. The seal facts line is shared.
9. T7 and T8 live in `B10AbruptFatalCaptureTests` and reuse `DeadlineFixture`, `WriteControlSealAsync` and `LoadControlSealAsync`, which became `internal` in `B10AbruptSupervisorTests.cs` (no assertion changed). `B10AbruptProducerTests.Fixture`, `HostOptions`, `ProducerHostServices`, `UnusedTokenProvider` and `RuntimeMember` also became `internal`.
10. The export test harness (`B10AbruptWireProducerTests.ProduceAsync`) gained optional parameters (`nativeErrors`, `atKill`, `publicationStorage`, `supervisorErrors`). Its clock became `HarnessClock`, manual only from the `control-seal.json` create to the `termination.json` create, because the supervisor's pump and the successor's chain wait on that clock's timers.
11. T14's failure line is observed at the first read of the synthetic child's exited state after the throw, through an `ExitedRead` hook on the synthetic process.

## Test map

The plan's section 8 is the predicate map. Each test maps as follows (test names are abbreviated to the part after `Given_`).

| Plan test | Requirements | Test |
|---|---|---|
| T1 | R1, R2, R9 | `B10AbruptPublicationTests`: `ProducedFirstInstance_When_Published...`; the export of `B10AbruptWireProducerTests` (22 files) |
| T2 | R1 | `B10AbruptPublicationTests`: `CohortFileChangedAfterSeal...` (3 cases) |
| T3 | R3 | `B10AbruptPublicationTests`: `PredecessorBinding_When_CarryingACohortFile...`; Python `test_produced_cohort_files_accepted` |
| T4 | R4, R10 | Python `test_produced_cohort_mutations_rejected` (8 cases, each also through `Trial.run()`) |
| T5 | R5 | Python `test_produced_cohort_collection` (changed byte, missing object, 70000-byte ledger) |
| T6 | R6, R7 | `B10AbruptFatalCaptureTests`: `RealFatalNativeError...` |
| T7 | R7 | `B10AbruptFatalCaptureTests`: `NativeErrorsOutOfSequence...`, `FirstNativeErrorNotNumberedOne...`, `NativeErrorAfterKillRequest...` |
| T8 | R8 | `B10AbruptFatalCaptureTests`: `ControlSealCountingAnUnjournaledNativeError...`, `ControlSealWithPlaceholderFatalStatus...`, `TimeoutSealWithBadFatalCoverage...` (2 rows) |
| T9 | R6 | `B10AbruptFatalCaptureTests`: `ConcurrentCallbacksAndCountReads...` |
| T10 | R9 | `B10AbruptPublicationTests`: `NativeErrorsBeforeAndAfterTheKill...` |
| T11 | R6 | `B10AbruptFatalCaptureTests`: `FullWriteQueue...` |
| T12 | none (guard) | `check_b10_abrupt_producers.py` on the new export (A3) |
| T13 | none | `B10AbruptRejectionTests` (A5); its diff adds eight lines of names and removes none |
| T14 | R12 | `B10AbruptPublicationTests`: `CohortFileUploads...`, `SecondCohortUploadFails...` |
| T15 | R6 | `B10AbruptFatalCaptureTests`: `HostBuiltProducer_When_NativeFatalErrorIsRaised...` |
| T16 | R6, R8 | `B10AbruptFatalCaptureTests`: `NativeErrorCountThatChangesDuringCohortRuns...` |

Mutants of the plan's section 8 table, run on scratch edits of the source (each reverted) against the named test: bypass handler, caught by T15 (the wiring test also fails T6); unwired count source, caught by T15; seal bodies hard-coded to 0, caught by T16 (timeout seal and control seal edited separately); stale read, caught by T16; upload line held (always, and on failure only), caught by T14 (both held-always cases; only the failure case for hold-on-failure); lock removed, caught by T9, 3 of 3 runs.

## Verification record

Source commit for the results below: the commit named in "Commands run", with the working tree otherwise clean.

(filled in after the final run)

## Cold review record

(filled in after each round)

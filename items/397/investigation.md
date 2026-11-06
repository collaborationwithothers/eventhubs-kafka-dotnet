# C5 runtime investigation

The completed A01 shard tested the same mutant identities, but median mutant execution rose from 20 to 72 seconds between the two recorded runs. The new full-phase capacity cases account for 61.500 seconds of summed test duration in one local baseline; this supports repeated test cost as a contributor, without isolating the entire hosted slowdown. This investigation changes no test, mutation setting or acceptance rule and establishes no optimization speedup.

## Measurement context

Measured on 2026-11-06 at source `29e3f0a9b17c3cf06b97bcc5c7125731a23dcc44`, whose verifier tree includes PR 393 and excludes the still-open PR 396. SDK 10.0.402, runtime 10.0.13, xunit.v3.mtp-v2 4.0.2 and Stryker 5.0.0 are pinned. The local Linux container exposes 5 online CPUs, a cgroup CPU quota of 400000/100000 (4 CPU equivalents), and a 17179869184-byte memory limit. Host MemTotal was 18440100 KiB. These are environment observations, not GitHub runner specifications.

All capacity workloads are synthetic. Local test timings are one observation, not repeated benchmark statistics. Test-duration sums include overlapping execution and must not be read as wall time or directly subtracted from a hosted job. A displayed zero means the XML reported zero at its precision, not that a test is free.

Raw inputs and scripts remain outside git under `/workspace/scratch/397`; they are session-local, not permanent downloadable evidence. Hosted source evidence is downloadable from the linked Actions artifacts while retained. The authored calculation commands below reproduce the tables from those inputs; no generated log/XML/JSON is committed.

Commands run from the repository root, with `/workspace/scratch/dotnet` prepended to PATH:

```bash
timeout 900 dotnet restore tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --locked-mode
ParchwellVerifierMutationOnly=true timeout 900 dotnet build tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --no-restore -m:1
timeout 600 dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --no-build --results-directory /workspace/scratch/397/baseline -- --report-xunit-xml --report-xunit-xml-filename baseline.xml
python3 /workspace/scratch/397/measure_capacity.py
```

Restore and build exited 0; build reported 30.91 seconds, zero warnings/errors. The property applies at compilation; the no-build baseline uses that mutation-only assembly. The initial attempt to invoke the DLL directly with MTP XML flags exited 3 before running tests because the native xUnit driver does not accept those flags. Reading its help and switching the baseline to the repository's existing `dotnet test` MTP invocation corrected this measurement-command error. The error was `unknown option: --report-xunit-xml`. Its original standalone log was inadvertently overwritten by reuse of the baseline log pathname; this retention gap is explicit rather than reconstructed as raw output. No test assertion was changed or retried. No Stryker process or workflow was launched.

## Hosted run comparison

Sources: [before run 41987704862](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862) on `8491e3a324578cf33ddcb78aa12521946be04883` and [after run 42187387042](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042) on `68b82dfcd2a5fb5f4daede5778565b48e5ad464f`. GET Actions run/jobs API snapshots were read on 2026-11-06. Both job listings fit one page of 100. The latter run is completed/cancelled, not still running; its aggregate job failed. Cancelled durations below are elapsed until cancellation, not completion estimates. No ratio is reported for them or for new shards.

| Shard | Before job / s | After job / s | After conclusion | Completed ratio |
|---|---:|---:|---|---:|
| a01 | [127112304958](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112304958) / 846 | [127790859107](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859107) / 2773 | success | 3.28 |
| single-action-repeat-p2 | [127112304996](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112304996) / 4291 | [127790859326](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859326) / 8613 | cancelled | - |
| workload | [127112305396](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112305396) / 1809 | [127790859340](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859340) / 2742 | success | 1.52 |
| single-action-configuration-p5 | [127112305018](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112305018) / 4048 | [127790859399](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859399) / 8542 | cancelled | - |
| phase | [127112304742](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112304742) / 2737 | [127790859405](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859405) / 8502 | cancelled | - |
| single-action-configuration-p7 | [127112305097](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112305097) / 5463 | [127790859410](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859410) / 8609 | cancelled | - |
| b10-restart-manifest | [127112305120](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112305120) / 3474 | [127790859413](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859413) / 4811 | success | 1.38 |
| b10-intake | [127112304976](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112304976) / 1793 | [127790859416](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859416) / 3149 | success | 1.76 |
| b10-sweep-judge | not present | [127790859442](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859442) / 2536 | success | - |
| single-action-repeat-p1 | [127112304703](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112304703) / 2107 | [127790859444](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859444) / 8611 | cancelled | - |
| single-action-configuration-p6 | [127112304781](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112304781) / 4393 | [127790859445](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859445) / 8612 | cancelled | - |
| publication | [127112304903](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112304903) / 3454 | [127790859465](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859465) / 8537 | cancelled | - |
| batching | [127112304993](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112304993) / 871 | [127790859480](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859480) / 3864 | success | 4.44 |
| single-action-configuration-p2 | [127112304812](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112304812) / 684 | [127790859496](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859496) / 1645 | success | 2.40 |
| single-action-configuration-p4 | [127112304967](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112304967) / 5201 | [127790859513](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859513) / 8610 | cancelled | - |
| single-action-configuration-p1 | [127112304456](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112304456) / 2724 | [127790859520](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859520) / 5243 | success | 1.92 |
| single-action-configuration-p3 | [127112305068](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112305068) / 3721 | [127790859529](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859529) / 8543 | cancelled | - |
| b10-repeat | [127112304943](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112304943) / 2301 | [127790859535](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859535) / 5755 | success | 2.50 |
| single-action-repeat-p3 | [127112304839](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112304839) / 1772 | [127790859542](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859542) / 4802 | success | 2.71 |
| b10-phase-registrations | not present | [127790859543](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859543) / 1389 | success | - |
| idle-observation-p2 | [127112304974](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112304974) / 1986 | [127790859554](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859554) / 5204 | success | 2.62 |
| b10-sweep | not present | [127790859555](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859555) / 1365 | success | - |
| single-action-configuration-p8 | [127112304870](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112304870) / 917 | [127790859577](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859577) / 2202 | success | 2.40 |
| idle-observation-p3 | [127112304910](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112304910) / 2302 | [127790859591](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859591) / 6300 | success | 2.74 |
| b10-judge | [127112305185](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112305185) / 954 | [127790859596](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859596) / 3666 | success | 3.84 |
| b10-sweep-codec | not present | [127790859606](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859606) / 8550 | cancelled | - |
| idle-observation-p1 | [127112304752](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/job/127112304752) / 1074 | [127790859656](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859656) / 2429 | success | 2.26 |
| b10-late-write | not present | [127790859679](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/job/127790859679) / 3404 | success | - |

A01 artifacts [13396924496](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41987704862/artifacts/13396924496) and [13490258849](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42187387042/artifacts/13490258849) supply command logs, receipts, configs and native reports. Their command-log and report SHA-256 values match their receipts. Both configs use MTP, coverage off and break 60. Matching target path, mutator, replacement and source location gives identical populations: 71 killed and 15 compile errors in both reports. Numeric mutant IDs are not the cross-run identity.

| A01 observation | Before | After |
|---|---:|---:|
| Discovered tests | 5713 | 6555 |
| Initial run start to logged output, including server resets (s) | 25 | 65 |
| Complete paired mutant intervals | 71 | 71 |
| Minimum / median / maximum interval (s) | 17 / 20 / 39 | 54 / 72 / 104 |
| Sum of mutant intervals, two runners overlap (s) | 1461 | 5147 |
| Shard command elapsed from receipt (s) | 816.395411730 | 2740.352816852 |
| Entire job elapsed (s) | 846 | 2773 |

The before initial run spans 07:31:26 to 07:31:51 in its command log; after spans 08:25:06 to 08:26:11. Pair each `Testing mutant(s) [ID]` with `Test run for ID:`; timestamps have one-second resolution. These are real mutant invocations including runner coordination, not isolated assertion timing. No unmatched starts remain in this sample. Job minus command time is 29.605 s before and 32.647 s after; setup/upload overhead outside the command cannot explain this shard's increase. Build, discovery, instrumentation and server behavior inside the command remain distinct possible costs.

PR 388 also merged between these run commits. The test-count increase cannot be attributed entirely to PRs 391-393. The fixed A01 sample does not establish the cost distribution of every shard or isolated static-mutant invocation.

## Local test cost

The MTP baseline passed 6555 tests with no failures or skips. Its reported duration was 79.866 seconds; the sum of XML test durations was 176.707 seconds across 84 classes. The full class aggregation is reproducible with the calculation block below; the largest classes are shown here.

| Class | Summed test seconds |
|---|---:|
| `Parchwell.Verifier.Tests.Host.B10SweepHandlerTests` | 72.617 |
| `Parchwell.Verifier.Tests.Host.VerifyInputCapacityTests` | 36.301 |
| `Parchwell.Verifier.Tests.Judge.JudgeHandlerTests` | 21.357 |
| `Parchwell.Verifier.Tests.ProbeGate.ProbeGateHostTests` | 10.998 |
| `Parchwell.Verifier.Tests.Host.B10TwoInstanceVerifyTests` | 5.881 |
| `Parchwell.Verifier.Tests.Host.VerifierCompletionTests` | 4.545 |
| `Parchwell.Verifier.Tests.SingleAction.ProbeV2HandlerTests` | 2.607 |
| `Parchwell.Verifier.Tests.IdleObservation.IdleObservationJudgeHandlerTests` | 2.352 |
| `Parchwell.Verifier.Tests.Host.B10SweepOptionsTests` | 2.272 |
| `Parchwell.Verifier.Tests.Window.ConsumedRecordBudgetTests` | 2.176 |

The ten longest individual observations (parameter values distinguish theory rows):

| Test | Seconds |
|---|---:|
| `Parchwell.Verifier.Tests.Host.VerifyInputCapacityTests.Given_FullA01AttemptInventory_When_UsingDeployedInputCapacity_Then_PublishesExactCompleteCounts` | 36.257 |
| `Parchwell.Verifier.Tests.Host.B10SweepHandlerTests.Given_MaximumAdmittedPhaseVolumeInFinalTail_When_Swept_Then_DeploymentLimitsFitAndActualSubLimitsFail(phase: "C0", boundary: "record-count-minus-one")` | 25.9 |
| `Parchwell.Verifier.Tests.Host.B10SweepHandlerTests.Given_MaximumAdmittedPhaseVolumeInFinalTail_When_Swept_Then_DeploymentLimitsFitAndActualSubLimitsFail(phase: "C0", boundary: "complete")` | 11.52 |
| `Parchwell.Verifier.Tests.Host.B10SweepHandlerTests.Given_MaximumAdmittedPhaseVolumeInFinalTail_When_Swept_Then_DeploymentLimitsFitAndActualSubLimitsFail(phase: "C1", boundary: "actual-byte-count-minus-one")` | 6.136 |
| `Parchwell.Verifier.Tests.Host.B10SweepHandlerTests.Given_MaximumAdmittedPhaseVolumeInFinalTail_When_Swept_Then_DeploymentLimitsFitAndActualSubLimitsFail(phase: "C0", boundary: "actual-byte-count-minus-one")` | 6.049 |
| `Parchwell.Verifier.Tests.Host.B10SweepHandlerTests.Given_MaximumAdmittedPhaseVolumeInFinalTail_When_Swept_Then_DeploymentLimitsFitAndActualSubLimitsFail(phase: "C1", boundary: "complete")` | 6.015 |
| `Parchwell.Verifier.Tests.Host.B10SweepHandlerTests.Given_MaximumAdmittedPhaseVolumeInFinalTail_When_Swept_Then_DeploymentLimitsFitAndActualSubLimitsFail(phase: "C1", boundary: "record-count-minus-one")` | 5.88 |
| `Parchwell.Verifier.Tests.Host.VerifierCompletionTests.Given_NormalReturn_When_StopAndDisposalFinish_Then_EmitsOneReceiptWithHandlerCode(mode: "start-line", code: 0)` | 4.43 |
| `Parchwell.Verifier.Tests.ProbeGate.ProbeGateHostTests.Given_SynchronousBlockedGateRead_When_WatchdogExpires_Then_ProductionHostExits124` | 3.905 |
| `Parchwell.Verifier.Tests.Host.B10SweepOptionsTests.Given_B10PhaseMode_When_Composed_Then_RegistersItsOwnDependencies(mode: "judge")` | 2.229 |

Attribution uses each PR's first-parent diff, maps changed lines within current test-method bodies, and sums only the matched XML rows. Helper/fixture changes outside those bodies are not charged here and are not quantified. The current file contents for these mapped methods have no subsequent modifications at the measured source. This measures present cost, not incremental before/after cost.

| PR | Mapped methods / cases | Current summed seconds | Limits |
|---|---:|---:|---|
| 391 partition readiness | 0 / 0 | not applicable | Changed component tests are excluded by the existing mutation-only project property. They add no tests to C5. Their ordinary component runtime was not measured. |
| 392 isolated diagnostics | 7 / 14 | 0.184 | Includes six new methods and one modified two-case host method. Shared diagnostic-helper and Python controller overhead is not isolated. |
| 393 scenario/sweep | 37 / 253 | 67.630 | Includes new capacity cases and other added/modified methods, not every test in the touched files. Shared fixture changes can affect existing tests too. |

The six capacity rows account for 61.500 seconds of the mapped PR 393 duration. The other mapped methods total 6.130 seconds. There is no overlap between the mapped PR 392 and PR 393 methods. The combined mapped current cost is 67.814 seconds; this is not a measured incremental slowdown. Existing input-capacity and host tests also remain costly.

<details>
<summary>Mapped changed methods</summary>

| PR | Method | Cases | Seconds |
|---|---|---:|---:|
| 392 | `GivenCurrentActivity_WhenRecorded_ThenRetainsCorrelationWithoutChangingAcceptance` | 1 | 0.018 |
| 392 | `GivenSyntheticRequestEvents_WhenRecorded_ThenPreservesPredicateAndAttribution` | 1 | 0.046 |
| 392 | `GivenStackBoundary_WhenFormatted_ThenMarksTruncationWithinLimit` | 3 | 0.002 |
| 392 | `GivenDiagnosticRecordBoundary_WhenRecorded_ThenKeepsSeparateDropCounts` | 3 | 0.063 |
| 392 | `GivenChartVerifyKeysWithoutCallerProducerIdentity_WhenProductionHostRuns_ThenDiscoversProducerAndPreservesFullFidelity` | 2 | 0.053 |
| 392 | `GivenLongPayloadAndFailingDiagnosticOutput_WhenRecorded_ThenPreservesRawGuardEvidence` | 1 | 0.002 |
| 392 | `GivenScalarBoundary_WhenFormatted_ThenMarksTruncationWithinLimit` | 3 | 0 |
| 393 | `Given_OverlappingAttemptStringsAndRepeatedCoordinates_When_Projected_Then_OwnersStayDistinctAndCoordinatesCountOnce` | 1 | 0.02 |
| 393 | `Given_OwnerCountAroundTwo_When_Projected_Then_UnexpectedOwnershipShapeIsUnavailable` | 4 | 0 |
| 393 | `Given_EnabledAndDisabledDiagnostics_When_Swept_Then_ResultsFinalizationAndExitAreIdentical` | 5 | 1.730 |
| 393 | `Given_PinnedTwoLateLocations_When_Projected_Then_OneLandsInRepeatTwoAndOneInGap` | 1 | 0.003 |
| 393 | `Given_OverlappingLandingWindowsOrOriginalBounds_When_Projected_Then_AmbiguityIsNotAttributedToALaterRepeat` | 1 | 0.001 |
| 393 | `Given_PartitionCountAroundDeploymentBound_When_Projected_Then_GroupStorageRemainsBounded` | 3 | 0.003 |
| 393 | `Given_ForeignOrMalformedTail_When_Projected_Then_ItCannotBecomeOwnedObservation` | 7 | 0.001 |
| 393 | `Given_SampleCapExhaustion_When_Logged_Then_ExactCountsOmissionsAndOnlyPublicLocationFieldsAreRetained` | 1 | 0.011 |
| 393 | `Given_StartEndAndSweepBoundaries_When_Projected_Then_HalfOpenWindowsDetermineSelectionAndLanding` | 9 | 0 |
| 393 | `Given_ObservationAvailabilityAndScanCompletion_When_Logged_Then_OnlyAvailableCompleteObservationsClaimCompleteFrequency` | 4 | 0.001 |
| 393 | `Given_SampleCountAroundCap_When_Projected_Then_TotalsStayExactAndSamplesAreBounded` | 5 | 0.002 |
| 393 | `Given_RepeatWindowCountAroundFive_When_Projected_Then_LandingLabelsUseOnlyTheFiveRecordedRepeats` | 4 | 0.001 |
| 393 | `Given_MultiplePartitionsAndOwners_When_Projected_Then_SamplesSortByPartitionAndOffsetAndGroupsCoverAllRecords` | 1 | 0.008 |
| 393 | `Given_MaximumAdmittedPhaseVolumeInFinalTail_When_Swept_Then_DeploymentLimitsFitAndActualSubLimitsFail` | 6 | 61.500 |
| 393 | `Given_DestinationExists_When_Captured_Then_OneCreateAttemptPreservesExistingObjectAndNoReceipt` | 1 | 0.004 |
| 393 | `Given_ReceiptArtifactLengthBoundary_When_Encoded_Then_RequiresNonnegativeLength` | 3 | 0.003 |
| 393 | `Given_InvalidMetadataOrLimit_When_Captured_Then_CleansWithoutPublishing` | 6 | 0.001 |
| 393 | `Given_ReceiptArtifactHashLengthBoundary_When_Encoded_Then_RequiresExactSha256Length` | 3 | 0.002 |
| 393 | `Given_DelayedTimerCallbacks_When_BudgetReached_Then_MonotonicDeadlineSuppressesSuccess` | 6 | 0.001 |
| 393 | `Given_LifecycleTimeAtBoundary_When_Captured_Then_CodecRequiresElapsedSettleAndCompletedVerifier` | 6 | 0.001 |
| 393 | `Given_OperationFailureOrCancellation_When_Captured_Then_NeverQueuesFinalReceipt` | 9 | 0.001 |
| 393 | `Given_MetadataHighs_When_Captured_Then_RealReaderAcceptsSortedHighsAndExactReceiptBinding` | 2 | 0.002 |
| 393 | `Given_WriterReceipt_When_WireBindingIsCorrupted_Then_StrictReaderRejectsIt` | 18 | 0.002 |
| 393 | `Given_CaptureHost_When_Finalizing_Then_ReceiptFollowsDisposalAndFailuresSuppressOrFailPublication` | 5 | 0.110 |
| 393 | `Given_RepeatOrProducerBinding_When_Validated_Then_RejectsBeforeIo` | 11 | 0 |
| 393 | `Given_B10Phase_When_Validated_Then_CaptureUsesOnlyMetadataDependencies` | 2 | 2.053 |
| 393 | `Given_MissingIdentity_When_Validated_Then_RejectsBeforeIo` | 32 | 0 |
| 393 | `Given_TimeoutRepresentationBoundary_When_Validated_Then_RejectsOverflow` | 3 | 0 |
| 393 | `Given_MalformedLifecycleTime_When_Validated_Then_RejectsBothFields` | 6 | 0 |
| 393 | `Given_BudgetOrLimit_When_Validated_Then_RequiresExplicitPositiveValue` | 52 | 0 |
| 393 | `Given_LifecycleSettingOnOtherMode_When_Validated_Then_RejectsBeforeModeSpecificEarlyReturn` | 6 | 0 |
| 393 | `Given_SettleInterval_When_Validated_Then_OnlyDeploymentIntervalIsAccepted` | 8 | 0 |
| 393 | `Given_ProcessBudgetSum_When_Validated_Then_RequiresStrictlyGreaterLifetime` | 3 | 0 |
| 393 | `Given_ForeignCoordinate_When_Validated_Then_RejectsBeforeIo` | 11 | 0 |
| 393 | `Given_ModeSpelling_When_Parsed_Then_OnlyExactCaptureModeIsAccepted` | 6 | 0 |
| 393 | `Given_DeployedPhaseRecordLimit_When_AccountingEmptyRecords_Then_EqualityPassesAndNextRecordCannotAdvanceCounters` | 1 | 1.733 |
| 393 | `Given_DeployedPhaseByteLimit_When_AccountingSharedValueBuffer_Then_EqualityPassesAndNextByteCannotAdvanceCounters` | 1 | 0.436 |

</details>

## Capacity memory

The separately filtered native xUnit invocation passed the same six method/parameter identities, with no failures, skips or not-run cases. The outer process elapsed 42.853 seconds and xUnit reported 42.249 seconds. `os.wait4` reported peak RSS of 586548 KiB = 572.801 MiB = 600625152 bytes. This is the peak of the one test-host invocation, including runtime/runner overhead, not six independent peaks or a sum of live process-tree memory. It is not a production-pod measurement or a resource assertion. The native runner differs from MTP, so this run cannot establish MTP overhead or a speedup against the full-suite baseline.

Each case retains its source assertions for 2325000 synthetic reads and 212505000 consumed bytes. No resource ceiling was introduced.

| Phase / boundary | Full-suite MTP seconds | Separate native seconds | Result |
|---|---:|---:|---|
| "C0", boundary: "complete" | 11.52 | 7.753 | Pass |
| "C1", boundary: "complete" | 6.015 | 6.633 | Pass |
| "C0", boundary: "record-count-minus-one" | 25.9 | 7.431 | Pass |
| "C1", boundary: "record-count-minus-one" | 5.88 | 6.579 | Pass |
| "C0", boundary: "actual-byte-count-minus-one" | 6.049 | 6.666 | Pass |
| "C1", boundary: "actual-byte-count-minus-one" | 6.136 | 7.011 | Pass |

## Coverage history and support

Coverage off is an adopted workaround, not an accidental default. The [retained diagnosis](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/187#issuecomment-6400381734) attributed 3511 seconds of a bounded 3600-second attempt to per-test coverage capture, with 685 relay-acknowledgement warnings even while tests passed. That historical report localizes the problem to Stryker's coverage-relay integration with MTP without proving a particular defect. Its original raw files are not independently downloadable here; these numbers are attributed historical evidence, not new measurements.

The [support lookup](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/187#issuecomment-6400504166) preceded [Hari's coverage-off method decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/187#issuecomment-6400987342). Commit `2d83fa36466e953be532e19f1f5a271e09b744b1` applied that choice. Current configuration and workflow validation/tests enforce it.

Directly checked at Stryker 5.0.0 commit `6e77a3451bac4793e9c839c3ff4055c1b30ca3af`:

- [Configuration](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/docs/configuration.md) documents `perTest`, `perTestInIsolation`, `all` and `off`, with `perTest` the default. Coverage off assigns all tests to each mutant, subject to existing bail behavior.
- [Option mapping](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Configuration/Options/Inputs/CoverageAnalysisInput.cs) maps off to `OptimizationModes.None`. [CoverageAnalyser](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/CoverageAnalysis/CoverageAnalyser.cs) assigns every test without capture in this mode.
- [MTP runner pool](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.TestRunner.MicrosoftTestPlatform/MicrosoftTestPlatformRunnerPool.cs) implements reused-process per-test capture, isolated per-test capture and aggregate capture. Thus this runner supports coverage-based modes. This does not prove the historical relay failure is fixed; no coverage-mode experiment or upgrade was performed.

## Options

All estimates below are dated 2026-11-06. None is a measured optimization saving. The committed break threshold remains 60; a stable threshold does not preserve detection if judging tests change.

| Option | Savings basis | Detection and score implications | Recommendation and strongest counterargument | Authority |
|---|---|---|---|---|
| Coverage-based selection | Not measured; no numeric estimate supported without capture cost and per-mutant coverage. | Target identities can remain fixed, but selected tests and NoCoverage classification can change kills and score; relay failure can make measurement incomplete. Break remains 60 unless separately approved. | Consider only a bounded comparison after explicit approval, preserving a coverage-off comparator. Strongest counterargument: the known relay failure may erase savings or undermine trustworthy selection. | Hari's Approval required before changing mode or its enforcing assertions. |
| Per-shard test filter | Not measured; class timing alone gives no safe dependency map or supported saving. | Omits judging tests; can lose kills, alter survivor/timeout counts and score even with unchanged mutants and break 60. | Do not choose from naming conventions alone. Strongest counterargument to rejection: an independently verified dependency map could eliminate large unrelated costs. | Hari's Approval required. |
| Move or mark full-volume cases outside mutation testing | The measured six-case work is 61.500 s in the full baseline, but removable wall time is unmeasured; no numeric saving asserted. | Removes resource-boundary evidence from mutant judgment and may change kills/score at unchanged break 60. Keeping the tests in ordinary CI does not preserve their mutation detection. | Do not exclude solely because they are costly. Strongest counterargument: ordinary capacity CI plus smaller boundary mutation tests might provide sufficient assurance if Hari explicitly accepts the tradeoff. | Hari's Approval required, including marks/filters/assertion edits. |
| More balanced shards with all tests retained | Illustrative estimate: halving A01's 2740.353 s command work gives 1370.176 s, before duplicated setup and imbalance. Assumes two balanced halves and available concurrent runners; not a whole-run prediction. | Can preserve target identities and judging tests if partition union/completeness is verified; timing effects may still change timeout classifications. Combined raw score and break 60 must remain. | Preferred optimization direction after current fixture work is resolved. Strongest counterargument: duplicates baseline/build work, consumes concurrency and cannot remove aggregate repeated-test cost; cancelled shards have no completed timing to balance from. | Semantics-preserving fixes allowed by issue decision; any gate/assertion change still requires Hari. Workflow/config merge goes to Hari. |
| Build/restore reuse or fixture overhead reduction | A01 job-minus-command overhead is only 29.605/32.647 s. Internal build savings and PR 396's fixture savings are unmeasured here. | Pure overhead changes can preserve tests/mutants/score rules; altered fixture behavior or timing remains a risk needing equivalence checks. | Resolve PR 396 and measure its effect before a broader redesign. Strongest counterargument: removing only outer overhead is too small to explain or repair the observed slowdown. | Allowed only while tests, assertions and gates retain their meaning; protected changes go to Hari. |

Recommendation: retain coverage off and every judging test for now, resolve the existing fixture correction, then plan balanced sharding using complete timings. If Hari instead prefers test selection, obtain an Approval on this issue for a bounded coverage comparison before changing the mode, filter or any enforcing assertion. The strongest counterargument to the recommendation is that sharding only redistributes repeated expensive work; selection could offer the larger durable reduction if its reliability is demonstrated.

The runbook timing sentence was verified against `src/Parchwell.Verifier/VerifyRun/B10SweepHandler.cs::ExecuteAsync` and `src/Parchwell.Verifier/Program.cs::RunAsync`: diagnostic work precedes host receipt publication, and watchdog disposal follows publication. This also applies to the residual coupling described for PR 396 without treating its changes as merged.

## Gaps and next action

No full-suite historical rerun, coverage experiment, optimized-fixture measurement, per-case independent memory peak, live pod memory measurement or new mutation campaign was performed. PR 388 and shared-helper effects confound a claim that PRs 391-393 caused the whole slowdown. Only A01 has per-mutant timing analysis here. The cancelled shards have no completion time. This first PR completes diagnosis and small documentation follow-ups; it does not complete the runtime-reduction objective or satisfy post-merge mutation acceptance.

The control session owns any later authorized main-only workflow dispatch. This session only reads existing Actions data. Hari decides any selection/threshold/gate/assertion change after this evidence, and the required code/security/Claude reviews and merge rules still apply to publication.

### Reproduction and input integrity

These authored snippets read the retained inputs, not the network. API snapshots can be reacquired with GET `repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/RUN_ID/jobs?per_page=100&page=1`; preserve all pages if `total_count` exceeds the page length. The exact API timestamps, job IDs, steps and conclusions remain in those snapshots and at the linked jobs.

Capacity controller:

```python
import json,os,signal,subprocess,time
from pathlib import Path
out=Path('/workspace/scratch/397/capacity');out.mkdir(exist_ok=True)
cmd=['/workspace/scratch/dotnet/dotnet','tests/Parchwell.Verifier.Tests/bin/Release/net10.0/Parchwell.Verifier.Tests.dll','-method','Parchwell.Verifier.Tests.Host.B10SweepHandlerTests.Given_MaximumAdmittedPhaseVolumeInFinalTail_When_Swept_Then_DeploymentLimitsFitAndActualSubLimitsFail','-xml',str(out/'tests.xml'),'-noColor']
with (out/'command.log').open('w') as log:
 start=time.monotonic();p=subprocess.Popen(cmd,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
 killed=False
 while True:
  pid,status,usage=os.wait4(p.pid,os.WNOHANG)
  if pid: break
  if time.monotonic()-start>600 and not killed:
   os.killpg(p.pid,signal.SIGKILL);killed=True
  time.sleep(.1)
 result={'command':cmd,'elapsed_seconds':time.monotonic()-start,'exit_code':os.waitstatus_to_exitcode(status),'peak_rss_kib':usage.ru_maxrss,'user_seconds':usage.ru_utime,'system_seconds':usage.ru_stime,'timed_out':killed}
 (out/'measurement.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
```

Local test aggregation and changed-method mapping (the method-body mapping deliberately excludes helper changes):

```python
import json,subprocess,re,xml.etree.ElementTree as E,collections,hashlib,statistics
from pathlib import Path
from decimal import Decimal
p=Path('/workspace/scratch/397'); repo=Path('/workspace/eventhubs-kafka-dotnet')
def git(*args):return subprocess.check_output(['git',*args],cwd=repo,text=True)
tests=[dict(x.attrib) for x in E.parse(p/'baseline/baseline.xml').findall('.//test')]
classes=collections.defaultdict(Decimal)
for t in tests:classes[t['type']]+=Decimal(t['time'])
prs={391:'f74638bd',392:'374bbf4',393:'68b82dfc'}
rows={};changes={}; unmapped={}
for pr,sha in prs.items():
 files=git('diff','--name-only',sha+'^1',sha,'--','tests/Parchwell.Verifier.Tests').splitlines(); changes[pr]=files
 mapping=[]; leftovers=[]
 for f in files:
  if not f.endswith('.cs'):continue
  patch=git('diff','-U0',sha+'^1',sha,'--',f);added=set()
  for line in patch.splitlines():
   m=re.match(r'@@ .* \+(\d+)(?:,(\d+))? @@',line)
   if m:
    start=int(m[1]);n=int(m[2] or 1);added.update(range(start,start+n))
  # Same main source for listed changes; verify file has no later changes in these merges.
  lines=(repo/f).read_text().splitlines(); seen=set();covered=set()
  for t in tests:
   if t.get('source-file')!=str(repo/f):continue
   key=(t['type'],t['method'])
   if key in seen:continue
   seen.add(key); start=int(t['source-line']); indent=len(lines[start-1])-len(lines[start-1].lstrip());end=start
   for end in range(start,len(lines)+1):
    if lines[end-1]==' '*indent+'}':break
   touched=added.intersection(range(start,end+1))
   if not touched:continue
   covered.update(touched); matched=[x for x in tests if (x['type'],x['method'])==key]
   mapping.append({'file':f,'type':key[0],'method':key[1],'start':start,'end':end,'cases':len(matched),'seconds':str(sum((Decimal(x['time']) for x in matched),Decimal(0)))})
  leftovers.append({'file':f,'changed_lines_outside_matched_methods':len(added-covered)})
 rows[pr]=mapping;unmapped[pr]=leftovers
out={'tests':len(tests),'results':dict(collections.Counter(t['result'] for t in tests)),'sum_seconds':str(sum(classes.values())),'classes':[(k,str(v)) for k,v in sorted(classes.items(),key=lambda x:-x[1])],'top_tests':sorted(tests,key=lambda x:-Decimal(x['time']))[:10],'capacity':[t for t in tests if 'MaximumAdmittedPhaseVolumeInFinalTail' in t['method']],'pr_methods':rows,'changed_files':changes,'outside_methods':unmapped}
(p/'analysis.json').write_text(json.dumps(out,indent=2))
print('Classes',len(classes))
for pr,rs in rows.items():print(pr,'methods',len(rs),'cases',sum(x['cases'] for x in rs),'seconds',sum(Decimal(x['seconds']) for x in rs));print('\n'.join(x['method']+' '+str(x['cases'])+' '+x['seconds'] for x in rs))
print('capacity sum',sum(Decimal(x['time']) for x in out['capacity']))
```

Hosted durations, explicit timing pairs and hashes:

```python
from pathlib import Path
import datetime as dt, hashlib, json, re, statistics
p = Path('/workspace/scratch/397')
def seconds(value):
    return dt.datetime.fromisoformat(value.replace('Z', '+00:00'))
for label in ('before', 'after'):
    data = json.loads((p / (label + '-jobs.json')).read_text())
    for job in data['jobs']:
        print(label, job['id'], job['name'], job['started_at'],
              job['completed_at'], job['conclusion'],
              (seconds(job['completed_at']) - seconds(job['started_at'])).total_seconds())
for folder in ('before-a01', 'a01'):
    root = p / folder
    receipt = json.loads((root / 'receipt.json').read_text())
    for name, key in [('command.log', 'command_log_sha256'),
                      ('stryker/reports/mutation-report.json', 'report_sha256')]:
        assert hashlib.sha256((root / name).read_bytes()).hexdigest() == receipt[key]
    starts, pairs, day, previous = {}, [], 0, 0
    for line_number, line in enumerate((root / 'command.log').read_text().splitlines(), 1):
        stamp = re.match(r'\[(\d\d):(\d\d):(\d\d)', line)
        if not stamp:
            continue
        h, m, sec = map(int, stamp.groups())
        now = h*3600 + m*60 + sec
        if now < previous:
            day += 86400
        previous = now
        now += day
        start = re.search(r'(MtpRunner-\d+): Testing mutant\(s\) \[(\d+)\]', line)
        end = re.search(r'Test run for (\d+):', line)
        if start:
            starts[start[2]] = (now, line_number, start[1])
        if end and end[1] in starts:
            begin, first_line, runner = starts.pop(end[1])
            pairs.append((end[1], now-begin, first_line, line_number, runner))
    assert len(pairs) <= 100
    values = [pair[1] for pair in pairs]
    print(folder, len(pairs), min(values), statistics.median(values), max(values), sum(values), starts)
    report = json.loads((root / 'stryker/reports/mutation-report.json').read_text())
    identities = [(path, m['mutatorName'], m.get('replacement'), m['location'], m['status'])
                  for path, file in report['files'].items() if path.endswith('A01EvidenceRecorder.cs')
                  for m in file['mutants'] if m['status'] != 'Ignored']
    print(folder, identities)
```

Input hashes (SHA-256):

| Local input | SHA-256 |
|---|---|
| `before-run.json` | `8f8d6cd9e3dd032597f6c1c280d7d854aebe7d91def5f2575f5a9039e5f7a674` |
| `after-run.json` | `52516c0a163452f7a6350350e9016e532cbbdd35a6cd1b926956f095a7d302e4` |
| `before-jobs.json` | `3b0b04bc2a752b9a6409d51b5fc7ac4cf60567a0614939f77ee724bc832df528` |
| `after-jobs.json` | `03a66e707f17e1fcb11262c1dc8bbc3b7f9c81fad46cb362986fed0b191b38e2` |
| `baseline/baseline.xml` | `0f21445c27d5d4662471b23132259210116e1ab7cf3906356ddccfc898d94aba` |
| `capacity/tests.xml` | `2a3aef5889be00003cd6a1177bd5d74f58f67d14f6479c2025d0111862a28814` |
| `capacity/measurement.json` | `fed1ee8d57a0b46e05538611b6a9b293828d4ea3c86f03b661d7464f820d30d7` |
| `before-a01/command.log` | `f9d4ff8e82ee5ba45a8433a6ce489a66429f2f82bd67eaf25e370a4e6726cd63` |
| `a01/command.log` | `3fdb3e11d15a18bc2f6d01f92506079e1db16b9ac43f13a016af32e91b796096` |

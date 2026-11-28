# Fast suite: contract-grade plan

Revision: r6. Base commit: `15b09147ed4e7c62ea08620b24c14cd049f07562` on `main` (the merge of the issue 601 fix, pull request #603).

Three pull requests make the verifier mutation campaign run a faster test suite, and check every day that the faster suite loses no kill. The first adds a script that proves, from a campaign's own reports, that no mutant is killed only by the slow tests, and that compares two campaigns mutant by mutant. The second adds a full-suite mode to the campaign runner, so a daily campaign can still run the slow tests after they leave the default build. The third moves 12 slow test methods out of the default mutation build, once the proof has passed on a post-601 campaign. The shard re-layout, the qualification-file shrink and the consumer targets of issue 598 wait for hosted timing of the faster suite (section 12).

## 1. Goal and authority

Goal: cut the time each mutant spends in tests by about 60 percent of the summed test time, without losing any kill the full mutation-only suite makes; check that every day; and prepare the measured re-layout.

Authority, in creation order:

1. The body of issue #602: design items 1 to 5, the strongest counterargument to state, and the constraints. The issue was opened at Hari's "yes" in the #499 control session on 2026-11-26.
2. The `ready-for-agent` label on #602, applied 2026-11-27T06:42:25Z (issue events). Under docs/agents/factory.md "Item lifecycle" step 2 it is the item-level approval.
3. [#602 decision 7223314573](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/602#issuecomment-7223314573), 2026-11-27, which adopts the pattern of [#598 decision 7184653835](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7184653835) and [#601 decision 7211787887](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/601#issuecomment-7211787887). Point 1: the label approves the plan revision whose newest fresh check states "Blocking findings: none" and whose open questions read None, and the control session records that approval with revision and commit. Point 2: every open question goes to Hari before approval, including the equivalence-check cadence, any job-capacity change, any change to the 4,200-second limit, any threshold, test-set or gate change, and the #598 sizing. Point 3: Claude Code implements on `claude-sonnet-5-5`; protected-path merges stay with Hari.
4. [#602 decision 7224014847](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/602#issuecomment-7224014847), 2026-11-27, answering [handoff 7223958923](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/602#issuecomment-7223958923). Point 1 adopts the campaign completeness conditions (R30). Point 2 accepts by name the assumption that kept tests do not depend on excluded tests running first, and requires a mutant-by-mutant comparison of the first acceptance campaign after "Fast suite" merges with the proof campaign (R33). Point 3 authorizes one paired campaign for the post-601 kill sets, doubling as the 601 acceptance run (D1). Point 4 accepts the 12 measured methods, and lets R31 shorten the list without coming back to Hari. Point 5 sets the cadence: daily on a full-suite campaign of `main`, plus whenever a pull request changes an excluded test; publication runs use the fast suite; a failing daily check returns the named tests before the next publication; the full-suite runner mode is required; the daily run is a control-session dispatch from a scheduled task. Point 6 authorizes the sizing diagnostic after "Fast suite" merges (D3). Point 7 puts Timeout mutants outside the proof, listed by name. Point 8 adopts the check's P2-A and P2-B and the handoff's four plan-text fixes. It authorizes this revision and one fresh check; a clean result approves it under decision 7223314573.

5. [#602 decision 7234844190](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/602#issuecomment-7234844190), 2026-11-28, answering [handoff 7224319601](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/602#issuecomment-7224319601) on r3. Point 1 chooses Q6 option (a), pinned exact hash pairs, with the larger scope of [check 7224304814](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/602#issuecomment-7224304814) finding 2, and section 15 takes the check's literal text (R19a). Point 2 allows the `suite` dispatch input under hard safety rule 8 (R20). Point 3 adopts finding 4 (a), a two-day bound on failed or incomplete daily checks (R21 item 4), and (b), a CI stage for the full-suite configuration (R21b). Point 4 adopts finding 5: R30 counts per shard the artifact the stored `campaign.json` names, and recovered campaigns depend on issue #604 (R30). Point 5 adopts the check's P2 tests (section 7). It authorizes this revision and one fresh check; a clean result approves it under decision 7223314573.

6. [#602 decision 7234963202](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/602#issuecomment-7234963202), 2026-11-28, answering [handoff 7234950283](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/602#issuecomment-7234950283). Point 1: this revision applies [check 7234941820](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/602#issuecomment-7234941820) findings 1 and 2 and P2-1 with the check's literal text; P2-2 is `apply in implementation`. Point 2: "Full-suite mode" starts with an early proof step (R19b). Point 3: one fresh check follows, and later rounds follow the [standing decision 7234963031 on #499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-7234963031), which that point links and adopts: a blocking finding with literal fix text inside Hari's earlier decisions on this issue may be applied in the next revision with one fresh check, and the control session records each such authorization.

Revision r6 applies the two blocking findings of [check 7235031591](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/602#issuecomment-7235031591) with the check's literal text, under that standing decision. Finding 2 narrows PR 2 inside decision 7234844190 point 1. Finding 1 reads decision 7234963202 point 2 as `renew` on a copy of the committed qualification file followed by `check` on that copy, because a `check` against the unrenewed file is always rejected; the control session judges whether that reading stays inside the decision, and if it does not, R19b goes to Hari.

The open questions of this revision read None (section 15).

Context, read as evidence and not as authority: [#600 findings 7201011493](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/600#issuecomment-7201011493); [#599 decision 7193808224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/599#issuecomment-7193808224) point 2; the #601 plan and its runbook record; plan checks [7223695785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/602#issuecomment-7223695785) and [7223928211](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/602#issuecomment-7223928211).

There is no item plan and no parent plan. This is a mutation-harness issue, and the issue body is the specification.

## 2. Scope

The set is named "Fast suite". It has three pull requests, in this order (factory.md "Item lifecycle" step 3 "Split by scope"). Each is reviewable and merges green on its own. The order follows from decision 7224014847 point 5: the daily full-suite check must exist when the fast suite becomes the default, so "Full-suite mode" merges before "Fast suite".

### PR 1 "Equivalence check", branch `claude/602-equivalence-check`

| File | Protected | Change |
|---|---|---|
| `tests/Parchwell.Verifier.Tests/Mutation/c5_equivalence.py` | yes (`tests/Parchwell.Verifier.Tests/**`) | new; R1 to R10 |
| `tests/Parchwell.Verifier.Tests/Mutation/test_c5_equivalence.py` | yes | new; section 7, PR 1 |
| `tests/Parchwell.Verifier.Tests/Mutation/c5-fast-suite-excluded.json` | yes | new; R11 |
| `docs/operations/verifier-mutation.md` | yes (`docs/`) | new section, R12 |

### PR 2 "Full-suite mode", branch `claude/602-full-suite-mode`

| File | Protected | Change |
|---|---|---|
| `tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj` | yes | R13 |
| `tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py` | yes | R14 to R16 |
| `tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py` | yes | R18, R19 |
| `tests/Parchwell.Verifier.Tests/Mutation/test_c5_workflow.py`, `test_c5_runtime.py` | yes | new tests only, section 7, PR 2 |
| `.github/workflows/verifier-c5-mutation.yml`, `.github/workflows/verifier-c5-mutation-companion.yml` | yes (hard safety rule 8) | R20 |
| `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` | yes | only as R19a requires |
| `scripts/verify-dotnet.sh` | no | R21b |
| `scripts/test-verify-dotnet.sh` | no | new cases and stub support only, R21b |
| `docs/operations/verifier-mutation.md` | yes | R21 |

Neither workflow has a committed copy under `docs/operations/workflows/` (reader-reported; that folder holds `verifier-c5-sizing.yml.txt`), so hard safety rule 8's byte-for-byte copy rule does not apply. The implementer confirms this by listing the folder.

### PR 3 "Fast suite", branch `claude/602-fast-suite`

| File | Protected | Change |
|---|---|---|
| The nine test files that hold the excluded methods (section 4) | yes | R22, R23 |
| New files under `tests/Parchwell.Verifier.Tests/Component/MutationExcluded/` | yes | R22 |
| `tests/Parchwell.Verifier.Tests/Mutation/c5-fast-suite-excluded.json` | yes | only as R31 requires |
| `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` | yes | R24, written only by the `renew` command |
| `docs/operations/verifier-mutation.md` | yes | R25 |

Paths that must not change in any of the three: everything under `src/`; every file under `tests/` not listed for that pull request; `tests/Parchwell.Verifier.Tests/stryker-config.json`; `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`, `c5_projection.py`, `c5_companion_artifacts.py` and `c5_campaign.py`; every workflow not listed; `.config/dotnet-tools.json`; `scripts/**` except the two files PR 2 lists; `results/**`; every `scenarios/*/expected.yaml`; AGENTS.md, CLAUDE.md and `docs/` outside `docs/operations/verifier-mutation.md`.

Out of scope for all three: any threshold or gate change; any change to the shard layout, the matrices' shard lists, `CAMPAIGN_LIMITS` (178 and 146), the 4,200-second `BUDGET` or `budget_seconds`; any change to a workflow's trigger events; any assertion, data row or expected value; any hosted dispatch by an implementing session; the scheduled task itself, which is the control session's.

Why the moved methods go under `Component/`: the mutation-only `Compile Remove` already drops `Component/**/*.cs` (csproj lines 20 and 21), and `c5_runtime.py` pins that list byte for byte (`REMOVE`, line 348; checked at line 471; any other string raises "unsupported Compile inclusion", line 479). Changing the list would change `c5_runtime.py`'s view of historical proof trees, which it re-reads at pinned commits (lines 1063 to 1071). A subfolder under `Component/` keeps `REMOVE` unchanged. The cost is a misleading folder name; R22 requires a one-line comment in each moved file saying why it sits there.

## 3. External contracts

- **Report fields the script reads.** Stryker's JSON report (`stryker/reports/mutation-report.json` in each shard artifact, `c5_workflow.py` line 327, and `isolated/<ID>/stryker/reports/mutation-report.json`, lines 1035 to 1041) has top-level `files` and `testFiles`. Each mutant has `id`, `mutatorName`, `replacement`, `location` (`start` and `end`, each `line` and `column`), `status`, `static` and, when Killed, `killedBy`, a list of test ids. `testFiles[<path>].tests[*]` has `id` and `name`. A name is `<Namespace>.<Type>.<Method>(<parameter types>)`, for example `Parchwell.Verifier.Tests.A01.A01C3EvidenceRecorderTests.Given_UnpassedStatisticsPrerequisite_When_C3Recorded_Then_OmitsWindowsAndProbe(System.Int32,System.String)`. File keys are absolute paths in a temporary checkout, such as `/tmp/c5-source-zl812_io/repository/src/Parchwell.Verifier/Judge/B10SweepChecks.cs`. Source: recorded output, artifact `c5-mutation-45555996117-1-b10-sweep-judge-p1` of accepted run 45555996117 at `e3c8eb94`, downloaded and read by script by this planning session on 2026-11-27: 8443 tests with 8443 distinct ids; 43 Killed mutants, each with a non-empty `killedBy` whose every id is in `testFiles`.
- **Excluded-test list.** `c5-fast-suite-excluded.json` is a JSON array of strings, each `<Namespace>.<Type>.<Method>` with no parameter list, sorted, no duplicates.
- **Script CLI.** Two subcommands, every option required:
  - `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_equivalence.py prove --reports-dir DIR --excluded FILE --output FILE`
  - `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_equivalence.py compare --before DIR --after DIR --excluded FILE --output FILE`
- **`prove` output JSON**, sorted keys, trailing newline:

  ```
  {"schema": 1, "status": "equivalent" | "not_equivalent", "reports": <int>, "killed": <int>, "excluded": <int>, "lost": [<lost entry>, ...], "timeouts": [<timeout entry>, ...]}
  ```

  A lost entry is `{"file": <normalized path>, "mutant_ids": [<ids, sorted as strings>], "mutator": <mutatorName>, "replacement": <replacement>, "location": <location object>, "static": <bool>, "killed_by": [<method keys, sorted>]}`. A timeout entry has the same keys without `killed_by`.
- **`compare` output JSON**, sorted keys, trailing newline:

  ```
  {"schema": 1, "status": "no_regressions" | "regressions", "before_reports": <int>, "after_reports": <int>, "regressions": [<regression entry>, ...]}
  ```

  A regression entry is `{"file", "mutant_ids", "mutator", "replacement", "location", "static"}` as above, plus `"before_killed_by": [<method keys, sorted>]`, `"after_statuses": [<distinct statuses, sorted>]` and `"excluded_killers": [<the before_killed_by keys that are in the excluded list, sorted>]`.
- **Sort order** of `lost`, `timeouts` and `regressions`: by `file`, then `location.start.line`, `location.start.column`, then `mutator`.
- **Failure names**, printed as `c5_equivalence: <name>: <detail>` on stderr with exit status 2 and no output file: `no_reports`, `invalid_report`, `invalid_excluded`, `killed_without_tests`, `unmapped_test_id`, `unmatched_excluded` (`prove` only), `identity_missing` and `identity_unscored` (`compare` only).
- **Exit status.** 0 for `equivalent` or `no_regressions`; 1 for `not_equivalent` or `regressions`; 2 for a failure name.
- **Full-suite mode** (PR 2): the MSBuild property `ParchwellVerifierMutationFullSuite`, the csproj item group of R13, the `--suite` option of R14, the `suite` key of R15, and the workflow input of R20.
- **Qualification basis text** (PR 3), R24.
- **Qualification basis text** (PR 2), R19a. `<BASIS>` is exactly: `Issue 602 adds a full-suite mode to the C5 runner and both mutation workflows, accepted through pinned exact hash pairs by https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/602#issuecomment-7234844190 point 1. Default-mode shard execution is byte-identical to the base commit for every shard (test_fast_shard_command_matches_base). Full-suite mode builds the base commit's test set on the base layout while Component/MutationExcluded holds no file. The test csproj gains one item group whose condition is false in default mode.`
- **Runbook sections** (R12, R21, R25), read by later sessions and the control session as the record and the daily procedure.

Kafka, librdkafka, Confluent.Kafka and Event Hubs behaviour: none is relied on.

Stryker behaviour relied on, checked by this session on 2026-11-27 by reading the source at [`6e77a345`](https://github.com/stryker-mutator/stryker-net/tree/6e77a3451bac4793e9c839c3ff4055c1b30ca3af) (tag `dotnet-stryker@5.0.0`) with `curl` from raw.githubusercontent.com:

- The MTP runner runs each assembly's selected tests to completion for a mutant and then collects every failed test: `RunAssemblyTestsAsync` in a loop over assemblies, then `var failedTestIds = accumulator.BuildFailedTests();` and `update.Invoke(mutants, failedTestIds, executedTests, timedOutTestIds);` ([MicrosoftTestingPlatformRunner.cs L1071-L1105](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.TestRunner.MicrosoftTestPlatform/MicrosoftTestingPlatformRunner.cs#L1071-L1105)). Inferred, not stated there: `killedBy` holds every test that failed under the mutant, not only the first. The #600 data agree: `killedBy` lengths had median 9 and range 1 to 223 over 9773 kills.
- With coverage `off`, every test runs for every mutant (#600 setup, `stryker-c5-config.json`). So a mutant killed only by excluded tests is the only way the fast suite can lose a Killed status at the commit the campaign tested, given the accepted assumption of decision 7224014847 point 2. Inferred.

MSBuild behaviour relied on (PR 2), checked with the Microsoft Learn documentation search tool on 2026-11-27: "Items are defined and modified in the order in which they appear" during evaluation ([Compare properties and items, "Property and item evaluation order"](https://learn.microsoft.com/visualstudio/msbuild/comparing-properties-and-items#property-and-item-evaluation-order)). Inferred, not stated there: a `Compile Include` placed after the mutation-only `Compile Remove` adds its files back. R13's acceptance step C2 checks it by test list.

## 4. The excluded methods

Measured by this planning session on 2026-11-27 at the base commit: Release build with `-p:ParchwellVerifierMutationOnly=true -p:RestoreLockedMode=true`, 4-core container, idle, fresh process per run, two runs, `dotnet exec tests/Parchwell.Verifier.Tests/bin/Release/net10.0/Parchwell.Verifier.Tests.dll -noColor -xml <file>` with `DOTNET_ROOT` set. Each run: 8443 tests, 0 failed; wall 83.17 s and 81.91 s; summed test time 221.40 s and 210.26 s. Durations were summed per method by script.

Selection rule: every test method with at least one case of 1.0 s or more in both runs. That gives 12 methods with 22 cases, 133.5 s and 126.4 s summed, 60.3 and 60.1 percent of the summed time. One method crossed 1.0 s in one run only and is not selected: `Parchwell.Verifier.Tests.Judge.ConsumerPhaseJudgeTests.Given_ConsumerCandidateWithAMismatchedPair_When_Read_Then_Rejects`, 1.088 s and 0.811 s. It is the class whose kills issue 601 just corrected, so keeping it in the mutation suite is the safer side. Hari accepted this list in place of #600's 14 (decision 7224014847 point 4).

| Method | Cases | Summed s, run 1 / run 2 | File |
|---|---:|---:|---|
| `Parchwell.Verifier.Tests.Host.B10SweepHandlerTests.Given_MaximumAdmittedPhaseVolumeInFinalTail_When_Swept_Then_DeploymentLimitsFitAndActualSubLimitsFail` | 6 | 61.94 / 61.77 | `Host/B10SweepHandlerTests.cs` |
| `Parchwell.Verifier.Tests.Host.VerifyInputCapacityTests.Given_FullA01AttemptInventory_When_UsingDeployedInputCapacity_Then_PublishesExactCompleteCounts` | 1 | 38.89 / 33.69 | `Host/VerifyInputCapacityTests.cs` |
| `Parchwell.Verifier.Tests.Consumer.ConsumerArtifactTests.Given_RecordBlobRowBoundaries_U4_When_Loaded_Then_EnforcesRollover` | 5 | 7.13 / 7.00 | `Consumer/ConsumerArtifactTests.cs` |
| `Parchwell.Verifier.Tests.Authentication.VerifierKafkaTokenProviderRetryTests.Given_EveryPostBlocks_When_TokenRequested_Then_FailsAfterTwoPosts` | 1 | 6.29 / 6.23 | `Authentication/VerifierKafkaTokenProviderRetryTests.cs` |
| `Parchwell.Verifier.Tests.ProbeGate.ProbeGateHostTests.Given_SynchronousBlockedGateRead_When_WatchdogExpires_Then_ProductionHostExits124` | 1 | 3.71 / 3.73 | `ProbeGate/ProbeGateHostTests.cs` |
| `Parchwell.Verifier.Tests.Window.ConsumedMetadataCaptureTests.ConsumedMetadata_JsonEscapingAndDescriptorCountAreBounded` | 1 | 2.97 / 1.64 | `Window/ConsumedMetadataCaptureTests.cs` |
| `Parchwell.Verifier.Tests.Consumer.ConsumerRecordNameTests.Given_UnsafeRecordName_U4_When_ExtensionReads_Then_PreservesInvalidProjectionAndSingleOffsetRead` | 2 | 2.72 / 2.29 | `Consumer/ConsumerRecordNameTests.cs` |
| `Parchwell.Verifier.Tests.Authentication.VerifierKafkaTokenProviderRetryTests.Given_CallerCancelsDuringFirstPost_When_TokenRequested_Then_NoSecondPostIsSent` | 1 | 2.58 / 2.56 | `Authentication/VerifierKafkaTokenProviderRetryTests.cs` |
| `Parchwell.Verifier.Tests.Authentication.VerifierKafkaTokenProviderRetryTests.Given_EveryPostAnswers503_When_TokenRequested_Then_FailsAfterTwoPosts` | 1 | 2.42 / 2.40 | `Authentication/VerifierKafkaTokenProviderRetryTests.cs` |
| `Parchwell.Verifier.Tests.Authentication.VerifierKafkaTokenProviderRetryTests.Given_FirstPostBlocks_When_TokenRequested_Then_OneRetrySucceeds` | 1 | 2.19 / 2.30 | `Authentication/VerifierKafkaTokenProviderRetryTests.cs` |
| `Parchwell.Verifier.Tests.Window.ConsumedRecordBudgetTests.Given_DeployedPhaseRecordLimit_When_AccountingEmptyRecords_Then_EqualityPassesAndNextRecordCannotAdvanceCounters` | 1 | 1.45 / 1.52 | `Window/ConsumedRecordBudgetTests.cs` |
| `Parchwell.Verifier.Tests.SingleAction.ProbeV2StatisticsBudgetTests.Given_RealNativeThirtyTwoPartitionSixtyFourBrokerReport_When_Judged_Then_OldBudgetFailsAndRepairedBudgetPreservesHash` | 1 | 1.19 / 1.31 | `SingleAction/ProbeV2StatisticsBudgetTests.cs` |

All file paths are under `tests/Parchwell.Verifier.Tests/`. Each class is `public sealed class` in a file-scoped namespace at the base commit.

## 5. Requirements

### PR 1 "Equivalence check"

R1. WHEN `prove` runs THEN it reads every file named `mutation-report.json` found recursively under `--reports-dir`, in sorted path order; `compare` does the same under `--before` and under `--after`. WHEN a directory holds none THEN the script fails with `no_reports`. WHEN a file is not a JSON object with object `files` and object `testFiles`, or a mutant lacks `id`, `mutatorName`, `replacement`, `location`, `status` or `static` THEN it fails with `invalid_report`, naming the file.

R2. WHEN the excluded file is not a JSON array of strings, is not sorted, has a duplicate, has an empty string, or has an entry containing `(` THEN the script fails with `invalid_excluded`.

R3. WHEN it reads a report THEN it maps each test id to its name through `testFiles[*].tests[*]`, and a test's method key is the name up to its first `(`, or the whole name when there is none. WHEN a Killed mutant has a missing or empty `killedBy` THEN it fails with `killed_without_tests`. WHEN a `killedBy` id is not in that report's `testFiles` THEN it fails with `unmapped_test_id`.

R4. WHEN it reads a file key THEN the normalized path is the text after the first `/repository/` segment. WHEN a key has no such segment THEN it fails with `invalid_report`. A mutant's identity is (normalized path, `mutatorName`, `replacement`, `location.start.line`, `location.start.column`, `location.end.line`, `location.end.column`, `static`), the #599 and #600 comparison key.

R5. `prove` judges each Killed row on its own. WHEN any one Killed row of an identity has a `killedBy` whose method keys are all excluded keys THEN the identity is lost, whatever its other rows show. The same identity can have several Killed rows (a cohort's top-level report copies its bootstrap isolated report, `c5_workflow.py` line 892; reports of earlier run attempts), and the gate scores only one row per mutant (`reconcile`, lines 1462 to 1493), so no row may mask another. The lost entry's `mutant_ids` and `killed_by` come from the rows that make it lost. Exact string match only: an excluded key `A.B.Foo` does not match `A.B.FooBar`. `killed` counts distinct identities with at least one Killed row. `reports` counts files read. `excluded` counts list entries.

R6. WHEN an identity has a Timeout row and no Killed row THEN `prove` lists it under `timeouts`. The gate counts a Timeout as a detection (`c5_workflow.py` line 1458), and a Timeout row has no `killedBy`, so the script cannot tell whether only excluded tests caused it. Inferred, not stated by #600: removing tests can turn a Timeout into Survived. [#600 findings](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/600#issuecomment-7201011493), section 3, "Limits", says only that the one hosted Timeout has no killing-test list, so its exposure is not known. `timeouts` does not change `status`. Each listed identity is outside the proof (decision 7224014847 point 7). No other status appears in the output.

R7. WHEN an excluded method key matches no test name in any report `prove` read THEN it fails with `unmatched_excluded`, naming the key.

R8. WHEN no identity is lost THEN `prove`'s `status` is `equivalent` and the exit status is 0. WHEN at least one is lost THEN `status` is `not_equivalent`, each lost identity is listed once, and the exit status is 1.

R9. `compare` reads both directories and, for each identity with at least one Killed row in `--before`: WHEN it has no row at all in `--after` THEN the script fails with `identity_missing`; WHEN its `--after` rows include Killed or Timeout THEN it is not a regression; WHEN its `--after` rows include Survived or NoCoverage and neither Killed nor Timeout THEN it is a regression; WHEN its `--after` rows have only other statuses (Ignored, CompileError, RuntimeError) THEN the script fails with `identity_unscored`. `before_killed_by` is the union of method keys over the identity's Killed rows in `--before`; `excluded_killers` is its intersection with the excluded list. WHEN there is no regression THEN `status` is `no_regressions` and the exit status is 0; otherwise `regressions` and 1. Identities that are not Killed in `--before` are ignored.

R10. The script makes no network call and writes nothing but `--output`.

R11. WHEN PR 1 merges THEN `c5-fast-suite-excluded.json` holds exactly the 12 method keys of section 4, sorted, two-space indented, with a trailing newline.

R12. WHEN PR 1 merges THEN docs/operations/verifier-mutation.md has a section `## Issue 602 fast-suite equivalence check`, placed directly after "Issue 601 test-side cached state". It states: what `prove` shows and what it cannot (R5; the proof holds only for the commit the campaign tested; Timeout identities are outside it, R6; it rests on the assumption, accepted by name in decision 7224014847 point 2 and not checked, that a kept test's outcome does not depend on which other tests ran earlier in the same reused test process, the kind of dependence issue 601 found); this sentence, after the Timeout limit: "RuntimeError identities are outside the proof as well: the gate does not score them (`c5_workflow.py` lines 1457 to 1459), and the script does not list them. Inferred, not measured: removing a test that crashed the test host can turn such an identity into Survived, which the gate scores."; what `compare` shows (R9); the CLI, exit statuses and failure names of section 3; the download procedure of section 9, step A3; the campaign completeness check of R30; and the strongest counterargument of section 11, quoted in full. It changes no other section.

### PR 2 "Full-suite mode"

R13. WHEN PR 2 merges THEN the test csproj has, directly after the existing mutation-only item group (lines 20 to 22, unchanged), exactly this item group:

```
  <ItemGroup Condition="'$(ParchwellVerifierMutationOnly)' == 'true' and '$(ParchwellVerifierMutationFullSuite)' == 'true'">
    <Compile Include="Component/MutationExcluded/**/*.cs" />
  </ItemGroup>
```

WHEN both properties are `true` THEN the build compiles every file under `Component/MutationExcluded/` and nothing else that the mutation-only group removes. WHEN only `ParchwellVerifierMutationOnly` is `true` THEN the build is the mutation-only build of today. The normal build is unchanged.

R14. WHEN `c5_workflow.py run-shard` and `aggregate` are called THEN they accept `--suite` with exactly the values `fast` and `full`, default `fast`. WHEN the suite is `fast` THEN the environment overrides are exactly today's `OVERRIDES` (lines 329 to 332), and the shard's Stryker argv, working directory and generated configuration are byte-identical to the base commit's for the same shard. WHEN the suite is `full` THEN the overrides are today's plus `"ParchwellVerifierMutationFullSuite": "true"`, and nothing else differs.

R15. WHEN `aggregate` runs THEN it requires every receipt's `environment_overrides` to equal the overrides of its own `--suite` value, and fails as it does today for any other value (line 1582). WHEN it writes `summary.json` THEN the summary has a key `"suite"` whose value is the suite. No other summary key changes.

R16. WHEN the suite is `full` THEN no shard plan, cut, cohort, isolation plan or time limit differs from `fast`: only the test set differs. Inferred consequence: until "Fast suite" merges, `Component/MutationExcluded/` is empty and the two suites build the same tests.

R17. WHEN a paired campaign is prepared or checked THEN both runs used the same suite; a mixed pair fails the campaign. It is enforced through the aggregate's receipt check of R15, which reads every coordinator and companion receipt (`c5_workflow.py` lines 1544, 1545 and 1582). `c5_campaign.py` does not change: it is `NON_RENEWABLE` (`c5_runtime.py` line 344), R19a pins no pair for it, and an edited validator stops `input_maps` from recording `CAMPAIGN_VALIDATOR_BASE` (lines 535, 536 and 543).

R18. WHEN `c5_runtime.py` parses a test csproj THEN `project_closure` accepts the item group of R13, exactly as written, as well as its absence at historical commits; any other `Compile Include` still raises "unsupported Compile inclusion". `REMOVE` stays byte-for-byte as it is.

R19. WHEN `c5_runtime.py` builds the shared inputs THEN `.cs` files under `tests/Parchwell.Verifier.Tests/Component/MutationExcluded/` are shared inputs, because a full-suite campaign compiles them; every other path under `Component/` stays excluded (`EXCLUDED`, line 346). At PR 2's head the folder is empty, so the shared-input map at PR 2's head equals the base's except for the files PR 2 itself changes. R19a says how the qualification accepts PR 2's changes to `c5_workflow.py` and both workflows, which are `NON_RENEWABLE` (line 344).

R19a. Qualification route, pinned exact pairs (decision 7234844190 point 1). WHEN `c5_runtime.py check --base <merge base> --head <PR 2 head>` runs THEN it does not report `rejected`, because `c5_runtime.py` accepts exactly these (old, new) hash pairs as timing-equivalent, and no others: the base and PR 2 hashes of `c5_workflow.py`, `verifier-c5-mutation.yml` and `verifier-c5-mutation-companion.yml`, each as `input_maps` records them. The pairs are new constants; `RUNNER_TIMING_PAIR` (lines 335 and 336) keeps holding the issue 494 transition unchanged, because `proof_tree` (line 1067) and `legacy_runner_bridge` (lines 1104 to 1116) read it as a single legacy bridge. Each place that accepts a pair accepts the new ones too: `provenance_errors` (lines 1803 to 1806, called by `validate` at line 2387) and `renew_v2` (lines 2575 to 2577), and `same_timing_layout` (lines 1074 to 1080) treats two trees as matching when their only differences are pinned pairs: the masked runner code (line 1078) may differ only when the two trees' runner execution hashes are the pinned runner pair, and each non-renewable hash (line 1080) may differ only by its pinned pair. `RUNNER_EXECUTION_PIN` (line 343, checked at line 543) is re-pinned to the new runner's execution hash, so the recorded `c5_campaign.py` hash (lines 535 and 536) stays as today. The workflow pairs are needed because `execution_workflow` keeps the `on:` block (lines 386 to 390), so adding an input changes both workflows' execution hashes. The basis recorded with the pairs is: default-mode execution is byte-identical (R14, tested for every shard), and full-suite mode builds today's test set on today's layout (R16), so today's timing evidence bounds both. WHEN a later commit in the PR changes any of the three files THEN the pair is recomputed for that commit. WHEN the head's hash of any of the three files is not its pinned new hash THEN `check` rejects as it does today. No hosted dispatch happens before merge. WHEN PR 2 is opened THEN `c5-runtime-qualification.json` has been renewed, as a separate commit after the last change to the three pinned files and the csproj, with `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py renew --head <HEAD_SHA> --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json --basis "<BASIS>"`, where `<HEAD_SHA>` is the commit that holds that last change, and `<BASIS>` is exactly the PR 2 basis text of section 3.

R19b. Early proof step (decision 7234963202 point 2). WHEN the implementing session starts "Full-suite mode" THEN its first work is a prototype of R13, R14, R18, R19a and R20 on its branch. It then runs, locally and before any other PR 2 work, the `renew` command of R19a, with `<HEAD_SHA>` the prototype commit, on a scratch copy of the committed qualification file, followed by `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base 15b09147ed4e7c62ea08620b24c14cd049f07562 --head <prototype commit> --qualification <scratch copy>`. A `check` against the unrenewed committed file would always report `rejected` (`stale_inputs`, `c5_runtime.py` lines 2255 and 2256), so it is not run. WHEN `renew` refuses or `check` reports `rejected` THEN the session stops and hands off under AGENTS.md GOVERNANCE "Handoffs" with the full output. It dispatches nothing. The PR records the commands and outputs.

R20. WHEN PR 2 merges THEN both `verifier-c5-mutation.yml` and `verifier-c5-mutation-companion.yml` have a `workflow_dispatch` input named `suite`, `type: choice`, `options: [fast, full]`, `default: fast`, `required: true`, passed as `--suite` to every `c5_workflow.py run-shard` and `aggregate` call. The trigger events stay exactly `workflow_dispatch`; no `schedule`, `push`, `pull_request` or `pull_request_target` is added. Adding the input is allowed under hard safety rule 8 by decision 7234844190 point 2; the coordinator's existing `companion_run_id` and `companion_run_attempt` inputs (lines 5 to 13) are the precedent.

R21. WHEN PR 2 merges THEN the runbook section of R12 gains `### Daily full-suite check`, the procedure the control session follows. It states, in this order:
1. Once a day, and after the merge of any pull request that changes a file under `Component/MutationExcluded/` or `c5-fast-suite-excluded.json`, the control session dispatches one paired campaign on `main` with `suite: full`, from its scheduled task (decision 7224014847 point 5). It does not dispatch while another paired campaign is running.
2. It applies the completeness check of R30 and runs `prove` with the committed list. It records the run ids, the commit, the R30 values and the output on #602 or the issue the control session names.
3. WHEN `prove` reports `not_equivalent` THEN every excluded method in a lost entry's `killed_by` returns to the mutation suite: a pull request moves it out of `Component/MutationExcluded/` and removes it from the list, and that pull request merges before the next publication's acceptance run. It is a protected change; Hari merges.
4. WHEN a campaign fails R30 or the script fails with a failure name THEN the day's check is incomplete; the control session reports it and dispatches again the next day. WHEN two consecutive daily checks are each failed (`not_equivalent`, or a failure name) or incomplete THEN the control session holds publication and hands off to Hari with the reasons and evidence of both days (decision 7234844190 point 3 (a)). Publication stays held until Hari decides.
5. Publication acceptance runs use `suite: fast` (decision 7224014847 point 5).
6. The runner time per full-suite campaign, as an estimate: about 530743 s (the accepted pre-601 campaign, #600 findings section 4, measured on that campaign; the post-601 figure is not measured).

R21b. Full-suite CI stage (decision 7234844190 point 3 (b)). WHEN `scripts/verify-dotnet.sh` runs the Verifier tests in a full run or the Verifier `rest` leg THEN, after the existing `mutation_only_build` and `mutation_only_test` stages (lines 471 to 496, unchanged), it runs two new stages labelled exactly `Full-suite mutation Verifier test build` and `Full-suite mutation Verifier tests`. They are the same commands as the mutation-only stages with `-p:ParchwellVerifierMutationFullSuite=true` added, the build to its own artifacts directory as `mutation_only_build` does, and the tests in the repository's own output directories with the same tier filter as `mutation_only_test`. The test stage does not run after a failed full-suite build. A failure of either stage fails the run, as the mutation-only stages do (line 598). Every other leg skips both, as the mutation-only stages are skipped. `scripts/test-verify-dotnet.sh` keeps every existing assertion, M1 to M10 included, unchanged. The stub routes the full-suite commands before its mutation-only match, as check 7234941820 P2-2 gives (`apply in implementation`, decision 7234963202 point 1).

### PR 3 "Fast suite"

R22. WHEN PR 3 merges THEN each method listed in `c5-fast-suite-excluded.json` is declared in a file under `tests/Parchwell.Verifier.Tests/Component/MutationExcluded/`, in the same namespace and class as at the base commit, so its fully qualified name and every data row are unchanged. A class that keeps other tests becomes `partial` in both files; a class whose every test moves may move whole. Each new file starts with a comment that says it sits under `Component/` only so the default mutation-only build drops it, and links the runbook section of R12. Helpers move with the tests only where the mutation-only build needs it to compile without warnings (warnings are errors, `Directory.Build.props` line 6).

R23. WHEN PR 3 merges THEN no other test method moves, and no test body, attribute, data row or assertion changes. The normal build compiles and runs every moved test (csproj line 20), and the full-suite mutation build compiles them too (R13).

R24. WHEN PR 3 is opened THEN `c5-runtime-qualification.json` has been renewed, as a separate commit after the test move, with `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py renew --head <HEAD_SHA> --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json --basis "<BASIS>"`, where `<HEAD_SHA>` is the commit that holds the move, and `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base <merge base> --head <HEAD> --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` does not report `rejected`. `<BASIS>` is exactly:

  `Issue 602 moves <N> slow test methods out of the default mutation-only build into Component/MutationExcluded, which only the full-suite mode compiles; no target, runner or workflow changed. In the fast suite, removing tests can only shorten each mutant's test run; the full suite builds the same tests as before the move. Local mutation-only suite wall, 4-core container, idle, fresh process per run, 2 runs each on <DATE>: <B1> s and <B2> s before, <A1> s and <A2> s after. Equivalence check on campaign <RUN_ID> at <SHA8>: equivalent; Timeout identities outside the proof accepted by https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/602#issuecomment-7224014847.`

  The placeholders are filled from step B2 and the B1 run, with `<N>` the number of methods moved. "Can only shorten" is an inference, not measured per shard: fewer tests run per mutant, and the Stryker initial run and process start are unchanged. The issue 601 renewal (commit `d4dfe39`, reader-reported) is the precedent for renewing changed test inputs with a basis. If the tool refuses the renewal, the session stops and hands off with its output.

R25. WHEN PR 3 merges THEN the runbook section of R12 gains `### Fast suite record`, stating: the moved methods, from the final list; the measurements of section 4 and of step B2; the proof result with the run ids, commit and R30 values; each Timeout identity outside the proof, by name; any method kept under R31; that default campaigns now run the fast suite and the daily check runs the full suite (R21); that the comparison of R33 is pending until it runs; and that the shard layout is unchanged until D3.

### Gates and checks that bind sessions after merge

R30. Campaign completeness (decision 7224014847 point 1). A campaign counts as input to `prove` or `compare` only WHEN all of these hold, and the session records each value: the main run's `summary.json` has `measurement_complete` true; both runs' `head_sha` is the campaign commit; for each shard in the `shards` map of the `campaign.json` stored with the aggregate (`c5_workflow.py` line 1542), the artifact `c5-mutation-<run_id>-<run_attempt>-<shard>`, with the run id and attempt that map names, exists and holds a `mutation-report.json`; and the map has exactly the shards of both matrices at the campaign commit. For a full-suite campaign, the summary's `suite` is `full`. Downloads under A3 and B1 may still include every attempt's artifacts; R5 already treats extra rows conservatively. Dependency: a campaign that needed lost-shard recovery cannot complete until issue #604 is fixed, because recovery fails today at the jobs API (decision 7234844190 point 4); such a campaign fails R30 and counts as incomplete under R21 item 4.

R31. Proof gate for PR 3. WHEN PR 3's pull request is opened THEN `prove` has run, under step B1, on a post-601 campaign that meets R30, whose commit contains PR 2's merge, and whose commit differs from PR 3's base only in paths under `docs/`, `items/` or `results/`. Its output is recorded in the pull request. WHEN any other path differs (for example under `src/`, `tests/`, `scenarios/`, `.config/` or `.github/`, or a root build file) THEN that campaign does not count. WHEN no such campaign exists THEN the session stops and hands off; it does not dispatch one. WHEN `prove` reports `not_equivalent` THEN every excluded method in a lost entry's `killed_by` stays in the mutation suite: PR 3 removes it from the list and does not move it, and `prove` runs again on the same reports with the shortened list, which must then report `equivalent`. This needs no further decision (decision 7224014847 point 4). WHEN the list would become empty THEN the session stops and hands off. The pull request lists each kept method with the lost identities that kept it. WHEN `prove` fails with a failure name THEN the session stops and hands off with the message.

R32. Proof campaign. The campaign of R31 is the one Hari authorized (decision 7224014847 point 3), dispatched by the control session on `main` after PR 2 merges, with `suite: full`. It also serves as the 601 acceptance run. Requiring PR 2's merge first follows from R33: the comparison allows no path difference outside `docs/`, `items/`, `results/` and PR 3's files, so the proof campaign must already contain PR 2.

R33. First fast-suite comparison (decision 7224014847 point 2). WHEN the first acceptance campaign with `suite: fast` after PR 3 merges completes and meets R30, and its commit differs from the proof campaign's commit only in paths under `docs/`, `items/`, `results/` and the files PR 3 changed, THEN the control session runs `compare --before <proof reports> --after <fast reports> --excluded <committed list>` and records the output. WHEN a regression has non-empty `excluded_killers` THEN those methods return to the mutation suite as in R21 item 3. WHEN a regression has empty `excluded_killers` THEN it is a finding for Hari: the fast suite lost a kill that only kept tests made, which the accepted assumption said would not happen. WHEN other paths differ THEN the comparison waits for a later fast-suite campaign that meets the condition against a full-suite campaign at the same commit.

## 6. Invariants and safety rules

- No assertion is loosened, deleted or skipped (AGENTS.md "Code Review Rules"). Moving a test out of the default mutation build is not deleting it: it still runs in normal CI and in the full-suite mutation build (R23).
- The 31 targets, the MTP runner and coverage `off` stay (issue design item 1).
- No file under `results/` and no `expected.yaml` changes. The mutation score is not edited. A mutant that turns Killed to Survived is a finding, not a reason to change a threshold.
- No workflow trigger event changes (hard safety rule 8). Dispatches are against `main` only, and only by the control session under decision 7224014847 points 3, 5 and 6. Implementing sessions dispatch nothing.
- Default-mode campaigns stay byte-identical to today's in execution (R14), so mutation acceptance under factory.md "Evidence pack" keeps its meaning.
- The protected paths of section 2 reach `main` only through Hari's merge.
- No number is published that was not measured (hard safety rule 11). Every estimate says "estimate", its basis and its date.

Standing fixture rule. PR 1 and PR 2 add checks: the script, the `--suite` validation, the summary `suite` key and the csproj parser. If an existing test's fixture is rejected by a new check, the implementer does exactly one of: (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it, and the cold review and Codex review check each one. Modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.

## 7. Test map

### PR 1, `test_c5_equivalence.py`

The file name matches `test_c5_*.py`, so `.NET verification` runs it (`.github/workflows/dotnet-verification.yml` lines 139 to 147). Each test writes reports into a `TemporaryDirectory` and calls the script's entry point with arguments. A fixture report has file key `/tmp/x/repository/src/Parchwell.Verifier/A.cs`, tests `t1` named `N.C.Keep(System.Int32)`, `t2` named `N.C.Slow`, `t3` named `N.C.SlowCase(System.String)` and `t4` named `N.C.SlowBar`, and an excluded list `["N.C.Slow", "N.C.SlowCase"]`. Unless a row says `compare`, the test calls `prove`.

| Test | Requirement | Input | Expected |
|---|---|---|---|
| `test_killed_by_kept_and_excluded_is_equivalent` | R5, R8 | mutant `1` Killed by `[t1, t2]` | exit 0, `status` `equivalent`, `killed` 1, `lost` [] |
| `test_killed_only_by_excluded_is_lost` | R5, R8 | mutant `1` Killed by `[t2, t3]` | exit 1, `not_equivalent`, one lost entry with `file` `src/Parchwell.Verifier/A.cs`, `mutant_ids` `["1"]`, `killed_by` `["N.C.Slow", "N.C.SlowCase"]`; the entry has exactly the keys `file`, `mutant_ids`, `mutator`, `replacement`, `location`, `static`, `killed_by`, and `mutator`, `replacement`, `location` and `static` equal the fixture mutant's `mutatorName`, `replacement`, `location` and `static` |
| `test_prefix_name_is_not_excluded` | R5 | mutant Killed by `[t4]` | exit 0 |
| `test_duplicate_row_cannot_mask_a_lost_row` | R5 | the same identity, id `1` in report A Killed by `[t2]` and id `7` in report B Killed by `[t1]` | exit 1, one lost entry, `mutant_ids` `["1"]`, `killed_by` `["N.C.Slow"]`, `killed` 1 |
| `test_duplicate_rows_both_kept` | R5 | the same identity in reports A and B, both Killed by `[t1, t2]` | exit 0, `killed` 1 |
| `test_duplicate_reports_both_excluded` | R5, R8 | the same identity, id `1` in A and id `7` in B, both Killed by `[t2]` | exit 1, one lost entry, `mutant_ids` `["1", "7"]` |
| `test_mutant_ids_sort_as_strings` | section 3 | the same identity, id `10` in report A and id `9` in report B, both Killed by `[t2]` | exit 1, one lost entry, `mutant_ids` `["10", "9"]` |
| `test_static_flag_separates_identities` | R4 | two mutants equal except `static` true and false, one Killed by `[t1]`, one by `[t2]` | exit 1, one lost entry, with the `static` value of the one Killed by `[t2]` |
| `test_other_statuses_are_not_lost` | R5, R6 | mutants with Survived, Timeout, Ignored, CompileError and no `killedBy` | exit 0, `killed` 0, `lost` [], `timeouts` one entry, for the Timeout mutant, with exactly the keys of a lost entry without `killed_by` |
| `test_timeout_with_a_killed_row_is_not_listed` | R6 | the same identity Timeout in report A and Killed by `[t1]` in report B | exit 0, `timeouts` [] |
| `test_zero_and_one_lost_boundary` | R8 | two mutants, Killed by `[t1]` and by `[t2]` | exit 1, exactly one lost entry |
| `test_counts_and_schema` | R1, R5 | reports at `a/b/mutation-report.json` and `c/mutation-report.json` under `--reports-dir`, each with the same identity, id `1`, Killed by `[t1]` | exit 0, `schema` 1, `reports` 2, `killed` 1, `excluded` 2, `lost` [], `timeouts` [] |
| `test_no_reports` | R1 | empty directory | exit 2, stderr starts `c5_equivalence: no_reports:` |
| `test_invalid_report` | R1 | report without `testFiles`; mutant without `static` | exit 2, `invalid_report`, each case |
| `test_invalid_report_shapes` | R1 | each case on its own: a report whose content is `[]`; a report without `files`; a report whose `files` is `[]`; a mutant without `id`; without `mutatorName`; without `replacement`; without `location`; without `status` | exit 2, stderr starts `c5_equivalence: invalid_report:` and contains the report's path, no output file, each case |
| `test_file_key_without_repository_segment` | R4 | file key `/tmp/x/src/A.cs` | exit 2, `invalid_report` |
| `test_first_repository_segment` | R4 | file key `/tmp/x/repository/src/repository/A.cs`, a mutant Killed by `[t2]` | exit 1, lost entry `file` `src/repository/A.cs` |
| `test_invalid_excluded` | R2 | not an array; unsorted; duplicate; empty string; entry `N.C.Slow(System.Int32)`; `["N.C.Slow", 1]` | exit 2, `invalid_excluded`, each case |
| `test_killed_without_tests` | R3 | Killed with `killedBy` [] and with no `killedBy` | exit 2, `killed_without_tests`, each case |
| `test_unmapped_test_id` | R3 | Killed by `[t9]` | exit 2, `unmapped_test_id` |
| `test_unmatched_excluded` | R7 | excluded list `["N.C.Gone", "N.C.Slow"]` | exit 2, `unmatched_excluded`, detail names `N.C.Gone` |
| `test_output_is_sorted_and_stable` | section 3 | two lost identities in reverse line order | `lost` sorted by file and line; output bytes identical over two runs; trailing newline |
| `test_lost_sort_tie_breaks` | section 3 | a second file key `/tmp/x/repository/src/Parchwell.Verifier/B.cs`; four mutants, each Killed by `[t2]`, given in this order: `B.cs` start 1:1 mutator `M`; `A.cs` start 5:9 mutator `M`; `A.cs` start 5:3 mutator `Z`; `A.cs` start 5:3 mutator `B` | exit 1, `lost` in the order `A.cs` 5:3 `B`, `A.cs` 5:3 `Z`, `A.cs` 5:9 `M`, `B.cs` 1:1 `M` |
| `test_compare_no_regression` | R9 | `compare`; before: id `1` Killed by `[t1]`; after: the same identity Killed by `[t1]`, and a second after report with it Ignored | exit 0, `no_regressions`, `regressions` [], `before_reports` 1, `after_reports` 2 |
| `test_compare_timeout_is_not_a_regression` | R9 | before Killed by `[t2]`; after Timeout | exit 0 |
| `test_compare_survived_is_a_regression` | R9 | before Killed by `[t1, t2]`; after Survived | exit 1, one regression, `before_killed_by` `["N.C.Keep", "N.C.Slow"]`, `after_statuses` `["Survived"]`, `excluded_killers` `["N.C.Slow"]` |
| `test_compare_regression_with_no_excluded_killer` | R9 | before Killed by `[t1]`; after NoCoverage | exit 1, one regression, `excluded_killers` [] |
| `test_compare_identity_missing` | R9 | before Killed by `[t1]`; after has no row for it | exit 2, `identity_missing` |
| `test_compare_identity_unscored` | R9 | before Killed by `[t1]`; after only Ignored and CompileError | exit 2, `identity_unscored` |
| `test_compare_ignores_unkilled_before` | R9 | before Survived; after missing | exit 0 |
| `test_compare_no_reports` | R1 | `compare`, each case on its own: empty `--before` with a valid `--after`; valid `--before` with empty `--after` | exit 2, stderr starts `c5_equivalence: no_reports:`, no output file, each case |
| `test_compare_invalid_excluded` | R2 | `compare` with valid `--before` and `--after` and excluded list `["N.C.SlowCase", "N.C.Slow"]` | exit 2, stderr starts `c5_equivalence: invalid_excluded:`, no output file |
| `test_excluded_list_is_committed_form` | R11 | the committed `c5-fast-suite-excluded.json` | parses under R2 rules, has at least one key, every key is one of the 12 keys of section 4, ends with a newline |

No failure path writes an output file; each failure test asserts that.

### PR 2

New tests only; every existing test keeps its assertions (standing fixture rule applies).

| Test file, test | Requirement | Input | Expected |
|---|---|---|---|
| `test_c5_workflow.py`, `test_fast_suite_overrides_are_unchanged` | R14 | `--suite fast` and no `--suite` | overrides equal `{"ParchwellVerifierMutationOnly": "true", "APPLICATIONINSIGHTS_STATSBEAT_DISABLED": "true"}` |
| `test_c5_workflow.py`, `test_full_suite_overrides` | R14 | `--suite full` | overrides equal the above plus `"ParchwellVerifierMutationFullSuite": "true"`, three keys |
| `test_c5_workflow.py`, `test_unknown_suite_is_rejected` | R14 | `--suite Full`; `--suite ""` | the CLI exits nonzero before any work |
| `test_c5_workflow.py`, `test_fast_shard_command_matches_base` | R14, R16 | for every shard in `SHARDS`, the argv, working directory and generated Stryker configuration that `run-shard --suite fast` would use, compared with the same values computed by the base commit's `c5_workflow.py` loaded from `git show 15b09147:tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py` | equal for every shard; the implementer may record the base values as a fixture generated once by that command, with the command in the PR notes |
| `test_c5_workflow.py`, `test_full_shard_plan_matches_fast` | R16 | every shard, `--suite full` against `--suite fast` | identical argv, configuration and plan; only the overrides differ |
| `test_c5_workflow.py`, `test_aggregate_rejects_mixed_suites` | R15 | an `AggregateFixture` campaign whose receipts carry fast overrides, aggregated with `--suite full`; and one receipt with full overrides in a fast campaign | aggregate fails, as today's override mismatch fails |
| `test_c5_workflow.py`, `test_summary_records_suite` | R15 | a valid fixture aggregated with `--suite full`, and with `--suite fast` | `summary.json` `suite` is `full`, then `fast`; all other keys equal today's for the same fixture |
| `test_c5_runtime.py`, `test_full_suite_item_group_is_accepted` | R18 | a synthetic csproj with today's groups plus R13's group | `project_closure` accepts it |
| `test_c5_runtime.py`, `test_other_compile_include_is_rejected` | R18 | R13's group with `Include="Component/**/*.cs"`; and with the condition changed | each raises "unsupported Compile inclusion" |
| `test_c5_runtime.py`, `test_mutation_excluded_files_are_shared_inputs` | R19 | a synthetic tree with `Component/MutationExcluded/X.cs` and `Component/Y.cs` | `X.cs` is a shared input; `Y.cs` is not |
| `test_c5_workflow.py`, `test_pair_with_mixed_suites_fails` | R17 | a main run with `suite` `full` and a companion with `fast` | the campaign fails |
| `test_c5_runtime.py`, `test_pinned_full_suite_pairs_are_accepted` | R19a | a synthetic qualification whose shared inputs hold the base hashes of the three files, checked against a head holding their pinned new hashes | not `rejected` |
| `test_c5_runtime.py`, `test_unpinned_runner_change_is_rejected` | R19a | the same, with one byte changed in each of the three files in turn | `rejected`, each case |
| `test_c5_runtime.py`, `test_pinned_pair_keeps_margin_layout` | R19a | the PR 2 head | `timing_margin_changes(<PR 2 head>)` equals `timing_margin_changes(<base>)`, and is not empty |
| `test_c5_runtime.py`, `test_legacy_runner_pair_is_unchanged` | R19a | `RUNNER_TIMING_PAIR` | equals the base commit's value, both hashes |
| `test_c5_runtime.py`, `test_pins_match_the_committed_files` (existing, line 2333) | R19a | the committed files at PR 2's head | passes with the re-pinned `RUNNER_EXECUTION_PIN`; the assertion itself is unchanged |
| `scripts/test-verify-dotnet.sh`, new cases F1 to F4 | R21b | F1 a full run; F2 the Verifier `rest` leg; F3 a failed full-suite build; F4 each skipped leg of M8 | F1 and F2: each full-suite stage runs once, with the Verifier test project and both properties; F3: the run fails after all tests, the full-suite test stage does not run, and the summary's `failed_stages` names `Full-suite mutation Verifier test build`; F4: neither stage runs |

### PR 3

PR 3 adds no test and no check. Its evidence is steps B1 to B4. No existing test changes; the standing fixture rule has nothing to act on.

## 8. Deployment assumptions

None about a deployed container, image, chart or runtime: no pull request changes what runs in Azure.

Assumptions about hosted CI and campaign data, each with how its evidence was taken:

- Hosted shard reports carry `killedBy` and `testFiles` as in section 3. Evidence: one artifact of accepted run 45555996117, read after the run (recorded output), not a live connection. `.config/dotnet-tools.json` is a non-renewable qualification input, so a Stryker version change would show in `c5_runtime.py check`. R3 fails closed if the shape does not hold.
- A hosted shard runs the test set its overrides select: `c5_workflow.py` passes `OVERRIDES` into the Stryker process environment (line 739), and MSBuild reads environment variables as properties ([How MSBuild builds projects, "Evaluate environment variables"](https://learn.microsoft.com/visualstudio/msbuild/build-process-overview#evaluation-phase), read 2026-11-27 with the Microsoft Learn documentation search tool). That the property reaches the test project's evaluation inside Stryker's build is inferred; today's campaigns depend on the same route for `ParchwellVerifierMutationOnly`. The CI stage of R21b checks, on every pull request, that the item group of R13 builds and its tests pass when the property is set with `-p:`; it does not check the environment-variable route, which only step C3 checks, after PR 3.
- Kept tests do not depend on excluded tests having run first in the same process. Accepted by name, decision 7224014847 point 2; checked once by R33.

## 9. Acceptance commands

### PR 1

A1. `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_*.py' -v` passes, with every test of section 7, PR 1 listed.

A2. `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base <merge base> --head HEAD --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` does not report `rejected`. Expected because none of PR 1's files is selected by `input_maps` (named files, pattern-matched files such as `packages.lock.json` and `scenarios/profiles/*.yaml`, project-closure extras, and `.cs`, `.csproj`, `.props`, `.targets` and `.resx` files; `c5_runtime.py` lines 476 to 517) or is in the `relevant` set (lines 2488 to 2490).

A3. Download procedure, run once on the accepted reference campaign as a diagnostic (not acceptance): for each run id, list `repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/<id>/artifacts?per_page=100&page=N` until a page is empty (AGENTS.md "GitHub issues and pull requests"); for each artifact whose name starts with `c5-mutation-`, download its zip and extract only `*mutation-report.json` into `<dir>/<artifact name>/`. Then run `prove` on `<dir>` with the committed list. The PR records the output's `status`, `reports`, `killed`, `lost` count and `timeouts` count for runs 45555996117 and 45555978345. This is a diagnostic of the script on real data at `e3c8eb94`, a pre-601 commit; it is not the proof. Each download is retried up to 4 times with backoff; on exhaustion, record the gap and continue, and the PR says which artifacts are missing.

A4. `npm run check:docs`, warnings disposed under docs/agents/pull-requests.md rule 8.

### PR 2

C1. A1 passes, with every test of section 7, PR 2 listed, and every existing `test_c5_*.py` test passing.

C2. Local builds at PR 2's head with a temporary, uncommitted file `tests/Parchwell.Verifier.Tests/Component/MutationExcluded/Probe.cs` that declares one `[Fact]` in a new class: the build with `-p:ParchwellVerifierMutationOnly=true -p:ParchwellVerifierMutationFullSuite=true` lists 8444 tests and includes the probe; the build with only `-p:ParchwellVerifierMutationOnly=true` lists 8443 and excludes it; no `Component/` test other than the probe appears in either. Counts by script from `-xml` runs as in section 4. The file is deleted before commit; the PR records the counts.

C3. Recorded after merge by the control session, not a merge gate: the first `suite: full` campaign's summary has `suite` `full`, and a report of a shard whose target an excluded test covers lists the excluded test names in `testFiles` once PR 3 has merged. Before PR 3 the folder is empty, so this check applies from the first daily campaign after PR 3.

C4. `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base <merge base> --head HEAD --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` does not report `rejected`, by the route of R19a.

C5. `scripts/verify-dotnet.sh` as CI runs it for the Verifier project passes, including `mutation_only_build`, `mutation_only_test` and the two full-suite stages of R21b; `bash scripts/test-verify-dotnet.sh` passes; A4 passes.

### PR 3

B1. Proof (R30, R31). The session lists `git diff --name-only <campaign commit> <PR 3 base>` and checks by script that every path is under `docs/`, `items/` or `results/`, and that the campaign commit contains PR 2's merge commit. It downloads the reports of both runs by the procedure of A3, applies R30 and records its values, and runs `prove` with the committed list. The PR records the command, both run ids, the commit, the path check's result, the R30 values, and the output file's content in full, including each `timeouts` entry by name. Workload: every `c5-mutation-` artifact of those two runs, 178 and 146 by matrix size, 324 in all; each download retried up to 4 times; on exhaustion, push -wip and hand off to the next session, because the proof is a gate and a missing report could hide a lost kill.

B2. Timing, both builds Release with `-p:ParchwellVerifierMutationOnly=true -p:RestoreLockedMode=true`, the command of section 4, two runs each, before (PR 3 base) and after (PR 3 head before the renewal commit). Both after runs pass. The PR records the four walls and the four summed test times.

B3. Test-list comparison, by script over the B2 XML files: the after runs list exactly the before runs' cases minus every case whose method key is in the final `c5-fast-suite-excluded.json`, by fully qualified name with parameters, and nothing else. For the 12-method list that is 8443 minus 22, 8421 cases. The full-suite build (`-p:ParchwellVerifierMutationFullSuite=true` added) lists the same case names as the before runs, compared as sets. The normal build (without the property) lists the same case names before and after, compared as sets by script. Each count is computed by script.

B4. `scripts/verify-dotnet.sh` as CI runs it for the Verifier project passes. A2 passes after the renewal commit. A1 and A4 pass.

## 10. Sources

- Issue #602 body; issue events of #602; decisions 7223314573 and 7224014847.
- [#600 findings 7201011493](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/600#issuecomment-7201011493), sections 3, 4 and 5 and the strongest counterargument.
- [#599 decision 7193808224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/599#issuecomment-7193808224) point 2.
- Issue #601 body, constraints; docs/operations/verifier-mutation.md "Issue 601 test-side cached state" at the base commit.
- At the base commit: `tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj` lines 20 to 22; `c5_runtime.py` lines 325 to 348, 471, 476 to 517, 1063 to 1071, 2488 to 2490, 2534 to 2536, 2575 to 2577; `c5_workflow.py` lines 304, 309, 316, 327, 329 to 332, 739, 892, 1035 to 1041, 1457 to 1459, 1462 to 1493, 1582, 1757 to 1774, 1895 to 1913; `c5_campaign.py` line 18; `.github/workflows/verifier-c5-mutation.yml` lines 3 to 13 and 400; `.github/workflows/verifier-c5-mutation-companion.yml` lines 3 and 4; `.github/workflows/dotnet-verification.yml` lines 139 to 152; `scripts/verify-dotnet.sh` lines 467 to 496; `Directory.Build.props` line 6. Lines marked reader-reported were read by reader subagents of this session and spot-checked by the planning session only where stated.
- Stryker.NET at `6e77a345`, [MicrosoftTestingPlatformRunner.cs L1071-L1105](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.TestRunner.MicrosoftTestPlatform/MicrosoftTestingPlatformRunner.cs#L1071-L1105), read 2026-11-27.
- Microsoft Learn: [Compare properties and items](https://learn.microsoft.com/visualstudio/msbuild/comparing-properties-and-items#property-and-item-evaluation-order) and [How MSBuild builds projects](https://learn.microsoft.com/visualstudio/msbuild/build-process-overview#evaluation-phase), read 2026-11-27.
- Artifact `c5-mutation-45555996117-1-b10-sweep-judge-p1` (id 14997750805), read 2026-11-27.
- The local timing runs of section 4, outside the repository.

## 11. Strongest counterargument

The excluded tests include the full-scale capacity tests. A future mutant that only a full-scale test kills would be missed between equivalence checks. Today's zero is one commit's evidence. The daily full-suite check (R21) bounds that gap to about a day of commits, and to the next publication, which waits for any return the check requires. It does not close the gap between checks.

The proof has two further limits. Timeout and RuntimeError identities are outside it (R6, R12): nothing records which tests caused them. And it rests on the accepted assumption that a kept test's outcome does not depend on which tests ran before it in the same reused test process; issue 601 found that kind of dependence once. R33 checks that assumption once.

A cost counterargument, which the daily cadence makes larger: a full-suite campaign costs about 530743 s of runner time (estimate from the accepted pre-601 campaign, #600 findings section 4, measured there; not measured after 601). Daily, that is about 530743 s a day whatever the fast suite saves on publication runs. The control session advised against a cadence of (a) or (b) before Hari chose daily (decision 7224014847 point 5). The fast suite's savings land only on publication runs; total runner time rises with the daily check.

A further point: the 1 s cut is near the measured times of at least one method (section 4), so the list is a choice, not a property of the code.

## 12. Deferred work

D1. **Proof campaign.** Authorized by decision 7224014847 point 3. Responsible: the control session, after PR 2 merges (R32), with `suite: full`. Completion evidence: both run ids and the R30 values. It blocks PR 3. It doubles as the 601 acceptance run; consequence: that acceptance run waits for PR 2's merge. If the 601 acceptance is needed sooner, a separate dispatch is the control session's to put to Hari.

D2. **The scheduled task for the daily check.** Responsible: the control session (decision 7224014847 point 5), under R21, starting after PR 3 merges; before that, default and full campaigns build the same tests (R16) and the check proves nothing new. Not a repository change.

D3. **Shard re-layout and qualification shrink** (issue design item 4). A new layout needs fresh hosted timing for every changed selection (runbook, "Runtime qualification diagnostics"), then `c5_workflow.py` `TARGETS`, `PRIMARY_CUTS` and `COHORTS`, both protected matrices, new `timing_evidence` and `timing_inputs` entries, and a paired run against the 4,200 s `BUDGET` (reader-reported from `c5_runtime.py` and the runbook). Hari authorized the sizing diagnostic after PR 3 merges (decision 7224014847 point 6). Responsible: a planning session after that diagnostic. On the verdict path: no. Consequence of the daily cadence, inferred: every layout must still qualify the full-suite campaign, which has today's test set and today's timing. A layout sized for the fast suite alone (about 50 to 80 jobs, #600 estimate of 2026-11-26, basis: 7.9 s per mutant locally on one target, 12715 scored mutants, 204 isolated invocations, and the unmeasured assumption that a hosted runner matches the local container; computed for a 14-method list at a pre-601 commit) would overrun its budgets in full-suite mode. So D3 must either keep today's 324-shard layout for full-suite campaigns and add a second, smaller layout for fast campaigns, or keep one layout sized for the full suite, which saves no jobs. The D3 plan puts that choice to Hari. Which timing machinery could then be retired is the D3 plan's question; with two layouts, little can be.

D4. **Issue 598 consumer targets** (issue design item 5). #598 is paused (#599 decision point 3); its sizing needs the D3 layout and goes to Hari (decision 7223314573 point 2). Responsible: the #598 planning session after D3.

## 13. Plan-check questions

(a) Steps whose effort scales with a number unknown at planning time: the artifact downloads of A3 and B1 (bounded by the artifacts of two named runs, 178 and 146 by matrix size; A3 records the gap and continues, B1 pushes -wip and hands off); the number of methods kept under R31 (bounded by the 12 of the list; one rerun on reports already downloaded); the regressions of R33 (bounded by the Killed identities of one campaign; each is handled by the same rule; no session work scales beyond one script run). The daily check (R21) repeats without end by design; each day's work is one campaign and one script run, and a failing day is reported, not retried the same day. Timing runs are fixed at two per build.

(b) Stricter than the parent: there is no parent plan. Against the issue body and decisions: R31 makes the proof a gate before PR 3 is opened ("before relying on it" in the issue); R32 requires PR 2's merge before the proof campaign, which follows from decision 7224014847 point 2's path rule; R14 requires default-mode execution to be byte-identical to today's, which no decision asks for but which keeps acceptance unchanged.

(c) Checks that depend on code, data or behaviour neither on `main` nor in the pull requests: B1 and R33 depend on campaigns that do not exist yet (D1); C3 depends on the first full-suite campaign after PR 3; R3 and the section 3 field shapes depend on Stryker's report, checked against recorded output of one hosted artifact; recovered campaigns depend on issue #604 (R30). Listed in section 8 or under R30.

## 14. Size estimate

Estimates, 2026-11-27, basis: the file structure above and the sizes of the #601 and #494 pull requests; not measured.

- PR 1: about 4 files, about 800 changed lines (script about 300, tests about 450, list 14, runbook about 45).
- PR 2: about 12 files, about 900 to 1,400 changed lines (runner and runtime changes about 250 including the pinned pairs and re-pin of R19a, tests about 400, `verify-dotnet.sh` and its test about 120, workflows about 20, runbook about 50, plus qualification changes).
- PR 3: about 20 files (9 edited test files, about 9 new files, the list, the qualification file and the runbook), about 1,200 changed lines, most of them the moved test bodies counted twice.

Size is reported, not enforced.

## 15. Open questions for Hari

None.

### Answered questions, kept for the record

Q6 and Q7 below are answered by decision 7234844190 points 1 and 2: Q6 option (a), applied as R19a, and Q7 yes, applied in R20. The text is kept as it stood, with the replacements of check 7224304814 finding 2 applied as that decision directs. The size note at the end of option (a) is superseded by section 14 of this revision, which now includes that work.

Q6. **How the runtime qualification accepts PR 2.** PR 2 changes `c5_workflow.py` and both mutation workflows. `c5_runtime.py` lists all three as `NON_RENEWABLE` (line 344): `renew` refuses, and `check` rejects, any non-renewable change unless the old and new hashes are an exact pinned pair (`renew` lines 2534 to 2536 and 2575 to 2577; `validate` lines 2269 to 2271 and 1803 to 1806; today only `c5_projection.py` and `c5_workflow.py` have such pairs, `PROJECTION_TIMING_PAIR` and `RUNNER_TIMING_PAIR`, lines 333 to 336). The existing `renew` command cannot cover it. Options:
  - (a) **Pinned exact pairs, recommended.** PR 2 adds exact (base, head) hash pairs for `c5_workflow.py` and both workflows to `c5_runtime.py`, in the pattern of `RUNNER_TIMING_PAIR`, with the basis that default-mode execution is byte-identical (R14, tested for every shard) and full-suite mode builds today's test set on today's layout, so today's timing evidence still bounds both. No hosted dispatch before merge. Risk: the pinned pair must be recomputed if the files change during review; a later change to any of the three needs its own pair or fresh timing. Further cost, read from `c5_runtime.py` at the base commit: `RUNNER_TIMING_PAIR` already holds the issue 494 runner transition and is read as a single legacy bridge (lines 1067 and 1104 to 1116), so a second `c5_workflow.py` pair needs its own constant and a change at each place that accepts a pair (lines 1803 to 1805 and 2575 to 2577) and in `same_timing_layout` (line 1080); `RUNNER_EXECUTION_PIN` (line 343, checked at line 543, asserted by `test_c5_runtime.py` line 2333) must be re-pinned, or the recorded `c5_campaign.py` hash changes too (lines 535 and 536). The PR 2 size estimate in section 14 does not include this work.
  - (b) **Fresh hosted timing.** A `verifier-c5-sizing.yml` diagnostic of the changed runner for every shard, then a qualification update, as issue 494 did. Cost: one or more hosted diagnostics and probably several pull requests (among the repository's latest 100 pull requests, 35 have a branch name containing `494`; listing by script, 2026-11-27); not estimated further.
  - (c) **Defer the runner change**, accepting no daily check until it lands; this contradicts decision 7224014847 point 5 and is listed only for completeness.

Q7. **A new dispatch input under hard safety rule 8.** R20 adds a `suite` input to the `workflow_dispatch` trigger of both workflows. Rule 8 says "An agent never changes an existing workflow's triggers". I read that as the trigger events, which stay exactly `workflow_dispatch`, but an input is part of the trigger's configuration. Do you allow the input? Recommendation: yes, since the events, the `main`-only guard (line 20) and the permissions are unchanged. Alternative: no new input; the suite then has to come from somewhere else, for example a committed file that a pull request flips, which would make the daily full-suite run a pull request each day and is not practical.

Q1 to Q5 of revision r2 are answered by decision 7224014847 and applied since r3.

## 16. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract of section 3, a requirement, the list other than under R31, or the scope of section 2; when the proof's campaign does not exist or does not meet R30 or R31; when the script fails with a failure name on real reports; when `renew` or `check` refuses, or R19a's pinned pairs cannot make `check` accept PR 2, including at the early proof step of R19b; when R14's byte-identity test cannot pass without changing default-mode execution; or when any required check fails for a reason it cannot fix inside the allowed files.

# Fast suite: contract-grade plan

Revision: r1. Base commit: `15b09147ed4e7c62ea08620b24c14cd049f07562` on `main` (the merge of the issue 601 fix, pull request #603).

Two pull requests make the verifier mutation campaign run a faster test suite. The first adds a script that proves, from a campaign's own reports, that no mutant is killed only by the slow tests. The second moves 12 slow test methods out of the mutation-only build, once that proof has passed on a post-601 campaign. The shard re-layout, the qualification-file shrink and the consumer targets of issue 598 cannot be pinned until a hosted run of the faster suite has been timed, so they are deferred (section 12) with their prerequisites.

## 1. Goal and authority

Goal: cut the time each mutant spends in tests by about 60 percent of the summed test time, without losing any kill the full mutation-only suite makes, and prepare the measured re-layout.

Authority, in creation order:

1. The body of issue #602: design items 1 to 5, the strongest counterargument to state, and the constraints. The issue was opened at Hari's "yes" in the #499 control session on 2026-11-26.
2. The `ready-for-agent` label on #602, applied 2026-11-27T06:42:25Z (issue events). Under docs/agents/factory.md "Item lifecycle" step 2 it is the item-level approval. No comment on #602 names an implementing tool, so the tool is Claude Code (AGENTS.md "Implementer and reviewer").

#602 has no comments at the base snapshot, so no decision or approval comment changes that authority. No decision on #602 adopts the pattern of [#601 decision 7211787887](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/601#issuecomment-7211787887), under which the label approved the plan revision with a clean check. This plan therefore needs Hari's `## Approval` naming its revision and commit (factory.md "Item lifecycle" step 3).

Context, read as evidence and not as authority: [#600 findings 7201011493](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/600#issuecomment-7201011493), which the issue body cites; [#599 decision 7193808224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/599#issuecomment-7193808224) point 2 (mutation acceptance runs are batched to publication points); the #601 plan and its runbook record.

There is no item plan and no parent plan. This is a mutation-harness issue, and the issue body is the specification.

## 2. Scope

The set is named "Fast suite". It has two pull requests, in this order (factory.md "Item lifecycle" step 3 "Split by scope"). Each is reviewable and merges green on its own.

### PR 1 "Equivalence check", branch `claude/602-equivalence-check`

| File | Protected | Change |
|---|---|---|
| `tests/Parchwell.Verifier.Tests/Mutation/c5_equivalence.py` | yes (`tests/Parchwell.Verifier.Tests/**`) | new; R1 to R7 |
| `tests/Parchwell.Verifier.Tests/Mutation/test_c5_equivalence.py` | yes | new; the tests of section 7, PR 1 |
| `tests/Parchwell.Verifier.Tests/Mutation/c5-fast-suite-excluded.json` | yes | new; R8 |
| `docs/operations/verifier-mutation.md` | yes (`docs/`) | new section, R9 |

### PR 2 "Fast suite", branch `claude/602-fast-suite`

| File | Protected | Change |
|---|---|---|
| The nine test files that hold the excluded methods (section 4) | yes | R10, R11 |
| New files under `tests/Parchwell.Verifier.Tests/Component/MutationExcluded/` | yes | R10 |
| `tests/Parchwell.Verifier.Tests/Mutation/c5-fast-suite-excluded.json` | yes | only as R13 requires |
| `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` | yes | R14, written only by the `renew` command |
| `docs/operations/verifier-mutation.md` | yes | R15 |

Paths that must not change in either pull request: everything under `src/`; `tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj`; `tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py`, `c5_runtime.py`, `c5_campaign.py`, `c5_projection.py`, `c5_companion_artifacts.py`, `stryker-c5-config.json` and their existing tests; `tests/Parchwell.Verifier.Tests/stryker-config.json`; every test file not named in section 4; `.github/**`; `.config/dotnet-tools.json`; `scripts/**`; `results/**`; every `scenarios/*/expected.yaml`; AGENTS.md, CLAUDE.md and `docs/` outside `docs/operations/verifier-mutation.md`.

Out of scope for both: any threshold or gate change; any change to the shard layout, the matrices, `CAMPAIGN_LIMITS` (178 and 146), the 4,200-second `BUDGET` or `budget_seconds`; any assertion, data row or expected value; any hosted dispatch by an implementing session.

Why the moved methods go under `Component/`: the mutation-only `Compile Remove` already drops `Component/**/*.cs` (csproj lines 20 and 21), and `c5_runtime.py` pins that list byte for byte (`REMOVE`, line 348; checked at line 471; any other string raises "unsupported Compile inclusion", line 477). Changing the list would change `c5_runtime.py`, which historical proof trees re-read at pinned commits (lines 1063 to 1071). A subfolder under `Component/` needs none of that, and `EXCLUDED` (line 346) keeps those files out of the shared inputs. The cost is a misleading folder name; R10 requires a one-line comment in each moved file saying why it sits there.

## 3. External contracts

- **Report fields the script reads.** Stryker's JSON report (`stryker/reports/mutation-report.json` in each shard artifact, `c5_workflow.py` line 327, and `isolated/<ID>/stryker/reports/mutation-report.json`, lines 1035 to 1041) has top-level `files` and `testFiles`. Each mutant has `id`, `mutatorName`, `replacement`, `location` (`start` and `end`, each `line` and `column`), `status`, `static` and, when Killed, `killedBy`, a list of test ids. `testFiles[<path>].tests[*]` has `id` and `name`. A name is `<Namespace>.<Type>.<Method>(<parameter types>)`, for example `Parchwell.Verifier.Tests.A01.A01C3EvidenceRecorderTests.Given_UnpassedStatisticsPrerequisite_When_C3Recorded_Then_OmitsWindowsAndProbe(System.Int32,System.String)`. File keys are absolute paths in a temporary checkout, such as `/tmp/c5-source-zl812_io/repository/src/Parchwell.Verifier/Judge/B10SweepChecks.cs`. Source: recorded output, artifact `c5-mutation-45555996117-1-b10-sweep-judge-p1` of accepted run 45555996117 at `e3c8eb94`, downloaded and read by script by this planning session on 2026-11-27: 8443 tests with 8443 distinct ids; 43 Killed mutants, each with a non-empty `killedBy` whose every id is in `testFiles`.
- **Excluded-test list.** `c5-fast-suite-excluded.json` is a JSON array of strings, each `<Namespace>.<Type>.<Method>` with no parameter list, sorted, no duplicates. The script, PR 2's acceptance commands and the runbook read it.
- **Script CLI.** `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_equivalence.py --reports-dir DIR --excluded FILE --output FILE`. All three are required.
- **Output JSON**, written to `--output` with sorted keys and a trailing newline:

  ```
  {"schema": 1, "status": "equivalent" | "not_equivalent", "reports": <int>, "killed": <int>, "timeouts": <int>, "excluded": <int>, "lost": [<lost entry>, ...]}
  ```

  A lost entry is `{"file": <normalized path>, "mutant_ids": [<ids, sorted as strings>], "mutator": <mutatorName>, "replacement": <replacement>, "location": <location object>, "static": <bool>, "killed_by": [<test names, sorted>]}`. `lost` is sorted by `file`, then `location.start.line`, `location.start.column`, then `mutator`.
- **Failure names**, printed as `c5_equivalence: <name>: <detail>` on stderr with exit status 2 and no output file: `no_reports`, `invalid_report`, `invalid_excluded`, `killed_without_tests`, `unmapped_test_id`, `unmatched_excluded`.
- **Exit status.** 0 for `equivalent`, 1 for `not_equivalent`, 2 for a failure name.
- **Qualification basis text** (PR 2), section 5 R14.
- **Runbook sections** (R9, R15), read by later sessions as the record.

Kafka, librdkafka, Confluent.Kafka and Event Hubs behaviour: none is relied on.

Stryker behaviour relied on, checked by this session on 2026-11-27 by reading the source at [`6e77a345`](https://github.com/stryker-mutator/stryker-net/tree/6e77a3451bac4793e9c839c3ff4055c1b30ca3af) (tag `dotnet-stryker@5.0.0`) with `curl` from raw.githubusercontent.com:

- The MTP runner runs each assembly's selected tests to completion for a mutant and then collects every failed test: `RunAssemblyTestsAsync` in a loop over assemblies, then `var failedTestIds = accumulator.BuildFailedTests();` and `update.Invoke(mutants, failedTestIds, executedTests, timedOutTestIds);` ([MicrosoftTestingPlatformRunner.cs L1071-L1105](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.TestRunner.MicrosoftTestPlatform/MicrosoftTestingPlatformRunner.cs#L1071-L1105)). Inferred, not stated there: `killedBy` holds every test that failed under the mutant, not only the first. The #600 data agree: `killedBy` lengths had median 9 and range 1 to 223 over 9773 kills.
- With coverage `off`, every test runs for every mutant (#600 setup, `stryker-c5-config.json`). So a mutant killed only by excluded tests is the only way the fast suite can lose a kill, at the commit the campaign tested. Inferred.

## 4. The excluded methods

Measured by this planning session on 2026-11-27 at the base commit: Release build with `-p:ParchwellVerifierMutationOnly=true -p:RestoreLockedMode=true`, 4-core container, idle, fresh process per run, two runs, `dotnet exec tests/Parchwell.Verifier.Tests/bin/Release/net10.0/Parchwell.Verifier.Tests.dll -noColor -xml <file>` with `DOTNET_ROOT` set. Each run: 8443 tests, 0 failed; wall 83.17 s and 81.91 s; summed test time 221.40 s and 210.26 s. Durations were summed per method by script.

Selection rule: every test method with at least one case of 1.0 s or more in both runs. That gives 12 methods with 22 cases, 133.5 s and 126.4 s summed, 60.3 and 60.1 percent of the summed time. One method crossed 1.0 s in one run only and is not selected: `Parchwell.Verifier.Tests.Judge.ConsumerPhaseJudgeTests.Given_ConsumerCandidateWithAMismatchedPair_When_Read_Then_Rejects`, 1.088 s and 0.811 s. It is the class whose kills issue 601 just corrected, so keeping it in the mutation suite is the safer side.

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

This is not the list of 14 that #600 measured. #600 timed a VSTest run at `46a58ddd`, before the 601 fix, and its method list was kept only in that session's scratchpad, which is not in the repository. The two lists share the selection rule (any case of 1 s or more) but not the runner, the commit or the method names. Question Q2 asks Hari to accept this list.

## 5. Requirements

### PR 1 "Equivalence check"

R1. WHEN the script runs THEN it reads every file named `mutation-report.json` found recursively under `--reports-dir`, in sorted path order. WHEN none is found THEN it fails with `no_reports`. WHEN a file is not a JSON object with object `files` and object `testFiles`, or a mutant lacks `id`, `mutatorName`, `replacement`, `location`, `status` or `static` THEN it fails with `invalid_report`, naming the file.

R2. WHEN the excluded file is not a JSON array of strings, is not sorted, has a duplicate, has an empty string, or has an entry containing `(` THEN the script fails with `invalid_excluded`.

R3. WHEN it reads a report THEN it maps each test id to its name through `testFiles[*].tests[*]`, and a test's method key is the name up to its first `(`, or the whole name when there is none. WHEN a Killed mutant has a missing or empty `killedBy` THEN it fails with `killed_without_tests`. WHEN a `killedBy` id is not in that report's `testFiles` THEN it fails with `unmapped_test_id`.

R4. WHEN it reads a file key THEN the normalized path is the text after the first `/repository/` segment. WHEN a key has no such segment THEN it fails with `invalid_report`. A mutant's identity is (normalized path, `mutatorName`, `replacement`, `location.start.line`, `location.start.column`, `location.end.line`, `location.end.column`, `static`), the #599 and #600 comparison key.

R5. WHEN the same identity is Killed in more than one report (a cohort's top-level report copies its bootstrap isolated report, `c5_workflow.py` lines 887 to 889; crossing spans) THEN its killing tests are the union of the method keys over all its Killed rows. WHEN that union, minus the excluded method keys, is empty THEN the identity is lost. Exact string match only: an excluded key `A.B.Foo` does not match `A.B.FooBar`. Statuses other than Killed never make an identity lost. `killed` counts distinct Killed identities. `timeouts` counts distinct identities with status Timeout that are never Killed. `reports` counts files read. `excluded` counts list entries.

R6. WHEN an excluded method key matches no test name in any report read THEN the script fails with `unmatched_excluded`, naming the key. This catches a renamed or mistyped entry.

R7. WHEN no identity is lost THEN `status` is `equivalent` and the exit status is 0. WHEN at least one is lost THEN `status` is `not_equivalent`, each lost identity is listed once with the ids it had in the reports, and the exit status is 1. The script makes no network call and writes nothing but `--output`.

R8. WHEN PR 1 merges THEN `c5-fast-suite-excluded.json` holds exactly the 12 method keys of section 4, sorted, two-space indented, with a trailing newline.

R9. WHEN PR 1 merges THEN docs/operations/verifier-mutation.md has a section `## Issue 602 fast-suite equivalence check`, placed directly after "Issue 601 test-side cached state". It states: what the script proves and what it cannot (R5; the proof holds only for the commit the campaign tested); the CLI, exit statuses and failure names of section 3; the download procedure of section 9, step A3; and the strongest counterargument of section 11, quoted in full. It changes no other section.

### PR 2 "Fast suite"

R10. WHEN PR 2 merges THEN each method listed in `c5-fast-suite-excluded.json` is declared in a file under `tests/Parchwell.Verifier.Tests/Component/MutationExcluded/`, in the same namespace and class as at the base commit, so its fully qualified name and every data row are unchanged. A class that keeps other tests becomes `partial` in both files; a class whose every test moves may move whole. Each new file starts with a comment that says it sits under `Component/` only so the mutation-only build drops it, and links this section of the runbook (R15). Helpers move with the tests only where the mutation-only build needs it to compile without warnings (warnings are errors, `Directory.Build.props` line 6).

R11. WHEN PR 2 merges THEN no other test method moves, and no test body, attribute, data row or assertion changes. The normal build compiles and runs every moved test, because the `Compile Remove` applies only when `ParchwellVerifierMutationOnly` is `true` (csproj line 20).

R12. Proof gate. WHEN PR 2's pull request is opened THEN the equivalence script has run, under section 9 step B1, on the reports of a post-601 campaign whose commit has the same `src/Parchwell.Verifier/**` and `tests/Parchwell.Verifier.Tests/**` content as PR 2's base, apart from `tests/Parchwell.Verifier.Tests/Mutation/**`, and its output is recorded in the pull request. WHEN no such campaign exists THEN the session stops and hands off; it does not dispatch one (Q1).

R13. WHEN that run reports `not_equivalent` THEN every excluded method that appears in any lost identity's `killed_by` stays in the mutation suite: PR 2 removes it from `c5-fast-suite-excluded.json` and does not move it, and the script is run again on the same reports with the shortened list, which must then report `equivalent`. The pull request lists each kept method with the lost identities that kept it. WHEN the run fails with a failure name THEN the session stops and hands off with the message.

R14. WHEN PR 2 is opened THEN `c5-runtime-qualification.json` has been renewed, as a separate commit after the test move, with `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py renew --head <HEAD_SHA> --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json --basis "<BASIS>"`, where `<HEAD_SHA>` is the commit that holds the move, and `c5_runtime.py check --base <merge base> --head <HEAD>` does not report `rejected`. `<BASIS>` is exactly:

  `Issue 602 moves <N> slow test methods out of the mutation-only build; no target, runner or workflow changed. Removing tests can only shorten each mutant's test run. Local mutation-only suite wall, 4-core container, idle, fresh process per run, 2 runs each on <DATE>: <B1> s and <B2> s before, <A1> s and <A2> s after. Equivalence check on campaign <RUN_ID> at <SHA8>: equivalent.`

  The placeholders are filled from the measurements of section 9 step B2 and the B1 run, with `<N>` the number of methods moved. "Can only shorten" is an inference, not measured per shard: fewer tests run per mutant, and the Stryker initial run and process start are unchanged. The issue 601 renewal (commit `d4dfe39`, reader-reported) is the precedent for renewing changed test inputs with a basis. If the tool refuses the renewal, the session stops and hands off with its output.

R15. WHEN PR 2 merges THEN the runbook section of R9 gains a subsection `### Fast suite record` that states: the moved methods, from the final list; the measurements of section 4 and of step B2; the equivalence result with the campaign run ids and commit; any method kept under R13; that the hosted campaign now runs the fast suite, so a later campaign's reports can no longer show what the excluded tests kill, and an equivalence check needs a full-suite campaign (D2); and that the shard layout is unchanged until D3.

## 6. Invariants and safety rules

- No assertion is loosened, deleted or skipped (AGENTS.md "Code Review Rules"). Moving a test out of the mutation build is not deleting it: it still runs in normal CI (R11).
- The 31 targets, the MTP runner and coverage `off` stay (issue design item 1).
- No file under `results/` and no `expected.yaml` changes. The mutation score is not edited. A mutant that turns Killed to Survived in a later campaign is a finding, not a reason to change a threshold.
- No session dispatches a workflow without Hari's decision (issue 601 constraints; #599 decision point 2). Dispatches are against `main` only (AGENTS.md hard safety rule 8).
- The protected paths of section 2 reach `main` only through Hari's merge.
- No number is published that was not measured (hard safety rule 11). Every estimate below says "estimate", its basis and its date.

Standing fixture rule. PR 1 adds a check, the equivalence script; it changes no existing test. If an existing test's fixture is still rejected by a new check, the implementer does exactly one of: (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it, and the cold review and Codex review check each one. Modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.

## 7. Test map

### PR 1, `test_c5_equivalence.py`

The file name matches `test_c5_*.py`, so `.NET verification` runs it (`.github/workflows/dotnet-verification.yml` lines 139 to 147). Each test writes reports into a `TemporaryDirectory` and calls the script's entry point with arguments. A fixture report has one file key `/tmp/x/repository/src/Parchwell.Verifier/A.cs`, tests `t1` named `N.C.Keep(System.Int32)`, `t2` named `N.C.Slow`, `t3` named `N.C.SlowCase(System.String)` and `t4` named `N.C.SlowBar`, and an excluded list `["N.C.Slow", "N.C.SlowCase"]`.

| Test | Requirement | Input | Expected |
|---|---|---|---|
| `test_killed_by_kept_and_excluded_is_equivalent` | R5, R7 | mutant `1` Killed by `[t1, t2]` | exit 0, `status` `equivalent`, `killed` 1, `lost` [] |
| `test_killed_only_by_excluded_is_lost` | R5, R7 | mutant `1` Killed by `[t2, t3]` | exit 1, `not_equivalent`, one lost entry with `file` `src/Parchwell.Verifier/A.cs`, `mutant_ids` `["1"]`, `killed_by` `["N.C.Slow", "N.C.SlowCase"]` |
| `test_prefix_name_is_not_excluded` | R5 | mutant Killed by `[t4]` | exit 0 |
| `test_union_over_duplicate_reports` | R5 | the same identity, id `1` in report A Killed by `[t2]` and id `7` in report B Killed by `[t1]` | exit 0, `killed` 1 |
| `test_duplicate_reports_both_excluded` | R5, R7 | as above, both Killed by `[t2]` | exit 1, one lost entry, `mutant_ids` `["1", "7"]` |
| `test_static_flag_separates_identities` | R4 | two mutants equal except `static` true and false, one Killed by `[t1]`, one by `[t2]` | exit 1, one lost entry with `static` true or false as given |
| `test_other_statuses_are_not_lost` | R5 | mutants with Survived, Timeout, Ignored, CompileError and no `killedBy` | exit 0, `killed` 0, `timeouts` 1 |
| `test_zero_and_one_lost_boundary` | R7 | two mutants, Killed by `[t1]` and by `[t2]` | exit 1, exactly one lost entry |
| `test_no_reports` | R1 | empty directory | exit 2, stderr starts `c5_equivalence: no_reports:` |
| `test_invalid_report` | R1 | report without `testFiles`; mutant without `static` | exit 2, `invalid_report`, each case |
| `test_file_key_without_repository_segment` | R4 | file key `/tmp/x/src/A.cs` | exit 2, `invalid_report` |
| `test_invalid_excluded` | R2 | not an array; unsorted; duplicate; empty string; entry `N.C.Slow(System.Int32)` | exit 2, `invalid_excluded`, each case |
| `test_killed_without_tests` | R3 | Killed with `killedBy` [] and with no `killedBy` | exit 2, `killed_without_tests`, each case |
| `test_unmapped_test_id` | R3 | Killed by `[t9]` | exit 2, `unmapped_test_id` |
| `test_unmatched_excluded` | R6 | excluded list `["N.C.Slow", "N.C.Gone"]` | exit 2, `unmatched_excluded`, detail names `N.C.Gone` |
| `test_output_is_sorted_and_stable` | section 3 | two lost identities in reverse line order | `lost` sorted by file and line; output bytes identical over two runs; trailing newline |
| `test_excluded_list_is_committed_form` | R8 | the committed `c5-fast-suite-excluded.json` | parses under R2 rules, has exactly the 12 keys of section 4, ends with a newline |

No failure path writes an output file; each failure test asserts that.

### PR 2

PR 2 adds no test and no check. Its evidence is the acceptance commands B1 to B4, which compare test lists and runs by script. No existing test changes; the standing fixture rule has nothing to act on.

## 8. Deployment assumptions

None about a deployed container, image, chart or runtime: neither pull request changes what runs in Azure.

Assumptions about hosted CI and campaign data, each with how its evidence was taken:

- Hosted shard reports carry `killedBy` and `testFiles` as in section 3. Evidence: one artifact of accepted run 45555996117, read after the run (recorded output), not a live connection. The same Stryker tool version applies at the base commit: `.config/dotnet-tools.json` is a non-renewable qualification input, so a version change would show in `c5_runtime.py check`. Not verified for the post-601 campaign until step B1 runs; R3 fails closed if it does not hold.
- A hosted shard runs the same mutation-only test set the local build lists: `c5_workflow.py` lines 329 to 332 set `ParchwellVerifierMutationOnly` for every shard (reader-reported; the planning session read the csproj condition). Not a live observation.

## 9. Acceptance commands

### PR 1

A1. `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_*.py' -v` passes, with every test of section 7 listed.

A2. `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base <merge base> --head HEAD --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` does not report `rejected`. Expected: PR 1 changes no qualification input, because `input_maps` adds only named files and `.cs`, `.csproj`, `.props`, `.targets` and `.resx` files (`c5_runtime.py` lines 499 to 517, reader-reported).

A3. Download procedure, run once on the accepted reference campaign as a diagnostic (not acceptance): for each run id, list `repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/<id>/artifacts?per_page=100&page=N` until a page is empty (AGENTS.md "GitHub issues and pull requests"); for each artifact whose name starts with `c5-mutation-`, download its zip and extract only `*mutation-report.json` into `<dir>/<artifact name>/`. Then run the script on `<dir>` with the committed list. The PR records the output's `status`, `reports`, `killed` and `lost` count for runs 45555996117 and 45555978345. This is a diagnostic of the script on real data at `e3c8eb94`, a pre-601 commit; it is not the proof. Each download is retried up to 4 times with backoff; on exhaustion, record the gap and continue, and the PR says which artifacts are missing.

A4. `npm run check:docs`, warnings disposed under docs/agents/pull-requests.md rule 8.

### PR 2

B1. Proof (R12, R13). The session confirms by `git diff --quiet <campaign commit> <PR 2 base> -- src/Parchwell.Verifier tests/Parchwell.Verifier.Tests ':!tests/Parchwell.Verifier.Tests/Mutation'`. It downloads the reports of the main and companion runs of the post-601 campaign that Hari dispatched (Q1) by the procedure of A3, and runs the script with the committed list. The PR records the command, both run ids, the commit, and the output file's content in full. Workload: every `c5-mutation-` artifact of those two runs: 178 in the reference main run (artifact listing by script, 2026-11-27) and, by the companion matrix size, 146 in its companion, 324 in all; each download retried up to 4 times; on exhaustion, push -wip and hand off to the next session, because the proof is a gate and a missing report could hide a lost kill.

B2. Timing, both builds Release with `-p:ParchwellVerifierMutationOnly=true -p:RestoreLockedMode=true`, the command of section 4, two runs each, before (PR 2 base) and after (PR 2 head before the renewal commit). Both after runs pass. The PR records the four walls and the four summed test times.

B3. Test-list comparison, by script over the B2 XML files: the after runs list exactly the before runs' cases minus every case whose method key is in the final `c5-fast-suite-excluded.json`, by fully qualified name with parameters, and nothing else. For the 12-method list that is 8443 minus 22, 8421 cases. The normal build (without the property) lists the same 8443 case names before and after. Each count is computed by script.

B4. `scripts/verify-dotnet.sh` as CI runs it for the Verifier project passes, including `mutation_only_build` and `mutation_only_test` (lines 471 to 496). A2 passes after the renewal commit. A1 and A4 pass.

## 10. Sources

- Issue #602 body; issue events of #602.
- [#600 findings 7201011493](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/600#issuecomment-7201011493), sections 3 and 5 and the strongest counterargument.
- [#599 decision 7193808224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/599#issuecomment-7193808224) point 2.
- Issue #601 body, constraints; docs/operations/verifier-mutation.md "Issue 601 test-side cached state" at the base commit.
- At the base commit: `tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj` lines 20 and 21; `c5_runtime.py` lines 327, 344 to 348, 471, 477, 499 to 517, 1063 to 1071; `c5_workflow.py` lines 304, 309, 316, 327, 329 to 332, 887 to 889, 1035 to 1041, 1462, 1582, 1757 to 1774; `c5_campaign.py` line 18; `.github/workflows/dotnet-verification.yml` lines 139 to 152; `scripts/verify-dotnet.sh` lines 467 to 496; `Directory.Build.props` line 6. Lines marked reader-reported were read by reader subagents of this session and spot-checked by the planning session only where stated.
- Stryker.NET at `6e77a345`, [MicrosoftTestingPlatformRunner.cs L1071-L1105](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.TestRunner.MicrosoftTestPlatform/MicrosoftTestingPlatformRunner.cs#L1071-L1105), read 2026-11-27.
- Artifact `c5-mutation-45555996117-1-b10-sweep-judge-p1` (id 14997750805), read 2026-11-27.
- The local timing runs of section 4, outside the repository.

## 11. Strongest counterargument

The excluded tests include the full-scale capacity tests. A future mutant that only a full-scale test kills would be missed between equivalence checks. Today's zero is one commit's evidence. The equivalence check bounds that gap at the commits it runs on; it does not close it between them.

What would reduce the gap, and its cost: a full-suite campaign at a set cadence (D2). Without one, a test or target change can create a slow-only kill that no check sees until the next full-suite campaign. The cadence is Hari's decision (Q3).

A second counterargument this plan adds: the 1 s cut is near the measured times of at least one method (section 4), so the list is a choice, not a property of the code. A different cut changes the list, and each list needs its own proof.

## 12. Deferred work

D1. **Post-601 full campaign (the proof's input).** Evidence: R12 needs kill sets after the 601 fix; no campaign has run since #603 merged (actions runs listing, 2026-11-27). On the verdict path: yes, it gates PR 2. Deferral is permitted because a dispatch needs Hari's decision (Q1). Responsible: the control session or a session Hari directs, dispatching `verifier-c5-mutation.yml` and its companion on `main` after PR 1 merges or before, at a commit that meets R12. Workload: one paired campaign, the current 324-shard layout. Completion evidence: both run ids and a passing aggregate. It blocks PR 2. Suggested use: the same campaign can be the batched acceptance run that the 601 change also needs (#601 plan D1; #599 decision point 2), so one dispatch serves both. Inferred; Hari decides.

D2. **Full-suite mode for later equivalence checks.** Evidence: after PR 2 merges, every hosted shard builds the fast suite, and `aggregate` requires each receipt's `environment_overrides` to equal `OVERRIDES` (`c5_workflow.py` line 1582). So no later campaign can show what the excluded tests kill without a runner and workflow change. `c5_workflow.py` and both workflows are `NON_RENEWABLE` (`c5_runtime.py` lines 344 and 345). Not on today's verdict path; it carries the counterargument of section 11. Deferral is permitted because its shape depends on Hari's cadence (Q3). Responsible: a new plan revision or issue after Hari answers Q3. Blocks: nothing now; each later reliance on the fast suite after a test or target change.

D3. **Shard re-layout and qualification shrink** (issue design item 4). Evidence: a new layout needs fresh hosted timing for every changed selection (runbook, "Runtime qualification diagnostics"; precedent `verifier-c5-sizing.yml` diagnostics), then `c5_workflow.py` `TARGETS`, `PRIMARY_CUTS` and `COHORTS`, both protected matrices, new `timing_evidence` and `timing_inputs` entries, and a paired run against the 4,200 s `BUDGET` (reader-reported from `c5_runtime.py` and the runbook). None of that timing exists for the fast suite, so the layout cannot be pinned contract-grade today. On the verdict path: no; the current layout stays valid with a faster suite. Responsible: a planning session after PR 2 merges, with a hosted sizing diagnostic of the fast suite that Hari authorizes (Q4). Workload bound: set by that plan. Completion: a qualified layout on `main`.

  Estimate (from #600 section 5, dated 2026-11-26, basis: local fast-suite 7.9 s per mutant on one target, 12715 scored mutants, 204 isolated invocations, and the unmeasured assumption that a hosted runner matches the local container): about 50 jobs under the 2,700 s `budget_seconds`, or about 80 under the 1,800 s advisory, against 324. That #600 figure used a 14-method list at a pre-601 commit; this plan has not re-estimated it.

  Which timing machinery could be retired, inferred and not checked against the code paths that use it: if the new layout fits the coordinator's 178-job capacity, the companion workflow and `c5_companion_artifacts.py` may no longer be needed; codec cohorts and many span cuts could merge, as #600 suggests; the per-shard `targets` and `timing_evidence` sections would shrink with the shard count; recovery and timing-margin bridges in `c5_runtime.py` (for example `TIMING_MARGIN_*` and `PAIRED_RECOVERY_*`) remain needed while historical proof trees are re-read. The D3 plan decides each with the code in hand.

D4. **Issue 598 consumer targets** (issue design item 5). Evidence: #598 is paused (#599 decision point 3); sizing new targets needs the D3 layout. Not on the verdict path of this issue. Responsible: the #598 planning session after D3. Blocks: #598 only.

## 13. Plan-check questions

(a) Steps whose effort scales with a number unknown at planning time: the artifact downloads of A3 and B1 (bounded by the artifacts of two named runs; 178 in the reference main run and 146 in its companion by matrix size; A3 records the gap and continues, B1 pushes -wip and hands off); the number of methods kept under R13 (bounded by the 12 of the list; each is handled by the same rule, and the rerun is one script run on reports already downloaded). Timing runs are fixed at two per build. No other step scales.

(b) Stricter than the parent: there is no parent plan. Against the issue body: R12 makes the proof a gate before PR 2 is opened, where the issue says "before relying on it"; R13 fixes what happens to a test that the proof flags, as the issue says ("that test then returns to the mutation suite"); the list has 12 methods, not the issue's 14 (section 4, Q2).

(c) Checks that depend on code, data or behaviour neither on `main` nor in the pull requests: B1 depends on a post-601 campaign that does not exist yet (D1, Q1); R3 and the section 3 field shapes depend on Stryker's report, checked against recorded output of one hosted artifact. Listed in section 8.

## 14. Size estimate

Estimates, 2026-11-27, basis: the file structure above and the #601 pull request's size; not measured.

- PR 1: about 4 files, about 550 changed lines (script about 200, tests about 300, list 14, runbook about 35).
- PR 2: about 20 files (9 edited test files, about 9 new files, the list, the qualification file and the runbook), about 1,200 changed lines, most of them the moved test bodies counted twice (removed and added).

Size is reported, not enforced.

## 15. Open questions for Hari

Q1. **Dispatch for the proof.** May the control session dispatch one paired C5 campaign (`verifier-c5-mutation.yml` with its companion) on `main` at a commit that meets R12, to supply the post-601 kill sets? Recommendation: yes, and let it double as the batched acceptance run for the 601 change, so one dispatch serves both. Alternative: wait for the next publication point's acceptance run; PR 2 waits with it.

Q2. **Test set.** Do you accept the 12 methods of section 4 in place of #600's 14? Recommendation: yes; #600's list is not in the repository, and this one was measured at the base commit on the runner the campaign uses (MTP-compatible xUnit v3 runner, not VSTest). Alternative: ask the #600 session's owner to recover its list, or set a different cut.

Q3. **Equivalence-check cadence** (the issue reserves it to you). Options: (a) a full-suite campaign before each publication point, which roughly doubles that point's runner time while D3 is pending (estimate, from #600's 530743 s full campaign); (b) one whenever a pull request changes a target or a test file; (c) none after PR 2, accepting the gap of section 11. Each of (a) and (b) needs D2. Recommendation: (b), because the gap opens only when tests or targets change. Your answer sets D2's scope; PR 1 and PR 2 do not depend on it.

Q4. **Sizing diagnostic for D3.** May a session dispatch `verifier-c5-sizing.yml` on `main` after PR 2 merges, to time the fast suite per shard? Recommendation: yes; without it D3 cannot be planned. Each such dispatch and any layout outside 178 and 146 jobs still comes back to you.

## 16. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract of section 3, a requirement, the list other than under R13, or the scope of section 2; when the proof's campaign does not exist or does not meet R12; when the script fails with a failure name on real reports; when `renew` refuses; or when any required check fails for a reason it cannot fix inside the allowed files.

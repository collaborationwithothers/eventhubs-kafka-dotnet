# Issue 333 receipt

This pull request adds the connection class `timed_out_on_resume` to the idle-observation judge, lets an idle-observation dispatch name an ordered subset of A02, A03 and A04, and splits the two longest C5 mutation targets into more span jobs. It dispatches nothing and publishes no verdict.

## Execution card

- Deliverable: one pull request with changes C1 to C31 of the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/333#issuecomment-6596783115), Revision 2: 29 changed files from C1 to C29, `items/333/state.md` (C30) and this receipt (C31).
- Authorization: plan Revision 2 (`updated_at` 2026-11-03T06:58:37Z) approved under standing approval by [plan check 6597891329](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/333#issuecomment-6597891329) (`Blocking findings: none`), with [decision 6596840872](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/333#issuecomment-6596840872), which adopts the #301 decisions the plan lists.
- Mandatory gates: acceptance commands 1 to 11 of the plan, the cold review under AGENTS.md "Before opening a pull request" rule 8, the required checks and Codex review.
- Publication checkpoint: after the first cold review reports no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: its review-budget handoff to Hari.

## Predicate map

| Predicate | Below, at and above the boundary; empty and precedence | Test |
|---|---|---|
| Resume timeout sequence on one key | same report; spread in order; connect at `R2` (480000) counts; connect after `R2` (495000) does not; timeout at `R1` (420000, idle window) does not; connect before disconnect, disconnect before timeout, and each of the three absent, do not | `Given_ResumeTimeoutDisconnectAndConnect_When_ReportOrderVaries_Then_OnlyThatOrderOnOneKeyIsTimedOutOnResume` (10 cases) |
| Same key | timeout on one key, disconnect and connect on another | `Given_TimeoutOnOneKeyAndReconnectOnAnother_When_Judged_Then_ConnectionIsKept` |
| Absent key counts as 0 | key first seen in the resume window with all three at 1 | `Given_KeyFirstSeenInResumeWindowWithTimeoutDisconnectAndConnect_When_Judged_Then_AbsentCountsAsZero` |
| Class precedence | resume `idle_timed` with the sequence; idle-window `idle_timed` with the sequence | `Given_ResumeIdleTimedDisconnectWithTimeoutAndReconnect_When_Judged_Then_StaleOnResumeTakesPrecedence`, `Given_IdleTimedCloseAndResumeTimeoutSequence_When_Judged_Then_ClosedWhileIdleTakesPrecedence` |
| Survival | three closing classes mixed give `not_kept`; one `timed_out_on_resume` with four `kept` gives `unequal` | `Given_EveryClosingClass_When_ConfigurationIsSummarised_Then_SummaryIsNotKept`, `Given_FourKeptAndOneTimedOutOnResume_When_ConfigurationIsSummarised_Then_SummaryIsUnequal` |
| Existing short resume disconnect with a timeout and no connect stays `kept` | unchanged test | `Given_ResumeDisconnectWithShortQuietPeriod_When_Judged_Then_ItIsOtherCauseAndTimeoutIsRecorded` |
| Judge `Items` | `A02,A03,A04`, `A02,A04`, `A03` accepted; `A04,A02`, `A02,A02`, `A02,`, empty, `a02`, `A05` rejected; missing leaf rejected (existing `RequiredLeaves` theory) | `Given_Items_When_Validated_Then_OnlyANonEmptyOrderedSubsetIsAccepted` |
| Capture pods within selected items | A02 and A04 pods accepted; an A03 pod with `A02,A04` rejected | `Given_SelectedItems_When_CapturePodsAreValidated_Then_OnlyPodsOfSelectedItemsAreAccepted` |
| Judge writes selected items only | `A02,A04` gives two candidates and exit code 0 | `Given_SelectedItemsA02AndA04_When_Judged_Then_OnlyTheirCandidatesAreWritten` |
| Phase admission | `all`, `A02`, `A03`, `A02,A04`, `A02,A03,A04` accepted; `A04,A02`, `A02,A02`, `A02,`, `a02`, `A05`, `ALL`, empty, unset rejected before side effects | `test_idle_observation_admits_only_the_smoke_and_batch_tuples_before_side_effects`, `test_idle_observation_preparation_freezes_an_ordered_item_subset` |
| `batch-stop` gate and select step | `A02`, `A02,A04`, `A03` admitted; `A04,A02`, `A02,A02`, `A05`, empty refused | `test_capture_validation_and_actual_stop_prerequisites`, `test_idle_observation_routes_only_to_batch_and_batch_stop`, the stop-condition literals |
| `judge_items` runtime and chart | all items, `A03`, `A02,A04`; schema refuses missing and `A04,A02` | `test_judge_values_*`, chart judge cases and negatives, loadgen host `items` regression |
| C5 counts and spans | 8 and 3 parts on the synthetic source; 18 matrix rows; rebalance message `part 5 of 8`, span 104..130 | `test_c5_workflow` literal, span-config, combine and rebalance tests |

Unreachable: a sequence whose disconnect is `idle_timed` is always `stale_on_resume` or `closed_while_idle` first, by precedence.

`docs/operations/evidence/issue-321-a03-verdict/make_results.py` pins the SHA-256 of `scenarios/A03/expected.yaml` (L122) and reads that file from the working tree, so after C27 it reproduces only at the commit where it was merged; no check runs it. `results/A03/2026-11-02.json` also records `expected_yaml_sha256` (L305), which equals the hash of that file on `main` before this pull request and no longer matches the head file; the published A03 verdict is unchanged, and no check reads that field.

## Commands and results

Branch `claude/issue-333-idle-resume-timeout` cut from `main` at `e921edeca6a0c47090cccdf3f752b344da60370b`. Every command ran on the tree of commit `9eb0474` (C1 to C30), which differs from the final head only by this receipt. The session used SDK 10.0.402, helm, kubeconform, actionlint and shellcheck from the pinned tools set, PyYAML 6.0.3 and CPython 3.13.18 (the plan names CPython 3.12; the session's venv was 3.13).

1. `dotnet build Parchwell.slnx -c Release`: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
2. `dotnet test --project tests/Parchwell.Verifier.Tests -c Release --no-build --filter-namespace Parchwell.Verifier.Tests.IdleObservation`: `total: 334`, `failed: 0`, `succeeded: 334`. The count on `main` is 305, so the difference is 29, as the plan states. With `--filter-not-namespace Parchwell.Verifier.Tests.Component`: `total: 4993`, `failed: 0`.
3. `./scripts/verify-dotnet.sh`: locked restore, solution build, Kafka XML inventory, architecture tests (15 passed) and enrichment tests (56 passed) passed. The stage `Parchwell.Kafka.Tests tests` failed (exit 2) on one test, `PhantomSpikeTests.Given_ApprovedFaultMatrix_...`, with `System.NullReferenceException` in `ExperimentDockerAuthentication.get_Endpoint()` under Testcontainers: the session has no usable Docker endpoint. The script stops at that stage, so the later stages (the remaining unit test projects with their reports, and the warning-as-error control) did not run through the script. Run by hand on the same tree: `Parchwell.Scenarios.Tests` 297 passed, 0 failed; `Parchwell.Kafka.Tests` without the `PhantomSpike` namespace 463 passed, 0 failed; `Parchwell.Verifier.Tests` without `Component` 4993 passed, 0 failed. `Parchwell.LoadGen.Tests` (4 failed of 815) and the `Component` namespace of `Parchwell.Verifier.Tests` (12 failed) fail on the same Testcontainers Docker error; none of them is in a file this pull request changes. The hosted `dotnet-verification` run on the pull request is cited for the Docker-dependent tests and the warning-as-error control.
4. `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`: `Ran 277 tests`, `OK`. `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'`: `Ran 229 tests`, `OK`. Both include `test_committed_scenario_items_map_passes_check_workflows`.
5. `cd tests/Parchwell.Verifier.Tests/Mutation && python3 -m unittest test_c5_workflow`: `Ran 63 tests`, `OK`.
6. `python3 infra/scripts/lifecycle/command.py 600 -- bash infra/scripts/lifecycle/check-offline.sh` with `PARCHWELL_TOOLS_DIRECTORY` set: exit 0, printing `idle-observation chart acceptance passed: 8 positive, 8 negative, 3 judge positive, 5 judge negative`.
7. `python3 infra/scripts/lifecycle/command.py 900 -- bash charts/parchwell/tests/check-loadgen-host.sh`: exit 0, printing `idle-observation judge host checks passed: 3 rendered environments, 5 rejected input regressions`.
8. `cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt`: exit 0, no output.
9. A script loading `scenarios/<item>/expected.yaml` at `origin/main` and at the head with `yaml.safe_load`: for A02, A03 and A04, `hypotheses`, `verdict.decision`, `verdict.precedence` and `verdict.no_verdict_when` equal; the changed `assessment_rules` keys are exactly `connection_classes`, `recorded_not_selecting`, `resume_timeout_sequence`, `rule_history` and `survival`. A03's whole `verdict` mapping is equal (`text_must_state` unchanged); for A02 and A04 only `verdict.text_must_state` differs, by edits 26.3 and 28.3.
10. `grep -nP '[^\x00-\x7F]'` over every changed file: no output (exit 1 from grep, no match).
11. `npm ci --ignore-scripts && npm run check:docs`: `8 warnings`, in `docs/agents/factory.md`, `docs/operations/evidence/issue-124/README.md`, `docs/operations/evidence/issue-140/README.md` and `docs/operations/evidence/issue-211/receipt.md`, none in a file this pull request changes (blanket line, rule 8).

## Deviations from the plan

- Edits 23.2 and 25.4 insert text that starts with a space and continues the line they follow; they are applied inline, not on a new line.
- The configuration summary rule in verifier-contract.md and one sentence in verifier-mutation.md are corrected after cold review round 1 (see "Cold review").
- Acceptance 3 is cited to the hosted run for the stages that need Docker, as the plan allows.

## Cold review

Round 1 (comprehensive, pass 1 of 2): input revision b67c561, comparison revision origin/main e921edeca6a0c47090cccdf3f752b344da60370b, reviewer model claude-opus-5-5, specification sources as listed under "Execution card". Findings and dispositions:

- P0 on `scenarios/A02/expected.yaml`, `scenarios/A04/expected.yaml` and `scenarios/A03/expected.yaml` (expected-outcome edits): kept, because the approved plan requires them (decision 6596426514 point 2 authorizes A02 and A04; the plan decides A03). Listed in the pull request description for Hari under rule 8, third paragraph.
- P1 `docs/operations/verifier-contract.md`, configuration summary rule: fixed, `timed_out_on_resume` added to the `not_kept` list. Deviation from the plan.
- P1 `docs/operations/verifier-mutation.md`, claim that more spans would not shorten the longest one: fixed, limited to the two `SingleAction` files.
- P2 this receipt, `results/A03/2026-11-02.json` hash: fixed above.

Focused correction round 1 follows on the fixes.

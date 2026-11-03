# Issue 340 receipt

This pull request makes each C5 job test every mutant of a `static readonly` field declaration in its own Stryker run, so a static value set by one mutant can no longer make other mutants look killed. It also corrects the "Issue 267 span shards" claim that boundary placement never changes the score. It dispatches nothing and publishes no verdict.

## Execution card

- Deliverable: one pull request with changes C1 to C5 of the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/340#issuecomment-6603225182), Revision 2: `c5_workflow.py`, `test_c5_workflow.py`, `docs/operations/verifier-mutation.md`, `items/340/state.md` and this receipt.
- Authorization: plan Revision 2 (`updated_at` 2026-11-03T12:04:33Z) approved under standing approval by [plan check 6603619206](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/340#issuecomment-6603619206) (`Blocking findings: none`; its three P2 findings marked `apply in implementation` are applied), with [decision 6602486682](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/340#issuecomment-6602486682), which adopts the #301 [override](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563515099), [verdict and bypass](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563540913), [recommendations](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6569906599) and [hold](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6602480631) decisions. The issue has no `ready-for-agent` event of its own; its listed parent #301 carries `ready-for-agent`, labelled 2026-11-01T20:30:48Z, and the hold decision states that it "authorizes planning and implementing that child", and the implementing tool is Claude Code by default.
- Mandatory gates: acceptance A1 to A6 of the plan, the cold review under AGENTS.md "Before opening a pull request" rule 8, the required checks and Codex review.
- Publication checkpoint: after the first cold review reports no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: its review-budget handoff to Hari.

## Predicate map

| Checks | Tests in `test_c5_workflow.py` |
|---|---|
| 1 to 11, 13 to 16 via aggregation (9 by `config-hash`, 11 by `command-log-hash`, the target source part of 12 by `source-hash`) | `test_static_isolation_failures_are_incomplete` (subtests `region`, `isolation-error`, `tested-in-main`, `missing-entry`, `malformed`, `plan`, `child`, `config`, `report-tamper`, `another-mutant`, `config-hash`, `command-log-hash`, `source-hash`); passing: `test_static_mutants_merge_from_their_isolated_invocations` |
| 13 to 16, each disposition | `MergeIsolatedTests` |
| 17; region edges 208, 209, 248, 249 | `StaticRegionTests`, `IsolationPlanTests` |
| runner errors (including the OSError path, subtest `isolation-oserror`), deadline, log move | `test_static_isolation_failures_fail_the_shard`, `test_static_region_mutants_run_in_their_own_invocations` |
| exit code 2 accepted for an isolated invocation (check 7) | `test_isolated_invocation_with_a_surviving_mutant_may_exit_with_code_2` |
| committed targets | `test_committed_static_regions_each_lie_inside_one_span` |

Check 12 is the existing `report_records`, covered by `test_report_scope_schema_and_status_failures`.

## Commands and results

Branch `claude/issue-340-static-field-isolation` cut from `main` at `69102fe874698ab16839fe789b0bb54f158d535a`. A1 to A5 ran on commit `236e589` (the code and documentation after cold review rounds 1 and 2, with `items/340/state.md`), which differs from the final head only by this receipt. The proof ran on the earlier commit `9a29c4f`; `git diff 9a29c4f 236e589` changes one code comment in `c5_workflow.py`, one sentence of `verifier-mutation.md` and `test_c5_workflow.py`, and adds this receipt; it changes no runner logic.

1. A1. `sha256sum`: `c5_workflow.py` `c27583891677d9e6a381de06401134daf6722fd86e0b89610558ded3f45edf91`, `test_c5_workflow.py` `d6039eb6058d587d115c1efc56dccb199f0427091aacb4e6f0386764c4386ebc`, `verifier-mutation.md` `8f081a4ad59221ed6b98414af8e88ab9e0970cd041229ed6c39c84f4347e50ca`. These differ from the plan's `c88f74f8...`, `535c8a37...` and `cb2c28b1...` by the plan check's three marked P2 fixes and the cold review corrections listed under "Deviations from the plan".
2. A2. `cd tests/Parchwell.Verifier.Tests/Mutation && python3 -m unittest test_c5_workflow`: `Ran 78 tests`, `OK` (77 before the exit code 2 test; 63 on `main`).
3. A3. `npm ci && npm run check:docs`: `no issues found` for `docs/operations/verifier-mutation.md`, `docs/operations/evidence/issue-340/receipt.md` and `items/340/state.md`; `8 warnings`, in files this pull request does not change (blanket line, rule 8).
4. A4. `grep -nP '[^\x00-\x7F]'` over the five files: no output (grep exit 1).
5. A5. `git diff --name-only origin/main...HEAD` lists exactly the five files: `docs/operations/evidence/issue-340/receipt.md`, `docs/operations/verifier-mutation.md`, `items/340/state.md`, `tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py`, `tests/Parchwell.Verifier.Tests/Mutation/test_c5_workflow.py`.
6. A6. Proof run: see below.

## Proof run

Diagnostic, not acceptance. SDK 10.0.402 from `dotnet-install.sh` in `/opt/c5-340/dotnet`, `dotnet tool restore` (dotnet-stryker 5.0.0), clean tracked checkout at commit `9a29c4f481b905d585e6c70eee166ab785c82ff2`, the plan's script run with `python3 -I`. Real Stryker ran; one run, no retry. Elapsed 1589.1 s (the plan estimated about 1900 s).

Structural expectations of the plan, all met: `result` 0; `static_regions` [[534, 603], [603, 690], [690, 762]]; `isolation_error` null; isolated ids 7340 to 7347 in order, each with `exclusions` `[]` and exit 0 or 2; `owned` 62; `static_tested_in_main` `[]`; each isolated log lists only its own id; CompileError 101; Killed + Timeout + Survived + NoCoverage + RuntimeError = 54 (54).

| Mutant | Span | Exit | Seconds | Status |
|---|---|---:|---:|---|
| 7340 | 581..586 | 0 | 123.2 | Killed |
| 7341 | 588..593 | 0 | 114.6 | Killed |
| 7342 | 595..600 | 0 | 103.7 | Killed |
| 7343 | 653..662 | 0 | 106.3 | Killed |
| 7344 | 664..676 | 0 | 105.2 | Killed |
| 7345 | 678..687 | 2 | 106.1 | Survived |
| 7346 | 748..753 | 2 | 116.8 | Survived |
| 7347 | 755..759 | 2 | 115.6 | Survived |

Recorded, not a gate: the main invocation's status counts were {"Killed": 48, "Timeout": 0, "Survived": 6, "NoCoverage": 0, "CompileError": 101, "RuntimeError": 0, "Ignored": 408}; the tests failing in every kill were {"MtpRunner-0": 0, "MtpRunner-1": 0} per runner, against 23 in campaign 41602164797. Over the 62 owned mutants, Killed 53 and Survived 9, where that campaign reported 62 Killed. Inference, as the plan states: this is consistent with the mechanism, because the shared static state no longer makes every mutant look killed. It is one local run of one job and is not a measurement of the campaign.

## Deviations from the plan

- The plan says to commit C1 to C5 before the proof. The proof ran on the commit holding C1 to C3 and C5; this receipt (C4) is committed after it, so it records the proof without a receipt-only amend (rule 8).
- `verifier-mutation.md` differs from the plan's script output in three marked P2 replacements from the plan check (`apply in implementation`) and one cold review correction: the new section's first paragraph says the survivor comparison "inferred from its measured logs and reports", not "measured", because that comparison labels the mechanism an inference.
- `c5_workflow.py` differs from patch P1 in one comment, which now says that logs left by a failed move stay in `isolated/ID/stryker/logs/` and are carried by the evidence upload.
- `test_c5_workflow.py` adds `test_isolated_invocation_with_a_surviving_mutant_may_exit_with_code_2` (plan "Enforced checks" 7) and, in cold review round 2, the subtests `config-hash` (check 9), `command-log-hash` (check 11), `source-hash` (check 12, target source part) and `isolation-oserror` (the runner's fifth failure message). Each was checked by replacing the code it covers with a no-op and running the test: it failed in each case.

## Cold review

Round 1 (comprehensive, pass 1 of 2): input revision `9a29c4f` plus the receipt commit `8d01825`, comparison revision `origin/main` 69102fe874698ab16839fe789b0bb54f158d535a, reviewer model claude-opus-5-5 (pinned by the `repo-reviewer` definition; spawned without a `model` parameter), specification sources as listed under "Execution card". Report complete. Findings: 2 at P0 or P1, 3 at P2.

- P1, `verifier-mutation.md` new section: said the survivor comparison "measured" the static-state mechanism. Fixed as above.
- P1, receipt: A3 to A5 were not recorded. Recorded above.
- P2, `c5_workflow.py` log-move comment was false. Fixed.
- P2, receipt named an input revision (27d8dff) that the amended history no longer holds. Corrected above to `9a29c4f`.
- P2, no test for exit code 2 in an isolated invocation. Test added.

Round 2 (comprehensive, pass 2 of 2): input revision `7373f7b` plus the receipt commit `88e0546`, comparison revision `origin/main` 69102fe874698ab16839fe789b0bb54f158d535a, reviewer model claude-opus-5-5 (spawned without a `model` parameter). Report complete. Findings: 1 at P0 or P1, 1 at P2.

- P1, plan "Enforced checks" 9, 11 and the target source part of 12, and the runner's fifth failure message, were named in the predicate map but had no test. Tests added; the map names them.
- P2, the authorization line did not say that the listed parent #301 carries `ready-for-agent`. Corrected above.

Focused round 1 of 2 (rule 8): input revision `236e589` plus this receipt, comparison revision `7373f7b` (the revision round 2 reviewed), full pull request diff as context, reviewing the round 2 corrections only.

Focused round 1 report: complete, no P0 or P1, one P2 (the sentence above said `git diff 9a29c4f 236e589` changed only three files, and it also adds this receipt). Fixed above, as prose in a file already in the diff.

Focused round 2 of 2 (rule 8, P2-correction round, reports only P0 or P1): input revision `236e589` plus this receipt, comparison revision `befa40d` (the receipt reviewed in focused round 1), full pull request diff as context.

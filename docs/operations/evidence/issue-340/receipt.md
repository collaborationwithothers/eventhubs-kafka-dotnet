# Issue 340 receipt

This pull request makes each C5 job test every mutant of a `static readonly` field declaration in its own Stryker run, so a static value set by one mutant can no longer make other mutants look killed. It also corrects the "Issue 267 span shards" claim that boundary placement never changes the score. It dispatches nothing and publishes no verdict.

## Execution card

- Deliverable: one pull request with changes C1 to C5 of the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/340#issuecomment-6603225182), Revision 2: `c5_workflow.py`, `test_c5_workflow.py`, `docs/operations/verifier-mutation.md`, `items/340/state.md` and this receipt.
- Authorization: plan Revision 2 (`updated_at` 2026-11-03T12:04:33Z) approved under standing approval by [plan check 6603619206](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/340#issuecomment-6603619206) (`Blocking findings: none`; its three P2 findings marked `apply in implementation` are applied), with [decision 6602486682](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/340#issuecomment-6602486682), which adopts the #301 [override](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563515099), [verdict and bypass](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563540913), [recommendations](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6569906599) and [hold](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6602480631) decisions. The issue has no `ready-for-agent` event; the hold decision states that it "authorizes planning and implementing that child", and the implementing tool is Claude Code by default.
- Mandatory gates: acceptance A1 to A6 of the plan, the cold review under AGENTS.md "Before opening a pull request" rule 8, the required checks and Codex review.
- Publication checkpoint: after the first cold review reports no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: its review-budget handoff to Hari.

## Predicate map

| Checks | Tests in `test_c5_workflow.py` |
|---|---|
| 1 to 11, 13 to 16 via aggregation | `test_static_isolation_failures_are_incomplete` (subtests `region`, `isolation-error`, `tested-in-main`, `missing-entry`, `malformed`, `plan`, `child`, `config`, `report-tamper`, `another-mutant`); passing: `test_static_mutants_merge_from_their_isolated_invocations` |
| 13 to 16, each disposition | `MergeIsolatedTests` |
| 17; region edges 208, 209, 248, 249 | `StaticRegionTests`, `IsolationPlanTests` |
| runner errors, deadline, log move | `test_static_isolation_failures_fail_the_shard`, `test_static_region_mutants_run_in_their_own_invocations` |
| committed targets | `test_committed_static_regions_each_lie_inside_one_span` |

Check 12 is the existing `report_records`, covered by `test_report_scope_schema_and_status_failures`.

## Commands and results

Branch `claude/issue-340-static-field-isolation` cut from `main` at `69102fe874698ab16839fe789b0bb54f158d535a`. Every command below ran on commit `9a29c4f` (C1 to C3 and C5), which differs from the final head only by this receipt.

1. A1. `sha256sum`: `c5_workflow.py` `c88f74f8ddf6b7029478209bf17ac18db4f9321f4bbe580bd5ecd86133abb002`, `test_c5_workflow.py` `535c8a37b9c4848cd851dd41606757c38e604ee22e3f09ebc8530c001b5bb3e0`, `verifier-mutation.md` `2529412f7544ad331aff8c71e49182c12821501a772e68ebd7f3a676c0821c78`. The first two equal the plan. The third is the digest the plan check gives after its three marked P2 fixes, not the plan's `cb2c28b1...`.
2. A2. `cd tests/Parchwell.Verifier.Tests/Mutation && python3 -m unittest test_c5_workflow`: `Ran 77 tests`, `OK`.
3. Proof run: see below.

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
- A1's third digest and the docs script differ from the plan's `cb2c28b1...` by the three P2 replacements marked `apply in implementation` in the plan check.

## Cold review

Round 1 (comprehensive, pass 1 of 2): input revision is commit 27d8dff plus this receipt, comparison revision `origin/main` 69102fe874698ab16839fe789b0bb54f158d535a, specification sources as listed under "Execution card" and the plan's own text (plan Revision 2, plan check 6603619206).

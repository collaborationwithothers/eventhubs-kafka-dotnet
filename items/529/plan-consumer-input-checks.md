# Plan: consumer input checks (#529)

Revision 4. Base commit: `2158a93bff0cf8b0fcd1de9ae13f776291342b19` on `main`.

This plan adds one check to the consumer verifier: a consumer record that ends before rows the start line already saw makes the repeat invalid. It merges before the scientific C10 and C01 runs, so the counts their verdicts rest on are covered. The second finding, configured row limits and the input-entry budget, gets no change, by Hari's [decision 7075872795](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/529#issuecomment-7075872795).

## 1. Goal and authority

Goal: settle the two hosted P2 findings that issue #529 records, so the consumer counts that the C10 verdict (#512) and the C01 verdict (#513) will rest on have a decided integrity rule.

Authority, in creation order:
- The body of #529 (updated 2026-11-16T10:37:38Z). It states that the issue "does not authorize source changes or a consumer evidence pack", and asks for "an explicit plan/scope decision" on each check.
- #529 [decision 7075872795](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/529#issuecomment-7075872795) (Hari, 2026-11-22, "all recommended" on revision 2): Q1 to Q5 answered; Q3 is "no change" and the entry-budget pull request is dropped; DA1 accepted by name; the C03 result is not rechecked; #529 adopts the batch standing decision 6895535291; the fixture rule of section 6 replaces #506 decision 6899696796 point 4(b); this decision authorizes the source changes of the pull request; P2-a, P2-b and P2-c of [check 7075838247](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/529#issuecomment-7075838247) are `apply in implementation`.
- Its parent items #506 and #499. Their decision and approval comments were read with `scripts/decision-log.py --issue 529 --issue 506 --issue 499` (exit 0, 49 comments). The ones this plan depends on:
  - #499 [6895299617](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895299617) point 7 (invalid run), as written into ADR-022 at docs/blueprint.md lines 777 to 787.
  - #506 [approval 6901013311](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/506#issuecomment-6901013311) of the [consumer-verifier plan, revision 5](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/a4c5ea1bec8630359a31d9b096416b2204f85dc7/items/506/plan-consumer-verifier.md), commit `a4c5ea1bec8630359a31d9b096416b2204f85dc7`: section 3.2, section 3.6, rule 2a, rule 4 and rule 5.
  - #499 [6895535291](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291), the batch standing decision. Adopted on #529 by decision 7075872795 point 5.
- docs/agents/pull-requests.md rule 10: a P2 that asks for a check the approved plan does not list is `Out of contract:` until Hari decides whether the check joins a plan.

The source pull request [#526](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/526) merged on 2026-11-16 as `e8da5826deba9b42bbf5224c835ad728768544ef`. Since the reviewed source `61b8c3c312d8e9eccc4427b38e2b50d1312dd064`, `ConsumerArtifactReader.cs` is unchanged, and `ConsumerJudgement.cs` gained two outputs that read the start snapshot: the C03 `membership` block and the C10 `subject_commit_coverage` block from [#573](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/573) (merged 2026-11-21 as `ac14419f5afb6a04251e0137ca38209614482c28`).

This plan decides nothing that its authorities leave open. Section 11 states `None`.

## 2. Evidence from `main` at the base commit

### 2.1 Snapshot frontier (finding 1)

- The start snapshot records, per member, `first_read_last_row_sequence` and `second_read_last_row_sequence`. The codec requires `-1 <= first <= second` (`src/Parchwell.Verifier/Common/Consumer/ConsumerArtifactCodec.cs` line 59).
- The start check is `ValidStart` (`src/Parchwell.Verifier/VerifyRun/Consumer/Judgement/ConsumerJudgement.cs` lines 116 to 125). It requires an `assigned` row at or below `first`, no `partition_event` row above `first` and at or below `second`, and exactly one holder per window partition at `first`. Nothing requires the final record to reach `second`. A final record whose last row is below `second` passes both row conditions vacuously.
- Three outputs replay the record up to `first`: `ValidStart` line 124, `Membership` lines 133 to 134 (C03), and `SubjectCommitCoverage` lines 177 to 178 (C10, from #573). Each would silently replay the shorter record.
- The record reader checks contiguity from row 0 and agreement with the completion receipt (`ConsumerArtifactReader.cs` lines 37, 41 and 48). A record that is contiguous and short, with a matching receipt, passes it.
- ADR-022 already makes such a record invalid: "a member's record is missing, or has a gap or a repeated row sequence number" (docs/blueprint.md line 779). Rows the start line read and the final record lacks are missing rows. The check therefore has a source under the closed check list of docs/blueprint.md line 605, and is not a new kind of check.

How much this matters for counts is inference, not tested. Counts need `complete_valid`, which needs the commit agreement of `ConsumerJudgement.cs` lines 59 to 67: for every window partition with messages, the broker's committed offset must equal an offset in some member's commit rows and be at or past the end line. A member record cut off before the start line holds no commit rows written after the load began, so in the realistic case this agreement already fails. Two things weaken that argument. C01 sets `kill_count: 1` (`scenarios/C01/scenario.yaml` lines 39 and 41), so its subject legitimately has no processed row before its kill-intent row, and the stop check (`ConsumerJudgement.cs` line 49) gives no protection for the subject. And the agreement uses the commit rows of both members together, so it does not tie each member's record to its own frontier.

The writer appends only. It creates each record blob create-only and appends at a conditioned position (`src/Parchwell.Enrichment/ConsumerProcessing/BlobConsumerRecordStore.cs` lines 31 to 33 and 44 to 46), and it has no delete. So in a correct run the new check should never fire (DA1, section 7).

### 2.2 Configured row limits and the entry budget (finding 2)

Decided: no change (decision 7075872795 point 3). An input resource bound needs a source in ADR-019's closed check list (docs/blueprint.md line 605), and none exists. This section stays as the record of the evidence behind that answer.


- The consumer reader enforces the wire limits: 65536 bytes per row and 40000 rows per blob (`src/Parchwell.Verifier/Common/Consumer/ConsumerRecordLimits.cs` lines 6 and 7; `ConsumerArtifactCodec.cs` line 30; `ConsumerArtifactReader.cs` line 41). It also enforces `MaxArtifactBytes` per artifact and charges every artifact's bytes to the shared `InputBudget` (`ConsumerArtifactReader.cs` lines 62 to 64). It never calls `InputBudget.ConsumeEntries` and never reads `InputLimits.MaxRowBytes` or `InputLimits.MaxRowsPerArtifact`.
- Other verifier readers charge entries, for example `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` line 35 and `src/Parchwell.Verifier/Judge/JudgeHandler.cs` line 42.
- `consumer-verify` passes the `verify` job's shared budget to the reader (`src/Parchwell.Verifier/VerifyRun/Consumer/ConsumerRepeatExtension.cs` line 20). The chart's `verifier_verify` bounds are `MaxArtifactBytes: 268435456`, `MaxTotalInputBytes: 1073741824`, `MaxRowBytes: 16777216`, `MaxRowsPerArtifact: 372000` and `MaxInputEntries: 4194304` (`charts/parchwell/values.yaml` lines 82 to 87).
- The configured row bounds are larger than the wire limits: 16777216 against 65536 bytes per row, and 372000 against 40000 rows per blob. Applying the smaller of each pair would change nothing in the deployed configuration.
- A configured `MaxRowsPerArtifact` below 40000 would conflict with the wire rule that every record blob before the last holds exactly 40000 rows (approved section 3.2). Every valid record with more than one blob would then be unreadable.
- `start-line` builds its own budget with `MaxInputEntries ?? long.MaxValue` (`src/Parchwell.Verifier/StartLine/StartLineHandler.cs` lines 105 to 108). The chart does not give `start-line` that key (`charts/parchwell/tests/test_chart.py` line 41). An entry charge has no effect there in the deployed configuration.
- Scale. The C03 run published on 2026-11-19 (workflow run 44562323424) has 239 (C0) or 240 (C1) processed-once messages per repeat (`results/C03/2026-11-19.json`, `candidates.*.repeats[*].consumer.counts`). C10 uses the same profile, `scenarios/profiles/c03-consumer.yaml`, offered at 4 attempts per second for 60 s. C01 uses `scenarios/profiles/c01-consumer.yaml`, offered at 4 attempts per second for 150 s, which is 600 offered attempts. C01's actual count is not measured. Estimate, 2026-11-22, from those profile values and the C03 counts: consumer entries per C10 or C01 repeat are below 10000, well under 4194304.
- An exceeded bound already has a defined outcome. `InputLimitExceededException` is caught at `ConsumerArtifactReader.cs` line 57 and gives `consumer_record_unavailable`, which is `incomplete` with no counts (approved rule 2a).

## 3. Scope

One pull request, named "snapshot frontier check". Revisions 1 and 2 had a second pull request for the entry budget; decision 7075872795 point 3 dropped it.

Files allowed to change:
- `src/Parchwell.Verifier/VerifyRun/Consumer/Judgement/ConsumerJudgement.cs`
- `tests/Parchwell.Verifier.Tests/Consumer/ConsumerSnapshotFrontierTests.cs` (new)
- `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`, with the version 2 assessments of acceptance command 4
- existing files under `tests/Parchwell.Verifier.Tests/Consumer/`, only under the fixture rule of section 6

Paths that must not change: `scenarios/`, `results/`, `docs/blueprint.md`, `charts/`, `infra/`, `.github/`, `src/Parchwell.Verifier/Judge/`, `src/Parchwell.Verifier/Common/Consumer/`, every Stryker target source listed in `tests/Parchwell.Verifier.Tests/stryker-config.json`, and every file not listed above. The pull request adds no reason name, so the judge's closed reason list (`src/Parchwell.Verifier/Judge/ConsumerPhaseEvidence.cs` lines 29 to 35) and `docs/operations/verifier-contract.md` line 128 stay correct unchanged.

## 4. External contracts

- Reason names. The check reuses `consumer_record_invalid`. It adds no reason, block property or status.
- Diagnostic. One entry per failing member in the `consumer` block's `diagnostics` array: `reason` `consumer_record_invalid`, `member` the member's `instance_id`, `artifact` the member prefix, `row_sequence` the member's `second_read_last_row_sequence`, `field` null, `detail` exactly `record ends before start snapshot`.
- Kafka client settings, CLI flags, configuration keys, stage names and wire formats: unchanged.

## 5. Requirements

Enforced checks added:
1. Snapshot frontier. For each registered member that has an entry in the start snapshot, the member's final record reaches that entry's `second_read_last_row_sequence`.

Definitions. L(m) is the `row_sequence` of member m's last row in the final record that `consumer-verify` reads, or -1 when the record has no row. F(m) is m's `second_read_last_row_sequence` in `consumer-start.json`. Because the codec requires `first <= second`, L(m) >= F(m) also gives L(m) >= `first_read_last_row_sequence`.

- A1. WHEN rule 3 of the approved plan has not skipped evaluation, the snapshot is present and parsed, member m is registered, the snapshot has an entry for m's `instance_id`, and L(m) < F(m), THEN the consumer reasons include `consumer_record_invalid`, the status is `invalid`, the block has no `counts`, `membership`, `reassignment` or `subject_commit_coverage`, and the diagnostic of section 4 is added for m.
- A2. WHEN L(m) >= F(m) for every such member, THEN check 1 adds no reason and no diagnostic.
- A3. WHEN the snapshot is missing or unparsable, or has no entry for m, THEN check 1 does not evaluate m. Approved rule 5 already gives `consumer_start_assignment_invalid` in those cases.
- A4. WHEN rule 2 (`producer_side_not_complete_valid`), rule 2a (`consumer_record_unavailable`) or rule 3 (registration invalid) skips evaluation, THEN check 1 does not run, and the reasons are exactly those of the skipping rule. In code, check 1 sits inside the existing `if(!skip)` block of `ConsumerJudgement.cs` (line 32), outside `if(final)` (lines 55 to 69), as rule 4's `consumer_record_invalid` does at line 39 (check 7075838247 P2-a).
- A5. Check 1 runs in the preflight evaluation and in the final evaluation alike. It does not stop the offset read: `ShouldRead` stays as it is today, as for rule 4's `consumer_record_invalid`.
- A6. Every other rule, output and reason is unchanged.

## 5a. Invariants and safety rules

- No expected outcome, verdict or file under `results/` changes (AGENTS.md GOVERNANCE "Verdict integrity").
- No Kafka client setting, workflow, chart, infrastructure or scenario file changes (section 3, paths that must not change).
- The check adds no reason name, block property or status (section 4).
- The consumer record writer and the consumer record reader are not changed.
- Every existing test assertion stays, except as the fixture rule of section 6 permits.

## 6. Test map

Fixtures. The tests use `ConsumerJudgementFixture` (`F`) as built by `F.Create()` and `F.Read()` (`tests/Parchwell.Verifier.Tests/Consumer/ConsumerJudgementFixture.cs` lines 18 to 41 and 69). Each member there has 7 rows, row sequences 0 to 6, and a completion receipt; the snapshot is `subject (0, 0)` and `witness (0, 0)`. Every other field stays valid, so no other reason can mask the check.

| ID | Req | Input | Expected |
|---|---|---|---|
| T1 | A1, A2 | Witness snapshot `(0, 5)`, `(0, 6)`, `(0, 7)` | `(0, 5)` and `(0, 6)`: `complete_valid` with `counts`. `(0, 7)`: `invalid`, reasons exactly `["consumer_record_invalid"]`, no `counts` or `reassignment`, and one diagnostic `{reason: consumer_record_invalid, member: "witness", artifact: "run1/C1/r1/consumer/witness/", row_sequence: 7, detail: "record ends before start snapshot"}` |
| T2 | A1 | Witness snapshot `(6, 6)` and `(7, 7)` | `(6, 6)`: `complete_valid`. `(7, 7)`: `invalid`, reasons exactly `["consumer_record_invalid"]` |
| T3 | A1 | Subject snapshot `(0, 7)` | `invalid`, reasons exactly `["consumer_record_invalid"]`, diagnostic `member` `"subject"`, `row_sequence` 7 |
| T4 | A1, A2 | Witness record empty (`F.WithRows(member, [])` with its receipt); witness snapshot `(-1, -1)` and `(-1, 0)` | `(-1, -1)`: `invalid`, reasons exactly `["consumer_start_assignment_invalid"]`. `(-1, 0)`: `invalid`, reasons exactly `["consumer_record_invalid", "consumer_start_assignment_invalid"]` |
| T5 | A4 | Both registrations with role `subject`; witness snapshot `(0, 7)` | reasons exactly `["consumer_registration_invalid"]`; `ShouldRead` false |
| T5b | A4 | `F.Create() with { RootStatus = VerifierResultStatus.Invalid }`, witness snapshot `(0, 7)`, `Evaluate(request, F.Read())` | `invalid`, reasons exactly `["producer_side_not_complete_valid"]`, and no diagnostic whose `detail` is `record ends before start snapshot` |
| T6 | A5 | Witness snapshot `(0, 7)`, `Evaluate(request, F.NotAttempted(), false)` | `ShouldRead` true; reasons include `consumer_record_invalid` |
| T7 | A1, end to end | The `ConsumerSnapshotBoundaryTests` wire path, rebuilt in the new test file because its helper is private, with `withLaterRows: true` (witness rows 0 to 2); snapshot witness `(1, 2)` and `(1, 3)` | `(1, 2)`: consumer `complete_valid`. `(1, 3)`: consumer `invalid`, reasons exactly `["consumer_record_invalid"]` |

T1 to T3 cover below, at and above the boundary for both frontiers and both roles. T4 covers the empty record. T5, T5b and T6 cover precedence. The T4 reasons follow from approved rule 5 for the empty record; if the implementer observes other values, the plan's reasoning is wrong and section 12 applies.

Standing fixture rule (modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2). When an existing test's fixture is rejected by a new check of this plan, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it. The cold review and Codex review check each listed change. This rule replaces, for this plan only, the stop in #506 [decision 6899696796](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/506#issuecomment-6899696796) point 4(b) (decision 7075872795 point 5).

## 7. Deployment assumptions

- DA1. In a correct C10 or C01 run, each member's final record reaches its start-snapshot frontier, so check 1 does not fire. Evidence: the writer is create-only and appends at a conditioned position, with no delete (`BlobConsumerRecordStore.cs` lines 31 to 33 and 44 to 46). No live evidence was taken in this session, on any connection, idle or under load. No offline test covers the deployed storage container. If DA1 fails, a repeat is `invalid`: a finding that blocks the verdict, not a wrong count. Accepted by name in decision 7075872795 point 4.

## 8. Acceptance commands

Run on the pull request branch, in the order docs/agents/pull-requests.md "Verification gates" sets.

1. `./scripts/verify-dotnet.sh --project Parchwell.Verifier.Tests` while work moves (gate step 1). Evidence: pass counts, cited as a partial run.
2. Cold review under docs/agents/pull-requests.md rule 8 (gate step 2), with the implementation notes and any fixture changes.
3. `./scripts/verify-dotnet.sh --tier sandbox` once on the reviewed source (gate step 3). Evidence: the summary, recorded as a partial run excluding `Tier=CGroupV2`.
4. After committing the change, add to `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` one version 2 assessment for each changed shared input, in every timing context that uses its old hash, under docs/operations/verifier-mutation.md line 86. Each basis cites [decision 7076000200](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/529#issuecomment-7076000200). Commit, then run `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base <BASE_SHA> --head <HEAD_SHA> --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`. Evidence: `qualified` or `provisional`, with the changed paths under `assessed_inputs`.
5. A script that maps T1 to T7, T5b included, to at least one test and its result in the step 3 run. Evidence: one line per ID, all passed.
6. `git diff --name-only origin/main...HEAD`. Evidence: only section 3's paths.
7. `npm run check:docs`. Evidence: the summary line.
8. Hosted `.NET verification` on the final head (gate step 4), Codex review, and, if Codex implements, the Claude review gate of [decision 6894587233](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894587233) point 2.

## 9. Verdict gating

- The pull request merges before the scientific C10 and C01 dispatches (decision 7075872795 point 2), the runs whose results #512 and #513 would publish. The verifier image of a dispatch is built from the dispatched ref, which is `main` (`.github/workflows/scenario.yml` lines 88 and 96), so a later merge does not reach runs already made. Smoke runs, which give no verdict (`scenarios/C10/expected.yaml` line 67), may run before it. The C01 smoke dispatch is being held for another fix (#510 comment [7066935196](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-7066935196)); that hold is not this plan's gate.

## 10. Plan-check questions (docs/agents/factory.md "Item lifecycle" step 1)

(a) One step's effort scales with a number unknown at planning time: the existing tests whose fixtures check 1 rejects, handled under the fixture rule of section 6. This plan sets no cap, so AGENTS.md "Before opening a pull request" rule 11 applies to it. Estimate, 2026-11-22, from reading the existing tests that set snapshot frontiers : 0. The new test inputs are fixed. The mutation check is one command with a fixed input set.

(b) Stricter than its parent plan, the approved #506 revision 5: The pull request adds a condition to rules 4 and 5, which do not require the record to reach the snapshot frontier. Source: ADR-022 "Invalid run" (docs/blueprint.md line 779). The fixture rule of section 6 is looser than #506 decision 6899696796 point 4(b), by decision 7075872795 point 5.

(c) No test depends on code, data or behaviour outside `main` and the pull request. DA1 is about the deployed storage; Hari accepted it by name (section 7).

Plan rule 1 (external behaviour): this plan relies on no Kafka, librdkafka, Confluent.Kafka or Event Hubs behaviour. Every claim rests on repository files at the base commit. No documentation tool was needed.

## 10a. Deferred work

- Mutation acceptance. Evidence: the pull request changes a file listed as a shared input in `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`, which docs/operations/verifier-mutation.md line 10 makes mutation inputs. On the verdict path: yes, for the C10 and C01 evidence packs (docs/agents/factory.md "Evidence pack"). Why deferral is permitted: the run is dispatched on `main` only after the merge. Successor: the session that dispatches it on `main` after the merge, under the dispatch authority that applies then. Completion evidence: `summary.json` with `measurement_complete: true` and `break_passed: true`, naming a `main` commit whose history includes the merge. A later passing run that includes the merge also counts. Gate: the C10 and C01 evidence packs. Exhaustion: a failed or incomplete run opens a fix issue (docs/agents/factory.md "Evidence pack").

## 11. Open questions for Hari

None. Q6 of [handoff 7075955109](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/529#issuecomment-7075955109) is answered by [decision 7076000200](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/529#issuecomment-7076000200).

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope; when a pinned expected value in section 6 differs from what the code produces; or when an existing test needs a change the fixture rule does not permit.

## 13. Size estimate

Estimate, 2026-11-22, from the code read in section 2. Size is reported, not enforced.
- About 15 changed source lines and 200 test lines, in 2 files, plus the qualification file if renewed.

## 14. Sources

- docs/blueprint.md lines 605 and 777 to 787 (ADR-022 "Invalid run"), at the base commit.
- The approved [#506 plan revision 5](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/a4c5ea1bec8630359a31d9b096416b2204f85dc7/items/506/plan-consumer-verifier.md), sections 3.2 and 3.6 and rules 2a, 4 and 5.
- Hosted findings [r4583551983](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/526#discussion_r4583551983) and [r4583551989](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/526#discussion_r4583551989), as quoted in the #529 body (not read in this session).
- Repository files cited in section 2, at the base commit.

## 15. Changes since revision 1

Applied the three `apply in revision` findings of [plan check 7075838247](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/529#issuecomment-7075838247), with their literal texts: finding 2 (mutation acceptance in section 10a), finding 3 (new section 5a) and finding 4 (section 10 answer (a)). None is design-changing under docs/agents/factory.md "Item lifecycle" step 1: each writes down a gate or rule that an existing file already sets, or corrects an answer to match the plan, and none changes a gate, contract, requirement, scope, policy value, verdict rule or open question. The check's findings 1 and 5 and its P2s are unchanged and go to Hari.

## 16. Changes since revision 2

Applies [decision 7075872795](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/529#issuecomment-7075872795):
- Q1 and Q2: the frontier check and its verdict gate are stated as decided (sections 1, 9 and 11).
- Q3 and finding 5: the entry-budget pull request, its contracts, requirements B1 to B5, tests T8 to T12 and its deferred item are removed; section 2.2 records the decision. The pull request may not change `src/Parchwell.Verifier/Common/Consumer/`.
- Q4: DA1 moves to section 7 as accepted; the C03 recheck deferred item is removed.
- Q5: the route and the fixture rule are stated as adopted.
- Check 7075838247 P2-a is written into A4 and test T5b. P2-b and P2-c concerned only the dropped pull request.
- Check notes: the C03 counts cite both configurations; the verifier image claim cites `.github/workflows/scenario.yml`; T7 is placed in the new test file.

## 17. Changes since revision 3

Applies [decision 7076000200](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/529#issuecomment-7076000200), which accepts Q6 and adopts the three revision 4 texts of [handoff 7075955109](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/529#issuecomment-7075955109), with their literal texts: the qualification line of section 3, acceptance command 4 of section 8, and section 11. Nothing else changes. P2-d of [check 7075948056](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/529#issuecomment-7075948056) is `apply in implementation` by the same decision; its text is in the handoff, not in this plan.

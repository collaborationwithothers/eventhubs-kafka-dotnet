# Plan: consumer phase judge (#521)

Issue: [#521](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/521), child of [#499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499), split entry 11. Pull request name: `consumer-phase-judge`. This is the contract-grade plan for that one pull request under docs/agents/factory.md "Item lifecycle" step 3.

Revision 1. Base: `main` at `e8da5826deba9b42bbf5224c835ad728768544ef`, the merge of #526 (the #506 pull request). File paths and line numbers are on the base unless a line says otherwise.

This pull request lets the verifier's `judge` mode build a phase candidate for C01, C03 and C10. Today the judge rejects those scenarios at start, and it ignores the `consumer` block that `consumer-verify` writes into each repeat's `result.json`. After this change the judge checks that block, carries the consumer evidence of ADR-022 into each repeat of the candidate, and adds a phase-level consumer result. The pull request leaves `Judge/PhaseJudge.cs`, a C5 mutation target, unchanged, and puts the consumer phase logic in a new file (Q5). It changes only shared mutation inputs, so it renews the runtime qualification before merge and needs a new passing mutation run on `main` after merge.

## 1. Goal and authority

Goal: when #507 runs the judge Job for a C01, C03 or C10 phase, the judge publishes a candidate that a verdict pull request can use, with the consumer outcome of every repeat and of the phase. This gates #508's smoke run, whose judge step fails without it (issue body).

Design authority: ADR-022, docs/blueprint.md lines 735 to 794; ADR-019 lines 603, 605, 609 and 617; ADR-020 line 648; ADR-013 lines 449 and 458. The judge and candidate rules are owned by docs/operations/verifier-contract.md "C5 phase, blob and receipt contract" (lines 95 to 149), which this pull request amends (section 3.7).

Authorities on #521, in creation order (factory.md "Item lifecycle" step 3, child issues):

1. Issue body. Scope: "This issue extends the judge for C01, C03 and C10 so a candidate carries the consumer evidence of ADR-022." It requires "its own mutation measurement, a passing `verifier-c5-mutation.yml` run on `main` under docs/agents/factory.md \"Evidence pack\"". Predecessor PRs: the #506 pull request. "Planning, approval and the implementing tool follow the batch's decisions on #499." The body has no `Parent items:` line; it names #499 as the source of its split entry, so this plan reads #499 as its parent item (inference).
2. [Decision 6899940947](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/521#issuecomment-6899940947) adopts #499 [decision 6899939624](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6899939624): point 2 adds this pull request as split entry 11, with predecessor the #506 pull request, gating #508's smoke run; "The judge change stays out of #506."
3. [Decision 6899941170](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/521#issuecomment-6899941170) adopts #499 [standing decision 6895535291](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291) and [decision 6894614673](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894614673) points 1 and 2: Claude plans; Codex implements. Standing decision point 2: grilling questions are written with recommended answers, which Hari adopts in advance and the control session records. Point 3: a clean fresh check with section 11 reading `None` is approval, recorded by the control session. Point 4: not covered are money, Azure dispatch, expected outcomes and verdicts, protected-path merges, scope beyond the approved split, and `ready-for-agent`.

Parent decisions read as context: #499 [6894587233](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894587233) (split approved; the Claude review gate of #347 decision 6633528258 point 2 applies to each Codex pull request), grilling rounds [6895299617](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895299617), [6895328446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895328446) and [6895343227](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895343227). #521 adopts none of the rounds, so they bind here only through ADR-022, which carries them as merged.

Evidence, not authority: the approved [consumer-verifier plan, revision 5](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/a4c5ea1bec8630359a31d9b096416b2204f85dc7/items/506/plan-consumer-verifier.md), sections 3.7 (reason names) and 3.8 (the `consumer` block), as implemented on the base in `src/Parchwell.Verifier/VerifyRun/Consumer/Judgement/ConsumerJudgement.cs` lines 69 to 96; [#506 handoff 6899689719](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/506#issuecomment-6899689719) point 1, which found the gap.

Predecessor: #526 merged as `e8da5826deba9b42bbf5224c835ad728768544ef` on 2026-11-16; #506 is closed. No item state file exists (`items/521/` is absent on the base). Implementing tool: Codex (decision 6894614673 point 2, adopted by decision 6899941170).

## 2. Scope

Files this pull request may add or change:
- `src/Parchwell.Verifier/Judge/JudgeHandler.cs`, `Judge/PhaseCandidate.cs`, `Judge/PhaseCandidateCodec.cs`.
- New files under `src/Parchwell.Verifier/Judge/` for the judge-side reader of the `consumer` block (3.3), its typed projection (3.4) and the phase consumer result (3.5). The phase consumer result is a pure function of the `PhaseJudgement` that `PhaseJudge.Evaluate` returns and the per-slot projections; it does not live in `PhaseJudge.cs`.
- `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs`: judge admission of 3.1 only.
- `src/Parchwell.Verifier/Common/Phase/PhaseWireCodec.cs`: judge receipts for consumer pairs (3.2) only.
- `docs/operations/verifier-contract.md`: the rule text of 3.7 only.
- `tests/Parchwell.Verifier.Tests/Judge/**`, `tests/Parchwell.Verifier.Tests/Consumer/**`, `tests/Parchwell.Verifier.Tests/Host/**`: new tests, and the one existing test that section 6 names.
- `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`: only by the repository's command for it (section 8).
- `items/521/state.md` (factory.md "Item state file").

Must not change: `src/Parchwell.Verifier/Judge/PhaseJudge.cs`, byte for byte (Q5); every behaviour and every byte of output for A01, B04, B06, B07 and B10 in every mode; `consumer-verify`, `verify` and `start-line` behaviour; `src/Parchwell.Verifier/VerifyRun/**`; `Common/Results/**`; `Common/Consumer/**`; `src/Parchwell.Scenarios/**` including `ScenarioPhases.cs`; `Common/Phase/PhaseIdentity.cs`; the 24 files in `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` "mutate"; `stryker-c5-config.json` itself; `c5_runtime.py`, `c5_workflow.py`; any `*.csproj`, `Directory.*.props` or lock file; `src/Parchwell.Kafka/**`, `src/Parchwell.Enrichment/**`, `src/Parchwell.LoadGen/**`; `charts/`, `infra/`, `.github/`, `scripts/`, `scenarios/`, `results/`; `docs/blueprint.md`; `AGENTS.md`, `CLAUDE.md`, `README.md`.

Out of scope, owned elsewhere: admission of C01, C03 and C10 to the chart schema, `infra/scripts/phase/phase.py` and `scenario.yml` (#507; `phase.py` lines 22 to 24, `charts/parchwell/values.schema.json` lines 11 to 31 and 1164 to 1190, `scenario.yml` lines 6 to 17 and 499 to 503 reject them today); scenario files, expected outcomes and decision rules (#508, #510); the evidence-pack format of a consumer verdict (#511 to #513).

No new project, assembly reference or NuGet package. Protected paths touched: `src/Parchwell.Verifier/**` and `tests/Parchwell.Verifier.Tests/**` (`.github/CODEOWNERS` line 16, `Parchwell.Verifier*/`), so Hari merges. `docs/operations/verifier-contract.md` has no owner (CODEOWNERS line 6).

## 3. External contracts

Everything in this section is read by #507, a scenario child, the evidence pack or `phase.py`. The implementer copies it exactly. A consumer pair is scenario `C01`, `C03` or `C10`, phase `C0` or `C1`, configuration ID equal to the phase (#506 plan 3.1, as `PhaseWireCodec.cs` lines 349 to 351 implement it).

### 3.1 Judge admission (`VerifierHostOptionsValidator.cs`)

- `Verifier:Mode = judge` accepts a consumer pair. Line 32 does not add its scenario error for `judge` with a consumer scenario, and line 365 applies its consumer branch to `judge` as well as `start-line` and `consumer-verify`.
- `judge` with a consumer scenario and any other phase or configuration fails with the existing message "Verifier:ConfigurationId does not match Verifier:Phase." (line 368).
- Every other rule for `judge` is unchanged, including the Job name, which stays `PhaseIdentity.JudgeJobName` (lines 375 to 390).
- `verify`, `b10-sweep-line`, `b10-sweep` and `b10-final-sweep` keep rejecting consumer scenarios with their existing messages. The judge needs no consumer verification plan: `Program.cs` line 180 is unchanged.

### 3.2 Judge receipt (`PhaseWireCodec.cs`)

`ValidateReceipt` line 164 uses `MatchesPhaseConfiguration` for mode `judge` as it does for `start-line` and `verify`. So a judge receipt for a consumer pair is written and read. Every other check on a judge receipt is unchanged (lines 219 to 224). Receipts of modes `b10-sweep-line`, `b10-sweep`, `b10-final-sweep` and `b10-abrupt-verify` for a consumer scenario stay rejected with `FormatException`. For every input the base accepts, the bytes written and values read are unchanged.

### 3.3 The judge's read of each repeat's `consumer` block

The verifier result codec carries `consumer` as an unchecked JSON value (`Common/Results/VerifierResultCodec.cs` lines 89 and 240). The judge therefore checks it. For a consumer scenario, check J3 (verifier-contract line 128) passes only when all of the existing J3 conditions hold, adapted by the exit rule below, and the result's `consumer` value meets every rule in this section. For any other scenario, a result that has a `consumer` property fails J3.

Exit rule. For a consumer scenario, the existing semantic receipt check (`JudgeHandler.cs` lines 254 to 255) becomes: receipt exit code 0 when, and only when, the root `status` is `complete_valid` and `consumer.status` is `complete_valid`; exit code 1 otherwise. This matches `consumer-verify` (`VerifyRunHandler.cs` lines 207 and 211). For other scenarios the check is unchanged.

Block rules, all on the JSON value, with no duplicate property (the result codec already rejects duplicates):
1. An object with exactly the properties `schema_version`, `status`, `reasons`, `group_id`, `kill_count`, `members`, `offset_read`, `partitions`, `diagnostics`, plus `counts` and `reassignment` only where rules 5 and 6 require them.
2. `schema_version` is the number 1. `status` is `complete_valid`, `invalid` or `incomplete`.
3. `reasons` is an array of distinct strings in ordinal order, each one of the 13 names of #506 plan 3.7: `producer_side_not_complete_valid`, `consumer_registration_invalid`, `consumer_record_invalid`, `consumer_start_assignment_invalid`, `consumer_processed_outside_window`, `consumer_processed_unjoined`, `consumer_stop_invalid`, `consumer_configuration_mismatch`, `consumer_commit_record_disagreement`, `consumer_record_unavailable`, `consumer_commit_evidence_unavailable`, `consumer_completion_deadline`, `consumer_commit_frontier_short`. It is empty when, and only when, `status` is `complete_valid`.
4. `group_id` is the string `<RunId>-<lower(Phase)>-r<repeat>-consumers` for the slot's repeat (#506 plan 3.1). `kill_count` is `null` or an integer 1 to 1000000.
5. `counts` is present when, and only when, `status` is `complete_valid`. It is an object with exactly `processed_once`, `processed_again`, `processed_again_extra_rows`, `committed_never_processed`, each an Int64 at least 0, and `processed_again_extra_rows` at least `processed_again`.
6. `reassignment` is present when, and only when, `status` is `complete_valid` and `kill_count` is not `null`. It is an object with exactly `microseconds` and `unavailable_reason`. Exactly one of them is `null`. `microseconds` is an Int64 at least 0. `unavailable_reason` is `different_nodes` or `no_reassignment`.
7. `offset_read` is an object with exactly `outcome`, `requested_partitions`, `started_utc`, `ended_utc`, `error_code`, `offsets`. `outcome` is `success`, `kafka_exception`, `partition_exception`, `invalid_response` or `not_attempted`. `requested_partitions` is an array of distinct integers at least 0 in ascending order. `started_utc` and `ended_utc` are each `null` or a string matching `^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$`; `error_code` is `null` or an integer. When `outcome` is `not_attempted`, those three are `null`. `offsets` is an array of objects with exactly `partition`, `offset`, `error_code`, each an integer.
8. `members`, `partitions` and `diagnostics` are arrays. The judge does not project them (3.4) and checks nothing else about them.

A block that breaks any rule makes J3 fail for that slot, with the existing J3 consequences: reason `j3_verifier_receipt`, slot status `incomplete` unless another rule makes it `invalid` (`PhaseJudge.cs` lines 61, 68 and 69), and no consumer projection for the slot.

### 3.4 Candidate: the per-repeat `consumer` projection

For a consumer scenario, every element of `repeats` has a `consumer` property. Its value is `null` unless the slot passed J2, J3, J4, J5 and J6, the gate the base applies to `batching_recorded_evidence` (`PhaseJudge.cs` lines 84 to 85). Otherwise it is an object with exactly:

| Property | Value |
|---|---|
| `status` | the block's `status` |
| `reasons` | the block's `reasons` |
| `group_id` | the block's `group_id` |
| `kill_count` | the block's `kill_count` |
| `offset_read` | the block's `offset_read`, every property as read |
| `counts` | the block's `counts`, only when present |
| `reassignment` | the block's `reassignment`, only when present |

Property order is as in this table, and within `counts`, `reassignment` and `offset_read` as in 3.3. For any other scenario no repeat has a `consumer` property. A repeat's `status`, `reasons`, `checks` and `counts` keep their base meaning, which for these scenarios is the producer side (ADR-022 line 749: "In these repeats the producer side keeps them unchanged too"). Q1 and Q2 of section 11 cover this shape.

### 3.5 Candidate: the phase-level `consumer` object and root status

For a consumer scenario the candidate root has a `consumer` property, after `counts` and before `reasons`, an object with exactly `status`, `reasons` and, only when `status` is `complete_valid`, `counts`:
- `status` is `invalid` when any repeat's `consumer` projection has `status` `invalid`. Otherwise it is `complete_valid` when all five repeats pass J1 to J6 and have a projection with `status` `complete_valid`, and the totals below are computed. Otherwise it is `incomplete`.
- `counts`: the checked Int64 sum, over the five repeats, of each of the four `counts` properties, in the order of 3.3 rule 5. An overflow makes `status` `incomplete` with reason `j10_consumer_counts_unavailable`.
- `reasons`: the distinct union, in ordinal order, of the five projections' `reasons`, plus `j10_consumer_counts_unavailable` whenever `counts` is absent; at most 100 entries.

No phase total or other aggregate of `reassignment` is published; each repeat's value stays in its own projection (ADR-022 line 773 defines timing per repeat; Q3).

The candidate root `status` for a consumer scenario is `invalid` when the producer-side phase status (the base rule, `PhaseJudge.cs` lines 91 to 92) or the phase `consumer.status` is `invalid`; `complete_valid` when both are `complete_valid`; otherwise `incomplete`. Root `counts` keep the base J7 rule over the producer-side repeat statuses. Root `reasons` are the base reasons plus the phase `consumer.reasons`, distinct, in ordinal order, at most 100. The judge exits 0 only when the root `status` is `complete_valid`, as today (`JudgeHandler.cs` line 416).

For A01, B04, B06, B07 and B10 the root has no `consumer` property and every byte of the candidate is as on the base.

### 3.6 Candidate codec (`PhaseCandidateCodec.cs`)

`Read` and `Write` accept a candidate whose `scenario_id`, `phase` and `configuration_id` form a consumer pair (line 66 accepts it as `MatchesPhaseConfiguration` does). For a consumer pair the root `consumer` property and each repeat's `consumer` property are required and checked against 3.4 and 3.5, with the value rules of 3.3 for their parts. For any other scenario a `consumer` property at the root or in a repeat is rejected with `FormatException`, as any unknown field is today (lines 49 and 121). `CheckNoPrivateKeys` (line 409) still applies to the whole candidate.

`phase.py` `check_candidate` (lines 1563 to 1597) accepts these added properties: it reads only named keys, and no `consumer` value can hold the keys its recursive walk rejects (lines 282 to 310), because 3.3 to 3.5 close every object it can contain.

### 3.7 Contract text (`docs/operations/verifier-contract.md`)

The pull request states these rules once, in "C5 phase, blob and receipt contract", and the code cites nothing else:
- line 97: add after the `ScenarioPhases` sentence: "The consumer pairs C01, C03 and C10 with `C0`/`C0` and `C1`/`C1` are not in `ScenarioPhases`; `start-line`, `consumer-verify` and `judge` accept them, and every other mode rejects them."
- line 128: in J3, add the consumer clause and exit rule of 3.3; add "J10 builds the phase consumer result of a C01, C03 or C10 phase: its status, its reasons and the four consumer totals, which it publishes only when every repeat's consumer projection is `complete_valid`, every J1-J6 passes, and checked Int64 addition succeeds."
- line 136: add the per-repeat and phase `consumer` properties of 3.4 and 3.5, and the root status rule of 3.5.

The wording beyond these sentences is the implementer's, and states no rule that sections 3.1 to 3.6 do not state.

## 4. Requirements

1. WHEN `judge` starts with a consumer pair, THEN option validation passes. WHEN it starts with a consumer scenario and any other phase or configuration, THEN it fails with "Verifier:ConfigurationId does not match Verifier:Phase." (3.1).
2. WHEN `verify`, `b10-sweep-line`, `b10-sweep` or `b10-final-sweep` starts with a consumer scenario, THEN it fails as on the base (3.1).
3. WHEN the judge writes its receipt for a consumer pair, THEN `PhaseWireCodec` writes it and reads it back equal. WHEN a receipt of a `b10-*` mode names a consumer scenario, THEN it is rejected with `FormatException` (3.2).
4. WHEN a consumer-scenario repeat's result has a `consumer` block that meets 3.3, and every other J3 condition holds under the 3.3 exit rule, THEN J3 passes. WHEN the block is missing or breaks any rule of 3.3, THEN J3 fails with reason `j3_verifier_receipt` and the repeat's `consumer` projection is `null`.
5. WHEN a non-consumer repeat's result has a `consumer` property, THEN J3 fails.
6. WHEN a consumer-scenario receipt has exit code 0 and either status is not `complete_valid`, or exit code 1 and both are `complete_valid`, THEN J3 fails.
7. WHEN a consumer-scenario slot passes J2 to J6, THEN its repeat carries the projection of 3.4. WHEN it fails any of them, THEN `consumer` is `null`.
8. WHEN the phase is a consumer pair, THEN the root `consumer` object, its totals and the root `status` follow 3.5, including the overflow failure path.
9. WHEN the scenario is A01, B04, B06, B07 or B10, THEN the candidate and the judge receipt are byte-identical to the base for the same inputs.
10. WHEN the codec reads or writes a candidate, THEN it applies 3.6, including rejection of a `consumer` property outside a consumer pair.

## 5. Invariants and safety rules

- The producer-side judgement of a consumer-scenario repeat is computed exactly as for B04: the same J1 to J7 rules, statuses and counts (ADR-022 line 749).
- Missing consumer evidence never becomes zero: absent counts stay absent (factory.md "Evidence pack" line 189; ADR-020 line 648).
- The judge constructs no Kafka client and reads no consumer-record blob; it reads only the artifacts it reads on the base (verifier-contract line 344).
- No expected outcome, verdict or file under `results/` changes. No workflow, chart or script changes (hard safety rule 8 is not engaged).
- No new mutation target, no change to the mutate list, and no change to any of the 24 target files, `PhaseJudge.cs` included. Acceptance still needs the post-merge measurement of section 8 (factory.md "Evidence pack" line 198; verifier-mutation.md line 39: "A renewal is not a measurement").

## 6. Test map

Fixtures. Consumer-scenario repeats are built from the `consumer-verify` results the #506 tests already produce (`tests/Parchwell.Verifier.Tests/Consumer/IntegrationConsumerHostTests.cs` and `IntegrationB04BaseFixtures.cs`) or by changing a B04 judge fixture's JSON (`JudgeHandlerTests.cs` lines 1310 to 1316 `ChangeJson`, with the result reference refreshed); the choice is the implementer's. Unless a row says otherwise, each repeat's block is: `status` `complete_valid`, `reasons` `[]`, `group_id` as 3.3 rule 4 for run `synthetic-c5`, `kill_count` `null`, `counts` `{ processed_once: 10, processed_again: 1, processed_again_extra_rows: 2, committed_never_processed: 0 }`, `offset_read` `{ outcome: "success", requested_partitions: [0, 1], started_utc: "2026-11-16T10:00:00.0000000Z", ended_utc: "2026-11-16T10:00:01.0000000Z", error_code: 0, offsets: [{partition: 0, offset: 7, error_code: 0}, {partition: 1, offset: 9, error_code: 0}] }`, `members`, `partitions`, `diagnostics` `[]`; producer side complete and valid; receipt exit 0.

| Test | Requirement | Inputs | Expected |
|---|---|---|---|
| H1 | 1 | `judge`, each of C01, C03, C10 with C0/C0 and C1/C1, Job `synthetic-c5-<phase lower>-judge` | validation succeeds (6 cases) |
| H2 | 1 | `judge`, C03 with C3/C3, C1/C0, probe/C0 | fails, message contains "Verifier:ConfigurationId does not match Verifier:Phase." |
| H3 | 2 | `verify` C03 C0/C0; `b10-sweep-line` C03 C0/C0 | fails, message contains "Verifier:ScenarioId must be A01 or B04 for C5 modes." |
| W1 | 3 | judge receipt C03 C0/C0, then C01 C1/C1 and C10 C0/C0 | `WriteReceipt` then `ReadReceipt` returns an equal receipt |
| W2 | 3 | receipts of modes `b10-sweep-line`, `b10-sweep`, `b10-final-sweep` naming C03 C0/C0 | `FormatException` on write |
| W3 | 3, 9 | the existing B04 judge receipt fixture | bytes equal to the base output (assert against a literal byte string captured from the base) |
| J1 | 4, 7, 8 | five default repeats, C03 C0 | candidate `status` `complete_valid`; each repeat `consumer` equal to the default projection; root `consumer` `{ status: "complete_valid", reasons: [], counts: { processed_once: 50, processed_again: 5, processed_again_extra_rows: 10, committed_never_processed: 0 } }`; judge exit 0 |
| J2 | 4 | one test per break, each on repeat 2: `consumer` missing; extra property `x`; `schema_version` 2; `status` `done`; `reasons` `["consumer_stop_invalid","consumer_record_invalid"]` with `status` `invalid`; `reasons` `[]` with `status` `invalid`; `reasons` `["consumer_x"]`; `counts` present with `status` `incomplete`; `counts` absent with `status` `complete_valid`; `processed_once` `-1`; `processed_again_extra_rows` 0 with `processed_again` 1; `group_id` `synthetic-c5-c0-r1-consumers`; `kill_count` 0; `kill_count` 1000001; `kill_count` 5 with `reassignment` absent; `reassignment` with both `null`; `reassignment` `{ microseconds: 5, unavailable_reason: "different_nodes" }`; `unavailable_reason` `late`; `offset_read.outcome` `ok`; `requested_partitions` `[1, 0]`; `started_utc` `2026-11-16T10:00:00Z`; `not_attempted` with `error_code` 0; `members` `{}` | repeat 2 `checks.j3_verifier_receipt` false, `reasons` contains `j3_verifier_receipt`, `status` `incomplete`, `consumer` `null`; root `consumer.status` `incomplete`, `counts` absent, `reasons` contains `j10_consumer_counts_unavailable`; candidate `status` `incomplete` |
| J3 | 5 | B04 phase whose repeat 2 result has `consumer` `{}` | repeat 2 `checks.j3_verifier_receipt` false |
| J4 | 6 | repeat 2 exit and statuses: (exit 1, root `complete_valid`, consumer `incomplete` with `consumer_completion_deadline`); (exit 1, both `complete_valid`); (exit 0, consumer `incomplete`); (exit 0, both `complete_valid`) | J3 true, false, false, true |
| J5 | 7, 8 | repeat 3: root `complete_valid`, consumer `incomplete`, reasons `["consumer_completion_deadline"]`, exit 1 | repeat 3 `status` `complete_valid` with its producer `counts`; repeat 3 `consumer.status` `incomplete`; root `counts` present (J7); root `consumer` `{ status: "incomplete", reasons: ["consumer_completion_deadline", "j10_consumer_counts_unavailable"] }`; candidate `status` `incomplete`; exit 1 |
| J6 | 8 | repeat 2 consumer `invalid`, reasons `["consumer_record_invalid"]`, exit 1 | root `consumer.status` `invalid`; candidate `status` `invalid` |
| J7 | 8 | repeat 4 producer `incomplete`; its consumer `invalid`, reasons `["producer_side_not_complete_valid"]`, exit 1 | repeat 4 `status` `incomplete`; root `counts` absent, reasons contain `j7_counts_unavailable`; root `consumer.status` `invalid`; candidate `status` `invalid` |
| J8 | 7, 8 | repeat 5 with a broken J2 publication reference (an existing J2 fixture) | repeat 5 `consumer` `null`; root `consumer.status` `incomplete` |
| J9 | 8 | repeats 1 and 2 with `processed_once` 9223372036854775807 | root `consumer` `{ status: "incomplete", reasons: ["j10_consumer_counts_unavailable"] }`; candidate `status` `incomplete` |
| J10 | 7 | C10 C1, `kill_count` 50; repeat 1 `reassignment` `{ microseconds: 1234567, unavailable_reason: null }`; repeat 2 `{ microseconds: null, unavailable_reason: "different_nodes" }`; repeats 3 to 5 `{ microseconds: null, unavailable_reason: "no_reassignment" }` | each repeat projects its own `reassignment` exactly; root `consumer` has no `reassignment` property |
| J11 | 9 | every existing test in `tests/Parchwell.Verifier.Tests/Judge/` | passes unchanged |
| J12 | 9 | the B04 fixture of `GivenFiveAcceptedB04EvidenceResults_WhenJudged_ThenProjectsEachIntoItsOwnRepeat` | candidate bytes equal to the base output (literal captured from the base); no `consumer` property anywhere |
| C1 | 10 | a C03 C0 candidate from J1 | `Write` then `Read` succeeds |
| C2 | 10 | J1 candidate with root `consumer` removed; with repeat 3 `consumer` removed; with root `consumer.counts` and `status` `incomplete`; with root `consumer` extra property `x` | `FormatException` |
| C3 | 10 | a B04 candidate with root `consumer` `{}`; with repeat 1 `consumer` `null` | `FormatException` |
| P1 | 8 | the new phase consumer function directly, with the `PhaseJudgement` and projections of J1, J5, J6, J7, J8 and J9 as typed inputs | the same root `consumer` object and root `status` as those rows |
| P2 | Q5 | `sha256sum src/Parchwell.Verifier/Judge/PhaseJudge.cs` on the head | `22c56f360af0ccab6123c471d7c092d588ca7547323a5415f606bee0368aa2f9`, the base hash |

Changed existing test: `tests/Parchwell.Verifier.Tests/Consumer/PhaseWireConsumerTests.cs` lines 88 to 95, `Given_ConsumerJudge_U21_When_WrittenOrRead_Then_RemainsRejected`, asserts that a C03 judge receipt is rejected. Requirement 3 reverses that contract on purpose ([decision 6899939624](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6899939624) point 2 puts the judge change in this pull request). The implementer replaces it with W1 and W2 and lists the replacement in the PR implementation notes, citing this paragraph. No other existing assertion changes.

Standing fixture rule. When an existing test's fixture is rejected by a new check of this pull request, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it. The cold review and Codex review check each listed change. Modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2. The only new checks that reach existing fixtures are requirement 5 and 3.6's rejection of `consumer` outside a consumer pair; no existing fixture is known to carry a `consumer` property (inference from a search of `tests/Parchwell.Verifier.Tests/Judge/` on the base).

## 7. Deployment assumptions

None that a test in this plan does not cover, except the one under section 11, Q4.

## 8. Acceptance commands

Run in this order on the committed head; each must produce the evidence named.

1. `./scripts/verify-dotnet.sh --tier sandbox` (docs/agents/pull-requests.md "Verification gates" step 3): every test outside the `Tier=CGroupV2` trait passes, the new tests of section 6 included; record it as a partial run with that exclusion.
2. `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py renew --head <committed head> --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json --basis "#521 consumer phase judge: changes shared inputs only; every C5 target source, PhaseJudge.cs included, is unchanged. Added judge and consumer tests may raise per-mutant runtime; that risk is accepted pending the required post-merge mutation acceptance run on main."`, then commit the rewritten qualification file. Evidence: status `renewed` listing only paths this pull request changes. If it raises `target sources changed` or `not renewable`, the scope of section 2 was broken: stop under section 12.
3. `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base e8da5826deba9b42bbf5224c835ad728768544ef --head <final head> --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`: status `qualified` or `provisional` with `renewed_inputs` listing the changed shared inputs; never `rejected`. The hosted `.NET verification` check runs the same command (`.github/workflows/dotnet-verification.yml` lines 137 to 151).
4. The hosted `.NET verification` check on the final head passes (pull-requests.md "Verification gates" step 4).
5. After merge, a `verifier-c5-mutation.yml` run dispatched on `main` (hard safety rule 8 allows `main` only) whose `summary.json` names a commit whose history includes the merge commit, with `measurement_complete: true` and `break_passed: true` (factory.md "Evidence pack" line 198). Cap: one dispatch by the implementing session. Exhaustion action for a failed or incomplete run: `record the gap and continue`, open a fix issue as line 198 requires, and report that consumer evidence-pack publication stays blocked. Survivors of a complete run are dispositioned under verifier-mutation.md line 144 (cap 50, then `record the gap and continue`).

## 9. Sources

Repository sources at the base commit, read in this session: the files and lines cited in sections 1 to 6, plus `.github/CODEOWNERS` lines 5 to 16, `infra/scripts/phase/phase.py` lines 22 to 24 and 1563 to 1597, and `.github/workflows/scenario.yml` lines 203 to 207. No Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour is relied on: the judge reads only Blob artifacts the verifier wrote (plan rule 1 does not apply). The #506 plan sections cited are evidence of the `consumer` block's design; the code on the base is the source for its shape.

## 10. Plan-check questions (factory.md "Item lifecycle" step 1)

(a) Steps whose effort scales with a number unknown at planning time: the post-merge mutation run (section 8, command 5), whose survivor count and runtime are known only after it runs. Its cap (one dispatch; survivor dispositions capped at 50) and exhaustion actions are in section 8. The test count is fixed by section 6.

(b) Where this plan is stricter than its parent plan: the parent split row has no text for this entry beyond "consumer phase judge". This plan adds the closed check of the `consumer` block (3.3), because the result codec does not check it and J9 (verifier-contract line 128) requires a closed candidate projection.

(c) Tests or checks that depend on code, data or behaviour neither on `main` nor in this pull request: none. The `consumer` block is produced by `consumer-verify` on the base. The judge Job for consumer scenarios does not yet exist; the chart and workflow that start it are #507's (section 2), and Q4 covers the one assumption about them.

## 10a. Deferred work

- Admission of C01, C03 and C10 to `phase.py`, the chart schema and `scenario.yml`. Evidence: section 2 "Out of scope". On the verdict path; deferral permitted because the approved split gives it to #507 (proposal revision 2, row 4). Responsible: #507. Completion evidence: #508's smoke run reaches the judge step and `phase.py candidate` accepts its candidate. Gate it blocks: #508's smoke run.
- The post-merge mutation run of section 8. Execution permitted only after merge (factory.md "Evidence pack" line 198). Responsible: the implementing session dispatches `verifier-c5-mutation.yml` on `main` after merge; the control session confirms it. Gate it blocks: evidence-pack publication for C01, C03 and C10.

## 11. Open questions for Hari

Each is a grilling question with a recommended answer, written under standing decision 6895535291 point 2.

- Q1. Status model. Recommended: a repeat's `status`, `reasons`, `checks` and `counts` stay the producer-side judgement, unchanged; the consumer judgement is the separate per-repeat `consumer` projection (3.4); the phase root `status` combines both (3.5). Reason: ADR-022 line 749 keeps the producer side unchanged, and the judge's exit must reflect both. Alternative: fold the consumer status into each repeat's `status`; that withholds producer totals whenever a consumer run is incomplete.
- Q2. Fields projected per repeat. Recommended: `status`, `reasons`, `group_id`, `kill_count`, `offset_read`, `counts`, `reassignment` (3.4); not `members` (pod and node names), `partitions` or `diagnostics`. Reason: ADR-019 line 609 publishes "only the captured fields a decision rule reads"; ADR-022 line 770 says to "Retain the group ID, requested partitions, returned values or errors, and read start/end times as evidence", and the candidate is the only artifact the workflow keeps (`scenario.yml` uploads only `candidate.json` and pod logs, lines 219 to 230). Alternative: also project `members` without pod names.
- Q3. Phase aggregation. Recommended: four consumer totals at phase level only when all five repeats are consumer `complete_valid`, as J7 does for producer totals; no phase aggregate of reassignment time. No authority defines a consumer aggregate (ADR-022 defines classes and timing per repeat); the scenario children's decision rules may read per-repeat values instead. Alternative: no phase consumer totals.
- Q4. Deployment assumption DA1, about the chart #507 will add: the `consumer-verify` Job of each repeat runs with `Verifier:JobName` equal to `PhaseIdentity.VerifyJobName(RunId, Phase, repeat)` (`PhaseIdentity.cs` line 43), because the judge requires that name in J3 (`JudgeHandler.cs` line 236). Evidence: none live; no connection was opened and no load was run. The judge fails closed if it is wrong (J3 false, candidate `incomplete`), and #508's smoke run shows it. Recommended: accept this risk by name.
- Q5. Where the consumer phase logic lives. Recommended: a new file under `Judge/`, with `PhaseJudge.cs` unchanged. Reason: `PhaseJudge.cs` is a C5 target whose shard layout and estimates are pinned to its hash in non-renewable runner tables (`c5_runtime.py` lines 604 to 612, `SIX_TAIL_ESTIMATES`; `renew` raises `target sources changed` at line 1355), so any edit needs new pre-merge timing measurement and changes to protected runner files outside this scope (verifier-mutation.md line 39). Cost of the recommendation: the new consumer phase logic is not itself a mutation target; its tests are section 6, and the post-merge run still measures the unchanged targets. Adding it as a target needs the runner, configuration, protected workflow matrix (which has 256 of 256 slots in use) and new measurement. Alternative: change `PhaseJudge.cs` and carry that new measurement; this widens scope beyond the split entry and goes to Hari. The issue body's sentence "`Judge/PhaseJudge.cs` is a C5 mutation target, so the change needs its own mutation measurement" stays met by the post-merge run of section 8.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, a literal expected value in section 6, or the scope of section 2, or when an existing test other than the one section 6 names would need an assertion changed.

## 13. Size estimate

Estimate, 2026-11-16, from the size of the base files touched and of comparable #506 tests: about 450 changed lines of production code in 7 files (3 new) and about 900 lines of tests in 4 to 6 files, plus about 15 lines in `docs/operations/verifier-contract.md`. Size is reported, not enforced.

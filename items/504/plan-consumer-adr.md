# Plan: consumer-processing ADR (ADR-022)

Issue: [#504](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504), child of [#499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499), split entry 1. Pull request name: `consumer-adr`. This is the contract-grade plan for that one pull request under docs/agents/factory.md "Item lifecycle" step 3.

Revision 2. Base: `main` at 72a4a280ea755408c3d823cbbeed08d432a11e59. Line numbers below are on that commit.

Changes since Revision 1, each from [decision 6895560872](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895560872) on #504:

- Section 11: the three open questions are answered (roll-over, kill point, stop rule) and now read `None`.
- Section 1: the standing decision on #499 and the two #504 decisions are listed as authorities.
- Section 3.5: ADR-022's decision line also cites decision 6895560872. Section 3.1: the approval record also cites it.
- Section 10 (b): the three items that went beyond the decisions are marked adopted.

Changes from the [Revision 1 plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895667441), by finding:

- 1: the subject and witness end conditions in "Invalid run" use the completion receipt, so a closed member is not invalid and a short window stays incomplete.
- 2: "Completion" counts a partition whose start and end lines are equal as complete without a commit row.
- 3: new 3.9 amends ADR-019's closed list of check sources (line 601); scope, requirement 3a, test A10 and acceptance updated.
- 4: "Configuration fidelity" checks the subject against the selected configuration and the witness against the baseline; "Registration" records role and effective settings as their evidence.
- 5: "Record" lists four row kinds, kill-intent included, all with row sequence numbers, and defines the kill-intent row.
- 6: 3.4 appends the ADR-020 sentence after the "Neither may start" sentence, so "Neither" keeps its antecedent.
- 7: the #504 decisions are authorities and cited in 3.1 and ADR-022's Decision line; section 11 updated.
- 8: new section 10a "Deferred work" assigns the committed-offset test.
- 9: section 10 (b) lists the further places the plan goes beyond its decisions; the container and role choice is adopted by decision 6895700580, now an authority.
- P2s: size estimate by script; ADR-022 option 5 states the contrast with ADR-012 option 4; the test-map note names `scripts/docs-rules.mjs`; the F01 fold row is listed in section 10a.

This pull request adds ADR-022 to docs/blueprint.md and makes the small edits it needs elsewhere in that file. The ADR is the design the worker (#505), verifier (#506), chart and workflow (#507) and self-termination (#509) children build on. The pull request changes only prose. Its published wording is pinned literally below, so the implementer copies it and judges nothing.

## 1. Goal and authority

Goal: record the consumer-processing design Hari settled in three grilling rounds, as an ADR in the blueprint's format (context, options, decision, consequences; AGENTS.md "Skills"), so later children cite one design authority.

Authorities, in creation order (factory.md "Item lifecycle" step 3, child issues):

1. #504 body (created 2026-11-15): `Parent items: #499`, `Split entry: 1`, `Predecessor PRs: none`; scope as in this plan's section 2.
2. #499 body: a request for a proposal; authorizes nothing by itself.
3. #499 [decision 6894534441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441): consumer foundation batch; crash by self-termination at a fixed point; K-55, K-28, K-57 tested, K-47 dropped and published as not tested; Codex implements.
4. #499 [decision 6894564479](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894564479): the pause test separates heartbeats from the poll limit. It binds #508 and #511, not this pull request.
5. #499 [decision 6894587233](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894587233): split approved; Claude review gate kept.
6. #499 [decision 6894614673](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894614673): Claude plans with `grilling` then `plan-pr`; Codex implements; this session coordinates.
7. #499 [decision 6895299617](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895299617), adopted on #504 by [6895299838](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895299838): grilling round 1.
8. #499 [decision 6895328446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895328446), adopted on #504 by [6895328621](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895328621): grilling round 2.
9. #499 [decision 6895343227](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895343227), adopted on #504 by [6895343446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895343446): grilling round 3.

10. #499 [decision 6895535291](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291), adopted on #504 by [6895535732](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895535732): the #347 control session coordinates this batch; grilling answers adopted in advance; plan approval on a fresh check with `Blocking findings: none` and no open questions.
11. #504 [decision 6895560872](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895560872): the three recommended answers of Revision 1's section 11 are adopted; point 2 replaces round 3's "count of processed messages" wording.
12. #504 [decision 6895700580](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895700580): the consumer record's container is `consumer-record`; the enrichment-consumer identity gets Storage Blob Data Contributor and the verifier Storage Blob Data Reader, both scoped to it; Contributor's read and delete rights are accepted.

Evidence, not authority: [proposal revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894560135).

There is no item state file (`items/504/` does not exist on the base) and no parent plan: #499 holds a proposal, not a plan. Implementing tool: Codex (authority 3, point 4). Hari still posts the tool-naming comment on #504 before `ready-for-agent` (factory.md "Item lifecycle" step 2).

Where this plan goes beyond the decisions, it says so in section 10 and asks in section 11.

## 2. Scope

Files this pull request may change:
- `docs/blueprint.md`, only as section 3 pins: after line 12, line 601, line 603, line 615, line 637, after line 730, line 1205, line 1210, after line 1354.
- `items/504/`: the item state file `items/504/state.md` under factory.md "Item state file", and any receipt the implementing skill requires.

Must not change: any other line of `docs/blueprint.md`; `AGENTS.md`, `CLAUDE.md`, `docs/agents/`, `docs/engineering/`, `docs/operations/`; `README.md`; `src/`, `tests/`, `scenarios/`, `results/`, `infra/`, `charts/`, `scripts/`, `.github/`.

Protected: `/docs/` is in `.github/CODEOWNERS` line 5 on the base, so Hari merges (AGENTS.md GOVERNANCE "Merge rule").

## 3. External contracts

The published text is pinned literally. "Append" means: add one space after the last character of the named existing text, then the literal text. Every block below is one line unless it shows more.

### 3.1 Approval record (insert after line 12)

Insert after line 12 one empty line, then this line:

```
ADR-022, the ADR-019 and ADR-020 amendments it makes, the narrowed C03 row and the C10/C15 note in section 12's "Folded into existing items", and source S49 follow the #499 decisions [6894534441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441), [6895299617](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895299617), [6895328446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895328446) and [6895343227](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895343227), and #504 decisions [6895560872](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895560872) and [6895700580](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895700580); Hari's merge of this pull request records approval of them.
```

### 3.2 ADR-019 check 1 (line 603)

Replace line 603, literal:

```
  1. Configuration fidelity. The effective producer settings in the captured evidence match the scenario's selected configuration and the baseline (ADR-014, ADR-015). A mismatch invalidates the run.
```

with:

```
  1. Configuration fidelity. The effective producer settings in the captured evidence match the scenario's selected configuration and the baseline (ADR-014, ADR-015). A mismatch invalidates the run. For C03, C10 and C01, it also covers the consumer members' effective settings (ADR-022).
```

### 3.3 ADR-019 consequence (line 615)

Replace line 615, literal:

```
- Configuration fidelity covers producer settings only. Consumer configuration fidelity for C-series items comes from each item's decision rules until this ADR is amended.
```

with:

```
- Configuration fidelity covers producer settings, and for C03, C10 and C01 also the consumer members' settings (ADR-022). Consumer configuration fidelity for other C-series items comes from each item's decision rules until this ADR is amended.
```

### 3.4 ADR-020 exception (line 637)

On line 637, after the sentence below (it occurs once in the file), append the new sentence. Anchor, literal:

```
Neither may start any CLI tool or other process, invoke a shell, use the Kubernetes API, manage pods or nodes, or act as an in-cluster orchestrator.
```

New sentence to append after it, before ` Workflows retain`:

```
Under ADR-022, the subject consumer of a C03, C10 or C01 repeat whose configuration names a kill count may also send SIGKILL only to its own process, once per repeat, and starts no process.
```

### 3.5 ADR-022 (insert after line 730)

Line 730 is the last ADR-021 consequence, `- The experiment reports operational recovery, ...`. Insert after it one empty line, then this block, so that the existing empty line 731 and the `---` of line 732 follow it unchanged:

```markdown
### ADR-022: C03, C10 and C01 judge a two-member consumer group by a durable per-member processing record joined to the produce ledger

**Context.** C03, C10 and C01 ask what a consumer group does when one member processes too slowly, crashes, or stops heartbeating. ADR-012's oracle classifies produce attempts only. It cannot say whether a consumer processed a message, processed it twice, or committed past it without processing it. ADR-019's configuration fidelity covers producer settings only, and ADR-020 permits one self-kill, B10's.

librdkafka v2.16.0 documents three behaviours these items rely on (Fact: S6, `max.poll.interval.ms`, `session.timeout.ms` and `enable.auto.offset.store` rows). When `max.poll.interval.ms` is exceeded, "the consumer is considered failed and the group will rebalance", and "Offset commits may be not possible at this point". If the broker receives no heartbeats from a member within `session.timeout.ms`, it removes the member and triggers a rebalance. `enable.auto.offset.store` defaults to `true`, and the library recommends `false` with offsets stored after processing for long-time processing. An append blob accepts at most 50,000 appends; a further append returns 409 Conflict (Fact: S49).

**Options considered.**
1. Count consumed messages only. Carries no per-message evidence, so it cannot separate a repeat from a gap.
2. Infer processing from committed offsets. A committed offset does not show that the message was processed, so the C10 loss case is invisible.
3. A per-member record written to a local file and uploaded at the end. A killed member loses it, which is the C10 and C01 case.
4. A per-member record in a Kafka topic. It shares the failure domain under test (ADR-012, option 3).
5. A per-member record appended to Blob before each offset store, joined to the produce ledger. ADR-012 rejected this pattern for the producer because it adds latency and failures to the produce path being measured (its option 4). Here the write sits in the consumer's processing path, whose processing time is simulated, and its rate effect is a stated consequence.

**Decision.** Option 5, for repeats of C03, C10 and C01 only, under the [round 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895299617), [round 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895328446) and [round 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895343227) decisions on #499 and decisions [6895560872](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895560872) and [6895700580](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895700580) on #504.
- Scope. Every other scenario keeps ADR-012, ADR-013, ADR-019 and ADR-020 unchanged. In these repeats the producer side keeps them unchanged too. A repeat whose producer side is invalid or incomplete under ADR-012 and ADR-013 has no consumer outcome counts.
- Group. Each repeat runs two consumer members in one consumer group, with a `group.id` derived from the run and repeat identity, so no repeat inherits committed offsets. The subject member carries the configuration's fault. The witness runs the baseline consumer with the profile's processing delay and no fault. Both run the enrichment-consumer image and identity, as Kubernetes Jobs on the `workload` pool. Each run records both members' node names.
- Start order. Both members register, subscribe to `document-events` and hold an assignment before the start line is recorded. At the start line every partition is assigned to exactly one member. `auto.offset.reset` keeps its baseline value.
- Registration. Each member writes a registration marker with its pod name and instance ID before it constructs its Kafka consumer. If that write fails, it exits non-zero without consuming. The marker also records the member's role, subject or witness, and its effective consumer settings, which are the evidence for configuration fidelity. Consumer members are not producer instances, so ADR-012's and ADR-020's producer-instance count is unchanged.
- Record. Each member writes these row kinds, each with its instance ID and a row sequence number that starts at 0 and increases by 1: processed, commit, partition-event and, for the subject under "Self-termination", kill-intent. A processed row holds partition, offset, the producer instance and attempt IDs from the message headers, key, sequence number, and processing start and end times. A commit row holds the partitions, offsets and error of one offset-commit result. A partition-event row holds assigned, revoked or lost, the partitions, and the time. A kill-intent row holds the partition and offset of the message it names, and the time.
- Durability. Each member appends each row to its own record in the `consumer-record` container of the ephemeral storage account. It appends a processed row before it stores that message's offset, so "processed" means "recorded". A record is a sequence of append blobs; a member starts a new blob before the current one reaches 50,000 appends (S49). A failed append ends the member with a non-zero exit, without storing that offset. Roles, additions to ADR-003's list: the enrichment-consumer identity gets Storage Blob Data Contributor scoped to `consumer-record`, and the verifier identity gets Storage Blob Data Reader scoped to it.
- No topic writes. The members write nothing to `document-state` or `document-events-dlq`, so ADR-012's deferred oracles for those topics are not needed.
- Self-termination. Only in a configuration whose scenario names a kill count N, only the subject, and only once per repeat. When `Consume` returns the subject's Nth message of the repeat, before processing it, the subject appends a kill-intent row naming that message's partition and offset, then sends SIGKILL to its own process. It sends no other signal and starts no process. This is the second exception in ADR-020.
- Join. The verifier joins each processed row to the produce ledger on producer instance ID and attempt ID, and requires the key and sequence number to agree.
- Classes. Each message that the ledger records as delivered and that the verifier consumed in the run window gets one consumer class:

| Processed rows for its partition and offset | Group committed past it | Class |
|---|---|---|
| exactly 1 | yes | processed once |
| more than 1 | yes | processed again; each extra row is counted |
| 0 | yes | committed but never processed |
| any | no | not reached |

- "Committed but never processed" is consumer loss. "Not reached" makes the run incomplete, not loss.
- Completion. A consumer run is complete when, for every partition, the newest successful commit in the commit rows is at or past the run's end line, within a deadline the item plan sets as a harness budget. That deadline is not a claim about Event Hubs. A partition whose start and end lines are equal is complete without a commit row. The verifier records the end line after the load generator ends, as ADR-013 sets, and classifies only after the consumer run completes. An incomplete run has no consumer outcome counts.
- Stop. Each member consumes for a window fixed in the scenario, measured from its own start, then closes its consumer and writes a completion receipt as its last action. The window is a harness budget. The verifier judges completion from the record afterwards; a window too short for the commits to reach the end line leaves the run incomplete.
- Independent commit evidence. If the item plans show that the verifier, with its existing identity, can read the group's committed offsets without joining the group, the verifier requires them to agree with the commit rows. Otherwise each publication states that commit evidence is self-reported by the members.
- C01 timing. Reassignment time runs from the subject's last row before it ends to the witness's assigned row for the subject's partitions. When the two members ran on different nodes, that repeat's timing is unavailable.
- Configuration fidelity. ADR-019's check 1 also covers the consumer members' effective settings for these items: the subject's must match the selected configuration and the baseline, and the witness's the baseline. A mismatch invalidates the run.
- Invalid run. A repeat has no consumer outcome counts when any of these hold:
  - its producer side is invalid or incomplete under ADR-012 and ADR-013;
  - a member's record is missing, or has a gap or a repeated row sequence number;
  - other than two consumer members registered;
  - at the start line, a partition was assigned to no member or to both;
  - a processed row names an offset outside the run window, or a message with no ledger row, or a key or sequence number that disagrees with its ledger row;
  - with a kill count configured, the subject ended other than at its kill-intent row; with none configured, the subject wrote a kill-intent row or ended without its completion receipt; or the witness ended without its completion receipt;
  - a consumer member's effective settings do not match the configuration.

  An item plan adds only the checks its decision rules need (ADR-019).

**Consequences.**
- Consumer outcome classes describe Event Hubs and the client, as ADR-012's classes do. A harness bookkeeping fault invalidates the run instead.
- The append before each offset store is in the processing path, so it bounds each member's processing rate. The effect is not measured. Each item plan sizes its workload and deadline for it.
- With store-after-process, a kill before processing leaves that offset unstored, so the witness reprocesses offsets stored since the last commit (inference from S6). With the default auto offset store, the offset of the message being processed is already stored, so a commit before the kill can pass it without a processed row (inference from S6). C10 measures both.
- Commit evidence may be self-reported, and each such publication says so.
- The record is in the ephemeral layer and is deleted at teardown. Retention follows ADR-020.
- The worker, verifier, chart, workflow and identity changes land in the #499 children. This ADR records the design before them; it does not claim they work.
```

### 3.6 Section 12 C03 row (line 1205)

Replace, literal:

```
| C03 | Pause and resume while processing; keep calling `Consume` (K-55); consumer token refresh runs only inside `Consume` (K-47); no commit on partitions lost (K-28) |
```

with:

```
| C03 | Pause and resume while processing; keep calling `Consume` (K-55); no commit on partitions lost (K-28). Narrowed for #499 ([decision 6894534441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441), point 3): consumer token refresh runs only inside `Consume` (K-47) is not tested |
```

### 3.7 Section 12 C10, C15 row (line 1210)

Replace, literal:

```
| C10, C15 | Killed before the first auto-commit, with `auto.offset.reset` `largest` (K-57) |
```

with:

```
| C10, C15 | Killed before the first auto-commit, with `auto.offset.reset` `largest` (K-57); tested under C10 only for #499 ([decision 6894534441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441), point 3) |
```

### 3.8 Source S49 (insert after line 1354)

Line 1354 is the S48 row, the last line of the file. Insert after it this line, and end the file with one newline:

```
| S49 | Azure Storage REST API: [Append Block](https://learn.microsoft.com/rest/api/storageservices/append-block#remarks), remarks: "A maximum of 50,000 appends are permitted for each append blob"; more returns 409 (Conflict) | read 2026-11-15 |
```

### 3.9 ADR-019 closed list (line 601)

On line 601, after the sentence below (it occurs once in the file), append the new sentence, before ` For B10's intentionally`. Anchor, literal:

```
For B10, that source category is the ADR-012 and ADR-013 invalid-run conditions as modified only by the explicit ADR-021 exceptions; the closed list still has four source categories.
```

New sentence:

```
For C03, C10 and C01, that source category also includes ADR-022's invalid-run conditions; the closed list still has four source categories.
```

## 4. Requirements

1. WHEN the pull request merges THEN docs/blueprint.md contains ADR-022 exactly as 3.5, between ADR-021 and section 5.
2. WHEN a later child cites the consumer record, its classes, completion, stop, durability, kill point or invalid-run conditions THEN ADR-022's text is the source (3.5).
3. WHEN the verifier's configuration-fidelity check is read for C03, C10 or C01 THEN ADR-019 covers consumer settings, the subject's against the selected configuration and the witness's against the baseline (3.2, 3.3, 3.5).
3a. WHEN the verifier's closed list of check sources is read for C03, C10 or C01 THEN it includes ADR-022's invalid-run conditions (3.9).
4. WHEN ADR-020's process rules are read THEN they name the ADR-022 self-kill as a second, scoped exception (3.4).
5. WHEN section 12's fold table is read THEN C03 states K-47 is not tested, and the C10, C15 row states K-57 is tested under C10 only (3.6, 3.7).
6. WHEN the status header is read THEN it records what this pull request adds and that Hari's merge approves it (3.1).
7. WHEN any line outside section 2's list differs from the base THEN the pull request is out of scope and acceptance check A8 fails.

## 5. Invariants and safety rules

- ADR-012, ADR-013 and ADR-021 text is unchanged. Producer-side rules are unchanged for every scenario.
- No verdict, expected outcome or `results/` file changes (AGENTS.md "Verdict integrity").
- No identity or role is created here. ADR-022 records role additions; `infra/ephemeral/identity*.tf` changes in #507.
- No new inventory item (blueprint line 4; AGENTS.md GOVERNANCE "Scope"). The two section 12 rows are narrowed or annotated, not added.
- ASCII punctuation only, no em or en dashes (AGENTS.md "Writing standards").

## 6. Test map

The change is prose; there is no code. Each requirement maps to a scripted text check on the pull request head (section 8). This is not verifier work, so docs/agents/pull-requests.md's predicate coverage does not apply.

| Requirement | Check | Input | Literal expected value |
|---|---|---|---|
| 1, 2 | A1 | `docs/blueprint.md` | the 3.5 block occurs exactly once; the line `### ADR-022: ...` comes after the line starting `### ADR-021:` and before the line `## 5. Failure modes` |
| 3 | A2 | `docs/blueprint.md` | the new lines of 3.2 and 3.3 each occur once; their old lines occur zero times |
| 3a | A10 | `docs/blueprint.md` | the 3.9 new sentence occurs once, on line 601, immediately after the 3.9 anchor and a space |
| 4 | A3 | `docs/blueprint.md` | the 3.4 appended sentence occurs once, on the line that contains `The sole production process-launch exception is ADR-021's B10 parent process.` |
| 5 | A4 | `docs/blueprint.md` | the new rows of 3.6 and 3.7 each occur once; the old rows occur zero times |
| 6 | A5 | `docs/blueprint.md` | the 3.1 line occurs once, and is line 14 of the file |
| sources | A6 | `docs/blueprint.md` | the 3.8 row occurs once and is the last line; the file ends with one newline |
| 7 | A7 | `git diff --name-only origin/main...HEAD` | only `docs/blueprint.md` and paths under `items/504/` |
| 7 | A8 | the base file and the head file | applying 3.1 to 3.9 to the base text by script gives the head file byte for byte |
| style | A9 | added lines | no byte outside printable ASCII, tab and newline |

Existing tests: no test asserts on docs/blueprint.md's wording. `scripts/docs-rules.mjs` lines 62 to 85 on the base read its ADR and source definitions; acceptance command 4 runs those checks. The plan adds no code check, so the plan-pr standing fixture rule has no case.

## 7. Deployment assumptions

None. The change is prose; no deployed component is involved.

## 8. Acceptance commands

Run on the pull request branch before the cold review.

1. A1 to A6, A9 and A10: one script that reads the file, counts each literal of section 3 by exact string match and checks the stated positions, printing `PASS <id>` or `FAIL <id>: <reason>`. Evidence: eight `PASS` lines (A1 to A6, A9, A10) and exit status 0.
2. A7: `git fetch origin main && git diff --name-only origin/main...HEAD`. Evidence: the printed list.
3. A8: a script that applies 3.1 to 3.9 to `git show origin/main:docs/blueprint.md` and compares with the head file. Evidence: `identical`.
4. `npm run check:docs`. Evidence: the summary line; each warning in docs/blueprint.md fixed or explained under docs/agents/pull-requests.md rule 8.
5. Verification gates (docs/agents/pull-requests.md "Verification gates"): steps 1 and 3 do not apply, because no file under `src/`, `tests/` or the other paths of its last paragraph changes. Step 2, the cold review under rule 8, applies. Step 4, the hosted `.NET verification` check on the final head, runs as the required check. The Claude review gate of decision 6894587233 applies after Codex opens the pull request.

## 9. Sources

1. librdkafka v2.16.0 [CONFIGURATION.md](https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md) (blueprint S6), lines 131 (`session.timeout.ms`), 137 (`max.poll.interval.ms`) and 140 (`enable.auto.offset.store`), read 2026-11-15 by fetching the raw file at tag v2.16.0. Quoted in ADR-022's context.
2. Azure Storage REST API [Append Block](https://learn.microsoft.com/rest/api/storageservices/append-block#remarks), remarks, read 2026-11-15 with the Microsoft Learn documentation search tool: "A maximum of 50,000 appends are permitted for each append blob" and "If you attempt to upload more than 50,000 blocks, the service returns error code 409 (Conflict)". The [scalability targets](https://learn.microsoft.com/azure/storage/blobs/scalability-targets#scale-targets-for-blob-storage) page gives the same 50,000-block maximum for an append blob. Becomes S49.
3. Confluent.Kafka v2.16.0 [ConsumerBuilder.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ConsumerBuilder.cs) (`SetPartitionsAssignedHandler`, `SetPartitionsRevokedHandler`, `SetPartitionsLostHandler`, `SetOffsetsCommittedHandler`, lines 472 to 718) and [IConsumer.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/IConsumer.cs) (`StoreOffset` line 363, `Committed(IEnumerable<TopicPartition>, TimeSpan)` line 563), read 2026-11-15. They show the three row kinds of ADR-022 have client callbacks to come from. Whether `Committed` works without joining the group is not verified; ADR-022 makes that conditional.
4. docs/blueprint.md at 72a4a280ea755408c3d823cbbeed08d432a11e59: ADR-003 (line 210), ADR-012 (lines 367 to 429), ADR-013 (lines 431 to 466), ADR-019 (lines 582 to 619), ADR-020 (lines 621 to 657), ADR-021 (lines 659 to 730), section 3 "AKS cluster" (line 141), section 12 fold table (lines 1195 to 1219), sources (lines 1297 to 1354).
5. `src/Parchwell.Enrichment/Program.cs` line 33 at 72a4a280ea755408c3d823cbbeed08d432a11e59: the enrichment worker has only the F04 seed mode.
6. `.github/CODEOWNERS` line 5 at 72a4a280ea755408c3d823cbbeed08d432a11e59.

External behaviour (plan-pr "Plan rules" rule 1): items 1 to 3. Tools: a raw GitHub fetch at the pinned tag for 1 and 3, and the Microsoft Learn documentation search tool for 2.

## 10. Plan-check questions (factory.md "Item lifecycle" step 1)

(a) Steps whose effort scales with a number unknown at planning time: none. The pull request changes nine places in one file.

(b) Where the plan is stricter than, or goes beyond, its authority:
- Append-blob roll-over. Round 1 decided "its own append blob". S49 caps an append blob at 50,000 appends, and a repeat can carry more rows than that: B06's repeats had 186000 events each ([README.md line 24](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/72a4a280ea755408c3d823cbbeed08d432a11e59/README.md?plain=1#L24)). ADR-022 therefore makes each record a sequence of append blobs. Adopted by decision 6895560872, point 1.
- Kill point. Round 3 decided "a count of processed messages". ADR-022 counts messages that `Consume` returns, and kills before processing the Nth. Reason: with the default auto offset store, librdkafka stores the offset of the last message provided to the application (S6 line 140). A kill just after a processed row would leave no message whose offset is stored but not processed, so C10 could not observe the auto-store loss it asks about (inference from S6). Adopted by decision 6895560872, point 2.
- Stop. Round 2 decided that the witness closes after completion, but a member cannot see whether the group's commits reached the end line. ADR-022's "Stop" bullet has each member consume for a scenario-fixed window, then close; the verifier judges completion afterwards. Adopted by decision 6895560872, point 3.
- Start-line coverage: "at the start line every partition is assigned to exactly one member", with its invalid-run condition. Round 1 decided only that both members hold an assignment before the producer starts; without coverage, a partition with no member could look like loss (inference).
- A processed row whose key or sequence number disagrees with its ledger row invalidates the run. This mirrors ADR-012's producer rule.
- Start order. ADR-022 requires the assignment before the start line is recorded; round 1 point 4 says before the load generator starts. The start line precedes the load generator (ADR-013), so this is stricter and changes no outcome.
- "Not reached". The class table gives "any" processed rows with no commit past the message; round 2 point 1 says "neither committed past nor processed". A complete run cannot contain the extra case, because completion requires every partition committed to its end line.
- End conditions. Round 1 point 7 says the witness "ends unexpectedly". ADR-022 makes the expected end the completion receipt written after the stop window, and keeps an incomplete run (window too short) distinct from an invalid one, as ADR-013 does.
- Settings evidence. Round 3 point 3 says the registration marker records pod name and instance ID. ADR-022 also has it record the member's role and effective consumer settings, the evidence configuration fidelity needs.
- Empty partitions. ADR-022 counts a partition whose start and end lines are equal as complete without a commit row, as ADR-013 does for the verifier (blueprint line 451).
- Container and role. ADR-022 pins the container name `consumer-record` and the built-in roles, as decision 6895700580 adopts.
- The committed-offset test of round 2 point 3 is not done by this plan; section 10a assigns it.

(c) Tests or checks that depend on code, data or behaviour neither on `main` nor in this pull request: none. ADR-022 describes behaviour that #505, #506, #507 and #509 implement and test. Its one conditional, the independent committed-offset read, is left to those plans by its own text.

## 10a. Deferred work

1. Independent committed-offset read (decision 6895328446 point 3). Evidence: no source read for this plan states whether `Committed(IEnumerable<TopicPartition>, TimeSpan)` (Confluent.Kafka v2.16.0 `IConsumer.cs` line 563) returns another group's offsets to a client that does not join it, or whether the verifier's Data Receiver role permits it. On the verdict path: yes; it decides whether each C03, C10 and C01 publication says commit evidence is self-reported. Why deferral is permitted: this pull request is prose and cannot run a client; ADR-022 makes the outcome conditional, so the ADR is correct either way. Responsible: the verifier child (#506) implements the read and its agreement check, with a component test against Testcontainers Kafka; the first live smoke run of C03 (#508) settles whether it works with the verifier identity on Event Hubs. Workload bound: one read per partition per repeat; exhaustion action `record the gap and continue`, with commit evidence published as self-reported. Completion evidence: the smoke run's verifier output for the read, success or the error code. Gate it blocks: the C03 verdict (#511) and, through it, the C10 and C01 verdicts (#512, #513) state which case applies.
2. F01 fold row (blueprint line 1214) says K-47's consumer half moved "to C03". After this pull request C03 states K-47 is not tested, so a reader of the F01 row alone could think C03 tests it. Not on the verdict path; outside #504's file scope by the approved split entry. Responsible: the C03 verdict child (#511), whose README and report wording touch the same fact; completion evidence: the F01 row corrected in that pull request or a recorded reason not to. It blocks no gate.

## 11. Open questions for Hari

None. Revision 1's three questions are answered by decision 6895560872, and the check's container and role question by decision 6895700580, both on #504.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it would need to change any literal in section 3, any requirement, the file scope, or when a check in section 6 cannot pass on the pinned text.

## 13. Size estimate

Estimate, 2026-11-15, from a script count of the section 3 literals of this revision: 60 added lines and 6 changed lines in `docs/blueprint.md`, plus `items/504/state.md`. Two files. Size is reported, not enforced.

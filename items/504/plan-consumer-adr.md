# Plan: consumer-processing ADR (ADR-022)

Issue: [#504](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504), child of [#499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499), split entry 1. Pull request name: `consumer-adr`. This is the contract-grade plan for that one pull request under docs/agents/factory.md "Item lifecycle" step 3.

Revision 3. Base: `main` at `23a6fb2639a9e0ea1c74dc466d7eba4a8c0a5c9a`. This is a proposed amendment, not an implementation authorization. It replaces Revision 2 at `4e68d69157a782ef77b8e14ce1798d3833c50607`, incorporating its approval and subsequent decisions.

This plan records a consumer-processing ADR, then leaves the worker, verifier and deployment to their existing children. It replaces an unsupported inference about the newest cross-member commit with an explicit broker observation boundary. Hari must decide the new tradeoffs in section 11 before implementation.

Changes since Revision 2:

- Incorporates all three corrections adopted by [approval 6897525876](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6897525876): configuration-specific durability, disagreement invalidation, and the corrected check anchor.
- Incorporates both consequence corrections adopted by [decision 6897936768](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6897936768).
- Corrects selected option 5 to describe append ordering per configuration.
- Defines successful commit evidence per partition, including automatic callbacks and results of any explicit commits the item's existing policy requires. It adds no explicit commit or Kafka setting merely to manufacture evidence.
- Proposes a post-stop group-offset observation instead of ordering commit rows across members. Missing independent evidence makes the repeat incomplete; a contradicted observation invalidates it. This changes the earlier self-reported fallback and needs Hari's decision.
- Pins empty-partition agreement, including `Offset.Unset`, without creating a synthetic commit row. Adds literal regression examples for the dependent worker and verifier plans.
- Uses unique text anchors on the current base instead of stale line positions. Adds official pinned client sources to the blueprint's source list.

Investigation limit: `git fetch origin 3de4d78115847c302e35db9c0430452129a43a12` returned "not our ref". Both `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/commits/3de4d78115847c302e35db9c0430452129a43a12` and the GitHub connector returned HTTP 422, "No commit found for SHA". The reported cold-review findings are inputs, not a claim that this session inspected that implementation. Revision 2's Record, Completion and Independent commit evidence literals independently contain the defects described. There is no `items/504/state.md` on the current base. No implementation pull request is produced here.

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

13. #504 [approval 6897525876](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6897525876): approves Revision 2 and adopts the three exact corrections in [handoff 6895894202](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895894202). The approval does not approve this amendment.
14. #504 [decision 6897936768](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6897936768): the append is in the processing path; witness replay is possible, not certain.
15. #504 [current session direction 6898138748](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6898138748): Codex revises and gets an independent check, with no implementation, verdict/result change, Azure dispatch or protected-path merge. This supersedes the planning-tool assignment for this session only. It adopts none of section 11's recommendations.

Evidence, not authority: [proposal revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894560135).

There is no item state file (`items/504/` does not exist on the base) and no parent plan: #499 holds a proposal, not a plan. Implementing tool: Codex, also named on #504 in [6897558239](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6897558239). The current request explicitly prohibits implementation even if a prior label or approval remains present.

Where this plan goes beyond the decisions, it says so in section 10 and asks in section 11.

## 2. Scope

Files this pull request may change:
- `docs/blueprint.md`, only the nine edit sites section 3 pins by unique anchors. The ADR insertion is after the final ADR-021 consequence, now base line 731; the fold rows are now 1206 and 1211; the final source row is now 1355. Match text, not obsolete line numbers.
- `items/504/`: the item state file `items/504/state.md` under factory.md "Item state file", and implementation notes only if current repository rules require a repository file. This planning session changes only `items/504/plan-consumer-adr.md`; it does not write the state file or the blueprint.

Must not change: any other line of `docs/blueprint.md`; `AGENTS.md`, `CLAUDE.md`, `docs/agents/`, `docs/engineering/`, `docs/operations/`; `README.md`; `src/`, `tests/`, `scenarios/`, `results/`, `infra/`, `charts/`, `scripts/`, `.github/`.

Protected: `/docs/` is in `.github/CODEOWNERS` line 5 on the base, so Hari merges (AGENTS.md GOVERNANCE "Merge rule").

## 3. External contracts

The published text is pinned literally. "Append" means: add one space after the last character of the named existing text, then the literal text. Every block below is one line unless it shows more. For every replacement or insertion anchor require exactly one match in the named enclosing section; otherwise stop. Numbers in headings identify Revision 2 locations only. Use the full current-base text named in section 9 for reconstruction. No generated implementation artifact is committed by this planning session.

### 3.1 Approval record (insert after line 12)

Insert after line 12 one empty line, then this line:

```
ADR-022, the ADR-019 and ADR-020 amendments it makes, the narrowed C03 row and the C10/C15 note in section 12's "Folded into existing items", and sources S49 to S51 follow the #499 decisions [6894534441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441), [6895299617](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895299617), [6895328446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895328446) and [6895343227](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895343227), and #504 decisions [6895560872](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895560872) and [6895700580](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895700580), [approval 6897525876](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6897525876) and [consequence correction 6897936768](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6897936768); the commit-evidence and empty-partition amendment is proposed in [the consumer-adr plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895443887). Hari's merge of this pull request records approval of them.
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

### 3.5 ADR-022 (insert after the last ADR-021 consequence)

Anchor: the complete last ADR-021 consequence starting `- The experiment reports operational recovery, not a universal causal effect`. Insert after that line one empty line, then this block. The existing empty line and section separator follow unchanged. Current-base anchor: line 731.

```markdown
### ADR-022: C03, C10 and C01 judge a two-member consumer group by a durable per-member processing record joined to the produce ledger

**Context.** C03, C10 and C01 ask what a consumer group does when one member processes too slowly, crashes, or stops heartbeating. ADR-012's oracle classifies produce attempts only. It cannot say whether a consumer processed a message, processed it twice, or committed past it without processing it. ADR-019's configuration fidelity covers producer settings only, and ADR-020 permits one self-kill, B10's.

librdkafka v2.16.0 documents three behaviours these items rely on (Fact: S6, `max.poll.interval.ms`, `session.timeout.ms` and `enable.auto.offset.store` rows). When `max.poll.interval.ms` is exceeded, "the consumer is considered failed and the group will rebalance", and "Offset commits may be not possible at this point". If the broker receives no heartbeats from a member within `session.timeout.ms`, it removes the member and triggers a rebalance. `enable.auto.offset.store` defaults to `true`, and the library recommends `false` with offsets stored after processing for long-time processing. An append blob accepts at most 50,000 appends; a further append returns 409 Conflict (Fact: S49).

**Options considered.**
1. Count consumed messages only. Carries no per-message evidence, so it cannot separate a repeat from a gap.
2. Infer processing from committed offsets. A committed offset does not show that the message was processed, so the C10 loss case is invisible.
3. A per-member record written to a local file and uploaded by that member at the end. A killed member cannot perform its final upload, which is the C10 and C01 case.
4. A per-member record in a Kafka topic. It shares the failure domain under test (ADR-012, option 3).
5. A per-member record appended to Blob and joined to the produce ledger. With store-after-process the processed row precedes the explicit offset store. With the default auto offset store, the library stores the offset on delivery to the application and the processed row follows processing (S6). ADR-012 rejected this pattern for the producer because it adds latency and failures to the produce path being measured (its option 4). Here the write sits in the consumer's processing path, whose processing time is simulated, and its rate effect is a stated consequence.

**Decision.** Option 5, for repeats of C03, C10 and C01 only, under the [round 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895299617), [round 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895328446) and [round 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895343227) decisions on #499 and decisions [6895560872](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895560872) and [6895700580](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895700580) on #504.
- Scope. Every other scenario keeps ADR-012, ADR-013, ADR-019 and ADR-020 unchanged. In these repeats the producer side keeps them unchanged too. A repeat whose producer side is invalid or incomplete under ADR-012 and ADR-013 has no consumer outcome counts.
- Group. Each repeat runs two consumer members in one consumer group, with a `group.id` derived from the run and repeat identity, so no repeat inherits committed offsets. The subject member carries the configuration's fault. The witness runs the baseline consumer with the profile's processing delay and no fault. Both run the enrichment-consumer image and identity, as Kubernetes Jobs on the `workload` pool. Each run records both members' node names.
- Start order. Both members register, subscribe to `document-events` and hold an assignment before the start line is recorded. At the start line every partition is assigned to exactly one member. `auto.offset.reset` keeps its baseline value.
- Registration. Each member writes a registration marker with its pod name and instance ID before it constructs its Kafka consumer. If that write fails, it exits non-zero without consuming. The marker also records the member's role, subject or witness, and its effective consumer settings, which are the evidence for configuration fidelity. Consumer members are not producer instances, so ADR-012's and ADR-020's producer-instance count is unchanged.
- Record. Each member writes these row kinds, each with its instance ID and a row sequence number that starts at 0 and increases by 1: processed, commit, partition-event and, for the subject under "Self-termination", kill-intent. A processed row holds partition, offset, the producer instance and attempt IDs from the message headers, key, sequence number, and processing start and end times. A commit row holds the topic, partition, offset and per-partition error for every entry of one offset-commit result, its overall error, and whether its source was an automatic callback or an explicit call result. The member-local row sequence orders durable observations only; it is not a cross-member broker commit order. The automatic handler does not report explicit `Commit` calls (Fact: S50). If an item already requires explicit commits, the member records the submitted offsets and the synchronous result or exception separately. A successful explicit void return applies to the submitted entries; a per-partition exception retains its result list with overall NoError for the successful native request; a request-level exception records its overall error and establishes no successful entry. No extra commit is made to generate evidence. An entry is successful only when its offset is nonnegative and both the overall result and its partition result report no error. This also rejects request failure with unset partition errors and rejects failed partitions even when overall success is reported (S50). A partition-event row holds assigned, revoked or lost, the partitions, and the time. A kill-intent row holds the partition and offset of the message it names, and the time.
- Durability. Each member appends each row to its own record in the `consumer-record` container of the ephemeral storage account. With store-after-process, it appends a processed row before it stores that message's offset, so "processed" means "recorded". With the default auto offset store, which C10 compares, librdkafka stores the offset when `Consume` returns the message (S6), so the row is appended after that store; that order is what C10 measures. A record is a sequence of append blobs; a member starts a new blob before the current one reaches 50,000 appends (S49). A failed append ends the member with a non-zero exit; with store-after-process it does so without storing that offset. Roles, additions to ADR-003's list: the enrichment-consumer identity gets Storage Blob Data Contributor scoped to `consumer-record`, and the verifier identity gets Storage Blob Data Reader scoped to it.
- No topic writes. The members write nothing to `document-state` or `document-events-dlq`, so ADR-012's deferred oracles for those topics are not needed.
- Self-termination. Only in a configuration whose scenario names a kill count N, only the subject, and only once per repeat. When `Consume` returns the subject's Nth message of the repeat, before processing it, the subject appends a kill-intent row naming that message's partition and offset, then sends SIGKILL to its own process. It sends no other signal and starts no process. This is the second exception in ADR-020.
- Join. The verifier joins each processed row to the produce ledger on producer instance ID and attempt ID, and requires the key and sequence number to agree.
- Classes. Each message that the ledger records as delivered and that the verifier consumed in the run window gets one consumer class:

| Processed rows for its partition and offset | Observed group offset is greater than the message offset | Class |
|---|---|---|
| exactly 1 | yes | processed once |
| more than 1 | yes | processed again; each extra row is counted |
| 0 | yes | committed but never processed |
| any | no | not reached |

- "Committed but never processed" is consumer loss. "Not reached" makes the run incomplete, not loss.
- Commit observation. This contract does not select a newest row across members. The exposed callback fields supply no such ordering token (Fact: S50); ordering by member-local sequence, callback time, blob append order, member name or maximum offset would be inference without a supported broker order. Partition leader epoch is not consumer-group generation. Do not decode opaque group metadata, use reflection or add native calls to obtain a speculative order.
- Stop and observation boundary. Each member consumes for the window fixed in the scenario, measured from its own start, then closes its consumer, finishes recording callbacks from that close, and writes its completion receipt as its last action. The configured subject kill retains its kill-intent ending instead. Workflows confirm both consumer processes have terminated before the verifier reads group offsets; Jobs and infrastructure remain workflow-owned (ADR-020). The verifier records the end line after the load generator ends, as ADR-013 sets. It may do that while consumers still run, but does not classify until the stop evidence and offset observation are available. The post-stop offset read defines the observation boundary; it is not proof that an earlier in-flight request can never change broker state afterward. Counts describe the state observed at that boundary, not eventual state. A killed member may have committed without persisting the callback; do not invent a row in that case.
- Independent commit evidence. The verifier constructs a separate offset-reading client using its existing identity, the repeat's consumer `group.id`, `enable.auto.commit=false`, and the same `isolation.level` as the consumer group. Its ordinary ADR-013 verification consumer keeps its existing settings and group. The offset reader does not call `Subscribe`, `Assign`, `Consume`, `Commit` or `StoreOffset`; it calls `Committed(partitions, timeout)` with the explicit full run partition set and then disposes. The managed/native call path issues OffsetFetch without a subscription (Fact: S50, S51). This is source evidence for a supported client path, not proof of permission on Event Hubs. Make one managed read call per repeat, with no harness retry and a timeout bounded by the item's completion deadline. The native client's internal retries remain bounded by that call. Retain the group ID, requested partitions, returned values or errors, and read start/end times as evidence. A missing, timed-out, erroneous, duplicate-partition or incomplete response cannot authorize counts: mark the repeat incomplete with reason `consumer_commit_evidence_unavailable`. Keep the member records as self-reported diagnostics, not authoritative outcome counts. Do not widen the verifier's roles.
- Commit agreement. For each partition p, let R[p] be the set of nonnegative offsets in its successful commit entries from either member; let B[p] be the offset in the successful full group read. This set carries values, not commit chronology. A concrete B[p] must equal an element of R[p]. Equal offsets in several rows need no tie-break. A different value in R[p] does not contradict B[p] and does not overrule it, even if it is larger. If B[p] is `Offset.Unset`, R[p] must be empty. `Offset.Unset` means no stored offset, not zero (Fact: S51). Any other negative value, or a concrete value with no matching successful entry, or `Offset.Unset` with a successful entry, invalidates the repeat with reason `consumer_commit_record_disagreement`. A failed commit entry never supplies the match. This corroborates the observed broker value, not the identity or ordering of the last commit operation.
- Completion. After validity and commit agreement pass, a partition with start S[p] equal to end E[p] is complete, including when B[p] is `Offset.Unset` and R[p] is empty. An empty partition with a concrete B[p] still needs the matching successful entry. For S[p] less than E[p], completion requires a concrete B[p] at or beyond E[p]. A missing stored offset or B[p] below E[p] makes the run incomplete with reason `consumer_commit_frontier_short`. All stop evidence and the successful read must be available within the item's fixed completion deadline, a harness budget rather than an Event Hubs performance claim. Deadline exhaustion makes the run incomplete with reason `consumer_completion_deadline`. Do not force or synthesize a commit on an empty partition. The verifier classifies only a complete, valid run; invalid or incomplete runs retain diagnostics and have no consumer outcome counts. Existing invalid-run conditions take precedence over incompleteness; an unavailable read does not become a disagreement against fabricated values.
- C01 timing. Reassignment time runs from the subject's last row before it ends to the witness's assigned row for the subject's partitions. When the two members ran on different nodes, that repeat's timing is unavailable.
- Configuration fidelity. ADR-019's check 1 also covers the consumer members' effective settings for these items: the subject's must match the selected configuration and the baseline, and the witness's the baseline. A mismatch invalidates the run.
- Invalid run. A repeat has no consumer outcome counts when any of these hold:
  - its producer side is invalid or incomplete under ADR-012 and ADR-013;
  - a member's record is missing, or has a gap or a repeated row sequence number;
  - other than two consumer members registered;
  - at the start line, a partition was assigned to no member or to both;
  - a processed row names an offset outside the run window, or a message with no ledger row, or a key or sequence number that disagrees with its ledger row;
  - with a kill count configured, the subject ended other than at its kill-intent row; with none configured, the subject wrote a kill-intent row or ended without its completion receipt; or the witness ended without its completion receipt;
  - a consumer member's effective settings do not match the configuration;
  - the successful group-offset read disagrees with the commit records under Commit agreement.

  An item plan adds only the checks its decision rules need (ADR-019).

**Consequences.**
- Consumer outcome classes describe Event Hubs and the client, as ADR-012's classes do. A harness bookkeeping fault invalidates the run instead.
- The append is in the processing path, so it bounds each member's processing rate. The effect is not measured. Each item plan sizes its workload and deadline for it.
- With store-after-process, a kill before processing leaves that offset unstored, so the witness can reprocess messages whose offsets were stored since the last commit (inference from S6). With the default auto offset store, the offset of the message being processed is already stored, so a commit before the kill can pass it without a processed row (inference from S6). C10 measures both.
- Complete consumer outcome counts require independent broker-offset evidence at the post-stop observation boundary. If that read is unavailable, only self-reported diagnostics are retained. This is stricter than the earlier self-reported fallback. A successful broker commit whose result was lost at subject death can invalidate agreement; that evidence limit is not consumer loss.
- A matching commit row corroborates the observed value but does not prove which member committed last. A later offset change is outside this observation boundary. No total commit chronology or eventual-processing claim is published.
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

### 3.8 Sources S49 to S51 (append after the current last source row)

The complete S48 row is the unique anchor, currently base line 1355. Insert after it these rows and end the file with one newline:

```
| S49 | Azure Storage REST API: [Append Block](https://learn.microsoft.com/rest/api/storageservices/append-block#remarks), remarks: "A maximum of 50,000 appends are permitted for each append blob"; more returns 409 (Conflict) | read 2026-11-15 |
| S50 | Confluent.Kafka v2.16.0, commit 56ef87df3402861f0dc72ef921fcfac702ae1f16: [CommittedOffsets.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/CommittedOffsets.cs), [ConsumerBuilder.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/ConsumerBuilder.cs), [Consumer.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/Consumer.cs), [SafeKafkaHandle.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/Impl/SafeKafkaHandle.cs), [TopicPartitionOffsetError.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/TopicPartitionOffsetError.cs) and [ConsumerGroupMetadata.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/ConsumerGroupMetadata.cs) | tag v2.16.0, read 2026-11-15 |
| S51 | librdkafka v2.16.0, commit c6c9973cf1b25394c58e0cade13a36f386856f77: [rdkafka.h](https://github.com/confluentinc/librdkafka/blob/c6c9973cf1b25394c58e0cade13a36f386856f77/src/rdkafka.h), `rd_kafka_committed`; [rdkafka.c](https://github.com/confluentinc/librdkafka/blob/c6c9973cf1b25394c58e0cade13a36f386856f77/src/rdkafka.c), `rd_kafka_committed`; [rdkafka_cgrp.c](https://github.com/confluentinc/librdkafka/blob/c6c9973cf1b25394c58e0cade13a36f386856f77/src/rdkafka_cgrp.c), offset fetch and commit result paths | tag v2.16.0, read 2026-11-15 |
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
2. WHEN a later child cites the consumer record, its classes, commit observation and agreement, empty partitions, completion, stop, durability, kill point or invalid-run conditions THEN ADR-022's text is the source (3.5).
3. WHEN the verifier's configuration-fidelity check is read for C03, C10 or C01 THEN ADR-019 covers consumer settings, the subject's against the selected configuration and the witness's against the baseline (3.2, 3.3, 3.5).
3a. WHEN the verifier's closed list of check sources is read for C03, C10 or C01 THEN it includes ADR-022's invalid-run conditions (3.9).
4. WHEN ADR-020's process rules are read THEN they name the ADR-022 self-kill as a second, scoped exception (3.4).
5. WHEN section 12's fold table is read THEN C03 states K-47 is not tested, and the C10, C15 row states K-57 is tested under C10 only (3.6, 3.7).
6. WHEN the status header is read THEN it records what this pull request adds and that Hari's merge approves it (3.1).
6a. WHEN a later child implements commit evidence THEN it implements the post-stop read, value-set agreement and completion predicates in 3.5; it never orders member streams by local sequences, timestamps, append order, maximum offset or leader epoch.
6b. WHEN an empty partition has no successful commit entry THEN a successful read returning `Offset.Unset` permits completion without an invented row; a concrete offset without a match is invalid.
6c. WHEN the independent read is unavailable THEN it records `consumer_commit_evidence_unavailable`, retains diagnostics and withholds consumer counts; it does not silently use the Revision 2 fallback.
7. WHEN any line outside section 2's list differs from the base THEN the pull request is out of scope and acceptance check A8 fails.

## 5. Invariants and safety rules

- ADR-012, ADR-013 and ADR-021 text is unchanged. Producer-side rules are unchanged for every scenario.
- No verdict, expected outcome or `results/` file changes (AGENTS.md "Verdict integrity").
- No identity or role is created here. ADR-022 records role additions; `infra/ephemeral/identity*.tf` changes in #507.
- No new inventory item (blueprint line 4; AGENTS.md GOVERNANCE "Scope"). The two section 12 rows are narrowed or annotated, not added.
- ASCII punctuation only, no em or en dashes (AGENTS.md "Writing standards").

## 6. Test map

The change is prose; there is no code. Requirements 6a to 6c are checked by A1 against the complete literal ADR block. Each other requirement maps to a scripted text check on the pull request head (section 8). This is not verifier work, so docs/agents/pull-requests.md's predicate coverage does not apply.

| Requirement | Check | Input | Literal expected value |
|---|---|---|---|
| 1, 2 | A1 | `docs/blueprint.md` | the 3.5 block occurs exactly once; the line `### ADR-022: ...` comes after the line starting `### ADR-021:` and before the line `## 5. Failure modes` |
| 3 | A2 | `docs/blueprint.md` | the new lines of 3.2 and 3.3 each occur once; their old lines occur zero times |
| 3a | A10 | `docs/blueprint.md` | the 3.9 new sentence occurs once in ADR-019, immediately after the unique 3.9 anchor and a space (base line 601, head line 603 with only 3.1 before it) |
| 4 | A3 | `docs/blueprint.md` | the 3.4 appended sentence occurs once, on the line that contains `The sole production process-launch exception is ADR-021's B10 parent process.` |
| 5 | A4 | `docs/blueprint.md` | the new rows of 3.6 and 3.7 each occur once; the old rows occur zero times |
| 6 | A5 | `docs/blueprint.md` | the 3.1 line occurs once immediately after the full final B10 status paragraph of the pinned base, with one intervening empty line |
| sources | A6 | `docs/blueprint.md` | each 3.8 row occurs once in order after the base S48 row; the S51 row is last; the file ends with one newline |
| 7 | A7 | `git diff --name-only origin/main...HEAD` | only `docs/blueprint.md` and paths under `items/504/` |
| 7 | A8 | the base file and the head file | applying 3.1 to 3.9 to the base text by script gives the head file byte for byte |
| style | A9 | added lines | no byte outside printable ASCII, tab and newline |

Existing tests: no test asserts on docs/blueprint.md's wording. `scripts/docs-rules.mjs` lines 62 to 85 on the base read its ADR and source definitions; acceptance command 4 runs those checks. The ADR adds contract checks for later children, not executable checks here. Standing fixture rule for those children: change a newly rejected existing fixture to pass the new checks while keeping every assertion and its tested boundary, or, when invalid input is the purpose, retain the input and set expectations to exactly these predicates. Never remove a case, loosen assertions or change a check to fit a fixture; list each fixture change and the forcing check in PR implementation notes.

### 6.1 Literal downstream predicate cases

These synthetic examples specify the amended contract. They are not test implementations, measured results, or claims about Event Hubs. The dependent plans in #505 and #506 must adopt them before their code lands. Unless a row says otherwise, use one partition with S=100, E=110, a valid producer window, two properly registered and ended members, valid settings and records, and a successful full group read returned before the deadline. R is as defined in 3.5. `NoError` means both overall and partition errors are clear. Keep all unrelated evidence valid.

| Case / named downstream test | Input | Literal expected value |
|---|---|---|
| D1 CrossMemberValueNotLargest | subject local row 90 reports 115; witness local row 2 reports 110; B=110 | complete valid, frontier=110; reversing member names, sequence magnitudes, callback timestamps or blob enumeration changes nothing |
| D2 FrontierBoundary | R={109}, B=109; R={110}, B=110; R={111}, B=111 | respectively incomplete `consumer_commit_frontier_short`, complete valid, complete valid |
| D3 EqualOffsetTie | each member reports 110; B=110 | complete valid; no tie-break or invented broker order |
| D4 MissingOrFailedMatch | R={109}, B=110; then add an entry at 110 with overall NoError but partition error; then overall error but partition NoError | each invalid `consumer_commit_record_disagreement`; an entirely successful 110 entry changes result to complete valid |
| D5 NoStoredNonempty | R={}, B=Offset.Unset | incomplete `consumer_commit_frontier_short`; never treat Unset as 0 |
| D6 EmptyNeverCommitted | S=E=0 and then S=E=100; R={}, B=Offset.Unset | each complete valid with no commit row required |
| D7 EmptyWithEvidence | S=E=100; R={100}, B=100; then R={99}, B=99; then R={101}, B=101 | each complete valid; no data to reach, agreement still required |
| D8 EmptyContradiction | S=E=100; R={}, B=0; then R={100}, B=Offset.Unset; then R={100}, B=101 | each invalid `consumer_commit_record_disagreement`; never manufacture a 0 or 100 row |
| D9 NoReadOrBadResponse | timeout, authorization error, missing partition, duplicate partition, or response-level error, including with S=E | each incomplete `consumer_commit_evidence_unavailable`, counts absent |
| D10 OtherNegative | B=-2 with valid response shape, R={} | invalid `consumer_commit_record_disagreement`; only Offset.Unset (-1001 in the pinned client) has the no-stored-offset meaning |
| D11 PartitionIsolation | p0 S=100 E=110 R={110} B=110; p1 S=E=50 R={} B=Offset.Unset | complete valid; replacing p1 B with 0 makes the repeat invalid; p0's 110 cannot match p1 |
| D12 Deadline | all evidence arrives just before deadline, exactly at deadline, just after deadline | first two satisfy the budget; last is incomplete `consumer_completion_deadline` |
| D13 ValidityPrecedence | malformed/gapped member record plus a read timeout; then valid records plus B=109 matching R | first invalid under existing record condition; second incomplete `consumer_commit_frontier_short`; no authoritative counts in either |
| D14 SuccessfulReadBeforeStops | full read returns B=110 before required member termination evidence | cannot classify from that read; fixed protocol requires a post-stop read, not a retry loop; missing timely post-stop read is incomplete |
| D15 ExplicitResultCapture | automatic success at 109; an already-required explicit commit of 110 returns successfully without invoking the handler | record explicit result separately; R={109,110}; B=110 passes agreement; empty explicit argument list is a no-op, adds no successful entry |
| D16 UnrecordedKillTail | broker B=110, only durable success entry is 109 because subject died before recording the later callback | invalid `consumer_commit_record_disagreement`; do not count the evidence gap as consumer loss |

For D12, child plans choose their existing timestamp resolution and budget value; "just before/after" means one representable tick below/above that value. Those choices must be explicit before implementation of that child. These cases assume the partition set recorded by ADR-013; they add no new partition-discovery predicate. Per-partition empty windows D6 to D8 remain valid cases. If a later child needs an additional predicate, change its plan under the existing decision rules rather than adding a guard here.

## 7. Deployment assumptions

This pull request deploys no container, chart, workflow or runtime. Its acceptance checks operate on prose only. The following downstream capability remains unverified and is an open named risk in section 11, not a claim of readiness:

- `ConsumerGroupOffsetRead`: the existing verifier identity can obtain the repeat group's committed offsets, including explicit no-stored-offset responses, from Event Hubs without joining it. Source examination establishes the pinned client call path, but not Event Hubs authorization or server behavior. No live evidence was taken, on either a fresh or already-open connection, idle or under load. Required disposition, owner, workload bound and blocking publication gate are in section 10a. Do not silently widen identity scope or treat a local Kafka component test as Event Hubs proof.

No clock synchronization or generation-order assumption is introduced: member timestamps are observations, and offset-value equality replaces chronology selection. The observation boundary does not promise that state never changes after the read; Hari must accept that scientific scope in section 11.

## 8. Acceptance commands

Run on the pull request branch before the cold review.

1. A1 to A6, A9 and A10: one script that reads the file, counts each literal of section 3 by exact string match and checks the stated positions, printing `PASS <id>` or `FAIL <id>: <reason>`. Evidence: one `PASS` per listed check and exit status 0; compute the count from the check list, not a copied total.
2. A7: `git fetch origin main && git diff --name-only origin/main...HEAD`. Evidence: the printed list.
3. A8: a script that applies 3.1 to 3.9 to `git show origin/main:docs/blueprint.md` and compares with the head file. Evidence: `identical`.
4. `npm run check:docs`. Evidence: the summary line; each warning in docs/blueprint.md fixed or explained under docs/agents/pull-requests.md rule 8.
5. Verification gates (docs/agents/pull-requests.md "Verification gates"): steps 1 and 3 do not apply, because no file under `src/`, `tests/` or the other paths of its last paragraph changes. Step 2, the cold review under rule 8, applies. Step 4, the hosted `.NET verification` check on the final head, runs as the required check. The Claude review gate of decision 6894587233 applies after Codex opens the pull request. No pull request opens in this planning session. Later protected-path merge remains Hari's.

## 9. Sources

Sources were read on 2026-11-15. Official documentation tool: `search_service_web_run.open/find` for Microsoft Learn Append Block and pinned raw Confluent sources; `exec_command` with HTTPS raw-source fetches and `gh api` REST for source bodies, tag resolution and repository evidence. The source-reader's claims used here were checked against the fetched source at the symbols below. Nothing relies on recollection of library behavior.

1. Blueprint S6: [librdkafka CONFIGURATION.md](https://github.com/confluentinc/librdkafka/blob/c6c9973cf1b25394c58e0cade13a36f386856f77/CONFIGURATION.md), v2.16.0 commit `c6c9973cf1b25394c58e0cade13a36f386856f77`, lines 131 and 137 to 140. Confirms the timeout descriptions, background auto commit, and in-memory auto-store of the next offset. Quoting those rows does not establish heartbeat-thread behavior; that remains the pause-test child's obligation under decision 6894564479.
2. Proposed blueprint S49: [Azure Storage Append Block](https://learn.microsoft.com/en-us/rest/api/storageservices/append-block#remarks), page last updated 2026-01-20, read 2026-11-15. Remarks confirm the 50,000-append ceiling and 409 Conflict beyond it. This live documentation has a read date rather than an immutable revision; the Learn Edit link and GitHub contents/history fetch returned 404 in this session. No other service property depends on that unavailable source repository.
3. Proposed blueprint S50, Confluent.Kafka v2.16.0 commit `56ef87df3402861f0dc72ef921fcfac702ae1f16`, resolved via `gh api` tag ref. [CommittedOffsets.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/CommittedOffsets.cs#L29), lines 29 to 60, defines aggregate/per-partition errors and the exposed payload. [ConsumerBuilder.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/ConsumerBuilder.cs#L706), lines 706 to 718, excludes explicit Commit from the automatic handler. [Consumer.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/Consumer.cs#L342), CommitCallback lines 342 to 355, explicit Commit lines 496 to 521, Committed lines 536 to 544 and Close lines 599 to 610; [SafeKafkaHandle.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/Impl/SafeKafkaHandle.cs#L915), Commit lines 915 to 977, Committed lines 1104 to 1132. These show explicit returns/exceptions, empty explicit-list no-op, no subscription in the explicit-partition read path and per-partition query exceptions. [TopicPartitionOffsetError.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/TopicPartitionOffsetError.cs#L87) identifies leader epoch as belonging to the offset; [ConsumerGroupMetadata.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/ConsumerGroupMetadata.cs#L23) exposes an empty interface and internal serialized payload. Inferring that none of these fields supports global commit chronology is explicitly an inference from the exposed contract, not a broker guarantee.
4. Proposed blueprint S51, librdkafka same commit as source 1: [rdkafka.h](https://github.com/confluentinc/librdkafka/blob/c6c9973cf1b25394c58e0cade13a36f386856f77/src/rdkafka.h#L4718), `rd_kafka_committed`, lines 4718 to 4736, defines no-stored-offset return; [rdkafka.c](https://github.com/confluentinc/librdkafka/blob/c6c9973cf1b25394c58e0cade13a36f386856f77/src/rdkafka.c#L5425), lines 5425 to 5489, submits OFFSET_FETCH; [rdkafka_cgrp.c](https://github.com/confluentinc/librdkafka/blob/c6c9973cf1b25394c58e0cade13a36f386856f77/src/rdkafka_cgrp.c#L7492), lines 7492 to 7508, sends group-ID/partition request without a joining member. Lines 4573 to 4580 and 4779 suppress empty automatic commit notifications. This is why an empty partition must not require an invented callback. Native close (`rdkafka.c` lines 5299 to 5364, `rdkafka_cgrp.c` lines 4076 to 4088) services callbacks and waits for outstanding commits in the normal graceful path; this is not a guarantee of durable capture after SIGKILL. [Offset.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/Offset.cs#L33), lines 33 to 56, gives Offset.Unset=-1001.
5. [docs/blueprint.md on the base](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/23a6fb2639a9e0ea1c74dc466d7eba4a8c0a5c9a/docs/blueprint.md): status lines 1 to 14, shared workload node lines 137 to 141, ADR-003 role decision line 211, ADR-005 consumer configuration, ADR-012 lines 367 to 429, ADR-013 lines 431 to 466, ADR-019 lines 582 to 619, ADR-020 lines 621 to 658, ADR-021 and separator lines 659 to 735, fold table lines 1196 to 1220, source definitions lines 1298 to 1355. A base diff against Revision 2 shows an unrelated AVM wording update and the archive-readiness consequence inserted in ADR-020; preserve both. ADR-022 is not present on this base.
6. [CODEOWNERS on the base](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/23a6fb2639a9e0ea1c74dc466d7eba4a8c0a5c9a/.github/CODEOWNERS#L5), line 5, covers `/docs/`. [BaselineSettingsCatalog.cs](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/23a6fb2639a9e0ea1c74dc466d7eba4a8c0a5c9a/src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs#L175), enrichment entries lines 175 to 190: auto commit true, interval 5000, auto-store false, reset latest. No setting is changed by this ADR plan.

## 10. Plan-check questions (factory.md "Item lifecycle" step 1)

(a) This prose pull request has a fixed set of edit sites and fixed text checks, not runtime-sized implementation work. Authority and source reads are exempt under AGENTS.md rule 11. Any unknown warning count is measured once; dispose of at most 10 changed-file documentation warnings (policy cap, not measured), then `push -wip and hand off to the next session` with the remaining warnings. Unchanged-file warnings use rule 8's blanket disposition. No generic runtime-sized fix loop is authorized. The independent check follows plan-pr step 5's cumulative cap; the two earlier blocking checks are recorded, not reset. The current user expressly requests this revision and its check; do not begin another rewrite-and-check loop on its findings. Downstream offset-reading work is bounded in section 10a.

(b) Revision 2's adopted refinements stay: roll-over, kill-before-processing the Nth Consume return, scenario-fixed stop window, exact start-line coverage, key/sequence agreement, settings/role evidence, and scoped container roles. Its narrower start-before-start-line rule and not-reached class remain unchanged. Its three approval corrections and two later consequence corrections now appear literally. Necessary new amendments, all pending section 11: replace cross-member newest-row selection with a post-stop observed broker value and matching-record membership; require independent evidence for counts instead of the earlier conditional self-reported fallback; specify Unset and concrete-value agreement for empty partitions. Added capture detail covers explicit results because the pinned managed callback excludes them; it does not introduce an extra commit policy. No outcome definition, node rule, signal rule, producer oracle, inventory scope or storage permission is otherwise changed.

(c) No acceptance check for this prose pull request depends on unmerged executable code. The algorithm's downstream examples are requirements for #505/#506, not tests claimed to run here. The native/managed source settles the client query path and Unset semantics. The Event Hubs verifier-identity capability is explicitly unverified under `ConsumerGroupOffsetRead`, with its owner and authorized test stage in section 10a and an open named-risk question in section 11. No live infrastructure is changed by this pull request.

## 10a. Deferred work

1. Consumer worker and verifier implementation remain in #505 and #506 under the approved split, after this ADR is approved and merged. Before either implementation merges, its own revised approved plan must reference ADR-022's new contract and tests D1 to D16; implement only its applicable cases and preserve the standing fixture rule. #505 supplies durable automatic and already-required explicit commit result capture, close callback draining, receipt/kill evidence and record seams. #506 supplies the offset reader, predicates and reason names. Those children pin their own JSON/CLI wire formats, deadlines, Kafka component tests and required counts before their code lands. No new wire format is implemented by #504. Evidence: source 3 establishes the callback limitation and query path. Deferral is permitted because these are the split's existing code children, not preparation hidden by this prose PR. Completion: their independent checks and reviewed PRs against the amended ADR. Gate: #507's integration and all consumer verdicts. Each child supplies fixed test/fixture workload bounds and exhaustion actions before execution; this plan authorizes no such execution or uncapped fixture-repair loop.
2. `ConsumerGroupOffsetRead` preparation: #506 must component-test a fresh unjoined client on local Kafka, after two member clients have stopped, both after an idle empty run and a run with synthetic messages. Assert explicit Unset for never-committed partitions and the exact observed offsets after committed messages, using the repeat group ID rather than the fixed verifier group. Source evidence above already removes any need to defer discovery of the .NET call shape. This is a premerge prerequisite for #506; a local broker cannot establish Azure permissions. The existing #508 smoke stage is responsible for Event Hubs verification using the existing identity after #507 integrates it. Only an independently authorized scenario workflow on main may execute that stage; this session dispatches nothing. The smoke observes one fresh offset-reading connection per repeat after the workload has stopped, not an already-open under-load connection. One managed group-offset read over all run partitions per repeat, zero harness retries. On exhaustion: `record the gap and continue` independent diagnostics; the repeat stays incomplete and consumer-count publication stays blocked. Evidence: raw requested partition set, returned offsets/Unset or actual errors and time interval, plus the worker stop evidence. A denied operation is a decision for Hari, never a wider role. Before #507 merge, its plan must state that diagnostic evidence survives a failed read and workflow wait, and must pin stop/reader ordering. #508 proves the server capability at the existing authorized live stage; no earlier agent session has Azure credentials. Required risk acceptance is section 11 question 3. This blocks consumer verdicts #511 to #513, and is not a claim that the earlier #504 prose merge makes the runtime ready.
3. F01 fold row still says the consumer refresh hypothesis moved to C03. Correcting it is optional in the approved split and outside #504's pinned changes. Responsible: C03 verdict child #511, whose report describes the same scope. It is not a verdict prerequisite; completion evidence is a corrected row in that authorized PR or a reason for leaving it. No execution workload applies.

## 11. Open questions for Hari

These are proposed design amendments. Earlier recommended-answer adoption is not treated as approval of a new external contract, and this Codex subagent check cannot supply standing approval under factory.md "Item lifecycle" step 3. An explicit approval must name this revision and its commit SHA and adopt the choices it accepts.

1. **Commit chronology and observation scope.** Approve replacing "newest successful commit row" with the post-stop broker observation and matching successful record entry, including explicit-result capture, as pinned in 3.5? Recommended: yes. Do not fabricate total order from local counters, timestamps, leader epoch or maximum offsets. The read defines a scientific observation boundary, not eventual finality or last-committer identity. This changes round 2's completion and the Revision 2 agreement rule, but retains invalidation when observed commit evidence is contradicted. Alternative: retain newest-row semantics and first approve a different client/capture/serialization protocol that proves per-commit order; it would need another design and could alter the auto-commit experiment. That alternative is not implemented or implicitly authorized here.
2. **Empty partitions and unavailable evidence.** Approve the 3.5 empty-partition agreement rules and requiring a successful independent read for every partition before counts, including empty windows? Recommended: yes. An empty window may have no commit and Unset even when its equal start/end lines are nonzero. A concrete observed offset still requires a successful matching entry. The tradeoff is removal of complete self-reported results when the read fails; retain diagnostics and report incomplete. The alternative of forcing a final commit would change the consumer under test and could overwrite evidence; do not adopt it without a new decision.
3. **Named downstream risk: ConsumerGroupOffsetRead.** Accept that this design-only ADR may merge before Event Hubs has proved the existing verifier identity's unjoined group-offset read, provided #506 completes local component proof before its merge, #507 pins diagnostic/stop ordering before integration, and #508 verifies the server capability at its separately authorized smoke stage? Recommended: yes, with no identity expansion and no consumer counts until evidence succeeds. The risk is that this API path may fail on Event Hubs and block the consumer verdict route despite source-supported client behavior. No source or local Kafka test resolves the Azure permission question. The observation scope in question 1 also accepts that a killed subject's unrecorded successful commit can make a repeat invalid, reducing usable repeats without being classified as loss.

## 12. Stop rule and handoff

Do not implement this revision until Hari's approval names its revision and commit and resolves section 11, with the required independent check linked. The current session publishes only this plan and its pointer/check/handoff comments; no PR, state-file update, implementation, label, workflow dispatch or merge.

The eventual implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" if a section 3 literal, requirement, scope or enforced predicate needs changing, if an anchor is missing/ambiguous, or if an acceptance check cannot pass the pinned text. Re-read authority before a push or publication. Preserve any implementation-branch work; this plan branch does not replace or reset that branch's review budget. The supplied implementation commit is unavailable in this session; recover its branch/PR and existing review history before implementation, without claiming that the three reported findings are a complete review.

After this check, batch its findings with section 11 for Hari. Keep the earlier check history: Revision 1 check 6895667441 and Revision 2 check 6895877604 had blocking findings, then Hari approved Revision 2 with exact changes. The present request authorizes a revision and a check, not an unlimited new check budget. No further rewrite-and-check is started here. A next implementing session must propagate any approved amended ADR to the existing worker, verifier and integration plans before their implementation, retain the Claude PR review gate, and leave the protected blueprint merge to Hari.

## 13. Size estimate

Estimate, 2026-11-15: a scratch text reconstruction of the section 3 literals against the pinned base gives 72 added lines and 6 removed lines in `docs/blueprint.md`. These are diff line counts, not a measurement of runtime work. The prose pull request changes two files: the blueprint and its implementation state file under `items/504/`. The state-file estimate is 26 added lines, based on eight template field/heading lines, fifteen authority entries and three open-question entries; the implementer records the actual size rather than trimming to it. This is a scope/size estimate, not a runtime measurement or enforced size limit. The plan-only branch contains this plan file alone.

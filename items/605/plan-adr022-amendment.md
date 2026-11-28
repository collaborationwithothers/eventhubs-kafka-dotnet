# Plan: ADR-022 amendment for C18, C06 and C04

Issue: [#605](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/605), child of [#597](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597), split entry 1. Pull request name: `adr022-amendment`. This is the contract-grade plan for that one pull request under docs/agents/factory.md "Item lifecycle" step 3.

Revision 3. Base: `main` at bc377bd7591183045678dc928df4bf990e52407d. Line numbers below are on that commit. Revisions 1 and 2 were written against 15b09147ed4e7c62ea08620b24c14cd049f07562; the only files that differ between the two commits are three mutation-test scripts under `tests/Parchwell.Verifier.Tests/Mutation/`, so every line number and citation below holds on both.

Changes since revision 2, applying [#605 decision 7236060793](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/605#issuecomment-7236060793), by its point:

- Point 1: the fourteen open questions are answered as recommended; section 11 now states `None`, and section 11a keeps the questions and their answers as a record.
- Point 2: the sixth 3.7 consequence is replaced with the text the decision request gave for option (a). The "Stop" bullet already has the subject finish the message in progress.
- Point 3: 3.7 "Shutdown margin" records two times, one from the subject's own record and one from pod evidence; the stop receipt gains its write time; 3.7 adds the consequence on reading a deleted pod's end time, and section 1 lists it as a downstream prerequisite. The literal text for the addition is pinned in 3.7 for the plan check.
- Point 4: the overlap invalid-run sub-bullet now rests on the workflow's confirmation.
- Point 5: 3.6 now appends to the end of the decision line; the 3.7 context says "suggests considering"; the 3.7 context adds the `Dispose` sentence, and S52 adds `Consumer.cs` and `SafeKafkaHandle.cs`.
- Point 6: section 1 records that the reason in decision 7235680921 point 5 is corrected, and its condition stands.
- Base moved to `bc377bd7591183045678dc928df4bf990e52407d`; section 1 lists decision 7236060793 and records #597 decision 7236013792, which this child has not adopted and which changes nothing here.

Revision 2's changes from the [revision 1 plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/605#issuecomment-7235835025) remain in the text. The [revision 2 plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/605#issuecomment-7235927753) findings 1 to 7 are applied through decision 7236060793; its finding 8 (the auto-commit-on half of K-56) needs no change here, as that check says.

The size estimate in section 13 is recomputed by script.

This pull request amends ADR-022 in docs/blueprint.md so the consumer oracle covers C18 (graceful shutdown on SIGTERM), C06 (static membership across one stop and one rejoin of Job members) and C04 (constant rebalancing, and a crash-looping consumer). It also records in section 12 which folds of those items are tested and which are not. It changes only prose, and its wording is pinned literally below. Every design point in that wording that Hari's decisions do not settle is listed in section 11 with a recommendation; the literal text carries the recommended answer, and the plan is not approvable until Hari answers.

## 1. Goal and authority

Goal: record the design of the stop, successor and consumer-error endings that C18, C06 and C04 need, as an amendment to ADR-022 in the blueprint's format (context, options, decision, consequences; AGENTS.md "Skills"), so the C18, C06 and C04 children cite one design authority.

Authorities, in creation order (factory.md "Item lifecycle" step 3, child issues):

1. #605 body (created 2026-11-28; the issue's `updated_at` moves with each comment, and the body text was read again at the revision 2 refresh): `Parent items: #597`, `Split entry: 1`, `Predecessor PRs: none`. It states that the ADR text, every protocol sequence, failure name, invalid-run condition and verdict rule are Hari's to decide, and that the plan decides none.
2. #597 body: a request for a proposal; authorizes no branch, code, label, dispatch, expected outcome or verdict.
3. #597 [decision 7235176688](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7235176688), adopting [#499 decision 7234963031](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-7234963031): plan approval by label on a fresh check with `Blocking findings: none` and open questions `None`; literal in-scope fixes; what still goes to Hari; Claude Code implements, planning on `claude-opus-5-5`, implementation on `claude-sonnet-5-5`.
4. #597 [decision 7235586661](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7235586661): the revision request that produced proposal revision 2. It decides no design.
5. #597 [decision 7235680921](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7235680921): points 1 to 6 (batch and order; narrowing; SIGTERM by workflow pod delete with no ADR-020 exception; C04 restarts as sequential subject Jobs started by the workflow, with no ADR-017 change and back-off not tested; no third C18 configuration, with the condition that the C18 plan measures the time from the observed SIGTERM to process exit against the 30 s grace for each repeat; no early `group.instance.id` check). It states "Not decided here: any ADR or blueprint text".
6. #605 [decision 7235685248](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/605#issuecomment-7235685248): links and adopts authorities 3 to 5 for this child.
7. #605 [decision 7236060793](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/605#issuecomment-7236060793) (posted 2026-11-28T07:37:38Z): Hari's answers to the revision 2 decision request, "yes to all recommendations with your addition": the fourteen questions as recommended; option (a) for the message in progress; the deleted-pod consequence plus the shutdown margin from the subject's own record; the overlap condition on the workflow's confirmation; the three wording fixes; and the correction of decision 7235680921 point 5's reason, whose condition stands. It is not an approval; approval follows authority 3 and Hari's label.

Also read, not adopted on this child: #597 [decision 7236013792](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7236013792) approves the proposal's split and keeps two configurations per consumer scenario. A parent decision changes this child's plan only when a decision on the child adopts it (factory.md "Item lifecycle" step 3). It agrees with this plan and changes no text in it.

Account note: decisions 7236060793 and 7236013792 are posted by the `haripraghash-bot` login. `scripts/decision-log.py` prints only comments by `haripraghash` (factory.md "Decision log"), so its log omits both. This plan recognizes them under factory.md "Item lifecycle" step 3, which recognizes a decision by its heading and by its recording Hari's decision, not by account; earlier decisions on #597 were recorded the same way by the control session.

Evidence, not authority: [proposal revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7235228182) and its [decision request](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7235252268). Decision 7235680921 adopts the six answers that the decision request lists, each as it recommends, and the narrowing table of the proposal (point 2). It does not adopt the rest of the proposal.

There is no parent plan: #597 holds a proposal. No item state file is written (factory.md "Decision log", last paragraph).

Downstream prerequisites (factory.md "Outcome responsibility"). None blocks this pull request. Each is recorded in the amendment's consequences and belongs to a later split entry, approved by #597 decision 7236013792: whether SIGTERM reaches the worker under `shareProcessNamespace: true` (settled by a live smoke before the C18 plan is approved, proposal "Prerequisites" table, under decision 7235680921 point 3); whether Event Hubs honours `group.instance.id`, and the stop-to-join time that sizes C06's `session.timeout.ms` (C06's first smoke, decision 7235680921 point 6). Whether the workflow can read the deleted subject pod's container end time before the pod is removed (the C18 smoke, before the C18 plan is approved, decision 7236060793 point 3). This pull request cannot settle any of them: it changes prose and runs nothing.

## 2. Scope

Files this pull request may change: `docs/blueprint.md`, only at the places section 3 pins: after line 14, line 605, line 607, line 619, line 641, line 750, after line 796, lines 1272, 1273 and 1278, after line 1423.

Must not change: any other line of `docs/blueprint.md`; `AGENTS.md`, `CLAUDE.md`, `docs/agents/`, `docs/engineering/`, `docs/operations/`, `docs/research/`; `README.md`; `src/`, `tests/`, `scenarios/`, `results/`, `infra/`, `charts/`, `scripts/`, `.github/`; `items/` (no item state file; factory.md "Decision log").

Protected: `/docs/` is in `.github/CODEOWNERS` line 5 on the base, so Hari merges (AGENTS.md GOVERNANCE "Merge rule").

## 3. External contracts

The published text is pinned literally. Every block below is one line unless it shows more. "Replace" means replace the exact anchor text, which occurs once in the file, with the new text, leaving the rest of the line unchanged.

### 3.1 Approval record (insert after line 14)

Insert after line 14 one empty line, then this line:

```
ADR-022's amendment 1 for C18, C06 and C04, the ADR-019 and ADR-020 references to those items, the narrowed C04, C06 and C18 rows in section 12's "Folded into existing items", and sources S52 to S55 follow the #597 decisions [7235176688](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7235176688), [7235586661](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7235586661) and [7235680921](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7235680921), adopted on #605 by [decision 7235685248](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/605#issuecomment-7235685248), and the #605 decisions that approve the adr022-amendment plan. Hari's merge of this pull request records approval of them.
```

### 3.2 ADR-019 closed list (line 605)

Replace:

```
For C03, C10 and C01, that source category also includes ADR-022's invalid-run conditions;
```

with:

```
For C03, C10, C01, C18, C06 and C04, that source category also includes ADR-022's invalid-run conditions;
```

### 3.3 ADR-019 check 1 (line 607)

Replace:

```
For C03, C10 and C01, it also covers the consumer members' effective settings (ADR-022).
```

with:

```
For C03, C10, C01, C18, C06 and C04, it also covers the consumer members' effective settings (ADR-022).
```

### 3.4 ADR-019 consequence (line 619)

Replace:

```
and for C03, C10 and C01 also the consumer members' settings (ADR-022).
```

with:

```
and for C03, C10, C01, C18, C06 and C04 also the consumer members' settings (ADR-022).
```

### 3.5 ADR-020 exception (line 641)

Replace:

```
Under ADR-022, the subject consumer of a C03, C10 or C01 repeat whose configuration names a kill count may also send SIGKILL only to its own process, once per repeat, and starts no process.
```

with:

```
Under ADR-022, the subject consumer of a C03, C10, C01 or C04 repeat whose configuration names a kill count may also send SIGKILL only to its own process, once per repeat, or in C04 once per subject instance under ADR-022's amendment 1, and starts no process.
```

### 3.6 ADR-022 decision line (line 750)

Replace, at the end of line 750:

```
[6895700580](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895700580) on #504.
```

with:

```
[6895700580](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/504#issuecomment-6895700580) on #504; and for repeats of C18, C06 and C04 as "Amendment 1: C18, C06 and C04" below sets.
```

The opening `**Decision.** Option 5, for repeats of C03, C10 and C01 only, under the` stays unchanged.

### 3.7 ADR-022 amendment 1 (insert after line 796)

Line 796 is the last ADR-022 consequence, `- The worker, verifier, chart, workflow and identity changes land in the #499 children. ...`. Insert after it one empty line, then this block, so that the existing empty line 797 and the `---` of line 798 follow it unchanged:

```markdown
**Amendment 1: C18, C06 and C04.** Under [#597 decision 7235680921](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7235680921), adopted on #605 by [decision 7235685248](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/605#issuecomment-7235685248), and [#605 decision 7236060793](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/605#issuecomment-7236060793). For repeats of C18, C06 and C04 the decision above applies with the changes below; where they differ, these bullets win for those items. C03, C10 and C01 are unchanged.

*Context.* C18 asks whether a consumer whose pod Kubernetes deletes closes, commits and leaves the group within its termination grace, with `Close` against `Dispose` only. C06 asks whether a successor with the same `group.instance.id` rejoins within `session.timeout.ms` without a rebalance, with dynamic against static membership. C04 asks whether each restart of a crashing subject causes a rebalance, and whether a group rebalances constantly with no error (S2). The decision above rejects two of their endings: a stopped subject ends without its completion receipt, and a successor makes more than two registered members. It also records no row for a non-fatal consume error, so "no error" has no evidence.

On pod deletion the kubelet typically has the container runtime send TERM, or the image's STOPSIGNAL where the runtime respects it, to the main process in each container, and KILL to any remaining processes once the grace period expires; the default grace is 30 seconds (Fact: S54). On .NET 6 and later, `ConsoleLifetime` handles SIGTERM and raises `ApplicationStopping` (Fact: S55). `Close` commits offsets if auto commit is enabled and alerts the group coordinator that the consumer is exiting; without `Close` or `Unsubscribe` the group rebalances after `session.timeout.ms` (Fact: S52). `Dispose` without `Close` does not commit offsets or alert the group coordinator (Fact: S52). Without a close, final offsets are not committed and the consumer does not actively leave the group (Fact: S53). Static members can leave and rejoin within `session.timeout.ms` without a rebalance, and send no leave group request on close (Fact: S6, S53). Microsoft suggests considering a unique `group.instance.id` per consumer instance to reduce rebalances from transient disconnections, and gives `session.timeout.ms` a constraint of 6000 to 300000 (Fact: S1). Consumers that constantly rebalance get "no exception or error" (Fact: S2). No Microsoft source read states that Event Hubs honours `group.instance.id`.

*Options considered.*
1. The subject sends SIGTERM to itself at a fixed message. It skips the kubelet and the grace period, and needs a third ADR-020 exception.
2. Kubernetes restarts the crashed subject with `restartPolicy: OnFailure`. It tests restart back-off, and needs an exception to ADR-017.
3. The workflow deletes the subject's pod at a fixed time after the start line, and starts later subject instances as new Jobs in sequence. A stopped subject ends with a stop receipt, and every member records its consume errors.

*Decision.* Option 3.
- Stop. A configuration names a stop time or a kill count, not both. In a configuration whose scenario names a stop time T, the workflow deletes the first subject instance's pod T seconds after the start line, once per repeat, with the pod's termination grace from the chart. The subject treats `ApplicationStopping` as the stop request. It finishes the message in progress, consumes no further message and appends a stop-observed row with its instance ID, row sequence number and the time. It then ends its consumer by the configuration's shutdown mode: `close` calls `Close` and then `Dispose`; `dispose` calls `Dispose` only. With no shutdown mode named, the mode is `close`. It finishes recording callbacks from that call and writes a stop receipt as its last action. The stop receipt holds the stop-observed time, the shutdown mode, the start and end times of the shutdown call, its outcome, and the time the receipt is written.
- Shutdown margin. For each repeat with a stop time, two times are recorded against the pod's termination grace. The first runs from the subject's stop-observed row to the time its stop receipt is written, from the subject's own record; it exists whenever the stop receipt is written. The second runs from the stop-observed row to the end of the subject's process, from the workflow's pod evidence (ADR-020), and includes host cleanup after the receipt. Both are recorded only: they decide no validity and no verdict.
- Missing stop receipt. A subject instance with a stop time configured that ends after its stop-observed row without its stop receipt makes the repeat incomplete with reason `consumer_stop_receipt_missing`, not loss. Its rows remain diagnostics.
- Subject instances. A configuration whose scenario names a number of subject instances S greater than 1 runs them in sequence. The workflow starts each later instance as a new Job only after it has confirmed that the previous instance's process has terminated (ADR-020). Each instance is a member with its own pod name, instance ID, registration marker and record, and runs the subject's configuration. Its window is measured from its own start. Only the first instance and the witness take part in "Start order".
- Members. In these repeats, "two consumer members" and "both members' node names" in "Group", "both consumer processes" in "Stop and observation boundary" and "either member" in "Commit agreement" mean every registered member: the witness and each subject instance.
- Static membership. In a configuration whose scenario names `group.instance.id` values, every subject instance uses the subject's value and the witness uses its own, different value. In every other configuration no member sets `group.instance.id`.
- Crash in sequence. In C04 the configuration's kill count applies to each subject instance: each instance sends SIGKILL to itself when `Consume` returns its Nth message, as "Self-termination" above sets, once per subject instance.
- Consumer errors. Each member also writes a consumer-error row, with its instance ID and row sequence number, for every `ConsumeException` that `Consume` raises and every call of its error handler (S52). The row holds the error code, whether the error is fatal, the reason and the time. A non-fatal error does not end the member. A member record with no consumer-error row is the evidence of "no error".
- Rebalance count. The verifier counts each member's partition-event rows per repeat, by kind. Each item plan's decision rules say how a count is judged.
- Takeover timing. For C18 and C06, reassignment time runs from the subject's stop-observed row to the first assigned row, from the witness or a later subject instance, for the subject's partitions. When the members ran on different nodes, that repeat's timing is unavailable, as under "C01 timing".
- Configuration fidelity. Every subject instance's effective settings, including `group.instance.id` when the configuration names it, must match the selected configuration and the baseline. The witness's must match the baseline, and its own `group.instance.id` when the configuration names one. The registration marker also records the shutdown mode.
- Invalid run. These conditions replace the decision's member-count and end-condition bullets for these items; its other conditions stay:
  - other than one witness and S subject instances registered, with S equal to 1 when the scenario names none;
  - the workflow did not confirm that the previous subject instance's process had terminated before it started the next one;
  - with a kill count configured, a subject instance ended other than at its kill-intent row;
  - with a stop time configured, the first subject instance wrote a kill-intent row or a completion receipt, or ended without a stop-observed row; or a later subject instance ended without its completion receipt;
  - with neither configured, a subject instance wrote a kill-intent row or ended without its completion receipt;
  - the witness ended without its completion receipt;
  - a subject instance's registration marker records a shutdown mode other than the configuration's.

*Consequences.*
- C06's result covers one stop and one rejoin of Job members, not a rolling deploy. That a successor Job looks the same to the broker as a rolled pod is inference: both are a new client process that joins with the same `group.id` and, when static, the same `group.instance.id`.
- Whether SIGTERM reaches the worker under the chart's shared process namespace is not verified; the Kubernetes page names both "the main process" and "process 1" in each container (S54). A live smoke settles it before the C18 plan is approved.
- Whether Event Hubs honours `group.instance.id` is not verified. C06's first smoke settles it before the C06 plan is approved. If it does not, C06 publishes that as its finding.
- The time from a subject's stop to its successor's join is not measured. The C06 plan sizes `session.timeout.ms` from that smoke, inside 6000 to 300000 (S1).
- On the base the processing delay ends on `ApplicationStopping`; under "Stop" the subject instead finishes the message in progress, so a message in progress delays the close by up to the processing delay (inference). The shutdown margin shows how much of the grace remains. A grace shorter than the close is not tested as its own configuration.
- Whether the workflow can read the deleted subject pod's container end time before the pod is removed is not verified. The C18 smoke settles it before the C18 plan is approved.
- Sequential subject Jobs do not exercise the kubelet's restart back-off. C04 states it as not tested.
- The worker, verifier, chart, workflow and scenario changes land in the #597 children. This amendment records the design before them; it does not claim they work.
```

### 3.8 Section 12 C04 row (line 1272)

Replace the whole line:

```
| C04 | Crash-looping consumer: restart back-off and a rebalance per restart (K-09) |
```

with:

```
| C04 | Crash-looping consumer: restart back-off and a rebalance per restart (K-09). Narrowed for #597 ([decision 7235680921](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7235680921), points 2 and 4): a rebalance per restart is tested with sequential subject Jobs started by the workflow; the Kubernetes restart back-off itself is not tested |
```

### 3.9 Section 12 C06 row (line 1273)

Replace the whole line:

```
| C06 | Deployment versus StatefulSet for `group.instance.id` (K-23, K-25); duplicate ID fencing (K-24); rolling update surge (K-22); node drain and AKS upgrade with a PDB (K-29, K-30); static members do not send LeaveGroup (K-05) |
```

with:

```
| C06 | Deployment versus StatefulSet for `group.instance.id` (K-23, K-25); duplicate ID fencing (K-24); rolling update surge (K-22); node drain and AKS upgrade with a PDB (K-29, K-30); static members do not send LeaveGroup (K-05). Narrowed for #597 ([decision 7235680921](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7235680921), point 2): dynamic versus static membership across one stop and one rejoin of Job members within `session.timeout.ms` (K-25), and no LeaveGroup from a static member (K-05), are tested; the result covers one stop and one rejoin, not a rolling deploy. Deployment versus StatefulSet (K-23), rolling update surge (K-22), node drain and AKS upgrade with a PDB (K-29, K-30) and duplicate ID fencing (K-24) are not tested |
```

### 3.10 Section 12 C18 row (line 1278)

Replace the whole line:

```
| C18 | Grace period shorter than flush plus close (K-03, K-04); shell-form `ENTRYPOINT` image (K-02); `Close` versus `Dispose` (K-05); commit on revoke versus lost (K-28); shutdown commit with auto-commit on and off (K-56) |
```

with:

```
| C18 | Grace period shorter than flush plus close (K-03, K-04); shell-form `ENTRYPOINT` image (K-02); `Close` versus `Dispose` (K-05); commit on revoke versus lost (K-28); shutdown commit with auto-commit on and off (K-56). Narrowed for #597 ([decision 7235680921](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/597#issuecomment-7235680921), points 2, 3 and 5): with the subject's pod deleted by the workflow, `Close` versus `Dispose` (K-05) and commit on revoke versus lost (K-28) are tested, and each repeat records the time from the observed SIGTERM to process exit against the 30 s grace (K-03, recorded only). A grace period shorter than the close as its own configuration (K-03), producer flush on shutdown (K-04), the shell-form `ENTRYPOINT` image (K-02) and shutdown commit with auto-commit off (K-56) are not tested |
```

### 3.11 Sources S52 to S55 (insert after line 1423)

Line 1423 is the S51 row, the last line of the file. Insert after it these four lines, and end the file with one newline:

```
| S52 | Confluent.Kafka v2.16.0: [IConsumer.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/IConsumer.cs#L669-L689), `Close` XML documentation; [ConsumerBuilder.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ConsumerBuilder.cs#L195-L208), `SetErrorHandler` XML documentation; [Consumer.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Consumer.cs#L615-L618), `Dispose` XML documentation; [SafeKafkaHandle.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Impl/SafeKafkaHandle.cs#L189-L191), consumer handles destroyed with `RD_KAFKA_DESTROY_F_NO_CONSUMER_CLOSE` | tag v2.16.0, read 2026-11-28 |
| S53 | librdkafka v2.16.0 [INTRODUCTION.md](https://github.com/confluentinc/librdkafka/blob/v2.16.0/INTRODUCTION.md), sections "Termination" (subsection "High-level KafkaConsumer", effects of not closing) and "Static consumer groups" | tag v2.16.0, read 2026-11-28 |
| S54 | Kubernetes documentation: [Termination of Pods](https://kubernetes.io/docs/concepts/workloads/pods/pod-termination/), kubernetes/website at commit 38552b318453cbf9e282f36262a6f163bb7a8017, `content/en/docs/concepts/workloads/pods/pod-termination.md` lines 22 to 30, 84 and 94 | read 2026-11-28 |
| S55 | [.NET Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host#host-shutdown), "Host shutdown" | read 2026-11-28 |
```

## 4. Requirements

1. WHEN the pull request merges THEN docs/blueprint.md contains the 3.7 block exactly once, after ADR-022's last consequence and before the `---` that precedes section 5.
2. WHEN a later C18, C06 or C04 child cites the stop, the shutdown margin, the stop receipt, subject instances, static membership, consumer-error rows, the rebalance count, takeover timing, fidelity or invalid-run conditions THEN amendment 1's text (3.7) is the source.
3. WHEN ADR-022's decision line is read THEN it keeps its C03, C10 and C01 authority unchanged and, after it, names C18, C06 and C04 and points to amendment 1 (3.6).
4. WHEN ADR-019's closed list, check 1 or its fidelity consequence is read for C18, C06 or C04 THEN each covers them as it covers C03, C10 and C01 (3.2 to 3.4).
5. WHEN ADR-020's process rules are read THEN the ADR-022 self-kill covers C04 once per subject instance, and no new signal exception exists (3.5).
6. WHEN section 12's fold table is read THEN the C04, C06 and C18 rows keep their original text and add which folds are tested and which are not, as decision 7235680921 lists them (3.8 to 3.10).
7. WHEN the status header is read THEN it records what this pull request adds and that Hari's merge approves it (3.1).
8. WHEN amendment 1 cites S52 to S55 THEN each is a row in the sources table (3.11).
9. WHEN any line outside section 2's list differs from the base THEN the pull request is out of scope and check A8 fails.

## 5. Invariants and safety rules

- ADR-022's existing decision bullets, classes, commit agreement and completion rules are unchanged for C03, C10 and C01. ADR-012, ADR-013, ADR-017 and ADR-021 text is unchanged.
- No verdict, expected outcome or `results/` file changes (AGENTS.md "Verdict integrity").
- No new inventory item (AGENTS.md GOVERNANCE "Scope"). The three fold rows keep their original text and gain a narrowing note, as the A02 and C03 rows did.
- No configuration, workflow change, dispatch or expected outcome is approved by this text (decision 7235586661, "What this does not decide"; decision 7235680921, "Not decided here").
- ASCII punctuation only, no em or en dashes (AGENTS.md "Writing standards").

## 6. Test map

The change is prose; there is no code. Each requirement maps to a scripted text check on the pull request head (section 8). This is not verifier work, so docs/agents/pull-requests.md's predicate coverage does not apply.

| Requirement | Check | Input | Literal expected value |
|---|---|---|---|
| 1, 2 | A1 | `docs/blueprint.md` | the 3.7 block occurs exactly once; its first line comes after the line starting `- The worker, verifier, chart, workflow and identity changes land in the #499 children.` and before the line `## 5. Failure modes` |
| 3 | A2 | `docs/blueprint.md` | the 3.6 new text occurs once, at the end of the line that starts `**Decision.** Option 5, for repeats of C03, C10 and C01 only, under the`; the 3.6 old text occurs zero times |
| 4 | A3 | `docs/blueprint.md` | the 3.2, 3.3 and 3.4 new texts each occur once; their old texts occur zero times |
| 5 | A4 | `docs/blueprint.md` | the 3.5 new sentence occurs once, on the line that contains `The sole production process-launch exception is ADR-021's B10 parent process.`; the old sentence occurs zero times |
| 6 | A5 | `docs/blueprint.md` | the new rows of 3.8, 3.9 and 3.10 each occur once; the old rows occur zero times |
| 7 | A6 | `docs/blueprint.md` | the 3.1 line occurs once, and is line 16 of the file |
| 8 | A7 | `docs/blueprint.md` | the four 3.11 rows occur once each, in order, as the last four lines; the file ends with one newline |
| 9 | A8 | the base file and the head file | applying 3.1 to 3.11 to the base text by script gives the head file byte for byte |
| 9 | A9 | `git diff --name-only origin/main...HEAD` | exactly `docs/blueprint.md` |
| style | A10 | added lines | no byte outside printable ASCII, tab and newline |

Existing tests: no test asserts on docs/blueprint.md's wording. `scripts/docs-rules.mjs` reads its ADR and source definitions; acceptance command 4 runs those checks. The plan adds no code check, so the plan-pr standing fixture rule has no case.

## 7. Deployment assumptions

None for this pull request: the change is prose, and no deployed component is involved. The amendment's own consequences name two deployment assumptions for later children (SIGTERM delivery under the shared process namespace; Event Hubs support for `group.instance.id`). Each is settled by a live smoke in its child before that child's plan is approved, as section 1 states. Neither is relied on by any check here.

## 8. Acceptance commands

Run on the pull request branch before the cold review.

1. A1 to A7 and A10: one script that reads the file, counts each literal of section 3 by exact string match and checks the stated positions, printing `PASS <id>` or `FAIL <id>: <reason>`. Evidence: eight `PASS` lines and exit status 0.
2. A8: a script that applies 3.1 to 3.11 to `git show origin/main:docs/blueprint.md` and compares with the head file. Evidence: `identical`.
3. A9: `git fetch origin main && git diff --name-only origin/main...HEAD`. Evidence: the printed list.
4. `npm run check:docs`. Evidence: the summary line; each warning in docs/blueprint.md fixed or explained under docs/agents/pull-requests.md rule 8.
5. Verification gates (docs/agents/pull-requests.md "Verification gates"): steps 1 and 3 do not apply, because no file under `src/`, `tests/` or the other paths of its last paragraph changes. Step 2, the cold review under rule 8, applies. Step 4, the hosted `.NET verification` check on the final head, runs as the required check.

## 9. Sources

External behaviour (plan-pr "Plan rules" rule 1). Tools: raw GitHub fetches at the pinned tag or commit for 1 to 4, and the Microsoft Learn documentation search tool for 5 to 7, all on 2026-11-28.

1. Confluent.Kafka v2.16.0 [IConsumer.cs lines 669 to 689](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/IConsumer.cs#L669-L689): `Close` "Commits offsets (if auto commit is enabled), alerts the group coordinator that the consumer is exiting the group"; "If you do not call `Close` or `Unsubscribe`, the group will rebalance after a timeout specified by the group's `session.timeout.ms`". [ConsumerBuilder.cs lines 195 to 208](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ConsumerBuilder.cs#L195-L208): `SetErrorHandler` is called "on error events", "Non-fatal errors should be interpreted as informational", and "Executes as a side-effect of the Consume method". [Consumer.cs lines 615 to 618](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Consumer.cs#L615-L618): `Dispose` "Releases all resources used by this Consumer without committing offsets and without alerting the group coordinator that the consumer is exiting the group". [SafeKafkaHandle.cs lines 189 to 191](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Impl/SafeKafkaHandle.cs#L189-L191): consumer handles are destroyed with `RD_KAFKA_DESTROY_F_NO_CONSUMER_CLOSE`. Both read at tag v2.16.0 on 2026-11-28 in this session. Becomes S52.
2. librdkafka v2.16.0 [INTRODUCTION.md](https://github.com/confluentinc/librdkafka/blob/v2.16.0/INTRODUCTION.md), "Termination", "High-level KafkaConsumer" (lines 992 to 994): without close, "Final offsets are not committed and the consumer will not actively leave the group, it will be kicked out of the group after the `session.timeout.ms` expires". "Static consumer groups" (lines 1676 to 1696): static members "can leave and rejoin a group within the `session.timeout.ms` without triggering a rebalance"; "Consumers with `group.instance.id` set will not send a leave group request on close". Becomes S53.
3. librdkafka v2.16.0 [CONFIGURATION.md line 129](https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md?plain=1#L129) (blueprint S6): `group.instance.id`, "Static group members are able to leave and rejoin a group within the configured `session.timeout.ms` without prompting a group rebalance". Already S6.
4. Kubernetes [Termination of Pods](https://kubernetes.io/docs/concepts/workloads/pods/pod-termination/), kubernetes/website at 38552b318453cbf9e282f36262a6f163bb7a8017, `pod-termination.md`: lines 22 to 24 "first sending a TERM (aka. SIGTERM) signal, with a grace period timeout, to the main process in each container"; lines 29 to 30 "Once the grace period has expired, the KILL signal is sent to any remaining processes"; line 84 "The default `terminationGracePeriodSeconds` setting is 30 seconds"; line 94 "send a TERM signal to process 1 inside each container". Becomes S54.
5. [.NET Generic Host, "Host shutdown"](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host#host-shutdown): "In .NET 6, POSIX signals are supported and handled. The `ConsoleLifetime` handles SIGTERM gracefully"; "The control flows from `ConsoleLifetime` to `ApplicationLifetime` to raise the `ApplicationStopping` event". Becomes S55.
6. [Apache Kafka client configurations for Azure Event Hubs](https://learn.microsoft.com/azure/event-hubs/apache-kafka-configurations) (blueprint S1): librdkafka consumer table, `session.timeout.ms` constraint 6000 to 300000 (the page writes the range with an en dash; the ADR writes "6000 to 300000"); "Troubleshooting common issues", "Consider setting unique `group.instance.id` per consumer instance to reduce rebalances from transient disconnections". Already S1.
7. [Apache Kafka troubleshooting guide for Event Hubs](https://learn.microsoft.com/azure/event-hubs/apache-kafka-troubleshooting-guide), "No records received" (blueprint S2): consumers "constantly rebalance. There's no exception or error when it happens". Already S2.

Repository, at bc377bd7591183045678dc928df4bf990e52407d (the same text as at 15b09147ed4e7c62ea08620b24c14cd049f07562):

8. `docs/blueprint.md`: line 14 (ADR-022 approval record); ADR-019 lines 605, 607, 619; ADR-020 line 641; ADR-022 lines 737 to 796; section 12 fold rows 1272, 1273, 1278; sources 1365 to 1423.
9. `src/Parchwell.Enrichment/ConsumerProcessing/ConsumerProcessingWorker.cs` lines 62 to 69 (cancellation checked before each `Poll`), 72 to 77 (any other exception becomes `consumer_fatal_error`), 156 to 160 (non-fatal `ConsumeException` returns null with no row), 191 (the pause loop checks the token) and 198 (the processing delay ends on the token). `src/Parchwell.Enrichment/Program.cs` lines 202 and 203 (the token is `ApplicationStopping`) and 216 and 217 (10 s host cleanup). The basis of the amendment's context and its sixth consequence.
10. `charts/parchwell/templates/_consumer.tpl` lines 46 and 47: `shareProcessNamespace: true` and the termination grace from `bounds.execution`.
11. `.github/CODEOWNERS` line 5.

## 10. Plan-check questions (factory.md "Item lifecycle" step 1)

(a) Steps whose effort scales with a number unknown at planning time: none. The pull request changes eleven places in one file.

(b) Where the plan is stricter than, or goes beyond, its authority. Since revision 3, decision 7236060793 adopts every point below as recommended (point 1), with the changes of its points 2 to 5. Before it, decision 7235680921 said "Not decided here: any ADR or blueprint text", and the issue body says the plan decides no protocol sequence, failure name, invalid-run condition or verdict rule. So every point below is a proposal, carried in the literal text as the recommended answer and asked in section 11:
- The placement as an "Amendment 1" block inside ADR-022 rather than edits to its bullets (question 1).
- The stop protocol: stop-observed row, the two shutdown modes and their default, the receipt's content, and "a stop time or a kill count, not both" (question 2).
- The incomplete reason `consumer_stop_receipt_missing` (question 3).
- The source of the process-exit time for decision point 5's condition (question 4).
- The subject-instance sequence and the overlap invalid-run condition (question 5).
- Which members carry `group.instance.id` in C06's static configuration (question 6).
- Self-kill per subject instance in C04, and the ADR-020 wording that extends to it (question 7). The proposal's C04 row reuses ADR-022's self-kill; decision 7235680921 point 4 decides only that restarts are sequential subject Jobs.
- Consumer-error rows: their content, that non-fatal errors do not end a member, and their scope of C18, C06 and C04 only (question 8).
- Takeover timing for C18 and C06 from the stop-observed row (question 9).
- The fold rows state what is tested and not tested without reasons; the reasons are in the proposal's narrowing table and go into each item's write-up (question 10).
- The rebalance count as a verifier count in the ADR (question 11).
- A shutdown-mode mismatch as an invalid-run condition (question 12).
- The end conditions for a configured stop time and for a configuration with neither stop nor kill (question 13).
- The "Members" bullet that extends three two-member bullets to every registered member (question 14).
- The stop receipt's write time and the margin from the subject's own record: the literal text is this plan's, for decision 7236060793 point 3's addition, which asks the planning session to pin it and the fresh check to cover it.

(c) Tests or checks that depend on code, data or behaviour neither on `main` nor in this pull request: none. Every check in section 6 reads only the base and head text of docs/blueprint.md. The amendment describes behaviour that later children implement and test.

## 11. Open questions for Hari

None. The fourteen questions of revision 2 are answered by [decision 7236060793](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/605#issuecomment-7236060793) point 1, each as recommended, and the revision 2 check's findings 2 to 4 by its points 2 to 4.

## 11a. Answered questions (record)

The questions as revision 2 asked them. Each recommended answer was adopted by decision 7236060793 point 1. Question 2's literal text is as revision 2 pinned it, with the receipt's write time added under point 3; question 5's overlap condition is replaced under point 4.

Each question names the recommended answer, which the literal text in section 3 already carries, and the alternative. An answer other than the recommendation changes the named literal, and the revision gets a fresh check.

1. Placement. Recommended: one "Amendment 1: C18, C06 and C04" block after ADR-022's consequences, whose bullets win for those items (3.7), and a pointer in the decision line (3.6). This leaves the published C03, C10 and C01 text untouched. Alternative: edit ADR-022's bullets in place, so a reader sees one list, at the cost of rewording text the three published results cite.
2. Stop protocol. Recommended: the subject treats `ApplicationStopping` as the stop request, finishes the message in progress, appends a stop-observed row, ends by the shutdown mode (`close`: `Close` then `Dispose`; `dispose`: `Dispose` only; default `close`), records callbacks from that call, and writes a stop receipt holding the stop-observed time, the mode, the call's start and end times and its outcome. A configuration names a stop time or a kill count, not both. Alternative: drop the stop-observed row and take the stop time from the receipt only, which loses the stop time when the receipt is never written.
3. Failure name and class. Recommended: a stopped subject that ends after its stop-observed row without a stop receipt makes the repeat incomplete with reason `consumer_stop_receipt_missing`, not invalid and not loss (the proposal's "If the receipt cannot be written before SIGKILL, the repeat is incomplete, not loss"). Alternative: invalid, which would hide a close slower than the grace inside a harness fault.
4. Shutdown margin source. Decision 7235680921 point 5 requires the time from the observed SIGTERM to process exit. A process cannot record its own exit. Recommended: the end comes from the workflow's pod evidence (ADR-020 keeps pod JSON for people) and is recorded only. Alternative: use the stop receipt's write time as the end, which the verifier can read, but which misses host cleanup after the receipt (bounded at 10 s by `src/Parchwell.Enrichment/Program.cs` lines 216 and 217 on the base).
5. Subject instances. Recommended: the workflow starts each later instance only after confirming the previous instance's process terminated; each instance registers as its own member; a registration marker earlier than the previous instance's last row invalidates the run; the member count becomes one witness plus S subject instances. Alternative: let instances overlap, which C06 must not do, because a second member with the same `group.instance.id` fences the first (S53).
6. Static IDs in C06. Recommended: in the static configuration, every subject instance uses the subject's `group.instance.id` and the witness uses its own different value, so the whole group is static as Microsoft's advice describes (S1, "unique `group.instance.id` per consumer instance"). Alternative: only the subject is static, which tests a mixed group.
7. C04 crash. Recommended: each subject instance sends SIGKILL to itself at its Nth message, under ADR-022's existing self-termination rule, and ADR-020's exception reads "once per subject instance" for C03, C10, C01 and C04 (3.5). Alternative: the workflow deletes each instance's pod, which needs no ADR-020 change but gives a SIGTERM and grace, not a crash.
8. Consumer-error rows. Recommended: every member of C18, C06 and C04 writes a row for each `ConsumeException` from `Consume` and each error-handler call, with code, fatal flag, reason and time; a non-fatal error does not end the member; no row means "no error". C03, C10 and C01 are unchanged. Alternative: apply the rows to all six items, which changes the text the published results rest on.
9. Takeover timing. Recommended: for C18 and C06, from the subject's stop-observed row to the first assigned row of the witness or a later subject instance for the subject's partitions, unavailable when the members ran on different nodes. Alternative: no takeover timing in the ADR, left to each item plan.
10. Fold rows. Recommended: the C04, C06 and C18 rows keep their text and add what is tested and what is not, citing decision 7235680921, without the reasons (3.8 to 3.10). Alternative: add each reason from the proposal's narrowing table to the rows, making them longer.
11. Rebalance count. Recommended: the ADR states that the verifier counts each member's partition-event rows per repeat, by kind, and leaves its judgement to each item plan. The proposal puts the count in C04's entry. Alternative: leave the count out of the ADR and let the C04 plan add it under ADR-019's closed list.
12. Shutdown mode mismatch. Recommended: a subject instance whose registration marker records a shutdown mode other than the configuration's invalidates the run, as a settings mismatch does. Alternative: no such condition; the mode is then unchecked evidence.
13. End conditions. Recommended: with a stop time, the first subject instance must end at its stop path (no kill-intent row, no completion receipt, a stop-observed row present), and a later instance must end with its completion receipt; with neither a stop time nor a kill count, a subject instance must write no kill-intent row and must end with its completion receipt, as the base text says for C03, C10 and C01. Alternative: accept a completion receipt from a stopped subject whose pod deletion came after its window ended, which would let a repeat pass without the stop it tests.
14. Members. Recommended: a "Members" bullet makes "Group", "Stop and observation boundary" and "Commit agreement" read every registered member, so the workflow waits for every subject instance before the offset read, and R[p] takes commit entries from every instance. Alternative: amend each of the three bullets in place, which edits text the published C03, C10 and C01 results rest on.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it would need to change any literal in section 3, any requirement, the file scope, or when a check in section 6 cannot pass on the pinned text.

## 13. Size estimate

Estimate, 2026-11-28, from applying the section 3 literals of this revision to the base by script and counting the diff: 56 added lines and 8 removed lines in `docs/blueprint.md` (8 of the added lines replace the 8 removed ones). One file. Size is reported, not enforced.

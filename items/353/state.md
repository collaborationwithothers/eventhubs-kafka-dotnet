# State: #353 B10 retry execution in the load generator (#348 entry 3)
Covers: comment 6626569222 created 2026-11-04T08:44:23Z; read at 2026-11-04T08:58:11+00:00
Parent items: [#348](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348), [#347](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347)
Item plan: the split in [handoff 6617284257](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617284257), adopted by [split adoption 6617607468](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617607468) and [decision 6617623208](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/353#issuecomment-6617623208); entry 3 is this child.
Implementing tool: Claude Code. Source: no comment on the issue or its listed parents names a tool before `ready-for-agent` (applied to #347 and #348 on 2026-11-03); Claude Code by default.
Approved plan: [B10 retry execution plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/353#issuecomment-6625703189), updated 2026-11-04T08:19:21Z; approved under standing approval by the [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/353#issuecomment-6626569222) (`Blocking findings: none`), whose one P2 marked `apply in implementation` is applied.
Decisions in force:
- [Issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/353): child of #348 and #347, split entry 3, predecessor the pull request of #351.
- Decision 6617623208 adopts the #347 standing authority points 1 to 6, the split adoption and the restart decision. Source: [adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/353#issuecomment-6617623208).
- Decision 6625942726 adopts one application retry per scheduled event, fixed in code, and selection by scenario ID B10 in code. Source: [recommendations adopted](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/353#issuecomment-6625942726).
Open questions:
- None
Next action: Cold review, publish the pull request, satisfy final-head CI and hosted review, then merge under the merge rule. Later entries: the verifier rules (entry 2), the producer restart (entry 3b), and the B10 scenario and host limits (entry 4), which must also size the ledger, read-back and manifest byte limits for retries ([plan check note](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/353#issuecomment-6626569222)).

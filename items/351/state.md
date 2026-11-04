# State: #351 B10 oracle amendment: late-write guard and producer restart (#348 entry 1)
Covers: comment 6624892579 created 2026-11-04T07:08:49Z; read at 2026-11-04T07:13:14Z
Parent items: [#348](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348), [#347](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347)
Item plan: [split adoption on #348](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617607468), with the [split handoff 6617284257](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617284257) it adopts (split entry 1)
Implementing tool: Claude Code. Source: no comment on the issue or its listed parents before `ready-for-agent` (applied to #347 and #348 on 2026-11-03); Claude Code by default
Approved plan: [Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6617796257), updated_at 2026-11-04T06:30:53Z, approved by Hari's [approval 6624892579](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6624892579), which also accepts both `apply in implementation` P2s of [plan check 6624472543](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6624472543)
Decisions in force:
- Issue body: amend ADR-013's late-write guard for B10 and ADR-012 and ADR-020's single-instance rule for a restarted producer; protected path; exact text to Hari before approval. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351)
- Standing authority: points 1 to 6 on #347 and its children. Source: [decision 6617257796](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6617257796)
- Test producer restart under B10 (K-53). Source: [decision 6617344472](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617344472)
- B10 split adopted. Source: [decision 6617607468](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617607468)
- This child adopts the standing authority and the split in full. Source: [decision 6617622676](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6617622676)
- Two sequential load generator Jobs per B10 repeat; late copies classified up to a phase sweep line. Source: [decision 6623554949](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6623554949)
- A late record of an earlier repeat inside a later repeat's window is excluded from the later repeat. Source: [decision 6623766068](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6623766068)
- Hari approves Revision 3 including the exact ADR-021 text and both P2 fixes. Source: [approval 6624892579](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6624892579)
Open questions:
- None
Next action: #348 entry 2, after this pull request merges.

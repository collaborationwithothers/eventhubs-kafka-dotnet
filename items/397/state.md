# State: #397 C5 mutation run time reduction and #355 follow-ups

Covers: comment 6683257235 created 2026-11-06T10:59:06Z; read at 2026-11-06T11:00:59Z
Parent items: [producer investigation](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347)
Item plan: [child scope](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397); [parent proposal](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347)
Implementing tool: Codex. Source: [tool assignment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6683024818)
Approved plan: [runtime investigation Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6683195297), approved through the [standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6683257235)
Decisions in force:
- Investigate runtime and coverage history first, propose options, and complete state/runbook/memory follow-ups. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397)
- Semantics-preserving fixes may proceed; selection, thresholds, gates and assertions need Hari's Approval after measurements. Existing run cancelled; control session owns a later main-only run. Source: [runtime decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6683039430)
Open questions:
- Which optimization follows the measured investigation? Selection changes remain reserved for Hari. Source: [runtime decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6683039430)
Next action: Publish the investigation and authorized documentation follow-ups after required review. Runtime reduction is not implemented or measured. Resolve PR 396, then plan a semantics-preserving optimization or obtain Hari's Approval for changed test selection. Control session owns any later main-only C5 dispatch; this session dispatches none.

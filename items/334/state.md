# State: #334 What closes idle connections to Event Hubs, and why the client sometimes does not see it
Covers: comment 6600267391 created 2026-11-03T09:42:24Z; read at 2026-11-03T09:43:12Z
Parent items: none
Item plan: none; the first-PR scope is [decision 6596595315 point 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6596595315).
Implementing tool: Codex. Source: [decision point 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6596595315).
Approved plan: [Registration plan, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6600210556), updated 2026-11-03T09:39:25Z; approved under standing approval by the [independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6600267391).
Decisions in force:
- [Issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334): asks what closes idle Kafka connections and why the client sometimes does not see it; originally unscheduled and outside the inventory.
- Decision 6596595315 adds the investigation to v1, assigns Codex, defines standing approval and its exceptions, limits evidence and Azure execution, retains protected-path merges, and excludes shortened node keepalive timers and changes to existing idle-result judgments. Source: [standing-approval decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6596595315).
Open questions:
- None for this registration PR; capture design and investigation planning remain later work.
Next action: Hari merges the protected-path registration PR after its required reviews; then plan the investigation under the issue's standing-approval decision.

# State: #365 B10 late-write scan (#348 entry 2b)
Covers: comment 6652678919 created 2026-11-05T07:49:13Z; read at 2026-11-05T07:53:29Z
Parent items: [#348](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348), [#347](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347)
Item plan: [adopted verifier split](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/352#issuecomment-6625943061)
Implementing tool: Codex. Source: [tool assignment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/365#issuecomment-6633640498)
Approved plan: [B10 phase late-write finalization, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/365#issuecomment-6652574041), approved through [standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/365#issuecomment-6652678919)
Decisions in force:
- Implement the phase-level late-write scan and final B10 counts, define the sweep note contract, and include new scan/judgement files in C5. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/365)
- Adopt standing authority, the B10 split, restart and late-copy decisions, cross-repeat exclusion and verifier re-split, with their exceptions reserved for Hari. Source: [authority adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/365#issuecomment-6625948951)
- Follow the intake and per-repeat judgement predecessors. Their implementation PRs have merged. Source: [predecessor update](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/365#issuecomment-6632607721)
- Codex implements; the control session obtains a separate Claude review before merge, and Hari merges workflow changes. Source: [review and merge adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/365#issuecomment-6633534814)
- Codex may plan and independently check under standing approval except for plans needing Hari's own approval. Source: [standing-approval amendment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/365#issuecomment-6633795011)
Open questions:
- None
Next action: Complete local acceptance and cold review, then obtain final-head CI and Codex review before marking ready. The control session arranges the separate Claude review; Hari merges the workflow change. Postmerge main-only C5 acceptance remains required before scientific evidence publication; the successor supplies the workflow sweep note and production settle interval.

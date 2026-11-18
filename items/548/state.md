# State: #548 Archive transfers objects one by one and overruns its deadline on a consumer phase
Covers: comment 6977149826 created 2026-11-18T11:23:53Z; read at 2026-11-18T21:46:50Z
Parent items: none
Item plan: none (contract-grade plan, revision 3: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548#issuecomment-6973353186)
Implementing tool: Claude Code. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548#issuecomment-6973159782
Approved plan: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548#issuecomment-6973353186 (revision 3, commit e005063389e526f6e19fb2fa0e0883d46a563db0)
Decisions in force:
- Issue body: measure the time a consumer phase needs. Its route (parallelize the archive, no larger deadline) is replaced by the decision at 6976180742. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548
- Claude Code implements; Codex reviews. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548#issuecomment-6973159782
- Server-side copy evaluation and its verification rules; later lapsed by the decision at 6976180742. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548#issuecomment-6973214094
- Reword of the previous point 2; lapsed with it by point 2 of the decision at 6976180742. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548#issuecomment-6973250625
- Raise `ArchiveDeadlineSeconds` from 600 to 1800 in every place it is stated; the concurrency design is withdrawn. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548#issuecomment-6976180742
- Approval of plan revision 3 at the commit above; D1 accepted by name; the plan check's D1 text fix adopted as `apply in implementation`. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/548#issuecomment-6977149826
Open questions:
- None
Next action: Cold review, push, open the pull request, then Codex review; the next C03 smoke measures the archive time (plan section 11).

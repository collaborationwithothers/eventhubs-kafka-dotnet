# State: #329 Rerun A02 and A04 with metadata traffic kept out of the idle window (#301 child 9)
Covers: comment 6588805136 created 2026-11-02T21:23:27Z; read at 2026-11-02T21:32:31Z
Parent items: [#301](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301)
Item plan: [rerun decision 6588207250](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6588207250), which adds split entry 9, as the plan's "Parent plan" line states
Implementing tool: Claude Code. Source: no comment on the issue or its listed parents before `ready-for-agent`; Claude Code by default
Approved plan: [Metadata timers out of the A02 and A04 idle window implementation-grade plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/329#issuecomment-6588394554), updated_at 2026-11-02T21:18:23Z, approved under standing approval by [plan check 6588805136](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/329#issuecomment-6588805136)
Decisions in force:
- Issue body: split entry 9 of #301; changes the A02 and A04 configurations so no Metadata request falls in the 420 s idle period, with their expected outcomes; publishes no verdict. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/329)
- Adopts, in creation order, the #301 decisions 6562996229, 6563452228, 6563515099, 6563540913, 6569906599 and 6588207250; under the override decision the plan may be approved under "Standing approval" and the #301 control session may merge. Source: [decision 6588207947](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/329#issuecomment-6588207947)
- The rerun is one idle-observation batch with `items: all`, no workflow change; A03 publishes from that run. Source: [decision 6588671289](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/329#issuecomment-6588671289)
Open questions:
- None
Next action: the #301 control session merges the pull request under the #301 override decision; after merge it dispatches one idle-observation batch with `items: all`.

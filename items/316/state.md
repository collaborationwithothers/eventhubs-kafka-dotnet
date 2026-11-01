# State: #316 Idle observation: scenario declaration and settings resolution (#301 child 2)
Covers: comment 6564344142 created 2026-11-01T21:46:26Z; read at 2026-11-01T21:59:18Z
Parent items: [#301](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301)
Item plan: none
Implementing tool: Claude Code. Source: no comment on the issue or its listed parents before `ready-for-agent`; Claude Code by default
Approved plan: [Idle observation declaration plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/316#issuecomment-6563709522), updated_at 2026-11-01T21:38:39Z, approved by [approval 6564337374](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/316#issuecomment-6564337374)
Decisions in force:
- Issue body: split entry 2 of #301; add the `idle_observation` block, its reader and closed checks, the override entries for the A02, A03 and A04 deltas and `scenarios/A02`, `A03`, `A04` `scenario.yaml`; produce only; no LoadGen, verifier, workflow or expected.yaml change. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/316)
- Adopts, in creation order, #301 decisions 6562996229, 6563452228, 6563483800, 6563495437, 6563515099 and 6563540913. Source: [decision 6563545330](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/316#issuecomment-6563545330)
- Open question 1, option 1: the baseline sets `topic.metadata.refresh.interval.ms` at `300000`, the golden fixture is regenerated, one protected test literal changes, and the override decision's point 1 covers #316. Source: [decision 6564185671](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/316#issuecomment-6564185671)
- Open question 2: child 3 (#317) adds the idle period, statistics interval and report cap, fields and values; this child declares none. Source: [decision 6564220623](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/316#issuecomment-6564220623)
- Approves plan Revision 2 and applies two corrections, the line citations of change 7 in `ScenarioResolver.cs` and the count of changed `src/` files in acceptance 6. Source: [approval 6564337374](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/316#issuecomment-6564337374)
- The plan check states `Blocking findings: none` and marks five P2s `apply in implementation`. Source: [plan check 6564289566](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/316#issuecomment-6564289566)
Open questions:
- None
Next action: Hari or the #301 control session merges the pull request under the #301 override decision as extended to #316; after merge, post the completion comment on this issue.

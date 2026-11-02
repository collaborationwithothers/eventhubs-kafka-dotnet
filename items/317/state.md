# State: #317 Idle observation: LoadGen capture worker, chart and phase (#301 child 3)
Covers: comment 6570016904 created 2026-11-02T05:42:18Z; read at 2026-11-02T06:30:00Z
Parent items: [#301](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301)
Item plan: none
Implementing tool: Claude Code. Source: no comment on the issue or its listed parents before `ready-for-agent`; Claude Code by default
Approved plan: [Idle observation capture implementation-grade plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/317#issuecomment-6564976331), updated_at 2026-11-02T05:30:47Z, approved under standing approval by [plan check 6570016904](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/317#issuecomment-6570016904)
Decisions in force:
- Issue body: split entry 3 of #301; LoadGen idle-observation worker, wire and capture contract, chart template and bounds, phase selection; no content checks; no workflow change. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/317)
- Adopts, in creation order, #301 decisions 6562996229, 6563452228, 6563483800, 6563495437, 6563515099 and 6563540913. Source: [decision 6563545506](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/317#issuecomment-6563545506)
- Plan Revision 1 open questions: option A for both, one pod per configuration with five concurrent clients, and the `Parchwell.LoadGen.csproj` scenario-packaging change is covered by override point 1. Source: [decision 6569822377](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/317#issuecomment-6569822377)
- Adopts the #301 recommendations decision on how open questions are answered. Source: [decision 6569916242](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/317#issuecomment-6569916242)
Open questions:
- None
Next action: Hari or the #301 control session merges the pull request under the #301 override decision as extended to #317; after merge, post the completion comment on this issue.

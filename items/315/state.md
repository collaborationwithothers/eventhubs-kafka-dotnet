# State: #315 Blueprint: narrow the A02 row and record A03's third configuration (#301 child 1)
Covers: comment 6563740367 created 2026-11-01T20:53:44Z; read at 2026-11-01T20:55:30Z
Parent items: [#301](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301)
Item plan: none
Implementing tool: Claude Code. Source: no comment on the issue or its listed parents before `ready-for-agent`; Claude Code by default
Approved plan: [Idle-connection blueprint rows plan, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/315#issuecomment-6563631886), updated_at 2026-11-01T20:44:15Z, approved under standing approval by [plan check 6563740367](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/315#issuecomment-6563740367)
Decisions in force:
- Issue body: split entry 1 of #301; narrow the A02 row of docs/blueprint.md "Inventory amendment 1" (no pod sysctl configuration, K-37 not tested), add A03's third configuration `topic.metadata.refresh.interval.ms=180000` with `metadata.max.age.ms` kept at 180000, record the change on line 4, note K-35 and K-37 in docs/research/kubernetes-kafka-clients.md, and record A04's default omission of `connections.max.idle.ms` where the plan finds the blueprint must say so; documents only. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/315)
- Adopts, in creation order, #301 decisions 6562996229, 6563452228, 6563483800, 6563495437, 6563515099 and 6563540913: A02 without pod sysctl and K-37 untested, A03's third configuration, A04's default omitting `connections.max.idle.ms`, standing approval for this plan, and merge by the #301 control session under the merge-rule conditions they keep. Source: [decision 6563545115](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/315#issuecomment-6563545115)
- Approves plan Revision 1 under standing approval, `Blocking findings: none`. Source: [plan check 6563740367](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/315#issuecomment-6563740367)
Open questions:
- None
Next action: Hari or the #301 control session merges the pull request under the #301 override decision; after merge, post the completion comment on this issue.

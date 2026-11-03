# State: #333 Count connections that fail on resume as not kept; idle item subsets; C5 span splits (#301 child 10)
Covers: comment 6597891329 created 2026-11-03T07:25:31Z; read at 2026-11-03T07:38:06Z
Parent items: [#301](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301)
Item plan: [split decision 6563452228](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563452228) and [silent-drop decision 6596426514](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6596426514), which adds split entry 10
Implementing tool: Claude Code. Source: no comment on the issue or its listed parents before `ready-for-agent`; Claude Code by default
Approved plan: [Idle resume timeout judgement implementation-grade plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/333#issuecomment-6596783115), updated_at 2026-11-03T06:58:37Z, approved under standing approval by [plan check 6597891329](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/333#issuecomment-6597891329)
Decisions in force:
- Issue body: split entry 10 of #301; adds the judge's resume-timeout connection class, idle-observation item subsets and two more C5 span splits; dispatches nothing and publishes no verdict. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/333)
- Adopts, in creation order, the #301 decisions 6562996229, 6563452228, 6563483800, 6563495437, 6563515099, 6563540913, 6569906599, 6588207250 and 6596426514, and decision 6596527645; under the override decision the plan may be approved under "Standing approval", including the expected-outcome changes the silent-drop decision authorizes, and the #301 control session may merge. Source: [decision 6596840872](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/333#issuecomment-6596840872)
Open questions:
- None
Next action: the #301 control session merges the pull request under the #301 override decision; after merge it dispatches a main-only C5 mutation campaign and then one idle-observation batch with `items` `A02,A04`.

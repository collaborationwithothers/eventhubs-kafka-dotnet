# State: #342 Add correction notes to published results whose mutation gate used a leakage-affected C5 campaign
Covers: comment 6612103873 created 2026-11-03T18:25:07Z; read at 2026-11-03T18:30:00Z
Parent items: none
Item plan: none
Implementing tool: Claude Code. Source: no comment on the issue before `ready-for-agent` (applied 2026-11-03T18:26:32Z); Claude Code by default
Approved plan: none
Decisions in force:
- Issue body: points 1 to 4; add dated correction notes to affected results, change nothing else, one pull request labelled `verdict` that Hari merges. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342)
- Decision points 5 to 8: re-judge each affected result offline; report the outcome in the note; a difference is a finding, not an edit; cap of 10 affected results, above which the session records the list and count and hands off under AGENTS.md "Before opening a pull request" rule 11 with exhaustion action `push -wip and hand off to the next session`. Source: [decision 6612103873](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6612103873)
Open questions:
- The count of affected results is 14, above the cap of 10. Applied unchanged, the cap stops every later session at the same count. Source: [handoff](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342)
Next action: Hari decides how to proceed past the cap; no plan has been drafted and no result file has been changed.

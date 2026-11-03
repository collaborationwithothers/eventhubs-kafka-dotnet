# State: #342 Add correction notes to published results whose mutation gate used a leakage-affected C5 campaign
Covers: comment 6612352786 created 2026-11-03T18:36:15Z; read at 2026-11-03T19:00:00Z
Parent items: none
Item plan: none
Implementing tool: Claude Code. Source: no comment on the issue before `ready-for-agent` (applied 2026-11-03T18:26:32Z); Claude Code by default
Approved plan: none
Decisions in force:
- Issue body: points 1 to 4; add dated correction notes to affected results, change nothing else, one pull request labelled `verdict` that Hari merges. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342)
- Decision points 5 to 8: re-judge each affected result offline; report the outcome in the note; a difference is a finding, not an edit; point 8's cap of 10 is amended by the next line. Source: [decision 6612103873](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6612103873)
- Cap raised to the 14 affected results A01, A03, B01, B03, B04, B05, B08, C08, C12, E01, F01, F02, F04, F06 in one pull request; each re-judge is one offline attempt with a 30 minute limit and gaps are recorded; fixed wording for campaign 41232286130; the note names both campaigns where `.md` and `.json` differ. If the list grows above 14, push -wip and hand off to the next session. Source: [decision 6612352786](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6612352786)
Open questions:
- None
Next action: Plan under standing approval, then implement the 14 correction notes and offline re-judges in one pull request labelled `verdict`.

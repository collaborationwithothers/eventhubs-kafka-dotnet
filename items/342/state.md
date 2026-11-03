# State: #342 Add correction notes to published results whose mutation gate used a leakage-affected C5 campaign
Covers: comment 6613306689 created 2026-11-03T19:21:24Z; read at 2026-11-03T19:40:00Z
Parent items: none
Item plan: none
Implementing tool: Claude Code. Source: no comment on the issue before `ready-for-agent` (applied 2026-11-03T18:26:32Z); Claude Code by default
Approved plan: [Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6612715816), updated_at 2026-11-03T19:12:33Z, approved under standing approval by [plan check 6613306689](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6613306689) (`Blocking findings: none`, open questions None)
Decisions in force:
- Issue body: points 1 to 4; add dated correction notes to affected results, change nothing else, one pull request labelled `verdict` that Hari merges. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342)
- Decision points 5 to 8: re-judge each affected result offline; report the outcome in the note; a difference is a finding, not an edit; point 8's cap of 10 is amended by the next line. Source: [decision 6612103873](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6612103873)
- Cap raised to the 14 affected results A01, A03, B01, B03, B04, B05, B08, C08, C12, E01, F01, F02, F04, F06 in one pull request; each re-judge is one offline attempt with a 30 minute limit and gaps are recorded; fixed wording for campaign 41232286130; the note names both campaigns where `.md` and `.json` differ. If the list grows above 14, push -wip and hand off to the next session. Source: [decision 6612352786](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6612352786)
- Re-judge gaps recorded for all 14 (option c, no offline driver); full note appended at the end of each `.md` and the `.json` field last; one pointer sentence after each `.md` opening paragraph; `.json` of all 14 gets the field; follow-up on retaining judge inputs is a separate issue. Source: [decision 6612892439](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6612892439)
Open questions:
- None. Both questions of plan Revision 1 are answered by decision 6612892439. Source: [decision 6612892439](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/342#issuecomment-6612892439)
Next action: Hari reviews and merges the verdict pull request (link in the issue 342 completion comment).

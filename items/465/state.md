# State: #465 Scope each rendered session timeline to its own session
Covers: comment 6861051925 created 2026-11-13T18:09:28Z; read at 2026-11-13T18:20:00Z
Parent items: none
Item plan: none
Implementing tool: Claude Code. Source: no comment on the issue or its listed parents before `ready-for-agent`; Claude Code by default
Approved plan: [session-window plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/465#issuecomment-6860599034), file `items/465/plan-session-window.md` at commit fafc9de4dafc2ec26503fc46d20e580b3c6bdb51, approved under standing approval by the [plan check 6861051925](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/465#issuecomment-6861051925) (`Blocking findings: none`, open questions `None`)
Decisions in force:
- Issue body: adds a session-start event so each rendered block covers only its own session; the plan chooses between that and a per-session log path. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/465)
- Decision on Revision 1: the window starts at the last `session.start` line in file order, other sources are cut at that line's time, one writer per log is accepted by name, per-session logs are not chosen. Source: [decision 6860837873](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/465#issuecomment-6860837873)
Open questions:
- None
Next action: Hari reviews and merges the pull request; after merge, post the completion comment on this issue.

# State: #445 Rename Tier=Host to Tier=CGroupV2, tag the abrupt-exit end-to-end test, and start Docker in Claude cloud sessions
Covers: comment 6849713128 created 2026-11-13T09:57:10Z; read at 2026-11-13T10:20:00Z
Parent items: none
Item plan: none
Implementing tool: Claude Code. Source: no comment on the issue or its listed parents before `ready-for-agent` (applied 2026-11-13T08:26:07Z); Claude Code by default. [Comment 6847943529](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6847943529), "Tool: Claude Code", came 17 s later and names the same tool.
Approved plan: [cgroupv2-docker-hook contract-grade plan, Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6848204985), commit 386f36bd5b4fa5b2295e6714bbf245a9c2b6916c, approved under standing approval by [plan check 6849713128](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6849713128) under [decision 6849531953](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6849531953)
Decisions in force:
- Issue body: rename the trait value `Host` to `CGroupV2`; tag the end-to-end test; start `dockerd` in Claude cloud sessions, preferably by a `SessionStart` hook. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445)
- Proposal 4 (Codex cloud measurement) is out of scope; implement proposals 1 to 3 only. Source: [decision 6848081896](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6848081896)
- Do not accept deployment assumption D3; before merge a fresh Claude Code cloud session on the branch runs `pgrep -x dockerd` and `docker version --format '{{.Server.Version}}'` in its first turn, and both outputs go in the receipt. Source: [decision 6848259371](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6848259371)
- Revise Revision 2 for plan check findings B1 and B2 using evidence note 6849527475, get a fresh check, then implement under standing approval. Source: [decision 6849531953](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6849531953)
Open questions:
- None
Next action: cold review, push, fresh-session live hook check (acceptance command 10), then Codex review and hand off to Hari for merge (protected paths).

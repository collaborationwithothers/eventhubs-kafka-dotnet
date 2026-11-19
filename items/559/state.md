# State: #559 Give each ephemeral storage account a unique name
Covers: comment 7022333432 created 2026-11-19T21:11:10Z; read at 2026-11-19T21:30:00Z
Parent items: none
Item plan: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/559#issuecomment-7022280608 (revision r1, `items/559/plan-unique-storage-name.md` on `claude/559-plan` at 26c3b40081be333fa9806a25fe2987df1f440ac1)
Implementing tool: Claude Code. Source: no comment on the issue before `ready-for-agent`; Claude Code by default
Approved plan: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/559#issuecomment-7022280608, revision r1
Decisions in force:
- Issue body: the ephemeral layer names its storage account with a part that differs on every apply. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/559
- Approval of plan r1 at commit 26c3b40081be333fa9806a25fe2987df1f440ac1, with the live smoke rule answered (A), the name collision risk accepted and the inferred cause accepted. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/559#issuecomment-7022333432
Open questions:
- None
Next action: The first two-phase scenario dispatch after merge is the live smoke run; the control session of that scenario issue records its result.

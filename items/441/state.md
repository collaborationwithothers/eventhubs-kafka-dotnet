# State: #441 Parallelise the .NET verification job by test project
Covers: comment 6852015890 created 2026-11-13T11:51:01Z; read at 2026-11-13T12:20:00Z
Parent items: none
Item plan: none (contract-grade plan below; no split)
Implementing tool: Claude Code. Source: no comment on the issue or its listed parents before `ready-for-agent`; Claude Code by default
Approved plan: [plan comment 6851421057, revision r2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/441#issuecomment-6851421057), approved under standing approval by the [plan check 6852015890](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/441#issuecomment-6852015890)
Decisions in force:
- Issue body: per-project matrix, aggregate job keeping the required check name, artifacts. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/441
- Required check context is `.NET verification`; matrix jobs need other names. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/441#issuecomment-6847978940
- No test project is sharded in this pull request; sharding goes to a follow-up issue. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/441#issuecomment-6851788370
Open questions:
- None
Next action: Codex review of the pull request head, then Hari merges (protected workflow path).

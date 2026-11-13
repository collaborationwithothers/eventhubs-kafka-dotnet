# State: #452 Shard the slowest .NET test projects in CI, from measured per-job times
Covers: comment 6858183518 created 2026-11-13T16:01:41Z; read at 2026-11-13T16:13:48Z
Parent items: none
Item plan: none (contract-grade plan below; no split)
Implementing tool: Claude Code. Source: no comment on the issue or its listed parents before `ready-for-agent`; Claude Code by default
Approved plan: [plan comment 6856499810, revision r4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/452#issuecomment-6856499810), file `items/452/plan-test-shards.md` at commit `2226c77a98bcd937989a1062cae8c7f6e551c494`, approved by Hari's [approval 6858183518](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/452#issuecomment-6858183518) after the [plan check 6858081815](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/452#issuecomment-6858081815) (`Blocking findings: none`)
Decisions in force:
- Issue body: shard the slowest test projects from measured per-job times, keep the aggregate check that every test ran once, state savings only as measurements, keep the triggers and the required check name `.NET verification`. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/452
- Approval of plan r4 at commit `2226c77a98bcd937989a1062cae8c7f6e551c494`: "Implement this exact revision." Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/452#issuecomment-6858183518
Open questions:
- None
Next action: Codex review of the pull request head and the hosted `.NET verification` run, then Hari merges (protected workflow path).

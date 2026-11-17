# State: #540 KafkaNativeMetricStateTests absent-counter test fails intermittently in CI
Covers: comment 6960689672 created 2026-11-17T20:53:08Z; read at 2026-11-17T21:03:01Z
Parent items: none
Item plan: none (one pull request; contract-grade plan file below)
Implementing tool: Claude Code. Source: no comment on the issue names a tool before `ready-for-agent` (applied 2026-11-17T20:53:48Z); Claude Code by default
Approved plan: [capture-isolation contract-grade plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/540#issuecomment-6958723357), `items/540/plan-capture-isolation.md` at commit cb09f6a50f6c244a731a212ec57f5fae43ef53bc on `claude/540-plan`
Decisions in force:
- Issue body: the absent-counter test fails intermittently in hosted CI; find the root cause, fix it, show it by repeated runs, never loosen or skip an assertion. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/540
- Agents merge by merge commit (`merge_method: merge`), adopted from #499. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/540#issuecomment-6958523164
- Approval of the plan at the commit above; plan-check finding 1 (missing xUnit citation) is `apply in implementation`. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/540#issuecomment-6960689672
Open questions:
- None
Next action: open the implementation pull request after cold review and the sandbox gate, then carry it through Codex review to merge

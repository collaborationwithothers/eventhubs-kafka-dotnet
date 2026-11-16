# State: #509 Consumer self-termination at a fixed point (#499 entry 6)
Covers: comment 6910955423 created 2026-11-16T08:09:56Z; read at 2026-11-16T08:31:42Z
Parent items: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499
Item plan: none; the parent holds a proposal, not an item plan.
Implementing tool: Claude Code. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6910955423 (replaces Codex for #509 only)
Approved plan: consumer-self-kill revision 2, items/509/plan-consumer-self-kill.md at 8bd0579b27ead7afaab4eb755a7d984803244fd3. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6909051898 and https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6909334509
Decisions in force:
- Issue body: split entry 6 adds the subject consumer's self-termination at a fixed point; predecessor entry 2 is merged through pull request 524. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509
- Adopt the parent round 1 answers (only the subject ends its own process, once per repeat, at a fixed point). Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6895300516
- Adopt the parent round 3 answers (SIGKILL, kill count of processed messages, kill-intent row before the kill). Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6895344090
- Adopt the standing control-session, grilling-answer and plan-approval authority, subject to its reserved actions. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6895538069
- Adopt the recommended answers to the plan's two questions: deployment assumptions D1 to D3 accepted by name, and the kill count and slow message are mutually exclusive. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6909196149
- Approve plan revision 2; apply the check's three implementation texts (ADR citations in sections 7 and 10a, test K1 valid cases). Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6909334509
- Claude Code implements this issue; Codex reviews. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/509#issuecomment-6910955423
Open questions:
- None.
Next action: Obtain the cold review, passing final-head CI and hosted reviews. The production kill-count source and the live smoke remain with issue 510.

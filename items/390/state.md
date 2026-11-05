# State: #390 Partition-add component test readiness precondition (#347 child)
Covers: comment 6672131429 created 2026-11-05T23:22:16Z; read at 2026-11-05T23:23:19.051462+00:00
Parent items: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347
Item plan: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/390
Implementing tool: Codex. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/390#issuecomment-6671956988
Approved plan: [Partition-add readiness, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/390#issuecomment-6672078583), standing approval through [independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/390#issuecomment-6672131429)
Decisions in force:
- The issue body requires bounded, observable partition and leader readiness before synthetic production, explicit failure on exhaustion, successful production and every existing assertion. The subject is verifier handling of an established added partition; a contrary subject or test relaxation goes to Hari. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/390
Open questions:
- None
Next action: Complete final-head CI and hosted Codex review, then mark the PR ready for user review. No merge or workflow dispatch is authorized by this request.

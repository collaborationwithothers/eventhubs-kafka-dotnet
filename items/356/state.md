# State: #356 B10 expected outcomes (#348 entry 5)
Covers: comment 6890174272 created 2026-11-15T06:09:38Z; read at 2026-11-15T06:17:20Z
Parent items: [#348](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348), [#347](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347)
Item plan: [B10 split, handoff 6617284257 on #348](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617284257), entry 5, adopted in [decision 6617607468](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617607468)
Implementing tool: Codex. Source: [comment 6633653241](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6633653241)
Approved plan: [B10 timeout-and-resend expectations contract-grade plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6693074361), Revision 5, commit aa305c5443d627e99bbea7ba859c52ef460d53a6
Decisions in force:
- Issue body: this child adds scenarios/B10/expected.yaml; its decision rules and verdict word go to Hari. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356)
- Adopts the #347 standing authority, points 1 to 6, the B10 split and restart decision 6617344472; decision rules and verdict words stay with Hari. Source: [decision 6617624003](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6617624003)
- Codex implements; a Claude review gates the Codex pull request before the control session merges. Source: [decision 6633535614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6633535614)
- Codex sessions may run standing approval, except for a plan that needs Hari's own approval, such as this issue's. Source: [decision 6633795942](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6633795942)
- B10 decision rules, restart attribution, smoke run 42189889469 smoke only, client versions fixed in a separate change, and Hari merges the expected-outcome pull request. Source: [decision 6703447017](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6703447017)
- Two ADR-021 verdicts, superseding decision 6703447017 points 1 and 2 where they conflict; duplicate sentence option B; held for a null b10_final; check findings B1 and B2 adopted. Source: [decision 6889976034](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6889976034)
- Hari approves Revision 5 at aa305c5443d627e99bbea7ba859c52ef460d53a6; Hari merges. Governed diagnostic run 43635822755 counts; record that in the PR notes without revising the plan file. Source: [approval 6890174272](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6890174272)
Open questions:
- None
Next action: Hari reviews and merges the expected-outcome pull request; then a fresh scientific B10 run on main and the verdict pull request.

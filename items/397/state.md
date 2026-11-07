# State: #397 C5 mutation run time reduction and #355 follow-ups

Covers: comment 6703427658 created 2026-11-07T06:34:32Z; read at 2026-11-07T06:35:34Z
Parent items: [producer investigation](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347)
Item plan: [child scope](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397); [parent proposal](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347)
Implementing tool: Codex. Source: [tool assignment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6683024818)
Approved plan: [C5 measured span rebalancing Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703130075), approved by [Hari's reporter-selector amendment approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703427658), after the [Revision 2 check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703401722). The [earlier bookkeeping exception](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703287109) remains in force.
Decisions in force:
- Investigate runtime and coverage history first, propose options, and complete state/runbook/memory follow-ups. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397)
- Semantics-preserving fixes may proceed; selection, thresholds, gates and assertions need Hari's Approval after measurements. Existing run cancelled; control session owns a later main-only run. Source: [runtime decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6683039430)
- Implement the checked rebalance with only its listed administrative contract and legacy fixture updates, including ProjectionTests, and both exact plan-check corrections. Every judging assertion, score/coverage/isolation assertion and gate stays unchanged; Hari merges the workflow change and Control owns the post-merge main-only C5 run. Source: [narrow approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703287109)
- Retain separate crossing negatives but clamp the final endpoint to decoded UTF-16 source length; add the independent reporter proof, preserving all existing judging behavior. Source: [Revision 2 approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703427658)
Open questions:
- None
Next action: Merge the measured span rebalance only after required final-head checks and reviews, with Hari merging the workflow change. The control session owns a later authorized main-only C5 measurement; runtime improvement and its resulting acceptance evidence are not established by this PR.

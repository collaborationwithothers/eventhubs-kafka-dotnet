# State: #367 B10 restart manifest and resend execution (#354 entry 3b1)
Covers: comment 6629355140 created 2026-11-04T11:24:56Z; read at 2026-11-04T11:30:47Z
Parent items: #354, #348, #347
Item plan: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/354#issuecomment-6626488514 (planning handoff adopted by the split decision; the parent plan for this child)
Implementing tool: Claude Code. Source: no comment on the issue or its listed parents before `ready-for-agent`; Claude Code by default
Approved plan: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/367#issuecomment-6628916473 (Revision: 2), approved under standing approval by the plan check https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/367#issuecomment-6629355140
Decisions in force:
- Issue body: child of #354, #348 and #347, split entry 3b1, with predecessors the pull requests of #351 and #353. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/367
- Adopts the #347 standing authority, the #348 split and restart decisions, the #351 decisions and the #354 decisions. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/367#issuecomment-6627000284
- #347 standing authority, points 1 to 6 (point 5 allows standing approval). Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6617257796
- #348: test producer restart under B10 (K-53). Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617344472
- #348: adopt the B10 split. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617607468
- #351: restart shape and late copies for the B10 oracle amendment. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6623554949
- #351: a late record inside a later repeat's window. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/351#issuecomment-6623766068
- #354: only resends, empty second instance allowed. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/354#issuecomment-6625943372
- #354: a restart manifest for the second B10 instance only. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/354#issuecomment-6625962570
- #354: split, original planned offset, no retry policy, one owner of the permitted-timeout rule. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/354#issuecomment-6626999164
- Plan check applies two P2 findings in implementation: stage named paths instead of git add -A, and run the LoadGen project directly when Docker is missing. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/367#issuecomment-6629355140
Open questions:
- None
Next action: cold review, push, open the pull request, request Codex review.

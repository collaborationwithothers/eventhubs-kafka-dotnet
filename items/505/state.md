# State: #505 Consumer worker: processing mode in Parchwell.Enrichment (#499 entry 2)
Covers: comment 6902733333 created 2026-11-15T22:53:25Z; read at 2026-11-15T23:03:36Z
Parent items: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499
Item plan: none; the parent holds a proposal, not an item plan.
Implementing tool: Codex. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894614673 and https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895573282
Approved plan: consumer-worker revision 4, items/505/plan-consumer-worker.md at c617dfba872ff10b89cf248dfd044af7ec135a74. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505#issuecomment-6897683461 and https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505#issuecomment-6900161937
Decisions in force:
- Issue body: split entry 2 adds the processing worker, durable record, registration, completion and component tests; predecessor entry 1 is merged through pull request 518. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505
- Adopt the parent round 1 answers: per-member durable processing records, group shape, start order, configuration fidelity and invalid runs. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505#issuecomment-6895300019
- Adopt the parent round 2 answers: consumer outcome classes, completion, independent commit evidence, storage, timing, witness and verifier order. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505#issuecomment-6895328783
- Adopt the parent round 3 answers: scoped self-termination, no topic writes and registration before consumer construction. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505#issuecomment-6895343691
- Adopt the standing control-session, grilling-answer and plan-approval authority, subject to its reserved actions. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505#issuecomment-6895536338
- The slow-message fault selects a consumed ordinal and uses inline or pause mode. Deployment assumptions D1 to D5 are accepted by name. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505#issuecomment-6899404704
- Ship the settings seam without a production source; issue 508 adds that source and schema, and issue 510 extends it. The consumer phase judge is separate entry 11, issue 521. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505#issuecomment-6899939867
- Adopt the verifier plan as record-format owner and phases C0 and C1 with configuration equal to phase. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505#issuecomment-6900154637
- Approve worker revision 4 and its two plan-text corrections; apply the check's four implementation P2s. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505#issuecomment-6900161937
- Amend sections 3.5 and 12 to use verifier revision 5 at a4c5ea1bec8630359a31d9b096416b2204f85dc7 for the record format and its stop comparison; all other requirements and implementation corrections remain. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/505#issuecomment-6902733333
Open questions:
- None.
Next action: Complete implementation verification and the required reviews; Hari merges the approved package-reference additions. The production settings source and live smoke remain with issue 508.

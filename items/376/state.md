# State: #376 B10 two-instance verify (#352 entry 2a1b)
Covers: comment 6639746016 created 2026-11-04T19:15:19Z; read at 2026-11-04T19:15:52.962547+00:00
Parent items: [#352](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/352), [#348](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348), [#347](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347)
Item plan: [adopted three-way split](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/352#issuecomment-6633309194), adopting [two-instance verify boundary](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/352#issuecomment-6632689637)
Implementing tool: Codex. Source: [tool assignment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/376#issuecomment-6633628942)
Approved plan: [B10 two-instance verify, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/376#issuecomment-6639646060), approved through [standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/376#issuecomment-6639746016)
Decisions in force:
- Implement two-instance binding, per-instance publication and bookkeeping checks, shared start/window and dual result/receipt identity after #375. B10 is never complete_valid in this slice; add its own new judgement targets to C5. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/376)
- Adopt the #347 standing authority, #348 restart/split, #351 oracle decisions and #352 verifier splits. Their stated exceptions still go to Hari. Source: [adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/376#issuecomment-6633310956)
- Codex implements after its tool assignment and ready-for-agent label; the separate Claude review gates merge, and Hari merges workflow changes. Source: [review and merge adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/376#issuecomment-6633533494)
- Codex may run its own standing-approval planning and independent check except for plans needing Hari's own approval. Source: [standing-approval amendment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/376#issuecomment-6633793761)
Open questions:
- None
Next action: Implementation and local verification are complete. Finish cold review, publish the PR and obtain final-head CI/Codex review before marking it ready. The control session then obtains Claude review and Hari's workflow merge. After merge, run C5 on main before evidence publication; #377 implements two-instance judge.

# State: #373 B10 per-repeat judgement (#352 entry 2a2)
Covers: comment 6651118233 created 2026-11-05T06:10:05Z; read at 2026-11-05T06:11:21.043102+00:00
Parent items: [#352](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/352), [#348](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348), [#347](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347)
Item plan: [adopted per-repeat split](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/352#issuecomment-6632607229), with [scope table](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/352#issuecomment-6632062800)
Implementing tool: Codex. Source: [tool assignment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/373#issuecomment-6633625521)
Approved plan: [B10 per-repeat judgement, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/373#issuecomment-6651019638), approved through [standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/373#issuecomment-6651118233)
Decisions in force:
- Implement the B10 per-repeat timeout, retry, combined-classification and nonfinal-result scope, with new judgement files in C5. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/373)
- Adopt standing authority, the B10 oracle/restart decisions and verifier splits, with their exceptions reserved for Hari. Source: [authority adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/373#issuecomment-6632608633)
- The predecessor is the two-instance judge child, whose PR 385 has merged. Source: [predecessor decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/373#issuecomment-6633309663)
- Codex implements; the control session obtains a separate Claude review before merge, and Hari merges workflow changes. Source: [review and merge adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/373#issuecomment-6633534412)
- Codex may plan and independently check under standing approval except for plans needing Hari's own decision. Source: [standing-approval amendment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/373#issuecomment-6633794577)
- Hari authorizes the named interim-timeout assertion replacement and coherent retry-fixture repair in this PR. Replacement tests retain every non-permitted guard rejection and the same timeout's check-12 rejection outside B10; the PR names the old assertion and ADR-021 sentence. Results remain nonfinal with no published counts. Source: [scoped test approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/373#issuecomment-6650981348)
Open questions:
- None
Next action: Obtain the required separate Claude review, have Hari merge the protected workflow change after final-head CI and Codex review pass, and obtain postmerge main-only C5 acceptance before any scientific evidence publication. Earlier-repeat exclusion and the final sweep remain successor work.

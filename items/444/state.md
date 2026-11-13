# State: #444 LoadGen Kafka fixture fails with NullReferenceException when Docker is unreachable
Covers: comment 6847065817 created 2026-11-13T07:36:25Z; read at 2026-11-13T07:41:51Z
Parent items: none
Item plan: none
Implementing tool: Claude Code. Source: no comment on the issue before `ready-for-agent` (applied 2026-11-13T07:13:17Z); Claude Code by default.
Approved plan: [docker-unreachable-message contract-grade plan, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/444#issuecomment-6846973892), commit 32d9c311194d3492fc39ffea2a05fbf3a7a43bf4, approved under standing approval by [plan check 6847065817](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/444#issuecomment-6847065817)
Decisions in force:
- Issue body: fail at fixture start with a message naming Docker when Testcontainers finds no endpoint; do not skip; change no assertion. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/444)
Open questions:
- None
Next action: Codex review of the pull request head, then merge under the merge rule or hand off.

# State: #355 B10 scenario, admission and smoke

Covers: comment 6678364206; read at 2026-11-06T06:15:51Z. The full child and parent metadata snapshots are retained outside the repository for refresh.
Parent items: [B10 design](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348), [producer investigation](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347)
Item plan: [adopted B10 split](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617607468)
Implementing tool: Codex. Source: [tool assignment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/355#issuecomment-6633648649)
Approved plan: [B10 scenario admission and sweep wiring, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/355#issuecomment-6669538298), approved through the [standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/355#issuecomment-6671933423)
Decisions in force:
- Add the scenario, workflow admission and later smoke, with the listed design and producer parents. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/355)
- Adopt standing authority, the B10 split and sequential producer restart, retaining the exceptions reserved for Hari. Source: [authority adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/355#issuecomment-6617623722)
- Follow the late-scan predecessor; supply its sweep note and record late-copy frequency and landing locations. Source: [reader/writer split adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/355#issuecomment-6625949286)
- Follow the second-Job predecessor and add its end-to-end worker test. Source: [restart dependency](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/355#issuecomment-6626999833)
- Follow the per-repeat judgement predecessor. Source: [judgement dependency](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/355#issuecomment-6632608182)
- Codex implements; a separate Claude review gates the pull request and Hari merges workflow changes. Source: [review and merge adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/355#issuecomment-6633535243)
- Codex may plan and independently check under standing approval, except for plans needing Hari's own approval. Source: [standing-approval amendment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/355#issuecomment-6633795477)
- Approve the scoped predecessor identity/chart amendment and fifteen-group cumulative failure cap, retaining eleven prior groups and every other gate. Source: [explicit chat approval recorded on the issue](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/355#issuecomment-6678125827)
- Approve the second-worker test-only comparison of production completion serialization and stored bytes, retaining the actual captured context and every other assertion. Source: [completion comparison approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/355#issuecomment-6678364206)
Open questions:
- None
Next action: Local acceptance passed: full .NET 9169, Python 307, chart, offline, mirror, whitespace and documentation checks (unchanged-file warnings retained). Cold-review the frozen full authored diff, then publish the tested draft PR from the normal branch and remove its backup. Final-head CI and hosted Codex review must pass before readiness; separate Claude review and Hari workflow merge remain required. All dispatches and smoke remain deferred. Expected outcomes and later main C5 acceptance remain successor work.

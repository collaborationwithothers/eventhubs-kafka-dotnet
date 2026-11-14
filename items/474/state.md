# State: #474 verifier mutation runtime

Covers: comment 6877906971 created 2026-11-14T13:22:10Z; read at 2026-11-14T13:26:21Z
Parent items: [#347](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347)
Item plan: [mutation runtime plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6876990691)
Implementing tool: Codex. Source: [approval and direction](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6877906971)
Approved plan: [revision 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6876990691), commit `1cdbcdf0407c1b897c7bcec29ef97d261109b473`
Decisions in force:
- Reduce mutation workflow runtime while preserving fresh coverage. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474)
- The cancelled mutation run is not acceptance evidence. Source: [cancellation](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6876575032)
- Use bounded profiling, estimates and unchanged fresh mutation testing. Source: [design](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6876901367)
- Reassess all shard timing when any target changes. Source: [timing correction](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6877623126)
- Implement the approved two-pull-request plan, starting with bounded tooling; no workflow dispatch is authorized. Source: [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6877906971)
Open questions:
- None about the chosen policy. Source: [plan section 10](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/1cdbcdf0407c1b897c7bcec29ef97d261109b473/items/474/plan-mutation-runtime.md#10-open-questions-and-stop-rule)
Next action: After the tooling pull request merges, execute the bounded evidence procedure and qualify the layout in the second pull request.

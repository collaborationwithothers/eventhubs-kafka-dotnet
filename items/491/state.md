# State: #491 Run both scenario configurations in one workflow job, so storage is not destroyed and recreated between them

Covers: comment 6894099185 created 2026-11-15T12:09:59Z; read at 2026-11-15T12:10:21Z
Parent items: [#347](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347)
Item plan: none
Implementing tool: Codex. Source: [smoke-only implementation approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/491#issuecomment-6893734659)
Approved plan: [retained-storage Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/491#issuecomment-6893070505), Entry A only; [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/491#issuecomment-6893734659)
Decisions in force:
- Investigate running configurations in one job; preserve the temporary C1 approval-gap workaround until replacement is validated. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/491)
- Use shared storage, sequential isolated namespaces, confirmed cleanup, one protected job and diagnostics to the existing LAW. Source: [decision 6892991919](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/491#issuecomment-6892991919)
- Correct the plan and conduct one additional check; this does not approve production implementation. Source: [decision 6893617134](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/491#issuecomment-6893617134)
- Implement smoke-only Entry A; production Entry B, a dispatch, Azure actions in an agent session and an agent merge of protected paths are not authorized. Source: [approval 6893734659](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/491#issuecomment-6893734659)
- Include the smoke workflow in the existing exact-concurrency and trigger checker and its tests. Source: [decision 6893894063](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/491#issuecomment-6893894063)
- Accept separate fresh workload-active private connections and positive delivery, with exact overlap unproven. Source: [decision 6894099185](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/491#issuecomment-6894099185)
Open questions:
- Live storage preservation, private connectivity and diagnostics ingestion remain to be established before production-plan approval. Source: [plan check 6893686701](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/491#issuecomment-6893686701)
Next action: After Entry A merges, control/Hari authorizes the bounded main-only smoke and verifies its evidence before production-plan approval.

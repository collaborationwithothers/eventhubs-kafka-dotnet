# State: #549 Archive records 'native index unavailable' for every unit in the C03 smoke run
Covers: comment 6977144908 created 2026-11-18T11:23:39Z; read at 2026-11-18T21:35:13Z
Parent items: none
Item plan: none (no item plan or parent plan; the contract-grade plan below is the plan of the one pull request)
Implementing tool: Claude Code. Source: [decision 6973160092](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/549#issuecomment-6973160092)
Approved plan: [native-index-cause plan, revision R2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/549#issuecomment-6973330349), file `items/549/plan-native-index-cause.md` at `ed8e9be03d6492b7a37b437eebec817de094659e`, approved by [approval 6977144908](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/549#issuecomment-6977144908). Checked by [plan check 6973460912](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/549#issuecomment-6973460912).
Decisions in force:
- Issue body: find the root cause of "native index unavailable" and fix it with evidence; retrying or loosening the gap rule is not a fix. Source: [issue 549](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/549)
- Claude Code implements; Codex reviews. Source: [6973160092](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/549#issuecomment-6973160092)
- Plan R2 approved; Q1 small pull request, Q2 deployment assumption DA1 accepted by name, Q3 settled by the issue 548 decision (deadline raised to 1800 s, nothing added here). Findings 3, 4, 6 and 7 of the check stand. Source: [6977144908](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/549#issuecomment-6977144908)
Open questions:
- None. Closing evidence D1 (next C03 smoke after issue 548 merges: every index object verified, no boundary starting `native index`) is deferred and owned by the session that runs that smoke. Source: plan section 11
Next action: cold review of the implementation diff, then open the pull request.

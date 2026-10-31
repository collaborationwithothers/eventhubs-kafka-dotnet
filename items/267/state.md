# State: #267 C5 mutation: split large targets across parallel span shards and document a fast local check
Covers: comment 6545852871 created 2026-10-31T16:08:15Z; read at 2026-10-31T16:46:37Z
Parent items: none
Item plan: none
Implementing tool: Claude Code. Source: [decision 6545852871](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/267#issuecomment-6545852871)
Approved plan: [plan comment 6543842230](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/267#issuecomment-6543842230), `Revision: 2.`
Decisions in force:
- Split large C5 targets across parallel jobs by Stryker span; document a fast local check that is never acceptance evidence; the post-merge gate on `main` is unchanged; coverage analysis stays off. Source: [issue body, "Decisions" 1 to 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/267)
- Member-only boundaries at about 1 h 45 min; 3 repeat and 6 configuration parts, 13 jobs; 7200 s rebalance threshold, recorded only; the section 6 validation budget and 10800 s fit gate; the `local-check` and `member-boundaries` subcommands; survivors exposed by run 41183628251 are tracked in #268, out of scope. Source: [decision 6544049253](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/267#issuecomment-6544049253)
- The `mutation` job's upload-step change in plan section 2 is within scope: the evidence artifact excludes `stryker/logs/**`, a new step uploads the logs as `c5-logs-RUN_ID-RUN_ATTEMPT-ID` for 90 days, and the aggregate download step is unchanged. Source: [decision 6545484104](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/267#issuecomment-6545484104)
- Hari approves plan Revision 2 with the two decisions above applied; any later plan edit is an amendment needing his approval. Source: [approval 6545825882](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/267#issuecomment-6545825882)
- The implementing tool is Claude Code; Codex still reviews the pull request. Source: [decision 6545852871](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/267#issuecomment-6545852871)
Open questions:
- None
Next action: Implementation pull request for plan Revision 2 under review; after Hari merges it, a passing `verifier-c5-mutation.yml` run on `main` that Hari authorizes and dispatches, recorded on #267.

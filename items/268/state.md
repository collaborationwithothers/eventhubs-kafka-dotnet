# State: #268 C5 mutation: sort 149 survivors exposed after the timeout repair
Covers: comment 6546005921 created 2026-10-31T16:21:55Z; read at 2026-10-31T17:07:00Z
Parent items: none
Item plan: none
Implementing tool: Claude Code. Source: no comment on the issue or its listed parents before `ready-for-agent`; Claude Code by default. [Approval 6546005921](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/268#issuecomment-6546005921) also names Claude Code.
Approved plan: [c5-survivor-sort implementation-grade plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/268#issuecomment-6544994859), Revision 1, `updated_at` 2026-10-31T14:54:07Z
Decisions in force:
- Issue body: sort each survivor of run 41183628251 into missing test, equivalent or accepted gap; add the missing tests without changing verifier judgement code or loosening an assertion; then report the evidence-pack question to Hari. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/268)
- Review cap 149 with `record the gap and continue`; no wait for #267, configuration-target survivors become a separate item; one pull request for all four targets, rule 6 still applies; "killed on replay" accepted as a fourth disposition for the five R1 mutants, cause recorded as inferred. Source: [decision 6545451062](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/268#issuecomment-6545451062)
- Mutants 14286 and 14288 stay "equivalent", with the inferred step stated as in E10 and E11. Source: [decision 6546002143](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/268#issuecomment-6546002143)
- Plan Revision 1 approved, with the two decisions above and the two `apply in implementation` P2s of the [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/268#issuecomment-6545984900). Source: [approval 6546005921](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/268#issuecomment-6546005921)
Open questions:
- Whether the eight evidence packs under `results/` that cite earlier C5 runs need a note; Hari decides after merge. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/268), [decision 6545451062](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/268#issuecomment-6545451062)
Next action: after the implementation pull request merges, post the completion report on this issue with the sorted counts and the evidence-pack question for Hari.

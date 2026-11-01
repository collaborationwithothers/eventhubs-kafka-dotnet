# State: #244 Live authentication follow-up: unmodified native client with AKS workload federation (F02)
Covers: comment 6554652156 created 2026-11-01T06:53:38Z; read at 2026-11-01T07:00:00Z
Parent items: none
Item plan: none
Implementing tool: Claude Code. Source: [comment 6547412843](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6547412843), before `ready-for-agent`
Approved plan: [plan comment 6547370647](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6547370647), `Revision 4`, `updated_at` 2026-10-31T21:04:04Z.
Decisions in force:
- The issue body asks for a live comparison of the unmodified native client with AKS workload federation, with a positive control; it is not an implementation plan or dispatch approval. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244)
- Revision 1 open-question answers: diagnostic route with no `results/`, `expected.yaml`, verifier or judge change; three native arms; `sasl.oauthbearer.sub.claim.name=oid`; redacted principal class as identity evidence; 120000 ms slot and 900 s Job bounds; live-smoke prerequisite does not apply; implementing tool Claude Code; the later dispatch shape is confirmed but not authorized and needs a separate approval after the implementation merges; the blueprint source re-pin is a separate later change. Source: [decision 6547412663](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6547412663)
- Native stages run after the C6 judge; `Validate` requires the 36-character hyphenated GUID principal; the Revision 3 check's P2s are folded into the plan. Source: [decision 6548960917](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6548960917)
- Hari approves plan Revision 4 in light of its plan check (`Blocking findings: none`); the implementing session applies the check's two `apply in implementation` P2s; the approval authorizes no dispatch, Azure action or spend. Source: [approval 6554645171](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6554645171), [plan check 6554607130](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6554607130)
Open questions:
- None
Next action: Implementation pull request for plan Revision 4 under review; after Hari merges it, a live dispatch needs a separate approval from Hari on issue 244.

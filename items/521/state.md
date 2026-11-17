# State: #521 Consumer phase judge: judge mode for C01, C03 and C10 (#499 entry 11)
Covers: comment 6926693475 created 2026-11-16T19:23:54Z; read at 2026-11-16T19:42:39Z
Parent items: [#499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499)
Item plan: the [#499 split approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894587233), which the approved plan names as the parent context (that comment was not read in this session)
Implementing tool: Claude Code. Source: [decision 6926693475](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/521#issuecomment-6926693475), posted before `ready-for-agent` was re-applied at 2026-11-16T19:24:17Z
Approved plan: [consumer-phase-judge contract-grade plan, revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/521#issuecomment-6914668094), `items/521/plan-consumer-phase-judge.md` at commit `dce4e195ccc9c0f51265111e8d28025211fa6ad4`
Decisions in force:
- Issue body: extend the judge so a candidate carries the consumer evidence of ADR-022 for C01, C03 and C10; needs a passing `verifier-c5-mutation.yml` run on `main` after merge. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/521)
- Adopts the #499 settings-source answer and the new consumer phase judge entry. Source: [6899940947](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/521#issuecomment-6899940947)
- Adopts the #499 standing decision and decision 6894614673 points 1 and 2 (Claude plans, Codex implements; point 2 is replaced for #521 by 6926693475). Source: [6899941170](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/521#issuecomment-6899941170)
- Adopts the recommended answers to the status-model fields, the phase aggregation, the codec shape-check option and the marked texts of the second plan check. Source: [6915213551](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/521#issuecomment-6915213551)
- Status model option (b), and the consumer phase logic in a new file with `Judge/PhaseJudge.cs` unchanged. Source: [6925863975](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/521#issuecomment-6925863975)
- Approves plan revision 3 at commit `dce4e195ccc9c0f51265111e8d28025211fa6ad4`, accepts the named mutation-runtime risk, and applies the check's two `apply in implementation` test rows. Source: [6926201546](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/521#issuecomment-6926201546)
- Claude Code implements #521 only. Source: [6926693475](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/521#issuecomment-6926693475)
Open questions:
- None
Next action: cold review, the pre-PR local gate, open the pull request; after Hari merges, dispatch `verifier-c5-mutation.yml` on `main` once (plan section 8, command 5).

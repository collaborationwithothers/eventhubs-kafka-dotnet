# State: #401 B10 candidates: client versions and broker counts missing
Covers: comment 6704292129 created 2026-11-07T08:07:39Z; read at 2026-11-07T08:12:25.889364+00:00
Parent items: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348 and https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347
Item plan: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617607468 (approved parent split); this issue is the separately added provenance fix.
Implementing tool: Codex. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703476599; Hari also selected and confirmed Codex in the interactive task.
Approved plan: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703603871 (Revision 1), standing approval from https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703637652.
Decisions in force:
- Issue body: fix B10 producer-version and broker-count projection before fresh verdict evidence; parent items #348 and #347; no predecessor PRs. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401
- Authorize evidence under the existing B10 intake predicate; retain both producers' versions, require per-version agreement across both instances in every repeat, and retain separate instance broker counts. Fix precedes mutation acceptance and the fresh verdict run; no expected outcome, verdict, test assertion or workflow trigger changes. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703489646
- Adopt the linked #347 standing authority and Codex decisions; allow standing approval, require Claude review of Codex PRs, and leave merge to the control session after its gates (workflow changes to Hari). Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703517852
- Omit individually over-bound captured version values from the B10 observation pair, preserving other evidence, strict supplied-document bounds and legacy root provenance. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6704292129
Open questions:
- None
Next action: verify and cold-review the approved Claude P2 correction in focused mode, publish the updated PR head, and hand off for fresh hosted/Claude review and remaining merge gates; post-merge mutation acceptance and fresh verdict evidence remain downstream. This session dispatches no workflow and does not merge.

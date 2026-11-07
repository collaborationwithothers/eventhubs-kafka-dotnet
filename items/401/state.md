# State: #401 B10 candidates: client versions and broker counts missing
Covers: comment 6703637652 created 2026-11-07T06:57:52Z; read at 2026-11-07T07:09:00.575382+00:00
Parent items: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348 and https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347
Item plan: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617607468 (approved parent split); this issue is the separately added provenance fix.
Implementing tool: Codex. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703476599; Hari also selected and confirmed Codex in the interactive task.
Approved plan: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703603871 (Revision 1), standing approval from https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703637652.
Decisions in force:
- Issue body: fix B10 producer-version and broker-count projection before fresh verdict evidence; parent items #348 and #347; no predecessor PRs. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401
- Authorize evidence under the existing B10 intake predicate; retain both producers' versions, require per-version agreement across both instances in every repeat, and retain separate instance broker counts. Fix precedes mutation acceptance and the fresh verdict run; no expected outcome, verdict, test assertion or workflow trigger changes. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703489646
- Adopt the linked #347 standing authority and Codex decisions; allow standing approval, require Claude review of Codex PRs, and leave merge to the control session after its gates (workflow changes to Hari). Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/401#issuecomment-6703517852
Open questions:
- None
Next action: complete local acceptance and cold review, publish the tested implementation PR, and hand off for hosted/Claude review and control-session merge; mutation acceptance and fresh verdict evidence remain downstream. No workflow dispatch or merge by this implementing session.

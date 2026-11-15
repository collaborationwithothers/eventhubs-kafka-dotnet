# State: #494 Native Kafka telemetry and retained diagnostic evidence

Covers: comment 6897941549 created 2026-11-15T16:52:19Z; read at 2026-11-15T17:29:01.882794+00:00
Parent items: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347
Item plan: none; the proposed product specification is not approved
Implementing tool: Codex. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6892754688
Approved plan: none; the smoke-only prerequisite and its diagnostic follow-up are directly authorized by the decisions below under factory.md Item lifecycle step 1
Decisions in force:
- The issue body defines native telemetry, retry observations and retained verifier evidence for future runs. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494
- Compare telemetry table plans and cost. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6892428668
- Agent Azure identities are separate. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6893629386
- Grilling settles persistent Blob retention, full native statistics, bounded metadata capture, inline observational duplicate categories and the three product PRs. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6893649088
- Adopt the false-receipt correction and prepare an archive setup-and-smoke prerequisite PR using fresh-context subagents. No deployment, spending, dispatch or full product-plan approval is granted. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6894417226
- Accept the dated cost estimate for one smoke and retaining its persistent archive. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6897041922
- Implement enough bounded safe diagnostics for the next controlled attempt and fix confirmed faults, preserving identity/network scope and scientific/evidence contracts. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6897941549
Open questions:
- The diagnostic run retained unknown CLI upload failures. Inference from its source and the pinned validator: the combined verbosity flags would reject command execution; the correction still needs deployed verification. Sources: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43739942721 ; https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/10807b33a89ff64d81c95212fdedae5a9ec62c40/infra/scripts/archive/smoke.py#L492 ; https://github.com/microsoft/knack/blob/v0.14.0/knack/log.py#L135-L148 ; https://github.com/Azure/azure-cli/blob/azure-cli-2.92.0/src/azure-cli/requirements.py3.Linux.txt
- Live archive smoke, named deployed assumptions and the complete product specification still need evidence or the applicable approval. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6894128442
- The first smoke failed at source creation; its generic retained error does not identify the cause. Diagnostic follow-up and actual readiness evidence remain needed. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6897893730
Next action: review/merge the CLI verbosity-conflict correction after its required checks, then obtain a controlled main-only smoke with the corrected arguments; retain the new evidence before claiming archive readiness or approving the full product specification

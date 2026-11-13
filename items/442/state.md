# State: #442 Verification script: UTC stage stamps, run summary and an explicit environment-limited mode
Covers: comment 6848261407 created 2026-11-13T08:44:06Z; read at 2026-11-13T08:50:36Z
Parent items: none
Item plan: none
Implementing tool: Claude Code. Source: no comment on the issue before `ready-for-agent` (applied 2026-11-13T07:12:46Z); Claude Code by default.
Approved plan: [verify-run-summary contract-grade plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/442#issuecomment-6846855136), commit c31a2aaffd502b715cc7d1ea9591761c38714c01, approved under standing approval by [plan check 6848261407](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/442#issuecomment-6848261407)
Decisions in force:
- Issue body: add UTC stage stamps and a run summary to `scripts/verify-dotnet.sh`; an environment-limited mode is proposed there. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/442)
- Decision: drop the environment-limited mode and both probes; keep the stage stamps, `summary.json` without `probes` and `excluded`, and the slowest-tests list; leave the full-gate sentence of `docs/engineering/dotnet-verification.md` unchanged; revise the plan, check it, then implement. Source: [decision 6847884649](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/442#issuecomment-6847884649)
Open questions:
- None
Next action: Codex review of the pull request head, then hand off to Hari, who merges because the change touches `/scripts/` and `/docs/`.

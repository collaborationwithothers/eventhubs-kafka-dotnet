# State: #358 B06 and B07 shared-run admission and smoke (#349 entry 2)
Covers: comment 6625107196 created 2026-11-04T07:21:14Z; read at 2026-11-04T07:22:07Z
Parent items: [#349](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/349), [#347](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347)
Item plan: [split adoption on #349](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/349#issuecomment-6617607805), adopting recommendations 1 to 6 of the [split proposal](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/349#issuecomment-6617362572)
Implementing tool: Claude Code. Source: no comment on the issue or its listed parents before `ready-for-agent` (applied to #347 and #349 on 2026-11-03); Claude Code by default
Approved plan: [Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/358#issuecomment-6619882968), updated_at 2026-11-04T05:46:46Z, approved under standing approval by [plan check 6624072067](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/358#issuecomment-6624072067) (`Blocking findings: none`, open questions None)
Decisions in force:
- Issue body: a scenario.yml input choice that runs every B06 and B07 configuration in one job, smoke only, and the phase script loop; byte copy updated; triggers unchanged. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/358)
- Standing authority: points 1 to 6 on #347 and its children, and the split adoption. Source: [decision 6617624626](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/358#issuecomment-6617624626)
- Changes 5 and 10 (LoadGen and Verifier project files, architecture tests) join this pull request; no assertion is loosened, deleted or skipped. Smoke shape: one repeat per configuration, no judge, summaries only, not published. Source: [decision 6620065964](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/358#issuecomment-6620065964)
- The new `scenario` input value is not a trigger change; the offline `on:` check is updated without loosening; Hari merges. Source: [decision 6623554708](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/358#issuecomment-6623554708)
- Hari confirmed the workflow identity can read the `ledger` container. Source: [decision 6623742499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/358#issuecomment-6623742499)
- Plan amendment: `charts/parchwell/tests/test_loadgen_host.py` is the 17th changed path (case count 7 to 11). Source: [decision 6625107196](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/358#issuecomment-6625107196)
Open questions:
- None
Next action: Hari merges the pull request; then the #347 control session dispatches the smoke on `main` (scenario=ledger-batch, run_kind=smoke, items=all); entry 3 is planned after it.

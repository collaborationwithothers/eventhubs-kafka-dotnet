# State: #357 B06 and B07 declaration and resolution (#349 entry 1)
Covers: comment 6618751693 created 2026-11-04T00:07:01Z; read at 2026-11-04T00:24:12Z
Parent items: [#349](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/349), [#347](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347)
Item plan: [split adoption on #349](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/349#issuecomment-6617607805), adopting recommendations 1 to 6 of the [split proposal](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/349#issuecomment-6617362572)
Implementing tool: Claude Code. Source: no comment on the issue or its listed parents before `ready-for-agent` (applied to #347 and #349 on 2026-11-03); Claude Code by default
Approved plan: [Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/357#issuecomment-6617822860), updated_at 2026-11-03T23:34:21Z, approved under standing approval by [plan check 6618751693](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/357#issuecomment-6618751693) (`Blocking findings: none`, open questions None)
Decisions in force:
- Issue body: allowlist acks and linger.ms for LoadGenProducer, add B06 and B07 as ledger-run items, the option A throughput measure entry, scenario files and contract text; no verifier or workflow change. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/357)
- Standing authority: points 1 to 6 on #347 and its children. Source: [decision 6617257796](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6617257796)
- Split of B06 and B07 into three children, documented values only (B06 acks -1 and 1; B07 linger.ms 5 and 0), option A throughput measure, own C0 per item. Source: [decision 6617607805](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/349#issuecomment-6617607805)
- This child adopts the standing authority and the split in full. Source: [decision 6617624359](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/357#issuecomment-6617624359)
- The resolver baseline parse changes to `NumberStyles.AllowLeadingSign`; the tests show `-1` resolves and a non-numeric baseline is still rejected. Source: [decision 6618177974](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/357#issuecomment-6618177974)
Open questions:
- None
Next action: #349 entry 2: shared-run admission, smoke only.

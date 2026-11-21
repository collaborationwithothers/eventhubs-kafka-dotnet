# State: #510 C10 and C01 scenarios and expected outcomes (#499 entry 7)
Covers: comment 7057936867 created 2026-11-21T05:28:07Z; read at 2026-11-21T05:43:27Z
Parent items: #499
Item plan: [#499 proposal revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894560135)
Implementing tool: Claude Code. Source: [Tool: Claude comment 7057936867](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-7057936867), created 9 seconds before `ready-for-agent` was applied
Approved plan: [c10-c01-scenarios plan, revision 6](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-7000962800), `items/510/plan-c10-c01-scenarios.md` at 975b93ba0aa3221027c9c69c543880e93e5b7436; three pull requests, this one is `self-kill-archive` (section 16)
Decisions in force:
- The issue body (scope and read-first list; its K-57-under-C10 line is amended by decision 7031523104 below, and its "Implementation: Codex" line is overridden by the tool-naming comment on line 5). Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510)
- Adopts the #499 consumer-processing grilling answers and the standing decision. Source: [6895329362](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-6895329362), [6895344271](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-6895344271), [6895538516](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-6895538516)
- Adopts the settings-source answer and the consumer phase judge entry. Source: [6899940730](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-6899940730)
- Agents merge by merge commit. Source: [6958522754](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-6958522754)
- All recommended answers to the plan revision 2 handoff; point 6 makes a missing native-monitoring index of a self-killed C10 or C01 subject the expected absence `configured_self_kill`, in its own pull request. Source: [7031523104](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-7031523104)
- Approves the revision 3 text of both `expected.yaml` files. Source: [7032156848](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-7032156848)
- Commit evidence for C10 comes from verifier output, as an added pull request. Source: [7033913610](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-7033913610)
- Mutation-runtime risk accepted for `c10-c01-inputs` and `c10-commit-coverage` only, not for `self-kill-archive`. Source: [7034587780](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-7034587780)
- Approves plan revision 6 at the commit above; check P2-1 to P2-3 are `apply in implementation`. Source: [7034786418](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-7034786418)
Open questions:
- None
Next action: Codex review and merge of the `self-kill-archive` pull request. The set is `c10-c01-inputs`, `self-kill-archive`, `c10-commit-coverage`; the plan allows any order, and all three merge before the smoke runs.

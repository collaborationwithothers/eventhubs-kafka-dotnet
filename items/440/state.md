# State: #440 Order verification gates so cold review findings do not invalidate full runs
Covers: comment 6856461667 created 2026-11-13T14:51:35Z; read at 2026-11-13T14:55:00Z
Parent items: none
Item plan: none
Implementing tool: Claude Code. Source: no comment on the issue or its listed parents before `ready-for-agent` (applied 2026-11-13T07:12:10Z); Claude Code by default.
Approved plan: [gate-order contract-grade plan, r2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6854384408), commit b76e0ba1db16b36b6ce418dd43b318af73784457, approved under standing approval by [plan check 6856461667](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6856461667), which states `Blocking findings: none`, under [decision 6856109826](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6856109826).
Decisions in force:
- Issue body: proposes four gate and control-session rule changes after the pull request 439 evidence. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440)
- Proposals 1 and 2 adopted: gate order, pre-PR local gate without `Tier=CGroupV2`, hosted full gate pre-merge; proposals 3 and 4 not decided. Source: [decision 6847972415](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6847972415)
- The pre-PR local gate reruns only for changes made before the pull request opens; later corrections need rule 8 coverage and the hosted gate; the plan check's citation texts apply; apply its P2s, get a fresh check, implement under standing approval. Source: [decision 6856109826](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6856109826)
Open questions:
- None
Next action: cold review of the staged diff, then the pre-PR local gate (`./scripts/verify-dotnet.sh --tier sandbox`) on the reviewed source, push, Codex review, hand off to Hari for merge (protected paths).

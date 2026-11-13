# State: #448 Clean up -wip backup branches that cloud sessions cannot delete
Covers: comment 6849532735 created 2026-11-13T09:45:44Z; read at 2026-11-13T09:54:45Z
Parent items: none
Item plan: none
Implementing tool: Codex. Source: [tool assignment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/448#issuecomment-6848100009), before `ready-for-agent` was reapplied at 2026-11-13T08:39:54Z.
Approved plan: [Backup cleanup contract-grade plan, r2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/448#issuecomment-6848363584), commit ba3f821b9099cad4fd6f4d74acc22af5f0bc177b, approved by [Hari](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/448#issuecomment-6849369765).
Decisions in force:
- Issue body: preserve session backups, add manual safe cleanup and the rule 12 amendment, check protection and measure one cloud dispatch on main after merge. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/448).
- Delete only backups whose observed heads are ancestors of protected main; a matching branch alone cannot authorize deletion. Source: [main-only decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/448#issuecomment-6849056421).
- Implement checked plan r2 at its pinned commit, with runtime protection evidence and fail-closed behavior. Protected merge and the post-merge dispatch sequence remain required. Source: [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/448#issuecomment-6849369765).
- The later decision addressing r1 repeats main-only ancestry, retention of matching-branch-only backups, an independent check and approval naming the revision SHA. The current r2 check and approval identify that revision. Source: [later decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/448#issuecomment-6849532735).
Open questions:
- None
Next action: obtain cold and hosted reviews of the verified r2 implementation, then hand the protected merge to Hari. Measure the single main dispatch only after merge.

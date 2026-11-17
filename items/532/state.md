# State: #532 Mutation projection capacity repair

Covers: decision 6939888191, read on 2026-11-17.
Parent item: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494
Task brief: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/532
Approved exact amendment: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/532#issuecomment-6939888191
Proposal: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/532#issuecomment-6926839845

The parent continuation authorizes the separate prerequisite, preserving mutation gates: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6901622458 . The CI-only verification decision applies: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6909056894 . The issue-specific decision accepts reuse of runtime estimates only for the exact capacity-and-rejected-setup-diagnostic script hash pair; it does not cover native telemetry source changes or authorize a dispatch.

Current main 859eb3d2 is integrated. The source capacity and diagnostic correction are authored, with boundary and real-repository fixtures. The exact version 2 exception and honest current-hash assessments are authored; original measured maps, target records and timing evidence remain. No duplicated local tests, builds or checkers are run; final hosted CI must establish validity.

Cold-review budget is cumulative: one comprehensive review completed at 89bccbac59a5a208928f5763fe779b8eb4b4b7c2 against 859eb3d28057fe02d27ed6015a14b8d7eeea93aa, finding one P1 diagnostic omission. Its correction at 99e1bb18 and the subsequently approved provenance exception need consolidated second comprehensive coverage. No focused round has started. No review budget is reset.

Next action: finish the exact exception and regressions, freeze the full change, complete the remaining comprehensive review, publish at the first authorized checkpoint, and use hosted CI/code/security reviews. Documentation is protected, so Hari merges the prerequisite when all gates pass. The later full main-only mutation acceptance remains required; no successful mutation run or issue closure is claimed.

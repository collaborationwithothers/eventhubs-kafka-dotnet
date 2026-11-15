# State: #520 Mutation timing provenance prerequisite

Covers: comment 6899300806 created 2026-11-15T18:37:24Z; read at 2026-11-15T18:43:03.346518+00:00
Parent items: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494
Item plan: parent R3 specification https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/f96eab914d00c06484431ff67a1808fb331c7e13/items/494/plan-observability-evidence.md
Implementing tool: Codex. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/520#issuecomment-6899300806
Approved plan: none; this prerequisite uses the direct task authorization listed below
Decisions in force:
- Authorize the separate prerequisite repair under its stated scope, preserving gates and execution bounds; this is not approval of a broader mutation-policy change. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6899300237
- Repair per-estimate timing provenance without rewriting historical measurements, renewing a changed target, broadening exceptions or weakening existing gates. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/520
- Adopt the parent authorization and Codex/Terra-high implementation assignment. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/520#issuecomment-6899300806
Open questions:
- This generic checker repair is not the archive PR's changed-target measurement or qualification; that PR still needs its own supported evidence and current check. Source: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6899300237
Next action: finish focused validation and cold review, publish the separate prerequisite PR, run required hosted checks and leave protected merge to Hari. Preserve archive implementation independently.

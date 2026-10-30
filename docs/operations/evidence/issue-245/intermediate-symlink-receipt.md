# Issue #245: intermediate symlink implementation receipt

## Execution card

- **Deliverable:** Implement only `projected-token-intermediate-symlinks`, Revision 2: component-wise symlink resolution for projected token metadata, its fixed offline tests, the exact operator-documentation update, and this receipt. Authority: approved plan [#245 comment 6517999117](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6517999117), independent check [6518218269](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6518218269), explicit specification approval [6518655244](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6518655244), and current direct Codex implementation authorization [6518957471](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6518957471). Current issue body `updated_at`: `2026-10-30T08:57:38Z`; full comment snapshot through ID 6518957471 was refreshed before edits. The current authorization retains `ready-for-agent` and the existing `Tool: Codex` assignment; it resolves the historical ordering concern without a label change.
- **Scope and deferrals:** Follow the seven approved paths and closed checks exactly. Preserve metadata-only operations, sanitized output and authentication contracts. Live renewal/rotation experiments, exchange/rotation provenance, longer observation windows, Entra-unreachable faults, federated-credential deletion, dispatch, Azure actions and protected paths remain deferred or unauthorized.
- **Mandatory gates:** Pinned SDK/runtime source verification; actual SDK/runtime record; `./scripts/verify-dotnet.sh`; `npm ci --ignore-scripts`; `npm run check:docs`; `git diff --check`. Report new Unit and Integration test names and outcomes from the checked xUnit XML using a script; do not commit generated reports. Verifier predicate mapping: not applicable (no verifier changes).
- **Exact commands:**
  ```sh
  dotnet --version
  dotnet --list-runtimes
  ./scripts/verify-dotnet.sh
  npm ci --ignore-scripts
  npm run check:docs
  git diff --check
  ```
- **Earliest authorized publication checkpoint:** Only after all mandatory local gates pass, documentation warnings are dispositioned, and complete applicable clean cold-review coverage exists. Hosted CI and hosted code/security review remain required gates after publication; final-head CI and hosted reviews remain coordinator-owned. This implementing session will not publish.
- **Cumulative cold-review budget:** At most two comprehensive and two focused rounds for this PR across sessions. Complete every started report before corrections and consolidate fixes by root cause. At exhaustion, preserve unpublished `-wip` and hand off to Hari under AGENTS.md rule 8; no third comprehensive round without Hari's decision.

## Implementation evidence

Pending implementation and mandatory verification. Source revision and test mapping will be recorded below after the fixed acceptance suite completes.

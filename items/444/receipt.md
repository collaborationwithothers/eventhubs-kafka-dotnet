# Authored receipt: #444

## Execution card
- Deliverable: one null check, `LocalKafkaFixture.RequireDockerEndpoint`, used by the fixture and by `B10OfflineImageRun`; two unit tests.
- Authorization: [plan Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/444#issuecomment-6846973892) at commit 32d9c311194d3492fc39ffea2a05fbf3a7a43bf4 (`items/444/plan-docker-unreachable-message.md` on `claude/444-plan`), approved by [plan check 6847065817](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/444#issuecomment-6847065817) under factory.md step 3 "Standing approval". No later decision exists.
- Gates: plan acceptance A1 to A5; cold review under rule 8 (2 comprehensive, 2 focused; exhaustion: push -wip and hand off to Hari); Codex review of the head commit.
- Predicate map: plan section 7 covers both sides of the single null condition.

## Plan check non-blocking findings (implementer choices)
- Finding 2 (struct boxing): `detected` is declared as the interface in the R4 test, as plan section 7 has it.
- Findings 1, 3, 4, 5: no code change. The pointer comment's broken link and footer are comment text only; its path and SHA are correct.

## Standing fixture rule (plan section 6)
Existing tests changed: 0.

## Commands and results (source `origin/main` 675f837, working tree of this branch)
- Build `dotnet build tests/Parchwell.LoadGen.Tests -warnaserror`: 0 warnings, 0 errors.
- A2: `--filter-class ...LocalKafkaFixtureDockerEndpointTests`: total 2, succeeded 2, skipped 0, no Docker daemon.
- A3: no `/var/run/docker.sock`, `docker version` fails. `--filter-namespace Parchwell.LoadGen.Tests.Abrupt --filter-trait "Tier=Docker"`: total 4, failed 3, succeeded 1. The message appears 3 times. `NullReferenceException` count 0.
- A1: `./scripts/verify-dotnet.sh --tier fast`: `Partial verification passed (tier fast, projects: all)`. Partial run, not the gate.
- A4 (hosted full gate on the final head) and A5: recorded in the pull request.

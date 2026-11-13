# Authored receipt: #444

## Execution card
- Deliverable: one null check, `LocalKafkaFixture.RequireDockerEndpoint`, used by the fixture and by `B10OfflineImageRun`; two unit tests.
- Authorization: [plan Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/444#issuecomment-6846973892) at commit 32d9c311194d3492fc39ffea2a05fbf3a7a43bf4 (`items/444/plan-docker-unreachable-message.md` on `claude/444-plan`), approved by [plan check 6847065817](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/444#issuecomment-6847065817) under factory.md step 3 "Standing approval". No later decision exists.
- Gates: plan acceptance A1 to A5; `npm run check:docs` before the cold review (rule 8); cold review under rule 8 (2 comprehensive, 2 focused; exhaustion: push -wip and hand off to Hari); Codex review of the head commit.
- Earliest authorized publication checkpoint: push the branch and open the pull request after a cold review round with no P0 or P1, and after the checks listed under "Commands and results" pass. Hosted CI and Codex review then run on the pushed head.
- Predicate map: plan section 7 covers both sides of the single null condition.

## Plan check non-blocking findings (implementer choices)
- Finding 2 (struct boxing): `detected` is declared as the interface in the R4 test, as plan section 7 has it.
- Finding 1 (Testcontainers line numbers in the plan text): no change. It concerns plan text only, the code the plan cites was checked by the check itself, and the implementation does not use the line numbers.
- Finding 3 (mis-cited dotnet.md rule in plan section 5): no change. It concerns plan text only; the no-retagging constraint comes from the plan's scope list, which this diff meets.
- Finding 4 (no acceptance command shows the new message for other fixture callers): no change. Those callers reach the check through `LocalKafkaFixture` construction (R2), and the plan's requirements name only the three tests of the issue.
- Finding 5 (broken link and footer in the pointer comment): no change. It is comment text only; its path and SHA are correct.

## Standing fixture rule (plan section 6)
Existing tests changed: 0.

## Commands and results (source `origin/main` 675f837, working tree of this branch)
- `dotnet build tests/Parchwell.LoadGen.Tests -warnaserror`: 0 warnings, 0 errors.
- A2: `dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --no-build -- --filter-class Parchwell.LoadGen.Tests.Component.LocalKafkaFixtureDockerEndpointTests`: total 2, succeeded 2, skipped 0, no Docker daemon.
- A3: `/var/run/docker.sock` absent and `docker version` fails. `dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --no-build -- --filter-namespace Parchwell.LoadGen.Tests.Abrupt --filter-trait "Tier=Docker"` output saved to a file: total 4, failed 3, succeeded 1. `grep -c "Docker daemon not reachable; this test is Tier=Docker"` over that file printed 3; `grep -c NullReferenceException` printed 0.
- `npm run check:docs`: no issues in `items/444/`; 9 warnings, all in files this pull request does not change (one blanket line in the pull request).
- A1: `./scripts/verify-dotnet.sh --tier fast`: `Partial verification passed (tier fast, projects: all)`. Partial run, not the gate.
- A4 (hosted full gate on the final head) and A5: recorded in the pull request.

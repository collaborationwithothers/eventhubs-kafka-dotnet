# Receipt: shorten the verification loop, pull request 1

Issue [#435](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/435). Plan: `items/435/plan-verification-tiers.md` (this pull request). Authorization: directly authorized governance work, Hari's chat instruction of 2026-11-12; no approved item plan and no plan approval is claimed. Implementing tool: Claude Code, published from branch `claude/435-verification-tiers`; the session branch `claude/modest-allen-wcadc4` holds the same commits.

## Execution card

- Deliverable: fail-late `scripts/verify-dotnet.sh` with `--tier`, `--project`, `PARCHWELL_BUILD_MAX_CPU` and stage timing; `Tier` traits on 14 test files; `scripts/test-verify-dotnet.sh`; documentation.
- Authorization: as above. Protected paths touched: `/scripts/`, `/docs/`, `*.csproj` pattern not touched, `Parchwell.Verifier*/` test files. Hari merges.
- Mandatory gates: `./scripts/test-verify-dotnet.sh`; the fast tier in this session; the full script in hosted CI on the final head (cannot pass here, no Docker daemon); `npm run check:docs`.
- Earliest publication checkpoint: after the first comprehensive cold review has no open P0 or P1.
- Review budget: two comprehensive and two focused rounds; exhaustion hands off to Hari.
- Predicate map: not applicable. The change adds no verifier predicate. The script's own conditions are mapped in plan section 6.

## Commands and results

Tested source: commit `c846c37` (tree `c93f523ce04a5c514a05d5915f0ff28ad7583638`) for the script, tests and documentation; this receipt and the plan file may differ from it only in prose. The baseline row ran on unmodified `a5c2403`. Session: 4 cores, 15 GB, Docker client without a daemon, SDK 10.0.402, 2026-11-12. One run each; run-to-run variation was not measured.

| Command | Result |
|---|---|
| Baseline, `dotnet test` per project on unmodified `main` | Restore 7.9 s, cold build 55 s. Wall: Enrichment 2 s, Scenarios 3 s, Kafka 48 s (1 failed), Verifier 87 s (7389 tests, 13 failed), LoadGen 316 s (1808 tests, 8 failed). The 22 failures are in Docker-dependent or cgroup-dependent tests. |
| `./scripts/verify-dotnet.sh --tier fast` (`c846c37`, run after the round 1 corrections) | Exit 0. Architecture 17, Enrichment 56, Kafka 490, LoadGen 1797, Scenarios 340, Verifier 7375 passed, every report validated. Test stages: Kafka 48 s, LoadGen 52 s, Verifier 72 s. Total 10075 tests. |
| `./scripts/verify-dotnet.sh` (full gate, run before the round 1 corrections; the later changes touch only option validation order, a few explicit return statements, and prose) | Exit 2. One run reported all three failing projects: Kafka 1, LoadGen 8, Verifier 13 failures, the same 22 as the baseline; the warning control still ran and passed. Before this change the run would have stopped at Kafka. |
| `./scripts/test-verify-dotnet.sh` | Exit 0, `verify-dotnet.sh checks passed.`, about 1.5 s. |
| `PARCHWELL_BUILD_MAX_CPU=1 ./scripts/verify-dotnet.sh --project Parchwell.Kafka.Tests --project Parchwell.Scenarios.Tests` | Restore, build and evaluation passed with `-maxcpucount:1`. Kafka failed (Docker), Scenarios passed, exit 2. |
| Option errors: `--tier slow`, `--bogus`, `--project Parchwell.Nope`, `PARCHWELL_BUILD_MAX_CPU=abc` | Each exited 2 with a message, before any test. |
| `npm run check:docs` | Passed with 9 warnings, none in files this pull request changes. |

Durations behind the `Slow` marks, from the baseline run: `B10AbruptProducerTests.Given_LastControlTerminalRacesWithCut_When_RunRepeatedly_...` 142.2 s; `B10AbruptRealProcessTests.Given_ProductionHostAfterCrossAdmission_When_NoKillExits_...` 121.1 s; `VerifyInputCapacityTests.Given_FullA01AttemptInventory_When_UsingDeployedInputCapacity_...` 41.2 s. The next slowest test took 13.8 s.

Counts: fast tier removes 26 tests: Verifier 7389 to 7375 (13 Docker, 1 Slow), LoadGen 1808 to 1797 (7 Docker tests that failed here plus the offline-image test, 1 Host, 2 Slow), Kafka 491 to 490 (1 Docker). These equal the marked tests, so A1 in the plan holds on this SDK.

Negative controls for `scripts/test-verify-dotnet.sh`, each applied to a copy of the script and reverted: dropping `Tier=Slow` from the filter; removing the failure record; removing the cap from the restore call. The script test failed for each (exit 1). A fourth edit that matched nothing was discarded.

## Not verified

- The full gate has not passed in this session. It needs a Docker daemon; hosted CI provides one.
- Whether `PARCHWELL_BUILD_MAX_CPU` prevents the MSBuild worker exits recorded in `items/404/b10-warm-leader-receipt.md` (V2). Not reproduced.
- Behaviour in Codex's managed environment.
- `scripts/test-verify-dotnet.sh` is not wired into CI; changing the workflow is outside this pull request.

## Round 1 corrections

The first comprehensive cold review (5 findings at P1, 5 at P2) led to commit `c846c37`: `--project` validation moved before restore with a stub-log check that no `dotnet` call happens before an option error (negative control: validation disabled, script test failed); explicit `|| return` on the remaining fail-late steps and a corrected comment; documentation claims reworded as observed; the cross-reference to rule 12 corrected; the dropped `--tier docker` value, the branch name and the state file raised as plan questions 3 to 5. Branch name: Hari gave permission in chat on 2026-11-12 to publish from `claude/435-verification-tiers`; `--tier docker` is left out by his decision in chat the same day (plan questions 3 and 4). The pre-existing `while read` loop that gives `dotnet test` the project list as standard input is left as on `main`.

## Review record

Review mode and counts are kept in the pull request's self-review record, not here, under AGENTS.md "Before opening a pull request" rule 8.

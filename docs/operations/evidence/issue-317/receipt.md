# Issue 317 receipt

This pull request adds the LoadGen idle-observation worker for A02, A03 and A04, the three timing fields of the `idle_observation` declaration, the wire and capture contract, the chart mode with its bounds, and a phase module that runs the configurations one at a time. It judges nothing, changes no workflow, verifier or `expected.yaml`, and dispatches nothing.

## Execution card

- Deliverable: one pull request with changes 1 to 25 of the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/317#issuecomment-6564976331), Revision 2 (`updated_at` 2026-11-02T05:30:47Z), plus this receipt.
- Authorization: the plan was approved under docs/agents/factory.md "Item lifecycle" step 3 "Standing approval". The [plan check 6570016904](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/317#issuecomment-6570016904) came from a fresh subagent, was posted after the plan's latest edit, and states `Blocking findings: none`; the plan states `None` for open questions. Its five `apply in implementation` P2s are applied. Item approval is the `ready-for-agent` label on parent #301 (2026-11-01T20:30:48Z); no comment names a tool, so Claude Code. Later decisions: [6563545506](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/317#issuecomment-6563545506) (adopts the #301 decisions), [6569822377](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/317#issuecomment-6569822377) (both open questions, option A) and [6569916242](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/317#issuecomment-6569916242) (no change to this plan).
- Mandatory gates: plan acceptance 1 to 8, the cold review under AGENTS.md "Before opening a pull request" rule 8, hosted CI, Codex review and security review. The pull request touches the protected path `src/Parchwell.LoadGen/Parchwell.LoadGen.csproj` (three `Content` lines); the merge is Hari's or the #301 control session's under the [override decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563515099), point 1, as extended by decision 6569822377.
- Publication checkpoint: open the pull request after the first cold review reports no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: its review-budget handoff to Hari.

## Plan deviations and choices

- Plan check P2-1 to P2-5 applied: 45 environment names, not 46; the reworded plain-scalar sentence in `scenario-contract.md`; only the `-idle` Job name and the `ledger` container validate; the per-client worst case is 1110.4 s and the per-process worst case 1240.4 s; the host check command carries `PARCHWELL_TOOLS_DIRECTORY`.
- Change 20: the runtime values include `run_kind`, which the chart requires.
- Change 7: `Error` checks the reason size before the dropped count, so an oversized reason sets `capture_limit` even after 64 errors.
- Change 22: the contract makes no claim about why a polled producer with nothing to send sends no Produce request, because `scenario-contract.md` does not contain that text.

## Predicate map

Not applicable: no verifier predicate changes.

## Commands and results

Run on commit 31de0706a6c3957c02d3cf0871405839cd55727c (the last commit that changed code, tests or scenarios; later commits change only this receipt, `items/317/state.md` and the prose of `docs/operations/idle-observation-contract.md`), with .NET SDK 10.0.402, Helm v4.3.1 and kubeconform 0.8.0 installed in the session.

1. `dotnet build Parchwell.slnx -c Release`: 0 warnings, 0 errors.
2. `dotnet test --project tests/Parchwell.Scenarios.Tests -c Release --no-build`: 293 passed, 0 failed.
3. `dotnet test --project tests/Parchwell.LoadGen.Tests -c Release --no-build`: 815 total, 811 passed, 4 failed. The four are the Docker-dependent LocalKafka tests `RunExecutorComponentTests.Given_LocalKafka_When_RunExecutorProduces_Then_IndependentConsumerAndLedgerJoinOnExactIdentityAndHeaders`, `BlobPublicationIntegrationTests.Given_RealLocalKafkaRun_When_PublishingThroughBlobSdk_Then_IndependentReaderAcceptsExactIdentityAndArtifacts` and the two `ProbeV2ComponentTests.Given_LocalKafka_*` tests. The session has no Docker daemon. They were not run on a clean `main` checkout. The 54 idle-observation tests passed.
4. `./scripts/verify-dotnet.sh`: restore, build, Kafka XML inventory, `Parchwell.ArchitectureTests` (14 passed) and `Parchwell.Enrichment.Tests` (56 passed) passed; it stopped at `Parchwell.Kafka.Tests` with a Testcontainers error (no Docker).
5. `PARCHWELL_TOOLS_DIRECTORY=<tools> bash charts/parchwell/tests/check.sh`: passed; the idle-observation line reads 8 positive, 8 negative.
6. `PARCHWELL_TOOLS_DIRECTORY=<tools> bash charts/parchwell/tests/check-loadgen-host.sh`: passed; the idle-observation line reads 8 rendered environments, 2 missing or unknown regressions.
7. `python3 -m unittest discover -s infra/scripts/phase/tests -p test_idle_observation.py`: 11 passed. The full `infra/scripts/tests` discovery shows 24 failures and 103 errors, identical in a clean `origin/main` worktree (the sorted failure lists are equal); the session has no `helm` on the default path for that suite.
8. Acceptance 1 script: prints 1110.4, 1240.4, 75, 139811200, 27960960, 1855, 13020 and 45. Acceptance 2 diffs print nothing.
9. `npm run check:docs`: 8 warnings, all in files this pull request does not change. Re-run at commit 5899854b12fbe293ecfe49076b98ac7a73f7eacf: the same 8 warnings.
10. Acceptance 7: `git diff --name-only origin/main...HEAD` lists nothing under `.github/`, `src/Parchwell.Verifier/`, `tests/Parchwell.Verifier.Tests/`, `results/` or any `expected.yaml`; the only `*.csproj` is `src/Parchwell.LoadGen/Parchwell.LoadGen.csproj`, whose diff is the three `Content` lines.

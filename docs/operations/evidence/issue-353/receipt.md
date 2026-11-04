# Receipt: B10 retry execution (issue 353)

This pull request makes the load generator retry a timed-out send in B10 repeats only. The approved plan is Revision 2 of the [B10 retry execution plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/353#issuecomment-6625703189), approved under standing approval by the [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/353#issuecomment-6626569222). The later decisions in force are [6617623208](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/353#issuecomment-6617623208) and [6625942726](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/353#issuecomment-6625942726).

## Execution card

- Deliverable: plan changes 1 to 7, plus the item state file.
- Authorization: standing approval; both parents carry `ready-for-agent`; the predecessor merged.
- Mandatory gates: build with warnings as errors, targeted tests, documentation check, cold review, hosted CI and Codex review of the head commit.
- Verification commands: the plan's "Steps and acceptance" 2 to 6.
- Earliest publication checkpoint: after the first comprehensive cold review has no P0 or P1.
- Review budget: two comprehensive and two focused rounds; exhaustion hands off to Hari (rule 8).
- Predicate map: not applicable; this is not verifier work.

## Specification comparison

Changes 1 to 7 are applied as written, with these additions. The `apply in implementation` P2 of the plan check (the worker-sequence sentence in `loadgen-contract.md`) is applied. Change 7b's text also links the pinned Confluent.Kafka v2.16.0 `ErrorCode.cs` source where the plan has the version name alone, as the cold review asked. Change 6's helper list names `Identity`, `Profile`, `HeaderValue`, `Options` and `ScriptedProducer`. The test file also holds two helpers the plan does not name, `RunAsync` and `NewLedger`, which hold the temporary directory, ledger store and read-back steps that tests 1 to 5 share; they keep the behaviour change 6 describes. `RunAsync` passes the read-back rows to each test as `ImmutableArray<LedgerRow>`, the type `LedgerValidator.ReadValidatedAsync` returns.

## Verification (commit `a1cb245`, rebased on `main` at `f122d30`; the later commits change only this receipt)

| Command | Result |
|---|---|
| `dotnet build Parchwell.slnx -c Release` | 0 warnings, 0 errors |
| Targeted retry tests (plan step 3) | 21 total, 21 passed, 0 failed, 0 skipped |
| `git diff --name-status origin/main -- tests/` | two `A` lines, the two new test files |
| `npm run check:docs` | 8 warnings, none in `docs/operations/loadgen-contract.md` (blanket disposition: the warnings are in files this pull request does not change) |
| ASCII scan of changed files | no output |
| `dotnet test` on `Parchwell.LoadGen.Tests`, all tests | 1082 total, 1078 passed, 4 failed (the 4 Docker-dependent tests named below), 0 skipped |
| `./scripts/verify-dotnet.sh` | exit 2: `Solution build: passed`, `Parchwell.ArchitectureTests tests: passed`; `Parchwell.Kafka.Tests tests: failed` (1 of 486), and the script stops there |

The plan's step 2 evidence line `Parchwell.LoadGen.Tests tests: passed` was not produced in this session. This container has the Docker client but no Docker daemon. Four `Parchwell.LoadGen.Tests` tests, one `Parchwell.Kafka.Tests` test and twelve `Parchwell.Verifier.Tests` tests need a local Kafka broker through Testcontainers and fail for that reason. Before the rebase, on `main` at `b233149` without these changes, `Parchwell.LoadGen.Tests` ran 1027 tests with the same 4 failures; with these changes it ran 1048 (the 21 new tests added) with the same 4 failures. The 1082 above is on the rebased head; `main` at `f122d30` was not run on its own. The Kafka and Verifier failures were not re-run on `main`; `Parchwell.Verifier` does not reference `Parchwell.LoadGen`, and the Kafka failure is a `NullReferenceException` in the Docker endpoint setup. The hosted `dotnet-verification.yml` run on the pull request is the first full run of the script's evidence.

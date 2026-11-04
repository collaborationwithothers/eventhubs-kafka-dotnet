# Receipt: B06 and B07 declaration and resolution

This pull request lets a scenario vary `acks` (B06) and `linger.ms` (B07) for the load generator, and declares B06 and B07 as ledger-run items with phases C0 and C1. It adds two statistics measures for request and batch counts, and commits `scenarios/B06/scenario.yaml` and `scenarios/B07/scenario.yaml`. Neither item can be dispatched or judged after it merges.

## Execution card

- Deliverable: the ten changes of the approved plan, the item state file and this receipt.
- Authorization: [issue 357](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/357) body, [decision 6617624359](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/357#issuecomment-6617624359), [decision 6618177974](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/357#issuecomment-6618177974), the [split adoption 6617607805](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/349#issuecomment-6617607805) and the [standing authority 6617257796](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6617257796).
- Plan: [Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/357#issuecomment-6617822860), updated_at 2026-11-03T23:34:21Z, approved under standing approval by [plan check 6618751693](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/357#issuecomment-6618751693) (`Blocking findings: none`, open questions None).
- Mandatory gates: the plan's acceptance commands 1 to 5; cold review under rule 8; final-head "Dotnet verification" CI; Codex review of the head commit.
- Predicate map: not applicable (no verifier work).
- Earliest publication checkpoint: after the first comprehensive cold review has no open P0 or P1.
- Review budget: two comprehensive passes, then at most two focused rounds. Exhaustion action: push -wip and hand off to Hari (rule 8).

## Commands run (base `179667e`, steps 1 to 4 and 6 on the working tree that became commit `81dd95e`, step 5 as it states)

1. `./scripts/verify-dotnet.sh` with SDK 10.0.402 (installed from dot.net into the session scratch directory). Locked restore, solution build (warnings as errors), Kafka XML inventory consistency, architecture tests (15) and Enrichment tests (56) passed. The Kafka test step then failed on `PhantomSpikeTests`, which needs a Docker daemon (`DockerUnavailableException`); the script stops at the first failing step.
2. The remaining unit projects were run directly with `dotnet test --project <p> --configuration Release --no-build`:
   - `Parchwell.Scenarios.Tests`: 331 passed, 0 failed. `B06B07DeclarationTests` accounts for 21 of them.
   - `Parchwell.Kafka.Tests` without `PhantomSpikeTests`: 485 passed, 0 failed.
   - `Parchwell.LoadGen.Tests`: 926 passed, 4 failed. `Parchwell.Verifier.Tests`: 4993 passed, 12 failed. All 16 were classified by script from the test log and, for three, from their evidence directories. 12 fail in the test fixture's Docker endpoint helper (`get_Endpoint`, `NullReferenceException`), 1 with `DockerUnavailableException: Failed to connect to Docker endpoint at 'unix:///var/run/docker.sock'`, and 3 (`VerifierOutcomeComponentTests`) report that no trial yielded its witness, and every trial's evidence shows the same `get_Endpoint` failure. `docker info` in this session shows a client and no reachable server. Some of the 16 exercise code this pull request changes: the three `VerifierOutcomeComponentTests` and the `VerifierWindowComponentTests` build a harness or call the scenario loader and resolver for A01, which reach `IsIntegerBaseline`, `MeasurementCatalog` and `ScenarioPhases`. I checked the other classes only for direct references (none found), not transitively. I did not run the same 16 on unmodified `main`. Final-head CI, which has Docker, is the evidence for them.
3. `npm ci --ignore-scripts && npm run check:docs` exited 0 with 8 warnings, all in files this pull request does not change (`docs/agents/factory.md`, `docs/operations/evidence/issue-124`, `issue-140` and `issue-211`).
4. `git diff --check origin/main` printed nothing.
5. Run later, with HEAD at `fbb9b3a`, and repeated for `81dd95e` and `a521023` with the same result: a script compared `git diff --name-only origin/main HEAD` with the allowed list of changes 1 to 10 of the plan and printed `PATHS-MATCH`; a second pattern check found no path under `.github/`, `infra/`, `charts/` or `results/`, no `Parchwell.Verifier` file, no `*.csproj` and no `expected.yaml`. The 16 paths:
   - `docs/operations/evidence/issue-357/declaration/receipt.md`
   - `docs/operations/scenario-contract.md`
   - `docs/operations/statistics-bridge.md`
   - `docs/operations/verifier-contract.md`
   - `items/357/state.md`
   - `scenarios/B06/scenario.yaml`
   - `scenarios/B07/scenario.yaml`
   - `src/Parchwell.Kafka/Configuration/KafkaScenarioOverrideSetting.cs`
   - `src/Parchwell.Scenarios/Contracts/MeasurementCatalog.cs`
   - `src/Parchwell.Scenarios/Contracts/ScenarioPhases.cs`
   - `src/Parchwell.Scenarios/Loading/SingleActionDefinitionReader.cs`
   - `src/Parchwell.Scenarios/Resolution/ScenarioResolver.cs`
   - `tests/Parchwell.Kafka.Tests/Configuration/ScenarioOverrideMetadataTests.cs`
   - `tests/Parchwell.Scenarios.Tests/B06B07DeclarationTests.cs`
   - `tests/Parchwell.Scenarios.Tests/ScenarioPhasesTests.cs`
   - `tests/Parchwell.Scenarios.Tests/ScenarioValidationBoundaryTests.cs`
6. An ASCII scan (`grep -P '[^\x00-\x7F]'`) of every changed text file printed nothing.

## Scope notes

- The `IsIntegerBaseline` expected values were first executed in step 2; all 13 rows passed.
- Known stale message for entry 3: `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs:27` says "Verifier:ScenarioId must be A01 or B04 for C5 modes." That file is protected and outside this issue, and `ScenarioPhases` now also admits B06 and B07.
- `Resolve` throws `invalid_baseline_value` whenever `IsIntegerBaseline` returns `false` (`ScenarioResolver.cs`, the Integer branch). A non-numeric baseline cannot reach `Resolve` from scenario input or a test, so the rejection half is tested at `IsIntegerBaseline`, as decision 6618177974 point 2 and the plan state.
- The statistics fields `stats.broker.req.produce` and `stats.topic.batchcnt` are declared only. No collector reads them. Whether Event Hubs emits them is unknown until entry 2's smoke run.

## Cold review

- Round 1 (comprehensive, 1 of 2): input commit `81dd95e` against merge base `179667e`. Reported 3 P1 and 2 P2, all fixed in one consolidated correction: the state file read time (now the real time of the authority refresh), the Docker attribution above (now classified for all 16), the inferred label and counting lines in `statistics-bridge.md`, the commit this receipt describes, and these counts.
- Round 2 (comprehensive re-review, 2 of 2): input `fbb9b3a`, comparison `81dd95e`. Reported 2 P1 and 1 P2, all fixed in one consolidated correction: the false "none of the 16" sentence, the missing source for the `Produce` key name, and the exact path list with the script check.
- Cumulative before the first focused round: 2 comprehensive, 0 focused. Later rounds are recorded in the pull request description.

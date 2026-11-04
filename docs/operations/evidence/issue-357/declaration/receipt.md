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

## Commands run (source commit `179667e`)

1. `./scripts/verify-dotnet.sh` with SDK 10.0.402 (installed from dot.net into the session scratch directory). Locked restore, solution build (warnings as errors), Kafka XML inventory consistency, architecture tests (15) and Enrichment tests (56) passed. The Kafka test step then failed on `PhantomSpikeTests`, which needs a Docker daemon (`DockerUnavailableException`); the script stops at the first failing step.
2. The remaining unit projects were run directly with `dotnet test --project <p> --configuration Release --no-build`:
   - `Parchwell.Scenarios.Tests`: 331 passed, 0 failed. `B06B07DeclarationTests` accounts for 21 of them.
   - `Parchwell.Kafka.Tests` without `PhantomSpikeTests`: 485 passed, 0 failed.
   - `Parchwell.LoadGen.Tests`: 926 passed, 4 failed. `Parchwell.Verifier.Tests`: 4993 passed, 12 failed. Every failure is a component test that starts a local Kafka broker through Testcontainers. The `VerifierOAuthComponentTests` failure was re-run alone and reported `DockerUnavailableException: Failed to connect to Docker endpoint at 'unix:///var/run/docker.sock'`. The other 15 were not each inspected; they sit in classes named `*Component*`, `*Integration*` and `ProbeV2Component`, which run a local broker. These tests did not run here. Final-head CI, which has Docker, is the evidence for them.
3. `npm ci --ignore-scripts && npm run check:docs` exited 0 with 8 warnings, all in files this pull request does not change (`docs/agents/factory.md`, `docs/operations/evidence/issue-124`, `issue-140` and `issue-211`).
4. `git diff --check origin/main` printed nothing.
5. `git diff --name-only origin/main` listed 16 paths, each named by changes 1 to 10 of the plan: three files under `docs/operations/`, `items/357/state.md`, this receipt, `scenarios/B06/scenario.yaml`, `scenarios/B07/scenario.yaml`, five files under `src/` and four under `tests/`. No path is under `.github/`, `infra/`, `charts/` or `results/`, no `Parchwell.Verifier` file, no `*.csproj` and no `expected.yaml`.
6. An ASCII scan (`grep -P '[^\x00-\x7F]'`) of every changed text file printed nothing.

## Scope notes

- The `IsIntegerBaseline` expected values were first executed in step 2; all 13 rows passed.
- Known stale message for entry 3: `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs:27` says "Verifier:ScenarioId must be A01 or B04 for C5 modes." That file is protected and outside this issue, and `ScenarioPhases` now also admits B06 and B07.
- `Resolve` throws `invalid_baseline_value` whenever `IsIntegerBaseline` returns `false` (`ScenarioResolver.cs`, the Integer branch). A non-numeric baseline cannot reach `Resolve` from scenario input or a test, so the rejection half is tested at `IsIntegerBaseline`, as decision 6618177974 point 2 and the plan state.
- The statistics fields `stats.broker.req.produce` and `stats.topic.batchcnt` are declared only. No collector reads them. Whether Event Hubs emits them is unknown until entry 2's smoke run.

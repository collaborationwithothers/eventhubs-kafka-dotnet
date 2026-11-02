# Issue 329 receipt

This pull request sets `metadata.max.age.ms` and `topic.metadata.refresh.interval.ms` to `900000` in every A02 and A04 configuration, so neither librdkafka metadata timer is due inside the 420 s idle period. It changes the two scenario files, the loader's fixed deltas, the override policy's inventory tags, the scenario contract, the two expected-outcome files and their tests. It dispatches nothing and publishes no verdict.

## Execution card

- Deliverable: one pull request with changes 1 to 10 of the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/329#issuecomment-6588394554), Revision 2 (`updated_at` 2026-11-02T21:18:23Z).
- Authorization: the plan was approved under docs/agents/factory.md "Item lifecycle" step 3 "Standing approval". The [plan check 6588805136](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/329#issuecomment-6588805136) came from a fresh subagent, was posted after the plan's latest edit, and states `Blocking findings: none`; the plan states `None` for open questions. Item approval is the `ready-for-agent` label on parent #301 (2026-11-01T20:30:48Z); no comment names a tool, so Claude Code. Later decisions: [6588207947](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/329#issuecomment-6588207947) and [6588671289](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/329#issuecomment-6588671289); the plan's Revision 2 applies the second.
- Mandatory gates: plan acceptance 1 to 8, the cold review under AGENTS.md "Before opening a pull request" rule 8, hosted CI, Codex review and security review. The pull request changes the protected `scenarios/*/expected.yaml`; the merge is Hari's or the #301 control session's under the [override decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563515099).
- Publication checkpoint: open the pull request after the first cold review reports no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: its review-budget handoff to Hari.

## Plan deviations and choices

None in the changes: the plan's edits applied by an exact-once replace script and each anchor matched. The plan's two non-blocking P2s from the plan check are not applied: the CODEOWNERS wording sits in plan text only, and `docs/operations/kafka-configuration-explained.md` line 47 is outside the plan's files and the check did not mark it `apply in implementation`. The wording in that file, that only A03 C2 overrides `topic.metadata.refresh.interval.ms`, is therefore now incomplete; it is left for a later change.

## Predicate map

- Loader configuration check: an added C2, a removed C1, a missing or extra delta in A02 or A04, and a changed value (900001, 600000) are each rejected by their own row; every row keeps the other fields valid.
- A03 is unchanged.

## Commands and results

Run on commit 8a0c1b366d756a1490481b851a97a529178cf1ed (all code, tests and docs; the state file and this receipt are added after it), with .NET SDK 10.0.402.

1. `./scripts/verify-dotnet.sh`: locked restore, solution build, Kafka XML inventory, `Parchwell.ArchitectureTests` (15 passed) and `Parchwell.Enrichment.Tests` (56 passed) passed. `Parchwell.Kafka.Tests` failed on one test, `PhantomSpikeTests`, which starts real Kafka clients. It fails the same way on an unmodified `origin/main` checkout (NullReferenceException, 1 of 1), so the failure is not from this change. The script stopped there. Run individually instead: `Parchwell.Kafka.Tests` without that test, 481 passed; `Parchwell.Scenarios.Tests`, 297 passed; `Parchwell.Enrichment.Tests`, 56 passed. `Parchwell.LoadGen.Tests` (4 failed of 815) and `Parchwell.Verifier.Tests` (12 failed of 4976) fail only in tests that run a local Kafka (names contain LocalKafka or RealHandler); there is no Docker daemon in this session. They were not run on a clean `main` checkout.
2. Acceptance 2: `IdleObservationDefinitionTests` 58 passed, 0 failed, 0 skipped; the base on `origin/main` is 54 (+4).
3. Acceptance 3: `ScenarioOverrideMetadataTests` 48 passed, 0 failed.
4. Acceptance 4: Verifier `IdleObservation` 305 passed; LoadGen `IdleObservation` 54 passed; no test file changed in either project.
5. Acceptance 5: `npm ci --ignore-scripts`, `npm run test:docs` and `npm run check:docs` exit 0; 8 warnings, none in a changed file.
6. Acceptance 6: `git diff --name-only origin/main...HEAD` lists the ten plan paths plus this receipt (eleven; the receipt is required by rule 8).
7. Acceptance 7: the plan's script prints `ok`.
8. Acceptance 8: the ASCII scan prints nothing.

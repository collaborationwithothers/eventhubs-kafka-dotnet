# Issue 316 receipt

This pull request adds the closed `idle_observation` declaration for A02, A03 and A04, the override-policy entries for their deltas, one new baseline key and the three `scenario.yaml` files. It runs nothing and declares no timing field. It changes one literal in a protected verifier test, as decision 6564185671 requires.

## Execution card

- Deliverable: one pull request with changes 1 to 14 of the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/316#issuecomment-6563709522), Revision 2 (`updated_at` 2026-11-01T21:38:39Z), plus this receipt.
- Authorization: Hari told the session in chat to approve and implement Revision 2; the session recorded that as [approval 6564337374](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/316#issuecomment-6564337374). The [plan check 6564289566](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/316#issuecomment-6564289566) states `Blocking findings: none`; its five `apply in implementation` P2s are applied. Later decisions: [6563545330](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/316#issuecomment-6563545330) (adopts the #301 decisions), [6564185671](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/316#issuecomment-6564185671) and [6564220623](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/316#issuecomment-6564220623). Handoff [6564344142](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/316#issuecomment-6564344142) from a stopped session says it did not post 6564220623 and Hari did not confirm it in that chat; its saved branch `claude/issue-316-idle-observation-declaration-wip` was not used.
- Mandatory gates: plan acceptance 1 to 8, the cold review under AGENTS.md "Before opening a pull request" rule 8, hosted CI, Codex review and security review. The pull request touches the protected path `tests/Parchwell.Verifier.Tests/Input/GoldenArtifactReaderTests.cs`; the merge is Hari's or the #301 control session's under the [override decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563515099), point 1, as extended to #316.
- Publication checkpoint: open the pull request after the first cold review reports no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: its review-budget handoff to Hari.

## Plan deviations

- Change 5: analyzer CA1854 rejected `ContainsKey` followed by an indexer in the shape check. The shape check now uses `TryGetValue` for `action` and `role`; the predicate is unchanged.
- Plan check P2-1 to P2-5 applied: change 7 applied at the true text (`ScenarioResolver.cs` lines 62-63 and 96); change 11b joins the new elements after the `debug` element's `});` becomes `},`; the two `scenario-contract.md` cross-references are extended; "Microsoft's librdkafka configuration table does not list it" replaces the broader claim in 13a and 13b; acceptance 6 counts six changed `src/` files.

## Predicate map

The plan's "Enforced checks" table is the map. Every row has a test in `tests/Parchwell.Scenarios.Tests/IdleObservationDefinitionTests.cs` or `tests/Parchwell.Kafka.Tests/Configuration/ScenarioOverrideMetadataTests.cs`: checks 1 to 7 by the committed-file theory and the mutated-file theory (each mutation asserts one occurrence of its `before` text), checks 8, 9 and 11 by the key theory, and check 10 by `Given_IdleKeyOnVerifierOrOmitOnIntegerKey_When_Loading_Then_Rejects` and the per-entry role assertions in `ScenarioOverrideMetadataTests`, at below, at and above each bound.

## Commands and results

Branch cut from `main` at `8d225aae4c1a6ad92024098beebf68d0389c797d`. Items 1 to 5 ran on the tree of commit `08c9a02`; `items/316/state.md` and this receipt changed afterwards and no code, test, fixture or scenario file did. Items 6 and 8 ran on commit `804afcb`, which holds the final state file. .NET SDK 10.0.402 was installed with the official install script into a scratch directory.

1. Change 9 fixture script: printed `capabilities.json 29160 38d8a1044a1d5e8042799e138d899f23bea011a8b9e1c37fea69364af1b3319a` and `index.json 932 7f01ddd43037fba50e3004eba401d75e62bd4b2a570c690a295204ed00e5e54f`, equal to the plan's two lines. `GoldenArtifactWriterTests` and the golden reader tests are not among the failures below.
2. Acceptance 4 script printed `remarks copied`.
3. `./scripts/verify-dotnet.sh`: first run failed the build on CA1854 (deviation above). Second run: locked restore, solution build, Kafka XML consistency (62 declarations), architecture tests (14 passed) and Enrichment tests (56 passed) passed. `Parchwell.Kafka.Tests` stopped the script with 1 failure of 482: the phantom-spike experiment, which needs a Docker daemon (`ExperimentDockerAuthentication.get_Endpoint` null reference). This container has no Docker daemon.
4. Because the script stops there, the remaining suites ran directly with `dotnet test --project ... -c Release --no-build`: `Parchwell.Scenarios.Tests` 286 passed, 0 failed. `Parchwell.LoadGen.Tests` 757 passed, 4 failed, all named `Given_LocalKafka...` or `Given_RealLocalKafkaRun...`. `Parchwell.Verifier.Tests` 4659 passed, 12 failed, all in `Component` tests whose message is "Failed to connect to Docker endpoint". These 17 failures are environmental; they are not verified on `main` here. The pull request's `dotnet-verification.yml` run on the head is the evidence for them.
5. `./scripts/verify-kafka-coverage.sh` was not run here. The CI coverage step is the evidence for plan acceptance 2.
6. `npm run check:docs`: `8 warnings`, none in the changed documents or `items/316/state.md`; all are in files this change does not touch.
7. Plan acceptance 5: `sha256sum` of the committed files printed `38d8a1044a1d5e8042799e138d899f23bea011a8b9e1c37fea69364af1b3319a` for `capabilities.json` and `7f01ddd43037fba50e3004eba401d75e62bd4b2a570c690a295204ed00e5e54f` for `index.json`. Acceptance 7: `git diff origin/main...HEAD -- tests/Parchwell.Verifier.Tests` shows one removed and one added line. Acceptance 8: the `grep` printed nothing and exited 1. Acceptance 3: `npm run check:docs` exited 0 (item 6).
8. `git diff --check origin/main...HEAD`: no output. `git diff --name-only origin/main...HEAD` lists 21 paths before this receipt.

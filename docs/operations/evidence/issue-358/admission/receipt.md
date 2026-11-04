# Receipt: B06 and B07 shared-run admission and smoke

This pull request adds a `ledger-batch` choice to the Scenario workflow. With run kind `smoke` and items `all`, it runs one repeat of each B06 and B07 configuration in the existing `batch` job and writes a summary of each producer's request counts, batch counts and delivery latency. It also lets the chart schema, the two worker images and the phase script accept B06 and B07. The smoke itself is dispatched after merge and is not part of this pull request.

## Execution card

- Deliverable: changes 1 to 11 of the approved plan, the two plan-check findings marked `apply in implementation`, the item state file and this receipt.
- Authorization: [issue 358](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/358) body, [decision 6617624626](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/358#issuecomment-6617624626), [decision 6620065964](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/358#issuecomment-6620065964), [decision 6623554708](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/358#issuecomment-6623554708), [decision 6623742499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/358#issuecomment-6623742499), the [split adoption 6617607805](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/349#issuecomment-6617607805) and the [standing authority 6617257796](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6617257796).
- Plan: [Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/358#issuecomment-6619882968), updated_at 2026-11-04T05:46:46Z, approved under standing approval by [plan check 6624072067](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/358#issuecomment-6624072067) (`Blocking findings: none`, open questions None). Its two P2 findings marked `apply in implementation` are applied in `charts/parchwell/tests/test_chart.py`.
- Mandatory gates: the plan's acceptance commands 1 to 10; cold review under rule 8; final-head "Dotnet verification" CI; Codex review of the head commit; Hari's merge (`.github/workflows/` is protected).
- Predicate map: not applicable (no verifier source change).
- Earliest publication checkpoint: after the first comprehensive cold review has no open P0 or P1.
- Review budget: two comprehensive passes, then at most two focused rounds. Exhaustion action: push -wip and hand off to Hari (rule 8).

## Commands run (base `cbbff70`, working tree that became commit `dadc7a0`)

Tools: `install-tools.sh ci` with Python 3.12 first on `PATH` (the script requires CPython 3.12), .NET SDK 10.0.402 from dot.net.

1. `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'`: `Ran 236 tests ... OK` (230 on `main`; six new tests).
2. `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`: `Ran 284 tests ... OK`.
3. `python3 -m unittest discover -s scripts/tests -p test_azure_cli_session.py`: `Ran 38 tests ... OK`.
4. `bash infra/scripts/lifecycle/check-offline.sh` with `PARCHWELL_TOOLS_DIRECTORY` set: exit 0.
5. `cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt`: no output, exit 0.
6. `./scripts/verify-dotnet.sh`: locked restore, build with warnings as errors, inventory consistency, architecture tests (15 passed, including `ChartContractTests` and `WorkflowAuthenticationContractTests`) and Enrichment tests (56 passed) passed. The Kafka test step then failed on 1 of 486 tests, `PhantomSpikeTests`, which needs a Docker daemon; the script stops at the first failing step. Then, run directly with `dotnet test --project <p> --configuration Release --no-build`:
   - `Parchwell.Scenarios.Tests`: 331 passed, 0 failed.
   - `Parchwell.LoadGen.Tests`: 991 passed, 4 failed. `Parchwell.Verifier.Tests`: 4993 passed, 12 failed. Each of the 16 is a test whose name says local Kafka or a component harness (`RunExecutorComponentTests`, `BlobPublicationIntegrationTests`, `ProbeV2ComponentTests`, `VerifierOAuthComponentTests`, `VerifierOutcomeComponentTests`, `VerifierWindowComponentTests`), and the logs contain 16 Docker endpoint matches (`get_Endpoint`, `DockerUnavailable`, `docker.sock`) in the two logs together; I did not classify each of the 16 individually. The #357 receipt reports the same counts, 4 and 12, and names only `VerifierOutcomeComponentTests`, `VerifierWindowComponentTests` and `PhantomSpikeTests` among its failures. This pull request changes no `.cs` file under these projects, only two `Content` lines in each project file. I did not run the 16 on unmodified `main` in this session. Final-head CI, which has Docker, is the evidence for them.
7. `find src/Parchwell.LoadGen/bin src/Parchwell.Verifier/bin -path '*/scenarios/B0[67]/scenario.yaml'` after step 6: the four pairs are present: `src/Parchwell.LoadGen/bin/Release/net10.0/scenarios/B06` and `B07`, and `src/Parchwell.Verifier/bin/Release/net10.0/scenarios/B06` and `B07`.
8. `git diff --name-only origin/main...HEAD | sort`: 15 paths at `dadc7a0`; with this receipt, the plan's 16 (a script compares the list).
9. `npm ci --ignore-scripts && npm run check:docs` exited 0 with 8 warnings, all in files this pull request does not change (`docs/agents/factory.md`, `docs/operations/evidence/issue-124`, `issue-140` and `issue-211`). `docs/operations/ledger-batch-smoke.md` has no issues.
10. An ASCII scan (`grep -P '[^\x00-\x7F]'`) of every changed text file printed nothing. `git diff origin/main...HEAD` contains no at-sign codex string.

11. Added after final-head CI at `194351b` failed the "Lifecycle verification" job: `python3 infra/scripts/lifecycle/command.py 900 -- bash charts/parchwell/tests/check-loadgen-host.sh` failed at `assert len(cases)==7`, because change 9a adds four loadgen positive cases. The plan and its check missed this script, which runs from `check-loadgen-host.sh` and not from `check.sh`. The fix changes the expected count to 11 (and the printed count) in `charts/parchwell/tests/test_loadgen_host.py`. It is stricter, not looser: the published load generator host now also validates the four B06 and B07 rendered environments. After the fix the command exited 0 locally and printed `loadgen host check passed: 11 rendered environments, 1 missing-setting regression`. That file is the 17th changed path, outside the plan's 16 and its acceptance step 8; the plan authorizes no such path, so this is recorded for Hari's attention.

## Scope notes

- Unchanged by this pull request: every verifier source file, every `expected.yaml`, `results/`, and every workflow other than `scenario.yml`.
- Workflow events are `workflow_dispatch` and `schedule` as before; only the `scenario` choice list gains a value (decision 6623554708, point 1).
- The plan-check's estimate corrections (worst-case run time 14880 s, about 4.1 h, estimate of 2026-11-04; three citations off by one or two lines) change no edit text. A dispatch report that quotes the worst case uses 14880 s.
- Whether Event Hubs reports `req.Produce` and `batchcnt`, and whether the verifier accepts the two `stats.` measures, are what the smoke observes.

## Cold review

- Round 1 (comprehensive, 1 of 2): input is the staged diff against merge base `cbbff70` (commit `dadc7a0` plus this receipt). Specification sources: the approved plan Revision 2, plan check 6624072067 and decisions 6617624626, 6620065964, 6623554708 and 6623742499 listed above. Report: 1 P1 (receipt overstated the #357 test classes), 1 P2 (wrong-shape JSON raised `AttributeError` past `summarise`). Both fixed in one consolidated correction at the next commit: the receipt sentence, `AttributeError` added to the `summarise` handler with `test_ledger_smoke_treats_wrong_shape_json_as_malformed_evidence`. Later rounds are recorded in the pull request description.
- Round 2 (comprehensive re-review, 2 of 2): input is the staged diff against merge base `cbbff70` at the correction commit, comparison revision `08de67f` (round 1's reviewed revision). Cumulative before any focused round: 2 comprehensive, 0 focused.

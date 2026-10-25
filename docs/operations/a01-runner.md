# A01 Runner retirement

The A01 Runner and Helm values generator have been removed under [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates). Use the [scenario workflow](a01-workflow.md) and [lifecycle commands](lifecycle.md#commands) to run the infrastructure and phase Jobs.

Workers resolve the packaged scenario and profile in-process through the [scenario contract](scenario-contract.md). The [chart contract](chart-contract.md) owns deployment inputs, the [verifier contract](verifier-contract.md) owns phase judgement, and [worker image smoke](../engineering/dotnet-verification.md#worker-image-smoke) checks that both published images start.

## Validation receipt

Implementation source commit: `3a35ef37762339df41e8fe910728eca295d30aa1` (the retirement implementation; the command results below concern that source, before this receipt update). Source baseline: `94a01f41eb8786eb3a9c85db5d2c5320621d1f67`. Work follows [approved C7 Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/164#issuecomment-6401969618) and its [scope decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/164#issuecomment-6402001247).

The session used SDK 10.0.402 from `/tmp/c7-164-dotnet` and installed the pinned CI tools in `/tmp/c7-164-tools`. For .NET commands it set `DOTNET_ROOT=/tmp/c7-164-dotnet`; for replacement checks it set `PARCHWELL_TOOLS_DIRECTORY=/tmp/c7-164-tools`. Its command PATH began `/tmp/c7-164-tools:/tmp/c7-164-tools/venv/bin:/tmp/c7-164-dotnet`. Docker used the managed local socket after a health check, preserving proxy, CA and registry settings. No Azure dispatch or infrastructure operation was performed.

### Commands and results

| Command | Result |
| --- | --- |
| `git ls-files src/Parchwell.Runner tests/Parchwell.Runner.Tests src/Parchwell.Scenarios/Generation` after staging | Passed: no paths. |
| `rg -n 'Parchwell[.]Runner\|HelmValuesGenerator\|GeneratedValuesCompatibility\|ValuesVersion' src Directory.Build.props Parchwell.slnx tests/Parchwell.ArchitectureTests/*.csproj tests/Parchwell.ArchitectureTests/ProjectModel.cs tests/Parchwell.ArchitectureTests/packages.lock.json tests/Parchwell.Scenarios.Tests` | Passed: exit 1, no matches. The migrated negative sweeper assertion intentionally retains the forbidden Runner name. |
| `python3 /tmp/plan-pr-164/check-retirement.py` (runtime limits) | Passed: schema version, outer keys and all retained sections equal; `execution` equals the exact approved object. |
| `python3 /tmp/plan-pr-164/check-retirement.py` (lock) | Passed: only the Runner project node is removed; retained dependency versions, hashes and edges equal. The two Runner project locks are deleted. |
| `./scripts/verify-dotnet.sh` | Passed: locked restore, Release build, XML inventory, evaluated manifests, required reports and warnings-as-errors control. |
| `python3 /tmp/plan-pr-164/summarize-reports.py` | Passed: XML counts and required migrated/A01 Facts verified. |
| `bash infra/scripts/lifecycle/install-tools.sh /tmp/c7-164-tools ci` | Passed: pinned tools installed. |
| `python3 infra/scripts/lifecycle/command.py 60 -- python3 -m unittest discover -s scripts/tests -p test_azure_cli_session.py -v` | Passed: all 20 authentication cases, no failures or skips. |
| `python3 infra/scripts/lifecycle/command.py 600 -- bash infra/scripts/lifecycle/check-offline.sh` | Passed: workflow/actionlint, ShellCheck, chart acceptance and strict Kubernetes schema checks. |
| `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` | Passed: all 76 lifecycle/phase cases, no failures or skips. |
| `npm ci --ignore-scripts` | Passed. |
| `npm run check:docs` | Completed; changed Markdown files have no warnings. Warnings in unchanged historical receipts remain under AGENTS.md rule 8. |
| `git diff --check` | Passed. |

Executed .NET counts were derived from the fresh xUnit XML reports with the temporary report-summary script: Parchwell.ArchitectureTests 11, Parchwell.Kafka.Tests 349, Parchwell.LoadGen.Tests 359, Parchwell.Scenarios.Tests 91, Parchwell.Verifier.Tests 2563. Total: 3373 passed; no failed, skipped, not-run tests or runner errors. Enrichment remains an empty scaffold and claims no unit coverage. Reports are retained locally at `/tmp/parchwell-dotnet-verification.IXoVO6q8`; CI provides durable rerun evidence in the linked pull request.

The unchanged `scripts/verify-worker-images.sh` remains in the required .NET CI job. Its published image smoke evidence belongs to that CI run, linked from the pull request description; this receipt does not claim a local smoke run.

### Retained tests and retirement disclosure

The chart schema Fact and the sweeper budget/authentication Facts moved to `tests/Parchwell.ArchitectureTests/DeploymentContracts/`. Scripted comparison found their full method bodies unchanged apart from the approved repository-root expression, and found the authentication helpers and literal workflow steps unchanged. The budget Fact still checks byte equality with the installed sweeper and its `120` minute timeout. The authentication Python suite is unchanged and now runs directly in CI under its existing command deadline.

All scenario loader/resolver assertions, theory rows and retained helpers remain unchanged. The A01 data test keeps definition/profile source-path assertions and every resolved-setting assertion; only generation and generated-document checks are removed. It was renamed to `Given_ApprovedA01Data_When_LoadingAndResolving_Then_UsesOnlyDefinitionAndProfileAndResolvesEveryConfiguration`.

Accepted P1 removals are retained under [the child-adopted test-removal decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/164#issuecomment-6386307382) and [its parent decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6386306137). They remove deliberately retired behavior; the protected-path merge remains Hari's. The Runner test tree is retired as follows, with paths relative to `tests/Parchwell.Runner.Tests/`:

- Migrated rather than dropped: `ChartContractTests.cs`, `Contracts/WorkflowBudgetContractTests.cs`, and the surviving Fact/helpers in `Contracts/WorkflowAuthenticationContractTests.cs`. That last file loses only `Given_AzureCliSessionOfflineSuite_When_ExecutedWithinBoundedProcess_Then_AllAuthenticationCasesPass`, replaced by the direct suite command above.
- Contracts: `Contracts/ExecutionBudgetsTests.cs`.
- Cost: `Cost/ActualCostReconcilerTests.cs`, `Cost/AzureCliRunStoreTests.cs`, `Cost/PhaseReservationTests.cs`, `Cost/RuntimeCostBoundaryTests.cs`.
- Deployment: `Deployment/DeploymentGeneratorTests.cs`, `Deployment/DeploymentPreflightTests.cs`, `Deployment/DeploymentRuntimeLimitsTests.cs`.
- Evidence: `Evidence/CandidateContractTests.cs`, `Evidence/EvidenceExportBoundaryTests.cs`, `Evidence/EvidenceExporterTests.cs`, `Evidence/ImmutablePhaseReplayTests.cs`, `Evidence/KqlBoundaryTests.cs`, `Evidence/PhaseCandidateTests.cs`, `Evidence/PrivateProvenanceCollectorTests.cs`, `Evidence/PublicCandidateBoundaryTests.cs`, `Evidence/RecoveryExportTests.cs`, and fixture `Evidence/azure-cli-query-recording.json`.
- Execution: `Execution/AksKubeconfigBindingTests.cs`, `Execution/BoundedProcessRunnerTests.cs`, `Execution/ConsumedFailureDiagnosticsTests.cs`, `Execution/DownloadBudgetTests.cs`, `Execution/FailureArtifactPreservationTests.cs`, `Execution/FailureDiagnosticsTests.cs`, `Execution/FailureStageDiagnosticsTests.cs`, `Execution/GenerationGuardTests.cs`, `Execution/KubernetesWatchTests.AdmissionDeadline.cs`, `Execution/KubernetesWatchTests.InitialList.cs`, `Execution/KubernetesWatchTests.cs`, `Execution/NormalDownloadBudgetTests.cs`, `Execution/PhaseEvidenceBoundaryTests.cs`, `Execution/PhaseEvidenceInventoryTests.cs`, `Execution/PodAdmissionTests.cs`, `Execution/PublicationBoundaryTests.cs`, `Execution/RecoveryEvidenceTests.cs`, `Execution/RepeatAdmissionDeadlineTests.cs`, `Execution/TerraformCancellationTests.cs`, `Execution/TerraformEnvironmentTests.cs`, `Execution/WorkerBlobBoundaryTests.cs`, `Execution/WorkerOutcomeBoundaryTests.cs`.
- Rehearsal/wire pipeline: `RehearsalTests.cs`, `Support/WorkerWireFixtures.cs`, `WireFixtureBoundsTests.cs`, `WorkerPublicationFixture.cs`, `WorkerWireCompatibilityTests.cs`, `WorkerWireFixtureTests.cs`. The project file and lock file are also deleted. This retires the Runner's rehearsal fixture pipeline; surviving publication, verifier-codec and gate behavior keeps its own worker/verifier tests.

Generator retirement additionally removes `tests/Parchwell.Scenarios.Tests/GeneratedValuesCompatibilityTests.cs`, including its exact-byte compatibility and changed-leaf rejection Facts. In `ScenarioResolutionAndGenerationTests.cs` it removes only `Given_ReplacementKeyWithDifferentMappingOrder_When_Generating_Then_ProducesSameValues`, `Given_SelectedBaselineConfiguration_When_Generating_Then_ReturnsCompleteInvariantYamlWithoutOrchestratorFields`, `Given_UnknownConfigurationId_When_Generating_Then_ThrowsArgumentException` and their generated YAML constant. The DI test loses only the generator registration assertion. The A01 pipeline test loses its generation call, `AssertGeneratedValues` and generation-only YAML helpers, including their generated profile/Kafka equality checks. Scenario data and expected outcomes are unchanged.

# Issue 114 cold review 5

Frozen authored diff: `/workspace/scratch/issue-114-cold-review-5.diff`.
Diff SHA-256: `df33f9c74a0294bd725840644422806693961143f4d8120926fb0f2c178e994b`.
Raw-evidence manifest: `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/raw-manifest.json`.
Manifest SHA-256: `e1697e4d06869d477829178977bab9497764fbf39188682fd9dbec3e96cc402f`.
Review-rule baseline: `origin/main` at `4a0acc5d886a12cafdfdeafbe1e71f4d0802bf55`.

Applied `.agents/skills/repo-review/SKILL.md` to the full frozen authored scope. The only author inputs were the diff and manifest paths. The lead retained full-scope reading and cross-checking; three fresh-context GPT-6.1 high-effort subagents independently checked execution/cost/rehearsal, workers/evidence/dependencies, and deployment/infrastructure/documentation. Their claims were checked before consolidation. Changed blueprint and engineering authorities use their `origin/main` versions. No required authority lacked an `origin/main` version.

## Findings

P1  src/Parchwell.Runner/Run/Execution/KubernetesRunCluster.cs:170  docs/blueprint.md ADR-017 "Consequences"; docs/agents/factory.md "Item lifecycle" step 3; approved Revision 2 plan "Chart and admission protocol" and enforced check 9  Preserve observed worker outcomes before downloading their result objects. VerifyAsync obtains the actual pod exit and original stdout from WaitJobAsync, then downloads result.json before returning that WorkerExecution. A missing object, failed transfer or cancellation throws before RepeatExecutor.PersistWorkerAsync receives it. StartLineAsync has the same ordering at lines 125-130 for inventory/start-line downloads. The normal-failure collector downloads fixed Blob objects and watch history only; it never preserves these already-observed Kubernetes stdout/termination records. Phase cleanup can therefore export and destroy while losing an available worker outcome and completion receipt. The plan expressly requires "Preserve event receipt and worker outcomes" and retaining original receipts/run outcomes before teardown. Evidence: RepeatExecutor.cs:32-33 and :47-48 persist only after the cluster method returns; KubernetesRunCluster.cs:174-226 collects Blob objects only. A scratch-only executable using the actual KubernetesRunCluster, generator and renderer with a fake process returning exit 0 plus ORIGINAL_COMPLETED_WORKER_STDOUT and then BlobNotFound reproduced both modes: original_stdout_retained=false and termination_file_retained=false after PreserveAvailableArtifactsAsync. Source and output: `/workspace/scratch/issue-114-cold-review-5-repro/Program.cs` and `result.log`.
P1  src/Parchwell.Runner/Program.cs:211  origin/main docs/engineering/dotnet.md "Seams and dependencies"; docs/agents/factory.md "Item lifecycle" step 3; approved Revision 2 plan enforced check 9  Bind and validate the required subscription redaction input before starting a phase. The only read of runtime.subscription_guid occurs inside WriteCandidateAsync after PhaseExecutor has applied, spent all repeats, privately exported and destroyed. A runtime containing the fields documented in a01-runner.md:23 can omit this undocumented key; the scenario workflow never populates it from its already validated AZURE_SUBSCRIPTION_ID and neither preflight nor readiness validates it. The whole phase then finishes before GetProperty throws, returning runner_execution_failed with no candidate and blocking the gate/full phases. A malformed GUID fails equally late in PublicCandidate.Create. The binding standard says a missing setting fails at start, not at first use. Evidence: Program.cs:107-111 invokes the phase before candidate writing; DeploymentPreflight.cs has no redaction-input check; scenario.yml.txt's three runtime.update blocks omit subscription_guid; repository search finds this as its only production read. The approved plan requires a supplied subscription GUID for public redaction, so this is an existing required input, not a new experimental check.
P1  src/Parchwell.Runner/Run/Execution/WorkerOutcome.cs:46  origin/main docs/engineering/dotnet.md "Testing"; docs/agents/factory.md "Item lifecycle" step 3; approved Revision 2 plan enforced check 6  Add the required positive proof that a real complete_valid result, actual exit 0 and matching receipt authorize and preserve all six counts. This new acceptance branch has no positive Runner test. WorkerWireCompatibilityTests calls Reconcile only for invalid/incomplete outputs or malformed complete_valid output without counts; every CountsAccepted assertion in Runner tests is false. All rehearsal fixture results are Invalid, so the happy orchestration path does not cover count acceptance either. The approved plan explicitly requires "Tests: complete_valid/0/receipt accepts counts", and the binding testing standard requires the behavior and its proving test in the same PR. Evidence: WorkerWireCompatibilityTests.cs:22-100; WorkerWireFixtureTests.cs:59-70; PhaseEvidenceInventoryTests.cs:54. This finding requests the plan's existing acceptance test, without changing oracle rules or adding a new guard.
P1  src/Parchwell.Runner/Parchwell.Runner.csproj:1  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8, third paragraph  This adds the Runner project, references to Scenarios/Kafka and Hosting/YamlDotNet package references. New projects, assembly references and NuGet packages must be flagged. The approved Revision 2 plan "File changes and dependency boundary" expressly requires these additions; retain only under rule 8's approval-only exception, list the finding in the PR description, and leave merge to Hari. Existing central pins do not remove this approval gate. This finding does not count toward review-round stop limits.
P1  tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj:1  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8, third paragraph  This adds Runner.Tests, the xUnit package reference and references to Runner/LoadGen/Verifier. The approved Revision 2 plan expressly requires them. Apply the approval-only disposition: retain under rule 8, list in the PR description, and leave merge to Hari; exclude from review-round stop limits.
P1  tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj:21  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8, third paragraph  This adds the Runner assembly reference. The approved plan expressly requires updating the architecture project inventory and reference. Apply the same approval-only disposition and exclude it from review-round stop limits.

Findings: 6 at P0 or P1

Pre-existing (not blocking):

None identified.

## Verification, disposition and limits

- The frozen diff contains 159 authored paths and 18755 lines. Every added/context line was mechanically matched against the frozen worktree; there were no mismatches. Reported finding locations are changed lines. No results/, expected.yaml, scenarios/, docs/adr/ or .github/workflows/ path changes. Workflow text under docs/operations remains authored review scope.
- All 470 raw-manifest paths are unique, stay inside the evidence raw/ tree, exist and match byte size/SHA-256. No retained raw file is unlisted. All six producer maps agree with the corresponding exact manifest producer strings. Repeated manifest entries were checked mechanically and their unique producer commands read; this does not substitute old dirty-worktree output for current-head acceptance.
- The authored README's current cited Runner XML reports 228 passed, with zero failed/errors/skipped. The focused LoadGen XML reports 33 passed with zero failed/errors/skipped. The cited build has zero warnings/errors, and the Terraform report has 12 passing mock tests. The cited rehearsal has three phases, five repeats per phase, three applies/destroys, zero Azure calls/replacements and 23 negative cases. All 518 files in the cited pre-validation repository-input inventory match the frozen files. Counts/hashes were computed by scripts.
- All seven changed/new lockfiles parse. The four new linux-x64 lockfiles' common net10.0 dependency maps equal their projects' origin/main maps; no version drift was found. Existing test changes retain their assertions. The mandatory project/package/reference findings above remain regardless of centrally existing pins.
- The three substantive findings concern existing execution/evidence, required startup configuration and explicit test obligations. They are subject to ordinary finding disposition and the cited continuation decision. The three approval-only findings are excluded from the cold-review churn stop limits under rule 8. The continuation override and historical trips are retained in the authored resume-status record; the approved decision does not erase new substantive findings.
- The prior account-projection and LoadGen options findings were independently checked against the new two-account projection, actual three-account fixtures, validated optional LoadGen host option and unchanged before-native equality check. The cost-scope fix retains the reservation unless both primary and node scopes are verified. Recovery retains a separate diagnostic snapshot without replacing the original phase manifest or authorizing counts.
- The storage/sweeper design amendment matches Hari's explicit adopted decision: private Entra-only storage, no new role assignment, destroy-only main-guarded sweeper on the existing app, with external prerequisites for Hari. This is an approved proposed design amendment requiring protected-path review, not proof that external readiness exists. No new public exposure or private-endpoint relaxation was found.
- Deployment archive/smoke, Helm rendering, committed real service-tag data, TFLint acceptance and exact-head PR CI remain explicitly incomplete. The README accurately retains these blockers. Source inspection, retained local reports, existing-binary selected tests and the scratch defect reproduction are not replacements for the prescribed acceptance executions.
- The scratch reproduction command was `/workspace/scratch/dotnet/dotnet run --project /workspace/scratch/issue-114-cold-review-5-repro/repro.csproj --configuration Release`; its external transport is fake and performs no Azure operation.
- Independent deployment checks ran 27 selected tests using existing Release binaries and checked shell/Python syntax. The execution pass built only a scratch reproduction referencing the existing assemblies. No full build/test suite was rerun, and no live Azure operation, repository source/evidence edit, commit, push, PR or external comment was made. Only scratch review artifacts were written.

## Sources and applied authorities

- AGENTS.md: all GOVERNANCE (including scope/deferred items, safety, verdict integrity, merge, handoff and truth rules), Skills, Before opening a pull request, Code Review Rules and Writing standards; CLAUDE.md; docs/agents/factory.md; origin/main blueprint ADRs 001-019 and scope/inventory; origin/main docs/engineering/dotnet.md and terraform.md; docs/engineering/code-comments.md review exceptions.
- Approved Revision 2 plan: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6321098789. Adopted runner/deployment checks: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/109#issuecomment-6313658165 and registry amendment https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/109#issuecomment-6313841552. Storage/recovery decision: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6332413257. Continuation decision: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6333713470. Historical cold-review records are authored review inputs in this diff, not replacement authority.
- Pinned broker formatting: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c#L141-L224. SASL_SSL names and IPv6 bracketing were checked against that implementation.
- Pinned Azure CLI conditional storage options: https://github.com/Azure/azure-cli/blob/da4c0548a2725ea3d08b1d94b8e7fa1b0b07256e/src/azure-cli/azure/cli/command_modules/storage/_params.py. Non-admin AKS credential options: https://github.com/Azure/azure-cli/blob/da4c0548a2725ea3d08b1d94b8e7fa1b0b07256e/src/azure-cli/azure/cli/command_modules/acs/_params.py#L1096-L1103. Azure CLI kubelogin behavior: https://github.com/Azure/kubelogin/blob/bdf0e79b5a0ea6fe9fd3b7ada771378c344ad760/docs/book/src/concepts/login-modes/azurecli.md.
- Pinned storage module: https://github.com/Azure/terraform-azurerm-avm-res-storage-storageaccount/tree/5695e5ec21744a3765051677d9e82bfba16b5d98. Pinned AKS state export: https://github.com/Azure/terraform-azurerm-avm-res-containerservice-managedcluster/blob/4bc0128b057d10d5ed146a53d8c77933fd00ead8/main.tf#L12-L26 and outputs.tf#L98-L109. The cited AKS FAQ separates primary/node resource groups: https://github.com/MicrosoftDocs/azure-aks-docs/blob/6901de76c108a545a6b17fee61a39ca9e9e001d1/articles/aks/faq.yml#L110-L117.
- Official Helm release checksum: https://api.github.com/repos/helm/helm/releases/tags/v4.3.1. NetworkPolicy conjunction: https://kubernetes.io/docs/concepts/services-networking/network-policies/#behavior-of-to-and-from-selectors. Kubernetes raw watch semantics were checked through the pinned kubectl source cited in KubernetesRunCluster and the independent watch tests.
- KQL table columns were checked at MicrosoftDocs/azure-monitor-docs commit 6c8e97c9e8b8e57c19e3b319bda5188e4ceba029, articles/azure-monitor/reference/tables/azmskafkausererrorlogs.md and azmsdiagnosticerrorlogs.md: Message versus ErrorMessage/ErrorCount, _ResourceId and TimeGenerated. Terraform v1.16.6 state_pull.go returns success with no output for absent state; the suspected fresh-backend failure was rejected.

## Full rubric coverage

Every path below applies A: the complete anything row, including all binding governance/process/review/writing rules, CLAUDE.md, factory.md and blueprint ADRs. N adds origin/main dotnet.md. T adds origin/main terraform.md and ADR-011. K adds the pinned Workloads settings/inventory traceability plus ADR-014/015. E means the changed engineering document is also reviewed against its own origin/main text. Verdict-integrity and workflow safety rules were checked globally; their path triggers are absent. Label-dependent rules were judged without assuming labels absent from the diff. Analyzer-enforced style and comment presence/style were not raised.

| Changed path | Applied rows |
|---|---|
| `Directory.Build.props` | A, N |
| `Parchwell.slnx` | A, N |
| `charts/parchwell/Chart.yaml` | A, K |
| `charts/parchwell/templates/_helpers.tpl` | A, K |
| `charts/parchwell/templates/admission.yaml` | A, K |
| `charts/parchwell/templates/generated-values.yaml` | A, K |
| `charts/parchwell/templates/jobs.yaml` | A, K |
| `charts/parchwell/templates/networkpolicies.yaml` | A, K |
| `charts/parchwell/templates/serviceaccounts.yaml` | A, K |
| `charts/parchwell/values.schema.json` | A, K |
| `charts/parchwell/values.yaml` | A, K |
| `docs/blueprint.md` | A |
| `docs/engineering/dotnet.md` | A, N, E |
| `docs/engineering/terraform.md` | A, T, E |
| `docs/operations/a01-runner.md` | A, K |
| `docs/operations/a01-runtime-limits.json` | A, K |
| `docs/operations/evidence/issue-114/README.md` | A |
| `docs/operations/evidence/issue-114/aks-binding-producers.json` | A |
| `docs/operations/evidence/issue-114/cold-review-1.md` | A |
| `docs/operations/evidence/issue-114/cold-review-2.md` | A |
| `docs/operations/evidence/issue-114/cold-review-3.md` | A |
| `docs/operations/evidence/issue-114/cold-review-4.md` | A |
| `docs/operations/evidence/issue-114/local-producers.json` | A |
| `docs/operations/evidence/issue-114/raw-manifest.json` | A |
| `docs/operations/evidence/issue-114/resume-producers.json` | A |
| `docs/operations/evidence/issue-114/resume-status.md` | A |
| `docs/operations/evidence/issue-114/review-4-producers.json` | A |
| `docs/operations/evidence/issue-114/review-fix-producers.json` | A |
| `docs/operations/evidence/issue-114/terraform-producers.json` | A |
| `docs/operations/evidence/issue-114/write-raw-manifest.mjs` | A |
| `docs/operations/kql/a01-runtime.kql` | A |
| `docs/operations/loadgen-contract.md` | A, K |
| `docs/operations/scenario-contract.md` | A, K |
| `docs/operations/terraform.md` | A |
| `docs/operations/verifier-contract.md` | A, K |
| `docs/operations/workflows/a01-deployment-acceptance.yml.txt` | A |
| `docs/operations/workflows/scenario.yml.txt` | A |
| `docs/operations/workflows/sweeper.yml.txt` | A |
| `infra/persistent/locals.tf` | A, T |
| `infra/persistent/outputs.tf` | A, T |
| `infra/persistent/storage.tf` | A, T |
| `scripts/fetch-service-tags.sh` | A |
| `scripts/verification/assert-a01-deployment.py` | A, K |
| `scripts/verify-a01-deployment.sh` | A, K |
| `src/Parchwell.Kafka/packages.linux-x64.lock.json` | A, N |
| `src/Parchwell.LoadGen/Parchwell.LoadGen.csproj` | A, N, K |
| `src/Parchwell.LoadGen/Program.cs` | A, N, K |
| `src/Parchwell.LoadGen/Run/Execution/LoadGenWorker.cs` | A, N, K |
| `src/Parchwell.LoadGen/Run/Input/DeploymentAdmission.cs` | A, N, K |
| `src/Parchwell.LoadGen/Run/Input/LoadGenHostOptions.cs` | A, N, K |
| `src/Parchwell.LoadGen/Run/Ledger/AssemblyInfo.cs` | A, N, K |
| `src/Parchwell.LoadGen/packages.linux-x64.lock.json` | A, N, K |
| `src/Parchwell.Runner/Parchwell.Runner.csproj` | A, N |
| `src/Parchwell.Runner/Program.cs` | A, N |
| `src/Parchwell.Runner/Run/Contracts/ExecutionBudgets.cs` | A, N |
| `src/Parchwell.Runner/Run/Contracts/IArtifactStore.cs` | A, N |
| `src/Parchwell.Runner/Run/Contracts/IProcessRunner.cs` | A, N |
| `src/Parchwell.Runner/Run/Contracts/IRunCluster.cs` | A, N |
| `src/Parchwell.Runner/Run/Contracts/IRunGate.cs` | A, N |
| `src/Parchwell.Runner/Run/Contracts/ITerraformEnvironment.cs` | A, N |
| `src/Parchwell.Runner/Run/Contracts/WorkerIdentity.cs` | A, N |
| `src/Parchwell.Runner/Run/Cost/ActualCostReconciler.cs` | A, N |
| `src/Parchwell.Runner/Run/Cost/AzureCliRunStore.cs` | A, N |
| `src/Parchwell.Runner/Run/Cost/LedgerModels.cs` | A, N |
| `src/Parchwell.Runner/Run/Cost/PhaseReservation.cs` | A, N |
| `src/Parchwell.Runner/Run/Cost/RunLedger.cs` | A, N |
| `src/Parchwell.Runner/Run/Deployment/ChartRenderer.cs` | A, N, K |
| `src/Parchwell.Runner/Run/Deployment/DeploymentGenerator.cs` | A, N, K |
| `src/Parchwell.Runner/Run/Deployment/DeploymentPreflight.cs` | A, N, K |
| `src/Parchwell.Runner/Run/Deployment/DeploymentScenarioSource.cs` | A, N, K |
| `src/Parchwell.Runner/Run/Deployment/ServiceTagData.cs` | A, N, K |
| `src/Parchwell.Runner/Run/Evidence/EvidenceExporter.cs` | A, N |
| `src/Parchwell.Runner/Run/Evidence/KqlCollector.cs` | A, N |
| `src/Parchwell.Runner/Run/Evidence/PrivateEvidenceCollector.cs` | A, N |
| `src/Parchwell.Runner/Run/Evidence/PrivateProvenanceCollector.cs` | A, N |
| `src/Parchwell.Runner/Run/Evidence/PublicCandidate.cs` | A, N |
| `src/Parchwell.Runner/Run/Execution/AksKubeconfigBinding.cs` | A, N |
| `src/Parchwell.Runner/Run/Execution/BoundedProcessRunner.cs` | A, N |
| `src/Parchwell.Runner/Run/Execution/BrokerInventory.cs` | A, N |
| `src/Parchwell.Runner/Run/Execution/GenerationGuard.cs` | A, N |
| `src/Parchwell.Runner/Run/Execution/KubernetesRunCluster.cs` | A, N |
| `src/Parchwell.Runner/Run/Execution/LocalVerifierGate.cs` | A, N |
| `src/Parchwell.Runner/Run/Execution/PhaseExecutor.cs` | A, N |
| `src/Parchwell.Runner/Run/Execution/PodAdmission.cs` | A, N |
| `src/Parchwell.Runner/Run/Execution/ProbeGateRunner.cs` | A, N |
| `src/Parchwell.Runner/Run/Execution/PublicationAcceptance.cs` | A, N |
| `src/Parchwell.Runner/Run/Execution/RecoveryEvidenceCollector.cs` | A, N |
| `src/Parchwell.Runner/Run/Execution/RepeatExecutor.cs` | A, N |
| `src/Parchwell.Runner/Run/Execution/SweepExecutor.cs` | A, N |
| `src/Parchwell.Runner/Run/Execution/TerraformEnvironment.cs` | A, N |
| `src/Parchwell.Runner/Run/Execution/WorkerBlobDownloader.cs` | A, N |
| `src/Parchwell.Runner/Run/Execution/WorkerOutcome.cs` | A, N |
| `src/Parchwell.Runner/Run/Rehearsal/RehearsalRunner.cs` | A, N |
| `src/Parchwell.Runner/Run/Rehearsal/RehearsalScenarioFactory.cs` | A, N |
| `src/Parchwell.Runner/Run/Rehearsal/RehearsalTransports.cs` | A, N |
| `src/Parchwell.Runner/Run/Rehearsal/WireFixtures.cs` | A, N |
| `src/Parchwell.Runner/packages.lock.json` | A, N |
| `src/Parchwell.Scenarios/Generation/GeneratedValuesCompatibility.cs` | A, N, K |
| `src/Parchwell.Scenarios/Generation/HelmValuesGenerator.cs` | A, N, K |
| `src/Parchwell.Scenarios/packages.linux-x64.lock.json` | A, N, K |
| `src/Parchwell.Verifier/AssemblyInfo.cs` | A, N, K |
| `src/Parchwell.Verifier/Common/Kafka/VerifierKafkaSession.cs` | A, N, K |
| `src/Parchwell.Verifier/Common/Serialization/DeploymentJson.cs` | A, N, K |
| `src/Parchwell.Verifier/ImageSmoke.cs` | A, N, K |
| `src/Parchwell.Verifier/Parchwell.Verifier.csproj` | A, N, K |
| `src/Parchwell.Verifier/Program.cs` | A, N, K |
| `src/Parchwell.Verifier/StartLine/BrokerInventoryCodec.cs` | A, N, K |
| `src/Parchwell.Verifier/StartLine/StartLineHandler.cs` | A, N, K |
| `src/Parchwell.Verifier/VerifierCompletionState.cs` | A, N, K |
| `src/Parchwell.Verifier/VerifierHostOptions.cs` | A, N, K |
| `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | A, N, K |
| `src/Parchwell.Verifier/VerifierWorker.cs` | A, N, K |
| `src/Parchwell.Verifier/VerifyRun/A01/A01EvidenceRecorder.cs` | A, N, K |
| `src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs` | A, N, K |
| `src/Parchwell.Verifier/packages.linux-x64.lock.json` | A, N, K |
| `tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj` | A, N |
| `tests/Parchwell.ArchitectureTests/ProjectModel.cs` | A, N |
| `tests/Parchwell.ArchitectureTests/packages.lock.json` | A, N |
| `tests/Parchwell.LoadGen.Tests/Host/DeploymentAdmissionTests.cs` | A, N, K |
| `tests/Parchwell.LoadGen.Tests/Host/GeneratedValuesCompatibilityTests.cs` | A, N, K |
| `tests/Parchwell.LoadGen.Tests/Host/LoadGenWorkerHostTests.cs` | A, N, K |
| `tests/Parchwell.Runner.Tests/ChartContractTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Cost/ActualCostReconcilerTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Cost/AzureCliRunStoreTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Cost/PhaseReservationTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentGeneratorTests.cs` | A, N, K |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentPreflightTests.cs` | A, N, K |
| `tests/Parchwell.Runner.Tests/Deployment/ServiceTagDataTests.cs` | A, N, K |
| `tests/Parchwell.Runner.Tests/Evidence/EvidenceExporterTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/PrivateProvenanceCollectorTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/RecoveryExportTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/azure-cli-query-recording.json` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/AksKubeconfigBindingTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/BoundedProcessRunnerTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/FailureArtifactPreservationTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/GenerationGuardTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/KubernetesWatchTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/PhaseEvidenceInventoryTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/PodAdmissionTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/RecoveryEvidenceTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/TerraformEnvironmentTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj` | A, N |
| `tests/Parchwell.Runner.Tests/RehearsalTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Support/WorkerWireFixtures.cs` | A, N |
| `tests/Parchwell.Runner.Tests/WorkerPublicationFixture.cs` | A, N |
| `tests/Parchwell.Runner.Tests/WorkerWireCompatibilityTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/WorkerWireFixtureTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/packages.lock.json` | A, N |
| `tests/Parchwell.Scenarios.Tests/GeneratedValuesCompatibilityTests.cs` | A, N, K |
| `tests/Parchwell.Verifier.Tests/A01/A01C3EvidenceRecorderTests.cs` | A, N, K |
| `tests/Parchwell.Verifier.Tests/Component/ComponentRunHarness.cs` | A, N, K |
| `tests/Parchwell.Verifier.Tests/Host/BrokerInventoryTests.cs` | A, N, K |
| `tests/Parchwell.Verifier.Tests/Host/C3HandlerTests.cs` | A, N, K |
| `tests/Parchwell.Verifier.Tests/Host/GeneratedValuesCompatibilityTests.cs` | A, N, K |
| `tests/Parchwell.Verifier.Tests/Host/ImageSmokeTests.cs` | A, N, K |
| `tests/Parchwell.Verifier.Tests/Host/VerifierCompletionTests.cs` | A, N, K |
| `tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | A, N, K |
| `tests/Parchwell.Verifier.Tests/Support/HostEvidenceBuilder.cs` | A, N, K |
| `tests/terraform/persistent/main.tftest.hcl` | A, T |

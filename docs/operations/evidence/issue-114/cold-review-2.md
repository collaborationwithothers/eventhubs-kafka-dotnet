# Issue 114 cold review 2

Frozen review diff: `/workspace/scratch/issue-114-cold-review-2.diff`.
Diff SHA-256: `7b711177943125fcb21841fbfbc0f577078e5d242c0f1ed17402e6a741369d88`.
Raw-evidence manifest: `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/raw-manifest.json`.
Manifest SHA-256: `38a9e0916eac7cf7637cc5563052f5100b3b8e2db0062f930fd49be730eb6ddc`.
Review-rule baseline: `origin/main` at `4a0acc5d886a12cafdfdeafbe1e71f4d0802bf55`.

Applied the repository-owned `.agents/skills/repo-review/SKILL.md` independently. The only author inputs were the frozen diff and raw-evidence manifest. The historical first-round report was read because it is itself authored content in this diff; its dispositions were checked against the source.

## Findings

P1  src/Parchwell.Runner/Run/Execution/RepeatExecutor.cs:38  docs/blueprint.md ADR-017 "Consequences"; docs/agents/factory.md "Item lifecycle" step 3; approved Revision 2 plan, enforced check 9  A failed LoadGen exit or missing completion receipt prevents preservation of already-published diagnostic artifacts. The success gate throws before DownloadPublicationAsync. A concrete supported trigger is successful publication followed by host stop, disposal or receipt-write failure: LoadGen then exits 1 without a receipt, while its six original Blob objects still exist. PhaseExecutor only exports files already in its local output tree and then destroys the ephemeral storage. The new RecoveryEvidenceCollector is wired only to sweep, so it cannot recover these objects after normal cleanup has deleted them. Keep verification blocked, but collect the available original objects on this failure path before guarded teardown. Evidence: LoadGen Program.cs RunAsync emits the receipt after host disposal; LoadGenWorkerHostTests.Given_ProcessLifecycle_When_Completing_Then_ReceiptRequiresPublicationStopDisposalAndWritableOutput explicitly covers published-but-failed stop/dispose/write cases; RepeatExecutor.cs:36-40 skips the download; PhaseExecutor.cs:100-130 performs only corroboration/local export before destruction.
P1  docs/operations/workflows/scenario.yml.txt:172  docs/agents/factory.md "Item lifecycle" step 3; approved Revision 2 plan, enforced check 9 and "Rule ownership and acceptance"  The workflow excludes required provenance and cost inputs from its durable private evidence. It writes runtime.json, image-receipts.json, image-publication.log and provisioned-tool receipts under A01_OUTPUT, but the phase and fallback export commands scan only A01_OUTPUT/phase; fallback destroy performs no additional provenance collection. The original cost file remains at an external private input path and is never copied into that export either; RunLedger retains only selected normalized allowance/baseline fields, not the sourced price lines or separate persistent estimate. Consequently a successful phase can record PrivateExport.Complete=true and destroy while these required original records have no verified persistent copy. The same output-directory mismatch occurs in the C0 and C3 jobs. Explicitly stage or include those bounded private records before the immutable manifest is committed. Evidence: scenario.yml.txt:84-97 prepares inputs outside phase; :105-138 records tools outside phase; :149-167 records image publication outside phase; :172/:177 pass only phase; PhaseExecutor.cs:114-118 and Program.cs ExportAsync enumerate only that directory; PublicCandidate upload intentionally includes only candidate.json.
P1  src/Parchwell.Runner/Parchwell.Runner.csproj:1  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8, third paragraph  The diff adds the Runner and Runner.Tests projects and new assembly references: Runner to Scenarios/Kafka, Runner.Tests to Runner/LoadGen/Verifier, and ArchitectureTests to Runner. New projects and assembly references must be flagged under the rubric. These are approval-only findings expressly required by the cited Revision 2 plan: retain under rule 8's exception, list the finding in the PR description and leave merge to Hari. They do not count toward the review-round stop limits.

Findings: 3 at P0 or P1

Pre-existing (not blocking):

None identified.

## Scope and rubric coverage

The frozen diff touches 147 authored paths. Every path below was matched to the rubric. The anything row applies AGENTS.md GOVERNANCE, Code Review Rules, Before opening a pull request, Skills and writing standards; CLAUDE.md; docs/agents/factory.md; and the blueprint ADRs. Changed blueprint and engineering documents were judged using their origin/main versions. No applicable rule document lacked an origin/main version.

.NET source, test, project, solution, property and lockfile paths additionally use docs/engineering/dotnet.md. Terraform and native Terraform tests additionally use docs/engineering/terraform.md and ADR-011. Scenario resolution, generated values, worker configuration and chart/runtime paths additionally use the pinned workload settings and ADR-014/015; their baseline remains Confluent.Kafka/librdkafka 2.16.0. Each changed docs/engineering document was also checked against its own baseline. No scenarios/, results/, expected.yaml, docs/adr/ or .github/workflows/ path is changed. The executable workflow text under docs/operations remains authored review scope.

- `Directory.Build.props`: anything, .NET.
- `Parchwell.slnx`: anything, .NET.
- `charts/parchwell/Chart.yaml`: anything, Kafka/settings and ADR-014/015.
- `charts/parchwell/templates/_helpers.tpl`: anything, Kafka/settings and ADR-014/015.
- `charts/parchwell/templates/admission.yaml`: anything, Kafka/settings and ADR-014/015.
- `charts/parchwell/templates/generated-values.yaml`: anything, Kafka/settings and ADR-014/015.
- `charts/parchwell/templates/jobs.yaml`: anything, Kafka/settings and ADR-014/015.
- `charts/parchwell/templates/networkpolicies.yaml`: anything, Kafka/settings and ADR-014/015.
- `charts/parchwell/templates/serviceaccounts.yaml`: anything, Kafka/settings and ADR-014/015.
- `charts/parchwell/values.schema.json`: anything, Kafka/settings and ADR-014/015.
- `charts/parchwell/values.yaml`: anything, Kafka/settings and ADR-014/015.
- `docs/blueprint.md`: anything.
- `docs/engineering/dotnet.md`: anything, changed engineering document.
- `docs/engineering/terraform.md`: anything, changed engineering document.
- `docs/operations/a01-runner.md`: anything, Kafka/settings and ADR-014/015.
- `docs/operations/a01-runtime-limits.json`: anything, Kafka/settings and ADR-014/015.
- `docs/operations/evidence/issue-114/README.md`: anything.
- `docs/operations/evidence/issue-114/aks-binding-producers.json`: anything.
- `docs/operations/evidence/issue-114/cold-review-1.md`: anything.
- `docs/operations/evidence/issue-114/local-producers.json`: anything.
- `docs/operations/evidence/issue-114/raw-manifest.json`: anything.
- `docs/operations/evidence/issue-114/terraform-producers.json`: anything.
- `docs/operations/evidence/issue-114/write-raw-manifest.mjs`: anything.
- `docs/operations/kql/a01-runtime.kql`: anything.
- `docs/operations/loadgen-contract.md`: anything.
- `docs/operations/scenario-contract.md`: anything, Kafka/settings and ADR-014/015.
- `docs/operations/terraform.md`: anything.
- `docs/operations/verifier-contract.md`: anything.
- `docs/operations/workflows/a01-deployment-acceptance.yml.txt`: anything.
- `docs/operations/workflows/scenario.yml.txt`: anything.
- `docs/operations/workflows/sweeper.yml.txt`: anything.
- `infra/persistent/locals.tf`: anything, Terraform; ADR-011.
- `infra/persistent/outputs.tf`: anything, Terraform; ADR-011.
- `infra/persistent/storage.tf`: anything, Terraform; ADR-011.
- `scripts/fetch-service-tags.sh`: anything.
- `scripts/verification/assert-a01-deployment.py`: anything.
- `scripts/verify-a01-deployment.sh`: anything.
- `src/Parchwell.Kafka/packages.linux-x64.lock.json`: anything, .NET.
- `src/Parchwell.LoadGen/Parchwell.LoadGen.csproj`: anything, .NET.
- `src/Parchwell.LoadGen/Program.cs`: anything, .NET, Kafka/settings and ADR-014/015.
- `src/Parchwell.LoadGen/Run/Execution/LoadGenWorker.cs`: anything, .NET, Kafka/settings and ADR-014/015.
- `src/Parchwell.LoadGen/Run/Input/DeploymentAdmission.cs`: anything, .NET.
- `src/Parchwell.LoadGen/Run/Ledger/AssemblyInfo.cs`: anything, .NET.
- `src/Parchwell.LoadGen/packages.linux-x64.lock.json`: anything, .NET.
- `src/Parchwell.Runner/Parchwell.Runner.csproj`: anything, .NET.
- `src/Parchwell.Runner/Program.cs`: anything, .NET, Kafka/settings and ADR-014/015.
- `src/Parchwell.Runner/Run/Contracts/ExecutionBudgets.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Contracts/IArtifactStore.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Contracts/IProcessRunner.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Contracts/IRunCluster.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Contracts/IRunGate.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Contracts/ITerraformEnvironment.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Contracts/WorkerIdentity.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Cost/ActualCostReconciler.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Cost/AzureCliRunStore.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Cost/LedgerModels.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Cost/PhaseReservation.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Cost/RunLedger.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Deployment/ChartRenderer.cs`: anything, .NET, Kafka/settings and ADR-014/015.
- `src/Parchwell.Runner/Run/Deployment/DeploymentGenerator.cs`: anything, .NET, Kafka/settings and ADR-014/015.
- `src/Parchwell.Runner/Run/Deployment/DeploymentPreflight.cs`: anything, .NET, Kafka/settings and ADR-014/015.
- `src/Parchwell.Runner/Run/Deployment/DeploymentScenarioSource.cs`: anything, .NET, Kafka/settings and ADR-014/015.
- `src/Parchwell.Runner/Run/Deployment/ServiceTagData.cs`: anything, .NET, Kafka/settings and ADR-014/015.
- `src/Parchwell.Runner/Run/Evidence/EvidenceExporter.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Evidence/KqlCollector.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Evidence/PrivateEvidenceCollector.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Evidence/PublicCandidate.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Execution/AksKubeconfigBinding.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Execution/BoundedProcessRunner.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Execution/BrokerInventory.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Execution/GenerationGuard.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Execution/KubernetesRunCluster.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Execution/LocalVerifierGate.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Execution/PhaseExecutor.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Execution/PodAdmission.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Execution/ProbeGateRunner.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Execution/PublicationAcceptance.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Execution/RecoveryEvidenceCollector.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Execution/RepeatExecutor.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Execution/SweepExecutor.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Execution/TerraformEnvironment.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Execution/WorkerBlobDownloader.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Execution/WorkerOutcome.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Rehearsal/RehearsalRunner.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Rehearsal/RehearsalScenarioFactory.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Rehearsal/RehearsalTransports.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Rehearsal/WireFixtures.cs`: anything, .NET.
- `src/Parchwell.Runner/packages.lock.json`: anything, .NET.
- `src/Parchwell.Scenarios/Generation/GeneratedValuesCompatibility.cs`: anything, .NET, Kafka/settings and ADR-014/015.
- `src/Parchwell.Scenarios/Generation/HelmValuesGenerator.cs`: anything, .NET, Kafka/settings and ADR-014/015.
- `src/Parchwell.Scenarios/packages.linux-x64.lock.json`: anything, .NET.
- `src/Parchwell.Verifier/AssemblyInfo.cs`: anything, .NET.
- `src/Parchwell.Verifier/Common/Kafka/VerifierKafkaSession.cs`: anything, .NET.
- `src/Parchwell.Verifier/Common/Serialization/DeploymentJson.cs`: anything, .NET.
- `src/Parchwell.Verifier/ImageSmoke.cs`: anything, .NET.
- `src/Parchwell.Verifier/Parchwell.Verifier.csproj`: anything, .NET.
- `src/Parchwell.Verifier/Program.cs`: anything, .NET, Kafka/settings and ADR-014/015.
- `src/Parchwell.Verifier/StartLine/BrokerInventoryCodec.cs`: anything, .NET.
- `src/Parchwell.Verifier/StartLine/StartLineHandler.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifierCompletionState.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifierHostOptions.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifierWorker.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifyRun/A01/A01EvidenceRecorder.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs`: anything, .NET, Kafka/settings and ADR-014/015.
- `src/Parchwell.Verifier/packages.linux-x64.lock.json`: anything, .NET.
- `tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj`: anything, .NET.
- `tests/Parchwell.ArchitectureTests/ProjectModel.cs`: anything, .NET.
- `tests/Parchwell.ArchitectureTests/packages.lock.json`: anything, .NET.
- `tests/Parchwell.LoadGen.Tests/Host/DeploymentAdmissionTests.cs`: anything, .NET.
- `tests/Parchwell.LoadGen.Tests/Host/GeneratedValuesCompatibilityTests.cs`: anything, .NET.
- `tests/Parchwell.LoadGen.Tests/Host/LoadGenWorkerHostTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/ChartContractTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Cost/ActualCostReconcilerTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Cost/AzureCliRunStoreTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Cost/PhaseReservationTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Deployment/DeploymentGeneratorTests.cs`: anything, .NET, Kafka/settings and ADR-014/015.
- `tests/Parchwell.Runner.Tests/Deployment/DeploymentPreflightTests.cs`: anything, .NET, Kafka/settings and ADR-014/015.
- `tests/Parchwell.Runner.Tests/Deployment/ServiceTagDataTests.cs`: anything, .NET, Kafka/settings and ADR-014/015.
- `tests/Parchwell.Runner.Tests/Evidence/EvidenceExporterTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Evidence/RecoveryExportTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Evidence/azure-cli-query-recording.json`: anything.
- `tests/Parchwell.Runner.Tests/Execution/AksKubeconfigBindingTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Execution/BoundedProcessRunnerTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Execution/GenerationGuardTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Execution/KubernetesWatchTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Execution/PodAdmissionTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Execution/RecoveryEvidenceTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Execution/TerraformEnvironmentTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj`: anything, .NET.
- `tests/Parchwell.Runner.Tests/RehearsalTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Support/WorkerWireFixtures.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/WorkerPublicationFixture.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/WorkerWireCompatibilityTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/WorkerWireFixtureTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/packages.lock.json`: anything, .NET.
- `tests/Parchwell.Scenarios.Tests/GeneratedValuesCompatibilityTests.cs`: anything, .NET.
- `tests/Parchwell.Verifier.Tests/A01/A01C3EvidenceRecorderTests.cs`: anything, .NET.
- `tests/Parchwell.Verifier.Tests/Component/ComponentRunHarness.cs`: anything, .NET.
- `tests/Parchwell.Verifier.Tests/Host/BrokerInventoryTests.cs`: anything, .NET.
- `tests/Parchwell.Verifier.Tests/Host/C3HandlerTests.cs`: anything, .NET.
- `tests/Parchwell.Verifier.Tests/Host/GeneratedValuesCompatibilityTests.cs`: anything, .NET.
- `tests/Parchwell.Verifier.Tests/Host/ImageSmokeTests.cs`: anything, .NET.
- `tests/Parchwell.Verifier.Tests/Host/VerifierCompletionTests.cs`: anything, .NET.
- `tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs`: anything, .NET.
- `tests/Parchwell.Verifier.Tests/Support/HostEvidenceBuilder.cs`: anything, .NET.
- `tests/terraform/persistent/main.tftest.hcl`: anything, Terraform; ADR-011.

## Verification and sources

- All added/context lines in the frozen diff match the current files; the implementation remained frozen. Every finding location was mechanically checked to be an added/changed line in that diff. A scan found no removed assertions.
- The manifest has 433 unique raw paths. Each is under the evidence directory's raw/ tree, exists, and matches its recorded byte size and SHA-256. Raw contents inspected for claims were the logs/reports cited by the authored README or review record; the other raw bytes were read only for manifest integrity.
- The cited current Runner XML reports 177 passed, zero failed/errors/skipped; the cited rehearsal summary reports three phases, five repeats per phase, three applies/destroys and no Azure calls or replacements. These are synthetic local checks, not reviewed-head CI acceptance or live evidence.
- The README correctly leaves image archive/smoke/Helm/service-tag acceptance and required exact-head CI evidence pending. The review does not substitute source inspection for those missing executions.
- The Revision 2 implementation plan was independently read through the REST API: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6321098789. Also read the adopted runner decision https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/109#issuecomment-6313658165 and storage/recovery decision https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6332413257. These support the intended new projects, private run storage and destroy-only sweeper design; they do not remove the evidence-retention obligations above.
- Pinned librdkafka v2.16.0 broker naming and IPv6 bracketing were checked at https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c#L141-L224. The Helm v4.3.1 linux-amd64 checksum matches the official release body at https://api.github.com/repos/helm/helm/releases/tags/v4.3.1.
- Azure CLI conditional Blob options and default download overwrite behavior were checked at https://github.com/Azure/azure-cli/blob/da4c0548a2725ea3d08b1d94b8e7fa1b0b07256e/src/azure-cli/azure/cli/command_modules/storage/_params.py. Kubelogin's existing-login/subscription behavior was checked at https://github.com/Azure/kubelogin/blob/bdf0e79b5a0ea6fe9fd3b7ada771378c344ad760/docs/book/src/concepts/login-modes/azurecli.md.
- The pinned AVM storage output shapes were checked at https://github.com/Azure/terraform-azurerm-avm-res-storage-storageaccount/blob/5695e5ec21744a3765051677d9e82bfba16b5d98/outputs.tf. The combined NetworkPolicy selector semantics were checked against the Kubernetes-owned documentation source, https://raw.githubusercontent.com/kubernetes/website/main/content/en/docs/concepts/services-networking/network-policies.md, after the rendered documentation endpoint returned HTTP 403.
- KQL Message/ErrorMessage/ErrorCount columns match the cited MicrosoftDocs/azure-monitor-docs commit 6c8e97c9e8b8e57c19e3b319bda5188e4ceba029, in articles/azure-monitor/reference/tables/azmskafkausererrorlogs.md and azmsdiagnosticerrorlogs.md.
- No live Azure operation, source edit, commit, push, PR creation or external comment was performed. This report was saved only outside the repository.

## Author disposition and handoff

The reviewed source diff is the tree of implementation commit
`394f1c670a0987f2e9acde3d46435c53f0cb1ba8` against
`4a0acc5d886a12cafdfdeafbe1e71f4d0802bf55`; its authored diff SHA-256 matches the
frozen hash above. This report and the handoff documentation are added afterward
for the permitted WIP backup, without further implementation changes.

Both evidence-retention findings are valid and remain unfixed. The first round
found missing aged-generation evidence capture before sweeper teardown; this
round finds another pre-teardown capture gap in the same execution/evidence
component. AGENTS.md "Before opening a pull request" rule 8 states:

> The churn breaker stops the session when the same file or component gets P0 or P1 findings in two cold-review rounds; hand off under GOVERNANCE "Handoffs".

That rule also states: "Only a `## Decision` on the issue overrides this breaker."

The normal failure path must retain already-published worker originals without
authorizing verification or counts. The workflow must durably include its bounded
original runtime, image/tool receipts and sourced cost inputs before its immutable
phase manifest. These are existing plan obligations, not new experiment checks.
They need a further authorized implementation session and independent review.

The project/reference finding remains the plan-required approval-only exception.
It does not count toward the churn breaker, and Hari retains merge authority.
No P2 follow-up issue is created because no clean cold-review round was reached.
No PR, required PR workflow run/logs or clean reviewed head has been produced.
Container/Helm/service-tag/TFLint acceptance remains blocked as the README records.

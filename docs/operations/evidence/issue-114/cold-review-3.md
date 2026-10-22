# Issue 114 cold review 3

Reviewed frozen diff: `/workspace/scratch/issue-114-cold-review-3.diff`.
Diff SHA-256: `92fae019549e744cdf30f6127aaa230f2dd9421ff3e3853218994ee5628b2f2e`.
Raw-evidence manifest: `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/raw-manifest.json`.
Manifest SHA-256: `e9ca61720e0fe9f7b5b977ec5000ae6002f4e200f8b3644fd19b6b9f94fd097e`.
Rule baseline: `origin/main` at `4a0acc5d886a12cafdfdeafbe1e71f4d0802bf55`.

Applied `.agents/skills/repo-review/SKILL.md` to the complete frozen authored diff. The only author inputs were the frozen diff and raw-evidence manifest. The review used independent production/integration, test/lockfile, deployment/documentation/Terraform, and evidence/provenance passes. Candidate findings were checked against the actual code and binding sources before consolidation. Changed blueprint and engineering documents were judged using their origin/main versions. No applicable rule document lacked an origin/main version.

## Findings

P1  src/Parchwell.Runner/Run/Cost/ActualCostReconciler.cs:25  AGENTS.md GOVERNANCE hard safety rule 4; docs/agents/factory.md "Item lifecycle" step 3; approved Revision 2 plan, enforced check 8 and "Persistent run storage and durable protocol"  Reconciliation accepts a cost record scoped only to the AKS cluster's primary resource group as the whole phase's settled actual cost, without establishing coverage of the separate AKS node resource group. It derives the sole acceptable ResourceScope by stripping the provider path from aks.id; RunLedger.ReconcileAsync requires exact equality and replaces the full reservation with that record's amount. A truthful Cost Management total for that primary group excludes the node VMs, networking and storage in the node group, so accepting it can understate committed spend and admit a later phase against the cumulative GBP 15 limit. A record carrying the node-group or broader subscription scope cannot pass the equality check. Retain the reservation until the actual-cost evidence covers the complete phase charge scope, including the independently obtained node resource group. Evidence: own infra/ephemeral/outputs.tf exposes only the cluster id/name/pools; pinned AKS AVM outputs.tf:98-109 exposes the separate node_resource_group_id; Microsoft's AKS FAQ, "Why are two resource groups created with AKS?", explicitly places node VMs/network/storage in the second group and states agent nodes are billed as VMs (https://github.com/MicrosoftDocs/azure-aks-docs/blob/6901de76c108a545a6b17fee61a39ca9e9e001d1/articles/aks/faq.yml#L110-L117 and #L36-L39). tests/Parchwell.Runner.Tests/Cost/ActualCostReconcilerTests.cs:55-64 demonstrates the primary-group match reducing committed spend from the reservation to the supplied actual. This finding concerns missing cost-coverage validation; the reconciler consumes a received cost record and does not itself execute a Cost Management query.
P1  src/Parchwell.Runner/Run/Deployment/DeploymentPreflight.cs:55  docs/engineering/dotnet.md "Seams and dependencies"  The new static preflight path constructs the ScenarioLoader service and DeploymentScenarioSource directly outside a composition root or a factory registered in dependency injection. The binding rule states: "Construction goes through dependency injection. No new of a service or client outside a composition root or a factory registered in DI." Program.cs already registers IScenarioSource and ScenarioLoader, but this method builds a separate graph instead of receiving those registered collaborators. Evidence: DeploymentPreflight.cs:54-56; Program.cs:51-53; origin/main docs/engineering/dotnet.md "Seams and dependencies". No explicit exception to this construction rule appears in the approved plan.
P1  src/Parchwell.Runner/Parchwell.Runner.csproj:1  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8, third paragraph  The diff adds Runner and Runner.Tests projects and assembly references: Runner to Scenarios/Kafka, Runner.Tests to Runner/LoadGen/Verifier, and ArchitectureTests to Runner. The rubric requires flagging new projects and assembly references. These are expressly required by the approved Revision 2 plan, so this is an approval-only finding: retain under rule 8's exception, list it in the PR description, and leave merge to Hari. It does not count toward review-round stop limits. Evidence: the new project files and tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj:21; approved plan "File changes and dependency boundary".

Findings: 3 at P0 or P1

Pre-existing (not blocking):

None identified.

## Verification and limits

- The complete diff contains 154 authored paths. No results/, expected.yaml, scenarios/, docs/adr/ or .github/workflows/ path changes. Executable workflow text under docs/operations remains authored review scope.
- All 448 manifest paths are unique, remain within the evidence raw/ tree, exist, and match their byte size and SHA-256. There are no unlisted retained raw files. Raw contents were opened only to check claims cited by authored files; other raw bytes were read for manifest integrity. The four producer maps agree with their manifest strings.
- The cited current Runner XML reports 204 passed and no failed, skipped or error cases. The cited build has zero warnings/errors. The cited Terraform report has 12 passed tests. The rehearsal reports three phases, five repeats per phase, three applies/destroys, no Azure calls and no replacement repeats. These are retained synthetic/local checks, not exact reviewed-head PR CI or live evidence. The resumed inventory hashes 518 repository inputs, all matching the frozen source. These counts were computed mechanically.
- All seven changed/new lockfiles parse. The four runtime-specific net10.0 dependency maps equal their projects' origin/main maps; there is no package-version drift. The removed-test-line scan found no loosened, deleted or skipped assertion. The new broker-boundary tests address the historical first-round gaps.
- The prior normal-failure artifact preservation and workflow-provenance findings were checked against the new collector, production wiring, bounded failure path, immutable export and related regressions. The separate recovery snapshot remains explicitly diagnostic and cannot authorize accepted counts.
- Deployment archive/smoke/Helm/real-service-tag acceptance and required exact-head PR CI remain explicitly incomplete in the README. Source inspection and local reports do not substitute for those executions. No live Azure call or new test/build execution was performed by this review.
- The implementation remained frozen: current added/context content was checked against the supplied diff and the repository worktree remained unchanged. Every reported location is an added line. Only scratch review files were written; no implementation edit, commit, push, PR or external comment was made.

## Sources checked

- Binding rules: AGENTS.md GOVERNANCE, "Skills", "Before opening a pull request", "Code Review Rules" and writing standards; CLAUDE.md; docs/agents/factory.md; blueprint ADRs and scope/deferred inventory; origin/main docs/engineering/dotnet.md and terraform.md; code-comment review exceptions.
- Revision 2 plan: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6321098789. Adopted runner checks: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/109#issuecomment-6313658165. Storage/recovery decision: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6332413257. Continuation override: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6333713470.
- Pinned AKS module node-group outputs: https://github.com/Azure/terraform-azurerm-avm-res-containerservice-managedcluster/blob/4bc0128b057d10d5ed146a53d8c77933fd00ead8/outputs.tf#L98-L109. Current official AKS FAQ was read and then checked at the immutable documentation commit cited in the cost finding.
- Pinned librdkafka broker naming/IPv6 behavior: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c#L141-L224.
- Deployment/source pass checked pinned AVM storage properties/outputs at https://github.com/Azure/terraform-azurerm-avm-res-storage-storageaccount/tree/5695e5ec21744a3765051677d9e82bfba16b5d98; Helm v4.3.1 official release checksum; action commit tags; Kubernetes-owned NetworkPolicy combined-selector semantics; and MicrosoftDocs table columns for Kafka Message and diagnostic ErrorMessage/ErrorCount/_ResourceId.

## Scope and rubric coverage

Every path uses the anything row: all AGENTS.md governance/process/review/writing rules, CLAUDE.md, docs/agents/factory.md and blueprint ADRs. Additional rows are recorded per path below. Changed engineering documents use their own origin/main text. Terraform paths use origin/main terraform.md and ADR-011. Kafka/settings paths additionally use the pinned workload settings/inventory traceability and ADR-014/015. No label-dependent approval exception is assumed to be satisfied by a label visible only outside the diff.

- `Directory.Build.props`: anything, .NET.
- `Parchwell.slnx`: anything, .NET.
- `charts/parchwell/Chart.yaml`: anything, Kafka/settings; ADR-014/015.
- `charts/parchwell/templates/_helpers.tpl`: anything, Kafka/settings; ADR-014/015.
- `charts/parchwell/templates/admission.yaml`: anything, Kafka/settings; ADR-014/015.
- `charts/parchwell/templates/generated-values.yaml`: anything, Kafka/settings; ADR-014/015.
- `charts/parchwell/templates/jobs.yaml`: anything, Kafka/settings; ADR-014/015.
- `charts/parchwell/templates/networkpolicies.yaml`: anything, Kafka/settings; ADR-014/015.
- `charts/parchwell/templates/serviceaccounts.yaml`: anything, Kafka/settings; ADR-014/015.
- `charts/parchwell/values.schema.json`: anything, Kafka/settings; ADR-014/015.
- `charts/parchwell/values.yaml`: anything, Kafka/settings; ADR-014/015.
- `docs/blueprint.md`: anything.
- `docs/engineering/dotnet.md`: anything, changed engineering document itself (origin/main).
- `docs/engineering/terraform.md`: anything, changed engineering document itself (origin/main).
- `docs/operations/a01-runner.md`: anything, Kafka/settings; ADR-014/015.
- `docs/operations/a01-runtime-limits.json`: anything, Kafka/settings; ADR-014/015.
- `docs/operations/evidence/issue-114/README.md`: anything.
- `docs/operations/evidence/issue-114/aks-binding-producers.json`: anything.
- `docs/operations/evidence/issue-114/cold-review-1.md`: anything.
- `docs/operations/evidence/issue-114/cold-review-2.md`: anything.
- `docs/operations/evidence/issue-114/local-producers.json`: anything.
- `docs/operations/evidence/issue-114/raw-manifest.json`: anything.
- `docs/operations/evidence/issue-114/resume-producers.json`: anything.
- `docs/operations/evidence/issue-114/resume-status.md`: anything.
- `docs/operations/evidence/issue-114/terraform-producers.json`: anything.
- `docs/operations/evidence/issue-114/write-raw-manifest.mjs`: anything.
- `docs/operations/kql/a01-runtime.kql`: anything.
- `docs/operations/loadgen-contract.md`: anything.
- `docs/operations/scenario-contract.md`: anything, Kafka/settings; ADR-014/015.
- `docs/operations/terraform.md`: anything.
- `docs/operations/verifier-contract.md`: anything.
- `docs/operations/workflows/a01-deployment-acceptance.yml.txt`: anything, Azure workflow trigger/security rules checked as executable text.
- `docs/operations/workflows/scenario.yml.txt`: anything, Azure workflow trigger/security rules checked as executable text.
- `docs/operations/workflows/sweeper.yml.txt`: anything, Azure workflow trigger/security rules checked as executable text.
- `infra/persistent/locals.tf`: anything, Terraform; ADR-011.
- `infra/persistent/outputs.tf`: anything, Terraform; ADR-011.
- `infra/persistent/storage.tf`: anything, Terraform; ADR-011.
- `scripts/fetch-service-tags.sh`: anything.
- `scripts/verification/assert-a01-deployment.py`: anything, Kafka/settings; ADR-014/015.
- `scripts/verify-a01-deployment.sh`: anything, Kafka/settings; ADR-014/015.
- `src/Parchwell.Kafka/packages.linux-x64.lock.json`: anything, .NET.
- `src/Parchwell.LoadGen/Parchwell.LoadGen.csproj`: anything, .NET.
- `src/Parchwell.LoadGen/Program.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `src/Parchwell.LoadGen/Run/Execution/LoadGenWorker.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `src/Parchwell.LoadGen/Run/Input/DeploymentAdmission.cs`: anything, .NET.
- `src/Parchwell.LoadGen/Run/Ledger/AssemblyInfo.cs`: anything, .NET.
- `src/Parchwell.LoadGen/packages.linux-x64.lock.json`: anything, .NET.
- `src/Parchwell.Runner/Parchwell.Runner.csproj`: anything, .NET.
- `src/Parchwell.Runner/Program.cs`: anything, .NET, Kafka/settings; ADR-014/015.
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
- `src/Parchwell.Runner/Run/Deployment/ChartRenderer.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Deployment/DeploymentGenerator.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `src/Parchwell.Runner/Run/Deployment/DeploymentPreflight.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Deployment/DeploymentScenarioSource.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Deployment/ServiceTagData.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Evidence/EvidenceExporter.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Evidence/KqlCollector.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Evidence/PrivateEvidenceCollector.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Evidence/PrivateProvenanceCollector.cs`: anything, .NET.
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
- `src/Parchwell.Scenarios/Generation/GeneratedValuesCompatibility.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `src/Parchwell.Scenarios/Generation/HelmValuesGenerator.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `src/Parchwell.Scenarios/packages.linux-x64.lock.json`: anything, .NET.
- `src/Parchwell.Verifier/AssemblyInfo.cs`: anything, .NET.
- `src/Parchwell.Verifier/Common/Kafka/VerifierKafkaSession.cs`: anything, .NET.
- `src/Parchwell.Verifier/Common/Serialization/DeploymentJson.cs`: anything, .NET.
- `src/Parchwell.Verifier/ImageSmoke.cs`: anything, .NET.
- `src/Parchwell.Verifier/Parchwell.Verifier.csproj`: anything, .NET.
- `src/Parchwell.Verifier/Program.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `src/Parchwell.Verifier/StartLine/BrokerInventoryCodec.cs`: anything, .NET.
- `src/Parchwell.Verifier/StartLine/StartLineHandler.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifierCompletionState.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifierHostOptions.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifierWorker.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifyRun/A01/A01EvidenceRecorder.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `src/Parchwell.Verifier/packages.linux-x64.lock.json`: anything, .NET.
- `tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj`: anything, .NET.
- `tests/Parchwell.ArchitectureTests/ProjectModel.cs`: anything, .NET.
- `tests/Parchwell.ArchitectureTests/packages.lock.json`: anything, .NET.
- `tests/Parchwell.LoadGen.Tests/Host/DeploymentAdmissionTests.cs`: anything, .NET.
- `tests/Parchwell.LoadGen.Tests/Host/GeneratedValuesCompatibilityTests.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `tests/Parchwell.LoadGen.Tests/Host/LoadGenWorkerHostTests.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `tests/Parchwell.Runner.Tests/ChartContractTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Cost/ActualCostReconcilerTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Cost/AzureCliRunStoreTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Cost/PhaseReservationTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Deployment/DeploymentGeneratorTests.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `tests/Parchwell.Runner.Tests/Deployment/DeploymentPreflightTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Deployment/ServiceTagDataTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Evidence/EvidenceExporterTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Evidence/PrivateProvenanceCollectorTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Evidence/RecoveryExportTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Evidence/azure-cli-query-recording.json`: anything.
- `tests/Parchwell.Runner.Tests/Execution/AksKubeconfigBindingTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Execution/BoundedProcessRunnerTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Execution/FailureArtifactPreservationTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Execution/GenerationGuardTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Execution/KubernetesWatchTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Execution/PhaseEvidenceInventoryTests.cs`: anything, .NET.
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
- `tests/Parchwell.Scenarios.Tests/GeneratedValuesCompatibilityTests.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `tests/Parchwell.Verifier.Tests/A01/A01C3EvidenceRecorderTests.cs`: anything, .NET.
- `tests/Parchwell.Verifier.Tests/Component/ComponentRunHarness.cs`: anything, .NET.
- `tests/Parchwell.Verifier.Tests/Host/BrokerInventoryTests.cs`: anything, .NET.
- `tests/Parchwell.Verifier.Tests/Host/C3HandlerTests.cs`: anything, .NET.
- `tests/Parchwell.Verifier.Tests/Host/GeneratedValuesCompatibilityTests.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `tests/Parchwell.Verifier.Tests/Host/ImageSmokeTests.cs`: anything, .NET.
- `tests/Parchwell.Verifier.Tests/Host/VerifierCompletionTests.cs`: anything, .NET.
- `tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs`: anything, .NET.
- `tests/Parchwell.Verifier.Tests/Support/HostEvidenceBuilder.cs`: anything, .NET.
- `tests/terraform/persistent/main.tftest.hcl`: anything, Terraform; ADR-011.

# Issue 114 cold review 9

Reviewed frozen authored branch diff: `/workspace/scratch/issue-114-cold-review-9.diff`.
Diff SHA-256: `f4bfed3145567a71af5dbabcd5ce37b62bcc82439c8108ad91631b26755ae6ac`.
Raw manifest: `docs/operations/evidence/issue-114/raw-manifest.json`.
Manifest SHA-256: `8c5bdc823d8c1b6fea84a47408180524d3e36060eb736a85c622784186672fc3`.
Source HEAD: `26b2fa6ba326b64fd7732c654c2a7077115b2595`.

Applied `origin/main:.agents/skills/repo-review/SKILL.md` as a stranger to the files. The complete authored diff contains 185 paths and 22,995 lines; only `docs/operations/evidence/**/raw/**` is excluded. No author explanation supplied review conclusions. The lead read the production changes, deployment/rehearsal changes and cross-component contracts, and reconciled all assigned path groups and their independent full-diff reads. No source was edited, no commit/push/external write was made, and no build, suite, coverage or live operation was run during review.

## Findings

P1  src/Parchwell.Runner/Run/Execution/KubernetesRunCluster.cs:161  docs/agents/factory.md "Item lifecycle" step 3 (implement the approved plan exactly); approved Revision 2 "Job/process deadlines" and enforced check 4; docs/operations/a01-runner.md "Bounds and lifecycle"  Normal downloads exceed the required 1 GiB per-repeat input ceiling before rejecting: DownloadPublicationAsync downloads and reads each entire next object before checking its aggregate, and the common DownloadAsync passes only the 256 MiB per-file ceiling.  Source lines 161-163 and 408-411 admit five listed objects of 268435456 bytes each, transferring/retaining/reading 1342177280 bytes before `publication_total_limit`, 268435456 bytes above 1073741824 (computed with Python). The earlier start-line/inventory and later result downloads are also outside that aggregate. WorkerBlobDownloader.cs:14-20 already checks Blob contentLength before transfer, but receives no remaining repeat allowance on this normal path. Failure/recovery collection already computes a remaining budget; normal collection must enforce the same required ceiling before transfer/allocation across all original repeat inputs. This is a required plan bound, not an additional experiment predicate or optional hardening check.
P1  src/Parchwell.Runner/Parchwell.Runner.csproj:1  AGENTS.md "Code Review Rules" (flag any new project, assembly reference or NuGet package)  New Runner project plus its Scenarios/Kafka references and Hosting/YamlDotNet package references require Hari's approval-only merge disposition.  New file lines 1,6-9 and Parchwell.slnx:8; approved Revision 2 "File changes and dependency boundary" expressly requires this project and these existing pinned dependencies; AGENTS.md "Before opening a pull request" rule 8 third paragraph permits retention and excludes this approval-only P1 from review-round stop limits.
P1  tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj:1  AGENTS.md "Code Review Rules" (flag any new project, assembly reference or NuGet package)  New Runner test project, its Runner/LoadGen/Verifier references and existing xUnit package reference require Hari's approval-only merge disposition.  New file lines 1,8-11 and Parchwell.slnx:16; approved Revision 2 "File changes and dependency boundary" expressly requires the test project and real-worker test references; the same rule 8 third-paragraph retention and stop-limit exclusion applies.
P1  tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj:21  AGENTS.md "Code Review Rules" (flag any new assembly reference)  Added Runner ProjectReference requires Hari's approval-only merge disposition.  Added line 21; approved Revision 2 "File changes and dependency boundary" explicitly requires ArchitectureTests to reference Runner and retain its existing dependency assertions; the same rule 8 third-paragraph retention and stop-limit exclusion applies.

Findings: 4 at P0 or P1

Pre-existing (not blocking):
None identified.

## Classification and reconciliation

There is one substantive correctness/contract P1 and three plan-required approval-only P1s. No P0 or P2 is established. Only the substantive download-bound finding counts toward review-round/churn limits; the three explicitly required project/reference/package findings are retained for Hari under rule 8 and must be listed in the pull-request description.

The same-class bound review covered normal producer publication and verifier/start-line downloads, failed-repeat preservation, fresh-process recovery, the common Blob downloader, PublicationAcceptance, phase/export ceilings and their boundary tests. The missing remaining-total allowance is specific to the normal common download path; failed-repeat and recovery paths already calculate it. The independent Execution reviewer and lead reached the same finding. The listing currently admits any six direct-child names at KubernetesRunCluster.cs:153-160; PublicationAcceptance.cs:17 checks the exact required names only after all transfers. Consequently a correction that reuses RetainedInputBytes must enforce that existing exact-six-name check before transfers, or account every actual downloaded path, so unknown or duplicate listing members cannot escape the canonical-name sum. This is the existing check 5 member-set predicate, not a proposed new validator. Existing per-file boundary tests do not exercise the normal cross-file ceiling. No large allocation or live download was needed to establish the source-level counterexample.

The second sweeper environment/FIC and runner allowlist text are an expressly approved protected design amendment/external prerequisite, not an unapproved new scope change: decision [6332413257](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6332413257), point 4, requires the exact main-only destroy-only configuration on the existing app. Scenario approval remains, no new identity/role assignment is declared, and Hari alone installs the workflow text and prerequisites. The unchanged origin/main blueprint/rules were used for review; the changed blueprint was not allowed to weaken its own review standard. The provisioned kubelogin prerequisite is also disclosed as Hari-only approval/readiness work, with no floating installation introduced.

Deployment acceptance remains explicitly incomplete: images/Helm/service-tag acquisition and exact-head PR CI evidence have not been established. The evidence README states those limits; fixture results do not claim live readiness or verdict acceptance. This review did not convert those disclosed missing external prerequisites into claims of success.

## Authority and rubric coverage

A applies to every listed path: origin/main AGENTS.md all GOVERNANCE, scope/deferred items, Code Review Rules, Before opening a pull request, Skills and Writing standards; CLAUDE.md; docs/agents/factory.md; blueprint ADRs 001-019. The unchanged origin/main forms of the changed blueprint and .NET/Terraform standards were applied. N adds docs/engineering/dotnet.md; T adds docs/engineering/terraform.md and ADR-011; E adds the changed engineering document's own origin/main rules; K adds the blueprint's pinned workload settings, inventory traceability and ADR-014/015. Governance, safety, verdict integrity, dependency/approval escalation, assertion preservation, source/number traceability and writing requirements were reconciled across all paths. Nothing touches `scenarios/`, `results/`, or `.github/workflows/`; those path-specific rows are not triggered. Workflow text was nevertheless checked for the Azure/main-only/privacy/teardown requirements under A.

The approved [Revision 2 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6321098789) and live storage/recovery/continuation decisions were read through REST as authority evidence. Relevant official/pinned source checks include Helm v4.3.1 checksum, AVM storage/AKS outputs, Azure CLI Blob preconditions and AKS get-credentials, kubelogin azurecli mode, Kubernetes policy selector/watch/log semantics, librdkafka v2.16.0 broker naming, AKS node-resource-group ownership, service tags and Azure Monitor table/column definitions. Detailed source checks are retained in the independent reports below.

Independent full assigned-diff reads: deployment/infra/scripts/workflow text 19 paths; Execution plus Program 30; Cost/Evidence/Contracts 32; workers/scenarios/architecture 44; docs/provenance 35. The lead additionally covered Runner project/locks, Deployment/Rehearsal, their tests and general wiring, and reconciled overlaps and the entire 185-path inventory. Subagent output was checked against the changed source and approved plan before inclusion.

The manifest has 511 unique raw entries; file sizes and hashes match. Ten producer maps (137 mapped commands), authored historical review records, current input hashes and cited local measurements were cross-checked. Current cited evidence supports 964 Runner tests, 33 focused LoadGen tests, 12 Terraform tests, the 3-by-5 rehearsal and 23 negatives, and nine workflow-preparation cases. These are local/dirty-source evidence with their recorded provenance, not exact-head PR CI or live acceptance. Raw contents were consulted only in support of authored citations; generated raw bytes are not part of the authored diff.

Independent reports:
- `/workspace/scratch/issue-114-cold-review-9-deployment-report.md`
- `/workspace/scratch/issue-114-cold-review-9-execution-report.md`
- `/workspace/scratch/issue-114-cold-review-9-workers-report.md`
- `/workspace/scratch/issue-114-review-9-docs-provenance-report.md`
- Cost/Evidence/Contracts reviewer returned a complete 32-path coverage record and no findings in the review thread.

## Every authored path

| Path | Rubric |
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
| `docs/engineering/dotnet.md` | A, E |
| `docs/engineering/terraform.md` | A, E |
| `docs/operations/a01-runner.md` | A, K |
| `docs/operations/a01-runtime-limits.json` | A |
| `docs/operations/evidence/issue-114/README.md` | A |
| `docs/operations/evidence/issue-114/acquisition-status-provenance.json` | A |
| `docs/operations/evidence/issue-114/acquisition-status.md` | A |
| `docs/operations/evidence/issue-114/aks-binding-producers.json` | A |
| `docs/operations/evidence/issue-114/chart-tool-acquisition-status.json` | A |
| `docs/operations/evidence/issue-114/cold-review-1.md` | A |
| `docs/operations/evidence/issue-114/cold-review-2.md` | A |
| `docs/operations/evidence/issue-114/cold-review-3.md` | A |
| `docs/operations/evidence/issue-114/cold-review-4.md` | A |
| `docs/operations/evidence/issue-114/cold-review-5.md` | A |
| `docs/operations/evidence/issue-114/cold-review-6.md` | A |
| `docs/operations/evidence/issue-114/cold-review-7.md` | A |
| `docs/operations/evidence/issue-114/cold-review-8.md` | A |
| `docs/operations/evidence/issue-114/local-producers.json` | A |
| `docs/operations/evidence/issue-114/placement-producers.json` | A |
| `docs/operations/evidence/issue-114/raw-manifest.json` | A |
| `docs/operations/evidence/issue-114/resume-producers.json` | A |
| `docs/operations/evidence/issue-114/resume-status.md` | A |
| `docs/operations/evidence/issue-114/review-4-producers.json` | A |
| `docs/operations/evidence/issue-114/review-5-producers.json` | A |
| `docs/operations/evidence/issue-114/review-7-producers.json` | A |
| `docs/operations/evidence/issue-114/review-8-producers.json` | A |
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
| `scripts/verification/assert-a01-deployment.py` | A |
| `scripts/verify-a01-deployment.sh` | A |
| `src/Parchwell.Kafka/packages.linux-x64.lock.json` | A, N |
| `src/Parchwell.LoadGen/Parchwell.LoadGen.csproj` | A, N |
| `src/Parchwell.LoadGen/Program.cs` | A, N |
| `src/Parchwell.LoadGen/Run/Execution/LoadGenWorker.cs` | A, N |
| `src/Parchwell.LoadGen/Run/Input/DeploymentAdmission.cs` | A, N |
| `src/Parchwell.LoadGen/Run/Input/LoadGenHostOptions.cs` | A, N |
| `src/Parchwell.LoadGen/Run/Ledger/AssemblyInfo.cs` | A, N |
| `src/Parchwell.LoadGen/packages.linux-x64.lock.json` | A, N |
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
| `src/Parchwell.Runner/Run/Deployment/ChartRenderer.cs` | A, N |
| `src/Parchwell.Runner/Run/Deployment/DeploymentGenerator.cs` | A, N, K |
| `src/Parchwell.Runner/Run/Deployment/DeploymentPreflight.cs` | A, N |
| `src/Parchwell.Runner/Run/Deployment/DeploymentRuntimeLimits.cs` | A, N |
| `src/Parchwell.Runner/Run/Deployment/DeploymentScenarioSource.cs` | A, N |
| `src/Parchwell.Runner/Run/Deployment/ServiceTagData.cs` | A, N |
| `src/Parchwell.Runner/Run/Evidence/EvidenceExporter.cs` | A, N |
| `src/Parchwell.Runner/Run/Evidence/KqlCollector.cs` | A, N |
| `src/Parchwell.Runner/Run/Evidence/PhaseCandidate.cs` | A, N |
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
| `src/Parchwell.Scenarios/packages.linux-x64.lock.json` | A, N |
| `src/Parchwell.Verifier/AssemblyInfo.cs` | A, N |
| `src/Parchwell.Verifier/Common/Kafka/VerifierKafkaSession.cs` | A, N |
| `src/Parchwell.Verifier/Common/Serialization/DeploymentJson.cs` | A, N |
| `src/Parchwell.Verifier/ImageSmoke.cs` | A, N |
| `src/Parchwell.Verifier/Parchwell.Verifier.csproj` | A, N |
| `src/Parchwell.Verifier/Program.cs` | A, N |
| `src/Parchwell.Verifier/StartLine/BrokerInventoryCodec.cs` | A, N |
| `src/Parchwell.Verifier/StartLine/StartLineHandler.cs` | A, N |
| `src/Parchwell.Verifier/VerifierCompletionState.cs` | A, N |
| `src/Parchwell.Verifier/VerifierHostOptions.cs` | A, N |
| `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | A, N |
| `src/Parchwell.Verifier/VerifierWorker.cs` | A, N |
| `src/Parchwell.Verifier/VerifyRun/A01/A01EvidenceRecorder.cs` | A, N |
| `src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs` | A, N |
| `src/Parchwell.Verifier/packages.linux-x64.lock.json` | A, N |
| `tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj` | A, N |
| `tests/Parchwell.ArchitectureTests/ProjectModel.cs` | A, N |
| `tests/Parchwell.ArchitectureTests/packages.lock.json` | A, N |
| `tests/Parchwell.LoadGen.Tests/Host/DeploymentAdmissionTests.cs` | A, N |
| `tests/Parchwell.LoadGen.Tests/Host/GeneratedValuesCompatibilityTests.cs` | A, N, K |
| `tests/Parchwell.LoadGen.Tests/Host/LoadGenWorkerHostTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/ChartContractTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Contracts/ExecutionBudgetsTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Cost/ActualCostReconcilerTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Cost/AzureCliRunStoreTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Cost/PhaseReservationTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Cost/RuntimeCostBoundaryTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentGeneratorTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentPreflightTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentRuntimeLimitsTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Deployment/ServiceTagDataTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/CandidateContractTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/EvidenceExportBoundaryTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/EvidenceExporterTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/KqlBoundaryTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/PhaseCandidateTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/PrivateProvenanceCollectorTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/PublicCandidateBoundaryTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/RecoveryExportTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/azure-cli-query-recording.json` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/AksKubeconfigBindingTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/BoundedProcessRunnerTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/FailureArtifactPreservationTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/GenerationGuardTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/KubernetesWatchTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/PhaseEvidenceBoundaryTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/PhaseEvidenceInventoryTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/PodAdmissionTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/PublicationBoundaryTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/RecoveryEvidenceTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/TerraformEnvironmentTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/WorkerBlobBoundaryTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/WorkerOutcomeBoundaryTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj` | A, N |
| `tests/Parchwell.Runner.Tests/RehearsalTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Support/WorkerWireFixtures.cs` | A, N |
| `tests/Parchwell.Runner.Tests/WireFixtureBoundsTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/WorkerPublicationFixture.cs` | A, N |
| `tests/Parchwell.Runner.Tests/WorkerWireCompatibilityTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/WorkerWireFixtureTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/packages.lock.json` | A, N |
| `tests/Parchwell.Scenarios.Tests/GeneratedValuesCompatibilityTests.cs` | A, N, K |
| `tests/Parchwell.Verifier.Tests/A01/A01C3EvidenceRecorderTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Component/ComponentRunHarness.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Host/BrokerInventoryTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Host/C3HandlerTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Host/GeneratedValuesCompatibilityTests.cs` | A, N, K |
| `tests/Parchwell.Verifier.Tests/Host/ImageSmokeTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Host/VerifierCompletionTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Support/HostEvidenceBuilder.cs` | A, N |
| `tests/terraform/persistent/main.tftest.hcl` | A, T |

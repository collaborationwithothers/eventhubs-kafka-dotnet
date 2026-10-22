# Issue 114 cold review 10

Frozen review diff: `/workspace/scratch/issue-114-cold-review-10.diff`.
Diff SHA-256: `c75d4c24598253c1ee24ba8ed1c4c9cfc52c4a86d1f6afcf0f746bea8b40822e`.
Raw manifest: `docs/operations/evidence/issue-114/raw-manifest.json`.
Manifest SHA-256: `eb74024f4bbb1a05a46c6e4e7ca8460bdb49ddd95622bf76a4ce92b3ef90ad67`.
Reviewed source HEAD: `edc7c5792e20df2c5dd42ecd65d6f349bfb0c4f0`.
Rule baseline: `origin/main` at `4a0acc5d886a12cafdfdeafbe1e71f4d0802bf55`.

Applied `origin/main:.agents/skills/repo-review/SKILL.md` to the complete authored branch diff as a stranger to the files. The supplied diff is byte-for-byte equal to the committed origin/main..HEAD diff with only `docs/operations/evidence/**/raw/**` excluded: 189 paths, 23922 lines and 1712759 bytes, computed by script. Historical review findings and author explanations are evidence, not current authority. All assigned authored hunks were read by fresh independent review groups; the lead read changed governing/worker contracts and Runner production/cross-component paths, checked the finding against primary sources, independently checked provenance claims, and reconciled the entire file/rule inventory. No source edits, builds, test suites, coverage, live commands, commits, pushes or external writes were performed during review.

## Findings

P1  docs/operations/kql/a01-runtime.kql:14  docs/agents/factory.md "Item lifecycle" step 3 (implement the approved plan exactly); approved #114 Revision 2 "Tests, KQL and workflow text" and enforced check 9; AGENTS.md "Before opening a pull request" rules 1-2  The required corroborating query cannot execute when both tables resolve: this arm projects ObservedErrorCount from ErrorCount, whose documented type is int, whereas line 10 projects the long literal 1. Default outer union creates ObservedErrorCount_int and ObservedErrorCount_long, so line 15 sum(ObservedErrorCount) references a missing column.  Evidence: the authored pinned AZMSDiagnosticErrorLogs schema at https://github.com/MicrosoftDocs/azure-monitor-docs/blob/6c8e97c9e8b8e57c19e3b319bda5188e4ceba029/articles/azure-monitor/reference/tables/azmsdiagnosticerrorlogs.md declares ErrorCount int; https://learn.microsoft.com/en-us/kusto/query/scalar-data-types/long says integer literals default to long; https://learn.microsoft.com/en-us/kusto/query/union-operator explicitly demonstrates separate x_long/x_int columns for print x=1 versus print x=toint(2). The independently read official Kusto binder confirms union unifies by name and type without numeric promotion. Cast ErrorCount to long, or make the first arm int, before union. KqlCollector.cs:33-38 submits this exact text and records query_failed; the allowed missing/delayed-log fallback does not implement a query that is always ill-typed for the cited schemas. This is a required plan output defect, not an additional experiment predicate or approval-only finding.
P1  src/Parchwell.Runner/Parchwell.Runner.csproj:1  AGENTS.md "Code Review Rules" (flag any new project, assembly reference or NuGet package)  The new Runner project, Scenarios/Kafka assembly references and Hosting/YamlDotNet package references require Hari's approval-only merge disposition.  Added lines 1 and 6-9, plus Parchwell.slnx:8; approved #114 Revision 2 "File changes and dependency boundary" expressly requires this project and these existing pinned dependencies. AGENTS.md "Before opening a pull request" rule 8 third paragraph permits retention, requires listing in the PR description and Hari's merge, and excludes this approval-only P1 from all review-round stop limits.
P1  tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj:1  AGENTS.md "Code Review Rules" (flag any new project, assembly reference or NuGet package)  The new Runner test project, existing xUnit package reference and Runner/LoadGen/Verifier assembly references require Hari's approval-only merge disposition.  Added lines 1 and 8-11, plus Parchwell.slnx:16; approved #114 Revision 2 "File changes and dependency boundary" expressly requires this test project and real-worker references. The same rule 8 third-paragraph retention, PR disclosure, Hari merge and stop-limit exclusion applies.
P1  tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj:21  AGENTS.md "Code Review Rules" (flag any new assembly reference)  The added Runner ProjectReference requires Hari's approval-only merge disposition.  Added line 21; approved #114 Revision 2 "File changes and dependency boundary" expressly requires the architecture reference/inventory addition while preserving existing forbidden dependency assertions. The same rule 8 third-paragraph retention, PR disclosure, Hari merge and stop-limit exclusion applies.

Findings: 4 at P0 or P1

Pre-existing (not blocking):
None established. No P2 finding was established.

## Classification and reconciliation

There is one substantive P1 and three plan-required approval-only P1s. Only the KQL defect counts toward substantive review-round/churn limits. The two solution registrations and matching lockfile project nodes are the same project changes, not additional findings. Friend-assembly attributes are not extra assembly references. Runtime-specific lockfiles retain the existing non-RID dependency graph and pinned versions.

The tenth review's download-accounting changes enforce the same fixed individual/aggregate ceilings before transfer across normal producer publication, earlier verifier originals, failed collection and recovery. Exact publication-member validation precedes Blob metadata/I/O; canonical retained files are reused. The lead reconciled the new normal-download tests, existing failure/recovery checks and common downloader against the required bound. No further defect was established in this class.

The same-class KQL reconciliation checked both union-arm schemas, the downstream aggregation and the actual CLI submission/response path. Official union semantics and the official binder agree, so this finding does not rely on running a live query. Missing tables or delayed logs remain legitimately unavailable; the current type mismatch fails independently of those operational conditions. Correcting it requires no new scientific threshold or broader validator scope.

The second sweeper environment/FIC and runner allowlist text follow the expressly approved protected design amendment in decision 6332413257 point 4. Scenario approval remains; the exact main-only destroy-only environment uses the existing app, with no new identity or role assignment. Hari installs the workflows and external prerequisites. Changed blueprint text was not used to weaken the origin/main review standard. The provisioned kubelogin prerequisite remains disclosed Hari-only readiness/approval work; no floating installation is introduced.

Deployment acceptance is explicitly incomplete: image archive/smoke, Helm rendering, real committed service-tag acquisition and exact-head PR CI evidence remain pending. The README does not claim those results. Local synthetic and dirty-source checks cannot substitute for required workflow acceptance or live readiness.

## Authority and rule coverage

Every path below receives A: origin/main AGENTS.md all GOVERNANCE (scope/deferred inventory, safety, verdict integrity, truth and verification, merge rule), Code Review Rules, Before opening a pull request, Skills and Writing standards; CLAUDE.md; docs/agents/factory.md; docs/blueprint.md ADR-001 through ADR-019. N additionally applies origin/main docs/engineering/dotnet.md. T adds origin/main docs/engineering/terraform.md and ADR-011. E adds the changed engineering document's own origin/main rules. K adds blueprint Workloads, inventory traceability and ADR-014/015 for Kafka settings/generation. Rules were applied across each matching file, including dependency/approval escalation, assertion preservation, source and numerical traceability, safety/scope, options/DI/clock, worker completion/oracle integrity and writing requirements. The changed blueprint and engineering documents all have origin/main versions, which were applied; no branch-only authority fallback was needed. No actual `.github/workflows/`, `scenarios/`, `results/` or `docs/adr/` path is changed. Workflow text still receives safety/identity/private-export/teardown checks through A.

Read approved #114 plan comment 6321098789 Revision 2, updated 2026-10-22T04:14:53Z, its standing-approval check 6332608289, decisions 6320840549/6332413257/6333713470, and the listed #109/#14 parent authority. Snapshot `/workspace/scratch/issue-114-authority-during-review-10` is identical to `/workspace/scratch/issue-114-authority-after-review-8` for full issue/comments JSON across all three items; no authority change was found. Drafts, historical findings and handoffs do not override recognized decisions.

Independent complete assigned reads cover Execution/Contracts/Rehearsal and their tests (49 paths), Cost/Evidence and tests (24), deployment/infrastructure/scripts/workflow text and related tests (37), workers/dependencies/architecture (46), and authored evidence/provenance (27). Six changed governing/worker-contract documents were reviewed by the lead. The path inventory below reconciles all 189 paths; overlaps/crossreads do not substitute for missing coverage.

Official source checks include the pinned/native broker-name and metadata APIs, worker/SDK actual writers/codecs, Helm release/checksum, pinned storage and AKS AVM outputs, Azure CLI conditional Blob/AKS/query behavior, kubelogin Azure CLI mode, Kubernetes selectors/watch/log semantics, Terraform absent-state behavior, service tags, .NET Guid parsing, the pinned KQL table schemas and official Kusto literal/union/binder semantics. Source details are in the independent reports; the lead directly checked the changed KQL, cited schemas/union example, dependency declarations and approved plan before adopting findings.

## Evidence integrity

All 524 raw manifest entries are unique, present, in scope and byte-size/SHA-256 correct; no raw file is unlisted. The 139 unique producers include source revision/status; all 150 exact commands across 11 producer maps agree with the manifest. Raw contents were read only to check authored citations; other raw bytes were processed only for integrity metadata. Historical rounds 1-9 were checked against their own frozen diff/manifest hashes and path/line counts, not against current counts.

Current cited evidence supports 1001 passing Runner tests, 33 focused LoadGen tests, 12 Terraform plan tests, zero build warnings/errors, the three-by-five rehearsal with 23 negative cases, and nine workflow-preparation cases. The cited current input inventory has 535 files whose bytes/hashes match the frozen source. The lead independently checked current XML totals/input hashes/rehearsal totals and historical diff hashes/counts. These are retained local/dirty-source measurements, not executions performed by this review and not exact-head CI acceptance. Acquisition conclusions/provenance and official Helm checksum match the cited captures; network denials do not establish smoke/render/service-tag acceptance.

Independent reports:

- `/workspace/scratch/issue-114-review-10-execution-report.md`
- `/workspace/scratch/issue-114-review-10-cost-evidence-report.md`
- `/workspace/scratch/issue-114-review-10-deployment-report.md`
- `/workspace/scratch/issue-114-review-10-workers-report.md`
- `/workspace/scratch/issue-114-review-10-docs-evidence-report.md`

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
| `docs/operations/evidence/issue-114/cold-review-9.md` | A |
| `docs/operations/evidence/issue-114/local-producers.json` | A |
| `docs/operations/evidence/issue-114/placement-producers.json` | A |
| `docs/operations/evidence/issue-114/raw-manifest.json` | A |
| `docs/operations/evidence/issue-114/resume-producers.json` | A |
| `docs/operations/evidence/issue-114/resume-status.md` | A |
| `docs/operations/evidence/issue-114/review-4-producers.json` | A |
| `docs/operations/evidence/issue-114/review-5-producers.json` | A |
| `docs/operations/evidence/issue-114/review-7-producers.json` | A |
| `docs/operations/evidence/issue-114/review-8-producers.json` | A |
| `docs/operations/evidence/issue-114/review-9-producers.json` | A |
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
| `src/Parchwell.LoadGen/Program.cs` | A, N, K |
| `src/Parchwell.LoadGen/Run/Execution/LoadGenWorker.cs` | A, N, K |
| `src/Parchwell.LoadGen/Run/Input/DeploymentAdmission.cs` | A, N |
| `src/Parchwell.LoadGen/Run/Input/LoadGenHostOptions.cs` | A, N |
| `src/Parchwell.LoadGen/Run/Ledger/AssemblyInfo.cs` | A, N |
| `src/Parchwell.LoadGen/packages.linux-x64.lock.json` | A, N |
| `src/Parchwell.Runner/Parchwell.Runner.csproj` | A, N |
| `src/Parchwell.Runner/Program.cs` | A, N, K |
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
| `src/Parchwell.Verifier/Program.cs` | A, N, K |
| `src/Parchwell.Verifier/StartLine/BrokerInventoryCodec.cs` | A, N |
| `src/Parchwell.Verifier/StartLine/StartLineHandler.cs` | A, N |
| `src/Parchwell.Verifier/VerifierCompletionState.cs` | A, N |
| `src/Parchwell.Verifier/VerifierHostOptions.cs` | A, N |
| `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | A, N |
| `src/Parchwell.Verifier/VerifierWorker.cs` | A, N |
| `src/Parchwell.Verifier/VerifyRun/A01/A01EvidenceRecorder.cs` | A, N |
| `src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs` | A, N, K |
| `src/Parchwell.Verifier/packages.linux-x64.lock.json` | A, N |
| `tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj` | A, N |
| `tests/Parchwell.ArchitectureTests/ProjectModel.cs` | A, N |
| `tests/Parchwell.ArchitectureTests/packages.lock.json` | A, N |
| `tests/Parchwell.LoadGen.Tests/Host/DeploymentAdmissionTests.cs` | A, N |
| `tests/Parchwell.LoadGen.Tests/Host/GeneratedValuesCompatibilityTests.cs` | A, N, K |
| `tests/Parchwell.LoadGen.Tests/Host/LoadGenWorkerHostTests.cs` | A, N, K |
| `tests/Parchwell.Runner.Tests/ChartContractTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Contracts/ExecutionBudgetsTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Cost/ActualCostReconcilerTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Cost/AzureCliRunStoreTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Cost/PhaseReservationTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Cost/RuntimeCostBoundaryTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentGeneratorTests.cs` | A, N, K |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentPreflightTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentRuntimeLimitsTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Deployment/ServiceTagDataTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/CandidateContractTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/EvidenceExportBoundaryTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/EvidenceExporterTests.cs` | A, N, K |
| `tests/Parchwell.Runner.Tests/Evidence/KqlBoundaryTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/PhaseCandidateTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/PrivateProvenanceCollectorTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/PublicCandidateBoundaryTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/RecoveryExportTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/azure-cli-query-recording.json` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/AksKubeconfigBindingTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/BoundedProcessRunnerTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/DownloadBudgetTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/FailureArtifactPreservationTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/GenerationGuardTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/KubernetesWatchTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Execution/NormalDownloadBudgetTests.cs` | A, N |
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
| `tests/Parchwell.Runner.Tests/WorkerPublicationFixture.cs` | A, N, K |
| `tests/Parchwell.Runner.Tests/WorkerWireCompatibilityTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/WorkerWireFixtureTests.cs` | A, N, K |
| `tests/Parchwell.Runner.Tests/packages.lock.json` | A, N |
| `tests/Parchwell.Scenarios.Tests/GeneratedValuesCompatibilityTests.cs` | A, N, K |
| `tests/Parchwell.Verifier.Tests/A01/A01C3EvidenceRecorderTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Component/ComponentRunHarness.cs` | A, N, K |
| `tests/Parchwell.Verifier.Tests/Host/BrokerInventoryTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Host/C3HandlerTests.cs` | A, N, K |
| `tests/Parchwell.Verifier.Tests/Host/GeneratedValuesCompatibilityTests.cs` | A, N, K |
| `tests/Parchwell.Verifier.Tests/Host/ImageSmokeTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Host/VerifierCompletionTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Support/HostEvidenceBuilder.cs` | A, N, K |
| `tests/terraform/persistent/main.tftest.hcl` | A, T |

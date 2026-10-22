# Issue 114 cold review 1

Reviewed frozen diff: `/workspace/scratch/issue-114-cold-review-1.diff`.
Diff SHA-256: `bc53daaaf710a88a20e2ceb466535b3fb52184d326854734e69a6f4807582fa4`.
Raw-evidence manifest: `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/raw-manifest.json`.
Manifest SHA-256: `2fc0bbfdb167af52cce7bab101798afb1f93123541b22db365eef993b1e4ed48`.

Applied `.agents/skills/repo-review/SKILL.md`. Binding documents are AGENTS.md, CLAUDE.md, docs/agents/factory.md, the blueprint ADRs, and the .NET/Terraform engineering standards. For the changed blueprint and engineering documents, the origin/main versions supply the review rules. The cited Revision 2 plan and decision 6332413257 were read as evidence; they authorize the intended new projects and storage/sweeper design.

## Scope

The frozen review diff touches 140 authored files. Every file is covered by the anything row; .NET source/tests additionally use dotnet.md, Terraform files use terraform.md and ADR-011, and the Kafka configuration/deployment paths use ADR-014/015 and the pinned workload settings. No results/, expected.yaml or .github/workflows/ path is changed.

- `Directory.Build.props`
- `Parchwell.slnx`
- `charts/parchwell/Chart.yaml`
- `charts/parchwell/templates/_helpers.tpl`
- `charts/parchwell/templates/admission.yaml`
- `charts/parchwell/templates/generated-values.yaml`
- `charts/parchwell/templates/jobs.yaml`
- `charts/parchwell/templates/networkpolicies.yaml`
- `charts/parchwell/templates/serviceaccounts.yaml`
- `charts/parchwell/values.schema.json`
- `charts/parchwell/values.yaml`
- `docs/blueprint.md`
- `docs/engineering/dotnet.md`
- `docs/engineering/terraform.md`
- `docs/operations/a01-runner.md`
- `docs/operations/a01-runtime-limits.json`
- `docs/operations/evidence/issue-114/README.md`
- `docs/operations/evidence/issue-114/aks-binding-producers.json`
- `docs/operations/evidence/issue-114/local-producers.json`
- `docs/operations/evidence/issue-114/raw-manifest.json`
- `docs/operations/evidence/issue-114/terraform-producers.json`
- `docs/operations/evidence/issue-114/write-raw-manifest.mjs`
- `docs/operations/kql/a01-runtime.kql`
- `docs/operations/loadgen-contract.md`
- `docs/operations/scenario-contract.md`
- `docs/operations/terraform.md`
- `docs/operations/verifier-contract.md`
- `docs/operations/workflows/a01-deployment-acceptance.yml.txt`
- `docs/operations/workflows/scenario.yml.txt`
- `docs/operations/workflows/sweeper.yml.txt`
- `infra/persistent/locals.tf`
- `infra/persistent/outputs.tf`
- `infra/persistent/storage.tf`
- `scripts/fetch-service-tags.sh`
- `scripts/verification/assert-a01-deployment.py`
- `scripts/verify-a01-deployment.sh`
- `src/Parchwell.Kafka/packages.linux-x64.lock.json`
- `src/Parchwell.LoadGen/Parchwell.LoadGen.csproj`
- `src/Parchwell.LoadGen/Program.cs`
- `src/Parchwell.LoadGen/Run/Execution/LoadGenWorker.cs`
- `src/Parchwell.LoadGen/Run/Input/DeploymentAdmission.cs`
- `src/Parchwell.LoadGen/Run/Ledger/AssemblyInfo.cs`
- `src/Parchwell.LoadGen/packages.linux-x64.lock.json`
- `src/Parchwell.Runner/Parchwell.Runner.csproj`
- `src/Parchwell.Runner/Program.cs`
- `src/Parchwell.Runner/Run/Contracts/ExecutionBudgets.cs`
- `src/Parchwell.Runner/Run/Contracts/IArtifactStore.cs`
- `src/Parchwell.Runner/Run/Contracts/IProcessRunner.cs`
- `src/Parchwell.Runner/Run/Contracts/IRunCluster.cs`
- `src/Parchwell.Runner/Run/Contracts/IRunGate.cs`
- `src/Parchwell.Runner/Run/Contracts/ITerraformEnvironment.cs`
- `src/Parchwell.Runner/Run/Contracts/WorkerIdentity.cs`
- `src/Parchwell.Runner/Run/Cost/ActualCostReconciler.cs`
- `src/Parchwell.Runner/Run/Cost/AzureCliRunStore.cs`
- `src/Parchwell.Runner/Run/Cost/LedgerModels.cs`
- `src/Parchwell.Runner/Run/Cost/PhaseReservation.cs`
- `src/Parchwell.Runner/Run/Cost/RunLedger.cs`
- `src/Parchwell.Runner/Run/Deployment/ChartRenderer.cs`
- `src/Parchwell.Runner/Run/Deployment/DeploymentGenerator.cs`
- `src/Parchwell.Runner/Run/Deployment/DeploymentPreflight.cs`
- `src/Parchwell.Runner/Run/Deployment/DeploymentScenarioSource.cs`
- `src/Parchwell.Runner/Run/Deployment/ServiceTagData.cs`
- `src/Parchwell.Runner/Run/Evidence/EvidenceExporter.cs`
- `src/Parchwell.Runner/Run/Evidence/KqlCollector.cs`
- `src/Parchwell.Runner/Run/Evidence/PrivateEvidenceCollector.cs`
- `src/Parchwell.Runner/Run/Evidence/PublicCandidate.cs`
- `src/Parchwell.Runner/Run/Execution/AksKubeconfigBinding.cs`
- `src/Parchwell.Runner/Run/Execution/BoundedProcessRunner.cs`
- `src/Parchwell.Runner/Run/Execution/BrokerInventory.cs`
- `src/Parchwell.Runner/Run/Execution/GenerationGuard.cs`
- `src/Parchwell.Runner/Run/Execution/KubernetesRunCluster.cs`
- `src/Parchwell.Runner/Run/Execution/LocalVerifierGate.cs`
- `src/Parchwell.Runner/Run/Execution/PhaseExecutor.cs`
- `src/Parchwell.Runner/Run/Execution/PodAdmission.cs`
- `src/Parchwell.Runner/Run/Execution/ProbeGateRunner.cs`
- `src/Parchwell.Runner/Run/Execution/PublicationAcceptance.cs`
- `src/Parchwell.Runner/Run/Execution/RepeatExecutor.cs`
- `src/Parchwell.Runner/Run/Execution/SweepExecutor.cs`
- `src/Parchwell.Runner/Run/Execution/TerraformEnvironment.cs`
- `src/Parchwell.Runner/Run/Execution/WorkerOutcome.cs`
- `src/Parchwell.Runner/Run/Rehearsal/RehearsalRunner.cs`
- `src/Parchwell.Runner/Run/Rehearsal/RehearsalScenarioFactory.cs`
- `src/Parchwell.Runner/Run/Rehearsal/RehearsalTransports.cs`
- `src/Parchwell.Runner/Run/Rehearsal/WireFixtures.cs`
- `src/Parchwell.Runner/packages.lock.json`
- `src/Parchwell.Scenarios/Generation/GeneratedValuesCompatibility.cs`
- `src/Parchwell.Scenarios/Generation/HelmValuesGenerator.cs`
- `src/Parchwell.Scenarios/packages.linux-x64.lock.json`
- `src/Parchwell.Verifier/AssemblyInfo.cs`
- `src/Parchwell.Verifier/Common/Kafka/VerifierKafkaSession.cs`
- `src/Parchwell.Verifier/Common/Serialization/DeploymentJson.cs`
- `src/Parchwell.Verifier/ImageSmoke.cs`
- `src/Parchwell.Verifier/Parchwell.Verifier.csproj`
- `src/Parchwell.Verifier/Program.cs`
- `src/Parchwell.Verifier/StartLine/BrokerInventoryCodec.cs`
- `src/Parchwell.Verifier/StartLine/StartLineHandler.cs`
- `src/Parchwell.Verifier/VerifierCompletionState.cs`
- `src/Parchwell.Verifier/VerifierHostOptions.cs`
- `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs`
- `src/Parchwell.Verifier/VerifierWorker.cs`
- `src/Parchwell.Verifier/VerifyRun/A01/A01EvidenceRecorder.cs`
- `src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs`
- `src/Parchwell.Verifier/packages.linux-x64.lock.json`
- `tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj`
- `tests/Parchwell.ArchitectureTests/ProjectModel.cs`
- `tests/Parchwell.ArchitectureTests/packages.lock.json`
- `tests/Parchwell.LoadGen.Tests/Host/DeploymentAdmissionTests.cs`
- `tests/Parchwell.LoadGen.Tests/Host/GeneratedValuesCompatibilityTests.cs`
- `tests/Parchwell.LoadGen.Tests/Host/LoadGenWorkerHostTests.cs`
- `tests/Parchwell.Runner.Tests/ChartContractTests.cs`
- `tests/Parchwell.Runner.Tests/Cost/ActualCostReconcilerTests.cs`
- `tests/Parchwell.Runner.Tests/Cost/AzureCliRunStoreTests.cs`
- `tests/Parchwell.Runner.Tests/Cost/PhaseReservationTests.cs`
- `tests/Parchwell.Runner.Tests/Deployment/DeploymentGeneratorTests.cs`
- `tests/Parchwell.Runner.Tests/Deployment/DeploymentPreflightTests.cs`
- `tests/Parchwell.Runner.Tests/Deployment/ServiceTagDataTests.cs`
- `tests/Parchwell.Runner.Tests/Evidence/EvidenceExporterTests.cs`
- `tests/Parchwell.Runner.Tests/Evidence/azure-cli-query-recording.json`
- `tests/Parchwell.Runner.Tests/Execution/AksKubeconfigBindingTests.cs`
- `tests/Parchwell.Runner.Tests/Execution/BoundedProcessRunnerTests.cs`
- `tests/Parchwell.Runner.Tests/Execution/GenerationGuardTests.cs`
- `tests/Parchwell.Runner.Tests/Execution/KubernetesWatchTests.cs`
- `tests/Parchwell.Runner.Tests/Execution/PodAdmissionTests.cs`
- `tests/Parchwell.Runner.Tests/Execution/TerraformEnvironmentTests.cs`
- `tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj`
- `tests/Parchwell.Runner.Tests/RehearsalTests.cs`
- `tests/Parchwell.Runner.Tests/Support/WorkerWireFixtures.cs`
- `tests/Parchwell.Runner.Tests/WorkerPublicationFixture.cs`
- `tests/Parchwell.Runner.Tests/WorkerWireCompatibilityTests.cs`
- `tests/Parchwell.Runner.Tests/WorkerWireFixtureTests.cs`
- `tests/Parchwell.Runner.Tests/packages.lock.json`
- `tests/Parchwell.Scenarios.Tests/GeneratedValuesCompatibilityTests.cs`
- `tests/Parchwell.Verifier.Tests/A01/A01C3EvidenceRecorderTests.cs`
- `tests/Parchwell.Verifier.Tests/Host/BrokerInventoryTests.cs`
- `tests/Parchwell.Verifier.Tests/Host/C3HandlerTests.cs`
- `tests/Parchwell.Verifier.Tests/Host/GeneratedValuesCompatibilityTests.cs`
- `tests/Parchwell.Verifier.Tests/Host/ImageSmokeTests.cs`
- `tests/Parchwell.Verifier.Tests/Host/VerifierCompletionTests.cs`
- `tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs`
- `tests/terraform/persistent/main.tftest.hcl`

## Findings

P1  src/Parchwell.Runner/Run/Execution/SweepExecutor.cs:22  docs/agents/factory.md "Item lifecycle" step 3; approved Revision 2 plan enforced checks 9-10  Sweep recovery exports only its own local output directory, rather than the aged generation's still-available evidence. The delivered sweeper passes a fresh `$A01_OUTPUT/recovery` directory and never populates it; SweepExecutor never reads the generation outputs, worker Blob artifacts or cluster records before guarded destruction. After an interrupted scenario this commits an empty manifest, marks export incomplete, and then deletes the ephemeral account/cluster containing recoverable original evidence. The plan explicitly requires "Capture the aged run-id/expiry, export that generation"; the linked storage decision point 3 requires private raw evidence before teardown.
P1  src/Parchwell.Verifier/StartLine/BrokerInventoryCodec.cs:24  docs/engineering/dotnet.md "Testing"  New verifier comparisons lack the required boundary/below/above tests. BrokerInventoryTests exercises invalid ports 0/65536 and ordinary 9093 but lacks passing 1/2/65534/65535 cases; it has no 252/253/254-character hostname cases for the new host-length bound. Its output-limit test uses only a one-byte failing allowance, without exact-size and neighboring allowances. The binding standard requires every comparison and boundary in verifier code to have boundary, below and above tests.
P1  src/Parchwell.Scenarios/Generation/GeneratedValuesCompatibility.cs:15  docs/engineering/dotnet.md "Seams and dependencies"  The new static compatibility helper constructs the HelmValuesGenerator service directly, outside a composition root or DI-registered factory. HelmValuesGenerator is already registered and injected as a service elsewhere. The binding rule says "Construction goes through dependency injection. No new of a service or client outside a composition root or a factory registered in DI." Supply the registered generator to this compatibility path instead.
P1  src/Parchwell.Runner/Parchwell.Runner.csproj:1  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8  The diff adds the Runner and Runner.Tests projects and assembly references, including Runner references to Scenarios/Kafka, test references to Runner/LoadGen/Verifier, and ArchitectureTests' reference to Runner. The rule requires flagging new projects and assembly references. These are approval-only findings explicitly required by the cited Revision 2 plan: retain only under rule 8's exception, list this finding in the PR description, and leave merge to Hari.

Findings: 4 at P0 or P1

Pre-existing (not blocking):

None identified.

## Verification notes

- The raw manifest has 410 entries; all retained raw file sizes and SHA-256 values match the supplied manifest.
- The suspected fresh-backend state-pull failure was disproved with the pinned Terraform 1.16.6 binary in an isolated local-backend fixture: after init, state pull exits 0 with empty stdout. It is not a finding.
- Pinned librdkafka v2.16.0 broker-name construction was checked against its cited source; the SASL_SSL name and IPv6 bracketing match.
- No live Azure operation, repository implementation edit, commit, push, PR creation or external comment was performed by this review. Required deployment/CI acceptance remains explicitly pending in the authored evidence README.

## Author dispositions

- Sweep evidence: fixed by `RecoveryEvidenceCollector`, required production wiring, a separate immutable private recovery export, and generation-guarded cleanup. Recovery preserves available original bytes and records unavailable history without accepted counts. Regression coverage is in `RecoveryEvidenceTests` and `RecoveryExportTests`.
- Verifier boundaries: fixed with host length, port, identifier, broker count and exact output-size neighbors in `BrokerInventoryTests`. The [focused test output](raw/local/broker-boundaries-final.log) records the passing run.
- Dependency injection: fixed by supplying the registered `HelmValuesGenerator` to the compatibility helper from both worker handlers. Existing assertions remain. The scoped compatibility test outputs are in `raw/local/di-fix-*-tests.log`.
- New projects and references: retained under AGENTS.md "Before opening a pull request" rule 8 because the checked Revision 2 plan requires them. This approval-only finding is escalated to Hari for merge; it is excluded from review-round stop limits by that rule.

This is the historical first-round report and its dispositions. The pull request self-review records subsequent cold-review results against their frozen diff hashes.

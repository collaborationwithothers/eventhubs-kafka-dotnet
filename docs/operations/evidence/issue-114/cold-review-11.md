# Issue 114 cold review 11

Fresh-context review of the complete authored branch diff against `origin/main`, using the supplied review diff and raw-evidence manifest as the only author-provided review inputs. The source remained frozen throughout review. Independent reviewers read complete assigned groups; the consolidating reviewer checked the governing/operational documents, the findings against their sources, the diff boundary and complete file coverage.

- Reviewed head: `d8b43a88729a74746a29d98fda1521b1407ea780`.
- Baseline: `4a0acc5d886a12cafdfdeafbe1e71f4d0802bf55`.
- Supplied diff: `/workspace/scratch/issue-114-cold-review-11.diff`; SHA-256 `b0b5e4422b7c59914961f65e7201589474a22991e66b95e71e052f237a57c769`; 1764813 bytes; 24414 lines; 193 changed authored files. Its bytes exactly match `git diff origin/main -- . ':!docs/operations/evidence/**/raw/**'`.
- Raw manifest: `docs/operations/evidence/issue-114/raw-manifest.json`; SHA-256 `765e986ed6257865f0e6730b61c7119fee352d1a905d2146a4d6e2f863651c75`. Only the permitted `docs/operations/evidence/**/raw/**` pathspec was excluded from the review diff; authored records, scripts, judgements and manifests were included.
- Static review only: no source edits, tests, builds, full-suite/coverage execution, live operations, pushes or external writes. Review artifacts were written only under `/workspace/scratch/`. Cited-source REST/HTTP reads were read-only.

## Findings

P0  docs/operations/workflows/sweeper.yml.txt:16  AGENTS.md "Code Review Rules" (flag any change that weakens identity scope); repo-review step 2; origin/main docs/blueprint.md section 9 "Control plane"  The second `parchwell-sweeper` environment expands the federation trust for the same apply-capable app without the existing required-human-review protection. The baseline trusts only the protected scenario environment requiring Hari's approval. The template uses the same client/app under an additional no-reviewer subject; invoking only `sweep` does not narrow that app's Azure permissions for that subject. Evidence: sweeper lines 16,19-25,78-87; a01-runner.md:86 and :88; branch blueprint:771 versus origin/main blueprint:769. Direct REST reading of [decision 6332413257](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6332413257), point 4, confirms this is intentional and authorized. This is a literal baseline-rule finding, not a claim of deviation from the approved plan. The unchanged P0 rule has no approved-plan exception, and rule 8's listed approval-only exception does not include this P0 class. Classification: substantive baseline-policy conflict, not approval-only.
P1  src/Parchwell.Verifier/Program.cs:368  origin/main docs/engineering/dotnet.md "Seams and dependencies" (configuration uses options with validation on start)  The new optional comparison setting binds an explicitly empty or whitespace-only path into `GeneratedValuesFile`, but `VerifierHostOptionsValidator` never validates this property. Startup therefore succeeds and `VerifyRunHandler` reads index, ledger, manifest, statistics, lifecycle and capabilities from Blob at lines 50-59 before `GeneratedValuesCompatibility.Validate` rejects the configured value at line 72. Keep absent/null optional, but validate a configured value through the existing startup validator, as the corresponding new LoadGen option does. This concerns validation of the configured path value; it does not require checking projected-file existence or contents at startup. Evidence: VerifierHostOptions.cs:45; Program.cs:368; full VerifierHostOptionsValidator; VerifyRunHandler.cs:50-72; GeneratedValuesCompatibility.cs:16. Classification: substantive engineering-rule violation, not approval-only.
P1  Parchwell.slnx:8  AGENTS.md "Code Review Rules" (flag any new project)  Adds `Parchwell.Runner` and `Parchwell.Runner.Tests` (also line 16 and their new project files). Evidence: supplied diff; approved Revision 2 "File changes and dependency boundary" explicitly requires both. Classification: approval-only under "Before opening a pull request" rule 8; retain only as required by that plan, list the finding and leave merge to Hari.
P1  src/Parchwell.Runner/Parchwell.Runner.csproj:6  AGENTS.md "Code Review Rules" (flag any new assembly reference)  Adds Runner references to Scenarios/Kafka, Runner-test references to Runner/LoadGen/Verifier and the ArchitectureTests reference to Runner. Evidence: this file lines 6-7; tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj:9-11; tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj:21; the approved Revision 2 dependency boundary requires these references. Classification: approval-only under rule 8; list and leave merge to Hari. These are grouped once across project and lockfile changes.
P1  src/Parchwell.Runner/Parchwell.Runner.csproj:8  AGENTS.md "Code Review Rules" (flag any new NuGet package)  Adds PackageReference declarations for Microsoft.Extensions.Hosting/YamlDotNet in Runner and xunit.v3.mtp-v2 in its new test project. Existing central pins and package versions do not remove the mandatory escalation for the new declarations. Evidence: this file lines 8-9; tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj:8; approved Revision 2 dependency boundary. Classification: approval-only under rule 8; list and leave merge to Hari. RID/transitive lock entries are not counted as additional findings.

Findings: 5 at P0 or P1

Pre-existing (not blocking):

None independently established in unchanged lines inspected. No P2 finding was established.

## Classification and scope limits

There are two non-approval findings and three grouped plan-required approval-only P1s. Rule 8 excludes the latter three from review-round stop counts before counting; it does not supply the same exception for the P0. The sweeper finding specifically records a conflict between the literal unchanged review rule/baseline and the explicitly approved trust amendment. It does not allege an accidental broad Azure role assignment, public endpoint, unapproved workflow installation or an implementation that ignores Hari's decision. The main-only source guard, destroy-only command, shared concurrency and Hari-only installation prerequisites were reviewed and are present.

Persistent plans/applies, protected design and verifier changes, workflow installation, provisioned kubelogin and live readiness remain with Hari. These ownership/readiness requirements are distinct from the three mandatory new-project/reference/package findings. No expected-outcome, verdict or results file changes, ready-for-agent label operation, new docs/adr file, actual .github/workflows edit, or PR-triggered Azure job is in this diff.

The disclosed acceptance gaps remain gaps: exact-head CI, real image archives/smokes, Helm render and service-tag acquisition are not established by this review. Authored evidence does not substitute fixtures or source inspection for those checks. This static review does not certify Azure readiness or live behavior.

## Rules and source verification

Read and applied the complete repository-owned `repo-review` skill and unchanged AGENTS.md, CLAUDE.md and docs/agents/factory.md. All GOVERNANCE rules, scope/deferred inventory, Code Review Rules, Before opening a pull request, Skills and writing standards were applied to every changed file. The blueprint ADRs and frozen scope were read. Because docs/blueprint.md, docs/engineering/dotnet.md and docs/engineering/terraform.md change, their origin/main versions supplied binding review rules; each changed engineering document was also reviewed against itself at that baseline. No required governing document lacked an origin/main version. New operational contracts were checked as implementation contracts, not used to weaken baseline rules.

Cross-file checks covered generated version-1 inputs and complete worker-specific equality, identity/admission and broker independence, completion receipts and actual exits, unchanged verifier judgement/count authorization, watch continuity through export, cumulative cost and scope settlement, conditional durable writes/readback, immutable private original bytes/manifest-last publication, public allowlists/redaction, bounded failures/recovery and generation-checked saved-plan cleanup, and all changed tests' assertion preservation. Kafka settings remain traceable to the A01/inventory rules; no unlinked baseline-setting change was established. All assigned tests were read; none was executed.

Direct source reads for the consolidating review included #114 decision 6332413257, Revision 2 6321098789 and #109 decision 6313658165 through REST. Relevant source pins were independently checked in the assigned groups and/or consolidation: native broker formatter at librdkafka v2.16.0; Azure CLI da4c0548a2725ea3d08b1d94b8e7fa1b0b07256e (storage preconditions, explicit non-admin AKS credentials and read-only ACR metadata); kubelogin bdf0e79b5a0ea6fe9fd3b7ada771378c344ad760 (Azure CLI login and subscription); AKS AVM 4bc0128b057d10d5ed146a53d8c77933fd00ead8 (nodeResourceGroup export); AKS FAQ 6901de76c108a545a6b17fee61a39ca9e9e001d1 (both resource groups); storage AVM 5695e5ec21744a3765051677d9e82bfba16b5d98; official Helm v4.3.1 release/checksum; MCR pinned index; Terraform v1.16.6 state pull; kubectl v0.35.0 raw/log transport; CLI query flattening at extensions 74172d7cf4637fdf039e6b9325498feaed459888. The MCR config-blob redirect was unavailable to one reviewer; resolved manifest-list identity/platform were verified, while acceptance gaps remain disclosed.

The KQL columns were read directly from MicrosoftDocs/azure-monitor-docs 6c8e97c9e8b8e57c19e3b319bda5188e4ceba029 (AZMSKafkaUserErrorLogs Message and AZMSDiagnosticErrorLogs ErrorMessage/int ErrorCount). The union type behavior and conversion were read directly from dataexplorer-docs 85351b30116da41b75c5a4e8a5b7f5730d81ab08 and ac19a887fb20eab5c08ec7742478621be92c56fd. The added query's conversion to long matches those sources; no new KQL defect was established.

## Evidence provenance coverage

The provenance reviewer covered every authored evidence file, including all ten historical cold-review records as authored content. The complete manifest metadata/producer audit found 535 unique listed raw files, 144 unique producer strings and 151 producer-map commands across 12 authored maps. Paths, existence, stat sizes, uniqueness, symlink absence, hash syntax and producer/source strings were checked across the manifest. These checks are distinct from reading and hashing every raw payload.

Raw payload content/hash reads were restricted to authored citations in README.md, acquisition-status.md, resume-status.md, the historical review/README records and the source audit. These include the current review-9 Runner/build/rehearsal/input-inventory records, review-10 query/docs/input-inventory records, the cited focused LoadGen/Terraform/workflow-preparation reports, the explicitly retained-attempt acquisition/build captures, and historical reports needed to check their cited counts. The consolidating reviewer also read the current README and parsed its three cited XML reports: 1001 Runner, 35 targeted query and 33 focused LoadGen tests. No report parsing was test execution.

The cited current measurements, historical frozen diff/manifest identities and historical path/rubric counts reproduce. All five pinned source-audit byte counts/hashes were checked against independently fetched official bytes. The provenance report records each citation class and the limits of payload inspection. It does not infer reviewed-head CI or deployment acceptance from dirty-worktree, historical, synthetic or post-execution inventories.

## Every-file rubric coverage

G = complete anything row: AGENTS rules above; CLAUDE.md; factory; blueprint ADRs, scope and deferred inventory. N = complete origin/main .NET engineering standards, including dependency/build/lock/test inputs. T = complete origin/main Terraform standards plus ADR-011. E = changed engineering document reviewed against its own origin/main version. K = blueprint Workloads/inventory and ADR-013/014/015 setting traceability. W = prospective workflow text also checked against safety rules 8/9, trigger/identity boundaries and installation ownership. G applies to all rows. No scenario/results file or installed workflow path is touched, so the corresponding path-triggered rubric rows have no omitted changed file.

Review groups: 46 Workers, 21 Infrastructure, 39 Runtime, 47 Cost/evidence, 31 Provenance, 9 Governing/operations documents. These mechanically sum to all 193 changed authored paths. Complete independent group notes are `/workspace/scratch/review11-workers.md`, `review11-infra.md`, `review11-runtime.md`, `review11-cost-evidence.md` and `review11-provenance.md`; historical authored cold reviews were examined as evidence, not adopted as review authority.

| Changed file | Rubric | Complete review group |
|---|---|---|
| `Directory.Build.props` | G, N | Workers |
| `Parchwell.slnx` | G, N | Workers |
| `charts/parchwell/Chart.yaml` | G, K | Infrastructure |
| `charts/parchwell/templates/_helpers.tpl` | G, K | Infrastructure |
| `charts/parchwell/templates/admission.yaml` | G, K | Infrastructure |
| `charts/parchwell/templates/generated-values.yaml` | G, K | Infrastructure |
| `charts/parchwell/templates/jobs.yaml` | G, K | Infrastructure |
| `charts/parchwell/templates/networkpolicies.yaml` | G, K | Infrastructure |
| `charts/parchwell/templates/serviceaccounts.yaml` | G, K | Infrastructure |
| `charts/parchwell/values.schema.json` | G, K | Infrastructure |
| `charts/parchwell/values.yaml` | G, K | Infrastructure |
| `docs/blueprint.md` | G | Governing/operations docs |
| `docs/engineering/dotnet.md` | G, E | Governing/operations docs |
| `docs/engineering/terraform.md` | G, E | Governing/operations docs |
| `docs/operations/a01-runner.md` | G, K | Governing/operations docs |
| `docs/operations/a01-runtime-limits.json` | G, K | Governing/operations docs |
| `docs/operations/evidence/issue-114/README.md` | G | Provenance |
| `docs/operations/evidence/issue-114/acquisition-status-provenance.json` | G | Provenance |
| `docs/operations/evidence/issue-114/acquisition-status.md` | G | Provenance |
| `docs/operations/evidence/issue-114/aks-binding-producers.json` | G | Provenance |
| `docs/operations/evidence/issue-114/chart-tool-acquisition-status.json` | G | Provenance |
| `docs/operations/evidence/issue-114/cold-review-1.md` | G | Provenance |
| `docs/operations/evidence/issue-114/cold-review-10.md` | G | Provenance |
| `docs/operations/evidence/issue-114/cold-review-2.md` | G | Provenance |
| `docs/operations/evidence/issue-114/cold-review-3.md` | G | Provenance |
| `docs/operations/evidence/issue-114/cold-review-4.md` | G | Provenance |
| `docs/operations/evidence/issue-114/cold-review-5.md` | G | Provenance |
| `docs/operations/evidence/issue-114/cold-review-6.md` | G | Provenance |
| `docs/operations/evidence/issue-114/cold-review-7.md` | G | Provenance |
| `docs/operations/evidence/issue-114/cold-review-8.md` | G | Provenance |
| `docs/operations/evidence/issue-114/cold-review-9.md` | G | Provenance |
| `docs/operations/evidence/issue-114/local-producers.json` | G | Provenance |
| `docs/operations/evidence/issue-114/placement-producers.json` | G | Provenance |
| `docs/operations/evidence/issue-114/raw-manifest.json` | G | Provenance |
| `docs/operations/evidence/issue-114/resume-producers.json` | G | Provenance |
| `docs/operations/evidence/issue-114/resume-status.md` | G | Provenance |
| `docs/operations/evidence/issue-114/review-10-fetch-sources.py` | G | Provenance |
| `docs/operations/evidence/issue-114/review-10-kql-audit.json` | G | Provenance |
| `docs/operations/evidence/issue-114/review-10-producers.json` | G | Provenance |
| `docs/operations/evidence/issue-114/review-4-producers.json` | G | Provenance |
| `docs/operations/evidence/issue-114/review-5-producers.json` | G | Provenance |
| `docs/operations/evidence/issue-114/review-7-producers.json` | G | Provenance |
| `docs/operations/evidence/issue-114/review-8-producers.json` | G | Provenance |
| `docs/operations/evidence/issue-114/review-9-producers.json` | G | Provenance |
| `docs/operations/evidence/issue-114/review-fix-producers.json` | G | Provenance |
| `docs/operations/evidence/issue-114/terraform-producers.json` | G | Provenance |
| `docs/operations/evidence/issue-114/write-raw-manifest.mjs` | G | Provenance |
| `docs/operations/kql/a01-runtime.kql` | G | Governing/operations docs |
| `docs/operations/loadgen-contract.md` | G, K | Governing/operations docs |
| `docs/operations/scenario-contract.md` | G, K | Governing/operations docs |
| `docs/operations/terraform.md` | G | Infrastructure |
| `docs/operations/verifier-contract.md` | G, K | Governing/operations docs |
| `docs/operations/workflows/a01-deployment-acceptance.yml.txt` | G, W | Infrastructure |
| `docs/operations/workflows/scenario.yml.txt` | G, W | Infrastructure |
| `docs/operations/workflows/sweeper.yml.txt` | G, W | Infrastructure |
| `infra/persistent/locals.tf` | G, T | Infrastructure |
| `infra/persistent/outputs.tf` | G, T | Infrastructure |
| `infra/persistent/storage.tf` | G, T | Infrastructure |
| `scripts/fetch-service-tags.sh` | G | Infrastructure |
| `scripts/verification/assert-a01-deployment.py` | G | Infrastructure |
| `scripts/verify-a01-deployment.sh` | G | Infrastructure |
| `src/Parchwell.Kafka/packages.linux-x64.lock.json` | G, N | Workers |
| `src/Parchwell.LoadGen/Parchwell.LoadGen.csproj` | G, N | Workers |
| `src/Parchwell.LoadGen/Program.cs` | G, N | Workers |
| `src/Parchwell.LoadGen/Run/Execution/LoadGenWorker.cs` | G, N, K | Workers |
| `src/Parchwell.LoadGen/Run/Input/DeploymentAdmission.cs` | G, N, K | Workers |
| `src/Parchwell.LoadGen/Run/Input/LoadGenHostOptions.cs` | G, N | Workers |
| `src/Parchwell.LoadGen/Run/Ledger/AssemblyInfo.cs` | G, N | Workers |
| `src/Parchwell.LoadGen/packages.linux-x64.lock.json` | G, N | Workers |
| `src/Parchwell.Runner/Parchwell.Runner.csproj` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Program.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Contracts/ExecutionBudgets.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Contracts/IArtifactStore.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Contracts/IProcessRunner.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Contracts/IRunCluster.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Contracts/IRunGate.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Contracts/ITerraformEnvironment.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Contracts/WorkerIdentity.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Cost/ActualCostReconciler.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Cost/AzureCliRunStore.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Cost/LedgerModels.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Cost/PhaseReservation.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Cost/RunLedger.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Deployment/ChartRenderer.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Deployment/DeploymentGenerator.cs` | G, N, K | Cost/evidence |
| `src/Parchwell.Runner/Run/Deployment/DeploymentPreflight.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Deployment/DeploymentRuntimeLimits.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Deployment/DeploymentScenarioSource.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Deployment/ServiceTagData.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Evidence/EvidenceExporter.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Evidence/KqlCollector.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Evidence/PhaseCandidate.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Evidence/PrivateEvidenceCollector.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Evidence/PrivateProvenanceCollector.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Evidence/PublicCandidate.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Execution/AksKubeconfigBinding.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Execution/BoundedProcessRunner.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Execution/BrokerInventory.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Execution/GenerationGuard.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Execution/KubernetesRunCluster.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Execution/LocalVerifierGate.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Execution/PhaseExecutor.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Execution/PodAdmission.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Execution/ProbeGateRunner.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Execution/PublicationAcceptance.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Execution/RecoveryEvidenceCollector.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Execution/RepeatExecutor.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Execution/SweepExecutor.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Execution/TerraformEnvironment.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Execution/WorkerBlobDownloader.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Execution/WorkerOutcome.cs` | G, N | Runtime |
| `src/Parchwell.Runner/Run/Rehearsal/RehearsalRunner.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Rehearsal/RehearsalScenarioFactory.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Rehearsal/RehearsalTransports.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/Run/Rehearsal/WireFixtures.cs` | G, N | Cost/evidence |
| `src/Parchwell.Runner/packages.lock.json` | G, N | Cost/evidence |
| `src/Parchwell.Scenarios/Generation/GeneratedValuesCompatibility.cs` | G, N, K | Workers |
| `src/Parchwell.Scenarios/Generation/HelmValuesGenerator.cs` | G, N, K | Workers |
| `src/Parchwell.Scenarios/packages.linux-x64.lock.json` | G, N | Workers |
| `src/Parchwell.Verifier/AssemblyInfo.cs` | G, N | Workers |
| `src/Parchwell.Verifier/Common/Kafka/VerifierKafkaSession.cs` | G, N | Workers |
| `src/Parchwell.Verifier/Common/Serialization/DeploymentJson.cs` | G, N | Workers |
| `src/Parchwell.Verifier/ImageSmoke.cs` | G, N | Workers |
| `src/Parchwell.Verifier/Parchwell.Verifier.csproj` | G, N | Workers |
| `src/Parchwell.Verifier/Program.cs` | G, N | Workers |
| `src/Parchwell.Verifier/StartLine/BrokerInventoryCodec.cs` | G, N | Workers |
| `src/Parchwell.Verifier/StartLine/StartLineHandler.cs` | G, N | Workers |
| `src/Parchwell.Verifier/VerifierCompletionState.cs` | G, N | Workers |
| `src/Parchwell.Verifier/VerifierHostOptions.cs` | G, N | Workers |
| `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | G, N | Workers |
| `src/Parchwell.Verifier/VerifierWorker.cs` | G, N | Workers |
| `src/Parchwell.Verifier/VerifyRun/A01/A01EvidenceRecorder.cs` | G, N, K | Workers |
| `src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs` | G, N, K | Workers |
| `src/Parchwell.Verifier/packages.linux-x64.lock.json` | G, N | Workers |
| `tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj` | G, N | Workers |
| `tests/Parchwell.ArchitectureTests/ProjectModel.cs` | G, N | Workers |
| `tests/Parchwell.ArchitectureTests/packages.lock.json` | G, N | Workers |
| `tests/Parchwell.LoadGen.Tests/Host/DeploymentAdmissionTests.cs` | G, N, K | Workers |
| `tests/Parchwell.LoadGen.Tests/Host/GeneratedValuesCompatibilityTests.cs` | G, N, K | Workers |
| `tests/Parchwell.LoadGen.Tests/Host/LoadGenWorkerHostTests.cs` | G, N, K | Workers |
| `tests/Parchwell.Runner.Tests/ChartContractTests.cs` | G, N | Infrastructure |
| `tests/Parchwell.Runner.Tests/Contracts/ExecutionBudgetsTests.cs` | G, N | Runtime |
| `tests/Parchwell.Runner.Tests/Cost/ActualCostReconcilerTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Cost/AzureCliRunStoreTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Cost/PhaseReservationTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Cost/RuntimeCostBoundaryTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentGeneratorTests.cs` | G, N, K | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentPreflightTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentRuntimeLimitsTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Deployment/ServiceTagDataTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Evidence/CandidateContractTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Evidence/EvidenceExportBoundaryTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Evidence/EvidenceExporterTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Evidence/KqlBoundaryTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Evidence/PhaseCandidateTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Evidence/PrivateProvenanceCollectorTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Evidence/PublicCandidateBoundaryTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Evidence/RecoveryExportTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Evidence/azure-cli-query-recording.json` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Execution/AksKubeconfigBindingTests.cs` | G, N | Runtime |
| `tests/Parchwell.Runner.Tests/Execution/BoundedProcessRunnerTests.cs` | G, N | Runtime |
| `tests/Parchwell.Runner.Tests/Execution/DownloadBudgetTests.cs` | G, N | Runtime |
| `tests/Parchwell.Runner.Tests/Execution/FailureArtifactPreservationTests.cs` | G, N | Runtime |
| `tests/Parchwell.Runner.Tests/Execution/GenerationGuardTests.cs` | G, N | Runtime |
| `tests/Parchwell.Runner.Tests/Execution/KubernetesWatchTests.cs` | G, N | Runtime |
| `tests/Parchwell.Runner.Tests/Execution/NormalDownloadBudgetTests.cs` | G, N | Runtime |
| `tests/Parchwell.Runner.Tests/Execution/PhaseEvidenceBoundaryTests.cs` | G, N | Runtime |
| `tests/Parchwell.Runner.Tests/Execution/PhaseEvidenceInventoryTests.cs` | G, N | Runtime |
| `tests/Parchwell.Runner.Tests/Execution/PodAdmissionTests.cs` | G, N | Runtime |
| `tests/Parchwell.Runner.Tests/Execution/PublicationBoundaryTests.cs` | G, N | Runtime |
| `tests/Parchwell.Runner.Tests/Execution/RecoveryEvidenceTests.cs` | G, N | Runtime |
| `tests/Parchwell.Runner.Tests/Execution/TerraformEnvironmentTests.cs` | G, N | Runtime |
| `tests/Parchwell.Runner.Tests/Execution/WorkerBlobBoundaryTests.cs` | G, N | Runtime |
| `tests/Parchwell.Runner.Tests/Execution/WorkerOutcomeBoundaryTests.cs` | G, N | Runtime |
| `tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj` | G, N | Workers |
| `tests/Parchwell.Runner.Tests/RehearsalTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/Support/WorkerWireFixtures.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/WireFixtureBoundsTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/WorkerPublicationFixture.cs` | G, N, K | Cost/evidence |
| `tests/Parchwell.Runner.Tests/WorkerWireCompatibilityTests.cs` | G, N | Cost/evidence |
| `tests/Parchwell.Runner.Tests/WorkerWireFixtureTests.cs` | G, N, K | Cost/evidence |
| `tests/Parchwell.Runner.Tests/packages.lock.json` | G, N | Workers |
| `tests/Parchwell.Scenarios.Tests/GeneratedValuesCompatibilityTests.cs` | G, N, K | Workers |
| `tests/Parchwell.Verifier.Tests/A01/A01C3EvidenceRecorderTests.cs` | G, N | Workers |
| `tests/Parchwell.Verifier.Tests/Component/ComponentRunHarness.cs` | G, N | Workers |
| `tests/Parchwell.Verifier.Tests/Host/BrokerInventoryTests.cs` | G, N | Workers |
| `tests/Parchwell.Verifier.Tests/Host/C3HandlerTests.cs` | G, N | Workers |
| `tests/Parchwell.Verifier.Tests/Host/GeneratedValuesCompatibilityTests.cs` | G, N, K | Workers |
| `tests/Parchwell.Verifier.Tests/Host/ImageSmokeTests.cs` | G, N | Workers |
| `tests/Parchwell.Verifier.Tests/Host/VerifierCompletionTests.cs` | G, N | Workers |
| `tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | G, N | Workers |
| `tests/Parchwell.Verifier.Tests/Support/HostEvidenceBuilder.cs` | G, N | Workers |
| `tests/terraform/persistent/main.tftest.hcl` | G, T | Infrastructure |

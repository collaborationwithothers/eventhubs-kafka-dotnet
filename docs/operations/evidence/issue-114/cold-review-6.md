# Issue 114 cold review 6

Frozen authored diff: `/workspace/scratch/issue-114-cold-review-6.diff`.
Diff SHA-256: `1be3e1ee2fff3124e03a22ce82b7afca7e76bcef2068f969c0aaa63dc9d0ffac`.
Raw-evidence manifest: `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/raw-manifest.json`.
Manifest SHA-256: `f0903692bf25cdecacdc70fd884ccab75d40f40733a5e869b0e63ed2a7b90d4b`.
Review-rule baseline: `origin/main` at `4a0acc5d886a12cafdfdeafbe1e71f4d0802bf55`.

Applied `.agents/skills/repo-review/SKILL.md` to the complete frozen authored scope. The only author inputs were the diff and manifest paths. The lead retained full-scope review responsibility, independent production/contract reading and cross-file/source checks; three fresh-context groups independently read runtime/tests/locks, infrastructure/chart/scripts and documentation/evidence. Their reports were checked before consolidation. Changed blueprint and engineering authorities use their `origin/main` versions. No required authority lacked an `origin/main` version.

## Findings

P1  docs/operations/evidence/issue-114/raw-manifest.json:77  AGENTS.md "Before opening a pull request" rule 8  Move the hand-authored acquisition status receipt out of raw/. This manifest entry records PYEDIT reading an existing raw/chart-tool-acquisition/receipt.json and assigning literal conclusions, including sdk_locked_linux_x64_restore_build_succeeded=True, sdk_container_archive_produced=False and image_smoke_executed=False, plus a blocked host. That is authored synthesis rather than original program-observed output. Rule 8 requires the review diff to contain judgements and explicitly says "Keep authored files outside raw folders." The receipt currently occupies the excluded/generated raw path. The transparent producer command makes this detectable but does not satisfy the placement rule. Evidence: the producer on line 77 contains those literal assignments and also modifies scripts/fetch-service-tags.sh; .gitattributes marks the evidence raw path generated and the frozen authored diff excludes that receipt. Retain original acquisition/tool captures under raw, move this authored receipt outside raw and update its manifest/citations. This finding does not assert that its status conclusions are false; it concerns the required review boundary.
P1  src/Parchwell.Runner/Parchwell.Runner.csproj:1  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8, third paragraph  The diff adds the Runner project, Scenarios/Kafka references and Hosting/YamlDotNet package references, which the mandatory review rule requires flagging. The approved Revision 2 plan "File changes and dependency boundary" expressly requires these additions. Retain only under the approval-only exception, list this finding in the PR description and leave merge to Hari. Existing central pins do not remove the approval gate. Evidence: this project lines 1-10 and Parchwell.slnx:8. This finding is excluded from review-round stop limits by rule 8.
P1  tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj:1  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8, third paragraph  The diff adds Runner.Tests, its xUnit package reference and references to Runner/LoadGen/Verifier. The approved Revision 2 plan expressly requires them. Retain under the approval-only exception, list the finding in the PR description and leave merge to Hari. Evidence: this project lines 1-11 and Parchwell.slnx:16. This finding is excluded from review-round stop limits.
P1  tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj:21  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8, third paragraph  This adds the Runner assembly reference. The approved plan expressly requires updating the architecture inventory and reference. Retain under the approval-only exception, list it in the PR description and leave merge to Hari. Evidence: changed ProjectReference at line 21 and ProjectModel.cs's expanded project/service inventories. This finding is excluded from review-round stop limits.

Findings: 4 at P0 or P1

Pre-existing (not blocking):

None identified.

## Verification, disposition and limits

- One substantive P1 was confirmed: authored acquisition status synthesis is stored in excluded raw evidence. The other three P1s are mandatory plan-required approval findings. Rule 8's approval-only exception applies to those three before stop-limit counting. No P0 or P2 was confirmed.
- The frozen diff contains 161 authored paths and 19464 lines, computed by script. Every added/context line was matched against the frozen worktree with zero mismatches. All finding locations are changed lines. No results/, expected.yaml, scenarios/, docs/adr/ or .github/workflows/ path changes. Installable workflow text remains authored review scope.
- All 481 manifest paths are unique, remain inside the evidence raw tree, exist and match recorded byte size/SHA-256. The authored/raw classification violation above remains despite correct integrity hashes. No retained raw file is unlisted. All seven producer maps agree with their manifest command/source/status strings. Raw content was opened only for authored citations; uncited raw bytes were processed only for integrity hashing.
- The authored README's current cited Runner report contains 271 passed, zero failed/errors/skipped; focused LoadGen contains 33 passed, zero failed/errors/skipped. The cited build reports zero warnings/errors and Terraform reports 12 passing mock plan tests. Current rehearsal reports three phases, five repeats per phase, three applies/destroys, zero Azure calls/replacements and 23 negative cases. The nine-case workflow preparation report agrees with its retained supporting script. The cited current repository input inventory has 518 entries, all matching the frozen inputs. These are dirty-worktree local reports, as the authored evidence states.
- The same-class producer check examined all 101 unique manifest producer commands for manual conclusion assignments. Only the flagged acquisition receipt was identified as an additional authored status synthesis; executable test/assertion outputs and program-measured source inventories remain raw evidence.
- Historical frozen review records retain their own diff/manifest identities and counts. Later evidence was not substituted for the earlier rounds. The continuation decision and authored resume-status record preserve churn trips and do not claim to relax the three-round Codex cap.
- All seven changed/new package locks parse. The four linux-x64 locks retain the existing common net10.0 dependency maps and native/library 2.16.0 pins. No additional package version drift was found. Existing test edits retain assertions; changes supply constructor arguments or strengthen coverage.
- Independently ran 79 selected tests from the existing Release binaries, covering WorkerWireCompatibilityTests, FailureArtifactPreservationTests, DeploymentPreflightTests and TerraformEnvironmentTests. All 79 passed with zero failures/errors/skips. Exact command: `timeout 120 /workspace/scratch/dotnet/dotnet tests/Parchwell.Runner.Tests/bin/Release/net10.0/Parchwell.Runner.Tests.dll -class '*WorkerWireCompatibilityTests' -class '*FailureArtifactPreservationTests' -class '*DeploymentPreflightTests' -class '*TerraformEnvironmentTests' -result-xml /workspace/scratch/issue-114-cold-review-6-selected-tests.xml > /workspace/scratch/issue-114-cold-review-6-selected-tests.log 2>&1`. This selected existing-binary run is additional review evidence, not a full build or exact-reviewed-head CI substitute.
- The previous worker-observation finding is addressed by bounded pod/termination/log caching before dependent Blob failures, independent retention, per-file failure isolation and production-path fake-transport regressions. Normal acceptance still requires actual exits, receipts, provenance and result agreement. Original Terraform output is retained before its dependent state read. Required subscription redaction input is validated before production transport construction, populated privately by all phase preparation blocks and rejects padding. The positive real-codec/receipt test accepts exactly six supplied counts, with mismatched variants rejecting them.
- The storage/sweeper design matches the explicit adopted decision: private Entra-only run storage, no new grants, an existing-app main-only destroy-recovery environment and external prerequisites for Hari. The change remains protected-path review work and does not establish live readiness. No additional public exposure or private endpoint relaxation was found.
- Deployment archive/image smoke, actual Helm rendering, committed real service-tag data, TFLint acceptance and required exact-head PR CI/run ID/logs remain explicitly incomplete. The evidence README accurately retains those blockers. This review does not replace any prescribed acceptance or authorize live execution/merge.
- Suspected initial empty Terraform-state handling was checked against pinned Terraform v1.16.6 state_pull.go: nil state produces no output and returns 0, agreeing with the adapter. Cross-ConfigMap projection delay was not raised as a rule violation: rejecting a mismatched comparison is the explicit fail-closed contract and cannot authorize wrong counts.
- No Azure operation, Terraform apply/destroy, source/evidence edit, commit, push, PR or external comment was performed. Only scratch review artifacts and read-only source downloads were written. Shell/Python/schema syntax checks passed in the independent infrastructure pass.

## Applied authorities and sources

For every path below, A includes all of AGENTS.md GOVERNANCE (scope/deferred, hard safety 1-11, verdict integrity, merge, handoffs, truth/verification), Code Review Rules, Before opening a pull request rules 1-13, Skills and writing standards; all of CLAUDE.md and docs/agents/factory.md; and origin/main blueprint ADR-001 through ADR-019. Scope and the full frozen inventory/amendment were checked. Process conditions that depend on a future push, PR label, status check or live run are not claimed completed. Reviewer exclusions for analyzer-enforced style and ordinary code-comment presence/style were applied.

N applies every origin/main .NET standard: solution/layout and vertical slices, seams/DI, options startup validation, TimeProvider, correctness settings/pins, behavior tests/assertion preservation, verifier boundary coverage, architecture, and logging/telemetry. T applies every origin/main Terraform standard and ADR-011: layout, provider/module hierarchy/pins, variables, naming, tags, external-state boundary, unknown apply values and checks. E reviews a changed engineering document against its own baseline. K additionally traces Kafka settings and generated values to Workloads/inventory and ADR-013/014/015. No changed Kafka baseline setting lacks an inventory link.

Read-only authority/source verification included the current #114 plan, clean standing-approval check, storage/recovery and continuation decisions, and adopted #109/#14 authorities. The following immutable/official sources were read across the review and key conclusions cross-checked during consolidation:

- Approved Revision 2 plan: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6321098789 ; clean check: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6332608289 ; adopted storage/recovery decision: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6332413257 ; continuation: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6333713470 .
- Native broker names: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c#L130-L225 .
- Azure CLI conditional Blob flags: https://github.com/Azure/azure-cli/blob/da4c0548a2725ea3d08b1d94b8e7fa1b0b07256e/src/azure-cli/azure/cli/command_modules/storage/_params.py#L284-L289 . AKS get-credentials options: same pinned repository, `command_modules/acs/_params.py#L1096-L1103`; ACR repository show registration/arguments/GET: `command_modules/acr/{commands.py,_params.py,repository.py}`.
- Existing Azure CLI login/subscription binding: https://github.com/Azure/kubelogin/blob/bdf0e79b5a0ea6fe9fd3b7ada771378c344ad760/docs/book/src/concepts/login-modes/azurecli.md .
- Raw watch forwarding and bounded logs: https://github.com/kubernetes/kubectl/blob/v0.35.0/pkg/rawhttp/raw.go ; https://github.com/kubernetes/kubectl/blob/v0.35.0/pkg/cmd/logs/logs.go ; https://github.com/kubernetes/kubectl/blob/v0.35.0/pkg/polymorphichelpers/logsforobject.go .
- Flat CLI query result rows: https://github.com/Azure/azure-cli-extensions/blob/74172d7cf4637fdf039e6b9325498feaed459888/src/log-analytics/azext_loganalytics/custom.py . KQL table columns: https://github.com/MicrosoftDocs/azure-monitor-docs/tree/6c8e97c9e8b8e57c19e3b319bda5188e4ceba029/articles/azure-monitor/reference/tables , `azmskafkausererrorlogs.md` and `azmsdiagnosticerrorlogs.md`.
- AKS node group contents: https://github.com/MicrosoftDocs/azure-aks-docs/blob/6901de76c108a545a6b17fee61a39ca9e9e001d1/articles/aks/faq.yml#L110-L117 ; pinned AKS module output: https://github.com/Azure/terraform-azurerm-avm-res-containerservice-managedcluster/tree/4bc0128b057d10d5ed146a53d8c77933fd00ead8 , `main.tf` and `outputs.tf`.
- Storage security/endpoint/output mapping: https://github.com/Azure/terraform-azurerm-avm-res-storage-storageaccount/tree/5695e5ec21744a3765051677d9e82bfba16b5d98 , `main.tf`, `main.privateendpoint.tf`, `main.containers.tf` and `outputs.tf`; downloaded source bytes match the installed pinned module.
- Official Helm archive checksum: https://api.github.com/repos/helm/helm/releases/tags/v4.3.1 , Linux amd64 SHA-256 `0403a88ae20913d11b10a2467b50087721fea7581736da6b9d3992f5357fe54b`.
- NetworkPolicy peer conjunction/additive semantics: https://raw.githubusercontent.com/kubernetes/website/main/content/en/docs/concepts/services-networking/network-policies.md .
- GUID parser padding behavior: https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/System.Private.CoreLib/src/System/Guid.cs#L441-L454 . Empty remote state: https://github.com/hashicorp/terraform/blob/v1.16.6/internal/command/state_pull.go .

Supporting independent notes: `/workspace/scratch/issue-114-cold-review-6-runtime-notes.md`, `/workspace/scratch/issue-114-cold-review-6-infra-notes.md` and `/workspace/scratch/issue-114-cold-review-6-docs-notes.md`. The findings above are the consolidated finding set; duplicate approval observations in those notes are not additional findings. The documentation group's initial no-finding notes preceded the lead's late producer-classification check; on follow-up it independently confirmed the raw-manifest:77 P1 without opening an uncited raw receipt. This final consolidated report supersedes that preliminary no-finding assessment.

## Complete authored path/rubric coverage

Every path receives A. Each applicable additional rubric row is listed; all rules in its named documents were applied, not only the examples in the preceding paragraphs.

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
| `docs/engineering/dotnet.md` | A, E, N |
| `docs/engineering/terraform.md` | A, E, T |
| `docs/operations/a01-runner.md` | A, K |
| `docs/operations/a01-runtime-limits.json` | A, K |
| `docs/operations/evidence/issue-114/README.md` | A |
| `docs/operations/evidence/issue-114/aks-binding-producers.json` | A |
| `docs/operations/evidence/issue-114/cold-review-1.md` | A |
| `docs/operations/evidence/issue-114/cold-review-2.md` | A |
| `docs/operations/evidence/issue-114/cold-review-3.md` | A |
| `docs/operations/evidence/issue-114/cold-review-4.md` | A |
| `docs/operations/evidence/issue-114/cold-review-5.md` | A |
| `docs/operations/evidence/issue-114/local-producers.json` | A |
| `docs/operations/evidence/issue-114/raw-manifest.json` | A |
| `docs/operations/evidence/issue-114/resume-producers.json` | A |
| `docs/operations/evidence/issue-114/resume-status.md` | A |
| `docs/operations/evidence/issue-114/review-4-producers.json` | A |
| `docs/operations/evidence/issue-114/review-5-producers.json` | A |
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
| `src/Parchwell.Scenarios/packages.linux-x64.lock.json` | A, N |
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
| `tests/Parchwell.LoadGen.Tests/Host/DeploymentAdmissionTests.cs` | A, N |
| `tests/Parchwell.LoadGen.Tests/Host/GeneratedValuesCompatibilityTests.cs` | A, N |
| `tests/Parchwell.LoadGen.Tests/Host/LoadGenWorkerHostTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/ChartContractTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Cost/ActualCostReconcilerTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Cost/AzureCliRunStoreTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Cost/PhaseReservationTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentGeneratorTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentPreflightTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Deployment/ServiceTagDataTests.cs` | A, N |
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
| `tests/Parchwell.Scenarios.Tests/GeneratedValuesCompatibilityTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/A01/A01C3EvidenceRecorderTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Component/ComponentRunHarness.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Host/BrokerInventoryTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Host/C3HandlerTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Host/GeneratedValuesCompatibilityTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Host/ImageSmokeTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Host/VerifierCompletionTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | A, N |
| `tests/Parchwell.Verifier.Tests/Support/HostEvidenceBuilder.cs` | A, N |
| `tests/terraform/persistent/main.tftest.hcl` | A, T |

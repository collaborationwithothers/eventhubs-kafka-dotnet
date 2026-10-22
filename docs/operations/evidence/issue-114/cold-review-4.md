# Issue 114 cold review 4

Frozen authored diff: `/workspace/scratch/issue-114-cold-review-4.diff`.
Diff SHA-256: `d9679b8c2ac03192776681076825a7ba11eaa8dd5fb859858f8b32247fae8feb`.
Raw-evidence manifest: `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/raw-manifest.json`.
Manifest SHA-256: `a284b75c2eb07d14b2055fd4c28b1fbb40253777fd59abd9560f47a0020f7d35`.
Rule baseline: `origin/main` at `4a0acc5d886a12cafdfdeafbe1e71f4d0802bf55`.

Applied the repository-owned `.agents/skills/repo-review/SKILL.md` to the complete frozen authored scope. The only author inputs were the diff and raw-manifest paths. The review used a lead pass and three fresh-context, high-effort independent passes for execution/cost/rehearsal, deployment/infrastructure, and workers/evidence. Findings below were independently checked against the frozen source and binding authority before consolidation. Changed blueprint and engineering documents were judged using their origin/main versions; no applicable rule document lacked an origin/main version.

## Findings

P1  src/Parchwell.Runner/Run/Deployment/DeploymentGenerator.cs:130  docs/blueprint.md ADR-014 "Decision"; docs/agents/factory.md "Item lifecycle" step 3; approved Revision 2 plan "Images and generated-value mapping" and "Chart and admission protocol"; repo-review step 4  Real Terraform output cannot pass the new chart schema. The generator copies the entire workload_identity.service_accounts object, which contains loadgen, enrichment-consumer and verifier, into runtime.service_accounts. The chart schema permits only loadgen and verifier and sets additionalProperties:false. ChartRenderer runs schema-validating Helm lint before applying any Job, so every real phase fails before its first worker starts. Project the two A01 worker accounts into the runtime envelope and exercise the actual Terraform output shape. Evidence: unchanged infra/ephemeral/locals.tf:20 and outputs.tf:31-35 define/export all three accounts; charts/parchwell/values.schema.json:118-124 permits two; ChartRenderer.cs:14-17 runs lint without skipping schema validation. A scratch generation using the three-account output followed by Draft 7 validation produced exactly one error, runtime/service_accounts: Additional properties are not allowed ('enrichment-consumer' was unexpected). The pinned Helm v4.3.1 lint implementation coalesces overrides then calls ValidateAgainstSingleSchema. The current two-account fixtures in DeploymentGeneratorTests and verify-a01-deployment.sh hide the mismatch.
P1  src/Parchwell.LoadGen/Run/Execution/LoadGenWorker.cs:47  origin/main docs/engineering/dotnet.md "Seams and dependencies"  The new worker configuration bypasses the required options pattern and startup validation: LoadGenWorker injects IConfiguration and directly reads PARCHWELL_GENERATED_VALUES_FILE into a private field, rather than binding it through its existing LoadGenHostOptions/ValidateOnStart path. The binding standard says "Configuration goes through the options pattern with validation on start (ValidateOnStart)." Bind the optional deployment setting in the composition root and consume the validated option in the worker. Evidence: LoadGenWorker.cs:32-47 and :88-89; existing Program.cs registers LoadGenHostOptions with ValidateOnStart; the verifier counterpart binds the same setting into VerifierHostOptions.GeneratedValuesFile. The approved plan authorizes the optional deployment comparison input but states no exception to this engineering rule. This is a binding engineering-rule violation; the comparison currently still runs before native producer construction.
P1  src/Parchwell.Runner/Parchwell.Runner.csproj:1  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8, third paragraph  This adds the Runner project and new assembly references to Scenarios and Kafka. The rubric requires flagging new projects and assembly references. These additions are expressly required by the approved Revision 2 plan "File changes and dependency boundary", so this is approval-only work: retain under rule 8's exception, list it in the PR description, and leave merge to Hari. Existing centrally pinned package references introduce no package-version change. This finding does not count toward review-round stop limits.
P1  tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj:1  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8, third paragraph  This adds the Runner.Tests project and new test-only assembly references to Runner, LoadGen and Verifier. These are expressly required by the approved Revision 2 plan "File changes and dependency boundary". Apply the same approval-only disposition: list in the PR description and leave merge to Hari; exclude from review-round stop limits.
P1  tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj:21  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8, third paragraph  This adds the Runner assembly reference. The approved Revision 2 plan expressly requires adding Runner to the existing architecture project inventory and references. Apply the same approval-only disposition: list in the PR description and leave merge to Hari; exclude from review-round stop limits.

Findings: 5 at P0 or P1

Pre-existing (not blocking):

None identified.

## Authority, verification and limits

- The frozen diff has 156 authored paths and 18316 lines. Every path below has a rubric mapping. No results/, expected.yaml, scenarios/, docs/adr/ or .github/workflows/ path changes. Workflow text in docs/operations remains authored review scope.
- The approved Revision 2 plan was read directly from issue comment 6321098789. The storage/recovery decision 6332413257 explicitly authorizes persistent private run storage and the main-only reviewer-free sweeper environment on the existing app, as external prerequisites for Hari. The review treats these as proposed, approved design changes requiring Hari's protected-path review, not as proof that those prerequisites exist. The frozen diff adds no new identity role assignment or public/private-endpoint relaxation. It preserves main guards before OIDC and destroy-only sweeper execution.
- Continuation decision 6333713470 was read directly. The existing review override record recognizes the earlier churn trips; the new substantive findings remain subject to that decision and ordinary finding disposition rules. The three approval-only findings above do not count toward those stops.
- All 457 raw-manifest paths are unique, stay within this evidence raw/ tree, exist and match byte size and SHA-256. There are no unlisted retained raw files. All five producer maps were checked. Raw content was examined only for authored citations; other raw bytes were processed only for manifest-integrity hashing.
- The cited current Runner XML reports 226 passed, zero failed, skipped or errors. The cited Release build has zero warnings/errors. The Terraform report has 12 passed tests. Rehearsal reports three phases, five repeats per phase, three applies/destroys, no Azure calls and no replacement repeats; its negative-case count is 23. These are retained local/synthetic observations, not exact reviewed-head PR CI or live results. The pre-validation inventory has 518 repository inputs, all matching the frozen worktree. Counts and hashes were computed by scripts.
- The seven changed/new lockfiles parse. The four new linux-x64 lockfiles' common net10.0 dependency maps exactly equal each project's existing origin/main map. No package-version drift was found. The removed-assertion scan found no loosened, deleted or skipped assertion. Verifier broker boundary additions cover the historical host/port/count/output-budget gaps; the C3 projection retains C0's existing contract and tests.
- Deployment image archive/smoke, Helm rendering, the real committed service-tag dataset, TFLint acceptance, and exact-head PR CI remain explicitly incomplete. The evidence README does not claim these passed. Missing acceptance is a known readiness blocker; source inspection and the scratch schema reproduction do not replace those required executions.
- The schema reproduction is under `/workspace/scratch/issue-114-cr4-infra-repro/`. It used the already built Runner with informational source f2c41aec2990bf8af532110062beb001906cb3eb, whose relevant generator matches the frozen source. The lead independently reran schema validation against the generated files and inspected the pinned Helm source. This is a defect reproduction, not current-head acceptance.
- No implementation/evidence edit, commit, push, PR, live Azure operation or external comment was made. Scratch files alone were written. The review did not rerun the full build or test suite.

## Sources checked

- Repository rules: AGENTS.md GOVERNANCE, Skills, Before opening a pull request, Code Review Rules and writing standards; CLAUDE.md; docs/agents/factory.md; blueprint ADRs and frozen scope/deferred inventory; origin/main .NET and Terraform standards; code-comment review policy.
- Issue authority: [Revision 2 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6321098789), [storage/recovery decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6332413257), and [continuation decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6333713470).
- [Helm v4.3.1 schema-validating lint](https://github.com/helm/helm/blob/v4.3.1/pkg/chart/v2/lint/rules/values.go).
- [librdkafka v2.16.0 broker/nodename formatting](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c#L141-L224).
- [Pinned AKS AVM state export](https://github.com/Azure/terraform-azurerm-avm-res-containerservice-managedcluster/blob/4bc0128b057d10d5ed146a53d8c77933fd00ead8/main.tf#L12-L26), its output contract, and the cited AKS FAQ explaining primary/node resource groups; reviewed against the actual-cost reconciliation fix.
- Pinned Azure CLI Blob conditional/download definitions and AKS get-credentials options, kubelogin Azure CLI login mode, pinned storage-module outputs, and the Kubernetes watch/NetworkPolicy semantics cited by the authored contracts were checked in the independent source passes.
- KQL columns checked independently at MicrosoftDocs/azure-monitor-docs commit 6c8e97c9e8b8e57c19e3b319bda5188e4ceba029: AZMSKafkaUserErrorLogs.Message, AZMSDiagnosticErrorLogs.ErrorMessage/ErrorCount, _ResourceId and TimeGenerated.

## Full rubric coverage

Every entry applies the anything row: all binding AGENTS governance/review/process/skills/writing rules, CLAUDE.md, factory.md and blueprint ADRs. Additional labels below apply the .NET row, Terraform row plus ADR-011, Kafka-settings row plus ADR-014/015, and the changed engineering-document row. Verdict-integrity and workflow safety rules were also checked globally; their protected path triggers are absent. No analyzer-enforced style or comment presence/style was raised.

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
- `docs/engineering/dotnet.md`: anything, changed engineering document itself at origin/main.
- `docs/engineering/terraform.md`: anything, changed engineering document itself at origin/main.
- `docs/operations/a01-runner.md`: anything, Kafka/settings; ADR-014/015.
- `docs/operations/a01-runtime-limits.json`: anything, Kafka/settings; ADR-014/015.
- `docs/operations/evidence/issue-114/README.md`: anything.
- `docs/operations/evidence/issue-114/aks-binding-producers.json`: anything.
- `docs/operations/evidence/issue-114/cold-review-1.md`: anything.
- `docs/operations/evidence/issue-114/cold-review-2.md`: anything.
- `docs/operations/evidence/issue-114/cold-review-3.md`: anything.
- `docs/operations/evidence/issue-114/local-producers.json`: anything.
- `docs/operations/evidence/issue-114/raw-manifest.json`: anything.
- `docs/operations/evidence/issue-114/resume-producers.json`: anything.
- `docs/operations/evidence/issue-114/resume-status.md`: anything.
- `docs/operations/evidence/issue-114/review-fix-producers.json`: anything.
- `docs/operations/evidence/issue-114/terraform-producers.json`: anything.
- `docs/operations/evidence/issue-114/write-raw-manifest.mjs`: anything.
- `docs/operations/kql/a01-runtime.kql`: anything.
- `docs/operations/loadgen-contract.md`: anything.
- `docs/operations/scenario-contract.md`: anything, Kafka/settings; ADR-014/015.
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
- `src/Parchwell.LoadGen/Program.cs`: anything, .NET.
- `src/Parchwell.LoadGen/Run/Execution/LoadGenWorker.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `src/Parchwell.LoadGen/Run/Input/DeploymentAdmission.cs`: anything, .NET.
- `src/Parchwell.LoadGen/Run/Ledger/AssemblyInfo.cs`: anything, .NET.
- `src/Parchwell.LoadGen/packages.linux-x64.lock.json`: anything, .NET.
- `src/Parchwell.Runner/Parchwell.Runner.csproj`: anything, .NET.
- `src/Parchwell.Runner/Program.cs`: anything, .NET.
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
- `src/Parchwell.Runner/Run/Deployment/DeploymentPreflight.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `src/Parchwell.Runner/Run/Deployment/DeploymentScenarioSource.cs`: anything, .NET.
- `src/Parchwell.Runner/Run/Deployment/ServiceTagData.cs`: anything, .NET, Kafka/settings; ADR-014/015.
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
- `src/Parchwell.Scenarios/Generation/HelmValuesGenerator.cs`: anything, .NET.
- `src/Parchwell.Scenarios/packages.linux-x64.lock.json`: anything, .NET.
- `src/Parchwell.Verifier/AssemblyInfo.cs`: anything, .NET.
- `src/Parchwell.Verifier/Common/Kafka/VerifierKafkaSession.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `src/Parchwell.Verifier/Common/Serialization/DeploymentJson.cs`: anything, .NET.
- `src/Parchwell.Verifier/ImageSmoke.cs`: anything, .NET.
- `src/Parchwell.Verifier/Parchwell.Verifier.csproj`: anything, .NET.
- `src/Parchwell.Verifier/Program.cs`: anything, .NET.
- `src/Parchwell.Verifier/StartLine/BrokerInventoryCodec.cs`: anything, .NET.
- `src/Parchwell.Verifier/StartLine/StartLineHandler.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifierCompletionState.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifierHostOptions.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifierWorker.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifyRun/A01/A01EvidenceRecorder.cs`: anything, .NET.
- `src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs`: anything, .NET.
- `src/Parchwell.Verifier/packages.linux-x64.lock.json`: anything, .NET.
- `tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj`: anything, .NET.
- `tests/Parchwell.ArchitectureTests/ProjectModel.cs`: anything, .NET.
- `tests/Parchwell.ArchitectureTests/packages.lock.json`: anything, .NET.
- `tests/Parchwell.LoadGen.Tests/Host/DeploymentAdmissionTests.cs`: anything, .NET.
- `tests/Parchwell.LoadGen.Tests/Host/GeneratedValuesCompatibilityTests.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `tests/Parchwell.LoadGen.Tests/Host/LoadGenWorkerHostTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/ChartContractTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Cost/ActualCostReconcilerTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Cost/AzureCliRunStoreTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Cost/PhaseReservationTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Deployment/DeploymentGeneratorTests.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `tests/Parchwell.Runner.Tests/Deployment/DeploymentPreflightTests.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `tests/Parchwell.Runner.Tests/Deployment/ServiceTagDataTests.cs`: anything, .NET, Kafka/settings; ADR-014/015.
- `tests/Parchwell.Runner.Tests/Evidence/EvidenceExporterTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Evidence/PrivateProvenanceCollectorTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Evidence/RecoveryExportTests.cs`: anything, .NET.
- `tests/Parchwell.Runner.Tests/Evidence/azure-cli-query-recording.json`: anything, .NET.
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

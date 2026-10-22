# Issue 114 cold review 8

Frozen authored diff: `/workspace/scratch/issue-114-cold-review-8.diff`.
Diff SHA-256: `97f38047cc58360effd9aebd8505bf66939651fe86df8e9d636ebb3c36b52b1b`.
Raw-evidence manifest: `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/raw-manifest.json`.
Manifest SHA-256: `870ebb8458f55c1ce1c1a90fda1aff91dac3d78806a469dfd44c72e649b47341`.
Reviewed source: `4590e7818b3ac720e593ef680ecb6a0d51467546`.
Rule baseline: `origin/main` at `4a0acc5d886a12cafdfdeafbe1e71f4d0802bf55`.

Applied the repository-owned repo-review skill from origin/main. The independent review covered the complete authored diff, including historical cold reviews, all producer scripts and manifest metadata; only `docs/operations/evidence/**/raw/**` was excluded. Three fresh independent path groups covered workers/dependencies, deployment/infrastructure and Contracts/Execution, with a separate authored-evidence pass; the lead reviewed the remaining paths and reconciled all findings against the frozen source and primary authority. All 180 changed paths have explicit rubric mappings below. Changed blueprint/engineering authorities were applied from origin/main, so their proposed text could not weaken the review rules. No applicable authority lacked an origin/main version.

## Findings

P1  src/Parchwell.Runner/Program.cs:209  docs/agents/factory.md "Item lifecycle" step 3; approved #114 Revision 2 "Runner state machine and enforced checks" check 9  The only public candidate never contains the required provenance, timing, gate or eligible result summary. WriteCandidateAsync supplies only scenario/configuration/run IDs, source commit, phase status, costs and a cost-availability reason; it omits versions, immutable image digests, timings and allowed diagnostics/counts, and gate execution never refreshes the candidate after either a new or retained gate decision. The workflow uploads only this unchanged candidate. Same-class evidence: PublicCandidate.cs:16 allowlists attempted/acknowledged/consumed/missing/duplicates/out_of_order, whereas WorkerOutcome.cs accepts the oracle's ok/loss/phantom/reported_failure/duplicate/reorder, so supplying the authoritative count object would silently discard every count. Evidence: approved plan https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6321098789 check 9 expressly requires the summary's "versions, commit/digests, timings, gate/status/reasons, allowed diagnostics/counts and cost estimates/actual-availability"; Program.cs:111 calls WriteCandidateAsync before the later gate command, gate branches return/write without updating it, and docs/operations/workflows/scenario.yml.txt:202, :380 and :558 upload only phase/candidate.json. The existing projection allowlist is not evidence that production fills these fields. This is an omitted approved output contract, not a request for a new guard or experimental check.

P1  src/Parchwell.Runner/Parchwell.Runner.csproj:1  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8, third paragraph  The new runtime project and its Scenarios/Kafka assembly references and Hosting/YamlDotNet package references require Hari's merge. Evidence: added csproj and Parchwell.slnx entry; approved Revision 2 "File changes and dependency boundary" expressly requires these additions. Approval-only: retain under rule 8, list in the PR description, leave merge to Hari and exclude from all review-round stop limits.

P1  tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj:1  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8, third paragraph  The new test project, its Runner/LoadGen/Verifier assembly references and xUnit package reference require Hari's merge. Evidence: added csproj and Parchwell.slnx entry; approved Revision 2 "File changes and dependency boundary" requires the existing xUnit project shape and test-only worker references. Apply the same approval-only disposition and exclusion from review-round stop limits.

P1  tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj:21  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8, third paragraph  The added Runner assembly reference requires Hari's merge. Evidence: added ProjectReference; approved Revision 2 "File changes and dependency boundary" expressly requires adding Runner to the architecture inventory/references. Apply the same approval-only disposition and exclusion from review-round stop limits.

Findings: 4 at P0 or P1

Pre-existing (not blocking):

None identified. No P2 finding was confirmed.

## Classification and authority

There is one substantive P1 and three approval-only P1s. The new runtime and test project additions in Parchwell.slnx are the same changes as the project-declaration findings, not extra findings. InternalsVisibleTo metadata is not counted as an additional assembly reference. Runtime-specific worker/Kafka/Scenarios lockfiles retain exactly the existing non-RID net10.0 dependency entries; the architecture lockfile adds the approved Runner project node. No unexpected package version was introduced. The persistent storage declaration is expressly authorized by decision 6332413257. The provisioned kubelogin prerequisite is explicitly left for Hari; no floating installation or new credential/grant is introduced.

The complete current Revision 2 plan was read from REST-snapshot comment 6321098789, updated `2026-10-22T04:14:53Z`, together with the clean standing-approval check 6332608289, issue #114 body/comments, listed parent #109/#14 authority and linked decisions. Decisions 6320840549, 6332413257 and 6333713470 govern long-job acceptance, private storage/recovery and continuation. The continuation decision keeps the churn override until clean; it does not remove any output requirement or substantive finding. Historical review reports are evidence, not authority. The new candidate finding is within check 9 of the actual approved plan and is not approval-only.

## Verification and limits

No source edits, commit, push, external write, full build/test suite, coverage or live command was performed. Review commands were read-only and bounded. Static production call-path inspection suffices for the candidate finding: no success path or gate path supplies the omitted fields. This review makes no claim of exact-head CI, image smoke, Helm rendering, live network/identity readiness or real service-tag acquisition.

The authored-evidence pass verified all 498 raw manifest entries for unique in-tree paths, completeness, size and SHA-256, and read all 116 unique producer commands. All nine producer maps agree with their manifest producer/source/status strings. The cited 530-entry current input inventory matches current source bytes. Cited raw reports support Runner 913 passed, focused LoadGen 33 passed, Terraform 12 plan tests passed, zero build warnings/errors, rehearsal three phases with five repeats each and 23 negative cases, and nine workflow-preparation cases. Local evidence remains correctly marked dirty-worktree and cannot replace PR CI. Historical review 1-7 diff hashes/path counts/scopes were checked against their frozen scratch diffs. Raw content was opened only to check claims cited by authored content; uncited raw bytes were read only by manifest-integrity hashing.

The bound audit was reconciled with the new required-field/type/positive-limit tests and the explicit below/equal/above fixtures for generation, ledger/cost, export, KQL, provenance, recovery and wire limits. Large sparse-file export fixtures prove preflight admission up to a deliberate transport stop; they do not prove complete large transfers. Existing verifier assertions remain intact. The previously suspected absent-backend state-pull defect was rejected against Terraform v1.16.6's explicit zero/no-output handling for a nil state file.

Official source checks included:

- Helm v4.3.1 official release REST body, confirming linux-amd64 SHA-256 `0403a88ae20913d11b10a2467b50087721fea7581736da6b9d3992f5357fe54b`: https://api.github.com/repos/helm/helm/releases/tags/v4.3.1.
- Pinned storage AVM output members and module pin: https://github.com/Azure/terraform-azurerm-avm-res-storage-storageaccount/blob/5695e5ec21744a3765051677d9e82bfba16b5d98/outputs.tf.
- Official Kubernetes NetworkPolicy documentation/source for combined namespace/pod selectors and additive ingress/egress rules: https://kubernetes.io/docs/concepts/services-networking/network-policies/.
- Official AKS workload identity and service-tag sources, confirming the chart's injection labels/annotations and downloadable outbound tag data: https://learn.microsoft.com/en-us/azure/aks/workload-identity-overview and https://learn.microsoft.com/en-us/azure/virtual-network/service-tags-overview. Where documentation-site access was denied, the official MicrosoftDocs/kubernetes source repositories supplied the content.
- Pinned native broker/IPv6 name formula: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c.
- Pinned Terraform state-pull behavior: https://github.com/hashicorp/terraform/blob/v1.16.6/internal/command/state_pull.go; kubectl raw/watch/log transport behavior: https://github.com/kubernetes/kubectl/tree/v0.35.0.
- KQL table/column definitions at the authored immutable source: https://github.com/MicrosoftDocs/azure-monitor-docs/blob/6c8e97c9e8b8e57c19e3b319bda5188e4ceba029/articles/azure-monitor/reference/tables/azmskafkausererrorlogs.md and https://github.com/MicrosoftDocs/azure-monitor-docs/blob/6c8e97c9e8b8e57c19e3b319bda5188e4ceba029/articles/azure-monitor/reference/tables/azmsdiagnosticerrorlogs.md, confirming Message and ErrorMessage/ErrorCount.

Independent path-group records: `/workspace/scratch/issue-114-review-8-workers-report.md`, `/workspace/scratch/issue-114-cold-review-8-deploy-report.md`, `/workspace/scratch/issue114-execution-review.md`, and `/workspace/scratch/issue-114-review-8-authored-evidence-report.md`. Their findings were reconciled; they do not replace the consolidated result above.

## Full path and rubric coverage

The diff contains 21,795 lines and 180 unique authored paths, computed from the frozen diff. Every row below applies A. N/T/E/K are additional applicable rubric rows. No changed file is unmatched. No actual `.github/workflows/`, `scenarios/`, `results/` or `docs/adr/` path occurs; safety/verdict/scope rules were nevertheless checked against all authored workflow text and the whole diff.

- A: origin/main repo-review skill; all AGENTS.md GOVERNANCE (scope/deferred items, safety, verdict integrity, merge and truth rules), Code Review Rules, Before opening a pull request, Skills and Writing standards; CLAUDE.md; docs/agents/factory.md; blueprint ADR-001 through ADR-019. Proposed authorities cannot relax this baseline.
- N: origin/main docs/engineering/dotnet.md, including layout/slices, seams/DI/options/clock, exact packages, testing/assertion preservation, verifier boundaries, architecture and operator logging.
- T: origin/main docs/engineering/terraform.md and ADR-011, including pinned modules/providers, naming/tag maps, private external-state consumption and offline/live boundary.
- E: the changed engineering document itself, applied from origin/main.
- K: blueprint Workloads and inventory traceability, ADR-014/015. Generated settings remain traceable to A01 C0/C3 and the existing profile; no new experimental setting or expected outcome is authorized.

| Full path | Rubric |
|---|---|
| `/workspace/eventhubs-kafka-dotnet/Directory.Build.props` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/Parchwell.slnx` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/charts/parchwell/Chart.yaml` | A, K |
| `/workspace/eventhubs-kafka-dotnet/charts/parchwell/templates/_helpers.tpl` | A, K |
| `/workspace/eventhubs-kafka-dotnet/charts/parchwell/templates/admission.yaml` | A, K |
| `/workspace/eventhubs-kafka-dotnet/charts/parchwell/templates/generated-values.yaml` | A, K |
| `/workspace/eventhubs-kafka-dotnet/charts/parchwell/templates/jobs.yaml` | A, K |
| `/workspace/eventhubs-kafka-dotnet/charts/parchwell/templates/networkpolicies.yaml` | A, K |
| `/workspace/eventhubs-kafka-dotnet/charts/parchwell/templates/serviceaccounts.yaml` | A, K |
| `/workspace/eventhubs-kafka-dotnet/charts/parchwell/values.schema.json` | A, K |
| `/workspace/eventhubs-kafka-dotnet/charts/parchwell/values.yaml` | A, K |
| `/workspace/eventhubs-kafka-dotnet/docs/blueprint.md` | A, K |
| `/workspace/eventhubs-kafka-dotnet/docs/engineering/dotnet.md` | A, E, K |
| `/workspace/eventhubs-kafka-dotnet/docs/engineering/terraform.md` | A, E |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/a01-runner.md` | A, K |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/a01-runtime-limits.json` | A, K |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/README.md` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/acquisition-status-provenance.json` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/acquisition-status.md` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/aks-binding-producers.json` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/chart-tool-acquisition-status.json` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/cold-review-1.md` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/cold-review-2.md` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/cold-review-3.md` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/cold-review-4.md` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/cold-review-5.md` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/cold-review-6.md` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/cold-review-7.md` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/local-producers.json` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/placement-producers.json` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/raw-manifest.json` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/resume-producers.json` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/resume-status.md` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/review-4-producers.json` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/review-5-producers.json` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/review-7-producers.json` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/review-fix-producers.json` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/terraform-producers.json` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/write-raw-manifest.mjs` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/kql/a01-runtime.kql` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/loadgen-contract.md` | A, K |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/scenario-contract.md` | A, K |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/terraform.md` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/verifier-contract.md` | A, K |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/workflows/a01-deployment-acceptance.yml.txt` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/workflows/scenario.yml.txt` | A |
| `/workspace/eventhubs-kafka-dotnet/docs/operations/workflows/sweeper.yml.txt` | A |
| `/workspace/eventhubs-kafka-dotnet/infra/persistent/locals.tf` | A, T |
| `/workspace/eventhubs-kafka-dotnet/infra/persistent/outputs.tf` | A, T |
| `/workspace/eventhubs-kafka-dotnet/infra/persistent/storage.tf` | A, T |
| `/workspace/eventhubs-kafka-dotnet/scripts/fetch-service-tags.sh` | A |
| `/workspace/eventhubs-kafka-dotnet/scripts/verification/assert-a01-deployment.py` | A |
| `/workspace/eventhubs-kafka-dotnet/scripts/verify-a01-deployment.sh` | A |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Kafka/packages.linux-x64.lock.json` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.LoadGen/Parchwell.LoadGen.csproj` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.LoadGen/Program.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.LoadGen/Run/Execution/LoadGenWorker.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.LoadGen/Run/Input/DeploymentAdmission.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.LoadGen/Run/Input/LoadGenHostOptions.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.LoadGen/Run/Ledger/AssemblyInfo.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.LoadGen/packages.linux-x64.lock.json` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Parchwell.Runner.csproj` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Program.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Contracts/ExecutionBudgets.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Contracts/IArtifactStore.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Contracts/IProcessRunner.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Contracts/IRunCluster.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Contracts/IRunGate.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Contracts/ITerraformEnvironment.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Contracts/WorkerIdentity.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Cost/ActualCostReconciler.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Cost/AzureCliRunStore.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Cost/LedgerModels.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Cost/PhaseReservation.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Cost/RunLedger.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Deployment/ChartRenderer.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Deployment/DeploymentGenerator.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Deployment/DeploymentPreflight.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Deployment/DeploymentRuntimeLimits.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Deployment/DeploymentScenarioSource.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Deployment/ServiceTagData.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Evidence/EvidenceExporter.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Evidence/KqlCollector.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Evidence/PrivateEvidenceCollector.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Evidence/PrivateProvenanceCollector.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Evidence/PublicCandidate.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Execution/AksKubeconfigBinding.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Execution/BoundedProcessRunner.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Execution/BrokerInventory.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Execution/GenerationGuard.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Execution/KubernetesRunCluster.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Execution/LocalVerifierGate.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Execution/PhaseExecutor.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Execution/PodAdmission.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Execution/ProbeGateRunner.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Execution/PublicationAcceptance.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Execution/RecoveryEvidenceCollector.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Execution/RepeatExecutor.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Execution/SweepExecutor.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Execution/TerraformEnvironment.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Execution/WorkerBlobDownloader.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Execution/WorkerOutcome.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Rehearsal/RehearsalRunner.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Rehearsal/RehearsalScenarioFactory.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Rehearsal/RehearsalTransports.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/Run/Rehearsal/WireFixtures.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Runner/packages.lock.json` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Scenarios/Generation/GeneratedValuesCompatibility.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Scenarios/Generation/HelmValuesGenerator.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Scenarios/packages.linux-x64.lock.json` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/AssemblyInfo.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Common/Kafka/VerifierKafkaSession.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Common/Serialization/DeploymentJson.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/ImageSmoke.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Parchwell.Verifier.csproj` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Program.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/StartLine/BrokerInventoryCodec.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/StartLine/StartLineHandler.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierCompletionState.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierHostOptions.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierWorker.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/A01/A01EvidenceRecorder.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/packages.linux-x64.lock.json` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.ArchitectureTests/ProjectModel.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.ArchitectureTests/packages.lock.json` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.LoadGen.Tests/Host/DeploymentAdmissionTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.LoadGen.Tests/Host/GeneratedValuesCompatibilityTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.LoadGen.Tests/Host/LoadGenWorkerHostTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/ChartContractTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Contracts/ExecutionBudgetsTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Cost/ActualCostReconcilerTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Cost/AzureCliRunStoreTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Cost/PhaseReservationTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Cost/RuntimeCostBoundaryTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Deployment/DeploymentGeneratorTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Deployment/DeploymentPreflightTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Deployment/DeploymentRuntimeLimitsTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Deployment/ServiceTagDataTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Evidence/EvidenceExportBoundaryTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Evidence/EvidenceExporterTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Evidence/KqlBoundaryTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Evidence/PrivateProvenanceCollectorTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Evidence/PublicCandidateBoundaryTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Evidence/RecoveryExportTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Evidence/azure-cli-query-recording.json` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Execution/AksKubeconfigBindingTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Execution/BoundedProcessRunnerTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Execution/FailureArtifactPreservationTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Execution/GenerationGuardTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Execution/KubernetesWatchTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Execution/PhaseEvidenceBoundaryTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Execution/PhaseEvidenceInventoryTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Execution/PodAdmissionTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Execution/PublicationBoundaryTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Execution/RecoveryEvidenceTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Execution/TerraformEnvironmentTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Execution/WorkerBlobBoundaryTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Execution/WorkerOutcomeBoundaryTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/RehearsalTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/Support/WorkerWireFixtures.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/WireFixtureBoundsTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/WorkerPublicationFixture.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/WorkerWireCompatibilityTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/WorkerWireFixtureTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Runner.Tests/packages.lock.json` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Scenarios.Tests/GeneratedValuesCompatibilityTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/A01/A01C3EvidenceRecorderTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Component/ComponentRunHarness.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/BrokerInventoryTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/C3HandlerTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/GeneratedValuesCompatibilityTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/ImageSmokeTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierCompletionTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Support/HostEvidenceBuilder.cs` | A, N, K |
| `/workspace/eventhubs-kafka-dotnet/tests/terraform/persistent/main.tftest.hcl` | A, T |

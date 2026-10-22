# Issue 114 cold review 7

Frozen authored diff: `/workspace/scratch/issue-114-cold-review-7.diff`.
Diff SHA-256: `b2edc21375d0439d9a5743e48f74996fcea6240bb2352074b15ccfb11682b010`.
Raw-evidence manifest: `/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/raw-manifest.json`.
Manifest SHA-256: `f77df85b9c0bc0f6f7a9b71bc4a18e6b6b972186e1ff1b927cb0e02f738a9c02`.
Rule baseline: `origin/main` at `4a0acc5d886a12cafdfdeafbe1e71f4d0802bf55`.

Applied the repository-owned repo-review skill from origin/main. The only author inputs were the frozen diff and raw-evidence manifest. Four fresh-context high-effort independent passes covered deployment/security, runtime/cost, workers/dependencies, and evidence/documentation; the lead reconciled their findings against the frozen source and primary authority. The combined passes read the complete authored diff, including historical review records, producer scripts and manifest metadata. All 166 changed paths have rubric mappings below. The changed blueprint and engineering authorities were applied in their origin/main versions, so the proposed text could not weaken its review baseline. No applicable authority lacked an origin/main version.

## Findings

P1  src/Parchwell.Runner/Run/Deployment/DeploymentGenerator.cs:76  docs/agents/factory.md "Item lifecycle" step 3; approved #114 Revision 2 "Runner state machine and enforced checks" check 4  Generation accepts missing or invalid deployment bounds although the approved check requires rejection at generation. Removing loadgen.MaxAttempts, setting MaxAttempts=-1, or setting UploadTimeout=-00:00:01 each returns exit 0 and writes the missing/invalid environment mapping. AddSection copies supplied properties without validating required members or finite positive values; production preflight also only iterates present properties, so a missing bound reaches worker startup after apply. Evidence: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6321098789 says "Fail ... missing deployment bounds at generation, and invalid finite/positive limits"; Program.cs's generate branch bypasses DeploymentPreflight; GenerateAsync:56-81 and AddSection:179-186 contain no required-bound validation. Scratch input/output/command records are under /workspace/scratch/issue-114-review7-limits-repro/{baseline,missing_MaxAttempts,negative_MaxAttempts,negative_UploadTimeout}/. The existing diagnostic DLL records source 0d25c6d71c2196dd49b32d87989f5aee5fb633ff; that commit's DeploymentGenerator.cs is byte-identical to the frozen generator. This reproduction is defect evidence, not reviewed-head acceptance.

P1  src/Parchwell.Runner/Run/Evidence/EvidenceExporter.cs:61  docs/agents/factory.md "Item lifecycle" step 3; approved #114 Revision 2 "Runner state machine and enforced checks" check 4  New numerical input limits lack the expressly required below/equal/above tests. The plan says "Tests at each new numerical bound: below/equal/above". The 256-entry export guard is tested only at 257, without 255/256 acceptance; the 10000-row KQL guard at KqlCollector.cs:57 has only 0/1/31-row fixtures, without 9999/10000/10001. The same-class audit below records further missing neighbors for explicit operational byte/count/finite-positive limits. Evidence: EvidenceExporterTests.cs:124, :187 and :201; PhaseEvidenceInventoryTests.cs:59 only asserts a small ordinary result falls within 1..MaxEntries. All changed test files and all relevant call sites were searched to exclude computed neighboring fixtures. These are missing tests for approved checks, not requests for new guards, workload caps or experiment predicates.

P1  src/Parchwell.Runner/Parchwell.Runner.csproj:1  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8, third paragraph  The new runtime project and its Scenarios/Kafka assembly references and Hosting/YamlDotNet package references require Hari's merge. The rubric explicitly flags every new project, assembly reference or NuGet package. Evidence: new csproj and Parchwell.slnx entry; approved Revision 2 "File changes and dependency boundary" expressly requires these additions. This is approval-only: retain under rule 8, list it in the PR description, leave merge to Hari, and exclude it from review-round stop limits.

P1  tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj:1  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8, third paragraph  The new test project and its Runner/LoadGen/Verifier assembly references require Hari's merge. Evidence: new csproj and Parchwell.slnx entry; approved Revision 2 "File changes and dependency boundary" expressly requires the project and test-only worker references. Apply the same approval-only disposition and exclusion from review-round stop limits.

P1  tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj:21  AGENTS.md "Code Review Rules"; "Before opening a pull request" rule 8, third paragraph  The added Runner assembly reference requires Hari's merge. Evidence: added ProjectReference; approved Revision 2 "File changes and dependency boundary" expressly requires adding Runner to the architecture inventory and references. Apply the same approval-only disposition and exclusion from review-round stop limits.

Findings: 5 at P0 or P1

Pre-existing (not blocking):

None identified. No P2 finding was confirmed.

## Same-class boundary-test audit

The tables concern existing explicit operational bounds. They report absent required neighboring tests, not evidence that each predicate computes the wrong answer. No large allocation or large test run was performed. Schema versions, fixed experiment cardinality and arbitrary implementation-only string/identity widths are excluded. Existing verifier boundary tests, GBP cost-limit cases, sweep-expiry cases and PodAdmission's event-count traversal were checked rather than re-raised.

| New guard | Exact source | Existing coverage | Missing required neighbors |
|---|---|---|---|
| Export entries <=256 | EvidenceExporter.cs:61 |257 rejects; ordinary small exports; full phase only InRange1..256 |255/256 accepted |
| Export file <=268435456 bytes | EvidenceExporter.cs:74 and81 |Empty and small originals; corruption/conflict tests |268435455/268435456/268435457 |
| Export aggregate <=8589934592 bytes | EvidenceExporter.cs:74 and84 |Ordinary small exports and finite full-phase fixture |8589934591/8589934592/8589934593 aggregate bytes |
| Actual-cost upload <=1048576 bytes | EvidenceExporter.cs:50 |ActualCostReconcilerTests uploads small actual-cost records |1048575/1048576/1048577 |
| Gate upload <=16777216 bytes | EvidenceExporter.cs:139 |Small real gate;16777217 rejects in EvidenceExporterTests.cs:63 |16777215/16777216 accepted |
| KQL output <=16777216 bytes | KqlCollector.cs:51 |Small0/1/31-row arrays only |16777215/16777216/16777217 |
| KQL output <=10000 rows | KqlCollector.cs:57 |0/1/31 rows only |9999/10000/10001 |
| Private KQL source <=65536 bytes and parameters <=1048576 bytes | PrivateEvidenceCollector.cs:13/16 |No direct collector test; phase tests do not instantiate it |65535/65536/65537 source bytes;1048575/1048576/1048577 parameter bytes |
| Public candidate input <=1048576 bytes | PublicCandidate.cs:25 |Small redaction/unknown/private-field fixtures |1048575/1048576/1048577 |
| Wire manifest <=1048576 bytes | WireFixtures.cs:31 |Fresh ordinary real-writer manifest and corrupted file test |1048575/1048576/1048577 |
| Wire manifest file count1..256 | WireFixtures.cs:35 |Ordinary complete real-writer fixture only |0/1/2 lower cases and255/256/257 upper cases |
| Wire fixture file <=16777216 bytes | WireFixtures.cs:45 |Small real-writer artifacts; corruption changes one byte |16777215/16777216/16777217 |
| Wire fixture aggregate <=268435456 bytes | WireFixtures.cs:47 |Ordinary bounded real-writer fixture only |268435455/268435456/268435457 aggregate bytes |


| Changed source location | Explicit operational cap | Existing evidence | Missing neighboring cases |
|---|---|---|---|
| Cost/ActualCostReconciler.cs:11 (also :81/:88) | 1 MiB record/document | ActualCostReconcilerTests:18 tests 1,048,577 only | 1,048,575 and 1,048,576 bytes |
| Cost/RunLedger.cs:215 (also persist :193) | 1 MiB ledger | Small ledger read/write fixtures; no ledger byte boundary tests | 1,048,575 / 1,048,576 / 1,048,577 bytes |
| Execution/WorkerBlobDownloader.cs:16 | 256 MiB downloaded artifact | FailureArtifactPreservationTests:391 and RecoveryEvidenceTests:233 test metadata length 268,435,457 only | 268,435,455 and 268,435,456 bytes |
| Execution/PublicationAcceptance.cs:16 | 256 MiB per artifact; 1 GiB aggregate input | Small real publisher outputs; oversized downloader tests do not reach this method | 268,435,455 / 268,435,456 / 268,435,457 and 1,073,741,823 / 1,073,741,824 / 1,073,741,825 bytes |
| Execution/WorkerOutcome.cs:18 | 65,536-byte receipt | WorkerWireCompatibilityTests uses short real receipts | 65,535 / 65,536 / 65,537 bytes |
| Execution/WorkerOutcome.cs:40 | 16 MiB result JSON | Small real result codec outputs | 16,777,215 / 16,777,216 / 16,777,217 bytes |
| Execution/BrokerInventory.cs:11 | 16 MiB inventory JSON | Small real inventory codec outputs | 16,777,215 / 16,777,216 / 16,777,217 bytes |
| Execution/ProbeGateRunner.cs:26 | 16 MiB gate JSON | Small real gate codec outputs | 16,777,215 / 16,777,216 / 16,777,217 bytes |
| Execution/BrokerInventory.cs:18 | 64 brokers | Runner fixtures use one broker; worker-codec cases cannot exercise Runner parser | 63 / 64 / 65 brokers through BrokerInventory.Read |
| Execution/PhaseExecutor.cs:190; Execution/RecoveryEvidenceCollector.cs:172 | 8 GiB phase evidence | PhaseEvidenceInventoryTests tests a small inventory fits | 8,589,934,591 / 8,589,934,592 / 8,589,934,593 retained bytes |
| Contracts/ExecutionBudgets.cs:18/:24 | Explicit execution bounds must be positive | RecoveryEvidenceTests:150 reads only valid committed limits; no invalid-limit mutation cases | -1 / 0 / 1 through ExecutionBudgets.Read |
| Contracts/ExecutionBudgets.cs:25 | Phase deadline must leave apply+export+destroy reserves | No reserve-sum boundary test | With committed 900+300+900 reserves: phase_seconds 2099 / 2100 / 2101 |


Evidence-table source names are relative to `src/Parchwell.Runner/Run/Evidence/`, except WireFixtures under `Run/Rehearsal/`. Runtime-table paths are relative to `src/Parchwell.Runner/Run/`. PrivateProvenanceCollector.ReadBoundedAsync already has the six 1-MiB/16-MiB neighbor cases and is excluded. The export-generated-manifest 1-MiB guard is not claimed as a reachable boundary: the 256-entry and path-length bounds keep valid generated manifests below it. Shared existing boundaries need not be tested twice if a test actually exercises the common production guard; current provenance-helper tests do not exercise the separate guards listed above.

## Authority and verification

- The full diff has 166 authored paths and 19790 lines, computed from the frozen input. Coverage unions across the four independent reports equal all 166 paths with no uncovered path.
- Applied all AGENTS.md GOVERNANCE, scope/deferred inventory, verdict integrity, safety and merge rules; Code Review Rules; Before opening a pull request; Skills; and writing standards, together with CLAUDE.md, factory.md and blueprint ADRs. No results/, expected.yaml, scenarios/, docs/adr/ or .github/workflows/ path changes. The installable workflow text remains authored review scope and was checked for equivalent Azure trigger/identity/cleanup hazards.
- Read the cited Revision 2 plan (6321098789), its check (6332608289), storage/recovery decision (6332413257), adopted parent decision (6313658165), registry amendment (6313841552), and continuation override (6333713470). The override lasts until the PR is clean and requires every passed churn trip in the PR description; it does not relax the ordinary Codex cap. The two substantive findings above still require disposition. The three approval-only findings do not count toward stops.
- All 483 raw-manifest paths are unique, remain within this raw tree, exist and match their size/SHA-256, with no unlisted retained raw file. The 103 unique producer commands and eight producer maps were checked. Raw content was opened only when authored evidence cited it; other raw bytes were processed solely for integrity hashing.
- Cited retained reports support Runner271, focused LoadGen33, Release build0 warnings/errors, Terraform12, rehearsal3 phases/five repeats each/three applies and destroys/zero Azure calls or replacements/23 negative cases, and nine synthetic workflow-preparation cases. The 518-entry pre-validation repository inventory matches the frozen input. Historical diff hashes/path counts and the acquisition relocation were checked mechanically. These remain local/dirty-worktree or synthetic observations, not exact-head PR CI or live findings.
- The relocated acquisition status is authored, 892 bytes, SHA-25643637bd85e327bde502f1f5b88f0f900b87d0105c0c6c6ebb68a2fe37b23adc8, matching its provenance and original receipt. Original tool captures remain raw. No additional authored synthesis was established under raw/.
- Lockfile graphs and hashes were checked structurally: the four RID net10.0 graphs equal their origin/main ordinary-lock graphs, with no new package-version drift. No existing assertion was loosened, deleted or skipped in the changed tests. Verifier C3 remains recorded-only and does not authorize outcome counts.
- The review examined durable reservation/readback before apply; unknown actuals retaining budget; both primary/node cost scopes; one producer and watch continuity through export; worker receipt/exit/result agreement; normal-failure and sweep evidence preservation; original-byte private export and candidate-only publication; explicit cluster kubeconfig; saved-plan generation/lineage/serial checks; and shared workflow serialization. No additional concrete defect was established in those paths.
- Deployment archive/smoke, Helm rendering, the real committed service-tag dataset, TFLint acceptance and exact-head PR CI remain explicitly incomplete in the evidence README. This review does not convert source inspection into those acceptance results.
- No source/evidence change, commit, push, PR creation, external comment or live Azure operation was performed. Only scratch reports/reproduction inputs were written. No full build/test acceptance was rerun.

## Primary sources checked

- Repository authority: origin/main repo-review skill; AGENTS.md; CLAUDE.md; docs/agents/factory.md; origin/main docs/blueprint.md; origin/main docs/engineering/dotnet.md and terraform.md; docs/engineering/code-comments.md review exclusions; and the authored operational contracts for cross-file consistency.
- Approved plan and decisions: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6321098789 ; https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6332413257 ; https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6333713470 ; https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/109#issuecomment-6313658165 ; https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/109#issuecomment-6313841552 .
- Helm official v4.3.1 release API and checksum: https://api.github.com/repos/helm/helm/releases/tags/v4.3.1 . Pinned checksum agrees with authored sources and retained release bytes.
- Pinned Storage AVM account properties/outputs: https://github.com/Azure/terraform-azurerm-avm-res-storage-storageaccount/tree/5695e5ec21744a3765051677d9e82bfba16b5d98 . Pinned AKS module resource/node-group outputs: https://github.com/Azure/terraform-azurerm-avm-res-containerservice-managedcluster/tree/4bc0128b057d10d5ed146a53d8c77933fd00ead8 . Official AKS FAQ scope facts at MicrosoftDocs/azure-aks-docs commit6901de76c108a545a6b17fee61a39ca9e9e001d1.
- Official NetworkPolicy semantics and AKS workload-identity label/annotation documentation, using the Kubernetes website and MicrosoftDocs/azure-aks-docs source repositories when their rendered documentation routes were denied.
- Pinned native broker naming: https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_broker.c , lines141-154 and196-224. Pinned kubectl v0.35.0 raw-watch/log implementations; Terraform v1.16.6 state-pull behavior; Azure CLI conditional Blob and AKS credential options at commitda4c0548a2725ea3d08b1d94b8e7fa1b0b07256e; kubelogin existing-login semantics at commitbdf0e79b5a0ea6fe9fd3b7ada771378c344ad760.
- KQL column definitions at MicrosoftDocs/azure-monitor-docs commit6c8e97c9e8b8e57c19e3b319bda5188e4ceba029; actual flat CLI row construction at https://raw.githubusercontent.com/Azure/azure-cli-extensions/74172d7cf4637fdf039e6b9325498feaed459888/src/log-analytics/azext_loganalytics/custom.py .

## Full rubric matrix

A means the complete anything row: AGENTS.md GOVERNANCE, Code Review Rules, Before opening a pull request, Skills and writing standards; CLAUDE.md; factory.md; blueprint ADRs. It applies to every path below. N adds origin/main .NET standards; T adds origin/main Terraform standards and ADR-011; K adds pinned workload/settings authority and ADR-014/015; E applies the changed engineering document's own origin/main rules; W records equivalent safety review of installable workflow text. Verdict integrity and workflow safety were also applied globally, with no changed protected scenario/result/workflow path. Each cell names the complete applicable rubric rows, not a sampling of their rules.

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
| `docs/engineering/dotnet.md` | A, E |
| `docs/engineering/terraform.md` | A, E |
| `docs/operations/a01-runner.md` | A, K |
| `docs/operations/a01-runtime-limits.json` | A, K |
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
| `docs/operations/evidence/issue-114/local-producers.json` | A |
| `docs/operations/evidence/issue-114/placement-producers.json` | A |
| `docs/operations/evidence/issue-114/raw-manifest.json` | A |
| `docs/operations/evidence/issue-114/resume-producers.json` | A |
| `docs/operations/evidence/issue-114/resume-status.md` | A |
| `docs/operations/evidence/issue-114/review-4-producers.json` | A |
| `docs/operations/evidence/issue-114/review-5-producers.json` | A |
| `docs/operations/evidence/issue-114/review-fix-producers.json` | A |
| `docs/operations/evidence/issue-114/terraform-producers.json` | A |
| `docs/operations/evidence/issue-114/write-raw-manifest.mjs` | A |
| `docs/operations/kql/a01-runtime.kql` | A |
| `docs/operations/loadgen-contract.md` | A |
| `docs/operations/scenario-contract.md` | A, K |
| `docs/operations/terraform.md` | A |
| `docs/operations/verifier-contract.md` | A |
| `docs/operations/workflows/a01-deployment-acceptance.yml.txt` | A, W |
| `docs/operations/workflows/scenario.yml.txt` | A, W |
| `docs/operations/workflows/sweeper.yml.txt` | A, W |
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
| `src/Parchwell.LoadGen/Run/Input/DeploymentAdmission.cs` | A, N, K |
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
| `src/Parchwell.Runner/Run/Deployment/DeploymentScenarioSource.cs` | A, N |
| `src/Parchwell.Runner/Run/Deployment/ServiceTagData.cs` | A, N |
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
| `tests/Parchwell.LoadGen.Tests/Host/DeploymentAdmissionTests.cs` | A, N, K |
| `tests/Parchwell.LoadGen.Tests/Host/GeneratedValuesCompatibilityTests.cs` | A, N, K |
| `tests/Parchwell.LoadGen.Tests/Host/LoadGenWorkerHostTests.cs` | A, N, K |
| `tests/Parchwell.Runner.Tests/ChartContractTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Cost/ActualCostReconcilerTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Cost/AzureCliRunStoreTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Cost/PhaseReservationTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentGeneratorTests.cs` | A, N, K |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentPreflightTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Deployment/ServiceTagDataTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/EvidenceExporterTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/PrivateProvenanceCollectorTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/RecoveryExportTests.cs` | A, N |
| `tests/Parchwell.Runner.Tests/Evidence/azure-cli-query-recording.json` | A |
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
| `tests/Parchwell.Runner.Tests/Support/WorkerWireFixtures.cs` | A, N, K |
| `tests/Parchwell.Runner.Tests/WorkerPublicationFixture.cs` | A, N, K |
| `tests/Parchwell.Runner.Tests/WorkerWireCompatibilityTests.cs` | A, N, K |
| `tests/Parchwell.Runner.Tests/WorkerWireFixtureTests.cs` | A, N, K |
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

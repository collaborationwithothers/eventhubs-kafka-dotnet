# Issue 114 cold review 12

Fresh-context review of the complete authored branch diff against `origin/main`, applying the repository-owned `repo-review` skill and AGENTS.md rule 8. Five fresh-context high-effort reviewers read independent complete assigned groups; the lead reviewed governing documents, authored evidence and provenance, checked findings against primary sources, and reconciled every changed file and rubric.

- Frozen source HEAD: `88c1d70cf44e3bdff5c10292f79bbc2462027c44`.
- Rule baseline: `origin/main` at `4a0acc5d886a12cafdfdeafbe1e71f4d0802bf55`.
- Author-provided inputs only: `/workspace/scratch/issue-114-cold-review-12.diff` and `docs/operations/evidence/issue-114/raw-manifest.json`.
- Diff SHA-256: `335241c8a47a9f80689034cb09c41874604d083b12e18e06d8239e39938a215c`; 1,824,843 bytes, 25,007 lines, 196 authored files (script-computed). The supplied bytes exactly equal the full branch diff against the merge base of origin/main with only `:(top,exclude,glob)docs/operations/evidence/**/raw/**` excluded.
- Manifest SHA-256: `cb535c1fafc59c778b8da85671a1ca4d4e909be3774cf6c4f030753e0f402565`.
- Static review: no source edits, tests, builds, full-suite/coverage work, live operations, pushes, commits or external writes. Only requested scratch review artifacts were written. Source remained frozen.

## Findings

P0  docs/operations/workflows/sweeper.yml.txt:16  AGENTS.md "Code Review Rules" identity-scope rule; repo-review step 2; origin/main docs/blueprint.md section 9 "Control plane"  The second no-reviewer `parchwell-sweeper` environment expands federation trust for the existing apply-capable app beyond the baseline's sole protected scenario environment requiring Hari's approval. A destroy-only command does not narrow the app's Azure permissions for that additional subject. Evidence: sweeper lines 16,19-25,78-87; a01-runner.md:86-88; blueprint.md:771 versus origin/main:769. Decision https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6332413257 point 4 explicitly authorizes this exact design, but does not amend the mandatory unchanged P0 reporting rule or rule 8's enumerated approval-only exception. Classification: documented disputed substantive baseline-policy conflict, not approval-only; this is the same underlying P0 as cold review 11, counted once.
P1  src/Parchwell.Runner/Run/Cost/AzureCliRunStore.cs:70  docs/agents/factory.md "Item lifecycle" step 3 (implement exactly the approved plan); approved #114 Revision 2 "Persistent run storage and durable protocol"  The single `az storage blob upload` invocation still allows SDK-internal write retries, contrary to the explicit "no write retry" requirement and the class summary. The pinned CLI selects azure-storage-blob 12.29.0b1 and supplies no retry override; that SDK defaults to ExponentialRetry with retry_total=3 and resends retryable requests. A committed ledger PUT whose response is lost can therefore be retried with the old If-Match, return 412/Conflict, and make RunLedger.PersistAsync:198 throw before its uncertain-write readback loop (consequence inferred from the verified source). Evidence: CLI da4c0548a2725ea3d08b1d94b8e7fa1b0b07256e setup.py:135, storage/_client_factory.py:121-154, core/commands/client_factory.py:132-182,312, storage/operations/blob.py:643-697; SDK _shared/base_client.py:467 and _shared/policies.py:521-525,553-586,608; local adapter lines 64-76 and RunLedger.cs:197-208. If-Match prevents a second commit; this finding does not allege duplicate commits or corrupted ledger bytes. Classification: new substantive plan-contract P1, not approval-only.
P1  Parchwell.slnx:8  AGENTS.md "Code Review Rules" (flag any new project)  Adds Parchwell.Runner and Parchwell.Runner.Tests, also line 16 and their new project files. Evidence: supplied diff and approved Revision 2 "File changes and dependency boundary" explicitly require both. Classification: approval-only under "Before opening a pull request" rule 8; retain only as required by that plan, disclose in the PR description, leave merge to Hari, and exclude from review-round stop limits.
P1  src/Parchwell.Runner/Parchwell.Runner.csproj:6  AGENTS.md "Code Review Rules" (flag any new assembly reference)  Adds Runner references to Scenarios/Kafka, Runner.Tests references to Runner/LoadGen/Verifier, and ArchitectureTests' Runner reference. Evidence: this file lines 6-7; tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj:9-11; tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj:21; approved Revision 2 dependency boundary. Classification: approval-only under rule 8, with PR disclosure and Hari's merge; grouped once across project and lockfile changes, excluded from review-round stop limits.
P1  src/Parchwell.Runner/Parchwell.Runner.csproj:8  AGENTS.md "Code Review Rules" (flag any new NuGet package)  Adds Hosting/YamlDotNet PackageReference declarations in Runner and xunit.v3.mtp-v2 in Runner.Tests. Existing central pins do not remove the mandatory flag for these new declarations. Evidence: this file lines 8-9; tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj:8; approved Revision 2 dependency boundary. Classification: approval-only under rule 8, with PR disclosure and Hari's merge; RID/transitive entries are not additional findings, and this group is excluded from review-round stop limits.
P2  src/Parchwell.Runner/Run/Evidence/KqlCollector.cs:47  AGENTS.md "Code Review Rules" false-source-comment exception; docs/engineering/code-comments.md "Review"  The comment's source locator says `log-analytics/_custom.py`, whereas the cited implementation is `src/log-analytics/azext_loganalytics/custom.py` in Azure/azure-cli-extensions. Correct the source locator so readers can find the evidence. Evidence: https://github.com/Azure/azure-cli-extensions/blob/74172d7cf4637fdf039e6b9325498feaed459888/src/log-analytics/azext_loganalytics/custom.py:12-38. Its flat TableName/string-column behavior is correct; this is a narrow citation wording correction, not a new checker request.

Findings: 5 at P0 or P1

Pre-existing (not blocking):

None independently established in the unchanged lines inspected.

## Classification and documented dispute

There are two non-approval findings (the previously disputed P0 and one new substantive P1) and three grouped plan-required approval-only P1s. The latter three receive rule 8's exception before review-round/churn counting; the P0 and retry P1 do not. Protected paths, provisioned kubelogin, persistent plans/applies, workflow installation and live readiness remain for Hari. Those ownership/readiness obligations are distinct from the mandatory dependency findings above.

The authored `resume-status.md` explicitly says the sweeper response disputes required corrective action, preserves the literal P0 and trust expansion, and is neither approval-only nor clean-review/merge clearance. The lead independently read decision 6332413257, its point 4, the selected Revision 2 plan/check, and continuation decision 6333713470 through REST. The exact second environment/FIC is authorized; the finding is not an allegation that the author invented an unapproved architecture. Baseline AGENTS.md still says to flag any weakening of identity scope, with no approved-design exception. Rule 8 enumerates its approval-only cases, and factory "Escapes" says an author's Not valid reply makes a finding disputed, not invalid, and does not clear a P0/P1 for merge. The current evidence faithfully preserves that dispute; no recognized explicit disposition of this precise review-rule conflict was found in the cited decisions. The review does not prescribe silently replacing the already-authorized architecture.

No dispute was received for the new retry P1. The exact plan paragraph says "All CLI calls retain the existing 60-second bound, total operation 180 seconds, no write retry, read delays1/2 inside that bound." One subprocess is insufficient to establish that requirement when its SDK retries writes. The actual provisioned CLI version is not established by this static review; its existing readiness/version/help acceptance remains pending. The pinned source cited by the plan demonstrates an allowed implementation that violates the requirement, and neither the adapter nor CLI verification requires a retry-disabled transport.

The same-class retry audit checked both production IArtifactStore.WriteAsync consumers: RunLedger.PersistAsync and EvidenceExporter.PutVerifiedAsync. Ledger, private artifact, gate, actual-cost and cleanup uploads all share this adapter. EvidenceExporter does readback after Conflict, so the clearest lost-recovery consequence is the ledger path. Existing fake-process tests count subprocesses and cannot observe resends inside a CLI process. No new client, test or live operation was run to establish the finding.

## Complete rule application and source checks

G applies every rule in the repo-review anything row to every changed file: all AGENTS.md GOVERNANCE (scope/deferred inventory, safety 1-11, verdict integrity, merge/handoff/truth), Code Review Rules, Before opening a pull request rules 1-13, Skills and Writing standards; CLAUDE.md; docs/agents/factory.md; blueprint ADR-001 through ADR-019 and frozen inventory/scope. Changed blueprint and engineering documents were evaluated using their origin/main versions, including each engineering document's own baseline rules. No applicable authority lacked an origin/main version. The unchanged repo-review skill was read independently. New operational contracts were cross-checked as implementation contracts and did not replace baseline authority.

N adds the full origin/main .NET standards: layout/slices, service/library dependency directions, DI/factories, options/startup validation, TimeProvider, nullable/warnings/analyzers, central exact versions, behavior and boundary tests, assertion preservation, architecture and logging. T adds full origin/main Terraform standards and ADR-011: pinned module/provider hierarchy, layout/variables/names/tags, external state, unknown apply values, offline/live checks. E marks the changed engineering document's own baseline. K additionally applies blueprint Workloads/inventory and ADR-013/014/015 setting fidelity. W marks installable workflow text additionally checked against safety 8/9, main/source guards, Azure identity, private artifacts and teardown ownership. No actual .github/workflows, scenarios, expected-outcome, results, docs/adr path or ready-for-agent operation changes. Analyzer-enforced style and ordinary comment presence/style were excluded.

Independent checks covered full generated version-1 equality and role identity, one producer/watch continuity, metadata-derived brokers, worker receipt/exit/result/count agreement, unchanged scientific judgement, budget/reservation/cost scopes, private original-byte immutable export and recovery, public allowlists/redaction, and saved-plan generation/lineage/serial cleanup. Existing assertion changes were read and no loosened/deleted/skipped assertion was established. Four new RID lockfiles preserve existing common net10.0 dependency entries and library/native pins. Package/project escalation is still reported above.

Primary source reads, across independent groups and consolidation, included:

- #114 Revision 2 plan 6321098789 (updated 2026-10-22T04:14:53Z), clean check 6332608289, storage/recovery decision 6332413257 and continuation 6333713470; adopted #109 decisions 6313658165/6313841552, #112 backend/tag decision 6318488448 and relevant #14 A01 authority. Historical reviews are authored evidence, not substitute authority.
- Azure CLI at da4c0548a2725ea3d08b1d94b8e7fa1b0b07256e: Blob conditionals, client construction and upload, SDK pin, explicit non-admin AKS credentials and read-only image metadata. Retry source: https://github.com/Azure/azure-cli/blob/da4c0548a2725ea3d08b1d94b8e7fa1b0b07256e/src/azure-cli/azure/cli/command_modules/storage/operations/blob.py and https://github.com/Azure/azure-cli/blob/da4c0548a2725ea3d08b1d94b8e7fa1b0b07256e/src/azure-cli-core/azure/cli/core/commands/client_factory.py .
- Blob SDK selected by that CLI: https://github.com/Azure/azure-sdk-for-python/blob/azure-storage-blob_12.29.0b1/sdk/storage/azure-storage-blob/azure/storage/blob/_shared/base_client.py and https://github.com/Azure/azure-sdk-for-python/blob/azure-storage-blob_12.29.0b1/sdk/storage/azure-storage-blob/azure/storage/blob/_shared/policies.py . Default retry policy and actual send loop were read, not inferred from generic SDK memory.
- librdkafka v2.16.0 broker-name/IPv6 formatting; kubectl v0.35.0 raw watch/log transport; Terraform v1.16.6 empty-state-pull behavior; kubelogin bdf0e79b5a0ea6fe9fd3b7ada771378c344ad760 existing Azure CLI login/subscription binding.
- Storage AVM 5695e5ec21744a3765051677d9e82bfba16b5d98 security/private-endpoint/output shapes; AKS AVM 4bc0128b057d10d5ed146a53d8c77933fd00ead8 nodeResourceGroup export; AKS FAQ 6901de76c108a545a6b17fee61a39ca9e9e001d1 two-resource-group facts.
- Official Helm v4.3.1 release/checksum; MCR pinned linux/amd64 index; official Kubernetes NetworkPolicy conjunction/additive semantics and workload identity/service-tag sources. Archive/smoke/render readiness is not inferred from these reads.
- Azure Monitor tables at 6c8e97c9e8b8e57c19e3b319bda5188e4ceba029; Kusto literal/union/tolong sources at 85351b30116da41b75c5a4e8a5b7f5730d81ab08 and ac19a887fb20eab5c08ec7742478621be92c56fd. The query's conversion of int ErrorCount to long is consistent. CLI result flattening was directly checked at extensions 74172d7cf4637fdf039e6b9325498feaed459888.

## Authored evidence and review limits

The lead covered all 39 governing/runtime-limit/authored-evidence paths, including every historical cold-review record, producer command/script, acquisition synthesis, source audit and the raw-manifest metadata. Historical finding records remain historical and do not constitute clean current review. The acquisition summary is correctly outside raw; its exact relocation/provenance and supporting captures are preserved. No additional authored synthesis under excluded raw was established from producer review.

Manifest metadata audit: 541 unique raw paths, all within raw/, present regular non-symlink files with matching stat byte sizes and valid SHA-256 syntax; no unlisted raw path. All 149 unique producer strings were read; every entry includes a source commit. All 157 mapped commands across 13 producer maps exactly agree with manifest producer/source/status strings; embedded supporting-script size/hash was checked. These are metadata checks, not a claim that every uncited raw payload was opened/hashed.

Payload/hash checks were limited to authored citations in README.md/acquisition-status.md/resume-status.md and the evidence records. The lead read and verified 18 cited raw records; the infrastructure reviewer also verified the authored-cited official Helm release capture. Cited XMLs support 1001 Runner, 33 focused LoadGen, 382 focused Verifier host and 35 targeted query passing tests. Cited build has zero warnings/errors, Terraform has 12 passing mock tests, the synthetic rehearsal has three phases/five repeats each/three applies and destroys/zero Azure calls or replacements/23 negative cases, and workflow preparation has nine synthetic cases. The cited review-11 input inventory hashes 535 repository inputs and all match the current frozen files outside evidence; earlier inventories correctly predate the disclosed KQL/Verifier-path fixes. The latest cited docs scan records 36 advisory upstream-source warnings with a retained explanation. These are parsed retained measurements, not executions performed by this review and not exact-head PR CI acceptance.

The evidence/rehearsal reviewer disclosed one read-only search whose raw exclusion glob was ineffective and emitted incidental matches from 16 raw inventory/XML paths. The search was stopped/corrected; no emitted raw match was relied upon for a finding or verification conclusion. Its exact known path list is retained in that group's scratch report. This review-scope incident is disclosed rather than claiming flawless raw-read isolation. The lead's targeted payload opens were citation-triggered; no raw file was modified.

Image archive/smoke, actual Helm render, committed real service-tag acquisition, TFLint and required exact-head PR CI evidence remain explicitly incomplete. The authored README does not claim those acceptances or live readiness. This review grants no merge/live clearance.

Independent group reports: /workspace/scratch/issue-114-review12-contracts.md (27 files), issue-114-review12-execution.md (31), issue-114-review12-evidence.md (30), issue-114-review12-infrastructure.md (22) and issue-114-review12-workers.md (47). Findings were checked against their source/rules before consolidation; duplicate project/reference observations were grouped into the three mandatory approval classes above. Together with the lead's 39 paths, these cover all 196 files.

## Every-file rubric coverage

Each row records a full assigned read and application of every rule in its applicable rubrics, with the cross-file checks described above. G applies to every file; all applicable added rows are listed.

| Changed authored file | Applied rubric | Full-read reviewer |
|---|---|---|
| `Directory.Build.props` | G, N | Workers/scenarios |
| `Parchwell.slnx` | G, N | Workers/scenarios |
| `charts/parchwell/Chart.yaml` | G, K | Infrastructure/chart |
| `charts/parchwell/templates/_helpers.tpl` | G, K | Infrastructure/chart |
| `charts/parchwell/templates/admission.yaml` | G, K | Infrastructure/chart |
| `charts/parchwell/templates/generated-values.yaml` | G, K | Infrastructure/chart |
| `charts/parchwell/templates/jobs.yaml` | G, K | Infrastructure/chart |
| `charts/parchwell/templates/networkpolicies.yaml` | G, K | Infrastructure/chart |
| `charts/parchwell/templates/serviceaccounts.yaml` | G, K | Infrastructure/chart |
| `charts/parchwell/values.schema.json` | G, K | Infrastructure/chart |
| `charts/parchwell/values.yaml` | G, K | Infrastructure/chart |
| `docs/blueprint.md` | G | Lead documents/provenance |
| `docs/engineering/dotnet.md` | G, N, E | Lead documents/provenance |
| `docs/engineering/terraform.md` | G, T, E | Lead documents/provenance |
| `docs/operations/a01-runner.md` | G, K | Lead documents/provenance |
| `docs/operations/a01-runtime-limits.json` | G, K | Lead documents/provenance |
| `docs/operations/evidence/issue-114/README.md` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/acquisition-status-provenance.json` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/acquisition-status.md` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/aks-binding-producers.json` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/chart-tool-acquisition-status.json` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/cold-review-1.md` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/cold-review-10.md` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/cold-review-11.md` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/cold-review-2.md` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/cold-review-3.md` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/cold-review-4.md` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/cold-review-5.md` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/cold-review-6.md` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/cold-review-7.md` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/cold-review-8.md` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/cold-review-9.md` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/local-producers.json` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/placement-producers.json` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/raw-manifest.json` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/resume-producers.json` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/resume-status.md` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/review-10-fetch-sources.py` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/review-10-kql-audit.json` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/review-10-producers.json` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/review-11-producers.json` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/review-4-producers.json` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/review-5-producers.json` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/review-7-producers.json` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/review-8-producers.json` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/review-9-producers.json` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/review-fix-producers.json` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/sweeper-authority.md` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/terraform-producers.json` | G | Lead documents/provenance |
| `docs/operations/evidence/issue-114/write-raw-manifest.mjs` | G | Lead documents/provenance |
| `docs/operations/kql/a01-runtime.kql` | G | Infrastructure/chart |
| `docs/operations/loadgen-contract.md` | G, K | Workers/scenarios |
| `docs/operations/scenario-contract.md` | G, K | Workers/scenarios |
| `docs/operations/terraform.md` | G, T | Infrastructure/chart |
| `docs/operations/verifier-contract.md` | G, K | Workers/scenarios |
| `docs/operations/workflows/a01-deployment-acceptance.yml.txt` | G, W | Infrastructure/chart |
| `docs/operations/workflows/scenario.yml.txt` | G, W | Infrastructure/chart |
| `docs/operations/workflows/sweeper.yml.txt` | G, W | Infrastructure/chart |
| `infra/persistent/locals.tf` | G, T | Infrastructure/chart |
| `infra/persistent/outputs.tf` | G, T | Infrastructure/chart |
| `infra/persistent/storage.tf` | G, T | Infrastructure/chart |
| `scripts/fetch-service-tags.sh` | G | Infrastructure/chart |
| `scripts/verification/assert-a01-deployment.py` | G, K | Infrastructure/chart |
| `scripts/verify-a01-deployment.sh` | G, K | Infrastructure/chart |
| `src/Parchwell.Kafka/packages.linux-x64.lock.json` | G, N | Workers/scenarios |
| `src/Parchwell.LoadGen/Parchwell.LoadGen.csproj` | G, N | Workers/scenarios |
| `src/Parchwell.LoadGen/Program.cs` | G, N | Workers/scenarios |
| `src/Parchwell.LoadGen/Run/Execution/LoadGenWorker.cs` | G, N | Workers/scenarios |
| `src/Parchwell.LoadGen/Run/Input/DeploymentAdmission.cs` | G, N, K | Workers/scenarios |
| `src/Parchwell.LoadGen/Run/Input/LoadGenHostOptions.cs` | G, N | Workers/scenarios |
| `src/Parchwell.LoadGen/Run/Ledger/AssemblyInfo.cs` | G, N | Workers/scenarios |
| `src/Parchwell.LoadGen/packages.linux-x64.lock.json` | G, N | Workers/scenarios |
| `src/Parchwell.Runner/Parchwell.Runner.csproj` | G, N | Evidence/rehearsal |
| `src/Parchwell.Runner/Program.cs` | G, N | Evidence/rehearsal |
| `src/Parchwell.Runner/Run/Contracts/ExecutionBudgets.cs` | G, N | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Contracts/IArtifactStore.cs` | G, N | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Contracts/IProcessRunner.cs` | G, N | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Contracts/IRunCluster.cs` | G, N | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Contracts/IRunGate.cs` | G, N | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Contracts/ITerraformEnvironment.cs` | G, N | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Contracts/WorkerIdentity.cs` | G, N | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Cost/ActualCostReconciler.cs` | G, N | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Cost/AzureCliRunStore.cs` | G, N | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Cost/LedgerModels.cs` | G, N | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Cost/PhaseReservation.cs` | G, N | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Cost/RunLedger.cs` | G, N | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Deployment/ChartRenderer.cs` | G, N, K | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Deployment/DeploymentGenerator.cs` | G, N, K | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Deployment/DeploymentPreflight.cs` | G, N, K | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Deployment/DeploymentRuntimeLimits.cs` | G, N, K | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Deployment/DeploymentScenarioSource.cs` | G, N, K | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Deployment/ServiceTagData.cs` | G, N, K | Contracts/Cost/Deployment |
| `src/Parchwell.Runner/Run/Evidence/EvidenceExporter.cs` | G, N | Evidence/rehearsal |
| `src/Parchwell.Runner/Run/Evidence/KqlCollector.cs` | G, N | Evidence/rehearsal |
| `src/Parchwell.Runner/Run/Evidence/PhaseCandidate.cs` | G, N | Evidence/rehearsal |
| `src/Parchwell.Runner/Run/Evidence/PrivateEvidenceCollector.cs` | G, N | Evidence/rehearsal |
| `src/Parchwell.Runner/Run/Evidence/PrivateProvenanceCollector.cs` | G, N | Evidence/rehearsal |
| `src/Parchwell.Runner/Run/Evidence/PublicCandidate.cs` | G, N | Evidence/rehearsal |
| `src/Parchwell.Runner/Run/Execution/AksKubeconfigBinding.cs` | G, N | Execution |
| `src/Parchwell.Runner/Run/Execution/BoundedProcessRunner.cs` | G, N | Execution |
| `src/Parchwell.Runner/Run/Execution/BrokerInventory.cs` | G, N | Execution |
| `src/Parchwell.Runner/Run/Execution/GenerationGuard.cs` | G, N | Execution |
| `src/Parchwell.Runner/Run/Execution/KubernetesRunCluster.cs` | G, N | Execution |
| `src/Parchwell.Runner/Run/Execution/LocalVerifierGate.cs` | G, N | Execution |
| `src/Parchwell.Runner/Run/Execution/PhaseExecutor.cs` | G, N | Execution |
| `src/Parchwell.Runner/Run/Execution/PodAdmission.cs` | G, N | Execution |
| `src/Parchwell.Runner/Run/Execution/ProbeGateRunner.cs` | G, N | Execution |
| `src/Parchwell.Runner/Run/Execution/PublicationAcceptance.cs` | G, N | Execution |
| `src/Parchwell.Runner/Run/Execution/RecoveryEvidenceCollector.cs` | G, N | Execution |
| `src/Parchwell.Runner/Run/Execution/RepeatExecutor.cs` | G, N | Execution |
| `src/Parchwell.Runner/Run/Execution/SweepExecutor.cs` | G, N | Execution |
| `src/Parchwell.Runner/Run/Execution/TerraformEnvironment.cs` | G, N | Execution |
| `src/Parchwell.Runner/Run/Execution/WorkerBlobDownloader.cs` | G, N | Execution |
| `src/Parchwell.Runner/Run/Execution/WorkerOutcome.cs` | G, N | Execution |
| `src/Parchwell.Runner/Run/Rehearsal/RehearsalRunner.cs` | G, N | Evidence/rehearsal |
| `src/Parchwell.Runner/Run/Rehearsal/RehearsalScenarioFactory.cs` | G, N | Evidence/rehearsal |
| `src/Parchwell.Runner/Run/Rehearsal/RehearsalTransports.cs` | G, N | Evidence/rehearsal |
| `src/Parchwell.Runner/Run/Rehearsal/WireFixtures.cs` | G, N | Evidence/rehearsal |
| `src/Parchwell.Runner/packages.lock.json` | G, N | Evidence/rehearsal |
| `src/Parchwell.Scenarios/Generation/GeneratedValuesCompatibility.cs` | G, N, K | Workers/scenarios |
| `src/Parchwell.Scenarios/Generation/HelmValuesGenerator.cs` | G, N, K | Workers/scenarios |
| `src/Parchwell.Scenarios/packages.linux-x64.lock.json` | G, N | Workers/scenarios |
| `src/Parchwell.Verifier/AssemblyInfo.cs` | G, N | Workers/scenarios |
| `src/Parchwell.Verifier/Common/Kafka/VerifierKafkaSession.cs` | G, N | Workers/scenarios |
| `src/Parchwell.Verifier/Common/Serialization/DeploymentJson.cs` | G, N, K | Workers/scenarios |
| `src/Parchwell.Verifier/ImageSmoke.cs` | G, N | Workers/scenarios |
| `src/Parchwell.Verifier/Parchwell.Verifier.csproj` | G, N | Workers/scenarios |
| `src/Parchwell.Verifier/Program.cs` | G, N | Workers/scenarios |
| `src/Parchwell.Verifier/StartLine/BrokerInventoryCodec.cs` | G, N | Workers/scenarios |
| `src/Parchwell.Verifier/StartLine/StartLineHandler.cs` | G, N | Workers/scenarios |
| `src/Parchwell.Verifier/VerifierCompletionState.cs` | G, N | Workers/scenarios |
| `src/Parchwell.Verifier/VerifierHostOptions.cs` | G, N | Workers/scenarios |
| `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | G, N | Workers/scenarios |
| `src/Parchwell.Verifier/VerifierWorker.cs` | G, N | Workers/scenarios |
| `src/Parchwell.Verifier/VerifyRun/A01/A01EvidenceRecorder.cs` | G, N | Workers/scenarios |
| `src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs` | G, N | Workers/scenarios |
| `src/Parchwell.Verifier/packages.linux-x64.lock.json` | G, N | Workers/scenarios |
| `tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj` | G, N | Workers/scenarios |
| `tests/Parchwell.ArchitectureTests/ProjectModel.cs` | G, N | Workers/scenarios |
| `tests/Parchwell.ArchitectureTests/packages.lock.json` | G, N | Workers/scenarios |
| `tests/Parchwell.LoadGen.Tests/Host/DeploymentAdmissionTests.cs` | G, N, K | Workers/scenarios |
| `tests/Parchwell.LoadGen.Tests/Host/GeneratedValuesCompatibilityTests.cs` | G, N | Workers/scenarios |
| `tests/Parchwell.LoadGen.Tests/Host/LoadGenWorkerHostTests.cs` | G, N | Workers/scenarios |
| `tests/Parchwell.Runner.Tests/ChartContractTests.cs` | G, N | Infrastructure/chart |
| `tests/Parchwell.Runner.Tests/Contracts/ExecutionBudgetsTests.cs` | G, N | Contracts/Cost/Deployment |
| `tests/Parchwell.Runner.Tests/Cost/ActualCostReconcilerTests.cs` | G, N | Contracts/Cost/Deployment |
| `tests/Parchwell.Runner.Tests/Cost/AzureCliRunStoreTests.cs` | G, N | Contracts/Cost/Deployment |
| `tests/Parchwell.Runner.Tests/Cost/PhaseReservationTests.cs` | G, N | Contracts/Cost/Deployment |
| `tests/Parchwell.Runner.Tests/Cost/RuntimeCostBoundaryTests.cs` | G, N | Contracts/Cost/Deployment |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentGeneratorTests.cs` | G, N, K | Contracts/Cost/Deployment |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentPreflightTests.cs` | G, N, K | Contracts/Cost/Deployment |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentRuntimeLimitsTests.cs` | G, N, K | Contracts/Cost/Deployment |
| `tests/Parchwell.Runner.Tests/Deployment/ServiceTagDataTests.cs` | G, N, K | Contracts/Cost/Deployment |
| `tests/Parchwell.Runner.Tests/Evidence/CandidateContractTests.cs` | G, N | Evidence/rehearsal |
| `tests/Parchwell.Runner.Tests/Evidence/EvidenceExportBoundaryTests.cs` | G, N | Evidence/rehearsal |
| `tests/Parchwell.Runner.Tests/Evidence/EvidenceExporterTests.cs` | G, N | Evidence/rehearsal |
| `tests/Parchwell.Runner.Tests/Evidence/KqlBoundaryTests.cs` | G, N | Evidence/rehearsal |
| `tests/Parchwell.Runner.Tests/Evidence/PhaseCandidateTests.cs` | G, N | Evidence/rehearsal |
| `tests/Parchwell.Runner.Tests/Evidence/PrivateProvenanceCollectorTests.cs` | G, N | Evidence/rehearsal |
| `tests/Parchwell.Runner.Tests/Evidence/PublicCandidateBoundaryTests.cs` | G, N | Evidence/rehearsal |
| `tests/Parchwell.Runner.Tests/Evidence/RecoveryExportTests.cs` | G, N | Evidence/rehearsal |
| `tests/Parchwell.Runner.Tests/Evidence/azure-cli-query-recording.json` | G, N | Evidence/rehearsal |
| `tests/Parchwell.Runner.Tests/Execution/AksKubeconfigBindingTests.cs` | G, N | Execution |
| `tests/Parchwell.Runner.Tests/Execution/BoundedProcessRunnerTests.cs` | G, N | Execution |
| `tests/Parchwell.Runner.Tests/Execution/DownloadBudgetTests.cs` | G, N | Execution |
| `tests/Parchwell.Runner.Tests/Execution/FailureArtifactPreservationTests.cs` | G, N | Execution |
| `tests/Parchwell.Runner.Tests/Execution/GenerationGuardTests.cs` | G, N | Execution |
| `tests/Parchwell.Runner.Tests/Execution/KubernetesWatchTests.cs` | G, N | Execution |
| `tests/Parchwell.Runner.Tests/Execution/NormalDownloadBudgetTests.cs` | G, N | Execution |
| `tests/Parchwell.Runner.Tests/Execution/PhaseEvidenceBoundaryTests.cs` | G, N | Execution |
| `tests/Parchwell.Runner.Tests/Execution/PhaseEvidenceInventoryTests.cs` | G, N | Execution |
| `tests/Parchwell.Runner.Tests/Execution/PodAdmissionTests.cs` | G, N | Execution |
| `tests/Parchwell.Runner.Tests/Execution/PublicationBoundaryTests.cs` | G, N | Execution |
| `tests/Parchwell.Runner.Tests/Execution/RecoveryEvidenceTests.cs` | G, N | Execution |
| `tests/Parchwell.Runner.Tests/Execution/TerraformEnvironmentTests.cs` | G, N | Execution |
| `tests/Parchwell.Runner.Tests/Execution/WorkerBlobBoundaryTests.cs` | G, N | Execution |
| `tests/Parchwell.Runner.Tests/Execution/WorkerOutcomeBoundaryTests.cs` | G, N | Execution |
| `tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj` | G, N | Evidence/rehearsal |
| `tests/Parchwell.Runner.Tests/RehearsalTests.cs` | G, N | Evidence/rehearsal |
| `tests/Parchwell.Runner.Tests/Support/WorkerWireFixtures.cs` | G, N | Evidence/rehearsal |
| `tests/Parchwell.Runner.Tests/WireFixtureBoundsTests.cs` | G, N | Evidence/rehearsal |
| `tests/Parchwell.Runner.Tests/WorkerPublicationFixture.cs` | G, N, K | Evidence/rehearsal |
| `tests/Parchwell.Runner.Tests/WorkerWireCompatibilityTests.cs` | G, N | Evidence/rehearsal |
| `tests/Parchwell.Runner.Tests/WorkerWireFixtureTests.cs` | G, N, K | Evidence/rehearsal |
| `tests/Parchwell.Runner.Tests/packages.lock.json` | G, N | Evidence/rehearsal |
| `tests/Parchwell.Scenarios.Tests/GeneratedValuesCompatibilityTests.cs` | G, N | Workers/scenarios |
| `tests/Parchwell.Verifier.Tests/A01/A01C3EvidenceRecorderTests.cs` | G, N | Workers/scenarios |
| `tests/Parchwell.Verifier.Tests/Component/ComponentRunHarness.cs` | G, N | Workers/scenarios |
| `tests/Parchwell.Verifier.Tests/Host/BrokerInventoryTests.cs` | G, N | Workers/scenarios |
| `tests/Parchwell.Verifier.Tests/Host/C3HandlerTests.cs` | G, N | Workers/scenarios |
| `tests/Parchwell.Verifier.Tests/Host/GeneratedValuesCompatibilityTests.cs` | G, N | Workers/scenarios |
| `tests/Parchwell.Verifier.Tests/Host/ImageSmokeTests.cs` | G, N | Workers/scenarios |
| `tests/Parchwell.Verifier.Tests/Host/VerifierCompletionTests.cs` | G, N | Workers/scenarios |
| `tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | G, N | Workers/scenarios |
| `tests/Parchwell.Verifier.Tests/Support/HostEvidenceBuilder.cs` | G, N | Workers/scenarios |
| `tests/terraform/persistent/main.tftest.hcl` | G, T | Infrastructure/chart |

# Issue 114 cold review 13

Fresh-context cold review of the full authored branch diff against `origin/main`, applying AGENTS.md "Before opening a pull request" rule 8 and the repository-owned `repo-review` skill. Five fresh-context independent reviewers read complete, non-overlapping assigned groups; the lead checked governing rules, scope, current decision evidence, cross-file behavior and the consolidated findings.

- Frozen source HEAD: `d60de515d9404e0563cfe102c2b6a37968f1221f`.
- Rule baseline: `origin/main` at `4a0acc5d886a12cafdfdeafbe1e71f4d0802bf55`.
- Only author-provided inputs: `/workspace/scratch/issue-114-cold-review-13.diff` and `docs/operations/evidence/issue-114/raw-manifest.json`.
- Script-computed diff identity: SHA-256 `ceb10d355c0843395e27b2a99be150f665aa6cee84e0ec7a9b372795a1f16309`, 1,981,475 bytes, 26,112 lines and 203 distinct authored files. The supplied bytes exactly match `git diff origin/main` with only `docs/operations/evidence/**/raw/**` excluded. `.gitattributes` marks only that raw pathspec generated.
- Manifest SHA-256: `321f9ead6b7421762a269a8282a06235184113e282113c1667a60ff7f4fc5ccf`.
- Static review only: no source edits, tests, builds, full-suite/coverage execution, live operations, commits, pushes or external writes. Only scratch review/source-read artifacts were written.

## Findings

P0  docs/blueprint.md:771  AGENTS.md "Code Review Rules" identity-scope rule; repo-review step 2; origin/main blueprint section 9 "Control plane"  The second no-reviewer `parchwell-sweeper` environment expands federation trust for the existing apply-capable app beyond the baseline's sole protected scenario environment requiring Hari's approval. Destroy-only workflow commands do not reduce the existing app's Azure permissions for that additional subject. Evidence: changed blueprint line 771, `docs/operations/workflows/sweeper.yml.txt:16` and its login/guard blocks, `docs/operations/a01-runner.md` installation prerequisites, baseline blueprint section 9, and recognized #114 decision 6332413257 point 4. That decision expressly requires the exact design. Later recognized decision 6336121859 explicitly classifies this exact P0 as approval-only under rule 8: retain it, list it in the PR description, leave merge to Hari and exclude it from all round limits. Classification: the same P0 as rounds 11/12, counted once, now approval-only by express current disposition; severity is preserved.
P1  Parchwell.slnx:8  AGENTS.md "Code Review Rules" (flag any new project)  Adds Runner and Runner.Tests, also solution line 16 and their new project files. Evidence: complete diff and #114 Revision 2 "File changes and dependency boundary", which expressly requires both. Classification: plan-required approval-only under AGENTS.md rule 8; disclose in the PR description, leave merge to Hari, exclude from review-round stop limits.
P1  src/Parchwell.Runner/Parchwell.Runner.csproj:6  AGENTS.md "Code Review Rules" (flag any new assembly reference)  Adds Runner references to Scenarios/Kafka, Runner.Tests references to Runner/LoadGen/Verifier, and ArchitectureTests' Runner reference. Evidence: this project lines 6-7, `tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj:9` through 11 and `tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj:21`; Revision 2 explicitly requires all. Classification: grouped plan-required approval-only P1 under rule 8, with PR disclosure and Hari's merge; exclude from review-round stop limits.
P1  src/Parchwell.Runner/Parchwell.Runner.csproj:8  AGENTS.md "Code Review Rules" (flag any new NuGet package)  Adds Hosting/YamlDotNet PackageReference declarations in Runner and xunit.v3.mtp-v2 in Runner.Tests. Existing central pins do not remove this mandatory flag. Evidence: this project lines 8-9, test project line 8 and Revision 2 dependency boundary. Classification: grouped plan-required approval-only P1 under rule 8; retain for Hari with disclosure, exclude from stop limits. Lockfile/transitive/RID entries are covered here rather than counted again.

Findings: 4 at P0 or P1

Pre-existing (not blocking):

None independently established in inspected unchanged lines.

## Classification and documented dispute

All four findings are approval-only: the retained P0 by the exact current decision 6336121859, and three grouped P1s expressly required by the approved plan. No non-approval P0/P1 or P2 was established. The literal reporting result above preserves severity and mandatory flags; approval-only exclusions apply before round-limit counting under the recognized disposition. This is not merge clearance; Hari merges.

The lead independently read the current Revision 2 plan, its standing-approval check 6332608289, storage/recovery decision 6332413257, continuation decision 6333713470, retry amendment decision 6336034219, and the newly discovered disposition decision 6336121859 through GitHub REST. Revision 2's dependency section requires both projects and every listed project/package reference; the clean check identifies its updated revision and standing approval. Protected paths, installed kubelogin compatibility, persistent plans/applies, workflow installation and live readiness remain Hari's responsibilities, distinct from the three mandatory dependency findings.

The sweeper design is authorized by decision 6332413257 point 4 and the approved blueprint amendment. The P0 does not allege an invented or unapproved architecture. The literal baseline identity-scope flag still applies and is retained. During authority refresh, decision 6336121859 (created and updated 2026-10-22T08:17:38Z, headed `## Decision` and expressly recognized for #114) supplied the previously missing exact disposition: "Treat it as an approval-only finding under AGENTS.md ... rule 8, third paragraph: keep it, list it in the pull request description, and do not count it toward any round limit. Hari merges." It supplements decision 6336034219, so its later explicit classification controls. This is a specific decision about this finding, not a generalized exception for identity/network changes.

The frozen authored `resume-status.md`, `sweeper-authority.md` and rounds 11/12 preserve the earlier evidence-backed `Not valid:` dispute, its original P0 severity, factual trust expansion and pre-decision interpretation. They remain historical evidence. The newly recognized decision changes current disposition; it does not make the earlier factual flag false, erase the dispute history or authorize an alternative architecture. The author should record this current disposition when saving the round-13 self-review. All finding disclosures and protected merge ownership remain.

The retry P1 from round 12 is historical, not a current finding: recognized decision 6336034219 replaces the original no-write-retry phrase with no Runner-initiated write retry, permits the required CLI's internal conditional SDK retries within existing deadlines, and preserves CLI conflict-stop. The current adapter invokes the write once, `RunLedger.PersistAsync` stops immediately on Conflict, and other uncertain outcomes retain bounded exact-byte readback. The two new synthetic committed-upload/CLI-412 cases exercise the real adapter and ledger through a fake process, assert no returned transition/readback/reinvocation after conflict, preserve preconditions and deadlines, and explicitly disclaim SDK HTTP execution. The operational contract and class summary match the amendment. Evidence export retains its separate unchanged immutable exact-byte readback protocol.

Decision 6333713470 and the authored override record remain historical evidence for earlier trips. Decision 6336121859 point 2 now ends that churn override after two more cold-review rounds: if round 14 still reports a non-approval-only P0/P1, open the PR with every open finding listed and hand off. Current approval-only findings are excluded from round limits as explicitly directed; no non-approval finding was established in this round. This report neither generalizes that exact decision nor changes the other stated gates or Hari's merge ownership.

## Complete rubric application

G applies every rule in the repo-review "anything" row to every file: AGENTS.md GOVERNANCE (including frozen/deferred scope, all hard safety rules, verdict integrity, merge/handoff/truth), Code Review Rules, Before opening a pull request rules 1-13, Skills and Writing standards; CLAUDE.md; docs/agents/factory.md; blueprint ADR-001 through ADR-019 and scope/inventory. Changed blueprint and engineering authorities were evaluated using their `origin/main` versions. No applicable authority lacked a baseline version. The repo-review skill itself was read independently. New branch operational/engineering statements were checked as proposed implementation contracts, not substituted for baseline governance.

N adds the complete origin/main .NET standards: solution/slice/dependency boundaries, DI/factories, validated configuration/startup, TimeProvider, exact central packages, nullable/warnings/analyzers, behavior/boundary tests, preservation of assertions, architecture and logging. T adds complete origin/main Terraform standards and ADR-011: pinned module/provider hierarchy, layout, variables/names/tags, external state, apply-time values and offline/live ownership. E additionally applies each changed engineering document's own baseline. K adds blueprint Workloads/inventory and ADR-013/014/015 settings fidelity. W additionally checks installable workflow text against safety rules 8/9, main/source guards, OIDC/trust, teardown and private-artifact rules. No actual `.github/workflows/`, scenario definition, expected outcome, results, docs/adr file or ready-for-agent operation changes appear. Analyzer-enforced style and ordinary comment presence/style were excluded under the review rules.

Cross-file review covered generated full-byte equality and native role identities, producer admission/list-watch continuity, independent broker inventory, publication/worker receipt/exit/status/count agreement, unchanged scientific judgement, fixed repeats and cost reservations, complete actual-cost scopes, immutable private original-byte export/recovery and public allowlist/redaction, explicit AKS context, and saved-plan generation/serial/lineage cleanup. Existing modified test assertions were read; no loosened/deleted/skipped assertion was established. The four RID lockfiles preserve ordinary net10.0 dependency maps and pinned Kafka/native dependencies.

## Independent groups and source checks

- Workers/shared: 44 files, 3,667 diff lines; `/workspace/scratch/review13-workers-report.md`.
- Deployment/docs/infra/scripts: 29 files, 2,278 lines; `/workspace/scratch/review13-deployment_docs-report.md`.
- Authored evidence/provenance: 41 files, 7,784 lines; `/workspace/scratch/review13-evidence-report.md`.
- Runner contracts/cost/deployment/evidence/rehearsal: 58 files, 7,779 lines; `/workspace/scratch/review13-runner_other-report.md`.
- Execution and its tests: 31 files, 4,604 lines; `/workspace/scratch/review13-execution-report.md`.

The five file lists form an exact non-overlapping partition of all 203 diff files. Each group's report records per-file rule application and supporting sources; duplicate project/reference observations are grouped into the three mandatory approval classes above. Findings were checked against changed lines, baseline rules and current decision evidence before consolidation.

Primary-source reads across the groups and lead included the #114 current plan/check/storage/recovery/continuation/retry/disposition authority; the pinned Azure CLI Blob preconditions, upload/client path, ACR metadata and non-admin AKS get-credentials sources at `da4c0548a2725ea3d08b1d94b8e7fa1b0b07256e`; SDK conditional retry source at `5eca695d87ce704eabe665ce42c698440521c289`; KQL CLI flattening at extensions `74172d7cf4637fdf039e6b9325498feaed459888`; librdkafka v2.16.0 broker naming/IPv6 source; kubectl v0.35.0 raw watch/log producers; Terraform v1.16.6 absent-state behavior; and kubelogin `bdf0e79b5a0ea6fe9fd3b7ada771378c344ad760` existing CLI login/subscription semantics. The current retry wording and corrected KQL source locator agree with those sources.

Infrastructure/documentation reads included pinned storage AVM `5695e5ec21744a3765051677d9e82bfba16b5d98` security/container/private-endpoint/output properties; AKS AVM `4bc0128b057d10d5ed146a53d8c77933fd00ead8` node-group output and AKS FAQ `6901de76c108a545a6b17fee61a39ca9e9e001d1`; Azure Monitor table schemas at `6c8e97c9e8b8e57c19e3b319bda5188e4ceba029` and Kusto literal/union/tolong sources at `85351b30116da41b75c5a4e8a5b7f5730d81ab08` and `ac19a887fb20eab5c08ec7742478621be92c56fd`; official Kubernetes NetworkPolicy selector conjunction/additive semantics; official Helm v4.3.1 release/checksum; and the pinned MCR digest endpoint. The mutable current runtime tag differs from the explicit historical digest; the pinned digest itself is available and unchanged, so mutable-tag movement is not a defect in this source pin.

Access limits were explicit. Direct get.helm.sh checksum access and kubernetes.io documentation access returned HTTP 403 in individual groups; the official Helm release API and official kubernetes/website source supplied the corresponding primary evidence. The Runner reviewer additionally checked only the authored-cited retained Helm release JSON, with manifest size/hash verification. No download-host bypass, tool installation, image execution or live operation occurred.

Manifest audit covered 574 unique raw entries and 161 distinct producer strings. Every listed path is in the evidence raw tree, exists as a regular non-symlink file and matches its stat byte size, with valid SHA-256/source-revision metadata; there is no unlisted raw file. All 15 authored producer maps and their 190 mapped command entries exactly agree with manifest producer/source/status strings. All unique producer commands were read, including the embedded workflow-preparation script and mechanical source-subset producers. These are metadata/stat checks, not a claim that every uncited payload was opened or hashed.

The evidence reviewer opened exactly 58 distinct citation-triggered raw payloads and recomputed matching sizes/SHA-256 values for all of them; the exact list is `/workspace/scratch/review13-evidence-cited-payloads.txt`. Citation triggers came from authored README/acquisition/history/provenance/source-receipt records. The Runner reviewer independently verified the cited Helm capture; the lead independently rechecked the current retry build/store XML and prior full Runner XML, including hashes and actual xUnit assembly/test totals. No uncited raw search/read or raw-scope incident was reported. The retained XMLs support 1001 prior full local Runner tests, 33 focused LoadGen tests, 382 focused Verifier host cases, 35 query cases, 28 pre-amendment and 30 post-amendment store cases, with the recorded zero failures/errors/skips. The current build has zero warnings/errors. The original captures also support twelve Terraform mock tests, twelve AKS binding cases, the happy rehearsal plus 23 negative cases, three phases/five repeats each/three applies and destroys/zero Azure calls or replacements, and nine workflow-preparation cases. These are retained measurements, not review-time test executions or current-head PR acceptance.

Historical rounds 1-12 diff/manifest identities and recorded file/line counts reproduce from their frozen evidence; their earlier totals were not silently replaced by later totals. The cited pre-build retry inventory's 535 repository inputs outside evidence match the frozen source. The evidence reviewer fetched 21 pinned small CLI/SDK/KQL source files from official URLs; their bytes/hashes match authored receipts and retained source bytes. Original source/archive reads remained static analysis. The original large CLI archive is correctly described as a scratch source with immutable receipt, not a committed raw file. Documentation captures retain the disclosed 36 advisory upstream-source warnings. Historical analyzer/fixture failures and absent deployment artifacts remain visible rather than being rewritten as successes.

Image archive/smoke, actual Helm rendering, committed real service-tag acquisition, TFLint and required exact-head PR CI remain explicitly incomplete. Retained local/synthetic checks are not substituted for them or for live identity/DNS/network readiness, actual settled cost or results. The report grants no deployment or merge clearance.

## Every changed file and rubric coverage

Each row records a complete assigned diff read and application of every rule in the listed rubrics. Group reports provide file-specific checks and source details. Build/project/lock inputs receive N as additional .NET contract checks.

| Changed file | Rubric | Independent group |
|---|---|---|
| `Directory.Build.props` | G + N | workers |
| `Parchwell.slnx` | G + N | workers |
| `charts/parchwell/Chart.yaml` | G | deployment_docs |
| `charts/parchwell/templates/_helpers.tpl` | G | deployment_docs |
| `charts/parchwell/templates/admission.yaml` | G | deployment_docs |
| `charts/parchwell/templates/generated-values.yaml` | G + K | deployment_docs |
| `charts/parchwell/templates/jobs.yaml` | G | deployment_docs |
| `charts/parchwell/templates/networkpolicies.yaml` | G | deployment_docs |
| `charts/parchwell/templates/serviceaccounts.yaml` | G | deployment_docs |
| `charts/parchwell/values.schema.json` | G + K | deployment_docs |
| `charts/parchwell/values.yaml` | G | deployment_docs |
| `docs/blueprint.md` | G | deployment_docs |
| `docs/engineering/dotnet.md` | G + E + N | deployment_docs |
| `docs/engineering/terraform.md` | G + T + E | deployment_docs |
| `docs/operations/a01-runner.md` | G + K | deployment_docs |
| `docs/operations/a01-runtime-limits.json` | G + K | deployment_docs |
| `docs/operations/evidence/issue-114/README.md` | G | evidence |
| `docs/operations/evidence/issue-114/acquisition-status-provenance.json` | G | evidence |
| `docs/operations/evidence/issue-114/acquisition-status.md` | G | evidence |
| `docs/operations/evidence/issue-114/aks-binding-producers.json` | G | evidence |
| `docs/operations/evidence/issue-114/blob-retry-investigation.md` | G | evidence |
| `docs/operations/evidence/issue-114/chart-tool-acquisition-status.json` | G | evidence |
| `docs/operations/evidence/issue-114/cold-review-1.md` | G | evidence |
| `docs/operations/evidence/issue-114/cold-review-10.md` | G | evidence |
| `docs/operations/evidence/issue-114/cold-review-11.md` | G | evidence |
| `docs/operations/evidence/issue-114/cold-review-12.md` | G | evidence |
| `docs/operations/evidence/issue-114/cold-review-2.md` | G | evidence |
| `docs/operations/evidence/issue-114/cold-review-3.md` | G | evidence |
| `docs/operations/evidence/issue-114/cold-review-4.md` | G | evidence |
| `docs/operations/evidence/issue-114/cold-review-5.md` | G | evidence |
| `docs/operations/evidence/issue-114/cold-review-6.md` | G | evidence |
| `docs/operations/evidence/issue-114/cold-review-7.md` | G | evidence |
| `docs/operations/evidence/issue-114/cold-review-8.md` | G | evidence |
| `docs/operations/evidence/issue-114/cold-review-9.md` | G | evidence |
| `docs/operations/evidence/issue-114/local-producers.json` | G | evidence |
| `docs/operations/evidence/issue-114/placement-producers.json` | G | evidence |
| `docs/operations/evidence/issue-114/raw-manifest.json` | G | evidence |
| `docs/operations/evidence/issue-114/resume-producers.json` | G | evidence |
| `docs/operations/evidence/issue-114/resume-status.md` | G | evidence |
| `docs/operations/evidence/issue-114/retry-amendment-producers.json` | G | evidence |
| `docs/operations/evidence/issue-114/retry-amendment-proposal.md` | G | evidence |
| `docs/operations/evidence/issue-114/review-10-fetch-sources.py` | G | evidence |
| `docs/operations/evidence/issue-114/review-10-kql-audit.json` | G | evidence |
| `docs/operations/evidence/issue-114/review-10-producers.json` | G | evidence |
| `docs/operations/evidence/issue-114/review-11-producers.json` | G | evidence |
| `docs/operations/evidence/issue-114/review-12-producers.json` | G | evidence |
| `docs/operations/evidence/issue-114/review-12-source-commands.json` | G | evidence |
| `docs/operations/evidence/issue-114/review-12-source-receipts.json` | G | evidence |
| `docs/operations/evidence/issue-114/review-4-producers.json` | G | evidence |
| `docs/operations/evidence/issue-114/review-5-producers.json` | G | evidence |
| `docs/operations/evidence/issue-114/review-7-producers.json` | G | evidence |
| `docs/operations/evidence/issue-114/review-8-producers.json` | G | evidence |
| `docs/operations/evidence/issue-114/review-9-producers.json` | G | evidence |
| `docs/operations/evidence/issue-114/review-fix-producers.json` | G | evidence |
| `docs/operations/evidence/issue-114/sweeper-authority.md` | G | evidence |
| `docs/operations/evidence/issue-114/terraform-producers.json` | G | evidence |
| `docs/operations/evidence/issue-114/write-raw-manifest.mjs` | G | evidence |
| `docs/operations/kql/a01-runtime.kql` | G | deployment_docs |
| `docs/operations/loadgen-contract.md` | G + K | deployment_docs |
| `docs/operations/scenario-contract.md` | G + K | deployment_docs |
| `docs/operations/terraform.md` | G | deployment_docs |
| `docs/operations/verifier-contract.md` | G + K | deployment_docs |
| `docs/operations/workflows/a01-deployment-acceptance.yml.txt` | G + W | deployment_docs |
| `docs/operations/workflows/scenario.yml.txt` | G + W + K | deployment_docs |
| `docs/operations/workflows/sweeper.yml.txt` | G + W | deployment_docs |
| `infra/persistent/locals.tf` | G + T | deployment_docs |
| `infra/persistent/outputs.tf` | G + T | deployment_docs |
| `infra/persistent/storage.tf` | G + T | deployment_docs |
| `scripts/fetch-service-tags.sh` | G | deployment_docs |
| `scripts/verification/assert-a01-deployment.py` | G + K | deployment_docs |
| `scripts/verify-a01-deployment.sh` | G + K | deployment_docs |
| `src/Parchwell.Kafka/packages.linux-x64.lock.json` | G + N | workers |
| `src/Parchwell.LoadGen/Parchwell.LoadGen.csproj` | G + N | workers |
| `src/Parchwell.LoadGen/Program.cs` | G + N + K | workers |
| `src/Parchwell.LoadGen/Run/Execution/LoadGenWorker.cs` | G + N + K | workers |
| `src/Parchwell.LoadGen/Run/Input/DeploymentAdmission.cs` | G + N + K | workers |
| `src/Parchwell.LoadGen/Run/Input/LoadGenHostOptions.cs` | G + N + K | workers |
| `src/Parchwell.LoadGen/Run/Ledger/AssemblyInfo.cs` | G + N + K | workers |
| `src/Parchwell.LoadGen/packages.linux-x64.lock.json` | G + N | workers |
| `src/Parchwell.Runner/Parchwell.Runner.csproj` | G + N | runner_other |
| `src/Parchwell.Runner/Program.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Contracts/ExecutionBudgets.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Contracts/IArtifactStore.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Contracts/IProcessRunner.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Contracts/IRunCluster.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Contracts/IRunGate.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Contracts/ITerraformEnvironment.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Contracts/WorkerIdentity.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Cost/ActualCostReconciler.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Cost/AzureCliRunStore.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Cost/LedgerModels.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Cost/PhaseReservation.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Cost/RunLedger.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Deployment/ChartRenderer.cs` | G + N + K | runner_other |
| `src/Parchwell.Runner/Run/Deployment/DeploymentGenerator.cs` | G + N + K | runner_other |
| `src/Parchwell.Runner/Run/Deployment/DeploymentPreflight.cs` | G + N + K | runner_other |
| `src/Parchwell.Runner/Run/Deployment/DeploymentRuntimeLimits.cs` | G + N + K | runner_other |
| `src/Parchwell.Runner/Run/Deployment/DeploymentScenarioSource.cs` | G + N + K | runner_other |
| `src/Parchwell.Runner/Run/Deployment/ServiceTagData.cs` | G + N + K | runner_other |
| `src/Parchwell.Runner/Run/Evidence/EvidenceExporter.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Evidence/KqlCollector.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Evidence/PhaseCandidate.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Evidence/PrivateEvidenceCollector.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Evidence/PrivateProvenanceCollector.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Evidence/PublicCandidate.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Execution/AksKubeconfigBinding.cs` | G + N | execution |
| `src/Parchwell.Runner/Run/Execution/BoundedProcessRunner.cs` | G + N | execution |
| `src/Parchwell.Runner/Run/Execution/BrokerInventory.cs` | G + N | execution |
| `src/Parchwell.Runner/Run/Execution/GenerationGuard.cs` | G + N | execution |
| `src/Parchwell.Runner/Run/Execution/KubernetesRunCluster.cs` | G + N | execution |
| `src/Parchwell.Runner/Run/Execution/LocalVerifierGate.cs` | G + N | execution |
| `src/Parchwell.Runner/Run/Execution/PhaseExecutor.cs` | G + N | execution |
| `src/Parchwell.Runner/Run/Execution/PodAdmission.cs` | G + N | execution |
| `src/Parchwell.Runner/Run/Execution/ProbeGateRunner.cs` | G + N | execution |
| `src/Parchwell.Runner/Run/Execution/PublicationAcceptance.cs` | G + N | execution |
| `src/Parchwell.Runner/Run/Execution/RecoveryEvidenceCollector.cs` | G + N | execution |
| `src/Parchwell.Runner/Run/Execution/RepeatExecutor.cs` | G + N | execution |
| `src/Parchwell.Runner/Run/Execution/SweepExecutor.cs` | G + N | execution |
| `src/Parchwell.Runner/Run/Execution/TerraformEnvironment.cs` | G + N | execution |
| `src/Parchwell.Runner/Run/Execution/WorkerBlobDownloader.cs` | G + N | execution |
| `src/Parchwell.Runner/Run/Execution/WorkerOutcome.cs` | G + N | execution |
| `src/Parchwell.Runner/Run/Rehearsal/RehearsalRunner.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Rehearsal/RehearsalScenarioFactory.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Rehearsal/RehearsalTransports.cs` | G + N | runner_other |
| `src/Parchwell.Runner/Run/Rehearsal/WireFixtures.cs` | G + N | runner_other |
| `src/Parchwell.Runner/packages.lock.json` | G + N | runner_other |
| `src/Parchwell.Scenarios/Generation/GeneratedValuesCompatibility.cs` | G + N + K | workers |
| `src/Parchwell.Scenarios/Generation/HelmValuesGenerator.cs` | G + N + K | workers |
| `src/Parchwell.Scenarios/packages.linux-x64.lock.json` | G + N | workers |
| `src/Parchwell.Verifier/AssemblyInfo.cs` | G + N + K | workers |
| `src/Parchwell.Verifier/Common/Kafka/VerifierKafkaSession.cs` | G + N + K | workers |
| `src/Parchwell.Verifier/Common/Serialization/DeploymentJson.cs` | G + N + K | workers |
| `src/Parchwell.Verifier/ImageSmoke.cs` | G + N + K | workers |
| `src/Parchwell.Verifier/Parchwell.Verifier.csproj` | G + N | workers |
| `src/Parchwell.Verifier/Program.cs` | G + N + K | workers |
| `src/Parchwell.Verifier/StartLine/BrokerInventoryCodec.cs` | G + N + K | workers |
| `src/Parchwell.Verifier/StartLine/StartLineHandler.cs` | G + N + K | workers |
| `src/Parchwell.Verifier/VerifierCompletionState.cs` | G + N + K | workers |
| `src/Parchwell.Verifier/VerifierHostOptions.cs` | G + N + K | workers |
| `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | G + N + K | workers |
| `src/Parchwell.Verifier/VerifierWorker.cs` | G + N + K | workers |
| `src/Parchwell.Verifier/VerifyRun/A01/A01EvidenceRecorder.cs` | G + N + K | workers |
| `src/Parchwell.Verifier/VerifyRun/VerifyRunHandler.cs` | G + N + K | workers |
| `src/Parchwell.Verifier/packages.linux-x64.lock.json` | G + N | workers |
| `tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj` | G + N | workers |
| `tests/Parchwell.ArchitectureTests/ProjectModel.cs` | G + N | workers |
| `tests/Parchwell.ArchitectureTests/packages.lock.json` | G + N | workers |
| `tests/Parchwell.LoadGen.Tests/Host/DeploymentAdmissionTests.cs` | G + N | workers |
| `tests/Parchwell.LoadGen.Tests/Host/GeneratedValuesCompatibilityTests.cs` | G + N | workers |
| `tests/Parchwell.LoadGen.Tests/Host/LoadGenWorkerHostTests.cs` | G + N | workers |
| `tests/Parchwell.Runner.Tests/ChartContractTests.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/Contracts/ExecutionBudgetsTests.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/Cost/ActualCostReconcilerTests.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/Cost/AzureCliRunStoreTests.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/Cost/PhaseReservationTests.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/Cost/RuntimeCostBoundaryTests.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentGeneratorTests.cs` | G + N + K | runner_other |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentPreflightTests.cs` | G + N + K | runner_other |
| `tests/Parchwell.Runner.Tests/Deployment/DeploymentRuntimeLimitsTests.cs` | G + N + K | runner_other |
| `tests/Parchwell.Runner.Tests/Deployment/ServiceTagDataTests.cs` | G + N + K | runner_other |
| `tests/Parchwell.Runner.Tests/Evidence/CandidateContractTests.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/Evidence/EvidenceExportBoundaryTests.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/Evidence/EvidenceExporterTests.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/Evidence/KqlBoundaryTests.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/Evidence/PhaseCandidateTests.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/Evidence/PrivateProvenanceCollectorTests.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/Evidence/PublicCandidateBoundaryTests.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/Evidence/RecoveryExportTests.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/Evidence/azure-cli-query-recording.json` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/Execution/AksKubeconfigBindingTests.cs` | G + N | execution |
| `tests/Parchwell.Runner.Tests/Execution/BoundedProcessRunnerTests.cs` | G + N | execution |
| `tests/Parchwell.Runner.Tests/Execution/DownloadBudgetTests.cs` | G + N | execution |
| `tests/Parchwell.Runner.Tests/Execution/FailureArtifactPreservationTests.cs` | G + N | execution |
| `tests/Parchwell.Runner.Tests/Execution/GenerationGuardTests.cs` | G + N | execution |
| `tests/Parchwell.Runner.Tests/Execution/KubernetesWatchTests.cs` | G + N | execution |
| `tests/Parchwell.Runner.Tests/Execution/NormalDownloadBudgetTests.cs` | G + N | execution |
| `tests/Parchwell.Runner.Tests/Execution/PhaseEvidenceBoundaryTests.cs` | G + N | execution |
| `tests/Parchwell.Runner.Tests/Execution/PhaseEvidenceInventoryTests.cs` | G + N | execution |
| `tests/Parchwell.Runner.Tests/Execution/PodAdmissionTests.cs` | G + N | execution |
| `tests/Parchwell.Runner.Tests/Execution/PublicationBoundaryTests.cs` | G + N | execution |
| `tests/Parchwell.Runner.Tests/Execution/RecoveryEvidenceTests.cs` | G + N | execution |
| `tests/Parchwell.Runner.Tests/Execution/TerraformEnvironmentTests.cs` | G + N | execution |
| `tests/Parchwell.Runner.Tests/Execution/WorkerBlobBoundaryTests.cs` | G + N | execution |
| `tests/Parchwell.Runner.Tests/Execution/WorkerOutcomeBoundaryTests.cs` | G + N | execution |
| `tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/RehearsalTests.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/Support/WorkerWireFixtures.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/WireFixtureBoundsTests.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/WorkerPublicationFixture.cs` | G + N + K | runner_other |
| `tests/Parchwell.Runner.Tests/WorkerWireCompatibilityTests.cs` | G + N | runner_other |
| `tests/Parchwell.Runner.Tests/WorkerWireFixtureTests.cs` | G + N + K | runner_other |
| `tests/Parchwell.Runner.Tests/packages.lock.json` | G + N | runner_other |
| `tests/Parchwell.Scenarios.Tests/GeneratedValuesCompatibilityTests.cs` | G + N | workers |
| `tests/Parchwell.Verifier.Tests/A01/A01C3EvidenceRecorderTests.cs` | G + N | workers |
| `tests/Parchwell.Verifier.Tests/Component/ComponentRunHarness.cs` | G + N | workers |
| `tests/Parchwell.Verifier.Tests/Host/BrokerInventoryTests.cs` | G + N | workers |
| `tests/Parchwell.Verifier.Tests/Host/C3HandlerTests.cs` | G + N | workers |
| `tests/Parchwell.Verifier.Tests/Host/GeneratedValuesCompatibilityTests.cs` | G + N | workers |
| `tests/Parchwell.Verifier.Tests/Host/ImageSmokeTests.cs` | G + N | workers |
| `tests/Parchwell.Verifier.Tests/Host/VerifierCompletionTests.cs` | G + N | workers |
| `tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | G + N | workers |
| `tests/Parchwell.Verifier.Tests/Support/HostEvidenceBuilder.cs` | G + N | workers |
| `tests/terraform/persistent/main.tftest.hcl` | G + T | deployment_docs |

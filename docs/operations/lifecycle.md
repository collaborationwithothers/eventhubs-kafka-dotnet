# Infrastructure lifecycle and cluster smoke

The workflows run Terraform and Kubernetes commands through portable lifecycle scripts. The cluster is reused; each run destroys its ephemeral layer and stops AKS when the dispatch ends. The first smoke run proves only the infrastructure path.

This is C2 of the [approved redesign](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6383206899), implementing the [checked C2 plan, revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/158#issuecomment-6385938476). Lifecycle ownership is defined by [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates) and [AGENTS.md](../../AGENTS.md#hard-safety-rules). The command bounds below are implementation policy choices from that plan, not measured capacities.

## Before live use

C1 must merge its cluster root and private input migration before gate 1. Compare its actual variable declarations and outputs with the fixed interfaces below. Hari applies the persistent layer and prepares the external backend and exact cluster identifier. An incompatible C1 interface needs an authority decision; C2 does not change C1's files.

The one-time entry prerequisite under [factory.md's #138 entry rule](../agents/factory.md#item-lifecycle) is satisfied by [Hari's portal confirmation and workflow evidence](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6386104951), [adopted for C2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/158#issuecomment-6386105123). Children need not re-establish that entry prerequisite. C2's earlier source-based verification is [recorded on the issue](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/158#issuecomment-6386058874). Later gate environments remain subject to their own cleanup rules. Gate 1 runs only after C1 and C2 merge, private inputs are updated, persistent apply is complete and the protected environment approves the dispatch.

Until C6 merges its replacement, **do not dispatch scenario.yml**. Its old Runner path is obsolete against the redesigned infrastructure. C2 does not claim that path implements dispatch-end AKS stopping. The replacement sweeper must be present before the first smoke apply.

## Tools and private inputs

The scripts use `/bin/bash` with Bash 3.2-compatible syntax and Python 3.10 or later. On macOS, install the pinned tools locally and use Hari's existing Azure CLI login. The Linux x86_64 installer supports `install-tools.sh DEST live` and `install-tools.sh DEST ci`; it rejects other platforms. Workflows use environment-bound OIDC and the unchanged renewable serialized Azure CLI wrapper. No lifecycle function logs in or chooses another identity.

The [installer](../../infra/scripts/lifecycle/install-tools.sh) pins Terraform 1.16.6, Helm 4.3.1, kubectl 1.36.5 and kubelogin 0.2.21 for live commands. CI additionally pins actionlint 1.7.12, ShellCheck 0.11.0, kubeconform 0.8.0, TFLint 0.65.0 and PyYAML 6.0.3. The existing azurerm TFLint plugin remains 0.32.0. CI's PyYAML wheel requires CPython 3.12 on Ubuntu 24.04. The plan's [integrity table](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/158#issuecomment-6385938476) records each exact upstream URL and digest source. Each artifact download is limited to 120 seconds and 128 MiB, checked before extraction or execution, with no automatic retry. A missing tool, wrong version or integrity failure prevents provisioning.

| Variable | Value |
|---|---|
| `PARCHWELL_CLUSTER_BACKEND_CONFIG` | Absolute path to the cluster backend HCL file |
| `PARCHWELL_CLUSTER_TFVARS_PATH` | Absolute path to the complete cluster `.tfvars.json` file |
| `PARCHWELL_EPHEMERAL_BACKEND_CONFIG` | Absolute path to the ephemeral backend HCL file |
| `PARCHWELL_EPHEMERAL_TFVARS_PATH` | Absolute path to the complete ephemeral `.tfvars.json` file |
| `PARCHWELL_CLUSTER_RESOURCE_ID` | Exact expected AKS ARM resource ID, supplied privately from C1's reviewed configuration |
| `PARCHWELL_PRIVATE_DIRECTORY` | Private working directory for inputs, plans, outputs, separate Terraform data directories and kubeconfig |
| `PARCHWELL_ARTIFACT_DIRECTORY` | Separate directory for public pod evidence only |
| `PARCHWELL_RUN_ID` | DNS label, at most 29 characters |
| `KUBECONFIG` | `PARCHWELL_PRIVATE_DIRECTORY/kubeconfig` for subsequent Kubernetes commands |

Private files are mode 0600 under a directory created with `umask 077`. Keep them outside the repository and outside the public artifact directory. Terraform JSON output can expose sensitive values ([Terraform output documentation](https://developer.hashicorp.com/terraform/cli/commands/output)); it is never echoed or uploaded.

Backend files are HCL assignments with exactly one literal key assignment:

```hcl
# Synthetic format example; substitute Hari's existing external backend values.
resource_group_name  = "synthetic-backend-group"
storage_account_name = "syntheticbackend"
container_name       = "synthetic-state"
key                  = "cluster.tfstate"
use_azuread_auth      = true
```

Use a different `key` for ephemeral state. Neither key may equal `persistent_backend_config.key` in the approved private JSON tfvars. A missing, duplicated or nonliteral backend key is rejected. Files are parsed as data; shell code and HCL expressions are not evaluated by the lifecycle validator.

Both tfvars files must be complete JSON objects accepted by their merged Terraform root. In particular, provide the complete `persistent_backend_config` object and retain the ephemeral root's required `run_id` and `expires_at` values for destroy. Planning overrides only `run_id` with `PARCHWELL_RUN_ID`; destroy does not depend on a saved plan. Do not treat this synthetic excerpt as a complete deployment configuration:

```json
{
  "persistent_backend_config": {
    "key": "persistent.tfstate"
  }
}
```

On a first dispatch, the privately supplied cluster resource ID permits start-before-apply and stop-after-partial-apply without a prior Terraform output. Successful cluster apply must return `aks.value` and `workload_identity.value`; `aks.value.id` must equal that supplied ID. Ephemeral output must contain `eventhubs.value`, `blob.value` and `private_endpoint_ips.value`. C1's real output remains a live prerequisite, not a synthetic-fixture result.

## Commands

Run commands from any directory through the repository's `infra/scripts/lifecycle/lifecycle.sh`. Ordinary Terraform output streams directly to the terminal or Actions log. `command.py SECONDS -- COMMAND ARG...` prints the shell-quoted command, preserves its exit status and returns 124 on its monotonic deadline. It terminates the process group, waits up to 5 seconds and kills remaining children. With `--stdout-file PATH --max-bytes N`, it writes privately, stops before byte N+1, marks incomplete output and returns 125 on overflow.

| Subcommand | Operation and policy bound |
|---|---|
| `init-cluster`, `init-ephemeral` | Reconfigure only that root's backend and separate `TF_DATA_DIR`; 300 seconds |
| `plan-cluster`, `plan-ephemeral` | Save a private plan, with 120-second lock timeout; 600 seconds |
| `apply-cluster`, `apply-ephemeral` | Apply that saved plan once; 1800 seconds; private JSON output capped at 4 MiB |
| `destroy-ephemeral` | Initialize independently, destroy once within 2400 seconds, then require a successful empty state list |
| `start-cluster` | Exact-ID lookup, absence handling, conservative cooldown and bounded asynchronous start |
| `stop-cluster` | Exact-ID lookup, at most one stop submission and bounded readback to Stopped |
| `credentials` | Non-admin AKS kubeconfig, then `kubelogin convert-kubeconfig -l azurecli` |
| `reset-namespace` | Delete, observe deletion and recreate `parchwell`; 360 seconds total |
| `install-smoke` | Install the smoke chart as `parchwell-smoke` with Job `RUN_ID-smoke` |
| `wait-job JOB DEADLINE_SECONDS` | Poll Job JSON; Failed wins over Complete; deadline plus 60 seconds |
| `collect-job JOB` | Collect bounded pod JSON/logs, record every gap and validate the smoke marker |
| `uninstall-smoke` | Remove the release; an absent release is tolerated, other errors are retained |

No command selects persistent state or destroys the cluster root. Apply has no automatic retry. Destroy requires only ephemeral backend/tfvars and its private working directory; it does not require a completed apply, saved plan, cluster output, cost reservation or generation record. Failed init or failed state read is a cleanup failure, not empty state.

AKS start/stop follow the [Microsoft start/stop guidance](https://learn.microsoft.com/azure/aks/start-stop-cluster#start-an-aks-cluster). Only a positively identified ResourceNotFound response counts as absence. Start waits 1800 seconds after observing fully Stopped/Succeeded, then permits at most seven submissions at least 300 seconds apart, only while still Stopped, within 3600 seconds and 120 reads. It never resubmits while Starting. Stop permits at most one submission, 60 reads and 1800 seconds. Incomplete readback fails and is visible; the next sweep may retry. These chosen bounds do not guarantee Azure completes an operation within them.

Namespace deletion is asynchronous ([Kubernetes namespaces](https://kubernetes.io/docs/tasks/administer-cluster/namespaces/)). A terminating namespace is never reused. Job waiting checks either terminal condition from [Job status](https://kubernetes.io/docs/concepts/workloads/controllers/job/); it does not combine two AND conditions. Polling and sleeps consume the same monotonic deadline.

Collection saves up to four pods and four named init/ordinary containers per pod, sorted by name, within 180 seconds and 24 MiB. Pod JSON is capped at 4 MiB and each log at 1 MiB. Requests use `--timestamps=false` and ask for one extra log byte to detect truncation. Failed reads, omissions and truncation are explicit gaps in `collection-status.json`; independent remaining logs are still attempted. Gate 1 requires one smoke pod, its ordinary container named `smoke`, and the complete bytes `parchwell-cluster-smoke-ok` followed by exactly one LF. Prefixes, timestamps, extra whitespace and partial logs fail the predicate.

## Manual sequence for Hari

Prepare all private variables above and locally installed pinned prerequisites first. Any temporary storage-network access is Hari's manual prerequisite and is not scripted. The following sequence uses the same commands as the workflow; cleanup commands remain independent even after an earlier failure.

```bash
#!/bin/bash
set -euo pipefail
umask 077
LIFECYCLE=/absolute/path/to/eventhubs-kafka-dotnet/infra/scripts/lifecycle/lifecycle.sh
export KUBECONFIG="$PARCHWELL_PRIVATE_DIRECTORY/kubeconfig"
cleanup() {
  original=$?
  trap - EXIT
  failed=0
  bash "$LIFECYCLE" uninstall-smoke || failed=1
  bash "$LIFECYCLE" destroy-ephemeral || failed=1
  bash "$LIFECYCLE" stop-cluster || failed=1
  if [ "$original" -ne 0 ]; then exit "$original"; fi
  exit "$failed"
}
trap cleanup EXIT
bash "$LIFECYCLE" init-cluster
bash "$LIFECYCLE" start-cluster
bash "$LIFECYCLE" plan-cluster
bash "$LIFECYCLE" apply-cluster
bash "$LIFECYCLE" init-ephemeral
bash "$LIFECYCLE" plan-ephemeral
bash "$LIFECYCLE" apply-ephemeral
bash "$LIFECYCLE" credentials
bash "$LIFECYCLE" reset-namespace
bash "$LIFECYCLE" install-smoke
wait_status=0
bash "$LIFECYCLE" wait-job "$PARCHWELL_RUN_ID-smoke" 120 || wait_status=$?
bash "$LIFECYCLE" collect-job "$PARCHWELL_RUN_ID-smoke"
exit "$wait_status"
```

Manual recovery uses `bash "$LIFECYCLE" destroy-ephemeral` and `bash "$LIFECYCLE" stop-cluster` independently. Neither needs the preceding install or apply to have succeeded. A partial collection or failed cleanup never becomes a passing gate.

## Actions and recovery

`cluster-smoke.yml` is dispatch-only on main, runs on `vars.PARCHWELL_RUNNER_LABEL` in `parchwell-scenarios`, and requires contents:read/id-token:write. It materializes secrets `PARCHWELL_CLUSTER_BACKEND`, `PARCHWELL_CLUSTER_TFVARS`, `PARCHWELL_EPHEMERAL_BACKEND` and `PARCHWELL_EPHEMERAL_TFVARS`. `PARCHWELL_CLUSTER_RESOURCE_ID` and existing Azure client/tenant/subscription settings are environment variables. Its run ID is `smoke-${GITHUB_RUN_ID}-${GITHUB_RUN_ATTEMPT}`. The sweeper uses `parchwell-sweeper`, only the two ephemeral input secrets and the exact AKS ID, plus actions:read for dispatch history.

Both workflows share `parchwell-ephemeral`, `queue: max`, `cancel-in-progress: false` ([GitHub concurrency](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency)). Smoke has a 300-minute job bound and the sweeper a 120-minute bound. Separate always-running collection, upload, uninstall, destroy, stop and reporting steps preserve cleanup after failures. Upload is limited to the separate public pod-evidence directory, 5 minutes and 90-day retention, with no retries. Plans, Terraform output/state, kubeconfig and private input files are excluded. Cleanup outcomes are in Actions logs and the job summary.

Gate 1 passes only when the Job completes, complete pod evidence and exact marker are available, upload succeeds, release removal succeeds, ephemeral state is empty and AKS is observed Stopped. No scenario result is published. After the prerequisites above, dispatch through REST with `POST repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/workflows/cluster-smoke.yml/dispatches` and body `{"ref":"main"}`. The parent allows at most three gate attempts; record each URL, source SHA, failed step, artifacts and cleanup outcome on #158. After failure three, hand off before another money-spending attempt.

The sweeper keeps its existing 15-minute schedule. It never starts AKS, applies Terraform or creates namespaces. [dispatch-state.py](../../infra/scripts/lifecycle/dispatch-state.py) reads both scenario and smoke histories: at most three pages of 100 runs per workflow, 20 completed runs inspected, three job pages per latest attempt, 60 seconds/4 MiB per response and 600 seconds overall. It defers both actions when active-state coverage is unknown. A queued run is future work; in_progress, waiting, requested, pending or unknown status defers destructive work.

While idle, ephemeral cleanup is immediate. Stopping additionally requires a proven latest Job completion at least 3600 seconds old. Job `completed_at` is an approximation for dispatch end; `updated_at` only selects old-created reruns for inspection and never supplies an end time ([GitHub runs](https://docs.github.com/en/rest/actions/workflow-runs), [jobs](https://docs.github.com/en/rest/actions/workflow-jobs)). No completed jobs, missing history, future timestamps, exhausted pagination or unresolved latest completion defer stopping, with the reason logged. An idle run with unknown end time can still destroy ephemeral infrastructure. Destruction failure does not suppress eligible stopping.

[Cancellation](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-cancellation) can interrupt teardown; the sweeper is the recovery path, not a guarantee. GitHub may disable scheduled workflows in an inactive public repository after 60 days ([schedule documentation](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#schedule)). Check that recovery remains enabled before live use.

## Offline acceptance and authored receipt

Source baseline: `e7ebd3dd7508b8735820f45704304ea0801606dd`. Initial validation below was executed on the authored C2 working tree on 2026-10-24, before its first cold review. Later disposition and decision sections retain that history and state the current status. No live smoke, C1 output or Azure cleanup outcome for the new design is claimed.

Commands used `/tmp/c2-tools` for verified binaries and `/tmp/c2-tools/venv/bin` for the pinned PyYAML environment. The tested versions were Terraform 1.16.6, actionlint 1.7.12, ShellCheck 0.11.0, kubeconform 0.8.0, TFLint 0.65.0 and PyYAML 6.0.3. A separately verified kubelogin 0.2.21 binary reported its version through `git hash: v0.2.21/<commit>`; the installer validates that actual format. No Azure command was used for acceptance.

| Command | Observed result |
|---|---|
| `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` | Passed: 40 tests, no skips |
| `bash infra/scripts/lifecycle/install-tools.sh /tmp/c2-tools ci` | Incomplete: Terraform verified, then exact pinned Helm URL returned HTTP 403; no alternate source substituted |
| `PARCHWELL_TOOLS_DIRECTORY=/tmp/c2-tools bash infra/scripts/lifecycle/check-offline.sh` | Failed: missing Helm; before that failure, structural workflow and root test-path checks passed |
| `actionlint -ignore '^unexpected key "queue" for "concurrency" section\. expected one of "cancel-in-progress", "group"$' .github/workflows/cluster-smoke.yml .github/workflows/sweeper.yml` | Passed with ShellCheck on PATH and only the prescribed exception |
| `actionlint .github/workflows/dotnet-verification.yml` | Passed |
| `actionlint .github/workflows/dotnet-verification.yml .github/workflows/scenario.yml .github/workflows/docs-advisory.yml` | Failed on unchanged scenario.yml line 559: `needs['full-C0']` is not recognized by actionlint's lower-case object key; no extra diagnostic suppression added |
| `shellcheck infra/scripts/lifecycle/*.sh` | Passed |
| `terraform fmt -check -recursive infra tests/terraform` | Passed |
| `npm run check:docs` | Exit 0, 36 advisory warnings; lifecycle.md has no issues |
| `cmp .github/workflows/sweeper.yml docs/operations/workflows/sweeper.yml.txt` | Passed |
| `git diff --check` and `git diff --cached --check` | Passed |
| `git diff --exit-code origin/main -- .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt` | Passed; both existing scenario files unchanged |
| `./scripts/verify-dotnet.sh` | Failed before build: local SDK 10.0.402 absent; required existing CI job remains pending |

For each existing root, `ROOT=persistent` and `ROOT=ephemeral`, commands were individually bounded by `command.py 600 --`:

```text
terraform -chdir=infra/ROOT init -backend=false -input=false -lockfile=readonly
terraform -chdir=infra/ROOT validate
tflint --chdir=infra/ROOT --config=/workspace/c2-158/.tflint.hcl --init
tflint --chdir=infra/ROOT --config=/workspace/c2-158/.tflint.hcl
terraform -chdir=infra/ROOT test -test-directory=tests -no-color
```

Both backend-disabled initializations and validations passed. Persistent mock tests reported 12 passed, zero failed; ephemeral reported 15 passed, zero failed. Both TFLint initializations failed because signature metadata at `https://tuf-repo-cdn.sigstore.dev/15.root.json` returned Forbidden. Both subsequent lint commands failed because the verified azurerm plugin was unavailable; signature verification was not bypassed. No cluster root exists at this baseline, so cluster acceptance is an unverified C1 dependency.

The 40-case cap is exhausted: five command-runner cases, seven dispatch-history cases, 20 lifecycle scenarios and eight tool/structural cases. These include the exact marker and Job terminal inputs, a full provisioning/partial-apply/independent-cleanup scenario, bounded collection failures, rerun-aware history and integrity rejection. Additional executable-stub coverage remains absent for missing private paths, malformed backend literals or run IDs, successful ephemeral output validation, unsupported AKS states/stop timeout, nonempty state-list rejection and pod-JSON overflow. Collector/upload/uninstall/destroy failure selection is inspected through workflow conditions and required status aggregation, but has no separate Actions execution simulator. The implementations retain those checks; the cap does not make their unexecuted branches verified. Under the plan's cap-exhaustion action, record these gaps and continue to review without claiming complete acceptance.

Documentation diagnostics were enumerated by script. The first 20 warnings were inspected for relevance and are missing links in unchanged historical source excerpts under `docs/operations/evidence/issue-114/raw/review-10-fixes/`; no C2 warning was reported. The remaining 16 warnings were not dispositioned after the inspection cap. No unrelated warning fixes were made.

Outstanding acceptance is strict rendering of both charts, full `check-offline.sh`, verified TFLint, and the unchanged .NET CI job including discovery and passing results for both migrated test classes. The unchanged scenario actionlint diagnostic needs an authority-compatible resolution before the full checker can pass. Cold review, protected merge, C1 interface validation and live gate 1 remain later steps. No generated output, downloaded tool, private input or test report is committed.

The existing .NET verification job remains unchanged. Only the obsolete sweeper assertions in `WorkflowAuthenticationContractTests` and `WorkflowBudgetContractTests` migrate to the lifecycle contract; scenario assertions, login/session blocks, offline authentication tests and Runner production code remain unchanged. These planned assertion replacements require protected review under AGENTS.md.

### Cold-review round 1 disposition

The [first cold review](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/158#issuecomment-6386166079) reviewed source commit `7d134746098aaf0553300e7c30b4edf348a86f8d` and reported two P1 findings. Both are valid and remain unresolved. This disposition updates the authored receipt only; it does not claim a successful fix round or authorize a normal branch push.

1. **Required actionlint check cannot pass.** Reproduced on that source with `PATH=/tmp/c2-tools:$PATH actionlint .github/workflows/scenario.yml`: exit 1 at line 559, where `needs['full-C0'].result` is rejected against actionlint's inferred `full-c0` property. The [checked plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/158#issuecomment-6385938476), "Workflow files" and "CI and tool installation", requires scenario.yml to remain unchanged, actionlint to check every workflow, and no extra suppression. These constraints cannot all pass on the current source. A temporary copy outside the repository, changing only that expression to `needs.full-C0.result`, passed `PATH=/tmp/c2-tools:$PATH actionlint /tmp/c2-fix1-authority/scenario-expression-proposal.yml` with exit 0. This is parser evidence for a proposed amendment, not a committed fix or live workflow validation. The initial temporary-copy invocation referenced a nonexistent actionlint configuration and exited 3; the successful invocation used no configuration argument. Any approved workflow edit must also update its committed mirror under AGENTS.md hard safety rule 8. No workflow, checker exception or trigger was changed in this disposition round.
2. **Planned sweeper assertion replacements trigger P1.** The branch removes the sweeper's old `SweepStep` equality assertion and its exact Runner wrapper/diagnostic targets while replacing the tested workflow. The new lifecycle assertions preserve authentication, independent cleanup and mirror coverage, but do not undo those removals. The checked plan's "Sweeper contract-test migration" requires the replacement and expressly retains AGENTS.md "Code Review Rules". [Testing](../engineering/dotnet.md#testing) says, "Never loosen, skip or delete an assertion in the same pull request as the code it tests. If an assertion is wrong, fix it in its own pull request with the reason." AGENTS.md "Before opening a pull request" rule 8's retained-plan exception does not list assertion migrations. Therefore this finding is not answered `Not valid:` and is not counted as an exempt P1. A separate assertion-change pull request with its reason, or an explicit decision resolving this particular plan/rule conflict, is needed before the combined change can proceed. Protected review and Hari's merge remain required; they do not by themselves clear this pre-push finding.

The implementing session needs Hari's decision on an amendment covering the scenario expression and on the assertion-migration conflict. [Factory "Item lifecycle" step 3](../agents/factory.md#item-lifecycle) states, "A plan revised after it is approved is an amendment and needs Hari's approval." No plan amendment, separate pull request, relaxed check or governance change is adopted by this receipt. The approved entry decision remains satisfied. Helm rendering, full check-offline, verified TFLint, .NET acceptance, C1 interfaces and live gate 1 remain unestablished as recorded above; unchanged acceptance commands were not rerun to manufacture a new result.

Disposition validation on 2026-10-24: `npm run check:docs` initially reported four new link warnings in this addition. Correcting the two relative link targets removed them; the final run exits 0 with the same 36 previously recorded advisory warnings and none for lifecycle.md. `git diff --check` passes. This is documentation validation only, not the separately orchestrated next cold review.

### Decision implementation after cold-review round 2

[Hari's decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/158#issuecomment-6386279393) amends the checked plan for the two findings carried by [round 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/158#issuecomment-6386235805). This section supersedes their unresolved disposition above. The source is `ec9b5862d5f5913aa5f66484c95ce64d06ec0754` plus the expression-only workflow and mirror edits in this commit; validation ran on 2026-10-24.

1. **actionlint finding fixed.** Only `needs['full-C0'].result` changes to `needs.full-C0.result` in scenario.yml and its byte-identical mirror. GitHub's current [context expression access documentation](https://docs.github.com/en/actions/reference/workflows-and-actions/contexts#available-contexts), read 2026-10-24, documents index and property-dereference syntax as access methods and explicitly permits a property name starting with a letter or underscore and containing alphanumeric characters, hyphens or underscores. Applying that rule, `full-C0` is a legal property name and both expressions select the same job's `result`. This is a documented-syntax conclusion, not a live dispatch result. Pinned actionlint 1.7.12 now accepts the actual workflow without an extra suppression. A byte comparison against the source commit confirms exactly this replacement in each file and no other scenario change.
2. **Assertion-removal P1 retained under the decision.** The old sweeper SweepStep equality and Runner wrapper-target assertions remain removed because the same change removes their tested behaviour. The replacement authentication, independent-cleanup and mirror assertions remain. This is still a P1 under Code Review Rules, not a `Not valid:` finding or a clean-review claim. The pull request description must list it and link Hari's decision; Hari merges. That decision excludes only these accepted removals from the named churn and review-round limits. No other assertion change is authorized.

The approved entry prerequisite remains satisfied. No trigger, checker suppression, package, unrelated assertion or lifecycle implementation changes in this decision round. The following reruns use the previously recorded pinned tools; all output remains outside git.

| Command | Observed result after the decision |
|---|---|
| `PYTHONDONTWRITEBYTECODE=1 PATH=/tmp/c2-tools:/tmp/c2-tools/venv/bin:$PATH python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` | Passed: 40 tests, no skips; no additional cases |
| `PATH=/tmp/c2-tools:$PATH actionlint .github/workflows/dotnet-verification.yml .github/workflows/scenario.yml .github/workflows/docs-advisory.yml` | Passed with ShellCheck on PATH and no ignored diagnostic |
| `PATH=/tmp/c2-tools:$PATH actionlint -ignore '^unexpected key "queue" for "concurrency" section\. expected one of "cancel-in-progress", "group"$' .github/workflows/cluster-smoke.yml .github/workflows/sweeper.yml` | Passed with only the approved queue exception |
| `cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt` and the corresponding sweeper comparison | Passed |
| `bash infra/scripts/lifecycle/install-tools.sh /tmp/c2-tools ci` | Failed: Terraform verified; the exact pinned Helm URL still returns HTTP 403 |
| `PARCHWELL_TOOLS_DIRECTORY=/tmp/c2-tools bash infra/scripts/lifecycle/check-offline.sh` | Failed: Helm unavailable; existing-root test-path and structural checks passed before the missing-tool check |
| `PATH=/tmp/c2-tools:$PATH python3 infra/scripts/lifecycle/command.py 600 -- tflint --chdir=infra/persistent --config=/workspace/c2-158/.tflint.hcl --init` | Failed: signature metadata at `https://tuf-repo-cdn.sigstore.dev/15.root.json` remains Forbidden; no signature bypass |

The exact .NET SDK 10.0.402 was installed under `/tmp/c2-158-dotnet-10.0.402` after checking the tarball SHA-512 against Microsoft's [10.0 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json). The unfiltered Runner project run against the existing Release build passed: 1171 tests, no failures or skips. Its xUnit XML contains three passing tests in `WorkflowAuthenticationContractTests` and six in `WorkflowBudgetContractTests`. These counts were computed from the XML; both migrated classes are discovered and pass. This is local evidence, not an executed CI job.

The .NET invocations from `/workspace/c2-158` were:

```sh
env PATH="/tmp/c2-158-dotnet-10.0.402:$PATH" DOTNET_ROOT=/tmp/c2-158-dotnet-10.0.402 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 timeout 600 ./scripts/verify-dotnet.sh > /tmp/c2-158-verify-dotnet-acceptance.log 2>&1
env PATH="/tmp/c2-158-dotnet-10.0.402:$PATH" DOTNET_ROOT=/tmp/c2-158-dotnet-10.0.402 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 PARCHWELL_REPOSITORY_ROOT=/workspace/c2-158 timeout 600 dotnet test --project tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj --configuration Release --no-build --minimum-expected-tests 1 --results-directory /tmp/c2-158-runner-acceptance -- --report-xunit-xml --report-xunit-xml-filename results.xml > /tmp/c2-158-runner-acceptance/test.log 2>&1
```

Full local `verify-dotnet.sh` also exited 0 within that invocation bound. Restore, Release build, Kafka documentation inventory, effective project settings, report checks and the warning-as-error control all passed. The six xUnit reports contain 4204 passed tests with no failures, skips, not-run tests or errors: Architecture 8, Kafka 306, LoadGen 312, Runner 1171, Scenarios 96 and Verifier 2311. The full-script Runner report independently includes the same three authentication and six budget contract passes. Enrichment remains an empty scaffold, with no unit-test coverage claimed. The existing containerized tests completed and cleaned up; their experiment output is not a live Event Hubs result. Temporary bytecode generated by those tests was removed; no generated evidence is committed. The required CI execution is still pending.

Final documentation validation exits 0 with the same 36 historical advisory warnings and none for lifecycle.md; `git diff --check` passes. The previously recorded warning dispositions and inspection cap remain unchanged.

Strict chart rendering, full check-offline and verified TFLint remain incomplete. The environment's enforced HTTP allowlist does not include `get.helm.sh` or `tuf-repo-cdn.sigstore.dev`; acceptance needs an environment that permits these official sources. No alternative download route or verification bypass was used. The unchanged Terraform results and the exhausted test-case/warning-inspection caps above remain the evidence for work not changed in this round. The next cold review is separately orchestrated; C1 interfaces, protected merge and live gate 1 remain later prerequisites.

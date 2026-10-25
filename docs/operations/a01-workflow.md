# A01 scenario workflow

The A01 scenario workflow runs each phase as Kubernetes Jobs on the reused AKS cluster. All five repeat slots run before the verifier judge writes the phase candidate. The workflow retrieves that candidate and the pod evidence before it destroys the ephemeral environment.

The [approved C6 plan, Revision 5](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/163#issuecomment-6388311497) and its [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/163#issuecomment-6398356463) define this implementation. [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates) owns lifecycle and judgement boundaries. The [chart contract](chart-contract.md), [verifier contract](verifier-contract.md) and [lifecycle commands](lifecycle.md#commands) own their respective interfaces.

## Jobs and approvals

The installed [scenario workflow](../../.github/workflows/scenario.yml) and its [committed copy](workflows/scenario.yml.txt) are byte-identical. It accepts manual dispatch on `main` only. Its phase jobs are `probe` with configuration C0, `full-C0` with C0, and `full-C3` with C3. The probe publishes worker images and records their registry digests. Full phases read back those immutable digests without publishing again. Full-C0 requires a successful probe whose checked candidate allows advancement; the full-C3 workload additionally requires full-C0 success. These conditions implement the approved plan's advancement check, rather than judging message evidence in the workflow.

Each job uses `parchwell-scenarios`, retaining the three-job design in [parent A14](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6383206899). Full-C3 runs even when an earlier phase fails or is skipped, so it can stop AKS without running its workload. Hari must approve that stop-only job. If he rejects it, recovery follows [AGENTS.md hard safety rule 6](../../AGENTS.md#hard-safety-rules) and [lifecycle actions and recovery](lifecycle.md#actions-and-recovery).

While full-C0 or full-C3 waits for approval the run is not completed, so the sweeper defers and AKS keeps running (`infra/scripts/lifecycle/dispatch-state.py`, non-completed runs); approve or reject promptly. This follows the [dispatch activity check](../../infra/scripts/lifecycle/dispatch-state.py).

## Private inputs

Prepare the environment's `PARCHWELL_CLUSTER_BACKEND`, `PARCHWELL_CLUSTER_TFVARS`, `PARCHWELL_EPHEMERAL_BACKEND`, `PARCHWELL_EPHEMERAL_TFVARS`, and `PARCHWELL_PERSISTENT_OUTPUTS` secrets. Supply `PARCHWELL_CLUSTER_RESOURCE_ID` as the exact expected cluster identifier. The backend and tfvars contracts remain in [lifecycle tools and private inputs](lifecycle.md#tools-and-private-inputs).

`PARCHWELL_PERSISTENT_OUTPUTS` must contain the `terraform output -json` text of `infra/persistent`, including `acr` and `application_insights_connection_string`. Hari supplies that output after applying the persistent root; an agent session does not apply it. Preparation validates the private inputs before image or infrastructure commands. Private inputs, Terraform outputs, Helm values and rendered manifests stay outside the candidate and pod artifact directories.

Inferred prerequisite: the workflow identity can read the ephemeral `verifier` container, as the former Runner's [WorkerBlobDownloader](../../src/Parchwell.Runner/Run/Execution/WorkerBlobDownloader.cs) did under the same identity. Offline fixtures do not establish that permission; gate 2 tests it live.

## Commands

Run [phase.sh](../../infra/scripts/phase/phase.sh) from the checked-out repository. It delegates infrastructure operations to the unchanged [lifecycle scripts](lifecycle.md#commands). The approved C6 plan owns the argument counts and input checks.

| Subcommand | Operation |
| --- | --- |
| `prepare PHASE` | Validate and materialise private inputs for `probe`, `C0` or `C3`; create disjoint private, candidate and artifact directories. |
| `prepare-stop` | Prepare a separate private directory for dispatch-end stopping without reading workload secrets. |
| `allow-c3` | Export whether probe and full-C0 results allow the C3 workload. |
| `publish-images` | Publish the probe's full-source-commit worker images and read back registry digests. |
| `use-images LOADGEN VERIFIER` | Validate and read back the probe's digests for a full phase. |
| `check-dns` | Require Event Hubs and blob host answers to equal their private endpoint addresses. |
| `resolve HOST` | Internal command that returns distinct resolved addresses as JSON. |
| `run-repeats` | Attempt each slot's start-line, loadgen and verify modes in order. |
| `judge` | Run the phase judge after the repeat loop, unless a safety stop prevents another mode. |
| `candidate` | Retrieve the judge's candidate with its ETag, check the workflow fields and retain its exact bytes. |
| `delete-workload` | Delete this run's labelled Jobs. |
| `report` | Summarise required step outcomes, mode outcomes and candidate status in the log and job summary. |

## Bounds

These are configured limits from [C6 plan section 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/163#issuecomment-6388311497), not measured durations. Worker deadlines come from the [chart contract](chart-contract.md#names-environment-and-deadlines); infrastructure bounds come from [lifecycle commands](lifecycle.md#commands).

| Step | Bound, seconds |
| --- | ---: |
| Per mode: render 120, apply 60, wait deadline+60, collect 180, delete and confirm 150 | start-line 720, loadgen 1710, verify 1320, judge 660 |
| One repeat slot | 3750 |
| Five slots | 18750 |
| Candidate retrieval, three attempts of show 60 and download 60, delays 1 and 2 | 363 |
| Cluster init, plan, apply, output (C2) | 2760 |
| AKS start (C2) | 3600 |
| Ephemeral init, plan, apply, output (C2) | 2760 |
| DNS 30, credentials 120, namespace reset 360 | 510 |
| Images: probe login 60 plus two publish 600 and readback 60; full phases two readbacks of 60 | 1380; 120 |
| Two uploads 300 each, workload delete 60, destroy 2760 (C2) | 3420 |
| Dispatch-end stop (C2, full-C3 only) | 1800 |
| Worst-case job totals | probe 34203, full-C0 32943, full-C3 34743 |

Each figure is the command's own internal budget; the outer helper limits are deadline+90 s for a wait and 210 s for collection, so a timed-out command can exceed its figure by up to 35 s. The helper's termination allowance is implemented in [command.py](../../infra/scripts/lifecycle/command.py).

Each phase job has `timeout-minutes: 360`. The sums above are not required to fit that limit, under [decision 6388539631 point 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/163#issuecomment-6388539631). Reaching it makes the dispatch fail; the sweeper is the recovery path when teardown cannot finish.

## Failure and cancellation

The approved plan's mode execution rules require one render, apply and wait per attempted mode, with no replacement. A worker or judge Job ending `Failed` is recorded and does not itself fail the phase job. A collection gap is also recorded without failing that job. The [verifier contract](verifier-contract.md) owns their effect on judgement.

An unknown wait or failed apply triggers Job deletion and a check that its pods are absent before another mode starts. If absence cannot be confirmed, the safety stop is persisted in `phase-status.json`. Later repeat modes and the separate judge process record `not_attempted`. This implements enforced check 6 and the [adopted safety-stop correction](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/163#issuecomment-6398349648).

Input, image, lifecycle, DNS, render, apply, unknown-wait, collection-error, safety-stop, candidate, upload, workload-delete and destroy failures fail the phase job under the plan's job result rule. Workload deletion and ephemeral destroy use `always()` with their own prerequisites; destroy needs preparation and the Azure session, rather than a successful apply. Full-C3 owns the dispatch's single stop attempt. Cancellation can prevent cleanup from completing; recovery follows [lifecycle actions and recovery](lifecycle.md#actions-and-recovery), not a claim that `always()` guarantees teardown.

## Artifacts

For phase `PHASE`, the workflow uploads `a01-PHASE-candidate-RUN_ID-RUN_ATTEMPT` and `a01-PHASE-pods-RUN_ID-RUN_ATTEMPT`. `PHASE` is `probe`, `C0` or `C3`; the suffixes are GitHub's run ID and attempt. The configured retention is 90 days, under [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates). The [parent C6 entry](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6383206899) records the access basis: GitHub documents artifact downloads for signed-in users with read access; access by any signed-in GitHub user is an inference for this public repository.

The candidate artifact contains only the judge's unchanged `candidate.json`. The pod artifact contains pod JSON, logs, per-Job `collection-status.json` and `phase-status.json`. Collection bounds remain those in [lifecycle commands](lifecycle.md#commands). No private inputs, Terraform outputs, values or rendered manifests are uploaded.

Pod JSON is uploaded unchanged, including `Telemetry__ConnectionString`, `AZURE_CLIENT_ID` and `AZURE_TENANT_ID`, under [decision 6398209659 point 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/163#issuecomment-6398209659). That decision covers Actions artifacts only. [AGENTS.md hard safety rule 7](../../AGENTS.md#hard-safety-rules) still bars those values from committed evidence; a later evidence pack redacts them or links to the artifact. Experiment data in ephemeral Blob storage is removed at teardown under ADR-020.

## Gate 2

[C6 plan section 11](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/163#issuecomment-6388311497) owns the gate protocol and dispatch cap. After Hari merges this protected change and the private inputs are current, dispatch `scenario.yml` on `main` only. There is no one-repeat input: the probe attempts the full repeat protocol. Hari may reject full-C0 to end workload execution after the probe, then approve full-C3's stop-only job.

Gate 2 passes only when one dispatch records all of the following:

- The probe's first slot has `complete` start-line, loadgen and verify mode records.
- The candidate step succeeds, checking the live judge's bytes against enforced check 8, and candidate repeat 1 is `complete_valid`.
- Destroy prints `ephemeral_state_empty`.
- Full-C3's stop step prints `aks_stopped`.

Record the dispatch number, run link, source SHA, every step outcome and artifact names on [issue 163](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/163). The plan permits at most three dispatches; after a failed third, hand off to Hari. Gate 2 is the real-writer integration under [decision 6398209659 point 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/163#issuecomment-6398209659). It does not establish an A01 verdict.

## Validation receipt

The implementing session checked the authored C6 diff against source commit `0496bf7d774bc1fdc97afc8476060e8ebf5cabca` on 2026-10-25. These are local observations, not live gate evidence. The final head's CI run and cold-review disposition are recorded in the pull request under AGENTS.md rule 8.

Pinned tools were installed with `bash infra/scripts/lifecycle/install-tools.sh /tmp/issue163/tools ci`. Acceptance used `PATH=/tmp/issue163/tools:/tmp/issue163/tools/venv/bin:$PATH` and `PARCHWELL_TOOLS_DIRECTORY=/tmp/issue163/tools`.

- `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py' -v`: passed, 34 tests, zero skipped, including real Helm renders.
- The same bounded discovery command with `-s infra/scripts/tests`: passed, 74 tests, zero skipped. Before the bridge, pinned-tool discovery passed 40 tests. The initial unpinned attempt could not find Helm or kubeconform; installing the declared tools resolved it.
- `bash infra/scripts/lifecycle/check-offline.sh`: passed, including actionlint, shellcheck and chart acceptance.
- `cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt` and `git diff --check`: passed.
- `python3 infra/scripts/lifecycle/command.py 600 -- ./scripts/verify-dotnet.sh`, using SDK 10.0.402: passed, including locked restore, solution build, Kafka XML inventory, architecture and unit suites, and the intentional warning-as-error control. All reported tests passed with no skips.
- `dotnet test --project tests/Parchwell.Runner.Tests/Parchwell.Runner.Tests.csproj --configuration Release --minimum-expected-tests 1`: passed, 1163 tests, zero skipped.
- `python3 -m unittest discover -s scripts/tests -p test_azure_cli_session.py -v`: passed, 20 tests.
- `npm run check:docs`: passed; the new workflow document has no warnings. Warnings in unchanged documents remain advisory.

No live dispatch has run for this change. Hari must merge the protected workflow before gate 2 can run on `main`.

The 2026-10-25 continuation against source commit `3dec9c990f98b5ecaec6cbf8153225431a3d7a49` implements Hari's session instruction, "fix it using the token also fix the review comment". The Terraform validation step in `dotnet-verification.yml` now receives the built-in `${{ github.token }}` as `GITHUB_TOKEN`, following [TFLint v0.65.0's documented authentication](https://github.com/terraform-linters/tflint/blob/502b6a3849534fe5518177d105a8db616b09b74d/docs/user-guide/plugins.md#avoiding-rate-limiting). A PyYAML structural comparison confirmed that this step's environment is the only workflow change; permissions and triggers are unchanged. The bounded `check-offline.sh` checks and `git diff --check` passed again. The review's test-retirement P1 remains accepted under the approved plan and requires Hari's merge; replacement coverage does not remove that escalation. Token-authenticated plugin installation is verified by the next PR CI run, not by a local credential fixture.

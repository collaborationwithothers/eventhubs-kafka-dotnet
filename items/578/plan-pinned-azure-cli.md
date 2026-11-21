# Pinned Azure CLI install: contract-grade plan for issue #578

Revision: r1. Base commit: `main` at 8c39cba25c912a94699b79002b7afcd5582f3dab.

Every workflow job that runs the archive or the Azure CLI session wrapper will install Azure CLI 2.92.0 from one checksum-pinned Microsoft package before it records the real CLI path. The package already carries `azure-storage-blob` 12.29.0b1 in the CLI's own Python, so the archive preflight passes again without any change to it. One pull request covers the work; three open questions about cleanup risk and runner assumptions need Hari's answer before approval.

## Goal and authority

- Authority: the issue body of #578 (created 2026-11-21T15:58:37Z, `updated_at` 2026-11-21T15:58:38Z) and Hari's decision comment [7065282362](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/578#issuecomment-7065282362), which chose the pinned install over moving the archive pin or using the image CLI unpinned. No other comment exists on the issue at the last refresh. There is no item state file (`items/578/state.md` does not exist on `main`), no parent issue and no item plan; the issue body is the specification.
- Goal: image updates of the GitHub-hosted runner can no longer change the Azure CLI the archive and the session wrapper use. The archive preflight (`infra/scripts/archive/smoke.py` `preflight`, lines 259 to 278 on the base commit) keeps failing closed on any other version.
- Candidate selection (docs/agents/factory.md "Item lifecycle" step 3): no split is approved on the issue and no plan comment exists, so the candidate is the whole issue as one pull request, named **pinned-azure-cli**.

## Scope

One pull request, branch `claude/578-pinned-azure-cli`.

Files allowed to change:

1. `infra/scripts/lifecycle/install-azure-cli.sh` (new).
2. `infra/scripts/tests/test_install_azure_cli.py` (new).
3. `.github/workflows/archive-smoke.yml` and `docs/operations/workflows/archive-smoke.yml.txt`.
4. `.github/workflows/b10-abrupt-trial.yml` and `docs/operations/workflows/b10-abrupt-trial.yml.txt`.
5. `.github/workflows/scenario.yml` and `docs/operations/workflows/scenario.yml.txt`.
6. `.github/workflows/sweeper.yml` and `docs/operations/workflows/sweeper.yml.txt`.
7. `.github/workflows/cluster-smoke.yml` (it has no committed copy under `docs/operations/workflows/` on the base commit).
8. `infra/scripts/phase/tests/test_workflow.py`, only `B10AbruptWorkflowTests` comment and `BASE_WORKFLOWS` values.
9. `scripts/tests/test_azure_cli_session.py`, only the `SETUP` constant and `setup_fixture` / `test_19_literal_install_failure_blocks_later_workloads` as R7 sets.
10. `docs/operations/archive-smoke.md`, line 15 only.
11. `items/578/state.md` (new; the implementing session writes it under docs/agents/factory.md "Item state file").

Paths that must not change: `infra/scripts/archive/` (preflight and all archive logic), `infra/scripts/lifecycle/install-tools.sh`, `scripts/azure-cli-session/az`, every `scenarios/` and `results/` file, every other workflow, and in the five changed workflows everything except the one line R3 adds to each `azure_session` step.

## External contracts

C1. The installer is `infra/scripts/lifecycle/install-azure-cli.sh`. It takes no arguments. Exit status 0 means the pinned CLI is installed and verified; any failure exits 1 with one line on stderr starting `install-azure-cli: `. Any argument exits 2 with `usage: install-azure-cli.sh` on stderr and does nothing else.

C2. The artifact, pinned as literals in the installer:

| Field | Value |
|---|---|
| URL | `https://packages.microsoft.com/repos/azure-cli/pool/main/a/azure-cli/azure-cli_2.92.0-1~noble_amd64.deb` |
| SHA-256 | `83c994db3fdbb70368c257f575e0d181f0c8ffed7ebcd761596c8f14af678346` |
| Maximum download size | 134217728 bytes (128 MiB, the `LIMIT` of `install-tools.sh` line 33) |
| Package version | `2.92.0-1~noble` |

C3. Post-install checks, all required:

- `command -v az` resolves to exactly `/usr/bin/az`.
- `/usr/bin/az version --output json` exits 0 and its JSON object has `"azure-cli": "2.92.0"`.
- `/opt/az/bin/python3 -c 'import importlib.metadata; print(importlib.metadata.version("azure-storage-blob"))'` exits 0 and prints exactly `12.29.0b1`.

C4. Workflow line. In each of the 11 `azure_session` steps listed in R3, this exact line is inserted directly after `set -euo pipefail` and before `umask 077`:

```bash
bash "$GITHUB_WORKSPACE/infra/scripts/lifecycle/install-azure-cli.sh"
```

No step is added, removed, renamed or re-ordered. Step ids, `if` expressions, `REQUIRED_STEPS`, `WORKLOAD_STEPS`, triggers, permissions, environments, concurrency, secrets and the `azure/login` steps stay byte-identical.

C5. Literal failure messages (each printed after the `install-azure-cli: ` prefix):

| Case | Message |
|---|---|
| Not Linux x86_64, or `/etc/os-release` `VERSION_CODENAME` is not `noble` | `requires Ubuntu 24.04 (noble) on x86_64` |
| Download bytes do not match C2 SHA-256, or exceed the size limit | `SHA-256 mismatch or oversized artifact: azure-cli_2.92.0-1~noble_amd64.deb` |
| `command -v az` is not `/usr/bin/az` after install | `az resolves to <path>, not /usr/bin/az` (`<path>` is the resolved path, or `nothing` when none) |
| Reported `azure-cli` version is not `2.92.0` | `az did not report pinned version 2.92.0` |
| Storage library version is not `12.29.0b1` | `azure-storage-blob is not 12.29.0b1` |
| A download, install or check command exits non-zero or times out | the existing `command.py` failure output, then exit 1 |

## Requirements

R1. WHEN the installer runs on Linux x86_64 whose `/etc/os-release` has `VERSION_CODENAME=noble`, THEN it downloads the C2 URL to a private temporary directory through `infra/scripts/lifecycle/command.py` with `curl --fail --silent --show-error --location --retry 3 --max-time 120 --max-filesize 134217728`, under a 300 second bound, and verifies the C2 SHA-256 before any `sudo` call.
 - WHEN the platform check fails, THEN it exits 1 with the C5 platform message and makes no download and no `sudo` call.
 - WHEN the SHA-256 or size check fails, THEN it exits 1 with the C5 checksum message and makes no `sudo` call.

R2. WHEN the checksum passes, THEN it sets `umask 022` and runs `sudo -n apt-get install --yes --allow-downgrades --no-install-recommends <absolute path of the verified file>` through `command.py` under a 300 second bound. `-n` makes `sudo` fail instead of prompting. `--allow-downgrades` is needed because the image may carry a newer CLI (2.93.0 on 2026-11-21).
 - WHEN that command fails, THEN the installer exits 1 and runs no C3 check.
 - WHEN it succeeds, THEN the installer runs the C3 checks in the order listed, and the first failing check exits 1 with its C5 message. WHEN all pass, it prints `Installed pinned Azure CLI 2.92.0 at /usr/bin/az` on stdout and exits 0.

R3. Each of these 11 `azure_session` steps gets the C4 line: `archive-smoke.yml` jobs `smoke` and `stop`; `b10-abrupt-trial.yml` jobs `trial` and `stop`; `cluster-smoke.yml` job `smoke`; `scenario.yml` jobs `probe`, `full-C0`, `full-C3`, `batch` and `batch-stop`; `sweeper.yml` job `sweep`. On the base commit these are the only jobs that write `PARCHWELL_REAL_AZ`, and their 11 step bodies are identical (counted by script on the base commit). They stay identical after the change, so the existing equality checks in `test_workflow.py` (lines 179 to 187 and 917 to 924) and the literal check in `test_azure_cli_session.py` `test_19` keep holding.
 - WHEN the installer fails, THEN `azure_session` fails, `PARCHWELL_REAL_AZ` is never written, and every step gated on `steps.azure_session.outcome == 'success'` is skipped, as for any other `azure_session` failure today.
 - WHEN it succeeds, THEN the unchanged `real_az="$(command -v az)"` resolves to `/usr/bin/az`, which C3 has just verified as 2.92.0, so `PARCHWELL_REAL_AZ` points at the installed CLI (issue requirement 3).

R4. Each changed workflow that has a committed copy is copied byte for byte: `archive-smoke.yml`, `b10-abrupt-trial.yml`, `scenario.yml`, `sweeper.yml`. `cluster-smoke.yml` has no copy and none is added.

R5. `B10AbruptWorkflowTests.BASE_WORKFLOWS` values for `.github/workflows/cluster-smoke.yml`, `.github/workflows/scenario.yml` and `.github/workflows/sweeper.yml` become the SHA-256 of the committed bytes, computed by script after the workflow edits are final. The other three values stay. The comment line `# and the coordinated C5 campaign of issue #494.` becomes `# the coordinated C5 campaign of issue #494, and the pinned Azure CLI install of issue #578.` The assertion in `test_mirror_and_all_preexisting_workflows_unchanged` is not changed.

R6. `docs/operations/archive-smoke.md` line 15 is replaced with exactly:

> The scenario, archive-smoke, B10 abrupt-trial, cluster-smoke and sweeper workflows install Azure CLI 2.92.0 from Microsoft's checksum-pinned Ubuntu 24.04 package with `infra/scripts/lifecycle/install-azure-cli.sh`, before they record the real CLI for the session wrapper (issue #578). That package carries the CLI's own Python environment with `azure-storage-blob` 12.29.0b1. The probe still verifies both versions and the interpreter path, and fails closed on a mismatch rather than installing or upgrading them. The pins follow the [CLI release](https://github.com/Azure/azure-cli/releases/tag/azure-cli-2.92.0), its [Linux dependency](https://github.com/Azure/azure-cli/blob/azure-cli-2.92.0/src/azure-cli/requirements.py3.Linux.txt#L91) and [Python-location formatter](https://github.com/Azure/azure-cli/blob/azure-cli-2.92.0/src/azure-cli-core/azure/cli/core/util.py#L442).

R7. `scripts/tests/test_azure_cli_session.py`: `SETUP` gains the C4 line as its second line, so `test_19`'s literal comparison matches the workflow. `setup_fixture` writes an executable stub at `<workspace>/infra/scripts/lifecycle/install-azure-cli.sh` that appends one line to `<base>/cli-install-calls` and exits 0. A new failure value `cli-install` makes that stub exit 7. `test_19`'s failure tuple gains `'cli-install'`, with the existing assertions applying to it unchanged.

## Invariants and safety rules

- Preflight is unchanged and still fails closed on any other version (issue requirement 3).
- AGENTS.md hard safety rules 8 and 9: no trigger, permission, identity or network exposure change. Only the C4 line enters each workflow. All five workflows reach Azure, so the pull request is Hari's to merge.
- Hard safety rule 7: the installer and tests hold no secret, tenant or subscription ID.
- The installer never runs `sudo` before the checksum passes, and never installs from an unverified file.
- Cleanup risk: in the stop, batch-stop and sweeper jobs, the cluster stop and ephemeral destroy are gated on `azure_session` success. A failed install there means no stop or destroy in that job. This is listed under "Open questions for Hari" Q1 and is not decided by this plan.

## Test map

All tests are offline. No test downloads, runs `sudo`, or runs the real package. The installer is bash with an embedded Python body in the style of `install-tools.sh`, so tests load it with the `embedded()` pattern of `infra/scripts/tests/test_tools_offline.py` lines 16 to 20 and patch the command runner, or run the script with stub `curl`, `sudo` and `az` executables on `PATH`. Function names are the implementer's.

| Req | Test (file `infra/scripts/tests/test_install_azure_cli.py` unless named) | Input | Literal expectation |
|---|---|---|---|
| C2 | `test_pinned_artifact_literals` | the installer's constants | URL, SHA-256 and limit exactly as C2 |
| R1 | `test_non_noble_or_non_x86_fails_before_download` | `VERSION_CODENAME=jammy` with x86_64; `noble` with `aarch64`; `noble` with system `Darwin` | each: exit 1, stderr `install-azure-cli: requires Ubuntu 24.04 (noble) on x86_64`, no download call, no `sudo` call |
| R1 | `test_curl_arguments_and_bound` | patched runner records the download call | argv contains `--fail`, `--retry`, `3`, `--max-time`, `120`, `--max-filesize`, `134217728` and the C2 URL; bound 300 |
| R1 | `test_checksum_mismatch_never_reaches_sudo` | download writes `b'corrupted synthetic package'` | exit 1, stderr `install-azure-cli: SHA-256 mismatch or oversized artifact: azure-cli_2.92.0-1~noble_amd64.deb`, no `sudo` call |
| R1 | `test_oversized_download_never_reaches_sudo` | download file size 134217729 bytes | same message as above, no `sudo` call |
| R2 | `test_install_command_and_umask` | synthetic bytes, and the expected digest patched to their SHA-256 | the one `sudo` argv is exactly `sudo -n apt-get install --yes --allow-downgrades --no-install-recommends <path>` with an absolute `<path>` to the verified file; umask during the call is `0o022` |
| R2 | `test_install_failure_skips_checks` | stub `sudo` exits 100 | exit 1, no `az` call |
| C3 | `test_wrong_az_path_fails` | `command -v az` gives `/usr/local/bin/az`; then no `az` at all | stderr `install-azure-cli: az resolves to /usr/local/bin/az, not /usr/bin/az`; then `... az resolves to nothing, not /usr/bin/az` |
| C3 | `test_wrong_cli_version_fails` | version JSON `{"azure-cli": "2.93.0"}`; then `{"azure-cli": "2.92.00"}`; then non-JSON `2.92.0` | each: stderr `install-azure-cli: az did not report pinned version 2.92.0` |
| C3 | `test_wrong_storage_version_fails` | storage output `12.29.0`; then `12.29.0b1 ` with a trailing space before the newline; then `12.29.0b2` | each: stderr `install-azure-cli: azure-storage-blob is not 12.29.0b1` |
| R2 | `test_success_prints_one_line` | all stubs good: version JSON `{"azure-cli": "2.92.0", "azure-cli-core": "2.92.0"}`, storage `12.29.0b1\n` | exit 0, stdout ends with `Installed pinned Azure CLI 2.92.0 at /usr/bin/az` |
| C1 | `test_any_argument_is_usage_error` | one argument `x` | exit 2, stderr `usage: install-azure-cli.sh`, no download |
| R3 | `test_every_session_step_installs_first` | the 5 workflow files | exactly 11 `azure_session` steps; each `run` has `set -euo pipefail` as line 1 and the C4 line as line 2; all 11 bodies equal |
| R3, R7 | `scripts/tests/test_azure_cli_session.py` `test_19_literal_install_failure_blocks_later_workloads`, subtest `cli-install` | stub installer exits 7 | `azure_session` outcome `failure`; no `export` or `destroy` permitted; `GITHUB_ENV` and `GITHUB_PATH` empty or absent; no `original-calls` and no `wrapper` marker |
| R7 | `scripts/tests/test_azure_cli_session.py` `test_20_successful_setup_fallback_status_matrix` (unchanged) | stub installer exits 0 | its existing assertions, including `PARCHWELL_REAL_AZ` equal to `<base>/original/az` |
| R4 | existing mirror checks: `test_workflow.py` lines 116 and 343 (scenario), 988 (B10); `test_archive_smoke_workflow.py` line 22; `check-offline.sh` `check_workflows` | changed files | byte equality passes unchanged |
| R5 | existing `test_mirror_and_all_preexisting_workflows_unchanged` | updated `BASE_WORKFLOWS` | passes, assertion unchanged |

Standing fixture rule (plan-pr "Plan rules" rule 2, modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2): when an existing test's fixture is rejected by a new check of this plan, the implementer does exactly one of (a) change the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; or (b) where invalid input is the test's purpose, keep the input and change the expected values to exactly what this plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each such change with the check that forces it; the cold review and Codex review check each listed change. The R7 stub is a planned case (a) change, forced by the C4 line in `SETUP`.

## Deployment assumptions

Each item has no offline test. Each is listed under Q2 until Hari accepts it by name.

- D1. The `gh-linux` runner can reach `packages.microsoft.com` over HTTPS. Evidence: none. On the base commit no workflow step contacts that host; `install-tools.sh` reaches `releases.hashicorp.com`, `get.helm.sh`, `dl.k8s.io` and `github.com` only (lines 22 to 31). The runner is in group `consultwithcloud-azure` with Azure private networking (`infra/persistent/variables.tf` line 20 `runner_vnet_id`); its egress rules are not in this repository. The planning session reached the host from its own cloud container, not from the runner.
- D2. The runner gives passwordless, non-interactive `sudo` to the job user. Evidence: none from this repository's runs; no workflow step uses `sudo` on the base commit.
- D3. The runner is Ubuntu 24.04 x86_64 with the package's dependencies already installed (`libbz2-1.0, libc6 (>= 2.38), libffi8 (>= 3.4), libgcc-s1 (>= 4.2), libssl3t64 (>= 3.0.0), libuuid1 (>= 2.20.1), zlib1g (>= 1:1.2.0)`, from the repository index). Evidence: the probe job log of run [44896017344](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/44896017344) (job 136821799721, read 2026-11-21 through the GitHub REST logs route) shows `Image: ubuntu-24.04`, `Version: 20261116.376.1`, and the image CLI at `/usr/bin/az`. That image's own `azure-cli` 2.93.0 was installed and running, which is inferred, not checked, to mean these dependencies are present. Taken from a recorded log of a finished run; not a live connection, so idle or under load does not apply.
- D4. Azure CLI 2.92.0 works with the `~/.azure` state the image CLI (2.93.0) writes earlier in the same job: the `az version` and `az aks --help` calls of `install-tools.sh` `live` (lines 110 to 113) and `azure/login`. Evidence: none. The session wrapper logs in again with `PARCHWELL_REAL_AZ` before its first command (`scripts/azure-cli-session/az` lines 352 to 360), which reduces but does not remove the dependency. Until 2026-11-19 the same jobs ran 2.92.0 end to end (C03 run 44562323424, per the issue body).

## Acceptance commands

Run from the repository root on the pull request head:

```bash
python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'
python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_workflow.py'
python3 -m unittest discover -s scripts/tests -p test_azure_cli_session.py -v
bash infra/scripts/lifecycle/check-offline.sh
for f in archive-smoke b10-abrupt-trial scenario sweeper; do cmp ".github/workflows/$f.yml" "docs/operations/workflows/$f.yml.txt"; done
npm run check:docs
```

Each must exit 0. `check-offline.sh` must show `shellcheck -x` passing on the new script. The pull request's "Lifecycle verification" job of `dotnet-verification.yml` runs the first, third and fourth on the final head. The PR description records the final-head run link and states that `azure-storage-blob` 12.29.0b1 comes with the pinned CLI (issue requirement 2; Sources S3 and S4).

Post-merge acceptance is in "Deferred work".

## Deferred work

- Post-merge live check. Evidence: the issue's second acceptance item. On the verdict path: yes; it blocks the archive of the C10 and C01 batch runs of #510. Deferral is required because the issue puts any dispatch out of scope and agents dispatch only against `main` (hard safety rule 8). Responsible: the control session that next dispatches a scenario. Stage: the first scenario dispatch after merge. Workload bound: that one dispatch, already planned; no extra dispatch is authorized by this plan. Completion evidence: the archive receipt shows `archive_complete` true, or a gap whose boundary is not `archive transport version`, plus the job log line `Installed pinned Azure CLI 2.92.0 at /usr/bin/az`; the control session records both on #578. That dispatch also tests D1 to D4 live. Exhaustion action: if the install fails, the control session records the job log on #578 and hands off to Hari; it does not retry by changing the pins.

## Sources

Tools used: the Microsoft Learn MCP server (`microsoft_docs_fetch`), `curl` for the Microsoft package index and package, `dpkg-deb -x` for static inspection of the package (not executed), and the GitHub REST API for job metadata and logs. All read 2026-11-21.

- S1. Microsoft Learn, [Install the Azure CLI on Linux, apt](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-linux?pivots=apt), "Install specific version": documents installing a fixed version as `sudo apt-get install azure-cli=${AZ_VER}-1~${AZ_DIST}` from the repository `https://packages.microsoft.com/repos/azure-cli/`, lists Ubuntu 24.04 (Noble Numbat) as tested, and names `packages.microsoft.com` as the host a proxy must allow. Confirmed: the package name, version format, repository and host. Inferred: that fetching the same file directly by its pool path and installing it with `apt-get install <file>` is equivalent. The page documents the repository route, not the direct fetch; the checksum pin is this plan's addition, as the issue's "checksum-pinned artifact" allows.
- S2. Microsoft package index `https://packages.microsoft.com/repos/azure-cli/dists/noble/main/binary-amd64/Packages`, entry `Version: 2.92.0-1~noble`: `SHA256: 83c994db3fdbb70368c257f575e0d181f0c8ffed7ebcd761596c8f14af678346`, `Size: 57174364`, `Filename: pool/main/a/azure-cli/azure-cli_2.92.0-1~noble_amd64.deb`, and the `Depends` line quoted in D3. The downloaded file's SHA-256, computed by `sha256sum`, equals the index value.
- S3. The package's contents, by `dpkg-deb -x`: `opt/az/lib/python3.14/site-packages/azure_storage_blob-12.29.0b1.dist-info` and `azure_cli-2.92.0.dist-info`; `usr/bin/az` runs `../../opt/az/bin/python3 -Im azure.cli`. So `azure-storage-blob` 12.29.0b1 comes with the pinned CLI and needs no separate pin (issue requirement 2). Running the extracted CLI was not done.
- S4. [azure-cli 2.92.0 `requirements.py3.Linux.txt` line 91](https://github.com/Azure/azure-cli/blob/azure-cli-2.92.0/src/azure-cli/requirements.py3.Linux.txt#L91): `azure-storage-blob==12.29.0b1`.
- S5. Run 44896017344 probe job 136821799721 log: runner label `gh-linux`, group `consultwithcloud-azure`, `Image: ubuntu-24.04`, `Version: 20261116.376.1`, `"azure-cli": "2.93.0"` at 2026-11-21T10:48:56Z, `PARCHWELL_REAL_AZ: /usr/bin/az`.
- S6. Repository files on the base commit: `infra/scripts/archive/smoke.py` lines 259 to 278; `infra/scripts/lifecycle/install-tools.sh` lines 20 to 63 and 110 to 113; `scripts/azure-cli-session/az` lines 294 to 360; `infra/scripts/phase/tests/test_workflow.py` lines 179 to 187, 848 to 860, 917 to 924 and 987 to 990; `scripts/tests/test_azure_cli_session.py` lines 31 to 41 and 1085 to 1162; `docs/operations/archive-smoke.md` line 15; the five workflows' `azure_session` steps and the steps gated on them.

## Plan-check questions

- (a) Steps whose effort scales with a number unknown at planning time: none. The workflow set is fixed at 5 files and 11 steps, counted by script on the base commit.
- (b) Where the plan is stricter than its parent: there is no parent plan. Against the issue body, the plan adds the C3 post-install checks and the `noble` / x86_64 platform check. The issue asks for a verifiable install; these make a wrong install fail in the installing step, with a named message, instead of later in preflight or not at all in jobs that run no preflight.
- (c) Checks that depend on code, data or behaviour not on `main` or in the pull request: the Microsoft package (S2, S3), checked against its recorded index entry and static contents; and the runner, D1 to D4, which no source settles, listed as deployment assumptions.

## Open questions for Hari

- Q1. Cleanup jobs. Issue requirement 1 covers every job that runs the session wrapper. That includes the cleanup-only jobs `archive-smoke.yml` `stop`, `b10-abrupt-trial.yml` `stop`, `scenario.yml` `batch-stop` and `sweeper.yml` `sweep`, and the in-job stop of `scenario.yml` `full-C3`. Their stop and destroy steps are gated on `azure_session` success. With this plan, a failed download or install there skips the AKS stop and, in the sweeper, the leftover-ephemeral destroy (hard safety rule 6), until a later run or Hari's manual action succeeds. Today those jobs depend only on GitHub and Azure; this adds `packages.microsoft.com`. Options: (A) follow requirement 1 as written and accept this risk by name, with `curl --retry 3` as the only mitigation; this is the plan as drafted and the recommendation, because a single pinned CLI everywhere keeps the checked behaviour and the stop commands ran on 2.92.0 until 2026-11-19. (B) Amend requirement 1 to exclude the cleanup-only jobs; they then keep the image CLI, and their `azure_session` bodies diverge from the others, which needs changes to the equality tests in `test_workflow.py` lines 179 to 187 and 924. (C) Install in cleanup jobs but fall back to the image CLI on failure; this contradicts requirement 3 and is not recommended.
- Q2. Accept deployment assumptions D1 (runner reaches `packages.microsoft.com`), D2 (passwordless `sudo`), D3 (Ubuntu 24.04 x86_64 with the package's dependencies) and D4 (2.92.0 works with `~/.azure` state written by the image CLI earlier in the job) by name. None can be tested before merge without a dispatch, which the issue puts out of scope. The first post-merge dispatch tests all four ("Deferred work").
- Q3. docs/agents/factory.md "Item lifecycle" step 1 makes a plan that changes live infrastructure unapprovable until a live smoke run of its design exists. This plan changes runner tooling in workflows, not Azure resources, and the issue puts any dispatch out of scope and makes the first post-merge dispatch its live check. The plan reads the rule as not applying. Confirm, or name the smoke run required.

## Size estimate

Estimate, by the planning session on 2026-11-21, from the base-commit file counts above; not measured: 14 files; about 21 added workflow lines (11 in workflows and 10 in their copies, counted by script), about 100 to 140 lines for the new installer, 150 to 220 lines for its new test file, about 15 lines in `test_azure_cli_session.py`, 4 lines in `test_workflow.py`, 1 line in `docs/operations/archive-smoke.md`, and about 15 lines for `items/578/state.md`. Size is reported, not enforced.

## Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract (C1 to C5), a requirement (R1 to R7) or the scope, including any change to preflight or the pinned versions. If the C2 URL no longer serves bytes matching the C2 SHA-256, it stops and hands off to Hari with what it found (issue requirement 2); it does not change the pin.

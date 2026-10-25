# Worker image base and smoke receipt

Both worker images now build on a digest-pinned `mcr.microsoft.com/dotnet/aspnet` 10.0.13 image instead of `mcr.microsoft.com/dotnet/runtime`. The workers need the `Microsoft.AspNetCore.App` framework since #166, and without it both exited 150 at start ([issue #179](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/179)). A new script, `scripts/verify-worker-images.sh`, starts each image in the `.NET verification` CI job so the failure cannot recur silently.

## Scope and authority

The work follows the [implementation-grade plan, Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/179#issuecomment-6396335296), approved under standing approval by its [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/179#issuecomment-6396686909), with the decisions [6396422349](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/179#issuecomment-6396422349) and [6396568695](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/179#issuecomment-6396568695). The protected paths touched require Hari's merge. Program output stays in the session workspace; the results below are authored observations of that output.

The labels below are the plan's. C1 to C6 are its changes, A0 to A10 its acceptance steps (plan section "Acceptance"), and R1 to R3 its classes of the first A3 result (plan section "Execution order", step 3). Sequence H is the plan's hosted path: a first head with only the script (C2) and the CI step (C3) must fail in CI, then the code head adds the base image change (C1) and the documentation.

## Sequence and A3 classification

Sequence H was used. SDK 10.0.402 was installed by A0 under `$HOME/.dotnet-10.0.402`. Local steps ran on `main` at `2dffbc3540316312fd42bbb94a8e7dd4c67f58e6` with the changes staged; the code head SHA is recorded under "Code head A9" by the receipt-only commit.

A3 ran `./scripts/verify-worker-images.sh` before C1, with C2 and C3 applied. It exited 1 with the stderr line `Docker daemon is not reachable.`, and no line containing `image smoke failed:`. That is R2, which selects sequence H. A4 was therefore not run as an acceptance step, with that stderr line as the reason.

Extra evidence outside the plan's sequence: the session then started the container's own `dockerd` (Docker 29.9.2). A second run of the script before C1 exited 1 with `LoadGen image smoke failed: exit 150.`, `Framework: 'Microsoft.AspNetCore.App', version '10.0.0' (x64)` and `No frameworks were found.` A run after C1 exited 0 with `LoadGen image smoke: passed` and `Verifier image smoke: passed`. Neither run is A3 or A4.

## First head A3H

First head: `5fc42beb0b261e1a43ab8f9bbed1330d01046845`, holding C2 and C3 only. Its `.NET verification` run [40188645057](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40188645057) concluded `failure`. Job 121154698232 (`.NET verification`) concluded `failure` with failed steps exactly `["Verify worker images start"]`; the `Terraform verification` and `Lifecycle verification` jobs concluded `success`. In the job log, `grep -cF 'LoadGen image smoke failed: exit 150.'` printed `1` and `grep -cF 'Microsoft.AspNetCore.App'` printed `2`. The matched lines include `LoadGen image smoke failed: exit 150.` and `Framework: 'Microsoft.AspNetCore.App', version '10.0.0' (x64)`.

## Acceptance steps

| Step | Command | Exit | Observed result |
|---|---|---|---|
| A0 | `dotnet-install.sh --version 10.0.402 --install-dir "$HOME/.dotnet-10.0.402" --no-path`, then `dotnet --version` | 0 | `10.0.402` |
| A1 | `bash -n scripts/verify-worker-images.sh` and `git ls-files -s scripts/verify-worker-images.sh` | 0 | Mode `100755` |
| A1b | shellcheck 0.11.0 tarball checked with `sha256sum -c`, then `shellcheck -x scripts/verify-worker-images.sh` | 0 | `OK`; shellcheck printed nothing |
| A2 | MCR index re-read for `sha256:8af87eba...` with `sha256sum` and the linux/amd64 entry, 2026-10-25 | 0 | `8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca` then `sha256:f879b8a9cb3f6694b8b0c9fa530eb0c39d8920db09c05a7edbd6c4c6c104ae1b` |
| A3 | `./scripts/verify-worker-images.sh` before C1 | 1 | R2, as above |
| A4 | Not run | | Sequence H; A3 stderr `Docker daemon is not reachable.` |
| A5 | `grep -c` of the aspnet image reference in both csproj files | 0 | Each file `:1` |
| A6 | `git grep -n 'dotnet/runtime:' -- src` | 1 | No output |
| A7 | `./scripts/verify-dotnet.sh` | 0 | Ends `Verification passed. Per-project reports identify the tests executed.`; ran with the session Docker daemon, which the Docker-backed component tests need |
| A8 | `npm ci --ignore-scripts && npm run check:docs` | 0 | No issues in the three changed Markdown files; 38 advisory warnings, all in unchanged files |

An earlier A7 attempt, before the Docker daemon was started, failed only in Docker-backed tests (`DockerUnavailableException` or a null Docker endpoint).

## Cold review

- Round 1, on an earlier full diff that used sequence L after the second local run: one P1, that the plan selects sequence H from the first A3 result. Fixed by switching to sequence H.
- Round 2, first head: one P1, `npm run check:docs` not recorded before the review. Fixed by running it.
- Round 3, first head: no P0 or P1. Its two P2s, a script header citing a section not yet present and the CI description not listing the new step, are fixed in this code head by C5(a) and C5(b).

Later rounds are recorded in the pull request's self-review section.

## Code head A9

Pending: recorded by the receipt-only commit after A9 passes on the code head.

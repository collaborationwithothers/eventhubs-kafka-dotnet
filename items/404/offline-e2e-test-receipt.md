# Offline image test implementation receipt

This pull request implements the approved offline end-to-end image test for issue 404. It tests the real parent, self-killing child and successor against local Kafka, and adds the workflow Hari runs before a diagnostic trial. It authorizes no dispatch or trial.

## Execution card

- Deliverable and contracts: [Revision 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/66517e9a9f28dae97d93c37d007b7feef5e362f3/items/404/plan-offline-e2e-test.md), sections 2 to 8; exact-body SHA-256 `d83bc0d509fe3d4a6de118a32a565842132275d38b19cdea4ad37bcd8d7496d1` verified before implementation.
- Authorization: [plan pointer](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788002493), [Hari's approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788501730), and decisions [6787880185](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787880185), [6788215099](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788215099), [6788464379](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788464379) and their cited decisions, amended by [6788754429](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788754429). Their sources are read in full by the control session.
- Plan check: [6788423093](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788423093), on Revision 3, stands for Revision 4 under the approval. Decision 6788464379 resolves B1; N3 applies below.
- Branch: `codex/404-offline-e2e-test`; base `3a4ee0b140a209c2cdeae0002a77c5349a6aabc7`. Execution part 2 and classification are separate work.
- Mandatory gates: plan section 8's three image runs and three separate scratch mutation runs, full .NET verification with the variables unset, lifecycle checks, documentation checks, cold review, final-head CI, hosted Codex code and security reviews. Hari merges the protected workflow; no agent dispatches it.
- Earliest publication: after the prescribed local acceptance evidence, documentation check and applicable complete cold review. Publish then and run CI and hosted reviews together.
- Review budget: two comprehensive passes and two focused correction rounds under `docs/agents/pull-requests.md` rule 8. Exhaustion or unsafe focus means push `-wip` and hand off to Hari. No reviews have started.
- Failure and finding budgets: amendment 6788754429 allows this implementing session 20 verification-failure root causes and 20 review-finding root causes. Each exhaustion action is `push -wip and hand off to the next session`; a continuation session starts its own count. All subagents share this implementing session's counts. The cold-review budget and scope/contract stop rules still apply. A surviving prescribed mutation still triggers plan section 8's stop.
- Predicate map: not applicable; this is not verifier work. Plan section 6 maps the test's requirements and the three mutations.

## Verification commands

The exact commands and results below are recorded as they run. The approved fixed workload is three baseline image runs and one run of each of the three mutations. Amendment 6788754429 permits fixes within the frozen scope under the root-cause caps; it does not change the prescribed workload or the surviving-mutation stop.

The control session records the committed acceptance source as `offline_source_commit` before building; the image tag, test environment and observation all use that value. The build commands are:

```sh
offline_source_commit="$(git rev-parse HEAD)"
dotnet publish src/Parchwell.LoadGen/Parchwell.LoadGen.csproj --configuration Release -p:RestoreLockedMode=true -p:NuGetLockFilePath=packages.linux-x64.lock.json -r linux-x64 --self-contained false /t:PublishContainer "-p:ContainerImageTag=offline-$offline_source_commit" -p:ContainerArchiveOutputPath=/tmp/issue404-control/loadgen.tar
docker load --input /tmp/issue404-control/loadgen.tar
docker image inspect --format '{{.Id}}' "parchwell/loadgen:offline-$offline_source_commit"
dotnet build tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --configuration Release -p:RestoreLockedMode=true -m:1
```

Each baseline attempt uses a fresh numbered results directory and evidence file through `offline_run`. Three passing baseline runs are still required; the failed first setup attempt below does not supply deployment-assumption evidence. The targeted command and both report checks match the workflow:

```sh
PARCHWELL_B10_OFFLINE_IMAGE="parchwell/loadgen:offline-$offline_source_commit" \
PARCHWELL_B10_OFFLINE_SOURCE_COMMIT="$offline_source_commit" \
PARCHWELL_B10_OFFLINE_EVIDENCE="/tmp/issue404-control/baseline-$offline_run-evidence.txt" \
dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj \
  --configuration Release --no-build --minimum-expected-tests 1 \
  --results-directory "/tmp/issue404-control/baseline-$offline_run-results" -- \
  --filter-method 'Parchwell.LoadGen.Tests.Abrupt.B10AbruptOfflineImageTests.Given_BuiltImage_When_ParentChildAndSuccessorRunInContainers_Then_JournalExitAndResendsAreObserved' \
  --report-xunit-xml --report-xunit-xml-filename results.xml
dotnet msbuild scripts/dotnet-verification.proj -t:ValidateReport \
  "-p:Report=/tmp/issue404-control/baseline-$offline_run-results/results.xml" \
  -p:ExpectedAssembly=Parchwell.LoadGen.Tests -nologo
python3 - "/tmp/issue404-control/baseline-$offline_run-results/results.xml" <<'PYTHON'
import sys
import xml.etree.ElementTree as ET

tests = ET.parse(sys.argv[1]).getroot().findall('./assembly/collection/test')
if len(tests) != 1 or tests[0].get('result') != 'Pass':
    raise SystemExit('Expected exactly one passed offline image test.')
PYTHON
cat "/tmp/issue404-control/baseline-$offline_run-evidence.txt"
```

The existing runner's help confirms the filter and XML-report flags (`/tmp/issue404-control/runner-help.txt`). Local tool installation completed with exit 0; its log is `/tmp/issue404-control/install-tools.log`. These are setup checks, not image-run acceptance evidence. Each scratch mutation uses a separate worktree, rebuilt image, fresh report path and fresh evidence path; its exact mutation and commands are recorded with the result.

The remaining local gates are:

```sh
env -u PARCHWELL_B10_OFFLINE_IMAGE -u PARCHWELL_B10_OFFLINE_SOURCE_COMMIT -u PARCHWELL_B10_OFFLINE_EVIDENCE ./scripts/verify-dotnet.sh
PARCHWELL_TOOLS_DIRECTORY=/tmp/issue404-tools bash infra/scripts/lifecycle/check-offline.sh
npm run check:docs
```

## Implementation choices and gate evidence

Source locations below refer to baseline `3a4ee0b140a209c2cdeae0002a77c5349a6aabc7`.

The producer-factory override (`B10AbruptHost.cs` lines 212 to 213) replaces the production producer builder: the OAuth session is still created (line 209) but is not attached to the producer, and the `SetLogHandler` librdkafka log capture is not installed; neither bears on DA1, DA5 or DA6.

Existing control modes are unchanged except for the `hook-loaded.jsonl` append that plan section 4.4 requires in every mode. The test manages containers through the existing Testcontainers and Docker.DotNet APIs to preserve the ADR-020 process boundary. Private helper structure and directory ownership/cleanup follow the implementation choices allowed in plan sections 2.1, 4.2, 4.5 and R12.

Initial verification source: `35dfba2f24a27d01596564bb8bf40db6540c7afc`. Its built image ID is `sha256:da7fa5cb60972417598fdf10809ef890b0224429ddb3ee6a328b7a70e5107660`. The final locked Release test build above exited 0 with 0 warnings and 0 errors in 9.76 s (`/tmp/issue404-control/image-test-build-final.log`). The earlier parallel build failed with MSB4166 child-node exits (`/tmp/issue404-control/image-test-build.log`); the local build uses `-m:1` to avoid that infrastructure failure. No source contract changed for this adjustment.

Baseline attempt 1 at the initial verification source failed before the parent started. The ownership helper exited 1: `chown: cannot read directory '/owned/parent': Permission denied`, with the same failure for the successor and storage directories. The host temporary directories had mode 700 under the workspace umask; the helper's dropped capabilities could not traverse them. The saved diagnostics are in `/tmp/issue404-control/baseline-1-ownership-helper.log`; `/tmp/issue404-control/baseline-1.log` preserves the canceled runner session. The CLI exited 130 after SIGINT, the test host exited, and all containers, including the broker, ownership helper and Ryuk, were removed. No XML report or evidence line was produced. This is setup failure evidence, not DA1, DA5 or DA6 evidence. The in-scope correction sets directory and file modes explicitly before ownership transfer. The exited helper's readiness wait also failed to return before cancellation (V6); a race in Testcontainers' default running-state wait is the code agent's inference. Ownership and cleanup helpers use the existing Docker client's create/start/wait/remove APIs so a short-lived helper's exit is observed directly; parent, successor and broker retain Testcontainers. A corrected source freeze and three passing runs on fresh paths remain pending.

The helper correction initially failed to compile because its `GetContainerLogsAsync` call used an overload absent from the installed Docker.DotNet.Enhanced package. `/tmp/issue404-control/image-test-mode-fix-build.log` records 0 warnings and 2 CS1503 errors in 14.05 s, one root cause (V7). The call now uses the installed signature; its successful build and image acceptance are pending.

Mutation runs, the full verification script, lifecycle and documentation gates, specification comparison and complete review reports are pending. No passing image-run evidence is yet claimed.

## Failure and review root-cause ledgers

Amendment 6788754429 caps each ledger at 20 root causes per implementing session. The control session consolidates shared causes before recording the cumulative count. Cold-review pass counts are separate and cumulative across sessions.

| Verification root | Evidence and disposition |
|---|---|
| V1: ambiguous `Metadata` (CS0104) | Docker models collided with Confluent.Kafka; qualified `Confluent.Kafka.Metadata`. Evidence is the code agent's compiler transcript; no saved log exists. |
| V2: missing `PullPolicy` namespace (CS0103) | Imported the existing `DotNet.Testcontainers.Images` namespace. Evidence is the code agent's compiler transcript; no saved log exists. |
| V3: nullable `HostConfig` (CS8602) | Initialized `HostConfig` in both affected container-create modifiers. Evidence is the code agent's compiler transcript; no saved log exists. |
| V4: parallel MSBuild child-node failure (MSB4166) | `image-test-build.log`; serial `-m:1` build succeeds in `image-test-build-final.log`. No contract change. |
| V5: ownership helper cannot traverse host directories | Baseline attempt 1: mode 700 directories deny the helper traversal with dropped capabilities. Explicit directory and file modes before `chown` are the pending in-scope correction. The same audit found generated hook-output directories at 700 and the test DLL at 600; generated directories/files are normalized to 755/644 before any hook attempt. No hook failure was observed. |
| V6: exited helper readiness wait does not return | Baseline attempt 1 required cancellation after the ownership helper exited. Existing Docker client create/start/wait/remove APIs replace the helper readiness path; the default-wait race diagnosis remains inferred. |
| V7: unsupported container-log API overload (CS1503) | `image-test-mode-fix-build.log` records two argument errors from one call. The installed `GetContainerLogsAsync(id, parameters, cancellationToken)` signature is the correction; build pending. |

The control session consolidates V1 to V7 in `/tmp/issue404-control/verification-failure-ledger.json`: 7 verification roots, including both setup failures. The initial final build's 0 warnings and 0 errors verify V1 to V3; V7's corrected build is pending. No cold review has started, so the review-finding root count is 0 and completed cold-review counts are 0 comprehensive and 0 focused.

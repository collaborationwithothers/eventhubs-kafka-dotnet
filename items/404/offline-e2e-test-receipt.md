# Offline image test implementation receipt

This pull request implements the approved offline end-to-end image test for issue 404. It tests the real parent, self-killing child and successor against local Kafka, and adds the workflow Hari runs before a diagnostic trial. It authorizes no dispatch or trial.

## Execution card

- Deliverable and contracts: [Revision 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/66517e9a9f28dae97d93c37d007b7feef5e362f3/items/404/plan-offline-e2e-test.md), sections 2 to 8; exact-body SHA-256 `d83bc0d509fe3d4a6de118a32a565842132275d38b19cdea4ad37bcd8d7496d1` verified before implementation.
- Authorization: [plan pointer](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788002493), [Hari's approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788501730), and decisions [6787880185](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787880185), [6788215099](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788215099), [6788464379](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788464379) and their cited decisions, amended by [6788754429](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788754429). Their sources are read in full by the control session.
- Plan check: [6788423093](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788423093), on Revision 3, stands for Revision 4 under the approval. Decision 6788464379 resolves B1; N3 applies below.
- Models: the control session uses Codex; its exact model identifier is not exposed. Fresh-context implementation subagents inherit the Codex model with requested high effort.
- Branch: `codex/404-offline-e2e-test`; base `3a4ee0b140a209c2cdeae0002a77c5349a6aabc7`. Execution part 2 and classification are separate work.
- Mandatory gates: plan section 8's three image runs and three separate scratch mutation runs, full .NET verification with the variables unset, lifecycle checks, documentation checks, cold review, final-head CI, hosted Codex code and security reviews. Hari merges the protected workflow; no agent dispatches it.
- Earliest publication: after the prescribed local acceptance evidence, documentation check and applicable complete cold review. Publish then and run CI and hosted reviews together.
- Review budget: two comprehensive passes and two focused correction rounds under `docs/agents/pull-requests.md` rule 8. Exhaustion or unsafe focus means push `-wip` and hand off to Hari. At preparation of the first cold-review input, completed cold-review counts are 0 comprehensive and 0 focused.
- Failure and finding budgets: amendment 6788754429 allows this implementing session 20 verification-failure root causes and 20 review-finding root causes. Each exhaustion action is `push -wip and hand off to the next session`; a continuation session starts its own count. All subagents share this implementing session's counts. The cold-review budget and scope/contract stop rules still apply. A surviving prescribed mutation still triggers plan section 8's stop.
- Predicate map: not applicable; this is not verifier work. Plan section 6 maps the test's requirements and the three mutations.

## Verification commands

The exact commands and results below are recorded as they run. The approved fixed workload is three baseline image runs and one run of each of the three mutations. Amendment 6788754429 permits fixes within the frozen scope under the root-cause caps; it does not change the prescribed workload or the surviving-mutation stop.

The control session records the committed acceptance source as `offline_source_commit` before building; the image tag, test environment and observation all use that value. The build commands are:

```sh
offline_source_commit="$(git rev-parse HEAD)"
dotnet publish src/Parchwell.LoadGen/Parchwell.LoadGen.csproj --configuration Release -p:RestoreLockedMode=true -p:NuGetLockFilePath=packages.linux-x64.lock.json -r linux-x64 --self-contained false /t:PublishContainer "-p:ContainerImageTag=offline-$offline_source_commit" "-p:ContainerArchiveOutputPath=/tmp/issue404-control/loadgen-$offline_source_commit.tar"
docker load --input "/tmp/issue404-control/loadgen-$offline_source_commit.tar"
docker image inspect --format '{{.Id}}' "parchwell/loadgen:offline-$offline_source_commit"
dotnet build tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --configuration Release -p:RestoreLockedMode=true -m:1
```

Each baseline attempt uses a fresh numbered results directory and evidence file through `offline_run`. The three completed passing runs are recorded below; failed setup attempts supply no deployment-assumption evidence. The targeted command and both report checks match the workflow:

```sh
PARCHWELL_B10_OFFLINE_IMAGE="parchwell/loadgen:offline-$offline_source_commit" \
PARCHWELL_B10_OFFLINE_SOURCE_COMMIT="$offline_source_commit" \
PARCHWELL_B10_OFFLINE_EVIDENCE="/tmp/issue404-control/baseline-accepted-$offline_run-evidence.txt" \
dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj \
  --configuration Release --no-build --minimum-expected-tests 1 \
  --results-directory "/tmp/issue404-control/baseline-accepted-$offline_run-results" -- \
  --filter-method 'Parchwell.LoadGen.Tests.Abrupt.B10AbruptOfflineImageTests.Given_BuiltImage_When_ParentChildAndSuccessorRunInContainers_Then_JournalExitAndResendsAreObserved' \
  --report-xunit-xml --report-xunit-xml-filename results.xml
dotnet msbuild scripts/dotnet-verification.proj -t:ValidateReport \
  "-p:Report=/tmp/issue404-control/baseline-accepted-$offline_run-results/results.xml" \
  -p:ExpectedAssembly=Parchwell.LoadGen.Tests -nologo
python3 - "/tmp/issue404-control/baseline-accepted-$offline_run-results/results.xml" <<'PYTHON'
import sys
import xml.etree.ElementTree as ET

tests = ET.parse(sys.argv[1]).getroot().findall('./assembly/collection/test')
if len(tests) != 1 or tests[0].get('result') != 'Pass':
    raise SystemExit('Expected exactly one passed offline image test.')
PYTHON
cat "/tmp/issue404-control/baseline-accepted-$offline_run-evidence.txt"
```

The existing runner's help confirms the filter and XML-report flags (`/tmp/issue404-control/runner-help.txt`). Local tool installation completed with exit 0; its log is `/tmp/issue404-control/install-tools.log`. These are setup checks, not image-run acceptance evidence. Each scratch mutation uses a separate worktree, rebuilt image, fresh report path and fresh evidence path. The exact preparation and one-shot test commands are recorded below before the first mutation test.

The local gate commands are:

```sh
env -u PARCHWELL_B10_OFFLINE_IMAGE -u PARCHWELL_B10_OFFLINE_SOURCE_COMMIT -u PARCHWELL_B10_OFFLINE_EVIDENCE ./scripts/verify-dotnet.sh
PARCHWELL_TOOLS_DIRECTORY=/tmp/issue404-tools bash infra/scripts/lifecycle/check-offline.sh
npm run check:docs
```

## Implementation choices and gate evidence

Source locations below refer to baseline `3a4ee0b140a209c2cdeae0002a77c5349a6aabc7`.

The producer-factory override (`B10AbruptHost.cs` lines 212 to 213) replaces the production producer builder: the OAuth session is still created (line 209) but is not attached to the producer, and the `SetLogHandler` librdkafka log capture is not installed; neither bears on DA1, DA5 or DA6.

Existing control modes are unchanged except for the `hook-loaded.jsonl` append that plan section 4.4 requires in every mode. The test manages containers through the existing Testcontainers and Docker.DotNet APIs to preserve the ADR-020 process boundary. Private helper structure and directory ownership/cleanup follow the implementation choices allowed in plan sections 2.1, 4.2, 4.5 and R12. The ownership mechanism captures the generated hook mount root's numeric uid/gid through `stat`, marks restoration required before changing ownership, normalizes it to uid/gid 1654 through the helper, and restores the exact original uid/gid during cleanup on pass or failure; hook files remain readable at mode 644. This meets the mounted-directory contract without copying the hook or changing its dependencies.

Initial verification source: `35dfba2f24a27d01596564bb8bf40db6540c7afc`. Its built image ID is `sha256:da7fa5cb60972417598fdf10809ef890b0224429ddb3ee6a328b7a70e5107660`. The final locked Release test build above exited 0 with 0 warnings and 0 errors in 9.76 s (`/tmp/issue404-control/image-test-build-final.log`). The earlier parallel build failed with MSB4166 child-node exits (`/tmp/issue404-control/image-test-build.log`); the local build uses `-m:1` to avoid that infrastructure failure. No source contract changed for this adjustment.

Baseline attempt 1 at the initial verification source failed before the parent started. The ownership helper exited 1: `chown: cannot read directory '/owned/parent': Permission denied`, with the same failure for the successor and storage directories. The host temporary directories had mode 700 under the workspace umask; the helper's dropped capabilities could not traverse them. The saved diagnostics are in `/tmp/issue404-control/baseline-1-ownership-helper.log`; `/tmp/issue404-control/baseline-1.log` preserves the canceled runner session. The CLI exited 130 after SIGINT, the test host exited, and all containers, including the broker, ownership helper and Ryuk, were removed. No XML report or evidence line was produced. This is setup failure evidence, not DA1, DA5 or DA6 evidence. The in-scope correction sets directory and file modes explicitly before ownership transfer. The exited helper's readiness wait also failed to return before cancellation (V6); a race in Testcontainers' default running-state wait is the code agent's inference. Ownership and cleanup helpers use the existing Docker client's create/start/wait/remove APIs so a short-lived helper's exit is observed directly; parent, successor and broker retain Testcontainers.

Second verification source: `ec1a6c7b145a8d1c4f6e3aa5e182283f8209087c`. Its image publish and load pass with image ID `sha256:03ee120b5ec3ad8d36cd2b1333f0db781548c449b438778eae5939c521f1842a` (`/tmp/issue404-control/baseline-corrected-image-build.log`, `baseline-corrected-image-load.log` and `baseline-corrected-image-id.txt`). Its `baseline-corrected-1` attempt failed before the parent executed: Docker rejected the Testcontainers host-network attachment because `HostConfig.NetworkMode` was not set to `host`. The ownership helper succeeded. `/tmp/issue404-control/baseline-corrected-1-results/results.xml` has exactly 1 failed test in 26.165 s; the runner exited 2 (`baseline-corrected-1.log`). No observation line or DA proof was produced. The in-scope V8 correction sets `NetworkMode = "host"` in the shared role-container modifier and removes `WithNetwork("host")`. Owned test containers were removed; the resource reaper briefly remained for test-process cleanup. The V8 locked Release build with `-m:1` passed with 0 warnings and 0 errors in 16.81 s (`/tmp/issue404-control/image-test-host-network-build.log`). A subsequent static audit found the generated `/hook` mount root still owned by host uid 1000; its normalization to uid/gid 1654 and restoration joins the V5 ownership correction before the next freeze. No hook had executed.

Third verification source: `c36ced24511ed0fc81fde181f95e731bb67a27fb`. The locked Release build with `-m:1` passed with 0 warnings and 0 errors in 10.94 s (`/tmp/issue404-control/image-test-hook-owner-build-final.log`). Its `baseline-final-1` attempt failed at R7: the stage parser received Docker log lines prefixed by timestamps, so its expected stage collection was empty. `/tmp/issue404-control/baseline-final-1-results/results.xml` records exactly 1 failed test in 182.488 s; the CLI exited 2 (`baseline-final-1.log`). The code agent reports that R4, R5 and R6 assertions had passed before this failure: real image hooks, intended self-kill with exit 137, equal non-null cgroup observations with no capture gaps, and journal byte/hash equality. These are partial diagnostics from a failed run; they are not completed gate evidence. No successor assertion or observation line completed. V9 disables Docker log timestamps in role and tail calls; assertions stay unchanged.

Passing runs 1 and 2 use source `19d5f783cbf1010bb1d3b127b4c07ad140003f20`. The locked Release build with `-m:1` passed with 0 warnings and 0 errors in 9.82 s (`/tmp/issue404-control/image-test-late-binding-mode-build-final.log`). All three Docker role/tail log calls now request raw lines. The V5 late-file audit sets the predecessor binding file to mode 644 explicitly; the existing ownership helper already changes this new file to uid/gid 1654. The three prescribed `baseline-accepted` runs pass below, with two runs before the assertion-order change and one after it.

The helper correction initially failed to compile because its `GetContainerLogsAsync` call used an overload absent from the installed Docker.DotNet.Enhanced package. `/tmp/issue404-control/image-test-mode-fix-build.log` records 0 warnings and 2 CS1503 errors in 14.05 s, one root cause (V7). The call now uses the installed signature. The corrected locked Release build with `-m:1` exited 0 with 0 warnings and 0 errors in 11.14 s (`/tmp/issue404-control/image-test-mode-fix-build-final.log`); the later three complete image runs verify the corrected helper path.

The lifecycle offline check passed at `c36ced24511ed0fc81fde181f95e731bb67a27fb`: exit 0 in 104.848366 s, from 2026-11-11T06:47:07.044576Z to 2026-11-11T06:48:51.892936Z. The command is the lifecycle command above. Its log and machine summary are `/tmp/issue404-acceptance-c36ced24/check-offline.log` and `check-offline-summary.json`.

The full .NET verification command above passed at `859bec754671cb6e7120acb0f2b3f04763d6ee76`: exit 0 in 1111.307367 s, from 2026-11-11T07:15:51.873551Z to 2026-11-11T07:34:23.180874Z. All three offline variables were explicitly unset. Independent XML aggregation finds 10021 passed tests across six assemblies, with 0 failed, skipped, not-run or errors: Architecture 17, Enrichment 56, Kafka 491, LoadGen 1728, Scenarios 340 and Verifier 7389. The new image test returns and passes in 0.002 s. Evidence: `/tmp/issue404-acceptance-859bec75/verify-dotnet.log`, `verify-dotnet-summary.json`, `verify-dotnet-xml-summary.json` and raw reports `/tmp/parchwell-dotnet-verification.vG9ekSrv/`. The control session's independent summary also matches (`/tmp/issue404-control/full-suite-independent-summary.json`).

All prescribed image and mutation runs, full .NET verification and lifecycle checks are complete. `npm run check:docs` exits 0 with 10 warnings across 6 unchanged files and 0 warnings in changed files (`/tmp/issue404-control/check-docs.log`, `check-docs-summary.json`). The warnings in unchanged files are retained as existing advisory warnings outside this diff. Publication review records remain separate gates. The cold reviewer compares the implementation with the approved plan and recognized decisions; complete reports and dispositions belong to the PR self-review record. The passing local runs below are workspace evidence; Hari's post-merge workflow on `main` remains the trial gate.

The author's pre-mutation audit found that M3's zero exit-wait can return before the child writes its hook record. Checking hooks first could mask the required R5 failure with R4. The implementation places the `exit_observed` assertion before the hook assertions, retaining every assertion and literal value. This is an assertion-order choice within the approved scope, not an observed failure or a cold-review finding. This change is frozen at `859bec754671cb6e7120acb0f2b3f04763d6ee76`; its locked Release build with `-m:1` passes with 0 warnings and 0 errors in 15.92 s (`/tmp/issue404-control/image-test-failure-precedence-build-final.log`). The diff only moves the existing hook assertions after `exit_observed`. The first two passing runs retain their unchanged happy-path coverage, and the third prescribed run uses this final driver. Its image publish/load pass with image ID `sha256:b19b25eb8f2a8cad7d40786811036d7cd82517abfd4f9b9fe4e1b629b0862906` (`/tmp/issue404-control/baseline-accepted-3-image-build.log`, `baseline-accepted-3-image-load.log` and `baseline-accepted-3-image-id.txt`). All three mutation preparations are rebased, the prescribed faults reapplied and images rebuilt for this driver before their one test each. These builds verify changed negative-case behavior under pull-request rule 13; no extra verification or review root is counted. Mutation execution results are recorded below.

## Passing local image runs

All observations below are local to this workspace's Docker runner. They do not establish Event Hubs or AKS behavior and do not replace Hari's workflow run on `main`. Runs 1 and 2 use source `19d5f783cbf1010bb1d3b127b4c07ad140003f20` and image `sha256:bcc1cad442b7b16dff423a2ce5371649dc6fa3c79f4927dfa5cd12f44f83eafa`; each observation line pins its own source and image.

| Run | Result and evidence |
|---|---|
| `baseline-accepted-1` | CLI 0; exactly 1 passed, 0 failed, 0 skipped/not-run/errors; XML test 331.474 s (runner summary 332.624 s). `ValidateReport` and the exact-one-pass check succeed. Logs/report/line: `/tmp/issue404-control/baseline-accepted-1.log`, `baseline-accepted-1-results/results.xml`, `baseline-accepted-1-validation.log`, `baseline-accepted-1-evidence.txt`. All containers removed; hook mount's original uid/gid restored. |
| `baseline-accepted-2` | CLI 0; exactly 1 passed, 0 failed, 0 skipped/not-run/errors; XML test 325.024 s (runner summary 326.091 s). `ValidateReport` and the exact-one-pass check succeed. Logs/report/line: `/tmp/issue404-control/baseline-accepted-2.log`, `baseline-accepted-2-results/results.xml`, `baseline-accepted-2-validation.log`, `baseline-accepted-2-evidence.txt`. All owned containers removed; hook mount's original uid/gid restored. |
| `baseline-accepted-3` | Source `859bec754671cb6e7120acb0f2b3f04763d6ee76`, image `sha256:b19b25eb8f2a8cad7d40786811036d7cd82517abfd4f9b9fe4e1b629b0862906`; CLI 0; exactly 1 passed, 0 failed, 0 skipped/not-run/errors; XML test 340.137 s (runner summary 341.141 s). `ValidateReport` and the exact-one-pass check succeed. Logs/report/line: `/tmp/issue404-control/baseline-accepted-3.log`, `baseline-accepted-3-results/results.xml`, `baseline-accepted-3-validation.log`, `baseline-accepted-3-evidence.txt`. All containers removed; hook mount's original uid/gid restored. |

Run 1's exact observation line:

```text
b10_offline_image source_commit=19d5f783cbf1010bb1d3b127b4c07ad140003f20 image_id=sha256:bcc1cad442b7b16dff423a2ce5371649dc6fa3c79f4927dfa5cd12f44f83eafa runtime=10.0.13 kernel=6.18.57 parent_exit=0 exit_code_local_observation=137 termination=intended_self_kill journal_records=39 kill_unconfirmed=1 cut_accepted_to_kill_intent_microseconds=2850.641 kill_intent_to_exit_observed_microseconds=8994.247 successor_exit=0 consumed_resends=1
```

Run 2's exact observation line:

```text
b10_offline_image source_commit=19d5f783cbf1010bb1d3b127b4c07ad140003f20 image_id=sha256:bcc1cad442b7b16dff423a2ce5371649dc6fa3c79f4927dfa5cd12f44f83eafa runtime=10.0.13 kernel=6.18.57 parent_exit=0 exit_code_local_observation=137 termination=intended_self_kill journal_records=51 kill_unconfirmed=1 cut_accepted_to_kill_intent_microseconds=2447.052 kill_intent_to_exit_observed_microseconds=8477.007 successor_exit=0 consumed_resends=1
```

Run 3's exact observation line:

```text
b10_offline_image source_commit=859bec754671cb6e7120acb0f2b3f04763d6ee76 image_id=sha256:b19b25eb8f2a8cad7d40786811036d7cd82517abfd4f9b9fe4e1b629b0862906 runtime=10.0.13 kernel=6.18.57 parent_exit=0 exit_code_local_observation=137 termination=intended_self_kill journal_records=51 kill_unconfirmed=1 cut_accepted_to_kill_intent_microseconds=1827.569 kill_intent_to_exit_observed_microseconds=7821.183 successor_exit=0 consumed_resends=1
```

## Prescribed scratch mutations

The three isolated worktrees use base `859bec754671cb6e7120acb0f2b3f04763d6ee76` plus exactly one approved fault: M1 changes `exitCode == 137` to `exitCode == 138`; M2 deletes `file.Flush();`; M3 changes `WaitForExit(milliseconds)` to `WaitForExit(0)`. The patches are saved as `/tmp/issue404-acceptance-859bec75/m{1,2,3}/mutation.diff`. The main implementation's `src/` is unchanged.

Preparation summaries in each mutation directory record successful image publish, load, inspect and test build. These are build/preparation results, not mutation-test results.

| Mutation | Worktree | Rebuilt image ID |
|---|---|---|
| M1 | `/tmp/issue404-mutation-m1-859bec75` | `sha256:dbeccd375207f447e3b2eedc514850f2b5de8aa5059afbe745f08b5dd67b0833` |
| M2 | `/tmp/issue404-mutation-m2-859bec75` | `sha256:a873ea2518cfe5d6e3c5f96f54e4c901b24a3af24508441f4177108870f56188` |
| M3 | `/tmp/issue404-mutation-m3-859bec75` | `sha256:25401256d10c4b0c8d7fdb56d07ad96904a744fd0192ab91df28d8e786832a60` |

The following block records the exact commands, with `mutation` set to `m1`, `m2` or `m3` for its corresponding worktree. Preparation ran before the final `dotnet test`, which ran once per mutation, sequentially after the full .NET suite. These commands were authored before the first mutation test.

```sh
mutation=m1
offline_mutation_source=859bec754671cb6e7120acb0f2b3f04763d6ee76
cd "/tmp/issue404-mutation-$mutation-859bec75"
dotnet publish src/Parchwell.LoadGen/Parchwell.LoadGen.csproj --configuration Release -p:RestoreLockedMode=true -p:NuGetLockFilePath=packages.linux-x64.lock.json -r linux-x64 --self-contained false /t:PublishContainer "-p:ContainerImageTag=offline-$offline_mutation_source-$mutation" "-p:ContainerArchiveOutputPath=/tmp/issue404-acceptance-859bec75/$mutation/image.tar" -m:1
docker load --input "/tmp/issue404-acceptance-859bec75/$mutation/image.tar"
docker image inspect --format '{{.Id}}' "parchwell/loadgen:offline-$offline_mutation_source-$mutation"
dotnet build tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --configuration Release -p:RestoreLockedMode=true -m:1
env PARCHWELL_B10_OFFLINE_IMAGE="parchwell/loadgen:offline-$offline_mutation_source-$mutation" \
  PARCHWELL_B10_OFFLINE_SOURCE_COMMIT="$offline_mutation_source" \
  PARCHWELL_B10_OFFLINE_EVIDENCE="/tmp/issue404-acceptance-859bec75/$mutation/evidence.txt" \
  dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj \
  --configuration Release --no-build --minimum-expected-tests 1 \
  --results-directory "/tmp/issue404-acceptance-859bec75/$mutation/results" -- \
  --filter-method 'Parchwell.LoadGen.Tests.Abrupt.B10AbruptOfflineImageTests.Given_BuiltImage_When_ParentChildAndSuccessorRunInContainers_Then_JournalExitAndResendsAreObserved' \
  --report-xunit-xml --report-xunit-xml-filename results.xml \
  > "/tmp/issue404-acceptance-859bec75/$mutation/test.log" 2>&1
```

The validator reads each fresh `results/results.xml`, requires exactly one failed test, and compares the full failure text with the named requirement: M1 R5 (`unexpected_exit`), M2 R5 or R6, M3 R5 (`exit_observed` false) or R7. A survivor or unexpected failure stops execution for the plan's handoff; no retry or assertion change is included. All three prescribed mutations are killed by R5, exactly one test invocation each, with no survivor, retry or assertion change. Expected negative-control failures do not add verification-failure or review-finding roots.

| Mutation | One-shot result |
|---|---|
| M1 | Killed by R5: expected `termination` `intended_self_kill`, actual `unexpected_exit`, at driver line 156. CLI 2; XML exactly 1 failed, 0 passed/skipped/not-run/errors. XML test 195.117 s; invocation wall 196.998256 s. No observation file. `/tmp/issue404-acceptance-859bec75/m1/test-summary.json`, `results/results.xml` and `test.log` retain full command, environment, timings and failure. |
| M2 | One invocation; CLI 2; XML exactly 1 failed, 0 passed/skipped/not-run/errors. R5 fails with `b10_offline_image_exit_code:3` at driver line 154. XML test 192.001 s; invocation wall 194.007186 s. No observation file. `/tmp/issue404-acceptance-859bec75/m2/test-summary.json`, `results/results.xml` and `test.log` preserve the full evidence. Killed by R5, which the approved mutation table permits. This is an expected rejection of a deliberately altered image; all three unmutated runs record 137. It supplies no baseline proof and does not change the candidate exit-code stop. Exit 3 may be a no-kill path; the mechanism was not investigated, so no post-kill journal-tail loss is claimed. No assertion was loosened and no rerun occurred. |
| M3 | Killed by R5: `b10_offline_image_exit_unobserved` at driver line 151. One invocation; CLI 2; XML exactly 1 failed, 0 passed/skipped/not-run/errors. XML test 44.505 s; invocation wall 46.346152 s. No observation file. `/tmp/issue404-acceptance-859bec75/m3/test-summary.json`, `results/results.xml` and `test.log` retain the full evidence. |

## Contract comparison

The implementation at `859bec754671cb6e7120acb0f2b3f04763d6ee76` was compared with the pinned Revision 4 plan, the retained check's N3 and amendment 6788754429. Source inspection and executed evidence are distinguished below; no extra negative-case test run is claimed.

| Plan requirement | Implementation and evidence |
|---|---|
| R1 | The Fact returns immediately when the image variable is null/empty, before reading the other variables or constructing the Docker client. The full suite records its passing no-op path. |
| R2 | Source guards require 40 lowercase hex, a nonempty/nonexistent evidence file, reachable Docker and a present local image, with named failures. The invalid-value paths were inspected; no separate invalid-input executions are claimed. |
| R3 | `InitializeAsync` uses the existing pinned `LocalKafkaFixture`, creates `document-events` with 1 partition and replication factor 1, reads metadata broker names and writes the bound source commit. Exercised by all three complete runs. |
| R4 to R6 | `AssertHooks` checks the real image apphost/assembly paths, uid and parent/child PIDs. `ObserveAsync` checks the intended termination fields, cgroup equality, nonempty kill set, journal byte/length/hash equality, parsed frames, gaps/failures and final producing `kill_intent`. All three complete runs pass these checks; M1/M2 reject through R5 as recorded. |
| R7 and R8 | Raw parent stderr is filtered by role; the single `exit_observed` and first-occurrence stage order are checked without requiring a single publication marker. The cut acceptance and kill/exit tick intervals use the intent frequency and reject negative values. All three complete runs pass; each exact observation is retained above. |
| R9 | A fresh successor work directory shares only storage/image inputs. The predecessor receipt order equals the kill set; read-back checks attempt/instance headers, key, sequence and payload hash against each prepared predecessor. All three complete runs consume 1 resend. |
| R10 and R11 | The method stopwatch starts after the enable switch, its 900 s cancellation covers initialization/execution, and the final elapsed check follows cleanup. A successful observation is written using `FileMode.CreateNew` in the pinned key order/invariant decimals. All complete runs finish below 900 s and produce the exact line; no forced-over-bound execution is claimed. |
| R12 and R13 | `finally` invokes cleanup, tracks/removes role/helper containers, disposes the broker, restores hook ownership and removes passing temporary directories. Role/helper failure paths and bounds attach output tails capped at 16384 characters. Source paths were inspected; passing runs and mutation failures leave no owned container. No separate bound-expiry run is claimed. |
| Workflow/checker | The new protected workflow has only `workflow_dispatch`, a `main` guard, pinned setup actions, 60 minute timeout, contents-read permission, separate image/test builds, the exact single-test/report gates and printed observation. The lifecycle checker adds only the approved trigger entry. The lifecycle pass includes this workflow. |

The authored diff contains exactly the six paths allowed by plan section 2.1. Production `src/`, other tests/workflows, projects/packages, chart, scientific outputs and expected outcomes are unchanged. The permitted process-harness change adds the hook record in every mode and ensures `image-kafka` keeps real time; its existing storage and PLAINTEXT producer override are reused, with no cgroup override for this mode. Scope was checked against the diff; the acceptance aggregate confirms the main production diff is empty and each scratch worktree retains only its prescribed fault.

The accepted residual assumptions remain those in plan section 7: separately built trial image bytes, Docker-mounted runner storage versus AKS `emptyDir`, Docker/containerd seccomp and kernel differences, and omitted resource requests. They are not closed by these local observations. DA2 to DA4 remain accepted only for the diagnostic trial, and part 2's bindings remain separate. No trial or dispatch has run. The validator's aggregate is `/tmp/issue404-acceptance-859bec75/acceptance-summary.json`; the control session independently confirms all three one-shot rejections in `/tmp/issue404-control/mutation-independent-summary.json`.

## Failure and review root-cause ledgers

Amendment 6788754429 caps each ledger at 20 root causes per implementing session. The control session consolidates shared causes before recording the cumulative count. Cold-review pass counts are separate and cumulative across sessions.

| Verification root | Evidence and disposition |
|---|---|
| V1: ambiguous `Metadata` (CS0104) | Docker models collided with Confluent.Kafka; qualified `Confluent.Kafka.Metadata`. Evidence is the code agent's compiler transcript; no saved log exists. |
| V2: missing `PullPolicy` namespace (CS0103) | Imported the existing `DotNet.Testcontainers.Images` namespace. Evidence is the code agent's compiler transcript; no saved log exists. |
| V3: nullable `HostConfig` (CS8602) | Initialized `HostConfig` in both affected container-create modifiers. Evidence is the code agent's compiler transcript; no saved log exists. |
| V4: parallel MSBuild child-node failure (MSB4166) | `image-test-build.log`; serial `-m:1` build succeeds in `image-test-build-final.log`. No contract change. |
| V5: ownership helper cannot traverse host directories | Baseline attempt 1: mode 700 directories deny the helper traversal with dropped capabilities. Explicit directory and file modes before `chown` are the in-scope correction, verified by the later complete image runs. The same audit found generated hook-output directories at 700 and the test DLL at 600; generated directories/files are normalized to 755/644 before any hook attempt. No hook failure was observed. The same audit requires uid/gid 1654 on the generated hook mount root with original-owner restoration during cleanup. |
| V6: exited helper readiness wait does not return | Baseline attempt 1 required cancellation after the ownership helper exited. Existing Docker client create/start/wait/remove APIs replace the helper readiness path; the default-wait race diagnosis remains inferred. |
| V7: unsupported container-log API overload (CS1503) | `image-test-mode-fix-build.log` records two argument errors from one call. The installed `GetContainerLogsAsync(id, parameters, cancellationToken)` signature is the correction; the 11.14 s corrected build passes. |
| V8: host network attachment without host network mode | `baseline-corrected-1` fails before parent execution. Set `HostConfig.NetworkMode` to `host` in the shared parent/successor modifier and remove `WithNetwork("host")`; the 16.81 s corrected build passes and `baseline-final-1` reaches real execution. |
| V9: timestamped Docker log lines do not match stage parser | `baseline-final-1` fails at R7 with an empty parsed stage collection. Set `timestampsEnabled: false` in all role/tail log calls; assertions unchanged. The 9.82 s corrected build passes; all three subsequent full runs pass. |
| V10: external XML aggregation helper compares paths with simple names | The control tool transcript records its assertion failure despite valid suite reports. Normalize `Path(assembly.name).stem`; the independent summary verifies all six reports. No implementation or suite change. Counted conservatively as a verification root. |

The control session consolidates V1 to V10 in `/tmp/issue404-control/verification-failure-ledger.json`: 10 verification roots. The initial final build's 0 warnings and 0 errors verify V1 to V3; the corrected build verifies V7. The second attempt's successful ownership helper verifies V5 and V6 at setup; the later three complete image runs verify their runtime and cleanup paths. At preparation of the first cold-review input, the review-finding root count is 0 and completed cold-review counts are 0 comprehensive and 0 focused. Later complete reports, dispositions and cumulative counts belong to the PR self-review record.

## First cold-review input

Mode: comprehensive R1, the entire authored PR diff against `origin/main` at `3a4ee0b140a209c2cdeae0002a77c5349a6aabc7`. The reviewed input revision is the final frozen commit containing this receipt and is identified in the frozen-diff metadata and PR self-review record. Under pull-request rule 8, this receipt need not describe its own containing commit.

The reviewer receives only the frozen full authored diff and this authored receipt, with revision metadata outside the receipt copy. Review inputs stay frozen until all started reviews return complete reports. Corrections are consolidated before another applicable review. No review result is claimed here; the last cold-review round, complete reports/dispositions, cumulative cold-review and hosted-review counts, final-head CI and hosted code/security review results are recorded in the PR description and final handoff. The earliest authorized publication checkpoint remains the tested, documentation-checked diff with applicable cold-review coverage. Hari merges the protected workflow; no agent dispatch is authorized.

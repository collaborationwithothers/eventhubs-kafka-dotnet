# Offline image test implementation receipt

This pull request implements the approved offline end-to-end image test for issue 404. It tests the real parent, self-killing child and successor against local Kafka, and adds the workflow Hari runs before a diagnostic trial. It authorizes no dispatch or trial.

## Execution card

- Deliverable and contracts: [Revision 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/66517e9a9f28dae97d93c37d007b7feef5e362f3/items/404/plan-offline-e2e-test.md), sections 2 to 8; exact-body SHA-256 `d83bc0d509fe3d4a6de118a32a565842132275d38b19cdea4ad37bcd8d7496d1` verified before implementation.
- Authorization: [plan pointer](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788002493), [Hari's approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788501730), and decisions [6787880185](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787880185), [6788215099](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788215099), [6788464379](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788464379) and their cited decisions. Their sources are read in full by the control session.
- Plan check: [6788423093](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788423093), on Revision 3, stands for Revision 4 under the approval. Decision 6788464379 resolves B1; N3 applies below.
- Branch: `codex/404-offline-e2e-test`; base `3a4ee0b140a209c2cdeae0002a77c5349a6aabc7`. Execution part 2 and classification are separate work.
- Mandatory gates: plan section 8's three image runs and three separate scratch mutation runs, full .NET verification with the variables unset, lifecycle checks, documentation checks, cold review, final-head CI, hosted Codex code and security reviews. Hari merges the protected workflow; no agent dispatches it.
- Earliest publication: after the prescribed local acceptance evidence, documentation check and applicable complete cold review. Publish then and run CI and hosted reviews together.
- Review budget: two comprehensive passes and two focused correction rounds under `docs/agents/pull-requests.md` rule 8. Exhaustion or unsafe focus means push `-wip` and hand off to Hari. No reviews have started.
- Predicate map: not applicable; this is not verifier work. Plan section 6 maps the test's requirements and the three mutations.

## Verification commands

The exact commands and results below are recorded as they run. The approved fixed workload is three baseline image runs and one run of each of the three mutations; no diagnostic rerun is authorized by this receipt.

The control session records the committed acceptance source as `offline_source_commit` before building; the image tag, test environment and observation all use that value. The build commands are:

```sh
offline_source_commit="$(git rev-parse HEAD)"
dotnet publish src/Parchwell.LoadGen/Parchwell.LoadGen.csproj --configuration Release -p:RestoreLockedMode=true -p:NuGetLockFilePath=packages.linux-x64.lock.json -r linux-x64 --self-contained false /t:PublishContainer "-p:ContainerImageTag=offline-$offline_source_commit" -p:ContainerArchiveOutputPath=/tmp/issue404-control/loadgen.tar
docker load --input /tmp/issue404-control/loadgen.tar
docker image inspect --format '{{.Id}}' "parchwell/loadgen:offline-$offline_source_commit"
dotnet build tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --configuration Release -p:RestoreLockedMode=true
```

For each of the three baseline runs, set `offline_run` to `1`, `2` or `3` once and use a fresh results directory and evidence file. The targeted command and both report checks match the workflow:

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

Measurements, source-specific results, specification comparison and complete review reports are pending. No image-run evidence is yet claimed.

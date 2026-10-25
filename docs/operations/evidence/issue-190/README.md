# Issue 190 validation receipt

AKS start now submits immediately when the cluster is Stopped/Succeeded. Stop already submits immediately on Running/Succeeded; the sweeper retains its existing idle delay and active-dispatch protection.

Tested source commit: `ba3997e46fb3552ef0e35ab925eea5fa59efef1a`, based on `3727c612b00e694b9968347c92b634c7bac3cdee`. Validation ran offline on 2026-10-25. It establishes no live Azure timing result.

Authority: [plan revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/190#issuecomment-6399908887), [independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/190#issuecomment-6399940681), [immediate-start decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/190#issuecomment-6399868145), and [retained sweeper delay](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/190#issuecomment-6399882449). The plan check's exact runbook clarification was applied.

Commands executed from the repository root:

```sh
PYTHONDONTWRITEBYTECODE=1 PATH=/tmp/issue-190-tools:$PATH python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'
PATH=/tmp/issue-190-tools:$PATH python3 infra/scripts/lifecycle/command.py 600 -- shellcheck -x infra/scripts/lifecycle/lifecycle.sh
PARCHWELL_TOOLS_DIRECTORY=/tmp/issue-190-tools bash infra/scripts/lifecycle/check-offline.sh
cmp .github/workflows/sweeper.yml docs/operations/workflows/sweeper.yml.txt
npm run check:docs
git diff --check
```

The Python suite passed 76 tests with no failures, errors or skips. Synthetic retry submissions were exactly `[0, 300, 600, 900, 1200, 1500, 1800]` seconds, with 120 reads and exhaustion at 3600 seconds. The transitional-state test submitted at `[0, 330]` seconds and finished at 360 seconds. Stop submitted at `[0]` seconds and completed readback at 60 seconds. Python arithmetic assertions checked these literal polling-time expectations.

The existing seven dispatch-history cases passed, retaining the 3599/3600-second stop-eligibility boundary and active/unknown-history protection. ShellCheck, the offline checker and the workflow/mirror comparison exited 0. Offline chart acceptance reported 21 positive and 32 negative cases; both smoke renders passed strict schema validation.

The plan's exact pinned-PyYAML workflow check also exited 0 and printed `sweeper shared-stop wiring and triggers passed`. It checked that the stop step invokes `bash infra/scripts/lifecycle/lifecycle.sh stop-cluster`, requires `steps.history.outputs.stop_eligible == 'true'`, and retains the dispatch/scheduled triggers. No workflow, idle predicate or identity assignment changed.

The documentation scan and whitespace check exited 0. No documentation warnings were reported, including in the changed runbook and receipt. At receipt preparation, no cold-review round had completed; the pull request records the final round and final-head CI. Generated logs and downloaded tools remain outside the repository.

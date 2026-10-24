# Incomplete LoadGen execution diagnostics receipt

LoadGen now logs the existing run-manifest reasons and attempted/planned counts before raising `worker_run_incomplete`. Runner debug failures also include captured worker output for rejected start-line and LoadGen executions. The patch exposes the failing condition without changing execution or publication acceptance.

## Scope and evidence

Hari supplied the post-merge `loadgen_execution` failure and then the worker log showing `worker_run_incomplete`, in the ongoing interactive diagnosis and repair request. [Issue #148](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/148) records the bounded follow-up. The supplied pod snapshot agrees with the unsuccessful application exit; the original executor reason remains unknown.

On the source baseline, `RunExecutor` records execution/bookkeeping diagnostics in its returned manifest. `LoadGenWorker` throws on a nonzero result before calling the publisher, without logging those diagnostics. The original manifest and native captured artifacts therefore need not reach Blob Storage for this failure. The new structured worker log includes the existing bounded diagnostic codes and counts with scenario/run identity; it does not include raw native messages or configuration. Incomplete execution still raises the same exception, exits unsuccessfully and never invokes publication.

The Runner's existing `WithWorkerOutput` helper now decorates the two pre-verifier exit/receipt rejection paths. Its exact-true debug setting, bounded worker tails, identity fields, original failure reasons and default safe output remain as before. No evidence, workload, cost, timeout, identity, verdict, workflow or dependency rule changes.

## Validation

Source baseline: `e6fd02ebcec9d59bd755b3eb8e9e03fac020c9b8`, with this PR's source/test edits. SDK: `10.0.402` at `/tmp/parchwell-dotnet/dotnet`. New host cases use synthetic authentication failures and a successful counterpart; no live Kafka or Azure behavior is claimed.

| Command | Observed result |
|---|---|
| `PATH=/tmp/parchwell-dotnet:$PATH timeout 180 dotnet build Parchwell.slnx --configuration Release --no-restore` | Passed with zero warnings and errors |
| `PATH=/tmp/parchwell-dotnet:$PATH timeout 180 dotnet tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll -class '*LoadGenWorkerHostTests' -maxThreads 4 -noLogo -result-xml /tmp/parchwell-148-host-tests.xml` | All 34 selected host tests passed; no errors, failures, skips or not-run tests |
| `PATH=/tmp/parchwell-dotnet:$PATH timeout 180 dotnet tests/Parchwell.Runner.Tests/bin/Release/net10.0/Parchwell.Runner.Tests.dll -maxThreads 4 -noLogo -result-xml /tmp/parchwell-148-runner-tests.xml` | All 1151 Runner tests passed; no errors, failures, skips or not-run tests |
| `npm run check:docs` | Exit 0; 36 advisory warnings in unchanged historical upstream captures under `issue-114/raw/review-10-fixes/`; this receipt has no warnings |
| `git diff --cached --check` | Exit 0 |

Test counts were read from the generated XML reports. The host regression cases verify the specific manifest diagnostic, structured identity/count fields, omission of synthetic credential/installation secrets, unsuccessful exit and absence of publication. The success case verifies absence of the new incomplete-run log. Runner cases cover nonzero exit and invalid/missing receipt for both worker modes, with debug enabled and disabled.

Program output stays outside Git. The documentation advisories concern existing upstream includes, relative links and punctuation outside this patch. The PR description records the final cold-review result and the models used. No live scenario was dispatched. A complete local full-suite pass and a fix for the unknown live executor condition are not claimed.

# Verifier failure diagnostics receipt

The Runner now attaches captured verifier logs, exit status, scenario ID and run ID when result retrieval or worker acceptance fails. Debug export-conflict output also identifies the object path, write outcome and content hashes. These changes make failures diagnosable; they do not establish why the live verifier failed.

## Scope and authorization

Hari requested "Diagnose and implement a fix" in the interactive session, then explicitly requested the pull request and cold review. [Issue #146](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/146) records this bounded follow-up to the existing diagnostics. This is an interactive repair, with no separate factory-plan approval claimed.

The supplied phase log reports `BlobNotFound` during `KubernetesRunCluster.VerifyAsync`, followed by an export readback conflict. The storage screenshot contains older and newer files under one environment run prefix. Reuse of the prefix is a possible explanation for the conflict; neither the exact conflicting object nor the verifier's original exception was established from those inputs.

Only exact `PARCHWELL_DEBUG_FAILURES=true` enables the additional output. Worker streams use the existing last-50-line helper. The original exception and dependent command details remain available. Default output, worker acceptance, create-only evidence writes, verification and cleanup behavior retain their existing rules. Debug output is unredacted, as the existing [runbook](../../a01-runner.md) documents.

## Validation

Source baseline: `6e93e85dab3c43e9e64ffb4faaa3ed08b96eda61`, with this PR's source and test edits. SDK: `10.0.402` installed at `/tmp/parchwell-dotnet/dotnet`. All added test payloads are synthetic.

| Command | Observed result |
|---|---|
| `PATH=/tmp/parchwell-dotnet:$PATH timeout 180 dotnet build Parchwell.slnx --configuration Release --no-restore` | Passed with zero warnings and errors after the cold-review correction |
| `PATH=/tmp/parchwell-dotnet:$PATH timeout 180 dotnet tests/Parchwell.Runner.Tests/bin/Release/net10.0/Parchwell.Runner.Tests.dll -maxThreads 4 -noLogo -result-xml /tmp/parchwell-146-final-runner-tests.xml` | Passed after the cold-review correction: 1143 tests, zero errors, failed, skipped or not-run tests; counts checked from the XML report |
| `PATH=/tmp/parchwell-dotnet:$PATH timeout 600 ./scripts/verify-dotnet.sh` | At receipt preparation: locked restore, Release solution build, Kafka XML inventory consistency, all 8 architecture tests/report validation, and all 306 Kafka tests/report validation passed. This broader run started before the cold-review correction and had not completed; no full-suite pass is claimed by this receipt |
| `npm ci --ignore-scripts` | Passed using the committed dependency lock |
| `npm run check:docs` | Exit 0; 36 advisory warnings in unchanged historical captures under `issue-114/raw/review-10-fixes/`; no warnings in this receipt |
| `git diff --cached --check` | Exit 0 |

The documentation advisories concern missing upstream includes, relative links and punctuation in existing captures. Those files are outside this patch. No Azure access or live scenario was run. Program output remains outside Git; this document is the PR's authored receipt. The PR description records the final broader-verification outcome, independent cold-review result and models used.

## Cold-review correction

The first independent review found that the new worker diagnostic headers omitted the scenario and run identifiers required by the .NET logging standard. Both production attachment sites now supply their existing worker identity, and the status and stream headers include those identifiers. The synthetic regression assertions verify identity on each header and retain their previous checks for worker output, exit status, Blob errors, bounded tails and default safe output. The build and Runner results above cover this corrected source.

The phase judge now reads the producer's confluent_kafka runtime-version key. Synthetic production-writer fixtures pass through the real verify and judge handlers, preserving the recorded version exactly.

## Source

- Approved [Canonical producer version plan, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/214#issuecomment-6462132828), last edited 2026-10-28T06:14:27Z, with the [independent standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/214#issuecomment-6462191181).
- Plan source: `main` at `1f14866faf395ecb28d3b9009986c1b332074386`. Branch base: `6942f20bd434804ddc2cbd7b7d276a3e9ab6bab3`; the implementation files were unchanged between these revisions.
- Tested code and test commit: `75b105529f6aa03e32178f266ba5f39274c1f384`.

## Validation

Validation ran on Linux with .NET SDK `10.0.402`, installed separately at `/tmp/issue214-dotnet`, and Node `24.19.0` with npm `11.9.0`. After an initial build lost its MSBuild child nodes with `MSB4166` (exit 1), subsequent .NET commands used `DOTNET_PROCESSOR_COUNT=2` and `MSBUILDDISABLENODEREUSE=1`. That initial failure was a setup failure, not the RED regression proof. A PTY retry could not start because the execution transport disconnected; it produced no build result.

The regression tests were added before the production lookup changed. The RED candidate assertion expected `"2.16.0"` or `"2.16.0+worker-build"`, and received `null` for each row. Both rows first passed the real verify-handler result, phase status and count assertions. Changing only the judge's lookup to the existing `confluent_kafka` wire key made the judge suite pass.

Commands ran from the repository root. `plan214_reports` was the fresh directory `/tmp/issue214-checks.3dE7Sd`; generated logs and XML reports remain outside the repository.

| Command | Exit | Measured evidence |
| --- | --- | --- |
| `dotnet restore Parchwell.slnx --locked-mode` | 0 | Locked restore succeeded. |
| `dotnet build Parchwell.slnx --configuration Release --no-restore` before the fix, with the setup above | 0 | No warnings or errors. |
| `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-build --minimum-expected-tests 2 --results-directory "$plan214_reports/red" -- --filter-method 'Parchwell.Verifier.Tests.Judge.JudgeHandlerTests.Given_WorkerRuntimeVersions_When_Judged_Then_PreservesExactProducerVersion' --report-xunit-xml --report-xunit-xml-filename results.xml` | 2 | RED: 0 passed, 2 failed, 0 skipped; both intended null-versus-literal candidate failures. |
| `dotnet build Parchwell.slnx --configuration Release --no-restore` after the fix | 0 | No warnings or errors. |
| `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-build --minimum-expected-tests 1 --results-directory "$plan214_reports/judge" -- --filter-class 'Parchwell.Verifier.Tests.Judge.JudgeHandlerTests' --report-xunit-xml --report-xunit-xml-filename results.xml` | 0 | 68 passed, 0 failed, 0 skipped. Includes both success rows, all four missing/disagreeing rows and the augmented empty-input case. |
| `dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --configuration Release --no-build --minimum-expected-tests 1 --results-directory "$plan214_reports/writer" -- --filter-class 'Parchwell.LoadGen.Tests.Evidence.GoldenArtifactWriterTests' --report-xunit-xml --report-xunit-xml-filename results.xml` | 0 | 1 passed, 0 failed, 0 skipped. Production writer bytes match the committed fixture. |
| `./scripts/verify-dotnet.sh` | 0 | 4,666 passed, 0 failed, 0 skipped, 0 not run, 0 runner errors; all report checks passed. |
| `npm ci --ignore-scripts` | 0 | Existing locked dependencies installed. |
| `npm run check:docs` | 0 | Scans before and after adding this receipt each reported the same 8 advisory warnings, all in unchanged files. |

Full verification reports are retained in `/tmp/parchwell-dotnet-verification.kd7Nfz3M`. The counts below were computed from the XML reports and checked against their individual test results; the verification script independently validated every report.

| Report assembly | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| `Parchwell.ArchitectureTests` | 12 | 0 | 0 |
| `Parchwell.Enrichment.Tests` | 55 | 0 | 0 |
| `Parchwell.Kafka.Tests` | 403 | 0 | 0 |
| `Parchwell.LoadGen.Tests` | 556 | 0 | 0 |
| `Parchwell.Scenarios.Tests` | 200 | 0 | 0 |
| `Parchwell.Verifier.Tests` | 3,440 | 0 | 0 |

The entry point also passed locked restore, Release build, Kafka XML inventory consistency, evaluated project settings and scaffold selection. Its warning control restored and built the valid counterpart, then rejected the intended `CS1030` warning as an error. The documentation warnings are pre-existing issues in unchanged files and remain advisory under AGENTS.md rule 8.

The fixtures are synthetic. This pull request establishes metadata propagation through the existing worker publication format and verifier handlers; it does not establish a new live scenario result. Missing or disagreeing version evidence remains null with `version_unavailable`, independently of message outcomes.

## Cold review

Round 1 applied the repository-owned repo-review skill to the complete authored diff and this receipt while full verification was explicitly pending. It reported `Findings: none at P0 or P1` and no P2 findings. The reviewer confirmed the lookup and tests matched the approved plan and the RED/GREEN counts matched the XML reports. Its only pre-existing observations were the intentionally unchanged dotted-key shared fixture and the documentation warnings in unchanged files. No finding disposition was consumed.

Round 1 is the completed clean round recorded by this receipt. The final receipt now includes the completed full-verification results and is reviewed again before publication. The pull request description records the last cold-review round and the final head's CI run under AGENTS.md "Before opening a pull request" rule 8; this receipt does not attempt to describe its own head commit. Cold reviewers use fresh contexts at high effort; the first round's resolved model identifier was not exposed.

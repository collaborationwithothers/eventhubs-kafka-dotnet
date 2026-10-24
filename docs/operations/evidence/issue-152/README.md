# Verifier input capacity for A01

The verifier now uses a separate input-entry budget while preserving its manifest inventory cap. The full-size synthetic regression reproduces the supplied ledger-limit failure with the old budget and publishes exact complete counts with the checked-in deployment settings.

## Scope and authority

[Issue 152](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/152) records Hari's chat authorization, failure and scope. `VerifyRunHandler` used `MaxInventoryEntries` both for the inventory count and for `InputLimits.MaxEntries`, which also counts supporting metadata and structural JSON entries. The index consumes entries before the ledger, and each manifest inventory object has multiple properties.

`MaxInputEntries` is required positive Int64 configuration in verify mode. It supplies the existing per-document structural entry ceiling and aggregate charged-entry ceiling; `MaxInventoryEntries` still supplies the independent manifest attempt limit. The [A01 limits](../../a01-runtime-limits.json) select finite input capacity `4194304` and retain inventory capacity `186000`; these are execution settings, not measurements of Event Hubs. Startup and deployment validation reject absent or nonpositive input capacity. The deployment generator forwards both settings to the verifier environment.

No parser, numbered judgement, outcome, expected-result, workload, identity, timeout, byte, row, depth, broker or consumed-record check changes. Probe-gate and start-line mode requirements are unchanged. Existing test assertions are retained; host/component fixtures supply the new explicit setting. This does not fix the separate pod-watcher shutdown cancellation.

## Validation

Source baseline: `00d92779fe05d46134233101f5eb373562c2df10`, with this PR's source/test edits. SDK: `10.0.402` at `/tmp/parchwell-dotnet/dotnet`. All added workloads and identities are synthetic.

| Command | Observed result |
|---|---|
| `PATH=/tmp/parchwell-dotnet:$PATH timeout 180 dotnet build Parchwell.slnx --configuration Release --no-restore` | Passed with zero warnings and errors |
| `PATH=/tmp/parchwell-dotnet:$PATH timeout 180 dotnet tests/Parchwell.Verifier.Tests/bin/Release/net10.0/Parchwell.Verifier.Tests.dll -class '*VerifyInputCapacityTests' -maxThreads 2 -noLogo -result-xml /tmp/parchwell-152-capacity-tests.xml` | All 8 capacity cases passed; no errors, failures, skips or not-run cases |
| `PATH=/tmp/parchwell-dotnet:$PATH timeout 600 dotnet tests/Parchwell.Verifier.Tests/bin/Release/net10.0/Parchwell.Verifier.Tests.dll -maxThreads 4 -noLogo -result-xml /tmp/parchwell-152-verifier-tests.xml` | All 2311 verifier tests passed, including local Kafka component tests; no errors, failures, skips or not-run cases |
| `PATH=/tmp/parchwell-dotnet:$PATH timeout 180 dotnet tests/Parchwell.Runner.Tests/bin/Release/net10.0/Parchwell.Runner.Tests.dll -maxThreads 4 -noLogo -result-xml /tmp/parchwell-152-runner-tests.xml` | All 1169 passed; no errors, failures, skips or not-run cases |
| Architecture setup and invocation below | All 8 passed; no errors, failures, skips or not-run cases |
| `npm run check:docs` | Exit 0; 36 advisory warnings in unchanged historical upstream captures under `issue-114/raw/review-10-fixes/`; none in changed documents |
| `git diff --cached --check` | Exit 0 |

Counts were read from generated XML. The full-size case derives `186000` attempts from the checked-in A01 profile, serializes that many distinct matching ledger and inventory entries, sets matching consumed-record identities and refreshes index hashes and lengths. With the inventory-sized parser budget, it asserts the exact combined-entry exception during the ledger read and no publication. With the deployed input budget, it asserts `complete_valid`, `ok=186000`, all other counts zero, no failed checks and one output call. This establishes offline capacity with synthetic evidence; it does not establish a live verdict or the maximum of every possible input combination.

Other cases cover empty and below/at/above inventory capacity, below/at/above structural input capacity, startup positivity/Int64 bounds, existing strict aggregate/byte/row/depth readers, and exact generated environment settings. Program output remains outside Git. No live workflow was dispatched and no full-repository test-suite or mutation pass is claimed. The PR description records cold-review findings and models.

Architecture setup follows `scripts/verify-dotnet.sh`:

```sh
mkdir -p /tmp/parchwell-152-manifests
PATH=/tmp/parchwell-dotnet:$PATH
export PATH
shopt -s globstar nullglob
for project in src/**/*.csproj tests/**/*.csproj; do
  manifest="/tmp/parchwell-152-manifests/$(basename "${project%.csproj}").json"
  timeout 30 dotnet msbuild "$project" -p:Configuration=Release -t:AddImplicitDefineConstants,_DisableDiagnosticTracing -getProperty:MSBuildProjectFullPath,TargetFramework,Nullable,TreatWarningsAsErrors,AnalysisLevel,EnableNETAnalyzers,RunAnalyzersDuringBuild,RunAnalyzers,AssemblyName,RootNamespace,ProjectDir,DefineConstants,LangVersion,EnforceCodeStyleInBuild -getItem:ProjectReference,Compile,CompilerVisibleProperty -nologo > "$manifest" 2> "$manifest.stderr" || exit "$?"
done
PARCHWELL_REPOSITORY_ROOT="$PWD" PARCHWELL_PROJECT_MANIFEST_DIRECTORY=/tmp/parchwell-152-manifests timeout 120 dotnet tests/Parchwell.ArchitectureTests/bin/Release/net10.0/Parchwell.ArchitectureTests.dll -maxThreads 4 -noLogo -result-xml /tmp/parchwell-152-architecture-tests.xml
```

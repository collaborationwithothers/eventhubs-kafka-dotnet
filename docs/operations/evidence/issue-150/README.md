# Terraform apply cancellation diagnostics

The Runner now preserves bounded partial Terraform output when its apply sequence is cancelled. It records the interrupted command and cancellation source in safe diagnostics and writes the capture to private evidence before rethrowing the original cancellation.

## Scope and authority

[Issue 150](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/150) records Hari's chat authorization and the implementation scope. [Scenario run 40081651458](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40081651458) supplied the cancelled-apply symptom; its second attempt completed guarded cleanup after Hari released the stale lock. This patch does not identify the stalled Azure resource or change Terraform ownership under [issue 138](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138).

`BoundedProcessRunner` retains only output already read within its existing byte bounds. Its cancellation exception carries that capture after the existing process-tree kill and bounded reap. `TerraformEnvironment.ApplyAsync` retains the same exception, adds command/cause fields, and attempts an independent five-second private write. Caller cancellation wins over an observed shared apply deadline. The existing command deadline is used when neither parent token is cancelled; a cancellation without boundary information is explicitly unknown.

`terraform-apply-cancellation.private.json` carries scenario/run identity, command, cause, partial streams and capture availability. The normal private exporter accepts it. Exact-true debug prints existing bounded command tails; default console output contains only allowlisted command/cause/evidence-status fields. No timeout increase, state-lock release, retry, ledger reset, scientific-gate change, workflow change or dependency is included. A failed evidence write cannot replace the original cancellation.

## Validation

Source baseline: `3d71e694e4c59940bb017ae825564a668eecd25f`, with this PR's source/test edits. SDK: `10.0.402` at `/tmp/parchwell-dotnet/dotnet`. All child output and identities in the new cases are synthetic.

| Command | Observed result |
|---|---|
| `PATH=/tmp/parchwell-dotnet:$PATH timeout 180 dotnet build Parchwell.slnx --configuration Release --no-restore` | Passed; zero warnings and errors |
| `PATH=/tmp/parchwell-dotnet:$PATH timeout 180 dotnet tests/Parchwell.Runner.Tests/bin/Release/net10.0/Parchwell.Runner.Tests.dll -maxThreads 4 -noLogo -result-xml /tmp/parchwell-150-runner-tests.xml` | All 1164 passed; no errors, failures, skips or not-run cases |
| Architecture preparation and invocation below | All 8 passed; no errors, failures, skips or not-run cases |
| `npm run check:docs` | Exit 0; 36 advisory warnings in unchanged historical upstream captures under `issue-114/raw/review-10-fixes/`; none in the changed documents |
| `git diff --cached --check` | Exit 0 |

Test counts were read from generated XML. New cases cover init/plan/apply interrupted by each cancellation source, simultaneous parent cancellation precedence, original exception identity, exact captured streams, process reaping, safe/debug diagnostics, private export, unavailable capture and an unwritable evidence directory. Existing output-byte boundary and output-limit rejection cases also pass. An initial direct architecture invocation lacked the required evaluated-project environment; the invocation below supplies it and passes.

Architecture setup follows the manifest collection in `scripts/verify-dotnet.sh`:

```sh
mkdir -p /tmp/parchwell-150-manifests
PATH=/tmp/parchwell-dotnet:$PATH
export PATH
shopt -s globstar nullglob
for project in src/**/*.csproj tests/**/*.csproj; do
  manifest="/tmp/parchwell-150-manifests/$(basename "${project%.csproj}").json"
  timeout 30 dotnet msbuild "$project" -p:Configuration=Release -t:AddImplicitDefineConstants,_DisableDiagnosticTracing -getProperty:MSBuildProjectFullPath,TargetFramework,Nullable,TreatWarningsAsErrors,AnalysisLevel,EnableNETAnalyzers,RunAnalyzersDuringBuild,RunAnalyzers,AssemblyName,RootNamespace,ProjectDir,DefineConstants,LangVersion,EnforceCodeStyleInBuild -getItem:ProjectReference,Compile,CompilerVisibleProperty -nologo > "$manifest" 2> "$manifest.stderr" || exit "$?"
done
PARCHWELL_REPOSITORY_ROOT="$PWD" PARCHWELL_PROJECT_MANIFEST_DIRECTORY=/tmp/parchwell-150-manifests timeout 120 dotnet tests/Parchwell.ArchitectureTests/bin/Release/net10.0/Parchwell.ArchitectureTests.dll -maxThreads 4 -noLogo -result-xml /tmp/parchwell-150-architecture-tests.xml
```

Program output remains outside Git. The documentation advisories concern pre-existing upstream includes, relative links and punctuation; changing those captures is outside this diagnostics fix. The PR description records the final cold-review result and models. No live workflow was dispatched, and no complete local full-suite pass or fix for the unknown Azure stall is claimed.

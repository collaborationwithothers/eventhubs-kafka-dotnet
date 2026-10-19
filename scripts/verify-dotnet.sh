#!/usr/bin/env bash
set -euo pipefail

repository_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repository_root"
if ! command -v dotnet >/dev/null 2>&1; then
    echo "Install .NET SDK 10.0.402 separately, then rerun ./scripts/verify-dotnet.sh." >&2
    exit 1
fi
if sdk_version=$(dotnet --version); then
    if [[ "$sdk_version" != "10.0.402" ]]; then
        echo "Expected .NET SDK 10.0.402; selected $sdk_version." >&2
        exit 1
    fi
else
    exit "$?"
fi

verification_results=$(mktemp -d "${TMPDIR:-/tmp}/parchwell-dotnet-verification.XXXXXXXX")
trap 'echo "Verification logs and reports: $verification_results"' EXIT
mkdir -p "$verification_results/manifests"
export PARCHWELL_REPOSITORY_ROOT="$repository_root"
export PARCHWELL_PROJECT_MANIFEST_DIRECTORY="$verification_results/manifests"

run_logged() {
    local label=$1 logfile=$2
    shift 2
    if "$@" >"$logfile" 2>&1; then
        echo "$label: passed"
    else
        local status=$?
        echo "$label: failed (exit $status)" >&2
        tail -n 80 "$logfile" >&2
        return "$status"
    fi
}

run_tests() {
    local project=$1 name=$2
    local results="$verification_results/$name"
    mkdir -p "$results"
    local test_status=0 evidence_status=0
    if [[ "$name" == "Parchwell.Kafka.Tests" ]]; then
        export PARCHWELL_SPIKE_EVIDENCE_DIRECTORY="$results/spike"
    fi
    run_logged "$name tests" "$results/test.log" dotnet test \
        --project "$project" --configuration Release --no-build \
        --minimum-expected-tests 1 --results-directory "$results" -- \
        --report-xunit-xml --report-xunit-xml-filename results.xml || test_status=$?
    if [[ "$name" == "Parchwell.Kafka.Tests" ]]; then
        dotnet msbuild scripts/dotnet-verification.proj -t:PublishSpikeEvidence \
            "-p:EvidenceDirectory=$PARCHWELL_SPIKE_EVIDENCE_DIRECTORY" -nologo || evidence_status=$?
        unset PARCHWELL_SPIKE_EVIDENCE_DIRECTORY
    fi
    if [[ "$test_status" -ne 0 ]]; then
        cat "$results/test.log" >&2
        return "$test_status"
    fi
    if [[ "$evidence_status" -ne 0 ]]; then return "$evidence_status"; fi
    run_logged "$name report" "$results/report.log" dotnet msbuild scripts/dotnet-verification.proj \
        -t:ValidateReport "-p:Report=$results/results.xml" "-p:ExpectedAssembly=$name" -nologo
    cat "$results/report.log"
}

echo "Verifying with .NET SDK $sdk_version (Release)."
run_logged "Locked restore" "$verification_results/restore.log" dotnet restore Parchwell.slnx --locked-mode
run_logged "Solution build" "$verification_results/build.log" dotnet build Parchwell.slnx --configuration Release --no-restore
run_logged "Kafka XML inventory consistency" "$verification_results/kafka-documentation.log" \
    dotnet msbuild scripts/dotnet-verification.proj -t:ValidateKafkaDocumentation -nologo
cat "$verification_results/kafka-documentation.log"

shopt -s globstar nullglob
projects=(src/**/*.csproj tests/**/*.csproj)
for project in "${projects[@]}"; do
    manifest="$verification_results/manifests/$(basename "${project%.csproj}").json"
    if dotnet msbuild "$project" -p:Configuration=Release -t:AddImplicitDefineConstants,_DisableDiagnosticTracing \
        -getProperty:MSBuildProjectFullPath,TargetFramework,Nullable,TreatWarningsAsErrors,AnalysisLevel,EnableNETAnalyzers,RunAnalyzersDuringBuild,RunAnalyzers,AssemblyName,RootNamespace,ProjectDir,DefineConstants,LangVersion,EnforceCodeStyleInBuild \
        -getItem:ProjectReference,Compile,CompilerVisibleProperty -nologo >"$manifest" 2>"$manifest.stderr"; then
        :
    else
        status=$?
        cat "$manifest" "$manifest.stderr" >&2
        exit "$status"
    fi
done
echo "Effective Release project settings and references: collected."
run_tests tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj Parchwell.ArchitectureTests

run_logged "Unit scaffold inventory" "$verification_results/scaffolds.log" dotnet msbuild scripts/dotnet-verification.proj \
    -t:SelectTests "-p:ManifestDirectory=$verification_results/manifests" "-p:ProjectList=$verification_results/unit-projects.txt" -nologo
cat "$verification_results/scaffolds.log"
while IFS= read -r project; do
    run_tests "$project" "$(basename "${project%.csproj}")"
done < "$verification_results/unit-projects.txt"

warning_fixture="$verification_results/warning-control"
mkdir -p "$warning_fixture"
cp tests/verification-fixtures/warnings/WarningControl.csproj.fixture "$warning_fixture/WarningControl.csproj"
cp tests/verification-fixtures/warnings/Allowed.cs.fixture "$warning_fixture/Allowed.cs"
cp tests/verification-fixtures/warnings/Forbidden.cs.fixture "$warning_fixture/Forbidden.cs"
run_logged "Warning control restore" "$warning_fixture/restore.log" dotnet restore "$warning_fixture/WarningControl.csproj" \
    "-p:ParchwellRepositoryRoot=$repository_root" "-p:ControlSource=$warning_fixture/Allowed.cs"
run_logged "Warning control valid counterpart" "$warning_fixture/allowed.log" dotnet build "$warning_fixture/WarningControl.csproj" \
    --configuration Release --no-restore "-p:ParchwellRepositoryRoot=$repository_root" "-p:ControlSource=$warning_fixture/Allowed.cs"
if dotnet build "$warning_fixture/WarningControl.csproj" --configuration Release --no-restore \
    "-p:ParchwellRepositoryRoot=$repository_root" "-p:ControlSource=$warning_fixture/Forbidden.cs" >"$warning_fixture/forbidden.log" 2>&1; then
    echo "Warning control failed: the intentional compiler warning was accepted." >&2
    exit 1
else
    status=$?
    expected_diagnostic="error CS1030: #warning: 'PARCHWELL_WARNING_CONTROL'"
    if [[ "$status" -ne 1 || "$(<"$warning_fixture/forbidden.log")" != *"$expected_diagnostic"* ]]; then
        echo "Warning control failed for an unrelated cause (exit $status)." >&2
        tail -n 80 "$warning_fixture/forbidden.log" >&2
        exit "$status"
    fi
    while IFS= read -r diagnostic || [[ -n "$diagnostic" ]]; do
        if [[ "$diagnostic" =~ (^|[[:space:]])[Ee][Rr][Rr][Oo][Rr][[:space:]] && "$diagnostic" != *"$expected_diagnostic"* ]]; then
            echo "Warning control included an unrelated error (exit $status)." >&2
            tail -n 80 "$warning_fixture/forbidden.log" >&2
            exit "$status"
        fi
    done < "$warning_fixture/forbidden.log"
fi
echo "Warning-as-error control: valid counterpart built; intended CS1030 error rejected."
echo "Verification passed. Per-project reports identify the tests executed."

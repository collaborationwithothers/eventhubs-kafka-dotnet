#!/usr/bin/env bash
set -euo pipefail

repository_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repository_root"
coverage_tool=${1:?Pass the path to the standalone dotnet-coverage executable.}
coverage_version=$("$coverage_tool" --version)
if [[ "${coverage_version%%+*}" != "18.12.0" ]]; then
    echo "Expected standalone dotnet-coverage 18.12.0." >&2
    exit 1
fi
coverage_results=$(mktemp -d "${TMPDIR:-/tmp}/parchwell-kafka-coverage.XXXXXXXX")
trap 'echo "Kafka coverage reports: $coverage_results"' EXIT

"$coverage_tool" collect --settings scripts/kafka-coverage.config \
    --output-format cobertura --output "$coverage_results/coverage.xml" \
    dotnet test --project tests/Parchwell.Kafka.Tests/Parchwell.Kafka.Tests.csproj \
    --configuration Release --no-build --minimum-expected-tests 1 \
    --results-directory "$coverage_results" -- \
    --filter-namespace Parchwell.Kafka.Tests.Configuration \
    --report-xunit-xml --report-xunit-xml-filename results.xml

dotnet msbuild scripts/dotnet-verification.proj -t:ValidateReport \
    "-p:Report=$coverage_results/results.xml" -p:ExpectedAssembly=Parchwell.Kafka.Tests -nologo
dotnet msbuild scripts/dotnet-verification.proj -t:ValidateKafkaCoverage \
    "-p:CoverageReport=$coverage_results/coverage.xml" -nologo

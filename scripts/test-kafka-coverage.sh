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
control_results=$(mktemp -d "${TMPDIR:-/tmp}/parchwell-kafka-coverage-controls.XXXXXXXX")
trap 'echo "Kafka coverage controls: $control_results"' EXIT
fixture="$control_results/fixture"
mkdir "$fixture"
cat > "$fixture/Control.csproj" <<'PROJECT'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>Parchwell.Kafka</AssemblyName>
    <DebugType>portable</DebugType>
    <Optimize>true</Optimize>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
PROJECT
cat > "$fixture/Program.cs" <<'CS'
namespace Parchwell.CoverageControl
{
    internal static class Program
    {
        public static void Main()
        {
            System.Console.WriteLine(Kafka.Configuration.Used.Run());
            System.Console.WriteLine(Kafka.Configuration.Generic<int>.Nested.Run());
        }
    }
}
namespace Parchwell.Kafka.Configuration
{
    internal static class Used
    {
        public static int Run() => 1;
    }
    internal static class Generic<T>
    {
        internal static class Nested
        {
            public static int Run() => 2;
        }
    }
#if CONTROL_UNEXECUTED
    internal static class UnexecutedMapping
    {
        public static int Run() => 3;
    }
#endif
}
namespace Parchwell.Kafka.Authentication
{
    internal static class OutsideMapping
    {
        public static int Run() => 4;
    }
}
CS
assembly="$fixture/bin/Release/net10.0/Parchwell.Kafka.dll"
verify_report() {
    dotnet msbuild scripts/dotnet-verification.proj -t:ValidateKafkaCoverage \
        "-p:CoverageReport=$1" "-p:KafkaAssembly=$assembly" -nologo
}
expect_rejection() {
    local name=$1
    local expected=$2
    shift 2
    if "$@" > "$control_results/$name.log" 2>&1; then
        echo "Coverage control unexpectedly passed: $name" >&2
        exit 1
    fi
    if ! rg -q -- "$expected" "$control_results/$name.log"; then
        cat "$control_results/$name.log" >&2
        echo "Coverage control failed for an unexpected reason: $name" >&2
        exit 1
    fi
    echo "Rejected coverage control: $name"
}
collect_fixture() {
    "$coverage_tool" collect --settings scripts/kafka-coverage.config \
        --output-format cobertura --output "$control_results/$1.xml" \
        dotnet "$assembly" > "$control_results/$1-collector.log" 2>&1
}
dotnet build "$fixture/Control.csproj" --configuration Release -m:1 -nr:false \
    -p:UseSharedCompilation=false -nologo > "$control_results/build-positive.log" 2>&1
collect_fixture positive
verify_report "$control_results/positive.xml" > "$control_results/positive.log" 2>&1
cat "$control_results/positive.log"

python3 - "$control_results" <<'PY'
from copy import deepcopy
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

output = Path(sys.argv[1])
original = ET.parse(output / "positive.xml")
package_path = ".//package[@name='Parchwell.Kafka']"
def write(name, edit):
    tree = deepcopy(original)
    edit(tree)
    tree.write(output / (name + ".xml"))
def mapping_classes(tree):
    return tree.findall(package_path + "/classes/class[@name='Parchwell.Kafka.Configuration.Used']")
def remove_type(tree):
    classes = tree.find(package_path + "/classes")
    classes.remove(mapping_classes(tree)[0])
def clear_type_lines(tree):
    mapping_classes(tree)[0].find("lines").clear()
def add_type(tree, name):
    classes = tree.find(package_path + "/classes")
    added = ET.SubElement(classes, "class", name=name, filename="synthetic.cs")
    ET.SubElement(ET.SubElement(added, "lines"), "line", number="1", hits="0")
write("zero-hit", lambda tree: mapping_classes(tree)[0].find("lines/line").set("hits", "0"))
write("invalid-hit", lambda tree: mapping_classes(tree)[0].find("lines/line").set("hits", "invalid"))
write("invalid-line", lambda tree: mapping_classes(tree)[0].find("lines/line").set("number", "0"))
write("missing-type", remove_type)
write("empty-type", clear_type_lines)
write("new-namespace-type", lambda tree: add_type(tree, "Parchwell.Kafka.Configuration.NewMapping"))
write("outside-namespace", lambda tree: add_type(tree, "Parchwell.Kafka.Authentication.TokenRefresh"))
write("missing-package", lambda tree: tree.find(package_path).set("name", "Other.Assembly"))
write("duplicate-package", lambda tree: tree.find("packages").append(deepcopy(tree.find(package_path))))
(output / "malformed.xml").write_text("<coverage>")
PY

verify_report "$control_results/outside-namespace.xml" > "$control_results/outside-namespace.log" 2>&1
echo "Accepted coverage control: outside-namespace"
for name in zero-hit invalid-hit invalid-line new-namespace-type; do
    expect_rejection "$name" 'Uncovered or invalid mapping line' verify_report "$control_results/$name.xml"
done
for name in missing-type empty-type; do
    expect_rejection "$name" 'Missing coverage for.*Configuration.Used' verify_report "$control_results/$name.xml"
done
for name in missing-package duplicate-package; do
    expect_rejection "$name" 'Expected the instrumented Parchwell.Kafka assembly' verify_report "$control_results/$name.xml"
done
expect_rejection malformed 'Kafka mapping coverage rejected:' verify_report "$control_results/malformed.xml"
expect_rejection missing-report 'Kafka mapping coverage rejected:.*missing.xml' verify_report "$control_results/missing.xml"

# Build a real executable mapping class that the program never invokes. The
# collector must expose it as uncovered or the assembly/PDB inventory must catch
# its omission from the report. This fixture does not alter repository outputs.
cp "$assembly" "$control_results/positive.dll"
cp "${assembly%.dll}.pdb" "$control_results/positive.pdb"
dotnet build "$fixture/Control.csproj" --configuration Release -m:1 -nr:false \
    -p:UseSharedCompilation=false -p:DefineConstants=CONTROL_UNEXECUTED -nologo \
    > "$control_results/build-negative.log" 2>&1
collect_fixture real-unexecuted
expect_rejection real-unexecuted 'Missing coverage for.*UnexecutedMapping|Uncovered or invalid mapping line' \
    verify_report "$control_results/real-unexecuted.xml"

cp "${assembly%.dll}.pdb" "$control_results/negative.pdb"
cp "$control_results/positive.pdb" "${assembly%.dll}.pdb"
expect_rejection mismatched-pdb 'portable PDB does not match' verify_report "$control_results/real-unexecuted.xml"
cp "$control_results/negative.pdb" "${assembly%.dll}.pdb"
mv "${assembly%.dll}.pdb" "$control_results/removed.pdb"
expect_rejection missing-pdb 'Kafka mapping coverage rejected:.*Parchwell.Kafka.pdb' verify_report "$control_results/real-unexecuted.xml"
echo "Kafka coverage regression controls passed."

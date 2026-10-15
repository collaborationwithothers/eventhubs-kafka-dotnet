#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repo_root"

command -v dotnet >/dev/null || { echo "dotnet is required." >&2; exit 1; }
command -v python3 >/dev/null || { echo "python3 is required for XML regression controls." >&2; exit 1; }

python3 - "$repo_root" "${1:-src/Parchwell.Kafka/bin/Release/net10.0/Parchwell.Kafka.dll}" <<'PY'
import copy
import pathlib
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

repo = pathlib.Path(sys.argv[1])
assembly = pathlib.Path(sys.argv[2]).resolve()
documentation = assembly.with_suffix(".xml")
if not assembly.is_file() or not documentation.is_file():
    raise SystemExit("Build Parchwell.Kafka in Release before running XML regression controls.")

controls = pathlib.Path(tempfile.mkdtemp(prefix="parchwell-kafka-documentation."))
source = ET.parse(documentation)
prefix = "F:Parchwell.Kafka.Configuration.KafkaSettingKeys."
group_name = prefix + "GroupId"

def setting(tree, name=group_name):
    return next(member for member in tree.getroot().find("members") if member.get("name") == name)

# The group key combines enrichment-consumer and verifier inventory metadata.
# Keep this independent expectation so a union regression cannot validate itself.
group_ids = {link.text for link in setting(source).iter("see")}
if group_ids != {"C12", "C13", "C14"}:
    raise SystemExit("Expected the approved role-union GroupId XML fixture: C12, C13, C14.")

passed = 0
rejected = 0

def verify(name, path, expected_error=None):
    global passed, rejected
    result = subprocess.run([
        "dotnet", "msbuild", str(repo / "scripts/dotnet-verification.proj"),
        "-t:ValidateKafkaDocumentation", "-nologo",
        "-p:KafkaAssembly=" + str(assembly), "-p:DocumentationFile=" + str(path),
    ], cwd=repo, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    (controls / (name + ".log")).write_text(result.stdout)
    if expected_error is None:
        if result.returncode != 0 or "setting XML declarations match catalogue inventory IDs across all roles." not in result.stdout:
            raise RuntimeError(name + " did not pass the real XML checker; see " + str(controls))
        passed += 1
        print("PASS " + name, flush=True)
    else:
        if result.returncode == 0 or "Kafka documentation rejected:" not in result.stdout or expected_error not in result.stdout:
            raise RuntimeError(name + " was not rejected for the intended reason; see " + str(controls))
        rejected += 1
        print("REJECT " + name, flush=True)

def mutated(name, change, expected_error=None):
    tree = copy.deepcopy(source)
    change(tree)
    path = controls / (name + ".xml")
    tree.write(path, encoding="utf-8", xml_declaration=True)
    verify(name, path, expected_error)

verify("generated-role-union", documentation)

def reverse_inventory_links(tree):
    remarks = setting(tree).find("remarks")
    children = list(remarks)
    for child in children:
        remarks.remove(child)
    remarks.extend(reversed(children))

mutated("reordered-role-union", reverse_inventory_links)
verify("missing-file", controls / "missing.xml", "Missing XML documentation:")
malformed = controls / "malformed.xml"
malformed.write_text("<doc>")
verify("malformed-xml", malformed, "Line 1, position")
dtd = controls / "dtd.xml"
dtd.write_text('<!DOCTYPE doc [<!ENTITY fixture "synthetic">]><doc/>')
verify("prohibited-dtd", dtd, "DTD is prohibited")
mutated("wrong-assembly", lambda tree: setattr(tree.getroot().find("assembly/name"), "text", "Other.Assembly"),
        "Expected generated Parchwell.Kafka XML documentation.")
mutated("missing-members", lambda tree: tree.getroot().remove(tree.getroot().find("members")),
        "Expected generated Parchwell.Kafka XML documentation.")
mutated("missing-declaration", lambda tree: tree.getroot().find("members").remove(setting(tree)),
        "Missing or duplicate XML declaration for group.id.")
mutated("duplicate-declaration", lambda tree: tree.getroot().find("members").append(copy.deepcopy(setting(tree))),
        "Missing or duplicate XML declaration for group.id.")
mutated("wrong-key", lambda tree: setattr(setting(tree).find("summary/c"), "text", "synthetic.wrong.key"),
        "XML summary names the wrong key for group.id.")
mutated("missing-summary", lambda tree: setting(tree).remove(setting(tree).find("summary")),
        "XML summary names the wrong key for group.id.")
mutated("wrong-inventory-id", lambda tree: setattr(next(setting(tree).iter("see")), "text", "A01"),
        "XML inventory IDs disagree with catalogue for group.id.")
mutated("missing-role-union-id", lambda tree: setting(tree).find("remarks").remove(next(setting(tree).iter("see"))),
        "XML inventory IDs disagree with catalogue for group.id.")
mutated("duplicate-inventory-id", lambda tree: setting(tree).find("remarks").append(copy.deepcopy(next(setting(tree).iter("see")))),
        "XML inventory IDs disagree with catalogue for group.id.")
mutated("wrong-inventory-link", lambda tree: next(setting(tree).iter("see")).set("href", "https://example.invalid/synthetic"),
        "XML inventory IDs disagree with catalogue for group.id.")

def add_extra_declaration(tree):
    extra = copy.deepcopy(setting(tree))
    extra.set("name", prefix + "SyntheticUnexpectedKey")
    tree.getroot().find("members").append(extra)

mutated("extra-setting-declaration", add_extra_declaration, "Unexpected XML setting declaration.")
print(f"XML controls: {passed} accepted, {rejected} rejected for the intended reason.")
print("Synthetic XML fixtures and checker logs: " + str(controls))
PY

"""Report this issue's formatted native test declarations and captured results.

Inputs are the two canonical main.tftest.hcl files and their -no-color logs.
This reports counts; Terraform's exit status and assertions decide success.
"""
import argparse
import json
import re
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--test-source-commit", required=True)
args = parser.parse_args()
evidence = Path(__file__).resolve().parent
repository = evidence.parents[3]
roots = {}
for layer in ("persistent", "ephemeral"):
    definitions = (repository / "tests/terraform" / layer / "main.tftest.hcl").read_text()
    captured = (evidence / "raw" / f"{layer}-test.log").read_text()
    declared = re.findall(r'^run "([^"\n]+)" \{', definitions, re.MULTILINE)
    reported = re.findall(r'run "([^"\n]+)"\.\.\. (pass|fail|skip)', captured)
    summaries = re.findall(r"Success! ([0-9]+) passed, ([0-9]+) failed\.", captured)
    roots[layer] = {
        "declared_runs": declared,
        "reported_runs": [{"name": name, "status": status} for name, status in reported],
        "declaration_count": len(declared),
        "reported_count": len(reported),
        "assertion_count": len(re.findall(r"^\s*assert \{", definitions, re.MULTILINE)),
        "passed": sum(status == "pass" for _, status in reported),
        "failed": sum(status == "fail" for _, status in reported),
        "skipped": sum(status == "skip" for _, status in reported),
        "native_success_summary": list(map(int, summaries[-1])) if summaries else None,
    }
result = {
    "test_source_commit": args.test_source_commit,
    "roots": roots,
    "total_reported": sum(row["reported_count"] for row in roots.values()),
    "total_passed": sum(row["passed"] for row in roots.values()),
    "total_failed": sum(row["failed"] for row in roots.values()),
    "total_skipped": sum(row["skipped"] for row in roots.values()),
}
(evidence / "raw/summary.json").write_text(json.dumps(result, indent=2) + "\n")

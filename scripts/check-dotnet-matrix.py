#!/usr/bin/env python3
"""Aggregate check for the hosted .NET verification matrix.

Usage: check-dotnet-matrix.py <reports directory> <job>=<result> [<job>=<result>...]

Exit 0 when there is no finding, 1 when there is at least one, 2 for a usage error.
Each finding is one line on stderr: check-dotnet-matrix: <code>: <detail>.
"""
import json
import os
import re
import sys

PREFIX = "check-dotnet-matrix"
USAGE = "usage: check-dotnet-matrix.py <reports directory> <job>=<result>..."
ARTIFACT_NAME = re.compile(r"^dotnet-verification-(.+)-attempt-([1-9][0-9]*)$")
ARCHITECTURE_TESTS = "Parchwell.ArchitectureTests"
REQUIRED_FILES = ("summary.json", "stage-timing.tsv", "unit-projects.txt", "results.xml")


def parse_jobs(arguments):
    """Returns (job, result) pairs in argument order, or None for a usage error."""
    if not arguments:
        return None
    jobs = []
    seen = set()
    for argument in arguments:
        job, separator, result = argument.partition("=")
        if not separator or not job or job in seen:
            return None
        seen.add(job)
        jobs.append((job, result))
    return jobs


def read_inventory(path):
    names = set()
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.rstrip()
            if not line:
                continue
            name = line.rsplit("/", 1)[-1]
            if name.endswith(".csproj"):
                name = name[: -len(".csproj")]
            names.add(name)
    return names


def is_int(value, expected):
    return type(value) is int and value == expected


def first_bad_summary_key(summary, project):
    checks = (
        ("schema_version", lambda v: is_int(v, 1)),
        ("tier", lambda v: type(v) is str and v == "all"),
        ("projects", lambda v: type(v) is list and v == [project]),
        ("exit_code", lambda v: is_int(v, 0)),
        ("outcome", lambda v: type(v) is str and v == "partial_passed"),
        ("failed_stages", lambda v: type(v) is list and v == []),
    )
    for key, predicate in checks:
        if key not in summary or not predicate(summary[key]):
            return key
    return None


def has_passed_stage(stages, label):
    return any(
        isinstance(stage, dict) and stage.get("label") == label and is_int(stage.get("exit_code"), 0)
        for stage in stages
    )


def main(argv):
    jobs = parse_jobs(argv[2:])
    if jobs is None:
        print(USAGE, file=sys.stderr)
        return 2
    return run(argv[1], jobs)


def run(reports, jobs):
    findings = []
    for job, result in jobs:
        if result != "success":
            findings.append(f"job-result: {job} is '{result}', expected 'success'")

    entries = sorted(os.listdir(reports)) if os.path.isdir(reports) else []
    if not entries:
        findings.append(f"no-artifacts: {reports}")

    latest = {}
    for entry in entries:
        match = ARTIFACT_NAME.match(entry) if os.path.isdir(os.path.join(reports, entry)) else None
        if not match:
            findings.append(f"bad-artifact-name: {entry}")
            continue
        project, attempt = match.group(1), int(match.group(2))
        if project not in latest or attempt > latest[project][0]:
            latest[project] = (attempt, os.path.join(reports, entry))

    inventories = {}
    for project in sorted(latest):
        path = os.path.join(latest[project][1], "unit-projects.txt")
        if os.path.isfile(path):
            inventories[project] = read_inventory(path)
    reference = inventories[min(inventories)] if inventories else set()

    for project in sorted(latest):
        directory = latest[project][1]
        for name in REQUIRED_FILES:
            if not os.path.isfile(os.path.join(directory, name)):
                findings.append(f"missing-file: {project}/{name}")
        if not os.path.isfile(os.path.join(directory, "leg-passed")):
            findings.append(f"leg-not-passed: {project}")

        summary_path = os.path.join(directory, "summary.json")
        if os.path.isfile(summary_path):
            try:
                with open(summary_path, encoding="utf-8") as handle:
                    summary = json.load(handle)
            except (ValueError, OSError):
                summary = None
            if not isinstance(summary, dict):
                findings.append(f"bad-summary: {project}: not a JSON object")
            else:
                bad_key = first_bad_summary_key(summary, project)
                if bad_key:
                    findings.append(f"bad-summary: {project}: {bad_key}")
                stages = summary.get("stages")
                if not (
                    isinstance(stages, list)
                    and has_passed_stage(stages, f"{project} tests")
                    and has_passed_stage(stages, f"{project} report")
                ):
                    findings.append(f"report-stage: {project}")

        if project in inventories and inventories[project] != reference:
            findings.append(f"inventory-mismatch: {project}")

    expected = sorted({ARCHITECTURE_TESTS} | reference)
    for name in expected:
        if name not in latest:
            findings.append(f"missing-project: {name}")
    for project in sorted(latest):
        if project not in expected:
            findings.append(f"extra-project: {project}")

    if findings:
        for finding in findings:
            print(f"{PREFIX}: {finding}", file=sys.stderr)
        return 1
    print(f"{PREFIX}: {len(expected)} test projects passed with validated reports: {', '.join(expected)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

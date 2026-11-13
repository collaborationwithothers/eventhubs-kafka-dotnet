#!/usr/bin/env python3
"""Aggregate check for the hosted .NET verification matrix.

Usage: check-dotnet-matrix.py <reports directory> <shard file> <job>=<result> [<job>=<result>...]

A unit is what one matrix job verifies: a whole test project, or one shard of it named in the shard
file (scripts/dotnet-test-shards.tsv). Each unit's artifact is dotnet-verification-<unit>-attempt-<n>,
where a shard unit is <project>--<shard>.

Exit 0 when there is no finding, 1 when there is at least one, 2 for a usage error.
Each finding is one line on stderr: check-dotnet-matrix: <code>: <detail>.
"""
import json
import os
import re
import sys
import xml.etree.ElementTree as ElementTree

PREFIX = "check-dotnet-matrix"
USAGE = "usage: check-dotnet-matrix.py <reports directory> <shard file> <job>=<result>..."
ARTIFACT_NAME = re.compile(r"^dotnet-verification-(.+)-attempt-([1-9][0-9]*)$")
ARCHITECTURE_TESTS = "Parchwell.ArchitectureTests"
REQUIRED_FILES = ("summary.json", "stage-timing.tsv", "unit-projects.txt", "results.xml", "methods.json")
SHARD_PROJECT = re.compile(r"Parchwell\.[A-Za-z0-9.]+")
SHARD_NAME = re.compile(r"[a-z0-9]+(-[a-z0-9]+)*")
REST = "rest"


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


def first_bad_summary_key(summary, project, shard):
    checks = (
        ("schema_version", lambda v: is_int(v, 1)),
        ("tier", lambda v: type(v) is str and v == "all"),
        ("projects", lambda v: type(v) is list and v == [project]),
        ("shard", lambda v: v is None if shard is None else type(v) is str and v == shard),
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


def read_shard_file(path):
    """Reads the shard file. Returns (entries, bad_lines, readable).

    entries is a list of (line number, project, shard, class) for the valid lines. bad_lines are the
    numbers of the lines that break the format. A line that is empty, or starts with '#', is ignored.
    """
    try:
        with open(path, encoding="utf-8", newline="") as handle:
            text = handle.read()
    except (OSError, UnicodeError):
        return [], [], False
    entries = []
    bad_lines = []
    seen = set()
    for number, line in enumerate(text.split("\n"), start=1):
        if line.endswith("\r"):
            line = line[:-1]
        if not line or line.startswith("#"):
            continue
        fields = line.split("\t")
        valid = len(fields) == 3
        if valid:
            project, shard, class_name = fields
            valid = (
                SHARD_PROJECT.fullmatch(project) is not None
                and SHARD_NAME.fullmatch(shard) is not None
                and shard != REST
                and class_name.startswith(project + ".")
                and " " not in class_name
                and (project, class_name) not in seen
            )
        if not valid:
            bad_lines.append(number)
            continue
        seen.add((project, class_name))
        entries.append((number, project, shard, class_name))
    return entries, bad_lines, True


def split_unit(unit):
    """Returns (project, shard) for a unit string, or None when the name is malformed."""
    if "--" not in unit:
        return unit, None
    project, shard = unit.split("--", 1)
    if not project or not (shard == REST or SHARD_NAME.fullmatch(shard)):
        return None
    return project, shard


def read_methods(path):
    """Returns the set of listed methods, or None when methods.json is not a valid listing."""
    try:
        with open(path, encoding="utf-8") as handle:
            data = json.load(handle)
    except (ValueError, OSError):
        return None
    if not isinstance(data, list) or not all(type(item) is str for item in data) or len(set(data)) != len(data):
        return None
    return set(data)


def read_results(path):
    """Returns (method strings, types) of the <test> elements, or None when results.xml does not parse."""
    try:
        root = ElementTree.parse(path).getroot()
    except (ElementTree.ParseError, OSError, ValueError):
        return None
    methods = set()
    types = set()
    for element in root.iter("test"):
        type_name = element.get("type")
        method = element.get("method")
        if type_name is None or method is None:
            continue
        methods.add(f"{type_name}.{method}")
        types.add(type_name)
    return methods, types


def main(argv):
    jobs = parse_jobs(argv[3:])
    if jobs is None:
        print(USAGE, file=sys.stderr)
        return 2
    return run(argv[1], argv[2], jobs)


def run(reports, shard_file, jobs):
    job_findings = []
    for job, result in jobs:
        if result != "success":
            job_findings.append(f"job-result: {job} is '{result}', expected 'success'")

    entries = sorted(os.listdir(reports)) if os.path.isdir(reports) else []
    no_artifacts = [f"no-artifacts: {reports}"] if not entries else []

    name_findings = []
    latest = {}
    for entry in entries:
        match = ARTIFACT_NAME.match(entry) if os.path.isdir(os.path.join(reports, entry)) else None
        parts = split_unit(match.group(1)) if match else None
        if not match or parts is None:
            name_findings.append(f"bad-artifact-name: {entry}")
            continue
        unit, attempt = match.group(1), int(match.group(2))
        if unit not in latest or attempt > latest[unit][0]:
            latest[unit] = (attempt, os.path.join(reports, entry), parts[0], parts[1])

    inventories = {}
    for unit in sorted(latest):
        path = os.path.join(latest[unit][1], "unit-projects.txt")
        if os.path.isfile(path):
            inventories[unit] = read_inventory(path)
    reference = inventories[min(inventories)] if inventories else set()
    expected = sorted({ARCHITECTURE_TESTS} | reference)

    shard_entries, bad_lines, readable = read_shard_file(shard_file)
    shard_findings = []
    if not readable:
        shard_findings.append(f"bad-shard-file: {shard_file}: unreadable")
    else:
        bad = set(bad_lines)
        for number, project, _, _ in shard_entries:
            if project not in expected:
                bad.add(number)
        shard_findings = [f"bad-shard-file: {shard_file}: line {number}" for number in sorted(bad)]
        if bad:
            shard_entries = []
    if not readable:
        shard_entries = []

    shard_order = {}
    shard_classes = {}
    project_classes = {}
    for _, project, shard, class_name in shard_entries:
        if shard not in shard_order.setdefault(project, []):
            shard_order[project].append(shard)
        shard_classes.setdefault((project, shard), []).append(class_name)
        project_classes.setdefault(project, []).append(class_name)

    unit_findings = []
    unit_data = {}
    for unit in sorted(latest):
        _, directory, project, shard = latest[unit]
        findings = unit_findings
        for name in REQUIRED_FILES:
            if not os.path.isfile(os.path.join(directory, name)):
                findings.append(f"missing-file: {unit}/{name}")
        if not os.path.isfile(os.path.join(directory, "leg-passed")):
            findings.append(f"leg-not-passed: {unit}")

        summary_path = os.path.join(directory, "summary.json")
        if os.path.isfile(summary_path):
            try:
                with open(summary_path, encoding="utf-8") as handle:
                    summary = json.load(handle)
            except (ValueError, OSError):
                summary = None
            if not isinstance(summary, dict):
                findings.append(f"bad-summary: {unit}: not a JSON object")
            else:
                bad_key = first_bad_summary_key(summary, project, shard)
                if bad_key:
                    findings.append(f"bad-summary: {unit}: {bad_key}")
                stages = summary.get("stages")
                if not (
                    isinstance(stages, list)
                    and has_passed_stage(stages, f"{project} tests")
                    and has_passed_stage(stages, f"{project} report")
                    and has_passed_stage(stages, f"{project} test list")
                ):
                    findings.append(f"report-stage: {unit}")

        methods = None
        methods_path = os.path.join(directory, "methods.json")
        if os.path.isfile(methods_path):
            methods = read_methods(methods_path)
            if methods is None:
                findings.append(f"bad-methods: {unit}")
        results = None
        results_path = os.path.join(directory, "results.xml")
        if os.path.isfile(results_path):
            results = read_results(results_path)
            if results is None:
                findings.append(f"bad-results: {unit}")
        unit_data[unit] = (methods, results)

        if methods is not None and results is not None:
            for method in sorted(results[0] - methods):
                findings.append(f"unlisted-test: {unit}: {method}")
        if results is not None and project in shard_order and shard is not None:
            if shard == REST:
                own = None
                outside = sorted(results[1] & set(project_classes[project]))
            elif shard in shard_order[project]:
                own = shard_classes[(project, shard)]
                outside = sorted(results[1] - set(own))
            else:
                own, outside = [], []
            for type_name in outside:
                findings.append(f"class-outside-shard: {unit}: {type_name}")
            for class_name in own or []:
                if class_name not in results[1]:
                    findings.append(f"shard-class-not-run: {unit}: {class_name}")

        if unit in inventories and inventories[unit] != reference:
            findings.append(f"inventory-mismatch: {unit}")

    expected_units = {}
    for project in expected:
        if project in shard_order:
            expected_units[project] = [f"{project}--{shard}" for shard in shard_order[project] + [REST]]
        else:
            expected_units[project] = [project]

    completeness = []
    for project in expected:
        units = expected_units[project]
        if not all(
            unit in unit_data and unit_data[unit][0] is not None and unit_data[unit][1] is not None for unit in units
        ):
            continue
        listings = [unit_data[unit][0] for unit in units]
        if any(listing != listings[0] for listing in listings):
            completeness.append(f"methods-mismatch: {project}")
            continue
        counts = {}
        for unit in units:
            for method in unit_data[unit][1][0]:
                counts[method] = counts.get(method, 0) + 1
        for method in sorted(method for method, count in counts.items() if count > 1):
            completeness.append(f"method-in-two-shards: {project}: {method}")
        for method in sorted(listings[0] - set(counts)):
            completeness.append(f"method-not-run: {project}: {method}")

    missing_projects = []
    missing_shards = []
    for project in expected:
        for unit in expected_units[project]:
            if unit not in latest:
                (missing_shards if project in shard_order else missing_projects).append(
                    f"missing-shard: {unit}" if project in shard_order else f"missing-project: {project}"
                )
    missing_shards.sort()
    extra_projects = []
    for project in sorted({latest[unit][2] for unit in latest}):
        if project not in expected:
            extra_projects.append(f"extra-project: {project}")
    unexpected = [
        f"unexpected-artifact: {unit}"
        for unit in sorted(latest)
        if latest[unit][2] in expected and unit not in expected_units[latest[unit][2]]
    ]

    findings = (
        job_findings + no_artifacts + shard_findings + name_findings + unit_findings + completeness
        + missing_projects + missing_shards + extra_projects + unexpected
    )
    if findings:
        for finding in findings:
            print(f"{PREFIX}: {finding}", file=sys.stderr)
        return 1
    print(f"{PREFIX}: {len(expected)} test projects passed with validated reports: {', '.join(expected)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

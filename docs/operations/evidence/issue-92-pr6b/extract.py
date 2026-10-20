#!/usr/bin/env python3
"""Extract bounded, report-bound Stryker and test-discovery evidence.

Usage: extract.py --run-dir PATH [--validation-dir PATH] [--review-dir PATH]

Inputs
------
run-dir/mutation-report.json[.gz]: pinned Stryker JSON report.
run-dir/controller.json: controller receipt, including source/config identities
  and generated mutant count when the controller observed one.
run-dir/tested-source.json: source commit/tree and source SHA-256 inventory.
validation/compile-normal.json and compile-mutation.json: evaluated MSBuild
  Compile inventories, each shaped as {"Items":{"Compile":[...]}}.
validation/discovery/command.log.gz: ordinary `dotnet test --list-tests`
  stdout, with test names following the "The following Tests are available:" line.
validation/mutation-only/results.xml[.gz]: ordinary test execution xUnit XML.

Outputs
-------
run-dir/extraction-summary.json records raw and serialized counts, report and
  tested-source identities, selected-scope checks, score integers, and test
  inventory reconciliation. `score_percent` is display-only; threshold
  comparisons use numerator and denominator. Raw statuses are never rewritten.
run-dir/mutants.json is a sorted array of raw mutant objects augmented with
  normalized `file`, embedded `source`, and `report_sha256`. `location` is
  copied byte-for-value from the report; its columns are Stryker UTF-16 columns.
run-dir/discovered-tests.json records report test metadata, grouped multiplicity
  counts from report, ordinary listing, and XML, plus reconciliation gaps.
review-dir/lines.json is the source-line review authority. Each line has a stable
  `line_key`, exact start line, member IDs, shared initial unresolved disposition,
  review fields, and `variant_reviews`: one `{mutant_id, explanation, evidence}`
  entry per linked mutant. Variant evidence starts empty for review.
review-dir/dispositions.json links each Survived/NoCoverage mutant to exactly
  one line key and preserves its complete span and replacement.

All JSON is UTF-8, indented, sorted by object key, and terminated by a newline.
The script uses only the Python standard library and refuses unsupported or
oversized report populations instead of silently truncating them.
"""

from __future__ import annotations

import argparse
import collections
import gzip
import hashlib
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Any


MAX_MUTANTS = 10000
SELECTED_PATTERNS = (
    "VerifyRun/Judgement/*.cs",
    "VerifyRun/Window/*.cs",
    "VerifyRun/A01/A01EvidenceRecorder.cs",
    "ProbeGate/ProbeGateEvaluator.cs",
)
SCORED_STATUSES = ("Killed", "Timeout", "Survived", "NoCoverage")
SURVIVOR_STATUSES = ("Survived", "NoCoverage")
METHOD_SUFFIX = re.compile(r"^(?P<type>.+)\.(?P<method>[^.]+)$")


class ExtractionError(Exception):
    pass


def json_bytes(value: Any) -> bytes:
    return (json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + "\n").encode("utf-8")


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(json_bytes(value))


def read_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def find_one(directory: Path, candidates: tuple[str, ...]) -> Path:
    for candidate in candidates:
        path = directory / candidate
        if path.is_file():
            return path
    raise ExtractionError(f"required input missing; tried: {', '.join(str(directory / x) for x in candidates)}")


def load_report(path: Path) -> tuple[dict[str, Any], str, str, int, int]:
    raw = path.read_bytes()
    try:
        payload = gzip.decompress(raw) if path.name.endswith(".gz") else raw
        report = json.loads(payload)
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ExtractionError(f"cannot decode Stryker report {path}: {exc}") from exc
    if not isinstance(report, dict) or not isinstance(report.get("files"), dict):
        raise ExtractionError("unsupported Stryker report shape: expected object with files")
    return report, hashlib.sha256(payload).hexdigest(), hashlib.sha256(raw).hexdigest(), len(raw), len(payload)


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def normalize_source(path: str, project_root: str) -> str:
    normalized = path.replace("\\", "/")
    root = project_root.replace("\\", "/").rstrip("/")
    if root and normalized.startswith(root + "/"):
        normalized = normalized[len(root) + 1 :]
    return normalized


def selected(path: str) -> bool:
    if path in SELECTED_PATTERNS[2:]:
        return True
    for pattern in SELECTED_PATTERNS[:2]:
        parent = pattern.removesuffix("*.cs")
        if path.startswith(parent) and "/" not in path[len(parent):] and path.endswith(".cs"):
            return True
    return False


def status_sort_key(status: str) -> tuple[int, str]:
    order = {name: index for index, name in enumerate(("Killed", "Timeout", "Survived", "NoCoverage", "CompileError", "RuntimeError", "Ignored"))}
    return (order.get(status, len(order)), status)


def raw_test_groups(test_files: Any, project_root: str) -> tuple[list[dict[str, Any]], dict[tuple[str, str, str], int], dict[str, str]]:
    if not isinstance(test_files, dict):
        raise ExtractionError("unsupported Stryker testFiles shape: expected object")
    records: list[dict[str, Any]] = []
    grouped: collections.Counter[tuple[str, str, str]] = collections.Counter()
    type_to_source: dict[str, str] = {}
    for raw_path, metadata in sorted(test_files.items()):
        if not isinstance(metadata, dict) or not isinstance(metadata.get("tests"), list):
            raise ExtractionError(f"unsupported Stryker test file entry: {raw_path}")
        source_file = normalize_source(raw_path, project_root)
        for test in metadata["tests"]:
            name = str(test.get("name", ""))
            # Parameter signatures can themselves contain dots, so remove the
            # suffix before splitting the fully qualified type and method.
            canonical_name = name.split("(", 1)[0]
            match = METHOD_SUFFIX.match(canonical_name)
            if not match:
                records.append({"source_file": source_file, "name": name, "group": None, "raw": test})
                continue
            key = (source_file, match.group("type"), match.group("method"))
            grouped[key] += 1
            type_to_source[match.group("type")] = source_file
            records.append({"source_file": source_file, "name": name,
                            "group": {"source_file": source_file, "type": key[1], "method": key[2]},
                            "raw": test})
    return records, dict(grouped), type_to_source


def ordinary_test_names(path: Path) -> list[str]:
    if path.name.endswith(".gz"):
        text = gzip.decompress(path.read_bytes()).decode("utf-8", errors="replace")
    else:
        text = path.read_text(encoding="utf-8", errors="replace")
    names: list[str] = []
    started = False
    for line in text.splitlines():
        stripped = line.strip()
        if stripped.startswith("The following Tests are available:"):
            started = True
            continue
        if re.match(r"Discovered \d+ tests in assembly\b", stripped):
            started = True
            continue
        if re.fullmatch(r"Discovered [0-9]+ tests\.", stripped):
            continue
        if started and stripped and not stripped.startswith("["):
            names.append(stripped)
    if not started:
        raise ExtractionError(f"ordinary discovery listing has no recognized test header: {path}")
    return names


def key_from_full_name(name: str, type_to_source: dict[str, str]) -> tuple[str, str, str] | None:
    match = METHOD_SUFFIX.match(name.split("(", 1)[0])
    if not match:
        return None
    test_type = match.group("type")
    source = type_to_source.get(test_type)
    if source is None:
        # dotnet list-tests may emit a fully-qualified type or just the type name.
        source = type_to_source.get(test_type.rsplit(".", 1)[-1])
    return (source, test_type, match.group("method")) if source else None


def xml_test_groups(path: Path, type_to_source: dict[str, str]) -> tuple[dict[tuple[str, str, str], int], int, list[dict[str, Any]]]:
    try:
        if path.name.endswith(".gz"):
            root = ET.fromstring(gzip.decompress(path.read_bytes()))
        else:
            root = ET.parse(path).getroot()
    except (ET.ParseError, OSError) as exc:
        raise ExtractionError(f"cannot parse ordinary test XML {path}: {exc}") from exc
    groups: collections.Counter[tuple[str, str, str]] = collections.Counter()
    count = 0
    unmapped: list[dict[str, Any]] = []
    for node in root.iter():
        local_name = node.tag.rsplit("}", 1)[-1]
        if local_name == "test":
            count += 1
            class_name = node.attrib.get("type", "")
            method_name = node.attrib.get("method", "").split("(", 1)[0]
        elif local_name == "UnitTest":
            method = next((n for n in node.iter() if n.tag.rsplit("}", 1)[-1] == "TestMethod"), None)
            if method is None:
                unmapped.append({"name": node.attrib.get("name"), "reason": "UnitTest has no TestMethod"})
                continue
            class_name = method.attrib.get("className", "")
            method_name = method.attrib.get("name", "").split("(", 1)[0]
            count += 1
        else:
            continue
        source = type_to_source.get(class_name) or type_to_source.get(class_name.rsplit(".", 1)[-1])
        if source and class_name and method_name:
            groups[(source, class_name, method_name)] += 1
        else:
            unmapped.append({"name": node.attrib.get("name"), "type": class_name,
                             "method": method_name, "reason": "test identity could not map to a report test source"})
    if count == 0:
        raise ExtractionError(f"ordinary test XML has no test entries: {path}")
    return dict(groups), count, unmapped


def compile_items(path: Path) -> list[dict[str, Any]]:
    data = read_json(path)
    items = data.get("Items", {}).get("Compile") if isinstance(data, dict) else None
    if not isinstance(items, list):
        raise ExtractionError(f"unsupported evaluated Compile inventory: {path}")
    return items


def runner_diagnostics(run_dir: Path) -> tuple[dict[str, Any], dict[str, Any] | None]:
    candidates = (run_dir / "command.log", run_dir / "command.log.gz")
    path = next((candidate for candidate in candidates if candidate.is_file()), None)
    if path is None:
        return {"available": False, "warning_count": None, "warnings": []}, None
    raw = path.read_bytes()
    content = gzip.decompress(raw) if path.name.endswith(".gz") else raw
    warnings = []
    for index, line in enumerate(content.decode("utf-8", errors="replace").splitlines(), start=1):
        lowered = line.lower()
        if "warning" in lowered or "dubious" in lowered or ("assum" in lowered and "framework" in lowered):
            category = "coverage_relay" if "coverage" in lowered or "dubious" in lowered else "runner_or_tool"
            warnings.append({"line": index, "category": category, "text": line})
    return {"available": True, "path": str(path), "warning_count": len(warnings),
            "coverage_relay_warning_count": sum(w["category"] == "coverage_relay" for w in warnings),
            "warnings": warnings}, {"path": str(path), "sha256": hashlib.sha256(raw).hexdigest(), "byte_count": len(raw)}


def reconciliation(report_groups: dict[tuple[str, str, str], int], ordinary_names: list[str],
                   xml_groups: dict[tuple[str, str, str], int], type_to_source: dict[str, str],
                   report_unmapped: list[str], xml_unmapped: list[dict[str, Any]]) -> dict[str, Any]:
    ordinary: collections.Counter[tuple[str, str, str]] = collections.Counter()
    ordinary_unmapped: list[str] = []
    for name in ordinary_names:
        key = key_from_full_name(name, type_to_source)
        if key is None:
            ordinary_unmapped.append(name)
        else:
            ordinary[key] += 1
    # Report and ordinary discovery names can carry parameter signatures.
    # Compare their stable source/type/method identity and case multiplicity.
    report_compact = collections.Counter()
    ordinary_compact = collections.Counter()
    xml_compact = collections.Counter()
    for (source, test_type, method), n in report_groups.items():
        report_compact[(source, test_type, method)] += n
    for (source, test_type, method), n in ordinary.items():
        ordinary_compact[(source, test_type, method)] += n
    for (source, test_type, method), n in xml_groups.items():
        xml_compact[(source, test_type, method)] += n
    all_keys = sorted(set(report_compact) | set(ordinary_compact) | set(xml_compact))
    rows = [{"source_file": k[0], "type": k[1], "method": k[2],
             "report_cases": report_compact[k], "ordinary_cases": ordinary_compact[k],
             "xml_cases": xml_compact[k],
             "reconciled": report_compact[k] == ordinary_compact[k] == xml_compact[k]}
            for k in all_keys]
    return {"grouping_key": ["source_file", "type", "method"], "case_multiplicity_compared": True,
            "report_case_count": sum(report_groups.values()), "ordinary_case_count": sum(ordinary.values()),
            "xml_unit_test_count": sum(xml_groups.values()), "ordinary_unmapped_names": ordinary_unmapped,
            "report_unmapped_names": report_unmapped, "xml_unmapped_tests": xml_unmapped,
            "groups": rows, "all_groups_reconciled": all(row["reconciled"] for row in rows) and
                not report_unmapped and not ordinary_unmapped and not xml_unmapped}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-dir", required=True, type=Path)
    parser.add_argument("--validation-dir", type=Path)
    parser.add_argument("--review-dir", type=Path)
    args = parser.parse_args()
    run_dir = args.run_dir.resolve()
    validation_dir = (args.validation_dir or run_dir.parent / "validation").resolve()
    review_dir = (args.review_dir or run_dir / "review").resolve()
    try:
        report_path = find_one(run_dir, ("mutation-report.json", "mutation-report.json.gz"))
        report, report_sha256, report_file_sha256, report_bytes, report_payload_bytes = load_report(report_path)
        project_root = report.get("projectRoot")
        if not isinstance(project_root, str) or not project_root:
            raise ExtractionError("report projectRoot is missing or not a string")
        raw_mutants: list[dict[str, Any]] = []
        source_by_file: dict[str, str] = {}
        outside_scope: list[dict[str, Any]] = []
        for report_file, metadata in report["files"].items():
            if not isinstance(metadata, dict) or not isinstance(metadata.get("mutants", []), list):
                raise ExtractionError(f"unsupported report file entry: {report_file}")
            normalized = normalize_source(report_file, project_root)
            source = metadata.get("source")
            if isinstance(source, str):
                source_by_file[normalized] = source
            for mutant in metadata.get("mutants", []):
                if len(raw_mutants) >= MAX_MUTANTS:
                    raise ExtractionError(f"serialized mutant record cap exceeded ({MAX_MUTANTS})")
                if not isinstance(mutant, dict) or not isinstance(mutant.get("status"), str):
                    raise ExtractionError(f"unsupported mutant record in {report_file}")
                entry = dict(mutant)
                entry["file"] = normalized
                entry["source"] = source if isinstance(source, str) else None
                entry["report_sha256"] = report_sha256
                raw_mutants.append(entry)
                if mutant["status"] in SCORED_STATUSES and not selected(normalized):
                    outside_scope.append({"file": normalized, "id": mutant.get("id"), "status": mutant["status"]})
        raw_count = len(raw_mutants)
        if raw_count > MAX_MUTANTS:
            raise ExtractionError(f"serialized mutant record cap exceeded ({MAX_MUTANTS})")
        counts = collections.Counter(m["status"] for m in raw_mutants)
        unknown = sorted(status for status in counts if status not in {
            "Killed", "Timeout", "Survived", "NoCoverage", "CompileError", "RuntimeError", "Ignored"})
        score_denominator = sum(counts.get(s, 0) for s in SCORED_STATUSES)
        score_numerator = counts.get("Killed", 0) + counts.get("Timeout", 0)
        score_percent = (score_numerator * 100 / score_denominator) if score_denominator else None
        source_info = read_json(run_dir / "tested-source.json") if (run_dir / "tested-source.json").exists() else {}
        receipt = read_json(run_dir / "controller.json") if (run_dir / "controller.json").exists() else {}
        diagnostics, command_log_hash = runner_diagnostics(run_dir)
        normal_path = validation_dir / "compile-normal.json"
        mutation_path = validation_dir / "compile-mutation.json"
        discovery_path = validation_dir / "discovery" / "command.log.gz"
        xml_path = find_one(validation_dir / "mutation-only", ("results.xml", "results.xml.gz"))
        report_test_records, report_groups, type_to_source = raw_test_groups(report.get("testFiles"), project_root)
        report_unmapped_tests = [record["name"] for record in report_test_records if record["group"] is None]
        ordinary_names = ordinary_test_names(discovery_path)
        xml_groups, xml_count, xml_unmapped_tests = xml_test_groups(xml_path, type_to_source)
        test_reconciliation = reconciliation(report_groups, ordinary_names, xml_groups, type_to_source,
                                             report_unmapped_tests, xml_unmapped_tests)
        example_key = next((key for key in report_groups
                            if key[1].rsplit(".", 1)[-1] == "StartLineHandlerTests" and
                            key[2] == "Given_CleanupNearDeadline_When_Capturing_Then_PublicationRequiresCompletedCleanupWithinBudget"), None)
        example_case_check = {"expected_case_count": 3, "present_in_report": example_key is not None,
                              "report_cases": None, "ordinary_cases": None, "xml_cases": None,
                              "matches_expected": None}
        if example_key is not None:
            exact_type = example_key[1]
            matching_rows = [row for row in test_reconciliation["groups"]
                             if row["source_file"] == example_key[0] and row["type"] == exact_type and row["method"] == example_key[2]]
            if matching_rows:
                row = matching_rows[0]
                example_case_check.update({"source_file": row["source_file"], "type": row["type"], "method": row["method"],
                    "report_cases": row["report_cases"], "ordinary_cases": row["ordinary_cases"],
                    "xml_cases": row["xml_cases"],
                    "matches_expected": row["report_cases"] == row["ordinary_cases"] == row["xml_cases"] == 3})
        normal_items = compile_items(normal_path)
        mutation_items = compile_items(mutation_path)
        normal_sources = sorted({str(item.get("Identity", "")).replace("\\", "/") for item in normal_items})
        mutation_sources = sorted({str(item.get("Identity", "")).replace("\\", "/") for item in mutation_items})
        removed_compile = sorted(set(normal_sources) - set(mutation_sources))
        expected_removed = sorted(source for source in normal_sources if source.startswith("Component/") or source in {
            "Support/ComponentEvidenceBuilder.cs", "Host/VerifyRunHandlerTests.cs", "Host/VerifierHostTests.cs"})
        unexpected_compile_removals = sorted(set(removed_compile) - set(expected_removed))
        expected_compile_not_removed = sorted(set(expected_removed) - set(removed_compile))
        added_compile_sources = sorted(set(mutation_sources) - set(normal_sources))
        # Test source files must be represented in the mutation compile inventory.
        mutation_full_paths = {str(item.get("FullPath", "")).replace("\\", "/") for item in mutation_items}
        report_test_sources = {normalize_source(path, project_root) for path in report.get("testFiles", {})}
        missing_test_sources = sorted(path for path in report_test_sources if path not in mutation_full_paths)

        ordered = sorted(raw_mutants, key=lambda m: (m["file"],
            m.get("location", {}).get("start", {}).get("line", 0),
            m.get("location", {}).get("start", {}).get("column", 0),
            m.get("location", {}).get("end", {}).get("line", 0),
            m.get("location", {}).get("end", {}).get("column", 0),
            str(m.get("mutatorName", "")), str(m.get("id", ""))))
        write_json(run_dir / "mutants.json", ordered)
        discovered = {"schema_version": 1, "report_sha256": report_sha256,
                      "report_test_file_count": len(report.get("testFiles", {})),
                      "report_test_count": len(report_test_records), "report_tests": report_test_records,
                      "ordinary_discovery": {"path": str(discovery_path), "test_count": len(ordinary_names), "names": ordinary_names},
                      "xml_discovery": {"path": str(xml_path), "unit_test_count": xml_count,
                                         "unmapped_tests": xml_unmapped_tests},
                      "reconciliation": test_reconciliation,
                      "approved_example": example_case_check}
        write_json(run_dir / "discovered-tests.json", discovered)

        grouped_lines: dict[tuple[str, int], list[dict[str, Any]]] = collections.defaultdict(list)
        disposition_records = []
        for mutant in ordered:
            if mutant["status"] not in SURVIVOR_STATUSES:
                continue
            loc = mutant.get("location")
            start = loc.get("start") if isinstance(loc, dict) else None
            if not isinstance(start, dict) or not isinstance(start.get("line"), int):
                raise ExtractionError(f"survivor mutant has invalid start line: {mutant.get('id')}")
            file_name = mutant["file"]
            line = start["line"]
            grouped_lines[(file_name, line)].append(mutant)
        lines = []
        line_key_by_member: dict[tuple[str, str], str] = {}
        for (file_name, line), members in sorted(grouped_lines.items()):
            key = f"{report_sha256}:{file_name}:{line}"
            ids = sorted((m.get("id") for m in members), key=str)
            for mutant in members:
                line_key_by_member[(file_name, str(mutant.get("id")))] = key
            lines.append({"line_key": key, "report_sha256": report_sha256, "file": file_name,
                          "start_line": line, "mutant_ids": ids, "category": "unresolved",
                          "reviewed": False, "explanation": "", "evidence": [],
                          "variant_reviews": [{"mutant_id": mutant_id, "explanation": "", "evidence": []}
                                              for mutant_id in ids],
                          "review_started_utc": None, "review_elapsed_seconds": None})
        for mutant in ordered:
            if mutant["status"] not in SURVIVOR_STATUSES:
                continue
            disposition_records.append({"report_sha256": report_sha256, "mutant_id": mutant.get("id"),
                "file": mutant["file"], "span": mutant["location"], "mutator": mutant.get("mutatorName"),
                "replacement": mutant.get("replacement"),
                "line_key": line_key_by_member[(mutant["file"], str(mutant.get("id")))]})
        write_json(review_dir / "lines.json", lines)
        write_json(review_dir / "dispositions.json", disposition_records)

        generated_count = receipt.get("maximum_observed_generated_count",
            receipt.get("generated_mutants", receipt.get("generated_count")))
        if generated_count is None and receipt.get("observed_generated_counts"):
            generated_count = max(receipt["observed_generated_counts"])
        report_receipt_completed = (
            receipt.get("execution_evidence_complete") is True
            and receipt.get("exit_code") == 0
            and receipt.get("exit_reason") == "command-exit"
            and receipt.get("source_inputs_unchanged") is True
            and receipt.get("termination") is None
            and receipt.get("report_completeness") == "pending-one-pass-extraction"
            and isinstance(generated_count, int) and 0 < generated_count <= MAX_MUTANTS
        )
        tested_source_path = run_dir / "tested-source.json"
        tested_source_sha256 = sha256_file(tested_source_path) if tested_source_path.exists() else None
        receipt_report_sha256 = receipt.get("report_sha256")
        receipt_source_manifest_sha256 = receipt.get("source_manifest_sha256")
        receipt_command_log_sha256 = receipt.get("command_log_sha256")
        report_receipt_hash_matches = receipt_report_sha256 == report_sha256
        source_receipt_hash_matches = tested_source_sha256 is not None and receipt_source_manifest_sha256 == tested_source_sha256
        command_log_hash_matches = command_log_hash is not None and receipt_command_log_sha256 == command_log_hash["sha256"]
        hashed_inputs = {"report_bytes": {"path": str(report_path), "sha256": report_file_sha256,
                          "stored_file_sha256": report_file_sha256,
                          "original_report_sha256": report_sha256,
                          "byte_count": report_bytes, "uncompressed_sha256": report_sha256,
                          "uncompressed_byte_count": report_payload_bytes},
            "tested_source": {"path": str(run_dir / "tested-source.json"),
                              "sha256": sha256_file(run_dir / "tested-source.json")} if (run_dir / "tested-source.json").exists() else None,
            "controller_receipt": {"path": str(run_dir / "controller.json"),
                                    "sha256": sha256_file(run_dir / "controller.json")} if (run_dir / "controller.json").exists() else None,
            "compile_normal": {"path": str(normal_path), "sha256": sha256_file(normal_path)},
            "compile_mutation": {"path": str(mutation_path), "sha256": sha256_file(mutation_path)},
            "ordinary_discovery": {"path": str(discovery_path), "sha256": sha256_file(discovery_path)},
            "ordinary_xml": {"path": str(xml_path), "sha256": sha256_file(xml_path)},
            "command_log": command_log_hash}
        summary = {"schema_version": 1, "report_path": str(report_path), "report_sha256": report_sha256,
            "report_file_sha256": report_file_sha256,
            "report_compressed_or_raw_bytes": report_bytes, "report_uncompressed_sha256": report_sha256,
            "input_hashes": hashed_inputs, "report_keys": sorted(report),
            "report_schema_version": report.get("schemaVersion"), "project_root": project_root,
            "tested_source": source_info, "controller_receipt": receipt,
            "runner_diagnostics": diagnostics,
            "receipt_hash_binding": {"report_sha256": receipt_report_sha256,
                "report_hash_matches_uncompressed_report": report_receipt_hash_matches,
                "source_manifest_sha256": receipt_source_manifest_sha256,
                "tested_source_file_sha256": tested_source_sha256,
                "source_hash_matches_tested_source_file": source_receipt_hash_matches,
                "command_log_sha256": receipt_command_log_sha256,
                "command_log_hash_matches": command_log_hash_matches},
            "generated_mutant_count": generated_count, "generated_count_basis": "controller receipt; may be unavailable",
            "serialized_mutant_count": raw_count, "serialized_count_is_not_generated_count": True,
            "status_counts": dict(sorted(counts.items(), key=lambda item: status_sort_key(item[0]))),
            "unknown_statuses": unknown, "unsupported_status_or_incomplete": bool(unknown),
            "score": {"numerator": score_numerator, "denominator": score_denominator,
                      "percent": score_percent, "percent_is_display_only": True,
                      "formula": "(Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage)" if score_denominator else None},
            "selected_patterns": list(SELECTED_PATTERNS), "outside_selected_scope": outside_scope,
            "source_file_count": len(report["files"]), "report_test_file_count": len(report.get("testFiles", {})),
            "report_test_count": len(report_test_records), "ordinary_discovery_count": len(ordinary_names),
            "xml_unit_test_count": xml_count, "test_reconciliation_complete": test_reconciliation["all_groups_reconciled"],
            "approved_three_case_example": example_case_check,
            "compile_inventory": {"normal_count": len(normal_sources), "mutation_count": len(mutation_sources),
                "normal_compile_sources": normal_sources, "mutation_compile_sources": mutation_sources,
                "removed": removed_compile,
                "added": added_compile_sources,
                "expected_removed_by_plan": expected_removed,
                "unexpected_removals": unexpected_compile_removals,
                "expected_files_not_removed": expected_compile_not_removed,
                "exact_plan_reconciliation": not unexpected_compile_removals and not expected_compile_not_removed and not added_compile_sources,
                "report_test_sources_missing_from_mutation_inventory": missing_test_sources,
                "paths": {"normal": str(normal_path), "mutation": str(mutation_path)}},
            "survivor_mutant_count": len(disposition_records), "survivor_line_count": len(lines),
            "survivor_file_count": len({line["file"] for line in lines}),
            "line_review_cap": 512, "line_review_cap_exceeded": len(lines) > 512,
            "report_receipt_completed": report_receipt_completed,
            "approved_example_complete": example_case_check["matches_expected"] is True,
            "extraction_complete": report_receipt_completed and report_receipt_hash_matches and
                source_receipt_hash_matches and command_log_hash_matches and not unknown and not outside_scope and
                test_reconciliation["all_groups_reconciled"] and
                not unexpected_compile_removals and not expected_compile_not_removed and
                not added_compile_sources and not missing_test_sources and
                len(lines) <= 512 and score_denominator > 0 and
                example_case_check["matches_expected"] is True,
            "source_columns": "Stryker UTF-16 columns preserved exactly in mutant locations and disposition spans"}
        write_json(run_dir / "extraction-summary.json", summary)
        if not summary["extraction_complete"]:
            return 2
        return 0
    except (ExtractionError, OSError, ValueError, KeyError, TypeError) as exc:
        print(f"extract.py: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

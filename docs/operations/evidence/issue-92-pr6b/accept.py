#!/usr/bin/env python3
"""Validate retained PR 6b evidence and write acceptance.json.

Expected retained inputs include mutation-full/{controller.json,
tested-source.json, extraction-summary.json, mutants.json, discovered-tests.json,
mutation-report.json[.gz], stryker-config.json}, review/{lines.json,
dispositions.json, replay/selection.json, replay/replay.json},
threshold-decision.json, publication-source.json, and retention.json. The
threshold decision records the authoritative URL, issue13_record_url,
accepted_break, report_sha256, and risk_disposition with exact row IDs for
runner disagreements, unresolved lines, incomplete or compile-failed replays,
uncovered replay files/mutators, and a rationale. The script
does not run Stryker, tests, or replays and does not infer Hari's decision.
"""

from __future__ import annotations

import argparse
import collections
import copy
import gzip
import hashlib
import json
import subprocess
import sys
from pathlib import Path
from typing import Any
import xml.etree.ElementTree as ET


MAX_MUTANTS = 10000
MAX_LINES = 512
MAX_REVIEW_SECONDS = 14400
MAX_REPLAY_IDS = 32
MAX_KILLED_REPLAYS = 4
VALIDATION_SOURCE_DIRS = (
    "baseline-build",
    "baseline-test",
    "discovery",
    "verify-dotnet",
)
ALLOWED_CATEGORIES = {
    "missing test", "equivalent with proof", "preview-runner discrepancy", "unresolved"
}


class EvidenceError(Exception):
    pass


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def git_repository_root(path: Path) -> Path | None:
    try:
        result = subprocess.run(
            ["git", "-C", str(path), "rev-parse", "--show-toplevel"],
            check=True, capture_output=True, text=True,
        )
    except (OSError, subprocess.CalledProcessError):
        return None
    return Path(result.stdout.strip()).resolve()


def valid_git_object_id(value: Any) -> bool:
    return (isinstance(value, str) and len(value) == 40
            and all(character in "0123456789abcdef" for character in value))


def git_commit_tree_matches(repository: Path, commit: Any, expected_tree: Any) -> bool:
    if not valid_git_object_id(commit) or not valid_git_object_id(expected_tree):
        return False
    try:
        resolved_commit = subprocess.run(
            ["git", "-C", str(repository), "rev-parse", "--verify", f"{commit}^{{commit}}"],
            check=True, capture_output=True, text=True,
        ).stdout.strip()
        resolved_tree = subprocess.run(
            ["git", "-C", str(repository), "rev-parse", "--verify", f"{commit}^{{tree}}"],
            check=True, capture_output=True, text=True,
        ).stdout.strip()
    except (OSError, subprocess.CalledProcessError):
        return False
    return resolved_commit == commit and resolved_tree == expected_tree


def selected_report_coverage(
    selected: Any, report_rows: dict[str, tuple[Any, ...]]
) -> tuple[bool, set[str], set[str], set[str], collections.Counter[str]]:
    if not isinstance(selected, list):
        return False, set(), set(), set(), collections.Counter()
    selected_ids = [
        str(record.get("mutant_id", record.get("id")))
        for record in selected if isinstance(record, dict)
    ]
    records_match = len(selected_ids) == len(selected) and len(set(selected_ids)) == len(selected_ids)
    survivor_files: set[str] = set()
    survivor_mutators: set[str] = set()
    killed_ids: set[str] = set()
    statuses: collections.Counter[str] = collections.Counter()
    for record in selected:
        if not isinstance(record, dict):
            records_match = False
            continue
        mutant_id = str(record.get("mutant_id", record.get("id")))
        raw = report_rows.get(mutant_id)
        if raw is None:
            records_match = False
            continue
        status, file, _location, mutator, _replacement = raw
        statuses[str(status)] += 1
        if (record.get("id", record.get("mutant_id")) is None
                or str(record.get("id", record.get("mutant_id"))) != mutant_id
                or record.get("status") != status
                or record.get("file") != file
                or record.get("mutatorName") != mutator):
            records_match = False
        if status in {"Survived", "NoCoverage"}:
            survivor_files.add(str(file))
            survivor_mutators.add(str(mutator))
        elif status == "Killed":
            killed_ids.add(mutant_id)
    return records_match, survivor_files, survivor_mutators, killed_ids, statuses


def read_json(path: Path) -> Any:
    try:
        retained = retained_bytes(path)
        if retained is None:
            raise FileNotFoundError(path)
        return json.loads(retained[0])
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise EvidenceError(f"cannot read JSON {path}: {exc}") from exc


def read_required(path: Path) -> Any:
    return read_json(path)


def checked(condition: bool, name: str, detail: str) -> dict[str, Any]:
    return {"name": name, "passed": bool(condition), "detail": detail}


def add_check(checks: list[dict[str, Any]], outstanding: list[str],
              condition: bool, name: str, detail: str) -> None:
    checks.append(checked(condition, name, detail))
    if not condition:
        outstanding.append(name)


def same_except_break(measured: Any, current: Any) -> bool:
    if not isinstance(measured, dict) or not isinstance(current, dict):
        return False
    before = copy.deepcopy(measured)
    after = copy.deepcopy(current)
    try:
        before["stryker-config"]["thresholds"].pop("break")
        after["stryker-config"]["thresholds"].pop("break")
    except (KeyError, TypeError, AttributeError):
        return False
    return before == after


def report_payload(path: Path) -> tuple[bytes, bytes]:
    stored = path.read_bytes()
    payload = gzip.decompress(stored) if path.name.endswith(".gz") else stored
    return payload, stored


def retained_bytes(base: Path) -> tuple[bytes, Path] | None:
    candidates = (base, base.with_name(base.name + ".gz"))
    for path in candidates:
        if path.is_file():
            data = path.read_bytes()
            return (gzip.decompress(data) if path.name.endswith(".gz") else data), path
    return None


def safe_evidence_path(root: Path, value: str, original_root: Path | None = None) -> Path:
    raw = Path(value)
    if raw.is_absolute():
        target = raw.resolve()
        try:
            target.relative_to(root)
        except ValueError:
            if original_root is None:
                raise EvidenceError(f"retained absolute path is outside evidence root: {value}")
            try:
                relative = target.relative_to(original_root.resolve())
            except ValueError as exc:
                raise EvidenceError(f"retained absolute path is outside the recorded working root: {value}") from exc
            target = (root / relative).resolve()
    else:
        target = (root / raw).resolve()
    try:
        target.relative_to(root)
    except ValueError as exc:
        raise EvidenceError(f"retained path escapes evidence root: {value}") from exc
    return target


def require_mapping(value: Any, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise EvidenceError(f"{label} must be a JSON object")
    return value


def controller_success(path: Path) -> bool:
    record = read_required(path)
    return (record.get("exit_code") == 0 and record.get("termination") is None
            and record.get("source_inputs_unchanged") is True
            and record.get("execution_evidence_complete") is True)


def validation_source_inputs_match(measured_source: Any,
                                   validation_sources: list[Any]) -> bool:
    if not isinstance(measured_source, dict):
        return False
    measured_inputs = measured_source.get("source_input_sha256")
    if not valid_source_input_map(measured_inputs):
        return False
    return bool(validation_sources) and all(
        isinstance(validation_source, dict)
        and valid_source_input_map(validation_source.get("source_input_sha256"))
        and validation_source["source_input_sha256"] == measured_inputs
        for validation_source in validation_sources
    )


def valid_source_input_map(value: Any) -> bool:
    return (isinstance(value, dict) and bool(value)
            and all(isinstance(path, str) and path
                    and isinstance(digest, str) and len(digest) == 64
                    and all(character in "0123456789abcdef" for character in digest)
                    for path, digest in value.items()))


def valid_xunit_report(base: Path) -> bool:
    retained = retained_bytes(base)
    if retained is None:
        return False
    try:
        root = ET.fromstring(retained[0])
    except ET.ParseError:
        return False
    assemblies = [node for node in root.iter() if node.tag.rsplit("}", 1)[-1] == "assembly"]
    return bool(assemblies) and all(
        int(node.attrib.get("passed", "0")) > 0
        and all(int(node.attrib.get(key, "0")) == 0 for key in ("failed", "skipped", "errors", "not-run"))
        for node in assemblies)


def xunit_counts(payload: bytes) -> tuple[int, int, int, int] | None:
    try:
        root = ET.fromstring(payload)
    except ET.ParseError:
        return None
    assemblies = [node for node in root.iter() if node.tag.rsplit("}", 1)[-1] == "assembly"]
    if not assemblies:
        return None
    try:
        total = sum(int(node.attrib.get("total", "0")) for node in assemblies)
        failed = sum(int(node.attrib.get("failed", "0")) for node in assemblies)
        errors = sum(int(node.attrib.get("errors", "0")) for node in assemblies)
        passed = sum(int(node.attrib.get("passed", "0")) for node in assemblies)
    except ValueError:
        return None
    return total, passed, failed, errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence", required=True, type=Path)
    parser.add_argument("--config", required=True, type=Path)
    parser.add_argument("--threshold-decision", required=True)
    args = parser.parse_args()
    root = args.evidence.resolve()
    config_path = args.config.resolve()
    checks: list[dict[str, Any]] = []
    outstanding: list[str] = []

    try:
        run = root / "mutation-full"
        review = root / "review"
        summary = require_mapping(read_required(run / "extraction-summary.json"), "extraction summary")
        mutants = read_required(run / "mutants.json")
        lines = read_required(review / "lines.json")
        dispositions = read_required(review / "dispositions.json")
        discovered = require_mapping(read_required(run / "discovered-tests.json"), "discovered tests")
        controller = require_mapping(read_required(run / "controller.json"), "mutation controller receipt")
        ledger_value = controller.get("invocation_ledger")
        if not isinstance(ledger_value, str):
            raise EvidenceError("full-run controller receipt has no invocation ledger path")
        original_evidence_root = Path(ledger_value).resolve().parent
        source = require_mapping(read_required(run / "tested-source.json"), "tested-source manifest")
        publication = require_mapping(read_required(root / "publication-source.json"), "publication-source manifest")
        measured_config = read_required(run / "stryker-config.json")
        current_config = read_required(config_path)
        decision = require_mapping(read_required(root / "threshold-decision.json"), "threshold decision")

        if not isinstance(mutants, list) or not isinstance(lines, list) or not isinstance(dispositions, list):
            raise EvidenceError("mutants, line review, and dispositions must be JSON arrays")

        report_path = run / "mutation-report.json"
        if not report_path.is_file():
            report_path = run / "mutation-report.json.gz"
        report_bytes_ok = report_path.is_file()
        report_hash = None
        report_file_hash = None
        report_mutant_rows: dict[str, tuple[Any, ...]] = {}
        report_status_counts: collections.Counter[str] = collections.Counter()
        if report_bytes_ok:
            payload, stored = report_payload(report_path)
            report_hash = sha256_bytes(payload)
            report_file_hash = sha256_bytes(stored)
            report = require_mapping(json.loads(payload), "raw Stryker report")
            report_files = require_mapping(report.get("files"), "raw Stryker report files")
            project_root = report.get("projectRoot")
            if not isinstance(project_root, str) or not project_root:
                raise EvidenceError("raw Stryker report projectRoot is missing")
            normalized_root = project_root.replace("\\", "/").rstrip("/")
            for report_file, file_data in report_files.items():
                metadata = require_mapping(file_data, f"raw report file {report_file}")
                normalized_file = str(report_file).replace("\\", "/")
                if normalized_root and normalized_file.startswith(normalized_root + "/"):
                    normalized_file = normalized_file[len(normalized_root) + 1:]
                records = metadata.get("mutants", [])
                if not isinstance(records, list):
                    raise EvidenceError(f"raw report mutants must be an array: {report_file}")
                for mutant in records:
                    record = require_mapping(mutant, "raw mutant")
                    mutant_id = str(record.get("id"))
                    if mutant_id in report_mutant_rows:
                        raise EvidenceError(f"duplicate mutant ID in raw report: {mutant_id}")
                    report_status_counts[str(record.get("status"))] += 1
                    report_mutant_rows[mutant_id] = (record.get("status"), normalized_file,
                        record.get("location"), record.get("mutatorName"), record.get("replacement"))
        expected_report_hash = summary.get("report_sha256")
        add_check(checks, outstanding,
                  report_bytes_ok and report_hash == expected_report_hash == controller.get("report_sha256")
                  and summary.get("report_file_sha256") == report_file_hash
                  and summary.get("report_uncompressed_sha256") == report_hash
                  and summary.get("input_hashes", {}).get("report_bytes", {}).get("original_report_sha256") == report_hash
                  and summary.get("input_hashes", {}).get("report_bytes", {}).get("stored_file_sha256") == report_file_hash,
                  "report-hash-and-source-identity",
                  "Uncompressed report SHA-256 must match controller/extraction identity; retained original and compressed hashes must also match.")
        input_hashes = summary.get("input_hashes", {})
        inventory_hashes_ok = isinstance(input_hashes, dict)
        hashed_paths = {
            "tested_source": run / "tested-source.json",
            "controller_receipt": run / "controller.json",
            "compile_normal": root / "validation" / "compile-normal.json",
            "compile_mutation": root / "validation" / "compile-mutation.json",
            "ordinary_discovery": root / "validation" / "discovery" / "command.log.gz",
        }
        for key, expected_path in hashed_paths.items():
            record = input_hashes.get(key, {}) if isinstance(input_hashes, dict) else {}
            if not expected_path.is_file() or not isinstance(record, dict) or record.get("sha256") != sha256_bytes(expected_path.read_bytes()):
                inventory_hashes_ok = False
        xml_record = input_hashes.get("ordinary_xml", {}) if isinstance(input_hashes, dict) else {}
        xml_retained = retained_bytes(root / "validation" / "mutation-only" / "results.xml")
        if xml_retained is None or not isinstance(xml_record, dict) or xml_record.get("sha256") != sha256_bytes(xml_retained[0]):
            inventory_hashes_ok = False
        add_check(checks, outstanding, inventory_hashes_ok,
                  "discovery-and-inventory-input-hashes", "Test inventories, discovery, XML, controller, and source manifests must match the hashes captured during extraction.")
        add_check(checks, outstanding,
                  summary.get("controller_receipt", {}).get("tested_commit") == source.get("tested_commit")
                  and summary.get("tested_source", {}).get("tested_commit") == source.get("tested_commit")
                  and controller.get("tested_commit") == source.get("tested_commit")
                  and controller.get("source_manifest_sha256") == sha256_bytes(
                      (run / "tested-source.json").read_bytes()),
                  "tested-source-identity", "Controller and extraction must identify the retained source manifest.")
        add_check(checks, outstanding,
                  summary.get("controller_receipt", {}).get("source_inputs_unchanged") is True
                  and controller.get("source_inputs_unchanged") is True,
                  "measured-input-integrity", "Measured source and configuration inputs must remain unchanged during the run.")

        # The config's own hash is in tested-source.json; it is deliberately
        # checked separately from the source-manifest hash above.
        config_identity = source.get("config") if isinstance(source.get("config"), dict) else {}
        config_hash_matches = (config_identity.get("sha256") == sha256_bytes((run / "stryker-config.json").read_bytes()))
        add_check(checks, outstanding, config_hash_matches,
                  "measured-config-hash", "Retained measured config bytes must match the tested-source SHA-256.")
        only_break = same_except_break(measured_config, current_config)
        add_check(checks, outstanding, only_break,
                  "config-diff-only-break", "Current Stryker configuration must differ from measured config only at thresholds.break.")
        measured_inputs = source.get("source_input_sha256", {})
        publication_inputs = publication.get("source_input_sha256", {})
        config_keys = [key for key in measured_inputs if Path(key).name == "stryker-config.json"] if isinstance(measured_inputs, dict) else []
        publication_config = publication.get("config") if isinstance(publication.get("config"), dict) else {}
        publication_config_hash = sha256_bytes(config_path.read_bytes())
        measured_manifest_hash = hashlib.sha256(json.dumps(measured_inputs, sort_keys=True).encode()).hexdigest() if isinstance(measured_inputs, dict) else None
        publication_manifest_hash = hashlib.sha256(json.dumps(publication_inputs, sort_keys=True).encode()).hexdigest() if isinstance(publication_inputs, dict) else None
        repository_root = git_repository_root(config_path.parent)
        measured_git_identity_ok = (repository_root is not None and git_commit_tree_matches(
            repository_root, source.get("tested_commit"), source.get("tested_commit_tree")))
        publication_git_identity_ok = (repository_root is not None and git_commit_tree_matches(
            repository_root, publication.get("tested_commit"), publication.get("tested_commit_tree")))
        publication_source_ok = (
            repository_root is not None
            and isinstance(measured_inputs, dict) and isinstance(publication_inputs, dict)
            and len(config_keys) == 1
            and source.get("source_input_manifest_sha256") == measured_manifest_hash
            and publication.get("source_input_manifest_sha256") == publication_manifest_hash
            and measured_inputs.get(config_keys[0]) == config_identity.get("sha256")
            and publication_inputs.get(config_keys[0]) == publication_config_hash
            and {key: value for key, value in measured_inputs.items() if key != config_keys[0]}
                == {key: value for key, value in publication_inputs.items() if key != config_keys[0]}
            and publication_config.get("sha256") == publication_config_hash
            and publication_config.get("path") == config_identity.get("path")
            and publication.get("tested_commit")
            and publication.get("tested_commit") != source.get("tested_commit")
            and measured_git_identity_ok
            and publication_git_identity_ok
        )
        if isinstance(publication_inputs, dict) and repository_root is not None:
            for relative, expected in publication_inputs.items():
                current_file = (repository_root / relative).resolve()
                try:
                    current_file.relative_to(repository_root.resolve())
                except ValueError:
                    publication_source_ok = False
                    break
                if not current_file.is_file() or sha256_bytes(current_file.read_bytes()) != expected:
                    publication_source_ok = False
                    break
        elif repository_root is None:
            publication_source_ok = False
        add_check(checks, outstanding, publication_source_ok,
                  "publication-source-identity", "Publication head must retain its source manifest and match measured inputs except for the accepted threshold-only config change.")

        status_counts = summary.get("status_counts", {})
        score = summary.get("score", {})
        denominator = score.get("denominator")
        numerator = score.get("numerator")
        raw_score_valid = (isinstance(numerator, int) and not isinstance(numerator, bool)
                           and isinstance(denominator, int) and not isinstance(denominator, bool)
                           and denominator > 0 and 0 <= numerator <= denominator)
        retained_mutant_rows = {str(m.get("id")): m for m in mutants if isinstance(m, dict)}
        retained_counts = collections.Counter(str(m.get("status")) for m in mutants if isinstance(m, dict))
        raw_status_counts = {key: value for key, value in sorted(report_status_counts.items())}
        retained_status_counts = {key: value for key, value in sorted(retained_counts.items())}
        raw_denominator = sum(report_status_counts.get(status, 0) for status in
                              ("Killed", "Timeout", "Survived", "NoCoverage"))
        raw_numerator = report_status_counts.get("Killed", 0) + report_status_counts.get("Timeout", 0)
        raw_mutant_integrity = (
            report_bytes_ok and len(mutants) == len(report_mutant_rows)
            and len(retained_mutant_rows) == len(mutants)
            and retained_status_counts == raw_status_counts
            and all(str(mid) in report_mutant_rows and
                    (m.get("status"), m.get("file"), m.get("location"), m.get("mutatorName"), m.get("replacement"))
                    == report_mutant_rows[str(mid)] and m.get("report_sha256") == report_hash
                    for mid, m in retained_mutant_rows.items()))
        add_check(checks, outstanding,
                  raw_mutant_integrity and numerator == raw_numerator
                  and denominator == raw_denominator and summary.get("status_counts") == raw_status_counts,
                  "raw-report-population-reconciliation", "Mutant rows, statuses, numerator, and denominator must reconcile exactly to the raw report bytes.")
        add_check(checks, outstanding,
                  summary.get("extraction_complete") is True
                  and summary.get("report_receipt_completed") is True
                  and summary.get("approved_example_complete") is True
                  and summary.get("unsupported_status_or_incomplete") is False
                  and summary.get("unknown_statuses") == []
                  and summary.get("outside_selected_scope") == []
                  and raw_score_valid and summary.get("serialized_mutant_count") == len(mutants)
                  and len(mutants) <= MAX_MUTANTS
                  and isinstance(summary.get("generated_mutant_count"), int)
                  and 0 < summary.get("generated_mutant_count") <= MAX_MUTANTS,
                  "complete-bounded-report", "Report must be complete, supported, in scope, and within 10,000 serialized mutants.")
        add_check(checks, outstanding,
                  discovered.get("report_sha256") == report_hash
                  and summary.get("test_reconciliation_complete") is True
                  and discovered.get("reconciliation", {}).get("all_groups_reconciled") is True,
                  "discovery-reconciliation", "Report tests, ordinary discovery, and XML must reconcile by source/type/method and multiplicity.")
        compile_inventory = summary.get("compile_inventory", {})
        add_check(checks, outstanding,
                  compile_inventory.get("exact_plan_reconciliation") is True
                  and compile_inventory.get("unexpected_removals") == []
                  and compile_inventory.get("expected_files_not_removed") == []
                  and compile_inventory.get("added") == []
                  and compile_inventory.get("report_test_sources_missing_from_mutation_inventory") == [],
                  "compile-selection", "Normal and mutation Compile inventories must match the exact approved selection diff with no added or missing files.")

        survivor_mutants = [m for m in mutants if isinstance(m, dict) and m.get("status") in {"Survived", "NoCoverage"}]
        survivor_ids = {str(m.get("id")) for m in survivor_mutants}
        line_ids = {str(mid) for line in lines if isinstance(line, dict) for mid in line.get("mutant_ids", [])}
        disposition_ids = {str(d.get("mutant_id")) for d in dispositions if isinstance(d, dict)}
        lines_by_key = {line.get("line_key"): line for line in lines if isinstance(line, dict)}
        expected_line_members: dict[tuple[str, int], set[str]] = collections.defaultdict(set)
        for mutant in survivor_mutants:
            start = mutant.get("location", {}).get("start", {})
            if isinstance(start.get("line"), int):
                expected_line_members[(str(mutant.get("file")), start["line"])].add(str(mutant.get("id")))
        actual_line_members: dict[tuple[str, int], set[str]] = {}
        line_structure_ok = len(lines_by_key) == len(lines)
        for line in lines:
            if not isinstance(line, dict) or not isinstance(line.get("file"), str) or not isinstance(line.get("start_line"), int):
                line_structure_ok = False
                continue
            key = (line["file"], line["start_line"])
            if key in actual_line_members or len(line.get("mutant_ids", [])) != len(set(map(str, line.get("mutant_ids", [])))):
                line_structure_ok = False
            actual_line_members[key] = set(map(str, line.get("mutant_ids", [])))
            expected_key = f"{report_hash}:{line['file']}:{line['start_line']}"
            if line.get("line_key") != expected_key:
                line_structure_ok = False
        disposition_by_id = {str(d.get("mutant_id")): d for d in dispositions if isinstance(d, dict)}
        disposition_structure_ok = (len(disposition_by_id) == len(dispositions)
            and all((d.get("file"), d.get("span"), d.get("mutator"), d.get("replacement"), d.get("line_key"))
                    == (retained_mutant_rows[mid].get("file"), retained_mutant_rows[mid].get("location"),
                        retained_mutant_rows[mid].get("mutatorName"), retained_mutant_rows[mid].get("replacement"),
                        f"{report_hash}:{retained_mutant_rows[mid].get('file')}:{retained_mutant_rows[mid].get('location', {}).get('start', {}).get('line')}")
                    for mid, d in disposition_by_id.items() if mid in retained_mutant_rows))
        line_count_ok = (len(lines) <= MAX_LINES and summary.get("line_review_cap_exceeded") is False
                         and summary.get("survivor_line_count") == len(lines))
        elapsed = sum(float(line.get("review_elapsed_seconds", 0) or 0) for line in lines if isinstance(line, dict))
        reviewed = all(line.get("reviewed") is True and line.get("category") in ALLOWED_CATEGORIES
                       and isinstance(line.get("explanation"), str) and bool(line.get("explanation").strip())
                       and isinstance(line.get("evidence"), list) and bool(line.get("evidence"))
                       and isinstance(line.get("variant_reviews"), list)
                       and len(line.get("variant_reviews", [])) == len(line.get("mutant_ids", []))
                       and {str(v.get("mutant_id")) for v in line.get("variant_reviews", []) if isinstance(v, dict)}
                           == set(map(str, line.get("mutant_ids", [])))
                       and all(isinstance(v, dict) and isinstance(v.get("explanation"), str)
                               and bool(v.get("explanation", "").strip())
                               and isinstance(v.get("evidence"), list) and bool(v.get("evidence"))
                               for v in line.get("variant_reviews", []))
                       for line in lines if isinstance(line, dict))
        add_check(checks, outstanding,
                  survivor_ids == line_ids == disposition_ids
                  and len(dispositions) == len(survivor_mutants)
                  and line_structure_ok and expected_line_members == actual_line_members
                  and disposition_structure_ok
                  and all(d.get("report_sha256") == report_hash and d.get("line_key") in lines_by_key
                          for d in dispositions if isinstance(d, dict))
                  and all(line.get("report_sha256") == report_hash for line in lines if isinstance(line, dict)),
                  "survivor-disposition-links", "Every Survived and NoCoverage mutant must link once to its report-bound source-line disposition.")
        add_check(checks, outstanding, line_count_ok and reviewed and elapsed <= MAX_REVIEW_SECONDS,
                  "source-line-review", f"Reviewed lines must be complete within {MAX_LINES} lines and {MAX_REVIEW_SECONDS} seconds (recorded {elapsed:.3f} seconds).")

        replay_dir = review / "replay"
        selection = require_mapping(read_required(replay_dir / "selection.json"), "replay selection")
        replay = require_mapping(read_required(replay_dir / "replay.json"), "replay execution")
        replay_records = replay.get("attempts", [])
        if not isinstance(replay_records, list):
            replay_records = []
        selected_records = selection.get("selected", selection.get("records", []))
        selected_ids = {str(x.get("mutant_id", x.get("id"))) for x in selected_records if isinstance(x, dict)} if isinstance(selected_records, list) else set()
        (selected_matches_report, selected_survivor_files, selected_survivor_mutators,
         selected_killed_ids, selection_statuses) = selected_report_coverage(
             selected_records, report_mutant_rows)
        replayed_ids = {str(x.get("mutant_id", x.get("id"))) for x in replay_records if isinstance(x, dict)}
        baseline_ok = replay.get("baseline_status") == "passed"
        baseline_records = replay.get("baseline_control", [])
        baseline_records_ok = (isinstance(baseline_records, list) and len(baseline_records) == 2
            and baseline_records[0].get("child") == "restore-and-build"
            and baseline_records[0].get("returncode") == 0
            and baseline_records[1].get("child") == "test"
            and baseline_records[1].get("classification") == "tests-passed")
        replay_execution_ok = replay.get("status") == "completed"
        graph = selection.get("graph", {})
        replay_limits = selection.get("limits", {})
        survivor_files = {
            str(row[1]) for row in report_mutant_rows.values()
            if row[0] in {"Survived", "NoCoverage"}
        }
        survivor_mutators = {
            str(row[3]) for row in report_mutant_rows.values()
            if row[0] in {"Survived", "NoCoverage"}
        }
        graph_coverage_ok = (graph.get("coverage_possible") is True
            and selection.get("report_sha256") == report_hash
            and selected_matches_report
            and selected_survivor_files >= survivor_files
            and selected_survivor_mutators >= survivor_mutators
            and replay.get("coverage", {}).get("possible") is True
            and replay.get("coverage", {}).get("uncovered_files") == []
            and replay.get("coverage", {}).get("uncovered_mutators") == [])
        attempts_well_formed = all(isinstance(attempt, dict)
            and isinstance(attempt.get("classification"), str)
            and (bool(attempt.get("commands")) or attempt.get("classification") in {
                "source-input-mismatch", "infrastructure-failure"})
            and all(isinstance(command, dict) and command.get("classification")
                    and command.get("controller_receipt_relative")
                    for command in attempt.get("commands", []))
            for attempt in replay_records)
        replay_receipt_paths: set[str] = set()
        replay_command_evidence_ok = attempts_well_formed and len(replay_records) == len(selected_ids)
        command_receipt_paths: set[str] = set()
        records_to_check = ([(record, True) for record in baseline_records]
                            + [(record, False) for record in replay_records]) if isinstance(baseline_records, list) else []
        for record, is_baseline in records_to_check:
            if not isinstance(record, dict):
                replay_command_evidence_ok = False
                continue
            command_rows = record.get("commands", []) if "commands" in record else [record]
            for command in command_rows:
                if not isinstance(command, dict) or not isinstance(command.get("controller_receipt_relative"), str):
                    replay_command_evidence_ok = False
                    continue
                receipt_path = safe_evidence_path(root, command["controller_receipt_relative"], original_evidence_root)
                if not receipt_path.is_file():
                    replay_command_evidence_ok = False
                    continue
                receipt_rel = receipt_path.relative_to(root).as_posix()
                command_receipt_paths.add(receipt_rel)
                replay_receipt_paths.add(receipt_rel)
                receipt = require_mapping(read_json(receipt_path), "replay controller receipt")
                if (receipt.get("tested_commit") != source.get("tested_commit")
                        or receipt.get("source_inputs_unchanged") is not True):
                    replay_command_evidence_ok = False
                command_log_name = receipt.get("command_log")
                if isinstance(command_log_name, str):
                    command_log_path = (receipt_path.parent / command_log_name).resolve()
                    command_log_rel = command_log_path.relative_to(root).as_posix()
                    replay_receipt_paths.add(command_log_rel)
                    if (not command_log_path.is_file()
                            or receipt.get("command_log_sha256") != sha256_bytes(command_log_path.read_bytes())):
                        replay_command_evidence_ok = False
                else:
                    replay_command_evidence_ok = False
                if is_baseline and (receipt.get("source_inputs_unchanged") is not True
                        or receipt.get("termination") is not None
                        or receipt.get("exit_reason") != "command-exit"):
                    replay_command_evidence_ok = False
                if command.get("child") == "test":
                    expected_xml = receipt.get("expected_xml", [])
                    if not isinstance(expected_xml, list) or len(expected_xml) != 1:
                        replay_command_evidence_ok = False
                    else:
                        xml_path = safe_evidence_path(root, str(expected_xml[0]), original_evidence_root)
                        xml_result = retained_bytes(xml_path)
                        if xml_result is None:
                            if command.get("classification") in {"tests-passed", "assertion-failure"} or is_baseline:
                                replay_command_evidence_ok = False
                        else:
                            xml_data, actual_xml_path = xml_result
                            counts = xunit_counts(xml_data)
                            if counts is None:
                                replay_command_evidence_ok = False
                            elif command.get("classification") == "tests-passed" and (counts[0] <= 0 or counts[1] <= 0 or counts[2] or counts[3]):
                                replay_command_evidence_ok = False
                            elif command.get("classification") == "assertion-failure" and counts[2] <= 0:
                                replay_command_evidence_ok = False
                            if actual_xml_path.is_file():
                                replay_receipt_paths.add(xml_path.relative_to(root).as_posix())
                            replay_receipt_paths.add(actual_xml_path.relative_to(root).as_posix())
                command_class = command.get("classification")
                if command_class == "command-passed" and (receipt.get("exit_code") != 0 or receipt.get("exit_reason") != "command-exit" or receipt.get("termination") is not None):
                    replay_command_evidence_ok = False
                if command_class == "tests-passed" and (receipt.get("exit_code") != 0 or receipt.get("exit_reason") != "command-exit" or receipt.get("termination") is not None):
                    replay_command_evidence_ok = False
                if command_class == "assertion-failure" and (command.get("child") != "test" or receipt.get("exit_code") == 0 or receipt.get("exit_reason") != "command-exit" or receipt.get("termination") is not None):
                    replay_command_evidence_ok = False
                if command_class == "compile-failure" and (command.get("child") != "build" or receipt.get("exit_code") in {None, 0} or receipt.get("exit_reason") != "command-exit"):
                    replay_command_evidence_ok = False
                if command_class == "timeout" and (receipt.get("exit_reason") != "elapsed-cap" or receipt.get("termination") is None):
                    replay_command_evidence_ok = False
                if is_baseline:
                    if command.get("child") == "restore-and-build":
                        if command.get("returncode") != 0 or receipt.get("exit_code") != 0:
                            replay_command_evidence_ok = False
                    elif command.get("child") == "test":
                        if command_class != "tests-passed":
                            replay_command_evidence_ok = False
                    else:
                        replay_command_evidence_ok = False
        ledger_path = root / "controller-invocations.json"
        ledger = read_required(ledger_path)
        ledger_rows = [row for row in ledger if isinstance(row, dict) and row.get("mode") == "replay"] if isinstance(ledger, list) else []
        ledger_by_id: dict[str, list[dict[str, Any]]] = collections.defaultdict(list)
        ledger_receipts: set[str] = set()
        ledger_budget_ok = isinstance(ledger, list) and len(ledger_rows) <= 66
        for row in ledger_rows:
            mutant_id = str(row.get("mutant_id"))
            ledger_by_id[mutant_id].append(row)
            receipt_value = row.get("receipt")
            if not isinstance(receipt_value, str):
                ledger_budget_ok = False
            else:
                try:
                    ledger_receipts.add(safe_evidence_path(root, receipt_value, original_evidence_root).relative_to(root).as_posix())
                except EvidenceError:
                    ledger_budget_ok = False
        distinct_mutant_ids = set(ledger_by_id) - {"baseline-control"}
        ledger_spent = sum(float(row.get("elapsed_seconds", 0)) if row.get("completed")
                           else float(row.get("reserved_seconds", 0)) for row in ledger_rows)
        expected_ledger_ids = set(selected_ids) | {"baseline-control"}
        ledger_budget_ok = (ledger_budget_ok and distinct_mutant_ids == selected_ids
            and set(ledger_by_id) == expected_ledger_ids
            and all(1 <= len(rows) <= 2 for rows in ledger_by_id.values())
            and len(ledger_by_id.get("baseline-control", [])) == 2
            and all(len(ledger_by_id.get(mid, [])) == len(next(
                (a.get("commands", []) for a in replay_records if str(a.get("mutant_id")) == mid), []))
                for mid in selected_ids)
            and ledger_receipts == command_receipt_paths
            and ledger_spent <= 3600)
        add_check(checks, outstanding, ledger_budget_ok,
                  "replay-invocation-ledger", "Retained replay ledger must match every baseline and mutant command and remain within 32 IDs, two commands per ID, and 3,600 seconds.")
        killed_report_ids = {mutant_id for mutant_id, row in report_mutant_rows.items() if row[0] == "Killed"}
        killed_replayed = selected_killed_ids & replayed_ids
        killed_required = bool(killed_report_ids)
        killed_ok = (not killed_required or 1 <= len(killed_replayed & killed_report_ids) <= MAX_KILLED_REPLAYS)
        add_check(checks, outstanding,
                  len(selected_ids) <= MAX_REPLAY_IDS and selected_ids <= replayed_ids
                  and replayed_ids <= selected_ids and replay_execution_ok and baseline_ok and killed_ok
                  and graph_coverage_ok and attempts_well_formed and baseline_records_ok
                  and replay_limits.get("max_mutants") == MAX_REPLAY_IDS
                  and replay_limits.get("killed_sample_max") == MAX_KILLED_REPLAYS
                  and replay_limits.get("command_seconds") == 180
                  and replay_limits.get("total_seconds") == 3600
                  and {str(x) for x in graph.get("selected_ids", [])} == selected_ids
                  and len(graph.get("selected_ids", [])) == len(selected_ids)
                  and graph.get("killed_selected") == selection_statuses.get("Killed", 0)
                  and replay_command_evidence_ok
                  and selection_statuses.get("Killed", 0) <= MAX_KILLED_REPLAYS
                  and (not killed_required or selection_statuses.get("Killed", 0) >= 1),
                  "bounded-replay-coverage", f"Replay must complete for selected mutants (max {MAX_REPLAY_IDS}), pass baseline, and include 1..{MAX_KILLED_REPLAYS} Killed mutant when available.")

        disagreements = {str(x.get("mutant_id", x.get("id"))) for x in replay_records
                         if isinstance(x, dict) and (x.get("runner_disagreement") is True
                         or x.get("classification") == "runner-disagreement")}
        incomplete_replay_ids = {str(x.get("mutant_id", x.get("id"))) for x in replay_records
                                 if isinstance(x, dict) and x.get("runner_comparison_complete") is not True}
        compile_failure_replay_ids = {str(x.get("mutant_id", x.get("id"))) for x in replay_records
                                      if isinstance(x, dict) and (x.get("classification") == "compile-failure"
                                      or any(command.get("classification") == "compile-failure"
                                             for command in x.get("commands", []) if isinstance(command, dict)))}
        decision_url = decision.get("url")
        accepted_break = decision.get("accepted_break")
        risk = decision.get("risk_disposition")
        risk = risk if isinstance(risk, dict) else {}
        risk_runner = {str(x) for x in risk.get("runner_disagreement_ids", [])}
        risk_lines = {str(x) for x in risk.get("unresolved_line_keys", [])}
        risk_uncovered_files = {str(x) for x in risk.get("replay_uncovered_files", [])}
        risk_uncovered_mutators = {str(x) for x in risk.get("replay_uncovered_mutators", [])}
        risk_incomplete_replays = {str(x) for x in risk.get("incomplete_replay_ids", [])}
        risk_compile_failures = {str(x) for x in risk.get("compile_failure_replay_ids", [])}
        unresolved_keys = {str(line.get("line_key")) for line in lines
                           if isinstance(line, dict) and line.get("category") == "unresolved"}
        uncovered_files = {str(x) for x in replay.get("coverage", {}).get("uncovered_files", [])}
        uncovered_mutators = {str(x) for x in replay.get("coverage", {}).get("uncovered_mutators", [])}
        decision_valid = (decision_url == args.threshold_decision
                          and isinstance(decision_url, str)
                          and ("/issues/92#issuecomment-" in decision_url or "/issues/13#issuecomment-" in decision_url)
                          and isinstance(decision.get("issue13_record_url"), str)
                          and decision.get("issue13_record_url").startswith("https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/13#issuecomment-")
                          and isinstance(accepted_break, int) and not isinstance(accepted_break, bool)
                          and 0 <= accepted_break <= 60
                          and decision.get("report_sha256") == report_hash
                          and isinstance(risk.get("rationale"), str) and bool(risk["rationale"].strip())
                          and disagreements == risk_runner and unresolved_keys == risk_lines
                          and incomplete_replay_ids == risk_incomplete_replays
                          and compile_failure_replay_ids == risk_compile_failures
                          and uncovered_files == risk_uncovered_files
                          and uncovered_mutators == risk_uncovered_mutators)
        add_check(checks, outstanding, decision_valid,
                  "hari-threshold-and-risk-decision", "Retained Hari decision must accept B for this report and explicitly dispose of every runner disagreement and unresolved line.")
        comparison_ok = (raw_score_valid and decision_valid
                         and current_config.get("stryker-config", {}).get("thresholds", {}).get("break") == accepted_break
                         and 100 * numerator >= accepted_break * denominator)
        add_check(checks, outstanding, comparison_ok,
                  "accepted-threshold-comparison", "Accepted break must be the current config value and pass the unrounded integer score comparison.")

        normal_projects = (
            "Parchwell.ArchitectureTests", "Parchwell.Kafka.Tests",
            "Parchwell.LoadGen.Tests", "Parchwell.Scenarios.Tests",
            "Parchwell.Verifier.Tests",
        )
        retention = require_mapping(read_required(root / "retention.json"), "retention manifest")
        artifacts = retention.get("artifacts")
        retention_errors: list[str] = []
        if not isinstance(artifacts, list):
            artifacts = []
            retention_errors.append("retention.json.artifacts must be an array")
        covered: set[str] = set()
        for item in artifacts:
            if not isinstance(item, dict) or not isinstance(item.get("path"), str):
                retention_errors.append("invalid artifact entry")
                continue
            relative = item["path"]
            target = (root / relative).resolve()
            try:
                target.relative_to(root)
            except ValueError:
                retention_errors.append(f"artifact escapes evidence root: {relative}")
                continue
            compressed_path = item.get("compressed_path")
            if target.is_file():
                actual = sha256_bytes(target.read_bytes())
                if item.get("sha256") != actual:
                    retention_errors.append(f"SHA-256 mismatch: {relative}")
                    continue
            elif compressed_path:
                compressed_candidate = (root / compressed_path).resolve()
                try:
                    compressed_candidate.relative_to(root)
                except ValueError:
                    retention_errors.append(f"compressed artifact escapes evidence root: {compressed_path}")
                    continue
                if not compressed_candidate.is_file():
                    retention_errors.append(f"missing retained artifact: {relative} and {compressed_path}")
                    continue
                try:
                    original = gzip.decompress(compressed_candidate.read_bytes())
                except (OSError, EOFError) as exc:
                    retention_errors.append(f"cannot decompress retained artifact {compressed_path}: {exc}")
                    continue
                if item.get("sha256") != sha256_bytes(original):
                    retention_errors.append(f"uncompressed SHA-256 mismatch: {relative}")
                    continue
            else:
                retention_errors.append(f"missing retained artifact: {relative}")
                continue
            covered.add(relative)
            if compressed_path:
                compressed = (root / compressed_path).resolve()
                try:
                    compressed.relative_to(root)
                except ValueError:
                    retention_errors.append(f"compressed artifact escapes evidence root: {compressed_path}")
                    continue
                if not compressed.is_file() or item.get("compressed_sha256") != sha256_bytes(compressed.read_bytes()):
                    retention_errors.append(f"compressed artifact hash missing or mismatched: {compressed_path}")
                else:
                    covered.add(compressed_path)
        required_retained = {
            "mutation-full/controller.json", "mutation-full/tested-source.json",
            "mutation-full/extraction-summary.json", "mutation-full/discovered-tests.json",
            "mutation-full/mutants.json", "review/lines.json", "review/dispositions.json",
            "review/replay/selection.json", "review/replay/replay.json", "threshold-decision.json",
            "mutation-full/stryker-config.json", "publication-source.json",
            "controller-invocations.json",
            "validation/baseline-build/controller.json", "validation/baseline-test/controller.json",
            "validation/verify-dotnet/controller.json", "validation/compile-normal.json",
            "validation/compile-mutation.json", "validation/discovery/command.log.gz",
            "validation/mutation-only/results.xml",
        }
        required_retained.update(replay_receipt_paths)
        required_retained.update(f"validation/verify-dotnet/{name}/results.xml" for name in normal_projects)
        required_retained.add("mutation-full/mutation-report.json")
        report_manifest = next((item for item in artifacts if isinstance(item, dict)
                                and item.get("path") == "mutation-full/mutation-report.json"), {})
        report_compression_ok = (
            report_manifest.get("sha256") == report_hash
            and report_manifest.get("compressed_path") == "mutation-full/mutation-report.json.gz"
            and report_manifest.get("compressed_sha256") == report_file_hash
        ) if report_path.name.endswith(".gz") else report_manifest.get("sha256") == report_hash
        add_check(checks, outstanding,
                  not retention_errors and required_retained <= covered and report_compression_ok,
                  "retained-artifact-hashes", "; ".join(retention_errors) if retention_errors else
                  "Manifest hashes must cover source identity, original and compressed report bytes, inventories, review, replay, validation, threshold decision, and measured config.")

        # These precondition receipts prove the ordinary baseline path and test
        # set were collected; failures are evidence gaps, not permission to pass.
        validation = root / "validation"
        validation_receipts = [
            validation / "baseline-build" / "controller.json",
            validation / "baseline-test" / "controller.json",
            validation / "verify-dotnet" / "controller.json",
        ]
        validation_ok = True
        for receipt_path in validation_receipts:
            try:
                validation_ok = controller_success(receipt_path) and validation_ok
            except EvidenceError:
                validation_ok = False
        add_check(checks, outstanding, validation_ok,
                  "validation-receipts", "Required build, test, and normal-suite controller receipts must show completed successful commands with unchanged inputs.")
        validation_sources: list[Any] = []
        validation_source_links_ok = True
        for name in VALIDATION_SOURCE_DIRS:
            source_path = validation / name / "tested-source.json"
            controller_path = validation / name / "controller.json"
            try:
                validation_source = require_mapping(
                    read_required(source_path), f"{name} tested-source manifest")
                validation_sources.append(validation_source)
                if controller_path.is_file():
                    receipt = require_mapping(
                        read_required(controller_path), f"{name} controller receipt")
                    if receipt.get("source_manifest_sha256") != sha256_bytes(source_path.read_bytes()):
                        validation_source_links_ok = False
            except (EvidenceError, OSError):
                validation_source_links_ok = False
        validation_source_maps_ok = (
            validation_source_links_ok
            and len(validation_sources) == len(VALIDATION_SOURCE_DIRS)
            and validation_source_inputs_match(source, validation_sources)
        )
        add_check(checks, outstanding, validation_source_maps_ok,
                  "validation-source-input-maps",
                  "Every retained validation source-input map must match the measured mutation source map, and each present controller receipt must hash its adjacent manifest.")
        validation_files_ok = all((validation / relative).is_file() for relative in (
            "compile-normal.json", "compile-mutation.json", "discovery/command.log.gz"))
        validation_files_ok = validation_files_ok and retained_bytes(validation / "mutation-only" / "results.xml") is not None
        normal_xml_ok = all(valid_xunit_report(validation / "verify-dotnet" / name / "results.xml")
                            for name in normal_projects)
        add_check(checks, outstanding, validation_files_ok,
                  "validation-inventories-and-tests", "Evaluated Compile inventories, ordinary discovery, and mutation-only XML must be retained.")
        add_check(checks, outstanding, normal_xml_ok,
                  "normal-suite-reports", "All five normal verification XML reports must show positive passing counts and zero failures, skips, errors, or not-run cases.")

        result = {
            "schema_version": 1,
            "complete": not outstanding,
            "report": {"path": report_path.relative_to(root).as_posix() if report_path.is_file() else None,
                       "sha256": report_hash, "stored_file_sha256": report_file_hash},
            "source_identity": {"tested_commit": source.get("tested_commit"),
                                "tested_commit_tree": source.get("tested_commit_tree"),
                                "source_input_manifest_sha256": source.get("source_input_manifest_sha256"),
                                "publication_commit": publication.get("tested_commit"),
                                "publication_commit_tree": publication.get("tested_commit_tree"),
                                "publication_source_input_manifest_sha256": publication.get("source_input_manifest_sha256")},
            "configuration": {"measured_sha256": config_identity.get("sha256"),
                              "current_sha256": publication_config_hash,
                              "current_break": current_config.get("stryker-config", {}).get("thresholds", {}).get("break")},
            "score": {"numerator": numerator, "denominator": denominator,
                      "proposal": min(60, (100 * numerator) // denominator) if raw_score_valid else None,
                      "proposal_formula": "min(60, (100 * numerator) // denominator)",
                      "accepted_break": accepted_break if decision_valid else None,
                      "comparison_passes": comparison_ok},
            "counts": {"serialized_mutants": summary.get("serialized_mutant_count"),
                       "survivor_mutants": len(survivor_mutants), "survivor_lines": len(lines),
                       "replay_selected": len(selected_ids), "replay_completed": len(selected_ids & replayed_ids),
                       "runner_disagreements": sorted(disagreements),
                       "unresolved_lines": sorted(unresolved_keys)},
            "threshold_decision_url": decision_url,
            "issue13_record_url": decision.get("issue13_record_url"),
            "risk_disposition": risk,
            "checks": checks,
            "outstanding": outstanding,
        }
        output = root / "acceptance.json"
        output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        print(json.dumps({"acceptance": str(output), "complete": result["complete"],
                          "outstanding": outstanding}, sort_keys=True))
        return 0 if result["complete"] else 1
    except (EvidenceError, OSError, ValueError, TypeError, KeyError) as exc:
        failure = {"schema_version": 1, "complete": False, "checks": checks,
                   "outstanding": outstanding + [f"input-error: {exc}"]}
        try:
            (root / "acceptance.json").write_text(json.dumps(failure, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        except OSError:
            pass
        print(f"accept.py: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

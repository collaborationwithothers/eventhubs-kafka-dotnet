#!/usr/bin/env python3
"""Map the frozen issue-93 assignment to one retained schema-2 report.

Mutant IDs are report-local locators, never cross-report identities. The CLI
consumes the retained reports once and exclusively creates its output, including
an incomplete error envelope when an input cannot support a diagnostic map.
"""

from __future__ import annotations

import argparse
import collections
import gzip
import hashlib
import json
import re
import sys
from pathlib import Path


HISTORICAL_SHA256 = "f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492"
HISTORICAL = "docs/operations/evidence/issue-92-pr6b"
CONFIG = "tests/Parchwell.Verifier.Tests/stryker-config.json"
MAX_MUTANTS = 10000
STATUSES = frozenset(("Killed", "Timeout", "Survived", "NoCoverage", "CompileError", "Ignored"))
SURVIVING = frozenset(("Survived", "NoCoverage"))
PATTERNS = ["VerifyRun/Judgement/*.cs", "VerifyRun/Window/*.cs",
            "VerifyRun/A01/A01EvidenceRecorder.cs", "ProbeGate/ProbeGateEvaluator.cs"]
PROOFS = {"4875": "P1", "5147": "P2", "5315": "P3", "5531": "P4", "5661": "P5"}


class MappingError(ValueError):
    """An input cannot support the approved evidence contract."""


def require(condition, message):
    if not condition:
        raise MappingError(message)


def sha256(payload):
    return hashlib.sha256(payload).hexdigest()


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, f"duplicate JSON key: {key}")
        result[key] = value
    return result


def decode_json(payload):
    return json.loads(payload, object_pairs_hook=unique_object)


def normalize_source(path, project_root):
    require(isinstance(path, str) and isinstance(project_root, str), "invalid source path/root")
    normalized = path.replace("\\", "/")
    root = project_root.replace("\\", "/").rstrip("/")
    if root and normalized.startswith(root + "/"):
        normalized = normalized[len(root) + 1:]
    require(normalized and not normalized.startswith("/") and
            not re.match(r"^[A-Za-z]:", normalized) and
            all(part not in ("", ".", "..") for part in normalized.split("/")),
            f"outside-root or noncanonical source path: {path}")
    return normalized


def identifier(value):
    require(type(value) in (str, int) and str(value).isdigit(), f"invalid mutant ID: {value!r}")
    return str(value)


def identity(file, location, mutator, replacement):
    require(isinstance(location, dict), "missing location")
    coordinates = []
    for endpoint in ("start", "end"):
        point = location.get(endpoint)
        require(isinstance(point, dict), f"missing location {endpoint}")
        for axis in ("line", "column"):
            value = point.get(axis)
            require(type(value) is int and value > 0, f"invalid {endpoint} {axis}")
            coordinates.append(value)
    require(tuple(coordinates[:2]) <= tuple(coordinates[2:]), "reversed location")
    require(isinstance(mutator, str) and bool(mutator) and isinstance(replacement, str),
            "invalid mutator/replacement")
    return (file, *coordinates, mutator, replacement)


def record_identity(record):
    return identity(record["file"], record["location"], record["mutatorName"], record["replacement"])


def parse_report(report):
    require(isinstance(report, dict) and report.get("schemaVersion") == "2",
            "expected Stryker schemaVersion '2'")
    require(isinstance(report.get("projectRoot"), str) and bool(report["projectRoot"]),
            "missing report projectRoot")
    require(isinstance(report.get("files"), dict), "missing report files")
    require(isinstance(report.get("testFiles"), dict) and isinstance(report.get("thresholds"), dict),
            "missing schema-2 testFiles/thresholds")
    sources, records, ids = {}, [], set()
    for raw_path, entry in report["files"].items():
        file = normalize_source(raw_path, report["projectRoot"])
        require(file not in sources, f"duplicate normalized source path: {file}")
        require(isinstance(entry, dict) and isinstance(entry.get("source"), str) and
                isinstance(entry.get("mutants"), list), f"invalid source/mutants: {file}")
        sources[file] = entry["source"]
        require(len(records) + len(entry["mutants"]) <= MAX_MUTANTS,
                f"serialized population exceeds {MAX_MUTANTS}")
        for mutant in entry["mutants"]:
            require(isinstance(mutant, dict), "invalid mutant object")
            mutant_id = identifier(mutant.get("id"))
            require(mutant_id not in ids, f"duplicate report mutant ID: {mutant_id}")
            ids.add(mutant_id)
            require(isinstance(mutant.get("status"), str) and mutant["status"] in STATUSES,
                    f"unsupported status: {mutant.get('status')!r}")
            record = dict(mutant, file=file)
            record_identity(record)
            records.append(record)
    return sources, records


def count_population(records):
    require(len(records) <= MAX_MUTANTS, f"serialized population exceeds {MAX_MUTANTS}")
    counts = collections.Counter()
    for record in records:
        status = record.get("status")
        require(isinstance(status, str) and status in STATUSES, f"unsupported status: {status!r}")
        counts[status] += 1
    numerator = counts["Killed"] + counts["Timeout"]
    denominator = numerator + counts["Survived"] + counts["NoCoverage"]
    return dict(sorted(counts.items())), {
        "numerator": numerator, "denominator": denominator,
        "percent": numerator * 100 / denominator if denominator else None,
    }


def match_details(record):
    fields = ("id", "status", "statusReason", "location", "mutatorName", "replacement")
    return {field: record[field] for field in fields if field in record}


def tuple_index(records):
    index = collections.defaultdict(list)
    for record in records:
        index[record_identity(record)].append(record)
    return index


def match_variant(original, index, historical_sources, new_sources, checkout_sources):
    file = original["file"]
    source = historical_sources.get(file)
    verified = source is not None and source == new_sources.get(file) == checkout_sources.get(file)
    matches = [match_details(record) for record in index.get(record_identity(original), [])]
    status = "source_mismatch" if not verified else (
        "missing" if not matches else "unique" if len(matches) == 1 else "ambiguous")
    return {"match_status": status, "matches": matches}


def proof_annotation(original_id):
    proof = PROOFS.get(str(original_id))
    if proof is None:
        return None
    annotation = {
        "proof": proof,
        "link": "../child-b/proofs.md#" + proof.lower() + "-variant-" + str(original_id),
        "evidence_kind": "callable-domain proof; independent of raw mutation status",
    }
    if proof == "P4":
        # A host/SDK inventory alone cannot establish which runtime the test
        # process selected. Do not infer that premise from an installed SDK.
        annotation.update(applicability="runtime premise not verified",
                          required_runtime_commit="6aa72daa24f66c01c3d7b6a32519e9d21396deec")
    return annotation


def frozen_assignment(lines, dispositions, historical_records):
    require(isinstance(lines, list) and isinstance(dispositions, list), "invalid frozen inventory")
    selected = [line for line in lines if line.get("category") in ("missing test", "unresolved")]
    require(len(selected) == 136 and len({line["line_key"] for line in selected}) == 136,
            "expected 136 unique assigned lines")
    historical_ids = {identifier(record["id"]): record for record in historical_records}
    disposition_ids = {}
    for disposition in dispositions:
        mutant_id = identifier(disposition["mutant_id"])
        require(mutant_id not in disposition_ids, f"duplicate frozen disposition ID: {mutant_id}")
        disposition_ids[mutant_id] = disposition
    assigned, linked, tuples = [], set(), set()
    for line in selected:
        file = normalize_source(line["file"], "")
        require(type(line["start_line"]) is int and line["start_line"] > 0, "invalid frozen line")
        expected_key = f"{HISTORICAL_SHA256}:{file}:{line['start_line']}"
        require(line["line_key"] == expected_key and line["report_sha256"] == HISTORICAL_SHA256,
                "inconsistent frozen line key/report")
        require(isinstance(line["mutant_ids"], list) and bool(line["mutant_ids"]), "empty frozen line")
        for value in line["mutant_ids"]:
            mutant_id = identifier(value)
            require(mutant_id not in linked, f"duplicate frozen linked ID: {mutant_id}")
            linked.add(mutant_id)
            require(mutant_id in disposition_ids and mutant_id in historical_ids,
                    f"missing frozen disposition/report ID: {mutant_id}")
            disposition, historical = disposition_ids[mutant_id], historical_ids[mutant_id]
            key = identity(normalize_source(disposition["file"], ""), disposition["span"],
                           disposition["mutator"], disposition["replacement"])
            require(disposition["line_key"] == expected_key and
                    disposition["report_sha256"] == HISTORICAL_SHA256 and
                    key == record_identity(historical) and key[0] == file and key[1] == line["start_line"],
                    f"inconsistent frozen tuple/line/report: {mutant_id}")
            require(key not in tuples, f"duplicate frozen variant tuple: {mutant_id}")
            tuples.add(key)
            assigned.append((line, historical))
    require(len(assigned) == len(linked) == 228, "expected 228 unique assigned variants")
    return selected, assigned


def read_bound_report(path, hashes, prefix):
    stored = path.read_bytes()
    hashes[prefix + "_gzip_sha256"] = sha256(stored)
    payload = gzip.decompress(stored)
    hashes[prefix + "_sha256"] = sha256(payload)
    hashes[prefix + "_bytes"] = len(payload)
    hashes[prefix + "_gzip_bytes"] = len(stored)
    return decode_json(payload)


def verify_bindings(root, controller, source, source_bytes, report_hash):
    require(controller.get("source_manifest_sha256") == sha256(source_bytes),
            "tested-source/controller hash mismatch")
    require(controller.get("report_sha256") == report_hash, "new report/controller hash mismatch")
    require(isinstance(source.get("tested_commit"), str) and bool(source["tested_commit"]) and
            controller.get("tested_commit") == source["tested_commit"] and
            isinstance(source.get("tested_commit_tree"), str) and bool(source["tested_commit_tree"]),
            "inconsistent tested commit/tree")
    config_bytes = (root / CONFIG).read_bytes()
    config_hash = sha256(config_bytes)
    require(isinstance(source.get("config"), dict) and source["config"].get("sha256") == config_hash,
            "actual config/tested-source hash mismatch")
    inputs = source.get("source_input_sha256")
    require(isinstance(inputs, dict) and inputs.get(CONFIG) == config_hash,
            "actual config/source-input hash mismatch")
    require(source.get("source_input_manifest_sha256") == sha256(json.dumps(inputs, sort_keys=True).encode()),
            "source-input manifest hash mismatch")
    config = decode_json(config_bytes).get("stryker-config")
    require(isinstance(config, dict) and config == {
        "project": "Parchwell.Verifier.csproj", "test-runner": "mtp", "configuration": "Release",
        "target-framework": "net10.0", "reporters": ["json", "html", "progress"],
        "thresholds": {"high": 80, "low": 60, "break": 60}, "mutate": PATTERNS,
    }, "current Stryker selection/settings/break 60 not preserved")
    project = "tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj"
    project_bytes = (root / project).read_bytes()
    require(inputs.get(project) == sha256(project_bytes), "test project/source-input hash mismatch")
    project_text = project_bytes.decode("utf-8")
    require("'$(ParchwellVerifierMutationOnly)' == 'true'" in project_text and
            'Compile Remove="Component/**/*.cs;Support/ComponentEvidenceBuilder.cs;Host/VerifyRunHandlerTests.cs;Host/VerifierHostTests.cs"' in project_text,
            "mutation-only project selection not preserved")
    require(controller.get("environment_overrides", {}).get("ParchwellVerifierMutationOnly") == "true",
            "mutation-only controller environment missing")


def extract(root, run_dir, hashes):
    historical_dir = root / HISTORICAL
    historical_report = read_bound_report(historical_dir / "mutation-full/mutation-report.json.gz",
                                         hashes, "historical_report")
    require(hashes["historical_report_sha256"] == HISTORICAL_SHA256, "historical report hash mismatch")
    historical_sources, historical_records = parse_report(historical_report)
    lines = decode_json((historical_dir / "review/lines.json").read_bytes())
    dispositions = decode_json((historical_dir / "review/dispositions.json").read_bytes())
    selected, assigned = frozen_assignment(lines, dispositions, historical_records)
    controller_bytes = (run_dir / "controller.json").read_bytes()
    hashes["controller_sha256"] = sha256(controller_bytes)
    controller = decode_json(controller_bytes)
    source_bytes = (run_dir / "tested-source.json").read_bytes()
    hashes["tested_source_sha256"] = sha256(source_bytes)
    source = decode_json(source_bytes)
    report = read_bound_report(run_dir / "mutation-report.json.gz", hashes, "report")
    new_sources, records = parse_report(report)
    verify_bindings(root, controller, source, source_bytes, hashes["report_sha256"])
    checkout_sources = {}
    for file in {record["file"] for _, record in assigned} | {
            record["file"] for record in historical_records if identifier(record["id"]) == "4585"}:
        path = root / "src/Parchwell.Verifier" / file
        if path.is_file():
            checkout_sources[file] = path.read_bytes().decode("utf-8")
    index = tuple_index(records)
    rows = []
    for line, historical in assigned:
        original_id = identifier(historical["id"])
        row = {"original_id": original_id, "line_key": line["line_key"],
               "category": line["category"], "file": historical["file"],
               "span": historical["location"], "mutator": historical["mutatorName"],
               "replacement": historical["replacement"], "historical_status": historical["status"]}
        row.update(match_variant(historical, index, historical_sources, new_sources, checkout_sources))
        proof = proof_annotation(original_id)
        if proof:
            row["proof_annotation"] = proof
        rows.append(row)
    rows.sort(key=lambda row: (row["file"], row["span"]["start"]["line"], int(row["original_id"])))
    grouped = collections.defaultdict(list)
    for row in rows:
        grouped[row["line_key"]].append(row)
    summaries = []
    for line in sorted(selected, key=lambda line: (line["file"], line["start_line"])):
        members = grouped[line["line_key"]]
        summaries.append({"line_key": line["line_key"], "file": line["file"],
                          "start_line": line["start_line"], "category": line["category"],
                          "original_ids": [row["original_id"] for row in members],
                          "variants": [{"original_id": row["original_id"],
                                        "match_status": row["match_status"], "matches": row["matches"]}
                                       for row in members]})
    previous = {record_identity(record) for record in historical_records if record["status"] in SURVIVING}
    survivors = [dict(file=record["file"], **match_details(record), classification=(
        "previously_surviving" if record_identity(record) in previous else "newly_surviving"))
        for record in records if record["status"] in SURVIVING]
    survivors.sort(key=lambda record: (record["file"], record["location"]["start"]["line"], int(identifier(record["id"]))))
    counts, score = count_population(records)
    generated = controller.get("maximum_observed_generated_count")
    complete = (controller.get("execution_evidence_complete") is True and
                controller.get("source_inputs_unchanged") is True and
                controller.get("exit_reason") == "command-exit" and
                type(controller.get("exit_code")) is int and controller["exit_code"] == 0 and
                controller.get("termination") is None and
                type(generated) is int and 0 < generated <= MAX_MUTANTS and
                generated in controller.get("observed_generated_counts", []))
    limitations = []
    historical_by_id = {identifier(record["id"]): record for record in historical_records}
    for original_id in ("2657", "4585", "5014"):
        historical = historical_by_id[original_id]
        limitation = {"original_id": original_id, "file": historical["file"],
                      "span": historical["location"], "control_only": original_id == "4585",
                      "historical_limitation": "CS8604 compile limitation" if original_id == "2657"
                      else "historical compile-limited comparison",
                      "evidence": f"../../issue-92-pr6b/review/replay/mutant-{original_id}/attempt.json"}
        limitation.update(match_variant(historical, index, historical_sources, new_sources, checkout_sources))
        limitations.append(limitation)
    limitations.append({"original_id": "5531", "limitation": "runtime premise not verified",
                        "proof": "../child-b/proofs.md#p4-variant-5531"})
    if not complete:
        limitations.append({"limitation": "controller evidence does not establish complete execution"})
    mapping_complete = (len(rows) == 228 and all(row["match_status"] == "unique" for row in rows) and
                        len({identifier(row["matches"][0]["id"]) for row in rows
                             if row["match_status"] == "unique"}) == 228)
    for row in rows:
        if row["match_status"] != "unique":
            limitations.append({"original_id": row["original_id"], "limitation": row["match_status"]})
    summary_fields = ("mode", "command", "cwd", "environment_overrides", "started_utc", "finished_utc",
                      "elapsed_seconds", "exit_code", "exit_reason", "termination", "execution_evidence_complete",
                      "source_inputs_unchanged", "observed_generated_counts", "maximum_observed_generated_count",
                      "generated_mutant_cap", "wall_time_cap_seconds", "source_manifest_sha256")
    return {"schema_version": 1, **hashes, "tested_commit": source["tested_commit"],
            "tested_tree": source["tested_commit_tree"],
            "controller_summary": {key: controller.get(key) for key in summary_fields},
            "inventory": {"lines": len(selected), "variants": len(rows),
                          "category_counts": dict(sorted(collections.Counter(line["category"] for line in selected).items())),
                          "variant_category_counts": dict(sorted(collections.Counter(row["category"] for row in rows).items()))},
            "status_counts": counts, "generated_count": generated, "serialized_count": len(records),
            "score": score, "mapping_complete": mapping_complete, "execution_complete": bool(complete),
            "rows": rows, "assigned_line_summary": summaries, "survivors": survivors, "limitations": limitations}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", required=True, type=Path)
    parser.add_argument("--run-dir", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args(argv)
    hashes = {}
    # Reserve the path before consuming either report. Existing evidence is
    # never replaced, even by an error envelope.
    try:
        with args.output.open("x", encoding="utf-8") as output:
            try:
                result = extract(args.repo_root.resolve(), args.run_dir.resolve(), hashes)
                code = 0 if result["mapping_complete"] and result["execution_complete"] else 1
            except Exception as error:
                result = {"schema_version": 1, "execution_complete": False, "mapping_complete": False,
                          "error": f"{type(error).__name__}: {error}", "input_hashes": dict(hashes), **hashes}
                code = 1
            json.dump(result, output, indent=2, sort_keys=True, ensure_ascii=False)
            output.write("\n")
    except OSError as error:
        print(f"cannot exclusively create output {args.output}: {error}", file=sys.stderr)
        return 1
    print(json.dumps({"output": str(args.output), "mapping_complete": result["mapping_complete"],
                      "execution_complete": result["execution_complete"], "error": result.get("error")}))
    return code


if __name__ == "__main__":
    sys.exit(main())

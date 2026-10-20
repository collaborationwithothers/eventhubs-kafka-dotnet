#!/usr/bin/env python3
"""Select and replay a bounded, deterministic sample of Stryker mutants.

The selection is a minimum edge cover of survivor files and mutator types.
Actual child commands are reserved by controller.py against the shared ledger.
"""

import argparse
import difflib
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from datetime import datetime, timezone


def utc():
    return datetime.now(timezone.utc).isoformat()


def git_root(path):
    value = subprocess.check_output(["git", "-C", str(path), "rev-parse", "--show-toplevel"],
                                   text=True, stderr=subprocess.PIPE)
    return Path(value.strip()).resolve()


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    temporary.replace(path)


def id_key(value):
    value = str(value)
    return (0, int(value)) if value.isdigit() else (1, value)


def mutant_key(mutant):
    location = mutant.get("location") or {}
    start = location.get("start") or {}
    end = location.get("end") or {}
    return (
        mutant.get("file", ""),
        int(start.get("line", 0)), int(start.get("column", 0)),
        int(end.get("line", 0)), int(end.get("column", 0)),
        str(mutant.get("mutatorName", "")), id_key(mutant.get("id", "")),
    )


def edge_cover(survivors):
    """Return the deterministic minimum edge cover and audit graph details."""
    files = sorted({m["file"] for m in survivors})
    mutators = sorted({str(m.get("mutatorName", "")) for m in survivors})
    representatives = {}
    for mutant in sorted(survivors, key=mutant_key):
        representatives.setdefault((mutant["file"], str(mutant.get("mutatorName", ""))), mutant)
    graph = {
        "survivor_count": len(survivors),
        "file_vertices": len(files),
        "mutator_vertices": len(mutators),
        "edge_count": len(representatives),
        "files": files,
        "mutators": mutators,
    }
    if len(files) + len(mutators) > 64:
        return [], {**graph, "matching": [], "coverage_possible": False,
                    "minimum_survivor_edge_cover_size": None,
                    "minimum_cover_status": "not-computed-over-64-vertices",
                    "reason": "more than 64 distinct graph vertices"}

    adjacency = {file: sorted(t for f, t in representatives if f == file) for file in files}
    matched_file_for_type = {}

    def augment(file, seen):
        for mutator in adjacency[file]:
            if mutator in seen:
                continue
            seen.add(mutator)
            previous = matched_file_for_type.get(mutator)
            if previous is None or augment(previous, seen):
                matched_file_for_type[mutator] = file
                return True
        return False

    for file in files:
        augment(file, set())
    matching = sorted((file, mutator) for mutator, file in matched_file_for_type.items())
    chosen = set(matching)
    matched_files = {file for file, _ in matching}
    matched_types = {mutator for _, mutator in matching}
    for file in files:
        if file not in matched_files:
            chosen.add((file, adjacency[file][0]))
    for mutator in mutators:
        if mutator not in matched_types:
            incident = next((file for file in files if (file, mutator) in representatives), None)
            if incident is not None:
                chosen.add((incident, mutator))
    edges = [representatives[pair] for pair in sorted(chosen)]
    graph.update({
        "matching": [{"file": file, "mutator": mutator,
                      "mutant_id": str(representatives[(file, mutator)]["id"])}
                     for file, mutator in matching],
        "chosen_edges": [{"file": file, "mutator": mutator,
                          "mutant_id": str(representatives[(file, mutator)]["id"])}
                         for file, mutator in sorted(chosen)],
        "coverage_possible": True,
        "uncovered_files": [],
        "uncovered_mutators": [],
        "minimum_survivor_edge_cover_size": len(edges),
        "minimum_cover_status": "computed",
    })
    return edges, graph


def select(mutants, max_mutants, killed_sample):
    survivors = sorted((m for m in mutants if m.get("status") in ("Survived", "NoCoverage")), key=mutant_key)
    killed = sorted((m for m in mutants if m.get("status") == "Killed"), key=mutant_key)
    survivor_sample, graph = edge_cover(survivors)
    required_survivor_sample_count = len(survivor_sample)
    minimum_killed_required = 1 if killed else 0
    required_total_with_minimum_killed = (
        required_survivor_sample_count + minimum_killed_required
        if graph.get("minimum_cover_status") == "computed" else None
    )
    if required_survivor_sample_count > max_mutants:
        survivor_sample = survivor_sample[:max_mutants]
        graph["coverage_possible"] = False
        graph["reason"] = "minimum survivor edge cover exceeds the 32-mutant sample cap"
    elif required_total_with_minimum_killed is not None and required_total_with_minimum_killed > max_mutants:
        graph["coverage_possible"] = False
        graph["reason"] = "minimum survivor edge cover plus one required Killed mutant exceeds the 32-mutant sample cap"
    remaining = max_mutants - len(survivor_sample)
    if killed and remaining < 1:
        graph["coverage_possible"] = False
        graph["reason"] = "survivor edge cover leaves no slot for the required Killed sample"
    chosen_killed_count = min(killed_sample, max(0, remaining), len(killed))
    killed_selection = []
    if chosen_killed_count:
        first_by_file = {}
        for mutant in killed:
            first_by_file.setdefault(mutant.get("file", ""), mutant)
        for file in sorted(first_by_file):
            if len(killed_selection) == chosen_killed_count:
                break
            killed_selection.append(first_by_file[file])
        used = {str(m["id"]) for m in killed_selection}
        for mutant in killed:
            if len(killed_selection) == chosen_killed_count:
                break
            if str(mutant["id"]) not in used:
                killed_selection.append(mutant)
                used.add(str(mutant["id"]))
    selected = sorted(survivor_sample + killed_selection, key=mutant_key)
    selected_ids = [str(m["id"]) for m in selected]
    if len(selected_ids) != len(set(selected_ids)):
        graph["coverage_possible"] = False
        graph["duplicate_selected_ids"] = sorted(
            mutant_id for mutant_id in set(selected_ids) if selected_ids.count(mutant_id) > 1
        )
        graph["reason"] = "selected report records contain duplicate mutant IDs"
    if len(selected) > max_mutants:
        selected = selected[:max_mutants]
        selected_ids = [str(m["id"]) for m in selected]
        graph["coverage_possible"] = False
        graph["reason"] = "required sample exceeds the 32-mutant cap"
    if not selected:
        graph["coverage_possible"] = False
        graph.setdefault("reason", "report has no replayable survivor or Killed mutants")
    graph.update({
        "minimum_survivor_edge_cover_size": graph.get("minimum_survivor_edge_cover_size", required_survivor_sample_count),
        "required_survivor_sample_count": (required_survivor_sample_count
            if graph.get("minimum_cover_status") == "computed" else None),
        "minimum_killed_required": minimum_killed_required,
        "required_total_with_minimum_killed": required_total_with_minimum_killed,
        "survivor_sample_count": len(survivor_sample),
        "survivor_file_coverage": sorted({m["file"] for m in survivor_sample}),
        "survivor_mutator_coverage": sorted({str(m.get("mutatorName", "")) for m in survivor_sample}),
        "uncovered_files": sorted({m["file"] for m in survivors} - {m["file"] for m in survivor_sample}),
        "uncovered_mutators": sorted({str(m.get("mutatorName", "")) for m in survivors}
                                      - {str(m.get("mutatorName", "")) for m in survivor_sample}),
        "killed_available": len(killed),
        "killed_selected": len(killed_selection),
        "killed_unavailable": not killed,
        "selected_ids": selected_ids,
        "selected_count": len(selected),
    })
    if graph["uncovered_files"] or graph["uncovered_mutators"]:
        graph["coverage_possible"] = False
        graph.setdefault("reason", "survivor file and mutator coverage is incomplete")
    if killed and not killed_selection:
        graph["coverage_possible"] = False
        graph.setdefault("reason", "no slot remains for at least one available Killed mutant")
    return selected, graph


def utf16_index(text, line, column):
    """Translate one-based Stryker UTF-16 line/column into a Python index."""
    if line < 1 or column < 1:
        raise ValueError("mutation span must use positive one-based coordinates")
    lines = text.splitlines(keepends=True)
    if line > len(lines):
        if line == len(lines) + 1 and column == 1:
            return len(text)
        raise ValueError(f"mutation line {line} is outside embedded source")
    prefix = sum(len(value) for value in lines[:line - 1])
    units = 0
    for offset, character in enumerate(lines[line - 1]):
        if units == column - 1:
            return prefix + offset
        units += len(character.encode("utf-16-le")) // 2
        if units > column - 1:
            raise ValueError("mutation column splits a UTF-16 surrogate pair")
    if units == column - 1:
        return prefix + len(lines[line - 1])
    raise ValueError(f"mutation column {column} is outside embedded source line {line}")


def replacement_source(mutant):
    source = mutant.get("source")
    if not isinstance(source, str):
        raise ValueError(f"mutant {mutant.get('id')} has no embedded source")
    location = mutant.get("location") or {}
    start = location.get("start") or {}
    end = location.get("end") or {}
    begin = utf16_index(source, int(start.get("line", 0)), int(start.get("column", 0)))
    finish = utf16_index(source, int(end.get("line", 0)), int(end.get("column", 0)))
    if finish < begin:
        raise ValueError(f"mutant {mutant.get('id')} has a reversed source span")
    replacement = mutant.get("replacement")
    if not isinstance(replacement, str):
        raise ValueError(f"mutant {mutant.get('id')} has no string replacement")
    return source[:begin] + replacement + source[finish:]


def controller_call(args, mutant_id, child_name, cwd, run_dir, child_argv, timeout=180):
    command = [sys.executable, str(args.controller), "--mode", "replay",
               "--repo-root", str(cwd), "--cwd", str(cwd),
               "--run-dir", str(run_dir), "--tested-commit", args.tested_commit,
               "--ledger-path", str(args.ledger_path), "--mutant-id", mutant_id,
               "--seconds", str(timeout), "--env", "ParchwellVerifierMutationOnly=true"]
    if child_name == "test":
        results_index = child_argv.index("--results-directory") + 1
        command.extend(["--xml", str(Path(child_argv[results_index]) / "results.xml")])
    command.extend(["--", *child_argv])
    started = utc()
    try:
        result = subprocess.run(command, check=False)
        receipt_path = run_dir / "controller.json"
        record = {"child": child_name, "argv": child_argv, "started_utc": started,
                "finished_utc": utc(), "returncode": result.returncode,
                "controller_receipt": str(receipt_path)}
        try:
            record["controller_receipt_relative"] = str(receipt_path.relative_to(args.review_dir.parent))
        except ValueError:
            record["controller_receipt_relative"] = None
        return record
    except OSError as error:
        return {"child": child_name, "argv": child_argv, "started_utc": started,
                "finished_utc": utc(), "launch_error": f"{type(error).__name__}: {error}",
                "controller_receipt": str(run_dir / "controller.json"),
                "controller_receipt_relative": str((run_dir / "controller.json").relative_to(args.review_dir.parent))}


def classify(outcome, phase, result_xml=None):
    receipt_path = Path(outcome.get("controller_receipt", ""))
    if not receipt_path.is_file():
        return "infrastructure-failure"
    try:
        receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return "infrastructure-failure"
    outcome["controller_exit_reason"] = receipt.get("exit_reason")
    if receipt.get("exit_reason") == "elapsed-cap":
        return "timeout"
    if receipt.get("exit_reason") in ("launch-error", "controller-error", "controller-signal"):
        return "infrastructure-failure"
    if phase != "test":
        if outcome.get("returncode") == 0:
            return "command-passed"
    elif result_xml and Path(result_xml).is_file():
        try:
            root = ET.parse(result_xml).getroot()
            tests = root.findall(".//test")
            test_count = len(tests) or max((int(node.attrib.get("total", node.attrib.get("tests", "0")))
                                            for node in root.iter()), default=0)
            failed = max((int(node.attrib.get("failed", node.attrib.get("failures", "0")))
                          for node in root.iter()), default=0)
            failed = max(failed, sum(test.get("result") == "Fail" for test in tests))
            errors = max((int(node.attrib.get("errors", "0")) for node in root.iter()), default=0)
            if test_count <= 0:
                return "missing-output"
            if failed:
                return "assertion-failure"
            if errors:
                return "infrastructure-failure"
            if outcome.get("returncode") == 0:
                return "tests-passed"
        except (ET.ParseError, OSError, ValueError):
            return "missing-output"
    elif outcome.get("returncode") == 0:
        return "missing-output"
    if phase == "build":
        log_path = receipt_path.parent / receipt.get("command_log", "command.log.gz")
        try:
            import gzip
            with gzip.open(log_path, "rt", encoding="utf-8", errors="replace") as stream:
                log = stream.read()
            if re.search(r"\berror\s+(?:CS|NETSDK)\d+\b|Build FAILED\.", log):
                return "compile-failure"
        except OSError:
            pass
    return "command-failed-or-incomplete"


def run_child(args):
    parser = argparse.ArgumentParser()
    parser.add_argument("operation", choices=("baseline-restore-build",))
    parser.add_argument("--cwd", required=True)
    parser.add_argument("--record", required=True)
    child = parser.parse_args(args)
    cwd = Path(child.cwd)
    record = Path(child.record)
    commands = [
        ["dotnet", "restore", "Parchwell.slnx", "--locked-mode"],
        ["dotnet", "build", "tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj",
         "--configuration", "Release", "--no-restore", "-m:1"],
    ]
    outcomes = []
    for argv in commands:
        completed = subprocess.run(argv, cwd=cwd, check=False)
        outcomes.append({"argv": argv, "returncode": completed.returncode, "finished_utc": utc()})
        write_json(record, {"commands": outcomes})
        if completed.returncode:
            break
    write_json(record, {"commands": outcomes, "success": len(outcomes) == 2 and outcomes[-1]["returncode"] == 0})
    return 0 if len(outcomes) == 2 and outcomes[-1]["returncode"] == 0 else 1


def test_argv(project_dir, results_dir):
    return ["dotnet", "test", "--project", str(project_dir / "Parchwell.Verifier.Tests.csproj"),
            "--configuration", "Release", "--no-build", "--minimum-expected-tests", "1",
            "--results-directory", str(results_dir), "--", "--report-xunit-xml",
            "--report-xunit-xml-filename", "results.xml"]


def execute(args, selected, graph, mutation_dir):
    source_identity_path = args.run_dir / "tested-source.json"
    source_identity = json.loads(source_identity_path.read_text(encoding="utf-8"))
    if source_identity.get("tested_commit") != args.tested_commit:
        raise ValueError("tested commit does not match full-run source identity")
    work_parent = Path(tempfile.mkdtemp(prefix="issue-92-replay-"))
    worktree = work_parent / "checkout"
    overall = {"started_utc": utc(), "worktree": str(worktree), "tested_commit": args.tested_commit,
               "attempts": [], "baseline_control": [], "selected_ids": graph["selected_ids"]}
    progress_path = mutation_dir / "replay-progress.json"
    write_json(progress_path, overall)
    try:
        subprocess.run(["git", "-C", str(args.repo_root), "worktree", "add", "--detach",
                        str(worktree), args.tested_commit], check=True, capture_output=True, text=True)
        expected_inputs = source_identity.get("source_input_sha256", {})
        for relative, expected_hash in expected_inputs.items():
            path = worktree / relative
            actual_hash = hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None
            if actual_hash != expected_hash:
                raise ValueError(f"tested checkout input differs from full-run source identity: {relative}")
        project_dir = worktree / "tests/Parchwell.Verifier.Tests"
        baseline_dir = mutation_dir / "baseline-control"
        baseline_dir.mkdir(parents=True, exist_ok=False)
        setup_record = baseline_dir / "subcommands.json"
        setup_wrapper = [sys.executable, str(Path(__file__).resolve()),
                         "baseline-restore-build", "--cwd", str(worktree), "--record", str(setup_record)]
        outcome = controller_call(args, "baseline-control", "restore-and-build", worktree,
                                  baseline_dir / "restore-build", setup_wrapper)
        overall["baseline_control"].append(outcome)
        write_json(progress_path, overall)
        if outcome.get("returncode") != 0:
            overall["baseline_status"] = "restore-or-build-failed"
            return overall
        baseline_xml_dir = baseline_dir / "test-results"
        outcome = controller_call(args, "baseline-control", "test", worktree,
                                  baseline_dir / "test", test_argv(project_dir, baseline_xml_dir))
        outcome["classification"] = classify(outcome, "test", baseline_xml_dir / "results.xml")
        overall["baseline_control"].append(outcome)
        write_json(progress_path, overall)
        if outcome.get("classification") != "tests-passed":
            overall["baseline_status"] = "test-failed"
            return overall
        overall["baseline_status"] = "passed"

        for mutant in selected:
            mutant_id = str(mutant["id"])
            slot = mutation_dir / ("mutant-" + mutant_id)
            slot.mkdir(parents=True, exist_ok=False)
            relative = Path(mutant["file"])
            source_root = worktree / "src/Parchwell.Verifier"
            attempt = {"mutant_id": mutant_id, "status": mutant.get("status"), "file": str(relative),
                       "mutator": mutant.get("mutatorName"), "span": mutant.get("location"),
                       "replacement": mutant.get("replacement"), "report_status": mutant.get("status"),
                       "started_utc": utc(), "commands": []}
            target = None
            before_bytes = None
            changed = False
            try:
                target = (source_root / relative).resolve()
                if source_root.resolve() not in target.parents:
                    raise ValueError(f"mutant source path escapes the verifier project: {relative}")
                if not target.is_file():
                    raise ValueError(f"mutant source path is absent from tested checkout: {relative}")
                before_bytes = target.read_bytes()
                before = before_bytes.decode("utf-8")
                if before != mutant.get("source"):
                    raise ValueError(f"mutant {mutant_id} embedded source differs from tested checkout")
                after = replacement_source(mutant)
                diff = "".join(difflib.unified_diff(before.splitlines(keepends=True),
                                                    after.splitlines(keepends=True),
                                                    fromfile=str(relative) + " (before)",
                                                    tofile=str(relative) + " (mutated)"))
                (slot / "before-after.diff").write_text(diff, encoding="utf-8", newline="")
                target.write_bytes(after.encode("utf-8"))
                changed = True
                build = ["dotnet", "build", "tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj",
                         "--configuration", "Release", "--no-restore", "-m:1"]
                outcome = controller_call(args, mutant_id, "build", worktree,
                                          slot / "build", build)
                outcome["classification"] = classify(outcome, "build")
                attempt["commands"].append(outcome)
                if outcome.get("classification") == "command-passed":
                    outcome = controller_call(args, mutant_id, "test", worktree,
                                              slot / "test", test_argv(project_dir, slot / "test-results"))
                    outcome["classification"] = classify(outcome, "test", slot / "test-results/results.xml")
                    attempt["commands"].append(outcome)
                    attempt["classification"] = outcome["classification"]
                    if outcome["classification"] in ("tests-passed", "assertion-failure"):
                        expected_failure = mutant.get("status") == "Killed"
                        observed_failure = outcome["classification"] == "assertion-failure"
                        attempt["runner_comparison_complete"] = True
                        attempt["runner_disagreement"] = expected_failure != observed_failure
                    else:
                        attempt["runner_comparison_complete"] = False
                        attempt["runner_disagreement"] = False
                else:
                    attempt["classification"] = outcome["classification"]
                    attempt["runner_comparison_complete"] = False
                    attempt["runner_disagreement"] = False
            except Exception as error:
                attempt["classification"] = "source-input-mismatch" if isinstance(error, ValueError) else "infrastructure-failure"
                attempt["error"] = f"{type(error).__name__}: {error}"
                attempt["runner_comparison_complete"] = False
                attempt["runner_disagreement"] = False
            finally:
                if changed and target is not None and before_bytes is not None:
                    target.write_bytes(before_bytes)
                    attempt["restored_source_sha256"] = hashlib.sha256(target.read_bytes()).hexdigest()
                attempt["finished_utc"] = utc()
                write_json(slot / "attempt.json", attempt)
            overall["attempts"].append(attempt)
            write_json(progress_path, overall)
        return overall
    finally:
        try:
            subprocess.run(["git", "-C", str(args.repo_root), "worktree", "remove", "--force", str(worktree)],
                           check=False, capture_output=True, text=True)
        finally:
            shutil.rmtree(work_parent, ignore_errors=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-dir", required=True, type=Path,
                        help="completed full-run directory containing mutants.json and tested-source.json")
    parser.add_argument("--review-dir", required=True, type=Path)
    parser.add_argument("--max-mutants", type=int, default=32)
    parser.add_argument("--killed-sample", type=int, default=4)
    parser.add_argument("--command-seconds", type=int, default=180)
    parser.add_argument("--total-seconds", type=int, default=3600)
    parser.add_argument("--ledger-path", required=True, type=Path)
    parser.add_argument("--controller", type=Path)
    parser.add_argument("--select-only", action="store_true")
    args = parser.parse_args()
    if args.max_mutants != 32 or args.killed_sample != 4 or args.command_seconds != 180 or args.total_seconds != 3600:
        parser.error("issue 92 replay limits are fixed at 32 mutants, 4 Killed, 180 seconds and 3600 seconds")
    args.run_dir = args.run_dir.resolve()
    args.review_dir = args.review_dir.resolve()
    args.ledger_path = args.ledger_path.resolve()
    try:
        args.repo_root = git_root(args.run_dir)
    except (OSError, subprocess.CalledProcessError) as error:
        parser.error(f"cannot resolve repository root from run-dir: {error}")
    source_identity_path = args.run_dir / "tested-source.json"
    if not source_identity_path.is_file():
        parser.error("run-dir/tested-source.json is missing")
    source_identity = json.loads(source_identity_path.read_text(encoding="utf-8"))
    args.tested_commit = source_identity.get("tested_commit")
    if not isinstance(args.tested_commit, str) or not args.tested_commit:
        parser.error("tested-source.json has no tested_commit")
    args.controller = (args.controller or args.run_dir.parent / "controller.py").resolve()
    if not args.repo_root.is_dir() or not args.ledger_path.is_file() or not args.controller.is_file():
        parser.error("repository root, shared invocation ledger and controller must already exist")
    mutants_path = args.run_dir / "mutants.json"
    if not mutants_path.is_file():
        parser.error("run-dir/mutants.json is missing")
    mutants = json.loads(mutants_path.read_text(encoding="utf-8"))
    if not isinstance(mutants, list) or len(mutants) > 10000:
        parser.error("mutants.json must be an array of at most 10000 serialized records")
    extraction_path = args.run_dir / "extraction-summary.json"
    full_receipt_path = args.run_dir / "controller.json"
    if not extraction_path.is_file() or not full_receipt_path.is_file():
        parser.error("full-run extraction summary and controller receipt are required")
    extraction = json.loads(extraction_path.read_text(encoding="utf-8"))
    full_receipt = json.loads(full_receipt_path.read_text(encoding="utf-8"))
    report_hashes = sorted({str(mutant.get("report_sha256")) for mutant in mutants
                            if mutant.get("report_sha256")})
    report_sha256 = extraction.get("report_sha256")
    preconditions = {
        "extraction_complete": extraction.get("extraction_complete") is True,
        "report_receipt_completed": extraction.get("report_receipt_completed") is True,
        "report_hash_matches_receipt": extraction.get("receipt_hash_binding", {}).get(
            "report_hash_matches_uncompressed_report") is True,
        "mutants_share_report_hash": report_hashes == [report_sha256],
        "report_hash_matches_controller": full_receipt.get("report_sha256") == report_sha256,
    }
    selected, graph = select(mutants, args.max_mutants, args.killed_sample)
    graph["preconditions"] = preconditions
    graph["report_sha256"] = report_sha256
    if not all(preconditions.values()):
        graph["coverage_possible"] = False
        graph["reason"] = "full-run report or extraction identity is incomplete or inconsistent"
    args.review_dir.mkdir(parents=True, exist_ok=True)
    mutation_dir = args.review_dir / "replay"
    mutation_dir.mkdir(parents=True, exist_ok=False)
    write_json(mutation_dir / "selection.json", {"created_utc": utc(), "limits": {
        "max_mutants": args.max_mutants, "killed_sample_max": args.killed_sample,
        "command_seconds": args.command_seconds, "total_seconds": args.total_seconds,
    }, "graph": graph, "report_sha256": report_sha256, "selected": selected})
    if args.select_only or not graph.get("coverage_possible"):
        status = "selection-only" if args.select_only else "handoff-required"
        write_json(mutation_dir / "replay.json", {"started_utc": utc(), "status": status,
            "selection_status": "complete" if graph.get("coverage_possible") else "incomplete",
            "coverage": {"possible": graph.get("coverage_possible"),
                         "uncovered_files": graph.get("uncovered_files", []),
                         "uncovered_mutators": graph.get("uncovered_mutators", [])},
            "graph": graph, "attempts": []})
        return 0 if args.select_only else 2
    try:
        result = execute(args, selected, graph, mutation_dir)
    except Exception as error:
        progress_path = mutation_dir / "replay-progress.json"
        result = json.loads(progress_path.read_text(encoding="utf-8")) if progress_path.is_file() else {}
        result.update({"status": "incomplete", "selection_status": "complete", "graph": graph,
                       "error": f"{type(error).__name__}: {error}"})
    result["finished_utc"] = utc()
    result.setdefault("status", "completed" if len(result.get("attempts", [])) == len(selected) else "incomplete")
    result.setdefault("selection_status", "complete")
    result["coverage"] = {"possible": graph.get("coverage_possible"),
                           "uncovered_files": graph.get("uncovered_files", []),
                           "uncovered_mutators": graph.get("uncovered_mutators", [])}
    write_json(mutation_dir / "replay.json", result)
    return 0 if result.get("baseline_status") == "passed" and result.get("status") == "completed" else 1


if __name__ == "__main__":
    if sys.argv[1:2] == ["baseline-restore-build"]:
        raise SystemExit(run_child(sys.argv[1:]))
    raise SystemExit(main())

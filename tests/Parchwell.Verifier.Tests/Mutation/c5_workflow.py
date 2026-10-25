#!/usr/bin/env python3
"""Retain one C5 shard and combine four complete raw mutation populations."""

from __future__ import annotations

import argparse
import copy
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import signal
import subprocess
import time


SHARDS = {
    "phase": "Judge/PhaseJudge.cs",
    "publication": "VerifyRun/Publication/PublicationAcceptance.cs",
    "a01": "VerifyRun/A01/A01EvidenceRecorder.cs",
    "workload": "VerifyRun/A01/WorkloadRecorder.cs",
}
BASE_CONFIG = {"stryker-config": {
    "project": "Parchwell.Verifier.csproj", "test-runner": "mtp",
    "configuration": "Release", "target-framework": "net10.0",
    "reporters": ["json", "html", "progress"],
    "thresholds": {"high": 80, "low": 60, "break": 60},
    "mutate": list(SHARDS.values()), "coverage-analysis": "off",
}}
STATUSES = ("Killed", "Timeout", "Survived", "NoCoverage", "CompileError",
            "RuntimeError", "Ignored")
SOURCE_PATHS = ("src", "tests/Parchwell.Verifier.Tests", ".config/dotnet-tools.json",
                "global.json", "Directory.Build.props", "Directory.Build.targets",
                "Directory.Packages.props", "NuGet.config", "Parchwell.slnx")
CONFIG_PATH = Path("tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json")
REPORT_PATH = Path("stryker/reports/mutation-report.json")
OVERRIDES = {"ParchwellVerifierMutationOnly": "true"}
RECEIPT_FIELDS = set("""shard target commit tree run_id run_attempt argv cwd
    environment_overrides started_at_utc finished_at_utc elapsed_seconds
    child_exit_code termination_reason command_completed source_sha256_before
    source_sha256_after tracked_clean_before tracked_clean_after config_sha256_before
    config_sha256_after report_sha256 command_log_sha256""".split())


def sha256(path: Path) -> str:
    with path.open("rb") as stream:
        digest = hashlib.sha256()
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
        return digest.hexdigest()


def git(repository: Path, *args: str) -> bytes:
    return subprocess.check_output(["git", "-C", str(repository), *args])


def identity(repository: Path) -> tuple[str, str, bool]:
    return (git(repository, "rev-parse", "HEAD").decode().strip(),
            git(repository, "rev-parse", "HEAD^{tree}").decode().strip(),
            not git(repository, "status", "--porcelain", "--untracked-files=no"))


def snapshot(repository: Path) -> dict[str, str]:
    names = git(repository, "ls-files", "-z", "--", *SOURCE_PATHS).decode().split("\0")
    return {name: sha256(repository / name) for name in names
            if name and (repository / name).is_file()}


def shard_config(shard: str) -> dict:
    config = copy.deepcopy(BASE_CONFIG)
    config["stryker-config"]["mutate"] = [SHARDS[shard]]
    return config


def write_json(path: Path, value: object) -> None:
    with path.open("x", encoding="utf-8") as stream:
        json.dump(value, stream, indent=2)
        stream.write("\n")


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def stop_group(process: subprocess.Popen) -> None:
    try:
        os.killpg(process.pid, signal.SIGKILL)
    except ProcessLookupError:
        pass
    process.wait()


def run_shard(shard: str, output_dir: Path, *, repository: Path | None = None,
              command_prefix: list[str] | None = None, timeout: float = 16200) -> int:
    repository = (repository or Path(__file__).resolve().parents[3]).resolve()
    output_dir = Path(output_dir).resolve()
    try:
        target = SHARDS[shard]
        if output_dir.exists():
            raise FileExistsError("output directory already exists")
        commit, tree, clean = identity(repository)
        if (commit != os.environ.get("GITHUB_SHA")
                or os.environ.get("GITHUB_REF") != "refs/heads/main" or not clean):
            raise ValueError("shard requires the clean workflow main commit")
        if json.loads((repository / CONFIG_PATH).read_text()) != BASE_CONFIG:
            raise ValueError("unexpected committed C5 configuration")
        before = snapshot(repository)
        output_dir.mkdir(parents=True, exist_ok=False)
    except (KeyError, OSError, ValueError, subprocess.SubprocessError) as error:
        print(f"C5 preparation failed: {error}")
        return 1

    config_path = output_dir / "shard-config.json"
    log_path = output_dir / "command.log"
    report_path = output_dir / REPORT_PATH
    write_json(config_path, shard_config(shard))
    config_before = sha256(config_path)
    argv = [*(command_prefix or ["dotnet", "stryker"]), "--config-file", str(config_path),
            "--output", str(output_dir / "stryker"), "--log-to-file", "--verbosity", "debug"]
    cwd = repository / "tests/Parchwell.Verifier.Tests"
    started = utc_now()
    start = time.monotonic()
    process = None
    child_exit = None
    reason = None
    previous_termination_handler = signal.getsignal(signal.SIGTERM)

    def interrupt(signum, frame):
        raise KeyboardInterrupt

    signal.signal(signal.SIGTERM, interrupt)
    try:
        with log_path.open("xb") as log:
            process = subprocess.Popen(argv, cwd=cwd, env={**os.environ, **OVERRIDES},
                                       stdout=log, stderr=subprocess.STDOUT,
                                       start_new_session=True)
            try:
                child_exit = process.wait(timeout=timeout)
            except subprocess.TimeoutExpired:
                reason = "time_cap_exceeded"
                stop_group(process)
                child_exit = process.returncode
    except (KeyboardInterrupt, SystemExit):
        reason = "interrupted"
        if process is not None:
            stop_group(process)
            child_exit = process.returncode
    except OSError:
        reason = "launch_failed"
        if process is not None:
            stop_group(process)
            child_exit = process.returncode
    finally:
        signal.signal(signal.SIGTERM, previous_termination_handler)
        finished = utc_now()
        elapsed = time.monotonic() - start
        after = {}
        clean_after = False
        unchanged_identity = False
        try:
            after = snapshot(repository)
            after_commit, after_tree, clean_after = identity(repository)
            unchanged_identity = (after_commit, after_tree) == (commit, tree)
        except (OSError, subprocess.SubprocessError):
            pass
        if not unchanged_identity:
            clean_after = False
        config_after = sha256(config_path) if config_path.is_file() else None
        receipt = {
            "shard": shard, "target": target, "commit": commit, "tree": tree,
            "run_id": os.environ.get("GITHUB_RUN_ID"),
            "run_attempt": os.environ.get("GITHUB_RUN_ATTEMPT"),
            "argv": argv, "cwd": str(cwd), "environment_overrides": OVERRIDES,
            "started_at_utc": started, "finished_at_utc": finished,
            "elapsed_seconds": elapsed, "child_exit_code": child_exit,
            "termination_reason": reason,
            "command_completed": child_exit is not None and reason is None,
            "source_sha256_before": before, "source_sha256_after": after,
            "tracked_clean_before": clean, "tracked_clean_after": clean_after,
            "config_sha256_before": config_before, "config_sha256_after": config_after,
            "report_sha256": sha256(report_path) if report_path.is_file() else None,
            "command_log_sha256": sha256(log_path) if log_path.is_file() else None,
        }
        write_json(output_dir / "receipt.json", receipt)
    return 0 if (receipt["command_completed"] and child_exit in (0, 2)
                 and clean_after and before == after and before
                 and config_before == config_after and receipt["report_sha256"]) else 1


def require(condition: object, message: str) -> None:
    if not condition:
        raise ValueError(message)


def report_records(report: dict, target: str) -> list[dict]:
    require(isinstance(report, dict) and report.get("schemaVersion") == "2",
            "unsupported report schema")
    root = report.get("projectRoot")
    require(isinstance(root, str) and isinstance(report.get("files"), dict),
            "malformed report paths")
    prefix = root.replace("\\", "/").rstrip("/") + "/"
    files = {}
    for key, source in report["files"].items():
        require(isinstance(key, str), "malformed file key")
        path = key.replace("\\", "/")
        if path.startswith(prefix):
            path = path[len(prefix):]
        require(path not in files, "duplicate normalized file path")
        files[path] = source
    assigned = files.get(target)
    require(isinstance(assigned, dict) and assigned.get("language") == "cs"
            and isinstance(assigned.get("source"), str)
            and isinstance(assigned.get("mutants"), list) and assigned["mutants"],
            "assigned target missing or malformed")
    for path, source in files.items():
        if path == target:
            continue
        require(isinstance(source, dict) and isinstance(source.get("mutants"), list),
                "malformed foreign file")
        sentinel = (source.get("language") == "none"
                    and source.get("source") == "File ignored by mutate filter"
                    and source["mutants"] == [])
        require(sentinel or all(isinstance(row, dict)
                                and row.get("status") in ("Ignored", "CompileError")
                                for row in source["mutants"]),
                "foreign mutant with disallowed status")
    records = []
    seen = set()
    for mutant in assigned["mutants"]:
        require(isinstance(mutant, dict)
                and all(isinstance(mutant.get(field), str)
                        for field in ("id", "mutatorName", "replacement", "status")),
                "malformed mutant")
        require(mutant["id"] not in seen, "duplicate target/id")
        seen.add(mutant["id"])
        require(mutant["status"] in STATUSES, "unfinished or unknown mutant status")
        location = mutant.get("location")
        require(isinstance(location, dict), "malformed mutant location")
        for edge in ("start", "end"):
            position = location.get(edge)
            require(isinstance(position, dict)
                    and all(type(position.get(axis)) is int and position[axis] > 0
                            for axis in ("line", "column")), "malformed mutant location")
        records.append({**mutant, "file": target})
    return records


def populations(records: list[dict]) -> tuple[dict, int, int]:
    counts = {status: 0 for status in STATUSES}
    for row in records:
        counts[row["status"]] += 1
    numerator = counts["Killed"] + counts["Timeout"]
    return counts, numerator, numerator + counts["Survived"] + counts["NoCoverage"]


def aggregate(input_dir: Path, expected_commit: str, run_id: str, run_attempt: str,
              output_dir: Path) -> int:
    input_dir, output_dir = Path(input_dir), Path(output_dir)
    try:
        output_dir.mkdir(parents=True, exist_ok=False)
    except OSError as error:
        print(f"C5 aggregate preparation failed: {error}")
        return 1
    prefix = f"c5-mutation-{run_id}-{run_attempt}-"
    reasons = []
    expected = {prefix + shard for shard in SHARDS}
    try:
        if input_dir.exists():
            extras = {p.name for p in input_dir.iterdir()
                      if p.is_dir() and p.name.startswith(prefix)} - expected
            if extras:
                reasons.append("unexpected shard directories: " + ", ".join(sorted(extras)))
    except OSError as error:
        reasons.append(f"cannot enumerate shard directory: {error}")
    reference_sources = None
    reference_tree = None
    shards = {}
    records = []
    for shard, target in SHARDS.items():
        directory = input_dir / (prefix + shard)
        try:
            receipt = json.loads((directory / "receipt.json").read_text())
            config_path = directory / "shard-config.json"
            report_path = directory / REPORT_PATH
            report_bytes = report_path.read_bytes()
            report = json.loads(report_bytes)
            require(isinstance(receipt, dict), "malformed receipt")
            require(set(receipt) == RECEIPT_FIELDS, "malformed receipt fields")
            require(receipt["environment_overrides"] == OVERRIDES
                    and isinstance(receipt["argv"], list) and receipt["argv"]
                    and all(isinstance(arg, str) for arg in receipt["argv"])
                    and isinstance(receipt["cwd"], str), "malformed invocation receipt")
            require(all(receipt.get(key) == value for key, value in {
                "shard": shard, "target": target, "commit": expected_commit,
                "run_id": run_id, "run_attempt": run_attempt,
                "command_completed": True, "termination_reason": None,
                "tracked_clean_before": True, "tracked_clean_after": True,
            }.items()), "receipt identity/completion mismatch")
            require(type(receipt.get("child_exit_code")) is int
                    and receipt["child_exit_code"] in (0, 2), "child execution error")
            require(receipt.get("command_completed") is True
                    and receipt.get("tracked_clean_before") is True
                    and receipt.get("tracked_clean_after") is True,
                    "invalid completion/clean flags")
            sources = receipt.get("source_sha256_before")
            require(isinstance(sources, dict) and sources
                    and all(isinstance(path, str) and isinstance(digest, str)
                            for path, digest in sources.items())
                    and sources == receipt.get("source_sha256_after"), "source drift")
            tree = receipt.get("tree")
            require(isinstance(tree, str) and tree, "missing source tree")
            if reference_sources is not None:
                require(sources == reference_sources and tree == reference_tree,
                        "cross-shard source mismatch")
            reference_sources, reference_tree = sources, tree
            actual_config_hash = sha256(config_path)
            require(actual_config_hash == receipt.get("config_sha256_before")
                    == receipt.get("config_sha256_after"), "config hash mismatch")
            require(json.loads(config_path.read_text()) == shard_config(shard),
                    "unexpected shard configuration")
            report_hash = hashlib.sha256(report_bytes).hexdigest()
            require(report_hash == receipt.get("report_sha256"), "report hash mismatch")
            require(sha256(directory / "command.log") == receipt.get("command_log_sha256"),
                    "command log hash mismatch")
            selected = report_records(report, target)
            counts, numerator, denominator = populations(selected)
            require(denominator > 0, "assigned target has no score population")
            shards[shard] = {"target": target, "status_counts": counts,
                             "numerator": numerator, "denominator": denominator,
                             "child_exit_code": receipt["child_exit_code"],
                             "report_sha256": report_hash}
            records.extend(selected)
        except (OSError, ValueError, TypeError, KeyError) as error:
            reasons.append(f"{shard}: {error}")
    counts, numerator, denominator = populations(records)
    if denominator == 0:
        reasons.append("combined score population is empty")
    complete = not reasons
    passed = 100 * numerator >= 60 * denominator if complete else None
    summary = {
        "commit": expected_commit, "tree": reference_tree, "run_id": run_id,
        "run_attempt": run_attempt, "measurement_complete": complete,
        "incomplete_reasons": reasons, "shards": shards, "status_counts": counts,
        "numerator": numerator, "denominator": denominator,
        "score_percent": 100 * numerator / denominator if complete else None,
        "break_passed": passed,
    }
    write_json(output_dir / "summary.json", summary)
    if complete:
        text = (f"Complete C5 measurement {'passes' if passed else 'does not pass'} break 60.\n\n"
                f"Raw score: {numerator}/{denominator} ({summary['score_percent']:.2f}%).\n")
    else:
        text = "Incomplete C5 measurement; no passing score is available.\n\n" + "\n".join(
            "- " + reason for reason in reasons) + "\n"
    text += (f"\nCommit: `{expected_commit}`. Tree: `{reference_tree}`. "
             f"Run: `{run_id}`, attempt `{run_attempt}`.\n\n"
             "| Population | " + " | ".join(STATUSES)
             + " | Numerator | Denominator |\n"
             + "| --- |" + " ---: |" * (len(STATUSES) + 2) + "\n")
    for label, population in [*shards.items(), ("Combined", summary)]:
        text += ("| " + label + " | "
                 + " | ".join(str(population["status_counts"][status]) for status in STATUSES)
                 + f" | {population['numerator']} | {population['denominator']} |\n")
    text += "\nIncomplete populations above are partial observations, not a mutation score.\n" if not complete else ""
    with (output_dir / "summary.md").open("x", encoding="utf-8") as stream:
        stream.write(text)
    survivors = sorted((row for row in records if row["status"] in ("Survived", "NoCoverage")),
                       key=lambda row: (row["file"], row["location"]["start"]["line"],
                                        row["location"]["start"]["column"],
                                        row["location"]["end"]["line"],
                                        row["location"]["end"]["column"], row["id"]))
    write_json(output_dir / "survivors.json", {"survivors": survivors,
               "disposition_sample": survivors[:50],
               "unreviewed_beyond_cap": max(0, len(survivors) - 50)})
    return 0 if complete and passed else 1


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    runner = commands.add_parser("run-shard")
    runner.add_argument("--shard", required=True, choices=SHARDS)
    runner.add_argument("--output-dir", required=True, type=Path)
    combiner = commands.add_parser("aggregate")
    combiner.add_argument("--input-dir", required=True, type=Path)
    combiner.add_argument("--expected-commit", required=True)
    combiner.add_argument("--run-id", required=True)
    combiner.add_argument("--run-attempt", required=True)
    combiner.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()
    if args.command == "run-shard":
        return run_shard(args.shard, args.output_dir)
    return aggregate(args.input_dir, args.expected_commit, args.run_id,
                     args.run_attempt, args.output_dir)


if __name__ == "__main__":
    raise SystemExit(main())

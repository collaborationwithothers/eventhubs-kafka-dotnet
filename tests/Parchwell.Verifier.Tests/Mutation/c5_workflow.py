#!/usr/bin/env python3
"""Retain one C5 shard and combine six complete raw mutation populations."""

from __future__ import annotations

import argparse
import copy
from datetime import datetime, timezone
import functools
import hashlib
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import time
import tempfile

import c5_projection as projection


TARGETS = {
    "phase": ("Judge/PhaseJudge.cs", 1),
    "publication": ("VerifyRun/Publication/PublicationAcceptance.cs", 1),
    "a01": ("VerifyRun/A01/A01EvidenceRecorder.cs", 1),
    "workload": ("VerifyRun/A01/WorkloadRecorder.cs", 1),
    "single-action-repeat": ("VerifyRun/SingleAction/SingleActionRepeatJudgement.cs", 3),
    "single-action-configuration": ("VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs", 6),
}
PARTS = {key if count == 1 else f"{key}-p{index}": (key, index, count)
         for key, (_, count) in TARGETS.items() for index in range(1, count + 1)}
SHARDS = {shard: TARGETS[key][0] for shard, (key, _, _) in PARTS.items()}
SPAN_END = 2147483647
UTF8_BOM = b"\xef\xbb\xbf"
FILTERED = ("Ignored", "Removed by mutate filter")
IDENTITY = ("mutatorName", "replacement", "location")
REBALANCE_SECONDS = 7200
LOCAL_PURPOSE = "Local C5 check: diagnostic only, never acceptance evidence."
BASE_CONFIG = {"stryker-config": {
    "project": "Parchwell.Verifier.csproj", "test-runner": "mtp",
    "configuration": "Release", "target-framework": "net10.0",
    "reporters": ["json", "html", "progress"],
    "thresholds": {"high": 80, "low": 60, "break": 60},
    "mutate": [target for target, _ in TARGETS.values()], "coverage-analysis": "off",
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
    config_sha256_after report_sha256 command_log_sha256 projection_manifest_sha256""".split())


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


def target_text(data: bytes) -> str:
    """Decode a target as Stryker counts its offsets: UTF-8 without a leading byte order mark."""
    return (data[len(UTF8_BOM):] if data.startswith(UTF8_BOM) else data).decode("utf-8")


def utf16_length(text: str) -> int:
    return len(text.encode("utf-16-le")) // 2


def member_boundaries(text: str) -> list[tuple[int, int]]:
    """UTF-16 offset and 1-based line of each line start that follows a line exactly '    }'."""
    boundaries = []
    position = 0
    total = utf16_length(text)
    for number, line in enumerate(text.split("\n")[:-1], start=2):
        position += utf16_length(line) + 1
        if line.removesuffix("\r") == "    }" and position < total:
            boundaries.append((position, number))
    return boundaries


def spans(text: str, count: int) -> list[tuple[int, int]]:
    """Split a target at member boundaries into count contiguous spans, minimizing the longest."""
    if type(count) is not int or count < 1:
        raise ValueError("span count must be a positive integer")
    length = utf16_length(text)
    if length >= SPAN_END:
        raise ValueError("target too long for span selection")
    edges = [0, *(offset for offset, _ in member_boundaries(text)), length]
    chunks = len(edges) - 1
    if count > chunks:
        raise ValueError("too few member boundaries for span count")

    @functools.lru_cache(maxsize=None)
    def best(first: int, parts: int) -> tuple[int, tuple[int, ...]]:
        if parts == 1:
            return edges[chunks] - edges[first], ()
        options = []
        for cut in range(first + 1, chunks - parts + 2):
            largest, rest = best(cut, parts - 1)
            options.append((max(edges[cut] - edges[first], largest), (edges[cut], *rest)))
        return min(options)

    bounds = [0, *best(0, count)[1], SPAN_END]
    return list(zip(bounds, bounds[1:]))


def shard_span(shard: str, source: bytes | None) -> tuple[int, int] | None:
    key, index, count = PARTS[shard]
    if count == 1:
        return None
    if source is None:
        raise ValueError("span shard requires target source")
    return spans(target_text(source), count)[index - 1]


def shard_config(shard: str, source: bytes | None = None) -> dict:
    target = SHARDS[shard]
    span = shard_span(shard, source)
    config = copy.deepcopy(BASE_CONFIG)
    config["stryker-config"]["mutate"] = [target if span is None else f"{target}{{{span[0]}..{span[1]}}}"]
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


def launch(argv: list[str], cwd: Path, log_path: Path, timeout: float) -> tuple[int | None, str | None]:
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
    return child_exit, reason


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

    staging = tempfile.TemporaryDirectory(prefix="c5-source-")
    staging_root = Path(staging.name) / "repository"
    staging_root.mkdir()
    try:
        projected = projection.prepare(repository, staging_root, target, output_dir / "projection-src")
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        staging.cleanup()
        print(f"C5 projection failed: {error}")
        return 1
    try:
        config = shard_config(shard, (staging_root / (projection.VERIFIER + target)).read_bytes())
    except (OSError, ValueError) as error:
        staging.cleanup()
        print(f"C5 span selection failed: {error}")
        return 1
    config_path = output_dir / "shard-config.json"
    log_path = output_dir / "command.log"
    report_path = output_dir / REPORT_PATH
    write_json(config_path, config)
    config_before = sha256(config_path)
    argv = [*(command_prefix or ["dotnet", "stryker"]), "--config-file", str(config_path),
            "--output", str(output_dir / "stryker"), "--log-to-file", "--verbosity", "debug"]
    cwd = staging_root / "tests/Parchwell.Verifier.Tests"
    started = utc_now()
    start = time.monotonic()
    child_exit = None
    reason = None
    try:
        child_exit, reason = launch(argv, cwd, log_path, timeout)
    finally:
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
        projection_path = output_dir / "projection.json"
        projection.finish(staging_root, projected, projection_path)
        projection_valid = False
        try:
            projection.validate(projected, output_dir / "projection-src", target, before)
            projection_valid = True
        except (OSError, ValueError, TypeError, KeyError):
            pass
        staging.cleanup()
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
            "projection_manifest_sha256": sha256(projection_path),
            "command_log_sha256": sha256(log_path) if log_path.is_file() else None,
        }
        write_json(output_dir / "receipt.json", receipt)
    return 0 if (receipt["command_completed"] and child_exit in (0, 2)
                 and clean_after and before == after and before
                 and projection_valid and config_before == config_after and receipt["report_sha256"]) else 1


def require(condition: object, message: str) -> None:
    if not condition:
        raise ValueError(message)


def report_records(report: dict, target: str, *, source_sha256: str | None = None) -> list[dict]:
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
    if source_sha256 is not None:
        require(hashlib.sha256(assigned["source"].encode()).hexdigest() == source_sha256,
                "report target source mismatch")
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


def reconcile(parts: list[list[dict]]) -> tuple[list[list[dict]], list[dict]]:
    """Return each span's owned records and the target population, failing closed on gaps and overlaps."""
    if len(parts) == 1:
        return [list(parts[0])], list(parts[0])
    indexed = [{row["id"]: row for row in rows} for rows in parts]
    order = [row["id"] for row in parts[0]]
    if any(set(rows) != set(order) for rows in indexed):
        raise ValueError("span inventories differ")
    owned = [[] for _ in parts]
    reconciled = []
    for identifier in order:
        rows = [records[identifier] for records in indexed]
        if any([row.get(field) for field in IDENTITY] != [rows[0].get(field) for field in IDENTITY]
               for row in rows):
            raise ValueError(f"mutant {identifier} identity differs across spans")
        dispositions = {(row["status"], row.get("statusReason")) for row in rows}
        owners = [index for index, row in enumerate(rows)
                  if (row["status"], row.get("statusReason")) != FILTERED]
        if len(dispositions) == 1 and rows[0]["status"] == "CompileError":
            reconciled.append(rows[0])
        elif len(dispositions) == 1 and rows[0]["status"] == "Ignored" and owners:
            reconciled.append(rows[0])
        elif any(row["status"] == "CompileError" for row in rows):
            raise ValueError(f"mutant {identifier} compile error differs across spans")
        elif not owners:
            raise ValueError(f"mutant {identifier} has no owning span")
        elif len(owners) > 1:
            raise ValueError(f"mutant {identifier} has more than one owning span")
        else:
            owned[owners[0]].append(rows[owners[0]])
            reconciled.append(rows[owners[0]])
    return owned, reconciled


def retained_span(directory: Path, target: str) -> list[int] | None:
    """Span named by a shard's retained configuration, for the recorded-only rebalance list."""
    try:
        mutate = json.loads((directory / "shard-config.json").read_text())["stryker-config"]["mutate"]
    except (OSError, ValueError, TypeError, KeyError):
        return None
    if not (isinstance(mutate, list) and len(mutate) == 1 and isinstance(mutate[0], str)):
        return None
    match = re.fullmatch(re.escape(target) + r"\{(\d+)\.\.(\d+)\}", mutate[0])
    return None if match is None else [int(match[1]), int(match[2])]


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
    reference_projection_sources = None
    shards = {}
    targets = {}
    records = []
    part_records = {}
    for shard, target in SHARDS.items():
        key, index, count = PARTS[shard]
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
            elapsed = receipt.get("elapsed_seconds")
            require(type(elapsed) in (int, float) and elapsed >= 0, "malformed elapsed time")
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
            report_hash = hashlib.sha256(report_bytes).hexdigest()
            require(report_hash == receipt.get("report_sha256"), "report hash mismatch")
            require(sha256(directory / "command.log") == receipt.get("command_log_sha256"),
                    "command log hash mismatch")
            projection_path = directory / "projection.json"
            require(sha256(projection_path) == receipt["projection_manifest_sha256"],
                    "projection manifest hash mismatch")
            projection_manifest = json.loads(projection_path.read_text())
            target_hash = projection.validate(projection_manifest, directory / "projection-src", target, sources)
            full_sources = projection_manifest["original_sha256"]
            require(reference_projection_sources is None or full_sources == reference_projection_sources,
                    "cross-shard projection input mismatch")
            reference_projection_sources = full_sources
            source = (directory / "projection-src" / (projection.VERIFIER + target)).read_bytes()
            require(json.loads(config_path.read_text()) == shard_config(shard, source),
                    "unexpected shard configuration")
            span = shard_span(shard, source)
            part_records[shard] = report_records(report, target, source_sha256=target_hash)
            shards[shard] = {"target": target, "part": index, "parts": count,
                             "span": None if span is None else list(span),
                             "elapsed_seconds": elapsed,
                             "child_exit_code": receipt["child_exit_code"],
                             "report_sha256": report_hash}
        except (OSError, ValueError, TypeError, KeyError) as error:
            reasons.append(f"{shard}: {error}")
    for key, (target, count) in TARGETS.items():
        members = [shard for shard, (owner, _, _) in PARTS.items() if owner == key]
        if any(shard not in part_records for shard in members):
            if count > 1:
                reasons.append(f"{key}: target incomplete because a span shard is incomplete")
            continue
        try:
            owned, reconciled = reconcile([part_records[shard] for shard in members])
            target_counts, target_numerator, target_denominator = populations(reconciled)
            require(target_denominator > 0, "assigned target has no score population")
        except ValueError as error:
            reasons.append(f"{key}: {error}")
            continue
        for shard, rows in zip(members, owned, strict=True):
            part_counts, part_numerator, part_denominator = populations(rows)
            shards[shard].update({"status_counts": part_counts, "numerator": part_numerator,
                                  "denominator": part_denominator})
        targets[key] = {"target": target, "parts": count, "status_counts": target_counts,
                        "numerator": target_numerator, "denominator": target_denominator}
        records.extend(reconciled)
    rebalance = []
    for shard, (_, part, parts) in PARTS.items():
        directory = input_dir / (prefix + shard)
        target = SHARDS[shard]
        try:
            elapsed = json.loads((directory / "receipt.json").read_text())["elapsed_seconds"]
        except (OSError, ValueError, TypeError, KeyError):
            continue
        if type(elapsed) in (int, float) and elapsed > REBALANCE_SECONDS:
            if shard in shards:
                span, verified = shards[shard]["span"], True
            else:
                span, verified = retained_span(directory, target), False
            rebalance.append({"shard": shard, "target": target, "part": part, "parts": parts,
                              "span": span, "span_verified": verified, "elapsed_seconds": elapsed})
    counts, numerator, denominator = populations(records)
    if denominator == 0:
        reasons.append("combined score population is empty")
    complete = not reasons
    passed = 100 * numerator >= 60 * denominator if complete else None
    summary = {
        "commit": expected_commit, "tree": reference_tree, "run_id": run_id,
        "run_attempt": run_attempt, "measurement_complete": complete,
        "incomplete_reasons": reasons, "shards": shards, "targets": targets,
        "status_counts": counts,
        "numerator": numerator, "denominator": denominator,
        "score_percent": 100 * numerator / denominator if complete else None,
        "break_passed": passed,
        "rebalance_threshold_seconds": REBALANCE_SECONDS, "rebalance_needed": rebalance,
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
    for label, population in [*targets.items(), ("Combined", summary)]:
        text += ("| " + label + " | "
                 + " | ".join(str(population["status_counts"][status]) for status in STATUSES)
                 + f" | {population['numerator']} | {population['denominator']} |\n")
    text += "\nIncomplete populations above are partial observations, not a mutation score.\n" if not complete else ""
    if rebalance:
        text += "\nRebalance needed (recorded only; not a gate):\n"
        for entry in rebalance:
            text += (f"- `{entry['shard']}` took {entry['elapsed_seconds']:.0f} s, over {REBALANCE_SECONDS} s: "
                     f"split part {entry['part']} of {entry['parts']} of `{entry['target']}`")
            if entry["span"] is not None:
                text += f", span {entry['span'][0]}..{entry['span'][1]}"
                if entry["span_verified"] is False:
                    text += " (unverified)"
            text += ".\n"
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


def parse_span(value: str) -> tuple[int, int]:
    match = re.fullmatch(r"(\d+)\.\.(\d+)", value)
    if match is None:
        raise ValueError("span must be START..END")
    start, end = int(match[1]), int(match[2])
    if not start < end <= SPAN_END:
        raise ValueError("span must satisfy START < END <= 2147483647")
    return start, end


def print_member_boundaries(target: str, repository: Path | None = None) -> int:
    repository = (repository or Path(__file__).resolve().parents[3]).resolve()
    try:
        require(target in BASE_CONFIG["stryker-config"]["mutate"], "unknown C5 target")
        text = target_text((repository / (projection.VERIFIER + target)).read_bytes())
    except (OSError, ValueError) as error:
        print(f"C5 member boundaries failed: {error}")
        return 1
    for offset, line in member_boundaries(text):
        print(offset, line)
    return 0


def local_check(target: str, span: str, output_dir: Path, *, timeout: float,
                repository: Path | None = None, command_prefix: list[str] | None = None) -> int:
    """Run one member-boundary span of one target on the committed HEAD; never acceptance evidence."""
    repository = (repository or Path(__file__).resolve().parents[3]).resolve()
    output_dir = Path(output_dir).resolve()
    try:
        require(target in BASE_CONFIG["stryker-config"]["mutate"], "unknown C5 target")
        require(type(timeout) in (int, float) and 0 < timeout <= 16200,
                "timeout must be above 0 and at most 16200 seconds")
        start, end = parse_span(span)
        if output_dir.exists():
            raise FileExistsError("output directory already exists")
        commit, tree, clean = identity(repository)
        require(clean, "local check requires a clean tracked checkout")
        output_dir.mkdir(parents=True, exist_ok=False)
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        print(f"C5 local check preparation failed: {error}")
        return 1

    staging = tempfile.TemporaryDirectory(prefix="c5-local-")
    staging_root = Path(staging.name) / "repository"
    staging_root.mkdir()
    try:
        projection.prepare(repository, staging_root, target, output_dir / "projection-src")
        text = target_text((staging_root / (projection.VERIFIER + target)).read_bytes())
        allowed = {0, SPAN_END, *(offset for offset, _ in member_boundaries(text))}
        require(start in allowed and end in allowed, "span must start and end at member boundaries")
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        staging.cleanup()
        print(f"C5 local check preparation failed: {error}")
        return 1
    config_path = output_dir / "local-config.json"
    log_path = output_dir / "command.log"
    report_path = output_dir / REPORT_PATH
    config = copy.deepcopy(BASE_CONFIG)
    config["stryker-config"]["mutate"] = [f"{target}{{{start}..{end}}}"]
    write_json(config_path, config)
    argv = [*(command_prefix or ["dotnet", "stryker"]), "--config-file", str(config_path),
            "--output", str(output_dir / "stryker"), "--log-to-file", "--verbosity", "debug"]
    start_time = time.monotonic()
    try:
        child_exit, reason = launch(argv, staging_root / "tests/Parchwell.Verifier.Tests", log_path, timeout)
    finally:
        elapsed = time.monotonic() - start_time
        staging.cleanup()
    report_hash = sha256(report_path) if report_path.is_file() else None
    write_json(output_dir / "local-receipt.json", {
        "purpose": LOCAL_PURPOSE, "target": target, "span": [start, end],
        "commit": commit, "tree": tree, "argv": argv, "environment_overrides": OVERRIDES,
        "timeout_seconds": timeout, "elapsed_seconds": elapsed,
        "child_exit_code": child_exit, "termination_reason": reason,
        "report_sha256": report_hash,
        "command_log_sha256": sha256(log_path) if log_path.is_file() else None,
    })
    print(LOCAL_PURPOSE)
    return 0 if reason is None and child_exit in (0, 2) and report_hash else 1


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
    checker = commands.add_parser("local-check")
    checker.add_argument("--target", required=True)
    checker.add_argument("--span", required=True)
    checker.add_argument("--output-dir", required=True, type=Path)
    checker.add_argument("--timeout", required=True, type=float)
    boundaries = commands.add_parser("member-boundaries")
    boundaries.add_argument("--target", required=True)
    args = parser.parse_args()
    if args.command == "run-shard":
        return run_shard(args.shard, args.output_dir)
    if args.command == "local-check":
        return local_check(args.target, args.span, args.output_dir, timeout=args.timeout)
    if args.command == "member-boundaries":
        return print_member_boundaries(args.target)
    return aggregate(args.input_dir, args.expected_commit, args.run_id,
                     args.run_attempt, args.output_dir)


if __name__ == "__main__":
    raise SystemExit(main())

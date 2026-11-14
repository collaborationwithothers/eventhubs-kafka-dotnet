#!/usr/bin/env python3
"""Retain one C5 shard and combine seventeen complete raw mutation populations."""

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
import shutil
import signal
import stat
import subprocess
import sys
import time
import tempfile

import c5_projection as projection


TARGETS = {
    "phase": ("Judge/PhaseJudge.cs", 3),
    "publication": ("VerifyRun/Publication/PublicationAcceptance.cs", 2),
    "a01": ("VerifyRun/A01/A01EvidenceRecorder.cs", 1),
    "workload": ("VerifyRun/A01/WorkloadRecorder.cs", 1),
    "single-action-repeat": ("VerifyRun/SingleAction/SingleActionRepeatJudgement.cs", 8),
    "single-action-configuration": ("VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs", 19),
    "idle-observation": ("VerifyRun/IdleObservation/IdleObservationJudgement.cs", 5),
    "batching": ("VerifyRun/LedgerBatch/BatchingEvidenceRecorder.cs", 1),
    "b10-restart-manifest": ("VerifyRun/Input/RestartManifestReader.cs", 2),
    "b10-intake": ("VerifyRun/Judgement/B10IntakeChecks.cs", 1),
    "b10-judge": ("Judge/B10JudgeChecks.cs", 1),
    "b10-repeat": ("VerifyRun/Judgement/B10RepeatChecks.cs", 2),
    "b10-phase-registrations": ("VerifyRun/Input/B10PhaseRegistrationReader.cs", 1),
    "b10-late-write": ("VerifyRun/Judgement/B10LateWriteChecks.cs", 1),
    "b10-sweep": ("VerifyRun/B10SweepHandler.cs", 1),
    "b10-sweep-judge": ("Judge/B10SweepChecks.cs", 1),
    "b10-sweep-codec": ("Common/Phase/B10SweepEvidenceCodec.cs", 3),
    "b10-final-sweep-handler": ("VerifyRun/B10Final/B10FinalSweepHandler.cs", 1),
    "b10-final-repeat-evidence": ("VerifyRun/B10Final/B10FinalRepeatEvidence.cs", 1),
    "b10-final-classification": ("VerifyRun/B10Final/B10FinalClassification.cs", 1),
    "b10-final-projection": ("Common/Phase/B10FinalProjection.cs", 1),
    "b10-final-projection-codec": ("Common/Phase/B10FinalProjectionCodec.cs", 1),
    "b10-final-registrations": ("Common/Phase/B10FinalRegistrationReader.cs", 1),
    "b10-final-judge": ("Judge/B10FinalJudge.cs", 1),
}
PRIMARY_CUTS = {
    "phase": (4376, 6570),
    "publication": (4719,),
    "single-action-repeat": (3555, 7222, 10692, 13977, 16262, 19347, 22533),
    "single-action-configuration": (2314, 4053, 6613, 9284, 10952, 13113, 14461, 16935,
                                    19779, 25473, 28510, 32364, 34949, 37376, 39225,
                                    44139, 48960, 53161),
    "idle-observation": (5062, 8804, 12323, 14813),
    "b10-restart-manifest": (6681,),
    "b10-repeat": (8428,),
    "b10-sweep-codec": (5934, 10787),
}
def build_parts(targets: dict[str, tuple[str, int]],
                primary_cuts: dict[str, tuple[int, ...]]) -> dict[str, tuple[str, int, int]]:
    """Reject singleton splits before their crossing entry can overwrite the primary."""
    for key in primary_cuts:
        count = targets[key][1]
        if type(count) is not int or count < 2:
            raise ValueError("declared split must have at least two primary spans")
    return {
        key if count == 1 else (f"{key}-p{index}" if index <= count else f"{key}-crossing"):
            (key, index, count + int(key in primary_cuts))
        for key, (_, count) in targets.items()
        for index in range(1, count + 1 + int(key in primary_cuts))
    }


PARTS = build_parts(TARGETS, PRIMARY_CUTS)
SHARDS = {shard: TARGETS[key][0] for shard, (key, _, _) in PARTS.items()}
SPAN_END = 2147483647
UTF8_BOM = b"\xef\xbb\xbf"
FILTERED = ("Ignored", "Removed by mutate filter")
IDENTITY = ("mutatorName", "replacement", "location")
REBALANCE_SECONDS = 7200
LOCAL_PURPOSE = "Local C5 check: diagnostic only, never acceptance evidence."
STATIC_FIELD = re.compile(r"    (?:[a-z]+ )*static readonly ")
LINE_TERMINATORS = ("\r", "\u2028", "\u2029", "\x85")
TESTED = ("Killed", "Timeout", "Survived", "NoCoverage", "RuntimeError")
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
    config_sha256_after report_sha256 command_log_sha256 projection_manifest_sha256
    static_regions isolated isolation_error""".split())
ISOLATED_FIELDS = set("""mutant_id span exclusions argv started_at_utc finished_at_utc
    elapsed_seconds child_exit_code termination_reason command_completed config_sha256_before
    config_sha256_after report_sha256 command_log_sha256""".split())
LOG_SCAN_LIMIT = 64
LOG_FILE_LIMIT = 8
LOG_BYTE_LIMIT = 16777216
LOG_TOTAL_LIMIT = 67108864
PROC_ROOT = Path("/proc")
PROC_SCAN_LIMIT = 4096
PROC_PID_LIMIT = 64
STATUS_BYTE_LIMIT = 16384
STAT_BYTE_LIMIT = 4096
FD_ENTRY_LIMIT = 512
MAPS_BYTE_LIMIT = 262144
MEMINFO_BYTE_LIMIT = 16384
PROCESS_FIELDS = ("Name", "State", "PPid", "VmSize", "VmRSS", "VmData", "VmSwap", "Threads", "FDSize")
MEMORY_FIELDS = ("MemTotal", "MemFree", "MemAvailable", "SwapTotal", "SwapFree")


def diagnostic_warning(error: Exception) -> None:
    try:
        print(f"C5 diagnostic unavailable: {type(error).__name__}: {error}", file=sys.stderr)
    except Exception:
        pass


def diagnostic(action, *arguments) -> None:
    """Diagnostic collection cannot replace a child's execution outcome."""
    try:
        action(*arguments)
    except Exception as error:
        diagnostic_warning(error)


def bounded_read(path: Path, limit: int) -> tuple[bytes, bool]:
    with path.open("rb") as stream:
        data = stream.read(limit + 1)
    return data[:limit], len(data) <= limit


def resource_fields(path: Path, limit: int, names: tuple[str, ...]) -> dict:
    data, complete = bounded_read(path, limit)
    fields = {}
    errors = []
    for line in data.decode("utf-8", errors="replace").splitlines():
        name, separator, value = line.partition(":")
        if name in names:
            if separator and value.strip():
                text = value.strip()
                numeric = name not in ("Name", "State")
                pattern = r"[0-9]+(?: kB)?" if name.startswith(("Vm", "Mem", "Swap")) else r"[0-9]+"
                if numeric and re.fullmatch(pattern, text) is None:
                    errors.append(f"malformed field: {name}")
                else:
                    fields[name] = text
            else:
                errors.append(f"malformed field: {name}")
    return {"fields": fields, "complete": complete, "errors": errors}


def descriptor_count(path: Path) -> dict:
    count = 0
    with os.scandir(path) as entries:
        for _ in entries:
            count += 1
            if count == FD_ENTRY_LIMIT:
                break
    return {"count": count, "complete": count < FD_ENTRY_LIMIT}


def mapping_count(path: Path) -> dict:
    data, complete = bounded_read(path, MAPS_BYTE_LIMIT)
    complete = complete and len(data) < MAPS_BYTE_LIMIT
    # An unterminated final sampled line is not a complete mapping record.
    return {"count": data.count(b"\n") + int(complete and bool(data) and not data.endswith(b"\n")),
            "complete": complete}


def process_relationship(path: Path) -> dict:
    data, complete = bounded_read(path, STAT_BYTE_LIMIT)
    if not complete:
        return {"start_time": {"complete": False, "status": "truncated"}, "complete": False}
    # comm is parenthesized and may itself contain spaces or parentheses.
    fields = data.decode("utf-8").rsplit(")", 1)[1].split()
    token, parent, session = fields[19], fields[1], fields[3]
    if not all(value.isdecimal() for value in (token, parent, session)):
        raise ValueError("malformed process identity fields")
    return {"start_time": {"token": token, "complete": True},
            "parent_pid": int(parent), "session_id": int(session), "complete": True}


def collect_resources(cwd: Path, directory: Path, phase: str, child_pid: int | None) -> dict:
    result = {"purpose": "diagnostic_only", "phase": phase, "observed_at_utc": utc_now(),
              "launcher_pid": os.getpid(), "child_pid": child_pid, "processes": [], "gaps": []}

    def component(name, action):
        try:
            value = action()
            if value.get("complete") is False:
                result["gaps"].append({"component": name, "reason": "collection limit reached"})
            return value
        except (OSError, ValueError, IndexError) as error:
            result["gaps"].append({"component": name, "reason": f"{type(error).__name__}: {error}"})
            return {"status": "unavailable"}

    pids = set()
    inspected = 0
    scan_complete = True
    try:
        with os.scandir(PROC_ROOT) as entries:
            for entry in entries:
                inspected += 1
                if entry.name.isascii() and entry.name.isdecimal() and entry.is_dir(follow_symlinks=False):
                    pids.add(int(entry.name))
                if inspected == PROC_SCAN_LIMIT:
                    result["gaps"].append({"component": "processes", "reason": "directory scan limit reached"})
                    scan_complete = False
                    break
        result["proc_status"] = "sampled"
    except OSError as error:
        scan_complete = False
        result["proc_status"] = "unavailable"
        result["gaps"].append({"component": "processes", "reason": f"{type(error).__name__}: {error}"})
    result["proc_entries_inspected"] = inspected
    seeds = [result["launcher_pid"], *([] if child_pid is None else [child_pid])]
    # Known roots get direct lookups even when the bounded scan ends before them.
    for pid in seeds:
        try:
            if (PROC_ROOT / str(pid)).is_dir():
                pids.add(pid)
        except OSError as error:
            result["gaps"].append({"component": f"{pid}/selection", "reason": str(error)})
    relationships = {pid: component(f"{pid}/stat", lambda pid=pid: process_relationship(PROC_ROOT / str(pid) / "stat"))
                     for pid in sorted(pids)}
    children = {}
    for pid, relationship in relationships.items():
        children.setdefault(relationship.get("parent_pid"), []).append(pid)
    related = set(seeds)
    related.update(pid for pid, relationship in relationships.items()
                   if child_pid is not None and relationship.get("session_id") == child_pid)
    pending = list(related)
    for parent in pending:
        for pid in children.get(parent, []):
            if pid not in related:
                related.add(pid)
                pending.append(pid)
    def priority(pid):
        return (0 if pid == seeds[0] else 1 if pid == child_pid else 2 if pid in related else 3, pid)
    selected = sorted(sorted(pids, key=priority)[:PROC_PID_LIMIT])
    if len(pids) >= PROC_PID_LIMIT:
        result["gaps"].append({"component": "processes", "reason": "PID limit reached"})
    result["process_selection"] = {
        "rule": "launcher_then_child_then_related_then_other_pids",
        "related_by": "observed_parent_chain_or_child_session",
        "scan_complete": scan_complete, "candidate_count": len(pids),
        "sample_limit": PROC_PID_LIMIT, "selected_pids": selected,
        "selected_related_pids": [pid for pid in selected if pid in related],
        "unobserved_seed_pids": [pid for pid in seeds if pid not in pids],
        "coverage": "Bounded boundary sample; absence does not establish process exit or complete process coverage.",
    }
    for pid in selected:
        root = PROC_ROOT / str(pid)
        relationship = relationships[pid]
        item = {"pid": pid}
        item["status"] = component(f"{pid}/status", lambda: resource_fields(root / "status", STATUS_BYTE_LIMIT, PROCESS_FIELDS))
        item["start_time"] = relationship.get("start_time", {"status": "unavailable"})
        item["file_descriptors"] = component(f"{pid}/fd", lambda: descriptor_count(root / "fd"))
        item["memory_mappings"] = component(f"{pid}/maps", lambda: mapping_count(root / "maps"))
        result["processes"].append(item)
    result["memory"] = component("meminfo", lambda: resource_fields(PROC_ROOT / "meminfo", MEMINFO_BYTE_LIMIT, MEMORY_FIELDS))
    result["disk"] = {}
    for name, path in (("working", cwd), ("output", directory)):
        def disk():
            usage = shutil.disk_usage(path)
            return {"total_bytes": usage.total, "used_bytes": usage.used, "free_bytes": usage.free}
        result["disk"][name] = component(f"disk/{name}", disk)
    return result


def capture_resources(cwd: Path, directory: Path, phase: str, child_pid: int | None) -> None:
    value = collect_resources(cwd, directory, phase, child_pid)
    destination = directory / "diagnostics"
    destination.mkdir(exist_ok=True)
    write_json(destination / f"{phase}.json", value)


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


def primary_spans(key: str, source: bytes) -> list[tuple[int, int]]:
    """Use the declared UTF-16 cuts, retaining the member splitter for undeclared targets."""
    text = target_text(source)
    count = TARGETS[key][1]
    if key not in PRIMARY_CUTS:
        return spans(text, count)
    if type(count) is not int or count < 1:
        raise ValueError("primary count must be a positive integer")
    if count == 1:
        raise ValueError("declared split must have at least two primary spans")
    cuts = PRIMARY_CUTS[key]
    if len(cuts) != count - 1:
        raise ValueError("primary cut count does not match primary count")
    length = utf16_length(text)
    if length >= SPAN_END:
        raise ValueError("target too long for span selection")
    if (any(type(cut) is not int or not 0 < cut < length for cut in cuts)
            or any(left >= right for left, right in zip(cuts, cuts[1:]))):
        raise ValueError("primary cuts must be strictly increasing integer offsets inside target")
    if any(start < cut < end for start, end in static_regions(text) for cut in cuts):
        raise ValueError("static field declaration crosses a span boundary")
    bounds = (0, *cuts, SPAN_END)
    return list(zip(bounds, bounds[1:]))


def shard_span(shard: str, source: bytes | None) -> tuple[int, int] | None:
    key, index, count = PARTS[shard]
    if count == 1:
        return None
    if source is None:
        raise ValueError("span shard requires target source")
    selected = primary_spans(key, source)
    if key in PRIMARY_CUTS and index == count:
        return None
    return selected[index - 1]


def line_starts(text: str) -> list[int]:
    """UTF-16 offset of each line start, numbered as Roslyn numbers lines; other line terminators are rejected."""
    if any(mark in text for mark in LINE_TERMINATORS):
        raise ValueError("unsupported line terminator in target")
    starts = [0]
    for line in text.split("\n")[:-1]:
        starts.append(starts[-1] + utf16_length(line) + 1)
    return starts


def static_regions(text: str) -> list[tuple[int, int]]:
    """Whole-line span of each member-level static readonly field, through its first line that ends with ';'."""
    starts = line_starts(text)
    lines = text.split("\n")
    regions = []
    index = 0
    while index < len(lines):
        if STATIC_FIELD.match(lines[index]) is None:
            index += 1
            continue
        end = index
        while end < len(lines) - 1 and not lines[end].endswith(";"):
            end += 1
        if end == len(lines) - 1:
            raise ValueError("unterminated static field declaration")
        regions.append((starts[index], starts[end + 1]))
        index = end + 1
    return regions


def shard_static_regions(shard: str, source: bytes | None) -> list[tuple[int, int]]:
    """Static field regions inside a shard's span; a region across the span's edge is an error."""
    span = shard_span(shard, source) or (0, SPAN_END)
    if source is None:
        raise ValueError("shard configuration requires target source")
    key, index, count = PARTS[shard]
    if key in PRIMARY_CUTS and index == count:
        return []
    inside = []
    for start, end in static_regions(target_text(source)):
        if span[0] <= start and end <= span[1]:
            inside.append((start, end))
        elif start < span[1] and span[0] < end:
            raise ValueError("static field declaration crosses a span boundary")
    return inside


def shard_config(shard: str, source: bytes | None) -> dict:
    """Select a primary minus isolated fields, or the complement of all primary spans."""
    target = SHARDS[shard]
    span = shard_span(shard, source)
    config = copy.deepcopy(BASE_CONFIG)
    key, index, count = PARTS[shard]
    if key in PRIMARY_CUTS and index == count:
        length = utf16_length(target_text(source))
        config["stryker-config"]["mutate"] = [
            target, *(f"!{target}{{{start}..{min(end, length)}}}"
                      for start, end in primary_spans(key, source))]
        return config
    config["stryker-config"]["mutate"] = [
        target if span is None else f"{target}{{{span[0]}..{span[1]}}}",
        *(f"!{target}{{{start}..{end}}}" for start, end in shard_static_regions(shard, source))]
    return config


def isolated_config(target: str, item: dict) -> dict:
    """One mutant's invocation: its node span, minus the node span of every other mutant inside it."""
    config = copy.deepcopy(BASE_CONFIG)
    start, end = item["span"]
    config["stryker-config"]["mutate"] = [
        f"{target}{{{start}..{end}}}",
        *(f"!{target}{{{low}..{high}}}" for low, high in item["exclusions"])]
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
        diagnostic(capture_resources, cwd, log_path.parent, "before", None)
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
        diagnostic(capture_resources, cwd, log_path.parent, "after", process.pid if process is not None else None)
    return child_exit, reason


FAILURE_TAIL_LINES = 40
FAILURE_KEY_LINES = 20
FAILURE_LINE_CHARS = 300
FAILURE_BYTE_LIMIT = 20480
FAILURE_KEY_PATTERN = re.compile(r"error [A-Z]{2,4}\d+|Initial testrun|failing tests|Stryker\.NET failed|State: failed|^\s*Parchwell\.\S+\.\S+")
ANSI_PATTERN = re.compile(r"\x1b\[[0-9;]*[A-Za-z]")


def annotation_text(text: str) -> str:
    return text.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")


def failure_reasons(receipt: dict, projection_valid: bool) -> list[str]:
    reasons = []
    if receipt["termination_reason"]:
        reasons.append(f"termination reason {receipt['termination_reason']}")
    if receipt["child_exit_code"] not in (0, 2):
        reasons.append(f"Stryker exit code {receipt['child_exit_code']} (expected 0 or 2)")
    if receipt["isolation_error"]:
        reasons.append(f"isolation: {receipt['isolation_error']}")
    if not receipt["tracked_clean_after"] or receipt["source_sha256_before"] != receipt["source_sha256_after"]:
        reasons.append("tracked source changed or was not clean")
    if not projection_valid:
        reasons.append("projection invalid")
    if receipt["config_sha256_before"] != receipt["config_sha256_after"]:
        reasons.append("shard config changed")
    if not receipt["report_sha256"]:
        reasons.append("no mutation report was written")
    return reasons or ["unspecified failure"]


def report_failure(shard: str, receipt: dict, projection_valid: bool, log_path: Path) -> None:
    """Prints a failed shard's reason and a bounded tail of Stryker's output to the job log.

    The log is the same file the shard uploads as an artifact. At most FAILURE_KEY_LINES matching
    lines plus FAILURE_TAIL_LINES final lines are printed, each cut to FAILURE_LINE_CHARS characters,
    and the output as a whole is cut to FAILURE_BYTE_LIMIT bytes."""
    reason = "; ".join(failure_reasons(receipt, projection_valid))
    lines: list[str] = []
    try:
        data, _ = bounded_read(log_path, LOG_BYTE_LIMIT)
        lines = [ANSI_PATTERN.sub("", line)[:FAILURE_LINE_CHARS]
                 for line in data.decode("utf-8", "replace").splitlines()]
    except OSError:
        pass
    tail = lines[-FAILURE_TAIL_LINES:]
    earlier = lines[:len(lines) - len(tail)]
    key = [line for line in earlier if FAILURE_KEY_PATTERN.search(line)][-FAILURE_KEY_LINES:]
    header = f"C5 shard {shard} failed: {reason}"
    body = []
    if key:
        body += [f"--- matching lines before the tail ({len(key)}) ---", *key]
    body += [f"--- last {len(tail)} lines of Stryker output (command.log) ---", *tail]
    encoded = "\n".join(body).encode("utf-8")
    text = encoded.decode("utf-8", "replace")
    if len(encoded) > FAILURE_BYTE_LIMIT:
        text = (f"[output cut to the last {FAILURE_BYTE_LIMIT} bytes]\n"
                + encoded[-FAILURE_BYTE_LIMIT:].decode("utf-8", "ignore"))
    print(header + "\n" + text, flush=True)
    print(f"::error title=C5 shard {annotation_text(shard)} failed::{annotation_text(reason)}", flush=True)


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
        source = (staging_root / (projection.VERIFIER + target)).read_bytes()
        config = shard_config(shard, source)
        regions = shard_static_regions(shard, source)
    except (OSError, ValueError) as error:
        staging.cleanup()
        print(f"C5 span selection failed: {error}")
        return 1
    config_path = output_dir / "shard-config.json"
    log_path = output_dir / "command.log"
    report_path = output_dir / REPORT_PATH
    write_json(config_path, config)
    config_before = sha256(config_path)
    prefix = command_prefix or ["dotnet", "stryker"]
    argv = [*prefix, "--config-file", str(config_path),
            "--output", str(output_dir / "stryker"), "--log-to-file", "--verbosity", "debug"]
    cwd = staging_root / "tests/Parchwell.Verifier.Tests"
    started = utc_now()
    start = time.monotonic()
    child_exit = None
    reason = None
    isolated = []
    isolation_error = None
    try:
        child_exit, reason = launch(argv, cwd, log_path, timeout)
        if regions and not (reason is None and child_exit in (0, 2) and report_path.is_file()):
            isolation_error = "main invocation did not complete"
        elif regions:
            try:
                isolated, isolation_error = run_isolated(target, source, regions, report_path, output_dir,
                                                         prefix, cwd, start + timeout)
            except OSError as error:
                isolation_error = f"isolation failed: {error}"
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
            "static_regions": [list(region) for region in regions],
            "isolated": isolated, "isolation_error": isolation_error,
        }
        write_json(output_dir / "receipt.json", receipt)
    passed = bool(receipt["command_completed"] and child_exit in (0, 2)
                  and clean_after and before == after and before and isolation_error is None
                  and projection_valid and config_before == config_after and receipt["report_sha256"])
    if not passed:
        diagnostic(report_failure, shard, receipt, projection_valid, log_path)
    return 0 if passed else 1


def retain_test_server_logs(directory: Path) -> None:
    source = directory / "stryker/logs/test-servers"
    value = {"purpose": "diagnostic_only", "source_directory": str(source),
             "status": "available", "files": [], "gaps": []}
    retained = 0
    inspected = 0
    try:
        if source.is_symlink():
            raise ValueError("source directory is a symlink")
        with os.scandir(source) as entries:
            for entry in entries:
                inspected += 1
                if entry.name.startswith("rpc-") and entry.name.endswith(".log"):
                    try:
                        if not entry.is_file(follow_symlinks=False):
                            raise ValueError("not a regular non-symlink file")
                        if len(value["files"]) >= LOG_FILE_LIMIT:
                            raise ValueError("file count limit reached")
                        path = Path(entry.path)
                        with os.fdopen(os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK), "rb") as stream:
                            metadata = os.fstat(stream.fileno())
                            if not stat.S_ISREG(metadata.st_mode):
                                raise ValueError("not a regular file")
                            if metadata.st_size > LOG_BYTE_LIMIT:
                                raise ValueError("per-file byte limit exceeded")
                            if metadata.st_size > LOG_TOTAL_LIMIT - retained:
                                raise ValueError("total byte limit exceeded")
                            data = stream.read(min(LOG_BYTE_LIMIT, LOG_TOTAL_LIMIT - retained) + 1)
                            if len(data) > LOG_BYTE_LIMIT or len(data) > LOG_TOTAL_LIMIT - retained:
                                raise ValueError("file grew beyond byte limit")
                        destination = directory / "test-server-logs" / entry.name
                        destination.parent.mkdir(exist_ok=True)
                        with destination.open("xb") as output:
                            output.write(data)
                        retained += len(data)
                        value["files"].append({"source": str(path.relative_to(directory)),
                                               "retained": str(destination.relative_to(directory)),
                                               "source_bytes": len(data), "retained_bytes": len(data)})
                    except (OSError, ValueError) as error:
                        value["gaps"].append({"component": entry.name, "reason": f"{type(error).__name__}: {error}"})
                if inspected == LOG_SCAN_LIMIT:
                    value["gaps"].append({"component": "source_directory", "reason": "directory scan limit reached"})
                    break
    except (OSError, ValueError) as error:
        value["status"] = "unavailable"
        value["gaps"].append({"component": "source_directory", "reason": f"{type(error).__name__}: {error}"})
    value["entries_inspected"] = inspected
    destination = directory / "diagnostics"
    destination.mkdir(exist_ok=True)
    write_json(destination / "test-server-logs.json", value)


def run_isolated(target: str, source: bytes, regions: list[tuple[int, int]], main_report: Path,
                 output_dir: Path, prefix: list[str], cwd: Path, deadline: float) -> tuple[list[dict], str | None]:
    """Run each mutant of the shard's static field regions in its own Stryker invocation, with fresh test processes."""
    try:
        plan = isolation_plan(report_records(json.loads(main_report.read_bytes()), target), source, regions)
    except (OSError, ValueError, TypeError, KeyError) as error:
        return [], f"isolation selection failed: {error}"
    entries = []
    for item in plan:
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            return entries, f"shard time limit reached before mutant {item['mutant_id']}"
        directory = output_dir / "isolated" / item["mutant_id"]
        directory.mkdir(parents=True)
        config_path = directory / "config.json"
        log_path = directory / "command.log"
        report_path = directory / REPORT_PATH
        write_json(config_path, isolated_config(target, item))
        config_before = sha256(config_path)
        argv = [*prefix, "--config-file", str(config_path), "--output", str(directory / "stryker"),
                "--log-to-file", "--verbosity", "debug"]
        started = utc_now()
        start = time.monotonic()
        child_exit, reason = launch(argv, cwd, log_path, remaining)
        diagnostic(retain_test_server_logs, directory)
        logs = directory / "stryker/logs"
        if logs.is_dir():
            try:
                (output_dir / "stryker/logs").mkdir(parents=True, exist_ok=True)
                shutil.move(logs, output_dir / "stryker/logs" / f"isolated-{item['mutant_id']}")
            except OSError:
                pass  # Logs are not evidence: aggregation never reads them. If the move fails they stay in isolated/ID/stryker/logs/, which the evidence upload carries.
        entry = {**item, "argv": argv, "started_at_utc": started, "finished_at_utc": utc_now(),
                 "elapsed_seconds": time.monotonic() - start, "child_exit_code": child_exit,
                 "termination_reason": reason, "command_completed": child_exit is not None and reason is None,
                 "config_sha256_before": config_before,
                 "config_sha256_after": sha256(config_path) if config_path.is_file() else None,
                 "report_sha256": sha256(report_path) if report_path.is_file() else None,
                 "command_log_sha256": sha256(log_path) if log_path.is_file() else None}
        entries.append(entry)
        if not (entry["command_completed"] and child_exit in (0, 2) and entry["report_sha256"]
                and entry["config_sha256_after"] == config_before):
            return entries, f"isolated invocation of mutant {item['mutant_id']} failed"
    return entries, None


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


def node_span(row: dict, starts: list[int], length: int) -> tuple[int, int]:
    """UTF-16 span of a report mutant's original node, from its 1-based line and column."""
    def offset(position: dict) -> int:
        require(position["line"] <= len(starts), "mutant location outside target")
        return starts[position["line"] - 1] + position["column"] - 1

    start, end = offset(row["location"]["start"]), offset(row["location"]["end"])
    require(0 <= start < end <= length, "mutant location outside target")
    return start, end


def isolation_plan(rows: list[dict], source: bytes, regions: list[tuple[int, int]]) -> list[dict]:
    """One invocation per mutant that the main invocation filtered out of a static field region, by ascending id."""
    if not regions:
        return []
    text = target_text(source)
    starts = line_starts(text)
    length = utf16_length(text)
    nodes = {row["id"]: node_span(row, starts, length) for row in rows}
    plan = []
    for row in sorted(rows, key=lambda item: int(item["id"])):
        start, end = nodes[row["id"]]
        if ((row["status"], row.get("statusReason")) != FILTERED
                or not any(low <= start and end <= high for low, high in regions)):
            continue
        others = [other for other in rows if other["id"] != row["id"]]
        for other in others:
            if nodes[other["id"]] == (start, end) and (
                    other["status"] in TESTED or (other["status"], other.get("statusReason")) == FILTERED):
                raise ValueError(f"mutant {row['id']} shares its span with mutant {other['id']}")
        inner = sorted({nodes[other["id"]] for other in others
                        if start <= nodes[other["id"]][0] and nodes[other["id"]][1] <= end
                        and nodes[other["id"]] != (start, end)})
        plan.append({"mutant_id": row["id"], "span": [start, end], "exclusions": [list(node) for node in inner]})
    return plan


def merge_isolated(rows: list[dict], item: dict, isolated_rows: list[dict]) -> list[dict]:
    """Replace one filtered main row with its isolated row, after checking that invocation tested only that mutant."""
    main = {row["id"]: row for row in rows}
    isolated = {row["id"]: row for row in isolated_rows}
    require(set(main) == set(isolated), "isolated inventory differs")
    identifier = item["mutant_id"]
    for key, row in isolated.items():
        require([row.get(field) for field in IDENTITY] == [main[key].get(field) for field in IDENTITY],
                f"mutant {key} identity differs in isolation")
        disposition = (row["status"], row.get("statusReason"))
        if key == identifier:
            require(disposition != FILTERED and row["status"] != "CompileError",
                    f"mutant {key} was not tested in isolation")
        else:
            original = (main[key]["status"], main[key].get("statusReason"))
            require(disposition == FILTERED or (disposition == original and original[0] not in TESTED),
                    f"mutant {key} ran in the isolated invocation of mutant {identifier}")
    return [isolated[row["id"]] if row["id"] == identifier else row for row in rows]


def isolated_rows(directory: Path, target: str, target_hash: str, item: dict, entry: object,
                  rows: list[dict]) -> list[dict]:
    """Check one isolated invocation's retained evidence and merge its one tested mutant into the shard's rows."""
    require(isinstance(entry, dict) and set(entry) == ISOLATED_FIELDS, "malformed isolated receipt")
    require(all(entry[key] == item[key] for key in ("mutant_id", "span", "exclusions")),
            "isolated invocation differs from plan")
    require(entry["command_completed"] is True and entry["termination_reason"] is None
            and type(entry["child_exit_code"]) is int and entry["child_exit_code"] in (0, 2),
            "isolated child execution error")
    folder = directory / "isolated" / item["mutant_id"]
    config_path = folder / "config.json"
    require(json.loads(config_path.read_text()) == isolated_config(target, item),
            "unexpected isolated configuration")
    require(sha256(config_path) == entry["config_sha256_before"] == entry["config_sha256_after"],
            "isolated config hash mismatch")
    report_bytes = (folder / REPORT_PATH).read_bytes()
    require(hashlib.sha256(report_bytes).hexdigest() == entry["report_sha256"], "isolated report hash mismatch")
    require(sha256(folder / "command.log") == entry["command_log_sha256"], "isolated command log hash mismatch")
    return merge_isolated(rows, item, report_records(json.loads(report_bytes), target, source_sha256=target_hash))


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
    if not (isinstance(mutate, list) and mutate and all(isinstance(item, str) for item in mutate)
            and all(re.fullmatch(re.escape("!" + target) + r"\{\d+\.\.\d+\}", item) for item in mutate[1:])):
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
            rows = report_records(report, target, source_sha256=target_hash)
            regions = shard_static_regions(shard, source)
            require(receipt["static_regions"] == [list(region) for region in regions], "static region mismatch")
            require(receipt["isolation_error"] is None, "static isolation incomplete")
            for row in rows:
                require(not (row.get("static") is True and row["status"] in TESTED),
                        f"static mutant {row['id']} tested outside isolation")
            plan = isolation_plan(rows, source, regions)
            require(isinstance(receipt["isolated"], list) and len(receipt["isolated"]) == len(plan),
                    "isolated invocation list mismatch")
            for item, entry in zip(plan, receipt["isolated"]):
                rows = isolated_rows(directory, target, target_hash, item, entry, rows)
            part_records[shard] = rows
            shards[shard] = {"target": target, "part": index, "parts": count,
                             "span": None if span is None else list(span),
                             "elapsed_seconds": elapsed,
                             "child_exit_code": receipt["child_exit_code"],
                             "report_sha256": report_hash,
                             "isolated_mutants": [item["mutant_id"] for item in plan]}
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
        targets[key] = {"target": target, "parts": len(members), "status_counts": target_counts,
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

#!/usr/bin/env python3
"""Bounded, single-start PR 6a command controller (Python standard library only).

Usage: controller.py --mode full|since|suite --repo-root ROOT --cwd CWD
    --run-dir DIR --tested-commit SHA [--comparison-commit SHA]
    [--config FILE] [--report FILE] [--xml FILE ...] [--env NAME=VALUE ...]
    [--seconds N] [--max-mutants N] -- command argument ...

The invocation ledger is local implementation support, never a scenario result.
Report presence is recorded here; the authorized single extraction pass establishes
serialized counts and completeness. A missing/capped report is never a pass.
"""

import argparse
import fcntl
import gzip
import hashlib
import json
import os
from pathlib import Path
import re
import selectors
import signal
import subprocess
import sys
import time
from datetime import datetime, timezone


CAPS = {"full": (1, 3600), "since": (6, 1200), "suite": (3, 1800)}
ANSI = re.compile(r"\x1b\[[0-?]*[ -/]*[@-~]")
# Exact form observed in the retained pinned Stryker 5.0.0 PR 5 command log.
GENERATED = re.compile(r"\b([0-9][0-9,]*)\s+mutants?\s+created\b", re.I)


def utc():
    return datetime.now(timezone.utc).isoformat()


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def write_json(path, value):
    temporary = Path(str(path) + ".tmp")
    temporary.write_text(json.dumps(value, indent=2) + "\n")
    temporary.replace(path)


def git(root, *args):
    return subprocess.check_output(["git", "-C", str(root), *args])


def source_identity(root, config, tested, comparison):
    head = git(root, "rev-parse", "HEAD").decode().strip()
    if git(root, "rev-parse", tested + "^{commit}").decode().strip() != head:
        raise ValueError("--tested-commit must resolve to the actual checkout HEAD")
    comparison_sha = None
    if comparison:
        comparison_sha = git(root, "rev-parse", comparison + "^{commit}").decode().strip()
    paths = git(root, "ls-files", "--cached", "--others", "--exclude-standard", "-z").split(b"\0")
    # Identity includes all current source/project/config input bytes, including
    # untracked implementation files. No secret/environment values are read.
    inputs = {}
    for raw in paths:
        if not raw:
            continue
        relative = os.fsdecode(raw)
        path = root / relative
        if (relative.startswith(("src/", "tests/", "scripts/", ".config/"))
                or "/" not in relative) and path.is_file():
            inputs[relative] = sha256(path)
    diff = git(root, "diff", "--binary", "HEAD", "--", ".", ":(exclude)docs/operations/evidence/issue-80-pr6a")
    return {
        "tested_commit": head,
        "tested_commit_tree": git(root, "rev-parse", "HEAD^{tree}").decode().strip(),
        "comparison_commit": comparison_sha,
        "dirty_diff_sha256": hashlib.sha256(diff).hexdigest(),
        "dirty_diff_bytes": len(diff),
        "dirty_diff_scope": "tracked HEAD diff excluding this PR's retained evidence directory",
        "source_input_sha256": inputs,
        "source_input_manifest_sha256": hashlib.sha256(json.dumps(inputs, sort_keys=True).encode()).hexdigest(),
        "config": {"path": str(config), "sha256": sha256(config)} if config else None,
        "controller": {"path": str(Path(__file__).resolve()), "sha256": sha256(__file__)},
    }


def reserve(root, mode, receipt, comparison):
    path = root / ".artifacts/issue-80-pr6a/controller-invocations.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a+") as ledger:
        fcntl.flock(ledger, fcntl.LOCK_EX)
        ledger.seek(0)
        contents = ledger.read()
        entries = json.loads(contents) if contents else []
        ordinal = 1 + sum(entry["mode"] == mode for entry in entries)
        if ordinal > CAPS[mode][0]:
            raise ValueError(f"authorized {mode} invocation allowance already consumed")
        if mode == "since" and not any(entry["mode"] == "full" for entry in entries):
            raise ValueError("a since invocation requires a preceding full invocation")
        if mode == "since":
            preceding = next(entry for entry in reversed(entries) if entry["mode"] in ("full", "since"))
            previous_receipt = json.loads(Path(preceding["receipt"]).read_text())
            if previous_receipt.get("tested_commit") != comparison:
                raise ValueError("since comparison must be the preceding mutation invocation's tested commit")
            if previous_receipt.get("exit_reason") in ("starting", "running"):
                raise ValueError("the preceding mutation invocation is still running")
        entries.append({"mode": mode, "ordinal": ordinal, "reserved_utc": utc(), "receipt": str(receipt)})
        ledger.seek(0)
        ledger.truncate()
        json.dump(entries, ledger, indent=2)
        ledger.write("\n")
        ledger.flush()
        os.fsync(ledger.fileno())
        return ordinal, str(path)


def terminate_group(process, receipt, reason):
    receipt["exit_reason"] = reason
    receipt["termination"] = {"process_group": process.pid, "signal": "SIGKILL", "utc": utc()}
    try:
        os.killpg(process.pid, signal.SIGKILL)
    except ProcessLookupError:
        receipt["termination"]["group_already_exited"] = True


def observe(line, process, receipt, ceiling):
    match = GENERATED.search(ANSI.sub("", line))
    if match:
        count = int(match.group(1).replace(",", ""))
        receipt["observed_generated_counts"].append(count)
        receipt["maximum_observed_generated_count"] = max(receipt["observed_generated_counts"])
        if count > ceiling and receipt["termination"] is None:
            terminate_group(process, receipt, "generated-mutant-cap")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--mode", required=True, choices=CAPS)
    for option in ("repo-root", "cwd", "run-dir", "tested-commit"):
        parser.add_argument("--" + option, required=True)
    parser.add_argument("--comparison-commit")
    parser.add_argument("--config")
    parser.add_argument("--report")
    parser.add_argument("--xml", action="append", default=[])
    parser.add_argument("--env", action="append", default=[])
    parser.add_argument("--seconds", type=float)
    parser.add_argument("--max-mutants", type=int, default=10000)
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ["--"] else args.command
    if not command:
        parser.error("a child argv after -- is required")
    seconds = args.seconds if args.seconds is not None else CAPS[args.mode][1]
    if not 0 < seconds <= CAPS[args.mode][1] or not 0 < args.max_mutants <= 10000:
        parser.error("limits must be positive and no greater than the Q1 authorization")
    if bool(args.comparison_commit) != (args.mode == "since"):
        parser.error("--comparison-commit is required exactly for since mode")
    if args.mode != "suite" and not (args.report and args.config):
        parser.error("mutation mode requires --report and --config")
    root, cwd, run_dir = (Path(value).resolve() for value in (args.repo_root, args.cwd, args.run_dir))
    config = Path(args.config).resolve() if args.config else None
    if not cwd.is_dir():
        parser.error("--cwd must exist")
    if args.report and Path(args.report).resolve().exists():
        parser.error("--report must be a fresh path; existing report evidence cannot be reused")
    overrides = {}
    for item in args.env:
        name, separator, value = item.partition("=")
        if not separator or not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", name):
            parser.error("--env must be NAME=VALUE (recorded verbatim; never supply secrets)")
        overrides[name] = value
    identity = source_identity(root, config, args.tested_commit, args.comparison_commit)
    if args.mode == "since" and not any(
        arg == "--since:" + args.comparison_commit or arg == "--since:" + identity["comparison_commit"]
        for arg in command
    ):
        parser.error("since child argv must name the recorded comparison commit")
    run_dir.mkdir(parents=True, exist_ok=True)
    receipt_path = run_dir / "controller.json"
    # A receipt is never overwritten: no silent restart or reuse of run evidence.
    with receipt_path.open("x") as initial:
        initial.write("{}\n")
    ordinal, ledger = reserve(root, args.mode, receipt_path, identity["comparison_commit"])
    write_json(run_dir / "tested-source.json", identity)
    receipt = {
        "mode": args.mode, "invocation_ordinal": ordinal, "invocation_ledger": ledger,
        "command": command, "cwd": str(cwd), "environment_overrides": overrides,
        "tested_commit": identity["tested_commit"], "comparison_commit": identity["comparison_commit"],
        "source_identity": "tested-source.json", "source_manifest_sha256": sha256(run_dir / "tested-source.json"),
        "wall_time_cap_seconds": seconds, "generated_mutant_cap": args.max_mutants,
        "observed_generated_counts": [], "maximum_observed_generated_count": None,
        "started_utc": utc(), "elapsed_seconds": 0, "exit_code": None,
        "exit_reason": "starting", "termination": None, "restart_count": 0,
        "command_log": "command.log.gz", "report_completeness": "not-assessed",
        "expected_report": str(Path(args.report).resolve()) if args.report else None,
        "expected_xml": [str(Path(path).resolve()) for path in args.xml],
    }
    write_json(receipt_path, receipt)
    started = time.monotonic()
    process = None
    pending = ""
    interrupted = []
    previous_handlers = {}

    def handle_signal(signum, frame):
        interrupted.append(signum)
        if process is not None and receipt["termination"] is None:
            terminate_group(process, receipt, "controller-signal")

    for sig in (signal.SIGINT, signal.SIGTERM):
        previous_handlers[sig] = signal.signal(sig, handle_signal)
    try:
        environment = os.environ.copy()
        environment.update(overrides)
        with gzip.open(run_dir / "command.log.gz", "xb") as log:
            process = subprocess.Popen(command, cwd=cwd, env=environment, stdout=subprocess.PIPE,
                                       stderr=subprocess.STDOUT, start_new_session=True)
            receipt["process_group"] = process.pid
            receipt["exit_reason"] = "running"
            write_json(receipt_path, receipt)
            os.set_blocking(process.stdout.fileno(), False)
            with selectors.DefaultSelector() as selector:
                selector.register(process.stdout, selectors.EVENT_READ)
                while selector.get_map():
                    remaining = seconds - (time.monotonic() - started)
                    if receipt["termination"] is None and (remaining <= 0 or interrupted):
                        terminate_group(process, receipt, "elapsed-cap" if not interrupted else "controller-signal")
                    # The controller consumes output to enforce the bound. The session
                    # waits for this command; it does not issue progress queries.
                    events = selector.select(max(0.01, remaining) if receipt["termination"] is None else 1)
                    for key, _ in events:
                        data = os.read(key.fd, 65536)
                        if not data:
                            observe(pending, process, receipt, args.max_mutants)
                            pending = ""
                            selector.unregister(key.fileobj)
                            continue
                        log.write(data)
                        pending += data.decode("utf-8", errors="replace")
                        lines = pending.split("\n")
                        pending = lines.pop()
                        for line in lines:
                            observe(line, process, receipt, args.max_mutants)
                        # Retain all bytes in the log while bounding the parser buffer.
                        pending = pending[-65536:]
            try:
                receipt["exit_code"] = process.wait(timeout=max(0.01, seconds - (time.monotonic() - started)))
            except subprocess.TimeoutExpired:
                terminate_group(process, receipt, "elapsed-cap")
                receipt["exit_code"] = process.wait()
            if receipt["termination"] is None:
                receipt["exit_reason"] = "command-exit"
    except BaseException as error:
        receipt["controller_error"] = f"{type(error).__name__}: {error}"
        if process is not None:
            if receipt["termination"] is None:
                terminate_group(process, receipt, "controller-error")
            receipt["exit_code"] = process.wait()
        else:
            receipt["exit_reason"] = "launch-error"
    finally:
        receipt["elapsed_seconds"] = time.monotonic() - started
        receipt["finished_utc"] = utc()
        for sig, handler in previous_handlers.items():
            signal.signal(sig, handler)
        try:
            final_identity = source_identity(root, config, args.tested_commit, args.comparison_commit)
            receipt["source_inputs_unchanged"] = (
                final_identity["source_input_manifest_sha256"] == identity["source_input_manifest_sha256"]
                and final_identity["config"] == identity["config"]
                and final_identity["controller"] == identity["controller"])
            if not receipt["source_inputs_unchanged"]:
                write_json(run_dir / "finished-source.json", final_identity)
        except Exception as error:
            receipt["source_inputs_unchanged"] = False
            receipt["finished_source_error"] = f"{type(error).__name__}: {error}"
        report = Path(args.report).resolve() if args.report else None
        receipt["report_present"] = report.is_file() if report else None
        if report and report.is_file():
            try:
                receipt["report_bytes"] = report.stat().st_size
                receipt["report_sha256"] = sha256(report)
                # Copy bytes without enumerating mutants; only the summary pass does so.
                with report.open("rb") as source, gzip.open(run_dir / "mutation-report.json.gz", "xb") as target:
                    for block in iter(lambda: source.read(1024 * 1024), b""):
                        target.write(block)
            except Exception as error:
                receipt["report_retention_error"] = f"{type(error).__name__}: {error}"
        if args.mode != "suite":
            receipt["report_completeness"] = (
                "pending-one-pass-extraction" if receipt["report_present"] and receipt["exit_code"] == 0
                and receipt["termination"] is None and receipt["source_inputs_unchanged"]
                and "report_retention_error" not in receipt else "incomplete-or-missing")
        else:
            receipt["report_completeness"] = "not-applicable-suite-mode"
        receipt["xml_presence"] = {path: Path(path).is_file() for path in receipt["expected_xml"]}
        receipt["command_log_sha256"] = sha256(run_dir / "command.log.gz") if (run_dir / "command.log.gz").exists() else None
        receipt["execution_evidence_complete"] = (
            receipt["exit_code"] == 0 and receipt["termination"] is None
            and receipt["source_inputs_unchanged"] and receipt["command_log_sha256"] is not None
            and all(receipt["xml_presence"].values())
            and (args.mode == "suite" or (
                receipt["report_completeness"] == "pending-one-pass-extraction"
                and receipt["maximum_observed_generated_count"] is not None
                and 0 < receipt["maximum_observed_generated_count"] <= args.max_mutants)))
        write_json(receipt_path, receipt)
    print(json.dumps({"receipt": str(receipt_path), "exit_reason": receipt["exit_reason"],
                      "exit_code": receipt["exit_code"], "elapsed_seconds": receipt["elapsed_seconds"]}))
    return 124 if receipt["termination"] else (0 if receipt["execution_evidence_complete"] else 1)


if __name__ == "__main__":
    sys.exit(main())

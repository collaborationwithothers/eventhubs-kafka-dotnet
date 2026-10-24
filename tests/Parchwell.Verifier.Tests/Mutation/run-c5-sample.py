#!/usr/bin/env python3
"""Run one bounded C5 Stryker sample and print a compact execution receipt."""

from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import re
import selectors
import subprocess
import sys
import tempfile
import time


ROOT = Path(__file__).resolve().parents[1]
CONFIG = ROOT / "Mutation" / "stryker-c5-config.json"
MAX_MUTANTS = 2000
MAX_SECONDS = 1800
MAX_OUTPUT = 16 * 1024 * 1024
GENERATED = re.compile(rb"\b([0-9][0-9,]*)\s+mutants?\s+created\b", re.IGNORECASE)
ANSI = re.compile(rb"\x1b\[[0-?]*[ -/]*[@-~]")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def source_hashes() -> dict[str, str]:
    files = [
        ROOT / "../../src/Parchwell.Verifier/Judge/PhaseJudge.cs",
        ROOT / "../../src/Parchwell.Verifier/VerifyRun/Publication/PublicationAcceptance.cs",
        ROOT / "../../src/Parchwell.Verifier/VerifyRun/A01/A01EvidenceRecorder.cs",
        ROOT / "../../src/Parchwell.Verifier/VerifyRun/A01/WorkloadRecorder.cs",
        ROOT / "Parchwell.Verifier.Tests.csproj",
        ROOT / "stryker-config.json",
        CONFIG,
        Path(__file__).resolve(),
    ]
    repo = ROOT.parent.parent.resolve()
    return {str(path.resolve().relative_to(repo)): sha256(path) for path in files}


def main() -> int:
    before = source_hashes()
    env = os.environ.copy()
    env["ParchwellVerifierMutationOnly"] = "true"
    command = ["dotnet", "stryker", "--config-file", "Mutation/stryker-c5-config.json"]
    started = time.monotonic()
    generated: set[int] = set()
    captured = bytearray()
    failure = None
    exit_code = None
    report_hashes: dict[str, str] = {}
    with tempfile.TemporaryDirectory(prefix="c5-stryker-") as temporary:
        output_dir = Path(temporary) / "stryker"
        command.extend(["--output", str(output_dir)])
        process = subprocess.Popen(command, cwd=ROOT, env=env, stdout=subprocess.PIPE,
                                   stderr=subprocess.STDOUT, bufsize=0)
        assert process.stdout is not None
        selector = selectors.DefaultSelector()
        selector.register(process.stdout, selectors.EVENT_READ)
        trailing = bytearray()
        while process.poll() is None or selector.get_map():
            elapsed = time.monotonic() - started
            if elapsed >= MAX_SECONDS:
                failure = "time_cap_exceeded"
                process.terminate()
                break
            for key, _ in selector.select(timeout=0.25):
                chunk = os.read(key.fd, 65536)
                if not chunk:
                    selector.unregister(key.fileobj)
                    continue
                captured.extend(chunk)
                if len(captured) > MAX_OUTPUT:
                    failure = "output_cap_exceeded"
                    process.terminate()
                    break
                trailing.extend(chunk)
                for match in GENERATED.finditer(ANSI.sub(b"", trailing)):
                    generated.add(int(match.group(1).replace(b",", b"")))
                if generated and max(generated) > MAX_MUTANTS:
                    failure = "generated_mutant_cap_exceeded"
                    process.terminate()
                    break
                if len(trailing) > 8192:
                    del trailing[:-8192]
            if failure:
                break
        try:
            exit_code = process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            exit_code = process.wait()
            failure = failure or "process_killed_after_termination_timeout"
        elapsed = time.monotonic() - started
        for report in output_dir.rglob("mutation-report.json"):
            report_hashes[str(report.relative_to(output_dir))] = sha256(report)
        after = source_hashes()
        result = {
            "command": command,
            "cwd": str(ROOT),
            "environment_overrides": {"ParchwellVerifierMutationOnly": "true"},
            "source_sha256": before,
            "source_unchanged": before == after,
            "generated_progress_observations": sorted(generated),
            "generated_count_observed": max(generated) if generated else None,
            "generated_count_observed_within_cap": bool(generated) and max(generated) <= MAX_MUTANTS,
            "elapsed_seconds": round(elapsed, 3),
            "elapsed_within_cap": elapsed < MAX_SECONDS,
            "captured_output_bytes": len(captured),
            "captured_output_within_cap": len(captured) <= MAX_OUTPUT,
            "exit_code": exit_code,
            "failure": failure,
            "report_sha256": report_hashes,
            "measurement_complete": failure is None and exit_code == 0 and bool(generated) and bool(report_hashes),
        }
        print(json.dumps(result, sort_keys=True))
        if failure is not None or exit_code != 0 or not generated:
            return 1
        return 0


if __name__ == "__main__":
    sys.exit(main())

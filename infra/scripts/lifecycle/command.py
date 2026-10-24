#!/usr/bin/env python3
"""Bound one external command; ordinary output stays visible to the caller."""

import argparse
import contextlib
import math
import os
from pathlib import Path
import selectors
import shlex
import signal
import subprocess
import sys
import time


class Interrupted(BaseException):
    def __init__(self, number):
        self.number = number


def stop_group(process, number=signal.SIGTERM):
    previous = {sig: signal.signal(sig, signal.SIG_IGN)
                for sig in (signal.SIGINT, signal.SIGTERM)}
    try:
        with contextlib.suppress(ProcessLookupError):
            os.killpg(process.pid, number)
        end = time.monotonic() + 5
        while time.monotonic() < end:
            process.poll()
            try:
                os.killpg(process.pid, 0)
            except ProcessLookupError:
                break
            time.sleep(min(0.05, max(0, end - time.monotonic())))
        with contextlib.suppress(ProcessLookupError):
            os.killpg(process.pid, signal.SIGKILL)
        process.wait()
    finally:
        for sig, handler in previous.items():
            signal.signal(sig, handler)


def run_command(argv, seconds, stdout_file=None, max_bytes=None):
    if not argv or not math.isfinite(seconds) or seconds <= 0:
        raise ValueError("invalid_command_deadline")
    if (stdout_file is None) != (max_bytes is None) or (max_bytes is not None and max_bytes < 0):
        raise ValueError("invalid_output_limit")
    print("+ " + shlex.join(argv), file=sys.stderr, flush=True)
    output = None
    marker = None
    process = None
    selector = selectors.DefaultSelector()
    previous = {}
    result = 1
    reason = "command_failed"

    def interrupted(number, _frame):
        raise Interrupted(number)

    try:
        if stdout_file is not None:
            target = Path(stdout_file)
            marker = Path(str(target) + ".incomplete")
            marker.unlink(missing_ok=True)
            descriptor = os.open(target, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
            os.fchmod(descriptor, 0o600)
            output = os.fdopen(descriptor, "wb")
        previous = {sig: signal.signal(sig, interrupted)
                    for sig in (signal.SIGINT, signal.SIGTERM)}
        end = time.monotonic() + seconds
        process = subprocess.Popen(argv, start_new_session=True,
                                   stdout=subprocess.PIPE if output else None)
        if output:
            selector.register(process.stdout, selectors.EVENT_READ)
        written = 0
        while True:
            remaining = end - time.monotonic()
            if remaining <= 0:
                result, reason = 124, "deadline_exceeded"
                stop_group(process)
                break
            if output and selector.get_map():
                for key, _events in selector.select(min(remaining, 0.1)):
                    chunk = os.read(key.fileobj.fileno(), 65536)
                    if not chunk:
                        selector.unregister(key.fileobj)
                        key.fileobj.close()
                        continue
                    available = max_bytes - written
                    output.write(chunk[:available])
                    written += min(len(chunk), available)
                    if len(chunk) > available:
                        result, reason = 125, "output_limit_exceeded"
                        stop_group(process)
                        break
                if result == 125:
                    break
            else:
                try:
                    process.wait(timeout=min(remaining, 0.1))
                except subprocess.TimeoutExpired:
                    pass
            if process.poll() is not None and not selector.get_map():
                result = process.returncode if process.returncode >= 0 else 128 - process.returncode
                reason = "child_exit_" + str(result)
                break
        return result
    except Interrupted as interruption:
        result, reason = 128 + interruption.number, "interrupted"
        if process:
            stop_group(process, interruption.number)
        return result
    except OSError as error:
        result, reason = 127, "command_os_error"
        print(reason + ": " + str(error), file=sys.stderr)
        if process:
            stop_group(process)
        return result
    finally:
        selector.close()
        if output:
            output.close()
        if marker is not None and result:
            descriptor = os.open(marker, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
            with os.fdopen(descriptor, "w") as incomplete:
                incomplete.write(reason + "\n")
        for sig, handler in previous.items():
            signal.signal(sig, handler)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("seconds", type=float)
    parser.add_argument("--stdout-file")
    parser.add_argument("--max-bytes", type=int)
    # Split explicitly so arguments after -- belong only to the child.
    try:
        boundary = sys.argv.index("--")
    except ValueError:
        parser.error("expected -- before command")
    arguments = parser.parse_args(sys.argv[1:boundary])
    try:
        return run_command(sys.argv[boundary + 1:], arguments.seconds,
                           arguments.stdout_file, arguments.max_bytes)
    except ValueError as error:
        parser.error(str(error))


if __name__ == "__main__":
    sys.exit(main())

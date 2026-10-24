"""Synthetic subprocess fixtures; no infrastructure executables are invoked."""

import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import unittest


COMMAND = Path(__file__).resolve().parents[1] / "lifecycle" / "command.py"


class CommandTests(unittest.TestCase):
    def invoke(self, code, *options, seconds="5"):
        return subprocess.run([sys.executable, str(COMMAND), seconds, *options,
                               "--", sys.executable, "-c", code],
                              capture_output=True, timeout=12)

    def test_exit_status_and_visible_output(self):
        result = self.invoke("import sys; print('ordinary'); print('diagnostic',file=sys.stderr); sys.exit(7)")
        self.assertEqual(7, result.returncode)
        self.assertEqual(b"ordinary\n", result.stdout)
        self.assertIn(b"diagnostic", result.stderr)
        self.assertIn(b"+ ", result.stderr)

    def test_deadline_kills_descendant_group(self):
        with tempfile.TemporaryDirectory() as temporary:
            pidfile = Path(temporary) / "pid"
            code = ("import os,signal,time,pathlib; pid=os.fork(); "
                    "signal.signal(signal.SIGTERM,signal.SIG_IGN); "
                    f"pathlib.Path({str(pidfile)!r}).write_text(str(os.getpid())) if pid==0 else None; "
                    "time.sleep(30)")
            result = self.invoke(code, seconds="0.2")
            self.assertEqual(124, result.returncode)
            pid = int(pidfile.read_text())
            state = Path(f"/proc/{pid}/stat")
            if state.exists():
                self.assertEqual("Z", state.read_text().split()[2])
            else:
                with self.assertRaises(ProcessLookupError):
                    os.kill(pid, 0)

    def test_exact_byte_limit_is_complete_and_private(self):
        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary) / "out"
            target.write_text("old")
            target.chmod(0o644)
            result = self.invoke("import sys; sys.stdout.buffer.write(b'abcd')",
                                 "--stdout-file", str(target), "--max-bytes", "4")
            self.assertEqual(0, result.returncode)
            self.assertEqual(b"abcd", target.read_bytes())
            self.assertEqual(0o600, target.stat().st_mode & 0o777)
            self.assertFalse(Path(str(target) + ".incomplete").exists())
            self.assertEqual(b"", result.stdout)

    def test_overflow_stops_before_extra_byte_and_marks_incomplete(self):
        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary) / "out"
            result = self.invoke("import sys; sys.stdout.buffer.write(b'abcde')",
                                 "--stdout-file", str(target), "--max-bytes", "4")
            self.assertEqual(125, result.returncode)
            self.assertEqual(b"abcd", target.read_bytes())
            self.assertEqual("output_limit_exceeded\n", Path(str(target) + ".incomplete").read_text())

    def test_signal_forwarded_and_child_reaped(self):
        with tempfile.TemporaryDirectory() as temporary:
            pidfile = Path(temporary) / "pid"
            code = f"import os,pathlib,time; pathlib.Path({str(pidfile)!r}).write_text(str(os.getpid())); time.sleep(30)"
            with subprocess.Popen([sys.executable, str(COMMAND), "10", "--",
                                   sys.executable, "-c", code],
                                  stdout=subprocess.PIPE, stderr=subprocess.PIPE) as process:
                deadline = time.monotonic() + 3
                while not pidfile.exists() and time.monotonic() < deadline:
                    time.sleep(0.01)
                self.assertTrue(pidfile.exists())
                process.send_signal(signal.SIGTERM)
                process.communicate(timeout=7)
                self.assertEqual(143, process.returncode)
                with self.assertRaises(ProcessLookupError):
                    os.kill(int(pidfile.read_text()), 0)


if __name__ == "__main__":
    unittest.main()

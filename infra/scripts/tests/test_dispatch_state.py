"""Seven synthetic history cases; HTTP and clocks never reach real services."""

import contextlib
import datetime as dt
import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest import mock
import urllib.error


SCRIPT = Path(__file__).resolve().parents[1] / "lifecycle" / "dispatch-state.py"
SPEC = importlib.util.spec_from_file_location("dispatch_state", SCRIPT)
dispatch = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(dispatch)
NOW = dt.datetime(2026, 10, 24, 12, tzinfo=dt.timezone.utc)


class FrozenDateTime(dt.datetime):
    @classmethod
    def now(cls, tz=None):
        return NOW


def stamp(seconds_ago):
    return (NOW - dt.timedelta(seconds=seconds_ago)).isoformat()


def run(run_id, *, created=10000, updated=3600, attempt=1, status="completed"):
    return {"id": run_id, "run_attempt": attempt, "status": status,
            "created_at": stamp(created), "updated_at": stamp(updated)}


def page(key, items, total=None, more=False):
    return ({key: items, "total_count": len(items) if total is None else total}, more)


def runs_path(workflow, number=1):
    return f"/actions/workflows/{workflow}/runs?per_page=100&page={number}"


def jobs_path(run_id, attempt=1, number=1):
    return f"/actions/runs/{run_id}/attempts/{attempt}/jobs?per_page=100&page={number}"


class Response(io.BytesIO):
    def __init__(self, document, more):
        super().__init__(document if isinstance(document, bytes) else json.dumps(document).encode())
        self.headers = {"Link": '<https://api.github.com/ignored>; rel="next"'} if more else {}
        self.read_sizes = []

    def read(self, size=-1):
        self.read_sizes.append(size)
        return super().read(size)


class FakeHTTP:
    def __init__(self, replies):
        self.replies = replies
        self.calls = []
        self.responses = []

    def open(self, request, timeout):
        prefix = "https://api.github.com/repos/synthetic/repository"
        if not request.full_url.startswith(prefix):
            raise AssertionError("Unexpected API host/repository")
        path = request.full_url[len(prefix):]
        self.calls.append((path, timeout))
        if path not in self.replies:
            raise AssertionError("Unexpected HTTP request: " + path)
        reply = self.replies[path]
        if isinstance(reply, int):
            raise urllib.error.HTTPError(request.full_url, reply, "synthetic", {}, None)
        response = Response(*reply)
        self.responses.append(response)
        return response


class DispatchStateTests(unittest.TestCase):
    def evaluate(self, replies, expected):
        http = FakeHTTP(replies)
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "github-output"
            capture = io.StringIO()
            with (mock.patch.object(dispatch, "OPENER", http),
                  mock.patch.object(dispatch.dt, "datetime", FrozenDateTime),
                  mock.patch.object(dispatch.time, "monotonic", return_value=100),
                  mock.patch.dict(os.environ, {"GITHUB_REPOSITORY": "synthetic/repository",
                                               "GITHUB_TOKEN": "synthetic-secret-token",
                                               "GITHUB_OUTPUT": str(output)}, clear=True),
                  contextlib.redirect_stdout(capture)):
                self.assertEqual(700, dispatch.Actions("synthetic/repository", "token").deadline)
                self.assertEqual(0, dispatch.main())
            self.assertEqual("idle=" + str(expected[0]).lower() + "\nstop_eligible="
                             + str(expected[1]).lower() + "\n", output.read_text())
        self.assertNotIn("synthetic-secret-token", capture.getvalue())
        self.assertTrue(all(timeout == 60 for _, timeout in http.calls))
        self.assertTrue(all(0 < size <= 65536 for response in http.responses
                            for size in response.read_sizes))
        return http, capture.getvalue()

    def test_latest_completion_3599_seconds_defers_stop(self):
        http, logs = self.evaluate({
            runs_path("scenario.yml"): page("workflow_runs", [run(1, updated=3599)]),
            runs_path("cluster-smoke.yml"): page("workflow_runs", [run(2, status="queued")]),
            jobs_path(1): page("jobs", [{"id": 11, "completed_at": stamp(3599)}]),
        }, (True, False))
        self.assertIn('"age_seconds": 3599.0', logs)
        self.assertEqual(3, len(http.calls))

    def test_old_created_rerun_ends_last_at_3600_seconds_with_job_pagination(self):
        http, logs = self.evaluate({
            runs_path("scenario.yml"): page("workflow_runs", [run(1, created=9000, updated=7200)]),
            runs_path("cluster-smoke.yml"): page("workflow_runs", [
                run(2, created=1000000, updated=3500, attempt=3),
                run(3, created=2000000, updated=2000000)]),
            jobs_path(1): page("jobs", [{"id": 11, "completed_at": stamp(7200)}]),
            jobs_path(2, 3): page("jobs", [{"id": 21, "completed_at": None}], total=2, more=True),
            jobs_path(2, 3, 2): page("jobs", [{"id": 22, "completed_at": stamp(3600)}], total=2),
        }, (True, True))
        self.assertIn('"age_seconds": 3600.0', logs)
        self.assertIn('"run_attempt": 3', logs)
        self.assertEqual(5, len(http.calls))
        self.assertFalse(any("/runs/3/" in path for path, _ in http.calls))

    def test_completed_run_without_completed_jobs_and_absent_workflow_is_idle_only(self):
        _, logs = self.evaluate({
            runs_path("scenario.yml"): page("workflow_runs", [run(1)]),
            runs_path("cluster-smoke.yml"): 404,
            jobs_path(1): page("jobs", [{"id": 11, "completed_at": None}]),
        }, (True, False))
        self.assertIn('"event": "workflow_absent"', logs)
        self.assertIn('"reason": "no_completed_jobs"', logs)

    def test_unknown_run_status_defers_all_cleanup(self):
        http, logs = self.evaluate({
            runs_path("scenario.yml"): page("workflow_runs", [run(1, status="new_api_status")]),
            runs_path("cluster-smoke.yml"): page("workflow_runs", [run(2, status="in_progress")]),
        }, (False, False))
        self.assertEqual(2, len(http.calls))
        self.assertIn('"event": "dispatch_active_or_unknown"', logs)

    def test_run_pagination_cap_cannot_establish_idle(self):
        replies = {
            runs_path("scenario.yml", number): page("workflow_runs",
                [run(index, status="queued") for index in range((number - 1) * 100 + 1, number * 100 + 1)],
                total=301, more=True)
            for number in range(1, 4)
        }
        http, logs = self.evaluate(replies, (False, False))
        self.assertEqual(3, len(http.calls))
        self.assertIn('"reason": "pagination_cap"', logs)

    def test_twenty_completed_attempt_cap_defers_stop_but_preserves_idle(self):
        replies = {
            runs_path("scenario.yml"): page("workflow_runs", [
                run(index, created=10000 + index, updated=100) for index in range(1, 22)]),
            runs_path("cluster-smoke.yml"): page("workflow_runs", []),
            **{jobs_path(index): page("jobs", [{"id": index, "completed_at": stamp(3600)}])
               for index in range(1, 21)},
        }
        http, logs = self.evaluate(replies, (True, False))
        self.assertEqual(22, len(http.calls))
        self.assertIn('"reason": "completed_run_cap"', logs)
        self.assertIn('"inspected": 20', logs)

    def test_oversize_job_response_is_unknown_end_and_never_logs_body(self):
        http, logs = self.evaluate({
            runs_path("scenario.yml"): page("workflow_runs", [run(1)]),
            runs_path("cluster-smoke.yml"): page("workflow_runs", []),
            jobs_path(1): (b"x" * (dispatch.MAX_BYTES + 1), False),
        }, (True, False))
        self.assertIn('"reason": "response_byte_cap"', logs)
        self.assertNotIn("x" * 100, logs)
        self.assertEqual(1, http.responses[-1].read_sizes[-1])


if __name__ == "__main__":
    unittest.main()

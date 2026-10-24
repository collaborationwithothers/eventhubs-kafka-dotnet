#!/usr/bin/env python3
"""Bounded Actions history used by the destroy-only sweeper."""

import contextlib
import datetime as dt
import http.client
import json
import os
import re
import signal
import time
import urllib.error
import urllib.request


MAX_BYTES = 4 * 1024 * 1024
MAX_PAGES = 3
MAX_COMPLETED = 20
WORKFLOWS = ("scenario.yml", "cluster-smoke.yml")


class Unknown(Exception):
    """History cannot prove a cleanup predicate."""


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


OPENER = urllib.request.build_opener(NoRedirect())


def log(event, **fields):
    print(json.dumps({"event": event, **fields}, sort_keys=True), flush=True)


def timestamp(value):
    if not isinstance(value, str):
        raise Unknown("invalid_timestamp")
    try:
        result = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
        if result.tzinfo is None:
            raise Unknown("invalid_timestamp")
        return result.astimezone(dt.timezone.utc)
    except (ValueError, OverflowError) as error:
        raise Unknown("invalid_timestamp") from error


@contextlib.contextmanager
def response_deadline(seconds):
    # A socket timeout alone permits a slow stream to exceed the response budget.
    def expired(_signum, _frame):
        raise Unknown("response_deadline")

    previous = signal.signal(signal.SIGALRM, expired)
    signal.setitimer(signal.ITIMER_REAL, seconds)
    try:
        yield
    finally:
        signal.setitimer(signal.ITIMER_REAL, 0)
        signal.signal(signal.SIGALRM, previous)


class Actions:
    def __init__(self, repository, token):
        if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
            raise Unknown("invalid_repository")
        if not token:
            raise Unknown("missing_token")
        self.root = "https://api.github.com/repos/" + repository
        self.token = token
        self.deadline = time.monotonic() + 600

    def remaining(self):
        remaining = self.deadline - time.monotonic()
        if remaining <= 0:
            raise Unknown("lookup_deadline")
        return remaining

    def get(self, path, allow_absent=False):
        budget = min(60, self.remaining())
        request = urllib.request.Request(self.root + path, headers={
            "Authorization": "Bearer " + self.token,
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
            "User-Agent": "parchwell-lifecycle",
        })
        try:
            with response_deadline(budget):
                with OPENER.open(request, timeout=budget) as response:
                    body = bytearray()
                    while True:
                        chunk = response.read(min(65536, MAX_BYTES + 1 - len(body)))
                        if not chunk:
                            break
                        body.extend(chunk)
                        if len(body) > MAX_BYTES:
                            raise Unknown("response_byte_cap")
                    document = json.loads(body)
                    more = bool(re.search(r'rel="?next"?(?:;|,|$)',
                                          response.headers.get("Link", "")))
            self.remaining()
        except urllib.error.HTTPError as error:
            # Only the initial workflow listing can establish a missing workflow.
            if error.code == 404 and allow_absent:
                return None, False
            raise Unknown("http_" + str(error.code)) from None
        except (OSError, http.client.HTTPException, ValueError, RecursionError) as error:
            # Exception text and server bodies can contain credentials; do not log them.
            raise Unknown("response_invalid") from error
        if not isinstance(document, dict):
            raise Unknown("response_shape")
        return document, more

    def pages(self, path, key, allow_absent=False):
        retained = []
        expected = None
        for page in range(1, MAX_PAGES + 1):
            data, more = self.get(path + "?per_page=100&page=" + str(page),
                                  allow_absent=allow_absent and page == 1)
            if data is None:
                log("workflow_absent", path=path)
                return []
            items = data.get(key)
            total = data.get("total_count")
            if (not isinstance(items, list) or len(items) > 100
                    or type(total) is not int or total < 0
                    or (expected is not None and total != expected)):
                raise Unknown("pagination_shape")
            expected = total
            retained.extend(items)
            log("history_page", path=path, page=page, count=len(items),
                total_count=total, next_page=more)
            if len(retained) > total:
                raise Unknown("pagination_changed")
            if not more and len(retained) == total:
                return retained
            if not items:
                raise Unknown("pagination_incomplete")
        raise Unknown("pagination_cap")


def lookup(api):
    runs = []
    try:
        seen = set()
        for workflow in WORKFLOWS:
            records = api.pages("/actions/workflows/" + workflow + "/runs",
                                "workflow_runs", allow_absent=True)
            for run in records:
                if not isinstance(run, dict):
                    raise Unknown("run_shape")
                run_id, attempt, status = (run.get("id"), run.get("run_attempt"),
                                           run.get("status"))
                if (type(run_id) is not int or run_id <= 0 or run_id in seen
                        or type(attempt) is not int or attempt <= 0
                        or not isinstance(status, str)):
                    raise Unknown("run_shape")
                seen.add(run_id)
                created = timestamp(run.get("created_at"))
                updated = timestamp(run.get("updated_at"))
                log("dispatch_run", workflow=workflow, id=run_id, run_attempt=attempt,
                    status=status, created_at=created.isoformat(), updated_at=updated.isoformat())
                runs.append((created, updated, run_id, attempt, status))
        api.remaining()
    except Unknown as error:
        log("dispatch_activity_unknown", reason=str(error))
        return False, False

    # queued is a future dispatch. All other non-completed statuses, including
    # unfamiliar API statuses, may own infrastructure and defer both operations.
    if any(run[4] not in ("completed", "queued") for run in runs):
        log("dispatch_active_or_unknown")
        return False, False

    latest = None
    inspected = 0
    try:
        for created, updated, run_id, attempt, status in sorted(runs, reverse=True):
            api.remaining()
            if status != "completed":
                continue
            if latest is not None and created < latest and updated <= latest:
                continue
            if inspected == MAX_COMPLETED:
                raise Unknown("completed_run_cap")
            inspected += 1
            jobs = api.pages(f"/actions/runs/{run_id}/attempts/{attempt}/jobs", "jobs")
            completion = None
            seen_jobs = set()
            for job in jobs:
                if (not isinstance(job, dict) or type(job.get("id")) is not int
                        or job["id"] in seen_jobs):
                    raise Unknown("job_shape")
                seen_jobs.add(job["id"])
                end = job.get("completed_at")
                if end is not None:
                    end = timestamp(end)
                    completion = max(completion, end) if completion else end
                log("dispatch_job", run_id=run_id, run_attempt=attempt,
                    job_id=job["id"], completed_at=end.isoformat() if end else None)
            if completion is None:
                raise Unknown("no_completed_jobs")
            latest = max(latest, completion) if latest else completion
        if latest is None:
            raise Unknown("no_completion_history")
        api.remaining()
    except Unknown as error:
        log("dispatch_end_unknown", reason=str(error), inspected=inspected)
        return True, False

    now = dt.datetime.now(dt.timezone.utc)
    age = (now - latest).total_seconds()
    eligible = age >= 3600
    log("dispatch_end", completed_at=latest.isoformat(), now=now.isoformat(),
        age_seconds=age, inspected=inspected, stop_eligible=eligible)
    return True, eligible


def main():
    idle, stop_eligible = False, False
    try:
        api = Actions(os.environ.get("GITHUB_REPOSITORY", ""),
                      os.environ.get("GITHUB_TOKEN", ""))
        idle, stop_eligible = lookup(api)
    except Unknown as error:
        log("dispatch_activity_unknown", reason=str(error))
    output = os.environ.get("GITHUB_OUTPUT")
    if not output:
        log("dispatch_output_missing")
        return 2
    with open(output, "a", encoding="utf-8") as stream:
        stream.write("idle=" + str(idle).lower() + "\n")
        stream.write("stop_eligible=" + str(stop_eligible).lower() + "\n")
    log("dispatch_state", idle=idle, stop_eligible=stop_eligible)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

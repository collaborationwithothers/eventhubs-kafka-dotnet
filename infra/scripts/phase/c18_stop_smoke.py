"""The C18 stop smoke: delete an idle consumer member's pod three times and record what Kubernetes and today's worker do.

It answers two questions that ADR-022 "Amendment 1" leaves to a smoke, whether the TERM signal of a pod deletion
reaches the worker process and whether the workflow can read the deleted pod's container end time, and a third that the
plan adds: whether the Job starts a replacement pod. It changes no worker, chart or scenario, judges nothing and publishes no result. The sequence, the bounds and the
evidence file are set by items/613/plan-c18-stop-smoke.md; docs/operations/c18-stop-smoke.md describes the evidence.
The workflow alone creates and deletes every Job and pod (ADR-020).
"""
import codecs
import json
import math
import os
import re
import signal
import subprocess
import threading
import time
from datetime import datetime, timezone
from pathlib import Path

TRIALS = (1, 2, 3)
MODE = "consumer-subject"
CONTAINER = "enrichment-consumer"
RECORD_CONTAINER = "consumer-record"
# Policy values set by Hari (decision 7237226936, point 3), not measurements, except LIST_INTERVAL_SECONDS (plan 3.3 step 7).
READINESS_CAP_SECONDS = 300
READINESS_INTERVAL_SECONDS = 5
DELAY_AFTER_READY_SECONDS = 30
OBSERVATION_CAP_SECONDS = 120
POLL_INTERVAL_SECONDS = 1
LIST_INTERVAL_SECONDS = 5
GRACE_SECONDS = 30  # charts/parchwell/values.yaml terminationGracePeriodSeconds
TERMINATION_RESERVE = 5  # equals phase.py TERMINATION_RESERVE: Phase.run refuses a command with this many seconds or fewer left
PARTITIONS = 32  # infra/ephemeral/eventhubs.tf, document-events partition_count
RECORD_BLOB_MAXIMUM = 65536 * 40000  # the worker's row cap times rows per blob (ConsumerRecordLimits.cs)
LIST_LIMIT = 1000
MIB = 1024 * 1024
WATCH_BYTE_LIMIT = 8 * MIB
LOG_BYTE_LIMIT = 1048576
RECORD_BLOB = r"[^/]+/record/[0-9]{6}\.jsonl"
FAILURE_LINE = re.compile(r"consumer_processing_failed reason=([a-z_]{1,64}) ")
STORAGE_NOT_READY = "consumer_storage_access_not_ready"
KILLED = 137  # 128 plus SIGKILL, which is signal 9 (inference from container-runtime practice; the signal field is recorded beside it)
REASONS = ("run_not_started", STORAGE_NOT_READY, "safety_stop", "render_failed", "apply_failed", "readiness_not_observed",
           "pod_not_running", "delete_failed")

# Closed shapes of the evidence file (plan section 3.4). None is a leaf; a dict is a closed object; a list holds one shape.
TERMINATED = {"exit_code": None, "signal": None, "reason": None, "started_at": None, "finished_at": None}
TRIAL_SHAPE = {
    "trial": None, "outcome": None, "reason": None, "job": None, "pod": None, "pod_uid": None, "node": None,
    "readiness": {"outcome": None, "polls": None, "elapsed_seconds": None, "last_condition": None},
    "delete": {"issued_at": None, "exit_code": None, "deletion_timestamp": None, "deletion_grace_period_seconds": None},
    "watch": {"started": None, "events": None, "terminated": TERMINATED, "terminated_event_type": None,
              "terminated_received_at": None, "deleted_received_at": None},
    "poll": {"polls": None, "longest_seconds": None, "terminated": TERMINATED, "terminated_read_at": None, "not_found_at": None},
    "log": {"stream_started": None, "failure_line_reason": None},
    "pods_seen": [None], "replacement_pod": None,
    "job_conditions": [{"type": None, "status": None, "reason": None}],
    "job_status": {"active": None, "failed": None, "terminating": None},
    "sigterm": None, "end_time": None, "end_time_source": None}
STORAGE_SHAPE = {"outcome": None, "attempts": None, "elapsed_seconds": None, "last_failure": None, "probe_delete_failed": None}
BOUNDS = {"trials": len(TRIALS), "readiness_cap_seconds": READINESS_CAP_SECONDS, "readiness_interval_seconds": READINESS_INTERVAL_SECONDS,
          "delay_after_ready_seconds": DELAY_AFTER_READY_SECONDS, "observation_cap_seconds": OBSERVATION_CAP_SECONDS,
          "poll_interval_seconds": POLL_INTERVAL_SECONDS}


def utc():
    return datetime.now(timezone.utc)


def stamp(moment):
    """A runner time as RFC 3339 UTC with milliseconds."""
    moment = moment.astimezone(timezone.utc)
    return moment.strftime("%Y-%m-%dT%H:%M:%S") + f".{moment.microsecond // 1000:03d}Z"


def parse_time(text):
    return datetime.fromisoformat(text.replace("Z", "+00:00")).astimezone(timezone.utc)


def clean(value, shape):
    """Copy value into the closed shape: keys outside it are dropped, absent keys and non-objects are None, a leaf keeps only a JSON scalar."""
    if isinstance(shape, dict):
        return {key: clean(value.get(key), sub) for key, sub in shape.items()} if isinstance(value, dict) else None
    if isinstance(shape, list):
        return [clean(item, shape[0]) for item in value] if isinstance(value, list) else []
    return value if value is None or isinstance(value, (str, int, float, bool)) else None


def terminated_state(pod):
    """The state.terminated object of the enrichment-consumer container, with snake-case keys, or None."""
    try:
        for status in pod["status"]["containerStatuses"]:
            if status.get("name") == CONTAINER and isinstance(status.get("state", {}).get("terminated"), dict):
                found = status["state"]["terminated"]
                return {"exit_code": found.get("exitCode"), "signal": found.get("signal"), "reason": found.get("reason"),
                        "started_at": found.get("startedAt"), "finished_at": found.get("finishedAt")}
    except (KeyError, TypeError, AttributeError):
        return None
    return None


def first_deletion(objects):
    """(deletionTimestamp, deletionGracePeriodSeconds) of the first pod object, in time order, that carries a deletionTimestamp."""
    for _, pod in sorted(objects, key=lambda item: item[0]):
        metadata = pod.get("metadata") if isinstance(pod, dict) else None
        if isinstance(metadata, dict) and metadata.get("deletionTimestamp"):
            return metadata["deletionTimestamp"], metadata.get("deletionGracePeriodSeconds")
    return None, None


def classify(terminated, issued_at, deadline, failure_reason):
    """The sigterm label of plan section 3.5: the first matching rule wins, and a null deadline only stops reached_worker."""
    if terminated is None:
        return "undetermined"
    code, finished = terminated.get("exit_code"), terminated.get("finished_at")
    if code != KILLED and finished and deadline:
        floor = parse_time(issued_at).replace(microsecond=0)
        if floor <= parse_time(finished) < parse_time(deadline):
            return "reached_worker"
    if code == KILLED:
        return "reached_but_killed_at_grace" if failure_reason is not None else "not_reached"
    return "undetermined"


def end_time(watch_terminated, poll_terminated):
    """(end_time, end_time_source): which method read the container end time."""
    terminated = watch_terminated if watch_terminated is not None else poll_terminated
    read = terminated is not None and terminated.get("finished_at") is not None
    sources = [name for name, found in (("watch", watch_terminated), ("poll", poll_terminated))
               if found is not None and found.get("finished_at") is not None]
    source = "both" if len(sources) == 2 else sources[0] if sources else "none"
    return ("read" if read else "not_read"), source


def exit_status(trials):
    return int(any(trial["outcome"] != "observed" or trial["sigterm"] == "undetermined" for trial in trials))


def stop_group(process):
    """Stop the process group of a background kubectl: TERM, then KILL after 5 s."""
    if process.poll() is None:
        try:
            os.killpg(process.pid, signal.SIGTERM)
            process.wait(timeout=5)
        except (ProcessLookupError, PermissionError):
            pass
        except subprocess.TimeoutExpired:
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except (ProcessLookupError, PermissionError):
                pass
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        pass


class Watch:
    """One `kubectl get pod --watch --output-watch-events -o json` in the background.

    It parses the stream as a sequence of JSON objects, each with keys type and object, and stamps each with the runner
    time at which it was complete. Output that does not parse sets malformed and stops the parsing; the trial goes on.
    """
    def __init__(self, args, cwd):
        self.events = []
        self.started = False
        self.malformed = False
        self.text = ""
        self.seen = 0
        self.decoder = codecs.getincrementaldecoder("utf-8")(errors="strict")
        self.process = None
        self.thread = None
        self.stopped = False
        try:
            self.process = subprocess.Popen(args, cwd=cwd, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                                            stderr=subprocess.DEVNULL, start_new_session=True)
        except OSError:
            return
        self.started = True
        self.thread = threading.Thread(target=self.read, daemon=True)
        self.thread.start()

    def read(self):
        descriptor = self.process.stdout.fileno()
        while True:
            try:
                chunk = os.read(descriptor, 65536)
            except OSError:
                break
            if not chunk:
                break
            self.feed(chunk)
        self.finish()

    def feed(self, chunk):
        if self.malformed:
            return
        self.seen += len(chunk)
        try:
            self.text += self.decoder.decode(chunk)
        except UnicodeDecodeError:
            self.malformed = True
            return
        if self.seen > WATCH_BYTE_LIMIT:
            self.malformed = True
            return
        decoder = json.JSONDecoder()
        while True:
            self.text = self.text.lstrip()
            if not self.text:
                return
            if self.text[0] != "{":
                self.malformed = True
                return
            try:
                event, end = decoder.raw_decode(self.text)
            except json.JSONDecodeError:
                return  # incomplete so far; finish() decides at the end of the stream
            if not isinstance(event, dict) or "type" not in event or not isinstance(event.get("object"), dict):
                self.malformed = True
                return
            self.events.append({"type": event["type"], "object": event["object"], "received_at": utc()})
            self.text = self.text[end:]

    def finish(self):
        if self.text.strip():
            self.malformed = True

    def stop(self):
        if self.process is not None and not self.stopped:
            self.stopped = True
            if self.process.poll() not in (None, 0) and not self.events:
                self.started = False  # the command exited non-zero before it printed an event, for example a Forbidden watch
            stop_group(self.process)
        if self.thread is not None:
            self.thread.join(timeout=5)
            if not self.thread.is_alive():
                self.process.stdout.close()


class LogStream:
    """One `kubectl logs -f` in the background, written to a private file."""
    def __init__(self, args, path, cwd):
        self.path = path
        self.started = False
        self.process = None
        self.stopped = False
        try:
            descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
            with os.fdopen(descriptor, "wb") as stream:
                self.process = subprocess.Popen(args, cwd=cwd, stdin=subprocess.DEVNULL, stdout=stream,
                                                stderr=subprocess.DEVNULL, start_new_session=True)
        except OSError:
            return
        self.started = True

    def stop(self):
        if self.process is not None and not self.stopped:
            self.stopped = True
            if self.process.poll() not in (None, 0) and self.path.stat().st_size == 0:
                self.started = False  # the command exited non-zero before it wrote a line, for example a Forbidden logs call
            stop_group(self.process)


class Smoke:
    def __init__(self, phase, failure):
        self.p = phase
        self.failure = failure
        self.cr = None

    def consumer(self):
        if self.cr is None:
            self.cr = self.p.consumer_repeats()
        return self.cr

    # -- commands ---------------------------------------------------------------------------------------------------

    def prepare(self):
        return self.p.prepare_shared("C03", "C0", "C0", "c18s", purpose="c18_stop_smoke", run_kind="smoke")

    def run(self):
        consumer = self.consumer()
        phase = self.p.read("phase.json")
        if not consumer.storage_access():
            reason = "safety_stop" if consumer.safety_stop() else STORAGE_NOT_READY
            trials = [self.not_attempted(number, self.p.mode_job(phase, MODE, number), reason) for number in TRIALS]
            for trial in trials:
                self.save_trial(trial)
            return exit_status(trials)
        trials = []
        for number in TRIALS:
            if consumer.safety_stop():
                trial = self.not_attempted(number, self.p.mode_job(phase, MODE, number), "safety_stop")
            else:
                trial = self.trial(number, phase)
            self.save_trial(trial)
            trials.append(trial)
        return exit_status(trials)

    def collect(self):
        try:
            phase = self.p.read("phase.json")
            try:
                state = self.p.state()
            except (OSError, ValueError, KeyError, TypeError, UnicodeError, self.failure):
                state = {}
            saved = {}
            block = state.get("c18_stop_smoke")
            for trial in block.get("trials", []) if isinstance(block, dict) else []:
                if isinstance(trial, dict) and trial.get("trial") in TRIALS:
                    saved[trial["trial"]] = trial
            trials = []
            for number in TRIALS:
                job = self.p.mode_job(phase, MODE, number)
                trial = saved.get(number)
                trial = (self.not_attempted(number, job, "run_not_started") if trial is None
                         else self.closed(clean(trial, TRIAL_SHAPE), number, job))
                trials.append(trial)
            access = state.get("consumer_storage_access") if saved else None  # no saved trial: plan 3.4 says null
            document = {"dialect": "c18_stop_smoke", "schema_version": 1, "run_id": phase["run_id"],
                        "source_commit": phase["source_commit"], "scenario": "C03", "configuration": "C0",
                        "grace_seconds": GRACE_SECONDS, "bounds": BOUNDS,
                        "consumer_storage_access": clean(access, STORAGE_SHAPE) if isinstance(access, dict) else None,
                        "trials": trials}
            directory = self.p.artifacts / "c18-stop-smoke"
            directory.mkdir(mode=0o700, exist_ok=True)
            self.write(directory / "c18-stop-smoke.json", json.dumps(document, sort_keys=True, indent=2, allow_nan=False) + "\n")
            return 0
        except (OSError, ValueError, KeyError, TypeError, UnicodeError, self.failure) as error:
            print("c18_stop_smoke collect failed: " + type(error).__name__, flush=True)
            return 1

    @staticmethod
    def write(path, text):
        descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
        os.fchmod(descriptor, 0o600)
        with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
            stream.write(text)

    def closed(self, trial, number, job):
        """A saved trial for the evidence file: its number and job are this run's, and the failure line is read from the log again."""
        trial["trial"], trial["job"] = number, job
        if trial["outcome"] not in ("observed", "not_attempted"):
            trial["outcome"] = "not_attempted"
            trial["reason"] = "run_not_started"
        if trial["reason"] not in REASONS:
            trial["reason"] = None if trial["outcome"] == "observed" else "run_not_started"
        if isinstance(trial["log"], dict):
            path = self.p.private / f"{job}-stream.log"
            if path.is_file():
                trial["log"]["failure_line_reason"] = self.failure_reason(path)
            elif not (isinstance(trial["log"]["failure_line_reason"], str)
                      and re.fullmatch(r"[a-z_]{1,64}", trial["log"]["failure_line_reason"])):
                trial["log"]["failure_line_reason"] = None
        return trial

    # -- records ----------------------------------------------------------------------------------------------------

    def save_trial(self, trial):
        state = self.p.state()
        block = state.setdefault("c18_stop_smoke", {"trials": []})
        block["trials"] = [saved for saved in block["trials"] if saved["trial"] != trial["trial"]] + [trial]
        self.p.save_state(state)

    @staticmethod
    def not_attempted(number, job, reason, readiness=None, delete=None):
        trial = dict.fromkeys(TRIAL_SHAPE)
        trial.update(trial=number, job=job, outcome="not_attempted", reason=reason, readiness=readiness, delete=delete,
                     pods_seen=[], job_conditions=[])
        return trial

    @staticmethod
    def failure_reason(path):
        """The captured group of the first worker failure line in the log, or None. No other log text is kept."""
        try:
            for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
                found = FAILURE_LINE.match(line)
                if found:
                    return found.group(1)
        except OSError:
            return None
        return None

    # -- one trial --------------------------------------------------------------------------------------------------

    def start_watch(self, args):
        return Watch(args, self.p.repo)

    def start_log(self, args, path):
        return LogStream(args, path, self.p.repo)

    def trial(self, number, phase):
        job = self.p.mode_job(phase, MODE, number)
        applied = False
        watch = log = None
        try:
            if self.p.render(MODE, number) is None:
                return self.not_attempted(number, job, "render_failed")
            applied = True  # a failed apply may still have created the Job, so cleanup follows it too
            code = self.p.run(["kubectl", "apply", "-f", str(self.p.private / f"rendered-{job}.yaml"), "--request-timeout=30s"], 60)
            applied_at = time.monotonic()
            if code:
                return self.not_attempted(number, job, "apply_failed")
            readiness, ready_at = self.readiness(phase, number, job, applied_at)
            if ready_at is None:
                return self.not_attempted(number, job, "readiness_not_observed", readiness=readiness)
            seen = []
            items = self.list_pods(job, seen)
            running = items if items is not None and len(items) == 1 and (items[0].get("status") or {}).get("phase") == "Running" else None
            if running is None:
                return self.not_attempted(number, job, "pod_not_running", readiness=readiness)
            pod = running[0]["metadata"]["name"]
            uid, node = running[0]["metadata"].get("uid"), (running[0].get("spec") or {}).get("nodeName")
            log_path = self.p.private / f"{job}-stream.log"
            log = self.start_log(["kubectl", "logs", "-f", "-n", "parchwell", pod, "-c", CONTAINER,
                                  f"--limit-bytes={LOG_BYTE_LIMIT}", "--request-timeout=0"], log_path)
            watch = self.start_watch(["kubectl", "get", "pod", pod, "-n", "parchwell", "--watch", "--output-watch-events",
                                      "-o", "json", "--request-timeout=0"])
            wait = ready_at + DELAY_AFTER_READY_SECONDS - time.monotonic()
            if wait > 0:
                self.p.sleep(wait)
            issued_at = utc()
            issued = time.monotonic()
            code = self.p.run(["kubectl", "delete", "pod", pod, "-n", "parchwell", "--wait=false", "--request-timeout=30s"], 60)
            delete = {"issued_at": stamp(issued_at), "exit_code": code, "deletion_timestamp": None, "deletion_grace_period_seconds": None}
            if code:
                return self.not_attempted(number, job, "delete_failed", readiness=readiness, delete=delete)
            poll, objects = self.observe(job, pod, issued, seen)
            watch.stop()
            log.stop()
            self.list_pods(job, seen)
            job_conditions, job_status = self.read_job(job)
            return self.observed(number, job, pod, uid, node, readiness, delete, issued_at, watch, log, log_path, poll,
                                 objects, seen, job_conditions, job_status)
        finally:
            for stream in (watch, log):
                if stream is not None:
                    stream.stop()
            if applied:
                self.consumer().delete_check_job(job)

    def readiness(self, phase, number, job, applied_at):
        """Poll the member's record until it is registered and holds every partition. Returns (record, time it was ready or None)."""
        prefix = f"{phase['run_id']}/{phase['phase'].upper()}/r{number}/consumer/"
        record = {"outcome": "not_observed", "polls": 0, "elapsed_seconds": None, "last_condition": "member_count"}
        ready_at = None
        previous = self.p.command_deadline
        self.p.command_deadline = applied_at + READINESS_CAP_SECONDS
        try:
            while time.monotonic() - applied_at < READINESS_CAP_SECONDS:
                record["polls"] += 1
                record["last_condition"] = self.member_condition(prefix, job)
                if record["last_condition"] == "ready":
                    ready_at = time.monotonic()
                    break
                if applied_at + READINESS_CAP_SECONDS - time.monotonic() <= TERMINATION_RESERVE:
                    break  # Phase.run would refuse every further command, so stop instead of spinning until the cap
                self.p.sleep(READINESS_INTERVAL_SECONDS)
        finally:
            self.p.command_deadline = previous
        record["elapsed_seconds"] = max(0, math.floor(time.monotonic() - applied_at))
        record["outcome"] = "ready" if ready_at is not None else "not_observed"
        return record, ready_at

    def member_condition(self, prefix, job):
        """One poll. Returns "ready" or the first failing condition; it never raises and never keeps blob contents."""
        strict_json = self.consumer().strict_json
        account = self.p.blob_host().split(".")[0]
        listing = self.p.private / f"{job}-list.json"
        args = ["az", "storage", "blob", "list", "--account-name", account, "--container-name", RECORD_CONTAINER, "--prefix", prefix,
                "--auth-mode", "login", "--num-results", str(LIST_LIMIT), "--only-show-errors", "-o", "json"]
        if self.p.run(args, 60, output=listing, maximum=4 * MIB):
            return "list_failed"
        try:
            names = [entry["name"] for entry in strict_json(listing.read_bytes())]
            if not all(isinstance(name, str) and name.startswith(prefix) for name in names):
                return "list_failed"
        except Exception:  # a malformed listing is a failed poll, not a crash
            return "list_failed"
        members = {}
        for name in names:
            members.setdefault(name[len(prefix):].split("/", 1)[0], []).append(name)
        registered = [member for member in members if f"{prefix}{member}/registration.json" in members[member]]
        if len(registered) != 1:
            return "member_count"
        last = None
        for name in sorted(name for name in members[registered[0]] if re.fullmatch(re.escape(prefix) + RECORD_BLOB, name)):
            content = self.p.download_blob(name, self.p.private / f"{job}-record.jsonl", RECORD_BLOB_MAXIMUM,
                                           self.p.command_deadline, container=RECORD_CONTAINER, minimum=0)
            if content is None:
                return "download_failed"
            for line in content.split(b"\n"):
                if not line:
                    continue
                try:
                    row = strict_json(line)
                except Exception:  # invalid JSON, duplicate keys, bad UTF-8
                    return "row_invalid"
                if not isinstance(row, dict):
                    return "row_invalid"
                if row.get("kind") == "partition_event":
                    partitions = row.get("partitions")
                    if (not isinstance(row.get("event"), str) or not isinstance(partitions, list)
                            or not all(type(item) is int for item in partitions)):
                        return "row_invalid"
                    last = row
        if last is None or last["event"] != "assigned":
            return "not_assigned"
        return "ready" if set(last["partitions"]) == set(range(PARTITIONS)) else "coverage"

    def list_pods(self, job, seen):
        """List the pods of the Job, add their names to seen, and return the items, or None when the listing failed."""
        path = self.p.private / f"{job}-pods.json"
        if self.p.run(["kubectl", "get", "pods", "-n", "parchwell", "-l", f"job-name={job}", "-o", "json", "--request-timeout=30s"],
                      30, output=path, maximum=4 * MIB):
            return None
        try:
            items = self.consumer().strict_json(path.read_bytes())["items"]
            names = [item["metadata"]["name"] for item in items]
        except Exception:
            return None
        seen.extend(name for name in names if name not in seen)
        return items

    def observe(self, job, pod, issued, seen):
        """Poll the deleted pod until NotFound or 120 s after the delete. Returns (poll record, pod objects read, with their times)."""
        strict_json = self.consumer().strict_json
        path = self.p.private / f"{job}-pod.json"
        poll = {"polls": 0, "longest_seconds": 0.0, "terminated": None, "terminated_read_at": None, "not_found_at": None}
        objects = []
        next_list = time.monotonic()
        while True:
            if time.monotonic() >= next_list:
                self.list_pods(job, seen)
                next_list = time.monotonic() + LIST_INTERVAL_SECONDS
            began = time.monotonic()
            code = self.p.run(["kubectl", "get", "pod", pod, "-n", "parchwell", "--ignore-not-found", "-o", "json",
                               "--request-timeout=5s"], 15, output=path, maximum=4 * MIB)
            returned = time.monotonic()
            at = utc()
            poll["polls"] += 1
            poll["longest_seconds"] = max(poll["longest_seconds"], round(returned - began, 3))
            if code == 0:
                content = path.read_bytes().strip()
                if not content:
                    poll["not_found_at"] = stamp(at)
                    break
                try:
                    found = strict_json(content)
                except Exception:  # an unreadable poll is a failed poll
                    found = None
                if isinstance(found, dict):
                    objects.append((at, found))
                    terminated = terminated_state(found)
                    if terminated is not None and poll["terminated"] is None:
                        poll["terminated"], poll["terminated_read_at"] = terminated, stamp(at)
            if returned >= issued + OBSERVATION_CAP_SECONDS:
                break
            self.p.sleep(POLL_INTERVAL_SECONDS)
        return poll, objects

    def read_job(self, job):
        path = self.p.private / f"{job}-job.json"
        if self.p.run(["kubectl", "get", "job", job, "-n", "parchwell", "-o", "json", "--request-timeout=30s"], 30,
                      output=path, maximum=4 * MIB):
            return [], None
        try:
            status = self.consumer().strict_json(path.read_bytes()).get("status") or {}
        except Exception:
            return [], None
        conditions = [condition for condition in status.get("conditions") or [] if isinstance(condition, dict)]
        return conditions, {key: status.get(key) for key in ("active", "failed", "terminating")}

    def observed(self, number, job, pod, uid, node, readiness, delete, issued_at, watch, log, log_path, poll, objects,
                 seen, job_conditions, job_status):
        events = watch.events
        terminated = next(((event, terminated_state(event["object"])) for event in events if terminated_state(event["object"]) is not None), None)
        deleted = next((event for event in events if event["type"] == "DELETED"), None)
        watched = {"started": watch.started, "events": len(events),
                   "terminated": None if terminated is None else terminated[1],
                   "terminated_event_type": None if terminated is None else terminated[0]["type"],
                   "terminated_received_at": None if terminated is None else stamp(terminated[0]["received_at"]),
                   "deleted_received_at": None if deleted is None else stamp(deleted["received_at"])}
        arrived = [(event["received_at"], event["object"]) for event in events if event["received_at"] >= issued_at]
        timestamp, grace = first_deletion(arrived + objects)
        delete = {**delete, "deletion_timestamp": timestamp, "deletion_grace_period_seconds": grace}
        reason = self.failure_reason(log_path) if log.started else None
        found = watched["terminated"] if watched["terminated"] is not None else poll["terminated"]
        read, source = end_time(watched["terminated"], poll["terminated"])
        return {"trial": number, "outcome": "observed", "reason": None, "job": job, "pod": pod, "pod_uid": uid, "node": node,
                "readiness": readiness, "delete": delete, "watch": watched, "poll": poll,
                "log": {"stream_started": log.started, "failure_line_reason": reason}, "pods_seen": list(seen),
                "replacement_pod": any(name != pod for name in seen),
                "job_conditions": [{key: condition.get(key) for key in ("type", "status", "reason")} for condition in job_conditions],
                "job_status": job_status, "sigterm": classify(found, delete["issued_at"], timestamp, reason),
                "end_time": read, "end_time_source": source}

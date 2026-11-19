"""Run the five repeats of a consumer scenario (C01, C03, C10) with a subject and a witness member Job per repeat.

Each repeat starts the two consumer members, polls their records until both hold an assignment, then runs the
start line, the load generator, the member waits and the verifier. The workflow alone creates, waits for and
deletes every Job (ADR-020). The order and the records are set by docs/operations/consumer-workflow.md; this
module records what happened and judges nothing.
"""
import json
import math
import re
import time

SLOTS = (1, 2, 3, 4, 5)
BAD_OUTCOMES = frozenset(("render_failed", "apply_failed", "unknown", "collect_error", "not_attempted"))  # equals phase.py BAD_OUTCOMES
MEMBERS = (("subject", "consumer-subject"), ("witness", "consumer-witness"))
READINESS_MODE = "consumer-readiness"
CONTAINER = "consumer-record"
# Policy values set by Hari (decision 6925863575 and the readiness answer of decision 6915070211), not measurements.
CONSUMER_PHASE_SECONDS = 7200
CONSUMER_REPEAT_RESERVE_SECONDS = 5100
READINESS_CAP_SECONDS = 300
READINESS_INTERVAL_SECONDS = 5
# Policy values set by Hari on 2026-11-19 (issue 556 decision), not measurements: the write check before the repeats.
STORAGE_CHECK_INTERVAL_SECONDS = 60
STORAGE_CHECK_CAP_SECONDS = 1800
STORAGE_CHECK_MODE = "consumer-storage-check"
STORAGE_CHECK_NOT_READY = "consumer_storage_access_not_ready"
STORAGE_CHECK_RESULT = re.compile(
    r"consumer_storage_check result=(ok|failed|probe_delete_failed|configuration_invalid)"
    r"(?: (kind=http status=[0-9]{3}(?: code=[A-Za-z0-9]{1,64})?|kind=aadsts code=AADSTS[0-9]{1,8}|kind=socket code=[A-Za-z]{1,64}|kind=other))?")
TERMINATION_RESERVE = 5  # equals phase.py TERMINATION_RESERVE: Phase.run refuses a command with this many seconds or fewer left
PARTITIONS = 32  # infra/ephemeral/eventhubs.tf, document-events partition_count
RECORD_BLOB_MAXIMUM = 65536 * 40000  # the worker's row cap times rows per blob (ConsumerRecordLimits.cs)
LIST_LIMIT = 1000
WAIT_MARGIN_SECONDS = 90
COLLECT_SECONDS = 210
RECORD_BLOB = r"[^/]+/record/[0-9]{6}\.jsonl"


class ConsumerRepeats:
    def __init__(self, phase, strict_json):
        self.p = phase
        self.strict_json = strict_json

    # -- records -----------------------------------------------------------------------------------------

    def blank(self, mode, repeat, job):
        return {"job": job, "repeat": repeat, "mode": mode,
                **{step: None for step in ("render", "apply", "wait", "collect", "delete", "confirm")},
                "deadline": None, "outcome": "not_attempted"}

    def append(self, record):
        state = self.p.state()
        state["modes"].append(record)
        self.p.save_state(state)
        return len(state["modes"]) - 1

    def save(self, index, record, **state_changes):
        state = self.p.state()
        state["modes"][index] = record
        state.update(state_changes)
        self.p.save_state(state)

    def safety_stop(self):
        return bool(self.p.state().get("safety_stop", False))

    def job(self, mode, repeat):
        return self.p.mode_job(self.p.read("phase.json"), mode, repeat)

    # -- the phase -------------------------------------------------------------------------------------------

    def run(self):
        if not self.storage_access():
            for repeat in SLOTS:
                self.skip_repeat(repeat, "safety_stop" if self.safety_stop() else STORAGE_CHECK_NOT_READY)
            return int(any(record["outcome"] in BAD_OUTCOMES for record in self.p.state()["modes"]))
        started = time.monotonic()  # the check above is not part of the consumer phase budget
        for repeat in SLOTS:
            if self.safety_stop():
                self.skip_repeat(repeat, "safety_stop")
            elif started + CONSUMER_PHASE_SECONDS - time.monotonic() < CONSUMER_REPEAT_RESERVE_SECONDS:
                self.skip_repeat(repeat, "orchestration_deadline")
            else:
                self.repeat(repeat)
        return int(any(record["outcome"] in BAD_OUTCOMES for record in self.p.state()["modes"]))

    # -- storage access check ------------------------------------------------------------------------------

    def storage_access(self):
        """Write one probe blob as the consumer identity until it works or 1800 s have passed. Returns True when ready.

        Each attempt is one short Job of the Enrichment image under the enrichment-consumer service account.
        The attempt number takes the place of the repeat number in the Job name. The result goes to the phase
        state under consumer_storage_access, not to the six records of a repeat.
        """
        began = time.monotonic()
        result = {"outcome": "not_ready", "attempts": 0, "elapsed_seconds": 0, "last_failure": None, "probe_delete_failed": False}
        ready = False
        while True:
            result["attempts"] += 1
            outcome, summary = self.storage_attempt(result["attempts"])
            result["probe_delete_failed"] |= outcome == "probe_delete_failed"
            ready = outcome in ("ok", "probe_delete_failed")
            result["last_failure"] = None if ready else summary
            result["elapsed_seconds"] = max(0, math.floor(time.monotonic() - began))
            result["outcome"] = "ready" if ready else "not_ready"
            self.save_storage_access(result)
            if ready or self.safety_stop():
                break
            self.p.sleep(STORAGE_CHECK_INTERVAL_SECONDS)
            if time.monotonic() - began >= STORAGE_CHECK_CAP_SECONDS:
                result["elapsed_seconds"] = max(0, math.floor(time.monotonic() - began))
                self.save_storage_access(result)
                break
        return ready

    def save_storage_access(self, result):
        state = self.p.state()
        state["consumer_storage_access"] = dict(result)
        self.p.save_state(state)

    def storage_attempt(self, attempt):
        """One attempt. Returns (ok | probe_delete_failed | failed, safe summary). It never raises."""
        job = self.p.mode_job(self.p.read("phase.json"), STORAGE_CHECK_MODE, attempt)
        if self.p.render(STORAGE_CHECK_MODE, attempt) is None:
            return "failed", "render_failed"
        rendered = self.p.private / f"rendered-{job}.yaml"
        outcome, summary = "failed", "apply_failed"
        if not self.p.run(["kubectl", "apply", "-f", str(rendered), "--request-timeout=30s"], 60):
            code = self.p.run(["bash", "infra/scripts/lifecycle/lifecycle.sh", "wait-job", job, "120"], 210)
            log = self.p.private / f"{job}.log"
            logged = self.p.run(["kubectl", "logs", f"job/{job}", "-n", "parchwell", "--request-timeout=30s"], 30, output=log, maximum=65536)
            outcome, summary = self.storage_result(code, log if not logged else None)
        self.delete_check_job(job)
        return outcome, summary

    def storage_result(self, wait_code, log):
        """Read the result line of the check pod. Only a line that matches the fixed grammar is kept."""
        line = None
        if log is not None:
            try:
                lines = [text for text in log.read_text(encoding="utf-8", errors="replace").splitlines()
                         if text.startswith("consumer_storage_check ")]
                line = STORAGE_CHECK_RESULT.fullmatch(lines[-1]) if lines else None
            except OSError:
                line = None
        if line is None:
            return "failed", f"wait_exit_{wait_code}_no_result"
        result, detail = line.group(1), line.group(2)
        if (result, wait_code) in (("ok", 0), ("probe_delete_failed", 1)):  # the pod exits 0 and 3; the wait reports Failed as 1
            return result, detail or ""
        return "failed", (detail or result)

    def delete_check_job(self, job):
        """Delete the check Job and confirm no pod remains; any failure sets safety_stop (as delete_confirm does)."""
        deleted = self.p.run(["kubectl", "delete", "job", job, "-n", "parchwell", "--ignore-not-found",
                              "--cascade=foreground", "--wait=true", "--timeout=90s", "--request-timeout=30s"], 120)
        confirmation = self.p.private / f"confirm-{job}.json"
        confirmed = self.p.run(["kubectl", "get", "pods", "-n", "parchwell", "-l", f"job-name={job}", "-o", "json",
                                "--request-timeout=30s"], 30, output=confirmation, maximum=4 * 1024 * 1024)
        empty = False
        if not confirmed:
            try:
                data = self.strict_json(confirmation.read_bytes())
                empty = isinstance(data, dict) and data.get("items") == []
            except Exception:  # phase.strict_json raises phase.Failure, which this module cannot import
                empty = False
        if deleted or confirmed or not empty:
            state = self.p.state()
            state["safety_stop"] = True
            self.p.save_state(state)
            print(f"safety_stop: pods of {job} may still run", flush=True)

    def skip_repeat(self, repeat, reason):
        """Append the six records of a repeat that runs no command."""
        for _, mode in MEMBERS:
            record = self.blank(mode, repeat, self.job(mode, repeat))
            record["reason"] = reason
            self.append(record)
        self.append(self.readiness_record(repeat, outcome="not_attempted", reason=reason))
        for mode in ("start-line", "loadgen", "verify"):
            self.p.run_mode(mode, repeat, skip_reason=reason)

    def readiness_record(self, repeat, **fields):
        record = {"job": self.job(READINESS_MODE, repeat), "repeat": repeat, "mode": READINESS_MODE,
                  "polls": None, "elapsed_seconds": None, "last_condition": None, "outcome": "not_attempted"}
        record.update(fields)
        return record

    def repeat(self, repeat):
        members = {}
        applied_at = {}
        for role, mode in MEMBERS:
            members[role] = self.start_member(role, mode, repeat, applied_at)
            if members[role]["record"]["outcome"] in ("render_failed", "apply_failed"):
                break
        if len(members) < len(MEMBERS) or any(member["record"]["outcome"] in ("render_failed", "apply_failed")
                                             for member in members.values()):
            self.not_started(repeat, members, "consumer_apply_failed")
            return
        readiness = self.readiness(repeat, applied_at["witness"])
        if readiness != "ready":
            for mode in ("start-line", "loadgen", "verify"):
                self.p.run_mode(mode, repeat, skip_reason="consumer_assignment_not_observed")
            self.stop_members(members, "consumer_assignment_not_observed")
            return
        self.p.run_mode("start-line", repeat)
        self.p.run_mode("loadgen", repeat)
        exhausted = False
        for role, _ in MEMBERS:
            exhausted |= self.finish_member(members[role], applied_at[role])
        self.p.run_mode("verify", repeat, skip_reason="consumer_wait_exhausted" if exhausted else None)

    def not_started(self, repeat, members, reason):
        """Rule 2: a member did not start. Missing member records, the four later records, then stop the members."""
        for role, mode in MEMBERS:
            if role not in members:
                record = self.blank(mode, repeat, self.job(mode, repeat))
                record["reason"] = reason
                members[role] = {"index": self.append(record), "record": record, "role": role, "mode": mode}
        self.append(self.readiness_record(repeat, outcome="not_attempted", reason=reason))
        for mode in ("start-line", "loadgen", "verify"):
            self.p.run_mode(mode, repeat, skip_reason=reason)
        self.stop_members(members, reason)

    # -- members -----------------------------------------------------------------------------------------------

    def start_member(self, role, mode, repeat, applied_at):
        record = self.blank(mode, repeat, self.job(mode, repeat))
        member = {"role": role, "mode": mode, "record": record, "index": self.append(record)}
        deadline = self.p.render(mode, repeat)
        record["render"] = self.p.render_code
        record["deadline"] = deadline
        if deadline is None:
            record["outcome"] = "render_failed"
            self.save(member["index"], record)
            return member
        self.save(member["index"], record)
        rendered = self.p.private / f"rendered-{record['job']}.yaml"
        record["apply"] = self.p.run(["kubectl", "apply", "-f", str(rendered), "--request-timeout=30s"], 60)
        applied_at[role] = time.monotonic()
        if record["apply"]:
            record["outcome"] = "apply_failed"
        self.save(member["index"], record)
        return member

    def finish_member(self, member, applied_at):
        """S6 and S7: wait for the member Job, then collect it. Returns True when its end is not known."""
        record = member["record"]
        if self.safety_stop():
            record["reason"] = "safety_stop"
            self.save(member["index"], record)
            return False
        remaining = max(1, math.ceil(applied_at + record["deadline"] - time.monotonic()))
        record["wait"] = self.p.run(["bash", "infra/scripts/lifecycle/lifecycle.sh", "wait-job", record["job"], str(remaining)],
                                    remaining + WAIT_MARGIN_SECONDS)
        record["outcome"] = "complete" if record["wait"] == 0 else "failed" if record["wait"] == 1 else "unknown"
        self.save(member["index"], record)
        record["collect"] = self.p.run(["bash", "infra/scripts/lifecycle/lifecycle.sh", "collect-job", record["job"]], COLLECT_SECONDS)
        if record["collect"] == 1:
            record["collect_gap"] = True
        elif record["collect"] and record["outcome"] != "unknown":
            record["outcome"] = "collect_error"
        self.save(member["index"], record)
        if record["outcome"] == "unknown":
            self.delete_confirm(member)
        return record["outcome"] == "unknown"

    def stop_members(self, members, reason):
        """Rules 2a: stop both members without a wait, collecting each applied member before its delete."""
        for role, _ in MEMBERS:
            member = members[role]
            record = member["record"]
            if record["apply"] == 0:
                record["collect"] = self.p.run(["bash", "infra/scripts/lifecycle/lifecycle.sh", "collect-job", record["job"]], COLLECT_SECONDS)
                record["reason"] = reason
                if record["collect"] == 1:
                    record["collect_gap"] = True
                elif record["collect"]:
                    record["outcome"] = "collect_error"
                self.save(member["index"], record)
            self.delete_confirm(member)

    def delete_confirm(self, member):
        """Delete the member Job and confirm no pod remains; any failure sets safety_stop (as run_mode does)."""
        record = member["record"]
        job = record["job"]
        record["delete"] = self.p.run(["kubectl", "delete", "job", job, "-n", "parchwell", "--ignore-not-found",
                                       "--cascade=foreground", "--wait=true", "--timeout=90s", "--request-timeout=30s"], 120)
        confirmation = self.p.private / f"confirm-{job}.json"
        record["confirm"] = self.p.run(["kubectl", "get", "pods", "-n", "parchwell", "-l", f"job-name={job}", "-o", "json",
                                        "--request-timeout=30s"], 30, output=confirmation, maximum=4 * 1024 * 1024)
        empty = False
        if not record["confirm"]:
            try:
                data = self.strict_json(confirmation.read_bytes())
                empty = isinstance(data, dict) and data.get("items") == []
            except Exception:  # phase.strict_json raises phase.Failure, which this module cannot import
                empty = False
        if record["delete"] or record["confirm"] or not empty:
            self.save(member["index"], record, safety_stop=True)
            print(f"safety_stop: pods of {job} may still run", flush=True)
        else:
            self.save(member["index"], record)

    # -- readiness (S3) ------------------------------------------------------------------------------------

    def readiness(self, repeat, applied):
        """Poll the member records until both members hold disjoint assignments covering every partition."""
        job = self.job(READINESS_MODE, repeat)
        record = self.readiness_record(repeat, polls=0)
        index = self.append(record)
        phase = self.p.read("phase.json")
        prefix = f"{phase['run_id']}/{phase['phase'].upper()}/r{repeat}/consumer/"
        previous = self.p.command_deadline
        self.p.command_deadline = applied + READINESS_CAP_SECONDS
        condition = "member_count"
        try:
            while time.monotonic() - applied < READINESS_CAP_SECONDS:
                record["polls"] += 1
                condition = self.poll(prefix, job)
                if condition == "ready":
                    break
                if applied + READINESS_CAP_SECONDS - time.monotonic() <= TERMINATION_RESERVE:
                    break  # Phase.run would refuse every further command, so stop instead of spinning until the cap
                self.p.sleep(READINESS_INTERVAL_SECONDS)
        finally:
            self.p.command_deadline = previous
        record["elapsed_seconds"] = max(0, math.floor(time.monotonic() - applied))
        record["last_condition"] = condition
        record["outcome"] = "ready" if condition == "ready" else "not_observed"
        self.save(index, record)
        return record["outcome"]

    def poll(self, prefix, job):
        """One poll. Returns "ready" or the first failing condition; it never raises and never keeps blob contents."""
        account = self.p.blob_host().split(".")[0]
        listing = self.p.private / f"{job}-list.json"
        args = ["az", "storage", "blob", "list", "--account-name", account, "--container-name", CONTAINER, "--prefix", prefix,
                "--auth-mode", "login", "--num-results", str(LIST_LIMIT), "--only-show-errors", "-o", "json"]
        if self.p.run(args, 60, output=listing, maximum=4 * 1024 * 1024):
            return "list_failed"
        try:
            names = [entry["name"] for entry in self.strict_json(listing.read_bytes())]
            if not all(isinstance(name, str) and name.startswith(prefix) for name in names):
                return "list_failed"
        except Exception:  # a malformed listing is a failed poll, not a crash
            return "list_failed"
        members = {}
        for name in names:
            members.setdefault(name[len(prefix):].split("/", 1)[0], []).append(name)
        records = {}
        for member, blobs in members.items():
            matched = sorted(name for name in blobs if re.fullmatch(re.escape(prefix) + RECORD_BLOB, name))
            rows = []
            for name in matched:
                target = self.p.private / f"{job}-record.jsonl"
                content = self.p.download_blob(name, target, RECORD_BLOB_MAXIMUM, self.p.command_deadline,
                                               container=CONTAINER, minimum=0)
                if content is None:
                    return "download_failed"
                rows.append(content)
            records[member] = rows
        events = {}
        for member, contents in records.items():
            last = None
            for content in contents:
                for line in content.split(b"\n"):
                    if not line:
                        continue
                    try:
                        row = self.strict_json(line)
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
            events[member] = last
        registered = [member for member, blobs in members.items() if f"{prefix}{member}/registration.json" in blobs]
        if len(members) != 2 or len(registered) != 2:
            return "member_count"
        if any(row is None or row["event"] != "assigned" for row in events.values()):
            return "not_assigned"
        first, second = (set(row["partitions"]) for row in events.values())
        if first & second:
            return "overlap"
        if first | second != set(range(PARTITIONS)):
            return "coverage"
        return "ready"

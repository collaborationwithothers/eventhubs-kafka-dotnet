"""Run idle-observation capture Jobs one configuration at a time; the verifier owns every judgement."""
import hashlib
import json
from pathlib import Path
import re
import time

ITEMS = ("A02", "A03", "A04")
CONFIGURATIONS = (("A02", "C0"), ("A02", "C1"), ("A03", "C0"), ("A03", "C1"), ("A03", "C2"), ("A04", "C0"), ("A04", "C1"))
MODE = "idle-observation"
JUDGE_MODE = "idle-observation-judge"
JUDGE_RESERVATION = 1255  # 60 apply + 810 wait + 210 collect + 120 delete + 30 confirm + 25 reserve
JUDGE_RECEIPT_MAXIMUM = 65536
CANDIDATE_MAXIMUM = 16777216
UID = r"[0-9A-Za-z._-]+"
RECEIPT_KEYS = ("schema_version mode run_id source_commit job_name pod_name pod_uid candidates "
                "handler_exit_code cleanup_completed").split()
ENTRY_KEYS = "scenario_id candidate_name candidate_sha256 status reason".split()
CANDIDATE_KEYS = ("schema_version mode synthetic run_kind run_id scenario_id source_commit managed_version native_version "
                  "image_digests configurations item_assessment adr012_counts notes").split()
CONFIGURATION_DEADLINE = 1860
ADMISSION_SECONDS = 14400  # equals phase.py BATCH_START_CAP
LABEL = r"[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?"
MAXIMUM_JSON = 4194304


def unwrap(value):
    return value["value"] if isinstance(value, dict) and "value" in value else value


def private_json(path, value):
    path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
    encoded = json.dumps(value, separators=(",", ":"), ensure_ascii=True).encode()
    with path.open("wb") as stream:
        path.chmod(0o600)
        stream.write(encoded)
    return encoded


def strict_json(raw):
    """Decode transport JSON, refusing duplicate keys, non-finite numbers and nesting the decoder cannot hold."""
    def pairs(items):
        result = {}
        for key, item in items:
            if key in result:
                raise ValueError("duplicate JSON key")
            result[key] = item
        return result

    def constant(_value):
        raise ValueError("non-finite JSON number")

    try:
        return json.loads(raw.decode("utf-8", errors="strict"), object_pairs_hook=pairs, parse_constant=constant)
    except RecursionError as error:
        raise ValueError("JSON nesting exceeded") from error


def read_json(path):
    with path.open("rb") as stream:
        raw = stream.read(MAXIMUM_JSON + 1)
    if len(raw) > MAXIMUM_JSON:
        raise ValueError("idle_observation_json_size")
    return json.loads(raw.decode("utf-8"))


class IdleObservation:
    def __init__(self, phase):
        self.p = phase

    def selection(self, phase=None):
        phase = phase or self.p.read("phase.json")
        items = phase.get("items")
        if (phase.get("scenario") == MODE and phase.get("run_kind") in ("smoke", "batch")
                and isinstance(items, list) and items and items == [item for item in ITEMS if item in items]):
            return tuple((item, configuration) for item, configuration in CONFIGURATIONS if item in items)
        raise ValueError("idle_observation_selection")

    def values(self, item, configuration):
        if (item, configuration) not in self.selection():
            raise ValueError("idle_observation_configuration")
        return {"scenario_id": item, "configuration_id": configuration, "runtime": self.runtime(configuration, MODE)}

    def runtime(self, configuration, mode):
        phase = self.p.read("phase.json")
        ephemeral = self.p.read("ephemeral-output.json")
        persistent = self.p.read("persistent-output.json")
        accounts = unwrap(self.p.read("cluster-output.json")["workload_identity"])["service_accounts"]
        return {"run_id": phase["run_id"], "phase": configuration, "mode": mode,
                "source_commit": phase["source_commit"], "run_kind": phase["run_kind"],
                "loadgen_image": phase["images"]["loadgen"]["reference"],
                "verifier_image": phase["images"]["verifier"]["reference"],
                "service_accounts": {role: {key: accounts[role][key] for key in ("name", "client_id", "subject")}
                                     for role in ("loadgen", "verifier")},
                "eventhubs": unwrap(ephemeral["eventhubs"]), "blob": unwrap(ephemeral["blob"]),
                "eventhubs_ip": unwrap(ephemeral["private_endpoint_ips"])["eventhubs"],
                "blob_ip": unwrap(ephemeral["private_endpoint_ips"])["blob"],
                "application_insights_connection_string": unwrap(persistent["application_insights_connection_string"])}

    def judge_values(self):
        pods = {(entry.get("item"), entry.get("configuration")): entry["capture_pod"] for entry in self.p.state()["modes"]
                if entry.get("mode") == MODE and entry.get("capture_complete") is True and isinstance(entry.get("capture_pod"), dict)}
        text = ",".join(f"{item}{configuration}:{pods[item, configuration]['name']}:{pods[item, configuration]['uid']}"
                        for item, configuration in CONFIGURATIONS if (item, configuration) in pods)
        runtime = self.runtime("C1", JUDGE_MODE)
        runtime["capture_pods"] = text or "none"
        runtime["judge_items"] = ",".join(item for item in ITEMS if item in self.p.read("phase.json")["items"])
        return {"scenario_id": "A04", "configuration_id": "C1", "runtime": runtime}

    def capture_complete(self, record):
        complete = False
        try:
            directory = self.p.artifacts / record["job"]
            status = read_json(directory / "collection-status.json")
            pods = read_json(directory / "pods.json")
            phase = self.p.read("phase.json")
            items = pods["items"]
            complete = (record["outcome"] == "complete" and record["collect"] == 0
                        and status["complete"] is True and status["gaps"] == []
                        and isinstance(items, list) and len(items) == 1
                        and self.pod_complete(items[0], phase["images"]["loadgen"]["reference"]))
            if complete:
                uid = items[0]["metadata"]["uid"]
                if not isinstance(uid, str) or re.fullmatch(UID, uid) is None:
                    raise ValueError("idle_observation_pod_uid")
                pod = {"name": items[0]["metadata"]["name"], "uid": uid}
        except (OSError, ValueError, KeyError, TypeError, IndexError, UnicodeError):
            complete = False
        state = self.p.state()
        for entry in state["modes"]:
            if entry.get("job") == record.get("job"):
                entry["capture_complete"] = complete
                if complete:
                    entry["capture_pod"] = pod
        self.p.save_state(state)
        return complete

    @staticmethod
    def pod_complete(pod, image):
        containers = pod["spec"]["containers"]
        statuses = [s for s in pod["status"]["containerStatuses"] if s["name"] == "loadgen"]
        if len(statuses) != 1:
            return False
        code = statuses[0]["state"]["terminated"]["exitCode"]
        return (pod["spec"]["serviceAccountName"] == "loadgen" and containers[0]["image"] == image
                and type(code) is int and code == 0
                and isinstance(pod["metadata"]["name"], str) and re.fullmatch(LABEL, pod["metadata"]["name"]) is not None)

    def run_batch(self):
        configurations = self.selection()
        state = self.p.state()
        state["not_selected"] = [{"item": item, "configuration": configuration}
                                 for item, configuration in CONFIGURATIONS if (item, configuration) not in configurations]
        self.p.save_state(state)
        clock = self.p.start_batch_clock()
        passed = clock
        phase = self.p.read("phase.json")
        end = phase["batch_started_monotonic"] + ADMISSION_SECONDS
        for item, configuration in configurations:
            reason = None if clock and self.p.admission_allowed() and end - time.monotonic() >= CONFIGURATION_DEADLINE else "start_cap"
            record = self.p.run_mode(MODE, None, item, configuration, "loadgen", skip_reason=reason,
                                     absolute_deadline=min(end, time.monotonic() + CONFIGURATION_DEADLINE))
            if not self.capture_complete(record):
                passed = False
        judged = self.judge(end)
        state = self.p.state()
        safety_stop = state.get("safety_stop", False)
        evidence_passed = bool(passed and judged and not safety_stop)
        private_json(self.p.artifacts / f"idle-observation-{phase['run_kind']}.json", {
            "schema_version": 1, "mode": MODE, "run_kind": phase["run_kind"], "selected_items": phase["items"],
            "run_id": phase["run_id"], "source_commit": phase["source_commit"],
            "image_digests": {role: phase["images"][role]["digest"] for role in ("loadgen", "verifier")},
            "jobs": len(state["modes"]), "client_slots": 5 * len(configurations),
            "candidates": sorted(entry["item"] for entry in state.get("candidates", []) if entry.get("accepted") is True),
            "evidence_passed": evidence_passed, "cleanup_required": True})
        return int(not evidence_passed)

    def judge_gap(self, items, reason):
        for item in items:
            self.p.candidate_gap(item, "judge", reason)

    def judge(self, end):
        """Run the verifier Job that judges every capture, then accept only candidates the receipt vouches for.

        Whatever the Job's outcome, its receipt and candidates are downloaded and checked, because a judge that exits 1
        has still published its evidence. Only a complete Job that exits 0 with a transport-valid candidate is accepted.
        """
        phase = self.p.read("phase.json")
        items = [item for item in ITEMS if item in phase["items"]]
        if end - time.monotonic() < JUDGE_RESERVATION or not self.p.admission_allowed():
            self.judge_gap(items, "start_cap")
            return False
        record = self.p.run_mode(JUDGE_MODE, None, None, None, "verifier",
                                 absolute_deadline=min(end, time.monotonic() + JUDGE_RESERVATION))
        if record["outcome"] == "not_attempted":
            self.judge_gap(items, record.get("reason", "not_attempted"))
            return False
        run = phase["run_id"]
        receipt_target = self.p.candidates / "idle-judge-completion.json"
        receipt_bytes = self.p.download_blob(f"{run}/idle-observation/judge-completion.json", receipt_target,
                                             JUDGE_RECEIPT_MAXIMUM, end)
        receipt = self.receipt(receipt_bytes, phase, record["job"], items)
        exit_code = receipt[1] if receipt is not None else None
        accepted = []
        for item in items:
            name = f"{run}/idle-observation/{item}/judge/candidate.json"
            target = self.p.candidates / f"{item}-idle-candidate.json"
            candidate_bytes = self.p.download_blob(name, target, CANDIDATE_MAXIMUM, end)
            transport_valid = receipt is not None and self.candidate_accepted(candidate_bytes, receipt[0].get(item), name, phase, item)
            ok = transport_valid and record["outcome"] == "complete" and exit_code == 0
            state = self.p.state()
            state.setdefault("candidates", []).append({
                "item": item, "configuration": "judge", "accepted": ok, "transport_valid": transport_valid,
                "candidate": target.name if candidate_bytes else None,
                "judge_completion": receipt_target.name if receipt_bytes else None})
            self.p.save_state(state)
            if not ok:
                reason = ("candidate_transport" if not transport_valid
                          else "judge_" + str(record["outcome"]) if record["outcome"] != "complete"
                          else f"judge_exit_{exit_code}")
                private_json(self.p.candidates / f"{item}-judge-gap.json",
                             {"item": item, "configuration": "judge", "reason": reason})
            accepted.append(ok)
        return all(accepted)

    def receipt(self, raw, phase, job, items):
        """Return ({item: (candidate_name, candidate_sha256)}, handler_exit_code) when the receipt is closed, bound to
        this run and to the collected judge pod, and carries the exit code its own entries give, else None."""
        try:
            document = strict_json(raw)
            if (not isinstance(document, dict) or set(document) != set(RECEIPT_KEYS) or type(document["schema_version"]) is not int
                    or document["schema_version"] != 1 or document["mode"] != "idle-observation-judge"
                    or document["run_id"] != phase["run_id"] or document["source_commit"] != phase["source_commit"]
                    or document["job_name"] != job or type(document["handler_exit_code"]) is not int
                    or document["cleanup_completed"] is not True
                    or not isinstance(document["pod_name"], str) or re.fullmatch(LABEL, document["pod_name"]) is None
                    or not isinstance(document["pod_uid"], str) or not document["pod_uid"]):
                return None
            pods_path = self.p.artifacts / job / "pods.json"
            if not 0 < pods_path.stat().st_size <= MAXIMUM_JSON:
                return None
            pods = strict_json(pods_path.read_bytes())["items"]
            if (not isinstance(pods, list) or len(pods) != 1 or pods[0]["metadata"]["name"] != document["pod_name"]
                    or pods[0]["metadata"]["uid"] != document["pod_uid"]):
                return None
            entries = document["candidates"]
            if not isinstance(entries, list) or len(entries) != len(items):
                return None
            result, failing = {}, False
            for entry in entries:
                if (not isinstance(entry, dict) or set(entry) != set(ENTRY_KEYS)
                        or not all(isinstance(entry[key], str) for key in entry) or entry["scenario_id"] in result
                        or entry["status"] not in ("complete_valid", "invalid", "incomplete")):
                    return None
                result[entry["scenario_id"]] = (entry["candidate_name"], entry["candidate_sha256"])
                failing = failing or entry["status"] != "complete_valid" or entry["reason"] == "incomplete_configuration"
            if set(result) != set(items) or document["handler_exit_code"] != int(failing):
                return None
            return result, document["handler_exit_code"]
        except (OSError, ValueError, KeyError, TypeError, UnicodeError, AttributeError):
            return None

    @staticmethod
    def candidate_accepted(raw, reference, name, phase, item):
        if raw is None or reference is None:
            return False
        try:
            if reference[0] != name or reference[1] != hashlib.sha256(raw).hexdigest():
                return False
            document = strict_json(raw)
            return (isinstance(document, dict) and set(document) == set(CANDIDATE_KEYS)
                    and type(document["schema_version"]) is int and document["schema_version"] == 1
                    and document["mode"] == "idle-observation-judge" and document["synthetic"] is True
                    and document["run_id"] == phase["run_id"] and document["scenario_id"] == item
                    and document["source_commit"] == phase["source_commit"] and document["run_kind"] == phase["run_kind"]
                    and document["image_digests"] == {worker: data["digest"] for worker, data in phase["images"].items()})
        except (ValueError, KeyError, TypeError, UnicodeError, AttributeError):
            return False

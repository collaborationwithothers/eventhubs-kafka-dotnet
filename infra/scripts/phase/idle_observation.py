"""Run idle-observation capture Jobs one configuration at a time; the verifier owns every judgement."""
import json
from pathlib import Path
import re
import time

ITEMS = ("A02", "A03", "A04")
CONFIGURATIONS = (("A02", "C0"), ("A02", "C1"), ("A03", "C0"), ("A03", "C1"), ("A03", "C2"), ("A04", "C0"), ("A04", "C1"))
MODE = "idle-observation"
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
        phase = self.p.read("phase.json")
        ephemeral = self.p.read("ephemeral-output.json")
        persistent = self.p.read("persistent-output.json")
        accounts = unwrap(self.p.read("cluster-output.json")["workload_identity"])["service_accounts"]
        runtime = {"run_id": phase["run_id"], "phase": configuration, "mode": MODE,
                   "source_commit": phase["source_commit"], "run_kind": phase["run_kind"],
                   "loadgen_image": phase["images"]["loadgen"]["reference"],
                   "verifier_image": phase["images"]["verifier"]["reference"],
                   "service_accounts": {role: {key: accounts[role][key] for key in ("name", "client_id", "subject")}
                                        for role in ("loadgen", "verifier")},
                   "eventhubs": unwrap(ephemeral["eventhubs"]), "blob": unwrap(ephemeral["blob"]),
                   "eventhubs_ip": unwrap(ephemeral["private_endpoint_ips"])["eventhubs"],
                   "blob_ip": unwrap(ephemeral["private_endpoint_ips"])["blob"],
                   "application_insights_connection_string": unwrap(persistent["application_insights_connection_string"])}
        return {"scenario_id": item, "configuration_id": configuration, "runtime": runtime}

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
        except (OSError, ValueError, KeyError, TypeError, IndexError, UnicodeError):
            complete = False
        state = self.p.state()
        for entry in state["modes"]:
            if entry.get("job") == record.get("job"):
                entry["capture_complete"] = complete
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
        state = self.p.state()
        safety_stop = state.get("safety_stop", False)
        evidence_passed = bool(passed and not safety_stop)
        private_json(self.p.artifacts / f"idle-observation-{phase['run_kind']}.json", {
            "schema_version": 1, "mode": MODE, "run_kind": phase["run_kind"], "selected_items": phase["items"],
            "run_id": phase["run_id"], "source_commit": phase["source_commit"],
            "image_digests": {role: phase["images"][role]["digest"] for role in ("loadgen", "verifier")},
            "jobs": len(state["modes"]), "client_slots": 5 * len(configurations),
            "evidence_passed": evidence_passed, "cleanup_required": True})
        return int(not evidence_passed)

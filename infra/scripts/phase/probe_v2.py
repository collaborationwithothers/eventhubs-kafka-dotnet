"""Bounded smoke-only transport for the second probe batch; no scientific verdicts."""

import hashlib
import base64
import binascii
import json
import math
from pathlib import Path
import re
import time

ITEMS = ("B08", "E01", "F06")
AUTHENTICATION_CONTROLS = (("F06", "C6"), ("F06", "C9"))
CONFIGURATIONS = tuple((item, f"C{number}") for item, count in (("B08", 2), ("E01", 2), ("F06", 12)) for number in range(count))
ACCOUNTS = ("loadgen", "probe", "enrichment-consumer", "verifier")
TOPICS = ("document-events", "document-state", "document-events-dlq")
DIGEST = r"sha256:[0-9a-f]{64}"
LABEL = r"[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?"
REGISTRATION_MARKER = b"PARCHWELL_PROBE_V2_REGISTRATION "
CAPTURE_MARKER = b"PARCHWELL_PROBE_V2_CAPTURE "
IDENTITY_KEYS = "schema_version mode synthetic run_id scenario_id configuration_id stage source_commit job_name pod_name pod_uid service_account".split()
CANDIDATE_KEYS = "schema_version mode synthetic run_id scenario_id configuration_id source_commit managed_version native_version image_digests status repeats deterministic outcome_class adr012_counts diagnostics security_finding item_assessment".split()
RECEIPT_KEYS = "schema_version mode run_id scenario_id configuration_id source_commit job_name pod_name pod_uid candidate_name candidate_sha256 handler_exit_code cleanup_completed".split()


CAPTURE_PART_BYTES = 524288
CAPTURE_PART_COUNT = 4


class TransferLimit(ValueError):
    def __init__(self, limit, observed, allowed):
        super().__init__("v2_transport_limit")
        self.details = {"limit": limit, "observed": observed, "allowed": allowed}


def closed(value, keys):
    return isinstance(value, dict) and set(value) == set(keys.split() if isinstance(keys, str) else keys)


def text(value):
    return isinstance(value, str) and bool(value) and "\r" not in value and "\n" not in value


def integer(value, minimum=0):
    return type(value) is int and minimum <= value <= 9223372036854775807


def decode(raw, maximum):
    if not isinstance(raw, bytes) or not 0 < len(raw) <= maximum:
        raise ValueError("v2_input_bytes")
    value = raw.decode("utf-8", errors="strict")
    depth = 0
    quoted = escaped = False
    for character in value:
        if quoted:
            if escaped:
                escaped = False
            elif character == "\\":
                escaped = True
            elif character == '"':
                quoted = False
        elif character == '"':
            quoted = True
        elif character in "[{":
            depth += 1
            if depth > 32:
                raise ValueError("v2_input_depth")
        elif character in "]}":
            depth -= 1
            if depth < 0:
                raise ValueError("v2_input_depth")

    def pairs(items):
        result = {}
        for key, child in items:
            if key in result:
                raise ValueError("v2_duplicate_key")
            result[key] = child
        return result

    def constant(_value):
        raise ValueError("v2_nonfinite")

    return json.loads(value, object_pairs_hook=pairs, parse_constant=constant)


def read(path, maximum):
    with path.open("rb") as stream:
        raw = stream.read(maximum + 1)
    return raw, decode(raw, maximum)


def private_json(path, value):
    path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
    encoded = json.dumps(value, separators=(",", ":"), ensure_ascii=True).encode()
    with path.open("wb") as stream:
        path.chmod(0o600)
        stream.write(encoded)
    return encoded


def stages(item, configuration):
    number = int(configuration[1:])
    if (item, configuration) not in CONFIGURATIONS:
        raise ValueError("v2_configuration")
    if item == "B08":
        return (("action", "loadgen", 1980), ("read", "probe", 900), ("judge", "verifier", 120))
    if item == "E01":
        return (("seed", "enrichment-consumer", 1980), ("seedread", "probe", 900), ("action", "probe", 1980), ("read", "probe", 900), ("judge", "verifier", 120))
    if number < 6:
        return (("action", "loadgen" if number % 2 == 0 else "probe", 1980), ("read", "probe", 900), ("judge", "verifier", 120))
    seed = (("seed", "loadgen" if number == 6 else "enrichment-consumer", 1980),) if number < 9 else ()
    return seed + (("action", "probe" if number < 9 else "enrichment-consumer", 900), ("judge", "verifier", 120))


def job_name(run, item, configuration, stage):
    return f"{run}-{item.lower()}-{configuration.lower()}-{stage}"


def unwrap(value):
    return value["value"] if isinstance(value, dict) and "value" in value else value


def public_pod(pod):
    """Redact identity configuration, retaining the observed workload coordinates."""
    hidden = {"AZURE_TENANT_ID", "AZURE_SUBSCRIPTION_ID", "AZURE_CLIENT_ID", "ARM_TENANT_ID", "ARM_SUBSCRIPTION_ID", "ARM_CLIENT_ID", "AZURE_PRINCIPAL_ID", "Telemetry__ConnectionString", "APPLICATIONINSIGHTS_CONNECTION_STRING"}
    for kind in ("containers", "initContainers"):
        for container in pod.get("spec", {}).get(kind, []):
            for variable in container.get("env", []):
                name = variable.get("name", "")
                if (name in hidden or name.endswith("__WorkloadIdentityClientId") or name.endswith("__PrincipalId")) and "value" in variable:
                    variable["value"] = "[redacted]"
    annotations = pod.get("metadata", {}).get("annotations", {})
    for name in ("azure.workload.identity/client-id", "azure.workload.identity/tenant-id"):
        if name in annotations:
            annotations[name] = "[redacted]"
    return pod


class ProbeV2:
    def __init__(self, phase):
        self.p = phase
        self.limits = decode((phase.repo / "docs/operations/probe-v2-runtime-limits.json").read_bytes(), 65536)

    def selection(self, phase=None):
        phase = phase or self.p.read("phase.json")
        selected = phase.get("probe_v2_selection", "all")
        capture = phase.get("capture_authentication_metadata", False)
        if phase.get("scenario") != "probe-batch-2" or type(capture) is not bool:
            raise ValueError("v2_smoke_selection")
        if selected == "all" and phase.get("items") == list(ITEMS) and phase.get("run_kind") == "smoke" and capture is False:
            return selected, CONFIGURATIONS
        if (selected == "authentication-controls" and phase.get("items") == ["F06"]
                and phase.get("run_kind") == "smoke" and capture is True):
            return selected, AUTHENTICATION_CONTROLS
        raise ValueError("v2_smoke_selection")

    def values(self, item, configuration, stage, account):
        phase = self.p.read("phase.json")
        ephemeral = self.p.read("ephemeral-output.json")
        persistent = self.p.read("persistent-output.json")
        accounts = unwrap(self.p.read("cluster-output.json")["workload_identity"])["service_accounts"]
        if not any(s == stage and a == account for s, a, _ in stages(item, configuration)):
            raise ValueError("v2_stage")
        selection, configurations = self.selection(phase)
        runtime = {
            "run_id": phase["run_id"], "phase": configuration, "mode": "single-action-v2-judge" if stage == "judge" else "single-action-v2",
            "source_commit": phase["source_commit"], "stage": stage, "action_role": account, "image_smoke": False,
            "probe_namespace_data_roles": True,
            "loadgen_image": phase["images"]["loadgen"]["reference"], "verifier_image": phase["images"]["verifier"]["reference"],
            "service_accounts": {role: {key: accounts[role][key] for key in ("name", "client_id", "subject")} for role in ACCOUNTS},
            "eventhubs": unwrap(ephemeral["eventhubs"]), "blob": unwrap(ephemeral["blob"]),
            "eventhubs_ip": unwrap(ephemeral["private_endpoint_ips"])["eventhubs"], "blob_ip": unwrap(ephemeral["private_endpoint_ips"])["blob"],
            "application_insights_connection_string": unwrap(persistent["application_insights_connection_string"]),
            "bounds": {"probe_v2": self.limits}}
        if selection == "authentication-controls" and (item, configuration) in configurations and stage != "judge":
            runtime["capture_authentication_metadata"] = True
        return {"scenario_id": item, "configuration_id": configuration, "runtime": runtime}

    def scope(self):
        phase = self.p.read("phase.json")
        ephemeral = self.p.read("ephemeral-output.json")
        deployed = unwrap(ephemeral["probe_v2_role_scope"])
        if not closed(deployed, "namespace_id topics accounts assignments probe_namespace_data_roles"):
            raise ValueError("v2_role_shape")
        if (not text(deployed["namespace_id"]) or not closed(deployed["topics"], TOPICS)
                or not closed(deployed["accounts"], ACCOUNTS) or deployed["probe_namespace_data_roles"] is not True
                or not isinstance(deployed["assignments"], dict)):
            raise ValueError("v2_role_shape")
        cluster_accounts = unwrap(self.p.read("cluster-output.json")["workload_identity"])["service_accounts"]
        for name, identity in deployed["accounts"].items():
            if (not closed(identity, "client_id principal_id") or not all(text(v) for v in identity.values())
                    or identity["client_id"] != cluster_accounts[name]["client_id"]):
                raise ValueError("v2_role_identity")
        if any(not text(value) for value in deployed["topics"].values()):
            raise ValueError("v2_role_topics")
        for assignment in deployed["assignments"].values():
            if not closed(assignment, "service_account role_definition_name scope principal_id") or not all(text(v) for v in assignment.values()):
                raise ValueError("v2_role_assignment")
        confirmation = digest = None
        try:
            raw, candidate = read(self.p.private / "probe-v2/effective-scope.json", 32768)
            if (not closed(candidate, "schema_version namespace_id accounts") or type(candidate["schema_version"]) is not int
                    or candidate["schema_version"] != 2
                    or candidate["namespace_id"] != deployed["namespace_id"] or not closed(candidate["accounts"], "loadgen enrichment-consumer")):
                raise ValueError("v2_effective_scope")
            for name, identity in candidate["accounts"].items():
                if (not closed(identity, "principal_id no_additional_data_access") or identity["no_additional_data_access"] is not True
                        or identity["principal_id"] != deployed["accounts"][name]["principal_id"]):
                    raise ValueError("v2_effective_scope_identity")
            confirmation, digest = candidate, hashlib.sha256(raw).hexdigest()
        except (OSError, ValueError, KeyError, TypeError, UnicodeError):
            pass
        snapshot = {"schema_version": 2, "run_id": phase["run_id"], "source_commit": phase["source_commit"],
                    "namespace_name": unwrap(ephemeral["eventhubs"])["namespace_name"], "deployed": deployed,
                    "effective_scope": confirmation, "effective_scope_sha256": digest}
        encoded = json.dumps(snapshot, separators=(",", ":")).encode()
        decode(encoded, 131072)
        private_json(self.p.private / "probe-v2/role-scope.json", snapshot)
        return self.configmap(phase["run_id"] + "-v2-role-scope", {"role-scope.json": encoded.decode()})

    def configmap(self, name, data, *, binary=False):
        phase = self.p.read("phase.json")
        manifest = {"apiVersion": "v1", "kind": "ConfigMap", "metadata": {"name": name, "namespace": "parchwell",
                    "labels": {"app.kubernetes.io/instance": phase["run_id"]}}, "immutable": True, "binaryData" if binary else "data": data}
        path = self.p.private / "probe-v2" / (name + ".json")
        if path.exists():
            raise ValueError("v2_configmap_already_materialized")
        raw = private_json(path, manifest)
        if len(raw) > 1048576:
            raise TransferLimit("configmap_bytes", len(raw), 1048576)
        return self.p.run(["kubectl", "create", "-f", str(path), "--request-timeout=30s"], 60) == 0

    def pod(self, record):
        _, document = read(self.p.artifacts / record["job"] / "pods.json", 4194304)
        pods = document["items"]
        if not isinstance(pods, list) or len(pods) != 1:
            raise ValueError("v2_pod_count")
        pod = pods[0]
        metadata, spec, status = pod["metadata"], pod["spec"], pod["status"]
        name, uid = metadata["name"], metadata["uid"]
        owners = [o for o in metadata["ownerReferences"] if o.get("controller") is True]
        containers = [c for c in spec["containers"] if c.get("name") == record["role"]]
        statuses = [c for c in status["containerStatuses"] if c.get("name") == record["role"]]
        worker = "verifier" if record["stage"] == "judge" else "loadgen"
        image = self.p.read("phase.json")["images"][worker]
        if (not isinstance(name, str) or not re.fullmatch(LABEL, name) or not text(uid) or spec["serviceAccountName"] != record["role"]
                or len(owners) != 1 or owners[0].get("kind") != "Job" or owners[0].get("name") != record["job"]
                or len(containers) != 1 or containers[0].get("image") != image["reference"] or len(statuses) != 1):
            raise ValueError("v2_pod_identity")
        match = re.search(r"@(sha256:[0-9a-f]{64})$", statuses[0].get("imageID", ""))
        if match is None or match.group(1) != image["digest"]:
            raise ValueError("v2_pod_image")
        envelope = {"schema_version": 2, "job_name": record["job"], "pod_name": name, "pod_uid": uid,
                    "service_account": record["role"], "owner_kind": "Job", "owner_name": record["job"], "image_digest": match.group(1)}
        decode(json.dumps(envelope).encode(), 65536)
        return envelope

    def capture_shape(self, document):
        def strings(value):
            return isinstance(value, list) and all(isinstance(v, str) for v in value)

        def binary(value, maximum, nullable=False, limit=None):
            if value is None:
                return nullable
            if not isinstance(value, str):
                return False
            try:
                observed = len(base64.b64decode(value, validate=True))
                if limit is not None and observed > maximum:
                    raise TransferLimit(limit, observed, maximum)
                return observed <= maximum
            except TransferLimit:
                raise
            except (ValueError, binascii.Error):
                return False

        def error(value):
            return value is None or (closed(value, "code is_fatal origin reason observed_elapsed_ms sequence")
                and type(value["code"]) is int and type(value["is_fatal"]) is bool and text(value["origin"])
                and isinstance(value["reason"], str) and len(value["reason"].encode()) <= 1024
                and integer(value["observed_elapsed_ms"]) and integer(value["sequence"]))

        if (not text(document["managed_version"]) or not text(document["native_version"])
                or type(document["handler_exit_code"]) is not int or type(document["cleanup_completed"]) is not bool
                or type(document["host_cleanup_completed"]) is not bool or not isinstance(document["repeats"], list)
                or len(document["repeats"]) != 5):
            return False
        keys = "repeat admission client_id action terminal elapsed_ms terminal_unix_ms settings omitted_keys metadata_partitions requested delivery read_observation result error client_errors statistics diagnostics"
        for ordinal, slot in enumerate(document["repeats"], 1):
            if (not closed(slot, keys) or type(slot["repeat"]) is not int or slot["repeat"] != ordinal or not text(slot["admission"])
                    or slot["client_id"] is not None and not text(slot["client_id"]) or not text(slot["action"]) or type(slot["terminal"]) is not bool
                    or not integer(slot["elapsed_ms"]) or slot["terminal_unix_ms"] is not None and not integer(slot["terminal_unix_ms"])
                    or not isinstance(slot["settings"], dict) or len(slot["settings"]) > 128 or any(not text(k) or not isinstance(v, str) for k, v in slot["settings"].items())
                    or not strings(slot["omitted_keys"]) or not strings(slot["diagnostics"]) or len(slot["diagnostics"]) > 100
                    or slot["metadata_partitions"] is not None and (not isinstance(slot["metadata_partitions"], list) or any(not integer(v) for v in slot["metadata_partitions"]))
                    or slot["result"] is not None and not text(slot["result"]) or not error(slot["error"])
                    or not isinstance(slot["client_errors"], list) or len(slot["client_errors"]) > 8 or any(v is None or not error(v) for v in slot["client_errors"])
                    or not isinstance(slot["statistics"], list) or len(slot["statistics"]) > 1):
                return False
            for report in slot["statistics"]:
                if (not closed(report, "sequence observed_elapsed_ms raw_base64") or not integer(report["sequence"])
                        or not integer(report["observed_elapsed_ms"]) or not binary(report["raw_base64"], self.limits["capture"]["MaxStatisticsReportBytes"], limit="statistics_report_bytes")):
                    return False
            requested = slot["requested"]
            if requested is not None and (not closed(requested, "topic partition_mode partition offset key_base64 value_base64")
                    or not text(requested["topic"]) or not text(requested["partition_mode"])
                    or any(requested[k] is not None and not integer(requested[k]) for k in ("partition", "offset"))
                    or not binary(requested["key_base64"], 4096) or not binary(requested["value_base64"], 4096, True)):
                return False
            delivery = slot["delivery"]
            if delivery is not None and (not closed(delivery, "topic partition offset persistence_status error observed_elapsed_ms sequence")
                    or not text(delivery["topic"]) or any(not integer(delivery[k]) for k in ("partition", "offset", "observed_elapsed_ms", "sequence"))
                    or not text(delivery["persistence_status"]) or not error(delivery["error"])):
                return False
            observed = slot["read_observation"]
            if not closed(observed, "records_seen eof_count record") or not integer(observed["records_seen"]) or not integer(observed["eof_count"]):
                return False
            record = observed["record"]
            if record is not None and (not closed(record, "topic partition offset key_base64 value_base64 observed_elapsed_ms sequence")
                    or not text(record["topic"]) or any(not integer(record[k]) for k in ("partition", "offset", "observed_elapsed_ms", "sequence"))
                    or not binary(record["key_base64"], 4096) or not binary(record["value_base64"], 4096, True)):
                return False
        return True

    def transfer(self, record):
        phase = self.p.read("phase.json")
        try:
            pod = self.pod(record)
            path = self.p.artifacts / record["job"] / f"{pod['pod_name']}.containers.{record['role']}.log"
            maximum = self.limits["execution"]["max_container_log_bytes"]
            with path.open("rb") as stream:
                log = stream.read(maximum + 1)
            if not 0 < len(log) <= maximum:
                raise TransferLimit("container_log_bytes", len(log), maximum)
            lines = log.splitlines(keepends=True)
            found = {}
            indexes = {}
            capture_document = None
            for key, marker, bound in (("registration.json", REGISTRATION_MARKER, self.limits["capture"]["MaxRegistrationBytes"]), ("capture.json", CAPTURE_MARKER, self.limits["capture"]["MaxCaptureBytes"])):
                matches = [(index, line[len(marker):].removesuffix(b"\n").removesuffix(b"\r")) for index, line in enumerate(lines) if line.startswith(marker)]
                if len(matches) != 1:
                    raise ValueError("v2_receipt_count")
                index, raw = matches[0]
                if len(raw) > bound:
                    raise TransferLimit(key + "_bytes", len(raw), bound)
                document = decode(raw, bound)
                expected_keys = IDENTITY_KEYS if key == "registration.json" else IDENTITY_KEYS + ["managed_version", "native_version", "repeats", "handler_exit_code", "cleanup_completed", "host_cleanup_completed"]
                if not closed(document, expected_keys) or type(document["schema_version"]) is not int or document["schema_version"] != 2 or document["mode"] != "single-action-v2" or document["synthetic"] is not True:
                    raise ValueError("v2_receipt_shape")
                expected = {"run_id": phase["run_id"], "scenario_id": record["item"], "configuration_id": record["configuration"],
                            "stage": record["stage"], "source_commit": phase["source_commit"], "job_name": record["job"],
                            "pod_name": pod["pod_name"], "pod_uid": pod["pod_uid"], "service_account": record["role"]}
                if any(document[k] != v for k, v in expected.items()):
                    raise ValueError("v2_receipt_identity")
                if key == "capture.json":
                    if not self.capture_shape(document):
                        raise ValueError("v2_capture_shape")
                    capture_document = document
                found[key] = raw
                indexes[key] = index
            if indexes["registration.json"] >= indexes["capture.json"]:
                raise ValueError("v2_receipt_order")
            found["pod.json"] = json.dumps(pod, separators=(",", ":")).encode("utf-8")
            decode(found["pod.json"], self.limits["capture"]["MaxPodBytes"])
            capture = found.pop("capture.json")
            if len(capture) > CAPTURE_PART_BYTES * CAPTURE_PART_COUNT:
                raise TransferLimit("capture_parts_bytes", len(capture), CAPTURE_PART_BYTES * CAPTURE_PART_COUNT)
            transferred = True
            for part in range(CAPTURE_PART_COUNT):
                payload = found.copy() if part == 0 else {}
                payload[f"capture.json.part{part}"] = capture[part * CAPTURE_PART_BYTES:(part + 1) * CAPTURE_PART_BYTES]
                name = record["job"] + "-evidence" + (f"-{part}" if part else "")
                if not self.configmap(name, {key: base64.b64encode(value).decode("ascii") for key, value in payload.items()}, binary=True):
                    transferred = False
                    break
            if transferred:
                record["admitted_client_slots"] = sum(slot["admission"] == "required" for slot in capture_document["repeats"])
                record["capture_complete"] = (capture_document["handler_exit_code"] == 0
                    and capture_document["cleanup_completed"] is True and capture_document["host_cleanup_completed"] is True)
                state = self.p.state()
                for saved in state["modes"]:
                    if saved["job"] == record["job"]:
                        saved.update(admitted_client_slots=record["admitted_client_slots"], capture_complete=record["capture_complete"])
                self.p.save_state(state)
            return transferred
        except (OSError, ValueError, KeyError, TypeError, UnicodeError, AttributeError) as exception:
            gap = {"job": record["job"], "reason": "v2_evidence_transport"}
            if isinstance(exception, TransferLimit):
                gap.update(exception.details)
            private_json(self.p.artifacts / record["job"] / "v2-transfer-gap.json", gap)
            return False

    def wait(self, job, seconds):
        end = min(time.monotonic() + seconds + 60, self.p.command_deadline)
        path = self.p.private / "probe-v2" / (job + "-wait.json")
        for _ in range(math.ceil((seconds + 60) / 5)):
            if time.monotonic() >= end:
                break
            code = self.p.run(["kubectl", "get", "job", job, "-n", "parchwell", "--ignore-not-found", "-o", "json", "--request-timeout=30s"], 30, output=path, maximum=65536)
            if code:
                return 124 if code == 124 else 2
            try:
                if path.read_bytes().strip():
                    _, value = read(path, 65536)
                    conditions = value.get("status", {}).get("conditions", [])
                    if not isinstance(conditions, list) or any(not isinstance(c, dict) for c in conditions):
                        return 2
                    if any(c.get("type") == "Failed" and c.get("status") == "True" for c in conditions):
                        return 1
                    if any(c.get("type") == "Complete" and c.get("status") == "True" for c in conditions):
                        return 0
            except (OSError, ValueError, TypeError):
                return 2
            remaining = end - time.monotonic()
            if remaining <= 0:
                break
            self.p.sleep(min(5, remaining))
        return 124

    def collect(self, record):
        directory = self.p.artifacts / record["job"]
        directory.mkdir(parents=True, exist_ok=True)
        source = self.p.private / "probe-v2" / (record["job"] + "-pods.json")
        gaps = []
        code = self.p.run(["kubectl", "get", "pods", "-n", "parchwell", "-l", "job-name=" + record["job"], "-o", "json", "--request-timeout=30s"], 30, output=source, maximum=4194304)
        terminated = False
        try:
            if code:
                raise ValueError("pod_read_failed")
            _, data = read(source, 4194304)
            pods = data["items"]
            if not isinstance(pods, list) or len(pods) != 1:
                raise ValueError("pod_count")
            pod = pods[0]
            name = pod["metadata"]["name"]
            if not isinstance(name, str) or not re.fullmatch(LABEL, name):
                raise ValueError("pod_name")
            statuses = [c for c in pod["status"]["containerStatuses"] if c.get("name") == record["role"]]
            terminated = len(statuses) == 1 and isinstance(statuses[0].get("state", {}).get("terminated"), dict)
            public_pod(pod)
            private_json(directory / "pods.json", data)
            path = directory / f"{name}.containers.{record['role']}.log"
            maximum = self.limits["execution"]["max_container_log_bytes"]
            code = self.p.run(["kubectl", "logs", "-n", "parchwell", name, "-c", record["role"], "--timestamps=false", f"--limit-bytes={maximum + 1}", "--request-timeout=30s"], 30, output=path, maximum=maximum)
            if code:
                gap = {"kind": "truncated_log" if code == 125 else "log_read_failed", "status": code}
                if code == 125:
                    gap.update(limit="container_log_bytes", observed=maximum + 1, observed_at_least=True, allowed=maximum)
                gaps.append(gap)
        except (OSError, ValueError, KeyError, TypeError, UnicodeError):
            gaps.append({"kind": "pod_collection"})
        private_json(directory / "collection-status.json", {"complete": not gaps, "gaps": gaps})
        return int(bool(gaps)), terminated

    def stop_uncertain(self, job):
        code = self.p.run(["kubectl", "delete", "job", job, "-n", "parchwell", "--ignore-not-found", "--cascade=foreground", "--wait=false", "--request-timeout=30s"], 30)
        if code:
            return False
        path = self.p.private / "probe-v2" / (job + "-confirm.json")
        end = min(time.monotonic() + 90, self.p.command_deadline)
        for _ in range(18):
            if time.monotonic() >= end:
                break
            code = self.p.run(["kubectl", "get", "pods", "-n", "parchwell", "-l", "job-name=" + job, "-o", "json", "--request-timeout=30s"], 30, output=path, maximum=4194304)
            if code:
                return False
            try:
                if read(path, 4194304)[1].get("items") == []:
                    return True
            except (OSError, ValueError, TypeError):
                return False
            remaining = end - time.monotonic()
            if remaining <= 0:
                break
            self.p.sleep(min(5, remaining))
        return False

    def run_stage(self, item, configuration, stage, account, seconds, deadline, skip=None):
        phase = self.p.read("phase.json")
        name = job_name(phase["run_id"], item, configuration, stage)
        record = {"job": name, "repeat": None, "mode": "single-action-v2-judge" if stage == "judge" else "single-action-v2",
                  "item": item, "configuration": configuration, "stage": stage, "role": account, "image_smoke": False,
                  "deadline": seconds, "outcome": "not_attempted", **{k: None for k in ("render", "apply", "wait", "collect", "delete", "confirm")}}
        if stage != "judge":
            record.update(admitted_client_slots=None, capture_complete=False)
        state = self.p.state()
        state["modes"].append(record)
        self.p.save_state(state)

        def save():
            current = self.p.state()
            current["modes"][-1] = record
            self.p.save_state(current)

        previous = self.p.command_deadline
        self.p.command_deadline = deadline
        try:
            if state.get("safety_stop") or skip or deadline - time.monotonic() <= 5:
                record["reason"] = "safety_stop" if state.get("safety_stop") else skip or "orchestration_deadline"
                return record
            values_path = self.p.private / ("values-" + name + ".json")
            rendered = self.p.private / ("rendered-" + name + ".yaml")
            private_json(values_path, self.values(item, configuration, stage, account))
            code = self.p.run(["helm", "template", "parchwell", "charts/parchwell", "--values", str(values_path)], 60, output=rendered, maximum=4194304)
            record["render"] = code
            if code:
                record["outcome"] = "render_failed"
                return record
            rendered_text = rendered.read_text()
            found = re.findall(r"^  activeDeadlineSeconds: ([1-9][0-9]*)$", rendered_text, re.MULTILINE)
            if (rendered_text.splitlines().count("kind: Job") != 1 or rendered_text.splitlines().count(f'  name: "{name}"') != 1
                    or found != [str(seconds)]):
                record["outcome"] = "render_failed"
                return record
            record["apply"] = self.p.run(["kubectl", "apply", "-f", str(rendered), "--request-timeout=30s"], 60)
            save()
            if record["apply"]:
                record["outcome"] = "apply_failed"
                terminated = False
            else:
                record["wait"] = self.wait(name, seconds)
                record["outcome"] = "complete" if record["wait"] == 0 else "failed" if record["wait"] == 1 else "unknown"
                save()
                record["collect"], terminated = self.collect(record)
                if record["collect"]:
                    record["collect_gap"] = True
            if not terminated:
                confirmed = self.stop_uncertain(name)
                record["confirm"] = 0 if confirmed else 1
                if not confirmed:
                    current = self.p.state()
                    current["safety_stop"] = True
                    self.p.save_state(current)
            return record
        except (OSError, ValueError, KeyError, TypeError, UnicodeError):
            record["outcome"] = "unknown"
            if record["apply"] is not None:
                current = self.p.state()
                current["safety_stop"] = True
                self.p.save_state(current)
            return record
        finally:
            save()
            self.p.command_deadline = previous

    @staticmethod
    def candidate_shape(candidate):
        def strings(value):
            return isinstance(value, list) and all(isinstance(v, str) for v in value)

        def outcome(value):
            return value is None or (closed(value, "result error_code is_fatal origin") and text(value["result"])
                and (value["error_code"] is None or type(value["error_code"]) is int)
                and (value["is_fatal"] is None or type(value["is_fatal"]) is bool)
                and (value["origin"] is None or text(value["origin"])))

        def statistics(value):
            return value is None or (closed(value, "sequence native_time native_ts raw_sha256")
                and all(integer(value[k]) for k in ("sequence", "native_time", "native_ts"))
                and isinstance(value["raw_sha256"], str) and re.fullmatch(r"[a-f0-9]{64}", value["raw_sha256"]))

        def reference(value):
            return (closed(value, "run_id scenario_id configuration_id stage source_commit job_name pod_uid raw_sha256") and all(text(v) for v in value.values())
                    and re.fullmatch(r"[0-9a-f]{64}", value["raw_sha256"]) is not None)

        def binary(value, nullable=False):
            if value is None:
                return nullable
            if not isinstance(value, str):
                return False
            try:
                return len(base64.b64decode(value, validate=True)) <= 4096
            except (ValueError, binascii.Error):
                return False

        def error(value):
            return value is None or (closed(value, "code is_fatal origin reason observed_elapsed_ms sequence")
                and type(value["code"]) is int and type(value["is_fatal"]) is bool and text(value["origin"])
                and isinstance(value["reason"], str) and len(value["reason"].encode()) <= 1024
                and integer(value["observed_elapsed_ms"]) and integer(value["sequence"]))

        def delivery(value):
            return value is None or (closed(value, "topic partition offset persistence_status error observed_elapsed_ms sequence")
                and text(value["topic"]) and text(value["persistence_status"]) and error(value["error"])
                and all(integer(value[k]) for k in ("partition", "offset", "observed_elapsed_ms", "sequence")))

        def observation(value):
            if value is None:
                return True
            if not closed(value, "records_seen eof_count record") or not integer(value["records_seen"]) or not integer(value["eof_count"]):
                return False
            record = value["record"]
            return record is None or (closed(record, "topic partition offset key_base64 value_base64 observed_elapsed_ms sequence")
                and text(record["topic"]) and binary(record["key_base64"]) and binary(record["value_base64"], True)
                and all(integer(record[k]) for k in ("partition", "offset", "observed_elapsed_ms", "sequence")))

        def semantic(value):
            return (closed(value, "delivered_partition consumed_partition key_base64 value_kind value_match")
                and all(value[k] is None or integer(value[k]) for k in ("delivered_partition", "consumed_partition"))
                and binary(value["key_base64"], True) and value["value_kind"] in (None, "null", "empty", "bytes")
                and (value["value_match"] is None or type(value["value_match"]) is bool))

        if (not closed(candidate, CANDIDATE_KEYS) or candidate["status"] not in ("complete_valid", "invalid", "incomplete")
                or candidate["deterministic"] is not None and type(candidate["deterministic"]) is not bool
                or type(candidate["security_finding"]) is not bool or not strings(candidate["diagnostics"])
                or not outcome(candidate["outcome_class"]) or candidate["item_assessment"] is not None
                or any(v is not None and not text(v) for v in (candidate["managed_version"], candidate["native_version"]))):
            return False
        repeats = candidate["repeats"]
        if not isinstance(repeats, list) or len(repeats) != 5:
            return False
        for ordinal, repeat in enumerate(repeats, 1):
            if (not closed(repeat, "repeat status reasons outcome_class broker_count statistics_reference semantic_outcome delivery read_observation seed_reference control_references clients")
                    or type(repeat["repeat"]) is not int or repeat["repeat"] != ordinal or repeat["status"] not in ("complete_valid", "invalid", "incomplete")
                    or not strings(repeat["reasons"]) or not outcome(repeat["outcome_class"]) or not statistics(repeat["statistics_reference"])
                    or repeat["broker_count"] is not None and not integer(repeat["broker_count"])
                    or not semantic(repeat["semantic_outcome"]) or not delivery(repeat["delivery"]) or not observation(repeat["read_observation"])
                    or repeat["seed_reference"] is not None and not reference(repeat["seed_reference"])
                    or not isinstance(repeat["control_references"], list) or any(not reference(v) for v in repeat["control_references"])
                    or not isinstance(repeat["clients"], list)):
                return False
            for client in repeat["clients"]:
                if (not closed(client, "client_id stage broker_count statistics_reference") or not text(client["client_id"]) or not text(client["stage"])
                        or client["broker_count"] is not None and not integer(client["broker_count"]) or not statistics(client["statistics_reference"])):
                    return False
        return True

    def candidate(self, record, deadline):
        phase = self.p.read("phase.json")
        item, configuration = record["item"], record["configuration"]
        prefix = f"{phase['run_id']}/single-action-v2/{item}/{configuration}/"
        target = self.p.candidates / f"{item}-{configuration}.json"
        receipt_target = self.p.candidates / f"{item}-{configuration}-judge-completion.json"
        candidate_bytes = self.p.download_blob(prefix + "candidate.json", target, 16777216, deadline)
        receipt_bytes = self.p.download_blob(prefix + "judge-completion.json", receipt_target, 65536, deadline)
        accepted = smoke = False
        try:
            candidate, receipt = decode(candidate_bytes, 16777216), decode(receipt_bytes, 65536)
            pod = self.pod(record)
            if not self.candidate_shape(candidate) or not closed(receipt, RECEIPT_KEYS):
                raise ValueError("v2_candidate_shape")
            for document in (candidate, receipt):
                if (type(document["schema_version"]) is not int or document["schema_version"] != 2
                        or any(document[k] != v for k, v in (("run_id", phase["run_id"]), ("scenario_id", item), ("configuration_id", configuration), ("source_commit", phase["source_commit"])))):
                    raise ValueError("v2_candidate_identity")
            images = {worker: phase["images"][worker]["digest"] for worker in ("loadgen", "verifier")}
            if (candidate["image_digests"] != images or any(not re.fullmatch(DIGEST, v) for v in images.values())
                    or candidate["mode"] != "single-action-v2" or candidate["synthetic"] is not True or candidate["adr012_counts"] != "not_applicable"
                    or receipt["mode"] != "single-action-v2-judge" or receipt["candidate_name"] != prefix + "candidate.json"
                    or receipt["candidate_sha256"] != hashlib.sha256(candidate_bytes).hexdigest()
                    or receipt["job_name"] != record["job"] or receipt["pod_name"] != pod["pod_name"] or receipt["pod_uid"] != pod["pod_uid"]
                    or type(receipt["handler_exit_code"]) is not int or receipt["handler_exit_code"] != 0 or receipt["cleanup_completed"] is not True):
                raise ValueError("v2_candidate_receipt")
            accepted = True
            smoke = (candidate["status"] == "complete_valid" and candidate["deterministic"] is True
                     and candidate["security_finding"] is False and "smoke_only" in candidate["diagnostics"]
                     and "smoke_controls_failed" not in candidate["diagnostics"])
        except (OSError, ValueError, KeyError, TypeError, UnicodeError, AttributeError):
            pass
        state = self.p.state()
        state.setdefault("candidates", []).append({"item": item, "configuration": configuration, "accepted": accepted,
            "smoke_valid": smoke, "candidate": target.name if candidate_bytes else None, "judge_completion": receipt_target.name if receipt_bytes else None})
        self.p.save_state(state)
        if not accepted:
            private_json(self.p.candidates / f"{item}-{configuration}-gap.json", {"item": item, "configuration": configuration, "reason": "v2_candidate_transport"})
        return accepted and smoke

    @staticmethod
    def admitted_slots(state, run, configurations=CONFIGURATIONS):
        """Count declared admissions only when every actor capture was transported and completed."""
        expected = {job_name(run, item, configuration, stage) for item, configuration in configurations
                    for stage, _, _ in stages(item, configuration) if stage != "judge"}
        actors = [record for record in state["modes"] if record.get("stage") != "judge"]
        if (len(actors) != len(expected) or {record["job"] for record in actors} != expected
                or any(record.get("capture_complete") is not True or type(record.get("admitted_client_slots")) is not int
                       or not 0 <= record["admitted_client_slots"] <= 5 for record in actors)):
            return None
        return sum(record["admitted_client_slots"] for record in actors)

    def run_batch(self):
        phase = self.p.read("phase.json")
        selection, configurations = self.selection(phase)
        clock = self.p.start_batch_clock()
        phase = self.p.read("phase.json")
        end = phase["batch_started_monotonic"] + 14400
        previous = self.p.command_deadline
        self.p.command_deadline = end
        scope_valid = False
        try:
            scope_valid = self.scope()
        except (OSError, ValueError, KeyError, TypeError, UnicodeError):
            private_json(self.p.artifacts / "v2-scope-gap.json", {"reason": "v2_scope_transport"})
        finally:
            self.p.command_deadline = previous
        passed = clock and scope_valid
        for item, configuration in configurations:
            selected_stages = stages(item, configuration)
            reservation = sum(seconds + 120 for _, _, seconds in selected_stages)
            reason = None if clock and self.p.admission_allowed() and end - time.monotonic() >= reservation else "start_cap"
            deadline = min(end, time.monotonic() + reservation)
            for stage, account, seconds in selected_stages:
                record = self.run_stage(item, configuration, stage, account, seconds, deadline, reason)
                if record["outcome"] != "complete" or record["collect"] != 0:
                    passed = False
                if stage == "judge":
                    if record["outcome"] == "not_attempted":
                        self.p.candidate_gap(item, configuration, record.get("reason", "not_attempted"))
                        passed = False
                    elif not self.candidate(record, deadline):
                        passed = False
                elif record["outcome"] != "not_attempted":
                    self.p.command_deadline = deadline
                    try:
                        if not self.transfer(record):
                            passed = False
                    finally:
                        self.p.command_deadline = previous
        state = self.p.state()
        if selection == "all":
            # Preserve the established full-matrix report and acceptance behavior.
            state["not_selected"] = []
            self.p.save_state(state)
            private_json(self.p.artifacts / "probe-v2-smoke.json", {"schema_version": 2, "mode": "smoke_only", "run_id": phase["run_id"],
                "source_commit": phase["source_commit"], "image_digests": {w: phase["images"][w]["digest"] for w in ("loadgen", "verifier")},
                "jobs": len(state["modes"]), "action_slots": len(CONFIGURATIONS) * 5, "maximum_clients": sum(stage != "judge" for i, c in CONFIGURATIONS for stage, _, _ in stages(i, c)) * 5,
                "admitted_client_slots": self.admitted_slots(state, phase["run_id"]),
                "evidence_passed": passed and len(state["modes"]) == 49 and not state.get("safety_stop", False), "cleanup_required": True})
            return int(not passed or len(state["modes"]) != 49 or state.get("safety_stop", False))
        selected_jobs = {job_name(phase["run_id"], item, configuration, stage)
                         for item, configuration in configurations for stage, _, _ in stages(item, configuration)}
        actual_jobs = [record.get("job") for record in state["modes"]]
        expected_candidates = {(item, configuration) for item, configuration in configurations}
        candidates = state.get("candidates", [])
        candidate_keys = [(candidate.get("item"), candidate.get("configuration")) for candidate in candidates]
        complete = (len(actual_jobs) == len(selected_jobs) and set(actual_jobs) == selected_jobs
                    and len(candidate_keys) == len(expected_candidates) and set(candidate_keys) == expected_candidates
                    and all(candidate.get("accepted") is True and candidate.get("smoke_valid") is True for candidate in candidates))
        if not complete or state.get("safety_stop", False):
            passed = False
        state["not_selected"] = [{"item": item, "configuration": configuration}
                                  for item, configuration in CONFIGURATIONS if (item, configuration) not in configurations]
        self.p.save_state(state)
        transport_passed = bool(passed and complete)
        private_json(self.p.artifacts / "authentication-metadata-collection.json", {
            "schema_version": 1, "mode": "authentication_metadata_collection", "selection": "authentication-controls",
            "run_id": phase["run_id"], "source_commit": phase["source_commit"],
            "image_digests": {w: phase["images"][w]["digest"] for w in ("loadgen", "verifier")},
            "configurations": [{"item": item, "configuration": configuration} for item, configuration in AUTHENTICATION_CONTROLS],
            "jobs": len(state["modes"]), "action_slots": 10, "maximum_clients": 15,
            "admitted_client_slots": self.admitted_slots(state, phase["run_id"], AUTHENTICATION_CONTROLS),
            "transport_passed": transport_passed, "metadata_assessed": False, "cleanup_required": True})
        return int(not transport_passed)

#!/usr/bin/env python3
"""Run scenario phase commands while keeping private inputs separate from evidence."""

import ctypes
import math
import json
import os
from pathlib import Path
import re
import socket
import subprocess
import sys
import tempfile
import importlib.util
import time
from urllib.parse import urlsplit

MIB = 1024 * 1024
SCENARIOS = {"A01": {"probe": "C0", "C0": "C0", "C3": "C3"},
             "B04": {"C0": "C0", "C1": "C1"}}
MODES = ("start-line", "loadgen", "verify")
SINGLE_ACTION_CONFIGURATIONS = (
    ('B01', 'C0', 'loadgen'), ('B01', 'C1', 'loadgen'),
    ('B03', 'C1', 'loadgen'), ('B03', 'C2', 'loadgen'),
    ('B03', 'C3', 'loadgen'), ('B03', 'C4', 'loadgen'),
    ('B05', 'C1', 'loadgen'),
    ('C08', 'C0', 'probe'), ('C08', 'C1', 'probe'),
    ('C12', 'C1', 'probe'), ('C12', 'C2', 'probe'),
    ('F04', 'C0', 'loadgen'), ('F04', 'C1', 'loadgen'),
    ('F04', 'C2', 'probe'), ('F04', 'C3', 'loadgen'),
    ('F04', 'C4', 'probe'),
)
SINGLE_ACTION_ITEMS = ('B01', 'B03', 'B05', 'C08', 'C12', 'F04')
SINGLE_ACTION_MODES = ("single-action", "single-action-judge", "single-action-seed")
SMOKE_MARKER = {"native_loaded": True, "scenario_files": 3, "uid_expected": 1654}
SEED_SMOKE_MARKER = {"mode": "single-action-seed-smoke", "native_loaded": True, "uid_expected": 1654}
SEED_RECEIPT_MARKER = "PARCHWELL_F04_SEED_RECEIPT "
BATCH_START_CAP = 14400
SEED_DEADLINE = 1050
PAIR_DEADLINE = 3360
TERMINATION_RESERVE = 5
SINGLE_ACTION_INPUT_DEPTH = 32
SLOTS = (1, 2, 3, 4, 5)
CANDIDATE_MAXIMUM = 16_777_216  # PhaseCandidateCodec.MaximumBytes
LABEL = r"[a-z0-9](?:[a-z0-9-]*[a-z0-9])?"
RESOURCE = r"/subscriptions/[0-9a-fA-F-]{36}/resourceGroups/[^/\r\n]+/providers/Microsoft.ContainerService/managedClusters/[^/\r\n]+"
DIGEST = r"sha256:[0-9a-f]{64}"
BAD_OUTCOMES = {"render_failed", "apply_failed", "unknown", "collect_error", "not_attempted"}


class Failure(Exception):
    def __init__(self, message, code=2):
        super().__init__(message)
        self.code = code


def scenario():
    selected = os.environ.get("PARCHWELL_SCENARIO", "A01")
    if selected not in (*SCENARIOS, "probe-batch", "probe-batch-2"):
        raise Failure("unsupported scenario")
    return selected


def capture_authentication_metadata():
    value = os.environ.get('PARCHWELL_CAPTURE_AUTHENTICATION_METADATA')
    if value is None or value == 'false':
        return False
    if value == 'true':
        return True
    raise Failure('unsupported probe batch selection')


def batch_items():
    value = os.environ.get('PARCHWELL_BATCH_ITEMS', 'all')
    selected_scenario = os.environ.get('PARCHWELL_SCENARIO')
    capture = capture_authentication_metadata()
    if selected_scenario == 'probe-batch-2':
        if os.environ.get('PARCHWELL_RUN_KIND') != 'smoke':
            raise Failure('unsupported probe batch selection')
        if value == 'all' and not capture:
            return ['B08', 'E01', 'F06']
        if value == 'authentication-controls' and capture:
            return ['F06']
        raise Failure('unsupported probe batch selection')
    if capture:
        raise Failure('unsupported probe batch selection')
    if value == 'all':
        return list(SINGLE_ACTION_ITEMS)
    selected = value.split(',')
    canonical = [item for item in SINGLE_ACTION_ITEMS if item in selected]
    if selected != canonical or not canonical:
        raise Failure('unsupported probe batch selection')
    return selected


def require(name):
    value = os.environ.get(name)
    if not value or "\r" in value or "\n" in value:
        raise Failure("missing or invalid " + name)
    return value


def strict_json(value):
    def pairs(items):
        result = {}
        for key, item in items:
            if key in result:
                raise Failure("duplicate JSON key")
            result[key] = item
        return result

    def constant(_value):
        raise Failure("non-finite JSON number")

    if isinstance(value, bytes):
        value = value.decode("utf-8", errors="strict")
    return json.loads(value, object_pairs_hook=pairs, parse_constant=constant)


def single_action_json(value):
    """Bound new transport nesting before the recursive JSON decoder allocates objects."""
    if isinstance(value, bytes):
        value = value.decode("utf-8", errors="strict")
    if not isinstance(value, str):
        raise Failure("single-action JSON text required")
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
            if depth > SINGLE_ACTION_INPUT_DEPTH:
                raise Failure("single-action JSON depth exceeded")
        elif character in "]}":
            depth -= 1
            if depth < 0:
                raise Failure("single-action JSON nesting invalid")
    try:
        return strict_json(value)
    except RecursionError as error:
        # A parser recursion failure is malformed transport, never a batch-wide abort.
        raise Failure("single-action JSON recursion exceeded") from error


def unwrap(value):
    return value["value"] if isinstance(value, dict) and "value" in value else value


def job_name(run, phase, mode, repeat=None, item=None, configuration=None, action_role=None):
    if mode == "single-action-seed":
        return f"{run}-f04-seed"
    if mode in SINGLE_ACTION_MODES:
        suffix = "single-judge" if mode == "single-action-judge" else "single"
        return f"{run}-{item.lower()}-{configuration.lower()}-{suffix}"
    if mode == "judge":
        return f"{run}-{phase.lower()}-judge"
    return f"{run}-{phase.lower()}-r{repeat}-{mode}"


def candidate_blob(run, phase):
    return f"{run}/{phase}/candidate.json"


def write_private(path, value):
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
    os.fchmod(descriptor, 0o600)
    with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
        stream.write(value)


def write_json(path, value):
    write_private(path, json.dumps(value) + "\n")


def append_lines(path, lines):
    if any("\n" in line or "\r" in line for line in lines):
        raise Failure("invalid environment line")
    with open(path, "a", encoding="utf-8") as stream:
        stream.write("".join(line + "\n" for line in lines))


class Phase:
    def __init__(self):
        self.command_deadline = None
        self.repo = Path(os.environ.get("PARCHWELL_REPOSITORY", Path(__file__).resolve().parents[3])).resolve()

    @property
    def private(self):
        return Path(require("PARCHWELL_PRIVATE_DIRECTORY"))

    @property
    def artifacts(self):
        return Path(require("PARCHWELL_ARTIFACT_DIRECTORY"))

    @property
    def candidates(self):
        return Path(require("PARCHWELL_CANDIDATE_DIRECTORY"))

    def probe_v2(self):
        source = Path(__file__).with_name("probe_v2.py")
        spec = importlib.util.spec_from_file_location("parchwell_probe_v2", source)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module.ProbeV2(self)

    def read(self, name):
        return strict_json((self.private / name).read_bytes())

    def state(self):
        return strict_json((self.artifacts / "phase-status.json").read_bytes())

    def save_state(self, state):
        write_json(self.artifacts / "phase-status.json", state)

    def run(self, args, seconds, output=None, maximum=4 * MIB):
        if self.command_deadline is not None:
            remaining = self.command_deadline - time.monotonic()
            if remaining <= TERMINATION_RESERVE:
                return 124
            seconds = min(seconds, math.floor(remaining - TERMINATION_RESERVE))
            if seconds <= 0:
                return 124
        command = [sys.executable, str(self.repo / "infra/scripts/lifecycle/command.py"), str(seconds)]
        if output is not None:
            command += ["--stdout-file", str(output), "--max-bytes", str(maximum)]
        return subprocess.call(command + ["--", *map(str, args)], cwd=self.repo)

    def sleep(self, seconds):
        time.sleep(seconds)

    def validate_resource(self):
        if not re.fullmatch(RESOURCE, require("PARCHWELL_CLUSTER_RESOURCE_ID")):
            raise Failure("invalid AKS resource ID")

    def validate_scenario(self):
        if scenario() not in SCENARIOS:
            raise Failure("unsupported scenario")
        if capture_authentication_metadata():
            raise Failure('unsupported probe batch selection')
        return 0

    def validate_batch(self):
        if scenario() == "probe-batch-2":
            batch_items()
            return 0
        if scenario() != "probe-batch" or os.environ.get("PARCHWELL_RUN_KIND") not in ("smoke", "batch"):
            raise Failure("unsupported probe batch selection")
        batch_items()
        return 0

    def allow_images(self):
        allowed = all(re.fullmatch(DIGEST, os.environ.get(name, "")) is not None
                      for name in ("LOADGEN_DIGEST", "VERIFIER_DIGEST"))
        decision = "workload=" + str(allowed).lower()
        append_lines(require("GITHUB_OUTPUT"), [decision])
        print(decision)
        return 0

    def prepare(self, phase):
        selected = scenario()
        if selected not in SCENARIOS or phase not in SCENARIOS[selected]:
            raise Failure("unsupported phase")
        return self.prepare_shared(selected, phase, SCENARIOS[selected][phase], selected.lower())

    def prepare_batch(self):
        self.validate_batch()
        selected = scenario()
        capture = capture_authentication_metadata() if selected == 'probe-batch-2' else False
        metadata = {'run_kind': os.environ['PARCHWELL_RUN_KIND'], 'items': batch_items()}
        if selected == 'probe-batch-2':
            metadata.update(probe_v2_selection=os.environ.get('PARCHWELL_BATCH_ITEMS', 'all'),
                            capture_authentication_metadata=capture)
        return self.prepare_shared(selected, "single-action", None, "probe", **metadata)

    def prepare_shared(self, selected, phase, configuration, prefix, **metadata):
        capture = capture_authentication_metadata()
        if selected == 'probe-batch-2':
            expected_items = batch_items()
            selector = os.environ.get('PARCHWELL_BATCH_ITEMS', 'all')
            expected_selection = 'authentication-controls' if capture else 'all'
            if (metadata.get('run_kind') != 'smoke' or metadata.get('items') != expected_items
                    or metadata.get('probe_v2_selection') != selector or selector != expected_selection
                    or metadata.get('capture_authentication_metadata') is not capture):
                raise Failure('unsupported probe batch selection')
        elif capture:
            raise Failure('unsupported probe batch selection')
        temporary, environment = require("RUNNER_TEMP"), require("GITHUB_ENV")
        run = f"{prefix}-{require('GITHUB_RUN_ID')}-{require('GITHUB_RUN_ATTEMPT')}"
        sha = require("GITHUB_SHA")
        if not re.fullmatch(r"[0-9a-f]{40}", sha) or len(run) > 29 or not re.fullmatch(LABEL, run):
            raise Failure("invalid run or source commit")
        self.validate_resource()
        secrets = {}
        for variable, name in (("S_CLUSTER_BACKEND", "cluster_backend.hcl"),
                               ("S_CLUSTER_TFVARS", "cluster_tfvars.tfvars.json"),
                               ("S_EPHEMERAL_BACKEND", "ephemeral_backend.hcl"),
                               ("S_EPHEMERAL_TFVARS", "ephemeral_tfvars.tfvars.json"),
                               ("S_PERSISTENT_OUTPUTS", "persistent-output.json")):
            value = os.environ.get(variable, "")
            if not value or len(value.encode("utf-8")) > 4 * MIB:
                raise Failure("missing or oversized " + variable)
            if name.endswith(".json") and not isinstance(strict_json(value), dict):
                raise Failure("expected JSON object: " + variable)
            secrets[name] = value
        ephemeral_variables = strict_json(secrets["ephemeral_tfvars.tfvars.json"])
        if selected == "probe-batch-2":
            ephemeral_variables["probe_namespace_data_roles"] = True
        elif ephemeral_variables.get("probe_namespace_data_roles", False) is not False:
            raise Failure("namespace probe roles require probe-batch-2")
        secrets["ephemeral_tfvars.tfvars.json"] = json.dumps(ephemeral_variables)
        persistent = strict_json(secrets["persistent-output.json"])
        acr = unwrap(persistent["acr"])
        if not re.fullmatch(r"[a-z0-9]+", acr["name"]) or not re.fullmatch(r"[a-z0-9.-]+", acr["login_server"]):
            raise Failure("invalid registry")
        connection = unwrap(persistent["application_insights_connection_string"])
        if not isinstance(connection, str) or not connection or "\r" in connection or "\n" in connection:
            raise Failure("invalid telemetry connection string")
        directories = [Path(tempfile.mkdtemp(prefix="parchwell-" + kind + "-", dir=temporary)).resolve()
                       for kind in ("private", "candidate", "artifacts")]
        for directory in directories:
            directory.chmod(0o700)
        if any(a == b or a in b.parents or b in a.parents
               for i, a in enumerate(directories) for b in directories[i + 1:]):
            raise Failure("phase directories overlap")
        private, candidate, artifacts = directories
        for name, value in secrets.items():
            write_private(private / name, value + "\n")
        write_json(private / "phase.json", {"scenario": selected, "phase": phase, "configuration": configuration,
                                           "run_id": run, "source_commit": sha, **metadata})
        status = {"phase": phase, "run_id": run, "modes": [], "candidate": None, "safety_stop": False}
        if selected in ("probe-batch", "probe-batch-2"):
            status["not_selected"] = []
        if selected == "probe-batch-2":
            scope_directory = private / "probe-v2"
            scope_directory.mkdir(mode=0o700)
            confirmation = os.environ.get("S_PROBE_V2_EFFECTIVE_SCOPE", "")
            if confirmation and len(confirmation.encode("utf-8")) <= 32768:
                write_private(scope_directory / "effective-scope.json", confirmation)
        write_json(artifacts / "phase-status.json", status)
        settings = {"PARCHWELL_PRIVATE_DIRECTORY": private, "PARCHWELL_CANDIDATE_DIRECTORY": candidate,
                    "PARCHWELL_ARTIFACT_DIRECTORY": artifacts,
                    "PARCHWELL_CLUSTER_BACKEND_CONFIG": private / "cluster_backend.hcl",
                    "PARCHWELL_CLUSTER_TFVARS_PATH": private / "cluster_tfvars.tfvars.json",
                    "PARCHWELL_EPHEMERAL_BACKEND_CONFIG": private / "ephemeral_backend.hcl",
                    "PARCHWELL_EPHEMERAL_TFVARS_PATH": private / "ephemeral_tfvars.tfvars.json",
                    "PARCHWELL_RUN_ID": run, "KUBECONFIG": private / "kubeconfig"}
        append_lines(environment, [f"{name}={value}" for name, value in settings.items()])
        return 0

    def prepare_stop(self):
        temporary, environment, output = require("RUNNER_TEMP"), require("GITHUB_ENV"), require("GITHUB_OUTPUT")
        self.validate_resource()
        private = Path(tempfile.mkdtemp(prefix="parchwell-stop-", dir=temporary)).resolve()
        private.chmod(0o700)
        append_lines(environment, [f"PARCHWELL_PRIVATE_DIRECTORY={private}"])
        append_lines(output, [f"private_directory={private}"])
        return 0

    def allow_c3(self):
        allowed = all(os.environ.get(name, "") == value for name, value in
                      (("PROBE_RESULT", "success"), ("FULL_ALLOWED", "true"), ("FULL_C0_RESULT", "success")))
        decision = "workload=" + str(allowed).lower()
        append_lines(require("GITHUB_OUTPUT"), [decision])
        print(decision)
        return 0

    def image_command(self, worker, reference, acr):
        return ["az", "acr", "repository", "show", "--name", acr["name"], "--image",
                f"parchwell/{worker}{reference}", "--query", "digest", "--output", "tsv"]

    def images(self, digests=None):
        phase = self.read("phase.json")
        acr = unwrap(self.read("persistent-output.json")["acr"])
        if digests is None:
            if self.run(["az", "acr", "login", "--name", acr["name"]], 60):
                raise Failure("registry login failed", 1)
        images = {}
        workers = [("loadgen", "LoadGen"), ("verifier", "Verifier")]
        if phase["scenario"] == "probe-batch":
            workers.append(("enrichment", "Enrichment"))
        if digests is not None and len(digests) != len(workers):
            raise Failure("image digest count mismatch")
        for index, (worker, project) in enumerate(workers):
            if digests is None:
                args = ["dotnet", "publish", f"src/Parchwell.{project}/Parchwell.{project}.csproj",
                        "--configuration", "Release", "-p:RestoreLockedMode=true",
                        "-p:NuGetLockFilePath=packages.linux-x64.lock.json", "-r", "linux-x64",
                        "--self-contained", "false", "/t:PublishContainer",
                        f"-p:ContainerRegistry={acr['login_server']}", f"-p:ContainerImageTag={phase['source_commit']}"]
                if self.run(args, 600):
                    raise Failure("image publish failed: " + worker, 1)
            reference = ":" + phase["source_commit"] if digests is None else "@" + digests[index]
            path = self.private / f"{worker}-digest.txt"
            attempt_limit = 2 if phase["scenario"] == "probe-batch-2" else 1
            for attempt in range(1, attempt_limit + 1):
                status = self.run(self.image_command(worker, reference, acr), 60, output=path, maximum=4096)
                detail = f"{worker} (attempt={attempt}/{attempt_limit}, exit={status})"
                if status == 0:
                    break
                if status not in (1, 124) or attempt == attempt_limit:
                    raise Failure("image readback failed: " + detail, 1)
                print("image readback retry: " + detail, file=sys.stderr)
            digest = path.read_text().strip()
            if not re.fullmatch(DIGEST, digest) or (digests is not None and digest != digests[index]):
                raise Failure("image digest mismatch: " + worker, 1)
            images[worker] = {"digest": digest, "reference": f"{acr['login_server']}/parchwell/{worker}@{digest}"}
        phase["images"] = images
        write_json(self.private / "phase.json", phase)
        if digests is None:
            append_lines(require("GITHUB_OUTPUT"), [f"{worker}_digest={images[worker]['digest']}" for worker in images])
        return 0

    def publish_images(self):
        return self.images()

    def use_images(self, loadgen, verifier):
        if any(not re.fullmatch(DIGEST, value) for value in (loadgen, verifier)):
            raise Failure("invalid image digest")
        return self.images((loadgen, verifier))

    def blob_host(self):
        blob = unwrap(self.read("ephemeral-output.json")["blob"])
        host = urlsplit(blob["account_url"]).hostname
        if host is None or not re.fullmatch(r"[a-z0-9]{3,24}\.blob\.core\.windows\.net", host):
            raise Failure("invalid blob host")
        return host

    def check_dns(self):
        ephemeral = self.read("ephemeral-output.json")
        eventhubs = unwrap(ephemeral["eventhubs"])["namespace_name"] + ".servicebus.windows.net"
        ips = unwrap(ephemeral["private_endpoint_ips"])
        for number, (host, address) in enumerate(((eventhubs, ips["eventhubs"]), (self.blob_host(), ips["blob"]))):
            path = self.private / f"dns-{number}.json"
            code = self.run([sys.executable, "infra/scripts/phase/phase.py", "resolve", host], 15,
                            output=path, maximum=65536)
            if code or set(strict_json(path.read_bytes())) != {address}:
                raise Failure("private DNS check failed: " + host, 1)
        return 0

    def values(self, mode, repeat, item=None, configuration=None, action_role=None):
        phase = self.read("phase.json")
        ephemeral = self.read("ephemeral-output.json")
        persistent = self.read("persistent-output.json")
        accounts = unwrap(self.read("cluster-output.json")["workload_identity"])["service_accounts"]
        single = mode in SINGLE_ACTION_MODES
        runtime = {"run_id": phase["run_id"], "phase": configuration if single else phase["phase"], "mode": mode,
                   "source_commit": phase["source_commit"],
                   "loadgen_image": phase["images"]["loadgen"]["reference"],
                   "verifier_image": phase["images"]["verifier"]["reference"],
                   "service_accounts": {role: {key: accounts[role][key] for key in ("name", "client_id", "subject")}
                                        for role in (("loadgen", "verifier", "probe", "enrichment-consumer") if single else ("loadgen", "verifier"))},
                   "eventhubs": unwrap(ephemeral["eventhubs"]), "blob": unwrap(ephemeral["blob"]),
                   "eventhubs_ip": unwrap(ephemeral["private_endpoint_ips"])["eventhubs"],
                   "blob_ip": unwrap(ephemeral["private_endpoint_ips"])["blob"],
                   "application_insights_connection_string": unwrap(persistent["application_insights_connection_string"])}
        if single:
            runtime.update(action_role=action_role, image_smoke=phase["run_kind"] == "smoke",
                           enrichment_image=phase["images"]["enrichment"]["reference"])
            if mode == "single-action-judge" and item == "F04" and not runtime["image_smoke"]:
                runtime["seed_receipt_configmap"] = phase["run_id"] + "-f04-seed-receipt"
        elif mode != "judge":
            runtime.update(repeat=repeat, loadgen_job_name=job_name(phase["run_id"], phase["phase"], "loadgen", repeat))
        return {"scenario_id": item if single else phase["scenario"],
                "configuration_id": configuration if single else phase["configuration"], "runtime": runtime}

    def render(self, mode, repeat, item=None, configuration=None, action_role=None):
        phase = self.read("phase.json")
        job = job_name(phase["run_id"], phase["phase"], mode, repeat, item, configuration, action_role)
        values = self.private / f"values-{job}.json"
        rendered = self.private / f"rendered-{job}.yaml"
        write_json(values, self.values(mode, repeat, item, configuration, action_role))
        self.render_code = self.run(["helm", "template", "parchwell", "charts/parchwell", "--values", str(values)],
                                    120, output=rendered, maximum=4 * MIB)
        if self.render_code:
            return None
        text = rendered.read_text()
        deadlines = re.findall(r"^  activeDeadlineSeconds: ([1-9][0-9]*)$", text, re.MULTILINE)
        if text.splitlines().count("kind: Job") != 1 or text.splitlines().count(f'  name: "{job}"') != 1 or len(deadlines) != 1:
            return None
        return int(deadlines[0])

    def run_mode(self, mode, repeat, item=None, configuration=None, action_role=None, skip_reason=None, absolute_deadline=None):
        previous_deadline = self.command_deadline
        self.command_deadline = absolute_deadline
        phase = self.read("phase.json")
        job = job_name(phase["run_id"], phase["phase"], mode, repeat, item, configuration, action_role)
        state = self.state()
        record = {"job": job, "repeat": repeat, "mode": mode,
                  **{step: None for step in ("render", "apply", "wait", "collect", "delete", "confirm")},
                  "deadline": None, "outcome": "not_attempted"}
        if mode in SINGLE_ACTION_MODES:
            record.update(item=item, configuration=configuration, role=action_role, image_smoke=phase["run_kind"] == "smoke")
        state["modes"].append(record)

        def save():
            self.save_state(state)

        def command(step, args, seconds, **options):
            code = self.run(args, seconds, **options)
            record[step] = code
            save()
            if code:
                print(f"::warning::{job} {step} exit {code}", flush=True)
            return code

        print("::group::" + job, flush=True)
        save()
        try:
            if state.get("safety_stop", False):
                if mode in SINGLE_ACTION_MODES:
                    record["reason"] = "safety_stop"
                    save()
                return record
            if absolute_deadline is not None and absolute_deadline - time.monotonic() <= TERMINATION_RESERVE:
                record["reason"] = "orchestration_deadline"
                save()
                return record
            if skip_reason:
                record["reason"] = skip_reason
                save()
                return record
            deadline = self.render(mode, repeat, item, configuration, action_role)
            record["render"] = self.render_code
            record["deadline"] = deadline
            if self.render_code:
                print(f"::warning::{job} render exit {self.render_code}", flush=True)
            if deadline is None:
                record["outcome"] = "render_failed"
                save()
                return record
            save()
            rendered = self.private / f"rendered-{job}.yaml"
            if command("apply", ["kubectl", "apply", "-f", str(rendered), "--request-timeout=30s"], 60):
                record["outcome"] = "apply_failed"
            else:
                code = command("wait", ["bash", "infra/scripts/lifecycle/lifecycle.sh", "wait-job", job, str(deadline)], deadline + 90)
                record["outcome"] = "complete" if code == 0 else "failed" if code == 1 else "unknown"
                save()
                code = command("collect", ["bash", "infra/scripts/lifecycle/lifecycle.sh", "collect-job", job], 210)
                if code == 1:
                    record["collect_gap"] = True
                elif code and record["outcome"] != "unknown":
                    record["outcome"] = "collect_error"
            save()
            if record["outcome"] in ("unknown", "apply_failed"):
                deleted = command("delete", ["kubectl", "delete", "job", job, "-n", "parchwell", "--ignore-not-found",
                    "--cascade=foreground", "--wait=true", "--timeout=90s", "--request-timeout=30s"], 120)
                confirmation = self.private / f"confirm-{job}.json"
                confirmed = command("confirm", ["kubectl", "get", "pods", "-n", "parchwell", "-l", f"job-name={job}",
                    "-o", "json", "--request-timeout=30s"], 30, output=confirmation, maximum=4 * MIB)
                empty = False
                if not confirmed:
                    try:
                        decode = single_action_json if mode in SINGLE_ACTION_MODES else strict_json
                        confirmation_data = decode(confirmation.read_bytes())
                        empty = isinstance(confirmation_data, dict) and confirmation_data.get("items") == []
                    except (Failure, OSError, ValueError, AttributeError):
                        pass
                if deleted or confirmed or not empty:
                    state["safety_stop"] = True
                    print(f"safety_stop: pods of {job} may still run", flush=True)
            save()
            if absolute_deadline is not None and absolute_deadline - time.monotonic() <= TERMINATION_RESERVE:
                record["reason"] = "orchestration_deadline"
                if record["apply"] == 0 and record["outcome"] != "complete":
                    state["safety_stop"] = True
                save()
            return record
        finally:
            if absolute_deadline is not None and absolute_deadline - time.monotonic() <= TERMINATION_RESERVE and not skip_reason and record.get("reason") != "safety_stop":
                record["reason"] = "orchestration_deadline"
                if record["apply"] is None:
                    record["outcome"] = "not_attempted"
                save()
            self.command_deadline = previous_deadline
            print("::endgroup::", flush=True)

    def run_repeats(self):
        records = [self.run_mode(mode, repeat) for repeat in SLOTS for mode in MODES]
        return int(any(record["outcome"] in BAD_OUTCOMES for record in records))

    def judge(self):
        return int(self.run_mode("judge", None)["outcome"] in BAD_OUTCOMES)

    def smoke_complete(self, record):
        """Check worker evidence from collect-job's public artifacts."""
        valid = False
        if record["outcome"] == "complete" and record["collect"] == 0:
            try:
                directory = self.artifacts / record["job"]
                collection = single_action_json((directory / "collection-status.json").read_bytes())
                pods = single_action_json((directory / "pods.json").read_bytes())["items"]
                if collection["complete"] is not True or collection["gaps"] != [] or not isinstance(pods, list) or len(pods) != 1:
                    raise ValueError("incomplete collection or pod count")
                pod = pods[0]
                role = record["role"]
                worker = "enrichment" if record["mode"] == "single-action-seed" else "verifier" if record["mode"] == "single-action-judge" else "loadgen"
                expected_image = self.read("phase.json")["images"][worker]["reference"]
                containers = pod["spec"]["containers"]
                statuses = [status for status in pod["status"]["containerStatuses"] if status["name"] == role]
                if (pod["spec"]["serviceAccountName"] != role or containers[0]["image"] != expected_image
                        or len(statuses) != 1):
                    raise ValueError("unexpected worker identity or image")
                exit_code = statuses[0]["state"]["terminated"]["exitCode"]
                if type(exit_code) is not int or exit_code != 0:
                    raise ValueError("worker did not exit zero")
                name = pod["metadata"]["name"]
                if not isinstance(name, str) or not re.fullmatch(LABEL, name):
                    raise ValueError("invalid pod name")
                expected_marker = SEED_SMOKE_MARKER if worker == "enrichment" else SMOKE_MARKER
                for line in (directory / f"{name}.containers.{role}.log").read_text().splitlines():
                    try:
                        marker = single_action_json(line)
                    except (ValueError, UnicodeError, Failure):
                        continue
                    if (marker == expected_marker and isinstance(marker, dict)
                            and all(type(marker[key]) is type(value) for key, value in expected_marker.items())):
                        valid = True
                        break
            except (OSError, ValueError, UnicodeError, KeyError, TypeError, IndexError, Failure):
                pass
        state = self.state()
        for saved in state["modes"]:
            if saved["job"] == record["job"]:
                saved["smoke_valid"] = valid
        self.save_state(state)
        return valid

    @staticmethod
    def boot_identity():
        """Read a bounded boot-session UUID without reconstructing wall-clock time."""
        try:
            if sys.platform.startswith("linux"):
                with Path("/proc/sys/kernel/random/boot_id").open("rb") as stream:
                    value = stream.read(65)
            elif sys.platform == "darwin":
                # Apple's read-only scalar (verified 2026-10-26):
                # https://github.com/apple-oss-distributions/xnu/blob/f6217f891ac0bb64f3d375211650a4c1ff8ca1ea/bsd/kern/kern_sysctl.c#L2921
                # https://developer.apple.com/library/archive/documentation/System/Conceptual/ManPages_iPhoneOS/man3/sysctlbyname.3.html
                read_value = ctypes.CDLL(None, use_errno=True).sysctlbyname
                read_value.argtypes = [ctypes.c_char_p, ctypes.c_void_p, ctypes.POINTER(ctypes.c_size_t),
                                       ctypes.c_void_p, ctypes.c_size_t]
                read_value.restype = ctypes.c_int
                buffer = ctypes.create_string_buffer(64)
                length = ctypes.c_size_t(len(buffer))
                if read_value(b"kern.bootsessionuuid", buffer, ctypes.byref(length), None, 0) != 0:
                    return None
                if not 0 < length.value <= len(buffer):
                    return None
                value = buffer.raw[:length.value].rstrip(b"\0")
            else:
                return None
            if len(value) > 64:
                return None
            identity = value.decode("ascii").strip().lower()
            return identity if re.fullmatch(r"[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}", identity) else None
        except (OSError, AttributeError, UnicodeError, ValueError):
            return None

    def start_batch_clock(self):
        phase = self.read("phase.json")
        phase["batch_started_monotonic"] = time.monotonic()
        phase["batch_clock_boot_id"] = self.boot_identity()
        phase["batch_clock_run_id"] = phase["run_id"]
        phase["batch_clock_source"] = "CLOCK_MONOTONIC"
        write_json(self.private / "phase.json", phase)
        return phase["batch_clock_boot_id"] is not None

    def admission_allowed(self):
        phase = self.read("phase.json")
        started = phase.get("batch_started_monotonic")
        now = time.monotonic()
        try:
            return (type(started) in (int, float) and 0 <= started <= now and math.isfinite(started)
                    and phase.get("batch_clock_boot_id") is not None
                    and phase["batch_clock_boot_id"] == self.boot_identity()
                    and phase.get("batch_clock_run_id") == phase["run_id"]
                    and phase.get("batch_clock_source") == "CLOCK_MONOTONIC"
                    and now - started < BATCH_START_CAP)
        except (OSError, ValueError, OverflowError):
            return False

    def transfer_seed(self, record, deadline):
        """Transfer the raw seed receipt and Kubernetes identity without judging delivery."""
        phase = self.read("phase.json")
        directory = self.artifacts / record["job"]
        try:
            pods_path = directory / "pods.json"
            if not 0 < pods_path.stat().st_size <= 4 * MIB:
                raise ValueError("seed pod size")
            pods = single_action_json(pods_path.read_bytes())["items"]
            if not isinstance(pods, list) or len(pods) != 1:
                raise ValueError("seed pod count")
            pod = pods[0]
            metadata = pod["metadata"]
            name = metadata["name"]
            if not isinstance(name, str) or not re.fullmatch(LABEL, name):
                raise ValueError("seed pod name")
            references = metadata["ownerReferences"]
            if not isinstance(references, list) or any(not isinstance(owner, dict) for owner in references):
                raise ValueError("seed pod owner schema")
            owners = [owner for owner in references if owner.get("controller") is True]
            if len(owners) != 1 or owners[0]["kind"] != "Job" or owners[0]["name"] != record["job"]:
                raise ValueError("seed pod owner")
            envelope = {"schema_version": 1, "job_name": record["job"], "pod_name": name,
                        "pod_uid": metadata["uid"], "service_account": pod["spec"]["serviceAccountName"],
                        "owner_kind": owners[0]["kind"], "owner_name": owners[0]["name"]}
            if any(not isinstance(value, str) or not value for key, value in envelope.items() if key != "schema_version"):
                raise ValueError("seed pod identity schema")
            log_path = directory / f"{name}.containers.enrichment-consumer.log"
            if log_path.stat().st_size > 16 * MIB:
                raise ValueError("seed log size")
            with log_path.open("rb") as stream:
                receipts = []
                for line in stream:
                    if line.startswith(SEED_RECEIPT_MARKER.encode()):
                        if len(line) > 65536:
                            raise ValueError("seed receipt size")
                        receipts.append(line[len(SEED_RECEIPT_MARKER):].rstrip(b"\r\n"))
            if len(receipts) != 1 or not isinstance(single_action_json(receipts[0]), dict):
                raise ValueError("seed receipt count or JSON")
            envelope_bytes = json.dumps(envelope).encode()
            if len(envelope_bytes) > 65536:
                raise ValueError("seed envelope size")
            configmap = {"apiVersion": "v1", "kind": "ConfigMap", "metadata": {
                "name": phase["run_id"] + "-f04-seed-receipt", "namespace": "parchwell",
                "labels": {"app.kubernetes.io/instance": phase["run_id"]}}, "immutable": True,
                "data": {"receipt.json": receipts[0].decode("utf-8"), "pod.json": envelope_bytes.decode()}}
            path = self.private / "seed-receipt-configmap.json"
            write_json(path, configmap)
            self.command_deadline = deadline
            if self.run(["kubectl", "create", "-f", str(path), "--request-timeout=30s"], 60):
                raise ValueError("seed ConfigMap create")
            return True
        except (OSError, ValueError, KeyError, TypeError, UnicodeError, Failure):
            write_json(self.artifacts / "seed-gap.json", {"job": record["job"], "reason": "seed_receipt_transfer"})
            return False
        finally:
            self.command_deadline = None

    def single_action_seed(self):
        if scenario() == 'probe-batch-2':
            self.validate_batch()
            return 0
        frozen = None
        if os.environ.get('PARCHWELL_PRIVATE_DIRECTORY'):
            try:
                frozen = self.read('phase.json')
            except (OSError, Failure, ValueError, KeyError, TypeError, UnicodeError):
                pass
        if isinstance(frozen, dict) and frozen.get('scenario') == 'probe-batch-2':
            self.probe_v2().selection(frozen)
            return 0
        self.validate_batch()
        clock_available = self.start_batch_clock()
        if 'F04' not in self.read('phase.json')['items']:
            return 0
        deadline = time.monotonic() + SEED_DEADLINE
        record = self.run_mode("single-action-seed", None, "F04", "C2", "enrichment-consumer",
                               skip_reason=None if clock_available else "start_cap", absolute_deadline=deadline)
        success = (self.smoke_complete(record) if self.read("phase.json")["run_kind"] == "smoke"
                   else self.transfer_seed(record, deadline))
        state = self.state()
        state["seed_gap"] = not success
        self.save_state(state)
        # A seed gap remains evidence for the judge; unrelated configurations still run.
        return 0

    def run_single_action_batch(self):
        frozen = None
        try:
            frozen = self.read('phase.json')
        except (OSError, Failure, ValueError, KeyError, TypeError, UnicodeError):
            pass
        if isinstance(frozen, dict) and frozen.get('scenario') == 'probe-batch-2':
            self.probe_v2().selection(frozen)
            return self.probe_v2().run_batch()
        self.validate_batch()
        if scenario() == "probe-batch-2":
            return self.probe_v2().run_batch()
        selected = self.read('phase.json')['items']
        state = self.state()
        state['not_selected'] = [
            {'item': item, 'configuration': configuration}
            for item, configuration, _ in SINGLE_ACTION_CONFIGURATIONS
            if item not in selected]
        self.save_state(state)
        passed = True
        smoke = self.read("phase.json")["run_kind"] == "smoke"
        for item, configuration, role in SINGLE_ACTION_CONFIGURATIONS:
            if item not in selected:
                continue
            reason = None if self.admission_allowed() else "start_cap"
            deadline = time.monotonic() + PAIR_DEADLINE
            for mode, selected_role in (("single-action", role), ("single-action-judge", "verifier")):
                record = self.run_mode(mode, None, item, configuration, selected_role,
                                       skip_reason=reason, absolute_deadline=deadline)
                if smoke:
                    if not self.smoke_complete(record):
                        passed = False
                    if mode == "single-action-judge":
                        self.candidate_gap(item, configuration, "image_smoke")
                elif mode == "single-action-judge":
                    if record["outcome"] == "not_attempted":
                        self.candidate_gap(item, configuration, record.get("reason", "not_attempted"))
                        passed = False
                    elif not self.single_action_candidate(record, deadline):
                        passed = False
        return int(not passed)

    def candidate_gap(self, item, configuration, reason):
        write_json(self.candidates / f"{item}-{configuration}-gap.json", {
            "item": item, "configuration": configuration, "reason": reason})
        state = self.state()
        state.setdefault("candidates", []).append({"item": item, "configuration": configuration,
            "accepted": False, "candidate": None, "judge_completion": None})
        self.save_state(state)

    def download_blob(self, name, target, maximum, deadline):
        previous = self.command_deadline
        self.command_deadline = deadline
        partial = Path(str(target) + ".partial")
        properties = self.private / (target.name + ".properties.json")
        account = self.blob_host().split(".")[0]
        try:
            args = ["az", "storage", "blob", "show", "--account-name", account, "--container-name", "verifier",
                    "--name", name, "--auth-mode", "login", "--query", "properties", "--only-show-errors", "-o", "json"]
            if self.run(args, 60, output=properties, maximum=MIB):
                return None
            data = single_action_json(properties.read_bytes())
            length, etag = data["contentLength"], data["etag"]
            if type(length) is not int or not 0 < length <= maximum or not isinstance(etag, str) or not etag:
                return None
            args = ["az", "storage", "blob", "download", "--account-name", account, "--container-name", "verifier",
                    "--name", name, "--file", str(partial), "--auth-mode", "login", "--if-match", etag,
                    "--overwrite", "false", "--only-show-errors", "-o", "none"]
            if self.run(args, 60) or partial.stat().st_size != length:
                return None
            content = partial.read_bytes()
            partial.replace(target)
            return content
        except (OSError, ValueError, KeyError, TypeError, UnicodeError, Failure):
            return None
        finally:
            partial.unlink(missing_ok=True)
            self.command_deadline = previous

    @staticmethod
    def candidate_transport_shape(candidate):
        """Check closed JSON transport types; the verifier owns every scientific decision."""
        def closed(value, keys):
            return isinstance(value, dict) and set(value) == set(keys.split())

        def outcome(value):
            return value is None or (closed(value, "result error_code is_fatal origin")
                and isinstance(value["result"], str)
                and (value["error_code"] is None or type(value["error_code"]) is int)
                and (value["is_fatal"] is None or type(value["is_fatal"]) is bool)
                and (value["origin"] is None or isinstance(value["origin"], str)))

        def strings(value):
            return isinstance(value, list) and all(isinstance(item, str) for item in value)

        if (candidate["status"] not in ("complete_valid", "invalid", "incomplete")
                or candidate["deterministic"] is not None and type(candidate["deterministic"]) is not bool
                or type(candidate["security_finding"]) is not bool or not isinstance(candidate["adr012_counts"], str)
                or not strings(candidate["diagnostics"]) or not outcome(candidate["outcome_class"])
                or any(value is not None and not isinstance(value, str)
                       for value in (candidate["managed_version"], candidate["native_version"]))):
            return False
        repeats = candidate["repeats"]
        if not isinstance(repeats, list) or len(repeats) != 5:
            return False
        for number, repeat in enumerate(repeats, 1):
            if (not closed(repeat, "repeat status reasons outcome_class broker_count statistics_reference")
                    or type(repeat["repeat"]) is not int or repeat["repeat"] != number
                    or repeat["status"] not in ("complete_valid", "invalid", "incomplete")
                    or not strings(repeat["reasons"]) or not outcome(repeat["outcome_class"])
                    or repeat["broker_count"] is not None and type(repeat["broker_count"]) is not int):
                return False
            reference = repeat["statistics_reference"]
            if reference is not None and (not closed(reference, "sequence native_time native_ts raw_sha256")
                    or any(type(reference[key]) is not int for key in ("sequence", "native_time", "native_ts"))
                    or not isinstance(reference["raw_sha256"], str)):
                return False
        assessment = candidate["item_assessment"]
        return assessment is None or (closed(assessment, "verdict hypotheses reason")
            and all(assessment[key] is None or isinstance(assessment[key], str) for key in ("verdict", "reason"))
            and isinstance(assessment["hypotheses"], dict)
            and all(isinstance(value, str) for value in assessment["hypotheses"].values()))

    def single_action_candidate(self, record, deadline):
        phase = self.read("phase.json")
        item, configuration = record["item"], record["configuration"]
        prefix = f"{phase['run_id']}/single-action/{item}/{configuration}/"
        stem = f"{item}-{configuration}"
        target = self.candidates / (stem + ".json")
        receipt_target = self.candidates / (stem + "-judge-completion.json")
        candidate_bytes = self.download_blob(prefix + "candidate.json", target, CANDIDATE_MAXIMUM, deadline)
        receipt_bytes = self.download_blob(prefix + "judge-completion.json", receipt_target, 65536, deadline)
        accepted = False
        try:
            candidate = single_action_json(candidate_bytes)
            receipt = single_action_json(receipt_bytes)
            candidate_keys = {"schema_version", "mode", "synthetic", "run_id", "scenario_id", "configuration_id",
                "source_commit", "managed_version", "native_version", "image_digests", "status", "repeats", "deterministic",
                "outcome_class", "adr012_counts", "diagnostics", "security_finding", "item_assessment"}
            receipt_keys = {"schema_version", "mode", "run_id", "scenario_id", "configuration_id", "source_commit", "job_name",
                "pod_name", "pod_uid", "candidate_name", "handler_exit_code", "cleanup_completed"}
            if not isinstance(candidate, dict) or set(candidate) != candidate_keys or not isinstance(receipt, dict) or set(receipt) != receipt_keys:
                raise ValueError("transport fields")
            for document in (candidate, receipt):
                if (type(document["schema_version"]) is not int or document["schema_version"] != 1
                        or any(document[key] != value for key, value in (("run_id", phase["run_id"]), ("scenario_id", item),
                        ("configuration_id", configuration), ("source_commit", phase["source_commit"])))):
                    raise ValueError("transport identity")
            if (not self.candidate_transport_shape(candidate) or candidate["mode"] != "single-action" or candidate["synthetic"] is not True
                    or candidate["image_digests"] != {worker: data["digest"] for worker, data in phase["images"].items()}
                    or receipt["mode"] != "single-action-judge" or receipt["candidate_name"] != prefix + "candidate.json"
                    or receipt["job_name"] != record["job"] or type(receipt["handler_exit_code"]) is not int
                    or receipt["handler_exit_code"] != 0 or receipt["cleanup_completed"] is not True
                    or not isinstance(receipt["pod_name"], str) or not re.fullmatch(LABEL, receipt["pod_name"])
                    or not isinstance(receipt["pod_uid"], str) or not receipt["pod_uid"]):
                raise ValueError("transport receipt")
            pods_path = self.artifacts / record["job"] / "pods.json"
            if not 0 < pods_path.stat().st_size <= 4 * MIB:
                raise ValueError("judge pod size")
            pods = single_action_json(pods_path.read_bytes())["items"]
            if (not isinstance(pods, list) or len(pods) != 1
                    or pods[0]["metadata"]["name"] != receipt["pod_name"]
                    or pods[0]["metadata"]["uid"] != receipt["pod_uid"]):
                raise ValueError("judge receipt pod binding")
            accepted = True
        except (OSError, ValueError, KeyError, TypeError, UnicodeError, Failure):
            pass
        state = self.state()
        state.setdefault("candidates", []).append({"item": item, "configuration": configuration, "accepted": accepted,
            "candidate": target.name if candidate_bytes else None, "judge_completion": receipt_target.name if receipt_bytes else None})
        self.save_state(state)
        if not accepted:
            write_json(self.candidates / (stem + "-gap.json"), {"item": item, "configuration": configuration, "reason": "candidate_transport"})
        return accepted

    def check_candidate(self, content):
        if not 0 < len(content) <= CANDIDATE_MAXIMUM:
            raise Failure("candidate size invalid", 1)
        try:
            candidate = strict_json(content)
            if not isinstance(candidate, dict):
                raise Failure("candidate must be an object", 1)
            phase = self.read("phase.json")
            statuses = ("complete_valid", "invalid", "incomplete")
            valid = isinstance(candidate, dict) and type(candidate.get("schema_version")) is int and candidate["schema_version"] == 1
            valid = valid and candidate.get("synthetic") is True and candidate.get("scenario_id") == phase["scenario"]
            for key, expected in (("run_id", phase["run_id"]), ("phase", phase["phase"]),
                                  ("configuration_id", phase["configuration"]), ("source_commit", phase["source_commit"])):
                valid = valid and candidate.get(key) == expected
            valid = valid and candidate.get("status") in statuses
            gate = candidate.get("gate")
            valid = valid and "gate" in candidate and (gate in ("pass", "untestable_at_build_scale", "inconclusive")
                    if phase["phase"] == "probe" else gate is None)
            valid = valid and candidate.get("image_digests") == {worker: phase["images"][worker]["digest"] for worker in ("loadgen", "verifier")}
            repeats = candidate.get("repeats")
            valid = valid and isinstance(repeats, list) and len(repeats) == 5
            valid = valid and all(isinstance(item, dict) and type(item.get("repeat")) is int
                                 and item["repeat"] == number and item.get("status") in statuses
                                 for number, item in enumerate(repeats, 1))
            if not valid:
                raise Failure("candidate contract invalid", 1)
            return candidate
        except (ValueError, UnicodeError, KeyError, TypeError, Failure) as error:
            raise Failure("candidate contract invalid", 1) from error

    def candidate(self):
        output = require("GITHUB_OUTPUT")
        partial = self.candidates / "candidate.json.partial"
        target = self.candidates / "candidate.json"
        success = False
        target.unlink(missing_ok=True)
        try:
            phase = self.read("phase.json")
            account = self.blob_host().split(".")[0]
            name = candidate_blob(phase["run_id"], phase["phase"])
            for attempt in range(3):
                partial.unlink(missing_ok=True)
                properties = self.private / "candidate-properties.json"
                args = ["az", "storage", "blob", "show", "--account-name", account, "--container-name", "verifier",
                        "--name", name, "--auth-mode", "login", "--query", "properties", "--only-show-errors", "-o", "json"]
                code = self.run(args, 60, output=properties, maximum=MIB)
                if not code:
                    try:
                        data = strict_json(properties.read_bytes())
                        length, etag = data["contentLength"], data["etag"]
                        if type(length) is not int or not 1 <= length <= CANDIDATE_MAXIMUM:
                            raise Failure("candidate content length invalid", 1)
                        if not isinstance(etag, str) or not etag:
                            raise ValueError("invalid etag")
                    except (ValueError, KeyError, TypeError, UnicodeError):
                        code = 1
                    if not code:
                        args = ["az", "storage", "blob", "download", "--account-name", account,
                                "--container-name", "verifier", "--name", name, "--file", str(partial),
                                "--auth-mode", "login", "--if-match", etag, "--overwrite", "false",
                                "--only-show-errors", "-o", "none"]
                        code = self.run(args, 60)
                        if not code and partial.stat().st_size == length:
                            candidate = self.check_candidate(partial.read_bytes())
                            partial.replace(target)
                            state = self.state()
                            state["candidate"] = {"status": candidate["status"], "gate": candidate["gate"],
                                                  "repeats": [[item["repeat"], item["status"]] for item in candidate["repeats"]]}
                            self.save_state(state)
                            append_lines(output, ["full_allowed=" + str(phase["phase"] == "probe" and candidate["gate"] == "pass").lower()])
                            success = True
                            return 0
                if attempt < 2:
                    self.sleep(attempt + 1)
            raise Failure("candidate retrieval exhausted", 1)
        except (OSError, ValueError, KeyError, TypeError, UnicodeError, Failure) as error:
            raise Failure("candidate retrieval failed", 1) from error
        finally:
            partial.unlink(missing_ok=True)
            if not success:
                append_lines(output, ["full_allowed=false"])

    def delete_workload(self):
        phase = self.read("phase.json")
        return self.run(["kubectl", "delete", "jobs,configmaps" if phase["scenario"] in ("probe-batch", "probe-batch-2") else "jobs", "-n", "parchwell", "-l",
                         f"app.kubernetes.io/instance={phase['run_id']}", "--ignore-not-found", "--wait=false",
                         "--request-timeout=30s"], 60)

    def report(self):
        results = strict_json(os.environ.get("STEP_RESULTS", ""))
        workload = require("WORKLOAD")
        if workload not in ("true", "false"):
            raise Failure("invalid workload decision")
        required = os.environ.get("REQUIRED_STEPS", "").split()
        if workload == "true":
            required += os.environ.get("WORKLOAD_STEPS", "").split()
        outcomes = [(step, results.get(step, {}).get("outcome", "missing")) for step in required]
        passed = require("JOB_STATUS") == "success" and all(outcome == "success" for _, outcome in outcomes)
        title = os.environ.get("PARCHWELL_PHASE", os.environ.get("GITHUB_JOB", "job"))
        lines = [f"## {scenario()} {title}: {'passed' if passed else 'failed'}"]
        lines += [f"{step}: {outcome}" for step, outcome in outcomes]
        artifact = os.environ.get("PARCHWELL_ARTIFACT_DIRECTORY")
        if artifact and (Path(artifact) / "phase-status.json").is_file():
            state = self.state()
            lines += [f"{mode['job']}: {mode['outcome']}" for mode in state["modes"]]
            for skipped in state.get('not_selected', []):
                lines += [f"{skipped['item']}-{skipped['configuration']}: not selected"]
            if self.read('phase.json').get('probe_v2_selection') == 'authentication-controls':
                lines += ['Selected authentication controls: F06-C6,F06-C9']
            if state.get("seed_gap"):
                lines += ["seed receipt gap"]
            for candidate in state.get("candidates", []):
                lines += [f"{candidate['item']}-{candidate['configuration']}: accepted={candidate['accepted']} candidate={candidate['candidate']} judge_completion={candidate['judge_completion']}"]
                if "smoke_valid" in candidate:
                    lines += [f"{candidate['item']}-{candidate['configuration']}: smoke_valid={candidate['smoke_valid']}"]
            if state["candidate"] is not None:
                lines += [f"candidate status: {state['candidate']['status']}", f"candidate gate: {state['candidate']['gate']}"]
        text = "\n".join(lines) + "\n"
        print(text, end="")
        with open(require("GITHUB_STEP_SUMMARY"), "a", encoding="utf-8") as stream:
            stream.write(text)
        return int(not passed)


def main(argv):
    commands = {"prepare": (1, "prepare"), "prepare-stop": (0, "prepare_stop"), "allow-c3": (0, "allow_c3"),
                "validate-batch": (0, "validate_batch"), "prepare-batch": (0, "prepare_batch"),
                "single-action-seed": (0, "single_action_seed"), "run-single-action-batch": (0, "run_single_action_batch"),
                "validate-scenario": (0, "validate_scenario"), "allow-images": (0, "allow_images"),
                "publish-images": (0, "publish_images"), "use-images": (2, "use_images"), "check-dns": (0, "check_dns"),
                "run-repeats": (0, "run_repeats"), "judge": (0, "judge"), "candidate": (0, "candidate"),
                "delete-workload": (0, "delete_workload"), "report": (0, "report")}
    try:
        if len(argv) == 2 and argv[0] == "resolve":
            print(json.dumps(sorted({answer[4][0] for answer in socket.getaddrinfo(argv[1], 443, type=socket.SOCK_STREAM)})))
            return 0
        if not argv or argv[0] not in commands or len(argv) != commands[argv[0]][0] + 1:
            raise Failure("unsupported command or argument count")
        return getattr(Phase(), commands[argv[0]][1])(*argv[1:])
    except Failure as error:
        print(str(error), file=sys.stderr)
        return error.code
    except (OSError, ValueError, KeyError, TypeError, UnicodeError) as error:
        # Inputs can contain credentials, so avoid echoing parser diagnostics.
        print("phase command failed: " + type(error).__name__, file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

#!/bin/bash
set -euo pipefail
# shellcheck source=infra/scripts/lifecycle/common.sh
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"

python3 - "$@" <<'PYTHON'
import json
import math
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import threading
import time

MIB = 1024 * 1024
MARKER = b"parchwell-cluster-smoke-ok\n"


class Failure(Exception):
    def __init__(self, message, code=2):
        super().__init__(message)
        self.code = code


def require(name):
    value = os.environ.get(name)
    if not value or "\n" in value or "\r" in value:
        raise Failure("missing or invalid " + name)
    return value


def label(value, maximum=63):
    if len(value) > maximum or not re.fullmatch(r"[a-z0-9](?:[a-z0-9-]*[a-z0-9])?", value):
        raise Failure("invalid DNS label")
    return value


class Lifecycle:
    def __init__(self):
        self.repo = Path(require("PARCHWELL_REPOSITORY")).resolve()
        self.private = Path(require("PARCHWELL_PRIVATE_DIRECTORY")).resolve()
        self.private.mkdir(parents=True, exist_ok=True, mode=0o700)
        self.helper = self.repo / "infra/scripts/lifecycle/command.py"
        os.environ["KUBECONFIG"] = str(self.private / "kubeconfig")

    def run(self, args, seconds, output=None, maximum=4 * MIB, end=None, errors=False, env=None):
        if end is not None:
            seconds = min(seconds, end - time.monotonic())
        if seconds <= 0:
            return 124, b""
        if not shutil.which(args[0]):
            raise Failure("required command missing: " + args[0])
        command = [sys.executable, str(self.helper), str(seconds)]
        if output is not None:
            command += ["--stdout-file", str(output), "--max-bytes", str(maximum)]
        command += ["--", *map(str, args)]
        # Only az error classification needs stderr. Stream every byte while
        # retaining a bounded prefix; a clipped error cannot prove absence.
        if errors:
            process = subprocess.Popen(command, env=env, cwd=self.repo, stderr=subprocess.PIPE)
            saved = bytearray()
            clipped = False

            def drain():
                nonlocal clipped
                while True:
                    block = process.stderr.read(4096)
                    if not block:
                        break
                    sys.stderr.buffer.write(block)
                    sys.stderr.buffer.flush()
                    remaining = 65536 - len(saved)
                    saved.extend(block[:remaining])
                    clipped = clipped or len(block) > remaining
                process.stderr.close()

            reader = threading.Thread(target=drain)
            reader.start()
            result = process.wait()
            reader.join()
            return result, b"" if clipped else bytes(saved)
        return subprocess.call(command, env=env, cwd=self.repo), b""

    def checked(self, args, seconds, **kwargs):
        status, _ = self.run(args, seconds, **kwargs)
        if status:
            raise Failure("command failed", status)

    def read_json(self, path):
        try:
            if Path(str(path) + ".incomplete").exists():
                raise Failure("incomplete JSON capture")
            return json.loads(path.read_bytes())
        except (ValueError, OSError):
            raise Failure("invalid or unreadable JSON") from None

    def backend(self, root):
        prefix = "PARCHWELL_" + root.upper()
        backend = Path(require(prefix + "_BACKEND_CONFIG")).resolve()
        variables = Path(require(prefix + "_TFVARS_PATH")).resolve()
        if not backend.is_file() or not variables.is_file() or not variables.name.endswith(".tfvars.json"):
            raise Failure("backend and .tfvars.json files are required")
        text = backend.read_text()
        key_lines = re.findall(r"^\s*key\b[^\n]*$", text, re.MULTILINE)
        assignments = re.findall(r"^\s*key\s*=([^\n]*)$", text, re.MULTILINE)
        if len(key_lines) != 1 or len(assignments) != 1 or not re.fullmatch(r'\s*"[^"\n\\$]+"\s*', assignments[0]):
            raise Failure("backend requires one literal key assignment")
        key = assignments[0].strip()[1:-1]
        data = self.read_json(variables)
        persistent = data.get("persistent_backend_config", {}).get("key")
        if not isinstance(persistent, str) or not persistent or persistent == key:
            raise Failure("backend key must differ from persistent key")
        return backend, variables, key, persistent

    def terraform(self, operation, root):
        if root not in ("cluster", "ephemeral") or (operation == "destroy" and root != "ephemeral"):
            raise Failure("unsupported Terraform root or operation")
        backend, variables, key, persistent = self.backend(root)
        if operation != "destroy":
            other = self.backend("ephemeral" if root == "cluster" else "cluster")
            if key == other[2] or key == other[3] or other[2] == persistent:
                raise Failure("Terraform state keys must differ")
            label(require("PARCHWELL_RUN_ID"), 29)
            self.cluster_args()
        if not (self.repo / "infra" / root).is_dir():
            raise Failure("Terraform root is missing; C1 prerequisite")
        environment = dict(os.environ, TF_DATA_DIR=str(self.private / ("tfdata-" + root)))
        base = ["terraform", "-chdir=infra/" + root]
        init = [*base, "init", "-input=false", "-reconfigure", "-backend-config=" + str(backend)]
        plan = self.private / (root + ".tfplan")
        if operation == "init":
            self.checked(init, 300, env=environment)
        elif operation == "plan":
            args = [*base, "plan", "-input=false", "-lock-timeout=120s", "-var-file=" + str(variables), "-out=" + str(plan)]
            if root == "ephemeral":
                args += ["-var=run_id=" + require("PARCHWELL_RUN_ID")]
            self.checked(args, 600, env=environment)
        elif operation == "apply":
            if not plan.is_file():
                raise Failure("saved plan is missing")
            existing = self.private / "cluster-output.json"
            if existing.exists():
                self.validate_output("cluster", existing)
            self.checked([*base, "apply", "-input=false", "-lock-timeout=120s", str(plan)], 1800, env=environment)
            output = self.private / (root + "-output.json")
            self.checked([*base, "output", "-json"], 60, output=output, env=environment)
            self.validate_output(root, output)
        elif operation == "destroy":
            self.checked(init, 300, env=environment)
            self.checked([*base, "destroy", "-input=false", "-auto-approve", "-lock-timeout=120s", "-var-file=" + str(variables)], 2400, env=environment)
            state = self.private / "ephemeral-state-list.txt"
            self.checked([*base, "state", "list"], 60, output=state, env=environment)
            if state.read_text().strip():
                raise Failure("ephemeral state is not empty", 1)
            print("ephemeral_state_empty")
        else:
            raise Failure("unsupported Terraform operation")

    def validate_output(self, root, output):
        data = self.read_json(output)
        names = ("aks", "workload_identity") if root == "cluster" else ("eventhubs", "blob", "private_endpoint_ips")
        if not isinstance(data, dict) or any(not isinstance(data.get(name), dict) or not isinstance(data[name].get("value"), dict) for name in names):
            raise Failure("Terraform output shape mismatch")
        if root == "cluster" and data["aks"]["value"].get("id") != require("PARCHWELL_CLUSTER_RESOURCE_ID"):
            raise Failure("AKS output does not match supplied resource ID")

    def cluster_args(self):
        resource = require("PARCHWELL_CLUSTER_RESOURCE_ID")
        match = re.fullmatch(r"/subscriptions/([^/\s]+)/resourceGroups/([^/\s]+)/providers/Microsoft.ContainerService/managedClusters/([^/\s]+)", resource, re.IGNORECASE)
        if not match:
            raise Failure("invalid AKS resource ID")
        subscription, group, name = match.groups()
        return ["--resource-group", group, "--name", name, "--subscription", subscription]

    def show_cluster(self, end):
        output = self.private / "aks-show.json"
        status, errors = self.run(["az", "aks", "show", *self.cluster_args(), "-o", "json"], 60, output=output, end=end, errors=True)
        if status:
            if status not in (124, 125) and re.search(rb"(?m)^ERROR: \(ResourceNotFound\) ", errors):
                return None
            raise Failure("AKS read failed", status)
        data = self.read_json(output)
        if not isinstance(data, dict) or data.get("id") != require("PARCHWELL_CLUSTER_RESOURCE_ID"):
            raise Failure("AKS read ID mismatch")
        power = data.get("powerState", {}).get("code")
        provisioning = data.get("provisioningState")
        if not isinstance(power, str) or not isinstance(provisioning, str):
            raise Failure("invalid AKS state")
        return power, provisioning

    def cluster(self, start):
        end = time.monotonic() + (3600 if start else 1800)
        stopped_at = None
        last_start = None
        submissions = 0
        try:
            for _ in range(120 if start else 60):
                if time.monotonic() >= end:
                    break
                state = self.show_cluster(end)
                if state is None:
                    print("cluster_absent_create_on_apply" if start else "cluster_absent")
                    return
                power, provisioning = state
                if start and state == ("Running", "Succeeded"):
                    print("aks_running")
                    return
                if not start and power == "Stopped":
                    print("aks_stopped")
                    return
                if power not in ("Running", "Stopped", "Starting", "Stopping") or provisioning in ("Failed", "Canceled"):
                    raise Failure("unsupported AKS state", 1)
                now = time.monotonic()
                if start and state == ("Stopped", "Succeeded"):
                    if stopped_at is None:
                        stopped_at = now
                    if now - stopped_at >= 1800 and submissions < 7 and (last_start is None or now - last_start >= 300):
                        last_start = now
                        submissions += 1
                        status, _ = self.run(["az", "aks", "start", *self.cluster_args(), "--no-wait"], 60, end=end)
                        # Readback decides whether a failed start submission can
                        # be retried; it never permits a retry while Starting.
                        if status:
                            print("aks_start_submission_failed", file=sys.stderr)
                elif start:
                    stopped_at = None
                elif state == ("Running", "Succeeded") and submissions == 0:
                    submissions += 1
                    self.checked(["az", "aks", "stop", *self.cluster_args(), "--no-wait"], 60, end=end)
                remaining = end - time.monotonic()
                if remaining > 0:
                    time.sleep(min(30, remaining))
            raise Failure("AKS polling exhausted", 124)
        except Failure:
            print("aks_start_incomplete" if start else "aks_stop_incomplete", file=sys.stderr)
            raise

    def credentials(self):
        config = self.private / "kubeconfig"
        self.checked(["az", "aks", "get-credentials", *self.cluster_args(), "--file", str(config), "--overwrite-existing"], 60)
        self.checked(["kubelogin", "convert-kubeconfig", "-l", "azurecli", "--kubeconfig", str(config)], 60)
        if os.environ.get("GITHUB_ENV"):
            with open(os.environ["GITHUB_ENV"], "a") as stream:
                stream.write("KUBECONFIG=" + str(config) + "\n")
        print("KUBECONFIG=" + str(config))

    def kubectl(self, *args):
        return ["kubectl", *args, "--request-timeout=30s"]

    def reset_namespace(self):
        end = time.monotonic() + 360
        self.checked(self.kubectl("delete", "namespace", "parchwell", "--ignore-not-found", "--wait=false"), 30, end=end)
        output = self.private / "namespace.json"
        self.checked(self.kubectl("get", "namespace", "parchwell", "--ignore-not-found", "-o", "json"), 30, output=output, end=end)
        if output.read_bytes().strip():
            self.checked(self.kubectl("wait", "--for=delete", "namespace/parchwell", "--timeout=300s"), 300, end=end)
        self.checked(self.kubectl("create", "namespace", "parchwell"), 30, end=end)

    def wait_job(self, job, seconds):
        label(job)
        if not re.fullmatch(r"[1-9][0-9]*", seconds):
            raise Failure("invalid Job deadline")
        budget = int(seconds) + 60
        end = time.monotonic() + budget
        output = self.private / "job.json"
        for _ in range(math.ceil(budget / 5)):
            status, _ = self.run(self.kubectl("get", "job", job, "-n", "parchwell", "--ignore-not-found", "-o", "json"), 30, output=output, end=end)
            if status:
                raise Failure("Job read failed", 124 if status == 124 else 2)
            if output.read_bytes().strip():
                data = self.read_json(output)
                conditions = data.get("status", {}).get("conditions", [])
                if not isinstance(conditions, list) or any(not isinstance(item, dict) for item in conditions):
                    raise Failure("invalid Job conditions")
                if any(item.get("type") == "Failed" and item.get("status") == "True" for item in conditions):
                    raise Failure("Job Failed", 1)
                if any(item.get("type") == "Complete" and item.get("status") == "True" for item in conditions):
                    return
            remaining = end - time.monotonic()
            if remaining <= 0:
                break
            time.sleep(min(5, remaining))
        raise Failure("Job deadline exceeded", 124)

    def collect_job(self, job):
        label(job)
        artifacts = Path(require("PARCHWELL_ARTIFACT_DIRECTORY")).resolve()
        if artifacts == self.private or artifacts in self.private.parents or self.private in artifacts.parents:
            raise Failure("artifact and private directories must be separate")
        destination = artifacts / job
        destination.mkdir(parents=True, exist_ok=True)
        end = time.monotonic() + 180
        gaps = []
        logs = []
        pods = []
        output = destination / "pods.json"
        status, _ = self.run(self.kubectl("get", "pods", "-n", "parchwell", "-l", "job-name=" + job, "-o", "json"), 30, output=output, end=end)
        if status:
            gaps.append({"kind": "pod_read_failed", "status": status})
        else:
            try:
                pods = self.read_json(output)["items"]
                if not isinstance(pods, list):
                    raise ValueError()
                pods = sorted(pods, key=lambda pod: label(pod["metadata"]["name"]))
            except (Failure, KeyError, TypeError, ValueError):
                gaps.append({"kind": "invalid_pod_json"})
                pods = []
        for pod in pods[4:]:
            gaps.append({"kind": "omitted_pod", "pod": pod["metadata"]["name"]})
        if not pods:
            gaps.append({"kind": "no_pods"})
        selections = []
        for pod in pods[:4]:
            name = pod["metadata"]["name"]
            try:
                spec = pod["spec"]
                containers = sorted([(label(c["name"]), kind) for kind in ("initContainers", "containers") for c in spec.get(kind, [])])
            except (Failure, KeyError, TypeError, AttributeError):
                gaps.append({"kind": "invalid_containers", "pod": name})
                continue
            for container, kind in containers[4:]:
                gaps.append({"kind": "omitted_container", "pod": name, "container": container})
            selections.append((name, containers[:4]))
        total = output.stat().st_size if output.exists() else 0
        # Reserve the already-known omissions and all remaining bounded log
        # failure records before spending the collection's total byte budget.
        status_reserve = len(json.dumps(gaps, indent=2).encode()) + 16384
        for name, containers in selections:
            for container, kind in containers:
                path = destination / (name + "." + kind + "." + container + ".log")
                if total + MIB + status_reserve > 24 * MIB:
                    gaps.append({"kind": "total_byte_limit", "pod": name, "container": container})
                    continue
                status, _ = self.run(self.kubectl("logs", "-n", "parchwell", name, "-c", container, "--timestamps=false", "--limit-bytes=1048577"), 30, output=path, maximum=MIB, end=end)
                total += path.stat().st_size if path.exists() else 0
                logs.append((name, container, kind, path, status))
                if status:
                    gaps.append({"kind": "truncated_log" if status == 125 else "log_read_failed", "pod": name, "container": container, "status": status})
        smoke = job == label(require("PARCHWELL_RUN_ID"), 29) + "-smoke"
        if smoke:
            smoke_logs = [entry for entry in logs if entry[1:3] == ("smoke", "containers")]
            if len(pods) != 1 or len(smoke_logs) != 1:
                gaps.append({"kind": "smoke_pod_or_container_missing_or_extra"})
            elif smoke_logs[0][4] != 0 or smoke_logs[0][3].read_bytes() != MARKER:
                gaps.append({"kind": "smoke_marker_mismatch"})
        status_file = destination / "collection-status.json"
        status_file.write_text(json.dumps({"complete": not gaps, "smoke_marker_valid": smoke and not gaps, "gaps": gaps}, indent=2) + "\n")
        # The status document carries incompleteness; only approved evidence
        # files are exposed to the artifact uploader.
        for path in destination.glob("*.incomplete"):
            path.unlink()
        if gaps:
            raise Failure("collection has gaps", 1)

    def smoke(self, install):
        if install:
            job = label(require("PARCHWELL_RUN_ID"), 29) + "-smoke"
            self.checked(["helm", "install", "parchwell-smoke", "charts/smoke", "--namespace", "parchwell", "--set-string", "jobName=" + job, "--wait=false"], 180)
        else:
            self.checked(["helm", "uninstall", "parchwell-smoke", "--namespace", "parchwell", "--ignore-not-found", "--wait", "--timeout", "120s"], 180)


def main(argv):
    try:
        if not argv:
            raise Failure("lifecycle command is required")
        operation, *args = argv
        expected = 2 if operation == "wait-job" else 1 if operation == "collect-job" else 0
        if len(args) != expected:
            raise Failure("invalid command arguments")
        lifecycle = Lifecycle()
        if operation in {action + "-" + root for action in ("init", "plan", "apply") for root in ("cluster", "ephemeral")} or operation == "destroy-ephemeral":
            action, root = operation.split("-", 1)
            lifecycle.terraform(action, root)
        elif operation in ("start-cluster", "stop-cluster"):
            lifecycle.cluster(operation == "start-cluster")
        elif operation == "credentials":
            lifecycle.credentials()
        elif operation == "reset-namespace":
            lifecycle.reset_namespace()
        elif operation == "wait-job":
            lifecycle.wait_job(*args)
        elif operation == "collect-job":
            lifecycle.collect_job(*args)
        elif operation in ("install-smoke", "uninstall-smoke"):
            lifecycle.smoke(operation == "install-smoke")
        else:
            raise Failure("unknown lifecycle command")
        return 0
    except Failure as error:
        print(str(error), file=sys.stderr)
        return error.code
    except (OSError, ValueError, TypeError, AttributeError) as error:
        print("invalid lifecycle input: " + type(error).__name__, file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
PYTHON

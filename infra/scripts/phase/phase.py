#!/usr/bin/env python3
"""Run scenario phase commands while keeping private inputs separate from evidence."""

import json
import os
from pathlib import Path
import re
import socket
import subprocess
import sys
import tempfile
import time
from urllib.parse import urlsplit

MIB = 1024 * 1024
SCENARIOS = {"A01": {"probe": "C0", "C0": "C0", "C3": "C3"},
             "B04": {"C0": "C0", "C1": "C1"}}
MODES = ("start-line", "loadgen", "verify")
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
    if selected not in SCENARIOS:
        raise Failure("unsupported scenario")
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


def unwrap(value):
    return value["value"] if isinstance(value, dict) and "value" in value else value


def job_name(run, phase, mode, repeat=None):
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

    def read(self, name):
        return strict_json((self.private / name).read_bytes())

    def state(self):
        return strict_json((self.artifacts / "phase-status.json").read_bytes())

    def save_state(self, state):
        write_json(self.artifacts / "phase-status.json", state)

    def run(self, args, seconds, output=None, maximum=4 * MIB):
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
        scenario()
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
        if phase not in SCENARIOS[selected]:
            raise Failure("unsupported phase")
        temporary, environment = require("RUNNER_TEMP"), require("GITHUB_ENV")
        run = f"{selected.lower()}-{require('GITHUB_RUN_ID')}-{require('GITHUB_RUN_ATTEMPT')}"
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
        write_json(private / "phase.json", {"scenario": selected, "phase": phase, "configuration": SCENARIOS[selected][phase],
                                           "run_id": run, "source_commit": sha})
        write_json(artifacts / "phase-status.json", {"phase": phase, "run_id": run,
                   "modes": [], "candidate": None, "safety_stop": False})
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
        for index, (worker, project) in enumerate((("loadgen", "LoadGen"), ("verifier", "Verifier"))):
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
            if self.run(self.image_command(worker, reference, acr), 60, output=path, maximum=4096):
                raise Failure("image readback failed: " + worker, 1)
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

    def values(self, mode, repeat):
        phase = self.read("phase.json")
        ephemeral = self.read("ephemeral-output.json")
        persistent = self.read("persistent-output.json")
        accounts = unwrap(self.read("cluster-output.json")["workload_identity"])["service_accounts"]
        runtime = {"run_id": phase["run_id"], "phase": phase["phase"], "mode": mode,
                   "source_commit": phase["source_commit"],
                   "loadgen_image": phase["images"]["loadgen"]["reference"],
                   "verifier_image": phase["images"]["verifier"]["reference"],
                   "service_accounts": {role: {key: accounts[role][key] for key in ("name", "client_id", "subject")}
                                        for role in ("loadgen", "verifier")},
                   "eventhubs": unwrap(ephemeral["eventhubs"]), "blob": unwrap(ephemeral["blob"]),
                   "eventhubs_ip": unwrap(ephemeral["private_endpoint_ips"])["eventhubs"],
                   "blob_ip": unwrap(ephemeral["private_endpoint_ips"])["blob"],
                   "application_insights_connection_string": unwrap(persistent["application_insights_connection_string"])}
        if mode != "judge":
            runtime.update(repeat=repeat, loadgen_job_name=job_name(phase["run_id"], phase["phase"], "loadgen", repeat))
        return {"scenario_id": phase["scenario"], "configuration_id": phase["configuration"], "runtime": runtime}

    def render(self, mode, repeat):
        phase = self.read("phase.json")
        job = job_name(phase["run_id"], phase["phase"], mode, repeat)
        values = self.private / f"values-{job}.json"
        rendered = self.private / f"rendered-{job}.yaml"
        write_json(values, self.values(mode, repeat))
        self.render_code = self.run(["helm", "template", "parchwell", "charts/parchwell", "--values", str(values)],
                                    120, output=rendered, maximum=4 * MIB)
        if self.render_code:
            return None
        text = rendered.read_text()
        deadlines = re.findall(r"^  activeDeadlineSeconds: ([1-9][0-9]*)$", text, re.MULTILINE)
        if text.splitlines().count("kind: Job") != 1 or text.splitlines().count(f'  name: "{job}"') != 1 or len(deadlines) != 1:
            return None
        return int(deadlines[0])

    def run_mode(self, mode, repeat):
        phase = self.read("phase.json")
        job = job_name(phase["run_id"], phase["phase"], mode, repeat)
        state = self.state()
        record = {"job": job, "repeat": repeat, "mode": mode,
                  **{step: None for step in ("render", "apply", "wait", "collect", "delete", "confirm")},
                  "deadline": None, "outcome": "not_attempted"}
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
                return record
            deadline = self.render(mode, repeat)
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
                        empty = strict_json(confirmation.read_bytes()).get("items") == []
                    except (Failure, OSError, ValueError, AttributeError):
                        pass
                if deleted or confirmed or not empty:
                    state["safety_stop"] = True
                    print(f"safety_stop: pods of {job} may still run", flush=True)
            save()
            return record
        finally:
            print("::endgroup::", flush=True)

    def run_repeats(self):
        records = [self.run_mode(mode, repeat) for repeat in SLOTS for mode in MODES]
        return int(any(record["outcome"] in BAD_OUTCOMES for record in records))

    def judge(self):
        return int(self.run_mode("judge", None)["outcome"] in BAD_OUTCOMES)

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
        return self.run(["kubectl", "delete", "jobs", "-n", "parchwell", "-l",
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
            if state["candidate"] is not None:
                lines += [f"candidate status: {state['candidate']['status']}", f"candidate gate: {state['candidate']['gate']}"]
        text = "\n".join(lines) + "\n"
        print(text, end="")
        with open(require("GITHUB_STEP_SUMMARY"), "a", encoding="utf-8") as stream:
            stream.write(text)
        return int(not passed)


def main(argv):
    commands = {"prepare": (1, "prepare"), "prepare-stop": (0, "prepare_stop"), "allow-c3": (0, "allow_c3"),
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

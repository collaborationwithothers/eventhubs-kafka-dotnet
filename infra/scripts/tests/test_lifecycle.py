"""Synthetic executable transcripts; never fall through to installed cloud tools."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import types
import unittest
from unittest.mock import patch

SOURCE = Path(__file__).resolve().parents[1] / "lifecycle"
RESOURCE = "/subscriptions/synthetic/resourceGroups/synthetic/providers/Microsoft.ContainerService/managedClusters/synthetic"
STUB = r'''#!/usr/bin/env python3
import json, os, pathlib, sys
name = pathlib.Path(sys.argv[0]).name
queue = pathlib.Path(os.environ["STUB_QUEUE"])
responses = json.loads(queue.read_text())
with open(os.environ["STUB_TRANSCRIPT"], "a") as out:
    out.write(json.dumps({"tool": name, "args": sys.argv[1:], "data": os.environ.get("TF_DATA_DIR"), "kubeconfig": os.environ.get("KUBECONFIG")}) + "\n")
if not responses:
    sys.stderr.write("UNEXPECTED STUB CALL\n")
    sys.exit(97)
response = responses.pop(0)
queue.write_text(json.dumps(responses))
if name != response["tool"] or any(arg not in sys.argv[1:] for arg in response.get("contains", [])):
    sys.stderr.write("UNEXPECTED STUB ARGUMENTS\n")
    sys.exit(98)
if name == "terraform" and "plan" in sys.argv:
    for arg in sys.argv:
        if arg.startswith("-out="):
            pathlib.Path(arg[5:]).write_text("synthetic plan")
if "bytes" in response:
    sys.stdout.buffer.write(b"x" * response["bytes"])
else:
    sys.stdout.write(response.get("stdout", ""))
sys.stderr.write(response.get("stderr", ""))
sys.exit(response.get("exit", 0))
'''


def response(tool, *contains, stdout="", code=0, stderr="", **extra):
    return dict(tool=tool, contains=list(contains), stdout=stdout, exit=code, stderr=stderr, **extra)


def pod(name="pod-a", containers=None):
    return {"metadata": {"name": name}, "spec": {"containers": containers or [{"name": "smoke"}]}}


class Clock:
    def __init__(self):
        self.now = 0
    def monotonic(self):
        return self.now
    def sleep(self, seconds):
        self.now += seconds


class LifecycleTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.repo = self.root / "repo"
        self.scripts = self.repo / "infra/scripts/lifecycle"
        self.scripts.mkdir(parents=True)
        for name in ("command.py", "common.sh", "lifecycle.sh"):
            shutil.copyfile(SOURCE / name, self.scripts / name)
        for name in ("cluster", "ephemeral"):
            (self.repo / "infra" / name).mkdir()
        self.private = self.root / "private"
        self.artifacts = self.root / "public"
        self.bin = self.root / "bin"
        self.bin.mkdir()
        for name in ("az", "terraform", "kubectl", "helm", "kubelogin"):
            path = self.bin / name
            path.write_text(STUB)
            path.chmod(0o700)
        self.queue = self.root / "queue.json"
        self.queue.write_text("[]")
        self.transcript = self.root / "transcript.jsonl"
        self.transcript.write_text("")
        self.environment = dict(os.environ, PATH=str(self.bin) + os.pathsep + os.environ["PATH"],
            STUB_QUEUE=str(self.queue), STUB_TRANSCRIPT=str(self.transcript),
            PARCHWELL_REPOSITORY=str(self.repo), PARCHWELL_PRIVATE_DIRECTORY=str(self.private),
            PARCHWELL_ARTIFACT_DIRECTORY=str(self.artifacts), PARCHWELL_RUN_ID="synthetic-c2",
            PARCHWELL_CLUSTER_RESOURCE_ID=RESOURCE)
        for root in ("cluster", "ephemeral"):
            backend = self.root / (root + ".hcl")
            backend.write_text('key = "synthetic-' + root + '"\n')
            variables = self.root / (root + ".tfvars.json")
            variables.write_text(json.dumps({"persistent_backend_config": {"key": "synthetic-persistent"}}))
            self.environment["PARCHWELL_" + root.upper() + "_BACKEND_CONFIG"] = str(backend)
            self.environment["PARCHWELL_" + root.upper() + "_TFVARS_PATH"] = str(variables)
        self.env_patch = patch.dict(os.environ, self.environment, clear=True)
        self.env_patch.start()
        self.addCleanup(self.env_patch.stop)
        source = (SOURCE / "lifecycle.sh").read_text().split("<<'PYTHON'\n", 1)[1].rsplit("\nPYTHON", 1)[0]
        self.module = types.ModuleType("synthetic_lifecycle")
        exec(compile(source, str(SOURCE / "lifecycle.sh"), "exec"), self.module.__dict__)
        self.clock = Clock()
        self.module.time = self.clock
        self.times = []
        original = self.module.Lifecycle.run
        times = self.times
        clock = self.clock
        def recording(instance, args, *rest, **kwargs):
            times.append((clock.now, list(args), rest, kwargs))
            return original(instance, args, *rest, **kwargs)
        self.module.Lifecycle.run = recording

    def script(self, *commands):
        self.queue.write_text(json.dumps(commands))

    def run_lifecycle(self, *args):
        return self.module.main(list(args))

    def calls(self):
        return [json.loads(line) for line in self.transcript.read_text().splitlines()]

    def assert_consumed(self):
        self.assertEqual([], json.loads(self.queue.read_text()))
        self.assertTrue(all(call["tool"] in ("az", "terraform", "kubectl", "helm", "kubelogin") for call in self.calls()))

    def aks(self, power, provisioning="Succeeded", resource=RESOURCE):
        return response("az", "show", stdout=json.dumps({"id": resource, "powerState": {"code": power}, "provisioningState": provisioning}))

    def collect(self, marker):
        self.script(response("kubectl", "get", "pods", stdout=json.dumps({"items": [pod()]})),
                    response("kubectl", "logs", "--timestamps=false", "--limit-bytes=1048577", stdout=marker.decode()))
        status = self.run_lifecycle("collect-job", "synthetic-c2-smoke")
        report = json.loads((self.artifacts / "synthetic-c2-smoke/collection-status.json").read_text())
        self.assert_consumed()
        self.assertEqual(status == 0, report["complete"])
        return status

    def test_marker_exact(self):
        self.assertEqual(0, self.collect(b"parchwell-cluster-smoke-ok\n"))

    def test_marker_wrong(self):
        self.assertEqual(1, self.collect(b"parchwell-cluster-smoke-wrong\n"))

    def test_marker_prefix(self):
        self.assertEqual(1, self.collect(b"prefix parchwell-cluster-smoke-ok\n"))

    def test_marker_timestamp(self):
        self.assertEqual(1, self.collect(b"2026-10-24T00:00:00Z parchwell-cluster-smoke-ok\n"))

    def test_marker_truncated(self):
        self.assertEqual(1, self.collect(b"parchwell-cluster-smoke-o"))

    def terminal(self, conditions, expected):
        self.script(response("kubectl", "get", "job", "--request-timeout=30s", stdout=json.dumps({"status": {"conditions": conditions}})))
        self.assertEqual(expected, self.run_lifecycle("wait-job", "synthetic-c2-smoke", "120"))
        self.assert_consumed()

    def test_job_complete(self):
        self.terminal([{"type": "Complete", "status": "True"}], 0)

    def test_job_failed(self):
        self.terminal([{"type": "Failed", "status": "True"}], 1)

    def test_job_both_failed_wins(self):
        self.terminal([{"type": "Complete", "status": "True"}, {"type": "Failed", "status": "True"}], 1)

    def test_job_missing_and_pending_timeout(self):
        self.script(*[response("kubectl", "get", "--ignore-not-found", stdout="" if i == 0 else '{"status": {}}') for i in range(36)])
        self.assertEqual(124, self.run_lifecycle("wait-job", "synthetic-c2-smoke", "120"))
        self.assertEqual(180, self.clock.now)
        self.assertEqual(36, len(self.calls()))
        self.assert_consumed()

    def test_terraform_full_sequence_partial_failure_and_independent_destroy(self):
        cluster_output = json.dumps({"aks": {"value": {"id": RESOURCE.lower()}}, "workload_identity": {"value": {}}})
        self.script(response("terraform", "init"), response("az", "show", code=3, stderr="ERROR: (ResourceGroupNotFound) synthetic group absent\n"),
            response("az", "show", code=3, stderr="ERROR: (ResourceNotFound) synthetic cluster absent\n"), response("terraform", "plan"), response("terraform", "apply"),
            response("terraform", "output", stdout=cluster_output), response("terraform", "init"), response("terraform", "plan"),
            response("terraform", "apply", code=9), response("terraform", "init"), response("terraform", "destroy"),
            response("terraform", "state", "list"), self.aks("Running", resource=RESOURCE.lower()), response("az", "stop", "--no-wait"), self.aks("Stopped", resource=RESOURCE.lower()))
        # Exercise the Bash entry and common.sh root resolution once as well.
        result = subprocess.run(["bash", str(self.scripts / "lifecycle.sh"), "init-cluster"], env=os.environ, capture_output=True)
        self.assertEqual(0, result.returncode, result.stderr)
        for command in ("start-cluster", "start-cluster", "plan-cluster", "apply-cluster", "init-ephemeral", "plan-ephemeral"):
            self.assertEqual(0, self.run_lifecycle(command))
        self.assertEqual(9, self.run_lifecycle("apply-ephemeral"))
        del os.environ["PARCHWELL_CLUSTER_BACKEND_CONFIG"]
        del os.environ["PARCHWELL_CLUSTER_TFVARS_PATH"]
        del os.environ["PARCHWELL_RUN_ID"]
        self.assertEqual(0, self.run_lifecycle("destroy-ephemeral"))
        self.assertEqual(0, self.run_lifecycle("stop-cluster"))
        calls = self.calls()
        self.assertEqual(1, sum(c["tool"] == "az" and "stop" in c["args"] for c in calls))
        self.assertTrue(all("-chdir=infra/persistent" not in c["args"] for c in calls))
        self.assertTrue(all("destroy" not in c["args"] or "-chdir=infra/ephemeral" in c["args"] for c in calls))
        self.assertIn("-var=run_id=synthetic-c2", calls[7]["args"])
        self.assertEqual(str(self.private / "tfdata-cluster"), calls[0]["data"])
        self.assertEqual(str(self.private / "tfdata-ephemeral"), calls[6]["data"])
        self.assertEqual(0o600, (self.private / "cluster-output.json").stat().st_mode & 0o777)
        self.assertFalse(self.artifacts.exists())
        self.assert_consumed()

    def test_backend_collision_fails_before_terraform(self):
        (self.root / "ephemeral.hcl").write_text('key = "synthetic-cluster"\n')
        self.assertEqual(2, self.run_lifecycle("apply-ephemeral"))
        self.assertEqual([], self.calls())

    def test_cluster_resource_id_mismatch_fails_closed(self):
        self.private.mkdir()
        (self.private / "cluster.tfplan").write_text("synthetic")
        (self.private / "cluster-output.json").write_text(json.dumps({"aks": {"value": {"id": RESOURCE + "-wrong"}}, "workload_identity": {"value": {}}}))
        self.assertEqual(2, self.run_lifecycle("apply-cluster"))
        self.assertEqual([], self.calls())
        self.script(response("az", "show", stdout=json.dumps({"id": RESOURCE + "-wrong", "powerState": {"code": "Running"}, "provisioningState": "Succeeded"})))
        self.assertEqual(2, self.run_lifecycle("start-cluster"))
        self.assertEqual(1, len(self.calls()))
        self.assert_consumed()

    def test_aks_authorization_error_does_not_mean_absent(self):
        self.script(response("az", "show", code=4, stderr="ERROR: (AuthorizationFailed) 403\n"))
        self.assertEqual(4, self.run_lifecycle("start-cluster"))
        self.assertFalse(any("start" in c["args"] for c in self.calls()))
        self.assert_consumed()

    def test_aks_immediate_start_and_bounded_retries(self):
        calls = []
        for index in range(120):
            calls.append(self.aks("Stopped") if index < 110 else self.aks("Starting", "Starting"))
            if index <= 60 and index % 10 == 0:
                calls.append(response("az", "start", "--no-wait", code=1))
        self.script(*calls)
        self.assertEqual(124, self.run_lifecycle("start-cluster"))
        starts = [now for now, args, _, _ in self.times if args[0:3] == ["az", "aks", "start"]]
        self.assertEqual([0, 300, 600, 900, 1200, 1500, 1800], starts)
        self.assertEqual(120, sum(args[0:3] == ["az", "aks", "show"] for _, args, _, _ in self.times))
        self.assertTrue(all(b - a >= 300 for a, b in zip(starts, starts[1:])))
        self.assertEqual(3600, self.clock.now)
        self.assert_consumed()

    def test_aks_start_waits_for_starting_readback(self):
        self.script(self.aks("Stopped"), response("az", "start", "--no-wait", code=1),
            *[self.aks("Starting", "Starting") for _ in range(10)],
            self.aks("Stopped"), response("az", "start", "--no-wait"), self.aks("Running"))
        self.assertEqual(0, self.run_lifecycle("start-cluster"))
        starts = [now for now, args, _, _ in self.times if args[0:3] == ["az", "aks", "start"]]
        self.assertEqual([0, 330], starts)
        self.assertEqual(360, self.clock.now)
        self.assert_consumed()

    def test_aks_stop_submits_immediately_and_waits_for_stopped(self):
        self.script(self.aks("Running"), response("az", "stop", "--no-wait"),
            self.aks("Stopping", "Stopping"), self.aks("Stopped"))
        self.assertEqual(0, self.run_lifecycle("stop-cluster"))
        stops = [now for now, args, _, _ in self.times if args[0:3] == ["az", "aks", "stop"]]
        self.assertEqual([0], stops)
        self.assertEqual(60, self.clock.now)
        self.assertFalse(any(args[0:3] == ["az", "aks", "start"] for _, args, _, _ in self.times))
        self.assert_consumed()

    def test_aks_running_zero_starts_and_credentials(self):
        self.script(self.aks("Running"),
            response("az", "get-credentials", "--overwrite-existing"), response("kubelogin", "convert-kubeconfig", "-l", "azurecli"))
        self.assertEqual(0, self.run_lifecycle("start-cluster"))
        environment = self.root / "github-env"
        os.environ["GITHUB_ENV"] = str(environment)
        self.assertEqual(0, self.run_lifecycle("credentials"))
        self.assertEqual("KUBECONFIG=" + str(self.private / "kubeconfig") + "\n", environment.read_text())
        self.assertFalse(any("start" in c["args"] or "--admin" in c["args"] or "--tenant-id" in c["args"] for c in self.calls()))
        self.assert_consumed()

    def test_namespace_delete_wait_create_then_smoke_install_uninstall(self):
        self.script(response("kubectl", "delete", "--wait=false"), response("kubectl", "get", stdout="{}"),
            response("kubectl", "wait", "--for=delete", "--timeout=300s"), response("kubectl", "create"),
            response("helm", "install", "jobName=synthetic-c2-smoke", "--wait=false"),
            response("helm", "uninstall", "--ignore-not-found", "--timeout", "120s"))
        self.assertEqual(0, self.run_lifecycle("reset-namespace"))
        self.assertEqual(0, self.run_lifecycle("install-smoke"))
        self.assertEqual(0, self.run_lifecycle("uninstall-smoke"))
        self.assertTrue(all("--request-timeout=30s" in c["args"] for c in self.calls() if c["tool"] == "kubectl"))
        self.assert_consumed()

    def test_namespace_stuck_never_creates_or_installs(self):
        self.script(response("kubectl", "delete"), response("kubectl", "get", stdout="{}"), response("kubectl", "wait", code=1))
        self.assertEqual(1, self.run_lifecycle("reset-namespace"))
        self.assertFalse(any("create" in c["args"] or c["tool"] == "helm" for c in self.calls()))
        self.assert_consumed()

    def test_collection_caps_and_independent_remaining_logs(self):
        pods = [pod("pod-" + str(i)) for i in range(5)]
        pods[0]["spec"]["containers"] = [{"name": "container-" + str(i)} for i in range(5)]
        pods[0]["spec"]["initContainers"] = [{"name": "a-init"}]
        self.script(response("kubectl", "get", stdout=json.dumps({"items": list(reversed(pods))})),
            response("kubectl", "logs", "a-init", code=7), response("kubectl", "logs", "container-0", bytes=1048577),
            response("kubectl", "logs", "container-1", stdout="independent\n"), response("kubectl", "logs", "container-2"),
            *[response("kubectl", "logs", "pod-" + str(i)) for i in range(1, 4)])
        self.assertEqual(1, self.run_lifecycle("collect-job", "synthetic-c2-worker"))
        directory = self.artifacts / "synthetic-c2-worker"
        report = json.loads((directory / "collection-status.json").read_text())
        self.assertEqual(["omitted_pod", "omitted_container", "omitted_container", "log_read_failed", "truncated_log"], [g["kind"] for g in report["gaps"]])
        self.assertEqual(1048576, (directory / "pod-0.containers.container-0.log").stat().st_size)
        self.assertEqual("independent\n", (directory / "pod-0.containers.container-1.log").read_text())
        self.assertEqual([], list(directory.glob("*.incomplete")))
        self.assertLess(sum(path.stat().st_size for path in directory.iterdir()), 24 * 1048576)
        self.assert_consumed()

    def test_destroy_state_read_failure_is_not_empty(self):
        self.script(response("terraform", "init"), response("terraform", "destroy"), response("terraform", "state", "list", code=5))
        self.assertEqual(5, self.run_lifecycle("destroy-ephemeral"))
        self.assert_consumed()

    def test_invalid_job_json(self):
        self.script(response("kubectl", "get", stdout="not JSON"))
        self.assertEqual(2, self.run_lifecycle("wait-job", "synthetic-c2-smoke", "120"))
        self.assert_consumed()


if __name__ == "__main__":
    unittest.main()

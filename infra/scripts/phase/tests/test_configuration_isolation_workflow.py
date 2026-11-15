"""Offline contracts for the configuration isolation diagnostic workflow."""
from pathlib import Path
import ast
import contextlib
import io
import json
import os
import re
import time
import tempfile
import subprocess
import threading
from types import SimpleNamespace
import unittest
from unittest import mock

import yaml

REPO = Path(__file__).resolve().parents[4]
WORKFLOW = REPO / '.github/workflows/configuration-isolation-smoke.yml'
MIRROR = REPO / 'docs/operations/workflows/configuration-isolation-smoke.yml.txt'


class ConfigurationIsolationWorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.text = WORKFLOW.read_text()
        cls.workflow = yaml.safe_load(cls.text)
        cls.job = cls.workflow['jobs']['smoke']
        cls.steps = cls.job['steps']
        cls.by_id = {step['id']: step for step in cls.steps if 'id' in step}

    def test_mirror_trigger_concurrency_and_protected_job(self):
        self.assertEqual(WORKFLOW.read_bytes(), MIRROR.read_bytes())
        self.assertEqual({'workflow_dispatch': None}, self.workflow[True])
        self.assertEqual({'group': 'parchwell-ephemeral', 'queue': 'max', 'cancel-in-progress': False}, self.workflow['concurrency'])
        self.assertEqual(['smoke'], list(self.workflow['jobs']))
        self.assertEqual('parchwell-scenarios', self.job['environment'])
        self.assertLess(next(i for i, s in enumerate(self.steps) if s.get('id') == 'guard'),
                        next(i for i, s in enumerate(self.steps) if s.get('id') == 'login'))
        self.assertIn('refs/heads/main', self.by_id['guard']['run'])
        self.assertEqual('B04', self.job['env']['PARCHWELL_SCENARIO'])

    def test_sequence_retains_sentinel_and_has_no_judge_or_candidate(self):
        ids = [s.get('id') for s in self.steps]
        required = ['ephemeral_init', 'initial_cleanup', 'ephemeral_plan', 'ephemeral_apply', 'dns', 'c0', 'c0_settings', 'c0_cleanup', 'off_transition', 'on_transition', 'c1_settings', 'c1', 'delete_workload', 'destroy', 'stop']
        self.assertEqual(sorted(ids.index(x) for x in required), [ids.index(x) for x in required])
        self.assertEqual('bash infra/scripts/lifecycle/lifecycle.sh destroy-ephemeral', self.by_id['initial_cleanup']['run'])
        self.assertIn('deactivate-namespace', self.by_id['off_transition']['run'])
        self.assertIn('activate-namespace', self.by_id['on_transition']['run'])
        self.assertIn("'/C0/r1/start-line.json'", self.by_id['c0']['run'])
        self.assertIn("'/C0/r1/start-line.json'", self.by_id['c1_settings']['run'])
        self.assertIn("sentinel == (p / 'sentinel-before').read_bytes()", self.by_id['c1_settings']['run'])
        self.assertNotIn('phase.sh judge', self.text)
        self.assertNotIn('phase.sh candidate', self.text)
        for entry in ('c0', 'c1'):
            self.assertIn("one('start-line')", self.by_id[entry]['run'])
            self.assertIn("pool.submit(one, 'loadgen')", self.by_id[entry]['run'])
            self.assertIn("one('verify')", self.by_id[entry]['run'])
            self.assertIn('pod_connections()', self.by_id[entry]['run'])
            self.assertIn('confirm_loadgen_delivery()', self.by_id[entry]['run'])
            self.assertIn("if row['delivered'] is True:", self.by_id[entry]['run'])

    def test_runner_probe_rejects_mixed_or_wrong_dns_and_connects_to_expected_ips(self):
        import socket
        for entry in ('c0', 'c1'):
            code = self.by_id[entry]['run'].split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
            fn = next(x for x in ast.parse(code).body if isinstance(x, ast.FunctionDef) and x.name == 'connections')
            compiled = compile(ast.Module(body=[fn], type_ignores=[]), 'runner probe fixture', 'exec')
            output = {'eventhubs': {'value': {'bootstrap_servers': 'eh.example:9093'}},
                      'private_endpoint_ips': {'value': {'eventhubs': '10.0.0.1', 'blob': '10.0.0.2'}}}
            phase = mock.Mock()
            phase.read.return_value = output
            phase.blob_host.return_value = 'blob.example'
            def exercise(eh_answers):
                calls = []
                def addresses(host, port, **kwargs):
                    values = eh_answers if host == 'eh.example' else ['10.0.0.2']
                    return [(None, None, None, None, (value, port)) for value in values]
                def connect(address, timeout):
                    calls.append(address)
                    connection = mock.MagicMock()
                    connection.__enter__.return_value.getpeername.return_value = address
                    return connection
                fake_socket = SimpleNamespace(SOCK_STREAM=socket.SOCK_STREAM, getaddrinfo=addresses, create_connection=connect)
                namespace = {'phase': phase, 'module': SimpleNamespace(unwrap=lambda x: x['value']), 'socket': fake_socket}
                exec(compiled, namespace)
                namespace['connections']()
                return calls
            with self.subTest(entry=entry):
                self.assertEqual([('10.0.0.1', 9093), ('10.0.0.2', 443)], exercise(['10.0.0.1']))
                for answers in ([], ['10.0.0.10'], ['10.0.0.1', '10.0.0.10']):
                    with self.subTest(answers=answers), self.assertRaises(AssertionError): exercise(answers)

    def test_pod_dns_requires_exact_answers_and_connects_to_private_ips(self):
        for entry in ('c0', 'c1'):
            code = self.by_id[entry]['run'].split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
            fn = next(x for x in ast.parse(code).body if isinstance(x, ast.FunctionDef) and x.name == 'pod_connections')
            nodes = [x for x in fn.body if isinstance(x, ast.Assign) and any(isinstance(t, ast.Name) and t.id in ('resolve', 'command') for t in x.targets)]
            namespace = {'eh': 'eh.example', 'blob': 'blob.example', 'ips': {'eventhubs': '10.0.0.1', 'blob': '10.0.0.2'}}
            exec(compile(ast.Module(body=nodes, type_ignores=[]), 'probe fixture', 'exec'), namespace)
            with tempfile.TemporaryDirectory() as temp:
                tempdir = Path(temp)
                calls = tempdir / 'calls'
                (tempdir / 'nslookup').write_text('#!/bin/sh\nprintf "Server: resolver\\nAddress: 10.0.0.53\\nName: %s\\nAddress: %s\\n" "$1" "$([ "$1" = eh.example ] && printf "${EH_ANSWER}" || printf "10.0.0.2")"\n')
                (tempdir / 'nc').write_text('#!/bin/sh\nprintf "%s\\n" "$*" >> "$CALLS"\n')
                for name in ('nslookup', 'nc'): (tempdir / name).chmod(0o755)
                env = {**os.environ, 'PATH': temp + ':' + os.environ['PATH'], 'CALLS': str(calls), 'EH_ANSWER': '10.0.0.1'}
                result = subprocess.run(['sh', '-c', namespace['command']], env=env, capture_output=True)
                self.assertEqual(0, result.returncode)
                self.assertEqual(b'parchwell-cluster-smoke-ok\n', result.stdout)
                self.assertEqual(['-z -w 15 10.0.0.1 9093', '-z -w 15 10.0.0.2 443'], calls.read_text().splitlines())
                calls.unlink()
                env['EH_ANSWER'] = '10.0.0.10'
                self.assertNotEqual(0, subprocess.run(['sh', '-c', namespace['command']], env=env, capture_output=True).returncode)
                self.assertFalse(calls.exists())

    def test_workload_active_probe_checks_job_before_and_after(self):
        for entry, phase_name in (('c0', 'C0'), ('c1', 'C1')):
            code = self.by_id[entry]['run'].split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
            node = next(x for x in ast.parse(code).body if isinstance(x, ast.FunctionDef) and x.name == 'probe_while_loadgen_active')
            compiled = compile(ast.Module(body=[node], type_ignores=[]), 'active probe fixture', 'exec')
            with tempfile.TemporaryDirectory() as temp:
                release = threading.Event()
                phase = mock.Mock()
                phase.read.return_value = {'phase': phase_name}
                phase.mode_job.return_value = 'job'
                def one(mode):
                    self.assertEqual('loadgen', mode)
                    self.assertTrue(release.wait(3))
                def probe(): release.set()
                invoke = mock.Mock(return_value=SimpleNamespace(returncode=0, stdout=b'{"status":{"active":1}}'))
                namespace = {'phase': phase, 'subprocess': SimpleNamespace(run=invoke), 'json': json, 'time': time,
                             'pathlib': __import__('pathlib'), 'os': os, 'one': one, 'pod_connections': probe}
                with mock.patch.dict(os.environ, {'RUNNER_TEMP': temp}):
                    exec(compiled, namespace)
                    namespace['probe_while_loadgen_active']()
                self.assertGreaterEqual(invoke.call_count, 3)
                self.assertEqual(2, len(Path(temp, 'configuration-' + phase_name.lower() + '-active-window').read_text().split()))
                release.clear()
                invoke.side_effect = [SimpleNamespace(returncode=0, stdout=b'{"status":{"active":1}}'),
                                      SimpleNamespace(returncode=0, stdout=b'{"status":{"active":1}}'),
                                      SimpleNamespace(returncode=0, stdout=b'{"status":{"active":0}}')]
                with mock.patch.dict(os.environ, {'RUNNER_TEMP': temp}), self.assertRaisesRegex(RuntimeError, 'ended during'):
                    namespace['probe_while_loadgen_active']()

    def test_deployed_worker_readback_requires_exact_bindings_in_job_and_pod(self):
        for entry, phase_name in (('c0', 'C0'), ('c1', 'C1')):
            code = self.by_id[entry]['run'].split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
            node = next(x for x in ast.parse(code).body if isinstance(x, ast.FunctionDef) and x.name == 'check_deployed_binding')
            compiled = compile(ast.Module(body=[node], type_ignores=[]), 'binding fixture', 'exec')
            for mode in ('start-line', 'loadgen', 'verify'):
                with self.subTest(entry=entry, mode=mode), tempfile.TemporaryDirectory() as temp:
                    directory = Path(temp)
                    baseline = directory / 'baseline.json'
                    blob = {'account_url': 'https://blob.example', 'containers': {'verifier': 'verifier'}}
                    baseline.write_text(json.dumps(blob))
                    output = {'eventhubs': {'value': {'namespace_name': 'eh-current', 'bootstrap_servers': 'eh-current.servicebus.windows.net:9093'}}, 'blob': {'value': blob}}
                    selected = {'run_id': 'b04-1-1', 'phase': phase_name, 'configuration': phase_name}
                    role = 'loadgen' if mode == 'loadgen' else 'verifier'
                    prefix = 'LoadGen__' if role == 'loadgen' else 'Verifier__'
                    expected = {'NamespaceName': 'eh-current', 'BlobEndpoint' if role == 'loadgen' else 'BlobServiceUri': 'https://blob.example', 'ScenarioId': 'B04', 'ConfigurationId': phase_name, 'RunId': 'b04-1-1', 'Phase': phase_name}
                    if role == 'verifier': expected['Mode'] = mode
                    def document(changed=None):
                        env = [{'name': prefix + k, 'value': ('wrong' if k == changed else v)} for k, v in expected.items()]
                        return {'containers': [{'name': role, 'env': env}]}
                    def exercise(job_change=None, pod_change=None, uid='uid-1'):
                        job = {'metadata': {'name': 'worker-job', 'namespace': 'parchwell', 'uid': 'uid-1'}, 'spec': {'template': {'spec': document(job_change)}}}
                        pod = {'metadata': {'namespace': 'parchwell', 'ownerReferences': [{'kind': 'Job', 'name': 'worker-job', 'uid': uid}]}, 'spec': document(pod_change)}
                        phase = mock.Mock(private=directory)
                        phase.read.side_effect = lambda name: selected if name == 'phase.json' else output
                        phase.mode_job.return_value = 'worker-job'
                        def capture(args, seconds, output, maximum):
                            output.write_text(json.dumps({'items': [pod]} if 'pods' in args else job))
                            return 0
                        phase.run.side_effect = capture
                        namespace = {'phase': phase, 'blob_baseline': baseline, 'json': json,
                                     'module': SimpleNamespace(unwrap=lambda x: x['value'], strict_json=json.loads)}
                        exec(compiled, namespace)
                        namespace['check_deployed_binding'](mode)
                    exercise()
                    for job_change, pod_change, uid in (('NamespaceName', None, 'uid-1'), (None, 'NamespaceName', 'uid-1'),
                                                         ('BlobEndpoint' if role == 'loadgen' else 'BlobServiceUri', None, 'uid-1'),
                                                         (None, 'BlobEndpoint' if role == 'loadgen' else 'BlobServiceUri', 'uid-1'),
                                                         (None, None, 'wrong-uid')):
                        with self.subTest(job_change=job_change, pod_change=pod_change, uid=uid), self.assertRaises(AssertionError):
                            exercise(job_change, pod_change, uid)

    def test_worker_delivery_fixture_requires_positive_matched_ledger(self):
        for entry, phase_name in (('c0', 'C0'), ('c1', 'C1')):
            code = self.by_id[entry]['run'].split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
            node = next(x for x in ast.parse(code).body if isinstance(x, ast.FunctionDef) and x.name == 'confirm_loadgen_delivery')
            compiled = compile(ast.Module(body=[node], type_ignores=[]), 'delivery fixture', 'exec')
            selected = {'scenario': 'B04', 'configuration': phase_name, 'phase': phase_name, 'run_id': 'b04-1-1'}
            identity = {'scenario_id': 'B04', 'configuration_id': phase_name, 'run_id': 'b04-1-1', 'instance_id': 'worker-pod'}
            def exercise(row):
                phase = mock.Mock(private=Path('/private'))
                phase.read.return_value = selected
                phase.mode_job.return_value = 'job'
                phase.download_blob.return_value = (json.dumps(row) + '\n').encode()
                invoke = mock.Mock(return_value=SimpleNamespace(stdout=json.dumps({'items': [{'metadata': {'name': 'worker-pod'}}]}).encode()))
                namespace = {'phase': phase, 'json': json, 're': re, 'io': io, 'time': time, 'subprocess': SimpleNamespace(run=invoke)}
                exec(compiled, namespace)
                namespace['confirm_loadgen_delivery']()
                self.assertEqual('b04-1-1/' + phase_name + '/r1/producer/worker-pod/publication/ledger.jsonl', phase.download_blob.call_args.args[0])
                self.assertEqual('ledger', phase.download_blob.call_args.kwargs['container'])
            with self.subTest(phase=phase_name):
                exercise({'identity': identity, 'delivered': True})
                with self.assertRaises(AssertionError): exercise({'identity': identity, 'delivered': False})
                with self.assertRaises(AssertionError): exercise({'identity': {**identity, 'run_id': 'wrong'}, 'delivered': True})

    def test_arm_settings_fixture_checks_exact_targets_workspace_and_categories(self):
        run = self.by_id['c0_settings']['run']
        code = run.split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
        lifecycle = {'storage_account_resource_id': '/synthetic/storage',
                     'blob_diagnostic_target_resource_id': '/synthetic/storage/blobServices/default',
                     'namespace_resource_id': '/synthetic/namespace',
                     'workspace_resource_id': '/synthetic/workspace'}
        def setting(logs, metrics, workspace='/synthetic/workspace'):
            return {'workspaceId': workspace, 'logAnalyticsDestinationType': 'Dedicated',
                    'logs': [{'category': x, 'enabled': True} for x in logs],
                    'metrics': [{'category': x, 'enabled': True} for x in metrics]}
        blob = setting(['StorageRead', 'StorageWrite', 'StorageDelete'], ['Transaction'])
        eh = setting(['allLogs'], ['AllMetrics'])
        def exercise(blob_setting):
            calls = []
            def invoke(args, **kwargs):
                calls.append(args)
                if 'account' in args:
                    result = {'creationTime': '2026-01-01T00:00:00Z'}
                elif '/synthetic/storage/blobServices/default' in args:
                    result = {'value': [blob_setting]}
                else:
                    self.assertIn('/synthetic/namespace', args)
                    result = {'value': [eh]}
                return SimpleNamespace(stdout=json.dumps(result).encode())
            with tempfile.TemporaryDirectory() as temp:
                Path(temp, 'ephemeral-output.json').write_text(json.dumps({'configuration_lifecycle': {'value': lifecycle}}))
                with mock.patch.dict(os.environ, {'PARCHWELL_PRIVATE_DIRECTORY': temp}), mock.patch('subprocess.run', side_effect=invoke):
                    exec(compile(code, 'fixture', 'exec'), {})
                self.assertEqual(3, len(calls))
        exercise(blob)
        for bad in (setting(['StorageRead', 'StorageWrite'], ['Transaction']),
                    setting(['StorageRead', 'StorageWrite', 'StorageDelete'], ['Transaction'], '/wrong')):
            with self.subTest(bad=bad), self.assertRaises(AssertionError):
                exercise(bad)

    def test_c1_fixture_rejects_shared_identity_or_sentinel_change(self):
        run = self.by_id['c1_settings']['run']
        code = run.split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
        shared = {'storage_account_resource_id': '/synthetic/storage',
                  'blob_private_endpoint_resource_id': '/synthetic/blob-pe',
                  'container_resource_ids': {'ledger': '/l', 'verifier': '/v', 'claim_check': '/c', 'single_action': '/s'},
                  'workspace_resource_id': '/synthetic/workspace',
                  'blob_diagnostic_target_resource_id': '/synthetic/storage/blobServices/default'}
        before = {**shared, 'eventhubs_enabled': True, 'namespace_resource_id': '/synthetic/old',
                  'eventhubs_private_endpoint_resource_id': '/synthetic/old-pe', 'storage_creation_time': 'creation'}
        after = {**shared, 'eventhubs_enabled': True, 'namespace_resource_id': '/synthetic/new',
                 'eventhubs_private_endpoint_resource_id': '/synthetic/new-pe'}
        def setting(logs, metrics):
            return {'workspaceId': '/synthetic/workspace', 'logAnalyticsDestinationType': 'Dedicated',
                    'logs': [{'category': x, 'enabled': True} for x in logs],
                    'metrics': [{'category': x, 'enabled': True} for x in metrics]}
        blob = setting(['StorageRead', 'StorageWrite', 'StorageDelete'], ['Transaction'])
        eh = setting(['allLogs'], ['AllMetrics'])
        def exercise(next_state, sentinel, eh_answers=('10.0.0.1',)):
            import hashlib
            with tempfile.TemporaryDirectory() as temp:
                private = Path(temp)
                (private / 'smoke-shared-before.json').write_text(json.dumps(before))
                (private / 'sentinel-before').write_bytes(b'sentinel')
                (private / 'sentinel-sha256').write_text(hashlib.sha256(b'sentinel').hexdigest())
                output = {'configuration_lifecycle': {'value': next_state},
                          'eventhubs': {'value': {'bootstrap_servers': 'eh.example:9093'}},
                          'private_endpoint_ips': {'value': {'eventhubs': '10.0.0.1', 'blob': '10.0.0.2'}}}
                phase = mock.Mock(private=private)
                phase.read.side_effect = lambda name: output if name == 'ephemeral-output.json' else {'phase': 'C0', 'configuration': 'C0'}
                phase.download_blob.return_value = sentinel
                phase.blob_host.return_value = 'blob.example'
                module = SimpleNamespace(Phase=lambda: phase, unwrap=lambda x: x['value'], write_json=mock.Mock())
                spec = SimpleNamespace(loader=SimpleNamespace(exec_module=lambda _: None))
                def invoke(args, **kwargs):
                    result = {'creationTime': 'creation'} if 'account' in args else {'value': [blob if '/synthetic/storage/blobServices/default' in args else eh]}
                    return SimpleNamespace(stdout=json.dumps(result).encode())
                connections = []
                def connect(address, timeout):
                    connections.append(address)
                    connection = mock.MagicMock()
                    connection.__enter__.return_value.getpeername.return_value = ('10.0.0.1' if address[1] == 9093 else '10.0.0.2', address[1])
                    return connection
                with (mock.patch.dict(os.environ, {'PARCHWELL_PRIVATE_DIRECTORY': temp, 'PARCHWELL_RUN_ID': 'b04-1-1'}),
                      mock.patch('importlib.util.spec_from_file_location', return_value=spec),
                      mock.patch('importlib.util.module_from_spec', return_value=module),
                      mock.patch('subprocess.run', side_effect=invoke), mock.patch('socket.create_connection', side_effect=connect),
                      mock.patch('socket.getaddrinfo', side_effect=lambda host, port, **kw: [(None, None, None, None, (value, port)) for value in (eh_answers if port == 9093 else ['10.0.0.2'])])):
                    exec(compile(code, 'C1 fixture', 'exec'), {})
                module.write_json.assert_called_once()
                self.assertEqual([('10.0.0.1', 9093), ('10.0.0.2', 443)], connections)
        exercise(after, b'sentinel')
        for answers in ((), ('10.0.0.10',), ('10.0.0.1', '10.0.0.10')):
            with self.subTest(answers=answers), self.assertRaises(AssertionError):
                exercise(after, b'sentinel', answers)
        for state, sentinel in (({**after, 'storage_account_resource_id': '/different'}, b'sentinel'), (after, b'changed')):
            with self.subTest(state=state, sentinel=sentinel), self.assertRaises(AssertionError):
                exercise(state, sentinel)

    def test_cleanup_failure_matrix_and_report_never_pass_incomplete_run(self):
        destroy_guard = self.by_id['destroy']['if']
        stop_guard = self.by_id['stop']['if']
        self.assertEqual("always() && steps.prepare.outcome == 'success' && steps.azure_session.outcome == 'success'", destroy_guard)
        self.assertEqual("always() && steps.prepare.outcome == 'success' && steps.azure_session.outcome == 'success' && (steps.start.outputs.attempted == 'true' || steps.cluster_apply.outputs.attempted == 'true')", stop_guard)
        report = next(step for step in self.steps if step['name'] == 'Report diagnostic and cleanup outcomes')
        code = report['run'].split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
        required = 'guard checkout select tools prepare azure_session images cluster_init start cluster_plan cluster_apply ephemeral_init initial_cleanup ephemeral_plan ephemeral_apply dns credentials namespace c0 c0_settings c0_cleanup off_transition on_transition c1_settings c1 delete_workload destroy stop receipt upload'.split()
        for failed in ('cluster_init', 'start', 'cluster_plan', 'cluster_apply', 'ephemeral_init', 'initial_cleanup', 'ephemeral_plan', 'ephemeral_apply', 'c0', 'c0_settings', 'c0_cleanup', 'off_transition', 'on_transition', 'c1_settings', 'c1', 'delete_workload', 'destroy', 'stop', 'receipt', 'upload', 'cancelled'):
            with self.subTest(failed=failed), tempfile.TemporaryDirectory() as temp:
                steps = {name: {'outcome': 'success'} for name in required}
                if failed != 'cancelled': steps[failed]['outcome'] = 'failure'
                environment = {'STEP_RESULTS': json.dumps(steps), 'JOB_STATUS': 'cancelled' if failed == 'cancelled' else 'success',
                               'GITHUB_STEP_SUMMARY': str(Path(temp) / 'summary')}
                with mock.patch.dict(os.environ, environment), contextlib.redirect_stdout(io.StringIO()), self.assertRaises(SystemExit) as result:
                    exec(compile(code, 'report fixture', 'exec'), {})
                self.assertEqual(1, result.exception.code)
                self.assertIn('failed', Path(temp, 'summary').read_text())
        # Both cleanup conditions require successful private setup and authenticated session.
        for guard in (destroy_guard, stop_guard):
            self.assertIn("steps.prepare.outcome == 'success'", guard)
            self.assertIn("steps.azure_session.outcome == 'success'", guard)
        # Stop does not depend on destroy success, preserving independent cleanup.
        self.assertNotIn('steps.destroy', stop_guard)

    def test_cleanup_is_independent_and_inline_python_compiles(self):
        for entry in ('delete_workload', 'destroy', 'stop'):
            self.assertIn('always()', self.by_id[entry]['if'])
        for step in self.steps:
            run = step.get('run', '')
            marker = "python3 - <<'PYTHON'\n"
            if marker in run:
                code = run.split(marker, 1)[1].split('\nPYTHON', 1)[0]
                compile(code, step['name'], 'exec')


if __name__ == '__main__':
    unittest.main()

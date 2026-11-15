"""Offline contracts for the configuration isolation diagnostic workflow."""
from pathlib import Path
import ast
import contextlib
import hashlib
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
SUBSCRIPTION = '00000000-0000-0000-0000-000000000000'
SUFFIX = 'offline1'
WORKSPACE = '/synthetic/workspace'
ROOT = '/subscriptions/' + SUBSCRIPTION + '/resourceGroups/rg-parchwell-eph/providers/'
BLOB = ROOT + 'Microsoft.Storage/storageAccounts/stparchwell' + SUFFIX + '/blobServices/default'
def namespace_id(phase):
    name = 'ehp-' + hashlib.sha256(('synthetic/repository\n1\n1\n' + phase).encode()).hexdigest()[:24]
    return ROOT + 'Microsoft.EventHub/namespaces/' + name

def settings_environment(temp):
    Path(temp, 'ephemeral.tfvars.json').write_text(json.dumps({'naming_suffix': SUFFIX}))
    return {'PARCHWELL_PRIVATE_DIRECTORY': temp, 'RUNNER_TEMP': temp,
            'PARCHWELL_EPHEMERAL_TFVARS_PATH': str(Path(temp, 'ephemeral.tfvars.json')),
            'ARM_SUBSCRIPTION_ID': SUBSCRIPTION, 'GITHUB_REPOSITORY': 'synthetic/repository',
            'GITHUB_RUN_ID': '1', 'GITHUB_RUN_ATTEMPT': '1'}

def write_state(args):
    path = Path(args[args.index('--stdout-file') + 1])
    path.write_text(json.dumps({'values': {'root_module': {'resources': [{'address': 'data.terraform_remote_state.persistent', 'mode': 'data', 'values': {'outputs': {'log_analytics_workspace_id': WORKSPACE}}}]}}}))



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
            self.assertIn("pod_connections('idle')", self.by_id[entry]['run'])
            self.assertIn("pod_connections('active')", self.by_id[entry]['run'])
            self.assertIn('confirm_loadgen_delivery()', self.by_id[entry]['run'])
            self.assertIn("if row['delivered'] is True:", self.by_id[entry]['run'])

    def test_runner_probe_rejects_mixed_or_wrong_dns_and_connects_to_expected_ips(self):
        import socket
        for entry in ('c0', 'c1'):
            code = self.by_id[entry]['run'].split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
            nodes = [x for x in ast.parse(code).body if isinstance(x, ast.FunctionDef) and x.name in ('save_probe', 'probe_runner', 'connections')]
            compiled = compile(ast.Module(body=nodes, type_ignores=[]), 'runner probe fixture', 'exec')
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
                fake_socket = SimpleNamespace(SOCK_STREAM=socket.SOCK_STREAM, timeout=socket.timeout, getaddrinfo=addresses, create_connection=connect)
                namespace = {'phase': phase, 'module': SimpleNamespace(unwrap=lambda x: x['value']), 'socket': fake_socket, 'pathlib': __import__('pathlib'), 'os': os, 'json': json}
                phase.read.side_effect = lambda name: {'phase': entry.upper()} if name == 'phase.json' else output
                with tempfile.TemporaryDirectory() as temp, mock.patch.dict(os.environ, {'RUNNER_TEMP': temp}):
                    exec(compiled, namespace)
                    namespace['connections']('idle')
                return calls
            with self.subTest(entry=entry):
                self.assertEqual([('10.0.0.1', 9093), ('10.0.0.2', 443)], exercise(['10.0.0.1']))
                for answers in ([], ['10.0.0.10'], ['10.0.0.1', '10.0.0.10']):
                    with self.subTest(answers=answers), self.assertRaisesRegex(RuntimeError, 'runner DNS probe failed'): exercise(answers)

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
                self.assertEqual(b'eventhubs dns passed\nblob dns passed\neventhubs tcp passed\nblob tcp passed\n', result.stdout)
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
                def probe(context):
                    self.assertEqual('active', context)
                    release.set()
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
            node = next(x for x in ast.parse(code).body if isinstance(x, ast.FunctionDef) and x.name == 'assert_deployed_binding')
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
                        namespace['assert_deployed_binding'](mode)
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
        lifecycle = {'storage_account_resource_id': BLOB.removesuffix('/blobServices/default'),
                     'blob_diagnostic_target_resource_id': BLOB,
                     'namespace_resource_id': namespace_id('C0'),
                     'workspace_resource_id': WORKSPACE}
        def setting(logs, metrics, workspace=WORKSPACE, target=BLOB, name='diag-blob-' + SUFFIX):
            return {'id': target + '/providers/Microsoft.Insights/diagnosticSettings/' + name, 'workspaceId': workspace, 'logAnalyticsDestinationType': 'Dedicated',
                    'logs': [{'category': x, 'enabled': True} for x in logs],
                    'metrics': [{'category': x, 'enabled': True} for x in metrics]}
        blob = setting(['StorageRead', 'StorageWrite', 'StorageDelete'], ['Transaction'])
        eh = setting(['allLogs'], ['AllMetrics'], target=namespace_id('C0'), name='diag-eventhubs-' + SUFFIX)
        def exercise(blob_setting):
            calls = []
            def invoke(args, **kwargs):
                calls.append(args)
                if '--stdout-file' in args:
                    write_state(args)
                    return SimpleNamespace(stdout=b'')
                if 'account' in args:
                    result = {'creationTime': '2026-01-01T00:00:00Z'}
                elif BLOB in args:
                    result = {'value': [blob_setting]}
                else:
                    self.assertIn(namespace_id('C0'), args)
                    result = {'value': [eh]}
                return SimpleNamespace(stdout=json.dumps(result).encode())
            with tempfile.TemporaryDirectory() as temp:
                Path(temp, 'ephemeral-output.json').write_text(json.dumps({'configuration_lifecycle': {'value': lifecycle}}))
                with mock.patch.dict(os.environ, settings_environment(temp)), mock.patch('subprocess.run', side_effect=invoke):
                    exec(compile(code, 'fixture', 'exec'), {})
                self.assertEqual(4, len(calls))
        exercise(blob)
        for bad in (setting(['StorageRead', 'StorageWrite'], ['Transaction']),
                    setting(['StorageRead', 'StorageWrite', 'StorageDelete'], ['Transaction'], '/wrong')):
            with self.subTest(bad=bad), self.assertRaises(RuntimeError):
                exercise(bad)

    def test_sanitized_observations_survive_partial_failure_without_private_ids(self):
        run = self.by_id['c0_settings']['run']
        code = run.split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
        fn = next(x for x in ast.parse(code).body if isinstance(x, ast.FunctionDef) and x.name == 'observe_settings')
        compiled = compile(ast.Module(body=[fn], type_ignores=[]), 'observation fixture', 'exec')
        targets = {'blob': BLOB, 'eventhubs': namespace_id('C0')}
        lifecycle = {'blob_diagnostic_target_resource_id': BLOB, 'namespace_resource_id': namespace_id('C0'), 'workspace_resource_id': WORKSPACE}
        def setting(service):
            name = ('diag-blob-' if service == 'blob' else 'diag-eventhubs-') + SUFFIX
            logs = ['StorageRead', 'StorageWrite', 'StorageDelete'] if service == 'blob' else ['allLogs']
            metrics = ['Transaction'] if service == 'blob' else ['AllMetrics']
            return {'id': targets[service] + '/providers/Microsoft.Insights/diagnosticSettings/' + name,
                    'workspaceId': WORKSPACE, 'logAnalyticsDestinationType': 'Dedicated',
                    'logs': [{'category': x, 'enabled': True} for x in logs],
                    'metrics': [{'category': x, 'enabled': True} for x in metrics]}
        with tempfile.TemporaryDirectory() as temp:
            env = settings_environment(temp)
            def invoke(args, **kwargs):
                write_state(args)
                return SimpleNamespace(stdout=b'')
            def command(args):
                if targets['eventhubs'] in args: raise RuntimeError('private server error and resource ID')
                return {'value': [setting('blob')]}
            namespace = {'os': os, 'pathlib': __import__('pathlib'), 'json': json, 'subprocess': SimpleNamespace(run=invoke, DEVNULL=subprocess.DEVNULL), 'p': Path(temp), 'command': command}
            with mock.patch.dict(os.environ, env):
                exec(compiled, namespace)
                with self.assertRaisesRegex(RuntimeError, 'diagnostic_settings_unconfirmed'):
                    namespace['observe_settings']('C0', lifecycle)
                directory = Path(temp, 'configuration-isolation-observations')
                blob = json.loads((directory / 'C0-blob.json').read_text())
                eh = json.loads((directory / 'C0-eventhubs.json').read_text())
                self.assertEqual(('passed', 'passed', 'passed', 'passed', 'none'), tuple(blob[k] for k in ('status', 'target', 'workspace', 'destination', 'reason')))
                self.assertEqual(('failed', 'readback_failed'), (eh['status'], eh['reason']))
                for record in (blob, eh):
                    for secret in (BLOB, WORKSPACE, SUBSCRIPTION, 'private server error'):
                        self.assertNotIn(secret, json.dumps(record))
                # Each mismatch leaves only a fixed reason and preserves the other service.
                for field, expected_reason in (('id', 'target_mismatch'), ('workspaceId', 'workspace_mismatch'),
                                               ('logAnalyticsDestinationType', 'destination_mismatch'), ('logs', 'category_mismatch')):
                    altered = setting('blob')
                    altered[field] = [] if field == 'logs' else 'private-mismatch'
                    namespace['command'] = lambda args, altered=altered: {'value': [altered if targets['blob'] in args else setting('eventhubs')]}
                    with self.subTest(field=field), self.assertRaisesRegex(RuntimeError, 'diagnostic_settings_unconfirmed'):
                        namespace['observe_settings']('C0', lifecycle)
                    record = json.loads((directory / 'C0-blob.json').read_text())
                    self.assertEqual(expected_reason, record['reason'])
                    self.assertNotIn('private-mismatch', json.dumps(record))
                    self.assertEqual('passed', json.loads((directory / 'C0-eventhubs.json').read_text())['status'])
                namespace['command'] = command
                with self.assertRaisesRegex(RuntimeError, 'diagnostic_settings_unconfirmed'):
                    namespace['observe_settings']('C0', lifecycle)
                # A malformed or absent protected-state record fails both observations closed.
                def invalid_state(args, **kwargs):
                    Path(args[args.index('--stdout-file') + 1]).write_text('{"values":{"root_module":{"resources":[]}}}')
                namespace['subprocess'] = SimpleNamespace(run=invalid_state, DEVNULL=subprocess.DEVNULL)
                with self.assertRaisesRegex(RuntimeError, 'diagnostic_settings_unconfirmed'):
                    namespace['observe_settings']('C1', lifecycle)
                self.assertEqual('expected_identity_unavailable', json.loads((directory / 'C1-blob.json').read_text())['reason'])
                self.assertEqual('passed', json.loads((directory / 'C0-blob.json').read_text())['status'])
                # The public receipt reads only allowlisted fields and rejects a contaminated file.
                (directory / 'C1-eventhubs.json').write_text(json.dumps({'phase': 'C1', 'secret': WORKSPACE}))
                receipt = self.by_id['receipt']['run'].split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
                with mock.patch.dict(os.environ, {**env, 'STEP_RESULTS': '{}'}):
                    exec(compile(receipt, 'receipt fixture', 'exec'), {})
                text = Path(temp, 'configuration-smoke-receipt', 'receipt.txt').read_text()
                self.assertIn('C0 blob: passed', text)
                self.assertIn('C0 eventhubs: failed', text)
                self.assertIn('C1 eventhubs: failed reason=invalid_observation', text)
                self.assertNotIn(WORKSPACE, text)
                self.assertNotIn(SUBSCRIPTION, text)

    def test_c1_fixture_rejects_shared_identity_or_sentinel_change(self):
        run = self.by_id['c1_settings']['run']
        code = run.split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
        shared = {'storage_account_resource_id': BLOB.removesuffix('/blobServices/default'),
                  'blob_private_endpoint_resource_id': '/synthetic/blob-pe',
                  'container_resource_ids': {'ledger': '/l', 'verifier': '/v', 'claim_check': '/c', 'single_action': '/s'},
                  'workspace_resource_id': '/synthetic/workspace',
                  'blob_diagnostic_target_resource_id': BLOB}
        before = {**shared, 'eventhubs_enabled': True, 'namespace_resource_id': '/synthetic/old',
                  'eventhubs_private_endpoint_resource_id': '/synthetic/old-pe', 'storage_creation_time': 'creation'}
        after = {**shared, 'eventhubs_enabled': True, 'namespace_resource_id': namespace_id('C1'),
                 'eventhubs_private_endpoint_resource_id': '/synthetic/new-pe'}
        def setting(logs, metrics, target, name):
            return {'id': target + '/providers/Microsoft.Insights/diagnosticSettings/' + name, 'workspaceId': WORKSPACE, 'logAnalyticsDestinationType': 'Dedicated',
                    'logs': [{'category': x, 'enabled': True} for x in logs],
                    'metrics': [{'category': x, 'enabled': True} for x in metrics]}
        blob = setting(['StorageRead', 'StorageWrite', 'StorageDelete'], ['Transaction'], BLOB, 'diag-blob-' + SUFFIX)
        eh = setting(['allLogs'], ['AllMetrics'], namespace_id('C1'), 'diag-eventhubs-' + SUFFIX)
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
                    if '--stdout-file' in args:
                        write_state(args)
                        return SimpleNamespace(stdout=b'')
                    result = {'creationTime': 'creation'} if 'account' in args else {'value': [blob if BLOB in args else eh]}
                    return SimpleNamespace(stdout=json.dumps(result).encode())
                connections = []
                def connect(address, timeout):
                    connections.append(address)
                    connection = mock.MagicMock()
                    connection.__enter__.return_value.getpeername.return_value = ('10.0.0.1' if address[1] == 9093 else '10.0.0.2', address[1])
                    return connection
                with (mock.patch.dict(os.environ, {**settings_environment(temp), 'PARCHWELL_RUN_ID': 'b04-1-1'}),
                      mock.patch('importlib.util.spec_from_file_location', return_value=spec),
                      mock.patch('importlib.util.module_from_spec', return_value=module),
                      mock.patch('subprocess.run', side_effect=invoke), mock.patch('socket.create_connection', side_effect=connect),
                      mock.patch('socket.getaddrinfo', side_effect=lambda host, port, **kw: [(None, None, None, None, (value, port)) for value in (eh_answers if port == 9093 else ['10.0.0.2'])])):
                    exec(compile(code, 'C1 fixture', 'exec'), {})
                module.write_json.assert_called_once()
                self.assertEqual([('10.0.0.1', 9093), ('10.0.0.2', 443)], connections)
        exercise(after, b'sentinel')
        for answers in ((), ('10.0.0.10',), ('10.0.0.1', '10.0.0.10')):
            with self.subTest(answers=answers), self.assertRaisesRegex(RuntimeError, 'runner DNS probe failed'):
                exercise(after, b'sentinel', answers)
        for state, sentinel in (({**after, 'storage_account_resource_id': '/different'}, b'sentinel'), (after, b'changed')):
            with self.subTest(state=state, sentinel=sentinel), self.assertRaises(AssertionError):
                exercise(state, sentinel)

    def test_binding_receipt_retains_success_failure_and_unattempted_without_exception_text(self):
        for entry, phase_name in (('c0', 'C0'), ('c1', 'C1')):
            code = self.by_id[entry]['run'].split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
            fn = next(x for x in ast.parse(code).body if isinstance(x, ast.FunctionDef) and x.name == 'check_deployed_binding')
            compiled = compile(ast.Module(body=[fn], type_ignores=[]), 'binding result fixture', 'exec')
            receipt = self.by_id['receipt']['run'].split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
            with self.subTest(entry=entry), tempfile.TemporaryDirectory() as temp:
                phase = mock.Mock()
                phase.read.return_value = {'phase': phase_name}
                check = mock.Mock()
                namespace = {'phase': phase, 'assert_deployed_binding': check, 'pathlib': __import__('pathlib'), 'os': os, 'json': json}
                with mock.patch.dict(os.environ, {**settings_environment(temp), 'STEP_RESULTS': '{}'}):
                    exec(compiled, namespace)
                    namespace['check_deployed_binding']('start-line')
                    check.side_effect = AssertionError('private resource and secret')
                    with self.assertRaises(AssertionError): namespace['check_deployed_binding']('loadgen')
                    exec(compile(receipt, 'binding receipt fixture', 'exec'), {})
                text = Path(temp, 'configuration-smoke-receipt', 'receipt.txt').read_text()
                self.assertIn(phase_name + ' start-line deployed binding: passed', text)
                self.assertIn(phase_name + ' loadgen deployed binding: failed reason=binding_unconfirmed', text)
                self.assertIn(phase_name + ' verify deployed binding: not_attempted', text)
                self.assertNotIn('private resource and secret', text)

    def test_receipt_projects_worker_status_without_raw_metadata_or_logs(self):
        code = self.by_id['receipt']['run'].split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
        complete = {'job': 'b04-1-1-c0-r1-start-line', 'mode': 'start-line', 'repeat': 1, 'outcome': 'complete',
                    'render': 0, 'apply': 0, 'wait': 0, 'collect': 0, 'private': '/private/resource-id'}
        failed = {'job': 'b04-1-1-c0-r1-loadgen', 'mode': 'loadgen', 'repeat': 1, 'outcome': 'failed',
                  'reason': 'sensitive worker output'}
        with tempfile.TemporaryDirectory() as temp:
            env = {**settings_environment(temp), 'STEP_RESULTS': '{}', 'PARCHWELL_ARTIFACT_DIRECTORY': temp}
            path = Path(temp, 'phase-status.json')
            path.write_text(json.dumps({'modes': [complete, failed]}))
            with mock.patch.dict(os.environ, env):
                exec(compile(code, 'worker receipt fixture', 'exec'), {})
            text = Path(temp, 'configuration-smoke-receipt', 'receipt.txt').read_text()
            self.assertIn('C0 start-line worker execution: passed', text)
            self.assertIn('C0 loadgen worker execution: failed reason=worker_incomplete', text)
            self.assertIn('C0 verify worker execution: not_attempted', text)
            self.assertIn('C1 start-line worker execution: not_attempted', text)
            self.assertNotIn('/private/resource-id', text)
            self.assertNotIn('sensitive worker output', text)
            path.write_text(json.dumps({'modes': [complete, complete]}))
            with mock.patch.dict(os.environ, env):
                exec(compile(code, 'duplicate receipt fixture', 'exec'), {})
            self.assertIn('C0 start-line worker execution: failed reason=status_invalid', Path(temp, 'configuration-smoke-receipt', 'receipt.txt').read_text())
            path.unlink()
            with mock.patch.dict(os.environ, env):
                exec(compile(code, 'missing receipt fixture', 'exec'), {})
            self.assertIn('C0 start-line worker execution: unavailable', Path(temp, 'configuration-smoke-receipt', 'receipt.txt').read_text())

    def test_runner_probe_failure_records_preceding_success_without_private_exception(self):
        import socket
        for entry, phase_name, context in (('c0', 'C0', 'idle'), ('c0', 'C0', 'active'),
                                            ('c1', 'C1', 'idle'), ('c1', 'C1', 'active'),
                                            ('c1_settings', 'C1', 'settings')):
            code = self.by_id[entry]['run'].split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
            nodes = [x for x in ast.parse(code).body if isinstance(x, ast.FunctionDef) and x.name in ('save_probe', 'probe_runner')]
            compiled = compile(ast.Module(body=nodes, type_ignores=[]), 'probe fixture', 'exec')
            for service, failure, expected in (('eventhubs', socket.timeout('sensitive exception'), 'timeout'),
                                                ('blob', OSError('sensitive exception'), 'failed')):
                with self.subTest(entry=entry, context=context, service=service), tempfile.TemporaryDirectory() as temp:
                    calls = []
                    def addresses(host, port, **kwargs):
                        return [(None, None, None, None, ('10.0.0.1' if port == 9093 else '10.0.0.2', port))]
                    def connect(address, timeout):
                        calls.append((address, timeout))
                        if (address[1] == 9093) == (service == 'eventhubs'):
                            raise failure
                        connection = mock.MagicMock()
                        connection.__enter__.return_value.getpeername.return_value = address
                        return connection
                    fake = SimpleNamespace(SOCK_STREAM=socket.SOCK_STREAM, timeout=socket.timeout,
                                           getaddrinfo=addresses, create_connection=connect)
                    namespace = {'socket': fake, 'pathlib': __import__('pathlib'), 'os': os, 'json': json}
                    with mock.patch.dict(os.environ, {'RUNNER_TEMP': temp}):
                        exec(compiled, namespace)
                        with self.assertRaisesRegex(RuntimeError, '^runner TCP probe failed$'):
                            namespace['probe_runner'](phase_name, context, 'eh.example', 'blob.example',
                                                      {'eventhubs': '10.0.0.1', 'blob': '10.0.0.2'})
                    directory = Path(temp, 'configuration-isolation-probes')
                    def status(svc, op):
                        return json.loads(Path(directory, '-'.join((phase_name, context, 'runner', svc, op)) + '.json').read_text())['status']
                    self.assertEqual('passed', status(service, 'dns'))
                    self.assertEqual(expected, status(service, 'tcp'))
                    tcp_record = json.loads((directory / '-'.join((phase_name, context, 'runner', service, 'tcp'))).with_suffix('.json').read_text())
                    self.assertEqual('timeout' if expected == 'timeout' else 'connection_error', tcp_record['reason'])
                    self.assertTrue(all(timeout == 15 for _, timeout in calls))
                    if service == 'blob':
                        self.assertEqual('passed', status('eventhubs', 'dns'))
                        self.assertEqual('passed', status('eventhubs', 'tcp'))
                    else:
                        self.assertFalse(Path(directory, phase_name + '-' + context + '-runner-blob-dns.json').exists())
                    self.assertNotIn('sensitive exception', ''.join(path.read_text() for path in directory.iterdir()))
                    self.assertNotIn('10.0.0.', ''.join(path.read_text() for path in directory.iterdir()))

    def test_cluster_shell_failure_matrix_and_mixed_dns(self):
        for entry in ('c0', 'c1'):
            code = self.by_id[entry]['run'].split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
            fn = next(x for x in ast.parse(code).body if isinstance(x, ast.FunctionDef) and x.name == 'pod_connections')
            nodes = [x for x in fn.body if isinstance(x, ast.Assign) and any(isinstance(t, ast.Name) and t.id in ('resolve', 'command') for t in x.targets)]
            namespace = {'eh': 'eh.example', 'blob': 'blob.example', 'ips': {'eventhubs': '10.0.0.1', 'blob': '10.0.0.2'}}
            exec(compile(ast.Module(body=nodes, type_ignores=[]), 'cluster shell', 'exec'), namespace)
            for failing in ('eventhubs dns', 'blob dns', 'eventhubs tcp', 'blob tcp', 'eventhubs mixed', 'blob mixed'):
                with self.subTest(entry=entry, failing=failing), tempfile.TemporaryDirectory() as temp:
                    directory = Path(temp)
                    (directory / 'nslookup').write_text('''#!/bin/sh
printf "dns %s\n" "$*" >> "$CALLS"
service=eventhubs; ip=10.0.0.1
[ "$1" = blob.example ] && service=blob && ip=10.0.0.2
printf "Name: %s\nAddress: %s\n" "$1" "$ip"
[ "$FAIL" = "$service dns" ] && printf "Address: 10.0.0.9\n"
[ "$FAIL" = "$service mixed" ] && printf "Address: 10.0.0.9\n"
exit 0
''')
                    (directory / 'nc').write_text('''#!/bin/sh
printf "tcp %s\n" "$*" >> "$CALLS"
service=eventhubs
[ "$4" = 10.0.0.2 ] && service=blob
[ "$FAIL" != "$service tcp" ]
''')
                    for name in ('nslookup', 'nc'): (directory / name).chmod(0o755)
                    env = {**os.environ, 'PATH': temp + ':' + os.environ['PATH'], 'FAIL': failing, 'CALLS': str(directory / 'calls')}
                    result = subprocess.run(['sh', '-c', namespace['command']], env=env, capture_output=True)
                    self.assertNotEqual(0, result.returncode)
                    operation = failing.replace(' mixed', ' dns')
                    order = ['eventhubs dns', 'blob dns', 'eventhubs tcp', 'blob tcp']
                    index = order.index(operation)
                    self.assertEqual([row + ' passed' for row in order[:index]] + [operation + ' failed'],
                                     result.stdout.decode().splitlines())
                    self.assertNotIn(b'10.0.0.', result.stdout)
                    expected_calls = ['dns eh.example', 'dns blob.example', 'tcp -z -w 15 10.0.0.1 9093', 'tcp -z -w 15 10.0.0.2 443'][:index + 1]
                    actual_calls = (directory / 'calls').read_text().splitlines() if (directory / 'calls').exists() else []
                    self.assertEqual(expected_calls, actual_calls)

    def test_runner_failure_reasons_and_private_recording_errors(self):
        import socket
        for entry, phase_name, context in (('c0', 'C0', 'idle'), ('c1', 'C1', 'active'), ('c1_settings', 'C1', 'settings')):
            code = self.by_id[entry]['run'].split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
            nodes = [x for x in ast.parse(code).body if isinstance(x, ast.FunctionDef) and x.name in ('save_probe', 'probe_runner')]
            compiled = compile(ast.Module(body=nodes, type_ignores=[]), 'probe fixture', 'exec')
            for service in ('eventhubs', 'blob'):
                for failure, reason, operation in (('lookup', 'dns_error', 'dns'), ('wrong', 'dns_mismatch', 'dns'),
                                                    ('peer', 'peer_mismatch', 'tcp'), ('connection', 'connection_error', 'tcp')):
                    with self.subTest(entry=entry, service=service, failure=failure), tempfile.TemporaryDirectory() as temp:
                        calls = []
                        def addresses(host, port, **kwargs):
                            calls.append(('dns', port))
                            selected = (port == 9093) == (service == 'eventhubs')
                            if selected and failure == 'lookup': raise OSError('private lookup exception')
                            ip = '10.0.0.1' if port == 9093 else '10.0.0.2'
                            if selected and failure == 'wrong': ip = '10.0.0.9'
                            return [(None, None, None, None, (ip, port))]
                        def connect(address, timeout):
                            calls.append(('tcp', address[1]))
                            selected = (address[1] == 9093) == (service == 'eventhubs')
                            if selected and failure == 'connection': raise OSError('private connection exception')
                            connection = mock.MagicMock()
                            connection.__enter__.return_value.getpeername.return_value = ('10.0.0.9' if selected and failure == 'peer' else address[0], address[1])
                            return connection
                        fake = SimpleNamespace(SOCK_STREAM=socket.SOCK_STREAM, timeout=socket.timeout, getaddrinfo=addresses, create_connection=connect)
                        namespace = {'socket': fake, 'pathlib': __import__('pathlib'), 'os': os, 'json': json}
                        with mock.patch.dict(os.environ, {'RUNNER_TEMP': temp}):
                            exec(compiled, namespace)
                            with self.assertRaises(RuntimeError) as caught:
                                namespace['probe_runner'](phase_name, context, 'eh.example', 'blob.example', {'eventhubs': '10.0.0.1', 'blob': '10.0.0.2'})
                        self.assertNotIn('private', str(caught.exception))
                        target = Path(temp, 'configuration-isolation-probes', '-'.join((phase_name, context, 'runner', service, operation)) + '.json')
                        data = json.loads(target.read_text())
                        self.assertEqual(('failed', reason), (data['status'], data['reason']))
                        self.assertEqual((operation, 9093 if service == 'eventhubs' else 443), calls[-1])
            with tempfile.TemporaryDirectory() as temp:
                fake = SimpleNamespace(SOCK_STREAM=socket.SOCK_STREAM, timeout=socket.timeout,
                                       getaddrinfo=mock.Mock(side_effect=OSError('private lookup exception')))
                namespace = {'socket': fake, 'pathlib': __import__('pathlib'), 'os': os, 'json': json}
                with mock.patch.dict(os.environ, {'RUNNER_TEMP': temp}), mock.patch.object(Path, 'write_text', side_effect=OSError('private write exception')):
                    exec(compiled, namespace)
                    with self.assertRaisesRegex(RuntimeError, '^probe recording failed$') as caught:
                        namespace['probe_runner'](phase_name, context, 'eh.example', 'blob.example', {'eventhubs': '10.0.0.1', 'blob': '10.0.0.2'})
                self.assertTrue(caught.exception.__suppress_context__)

    def test_failed_cluster_wait_collects_rows_before_delete_and_rejects_injection(self):
        for entry, phase_name in (('c0', 'C0'), ('c1', 'C1')):
            code = self.by_id[entry]['run'].split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
            nodes = [x for x in ast.parse(code).body if isinstance(x, ast.FunctionDef) and x.name in ('save_probe', 'pod_connections')]
            compiled = compile(ast.Module(body=nodes, type_ignores=[]), 'cluster fixture', 'exec')
            for logs, expected in (('eventhubs dns passed\nblob dns failed\n', 'failed'),
                                   ('eventhubs dns passed\n10.9.8.7 private exception\n', None),
                                   ('', None)):
                with self.subTest(entry=entry, logs=logs), tempfile.TemporaryDirectory() as temp:
                    phase = mock.Mock(private=Path(temp))
                    phase.read.return_value = {'phase': phase_name}
                    calls = []
                    def invoke(args, **kwargs):
                        calls.append(args)
                        if 'helm' in args:
                            return SimpleNamespace(stdout=b'          command: []\n')
                        if 'wait-job' in args:
                            raise subprocess.CalledProcessError(1, args)
                        if '--stdout-file' in args:
                            Path(args[args.index('--stdout-file') + 1]).write_text(logs)
                        return SimpleNamespace(stdout=b'')
                    namespace = {'phase': phase, 'connections': lambda context: ('eh.example', 'blob.example', {'eventhubs': '10.0.0.1', 'blob': '10.0.0.2'}),
                                 'os': os, 'pathlib': __import__('pathlib'), 'ipaddress': __import__('ipaddress'), 're': re, 'json': json,
                                 'subprocess': SimpleNamespace(run=invoke)}
                    with mock.patch.dict(os.environ, {'RUNNER_TEMP': temp, 'PARCHWELL_RUN_ID': 'synthetic'}):
                        exec(compiled, namespace)
                        with self.assertRaises(RuntimeError): namespace['pod_connections']('idle')
                    self.assertTrue(any('delete' in call for call in calls))
                    self.assertLess(next(i for i, c in enumerate(calls) if '--stdout-file' in c),
                                    next(i for i, c in enumerate(calls) if 'delete' in c))
                    directory = Path(temp, 'configuration-isolation-probes')
                    target = directory / (phase_name + '-idle-cluster-blob-dns.json')
                    if expected:
                        self.assertEqual(expected, json.loads(target.read_text())['status'])
                        self.assertEqual('command_failed', json.loads(target.read_text())['reason'])
                        self.assertEqual('passed', json.loads((directory / (phase_name + '-idle-cluster-eventhubs-dns.json')).read_text())['status'])
                    else:
                        self.assertFalse(target.exists())
                    self.assertNotIn('private exception', ''.join(path.read_text() for path in directory.iterdir()))

    def test_cluster_receipt_marks_only_proven_later_operations_not_attempted(self):
        receipt = self.by_id['receipt']['run'].split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
        order = [('eventhubs', 'dns'), ('blob', 'dns'), ('eventhubs', 'tcp'), ('blob', 'tcp')]
        for phase in ('C0', 'C1'):
            for context in ('idle', 'active'):
                for failure_index in range(4):
                    with self.subTest(phase=phase, context=context, failure=failure_index), tempfile.TemporaryDirectory() as temp:
                        directory = Path(temp, 'configuration-isolation-probes')
                        directory.mkdir()
                        (directory / (phase + '-' + context + '-cluster.attempted')).touch()
                        for index, (service, operation) in enumerate(order[:failure_index + 1]):
                            key = (phase, context, 'cluster', service, operation)
                            row = dict(zip(('phase', 'context', 'perspective', 'service', 'operation'), key))
                            row.update(status='failed' if index == failure_index else 'passed', reason='command_failed' if index == failure_index else 'none')
                            (directory / ('-'.join(key) + '.json')).write_text(json.dumps(row))
                        with mock.patch.dict(os.environ, {**settings_environment(temp), 'STEP_RESULTS': '{}'}):
                            exec(compile(receipt, 'cluster receipt', 'exec'), {})
                        text = Path(temp, 'configuration-smoke-receipt', 'receipt.txt').read_text()
                        for index, (service, operation) in enumerate(order):
                            status, reason = (('passed', 'none') if index < failure_index else
                                              ('failed', 'command_failed') if index == failure_index else ('not_attempted', 'none'))
                            self.assertIn(f'{phase} {context} cluster {service} {operation}: {status} reason={reason}', text)
                        if failure_index:
                            first = directory / (phase + '-' + context + '-cluster-eventhubs-dns.json')
                            first.unlink()
                            with mock.patch.dict(os.environ, {**settings_environment(temp), 'STEP_RESULTS': '{}'}):
                                exec(compile(receipt, 'gap receipt', 'exec'), {})
                            text = Path(temp, 'configuration-smoke-receipt', 'receipt.txt').read_text()
                            self.assertIn(f'{phase} {context} cluster eventhubs dns: unavailable reason=unavailable', text)
                            if failure_index < 3:
                                service, operation = order[failure_index + 1]
                                self.assertIn(f'{phase} {context} cluster {service} {operation}: unavailable reason=unavailable', text)

    def test_probe_receipt_rejects_contamination_and_preserves_observations(self):
        code = self.by_id['receipt']['run'].split("python3 - <<'PYTHON'\n", 1)[1].split('\nPYTHON', 1)[0]
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp, 'configuration-isolation-probes')
            directory.mkdir()
            def record(phase, context, perspective, service, operation, status):
                key = '-'.join((phase, context, perspective, service, operation))
                row = dict(phase=phase, context=context, perspective=perspective,
                           service=service, operation=operation, status=status,
                           reason={'passed': 'none', 'timeout': 'timeout', 'failed': 'dns_error'}[status])
                Path(directory, key + '.json').write_text(json.dumps(row))
                return Path(directory, key + '.json')
            record('C0', 'idle', 'runner', 'eventhubs', 'dns', 'passed')
            record('C0', 'idle', 'runner', 'eventhubs', 'tcp', 'timeout')
            record('C1', 'settings', 'runner', 'blob', 'dns', 'failed')
            Path(directory, 'C0-active-cluster.attempted').touch()
            record('C0', 'active', 'cluster', 'eventhubs', 'dns', 'passed')
            bad = record('C0', 'active', 'cluster', 'eventhubs', 'tcp', 'passed')
            bad.write_text('{"phase":"C0","secret":"10.9.8.7 private exception"}')
            with mock.patch.dict(os.environ, {**settings_environment(temp), 'STEP_RESULTS': '{}'}):
                exec(compile(code, 'probe receipt fixture', 'exec'), {})
            text = Path(temp, 'configuration-smoke-receipt', 'receipt.txt').read_text()
            for line in ('C0 idle runner eventhubs dns: passed reason=none',
                         'C0 idle runner eventhubs tcp: timeout reason=timeout',
                         'C1 settings runner blob dns: failed reason=dns_error',
                         'C0 active cluster eventhubs dns: passed reason=none',
                         'C0 active cluster eventhubs tcp: unavailable reason=unavailable',
                         'C1 idle runner blob tcp: not_attempted reason=none'):
                self.assertIn(line, text)
            self.assertNotIn('10.9.8.7', text)
            self.assertNotIn('private exception', text)
            duplicate = directory / 'C0-idle-runner-eventhubs-dns.json'
            duplicate.write_text('{"phase":"C0","phase":"private duplicate","context":"idle","perspective":"runner","service":"eventhubs","operation":"dns","status":"passed","reason":"none"}')
            with mock.patch.dict(os.environ, {**settings_environment(temp), 'STEP_RESULTS': '{}'}):
                exec(compile(code, 'duplicate probe receipt fixture', 'exec'), {})
            text = Path(temp, 'configuration-smoke-receipt', 'receipt.txt').read_text()
            self.assertIn('C0 idle runner eventhubs dns: unavailable reason=unavailable', text)
            self.assertNotIn('private duplicate', text)

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

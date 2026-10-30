"""Synthetic command-contract tests; real judge bytes are checked at gate 2."""
import contextlib
import copy
import importlib.util
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

REPO = Path(__file__).resolve().parents[4]
SPEC = importlib.util.spec_from_file_location('phase', REPO / 'infra/scripts/phase/phase.py')
phase = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(phase)
SHA = 'a' * 40
DIGESTS = {'loadgen': 'sha256:' + 'b' * 64, 'verifier': 'sha256:' + 'c' * 64}
BATCH_DIGESTS = {**DIGESTS, 'enrichment': 'sha256:' + 'd' * 64}
RESOURCE = '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-synthetic/providers/Microsoft.ContainerService/managedClusters/aks-synthetic'
PERSISTENT = {'acr': {'value': {'name': 'parchwellacr', 'login_server': 'parchwellacr.azurecr.io'}}, 'application_insights_connection_string': {'value': 'InstrumentationKey=00000000-0000-0000-0000-000000000001'}}
ACCOUNTS = {'loadgen': {'name': 'loadgen', 'client_id': 'synthetic-loadgen', 'subject': 'system:serviceaccount:parchwell:loadgen'}, 'verifier': {'name': 'verifier', 'client_id': 'synthetic-verifier', 'subject': 'system:serviceaccount:parchwell:verifier'}, 'enrichment-consumer': {'name': 'unused'}}
EPHEMERAL = {'eventhubs': {'value': {'namespace_name': 'eh-synthetic', 'bootstrap_servers': 'eh-synthetic.servicebus.windows.net:9093', 'topics': {'events': 'document-events', 'state': 'document-state', 'dlq': 'document-events-dlq'}}}, 'blob': {'value': {'account_url': 'https://synthetic.blob.core.windows.net', 'containers': {'ledger': 'ledger', 'verifier': 'verifier', 'claim_check': 'claim-check'}}}, 'private_endpoint_ips': {'value': {'eventhubs': '10.0.0.4', 'blob': '10.0.0.5'}}}
DEADLINES = {'start-line': 150, 'loadgen': 1140, 'verify': 750, 'judge': 90}
BATCH_DEADLINES = {'single-action': 1980, 'single-action-judge': 120, 'single-action-seed': 420}
BATCH_MATRIX = (
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


class ScriptedCommands:
    """Fail selected synthetic commands and record every requested process."""
    def __init__(self):
        self.calls = []
        self.responses = {}
        self.download = b''
        self.properties = []
        self.artifact_mutator = None

    def __call__(self, instance, args, seconds, output=None, maximum=4 * 1024 * 1024):
        args = list(map(str, args))
        self.calls.append((args, seconds, output, maximum))
        key, default = tuple(args), (0, '')
        if args[0] == 'helm':
            document = json.loads(Path(args[-1]).read_text())
            values = document['runtime']
            name = phase.job_name(values['run_id'], values['phase'], values['mode'], values.get('repeat'), document['scenario_id'], document['configuration_id'], values.get('action_role'))
            deadline = {**DEADLINES, **BATCH_DEADLINES}[values['mode']]
            default = (0, f'kind: Job\nmetadata:\n  name: "{name}"\nspec:\n  activeDeadlineSeconds: {deadline}\n')
        elif len(args) == 4 and args[:3] == ['bash', 'infra/scripts/lifecycle/lifecycle.sh', 'collect-job']:
            job = args[-1]
            values = json.loads((instance.private / f'values-{job}.json').read_text())['runtime']
            if values['mode'] in BATCH_DEADLINES:
                directory = instance.artifacts / job
                directory.mkdir()
                role = values['action_role']
                worker = 'enrichment' if values['mode'] == 'single-action-seed' else 'verifier' if values['mode'] == 'single-action-judge' else 'loadgen'
                pod = {'metadata': {'name': job + '-pod', 'uid': '00000000-0000-4000-8000-000000000213'},
                       'spec': {'serviceAccountName': role, 'containers': [{'name': role, 'image': values[worker + '_image']}],
                                'initContainers': [{'name': 'azure-wi-proxy-init', 'image': 'synthetic-init'}]},
                       'status': {'containerStatuses': [{'name': role, 'state': {'terminated': {'exitCode': 0}}}]}}
                pods = {'apiVersion': 'v1', 'kind': 'List', 'items': [pod]}
                collection = {'complete': True, 'smoke_marker_valid': False, 'gaps': []}
                logs = {f'{job}-pod.containers.{role}.log': 'worker startup\n' + json.dumps({'mode': 'single-action-seed-smoke', 'native_loaded': True, 'uid_expected': 1654} if worker == 'enrichment' else {'native_loaded': True, 'scenario_files': 3, 'uid_expected': 1654}) + '\n',
                        f'{job}-pod.initContainers.azure-wi-proxy-init.log': 'init log without worker marker\n'}
                if self.artifact_mutator:
                    self.artifact_mutator(job, pods, collection, logs)
                (directory / 'pods.json').write_text(json.dumps(pods))
                (directory / 'collection-status.json').write_text(json.dumps(collection))
                for name, content in logs.items():
                    (directory / name).write_text(content)
        elif args[:3] == ['kubectl', 'get', 'pods']:
            default = (0, '{"items": []}')
        elif args[:4] == ['az', 'storage', 'blob', 'show']:
            default = self.properties.pop(0) if self.properties else (0, json.dumps({'etag': 'synthetic-etag', 'contentLength': len(self.download)}))
        elif args[:4] == ['az', 'storage', 'blob', 'download']:
            Path(args[args.index('--file') + 1]).write_bytes(self.download)
        elif args[:4] == ['az', 'acr', 'repository', 'show']:
            worker = args[args.index('--image') + 1].split('/')[1].split(':')[0].split('@')[0]
            default = (0, BATCH_DIGESTS[worker] + '\n')
        elif len(args) > 2 and args[2] == 'resolve':
            default = (0, json.dumps(['10.0.0.4' if 'servicebus' in args[3] else '10.0.0.5']))
        code, content = self.responses.get(key, default)
        if output is not None:
            Path(output).write_bytes(content.encode() if isinstance(content, str) else content)
        return code


class PhaseTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.env_file = self.root / 'env'
        self.output_file = self.root / 'output'
        self.summary_file = self.root / 'summary'
        self.env_file.touch()
        self.output_file.touch()
        env = {'PATH': os.environ.get('PATH', ''), 'RUNNER_TEMP': str(self.root), 'GITHUB_ENV': str(self.env_file), 'GITHUB_OUTPUT': str(self.output_file), 'GITHUB_STEP_SUMMARY': str(self.summary_file), 'GITHUB_RUN_ID': '40121134428', 'GITHUB_RUN_ATTEMPT': '1', 'GITHUB_SHA': SHA, 'PARCHWELL_REPOSITORY': str(REPO), 'PARCHWELL_CLUSTER_RESOURCE_ID': RESOURCE, 'S_CLUSTER_BACKEND': 'synthetic-backend', 'S_CLUSTER_TFVARS': '{}', 'S_EPHEMERAL_BACKEND': 'synthetic-backend', 'S_EPHEMERAL_TFVARS': '{}', 'S_PERSISTENT_OUTPUTS': json.dumps(PERSISTENT)}
        self.environment = patch.dict(os.environ, env, clear=True)
        self.environment.start()
        self.addCleanup(self.environment.stop)
        self.fake = ScriptedCommands()
        self.delays = []

    def main(self, *argv):
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            return phase.main(list(argv))

    def prepare(self, selected='C0', run=None):
        self.assertEqual(0, self.main('prepare', selected))
        os.environ.update(dict(line.split('=', 1) for line in self.env_file.read_text().splitlines()))
        self.private = Path(os.environ['PARCHWELL_PRIVATE_DIRECTORY'])
        self.artifacts = Path(os.environ['PARCHWELL_ARTIFACT_DIRECTORY'])
        self.candidate_dir = Path(os.environ['PARCHWELL_CANDIDATE_DIRECTORY'])
        state = json.loads((self.private / 'phase.json').read_text())
        if run:
            state['run_id'] = run
            os.environ['PARCHWELL_RUN_ID'] = run
        state['images'] = {worker: {'digest': digest, 'reference': f'parchwellacr.azurecr.io/parchwell/{worker}@{digest}'} for worker, digest in DIGESTS.items()}
        (self.private / 'phase.json').write_text(json.dumps(state))
        (self.private / 'cluster-output.json').write_text(json.dumps({'workload_identity': {'value': {'namespace': 'parchwell', 'service_accounts': ACCOUNTS}}}))
        (self.private / 'ephemeral-output.json').write_text(json.dumps(EPHEMERAL))
        self.run_id = state['run_id']
        self.selected = selected

    def mocked(self):
        stack = contextlib.ExitStack()
        stack.enter_context(patch.object(phase.Phase, 'run', autospec=True, side_effect=self.fake))
        stack.enter_context(patch.object(phase.Phase, 'sleep', side_effect=self.delays.append))
        return stack

    def records(self):
        return json.loads((self.artifacts / 'phase-status.json').read_text())['modes']

    def name(self, mode, repeat=1):
        return phase.job_name(self.run_id, self.selected, mode, repeat)

    def wait(self, mode, repeat=1):
        return ('bash', 'infra/scripts/lifecycle/lifecycle.sh', 'wait-job', self.name(mode, repeat), str(DEADLINES[mode]))

    def collect(self, mode, repeat=1):
        return ('bash', 'infra/scripts/lifecycle/lifecycle.sh', 'collect-job', self.name(mode, repeat))

    def delete(self, mode, repeat=1):
        return ('kubectl', 'delete', 'job', self.name(mode, repeat), '-n', 'parchwell', '--ignore-not-found', '--cascade=foreground', '--wait=true', '--timeout=90s', '--request-timeout=30s')

    def confirm(self, mode, repeat=1):
        return ('kubectl', 'get', 'pods', '-n', 'parchwell', '-l', f'job-name={self.name(mode, repeat)}', '-o', 'json', '--request-timeout=30s')

    def candidate(self, gate='pass'):
        return {'schema_version': 1, 'synthetic': True, 'scenario_id': 'A01', 'run_id': self.run_id, 'phase': self.selected, 'configuration_id': 'C3' if self.selected == 'C3' else 'C0', 'source_commit': SHA, 'status': 'complete_valid', 'gate': gate, 'image_digests': dict(DIGESTS), 'repeats': [{'repeat': n, 'status': 'complete_valid'} for n in range(1, 6)]}

    def test_names(self):
        self.assertEqual('a01-1-1-c0-r1-loadgen', phase.job_name('a01-1-1', 'C0', 'loadgen', 1))
        self.assertEqual('a01-1-1-probe-r5-verify', phase.job_name('a01-1-1', 'probe', 'verify', 5))
        self.assertEqual('a01-1-1-c3-judge', phase.job_name('a01-1-1', 'C3', 'judge'))
        self.assertEqual('a01-1-1/C3/candidate.json', phase.candidate_blob('a01-1-1', 'C3'))

    def test_validate_scenario_allowlist(self):
        for selected in ('A01', 'B04'):
            with self.subTest(scenario=selected), patch.dict(os.environ, {'PARCHWELL_SCENARIO': selected}):
                self.assertEqual(0, self.main('validate-scenario'))
        for selected in ('A02', 'b04', ''):
            with self.subTest(scenario=selected), patch.dict(os.environ, {'PARCHWELL_SCENARIO': selected}):
                self.assertEqual(2, self.main('validate-scenario'))
        with patch.dict(os.environ, {}, clear=True):
            self.assertEqual(0, self.main('validate-scenario'))

    def test_allow_images_requires_both_immutable_digests(self):
        cases = [(DIGESTS['loadgen'], DIGESTS['verifier'], True)]
        invalid = [None, '', 'sha256:bad', 'sha256:' + 'a' * 63]
        cases.extend((value, DIGESTS['verifier'], False) for value in invalid)
        cases.extend((DIGESTS['loadgen'], value, False) for value in invalid)
        for loadgen, verifier, allowed in cases:
            with self.subTest(loadgen=loadgen, verifier=verifier):
                self.output_file.write_text('')
                env = {'GITHUB_OUTPUT': str(self.output_file)}
                if loadgen is not None:
                    env['LOADGEN_DIGEST'] = loadgen
                if verifier is not None:
                    env['VERIFIER_DIGEST'] = verifier
                with patch.dict(os.environ, env, clear=True), contextlib.redirect_stdout(io.StringIO()) as output:
                    self.assertEqual(0, phase.main(['allow-images']))
                expected = 'workload=' + str(allowed).lower() + '\n'
                self.assertEqual(expected, output.getvalue())
                self.assertEqual(expected, self.output_file.read_text())

    def test_scenario_commands_reject_extra_arguments_without_output(self):
        for command in ('validate-scenario', 'allow-images'):
            with self.subTest(command=command), contextlib.redirect_stdout(io.StringIO()) as output:
                self.output_file.write_text('')
                self.assertEqual(2, phase.main([command, 'extra']))
                self.assertEqual('', output.getvalue())
                self.assertEqual('', self.output_file.read_text())

    def test_prepare_probe(self):
        self.assertEqual(0, self.main('prepare', 'probe'))
        exported = dict(line.split('=', 1) for line in self.env_file.read_text().splitlines())
        self.assertEqual('a01-40121134428-1', exported['PARCHWELL_RUN_ID'])
        self.assertEqual('A01', json.loads((Path(exported['PARCHWELL_PRIVATE_DIRECTORY']) / 'phase.json').read_text())['scenario'])
        self.assertEqual({'PARCHWELL_PRIVATE_DIRECTORY', 'PARCHWELL_CANDIDATE_DIRECTORY', 'PARCHWELL_ARTIFACT_DIRECTORY', 'PARCHWELL_CLUSTER_BACKEND_CONFIG', 'PARCHWELL_CLUSTER_TFVARS_PATH', 'PARCHWELL_EPHEMERAL_BACKEND_CONFIG', 'PARCHWELL_EPHEMERAL_TFVARS_PATH', 'PARCHWELL_RUN_ID', 'KUBECONFIG'}, set(exported))
        directories = [Path(exported[key]) for key in ('PARCHWELL_PRIVATE_DIRECTORY', 'PARCHWELL_CANDIDATE_DIRECTORY', 'PARCHWELL_ARTIFACT_DIRECTORY')]
        self.assertEqual(3, len(set(directories)))
        for directory in directories:
            self.assertEqual(0o700, directory.stat().st_mode & 0o777)
            self.assertFalse(any(directory in other.parents for other in directories))
        for name in ('cluster_backend.hcl', 'cluster_tfvars.tfvars.json', 'ephemeral_backend.hcl', 'ephemeral_tfvars.tfvars.json', 'persistent-output.json'):
            self.assertEqual(0o600, (directories[0] / name).stat().st_mode & 0o777)
        self.assertEqual({'phase': 'probe', 'run_id': 'a01-40121134428-1', 'modes': [], 'candidate': None, 'safety_stop': False}, json.loads((directories[2] / 'phase-status.json').read_text()))

    def test_prepare_rejections(self):
        cases = [('c0', {}), ('probe', {'S_CLUSTER_BACKEND': ''}), ('probe', {'S_PERSISTENT_OUTPUTS': '{"acr": {}, "acr": {}}'}), ('probe', {'S_PERSISTENT_OUTPUTS': 'x' * 4194305}), ('probe', {'GITHUB_SHA': 'A' * 40}), ('probe', {'GITHUB_RUN_ID': '1' * 30}), ('probe', {'S_PERSISTENT_OUTPUTS': json.dumps({**PERSISTENT, 'acr': {'name': 'Parchwell', 'login_server': 'parchwellacr.azurecr.io'}})})]
        for selected, overrides in cases:
            with self.subTest(phase=selected, overrides=list(overrides)), patch.dict(os.environ, overrides):
                self.assertEqual(2, self.main('prepare', selected))
                self.assertEqual('', self.env_file.read_text())

    def test_prepare_b04_c0_and_c1(self):
        os.environ['PARCHWELL_SCENARIO'] = 'B04'
        for selected in ('C0', 'C1'):
            with self.subTest(phase=selected):
                self.env_file.write_text('')
                self.assertEqual(0, self.main('prepare', selected))
                exported = dict(line.split('=', 1) for line in self.env_file.read_text().splitlines())
                os.environ.update(exported)
                private = Path(exported['PARCHWELL_PRIVATE_DIRECTORY'])
                state = json.loads((private / 'phase.json').read_text())
                self.assertEqual({'scenario': 'B04', 'phase': selected, 'configuration': selected,
                                  'run_id': f'b04-40121134428-1', 'source_commit': SHA}, state)
                self.assertEqual('B04', state['scenario'])
                self.assertEqual(selected, state['configuration'])
                self.assertEqual('b04-40121134428-1', state['run_id'])
                self.private = private
                self.artifacts = Path(exported['PARCHWELL_ARTIFACT_DIRECTORY'])
                self.candidate_dir = Path(exported['PARCHWELL_CANDIDATE_DIRECTORY'])
                state['images'] = {worker: {'digest': digest, 'reference': f'parchwellacr.azurecr.io/parchwell/{worker}@{digest}'} for worker, digest in DIGESTS.items()}
                (private / 'phase.json').write_text(json.dumps(state))
                (private / 'cluster-output.json').write_text(json.dumps({'workload_identity': {'value': {'namespace': 'parchwell', 'service_accounts': ACCOUNTS}}}))
                (private / 'ephemeral-output.json').write_text(json.dumps(EPHEMERAL))
                values = phase.Phase().values('loadgen', 1)
                self.assertEqual('B04', values['scenario_id'])
                self.assertEqual(selected, values['configuration_id'])

    def test_prepare_b04_rejections(self):
        cases = [('B04', 'probe'), ('B04', 'C3'), ('A01', 'C1'), ('b04', 'C0')]
        for selected_scenario, selected_phase in cases:
            with self.subTest(scenario=selected_scenario, phase=selected_phase):
                self.env_file.write_text('')
                with patch.dict(os.environ, {'PARCHWELL_SCENARIO': selected_scenario}, clear=False):
                    if selected_scenario == 'A01':
                        os.environ.pop('PARCHWELL_SCENARIO', None)
                    self.assertEqual(2, self.main('prepare', selected_phase))
                self.assertEqual('', self.env_file.read_text())

    def test_b04_candidate_scenario_matches_phase(self):
        os.environ['PARCHWELL_SCENARIO'] = 'B04'
        self.prepare('C1')
        candidate = self.candidate(gate=None)
        candidate.update(scenario_id='B04', configuration_id='C1')
        content = json.dumps(candidate).encode()
        self.assertEqual(candidate, phase.Phase().check_candidate(content))
        candidate['scenario_id'] = 'A01'
        with self.assertRaises(phase.Failure):
            phase.Phase().check_candidate(json.dumps(candidate).encode())

    def test_v2_stop_rejects_case_variants_and_invalid_selection_before_private_or_azure_work(self):
        rejected = [('probe-batch-2', 'batch', 'b08'), ('probe-batch-2', 'batch', 'B08,e01'),
                    ('probe-batch-2', 'batch', 'F06,B08'), ('probe-batch-2', 'Batch', 'B08'),
                    ('probe-batch-2', 'smoke', 'ALL'), ('PROBE-BATCH-2', 'batch', 'B08')]
        for selected, kind, items in rejected:
            with self.subTest(selected=selected, kind=kind, items=items), patch.dict(os.environ,
                    PARCHWELL_SCENARIO=selected, PARCHWELL_RUN_KIND=kind, PARCHWELL_BATCH_ITEMS=items):
                before = set(self.root.iterdir())
                environment_before, output_before = self.env_file.read_bytes(), self.output_file.read_bytes()
                with patch.object(phase.Phase, 'run') as run:
                    self.assertEqual(2, self.main('prepare-stop'))
                    run.assert_not_called()
                self.assertEqual(before, set(self.root.iterdir()))
                self.assertEqual(environment_before, self.env_file.read_bytes())
                self.assertEqual(output_before, self.output_file.read_bytes())

    def test_v2_stop_accepts_each_admitted_selector_independently_of_workload_failure(self):
        for kind, items in [('smoke', 'all'), *[('batch', v) for v in
                ('all', 'B08', 'E01', 'F06', 'B08,E01', 'B08,F06', 'E01,F06', 'B08,E01,F06')]]:
            with self.subTest(kind=kind, items=items), patch.dict(os.environ,
                    PARCHWELL_SCENARIO='probe-batch-2', PARCHWELL_RUN_KIND=kind, PARCHWELL_BATCH_ITEMS=items):
                with patch.object(phase.Phase, 'run') as run:
                    self.assertEqual(0, self.main('prepare-stop'))
                    run.assert_not_called()
                self.assertIn('private_directory=', self.output_file.read_text())

    def test_prepare_stop(self):
        env = {key: os.environ[key] for key in ('RUNNER_TEMP', 'GITHUB_ENV', 'GITHUB_OUTPUT', 'PARCHWELL_CLUSTER_RESOURCE_ID')}
        with patch.dict(os.environ, env, clear=True):
            self.assertEqual(0, self.main('prepare-stop'))
            directory = dict(line.split('=', 1) for line in self.output_file.read_text().splitlines())['private_directory']
            self.assertEqual(0o700, Path(directory).stat().st_mode & 0o777)
            os.environ['PARCHWELL_CLUSTER_RESOURCE_ID'] = 'invalid'
            self.assertEqual(2, self.main('prepare-stop'))

    def test_allow_c3(self):
        for probe, allowed, full, expected in [('success', 'true', 'success', 'true'), ('success', 'false', 'skipped', 'false'), ('failure', '', 'skipped', 'false'), ('success', 'true', 'failure', 'false')]:
            with self.subTest(probe=probe, allowed=allowed, full=full):
                self.output_file.write_text('')
                os.environ.update(PROBE_RESULT=probe, FULL_ALLOWED=allowed, FULL_C0_RESULT=full)
                self.assertEqual(0, self.main('allow-c3'))
                self.assertEqual(f'workload={expected}\n', self.output_file.read_text())

    def test_values_exact(self):
        self.prepare(run='a01-1-1')
        expected = {'scenario_id': 'A01', 'configuration_id': 'C0', 'runtime': {'run_id': 'a01-1-1', 'phase': 'C0', 'mode': 'loadgen', 'repeat': 1, 'loadgen_job_name': 'a01-1-1-c0-r1-loadgen', 'source_commit': SHA, 'loadgen_image': 'parchwellacr.azurecr.io/parchwell/loadgen@' + DIGESTS['loadgen'], 'verifier_image': 'parchwellacr.azurecr.io/parchwell/verifier@' + DIGESTS['verifier'], 'service_accounts': {'loadgen': ACCOUNTS['loadgen'], 'verifier': ACCOUNTS['verifier']}, 'eventhubs': EPHEMERAL['eventhubs']['value'], 'blob': EPHEMERAL['blob']['value'], 'eventhubs_ip': '10.0.0.4', 'blob_ip': '10.0.0.5', 'application_insights_connection_string': 'InstrumentationKey=00000000-0000-0000-0000-000000000001'}}
        self.assertEqual(expected, phase.Phase().values('loadgen', 1))
        judge = phase.Phase().values('judge', None)['runtime']
        self.assertNotIn('repeat', judge)
        self.assertNotIn('loadgen_job_name', judge)
        self.assertNotIn('enrichment-consumer', judge['service_accounts'])

    def test_run_repeats_happy(self):
        self.prepare()
        with self.mocked():
            self.assertEqual(0, self.main('run-repeats'))
        expected = []
        for repeat in range(1, 6):
            for mode in ('start-line', 'loadgen', 'verify'):
                name = self.name(mode, repeat)
                expected.extend([['helm', 'template', 'parchwell', 'charts/parchwell', '--values', str(self.private / f'values-{name}.json')], ['kubectl', 'apply', '-f', str(self.private / f'rendered-{name}.yaml'), '--request-timeout=30s'], list(self.wait(mode, repeat)), list(self.collect(mode, repeat))])
        self.assertEqual(expected, [call[0] for call in self.fake.calls])
        self.assertEqual([self.name(mode, repeat) for repeat in range(1, 6) for mode in ('start-line', 'loadgen', 'verify')], [record['job'] for record in self.records()])
        self.assertEqual(['complete'] * 15, [record['outcome'] for record in self.records()])

    def test_failed_mode_continues(self):
        for mode in ('start-line', 'loadgen', 'verify'):
            with self.subTest(mode=mode):
                self.prepare()
                self.fake = ScriptedCommands()
                self.fake.responses[self.wait(mode, 2)] = (1, '')
                with self.mocked():
                    self.assertEqual(0, self.main('run-repeats'))
                self.assertEqual(15, len(self.records()))
                self.assertEqual('failed', next(r for r in self.records() if r['job'] == self.name(mode, 2))['outcome'])

    def test_unknown_wait_deletes_then_continues(self):
        self.prepare()
        self.fake.responses[self.wait('loadgen')] = (124, '')
        with self.mocked():
            self.assertEqual(1, self.main('run-repeats'))
        calls = [tuple(c[0]) for c in self.fake.calls]
        index = calls.index(self.delete('loadgen'))
        self.assertEqual(self.confirm('loadgen'), calls[index + 1])
        self.assertEqual(15, len(self.records()))
        self.assertEqual('unknown', self.records()[1]['outcome'])

    def test_safety_stop(self):
        self.prepare()
        self.fake.responses[self.wait('loadgen')] = (124, '')
        self.fake.responses[self.confirm('loadgen')] = (0, '{"items": [{"metadata": {"name": "p"}}]}')
        with self.mocked():
            self.assertEqual(1, self.main('run-repeats'))
            before = len(self.fake.calls)
            self.assertEqual(1, self.main('judge'))
            self.assertEqual(before, len(self.fake.calls))
        self.assertEqual(['complete', 'unknown'] + ['not_attempted'] * 14, [r['outcome'] for r in self.records()])
        self.assertEqual(self.name('judge', None), self.records()[-1]['job'])
        self.assertEqual(2, sum(c[0][0] == 'helm' for c in self.fake.calls))

    def test_render_failures(self):
        for code, text in [(1, ''), (0, 'kind: Job\nkind: Job\n  name: "{name}"\n  activeDeadlineSeconds: 750\n'), (0, 'kind: Job\n  activeDeadlineSeconds: 750\n'), (0, 'kind: Job\n  name: "{name}"\n  activeDeadlineSeconds: 0\n')]:
            with self.subTest(code=code, text=text):
                self.prepare()
                self.fake = ScriptedCommands()
                name = self.name('verify', 3)
                args = ('helm', 'template', 'parchwell', 'charts/parchwell', '--values', str(self.private / f'values-{name}.json'))
                self.fake.responses[args] = (code, text.format(name=name))
                with self.mocked():
                    self.assertEqual(1, self.main('run-repeats'))
                self.assertEqual(15, len(self.records()))
                self.assertEqual('render_failed', self.records()[8]['outcome'])
                self.assertFalse(any(c[0][:2] == ['kubectl', 'apply'] and name in c[0][3] for c in self.fake.calls))

    def test_apply_failure_deletes(self):
        self.prepare()
        args = ('kubectl', 'apply', '-f', str(self.private / f'rendered-{self.name("start-line")}.yaml'), '--request-timeout=30s')
        self.fake.responses[args] = (1, '')
        with self.mocked():
            self.assertEqual(1, self.main('run-repeats'))
        calls = [tuple(c[0]) for c in self.fake.calls]
        self.assertNotIn(self.wait('start-line'), calls)
        self.assertIn(self.delete('start-line'), calls)
        self.assertIn(self.confirm('start-line'), calls)
        self.assertEqual(15, len(self.records()))
        self.assertEqual('apply_failed', self.records()[0]['outcome'])

    def test_collect_codes(self):
        self.prepare()
        self.fake.responses[self.collect('start-line')] = (1, '')
        with self.mocked():
            self.assertEqual(0, self.main('run-repeats'))
        self.assertTrue(self.records()[0]['collect_gap'])
        self.prepare()
        self.fake = ScriptedCommands()
        self.fake.responses[self.collect('start-line')] = (2, '')
        with self.mocked():
            self.assertEqual(1, self.main('run-repeats'))
        self.assertEqual('collect_error', self.records()[0]['outcome'])

    def test_judge_failed_is_recorded(self):
        self.prepare()
        self.fake.responses[self.wait('judge', None)] = (1, '')
        with self.mocked():
            self.assertEqual(0, self.main('judge'))
        self.assertEqual('failed', self.records()[0]['outcome'])

    def test_candidate_gates(self):
        for gate in ('pass', 'untestable_at_build_scale', 'inconclusive'):
            with self.subTest(gate=gate):
                self.prepare('probe')
                self.output_file.write_text('')
                self.fake.download = (json.dumps(self.candidate(gate), indent=2) + '\n').encode()
                with self.mocked():
                    self.assertEqual(0, self.main('candidate'))
                self.assertEqual(self.fake.download, (self.candidate_dir / 'candidate.json').read_bytes())
                self.assertEqual(f'full_allowed={str(gate == "pass").lower()}\n', self.output_file.read_text())

    def test_candidate_rejections(self):
        self.prepare('probe')
        base = self.candidate()
        cases = [('duplicate', b'{"a":1,"a":2}'), ('constant', b'{"a": NaN}')]
        for key, value in [('schema_version', True), ('schema_version', 2), ('synthetic', False), ('run_id', 'wrong'), ('phase', 'C3'), ('configuration_id', 'C3'), ('source_commit', 'f' * 40), ('image_digests', {**DIGESTS, 'loadgen': 'sha256:' + 'd' * 64}), ('image_digests', {**DIGESTS, 'extra': 'x'}), ('repeats', base['repeats'][:4]), ('repeats', [base['repeats'][i] for i in (0, 1, 2, 4, 3)]), ('status', 'complete')]:
            cases.append((key, json.dumps({**base, key: value}).encode()))
        cases.extend([('C0 gate', json.dumps({**base, 'phase': 'C0'}).encode()), ('oversize', b' ' * 16777217)])
        for label, content in cases:
            with self.subTest(case=label):
                self.output_file.write_text('')
                self.fake.download = content
                self.delays.clear()
                if label == 'C0 gate':
                    state = json.loads((self.private / 'phase.json').read_text())
                    state['phase'] = 'C0'
                    (self.private / 'phase.json').write_text(json.dumps(state))
                with self.mocked():
                    self.assertEqual(1, self.main('candidate'))
                self.assertEqual('full_allowed=false\n', self.output_file.read_text())
                self.assertFalse((self.candidate_dir / 'candidate.json').exists())
                self.assertEqual([], self.delays)

    def test_candidate_retry(self):
        self.prepare('probe')
        self.fake.download = json.dumps(self.candidate()).encode()
        self.fake.properties = [(1, '')]
        with self.mocked():
            self.assertEqual(0, self.main('candidate'))
        self.assertEqual([1], self.delays)

    def test_candidate_exhausted(self):
        self.prepare('probe')
        self.fake.download = json.dumps(self.candidate()).encode()
        self.fake.properties = [(0, json.dumps({'etag': 'synthetic', 'contentLength': len(self.fake.download) + 1}))] * 3
        with self.mocked():
            self.assertEqual(1, self.main('candidate'))
        self.assertEqual([1, 2], self.delays)
        self.assertFalse((self.candidate_dir / 'candidate.json').exists())
        self.fake.calls.clear()
        self.delays.clear()
        self.fake.properties = [(0, '{"etag": "synthetic", "contentLength": 16777217}')]
        with self.mocked():
            self.assertEqual(1, self.main('candidate'))
        self.assertEqual([], self.delays)
        self.assertFalse(any(c[0][:4] == ['az', 'storage', 'blob', 'download'] for c in self.fake.calls))

    def test_v2_image_readback_timeout_recovers_without_republishing(self):
        self.prepare('probe')
        state = phase.Phase().read('phase.json')
        state['scenario'] = 'probe-batch-2'
        state.pop('images')
        phase.write_json(self.private / 'phase.json', state)
        commands = self.root / 'commands'
        commands.mkdir()
        script = '''import json, os, sys, time
from pathlib import Path
root = Path(os.environ['IMAGE_FIXTURE_ROOT'])
name, args = Path(sys.argv[0]).name, sys.argv[1:]
with (root / 'image-calls.jsonl').open('a') as stream:
    stream.write(json.dumps([name, args]) + '\\n')
if name == 'az' and args[:3] == ['acr', 'repository', 'show']:
    verifier = 'parchwell/verifier' in args[args.index('--image') + 1]
    marker = root / 'first-verifier-read'
    if verifier and not marker.exists():
        marker.touch()
        print('sha256:' + 'd' * 64, flush=True)
        time.sleep(30)
    print('sha256:' + ('c' if verifier else 'b') * 64)
'''
        for name in ('az', 'dotnet'):
            executable = commands / name
            executable.write_text('#!' + sys.executable + '\n' + script)
            executable.chmod(0o700)
        real_run = phase.Phase.run
        def bounded(instance, args, seconds, output=None, maximum=4 * phase.MIB):
            if args[:4] == ['az', 'acr', 'repository', 'show']:
                seconds = 1
            return real_run(instance, args, seconds, output, maximum)
        with patch.dict(os.environ, {'PATH': str(commands) + os.pathsep + os.environ['PATH'],
                                     'IMAGE_FIXTURE_ROOT': str(self.root)}), patch.object(
                phase.Phase, 'run', autospec=True, side_effect=bounded):
            self.assertEqual(0, self.main('publish-images'))
        calls = [json.loads(line) for line in (self.root / 'image-calls.jsonl').read_text().splitlines()]
        self.assertEqual(2, sum(name == 'dotnet' for name, _ in calls))
        reads = [args for name, args in calls if name == 'az' and args[:3] == ['acr', 'repository', 'show']]
        self.assertEqual(3, len(reads))
        self.assertEqual(reads[1], reads[2])
        self.assertEqual(DIGESTS['verifier'], (self.private / 'verifier-digest.txt').read_text().strip())
        self.assertFalse((self.private / 'verifier-digest.txt.incomplete').exists())
        self.assertEqual(DIGESTS, {worker: value['digest'] for worker, value in phase.Phase().read('phase.json')['images'].items()})
        self.assertEqual(f'loadgen_digest={DIGESTS["loadgen"]}\nverifier_digest={DIGESTS["verifier"]}\n', self.output_file.read_text())

    def test_publish_images_argv(self):
        self.prepare('probe')
        with self.mocked():
            self.assertEqual(0, self.main('publish-images'))
        expected = [['az', 'acr', 'login', '--name', 'parchwellacr']]
        for worker, project in [('loadgen', 'LoadGen'), ('verifier', 'Verifier')]:
            expected.extend([['dotnet', 'publish', f'src/Parchwell.{project}/Parchwell.{project}.csproj', '--configuration', 'Release', '-p:RestoreLockedMode=true', '-p:NuGetLockFilePath=packages.linux-x64.lock.json', '-r', 'linux-x64', '--self-contained', 'false', '/t:PublishContainer', '-p:ContainerRegistry=parchwellacr.azurecr.io', '-p:ContainerImageTag=' + SHA], ['az', 'acr', 'repository', 'show', '--name', 'parchwellacr', '--image', f'parchwell/{worker}:{SHA}', '--query', 'digest', '--output', 'tsv']])
        self.assertEqual(expected, [c[0] for c in self.fake.calls])
        self.assertEqual(f'loadgen_digest={DIGESTS["loadgen"]}\nverifier_digest={DIGESTS["verifier"]}\n', self.output_file.read_text())
        self.fake.responses[tuple(expected[2])] = (0, 'sha256:XYZ\n')
        with self.mocked():
            self.assertEqual(1, self.main('publish-images'))

    def test_v2_image_readback_failure_retries_only_the_read(self):
        for status in (1, 124):
            with self.subTest(status=status):
                self.prepare('probe')
                state = phase.Phase().read('phase.json')
                state['scenario'] = 'probe-batch-2'
                state.pop('images')
                phase.write_json(self.private / 'phase.json', state)
                self.fake.calls.clear()
                reads = []
                def command(instance, args, seconds, output=None, maximum=4 * phase.MIB):
                    if args[:4] == ['az', 'acr', 'repository', 'show'] and 'verifier' in args[7]:
                        reads.append(list(args))
                        if len(reads) == 1:
                            self.fake.responses[tuple(args)] = (status, 'sha256:' + 'd' * 64)
                            result = self.fake(instance, args, seconds, output, maximum)
                            del self.fake.responses[tuple(args)]
                            return result
                    return self.fake(instance, args, seconds, output, maximum)
                with patch.object(phase.Phase, 'run', autospec=True, side_effect=command):
                    self.assertEqual(0, self.main('publish-images'))
                self.assertEqual(2, len(reads))
                self.assertEqual(reads[0], reads[1])
                self.assertEqual(2, sum(call[0][:2] == ['dotnet', 'publish'] for call in self.fake.calls))
                self.assertTrue(all(call[1] == 60 and call[3] == 4096 for call in self.fake.calls if call[0][:4] == ['az', 'acr', 'repository', 'show']))
                self.assertEqual(DIGESTS['verifier'], phase.Phase().read('phase.json')['images']['verifier']['digest'])

    def test_image_readback_exhaustion_preserves_status_and_never_publishes_partial_map(self):
        for scenario, status, attempts in (('probe-batch-2', 1, 2), ('probe-batch-2', 124, 2),
                                            ('probe-batch-2', 125, 1), ('probe-batch-2', 127, 1),
                                            ('probe-batch-2', 130, 1), ('probe-batch-2', 143, 1),
                                            ('A01', 124, 1), ('probe-batch', 124, 1)):
            with self.subTest(scenario=scenario, status=status):
                self.prepare('probe')
                state = phase.Phase().read('phase.json')
                state['scenario'] = scenario
                state.pop('images')
                phase.write_json(self.private / 'phase.json', state)
                self.fake.calls.clear()
                key = tuple(phase.Phase().image_command('verifier', ':' + SHA, PERSISTENT['acr']['value']))
                self.fake.responses[key] = (status, 'partial')
                self.output_file.write_text('')
                diagnostics = io.StringIO()
                with self.mocked(), contextlib.redirect_stderr(diagnostics):
                    with self.assertRaisesRegex(phase.Failure, f'image readback failed: verifier.*exit={status}'):
                        phase.Phase().publish_images()
                del self.fake.responses[key]
                self.assertEqual(attempts, sum(tuple(call[0]) == key for call in self.fake.calls))
                self.assertEqual(2, sum(call[0][:2] == ['dotnet', 'publish'] for call in self.fake.calls))
                self.assertNotIn('images', phase.Phase().read('phase.json'))
                self.assertEqual('', self.output_file.read_text())
                self.assertNotIn('partial', diagnostics.getvalue())

    def test_v2_invalid_digest_or_publication_failure_does_not_retry(self):
        for operation, content in (('read', ''), ('read', 'sha256:bad'), ('publish', '')):
            with self.subTest(operation=operation, content=content):
                self.prepare('probe')
                state = phase.Phase().read('phase.json')
                state['scenario'] = 'probe-batch-2'
                state.pop('images')
                phase.write_json(self.private / 'phase.json', state)
                self.fake.calls.clear()
                key = tuple(phase.Phase().image_command('loadgen', ':' + SHA, PERSISTENT['acr']['value']))
                if operation == 'read':
                    self.fake.responses[key] = (0, content)
                else:
                    def command(instance, args, seconds, output=None, maximum=4 * phase.MIB):
                        result = self.fake(instance, args, seconds, output, maximum)
                        return 1 if args[:2] == ['dotnet', 'publish'] else result
                with self.mocked() if operation == 'read' else patch.object(phase.Phase, 'run', autospec=True, side_effect=command):
                    self.assertEqual(1, self.main('publish-images'))
                self.fake.responses.pop(key, None)
                self.assertEqual(1 if operation == 'read' else 0, sum(tuple(call[0]) == key for call in self.fake.calls))
                self.assertEqual(1, sum(call[0][:2] == ['dotnet', 'publish'] for call in self.fake.calls))
                self.assertNotIn('images', phase.Phase().read('phase.json'))

    def test_use_images(self):
        self.prepare()
        with self.mocked():
            self.assertEqual(2, self.main('use-images', 'sha256:' + 'b' * 63, DIGESTS['verifier']))
            self.assertEqual([], self.fake.calls)
            self.assertEqual(0, self.main('use-images', DIGESTS['loadgen'], DIGESTS['verifier']))
        state = json.loads((self.private / 'phase.json').read_text())
        self.assertEqual('parchwellacr.azurecr.io/parchwell/loadgen@' + DIGESTS['loadgen'], state['images']['loadgen']['reference'])
        self.assertEqual('parchwellacr.azurecr.io/parchwell/verifier@' + DIGESTS['verifier'], state['images']['verifier']['reference'])
        self.fake.responses[tuple(self.fake.calls[0][0])] = (0, DIGESTS['verifier'])
        with self.mocked():
            self.assertEqual(1, self.main('use-images', DIGESTS['loadgen'], DIGESTS['verifier']))

    def test_check_dns(self):
        for code, response, expected in [(0, '["10.0.0.4"]', 0), (0, '["10.0.0.4", "20.1.1.1"]', 1), (124, '', 1)]:
            with self.subTest(code=code, response=response):
                self.prepare()
                self.fake = ScriptedCommands()
                self.fake.responses[(os.sys.executable, 'infra/scripts/phase/phase.py', 'resolve', 'eh-synthetic.servicebus.windows.net')] = (code, response)
                with self.mocked():
                    self.assertEqual(expected, self.main('check-dns'))

    def test_delete_workload_argv(self):
        self.prepare()
        with self.mocked():
            self.assertEqual(0, self.main('delete-workload'))
        self.assertEqual([['kubectl', 'delete', 'jobs', '-n', 'parchwell', '-l', f'app.kubernetes.io/instance={self.run_id}', '--ignore-not-found', '--wait=false', '--request-timeout=30s']], [c[0] for c in self.fake.calls])

    def test_report(self):
        os.environ.update(STEP_RESULTS=json.dumps({'guard': {'outcome': 'success'}, 'stop': {'outcome': 'success'}}, indent=2), JOB_STATUS='success', REQUIRED_STEPS='guard stop', WORKLOAD_STEPS='destroy', WORKLOAD='false', PARCHWELL_PHASE='C3')
        self.assertEqual(0, self.main('report'))
        os.environ.update(STEP_RESULTS=json.dumps({'guard': {'outcome': 'success'}, 'stop': {'outcome': 'success'}, 'destroy': {'outcome': 'failure'}}), WORKLOAD='true')
        self.assertEqual(1, self.main('report'))
        self.assertIn('destroy: failure', self.summary_file.read_text())

    def test_b04_report(self):
        os.environ.update(PARCHWELL_SCENARIO='B04', PARCHWELL_PHASE='C1', STEP_RESULTS='{}',
                          JOB_STATUS='success', WORKLOAD='true', REQUIRED_STEPS='', WORKLOAD_STEPS='')
        self.assertEqual(0, self.main('report'))
        self.assertTrue(self.summary_file.read_text().startswith('## B04 C1: passed'))

    def test_selected_metadata_summary_line(self):
        self.prepare_v2_batch('authentication-controls', 'true')
        os.environ.update(STEP_RESULTS='{}', JOB_STATUS='success', WORKLOAD='false', REQUIRED_STEPS='',
                          WORKLOAD_STEPS='', PARCHWELL_SCENARIO='probe-batch-2', PARCHWELL_PHASE='single-action')
        self.assertEqual(0, self.main('report'))
        self.assertIn('Selected authentication controls: F06-C6,F06-C9', self.summary_file.read_text())

    def prepare_batch(self, items='all'):
        os.environ.update(PARCHWELL_SCENARIO='probe-batch', PARCHWELL_RUN_KIND='smoke', PARCHWELL_BATCH_ITEMS=items)
        self.assertEqual(0, self.main('prepare-batch'))
        os.environ.update(dict(line.split('=', 1) for line in self.env_file.read_text().splitlines()))
        self.private = Path(os.environ['PARCHWELL_PRIVATE_DIRECTORY'])
        self.artifacts = Path(os.environ['PARCHWELL_ARTIFACT_DIRECTORY'])
        self.candidate_dir = Path(os.environ['PARCHWELL_CANDIDATE_DIRECTORY'])
        state = json.loads((self.private / 'phase.json').read_text())
        state['images'] = {worker: {'digest': digest, 'reference': f'parchwellacr.azurecr.io/parchwell/{worker}@{digest}'} for worker, digest in BATCH_DIGESTS.items()}
        (self.private / 'phase.json').write_text(json.dumps(state))
        accounts = copy.deepcopy(ACCOUNTS)
        for role in ('probe', 'enrichment-consumer'):
            accounts[role] = {'name': role, 'client_id': 'synthetic-' + role, 'subject': 'system:serviceaccount:parchwell:' + role}
        (self.private / 'cluster-output.json').write_text(json.dumps({'workload_identity': {'value': {'namespace': 'parchwell', 'service_accounts': accounts}}}))
        ephemeral = copy.deepcopy(EPHEMERAL)
        ephemeral['blob']['value']['containers']['single_action'] = 'single-action'
        (self.private / 'ephemeral-output.json').write_text(json.dumps(ephemeral))
        self.run_id = state['run_id']
        phase.Phase().start_batch_clock()

    def batch_name(self, mode, item='B01', configuration='C0'):
        suffix = 'single-judge' if mode == 'single-action-judge' else 'single'
        return self.run_id + ('-f04-seed' if mode == 'single-action-seed' else f'-{item.lower()}-{configuration.lower()}-{suffix}')

    def prepare_v2_batch(self, items='all', capture='false', run_kind='smoke'):
        os.environ.update(PARCHWELL_SCENARIO='probe-batch-2', PARCHWELL_RUN_KIND=run_kind,
                          PARCHWELL_BATCH_ITEMS=items, PARCHWELL_CAPTURE_AUTHENTICATION_METADATA=capture)
        self.assertEqual(0, self.main('prepare-batch'))
        self.env_file_contents = self.env_file.read_text()
        os.environ.update(dict(line.split('=', 1) for line in self.env_file_contents.splitlines()))
        self.private = Path(os.environ['PARCHWELL_PRIVATE_DIRECTORY'])
        self.artifacts = Path(os.environ['PARCHWELL_ARTIFACT_DIRECTORY'])
        self.candidate_dir = Path(os.environ['PARCHWELL_CANDIDATE_DIRECTORY'])
        phase_data = json.loads((self.private / 'phase.json').read_text())
        phase_data['images'] = {worker: {'digest': digest, 'reference': f'parchwellacr.azurecr.io/parchwell/{worker}@{digest}'} for worker, digest in BATCH_DIGESTS.items() if worker in ('loadgen', 'verifier')}
        (self.private / 'phase.json').write_text(json.dumps(phase_data))
        accounts = copy.deepcopy(ACCOUNTS)
        for role in ('probe', 'enrichment-consumer'):
            accounts[role] = {'name': role, 'client_id': 'synthetic-' + role, 'subject': 'system:serviceaccount:parchwell:' + role}
        (self.private / 'cluster-output.json').write_text(json.dumps({'workload_identity': {'value': {'namespace': 'parchwell', 'service_accounts': accounts}}}))
        ephemeral = copy.deepcopy(EPHEMERAL)
        ephemeral['blob']['value']['containers']['single_action'] = 'single-action'
        ephemeral['probe_v2_role_scope'] = {'value': {'namespace_id': 'synthetic', 'topics': {}, 'accounts': {}, 'assignments': {}, 'probe_namespace_data_roles': True}}
        (self.private / 'ephemeral-output.json').write_text(json.dumps(ephemeral))
        self.run_id = phase_data['run_id']
        phase.Phase().start_batch_clock()

    def test_v2_selection_values_are_frozen_and_exact(self):
        with self.mocked():
            for items, capture, expected in [('all', 'false', ['B08', 'E01', 'F06']),
                                             ('authentication-controls', 'true', ['F06'])]:
                self.env_file.write_text('')
                self.prepare_v2_batch(items, capture)
                saved = json.loads((self.private / 'phase.json').read_text())
                self.assertEqual(items, saved['probe_v2_selection'])
                self.assertIs(capture == 'true', saved['capture_authentication_metadata'])
                self.assertEqual(expected, saved['items'])
            for capture in ('TRUE', '', '1', ' true', 'false '):
                self.env_file.write_text('')
                os.environ.update(PARCHWELL_SCENARIO='probe-batch-2', PARCHWELL_RUN_KIND='smoke',
                                  PARCHWELL_BATCH_ITEMS='authentication-controls', PARCHWELL_CAPTURE_AUTHENTICATION_METADATA=capture)
                self.assertEqual(2, self.main('validate-batch'))
                self.assertEqual(2, self.main('prepare-batch'))
                self.assertEqual('', self.env_file.read_text())
            for scenario, kind, items, capture in [
                    ('A01', 'smoke', 'all', 'true'), ('B04', 'smoke', 'all', 'true'),
                    ('probe-batch', 'smoke', 'all', 'true'), ('probe-batch-2', 'batch', 'all', 'true'),
                    ('probe-batch-2', 'smoke', 'all', 'true'), ('probe-batch-2', 'smoke', 'authentication-controls', 'false'),
                    ('probe-batch-2', 'smoke', 'authentication-controls', None), ('probe-batch-2', 'smoke', 'B08', 'false')]:
                self.env_file.write_text('')
                env = {'PARCHWELL_SCENARIO': scenario, 'PARCHWELL_RUN_KIND': kind, 'PARCHWELL_BATCH_ITEMS': items}
                if capture is not None:
                    env['PARCHWELL_CAPTURE_AUTHENTICATION_METADATA'] = capture
                with self.subTest(case=(scenario, kind, items, capture)), patch.dict(os.environ, env):
                    if capture is None:
                        os.environ.pop('PARCHWELL_CAPTURE_AUTHENTICATION_METADATA', None)
                    command = 'validate-scenario' if scenario in ('A01', 'B04') else 'validate-batch'
                    self.assertEqual(2, self.main(command))
                    if command == 'validate-batch':
                        self.assertEqual(2, self.main('prepare-batch'))
                    self.assertEqual('', self.env_file.read_text())
                    self.assertEqual([], self.fake.calls)
        self.env_file.write_text('')
        with patch.dict(os.environ, {'PARCHWELL_SCENARIO': 'A01', 'PARCHWELL_CAPTURE_AUTHENTICATION_METADATA': 'true'}):
            self.assertEqual(2, self.main('prepare', 'C0'))
        self.assertEqual('', self.env_file.read_text())
        self.assertEqual([], self.fake.calls)

    def test_v2_subset_report_retains_unselected_item_aliases(self):
        os.environ.update(PARCHWELL_SCENARIO='probe-batch-2', PARCHWELL_RUN_KIND='batch', PARCHWELL_BATCH_ITEMS='B08')
        self.assertEqual(0, self.main('prepare-batch'))
        os.environ.update(dict(line.split('=', 1) for line in self.env_file.read_text().splitlines()))
        instance = phase.Phase()
        state = instance.state()
        state['not_selected'] = ['E01', 'F06']
        instance.save_state(state)
        os.environ.update(STEP_RESULTS='{}', JOB_STATUS='success', WORKLOAD='false', REQUIRED_STEPS='')
        self.assertEqual(0, self.main('report'))
        summary = self.summary_file.read_text()
        self.assertIn('E01: not selected', summary)
        self.assertIn('F06: not selected', summary)
        self.assertEqual(['E01', 'F06'], instance.state()['not_selected'])
        self.assertEqual([], self.fake.calls)

    def test_v2_preparation_selection_roles_and_smoke_only_private_confirmation(self):
        for kind, items, roles in [('smoke', 'all', True), ('batch', 'B08', False), ('batch', 'E01', True),
                                   ('batch', 'F06', True), ('batch', 'B08,F06', True)]:
            with self.subTest(kind=kind, items=items), patch.dict(os.environ, {
                    'PARCHWELL_SCENARIO': 'probe-batch-2', 'PARCHWELL_RUN_KIND': kind,
                    'PARCHWELL_BATCH_ITEMS': items, 'S_PROBE_V2_EFFECTIVE_SCOPE': 'private-confirmation'}):
                self.env_file.write_text('')
                self.assertEqual(0, self.main('prepare-batch'))
                private = Path(dict(line.split('=', 1) for line in self.env_file.read_text().splitlines())['PARCHWELL_PRIVATE_DIRECTORY'])
                self.assertIs(roles, json.loads((private / 'ephemeral_tfvars.tfvars.json').read_text())['probe_namespace_data_roles'])
                confirmation = private / 'probe-v2/effective-scope.json'
                self.assertEqual(kind == 'smoke', confirmation.exists())
                if kind == 'smoke':
                    self.assertEqual('private-confirmation', confirmation.read_text())
                self.assertNotIn('private-confirmation', self.env_file.read_text())

    def test_v2_invalid_selection_precedes_private_and_azure_work(self):
        for kind, items in [('smoke', 'B08'), ('batch', ''), ('batch', 'F06,B08'), ('batch', 'B08,B08'),
                            ('batch', 'B08, E01'), ('batch', 'F04'), ('unknown', 'all')]:
            with self.subTest(kind=kind, items=items), patch.dict(os.environ, {
                    'PARCHWELL_SCENARIO': 'probe-batch-2', 'PARCHWELL_RUN_KIND': kind, 'PARCHWELL_BATCH_ITEMS': items}):
                self.assertEqual(2, self.main('prepare-batch'))
                self.assertEqual('', self.env_file.read_text())
                self.assertEqual([], self.fake.calls)
                self.assertEqual([], list(self.root.glob('parchwell-*')))

    def test_real_preparation_scientific_selectors_reach_selection_and_entrypoints(self):
        with self.mocked():
            for selector in ('all', 'B08', 'E01', 'F06', 'B08,E01', 'B08,F06', 'E01,F06', 'B08,E01,F06'):
                with self.subTest(selector=selector):
                    self.env_file.write_text('')
                    self.prepare_v2_batch(selector, run_kind='batch')
                    frozen = json.loads((self.private / 'phase.json').read_text())
                    items = ['B08', 'E01', 'F06'] if selector == 'all' else selector.split(',')
                    probe = phase.Phase().probe_v2()
                    counts = {'B08': 2, 'E01': 2, 'F06': 12}
                    expected = tuple((item, f'C{number}') for item in ('B08', 'E01', 'F06') if item in items
                                     for number in range(counts[item]))
                    self.assertEqual(items, frozen['items'])
                    self.assertEqual(selector, frozen['probe_v2_selection'])
                    self.assertEqual(('batch', expected), probe.selection(frozen))
                    with patch.object(probe, 'run_batch', return_value=7), patch.object(phase.Phase, 'probe_v2', return_value=probe):
                        self.assertEqual(0, phase.Phase().single_action_seed())
                        self.assertEqual(7, phase.Phase().run_single_action_batch())
            self.assertEqual([], self.fake.calls)

    def test_batch_selection_and_strict_command_arguments(self):
        os.environ.update(PARCHWELL_SCENARIO='probe-batch', PARCHWELL_RUN_KIND='smoke')
        with self.mocked():
            self.assertEqual(0, self.main('validate-batch'))
            for kind in ('smoke', 'batch'):
                for items in ('all', 'C08,F04', 'F04', 'C08'):
                    with self.subTest(run_kind=kind, items=items), patch.dict(os.environ, {
                            'PARCHWELL_RUN_KIND': kind, 'PARCHWELL_BATCH_ITEMS': items}):
                        self.assertEqual(0, self.main('validate-batch'))
                        self.assertEqual(0, self.main('prepare-batch'))
                        private = Path(dict(line.split('=', 1) for line in self.env_file.read_text().splitlines())['PARCHWELL_PRIVATE_DIRECTORY'])
                        expected = ['B01', 'B03', 'B05', 'C08', 'C12', 'F04'] if items == 'all' else items.split(',')
                        self.assertEqual(expected, json.loads((private / 'phase.json').read_text())['items'])
                        self.env_file.write_text('')
                for items in ('F04,C08', 'C08,C08', 'E01', ''):
                    with self.subTest(run_kind=kind, items=items), patch.dict(os.environ, {
                            'PARCHWELL_RUN_KIND': kind, 'PARCHWELL_BATCH_ITEMS': items}):
                        for command in ('validate-batch', 'prepare-batch'):
                            with contextlib.redirect_stderr(io.StringIO()) as error:
                                self.assertEqual(2, phase.main([command]))
                            self.assertEqual('unsupported probe batch selection', error.getvalue().strip())
                        self.assertEqual('', self.env_file.read_text())
                        self.assertEqual([], self.fake.calls)
            os.environ.pop('PARCHWELL_BATCH_ITEMS', None)
            self.assertEqual(0, self.main('prepare-batch'))
            private = Path(dict(line.split('=', 1) for line in self.env_file.read_text().splitlines())['PARCHWELL_PRIVATE_DIRECTORY'])
            self.assertEqual(['B01', 'B03', 'B05', 'C08', 'C12', 'F04'],
                             json.loads((private / 'phase.json').read_text())['items'])
            self.env_file.write_text('')
            for selected, kind in [('probe-batch', 'unknown'), ('A01', 'smoke'), ('unknown', 'smoke')]:
                with self.subTest(scenario=selected, run_kind=kind), patch.dict(os.environ, {'PARCHWELL_SCENARIO': selected, 'PARCHWELL_RUN_KIND': kind}):
                    with contextlib.redirect_stderr(io.StringIO()) as error:
                        self.assertEqual(2, phase.main(['validate-batch']))
                    self.assertEqual(2, self.main('prepare-batch'))
                    self.assertEqual('', self.env_file.read_text())
            for command in ('validate-batch', 'prepare-batch', 'single-action-seed', 'run-single-action-batch'):
                self.assertEqual(2, self.main(command, 'extra'))
            self.assertEqual(2, self.main('prepare', 'C0'))
        self.assertEqual([], self.fake.calls)

    def test_batch_preparation_privacy_and_metadata(self):
        self.prepare_batch()
        metadata = json.loads((self.private / 'phase.json').read_text())
        self.assertEqual({'scenario': 'probe-batch', 'phase': 'single-action', 'configuration': None,
                          'run_id': 'probe-40121134428-1', 'source_commit': SHA, 'run_kind': 'smoke',
                          'items': ['B01', 'B03', 'B05', 'C08', 'C12', 'F04']},
                         {key: value for key, value in metadata.items() if key not in ('images', 'batch_started_monotonic', 'batch_clock_boot_id', 'batch_clock_run_id', 'batch_clock_source')})
        self.assertEqual({'phase': 'single-action', 'run_id': self.run_id, 'modes': [], 'candidate': None, 'safety_stop': False, 'not_selected': []}, phase.Phase().state())
        for directory in (self.private, self.artifacts, self.candidate_dir):
            self.assertEqual(0o700, directory.stat().st_mode & 0o777)
        self.assertNotEqual(self.private, self.artifacts)
        for name in ('phase.json', 'persistent-output.json', 'cluster_backend.hcl', 'cluster_tfvars.tfvars.json', 'ephemeral_backend.hcl', 'ephemeral_tfvars.tfvars.json'):
            self.assertEqual(0o600, (self.private / name).stat().st_mode & 0o777)
            self.assertFalse((self.artifacts / name).exists())
        self.assertEqual([], list(self.candidate_dir.iterdir()))
        for overrides in ({'GITHUB_RUN_ID': '1' * 30}, {'GITHUB_SHA': 'invalid'}, {'S_CLUSTER_BACKEND': ''}):
            self.env_file.write_text('')
            with patch.dict(os.environ, overrides):
                self.assertEqual(2, self.main('prepare-batch'))
            self.assertEqual('', self.env_file.read_text())

    def test_batch_matrix_names_roles_images_and_sequence(self):
        self.assertEqual(BATCH_MATRIX, phase.SINGLE_ACTION_CONFIGURATIONS)
        self.prepare_batch()
        with self.mocked():
            self.assertEqual(0, self.main('single-action-seed'))
            self.assertEqual(1, len(self.records()))
            self.assertEqual(0, self.main('run-single-action-batch'))
        expected = [('F04', 'C2', 'single-action-seed', 'enrichment-consumer')]
        expected += [(item, configuration, mode, selected_role) for item, configuration, role in BATCH_MATRIX
                     for mode, selected_role in [('single-action', role), ('single-action-judge', 'verifier')]]
        self.assertEqual(33, len(self.records()))
        self.assertEqual(33, len({record['job'] for record in self.records()}))
        for record, (item, configuration, mode, role) in zip(self.records(), expected):
            with self.subTest(job=record['job']):
                self.assertEqual(self.batch_name(mode, item, configuration), record['job'])
                self.assertEqual((item, configuration, mode, role, True, True, 'complete'),
                                 (record['item'], record['configuration'], record['mode'], record['role'], record['image_smoke'], record['smoke_valid'], record['outcome']))
                values_path = self.private / f'values-{record["job"]}.json'
                document = json.loads(values_path.read_text())
                runtime = document['runtime']
                self.assertEqual((item, configuration, configuration, role, True),
                                 (document['scenario_id'], document['configuration_id'], runtime['phase'], runtime['action_role'], runtime['image_smoke']))
                self.assertEqual({'loadgen', 'verifier', 'probe', 'enrichment-consumer'}, set(runtime['service_accounts']))
                self.assertNotIn('repeat', runtime)
                self.assertNotIn('loadgen_job_name', runtime)
                worker = 'enrichment' if mode == 'single-action-seed' else 'verifier' if mode == 'single-action-judge' else 'loadgen'
                pod = json.loads((self.artifacts / record['job'] / 'pods.json').read_text())['items'][0]
                self.assertEqual(runtime[worker + '_image'], pod['spec']['containers'][0]['image'])
                self.assertEqual('single-action', runtime['blob']['containers']['single_action'])
                self.assertEqual(0o600, values_path.stat().st_mode & 0o777)
                self.assertFalse((self.artifacts / values_path.name).exists())
        self.assertEqual([record['job'] for record in self.records()],
                         [call[0][3] for call in self.fake.calls if call[0][:3] == ['bash', 'infra/scripts/lifecycle/lifecycle.sh', 'wait-job']])
        self.assertEqual(33, sum(call[0][:2] == ['kubectl', 'apply'] for call in self.fake.calls))
        self.assertFalse(any(call[0][0] == 'az' for call in self.fake.calls))
        self.assertIsNone(phase.Phase().state()['candidate'])
        self.assertEqual([], phase.Phase().state()['not_selected'])
        self.assertEqual({f'{item}-{config}-gap.json' for item, config, _ in BATCH_MATRIX}, {path.name for path in self.candidate_dir.iterdir()})

    def test_batch_seed_failure_is_failure_despite_legacy_failed_outcome(self):
        self.prepare_batch()
        job = self.batch_name('single-action-seed')
        self.fake.responses[('bash', 'infra/scripts/lifecycle/lifecycle.sh', 'wait-job', job, '420')] = (1, '')
        with self.mocked():
            self.assertEqual(0, self.main('single-action-seed'))
        self.assertEqual(1, len(self.records()))
        self.assertEqual(('failed', False), (self.records()[0]['outcome'], self.records()[0]['smoke_valid']))
        self.assertNotIn('failed', phase.BAD_OUTCOMES)

    def test_batch_failed_job_and_collect_failures_do_not_pass(self):
        for operation, code in [('wait-job', 1), ('collect-job', 1), ('collect-job', 2)]:
            with self.subTest(operation=operation, code=code):
                self.prepare_batch()
                self.fake = ScriptedCommands()
                job = self.batch_name('single-action')
                args = ('bash', 'infra/scripts/lifecycle/lifecycle.sh', operation, job)
                if operation == 'wait-job':
                    args += ('1980',)
                self.fake.responses[args] = (code, '')
                with self.mocked():
                    self.assertEqual(1, self.main('run-single-action-batch'))
                self.assertEqual(32, len(self.records()))
                self.assertFalse(self.records()[0]['smoke_valid'])
                self.assertTrue(all(record['smoke_valid'] for record in self.records()[1:]))
                self.assertEqual(32, sum(call[0][:2] == ['kubectl', 'apply'] for call in self.fake.calls))

    def test_batch_collected_pod_identity_exit_and_marker_contract(self):
        def mutate(case, _job, pods, collection, logs):
            pod = pods['items'][0]
            worker_log = next(name for name in logs if '.containers.' in name)
            if case == 'missing pod':
                pods['items'] = []
            elif case == 'duplicate pod':
                pods['items'].append(copy.deepcopy(pod))
            elif case == 'account':
                pod['spec']['serviceAccountName'] = 'probe'
            elif case == 'image':
                pod['spec']['containers'][0]['image'] = 'unexpected-image'
            elif case == 'exit':
                pod['status']['containerStatuses'][0]['state']['terminated']['exitCode'] = 1
            elif case == 'marker':
                logs[worker_log] = '{"native_loaded":true,"scenario_files":2,"uid_expected":1654}\n'
            elif case == 'marker types':
                logs[worker_log] = '{"native_loaded":1,"scenario_files":3,"uid_expected":1654}\n'
            elif case == 'missing log':
                del logs[worker_log]
            elif case == 'collection incomplete':
                collection['complete'] = False
            elif case == 'collection gap':
                collection['gaps'] = [{'kind': 'log_read_failed'}]
        for case in ('missing pod', 'duplicate pod', 'account', 'image', 'exit', 'marker', 'marker types', 'missing log', 'collection incomplete', 'collection gap'):
            with self.subTest(case=case):
                self.prepare_batch()
                self.fake = ScriptedCommands()
                self.fake.artifact_mutator = lambda *args: mutate(case, *args)
                with self.mocked():
                    self.assertEqual(0, self.main('single-action-seed'))
                self.assertFalse(self.records()[0]['smoke_valid'])
                self.assertTrue((self.artifacts / self.batch_name('single-action-seed') / 'pods.json').exists())

    def test_batch_uncertain_pod_safety_stop_preserves_all_records(self):
        self.prepare_batch()
        job = self.batch_name('single-action')
        self.fake.responses[('bash', 'infra/scripts/lifecycle/lifecycle.sh', 'wait-job', job, '1980')] = (124, '')
        confirm = ('kubectl', 'get', 'pods', '-n', 'parchwell', '-l', f'job-name={job}', '-o', 'json', '--request-timeout=30s')
        self.fake.responses[confirm] = (0, '{"items":[{"metadata":{"name":"uncertain"}}]}')
        with self.mocked():
            self.assertEqual(1, self.main('run-single-action-batch'))
        records = self.records()
        self.assertEqual(['unknown'] + ['not_attempted'] * 31, [record['outcome'] for record in records])
        self.assertTrue(all(record['reason'] == 'safety_stop' for record in records[1:]))
        self.assertTrue(phase.Phase().state()['safety_stop'])
        self.assertEqual(1, sum(call[0][:2] == ['kubectl', 'apply'] for call in self.fake.calls))
        calls = [tuple(call[0]) for call in self.fake.calls]
        deletion = ('kubectl', 'delete', 'job', job, '-n', 'parchwell', '--ignore-not-found', '--cascade=foreground', '--wait=true', '--timeout=90s', '--request-timeout=30s')
        self.assertEqual(confirm, calls[calls.index(deletion) + 1])

    def test_batch_start_cap_configuration_boundary(self):
        for elapsed, attempted in [(14399, 32), (14400, 0), (14401, 0)]:
            with self.subTest(elapsed=elapsed):
                self.prepare_batch()
                phase_data = phase.Phase().read('phase.json')
                phase_data['batch_started_monotonic'] = 100
                phase.write_json(self.private / 'phase.json', phase_data)
                self.fake = ScriptedCommands()
                with self.mocked(), patch.object(phase.time, 'monotonic', return_value=100 + elapsed):
                    self.assertEqual(int(attempted == 0), self.main('run-single-action-batch'))
                records = self.records()
                self.assertEqual(32, len(records))
                self.assertEqual(['complete'] * attempted + ['not_attempted'] * (32 - attempted), [record['outcome'] for record in records])
                self.assertTrue(all(record['reason'] == 'start_cap' for record in records[attempted:]))
                self.assertEqual(attempted, sum(call[0][:2] == ['kubectl', 'apply'] for call in self.fake.calls))
                self.assertFalse(any(record['smoke_valid'] for record in records[attempted:]))

    def test_expanded_blob_output_renders_legacy_a01_and_b04(self):
        for selected in ('A01', 'B04'):
            with self.subTest(scenario=selected):
                os.environ['PARCHWELL_SCENARIO'] = selected
                self.prepare('C0', run='synthetic-c6')
                ephemeral = copy.deepcopy(EPHEMERAL)
                ephemeral['blob']['value']['containers']['single_action'] = 'single-action'
                (self.private / 'ephemeral-output.json').write_text(json.dumps(ephemeral))
                self.assertEqual(DEADLINES['loadgen'], phase.Phase().render('loadgen', 1))
                self.assertEqual({'loadgen', 'verifier'}, set(phase.Phase().values('loadgen', 1)['runtime']['service_accounts']))

    def test_real_batch_chart_seed_action_judge(self):
        self.prepare_batch()
        # Synthetic GUIDs satisfy the chart account contract.
        cluster = json.loads((self.private / 'cluster-output.json').read_text())
        for index, account in enumerate(cluster['workload_identity']['value']['service_accounts'].values(), 1):
            account['client_id'] = f'00000000-0000-0000-0000-{index:012d}'
        (self.private / 'cluster-output.json').write_text(json.dumps(cluster))
        for mode, item, configuration, role in [('single-action-seed', 'F04', 'C2', 'enrichment-consumer'),
                                                ('single-action', 'C12', 'C2', 'probe'),
                                                ('single-action-judge', 'C12', 'C2', 'verifier')]:
            with self.subTest(mode=mode):
                self.assertEqual(BATCH_DEADLINES[mode], phase.Phase().render(mode, None, item, configuration, role))
                rendered = self.private / f'rendered-{self.batch_name(mode, item, configuration)}.yaml'
                text = rendered.read_text()
                self.assertIn('args: ["--image-smoke"]', text)
                self.assertIn(f'serviceAccountName: "{role}"', text)
                worker = 'enrichment' if mode == 'single-action-seed' else 'verifier' if mode == 'single-action-judge' else 'loadgen'
                self.assertIn(f'image: "parchwellacr.azurecr.io/parchwell/{worker}@{BATCH_DIGESTS[worker]}"', text)

    def test_real_chart(self):
        for selected, configuration in [('probe', 'C0'), ('C0', 'C0'), ('C3', 'C3')]:
            self.prepare(selected, run='synthetic-c6')
            for mode in ('start-line', 'loadgen', 'verify', 'judge'):
                with self.subTest(phase=selected, mode=mode):
                    repeat = None if mode == 'judge' else 1
                    self.assertEqual(DEADLINES[mode], phase.Phase().render(mode, repeat))
                    text = (self.private / f'rendered-{self.name(mode, repeat)}.yaml').read_text()
                    self.assertIn(f'  name: "synthetic-c6-{selected.lower()}-' + ('judge"' if mode == 'judge' else f'r1-{mode}"'), text)
                    self.assertIn(f'  activeDeadlineSeconds: {DEADLINES[mode]}', text)
                    if selected == 'C3' and mode == 'judge':
                        self.assertRegex(text, r'- name: "Verifier__CandidateOutputName"\s+value: "synthetic-c6/C3/candidate.json"')
                    if selected == 'C0' and mode == 'loadgen':
                        self.assertRegex(text, r'- name: "LoadGen__RepeatNumber"\s+value: "1"')
                        self.assertRegex(text, r'- name: "LoadGen__PodUid"\s+valueFrom: \{fieldRef: \{fieldPath: metadata\.uid\}\}')


if __name__ == '__main__':
    unittest.main()

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
from unittest.mock import Mock, patch

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
DEADLINES = {'start-line': 150, 'loadgen': 1140, 'verify': 750, 'judge': 90,
             'loadgen2': 1140, 'b10-sweep-line': 150, 'b10-sweep': 750}
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
LEDGER_RUN = 'ledger-40121134428-1'
LEDGER_STATS = (json.dumps({'schema_version': 1, 'raw_json': json.dumps({'ts': 1000, 'brokers': {'b0': {'req': {'Produce': 3}}, 'b1': {'req': {'Produce': 2, 'Metadata': 1}}, 'boot': {'req': {}}}, 'topics': {'document-events': {'batchcnt': {'cnt': 5, 'sum': 40, 'min': 1, 'max': 20, 'avg': 8, 'p50': 6}}}})}) + '\n'
                + json.dumps({'schema_version': 1, 'raw_json': json.dumps({'ts': 2000, 'brokers': {'b0': {'req': {'Produce': 7}}, 'b1': {'req': {'Produce': 4}}}, 'topics': {'document-events': {'batchcnt': {'cnt': 0, 'sum': 0, 'min': 0, 'max': 0, 'avg': 0, 'p50': 0}}}})}) + '\n').encode()
LEDGER_ROWS = (json.dumps({'delivered': True, 'delivery_latency_ms': 9.5}) + '\n' + json.dumps({'delivered': True, 'delivery_latency_ms': 4}) + '\n'
               + json.dumps({'delivered': False, 'delivery_latency_ms': None}) + '\n').encode()
LEDGER_CONFIGURATIONS = (('B06', 'C0'), ('B06', 'C1'), ('B07', 'C0'), ('B07', 'C1'))


class ScriptedCommands:
    """Fail selected synthetic commands and record every requested process."""
    def __init__(self):
        self.calls = []
        self.responses = {}
        self.download = b''
        self.properties = []
        self.artifact_mutator = None
        self.collect_hook = None
        self.blobs = {}

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
            if self.collect_hook:
                self.collect_hook(instance, job, values)
        elif args[:3] == ['kubectl', 'get', 'pods']:
            default = (0, '{"items": []}')
        elif args[:4] == ['az', 'storage', 'blob', 'show']:
            name = args[args.index('--name') + 1]
            if name in self.blobs:
                default = (0, json.dumps({'etag': 'synthetic-etag', 'contentLength': len(self.blobs[name])}))
            else:
                default = self.properties.pop(0) if self.properties else (0, json.dumps({'etag': 'synthetic-etag', 'contentLength': len(self.download)}))
        elif args[:4] == ['az', 'storage', 'blob', 'download']:
            name = args[args.index('--name') + 1]
            Path(args[args.index('--file') + 1]).write_bytes(self.blobs.get(name, self.download))
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

    def prepare_ledger(self, run_kind='smoke'):
        os.environ.update(PARCHWELL_SCENARIO='ledger-batch', PARCHWELL_RUN_KIND=run_kind, PARCHWELL_BATCH_ITEMS='all')
        self.assertEqual(0, self.main('prepare-batch'))
        os.environ.update(dict(line.split('=', 1) for line in self.env_file.read_text().splitlines()))
        self.private = Path(os.environ['PARCHWELL_PRIVATE_DIRECTORY'])
        self.artifacts = Path(os.environ['PARCHWELL_ARTIFACT_DIRECTORY'])
        self.candidate_dir = Path(os.environ['PARCHWELL_CANDIDATE_DIRECTORY'])
        state = json.loads((self.private / 'phase.json').read_text())
        state['images'] = {worker: {'digest': digest, 'reference': f'parchwellacr.azurecr.io/parchwell/{worker}@{digest}'} for worker, digest in DIGESTS.items()}
        (self.private / 'phase.json').write_text(json.dumps(state))
        (self.private / 'cluster-output.json').write_text(json.dumps({'workload_identity': {'value': {'namespace': 'parchwell', 'service_accounts': ACCOUNTS}}}))
        (self.private / 'ephemeral-output.json').write_text(json.dumps(EPHEMERAL))

        def producer_pod(instance, job, values):
            if values['mode'] == 'loadgen':
                directory = instance.artifacts / job
                directory.mkdir()
                (directory / 'pods.json').write_text(json.dumps({'items': [{'metadata': {'name': job + '-pod'}}]}))
        self.fake.collect_hook = producer_pod
        for item, configuration in LEDGER_CONFIGURATIONS:
            run = f'{LEDGER_RUN}-{item.lower()}'
            prefix = f'{run}/{configuration}/r1/producer/{run}-{configuration.lower()}-r1-loadgen-pod/publication/'
            self.fake.blobs[prefix + 'statistics.jsonl'] = LEDGER_STATS
            self.fake.blobs[prefix + 'ledger.jsonl'] = LEDGER_ROWS

    def prepare_b10(self, selected='C0'):
        os.environ['PARCHWELL_SCENARIO'] = 'B10'
        self.prepare(selected)
        self.clock = 100.0
        self.wall = 1_767_225_600.0  # Synthetic 2026-01-01 UTC.
        self.addCleanup(patch.stopall)
        patch.object(phase.time, 'monotonic', side_effect=lambda: self.clock).start()
        patch.object(phase.time, 'time', side_effect=lambda: self.wall).start()
        patch.object(phase.Phase, 'boot_identity', return_value='00000000-0000-0000-0000-000000000000').start()

        def collect(instance, job, values):
            if values['mode'] not in ('loadgen', 'loadgen2'):
                return
            path = instance.artifacts / job
            path.mkdir(exist_ok=True)
            pod = {'metadata': {'name': job + '-pod', 'uid': 'synthetic-uid',
                                'ownerReferences': [{'controller': True, 'kind': 'Job', 'name': job}]},
                   'spec': {'serviceAccountName': 'loadgen', 'containers': [
                       {'name': 'loadgen', 'image': values['loadgen_image'], 'env': [
                           {'name': 'PARCHWELL_SOURCE_COMMIT', 'value': SHA},
                           {'name': 'PARCHWELL_JOB_NAME', 'value': job}]}]},
                   'status': {'containerStatuses': [{'name': 'loadgen', 'state': {'terminated':
                       {'exitCode': 1, 'finishedAt': '2026-01-01T00:00:00Z'}}}]}}
            if getattr(self, 'b10_pod_mutator', None):
                self.b10_pod_mutator(pod, values)
            (path / 'pods.json').write_text(json.dumps({'items': [pod]}))
        self.fake.collect_hook = collect
        for repeat in phase.SLOTS:
            receipt = {'schema_version': 1, 'mode': 'verify', 'scenario_id': 'B10',
                       'configuration_id': selected, 'run_id': self.run_id, 'phase': selected, 'repeat': repeat,
                       'job_name': self.name('verify', repeat), 'pod_name': self.name('verify', repeat) + '-pod',
                       'source_commit': SHA, 'cleanup_completed': True, 'handler_exit_code': 1,
                       'started_utc': '2026-01-01T00:00:00.0000000+00:00',
                       'completed_utc': '2026-01-01T00:00:00.0000000+00:00',
                       'identity': {'scenario_id': 'B10', 'configuration_id': selected, 'run_id': self.run_id,
                                    'instance_id': self.name('loadgen', repeat) + '-pod',
                                    'expected_instance_id': self.name('loadgen', repeat) + '-pod', 'client_id': 'synthetic'}}
            self.fake.blobs[f'{self.run_id}/{selected}/r{repeat}/verify-completion.json'] = json.dumps(receipt).encode()

    def test_b10_runs_twenty_ordered_modes_then_repeat_free_capture_and_sweep(self):
        for selected in ('C0', 'C1'):
            with self.subTest(selected=selected):
                self.fake = ScriptedCommands()
                self.prepare_b10(selected)
                with self.mocked():
                    self.assertEqual(0, self.main('run-repeats'))
                    self.assertEqual(0, self.main('judge'))
                self.assertEqual([(repeat, mode) for repeat in phase.SLOTS for mode in phase.B10_MODES]
                                 + [(None, 'b10-sweep-line'), (None, 'b10-sweep'), (None, 'judge')],
                                 [(r['repeat'], r['mode']) for r in self.records()])
                self.assertEqual(('start-line', 'loadgen', 'verify'), phase.MODES)
                second = json.loads((self.private / f"values-{self.name('loadgen2')}.json").read_text())['runtime']
                self.assertEqual(self.name('loadgen'), second['loadgen_job_name'])
                for mode in (*phase.B10_PHASE_MODES, 'judge'):
                    runtime = json.loads((self.private / f"values-{self.name(mode, None)}.json").read_text())['runtime']
                    self.assertNotIn('repeat', runtime)
                    self.assertNotIn('loadgen_job_name', runtime)
                    self.assertEqual(mode == 'b10-sweep-line', 'b10_last_loadgen_terminated_utc' in runtime)
                self.assertEqual([60.0] * (1 if selected == 'C0' else 2), self.delays)
                state = phase.Phase().state()['b10_lifecycle']
                self.assertEqual((10, 5, []), (len(state['producers']), len(state['verifiers']), state['gaps']))
                self.assertTrue(all(item['exit_code'] == 1 for item in state['producers']))
                shows = [call for call in self.fake.calls if call[0][:4] == ['az', 'storage', 'blob', 'show']]
                self.assertEqual(5, len(shows))

    def test_b10_nonfinal_verifier_failure_is_retained_and_later_slots_run(self):
        self.prepare_b10()
        self.fake.responses[self.wait('verify')] = (1, '')
        with self.mocked():
            self.assertEqual(0, self.main('run-repeats'))
        self.assertEqual('failed', self.records()[3]['outcome'])
        self.assertEqual('complete', self.records()[19]['outcome'])
        self.assertEqual(22, len(self.records()))

    def test_b10_missing_first_termination_blocks_second_without_exit_success_gate(self):
        self.prepare_b10()
        self.b10_pod_mutator = lambda pod, values: pod['status']['containerStatuses'][0]['state'].clear() if values['repeat'] == 1 and values['mode'] == 'loadgen' else None
        with self.mocked():
            self.assertEqual(1, self.main('run-repeats'))
        second = self.records()[2]
        self.assertEqual(('not_attempted', 'first_producer_termination_unavailable'), (second['outcome'], second['reason']))
        self.assertNotIn(self.wait('loadgen2'), [tuple(call[0]) for call in self.fake.calls])
        self.assertIn(self.delete('loadgen'), [tuple(call[0]) for call in self.fake.calls])
        self.assertEqual(('not_attempted', 'lifecycle_evidence_gap'), (self.records()[20]['outcome'], self.records()[20]['reason']))
        self.assertEqual('complete', self.records()[21]['outcome'])
        self.assertFalse((self.private / f"values-{self.name('b10-sweep-line', None)}.json").exists())

    def test_b10_unknown_live_pod_stops_every_subsequent_launch(self):
        self.prepare_b10()
        self.fake.responses[self.wait('loadgen')] = (124, '')
        self.fake.responses[self.confirm('loadgen')] = (0, '{"items": [{}]}')
        with self.mocked():
            self.assertEqual(1, self.main('run-repeats'))
            self.assertEqual(1, self.main('judge'))
        self.assertEqual(2, sum(call[0][0] == 'helm' for call in self.fake.calls))
        self.assertEqual(23, len(self.records()))
        self.assertTrue(phase.Phase().state()['safety_stop'])
        self.assertTrue(all(record['reason'] == 'safety_stop' for record in self.records()[2:]))

    def test_b10_uses_actual_pod_time_and_latest_receipt_time(self):
        self.prepare_b10()
        self.b10_pod_mutator = lambda pod, values: pod['status']['containerStatuses'][0]['state']['terminated'].update(
            finishedAt='2026-01-01T00:00:02.1234567Z' if values['repeat'] == 5 else '2026-01-01T00:00:00Z')
        name = f'{self.run_id}/C0/r2/verify-completion.json'
        receipt = json.loads(self.fake.blobs[name])
        receipt['completed_utc'] = '2026-01-01T00:00:03.0000000+00:00'
        self.fake.blobs[name] = json.dumps(receipt).encode()
        with self.mocked():
            self.assertEqual(0, self.main('run-repeats'))
        runtime = phase.Phase().values('b10-sweep-line', None)['runtime']
        self.assertEqual('2026-01-01T00:00:02.1234567+00:00', runtime['b10_last_loadgen_terminated_utc'])
        self.assertEqual('2026-01-01T00:00:03.0000000+00:00', runtime['b10_last_verifier_completed_utc'])
        self.assertAlmostEqual(62.1234567, sum(self.delays), places=5)

    def test_b10_settle_boundary_waits_only_remaining_time(self):
        for elapsed, delay in ((59.999, .001), (60, 0), (60.001, 0)):
            with self.subTest(elapsed=elapsed):
                self.fake = ScriptedCommands()
                self.prepare_b10()
                self.wall += elapsed
                self.delays.clear()
                with self.mocked():
                    self.assertEqual(0, self.main('run-repeats'))
                self.assertAlmostEqual(delay, sum(self.delays), places=5)

    def test_b10_lifecycle_rejects_missing_malformed_and_wrong_binding_receipts(self):
        cases = [('completed_utc', None), ('completed_utc', 'not-a-time'),
                 ('completed_utc', '2026-01-01T00:00:00+01:00'), ('cleanup_completed', False),
                 ('schema_version', True), ('repeat', True), ('run_id', 'other'), ('source_commit', 'b' * 40),
                 ('job_name', 'other'), ('phase', 'C1'), ('configuration_id', 'C1'), ('scenario_id', 'B04'),
                 ('identity', None), ('started_utc', '2026-01-01T00:01:00Z')]
        for key, value in cases:
            with self.subTest(key=key, value=value):
                self.fake = ScriptedCommands()
                self.prepare_b10()
                name = f'{self.run_id}/C0/r1/verify-completion.json'
                receipt = json.loads(self.fake.blobs[name])
                receipt[key] = value
                self.fake.blobs[name] = json.dumps(receipt).encode()
                with self.mocked():
                    self.assertEqual(1, self.main('run-repeats'))
                self.assertEqual('lifecycle_evidence_gap', self.records()[20]['reason'])
                self.assertEqual(1, len(phase.Phase().state()['b10_lifecycle']['gaps']))

    def test_b10_pod_identity_and_source_cannot_supply_lifecycle_time(self):
        for mutation in (lambda p: p['metadata']['ownerReferences'][0].update(name='other'),
                         lambda p: p['spec']['containers'][0]['env'][0].update(value='b' * 40),
                         lambda p: p['status']['containerStatuses'][0]['state']['terminated'].update(finishedAt='bad')):
            with self.subTest(mutation=mutation):
                self.fake = ScriptedCommands()
                self.prepare_b10()
                self.b10_pod_mutator = lambda pod, values: mutation(pod) if values['repeat'] == 1 else None
                with self.mocked():
                    self.assertEqual(1, self.main('run-repeats'))
                self.assertEqual('not_attempted', self.records()[2]['outcome'])

    def test_b10_shared_deadline_stops_second_capture_sweep_and_judge(self):
        for last_mode in ('loadgen', 'verify', 'b10-sweep-line', 'b10-sweep'):
            with self.subTest(last_mode=last_mode):
                self.fake = ScriptedCommands()
                self.prepare_b10()
                original = self.fake.collect_hook
                def exhaust(instance, job, values):
                    original(instance, job, values)
                    if values['mode'] == last_mode and (values.get('repeat') in (None, 1) if last_mode == 'loadgen' else values.get('repeat') in (None, 5)):
                        self.clock = 18100
                self.fake.collect_hook = exhaust
                with self.mocked():
                    self.assertEqual(1 if last_mode != 'b10-sweep' else 0, self.main('run-repeats'))
                    self.assertEqual(1, self.main('judge'))
                self.assertEqual(23, len(self.records()))
                if last_mode == 'loadgen':
                    self.assertEqual('not_attempted', self.records()[2]['outcome'])
                self.assertEqual('orchestration_deadline', self.records()[-1]['reason'])
                self.assertEqual('not_attempted', self.records()[-1]['outcome'])
                clock = phase.Phase().read('phase.json')['b10_clock']
                self.assertEqual(18000, clock['deadline'] - clock['started'])

    def test_b10_judge_fails_closed_with_missing_or_wrong_clock(self):
        for mutation in (None, {'boot_id': 'other'}, {'run_id': 'other'}, {'source_commit': 'other'}, {'deadline': float('inf')}):
            with self.subTest(mutation=mutation):
                self.prepare_b10()
                if mutation is not None:
                    phase.Phase().b10_deadline(start=True)
                    document = phase.Phase().read('phase.json')
                    document['b10_clock'].update(mutation)
                    # strict JSON correctly rejects nonfinite values; use a finite invalid deadline here.
                    if mutation.get('deadline') == float('inf'):
                        document['b10_clock']['deadline'] = 999999
                    phase.write_json(self.private / 'phase.json', document)
                with self.mocked():
                    before = len(self.fake.calls)
                    self.assertEqual(1, self.main('judge'))
                self.assertEqual(before, len(self.fake.calls))
                self.assertEqual('orchestration_deadline', self.records()[-1]['reason'])

    def test_b10_receipt_missing_or_over_capacity_records_gap_without_retry(self):
        for size in (None, 65535, 65536, 65537):
            with self.subTest(content_length=size):
                self.fake = ScriptedCommands()
                self.prepare_b10()
                name = f'{self.run_id}/C0/r3/verify-completion.json'
                if size is None:
                    del self.fake.blobs[name]
                else:
                    self.fake.blobs[name] += b' ' * (size - len(self.fake.blobs[name]))
                rejected = size is None or size > 65536
                with self.mocked():
                    self.assertEqual(int(rejected), self.main('run-repeats'))
                if rejected:
                    self.assertEqual('lifecycle_evidence_gap', self.records()[20]['reason'])
                else:
                    self.assertEqual('complete', self.records()[20]['outcome'])
                shows = [call for call in self.fake.calls if call[0][:4] == ['az', 'storage', 'blob', 'show']]
                self.assertEqual(5, len(shows))
                self.assertEqual(1, sum(call[0][call[0].index('--name') + 1] == name for call in shows))
                self.assertEqual('complete', self.records()[21]['outcome'])

    def test_b10_lifecycle_budget_is_shared_across_all_fifteen_observations(self):
        self.prepare_b10()
        observe = phase.Phase.b10_pod_termination
        download = phase.Phase.download_blob
        deadlines = []
        def costly_observation(instance, record):
            result = observe(instance, record)
            self.clock += 2
            return result
        def costly_lookup(instance, name, target, maximum, deadline, container='verifier'):
            deadlines.append(deadline)
            self.assertEqual(65536, maximum)
            result = download(instance, name, target, maximum, deadline, container)
            self.clock += 25
            return result
        with self.mocked(), patch.object(phase.Phase, 'b10_pod_termination', autospec=True, side_effect=costly_observation) as pods, \
                patch.object(phase.Phase, 'download_blob', autospec=True, side_effect=costly_lookup) as receipts:
            self.assertEqual(1, self.main('run-repeats'))
        self.assertEqual(10, pods.call_count)
        self.assertEqual(4, receipts.call_count)
        self.assertEqual([220.0] * 4, deadlines)
        self.assertEqual(1, len(phase.Phase().state()['b10_lifecycle']['gaps']))
        self.assertEqual('lifecycle_evidence_gap', self.records()[20]['reason'])

    def test_b10_phase_worker_failures_preserve_sweep_and_separate_judge(self):
        for failed_mode in ('b10-sweep-line', 'b10-sweep'):
            with self.subTest(failed_mode=failed_mode):
                self.fake = ScriptedCommands()
                self.prepare_b10()
                job = self.name(failed_mode, None)
                args = ('helm', 'template', 'parchwell', 'charts/parchwell', '--values', str(self.private / f'values-{job}.json'))
                self.fake.responses[args] = (1, '')
                with self.mocked():
                    self.assertEqual(1, self.main('run-repeats'))
                    self.assertEqual(0, self.main('judge'))
                self.assertEqual('render_failed', self.records()[20 if failed_mode == 'b10-sweep-line' else 21]['outcome'])
                self.assertEqual('complete', self.records()[-1]['outcome'])
                if failed_mode == 'b10-sweep-line':
                    self.assertEqual('complete', self.records()[21]['outcome'])

    def test_b10_all_command_types_and_sleep_obey_remaining_deadline(self):
        self.prepare_b10()
        instance = phase.Phase()
        instance.command_deadline = 120
        with patch.object(phase.subprocess, 'call', return_value=0) as invoke:
            for args in (['helm', 'template'], ['kubectl', 'apply'], ['bash', 'wait-job'], ['bash', 'collect-job'],
                         ['kubectl', 'delete'], ['kubectl', 'get', 'pods'], ['az', 'storage', 'blob', 'show'],
                         ['az', 'storage', 'blob', 'download']):
                self.assertEqual(0, instance.run(args, 1200))
                self.assertEqual('15', invoke.call_args[0][0][2])
            self.clock = 115
            before = invoke.call_count
            self.assertEqual(124, instance.run(['helm', 'template'], 120))
            self.assertEqual(before, invoke.call_count)
        self.clock = 100
        self.fake.collect_hook_original = self.fake.collect_hook
        def almost_exhaust(instance, job, values):
            self.fake.collect_hook_original(instance, job, values)
            if values['mode'] == 'verify' and values['repeat'] == 5:
                self.clock = 18090
        self.fake.collect_hook = almost_exhaust
        def advance_sleep(seconds):
            self.delays.append(seconds)
            self.clock += seconds
        with self.mocked(), patch.object(phase.Phase, 'sleep', side_effect=advance_sleep):
            self.assertEqual(1, self.main('run-repeats'))
        self.assertEqual([5.0], self.delays)
        self.assertEqual('orchestration_deadline', self.records()[20]['reason'])

    def test_b10_phase_values_render_the_actual_chart(self):
        for selected in ('C0', 'C1'):
            self.prepare_b10(selected)
            document = phase.Phase().read('phase.json')
            document['b10_lifecycle_inputs'] = {
                'b10_last_loadgen_terminated_utc': '2026-01-01T00:00:00.0000000+00:00',
                'b10_last_verifier_completed_utc': '2026-01-01T00:00:00.0000000+00:00'}
            phase.write_json(self.private / 'phase.json', document)
            instance = phase.Phase()
            for mode in (*phase.B10_MODES, *phase.B10_PHASE_MODES, 'judge'):
                with self.subTest(selected=selected, mode=mode):
                    repeat = 1 if mode in phase.B10_MODES else None
                    self.assertEqual(DEADLINES[mode], instance.render(mode, repeat))
                    text = (self.private / f'rendered-{self.name(mode, repeat)}.yaml').read_text()
                    if mode == 'b10-sweep-line':
                        self.assertIn('Verifier__B10LastLoadgenTerminatedUtc', text)
                        self.assertIn('Verifier__B10LastVerifierCompletedUtc', text)
                    if mode in (*phase.B10_PHASE_MODES, 'judge'):
                        self.assertIn('Verifier__B10SettleIntervalMilliseconds', text)

    def test_b10_candidate_retrieval_uses_stored_deadline_and_cleanup_remains_available(self):
        self.prepare_b10()
        phase.Phase().b10_deadline(start=True)
        self.fake.download = json.dumps({**self.candidate(None), 'scenario_id': 'B10'}).encode()
        original = self.fake
        observed_deadlines = []
        def bounded_commands(instance, *args, **options):
            observed_deadlines.append(instance.command_deadline)
            return original(instance, *args, **options)
        with patch.object(phase.Phase, 'run', autospec=True, side_effect=bounded_commands):
            self.assertEqual(0, self.main('candidate'))
        self.assertEqual([18100, 18100], observed_deadlines)
        self.clock = 18100
        (self.candidate_dir / 'candidate.json.partial').write_text('stale')
        self.output_file.write_text('')
        before = len(self.fake.calls)
        with self.mocked():
            self.assertEqual(1, self.main('candidate'))
            self.assertEqual(before, len(self.fake.calls))
            self.assertEqual(0, self.main('delete-workload'))
        self.assertFalse((self.candidate_dir / 'candidate.json').exists())
        self.assertFalse((self.candidate_dir / 'candidate.json.partial').exists())
        self.assertEqual('full_allowed=false\n', self.output_file.read_text())
        self.assertEqual('orchestration_deadline', phase.Phase().state()['b10_candidate_gap'])
        instance = phase.Phase()
        instance.command_deadline = 18110
        with patch.object(phase.time, 'sleep') as sleep:
            instance.sleep(100)
            sleep.assert_called_once_with(5)
            self.clock = 18105
            instance.sleep(100)
            sleep.assert_called_once_with(5)

    def test_ledger_batch_selection(self):
        os.environ.update(PARCHWELL_SCENARIO='ledger-batch', PARCHWELL_RUN_KIND='smoke', PARCHWELL_BATCH_ITEMS='all')
        self.assertEqual(0, self.main('validate-batch'))
        self.assertEqual(2, self.main('validate-scenario'))
        with patch.dict(os.environ, {'PARCHWELL_RUN_KIND': 'batch'}):
            self.assertEqual(0, self.main('validate-batch'))
        for name, value in (('PARCHWELL_RUN_KIND', 'full'), ('PARCHWELL_BATCH_ITEMS', 'B06'), ('PARCHWELL_BATCH_ITEMS', 'B06,B07'),
                            ('PARCHWELL_CAPTURE_AUTHENTICATION_METADATA', 'true'), ('PARCHWELL_TEST_HANDLER_REFRESH_PATH', 'true'),
                            ('PARCHWELL_TEST_NATIVE_AUTHENTICATION', 'true')):
            with self.subTest(name=name, value=value), patch.dict(os.environ, {name: value}):
                stderr = io.StringIO()
                with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(stderr):
                    self.assertEqual(2, phase.main(['validate-batch']))
                self.assertEqual('unsupported ledger batch selection', stderr.getvalue().strip())

    def test_prepare_ledger_batch(self):
        self.prepare_ledger()
        self.assertEqual({'scenario': 'ledger-batch', 'phase': 'ledger-batch', 'configuration': None, 'run_id': LEDGER_RUN,
                          'source_commit': SHA, 'run_kind': 'smoke', 'items': ['B06', 'B07'], 'images': json.loads((self.private / 'phase.json').read_text())['images']},
                         json.loads((self.private / 'phase.json').read_text()))

    def test_prepare_ledger_batch_rejects_long_worker_run(self):
        os.environ.update(PARCHWELL_SCENARIO='ledger-batch', PARCHWELL_RUN_KIND='smoke', PARCHWELL_BATCH_ITEMS='all',
                          GITHUB_RUN_ID='4' * 19)
        self.assertEqual(2, self.main('prepare-batch'))
        self.assertEqual('', self.env_file.read_text())

    def test_ledger_smoke_runs_every_configuration_and_summarises(self):
        self.prepare_ledger()
        with self.mocked():
            self.assertEqual(0, self.main('single-action-seed'))
            self.assertEqual(0, self.main('run-single-action-batch'))
            self.assertEqual(0, self.main('delete-workload'))
        expected_jobs = [f'{LEDGER_RUN}-{item.lower()}-{configuration.lower()}-r1-{mode}'
                         for item, configuration in LEDGER_CONFIGURATIONS for mode in ('start-line', 'loadgen', 'verify')]
        self.assertEqual(expected_jobs, [call[0][3] for call in self.fake.calls if call[0][:3] == ['bash', 'infra/scripts/lifecycle/lifecycle.sh', 'wait-job']])
        values = json.loads((self.private / f'values-{LEDGER_RUN}-b06-c1-r1-loadgen.json').read_text())
        self.assertEqual(('B06', 'C1', f'{LEDGER_RUN}-b06', 'C1', 1, f'{LEDGER_RUN}-b06-c1-r1-loadgen'),
                         (values['scenario_id'], values['configuration_id'], values['runtime']['run_id'], values['runtime']['phase'],
                          values['runtime']['repeat'], values['runtime']['loadgen_job_name']))
        shows = [call[0] for call in self.fake.calls if call[0][:4] == ['az', 'storage', 'blob', 'show']]
        self.assertEqual(['ledger'] * 8, [args[args.index('--container-name') + 1] for args in shows])
        summary = json.loads((self.candidate_dir / 'B06-C0-smoke-summary.json').read_text())
        self.assertEqual({'schema_version': 1, 'item': 'B06', 'configuration': 'C0', 'repeat': 1, 'smoke_diagnostic': True,
                          'complete': True, 'reason': None,
                          'statistics': {'reports': 2, 'rows': [
                              {'ts': 1000, 'produce_requests': 5, 'batchcnt': {'document-events': {'cnt': 5, 'sum': 40, 'min': 1, 'max': 20, 'avg': 8, 'p50': 6}}},
                              {'ts': 2000, 'produce_requests': 11, 'batchcnt': {'document-events': {'cnt': 0, 'sum': 0, 'min': 0, 'max': 0, 'avg': 0, 'p50': 0}}}]},
                          'ledger': {'rows': 3, 'delivered': 2, 'latency_count': 2, 'latency_min_ms': 4.0, 'latency_p50_ms': 4.0,
                                     'latency_p95_ms': 9.5, 'latency_max_ms': 9.5}}, summary)
        self.assertEqual([{'item': item, 'configuration': configuration, 'complete': True, 'reason': None} for item, configuration in LEDGER_CONFIGURATIONS],
                         json.loads((self.artifacts / 'phase-status.json').read_text())['ledger_smoke'])
        delete = [call[0] for call in self.fake.calls if call[0][:2] == ['kubectl', 'delete']]
        self.assertEqual([['kubectl', 'delete', 'jobs', '-n', 'parchwell', '-l',
                           f'app.kubernetes.io/instance in ({LEDGER_RUN}-b06,{LEDGER_RUN}-b07)', '--ignore-not-found', '--wait=false', '--request-timeout=30s']], delete)

    def test_ledger_smoke_treats_wrong_shape_json_as_malformed_evidence(self):
        self.prepare_ledger()
        self.fake.blobs[f'{LEDGER_RUN}-b06/C0/r1/producer/{LEDGER_RUN}-b06-c0-r1-loadgen-pod/publication/statistics.jsonl'] = (
            json.dumps({'raw_json': '[]'}) + '\n').encode()
        self.fake.blobs[f'{LEDGER_RUN}-b07/C0/r1/producer/{LEDGER_RUN}-b07-c0-r1-loadgen-pod/publication/ledger.jsonl'] = b'[1]\n'
        with self.mocked():
            self.assertEqual(1, self.main('run-single-action-batch'))
        self.assertEqual([('B06', 'C0', False, 'malformed_evidence'), ('B06', 'C1', True, None),
                          ('B07', 'C0', False, 'malformed_evidence'), ('B07', 'C1', True, None)],
                         [(entry['item'], entry['configuration'], entry['complete'], entry['reason'])
                          for entry in json.loads((self.artifacts / 'phase-status.json').read_text())['ledger_smoke']])

    def test_ledger_smoke_records_gaps_and_continues(self):
        self.prepare_ledger()
        del self.fake.blobs[f'{LEDGER_RUN}-b07/C1/r1/producer/{LEDGER_RUN}-b07-c1-r1-loadgen-pod/publication/statistics.jsonl']
        self.fake.blobs[f'{LEDGER_RUN}-b06/C1/r1/producer/{LEDGER_RUN}-b06-c1-r1-loadgen-pod/publication/statistics.jsonl'] = (
            json.dumps({'raw_json': {'ts': 1}}) + '\n').encode()
        with self.mocked():
            self.assertEqual(1, self.main('run-single-action-batch'))
        self.assertEqual([('B06', 'C0', True, None), ('B06', 'C1', False, 'malformed_evidence'), ('B07', 'C0', True, None),
                          ('B07', 'C1', False, 'download_failed:statistics.jsonl')],
                         [(entry['item'], entry['configuration'], entry['complete'], entry['reason'])
                          for entry in json.loads((self.artifacts / 'phase-status.json').read_text())['ledger_smoke']])
        self.assertEqual(12, len([call for call in self.fake.calls if call[0][:3] == ['bash', 'infra/scripts/lifecycle/lifecycle.sh', 'wait-job']]))

    def ledger_candidate(self, item, configuration, **changes):
        return json.dumps({'schema_version': 1, 'synthetic': True, 'scenario_id': item,
                           'run_id': f'{LEDGER_RUN}-{item.lower()}', 'phase': configuration,
                           'configuration_id': configuration, 'source_commit': SHA, 'status': 'complete_valid',
                           'gate': None, 'image_digests': dict(DIGESTS),
                           'repeats': [{'repeat': n, 'status': 'complete_valid'} for n in range(1, 6)], **changes}).encode()

    def prepare_ledger_batch_candidates(self):
        self.prepare_ledger('batch')
        for item, configuration in LEDGER_CONFIGURATIONS:
            self.fake.blobs[f'{LEDGER_RUN}-{item.lower()}/{configuration}/candidate.json'] = self.ledger_candidate(item, configuration)

    def run_ledger_batch(self, code, *patches):
        with self.mocked(), patch.object(phase.Phase, 'boot_identity', return_value='00000000-0000-0000-0000-000000000000'), contextlib.ExitStack() as stack:
            for extra in patches:
                stack.enter_context(extra)
            self.assertEqual(0, self.main('single-action-seed'))
            self.assertEqual(code, self.main('run-single-action-batch'))
            self.assertEqual(0, self.main('delete-workload'))
        return [call[0][3] for call in self.fake.calls if call[0][:3] == ['bash', 'infra/scripts/lifecycle/lifecycle.sh', 'wait-job']]

    def ledger_gaps(self):
        return {path.name: json.loads(path.read_text())['reason'] for path in self.candidate_dir.glob('*-gap.json')}

    # The dispatch selection must survive preparation so it chooses the five-repeat path.
    def test_prepare_ledger_batch_records_batch_kind(self):
        self.prepare_ledger('batch')
        self.assertEqual({'scenario': 'ledger-batch', 'phase': 'ledger-batch', 'configuration': None, 'run_id': LEDGER_RUN,
                          'source_commit': SHA, 'run_kind': 'batch', 'items': ['B06', 'B07'], 'images': json.loads((self.private / 'phase.json').read_text())['images']},
                         json.loads((self.private / 'phase.json').read_text()))

    # Each configuration must publish a candidate after its own ordered repeats and judge.
    def test_ledger_batch_runs_five_repeats_and_a_judge_per_configuration(self):
        self.prepare_ledger_batch_candidates()
        jobs = self.run_ledger_batch(0)
        expected_jobs = []
        for item, configuration in LEDGER_CONFIGURATIONS:
            prefix = f'{LEDGER_RUN}-{item.lower()}-{configuration.lower()}'
            expected_jobs.extend(f'{prefix}-r{n}-{mode}' for n in range(1, 6) for mode in ('start-line', 'loadgen', 'verify'))
            expected_jobs.append(f'{prefix}-judge')
            self.assertEqual(self.ledger_candidate(item, configuration),
                             (self.candidate_dir / f'{item}-{configuration}-candidate.json').read_bytes())
        self.assertEqual(64, len(jobs))
        self.assertEqual(expected_jobs, jobs)
        values = json.loads((self.private / f'values-{LEDGER_RUN}-b07-c1-judge.json').read_text())
        self.assertEqual(('B07', 'C1', f'{LEDGER_RUN}-b07', 'C1', 'judge'),
                         (values['scenario_id'], values['configuration_id'], values['runtime']['run_id'],
                          values['runtime']['phase'], values['runtime']['mode']))
        self.assertNotIn('repeat', values['runtime'])
        self.assertNotIn('loadgen_job_name', values['runtime'])
        self.assertEqual([], list(self.candidate_dir.glob('*-smoke-summary.json')))
        self.assertEqual({}, self.ledger_gaps())
        self.assertEqual([{'item': item, 'configuration': configuration, 'accepted': True,
                           'candidate': f'{item}-{configuration}-candidate.json', 'judge_completion': None}
                          for item, configuration in LEDGER_CONFIGURATIONS],
                         json.loads((self.artifacts / 'phase-status.json').read_text())['candidates'])
        self.assertEqual('CLOCK_MONOTONIC', json.loads((self.private / 'phase.json').read_text())['batch_clock_source'])
        shows = [call[0] for call in self.fake.calls if call[0][:4] == ['az', 'storage', 'blob', 'show']]
        self.assertEqual(['verifier'] * 4, [args[args.index('--container-name') + 1] for args in shows])
        delete = [call[0] for call in self.fake.calls if call[0][:2] == ['kubectl', 'delete']]
        self.assertEqual([['kubectl', 'delete', 'jobs', '-n', 'parchwell', '-l',
                           f'app.kubernetes.io/instance in ({LEDGER_RUN}-b06,{LEDGER_RUN}-b07)', '--ignore-not-found', '--wait=false', '--request-timeout=30s']], delete)

    # A missing or misidentified candidate is a gap without suppressing later configurations.
    def test_ledger_batch_rejects_wrong_identity_and_continues(self):
        self.prepare_ledger_batch_candidates()
        self.fake.blobs[f'{LEDGER_RUN}-b06/C1/candidate.json'] = self.ledger_candidate('B06', 'C1', scenario_id='B07')
        del self.fake.blobs[f'{LEDGER_RUN}-b07/C0/candidate.json']
        self.assertEqual(64, len(self.run_ledger_batch(1)))
        self.assertEqual([('B06', 'C0', True), ('B06', 'C1', False), ('B07', 'C0', False), ('B07', 'C1', True)],
                         [(entry['item'], entry['configuration'], entry['accepted'])
                          for entry in json.loads((self.artifacts / 'phase-status.json').read_text())['candidates']])
        self.assertEqual({'B06-C1-gap.json': 'candidate_contract', 'B07-C0-gap.json': 'download_failed'}, self.ledger_gaps())
        for stem in ('B06-C1', 'B07-C0'):
            self.assertFalse((self.candidate_dir / f'{stem}-candidate.json').exists())

    # The admission cap records every skipped configuration without starting its Jobs.
    def test_ledger_batch_skips_configurations_past_the_admission_cap(self):
        self.prepare_ledger_batch_candidates()
        jobs = self.run_ledger_batch(1, patch.object(phase.Phase, 'admission_allowed', side_effect=[True, True, False, False]))
        self.assertEqual(32, len(jobs))
        self.assertTrue(all(job.startswith(f'{LEDGER_RUN}-b06-') for job in jobs))
        self.assertEqual({'B07-C0-gap.json': 'start_cap', 'B07-C1-gap.json': 'start_cap'}, self.ledger_gaps())

    # The separate orchestration module shares the phase's caps and repeat contract.
    def test_ledger_batch_constants_match_phase(self):
        spec = importlib.util.spec_from_file_location('ledger_batch_constants', REPO / 'infra/scripts/phase/ledger_batch.py')
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        self.assertEqual((phase.BATCH_START_CAP, phase.CANDIDATE_MAXIMUM, phase.SLOTS, phase.BAD_OUTCOMES),
                         (module.ADMISSION_SECONDS, module.CANDIDATE_MAXIMUM, module.SLOTS, set(module.BAD_OUTCOMES)))

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

    def test_handler_refresh_flag_requires_exact_tuple_before_prepare_or_stop_side_effects(self):
        valid = {'PARCHWELL_SCENARIO': 'probe-batch-2', 'PARCHWELL_RUN_KIND': 'smoke',
                 'PARCHWELL_BATCH_ITEMS': 'authentication-controls',
                 'PARCHWELL_CAPTURE_AUTHENTICATION_METADATA': 'true',
                 'PARCHWELL_TEST_HANDLER_REFRESH_PATH': 'true'}
        with patch.dict(os.environ, valid):
            self.assertEqual(0, self.main('validate-batch'))
            with self.mocked():
                self.assertEqual(0, self.main('prepare-batch'))
                saved = json.loads((Path(dict(line.split('=', 1) for line in self.env_file.read_text().splitlines())[
                    'PARCHWELL_PRIVATE_DIRECTORY']) / 'phase.json').read_text())
                self.assertIs(True, saved['test_handler_refresh_path'])

        rejected = [
            {'PARCHWELL_SCENARIO': 'probe-batch', 'PARCHWELL_RUN_KIND': 'smoke', 'PARCHWELL_BATCH_ITEMS': 'all'},
            {'PARCHWELL_SCENARIO': 'probe-batch-2', 'PARCHWELL_RUN_KIND': 'batch', 'PARCHWELL_BATCH_ITEMS': 'F06'},
            {'PARCHWELL_SCENARIO': 'probe-batch-2', 'PARCHWELL_RUN_KIND': 'smoke', 'PARCHWELL_BATCH_ITEMS': 'all'},
            {'PARCHWELL_SCENARIO': 'probe-batch-2', 'PARCHWELL_RUN_KIND': 'smoke', 'PARCHWELL_BATCH_ITEMS': 'authentication-controls', 'PARCHWELL_CAPTURE_AUTHENTICATION_METADATA': 'false'},
            {'PARCHWELL_SCENARIO': 'A01', 'PARCHWELL_RUN_KIND': 'smoke', 'PARCHWELL_BATCH_ITEMS': 'authentication-controls'},
        ]
        for case in rejected:
            env = {**case, 'PARCHWELL_TEST_HANDLER_REFRESH_PATH': 'true'}
            with self.subTest(env=env), patch.dict(os.environ, env, clear=False):
                before = set(self.root.iterdir())
                self.env_file.write_text('')
                self.assertEqual(2, self.main('validate-batch') if env['PARCHWELL_SCENARIO'].startswith('probe-') else self.main('validate-scenario'))
                with patch.object(phase.Phase, 'run') as run:
                    self.assertEqual(2, self.main('prepare-stop'))
                    run.assert_not_called()
                self.assertEqual('', self.env_file.read_text())
                self.assertLessEqual(set(self.root.iterdir()), before | {self.env_file, self.output_file, self.summary_file})

    def test_handler_refresh_preparation_rejects_requested_vs_frozen_flag_mismatch_before_side_effects(self):
        env = {'PARCHWELL_SCENARIO': 'probe-batch-2', 'PARCHWELL_RUN_KIND': 'smoke',
               'PARCHWELL_BATCH_ITEMS': 'authentication-controls',
               'PARCHWELL_CAPTURE_AUTHENTICATION_METADATA': 'true',
               'PARCHWELL_TEST_HANDLER_REFRESH_PATH': 'true'}
        metadata = {'run_kind': 'smoke', 'items': ['F06'], 'probe_v2_selection': 'authentication-controls',
                    'capture_authentication_metadata': True, 'test_handler_refresh_path': False}
        with patch.dict(os.environ, env):
            before = set(self.root.iterdir())
            with self.assertRaisesRegex(phase.Failure, 'unsupported probe batch selection'):
                phase.Phase().prepare_shared('probe-batch-2', 'single-action', None, 'probe', **metadata)
            self.assertEqual('', self.env_file.read_text())
            self.assertEqual(before, set(self.root.iterdir()))

    def test_handler_refresh_parser_is_strict_and_old_absence_defaults_false(self):
        for value, expected in [(None, False), ('false', False), ('true', True)]:
            with self.subTest(value=value), patch.dict(os.environ, {}, clear=False):
                os.environ.pop('PARCHWELL_TEST_HANDLER_REFRESH_PATH', None)
                if value is not None:
                    os.environ['PARCHWELL_TEST_HANDLER_REFRESH_PATH'] = value
                self.assertIs(expected, phase.test_handler_refresh_path())
        for value in ('TRUE', 'True', 'yes', '', '1'):
            with self.subTest(value=value), patch.dict(os.environ, {'PARCHWELL_TEST_HANDLER_REFRESH_PATH': value}):
                with self.assertRaisesRegex(phase.Failure, 'unsupported probe batch selection'):
                    phase.test_handler_refresh_path()

    def test_native_authentication_flag_requires_exact_tuple_before_prepare_or_stop_side_effects(self):
        valid = {'PARCHWELL_SCENARIO': 'probe-batch-2', 'PARCHWELL_RUN_KIND': 'smoke',
                 'PARCHWELL_BATCH_ITEMS': 'authentication-controls',
                 'PARCHWELL_CAPTURE_AUTHENTICATION_METADATA': 'true',
                 'PARCHWELL_TEST_HANDLER_REFRESH_PATH': 'false',
                 'PARCHWELL_TEST_NATIVE_AUTHENTICATION': 'true'}
        with patch.dict(os.environ, valid):
            self.assertEqual(0, self.main('validate-batch'))
            with self.mocked():
                self.assertEqual(0, self.main('prepare-batch'))
                saved = json.loads((Path(dict(line.split('=', 1) for line in self.env_file.read_text().splitlines())[
                    'PARCHWELL_PRIVATE_DIRECTORY']) / 'phase.json').read_text())
                self.assertIs(True, saved['test_native_authentication'])

        rejected = [
            {'PARCHWELL_TEST_HANDLER_REFRESH_PATH': 'true'},
            {'PARCHWELL_CAPTURE_AUTHENTICATION_METADATA': 'false'},
            {'PARCHWELL_BATCH_ITEMS': 'all'},
            {'PARCHWELL_RUN_KIND': 'batch'},
            {'PARCHWELL_SCENARIO': 'probe-batch', 'PARCHWELL_BATCH_ITEMS': 'all', 'PARCHWELL_CAPTURE_AUTHENTICATION_METADATA': 'false'},
            {'PARCHWELL_SCENARIO': 'A01', 'PARCHWELL_CAPTURE_AUTHENTICATION_METADATA': 'false'},
        ]
        for change in rejected:
            env = {**valid, **change}
            with self.subTest(env=env), patch.dict(os.environ, env, clear=False):
                before = set(self.root.iterdir())
                self.env_file.write_text('')
                self.assertEqual(2, self.main('validate-batch') if env['PARCHWELL_SCENARIO'].startswith('probe-') else self.main('validate-scenario'))
                with patch.object(phase.Phase, 'run') as run:
                    self.assertEqual(2, self.main('prepare-stop'))
                    run.assert_not_called()
                self.assertEqual('', self.env_file.read_text())
                self.assertLessEqual(set(self.root.iterdir()), before | {self.env_file, self.output_file, self.summary_file})

    def test_native_authentication_preparation_rejects_requested_vs_frozen_flag_mismatch_before_side_effects(self):
        env = {'PARCHWELL_SCENARIO': 'probe-batch-2', 'PARCHWELL_RUN_KIND': 'smoke',
               'PARCHWELL_BATCH_ITEMS': 'authentication-controls',
               'PARCHWELL_CAPTURE_AUTHENTICATION_METADATA': 'true'}
        for requested, frozen in [('true', False), ('false', True)]:
            metadata = {'run_kind': 'smoke', 'items': ['F06'], 'probe_v2_selection': 'authentication-controls',
                        'capture_authentication_metadata': True, 'test_handler_refresh_path': False,
                        'test_native_authentication': frozen}
            with self.subTest(requested=requested, frozen=frozen), patch.dict(
                    os.environ, {**env, 'PARCHWELL_TEST_NATIVE_AUTHENTICATION': requested}):
                os.environ.pop('PARCHWELL_TEST_HANDLER_REFRESH_PATH', None)
                before = set(self.root.iterdir())
                with self.assertRaisesRegex(phase.Failure, 'unsupported probe batch selection'):
                    phase.Phase().prepare_shared('probe-batch-2', 'single-action', None, 'probe', **metadata)
                self.assertEqual('', self.env_file.read_text())
                self.assertEqual(before, set(self.root.iterdir()))

    def test_native_authentication_preparation_rejects_other_scenarios_before_side_effects(self):
        with patch.dict(os.environ, {'PARCHWELL_SCENARIO': 'A01', 'PARCHWELL_TEST_NATIVE_AUTHENTICATION': 'true'}):
            os.environ.pop('PARCHWELL_CAPTURE_AUTHENTICATION_METADATA', None)
            os.environ.pop('PARCHWELL_TEST_HANDLER_REFRESH_PATH', None)
            before = set(self.root.iterdir())
            with self.assertRaisesRegex(phase.Failure, 'unsupported probe batch selection'):
                phase.Phase().prepare('probe')
            self.assertEqual('', self.env_file.read_text())
            self.assertEqual(before, set(self.root.iterdir()))

    def test_native_authentication_parser_is_strict_and_old_absence_defaults_false(self):
        for value, expected in [(None, False), ('false', False), ('true', True)]:
            with self.subTest(value=value), patch.dict(os.environ, {}, clear=False):
                os.environ.pop('PARCHWELL_TEST_NATIVE_AUTHENTICATION', None)
                if value is not None:
                    os.environ['PARCHWELL_TEST_NATIVE_AUTHENTICATION'] = value
                self.assertIs(expected, phase.test_native_authentication())
        for value in ('True', '1', ''):
            with self.subTest(value=value), patch.dict(os.environ, {'PARCHWELL_TEST_NATIVE_AUTHENTICATION': value}):
                with self.assertRaisesRegex(phase.Failure, 'unsupported probe batch selection'):
                    phase.test_native_authentication()

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

    def prepare_v2_batch(self, items='all', capture='false', run_kind='smoke', refresh='false'):
        os.environ.update(PARCHWELL_SCENARIO='probe-batch-2', PARCHWELL_RUN_KIND=run_kind,
                          PARCHWELL_BATCH_ITEMS=items, PARCHWELL_CAPTURE_AUTHENTICATION_METADATA=capture,
                          PARCHWELL_TEST_HANDLER_REFRESH_PATH=refresh)
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

    def idle_environment(self, **overrides):
        return {'PARCHWELL_SCENARIO': 'idle-observation', 'PARCHWELL_RUN_KIND': 'smoke', 'PARCHWELL_BATCH_ITEMS': 'all', **overrides}

    def prepare_idle(self, **overrides):
        os.environ.update(self.idle_environment(**overrides))
        self.assertEqual(0, self.main('prepare-batch'))
        os.environ.update(dict(line.split('=', 1) for line in self.env_file.read_text().splitlines()))
        self.private = Path(os.environ['PARCHWELL_PRIVATE_DIRECTORY'])
        self.artifacts = Path(os.environ['PARCHWELL_ARTIFACT_DIRECTORY'])
        self.candidate_dir = Path(os.environ['PARCHWELL_CANDIDATE_DIRECTORY'])

    def test_idle_observation_admits_only_the_smoke_and_batch_tuples_before_side_effects(self):
        for kind in ('smoke', 'batch'):
            for items in ('all', 'A02', 'A03', 'A02,A04', 'A02,A03,A04'):
                with self.subTest(run_kind=kind, items=items), \
                        patch.dict(os.environ, self.idle_environment(PARCHWELL_RUN_KIND=kind, PARCHWELL_BATCH_ITEMS=items)), self.mocked():
                    self.assertEqual(0, self.main('validate-batch'))
                    self.assertEqual(0, self.main('prepare-stop'))
        self.assertIn('private_directory=', self.output_file.read_text())
        rejected = [{'PARCHWELL_RUN_KIND': 'Batch'}, {'PARCHWELL_RUN_KIND': 'Smoke'}, {'PARCHWELL_BATCH_ITEMS': 'A04,A02'},
                    {'PARCHWELL_BATCH_ITEMS': 'A02,A02'}, {'PARCHWELL_BATCH_ITEMS': 'A02,'}, {'PARCHWELL_BATCH_ITEMS': 'a02'},
                    {'PARCHWELL_BATCH_ITEMS': 'A05'}, {'PARCHWELL_BATCH_ITEMS': 'ALL'}, {'PARCHWELL_BATCH_ITEMS': ''},
                    {'PARCHWELL_CAPTURE_AUTHENTICATION_METADATA': 'true'}, {'PARCHWELL_CAPTURE_AUTHENTICATION_METADATA': 'yes'},
                    {'PARCHWELL_TEST_HANDLER_REFRESH_PATH': 'true'},
                    {'PARCHWELL_TEST_NATIVE_AUTHENTICATION': 'true'}, {'PARCHWELL_SCENARIO': 'IDLE-OBSERVATION'}, None]
        for overrides in rejected:
            with self.subTest(overrides=overrides), patch.dict(os.environ, self.idle_environment(**(overrides or {}))):
                if overrides is None:
                    del os.environ['PARCHWELL_BATCH_ITEMS']
                before = set(self.root.iterdir())
                environment_before, output_before = self.env_file.read_bytes(), self.output_file.read_bytes()
                with self.mocked():
                    for command in ('validate-batch', 'prepare-batch', 'prepare-stop', 'single-action-seed', 'run-single-action-batch'):
                        self.assertEqual(2, self.main(command), command)
                self.assertEqual(before, set(self.root.iterdir()))
                self.assertEqual(environment_before, self.env_file.read_bytes())
                self.assertEqual(output_before, self.output_file.read_bytes())
        self.assertEqual([], self.fake.calls)
        with patch.dict(os.environ, self.idle_environment(PARCHWELL_RUN_KIND='Batch')), contextlib.redirect_stderr(io.StringIO()) as error:
            self.assertEqual(2, phase.main(['validate-batch']))
        self.assertEqual('unsupported idle observation selection', error.getvalue().strip())

    def test_idle_observation_refuses_invalid_items_before_side_effects(self):
        refused = {
            'out of order': 'A04,A02', 'out of order, all three': 'A04,A03,A02', 'out of order, adjacent pair': 'A03,A02',
            'unknown item': 'A05', 'unknown item after a valid one': 'A02,A05', 'item from another scenario': 'B08',
            'duplicate item': 'A02,A02', 'duplicate item, non-adjacent': 'A02,A04,A02',
            'all with another item': 'all,A02', 'all repeated': 'all,all',
            'empty list': '', 'separator only': ',', 'trailing separator': 'A02,', 'leading separator': ',A02',
            'doubled separator': 'A02,,A04', 'space after separator': 'A02, A04', 'leading space': ' A02', 'trailing space': 'A02 ',
            'lower case item': 'a02', 'mixed case list': 'A02,a04', 'upper case all': 'ALL', 'title case all': 'All',
            'other separator': 'A02;A04', 'separator-joined without comma': 'A02A04', 'unset': None}
        for kind in ('smoke', 'batch'):
            for label, items in refused.items():
                with self.subTest(run_kind=kind, case=label, items=items), \
                        patch.dict(os.environ, self.idle_environment(PARCHWELL_RUN_KIND=kind, PARCHWELL_BATCH_ITEMS=items or '')):
                    if items is None:
                        del os.environ['PARCHWELL_BATCH_ITEMS']
                    before = set(self.root.iterdir())
                    environment_before, output_before = self.env_file.read_bytes(), self.output_file.read_bytes()
                    with self.mocked():
                        for command in ('validate-batch', 'prepare-batch', 'prepare-stop'):
                            self.assertEqual(2, self.main(command), command)
                    with contextlib.redirect_stderr(io.StringIO()) as error:
                        self.assertEqual(2, phase.main(['validate-batch']))
                    self.assertEqual('unsupported idle observation selection', error.getvalue().strip())
                    self.assertEqual(before, set(self.root.iterdir()))
                    self.assertEqual(environment_before, self.env_file.read_bytes())
                    self.assertEqual(output_before, self.output_file.read_bytes())
        self.assertEqual([], self.fake.calls)

    def test_idle_observation_preparation_freezes_all_seven_configurations(self):
        self.prepare_idle()
        self.assertEqual({'scenario': 'idle-observation', 'phase': 'idle-observation', 'configuration': None,
                          'run_id': 'idle-40121134428-1', 'source_commit': SHA, 'run_kind': 'smoke',
                          'items': ['A02', 'A03', 'A04']}, json.loads((self.private / 'phase.json').read_text()))
        self.assertEqual({'phase': 'idle-observation', 'run_id': 'idle-40121134428-1', 'modes': [], 'candidate': None,
                          'safety_stop': False}, phase.Phase().state())
        self.assertEqual({}, json.loads((self.private / 'ephemeral_tfvars.tfvars.json').read_text()))
        self.assertEqual([], list(self.candidate_dir.iterdir()))
        self.assertEqual(('A02', 'A03', 'A04'), phase.IDLE_ITEMS)
        self.assertEqual((('A02', 'C0'), ('A02', 'C1'), ('A03', 'C0'), ('A03', 'C1'), ('A03', 'C2'), ('A04', 'C0'), ('A04', 'C1')),
                         phase.Phase().idle_observation().selection())

    def test_idle_observation_preparation_freezes_an_ordered_item_subset(self):
        self.prepare_idle(PARCHWELL_RUN_KIND='batch', PARCHWELL_BATCH_ITEMS='A02,A04')
        self.assertEqual(['A02', 'A04'], json.loads((self.private / 'phase.json').read_text())['items'])
        self.assertEqual((('A02', 'C0'), ('A02', 'C1'), ('A04', 'C0'), ('A04', 'C1')), phase.Phase().idle_observation().selection())

    def test_idle_observation_preparation_freezes_the_batch_run_kind_and_names_the_judge_job(self):
        self.prepare_idle(PARCHWELL_RUN_KIND='batch')
        self.assertEqual('batch', json.loads((self.private / 'phase.json').read_text())['run_kind'])
        self.assertEqual('idle-40121134428-1-idle-judge',
                         phase.job_name('idle-40121134428-1', 'idle-observation', phase.IDLE_JUDGE_MODE))
        self.assertEqual('idle-observation-judge', phase.IDLE_JUDGE_MODE)

    def test_idle_observation_batch_entrypoints_route_only_to_the_idle_module(self):
        self.prepare_idle()
        idle = Mock()
        idle.run_batch.side_effect = [0, 1]
        with self.mocked(), patch.object(phase.Phase, 'idle_observation', return_value=idle), \
                patch.object(phase.Phase, 'probe_v2', side_effect=AssertionError('probe_v2')):
            self.assertEqual(0, self.main('single-action-seed'))
            self.assertEqual(1, idle.selection.call_count)
            self.assertEqual('idle-observation', idle.selection.call_args.args[0]['scenario'])
            self.assertEqual(0, self.main('run-single-action-batch'))
            self.assertEqual(1, self.main('run-single-action-batch'))
            self.assertEqual(2, idle.run_batch.call_count)
            self.assertEqual(0, self.main('delete-workload'))
        self.assertEqual([['kubectl', 'delete', 'jobs', '-n', 'parchwell', '-l', 'app.kubernetes.io/instance=idle-40121134428-1',
                           '--ignore-not-found', '--wait=false', '--request-timeout=30s']], [call[0] for call in self.fake.calls])
        self.assertNotIn('seed_gap', phase.Phase().state())

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

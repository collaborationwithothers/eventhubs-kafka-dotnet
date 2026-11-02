"""Synthetic transport regressions for the idle-observation phase module."""
import copy
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

REPO = Path(__file__).resolve().parents[4]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


phase = module('phase_idle_tests', REPO / 'infra/scripts/phase/phase.py')
idle = module('idle_observation_tests', REPO / 'infra/scripts/phase/idle_observation.py')
SHA = 'a' * 40
RUN = 'idle-synthetic'
IMAGES = {name: {'digest': 'sha256:' + character * 64, 'reference': f'synthetic.azurecr.io/parchwell/{name}@sha256:' + character * 64}
          for name, character in (('loadgen', 'b'), ('verifier', 'c'))}
ACCOUNTS = {name: {'name': name, 'client_id': 'client-' + name, 'principal_id': 'principal-' + name,
                   'subject': 'system:serviceaccount:parchwell:' + name} for name in ('loadgen', 'verifier', 'probe', 'enrichment-consumer')}
BOOT = '00000000-0000-0000-0000-000000000001'


def phase_document(items=('A02', 'A03', 'A04'), scenario='idle-observation', run_kind='batch'):
    return {'run_id': RUN, 'phase': 'C0', 'scenario': scenario, 'configuration': 'C0', 'run_kind': run_kind,
            'items': list(items), 'source_commit': SHA, 'images': IMAGES}


def good_pod(job, image=IMAGES['loadgen']['reference'], account='loadgen', code=0):
    return {'metadata': {'name': job + '-pod'},
            'spec': {'serviceAccountName': account, 'containers': [{'name': 'loadgen', 'image': image}]},
            'status': {'containerStatuses': [{'name': 'loadgen', 'state': {'terminated': {'exitCode': code}}}]}}


class IdleObservationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        root = Path(self.temp.name)
        self.private, self.artifacts, self.candidates = (root / p for p in ('private', 'artifacts', 'candidates'))
        for path in (self.private, self.artifacts, self.candidates):
            path.mkdir()
        environment = patch.dict(os.environ, {'PARCHWELL_PRIVATE_DIRECTORY': str(self.private), 'PARCHWELL_ARTIFACT_DIRECTORY': str(self.artifacts),
            'PARCHWELL_CANDIDATE_DIRECTORY': str(self.candidates), 'PARCHWELL_REPOSITORY': str(REPO)})
        environment.start()
        self.addCleanup(environment.stop)
        self.p = phase.Phase()
        self.idle = self.p.idle_observation()
        self.write_phase(phase_document())
        self.write('ephemeral-output.json', {'eventhubs': {'value': {'namespace_name': 'ns'}}, 'blob': {'value': {'account_url': 'https://b/'}},
                                             'private_endpoint_ips': {'value': {'eventhubs': '10.0.0.4', 'blob': '10.0.0.5'}}})
        self.write('persistent-output.json', {'application_insights_connection_string': {'value': 'InstrumentationKey=synthetic'}})
        self.write('cluster-output.json', {'workload_identity': {'value': {'service_accounts': ACCOUNTS}}})
        self.p.save_state({'modes': []})

    def write(self, name, value):
        (self.private / name).write_text(json.dumps(value))

    def write_phase(self, document):
        self.write('phase.json', document)

    def selected(self, **kwargs):
        return self.idle.selection(phase_document(**kwargs))

    def test_selection_accepts_ordered_item_subsets(self):
        self.assertEqual(len(self.selected()), 7)
        self.assertEqual(self.selected(items=['A03']), (('A03', 'C0'), ('A03', 'C1'), ('A03', 'C2')))
        self.assertEqual(len(self.selected(items=['A02', 'A04'])), 4)
        self.assertEqual(self.selected(), idle.CONFIGURATIONS)

    def test_selection_rejects_everything_else(self):
        for kwargs in ({'items': ['A03', 'A02']}, {'items': []}, {'items': ['B01']}, {'scenario': 'probe-batch'}, {'run_kind': 'probe'}):
            with self.subTest(kwargs=kwargs), self.assertRaisesRegex(ValueError, 'idle_observation_selection'):
                self.selected(**kwargs)
        with self.assertRaisesRegex(ValueError, 'idle_observation_selection'):
            self.idle.selection({'scenario': 'idle-observation', 'run_kind': 'batch'})

    def test_job_name(self):
        self.assertEqual(phase.job_name(RUN, 'C0', 'idle-observation', None, 'A02', 'C0', 'loadgen'), RUN + '-a02-c0-idle')

    def test_values(self):
        values = self.idle.values('A03', 'C2')
        self.assertEqual(values['scenario_id'], 'A03')
        self.assertEqual(values['configuration_id'], 'C2')
        runtime = values['runtime']
        self.assertEqual(set(runtime), {'run_id', 'phase', 'mode', 'source_commit', 'run_kind', 'loadgen_image', 'verifier_image',
                                        'service_accounts', 'eventhubs', 'blob', 'eventhubs_ip', 'blob_ip', 'application_insights_connection_string'})
        self.assertEqual(set(runtime['service_accounts']), {'loadgen', 'verifier'})
        self.assertEqual(runtime['phase'], 'C2')
        self.assertEqual(self.p.values('idle-observation', None, 'A03', 'C2', 'loadgen'), values)
        with self.assertRaisesRegex(ValueError, 'idle_observation_configuration'):
            self.idle.values('A02', 'C2')
        self.write_phase(phase_document(items=['A02']))
        with self.assertRaisesRegex(ValueError, 'idle_observation_configuration'):
            self.idle.values('A03', 'C0')

    def record(self, job, outcome='complete', collect=0, pods=None, status=None, write=True):
        record = {'job': job, 'mode': 'idle-observation', 'outcome': outcome, 'collect': collect}
        if write:
            directory = self.artifacts / job
            directory.mkdir(exist_ok=True)
            (directory / 'collection-status.json').write_text(json.dumps(status or {'complete': True, 'gaps': []}))
            (directory / 'pods.json').write_text(json.dumps({'items': pods if pods is not None else [good_pod(job)]}))
        return record

    def test_capture_complete(self):
        job = RUN + '-a02-c0-idle'
        state = {'modes': [{'job': job}]}
        self.p.save_state(state)
        self.assertTrue(self.idle.capture_complete(self.record(job)))
        self.assertIs(self.p.state()['modes'][0]['capture_complete'], True)
        cases = {
            'exit code': lambda: self.record(job, pods=[good_pod(job, code=1)]),
            'account': lambda: self.record(job, pods=[good_pod(job, account='probe')]),
            'image': lambda: self.record(job, pods=[good_pod(job, image='other')]),
            'two pods': lambda: self.record(job, pods=[good_pod(job), good_pod(job)]),
            'gap': lambda: self.record(job, status={'complete': False, 'gaps': ['pods']}),
            'outcome': lambda: self.record(job, outcome='failed'),
            'collect': lambda: self.record(job, collect=1),
        }
        for name, build in cases.items():
            with self.subTest(name):
                self.assertFalse(self.idle.capture_complete(build()))
        self.assertFalse(self.idle.capture_complete(self.record(job, write=False) | {'job': RUN + '-missing'}))
        self.assertIs(self.p.state()['modes'][0]['capture_complete'], False)

    def run_batch(self, boot=BOOT, monotonic_offset=None, fail=None):
        calls = []

        def run_mode(mode, repeat, item=None, configuration=None, action_role=None, skip_reason=None, absolute_deadline=None):
            calls.append((mode, repeat, item, configuration, action_role, skip_reason))
            job = phase.job_name(RUN, 'C0', mode, repeat, item, configuration, action_role)
            record = self.record(job, outcome='not_attempted' if skip_reason else 'complete',
                                 pods=[good_pod(job, code=1 if len(calls) == fail else 0)])
            state = self.p.state()
            state['modes'].append({'job': job})
            self.p.save_state(state)
            return record

        real = phase.time.monotonic

        def monotonic():
            value = real()
            if monotonic_offset is not None and len(calls) == 6:
                return value + monotonic_offset
            return value

        with patch.object(self.p, 'run_mode', run_mode), patch.object(self.p, 'boot_identity', lambda: boot), \
                patch.object(idle.time, 'monotonic', monotonic):
            code = self.idle.run_batch()
        return code, calls

    def test_run_batch_passes(self):
        code, calls = self.run_batch()
        self.assertEqual(code, 0)
        self.assertEqual([(c[2], c[3]) for c in calls], list(idle.CONFIGURATIONS))
        self.assertTrue(all(c[0] == 'idle-observation' and c[4] == 'loadgen' and c[5] is None for c in calls))
        summary = json.loads((self.artifacts / 'idle-observation-batch.json').read_text())
        self.assertEqual(summary['client_slots'], 35)
        self.assertEqual(summary['jobs'], 7)
        self.assertIs(summary['evidence_passed'], True)
        self.assertEqual(summary['image_digests'], {'loadgen': IMAGES['loadgen']['digest'], 'verifier': IMAGES['verifier']['digest']})
        self.assertEqual(self.p.state()['not_selected'], [])

    def test_run_batch_not_selected(self):
        self.write_phase(phase_document(items=['A03']))
        code, calls = self.run_batch()
        self.assertEqual(code, 0)
        self.assertEqual(len(calls), 3)
        self.assertEqual(len(self.p.state()['not_selected']), 4)
        self.assertEqual(json.loads((self.artifacts / 'idle-observation-batch.json').read_text())['client_slots'], 15)

    def test_run_batch_fails_when_one_capture_incomplete(self):
        code, calls = self.run_batch(fail=3)
        self.assertEqual(code, 1)
        self.assertEqual(len(calls), 7)
        self.assertIs(json.loads((self.artifacts / 'idle-observation-batch.json').read_text())['evidence_passed'], False)

    def test_run_batch_skips_without_boot_identity(self):
        code, calls = self.run_batch(boot=None)
        self.assertEqual(code, 1)
        self.assertEqual([c[5] for c in calls], ['start_cap'] * 7)

    def test_run_batch_skips_seventh_when_window_is_short(self):
        code, calls = self.run_batch(monotonic_offset=idle.ADMISSION_SECONDS - idle.CONFIGURATION_DEADLINE + 1)
        self.assertEqual(code, 1)
        self.assertEqual([c[5] for c in calls], [None] * 6 + ['start_cap'])

    def test_admission_window_matches_phase(self):
        self.assertEqual(idle.ADMISSION_SECONDS, phase.BATCH_START_CAP)


if __name__ == '__main__':
    unittest.main()

"""Offline trial contracts; produced-byte cases run only through the required exporter."""
import importlib.util
import json
import os
import time
import unittest
from unittest.mock import patch

try:
    from . import test_phase as support
except ImportError:
    import test_phase as support

phase = support.phase
SPEC = importlib.util.spec_from_file_location('b10_abrupt', support.REPO / 'infra/scripts/phase/b10_abrupt.py')
abrupt = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(abrupt)


class Setup:
    setUp = support.PhaseTests.setUp
    main = support.PhaseTests.main
    prepare = support.PhaseTests.prepare

    def diagnostic(self):
        os.environ['PARCHWELL_SCENARIO'] = 'B10'
        self.prepare('C1')
        self.p = phase.Phase()
        document = self.p.read('phase.json')
        document.update(purpose='diagnostic_trial', run_kind='smoke', b10_abrupt_diagnostic=True)
        phase.write_json(self.private / 'phase.json', document)
        self.trial = abrupt.Trial(self.p, phase.Failure)
        self.trial.directory.mkdir()


class ByteIndependentCases(Setup, unittest.TestCase):
    def test_c0_has_only_ordinary_loadgen(self):
        self.assertEqual(('loadgen',), abrupt.execution_modes('C0'))
        self.assertEqual(('loadgen', 'loadgen2'), abrupt.execution_modes('C1'))

    def test_prepare_requires_explicit_full_selector_and_confirmation(self):
        with patch.object(phase.Phase, 'prepare_shared') as shared:
            self.assertNotEqual(0, self.main('prepare-b10-abrupt-trial'))
            shared.assert_not_called()
            os.environ.update(PARCHWELL_SCENARIO='B10', PARCHWELL_PHASE='C1', PARCHWELL_CONFIGURATION='C1',
                PARCHWELL_REPEAT='1', PARCHWELL_RUN_KIND='smoke', PARCHWELL_PURPOSE='diagnostic_trial',
                PARCHWELL_B10_ABRUPT_DIAGNOSTIC='true', PARCHWELL_CONFIRM_DIAGNOSTIC_TRIAL='true')
            shared.return_value = 0
            self.assertEqual(0, self.main('prepare-b10-abrupt-trial'))
            shared.assert_called_once_with('B10', 'C1', 'C1', 'b10', purpose='diagnostic_trial', run_kind='smoke', b10_abrupt_diagnostic=True)

    def test_every_scientific_entry_rejects_before_work_and_removes_candidate(self):
        self.diagnostic()
        for name, args in [('prepare', ('C1',)), ('validate_scenario', ()), ('run_repeats', ()),
                ('judge', ()), ('candidate', ()), ('candidate_download', ()), ('check_candidate', (b'{}',))]:
            with self.subTest(name=name), patch.object(self.p, 'run') as run:
                for filename in ('candidate.json', 'candidate.json.partial'):
                    (self.candidate_dir / filename).write_bytes(b'not accepted')
                with self.assertRaisesRegex(phase.Failure, 'b10_abrupt_diagnostic_not_scientific'):
                    getattr(self.p, name)(*args)
                run.assert_not_called()
                self.assertFalse((self.candidate_dir / 'candidate.json').exists())
                self.assertFalse((self.candidate_dir / 'candidate.json.partial').exists())
                self.assertIn('full_allowed=false', self.output_file.read_text())

    def test_configmaps_are_immutable_and_cleanup_selects_both(self):
        self.diagnostic()
        with patch.object(self.p, 'run', return_value=0) as run:
            for role in ('first', 'successor'):
                value = self.trial.configmap(role, {'synthetic_transport_check': True})
                self.assertEqual({'app.kubernetes.io/instance': self.run_id}, value['metadata']['labels'])
                self.assertIs(value['immutable'], True)
                self.assertEqual(self.run_id + '-b10-abrupt-' + role, value['metadata']['name'])
                self.assertEqual('create', run.call_args.args[0][1])
            self.p.delete_workload()
            self.assertEqual('jobs,configmaps', run.call_args.args[0][2])

    def test_pod_unknown_never_authorizes_successor_and_sets_safety_stop(self):
        self.diagnostic()
        record = {'job': self.run_id + '-c1-r1-loadgen', 'mode': 'loadgen'}
        for payload in ({'items': []}, {'items': [{}, {}]}, {'items': [{}]}):
            def run(_args, _seconds, output=None, **_kwargs):
                output.write_text(json.dumps(payload))
                return 0
            with self.subTest(payload=payload), patch.object(self.p, 'run', side_effect=run):
                with self.assertRaisesRegex(ValueError, 'b10_abrupt_first_termination_unknown'):
                    self.trial.observe_first_termination(record)
                self.assertTrue(self.p.state()['safety_stop'])

    def test_diagnostic_container_limits_precede_decoding(self):
        for entries in (4095, 4096):
            self.assertEqual(entries, len(abrupt.decode(json.dumps([0] * entries).encode())))
        with self.assertRaisesRegex(ValueError, 'entry bound'):
            abrupt.decode(json.dumps([0] * 4097).encode())
        self.assertIsNotNone(abrupt.decode(('[' * 32 + '0' + ']' * 32).encode()))
        with self.assertRaisesRegex(ValueError, 'depth bound'):
            abrupt.decode(('[' * 33 + '0' + ']' * 33).encode())

    def test_late_release_never_creates_or_launches_successor(self):
        self.diagnostic()
        with patch.object(self.p, 'run') as run:
            with self.assertRaisesRegex(ValueError, 'b10_abrupt_successor_late'):
                self.trial.release_successor({'release_utc': '2000-01-01T00:00:00.0000000+00:00'}, time.monotonic() + 30)
            run.assert_not_called()


class ProducedBytesCases(Setup):
    """Only the export checker creates a TestCase from this production-byte mixin."""
    def produced(self):
        self.diagnostic()
        self.first = {name: (self.export_directory / 'first' / name).read_bytes() for name in abrupt.FIRST_FILES}
        self.successor = {name: (self.export_directory / 'successor' / name).read_bytes() for name in abrupt.SUCCESSOR_FILES}
        identity = abrupt.decode(self.first['termination.json'])['identity']
        document = self.p.read('phase.json')
        document.update(run_id=identity['run_id'], source_commit=identity['source_commit'])
        document['images']['loadgen']['reference'] = identity['image_reference']
        phase.write_json(self.private / 'phase.json', document)
        self.observation = {key: identity[key] for key in ('job_name', 'pod_name', 'pod_uid', 'source_commit', 'image_reference')}
        self.observation['finished_utc'] = '2030-01-01T00:00:00.0000000+00:00'
        self.record = {'job': identity['job_name'], 'mode': 'loadgen'}
        # Metadata transport is independently exercised by the ordinary start-line suite.
        # This marker is not diagnostic producer proof or passed to the production successor.
        phase.write_json(self.private / 'b10-abrupt-start-line-binding.json', {'metadata_transport_test': True})

    def test_produced_first_and_successor_transport(self):
        self.produced()
        binding = self.trial.accept_diagnostic_publication(self.observation, self.first)
        (self.trial.directory / 'predecessor-binding.json').write_bytes(abrupt.encode(binding))
        identity = abrupt.decode(self.successor['completion.json'])['identity']
        observation = {key: identity[key] for key in ('job_name', 'pod_name', 'pod_uid', 'source_commit', 'image_reference')}
        self.assertTrue(self.trial.collect(self.successor, observation)['cleanup_completed'])
        with patch.object(abrupt.time, 'time', return_value=0), patch.object(self.p, 'run', return_value=0), patch.object(self.trial, 'job', return_value={'outcome': 'complete'}) as job:
            self.trial.release_successor(binding, time.monotonic() + 60)
            job.assert_called_once()
            configmap = json.loads((self.private / 'b10-abrupt-successor-configmap.json').read_bytes())
            self.assertIs(configmap['immutable'], True)
            self.assertEqual({'app.kubernetes.io/instance': identity['run_id']}, configmap['metadata']['labels'])

    def test_produced_pod_binding_rejects_disappearance_replaced_uid_and_running(self):
        self.produced()
        def pod_payload():
            return {'items': [{'metadata': {'name': self.observation['pod_name'], 'uid': self.observation['pod_uid'],
                'ownerReferences': [{'controller': True, 'kind': 'Job', 'name': self.record['job']}]},
                'spec': {'containers': [{'name': 'loadgen', 'image': self.observation['image_reference'], 'env': [
                    {'name': 'B10Abrupt__SourceCommit', 'value': self.observation['source_commit']},
                    {'name': 'B10Abrupt__JobName', 'value': self.record['job']}]}]},
                'status': {'containerStatuses': [{'name': 'loadgen', 'state': {'terminated': {'exitCode': 0,
                    'finishedAt': self.observation['finished_utc']}}}]}}]}
        payload = pod_payload()
        def run(_args, _seconds, output=None, **_kwargs):
            output.write_bytes(abrupt.encode(payload))
            return 0
        with patch.object(self.p, 'run', side_effect=run):
            actual = self.trial.observe_first_termination(self.record)
            self.trial.accept_diagnostic_publication(actual, self.first)
            payload['items'][0]['metadata']['uid'] = 'replaced'
            actual = self.trial.observe_first_termination(self.record)
            with self.assertRaises(ValueError):
                self.trial.accept_diagnostic_publication(actual, self.first)
            payload = pod_payload()
            payload['items'][0]['status']['containerStatuses'][0]['state'] = {'running': {}}
            with self.assertRaisesRegex(ValueError, 'first_termination_unknown'):
                self.trial.observe_first_termination(self.record)
        self.assertTrue(self.p.state()['safety_stop'])

    def test_produced_mutations_are_not_accepted(self):
        self.produced()
        cases = [('termination.json', lambda value: value['data'].update(eof_complete=False)),
                 ('termination.json', lambda value: value['identity'].update(source_commit='f' * 40)),
                 ('termination.json', lambda value: value.update(schema_version=1)),
                 ('termination.json', lambda value: value.update(purpose='scientific')),
                 ('control-seal.json', lambda value: value['data'].update(drain_complete=False)),
                 ('kill-selection.json', lambda value: value['data'].update(entries=[]))]
        for name, mutate in cases:
            files = dict(self.first)
            value = abrupt.decode(files[name])
            mutate(value)
            files[name] = abrupt.encode(value)
            with self.subTest(name=name), self.assertRaises(ValueError):
                self.trial.accept_diagnostic_publication(self.observation, files)
        missing = dict(self.first)
        del missing['termination.json']
        with self.assertRaises(ValueError):
            self.trial.accept_diagnostic_publication(self.observation, missing)

    def test_produced_checkpoint_descriptor_binds_exact_frame_bytes(self):
        self.produced()
        for key, value in [('name', 'journal.jsonl'), ('length', 0), ('sha256', '0' * 64)]:
            files = dict(self.first)
            selection = abrupt.decode(files['kill-selection.json'])
            selection['data']['checkpoint_descriptor'][key] = value
            files['kill-selection.json'] = abrupt.encode(selection)
            manifest = abrupt.decode(files['abrupt-manifest.json'])
            manifest['data']['selection'] = abrupt.descriptor('kill-selection.json', files['kill-selection.json'])
            files['abrupt-manifest.json'] = abrupt.encode(manifest)
            termination = abrupt.decode(files['termination.json'])
            termination['data']['manifest'] = abrupt.descriptor('abrupt-manifest.json', files['abrupt-manifest.json'])
            files['termination.json'] = abrupt.encode(termination)
            with self.subTest(field=key), self.assertRaisesRegex(ValueError, 'diagnostic descriptor mismatch'):
                self.trial.accept_diagnostic_publication(self.observation, files)

    def test_produced_candidate_schema_only_relabel_still_rejected(self):
        self.produced()
        document = self.p.read('phase.json')
        for key in ('purpose', 'b10_abrupt_diagnostic'):
            document.pop(key)
        phase.write_json(self.private / 'phase.json', document)
        candidate = abrupt.decode(self.first['termination.json'])
        candidate['schema_version'] = 1
        with patch.object(self.p, 'run') as run:
            with self.assertRaisesRegex(phase.Failure, 'b10_abrupt_diagnostic_not_scientific'):
                self.p.check_candidate(abrupt.encode(candidate))
            run.assert_not_called()
        self.assertIn('full_allowed=false', self.output_file.read_text())

    def test_failed_publication_blocks_successor_in_real_trial(self):
        self.produced()
        cases = []
        missing = dict(self.first)
        del missing['termination.json']
        cases.append(missing)
        failed_control = dict(self.first)
        control = abrupt.decode(failed_control['control-seal.json'])
        control['data']['drain_complete'] = False
        failed_control['control-seal.json'] = abrupt.encode(control)
        cases.append(failed_control)
        for files in cases:
            state = self.p.state()
            state.pop('b10_abrupt_started', None)
            self.p.save_state(state)
            with self.subTest(files=sorted(files)), \
                 patch.object(self.p, 'b10_deadline', return_value=time.monotonic() + 18000), \
                 patch.object(self.p, 'run_mode', return_value={'outcome': 'complete'}), \
                 patch.object(self.trial, 'metadata', return_value=None), \
                 patch.object(self.trial, 'job', return_value={**self.record, 'outcome': 'complete'}), \
                 patch.object(self.trial, 'observe_first_termination', return_value=self.observation), \
                 patch.object(self.trial, 'download', return_value=files), \
                 patch.object(self.trial, 'release_successor') as release:
                self.assertEqual(1, self.trial.run())
                release.assert_not_called()

    def test_trial_order_and_delayed_logs_outside_release_path(self):
        self.produced()
        order = []
        identity = abrupt.decode(self.first['termination.json'])['identity']
        payload = {'items': [{'metadata': {'name': identity['pod_name'], 'uid': identity['pod_uid'],
            'ownerReferences': [{'controller': True, 'kind': 'Job', 'name': identity['job_name']}]},
            'spec': {'containers': [{'name': 'loadgen', 'image': identity['image_reference'], 'env': [
                {'name': 'B10Abrupt__SourceCommit', 'value': identity['source_commit']},
                {'name': 'B10Abrupt__JobName', 'value': identity['job_name']}]}]},
            'status': {'containerStatuses': [{'name': 'loadgen', 'state': {'terminated': {'exitCode': 0,
                'finishedAt': self.observation['finished_utc']}}}]}}]}
        prefix = identity['run_id'] + '/C1/r1/b10-abrupt-diagnostic/' + identity['pod_name'] + '/'
        self.fake.blobs.update({prefix + name: raw for name, raw in self.first.items()})
        def command(args, seconds, output=None, maximum=4 * 1024 * 1024):
            if args[:3] == ['kubectl', 'get', 'pods']:
                order.append('termination')
                output.write_bytes(abrupt.encode(payload))
                return 0
            if args[:2] == ['kubectl', 'apply']:
                order.append('loadgen2' if 'loadgen2' in args[3] else 'loadgen')
            if 'collect-job' in args:
                # Any slow collection before successor admission fails immediately.
                self.assertIn('loadgen2', order)
                order.append('slow-logs')
            return self.fake(self.p, args, seconds, output, maximum)
        def publication(observation, files):
            order.append('publication')
            return abrupt.Trial.accept_diagnostic_publication(self.trial, observation, files)
        with patch.object(self.p, 'b10_deadline', return_value=time.monotonic() + 18000), \
             patch.object(self.p, 'run_mode', side_effect=lambda *args, **kwargs: order.append('metadata') or {'outcome': 'complete'}), \
             patch.object(self.trial, 'metadata', return_value=None), \
             patch.object(self.trial, 'accept_diagnostic_publication', side_effect=publication), \
             patch.object(abrupt.time, 'time', return_value=0), \
             patch.object(self.p, 'run', side_effect=command):
            self.assertEqual(0, self.trial.run())
        self.assertEqual(['metadata', 'loadgen', 'termination', 'publication', 'loadgen2'], order)
        modes = [record['mode'] for record in self.p.state()['modes']]
        self.assertEqual(['loadgen', 'loadgen2'], modes)
        self.assertFalse((self.candidate_dir / 'candidate.json').exists())
        self.assertFalse(any('collect-job' in call[0] for call in self.fake.calls))

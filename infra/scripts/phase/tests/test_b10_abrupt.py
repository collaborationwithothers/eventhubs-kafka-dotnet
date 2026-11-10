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
SOURCE = (support.REPO / 'infra/scripts/phase/b10_abrupt.py').read_text().splitlines()
SITE_MEMBERS = {'file', 'function', 'line', 'origin_file', 'origin_function', 'origin_line'}


def source_line(text, offset=0):
    """Return the one source line that contains text, plus offset, so no test pins a literal line number."""
    found = [number for number, line in enumerate(SOURCE, 1) if text in line]
    assert len(found) == 1, (text, found)
    return found[0] + offset


def site(diagnostic):
    return {key: diagnostic['site'][key] for key in ('file', 'function', 'line')}


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
        cases = ({'items': []}, {'items': [{}, {}]}, {'items': [{}]},
                 {'items': [{'metadata': {'ownerReferences': [None]}, 'spec': {}}]},
                 {'items': [{'metadata': {'ownerReferences': [[]]}, 'spec': {}}]},
                 {'items': [{'metadata': None, 'spec': {}}]})
        for payload in cases:
            state = self.p.state()
            state.update(safety_stop=False, b10_abrupt_gaps=[])
            self.p.save_state(state)
            def run(_args, _seconds, output=None, **_kwargs):
                output.write_text(json.dumps(payload))
                return 0
            with self.subTest(payload=payload), patch.object(self.p, 'run', side_effect=run):
                with self.assertRaisesRegex(ValueError, 'b10_abrupt_first_termination_unknown'):
                    self.trial.observe_first_termination(record)
                self.assertTrue(self.p.state()['safety_stop'])
                self.assertEqual(['b10_abrupt_first_termination_unknown'], self.p.state()['b10_abrupt_gaps'])

    def test_outer_unknown_termination_mapping_sets_safety_stop(self):
        self.diagnostic()
        self.assertFalse(self.p.state()['safety_stop'])
        self.trial.record_failure(AttributeError('synthetic malformed owner'),
                                  'b10_abrupt_first_termination_unknown', 'first_termination')
        self.assertTrue(self.p.state()['safety_stop'])
        self.assertEqual(['b10_abrupt_first_termination_unknown'], self.p.state()['b10_abrupt_gaps'])

    def test_diagnostic_container_limits_precede_decoding(self):
        for entries in (4095, 4096):
            self.assertEqual(entries, len(abrupt.decode(json.dumps([0] * entries).encode())))
        with self.assertRaisesRegex(ValueError, 'entry bound'):
            abrupt.decode(json.dumps([0] * 4097).encode())
        self.assertIsNotNone(abrupt.decode(('[' * 32 + '0' + ']' * 32).encode()))
        with self.assertRaisesRegex(ValueError, 'depth bound'):
            abrupt.decode(('[' * 33 + '0' + ']' * 33).encode())

    def test_collection_command_and_copy_failures_record_named_gaps(self):
        self.diagnostic()
        record = {'mode': 'loadgen', 'job': self.run_id + '-c1-r1-loadgen', 'apply': 0}
        for code, expected in [(1, 'b10_abrupt_publication_ambiguous'), (124, 'b10_abrupt_deadline_exhausted')]:
            state = self.p.state()
            state.update(modes=[record], b10_abrupt_gaps=[], b10_abrupt_diagnostics=[])
            self.p.save_state(state)
            with self.subTest(code=code), patch.object(self.p, 'run', return_value=code):
                self.assertEqual(1, self.trial.collect())
            self.assertEqual([expected], self.p.state()['b10_abrupt_gaps'])
            self.assertEqual('job_collection', self.p.state()['b10_abrupt_diagnostics'][0]['boundary'])
            diagnostic = self.p.state()['b10_abrupt_diagnostics'][0]
            self.assertEqual({'file': 'b10_abrupt.py', 'function': 'collect',
                              'line': source_line("self.record_failure(ValueError(reason), reason, 'job_collection')")},
                             site(diagnostic))
            self.assertIsNone(diagnostic['site']['origin_line'])
        state.update(b10_abrupt_gaps=[], b10_abrupt_diagnostics=[])
        self.p.save_state(state)
        (self.p.artifacts / record['job']).mkdir()
        with patch.object(self.p, 'run', return_value=0), \
             patch.object(abrupt.shutil, 'copytree', side_effect=OSError('synthetic copy failure')):
            self.assertEqual(1, self.trial.collect())
        self.assertEqual(['b10_abrupt_publication_failed'], self.p.state()['b10_abrupt_gaps'])
        self.assertEqual('synthetic copy failure', self.p.state()['b10_abrupt_diagnostics'][0]['validation_detail'])
        diagnostic = self.p.state()['b10_abrupt_diagnostics'][0]
        self.assertEqual({'file': 'b10_abrupt.py', 'function': 'collect',
                          'line': source_line('shutil.copytree(source, self.directory')}, site(diagnostic))
        self.assertEqual(SITE_MEMBERS, set(diagnostic['site']))
        self.assertNotEqual('b10_abrupt.py', diagnostic['site']['origin_file'])
        self.assertNotIn('synthetic copy failure', json.dumps(diagnostic['site']))
        self.assertIsNone(diagnostic['cause_type'])

    def test_named_admission_failure_is_recorded_once(self):
        self.diagnostic()
        with self.assertRaisesRegex(abrupt.RecordedFailure, 'b10_abrupt_successor_late') as raised:
            self.trial.fail('b10_abrupt_successor_late')
        self.trial.record_failure(raised.exception, 'b10_abrupt_publication_failed', 'outer')
        self.assertEqual(['b10_abrupt_successor_late'], self.p.state()['b10_abrupt_gaps'])
        self.assertEqual('test_named_admission_failure_is_recorded_once',
                         self.p.state()['b10_abrupt_diagnostics'][0]['site']['function'])

    def test_termination_failure_records_its_cause_site(self):
        self.diagnostic()
        record = {'job': self.run_id + '-c1-r1-loadgen', 'mode': 'loadgen'}
        cases = [({'items': []}, "raise ValueError('pod count')", 'ValueError'),
                 ({'items': [{'metadata': None, 'spec': {}}]},
                  "owners = [owner for owner in metadata['ownerReferences']", 'TypeError')]
        for payload, source, cause_type in cases:
            state = self.p.state()
            state.update(safety_stop=False, b10_abrupt_gaps=[], b10_abrupt_diagnostics=[])
            self.p.save_state(state)
            def run(_args, _seconds, output=None, **_kwargs):
                output.write_text(json.dumps(payload))
                return 0
            with self.subTest(payload=payload), patch.object(self.p, 'run', side_effect=run):
                with self.assertRaisesRegex(ValueError, 'b10_abrupt_first_termination_unknown'):
                    self.trial.observe_first_termination(record)
            diagnostic = self.p.state()['b10_abrupt_diagnostics'][0]
            self.assertEqual('b10_abrupt_first_termination_unknown', diagnostic['reason'])
            self.assertEqual({'file': 'b10_abrupt.py', 'function': 'observe_first_termination',
                              'line': source_line(source)}, site(diagnostic))
            self.assertEqual(cause_type, diagnostic['cause_type'])

    def test_unfinished_first_job_names_its_site(self):
        self.diagnostic()
        record = {'job': self.run_id + '-c1-r1-loadgen', 'mode': 'loadgen', 'outcome': 'failed'}
        with patch.object(self.p, 'b10_deadline', return_value=time.monotonic() + 18000), \
             patch.object(self.p, 'run_mode', return_value={'outcome': 'complete'}), \
             patch.object(self.trial, 'metadata', return_value=None), \
             patch.object(self.trial, 'job', return_value=record), \
             patch.object(self.trial, 'observe_first_termination', return_value={}), \
             patch.object(self.trial, 'download') as download:
            self.assertEqual(1, self.trial.run())
            download.assert_not_called()
        diagnostic = self.p.state()['b10_abrupt_diagnostics'][0]
        self.assertEqual('b10_abrupt_publication_failed', diagnostic['reason'])
        self.assertEqual('first_termination', diagnostic['boundary'])
        self.assertEqual('ValueError', diagnostic['exception_type'])
        expected = {'file': 'b10_abrupt.py', 'function': 'run',
                    'line': source_line("if first['outcome'] != 'complete':", 1)}
        self.assertEqual(expected, site(diagnostic))
        self.assertEqual({'origin_' + key: value for key, value in expected.items()},
                         {key: diagnostic['site'][key] for key in ('origin_file', 'origin_function', 'origin_line')})
        self.assertIsNone(diagnostic['cause_type'])

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
        self.cohort = {name: (self.export_directory / 'first' / name).read_bytes() for name in abrupt.COHORT_FILES}
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

    def rebound(self, edit):
        """Apply edit to the decoded seals, manifest and termination and to the journal lines, then rebind every
        descriptor above the edit, so only the targeted check can fail."""
        files = dict(self.first)
        names = ('timeout-seal.json', 'control-seal.json', 'abrupt-manifest.json', 'termination.json')
        objects = {name: abrupt.decode(files[name]) for name in names}
        lines = files['journal.jsonl'].splitlines()
        edit(objects, lines)
        files['journal.jsonl'] = b'\n'.join(lines) + b'\n'
        for name in ('timeout-seal.json', 'control-seal.json'):
            files[name] = abrupt.encode(objects[name])
        manifest = objects['abrupt-manifest.json']
        manifest['data'].update(journal=abrupt.descriptor('journal.jsonl', files['journal.jsonl']), journal_entry_count=len(lines),
            timeout_seal=abrupt.descriptor('timeout-seal.json', files['timeout-seal.json']),
            control_seal=abrupt.descriptor('control-seal.json', files['control-seal.json']))
        files['abrupt-manifest.json'] = abrupt.encode(manifest)
        objects['termination.json']['data']['manifest'] = abrupt.descriptor('abrupt-manifest.json', files['abrupt-manifest.json'])
        files['termination.json'] = abrupt.encode(objects['termination.json'])
        return files

    @staticmethod
    def native_lines(lines, *captures):
        """Journal lines for native_error frames appended after the last frame; captures are (capture_sequence, is_fatal)."""
        last = abrupt.decode(lines[-1], abrupt.JOURNAL_MAXIMUM)
        added = []
        for offset, (capture, fatal) in enumerate(captures, 1):
            frame = dict(last, kind='native_error', sequence=last['sequence'] + offset, data={
                'capture_sequence': capture, 'error_code': 45 if fatal else -169,
                'error_name': 'OutOfOrderSequenceNumber' if fatal else 'Local_Authentication',
                'is_error': True, 'is_fatal': fatal, 'callback_monotonic_ticks': 1})
            added.append(abrupt.encode(frame))
        return added

    def test_produced_cohort_files_accepted(self):
        self.produced()
        binding = self.trial.accept_diagnostic_publication(self.observation, self.first)
        self.assertEqual(['abrupt-manifest.json', 'control-seal.json', 'kill-selection.json', 'registration.json',
                          'termination.json', 'timeout-seal.json'], sorted(binding['files']))
        manifest = abrupt.decode(self.first['abrupt-manifest.json'])['data']
        self.assertEqual(list(abrupt.COHORT_FILES), [entry['name'] for entry in manifest['cohort_files']])
        for name, entry in zip(abrupt.COHORT_FILES, manifest['cohort_files']):
            abrupt.bound(entry, name, self.cohort[name])
        # The same-instance and cross-restart reports can hold identical bytes to a post-drain report: binding is by role.
        # Two journaled native errors, one fatal, with seals that covered none and one of them, are accepted.
        def edit(objects, lines):
            lines.extend(self.native_lines(lines, (1, False), (2, True)))
            objects['termination.json']['data'].update(native_error_count=2, native_fatal_count=1)
            objects['control-seal.json']['data'].update(native_error_count=1)
        self.trial.accept_diagnostic_publication(self.observation, self.rebound(edit))

    def test_produced_cohort_mutations_rejected(self):
        self.produced()
        def cohort(change):
            def edit(objects, _lines):
                change(objects['abrupt-manifest.json']['data']['cohort_files'])
            return edit
        def digit(entries):
            entries[3]['sha256'] = ('0' if entries[3]['sha256'][0] != '0' else '1') + entries[3]['sha256'][1:]
        def termination(**changes):
            return lambda objects, _lines: objects['termination.json']['data'].update(changes)
        count = abrupt.decode(self.first['termination.json'])['data']['native_error_count']
        def swap(entries):
            entries[0], entries[1] = entries[1], entries[0]
        def length(entries):
            entries[2]['length'] += 1
        def seal(objects, _lines):
            objects['timeout-seal.json']['data']['native_error_count'] = count + 1
        def gap(objects, lines):
            lines.extend(self.native_lines(lines, (1, False), (3, False)))
            objects['termination.json']['data'].update(native_error_count=2, native_fatal_count=0)
        cases = {'seven entries': cohort(lambda entries: entries.pop()), 'swapped entries': cohort(swap),
                 'length plus one': cohort(length), 'sha256 digit': cohort(digit),
                 'termination status': termination(fatal_capture_status='not_implemented_pr3'),
                 'termination count': termination(native_error_count=count + 1),
                 'seal count above journal': seal, 'sequence gap': gap}
        for label, edit in cases.items():
            with self.subTest(label=label), self.assertRaises(ValueError):
                self.trial.accept_diagnostic_publication(self.observation, self.rebound(edit))
        # Each mutation, applied to the checker's input, is the only difference from an accepted publication.
        self.trial.accept_diagnostic_publication(self.observation, self.rebound(lambda objects, lines: None))

    def test_produced_cohort_collection(self):
        self.produced()
        binding = self.trial.accept_diagnostic_publication(self.observation, self.first)
        identity = abrupt.decode(self.successor['completion.json'])['identity']
        successor_observation = {key: identity[key] for key in ('job_name', 'pod_name', 'pod_uid', 'source_commit', 'image_reference')}
        prefix = self.observation['pod_name'].join((identity['run_id'] + '/C1/r1/b10-abrupt-diagnostic/', '/'))
        original = abrupt.Trial.download
        calls = []
        def command(args, seconds, output=None, maximum=4 * 1024 * 1024):
            calls.append(args)
            return 0 if 'collect-job' in args else self.fake(self.p, args, seconds, output, maximum)
        def download(observation, names, deadline):
            calls.append(tuple(names))
            return original(self.trial, observation, names, deadline) if tuple(names) == abrupt.COHORT_FILES else self.successor
        def run(blobs, document=None):
            state = self.p.state()
            state.update(modes=[{'mode': 'loadgen2', 'job': identity['job_name'], 'apply': 0}], b10_abrupt_gaps=[], b10_abrupt_diagnostics=[])
            self.p.save_state(state)
            (self.trial.directory / 'predecessor-binding.json').write_bytes(abrupt.encode(document or binding))
            self.fake.blobs.clear()
            self.fake.blobs.update({prefix + name: raw for name, raw in blobs.items()})
            del calls[:]
            with patch.object(self.trial, 'observe_first_termination', return_value=successor_observation), \
                 patch.object(self.trial, 'download', side_effect=download), \
                 patch.object(self.p, 'b10_deadline', return_value=time.monotonic() + 60), \
                 patch.object(self.p, 'run', side_effect=command):
                return self.trial.collect()
        self.assertEqual(0, run(self.cohort))
        self.assertEqual([], self.p.state()['b10_abrupt_gaps'])
        for name in abrupt.COHORT_FILES:
            self.assertEqual(self.cohort[name], (self.trial.directory / self.observation['pod_name'] / name).read_bytes())
        # One byte changed: named once, collection continues with the successor and the Job logs.
        changed = dict(self.cohort)
        changed['control-ledger.jsonl'] = bytes([changed['control-ledger.jsonl'][0] ^ 1]) + changed['control-ledger.jsonl'][1:]
        self.assertEqual(1, run(changed))
        self.assertEqual(['b10_abrupt_publication_failed'], self.p.state()['b10_abrupt_gaps'])
        self.assertEqual('first_cohort_publication', self.p.state()['b10_abrupt_diagnostics'][0]['boundary'])
        self.assertIn(abrupt.SUCCESSOR_FILES, calls)
        self.assertTrue(any('collect-job' in call for call in calls if isinstance(call, list)))
        # A missing object is ambiguous, not failed.
        missing = dict(self.cohort)
        del missing['timeout-post-drain-report.json']
        self.assertEqual(1, run(missing))
        self.assertEqual(['b10_abrupt_publication_ambiguous'], self.p.state()['b10_abrupt_gaps'])
        self.assertEqual('first_cohort_publication', self.p.state()['b10_abrupt_diagnostics'][0]['boundary'])
        self.assertIn(abrupt.SUCCESSOR_FILES, calls)
        # A 70000-byte ledger is above the 65536-byte auxiliary limit that applied before the cohort limits existed.
        large = dict(self.cohort)
        large['timeout-ledger.jsonl'] = b'x' * 70000
        document = dict(binding)
        manifest = abrupt.decode(binding['files']['abrupt-manifest.json'].encode())
        manifest['data']['cohort_files'][1] = abrupt.descriptor('timeout-ledger.jsonl', large['timeout-ledger.jsonl'])
        document['files'] = dict(binding['files'], **{'abrupt-manifest.json': abrupt.encode(manifest).decode()})
        self.assertEqual(0, run(large, document))
        self.assertEqual([], self.p.state()['b10_abrupt_gaps'])
        self.assertEqual(70000, len((self.trial.directory / self.observation['pod_name'] / 'timeout-ledger.jsonl').read_bytes()))

    def test_produced_second_candidate_without_first_exclusion_is_stale(self):
        self.produced()
        files = dict(self.first)
        selection = abrupt.decode(files['kill-selection.json'])
        candidate = selection['data']['candidate_attempt_ids'][1]
        frames = [abrupt.decode(line) for line in files['journal.jsonl'].splitlines()]
        prepared = next(frame['data'] for frame in frames if frame['kind'] == 'prepared' and frame['data']['attempt_id'] == candidate)
        selection['data']['entries'][0].update({key: prepared[key] for key in
            ('attempt_id', 'key', 'sequence', 'payload_length', 'payload_sha256')})
        files['kill-selection.json'] = abrupt.encode(selection)
        self.rebind_first_publication(files)
        with self.assertRaisesRegex(ValueError, '^selection stale$'):
            self.trial.accept_diagnostic_publication(self.observation, files)

    def test_produced_exclusion_without_journaled_terminal_is_stale(self):
        self.produced()
        files = dict(self.first)
        selection = abrupt.decode(files['kill-selection.json'])
        first_candidate, second_candidate = selection['data']['candidate_attempt_ids'][:2]
        frames = [abrupt.decode(line) for line in files['journal.jsonl'].splitlines()]
        prepared = {frame['data']['attempt_id']: frame['data'] for frame in frames if frame['kind'] == 'prepared'}
        accepted = {frame['data']['attempt_id'] for frame in frames if frame['kind'] == 'accepted'}
        terminal = {frame['data']['attempt_id'] for frame in frames if frame['kind'] == 'terminal'}
        for candidate in (first_candidate, second_candidate):
            self.assertIn(candidate, prepared)
            self.assertIn(candidate, accepted)
            self.assertNotIn(candidate, terminal)
        selection['data']['terminal_exclusions'] = [{'attempt_id': first_candidate, 'terminal_sequence': 1}]
        selection['data']['entries'][0].update({key: prepared[second_candidate][key] for key in
            ('attempt_id', 'key', 'sequence', 'payload_length', 'payload_sha256')})
        files['kill-selection.json'] = abrupt.encode(selection)
        self.rebind_first_publication(files)
        with self.assertRaisesRegex(ValueError, '^selection stale$'):
            self.trial.accept_diagnostic_publication(self.observation, files)

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

    def rebind_first_publication(self, files):
        manifest = abrupt.decode(files['abrupt-manifest.json'])
        for key, name in [('registration', 'registration.json'), ('journal', 'journal.jsonl'),
                          ('timeout_seal', 'timeout-seal.json'), ('control_seal', 'control-seal.json'),
                          ('selection', 'kill-selection.json')]:
            manifest['data'][key] = abrupt.descriptor(name, files[name])
        files['abrupt-manifest.json'] = abrupt.encode(manifest)
        termination = abrupt.decode(files['termination.json'])
        termination['data']['manifest'] = abrupt.descriptor('abrupt-manifest.json', files['abrupt-manifest.json'])
        files['termination.json'] = abrupt.encode(termination)
        return files

    def test_trial_records_approved_names_for_produced_transport_failures(self):
        self.produced()
        cases = []
        files = dict(self.first)
        receipt = abrupt.decode(files['termination.json'])
        receipt['data']['manifest']['sha256'] = '0' * 64
        files['termination.json'] = abrupt.encode(receipt)
        cases.append((files, 'b10_abrupt_publication_failed', 'diagnostic descriptor mismatch', 'bound'))
        files = dict(self.first)
        receipt = abrupt.decode(files['termination.json'])
        receipt['identity']['source_commit'] = 'f' * 40
        files['termination.json'] = abrupt.encode(receipt)
        cases.append((files, 'b10_abrupt_successor_binding_invalid', 'diagnostic identity', 'envelope'))
        files = dict(self.first)
        selection = abrupt.decode(files['kill-selection.json'])
        frames = [abrupt.decode(line) for line in files['journal.jsonl'].splitlines()]
        checkpoint = next(frame for frame in frames if frame['kind'] == 'checkpoint' and frame['data']['arm'] == 'cross_restart')
        checkpoint['data']['release_utc'] = checkpoint['data']['checkpoint_utc']
        selection['data']['checkpoint'] = checkpoint
        selection['data']['checkpoint_descriptor'] = abrupt.descriptor('cross_restart-checkpoint.json', abrupt.encode(checkpoint))
        files['journal.jsonl'] = b''.join(abrupt.encode(frame) + b'\n' for frame in frames)
        files['kill-selection.json'] = abrupt.encode(selection)
        cases.append((self.rebind_first_publication(files), 'b10_abrupt_successor_binding_invalid', 'release boundary', 'accept_diagnostic_publication'))
        for files, expected, detail, function in cases:
            state = self.p.state()
            state.pop('b10_abrupt_started', None)
            state.update(b10_abrupt_gaps=[], b10_abrupt_diagnostics=[])
            self.p.save_state(state)
            with self.subTest(detail=detail), \
                 patch.object(self.p, 'b10_deadline', return_value=time.monotonic() + 18000), \
                 patch.object(self.p, 'run_mode', return_value={'outcome': 'complete'}), \
                 patch.object(self.trial, 'metadata', return_value=None), \
                 patch.object(self.trial, 'job', return_value={**self.record, 'outcome': 'complete'}), \
                 patch.object(self.trial, 'observe_first_termination', return_value=self.observation), \
                 patch.object(self.trial, 'download', return_value=files), \
                 patch.object(self.trial, 'release_successor') as release:
                self.assertEqual(1, self.trial.run())
                release.assert_not_called()
            self.assertEqual([expected], self.p.state()['b10_abrupt_gaps'])
            diagnostic = self.p.state()['b10_abrupt_diagnostics'][0]
            self.assertEqual(expected, diagnostic['reason'])
            self.assertEqual(detail, diagnostic['validation_detail'])
            self.assertEqual('first_publication', diagnostic['boundary'])
            self.assertEqual(function, diagnostic['site']['function'])
            self.assertEqual(source_line("raise ValueError('" + detail + "')"), diagnostic['site']['line'])

    def test_unsuccessful_successor_records_named_trial_failure(self):
        self.produced()
        for result, expected in [({'outcome': 'unknown', 'wait': 124}, 'b10_abrupt_deadline_exhausted'),
                                 ({'outcome': 'failed', 'wait': 1}, 'b10_abrupt_publication_ambiguous')]:
            state = self.p.state()
            state.pop('b10_abrupt_started', None)
            state.update(b10_abrupt_gaps=[], b10_abrupt_diagnostics=[])
            self.p.save_state(state)
            with self.subTest(result=result), \
                 patch.object(self.p, 'b10_deadline', return_value=time.monotonic() + 18000), \
                 patch.object(self.p, 'run_mode', return_value={'outcome': 'complete'}), \
                 patch.object(self.trial, 'metadata', return_value=None), \
                 patch.object(self.trial, 'job', return_value={**self.record, 'outcome': 'complete'}), \
                 patch.object(self.trial, 'observe_first_termination', return_value=self.observation), \
                 patch.object(self.trial, 'download', return_value=self.first), \
                 patch.object(self.trial, 'release_successor', return_value=result):
                self.assertEqual(1, self.trial.run())
            self.assertEqual([expected], self.p.state()['b10_abrupt_gaps'])
            self.assertEqual('successor_job', self.p.state()['b10_abrupt_diagnostics'][0]['boundary'])

    def test_collection_retains_produced_receipt_failure_and_collects_logs(self):
        self.produced()
        binding = self.trial.accept_diagnostic_publication(self.observation, self.first)
        (self.trial.directory / 'predecessor-binding.json').write_bytes(abrupt.encode(binding))
        identity = abrupt.decode(self.successor['completion.json'])['identity']
        observation = {key: identity[key] for key in ('job_name', 'pod_name', 'pod_uid', 'source_commit', 'image_reference')}
        cases = []
        for mutation, expected, detail in [
                (lambda receipt: receipt['data']['registration'].update(sha256='0' * 64),
                 'b10_abrupt_publication_failed', 'diagnostic descriptor mismatch'),
                (lambda receipt: receipt['identity'].update(source_commit='f' * 40),
                 'b10_abrupt_successor_binding_invalid', 'diagnostic identity')]:
            files = dict(self.successor)
            receipt = abrupt.decode(files['completion.json'])
            mutation(receipt)
            files['completion.json'] = abrupt.encode(receipt)
            cases.append((files, expected, detail))
        for files, expected, detail in cases:
            state = self.p.state()
            state.update(modes=[{'mode': 'loadgen2', 'job': identity['job_name'], 'apply': 0}],
                         b10_abrupt_gaps=[], b10_abrupt_diagnostics=[])
            self.p.save_state(state)
            def download(_observation, names, _deadline, files=files):
                return self.cohort if tuple(names) == abrupt.COHORT_FILES else files
            with self.subTest(detail=detail), patch.object(self.trial, 'observe_first_termination', return_value=observation), \
                 patch.object(self.trial, 'download', side_effect=download), \
                 patch.object(self.p, 'b10_deadline', return_value=time.monotonic() + 60), \
                 patch.object(self.p, 'run', return_value=0) as run:
                self.assertEqual(1, self.trial.collect())
                self.assertEqual('collect-job', run.call_args.args[0][2])
            self.assertEqual([expected], self.p.state()['b10_abrupt_gaps'])
            diagnostic = self.p.state()['b10_abrupt_diagnostics'][0]
            self.assertEqual(detail, diagnostic['validation_detail'])
            self.assertEqual('successor_receipt', diagnostic['boundary'])
            exported = abrupt.decode((self.trial.directory / 'phase-status.json').read_bytes())
            self.assertEqual([expected], exported['b10_abrupt_gaps'])

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

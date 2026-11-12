"""Offline trial contracts; produced-byte cases run only through the required exporter."""
import ast
import copy
from datetime import datetime, timedelta, timezone
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


def source_line(text, offset=0, function=None):
    """Return the one source line that contains text, plus offset, so no test pins a literal line number."""
    scope = next((node for node in ast.walk(ast.parse('\n'.join(SOURCE)))
                  if isinstance(node, ast.FunctionDef) and node.name == function), None) if function else None
    found = [number for number, line in enumerate(SOURCE, 1) if text in line
             and (scope is None or scope.lineno <= number <= scope.end_lineno)]
    assert len(found) == 1, (text, found)
    return found[0] + offset


def site(diagnostic):
    return {key: diagnostic['site'][key] for key in ('file', 'function', 'line')}


class Setup:
    setUp = support.PhaseTests.setUp
    main = support.PhaseTests.main
    prepare = support.PhaseTests.prepare

    def timeout_pod(self, exit_code=0):
        document = self.p.read('phase.json')
        return {'items': [{'metadata': {'ownerReferences': [
            {'controller': True, 'kind': 'Job', 'name': document['run_id'] + '-c1-r1-loadgen'}]},
            'spec': {'containers': [{'name': 'loadgen', 'image': document['images']['loadgen']['reference']}]},
            'status': {'containerStatuses': [{'name': 'loadgen', 'state': {'terminated': {'exitCode': exit_code}}}]}}]}

    def timeout_record(self):
        return {'job': self.p.read('phase.json')['run_id'] + '-c1-r1-loadgen',
                'mode': 'loadgen', 'outcome': 'complete'}

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
        self.assertEqual(('loadgen-restart', 'loadgen-successor'), abrupt.execution_modes('C1'))

    def test_prepare_requires_explicit_full_selector_and_confirmation(self):
        with patch.object(phase.Phase, 'prepare_shared') as shared:
            self.assertNotEqual(0, self.main('prepare-b10-abrupt-trial'))
            shared.assert_not_called()
            os.environ.update(PARCHWELL_SCENARIO='B10', PARCHWELL_PHASE='C1', PARCHWELL_CONFIGURATION='C1',
                PARCHWELL_REPEAT='1', PARCHWELL_RUN_KIND='smoke', PARCHWELL_PURPOSE='diagnostic_trial',
                PARCHWELL_B10_ABRUPT_DIAGNOSTIC='true', PARCHWELL_CONFIRM_DIAGNOSTIC_TRIAL='true')
            shared.return_value = 0
            self.assertEqual(0, self.main('prepare-b10-abrupt-trial'))
            shared.assert_called_once_with('B10', 'C1', 'C1', 'b10d', purpose='diagnostic_trial', run_kind='smoke', b10_abrupt_diagnostic=True)

    def test_timeout_failure_name_is_closed(self):
        main_names = set('''
            b10_abrupt_diagnostic_not_scientific b10_abrupt_configuration_invalid
            b10_abrupt_platform_unsupported b10_abrupt_signal_preflight_failed
            b10_abrupt_child_start_failed b10_abrupt_child_identity_mismatch
            b10_abrupt_signal_failed b10_abrupt_exit_unobserved b10_abrupt_unexpected_exit
            b10_abrupt_capture_gap b10_abrupt_sample_missing b10_abrupt_report_binding_invalid
            b10_abrupt_guard_failed b10_abrupt_control_incomplete b10_abrupt_exposure_inconclusive
            b10_abrupt_cut_rejected kill_too_early kill_too_late b10_abrupt_cgroup_unavailable
            b10_abrupt_oom_conflict b10_abrupt_publication_failed b10_abrupt_publication_ambiguous
            b10_abrupt_first_termination_unknown b10_abrupt_successor_binding_invalid
            b10_abrupt_successor_late b10_abrupt_deadline_exhausted
        '''.split())
        self.assertEqual(main_names | {'b10_abrupt_timeout_job_unknown', 'b10_abrupt_warmup_failed', 'b10_abrupt_leader_not_up', 'b10_abrupt_verify_failed'}, abrupt.FAILURE_NAMES)
        self.assertEqual(30, len(abrupt.FAILURE_NAMES))

    def test_trial_and_science_run_ids_never_collide(self):
        diagnostic_environment = dict(PARCHWELL_SCENARIO='B10', PARCHWELL_PHASE='C1', PARCHWELL_CONFIGURATION='C1',
            PARCHWELL_REPEAT='1', PARCHWELL_RUN_KIND='smoke', PARCHWELL_PURPOSE='diagnostic_trial',
            PARCHWELL_B10_ABRUPT_DIAGNOSTIC='true', PARCHWELL_CONFIRM_DIAGNOSTIC_TRIAL='true')
        for identifier in ('1', '42925777839', '9' * 22):
            with self.subTest(identifier=identifier), patch.dict(os.environ, GITHUB_RUN_ID=identifier, GITHUB_RUN_ATTEMPT='1'):
                os.environ['PARCHWELL_SCENARIO'] = 'B10'
                self.prepare('C1')
                scientific_run = phase.Phase().read('phase.json')['run_id']
                with patch.dict(os.environ, diagnostic_environment):
                    self.assertEqual(0, abrupt.Trial(phase.Phase(), phase.Failure).prepare())
                trial_environment = dict(line.split('=', 1) for line in self.env_file.read_text().splitlines())
                with patch.dict(os.environ, trial_environment):
                    trial_run = phase.Phase().read('phase.json')['run_id']
                self.assertEqual('b10-' + identifier + '-1', scientific_run)
                self.assertEqual('b10d-' + identifier + '-1', trial_run)
                self.assertNotEqual(scientific_run, trial_run)
        with patch.dict(os.environ, {**diagnostic_environment, 'GITHUB_RUN_ID': '9' * 23}):
            with self.assertRaisesRegex(phase.Failure, '^invalid run or source commit$'):
                abrupt.Trial(phase.Phase(), phase.Failure).prepare()
        (self.private / 'phase.json').unlink()
        with patch.dict(os.environ, {**diagnostic_environment, 'PARCHWELL_PRIVATE_DIRECTORY': str(self.private)}), \
             patch.object(phase.Phase, 'prepare_shared') as shared:
            with self.assertRaisesRegex(phase.Failure, '^b10_abrupt_diagnostic_not_scientific$'):
                phase.Phase().prepare('C1')
            shared.assert_not_called()
            self.assertFalse((self.private / 'phase.json').exists())

    def run_timeout_case(self, wait=0, apply=0, render_failed=False, payload=None, pod_code=0, pod_error=False):
        state = self.p.state()
        state.pop('b10_abrupt_started', None)
        state.update(modes=[], safety_stop=False, b10_abrupt_gaps=[], b10_abrupt_diagnostics=[])
        self.p.save_state(state)
        calls = []
        job_name = self.run_id + '-c1-r1-loadgen'
        def command(args, seconds, output=None, maximum=4 * 1024 * 1024):
            calls.append((args, seconds, output))
            if args[:2] == ['kubectl', 'apply'] and args[3].endswith('rendered-' + job_name + '.yaml'):
                if isinstance(apply, Exception):
                    raise apply
                return apply
            if 'wait-job' in args and args[3] == job_name:
                return wait
            if args[:3] == ['kubectl', 'get', 'pods'] and 'job-name=' + job_name in args:
                if pod_error:
                    raise OSError('synthetic timeout pod read failure')
                output.write_bytes(payload if isinstance(payload, bytes) else abrupt.encode(
                    self.timeout_pod(exit_code=wait) if payload is None else payload))
                return pod_code
            return self.fake(self.p, args, seconds, output, maximum)
        original_render = self.p.render
        def render(mode, repeat):
            if render_failed and mode == 'loadgen':
                self.p.render_code = 1
                return None
            return original_render(mode, repeat)
        with patch.dict(support.DEADLINES, {'loadgen-restart': 1140, 'loadgen-successor': 1140}), \
             patch.object(self.p, 'b10_deadline', return_value=time.monotonic() + 18000), \
             patch.object(self.p, 'run_mode', return_value={'outcome': 'complete'}), \
             patch.object(self.trial, 'metadata', return_value=None), \
             patch.object(self.trial, 'observe_first_termination', return_value={}), \
             patch.object(self.trial, 'download', side_effect=ValueError('synthetic publication stop')), \
             patch.object(self.p, 'render', side_effect=render) as rendered, \
             patch.object(self.p, 'run', side_effect=command):
            result = self.trial.run()
        applied = [args[3] for args, _, _ in calls if args[:2] == ['kubectl', 'apply']]
        return result, rendered, applied, calls

    def test_timeout_job_outcome_rule(self):
        self.diagnostic()
        for wait, apply, render_failed, outcome, gap, safety in (
                (0, 0, False, 'complete', None, False), (1, 0, False, 'failed', None, False),
                (124, 0, False, 'unknown', 'b10_abrupt_timeout_job_unknown', True),
                (2, 0, False, 'unknown', 'b10_abrupt_timeout_job_unknown', True),
                (0, 1, False, 'apply_failed', 'b10_abrupt_timeout_job_unknown', True),
                (0, 0, True, 'render_failed', 'b10_abrupt_configuration_invalid', False)):
            with self.subTest(wait=wait, apply=apply, render_failed=render_failed):
                result, rendered, applied, calls = self.run_timeout_case(wait, apply, render_failed)
                self.assertEqual(outcome, self.p.state()['modes'][0]['outcome'])
                self.assertEqual(safety, self.p.state()['safety_stop'])
                if gap is None:
                    self.assertEqual(['loadgen', 'loadgen-restart'], [call.args[0] for call in rendered.call_args_list])
                    self.assertTrue(applied[-1].endswith('-loadgen-restart.yaml'))
                else:
                    self.assertEqual(1, result)
                    self.assertEqual(['loadgen'], [call.args[0] for call in rendered.call_args_list])
                    self.assertFalse(any('-loadgen-restart.yaml' in path for path in applied))
                    self.assertEqual([gap], self.p.state()['b10_abrupt_gaps'])
                    self.assertEqual('timeout_job', self.p.state()['b10_abrupt_diagnostics'][0]['boundary'])
                    self.assertEqual(0 if render_failed else 1, len(applied))
                reads = [args for args, _, _ in calls if args[:3] == ['kubectl', 'get', 'pods']]
                self.assertEqual(1 if gap is None else 0, len(reads))
        result, rendered, applied, _ = self.run_timeout_case(apply=OSError('synthetic apply failure'))
        self.assertEqual(1, result)
        self.assertEqual(['loadgen'], [call.args[0] for call in rendered.call_args_list])
        self.assertFalse(any('-loadgen-restart.yaml' in path for path in applied))
        self.assertEqual(['b10_abrupt_publication_failed'], self.p.state()['b10_abrupt_gaps'])
        self.assertEqual('timeout_job', self.p.state()['b10_abrupt_diagnostics'][0]['boundary'])
        self.assertFalse(self.p.state()['safety_stop'])

    def test_timeout_pod_must_be_observed_terminated(self):
        self.diagnostic()
        ordinary = self.timeout_pod()
        running = copy.deepcopy(ordinary)
        running['items'][0]['status']['containerStatuses'][0]['state'] = {'running': {}}
        string_exit = copy.deepcopy(ordinary)
        string_exit['items'][0]['status']['containerStatuses'][0]['state']['terminated']['exitCode'] = '0'
        wrong_owner = copy.deepcopy(ordinary)
        wrong_owner['items'][0]['metadata']['ownerReferences'][0]['name'] = 'other'
        wrong_image = copy.deepcopy(ordinary)
        wrong_image['items'][0]['spec']['containers'][0]['image'] = 'other'
        invalid = [dict(pod_code=1), dict(payload=b'not-json'), dict(payload={'items': []}),
                   dict(payload={'items': ordinary['items'] * 2}), dict(payload=running), dict(payload=string_exit),
                   dict(payload=wrong_owner), dict(payload=wrong_image), dict(pod_error=True)]
        for case in invalid:
            with self.subTest(case=case):
                result, rendered, applied, calls = self.run_timeout_case(**case)
                self.assertEqual(1, result)
                self.assertEqual('unknown', self.p.state()['modes'][0]['outcome'])
                self.assertEqual(['loadgen'], [call.args[0] for call in rendered.call_args_list])
                self.assertFalse(any('-loadgen-restart.yaml' in path for path in applied))
                self.assertTrue(self.p.state()['safety_stop'])
                self.assertEqual(['b10_abrupt_timeout_job_unknown'], self.p.state()['b10_abrupt_gaps'])
                self.assertEqual('timeout_job', self.p.state()['b10_abrupt_diagnostics'][0]['boundary'])
                self.assertEqual(1, len([args for args, _, _ in calls if args[:3] == ['kubectl', 'get', 'pods']]))
        for wait in (0, 1):
            with self.subTest(valid_exit=wait):
                _, rendered, applied, calls = self.run_timeout_case(wait=wait)
                self.assertEqual(['loadgen', 'loadgen-restart'], [call.args[0] for call in rendered.call_args_list])
                self.assertTrue(applied[-1].endswith('-loadgen-restart.yaml'))
                self.assertTrue((self.trial.directory / 'loadgen-pods.json').exists())
                reads = [(args, seconds, output) for args, seconds, output in calls if args[:3] == ['kubectl', 'get', 'pods']]
                self.assertEqual([(['kubectl', 'get', 'pods', '-n', 'parchwell', '-l',
                    'job-name=' + self.run_id + '-c1-r1-loadgen', '-o', 'json', '--request-timeout=30s'],
                    30, self.trial.directory / 'loadgen-pods.json')], reads)

    def test_timeout_job_render_is_ordinary(self):
        self.diagnostic()
        with patch.object(self.p, 'run', side_effect=lambda *args, **kwargs: self.fake(self.p, *args, **kwargs)), \
             patch.object(self.p, 'b10_abrupt', return_value=self.trial), patch.object(self.trial, 'values') as hook:
            self.assertEqual(1140, self.p.render('loadgen', 1))
            hook.assert_not_called()
        values = abrupt.decode((self.private / ('values-' + self.run_id + '-c1-r1-loadgen.json')).read_bytes())
        self.assertNotIn('b10_abrupt_diagnostic', values['runtime'])
        self.assertNotIn('b10_abrupt_binding_configmap', values['runtime'])

    def test_collection_copies_timeout_job_into_diagnostic_artifact(self):
        self.diagnostic()
        modes = ('start-line', 'loadgen', 'loadgen-restart')
        records = [{'mode': mode, 'job': self.run_id + '-c1-r1-' + mode, 'apply': 0} for mode in modes]
        state = self.p.state()
        state['modes'] = records
        self.p.save_state(state)
        for record in records:
            directory = self.p.artifacts / record['job']
            directory.mkdir()
            (directory / 'synthetic.log').write_bytes(b'synthetic timeout diagnostics')
        with patch.object(self.p, 'run', return_value=0) as run, \
             patch.object(self.trial, 'observe_first_termination') as observe, \
             patch.object(self.trial, 'collect', wraps=self.trial.collect) as collect:
            self.assertEqual(0, collect())
            self.assertEqual([['bash', 'infra/scripts/lifecycle/lifecycle.sh', 'collect-job', record['job']]
                              for record in records[1:]], [call.args[0] for call in run.call_args_list])
            observe.assert_not_called()
            collect.assert_called_once_with()
        self.assertEqual(b'synthetic timeout diagnostics',
            (self.trial.directory / records[1]['job'] / 'synthetic.log').read_bytes())
        self.assertFalse((self.trial.directory / records[0]['job']).exists())

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
        record = {'job': self.run_id + '-c1-r1-loadgen-restart', 'mode': 'loadgen-restart'}
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
        for mode in ('loadgen-restart', 'loadgen'):
            record = {'mode': mode, 'job': self.run_id + '-c1-r1-' + mode, 'apply': 0}
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
        record = {'job': self.run_id + '-c1-r1-loadgen-restart', 'mode': 'loadgen-restart'}
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
        record = {'job': self.run_id + '-c1-r1-loadgen-restart', 'mode': 'loadgen-restart', 'outcome': 'failed'}
        with patch.object(self.p, 'b10_deadline', return_value=time.monotonic() + 18000), \
             patch.object(self.p, 'run_mode', return_value={'outcome': 'complete'}), \
             patch.object(self.trial, 'metadata', return_value=None), \
             patch.object(self.trial, 'job', side_effect=lambda mode, _deadline: self.timeout_record() if mode == 'loadgen' else record), \
             patch.object(self.trial, '_observe_timeout_termination', return_value=None), \
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

    def test_render_hook_selects_only_abrupt_modes(self):
        self.diagnostic()
        with patch.dict(support.DEADLINES, {'loadgen-restart': 1140, 'loadgen-successor': 1140}), \
             patch.object(self.p, 'run', side_effect=lambda *args, **kwargs: self.fake(self.p, *args, **kwargs)), \
             patch.object(self.p, 'b10_abrupt', return_value=self.trial), \
             patch.object(self.trial, 'values', side_effect=lambda mode, values: values) as hook:
            for mode in ('loadgen-restart', 'loadgen-successor', 'loadgen'):
                hook.reset_mock()
                self.assertEqual(1140, self.p.render(mode, 1))
                if mode == 'loadgen':
                    hook.assert_not_called()
                else:
                    hook.assert_called_once()
                    self.assertEqual(mode, hook.call_args.args[0])


class VerifyTrialCases(Setup, unittest.TestCase):
    """The diagnostic consumer follows accepted publication even after producer failure."""

    def reset_trial(self):
        if not hasattr(self, 'trial'):
            self.diagnostic()
        state = self.p.state()
        state.pop('b10_abrupt_started', None)
        state.update(modes=[], safety_stop=False, b10_abrupt_gaps=[], b10_abrupt_diagnostics=[])
        self.p.save_state(state)

    def run_case(self, successor=None, wait=0, apply=0, render=750, rejected=False):
        self.reset_trial()
        self.p.render_code = 0 if render is not None else 1
        deadline = time.monotonic() + 18000
        calls = []
        original_job = self.trial.job
        def job(mode, bound):
            calls.append(mode)
            return original_job(mode, bound) if mode == 'b10-abrupt-verify' else {'outcome': 'complete'}
        def release(_binding, _deadline):
            calls.append('loadgen-successor')
            if isinstance(successor, Exception):
                raise successor
            if successor == 'unknown':
                state = self.p.state(); state['safety_stop'] = True; self.p.save_state(state)
            return {'outcome': successor or 'complete', 'wait': 1 if successor == 'failed' else 2}
        def command(args, _seconds, **_kwargs):
            return apply if args[:2] == ['kubectl', 'apply'] else wait
        with patch.object(self.p, 'b10_deadline', return_value=deadline), \
             patch.object(self.p, 'run_mode', return_value={'outcome': 'complete'}), \
             patch.object(self.trial, 'metadata'), patch.object(self.trial, 'job', side_effect=job), \
             patch.object(self.trial, '_observe_timeout_termination'), \
             patch.object(self.trial, 'observe_first_termination', return_value={}), \
             patch.object(self.trial, 'download', return_value={'abrupt-manifest.json': b'ignored'}), \
             patch.object(self.trial, 'envelope', return_value={'data': {'report_files': []}}), \
             patch.object(self.trial, 'accept_diagnostic_publication',
                          side_effect=ValueError('b10_abrupt_publication_failed') if rejected else None,
                          return_value={'accepted': True}), \
             patch.object(self.trial, 'release_successor', side_effect=release), \
             patch.object(self.trial, 'render_verify', return_value=render), \
             patch.object(self.p, 'render', return_value=render), \
             patch.object(self.p, 'run', side_effect=command):
            result = self.trial.run()
        self.assertIsNone(self.p.command_deadline)
        return result, calls

    def test_verify_render_uses_real_helm_and_removes_producer_flag(self):
        self.reset_trial()
        document = self.p.read('phase.json')
        document['run_id'] = 'b10d-1-1'
        phase.write_json(self.private / 'phase.json', document)
        self.assertEqual(750, self.trial.render_verify())
        name = 'b10d-1-1-c1-r1-b10-abrupt-verify'
        values = self.p.read('values-' + name + '.json')
        self.assertIsNone(values['runtime']['b10_abrupt_diagnostic'])
        self.assertNotIn('b10_abrupt_binding_configmap', values['runtime'])
        rendered = (self.private / ('rendered-' + name + '.yaml')).read_text()
        self.assertIn('  name: "' + name + '"', rendered)
        self.assertIn('  activeDeadlineSeconds: 750', rendered)
        self.assertNotIn('b10-abrupt-binding', rendered)

    def test_successor_failure_unknown_or_raise_still_applies_verify(self):
        for outcome, expected in [('failed', 'b10_abrupt_publication_ambiguous'),
                                  ('unknown', 'b10_abrupt_publication_ambiguous'),
                                  (ValueError('b10_abrupt_successor_late'), 'b10_abrupt_successor_late')]:
            with self.subTest(outcome=outcome):
                result, calls = self.run_case(successor=outcome)
                self.assertEqual(1, result)
                self.assertEqual(['loadgen', 'loadgen-restart', 'loadgen-successor', 'b10-abrupt-verify'], calls)
                self.assertEqual([expected], self.p.state()['b10_abrupt_gaps'])
                self.assertEqual('complete', self.p.state()['modes'][-1]['outcome'])
                self.assertEqual(0, self.p.state()['modes'][-1]['apply'])
                self.assertEqual(outcome == 'unknown', self.p.state()['safety_stop'])

    def test_rejected_first_publication_never_renders_verify(self):
        result, calls = self.run_case(rejected=True)
        self.assertEqual(1, result)
        self.assertEqual(['loadgen', 'loadgen-restart'], calls)
        self.assertEqual([], self.p.state()['modes'])

    def test_verify_outcomes_preserve_safety_stop_and_record_one_gap(self):
        for options, outcome in [({}, 'complete'), ({'wait': 1}, 'failed'), ({'wait': 2}, 'unknown'),
                                 ({'apply': 1}, 'apply_failed'), ({'render': None}, 'render_failed')]:
            with self.subTest(outcome=outcome):
                result, calls = self.run_case(**options)
                self.assertEqual(int(outcome != 'complete'), result)
                self.assertEqual('b10-abrupt-verify', calls[-1])
                self.assertEqual(outcome, self.p.state()['modes'][-1]['outcome'])
                self.assertFalse(self.p.state()['safety_stop'])
                self.assertEqual([] if outcome == 'complete' else ['b10_abrupt_verify_failed'],
                                 self.p.state().get('b10_abrupt_gaps', []))
                if outcome != 'complete':
                    self.assertEqual('verify_job', self.p.state()['b10_abrupt_diagnostics'][0]['boundary'])
        result, _ = self.run_case(successor='unknown', wait=2)
        self.assertEqual(1, result)
        self.assertTrue(self.p.state()['safety_stop'])
        self.assertEqual(['b10_abrupt_publication_ambiguous', 'b10_abrupt_verify_failed'],
                         self.p.state()['b10_abrupt_gaps'])

    def test_complete_verify_collects_raw_output_once_and_always_collects_logs(self):
        for outcome, content in [('complete', b'not JSON: diagnostic transport only'),
                                 ('complete', None), ('failed', None), ('unknown', None)]:
            with self.subTest(outcome=outcome, content=content):
                self.reset_trial()
                name = self.run_id + '-c1-r1-b10-abrupt-verify'
                state = self.p.state()
                state['modes'] = [{'mode': 'b10-abrupt-verify', 'job': name, 'apply': 0, 'outcome': outcome}]
                self.p.save_state(state)
                source = self.p.artifacts / name; source.mkdir(exist_ok=True)
                (source / 'synthetic.log').write_bytes(b'diagnostic logs')
                deadline = time.monotonic() + 60
                def download(_name, target, _maximum, _deadline, **_kwargs):
                    if content is not None:
                        target.write_bytes(content)
                    return content
                with patch.object(self.p, 'download_blob', side_effect=download) as fetch, \
                     patch.object(self.p, 'b10_deadline', return_value=deadline), \
                     patch.object(self.p, 'run', return_value=0) as command:
                    self.assertEqual(int(outcome == 'complete' and content is None), self.trial.collect())
                command.assert_called_once_with(['bash', 'infra/scripts/lifecycle/lifecycle.sh', 'collect-job', name], 210)
                self.assertEqual(b'diagnostic logs', (self.trial.directory / name / 'synthetic.log').read_bytes())
                if outcome == 'complete':
                    fetch.assert_called_once_with(self.run_id + '/C1/r1/b10-abrupt-diagnostic/nonfinal-result.json',
                        self.trial.directory / 'nonfinal-result.json', 16777216, deadline, container='verifier')
                    if content is None:
                        self.assertEqual(['b10_abrupt_verify_failed'], self.p.state()['b10_abrupt_gaps'])
                        self.assertEqual('verify_collection', self.p.state()['b10_abrupt_diagnostics'][0]['boundary'])
                    else:
                        self.assertEqual(content, (self.trial.directory / 'nonfinal-result.json').read_bytes())
                else:
                    fetch.assert_not_called()


class WarmupJournalCases(Setup, unittest.TestCase):
    """Synthetic journal vectors exercise the phase gate independently of Kafka."""

    def setUp(self):
        super().setUp()
        self.diagnostic()
        document = self.p.read('phase.json')
        document['run_id'] = 'b10d-1-1'
        phase.write_json(self.private / 'phase.json', document)
        phase.write_json(self.private / 'b10-abrupt-start-line-binding.json', {'synthetic': True})
        self.observation = dict(job_name='b10d-1-1-c1-r1-loadgen-restart', pod_name='first', pod_uid='uid',
                                source_commit=document['source_commit'], image_reference=document['images']['loadgen']['reference'])
        self.identity = dict(run_id='b10d-1-1', scenario_id='B10', configuration_id='C1', phase='C1', repeat=1,
                             instance_id='first', predecessor_instance_id=None, **self.observation)
        self.frames = []
        self.report_bytes = abrupt.encode({'topics': {'document-events': {'partitions': {'0': {'leader': 1}}}},
                                          'brokers': {'one': {'nodeid': 1, 'state': 'UP'}}})
        self.append('child_started', dict(pid=1, start_time_ticks=1, boot_id='boot'))
        self.arm('same_instance', 0)
        self.append('cut', dict(arm='same_instance', cut_attempt_id='same_instance-4', release_utc=self.utc(120),
                               unconfirmed_attempt_ids=['same_instance-' + str(n) for n in range(1, 5)]))
        for n in range(1, 5):
            self.original('same_instance', n, 120, resend=True)
        for n in range(1, 5):
            self.terminal('same_instance-' + str(n), 121)
        self.append('control_drained', dict(flush_returned=True, out_queue_length=0), 121)
        self.report('same_instance', 122, post=True)
        self.control_end = len(self.frames)
        self.arm('cross_restart', 123)
        self.append('kill_intent', dict(arm='cross_restart', cut_attempt_id='cross_restart-4', release_utc=self.utc(243)), 123)

    @staticmethod
    def utc(seconds=0, tick=0):
        value = datetime(2030, 1, 1, tzinfo=timezone.utc) + timedelta(seconds=seconds)
        return value.strftime('%Y-%m-%dT%H:%M:%S.') + str(tick).zfill(7) + '+00:00'

    def append(self, kind, data, seconds=0):
        frame = dict(schema_version=3, dialect='b10_abrupt', purpose='diagnostic_trial', synthetic=True,
                     scientific_admissible=False, identity=self.identity, kind=kind, data=data,
                     sequence=len(self.frames) + 1, observed_utc=self.utc(seconds), monotonic_ticks=1, clock_frequency=1)
        self.frames.append(frame)
        return frame

    def arm(self, arm, seconds):
        self.append('warmup_prepared', dict(attempt_id='first-' + arm + '-warmup', arm=arm,
                    key='B10/b10d-1-1/C1/1/' + arm, payload_length=1, payload_sha256='a' * 64), seconds)
        self.append('warmup_terminal', dict(attempt_id='first-' + arm + '-warmup', completion_source='callback',
                    delivered=True, error_code=0, persistence_status=2, partition=0, offset=0), seconds)
        for n in range(5):
            self.original(arm, n, seconds)
            if n == 0 and arm == 'same_instance':
                self.terminal(arm + '-0', seconds)

    def original(self, arm, ordinal, seconds, resend=False):
        attempt = arm + '-' + str(ordinal) + ('-resend' if resend else '')
        prepared = self.append('prepared', dict(attempt_id=attempt, arm=arm, role='resend' if resend else 'original',
            ordinal=ordinal, probe_id='B10/b10d-1-1/C1/1/' + arm, key='B10/b10d-1-1/C1/1/' + arm,
            sequence=ordinal, payload_length=1, payload_sha256='a' * 64,
            predecessor_attempt_id=arm + '-' + str(ordinal) if resend else None), seconds)
        if not resend and ordinal == 0:
            self.report(arm, seconds)
        self.append('accepted', dict(attempt_id=attempt, prepared_sequence=prepared['sequence']), seconds)
        if resend:
            self.terminal(attempt, seconds)

    def terminal(self, attempt, seconds):
        self.append('terminal', dict(attempt_id=attempt, completion_source='callback', delivered=True,
                                    error_code=0, persistence_status=2), seconds)

    def report(self, arm, seconds, post=False):
        name = 'control-post-drain-report.json' if post else arm + '-pre-acceptance-report.json'
        data = dict(arm=arm, attempt_id=None if post else arm + '-0', **abrupt.descriptor(name, self.report_bytes),
                    report_identity=dict(scenario_id='B10', run_id='b10d-1-1', role='LoadGenProducer', client_id='client'),
                    report_sequence=2 if post else 1 if arm == 'same_instance' else 3,
                    report_capture_utc=self.utc(seconds, tick=1))
        if not post:
            data.update(warmup_attempt_id='first-' + arm + '-warmup', partition=0)
        self.append('post_drain_report' if post else 'pre_acceptance_report', data, seconds)

    @staticmethod
    def journal(frames):
        prepared = {}
        for sequence, frame in enumerate(frames, 1):
            frame['sequence'] = sequence
            if frame['kind'] == 'prepared':
                prepared[frame['data']['attempt_id']] = sequence
            elif frame['kind'] in ('accepted', 'synchronous_rejection'):
                frame['data']['prepared_sequence'] = prepared[frame['data']['attempt_id']]
        return b''.join(abrupt.encode(frame) + b'\n' for frame in frames)

    def publication(self, frames, reports=None):
        """Keep every other binding valid so the changed journal or report is decisive."""
        def envelope(kind, data):
            return abrupt.encode({key: value for key, value in dict(self.frames[0], kind=kind, data=data).items()
                                  if key in abrupt.ENVELOPE_FIELDS})
        reports = reports or {name: self.report_bytes for name in abrupt.REPORT_FILES}
        files = dict(reports, **{'journal.jsonl': self.journal(frames)})
        report_files = []
        for frame in frames:
            if frame['kind'] in ('pre_acceptance_report', 'post_drain_report'):
                descriptor = abrupt.descriptor(frame['data']['name'], files[frame['data']['name']])
                frame['data'].update(descriptor)
                report_files.append(descriptor)
        files['journal.jsonl'] = self.journal(frames)
        files['registration.json'] = envelope('registration', dict(registered_utc=self.utc()))
        files['abrupt-manifest.json'] = envelope('abrupt_manifest', dict(
            journal=abrupt.descriptor('journal.jsonl', files['journal.jsonl']), journal_records=len(frames), report_files=report_files, gaps=[]))
        cgroup = dict(boot_id='boot', cgroup_path='/synthetic', directory_identity='directory', oom_kill=0)
        files['termination.json'] = envelope('termination', dict(termination='intended_self_kill', outcome=None, detail=None,
            signal_observation=None, successor_permitted=True, child=dict(pid=1, start_time_ticks=1, boot_id='boot', parent_observed_start_time_ticks=1),
            exit_observed=True, exit_code_local_observation=137, runtime_version='synthetic', wait_bound_milliseconds=720000,
            cgroup_before=cgroup, cgroup_after=cgroup, control_unconfirmed_attempt_ids=['same_instance-' + str(n) for n in range(1, 5)],
            kill_unconfirmed_attempt_ids=['cross_restart-' + str(n) for n in range(5)], capture_gaps=[], native_error_count=0,
            native_fatal_count=0, native_tail='unknown_due_to_intended_kill', manifest=abrupt.descriptor('abrupt-manifest.json', files['abrupt-manifest.json'])))
        return files

    def test_valid_warmups_leave_unconfirmed_sets_unchanged(self):
        self.trial.accept_diagnostic_publication(self.observation, self.publication(copy.deepcopy(self.frames)))
        def unconfirmed(frames, arm):
            accepted = {frame['data']['attempt_id'] for frame in frames if frame['kind'] == 'accepted'}
            terminal = {frame['data']['attempt_id'] for frame in frames if frame['kind'] in ('terminal', 'synchronous_rejection')}
            return [frame['data']['attempt_id'] for frame in frames if frame['kind'] == 'prepared'
                    and frame['data']['role'] == 'original' and frame['data']['arm'] == arm
                    and frame['data']['attempt_id'] in accepted - terminal]
        for arm, boundary in [('same_instance', next(i + 1 for i, frame in enumerate(self.frames) if frame['kind'] == 'cut')),
                              ('cross_restart', len(self.frames))]:
            frames = self.frames[:boundary]
            without = [frame for frame in frames if not frame['kind'].startswith('warmup_')]
            self.assertEqual(unconfirmed(without, arm), unconfirmed(frames, arm))
            self.assertTrue(unconfirmed(frames, arm))

    def test_invalid_warmup_journals_reject_first_publication(self):
        def missing(frames):
            frames[:] = [frame for frame in frames if not (frame['kind'] == 'warmup_prepared' and frame['data']['arm'] == 'cross_restart')]
        def late(frames):
            terminal = next(frame for frame in frames if frame['kind'] == 'warmup_terminal')
            frames.remove(terminal)
            frames.insert(next(i + 1 for i, frame in enumerate(frames) if frame['kind'] == 'prepared'), terminal)
        def duplicate(frames):
            frames.insert(2, copy.deepcopy(frames[1]))
        def failed(frames):
            next(frame for frame in frames if frame['kind'] == 'warmup_terminal')['data'].update(delivered=False, error_code=-192)
        for label, edit in [('missing cross warmup', missing), ('late terminal', late),
                            ('duplicate control warmup', duplicate), ('failed warmup before ordinal zero', failed)]:
            frames = copy.deepcopy(self.frames)
            edit(frames)
            with self.subTest(label=label), self.assertRaisesRegex(ValueError, '^journal malformed_frame$'):
                self.trial.accept_diagnostic_publication(self.observation, self.publication(frames))

    def test_report_warmup_binding_and_strict_capture_time(self):
        for field, value in [('warmup_attempt_id', 'other'), ('partition', 1), ('report_capture_utc', self.utc())]:
            frames = copy.deepcopy(self.frames)
            next(frame for frame in frames if frame['kind'] == 'pre_acceptance_report')['data'][field] = value
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, '^b10_abrupt_report_binding_invalid$'):
                self.trial.accept_diagnostic_publication(self.observation, self.publication(frames))
        # The valid fixture binds exactly one 100 ns tick after the warm-up terminal.
        self.trial.accept_diagnostic_publication(self.observation, self.publication(copy.deepcopy(self.frames)))

    def test_warmup_always_rules(self):
        def change(frames, index, **data):
            frames[index]['data'].update(data)
        def terminal_first(frames):
            frames[1], frames[2] = frames[2], frames[1]
        def early_cross(frames):
            warmup = frames.pop(self.control_end)
            frames.insert(3, warmup)
        for label, edit in [
                ('attempt ID', lambda frames: change(frames, 1, attempt_id='other')),
                ('arm key', lambda frames: change(frames, 1, key='other')),
                ('terminal attempt ID', lambda frames: change(frames, 2, attempt_id='other')),
                ('terminal before prepared', terminal_first),
                ('duplicate terminal', lambda frames: frames.insert(3, copy.deepcopy(frames[2]))),
                ('cross warmup before control complete', early_cross),
                ('extra warmup member', lambda frames: change(frames, 1, extra=True)),
                ('missing warmup member', lambda frames: frames[2]['data'].pop('offset'))]:
            frames = copy.deepcopy(self.frames)
            edit(frames)
            with self.subTest(label=label), self.assertRaisesRegex(ValueError, '^journal malformed_frame$'):
                self.trial.accept_diagnostic_publication(self.observation, self.publication(frames))

    def test_no_kill_warmup_journals_are_valid(self):
        control = copy.deepcopy(self.frames[:self.control_end])
        cases = []
        failed = copy.deepcopy(self.frames[:3])
        failed[-1]['data'].update(delivered=False, error_code=-192, persistence_status=0, partition=-1, offset=-1)
        cases.append(('failed control', failed, 'b10_abrupt_warmup_failed', False))
        cases.append(('missing terminal', copy.deepcopy(self.frames[:2]), 'b10_abrupt_warmup_failed', False))
        cases.append(('late terminal', copy.deepcopy(self.frames[:2]), 'b10_abrupt_warmup_failed', True))
        failed_cross = copy.deepcopy(self.frames[:self.control_end + 2])
        failed_cross[-1]['data'].update(delivered=False, error_code=-192)
        cases.append(('failed cross', failed_cross, 'b10_abrupt_warmup_failed', False))
        cases.append(('no cross warmup', control, 'b10_abrupt_deadline_exhausted', False))
        for reason in ('b10_abrupt_leader_not_up', 'b10_abrupt_sample_missing'):
            cases.append((reason, copy.deepcopy(self.frames[:3]), reason, False))
        for label, frames, reason, late in cases:
            no_kill = copy.deepcopy(self.frames[-1])
            no_kill.update(kind='no_kill', data=dict(reason=reason, cross_restart_admitted=False))
            frames.append(no_kill)
            if late:
                frames.append(copy.deepcopy(self.frames[2]))
            with self.subTest(label=label):
                self.assertEqual(len(frames), len(self.trial.read_journal(self.journal(frames), self.observation)))
                self.assertIn(reason, abrupt.FAILURE_NAMES)

    def test_warmup_partition_and_offset_ranges(self):
        for field, valid, invalid in [
                ('partition', [0, 2147483647], [-1, -2147483649, 2147483648, True, 0.0, '0']),
                ('offset', [0, 4294967296, 9223372036854775807], [-1, -9223372036854775809, 9223372036854775808, True, 0.0, '0'])]:
            for value in valid + invalid:
                frames = copy.deepcopy(self.frames)
                frames[2]['data'][field] = value
                if field == 'partition' and value in valid and type(value) is int:
                    next(frame for frame in frames if frame['kind'] == 'pre_acceptance_report')['data']['partition'] = value
                with self.subTest(field=field, value=value):
                    if type(value) is int and value in valid:
                        self.trial.read_journal(self.journal(frames), self.observation)
                    else:
                        with self.assertRaisesRegex(ValueError, '^journal malformed_frame$'):
                            self.trial.read_journal(self.journal(frames), self.observation)
        for partition, offset in [(-2147483648, -9223372036854775808), (2147483647, 9223372036854775807)]:
            frames = copy.deepcopy(self.frames[:3])
            frames[2]['data'].update(delivered=False, error_code=-192, partition=partition, offset=offset)
            no_kill = copy.deepcopy(self.frames[-1])
            no_kill.update(kind='no_kill', data=dict(reason='b10_abrupt_warmup_failed', cross_restart_admitted=False))
            frames.append(no_kill)
            self.trial.read_journal(self.journal(frames), self.observation)

    def test_leader_up_table(self):
        base = abrupt.decode(self.report_bytes)
        cases = [('UP', base, True)]
        for state in ('INIT', 'CONNECT', 'DOWN'):
            report = copy.deepcopy(base)
            report['brokers']['one']['state'] = state
            cases.append((state, report, False))
        for leader in (-1, '1', 1.0, True):
            report = copy.deepcopy(base)
            report['topics']['document-events']['partitions']['0']['leader'] = leader
            cases.append(('leader ' + repr(leader), report, False))
        for label, edit in [('partition missing', lambda r: r['topics']['document-events']['partitions'].clear()),
                            ('topic missing', lambda r: r['topics'].clear()),
                            ('leader missing', lambda r: r['topics']['document-events']['partitions']['0'].clear()),
                            ('nodeid missing', lambda r: r['brokers']['one'].pop('nodeid')),
                            ('state missing', lambda r: r['brokers']['one'].pop('state')),
                            ('duplicate broker', lambda r: r['brokers'].update(two=dict(nodeid=1, state='UP'))),
                            ('wrong broker', lambda r: r['brokers']['one'].update(nodeid=2))]:
            report = copy.deepcopy(base)
            edit(report)
            cases.append((label, report, False))
        for label, report, expected in cases:
            with self.subTest(label=label):
                self.assertEqual(expected, abrupt.leader_up(abrupt.encode(report), 'document-events', 0))
        for raw in (b'null', b'[]', b'{', b'{}'):
            with self.subTest(raw=raw):
                self.assertFalse(abrupt.leader_up(raw, 'document-events', 0))

    def test_published_report_bytes_must_show_leader_up(self):
        for name in (abrupt.REPORT_FILES[0], abrupt.REPORT_FILES[2]):
            report = abrupt.decode(self.report_bytes)
            report['brokers']['one']['state'] = 'INIT'
            reports = {entry: self.report_bytes for entry in abrupt.REPORT_FILES}
            reports[name] = abrupt.encode(report)
            with self.subTest(name=name), self.assertRaisesRegex(ValueError, '^b10_abrupt_report_binding_invalid$'):
                self.trial.accept_diagnostic_publication(self.observation, self.publication(copy.deepcopy(self.frames), reports))

    def test_bad_leader_binding_blocks_successor_in_trial(self):
        reports = {name: self.report_bytes for name in abrupt.REPORT_FILES}
        report = abrupt.decode(self.report_bytes)
        report['brokers']['one']['state'] = 'INIT'
        reports[abrupt.REPORT_FILES[0]] = abrupt.encode(report)
        files = self.publication(copy.deepcopy(self.frames), reports)
        with patch.object(self.p, 'b10_deadline', return_value=time.monotonic() + 18000), \
             patch.object(self.p, 'run_mode', return_value={'outcome': 'complete'}), \
             patch.object(self.trial, 'metadata', return_value=None), \
             patch.object(self.trial, 'job', side_effect=lambda mode, _deadline:
                          self.timeout_record() if mode == 'loadgen' else {'outcome': 'complete'}), \
             patch.object(self.trial, '_observe_timeout_termination', return_value=None), \
             patch.object(self.trial, 'observe_first_termination', return_value=self.observation), \
             patch.object(self.trial, 'download', return_value=files), \
             patch.object(self.trial, 'release_successor') as release:
            self.assertEqual(1, self.trial.run())
            release.assert_not_called()
        self.assertEqual(['b10_abrupt_report_binding_invalid'], self.p.state()['b10_abrupt_gaps'])
        self.assertEqual('first_publication', self.p.state()['b10_abrupt_diagnostics'][0]['boundary'])


class ProducedBytesCases(Setup):
    """Only the export checker creates a TestCase from this production-byte mixin."""
    def produced(self):
        self.diagnostic()
        self.first = {name: (self.export_directory / 'first' / name).read_bytes() for name in abrupt.FIRST_FILES}
        manifest = abrupt.decode(self.first['abrupt-manifest.json'])['data']
        self.first.update({entry['name']: (self.export_directory / 'first' / entry['name']).read_bytes()
                           for entry in manifest['report_files']})
        self.successor = {name: (self.export_directory / 'successor' / name).read_bytes() for name in abrupt.SUCCESSOR_FILES}
        identity = abrupt.decode(self.first['termination.json'])['identity']
        document = self.p.read('phase.json')
        document.update(run_id=identity['run_id'], source_commit=identity['source_commit'])
        document['images']['loadgen']['reference'] = identity['image_reference']
        phase.write_json(self.private / 'phase.json', document)
        self.observation = {key: identity[key] for key in ('job_name', 'pod_name', 'pod_uid', 'source_commit', 'image_reference')}
        self.observation['finished_utc'] = '2030-01-01T00:00:00.0000000+00:00'
        self.record = {'job': identity['job_name'], 'mode': 'loadgen-restart'}
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
        self.trial.utc_ticks(binding['runner_utc'])
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
        cases = [('termination.json', lambda value: value['data'].update(successor_permitted=False)),
                 ('termination.json', lambda value: value['identity'].update(source_commit='f' * 40)),
                 ('termination.json', lambda value: value.update(schema_version=2)),
                 ('termination.json', lambda value: value.update(purpose='scientific')),
                 ('termination.json', lambda value: value['data']['kill_unconfirmed_attempt_ids'].pop()),
                 ('abrupt-manifest.json', lambda value: value['data']['journal'].update(sha256='0' * 64))]
        for name, mutate in cases:
            files = dict(self.first)
            value = abrupt.decode(files[name])
            mutate(value)
            files[name] = abrupt.encode(value)
            if name == 'abrupt-manifest.json':
                receipt = abrupt.decode(files['termination.json'])
                receipt['data']['manifest'] = abrupt.descriptor(name, files[name])
                files['termination.json'] = abrupt.encode(receipt)
            with self.subTest(name=name), self.assertRaises(ValueError):
                self.trial.accept_diagnostic_publication(self.observation, files)
        missing = dict(self.first)
        del missing['termination.json']
        torn = dict(self.first)
        torn['journal.jsonl'] = torn['journal.jsonl'][:-1]
        self.rebind_first_publication(torn)
        for files in (missing, torn):
            with self.assertRaises(ValueError):
                self.trial.accept_diagnostic_publication(self.observation, files)

    def rebound(self, edit):
        """Rebind the manifest and receipt so each edit reaches its target predicate."""
        files = dict(self.first)
        objects = {name: abrupt.decode(files[name]) for name in ('abrupt-manifest.json', 'termination.json')}
        lines = files['journal.jsonl'].splitlines()
        edit(objects, lines)
        files['journal.jsonl'] = b'\n'.join(lines) + b'\n'
        objects['abrupt-manifest.json']['data'].update(
            journal=abrupt.descriptor('journal.jsonl', files['journal.jsonl']), journal_records=len(lines))
        files['abrupt-manifest.json'] = abrupt.encode(objects['abrupt-manifest.json'])
        objects['termination.json']['data']['manifest'] = abrupt.descriptor('abrupt-manifest.json', files['abrupt-manifest.json'])
        files['termination.json'] = abrupt.encode(objects['termination.json'])
        return files

    @staticmethod
    def change_record(lines, kind, **changes):
        index = next(index for index, line in enumerate(lines) if abrupt.decode(line)['kind'] == kind)
        frame = abrupt.decode(lines[index])
        frame['data'].update(changes)
        lines[index] = abrupt.encode(frame)

    def change_record_bytes(self, files, kind, **changes):
        lines = files['journal.jsonl'].splitlines()
        self.change_record(lines, kind, **changes)
        files['journal.jsonl'] = b'\n'.join(lines) + b'\n'

    def terminal_publication(self, edit):
        """Edit terminal evidence and keep the receipt's kill set bound to the journal."""
        def change(_objects, lines):
            frames = [abrupt.decode(line) for line in lines]
            original = next(frame for frame in frames if frame['kind'] == 'prepared'
                            and frame['data']['arm'] == 'same_instance' and frame['data']['role'] == 'original'
                            and frame['data']['ordinal'] == 0)
            attempt = original['data']['attempt_id']
            terminal = next(frame for frame in frames if frame['kind'] == 'terminal' and frame['data']['attempt_id'] == attempt)
            accepted = next(frame for frame in frames if frame['kind'] == 'accepted' and frame['data']['attempt_id'] == attempt)
            self.assertNotIn(attempt, _objects['termination.json']['data']['control_unconfirmed_attempt_ids'])
            self.assertNotIn(attempt, _objects['termination.json']['data']['kill_unconfirmed_attempt_ids'])
            edit(frames, terminal, accepted)
            accepted_ids = {frame['data']['attempt_id'] for frame in frames if frame['kind'] == 'accepted'}
            completed_ids = {frame['data']['attempt_id'] for frame in frames
                             if frame['kind'] in ('terminal', 'synchronous_rejection')}
            _objects['termination.json']['data']['kill_unconfirmed_attempt_ids'] = [
                frame['data']['attempt_id'] for frame in frames if frame['kind'] == 'prepared'
                and frame['data']['arm'] == 'cross_restart' and frame['data']['role'] == 'original'
                and frame['data']['attempt_id'] in accepted_ids - completed_ids]
            prepared = {frame['data']['attempt_id']: sequence for sequence, frame in enumerate(frames, 1)
                        if frame['kind'] == 'prepared'}
            for sequence, frame in enumerate(frames, 1):
                frame['sequence'] = sequence
                if frame['kind'] in ('accepted', 'synchronous_rejection'):
                    frame['data']['prepared_sequence'] = prepared[frame['data']['attempt_id']]
            lines[:] = [abrupt.encode(frame) for frame in frames]
        return self.rebound(change)

    def test_produced_terminal_tuples_require_permitted_state(self):
        self.produced()
        # The first nine vectors retain the independent delivered input of the C# supervisor theory.
        cases = [(0, 2, True, True), (0, 0, True, True), (0, 1, True, False),
                 (-192, 0, False, True), (-192, 1, False, True), (-192, 1, True, False),
                 (-192, 2, False, False), (-195, 1, False, False), (0, None, True, False),
                 (0, 2, False, False), (0, -1, True, False), (0, 3, True, False)]
        for code, status, delivered, permitted in cases:
            files = self.terminal_publication(lambda _frames, terminal, _accepted:
                terminal['data'].update(error_code=code, persistence_status=status, delivered=delivered))
            with self.subTest(code=code, status=status, delivered=delivered):
                if permitted:
                    self.trial.accept_diagnostic_publication(self.observation, files)
                else:
                    with self.assertRaisesRegex(ValueError, '^b10_abrupt_guard_failed$'):
                        self.trial.accept_diagnostic_publication(self.observation, files)

    def test_produced_terminal_fields_require_strict_types(self):
        self.produced()
        cases = [('error_code', False, 'diagnostic data types'),
                 ('error_code', 0.0, 'diagnostic data types'),
                 ('error_code', '0', 'diagnostic data types'),
                 ('error_code', None, 'diagnostic data types'),
                 ('error_code', -2147483649, 'diagnostic data types'),
                 ('error_code', 2147483648, 'diagnostic data types'),
                 ('error_code', -2147483648, 'b10_abrupt_guard_failed'),
                 ('error_code', 2147483647, 'b10_abrupt_guard_failed'),
                 ('persistence_status', False, 'terminal binding'),
                 ('persistence_status', 0.0, 'terminal binding'),
                 ('persistence_status', '0', 'terminal binding'),
                 ('persistence_status', -2147483649, 'terminal binding'),
                 ('persistence_status', 2147483648, 'terminal binding'),
                 ('persistence_status', -2147483648, 'b10_abrupt_guard_failed'),
                 ('persistence_status', 2147483647, 'b10_abrupt_guard_failed'),
                 ('delivered', 0, 'diagnostic data types'),
                 ('delivered', 1, 'diagnostic data types'),
                 ('delivered', None, 'diagnostic data types'),
                 ('delivered', 'true', 'diagnostic data types'),
                 ('completion_source', '', 'diagnostic data types'),
                 ('completion_source', 'flush', 'terminal binding')]
        for field, value, error in cases:
            files = self.terminal_publication(lambda _frames, terminal, _accepted: terminal['data'].update({field: value}))
            with self.subTest(field=field, value=value), self.assertRaisesRegex(ValueError, '^' + error + '$'):
                self.trial.accept_diagnostic_publication(self.observation, files)

    def test_produced_terminal_relations_reconcile_eventual_acceptance(self):
        self.produced()
        def reorder(frames, terminal, accepted, before):
            frames.remove(terminal)
            frames.insert(frames.index(accepted) + (0 if before else 1), terminal)
        for before in (True, False):
            files = self.terminal_publication(lambda frames, terminal, accepted: reorder(frames, terminal, accepted, before))
            with self.subTest(before_acceptance=before):
                self.trial.accept_diagnostic_publication(self.observation, files)

        def reject(frames, terminal, accepted, before):
            accepted['kind'] = 'synchronous_rejection'
            accepted['data'].update(error_code=-184, error_reason='synthetic rejection')
            reorder(frames, terminal, accepted, not before)

        def before_prepared(frames, terminal, accepted):
            prepared = next(frame for frame in frames if frame['kind'] == 'prepared'
                            and frame['data']['attempt_id'] == accepted['data']['attempt_id'])
            frames.remove(terminal)
            frames.insert(frames.index(prepared), terminal)

        def unknown_prepared(frames, terminal, _accepted):
            frames.append(dict(frames[-1], kind='terminal',
                               data=dict(terminal['data'], attempt_id='synthetic-unprepared')))

        def missing_acceptance(frames, _terminal, _accepted):
            # Preserve the completed control arm required by the cross-restart warm-up.
            original = next(frame for frame in frames if frame['kind'] == 'prepared'
                            and frame['data']['arm'] == 'cross_restart' and frame['data']['role'] == 'original'
                            and frame['data']['ordinal'] == 0)
            frames.remove(next(frame for frame in frames if frame['kind'] == 'accepted'
                               and frame['data']['attempt_id'] == original['data']['attempt_id']))

        cases = [('unknown prepared attempt', unknown_prepared, 'terminal binding'),
                 ('terminal before prepared', before_prepared, 'terminal binding'),
                 ('duplicate terminal', lambda frames, terminal, _accepted:
                    frames.insert(frames.index(terminal) + 1, abrupt.decode(abrupt.encode(terminal))), 'terminal binding'),
                 ('rejection before terminal', lambda frames, terminal, accepted:
                    reject(frames, terminal, accepted, True), 'terminal binding'),
                 ('rejection after terminal', lambda frames, terminal, accepted:
                    reject(frames, terminal, accepted, False), 'terminal binding'),
                 ('missing acceptance', missing_acceptance, 'prepared acceptance binding')]
        for label, edit, error in cases:
            files = self.terminal_publication(edit)
            with self.subTest(label=label), self.assertRaisesRegex(ValueError, '^' + error + '$'):
                self.trial.accept_diagnostic_publication(self.observation, files)

        def rejected_without_callback(frames, terminal, accepted):
            reject(frames, terminal, accepted, True)
            frames.remove(terminal)
        self.trial.accept_diagnostic_publication(self.observation, self.terminal_publication(rejected_without_callback))

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

    def test_produced_report_files_accepted(self):
        self.produced()
        binding = self.trial.accept_diagnostic_publication(self.observation, self.first)
        self.assertEqual({'first_pod', 'termination', 'start_line_binding', 'release_utc', 'runner_utc',
                          'runner_monotonic_ticks', 'cross_host_timing'}, set(binding))
        abrupt.bound(binding['termination'], 'termination.json', self.first['termination.json'])
        manifest = abrupt.decode(self.first['abrupt-manifest.json'])['data']
        self.assertEqual(list(abrupt.REPORT_FILES), [entry['name'] for entry in manifest['report_files']])
        for entry in manifest['report_files']:
            abrupt.bound(entry, entry['name'], self.first[entry['name']])
        # Native writer records after kill_intent do not become producing-thread records.
        def edit(objects, lines):
            lines.extend(self.native_lines(lines, (1, False), (2, True)))
            objects['termination.json']['data'].update(native_error_count=2, native_fatal_count=1)
        self.trial.accept_diagnostic_publication(self.observation, self.rebound(edit))

    def test_produced_report_and_native_mutations_rejected(self):
        self.produced()
        def reports(change):
            return lambda objects, _lines: change(objects['abrupt-manifest.json']['data']['report_files'])
        def digit(entries):
            entries[0]['sha256'] = ('0' if entries[0]['sha256'][0] != '0' else '1') + entries[0]['sha256'][1:]
        def swap(entries):
            entries[0], entries[1] = entries[1], entries[0]
        def length(entries):
            entries[0]['length'] += 1
        def gap(objects, lines):
            lines.extend(self.native_lines(lines, (1, False), (3, False)))
            objects['termination.json']['data'].update(native_error_count=2, native_fatal_count=0)
        count = abrupt.decode(self.first['termination.json'])['data']['native_error_count']
        cases = {'missing report': reports(lambda entries: entries.pop()), 'swapped reports': reports(swap),
                 'length plus one': reports(length), 'sha256 digit': reports(digit),
                 'termination count': lambda objects, _lines: objects['termination.json']['data'].update(native_error_count=count + 1),
                 'sequence gap': gap}
        self.trial.accept_diagnostic_publication(self.observation, self.rebound(lambda objects, lines: None))
        for label, edit in cases.items():
            files = self.rebound(edit)
            with self.subTest(label=label), self.assertRaisesRegex(ValueError, '^b10_abrupt_publication_failed$'):
                self.trial.accept_diagnostic_publication(self.observation, files)
            state = self.p.state()
            state.pop('b10_abrupt_started', None)
            state.update(b10_abrupt_gaps=[], b10_abrupt_diagnostics=[])
            self.p.save_state(state)
            with self.subTest(label=label, recorded=True), \
                 patch.object(self.p, 'b10_deadline', return_value=time.monotonic() + 18000), \
                 patch.object(self.p, 'run_mode', return_value={'outcome': 'complete'}), \
                 patch.object(self.trial, 'metadata', return_value=None), \
                 patch.object(self.trial, 'job', side_effect=lambda mode, _deadline: self.timeout_record() if mode == 'loadgen' else {**self.record, 'outcome': 'complete'}), \
                 patch.object(self.trial, '_observe_timeout_termination', return_value=None), \
                 patch.object(self.trial, 'observe_first_termination', return_value=self.observation), \
                 patch.object(self.trial, 'download', return_value=files), \
                 patch.object(self.trial, 'release_successor') as release:
                self.assertEqual(1, self.trial.run())
                release.assert_not_called()
                self.assertEqual(['b10_abrupt_publication_failed'], self.p.state()['b10_abrupt_gaps'])
                self.assertEqual('first_publication', self.p.state()['b10_abrupt_diagnostics'][0]['boundary'])

    def test_produced_successor_collection(self):
        self.produced()
        binding = self.trial.accept_diagnostic_publication(self.observation, self.first)
        identity = abrupt.decode(self.successor['completion.json'])['identity']
        observation = {key: identity[key] for key in ('job_name', 'pod_name', 'pod_uid', 'source_commit', 'image_reference')}
        state = self.p.state()
        state.update(modes=[{'mode': 'loadgen-successor', 'job': identity['job_name'], 'apply': 0}])
        self.p.save_state(state)
        (self.trial.directory / 'predecessor-binding.json').write_bytes(abrupt.encode(binding))
        with patch.object(self.trial, 'observe_first_termination', return_value=observation), \
             patch.object(self.trial, 'download', return_value=self.successor) as download, \
             patch.object(self.p, 'b10_deadline', return_value=time.monotonic() + 60), \
             patch.object(self.p, 'run', return_value=0) as run:
            self.assertEqual(0, self.trial.collect())
            self.assertEqual(abrupt.SUCCESSOR_FILES, download.call_args.args[1])
            download.assert_called_once()
            self.assertEqual('collect-job', run.call_args.args[0][2])
        for name in abrupt.REPORT_FILES:
            self.assertEqual(self.first[name], (self.trial.directory / self.observation['pod_name'] / name).read_bytes())



    def test_produced_journal_descriptor_binds_exact_bytes(self):
        self.produced()
        for key, value in [('name', 'other.jsonl'), ('length', 0), ('sha256', '0' * 64)]:
            files = dict(self.first)
            manifest = abrupt.decode(files['abrupt-manifest.json'])
            manifest['data']['journal'][key] = value
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
        failed_control = self.rebound(lambda _objects, lines: self.change_record(lines, 'control_drained', flush_returned=False))
        cases.append(failed_control)
        for files in cases:
            state = self.p.state()
            state.pop('b10_abrupt_started', None)
            self.p.save_state(state)
            with self.subTest(files=sorted(files)), \
                 patch.object(self.p, 'b10_deadline', return_value=time.monotonic() + 18000), \
                 patch.object(self.p, 'run_mode', return_value={'outcome': 'complete'}), \
                 patch.object(self.trial, 'metadata', return_value=None), \
                 patch.object(self.trial, 'job', side_effect=lambda mode, _deadline: self.timeout_record() if mode == 'loadgen' else {**self.record, 'outcome': 'complete'}), \
                 patch.object(self.trial, '_observe_timeout_termination', return_value=None), \
                 patch.object(self.trial, 'observe_first_termination', return_value=self.observation), \
                 patch.object(self.trial, 'download', return_value=files), \
                 patch.object(self.trial, 'release_successor') as release:
                self.assertEqual(1, self.trial.run())
                release.assert_not_called()

    def rebind_first_publication(self, files):
        manifest = abrupt.decode(files['abrupt-manifest.json'])
        manifest['data']['journal'] = abrupt.descriptor('journal.jsonl', files['journal.jsonl'])
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
        self.change_record_bytes(files, 'kill_intent', release_utc=next(abrupt.decode(line)['observed_utc'] for line in files['journal.jsonl'].splitlines() if abrupt.decode(line)['kind'] == 'kill_intent'))
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
                 patch.object(self.trial, 'job', side_effect=lambda mode, _deadline: self.timeout_record() if mode == 'loadgen' else {**self.record, 'outcome': 'complete'}), \
                 patch.object(self.trial, '_observe_timeout_termination', return_value=None), \
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
            self.assertEqual(source_line("raise ValueError('" + detail + "')", function=function), diagnostic['site']['line'])

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
                 patch.object(self.trial, 'job', side_effect=lambda mode, _deadline: self.timeout_record() if mode == 'loadgen' else {**self.record, 'outcome': 'complete'}), \
                 patch.object(self.trial, '_observe_timeout_termination', return_value=None), \
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
            state.update(modes=[{'mode': 'loadgen-successor', 'job': identity['job_name'], 'apply': 0}],
                         b10_abrupt_gaps=[], b10_abrupt_diagnostics=[])
            self.p.save_state(state)
            def download(_observation, names, _deadline, files=files):
                return files
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

    def test_trial_runs_timeout_job_before_restart(self):
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
                if 'job-name=' + identity['run_id'] + '-c1-r1-loadgen' in args:
                    order.append('timeout-termination')
                    output.write_bytes(abrupt.encode(self.timeout_pod()))
                    return 0
                order.append('termination')
                output.write_bytes(abrupt.encode(payload))
                return 0
            if args[:2] == ['kubectl', 'apply']:
                order.append('b10-abrupt-verify' if 'b10-abrupt-verify' in args[3] else
                             'loadgen-successor' if 'loadgen-successor' in args[3] else
                             'loadgen-restart' if 'loadgen-restart' in args[3] else 'loadgen')
            if 'collect-job' in args:
                # Any slow collection before successor admission fails immediately.
                self.assertIn('loadgen-successor', order)
                order.append('slow-logs')
            return self.fake(self.p, args, seconds, output, maximum)
        def publication(observation, files):
            order.append('publication')
            return abrupt.Trial.accept_diagnostic_publication(self.trial, observation, files)
        with patch.dict(support.DEADLINES, {'loadgen-restart': 1140, 'loadgen-successor': 1140, 'b10-abrupt-verify': 750}), \
             patch.object(self.p, 'b10_deadline', return_value=time.monotonic() + 18000), \
             patch.object(self.p, 'run_mode', side_effect=lambda *args, **kwargs: order.append('metadata') or {'outcome': 'complete'}), \
             patch.object(self.trial, 'metadata', return_value=None), \
             patch.object(self.trial, 'accept_diagnostic_publication', side_effect=publication), \
             patch.object(abrupt.time, 'time', return_value=0), \
             patch.object(self.p, 'run', side_effect=command):
            self.assertEqual(0, self.trial.run())
        self.assertEqual(['metadata', 'loadgen', 'timeout-termination', 'loadgen-restart', 'termination', 'publication', 'loadgen-successor', 'b10-abrupt-verify'], order)
        self.assertEqual(['loadgen', 'loadgen-restart', 'loadgen-successor', 'b10-abrupt-verify'],
                         [event for event in order if event in ('loadgen', 'loadgen-restart', 'loadgen-successor', 'b10-abrupt-verify')])
        modes = [record['mode'] for record in self.p.state()['modes']]
        self.assertEqual(['loadgen', 'loadgen-restart', 'loadgen-successor', 'b10-abrupt-verify'], modes)
        self.assertFalse((self.candidate_dir / 'candidate.json').exists())
        self.assertFalse(any('collect-job' in call[0] for call in self.fake.calls))


    def test_produced_successor_invocation_payload_binding(self):
        self.produced()
        binding = self.trial.accept_diagnostic_publication(self.observation, self.first)
        (self.trial.directory / 'predecessor-binding.json').write_bytes(abrupt.encode(binding))
        receipt = abrupt.decode(self.successor['completion.json'])
        identity = receipt['identity']
        observation = {key: identity[key] for key in ('job_name', 'pod_name', 'pod_uid', 'source_commit', 'image_reference')}
        receipt['data']['invocations'][0]['payload_sha256'] = '0' * 64
        files = dict(self.successor, **{'completion.json': abrupt.encode(receipt)})
        with self.assertRaisesRegex(ValueError, '^b10_abrupt_successor_binding_invalid$'):
            self.trial.collect(files, observation)

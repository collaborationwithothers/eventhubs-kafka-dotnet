"""Synthetic transport regressions for the idle-observation phase module."""
import copy
import hashlib
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
JUDGE_JOB = RUN + '-idle-judge'


def candidate_document(item, **changes):
    document = {'schema_version': 1, 'mode': 'idle-observation-judge', 'synthetic': True, 'run_kind': 'batch', 'run_id': RUN,
                'scenario_id': item, 'source_commit': SHA, 'managed_version': None, 'native_version': None,
                'image_digests': {name: data['digest'] for name, data in IMAGES.items()}, 'configurations': [],
                'item_assessment': {'verdict': None, 'reason': 'incomplete_configuration', 'hypotheses': {}},
                'adr012_counts': 'not_applicable', 'notes': []}
    document.update(changes)
    return document


def encode(document):
    return json.dumps(document, separators=(',', ':')).encode()


def candidate_name(item):
    return f'{RUN}/idle-observation/{item}/judge/candidate.json'


def phase_document(items=('A02', 'A03', 'A04'), scenario='idle-observation', run_kind='batch'):
    return {'run_id': RUN, 'phase': 'C0', 'scenario': scenario, 'configuration': 'C0', 'run_kind': run_kind,
            'items': list(items), 'source_commit': SHA, 'images': IMAGES}


JUDGE_POD = {'items': [{'metadata': {'name': JUDGE_JOB + '-x7k2p', 'uid': 'uid-judge'}}]}


def good_pod(job, image=IMAGES['loadgen']['reference'], account='loadgen', code=0):
    return {'metadata': {'name': job + '-pod', 'uid': 'uid-' + job},
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

    def run_batch(self, boot=BOOT, monotonic_offset=None, fail=None, judge=True, blobs=None):
        calls = []

        def run_mode(mode, repeat, item=None, configuration=None, action_role=None, skip_reason=None, absolute_deadline=None):
            calls.append((mode, repeat, item, configuration, action_role, skip_reason))
            job = phase.job_name(RUN, 'C0', mode, repeat, item, configuration, action_role)
            if mode == 'idle-observation-judge':
                (self.artifacts / job).mkdir(exist_ok=True)
                (self.artifacts / job / 'pods.json').write_text(json.dumps(JUDGE_POD))
                return {'job': job, 'outcome': 'complete'}
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

        def download(name, target, maximum, deadline):
            data = blobs.get(name)
            if data is not None:
                target.write_bytes(data)
            return data

        stub = patch.object(self.idle, 'judge', lambda end: judge) if blobs is None else patch.object(self.p, 'download_blob', download)
        with patch.object(self.p, 'run_mode', run_mode), patch.object(self.p, 'boot_identity', lambda: boot), \
                patch.object(idle.time, 'monotonic', monotonic), stub:
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
        self.assertEqual(summary['candidates'], [])

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

    def test_run_batch_fails_when_the_judge_fails(self):
        code, calls = self.run_batch(judge=False)
        self.assertEqual(code, 1)
        self.assertIs(json.loads((self.artifacts / 'idle-observation-batch.json').read_text())['evidence_passed'], False)

    def test_capture_complete_stores_the_pod_name_and_uid(self):
        job = RUN + '-a02-c0-idle'
        self.p.save_state({'modes': [{'job': job}]})
        self.assertTrue(self.idle.capture_complete(self.record(job)))
        self.assertEqual(self.p.state()['modes'][0]['capture_pod'], {'name': job + '-pod', 'uid': 'uid-' + job})
        for bad in ('', 'a:b', 'a,b', 5):
            self.p.save_state({'modes': [{'job': job}]})
            pod = good_pod(job)
            pod['metadata']['uid'] = bad
            with self.subTest(uid=bad):
                self.assertFalse(self.idle.capture_complete(self.record(job, pods=[pod])))
                self.assertNotIn('capture_pod', self.p.state()['modes'][0])

    def capture_entries(self, configurations):
        return [{'job': phase.job_name(RUN, 'C0', 'idle-observation', None, item, configuration, 'loadgen'), 'mode': 'idle-observation',
                 'item': item, 'configuration': configuration, 'capture_complete': True,
                 'capture_pod': {'name': f'{RUN}-{item.lower()}-{configuration.lower()}-idle-x', 'uid': f'u-{item}{configuration}'}}
                for item, configuration in configurations]

    def test_judge_values_list_the_complete_captures_in_configuration_order(self):
        entries = self.capture_entries(idle.CONFIGURATIONS)
        entries[2]['capture_complete'] = False
        self.p.save_state({'modes': list(reversed(entries))})
        values = self.idle.judge_values()
        self.assertEqual((values['scenario_id'], values['configuration_id']), ('A04', 'C1'))
        runtime = values['runtime']
        self.assertEqual((runtime['mode'], runtime['phase'], runtime['run_kind']), ('idle-observation-judge', 'C1', 'batch'))
        expected = ','.join(f'{e["item"]}{e["configuration"]}:{e["capture_pod"]["name"]}:{e["capture_pod"]["uid"]}'
                            for e in entries if e['capture_complete'])
        self.assertEqual(runtime['capture_pods'], expected)
        self.assertEqual(runtime['judge_items'], 'A02,A03,A04')
        self.assertEqual(len(expected.split(',')), 6)
        self.assertNotIn('A03C0', expected)
        self.assertEqual(self.p.values('idle-observation-judge', None, None, None, 'verifier'), values)

    def test_judge_values_with_no_complete_capture(self):
        self.p.save_state({'modes': []})
        self.assertEqual(self.idle.judge_values()['runtime']['capture_pods'], 'none')
        self.write_phase(phase_document(items=['A03']))
        self.assertEqual(self.idle.judge_values()['configuration_id'], 'C1')
        self.assertEqual(self.idle.judge_values()['runtime']['judge_items'], 'A03')
        self.write_phase(phase_document(items=['A02', 'A04']))
        self.assertEqual(self.idle.judge_values()['runtime']['judge_items'], 'A02,A04')

    def judge_clock(self, remaining, items=('A02', 'A03', 'A04')):
        document = phase_document(items=items) | {'batch_started_monotonic': 1000.0, 'batch_clock_boot_id': BOOT, 'batch_clock_run_id': RUN,
                                       'batch_clock_source': 'CLOCK_MONOTONIC'}
        self.write_phase(document)
        return 1100.0 + remaining

    def run_judge(self, blobs, remaining=idle.JUDGE_RESERVATION, outcome='complete', items=('A02', 'A03', 'A04'), pods=None, reason=None):
        end = self.judge_clock(remaining, items)
        calls = []
        self.downloads = []
        if pods is not False:
            (self.artifacts / JUDGE_JOB).mkdir(exist_ok=True)
            (self.artifacts / JUDGE_JOB / 'pods.json').write_bytes(pods if isinstance(pods, bytes) else json.dumps(pods or JUDGE_POD).encode())

        def run_mode(mode, repeat, item=None, configuration=None, action_role=None, skip_reason=None, absolute_deadline=None):
            calls.append((mode, repeat, item, configuration, action_role, skip_reason, absolute_deadline))
            return {'job': JUDGE_JOB, 'outcome': outcome} | ({'reason': reason} if reason else {})

        def download(name, target, maximum, deadline):
            self.downloads.append(name)
            data = blobs.get(name)
            if data is not None:
                target.write_bytes(data)
            return data

        with patch.object(self.p, 'run_mode', run_mode), patch.object(self.p, 'boot_identity', lambda: BOOT), \
                patch.object(self.p, 'download_blob', download), patch.object(idle.time, 'monotonic', lambda: 1100.0):
            return self.idle.judge(end), calls

    def good_blobs(self, items=('A02', 'A03', 'A04'), results=None, **receipt_changes):
        blobs, references = {}, []
        results = {item: ('complete_valid', 'no_idle_close_observed') for item in items} | (results or {})
        for item in items:
            raw = encode(candidate_document(item))
            blobs[candidate_name(item)] = raw
            references.append({'scenario_id': item, 'candidate_name': candidate_name(item), 'candidate_sha256': hashlib.sha256(raw).hexdigest(),
                               'status': results[item][0], 'reason': results[item][1]})
        failing = any(results[item][0] != 'complete_valid' or results[item][1] == 'incomplete_configuration' for item in items)
        receipt = {'schema_version': 1, 'mode': 'idle-observation-judge', 'run_id': RUN, 'source_commit': SHA, 'job_name': JUDGE_JOB,
                   'pod_name': JUDGE_JOB + '-x7k2p', 'pod_uid': 'uid-judge', 'candidates': references, 'handler_exit_code': int(failing),
                   'cleanup_completed': True} | receipt_changes
        blobs[f'{RUN}/idle-observation/judge-completion.json'] = encode(receipt)
        return blobs

    def test_judge_starts_at_the_reservation_and_not_one_second_before(self):
        accepted, calls = self.run_judge(self.good_blobs(), remaining=idle.JUDGE_RESERVATION)
        self.assertTrue(accepted)
        self.assertEqual([(c[0], c[1], c[2], c[3], c[4], c[5]) for c in calls], [('idle-observation-judge', None, None, None, 'verifier', None)])
        self.assertEqual(calls[0][6], 1100.0 + idle.JUDGE_RESERVATION)
        self.setUp()
        accepted, calls = self.run_judge(self.good_blobs(), remaining=idle.JUDGE_RESERVATION - 1)
        self.assertFalse(accepted)
        self.assertEqual(calls, [])
        self.assertEqual(sorted(p.name for p in self.candidates.iterdir()), ['A02-judge-gap.json', 'A03-judge-gap.json', 'A04-judge-gap.json'])
        self.assertEqual(json.loads((self.candidates / 'A02-judge-gap.json').read_text()),
                         {'item': 'A02', 'configuration': 'judge', 'reason': 'start_cap'})
        self.assertEqual([c['accepted'] for c in self.p.state()['candidates']], [False] * 3)

    def test_judge_accepts_three_candidates_and_keeps_the_files_for_upload(self):
        accepted, _ = self.run_judge(self.good_blobs())
        self.assertTrue(accepted)
        self.assertEqual(sorted(p.name for p in self.candidates.iterdir()),
                         ['A02-idle-candidate.json', 'A03-idle-candidate.json', 'A04-idle-candidate.json', 'idle-judge-completion.json'])
        self.assertEqual(self.p.state()['candidates'], [
            {'item': item, 'configuration': 'judge', 'accepted': True, 'transport_valid': True, 'candidate': f'{item}-idle-candidate.json',
             'judge_completion': 'idle-judge-completion.json'} for item in ('A02', 'A03', 'A04')])

    def test_judge_follows_the_selected_items(self):
        accepted, _ = self.run_judge(self.good_blobs(items=('A03',)), items=('A03',))
        self.assertTrue(accepted)
        self.assertEqual([c['item'] for c in self.p.state()['candidates']], ['A03'])
        self.setUp()
        self.assertFalse(self.run_judge(self.good_blobs(items=('A02', 'A03', 'A04')), items=('A03',))[0])

    def gap_reasons(self):
        return {item: json.loads((self.candidates / f'{item}-judge-gap.json').read_text())['reason'] for item in ('A02', 'A03', 'A04')
                if (self.candidates / f'{item}-judge-gap.json').exists()}

    def test_judge_not_attempted_writes_three_gaps_and_downloads_nothing(self):
        accepted, calls = self.run_judge(self.good_blobs(), outcome='not_attempted')
        self.assertFalse(accepted)
        self.assertEqual(len(calls), 1)
        self.assertEqual(self.downloads, [])
        self.assertEqual(self.gap_reasons(), {'A02': 'not_attempted', 'A03': 'not_attempted', 'A04': 'not_attempted'})
        self.assertEqual([c['accepted'] for c in self.p.state()['candidates']], [False] * 3)
        self.setUp()
        self.assertFalse(self.run_judge(self.good_blobs(), outcome='not_attempted', reason='safety_stop')[0])
        self.assertEqual(set(self.gap_reasons().values()), {'safety_stop'})

    def test_judge_failed_and_unknown_still_download_and_check_the_evidence(self):
        for outcome in ('failed', 'unknown'):
            with self.subTest(outcome):
                self.setUp()
                accepted, calls = self.run_judge(self.good_blobs(), outcome=outcome)
                self.assertFalse(accepted)
                self.assertEqual(len(calls), 1)
                self.assertEqual(len(self.downloads), 4)
                self.assertEqual(self.gap_reasons(), {item: 'judge_' + outcome for item in ('A02', 'A03', 'A04')})
                self.assertEqual([(c['accepted'], c['transport_valid']) for c in self.p.state()['candidates']], [(False, True)] * 3)
                self.assertEqual(sorted(p.name for p in self.candidates.iterdir() if 'candidate' in p.name or 'completion' in p.name),
                                 ['A02-idle-candidate.json', 'A03-idle-candidate.json', 'A04-idle-candidate.json', 'idle-judge-completion.json'])

    def test_judge_failed_with_a_failing_receipt_keeps_the_candidates(self):
        for results in ({'A03': ('invalid', 'incomplete_configuration')}, {'A03': ('incomplete', 'smoke_only')},
                        {'A04': ('complete_valid', 'incomplete_configuration')}):
            with self.subTest(results=results):
                self.setUp()
                blobs = self.good_blobs(results=results)
                self.assertEqual(json.loads(blobs[f'{RUN}/idle-observation/judge-completion.json'])['handler_exit_code'], 1)
                accepted, _ = self.run_judge(blobs, outcome='failed')
                self.assertFalse(accepted)
                self.assertEqual(len(self.downloads), 4)
                self.assertEqual([(c['accepted'], c['transport_valid']) for c in self.p.state()['candidates']], [(False, True)] * 3)
                self.assertEqual(self.gap_reasons()['A02'], 'judge_failed')

    def test_judge_complete_with_exit_one_is_transport_valid_but_not_accepted(self):
        accepted, _ = self.run_judge(self.good_blobs(results={'A03': ('invalid', 'incomplete_configuration')}))
        self.assertFalse(accepted)
        self.assertEqual([(c['accepted'], c['transport_valid']) for c in self.p.state()['candidates']], [(False, True)] * 3)
        self.assertEqual(self.gap_reasons(), {'A02': 'judge_exit_1', 'A03': 'judge_exit_1', 'A04': 'judge_exit_1'})

    def test_judge_gap_reason_prefers_candidate_transport_over_the_outcome_and_the_exit_code(self):
        invalid = {'a pod uid that is not the collected pod': {'pod_uid': 'uid-other'},
                   'a candidate hash that does not match': None}
        for outcome in ('failed', 'unknown', 'complete'):
            for name, change in invalid.items():
                with self.subTest(outcome=outcome, transport=name):
                    self.setUp()
                    if change is None:
                        blobs = self.good_blobs()
                        receipt = json.loads(blobs[f'{RUN}/idle-observation/judge-completion.json'])
                        receipt['candidates'][0]['candidate_sha256'] = '0' * 64
                        blobs[f'{RUN}/idle-observation/judge-completion.json'] = encode(receipt)
                        expected_valid = [False, True, True]
                    else:
                        blobs = self.good_blobs(**change)
                        expected_valid = [False] * 3
                    self.assertFalse(self.run_judge(blobs, outcome=outcome)[0])
                    self.assertEqual([c['transport_valid'] for c in self.p.state()['candidates']], expected_valid)
                    reasons = self.gap_reasons()
                    self.assertEqual(reasons['A02'], 'candidate_transport')
                    if change is not None:
                        self.assertEqual(reasons, {item: 'candidate_transport' for item in ('A02', 'A03', 'A04')})

    def test_judge_gap_reason_prefers_candidate_transport_over_exit_one_receipts(self):
        failing = {'A03': ('invalid', 'incomplete_configuration')}
        blobs = self.good_blobs(results=failing, pod_uid='uid-other')
        self.assertEqual(json.loads(blobs[f'{RUN}/idle-observation/judge-completion.json'])['handler_exit_code'], 1)
        self.assertFalse(self.run_judge(blobs, outcome='complete')[0])
        self.assertEqual([c['transport_valid'] for c in self.p.state()['candidates']], [False] * 3)
        self.assertEqual(self.gap_reasons(), {item: 'candidate_transport' for item in ('A02', 'A03', 'A04')})

    def test_judge_gap_reason_prefers_the_outcome_over_the_exit_code_when_transport_is_valid(self):
        failing = {'A03': ('invalid', 'incomplete_configuration')}
        for outcome in ('failed', 'unknown'):
            with self.subTest(outcome):
                self.setUp()
                blobs = self.good_blobs(results=failing)
                self.assertEqual(json.loads(blobs[f'{RUN}/idle-observation/judge-completion.json'])['handler_exit_code'], 1)
                self.assertFalse(self.run_judge(blobs, outcome=outcome)[0])
                self.assertEqual([c['transport_valid'] for c in self.p.state()['candidates']], [True] * 3)
                self.assertEqual(self.gap_reasons(), {item: 'judge_' + outcome for item in ('A02', 'A03', 'A04')})

    def test_judge_rejects_an_exit_code_the_entries_do_not_give(self):
        cases = {
            'exit 1, all complete_valid': self.good_blobs(handler_exit_code=1),
            'exit 0, invalid': self.good_blobs(results={'A02': ('invalid', 'incomplete_configuration')}, handler_exit_code=0),
            'exit 0, incomplete': self.good_blobs(results={'A03': ('incomplete', 'smoke_only')}, handler_exit_code=0),
            'exit 0, incomplete_configuration': self.good_blobs(results={'A04': ('complete_valid', 'incomplete_configuration')}, handler_exit_code=0),
            'exit 2': self.good_blobs(handler_exit_code=2),
            'exit true': self.good_blobs(handler_exit_code=True),
        }
        for name, blobs in cases.items():
            with self.subTest(name):
                self.setUp()
                self.assertFalse(self.run_judge(blobs)[0])
                self.assertEqual([(c['accepted'], c['transport_valid']) for c in self.p.state()['candidates']], [(False, False)] * 3)
                self.assertEqual(set(self.gap_reasons().values()), {'candidate_transport'})

    def test_judge_rejects_a_receipt_pod_that_is_not_the_collected_pod(self):
        accepted, _ = self.run_judge(self.good_blobs(pod_uid='uid-other'))
        self.assertFalse(accepted)
        self.assertEqual([(c['accepted'], c['transport_valid']) for c in self.p.state()['candidates']], [(False, False)] * 3)
        self.assertEqual(self.gap_reasons(), {item: 'candidate_transport' for item in ('A02', 'A03', 'A04')})
        collected = {
            'name differs': JUDGE_POD | {'items': [{'metadata': {'name': JUDGE_JOB + '-other', 'uid': 'uid-judge'}}]},
            'two pods': {'items': JUDGE_POD['items'] * 2},
            'no pods': {'items': []},
            'not a list': {'items': {}},
        }
        for name, pods in collected.items():
            with self.subTest(name):
                self.setUp()
                self.assertFalse(self.run_judge(self.good_blobs(), pods=pods)[0])
                self.assertEqual(set(self.gap_reasons().values()), {'candidate_transport'})
        document = json.dumps(JUDGE_POD).encode()
        at_limit = document + b' ' * (4194304 - len(document))
        one_above = document + b' ' * (4194305 - len(document))
        self.assertEqual((len(at_limit), len(one_above)), (4194304, 4194305))
        sizes = {'missing': False, 'empty': b'', 'one byte above the maximum': one_above}
        for name, pods in sizes.items():
            with self.subTest(name):
                self.setUp()
                self.assertFalse(self.run_judge(self.good_blobs(), pods=pods)[0])
                self.assertEqual([c['transport_valid'] for c in self.p.state()['candidates']], [False] * 3)
                self.assertEqual(set(self.gap_reasons().values()), {'candidate_transport'})
        self.setUp()
        self.assertTrue(self.run_judge(self.good_blobs(), pods=at_limit)[0])

    def test_judge_rejects_each_receipt_field_that_is_off_by_one(self):
        changes = {'schema_version': 2, 'mode': 'other', 'run_id': 'other', 'source_commit': 'b' * 40, 'job_name': 'other-idle-judge',
                   'handler_exit_code': 1, 'cleanup_completed': False, 'pod_name': 'Bad_Name', 'pod_uid': '', 'candidates': []}
        for key, value in changes.items():
            with self.subTest(key):
                self.setUp()
                accepted, _ = self.run_judge(self.good_blobs(**{key: value}))
                self.assertFalse(accepted)
                self.assertEqual([c['accepted'] for c in self.p.state()['candidates']], [False] * 3)
        for name, build in {
            'extra key': lambda b: b | {'extra': 1},
            'missing key': lambda b: {k: v for k, v in b.items() if k != 'pod_uid'},
        }.items():
            with self.subTest(name):
                self.setUp()
                blobs = self.good_blobs()
                key = f'{RUN}/idle-observation/judge-completion.json'
                blobs[key] = encode(build(json.loads(blobs[key])))
                self.assertFalse(self.run_judge(blobs)[0])

    def test_judge_rejects_receipt_candidate_references_that_do_not_match(self):
        def mutate(change):
            blobs = self.good_blobs()
            key = f'{RUN}/idle-observation/judge-completion.json'
            receipt = json.loads(blobs[key])
            change(receipt['candidates'])
            blobs[key] = encode(receipt)
            return blobs
        cases = {
            'name': lambda c: c[1].update(candidate_name='x'),
            'hash': lambda c: c[1].update(candidate_sha256='0' * 64),
            'item': lambda c: c[1].update(scenario_id='A02'),
            'extra key': lambda c: c[1].update(extra=1),
            'missing': lambda c: c.pop(),
            'no reason': lambda c: c[1].pop('reason'),
            'no status': lambda c: c[1].pop('status'),
            'status complete': lambda c: c[1].update(status='complete'),
            'status null': lambda c: c[1].update(status=None),
            'reason number': lambda c: c[1].update(reason=5),
        }
        for name, change in cases.items():
            with self.subTest(name):
                self.setUp()
                self.assertFalse(self.run_judge(mutate(change))[0])

    def test_judge_rejects_each_candidate_field_that_is_off_by_one_and_only_that_item(self):
        changes = {'schema_version': 2, 'mode': 'other', 'synthetic': False, 'run_kind': 'smoke', 'run_id': 'other',
                   'scenario_id': 'A02', 'source_commit': 'b' * 40, 'image_digests': {'loadgen': IMAGES['loadgen']['digest']}}
        for key, value in changes.items():
            with self.subTest(key):
                self.setUp()
                blobs = self.good_blobs()
                raw = encode(candidate_document('A03', **{key: value}))
                blobs[candidate_name('A03')] = raw
                receipt_key = f'{RUN}/idle-observation/judge-completion.json'
                receipt = json.loads(blobs[receipt_key])
                receipt['candidates'][1]['candidate_sha256'] = hashlib.sha256(raw).hexdigest()
                blobs[receipt_key] = encode(receipt)
                self.assertFalse(self.run_judge(blobs)[0])
                self.assertEqual([c['accepted'] for c in self.p.state()['candidates']], [True, False, True])
        self.setUp()
        blobs = self.good_blobs()
        document = candidate_document('A03')
        del document['notes']
        raw = encode(document)
        blobs[candidate_name('A03')] = raw
        receipt_key = f'{RUN}/idle-observation/judge-completion.json'
        receipt = json.loads(blobs[receipt_key])
        receipt['candidates'][1]['candidate_sha256'] = hashlib.sha256(raw).hexdigest()
        blobs[receipt_key] = encode(receipt)
        self.assertFalse(self.run_judge(blobs)[0])

    def test_judge_rejects_missing_and_malformed_transport(self):
        cases = {
            'no receipt': lambda b: b.pop(f'{RUN}/idle-observation/judge-completion.json'),
            'no candidate': lambda b: b.pop(candidate_name('A04')),
            'not json': lambda b: b.update({candidate_name('A02'): b'{'}),
            'duplicate key': lambda b: b.update({f'{RUN}/idle-observation/judge-completion.json': b'{"a":1,"a":2}'}),
            'not utf8': lambda b: b.update({candidate_name('A02'): b'\xff'}),
            'not an object': lambda b: b.update({f'{RUN}/idle-observation/judge-completion.json': b'[]'}),
        }
        for name, change in cases.items():
            with self.subTest(name):
                self.setUp()
                blobs = self.good_blobs()
                change(blobs)
                self.assertFalse(self.run_judge(blobs)[0])
                self.assertTrue((self.candidates / 'A02-judge-gap.json').exists() or (self.candidates / 'A04-judge-gap.json').exists())

    def test_run_batch_runs_the_judge_after_the_seven_captures(self):
        code, calls = self.run_batch(blobs=self.good_blobs())
        self.assertEqual(code, 0)
        self.assertEqual([c[0] for c in calls], ['idle-observation'] * 7 + ['idle-observation-judge'])
        self.assertEqual(calls[-1][1:5], (None, None, None, 'verifier'))
        summary = json.loads((self.artifacts / 'idle-observation-batch.json').read_text())
        self.assertEqual(summary['candidates'], ['A02', 'A03', 'A04'])
        self.assertIs(summary['evidence_passed'], True)

    def test_run_batch_does_not_pass_when_the_judge_exits_one(self):
        code, calls = self.run_batch(blobs=self.good_blobs(results={'A03': ('invalid', 'incomplete_configuration')}))
        self.assertEqual(code, 1)
        summary = json.loads((self.artifacts / 'idle-observation-batch.json').read_text())
        self.assertEqual(summary['candidates'], [])
        self.assertIs(summary['evidence_passed'], False)
        self.assertEqual([c['transport_valid'] for c in self.p.state()['candidates']], [True] * 3)

    def test_run_batch_does_not_pass_when_a_candidate_is_rejected(self):
        blobs = self.good_blobs()
        blobs[candidate_name('A02')] = b'{}'
        code, calls = self.run_batch(blobs=blobs)
        self.assertEqual(code, 1)
        summary = json.loads((self.artifacts / 'idle-observation-batch.json').read_text())
        self.assertEqual(summary['candidates'], ['A03', 'A04'])
        self.assertIs(summary['evidence_passed'], False)

    def test_admission_window_matches_phase(self):
        self.assertEqual(idle.ADMISSION_SECONDS, phase.BATCH_START_CAP)


if __name__ == '__main__':
    unittest.main()

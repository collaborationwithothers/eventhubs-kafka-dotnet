"""Offline tests for archiving the consumer scenarios C01, C03 and C10 (issue 531 plan, section 6)."""
import copy
import importlib.util
import json
from pathlib import Path
import unittest

HERE = Path(__file__).resolve().parent


def load(name, file):
    spec = importlib.util.spec_from_file_location(name, file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


# BlobCLI and Harness are imported unchanged, so the fixture shares the live blob.containers keys,
# the native-monitoring indexes and the pod collection files that main's own tests use.
integration = load('archive_consumer_integration', HERE / 'test_archive_inventory_integration.py')
pack = load('archive_consumer_pack', HERE.parent / 'archive_pack.py')
archive = integration.a
encoded = integration.encoded

DIGEST = 'sha256:' + 'b' * 64
LEAVES = ('ledger.jsonl', 'manifest.json', 'statistics.jsonl', 'request-lifecycle.jsonl', 'capabilities.json')
MEMBERS = ('consumer-subject', 'consumer-witness')


def readiness_record(run, phase, repeat, **fields):
    return {'mode': 'consumer-readiness', 'repeat': repeat, 'job': f'{run}-{phase.lower()}-r{repeat}-consumer-readiness',
            'outcome': 'ready', 'polls': 1, 'elapsed_seconds': 5, 'last_condition': 'ready', **fields}


class Phase:
    """One consumer phase built on the shared Harness, with every expected object present."""

    def __init__(self, scenario='C03', phase='C0', rows=40001, blobs=2, completion=True):
        self.run = f'{scenario.lower()}-42-1'
        self.scenario, self.phase = scenario, phase
        self.h = integration.Harness(scenario, phase, self.run, 'scientific')
        self.h.phase['images']['enrichment'] = {
            'digest': DIGEST, 'reference': f'archive123.azurecr.io/parchwell/enrichment@{DIGEST}'}
        self.pods = {}
        for repeat in range(1, 6):
            for mode in MEMBERS:
                job = self.job(repeat, mode)
                self.pods[(repeat, mode)] = self.h.job(mode, job, repeat=repeat)
                self.member(repeat, mode, rows=rows, blobs=blobs, completion=completion)
            self.h.status['modes'].append(readiness_record(self.run, phase, repeat))
            self.verifier(repeat, 'start-line', ('start-line.json', 'broker-inventory.json', 'start-line-completion.json', 'consumer-start.json'))
            self.loadgen(repeat)
            self.verifier(repeat, 'verify', ('result.json', 'verify-completion.json'))
        self.h.job('judge', f'{self.run}-{phase.lower()}-judge')
        for leaf in ('candidate.json', 'judge-completion.json'):
            self.h.cli.put('verifier', f'{self.run}/{phase}/{leaf}', {'leaf': leaf})

    def job(self, repeat, mode):
        return f'{self.run}-{self.phase.lower()}-r{repeat}-{mode}'

    def root(self, repeat, mode):
        return f'{self.run}/{self.phase}/r{repeat}/consumer/{self.pods[(repeat, mode)]}/'

    def identity(self, repeat, mode):
        return {'run_id': self.run, 'phase': self.phase, 'scenario_id': self.scenario, 'configuration_id': self.phase,
                'repeat_number': repeat, 'role': 'subject' if mode == 'consumer-subject' else 'witness',
                'instance_id': self.pods[(repeat, mode)], 'job_name': self.job(repeat, mode), 'pod_uid': 'uid',
                'source_commit': 'a' * 40}

    def member(self, repeat, mode, rows=40001, blobs=2, completion=True):
        root, identity, pod = self.root(repeat, mode), self.identity(repeat, mode), self.pods[(repeat, mode)]
        self.h.cli.put('consumer-record', root + 'registration.json',
                       {'schema_version': 1, 'kind': 'consumer_registration', **identity, 'pod_name': pod, 'node_name': 'node',
                        'group_id': 'group', 'effective_settings': {}, 'registered_utc': '2026-11-17T00:00:00Z'})
        if completion:
            self.h.cli.put('consumer-record', root + 'completion.json',
                           {'schema_version': 1, 'kind': 'consumer_completion', **identity, 'rows': rows, 'record_blobs': blobs,
                            'messages_processed': rows, 'closed_utc': '2026-11-17T00:00:01Z'})
        for index in range(blobs):
            self.h.cli.put('consumer-record', f'{root}record/{index:06d}.jsonl', b'{"offset":%d}\n' % index)

    def verifier(self, repeat, mode, leaves):
        self.h.job(mode, self.job(repeat, mode), repeat=repeat)
        for leaf in leaves:
            self.h.cli.put('verifier', f'{self.run}/{self.phase}/r{repeat}/{leaf}', {'leaf': leaf})

    def loadgen(self, repeat):
        job = self.job(repeat, 'loadgen')
        pod = self.h.job('loadgen', job, repeat=repeat)
        root = f'{self.run}/{self.phase}/r{repeat}/producer/{pod}/'
        identity = {'scenario_id': self.scenario, 'configuration_id': self.phase, 'run_id': self.run,
                    'instance_id': pod, 'expected_instance_id': pod, 'client_id': pod}
        self.h.cli.put('ledger', root + 'registration.json', {'identity': identity, 'phase': self.phase, 'repeat_number': repeat,
                       'job_name': job, 'pod_name': pod, 'pod_uid': 'uid', 'source_commit': 'a' * 40})
        self.h.cli.put('ledger', root + 'completion.json', b'complete')
        # An empty statistics file passes the archive's statistics scan; the other leaves are opaque.
        body = {leaf: b'' if leaf == 'statistics.jsonl' else b'abc' for leaf in LEAVES}
        artifacts = [{'name': leaf, 'length': len(body[leaf]), 'sha256': integration.sha(body[leaf])} for leaf in LEAVES]
        self.h.cli.put('ledger', root + 'publication/index.json', {
            'schema_version': 1, 'identity': identity, 'worker_status': 'complete', 'bookkeeping_complete': True,
            'worker_complete': True, 'publication_complete': True, 'artifacts': artifacts})
        for leaf in LEAVES:
            self.h.cli.put('ledger', root + 'publication/' + leaf, body[leaf])

    def blob_name(self, repeat, mode, index):
        return f'{self.root(repeat, mode)}record/{index:06d}.jsonl'

    def run_archive(self):
        return self.h.run()

    def close(self):
        self.h.close()


def gaps(manifest, boundary=None, code=None):
    return [g for g in manifest['gaps'] if (boundary is None or g['boundary'] == boundary) and (code is None or g['code'] == code)]


class ConsumerArchiveTests(unittest.TestCase):
    def phase(self, *args, **kwargs):
        fixture = Phase(*args, **kwargs)
        self.addCleanup(fixture.close)
        return fixture

    def test_consumer_manifest_images(self):
        # T1
        for scenario in ('C01', 'C03', 'C10'):
            for phase in ('C0', 'C1'):
                with self.subTest(scenario=scenario, phase=phase):
                    fixture = self.phase(scenario, phase)
                    code, manifest, receipt = fixture.run_archive()
                    self.assertEqual(0, code)
                    images = manifest['run']['images']
                    self.assertEqual({'loadgen', 'verifier', 'probe', 'enrichment'}, set(images))
                    self.assertIsNone(images['probe'])
                    self.assertEqual(f'archive123.azurecr.io/parchwell/enrichment@{DIGEST}', images['enrichment'])

    def test_image_key_sets_by_scenario(self):
        # T3
        fixture = self.phase()
        code, manifest, _ = fixture.run_archive()
        self.assertEqual(0, code)
        reference = f'archive123.azurecr.io/parchwell/%s@{DIGEST}'
        def variant(scenario, keys, enrichment=None):
            value = copy.deepcopy(manifest)
            value['run']['scenario_id'] = scenario
            value['run']['images'] = {key: (None if key == 'probe' else reference % key) for key in keys}
            if enrichment is not None:
                value['run']['images']['enrichment'] = enrichment
            return value
        four, three = ('loadgen', 'verifier', 'probe', 'enrichment'), ('loadgen', 'verifier', 'probe')
        wrong = f'x.azurecr.io/parchwell/loadgen@{DIGEST}'
        cases = [(variant('C03', four), True), (variant('C03', three), False), (variant('B04', four), False),
                 (variant('B04', three), True), (variant('C03', four, wrong), False)]
        for index, (value, accepted) in enumerate(cases):
            with self.subTest(case=index):
                if accepted:
                    archive.validate_manifest(value)
                else:
                    with self.assertRaises(archive.InvalidArchiveConfig):
                        archive.validate_manifest(value)
                self.assertEqual(accepted, pack._context(value['run']))

    def test_missing_enrichment_image_is_a_gap(self):
        # T3, last sentence
        fixture = self.phase()
        del fixture.h.phase['images']['enrichment']
        code, manifest, _ = fixture.run_archive()
        self.assertEqual(1, code)
        self.assertIsNone(manifest['run']['images']['enrichment'])
        self.assertEqual(1, len(gaps(manifest, 'image identity unavailable')))
        self.assertEqual(1, len(manifest['gaps']))

    def test_readiness_projection(self):
        # T4
        run, job = 'c03-42-1', 'c03-42-1-c0-r1-consumer-readiness'
        def project(**fields):
            record = {'mode': 'consumer-readiness', 'repeat': 1, 'job': job, 'outcome': 'ready',
                      'polls': 3, 'elapsed_seconds': 12, 'last_condition': 'ready', **fields}
            for key in [key for key, value in fields.items() if value is KeyError]:
                del record[key]
            return json.loads(archive.project_phase_status({'run_id': run, 'phase': 'C0', 'modes': [record]}))['modes'][0]
        nulls = {key: None for key in ('render', 'apply', 'wait', 'collect', 'delete', 'confirm')}
        accepted = [({}, 'ready', 3, 12, 'ready'),
                    ({'outcome': 'not_observed', 'polls': 60, 'elapsed_seconds': 300, 'last_condition': 'list_failed'}, 'not_observed', 60, 300, 'list_failed'),
                    ({'outcome': 'not_attempted', 'polls': 0, 'elapsed_seconds': 0, 'last_condition': None}, 'not_attempted', 0, 0, None)]
        for fields, outcome, polls, elapsed, condition in accepted:
            with self.subTest(outcome=outcome):
                self.assertEqual({'job': job, 'mode': 'consumer-readiness', 'repeat': 1, 'outcome': outcome, **nulls,
                                  'polls': polls, 'elapsed_seconds': elapsed, 'last_condition': condition}, project(**fields))
        rejected = [{'polls': -1}, {'polls': True}, {'elapsed_seconds': 1.5}, {'elapsed_seconds': -1}, {'last_condition': 'other'},
                    {'outcome': 'complete'}, {'polls': KeyError}, {'elapsed_seconds': KeyError}, {'last_condition': KeyError}]
        for fields in rejected:
            with self.subTest(fields=fields), self.assertRaises(archive.InvalidArchiveConfig):
                project(**fields)
        with self.assertRaises(archive.InvalidArchiveConfig):
            archive.project_phase_status({'run_id': run, 'phase': 'C0', 'modes': [
                {'mode': 'verify', 'repeat': 1, 'job': 'c03-42-1-c0-r1-verify', 'outcome': 'ready'}]})

    def test_complete_consumer_phase(self):
        # T5
        fixture = self.phase()
        code, manifest, receipt = fixture.run_archive()
        self.assertEqual([], manifest['gaps'])
        self.assertEqual(0, code)
        self.assertTrue(manifest['archive_complete'])
        self.assertEqual(26, len(manifest['expected_units']))
        self.assertEqual([], manifest['captures'])
        objects = {o['source']['name']: o for o in manifest['objects']}
        blob = objects[fixture.blob_name(1, 'consumer-subject', 1)]
        self.assertEqual('consumer_record', blob['kind'])
        self.assertEqual('verified', blob['state'])
        self.assertEqual('v1/' + 'a' * 40 + '/42/1/c03-42-1/C0/objects/consumer-record/' + fixture.blob_name(1, 'consumer-subject', 1),
                         blob['destination_name'])
        start = objects['c03-42-1/C0/r1/consumer-start.json']
        self.assertEqual(('verifier_output', 'verified', True), (start['kind'], start['state'], start['required']))
        kinds = {o['kind'] for o in manifest['objects']}
        self.assertTrue({'consumer_registration', 'consumer_completion', 'consumer_record'} <= kinds)
        self.assertNotIn(fixture.blob_name(1, 'consumer-subject', 2), objects)

    def test_killed_subject_without_completion(self):
        # T6: a self-killed subject writes no completion and, at shutdown only, no native index (plan section 12, D2).
        fixture = self.phase()
        for repeat in range(1, 6):
            subject = 'consumer-subject'
            fixture.member(repeat, subject, blobs=1, completion=False)
            for index in (1,):
                del fixture.h.cli.blobs[('source123', 'consumer-record', fixture.blob_name(repeat, subject, index))]
            del fixture.h.cli.blobs[('source123', 'consumer-record', fixture.root(repeat, subject) + 'completion.json')]
            del fixture.h.cli.blobs[('source123', 'native-monitoring',
                                     f'c03-42-1/C0/{fixture.job(repeat, subject)}/uid/native-monitoring/index.json')]
        code, manifest, receipt = fixture.run_archive()
        self.assertEqual(1, code)
        self.assertFalse(manifest['archive_complete'])
        self.assertEqual([], [g for g in manifest['gaps'] if g['boundary'].startswith('consumer ')])
        subjects = [o for o in manifest['objects'] if '/consumer/c03-42-1-c0-r' in o['source']['name'] and '-consumer-subject-pod/' in o['source']['name']]
        self.assertEqual(5, sum(o['kind'] == 'consumer_record' for o in subjects))
        self.assertEqual(0, sum(o['kind'] == 'consumer_completion' for o in subjects))
        expected = set()
        for repeat in range(1, 6):
            job = fixture.job(repeat, 'consumer-subject')
            expected.add(('source_missing', 'source properties', f'c03-42-1/C0/{job}/uid/native-monitoring/index.json', None))
            expected.add(('monitoring_incomplete', 'native index unavailable', None, job))
        self.assertEqual(expected, {(g['code'], g['boundary'], g['object_name'], g['expected_unit']) for g in manifest['gaps']})

    def test_extra_record_blob_after_completion_count(self):
        # T7
        fixture = self.phase()
        fixture.member(1, 'consumer-witness', rows=5, blobs=1)
        fixture.h.cli.put('consumer-record', fixture.blob_name(1, 'consumer-witness', 1), b'{}\n')
        code, manifest, receipt = fixture.run_archive()
        name = fixture.blob_name(1, 'consumer-witness', 1)
        self.assertEqual(1, code)
        self.assertFalse(manifest['archive_complete'])
        self.assertEqual([('monitoring_incomplete', 'consumer record inventory', name)],
                         [(g['code'], g['boundary'], g['object_name']) for g in manifest['gaps']])
        self.assertNotIn(name, {o['source']['name'] for o in manifest['objects']})

    def test_missing_record_blob_named_by_completion(self):
        # T8
        fixture = self.phase()
        fixture.member(1, 'consumer-subject', rows=40001, blobs=2)
        name = fixture.blob_name(1, 'consumer-subject', 1)
        del fixture.h.cli.blobs[('source123', 'consumer-record', name)]
        code, manifest, _ = fixture.run_archive()
        self.assertEqual(1, code)
        self.assertEqual([('source_missing', 'source properties', name)],
                         [(g['code'], g['boundary'], g['object_name']) for g in manifest['gaps']])
        self.assertEqual('missing', next(o for o in manifest['objects'] if o['source']['name'] == name)['state'])

    def test_registration_identity_mismatch(self):
        # T9
        for field, value in (('pod_uid', 'other'), ('role', 'witness')):
            with self.subTest(field=field):
                fixture = self.phase()
                root = fixture.root(2, 'consumer-subject')
                key = ('source123', 'consumer-record', root + 'registration.json')
                document = json.loads(fixture.h.cli.blobs[key])
                document[field] = value
                fixture.h.cli.put('consumer-record', root + 'registration.json', document)
                code, manifest, _ = fixture.run_archive()
                self.assertEqual(1, code)
                self.assertEqual([('monitoring_incomplete', 'consumer registration identity', root + 'registration.json')],
                                 [(g['code'], g['boundary'], g['object_name']) for g in manifest['gaps']])
                self.assertEqual('verified', next(o for o in manifest['objects'] if o['source']['name'] == root + 'registration.json')['state'])

    def test_registration_other_identity_fields(self):
        # 8.1: each remaining identity field is checked, and a non-object or oversize document fails the same way.
        mutations = {'run_id': 'c03-42-2', 'phase': 'C1', 'scenario_id': 'C01', 'configuration_id': 'C1', 'repeat_number': 3,
                     'job_name': 'other', 'pod_name': 'other', 'instance_id': 'other', 'source_commit': 'b' * 40, 'kind': 'consumer_completion'}
        for field, value in mutations.items():
            with self.subTest(field=field):
                fixture = self.phase()
                root = fixture.root(1, 'consumer-witness')
                key = ('source123', 'consumer-record', root + 'registration.json')
                document = json.loads(fixture.h.cli.blobs[key])
                document[field] = value
                fixture.h.cli.put('consumer-record', root + 'registration.json', document)
                _, manifest, _ = fixture.run_archive()
                self.assertEqual(1, len(gaps(manifest, 'consumer registration identity')))
        for body in (b'[]', b'x' * 65537):
            with self.subTest(body=body[:4]):
                fixture = self.phase()
                root = fixture.root(1, 'consumer-witness')
                fixture.h.cli.put('consumer-record', root + 'registration.json', body)
                _, manifest, _ = fixture.run_archive()
                self.assertEqual(1, len(gaps(manifest, 'consumer registration identity')))

    def test_completion_wrong_kind(self):
        # T9b
        fixture = self.phase()
        root = fixture.root(2, 'consumer-witness')
        key = ('source123', 'consumer-record', root + 'completion.json')
        document = json.loads(fixture.h.cli.blobs[key])
        document['kind'] = 'consumer_registration'
        fixture.h.cli.put('consumer-record', root + 'completion.json', document)
        _, manifest, _ = fixture.run_archive()
        self.assertEqual([('monitoring_incomplete', 'consumer completion contract', root + 'completion.json')],
                         [(g['code'], g['boundary'], g['object_name']) for g in manifest['gaps']])
        objects = {o['source']['name']: o for o in manifest['objects']}
        self.assertEqual('verified', objects[root + 'record/000000.jsonl']['state'])
        self.assertEqual('verified', objects[root + 'record/000001.jsonl']['state'])

    def test_completion_fields_are_checked(self):
        # 8.2: each completion field and its boundary.
        cases = {'rows': -1, 'messages_processed': True, 'record_blobs': 3, 'run_id': 'other', 'pod_uid': 'other', 'role': 'subject'}
        for field, value in cases.items():
            with self.subTest(field=field):
                fixture = self.phase()
                root = fixture.root(1, 'consumer-witness')
                key = ('source123', 'consumer-record', root + 'completion.json')
                document = json.loads(fixture.h.cli.blobs[key])
                document[field] = value
                fixture.h.cli.put('consumer-record', root + 'completion.json', document)
                _, manifest, _ = fixture.run_archive()
                self.assertEqual(1, len(gaps(manifest, 'consumer completion contract')))
        # A completion that matches registration's pod_name field is not required to carry it.
        fixture = self.phase()
        _, manifest, _ = fixture.run_archive()
        self.assertEqual([], manifest['gaps'])

    def test_invalid_completion_falls_back_to_probe(self):
        # T10
        fixture = self.phase()
        fixture.member(1, 'consumer-subject', rows=5, blobs=1)
        root = fixture.root(1, 'consumer-subject')
        del fixture.h.cli.blobs[('source123', 'consumer-record', root + 'record/000001.jsonl')]
        document = json.loads(fixture.h.cli.blobs[('source123', 'consumer-record', root + 'completion.json')])
        document['record_blobs'] = 3
        fixture.h.cli.put('consumer-record', root + 'completion.json', document)
        _, manifest, _ = fixture.run_archive()
        self.assertEqual([('monitoring_incomplete', 'consumer completion contract', root + 'completion.json')],
                         [(g['code'], g['boundary'], g['object_name']) for g in manifest['gaps']])
        objects = {o['source']['name']: o for o in manifest['objects']}
        self.assertEqual('verified', objects[root + 'record/000000.jsonl']['state'])
        self.assertNotIn(root + 'record/000001.jsonl', objects)
        self.assertFalse(any(g['code'] == 'source_missing' for g in manifest['gaps']))

    def test_missing_first_record_blob(self):
        # T11
        fixture = self.phase()
        fixture.member(1, 'consumer-subject', blobs=0, completion=False)
        root = fixture.root(1, 'consumer-subject')
        for key in [key for key in fixture.h.cli.blobs if key[1] == 'consumer-record' and key[2].startswith(root + 'record/')]:
            del fixture.h.cli.blobs[key]
        del fixture.h.cli.blobs[('source123', 'consumer-record', root + 'completion.json')]
        _, manifest, _ = fixture.run_archive()
        self.assertEqual([('monitoring_incomplete', 'consumer record inventory', root + 'record/000000.jsonl')],
                         [(g['code'], g['boundary'], g['object_name']) for g in manifest['gaps']])

    def test_probe_gap_stops(self):
        # T11b
        fixture = self.phase()
        root = fixture.root(1, 'consumer-subject')
        del fixture.h.cli.blobs[('source123', 'consumer-record', root + 'completion.json')]
        fixture.h.cli.put('consumer-record', root + 'record/000002.jsonl', b'{}\n')
        failing = root + 'record/000001.jsonl'
        original = fixture.h.cli.__call__
        def command(args, deadline):
            if args[3] == 'show' and args[args.index('--name') + 1] == failing:
                fixture.h.cli.calls.append(args)
                return 1, b'', b'ServerBusy'
            return original(args, deadline)
        fixture.h.cli = type('CLI', (), {'__call__': staticmethod(command), 'blobs': fixture.h.cli.blobs,
                                         'calls': fixture.h.cli.calls, 'put': fixture.h.cli.put})()
        _, manifest, _ = fixture.run_archive()
        self.assertEqual([('transport', 'properties', failing)],
                         [(g['code'], g['boundary'], g['object_name']) for g in manifest['gaps']])
        names = {o['source']['name'] for o in manifest['objects']}
        self.assertIn(root + 'record/000000.jsonl', names)
        self.assertNotIn(root + 'record/000002.jsonl', names)
        shown = [args[args.index('--name') + 1] for args in fixture.h.cli.calls if args[3] == 'show']
        self.assertNotIn(root + 'record/000002.jsonl', shown)

    def test_probe_stops_at_the_object_limit(self):
        fixture = self.phase()
        root = fixture.root(1, 'consumer-subject')
        del fixture.h.cli.blobs[('source123', 'consumer-record', root + 'completion.json')]
        for index in range(2, 40):
            fixture.h.cli.put('consumer-record', f'{root}record/{index:06d}.jsonl', b'{}\n')
        original = archive.ArchiveMaxObjects
        archive.ArchiveMaxObjects = 30
        self.addCleanup(setattr, archive, 'ArchiveMaxObjects', original)
        _, manifest, _ = fixture.run_archive()
        limit = [g for g in manifest['gaps'] if g['code'] == 'object_limit']
        self.assertGreaterEqual(len(limit), 1)
        shown = [args[args.index('--name') + 1] for args in fixture.h.cli.calls if args[3] == 'show']
        self.assertLess(sum(name.startswith(root + 'record/') for name in shown), 38)

    def test_missing_readiness_record(self):
        # T12
        fixture = self.phase()
        fixture.h.status['modes'] = [r for r in fixture.h.status['modes'] if not (r['mode'] == 'consumer-readiness' and r['repeat'] == 1)]
        _, manifest, _ = fixture.run_archive()
        self.assertEqual([('monitoring_incomplete', 'missing readiness status', None, 'c03-42-1-c0-r1-consumer-readiness')],
                         [(g['code'], g['boundary'], g['object_name'], g['expected_unit']) for g in manifest['gaps']])
        self.assertFalse(any(u['mode'] == 'consumer-readiness' for u in manifest['expected_units']))

    def test_unstarted_member_job(self):
        # T13
        fixture = self.phase()
        modes = fixture.h.status['modes']
        for record in modes:
            if record.get('repeat') == 5:
                if record['mode'] == 'consumer-readiness':
                    record.update(outcome='not_attempted', polls=None, elapsed_seconds=None, last_condition=None, reason='orchestration_deadline')
                else:
                    record.update(apply=None, collect=None, outcome='not_attempted', reason='orchestration_deadline')
        for mode in MEMBERS + ('start-line', 'verify', 'loadgen'):
            job = fixture.job(5, mode)
            for key in [key for key in fixture.h.cli.blobs if f'/{job}/' in key[2]]:
                del fixture.h.cli.blobs[key]
        for key in [key for key in fixture.h.cli.blobs if key[1] in ('verifier', 'ledger', 'consumer-record') and '/r5/' in key[2]]:
            del fixture.h.cli.blobs[key]
        _, manifest, _ = fixture.run_archive()
        required = {g['expected_unit'] for g in manifest['gaps'] if g['boundary'] == 'required job not started'}
        self.assertEqual({fixture.job(5, mode) for mode in MEMBERS + ('start-line', 'verify')}, required)
        objects = {o['source']['name']: o for o in manifest['objects']}
        for leaf in ('start-line.json', 'broker-inventory.json', 'start-line-completion.json', 'consumer-start.json', 'result.json', 'verify-completion.json'):
            self.assertEqual('not_started', objects[f'c03-42-1/C0/r5/{leaf}']['state'])
        self.assertFalse(any('/r5/consumer/' in name for name in objects))

    def test_unplanned_consumer_record_rejected(self):
        # Requirement 5 is on main; this checks it still holds with readiness records present.
        for extra in ({'mode': 'consumer-subject', 'repeat': 6, 'job': 'c03-42-1-c0-r6-consumer-subject', 'apply': 0, 'collect': 0, 'outcome': 'failed'},
                      readiness_record('c03-42-1', 'C0', 1)):
            with self.subTest(mode=extra['mode'], repeat=extra['repeat']):
                fixture = self.phase()
                fixture.h.status['modes'].append(extra)
                _, manifest, _ = fixture.run_archive()
                self.assertEqual(1, len(gaps(manifest, 'inventory construction')))

    def test_fixed_inventory_scenario_argument(self):
        # Requirement 9: the scenario argument is optional and adds the consumer start snapshot for consumer start-lines only.
        modes = [{'mode': 'start-line', 'repeat': 1, 'apply': 0}]
        plain = archive.ordinary_fixed_inventory('c03-42-1', 'C0', modes)
        self.assertEqual(3, len(plain))
        self.assertEqual(plain, archive.ordinary_fixed_inventory('c03-42-1', 'C0', modes, 'B04'))
        names = [item['name'] for item in archive.ordinary_fixed_inventory('c03-42-1', 'C0', modes, 'C03')]
        self.assertEqual(sorted([item['name'] for item in plain] + ['c03-42-1/C0/r1/consumer-start.json']), names)
        unstarted = archive.ordinary_fixed_inventory('c03-42-1', 'C0', [{'mode': 'start-line', 'repeat': 1, 'apply': None}], 'C03')
        self.assertEqual({'not_started'}, {item['state'] for item in unstarted})
        self.assertEqual(4, len(unstarted))


if __name__ == '__main__':
    unittest.main()

"""V2 candidate body exclusion through the archive adapter and transport."""
import base64
import contextlib
import hashlib
import io
import json
import unittest

from test_archive_inventory_integration import BODY, Harness, candidate_fixture, encoded, publish_v2_actor, sha


def fixture():
    h = Harness('probe-batch-2', 'single-action', 'probe-42-1', 'smoke', items=['F06'],
                probe_v2_selection='authentication-controls', capture_authentication_metadata=True)
    for config, stages in [('C6', ('seed','action','judge')), ('C9', ('action','judge'))]:
        for stage in stages:
            job = f'probe-42-1-f06-{config.lower()}-{stage}'
            mode = 'single-action-v2-judge' if stage == 'judge' else 'single-action-v2'
            pod = h.job(mode, job, 'F06', config, stage=stage)
            if stage != 'judge':
                publish_v2_actor(h, job, pod, config, stage)
                continue
            prefix = f'probe-42-1/single-action-v2/F06/{config}/'
            data = encoded(candidate_fixture('probe-42-1', config))
            h.cli.put('verifier', prefix+'candidate.json', data)
            h.cli.put('verifier', prefix+'judge-completion.json', {'schema_version':2, 'mode':mode, 'run_id':'probe-42-1',
                'scenario_id':'F06', 'configuration_id':config, 'source_commit':'a'*40, 'job_name':job, 'pod_name':pod,
                'pod_uid':'uid', 'candidate_name':prefix+'candidate.json', 'candidate_sha256':sha(data),
                'handler_exit_code':0, 'cleanup_completed':True})
    return h


class CandidateProjectionTests(unittest.TestCase):
    def test_complete_candidate_projects_bodies_and_binds_original(self):
        h = fixture(); self.addCleanup(h.close)
        output = io.StringIO()
        with contextlib.redirect_stdout(output): code, manifest, receipt = h.run()
        self.assertEqual(0, code); self.assertTrue(receipt['archive_complete'])
        source = 'probe-42-1/single-action-v2/F06/C6/candidate.json'
        original = h.cli.blobs[('source123', 'verifier', source)]
        obj = next(o for o in manifest['objects'] if o['source']['name'] == source)
        self.assertEqual('source_metadata_projection', obj['kind'])
        archived = h.cli.blobs[('archive123', 'run-evidence', obj['destination_name'])]
        projected = json.loads(archived)
        self.assertEqual('probe_v2_candidate', projected['format'])
        declared = json.loads(h.cli.blobs[('source123', 'verifier', source.replace('candidate.json', 'judge-completion.json'))])
        self.assertEqual(sha(original), declared['candidate_sha256'])
        self.assertEqual(declared['candidate_sha256'], projected['source']['sha256'])
        self.assertEqual(('verifier', source), (projected['source']['container'], projected['source']['name']))
        self.assertEqual(len(original), projected['source']['bytes'])
        self.assertEqual('etag', projected['source']['etag'])
        self.assertEqual(sha(archived), obj['sha256']); self.assertNotEqual(sha(original), obj['sha256'])
        rows = projected['metadata']['repeats']
        self.assertEqual({'is_null':False, 'length':len(BODY), 'sha256':hashlib.sha256(BODY).hexdigest()}, rows[0]['read_observation']['record']['value_base64'])
        self.assertEqual({'is_null':True, 'length':None, 'sha256':None}, rows[1]['read_observation']['record']['value_base64'])
        self.assertEqual({'is_null':False, 'length':0, 'sha256':hashlib.sha256(b'').hexdigest()}, rows[2]['read_observation']['record']['value_base64'])
        self.assertEqual(rows[0]['read_observation']['record']['value_base64'], rows[3]['read_observation']['record']['value_base64'])
        self.assertEqual('aw==', rows[0]['read_observation']['record']['key_base64'])
        self.assertEqual('d'*64, rows[0]['seed_reference']['raw_sha256'])
        self.assertEqual('c'*64, rows[0]['statistics_reference']['raw_sha256'])
        self.assertEqual('native error retained', rows[0]['delivery']['error']['reason'])
        self.assertEqual(['diagnostic retained'], projected['metadata']['diagnostics'])
        published = b''.join(data for (account, _, _), data in h.cli.blobs.items() if account == 'archive123')
        public = b''.join(p.read_bytes() for p in (h.artifacts/'archive').iterdir() if p.is_file())
        for material in (published, public, output.getvalue().encode()):
            self.assertNotIn(base64.b64encode(BODY), material)
            self.assertNotIn(BODY, material)
        self.assertIn(b'probe_v2_client_log diagnostic', published)

    def test_invalid_candidate_and_reference_never_complete(self):
        for fault in ('missing', 'hash', 'receipt_reference', 'identity', 'malformed', 'truncated', 'unknown', 'incomplete',
                      'empty', 'invalid_utf8', 'duplicate', 'nonstr_body', 'int32_overflow', 'int64_overflow',
                      'int32_underflow', 'int64_underflow', 'wrong_number_type', 'bad_reference_hash', 'bad_statistics_hash', 'bad_key', 'bad_semantic_key', 'bad_body'):
            with self.subTest(fault=fault):
                h = fixture()
                try:
                    prefix = 'probe-42-1/single-action-v2/F06/C6/'
                    key = ('source123', 'verifier', prefix+'candidate.json')
                    receipt_key = ('source123', 'verifier', prefix+'judge-completion.json')
                    receipt = json.loads(h.cli.blobs[receipt_key]); candidate = candidate_fixture('probe-42-1', 'C6')
                    if fault == 'missing': del h.cli.blobs[key]
                    elif fault == 'hash': receipt['candidate_sha256'] = '0'*64
                    elif fault == 'receipt_reference': receipt['candidate_name'] += '.wrong'
                    else:
                        if fault == 'identity': candidate['run_id'] = 'wrong'
                        if fault == 'unknown': candidate['repeats'][0]['read_observation']['record']['unexpected'] = 'payload'
                        if fault == 'incomplete': del candidate['repeats'][0]['read_observation']['record']['value_base64']
                        record = candidate['repeats'][0]['read_observation']['record']
                        if fault == 'nonstr_body': record['value_base64'] = 1
                        if fault == 'int32_overflow': record['partition'] = 2**31
                        if fault == 'int64_overflow': record['offset'] = 2**63
                        if fault == 'int32_underflow': record['partition'] = -(2**31)-1
                        if fault == 'int64_underflow': record['offset'] = -(2**63)-1
                        if fault == 'wrong_number_type': record['offset'] = True
                        if fault == 'bad_reference_hash': candidate['repeats'][0]['seed_reference']['raw_sha256'] = 'not-a-hash'
                        if fault == 'bad_statistics_hash': candidate['repeats'][0]['statistics_reference']['raw_sha256'] = 'not-a-hash'
                        if fault == 'bad_key': record['key_base64'] = 'not base64!'
                        if fault == 'bad_semantic_key': candidate['repeats'][0]['semantic_outcome']['key_base64'] = 'not base64!'
                        if fault == 'bad_body': record['value_base64'] = 'not base64!'
                        data = b'{' if fault == 'malformed' else encoded(candidate)
                        if fault == 'empty': data = b''
                        if fault == 'invalid_utf8': data = data.replace(b'diagnostic retained', b'\xff', 1)
                        if fault == 'duplicate': data = data[:-1] + b',\"status\":\"invalid\"}'
                        if fault == 'truncated': data = data[:-1]
                        h.cli.blobs[key] = data; receipt['candidate_sha256'] = sha(data)
                    h.cli.blobs[receipt_key] = encoded(receipt)
                    code, manifest, result = h.run()
                    self.assertNotEqual(0, code); self.assertFalse(result['archive_complete']); self.assertIsNone(result['completion'])
                    self.assertFalse(any(k[0] == 'archive123' and k[2].endswith('/C6/candidate.json') for k in h.cli.blobs))
                finally:
                    h.close()

    def test_candidate_size_boundary(self):
        maximum = 16777216
        for length in (maximum - 1, maximum, maximum + 1):
            with self.subTest(length=length):
                h = fixture()
                try:
                    prefix = 'probe-42-1/single-action-v2/F06/C6/'
                    key = ('source123', 'verifier', prefix+'candidate.json')
                    receipt_key = ('source123', 'verifier', prefix+'judge-completion.json')
                    raw = h.cli.blobs[key]
                    raw += b' ' * (length - len(raw))
                    h.cli.blobs[key] = raw
                    receipt = json.loads(h.cli.blobs[receipt_key]); receipt['candidate_sha256'] = sha(raw)
                    h.cli.blobs[receipt_key] = encoded(receipt)
                    code, manifest, result = h.run()
                    self.assertEqual(length <= maximum, result['archive_complete'])
                    if length <= maximum:
                        self.assertEqual(0, code)
                        obj = next(o for o in manifest['objects'] if o['source']['name'] == prefix+'candidate.json')
                        projected = json.loads(h.cli.blobs[('archive123', 'run-evidence', obj['destination_name'])])
                        self.assertEqual(length, projected['source']['bytes'])
                    else:
                        self.assertNotEqual(0, code); self.assertIsNone(result['completion'])
                        self.assertTrue(any(g['code'] == 'source_too_large' for g in result['gaps']))
                        downloads = [c for c in h.cli.calls if c[3] == 'download' and '--account-name' in c
                                     and c[c.index('--account-name')+1] == 'source123'
                                     and c[c.index('--name')+1] == prefix+'candidate.json']
                        self.assertEqual([], downloads)
                finally:
                    h.close()

    def test_scientifically_incomplete_candidate_can_be_archived(self):
        h = fixture(); self.addCleanup(h.close)
        prefix = 'probe-42-1/single-action-v2/F06/C6/'
        key = ('source123', 'verifier', prefix+'candidate.json')
        receipt_key = ('source123', 'verifier', prefix+'judge-completion.json')
        candidate = candidate_fixture('probe-42-1', 'C6')
        candidate['status'] = 'incomplete'; candidate['repeats'][0]['status'] = 'invalid'
        candidate['diagnostics'] = ['scientific incompleteness retained']
        row = candidate['repeats'][0]; record = row['read_observation']['record']
        record['partition'] = -(2**31); record['offset'] = -(2**63)
        record['key_base64'] = base64.b64encode(b'k'*5000).decode()
        row['semantic_outcome']['key_base64'] = record['key_base64']
        record['value_base64'] = base64.b64encode(b'v'*5000).decode()
        row['delivery']['error']['reason'] = 'r'*2000
        candidate['repeats'][1]['read_observation']['record']['partition'] = 2**31-1
        candidate['repeats'][1]['read_observation']['record']['offset'] = 2**63-1
        data = encoded(candidate); h.cli.blobs[key] = data
        receipt = json.loads(h.cli.blobs[receipt_key]); receipt['candidate_sha256'] = sha(data)
        h.cli.blobs[receipt_key] = encoded(receipt)
        code, manifest, result = h.run()
        self.assertEqual(0, code); self.assertTrue(result['archive_complete'])
        obj = next(o for o in manifest['objects'] if o['source']['name'] == prefix+'candidate.json')
        document = json.loads(h.cli.blobs[('archive123', 'run-evidence', obj['destination_name'])])
        self.assertEqual('incomplete', document['metadata']['status'])
        self.assertEqual('invalid', document['metadata']['repeats'][0]['status'])
        self.assertEqual(5000, document['metadata']['repeats'][0]['read_observation']['record']['value_base64']['length'])
        self.assertEqual('r'*2000, document['metadata']['repeats'][0]['delivery']['error']['reason'])
        self.assertEqual(2**63-1, document['metadata']['repeats'][1]['read_observation']['record']['offset'])

"""Pack coverage and binding checks against real archive control validation."""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
def load(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module
archive = load('archive')
pack = load('archive_pack')

# Expected contexts are independently specified, not read from controls.
C0 = {'github_run_id': '42', 'github_run_attempt': 1, 'run_id': 'a01-42-1',
      'phase': 'C0', 'source_commit': 'a' * 40, 'scenario_id': 'A01',
      'configuration_id': 'C0', 'images': {'loadgen': None, 'verifier': None, 'probe': None},
      'run_kind': 'smoke', 'purpose': 'diagnostic', 'scientific_admissible': False}
C1 = {**C0, 'phase': 'C1', 'configuration_id': 'C1'}
MANIFEST = b'{"schema_version":1,"kind":"run_evidence_archive","archive_complete":true,"run":{"github_run_id":"42","github_run_attempt":1,"run_id":"a01-42-1","scenario_id":"A01","configuration_id":"C0","phase":"C0","source_commit":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","images":{"loadgen":null,"verifier":null,"probe":null},"run_kind":"smoke","purpose":"diagnostic","scientific_admissible":false},"destination":{"account_host":"archive123.blob.core.windows.net","container":"run-evidence","prefix":"v1/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/42/1/a01-42-1/C0/","retention_days":90},"policy":{"max_bytes":17179869184,"max_object_bytes":268435456,"max_objects":1024,"deadline_seconds":600,"transfer_passes":1,"metadata_chunk_bytes":134217728,"metadata_row_bytes":1048576},"expected_units":[],"objects":[],"captures":[],"totals":{"expected_objects":0,"verified_objects":0,"verified_bytes":0},"gaps":[],"completion":null}\n'
MARKER = b'{"schema_version":1,"kind":"run_evidence_archive_completion","identity":{"github_run_id":"42","github_run_attempt":1,"run_id":"a01-42-1","phase":"C0","source_commit":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"},"manifest":{"name":"v1/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/42/1/a01-42-1/C0/archive-manifest.json","bytes":911,"sha256":"6c5fcc28afea6d49840fc75d06c28551a4a45afe119d822eb1b43b5872d0cbba"},"totals":{"expected_objects":0,"verified_objects":0,"verified_bytes":0},"archive_complete":true}\n'


def encode(obj):
    return json.dumps(obj, separators=(',', ':')).encode()


def triple(run=C0):
    # Construct valid alternate triples using standard JSON and hashlib, independent
    # of the production serializer. The canonical base bytes and digests are literal.
    manifest = json.loads(MANIFEST)
    manifest['run'] = copy.deepcopy(run)
    prefix = 'v1/{source_commit}/{github_run_id}/{github_run_attempt}/{run_id}/{phase}/'.format(**run)
    manifest['destination']['prefix'] = prefix
    mb = MANIFEST if run == C0 else encode(manifest)
    identity = {k: run[k] for k in ('github_run_id', 'github_run_attempt', 'run_id', 'phase', 'source_commit')}
    mr = {'name': prefix + 'archive-manifest.json', 'bytes': len(mb), 'sha256': hashlib.sha256(mb).hexdigest()}
    marker = {'schema_version': 1, 'kind': 'run_evidence_archive_completion', 'identity': identity,
              'manifest': mr, 'totals': manifest['totals'], 'archive_complete': True}
    cb = MARKER if run == C0 else encode(marker)
    receipt = {'schema_version': 1, 'kind': 'run_evidence_archive_receipt', 'identity': identity,
               'archive_complete': True, 'manifest': mr,
               'completion': {'name': prefix + 'archive-completion.json', 'bytes': len(cb),
                              'sha256': hashlib.sha256(cb).hexdigest()}, 'gaps': []}
    return {'identity': identity, 'manifest': mb, 'marker': cb, 'receipt': encode(receipt)}


def identity(run):
    return {k: run[k] for k in ('github_run_id', 'github_run_attempt', 'run_id', 'phase', 'source_commit')}


def check(expected, supplied):
    return pack.verify_pack(expected, supplied, archive.verify_controls)


class PackTests(unittest.TestCase):
    def test_complete_single_and_multiple(self):
        self.assertTrue(check([C0], [triple()])['complete'])
        self.assertTrue(check([C0, C1], [triple(C1), triple()])['complete'])
        self.assertEqual('6c5fcc28afea6d49840fc75d06c28551a4a45afe119d822eb1b43b5872d0cbba', hashlib.sha256(MANIFEST).hexdigest())

    def test_same_phase_from_distinct_runs(self):
        other = {**C0, 'github_run_id': '43', 'run_id': 'a01-43-1'}
        self.assertTrue(check([C0, other], [triple(), triple(other)])['complete'])
        self.assertEqual([identity(other)], check([C0, other], [triple()])['missing'])
        self.assertEqual([identity(other)], check([C0], [triple(), triple(other)])['extra'])

    def test_coverage_and_duplicates(self):
        self.assertEqual([identity(C1)], check([C0, C1], [triple()])['missing'])
        self.assertEqual([identity(C1)], check([C0], [triple(), triple(C1)])['extra'])
        r = check([C0, C0], [triple()])
        self.assertEqual([identity(C0)], r['duplicate_expected']); self.assertEqual([identity(C0)], r['missing'])
        r = check([C0], [triple(), triple()])
        self.assertEqual([identity(C0)], r['duplicate_provided']); self.assertEqual([identity(C0)], r['missing'])
        self.assertFalse(check([], [])['complete'])

    def test_valid_controls_for_other_context_are_rejected(self):
        changes = [{'github_run_id': '43'}, {'github_run_attempt': 2}, {'run_id': 'a01-43-1'},
                   {'phase': 'C1'}, {'source_commit': 'b' * 40}, {'scenario_id': 'A02'},
                   {'configuration_id': 'C1'}, {'purpose': 'other'}, {'run_kind': 'scientific'},
                   {'scientific_admissible': True},
                   {'images': {'loadgen': 'registry.example/parchwell/loadgen@sha256:' + 'b' * 64,
                               'verifier': None, 'probe': None}}]
        for change in changes:
            with self.subTest(change=change):
                wrong = triple({**C0, **change})
                archive.verify_controls(wrong['manifest'], wrong['marker'], wrong['receipt'])
                wrong['identity'] = identity(C0)
                result = check([C0], [wrong])
                self.assertFalse(result['complete']); self.assertEqual([identity(C0)], result['missing'])

    def test_bad_controls_and_bindings(self):
        original = triple()
        receipt = json.loads(original['receipt'])
        incomplete = {**receipt, 'archive_complete': False, 'completion': None,
                      'gaps': [{'code': 'transport', 'boundary': 'test', 'object_name': None, 'expected_unit': None}]}
        variants = [{**original, 'marker': b''},
                    {**original, 'manifest': original['manifest'].replace(b'"archive_complete":true', b'"archive_complete":false')},
                    {**original, 'marker': original['marker'].replace(b'"archive_complete":true', b'"archive_complete":false')},
                    {**original, 'receipt': encode(incomplete)},
                    {**original, 'receipt': original['receipt'][:-5]},
                    {**original, 'manifest': original['manifest'] + b' '},
                    {**original, 'marker': original['marker'] + b' '},
                    {**original, 'receipt': encode({**receipt, 'manifest': {**receipt['manifest'], 'bytes': 1}})},
                    {**original, 'receipt': encode({**receipt, 'manifest': {**receipt['manifest'], 'sha256': 'b' * 64}})},
                    {**original, 'receipt': encode({**receipt, 'identity': {**receipt['identity'], 'run_id': 'other'}})}]
        for variant in variants:
            with self.subTest(variant=variant):
                result = check([C0], [variant])
                self.assertFalse(result['complete']); self.assertEqual([identity(C0)], result['missing'])

    def test_closed_input_types_and_absent_proof(self):
        for bad in ({**C0, 'scientific_admissible': 0}, {**C0, 'extra': 1},
                    {**C0, 'images': {'loadgen': None}}, {**C0, 'github_run_attempt': True}):
            self.assertFalse(check([bad], [triple()])['complete'])
        for bad in (None, {}, {**triple(), 'extra': 1}, {**triple(), 'receipt': None}):
            self.assertFalse(check([C0], [bad])['complete'])
        self.assertEqual([identity(C0)], check([C0], [])['missing'])


if __name__ == '__main__':
    unittest.main()

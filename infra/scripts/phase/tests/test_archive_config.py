"""Offline checks of the archive configuration boundary."""
import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('archive', Path(__file__).resolve().parents[1] / 'archive.py')
archive = importlib.util.module_from_spec(spec)
spec.loader.exec_module(archive)
RID = '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/archive123'
VALID = {'storage_account_id': RID, 'account_name': 'archive123',
         'blob_service_uri': 'https://archive123.blob.core.windows.net/',
         'container_name': 'run-evidence', 'container_id': RID + '/blobServices/default/containers/run-evidence',
         'retention_days': 90}


class ArchiveConfigTests(unittest.TestCase):
    def test_valid_config_is_copied(self):
        result = archive.validate_archive_config(VALID)
        self.assertEqual(VALID, result)
        self.assertIsNot(VALID, result)

    def test_invalid_config(self):
        cases = [None, {}, {**VALID, 'unknown': 'x'}, {**VALID, 'retention_days': True},
                 {**VALID, 'retention_days': 89}, {**VALID, 'container_name': 'other'},
                 {**VALID, 'container_id': RID}, {**VALID, 'account_name': 'different'},
                 {**VALID, 'blob_service_uri': 'https://archive123.blob.core.windows.net/?sig=secret'},
                 {**VALID, 'blob_service_uri': 'https://user@archive123.blob.core.windows.net/'},
                 {**VALID, 'blob_service_uri': 'https://archive123.blob.core.windows.net/../private'},
                 {**VALID, 'blob_service_uri': 'https://archive123.blob.core.windows.net:443/'},
                 {**VALID, 'storage_account_id': RID + '/other'}]
        for case in cases:
            with self.subTest(case=case), self.assertRaises(archive.InvalidArchiveConfig):
                archive.validate_archive_config(copy.deepcopy(case))


class ReceiptTests(unittest.TestCase):
    def test_false_receipt_allows_verified_canonical_reference(self):
        identity = {'github_run_id': '42', 'github_run_attempt': 1, 'run_id': 'a01-42-1', 'phase': 'C0', 'source_commit': 'a' * 40}
        ref = {'name': 'v1/a/archive-manifest.json', 'bytes': 100, 'sha256': 'b' * 64}
        gap = {'code': 'transport', 'boundary': 'completion readback', 'object_name': None, 'expected_unit': None}
        receipt = {'schema_version': 1, 'kind': 'run_evidence_archive_receipt', 'identity': identity,
                   'archive_complete': False, 'manifest': ref, 'completion': None, 'gaps': [gap]}
        archive.validate_receipt(receipt)
        for change in ({'completion': ref}, {'gaps': []}, {'archive_complete': True}, {'extra': 1}):
            with self.subTest(change=change), self.assertRaises(archive.InvalidArchiveConfig):
                archive.validate_receipt({**receipt, **change})


class FixedInventoryTests(unittest.TestCase):
    def test_expected_objects_derive_from_jobs_even_without_listing(self):
        modes = [{'mode': 'start-line', 'repeat': 1, 'apply': 0},
                 {'mode': 'verify', 'repeat': 1, 'apply': None},
                 {'mode': 'judge', 'repeat': None, 'apply': 0}]
        result = archive.ordinary_fixed_inventory('a01-42-1', 'C0', modes)
        self.assertEqual([
            ('a01-42-1/C0/candidate.json', True, 'pending'),
            ('a01-42-1/C0/judge-completion.json', True, 'pending'),
            ('a01-42-1/C0/r1/broker-inventory.json', True, 'pending'),
            ('a01-42-1/C0/r1/result.json', False, 'not_started'),
            ('a01-42-1/C0/r1/start-line-completion.json', True, 'pending'),
            ('a01-42-1/C0/r1/start-line.json', True, 'pending'),
            ('a01-42-1/C0/r1/verify-completion.json', False, 'not_started')],
            [(item['name'], item['required'], item['state']) for item in result])


class ControlTests(unittest.TestCase):
    def test_strict_json_rejects_duplicates_nonfinite_and_depth(self):
        for data in (b'{"x":1,"x":2}', b'{"x":NaN}', b'[' * 33 + b'0' + b']' * 33):
            with self.subTest(data=data), self.assertRaises(archive.InvalidArchiveConfig):
                archive.strict_control(data, 65536)

    def test_marker_rejects_bad_totals_and_false_completion(self):
        marker = {'schema_version': 1, 'kind': 'run_evidence_archive_completion',
                  'identity': {'github_run_id': '42', 'github_run_attempt': 1, 'run_id': 'a01-42-1',
                               'phase': 'C0', 'source_commit': 'a' * 40},
                  'manifest': {'name': 'v1/a/archive-manifest.json', 'bytes': 100, 'sha256': 'b' * 64},
                  'totals': {'expected_objects': 0, 'verified_objects': 0, 'verified_bytes': 0},
                  'archive_complete': True}
        archive.validate_marker(marker)
        for changed in ({'archive_complete': False}, {'unknown': 0},
                        {'totals': {'expected_objects': 1, 'verified_objects': 0, 'verified_bytes': 0}},
                        {'totals': {'expected_objects': 0, 'verified_objects': 0, 'verified_bytes': -1}}):
            with self.subTest(changed=changed), self.assertRaises(archive.InvalidArchiveConfig):
                archive.validate_marker({**marker, **changed})


class ArchiveDeadlineTests(unittest.TestCase):
    def test_archive_deadline_is_1800_seconds(self):
        values = (archive.ArchiveDeadlineSeconds, archive._common.DEADLINE, archive._common.POLICY['deadline_seconds'])
        self.assertEqual((1800, 1800, 1800), values)
        self.assertEqual([int, int, int], [type(value) for value in values])
        self.assertEqual(['max_bytes', 'max_object_bytes', 'max_objects', 'deadline_seconds', 'transfer_passes',
                          'metadata_chunk_bytes', 'metadata_row_bytes'], list(archive._common.POLICY))


class CaptureInventoryTests(unittest.TestCase):
    def test_empty_capture_and_declared_chunk(self):
        import json
        identity = {'run_id': 'a01-42-1', 'phase': 'C0', 'scenario_id': 'A01', 'configuration_id': 'C0', 'job_name': 'job', 'pod_uid': 'pod', 'source_commit': 'a' * 40}
        prefix = 'a01-42-1/C0/r1/consumed-metadata/verify-job-pod-w0000/'
        value = {'schema_version': 1, 'capture_id': 'verify-job-pod-w0000', 'identity': identity,
                 'window': {'index': 0, 'kind': 'ordinary', 'repeat': 1, 'topic': 'document-events', 'start': [{'partition': 0, 'offset': 0}], 'end': [{'partition': 0, 'offset': 0}]},
                 'chunks': [], 'observed_rows': 0, 'captured_rows': 0, 'complete': True,
                 'first_missing_ordinal': None, 'gaps': []}
        summary, chunks = archive.capture_inventory(json.dumps(value).encode(), prefix, identity, 'verify', 1, value['capture_id'])
        self.assertTrue(summary['complete'])
        self.assertEqual(0, summary['rows'])
        self.assertEqual([], chunks)
        value.update(chunks=[{'name': prefix + 'chunk-000001.jsonl', 'bytes': 4, 'sha256': 'a' * 64, 'rows': 1}], observed_rows=1, captured_rows=1)
        self.assertEqual(value['chunks'], archive.capture_inventory(json.dumps(value).encode(), prefix, identity, 'verify', 1, value['capture_id'])[1])
        value['chunks'][0]['name'] = prefix + '../private'
        with self.assertRaises(archive.InvalidArchiveConfig):
            archive.capture_inventory(json.dumps(value).encode(), prefix, identity, 'verify', 1, value['capture_id'])

    def test_manifest_policy_deadline_boundary(self):
        identity = {'github_run_id': '42', 'github_run_attempt': 1, 'run_id': 'a01-42-1', 'phase': 'C0', 'source_commit': 'a' * 40}
        prefix = 'v1/' + 'a' * 40 + '/42/1/a01-42-1/C0/'
        base = {'schema_version': 1, 'kind': 'run_evidence_archive', 'archive_complete': True,
                'run': {**identity, 'scenario_id': 'A01', 'configuration_id': 'C0',
                        'images': {'loadgen': None, 'verifier': None, 'probe': None},
                        'run_kind': 'smoke', 'purpose': 'diagnostic', 'scientific_admissible': False},
                'destination': {'account_host': 'archive123.blob.core.windows.net', 'container': 'run-evidence',
                                'prefix': prefix, 'retention_days': 90},
                'expected_units': [], 'objects': [], 'captures': [],
                'totals': {'expected_objects': 0, 'verified_objects': 0, 'verified_bytes': 0}, 'gaps': [], 'completion': None}
        for seconds in (1800, 600, 1799, 1801):
            with self.subTest(deadline_seconds=seconds):
                manifest = {**base, 'policy': {**archive._common.POLICY, 'deadline_seconds': seconds}}
                if seconds == 1800:
                    self.assertEqual(identity, archive.validate_manifest(manifest))
                else:
                    with self.assertRaises(archive.InvalidArchiveConfig):
                        archive.validate_manifest(manifest)

    def test_manifest_zero_data_and_object_completion_rejected(self):
        identity = {'github_run_id': '42', 'github_run_attempt': 1, 'run_id': 'a01-42-1', 'phase': 'C0', 'source_commit': 'a' * 40}
        prefix = 'v1/' + 'a' * 40 + '/42/1/a01-42-1/C0/'
        manifest = {'schema_version': 1, 'kind': 'run_evidence_archive', 'archive_complete': True,
                    'run': {**identity, 'scenario_id': 'A01', 'configuration_id': 'C0',
                            'images': {'loadgen': None, 'verifier': None, 'probe': None},
                            'run_kind': 'smoke', 'purpose': 'diagnostic', 'scientific_admissible': False},
                    'destination': {'account_host': 'archive123.blob.core.windows.net', 'container': 'run-evidence',
                                    'prefix': prefix, 'retention_days': 90},
                    'policy': {'max_bytes': 17179869184, 'max_object_bytes': 268435456, 'max_objects': 1024,
                               'deadline_seconds': 1800, 'transfer_passes': 1, 'metadata_chunk_bytes': 134217728, 'metadata_row_bytes': 1048576},
                    'expected_units': [], 'objects': [], 'captures': [],
                    'totals': {'expected_objects': 0, 'verified_objects': 0, 'verified_bytes': 0}, 'gaps': [], 'completion': None}
        self.assertEqual(identity, archive.validate_manifest(manifest))
        with self.assertRaises(archive.InvalidArchiveConfig):
            archive.validate_manifest({**manifest, 'completion': {}})

    def test_unreached_jobs_are_explicit_not_started(self):
        modes = [{'job': 'a01-42-1-c0-r1-start-line', 'repeat': 1, 'mode': 'start-line', 'apply': 0}]
        units, gaps = archive.ordinary_units('a01-42-1', 'C0', modes)
        self.assertEqual(15, len(gaps))
        self.assertEqual(16, len(units))
        self.assertEqual('started', units[0]['state'])
        self.assertEqual('not_started', units[1]['state'])
        self.assertEqual('a01-42-1-c0-judge', units[-1]['job_name'])

    def test_capture_name_matches_verifier_path(self):
        name, prefix = archive.capture_name('a01-42-1', 'C0', 'verify', 'a01-42-1-c0-r1-verify', 'pod-uid', 0, 1)
        self.assertEqual('verify-a01-42-1-c0-r1-verify-pod-uid-w0000', name)
        self.assertEqual('a01-42-1/C0/r1/consumed-metadata/verify-a01-42-1-c0-r1-verify-pod-uid-w0000/', prefix)
        with self.assertRaises(archive.InvalidArchiveConfig):
            archive.capture_name('a01-42-1', 'C0', 'verify', '../job', 'pod-uid', 0, 1)


class PodProjectionTests(unittest.TestCase):
    def test_private_environment_and_volume_credentials_are_not_retained(self):
        import json
        data = {'items': [{'metadata': {'name': 'job-pod', 'uid': 'pod-uid', 'annotations': {'token': 'SENTINEL'}},
                           'spec': {'containers': [{'name': 'worker', 'env': [{'name': 'Telemetry__ConnectionString', 'value': 'SENTINEL'}]}],
                                    'volumes': [{'secret': {'secretName': 'SENTINEL'}}]},
                           'status': {'phase': 'Failed', 'conditions': [{'type': 'Ready', 'status': 'False', 'message': 'SENTINEL'}],
                                      'containerStatuses': [{'name': 'worker', 'state': {'terminated': {'exitCode': 1, 'message': 'SENTINEL'}}}]}}]}
        output = archive.project_pods(json.dumps(data).encode())
        self.assertNotIn(b'SENTINEL', output)
        self.assertEqual({'schema_version': 1, 'kind': 'pod_diagnostics', 'items': [
            {'name': 'job-pod', 'uid': 'pod-uid', 'phase': 'Failed',
             'conditions': [{'type': 'Ready', 'status': 'False'}],
             'containers': [{'kind': 'containerStatuses', 'name': 'worker', 'terminated_exit_code': 1}]}]}, json.loads(output))

    def test_capture_paths_match_literal_verifier_paths(self):
        _, prefix = archive.capture_name('run-1', 'C0', 'verify', 'run-1-c0-r1-verify', 'verifier-uid', 0, 1)
        self.assertEqual('run-1/C0/r1/consumed-metadata/verify-run-1-c0-r1-verify-verifier-uid-w0000/capture.json', prefix + 'capture.json')
        _, sweep = archive.capture_name('run-1', 'C0', 'b10-sweep', 'run-1-c0-b10-sweep', 'verifier-uid', 9, None)
        self.assertEqual('run-1/C0/consumed-metadata/b10-sweep-run-1-c0-b10-sweep-verifier-uid-w0009/capture.json', sweep + 'capture.json')
        _, final = archive.capture_name('run-1', 'C1', 'b10-final-sweep', 'run-1-c1-b10-final-sweep', 'verifier-uid', 0, None)
        self.assertEqual('run-1/C1/consumed-metadata/b10-final-sweep-run-1-c1-b10-final-sweep-verifier-uid-w0000/capture.json', final + 'capture.json')


class PublicationInventoryTests(unittest.TestCase):
    def test_index_binds_fixed_names_and_hashes(self):
        import json
        names = ('ledger.jsonl', 'manifest.json', 'statistics.jsonl', 'request-lifecycle.jsonl', 'capabilities.json')
        identity = {'run_id': 'run-1'}
        value = {'schema_version': 1, 'identity': identity, 'worker_status': 'complete', 'bookkeeping_complete': True,
                 'worker_complete': True, 'publication_complete': True,
                 'artifacts': [{'name': name, 'length': n, 'sha256': 'a' * 64} for n, name in enumerate(names)]}
        result = archive.publication_inventory(json.dumps(value).encode(), 'run-1/C0/r1/producer/pod/publication/', identity)
        self.assertEqual(['run-1/C0/r1/producer/pod/publication/' + name for name in names], [x['name'] for x in result])
        self.assertEqual([0, 1, 2, 3, 4], [x['bytes'] for x in result])
        value['artifacts'][0]['name'] = '../private'
        with self.assertRaises(archive.InvalidArchiveConfig):
            archive.publication_inventory(json.dumps(value).encode(), 'run-1/C0/r1/producer/pod/publication/', identity)


class ContextTests(unittest.TestCase):
    def test_context_uses_frozen_config_and_lifecycle_source(self):
        import json
        import tempfile
        with tempfile.TemporaryDirectory() as temp:
            private, artifacts = Path(temp) / 'private', Path(temp) / 'artifacts'
            private.mkdir(); artifacts.mkdir()
            identity = {'run_id': 'a01-42-1', 'phase': 'C0', 'source_commit': 'a' * 40}
            (private / 'phase.json').write_text(json.dumps({**identity, 'evidence_archive': VALID}))
            (private / 'ephemeral-output.json').write_text(json.dumps({'blob': {'value': {
                'account_url': 'https://source123.blob.core.windows.net/', 'containers': {'ledger': 'ledger', 'verifier': 'verifier'}}}}))
            env = {'PARCHWELL_PRIVATE_DIRECTORY': str(private), 'PARCHWELL_ARTIFACT_DIRECTORY': str(artifacts),
                   'PARCHWELL_ARCHIVE_DIRECTORY': str(private / 'archive'),
                   'PARCHWELL_ARCHIVE_MANIFEST_DIRECTORY': str(artifacts / 'archive'),
                   'PARCHWELL_ARCHIVE_MANIFEST_PATH': str(artifacts / 'archive/archive-manifest.json'),
                   'PARCHWELL_RUN_ID': 'a01-42-1', 'GITHUB_RUN_ID': '42', 'GITHUB_RUN_ATTEMPT': '1', 'GITHUB_SHA': 'a' * 40}
            result = archive.load_context(env)
            self.assertEqual('source123.blob.core.windows.net', result[5])
            self.assertFalse((private / 'archive').exists())
            with self.assertRaises(archive.InvalidArchiveConfig):
                archive.load_context({**env, 'PARCHWELL_ARCHIVE_DIRECTORY': str(artifacts / 'archive')})
            from unittest.mock import patch
            for parent, other in ((private, artifacts), (artifacts, private)):
                for kind in ('symlink', 'file'):
                    child = parent / 'archive'
                    if kind == 'symlink': child.symlink_to(Path(temp), target_is_directory=True)
                    else: child.write_text('not a directory')
                    with self.subTest(parent=parent.name, kind=kind), patch.object(archive._common, 'preflight') as preflight:
                        self.assertEqual(2, archive.execute(object(), env))
                        preflight.assert_not_called()
                        self.assertFalse((other / 'archive').exists())
                    child.unlink()


class NonledgerInventoryTests(unittest.TestCase):
    def test_selected_idle_candidates_and_missing_job_status(self):
        phase = {'scenario': 'idle-observation', 'run_id': 'idle-42-1', 'run_kind': 'batch', 'items': ['A02', 'A04']}
        objects, gaps = archive.fixed_nonledger_objects(phase, [])
        self.assertEqual(['idle-42-1/idle-observation/A02/judge/candidate.json',
                          'idle-42-1/idle-observation/A04/judge/candidate.json',
                          'idle-42-1/idle-observation/judge-completion.json'], [item['name'] for item in objects])
        self.assertEqual(3, len(gaps))
        self.assertTrue(all(item['state'] == 'not_started' for item in objects))

    def test_v2_authentication_smoke_uses_v2_judge_names(self):
        phase = {'scenario': 'probe-batch-2', 'run_id': 'probe-42-1', 'run_kind': 'smoke',
                 'items': ['F06'], 'capture_authentication_metadata': True}
        records = [{'mode': 'single-action-v2-judge', 'item': 'F06', 'configuration': 'C6', 'apply': 0},
                   {'mode': 'single-action-v2-judge', 'item': 'F06', 'configuration': 'C9', 'apply': 0}]
        objects, gaps = archive.fixed_nonledger_objects(phase, records)
        self.assertEqual([], gaps)
        self.assertEqual(['probe-42-1/single-action-v2/F06/C6/candidate.json',
                          'probe-42-1/single-action-v2/F06/C6/judge-completion.json',
                          'probe-42-1/single-action-v2/F06/C9/candidate.json',
                          'probe-42-1/single-action-v2/F06/C9/judge-completion.json'], [item['name'] for item in objects])
        self.assertTrue(all(item['required'] for item in objects))


class LiteralControlTests(unittest.TestCase):
    def test_literal_zero_data_bindings_and_mutations(self):
        import hashlib
        import json
        manifest = b'{"schema_version":1,"kind":"run_evidence_archive","archive_complete":true,"run":{"github_run_id":"42","github_run_attempt":1,"run_id":"a01-42-1","scenario_id":"A01","configuration_id":"C0","phase":"C0","source_commit":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","images":{"loadgen":null,"verifier":null,"probe":null},"run_kind":"smoke","purpose":"diagnostic","scientific_admissible":false},"destination":{"account_host":"archive123.blob.core.windows.net","container":"run-evidence","prefix":"v1/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/42/1/a01-42-1/C0/","retention_days":90},"policy":{"max_bytes":17179869184,"max_object_bytes":268435456,"max_objects":1024,"deadline_seconds":1800,"transfer_passes":1,"metadata_chunk_bytes":134217728,"metadata_row_bytes":1048576},"expected_units":[],"objects":[],"captures":[],"totals":{"expected_objects":0,"verified_objects":0,"verified_bytes":0},"gaps":[],"completion":null}\n'
        marker = b'{"schema_version":1,"kind":"run_evidence_archive_completion","identity":{"github_run_id":"42","github_run_attempt":1,"run_id":"a01-42-1","phase":"C0","source_commit":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"},"manifest":{"name":"v1/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/42/1/a01-42-1/C0/archive-manifest.json","bytes":912,"sha256":"37041710d72f19bc31914711eaf10d78dcb8643e6cda98032ea375f1159471de"},"totals":{"expected_objects":0,"verified_objects":0,"verified_bytes":0},"archive_complete":true}\n'
        self.assertEqual((912, '37041710d72f19bc31914711eaf10d78dcb8643e6cda98032ea375f1159471de'), (len(manifest), hashlib.sha256(manifest).hexdigest()))
        self.assertEqual((500, '660f6115d897491a6116485e329c7a107c3f7238debbbc3bb5a8ff645bbc7461'), (len(marker), hashlib.sha256(marker).hexdigest()))
        identity = json.loads(marker)['identity']
        receipt = {'schema_version': 1, 'kind': 'run_evidence_archive_receipt', 'identity': identity,
                   'archive_complete': True, 'manifest': json.loads(marker)['manifest'],
                   'completion': {'name': 'v1/' + 'a' * 40 + '/42/1/a01-42-1/C0/archive-completion.json',
                                  'bytes': 500, 'sha256': '660f6115d897491a6116485e329c7a107c3f7238debbbc3bb5a8ff645bbc7461'}, 'gaps': []}
        receipt_bytes = json.dumps(receipt, separators=(',', ':')).encode()
        self.assertTrue(archive.verify_controls(manifest, marker, receipt_bytes)['archive_complete'])
        for invalid in (manifest.replace(b'"completion":null', b'"completion":{}'), manifest + b' '):
            with self.assertRaises(archive.InvalidArchiveConfig):
                archive.verify_controls(invalid, marker, receipt_bytes)


class RunIdentityTests(unittest.TestCase):
    def test_supported_phase_and_run_identity_pairs_are_closed(self):
        cases = {
            'A01': ('a01', {'probe': 'C0', 'C0': 'C0', 'C3': 'C3'}),
            'B04': ('b04', {'C0': 'C0', 'C1': 'C1'}),
            'B10': ('b10', {'C0': 'C0', 'C1': 'C1'}),
            'C01': ('c01', {'C0': 'C0', 'C1': 'C1'}),
            'C03': ('c03', {'C0': 'C0', 'C1': 'C1'}),
            'C10': ('c10', {'C0': 'C0', 'C1': 'C1'}),
            'probe-batch': ('probe', {'single-action': None}),
            'probe-batch-2': ('probe', {'single-action': None}),
            'idle-observation': ('idle', {'idle-observation': None}),
            'ledger-batch': ('ledger', {'ledger-batch': None}),
        }
        for scenario, (prefix, phases) in cases.items():
            for selected, configuration in phases.items():
                with self.subTest(scenario=scenario, phase=selected):
                    identity = {'github_run_id': '42', 'github_run_attempt': 1,
                                'run_id': f'{prefix}-42-1', 'phase': selected, 'source_commit': 'a' * 40}
                    phase = {'scenario': scenario, 'phase': selected, 'configuration': configuration}
                    run = archive.manifest_run(phase, identity)
                    self.assertEqual((scenario, configuration or selected),
                                     (run['scenario_id'], run['configuration_id']))
                    for bad_phase, bad_identity in (
                        ({**phase, 'scenario': 'unknown'}, identity),
                        ({**phase, 'phase': 'unsupported'}, {**identity, 'phase': 'unsupported'}),
                        ({**phase, 'phase': 'C3' if 'C3' not in phases else 'C1'},
                         {**identity, 'phase': 'C3' if 'C3' not in phases else 'C1'}),
                        (phase, {**identity, 'phase': 'unsupported'}),
                        (phase, {**identity, 'run_id': 'wrong-42-1'}),
                        (phase, {**identity, 'run_id': f"{'b04' if prefix == 'a01' else 'a01'}-42-1"}),
                        (phase, {**identity, 'run_id': f'{prefix}-42-2'}),
                    ):
                        with self.assertRaises(archive.InvalidArchiveConfig):
                            archive.manifest_run(bad_phase, bad_identity)
                    if configuration is not None:
                        with self.assertRaises(archive.InvalidArchiveConfig):
                            archive.manifest_run({**phase, 'configuration': 'wrong'}, identity)

    def test_b10_diagnostic_trial_is_not_scientifically_admissible(self):
        identity = {'github_run_id': '42', 'github_run_attempt': 1, 'run_id': 'b10d-42-1', 'phase': 'C1', 'source_commit': 'a' * 40}
        phase = {'scenario': 'B10', 'phase': 'C1', 'configuration': 'C1', 'run_kind': 'smoke',
                 'purpose': 'diagnostic_trial', 'b10_abrupt_diagnostic': True}
        run = archive.manifest_run(phase, identity)
        self.assertEqual(('B10', 'C1', 'smoke', 'diagnostic_trial', False),
                         (run['scenario_id'], run['configuration_id'], run['run_kind'], run['purpose'], run['scientific_admissible']))

    def test_batch_uses_frozen_phase_as_aggregate_identifier(self):
        identity = {'github_run_id': '42', 'github_run_attempt': 1, 'run_id': 'probe-42-1', 'phase': 'single-action', 'source_commit': 'a' * 40}
        phase = {'scenario': 'probe-batch', 'phase': 'single-action', 'configuration': None, 'run_kind': 'batch'}
        run = archive.manifest_run(phase, identity)
        self.assertEqual('single-action', run['configuration_id'])
        self.assertNotEqual('C0', run['configuration_id'])


class AbruptOptionalityTests(unittest.TestCase):
    def test_verified_no_kill_distinguishes_optional_successor_from_missing_termination(self):
        phase = {'scenario': 'B10', 'phase': 'C1', 'configuration': 'C1', 'run_id': 'b10d-42-1',
                 'source_commit': 'a' * 40, 'run_kind': 'smoke', 'purpose': 'diagnostic_trial',
                 'b10_abrupt_diagnostic': True}
        class Owner:
            def mode_job(self, phase, mode, repeat): return f"{phase['run_id']}-c1-r{repeat}-{mode}"
            def b10_abrupt(self, *args):
                class Validator:
                    def envelope(self, raw, kind, observation):
                        if raw != b'{"termination":"no_kill"}' or kind != 'termination': raise ValueError('invalid')
                        return {'data': {'termination': 'no_kill'}}
                return Validator()
        class Collector:
            def __init__(self, verified): self.verified, self.gaps = verified, []; self.transfer = type('T', (), {'deadline': float('inf')})()
            def gap(self, code, boundary, name=None, unit=None): self.gaps.append((code, boundary, unit))
            def source(self, *args, **kwargs): pass
            def not_started(self, *args, **kwargs): pass
            def abrupt(self, container, run, repeat, pod, role, diagnostic, validator, *other):
                if not self.verified: return False
                return validator(b'{"termination":"no_kill"}')['data']['termination'] == 'no_kill'
        owner = Owner()
        modes = [{'job': owner.mode_job(phase, mode, 1), 'mode': mode, 'repeat': 1, 'apply': None, 'outcome': 'not_attempted'}
                 for mode in ('start-line', 'loadgen')]
        modes.append({'job': owner.mode_job(phase, 'loadgen-restart', 1), 'mode': 'loadgen-restart', 'repeat': 1, 'apply': 0, 'outcome': 'complete'})
        pods = [{'name': 'first-pod', 'uid': 'first-uid', 'containers': [{'kind': 'containerStatuses', 'name': 'loadgen', 'terminated_exit_code': 0}]}]
        from unittest.mock import patch
        for verified in (True, False):
            collector = Collector(verified)
            with patch.object(archive, 'read_job_pods', return_value=pods):
                units = archive.collect_ordinary(collector, phase, {'modes': modes}, {'ledger': 'ledger', 'verifier': 'verifier'}, Path('/unused'), owner.mode_job)
            self.assertEqual('not_started', next(u['state'] for u in units if u['mode'] == 'loadgen-successor'))
            self.assertEqual('not_started', next(u['state'] for u in units if u['mode'] == 'b10-abrupt-verify'))
            missing = [unit for code, boundary, unit in collector.gaps if boundary == 'missing job status']
            self.assertEqual([] if verified else ['b10d-42-1-c1-r1-loadgen-successor', 'b10d-42-1-c1-r1-b10-abrupt-verify'], missing)


class NonledgerNotStartedTests(unittest.TestCase):
    def test_explicit_unstarted_selected_work_retains_gap(self):
        spec = importlib.util.spec_from_file_location('adapter', Path(__file__).resolve().parents[1] / 'archive_nonledger.py')
        adapter = importlib.util.module_from_spec(spec); spec.loader.exec_module(adapter)
        class Fake:
            def __init__(self): self.gaps = []
            def gap(self, code, boundary, name=None, unit=None): self.gaps.append((code, boundary, unit))
            def not_started(self, *args, **kwargs): pass
        fake = Fake()
        phase = {'scenario': 'idle-observation', 'run_id': 'idle-42-1', 'run_kind': 'batch', 'items': ['A02']}
        record = {'job': 'idle-42-1-a02-c0-idle', 'mode': 'idle-observation', 'item': 'A02', 'configuration': 'C0',
                  'repeat': None, 'apply': None, 'outcome': 'not_attempted'}
        units = adapter.collect(fake, phase, {'modes': [record]}, {'ledger': 'ledger', 'verifier': 'verifier'}, Path('/unused'))
        self.assertEqual('not_started', units[0]['state'])
        self.assertIn(('monitoring_incomplete', 'required job not started', record['job']), fake.gaps)

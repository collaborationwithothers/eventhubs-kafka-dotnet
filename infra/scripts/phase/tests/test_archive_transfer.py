"""Synthetic transfer tests: conditional writes and independent readback."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('archive', Path(__file__).resolve().parents[1] / 'archive.py')
archive = importlib.util.module_from_spec(spec)
spec.loader.exec_module(archive)


class TransferTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.calls = []
        self.blobs = {('source', 'ledger', 'run/a'): b'abc'}
        self.corrupt = False
        self.transfer = archive.Transfer('source', 'dest', self.tmp.name, float('inf'), self.command)

    def command(self, args, deadline):
        self.calls.append(args)
        def opt(name): return args[args.index(name) + 1]
        account, container, name, action = opt('--account-name'), opt('--container-name'), opt('--name'), args[3]
        key = (account, container, name)
        if action == 'show':
            if key not in self.blobs: return 1, b'', b'BlobNotFound'
            return 0, json.dumps({'properties': {'contentLength': len(self.blobs[key]), 'etag': 'etag'}}).encode(), b''
        if action == 'upload':
            self.assertEqual('*', opt('--if-none-match'))
            self.assertEqual('false', opt('--overwrite'))
            if key in self.blobs: return 1, b'', b'BlobAlreadyExists'
            self.blobs[key] = Path(opt('--file')).read_bytes()
        else:
            self.assertEqual('etag', opt('--if-match'))
            self.assertEqual('false', opt('--overwrite'))
            self.assertEqual('1', opt('--max-connections'))
            data = self.blobs[key]
            if account == 'dest' and self.corrupt: data = b'xyz'
            Path(opt('--file')).write_bytes(data)
        return 0, b'', b''

    @patch.object(archive.shutil, 'disk_usage')
    def test_create_then_readback_and_existing(self, disk):
        disk.return_value.free = 2 * archive.ArchiveMaxObjectBytes + 1048576 + 65536
        actual = self.transfer.copy('ledger', 'run/a', 'v1/a', 3)
        self.assertEqual((3, 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad', 'etag', 'etag'), actual)
        self.assertEqual(['show', 'download', 'show', 'upload', 'show', 'download'], [c[3] for c in self.calls])
        self.calls.clear()
        self.assertEqual(actual, self.transfer.copy('ledger', 'run/a', 'v1/a', 3))
        self.assertEqual(['show', 'download', 'show', 'download'], [c[3] for c in self.calls])
        self.assertEqual([], list(Path(self.tmp.name).iterdir()))

    @patch.object(archive.shutil, 'disk_usage')
    def test_corruption_and_missing_fail_closed(self, disk):
        disk.return_value.free = 2 * archive.ArchiveMaxObjectBytes + 1048576 + 65536
        self.corrupt = True
        with self.assertRaises(archive.Gap) as error:
            self.transfer.copy('ledger', 'run/a', 'v1/a', 3)
        self.assertEqual('destination_hash_mismatch', error.exception.code)
        self.assertEqual([], list(Path(self.tmp.name).iterdir()))
        with self.assertRaises(archive.Gap) as error:
            self.transfer.copy('ledger', 'absent', 'v1/b', 3)
        self.assertEqual('source_missing', error.exception.code)

    def test_control_readback_fails_without_reference(self):
        self.corrupt = True
        with self.assertRaises(archive.Gap) as error:
            self.transfer.write_control('v1/a/archive-manifest.json', b'abc', 1048576)
        self.assertEqual('destination_hash_mismatch', error.exception.code)
        self.assertEqual([], list(Path(self.tmp.name).iterdir()))

    @patch.object(archive.shutil, 'disk_usage')
    def test_runner_snapshot_is_verified_and_cleaned(self, disk):
        disk.return_value.free = 2 * archive.ArchiveMaxObjectBytes + 1048576 + 65536
        with tempfile.TemporaryDirectory() as other:
            source = Path(other) / 'phase-status.json'
            source.write_bytes(b'abc')
            size, digest, etag = self.transfer.copy_local(source, 'v1/a/runner/phase-status.json', 3)
            self.assertEqual((3, 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad', 'etag'), (size, digest, etag))
            self.assertEqual([], list(Path(self.tmp.name).iterdir()))

    def test_marker_failure_keeps_verified_canonical_and_false_receipt(self):
        import hashlib
        identity = {'github_run_id': '42', 'github_run_attempt': 1, 'run_id': 'a01-42-1', 'phase': 'C0', 'source_commit': 'a' * 40}
        prefix = 'v1/' + 'a' * 40 + '/42/1/a01-42-1/C0/'
        manifest = {'schema_version': 1, 'kind': 'run_evidence_archive', 'archive_complete': True,
                    'run': {**identity, 'scenario_id': 'A01', 'configuration_id': 'C0',
                            'images': {'loadgen': None, 'verifier': None, 'probe': None},
                            'run_kind': 'smoke', 'purpose': 'diagnostic', 'scientific_admissible': False},
                    'destination': {'account_host': 'archive123.blob.core.windows.net', 'container': 'run-evidence',
                                    'prefix': prefix, 'retention_days': 90}, 'policy': archive._common.POLICY,
                    'expected_units': [], 'objects': [], 'captures': [], 'completion': None, 'gaps': [],
                    'totals': {'expected_objects': 0, 'verified_objects': 0, 'verified_bytes': 0}}
        class Controls:
            fail = True
            deadline = float('inf')
            calls = []
            def write_control(self, name, data, cap):
                self.calls.append(name)
                if self.fail and name.endswith('archive-completion.json'):
                    raise archive.Gap('transport', 'completion readback')
                return {'name': name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}
        controls = Controls()
        receipt = archive.publish_controls(controls, manifest, identity, self.tmp.name, prefix)
        self.assertFalse(receipt['archive_complete'])
        self.assertIsNone(receipt['completion'])
        self.assertIsNotNone(receipt['manifest'])
        self.assertEqual('transport', receipt['gaps'][0]['code'])
        self.assertTrue(json.loads((Path(self.tmp.name) / 'archive-manifest.json').read_bytes())['archive_complete'])
        self.assertEqual(receipt, json.loads((Path(self.tmp.name) / 'archive-receipt.json').read_bytes()))
        canonical = (Path(self.tmp.name) / 'archive-manifest.json').read_bytes()
        controls.fail = False; controls.calls.clear()
        recovered = archive.publish_controls(controls, manifest, identity, self.tmp.name, prefix)
        self.assertTrue(recovered['archive_complete'])
        self.assertEqual(canonical, (Path(self.tmp.name) / 'archive-manifest.json').read_bytes())
        self.assertEqual(['archive-manifest.json', 'archive-completion.json'], [name.rsplit('/', 1)[-1] for name in controls.calls])

    @patch.object(archive.shutil, 'disk_usage')
    def test_source_is_inspected_in_same_single_download(self, disk):
        disk.return_value.free = 2 * archive.ArchiveMaxObjectBytes + 1048576 + 65536
        observed = []
        self.transfer.copy('ledger', 'run/a', 'v1/a', 3, inspect_source=lambda path, length: observed.append((path.read_bytes(), length)))
        self.assertEqual([(b'abc', 3)], observed)
        self.assertEqual(2, sum(c[3] == 'download' for c in self.calls))

    @patch.object(archive.shutil, 'disk_usage')
    def test_collector_preserves_missing_and_counts_verified_data(self, disk):
        disk.return_value.free = 2 * archive.ArchiveMaxObjectBytes + 1048576 + 65536
        collector = archive.Collector(self.transfer, 'v1/a/')
        collector.source('ledger', 'run/a', 'ledger')
        collector.source('ledger', 'run/missing', 'ledger')
        self.assertEqual({'expected_objects': 2, 'verified_objects': 1, 'verified_bytes': 3}, collector.totals())
        self.assertEqual('missing', collector.objects[1]['state'])
        self.assertEqual('source_missing', collector.gaps[0]['code'])
        self.assertEqual(3, collector.logical_bytes)

    @patch.object(archive.shutil, 'disk_usage')
    def test_zero_row_capture_is_complete_and_missing_is_not(self, disk):
        disk.return_value.free = 2 * archive.ArchiveMaxObjectBytes + 1048576 + 65536
        identity = {'run_id': 'run-1', 'phase': 'C0', 'scenario_id': 'A01', 'configuration_id': 'C0',
                    'job_name': 'job', 'pod_uid': 'pod', 'source_commit': 'a' * 40}
        capture_id = 'verify-job-pod-w0000'
        prefix = 'run-1/C0/r1/consumed-metadata/' + capture_id + '/'
        manifest = {'schema_version': 1, 'capture_id': capture_id, 'identity': identity,
                    'window': {'index': 0, 'kind': 'ordinary', 'repeat': 1, 'topic': 'events', 'start': [], 'end': []},
                    'chunks': [], 'observed_rows': 0, 'captured_rows': 0, 'complete': True,
                    'first_missing_ordinal': None, 'gaps': []}
        self.blobs[('source', 'verifier', prefix + 'capture.json')] = json.dumps(manifest).encode()
        collector = archive.Collector(self.transfer, 'v1/a/')
        collector.capture('verifier', prefix, identity, 'verify', 1, capture_id)
        self.assertTrue(collector.captures[0]['complete'])
        self.assertEqual(0, collector.captures[0]['rows'])
        self.assertEqual([], collector.gaps)
        collector.capture('verifier', prefix + 'absent/', identity, 'verify', 1, 'verify-job-pod-w0001')
        self.assertFalse(collector.captures[1]['complete'])
        self.assertEqual('capture_missing', collector.gaps[0]['code'])

    @patch.object(archive.shutil, 'disk_usage')
    def test_missing_publication_index_is_not_replaced_by_listing(self, disk):
        disk.return_value.free = 2 * archive.ArchiveMaxObjectBytes + 1048576 + 65536
        prefix = 'run/a/publication/'
        self.blobs[('source', 'ledger', prefix + 'ledger.jsonl')] = b'abc'
        collector = archive.Collector(self.transfer, 'v1/a/')
        collector.publication('ledger', prefix, {'run_id': 'run'}, 1)
        self.assertEqual('source_missing', collector.gaps[0]['code'])
        self.assertEqual('missing', collector.objects[0]['state'])
        self.assertEqual('verified', collector.objects[1]['state'])
        self.assertEqual(6, len(collector.objects))

    def test_prepared_interrupted_phase_publishes_incomplete_receipt(self):
        import hashlib
        from unittest.mock import patch
        with tempfile.TemporaryDirectory() as root:
            private, artifacts = Path(root) / 'private', Path(root) / 'artifacts'
            private.mkdir(); artifacts.mkdir()
            rid = '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/archive123'
            config = {'storage_account_id': rid, 'account_name': 'archive123', 'blob_service_uri': 'https://archive123.blob.core.windows.net/',
                      'container_name': 'run-evidence', 'container_id': rid + '/blobServices/default/containers/run-evidence', 'retention_days': 90}
            phase = {'scenario': 'A01', 'phase': 'C0', 'configuration': 'C0', 'run_id': 'a01-42-1', 'source_commit': 'a' * 40, 'evidence_archive': config,
                     'images': {worker: {'digest': 'sha256:' + 'b' * 64,
                               'reference': 'archive123.azurecr.io/parchwell/' + worker + '@sha256:' + 'b' * 64}
                                for worker in ('loadgen', 'verifier')}}
            (private / 'phase.json').write_text(json.dumps(phase))
            (private / 'ephemeral-output.json').write_text(json.dumps({'blob': {'value': {'account_url': 'https://source123.blob.core.windows.net/', 'containers': {'ledger': 'ledger', 'verifier': 'verifier'}}}}))
            science = {'candidate': {'accepted': False, 'outcome': 'failed'}, 'full_allowed': False, 'modes': []}
            (artifacts / 'phase-status.json').write_text(json.dumps({'run_id': 'a01-42-1', 'phase': 'C0', **science}))
            env = {'PARCHWELL_PRIVATE_DIRECTORY': str(private), 'PARCHWELL_ARTIFACT_DIRECTORY': str(artifacts),
                   'PARCHWELL_ARCHIVE_DIRECTORY': str(private / 'archive'), 'PARCHWELL_ARCHIVE_MANIFEST_DIRECTORY': str(artifacts / 'archive'),
                   'PARCHWELL_ARCHIVE_MANIFEST_PATH': str(artifacts / 'archive/archive-manifest.json'), 'PARCHWELL_RUN_ID': 'a01-42-1',
                   'GITHUB_RUN_ID': '42', 'GITHUB_RUN_ATTEMPT': '1', 'GITHUB_SHA': 'a' * 40}
            class Controls:
                writes = []
                def __init__(self, source, dest, spool, deadline): self.deadline = deadline; self.spool = spool; self.source_account = source
                def write_control(self, name, data, cap):
                    self.writes.append(name)
                    return {'name': name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}
                def copy_local(self, path, name, remaining, reserve=None):
                    data = path.read_bytes()
                    if reserve is not None: reserve(len(data))
                    return len(data), hashlib.sha256(data).hexdigest(), 'etag'
                def properties(self, *args, **kwargs): return None
                def copy(self, container, name, destination, remaining, **kwargs):
                    data = b'abc'
                    parts = name.split('/')
                    if name.endswith('/registration.json'):
                        pod, repeat = parts[4], int(parts[2][1:])
                        job = pod[:-4]
                        identity = {'scenario_id': 'A01', 'configuration_id': 'C0', 'run_id': 'a01-42-1',
                                    'instance_id': pod, 'expected_instance_id': pod, 'client_id': pod}
                        data = json.dumps({'identity': identity, 'phase': 'C0', 'repeat_number': repeat,
                                           'job_name': job, 'pod_name': pod, 'pod_uid': 'uid', 'source_commit': 'a' * 40}).encode()
                    elif name.endswith('/publication/index.json'):
                        pod = parts[4]
                        identity = {'scenario_id': 'A01', 'configuration_id': 'C0', 'run_id': 'a01-42-1',
                                    'instance_id': pod, 'expected_instance_id': pod, 'client_id': pod}
                        data = json.dumps({'schema_version': 1, 'identity': identity, 'worker_status': 'complete',
                                           'bookkeeping_complete': True, 'worker_complete': True, 'publication_complete': True,
                                           'artifacts': [{'name': leaf, 'length': 3, 'sha256': hashlib.sha256(b'abc').hexdigest()}
                                                         for leaf in ('ledger.jsonl','manifest.json','statistics.jsonl','request-lifecycle.jsonl','capabilities.json')]}).encode()
                    elif name.endswith('/capture.json'):
                        capture_id, repeat = parts[-2], int(parts[2][1:])
                        job = capture_id[len('verify-'):-len('-uid-w0000')]
                        identity = {'run_id': 'a01-42-1', 'phase': 'C0', 'scenario_id': 'A01', 'configuration_id': 'C0',
                                    'job_name': job, 'pod_uid': 'uid', 'source_commit': 'a' * 40}
                        data = json.dumps({'schema_version': 1, 'capture_id': capture_id, 'identity': identity,
                                           'window': {'index': 0, 'kind': 'ordinary', 'repeat': repeat, 'topic': 'events', 'start': [], 'end': []},
                                           'chunks': [], 'observed_rows': 0, 'captured_rows': 0, 'complete': True,
                                           'first_missing_ordinal': None, 'gaps': []}).encode()
                    if kwargs.get('expected_length') is not None: assert kwargs['expected_length'] == len(data)
                    if kwargs.get('expected_sha256') is not None: assert kwargs['expected_sha256'] == hashlib.sha256(data).hexdigest()
                    if kwargs.get('reserve'): kwargs['reserve'](len(data))
                    if kwargs.get('inspect_source'):
                        path = self.spool / 'inspect'; path.write_bytes(data)
                        try: kwargs['inspect_source'](path, len(data))
                        finally: path.unlink()
                    return len(data), hashlib.sha256(data).hexdigest(), 'source-etag', 'dest-etag'
            class Phase:
                def state(self): return json.loads((artifacts / 'phase-status.json').read_bytes())
                def save_state(self, state): (artifacts / 'phase-status.json').write_text(json.dumps(state))
                def mode_job(self, phase, mode, repeat): return f"{phase['run_id']}-{phase['phase'].lower()}" + (f'-r{repeat}' if repeat else '') + f'-{mode}'
            import os
            phase_spec = importlib.util.spec_from_file_location('phase_entry', Path(__file__).resolve().parents[1] / 'phase.py')
            phase_module = importlib.util.module_from_spec(phase_spec); phase_spec.loader.exec_module(phase_module)
            with patch.dict(os.environ, env), patch.object(phase_module.Phase, 'archive_module', return_value=archive), \
                    patch.object(archive._common, 'preflight'), patch.object(archive, 'Transfer', Controls):
                self.assertEqual(1, phase_module.main(['archive']))
            manifest = json.loads((artifacts / 'archive/archive-manifest.json').read_bytes())
            receipt = json.loads((artifacts / 'archive/archive-receipt.json').read_bytes())
            self.assertFalse(receipt['archive_complete'])
            status = json.loads((artifacts / 'phase-status.json').read_bytes())
            self.assertFalse(status['archive']['archive_complete'])
            self.assertEqual(science, {key: status[key] for key in science})
            self.assertIsNotNone(receipt['manifest'])
            self.assertIsNone(receipt['completion'])
            self.assertEqual(16, len(manifest['expected_units']))
            self.assertTrue(all(gap['code'] == 'monitoring_incomplete' for gap in manifest['gaps']))
            import shutil
            shutil.rmtree(artifacts / 'archive')
            shutil.rmtree(private / 'archive')
            records = [{'job': Phase().mode_job(phase, mode, repeat), 'repeat': repeat, 'mode': mode,
                        'outcome': 'complete', 'apply': 0} for repeat in range(1, 6)
                       for mode in ('start-line', 'loadgen', 'verify')]
            records.append({'job': Phase().mode_job(phase, 'judge', None), 'repeat': None,
                            'mode': 'judge', 'outcome': 'complete', 'apply': 0})
            for record in records:
                directory = artifacts / record['job']; directory.mkdir()
                (directory / 'pods.json').write_text(json.dumps({'items': [{'metadata': {'name': record['job'] + '-pod', 'uid': 'uid'}, 'status': {'phase': 'Succeeded', 'containerStatuses': []}}]}))
            (artifacts / 'phase-status.json').write_text(json.dumps({'run_id': 'a01-42-1', 'phase': 'C0',
                                                                     'candidate': science['candidate'], 'full_allowed': False, 'modes': records}))
            Controls.writes.clear()
            with patch.dict(os.environ, env), patch.object(phase_module.Phase, 'archive_module', return_value=archive), \
                    patch.object(archive._common, 'preflight'), patch.object(archive, 'Transfer', Controls):
                self.assertEqual(0, phase_module.main(['archive']))
            receipt = json.loads((artifacts / 'archive/archive-receipt.json').read_bytes())
            self.assertTrue(receipt['archive_complete'])
            self.assertEqual(['archive-manifest.json', 'archive-completion.json'], [name.rsplit('/', 1)[-1] for name in Controls.writes])
            status = json.loads((artifacts / 'phase-status.json').read_bytes())
            self.assertEqual(science['candidate'], status['candidate'])
            self.assertFalse(status['full_allowed'])
            self.assertEqual(records, status['modes'])
            shutil.rmtree(artifacts / 'archive'); shutil.rmtree(private / 'archive')
            (private / 'ephemeral-output.json').unlink()
            with patch.dict(os.environ, env), patch.object(phase_module.Phase, 'archive_module', return_value=archive), \
                    patch.object(archive._common, 'preflight') as preflight:
                self.assertEqual(1, phase_module.main(['archive']))
                preflight.assert_not_called()
            receipt = json.loads((artifacts / 'archive/archive-receipt.json').read_bytes())
            self.assertFalse(receipt['archive_complete'])
            self.assertIsNone(receipt['manifest'])
            self.assertEqual('transport', receipt['gaps'][0]['code'])
            shutil.rmtree(artifacts / 'archive'); shutil.rmtree(private / 'archive')
            (private / 'ephemeral-output.json').write_text(json.dumps({'blob': {'value': {'account_url': 'https://source123.blob.core.windows.net/', 'containers': {'ledger': 'ledger', 'verifier': 'verifier'}}}}))
            with patch.dict(os.environ, env), patch.object(phase_module.Phase, 'archive_module', return_value=archive), \
                    patch.object(archive._common, 'preflight', side_effect=archive._common.Invalid):
                self.assertEqual(2, phase_module.main(['archive']))
            receipt = json.loads((artifacts / 'archive/archive-receipt.json').read_bytes())
            self.assertEqual('config_invalid', receipt['gaps'][0]['code'])
            shutil.rmtree(artifacts / 'archive'); shutil.rmtree(private / 'archive')
            with patch.dict(os.environ, env), patch.object(phase_module.Phase, 'archive_module', return_value=archive), \
                    patch.object(archive._common, 'preflight'), patch.object(archive, 'Transfer', side_effect=OSError('private sentinel')):
                self.assertEqual(1, phase_module.main(['archive']))
            receipt = json.loads((artifacts / 'archive/archive-receipt.json').read_bytes())
            self.assertFalse(receipt['archive_complete'])
            self.assertNotIn('private sentinel', json.dumps(receipt))
            self.assertEqual(science['candidate'], json.loads((artifacts / 'phase-status.json').read_bytes())['candidate'])
            shutil.rmtree(artifacts / 'archive'); shutil.rmtree(private / 'archive')
            phase['evidence_archive']['retention_days'] = True
            (private / 'phase.json').write_text(json.dumps(phase))
            with patch.dict(os.environ, env), patch.object(phase_module.Phase, 'archive_module', return_value=archive), \
                    patch.object(archive._common, 'preflight') as preflight:
                self.assertEqual(2, phase_module.main(['archive']))
                preflight.assert_not_called()
            self.assertFalse((artifacts / 'archive').exists())
            self.assertFalse((private / 'archive').exists())

    def test_actual_cli_wrapper_transient_failure_is_not_retried(self):
        import os
        import time
        session = Path(self.tmp.name) / 'session'; session.mkdir(mode=0o700)
        (session / 'freshness.json').write_text(json.dumps({'request_started_monotonic': time.monotonic(), 'assertion_exp': int(time.time()) + 600}))
        calls = Path(self.tmp.name) / 'calls'
        fake = Path(self.tmp.name) / 'az-real'
        fake.write_text('#!/usr/bin/python3\nimport sys\nopen(' + repr(str(calls)) + ', "a").write("called\\n")\nsys.stderr.write("transient failure\\n")\nsys.exit(1)\n')
        fake.chmod(0o700)
        wrapper_dir = Path(__file__).resolve().parents[4] / 'scripts/azure-cli-session'
        uid = '11111111-1111-1111-1111-111111111111'
        env = {'PATH': str(wrapper_dir) + ':' + os.environ['PATH'], 'GITHUB_ACTIONS': 'true', 'GITHUB_REF': 'refs/heads/main',
               'PARCHWELL_REAL_AZ': str(fake), 'PARCHWELL_AZ_SESSION_DIRECTORY': str(session),
               'AZURE_CLIENT_ID': uid, 'AZURE_TENANT_ID': uid, 'AZURE_SUBSCRIPTION_ID': uid}
        spool = Path(self.tmp.name) / 'spool'; spool.mkdir()
        transfer = archive.Transfer('source', 'dest', spool, time.monotonic() + 30)
        with patch.dict(os.environ, env), patch.object(archive.shutil, 'disk_usage') as disk:
            disk.return_value.free = 2 * archive.ArchiveMaxObjectBytes + 1048576 + 65536
            with self.assertRaises(archive.Gap) as error:
                transfer.copy('ledger', 'run/a', 'v1/a', 3)
        self.assertEqual('transport', error.exception.code)
        self.assertEqual(['called'], calls.read_text().splitlines())
        self.assertEqual([], list(spool.iterdir()))

    def test_control_reserved_object_limit_and_no_duplicate_accounting(self):
        collector = archive.Collector(self.transfer, 'v1/a/')
        for index in range(1022):
            collector.not_started('ledger', f'run/object-{index}', 'object')
        self.assertEqual(1022, len(collector.objects))
        collector.not_started('ledger', 'run/overflow', 'object')
        self.assertEqual(1022, len(collector.objects))
        self.assertEqual('object_limit', collector.gaps[0]['code'])
        self.assertEqual({'expected_objects': 0, 'verified_objects': 0, 'verified_bytes': 0}, collector.totals())

    @patch.object(archive.shutil, 'disk_usage')
    def test_descriptor_and_phase_byte_limits_before_download(self, disk):
        disk.return_value.free = 2 * archive.ArchiveMaxObjectBytes + 1048576 + 65536
        with self.assertRaises(archive.Gap) as error:
            self.transfer.copy('ledger', 'run/a', 'v1/a', 2)
        self.assertEqual('byte_limit', error.exception.code)
        self.assertEqual(['show'], [call[3] for call in self.calls])
        self.calls.clear()
        with self.assertRaises(archive.Gap) as error:
            self.transfer.copy('ledger', 'run/a', 'v1/a', 3, expected_length=2)
        self.assertEqual('source_length_mismatch', error.exception.code)
        self.assertEqual(['show', 'download', 'show', 'upload', 'show', 'download'], [call[3] for call in self.calls])
        self.assertEqual(b'abc', self.blobs[('dest', 'run-evidence', 'v1/a')])
        self.assertEqual((3, 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad', 'etag', 'etag'), error.exception.retained)

    def test_runner_interruption_keeps_valid_failed_object(self):
        class Interrupted:
            def copy_local(self, *args, **kwargs): raise KeyboardInterrupt()
        collector = archive.Collector(Interrupted(), 'v1/a/')
        with self.assertRaises(KeyboardInterrupt):
            collector.runner('phase-status.json', Path(self.tmp.name) / 'unused', 'phase_status')
        self.assertEqual('failed', collector.objects[0]['state'])
        self.assertEqual('interrupted', collector.objects[0]['gap_code'])
        self.assertEqual('interrupted', collector.gaps[0]['code'])
        self.assertEqual({'expected_objects': 1, 'verified_objects': 0, 'verified_bytes': 0}, collector.totals())

    @patch.object(archive.shutil, 'disk_usage')
    def test_mismatched_source_hash_is_retained_without_certification(self, disk):
        disk.return_value.free = 2 * archive.ArchiveMaxObjectBytes + 1048576 + 65536
        with self.assertRaises(archive.Gap) as error:
            self.transfer.copy('ledger', 'run/a', 'v1/a', 3, expected_sha256='0' * 64)
        self.assertEqual('source_hash_mismatch', error.exception.code)
        self.assertEqual(b'abc', self.blobs[('dest', 'run-evidence', 'v1/a')])
        self.assertEqual([], list(Path(self.tmp.name).iterdir()))

    @patch.object(archive.shutil, 'disk_usage')
    def test_length_mismatch_retains_actual_metadata_but_never_counts_verified(self, disk):
        disk.return_value.free = 2 * archive.ArchiveMaxObjectBytes + 1048576 + 65536
        collector = archive.Collector(self.transfer, 'v1/a/')
        collector.source('ledger', 'run/a', 'ledger', expected_length=2)
        obj = collector.objects[0]
        self.assertEqual(('failed', 'source_length_mismatch', 3, 'etag', 'etag'),
                         (obj['state'], obj['gap_code'], obj['bytes'], obj['source']['etag'], obj['destination_etag']))
        self.assertEqual('ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad', obj['sha256'])
        self.assertEqual(b'abc', self.blobs[('dest', 'run-evidence', obj['destination_name'])])
        self.assertEqual({'expected_objects': 1, 'verified_objects': 0, 'verified_bytes': 0}, collector.totals())

    @patch.object(archive.shutil, 'disk_usage')
    def test_late_binding_mismatch_removes_verified_count_but_keeps_bytes(self, disk):
        disk.return_value.free = 2 * archive.ArchiveMaxObjectBytes + 1048576 + 65536
        collector = archive.Collector(self.transfer, 'v1/a/')
        obj = collector.source('ledger', 'run/a', 'ledger')
        collector.reject_binding(obj, 'source_hash_mismatch', 'predecessor reference')
        self.assertEqual(('rejected', 'source_hash_mismatch', 3), (obj['state'], obj['gap_code'], obj['bytes']))
        self.assertEqual({'expected_objects': 1, 'verified_objects': 0, 'verified_bytes': 0}, collector.totals())
        self.assertEqual(b'abc', self.blobs[('dest', 'run-evidence', obj['destination_name'])])

    def test_corrupt_b10_journal_is_not_reported_as_missing(self):
        spec = importlib.util.spec_from_file_location('inventory_fixture', Path(__file__).with_name('test_archive_inventory_integration.py'))
        fixture = importlib.util.module_from_spec(spec); spec.loader.exec_module(fixture)
        case = fixture.InventoryIntegrationTests(); case.setUp()
        try:
            harness = case.harness('B10', 'C1', 'b10-42-1', 'scientific')
            first, _, _ = fixture.complete_b10(harness)
            key = ('source123', 'ledger', first + 'journal.jsonl')
            harness.cli.blobs[key] = b'x' * len(harness.cli.blobs[key])
            code, manifest, receipt = harness.run()
            self.assertEqual(1, code)
            self.assertFalse(receipt['archive_complete'])
            journal_gaps = [gap['code'] for gap in manifest['gaps'] if gap['object_name'] == first + 'journal.jsonl']
            self.assertIn('source_hash_mismatch', journal_gaps)
            self.assertNotIn('source_missing', journal_gaps)
        finally:
            case.doCleanups()

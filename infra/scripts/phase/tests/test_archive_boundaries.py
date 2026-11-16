"""Literal offline boundaries for archive controls, accounting and deadlines."""
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import time
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('archive_boundaries', Path(__file__).resolve().parents[1] / 'archive.py')
archive = importlib.util.module_from_spec(spec)
spec.loader.exec_module(archive)

IDENTITY = {'github_run_id': '42', 'github_run_attempt': 1, 'run_id': 'a01-42-1', 'phase': 'C0', 'source_commit': 'a' * 40}
PREFIX = 'v1/' + 'a' * 40 + '/42/1/a01-42-1/C0/'
REF = {'name': PREFIX + 'archive-manifest.json', 'bytes': 3, 'sha256': 'a' * 64}
GAP = {'code': 'transport', 'boundary': 'failure', 'object_name': None, 'expected_unit': None}


def manifest():
    return {'schema_version': 1, 'kind': 'run_evidence_archive', 'archive_complete': True,
            'run': {**IDENTITY, 'scenario_id': 'A01', 'configuration_id': 'C0',
                    'images': {'loadgen': None, 'verifier': None, 'probe': None},
                    'run_kind': 'smoke', 'purpose': 'diagnostic', 'scientific_admissible': False},
            'destination': {'account_host': 'archive123.blob.core.windows.net', 'container': 'run-evidence',
                            'prefix': PREFIX, 'retention_days': 90}, 'policy': dict(archive._common.POLICY),
            'expected_units': [], 'objects': [], 'captures': [], 'completion': None, 'gaps': [],
            'totals': {'expected_objects': 0, 'verified_objects': 0, 'verified_bytes': 0}}


def receipt():
    return {'schema_version': 1, 'kind': 'run_evidence_archive_receipt', 'identity': dict(IDENTITY),
            'archive_complete': True, 'manifest': dict(REF),
            'completion': {**REF, 'name': PREFIX + 'archive-completion.json'}, 'gaps': []}


def marker():
    return {'schema_version': 1, 'kind': 'run_evidence_archive_completion', 'identity': dict(IDENTITY),
            'manifest': dict(REF), 'totals': {'expected_objects': 0, 'verified_objects': 0, 'verified_bytes': 0},
            'archive_complete': True}


class ControlBoundaries(unittest.TestCase):
    def test_reference_lengths_and_types_in_receipt_and_marker(self):
        for size in (0, 1048575, 1048576):
            for factory, validator in ((receipt, archive.validate_receipt), (marker, archive.validate_marker)):
                value = factory(); value['manifest']['bytes'] = size
                with self.subTest(size=size, validator=validator.__name__): validator(value)
        for size in (-1, 1048577, True, 1.5, '3', None):
            for factory, validator in ((receipt, archive.validate_receipt), (marker, archive.validate_marker)):
                value = factory(); value['manifest']['bytes'] = size
                with self.subTest(size=size, validator=validator.__name__), self.assertRaises(archive.InvalidArchiveConfig): validator(value)
        for size in (0, 65535, 65536):
            value = receipt(); value['completion']['bytes'] = size; archive.validate_receipt(value)
        for size in (-1, 65537, True, None):
            value = receipt(); value['completion']['bytes'] = size
            with self.subTest(size=size), self.assertRaises(archive.InvalidArchiveConfig): archive.validate_receipt(value)

    def test_nested_identity_reference_and_gap_rejections(self):
        for factory, validator in ((receipt, archive.validate_receipt), (marker, archive.validate_marker)):
            for key, bad in [('github_run_id', ''), ('github_run_attempt', True), ('run_id', ''),
                             ('phase', ''), ('source_commit', 'a' * 39), ('source_commit', 'A' * 40)]:
                value = factory(); value['identity'][key] = bad
                with self.subTest(key=key, bad=bad, validator=validator.__name__), self.assertRaises(archive.InvalidArchiveConfig): validator(value)
            for bad in ('', 'a' * 63, 'a' * 65, 'A' * 64, None):
                value = factory(); value['manifest']['sha256'] = bad
                with self.subTest(hash=bad, validator=validator.__name__), self.assertRaises(archive.InvalidArchiveConfig): validator(value)
            for nested in ('identity', 'manifest'):
                value = factory(); value[nested]['unknown'] = 1
                with self.subTest(nested=nested, validator=validator.__name__), self.assertRaises(archive.InvalidArchiveConfig): validator(value)
        for changes in ({'gaps': []}, {'gaps': [dict(GAP, unknown=1)]}, {'gaps': [dict(GAP, code='other')]},
                        {'gaps': [dict(GAP, boundary='é' * 1025)]}, {'gaps': [GAP], 'completion': dict(REF)}):
            value = receipt(); value.update(archive_complete=False, manifest=None, completion=None); value.update(changes)
            with self.subTest(changes=changes), self.assertRaises(archive.InvalidArchiveConfig): archive.validate_receipt(value)
        value = receipt(); value.update(archive_complete=False, manifest=dict(REF), completion=None, gaps=[GAP]); archive.validate_receipt(value)

    def test_strict_json_sizes_duplicates_depth_and_empty(self):
        for limit in (1048576, 65536):
            for size in (limit - 1, limit):
                raw = b'"' + b'a' * (size - 2) + b'"'
                with self.subTest(limit=limit, size=size): self.assertEqual(size - 2, len(archive.strict_control(raw, limit)))
            with self.assertRaises(archive.InvalidArchiveConfig): archive.strict_control(b'"' + b'a' * (limit - 1) + b'"', limit)
        for raw in (b'', b'{"outer":{"x":1,"x":2}}', b'{"x":NaN}', b'[' * 32 + b'0' + b']' * 32):
            with self.subTest(raw=raw[:40]), self.assertRaises(archive.InvalidArchiveConfig): archive.strict_control(raw, 1048576)
        self.assertEqual([], archive.strict_control(b'[]', 1048576))

    def test_totals_strict_boundaries_and_precedence(self):
        for count in (0, 1021, 1022):
            value = marker(); value['totals'].update(expected_objects=count, verified_objects=count); archive.validate_marker(value)
        for count in (1023, True, -1):
            value = marker(); value['totals'].update(expected_objects=count, verified_objects=count)
            with self.subTest(count=count), self.assertRaises(archive.InvalidArchiveConfig): archive.validate_marker(value)
        for size in (0, 17179869183, 17179869184):
            value = marker(); value['totals']['verified_bytes'] = size; archive.validate_marker(value)
        for size in (17179869185, True, -1):
            value = marker(); value['totals']['verified_bytes'] = size
            with self.subTest(size=size), self.assertRaises(archive.InvalidArchiveConfig): archive.validate_marker(value)
        value = marker(); value['totals'].update(expected_objects=1, verified_objects=0)
        with self.assertRaises(archive.InvalidArchiveConfig): archive.validate_marker(value)

    def test_manifest_nested_unknown_and_complete_consistency(self):
        archive.validate_manifest(manifest())
        for path, key, bad in [(('run',), 'github_run_attempt', True), (('destination',), 'retention_days', True),
                               (('run', 'images'), 'extra', None), (('destination',), 'extra', 1),
                               ((), 'gaps', [GAP]), ((), 'completion', dict(REF))]:
            value = manifest(); target = value
            for part in path: target = target[part]
            target[key] = bad
            with self.subTest(path=path, key=key), self.assertRaises(archive.InvalidArchiveConfig): archive.validate_manifest(value)


class TransferBoundaries(unittest.TestCase):
    def test_object_limit_and_precedence_with_metadata_only(self):
        with tempfile.TemporaryDirectory() as root, patch.object(archive.shutil, 'disk_usage') as disk:
            disk.return_value.free = 2 * 268435456 + 1048576 + 65536
            transfer = archive.Transfer('source', 'dest', root, float('inf'))
            for size in (268435455, 268435456, 268435457):
                with self.subTest(size=size), patch.object(transfer, 'download', side_effect=archive.Gap('transport', 'download')) as download:
                    with self.assertRaises(archive.Gap) as error:
                        transfer.copy('ledger', 'run/a', 'v1/a', 268435456, source_properties=(size, 'etag'))
                    self.assertEqual('source_too_large' if size > 268435456 else 'transport', error.exception.code)
                    self.assertEqual(size <= 268435456, download.called)
            with self.assertRaises(archive.Gap) as error:
                transfer.copy('ledger', 'run/a', 'v1/a', 0, source_properties=(268435457, 'etag'))
            self.assertEqual('source_too_large', error.exception.code)

    def test_control_caps_empty_below_at_above(self):
        with tempfile.TemporaryDirectory() as root, patch.object(archive.shutil, 'disk_usage') as disk:
            disk.return_value.free = 2 * 268435456 + 1048576 + 65536
            transfer = archive.Transfer('source', 'dest', root, float('inf'))
            for cap in (1048576, 65536):
                for size in (0, cap - 1, cap, cap + 1):
                    with self.subTest(cap=cap, size=size), patch.object(transfer, 'properties', side_effect=archive.Gap('transport', 'properties')) as prop:
                        with self.assertRaises(archive.Gap) as error: transfer.write_control('v1/control', b'x' * size, cap)
                        self.assertEqual(('manifest_too_large' if cap == 1048576 else 'byte_limit') if size > cap else 'transport', error.exception.code)
                        self.assertEqual(size <= cap, prop.called)

    def test_reserved_phase_bytes_below_at_above(self):
        class Transfer:
            def copy(self, container, name, destination, remaining, **kw):
                size = 3
                if size > remaining: raise archive.Gap('byte_limit', 'phase bytes')
                kw['reserve'](size)
                return size, 'a' * 64, 's', 'd'
        for remaining, accepted in ((2, False), (3, True), (4, True)):
            collector = archive.Collector(Transfer(), 'v1/a/')
            collector.logical_bytes = 17179869184 - 1048576 - 65536 - remaining
            collector.source('ledger', 'run/a', 'ledger')
            with self.subTest(remaining=remaining):
                self.assertEqual('verified' if accepted else 'failed', collector.objects[0]['state'])
                self.assertEqual([] if accepted else ['byte_limit'], [gap['code'] for gap in collector.gaps])
                self.assertEqual(3 if accepted else 0, collector.verified_bytes)

    def test_failure_at_each_control_stage_keeps_canonical(self):
        class Controls:
            deadline = float('inf')
            def __init__(self, fail): self.fail = fail
            def write_control(self, name, data, cap):
                if name.endswith(self.fail): raise archive.Gap('transport', 'injected failure')
                return {'name': name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}
        for failing in ('archive-manifest.json', 'archive-completion.json'):
            with self.subTest(failing=failing), tempfile.TemporaryDirectory() as root:
                original = manifest(); expected = archive._common.encoded(original)
                result = archive.publish_controls(Controls(failing), original, IDENTITY, root, PREFIX)
                self.assertFalse(result['archive_complete']); self.assertIsNone(result['completion'])
                self.assertEqual('transport', result['gaps'][0]['code'])
                self.assertEqual(failing != 'archive-manifest.json', result['manifest'] is not None)
                self.assertEqual(expected, (Path(root) / 'archive-manifest.json').read_bytes())
                self.assertEqual(result, json.loads((Path(root) / 'archive-receipt.json').read_bytes()))

    def test_deadline_through_actual_session_wrapper(self):
        with tempfile.TemporaryDirectory() as root:
            root = Path(root); session = root / 'session'; session.mkdir(mode=0o700)
            now = time.monotonic()
            (session / 'freshness.json').write_text(json.dumps({'request_started_monotonic': now, 'assertion_exp': int(time.time()) + 600}))
            called = root / 'called'; fake = root / 'az-real'
            fake.write_text('#!/usr/bin/python3\nimport os, pathlib, time\npathlib.Path(' + repr(str(called)) + ').write_text(str(os.getpid()))\nprint("ready", flush=True)\ntime.sleep(300)\n')
            fake.chmod(0o700)
            wrapper = Path(__file__).resolve().parents[4] / 'scripts/azure-cli-session'
            uid = '11111111-1111-1111-1111-111111111111'
            env = {'PATH': str(wrapper) + ':' + os.environ['PATH'], 'GITHUB_ACTIONS': 'true', 'GITHUB_REF': 'refs/heads/main',
                   'PARCHWELL_REAL_AZ': str(fake), 'PARCHWELL_AZ_SESSION_DIRECTORY': str(session),
                   'AZURE_CLIENT_ID': uid, 'AZURE_TENANT_ID': uid, 'AZURE_SUBSCRIPTION_ID': uid}
            for initial, advance in ((0, 61), (590, 11)):
                called.unlink(missing_ok=True)
                clock = lambda: now + initial + (advance if called.exists() else 0)
                with self.subTest(initial=initial), patch.dict(os.environ, env), patch.object(archive._common.time, 'monotonic', side_effect=clock):
                    with self.assertRaises(archive.Gap) as error:
                        archive.Transfer('source', 'dest', root, now + 600).properties('source', 'ledger', 'run/a')
                    self.assertEqual(('deadline', 'command timeout'), (error.exception.code, error.exception.boundary))
                    self.assertTrue(called.exists())
                    with self.assertRaises(ProcessLookupError): os.kill(int(called.read_text()), 0)

class InventoryBoundaries(unittest.TestCase):
    def capture_fixture(self):
        identity = {'run_id': 'run-1', 'phase': 'C0', 'scenario_id': 'A01', 'configuration_id': 'C0',
                    'job_name': 'job', 'pod_uid': 'pod', 'source_commit': 'a' * 40}
        prefix = 'run-1/C0/r1/consumed-metadata/verify-job-pod-w0000/'
        capture = {'schema_version': 1, 'capture_id': 'verify-job-pod-w0000', 'identity': identity,
                   'window': {'index': 0, 'kind': 'ordinary', 'repeat': 1, 'topic': 'document-events', 'start': [{'partition': 0, 'offset': 0}], 'end': [{'partition': 0, 'offset': 0}]},
                   'chunks': [{'name': prefix + 'chunk-000001.jsonl', 'bytes': 1, 'sha256': 'a' * 64, 'rows': 1}],
                   'observed_rows': 1, 'captured_rows': 1, 'complete': True, 'first_missing_ordinal': None, 'gaps': []}
        return identity, prefix, capture

    def validate_capture(self, identity, prefix, value):
        return archive.capture_inventory(json.dumps(value).encode(), prefix, identity, 'verify', 1, 'verify-job-pod-w0000')

    def test_chunk_size_rows_and_nested_gaps(self):
        identity, prefix, base = self.capture_fixture()
        for size in (0, 134217727, 134217728):
            value = copy.deepcopy(base); value['chunks'][0]['bytes'] = size
            with self.subTest(size=size): self.validate_capture(identity, prefix, value)
        for size in (-1, 134217729, True, None):
            value = copy.deepcopy(base); value['chunks'][0]['bytes'] = size
            with self.subTest(size=size), self.assertRaises(archive.InvalidArchiveConfig): self.validate_capture(identity, prefix, value)
        for key, bad in [('rows', 0), ('rows', True), ('sha256', 'a' * 63), ('name', prefix + 'chunk-000002.jsonl'), ('extra', 1)]:
            value = copy.deepcopy(base); value['chunks'][0][key] = bad
            with self.subTest(key=key, bad=bad), self.assertRaises(archive.InvalidArchiveConfig): self.validate_capture(identity, prefix, value)
        for gap in ({'code': 'row_limit', 'ordinal': True}, {'code': 'bad', 'ordinal': 0},
                    {'code': 'row_limit', 'ordinal': 0, 'extra': 1}):
            value = copy.deepcopy(base); value.update(complete=False, first_missing_ordinal=1, gaps=[gap])
            with self.subTest(gap=gap), self.assertRaises(archive.InvalidArchiveConfig): self.validate_capture(identity, prefix, value)
        value = copy.deepcopy(base); value.update(complete=False, first_missing_ordinal=1, gaps=[{'code': 'row_limit', 'ordinal': 1}])
        self.assertFalse(self.validate_capture(identity, prefix, value)[0]['complete'])
        value['complete'] = True
        with self.assertRaises(archive.InvalidArchiveConfig): self.validate_capture(identity, prefix, value)

    def test_per_stage_object_failure_produces_incomplete_canonical_and_receipt(self):
        for fail_call in range(1, 7):
            with self.subTest(fail_call=fail_call), tempfile.TemporaryDirectory() as root, patch.object(archive.shutil, 'disk_usage') as disk:
                disk.return_value.free = 2 * 268435456 + 1048576 + 65536
                calls = []
                def command(args, deadline):
                    calls.append(args[3]); index = len(calls)
                    if index == fail_call: return 1, b'', b'unexpected failure'
                    if index in (1, 5): return 0, b'{"properties":{"contentLength":3,"etag":"etag"}}', b''
                    if index == 3: return 1, b'', b'BlobNotFound'
                    if index in (2, 6): Path(args[args.index('--file') + 1]).write_bytes(b'abc')
                    return 0, b'', b''
                transfer = archive.Transfer('source', 'dest', root, float('inf'), command)
                collector = archive.Collector(transfer, PREFIX)
                collector.source('ledger', 'run/a', 'ledger')
                self.assertEqual('failed', collector.objects[0]['state'])
                self.assertEqual('transport', collector.gaps[0]['code'])
                value = manifest(); value.update(archive_complete=False, objects=collector.objects,
                                                 totals=collector.totals(), gaps=collector.gaps)
                class Controls:
                    deadline = float('inf')
                    def write_control(self, name, data, cap):
                        return {'name': name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}
                result = archive.publish_controls(Controls(), value, IDENTITY, root, PREFIX)
                canonical = (Path(root) / 'archive-manifest.json').read_bytes()
                self.assertEqual(archive._common.encoded(value), canonical)
                self.assertFalse(result['archive_complete']); self.assertIsNone(result['completion'])
                self.assertEqual('transport', result['gaps'][0]['code'])
                self.assertEqual(result, json.loads((Path(root) / 'archive-receipt.json').read_bytes()))
                self.assertEqual(canonical, (Path(root) / 'archive-manifest.json').read_bytes())

    def test_large_gap_set_keeps_public_receipt_within_64k(self):
        class Controls:
            deadline = float('inf')
            def write_control(self, name, data, cap):
                return {'name': name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}
        value = manifest()
        value.update(archive_complete=False, gaps=[dict(GAP, boundary=f'{index:03d}' + 'x' * 2045) for index in range(40)])
        with tempfile.TemporaryDirectory() as root:
            result = archive.publish_controls(Controls(), value, IDENTITY, root, PREFIX)
            raw = (Path(root) / 'archive-receipt.json').read_bytes()
            self.assertLessEqual(len(raw), 65536)
            self.assertFalse(result['archive_complete'])
            self.assertTrue(result['gaps'])
            self.assertIn('diagnostics_truncated', [gap['code'] for gap in result['gaps']])
            self.assertEqual(archive._common.encoded(value), (Path(root) / 'archive-manifest.json').read_bytes())

    def test_control_transport_failure_at_each_stage(self):
        for control in ('archive-manifest.json', 'archive-completion.json'):
            for failed_stage in ('initial show', 'upload', 'final show', 'download'):
                with self.subTest(control=control, stage=failed_stage), tempfile.TemporaryDirectory() as root, patch.object(archive.shutil, 'disk_usage') as disk:
                    disk.return_value.free = 2 * 268435456 + 1048576 + 65536
                    blobs = {}; seen = {}
                    def command(args, deadline):
                        name = args[args.index('--name') + 1]; action = args[3]
                        seen[name] = seen.get(name, 0) + 1
                        stage = ('initial show' if seen[name] == 1 else 'final show') if action == 'show' else action
                        if name.endswith(control) and stage == failed_stage: return 1, b'', b'injected failure'
                        if action == 'show':
                            if name not in blobs: return 1, b'', b'BlobNotFound'
                            return 0, json.dumps({'properties': {'contentLength': len(blobs[name]), 'etag': 'e'}}).encode(), b''
                        if action == 'upload': blobs[name] = Path(args[args.index('--file') + 1]).read_bytes()
                        if action == 'download': Path(args[args.index('--file') + 1]).write_bytes(blobs[name])
                        return 0, b'', b''
                    transfer = archive.Transfer('source', 'dest', root, float('inf'), command)
                    original = manifest(); result = archive.publish_controls(transfer, original, IDENTITY, root, PREFIX)
                    self.assertFalse(result['archive_complete']); self.assertIsNone(result['completion'])
                    self.assertEqual('transport', result['gaps'][0]['code'])
                    self.assertEqual(control == 'archive-completion.json', result['manifest'] is not None)
                    self.assertEqual(archive._common.encoded(original), (Path(root) / 'archive-manifest.json').read_bytes())
                    self.assertFalse((Path(root) / 'control.partial').exists())
                    self.assertFalse((Path(root) / 'readback.partial').exists())

    def test_properties_and_spool_reserve_boundaries(self):
        for size in (0, 268435455, 268435456, 268435457, True, -1):
            with self.subTest(size=size), tempfile.TemporaryDirectory() as root:
                command = lambda args, deadline: (0, json.dumps({'properties': {'contentLength': size, 'etag': 'etag'}}).encode(), b'')
                transfer = archive.Transfer('source', 'dest', root, float('inf'), command)
                if type(size) is int and 0 <= size <= 268435456:
                    self.assertEqual((size, 'etag'), transfer.properties('source', 'ledger', 'run/a'))
                else:
                    with self.assertRaises(archive.Gap) as error: transfer.properties('source', 'ledger', 'run/a')
                    self.assertEqual('source_too_large', error.exception.code)
        reserve = 2 * 268435456 + 1048576 + 65536
        with tempfile.TemporaryDirectory() as root, patch.object(archive.shutil, 'disk_usage') as disk:
            transfer = archive.Transfer('source', 'dest', root, float('inf'))
            for free in (reserve - 1, reserve, reserve + 1):
                disk.return_value.free = free
                with self.subTest(free=free), patch.object(transfer, 'properties', side_effect=archive.Gap('transport', 'properties')) as properties:
                    with self.assertRaises(archive.Gap) as error: transfer.write_control('v1/control', b'', 65536)
                    self.assertEqual('disk_space' if free < reserve else 'transport', error.exception.code)
                    self.assertEqual(free >= reserve, properties.called)

    def test_data_count_before_and_at_reserved_control_ceiling(self):
        for count in (1021, 1022, 1023):
            collector = archive.Collector(None, 'v1/a/')
            for index in range(count): collector.not_started('ledger', f'run/o{index}', 'object')
            with self.subTest(count=count):
                self.assertEqual(min(count, 1022), len(collector.objects))
                self.assertEqual([] if count <= 1022 else ['object_limit'], [gap['code'] for gap in collector.gaps])

    def test_serialized_receipt_exact_byte_ceiling(self):
        for target in (65535, 65536, 65537):
            value = receipt()
            value.update(archive_complete=False, manifest=None, completion=None,
                         gaps=[dict(GAP, boundary='x' * 2048) for _ in range(30)] + [dict(GAP, boundary='')])
            base = len(archive._common.encoded(value))
            value['gaps'][-1]['boundary'] = 'x' * (target - base)
            self.assertEqual(target, len(archive._common.encoded(value)))
            archive.validate_receipt(value)
            with self.subTest(target=target), tempfile.TemporaryDirectory() as root:
                archive.write_receipt(root, value)
                raw = (Path(root) / 'archive-receipt.json').read_bytes()
                written = json.loads(raw)
                if target <= 65536:
                    self.assertEqual(target, len(raw))
                    self.assertEqual(value, written)
                else:
                    self.assertLessEqual(len(raw), 65536)
                    self.assertFalse(written['archive_complete'])
                    self.assertIn('diagnostics_truncated', [gap['code'] for gap in written['gaps']])
                archive.validate_receipt(written)

class SecretOutputBoundaries(unittest.TestCase):
    def test_private_inputs_cli_errors_and_spool_stay_out_of_public_outputs(self):
        import contextlib
        import io

        fixture_spec = importlib.util.spec_from_file_location(
            'archive_boundary_integration_fixture', Path(__file__).with_name('test_archive_inventory_integration.py'))
        fixture = importlib.util.module_from_spec(fixture_spec)
        fixture_spec.loader.exec_module(fixture)
        fixture.a = archive
        sentinels = {
            'backend.conf': b'BACKEND_PRIVATE_SENTINEL_494',
            'terraform.tfvars': b'TFVARS_PRIVATE_SENTINEL_494',
            'oidc-request': b'OIDC_PRIVATE_SENTINEL_494',
            'access-token': b'TOKEN_PRIVATE_SENTINEL_494',
            'cli-error': b'CLI_ERROR_PRIVATE_SENTINEL_494',
            'claim-check': b'CLAIM_CHECK_BODY_SENTINEL_494',
        }
        for fail in (False, True):
            with self.subTest(cli_failure=fail):
                h = fixture.Harness('A01', 'C0', 'a01-42-1', kind='smoke')
                self.addCleanup(h.close)
                for name in ('backend.conf', 'terraform.tfvars', 'oidc-request', 'access-token'):
                    (h.private / name).write_bytes(sentinels[name])
                h.cli.put('ledger', 'a01-42-1/C0/source.json', b'{"safe":"permitted metadata"}')
                h.cli.put('claim-check', 'a01-42-1/C0/body', sentinels['claim-check'])
                calls = []
                cli = h.cli
                def command(args, deadline):
                    calls.append(args)
                    if fail and args[3] == 'show' and args[args.index('--account-name') + 1] == 'source123':
                        return 1, b'', sentinels['cli-error']
                    return cli(args, deadline)
                h.cli = command
                def collect(collector, phase, status, containers, artifacts, job_name):
                    collector.source('ledger', 'a01-42-1/C0/source.json', 'ledger')
                    return []
                stdout, stderr = io.StringIO(), io.StringIO()
                with patch.object(archive, 'collect_ordinary', side_effect=collect), \
                        patch.object(archive, 'ordinary_fixed_inventory', return_value=[]), \
                        patch.object(archive, 'ordinary_units', return_value=([], [])), \
                        contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                    _, value, result = h.run()
                self.assertEqual(not fail, result['archive_complete'])
                self.assertEqual(not fail, value['archive_complete'])
                public = b'\n'.join(p.read_bytes() for p in h.artifacts.rglob('*') if p.is_file())
                output = stdout.getvalue().encode() + stderr.getvalue().encode()
                archived = b'\n'.join(data for (account, _, _), data in cli.blobs.items() if account == 'archive123')
                for name, secret in sentinels.items():
                    with self.subTest(secret=name):
                        self.assertNotIn(secret, public)
                        self.assertNotIn(secret, output)
                        self.assertNotIn(secret, archived)
                self.assertFalse(any('claim-check' in args for args in calls))
                transfer_paths = [Path(args[args.index('--file') + 1]) for args in calls if '--file' in args]
                self.assertTrue(transfer_paths)
                for path in transfer_paths:
                    self.assertTrue(path.is_relative_to(h.private / 'archive'))
                    self.assertFalse(path.is_relative_to(h.artifacts))

    def test_pod_and_phase_projections_use_closed_safe_fields(self):
        secret = 'POD_CONFIG_SECRET_494'
        pods = {'items': [{'metadata': {'name': 'worker-1', 'uid': 'uid-1', 'annotations': {'token': secret}},
                           'spec': {'containers': [{'env': [{'value': secret}]}]},
                           'status': {'phase': 'Failed', 'message': secret,
                                      'conditions': [{'type': 'Ready', 'status': 'False', 'message': secret},
                                                     {'type': secret, 'status': 'True'}],
                                      'containerStatuses': [{'name': 'worker', 'state': {'terminated': {
                                          'exitCode': 3, 'message': secret}}, 'containerID': secret}]}}]}
        projection = json.loads(archive.project_pods(json.dumps(pods).encode()))
        self.assertEqual({'schema_version': 1, 'kind': 'pod_diagnostics', 'items': [
            {'name': 'worker-1', 'uid': 'uid-1', 'phase': 'Failed',
             'conditions': [{'type': 'Ready', 'status': 'False'}],
             'containers': [{'kind': 'containerStatuses', 'name': 'worker', 'terminated_exit_code': 3}]}]}, projection)
        status = {'run_id': 'a01-42-1', 'phase': 'C0', 'secret': secret,
                  'modes': [{'job': 'worker-1', 'mode': 'verify', 'repeat': 1, 'outcome': 'failed',
                             'apply': 0, 'error': secret}]}
        projected = json.loads(archive.project_phase_status(status))
        self.assertEqual({'schema_version': 1, 'kind': 'phase_status_diagnostics', 'run_id': 'a01-42-1',
                          'phase': 'C0', 'modes': [{'job': 'worker-1', 'mode': 'verify', 'repeat': 1,
                              'outcome': 'failed', 'render': None, 'apply': 0, 'wait': None,
                              'collect': None, 'delete': None, 'confirm': None}]}, projected)
        self.assertNotIn(secret, json.dumps(projection) + json.dumps(projected))

    def test_overlapping_private_and_uploaded_directories_are_rejected(self):
        with tempfile.TemporaryDirectory() as root:
            root = Path(root)
            private = root / 'private'; private.mkdir()
            nested = private / 'uploaded'; nested.mkdir()
            for private_path, artifacts_path in ((private, private), (private, nested), (nested, private)):
                env = {'PARCHWELL_PRIVATE_DIRECTORY': str(private_path),
                       'PARCHWELL_ARTIFACT_DIRECTORY': str(artifacts_path),
                       'PARCHWELL_ARCHIVE_DIRECTORY': str(private_path / 'archive'),
                       'PARCHWELL_ARCHIVE_MANIFEST_DIRECTORY': str(artifacts_path / 'archive'),
                       'PARCHWELL_ARCHIVE_MANIFEST_PATH': str(artifacts_path / 'archive' / 'archive-manifest.json')}
                with self.subTest(private=private_path, artifacts=artifacts_path), self.assertRaises(archive.InvalidArchiveConfig):
                    archive.load_context(env)

    def test_body_projection_preserves_header_and_nonbody_fields(self):
        import base64
        import io
        spec = importlib.util.spec_from_file_location('archive_json_sentinel', Path(__file__).resolve().parents[1] / 'archive_json.py')
        projection = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(projection)
        body = b'EXCLUDED_MESSAGE_BODY_SENTINEL_494'
        encoded_body = base64.b64encode(body).decode()
        header = base64.b64encode(b'RETAINED_HEADER_SENTINEL_494').decode()
        source = {'identity': {'run_id': 'a01-42-1'}, 'repeats': [
            {'repeat': 1, 'matched': {'value_base64': encoded_body, 'key_base64': 'cmV0YWluZWQta2V5',
                                      'topic': 'document-events', 'partition': 0},
             'requested': {'value_base64': ''}, 'read_observation': {'record': {'value_base64': None}}}]}
        with tempfile.TemporaryDirectory() as root:
            path = Path(root) / 'source.json'
            path.write_text(json.dumps(source))
            sink = io.BytesIO()
            projection.project(path, float('inf'), sink)
            output = sink.getvalue()
            result = json.loads(output)
            row = result['repeats'][0]
            self.assertEqual({'is_null': False, 'length': len(body), 'sha256': hashlib.sha256(body).hexdigest()}, row['matched']['value_base64'])
            self.assertEqual({'is_null': False, 'length': 0, 'sha256': hashlib.sha256(b'').hexdigest()}, row['requested']['value_base64'])
            self.assertEqual({'is_null': True, 'length': None, 'sha256': None}, row['read_observation']['record']['value_base64'])
            self.assertEqual('cmV0YWluZWQta2V5', row['matched']['key_base64'])
            self.assertEqual('document-events', row['matched']['topic'])
            self.assertEqual(0, row['matched']['partition'])
            self.assertNotIn(body, output)
            self.assertNotIn(encoded_body.encode(), output)
            # SingleActionMatched is closed and cannot carry headers.
            source['repeats'][0]['matched']['headers'] = [{'key': 'trace', 'value_base64': header}]
            path.write_text(json.dumps(source))
            with self.assertRaises(projection.Invalid):
                projection.project(path, float('inf'), io.BytesIO())
            source['repeats'][0]['matched']['key'] = 'retained-key'
            path.write_text(json.dumps(source))
            with self.assertRaises(projection.Invalid):
                projection.project(path, float('inf'), io.BytesIO())
            del source['repeats'][0]['matched']['key']
            del source['repeats'][0]['matched']['headers']
            source['repeats'][0]['unrecognized'] = {'value_base64': encoded_body}
            path.write_text(json.dumps(source))
            with self.assertRaises(projection.Invalid):
                projection.project(path, float('inf'), io.BytesIO())

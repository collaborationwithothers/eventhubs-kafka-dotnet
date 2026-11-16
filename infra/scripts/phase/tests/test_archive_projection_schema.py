"""Regression coverage for the closed legacy capture projection wire."""
import base64
import importlib.util
import json
from pathlib import Path
import unittest


spec = importlib.util.spec_from_file_location('archive_projection_fixtures', Path(__file__).with_name('test_archive_inventory_integration.py'))
fixture = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fixture)


class ProjectionSchemaTests(unittest.TestCase):
    def capture(self, idle):
        scenario, phase, run, item, container, suffix = (
            ('idle-observation', 'idle-observation', 'idle-42-1', 'A02', 'ledger', 'idle') if idle else
            ('probe-batch', 'single-action', 'probe-42-1', 'B01', 'single-action', 'single'))
        harness = fixture.Harness(scenario, phase, run, 'batch', items=[item])
        self.addCleanup(harness.close)
        fixture.complete_family(harness, scenario)
        name = f'{run}/{phase}/{item}/C0/instances/{run}-{item.lower()}-c0-{suffix}-pod/capture.json'
        key = ('source123', container, name)
        return harness, key, json.loads(harness.cli.blobs[key])

    def test_valid_nested_evidence_is_preserved(self):
        for idle in (False, True):
            with self.subTest(idle=idle):
                h, key, capture = self.capture(idle)
                row = capture['repeats'][0]
                report = base64.b64encode(b'{"arbitrary_native_field":"retained"}').decode()
                error = {'code': -1, 'is_fatal': False, 'origin': 'broker', 'reason': 'native reason'}
                row.update(settings={'value_base64': 'ordinary setting', 'native': 'value'}, omitted_keys=['absent'],
                           client_errors=[{**error, 'observed_elapsed_ms': 3}], client_errors_dropped=1,
                           statistics=[{'sequence': 1, 'observed_elapsed_ms': 4, 'raw_base64': report}],
                           diagnostics=['native diagnostic'])
                if idle:
                    row['first'] = {'attempt': 1, 'key': 'key', 'started_elapsed_ms': 0, 'terminal': True,
                                    'result': 'error', 'error': error, 'terminal_elapsed_ms': 2, 'terminal_unix_ms': 3}
                    row['second'] = {'attempt': 2, 'key': 'key2', 'started_elapsed_ms': 5, 'terminal': False,
                                     'result': None, 'error': None, 'terminal_elapsed_ms': None, 'terminal_unix_ms': None}
                else:
                    row['result'] = 'error'; row['error'] = error
                h.cli.blobs[key] = fixture.encoded(capture)
                _, manifest, _ = h.run()
                obj = next(o for o in manifest['objects'] if o['source']['name'] == key[2])
                self.assertEqual('verified', obj['state'])
                projected = json.loads(h.cli.blobs[('archive123', 'run-evidence', obj['destination_name'])])['metadata']['repeats'][0]
                for field in ('settings', 'omitted_keys', 'client_errors', 'statistics', 'diagnostics'):
                    self.assertEqual(row[field], projected[field])
                if idle: self.assertEqual(row['first'], projected['first'])
                else: self.assertEqual(row['error'], projected['error'])

    def test_large_native_reports_are_streamed_and_retained(self):
        h, key, capture = self.capture(False)
        raw = b'{"native":"' + b'x' * (262144 - len(b'{"native":""}')) + b'"}'
        report = base64.b64encode(raw).decode()
        for row in capture['repeats']:
            row['statistics'] = [{'sequence': 1, 'observed_elapsed_ms': 4, 'raw_base64': report}]
        h.cli.blobs[key] = fixture.encoded(capture)
        _, manifest, _ = h.run()
        obj = next(o for o in manifest['objects'] if o['source']['name'] == key[2])
        self.assertEqual('verified', obj['state'])
        projected = json.loads(h.cli.blobs[('archive123', 'run-evidence', obj['destination_name'])])
        self.assertEqual([report] * 5, [r['statistics'][0]['raw_base64'] for r in projected['metadata']['repeats']])

    def test_duplicate_nested_member_is_rejected(self):
        h, key, capture = self.capture(False)
        original = fixture.encoded(capture)
        h.cli.blobs[key] = original.replace(b'"settings":{}', b'"settings":{"key":"one","key":"two"}', 1)
        code, manifest, receipt = h.run()
        self.assertEqual(1, code)
        self.assertFalse(receipt['archive_complete'])
        self.assertIsNone(receipt['completion'])
        obj = next(o for o in manifest['objects'] if o['source']['name'] == key[2])
        self.assertNotEqual('verified', obj['state'])
        self.assertTrue(any(g['code'] == 'monitoring_incomplete' for g in manifest['gaps']))

    def test_malformed_nested_fields_are_gaps_and_never_uploaded(self):
        sentinel = 'message credential SENTINEL_NOT_PUBLIC'
        error = {'code': 1, 'is_fatal': False, 'origin': 'local', 'reason': 'reason'}
        produce = {'attempt': 1, 'key': 'key', 'started_elapsed_ms': 0, 'terminal': True,
                   'result': 'success', 'error': None, 'terminal_elapsed_ms': 1, 'terminal_unix_ms': 2}
        cases = [
            ('settings', sentinel), ('settings', {'key': 7}), ('omitted_keys', [sentinel, None]),
            ('client_errors', [{**error, 'observed_elapsed_ms': True}]),
            ('client_errors', [{**error, 'observed_elapsed_ms': 1, 'unknown': sentinel}]),
            ('statistics', [{'sequence': 1, 'observed_elapsed_ms': 2, 'raw_base64': sentinel}]),
            ('statistics', [{'sequence': True, 'observed_elapsed_ms': 2, 'raw_base64': 'e30='}]),
            ('diagnostics', [sentinel, {'unexpected': sentinel}]), ('client_errors_dropped', False),
        ]
        for idle in (False, True):
            extra = [('first', {**produce, 'error': {**error, 'reason': 3}}),
                     ('second', {**produce, 'unknown': sentinel}), ('client_started_unix_ms', True),
                     ('idle_started_elapsed_ms', sentinel)] if idle else [
                         ('action', 1), ('terminal', 1), ('elapsed_ms', True), ('terminal_unix_ms', sentinel),
                         ('terminal_unix_ms', []), ('matched', []),
                         ('result', sentinel), ('error', {**error, 'code': True}),
                         ('matched', {'topic': 't', 'partition': 0, 'key_base64': 'aw==',
                                      'value_base64': None, 'unknown': sentinel})]
            for field, invalid in cases + extra:
                with self.subTest(idle=idle, field=field, invalid=invalid):
                    h, key, capture = self.capture(idle)
                    capture['repeats'][0][field] = invalid
                    # Include the sentinel in every malformed source, including scalar type cases.
                    capture['repeats'][1]['diagnostics'] = [sentinel]
                    h.cli.blobs[key] = fixture.encoded(capture)
                    code, manifest, receipt = h.run()
                    self.assertEqual(1, code)
                    self.assertFalse(receipt['archive_complete'])
                    self.assertIsNone(receipt['completion'])
                    obj = next(o for o in manifest['objects'] if o['source']['name'] == key[2])
                    self.assertNotEqual('verified', obj['state'])
                    self.assertTrue(any(g['code'] == 'monitoring_incomplete' for g in manifest['gaps']))
                    remote = b''.join(v for (account, _, _), v in h.cli.blobs.items() if account == 'archive123')
                    public = b''.join(p.read_bytes() for p in (h.artifacts / 'archive').rglob('*') if p.is_file())
                    self.assertNotIn(sentinel.encode(), remote + public)

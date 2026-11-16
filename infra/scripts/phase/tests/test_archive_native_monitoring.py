import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('archive_native_monitoring', Path(__file__).resolve().parents[1] / 'archive_native_monitoring.py')
native = importlib.util.module_from_spec(spec)
spec.loader.exec_module(native)


class NativeMonitoringTests(unittest.TestCase):
    def setUp(self):
        self.identity = dict(run_id='run', phase='C0', scenario_id='A01', configuration_id='C0', job_name='job', pod_uid='uid', source_commit='a' * 40)
        self.prefix = 'run/C0/job/uid/native-monitoring/'
        self.client = dict(ordinal=1, role='LoadGenProducer', client_id='client', native_identity=None, build_status='built', report_count=0, raw_sources=[], diagnostics=[], capture_complete=True, export_tail_status='unknown')
        self.document = dict(schema_version=1, identity=self.identity, clients=[self.client], capture_complete=True, diagnostics_truncated=False)

    def encode(self):
        return json.dumps(self.document).encode()

    def test_zero_reports_and_identity(self):
        self.assertTrue(native.index(self.encode(), self.identity, self.prefix)['capture_complete'])
        self.document['identity'] = dict(self.identity, pod_uid='other')
        with self.assertRaises(ValueError): native.index(self.encode(), self.identity, self.prefix)

    def test_limits_and_incomplete_precedence(self):
        self.document['clients'] = [dict(self.client, ordinal=i + 1) for i in range(33)]
        with self.assertRaises(ValueError): native.index(self.encode(), self.identity, self.prefix)
        self.document['clients'] = [self.client]
        self.document['diagnostics_truncated'] = True
        with self.assertRaises(ValueError): native.index(self.encode(), self.identity, self.prefix)
        self.document['capture_complete'] = False
        self.assertTrue(native.index(self.encode(), self.identity, self.prefix)['diagnostics_truncated'])
        with self.assertRaises(ValueError): native.index(b' ' * (native.MAX_INDEX + 1), self.identity, self.prefix)
        with self.assertRaises(ValueError): native.index(b'{"schema_version":1,"schema_version":1}', self.identity, self.prefix)

    def test_chunk_sequence_and_exact_raw_bound(self):
        descriptor = dict(report_ordinal=1, row_count=1)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'chunk'
            path.write_bytes((json.dumps(dict(client_ordinal=1, report_ordinal=1, raw='x' * native.MAX_REPORT)) + '\n').encode())
            native.chunk(path, descriptor, 1)
            path.write_bytes((json.dumps(dict(client_ordinal=1, report_ordinal=2, raw='x')) + '\n').encode())
            with self.assertRaises(ValueError): native.chunk(path, descriptor, 1)
            path.write_bytes((json.dumps(dict(client_ordinal=1, report_ordinal=1, raw='x' * (native.MAX_REPORT + 1))) + '\n').encode())
            with self.assertRaises(ValueError): native.chunk(path, descriptor, 1)


if __name__ == '__main__':
    unittest.main()

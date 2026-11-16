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

    def test_index_policy_boundaries(self):
        clients = [dict(self.client, ordinal=i + 1) for i in range(native.MAX_CLIENTS)]
        self.document['clients'] = clients
        native.index(self.encode(), self.identity, self.prefix)
        self.document['clients'] = clients + [dict(self.client, ordinal=33)]
        with self.assertRaises(ValueError): native.index(self.encode(), self.identity, self.prefix)
        self.document['clients'] = [self.client]
        descriptors = [dict(kind='existing_capture', source='ledger/run/source', byte_length=1,
                            sha256='a' * 64, report_ordinal=i + 1, row_count=1) for i in range(native.MAX_REFS)]
        self.client.update(report_count=native.MAX_REFS, raw_sources=descriptors)
        native.index(self.encode(), self.identity, self.prefix)
        self.client.update(report_count=native.MAX_REFS + 1, raw_sources=descriptors + [dict(descriptors[0], report_ordinal=native.MAX_REFS + 1)])
        with self.assertRaises(ValueError): native.index(self.encode(), self.identity, self.prefix)
        self.client.update(report_count=0, raw_sources=[], diagnostics=[f'diagnostic_{i}' for i in range(native.MAX_DIAGNOSTICS)], capture_complete=False)
        self.document['capture_complete'] = False
        native.index(self.encode(), self.identity, self.prefix)
        self.client['diagnostics'].append('diagnostic_over')
        with self.assertRaises(ValueError): native.index(self.encode(), self.identity, self.prefix)
        self.client.update(report_count=1, diagnostics=[], capture_complete=True, raw_sources=[dict(
            kind='raw_chunk', source=self.prefix + 'raw-000001.jsonl', byte_length=native.MAX_RAW,
            sha256='a'*64, report_ordinal=1, row_count=1)])
        self.document['capture_complete'] = True
        native.index(self.encode(), self.identity, self.prefix)
        self.client['raw_sources'][0]['byte_length'] += 1
        with self.assertRaises(ValueError): native.index(self.encode(), self.identity, self.prefix)

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


class AdapterTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location('archive_native_adapter', Path(__file__).resolve().parents[1] / 'archive.py')
        self.archive = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.archive)
        self.identity = dict(run_id='run', phase='C0', scenario_id='A01', configuration_id='C0', job_name='job', pod_uid='uid', source_commit='a' * 40)
        self.prefix = 'run/C0/job/uid/native-monitoring/'
        self.phase = dict(run_id='run', phase='C0', configuration='C0', scenario='A01', source_commit='a' * 40)
        self.units = [dict(state='started', pod_uid='uid', pod_name='job-pod', job_name='job', item=None, mode='loadgen', repeat=1)]
        self.status = {'modes': [{'job': 'job'}]}

    def make_collector(self, documents):
        archive = self.archive
        class Collector:
            def __init__(self):
                self.objects, self.gaps, self.native_existing = [], [], {}
            def gap(self, code, boundary, name=None, unit=None):
                self.gaps.append((code, boundary, name, unit))
            def source(self, container, name, kind, **kwargs):
                data = documents.get(name)
                if data is None:
                    return None
                with tempfile.NamedTemporaryFile() as stream:
                    stream.write(data); stream.flush()
                    try:
                        kwargs['inspect_source'](stream.name, len(data))
                    except archive.Gap:
                        return None
                obj = {'source': {'container': container, 'name': name}, 'state': 'verified'}
                self.objects.append(obj)
                return obj
        return Collector()

    def encode(self, client=None, complete=True, truncated=False):
        return json.dumps(dict(schema_version=1, identity=self.identity, clients=[] if client is None else [client],
                               capture_complete=complete, diagnostics_truncated=truncated)).encode()

    def test_missing_index_and_diagnostic_truncation_are_gaps(self):
        collector = self.make_collector({})
        self.archive.collect_native_monitoring(collector, self.phase, self.status, self.units)
        self.assertTrue(any(g[0] == 'monitoring_incomplete' for g in collector.gaps))
        collector = self.make_collector({self.prefix + 'index.json': self.encode(complete=False, truncated=True)})
        self.archive.collect_native_monitoring(collector, self.phase, self.status, self.units)
        self.assertEqual({'monitoring_incomplete', 'diagnostics_truncated'}, {g[0] for g in collector.gaps})

    def test_existing_reference_requires_same_pod_and_matching_hash(self):
        source = 'run/C0/r1/producer/job-pod/publication/statistics.jsonl'
        descriptor = dict(kind='existing_capture', source='ledger/' + source, byte_length=2, sha256='b' * 64, report_ordinal=1, row_count=1)
        client = dict(ordinal=1, role='LoadGenProducer', client_id='client', native_identity=None, build_status='built', report_count=1,
                      raw_sources=[descriptor], diagnostics=[], capture_complete=True, export_tail_status='unknown')
        collector = self.make_collector({self.prefix + 'index.json': self.encode(client)})
        collector.objects.append({'source': {'container': 'ledger', 'name': source}, 'state': 'verified'})
        collector.native_existing[('ledger', source)] = {('client', 1): (2, 'b' * 64)}
        self.archive.collect_native_monitoring(collector, self.phase, self.status, self.units)
        self.assertEqual([], collector.gaps)
        collector.native_existing[('ledger', source)][('client', 1)] = (2, 'c' * 64)
        self.archive.collect_native_monitoring(collector, self.phase, self.status, self.units)
        self.assertTrue(any(g[0] == 'monitoring_incomplete' for g in collector.gaps))


if __name__ == '__main__':
    unittest.main()

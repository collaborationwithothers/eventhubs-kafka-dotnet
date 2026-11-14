import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

import c5_runtime as profile_module
ROOT = Path(__file__).resolve().parents[3]


def xml(*results):
    tests = ''.join(f'<test name="case{i}" result="{result}" time="8" />' for i, result in enumerate(results))
    return f'<assemblies><assembly><collection>{tests}</collection></assembly></assemblies>'


class ProfileTests(unittest.TestCase):
    def invoke(self, content, code=0, reason=None):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / 'evidence'
            calls = []
            def launch(command, root, logfile, timeout):
                calls.append((command, timeout))
                logfile.write_text('synthetic')
                if content is not None:
                    (logfile.parent / 'results.xml').write_text(content)
                return code, reason
            ticks = iter([0, 10])
            status = profile_module.profile(output, 600, ROOT, launch, lambda: next(ticks))
            return status, json.loads((output / 'profile.json').read_text()), calls

    def test_profile_population_and_wall_time(self):
        status, result, calls = self.invoke(xml('Pass', 'Pass'))
        self.assertEqual(0, status)
        self.assertEqual('complete', result['status'])
        self.assertEqual(2, len(result['cases']))
        self.assertEqual(16, result['test_duration_seconds'])
        self.assertEqual(10, result['wall_seconds'])
        self.assertEqual(1, len(calls))
        self.assertIn('-p:ParchwellVerifierMutationOnly=true', calls[0][0])
        self.assertIn('--report-xunit-xml', calls[0][0])

    def test_profile_failure_is_not_measurement(self):
        for content, code, reason in [(xml('Fail'), 1, None), (None, 0, None), (None, None, 'timeout')]:
            with self.subTest(reason=reason, code=code):
                status, result, calls = self.invoke(content, code, reason)
                self.assertEqual(1, status)
                self.assertEqual('incomplete', result['status'])
                if content is None:
                    self.assertNotIn('cases', result)
                else:
                    self.assertEqual(1, result['failures'])
                self.assertEqual(1, len(calls))

    def test_nonempty_output_never_launches(self):
        with tempfile.TemporaryDirectory() as directory:
            Path(directory, 'existing').write_text('keep')
            with self.assertRaises(ValueError):
                profile_module.profile(directory, 600, ROOT, lambda *args: self.fail('launched'))
            self.assertEqual('keep', Path(directory, 'existing').read_text())



import copy
import importlib.util
import unittest
from datetime import datetime, timedelta, timezone

import c5_runtime as p


def stamp(s):
    return (datetime(2026, 1, 1, tzinfo=timezone.utc) + timedelta(seconds=s)).isoformat()


def fixture(wall=20):
    run = {'id': 12, 'run_attempt': 1, 'head_sha': 'abc', 'created_at': stamp(0),
           'updated_at': stamp(wall), 'status': 'completed', 'conclusion': 'success'}
    def job(i, name, created, start, end):
        return {'id': i, 'run_id': 12, 'run_attempt': 1, 'head_sha': 'abc', 'name': name,
                'created_at': stamp(created), 'started_at': stamp(start), 'completed_at': stamp(end),
                'status': 'completed', 'conclusion': 'success'}
    jobs = [job(1, 'mutation (a, a.cs)', 0, 0, 10), job(2, 'mutation (b, b.cs)', 0, 5, 15),
            job(3, 'aggregate', 15, 15, 20)]
    return run, {'jobs': jobs}, {'a': 'a.cs', 'b': 'b.cs'}


class PerformanceTests(unittest.TestCase):
    def test_boundary(self):
        for wall, status in [(1799, 'met'), (1800, 'met'), (1801, 'missed')]:
            with self.subTest(wall=wall):
                self.assertEqual(p.performance_report(*fixture(wall))['status'], status)

    def test_compute_peak_queue_and_aggregation(self):
        run, jobs, expected = fixture(1810)
        jobs['jobs'][1]['completed_at'] = stamp(1790)
        result = p.performance_report(run, jobs, expected)
        self.assertEqual(result['status'], 'missed')
        self.assertEqual(result['workflow_elapsed_seconds'], 1810)
        self.assertEqual(result['mutation_job_seconds'], 1795)
        self.assertEqual(result['peak_mutation_jobs'], 2)
        self.assertEqual(result['queue_seconds']['mutation (b, b.cs)'], 5)
        run, jobs, expected = fixture()
        self.assertEqual(p.performance_report(run, jobs, expected)['mutation_job_seconds'], 20)
        self.assertEqual(p.performance_report(run, jobs, expected)['peak_mutation_jobs'], 2)

    def test_incomplete(self):
        for change in ('cancelled', 'missing_completion', 'mixed_attempt', 'early_run_end', 'missing_job', 'partial_page'):
            with self.subTest(change=change):
                run, jobs, expected = fixture()
                if change == 'cancelled': jobs['jobs'][0]['conclusion'] = 'cancelled'
                if change == 'missing_completion': jobs['jobs'][0]['completed_at'] = None
                if change == 'mixed_attempt': jobs['jobs'][0]['run_attempt'] = 2
                if change == 'early_run_end': run['updated_at'] = stamp(19)
                if change == 'missing_job': jobs['jobs'].pop(0)
                if change == 'partial_page': jobs['total_count'] = 4
                self.assertEqual(p.performance_report(run, jobs, expected)['status'], 'incomplete')




class GeometryTests(unittest.TestCase):
    def test_probe_geometry_is_not_outcomes(self):
        import hashlib
        source = b'class A {}\n'
        def row(index, status, reason=None):
            return {'id': str(index), 'mutatorName': 'test', 'replacement': 'x', 'status': status,
                    'statusReason': reason, 'location': {'start': {'line': 1, 'column': 1},
                                                        'end': {'line': 1, 'column': 2}}}
        report = {'schemaVersion': '2', 'projectRoot': '/p', 'files': {'/p/A.cs': {
            'language': 'cs', 'source': source.decode(), 'mutants': [
                row(1, 'Killed'), row(2, 'Ignored', 'Removed by mutate filter'), row(3, 'CompileError')]}}}
        result = p.geometry(report, 'A.cs', source, hashlib.sha256(source).hexdigest(), b'config', hashlib.sha256(b'config').hexdigest())
        self.assertEqual(3, len(result))
        self.assertEqual(1, sum(r['measured_execution'] for r in result))
        self.assertTrue(result[1]['filter_ignored'])
        with self.assertRaises(ValueError):
            p.geometry(report, 'A.cs', source, '0' * 64, b'config', hashlib.sha256(b'config').hexdigest())
        report['files']['/p/A.cs']['mutants'] = []
        with self.assertRaises(ValueError):
            p.geometry(report, 'A.cs', source, hashlib.sha256(source).hexdigest(), b'config', hashlib.sha256(b'config').hexdigest())

    def test_unobserved_mutant_weight_and_isolated_work(self):
        self.assertEqual({'main_work_seconds': 100, 'estimated_seconds': 450},
                         p.estimate_work([20, 40], 1, 100, 2, 300))
        self.assertEqual(1400, p.estimate_work([2000], 0, 100, 2, 300)['estimated_seconds'])
        with self.assertRaises(ValueError):
            p.estimate_work([], 1, 100, 2, 300)

from unittest import mock
import c5_runtime as runtime

class QualificationTests(unittest.TestCase):
    def setUp(self):
        self.shards = {'a-p1': 'a.cs', 'b': 'b.cs'}
        self.source = {'a.cs': b'a', 'b.cs': b'b'}
        self.shared = {'global.json': runtime.digest(b'sdk')}
        self.targets = {p: runtime.digest(data) for p, data in self.source.items()}
        self.workflow_patcher = mock.patch.object(runtime.workflow, 'SHARDS', self.shards)
        self.config_patcher = mock.patch.object(runtime.workflow, 'shard_config', side_effect=lambda shard, source: {'shard': shard, 'source': source.decode(), 'cut': 10})
        self.workflow_patcher.start()
        self.config_patcher.start()
        self.addCleanup(self.workflow_patcher.stop)
        self.addCleanup(self.config_patcher.stop)
        evidence = {'source_commit': 'a' * 40, 'locator': 'example', 'sha256': 'a' * 64,
                    'kind': 'local_probe', 'note': 'calibration uncertainty'}
        self.config = {'schema_version': 1, 'budget_seconds': 1800, 'shared_inputs': self.shared.copy(), 'targets': {}}
        timing = runtime.digest(runtime.canonical({'shared_inputs': self.shared, 'target_sources': self.targets}))
        for shard, path in self.shards.items():
            self.config['targets'][path] = {'source_sha256': self.targets[path], 'evidence': [evidence.copy()], 'shards': {shard: {
                'selection_sha256': runtime.digest(runtime.canonical(runtime.workflow.shard_config(shard, self.source[path]))),
                'timing_inputs_sha256': timing, 'main_mutants': 1, 'main_work_seconds': 2000,
                'parallelism': 2, 'setup_seconds': 100, 'isolated_seconds': 300, 'estimated_seconds': 1400,
                'basis': 'example; conservative weighting; local calibration uncertain'}}}

    def errors(self):
        runtime.shape(self.config)
        return runtime.validate(self.config, self.shared, self.targets, self.source, self.shards)

    def test_estimate_main_and_isolated_work(self):
        self.assertEqual(set(), self.errors())
        self.config['targets']['a.cs']['shards']['a-p1']['estimated_seconds'] = 1250
        self.assertIn('invalid_estimate', self.errors())

    def test_shard_budget_boundary(self):
        for seconds in (1799, 1800, 1801, 1500):
            with self.subTest(seconds=seconds):
                item = self.config['targets']['a.cs']['shards']['a-p1']
                item.update(main_work_seconds=2 * (seconds - 100), isolated_seconds=0, estimated_seconds=seconds)
                self.assertEqual(seconds > 1800, 'shard_over_budget' in self.errors())

    def test_estimate_arithmetic_and_numeric_types(self):
        item = self.config['targets']['a.cs']['shards']['a-p1']
        for field, bad in [('parallelism', True), ('parallelism', 0), ('main_mutants', -1),
                           ('main_work_seconds', float('nan')), ('setup_seconds', float('inf')),
                           ('estimated_seconds', 1399)]:
            with self.subTest(field=field, value=bad):
                original = item[field]
                item[field] = bad
                self.assertIn('invalid_estimate', self.errors())
                item[field] = original

    def test_same_ids_changed_cut(self):
        with mock.patch.object(runtime.workflow, 'shard_config', side_effect=lambda shard, source: {'shard': shard, 'source': source.decode(), 'cut': 20}):
            self.assertIn('layout_mismatch', self.errors())
            for shard, path in self.shards.items():
                self.config['targets'][path]['shards'][shard]['selection_sha256'] = runtime.digest(runtime.canonical(runtime.workflow.shard_config(shard, self.source[path])))
            self.assertEqual(set(), self.errors())

    def test_target_change_invalidates_all_rates(self):
        self.source['a.cs'] = b'changed'
        self.targets['a.cs'] = runtime.digest(self.source['a.cs'])
        a = self.config['targets']['a.cs']
        a['source_sha256'] = self.targets['a.cs']
        a['shards']['a-p1']['selection_sha256'] = runtime.digest(runtime.canonical(runtime.workflow.shard_config('a-p1', self.source['a.cs'])))
        binding = runtime.digest(runtime.canonical({'shared_inputs': self.shared, 'target_sources': self.targets}))
        a['shards']['a-p1']['timing_inputs_sha256'] = binding
        self.assertIn('stale_inputs', self.errors())
        self.config['targets']['b.cs']['shards']['b']['timing_inputs_sha256'] = binding
        self.assertEqual(set(), self.errors())

    def test_shared_input_invalidates_all_rates(self):
        self.shared['global.json'] = runtime.digest(b'new sdk')
        self.assertIn('stale_inputs', self.errors())

    def test_layout_and_missing_evidence(self):
        self.config['targets']['a.cs']['evidence'] = []
        self.assertIn('missing_measurement', self.errors())
        del self.config['targets']['b.cs']
        self.assertIn('missing_qualification', self.errors())
        self.assertIn('layout_mismatch', self.errors())

    def test_matrix_capacity_boundary(self):
        for count in (255, 256, 257):
            with self.subTest(count=count):
                matrix = {f's{i}': 'a.cs' for i in range(count)}
                self.assertEqual(count > 256, 'matrix_over_capacity' in runtime.validate(self.config, self.shared, self.targets, self.source, matrix))

    def test_execution_hash_separates_cuts(self):
        data = Path(runtime.workflow.__file__).read_bytes()
        self.assertEqual(runtime.execution_runner(data), runtime.execution_runner(data.replace(b'(4376, 6570)', b'(4377, 6570)')))
        self.assertNotEqual(runtime.execution_runner(data), runtime.execution_runner(data.replace(b'REBALANCE_SECONDS = 7200', b'REBALANCE_SECONDS = 7201')))

    def test_runtime_profile_directory_membership(self):
        # Capture the tracked tree once; in-memory variants test additions, edits and deletions.
        names = runtime.files('HEAD')
        original_blob = runtime.blob
        cache = {p: original_blob('HEAD', p) for p in names if p.startswith(('src/', 'tests/', 'scenarios/', '.config/', 'docs/operations/', '.github/workflows/')) or '/' not in p}
        fixture = 'scenarios/profiles/synthetic-runtime-fixture.yaml'
        def get_blob(ref, path):
            return cache[path]
        with mock.patch.object(runtime, 'blob', side_effect=get_blob), mock.patch.object(runtime, 'files', return_value=names):
            baseline, _ = runtime.input_maps('HEAD')
            cache[fixture] = b'one'
            with mock.patch.object(runtime, 'files', return_value=names | {fixture}):
                added, _ = runtime.input_maps('HEAD')
                self.assertNotEqual(baseline, added)
                cache[fixture] = b'two'
                changed, _ = runtime.input_maps('HEAD')
                self.assertNotEqual(added, changed)
            self.assertNotIn(fixture, baseline)

    def test_malformed_precedes_irrelevant_bypass(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'bad.json'
            path.write_text('{')
            self.assertEqual({'status': 'rejected', 'reasons': ['invalid_input']}, runtime.check('HEAD', 'HEAD', path))
            self.assertEqual({'status': 'rejected', 'reasons': ['invalid_input']}, runtime.check('not-a-ref', 'HEAD', path))

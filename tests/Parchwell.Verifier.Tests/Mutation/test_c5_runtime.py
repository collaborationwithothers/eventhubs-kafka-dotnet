import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import os
import subprocess

import c5_runtime as profile_module
ROOT = Path(__file__).resolve().parents[3]


class RequiredCheckIntegrationTests(unittest.TestCase):
    def invoke_step(self, event, fail_check):
        import yaml
        workflow = yaml.safe_load((ROOT / '.github/workflows/dotnet-verification.yml').read_text())
        job = workflow['jobs']['dotnet-verification']
        self.assertEqual('.NET verification', job['name'])
        checkout = next(step for step in job['steps'] if step['name'] == 'Check out repository')
        self.assertEqual(0, checkout['with']['fetch-depth'])
        step = next(step for step in job['steps'] if step['name'] == 'Qualify verifier mutation runtime')
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            log = directory / 'calls'
            stub = directory / 'python3'
            stub.write_text('#!/bin/bash\nprintf "%s\\n" "$*" >> "$CALL_LOG"\n'
                            'if [[ "$1" == *c5_runtime.py && "$FAIL_CHECK" = yes ]]; then exit 1; fi\n')
            stub.chmod(0o755)
            env = {**os.environ, 'PATH': str(directory) + os.pathsep + os.environ['PATH'],
                   'CALL_LOG': str(log), 'FAIL_CHECK': 'yes' if fail_check else 'no',
                   'PARCHWELL_EVENT': event, 'PARCHWELL_BASE_SHA': 'base-sha',
                   'PARCHWELL_HEAD_SHA': 'head-sha'}
            result = subprocess.run(['bash', '-c', step['run']], cwd=ROOT, env=env, check=False)
            return result.returncode, log.read_text().splitlines()

    def test_required_check_propagates_rejection(self):
        status, calls = self.invoke_step('pull_request', True)
        self.assertNotEqual(0, status)
        self.assertEqual(3, len(calls))
        self.assertIn('test_c5_runtime.py', calls[0])
        self.assertIn('test_c5_workflow.py', calls[1])
        self.assertIn('--head head-sha', calls[2])
        self.assertIn('--base base-sha', calls[2])

    def test_main_validates_without_diff(self):
        for event in ('push', 'workflow_dispatch'):
            with self.subTest(event=event):
                status, calls = self.invoke_step(event, True)
                self.assertNotEqual(0, status)
                self.assertIn('--head head-sha', calls[2])
                self.assertNotIn('--base', calls[2])


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
                    'kind': 'local_probe', 'note': 'hardware=synthetic calibration=uncertain'}
        self.config = {'schema_version': 1, 'budget_seconds': 1800, 'shared_inputs': self.shared.copy(), 'targets': {}}
        timing = runtime.digest(runtime.canonical({'shared_inputs': self.shared, 'target_sources': self.targets}))
        for shard, path in self.shards.items():
            self.config['targets'][path] = {'source_sha256': self.targets[path], 'evidence': [evidence.copy()], 'shards': {shard: {
                'selection_sha256': runtime.digest(runtime.canonical(runtime.workflow.shard_config(shard, self.source[path]))),
                'timing_inputs_sha256': timing, 'main_mutants': 1, 'main_work_seconds': 2000,
                'parallelism': 2, 'setup_seconds': 100, 'isolated_seconds': 300, 'estimated_seconds': 1400,
                'basis': 'example; observed_workers=2; conservative weighting; local calibration uncertain'}}}

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

    def test_only_measured_static_owner_has_budget_exception(self):
        path = 'VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs'
        data = (runtime.ROOT / 'src/Parchwell.Verifier' / path).read_bytes()
        shard = 'configuration-static'
        self.shards.clear()
        self.shards[shard] = path
        self.source = {path: data}
        self.targets = {path: runtime.digest(data)}
        timing = runtime.digest(runtime.canonical({'shared_inputs': self.shared, 'target_sources': self.targets}))
        evidence = {'source_commit': 'a' * 40, 'locator': 'artifact 14108519875',
                    'sha256': '7eb4a26b2498293a80a6d7b91155ea2eca785da32b59ed05bdce730bffe3ce38',
                    'kind': 'hosted_complete', 'note': 'observed hosted evidence'}
        item = {'selection_sha256': runtime.digest(runtime.canonical(runtime.workflow.shard_config(shard, data))),
                'timing_inputs_sha256': timing, 'main_mutants': 0, 'main_work_seconds': 0,
                'parallelism': 1, 'setup_seconds': 208, 'isolated_seconds': 1892.542,
                'estimated_seconds': 2101, 'basis': 'artifact 14108519875 [11134,11241) observed_workers=1'}
        record = {'source_sha256': self.targets[path], 'evidence': [evidence], 'shards': {shard: item}}
        self.config['targets'] = {path: record}
        with mock.patch.object(runtime.workflow, 'shard_span', return_value=(11133, 11241)) as span, \
             mock.patch.object(runtime.workflow, 'shard_static_regions', return_value=[(11134, 11241)]) as regions:
            self.assertEqual(set(), self.errors())
            for field, value in [('main_mutants', 1), ('main_work_seconds', 1),
                                 ('setup_seconds', 209), ('isolated_seconds', 1892.543),
                                 ('basis', 'observed_workers=1')]:
                with self.subTest(field=field):
                    old = item[field]
                    item[field] = value
                    self.assertIn('shard_over_budget', self.errors())
                    item[field] = old
            span.return_value = (11134, 11241)
            self.assertIn('shard_over_budget', self.errors())
            span.return_value = (11133, 11241)
            regions.return_value = [(11134, 11241), (11241, 11371)]
            self.assertIn('shard_over_budget', self.errors())
            regions.return_value = [(11134, 11241)]
            evidence['sha256'] = 'f' * 64
            self.assertIn('shard_over_budget', self.errors())
            evidence['sha256'] = '7eb4a26b2498293a80a6d7b91155ea2eca785da32b59ed05bdce730bffe3ce38'
            item.update(isolated_seconds=1000, estimated_seconds=1196)
            self.assertIn('invalid_estimate', self.errors())
            item.update(isolated_seconds=1892.542, estimated_seconds=2101)
            self.source[path] += b'changed'
            self.assertIn('shard_over_budget', self.errors())
            self.source[path] = data
            span.return_value = (370, 596)
            regions.return_value = [(370, 595)]
            evidence.update(locator='artifact 14108227262',
                            sha256='68f25d8a4090d13ed253cb33bb69be5ecc852367210b1fe36016d5272c2ec911')
            item.update(setup_seconds=208, isolated_seconds=1743.157, estimated_seconds=1952,
                        basis='artifact 14108227262 [370,595) observed_workers=1')
            self.assertEqual(set(), self.errors())
            item.update(isolated_seconds=1743.156, estimated_seconds=1952)
            self.assertIn('invalid_estimate', self.errors())
            self.assertIn('shard_over_budget', self.errors())
            item.update(isolated_seconds=1743.157, setup_seconds=209, estimated_seconds=1953)
            self.assertIn('shard_over_budget', self.errors())

    def test_codec_cohorts_require_measured_costs_and_stay_under_budget(self):
        import copy
        config = json.loads((runtime.ROOT / 'tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json').read_text())
        path = runtime.CODEC_TARGET
        record = config['targets'][path]
        source = (runtime.ROOT / 'src/Parchwell.Verifier' / path).read_bytes()
        for shard in runtime.CODEC_PROVISIONAL_SHARDS:
            with self.subTest(shard=shard):
                item = record['shards'][shard]
                self.assertTrue(runtime.approved_codec_static_owner(path, record, shard, item, source))
                self.assertLessEqual(item['estimated_seconds'], 1800)
                changed = copy.deepcopy(item)
                changed['isolated_seconds'] -= 1
                self.assertFalse(runtime.approved_codec_static_owner(path, record, shard, changed, source))
                changed = copy.deepcopy(record)
                changed['evidence'] = [e for e in changed['evidence'] if e['sha256'] != runtime.CODEC_EVIDENCE[-1][1]]
                self.assertFalse(runtime.approved_codec_static_owner(path, changed, shard, item, source))
                with mock.patch.dict(runtime.workflow.COHORTS, {shard: ('wrong', ())}):
                    self.assertFalse(runtime.approved_codec_static_owner(path, record, shard, item, source))

    def test_codec_over_budget_has_no_exception(self):
        path = runtime.CODEC_TARGET
        data = (runtime.ROOT / 'src/Parchwell.Verifier' / path).read_bytes()
        shard = 'b10-final-projection-codec-p1-a'
        self.shards.clear()
        self.shards[shard] = path
        self.source = {path: data}
        self.targets = {path: runtime.digest(data)}
        timing = runtime.digest(runtime.canonical({'shared_inputs': self.shared, 'target_sources': self.targets}))
        item = {'selection_sha256': runtime.digest(runtime.canonical(runtime.workflow.shard_config(shard, data))),
                'timing_inputs_sha256': timing, 'main_mutants': 0, 'main_work_seconds': 0,
                'parallelism': 2, 'setup_seconds': 60, 'isolated_seconds': 1800,
                'estimated_seconds': 1860, 'basis': 'observed_workers=2'}
        self.config['targets'] = {path: {'source_sha256': self.targets[path], 'evidence': [], 'shards': {shard: item}}}
        self.assertIn('shard_over_budget', self.errors())

    def test_repeat_merged_estimate_is_pinned_and_required(self):
        import copy
        config = json.loads((runtime.ROOT / 'tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json').read_text())
        record = config['targets'][runtime.REPEAT_TARGET]
        item = record['shards'][runtime.REPEAT_MERGED_SHARD]
        source = (runtime.ROOT / 'src/Parchwell.Verifier' / runtime.REPEAT_TARGET).read_bytes()
        self.assertTrue(runtime.approved_repeat_merged_owner(runtime.REPEAT_TARGET, record, runtime.REPEAT_MERGED_SHARD, item, source))
        self.assertLessEqual(item['estimated_seconds'], 1800)
        for field in runtime.NUMERIC_FIELDS:
            candidate = copy.deepcopy(config)
            candidate['targets'][runtime.REPEAT_TARGET]['shards'][runtime.REPEAT_MERGED_SHARD][field] = None
            with self.subTest(field=field), self.assertRaises(ValueError):
                runtime.shape(candidate)
        changed = copy.deepcopy(item)
        changed['main_work_seconds'] -= 1
        self.assertFalse(runtime.approved_repeat_merged_owner(runtime.REPEAT_TARGET, record, runtime.REPEAT_MERGED_SHARD, changed, source))
        changed = copy.deepcopy(record)
        changed['evidence'] = [e for e in changed['evidence'] if '132540792966' not in e['locator']]
        self.assertFalse(runtime.approved_repeat_merged_owner(runtime.REPEAT_TARGET, changed, runtime.REPEAT_MERGED_SHARD, item, source))
        self.assertFalse(runtime.approved_repeat_merged_owner(runtime.REPEAT_TARGET, record, runtime.REPEAT_MERGED_SHARD, item, source + b'changed'))

    def test_pinned_native_geometry_rejects_tampering(self):
        self.workflow_patcher.stop()
        self.config_patcher.stop()
        import copy
        config = json.loads((runtime.ROOT / 'tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json').read_text())
        for path in runtime.NATIVE_GEOMETRY_BINDINGS:
            with self.subTest(path=path):
                record = copy.deepcopy(config['targets'][path])
                source = (runtime.ROOT / 'src/Parchwell.Verifier' / path).read_bytes()
                self.assertTrue(runtime.native_geometry_valid(path, record, source))
                self.assertFalse(runtime.native_geometry_valid(path, record, source + b'changed'))
                for field in ('inventory_sha256', 'ownership_proof_sha256', 'layout_sha256', 'source_commit'):
                    changed = copy.deepcopy(record)
                    changed['native_geometry'][field] = '0' * len(changed['native_geometry'][field])
                    self.assertFalse(runtime.native_geometry_valid(path, changed, source))
                changed = copy.deepcopy(record)
                owner = next(iter(changed['native_geometry']['owner_counts']))
                changed['native_geometry']['owner_counts'][owner] += 1
                self.assertFalse(runtime.native_geometry_valid(path, changed, source))
                changed = copy.deepcopy(record)
                changed['evidence'] = [e for e in changed['evidence'] if e['sha256'] != changed['native_geometry']['archive_sha256']]
                self.assertFalse(runtime.native_geometry_valid(path, changed, source))
                with mock.patch.object(runtime.workflow, 'shard_config', return_value={'changed': True}):
                    self.assertFalse(runtime.native_geometry_valid(path, record, source))

    def test_pinned_codec_owners_cannot_be_unknown(self):
        import copy
        config = json.loads((runtime.ROOT / 'tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json').read_text())
        for shard in runtime.CODEC_PROVISIONAL_SHARDS:
            candidate = copy.deepcopy(config)
            item = candidate['targets'][runtime.CODEC_TARGET]['shards'][shard]
            for field in runtime.NUMERIC_FIELDS:
                item[field] = None
            with self.subTest(shard=shard), self.assertRaises(ValueError):
                runtime.shape(candidate)

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
        self.assertEqual(runtime.execution_runner(data), runtime.execution_runner(data.replace(b'(3600, 4517,', b'(3601, 4517,')))
        self.assertNotEqual(runtime.execution_runner(data), runtime.execution_runner(data.replace(b'REBALANCE_SECONDS = 1800', b'REBALANCE_SECONDS = 1801')))

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

class ReviewRegressionTests(unittest.TestCase):
    setUp = QualificationTests.setUp
    errors = QualificationTests.errors
    def test_observed_workers_bound(self):
        item = self.config['targets']['a.cs']['shards']['a-p1']
        item['parallelism'] = 3
        item['estimated_seconds'] = 1067
        self.assertIn('missing_measurement', self.errors())
        item['basis'] = 'no observation'
        self.assertIn('missing_measurement', self.errors())

    def test_oversized_numbers_do_not_escape(self):
        item = self.config['targets']['a.cs']['shards']['a-p1']
        item['main_work_seconds'] = 10 ** 400
        self.assertIn('invalid_estimate', self.errors())
        item.update(main_work_seconds=1e308, setup_seconds=1e308, isolated_seconds=1e308)
        self.assertIn('invalid_estimate', self.errors())

    def test_qualification_only_change_requires_check(self):
        import json
        data = json.dumps(self.config)
        with tempfile.TemporaryDirectory() as directory:
            file = Path(directory) / 'qualification.json'
            file.write_text(data)
            def fake_git(*args):
                if args[0] == 'rev-parse': return b'abc\n'
                if args[0] == 'diff': return b'tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json\0'
                raise AssertionError(args)
            with mock.patch.object(runtime, 'git', side_effect=fake_git), \
                 mock.patch.object(runtime, 'blob', side_effect=lambda ref, path: Path(runtime.workflow.__file__).read_bytes() if path == runtime.RUNNER else b'x'), \
                 mock.patch.object(runtime, 'input_maps', return_value=(self.shared, self.targets)), \
                 mock.patch.object(runtime, 'workflow_matrix', return_value=self.shards), \
                 mock.patch.object(runtime, 'validate', return_value={'shard_over_budget'}):
                self.assertEqual({'status': 'rejected', 'reasons': ['shard_over_budget']}, runtime.check('base', 'head', file))


class AdditionalQualificationTests(unittest.TestCase):
    setUp = QualificationTests.setUp
    errors = QualificationTests.errors

    def simulated_check(self, change, old_shared=None, old_targets=None):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'qualification.json'
            path.write_text(json.dumps(self.config))
            runner = Path(runtime.workflow.__file__).read_bytes()
            original_git = runtime.git
            def git(*args):
                if args[0] == 'rev-parse':
                    return (b'base\n' if args[-1].startswith('base') else b'head\n')
                if args[0] == 'diff':
                    return (change + '\0').encode()
                return original_git(*args)
            def blob(ref, name):
                if name == runtime.RUNNER:
                    return runner
                if name == runtime.WORKFLOW:
                    return b'matrix'
                return self.source[name.removeprefix('src/Parchwell.Verifier/')]
            def input_maps(ref):
                return ((old_shared if old_shared is not None else self.shared),
                        (old_targets if old_targets is not None else self.targets)) if ref == 'base' else (self.shared, self.targets)
            with mock.patch.object(runtime, 'git', side_effect=git), mock.patch.object(runtime, 'blob', side_effect=blob), \
                 mock.patch.object(runtime, 'input_maps', side_effect=input_maps), \
                 mock.patch.object(runtime, 'workflow_matrix', return_value=self.shards):
                return runtime.check('base', 'head', path)

    def test_irrelevant_and_excluded_change_is_not_applicable(self):
        for path in ('README.md', 'items/474/state.md', 'src/Parchwell.LoadGenerator/Program.cs',
                     'tests/Parchwell.Verifier.Tests/Component/Example.cs'):
            with self.subTest(path=path):
                self.assertEqual({'status': 'not_applicable', 'reasons': []}, self.simulated_check(path))

    def test_runtime_limits_json_is_execution_data(self):
        path = 'docs/operations/a01-runtime-limits.json'
        self.shared[path] = runtime.digest(b'new')
        old = dict(self.shared)
        old[path] = runtime.digest(b'old')
        result = self.simulated_check(path, old_shared=old)
        self.assertEqual('rejected', result['status'])
        self.assertIn('stale_inputs', result['reasons'])

    def test_new_target_has_no_default_pass(self):
        self.targets['new.cs'] = runtime.digest(b'new')
        self.source['new.cs'] = b'new'
        old = {p: h for p, h in self.targets.items() if p != 'new.cs'}
        result = self.simulated_check('src/Parchwell.Verifier/new.cs', old_targets=old)
        self.assertEqual('rejected', result['status'])
        self.assertIn('missing_qualification', result['reasons'])

    def test_matching_matrix_capacity_boundary(self):
        for count in (255, 256, 257):
            with self.subTest(count=count):
                shard_map = {f's{i}': 'a.cs' for i in range(count)}
                original = self.config['targets']['a.cs']['shards']['a-p1']
                shards = {}
                for shard in shard_map:
                    item = original.copy()
                    item['selection_sha256'] = runtime.digest(runtime.canonical(runtime.workflow.shard_config(shard, self.source['a.cs'])))
                    shards[shard] = item
                config = copy.deepcopy(self.config)
                config['targets']['a.cs']['shards'] = shards
                config['targets']['b.cs']['shards'] = {}
                with mock.patch.object(runtime.workflow, 'SHARDS', shard_map):
                    reasons = runtime.validate(config, self.shared, self.targets, self.source, shard_map)
                self.assertEqual({'matrix_over_capacity'} if count == 257 else set(), reasons)

    def test_local_rate_basis_records_uncertainty(self):
        item = self.config['targets']['a.cs']['shards']['a-p1']
        self.assertIn('local calibration uncertain', item['basis'])
        self.assertNotIn('missing_measurement', self.errors())


class AdditionalProjectAndLauncherTests(unittest.TestCase):
    def test_unknown_inclusion_rejected(self):
        names = runtime.files('HEAD')
        original_blob = runtime.blob
        def changed_blob(ref, path):
            data = original_blob(ref, path)
            if path == runtime.TEST_PROJECT:
                return data.replace(b'</Project>', b'<ItemGroup><Compile Include="new/**/*.cs" /></ItemGroup></Project>')
            return data
        with mock.patch.object(runtime, 'blob', side_effect=changed_blob):
            with self.assertRaises(ValueError):
                runtime.project_closure('HEAD', names)

    def test_timeout_launcher_kills_group_without_retry(self):
        process = mock.Mock(pid=123, returncode=-9)
        process.wait.side_effect = [runtime.subprocess.TimeoutExpired('fake-child', 600), -9]
        with tempfile.TemporaryDirectory() as directory, \
             mock.patch.object(runtime.subprocess, 'Popen', return_value=process) as popen, \
             mock.patch.object(runtime.os, 'killpg') as killpg, \
             mock.patch.object(runtime.signal, 'signal'), mock.patch.object(runtime.signal, 'getsignal'):
            result = runtime.profile_launch(['fake-child'], directory, Path(directory) / 'log', 600)
        self.assertEqual((-9, 'timeout'), result)
        popen.assert_called_once()
        killpg.assert_called_once_with(123, runtime.signal.SIGKILL)
        self.assertEqual(2, process.wait.call_count)

    def test_excluded_component_source_does_not_change_inputs(self):
        names = runtime.files('HEAD')
        excluded = next(p for p in names if p.startswith('tests/Parchwell.Verifier.Tests/Component/') and p.endswith('.cs'))
        included = next(p for p in names if p.startswith('tests/Parchwell.Verifier.Tests/Host/') and p.endswith('.cs') and p not in {
            'tests/Parchwell.Verifier.Tests/' + value for value in runtime.EXCLUDED})
        actual_blob = runtime.blob
        cache = {}
        changed = set()
        def get_blob(ref, path):
            if path in changed:
                return b'changed synthetic contents'
            if path not in cache:
                cache[path] = actual_blob(ref, path)
            return cache[path]
        with mock.patch.object(runtime, 'blob', side_effect=get_blob), mock.patch.object(runtime, 'files', return_value=names):
            before = runtime.input_maps('HEAD')
            changed.add(excluded)
            self.assertEqual(before, runtime.input_maps('HEAD'))
            changed.add(included)
            self.assertNotEqual(before, runtime.input_maps('HEAD'))
    def test_excluded_component_source_does_not_change_inputs(self):
        names = runtime.files('HEAD')
        excluded = next(p for p in names if p.startswith('tests/Parchwell.Verifier.Tests/Component/') and p.endswith('.cs'))
        included = next(p for p in names if p.startswith('tests/Parchwell.Verifier.Tests/Host/') and p.endswith('.cs') and p not in {
            'tests/Parchwell.Verifier.Tests/' + value for value in runtime.EXCLUDED})
        actual_blob = runtime.blob
        cache = {}
        changed = set()
        def get_blob(ref, path):
            if path in changed:
                return b'changed synthetic contents'
            if path not in cache:
                cache[path] = actual_blob(ref, path)
            return cache[path]
        with mock.patch.object(runtime, 'blob', side_effect=get_blob), mock.patch.object(runtime, 'files', return_value=names):
            before = runtime.input_maps('HEAD')
            changed.add(excluded)
            self.assertEqual(before, runtime.input_maps('HEAD'))
            changed.add(included)
            self.assertNotEqual(before, runtime.input_maps('HEAD'))

    def test_probe_budget_and_failed_invocation(self):
        with tempfile.TemporaryDirectory() as directory:
            tally = Path(directory) / 'tally.json'
            output = Path(directory) / 'probe'
            calls = []
            def failed(*args, **kwargs):
                calls.append(args)
                return 1
            self.assertEqual(1, runtime.bounded_probe('a.cs', '0..1', output, tally, launcher=failed))
            self.assertEqual(1, len(json.loads(tally.read_text())))
            self.assertEqual(1, len(calls))
            tally.write_text(json.dumps([{}] * 24))
            with self.assertRaises(ValueError):
                runtime.bounded_probe('a.cs', '0..1', output, tally, launcher=failed)
            self.assertEqual(1, len(calls))
            output.mkdir()
            tally.write_text('[]')
            with self.assertRaises(ValueError):
                runtime.bounded_probe('a.cs', '0..1', output, tally, launcher=failed)
            self.assertEqual([], json.loads(tally.read_text()))

    def test_local_probe_without_calibration_is_missing_measurement(self):
        case = AdditionalQualificationTests()
        case.setUp()
        try:
            case.config['targets']['a.cs']['evidence'][0]['note'] = 'no calibration'
            self.assertIn('missing_measurement', case.errors())
        finally:
            case.doCleanups()

    def test_ancestor_build_input_is_tracked(self):
        names = runtime.files('HEAD')
        actual_blob = runtime.blob
        ancestor = 'src/Directory.Build.props'
        def get_blob(ref, path):
            return b'<Project />' if path == ancestor else actual_blob(ref, path)
        with mock.patch.object(runtime, 'blob', side_effect=get_blob):
            with mock.patch.object(runtime, 'files', return_value=names):
                before, _ = runtime.input_maps('HEAD')
            with mock.patch.object(runtime, 'files', return_value=names | {ancestor}):
                after, _ = runtime.input_maps('HEAD')
        self.assertNotIn(ancestor, before)
        self.assertIn(ancestor, after)

class FinalReviewRegressionTests(AdditionalQualificationTests):
    def test_malformed_shard_precedes_scope_bypass(self):
        self.config['targets']['a.cs']['shards']['a-p1'] = 'bad'
        self.assertEqual({'status': 'rejected', 'reasons': ['invalid_input']}, self.simulated_check('README.md'))
        self.config['targets']['a.cs']['shards']['a-p1'] = {}
        self.assertEqual({'status': 'rejected', 'reasons': ['invalid_input']}, self.simulated_check('README.md'))

    def test_shared_additions_deletions_reject_stale(self):
        for path in ('tests/fixtures/verifier-v1/synthetic.json', 'src/Directory.Build.props',
                     'scenarios/profiles/synthetic.yaml'):
            with self.subTest(path=path):
                old = self.shared.copy()
                self.shared[path] = runtime.digest(b'synthetic')
                self.assertIn('stale_inputs', self.simulated_check(path, old_shared=old)['reasons'])
                self.config['shared_inputs'] = self.shared.copy()
                previous = self.shared.copy()
                del self.shared[path]
                self.assertIn('stale_inputs', self.simulated_check(path, old_shared=previous)['reasons'])
                self.config['shared_inputs'] = self.shared.copy()

    def test_build_inclusion_fails_closed(self):
        names = runtime.files('HEAD')
        actual_blob = runtime.blob
        def malicious(ref, path):
            if path == 'src/Directory.Build.targets':
                return b'<Project><Import Project="../outside.targets" /></Project>'
            return actual_blob(ref, path)
        with mock.patch.object(runtime, 'files', return_value=names | {'src/Directory.Build.targets'}), \
             mock.patch.object(runtime, 'blob', side_effect=malicious):
            with self.assertRaises(ValueError):
                runtime.input_maps('HEAD')

class DependencyEndToEndTests(unittest.TestCase):
    def test_actual_discovery_addition_and_deletion(self):
        names = runtime.files('HEAD')
        actual_blob = runtime.blob
        cache = {}
        additions = ('tests/fixtures/verifier-v1/runtime-test.json', 'src/Directory.Build.props',
                     'scenarios/profiles/runtime-test.yaml')
        def blob(ref, path):
            if path == runtime.RUNNER:
                return Path(runtime.workflow.__file__).read_bytes()
            if path in additions:
                return b'<Project />' if path.endswith('.props') else b'synthetic'
            if path not in cache:
                cache[path] = runtime.subprocess.check_output(['git', 'show', f'HEAD:{path}'], cwd=ROOT)
                # This dependency fixture uses synthetic estimates, not the measured static-owner workload.
                if path in {'src/Parchwell.Verifier/' + runtime.STATIC_TARGET,
                            'src/Parchwell.Verifier/' + runtime.CODEC_TARGET}:
                    cache[path] += b'\n'
            return cache[path]
        def files(ref):
            return names | ({current} if ref == 'added' else set())
        def git(*args):
            if args[0] == 'rev-parse': return args[-1].replace('^{commit}', '').encode()
            if args[0] == 'diff': return (current + '\0').encode()
            raise AssertionError(args)
        with mock.patch.object(runtime, 'files', side_effect=files), mock.patch.object(runtime, 'blob', side_effect=blob), \
             mock.patch.object(runtime, 'git', side_effect=git), \
             mock.patch.object(runtime, 'workflow_matrix', return_value=runtime.workflow.SHARDS):
            base_shared, targets = runtime.input_maps('base')
            sources = {p: blob('base', 'src/Parchwell.Verifier/' + p) for p in targets}
            def config(shared):
                binding = runtime.digest(runtime.canonical({'shared_inputs': shared, 'target_sources': targets}))
                evidence = {'source_commit': 'a' * 40, 'locator': 'synthetic', 'sha256': 'a' * 64,
                            'kind': 'hosted_complete', 'note': 'synthetic'}
                result = {'schema_version': 1, 'budget_seconds': 1800, 'shared_inputs': shared,
                          'targets': {p: {'source_sha256': h, 'evidence': [evidence], 'shards': {}} for p, h in targets.items()}}
                for shard, target in runtime.workflow.SHARDS.items():
                    result['targets'][target]['shards'][shard] = {
                        'selection_sha256': runtime.digest(runtime.canonical(runtime.workflow.shard_config(shard, sources[target]))),
                        'timing_inputs_sha256': binding, 'main_mutants': 1, 'main_work_seconds': 20,
                        'parallelism': 1, 'setup_seconds': 10, 'isolated_seconds': 0,
                        'estimated_seconds': 30, 'basis': 'synthetic observed_workers=1'}
                return result
            with tempfile.TemporaryDirectory() as directory:
                qualification = Path(directory) / 'q.json'
                for current in additions:
                    with self.subTest(current=current):
                        added_shared, _ = runtime.input_maps('added')
                        qualification.write_text(json.dumps(config(base_shared)))
                        self.assertEqual({'status': 'rejected', 'reasons': ['stale_inputs']}, runtime.check('base', 'added', qualification))
                        qualification.write_text(json.dumps(config(added_shared)))
                        self.assertEqual({'status': 'rejected', 'reasons': ['stale_inputs']}, runtime.check('added', 'base', qualification))

    def test_namespaced_build_import_rejected(self):
        names = runtime.files('HEAD')
        original = runtime.blob
        new = 'src/Directory.Build.targets'
        def blob(ref, path):
            return (b'<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><Import Project="../outside.targets" /></Project>'
                    if path == new else original(ref, path))
        with mock.patch.object(runtime, 'files', return_value=names | {new}), mock.patch.object(runtime, 'blob', side_effect=blob):
            with self.assertRaises(ValueError):
                runtime.input_maps('HEAD')

    def test_numeric_schema_rejects_object_before_bypass(self):
        case = AdditionalQualificationTests()
        case.setUp()
        try:
            case.config['targets']['a.cs']['shards']['a-p1']['main_mutants'] = {}
            self.assertEqual({'status': 'rejected', 'reasons': ['invalid_input']}, case.simulated_check('README.md'))
        finally:
            case.doCleanups()

    def test_numeric_values_never_bypass_schema(self):
        for field, value in [('main_mutants', True), ('parallelism', 0), ('main_work_seconds', -1),
                             ('estimated_seconds', 1399), ('setup_seconds', 10 ** 400)]:
            with self.subTest(field=field):
                case = AdditionalQualificationTests()
                case.setUp()
                try:
                    case.config['targets']['a.cs']['shards']['a-p1'][field] = value
                    self.assertEqual({'status': 'rejected', 'reasons': ['invalid_estimate']}, case.simulated_check('README.md'))
                finally:
                    case.doCleanups()

    def test_all_automatically_selected_build_xml_reject_namespaces(self):
        names = runtime.files('HEAD')
        original = runtime.blob
        for name in ('Directory.Build.props', 'Directory.Packages.props', 'src/Directory.Build.targets', runtime.TEST_PROJECT):
            with self.subTest(name=name):
                def blob(ref, path):
                    if path == name:
                        return b'<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><Import Project="other.targets" /></Project>'
                    return original(ref, path)
                with mock.patch.object(runtime, 'files', return_value=names | {name}), mock.patch.object(runtime, 'blob', side_effect=blob):
                    with self.assertRaises(ValueError):
                        runtime.input_maps('HEAD')


class ProvisionalQualificationTests(unittest.TestCase):
    setUp = QualificationTests.setUp
    errors = QualificationTests.errors
    simulated_check = AdditionalQualificationTests.simulated_check

    def provisional(self):
        source_hash = self.targets['a.cs']
        self.provisional_patch = mock.patch.object(runtime, 'PROVISIONAL_TARGETS',
            {'a.cs': (source_hash, '42', 'f' * 64)})
        self.provisional_patch.start()
        self.addCleanup(self.provisional_patch.stop)
        selection = self.config['targets']['a.cs']['shards']['a-p1']['selection_sha256']
        layout_hash = runtime.digest(runtime.canonical({'a-p1': selection}))
        layout_patch = mock.patch.object(runtime, 'PROVISIONAL_LAYOUT_HASHES', {'a.cs': layout_hash})
        layout_patch.start()
        self.addCleanup(layout_patch.stop)
        timing = self.config['targets']['a.cs']['shards']['a-p1']['timing_inputs_sha256']
        timing_patch = mock.patch.object(runtime, 'PROVISIONAL_TIMING_INPUTS_HASH', timing)
        timing_patch.start()
        self.addCleanup(timing_patch.stop)
        record = self.config['targets']['a.cs']
        record['geometry_status'] = runtime.PROVISIONAL_STATUS
        record['native_geometry'] = {}
        geometry_patch = mock.patch.object(runtime, 'native_geometry_valid', return_value=True)
        geometry_patch.start()
        self.addCleanup(geometry_patch.stop)
        record['evidence'] = [{'source_commit': 'e7b303f3d2bb7c6d54e7be804cc5bf8bf83aa2a5',
            'locator': 'artifact 42', 'sha256': 'f' * 64, 'kind': 'partial_timing',
            'note': 'cancelled; timing only'}]
        item = record['shards']['a-p1']
        for field in runtime.NUMERIC_FIELDS:
            item[field] = None
        item['basis'] = 'source-based cut; native geometry and timing not established'
        return record, item

    def test_pilot_is_explicit_and_other_checks_remain(self):
        record, item = self.provisional()
        self.assertEqual(set(), self.errors())
        self.assertEqual({'status': 'provisional', 'reasons': [], 'pending_targets': ['a.cs']},
                         self.simulated_check(runtime.RUNNER))
        item['selection_sha256'] = '0' * 64
        self.assertIn('layout_mismatch', self.errors())
        item['selection_sha256'] = runtime.digest(runtime.canonical(runtime.workflow.shard_config('a-p1', self.source['a.cs'])))
        item['timing_inputs_sha256'] = '0' * 64
        self.assertIn('stale_inputs', self.errors())
        self.config['targets']['b.cs']['shards']['b']['estimated_seconds'] = 1801
        self.assertIn('invalid_estimate', self.errors())

    def test_pilot_requires_pinned_source_and_partial_evidence(self):
        record, _ = self.provisional()
        record['source_sha256'] = '0' * 64
        self.assertIn('stale_inputs', self.errors())
        record['source_sha256'] = self.targets['a.cs']
        record['evidence'][0]['sha256'] = '0' * 64
        self.assertIn('missing_measurement', self.errors())
        record['evidence'][0]['sha256'] = 'f' * 64
        record['evidence'][0]['kind'] = 'hosted_complete'
        self.assertIn('missing_measurement', self.errors())
        record['evidence'].clear()
        self.assertIn('missing_measurement', self.errors())

    def test_pilot_rejects_unknown_targets_or_fabricated_values_before_bypass(self):
        record, item = self.provisional()
        item['main_mutants'] = 0
        with self.assertRaises(ValueError):
            runtime.shape(self.config)
        self.assertEqual({'status': 'rejected', 'reasons': ['invalid_input']}, self.simulated_check('README.md'))
        item['main_mutants'] = None
        self.config['targets']['b.cs']['geometry_status'] = runtime.PROVISIONAL_STATUS
        with self.assertRaises(ValueError):
            runtime.shape(self.config)
        self.assertEqual({'status': 'rejected', 'reasons': ['invalid_input']}, self.simulated_check('README.md'))

    def test_rebound_cut_cannot_authorize_another_provisional_layout(self):
        _, item = self.provisional()
        self.assertEqual(set(), self.errors())
        with mock.patch.object(runtime.workflow, 'shard_config', side_effect=lambda shard, source:
                               {'shard': shard, 'source': source.decode(), 'cut': 20}):
            item['selection_sha256'] = runtime.digest(runtime.canonical(runtime.workflow.shard_config('a-p1', self.source['a.cs'])))
            self.assertIn('layout_mismatch', self.errors())

    def rebind_timing(self):
        binding = runtime.digest(runtime.canonical({'shared_inputs': self.config['shared_inputs'],
            'target_sources': {path: record['source_sha256'] for path, record in self.config['targets'].items()}}))
        for record in self.config['targets'].values():
            for item in record['shards'].values():
                item['timing_inputs_sha256'] = binding

    def test_rebound_shared_input_cannot_authorize_another_pilot(self):
        self.provisional()
        self.shared['global.json'] = runtime.digest(b'changed sdk')
        self.config['shared_inputs'] = self.shared.copy()
        self.rebind_timing()
        self.assertIn('stale_inputs', self.errors())

    def test_rebound_other_target_cannot_authorize_another_pilot(self):
        self.provisional()
        self.source['b.cs'] = b'changed target'
        self.targets['b.cs'] = runtime.digest(self.source['b.cs'])
        self.config['targets']['b.cs']['source_sha256'] = self.targets['b.cs']
        self.config['targets']['b.cs']['shards']['b']['selection_sha256'] = runtime.digest(
            runtime.canonical(runtime.workflow.shard_config('b', self.source['b.cs'])))
        self.rebind_timing()
        self.assertIn('stale_inputs', self.errors())

    def test_qualified_targets_can_rebind_changed_inputs(self):
        self.shared['global.json'] = runtime.digest(b'changed sdk')
        self.config['shared_inputs'] = self.shared.copy()
        self.source['b.cs'] = b'changed target'
        self.targets['b.cs'] = runtime.digest(self.source['b.cs'])
        self.config['targets']['b.cs']['source_sha256'] = self.targets['b.cs']
        self.config['targets']['b.cs']['shards']['b']['selection_sha256'] = runtime.digest(
            runtime.canonical(runtime.workflow.shard_config('b', self.source['b.cs'])))
        self.rebind_timing()
        self.assertEqual(set(), self.errors())

    def test_qualified_target_can_use_a_reassessed_cut(self):
        with mock.patch.object(runtime.workflow, 'shard_config', side_effect=lambda shard, source:
                               {'shard': shard, 'source': source.decode(), 'cut': 20}):
            for shard, path in self.shards.items():
                self.config['targets'][path]['shards'][shard]['selection_sha256'] = runtime.digest(
                    runtime.canonical(runtime.workflow.shard_config(shard, self.source[path])))
            self.assertEqual(set(), self.errors())

    def test_pilot_preserves_matrix_and_capacity_checks(self):
        self.provisional()
        matrix = dict(self.shards)
        matrix['extra'] = 'a.cs'
        self.assertIn('layout_mismatch', runtime.validate(self.config, self.shared, self.targets, self.source, matrix))
        matrix = {f'job{i}': 'a.cs' for i in range(257)}
        self.assertIn('matrix_over_capacity', runtime.validate(self.config, self.shared, self.targets, self.source, matrix))

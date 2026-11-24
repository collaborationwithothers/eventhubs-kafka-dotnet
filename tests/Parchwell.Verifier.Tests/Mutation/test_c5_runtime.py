import importlib.util
import fnmatch
from functools import cache
import contextlib
import json
import math
from pathlib import Path
import shlex
import shutil
import tempfile
import unittest
import os
import subprocess

import c5_runtime as profile_module
ROOT = Path(__file__).resolve().parents[3]
APPROVED_PROJECTION_PAIR = ('b1c9a96740111676caa0b9e95db90b4ff93f07ff2317710233558412a88ac1bc',
                            '3771b676b8d5856eb34705dcaa3e9c34ffd8184dd514179a1bf85ee7535fb16f')
PROJECTION_PATH = 'tests/Parchwell.Verifier.Tests/Mutation/c5_projection.py'
APPROVED_RUNNER_PAIR = ('dcf4ac956fe262a3178d0673071811fef63bf1a357ae42df7ea14629a0af4625',
                        'c147bb8000ef1273609052660e80b20c8da8bf33a77a0df65a94d32a5f7a1c40')
RUNNER_PATH = 'tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py'


class ExecutionWorkflowTests(unittest.TestCase):
    def test_timing_fingerprint_ignores_only_the_reviewed_companion_action_upgrade(self):
        step = (b"      - name: Download companion shard evidence\n"
                b"        if: always() && steps.campaign.outputs.run-id != ''\n")
        old = b'        uses: actions/download-artifact@d3f86a106a0bac45b974a628896c90dbdf5c8093\n'
        new = b'        uses: actions/download-artifact@9000827ccba6bdab643e8b6fd33ac0654aef8333 # v8.0.2\n'
        placement = (b'        include:\n          - shard: shard-a\n    env:\n'
                     b'  aggregate:\n      - name: Download coordinator shard evidence\n')
        baseline = placement + old + step + old
        updated = placement + old + step + new
        fingerprint = profile_module.execution_workflow(baseline)
        self.assertEqual(fingerprint, profile_module.execution_workflow(updated))
        for other in (placement + new + step + new,
                      placement.replace(b'    env:\n', b'    env:\n      WORKER: changed\n') + old + step + new,
                      updated.replace(b' # v8.0.2', b' # another action')):
            with self.subTest(workflow=other):
                self.assertNotEqual(fingerprint, profile_module.execution_workflow(other))

    def test_timing_fingerprint_retains_the_exact_paced_companion_step_only(self):
        workflow = (ROOT / profile_module.WORKFLOW).read_bytes()
        previous = profile_module.blob('717c5a75e3f0a56ad5378a0e7551caa832ed63d8', profile_module.WORKFLOW)
        fingerprint = profile_module.execution_workflow(previous)
        self.assertEqual(fingerprint, profile_module.execution_workflow(workflow))
        for old, changed in (
                (b'c5_companion_artifacts.py', b'c5_other_artifacts.py'),
                (b'--manifest "$C5_CAMPAIGN"', b'--manifest "$OTHER_MANIFEST"'),
                (b'--campaign-manifest "$C5_CAMPAIGN"', b'--campaign-manifest "$OTHER_MANIFEST"'),
                (b'--output-dir "$C5_OUTPUT"', b'--output-dir "$OTHER_C5_OUTPUT"')):
            with self.subTest(old=old):
                self.assertIn(old, workflow)
                self.assertNotEqual(fingerprint, profile_module.execution_workflow(workflow.replace(old, changed, 1)))


class RequiredCheckIntegrationTests(unittest.TestCase):
    def invoke_step(self, event, fail_check, fail_discovery=False):
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
                            'if [[ "$1" = -m && "$FAIL_DISCOVERY" = yes ]]; then exit 1; fi\n'
                            'if [[ "$1" == *c5_runtime.py && "$FAIL_CHECK" = yes ]]; then exit 1; fi\n')
            stub.chmod(0o755)
            env = {**os.environ, 'PATH': str(directory) + os.pathsep + os.environ['PATH'],
                   'CALL_LOG': str(log), 'FAIL_CHECK': 'yes' if fail_check else 'no',
                   'FAIL_DISCOVERY': 'yes' if fail_discovery else 'no',
                   'PARCHWELL_EVENT': event, 'PARCHWELL_BASE_SHA': 'base-sha',
                   'PARCHWELL_HEAD_SHA': 'head-sha'}
            result = subprocess.run(['bash', '-c', step['run']], cwd=ROOT, env=env, check=False)
            return result.returncode, log.read_text().splitlines()

    def assert_c5_discovery(self, invocation):
        arguments = shlex.split(invocation)
        self.assertEqual(['-m', 'unittest', 'discover'], arguments[:3])
        self.assertEqual('tests/Parchwell.Verifier.Tests/Mutation', arguments[arguments.index('-s') + 1])
        pattern = arguments[arguments.index('-p') + 1]
        for module in ('test_c5_runtime.py', 'test_c5_workflow.py',
                       'test_c5_cohort_runner.py', 'test_c5_cohort_aggregate.py'):
            with self.subTest(module=module):
                self.assertTrue(fnmatch.fnmatchcase(module, pattern), invocation)

    def test_required_check_propagates_rejection(self):
        status, calls = self.invoke_step('pull_request', True)
        self.assertNotEqual(0, status)
        self.assertEqual(2, len(calls))
        self.assert_c5_discovery(calls[0])
        self.assertIn('--head head-sha', calls[1])
        self.assertIn('--base base-sha', calls[1])

    def test_discovery_failure_stops_before_qualification(self):
        status, calls = self.invoke_step('pull_request', False, fail_discovery=True)
        self.assertNotEqual(0, status)
        self.assertEqual(1, len(calls))
        self.assert_c5_discovery(calls[0])

    def test_main_validates_without_diff(self):
        for event in ('push', 'workflow_dispatch'):
            with self.subTest(event=event):
                status, calls = self.invoke_step(event, True)
                self.assertNotEqual(0, status)
                self.assertEqual(2, len(calls))
                self.assert_c5_discovery(calls[0])
                self.assertIn('--head head-sha', calls[1])
                self.assertNotIn('--base', calls[1])


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
            self.assertEqual(profile_module.workflow.OVERRIDES,
                             json.loads((output / 'command.json').read_text())['environment_overrides'])
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

    def test_profile_launch_uses_workflow_overrides(self):
        process = mock.Mock()
        process.wait.return_value = 0
        with tempfile.TemporaryDirectory() as directory, \
             mock.patch.dict(os.environ, {'APPLICATIONINSIGHTS_STATSBEAT_DISABLED': 'false'}), \
             mock.patch.object(profile_module.subprocess, 'Popen', return_value=process) as popen:
            self.assertEqual((0, None), profile_module.profile_launch(
                ['fake-child'], directory, Path(directory) / 'log', 600))
        for key, value in profile_module.workflow.OVERRIDES.items():
            self.assertEqual(value, popen.call_args.kwargs['env'][key])

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
    def test_historical_performance_reader_rejects_paired_campaigns(self):
        from unittest.mock import patch

        head = 'a' * 40
        with patch.object(profile_module.subprocess, 'check_output',
                          return_value=profile_module.COMPANION_WORKFLOW + '\n'):
            with self.assertRaisesRegex(ValueError, 'c5_campaign.py terminal'):
                profile_module.expected_layout_at_head(head, ROOT)

    def test_boundary(self):
        for wall, status in [(1799, 'met'), (1800, 'met'), (1801, 'met'), (2999, 'met'), (3000, 'met'), (3001, 'missed')]:
            with self.subTest(wall=wall):
                result = p.performance_report(*fixture(wall))
                self.assertEqual(result['budget_seconds'], 3000)
                self.assertEqual(result['status'], status)

    def test_incomplete_at_and_over_budget(self):
        for wall in (3000, 3001):
            with self.subTest(wall=wall):
                run, jobs, expected = fixture(wall)
                jobs['jobs'][0]['conclusion'] = 'failure'
                result = p.performance_report(run, jobs, expected)
                self.assertEqual(result['budget_seconds'], 3000)
                self.assertEqual(result['status'], 'incomplete')

    def test_compute_peak_queue_and_aggregation(self):
        run, jobs, expected = fixture(1810)
        jobs['jobs'][1]['completed_at'] = stamp(1790)
        result = p.performance_report(run, jobs, expected)
        self.assertEqual(result['status'], 'met')
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


def historical_source(path):
    # These tests exercise source-bound historical exceptions, not today's target bytes.
    return runtime.blob(runtime.HISTORICAL_QUALIFICATION_COMMIT, 'src/Parchwell.Verifier/' + path)


def historical_layout(test):
    # Load the runner from the same revision as the pinned qualification; later layouts may differ.
    from functools import wraps
    from types import ModuleType
    @wraps(test)
    def wrapped(*args, **kwargs):
        runner = ModuleType('historical_c5_workflow')
        data = runtime.blob(runtime.HISTORICAL_QUALIFICATION_COMMIT, runtime.RUNNER)
        exec(compile(data, 'historical_c5_workflow.py', 'exec'), runner.__dict__)
        with mock.patch.object(runtime, 'workflow', runner):
            return test(*args, **kwargs)
    return wrapped


class CommittedProvenanceTests(unittest.TestCase):
    def setUp(self):
        import ast
        self.old, self.head = 'a' * 40, 'b' * 40
        self.owner, self.shard = 'Judge/PhaseJudge.cs', 'phase-p1'
        self.source = (ROOT / 'src/Parchwell.Verifier' / self.owner).read_bytes()
        runner = (ROOT / runtime.RUNNER).read_bytes()
        tree = ast.parse(runner)
        lines = runner.splitlines(keepends=True)
        offsets = [0]
        for line in lines:
            offsets.append(offsets[-1] + len(line))
        tables = {'TARGETS': dict(runtime.workflow.TARGETS), 'PRIMARY_CUTS': dict(runtime.workflow.PRIMARY_CUTS)}
        tables['TARGETS'].pop('b10-final-judge')
        tables['PRIMARY_CUTS'].pop('b10-final-judge')
        edits = []
        for node in tree.body:
            if isinstance(node, ast.Assign) and len(node.targets) == 1 and isinstance(node.targets[0], ast.Name) and node.targets[0].id in tables:
                v = node.value
                edits.append((offsets[v.lineno - 1] + v.col_offset, offsets[v.end_lineno - 1] + v.end_col_offset,
                              repr(tables[node.targets[0].id]).encode()))
        old_runner = runner
        for start, end, value in sorted(edits, reverse=True):
            old_runner = old_runner[:start] + value + old_runner[end:]
        config = copy.deepcopy(runtime.workflow.BASE_CONFIG)
        old_config = copy.deepcopy(config)
        old_config['stryker-config']['mutate'].remove('Judge/B10FinalJudge.cs')
        matrix = runtime.workflow.SHARDS
        def matrix_blob(entries):
            return ('        include:\n' + ''.join('          - shard: ' + s + '\n            target: ' + p + '\n'
                    for s, p in entries.items()) + '    env:\n').encode()
        self.blobs = {
            (self.old, runtime.RUNNER): old_runner, (self.head, runtime.RUNNER): runner,
            (self.old, runtime.CONFIG_PATH): json.dumps(old_config).encode(),
            (self.head, runtime.CONFIG_PATH): json.dumps(config).encode(),
            (self.old, runtime.WORKFLOW): matrix_blob({s: p for s, p in matrix.items() if p != 'Judge/B10FinalJudge.cs'}),
            (self.head, runtime.WORKFLOW): matrix_blob(matrix),
            (self.head, 'src/Parchwell.Verifier/' + self.owner): self.source,
        }
        self.context = {'source_commit': self.old, 'shared_inputs': {'fixture': runtime.digest(b'fixture')},
                        'target_sources': {self.owner: runtime.digest(self.source)},
                        'assessments': {'shared_inputs': {}, 'target_sources': {}}}
        self.blob_patch = mock.patch.object(runtime, 'blob', side_effect=lambda ref, path: self.blobs[(ref, path)])
        self.maps_patch = mock.patch.object(runtime, 'input_maps', side_effect=lambda ref, declared: (
            self.context['shared_inputs'], self.context['target_sources']))
        real_git = runtime.git
        self.git_patch = mock.patch.object(runtime, 'git', side_effect=lambda *args:
            b'commit\n' if args[:2] == ('cat-file', '-t') else real_git(*args))
        self.blob_patch.start()
        self.maps_patch.start()
        self.git_patch.start()
        runtime.proof_tree.cache_clear()
        self.addCleanup(self.blob_patch.stop)
        self.addCleanup(self.maps_patch.stop)
        self.addCleanup(self.git_patch.stop)
        self.addCleanup(runtime.proof_tree.cache_clear)

    def equivalent(self):
        runtime.proof_tree.cache_clear()
        return runtime.committed_effective_config(self.context, self.owner, self.shard, self.head)

    def set_old_tables(self, targets, cuts):
        import ast
        key = (self.old, runtime.RUNNER)
        data = self.blobs[key]
        lines = data.splitlines(keepends=True)
        offsets = [0]
        for line in lines:
            offsets.append(offsets[-1] + len(line))
        replacements = {'TARGETS': targets, 'PRIMARY_CUTS': cuts}
        edits = []
        for node in ast.parse(data).body:
            if isinstance(node, ast.Assign) and len(node.targets) == 1 and isinstance(node.targets[0], ast.Name) and node.targets[0].id in replacements:
                v = node.value
                edits.append((offsets[v.lineno - 1] + v.col_offset, offsets[v.end_lineno - 1] + v.end_col_offset,
                              repr(replacements[node.targets[0].id]).encode()))
        for start, end, value in sorted(edits, reverse=True):
            data = data[:start] + value + data[end:]
        self.blobs[key] = data
        config = json.loads(self.blobs[(self.old, runtime.CONFIG_PATH)])
        config['stryker-config']['mutate'] = [v[0] for v in targets.values()]
        self.blobs[(self.old, runtime.CONFIG_PATH)] = json.dumps(config).encode()
        parts = runtime.workflow.build_parts(targets, cuts)
        physical = {}
        for name, (target, _, _) in parts.items():
            members = [member for member, (logical, _) in runtime.workflow.COHORTS.items() if logical == name]
            for member in members or [name]:
                physical[member] = targets[target][0]
        self.blobs[(self.old, runtime.WORKFLOW)] = ('        include:\n' + ''.join(
            f'          - shard: {s}\n            target: {p}\n' for s, p in physical.items()) + '    env:\n').encode()
        self.assertEqual([v[0] for v in targets.values()], config['stryker-config']['mutate'])
        self.assertEqual(physical, runtime.workflow_matrix(self.blobs[(self.old, runtime.WORKFLOW)]))

    def test_removal_and_reordering_reject_with_each_tree_in_parity(self):
        targets = dict(runtime.workflow.TARGETS)
        cuts = dict(runtime.workflow.PRIMARY_CUTS)
        self.set_old_tables(targets, cuts)
        self.assertTrue(self.equivalent())
        targets['old-only'] = ('Synthetic/Old.cs', 1)
        self.context['target_sources']['Synthetic/Old.cs'] = runtime.digest(b'old-only target')
        self.set_old_tables(targets, cuts)
        self.assertFalse(self.equivalent())
        del self.context['target_sources']['Synthetic/Old.cs']
        items = list(runtime.workflow.TARGETS.items())
        items[1], items[2] = items[2], items[1]
        self.set_old_tables(dict(items), cuts)
        self.assertFalse(self.equivalent())

    def test_own_cut_and_physical_owner_reject_with_parity(self):
        targets = dict(runtime.workflow.TARGETS)
        cuts = dict(runtime.workflow.PRIMARY_CUTS)
        self.set_old_tables(targets, cuts)
        self.assertTrue(self.equivalent())
        changed = list(cuts['phase'])
        changed[0] += 1
        cuts['phase'] = tuple(changed)
        self.set_old_tables(targets, cuts)
        self.assertFalse(self.equivalent())
        cuts = dict(runtime.workflow.PRIMARY_CUTS)
        targets['phase'] = (targets['phase'][0], targets['phase'][1] + 1)
        cuts['phase'] = cuts['phase'] + (cuts['phase'][-1] + 1000,)
        self.set_old_tables(targets, cuts)
        self.assertFalse(self.equivalent())

    def test_additive_target_preserves_committed_invocation(self):
        self.assertTrue(self.equivalent())

    def test_config_and_matrix_tampering_rejects(self):
        key = (self.old, runtime.CONFIG_PATH)
        original = self.blobs[key]
        for change in ('setting', 'duplicate', 'removal', 'unknown'):
            with self.subTest(change=change):
                config = json.loads(original)
                settings = config['stryker-config']
                if change == 'setting':
                    settings['coverage-analysis'] = 'all'
                elif change == 'duplicate':
                    settings['mutate'].append(settings['mutate'][0])
                elif change == 'removal':
                    settings['mutate'].pop()
                else:
                    settings['unknown'] = True
                self.blobs[key] = json.dumps(config).encode()
                self.assertFalse(self.equivalent())
        self.blobs[key] = original
        self.blobs[key] = original.replace(b'"stryker-config":', b'"stryker-config": {}, "stryker-config":', 1)
        self.assertFalse(self.equivalent())
        self.blobs[key] = original
        self.blobs[(self.old, runtime.WORKFLOW)] = b'        include:\n    env:\n'
        self.assertFalse(self.equivalent())

    def test_source_ref_hash_and_executable_changes_reject(self):
        source_key = (self.head, 'src/Parchwell.Verifier/' + self.owner)
        self.blobs[source_key] += b' changed'
        self.assertFalse(self.equivalent())
        self.blobs[source_key] = self.source
        self.context['target_sources'][self.owner] = '0' * 64
        self.assertFalse(self.equivalent())
        self.context['target_sources'][self.owner] = runtime.digest(self.source)
        with mock.patch.object(runtime, 'input_maps', return_value=({}, {})):
            self.assertFalse(self.equivalent())
        self.assertFalse(runtime.committed_effective_config(self.context, self.owner, 'unknown', self.head))
        self.assertFalse(runtime.committed_effective_config(self.context, self.owner, self.shard, None))
        runner_key = (self.old, runtime.RUNNER)
        original_runner = self.blobs[runner_key]
        changed_runner = original_runner.replace(b'REBALANCE_SECONDS = 1800', b'REBALANCE_SECONDS = 1801', 1)
        self.assertNotEqual(original_runner, changed_runner)
        self.blobs[runner_key] = changed_runner
        self.assertFalse(self.equivalent())
        self.blobs[runner_key] = original_runner
        self.blobs[runner_key] += b'\nsynthetic_executable = 1\n'
        self.assertFalse(self.equivalent())

    def test_typed_cohort_change_does_not_rebind_its_owner(self):
        runner_key = (self.old, runtime.RUNNER)
        values, _ = runtime.runner_literals(self.blobs[runner_key])
        old_hash = values['COHORT_INVENTORY_SHA256'].encode()
        self.blobs[runner_key] = self.blobs[runner_key].replace(old_hash, b'0' * 64, 1)
        self.assertTrue(self.equivalent())
        self.owner = runtime.CODEC_TARGET
        self.shard = 'b10-final-projection-codec-p1-a'
        self.source = (ROOT / 'src/Parchwell.Verifier' / self.owner).read_bytes()
        self.context['target_sources'][self.owner] = runtime.digest(self.source)
        self.blobs[(self.head, 'src/Parchwell.Verifier/' + self.owner)] = self.source
        self.assertFalse(self.equivalent())

    def test_runner_literal_parser_rejects_rebinding_and_executable_normalization(self):
        runner = self.blobs[(self.head, runtime.RUNNER)]
        _, original_code = runtime.runner_literals(runner)
        for suffix in (b'\nTARGETS = {}\n', b'\nCOHORT_COORDINATES = ()\n'):
            with self.subTest(suffix=suffix), self.assertRaises(ValueError):
                runtime.runner_literals(runner + suffix)
        changed = runner.replace(b'range(116, 122)', b'range(116, 123)', 1)
        self.assertNotEqual(original_code, runtime.runner_literals(changed)[1])

    def test_runner_literal_types_and_boundaries(self):
        import ast
        runner = self.blobs[(self.head, runtime.RUNNER)]
        lines = runner.splitlines(keepends=True)
        offsets = [0]
        for line in lines:
            offsets.append(offsets[-1] + len(line))
        nodes = {node.targets[0].id: node.value for node in ast.parse(runner).body
                 if isinstance(node, ast.Assign) and len(node.targets) == 1 and isinstance(node.targets[0], ast.Name)}
        def changed(name, expression):
            node = nodes[name]
            start = offsets[node.lineno - 1] + node.col_offset
            end = offsets[node.end_lineno - 1] + node.end_col_offset
            return runner[:start] + expression.encode() + runner[end:]
        self.assertTrue(runtime.runner_literals(runner)[0]['TARGETS'])
        cases = {
            'TARGETS': ['{}', '[]', "{'': ('a.cs', 1)}", "{'a': ('', 1)}",
                        "{'a': ('a.cs', 0)}", "{'a': ('a.cs', True)}", "{'a': ['a.cs', 1]}",
                        "{'a': ('a.cs', 1), 'b': ('a.cs', 1)}",
                        "{'a': ('a.cs', 1), 'a': ('b.cs', 1)}"],
            'PRIMARY_CUTS': ['[]', "{'unknown': (1,)}", "{'phase': [1]}",
                             "{'phase': (0,)}", "{'phase': (-1,)}", "{'phase': (True,)}",
                             "{'phase': (1,), 'phase': (2,)}"],
            'COHORT_BOOTSTRAP_SPANS': ['{}', '[]', "{'x': (0, 1)}", "{'1': [0, 1]}",
                                       "{'1': (-1, 1)}", "{'1': (1, 1)}", "{'1': (2, 1)}",
                                       "{'1': (False, 1)}", "{'1': (0, 1), '1': (1, 2)}"],
            'COHORT_COORDINATES': ['()', '[]', '((0, 0, 0),)', '((-1, 0, 0, 0),)',
                                   '((False, 0, 0, 0),)', '([0, 0, 0, 0],)'],
            'COHORT_SOURCE_SHA256': ["''", "'a' * 64", "'A' * 64", '1'],
            'COHORT_INVENTORY_SHA256': ["''", repr('A' * 64), repr('a' * 63)],
        }
        for name, expressions in cases.items():
            for expression in expressions:
                with self.subTest(name=name, expression=expression), self.assertRaises((ValueError, TypeError)):
                    runtime.runner_literals(changed(name, expression))
        for name, expression in [('PRIMARY_CUTS', '{}'), ('COHORT_BOOTSTRAP_SPANS', "{'1': (0, 1)}"),
                                 ('COHORT_COORDINATES', '((0, 0, 0, 0),)'),
                                 ('COHORT_SOURCE_SHA256', repr('a' * 64))]:
            with self.subTest(name=name, boundary=expression):
                self.assertEqual(ast.literal_eval(expression), runtime.runner_literals(changed(name, expression))[0][name])

    def test_fresh_pinned_shard_requires_current_source_and_linked_measurement(self):
        path = runtime.CODEC_TARGET
        shard = next(iter(runtime.CODEC_NEIGHBOR))
        original = runtime.git('show', runtime.HISTORICAL_QUALIFICATION_COMMIT + ':src/Parchwell.Verifier/' + path)
        source = original + b'\n// changed source\n'
        self.blobs[(self.old, 'src/Parchwell.Verifier/' + path)] = source
        evidence = {'source_commit': self.old, 'locator': 'synthetic', 'sha256': 'c' * 64,
                    'kind': 'local_probe', 'note': 'hardware=synthetic calibration=uncertain'}
        record = {'source_sha256': runtime.digest(source), 'evidence': [evidence]}
        item = {'timing_inputs_sha256': 'd' * 64, 'basis': 'observed_workers=2'}
        proof = {'kind': 'measured', 'source_commit': self.old, 'artifact_sha256': evidence['sha256'],
                 'timing_inputs_sha256': item['timing_inputs_sha256'], 'observed_workers': 2,
                 'observed_seconds': 10, 'method': 'synthetic timing'}
        context = {'source_commit': self.old}
        config = {'schema_version': 2, 'timing_evidence': {shard: proof},
                  'timing_inputs': {item['timing_inputs_sha256']: context}}
        with mock.patch.object(runtime, 'committed_effective_config', return_value=True):
            def fresh():
                return runtime.fresh_pinned_timing(config, path, record, shard, item, source, self.head)
            self.assertTrue(fresh())
            proof['artifact_sha256'] = '0' * 64
            self.assertFalse(fresh())
            proof['artifact_sha256'] = evidence['sha256']
            proof['source_commit'] = 'e' * 40
            self.assertFalse(fresh())
            proof['source_commit'] = self.old
            context.pop('source_commit')
            self.assertFalse(fresh())
            context['source_commit'] = self.old
            record['geometry_status'] = 'provisional'
            self.assertFalse(fresh())
            del record['geometry_status']
            self.assertFalse(runtime.fresh_pinned_timing(config, path, record, shard, item,
                                                         original, self.head))

    def test_full_validation_accepts_fresh_pinned_measurements_and_rejects_tampering(self):
        for path in (runtime.RUNNER, runtime.CONFIG_PATH, runtime.WORKFLOW):
            self.blobs[(self.old, path)] = self.blobs[(self.head, path)]
        # Both synthetic commits use the same paired matrix and inputs.
        self.context['shared_inputs'][runtime.COMPANION_WORKFLOW] = runtime.digest(b'synthetic companion')
        layout_patch = mock.patch.object(runtime, 'workflow_layout', return_value=runtime.workflow.SHARDS)
        layout_patch.start()
        self.addCleanup(layout_patch.stop)
        sources = {}
        for path in set(runtime.workflow.SHARDS.values()):
            source = (ROOT / 'src/Parchwell.Verifier' / path).read_bytes() + b'\n// fresh synthetic source\n'
            sources[path] = source
            self.blobs[(self.old, 'src/Parchwell.Verifier/' + path)] = source
            self.blobs[(self.head, 'src/Parchwell.Verifier/' + path)] = source
        targets = {path: runtime.digest(source) for path, source in sources.items()}
        self.context['target_sources'] = targets.copy()
        measured = {'shared_inputs': self.context['shared_inputs'], 'target_sources': targets}
        binding = runtime.digest(runtime.canonical(measured))
        evidence = {'source_commit': self.old, 'locator': 'synthetic timing', 'sha256': 'c' * 64,
                    'kind': 'local_probe', 'note': 'hardware=synthetic calibration=uncertain'}
        records = {path: {'source_sha256': targets[path], 'evidence': [evidence.copy()], 'shards': {}}
                   for path in targets}
        proofs = {}
        for shard, path in runtime.workflow.SHARDS.items():
            item = {'selection_sha256': runtime.digest(runtime.canonical(runtime.workflow.shard_config(shard, sources[path]))),
                    'timing_inputs_sha256': binding, 'main_mutants': 1, 'main_work_seconds': 2,
                    'parallelism': 2, 'setup_seconds': 1, 'isolated_seconds': 1,
                    'estimated_seconds': 3, 'basis': 'observed_workers=2; synthetic'}
            records[path]['shards'][shard] = item
            proofs[shard] = {'kind': 'measured', 'source_commit': self.old, 'artifact_sha256': evidence['sha256'],
                             'timing_inputs_sha256': binding, 'observed_workers': 2,
                             'observed_seconds': 10, 'method': 'synthetic timing'}
        config = {'schema_version': 2, 'budget_seconds': 2700, 'shared_inputs': self.context['shared_inputs'].copy(),
                  'targets': records, 'timing_inputs': {binding: self.context}, 'timing_evidence': proofs}
        shard = 'b10-final-projection-codec-p8'
        path = runtime.CODEC_TARGET
        item = records[path]['shards'][shard]
        proof = proofs[shard]
        def validate():
            runtime.proof_tree.cache_clear()
            runtime.shape(config)
            return runtime.validate(config, config['shared_inputs'], targets, sources, runtime.workflow.SHARDS, self.head)
        self.assertTrue(runtime.committed_effective_config(self.context, path, shard, self.head))
        self.assertTrue(runtime.fresh_pinned_timing(config, path, records[path], shard, item, sources[path], self.head))
        self.assertEqual(set(), validate())
        version_one = dict(config, schema_version=1)
        self.assertFalse(runtime.fresh_pinned_timing(version_one, path, records[path], shard, item, sources[path], self.head))
        proof['artifact_sha256'] = '0' * 64
        self.assertIn('missing_measurement', validate())
        self.assertIn('invalid_estimate', validate())
        proof['artifact_sha256'] = evidence['sha256']
        proof['source_commit'] = self.head
        self.assertIn('missing_measurement', validate())
        self.assertIn('invalid_estimate', validate())
        proof['source_commit'] = self.old
        item['selection_sha256'] = '0' * 64
        self.assertIn('layout_mismatch', validate())
        item['selection_sha256'] = runtime.digest(runtime.canonical(runtime.workflow.shard_config(shard, sources[path])))
        self.blobs[(self.head, 'src/Parchwell.Verifier/' + path)] += b'changed'
        self.assertIn('invalid_estimate', validate())
        self.blobs[(self.head, 'src/Parchwell.Verifier/' + path)] = sources[path]
        self.context.pop('source_commit')
        self.assertIn('invalid_estimate', validate())
        self.context['source_commit'] = self.old
        tampered = dict(targets, **{path: '0' * 64})
        with mock.patch.object(runtime, 'input_maps', return_value=(self.context['shared_inputs'], tampered)):
            self.assertIn('invalid_estimate', validate())
        self.assertEqual(set(), validate())
        old_config = json.loads(self.blobs[(self.old, runtime.CONFIG_PATH)])
        self.blobs[(self.old, runtime.CONFIG_PATH)] = json.dumps(old_config, indent=4).encode()
        self.context['shared_inputs'][runtime.CONFIG_PATH] = runtime.digest(self.blobs[(self.old, runtime.CONFIG_PATH)])
        current_hash = runtime.digest(self.blobs[(self.head, runtime.CONFIG_PATH)])
        config['shared_inputs'][runtime.CONFIG_PATH] = current_hash
        self.context['assessments']['shared_inputs'][runtime.CONFIG_PATH] = {
            'current_sha256': current_hash, 'basis': ['Synthetic accepted risk.']}
        new_binding = runtime.digest(runtime.canonical({'shared_inputs': self.context['shared_inputs'],
                                                        'target_sources': targets}))
        config['timing_inputs'] = {new_binding: self.context}
        for record in records.values():
            for shard_item in record['shards'].values():
                shard_item['timing_inputs_sha256'] = new_binding
        for shard_proof in proofs.values():
            shard_proof['timing_inputs_sha256'] = new_binding
        self.assertEqual(set(), validate())
        proof['source_commit'] = self.head
        self.assertIn('missing_measurement', validate())
        self.assertIn('invalid_estimate', validate())


class CommittedInputClassificationTests(unittest.TestCase):
    def test_target_classification_uses_each_tree_declaration(self):
        ref = runtime.git('rev-parse', 'HEAD').decode().strip()
        declared = dict(runtime.workflow.TARGETS)
        path = declared.pop('b10-final-judge')[0]
        source_path = 'src/Parchwell.Verifier/' + path
        old_shared, old_targets = runtime.input_maps(ref, declared)
        current_shared, current_targets = runtime.input_maps(ref, runtime.workflow.TARGETS)
        self.assertEqual(runtime.digest(runtime.blob(ref, source_path)), old_shared[source_path])
        self.assertNotIn(path, old_targets)
        self.assertNotIn(source_path, current_shared)
        self.assertEqual(old_shared[source_path], current_targets[path])


class ReviewCorrectionBridgeTests(unittest.TestCase):
    OWNER = 'Judge/PhaseJudge.cs'
    SHARD = 'phase-p1'

    def setUp(self):
        qualification = json.loads(runtime.blob(runtime.REVIEW_CORRECTION_PRIOR, runtime.HISTORICAL_QUALIFICATION_PATH))
        item = qualification['targets'][self.OWNER]['shards'][self.SHARD]
        self.context = copy.deepcopy(qualification['timing_inputs'][item['timing_inputs_sha256']])
        shared, targets = runtime.proof_tree(runtime.REVIEW_CORRECTION)[2]
        for key, actual in (('shared_inputs', shared), ('target_sources', targets)):
            measured = self.context[key]
            self.context['assessments'][key] = {
                path: {'current_sha256': actual.get(path), 'basis': ['Approved correction timing risk']}
                for path in measured.keys() | actual.keys() if measured.get(path) != actual.get(path)
            }

    def proof(self, context=None, owner=None, shard=None, head=None):
        return runtime.committed_effective_config(
            context if context is not None else self.context, owner or self.OWNER,
            shard or self.SHARD, head or runtime.REVIEW_CORRECTION)

    def test_retains_actual_predecessor_proof_for_unchanged_owner(self):
        self.assertTrue(runtime._committed_effective_config(
            self.context, self.OWNER, self.SHARD, runtime.REVIEW_CORRECTION_PRIOR))
        self.assertFalse(runtime._committed_effective_config(
            self.context, self.OWNER, self.SHARD, runtime.REVIEW_CORRECTION))
        self.assertTrue(self.proof())

    def test_rebalanced_tree_retains_predecessor_for_unrelated_owner(self):
        self.assertTrue(self.proof(head=runtime.REBALANCED_SOURCE))
        self.assertFalse(self.proof(shard='unknown', head=runtime.REBALANCED_SOURCE))
        self.assertFalse(self.proof(owner=runtime.CODEC_TARGET,
                                    shard='b10-final-projection-codec-p1-a',
                                    head=runtime.REBALANCED_SOURCE))
        context = copy.deepcopy(self.context)
        context['assessments']['target_sources'][runtime.CODEC_TARGET]['current_sha256'] = '0' * 64
        self.assertFalse(self.proof(context, head=runtime.REBALANCED_SOURCE))

    def test_ci_repair_links_only_retained_selections_to_their_prior_context(self):
        head = runtime.CI_REPAIR_SOURCE
        previous = runtime.prior_ci_repair_qualification()
        binding = previous['targets'][self.OWNER]['shards'][self.SHARD]['timing_inputs_sha256']
        context = copy.deepcopy(previous['timing_inputs'][binding])
        shared, targets = runtime.proof_tree(head)[2]
        for key, actual in (('shared_inputs', shared), ('target_sources', targets)):
            measured = context[key]
            context['assessments'][key] = {
                path: {'current_sha256': actual.get(path), 'basis': ['Approved CI repair timing risk']}
                for path in measured.keys() | actual.keys() if measured.get(path) != actual.get(path)
            }
        for candidate in (head, runtime.CI_REPAIR_REBALANCE):
            self.assertTrue(self.proof(context, head=candidate))
            self.assertEqual(set(), runtime.provenance_errors(context, shared, targets, self.OWNER, self.SHARD, candidate))
        self.assertFalse(runtime.retained_ci_repair_selection(head, self.OWNER, 'unknown'))
        for owner in runtime.CI_REPAIR_OWNERS:
            self.assertFalse(runtime.retained_ci_repair_selection(head, runtime.workflow.TARGETS[owner][0], owner + '-p1'))
        context['source_commit'] = runtime.CI_REPAIR_PRIOR
        self.assertFalse(self.proof(context, head=head))

    def test_classifier_capacity_retains_only_other_owners_on_exact_trees(self):
        judge = 'b10-final-judge-crossing'
        for head in (runtime.CI_REPAIR_CAPACITY, runtime.CI_REPAIR_CAPACITY_COMBINED, runtime.CI_REPAIR_OWNERSHIP_SOURCE):
            with self.subTest(head=head):
                self.assertTrue(runtime.retained_classifier_capacity_selection(head, self.OWNER, self.SHARD))
                self.assertTrue(runtime.retained_classifier_capacity_selection(head, runtime.workflow.SHARDS[judge], judge))
                for suffix in ('p1', 'p7', 'p18', 'crossing'):
                    shard = 'b10-final-classification-' + suffix
                    self.assertFalse(runtime.retained_classifier_capacity_selection(head, runtime.workflow.SHARDS[shard], shard))
                self.assertFalse(runtime.retained_classifier_capacity_selection(head, self.OWNER, 'unknown'))
        self.assertFalse(runtime.retained_classifier_capacity_selection(runtime.CI_REPAIR_REBALANCE, self.OWNER, self.SHARD))

    def test_classifier_capacity_accepts_only_the_pinned_validator_for_retained_timing(self):
        previous = runtime.prior_ci_repair_qualification()
        binding = previous['targets'][self.OWNER]['shards'][self.SHARD]['timing_inputs_sha256']
        context = copy.deepcopy(previous['timing_inputs'][binding])
        head = runtime.CI_REPAIR_OWNERSHIP_SOURCE
        shared, targets = runtime.proof_tree(head)[2]
        for key, current in (('shared_inputs', shared), ('target_sources', targets)):
            measured = context[key]
            context['assessments'][key] = {
                path: {'current_sha256': current.get(path), 'basis': ['Approved capacity timing scope']}
                for path in measured.keys() | current.keys() if measured.get(path) != current.get(path)
            }
        self.assertEqual(set(), runtime.provenance_errors(context, shared, targets, self.OWNER, self.SHARD, head))

        original = runtime.proof_tree
        current_tree = original(head)
        changed_head = 'c' * 40
        changed_shared = {**shared, runtime.CAMPAIGN_VALIDATOR: '0' * 64}
        changed_tree = (*current_tree[:2], (changed_shared, targets), *current_tree[3:])
        context['assessments']['shared_inputs'][runtime.CAMPAIGN_VALIDATOR]['current_sha256'] = '0' * 64
        with mock.patch.object(runtime, 'proof_tree', side_effect=lambda ref: changed_tree if ref == changed_head else original(ref)):
            self.assertIn('invalid_renewal', runtime.provenance_errors(
                context, changed_shared, targets, self.OWNER, self.SHARD, changed_head))

    def test_classifier_capacity_accepts_a_later_assessed_shared_input(self):
        original = runtime.proof_tree
        source = runtime.CI_REPAIR_OWNERSHIP_SOURCE
        tree = original(source)
        shared, targets = tree[2]
        path = runtime.CI_REPAIR_TEST
        head = 'a' * 40
        qualification = runtime.prior_ci_repair_qualification()
        binding = qualification['targets'][self.OWNER]['shards'][self.SHARD]['timing_inputs_sha256']
        context = copy.deepcopy(qualification['timing_inputs'][binding])
        previous_hash = context['shared_inputs'].get(path)
        self.assertNotIn(path, runtime.NON_RENEWABLE)
        self.assertIsNotNone(previous_hash)

        original_blob = runtime.blob
        def blob(ref, requested):
            return original_blob(source if ref == head else ref, requested)

        for current_hash in ('1' * 64, None, previous_hash):
            with self.subTest(current_hash=current_hash):
                edited = dict(shared)
                if current_hash is None:
                    edited.pop(path)
                else:
                    edited[path] = current_hash
                changed = (*tree[:2], (edited, targets), *tree[3:])
                for key, current in (('shared_inputs', edited), ('target_sources', targets)):
                    measured = context[key]
                    context['assessments'][key] = {
                        name: {'current_sha256': current.get(name), 'basis': ['Synthetic assessment fixture']}
                        for name in measured.keys() | current.keys() if measured.get(name) != current.get(name)
                    }
                runtime.classifier_capacity_trees.cache_clear()
                with mock.patch.object(runtime, 'proof_tree', side_effect=lambda ref:
                        changed if ref == head else original(ref)), mock.patch.object(runtime, 'blob', side_effect=blob):
                    self.assertEqual(set(), runtime.provenance_errors(
                        context, edited, targets, self.OWNER, self.SHARD, head))
                    if current_hash != previous_hash:
                        assessment = context['assessments']['shared_inputs'].pop(path)
                        self.assertIn('unaccounted_inputs', runtime.provenance_errors(
                            context, edited, targets, self.OWNER, self.SHARD, head))
                        context['assessments']['shared_inputs'][path] = {**assessment, 'current_sha256': '9' * 64}
                        self.assertIn('unaccounted_inputs', runtime.provenance_errors(
                            context, edited, targets, self.OWNER, self.SHARD, head))
        runtime.classifier_capacity_trees.cache_clear()

    def test_rejects_changed_owner_and_incomplete_or_wrong_assessments(self):
        self.assertFalse(self.proof(owner=runtime.REVIEW_CORRECTION_TARGET,
                                    shard='b10-final-projection-codec-p1-a'))
        for mutation in ('missing', 'hash', 'source', 'owner', 'selector'):
            with self.subTest(mutation=mutation):
                context = copy.deepcopy(self.context)
                if mutation == 'missing':
                    del context['assessments']['shared_inputs'][next(iter(runtime.REVIEW_CORRECTION_SHARED))]
                elif mutation == 'hash':
                    context['assessments']['target_sources'][runtime.REVIEW_CORRECTION_TARGET]['current_sha256'] = '0' * 64
                elif mutation == 'source':
                    context['source_commit'] = '0' * 40
                elif mutation == 'owner':
                    context['target_sources'][self.OWNER] = '0' * 64
                self.assertFalse(self.proof(context, shard='unknown' if mutation == 'selector' else None))

    def test_substantive_head_drift_and_missing_pins_refuse_bridge(self):
        original = runtime.proof_tree
        correction = original(runtime.REVIEW_CORRECTION)
        synthetic_head = 'f' * 40
        for field in range(len(correction)):
            with self.subTest(field=field):
                changed = list(copy.deepcopy(correction))
                if field == 0:
                    changed[field]['TARGETS'][next(iter(changed[field]['TARGETS']))] = ('changed.cs', 1)
                elif field == 1:
                    changed[field] += b' changed'
                elif field == 2:
                    changed[field][0]['drift'] = '0' * 64
                elif field == 3:
                    changed[field]['drift'] = True
                else:
                    changed[field]['drift'] = 'changed.cs'
                with mock.patch.object(runtime, 'proof_tree', side_effect=lambda ref:
                        tuple(changed) if ref == synthetic_head else original(ref)):
                    self.assertFalse(self.proof(head=synthetic_head))
        with mock.patch.object(runtime, 'proof_tree', side_effect=lambda ref:
                (_ for _ in ()).throw(ValueError('missing commit'))
                if ref == runtime.REVIEW_CORRECTION_PRIOR else original(ref)):
            self.assertFalse(self.proof())


class HistoricalRunnerBridgeTests(unittest.TestCase):
    OLD_CONTEXTS = (
        ('6c291d8832d0c722352ff979545a136e5ea2ddeb',
         'd700bd7823eba6654556c98af0e44bf1541adb877ee4d19bda63fdcb78959dd5'),
        ('6c6ed9208b005df0ac51033f4cf2be687b0983b9',
         'e15cd682a2731538bfde90aa333a9742bccb40e3db957f855d71a25a9e520463'),
    )
    MODERN = '1e38605cceaf14c92ec9157e8eead43eedbf3a8d'
    OWNER = 'Judge/PhaseJudge.cs'
    SHARD = 'phase-p1'

    def setUp(self):
        runtime.proof_tree.cache_clear()
        self.addCleanup(runtime.proof_tree.cache_clear)

    def context(self, commit):
        shared, targets = runtime.proof_tree(commit)[2]
        return {'source_commit': commit, 'shared_inputs': shared, 'target_sources': targets,
                'assessments': {'shared_inputs': {}, 'target_sources': {}}}

    def proof(self, context, head=MODERN):
        runtime.proof_tree.cache_clear()
        return runtime.committed_effective_config(context, self.OWNER, self.SHARD, head)

    def test_actual_legacy_contexts_bridge_only_the_approved_runner_pair(self):
        actual_blob = runtime.blob
        for commit, expected_binding in self.OLD_CONTEXTS:
            with self.subTest(commit=commit):
                context = self.context(commit)
                maps = {key: context[key] for key in ('shared_inputs', 'target_sources')}
                self.assertEqual(expected_binding, runtime.digest(runtime.canonical(maps)))
                self.assertEqual(runtime.RUNNER_TIMING_PAIR[0], context['shared_inputs'][runtime.RUNNER])
                modern_shared, _ = runtime.proof_tree(self.MODERN)[2]
                self.assertEqual(runtime.RUNNER_TIMING_PAIR[1], modern_shared[runtime.RUNNER])
                self.assertTrue(self.proof(context))
                runtime.proof_tree.cache_clear()
                self.assertTrue(runtime.committed_effective_config(context, runtime.CODEC_TARGET,
                                'b10-final-projection-codec-p1-a', self.MODERN))
                altered = copy.deepcopy(context)
                altered['target_sources'][self.OWNER] = '0' * 64
                self.assertFalse(self.proof(altered))
                altered = copy.deepcopy(context)
                altered['source_commit'] = self.MODERN
                self.assertFalse(self.proof(altered))
                with mock.patch.object(runtime, 'blob', side_effect=lambda ref, path:
                        actual_blob(ref, path) + b'\n# unknown runner\n'
                        if ref == commit and path == runtime.RUNNER else actual_blob(ref, path)):
                    self.assertFalse(self.proof(context))
                with mock.patch.object(runtime, 'blob', side_effect=lambda ref, path:
                        actual_blob(ref, path) + b'\n# unknown bridge\n'
                        if ref == self.MODERN and path == runtime.RUNNER else actual_blob(ref, path)):
                    self.assertFalse(self.proof(context))
                self.assertTrue(self.proof(context))
                self.assertFalse(self.proof(self.context(self.MODERN), commit))

    def test_bridge_selector_comparison_includes_isolated_invocation(self):
        old = runtime.blob(self.OLD_CONTEXTS[0][0], runtime.RUNNER)
        modern = runtime.blob(self.MODERN, runtime.RUNNER)
        self.assertEqual(runtime.selector_ast(old), runtime.selector_ast(modern))
        changed = old.replace(b'def isolated_config(target: str, item: dict)',
                              b'def isolated_config(target: str, item: tuple)', 1)
        self.assertNotEqual(old, changed)
        self.assertNotEqual(runtime.selector_ast(changed), runtime.selector_ast(modern))

    def test_historical_link_uses_pinned_record_instead_of_an_evidence_commit(self):
        baseline = runtime.historical_qualification()
        record = baseline['targets'][self.OWNER]
        item = record['shards'][self.SHARD]
        context = self.context(runtime.HISTORICAL_QUALIFICATION_COMMIT)
        timing = {'kind': 'historical'}
        self.assertNotIn(context['source_commit'], {entry['source_commit'] for entry in record['evidence']})
        self.assertTrue(self.proof(context))
        self.assertTrue(runtime.timing_context_linked(context, self.SHARD, self.OWNER, record, item, timing, baseline))
        changed = copy.deepcopy(record)
        changed['evidence'][0]['sha256'] = '0' * 64
        self.assertFalse(runtime.timing_context_linked(context, self.SHARD, self.OWNER, changed, item, timing, baseline))
        changed = copy.deepcopy(item)
        changed['selection_sha256'] = '0' * 64
        self.assertFalse(runtime.timing_context_linked(context, self.SHARD, self.OWNER, record, changed, timing, baseline))
        changed = copy.deepcopy(record)
        changed['source_sha256'] = '0' * 64
        self.assertFalse(runtime.timing_context_linked(context, self.SHARD, self.OWNER, changed, item, timing, baseline))
        context['source_commit'] = self.OLD_CONTEXTS[0][0]
        self.assertFalse(runtime.timing_context_linked(context, self.SHARD, self.OWNER, record, item, timing, baseline))
        context.pop('source_commit')
        self.assertFalse(runtime.timing_context_linked(context, self.SHARD, self.OWNER, record, item, timing, baseline))

    def test_measured_link_requires_source_commit_and_matching_artifact(self):
        commit = self.OLD_CONTEXTS[0][0]
        context = self.context(commit)
        source = runtime.blob(commit, 'src/Parchwell.Verifier/' + self.OWNER)
        evidence = {'source_commit': commit, 'locator': 'synthetic', 'sha256': 'c' * 64,
                    'kind': 'local_probe', 'note': 'hardware=synthetic calibration=uncertain'}
        record = {'source_sha256': runtime.digest(source), 'evidence': [evidence]}
        item = {'timing_inputs_sha256': 'd' * 64, 'basis': 'observed_workers=2'}
        timing = {'kind': 'measured', 'source_commit': commit, 'artifact_sha256': evidence['sha256'],
                  'timing_inputs_sha256': item['timing_inputs_sha256'], 'observed_workers': 2,
                  'observed_seconds': 10, 'method': 'synthetic timing'}
        self.assertTrue(self.proof(context))
        self.assertTrue(runtime.timing_context_linked(context, self.SHARD, self.OWNER, record, item, timing, {}))
        for field, invalid in [('artifact_sha256', '0' * 64), ('source_commit', self.MODERN),
                               ('timing_inputs_sha256', '0' * 64), ('observed_workers', 1)]:
            with self.subTest(field=field):
                changed = dict(timing, **{field: invalid})
                self.assertFalse(runtime.timing_context_linked(context, self.SHARD, self.OWNER, record, item, changed, {}))

    @historical_layout
    def test_historical_baseline_validates_through_real_legacy_bridge(self):
        baseline = runtime.historical_qualification()
        context = self.context(runtime.HISTORICAL_QUALIFICATION_COMMIT)
        binding = runtime.digest(runtime.canonical({k: context[k] for k in ('shared_inputs', 'target_sources')}))
        self.assertEqual(runtime.PROVISIONAL_TIMING_INPUTS_HASH, binding)
        record = copy.deepcopy(baseline['targets'][self.OWNER])
        record['shards'] = {self.SHARD: record['shards'][self.SHARD]}
        item = record['shards'][self.SHARD]
        shared = context['shared_inputs'].copy()
        shared[runtime.RUNNER] = runtime.RUNNER_TIMING_PAIR[1]
        context['assessments']['shared_inputs'][runtime.RUNNER] = {
            'current_sha256': shared[runtime.RUNNER], 'basis': ['Synthetic risk acceptance.']}
        target_hash = context['target_sources'][self.OWNER]
        targets = {self.OWNER: target_hash}
        context['assessments']['target_sources'] = {
            path: {'current_sha256': None, 'basis': ['Synthetic removed target.']}
            for path in context['target_sources'] if path != self.OWNER}
        source = runtime.blob(self.MODERN, 'src/Parchwell.Verifier/' + self.OWNER)
        config = {'schema_version': 2, 'budget_seconds': 1800, 'shared_inputs': shared,
                  'targets': {self.OWNER: record}, 'timing_inputs': {binding: context},
                  'timing_evidence': {self.SHARD: {'kind': 'historical'}}}
        matrix = {self.SHARD: self.OWNER}
        def validate():
            runtime.proof_tree.cache_clear()
            runtime.shape(config)
            return runtime.validate(config, shared, targets, {self.OWNER: source}, matrix, self.MODERN)
        with mock.patch.object(runtime.workflow, 'SHARDS', matrix):
            self.assertTrue(self.proof(context))
            self.assertEqual(set(), validate())
            record['evidence'][0]['sha256'] = '0' * 64
            self.assertIn('missing_measurement', validate())
            record['evidence'][0]['sha256'] = baseline['targets'][self.OWNER]['evidence'][0]['sha256']
            original_selection = item['selection_sha256']
            item['selection_sha256'] = '0' * 64
            self.assertIn('missing_measurement', validate())
            self.assertIn('layout_mismatch', validate())
            item['selection_sha256'] = original_selection
            context['source_commit'] = self.OLD_CONTEXTS[0][0]
            self.assertIn('missing_measurement', validate())
            context['source_commit'] = runtime.HISTORICAL_QUALIFICATION_COMMIT
            self.assertEqual(set(), validate())


class QualificationTests(unittest.TestCase):
    def setUp(self):
        self.original_shards = runtime.workflow.SHARDS
        self.original_shard_config = runtime.workflow.shard_config
        self.original_evidence_matches_target = runtime.evidence_matches_target
        self.original_has_shard_timing = runtime.has_shard_timing
        timing_patch = mock.patch.object(runtime, 'has_shard_timing', return_value=True)
        timing_patch.start()
        self.addCleanup(timing_patch.stop)
        evidence_patch = mock.patch.object(runtime, 'evidence_matches_target', return_value=True)
        evidence_patch.start()
        self.addCleanup(evidence_patch.stop)
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

    def migrate_v2(self):
        measured_targets = self.targets.copy()
        measured_shared = self.shared.copy()
        binding = runtime.digest(runtime.canonical({'shared_inputs': measured_shared, 'target_sources': measured_targets}))
        self.config['schema_version'] = 2
        self.config['timing_evidence'] = {shard: {'kind': 'measured', 'source_commit': 'a' * 40,
            'artifact_sha256': 'a' * 64, 'timing_inputs_sha256': binding, 'observed_workers': 2,
            'observed_seconds': 10, 'method': 'synthetic execution timing'} for shard in self.shards}
        self.config['timing_inputs'] = {binding: {'shared_inputs': measured_shared,
            'target_sources': measured_targets, 'assessments': {'shared_inputs': {}, 'target_sources': {}}}}
        return binding

    def test_timing_context_commit_is_optional_and_strictly_formed(self):
        binding = self.migrate_v2()
        context = self.config['timing_inputs'][binding]
        runtime.shape(self.config)
        context['source_commit'] = 'a' * 40
        runtime.shape(self.config)
        for invalid in ('', 'a' * 39, 'A' * 40, None, 1):
            with self.subTest(commit=invalid):
                context['source_commit'] = invalid
                with self.assertRaises(ValueError):
                    runtime.shape(self.config)

    def assert_committed_maps_reconstructed(self, approved_runner_pair):
        if approved_runner_pair:
            self.shared[runtime.RUNNER] = runtime.RUNNER_TIMING_PAIR[0]
            self.config['shared_inputs'] = self.shared.copy()
        binding = self.migrate_v2()
        context = self.config['timing_inputs'][binding]
        context['source_commit'] = 'a' * 40
        for record in self.config['targets'].values():
            for item in record['shards'].values():
                item['timing_inputs_sha256'] = binding
        if approved_runner_pair:
            self.shared[runtime.RUNNER] = runtime.RUNNER_TIMING_PAIR[1]
            self.config['shared_inputs'] = self.shared.copy()
            context['assessments']['shared_inputs'][runtime.RUNNER] = {
                'current_sha256': self.shared[runtime.RUNNER], 'basis': ['Synthetic approved pair.']}
        def tree(shared, targets):
            return (None, None, (shared, targets), None, None)
        shared = context['shared_inputs'].copy()
        targets = context['target_sources'].copy()
        with mock.patch.object(runtime, 'proof_tree', return_value=tree(shared, targets)) as committed:
            self.assertEqual(set(), self.errors())
            committed.assert_called_with(context['source_commit'])
            for mapping in ('shared_inputs', 'target_sources'):
                with self.subTest(approved_runner_pair=approved_runner_pair, mapping=mapping):
                    reconstructed_shared = shared.copy()
                    reconstructed_targets = targets.copy()
                    if mapping == 'shared_inputs':
                        reconstructed_shared['global.json'] = '0' * 64
                    else:
                        reconstructed_targets['a.cs'] = '0' * 64
                    committed.return_value = tree(reconstructed_shared, reconstructed_targets)
                    self.assertEqual({'stale_inputs'}, self.errors())
                    del context['source_commit']
                    self.assertEqual(set(), self.errors())
                    context['source_commit'] = 'a' * 40
                    committed.return_value = tree(shared, targets)
                    self.assertEqual(set(), self.errors())

    def test_committed_maps_checked_without_runner_exception(self):
        self.assert_committed_maps_reconstructed(False)

    def test_committed_maps_checked_with_approved_runner_pair(self):
        self.assert_committed_maps_reconstructed(True)

    def test_config_exception_requires_linked_timing_and_does_not_renew_tooling(self):
        self.shared[runtime.CONFIG_PATH] = runtime.digest(b'old config')
        self.config['shared_inputs'] = self.shared.copy()
        binding = self.migrate_v2()
        context = self.config['timing_inputs'][binding]
        context['source_commit'] = 'a' * 40
        for record in self.config['targets'].values():
            for item in record['shards'].values():
                item['timing_inputs_sha256'] = binding
        self.shared[runtime.CONFIG_PATH] = runtime.digest(b'additive config')
        self.config['shared_inputs'] = self.shared.copy()
        changes = context['assessments']['shared_inputs']
        changes[runtime.CONFIG_PATH] = {'current_sha256': self.shared[runtime.CONFIG_PATH], 'basis': ['Synthetic risk.']}
        def validate():
            runtime.shape(self.config)
            return runtime.validate(self.config, self.shared, self.targets, self.source, self.shards, head='b' * 40)
        with mock.patch.object(runtime, 'committed_effective_config',
                               side_effect=lambda context, owner, shard, head: 'source_commit' in context), \
             mock.patch.object(runtime, 'proof_tree', return_value=(None, None,
                               (context['shared_inputs'], context['target_sources']), None, None)):
            self.assertEqual(set(), validate())
            self.config['timing_evidence']['a-p1']['source_commit'] = 'c' * 40
            self.assertIn('missing_measurement', validate())
            self.config['timing_evidence']['a-p1']['source_commit'] = 'a' * 40
            del context['source_commit']
            self.assertIn('invalid_renewal', validate())
            context['source_commit'] = 'a' * 40
            tool = '.config/dotnet-tools.json'
            self.shared[tool] = runtime.digest(b'new tool')
            self.config['shared_inputs'] = self.shared.copy()
            changes[tool] = {'current_sha256': self.shared[tool], 'basis': ['Synthetic risk.']}
            self.assertIn('invalid_renewal', validate())

    @historical_layout
    def test_v2_migration_preserves_committed_historical_qualification(self):
        self.original_shards = runtime.workflow.SHARDS
        self.original_shard_config = runtime.workflow.shard_config
        config = runtime.historical_qualification()
        self.assertEqual(1, config['schema_version'])
        measured_shared = config['shared_inputs'].copy()
        for path, renewal in config.get('renewals', {}).items():
            if renewal['measured_sha256'] is None:
                measured_shared.pop(path, None)
            else:
                measured_shared[path] = renewal['measured_sha256']
        targets = {path: record['source_sha256'] for path, record in config['targets'].items()}
        binding = runtime.digest(runtime.canonical({'shared_inputs': measured_shared, 'target_sources': targets}))
        self.assertEqual(runtime.PROVISIONAL_TIMING_INPUTS_HASH, binding)
        assessments = {path: {'current_sha256': item['current_sha256'], 'basis': item['basis']}
                       for path, item in config.pop('renewals', {}).items()}
        config['schema_version'] = 2
        config['timing_evidence'] = {shard: {'kind': 'historical'} for shard in self.original_shards}
        config['timing_inputs'] = {binding: {'shared_inputs': measured_shared, 'target_sources': targets,
            'assessments': {'shared_inputs': assessments, 'target_sources': {}}}}
        source = {path: historical_source(path) for path in targets}
        runtime.shape(config)
        with mock.patch.object(runtime.workflow, 'SHARDS', self.original_shards), \
             mock.patch.object(runtime.workflow, 'shard_config', self.original_shard_config), \
             mock.patch.object(runtime, 'evidence_matches_target', self.original_evidence_matches_target), \
             mock.patch.object(runtime, 'has_shard_timing', self.original_has_shard_timing):
            self.assertEqual(set(), runtime.validate(config, config['shared_inputs'], targets, source, self.original_shards))
            first_shard = next(iter(self.original_shards))
            owner = self.original_shards[first_shard]
            item = config['targets'][owner]['shards'][first_shard]
            basis = item['basis']
            item['basis'] = basis + ' altered'
            self.assertIn('missing_measurement', runtime.validate(config, config['shared_inputs'], targets, source, self.original_shards))
            item['basis'] = basis
            config['targets'][owner]['evidence'][0]['note'] += ' altered'
            self.assertIn('missing_measurement', runtime.validate(config, config['shared_inputs'], targets, source, self.original_shards))

    def test_v2_accounts_for_other_target_without_rebinding_historical_measurement(self):
        old_binding = self.migrate_v2()
        self.source['a.cs'] = b'changed'
        self.targets['a.cs'] = runtime.digest(self.source['a.cs'])
        changed = self.config['targets']['a.cs']
        changed['source_sha256'] = self.targets['a.cs']
        changed['shards']['a-p1']['selection_sha256'] = runtime.digest(runtime.canonical(runtime.workflow.shard_config('a-p1', self.source['a.cs'])))
        current_binding = runtime.digest(runtime.canonical({'shared_inputs': self.shared, 'target_sources': self.targets}))
        self.config['timing_inputs'][current_binding] = {'shared_inputs': self.shared.copy(),
            'target_sources': self.targets.copy(), 'assessments': {'shared_inputs': {}, 'target_sources': {}}}
        changed['shards']['a-p1']['timing_inputs_sha256'] = current_binding
        self.assertIn('unaccounted_inputs', self.errors())
        assessment = {'current_sha256': self.targets['a.cs'], 'basis': ['Synthetic accepted timing risk for the other target.']}
        self.config['timing_inputs'][old_binding]['assessments']['target_sources']['a.cs'] = assessment
        self.assertEqual(set(), self.errors())
        assessment['current_sha256'] = '0' * 64
        self.assertIn('unaccounted_inputs', self.errors())
        assessment['current_sha256'] = self.targets['a.cs']
        changed['shards']['a-p1']['timing_inputs_sha256'] = old_binding
        self.assertIn('invalid_renewal', self.errors())
        self.assertIn('stale_inputs', self.errors())

    def test_v2_shared_differences_require_exact_assessments_and_cannot_renew_tooling(self):
        binding = self.migrate_v2()
        self.shared['global.json'] = runtime.digest(b'new sdk')
        self.config['shared_inputs'] = self.shared.copy()
        self.assertIn('unaccounted_inputs', self.errors())
        changes = self.config['timing_inputs'][binding]['assessments']['shared_inputs']
        changes['global.json'] = {'current_sha256': self.shared['global.json'], 'basis': ['Synthetic accepted risk.']}
        self.assertEqual(set(), self.errors())
        changes['missing'] = {'current_sha256': None, 'basis': ['No change is not an assessment.']}
        self.assertIn('unaccounted_inputs', self.errors())
        del changes['missing']
        self.shared[runtime.RUNNER] = runtime.digest(b'new runner')
        self.config['shared_inputs'] = self.shared.copy()
        changes[runtime.RUNNER] = {'current_sha256': self.shared[runtime.RUNNER], 'basis': ['Not renewable.']}
        self.assertIn('invalid_renewal', self.errors())

    def test_v2_projection_exact_pair_requires_complete_current_assessment(self):
        old, new = APPROVED_PROJECTION_PAIR
        self.shared[PROJECTION_PATH] = old
        self.config['shared_inputs'] = self.shared.copy()
        binding = self.migrate_v2()
        for record in self.config['targets'].values():
            for item in record['shards'].values():
                item['timing_inputs_sha256'] = binding
        self.shared[PROJECTION_PATH] = new
        self.config['shared_inputs'] = self.shared.copy()
        changes = self.config['timing_inputs'][binding]['assessments']['shared_inputs']
        self.assertIn('unaccounted_inputs', self.errors())
        changes[PROJECTION_PATH] = {'current_sha256': new, 'basis': ['Synthetic accepted timing risk.']}
        self.assertEqual(set(), self.errors())
        changes[PROJECTION_PATH]['current_sha256'] = old
        self.assertIn('unaccounted_inputs', self.errors())
        changes[PROJECTION_PATH]['current_sha256'] = new
        changes['extra'] = {'current_sha256': None, 'basis': ['Synthetic extra assessment.']}
        self.assertIn('unaccounted_inputs', self.errors())
        del changes['extra']
        changes[PROJECTION_PATH]['basis'] = []
        with self.assertRaises(ValueError):
            self.errors()

    def test_v2_projection_exception_rejects_other_pairs_and_nonrenewable_paths(self):
        old, new = APPROVED_PROJECTION_PAIR
        other = runtime.digest(b'other')
        cases = [(PROJECTION_PATH, other, new), (PROJECTION_PATH, old, other),
                 (PROJECTION_PATH, new, old), (PROJECTION_PATH, None, new),
                 (PROJECTION_PATH, old, None)]
        cases += [(path, old, new) for path in (
            'tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py',
            '.github/workflows/verifier-c5-mutation.yml',
            'tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json',
            '.config/dotnet-tools.json')]
        for path, measured_hash, current_hash in cases:
            with self.subTest(path=path, measured=measured_hash, current=current_hash):
                measured = {} if measured_hash is None else {path: measured_hash}
                current = {} if current_hash is None else {path: current_hash}
                context = {'shared_inputs': measured, 'target_sources': self.targets.copy(),
                           'assessments': {'shared_inputs': {path: {'current_sha256': current_hash,
                                                                    'basis': ['Synthetic risk.']}},
                                           'target_sources': {}}}
                binding = runtime.digest(runtime.canonical({'shared_inputs': measured, 'target_sources': self.targets}))
                runtime.provenance_shape({binding: context})
                self.assertEqual({'invalid_renewal'}, runtime.provenance_errors(context, current, self.targets, 'a.cs'))

    def test_v2_runner_exact_pair_requires_complete_current_assessment(self):
        old, new = APPROVED_RUNNER_PAIR
        self.shared[RUNNER_PATH] = old
        self.config['shared_inputs'] = self.shared.copy()
        binding = self.migrate_v2()
        for record in self.config['targets'].values():
            for item in record['shards'].values():
                item['timing_inputs_sha256'] = binding
        self.shared[RUNNER_PATH] = new
        self.config['shared_inputs'] = self.shared.copy()
        changes = self.config['timing_inputs'][binding]['assessments']['shared_inputs']
        self.assertIn('unaccounted_inputs', self.errors())
        changes[RUNNER_PATH] = {'current_sha256': new, 'basis': ['Synthetic accepted timing risk.']}
        self.assertEqual(set(), self.errors())
        changes[RUNNER_PATH]['current_sha256'] = old
        self.assertIn('unaccounted_inputs', self.errors())
        changes[RUNNER_PATH]['current_sha256'] = new
        changes['extra'] = {'current_sha256': None, 'basis': ['Synthetic extra assessment.']}
        self.assertIn('unaccounted_inputs', self.errors())
        del changes['extra']
        changes[RUNNER_PATH]['basis'] = []
        with self.assertRaises(ValueError):
            self.errors()

    def test_v2_runner_exception_rejects_other_pairs_and_nonrenewable_paths(self):
        old, new = APPROVED_RUNNER_PAIR
        other = runtime.digest(b'other')
        cases = [(RUNNER_PATH, other, new), (RUNNER_PATH, old, other),
                 (RUNNER_PATH, new, old), (RUNNER_PATH, None, new),
                 (RUNNER_PATH, old, None)]
        cases += [(path, old, new) for path in (
            PROJECTION_PATH, '.github/workflows/verifier-c5-mutation.yml',
            'tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json',
            '.config/dotnet-tools.json')]
        for path, measured_hash, current_hash in cases:
            with self.subTest(path=path, measured=measured_hash, current=current_hash):
                measured = {} if measured_hash is None else {path: measured_hash}
                current = {} if current_hash is None else {path: current_hash}
                context = {'shared_inputs': measured, 'target_sources': self.targets.copy(),
                           'assessments': {'shared_inputs': {path: {'current_sha256': current_hash,
                                                                    'basis': ['Synthetic risk.']}},
                                           'target_sources': {}}}
                binding = runtime.digest(runtime.canonical({'shared_inputs': measured, 'target_sources': self.targets}))
                runtime.provenance_shape({binding: context})
                self.assertEqual({'invalid_renewal'}, runtime.provenance_errors(context, current, self.targets, 'a.cs'))

    def test_v2_requires_evidence_for_current_target_source(self):
        self.migrate_v2()
        self.assertEqual(set(), self.errors())
        with mock.patch.object(runtime, 'evidence_matches_target', return_value=False):
            self.assertIn('missing_measurement', self.errors())
        record = self.config['targets']['a.cs']
        evidence = record['evidence'][0]
        with mock.patch.object(runtime, 'evidence_matches_target', self.original_evidence_matches_target), \
             mock.patch.object(runtime, 'blob', return_value=b'old source'):
            self.assertIn('missing_measurement', self.errors())
            with mock.patch.object(runtime, 'blob', side_effect=lambda commit, path: self.source[path.removeprefix('src/Parchwell.Verifier/')]):
                self.assertEqual(set(), self.errors())
            evidence['kind'] = 'suite_profile'
            self.assertIn('missing_measurement', self.errors())

    def test_v2_requires_per_shard_timing_for_changed_source(self):
        binding = self.migrate_v2()
        record = self.config['targets']['a.cs']
        proof = self.config['timing_evidence']['a-p1']
        old_timing = record['evidence'][0]
        geometry = dict(old_timing, source_commit='b' * 40, sha256='b' * 64, kind='hosted_complete', note='geometry only')
        record['evidence'].append(geometry)
        def source_at(commit, path):
            if commit == 'a' * 40 and path.endswith('/a.cs'):
                return b'old target source'
            return self.source[path.removeprefix('src/Parchwell.Verifier/')]
        with mock.patch.object(runtime, 'has_shard_timing', self.original_has_shard_timing), \
             mock.patch.object(runtime, 'blob', side_effect=source_at):
            # Geometry at the new source does not make old-source timing current.
            self.assertIn('missing_measurement', self.errors())
            new_timing = dict(geometry, sha256='c' * 64, note='measured shard timing')
            record['evidence'].append(new_timing)
            proof.update(artifact_sha256='c' * 64, source_commit='b' * 40)
            self.assertEqual(set(), self.errors())
            proof['timing_inputs_sha256'] = '0' * 64
            self.assertIn('missing_measurement', self.errors())
            proof['timing_inputs_sha256'] = binding
            proof['observed_workers'] = 1
            self.assertIn('missing_measurement', self.errors())
            proof['observed_workers'] = 2
            proof['source_commit'] = 'a' * 40
            self.assertIn('missing_measurement', self.errors())
            proof['source_commit'] = 'b' * 40
            del self.config['timing_evidence']['a-p1']
            self.assertIn('missing_measurement', self.errors())

    def test_v2_timing_metadata_rejects_empty_and_invalid_values(self):
        self.migrate_v2()
        proof = self.config['timing_evidence']['a-p1']
        for key, bad in [('observed_seconds', 0), ('observed_seconds', True),
                         ('observed_seconds', float('inf')), ('observed_workers', 0),
                         ('observed_workers', True), ('method', ''), ('method', '   '),
                         ('artifact_sha256', 'bad'), ('source_commit', 'bad')]:
            with self.subTest(key=key, value=bad):
                old = proof[key]
                proof[key] = bad
                with self.assertRaises(ValueError):
                    runtime.shape(self.config)
                proof[key] = old

    def test_v2_added_removed_and_unused_contexts(self):
        binding = self.migrate_v2()
        assessments = self.config['timing_inputs'][binding]['assessments']['shared_inputs']
        self.shared['added.cs'] = runtime.digest(b'new input')
        self.config['shared_inputs'] = self.shared.copy()
        self.assertIn('unaccounted_inputs', self.errors())
        assessments['added.cs'] = {'current_sha256': self.shared['added.cs'], 'basis': ['Synthetic risk acceptance.']}
        self.assertEqual(set(), self.errors())
        old = self.shared.pop('global.json')
        self.config['shared_inputs'] = self.shared.copy()
        self.assertIn('unaccounted_inputs', self.errors())
        assessments['global.json'] = {'current_sha256': old, 'basis': ['Synthetic risk acceptance.']}
        self.assertIn('unaccounted_inputs', self.errors())
        assessments['global.json']['current_sha256'] = None
        self.assertEqual(set(), self.errors())
        extra = {'shared_inputs': {}, 'target_sources': {}, 'assessments': {'shared_inputs': {}, 'target_sources': {}}}
        key = runtime.digest(runtime.canonical({'shared_inputs': {}, 'target_sources': {}}))
        self.config['timing_inputs'][key] = extra
        self.assertIn('unaccounted_inputs', self.errors())

    def test_v2_rejects_tampered_historical_inputs_missing_context_and_empty_basis(self):
        binding = self.migrate_v2()
        self.assertEqual(set(), self.errors())
        context = self.config['timing_inputs'][binding]
        context['target_sources']['b.cs'] = '0' * 64
        with self.assertRaises(ValueError):
            runtime.shape(self.config)
        context['target_sources']['b.cs'] = self.targets['b.cs']
        context['assessments']['target_sources']['b.cs'] = {'current_sha256': self.targets['b.cs'], 'basis': []}
        with self.assertRaises(ValueError):
            runtime.shape(self.config)
        del context['assessments']['target_sources']['b.cs']
        self.config['targets']['a.cs']['shards']['a-p1']['timing_inputs_sha256'] = '0' * 64
        self.assertIn('missing_measurement', self.errors())

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

    def test_supported_budget_shape(self):
        for version in (1, 2):
            if version == 2:
                self.migrate_v2()
            for budget in (1799, 1800, 1801, 2699, 2700, 2701, True, False, None, '', '1800', 1800.0):
                with self.subTest(version=version, budget=budget):
                    self.config['budget_seconds'] = budget
                    if type(budget) is int and budget in (1800, 2700):
                        runtime.shape(self.config)
                    else:
                        with self.assertRaises(ValueError):
                            runtime.shape(self.config)

    def test_opt_in_shard_budget_boundary(self):
        for version in (1, 2):
            if version == 2:
                self.migrate_v2()
            self.config['budget_seconds'] = 2700
            for seconds in (2699, 2700, 2701):
                with self.subTest(version=version, seconds=seconds):
                    item = self.config['targets']['a.cs']['shards']['a-p1']
                    item.update(main_work_seconds=2 * (seconds - 100), isolated_seconds=0, estimated_seconds=seconds)
                    expected = {'shard_over_budget'} if seconds > 2700 else set()
                    self.assertEqual(expected, self.errors())

    def test_only_measured_static_owner_has_budget_exception(self):
        path = 'VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs'
        data = historical_source(path)
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

    @historical_layout
    def test_codec_cohorts_require_measured_costs_and_stay_under_budget(self):
        import copy
        config = runtime.historical_qualification()
        path = runtime.CODEC_TARGET
        record = config['targets'][path]
        source = historical_source(path)
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

    @historical_layout
    def test_six_tail_estimates_require_source_evidence_and_promoted_charges(self):
        import copy
        config = runtime.historical_qualification()
        for shard, (path, _, _, _) in runtime.SIX_TAIL_BINDINGS.items():
            record = config['targets'][path]
            source = historical_source(path)
            item = record['shards'][shard]
            with self.subTest(shard=shard):
                self.assertTrue(runtime.approved_six_tail(path, record, shard, item, source))
                self.assertFalse(runtime.approved_six_tail(path, record, shard, item, source + b'x'))
                changed = copy.deepcopy(item)
                changed['setup_seconds'] -= 1
                self.assertFalse(runtime.approved_six_tail(path, record, shard, changed, source))
                changed_record = copy.deepcopy(record)
                locator = runtime.SIX_TAIL_BINDINGS[shard][3][0][0]
                changed_record['evidence'] = [e for e in record['evidence'] if e['locator'] != locator]
                self.assertFalse(runtime.approved_six_tail(path, changed_record, shard, item, source))
        for shard, charge, estimate in [('b10-final-classification-p20', 69, 1440),
                                         ('b10-final-registrations-p1', 67, 1742)]:
            path = runtime.SIX_TAIL_BINDINGS[shard][0]
            record = config['targets'][path]
            item = record['shards'][shard]
            self.assertEqual(estimate, item['estimated_seconds'])
            changed = copy.deepcopy(item)
            changed['setup_seconds'] -= charge
            changed['estimated_seconds'] -= charge
            source = historical_source(path)
            self.assertFalse(runtime.approved_six_tail(path, record, shard, changed, source))

    @historical_layout
    def test_six_tail_rejects_each_evidence_field_and_wrong_inputs(self):
        import copy
        config = runtime.historical_qualification()
        for shard, (path, _, _, required) in runtime.SIX_TAIL_BINDINGS.items():
            record = config['targets'][path]
            source = historical_source(path)
            item = record['shards'][shard]
            with self.subTest(shard=shard, case='wrong target'):
                self.assertFalse(runtime.approved_six_tail('wrong.cs', record, shard, item, source))
            with self.subTest(shard=shard, case='wrong shard'):
                self.assertFalse(runtime.approved_six_tail(path, record, 'wrong-shard', item, source))
            with self.subTest(shard=shard, case='empty evidence'):
                changed = copy.deepcopy(record)
                changed['evidence'] = []
                self.assertFalse(runtime.approved_six_tail(path, changed, shard, item, source))
            for locator, sha, commit in set(required):
                for field, value in [('kind', 'partial_timing'), ('sha256', '0' * 64),
                                     ('source_commit', '0' * 40)]:
                    with self.subTest(shard=shard, locator=locator, field=field):
                        changed = copy.deepcopy(record)
                        # Repeated identical references are equivalent evidence; change all copies.
                        matching = [e for e in changed['evidence'] if (e['locator'], e['sha256'], e['source_commit']) == (locator, sha, commit) and e['kind'] == 'hosted_complete']
                        self.assertTrue(matching)
                        for evidence in matching:
                            evidence[field] = value
                        self.assertFalse(runtime.approved_six_tail(path, changed, shard, item, source))
        self.assertFalse(runtime.approved_six_tail('', {}, '', {}, b''))
        shard, (path, _, _, _) = next(iter(runtime.SIX_TAIL_BINDINGS.items()))
        record = config['targets'][path]
        source = historical_source(path)
        self.assertFalse(runtime.approved_six_tail(path, {}, shard, record['shards'][shard], source))
        self.assertFalse(runtime.approved_six_tail(path, record, shard, {}, source))
        self.assertFalse(runtime.approved_six_tail(path, record, shard, record['shards'][shard], b''))

    @historical_layout
    def test_six_tail_validation_guard_overrides_other_approval(self):
        import copy
        self.workflow_patcher.stop()
        self.config_patcher.stop()
        config = runtime.historical_qualification()
        targets = {path: record['source_sha256'] for path, record in config['targets'].items()}
        sources = {path: historical_source(path) for path in targets}
        shard = 'b10-final-judge-p9'
        path = runtime.SIX_TAIL_BINDINGS[shard][0]
        with mock.patch.object(runtime, 'approved_codec_static_owner', return_value=True) as other_approval:
            self.assertEqual(set(), runtime.validate(config, config['shared_inputs'], targets, sources, runtime.workflow.SHARDS))
            changed = copy.deepcopy(config)
            item = changed['targets'][path]['shards'][shard]
            item['setup_seconds'] += 1
            item['estimated_seconds'] += 1
            reasons = runtime.validate(changed, changed['shared_inputs'], targets, sources, runtime.workflow.SHARDS)
            self.assertEqual({'invalid_estimate'}, reasons)
            self.assertTrue(any(call.args[0] == path and call.args[2] == shard for call in other_approval.call_args_list))

    @historical_layout
    def test_tail_splits_require_exact_source_cost_and_evidence(self):
        import copy
        config = runtime.historical_qualification()
        for shard, (path, _, _, artifact, _) in runtime.TAIL_SPLITS.items():
            with self.subTest(shard=shard):
                record = config['targets'][path]
                source = historical_source(path)
                item = record['shards'][shard]
                self.assertTrue(runtime.approved_tail_split(path, record, shard, item, source))
                self.assertFalse(runtime.approved_tail_split(path, record, shard, item, source + b'x'))
                changed = copy.deepcopy(item)
                changed['main_work_seconds'] -= 1
                self.assertFalse(runtime.approved_tail_split(path, record, shard, changed, source))
                changed_record = copy.deepcopy(record)
                changed_record['evidence'] = [e for e in record['evidence'] if artifact not in e['locator']]
                self.assertFalse(runtime.approved_tail_split(path, changed_record, shard, item, source))
                changed_config = copy.deepcopy(config)
                changed_config['targets'][path]['shards'][shard]['main_mutants'] = None
                with self.assertRaises(ValueError):
                    runtime.shape(changed_config)

    @historical_layout
    def test_tail_split_checks_span_and_each_evidence_field(self):
        import copy
        config = runtime.historical_qualification()
        for shard, (path, span, _, artifact, _) in runtime.TAIL_SPLITS.items():
            record = config['targets'][path]
            source = historical_source(path)
            item = record['shards'][shard]
            evidence = next(e for e in record['evidence'] if artifact in e['locator'])
            with self.subTest(shard=shard, case='span'):
                with mock.patch.object(runtime.workflow, 'shard_span', return_value=(span[0], span[1] + 1)):
                    self.assertFalse(runtime.approved_tail_split(path, record, shard, item, source))
            for field, value in [('kind', 'partial_timing'), ('source_commit', '0' * 40),
                                 ('locator', evidence['locator'] + '-other'), ('sha256', '0' * 64)]:
                with self.subTest(shard=shard, field=field):
                    changed = copy.deepcopy(record)
                    changed['evidence'] = [copy.deepcopy(evidence)]
                    changed['evidence'][0][field] = value
                    self.assertFalse(runtime.approved_tail_split(path, changed, shard, item, source))
            for evidence_list in ([], [e for e in record['evidence'] if artifact not in e['locator']]):
                changed = copy.deepcopy(record)
                changed['evidence'] = evidence_list
                self.assertFalse(runtime.approved_tail_split(path, changed, shard, item, source))

    @historical_layout
    def test_native_geometry_requires_exact_ownership_evidence(self):
        self.workflow_patcher.stop()
        self.config_patcher.stop()
        import copy
        config = runtime.historical_qualification()
        for path in (runtime.REPEAT_TARGET, runtime.CODEC_TARGET):
            record = config['targets'][path]
            source = historical_source(path)
            artifact = record['native_geometry']['ownership_artifact_id']
            evidence = next(e for e in record['evidence'] if artifact in e['locator'])
            self.assertTrue(runtime.native_geometry_valid(path, record, source))
            for field, value in [(None, None), ('kind', 'partial_timing'), ('source_commit', '0' * 40),
                                 ('locator', evidence['locator'] + '-other'), ('sha256', '0' * 64)]:
                with self.subTest(path=path, field=field):
                    changed = copy.deepcopy(record)
                    changed['evidence'] = [e for e in changed['evidence'] if artifact not in e['locator']]
                    if field is not None:
                        altered = copy.deepcopy(evidence)
                        altered[field] = value
                        changed['evidence'].append(altered)
                    self.assertFalse(runtime.native_geometry_valid(path, changed, source))

    def test_codec_over_budget_has_no_exception(self):
        path = runtime.CODEC_TARGET
        data = historical_source(path)
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

    @historical_layout
    def test_repeat_bounds_are_pinned_and_required(self):
        import copy
        config = runtime.historical_qualification()
        record = config['targets'][runtime.REPEAT_TARGET]
        source = historical_source(runtime.REPEAT_TARGET)
        for shard in runtime.REPEAT_BOUNDS:
            item = record['shards'][shard]
            with self.subTest(shard=shard):
                self.assertTrue(runtime.approved_repeat_merged_owner(runtime.REPEAT_TARGET, record, shard, item, source))
                self.assertLessEqual(item['estimated_seconds'], 1800)
                for field in runtime.NUMERIC_FIELDS:
                    candidate = copy.deepcopy(config)
                    candidate['targets'][runtime.REPEAT_TARGET]['shards'][shard][field] = None
                    with self.assertRaises(ValueError):
                        runtime.shape(candidate)
                changed = copy.deepcopy(item)
                changed['main_work_seconds'] -= 1
                self.assertFalse(runtime.approved_repeat_merged_owner(runtime.REPEAT_TARGET, record, shard, changed, source))
                changed = copy.deepcopy(record)
                changed['evidence'] = [e for e in changed['evidence'] if '14132774324' not in e['locator']]
                self.assertFalse(runtime.approved_repeat_merged_owner(runtime.REPEAT_TARGET, changed, shard, item, source))
                changed = copy.deepcopy(record)
                job = runtime.REPEAT_BOUND_JOBS[shard][0][0]
                changed['evidence'] = [e for e in changed['evidence'] if not e['locator'].endswith('/job/' + job)]
                self.assertFalse(runtime.approved_repeat_merged_owner(runtime.REPEAT_TARGET, changed, shard, item, source))
                self.assertFalse(runtime.approved_repeat_merged_owner(runtime.REPEAT_TARGET, record, shard, item, source + b'changed'))

    @historical_layout
    def test_codec_neighborhood_requires_exact_evidence_and_estimates(self):
        self.workflow_patcher.stop()
        self.config_patcher.stop()
        import copy
        config = runtime.historical_qualification()
        path = runtime.CODEC_TARGET
        record = config['targets'][path]
        source = historical_source(path)
        for shard in runtime.CODEC_NEIGHBOR:
            with self.subTest(shard=shard):
                item = record['shards'][shard]
                self.assertTrue(runtime.approved_codec_neighbor(path, record, shard, item, source))
                self.assertFalse(runtime.approved_codec_neighbor(path, record, shard, item, source + b'changed'))
                for field in runtime.NUMERIC_FIELDS:
                    changed = copy.deepcopy(item)
                    changed[field] += 1
                    self.assertFalse(runtime.approved_codec_neighbor(path, record, shard, changed, source))
                changed = copy.deepcopy(item)
                changed['timing_inputs_sha256'] = '0' * 64
                self.assertFalse(runtime.approved_codec_neighbor(path, record, shard, changed, source))
                for artifact, _ in runtime.CODEC_NEIGHBOR_EVIDENCE:
                    changed = copy.deepcopy(record)
                    changed['evidence'] = [e for e in changed['evidence'] if not e['locator'].endswith('artifact ' + artifact)]
                    self.assertFalse(runtime.approved_codec_neighbor(path, changed, shard, item, source))
                candidate = copy.deepcopy(config)
                candidate['targets'][path]['shards'][shard]['estimated_seconds'] = None
                with self.assertRaises(ValueError):
                    runtime.shape(candidate)
        with mock.patch.object(runtime.workflow, 'shard_span', return_value=(0, 1)):
            shard = 'b10-final-projection-codec-p8'
            self.assertFalse(runtime.approved_codec_neighbor(path, record, shard, record['shards'][shard], source))

    @historical_layout
    def test_codec_neighborhood_evidence_fields_and_adjacent_spans(self):
        self.workflow_patcher.stop()
        self.config_patcher.stop()
        import copy
        config = runtime.historical_qualification()
        path = runtime.CODEC_TARGET
        record = config['targets'][path]
        source = historical_source(path)
        for shard, (span, _) in runtime.CODEC_NEIGHBOR.items():
            item = record['shards'][shard]
            for artifact, sha in runtime.CODEC_NEIGHBOR_EVIDENCE:
                for field, value in (('kind', 'partial_timing'), ('source_commit', '0' * 40),
                                     ('locator', 'unrelated'), ('sha256', '0' * 64)):
                    with self.subTest(shard=shard, artifact=artifact, field=field):
                        changed = copy.deepcopy(record)
                        matches = [e for e in changed['evidence'] if e['locator'].endswith('artifact ' + artifact)
                                   and e['sha256'] == sha]
                        self.assertTrue(matches)
                        for evidence in matches:
                            evidence[field] = value
                        self.assertFalse(runtime.approved_codec_neighbor(path, changed, shard, item, source))
            for evidence in ([], [{'kind': 'hosted_complete', 'source_commit': '0' * 40,
                                   'locator': 'unrelated', 'sha256': '0' * 64, 'note': 'unrelated'}]):
                with self.subTest(shard=shard, evidence=evidence):
                    changed = copy.deepcopy(record)
                    changed['evidence'] = evidence
                    self.assertFalse(runtime.approved_codec_neighbor(path, changed, shard, item, source))
            if span is not None:
                for adjacent in ((span[0] - 1, span[1]), (span[0] + 1, span[1]),
                                 (span[0], span[1] - 1), (span[0], span[1] + 1)):
                    with self.subTest(shard=shard, adjacent=adjacent), \
                         mock.patch.object(runtime.workflow, 'shard_span', return_value=adjacent):
                        self.assertFalse(runtime.approved_codec_neighbor(path, record, shard, item, source))

    @historical_layout
    def test_codec_neighborhood_validation_guard_overrides_other_approval(self):
        self.workflow_patcher.stop()
        self.config_patcher.stop()
        import copy
        config = runtime.historical_qualification()
        targets = {path: record['source_sha256'] for path, record in config['targets'].items()}
        sources = {path: historical_source(path) for path in targets}
        path = runtime.CODEC_TARGET
        with mock.patch.object(runtime, 'approved_codec_static_owner', return_value=True) as other_approval:
            self.assertEqual(set(), runtime.validate(config, config['shared_inputs'], targets, sources, runtime.workflow.SHARDS))
            for shard in runtime.CODEC_NEIGHBOR:
                with self.subTest(shard=shard):
                    changed = copy.deepcopy(config)
                    item = changed['targets'][path]['shards'][shard]
                    item['setup_seconds'] += 1
                    item['estimated_seconds'] += 1
                    self.assertEqual({'invalid_estimate'}, runtime.validate(
                        changed, changed['shared_inputs'], targets, sources, runtime.workflow.SHARDS))
                    self.assertTrue(any(call.args[0] == path and call.args[2] == shard
                                        for call in other_approval.call_args_list))

    @historical_layout
    def test_pinned_native_geometry_rejects_tampering(self):
        self.workflow_patcher.stop()
        self.config_patcher.stop()
        import copy
        config = runtime.historical_qualification()
        for path in runtime.NATIVE_GEOMETRY_BINDINGS:
            with self.subTest(path=path):
                record = copy.deepcopy(config['targets'][path])
                source = historical_source(path)
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

    @historical_layout
    def test_pinned_codec_owners_cannot_be_unknown(self):
        import copy
        config = runtime.historical_qualification()
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
        changed = data.replace(b"'phase': (3694,", b"'phase': (3695,", 1)
        self.assertNotEqual(data, changed)
        self.assertEqual(runtime.execution_runner(data), runtime.execution_runner(changed))
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
        names = runtime.files('HEAD') | {runtime.COMPANION_WORKFLOW, runtime.CAMPAIGN_VALIDATOR}
        actual_blob = runtime.blob
        cache = {}
        additions = ('tests/fixtures/verifier-v1/runtime-test.json', 'src/Directory.Build.props',
                     'scenarios/profiles/runtime-test.yaml')
        def blob(ref, path):
            if path == runtime.RUNNER:
                return Path(runtime.workflow.__file__).read_bytes()
            if path in {runtime.COMPANION_WORKFLOW, runtime.CAMPAIGN_VALIDATOR}:
                return (ROOT / path).read_bytes()
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
            if (len(args) == 4 and args[:2] == ('merge-base', '--is-ancestor')
                    and args[2] in {runtime.PAIRED_TAIL_SOURCE, runtime.PAIRED_RECOVERY_SOURCE,
                                    runtime.OBSERVED_TAIL_SOURCE, runtime.TIMING_MARGIN_SOURCE}
                    and args[3] in {'base', 'added'}):
                raise subprocess.CalledProcessError(1, args)
            raise AssertionError(args)
        with mock.patch.object(runtime, 'files', side_effect=files), mock.patch.object(runtime, 'blob', side_effect=blob), \
             mock.patch.object(runtime, 'git', side_effect=git), \
             mock.patch.object(runtime, 'workflow_matrix', return_value=runtime.workflow.SHARDS), \
             mock.patch.object(runtime, 'workflow_layout', return_value=runtime.workflow.SHARDS), \
             mock.patch.object(runtime, 'SIX_TAIL_BINDINGS', {}), \
             mock.patch.object(runtime, 'CODEC_NEIGHBOR', {}):
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


class CampaignValidatorRecoveryTests(unittest.TestCase):
    """The one reviewed c5_campaign.py is recorded at its measured hash; anything else is compared as it is."""
    VALIDATOR = b'# synthetic reviewed validator\n'

    def environment(self, validator=None, runner=None, companion=lambda text: text, coordinator=lambda text: text):
        validator = self.VALIDATOR if validator is None else validator
        names = runtime.files('HEAD') | {runtime.COMPANION_WORKFLOW, runtime.CAMPAIGN_VALIDATOR}
        real_runner = Path(runtime.workflow.__file__).read_bytes()
        cache = {}

        def blob(ref, path):
            if path == runtime.RUNNER:
                return real_runner if runner is None else runner
            if path == runtime.CAMPAIGN_VALIDATOR:
                return validator
            if path == runtime.COMPANION_WORKFLOW:
                return companion((ROOT / path).read_text()).encode()
            if path == runtime.WORKFLOW:
                return coordinator((ROOT / path).read_text()).encode()
            if path not in cache:
                cache[path] = runtime.subprocess.check_output(['git', 'show', f'HEAD:{path}'], cwd=ROOT)
            return cache[path]

        def git(*args):
            if args[0] == 'rev-parse':
                return args[-1].replace('^{commit}', '').encode()
            if args[0] == 'diff':
                return b'README.md\0'
            if len(args) == 4 and args[:2] == ('merge-base', '--is-ancestor'):
                raise subprocess.CalledProcessError(1, args)
            raise AssertionError(args)

        stack = contextlib.ExitStack()
        for patch in (mock.patch.object(runtime, 'files', return_value=names),
                      mock.patch.object(runtime, 'blob', side_effect=blob),
                      mock.patch.object(runtime, 'git', side_effect=git),
                      mock.patch.object(runtime, 'workflow_matrix', return_value=runtime.workflow.SHARDS),
                      mock.patch.object(runtime, 'workflow_layout', return_value=runtime.workflow.SHARDS),
                      mock.patch.object(runtime, 'SIX_TAIL_BINDINGS', {}),
                      mock.patch.object(runtime, 'CODEC_NEIGHBOR', {}),
                      mock.patch.object(runtime, 'CAMPAIGN_VALIDATOR_PIN', runtime.digest(self.VALIDATOR))):
            stack.enter_context(patch)
        self.addCleanup(stack.close)
        return blob

    def test_pins_match_the_committed_files(self):
        runner = Path(runtime.workflow.__file__).read_bytes()
        self.assertEqual(runtime.RUNNER_EXECUTION_PIN, runtime.digest(runtime.execution_runner(runner)))
        committed = json.loads((ROOT / 'tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json').read_text())
        self.assertEqual(runtime.CAMPAIGN_VALIDATOR_BASE, committed['shared_inputs'][runtime.CAMPAIGN_VALIDATOR])
        self.assertEqual(runtime.CAMPAIGN_VALIDATOR_PIN, runtime.digest((ROOT / runtime.CAMPAIGN_VALIDATOR).read_bytes()))

    def recorded(self, **changes):
        self.environment(**changes)
        shared, _ = runtime.input_maps('HEAD')
        return shared[runtime.CAMPAIGN_VALIDATOR]

    BASE = '4ea8fae3d52111e37028c39033430bc0a3c40776'
    QUALIFICATION = 'tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json'

    def synthetic_head(self, **replacements):
        """Commit the base tree with some files replaced (path -> bytes); only git objects are written."""
        def git(*args, env=None, data=None):
            return subprocess.run(['git', *args], cwd=ROOT, check=True, capture_output=True, env=env,
                                  input=data).stdout.decode().strip()
        with tempfile.TemporaryDirectory() as directory:
            env = {**os.environ, 'GIT_INDEX_FILE': str(Path(directory) / 'index')}
            git('read-tree', self.BASE, env=env)
            for path, data in replacements.items():
                git('update-index', '--cacheinfo', f"100644,{git('hash-object', '-w', '--stdin', data=data)},{path}", env=env)
            tree = git('write-tree', env=env)
        return git('commit-tree', tree, '-p', self.BASE, '-m', 'synthetic head for a checker test')

    def check_head(self, base, **replacements):
        """Run `check` on a synthetic head that sits on the base commit and its qualification."""
        head = self.synthetic_head(**replacements)
        with tempfile.TemporaryDirectory() as directory:
            qualification = Path(directory) / 'qualification.json'
            qualification.write_bytes(runtime.blob(self.BASE, self.QUALIFICATION))
            return runtime.check(self.BASE if base else None, head, qualification)

    def test_check_outcomes_for_the_reviewed_validator(self):
        """A head that differs from the base only by the reviewed validator needs no new measurement."""
        reviewed = {runtime.CAMPAIGN_VALIDATOR: (ROOT / runtime.CAMPAIGN_VALIDATOR).read_bytes()}
        self.assertEqual({'status': 'not_applicable', 'reasons': []}, self.check_head(True, **reviewed))
        result = self.check_head(False, **reviewed)
        self.assertEqual(('qualified', []), (result['status'], result['reasons']))

    def test_check_rejects_a_validator_that_is_not_the_reviewed_one(self):
        real = (ROOT / runtime.CAMPAIGN_VALIDATOR).read_bytes()
        companion = runtime.blob(self.BASE, runtime.COMPANION_WORKFLOW) + b'# c5_campaign\n'
        cases = {'one byte off the pin': {runtime.CAMPAIGN_VALIDATOR: real + b'#'},
                 'companion workflow mentions the validator': {runtime.CAMPAIGN_VALIDATOR: real,
                                                               runtime.COMPANION_WORKFLOW: companion}}
        for label, replacements in cases.items():
            for base in (True, False):
                with self.subTest(label, base=base):
                    result = self.check_head(base, **replacements)
                    self.assertEqual('rejected', result['status'])
                    self.assertIn('missing_measurement', result['reasons'])

    def test_check_refuses_a_head_with_another_runner(self):
        runner = runtime.blob(self.BASE, runtime.RUNNER) + b'\n#\n'
        for base in (True, False):
            with self.subTest(base=base):
                # The checker refuses a head whose runner differs from the one it runs; no reason is pinned.
                self.assertEqual('rejected', self.check_head(base, **{runtime.RUNNER: runner})['status'])

    def test_input_maps_records_the_measured_hash_only_for_the_reviewed_validator(self):
        self.assertEqual(runtime.CAMPAIGN_VALIDATOR_BASE, self.recorded())
        off = self.VALIDATOR + b'#'
        cases = {
            'validator one byte off the pin': {'validator': off},
            'runner changed by one line': {'runner': Path(runtime.workflow.__file__).read_bytes() + b'\n#\n'},
            'companion workflow mentions the validator': {'companion': lambda text: text + '# c5_campaign\n'},
            'coordinator job mentions the validator': {'coordinator': lambda text: text.replace(
                '\n  aggregate:\n', '\n    # c5_campaign\n\n  aggregate:\n', 1)},
        }
        for label, changes in cases.items():
            with self.subTest(label):
                actual = self.recorded(**changes)
                self.assertEqual(runtime.digest(changes.get('validator', self.VALIDATOR)), actual)
                self.assertNotEqual(runtime.CAMPAIGN_VALIDATOR_BASE, actual)
        with self.subTest('a mention after the aggregate job is the reviewed step'):
            self.assertEqual(runtime.CAMPAIGN_VALIDATOR_BASE, self.recorded(
                coordinator=lambda text: text + '\n# c5_campaign\n'))


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

    def test_v2_preserves_pilot_binding_with_assessed_other_target(self):
        record, item = self.provisional()
        old_binding = QualificationTests.migrate_v2(self)
        self.source['b.cs'] = b'changed target'
        self.targets['b.cs'] = runtime.digest(self.source['b.cs'])
        other = self.config['targets']['b.cs']
        other['source_sha256'] = self.targets['b.cs']
        other['shards']['b']['selection_sha256'] = runtime.digest(runtime.canonical(runtime.workflow.shard_config('b', self.source['b.cs'])))
        binding = runtime.digest(runtime.canonical({'shared_inputs': self.shared, 'target_sources': self.targets}))
        self.config['timing_inputs'][binding] = {'shared_inputs': self.shared.copy(), 'target_sources': self.targets.copy(),
            'assessments': {'shared_inputs': {}, 'target_sources': {}}}
        other['shards']['b']['timing_inputs_sha256'] = binding
        self.assertIn('unaccounted_inputs', self.errors())
        self.config['timing_inputs'][old_binding]['assessments']['target_sources']['b.cs'] = {
            'current_sha256': self.targets['b.cs'], 'basis': ['Synthetic accepted risk.']}
        self.assertEqual(set(), self.errors())
        self.assertEqual(old_binding, item['timing_inputs_sha256'])
        item['timing_inputs_sha256'] = binding
        self.assertIn('stale_inputs', self.errors())
        item['timing_inputs_sha256'] = old_binding
        record['evidence'].clear()
        self.assertIn('missing_measurement', self.errors())

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


class RenewalTests(unittest.TestCase):
    setUp = QualificationTests.setUp
    errors = QualificationTests.errors
    simulated_check = AdditionalQualificationTests.simulated_check
    provisional = ProvisionalQualificationTests.provisional

    def renew_fixture(self, path, old, new):
        if new is None:
            self.shared.pop(path, None)
        else:
            self.shared[path] = new
        self.config['shared_inputs'] = self.shared.copy()
        self.config['renewals'] = {path: {'measured_sha256': old, 'current_sha256': new,
                                          'basis': ['synthetic renewal']}}

    def test_unrenewed_shared_change_stays_stale(self):
        self.shared['global.json'] = runtime.digest(b'new sdk')
        self.config['shared_inputs'] = self.shared.copy()
        self.assertEqual({'stale_inputs'}, self.errors())

    def test_renewed_change_keeps_measured_estimates(self):
        self.renew_fixture('global.json', runtime.digest(b'sdk'), runtime.digest(b'new sdk'))
        self.assertEqual(set(), self.errors())

    def test_renewed_addition_and_deletion(self):
        for path, old, new in [('tests/fixtures/verifier-v1/new.json', None, runtime.digest(b'x')),
                               ('global.json', runtime.digest(b'sdk'), None)]:
            with self.subTest(path=path):
                self.setUp()
                self.renew_fixture(path, old, new)
                self.assertEqual(set(), self.errors())

    def test_non_renewable_paths(self):
        for path in ('tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py',
                     '.github/workflows/verifier-c5-mutation.yml',
                     'tests/Parchwell.Verifier.Tests/Mutation/c5_projection.py',
                     'tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json',
                     '.config/dotnet-tools.json'):
            with self.subTest(path=path):
                self.setUp()
                self.shared[path] = runtime.digest(b'r1')
                self.config['shared_inputs'] = self.shared.copy()
                timing = runtime.digest(runtime.canonical({'shared_inputs': self.shared, 'target_sources': self.targets}))
                for record in self.config['targets'].values():
                    for item in record['shards'].values():
                        item['timing_inputs_sha256'] = timing
                self.assertEqual(set(), self.errors())
                self.renew_fixture(path, runtime.digest(b'r1'), runtime.digest(b'r2'))
                self.assertEqual({'invalid_renewal'}, self.errors())

    def test_v1_rejects_even_the_projection_timing_pair(self):
        old, new = APPROVED_PROJECTION_PAIR
        self.shared[PROJECTION_PATH] = old
        self.config['shared_inputs'] = self.shared.copy()
        timing = runtime.digest(runtime.canonical({'shared_inputs': self.shared, 'target_sources': self.targets}))
        for record in self.config['targets'].values():
            for item in record['shards'].values():
                item['timing_inputs_sha256'] = timing
        self.assertEqual(set(), self.errors())
        self.renew_fixture(PROJECTION_PATH, old, new)
        self.assertEqual({'invalid_renewal'}, self.errors())

    def test_v1_rejects_even_the_runner_timing_pair(self):
        old, new = APPROVED_RUNNER_PAIR
        self.shared[RUNNER_PATH] = old
        self.config['shared_inputs'] = self.shared.copy()
        timing = runtime.digest(runtime.canonical({'shared_inputs': self.shared, 'target_sources': self.targets}))
        for record in self.config['targets'].values():
            for item in record['shards'].values():
                item['timing_inputs_sha256'] = timing
        self.assertEqual(set(), self.errors())
        self.renew_fixture(RUNNER_PATH, old, new)
        self.assertEqual({'invalid_renewal'}, self.errors())

    def test_renewal_must_change_the_hash(self):
        for path, value in [('global.json', runtime.digest(b'sdk')), ('docs/x.json', None)]:
            with self.subTest(path=path):
                self.config['renewals'] = {path: {'measured_sha256': value, 'current_sha256': value, 'basis': ['x']}}
                self.assertEqual({'invalid_renewal'}, self.errors())

    def test_renewal_must_match_shared_inputs(self):
        self.config['renewals'] = {'global.json': {'measured_sha256': runtime.digest(b'old'),
            'current_sha256': runtime.digest(b'other'), 'basis': ['x']}}
        self.assertEqual({'invalid_renewal', 'stale_inputs'}, self.errors())

    def test_target_source_cannot_be_renewed(self):
        self.config['renewals'] = {'src/Parchwell.Verifier/a.cs': {'measured_sha256': runtime.digest(b'a'),
            'current_sha256': None, 'basis': ['x']}}
        self.assertEqual({'invalid_renewal', 'stale_inputs'}, self.errors())

    def test_malformed_renewals_are_invalid_input(self):
        valid = {'measured_sha256': runtime.digest(b'sdk'), 'current_sha256': runtime.digest(b'new sdk'), 'basis': ['x']}
        cases = [[], {'global.json': {**valid, 'note': 'x'}},
                 {'global.json': {**valid, 'measured_sha256': 'A' * 64}},
                 {'global.json': {**valid, 'current_sha256': 'a' * 63}},
                 {'global.json': {**valid, 'current_sha256': 'a' * 65}},
                 *({'global.json': {**valid, 'basis': value}} for value in ([], ['  '], 'x', [1])),
                 {'global.json': {'measured_sha256': valid['measured_sha256'], 'current_sha256': valid['current_sha256']}},
                 {'global.json': 'x'}]
        for value in cases:
            with self.subTest(renewals=value):
                self.config['renewals'] = value
                with self.assertRaises(ValueError):
                    runtime.shape(self.config)
                self.assertEqual({'status': 'rejected', 'reasons': ['invalid_input']}, self.simulated_check('README.md'))
        self.config.pop('renewals')
        self.config['other'] = 'x'
        with self.assertRaises(ValueError):
            runtime.shape(self.config)
        self.assertEqual({'status': 'rejected', 'reasons': ['invalid_input']}, self.simulated_check('README.md'))

    def test_empty_renewals_are_no_renewals(self):
        self.config['renewals'] = {}
        self.assertEqual(set(), self.errors())
        self.assertEqual({'status': 'qualified', 'reasons': []}, self.simulated_check(runtime.RUNNER))

    def test_check_discloses_renewed_inputs(self):
        self.renew_fixture('global.json', runtime.digest(b'sdk'), runtime.digest(b'new sdk'))
        self.assertEqual({'status': 'qualified', 'reasons': [], 'renewed_inputs': ['global.json']},
            self.simulated_check('src/Parchwell.Verifier/AssemblyInfo.cs', old_shared={'global.json': runtime.digest(b'sdk')}))

    def test_provisional_target_survives_renewal(self):
        self.provisional()
        self.renew_fixture('global.json', runtime.digest(b'sdk'), runtime.digest(b'new sdk'))
        self.assertEqual(set(), self.errors())
        self.assertEqual({'status': 'provisional', 'reasons': [], 'pending_targets': ['a.cs'], 'renewed_inputs': ['global.json']},
            self.simulated_check('global.json', old_shared={'global.json': runtime.digest(b'sdk')}))

    def test_rejected_result_has_no_renewed_inputs(self):
        for case in ('estimate', 'layout', 'not_applicable'):
            with self.subTest(case=case):
                self.setUp()
                self.renew_fixture('global.json', runtime.digest(b'sdk'), runtime.digest(b'new sdk'))
                if case == 'estimate':
                    self.config['targets']['b.cs']['shards']['b']['estimated_seconds'] = 1801
                    expected = {'status': 'rejected', 'reasons': ['invalid_estimate']}
                elif case == 'layout':
                    self.config['targets']['b.cs']['shards']['b']['selection_sha256'] = '0' * 64
                    expected = {'status': 'rejected', 'reasons': ['layout_mismatch']}
                else:
                    expected = {'status': 'not_applicable', 'reasons': []}
                result = (self.simulated_check('README.md') if case == 'not_applicable' else
                          self.simulated_check('global.json', old_shared={'global.json': runtime.digest(b'sdk')}))
                self.assertEqual(expected, result)

    @historical_layout
    def test_committed_qualification_renews_a_comment_change(self):
        self.workflow_patcher.stop()
        self.config_patcher.stop()
        path = 'src/Parchwell.Verifier/AssemblyInfo.cs'
        actual_blob = runtime.blob
        def blob(ref, name):
            data = actual_blob(ref, name)
            return data + b'// renewal test\n' if name == path else data
        config = runtime.historical_qualification()
        with mock.patch.object(runtime, 'blob', side_effect=blob):
            shared, targets = runtime.input_maps(runtime.HISTORICAL_QUALIFICATION_COMMIT)
            sources = {p: historical_source(p) for p in targets}
            matrix = runtime.workflow_matrix(runtime.blob(runtime.HISTORICAL_QUALIFICATION_COMMIT, runtime.WORKFLOW))
            self.assertEqual({'stale_inputs'}, runtime.validate(config, shared, targets, sources, matrix))
            renewals = config.setdefault('renewals', {})
            measured = renewals[path]['measured_sha256'] if path in renewals else config['shared_inputs'].get(path)
            renewals[path] = {'measured_sha256': measured, 'current_sha256': shared[path], 'basis': ['synthetic renewal']}
            config['shared_inputs'] = shared
            self.assertEqual(set(), runtime.validate(config, shared, targets, sources, matrix))

    def test_renew_command(self):
        import contextlib
        import io
        import sys
        old = runtime.digest(b'sdk')
        new = runtime.digest(b'new sdk')
        newer = runtime.digest(b'newer sdk')
        runner = Path(runtime.workflow.__file__).read_bytes()
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'qualification.json'
            initial = copy.deepcopy(self.config)
            path.write_text(json.dumps(initial, indent=2, sort_keys=True) + '\n')
            def invoke(shared, targets, basis='synthetic renewal', head_runner=None):
                stdout, stderr = io.StringIO(), io.StringIO()
                argv = ['c5_runtime.py', 'renew', '--head', 'head', '--qualification', str(path), '--basis', basis]
                with mock.patch.object(sys, 'argv', argv), mock.patch.object(runtime, 'git', return_value=b'head\n'), \
                     mock.patch.object(runtime, 'blob', return_value=runner if head_runner is None else head_runner), \
                     mock.patch.object(runtime, 'input_maps', return_value=(shared, targets)), \
                     contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                    status = runtime.main()
                return status, stdout.getvalue(), stderr.getvalue()

            with self.subTest(case='first renewal'):
                self.assertEqual((0, '{"renewed": ["global.json"], "status": "renewed"}\n', ''),
                                 invoke({'global.json': new}, self.targets))
                expected = copy.deepcopy(initial)
                expected['shared_inputs'] = {'global.json': new}
                expected['renewals'] = {'global.json': {'measured_sha256': old, 'current_sha256': new,
                                                         'basis': ['synthetic renewal']}}
                self.assertEqual(json.dumps(expected, indent=2, sort_keys=True) + '\n', path.read_text())
                self.config = json.loads(path.read_text())
                self.shared = {'global.json': new}
                self.assertEqual({'status': 'qualified', 'reasons': [], 'renewed_inputs': ['global.json']},
                                 self.simulated_check('global.json', old_shared={'global.json': old}))
            with self.subTest(case='second renewal'):
                self.assertEqual((0, '{"renewed": ["global.json"], "status": "renewed"}\n', ''),
                                 invoke({'global.json': newer}, self.targets, 'second'))
                expected['shared_inputs'] = {'global.json': newer}
                expected['renewals']['global.json']['current_sha256'] = newer
                expected['renewals']['global.json']['basis'].append('second')
                self.assertEqual(json.dumps(expected, indent=2, sort_keys=True) + '\n', path.read_text())
            with self.subTest(case='return to measured'):
                self.assertEqual((0, '{"renewed": ["global.json"], "status": "renewed"}\n', ''),
                                 invoke({'global.json': old}, self.targets))
                self.assertEqual(initial, json.loads(path.read_text()))
            with self.subTest(case='unchanged'):
                before = path.read_bytes()
                self.assertEqual((0, '{"renewed": [], "status": "unchanged"}\n', ''), invoke({'global.json': old}, self.targets))
                self.assertEqual(before, path.read_bytes())
            with self.subTest(case='blocked'):
                before = path.read_bytes()
                status, stdout, stderr = invoke({'global.json': old, '.config/dotnet-tools.json': new}, self.targets)
                self.assertEqual(1, status)
                self.assertEqual('', stdout)
                self.assertIn('not renewable: .config/dotnet-tools.json', stderr)
                self.assertEqual(before, path.read_bytes())
            with self.subTest(case='target changed'):
                before = path.read_bytes()
                status, stdout, stderr = invoke({'global.json': old}, {**self.targets, 'b.cs': new})
                self.assertEqual(1, status)
                self.assertEqual('', stdout)
                self.assertIn('target sources changed', stderr)
                self.assertEqual(before, path.read_bytes())
            with self.subTest(case='blank basis'):
                before = path.read_bytes()
                status, stdout, _ = invoke({'global.json': new}, self.targets, '  ')
                self.assertEqual(1, status)
                self.assertEqual('', stdout)
                self.assertEqual(before, path.read_bytes())
            with self.subTest(case='runner differs from head'):
                before = path.read_bytes()
                status, stdout, stderr = invoke({'global.json': new}, self.targets, head_runner=b'changed runner')
                self.assertEqual(1, status)
                self.assertEqual('', stdout)
                self.assertIn('checker is not running the requested revision', stderr)
                self.assertEqual(before, path.read_bytes())
            with self.subTest(case='malformed qualification'):
                malformed = copy.deepcopy(initial)
                malformed['renewals'] = {'global.json': 'invalid'}
                path.write_text(json.dumps(malformed))
                before = path.read_bytes()
                status, stdout, _ = invoke({'global.json': new}, self.targets)
                self.assertEqual(1, status)
                self.assertEqual('', stdout)
                self.assertEqual(before, path.read_bytes())
            with self.subTest(case='target set changed'):
                path.write_text(json.dumps(initial, indent=2, sort_keys=True) + '\n')
                before = path.read_bytes()
                status, stdout, stderr = invoke({'global.json': new}, {'a.cs': self.targets['a.cs']})
                self.assertEqual(1, status)
                self.assertEqual('', stdout)
                self.assertIn('target sources changed', stderr)
                self.assertEqual(before, path.read_bytes())
            with self.subTest(case='atomic replacement fails'):
                before = path.read_bytes()
                entries = set(Path(directory).iterdir())
                with mock.patch.object(runtime.os, 'replace', side_effect=OSError('synthetic replacement failure')) as replace:
                    status, stdout, stderr = invoke({'global.json': new}, self.targets)
                replace.assert_called_once()
                self.assertEqual(1, status)
                self.assertEqual('', stdout)
                self.assertIn('synthetic replacement failure', stderr)
                self.assertEqual(before, path.read_bytes())
                self.assertEqual(entries, set(Path(directory).iterdir()))


class RenewalV2Tests(unittest.TestCase):
    setUp = QualificationTests.setUp
    D1 = 'synthetic decision 1'
    D2 = 'synthetic decision 2'
    A, B, C = (runtime.digest(value) for value in (b'A', b'B', b'C'))

    def build(self, previous, *contexts):
        """Version 2 file whose top level is `previous`; each context is (measured shared map, assessments)."""
        self.config['schema_version'] = 2
        self.config['shared_inputs'] = dict(previous)
        self.config['timing_inputs'] = {}
        self.config['timing_evidence'] = {}
        bindings = []
        for measured, assessments, *extra in contexts:
            targets, target_changes = extra or (self.targets, {})
            binding = runtime.digest(runtime.canonical({'shared_inputs': measured, 'target_sources': targets}))
            self.config['timing_inputs'][binding] = {'shared_inputs': dict(measured),
                'target_sources': dict(targets),
                'assessments': {'shared_inputs': copy.deepcopy(assessments), 'target_sources': copy.deepcopy(target_changes)}}
            bindings.append(binding)
        for index, (shard, path) in enumerate(self.shards.items()):
            binding = bindings[min(index, len(bindings) - 1)]
            self.config['targets'][path]['shards'][shard]['timing_inputs_sha256'] = binding
            self.config['timing_evidence'][shard] = {'kind': 'measured', 'source_commit': 'a' * 40,
                'artifact_sha256': 'a' * 64, 'timing_inputs_sha256': binding, 'observed_workers': 2,
                'observed_seconds': 10, 'method': 'synthetic execution timing'}
        return [self.config['timing_inputs'][binding] for binding in bindings]

    def assessed(self, current, *basis):
        return {'current_sha256': current, 'basis': list(basis)}

    def write(self, directory):
        self.path = Path(directory) / 'qualification.json'
        self.path.write_text(json.dumps(self.config, indent=2, sort_keys=True) + '\n')

    def mocks(self, shared, targets=None, runner=None):
        original = Path(runtime.workflow.__file__).read_bytes()
        targets = self.targets if targets is None else targets
        def blob(ref, name):
            if name == runtime.RUNNER:
                return original if runner is None else runner
            if name == runtime.WORKFLOW:
                return b'matrix'
            return self.source[name.removeprefix('src/Parchwell.Verifier/')]
        return (mock.patch.object(runtime, 'git', return_value=b'head\n'), mock.patch.object(runtime, 'blob', side_effect=blob),
                mock.patch.object(runtime, 'input_maps', return_value=(shared, targets)),
                mock.patch.object(runtime, 'workflow_matrix', return_value=self.shards))

    def invoke(self, shared, basis=None, targets=None, runner=None, command='renew'):
        import contextlib
        import io
        import sys
        basis = self.D1 if basis is None else basis
        argv = ['c5_runtime.py', command, '--head', 'head', '--qualification', str(self.path)]
        if command == 'renew':
            argv += ['--basis', basis]
        stdout, stderr = io.StringIO(), io.StringIO()
        git, blob, input_maps, matrix = self.mocks(shared, targets, runner)
        with mock.patch.object(sys, 'argv', argv), git, blob, input_maps, matrix, \
             contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            status = runtime.main()
        return status, stdout.getvalue(), stderr.getvalue()

    def checked(self, shared):
        status, stdout, _ = self.invoke(shared, command='check')
        return status, json.loads(stdout)

    def test_v2_context_renewal(self):
        other, other_old = runtime.digest(b'other current'), runtime.digest(b'other measured')
        previous = {'global.json': self.A, 'other.json': other}
        unrelated = {'other.json': self.assessed(other, 'unrelated risk')}
        stale_b = {**self.targets, 'b.cs': runtime.digest(b'old b')}
        target_change = {'b.cs': self.assessed(self.targets['b.cs'], 'unrelated target risk')}
        contexts = self.build(previous,
                              ({'global.json': self.A, 'other.json': other_old}, unrelated, stale_b, target_change),
                              ({'global.json': self.B, 'other.json': other},
                               {'global.json': self.assessed(self.A, self.D1)}))
        with tempfile.TemporaryDirectory() as directory:
            self.write(directory)
            expected = copy.deepcopy(self.config)
            head = {'global.json': self.C, 'other.json': other}
            self.assertEqual((0, '{"renewed": ["global.json"], "status": "renewed"}\n', ''), self.invoke(head, self.D2))
            expected['shared_inputs'] = head
            first, second = (expected['timing_inputs'][key] for key in
                             (next(iter(expected['timing_inputs'])), list(expected['timing_inputs'])[1]))
            first['assessments']['shared_inputs']['global.json'] = self.assessed(self.C, self.D2)
            second['assessments']['shared_inputs']['global.json'] = self.assessed(self.C, self.D1, self.D2)
            self.assertEqual(json.dumps(expected, indent=2, sort_keys=True) + '\n', self.path.read_text())
            status, result = self.checked(head)
            self.assertEqual((0, 'qualified', []), (status, result['status'], result['reasons']))
            self.assertEqual(1, result['assessed_inputs'].count('shared_inputs:global.json'))
            head = {'global.json': self.B, 'other.json': other}
            self.assertEqual((0, '{"renewed": ["global.json"], "status": "renewed"}\n', ''), self.invoke(head, self.D1))
            expected['shared_inputs'] = head
            del second['assessments']['shared_inputs']['global.json']
            first['assessments']['shared_inputs']['global.json'] = self.assessed(self.B, self.D2, self.D1)
            self.assertEqual(json.dumps(expected, indent=2, sort_keys=True) + '\n', self.path.read_text())
            self.assertEqual(0, self.checked(head)[0])
            before = self.path.read_bytes()
            self.assertEqual((0, '{"renewed": [], "status": "unchanged"}\n', ''), self.invoke(head, self.D2))
            self.assertEqual(before, self.path.read_bytes())

    def test_v2_membership(self):
        old = runtime.digest(b'old')
        self.build({'old.cs': old}, ({'old.cs': old}, {}))
        with tempfile.TemporaryDirectory() as directory:
            self.write(directory)
            self.assertEqual((0, '{"renewed": ["new.cs", "old.cs"], "status": "renewed"}\n', ''),
                             self.invoke({'new.cs': self.B}, self.D1))
            context = next(iter(json.loads(self.path.read_text())['timing_inputs'].values()))
            self.assertEqual({'old.cs': self.assessed(None, self.D1), 'new.cs': self.assessed(self.B, self.D1)},
                             context['assessments']['shared_inputs'])
            self.assertEqual((0, '{"renewed": ["new.cs", "old.cs"], "status": "renewed"}\n', ''),
                             self.invoke({'old.cs': old}, self.D2))
            document = json.loads(self.path.read_text())
            self.assertEqual({}, next(iter(document['timing_inputs'].values()))['assessments']['shared_inputs'])
            self.assertEqual({'old.cs': old}, document['shared_inputs'])
            status, result = self.checked({'old.cs': old})
            self.assertEqual((0, 'qualified'), (status, result['status']))
            self.assertNotIn('assessed_inputs', result)

    def test_v2_refusal(self):
        other = runtime.digest(b'other')
        tools, companion = '.config/dotnet-tools.json', runtime.COMPANION_WORKFLOW
        def refused(label, shared, text, *, build=None, basis=None, targets=None, runner=None, replace_fails=False):
            with self.subTest(case=label), tempfile.TemporaryDirectory() as directory:
                self.build(*(build or ({'global.json': self.A}, ({'global.json': self.A}, {}))))
                self.write(directory)
                before, entries = self.path.read_bytes(), set(Path(directory).iterdir())
                if replace_fails:
                    with mock.patch.object(runtime.os, 'replace', side_effect=OSError(text)):
                        status, stdout, stderr = self.invoke(shared, basis, targets, runner)
                else:
                    status, stdout, stderr = self.invoke(shared, basis, targets, runner)
                self.assertEqual((1, ''), (status, stdout))
                self.assertEqual(text + '\n', stderr) if not replace_fails else self.assertIn(text, stderr)
                self.assertEqual(before, self.path.read_bytes())
                self.assertEqual(entries, set(Path(directory).iterdir()))
        refused('nonrenewable', {'global.json': self.C, tools: other, companion: other},
                f'not renewable: {tools}, {companion}')
        refused('changed target', {'global.json': self.A}, 'target sources changed', targets={**self.targets, 'b.cs': other})
        refused('added target', {'global.json': self.A}, 'target sources changed', targets={**self.targets, 'c.cs': other})
        refused('removed target', {'global.json': self.A}, 'target sources changed', targets={'a.cs': self.targets['a.cs']})
        refused('blank basis', {'global.json': self.C}, 'basis must not be empty', basis='  ')
        refused('runner differs', {'global.json': self.C}, 'checker is not running the requested revision', runner=b'changed runner')
        gap = ({'global.json': self.A, 'x.json': self.A}, {})
        refused('missing assessment with a change', {'global.json': self.C, 'x.json': self.B},
                'qualification rejected: unaccounted_inputs', build=({'global.json': self.A, 'x.json': self.B}, gap))
        refused('missing assessment without a change', {'global.json': self.A, 'x.json': self.B},
                'qualification rejected: unaccounted_inputs', build=({'global.json': self.A, 'x.json': self.B}, gap))
        refused('replacement fails', {'global.json': self.C}, 'synthetic replacement failure', replace_fails=True)

    def test_v2_protected_pairs(self):
        for path, (old, new) in ((PROJECTION_PATH, APPROVED_PROJECTION_PAIR), (RUNNER_PATH, APPROVED_RUNNER_PAIR)):
            with self.subTest(path=path), tempfile.TemporaryDirectory() as directory:
                self.build({path: old}, ({path: old}, {}), ({path: old}, {}))
                self.write(directory)
                self.assertEqual((0, json.dumps({'renewed': [path], 'status': 'renewed'}) + '\n', ''),
                                 self.invoke({path: new}, self.D1))
                for context in json.loads(self.path.read_text())['timing_inputs'].values():
                    self.assertEqual({path: self.assessed(new, self.D1)}, context['assessments']['shared_inputs'])
            for label, previous, head in (('reversed', new, old), ('other hash', old, new[:-1] + ('1' if new[-1] == '0' else '0'))):
                with self.subTest(path=path, case=label), tempfile.TemporaryDirectory() as directory:
                    self.build({path: previous}, ({path: previous}, {}), ({path: previous}, {}))
                    self.write(directory)
                    before = self.path.read_bytes()
                    self.assertEqual((1, '', f'not renewable: {path}\n'), self.invoke({path: head}, self.D1))
                    self.assertEqual(before, self.path.read_bytes())
        with tempfile.TemporaryDirectory() as directory:
            old, new = runtime.digest(b'config old'), runtime.digest(b'config new')
            self.build({runtime.CONFIG_PATH: old}, ({runtime.CONFIG_PATH: old}, {}))
            self.write(directory)
            before = self.path.read_bytes()
            with mock.patch.object(runtime, 'committed_effective_config', return_value=True):
                self.assertEqual((1, '', f'not renewable: {runtime.CONFIG_PATH}\n'), self.invoke({runtime.CONFIG_PATH: new}))
            self.assertEqual(before, self.path.read_bytes())
        with tempfile.TemporaryDirectory() as directory:
            old, new = runtime.digest(b'config old'), runtime.digest(b'config new')
            previous = {'global.json': self.A, runtime.CONFIG_PATH: new}
            held = {runtime.CONFIG_PATH: self.assessed(new, 'equivalence retained')}
            self.build(previous, ({'global.json': self.A, runtime.CONFIG_PATH: old}, held))
            self.write(directory)
            with mock.patch.object(runtime, 'committed_effective_config', return_value=True):
                self.assertEqual((0, '{"renewed": ["global.json"], "status": "renewed"}\n', ''),
                                 self.invoke({'global.json': self.C, runtime.CONFIG_PATH: new}))
            assessments = next(iter(json.loads(self.path.read_text())['timing_inputs'].values()))['assessments']['shared_inputs']
            self.assertEqual({'global.json': self.assessed(self.C, self.D1), runtime.CONFIG_PATH: held[runtime.CONFIG_PATH]},
                             assessments)

    def test_v2_provisional_result_is_written(self):
        self.build({'global.json': self.A}, ({'global.json': self.A}, {}))
        ProvisionalQualificationTests.provisional(self)
        with tempfile.TemporaryDirectory() as directory:
            self.write(directory)
            self.assertEqual((0, '{"renewed": ["global.json"], "status": "renewed"}\n', ''),
                             self.invoke({'global.json': self.C}, self.D1))
            status, result = self.checked({'global.json': self.C})
            self.assertEqual((0, 'provisional', ['a.cs']), (status, result['status'], result['pending_targets']))

    def test_v2_unchanged_file_is_not_rewritten_when_not_canonical(self):
        self.build({'global.json': self.A}, ({'global.json': self.A}, {}))
        with tempfile.TemporaryDirectory() as directory:
            self.write(directory)
            self.path.write_text(json.dumps(self.config, indent=4) + '\n')
            before = self.path.read_bytes()
            self.assertEqual((0, '{"renewed": [], "status": "unchanged"}\n', ''), self.invoke({'global.json': self.A}))
            self.assertEqual(before, self.path.read_bytes())

    @unittest.skipUnless(shutil.which('git'), 'git is required')
    def test_v2_committed_renewal(self):
        base = '3a72f1350b4a34ba2283457be4227c5c143ee79e'
        path = 'src/Parchwell.Verifier/AssemblyInfo.cs'
        module = 'tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py'
        qualification = 'tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json'
        new_hash = '36b6226f6db9e1d99baf0b3da865ea6a9f3c608e95267d1a49b47e4fad912ed2'
        identity = {**os.environ, 'GIT_AUTHOR_NAME': 'synthetic', 'GIT_AUTHOR_EMAIL': 'synthetic@example.invalid',
                    'GIT_COMMITTER_NAME': 'synthetic', 'GIT_COMMITTER_EMAIL': 'synthetic@example.invalid',
                    'GIT_AUTHOR_DATE': '2026-11-22T00:00:00Z', 'GIT_COMMITTER_DATE': '2026-11-22T00:00:00Z'}
        def git(*args, cwd=ROOT, **options):
            return subprocess.run(['git', *args], cwd=cwd, check=True, capture_output=True, **options)
        with tempfile.TemporaryDirectory() as directory:
            tree = Path(directory) / 'tree'
            git('worktree', 'add', '--detach', str(tree), base)
            try:
                shutil.copyfile(ROOT / module, tree / module)
                with (tree / path).open('ab') as source:
                    source.write(b'// synthetic renewal comment\n')
                git('add', path, cwd=tree)
                git('-c', 'commit.gpgsign=false', 'commit', '-q', '-m', 'synthetic renewal comment', cwd=tree, env=identity)
                head = git('rev-parse', 'HEAD', cwd=tree).stdout.decode().strip()
                self.assertEqual(new_hash, runtime.digest(git('show', f'{head}:{path}').stdout))
                file = Path(directory) / 'qualification.json'
                original = git('show', f'{base}:{qualification}').stdout
                file.write_bytes(original)
                def run(*args):
                    return subprocess.run(['python3', str(tree / module), *args, '--head', head, '--qualification', str(file)],
                                          cwd=tree, capture_output=True, text=True)
                renewed = run('renew', '--basis', self.D1)
                self.assertEqual((0, '{"renewed": ["src/Parchwell.Verifier/AssemblyInfo.cs"], "status": "renewed"}\n', ''),
                                 (renewed.returncode, renewed.stdout, renewed.stderr))
                expected = json.loads(original)
                old_hash = expected['shared_inputs'][path]
                expected['shared_inputs'][path] = new_hash
                self.assertEqual(7, len(expected['timing_inputs']))
                for context in expected['timing_inputs'].values():
                    self.assertEqual(old_hash, context['shared_inputs'][path])
                    self.assertNotIn(path, context['assessments']['shared_inputs'])
                    context['assessments']['shared_inputs'][path] = self.assessed(new_hash, self.D1)
                self.assertEqual(expected, json.loads(file.read_text()))
                checked = run('check')
                result = json.loads(checked.stdout)
                self.assertEqual((0, 'qualified', []), (checked.returncode, result['status'], result['reasons']))
                self.assertEqual(149, len(result['assessed_inputs']))
                self.assertIn(f'shared_inputs:{path}', result['assessed_inputs'])
            finally:
                git('worktree', 'remove', '--force', str(tree))
                git('worktree', 'prune')


class LocalShardTests(unittest.TestCase):
    def setUp(self):
        from contextlib import ExitStack
        self.stack = ExitStack()
        self.addCleanup(self.stack.close)
        self.tmp = self.stack.enter_context(tempfile.TemporaryDirectory())
        self.output = Path(self.tmp) / 'receipt'
        self.workflow = profile_module.workflow
        self.snapshot = {'src/example.cs': 'a' * 64}
        self.identity = self.stack.enter_context(mock.patch.object(self.workflow, 'identity', return_value=('commit', 'tree', True)))
        self.snapshots = self.stack.enter_context(mock.patch.object(self.workflow, 'snapshot', return_value=self.snapshot))
        self.stack.enter_context(mock.patch.object(self.workflow, 'git', side_effect=lambda root, *args:
            (root / args[-1].split(':', 1)[1]).read_bytes()))
        self.stack.enter_context(mock.patch.object(profile_module.shutil, 'which', return_value='/usr/bin/dotnet'))
        self.stack.enter_context(mock.patch.object(profile_module.subprocess, 'check_output', side_effect=lambda args, **kwargs:
            '10.0.402' if args[-1] == '--version' else 'dotnet-stryker 5.0.0 stryker'))
        def prepare(root, staging, target, retained):
            path = staging / (self.workflow.projection.VERIFIER + target)
            path.parent.mkdir(parents=True)
            path.write_bytes((root / (self.workflow.projection.VERIFIER + target)).read_bytes())
            retained.mkdir()
            return {}
        self.stack.enter_context(mock.patch.object(self.workflow.projection, 'prepare', side_effect=prepare))
        self.stack.enter_context(mock.patch.object(self.workflow.projection, 'finish', side_effect=lambda root, manifest, out: out.write_text('{}')))
        self.stack.enter_context(mock.patch.object(self.workflow.projection, 'validate', return_value='hash'))
        self.launch = self.stack.enter_context(mock.patch.object(self.workflow, 'launch', side_effect=self.completed_launch))
        self.isolate = self.stack.enter_context(mock.patch.object(self.workflow, 'run_isolated', return_value=([], None)))

    def completed_launch(self, argv, cwd, log, timeout):
        log.write_text('workers observed in this raw log')
        report = self.output / self.workflow.REPORT_PATH
        report.parent.mkdir(parents=True)
        report.write_text('{}')
        return 0, None

    def receipt(self):
        return json.loads((self.output / 'local-shard-receipt.json').read_text())

    def test_exact_primary_and_complement_use_production_selector_without_workflow_identity(self):
        candidates = [name for name, part in self.workflow.PARTS.items()
                      if part[0] == 'b10-final-sweep-handler']
        for shard in (candidates[0], candidates[-1]):
            with self.subTest(shard=shard):
                self.output = Path(self.tmp) / shard
                env = dict(os.environ)
                with mock.patch.dict(os.environ, {'GITHUB_SHA': 'branch', 'GITHUB_REF': 'refs/heads/branch'}):
                    self.assertEqual(0, profile_module.local_shard(shard, self.output, 100))
                    self.assertEqual('branch', os.environ['GITHUB_SHA'])
                    self.assertEqual('refs/heads/branch', os.environ['GITHUB_REF'])
                self.assertEqual(env, dict(os.environ))
                target = self.workflow.SHARDS[shard]
                source = (ROOT / (self.workflow.projection.VERIFIER + target)).read_bytes()
                self.assertEqual(self.workflow.shard_config(shard, source), json.loads((self.output / 'shard-config.json').read_text()))
                self.assertEqual('completed_unverified', self.receipt()['status'])
                self.assertIsNone(self.receipt()['observed_workers'])
        self.assertFalse((Path(self.launch.call_args.args[1])).exists())

    def test_invalid_preflight_does_not_launch_or_create_output(self):
        shard = next(iter(self.workflow.SHARDS))
        for bad_shard, output, timeout in [('unknown', self.output, 1), (shard, self.output, 0),
                                            (shard, self.output, float('nan')), (shard, self.output, 16201),
                                            (shard, ROOT / 'out', 1), (shard, Path(self.tmp), 1)]:
            with self.subTest(shard=bad_shard, output=output, timeout=timeout):
                with self.assertRaises(ValueError):
                    profile_module.local_shard(bad_shard, output, timeout)
        self.identity.return_value = ('commit', 'tree', False)
        with self.assertRaises(ValueError):
            profile_module.local_shard(shard, self.output, 1)
        self.launch.assert_not_called()
        self.assertFalse(self.output.exists())

    def test_timeout_and_source_drift_are_incomplete_and_retained(self):
        shard = next(iter(self.workflow.SHARDS))
        self.launch.side_effect = lambda argv, cwd, log, timeout: (None, 'time_cap_exceeded')
        self.assertEqual(1, profile_module.local_shard(shard, self.output, 100))
        self.assertEqual('time_cap_exceeded', self.receipt()['termination_reason'])
        self.assertEqual('incomplete', self.receipt()['status'])
        self.output = Path(self.tmp) / 'drift'
        self.launch.side_effect = self.completed_launch
        self.snapshots.side_effect = [self.snapshot, {'src/example.cs': 'b' * 64}]
        self.assertEqual(1, profile_module.local_shard(shard, self.output, 100))
        self.assertEqual('incomplete', self.receipt()['status'])

    def test_config_change_and_isolation_failure_remain_incomplete(self):
        shard = next(iter(self.workflow.SHARDS))
        def change(argv, cwd, log, timeout):
            result = self.completed_launch(argv, cwd, log, timeout)
            (self.output / 'shard-config.json').write_text('{}')
            return result
        self.launch.side_effect = change
        self.assertEqual(1, profile_module.local_shard(shard, self.output, 100))
        self.assertNotEqual(self.receipt()['config_sha256_before'], self.receipt()['config_sha256_after'])

    def test_boundary_error_is_preserved_before_launch(self):
        shard = next(iter(self.workflow.SHARDS))
        with mock.patch.object(self.workflow, 'shard_config', side_effect=ValueError('static field declaration crosses a span boundary')):
            self.assertEqual(1, profile_module.local_shard(shard, self.output, 100))
        self.assertIn('crosses', self.receipt()['error'])
        self.launch.assert_not_called()

    def test_isolation_uses_remaining_deadline_and_failure_is_retained(self):
        shard = next(iter(self.workflow.SHARDS))
        self.isolate.return_value = ([{'mutant_id': '1'}], 'shard time limit reached')
        with mock.patch.object(self.workflow, 'shard_static_regions', return_value=[(1, 2)]):
            before = profile_module.time.monotonic()
            self.assertEqual(1, profile_module.local_shard(shard, self.output, 100))
            after = profile_module.time.monotonic()
        deadline = self.isolate.call_args.args[-1]
        self.assertGreaterEqual(deadline, before + 100)
        self.assertLessEqual(deadline, after + 100)
        self.assertEqual('shard time limit reached', self.receipt()['isolation_error'])
        self.assertEqual([{'mutant_id': '1'}], self.receipt()['isolated'])

    def test_executing_helper_mismatch_rejected_before_launch(self):
        self.stack.enter_context(mock.patch.object(self.workflow, 'git', return_value=b'other revision'))
        with self.assertRaisesRegex(ValueError, 'differs from committed source'):
            profile_module.local_shard(next(iter(self.workflow.SHARDS)), self.output, 100)
        self.launch.assert_not_called()
        self.assertFalse(self.output.exists())

    def test_interruption_during_projection_preserves_receipt(self):
        with mock.patch.object(self.workflow.projection, 'prepare', side_effect=KeyboardInterrupt):
            self.assertEqual(1, profile_module.local_shard(next(iter(self.workflow.SHARDS)), self.output, 100))
        self.assertIn('KeyboardInterrupt', self.receipt()['error'])
        self.assertEqual('incomplete', self.receipt()['status'])
        self.launch.assert_not_called()

    def test_runtime_helper_is_not_a_qualification_input(self):
        # Use the real Git-backed discovery, not a synthetic input map.
        shared, _ = profile_module.input_maps('HEAD')
        self.assertNotIn('tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py', shared)

    def test_exit_two_with_unclassified_report_does_not_claim_verified_population(self):
        shard = next(iter(self.workflow.SHARDS))
        def launch(argv, cwd, log, timeout):
            self.completed_launch(argv, cwd, log, timeout)
            return 2, None
        self.launch.side_effect = launch
        self.assertEqual(0, profile_module.local_shard(shard, self.output, 100))
        self.assertEqual('completed_unverified', self.receipt()['status'])
        self.assertNotIn('mutant_count', self.receipt())

    def test_wrong_sdk_is_rejected_before_execution(self):
        with mock.patch.object(profile_module.subprocess, 'check_output', side_effect=['0.0.0', 'dotnet-stryker 5.0.0 stryker']):
            with self.assertRaisesRegex(ValueError, 'selected SDK'):
                profile_module.local_shard(next(iter(self.workflow.SHARDS)), self.output, 100)
        self.assertFalse(self.output.exists())
        self.launch.assert_not_called()


class SparseSamplePlanningTests(unittest.TestCase):
    TARGET = 'Example.cs'
    SOURCE = b'abcdefghij\nklmnopqrst\n'

    @staticmethod
    def row(identifier, start, end, *, status='Killed', static=False, replacement='x', line=1):
        row = {'id': str(identifier), 'mutatorName': 'Synthetic mutation',
               'replacement': replacement, 'status': status, 'static': static,
               'location': {'start': {'line': line, 'column': start},
                            'end': {'line': line, 'column': end}}}
        if status == 'Ignored':
            row['statusReason'] = 'Removed by mutate filter'
        return row

    def plan(self, rows, selected):
        return runtime.sample_plan(self.TARGET, rows, self.SOURCE, selected)

    def test_disjoint_selected_nodes_produce_one_sparse_main_selection(self):
        rows = [self.row(1, 1, 3), self.row(2, 5, 7), self.row(3, 9, 10)]
        plan = self.plan(rows, ['1', '3'])
        self.assertEqual({'1', '3'}, set(plan['main_ids']))
        self.assertEqual([], plan['isolated'])
        mutate = plan['main_config']['stryker-config']['mutate']
        self.assertEqual({self.TARGET + '{0..2}', self.TARGET + '{8..9}',
                          self.TARGET + '{22..' + str(runtime.workflow.SPAN_END) + '}'}, set(mutate))
        self.assertEqual(runtime.workflow.BASE_CONFIG['stryker-config']['thresholds'],
                         plan['main_config']['stryker-config']['thresholds'])

    def test_selection_must_be_nonempty_unique_known_and_testable(self):
        rows = [self.row(1, 1, 3), self.row(2, 5, 7, status='CompileError')]
        for selected in ([], ['1', '1'], ['missing'], ['2']):
            with self.subTest(selected=selected), self.assertRaises(ValueError):
                self.plan(rows, selected)

    def test_same_span_peers_must_all_be_selected(self):
        rows = [self.row(1, 1, 3, replacement='x'), self.row(2, 1, 3, replacement='y')]
        with self.assertRaises(ValueError):
            self.plan(rows, ['1'])
        self.assertEqual({'1', '2'}, set(self.plan(rows, ['1', '2'])['main_ids']))

    def test_unselected_descendant_is_excluded_without_losing_selected_parent(self):
        rows = [self.row(1, 1, 7), self.row(2, 3, 5), self.row(3, 9, 10)]
        mutate = self.plan(rows, ['1'])['main_config']['stryker-config']['mutate']
        self.assertIn(self.TARGET + '{0..6}', mutate)
        self.assertIn('!' + self.TARGET + '{2..4}', mutate)
        self.assertIn(self.TARGET + '{22..' + str(runtime.workflow.SPAN_END) + '}', mutate)
        self.assertTrue(all(runtime.workflow.node_span(row, [0, 11, 22], 22)[0] < 22 for row in rows))

    def test_nested_unselected_span_cannot_be_excluded_if_it_removes_selected_node(self):
        rows = [self.row(1, 1, 7), self.row(2, 3, 5), self.row(3, 4, 5)]
        with self.assertRaises(ValueError):
            self.plan(rows, ['1', '3'])

    def test_selected_static_mutants_are_isolated_without_changing_reference_rows(self):
        source = b'namespace N;\nclass C {\n    static readonly int A = 1;\n    static readonly int B = 2;\n}\n'
        rows = [self.row(1, 5, 6, line=3, static=True),
                self.row(2, 5, 6, line=4, static=True)]
        before = copy.deepcopy(rows)
        plan = runtime.sample_plan(self.TARGET, rows, source, ['1', '2'])
        self.assertEqual([], plan['main_ids'])
        self.assertIsNone(plan['main_config'])
        self.assertEqual(['1', '2'], [entry['mutant_id'] for entry in plan['isolated']])
        self.assertEqual(before, rows)

    def test_unselected_static_peers_do_not_block_selected_static_mutant(self):
        source = b'namespace N;\nclass C {\n    static readonly int A = 1;\n    static readonly int B = 2;\n}\n'
        for peer_line in (3, 4):
            with self.subTest(peer_line=peer_line):
                rows = [self.row(1, 5, 20, line=3, static=True),
                        self.row(2, 8, 10, line=peer_line, static=True, replacement='x'),
                        self.row(3, 8, 10, line=peer_line, static=True, replacement='y')]
                before = copy.deepcopy(rows)
                plan = runtime.sample_plan(self.TARGET, rows, source, ['1'])
                text = runtime.workflow.target_text(source)
                starts = runtime.workflow.line_starts(text)
                length = runtime.workflow.utf16_length(text)
                span = list(runtime.workflow.node_span(rows[0], starts, length))
                exclusions = ([list(runtime.workflow.node_span(rows[1], starts, length))]
                              if peer_line == 3 else [])
                self.assertEqual([], plan['main_ids'])
                self.assertIsNone(plan['main_config'])
                self.assertEqual([{'mutant_id': '1', 'span': span, 'exclusions': exclusions}],
                                 plan['isolated'])
                self.assertEqual(before, rows)

    def test_selected_static_mutant_with_unselected_same_span_peer_fails_closed(self):
        source = b'namespace N;\nclass C {\n    static readonly int A = 1;\n}\n'
        rows = [self.row(1, 5, 6, line=3, static=True, replacement='x'),
                self.row(2, 5, 6, line=3, static=True, replacement='y')]
        before = copy.deepcopy(rows)
        with self.assertRaisesRegex(ValueError, 'shares its span'):
            runtime.sample_plan(self.TARGET, rows, source, ['1'])
        self.assertEqual(before, rows)


class SparseSampleBindingTests(unittest.TestCase):
    def setUp(self):
        row = SparseSamplePlanningTests.row
        self.reference = [row(1, 1, 3), row(2, 5, 7), row(3, 9, 10, static=True)]
        self.actual = [row(101, 1, 3), row(102, 5, 7, status='Ignored'),
                       row(103, 9, 10, status='Ignored', static=True)]

    def test_native_ids_can_shift_when_full_identity_and_execution_match(self):
        self.assertEqual({'1': '101'}, runtime.sample_bindings(self.reference, self.actual, ['1']))

    def test_missing_extra_duplicate_or_changed_inventory_fails_closed(self):
        cases = {}
        cases['missing'] = self.actual[:-1]
        cases['extra'] = self.actual + [SparseSamplePlanningTests.row(104, 2, 4, status='Ignored')]
        cases['duplicate id'] = copy.deepcopy(self.actual)
        cases['duplicate id'][1]['id'] = cases['duplicate id'][0]['id']
        cases['duplicate identity'] = copy.deepcopy(self.actual)
        cases['duplicate identity'][1]['location'] = copy.deepcopy(cases['duplicate identity'][0]['location'])
        cases['changed replacement'] = copy.deepcopy(self.actual)
        cases['changed replacement'][1]['replacement'] = 'different'
        cases['changed static'] = copy.deepcopy(self.actual)
        cases['changed static'][2]['static'] = False
        for label, actual in cases.items():
            with self.subTest(case=label), self.assertRaises(ValueError):
                runtime.sample_bindings(self.reference, actual, ['1'])

    def test_reference_ids_and_identities_must_be_unique(self):
        duplicate_id = copy.deepcopy(self.reference)
        duplicate_id[1]['id'] = duplicate_id[0]['id']
        duplicate_identity = copy.deepcopy(self.reference)
        duplicate_identity[1]['location'] = copy.deepcopy(duplicate_identity[0]['location'])
        for reference in (duplicate_id, duplicate_identity):
            with self.subTest(reference=reference), self.assertRaises(ValueError):
                runtime.sample_bindings(reference, self.actual, ['1'])

    def test_selected_must_execute_and_no_other_mutant_may_execute(self):
        missing = copy.deepcopy(self.actual)
        missing[0]['status'] = 'Ignored'
        missing[0]['statusReason'] = 'Removed by mutate filter'
        extra = copy.deepcopy(self.actual)
        extra[1]['status'] = 'Killed'
        extra[1].pop('statusReason')
        for actual in (missing, extra):
            with self.subTest(actual=actual), self.assertRaises(ValueError):
                runtime.sample_bindings(self.reference, actual, ['1'])


class LocalSampleTests(unittest.TestCase):
    def setUp(self):
        LocalShardTests.setUp(self)
        self.identity.return_value = ('a' * 40, 'tree', True)
        self.target = next(iter(self.workflow.TARGETS.values()))[0]
        self.source = (ROOT / (self.workflow.projection.VERIFIER + self.target)).read_text()
        row = SparseSamplePlanningTests.row
        self.rows = [row(1, 1, 2), row(2, 3, 4)]
        self.inventory = Path(self.tmp) / 'reference.json'
        self.selection = Path(self.tmp) / 'selected.json'
        self.inventory.write_text(json.dumps(self.report(self.rows)))
        self.selection.write_text(json.dumps(['1']))
        self.stack.enter_context(mock.patch.object(self.workflow.projection, 'validate', return_value=True))
        self.launch = self.stack.enter_context(mock.patch.object(runtime, 'sample_launch', side_effect=self.sample_launch, create=True))

    def completed_launch(self, *args):
        raise AssertionError('unexpected launcher')

    def report(self, rows):
        return {'schemaVersion': '2', 'projectRoot': '/synthetic', 'files': {
            '/synthetic/' + self.target: {'language': 'cs', 'source': self.source, 'mutants': rows}}}

    def sample_launch(self, argv, cwd, log, timeout):
        log.write_text('synthetic raw log')
        rows = copy.deepcopy(self.rows)
        rows[0]['id'] = '101'
        rows[1].update(id='102', status='Ignored', statusReason='Removed by mutate filter')
        report = self.output / self.workflow.REPORT_PATH
        report.parent.mkdir(parents=True)
        report.write_text(json.dumps(self.report(rows)))
        return 0, None

    def invoke(self, timeout=100):
        return runtime.local_sample(self.target, self.inventory, 'b' * 40, self.selection, self.output, timeout)

    def receipt(self):
        return json.loads((self.output / 'local-sample-receipt.json').read_text())

    def test_completed_sample_retains_raw_inputs_and_receipts_without_claiming_timing(self):
        self.assertEqual(0, self.invoke())
        receipt = self.receipt()
        self.assertEqual(self.workflow.LOCAL_PURPOSE, receipt['purpose'])
        self.assertEqual('completed_unverified', receipt['status'])
        self.assertIsNone(receipt['observed_workers'])
        self.assertEqual(self.inventory.read_bytes(), (self.output / 'reference-report.json').read_bytes())
        self.assertEqual(self.selection.read_bytes(), (self.output / 'selection.json').read_bytes())
        self.assertEqual(runtime.digest(self.inventory.read_bytes()), receipt['reference_sha256'])
        self.assertEqual(runtime.digest(self.selection.read_bytes()), receipt['selection_sha256'])
        child, = receipt['children']
        self.assertEqual({'1': '101'}, child['native_bindings'])
        self.assertEqual(runtime.digest((self.output / 'command.log').read_bytes()), child['command_log_sha256'])
        self.assertEqual(runtime.digest((self.output / self.workflow.REPORT_PATH).read_bytes()), child['report_sha256'])
        self.assertEqual(runtime.digest((self.output / 'sample-config.json').read_bytes()), child['config_sha256_after'])
        self.assertEqual(child['config_sha256_before'], child['config_sha256_after'])
        self.launch.assert_called_once()

    def test_invalid_sample_timeouts_fail_before_launch_or_output(self):
        for timeout in (0, -1, float('nan'), float('inf'), 16201, True):
            with self.subTest(timeout=timeout), self.assertRaisesRegex(ValueError, 'timeout must be positive'):
                self.invoke(timeout=timeout)
            self.assertFalse(self.output.exists())
        self.launch.assert_not_called()

    def test_invalid_inventory_commits_fail_before_launch_or_output(self):
        for commit in ('', 'main', 'B' * 40, 'b' * 39, 'b' * 41, None):
            with self.subTest(commit=commit), self.assertRaisesRegex(ValueError, 'invalid inventory source commit'):
                runtime.local_sample(self.target, self.inventory, commit, self.selection, self.output, 100)
            self.assertFalse(self.output.exists())
        self.launch.assert_not_called()

    def test_inventory_commit_with_different_target_bytes_fails_before_launch(self):
        original_git = self.workflow.git.side_effect
        reference = f"{'b' * 40}:{self.workflow.projection.VERIFIER}{self.target}"
        def git(root, *args):
            source = original_git(root, *args)
            return source + b'\n// different source' if args == ('show', reference) else source
        self.workflow.git.side_effect = git
        with self.assertRaisesRegex(ValueError, 'inventory commit has different target source'):
            self.invoke()
        self.assertFalse(self.output.exists())
        self.launch.assert_not_called()

    def test_preflight_rejects_manifest_and_dirty_source_without_launch_or_output(self):
        for selection in ([], ['1', '1'], ['unknown'], {'ids': ['1']}):
            with self.subTest(selection=selection):
                self.selection.write_text(json.dumps(selection))
                with self.assertRaises(ValueError):
                    self.invoke()
                self.assertFalse(self.output.exists())
        self.selection.write_text('["1"]')
        self.identity.return_value = ('a' * 40, 'tree', False)
        with self.assertRaises(ValueError):
            self.invoke()
        self.launch.assert_not_called()
        self.assertFalse(self.output.exists())

    def test_source_mismatch_and_duplicate_inventory_fail_preflight(self):
        for change in ('source', 'duplicate'):
            report = self.report(copy.deepcopy(self.rows))
            item = report['files']['/synthetic/' + self.target]
            if change == 'source':
                item['source'] += 'changed'
            else:
                item['mutants'].append(copy.deepcopy(item['mutants'][0]))
            self.inventory.write_text(json.dumps(report))
            with self.subTest(change=change), self.assertRaises(ValueError):
                self.invoke()
            self.assertFalse(self.output.exists())
        self.launch.assert_not_called()

    def test_failure_missing_report_and_source_drift_are_incomplete(self):
        for case in ('failed', 'missing', 'drift'):
            with self.subTest(case=case):
                self.output = Path(self.tmp) / case
                if case == 'failed':
                    self.launch.side_effect = lambda *args: (1, None)
                elif case == 'missing':
                    self.launch.side_effect = lambda *args: (0, None)
                else:
                    self.launch.side_effect = self.sample_launch
                    self.snapshots.side_effect = [self.snapshot, {'src/example.cs': 'b' * 64}]
                self.assertEqual(1, self.invoke())
                self.assertEqual('incomplete', self.receipt()['status'])

    def test_existing_output_is_never_overwritten(self):
        self.output.mkdir()
        sentinel = self.output / 'sentinel'
        sentinel.write_text('keep')
        with self.assertRaises(ValueError):
            self.invoke()
        self.assertEqual('keep', sentinel.read_text())
        self.launch.assert_not_called()

    def test_wrong_sdk_and_helper_mismatch_fail_before_launch(self):
        with mock.patch.object(runtime.subprocess, 'check_output', side_effect=['0.0.0', 'dotnet-stryker 5.0.0 stryker']):
            with self.assertRaises(ValueError):
                self.invoke()
        with mock.patch.object(self.workflow, 'git', return_value=b'different helper'):
            with self.assertRaises(ValueError):
                self.invoke()
        self.assertFalse(self.output.exists())
        self.launch.assert_not_called()

    def test_staging_time_consumes_deadline_and_prevents_launch(self):
        now = [100.0]
        prepare = self.workflow.projection.prepare.side_effect
        def delayed(*args):
            result = prepare(*args)
            now[0] += 101
            return result
        self.workflow.projection.prepare.side_effect = delayed
        with mock.patch.object(runtime.time, 'monotonic', side_effect=lambda: now[0]):
            self.assertEqual(1, self.invoke(timeout=100))
        self.assertEqual('incomplete', self.receipt()['status'])
        self.launch.assert_not_called()

    def test_static_samples_are_sequential_and_bind_each_fresh_report(self):
        self.source = ('namespace N;\nclass C {\n    static readonly int A = 1;\n'
                       '    static readonly int B = 2;\n}\n')
        row = SparseSamplePlanningTests.row
        self.rows = [row(1, 5, 6, line=3, static=True), row(2, 5, 6, line=4, static=True)]
        self.inventory.write_text(json.dumps(self.report(self.rows)))
        self.selection.write_text('["1", "2"]')
        original_git = self.workflow.git.side_effect
        def git(root, *args):
            if args[-1].split(':', 1)[-1] == self.workflow.projection.VERIFIER + self.target:
                return self.source.encode()
            return original_git(root, *args)
        self.workflow.git.side_effect = git
        def prepare(root, staging, target, retained):
            path = staging / (self.workflow.projection.VERIFIER + target)
            path.parent.mkdir(parents=True)
            path.write_text(self.source)
            retained.mkdir()
            return {}
        self.workflow.projection.prepare.side_effect = prepare
        seen = []
        def launch(argv, cwd, log, deadline):
            directory = Path(argv[argv.index('--output') + 1]).parent
            identifier = directory.name
            seen.append(identifier)
            log.write_text('synthetic isolated log')
            report = directory / self.workflow.REPORT_PATH
            report.parent.mkdir(parents=True)
            rows = copy.deepcopy(self.rows)
            for index, entry in enumerate(rows):
                entry['id'] = str(101 + index)
                if str(index + 1) != identifier:
                    entry.update(status='Ignored', statusReason='Removed by mutate filter')
            report.write_text(json.dumps(self.report(rows)))
            return 0, None
        self.launch.side_effect = launch
        self.assertEqual(0, self.invoke())
        self.assertEqual(['1', '2'], seen)
        children = self.receipt()['children']
        self.assertEqual([{'1': '101'}, {'2': '102'}], [child['native_bindings'] for child in children])
        self.assertEqual(['isolated', 'isolated'], [child['kind'] for child in children])
        for child in children:
            directory = self.workflow.isolated_directory(self.output, child)
            self.assertEqual(runtime.digest((directory / self.workflow.REPORT_PATH).read_bytes()), child['report_sha256'])
            self.assertEqual(runtime.digest((directory / 'command.log').read_bytes()), child['command_log_sha256'])
            self.assertEqual(runtime.digest((directory / 'config.json').read_bytes()), child['config_sha256_after'])
            self.assertEqual(child['config_sha256_before'], child['config_sha256_after'])
        self.assertEqual(2, self.launch.call_count)
        self.assertEqual(self.launch.call_args_list[0].args[3], self.launch.call_args_list[1].args[3])
        for failure in ('failed', 'missing', 'invalid'):
            with self.subTest(failure=failure):
                self.output = Path(self.tmp) / ('static-' + failure)
                self.launch.reset_mock()
                def fail_first(argv, cwd, log, deadline):
                    if failure == 'invalid':
                        launch(argv, cwd, log, deadline)
                        directory = Path(argv[argv.index('--output') + 1]).parent
                        report = directory / self.workflow.REPORT_PATH
                        report.write_text('{}')
                    return (1 if failure == 'failed' else 0), None
                self.launch.side_effect = fail_first
                self.assertEqual(1, self.invoke())
                self.launch.assert_called_once()
                self.assertEqual('incomplete', self.receipt()['status'])


class SampleLauncherDeadlineTests(unittest.TestCase):
    def setUp(self):
        from contextlib import ExitStack
        self.stack = ExitStack()
        self.addCleanup(self.stack.close)
        self.tmp = Path(self.stack.enter_context(tempfile.TemporaryDirectory()))
        self.log = self.tmp / 'command.log'
        self.now = 100.0
        self.stack.enter_context(mock.patch.object(runtime.time, 'monotonic', side_effect=lambda: self.now))
        self.diagnostic = self.stack.enter_context(mock.patch.object(runtime.workflow, 'diagnostic'))
        self.popen = self.stack.enter_context(mock.patch.object(runtime.subprocess, 'Popen'))
        self.kill = self.stack.enter_context(mock.patch.object(runtime.os, 'killpg'))
        self.signal = self.stack.enter_context(mock.patch.object(runtime.signal, 'signal'))
        self.stack.enter_context(mock.patch.object(runtime.signal, 'getsignal', return_value='previous-handler'))
        self.process = mock.Mock(pid=1234)
        self.popen.return_value = self.process

    def launch(self):
        return runtime.sample_launch(['synthetic'], self.tmp, self.log, 110.0)

    def assert_cleanup(self, pid):
        self.assertEqual('previous-handler', self.signal.call_args_list[-1].args[1])
        self.assertEqual('after', self.diagnostic.call_args_list[-1].args[3])
        self.assertEqual(pid, self.diagnostic.call_args_list[-1].args[4])

    def test_resource_capture_can_exhaust_deadline_before_popen(self):
        def capture(*args):
            if args[3] == 'before':
                self.now = 111.0
        self.diagnostic.side_effect = capture
        self.assertEqual((None, 'time_cap_exceeded'), self.launch())
        self.popen.assert_not_called()
        self.assertFalse(self.log.exists())
        self.assert_cleanup(None)

    def test_popen_startup_time_reduces_wait_budget(self):
        def start(*args, **kwargs):
            self.now = 107.0
            return self.process
        self.popen.side_effect = start
        self.process.wait.return_value = 0
        self.assertEqual((0, None), self.launch())
        self.process.wait.assert_called_once_with(timeout=3.0)
        self.kill.assert_not_called()
        self.assert_cleanup(1234)

    def test_popen_startup_exhausting_deadline_kills_without_timed_wait(self):
        def start(*args, **kwargs):
            self.now = 111.0
            return self.process
        self.popen.side_effect = start
        self.process.wait.return_value = -9
        self.assertEqual((-9, 'time_cap_exceeded'), self.launch())
        self.kill.assert_called_once_with(1234, runtime.signal.SIGKILL)
        self.process.wait.assert_called_once_with()
        self.assert_cleanup(1234)

    def test_timeout_and_interruption_kill_group_and_reap(self):
        for reason, error in (('time_cap_exceeded', subprocess.TimeoutExpired('synthetic', 10)),
                              ('interrupted', KeyboardInterrupt())):
            with self.subTest(reason=reason):
                self.log.unlink(missing_ok=True)
                self.process.reset_mock()
                self.kill.reset_mock()
                self.process.wait.side_effect = [error, -9]
                self.assertEqual((-9, reason), self.launch())
                self.kill.assert_called_once_with(1234, runtime.signal.SIGKILL)
                self.assertEqual([mock.call(timeout=10.0), mock.call()], self.process.wait.call_args_list)
                self.assert_cleanup(1234)


class RebalanceTimingProvenanceTests(unittest.TestCase):
    def setUp(self):
        self.head = runtime.REBALANCED_SOURCE
        shared, targets = runtime.input_maps(self.head)
        self.context = {'source_commit': self.head, 'shared_inputs': shared,
                        'target_sources': targets,
                        'assessments': {'shared_inputs': {}, 'target_sources': {}}}
        self.source = runtime.blob(self.head, 'src/Parchwell.Verifier/' + runtime.CODEC_TARGET)
        shard = 'b10-final-projection-codec-p1-a'
        self.record, self.item, self.timing = self.receipt(shard)
        self.evidence = self.record['evidence'][0]

    def receipt(self, shard):
        artifact, archive, selection, numbers, seconds = runtime.REBALANCED_RECEIPTS[shard]
        locator = ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/'
                   '44686622095/artifacts/' + artifact)
        evidence = {'source_commit': runtime.REVIEW_CORRECTION, 'sha256': archive,
                    'kind': 'local_probe', 'locator': locator}
        record = {'source_sha256': runtime.digest(self.source), 'evidence': [evidence]}
        item = dict(zip(runtime.NUMERIC_FIELDS, numbers), selection_sha256=selection,
                    timing_inputs_sha256='b' * 64, basis=f'observed_workers={numbers[2]}')
        timing = {'kind': 'measured', 'source_commit': runtime.REVIEW_CORRECTION,
                  'artifact_sha256': archive, 'timing_inputs_sha256': 'b' * 64,
                  'observed_workers': numbers[2], 'observed_seconds': seconds, 'method': 'synthetic'}
        return record, item, timing

    def link(self, shard, context=None, timing=None, record=None, head=None):
        default_record, item, default_timing = self.receipt(
            shard if shard in runtime.REBALANCED_RECEIPTS else 'b10-final-projection-codec-p1-a')
        return runtime.timing_context_linked(context or self.context, shard, runtime.CODEC_TARGET,
                    record or default_record, item, timing or default_timing, {}, head or self.head)

    def test_only_unchanged_selections_transfer_and_fresh_sources_link(self):
        for shard in runtime.REBALANCED_RECEIPTS:
            with self.subTest(shard=shard):
                self.assertTrue(self.link(shard))
                self.assertTrue(self.link(shard, head=runtime.CI_REPAIR_SOURCE))
        for suffix in (*(f'p{i}' for i in range(4, 16)), 'crossing'):
            shard = 'b10-final-projection-codec-' + suffix
            self.assertFalse(self.link(shard))
            fresh = dict(self.timing, source_commit=self.head)
            record = dict(self.record, evidence=[dict(self.evidence, source_commit=self.head)])
            self.assertTrue(self.link(shard, timing=fresh, record=record))
        self.assertFalse(self.link('b10-final-projection-codec-unknown'))

    def test_transfer_rejects_map_source_evidence_and_head_drift(self):
        shard = 'b10-final-projection-codec-p1-a'
        context = copy.deepcopy(self.context)
        context['shared_inputs'][runtime.RUNNER] = '0' * 64
        self.assertFalse(self.link(shard, context=context))
        context = copy.deepcopy(self.context)
        context['target_sources'][runtime.CODEC_TARGET] = '0' * 64
        self.assertFalse(self.link(shard, context=context))
        self.assertFalse(self.link(shard, timing=dict(self.timing, source_commit='0' * 40)))
        self.assertFalse(self.link(shard, timing=dict(self.timing, artifact_sha256='0' * 64)))
        self.assertFalse(self.link(shard, record=dict(self.record, source_sha256='0' * 64)))
        self.assertFalse(self.link(shard, head=runtime.REVIEW_CORRECTION))
        old_context = dict(self.context, source_commit=runtime.REVIEW_CORRECTION)
        for suffix in ('p8', 'p9', 'p10', 'crossing', 'p1-a'):
            self.assertFalse(self.link('b10-final-projection-codec-' + suffix, context=old_context))
        self.assertFalse(self.link(shard, timing=dict(self.timing, observed_seconds=1)))
        for key in (*runtime.NUMERIC_FIELDS, 'selection_sha256'):
            changed = dict(self.item, **{key: '0' * 64 if key == 'selection_sha256' else self.item[key] + 1})
            self.assertFalse(runtime.timing_context_linked(self.context, shard, runtime.CODEC_TARGET,
                            self.record, changed, self.timing, {}, self.head))

    def test_tree_and_boundary_drift_are_rejected(self):
        original = runtime.proof_tree
        pinned = original(self.head)
        shard = 'b10-final-projection-codec-p1-a'
        for field in range(len(pinned)):
            changed = list(copy.deepcopy(pinned))
            if field == 0:
                cuts = list(changed[0]['PRIMARY_CUTS']['b10-final-projection-codec'])
                cuts[7] += 1
                changed[0]['PRIMARY_CUTS']['b10-final-projection-codec'] = tuple(cuts)
            elif field == 1:
                changed[1] += b'drift'
            elif field == 2:
                changed[2][0]['drift'] = '0' * 64
            elif field == 3:
                changed[3]['drift'] = True
            else:
                changed[4]['drift'] = 'other'
            with self.subTest(field=field), mock.patch.object(runtime, 'proof_tree', side_effect=lambda ref:
                    tuple(changed) if ref == self.head else original(ref)):
                self.assertFalse(self.link(shard))

    def test_fresh_pinned_timing_requires_measurement_on_the_new_tail_selection(self):
        for suffix in ('p11', 'p12'):
            shard = 'b10-final-projection-codec-' + suffix
            self.assertIn(shard, runtime.SIX_TAIL_BINDINGS)
            item = dict(self.item, selection_sha256=runtime.digest(runtime.canonical(
                runtime.workflow.shard_config(shard, self.source))))
            config = {'schema_version': 2, 'timing_evidence': {shard: self.timing},
                      'timing_inputs': {item['timing_inputs_sha256']: self.context}}
            self.assertFalse(runtime.fresh_pinned_timing(config, runtime.CODEC_TARGET,
                             self.record, shard, item, self.source, self.head))
            fresh = dict(self.timing, source_commit=self.head, artifact_sha256='0' * 64)
            evidence = dict(self.evidence, source_commit=self.head, sha256='0' * 64)
            record = dict(self.record, evidence=[evidence])
            config['timing_evidence'][shard] = fresh
            self.assertTrue(runtime.fresh_pinned_timing(config, runtime.CODEC_TARGET,
                            record, shard, item, self.source, self.head))


class PairedTailTimingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.head = runtime.PAIRED_TAIL_SOURCE
        cls.tree = runtime.proof_tree(cls.head)
        shared, sources = cls.tree[2]
        measured = {'shared_inputs': shared, 'target_sources': sources}
        cls.binding = runtime.digest(runtime.canonical(measured))
        prior = runtime.strict_json(runtime.blob(runtime.PAIRED_TAIL_PRIOR, runtime.HISTORICAL_QUALIFICATION_PATH))
        cls.context = prior['timing_inputs'][cls.binding]

    def receipt(self, shard):
        owner = self.tree[4][shard]
        source = runtime.blob(self.head, 'src/Parchwell.Verifier/' + owner)
        artifact = 'a' * 64
        record = {
            'source_sha256': runtime.digest(source),
            'evidence': [{'source_commit': self.head, 'kind': 'hosted_complete', 'sha256': artifact}],
        }
        item = {'timing_inputs_sha256': self.binding, 'basis': 'synthetic fixture observed_workers=2',
                'estimated_seconds': 2700}
        timing = {'kind': 'measured', 'source_commit': self.head, 'artifact_sha256': artifact,
                  'timing_inputs_sha256': self.binding, 'observed_workers': 2, 'observed_seconds': 2650}
        return owner, source, record, item, timing

    def test_exact_transition_changes_only_the_reviewed_physical_owners(self):
        changed = runtime.paired_tail_changes(self.head)
        self.assertEqual(43, len(changed))
        self.assertTrue({'verifier-result-codec-crossing', 'b10-final-judge-crossing',
                         'b10-final-projection-codec-crossing'}.issubset(changed))
        judge_selectors = {name for name in self.tree[4] if name.startswith('b10-final-judge-')}
        self.assertTrue(judge_selectors.issubset(changed))
        self.assertNotIn('verifier-result-codec-p7', changed)
        self.assertNotIn('b10-final-projection-codec-p1-a', changed)
        self.assertNotIn('a01-p1', changed)
        self.assertEqual(frozenset(), runtime.paired_tail_changes(runtime.PAIRED_TAIL_PRIOR))

    def test_changed_crossing_requires_the_exact_fresh_measurement(self):
        shard = 'verifier-result-codec-crossing'
        owner, _, record, item, timing = self.receipt(shard)
        self.assertNotEqual(self.head, self.context['source_commit'])
        self.assertTrue(runtime.timing_context_linked(
            self.context, shard, owner, record, item, timing, {}, self.head))
        for delta in ({'source_commit': self.context['source_commit']}, {'artifact_sha256': 'f' * 64},
                      {'observed_seconds': 2651}, {'observed_workers': 3}, {'kind': 'historical'}):
            with self.subTest(delta=delta):
                self.assertFalse(runtime.timing_context_linked(
                    self.context, shard, owner, record, item, {**timing, **delta}, {}, self.head))

    def test_fresh_measurement_replaces_only_the_affected_pinned_selections(self):
        for shard in ('b10-final-judge-p11', 'b10-final-projection-codec-p9'):
            with self.subTest(shard=shard):
                owner, source, record, item, timing = self.receipt(shard)
                config = {'schema_version': 2, 'timing_evidence': {shard: timing},
                          'timing_inputs': {self.binding: self.context}}
                self.assertTrue(runtime.fresh_pinned_timing(config, owner, record, shard, item, source, self.head))
                config['timing_evidence'][shard] = {**timing, 'source_commit': self.context['source_commit']}
                self.assertFalse(runtime.fresh_pinned_timing(config, owner, record, shard, item, source, self.head))


class PairedRecoveryTimingTests(PairedTailTimingTests):
    # The qualification merge is a descendant with the reviewed recovery layout.
    # Later checkouts can have unrelated assessed inputs or a different layout.
    recovery_descendant = '717c5a75e3f0a56ad5378a0e7551caa832ed63d8'

    @classmethod
    def setUpClass(cls):
        cls.head = runtime.PAIRED_RECOVERY_SOURCE
        cls.tree = runtime.proof_tree(cls.head)
        shared, sources = cls.tree[2]
        cls.binding = runtime.digest(runtime.canonical({'shared_inputs': shared, 'target_sources': sources}))
        prior = runtime.strict_json(runtime.blob(runtime.PAIRED_TAIL_PRIOR, runtime.HISTORICAL_QUALIFICATION_PATH))
        cls.context = prior['timing_inputs'][cls.binding]

    # The parent tests describe the first transition; the recovery has its own assertions.
    def test_exact_transition_changes_only_the_reviewed_physical_owners(self):
        changed = runtime.paired_recovery_changes(self.head)
        self.assertEqual(runtime.PAIRED_RECOVERY_CHANGED, changed)
        self.assertEqual(18, len(changed))
        for prefix, count in (('b10-final-judge-', 5), ('b10-final-projection-codec-', 6),
                              ('verifier-result-codec-', 7)):
            self.assertEqual(count, sum(name.startswith(prefix) for name in changed))
            self.assertIn(prefix + 'crossing', changed)
        self.assertEqual(25, len(runtime.PAIRED_RECOVERY_RETAINED))
        self.assertFalse(changed & runtime.PAIRED_RECOVERY_RETAINED.keys())

    def test_changed_crossing_requires_the_exact_fresh_measurement(self):
        for shard in ('b10-final-judge-crossing', 'b10-final-projection-codec-crossing',
                      'verifier-result-codec-crossing'):
            owner, _, record, item, timing = self.receipt(shard)
            for seconds in (2649, 2650, 2651):
                for estimate in (2649, 2650, 2651):
                    with self.subTest(shard=shard, seconds=seconds, estimate=estimate):
                        self.assertEqual(seconds <= min(2650, estimate), runtime.timing_context_linked(
                            self.context, shard, owner, record, {**item, 'estimated_seconds': estimate},
                            {**timing, 'observed_seconds': seconds}, {}, self.head))
            for delta in ({'source_commit': runtime.PAIRED_TAIL_SOURCE}, {'artifact_sha256': 'f' * 64},
                          {'observed_seconds': 2651}, {'observed_workers': 3}, {'kind': 'historical'},
                          {'timing_inputs_sha256': '0' * 64}):
                with self.subTest(shard=shard, delta=delta):
                    self.assertFalse(runtime.timing_context_linked(
                        self.context, shard, owner, record, item, {**timing, **delta}, {}, self.head))

    def test_fresh_measurement_replaces_only_the_affected_pinned_selections(self):
        for shard in ('b10-final-projection-codec-p9', 'b10-final-projection-codec-crossing'):
            owner, source, record, item, timing = self.receipt(shard)
            config = {'schema_version': 2, 'timing_evidence': {shard: timing},
                      'timing_inputs': {self.binding: self.context}}
            self.assertTrue(runtime.fresh_pinned_timing(config, owner, record, shard, item, source, self.head))
            config['timing_evidence'][shard] = {**timing, 'source_commit': runtime.PAIRED_TAIL_SOURCE}
            self.assertFalse(runtime.fresh_pinned_timing(config, owner, record, shard, item, source, self.head))

    def test_retained_receipt_requires_original_source_artifact_and_observation(self):
        shard = 'b10-final-judge-p5'
        owner, _, record, item, timing = self.receipt(shard)
        artifact, observed, mutants, estimate = runtime.PAIRED_RECOVERY_RETAINED[shard]
        item.update(main_mutants=mutants, estimated_seconds=estimate)
        record['evidence'][0].update(source_commit=runtime.PAIRED_TAIL_SOURCE, sha256=artifact)
        timing.update(source_commit=runtime.PAIRED_TAIL_SOURCE, artifact_sha256=artifact,
                      observed_seconds=observed)
        self.assertTrue(runtime.timing_context_linked(self.context, shard, owner, record, item, timing, {}, self.head))
        for delta in ({'source_commit': self.head}, {'artifact_sha256': 'f' * 64},
                      {'observed_seconds': observed + 0.01}, {'observed_seconds': math.ceil(observed)}, {'observed_workers': 1}):
            self.assertFalse(runtime.timing_context_linked(
                self.context, shard, owner, record, item, {**timing, **delta}, {}, self.head))
        wrong_context = copy.deepcopy(self.context)
        wrong_context['shared_inputs'][runtime.CONFIG_PATH] = '0' * 64
        self.assertFalse(runtime.timing_context_linked(wrong_context, shard, owner, record, item, timing, {}, self.head))

    def test_later_renewable_change_preserves_recovery_receipts_and_requires_assessment(self):
        head = self.recovery_descendant
        original = runtime.proof_tree
        tree = original(head)
        shared, targets = tree[2]
        path = runtime.CI_REPAIR_TEST
        edited = {**shared, path: '9' * 64}
        changed = (*tree[:2], (edited, targets), *tree[3:])
        self.assertNotIn(path, runtime.NON_RENEWABLE)
        self.assertNotEqual(shared[path], edited[path])
        context = copy.deepcopy(self.context)
        assessment = {'current_sha256': edited[path], 'basis': ['Synthetic assessment fixture']}
        context['assessments']['shared_inputs'][path] = assessment
        shard = 'b10-final-judge-crossing'
        owner, _, record, item, timing = self.receipt(shard)

        runtime.paired_recovery_changes.cache_clear()
        with mock.patch.object(runtime, 'proof_tree', side_effect=lambda ref:
                changed if ref == head else original(ref)):
            self.assertEqual(runtime.PAIRED_RECOVERY_CHANGED, runtime.paired_recovery_changes(head))
            self.assertTrue(runtime.timing_context_linked(context, shard, owner, record, item, timing, {}, head))
            self.assertEqual(set(), runtime.provenance_errors(context, edited, targets, owner, shard, head))
            self.assertIn('unaccounted_inputs', runtime.provenance_errors(
                self.context, edited, targets, owner, shard, head))
        runtime.paired_recovery_changes.cache_clear()

        prior = runtime.strict_json(runtime.blob(runtime.PAIRED_TAIL_PRIOR, runtime.HISTORICAL_QUALIFICATION_PATH))
        owner, shard = 'Judge/PhaseJudge.cs', 'phase-p1'
        binding = prior['targets'][owner]['shards'][shard]['timing_inputs_sha256']
        original_context = prior['timing_inputs'][binding]
        updated = copy.deepcopy(original_context)
        updated['assessments']['shared_inputs'][path] = assessment
        self.assertTrue(runtime.retains_measurement_context(updated, original_context, edited))
        self.assertEqual(set(), runtime.provenance_errors(
            updated, edited, targets, owner, shard, runtime.PAIRED_TAIL_PRIOR))
        updated['source_commit'] = self.head
        self.assertFalse(runtime.retains_measurement_context(updated, original_context, edited))

    def test_cut_and_execution_drift_reject_the_transition(self):
        head = self.recovery_descendant
        self.assertNotEqual(head, self.head)
        self.assertEqual(runtime.PAIRED_RECOVERY_CHANGED, runtime.paired_recovery_changes(head))
        original = runtime.proof_tree
        tree = list(original(head))
        for index in (0, 1, 2, 3, 4):
            altered = copy.deepcopy(tree)
            if index == 0:
                cuts = list(altered[0]['PRIMARY_CUTS']['b10-final-judge'])
                cuts[0] += 1
                altered[0]['PRIMARY_CUTS']['b10-final-judge'] = tuple(cuts)
            elif index == 1:
                altered[1] = b'changed selector'
            elif index == 2:
                altered[2][0][runtime.CONFIG_PATH] = '0' * 64
            elif index == 3:
                altered[3]['changed'] = True
            else:
                altered[4].pop('b10-final-judge-p1')
            runtime.paired_recovery_changes.cache_clear()
            with self.subTest(index=index), mock.patch.object(runtime, 'proof_tree', side_effect=lambda ref:
                    tuple(altered) if ref == head else original(ref)):
                self.assertFalse(runtime.paired_recovery_changes(head))
        runtime.paired_recovery_changes.cache_clear()

    def test_each_role_matrix_must_match_both_sources(self):
        head = self.recovery_descendant
        original = runtime.blob
        for path in runtime.CAMPAIGN_LIMITS:
            for ref in (runtime.PAIRED_TAIL_SOURCE, self.head, head):
                runtime.paired_recovery_changes.cache_clear()

                def changed_blob(commit, requested):
                    data = original(commit, requested)
                    if commit == ref and requested == path:
                        matrix = runtime.workflow_matrix(data)
                        first, second = (f'          - shard: {shard}\n            target: {target}\n'.encode()
                                         for shard, target in list(matrix.items())[:2])
                        return data.replace(first + second, second + first, 1)
                    return data

                with self.subTest(path=path, ref=ref), mock.patch.object(runtime, 'blob', side_effect=changed_blob):
                    self.assertFalse(runtime.paired_recovery_changes(head))
        runtime.paired_recovery_changes.cache_clear()


class ObservedTailTimingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = runtime.OBSERVED_TAIL_SOURCE
        cls.prior = runtime.OBSERVED_TAIL_PRIOR
        cls.measured = runtime.proof_tree(cls.source)
        cls.shared, cls.targets = cls.measured[2]
        cls.binding = runtime.digest(runtime.canonical({
            'shared_inputs': cls.shared, 'target_sources': cls.targets}))
        ledger = runtime.strict_json(runtime.blob(cls.prior, runtime.HISTORICAL_QUALIFICATION_PATH))
        cls.context = ledger['timing_inputs'][cls.binding]

    def setUp(self):
        runtime.observed_tail_changes.cache_clear()
        self.addCleanup(runtime.observed_tail_changes.cache_clear)

    def test_exact_source_has_the_reviewed_changes_and_requires_ancestry(self):
        self.assertEqual(runtime.OBSERVED_TAIL_CHANGED, runtime.observed_tail_changes(self.source))
        self.assertEqual(31, len(runtime.observed_tail_changes(self.source)))
        self.assertNotIn('phase-candidate-codec-p10', runtime.observed_tail_changes(self.source))
        self.assertFalse(runtime.observed_tail_changes(self.prior))

    def test_unapproved_source_drift_cannot_reuse_the_transition(self):
        original = runtime.proof_tree
        for field in range(5):
            changed = list(copy.deepcopy(self.measured))
            if field == 0:
                cuts = changed[0]['PRIMARY_CUTS']['phase']
                changed[0]['PRIMARY_CUTS']['phase'] = (cuts[0] + 1, *cuts[1:])
            elif field == 1:
                changed[1] += b'unapproved execution'
            elif field == 2:
                changed[2][0][runtime.CONFIG_PATH] = 'f' * 64
            elif field == 3:
                changed[3]['unapproved'] = True
            else:
                changed[4].pop('phase-candidate-codec-p1')
            runtime.observed_tail_changes.cache_clear()
            with self.subTest(field=field), mock.patch.object(runtime, 'proof_tree', side_effect=lambda ref:
                    tuple(changed) if ref == self.source else original(ref)):
                self.assertFalse(runtime.observed_tail_changes(self.source))

    def test_ordered_membership_of_each_role_is_part_of_the_proof(self):
        original = runtime.blob
        for path in runtime.CAMPAIGN_LIMITS:
            data = original(self.source, path)
            first, second = (
                f'          - shard: {shard}\n            target: {target}\n'.encode()
                for shard, target in list(runtime.workflow_matrix(data).items())[:2])
            self.assertIn(first + second, data)
            swapped = data.replace(first + second, second + first, 1)
            runtime.observed_tail_changes.cache_clear()
            with self.subTest(role=path), mock.patch.object(runtime, 'blob', side_effect=lambda ref, name:
                    swapped if (ref, name) == (self.source, path) else original(ref, name)):
                self.assertFalse(runtime.observed_tail_changes(self.source))

    def test_renewable_descendant_requires_exact_current_assessment(self):
        path = runtime.CI_REPAIR_TEST
        source = self.source
        head = 'f' * 40
        shared = {**self.shared, path: '9' * 64}
        current = (*self.measured[:2], (shared, self.targets), *self.measured[3:])
        original_git, original_tree, original_blob = runtime.git, runtime.proof_tree, runtime.blob
        def git(*args):
            return b'' if args == ('merge-base', '--is-ancestor', source, head) else original_git(*args)

        with (mock.patch.object(runtime, 'git', side_effect=git),
              mock.patch.object(runtime, 'proof_tree', side_effect=lambda ref:
                                current if ref == head else original_tree(ref)),
              mock.patch.object(runtime, 'blob', side_effect=lambda ref, name:
                                original_blob(source if ref == head else ref, name))):
            self.assertEqual(runtime.OBSERVED_TAIL_CHANGED, runtime.observed_tail_changes(head))
            context = copy.deepcopy(self.context)
            self.assertIn('unaccounted_inputs', runtime.provenance_errors(
                context, shared, self.targets, 'Judge/PhaseCandidateCodec.cs',
                'phase-candidate-codec-p10', self.prior, self.context))
            context['assessments']['shared_inputs'][path] = {
                'current_sha256': shared[path], 'basis': ['Synthetic assessment fixture']}
            self.assertTrue(runtime.retains_measurement_context(context, self.context, shared))
            self.assertEqual(set(), runtime.provenance_errors(
                context, shared, self.targets, 'Judge/PhaseCandidateCodec.cs',
                'phase-candidate-codec-p10', self.prior, self.context))

    def test_new_receipt_requires_exact_source_workers_and_bounded_elapsed(self):
        shard = 'phase-candidate-codec-p3'
        owner = self.measured[4][shard]
        archive = 'a' * 64
        record = {'source_sha256': self.targets[owner], 'evidence': [
            {'kind': 'hosted_complete', 'source_commit': self.source, 'sha256': archive}]}
        item = {'timing_inputs_sha256': self.binding, 'estimated_seconds': 2700,
                'basis': 'Synthetic receipt with observed_workers=2'}
        timing = {'kind': 'measured', 'source_commit': self.source,
                  'artifact_sha256': archive, 'observed_workers': 2,
                  'timing_inputs_sha256': self.binding, 'observed_seconds': 2650}
        def linked(evidence, estimate=2700):
            return runtime.measured_cut_timing_linked(
                self.context, shard, owner, record, {**item, 'estimated_seconds': estimate},
                evidence, {}, self.source)

        self.assertNotEqual(self.context['source_commit'], self.source)
        for seconds, expected in ((0, False), (2649, True), (2650, True), (2651, False)):
            with self.subTest(seconds=seconds):
                self.assertEqual(expected, linked({**timing, 'observed_seconds': seconds}))
        for estimate, expected in ((2699, True), (2700, True), (2701, False)):
            with self.subTest(estimate=estimate):
                self.assertEqual(expected, linked(timing, estimate))
        for delta in ({'source_commit': self.prior}, {'kind': 'historical'},
                      {'artifact_sha256': 'b' * 64}, {'observed_workers': 0}, {'observed_workers': 3}):
            with self.subTest(delta=delta):
                self.assertFalse(linked({**timing, **delta}))
        self.assertFalse(linked(timing, 2649))


class ObservedTailLedgerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        # This snapshot contains the hosted receipt set and the merged frontier assessments.
        cls.head = '64075129a9885956b4ae097f923c2990fd4d3907'
        cls.ledger = runtime.strict_json(runtime.blob(cls.head, runtime.HISTORICAL_QUALIFICATION_PATH))
        cls.prior = runtime.strict_json(runtime.blob(runtime.OBSERVED_TAIL_PRIOR, runtime.HISTORICAL_QUALIFICATION_PATH))
        cls.shared, cls.targets = runtime.input_maps(cls.head)
        cls.source = {path: runtime.blob(cls.head, 'src/Parchwell.Verifier/' + path) for path in cls.targets}
        cls.matrix = runtime.workflow_layout(cls.head, paired=True)
        cls.fixture_cuts = runtime.proof_tree(cls.head)[0]['PRIMARY_CUTS']
        cls.changed = runtime.observed_tail_changes(cls.head)
        cls.target = 'Judge/PhaseCandidateCodec.cs'
        cls.retained = 'phase-candidate-codec-p10'
        cls.fresh = 'phase-candidate-codec-p3'
        cls.peer = 'phase-candidate-codec-p4'

    def validate(self, config):
        # Generate selectors from the same committed layout as the historical ledger.
        with mock.patch.object(runtime.workflow, 'PRIMARY_CUTS', self.fixture_cuts):
            return runtime.validate(config, self.shared, self.targets, self.source, self.matrix, self.head)

    def test_complete_hosted_ledger_matches_the_exact_receipts_and_retained_items(self):
        self.assertEqual(324, len(self.ledger['timing_evidence']))
        self.assertEqual(runtime.OBSERVED_TAIL_CHANGED, self.changed)
        self.assertEqual(set(), runtime.measured_cut_ledger_errors(
            self.ledger, self.prior, self.shared, self.changed, runtime.OBSERVED_TAIL_RECEIPTS_SHA256))

    def test_foreign_receipt_swap_inside_one_target_is_rejected(self):
        config = copy.deepcopy(self.ledger)
        first, second = (config['timing_evidence'][shard] for shard in (self.fresh, self.peer))
        self.assertNotEqual(first['artifact_sha256'], second['artifact_sha256'])
        first['artifact_sha256'], second['artifact_sha256'] = second['artifact_sha256'], first['artifact_sha256']
        self.assertEqual({'missing_measurement'}, self.validate(config))

    def test_receipt_for_a_different_selector_or_archive_is_rejected(self):
        config = copy.deepcopy(self.ledger)
        first, second = (config['targets'][self.target]['shards'][shard] for shard in (self.fresh, self.peer))
        self.assertNotEqual(first['selection_sha256'], second['selection_sha256'])
        first['selection_sha256'] = second['selection_sha256']
        self.assertEqual({'missing_measurement'}, self.validate(config))

        config = copy.deepcopy(self.ledger)
        record = config['targets'][self.target]
        timing = config['timing_evidence'][self.fresh]
        entry = next(e for e in record['evidence'] if e['sha256'] == timing['artifact_sha256'])
        entry['locator'] = entry['locator'].rsplit('/', 1)[0] + '/1'
        self.assertEqual({'missing_measurement'}, self.validate(config))

    def test_retained_item_timing_and_measured_inputs_keep_their_original_values(self):
        for field in ('item', 'timing', 'measured_input'):
            with self.subTest(field=field):
                config = copy.deepcopy(self.ledger)
                item = config['targets'][self.target]['shards'][self.retained]
                timing = config['timing_evidence'][self.retained]
                if field == 'item':
                    item['estimated_seconds'] -= 1
                elif field == 'timing':
                    timing['artifact_sha256'] = 'f' * 64
                else:
                    context = config['timing_inputs'][item['timing_inputs_sha256']]
                    context['shared_inputs'][runtime.RUNNER] = 'f' * 64
                self.assertEqual({'missing_measurement'}, self.validate(config))

    def test_missing_and_extra_physical_rows_are_rejected(self):
        config = copy.deepcopy(self.ledger)
        del config['targets'][self.target]['shards'][self.retained]
        self.assertIn('layout_mismatch', self.validate(config))

        config = copy.deepcopy(self.ledger)
        record = config['targets'][self.target]['shards']
        record['phase-candidate-codec-p99'] = copy.deepcopy(record[self.retained])
        self.assertIn('layout_mismatch', self.validate(config))

    def test_complete_ledger_enforces_timing_boundaries_after_receipt_authentication(self):
        # The fixture samples strict timing bounds with a matching synthetic receipt pin.
        # Reuse calculations on the same immutable inputs across full validations.
        cases = (
            (2649, 2699, 2699, set()),
            (2650, 2700, 2700, set()),
            (2651, 2700, 2700, {'missing_measurement'}),
            (2650, 2701, 2701, {'missing_measurement', 'shard_over_budget'}),
            (2650, 2649, 2649, {'missing_measurement'}),
            (2649, 2699, 2700, {'invalid_estimate'}),
        )
        original_owners = runtime.committed_physical_owners
        owners = {}

        def physical_owners(declarations, matrix):
            key = id(declarations), id(matrix)
            if key not in owners:
                # Keep both immutable inputs alive for the lifetime of the cache.
                owners[key] = declarations, matrix, original_owners(declarations, matrix)
            return owners[key][2]

        with (mock.patch.object(runtime, 'git', cache(runtime.git)),
              mock.patch.object(runtime, 'committed_physical_owners', physical_owners),
              mock.patch.object(runtime.workflow, 'shard_config', cache(runtime.workflow.shard_config))):
            for observed, estimate, calculated, expected in cases:
                with self.subTest(observed=observed, estimate=estimate, calculated=calculated):
                    config = copy.deepcopy(self.ledger)
                    item = config['targets'][self.target]['shards'][self.fresh]
                    item['estimated_seconds'] = estimate
                    item['main_work_seconds'] = (calculated - item['setup_seconds'] - item['isolated_seconds']) * item['parallelism']
                    config['timing_evidence'][self.fresh]['observed_seconds'] = observed
                    receipts = {}
                    for shard in self.changed:
                        target = self.matrix[shard]
                        record = config['targets'][target]
                        timing = config['timing_evidence'][shard]
                        entry = next(e for e in record['evidence'] if e['sha256'] == timing['artifact_sha256'])
                        receipts[shard] = {'target': target, 'item': record['shards'][shard],
                                           'timing': timing, 'evidence': entry}
                    fingerprint = runtime.digest(runtime.canonical(receipts))
                    with mock.patch.object(runtime, 'OBSERVED_TAIL_RECEIPTS_SHA256', fingerprint):
                        self.assertEqual(set(), runtime.measured_cut_ledger_errors(
                            config, self.prior, self.shared, self.changed, fingerprint))
                        self.assertEqual(expected, self.validate(config))



class TimingMarginTests(unittest.TestCase):
    # This snapshot contains all 38 verified receipts and both prior proof layers.
    LEDGER_REVISION = '728f337a43da4698ecd943a180dc134368c85e8e'
    TARGET = 'VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs'
    RETAINED_TARGET = 'VerifyRun/Input/RestartManifestReader.cs'
    RETAINED = 'b10-restart-manifest-p1'
    FRESH = 'single-action-configuration-p41'
    PEER = 'single-action-configuration-p40'

    @classmethod
    def setUpClass(cls):
        cls.source = runtime.TIMING_MARGIN_SOURCE
        cls.prior_head = runtime.TIMING_MARGIN_PRIOR
        cls.tree = runtime.proof_tree(cls.source)
        cls.shared, cls.targets = cls.tree[2]
        cls.prior = runtime.strict_json(runtime.blob(cls.prior_head, runtime.HISTORICAL_QUALIFICATION_PATH))
        cls.ledger = runtime.strict_json(runtime.blob(cls.LEDGER_REVISION, runtime.HISTORICAL_QUALIFICATION_PATH))

    def setUp(self):
        runtime.timing_margin_changes.cache_clear()
        self.addCleanup(runtime.timing_margin_changes.cache_clear)

    def test_reviewed_selector_set_and_ancestry(self):
        changed = runtime.timing_margin_changes(self.source)
        self.assertEqual(runtime.TIMING_MARGIN_CHANGED, changed)
        self.assertEqual(38, len(changed))
        self.assertNotIn(self.RETAINED, changed)
        self.assertFalse(runtime.timing_margin_changes(self.prior_head))
        runtime.timing_margin_changes.cache_clear()
        original = runtime.git

        def missing_ancestry(*args):
            if args == ('merge-base', '--is-ancestor', self.source, self.source):
                raise subprocess.CalledProcessError(1, args)
            return original(*args)

        with mock.patch.object(runtime, 'git', side_effect=missing_ancestry):
            self.assertFalse(runtime.timing_margin_changes(self.source))

    def test_layout_and_input_drift_reject(self):
        data = runtime.blob(self.source, runtime.WORKFLOW)
        first, second = (
            f'          - shard: {shard}\n            target: {target}\n'.encode()
            for shard, target in list(runtime.workflow_matrix(data).items())[:2])
        self.assertIn(first + second, data)
        swapped = data.replace(first + second, second + first, 1)
        original_blob = runtime.blob
        with mock.patch.object(runtime, 'blob', side_effect=lambda ref, name:
                               swapped if (ref, name) == (self.source, runtime.WORKFLOW)
                               else original_blob(ref, name)):
            self.assertFalse(runtime.timing_margin_changes(self.source))

        runtime.timing_margin_changes.cache_clear()
        edited = copy.deepcopy(self.tree)
        edited[2][0][runtime.CONFIG_PATH] = '0' * 64
        original_tree = runtime.proof_tree
        with mock.patch.object(runtime, 'proof_tree', side_effect=lambda ref:
                               edited if ref == self.source else original_tree(ref)):
            self.assertFalse(runtime.timing_margin_changes(self.source))

    def test_bound_receipts_and_retained_items(self):
        changed = runtime.timing_margin_changes(self.source)

        def ledger_errors(ledger):
            return runtime.measured_cut_ledger_errors(
                ledger, self.prior, self.shared, changed, runtime.TIMING_MARGIN_RECEIPTS_SHA256)

        self.assertEqual(set(), ledger_errors(self.ledger))
        for alteration in ('swapped', 'older_source', 'retained_item', 'retained_context', 'missing', 'extra'):
            with self.subTest(alteration=alteration):
                ledger = copy.deepcopy(self.ledger)
                receipts = ledger['timing_evidence']
                retained = ledger['targets'][self.RETAINED_TARGET]['shards'][self.RETAINED]
                if alteration == 'swapped':
                    first, second = receipts[self.FRESH], receipts[self.PEER]
                    self.assertNotEqual(first['artifact_sha256'], second['artifact_sha256'])
                    first['artifact_sha256'], second['artifact_sha256'] = (
                        second['artifact_sha256'], first['artifact_sha256'])
                elif alteration == 'older_source':
                    receipts[self.FRESH]['source_commit'] = runtime.OBSERVED_TAIL_SOURCE
                elif alteration == 'retained_item':
                    retained['setup_seconds'] += 1
                elif alteration == 'retained_context':
                    ledger['timing_inputs'][retained['timing_inputs_sha256']]['source_commit'] = '0' * 40
                elif alteration == 'missing':
                    receipts.pop(self.RETAINED)
                else:
                    receipts['not-a-shard'] = receipts[self.RETAINED]
                self.assertEqual({'missing_measurement'}, ledger_errors(ledger))

    def test_timing_observation_and_estimate_limits(self):
        record = self.ledger['targets'][self.TARGET]
        item = record['shards'][self.FRESH]
        timing = self.ledger['timing_evidence'][self.FRESH]
        context = self.ledger['timing_inputs'][item['timing_inputs_sha256']]
        cases = (
            (2650, 2700, 0, False),
            (2650, 2700, 1, True),
            (2650, 2700, 2, True),
            (2650, 2700, 3, False),
            (2651, 2700, 2, False),
            (2650, 2701, 2, False),
            (2650, 2649, 2, False),
        )
        for observed, estimate, workers, expected in cases:
            with self.subTest(observed=observed, estimate=estimate, workers=workers):
                actual = runtime.measured_cut_timing_linked(
                    context, self.FRESH, self.TARGET, record,
                    {**item, 'basis': f'Synthetic boundary fixture observed_workers={workers}',
                     'estimated_seconds': estimate},
                    {**timing, 'observed_workers': workers, 'observed_seconds': observed}, {}, self.source)
                self.assertEqual(expected, actual)

    def test_retained_assessment_tracks_actual_head(self):
        row = self.ledger['targets'][self.RETAINED_TARGET]['shards'][self.RETAINED]
        proof = self.prior['timing_inputs'][row['timing_inputs_sha256']]
        context = copy.deepcopy(self.ledger['timing_inputs'][row['timing_inputs_sha256']])
        frontier = 'tests/Parchwell.Verifier.Tests/Consumer/ConsumerSnapshotFrontierTests.cs'
        self.assertEqual(proof['assessments']['shared_inputs'][frontier],
                         context['assessments']['shared_inputs'][frontier])
        self.assertEqual(self.shared[frontier], context['assessments']['shared_inputs'][frontier]['current_sha256'])
        path = runtime.CI_REPAIR_TEST
        shared = {**self.shared, path: '9' * 64}
        assessments = context['assessments']['shared_inputs']

        def reasons():
            return runtime.provenance_errors(
                context, shared, self.targets, self.RETAINED_TARGET, self.RETAINED,
                runtime.PAIRED_TAIL_PRIOR, proof)

        assessments[path] = {'current_sha256': shared[path], 'basis': ['Synthetic assessed current input']}
        self.assertEqual(set(), reasons())
        assessments[path]['current_sha256'] = '0' * 64
        self.assertEqual({'unaccounted_inputs'}, reasons())
        assessments.pop(path)
        self.assertEqual({'unaccounted_inputs'}, reasons())

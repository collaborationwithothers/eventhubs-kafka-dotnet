import contextlib
import copy
from datetime import datetime, timedelta, timezone
import hashlib
import io
import json
import os
import re
import shutil
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import c5_campaign as campaign
import c5_workflow
from test_c5_workflow import ATTEMPT, COMMIT, RUN_ID, AggregateFixture, legacy_layout, write_json


class CampaignTests(unittest.TestCase):
    def setUp(self):
        self.commit = 'a' * 40
        self.repo = 'owner/repo'
        self.layout = {'coordinator': {'a': 'A.cs'}, 'companion': {'b': 'B.cs'}}
        self.runs = {role: {'id': index, 'run_attempt': 1, 'path': campaign.PATHS[role],
                            'event': 'workflow_dispatch', 'head_branch': 'main',
                            'head_sha': self.commit, 'repository': {'full_name': self.repo},
                            'head_repository': {'full_name': self.repo},
                            'created_at': '2026-11-19T00:00:00Z',
                            'status': 'in_progress' if role == 'coordinator' else 'completed',
                            'conclusion': None if role == 'coordinator' else 'success'}
                     for index, role in enumerate(campaign.PATHS, 1)}
        self.shards = patch.object(campaign.c5_workflow, 'SHARDS', {'a': 'A.cs', 'b': 'B.cs'})
        self.shards.start()
        self.addCleanup(self.shards.stop)

    def manifest(self):
        return campaign.build_manifest(self.repo, self.commit, self.runs, self.layout)

    def validate(self, manifest):
        return campaign.validate_manifest(manifest, self.commit, self.layout['coordinator'], self.layout['companion'])

    def job(self, role, name, job_id):
        run = self.runs[role]
        return {'id': job_id, 'name': name, 'run_id': run['id'], 'run_attempt': 1,
                'head_sha': self.commit, 'status': 'in_progress' if name == 'aggregate' else 'completed',
                'conclusion': None if name == 'aggregate' else 'success'}

    def document(self, role):
        shard, target = next(iter(self.layout[role].items()))
        rows = [self.job(role, f'mutation ({shard}, {target})', 10)]
        if role == 'coordinator':
            rows.append(self.job(role, 'aggregate', 11))
        return {'jobs': rows, 'total_count': len(rows)}

    def test_valid_manifest_binds_each_shard(self):
        origins = self.validate(self.manifest())
        self.assertEqual({'target': 'A.cs', 'run_id': '1', 'run_attempt': '1'}, origins['a'])
        self.assertEqual('2', origins['b']['run_id'])

    def test_run_identity_rejections(self):
        cases = [('id', 8), ('run_attempt', 2), ('path', 'foreign.yml'), ('event', 'push'),
                 ('head_branch', 'other'), ('head_sha', 'b' * 40),
                 ('repository', {'full_name': 'other/repo'}),
                 ('head_repository', {'full_name': 'other/repo'})]
        for field, value in cases:
            with self.subTest(field=field), self.assertRaises(ValueError):
                run = copy.deepcopy(self.runs['companion'])
                run[field] = value
                campaign.validate_run(run, 'companion', self.repo, self.commit, 2, 1)

    def test_manifest_rejects_forged_missing_mixed_or_foreign_origins(self):
        changes = [lambda m: m['shards'].pop('a'),
                   lambda m: m['shards']['b'].update(run_id='1'),
                   lambda m: m['shards']['a'].update(run_attempt='2'),
                   lambda m: m['shards'].update(foreign={'target': 'X', 'run_id': '1', 'run_attempt': '1'}),
                   lambda m: m['runs']['companion'].update(id=1),
                   lambda m: m['runs']['companion'].update(path='foreign'),
                   lambda m: m.update(commit='b' * 40),
                   lambda m: m.update(extra=True),
                   lambda m: m.update(incomplete_reasons='failure'),
                   lambda m: m.update(incomplete_reasons=['']),
                   lambda m: m.update(incomplete_reasons=[1])]
        for change in changes:
            with self.subTest(change=change), self.assertRaises(ValueError):
                manifest = self.manifest()
                change(manifest)
                self.validate(manifest)

    def test_layout_rejects_overlap_missing_and_changed_target(self):
        for companion in ({'a': 'A.cs', 'b': 'B.cs'}, {}, {'b': 'other'}):
            with self.subTest(companion=companion), self.assertRaises(ValueError):
                campaign.validate_layout({'coordinator': {'a': 'A.cs'}, 'companion': companion})

    def test_job_membership_and_results(self):
        for role in campaign.PATHS:
            document = self.document(role)
            run = self.runs[role]
            campaign.validate_jobs(document, run['id'], 1, self.commit, self.layout[role], role == 'coordinator')
            mutations = [lambda d: d['jobs'].pop(0),
                         lambda d: d['jobs'].append(copy.deepcopy(d['jobs'][0])),
                         lambda d: d['jobs'][0].update(name='mutation (foreign, X.cs)'),
                         lambda d: d['jobs'][0].update(conclusion='failure'),
                         lambda d: d['jobs'][0].update(run_attempt=2),
                         lambda d: d['jobs'][0].update(head_sha='b' * 40),
                         lambda d: d.update(total_count=999)]
            for mutation in mutations:
                with self.subTest(role=role, mutation=mutation), self.assertRaises(ValueError):
                    bad = copy.deepcopy(document)
                    mutation(bad)
                    campaign.validate_jobs(bad, run['id'], 1, self.commit, self.layout[role], role == 'coordinator')

    def test_job_pagination(self):
        pages = [{'total_count': 101, 'jobs': list(range(100))}, {'total_count': 101, 'jobs': [100]}]
        with patch.object(campaign, 'api', side_effect=pages) as api:
            self.assertEqual(101, len(campaign.jobs(self.repo, 1, 1)['jobs']))
            self.assertIn('page=2', api.call_args.args[1])
        with patch.object(campaign, 'api', return_value={'total_count': 101, 'jobs': [1]}):
            with self.assertRaisesRegex(ValueError, 'missing job pages'):
                campaign.jobs(self.repo, 1, 1)

    def test_prepare_retains_late_and_incomplete_evidence(self):
        def api(_repo, suffix):
            return copy.deepcopy(self.runs['coordinator' if '/1/' in suffix else 'companion'])
        def jobs(_repo, run_id, _attempt):
            return self.document('coordinator' if run_id == 1 else 'companion')
        with tempfile.TemporaryDirectory() as directory, patch.object(campaign, 'matrices', return_value=self.layout), \
                patch.object(campaign, 'api', side_effect=api), patch.object(campaign, 'jobs', side_effect=jobs), \
                patch.object(campaign.subprocess, 'check_output', side_effect=[self.commit + '\n', b'workflow', b'workflow'] * 5), \
                patch.object(Path, 'read_bytes', return_value=b'workflow'), \
                patch.object(campaign.time, 'time', return_value=2_000_000_000), \
                patch.object(campaign.time, 'monotonic', return_value=0):
            output = Path(directory) / 'late.json'
            campaign.prepare(self.repo, self.commit, 1, 1, 2, 1, output)
            self.assertEqual([], json.loads(output.read_text())['incomplete_reasons'])
            self.runs['companion']['conclusion'] = 'failure'
            output = Path(directory) / 'failed.json'
            campaign.prepare(self.repo, self.commit, 1, 1, 2, 1, output)
            failed = json.loads(output.read_text())
            self.assertIn('companion run was not successful', failed['incomplete_reasons'])
            self.assertEqual('2', failed['shards']['b']['run_id'])
            with patch.object(campaign, 'jobs', side_effect=ValueError('missing job pages')):
                output = Path(directory) / 'missing.json'
                campaign.prepare(self.repo, self.commit, 1, 1, 2, 1, output)
                missing = json.loads(output.read_text())
                self.assertIn('companion jobs: missing job pages', missing['incomplete_reasons'])
            self.runs['companion'].update(status='in_progress', conclusion=None)
            with patch.object(campaign.time, 'monotonic', side_effect=[0, campaign.POLL_SECONDS]):
                output = Path(directory) / 'timeout.json'
                campaign.prepare(self.repo, self.commit, 1, 1, 2, 1, output)
                self.assertIn('companion polling timed out', json.loads(output.read_text())['incomplete_reasons'])
            with patch.object(campaign.time, 'sleep'), patch.object(campaign, 'api', side_effect=[
                    self.runs['coordinator'], self.runs['companion'], self.runs['companion'],
                    OSError('API unavailable')]):
                output = Path(directory) / 'poll-failed.json'
                campaign.prepare(self.repo, self.commit, 1, 1, 2, 1, output)
                interrupted = json.loads(output.read_text())
                self.assertIn('companion status: API unavailable', interrupted['incomplete_reasons'])
                self.assertEqual({'target': 'B.cs', 'run_id': '2', 'run_attempt': '1'}, self.validate(interrupted)['b'])

    def test_prepare_rejects_unverifiable_metadata(self):
        self.runs['companion']['head_branch'] = 'other'
        def api(_repo, suffix):
            return copy.deepcopy(self.runs['coordinator' if '/1/' in suffix else 'companion'])
        with tempfile.TemporaryDirectory() as directory, patch.object(campaign, 'matrices', return_value=self.layout), \
                patch.object(campaign, 'api', side_effect=api), \
                patch.object(campaign.subprocess, 'check_output', side_effect=[self.commit + '\n', b'workflow', b'workflow']), \
                patch.object(Path, 'read_bytes', return_value=b'workflow'), \
                self.assertRaisesRegex(ValueError, 'workflow, event, branch or commit mismatch'):
            campaign.prepare(self.repo, self.commit, 1, 1, 2, 1, Path(directory) / 'manifest.json')

    def terminal_inputs(self):
        manifest_bytes = json.dumps(self.manifest()).encode()
        summary = {'commit': self.commit, 'run_id': '1', 'run_attempt': '1',
                   'campaign_manifest_sha256': hashlib.sha256(manifest_bytes).hexdigest(),
                   'measurement_complete': True, 'break_passed': True}
        runs = copy.deepcopy(self.runs)
        documents = {role: self.document(role) for role in campaign.PATHS}
        for role in campaign.PATHS:
            runs[role].update(status='completed', conclusion='success', updated_at='2026-11-19T00:40:00Z')
            for job in documents[role]['jobs']:
                job.update(status='completed', conclusion='success', created_at='2026-11-19T00:01:00Z',
                           started_at='2026-11-19T00:02:00Z', completed_at='2026-11-19T00:30:00Z')
        return manifest_bytes, summary, runs, documents

    def terminal_report(self, manifest_bytes, summary, runs, documents):
        return campaign.terminal_report(self.repo, self.commit, manifest_bytes,
                                        json.dumps(summary).encode(), self.layout, runs, documents)

    def test_terminal_performance_accepts_complete_campaign_and_budget_boundary(self):
        manifest, summary, runs, documents = self.terminal_inputs()
        report = self.terminal_report(manifest, summary, runs, documents)
        self.assertEqual('met', report['status'])
        self.assertEqual(2400, report['elapsed_seconds'])
        runs['coordinator']['updated_at'] = '2026-11-19T00:50:00Z'
        self.assertEqual('met', self.terminal_report(manifest, summary, runs, documents)['status'])
        runs['coordinator']['updated_at'] = '2026-11-19T00:50:01Z'
        self.assertEqual('met', self.terminal_report(manifest, summary, runs, documents)['status'])
        runs['coordinator']['updated_at'] = '2026-11-19T01:10:00Z'
        report = self.terminal_report(manifest, summary, runs, documents)
        self.assertEqual(('met', 4200, 4200), (report['status'], report['elapsed_seconds'], report['budget_seconds']))
        runs['coordinator']['updated_at'] = '2026-11-19T01:10:01Z'
        report = self.terminal_report(manifest, summary, runs, documents)
        self.assertEqual(('missed', ['campaign_exceeded_budget'], 4200),
                         (report['status'], report['reasons'], report['budget_seconds']))

    def test_terminal_rejects_unbound_summary_failed_runs_and_bad_timing(self):
        mutations = [lambda s, r, d: s.update(campaign_manifest_sha256='0' * 64),
                     lambda s, r, d: s.update(measurement_complete=False),
                     lambda s, r, d: s.update(break_passed=False),
                     lambda s, r, d: s.update(run_attempt='2'),
                     lambda s, r, d: r['companion'].update(conclusion='failure'),
                     lambda s, r, d: r['coordinator'].update(status='in_progress'),
                     lambda s, r, d: r['companion'].update(run_attempt=2),
                     lambda s, r, d: r['companion'].update(created_at='2026-11-19T00:01:00Z'),
                     lambda s, r, d: d['companion']['jobs'][0].update(completed_at='2026-11-19T00:41:00Z'),
                     lambda s, r, d: d['coordinator']['jobs'][0].update(conclusion='failure'),
                     lambda s, r, d: d['companion']['jobs'].append(copy.deepcopy(d['companion']['jobs'][0]))]
        for mutate in mutations:
            with self.subTest(mutate=mutate):
                manifest, summary, runs, documents = self.terminal_inputs()
                mutate(summary, runs, documents)
                self.assertEqual('incomplete', self.terminal_report(manifest, summary, runs, documents)['status'])

    def test_terminal_rejects_recorded_incomplete_evidence(self):
        _, summary, runs, documents = self.terminal_inputs()
        manifest = self.manifest()
        manifest['incomplete_reasons'] = ['companion run was not successful']
        manifest_bytes = json.dumps(manifest).encode()
        summary['campaign_manifest_sha256'] = hashlib.sha256(manifest_bytes).hexdigest()
        self.assertEqual('incomplete', self.terminal_report(manifest_bytes, summary, runs, documents)['status'])

    def test_terminal_rejects_malformed_or_foreign_manifest(self):
        manifest, summary, runs, documents = self.terminal_inputs()
        self.assertEqual('incomplete', campaign.terminal_report(
            'foreign/repo', self.commit, manifest, json.dumps(summary).encode(),
            self.layout, runs, documents)['status'])
        self.assertEqual('incomplete', campaign.terminal_report(
            self.repo, self.commit, b'{"schema":1,"schema":1}', json.dumps(summary).encode(),
            self.layout, runs, documents)['status'])


    def schema_two_terminal(self):
        """A recovered coordinator at attempt 2 whose shard a was carried over from attempt 1."""
        t0 = datetime(2026, 11, 19, tzinfo=timezone.utc)

        def at(seconds):
            return (t0 + timedelta(seconds=seconds)).strftime('%Y-%m-%dT%H:%M:%SZ')

        def run(role, attempt, created, updated):
            return {**self.runs[role], 'run_attempt': attempt, 'created_at': at(created), 'updated_at': at(updated),
                    'status': 'completed', 'conclusion': 'success'}

        def record(role, attempt, name, job_id, created, started, completed, conclusion='success'):
            return {'id': job_id, 'name': name, 'run_id': self.runs[role]['id'], 'run_attempt': attempt,
                    'head_sha': self.commit, 'status': 'completed', 'conclusion': conclusion,
                    'created_at': at(created), 'started_at': at(started), 'completed_at': at(completed)}

        shard_a = 'mutation (a, A.cs)'
        manifest = campaign.build_manifest(
            self.repo, self.commit,
            {'coordinator': run('coordinator', 2, 4200, 4200), 'companion': run('companion', 1, 0, 2900)},
            self.layout, [], {'a': 1, 'b': 1}, {'coordinator': at(0), 'companion': at(0)})
        manifest_bytes = json.dumps(manifest).encode()
        summary = {'commit': self.commit, 'run_id': '1', 'run_attempt': '2',
                   'campaign_manifest_sha256': hashlib.sha256(manifest_bytes).hexdigest(),
                   'measurement_complete': True, 'break_passed': True}
        runs = {'coordinator': run('coordinator', 2, 4200, 4200), 'companion': run('companion', 1, 0, 2900)}
        documents = {
            'coordinator': {'total_count': 2, 'jobs': [record('coordinator', 2, shard_a, 10, 4200, 10, 2000),
                                                       record('coordinator', 2, 'aggregate', 11, 4200, 4200, 4200)]},
            'companion': {'total_count': 1, 'jobs': [record('companion', 1, 'mutation (b, B.cs)', 12, 5, 10, 2900)]}}
        firsts = {'coordinator': run('coordinator', 1, 0, 100)}
        earlier = {'coordinator': {1: {'total_count': 1, 'jobs': [record('coordinator', 1, shard_a, 20, 5, 10, 2000)]}}}
        return manifest, manifest_bytes, summary, runs, documents, firsts, earlier, at

    def test_terminal_schema_two_measures_from_first_creation(self):
        def report(manifest_bytes, summary, runs, documents, firsts, earlier):
            return campaign.terminal_report(self.repo, self.commit, manifest_bytes, json.dumps(summary).encode(),
                                            self.layout, runs, documents, firsts, earlier)

        _, manifest_bytes, summary, runs, documents, firsts, earlier, at = self.schema_two_terminal()
        result = report(manifest_bytes, summary, runs, documents, firsts, earlier)
        self.assertEqual(('met', 4200, 4200), (result['status'], result['elapsed_seconds'], result['budget_seconds']))
        self.assertEqual(4200, result['runs']['coordinator']['elapsed_seconds'])
        # The carried-over job counts from its origin record, queued from attempt 1.
        self.assertEqual(5, result['runs']['coordinator']['queue_seconds'])
        with self.subTest('one second over the limit'):
            _, manifest_bytes, summary, runs, documents, firsts, earlier, at = self.schema_two_terminal()
            runs['coordinator']['updated_at'] = at(4201)
            result = report(manifest_bytes, summary, runs, documents, firsts, earlier)
            self.assertEqual(('missed', 4201, ['campaign_exceeded_budget']),
                             (result['status'], result['elapsed_seconds'], result['reasons']))
        with self.subTest('origin record with a different completion'):
            _, manifest_bytes, summary, runs, documents, firsts, earlier, at = self.schema_two_terminal()
            earlier['coordinator'][1]['jobs'][0]['completed_at'] = at(2001)
            result = report(manifest_bytes, summary, runs, documents, firsts, earlier)
            self.assertEqual(('incomplete', ['invalid job timing']), (result['status'], result['reasons']))
        with self.subTest('missing origin job list'):
            _, manifest_bytes, summary, runs, documents, firsts, earlier, at = self.schema_two_terminal()
            result = report(manifest_bytes, summary, runs, documents, firsts, {})
            self.assertEqual(('incomplete', ['invalid job timing']), (result['status'], result['reasons']))
        with self.subTest('first creation one second off attempt one'):
            _, manifest_bytes, summary, runs, documents, firsts, earlier, at = self.schema_two_terminal()
            firsts['coordinator']['created_at'] = at(1)
            result = report(manifest_bytes, summary, runs, documents, firsts, earlier)
            self.assertEqual(('incomplete', ['run creation time mismatch']), (result['status'], result['reasons']))
        with self.subTest('missing attempt one document'):
            _, manifest_bytes, summary, runs, documents, firsts, earlier, at = self.schema_two_terminal()
            result = report(manifest_bytes, summary, runs, documents, {}, earlier)
            self.assertEqual(('incomplete', ['run creation time mismatch']), (result['status'], result['reasons']))
        with self.subTest('job created before the first creation'):
            _, manifest_bytes, summary, runs, documents, firsts, earlier, at = self.schema_two_terminal()
            earlier['coordinator'][1]['jobs'][0]['created_at'] = (
                datetime.fromisoformat(at(0).replace('Z', '+00:00')) - timedelta(seconds=1)).strftime('%Y-%m-%dT%H:%M:%SZ')
            result = report(manifest_bytes, summary, runs, documents, firsts, earlier)
            self.assertEqual(('incomplete', ['invalid job timing']), (result['status'], result['reasons']))


class RecoveryTests(unittest.TestCase):
    """Recovered campaigns: origin attempts, schema 2 manifests and the paired limit."""

    def setUp(self):
        self.commit = 'a' * 40
        self.repo = 'owner/repo'
        self.layout = {'coordinator': {'a': 'A.cs', 'b': 'B.cs', 'c': 'C.cs'}, 'companion': {'d': 'D.cs'}}
        self.t0 = datetime(2026, 11, 20, tzinfo=timezone.utc)
        patcher = patch.object(campaign.c5_workflow, 'SHARDS', {'a': 'A.cs', 'b': 'B.cs', 'c': 'C.cs', 'd': 'D.cs'})
        patcher.start()
        self.addCleanup(patcher.stop)
        self.ids = {'coordinator': 1, 'companion': 2}
        self.latest = {1: 2, 2: 1}
        self.attempts, self.lists = {}, {}
        # Coordinator: attempt 1 at 0 s, attempt 2 at 1,000 s. Companion: attempt 1 at 100 s, attempt 2 at 1,100 s.
        for role, base in (('coordinator', 0), ('companion', 100)):
            for attempt in (1, 2):
                self.attempts[(self.ids[role], attempt)] = self.run_doc(role, attempt, base + 1000 * (attempt - 1))
        self.attempts[(1, 2)]['status'] = 'in_progress'
        self.attempts[(1, 2)]['conclusion'] = None
        self.set_jobs('coordinator', 1, {'a': 999, 'b': 1000, 'c': 1001})
        self.set_jobs('coordinator', 2, {'a': 999, 'b': 1000, 'c': 1001}, aggregate=True)
        self.set_jobs('companion', 1, {'d': 150})
        self.set_jobs('companion', 2, {'d': 150})

    def at(self, seconds):
        return (self.t0 + timedelta(seconds=seconds)).strftime('%Y-%m-%dT%H:%M:%SZ')

    def run_doc(self, role, attempt, created):
        return {'id': self.ids[role], 'run_attempt': attempt, 'path': campaign.PATHS[role],
                'event': 'workflow_dispatch', 'head_branch': 'main', 'head_sha': self.commit,
                'repository': {'full_name': self.repo}, 'head_repository': {'full_name': self.repo},
                'created_at': self.at(created), 'run_started_at': self.at(created), 'updated_at': self.at(created + 500),
                'status': 'completed', 'conclusion': 'success'}

    def set_jobs(self, role, attempt, starts, aggregate=False):
        rows = []
        for shard, started in starts.items():
            target = {**self.layout['coordinator'], **self.layout['companion']}[shard]
            rows.append({'id': len(rows) + 100 * attempt, 'name': campaign.job_name(shard, target),
                         'run_id': self.ids[role], 'run_attempt': attempt, 'head_sha': self.commit,
                         'status': 'completed', 'conclusion': 'success', 'started_at': self.at(started)})
        if aggregate:
            rows.append({'id': 999, 'name': 'aggregate', 'run_id': self.ids[role], 'run_attempt': attempt,
                         'head_sha': self.commit, 'status': 'in_progress', 'conclusion': None,
                         'started_at': self.at(1002)})
        self.lists[(self.ids[role], attempt)] = rows

    def job(self, role, attempt, shard):
        target = {**self.layout['coordinator'], **self.layout['companion']}[shard]
        name = campaign.job_name(shard, target)
        return next(job for job in self.lists[(self.ids[role], attempt)] if job['name'] == name)

    def prepare(self, coordinator_attempt=2, companion_attempt=1):
        def api(_repo, suffix):
            match = re.fullmatch(r'actions/runs/(\d+)(?:/attempts/(\d+))?', suffix)
            if match[2] is None:
                return {'run_attempt': self.latest[int(match[1])]}
            return copy.deepcopy(self.attempts[(int(match[1]), int(match[2]))])

        def jobs(_repo, run_id, attempt):
            rows = copy.deepcopy(self.lists[(run_id, attempt)])
            return {'total_count': len(rows), 'jobs': rows}

        def check_output(command, **_options):
            return self.commit + '\n' if command[1] == 'rev-parse' else b'workflow'

        with tempfile.TemporaryDirectory() as directory, patch.object(campaign, 'matrices', return_value=self.layout), \
                patch.object(campaign, 'api', side_effect=api), patch.object(campaign, 'jobs', side_effect=jobs), \
                patch.object(campaign.subprocess, 'check_output', side_effect=check_output), \
                patch.object(Path, 'read_bytes', return_value=b'workflow'), \
                patch.object(campaign.time, 'monotonic', return_value=0):
            output = Path(directory) / 'manifest.json'
            campaign.prepare(self.repo, self.commit, 1, coordinator_attempt, 2, companion_attempt, output)
            with output.open('rb') as stream:
                raw = stream.read()
            return json.loads(raw), raw

    def test_first_attempts_write_schema_one(self):
        self.latest = {1: 1, 2: 1}
        for role in campaign.PATHS:
            self.attempts[(self.ids[role], 1)]['status'] = 'completed' if role == 'companion' else 'in_progress'
        self.set_jobs('coordinator', 1, {'a': 10, 'b': 11, 'c': 12}, aggregate=True)
        manifest, raw = self.prepare(coordinator_attempt=1)
        expected = {'schema': 1, 'repository': self.repo, 'commit': self.commit,
                    'runs': {'coordinator': {'id': 1, 'attempt': 1, 'path': campaign.PATHS['coordinator'],
                                             'created_at': self.at(0)},
                             'companion': {'id': 2, 'attempt': 1, 'path': campaign.PATHS['companion'],
                                           'created_at': self.at(100)}},
                    'shards': {shard: {'target': target, 'run_id': '1' if shard != 'd' else '2', 'run_attempt': '1'}
                               for shard, target in (('a', 'A.cs'), ('b', 'B.cs'), ('c', 'C.cs'), ('d', 'D.cs'))},
                    'incomplete_reasons': []}
        self.assertEqual(expected, manifest)
        self.assertEqual((json.dumps(expected, indent=2, sort_keys=True) + '\n').encode(), raw)

    def test_origin_rule_selects_rerun_shards(self):
        manifest, _ = self.prepare()
        self.assertEqual(2, manifest['schema'])
        self.assertEqual([], manifest['incomplete_reasons'])
        # A started 1 s before attempt 2 began, B exactly when it began, C 1 s after.
        self.assertEqual(['1', '2', '2', '1'], [manifest['shards'][s]['run_attempt'] for s in 'abcd'])
        self.assertEqual(self.at(0), manifest['runs']['coordinator']['first_created_at'])
        self.assertEqual(self.at(1000), manifest['runs']['coordinator']['created_at'])
        self.assertEqual(self.at(100), manifest['runs']['companion']['first_created_at'])
        self.assertEqual(1, manifest['runs']['companion']['attempt'])
        origins = campaign.validate_manifest(manifest, self.commit, self.layout['coordinator'], self.layout['companion'])
        self.assertEqual('1', origins['a']['run_attempt'])

    def test_unverified_origin_fails_closed(self):
        cases = {
            'no start in attempt 2': lambda: self.job('coordinator', 2, 'a').pop('started_at'),
            'start moved in attempt 1': lambda: self.job('coordinator', 1, 'a').update(started_at=self.at(998)),
            'attempt 1 job failed': lambda: self.job('coordinator', 1, 'a').update(conclusion='failure'),
            'shard job failed in attempt 2': lambda: self.job('coordinator', 2, 'c').update(conclusion='failure'),
            'attempt 1 job missing': lambda: self.lists[(1, 1)].pop(0),
            'attempt 1 run id differs': lambda: self.job('coordinator', 1, 'a').update(run_id=7),
        }
        for label, change in cases.items():
            with self.subTest(label):
                self.setUp()
                change()
                shard = 'c' if 'attempt 2' in label and 'failed' in label else 'a'
                manifest, _ = self.prepare()
                self.assertIn(f'{shard}: unverified attempt origin', manifest['incomplete_reasons'])
                self.assertTrue(manifest['incomplete_reasons'])
                campaign.validate_manifest(manifest, self.commit, self.layout['coordinator'], self.layout['companion'])

    def test_job_before_every_attempt_or_without_attempt_starts_fails_closed(self):
        # No attempt began at or before the job's start: no origin exists, so none is invented.
        self.job('coordinator', 2, 'a')['started_at'] = self.at(-1)
        manifest, _ = self.prepare()
        self.assertEqual(['a: unverified attempt origin'], manifest['incomplete_reasons'])
        self.assertEqual('2', manifest['shards']['a']['run_attempt'])
        # An attempt document without a usable start leaves no shard of that run verifiable.
        for change in (lambda doc: doc.pop('run_started_at'), lambda doc: doc.update(run_started_at='not a time')):
            with self.subTest(change=change):
                self.setUp()
                change(self.attempts[(1, 1)])
                manifest, _ = self.prepare()
                self.assertEqual([f'{s}: unverified attempt origin' for s in 'abc'], manifest['incomplete_reasons'])

    def test_unreadable_origin_jobs_fail_closed(self):
        del self.lists[(1, 1)]
        manifest, _ = self.prepare()
        self.assertIn('a: unverified attempt origin', manifest['incomplete_reasons'])
        self.assertNotIn('b: unverified attempt origin', manifest['incomplete_reasons'])

    def test_invalid_attempt_document_prepares_no_manifest(self):
        self.attempts[(1, 1)]['head_branch'] = 'other'
        with self.assertRaisesRegex(ValueError, 'workflow, event, branch or commit mismatch'):
            self.prepare()

    def test_attempt_limit(self):
        for attempt in (5, 6):
            with self.subTest(attempt=attempt):
                self.setUp()
                self.latest = {1: attempt, 2: 1}
                for k in range(1, attempt + 1):
                    self.attempts[(1, k)] = self.run_doc('coordinator', k, 1000 * (k - 1))
                self.attempts[(1, attempt)]['status'] = 'in_progress'
                self.attempts[(1, attempt)]['conclusion'] = None
                started = {s: 1000 * (attempt - 1) + 1 for s in 'abc'}
                self.set_jobs('coordinator', attempt, started, aggregate=True)
                manifest, _ = self.prepare(coordinator_attempt=attempt)
                self.assertEqual(2, manifest['schema'])
                self.assertEqual(attempt == 6, 'too many run attempts' in manifest['incomplete_reasons'])
                if attempt == 5:
                    self.assertEqual([], manifest['incomplete_reasons'])
                    self.assertEqual({'5'}, {manifest['shards'][s]['run_attempt'] for s in 'abc'})

    def test_companion_latest_attempt(self):
        self.latest[2] = 2
        manifest, _ = self.prepare(companion_attempt=1)
        self.assertEqual(2, manifest['runs']['companion']['attempt'])
        self.assertEqual(self.at(1100), manifest['runs']['companion']['created_at'])
        self.assertEqual('1', manifest['shards']['d']['run_attempt'])
        self.assertEqual([], manifest['incomplete_reasons'])
        self.latest[2] = 1
        manifest, _ = self.prepare(companion_attempt=2)
        self.assertIn('companion attempt regressed', manifest['incomplete_reasons'])
        self.assertEqual(2, manifest['runs']['companion']['attempt'])

    def recovered_manifest(self):
        manifest, _ = self.prepare()
        return manifest

    def validate(self, manifest):
        return campaign.validate_manifest(manifest, self.commit, self.layout['coordinator'], self.layout['companion'])

    def test_validate_manifest_schema_two(self):
        origins = self.validate(self.recovered_manifest())
        self.assertEqual({'target': 'B.cs', 'run_id': '1', 'run_attempt': '2'}, origins['b'])
        single = self.recovered_manifest()
        single['runs']['coordinator']['attempt'] = 1
        single['runs']['companion']['attempt'] = 1
        single['runs']['coordinator']['first_created_at'] = single['runs']['coordinator']['created_at']
        for shard in 'abc':
            single['shards'][shard]['run_attempt'] = '1'
        cases = {
            'origin zero': lambda m: m['shards']['a'].update(run_attempt='0'),
            'origin with leading zero': lambda m: m['shards']['a'].update(run_attempt='01'),
            'origin above the run attempt': lambda m: m['shards']['a'].update(run_attempt='3'),
            'origin not a string': lambda m: m['shards']['a'].update(run_attempt=1),
            'first creation after creation': lambda m: m['runs']['coordinator'].update(
                first_created_at=self.at(1001)),
            'extra run key': lambda m: m['runs']['coordinator'].update(extra=1),
            'missing first creation': lambda m: m['runs']['coordinator'].pop('first_created_at'),
            'extra shard key': lambda m: m['shards']['a'].update(extra=1),
            'shard of another run': lambda m: m['shards']['d'].update(run_id='1'),
            'missing shard': lambda m: m['shards'].pop('d'),
            'schema three': lambda m: m.update(schema=3),
        }
        for label, change in cases.items():
            with self.subTest(label), self.assertRaises(ValueError):
                manifest = self.recovered_manifest()
                change(manifest)
                self.validate(manifest)
        with self.subTest('schema two with every origin equal to attempt one'), self.assertRaises(ValueError):
            self.validate(single)
        with self.subTest('schema one keeps rejecting an origin that is not its run attempt'), self.assertRaises(ValueError):
            manifest = self.recovered_manifest()
            manifest['schema'] = 1
            self.validate(manifest)

    def test_aggregate_accepts_rerun_shard_only_at_its_origin(self):
        repository, companion_id, shard = 'owner/repo', '67890', 'phase'
        legacy_layout(self)
        rerun = 'publication'
        layout = {'coordinator': {n: t for n, t in c5_workflow.SHARDS.items() if n != shard},
                  'companion': {shard: c5_workflow.SHARDS[shard]}}
        runs = {'coordinator': {'id': int(RUN_ID), 'attempt': int(ATTEMPT), 'path': campaign.PATHS['coordinator'],
                                'created_at': '2026-10-25T17:59:00Z', 'first_created_at': '2026-10-25T17:00:00Z'},
                'companion': {'id': int(companion_id), 'attempt': 1, 'path': campaign.PATHS['companion'],
                              'created_at': '2026-10-25T17:59:00Z', 'first_created_at': '2026-10-25T17:59:00Z'}}
        origin = {name: ('1' if name != rerun else ATTEMPT) for name in c5_workflow.SHARDS}
        manifest = {'schema': 2, 'repository': repository, 'commit': COMMIT, 'runs': runs, 'incomplete_reasons': [],
                    'shards': {name: {'target': target, 'run_id': str(runs[role]['id']),
                                      'run_attempt': origin[name]}
                               for role, entries in layout.items() for name, target in entries.items()}}
        for variant in ('complete', 'rerun receipt says attempt 1'):
            with self.subTest(variant), tempfile.TemporaryDirectory() as temporary:
                directory = Path(temporary)
                fixture = AggregateFixture(directory)
                for name in c5_workflow.SHARDS:
                    if origin[name] != ATTEMPT or name == shard:
                        run_id = companion_id if name == shard else RUN_ID
                        fixture.paths[name] = fixture.paths[name].rename(
                            fixture.input / f'c5-mutation-{run_id}-{origin[name]}-{name}')
                        fixture.receipts[name].update(run_id=run_id, run_attempt=origin[name])
                        fixture.save(name)
                if variant != 'complete':
                    fixture.receipts[rerun]['run_attempt'] = '1'
                    fixture.save(rerun)
                manifest_path = directory / 'campaign.json'
                write_json(manifest_path, manifest)
                with patch.object(campaign, 'matrices', return_value=layout), \
                        patch.object(c5_workflow, 'git', side_effect=[COMMIT.encode(), b'2' * 40]), \
                        patch.dict(os.environ, {'GITHUB_REPOSITORY': repository}):
                    result = c5_workflow.aggregate(fixture.input, COMMIT, RUN_ID, ATTEMPT, fixture.output, manifest_path)
                summary = json.loads((fixture.output / 'summary.json').read_bytes())
                self.assertEqual(variant != 'complete', result == 1)
                self.assertIs(summary['measurement_complete'], variant == 'complete')
                if variant != 'complete':
                    self.assertTrue(any(rerun in reason and 'receipt identity/completion mismatch' in reason
                                        for reason in summary['incomplete_reasons']))


if __name__ == '__main__':
    unittest.main()

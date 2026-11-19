import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import c5_campaign as campaign


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
                   lambda m: m.update(extra=True)]
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

    def test_prepare_and_failed_or_late_peer(self):
        def api(_repo, suffix):
            return copy.deepcopy(self.runs['coordinator' if '/1/' in suffix else 'companion'])
        def jobs(_repo, run_id, _attempt):
            return self.document('coordinator' if run_id == 1 else 'companion')
        start = campaign.timestamp(self.runs['companion']['created_at']).timestamp()
        with tempfile.TemporaryDirectory() as directory, patch.object(campaign, 'matrices', return_value=self.layout), \
                patch.object(campaign, 'api', side_effect=api), patch.object(campaign, 'jobs', side_effect=jobs), \
                patch.object(campaign.subprocess, 'check_output', side_effect=[self.commit + '\n', b'workflow', b'workflow'] * 3), \
                patch.object(Path, 'read_bytes', return_value=b'workflow'), \
                patch.object(campaign.time, 'time', return_value=start + 100):
            output = Path(directory) / 'manifest.json'
            campaign.prepare(self.repo, self.commit, 1, 1, 2, 1, output)
            self.assertTrue(output.is_file())
            self.runs['companion']['conclusion'] = 'failure'
            with self.assertRaisesRegex(ValueError, 'companion run failed'):
                campaign.prepare(self.repo, self.commit, 1, 1, 2, 1, Path(directory) / 'fail')
            self.runs['companion']['conclusion'] = 'success'
            with patch.object(campaign.time, 'time', return_value=start + 3001), \
                    self.assertRaisesRegex(ValueError, 'deadline exceeded'):
                campaign.prepare(self.repo, self.commit, 1, 1, 2, 1, Path(directory) / 'late')

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
        self.assertEqual('missed', self.terminal_report(manifest, summary, runs, documents)['status'])

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

    def test_terminal_rejects_malformed_or_foreign_manifest(self):
        manifest, summary, runs, documents = self.terminal_inputs()
        self.assertEqual('incomplete', campaign.terminal_report(
            'foreign/repo', self.commit, manifest, json.dumps(summary).encode(),
            self.layout, runs, documents)['status'])
        self.assertEqual('incomplete', campaign.terminal_report(
            self.repo, self.commit, b'{"schema":1,"schema":1}', json.dumps(summary).encode(),
            self.layout, runs, documents)['status'])


if __name__ == '__main__':
    unittest.main()

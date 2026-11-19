"""Source-bound RequiredRootFields cohort selection and bootstrap checks."""
import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import c5_workflow as workflow


OWNER = 'verifier-result-codec-p2'
TARGET = 'Common/Results/VerifierResultCodec.cs'
ROOT = Path(__file__).resolve().parents[3]


class ResultCodecCohortTests(unittest.TestCase):
    def setUp(self):
        self.source = (ROOT / 'src/Parchwell.Verifier' / TARGET).read_bytes()
        self.definition = copy.deepcopy(workflow.RESULT_CODEC_COHORT)
        self.rows = [{**copy.deepcopy(identity), 'id': str(9000 + index),
                      'status': workflow.FILTERED[0], 'statusReason': workflow.FILTERED[1]}
                     for index, identity in enumerate(self.definition['identities'].values())]
        identities = sorted(workflow.identity_json(row) for row in self.rows)
        self.definition['inventory_sha256'] = hashlib.sha256(
            ('[' + ','.join(identities) + ']').encode()).hexdigest()

    def test_exact_ownership_and_source_binding(self):
        self.assertEqual(self.definition['source_sha256'], hashlib.sha256(self.source).hexdigest())
        left, right = [workflow.COHORTS[f'{OWNER}-{suffix}'][1] for suffix in ('a', 'b')]
        self.assertEqual(5, len(left))
        self.assertEqual(5, len(right))
        self.assertFalse(set(left) & set(right))
        self.assertEqual(set(self.definition['identities']), set(left) | set(right))
        self.assertEqual(workflow.LOGICAL_PARTS[OWNER], workflow.PARTS[f'{OWNER}-a'])
        self.assertEqual(workflow.PARTS[f'{OWNER}-a'], workflow.PARTS[f'{OWNER}-b'])
        self.assertNotIn(OWNER, workflow.PARTS)

    def test_resolves_ids_by_identity_and_rejects_changed_inventories(self):
        with patch.object(workflow, 'RESULT_CODEC_COHORT', self.definition):
            resolved = workflow.resolve_cohort(self.rows, self.source, OWNER)
            self.assertEqual('9000', resolved['4093'])
            self.assertEqual('9009', resolved['4102'])
            for rows, source in ((self.rows, self.source + b' '), (self.rows[:-1], self.source),
                                 (self.rows + [self.rows[0]], self.source)):
                with self.subTest(length=len(rows), source_length=len(source)), self.assertRaises(ValueError):
                    workflow.resolve_cohort(rows, source, OWNER)
            duplicate = self.rows + [dict(self.rows[0], id='9999')]
            values = sorted(workflow.identity_json(row) for row in duplicate)
            self.definition['inventory_sha256'] = hashlib.sha256(
                ('[' + ','.join(values) + ']').encode()).hexdigest()
            with self.assertRaisesRegex(ValueError, 'duplicate cohort identity'):
                workflow.resolve_cohort(duplicate, self.source, OWNER)

    def test_both_groups_bootstrap_and_run_each_assigned_mutation(self):
        for suffix, first_index in (('a', 0), ('b', 5)):
            with self.subTest(suffix=suffix), tempfile.TemporaryDirectory() as temp:
                report_rows = copy.deepcopy(self.rows)
                report_rows[first_index].update(status='Killed', statusReason=None)
                report = {'schemaVersion': '2', 'projectRoot': '/synthetic', 'files': {
                    '/synthetic/' + TARGET: {'language': 'cs', 'source': self.source.decode(),
                                              'mutants': report_rows}}}
                calls = []
                def execute(target, plan, output, prefix, cwd, deadline, *, bootstrap_shard=None):
                    calls.append(copy.deepcopy(plan))
                    entries = []
                    for item in plan:
                        entry = dict(item)
                        if bootstrap_shard:
                            entry['directory_id'] = 'bootstrap-' + item['mutant_id']
                            path = workflow.isolated_directory(output, entry, bootstrap_shard) / workflow.REPORT_PATH
                            path.parent.mkdir(parents=True)
                            path.write_text(json.dumps(report))
                        entries.append(entry)
                    return entries, None
                with patch.object(workflow, 'RESULT_CODEC_COHORT', self.definition), \
                     patch.object(workflow, 'run_isolation_plan', side_effect=execute):
                    entries, error = workflow.run_cohort(f'{OWNER}-{suffix}', TARGET, self.source,
                                                         [(477, 807)], Path(temp), [], Path(temp), 100)
                self.assertIsNone(error)
                self.assertEqual([str(9000 + i) for i in range(first_index, first_index + 5)],
                                 [entry['mutant_id'] for entry in entries])
                self.assertEqual(workflow.cohort_bootstrap(OWNER, str(4093 + first_index)), calls[0][0])
                self.assertEqual(9 if suffix == 'a' else 0, len(calls[0][0]['exclusions']))
                self.assertEqual(4, len(calls[1]))

    def test_rejects_incomplete_or_overlapping_ownership(self):
        for groups in ((('4093',), ('4098',)),
                       (tuple(map(str, range(4093, 4098))), tuple(map(str, range(4097, 4103))))):
            cohorts = {f'{OWNER}-{suffix}': (OWNER, group)
                       for suffix, group in zip(('a', 'b'), groups)}
            report_rows = copy.deepcopy(self.rows)
            report_rows[0].update(status='Killed', statusReason=None)
            report = {'schemaVersion': '2', 'projectRoot': '/synthetic', 'files': {
                '/synthetic/' + TARGET: {'language': 'cs', 'source': self.source.decode(), 'mutants': report_rows}}}
            def execute(target, plan, output, prefix, cwd, deadline, *, bootstrap_shard=None):
                entry = dict(plan[0], directory_id='bootstrap-4093')
                path = workflow.isolated_directory(output, entry, bootstrap_shard) / workflow.REPORT_PATH
                path.parent.mkdir(parents=True)
                path.write_text(json.dumps(report))
                return [entry], None
            with self.subTest(groups=groups), tempfile.TemporaryDirectory() as temp, \
                 patch.object(workflow, 'COHORTS', cohorts), \
                 patch.object(workflow, 'RESULT_CODEC_COHORT', self.definition), \
                 patch.object(workflow, 'run_isolation_plan', side_effect=execute):
                _, error = workflow.run_cohort(f'{OWNER}-a', TARGET, self.source, [(477, 807)],
                                               Path(temp), [], Path(temp), 100)
                self.assertIn('cohort inventory changed', error)


if __name__ == '__main__':
    unittest.main()

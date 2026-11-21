"""Committed cohort declaration and physical-owner provenance checks."""
from pathlib import Path
import unittest

import c5_runtime as runtime
import c5_workflow as workflow


class CohortDeclarationTests(unittest.TestCase):
    def setUp(self):
        self.data = Path(workflow.__file__).read_bytes()

    def test_typed_literal_remains_in_executable_fingerprint(self):
        values, code = runtime.runner_literals(self.data)
        self.assertEqual(workflow.RESULT_CODEC_COHORT, values['RESULT_CODEC_COHORT'])
        self.assertEqual(workflow.RESULT_CODEC_P4_COHORT, values['RESULT_CODEC_P4_COHORT'])
        self.assertIn(b'RESULT_CODEC_COHORT: CohortDefinition = {', code)
        self.assertIn(b'RESULT_CODEC_P4_COHORT: CohortDefinition = {', code)
        changed = self.data.replace(b'8526a0adbb9e5ac1cac4c92177377616d2768c003fa7d91338d231d99baa8dba',
                                    b'9526a0adbb9e5ac1cac4c92177377616d2768c003fa7d91338d231d99baa8dba', 1)
        self.assertNotEqual(self.data, changed)
        _, changed_code = runtime.runner_literals(changed)
        self.assertNotEqual(code, changed_code)

    def test_duplicate_nested_key_and_rebinding_rejected(self):
        damaged = self.data.replace(b'"line": 14, "column": 71',
                                    b'"line": 14, "line": 14, "column": 71', 1)
        self.assertNotEqual(damaged, self.data)
        for data in (damaged, self.data + b'\nRESULT_CODEC_COHORT = {}\n',
                     self.data + b'\nRESULT_CODEC_P4_COHORT = {}\n'):
            with self.subTest(damage=data[-30:]), self.assertRaises(ValueError):
                runtime.runner_literals(data)

    def test_old_and_new_matrices_use_their_own_cohort_declarations(self):
        values, _ = runtime.runner_literals(self.data)
        new_matrix = dict(workflow.SHARDS)
        new = runtime.committed_physical_owners(values, new_matrix)
        self.assertEqual(workflow.LOGICAL_PARTS['verifier-result-codec-p2'],
                         new['verifier-result-codec-p2-a'][1])
        self.assertEqual(workflow.LOGICAL_PARTS['verifier-result-codec-p4'],
                         new['verifier-result-codec-p4-a'][1])
        old_values = dict(values)
        del old_values['RESULT_CODEC_COHORT']
        old_matrix = dict(new_matrix)
        target = old_matrix.pop('verifier-result-codec-p2-a')
        self.assertEqual(target, old_matrix.pop('verifier-result-codec-p2-b'))
        old_matrix['verifier-result-codec-p2'] = target
        old = runtime.committed_physical_owners(old_values, old_matrix)
        self.assertIn('verifier-result-codec-p2', old)
        self.assertNotIn('verifier-result-codec-p2-a', old)
        for declarations, matrix in ((values, old_matrix), (old_values, new_matrix)):
            with self.subTest(matrix_size=len(matrix)), self.assertRaises(ValueError):
                runtime.committed_physical_owners(declarations, matrix)

    def test_derive_earlier_p4_owner_and_retain_exact_earlier_cohorts(self):
        new, _ = runtime.runner_literals(self.data)
        old = dict(new)
        del old['RESULT_CODEC_P4_COHORT']
        new_matrix = dict(workflow.SHARDS)
        old_matrix = dict(new_matrix)
        old_matrix['verifier-result-codec-p4'] = old_matrix.pop('verifier-result-codec-p4-a')
        del old_matrix['verifier-result-codec-p4-b']
        first = 'verifier-result-codec-p2-a'
        self.assertEqual(runtime.selected_invocation(old, old_matrix, first),
                         runtime.selected_invocation(new, new_matrix, first))
        self.assertIsNone(runtime.selected_invocation(old, old_matrix, 'verifier-result-codec-p4-a'))

        defaults = {'stryker-config': {}}
        targets = {'Example.cs': 'source'}
        old_maps = ({**runtime.CAPACITY_EXECUTION, 'build': 'same'}, targets)
        new_maps = ({**runtime.FOLLOWUP_EXECUTION, 'build': 'same'}, targets)
        self.assertTrue(runtime.followup_equivalent(old, old_maps, defaults, new, new_maps, defaults))
        modified = ({**new_maps[0], 'build': 'different'}, targets)
        self.assertFalse(runtime.followup_equivalent(old, old_maps, defaults, new, modified, defaults))

    def test_effective_span_checks_only_the_worker_span_and_full_crossing_complement(self):
        matrix = {name: 'Example.cs' for name in ('example-p1', 'example-p2', 'example-p3', 'example-crossing')}
        old = {'TARGETS': {'example': ('Example.cs', 3)}, 'PRIMARY_CUTS': {'example': (100, 200)}}
        new = {**old, 'PRIMARY_CUTS': {'example': (100, 240)}}
        self.assertEqual(runtime.selected_invocation(old, matrix, 'example-p1'),
                         runtime.selected_invocation(new, matrix, 'example-p1'))
        for shard in ('example-p2', 'example-p3', 'example-crossing'):
            self.assertNotEqual(runtime.selected_invocation(old, matrix, shard),
                                runtime.selected_invocation(new, matrix, shard))

import contextlib
import io
import json
from pathlib import Path
import tempfile
import unittest

import c5_equivalence as equivalence

FILE_A = '/tmp/x/repository/src/Parchwell.Verifier/A.cs'
FILE_B = '/tmp/x/repository/src/Parchwell.Verifier/B.cs'
TESTS = {'t1': 'N.C.Keep(System.Int32)', 't2': 'N.C.Slow', 't3': 'N.C.SlowCase(System.String)', 't4': 'N.C.SlowBar'}
EXCLUDED = ['N.C.Slow', 'N.C.SlowCase']
COMMITTED = Path(__file__).with_name('c5-fast-suite-excluded.json')
SECTION_4_KEYS = {
    'Parchwell.Verifier.Tests.Host.B10SweepHandlerTests.Given_MaximumAdmittedPhaseVolumeInFinalTail_When_Swept_Then_DeploymentLimitsFitAndActualSubLimitsFail',
    'Parchwell.Verifier.Tests.Host.VerifyInputCapacityTests.Given_FullA01AttemptInventory_When_UsingDeployedInputCapacity_Then_PublishesExactCompleteCounts',
    'Parchwell.Verifier.Tests.Consumer.ConsumerArtifactTests.Given_RecordBlobRowBoundaries_U4_When_Loaded_Then_EnforcesRollover',
    'Parchwell.Verifier.Tests.Authentication.VerifierKafkaTokenProviderRetryTests.Given_EveryPostBlocks_When_TokenRequested_Then_FailsAfterTwoPosts',
    'Parchwell.Verifier.Tests.ProbeGate.ProbeGateHostTests.Given_SynchronousBlockedGateRead_When_WatchdogExpires_Then_ProductionHostExits124',
    'Parchwell.Verifier.Tests.Window.ConsumedMetadataCaptureTests.ConsumedMetadata_JsonEscapingAndDescriptorCountAreBounded',
    'Parchwell.Verifier.Tests.Consumer.ConsumerRecordNameTests.Given_UnsafeRecordName_U4_When_ExtensionReads_Then_PreservesInvalidProjectionAndSingleOffsetRead',
    'Parchwell.Verifier.Tests.Authentication.VerifierKafkaTokenProviderRetryTests.Given_CallerCancelsDuringFirstPost_When_TokenRequested_Then_NoSecondPostIsSent',
    'Parchwell.Verifier.Tests.Authentication.VerifierKafkaTokenProviderRetryTests.Given_EveryPostAnswers503_When_TokenRequested_Then_FailsAfterTwoPosts',
    'Parchwell.Verifier.Tests.Authentication.VerifierKafkaTokenProviderRetryTests.Given_FirstPostBlocks_When_TokenRequested_Then_OneRetrySucceeds',
    'Parchwell.Verifier.Tests.Window.ConsumedRecordBudgetTests.Given_DeployedPhaseRecordLimit_When_AccountingEmptyRecords_Then_EqualityPassesAndNextRecordCannotAdvanceCounters',
    'Parchwell.Verifier.Tests.SingleAction.ProbeV2StatisticsBudgetTests.Given_RealNativeThirtyTwoPartitionSixtyFourBrokerReport_When_Judged_Then_OldBudgetFailsAndRepairedBudgetPreservesHash',
}


def mutant(number, status, killed_by=None, line=1, column=1, mutator='M', static=False, replacement='r'):
    row = {'id': number, 'mutatorName': mutator, 'replacement': replacement,
           'location': {'start': {'line': line, 'column': column}, 'end': {'line': line, 'column': column + 4}},
           'status': status, 'static': static}
    if killed_by is not None:
        row['killedBy'] = killed_by
    return row


def report(mutants, file=FILE_A, tests=TESTS):
    return {'files': {file: {'language': 'cs', 'mutants': mutants}},
            'testFiles': {'/t/T.cs': {'tests': [{'id': key, 'name': name} for key, name in tests.items()]}}}


class Base(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.output = self.root / 'out.json'
        self.excluded = self.write('excluded.json', EXCLUDED)

    def write(self, relative, value):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(value if isinstance(value, str) else json.dumps(value), encoding='utf-8')
        return path

    def reports(self, name, *documents):
        for index, document in enumerate(documents):
            self.write(f'{name}/r{index}/mutation-report.json', document)
        (self.root / name).mkdir(exist_ok=True)
        return self.root / name

    def run_main(self, *argv):
        err = io.StringIO()
        with contextlib.redirect_stderr(err):
            status = equivalence.main([str(item) for item in argv])
        return status, err.getvalue()

    def prove(self, *documents, excluded=None):
        directory = self.reports('reports', *documents)
        status, err = self.run_main('prove', '--reports-dir', directory, '--excluded', excluded or self.excluded,
                                    '--output', self.output)
        return status, err, (json.loads(self.output.read_text()) if self.output.exists() else None)

    def compare(self, before, after, excluded=None):
        before_dir = self.reports('before', *before)
        after_dir = self.reports('after', *after)
        status, err = self.run_main('compare', '--before', before_dir, '--after', after_dir,
                                    '--excluded', excluded or self.excluded, '--output', self.output)
        return status, err, (json.loads(self.output.read_text()) if self.output.exists() else None)

    def assert_failure(self, result, name):
        status, err, output = result
        self.assertEqual(2, status)
        self.assertTrue(err.startswith(f'c5_equivalence: {name}:'), err)
        self.assertIsNone(output)


class ProveTests(Base):
    def test_killed_by_kept_and_excluded_is_equivalent(self):
        status, _, out = self.prove(report([mutant('1', 'Killed', ['t1', 't2'])]))
        self.assertEqual(0, status)
        self.assertEqual('equivalent', out['status'])
        self.assertEqual(1, out['killed'])
        self.assertEqual([], out['lost'])

    def test_killed_only_by_excluded_is_lost(self):
        row = mutant('1', 'Killed', ['t2', 't3'], mutator='Arith', replacement='x-y', static=True)
        status, _, out = self.prove(report([row]))
        self.assertEqual(1, status)
        self.assertEqual('not_equivalent', out['status'])
        self.assertEqual(1, len(out['lost']))
        lost = out['lost'][0]
        self.assertEqual({'file', 'mutant_ids', 'mutator', 'replacement', 'location', 'static', 'killed_by'}, set(lost))
        self.assertEqual('src/Parchwell.Verifier/A.cs', lost['file'])
        self.assertEqual(['1'], lost['mutant_ids'])
        self.assertEqual(['N.C.Slow', 'N.C.SlowCase'], lost['killed_by'])
        self.assertEqual((row['mutatorName'], row['replacement'], row['location'], row['static']),
                         (lost['mutator'], lost['replacement'], lost['location'], lost['static']))

    def test_prefix_name_is_not_excluded(self):
        status, _, out = self.prove(report([mutant('1', 'Killed', ['t4'])]))
        self.assertEqual(0, status)
        self.assertEqual([], out['lost'])

    def test_duplicate_row_cannot_mask_a_lost_row(self):
        status, _, out = self.prove(report([mutant('1', 'Killed', ['t2'])]), report([mutant('7', 'Killed', ['t1'])]))
        self.assertEqual(1, status)
        self.assertEqual(1, len(out['lost']))
        self.assertEqual(['1'], out['lost'][0]['mutant_ids'])
        self.assertEqual(['N.C.Slow'], out['lost'][0]['killed_by'])
        self.assertEqual(1, out['killed'])

    def test_duplicate_rows_both_kept(self):
        status, _, out = self.prove(report([mutant('1', 'Killed', ['t1', 't2'])]), report([mutant('7', 'Killed', ['t1', 't2'])]))
        self.assertEqual(0, status)
        self.assertEqual(1, out['killed'])

    def test_duplicate_reports_both_excluded(self):
        status, _, out = self.prove(report([mutant('1', 'Killed', ['t2'])]), report([mutant('7', 'Killed', ['t2'])]))
        self.assertEqual(1, status)
        self.assertEqual(1, len(out['lost']))
        self.assertEqual(['1', '7'], out['lost'][0]['mutant_ids'])

    def test_mutant_ids_sort_as_strings(self):
        status, _, out = self.prove(report([mutant('10', 'Killed', ['t2'])]), report([mutant('9', 'Killed', ['t2'])]))
        self.assertEqual(1, status)
        self.assertEqual(['10', '9'], out['lost'][0]['mutant_ids'])

    def test_static_flag_separates_identities(self):
        rows = [mutant('1', 'Killed', ['t1'], static=False), mutant('2', 'Killed', ['t2'], static=True)]
        status, _, out = self.prove(report(rows))
        self.assertEqual(1, status)
        self.assertEqual(1, len(out['lost']))
        self.assertIs(True, out['lost'][0]['static'])

    def test_other_statuses_are_not_lost(self):
        rows = [mutant(str(i), status, line=i) for i, status in
                enumerate(['Survived', 'Timeout', 'Ignored', 'CompileError'], 1)]
        status, _, out = self.prove(report(rows))
        self.assertEqual(0, status)
        self.assertEqual(0, out['killed'])
        self.assertEqual([], out['lost'])
        self.assertEqual(1, len(out['timeouts']))
        self.assertEqual({'file', 'mutant_ids', 'mutator', 'replacement', 'location', 'static'}, set(out['timeouts'][0]))
        self.assertEqual(['2'], out['timeouts'][0]['mutant_ids'])

    def test_timeout_with_a_killed_row_is_not_listed(self):
        status, _, out = self.prove(report([mutant('1', 'Timeout')]), report([mutant('2', 'Killed', ['t1'])]))
        self.assertEqual(0, status)
        self.assertEqual([], out['timeouts'])

    def test_zero_and_one_lost_boundary(self):
        status, _, out = self.prove(report([mutant('1', 'Killed', ['t1'], line=1), mutant('2', 'Killed', ['t2'], line=2)]))
        self.assertEqual(1, status)
        self.assertEqual(1, len(out['lost']))

    def test_counts_and_schema(self):
        self.write('reports/a/b/mutation-report.json', report([mutant('1', 'Killed', ['t1'])]))
        self.write('reports/c/mutation-report.json', report([mutant('1', 'Killed', ['t1'])]))
        status, _ = self.run_main('prove', '--reports-dir', self.root / 'reports', '--excluded', self.excluded,
                                  '--output', self.output)
        out = json.loads(self.output.read_text())
        self.assertEqual(0, status)
        self.assertEqual((1, 2, 1, 2, [], []), (out['schema'], out['reports'], out['killed'], out['excluded'],
                                                 out['lost'], out['timeouts']))

    def test_no_reports(self):
        self.assert_failure(self.prove(), 'no_reports')

    def test_invalid_report(self):
        broken = report([mutant('1', 'Killed', ['t1'])])
        del broken['testFiles']
        self.assert_failure(self.prove(broken), 'invalid_report')
        self.output.unlink(missing_ok=True)
        row = mutant('1', 'Survived')
        del row['static']
        self.assert_failure(self.prove(report([row])), 'invalid_report')

    def test_invalid_report_shapes(self):
        cases = {'list': [], 'no files': {'testFiles': {}}, 'files list': {'files': [], 'testFiles': {}}}
        for field in ('id', 'mutatorName', 'replacement', 'location', 'status'):
            row = mutant('1', 'Survived')
            del row[field]
            cases[f'no {field}'] = report([row])
        for label, document in cases.items():
            with self.subTest(label):
                for old in (self.root / 'reports').rglob('*.json'):
                    old.unlink()
                status, err, out = self.prove(document)
                self.assertEqual(2, status)
                self.assertTrue(err.startswith('c5_equivalence: invalid_report:'), err)
                self.assertIn('mutation-report.json', err)
                self.assertIsNone(out)

    def test_file_key_without_repository_segment(self):
        self.assert_failure(self.prove(report([mutant('1', 'Killed', ['t1'])], file='/tmp/x/src/A.cs')), 'invalid_report')

    def test_first_repository_segment(self):
        status, _, out = self.prove(report([mutant('1', 'Killed', ['t2'])], file='/tmp/x/repository/src/repository/A.cs'))
        self.assertEqual(1, status)
        self.assertEqual('src/repository/A.cs', out['lost'][0]['file'])

    def test_invalid_excluded(self):
        cases = {'not an array': {'a': 1}, 'unsorted': ['N.C.SlowCase', 'N.C.Slow'], 'duplicate': ['N.C.Slow', 'N.C.Slow'],
                 'empty': ['', 'N.C.Slow'], 'parameters': ['N.C.Slow(System.Int32)'], 'not a string': ['N.C.Slow', 1]}
        for label, value in cases.items():
            with self.subTest(label):
                self.output.unlink(missing_ok=True)
                excluded = self.write('bad.json', value)
                self.assert_failure(self.prove(report([mutant('1', 'Killed', ['t1'])]), excluded=excluded), 'invalid_excluded')

    def test_killed_without_tests(self):
        for label, killed_by in (('empty', []), ('absent', None)):
            with self.subTest(label):
                self.output.unlink(missing_ok=True)
                self.assert_failure(self.prove(report([mutant('1', 'Killed', killed_by)])), 'killed_without_tests')

    def test_unmapped_test_id(self):
        self.assert_failure(self.prove(report([mutant('1', 'Killed', ['t9'])])), 'unmapped_test_id')

    def test_unmatched_excluded(self):
        excluded = self.write('gone.json', ['N.C.Gone', 'N.C.Slow'])
        result = self.prove(report([mutant('1', 'Killed', ['t1'])]), excluded=excluded)
        self.assert_failure(result, 'unmatched_excluded')
        self.assertIn('N.C.Gone', result[1])

    def test_output_is_sorted_and_stable(self):
        document = report([mutant('2', 'Killed', ['t2'], line=9), mutant('1', 'Killed', ['t2'], line=3)])
        self.prove(document)
        first = self.output.read_bytes()
        _, _, out = self.prove(document)
        self.assertEqual(first, self.output.read_bytes())
        self.assertTrue(first.endswith(b'\n'))
        self.assertEqual([3, 9], [item['location']['start']['line'] for item in out['lost']])
        self.assertEqual(first.decode(), json.dumps(out, sort_keys=True) + '\n')

    def test_lost_sort_tie_breaks(self):
        a = report([mutant('3', 'Killed', ['t2'], line=5, column=9, mutator='M'),
                    mutant('4', 'Killed', ['t2'], line=5, column=3, mutator='Z'),
                    mutant('5', 'Killed', ['t2'], line=5, column=3, mutator='B')])
        b = report([mutant('1', 'Killed', ['t2'], line=1, column=1, mutator='M')], file=FILE_B)
        self.write('reports/b/mutation-report.json', b)
        _, _, out = self.prove(a)
        order = [(item['file'][-4:], item['location']['start']['line'], item['location']['start']['column'], item['mutator'])
                 for item in out['lost']]
        self.assertEqual([('A.cs', 5, 3, 'B'), ('A.cs', 5, 3, 'Z'), ('A.cs', 5, 9, 'M'), ('B.cs', 1, 1, 'M')], order)


class CompareTests(Base):
    def test_compare_no_regression(self):
        before = [report([mutant('1', 'Killed', ['t1'])])]
        after = [report([mutant('1', 'Killed', ['t1'])]), report([mutant('1', 'Ignored')])]
        status, _, out = self.compare(before, after)
        self.assertEqual(0, status)
        self.assertEqual(('no_regressions', [], 1, 2), (out['status'], out['regressions'], out['before_reports'], out['after_reports']))

    def test_compare_timeout_is_not_a_regression(self):
        status, _, _ = self.compare([report([mutant('1', 'Killed', ['t2'])])], [report([mutant('1', 'Timeout')])])
        self.assertEqual(0, status)

    def test_compare_survived_is_a_regression(self):
        status, _, out = self.compare([report([mutant('1', 'Killed', ['t1', 't2'])])], [report([mutant('1', 'Survived')])])
        self.assertEqual(1, status)
        self.assertEqual(1, len(out['regressions']))
        entry = out['regressions'][0]
        self.assertEqual(['N.C.Keep', 'N.C.Slow'], entry['before_killed_by'])
        self.assertEqual(['Survived'], entry['after_statuses'])
        self.assertEqual(['N.C.Slow'], entry['excluded_killers'])

    def test_compare_regression_with_no_excluded_killer(self):
        status, _, out = self.compare([report([mutant('1', 'Killed', ['t1'])])], [report([mutant('1', 'NoCoverage')])])
        self.assertEqual(1, status)
        self.assertEqual([], out['regressions'][0]['excluded_killers'])

    def test_compare_identity_missing(self):
        other = report([mutant('2', 'Killed', ['t1'], line=7)])
        self.assert_failure(self.compare([report([mutant('1', 'Killed', ['t1'])])], [other]), 'identity_missing')

    def test_compare_identity_unscored(self):
        after = report([mutant('1', 'Ignored'), mutant('1', 'CompileError')])
        self.assert_failure(self.compare([report([mutant('1', 'Killed', ['t1'])])], [after]), 'identity_unscored')

    def test_compare_ignores_unkilled_before(self):
        other = report([mutant('2', 'Survived', line=7)])
        status, _, _ = self.compare([report([mutant('1', 'Survived')])], [other])
        self.assertEqual(0, status)

    def test_compare_no_reports(self):
        valid = report([mutant('1', 'Killed', ['t1'])])
        for label in ('before', 'after'):
            with self.subTest(label):
                self.output.unlink(missing_ok=True)
                for name in ('before', 'after'):
                    (self.root / name).mkdir(exist_ok=True)
                    for old in (self.root / name).rglob('*.json'):
                        old.unlink()
                other = 'after' if label == 'before' else 'before'
                self.write(f'{other}/r/mutation-report.json', valid)
                status, err = self.run_main('compare', '--before', self.root / 'before', '--after', self.root / 'after',
                                            '--excluded', self.excluded, '--output', self.output)
                self.assertEqual(2, status)
                self.assertTrue(err.startswith('c5_equivalence: no_reports:'), err)
                self.assertFalse(self.output.exists())

    def test_compare_invalid_excluded(self):
        excluded = self.write('bad.json', ['N.C.SlowCase', 'N.C.Slow'])
        valid = report([mutant('1', 'Killed', ['t1'])])
        self.assert_failure(self.compare([valid], [valid], excluded=excluded), 'invalid_excluded')


class CommittedListTests(unittest.TestCase):
    def test_excluded_list_is_committed_form(self):
        text = COMMITTED.read_text(encoding='utf-8')
        keys = equivalence.read_excluded(COMMITTED)
        self.assertTrue(keys)
        self.assertTrue(set(keys) <= SECTION_4_KEYS)
        self.assertTrue(text.endswith('\n'))


if __name__ == '__main__':
    unittest.main()

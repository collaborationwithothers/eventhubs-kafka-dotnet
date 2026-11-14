"""Synthetic evidence tests for physical cohort aggregation."""
import json
import shutil
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import c5_workflow as workflow
import test_c5_workflow as fixture_helpers
from test_c5_workflow import WORKLOAD, legacy_layout, digest, write_json


class CohortAggregateTests(unittest.TestCase):
    def setUp(self):
        legacy_layout(self)
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.directory = Path(temp.name)
        # Reuse the existing two-mutant isolated evidence, then retain each raw
        # invocation as the physical cohort's bootstrap report.
        self.fixture = fixture_helpers.AggregationTests.static_fixture(self, self.directory)
        fixture = self.fixture
        source = fixture.paths['workload']
        # Make the two static mutant spans independent for the two bootstraps.
        for item in fixture.receipts['workload']['isolated']:
            identifier = item['mutant_id']
            folder = source / 'isolated' / identifier
            path = folder / workflow.REPORT_PATH
            report = json.loads(path.read_text())
            rows = report['files']['/synthetic/src/Parchwell.Verifier/' + WORKLOAD]['mutants']
            next(value for value in rows if value['id'] == identifier)['killedBy'] = ['test-id']
            row = next(value for value in rows if value['id'] == '9')
            row['location']['start']['column'] = 32
            row['location']['end']['column'] = 33
            write_json(path, report)
            if identifier == '9':
                item['span'] = [239, 240]
            item['exclusions'] = []
            write_json(folder / 'config.json', workflow.isolated_config(WORKLOAD, item))
            item['report_sha256'] = digest(path)
            item['config_sha256_before'] = item['config_sha256_after'] = digest(folder / 'config.json')
        fixture.save('workload')
        cohorts = {'workload-a': ('workload', ('9',)), 'workload-b': ('workload', ('10',))}
        for name, (_, ids) in cohorts.items():
            folder = source.with_name(source.name + '-' + name[-1])
            shutil.copytree(source, folder)
            entry = next(item for item in fixture.receipts['workload']['isolated'] if item['mutant_id'] == ids[0])
            shutil.copyfile(folder / 'isolated' / ids[0] / workflow.REPORT_PATH, folder / workflow.REPORT_PATH)
            shutil.copyfile(folder / 'isolated' / ids[0] / 'command.log', folder / 'command.log')
            entry['argv'] = ['dotnet', 'stryker', '--config-file', f'/runner/.artifacts/c5-mutation/{name}/isolated/{ids[0]}/config.json',
                             '--output', f'/runner/.artifacts/c5-mutation/{name}/isolated/{ids[0]}/stryker', '--log-to-file', '--verbosity', 'debug']
            receipt = json.loads((folder / 'receipt.json').read_text())
            receipt.update({field: entry[field] for field in ("argv", "child_exit_code", "termination_reason", "command_completed")})
            receipt.update(shard=name, isolated=[entry], report_sha256=digest(folder / workflow.REPORT_PATH),
                           command_log_sha256=digest(folder / 'command.log'))
            write_json(folder / 'receipt.json', receipt)
        shutil.rmtree(source)
        parts = dict(workflow.PARTS)
        part = parts.pop('workload')
        parts.update({name: part for name in cohorts})
        shards = dict(workflow.SHARDS)
        shards.pop('workload')
        shards.update({name: WORKLOAD for name in cohorts})
        self.enterContext(patch.object(workflow, 'COHORTS', cohorts))
        self.enterContext(patch.object(workflow, 'PARTS', parts))
        self.enterContext(patch.object(workflow, 'SHARDS', shards))
        self.enterContext(patch.object(workflow, 'COHORT_BOOTSTRAP_SPANS', {'9': (239, 240), '10': (244, 247)}))
        # The wider second mutant encloses mutant 9 and has an exclusion; use
        # a narrower independent synthetic plan for this cohort bootstrap.
        self.fixture = fixture

    def test_full_pair_merges_before_reconciliation(self):
        result, summary = self.fixture.invoke()
        self.assertEqual(0, result, summary['incomplete_reasons'])
        self.assertEqual(['9', '10'], summary['shards']['workload']['isolated_mutants'])
        counts = summary['targets']['workload']['status_counts']
        self.assertEqual((1, 1), (counts['Killed'], counts['Survived']))

    def test_missing_or_duplicate_physical_evidence_is_incomplete(self):
        folder = self.fixture.input / 'c5-mutation-12345-2-workload-b'
        shutil.rmtree(folder)
        result, summary = self.fixture.invoke()
        self.assertEqual(1, result)
        self.assertTrue(any('physical cohorts incomplete' in reason for reason in summary['incomplete_reasons']))

    def test_wrong_or_duplicate_assigned_ids_fail(self):
        folder = self.fixture.input / 'c5-mutation-12345-2-workload-b'
        receipt = json.loads((folder / 'receipt.json').read_text())
        receipt['isolated'][0]['mutant_id'] = '9'
        write_json(folder / 'receipt.json', receipt)
        result, summary = self.fixture.invoke()
        self.assertEqual(1, result)
        self.assertTrue(any('cohort IDs mismatch' in reason for reason in summary['incomplete_reasons']))

    def test_changed_native_geometry_fails(self):
        folder = self.fixture.input / 'c5-mutation-12345-2-workload-b'
        path = folder / workflow.REPORT_PATH
        report = json.loads(path.read_text())
        rows = report['files']['/synthetic/src/Parchwell.Verifier/' + WORKLOAD]['mutants']
        next(row for row in rows if row['id'] == '9')['replacement'] = 'different'
        write_json(path, report)
        isolated = folder / 'isolated/10' / workflow.REPORT_PATH
        write_json(isolated, report)
        receipt = json.loads((folder / 'receipt.json').read_text())
        receipt['report_sha256'] = receipt['isolated'][0]['report_sha256'] = digest(path)
        write_json(folder / 'receipt.json', receipt)
        result, summary = self.fixture.invoke()
        self.assertEqual(1, result)
        self.assertTrue(any('cohort native geometry mismatch' in reason for reason in summary['incomplete_reasons']))

    def test_unexpected_main_work_fails(self):
        folder = self.fixture.input / 'c5-mutation-12345-2-workload-b'
        path = folder / workflow.REPORT_PATH
        report = json.loads(path.read_text())
        rows = report['files']['/synthetic/src/Parchwell.Verifier/' + WORKLOAD]['mutants']
        next(row for row in rows if row['id'] == '9').update(status='Killed', statusReason=None)
        write_json(path, report)
        receipt = json.loads((folder / 'receipt.json').read_text())
        receipt['report_sha256'] = receipt['isolated'][0]['report_sha256'] = digest(path)
        write_json(folder / 'receipt.json', receipt)
        result, summary = self.fixture.invoke()
        self.assertEqual(1, result)
        self.assertTrue(any('unexpected main work' in reason for reason in summary['incomplete_reasons']))

    def test_cohort_argv_cannot_reference_another_invocation(self):
        folder = self.fixture.input / 'c5-mutation-12345-2-workload-b'
        path = folder / 'receipt.json'
        receipt = json.loads(path.read_text())
        receipt['argv'][3] = '/runner/.artifacts/c5-mutation/workload-b/isolated/9/config.json'
        receipt['isolated'][0]['argv'][3] = '/runner/.artifacts/c5-mutation/workload-b/isolated/9/config.json'
        write_json(path, receipt)
        result, summary = self.fixture.invoke()
        self.assertEqual(1, result)
        self.assertTrue(any('cohort argv paths mismatch' in reason for reason in summary['incomplete_reasons']))

    def test_extra_physical_artifact_is_incomplete(self):
        folder = self.fixture.input / 'c5-mutation-12345-2-workload-foreign'
        folder.mkdir()
        result, summary = self.fixture.invoke()
        self.assertEqual(1, result)
        self.assertTrue(any('unexpected shard directories' in reason for reason in summary['incomplete_reasons']))

    def test_duplicate_isolation_entry_is_incomplete(self):
        folder = self.fixture.input / 'c5-mutation-12345-2-workload-b'
        path = folder / 'receipt.json'
        receipt = json.loads(path.read_text())
        receipt['isolated'].append(receipt['isolated'][0])
        write_json(path, receipt)
        result, summary = self.fixture.invoke()
        self.assertEqual(1, result)
        self.assertTrue(any('isolated invocation list mismatch' in reason for reason in summary['incomplete_reasons']))

    def test_mixed_source_receipt_is_incomplete(self):
        folder = self.fixture.input / 'c5-mutation-12345-2-workload-b'
        path = folder / 'receipt.json'
        receipt = json.loads(path.read_text())
        source = next(iter(receipt['source_sha256_before']))
        receipt['source_sha256_before'][source] = 'f' * 64
        receipt['source_sha256_after'][source] = 'f' * 64
        write_json(path, receipt)
        result, summary = self.fixture.invoke()
        self.assertEqual(1, result)
        self.assertTrue(any('cross-shard source mismatch' in reason for reason in summary['incomplete_reasons']))

    def test_cohort_argv_rejects_extra_executable_options(self):
        folder = self.fixture.input / 'c5-mutation-12345-2-workload-b'
        path = folder / 'receipt.json'
        receipt = json.loads(path.read_text())
        receipt['argv'].insert(2, '--no-mutation')
        receipt['isolated'][0]['argv'].insert(2, '--no-mutation')
        write_json(path, receipt)
        result, summary = self.fixture.invoke()
        self.assertEqual(1, result)
        self.assertTrue(any('malformed cohort argv' in reason for reason in summary['incomplete_reasons']))

    def test_cohort_argv_rejects_another_executable(self):
        folder = self.fixture.input / 'c5-mutation-12345-2-workload-b'
        path = folder / 'receipt.json'
        receipt = json.loads(path.read_text())
        receipt['argv'][0] = '/tmp/fake-dotnet'
        receipt['isolated'][0]['argv'][0] = '/tmp/fake-dotnet'
        write_json(path, receipt)
        result, summary = self.fixture.invoke()
        self.assertEqual(1, result)
        self.assertTrue(any('malformed cohort argv' in reason for reason in summary['incomplete_reasons']))

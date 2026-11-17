"""Synthetic evidence tests for physical cohort aggregation."""
import hashlib
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
        # Bind this independently authored small inventory to its own synthetic source.
        sample = json.loads((source.with_name(source.name + '-a') / workflow.REPORT_PATH).read_text())
        file = sample['files']['/synthetic/src/Parchwell.Verifier/' + WORKLOAD]
        identities = {row['id']: {key: row.get(key) for key in (*workflow.IDENTITY, 'static')}
                      for row in file['mutants'] if row['id'] in ('9', '10')}
        keys = ('mutatorName', 'replacement', 'location', 'static')
        canonical = lambda value: json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=True)
        values = [{key: row.get(key) for key in keys} for row in file['mutants']]
        values.sort(key=canonical)
        inventory = hashlib.sha256(canonical(values).encode()).hexdigest()
        self.enterContext(patch.object(workflow, 'COHORT_IDENTITIES', identities))
        self.enterContext(patch.object(workflow, 'COHORT_INVENTORY_SHA256', inventory))
        self.enterContext(patch.object(workflow, 'COHORT_SOURCE_SHA256',
                                       hashlib.sha256(file['source'].encode()).hexdigest()))
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

    def use_bootstrap_token(self, folder, old, actual):
        shutil.move(folder / 'isolated' / old, folder / 'isolated' / ('bootstrap-' + old))
        path = folder / 'receipt.json'
        receipt = json.loads(path.read_text())
        entry = receipt['isolated'][0]
        entry['mutant_id'], entry['directory_id'] = actual, 'bootstrap-' + old
        entry['argv'] = [arg.replace('/isolated/' + old + '/', '/isolated/bootstrap-' + old + '/')
                         for arg in entry['argv']]
        receipt['argv'] = entry['argv']
        write_json(path, receipt)

    def test_unshifted_bootstrap_tokens_preserve_summary(self):
        for suffix, old in (('a', '9'), ('b', '10')):
            folder = self.fixture.input / ('c5-mutation-12345-2-workload-' + suffix)
            self.use_bootstrap_token(folder, old, old)
        result, summary = self.fixture.invoke()
        self.assertEqual(0, result, summary['incomplete_reasons'])
        self.assertEqual(['9', '10'], summary['shards']['workload']['isolated_mutants'])

    def test_shifted_native_ids_preserve_bootstrap_paths_and_summary(self):
        for suffix, old, actual in (('a', '9', '654'), ('b', '10', '655')):
            folder = self.fixture.input / ('c5-mutation-12345-2-workload-' + suffix)
            paths = [folder / workflow.REPORT_PATH,
                     folder / 'isolated' / old / workflow.REPORT_PATH]
            for path in paths:
                report = json.loads(path.read_text())
                rows = report['files']['/synthetic/src/Parchwell.Verifier/' + WORKLOAD]['mutants']
                for row in rows:
                    row['id'] = {'9': '654', '10': '655'}.get(row['id'], row['id'])
                write_json(path, report)
            receipt_path = folder / 'receipt.json'
            receipt = json.loads(receipt_path.read_text())
            receipt['report_sha256'] = receipt['isolated'][0]['report_sha256'] = digest(paths[0])
            write_json(receipt_path, receipt)
            self.use_bootstrap_token(folder, old, actual)
        result, summary = self.fixture.invoke()
        self.assertEqual(0, result, summary['incomplete_reasons'])
        self.assertEqual(['654', '655'], summary['shards']['workload']['isolated_mutants'])

    def test_invalid_bootstrap_directory_is_rejected(self):
        folder = self.fixture.input / 'c5-mutation-12345-2-workload-b'
        path = folder / 'receipt.json'
        receipt = json.loads(path.read_text())
        receipt['isolated'][0]['directory_id'] = '../9'
        write_json(path, receipt)
        result, summary = self.fixture.invoke()
        self.assertEqual(1, result)
        self.assertTrue(any('invalid bootstrap directory' in reason for reason in summary['incomplete_reasons']))

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

    def test_inconsistent_peer_baseline_is_rejected(self):
        folder = self.fixture.input / 'c5-mutation-12345-2-workload-b'
        paths = [folder / workflow.REPORT_PATH, folder / 'isolated/10' / workflow.REPORT_PATH]
        for path in paths:
            report = json.loads(path.read_text())
            rows = report['files']['/synthetic/src/Parchwell.Verifier/' + WORKLOAD]['mutants']
            next(row for row in rows if row['id'] == '9')['statusReason'] = 'different reason'
            write_json(path, report)
        receipt_path = folder / 'receipt.json'
        receipt = json.loads(receipt_path.read_text())
        receipt['report_sha256'] = receipt['isolated'][0]['report_sha256'] = digest(paths[0])
        write_json(receipt_path, receipt)
        result, summary = self.fixture.invoke()
        self.assertEqual(1, result)
        self.assertTrue(any('cohort native geometry mismatch' in reason for reason in summary['incomplete_reasons']))

    def test_isolated_log_hash_change_is_rejected(self):
        folder = self.fixture.input / 'c5-mutation-12345-2-workload-b'
        path = folder / 'isolated/10/command.log'
        path.write_text('different log')
        result, summary = self.fixture.invoke()
        self.assertEqual(1, result)
        self.assertTrue(any('isolated command log hash mismatch' in reason for reason in summary['incomplete_reasons']))

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

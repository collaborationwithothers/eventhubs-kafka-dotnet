"""Thirteen offline cases; downloaded tools only validate local synthetic inputs."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import yaml

ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / 'infra/scripts/lifecycle'


def embedded(name):
    source = (SCRIPTS / name).read_text().split("<<'PY'\n", 1)[1].split('\nPY\n', 1)[0]
    namespace = {'__name__': 'offline_test_module'}
    exec(compile(source, str(SCRIPTS / name), 'exec'), namespace)
    return namespace


INSTALLER = embedded('install-tools.sh')
CHECKER = embedded('check-offline.sh')


class ToolsOfflineTests(unittest.TestCase):
    def test_committed_scenario_items_map_passes_check_workflows(self):
        CHECKER['check_workflows'](ROOT)

    def scenario_workflow_fixture(self, mutation):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            shutil.copytree(ROOT / '.github/workflows', root / '.github/workflows')
            mirrors = root / 'docs/operations/workflows'
            mirrors.mkdir(parents=True)
            shutil.copy2(ROOT / 'docs/operations/workflows/sweeper.yml.txt', mirrors / 'sweeper.yml.txt')
            path = root / '.github/workflows/scenario.yml'
            document = CHECKER['load_yaml'](path.read_text())
            mutation(document)
            path.write_text(yaml.safe_dump(document, sort_keys=False))
            with self.assertRaisesRegex(ValueError, r'^scenario\.yml: unexpected workflow triggers$'):
                CHECKER['check_workflows'](root)

    def test_scenario_missing_items_fails_check_workflows(self):
        self.scenario_workflow_fixture(lambda document: document['on']['workflow_dispatch']['inputs'].pop('items'))

    def test_scenario_wrong_items_default_fails_check_workflows(self):
        self.scenario_workflow_fixture(lambda document: document['on']['workflow_dispatch']['inputs']['items'].update(default='C08,F04'))

    def test_scenario_extra_input_fails_check_workflows(self):
        self.scenario_workflow_fixture(lambda document: document['on']['workflow_dispatch']['inputs'].update(
            unexpected={'description': 'synthetic', 'type': 'string', 'required': 'false', 'default': 'synthetic'}))

    def test_scenario_extra_pull_request_trigger_fails_check_workflows(self):
        self.scenario_workflow_fixture(lambda document: document['on'].update(pull_request=''))

    def test_corrupted_download_never_reaches_extraction(self):
        with tempfile.TemporaryDirectory() as directory:
            artifact = Path(directory) / 'terraform.zip'

            def corrupt_download(helper, seconds, argv, output, limit=INSTALLER['LIMIT']):
                self.assertEqual(seconds, 120)
                self.assertIn('--max-filesize', argv)
                self.assertEqual(limit, 128 * 1024 * 1024)
                output.write_bytes(b'corrupted synthetic archive')

            with patch.dict(INSTALLER, run=corrupt_download):
                with self.assertRaisesRegex(ValueError, 'SHA-256 mismatch'):
                    INSTALLER['download'](Path('unused-helper'), 'https://synthetic.invalid/terraform.zip', INSTALLER['TOOLS'][0][3], Path(directory))
            self.assertEqual(artifact.read_bytes(), b'corrupted synthetic archive')

    def test_wrong_reported_version_fails_before_tool_is_published(self):
        with tempfile.TemporaryDirectory() as directory:
            tool = Path(directory) / 'synthetic-terraform'
            tool.write_text('#!/bin/sh\nprintf "Terraform v0.0.0\\n"\n')
            tool.chmod(0o700)
            output = Path(directory) / 'version.txt'
            INSTALLER['run'](SCRIPTS / 'command.py', 5, [tool, 'version'], output, 65536)
            with self.assertRaisesRegex(ValueError, 'pinned version 1.16.6'):
                INSTALLER['check_version'](INSTALLER['TOOLS'][0], output.read_text())

    def test_unsupported_platform_fails_without_download(self):
        with patch.object(INSTALLER['platform'], 'system', return_value='Darwin'):
            with self.assertRaisesRegex(ValueError, 'Linux x86_64 only'):
                INSTALLER['prerequisites']('live')

    def test_unsupported_python_abi_fails_without_download(self):
        with patch.object(INSTALLER['platform'], 'system', return_value='Linux'), patch.object(INSTALLER['platform'], 'machine', return_value='x86_64'), patch.object(INSTALLER['sys'], 'version_info', (3, 11, 0)):
            with self.assertRaisesRegex(ValueError, 'CPython 3.12'):
                INSTALLER['prerequisites']('ci')

    def test_queue_wrong_fails_structural_check(self):
        document = {'concurrency': {'group': 'parchwell-ephemeral', 'queue': 'wrong', 'cancel-in-progress': 'false'}}
        with self.assertRaisesRegex(ValueError, 'concurrency must be exactly'):
            CHECKER['check_queue'](document, 'cluster-smoke.yml')

    def test_missing_root_test_directory_fails_before_terraform(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'infra/persistent').mkdir(parents=True)
            with self.assertRaisesRegex(ValueError, 'root-local tests'):
                CHECKER['terraform_test_paths'](root)

    def test_actionlint_queue_exception_keeps_other_errors_visible(self):
        with tempfile.TemporaryDirectory() as directory:
            workflow = Path(directory) / 'synthetic.yml'
            workflow.write_text('''name: synthetic
on: workflow_dispatch
concurrency:
  group: parchwell-ephemeral
  queue: max
  cancel-in-progress: false
jobs:
  synthetic:
    runs-on: ubuntu-24.04
    malformed-field: true
    steps:
      - run: echo synthetic
''')
            diagnostic = '^unexpected key "queue" for "concurrency" section\\. expected one of "cancel-in-progress", "group"$'
            result = subprocess.run(['actionlint', '-ignore', diagnostic, str(workflow)], capture_output=True, text=True, timeout=30)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('malformed-field', result.stdout + result.stderr)
            self.assertNotIn('unexpected key "queue"', result.stdout + result.stderr)

    def test_strict_kubernetes_schema_rejects_unknown_field(self):
        with tempfile.TemporaryDirectory() as directory:
            manifest = Path(directory) / 'synthetic.yaml'
            manifest.write_text('''apiVersion: v1
kind: ConfigMap
metadata:
  name: synthetic
unknownSyntheticField: true
''')
            result = subprocess.run(['kubeconform', '-strict', '-summary', '-kubernetes-version', '1.35.0', str(manifest)], capture_output=True, text=True, timeout=60)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('unknownSyntheticField', result.stdout + result.stderr)
            self.assertIn('not allowed', result.stdout + result.stderr)


if __name__ == '__main__':
    unittest.main()

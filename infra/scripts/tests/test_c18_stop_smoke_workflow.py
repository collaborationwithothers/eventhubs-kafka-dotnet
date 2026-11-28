"""Offline checks for the manual C18 stop smoke workflow (plan c18-stop-smoke, requirements R1 to R3)."""
import os
from pathlib import Path
import subprocess
import unittest

import yaml

ROOT = Path(__file__).resolve().parents[3]
WORKFLOW = ROOT / '.github/workflows/c18-stop-smoke.yml'
COPY = ROOT / 'docs/operations/workflows/c18-stop-smoke.yml.txt'
SYNTHETIC_UUID = '00000000-0000-0000-0000-000000000001'
SMOKE_STEPS = ('guard checkout - tools - prepare login azure_session images cluster_init start cluster_plan cluster_apply '
               'ephemeral_init ephemeral_plan ephemeral_apply dns credentials namespace smoke_run collect upload_smoke '
               'delete_workload destroy report').split()
WORKLOAD_STEPS = ('images cluster_init start cluster_plan cluster_apply ephemeral_init ephemeral_plan ephemeral_apply dns '
                  'credentials namespace smoke_run collect upload_smoke delete_workload destroy')


def load():
    return yaml.load(WORKFLOW.read_text(), Loader=yaml.BaseLoader)


class GuardTests(unittest.TestCase):
    def test_guard_refuses_without_confirmation_or_main(self):
        steps = {step.get('id'): step for step in load()['jobs']['smoke']['steps'] if step.get('id')}
        script = steps['guard']['run']

        def run(ref, confirmed, identifier):
            environment = {'PATH': os.environ['PATH'], 'GITHUB_REF': ref, 'PARCHWELL_CONFIRM_STOP_SMOKE': confirmed,
                           'AZURE_CLIENT_ID': identifier, 'AZURE_TENANT_ID': identifier, 'AZURE_SUBSCRIPTION_ID': identifier}
            return subprocess.run(['bash', '-c', script], env=environment, capture_output=True).returncode

        self.assertNotEqual(0, run('refs/heads/main', 'false', SYNTHETIC_UUID))
        self.assertNotEqual(0, run('refs/heads/x', 'true', SYNTHETIC_UUID))
        self.assertEqual(0, run('refs/heads/main', 'true', SYNTHETIC_UUID))
        self.assertNotEqual(0, run('refs/heads/main', 'true', 'not-a-uuid'))


class ShapeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.document = load()
        cls.smoke = cls.document['jobs']['smoke']
        cls.stop = cls.document['jobs']['stop']
        cls.steps = {step.get('id'): step for step in cls.smoke['steps'] if step.get('id')}

    def test_trigger_jobs_and_steps(self):
        document = self.document
        self.assertEqual({'workflow_dispatch': {'inputs': {'confirm_stop_smoke': {
            'description': 'Confirm the C18 pod-deletion smoke, which applies and destroys the ephemeral environment',
            'type': 'boolean', 'required': 'true', 'default': 'false'}}}}, document['on'])
        self.assertEqual({'contents': 'read', 'id-token': 'write'}, document['permissions'])
        self.assertEqual({'group': 'parchwell-ephemeral', 'queue': 'max', 'cancel-in-progress': 'false'}, document['concurrency'])
        self.assertEqual(['smoke', 'stop'], list(document['jobs']))
        self.assertEqual(SMOKE_STEPS, [step.get('id', '-') for step in self.smoke['steps']])
        self.assertEqual(('parchwell-scenarios', 'parchwell-sweeper'), (self.smoke['environment'], self.stop['environment']))
        self.assertEqual(('360', '360'), (self.smoke['timeout-minutes'], self.stop['timeout-minutes']))
        self.assertEqual(['smoke'], self.stop['needs'])
        self.assertEqual('always()', self.stop['if'])
        environment = self.smoke['env']
        for key, value in {'PARCHWELL_SCENARIO': 'C03', 'PARCHWELL_PHASE': 'C0', 'PARCHWELL_CONFIGURATION': 'C0',
                           'PARCHWELL_RUN_KIND': 'smoke', 'PARCHWELL_PURPOSE': 'c18_stop_smoke',
                           'PARCHWELL_CONFIRM_STOP_SMOKE': "${{ inputs.confirm_stop_smoke && 'true' || 'false' }}"}.items():
            self.assertEqual(value, environment[key], key)
        self.assertEqual(('C03', 'C0'), (self.stop['env']['PARCHWELL_SCENARIO'], self.stop['env']['PARCHWELL_PHASE']))
        upload = self.steps['upload_smoke']
        self.assertEqual("always() && steps.prepare.outcome == 'success'", upload['if'])
        self.assertEqual('5', upload['timeout-minutes'])
        self.assertEqual({'name': 'c18-stop-smoke-${{ github.run_id }}-${{ github.run_attempt }}',
                          'path': '${{ env.PARCHWELL_ARTIFACT_DIRECTORY }}/c18-stop-smoke', 'retention-days': '90',
                          'if-no-files-found': 'error'}, upload['with'])
        self.assertEqual(WORKLOAD_STEPS, self.steps['report']['env']['WORKLOAD_STEPS'])
        for step, command in (('prepare', 'prepare-c18-stop-smoke'), ('smoke_run', 'run-c18-stop-smoke'), ('collect', 'collect-c18-stop-smoke')):
            self.assertEqual(f'bash infra/scripts/phase/phase.sh {command}', self.steps[step]['run'])
        self.assertEqual("always() && steps.prepare.outcome == 'success'", self.steps['collect']['if'])

    def test_no_pull_request_trigger_and_no_scientific_steps(self):
        self.assertNotIn('pull_request', WORKFLOW.read_text())
        commands = '\n'.join(step.get('run', '') for step in self.smoke['steps'])
        for forbidden in ('judge', 'run-repeats', 'candidate', 'archive', 'loadgen', 'verifier'):
            self.assertNotIn(forbidden, commands)

    def test_committed_copy_is_identical(self):
        self.assertEqual(WORKFLOW.read_bytes(), COPY.read_bytes())
        self.assertTrue(WORKFLOW.read_text().startswith(
            '# Installed at .github/workflows/c18-stop-smoke.yml; committed copy at docs/operations/workflows/c18-stop-smoke.yml.txt; '
            'the two are byte-identical (AGENTS.md hard safety rule 8).\n'))


if __name__ == '__main__':
    unittest.main()

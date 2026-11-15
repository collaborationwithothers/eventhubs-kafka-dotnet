"""Offline checks for the bounded manual archive smoke lifecycle."""
from pathlib import Path
import unittest

import yaml

ROOT = Path(__file__).resolve().parents[3]
WORKFLOW = ROOT / '.github/workflows/archive-smoke.yml'


class ArchiveSmokeWorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.document = yaml.load(WORKFLOW.read_text(), Loader=yaml.BaseLoader)
        cls.jobs = cls.document['jobs']
        cls.smoke = cls.jobs['smoke']
        cls.stop = cls.jobs['stop']
        cls.steps = {step.get('id'): step for step in cls.smoke['steps'] if step.get('id')}

    def test_manual_only_mirror_and_serialization(self):
        self.assertEqual({'workflow_dispatch': ''}, self.document['on'])
        self.assertEqual(WORKFLOW.read_bytes(), (ROOT / 'docs/operations/workflows/archive-smoke.yml.txt').read_bytes())
        self.assertEqual({'group': 'parchwell-ephemeral', 'queue': 'max', 'cancel-in-progress': 'false'}, self.document['concurrency'])
        self.assertEqual('true', self.smoke['env']['TF_VAR_archive_smoke_enabled'])
        self.assertEqual(('A01', 'probe'), (self.smoke['env']['PARCHWELL_SCENARIO'], self.smoke['env']['PARCHWELL_PHASE']))

    def test_guard_prepare_preflight_apply_and_probe_order(self):
        ids = [step.get('id') for step in self.smoke['steps']]
        ordered = ['guard', 'checkout', 'terraform', 'tools', 'prepare', 'login', 'azure_session', 'preflight', 'ephemeral_init', 'ephemeral_plan', 'ephemeral_apply', 'check_dns', 'probe', 'upload', 'destroy', 'report']
        self.assertEqual(ordered, ids)
        self.assertIn('refs/heads/main', self.steps['guard']['run'])
        self.assertIn('AZURE_SUBSCRIPTION_ID', self.steps['guard']['run'])
        self.assertEqual('${{ github.sha }}', self.steps['checkout']['with']['ref'])
        self.assertIn('S_PERSISTENT_OUTPUTS', self.steps['prepare']['env'])
        self.assertEqual('bash infra/scripts/phase/phase.sh prepare "$PARCHWELL_PHASE"', self.steps['prepare']['run'])
        self.assertEqual('python3 infra/scripts/archive/smoke.py preflight', self.steps['preflight']['run'])
        self.assertEqual('bash infra/scripts/phase/phase.sh check-dns', self.steps['check_dns']['run'])
        self.assertEqual(self.steps['ephemeral_apply'].get('if'), self.steps['check_dns'].get('if'))
        self.assertEqual('python3 infra/scripts/archive/smoke.py run', self.steps['probe']['run'])
        for name, command in [('ephemeral_init', 'init-ephemeral'), ('ephemeral_plan', 'plan-ephemeral'), ('ephemeral_apply', 'apply-ephemeral'), ('destroy', 'destroy-ephemeral')]:
            self.assertEqual('bash infra/scripts/lifecycle/lifecycle.sh ' + command, self.steps[name]['run'])
        commands = '\n'.join(step.get('run', '') for step in self.smoke['steps'])
        for forbidden in ('start-cluster', 'init-cluster', 'publish-images', 'run-repeats', 'credentials', 'reset-namespace'):
            self.assertNotIn(forbidden, commands)

    def test_cleanup_and_upload_are_bounded_and_reported(self):
        self.assertEqual("always() && steps.prepare.outcome == 'success' && steps.azure_session.outcome == 'success'", self.steps['destroy']['if'])
        self.assertEqual('always()', self.steps['upload']['if'])
        upload = self.steps['upload']['with']
        self.assertEqual('90', upload['retention-days'])
        self.assertEqual('error', upload['if-no-files-found'])
        self.assertEqual([f'${{{{ env.PARCHWELL_ARTIFACT_DIRECTORY }}}}/archive/{name}' for name in ('archive-smoke.json', 'archive-manifest.json', 'archive-receipt.json')], upload['path'].splitlines())
        report = self.steps['report']['env']
        self.assertEqual('true', report['WORKLOAD'])
        self.assertIn('destroy', report['WORKLOAD_STEPS'].split())
        self.assertIn('check_dns', report['WORKLOAD_STEPS'].split())
        self.assertIn('probe', report['WORKLOAD_STEPS'].split())
        self.assertIn('upload', report['WORKLOAD_STEPS'].split())
        self.assertEqual('always()', self.stop['if'])
        self.assertEqual(['smoke'], self.stop['needs'])
        self.assertEqual('parchwell-sweeper', self.stop['environment'])
        steps = {step.get('id'): step for step in self.stop['steps'] if step.get('id')}
        self.assertEqual("always() && steps.stop_prepare.outcome == 'success' && steps.azure_session.outcome == 'success'", steps['stop']['if'])
        self.assertIn('stop-cluster', steps['stop']['run'])
        self.assertIn('stop', steps['report']['env']['REQUIRED_STEPS'].split())


if __name__ == '__main__':
    unittest.main()

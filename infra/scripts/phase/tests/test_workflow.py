"""Offline workflow contracts for approved C6 orchestration."""
from pathlib import Path
import unittest

import yaml

REPO = Path(__file__).resolve().parents[4]


class UniqueLoader(yaml.BaseLoader):
    pass


def unique_mapping(loader, node):
    result = {}
    for key_node, value_node in node.value:
        key = loader.construct_object(key_node)
        if key in result:
            raise ValueError(f'duplicate YAML key: {key}')
        result[key] = loader.construct_object(value_node)
    return result


UniqueLoader.add_constructor(yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG, unique_mapping)


class WorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.text = (REPO / '.github/workflows/scenario.yml').read_text()
        cls.workflow = yaml.load(cls.text, Loader=UniqueLoader)
        cls.jobs = cls.workflow['jobs']
        cls.smoke = yaml.load((REPO / '.github/workflows/cluster-smoke.yml').read_text(), Loader=UniqueLoader)

    def test_mirror(self):
        self.assertEqual((REPO / '.github/workflows/scenario.yml').read_bytes(), (REPO / 'docs/operations/workflows/scenario.yml.txt').read_bytes())

    def test_top_level(self):
        self.assertEqual({'workflow_dispatch': ''}, self.workflow['on'])
        self.assertEqual({'contents': 'read', 'id-token': 'write'}, self.workflow['permissions'])
        self.assertEqual({'group': 'parchwell-ephemeral', 'queue': 'max', 'cancel-in-progress': 'false'}, self.workflow['concurrency'])

    def test_jobs(self):
        self.assertEqual(['probe', 'full-C0', 'full-C3'], list(self.jobs))
        for job in self.jobs.values():
            self.assertEqual('parchwell-scenarios', job['environment'])
            self.assertEqual('${{ vars.PARCHWELL_RUNNER_LABEL }}', job['runs-on'])
            self.assertEqual('360', job['timeout-minutes'])
            steps = job['steps']
            self.assertEqual('guard', steps[0]['id'])
            self.assertIn('test "$GITHUB_REF" = refs/heads/main', steps[0]['run'])
            self.assertIn('refs/heads/main', steps[0]['run'])
        self.assertNotIn('needs', self.jobs['probe'])
        self.assertEqual(['probe'], self.jobs['full-C0']['needs'])
        self.assertEqual("needs.probe.outputs.full_allowed == 'true'", self.jobs['full-C0']['if'])
        self.assertEqual(['probe', 'full-C0'], self.jobs['full-C3']['needs'])
        self.assertEqual('always()', self.jobs['full-C3']['if'])

    def test_login_then_session(self):
        smoke_steps = next(iter(self.smoke['jobs'].values()))['steps']
        login = next(step for step in smoke_steps if step.get('uses', '').startswith('azure/login@'))
        session = next(step for step in smoke_steps if step.get('id') == 'azure_session')
        for job in self.jobs.values():
            steps = job['steps']
            index = next(i for i, step in enumerate(steps) if step.get('uses', '').startswith('azure/login@'))
            actual = dict(steps[index])
            actual.pop('id', None)
            self.assertEqual(login, actual)
            self.assertEqual(session, steps[index + 1])

    def test_c3_predicates(self):
        steps = self.jobs['full-C3']['steps']
        self.assertEqual(['guard', 'checkout', 'allowed', 'stop_prepare', 'login', 'azure_session', 'tools', 'prepare', 'images', 'cluster_init', 'start', 'cluster_plan', 'cluster_apply', 'ephemeral_init', 'ephemeral_plan', 'ephemeral_apply', 'dns', 'credentials', 'namespace', 'repeats', 'judge', 'candidate', 'upload_candidate', 'upload_pods', 'delete_workload', 'destroy', 'stop', 'report'], [s['id'] for s in steps if 'id' in s])
        start = next(i for i, s in enumerate(steps) if s['name'] == 'Install the pinned Terraform')
        end = next(i for i, s in enumerate(steps) if s.get('id') == 'repeats')
        for step in steps[start:end + 1]:
            self.assertEqual("steps.allowed.outputs.workload == 'true'", step['if'])
        self.assertEqual('${{ needs.full-C0.result }}', next(s for s in steps if s.get('id') == 'allowed')['env']['FULL_C0_RESULT'])
        self.assertNotIn("needs['full-C0']", self.text)

    def test_single_stop(self):
        stops = [(name, step) for name, job in self.jobs.items() for step in job['steps'] if 'lifecycle.sh stop-cluster' in step.get('run', '')]
        self.assertEqual(1, len(stops))
        self.assertEqual('full-C3', stops[0][0])
        self.assertEqual("always() && steps.stop_prepare.outcome == 'success' && steps.azure_session.outcome == 'success'", stops[0][1]['if'])

    def test_no_obsolete_text(self):
        for forbidden in ('Parchwell.Runner', 'A01_OUTPUT', 'PARCHWELL_COST', 'READINESS', 'PARCHWELL_DEBUG_FAILURES', '|| true', 'continue-on-error'):
            self.assertNotIn(forbidden, self.text)

    def test_uploads(self):
        for name, job in self.jobs.items():
            uploads = [step for step in job['steps'] if step.get('uses', '').startswith('actions/upload-artifact@')]
            self.assertEqual(2, len(uploads))
            selected = 'probe' if name == 'probe' else name.removeprefix('full-')
            for step, kind, expected_path in [(uploads[0], 'candidate', '${{ env.PARCHWELL_CANDIDATE_DIRECTORY }}/candidate.json'), (uploads[1], 'pods', '${{ env.PARCHWELL_ARTIFACT_DIRECTORY }}')]:
                self.assertEqual(f'a01-{selected}-{kind}-${{{{ github.run_id }}}}-${{{{ github.run_attempt }}}}', step['with']['name'])
                self.assertEqual(expected_path, step['with']['path'])
                self.assertEqual('90', step['with']['retention-days'])
                self.assertEqual('5', step['timeout-minutes'])
                self.assertEqual('error', step['with']['if-no-files-found'])

    def test_cleanup_predicates(self):
        for job in self.jobs.values():
            steps = {step['id']: step for step in job['steps'] if 'id' in step}
            self.assertEqual("always() && steps.prepare.outcome == 'success' && steps.azure_session.outcome == 'success' && steps.credentials.outcome == 'success'", steps['delete_workload']['if'])
            self.assertEqual("always() && steps.prepare.outcome == 'success' && steps.azure_session.outcome == 'success'", steps['destroy']['if'])

    def test_bridge_discovery(self):
        suite = unittest.TestLoader().discover(str(REPO / 'infra/scripts/tests'), pattern='test_*.py')

        def flatten(tests):
            for test in tests:
                if isinstance(test, unittest.TestSuite):
                    yield from flatten(test)
                else:
                    yield test

        tests = list(flatten(suite))
        identifiers = [test.id() for test in tests]
        self.assertTrue(any(identifier.startswith('test_phase.') for identifier in identifiers), identifiers)
        self.assertTrue(any(identifier.startswith('test_workflow.') for identifier in identifiers), identifiers)
        self.assertFalse(any(isinstance(test, unittest.loader._FailedTest) for test in tests), identifiers)


if __name__ == '__main__':
    unittest.main()

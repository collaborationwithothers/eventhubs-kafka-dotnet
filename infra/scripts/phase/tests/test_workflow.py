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


class B04WorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.text = (REPO / '.github/workflows/b04-scenario.yml').read_text()
        cls.workflow = yaml.load(cls.text, Loader=UniqueLoader)
        cls.source = yaml.load((REPO / '.github/workflows/scenario.yml').read_text(), Loader=UniqueLoader)
        cls.smoke = yaml.load((REPO / '.github/workflows/cluster-smoke.yml').read_text(), Loader=UniqueLoader)
        cls.jobs = cls.workflow['jobs']

    def steps(self, job):
        return {step['id']: step for step in job['steps'] if 'id' in step}

    def test_mirror_and_top_level(self):
        self.assertEqual((REPO / '.github/workflows/b04-scenario.yml').read_bytes(),
                         (REPO / 'docs/operations/workflows/b04-scenario.yml.txt').read_bytes())
        self.assertEqual({'workflow_dispatch': ''}, self.workflow['on'])
        self.assertEqual(self.source['permissions'], self.workflow['permissions'])
        self.assertEqual({'group': 'parchwell-ephemeral', 'queue': 'max', 'cancel-in-progress': 'false'},
                         self.workflow['concurrency'])

    def test_job_selection_and_identity(self):
        self.assertEqual(['C0', 'C1'], list(self.jobs))
        for name, source_name, phase_name in (('C0', 'probe', 'C0'), ('C1', 'full-C3', 'C1')):
            job = self.jobs[name]
            source = self.source['jobs'][source_name]
            self.assertEqual(source['environment'], job['environment'])
            self.assertEqual(source['runs-on'], job['runs-on'])
            self.assertEqual(source['timeout-minutes'], job['timeout-minutes'])
            self.assertEqual('B04', job['env']['PARCHWELL_SCENARIO'])
            self.assertEqual(phase_name, job['env']['PARCHWELL_PHASE'])
        self.assertNotIn('needs', self.jobs['C0'])
        self.assertNotIn('outputs', self.jobs['C0'])
        self.assertEqual(['C0'], self.jobs['C1']['needs'])
        self.assertEqual('always()', self.jobs['C1']['if'])

    def test_main_guard_checkout_and_login(self):
        for name, source_name in (('C0', 'probe'), ('C1', 'full-C3')):
            steps = self.jobs[name]['steps']
            source_steps = self.source['jobs'][source_name]['steps']
            self.assertEqual(source_steps[:2], steps[:2])
            login_index = next(index for index, step in enumerate(steps)
                               if step.get('uses', '').startswith('azure/login@'))
            self.assertGreater(login_index, next(index for index, step in enumerate(steps)
                                                 if step.get('id') == 'guard'))
            source_login = next(step for step in source_steps if step.get('uses', '').startswith('azure/login@'))
            self.assertEqual(source_login, steps[login_index])
            source_session = next(step for step in source_steps if step.get('id') == 'azure_session')
            actual_session = next(step for step in steps if step.get('id') == 'azure_session')
            expected_session = dict(source_session)
            expected_session['run'] = expected_session['run'].replace('a01-oidc', 'b04-oidc')
            self.assertEqual(expected_session, actual_session)

    def test_smoke_lifecycle_and_artifacts(self):
        smoke_steps = self.steps(next(iter(self.smoke['jobs'].values())))
        for name in ('C0', 'C1'):
            steps = self.steps(self.jobs[name])
            for identifier in ('install', 'wait', 'collect', 'uninstall'):
                self.assertEqual(smoke_steps[identifier], steps[identifier])
            source_name = 'probe' if name == 'C0' else 'full-C3'
            source_destroy = self.steps(self.source['jobs'][source_name])['destroy']
            self.assertEqual(source_destroy['if'], steps['destroy']['if'])
            upload = steps['upload_pods']
            self.assertEqual(f'b04-{name}-smoke-${{{{ github.run_id }}}}-${{{{ github.run_attempt }}}}', upload['with']['name'])
            self.assertEqual('${{ env.PARCHWELL_ARTIFACT_DIRECTORY }}', upload['with']['path'])
            self.assertEqual('90', upload['with']['retention-days'])
            self.assertEqual('5', upload['timeout-minutes'])
            self.assertEqual('error', upload['with']['if-no-files-found'])

    def test_c1_dispatch_end_stop_and_reports(self):
        stops = [(name, step) for name, job in self.jobs.items() for step in job['steps']
                 if 'lifecycle.sh stop-cluster' in step.get('run', '')]
        self.assertEqual(1, len(stops))
        self.assertEqual('C1', stops[0][0])
        source = self.steps(self.source['jobs']['full-C3'])
        actual = self.steps(self.jobs['C1'])
        for field in ('if', 'run', 'env'):
            self.assertEqual(source['stop'][field], actual['stop'][field])
        self.assertEqual(source['stop_prepare'], actual['stop_prepare'])
        self.assertNotIn('steps.allowed', self.text)
        self.assertNotIn('needs.C0.outputs', self.text)
        expected_workload = 'tools prepare cluster_init start cluster_plan cluster_apply ephemeral_init ephemeral_plan ephemeral_apply dns credentials namespace install wait collect upload_pods uninstall destroy'
        for name, required in (('C0', 'guard checkout tools prepare azure_session'),
                               ('C1', 'guard checkout stop_prepare azure_session stop')):
            report = self.steps(self.jobs[name])['report']['env']
            self.assertEqual('true', report['WORKLOAD'])
            self.assertEqual(required, report['REQUIRED_STEPS'])
            self.assertEqual(expected_workload, report['WORKLOAD_STEPS'])

    def test_exact_step_order(self):
        self.assertEqual('guard checkout tools prepare login azure_session cluster_init start cluster_plan cluster_apply ephemeral_init ephemeral_plan ephemeral_apply dns credentials namespace install wait collect upload_pods uninstall destroy report'.split(),
                         [step['id'] for step in self.jobs['C0']['steps'] if 'id' in step])
        self.assertEqual('guard checkout stop_prepare login azure_session tools prepare cluster_init start cluster_plan cluster_apply ephemeral_init ephemeral_plan ephemeral_apply dns credentials namespace install wait collect upload_pods uninstall destroy stop report'.split(),
                         [step['id'] for step in self.jobs['C1']['steps'] if 'id' in step])

    def test_no_a01_or_worker_path_text(self):
        for forbidden in ('a01-', 'needs.probe', 'full-C0', 'full-C3', 'allow-c3', 'allow-images',
                          'publish-images', 'use-images', 'run-repeats', 'phase.sh judge',
                          'phase.sh candidate', 'setup-dotnet'):
            self.assertNotIn(forbidden, self.text)


if __name__ == '__main__':
    unittest.main()

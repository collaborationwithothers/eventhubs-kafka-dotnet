"""Offline contract tests for the shared scenario workflow and batch smoke."""
from pathlib import Path
import importlib.util
import hashlib
import json
import os
import re
import subprocess
import tempfile
import unittest

import yaml

REPO = Path(__file__).resolve().parents[4]
NATIVE_FACTOR = "(!inputs.test_native_authentication || (inputs.scenario == 'probe-batch-2' && inputs.run_kind == 'smoke' && inputs.items == 'authentication-controls' && inputs.capture_authentication_metadata && !inputs.test_handler_refresh_path))"


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
        cls.path = REPO / '.github/workflows/scenario.yml'
        cls.text = cls.path.read_text()
        cls.workflow = yaml.load(cls.text, Loader=UniqueLoader)
        cls.all_jobs = cls.workflow['jobs']
        cls.jobs = {name: cls.all_jobs[name] for name in ('probe', 'full-C0', 'full-C3')}
        cls.smoke = yaml.load((REPO / '.github/workflows/cluster-smoke.yml').read_text(), Loader=UniqueLoader)

    def steps(self, job):
        return {step['id']: step for step in job['steps'] if 'id' in step}

    def test_mirror_and_dispatch_contract(self):
        self.assertEqual(self.path.read_bytes(), (REPO / 'docs/operations/workflows/scenario.yml.txt').read_bytes())
        self.assertFalse((REPO / '.github/workflows/b04-scenario.yml').exists())
        self.assertFalse((REPO / 'docs/operations/workflows/b04-scenario.yml.txt').exists())
        self.assertEqual({'workflow_dispatch': {'inputs': {'scenario': {
            'description': 'Scenario definition to run', 'type': 'choice', 'required': 'true',
            'default': 'A01', 'options': ['A01', 'B04', 'B10', 'probe-batch', 'probe-batch-2', 'idle-observation', 'ledger-batch']},
            'run_kind': {'description': 'Probe batch run kind', 'type': 'choice', 'required': 'true',
                         'default': 'smoke', 'options': ['smoke', 'batch']},
            'items': {'description': 'Probe batch items (probe-batch: all or ordered B01,B03,B05,C08,C12,F04; probe-batch-2: smoke all or authentication-controls; batch all or ordered B08,E01,F06; idle-observation: all or ordered A02,A03,A04)',
                      'type': 'string', 'required': 'false', 'default': 'all'},
            'capture_authentication_metadata': {'description': 'Collect bounded authentication metadata (selected authentication controls only)',
                      'type': 'boolean', 'required': 'false', 'default': 'false'},
            'test_handler_refresh_path': {'description': 'Test our handler refresh path with a 5000 ms advertised expiry cap (selected authentication controls only)',
                      'type': 'boolean', 'required': 'false', 'default': 'false'},
            'test_native_authentication': {'description': 'Build three native librdkafka OIDC producers beside the custom-handler control (selected authentication controls only)',
                      'type': 'boolean', 'required': 'false', 'default': 'false'}}}}, self.workflow['on'])
        self.assertEqual({'contents': 'read', 'id-token': 'write'}, self.workflow['permissions'])
        self.assertEqual({'group': 'parchwell-ephemeral', 'queue': 'max', 'cancel-in-progress': 'false'}, self.workflow['concurrency'])
        phase_spec = importlib.util.spec_from_file_location('phase_for_workflow', REPO / 'infra/scripts/phase/phase.py')
        phase = importlib.util.module_from_spec(phase_spec)
        phase_spec.loader.exec_module(phase)
        self.assertEqual(
            ['A01', 'B04', 'B10', 'probe-batch', 'probe-batch-2', 'idle-observation', 'ledger-batch'],
            self.workflow['on']['workflow_dispatch']['inputs']['scenario']['options'])
        self.assertEqual(['A01', 'B04', 'B10'], sorted(phase.SCENARIOS))

    def test_job_topology_routing_and_main_guards(self):
        self.assertEqual(['probe', 'full-C0', 'full-C3'], list(self.jobs))
        self.assertNotIn('needs', self.jobs['probe'])
        self.assertEqual(['probe'], self.jobs['full-C0']['needs'])
        factor = "(!inputs.test_handler_refresh_path || (inputs.scenario == 'probe-batch-2' && inputs.run_kind == 'smoke' && inputs.items == 'authentication-controls' && inputs.capture_authentication_metadata))"
        self.assertEqual(f"(inputs.scenario != 'probe-batch' && inputs.scenario != 'probe-batch-2' && inputs.scenario != 'idle-observation' && inputs.scenario != 'ledger-batch' && inputs.scenario != 'B04' && inputs.scenario != 'B10' && needs.probe.outputs.full_allowed == 'true') && {factor} && " + NATIVE_FACTOR, self.jobs['full-C0']['if'])
        self.assertEqual(['probe', 'full-C0'], self.jobs['full-C3']['needs'])
        self.assertEqual(f"(always() && inputs.scenario != 'probe-batch' && inputs.scenario != 'probe-batch-2' && inputs.scenario != 'idle-observation' && inputs.scenario != 'ledger-batch') && {factor} && " + NATIVE_FACTOR, self.jobs['full-C3']['if'])
        self.assertEqual(f"(inputs.scenario != 'probe-batch' && inputs.scenario != 'probe-batch-2' && inputs.scenario != 'idle-observation' && inputs.scenario != 'ledger-batch') && {factor} && " + NATIVE_FACTOR, self.jobs['probe']['if'])
        expected_phases = {
            'probe': ("${{ (inputs.scenario == 'B04' || inputs.scenario == 'B10') && 'C0' || 'probe' }}", "${{ inputs.scenario || 'A01' }}"),
            'full-C0': ('C0', "${{ inputs.scenario || 'A01' }}"),
            'full-C3': ("${{ (inputs.scenario == 'B04' || inputs.scenario == 'B10') && 'C1' || 'C3' }}", "${{ inputs.scenario || 'A01' }}"),
        }
        for name, job in self.jobs.items():
            self.assertEqual('parchwell-scenarios', job['environment'])
            self.assertEqual('${{ vars.PARCHWELL_RUNNER_LABEL }}', job['runs-on'])
            self.assertEqual('360', job['timeout-minutes'])
            self.assertEqual(expected_phases[name][0], job['env']['PARCHWELL_PHASE'])
            self.assertEqual(expected_phases[name][1], job['env']['PARCHWELL_SCENARIO'])
            self.assertEqual("${{ inputs.test_handler_refresh_path && 'true' || 'false' }}", job['env']['PARCHWELL_TEST_HANDLER_REFRESH_PATH'])
            self.assertEqual("${{ inputs.test_native_authentication && 'true' || 'false' }}", job['env']['PARCHWELL_TEST_NATIVE_AUTHENTICATION'])
            steps = job['steps']
            self.assertEqual('guard', steps[0]['id'])
            self.assertIn('test "$GITHUB_REF" = refs/heads/main', steps[0]['run'])
            self.assertEqual({'name': 'Validate the selected scenario', 'id': 'select', 'shell': 'bash',
                              'run': 'python3 infra/scripts/phase/phase.py validate-scenario'}, steps[2])
            checkout = steps[1]
            self.assertEqual('actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1', checkout['uses'])
            self.assertEqual({'ref': '${{ github.sha }}', 'persist-credentials': 'false'}, checkout['with'])
            login = next(i for i, step in enumerate(steps) if step.get('uses', '').startswith('azure/login@'))
            self.assertLess(2, login)
            self.assertLess(next(i for i, step in enumerate(steps) if step.get('id') == 'select'), login)
            for private_step in ('prepare', 'stop_prepare'):
                matches = [i for i, step in enumerate(steps) if step.get('id') == private_step]
                for index in matches:
                    self.assertLess(2, index)
        smoke_steps = next(iter(self.smoke['jobs'].values()))['steps']
        smoke_login = next(step for step in smoke_steps if step.get('uses', '').startswith('azure/login@'))
        smoke_session = next(step for step in smoke_steps if step.get('id') == 'azure_session')
        for job in self.jobs.values():
            steps = job['steps']
            login_index = next(i for i, step in enumerate(steps) if step.get('uses', '').startswith('azure/login@'))
            actual_login = dict(steps[login_index]); actual_login.pop('id', None)
            self.assertEqual(smoke_login, actual_login)
            self.assertEqual(smoke_session, steps[login_index + 1])

    def test_b10_routes_c0_and_c1_without_the_a01_full_c0_job(self):
        # B10 reuses the B04 two-phase workflow while retaining A01's three jobs.
        for scenario, expected in (('A01', ['probe', 'full-C0', 'full-C3']),
                                   ('B04', ['probe', 'full-C3']),
                                   ('B10', ['probe', 'full-C3'])):
            selected = []
            for name, job in self.jobs.items():
                predicate = job['if'].replace('always()', 'True').replace('needs.probe.outputs.full_allowed', "'true'")
                for field in ('test_handler_refresh_path', 'test_native_authentication'):
                    predicate = predicate.replace('!inputs.' + field, 'not inputs.' + field)
                for field, value in {'scenario': scenario, 'run_kind': 'smoke', 'items': 'all',
                                     'capture_authentication_metadata': False, 'test_handler_refresh_path': False,
                                     'test_native_authentication': False}.items():
                    predicate = predicate.replace('inputs.' + field, repr(value))
                if eval(predicate.replace('&&', ' and ').replace('||', ' or '), {'__builtins__': {}}, {}):
                    selected.append(name)
            self.assertEqual(expected, selected)
        for name, phase in (('probe', 'C0'), ('full-C3', 'C1')):
            job = self.jobs[name]
            phase_expression = job['env']['PARCHWELL_PHASE'][3:-3].replace('inputs.scenario', repr('B10'))
            self.assertEqual(phase, eval(phase_expression.replace('&&', ' and ').replace('||', ' or '), {'__builtins__': {}}, {}))
            for step, suffix in (('upload_candidate', 'candidate'), ('upload_pods', 'pods')):
                self.assertIn(f"'b10-{phase}'", self.steps(job)[step]['with']['name'])
                self.assertIn(f"-{suffix}-${{{{ github.run_id }}}}-${{{{ github.run_attempt }}}}", self.steps(job)[step]['with']['name'])

    def test_refresh_path_admission_factors_preserve_batch_parentheses_and_cleanup(self):
        factor = "(!inputs.test_handler_refresh_path || (inputs.scenario == 'probe-batch-2' && inputs.run_kind == 'smoke' && inputs.items == 'authentication-controls' && inputs.capture_authentication_metadata))"
        batch = self.all_jobs['batch']
        self.assertEqual(f"(inputs.scenario == 'probe-batch' || inputs.scenario == 'probe-batch-2' || inputs.scenario == 'idle-observation' || inputs.scenario == 'ledger-batch') && {factor} && " + NATIVE_FACTOR, batch['if'])
        stop = self.all_jobs['batch-stop']
        expected_stop = "always() && ((inputs.scenario == 'probe-batch' && !inputs.capture_authentication_metadata) || (inputs.scenario == 'probe-batch-2' && ((inputs.run_kind == 'smoke' && ((inputs.items == 'all' && !inputs.capture_authentication_metadata) || (inputs.items == 'authentication-controls' && inputs.capture_authentication_metadata))) || (inputs.run_kind == 'batch' && !inputs.capture_authentication_metadata && contains(fromJSON('[\"all\",\"B08\",\"E01\",\"F06\",\"B08,E01\",\"B08,F06\",\"E01,F06\",\"B08,E01,F06\"]'), inputs.items)))) || (inputs.scenario == 'idle-observation' && (inputs.run_kind == 'smoke' || inputs.run_kind == 'batch') && contains(fromJSON('[\"all\",\"A02\",\"A03\",\"A04\",\"A02,A03\",\"A02,A04\",\"A03,A04\",\"A02,A03,A04\"]'), inputs.items) && !inputs.capture_authentication_metadata) || (inputs.scenario == 'ledger-batch' && (inputs.run_kind == 'smoke' || inputs.run_kind == 'batch') && inputs.items == 'all' && !inputs.capture_authentication_metadata))"
        self.assertEqual(f"({expected_stop}) && {factor} && " + NATIVE_FACTOR, stop['if'])
        self.assertIn('PARCHWELL_TEST_HANDLER_REFRESH_PATH', batch['env'])
        self.assertIn('PARCHWELL_TEST_NATIVE_AUTHENTICATION', batch['env'])
        steps = self.steps(batch)
        self.assertEqual("always() && steps.prepare.outcome == 'success' && steps.azure_session.outcome == 'success'", steps['destroy']['if'])
        self.assertEqual("always() && steps.prepare.outcome == 'success' && steps.azure_session.outcome == 'success' && steps.credentials.outcome == 'success'", steps['delete_workload']['if'])
        for job in self.all_jobs.values():
            if 'if' in job:
                self.assertIn(factor, job['if'])
                self.assertIn(NATIVE_FACTOR, job['if'])

    def test_refresh_path_job_predicates_admit_only_exact_tuple(self):
        def admitted(source, values):
            evaluated = source.replace('always()', 'True')
            for field in ('capture_authentication_metadata', 'test_handler_refresh_path', 'test_native_authentication'):
                evaluated = evaluated.replace('!inputs.' + field, 'not inputs.' + field)
            evaluated = evaluated.replace('&&', ' and ').replace('||', ' or ')
            for name, value in values.items():
                evaluated = evaluated.replace('inputs.' + name, repr(value))
            context = {
                'contains': lambda candidates, value: any(str(candidate).casefold() == str(value).casefold() for candidate in candidates),
                'fromJSON': json.loads,
            }
            return bool(eval(evaluated, {'__builtins__': {}}, context))

        valid = {'scenario': 'probe-batch-2', 'run_kind': 'smoke', 'items': 'authentication-controls',
                 'capture_authentication_metadata': True, 'test_handler_refresh_path': True,
                 'test_native_authentication': False}
        self.assertTrue(admitted(self.all_jobs['batch']['if'], valid))
        self.assertTrue(admitted(self.all_jobs['batch-stop']['if'], valid))
        for invalid in (
                {**valid, 'scenario': 'probe-batch'},
                {**valid, 'run_kind': 'batch'},
                {**valid, 'items': 'all'},
                {**valid, 'capture_authentication_metadata': False}):
            self.assertFalse(admitted(self.all_jobs['batch']['if'], invalid))
            self.assertFalse(admitted(self.all_jobs['batch-stop']['if'], invalid))
        legacy = {**valid, 'scenario': 'probe-batch', 'test_handler_refresh_path': False, 'test_native_authentication': False,
                  'capture_authentication_metadata': False}
        self.assertTrue(admitted(self.all_jobs['batch']['if'], legacy))
        native = {**valid, 'test_handler_refresh_path': False, 'test_native_authentication': True}
        self.assertTrue(admitted(self.all_jobs['batch']['if'], native))
        self.assertTrue(admitted(self.all_jobs['batch-stop']['if'], native))
        for invalid in (
                {**native, 'test_handler_refresh_path': True},
                {**native, 'capture_authentication_metadata': False},
                {**native, 'items': 'all'},
                {**native, 'run_kind': 'batch'},
                {**native, 'scenario': 'probe-batch'}):
            self.assertFalse(admitted(self.all_jobs['batch']['if'], invalid))
            self.assertFalse(admitted(self.all_jobs['batch-stop']['if'], invalid))

    def test_reports_and_cleanup(self):
        expected_workload_steps = {
            'probe': 'images cluster_init start cluster_plan cluster_apply ephemeral_init ephemeral_plan ephemeral_apply dns credentials namespace repeats judge candidate upload_candidate upload_pods delete_workload destroy',
            'full-C0': 'images cluster_init start cluster_plan cluster_apply ephemeral_init ephemeral_plan ephemeral_apply dns credentials namespace repeats judge candidate upload_candidate upload_pods delete_workload destroy',
            'full-C3': 'tools prepare images cluster_init start cluster_plan cluster_apply ephemeral_init ephemeral_plan ephemeral_apply dns credentials namespace repeats judge candidate upload_candidate upload_pods delete_workload destroy',
        }
        for name, job in self.jobs.items():
            steps = self.steps(job)
            self.assertIn('select', steps['report']['env']['REQUIRED_STEPS'])
            self.assertEqual(expected_workload_steps[name], steps['report']['env']['WORKLOAD_STEPS'])
            self.assertEqual("always() && steps.prepare.outcome == 'success' && steps.azure_session.outcome == 'success' && steps.credentials.outcome == 'success'", steps['delete_workload']['if'])
            self.assertEqual("always() && steps.prepare.outcome == 'success' && steps.azure_session.outcome == 'success'", steps['destroy']['if'])
            required = steps['report']['env']['REQUIRED_STEPS'].split()
            self.assertIn('select', required)
        final = self.steps(self.jobs['full-C3'])
        self.assertEqual('guard checkout select allowed stop_prepare login azure_session tools prepare images cluster_init start cluster_plan cluster_apply ephemeral_init ephemeral_plan ephemeral_apply dns credentials namespace repeats judge candidate upload_candidate upload_pods delete_workload destroy stop report'.split(),
                         [step['id'] for step in self.jobs['full-C3']['steps'] if 'id' in step])
        stops = [(name, step) for name, job in self.jobs.items() for step in job['steps'] if 'lifecycle.sh stop-cluster' in step.get('run', '')]
        self.assertEqual(1, len(stops))
        self.assertEqual('full-C3', stops[0][0])
        self.assertEqual("always() && steps.stop_prepare.outcome == 'success' && steps.azure_session.outcome == 'success'", stops[0][1]['if'])
        self.assertEqual('${{ needs.probe.outputs.loadgen_digest }}', final['allowed']['env']['LOADGEN_DIGEST'])
        self.assertEqual('${{ needs.probe.outputs.verifier_digest }}', final['allowed']['env']['VERIFIER_DIGEST'])
        self.assertEqual('${{ needs.full-C0.result }}', final['allowed']['env']['FULL_C0_RESULT'])
        self.assertNotIn("needs['full-C0']", self.text)
        self.assertIn('prepare-stop', final['stop_prepare']['run'])
        self.assertEqual('${{ steps.stop_prepare.outputs.private_directory }}', final['stop']['env']['STOP_PRIVATE_DIRECTORY'])
        self.assertEqual('true', self.steps(self.jobs['full-C0'])['report']['env']['WORKLOAD'])
        self.assertEqual('guard checkout select tools prepare azure_session',
                         self.steps(self.jobs['probe'])['report']['env']['REQUIRED_STEPS'])
        self.assertEqual('guard checkout select tools prepare azure_session',
                         self.steps(self.jobs['full-C0'])['report']['env']['REQUIRED_STEPS'])
        self.assertEqual('guard checkout select allowed stop_prepare azure_session stop',
                         final['report']['env']['REQUIRED_STEPS'])

    def test_final_workload_predicates_and_exact_job_order(self):
        expected_ids = {
            'probe': 'guard checkout select tools prepare login azure_session images cluster_init start cluster_plan cluster_apply ephemeral_init ephemeral_plan ephemeral_apply dns credentials namespace repeats judge candidate upload_candidate upload_pods delete_workload destroy report'.split(),
            'full-C0': 'guard checkout select tools prepare login azure_session images cluster_init start cluster_plan cluster_apply ephemeral_init ephemeral_plan ephemeral_apply dns credentials namespace repeats judge candidate upload_candidate upload_pods delete_workload destroy report'.split(),
            'full-C3': 'guard checkout select allowed stop_prepare login azure_session tools prepare images cluster_init start cluster_plan cluster_apply ephemeral_init ephemeral_plan ephemeral_apply dns credentials namespace repeats judge candidate upload_candidate upload_pods delete_workload destroy stop report'.split(),
        }
        for name, job in self.jobs.items():
            self.assertEqual(expected_ids[name], [step['id'] for step in job['steps'] if 'id' in step])
        final_steps = self.jobs['full-C3']['steps']
        start = next(i for i, step in enumerate(final_steps) if step.get('name') == 'Install the pinned Terraform')
        end = next(i for i, step in enumerate(final_steps) if step.get('id') == 'repeats')
        for step in final_steps[start:end + 1]:
            self.assertEqual("steps.allowed.outputs.workload == 'true'", step['if'])

    def test_artifacts_and_image_reuse(self):
        expected_prefixes = {
            'probe': "${{ inputs.scenario == 'B04' && 'b04-C0' || inputs.scenario == 'B10' && 'b10-C0' || 'a01-probe' }}",
            'full-C0': 'a01-C0',
            'full-C3': "${{ inputs.scenario == 'B04' && 'b04-C1' || inputs.scenario == 'B10' && 'b10-C1' || 'a01-C3' }}",
        }
        for name, job in self.jobs.items():
            uploads = [step for step in job['steps'] if step.get('uses', '').startswith('actions/upload-artifact@')]
            self.assertEqual(2, len(uploads))
            for step, kind, path in ((uploads[0], 'candidate', '${{ env.PARCHWELL_CANDIDATE_DIRECTORY }}/candidate.json'),
                                     (uploads[1], 'pods', '${{ env.PARCHWELL_ARTIFACT_DIRECTORY }}')):
                prefix = expected_prefixes[name]
                expected = f'{prefix}-{kind}-${{{{ github.run_id }}}}-${{{{ github.run_attempt }}}}'
                self.assertEqual(expected, step['with']['name'])
                self.assertEqual(path, step['with']['path'])
                self.assertEqual('90', step['with']['retention-days'])
                self.assertEqual('5', step['timeout-minutes'])
                self.assertEqual('error', step['with']['if-no-files-found'])
        self.assertIn('publish-images', self.steps(self.jobs['probe'])['images']['run'])
        selector = self.steps(self.jobs['full-C3'])['allowed']
        self.assertEqual('Decide whether the final phase workload runs', selector['name'])
        self.assertEqual('''case "$PARCHWELL_SCENARIO" in
  A01) bash infra/scripts/phase/phase.sh allow-c3 ;;
  B04|B10) bash infra/scripts/phase/phase.sh allow-images ;;
  *) exit 2 ;;
esac
''', selector['run'])
        self.assertEqual('Use the initial job\'s immutable worker images', self.steps(self.jobs['full-C3'])['images']['name'])
        self.assertIn('use-images', self.steps(self.jobs['full-C3'])['images']['run'])
        self.assertNotIn('A01_OUTPUT', self.text)

    def test_no_obsolete_text(self):
        for forbidden in ('Parchwell.Runner', 'A01_OUTPUT', 'PARCHWELL_COST', 'READINESS',
                          'PARCHWELL_DEBUG_FAILURES', '|| true', 'continue-on-error'):
            self.assertNotIn(forbidden, self.text)

    def test_selector_shell_paths_with_stubbed_commands(self):
        allowed = self.steps(self.jobs['full-C3'])['allowed']['run']
        for scenario, expected in (('A01', 'infra/scripts/phase/phase.sh allow-c3'),
                                   ('B04', 'infra/scripts/phase/phase.sh allow-images'),
                                   ('B10', 'infra/scripts/phase/phase.sh allow-images')):
            with self.subTest(scenario=scenario), tempfile.TemporaryDirectory() as directory:
                root = Path(directory); stub = root / 'bash'; record = root / 'args'; output = root / 'github-output'
                stub.write_text('#!/bin/sh\nprintf "%s\\n" "$*" > "$SELECTOR_RECORD"\nprintf "workload=true\\n" >> "$GITHUB_OUTPUT"\n')
                stub.chmod(0o755); output.touch()
                env = dict(os.environ, PATH=f'{root}:{os.environ.get("PATH", "")}', SELECTOR_RECORD=str(record),
                           GITHUB_OUTPUT=str(output), PARCHWELL_SCENARIO=scenario)
                run = subprocess.run(['/bin/bash', '-c', allowed], cwd=REPO, env=env, text=True, capture_output=True)
                self.assertEqual(0, run.returncode, run.stderr)
                self.assertEqual(expected, record.read_text().strip())
                self.assertEqual('workload=true\n', output.read_text())
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); stub = root / 'bash'; record = root / 'args'; output = root / 'github-output'
            stub.write_text('#!/bin/sh\nprintf "%s\\n" "$*" > "$SELECTOR_RECORD"\n'); stub.chmod(0o755); output.touch()
            env = dict(os.environ, PATH=f'{root}:{os.environ.get("PATH", "")}', SELECTOR_RECORD=str(record),
                       GITHUB_OUTPUT=str(output), PARCHWELL_SCENARIO='A02')
            run = subprocess.run(['/bin/bash', '-c', allowed], cwd=REPO, env=env, text=True, capture_output=True)
            self.assertEqual(2, run.returncode)
            self.assertFalse(record.exists())
            self.assertEqual('', output.read_text())

    def test_batch_topology_and_exact_steps(self):
        self.assertEqual(['probe', 'full-C0', 'full-C3', 'batch', 'batch-stop'], list(self.all_jobs))
        batch = self.all_jobs['batch']
        stop = self.all_jobs['batch-stop']
        factor = "(!inputs.test_handler_refresh_path || (inputs.scenario == 'probe-batch-2' && inputs.run_kind == 'smoke' && inputs.items == 'authentication-controls' && inputs.capture_authentication_metadata))"
        self.assertEqual(f"(inputs.scenario == 'probe-batch' || inputs.scenario == 'probe-batch-2' || inputs.scenario == 'idle-observation' || inputs.scenario == 'ledger-batch') && {factor} && " + NATIVE_FACTOR, batch['if'])
        self.assertNotIn('needs', batch)
        self.assertEqual(['batch'], stop['needs'])
        expected_stop = "always() && ((inputs.scenario == 'probe-batch' && !inputs.capture_authentication_metadata) || (inputs.scenario == 'probe-batch-2' && ((inputs.run_kind == 'smoke' && ((inputs.items == 'all' && !inputs.capture_authentication_metadata) || (inputs.items == 'authentication-controls' && inputs.capture_authentication_metadata))) || (inputs.run_kind == 'batch' && !inputs.capture_authentication_metadata && contains(fromJSON('[\"all\",\"B08\",\"E01\",\"F06\",\"B08,E01\",\"B08,F06\",\"E01,F06\",\"B08,E01,F06\"]'), inputs.items)))) || (inputs.scenario == 'idle-observation' && (inputs.run_kind == 'smoke' || inputs.run_kind == 'batch') && contains(fromJSON('[\"all\",\"A02\",\"A03\",\"A04\",\"A02,A03\",\"A02,A04\",\"A03,A04\",\"A02,A03,A04\"]'), inputs.items) && !inputs.capture_authentication_metadata) || (inputs.scenario == 'ledger-batch' && (inputs.run_kind == 'smoke' || inputs.run_kind == 'batch') && inputs.items == 'all' && !inputs.capture_authentication_metadata))"
        self.assertEqual(f"({expected_stop}) && {factor} && " + NATIVE_FACTOR, stop['if'])
        expected_ids = {
            'batch': 'guard checkout select tools prepare login azure_session images cluster_init start cluster_plan cluster_apply ephemeral_init ephemeral_plan ephemeral_apply dns credentials namespace seed batch upload_candidates upload_pods delete_workload destroy report'.split(),
            'batch-stop': 'guard checkout stop_prepare login azure_session stop report'.split(),
        }
        # The stop-only job uses the no-reviewer main-only environment so the
        # dispatch-end AKS stop does not wait for a second approval.
        expected_environments = {'batch': 'parchwell-scenarios', 'batch-stop': 'parchwell-sweeper'}
        self.assertEqual(['batch-stop'], [name for name, job in self.all_jobs.items()
                                          if job['environment'] == 'parchwell-sweeper'])
        # That environment's secrets are reachable without a human gate, so the
        # stop job may reference only the three Azure identifiers that
        # azure/login needs (issue 118), and no other secret.
        allowed_secrets = r'secrets\.AZURE_(?:CLIENT|TENANT|SUBSCRIPTION)_ID\b'
        self.assertNotRegex(re.sub(allowed_secrets, '', json.dumps(stop)), r'\bsecrets\b')
        for name in expected_ids:
            job = self.all_jobs[name]
            self.assertEqual(expected_ids[name], [step['id'] for step in job['steps'] if 'id' in step])
            self.assertEqual('${{ vars.PARCHWELL_RUNNER_LABEL }}', job['runs-on'])
            self.assertEqual(expected_environments[name], job['environment'])
            self.assertEqual('360', job['timeout-minutes'])
            self.assertEqual('${{ inputs.scenario }}', job['env']['PARCHWELL_SCENARIO'])
            self.assertEqual('single-action', job['env']['PARCHWELL_PHASE'])
            self.assertEqual('${{ inputs.run_kind }}', job['env']['PARCHWELL_RUN_KIND'])
            self.assertEqual('${{ inputs.items }}', job['env']['PARCHWELL_BATCH_ITEMS'])
            self.assertEqual("${{ inputs.capture_authentication_metadata && 'true' || 'false' }}", job['env']['PARCHWELL_CAPTURE_AUTHENTICATION_METADATA'])
            steps = self.steps(job)
            for ident in ('guard', 'checkout', 'login', 'azure_session'):
                self.assertEqual(self.steps(self.jobs['probe'])[ident], steps[ident])
            self.assertEqual('always()', steps['report']['if'])
            self.assertEqual('${{ toJSON(steps) }}', steps['report']['env']['STEP_RESULTS'])
            self.assertEqual('${{ job.status }}', steps['report']['env']['JOB_STATUS'])
        steps = self.steps(batch)
        self.assertEqual('python3 infra/scripts/phase/phase.py validate-batch', steps['select']['run'])
        self.assertLess(expected_ids['batch'].index('select'), expected_ids['batch'].index('login'))
        self.assertEqual('bash infra/scripts/phase/phase.sh prepare-batch', steps['prepare']['run'])
        self.assertEqual('bash infra/scripts/phase/phase.sh single-action-seed', steps['seed']['run'])
        self.assertNotIn('if', steps['seed'])
        self.assertEqual('bash infra/scripts/phase/phase.sh run-single-action-batch', steps['batch']['run'])
        self.assertNotIn('if', steps['batch'])
        self.assertIn('publish-images', steps['images']['run'])
        for ident in ('tools', 'images', 'cluster_init', 'start', 'cluster_plan', 'cluster_apply',
                      'ephemeral_init', 'ephemeral_plan', 'ephemeral_apply', 'dns', 'credentials',
                      'namespace', 'delete_workload', 'destroy'):
            self.assertEqual(self.steps(self.jobs['probe'])[ident], steps[ident])
        self.assertEqual('true', steps['report']['env']['WORKLOAD'])
        self.assertEqual('guard checkout select tools prepare azure_session', steps['report']['env']['REQUIRED_STEPS'])
        self.assertEqual('images cluster_init start cluster_plan cluster_apply ephemeral_init ephemeral_plan ephemeral_apply dns credentials namespace seed batch upload_candidates upload_pods delete_workload destroy', steps['report']['env']['WORKLOAD_STEPS'])
        stop_steps = self.steps(stop)
        for ident in ('stop_prepare', 'stop'):
            self.assertEqual(self.steps(self.jobs['full-C3'])[ident], stop_steps[ident])
        self.assertEqual('false', stop_steps['report']['env']['WORKLOAD'])
        self.assertEqual('guard checkout stop_prepare azure_session stop', stop_steps['report']['env']['REQUIRED_STEPS'])
        self.assertEqual('', stop_steps['report']['env']['WORKLOAD_STEPS'])
        self.assertEqual(2, sum('lifecycle.sh stop-cluster' in step.get('run', '')
                               for job in self.all_jobs.values() for step in job['steps']))

    def test_batch_upload_includes_candidates_receipts_and_public_pods(self):
        uploads = [step for step in self.all_jobs['batch']['steps']
                   if step.get('uses', '').startswith('actions/upload-artifact@')]
        self.assertEqual(2, len(uploads))
        candidates = uploads[0]
        self.assertEqual('upload_candidates', candidates['id'])
        self.assertEqual("always() && steps.prepare.outcome == 'success'", candidates['if'])
        self.assertEqual({'name': "${{ (inputs.scenario == 'probe-batch' || inputs.scenario == 'probe-batch-2') && 'probe-batch' || inputs.scenario }}-candidates-${{ github.run_id }}-${{ github.run_attempt }}",
                          'path': '${{ env.PARCHWELL_CANDIDATE_DIRECTORY }}', 'retention-days': '90',
                          'if-no-files-found': 'error'}, candidates['with'])
        upload = uploads[1]
        self.assertEqual('upload_pods', upload['id'])
        self.assertEqual("always() && steps.prepare.outcome == 'success'", upload['if'])
        self.assertEqual("${{ (inputs.scenario == 'probe-batch' || inputs.scenario == 'probe-batch-2') && 'probe-batch' || inputs.scenario }}-pods-${{ github.run_id }}-${{ github.run_attempt }}", upload['with']['name'])
        self.assertEqual('${{ env.PARCHWELL_ARTIFACT_DIRECTORY }}', upload['with']['path'])
        self.assertEqual('90', upload['with']['retention-days'])
        self.assertEqual('5', upload['timeout-minutes'])
        self.assertEqual('error', upload['with']['if-no-files-found'])
        self.assertFalse(any(step.get('uses', '').startswith('actions/upload-artifact@')
                             for step in self.all_jobs['batch-stop']['steps']))

    def test_batch_selector_shell_accepts_smoke_and_batch_before_login(self):
        select = self.steps(self.all_jobs['batch'])['select']['run']
        for kind, expected in (('smoke', 0), ('batch', 0), ('unknown', 2)):
            with self.subTest(kind=kind):
                env = dict(os.environ, PARCHWELL_SCENARIO='probe-batch', PARCHWELL_RUN_KIND=kind, PARCHWELL_BATCH_ITEMS='all')
                result = subprocess.run(['/bin/bash', '-c', select], cwd=REPO, env=env,
                                        text=True, capture_output=True, timeout=30)
                self.assertEqual(expected, result.returncode, result.stdout + result.stderr)

    def test_batch_selector_shell_validates_items_before_login(self):
        select = self.steps(self.all_jobs['batch'])['select']['run']
        for items, expected in [('all', 0), ('C08,F04', 0), ('F04', 0), ('C08', 0),
                                ('F04,C08', 2), ('C08,C08', 2), ('E01', 2), ('', 2), (None, 0)]:
            with self.subTest(items=items):
                env = dict(os.environ, PARCHWELL_SCENARIO='probe-batch', PARCHWELL_RUN_KIND='batch',
                           PARCHWELL_BATCH_ITEMS=items or '')
                if items is None:
                    env.pop('PARCHWELL_BATCH_ITEMS', None)
                result = subprocess.run(['/bin/bash', '-c', select], cwd=REPO, env=env,
                                        text=True, capture_output=True, timeout=30)
                self.assertEqual(expected, result.returncode, result.stdout + result.stderr)
                if expected:
                    self.assertEqual('unsupported probe batch selection', result.stderr.strip())

    def test_v2_selector_admits_batches_and_rejects_partial_smokes_before_login(self):
        select = self.steps(self.all_jobs['batch'])['select']['run']
        for kind, items, expected in [('smoke', 'all', 0), ('batch', 'all', 0), ('smoke', 'B08', 2), ('smoke', 'E01,F06', 2), ('smoke', 'F04', 2), ('smoke', '', 2)]:
            with self.subTest(kind=kind, items=items):
                env = dict(os.environ, PARCHWELL_SCENARIO='probe-batch-2', PARCHWELL_RUN_KIND=kind, PARCHWELL_BATCH_ITEMS=items)
                result = subprocess.run(['/bin/bash', '-c', select], cwd=REPO, env=env, text=True, capture_output=True, timeout=30)
                self.assertEqual(expected, result.returncode, result.stdout + result.stderr)
                if expected:
                    self.assertEqual('unsupported probe batch selection', result.stderr.strip())
        prepare = self.steps(self.all_jobs['batch'])['prepare']
        self.assertEqual("${{ inputs.scenario == 'probe-batch-2' && inputs.run_kind == 'smoke' && secrets.PARCHWELL_PROBE_V2_EFFECTIVE_SCOPE || '' }}", prepare['env']['S_PROBE_V2_EFFECTIVE_SCOPE'])
        locations = [(name, step.get('id')) for name, job in self.all_jobs.items() for step in job['steps']
                     if 'S_PROBE_V2_EFFECTIVE_SCOPE' in step.get('env', {})]
        self.assertEqual([('batch', 'prepare')], locations)
        expression = self.all_jobs['batch-stop']['if']
        for selected, kind, items, capture, expected in [
                ('probe-batch-2', 'smoke', 'all', False, True), ('probe-batch-2', 'smoke', 'all', True, False),
                ('probe-batch-2', 'batch', 'all', False, True), ('probe-batch-2', 'batch', 'B08', False, True),
                ('probe-batch-2', 'batch', 'E01,F06', False, True), ('probe-batch-2', 'batch', 'F06,B08', False, False),
                ('probe-batch-2', 'batch', 'all', True, False), ('probe-batch-2', 'smoke', '', False, False),
                ('probe-batch-2', 'smoke', 'B08', False, False), ('probe-batch-2', 'smoke', 'authentication-controls', True, True),
                ('probe-batch-2', 'smoke', 'authentication-controls', False, False), ('probe-batch', 'batch', 'F04', False, True),
                ('probe-batch', 'batch', 'F04', True, False), ('A01', 'smoke', 'all', False, False),
                ('probe-batch-2', 'batch', 'b08', False, True), ('probe-batch-2', 'Batch', 'B08', False, True),
                ('probe-batch-2', 'smoke', 'ALL', False, True), ('idle-observation', 'smoke', 'all', False, True),
                ('idle-observation', 'batch', 'all', False, True), ('idle-observation', 'smoke', 'A02', False, True),
                ('idle-observation', 'batch', 'A02,A04', False, True), ('idle-observation', 'batch', 'A04,A02', False, False),
                ('idle-observation', 'batch', 'A02,A02', False, False), ('idle-observation', 'batch', '', False, False),
                ('idle-observation', 'smoke', 'all', True, False)]:
            evaluated = expression.replace('always()', 'True').replace('&&', ' and ').replace('||', ' or ')
            evaluated = evaluated.replace('!inputs.capture_authentication_metadata', 'not capture').replace('&&', ' and ').replace('||', ' or ')
            evaluated = evaluated.replace('!inputs.test_handler_refresh_path', 'not refresh_test')
            evaluated = evaluated.replace('!inputs.test_native_authentication', 'not native_test')
            for name, value in [('scenario', selected), ('run_kind', kind), ('items', items)]:
                evaluated = evaluated.replace('inputs.' + name, repr(value.casefold()))
            evaluated = evaluated.replace('inputs.capture_authentication_metadata', 'capture')
            evaluated = evaluated.replace('inputs.test_handler_refresh_path', 'refresh_test')
            evaluated = evaluated.replace('inputs.test_native_authentication', 'native_test')
            self.assertEqual(expected, eval(evaluated, {'__builtins__': {}, 'contains': lambda values, value: any(str(v).casefold() == str(value).casefold() for v in values), 'fromJSON': json.loads}, {'capture': capture, 'refresh_test': False, 'native_test': False}), (selected, kind, items, capture))
        stop_steps = self.steps(self.all_jobs['batch-stop'])
        self.assertIn('prepare-stop', stop_steps['stop_prepare']['run'])
        self.assertLess(list(stop_steps).index('stop_prepare'), list(stop_steps).index('login'))
        self.assertIsNone(stop_steps['login'].get('if'))
        self.assertIn("steps.stop_prepare.outcome == 'success'", stop_steps['stop']['if'])
        self.assertEqual(['A01', 'B04', 'B10', 'probe-batch'], self.workflow['on']['workflow_dispatch']['inputs']['scenario']['options'][:4])

    def test_capture_validation_and_actual_stop_prerequisites(self):
        def expression(source, inputs, outcomes):
            evaluated = source.replace('always()', 'True')
            for field in ('capture_authentication_metadata', 'test_handler_refresh_path', 'test_native_authentication'):
                evaluated = evaluated.replace('!inputs.' + field, 'not inputs.' + field)
            evaluated = evaluated.replace('&&', ' and ').replace('||', ' or ')
            for name, value in inputs.items():
                evaluated = evaluated.replace('inputs.' + name, repr(value))
            for name, value in outcomes.items():
                evaluated = evaluated.replace('steps.' + name + '.outcome', repr(value))
            return bool(eval(evaluated, {'__builtins__': {}, 'fromJSON': json.loads,
                                         'contains': lambda values, value: any(str(v).casefold() == str(value).casefold() for v in values)}, {}))

        def simulate_cleanup(job, inputs, initial_outcomes=None, forced_results=None):
            outcomes = dict(initial_outcomes or {})
            forced_results = dict(forced_results or {})
            invocations = []
            for step in job['steps']:
                identifier = step.get('id')
                if identifier not in ('stop_prepare', 'login', 'azure_session', 'stop'):
                    continue
                condition = step.get('if')
                admitted = (expression(condition, inputs, outcomes) if condition else
                            all(outcome == 'success' for outcome in outcomes.values()))
                if admitted:
                    # The workflow declaration is the invocation source; commands/actions stay stubbed offline.
                    invocations.append((identifier, step.get('run'), step.get('uses')))
                    outcomes[identifier] = forced_results.get(identifier, 'success')
                else:
                    outcomes[identifier] = 'skipped'
            return invocations, outcomes

        select = self.steps(self.all_jobs['batch'])['select']['run']
        valid = [('probe-batch', 'batch', 'all', 'false'),
                 ('probe-batch-2', 'smoke', 'all', 'false'),
                 ('probe-batch-2', 'smoke', 'authentication-controls', 'true'),
                 ('idle-observation', 'smoke', 'all', 'false'),
                 ('idle-observation', 'batch', 'all', 'false'),
                 ('idle-observation', 'batch', 'A02,A04', 'false'),
                 ('idle-observation', 'smoke', 'A03', 'false')]
        rejected = [('probe-batch', 'smoke', 'all', 'true'),
                    ('probe-batch-2', 'smoke', 'all', 'true'),
                    ('probe-batch-2', 'smoke', 'authentication-controls', 'false'),
                    ('probe-batch-2', 'batch', 'authentication-controls', 'true'),
                    ('probe-batch-2', 'smoke', 'B08', 'true'),
                    ('A01', 'smoke', 'all', 'true'), ('B04', 'smoke', 'all', 'true'), ('B10', 'smoke', 'all', 'true'),
                    ('idle-observation', 'Batch', 'all', 'false'), ('idle-observation', 'batch', 'A04,A02', 'false'),
                    ('idle-observation', 'batch', 'A02,A02', 'false'), ('idle-observation', 'batch', 'A05', 'false'),
                    ('idle-observation', 'smoke', 'all', 'true')]
        for case, expected in [(item, 0) for item in valid] + [(item, 2) for item in rejected]:
            scenario, kind, items, capture = case
            inputs = {'scenario': scenario, 'run_kind': kind, 'items': items,
                      'capture_authentication_metadata': capture == 'true', 'test_handler_refresh_path': False,
                      'test_native_authentication': False}
            with self.subTest(case=case):
                env = dict(os.environ, PARCHWELL_SCENARIO=scenario, PARCHWELL_RUN_KIND=kind,
                           PARCHWELL_BATCH_ITEMS=items, PARCHWELL_CAPTURE_AUTHENTICATION_METADATA=capture)
                command = select if scenario.startswith('probe-batch') or scenario == 'idle-observation' else self.steps(self.jobs['full-C3'])['select']['run']
                run = subprocess.run(['/bin/bash', '-c', command], cwd=REPO, env=env, text=True, capture_output=True)
                self.assertEqual(expected, run.returncode, run.stderr)
                job_gate = expression(self.all_jobs['batch-stop']['if'], inputs, {})
                self.assertEqual(bool(expected == 0), job_gate)
                invocations, _ = (simulate_cleanup(self.all_jobs['batch-stop'], inputs) if job_gate else ([], {}))
                self.assertEqual(['stop_prepare', 'login', 'azure_session', 'stop'] if job_gate else [],
                                 [name for name, _, _ in invocations])

        full_c3 = self.jobs['full-C3']
        for scenario in ('A01', 'B04', 'B10'):
            for capture, expected_select, expected_cleanup in (('true', 2, []), ('false', 0, ['stop_prepare', 'login', 'azure_session', 'stop'])):
                inputs = {'scenario': scenario, 'run_kind': 'smoke', 'items': 'all',
                          'capture_authentication_metadata': capture == 'true', 'test_handler_refresh_path': False,
                          'test_native_authentication': False}
                env = dict(os.environ, PARCHWELL_SCENARIO=scenario, PARCHWELL_CAPTURE_AUTHENTICATION_METADATA=capture)
                select_run = subprocess.run(['/bin/bash', '-c', self.steps(full_c3)['select']['run']], cwd=REPO,
                                            env=env, text=True, capture_output=True)
                self.assertEqual(expected_select, select_run.returncode, select_run.stderr)
                initial = {'guard': 'success', 'checkout': 'success',
                           'select': 'success' if select_run.returncode == 0 else 'failure'}
                invocations, outcomes = simulate_cleanup(full_c3, inputs, initial)
                self.assertEqual(expected_cleanup, [name for name, _, _ in invocations])
                if expected_cleanup:
                    self.assertEqual('success', outcomes['stop_prepare'])
                    self.assertEqual('success', outcomes['azure_session'])
                else:
                    self.assertEqual('skipped', outcomes['stop_prepare'])
                    self.assertEqual('skipped', outcomes['azure_session'])

    def test_idle_observation_routes_only_to_batch_and_batch_stop(self):
        def admitted(values):
            names = []
            context = {'contains': lambda candidates, value: any(str(candidate).casefold() == str(value).casefold() for candidate in candidates),
                       'fromJSON': json.loads}
            for name, job in self.all_jobs.items():
                evaluated = job['if'].replace('always()', 'True').replace('needs.probe.outputs.full_allowed', "'true'")
                for field in ('capture_authentication_metadata', 'test_handler_refresh_path', 'test_native_authentication'):
                    evaluated = evaluated.replace('!inputs.' + field, 'not inputs.' + field)
                evaluated = evaluated.replace('&&', ' and ').replace('||', ' or ')
                for field, value in values.items():
                    evaluated = evaluated.replace('inputs.' + field, repr(value))
                if eval(evaluated, {'__builtins__': {}}, context):
                    names.append(name)
            return names

        valid = {'scenario': 'idle-observation', 'run_kind': 'smoke', 'items': 'all', 'capture_authentication_metadata': False,
                 'test_handler_refresh_path': False, 'test_native_authentication': False}
        environment = {'PARCHWELL_SCENARIO': 'idle-observation', 'PARCHWELL_RUN_KIND': 'smoke', 'PARCHWELL_BATCH_ITEMS': 'all',
                       'PARCHWELL_CAPTURE_AUTHENTICATION_METADATA': 'false', 'PARCHWELL_TEST_HANDLER_REFRESH_PATH': 'false',
                       'PARCHWELL_TEST_NATIVE_AUTHENTICATION': 'false'}
        select = self.steps(self.all_jobs['batch'])['select']['run']
        self.assertEqual(['batch', 'batch-stop'], admitted(valid))
        self.assertEqual(['batch', 'batch-stop'], admitted({**valid, 'run_kind': 'batch'}))
        self.assertEqual(['batch', 'batch-stop'], admitted({**valid, 'run_kind': 'batch', 'items': 'A02,A04'}))
        run = subprocess.run(['/bin/bash', '-c', select], cwd=REPO,
                             env=dict(os.environ, **{**environment, 'PARCHWELL_RUN_KIND': 'batch', 'PARCHWELL_BATCH_ITEMS': 'A02,A04'}),
                             text=True, capture_output=True, timeout=30)
        self.assertEqual(0, run.returncode, run.stderr)
        for kind in ('smoke', 'batch'):
            run = subprocess.run(['/bin/bash', '-c', select], cwd=REPO, env=dict(os.environ, **{**environment, 'PARCHWELL_RUN_KIND': kind}),
                                 text=True, capture_output=True, timeout=30)
            self.assertEqual(0, run.returncode, run.stderr)
        for field, value, variable, text in (('run_kind', 'Batch', 'PARCHWELL_RUN_KIND', 'Batch'),
                                             ('items', 'A04,A02', 'PARCHWELL_BATCH_ITEMS', 'A04,A02'),
                                             ('capture_authentication_metadata', True, 'PARCHWELL_CAPTURE_AUTHENTICATION_METADATA', 'true'),
                                             ('test_handler_refresh_path', True, 'PARCHWELL_TEST_HANDLER_REFRESH_PATH', 'true'),
                                             ('test_native_authentication', True, 'PARCHWELL_TEST_NATIVE_AUTHENTICATION', 'true')):
            with self.subTest(field=field):
                jobs = admitted({**valid, field: value})
                self.assertNotIn('batch-stop', jobs)
                self.assertLessEqual(set(jobs), {'batch'})
                run = subprocess.run(['/bin/bash', '-c', select], cwd=REPO, env=dict(os.environ, **{**environment, variable: text}),
                                     text=True, capture_output=True, timeout=30)
                self.assertEqual(2, run.returncode, run.stderr)
                self.assertEqual('unsupported idle observation selection', run.stderr.strip())

    def test_ledger_batch_routes_only_to_batch_and_batch_stop(self):
        def admitted(values):
            names = []
            context = {'contains': lambda candidates, value: any(str(candidate).casefold() == str(value).casefold() for candidate in candidates),
                       'fromJSON': json.loads}
            for name, job in self.all_jobs.items():
                evaluated = job['if'].replace('always()', 'True').replace('needs.probe.outputs.full_allowed', "'true'")
                for field in ('capture_authentication_metadata', 'test_handler_refresh_path', 'test_native_authentication'):
                    evaluated = evaluated.replace('!inputs.' + field, 'not inputs.' + field)
                evaluated = evaluated.replace('&&', ' and ').replace('||', ' or ')
                for field, value in values.items():
                    evaluated = evaluated.replace('inputs.' + field, repr(value))
                if eval(evaluated, {'__builtins__': {}}, context):
                    names.append(name)
            return names

        valid = {'scenario': 'ledger-batch', 'run_kind': 'smoke', 'items': 'all', 'capture_authentication_metadata': False,
                 'test_handler_refresh_path': False, 'test_native_authentication': False}
        environment = {'PARCHWELL_SCENARIO': 'ledger-batch', 'PARCHWELL_RUN_KIND': 'smoke', 'PARCHWELL_BATCH_ITEMS': 'all',
                       'PARCHWELL_CAPTURE_AUTHENTICATION_METADATA': 'false', 'PARCHWELL_TEST_HANDLER_REFRESH_PATH': 'false',
                       'PARCHWELL_TEST_NATIVE_AUTHENTICATION': 'false'}
        select = self.steps(self.all_jobs['batch'])['select']['run']
        self.assertEqual(['batch', 'batch-stop'], admitted(valid))
        run = subprocess.run(['/bin/bash', '-c', select], cwd=REPO, env=dict(os.environ, **environment),
                             text=True, capture_output=True, timeout=30)
        self.assertEqual(0, run.returncode, run.stderr)
        self.assertEqual(['batch', 'batch-stop'], admitted({**valid, 'run_kind': 'batch'}))
        run = subprocess.run(['/bin/bash', '-c', select], cwd=REPO, env=dict(os.environ, **{**environment, 'PARCHWELL_RUN_KIND': 'batch'}),
                             text=True, capture_output=True, timeout=30)
        self.assertEqual(0, run.returncode, run.stderr)
        for field, value, variable, text in (('run_kind', 'full', 'PARCHWELL_RUN_KIND', 'full'),
                                             ('items', 'B06', 'PARCHWELL_BATCH_ITEMS', 'B06'),
                                             ('capture_authentication_metadata', True, 'PARCHWELL_CAPTURE_AUTHENTICATION_METADATA', 'true'),
                                             ('test_handler_refresh_path', True, 'PARCHWELL_TEST_HANDLER_REFRESH_PATH', 'true'),
                                             ('test_native_authentication', True, 'PARCHWELL_TEST_NATIVE_AUTHENTICATION', 'true')):
            with self.subTest(field=field):
                jobs = admitted({**valid, field: value})
                self.assertNotIn('batch-stop', jobs)
                self.assertLessEqual(set(jobs), {'batch'})
                run = subprocess.run(['/bin/bash', '-c', select], cwd=REPO, env=dict(os.environ, **{**environment, variable: text}),
                                     text=True, capture_output=True, timeout=30)
                self.assertEqual(2, run.returncode, run.stderr)
                self.assertEqual('unsupported ledger batch selection', run.stderr.strip())

    def test_bridge_discovery(self):
        suite = unittest.TestLoader().discover(str(REPO / 'infra/scripts/tests'), pattern='test_*.py')
        def flatten(tests):
            for test in tests:
                if isinstance(test, unittest.TestSuite):
                    yield from flatten(test)
                else:
                    yield test
        tests = list(flatten(suite)); identifiers = [test.id() for test in tests]
        self.assertTrue(any(identifier.startswith('test_phase.') for identifier in identifiers), identifiers)
        self.assertTrue(any(identifier.startswith('test_workflow.') for identifier in identifiers), identifiers)
        self.assertFalse(any(isinstance(test, unittest.loader._FailedTest) for test in tests), identifiers)


class B10AbruptWorkflowTests(unittest.TestCase):
    # SHA-256 of exact workflow bytes at merged PR1 74be825cb6ac71c2c70a6612f3fa0910dcdc52ef,
    # except the .NET verification logging update authorized in the PR407 receipt, the job split of issue #441 and the test shards of issue #452.
    # Actions checks out one commit; preservation must not require Git history.
    BASE_WORKFLOWS = {
        '.github/workflows/cluster-smoke.yml': 'e9266d30ff356782918f5fc5b048963265c58a48a3ea6c4733496374f30e3196',
        '.github/workflows/docs-advisory.yml': '39bea63dea8fb37d330e2be1b74782bdc33b3d2655e804ee6b78a231263475ec',
        '.github/workflows/dotnet-verification.yml': 'a7cbf55f1e892dca39977abc927d210b63f12539aef3b55771d2f4c26b486118',
        '.github/workflows/scenario.yml': 'c3323aca63fa977f75129ed9050c2d30f98a49ae18104d3f88240d40ecce7099',
        '.github/workflows/sweeper.yml': 'fdff4a41ab1df0ade1c8e6d216adaeba6aba13dc6415b0b184de1101d7dc2234',
        '.github/workflows/verifier-c5-mutation.yml': 'b1831877c056d4a6ceddff38d75baa54f766c3287a42664cd48900e03f5016a3',
    }

    @classmethod
    def setUpClass(cls):
        cls.path = REPO / '.github/workflows/b10-abrupt-trial.yml'
        cls.workflow = yaml.load(cls.path.read_text(), Loader=UniqueLoader)
        cls.scenario = yaml.load((REPO / '.github/workflows/scenario.yml').read_text(), Loader=UniqueLoader)
        cls.trial = cls.workflow['jobs']['trial']
        cls.stop = cls.workflow['jobs']['stop']

    @staticmethod
    def steps(job):
        return {step['id']: step for step in job['steps'] if 'id' in step}

    def test_manual_confirmation_and_fixed_diagnostic_selection(self):
        self.assertEqual({'workflow_dispatch': {'inputs': {'confirm_diagnostic_trial': {
            'description': 'Confirm the single diagnostic-only B10 restart trial',
            'type': 'boolean', 'required': 'true', 'default': 'false'}}}}, self.workflow['on'])
        self.assertEqual({'contents': 'read', 'id-token': 'write'}, self.workflow['permissions'])
        self.assertEqual({'group': 'parchwell-ephemeral', 'queue': 'max', 'cancel-in-progress': 'false'}, self.workflow['concurrency'])
        self.assertEqual(['trial', 'stop'], list(self.workflow['jobs']))
        self.assertNotIn('outputs', self.trial)
        for name, value in {'PARCHWELL_SCENARIO': 'B10', 'PARCHWELL_PHASE': 'C1',
                            'PARCHWELL_CONFIGURATION': 'C1', 'PARCHWELL_RUN_KIND': 'smoke',
                            'PARCHWELL_PURPOSE': 'diagnostic_trial', 'PARCHWELL_REPEAT': '1',
                            'PARCHWELL_B10_ABRUPT_DIAGNOSTIC': 'true',
                            'PARCHWELL_CONFIRM_DIAGNOSTIC_TRIAL': "${{ inputs.confirm_diagnostic_trial && 'true' || 'false' }}"}.items():
            self.assertEqual(value, self.trial['env'][name])
        self.assertNotIn('needs', self.trial)
        self.assertEqual(['trial'], self.stop['needs'])
        self.assertEqual('always()', self.stop['if'])
        for job, environment in ((self.trial, 'parchwell-scenarios'), (self.stop, 'parchwell-sweeper')):
            self.assertEqual(environment, job['environment'])
            self.assertEqual('${{ vars.PARCHWELL_RUNNER_LABEL }}', job['runs-on'])
            self.assertEqual('360', job['timeout-minutes'])

    def test_guards_before_login_with_real_shell_and_no_cloud(self):
        synthetic_uuid = '00000000-0000-0000-0000-000000000001'
        for job in (self.trial, self.stop):
            steps = job['steps']
            self.assertEqual('guard', steps[0]['id'])
            self.assertLess(0, next(i for i, step in enumerate(steps) if step.get('id') == 'login'))
            self.assertIn('test "$GITHUB_REF" = refs/heads/main', steps[0]['run'])
        for ref, confirmation, succeeds in [('refs/heads/main', 'true', True),
                                            ('refs/heads/main', 'false', False),
                                            ('refs/heads/main', '', False),
                                            ('refs/heads/untrusted', 'true', False)]:
            with self.subTest(ref=ref, confirmation=confirmation):
                env = dict(os.environ, GITHUB_REF=ref, PARCHWELL_CONFIRM_DIAGNOSTIC_TRIAL=confirmation,
                           **{key: synthetic_uuid for key in ('AZURE_CLIENT_ID', 'AZURE_TENANT_ID', 'AZURE_SUBSCRIPTION_ID')})
                result = subprocess.run(['bash', '-c', self.trial['steps'][0]['run']], env=env,
                                        text=True, capture_output=True, timeout=30)
                self.assertEqual(succeeds, result.returncode == 0, result.stderr)

    def test_pinned_setup_lifecycle_and_stop_bodies_are_copied(self):
        source = self.steps(self.scenario['jobs']['probe'])
        actual = self.steps(self.trial)
        copied = ('checkout', 'tools', 'login', 'azure_session', 'images', 'cluster_init',
                  'start', 'cluster_plan', 'cluster_apply', 'ephemeral_init', 'ephemeral_plan',
                  'ephemeral_apply', 'dns', 'credentials', 'namespace', 'delete_workload', 'destroy')
        for identifier in copied:
            self.assertEqual(source[identifier], actual[identifier], identifier)
        self.assertEqual([step for step in self.scenario['jobs']['probe']['steps'] if 'id' not in step],
                         [step for step in self.trial['steps'] if 'id' not in step])
        self.assertEqual(self.scenario['jobs']['batch-stop']['steps'], self.stop['steps'])
        expected_pins = {
            'actions/checkout': '3d3c42e5aac5ba805825da76410c181273ba90b1',
            'hashicorp/setup-terraform': 'dfe3c3f87815947d99a8997f908cb6525fc44e9e',
            'actions/setup-dotnet': 'a98b56852c35b8e3190ac28c8c2271da59106c68',
            'azure/login': '7184910d9eb2b1c5e48f7073824a90609bb9b6d6',
            'actions/upload-artifact': 'ea165f8d65b6e75b540449e92b4886f43607fa02',
        }
        for job in self.workflow['jobs'].values():
            for step in job['steps']:
                if 'uses' in step:
                    action, revision = step['uses'].split('@')
                    self.assertEqual(expected_pins[action], revision)
        self.assertEqual(source['prepare']['env'], actual['prepare']['env'])
        self.assertEqual('bash infra/scripts/phase/phase.sh prepare-b10-abrupt-trial', actual['prepare']['run'])
        self.assertEqual('bash infra/scripts/phase/phase.sh run-b10-abrupt-trial', actual['trial']['run'])
        self.assertEqual('bash infra/scripts/phase/phase.sh collect-b10-abrupt-trial', actual['collect']['run'])
        self.assertEqual("always() && steps.prepare.outcome == 'success'", actual['collect']['if'])
        self.assertLess(list(actual).index('prepare'), list(actual).index('login'))

    def test_only_diagnostic_upload_and_required_steps_match(self):
        steps = self.steps(self.trial)
        self.assertFalse({'judge', 'candidate', 'upload_candidate', 'upload_pods', 'repeats'} & steps.keys())
        uploads = [step for job in self.workflow['jobs'].values() for step in job['steps']
                   if step.get('uses', '').startswith('actions/upload-artifact@')]
        self.assertEqual([steps['upload_diagnostic']], uploads)
        self.assertEqual('${{ env.PARCHWELL_ARTIFACT_DIRECTORY }}/b10-abrupt-diagnostic', uploads[0]['with']['path'])
        self.assertEqual('b10-abrupt-diagnostic-${{ github.run_id }}-${{ github.run_attempt }}', uploads[0]['with']['name'])
        for job in (self.trial, self.stop):
            indexed = self.steps(job)
            report = indexed['report']
            self.assertEqual('always()', report['if'])
            required = report['env']['REQUIRED_STEPS'].split() + report['env']['WORKLOAD_STEPS'].split()
            self.assertEqual(len(required), len(set(required)))
            self.assertEqual(set(indexed) - {'login', 'report'}, set(required))

    def test_success_failure_timeout_and_cancelled_paths_keep_cleanup(self):
        def allowed(expression, outcomes):
            expression = expression.replace('always()', 'True').replace('&&', ' and ')
            for name, value in outcomes.items():
                expression = expression.replace('steps.' + name + '.outcome', repr(value))
            return eval(expression, {'__builtins__': {}}, {})

        steps = self.steps(self.trial)
        stop = self.steps(self.stop)
        for status in ('success', 'failure', 'timed_out', 'cancelled'):
            with self.subTest(trial=status):
                outcomes = {name: 'success' for name in steps}
                outcomes['trial'] = status
                for name in ('collect', 'upload_diagnostic', 'delete_workload', 'destroy', 'report'):
                    self.assertTrue(allowed(steps[name]['if'], outcomes), name)
                self.assertTrue(allowed(self.stop['if'], outcomes))
                self.assertTrue(allowed(stop['stop']['if'], {'stop_prepare': 'success', 'azure_session': 'success'}))
                self.assertTrue(allowed(stop['report']['if'], {}))
        for failed in ('prepare', 'azure_session', 'credentials'):
            outcomes = {name: 'success' for name in steps}
            outcomes[failed] = 'failure'
            self.assertFalse(allowed(steps['delete_workload']['if'], outcomes))
            self.assertEqual(failed == 'credentials', allowed(steps['destroy']['if'], outcomes))
        for failed in ('stop_prepare', 'azure_session'):
            outcomes = {'stop_prepare': 'success', 'azure_session': 'success', failed: 'failure'}
            self.assertFalse(allowed(stop['stop']['if'], outcomes))

    def test_mirror_and_all_preexisting_workflows_unchanged(self):
        self.assertEqual(self.path.read_bytes(), (REPO / 'docs/operations/workflows/b10-abrupt-trial.yml.txt').read_bytes())
        for relative, expected in self.BASE_WORKFLOWS.items():
            self.assertEqual(expected, hashlib.sha256((REPO / relative).read_bytes()).hexdigest(), relative)


if __name__ == '__main__':
    unittest.main()

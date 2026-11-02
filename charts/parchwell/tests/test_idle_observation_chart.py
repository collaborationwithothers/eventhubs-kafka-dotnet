#!/usr/bin/env python3
"""Idle-observation Job contract: pairs, bounds, environment names and rejected inputs."""
from copy import deepcopy
from pathlib import Path
import json
import tempfile

import yaml

from test_chart import CHART, ROOT, FIXTURE, DEFAULTS, run, with_case

LIMITS = json.loads((ROOT / 'docs/operations/idle-observation-runtime-limits.json').read_text())
PAIRS = (('A02', 'C0'), ('A02', 'C1'), ('A03', 'C0'), ('A03', 'C1'), ('A03', 'C2'), ('A04', 'C0'), ('A04', 'C1'))
FIELDS = ('ScenarioId', 'ConfigurationId', 'RunId', 'RunKind', 'SourceCommit', 'JobName', 'NamespaceName',
          'BlobEndpoint', 'BlobContainer', 'ScenarioRoot', 'PodName', 'PodUid')
PARSING = ('MaxDocumentCharacters', 'MaxNestingDepth', 'MaxScalarCharacters', 'MaxParserEvents')
JUDGE_FIELDS = ('RunKind', 'RunId', 'SourceCommit', 'JobName', 'NamespaceName', 'ServiceAccount', 'WorkloadIdentityClientId',
                'ScenarioRoot', 'BlobServiceUri', 'LedgerContainer', 'VerifierContainer', 'LoadGenImage', 'VerifierImage',
                'CapturePods', 'PodName', 'PodUid')
CAPTURE_PODS = 'A02C0:idle-1-1-a02-c0-idle-x7k2p:uid-1,A04C1:idle-1-1-a04-c1-idle-q9m4z:uid-2'


def case(item, configuration, run_kind):
    values = deepcopy(FIXTURE)
    values.update(scenario_id=item, configuration_id=configuration)
    runtime = values['runtime']
    runtime.update(run_id='idle-1-1', phase=configuration, mode='idle-observation', run_kind=run_kind,
                   bounds=deepcopy(DEFAULTS))
    runtime.pop('repeat')
    runtime.pop('loadgen_job_name')
    return values


def positive_cases():
    for item, configuration in PAIRS:
        yield case(item, configuration, 'batch')
    yield case('A02', 'C0', 'smoke')


def judge_case(run_kind, capture_pods=CAPTURE_PODS):
    values = case('A04', 'C1', run_kind)
    values['runtime'].update(mode='idle-observation-judge', capture_pods=capture_pods)
    values['runtime']['service_accounts']['loadgen']['client_id'] = '00000000-0000-0000-0000-000000000001'
    values['runtime']['service_accounts']['verifier']['client_id'] = '00000000-0000-0000-0000-000000000002'
    return values


def judge_cases():
    yield judge_case('smoke')
    yield judge_case('batch')
    yield judge_case('batch', 'none')


def judge_expected_names():
    names = {'PARCHWELL_SOURCE_COMMIT', 'PARCHWELL_JOB_NAME', 'PARCHWELL_POD_NAME', 'PARCHWELL_POD_UID',
             'Telemetry__ConnectionString', 'Telemetry__ServiceName', 'Telemetry__ServiceInstanceId',
             'Telemetry__ScenarioId', 'Telemetry__RunId'}
    names.update('IdleObservationJudge__' + key for key in JUDGE_FIELDS)
    names.update('IdleObservationJudge__Limits__' + key for key in LIMITS['judge'])
    names.update('IdleObservationJudge__ScenarioLoadOptions__' + key for key in PARSING)
    return names


def expected_names():
    names = {'PARCHWELL_SOURCE_COMMIT', 'PARCHWELL_JOB_NAME', 'PARCHWELL_POD_NAME', 'PARCHWELL_POD_UID',
             'Telemetry__ConnectionString', 'Telemetry__ServiceName', 'Telemetry__ServiceInstanceId',
             'Telemetry__ScenarioId', 'Telemetry__RunId'}
    names.update('IdleObservation__' + key for key in FIELDS)
    names.update('IdleObservation__' + key for key in LIMITS['capture'])
    names.update('IdleObservation__ScenarioLoadOptions__' + key for key in PARSING)
    return names


def verify(document, values):
    objects = list(yaml.safe_load_all(document))
    jobs = [o for o in objects if o and o['kind'] == 'Job']
    assert len(jobs) == 1
    runtime = values['runtime']
    job = jobs[0]
    spec = job['spec']
    pod = spec['template']['spec']
    container = pod['containers'][0]
    assert job['metadata']['name'] == f"idle-1-1-{values['scenario_id'].lower()}-{values['configuration_id'].lower()}-idle"
    assert spec['activeDeadlineSeconds'] == 1320 and spec['backoffLimit'] == 0
    assert spec['podReplacementPolicy'] == 'Failed'
    assert pod['serviceAccountName'] == 'loadgen' and pod['nodeSelector'] == {'workload': 'loadgen'}
    assert pod['terminationGracePeriodSeconds'] == 30
    assert len(pod['containers']) == 1 and container['name'] == 'loadgen'
    assert container['args'] == ['--idle-observation']
    assert container['image'] == runtime['loadgen_image']
    env = {entry['name']: entry for entry in container['env']}
    assert len(env) == len(container['env'])
    assert set(env) == expected_names() and len(env) == 45, set(env) ^ expected_names()
    assert env['IdleObservation__BlobContainer']['value'] == 'ledger'
    assert env['IdleObservation__RunKind']['value'] == runtime['run_kind']
    for key, value in LIMITS['capture'].items():
        assert env['IdleObservation__' + key]['value'] == str(value), key


def verify_judge(document, values):
    objects = list(yaml.safe_load_all(document))
    jobs = [o for o in objects if o and o['kind'] == 'Job']
    assert len(jobs) == 1
    runtime = values['runtime']
    job = jobs[0]
    spec = job['spec']
    pod = spec['template']['spec']
    container = pod['containers'][0]
    assert job['metadata']['name'] == 'idle-1-1-idle-judge'
    assert job['metadata']['labels']['parchwell.io/role'] == 'verifier'
    assert spec['activeDeadlineSeconds'] == 720 and spec['backoffLimit'] == 0
    assert 'podReplacementPolicy' not in spec
    assert pod['serviceAccountName'] == 'verifier' and pod['nodeSelector'] == {'workload': 'workload'}
    assert 'tolerations' not in pod
    assert len(pod['containers']) == 1 and container['name'] == 'verifier'
    assert container['args'] == ['--idle-observation-judge']
    assert container['image'] == runtime['verifier_image']
    env = {entry['name']: entry for entry in container['env']}
    assert len(env) == len(container['env'])
    assert set(env) == judge_expected_names() and len(env) == 40, set(env) ^ judge_expected_names()
    assert env['Telemetry__ServiceName']['value'] == 'Parchwell.Verifier'
    assert env['Telemetry__ScenarioId']['value'] == 'idle-observation'
    assert env['IdleObservationJudge__LedgerContainer']['value'] == 'ledger'
    assert env['IdleObservationJudge__RunKind']['value'] == runtime['run_kind']
    assert env['IdleObservationJudge__CapturePods']['value'] == runtime['capture_pods']
    assert env['IdleObservationJudge__VerifierImage']['value'] == runtime['verifier_image']
    assert env['IdleObservationJudge__LoadGenImage']['value'] == runtime['loadgen_image']
    for key, value in LIMITS['judge'].items():
        assert env['IdleObservationJudge__Limits__' + key]['value'] == str(value), key


def judge_negatives():
    def mutate(base, change):
        bad = deepcopy(base)
        change(bad)
        return bad
    good = judge_case('batch')
    return [
        mutate(good, lambda d: d.update(scenario_id='A02')),
        mutate(good, lambda d: d.update(configuration_id='C0')),
        mutate(good, lambda d: d['runtime'].pop('capture_pods')),
    ]


def negatives():
    def mutate(base, change):
        bad = deepcopy(base)
        change(bad)
        return bad
    good = case('A02', 'C0', 'batch')
    return [
        mutate(good, lambda d: d.update(scenario_id='B01')),
        mutate(case('A02', 'C0', 'batch'), lambda d: d.update(configuration_id='C2')),
        mutate(good, lambda d: d['runtime'].update(phase='C1')),
        mutate(good, lambda d: d['runtime'].pop('run_kind')),
        mutate(good, lambda d: d['runtime'].update(repeat=1)),
        mutate(good, lambda d: d['runtime'].update(action_role='loadgen')),
        mutate(good, lambda d: d['runtime']['bounds']['idle_observation']['capture'].update(ActionWindowMs=1)),
        mutate(good, lambda d: d['runtime']['bounds']['idle_observation']['execution'].update(job_seconds=1321)),
    ]


def main():
    schema = json.loads((CHART / 'values.schema.json').read_text())
    const = schema['properties']['runtime']['properties']['bounds']['properties']['idle_observation']['const']
    assert DEFAULTS['idle_observation'] == LIMITS == const
    assert len(expected_names()) == 45 and len(judge_expected_names()) == 40
    assert len(LIMITS['judge']) == 11 and LIMITS['execution']['judge_job_seconds'] == 720
    cases = list(positive_cases())
    assert len(cases) == 8
    with tempfile.TemporaryDirectory(prefix='idle-observation-chart-') as name:
        directory = Path(name)
        for index, values in enumerate(cases):
            fixture = directory / f'{index}.yaml'
            with_case(values, fixture)
            lint = run(['helm', 'lint', str(CHART), '-f', str(fixture)])
            assert lint.returncode == 0, (index, lint.stdout, lint.stderr)
            rendered = run(['helm', 'template', 'synthetic', str(CHART), '--namespace', 'parchwell', '-f', str(fixture)])
            assert rendered.returncode == 0, (index, rendered.stderr)
            verify(rendered.stdout, values)
            output = directory / f'{index}.rendered.yaml'
            output.write_text(rendered.stdout)
            checked = run(['kubeconform', '-strict', '-summary', '-kubernetes-version', '1.35.0', str(output)])
            assert checked.returncode == 0, checked.stdout + checked.stderr
        bad_cases = negatives()
        assert len(bad_cases) == 8
        for index, values in enumerate(bad_cases):
            fixture = directory / f'negative-{index}.yaml'
            with_case(values, fixture)
            rendered = run(['helm', 'template', 'synthetic', str(CHART), '--namespace', 'parchwell', '-f', str(fixture)])
            assert rendered.returncode != 0, index
            assert not any(o and o.get('kind') == 'Job' for o in yaml.safe_load_all(rendered.stdout)), index
        judge_positive = list(judge_cases())
        assert len(judge_positive) == 3
        for index, values in enumerate(judge_positive):
            fixture = directory / f'judge-{index}.yaml'
            with_case(values, fixture)
            lint = run(['helm', 'lint', str(CHART), '-f', str(fixture)])
            assert lint.returncode == 0, (index, lint.stdout, lint.stderr)
            rendered = run(['helm', 'template', 'synthetic', str(CHART), '--namespace', 'parchwell', '-f', str(fixture)])
            assert rendered.returncode == 0, (index, rendered.stderr)
            verify_judge(rendered.stdout, values)
            output = directory / f'judge-{index}.rendered.yaml'
            output.write_text(rendered.stdout)
            checked = run(['kubeconform', '-strict', '-summary', '-kubernetes-version', '1.35.0', str(output)])
            assert checked.returncode == 0, checked.stdout + checked.stderr
        judge_bad = judge_negatives()
        assert len(judge_bad) == 3
        for index, values in enumerate(judge_bad):
            fixture = directory / f'judge-negative-{index}.yaml'
            with_case(values, fixture)
            rendered = run(['helm', 'template', 'synthetic', str(CHART), '--namespace', 'parchwell', '-f', str(fixture)])
            assert rendered.returncode != 0, index
            assert not any(o and o.get('kind') == 'Job' for o in yaml.safe_load_all(rendered.stdout)), index
    print('idle-observation chart acceptance passed: 8 positive, 8 negative, 3 judge positive, 3 judge negative')


if __name__ == '__main__':
    main()

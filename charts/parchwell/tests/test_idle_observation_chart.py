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
    assert len(expected_names()) == 45
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
    print('idle-observation chart acceptance passed: 8 positive, 8 negative')


if __name__ == '__main__':
    main()

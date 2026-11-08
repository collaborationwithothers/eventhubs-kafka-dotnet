#!/usr/bin/env python3
"""Default-off B10 diagnostic Jobs and their closed configuration boundary."""
from copy import deepcopy
from pathlib import Path
import json
import tempfile

import yaml

from test_chart import CHART, DEFAULTS, FIXTURE, expected_environment, run, scalar, with_case

POLICY = {
    'MaxFrameBytes': 65536, 'QueueCapacity': 256, 'MaxJournalBytes': 16777216,
    'MaxJournalEntries': 4096, 'MaxAuxiliaryBytes': 65536, 'MaxJsonDepth': 32,
    'ArmTimeoutSeconds': 30, 'KillTimeoutSeconds': 10, 'ReapTimeoutSeconds': 10,
    'EofTimeoutSeconds': 10, 'ReleaseDelaySeconds': 120,
}


def diagnostic_cases():
    cases = []
    for mode, suffix in (('loadgen', 'first'), ('loadgen2', 'successor')):
        values = deepcopy(FIXTURE)
        values.update(scenario_id='B10', configuration_id='C1')
        runtime = values['runtime']
        runtime.update(phase='C1', repeat=1, mode=mode, run_kind='smoke',
                       loadgen_job_name=runtime['run_id'] + '-c1-r1-loadgen',
                       b10_abrupt_diagnostic=True,
                       b10_abrupt_binding_configmap=runtime['run_id'] + '-b10-abrupt-' + suffix,
                       bounds=deepcopy(DEFAULTS))
        cases.append(values)
    return cases


def render(values, directory, name, valid=True):
    source = directory / (name + '.yaml')
    output = directory / (name + '.rendered.yaml')
    with_case(values, source)
    proc = run(['python3', 'infra/scripts/lifecycle/command.py', '120', '--stdout-file', str(output),
                '--max-bytes', '4194304', '--', 'helm', 'template', 'synthetic', 'charts/parchwell',
                '--namespace', 'parchwell', '-f', str(source)])
    assert (proc.returncode == 0) == valid, (name, proc.returncode, proc.stderr)
    if valid:
        assert output.is_file(), 'b10_abrupt_render_missing:' + str(output)
        assert not Path(str(output) + '.incomplete').exists()
        return output.read_bytes()
    return None


def verify(document, values):
    objects = list(yaml.safe_load_all(document))
    assert sorted(obj['kind'] for obj in objects) == ['Job', 'ServiceAccount', 'ServiceAccount']
    job = next(obj for obj in objects if obj['kind'] == 'Job')
    runtime = values['runtime']
    first = runtime['mode'] == 'loadgen'
    name = runtime['loadgen_job_name'] + ('' if first else '2')
    assert job['metadata']['name'] == name
    assert job['metadata']['namespace'] == 'parchwell'
    spec = job['spec']
    assert spec['completions'] == spec['parallelism'] == 1
    assert spec['backoffLimit'] == 0 and spec['podReplacementPolicy'] == 'Failed'
    assert spec['activeDeadlineSeconds'] == 1140
    pod = spec['template']['spec']
    assert pod['restartPolicy'] == 'Never' and pod['terminationGracePeriodSeconds'] == 30
    assert pod['serviceAccountName'] == 'loadgen' and pod['automountServiceAccountToken'] is False
    assert pod['nodeSelector'] == {'workload': 'loadgen'}
    assert pod['tolerations'] == [{'key': 'workload', 'operator': 'Equal', 'value': 'loadgen', 'effect': 'NoSchedule'}]
    assert pod['securityContext'] == {'runAsUser': 1654, 'runAsGroup': 1654, 'fsGroup': 1654,
                                      'runAsNonRoot': True, 'seccompProfile': {'type': 'RuntimeDefault'}}
    assert not any(pod.get(key) for key in ('hostPID', 'hostIPC', 'hostNetwork', 'shareProcessNamespace', 'initContainers'))
    assert len(pod['containers']) == 1
    container = pod['containers'][0]
    assert container['name'] == 'loadgen' and container['image'] == runtime['loadgen_image']
    assert container['args'] == ['--b10-abrupt-supervisor' if first else '--b10-abrupt-successor']
    assert 'command' not in container
    assert container['securityContext'] == {'allowPrivilegeEscalation': False, 'readOnlyRootFilesystem': True,
                                            'capabilities': {'drop': ['ALL']}}
    assert container['resources'] == {'requests': {'cpu': '1', 'memory': '2Gi'}, 'limits': {'cpu': '3', 'memory': '12Gi'}}
    filename = 'start-line.json' if first else 'predecessor.json'
    assert container['volumeMounts'] == [
        {'name': 'work', 'mountPath': '/work'}, {'name': 'tmp', 'mountPath': '/tmp'},
        {'name': 'b10-abrupt-binding', 'mountPath': '/bindings/b10-abrupt', 'readOnly': True}]
    assert pod['volumes'] == [
        {'name': 'work', 'emptyDir': {'sizeLimit': '8Gi'}}, {'name': 'tmp', 'emptyDir': {}},
        {'name': 'b10-abrupt-binding', 'configMap': {'name': runtime['b10_abrupt_binding_configmap'],
                                                   'items': [{'key': 'binding.json', 'path': filename}]}}]
    ordinary = expected_environment(values)
    expected = {key: value for key, value in ordinary.items() if not key.startswith('LoadGen__')}
    identity = {'Enabled': 'true', 'Purpose': 'diagnostic_trial', 'Role': 'supervisor' if first else 'successor',
                'RunId': runtime['run_id'], 'ScenarioId': 'B10', 'ConfigurationId': 'C1', 'Phase': 'C1',
                'Repeat': '1', 'SourceCommit': runtime['source_commit'], 'ImageReference': runtime['loadgen_image'],
                'JobName': name, 'NamespaceName': runtime['eventhubs']['namespace_name'],
                'BlobEndpoint': runtime['blob']['account_url'], 'ScenarioSourceRoot': '/app',
                'OutputRoot': '/work/loadgen',
                'StartLineBindingPath' if first else 'PredecessorBindingPath': '/bindings/b10-abrupt/' + filename}
    expected.update({'B10Abrupt__' + key: ('value', scalar(value)) for key, value in (identity | POLICY).items()})
    expected.update({'B10Abrupt__PodName': ('field', 'metadata.name'), 'B10Abrupt__PodUid': ('field', 'metadata.uid')})
    env = container['env']
    assert len(env) == len({entry['name'] for entry in env})
    actual = {entry['name']: ('value', entry['value']) if 'value' in entry else
              ('field', entry['valueFrom']['fieldRef']['fieldPath']) for entry in env}
    assert actual == expected, (actual, expected)
    assert spec['template']['metadata']['labels']['azure.workload.identity/use'] == 'true'
    for account in (obj for obj in objects if obj['kind'] == 'ServiceAccount'):
        assert account['automountServiceAccountToken'] is False
        assert account['metadata']['annotations']['azure.workload.identity/client-id'] == runtime['service_accounts'][account['metadata']['name']]['client_id']


def invalid_cases():
    first, successor = diagnostic_cases()
    for key, value in (('scenario_id', 'A01'), ('configuration_id', 'C0')):
        case = deepcopy(first); case[key] = value
        yield key, case
    for key, value in (('phase', 'C0'), ('repeat', 0), ('repeat', 2), ('run_kind', 'batch'),
                       ('source_commit', 'main'), ('loadgen_image', 'registry.invalid/parchwell/loadgen:latest'),
                       ('b10_abrupt_diagnostic', 'true'), ('b10_abrupt_role', 'child'),
                       ('b10_abrupt_role', 'unknown'), ('b10_abrupt_binding_configmap', ''),
                       ('capture_pods', 'mixed'), ('native_authentication', 'jwt-bearer'),
                       ('loadgen_job_name', 'unbound-loadgen')):
        case = deepcopy(first); case['runtime'][key] = value
        yield key + '-' + str(value).replace('/', '-'), case
    for mode in ('start-line', 'verify', 'b10-sweep-line', 'b10-sweep', 'judge', 'single-action', 'idle-observation'):
        case = deepcopy(first); case['runtime']['mode'] = mode
        yield 'mode-' + mode, case
    for key in ('run_kind', 'repeat', 'b10_abrupt_binding_configmap'):
        case = deepcopy(first); del case['runtime'][key]
        yield 'missing-' + key, case
    case = deepcopy(successor); del case['runtime']['b10_abrupt_binding_configmap']
    yield 'successor-missing-binding', case
    case = deepcopy(first); case['runtime']['b10_abrupt_diagnostic'] = False
    yield 'false-with-binding', case
    case = deepcopy(first); del case['runtime']['b10_abrupt_diagnostic']
    yield 'missing-confirmation-with-binding', case
    for key, value in POLICY.items():
        for delta in (-1, 1):
            case = deepcopy(first); case['runtime']['bounds']['b10_abrupt'][key] = value + delta
            yield key + '-' + str(delta), case
    case = deepcopy(first); case['runtime']['bounds']['b10_abrupt']['UnsupportedSetting'] = 1
    yield 'unknown-policy', case


def main():
    schema = json.loads((CHART / 'values.schema.json').read_text())
    assert DEFAULTS['b10_abrupt'] == POLICY
    assert schema['properties']['runtime']['properties']['bounds']['properties']['b10_abrupt']['const'] == POLICY
    with tempfile.TemporaryDirectory(prefix='parchwell-b10-abrupt-chart-') as name:
        directory = Path(name)
        defaults = deepcopy(FIXTURE)
        explicit_false = deepcopy(defaults); explicit_false['runtime']['b10_abrupt_diagnostic'] = False
        assert render(defaults, directory, 'defaults') == render(explicit_false, directory, 'explicit-false')
        for case_name, values in zip(('first', 'successor'), diagnostic_cases(), strict=True):
            verify(render(values, directory, case_name), values)
            proc = run(['kubeconform', '-strict', '-summary', '-kubernetes-version', '1.35.0',
                        str(directory / (case_name + '.rendered.yaml'))])
            assert proc.returncode == 0, proc.stdout + proc.stderr
        negatives = list(invalid_cases())
        for index, (case_name, values) in enumerate(negatives):
            render(values, directory, str(index) + '-' + case_name, valid=False)
    print(f'b10 abrupt chart checks passed: 4 renders, {len(negatives)} invalid selections')


if __name__ == '__main__':
    main()

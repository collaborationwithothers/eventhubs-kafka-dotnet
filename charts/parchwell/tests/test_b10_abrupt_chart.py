#!/usr/bin/env python3
"""Default-off B10 diagnostic Jobs and their closed configuration boundary."""
from copy import deepcopy
from pathlib import Path
import json
import tempfile

import yaml

from test_chart import (CHART, DEFAULTS, FIXTURE, VERIFIER_REQUIRED, expected_environment,
                        run, scalar, with_case, worker_setting_errors)

POLICY = {
    'MaxRecordBytes': 65536, 'QueueCapacity': 256, 'MaxJournalBytes': 16777216,
    'MaxJournalEntries': 4096, 'MaxAuxiliaryBytes': 65536, 'MaxJsonDepth': 32,
    'ReleaseDelaySeconds': 120, 'ProbeRate': 100, 'CutOrdinal': 4,
}


def diagnostic_cases():
    cases = []
    for mode, suffix in (('loadgen-restart', 'first'), ('loadgen-successor', 'successor')):
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
    first = runtime['mode'] == 'loadgen-restart'
    name = runtime['loadgen_job_name'] + ('-restart' if first else '-successor')
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
    assert container['args'] == ['--b10-abrupt-parent' if first else '--b10-abrupt-successor']
    assert container['command'] == ['/app/Parchwell.LoadGen']
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
    ordinary_values = deepcopy(values)
    ordinary_values['runtime']['mode'] = 'loadgen'
    ordinary = expected_environment(ordinary_values)
    ordinary['PARCHWELL_JOB_NAME'] = ('value', name)
    expected = {key: value for key, value in ordinary.items() if not key.startswith('LoadGen__')}
    identity = {'Enabled': 'true', 'Purpose': 'diagnostic_trial', 'Role': 'parent' if first else 'successor',
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
    for mode in ('loadgen', 'loadgen2', 'start-line', 'verify', 'b10-sweep-line', 'b10-sweep', 'judge', 'single-action', 'idle-observation'):
        case = deepcopy(first); case['runtime']['mode'] = mode
        yield 'mode-' + mode, case
    for key in ('run_kind', 'repeat', 'loadgen_job_name', 'b10_abrupt_binding_configmap'):
        case = deepcopy(first); del case['runtime'][key]
        yield 'missing-' + key, case
    case = deepcopy(successor); del case['runtime']['b10_abrupt_binding_configmap']
    yield 'successor-missing-binding', case
    for original in diagnostic_cases():
        for flag in (False, None):
            case = deepcopy(original)
            del case['runtime']['b10_abrupt_binding_configmap']
            if flag is None:
                del case['runtime']['b10_abrupt_diagnostic']
            else:
                case['runtime']['b10_abrupt_diagnostic'] = flag
            yield case['runtime']['mode'] + '-flag-' + str(flag), case
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



def verify_reader_case():
    values = deepcopy(FIXTURE)
    values.update(scenario_id='B10', configuration_id='C1')
    values['runtime'].update(mode='b10-abrupt-verify', phase='C1', repeat=1,
        run_id='b10d-1-1', loadgen_job_name='b10d-1-1-c1-r1-loadgen',
        b10_abrupt_diagnostic=None, bounds=deepcopy(DEFAULTS))
    return values


def verify_reader(document, values):
    jobs = [obj for obj in yaml.safe_load_all(document) if obj['kind'] == 'Job']
    assert len(jobs) == 1
    job = jobs[0]
    assert job['metadata']['name'] == 'b10d-1-1-c1-r1-b10-abrupt-verify'
    assert job['metadata']['labels']['parchwell.io/role'] == 'verifier'
    assert job['spec']['activeDeadlineSeconds'] == 750
    pod = job['spec']['template']['spec']
    assert pod['serviceAccountName'] == 'verifier'
    assert pod['nodeSelector'] == {'workload': 'workload'}
    assert all(volume['name'] != 'b10-abrupt-binding' for volume in pod['volumes'])
    container = pod['containers'][0]
    assert container['name'] == 'verifier' and container['image'] == values['runtime']['verifier_image']
    assert all(mount['name'] != 'b10-abrupt-binding' for mount in container['volumeMounts'])
    names = [entry['name'] for entry in container['env']]
    assert len(names) == len(set(names))
    assert {'Verifier__' + key for key in VERIFIER_REQUIRED['b10-abrupt-verify']} <= set(names)
    assert worker_setting_errors('b10-abrupt-verify', names) == []
    env = {entry['name']: entry.get('value') for entry in container['env']}
    assert env['Verifier__Mode'] == 'b10-abrupt-verify'
    assert env['Verifier__LoadGenImage'] == values['runtime']['loadgen_image']
    assert env['Verifier__VerifierImage'] == values['runtime']['verifier_image']
    assert env['Verifier__StartLineInputName'] == 'b10d-1-1/C1/r1/start-line.json'
    assert env['Verifier__ArtifactPrefix'] == values['runtime']['run_id'] + '/C1/r1/producer/'
    bounds = values['runtime']['bounds']
    assert env['Verifier__ScenarioRoot'] == scalar(bounds['verifier_kafka']['ScenarioRoot'])
    for key in ('MaxRowBytes', 'MaxRowsPerArtifact', 'MaxInventoryEntries', 'MaxInputEntries',
                'MaxNativeReportBytes', 'MaxBrokerEntries'):
        assert env['Verifier__' + key] == scalar(bounds['verifier_verify'][key])
    for key, value in bounds['scenario_parsing'].items():
        assert env['Verifier__ScenarioLoadOptions__' + key] == scalar(value)


def invalid_reader_cases():
    for key, value in [('scenario_id', 'B04'), ('configuration_id', 'C0')]:
        case = verify_reader_case(); case[key] = value
        yield key, case
    for key, value in [('phase', 'C0'), ('repeat', 2), ('run_id', 'b10-1-1'),
                       ('b10_abrupt_diagnostic', True), ('b10_abrupt_diagnostic', False),
                       ('b10_abrupt_binding_configmap', 'binding')]:
        case = verify_reader_case(); case['runtime'][key] = value
        yield key + '-' + str(value), case


def scientific_cases():
    for repeat in (1, 5):
        for original in diagnostic_cases():
            case = deepcopy(original)
            runtime = case['runtime']
            runtime.update(run_id='b10-1-1', repeat=repeat, b10_abrupt_diagnostic=False,
                           b10_abrupt_science=True, run_kind='batch',
                           loadgen_job_name=f'b10-1-1-c1-r{repeat}-loadgen')
            yield case


def scientific_schema_rejections():
    original = next(scientific_cases())
    for key, value in [('run_id', 'b10d-1-1'), ('repeat', 6),
                       ('b10_abrupt_diagnostic', True), ('b10_abrupt_science', False),
                       ('capture_pods', 'excluded')]:
        case = deepcopy(original); case['runtime'][key] = value
        yield key + '-' + str(value), case
    case = deepcopy(original); case['runtime']['bounds']['b10_abrupt'] = None
    yield 'missing-abrupt-bounds', case
    case = deepcopy(original); case['runtime']['mode'] = 'loadgen'
    case['runtime']['b10_abrupt_science'] = False
    yield 'binding-with-neither-flag', case
    for run_id, repeat in [('b10-1-1', 6), ('b10d-1-1', 2)]:
        case = verify_reader_case()
        case['runtime'].update(run_id=run_id, repeat=repeat,
                               loadgen_job_name=f'{run_id}-c1-r{repeat}-loadgen')
        yield 'reader-' + run_id + '-' + str(repeat), case


def verify_scientific(directory):
    positives = list(scientific_cases())
    for index, values in enumerate(positives):
        document = render(values, directory, 'science-' + str(index))
        job = next(obj for obj in yaml.safe_load_all(document) if obj['kind'] == 'Job')
        runtime = values['runtime']
        container = job['spec']['template']['spec']['containers'][0]
        env = {entry['name']: entry.get('value') for entry in container['env']}
        assert env['B10Abrupt__Purpose'] == 'scientific'
        assert env['B10Abrupt__Repeat'] == str(runtime['repeat'])
        suffix = 'restart' if runtime['mode'] == 'loadgen-restart' else 'successor'
        assert job['metadata']['name'] == runtime['loadgen_job_name'] + '-' + suffix
        assert container['args'] == ['--b10-abrupt-parent' if suffix == 'restart' else '--b10-abrupt-successor']
        assert job['spec']['activeDeadlineSeconds'] == 1140
        assert any(volume['name'] == 'b10-abrupt-binding' for volume in job['spec']['template']['spec']['volumes'])
        proc = run(['kubeconform', '-strict', '-summary', '-kubernetes-version', '1.35.0',
                    str(directory / ('science-' + str(index) + '.rendered.yaml'))])
        assert proc.returncode == 0, proc.stdout + proc.stderr
    reader = verify_reader_case()
    reader['runtime'].update(run_id='b10-1-1', repeat=5, loadgen_job_name='b10-1-1-c1-r5-loadgen')
    document = render(reader, directory, 'science-reader')
    job = next(obj for obj in yaml.safe_load_all(document) if obj['kind'] == 'Job')
    env = {entry['name']: entry.get('value') for entry in job['spec']['template']['spec']['containers'][0]['env']}
    assert job['metadata']['name'] == 'b10-1-1-c1-r5-b10-abrupt-verify'
    assert env['Verifier__ArtifactPrefix'] == 'b10-1-1/C1/r5/producer/'
    assert job['spec']['activeDeadlineSeconds'] == 750
    negatives = list(scientific_schema_rejections())
    for index, (name, values) in enumerate(negatives):
        source = directory / ('science-invalid-' + str(index) + '.yaml')
        with_case(values, source)
        proc = run(['helm', 'template', 'synthetic', 'charts/parchwell', '-f', str(source)])
        assert proc.returncode != 0, name
        assert "values don't meet the specifications" in proc.stderr, (name, proc.stderr)
    # Omission and explicit false are the same default-off runtime selection.
    absent = deepcopy(FIXTURE)
    explicit = deepcopy(absent); explicit['runtime']['b10_abrupt_science'] = False
    assert render(absent, directory, 'science-absent') == render(explicit, directory, 'science-false')
    print(f'b10 scientific chart checks passed: {len(positives) + 3} renders, {len(negatives)} schema rejections')

def main():
    schema = json.loads((CHART / 'values.schema.json').read_text())
    assert DEFAULTS['b10_abrupt'] == POLICY
    assert schema['properties']['runtime']['properties']['bounds']['properties']['b10_abrupt']['const'] == POLICY
    with tempfile.TemporaryDirectory(prefix='parchwell-b10-abrupt-chart-') as name:
        directory = Path(name)
        verify_scientific(directory)
        defaults = deepcopy(FIXTURE)
        explicit_false = deepcopy(defaults); explicit_false['runtime']['b10_abrupt_diagnostic'] = False
        ordinary = render(defaults, directory, 'defaults')
        assert ordinary == render(explicit_false, directory, 'explicit-false')
        ordinary_job = next(obj for obj in yaml.safe_load_all(ordinary) if obj and obj['kind'] == 'Job')
        assert 'command' not in ordinary_job['spec']['template']['spec']['containers'][0]
        for mode in ('loadgen', 'loadgen2'):
            legacy = deepcopy(diagnostic_cases()[0])
            legacy['runtime']['mode'] = mode
            del legacy['runtime']['b10_abrupt_diagnostic']
            del legacy['runtime']['b10_abrupt_binding_configmap']
            implicit = render(legacy, directory, 'legacy-' + mode)
            legacy['runtime']['b10_abrupt_diagnostic'] = False
            assert implicit == render(legacy, directory, 'legacy-false-' + mode)
        for case_name, values in zip(('first', 'successor'), diagnostic_cases(), strict=True):
            verify(render(values, directory, case_name), values)
            proc = run(['kubeconform', '-strict', '-summary', '-kubernetes-version', '1.35.0',
                        str(directory / (case_name + '.rendered.yaml'))])
            assert proc.returncode == 0, proc.stdout + proc.stderr
        reader = verify_reader_case()
        verify_reader(render(reader, directory, 'reader'), reader)
        proc = run(['kubeconform', '-strict', '-summary', '-kubernetes-version', '1.35.0',
                    str(directory / 'reader.rendered.yaml')])
        assert proc.returncode == 0, proc.stdout + proc.stderr
        reader_negatives = list(invalid_reader_cases())
        for case_name, values in reader_negatives:
            render(values, directory, 'reader-invalid-' + case_name, valid=False)
        negatives = list(invalid_cases())
        for index, (case_name, values) in enumerate(negatives):
            render(values, directory, str(index) + '-' + case_name, valid=False)
    print(f'b10 abrupt chart checks passed: 9 renders, {len(negatives) + len(reader_negatives)} invalid selections')


if __name__ == '__main__':
    main()

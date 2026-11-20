#!/usr/bin/env python3
"""Consumer member Jobs and consumer verifier modes: shape, environment names and rejected inputs."""
from copy import deepcopy
from pathlib import Path
import json
import re
import tempfile

import yaml

from test_chart import (CHART, ROOT, FIXTURE, DEFAULTS, run, with_case, expected_environment, jobname,
                        option_properties, worker_setting_errors)

PAIRS = (('C03', 'C0'), ('C10', 'C1'), ('C01', 'C0'))
MEMBERS = ('subject', 'witness')
ENRICHMENT_IMAGE = 'synthetic.azurecr.io/parchwell/enrichment@sha256:' + 'd' * 64
OPTIONS_FILE = 'src/Parchwell.Enrichment/ConsumerProcessing/ConsumerProcessingOptions.cs'
PROGRAM_FILE = 'src/Parchwell.Enrichment/Program.cs'
# Names the worker reads, in the order of plan section 3.4 rule 5.
WORKER_NAMES = (
    'PARCHWELL_SOURCE_COMMIT', 'PARCHWELL_JOB_NAME', 'PARCHWELL_POD_NAME', 'PARCHWELL_POD_UID', 'PARCHWELL_NODE_NAME',
    'ConsumerProcessing__RunId', 'ConsumerProcessing__ScenarioId', 'ConsumerProcessing__Phase',
    'ConsumerProcessing__RepeatNumber', 'ConsumerProcessing__Role', 'ConsumerProcessing__JobName',
    'ConsumerProcessing__PodName', 'ConsumerProcessing__PodUid', 'ConsumerProcessing__NodeName',
    'ConsumerProcessing__SourceCommit', 'ConsumerProcessing__NamespaceName', 'ConsumerProcessing__BlobEndpoint')
# Verifier modes that the chart still renders unchanged for consumer scenarios.
UNCHANGED_MODES = ('loadgen', 'judge')
SERVICE_ACCOUNTS = ('loadgen', 'verifier', 'enrichment-consumer')
REJECTED_MODES = ('loadgen2', 'loadgen-restart', 'loadgen-successor', 'b10-abrupt-verify', 'b10-sweep-line',
                  'b10-sweep', 'b10-final-sweep', 'single-action', 'single-action-judge', 'single-action-seed',
                  'single-action-v2', 'single-action-v2-judge', 'idle-observation', 'idle-observation-judge')


def run_id_for(item):
    # 29 characters: the longest run ID the schema admits.
    value = f"{item.lower()}-{'1' * 23}-1"
    assert len(value) == 29
    return value


def case(item, configuration, mode, repeat=1):
    values = deepcopy(FIXTURE)
    values.update(scenario_id=item, configuration_id=configuration)
    runtime = values['runtime']
    runtime.update(run_id=run_id_for(item), phase=configuration, mode=mode, repeat=repeat,
                   enrichment_image=ENRICHMENT_IMAGE, bounds=deepcopy(DEFAULTS))
    runtime['loadgen_job_name'] = f"{runtime['run_id']}-{configuration.lower()}-r{repeat}-loadgen"
    runtime['service_accounts']['enrichment-consumer'] = {
        'name': 'enrichment-consumer', 'client_id': 'synthetic-enrichment-consumer'}
    if mode == 'judge':
        runtime.pop('repeat')
        runtime.pop('loadgen_job_name')
    return values


def member_cases():
    for item, configuration in PAIRS:
        for member in MEMBERS:
            for repeat in (1, 5):
                yield case(item, configuration, f'consumer-{member}', repeat)


def verifier_cases():
    for item, configuration in PAIRS:
        for mode in ('start-line', 'verify'):
            yield case(item, configuration, mode)
    for mode in UNCHANGED_MODES:
        yield case('C03', 'C0', mode)


def jobs_of(document):
    return [o for o in yaml.safe_load_all(document) if o and o['kind'] == 'Job']


def job_name(values):
    runtime = values['runtime']
    return f"{runtime['run_id']}-{runtime['phase'].lower()}-r{runtime['repeat']}-{runtime['mode']}"


def expected_member_env(values):
    runtime = values['runtime']
    role = runtime['mode'].removeprefix('consumer-')
    name = job_name(values)
    pod_name, pod_uid, node_name = ('field', 'metadata.name'), ('field', 'metadata.uid'), ('field', 'spec.nodeName')
    return {
        'NativeMonitoring__BlobEndpoint': ('value', runtime['blob']['account_url']),
        'NativeMonitoring__BlobContainer': ('value', 'native-monitoring'),
        'NativeMonitoring__RunId': ('value', runtime['run_id']),
        'NativeMonitoring__ScenarioId': ('value', values['scenario_id']),
        'NativeMonitoring__JobName': ('value', name),
        'NativeMonitoring__PodUid': pod_uid,
        'NativeMonitoring__SourceCommit': ('value', runtime['source_commit']),
        'NativeMonitoring__Phase': ('value', runtime['phase']),
        'NativeMonitoring__ConfigurationId': ('value', values['configuration_id']),
        'Telemetry__ConnectionString': ('value', runtime['application_insights_connection_string']),
        'Telemetry__ServiceName': ('value', 'Parchwell.Enrichment'),
        'Telemetry__ServiceInstanceId': pod_name,
        'Telemetry__ScenarioId': ('value', values['scenario_id']),
        'Telemetry__RunId': ('value', runtime['run_id']),
        'PARCHWELL_SOURCE_COMMIT': ('value', runtime['source_commit']),
        'PARCHWELL_JOB_NAME': ('value', name),
        'PARCHWELL_POD_NAME': pod_name, 'PARCHWELL_POD_UID': pod_uid, 'PARCHWELL_NODE_NAME': node_name,
        'ConsumerProcessing__RunId': ('value', runtime['run_id']),
        'ConsumerProcessing__ScenarioId': ('value', values['scenario_id']),
        'ConsumerProcessing__Phase': ('value', runtime['phase']),
        'ConsumerProcessing__RepeatNumber': ('value', str(runtime['repeat'])),
        'ConsumerProcessing__Role': ('value', role),
        'ConsumerProcessing__JobName': ('value', name),
        'ConsumerProcessing__PodName': pod_name, 'ConsumerProcessing__PodUid': pod_uid,
        'ConsumerProcessing__NodeName': node_name,
        'ConsumerProcessing__SourceCommit': ('value', runtime['source_commit']),
        'ConsumerProcessing__NamespaceName': ('value', runtime['eventhubs']['namespace_name']),
        'ConsumerProcessing__BlobEndpoint': ('value', runtime['blob']['account_url']),
    }


def verify_member(document, values):
    objects = list(yaml.safe_load_all(document))
    jobs = jobs_of(document)
    assert len(jobs) == 1 and len(objects) == 4
    accounts = [o for o in objects if o['kind'] == 'ServiceAccount']
    assert {o['metadata']['name'] for o in accounts} == set(SERVICE_ACCOUNTS) and len(accounts) == 3
    runtime = values['runtime']
    for account in accounts:
        name = account['metadata']['name']
        assert account['metadata']['annotations']['azure.workload.identity/client-id'] == runtime['service_accounts'][name]['client_id']
        assert account['automountServiceAccountToken'] is False
    job = jobs[0]
    role = runtime['mode'].removeprefix('consumer-')
    name = job_name(values)
    assert job['metadata']['name'] == name and name.endswith(f"-r{runtime['repeat']}-consumer-{role}")
    assert len(name) <= 52 and job['metadata']['namespace'] == 'parchwell'
    labels = {'app.kubernetes.io/name': 'parchwell', 'app.kubernetes.io/instance': runtime['run_id'],
              'parchwell.io/role': 'enrichment-consumer'}
    assert job['metadata']['labels'] == labels
    spec = job['spec']
    assert spec['completions'] == 1 and spec['parallelism'] == 1 and spec['backoffLimit'] == 0
    assert spec['podReplacementPolicy'] == 'Failed' and spec['activeDeadlineSeconds'] == 1800
    assert runtime['bounds']['execution']['consumer_job_seconds'] == 1800
    template = spec['template']
    assert template['metadata']['labels'] == {**labels, 'azure.workload.identity/use': 'true'}
    pod = template['spec']
    assert pod['shareProcessNamespace'] is True and pod['restartPolicy'] == 'Never'
    assert pod['terminationGracePeriodSeconds'] == 30 and pod['automountServiceAccountToken'] is False
    assert pod['serviceAccountName'] == 'enrichment-consumer' and pod['nodeSelector'] == {'workload': 'workload'}
    assert 'tolerations' not in pod and not pod.get('initContainers')
    assert pod['securityContext'] == {'runAsUser': 1654, 'runAsGroup': 1654, 'fsGroup': 1654, 'runAsNonRoot': True,
                                      'seccompProfile': {'type': 'RuntimeDefault'}}
    assert {(v['name'], v.get('emptyDir', {}).get('sizeLimit')) for v in pod['volumes']} == {('work', '8Gi'), ('tmp', None)}
    assert len(pod['containers']) == 1
    container = pod['containers'][0]
    assert container['name'] == 'enrichment-consumer' and container['image'] == runtime['enrichment_image']
    assert container['imagePullPolicy'] == 'IfNotPresent'
    assert container['args'] == ['--consumer-processing'] and 'command' not in container
    assert container['securityContext'] == {'allowPrivilegeEscalation': False, 'readOnlyRootFilesystem': True,
                                            'capabilities': {'drop': ['ALL']}}
    assert container['resources'] == {'requests': {'cpu': '500m', 'memory': '1Gi'}, 'limits': {'cpu': '1500m', 'memory': '4Gi'}}
    assert {(m['name'], m['mountPath']) for m in container['volumeMounts']} == {('work', '/work'), ('tmp', '/tmp')}
    env = container['env']
    names = [entry['name'] for entry in env]
    expected = expected_member_env(values)
    assert len(names) == len(set(names)) == 31 and set(names) == set(expected), set(names) ^ set(expected)
    assert {name for name in names if name.startswith(('ConsumerProcessing__', 'PARCHWELL_'))} == set(WORKER_NAMES)
    assert len(WORKER_NAMES) == 17
    for entry in env:
        kind, value = expected[entry['name']]
        if kind == 'field':
            assert entry['valueFrom']['fieldRef']['fieldPath'] == value, entry['name']
        else:
            assert entry['value'] == value, (entry['name'], entry['value'], value)


def verify_verifier(document, values):
    objects = list(yaml.safe_load_all(document))
    jobs = jobs_of(document)
    assert len(jobs) == 1 and len(objects) == 4
    assert {o['metadata']['name'] for o in objects if o['kind'] == 'ServiceAccount'} == set(SERVICE_ACCOUNTS)
    job = jobs[0]
    runtime = values['runtime']
    mode = runtime['mode']
    assert job['metadata']['name'] == jobname(values)
    if mode == 'verify':
        assert job['metadata']['name'].endswith('-r1-verify')
    container = job['spec']['template']['spec']['containers'][0]
    env = {entry['name']: entry for entry in container['env']}
    assert len(env) == len(container['env'])
    expected = expected_environment(values)
    if mode in ('start-line', 'verify'):
        expected['Verifier__ConsumerRecordContainer'] = ('value', 'consumer-record')
        if mode == 'verify':
            expected['Verifier__Mode'] = ('value', 'consumer-verify')
    assert set(env) == set(expected), set(env) ^ set(expected)
    for name, entry in env.items():
        kind, value = expected[name]
        if kind == 'field':
            assert entry['valueFrom']['fieldRef']['fieldPath'] == value, name
        else:
            assert entry['value'] == value, (name, entry['value'], value)
    if mode in ('start-line', 'verify'):
        assert worker_setting_errors(mode, list(env)) == []
    else:
        assert 'Verifier__ConsumerRecordContainer' not in env
        assert env.get('Verifier__Mode', {}).get('value') != 'consumer-verify'


def worker_option_check():
    """T21: the rendered ConsumerProcessing names are exactly the options class properties."""
    properties = option_properties(OPTIONS_FILE, 'internal sealed class ConsumerProcessingOptions')
    assert properties == {'RunId', 'ScenarioId', 'Phase', 'RepeatNumber', 'Role', 'JobName', 'PodName', 'PodUid',
                          'NodeName', 'SourceCommit', 'NamespaceName', 'BlobEndpoint'}, properties
    options_text = (ROOT / OPTIONS_FILE).read_text()
    deployment = set(re.findall(r'configuration\["(PARCHWELL_[A-Z_]+)"\]', options_text))
    assert deployment == {'PARCHWELL_SOURCE_COMMIT', 'PARCHWELL_JOB_NAME', 'PARCHWELL_POD_NAME', 'PARCHWELL_POD_UID',
                          'PARCHWELL_NODE_NAME'}, deployment
    assert re.search(r'args is \["--consumer-processing"\]', (ROOT / PROGRAM_FILE).read_text())
    return {f'ConsumerProcessing__{name}' for name in properties} | deployment


def storage_check_cases():
    for item, configuration in PAIRS:
        for attempt in (1, 31):
            yield case(item, configuration, 'consumer-storage-check', attempt)


def verify_storage_check(document, values):
    """The check Job runs the Enrichment image under the enrichment-consumer account with the check argument and its own environment."""
    runtime = values['runtime']
    jobs = jobs_of(document)
    assert len(jobs) == 1
    job = jobs[0]
    name = f"{runtime['run_id']}-{runtime['phase'].lower()}-storage-check-a{runtime['repeat']}"
    assert job['metadata']['name'] == name and len(name) <= 63
    assert job['spec']['activeDeadlineSeconds'] == 120 and job['spec']['backoffLimit'] == 0
    pod = job['spec']['template']['spec']
    assert pod['serviceAccountName'] == 'enrichment-consumer' and pod['automountServiceAccountToken'] is False
    assert job['spec']['template']['metadata']['labels']['azure.workload.identity/use'] == 'true'
    assert pod['restartPolicy'] == 'Never' and pod['nodeSelector'] == {'workload': 'workload'}
    container, = pod['containers']
    assert container['image'] == runtime['enrichment_image'] and container['args'] == ['--consumer-storage-check'] and 'command' not in container
    assert container['securityContext']['readOnlyRootFilesystem'] is True and container['securityContext']['allowPrivilegeEscalation'] is False
    env = {e['name']: e['value'] for e in container['env']}
    assert env == {'ConsumerStorageCheck__RunId': runtime['run_id'], 'ConsumerStorageCheck__Phase': runtime['phase'],
                   'ConsumerStorageCheck__JobName': name, 'ConsumerStorageCheck__BlobEndpoint': runtime['blob']['account_url'],
                   'PARCHWELL_JOB_NAME': name}, env
    # The worker binds every property of its Options class (unknown names are rejected) and cross-checks PARCHWELL_JOB_NAME.
    source = (ROOT / 'src/Parchwell.Enrichment/ConsumerProcessing/ConsumerStorageCheck.cs').read_text()
    properties = set(re.findall(r'public string (\w+) \{ get; set; \}', source))
    assert properties == {key.split('__')[1] for key in env if key.startswith('ConsumerStorageCheck__')}, properties
    assert re.findall(r'configuration\["(PARCHWELL_\w+)"\]', source) == ['PARCHWELL_JOB_NAME']
    assert re.search(r'args is \["--consumer-storage-check"\]', (ROOT / PROGRAM_FILE).read_text())


def storage_check_negatives():
    good = case('C03', 'C0', 'consumer-storage-check', 2)

    def mutate(change):
        bad = deepcopy(good)
        change(bad)
        return bad
    return [
        mutate(lambda d: d['runtime'].pop('enrichment_image')),
        mutate(lambda d: d['runtime']['service_accounts'].pop('enrichment-consumer')),
        mutate(lambda d: d['runtime'].pop('repeat')),
        mutate(lambda d: d['runtime'].update(phase='C1')),
        mutate(lambda d: d.update(scenario_id='A01')),
        mutate(lambda d: d.update(scenario_id='C02')),
        mutate(lambda d: (d.update(configuration_id='C2'), d['runtime'].update(phase='C2'))),
    ]


def negatives():
    def mutate(base, change):
        bad = deepcopy(base)
        change(bad)
        return bad
    good = case('C03', 'C0', 'consumer-subject')
    witness = case('C10', 'C1', 'consumer-witness', 5)
    bad_cases = [
        mutate(good, lambda d: d.update(scenario_id='A01')),
        mutate(good, lambda d: d.update(scenario_id='B04', configuration_id='C1', runtime={**d['runtime'], 'phase': 'C1'})),
        mutate(good, lambda d: d.update(scenario_id='C02')),
        mutate(good, lambda d: (d.update(configuration_id='C2'), d['runtime'].update(phase='C2'))),
        mutate(good, lambda d: (d.update(configuration_id='C3'), d['runtime'].update(phase='C3'))),
        mutate(good, lambda d: d['runtime'].update(phase='probe')),
        mutate(good, lambda d: d['runtime'].update(phase='C1')),
        mutate(witness, lambda d: d['runtime'].update(phase='C0')),
        mutate(good, lambda d: d['runtime'].pop('enrichment_image')),
        mutate(good, lambda d: d['runtime'].update(enrichment_image='synthetic.azurecr.io/parchwell/enrichment:latest')),
        mutate(good, lambda d: d['runtime']['service_accounts'].pop('enrichment-consumer')),
        mutate(good, lambda d: d['runtime']['service_accounts'].update(probe={'name': 'probe', 'client_id': 'synthetic-probe'})),
        mutate(good, lambda d: d['runtime'].pop('repeat')),
        mutate(good, lambda d: d['runtime'].update(repeat=6)),
        mutate(good, lambda d: d['runtime'].update(loadgen_job_name='other-c0-r1-loadgen')),
        mutate(good, lambda d: d['runtime'].update(image_smoke=True)),
    ]
    # Every mode of a consumer scenario needs the enrichment image and the three service accounts.
    for mode in ('start-line', 'loadgen', 'verify', 'judge'):
        bad_cases.append(mutate(case('C03', 'C0', mode), lambda d: d['runtime'].pop('enrichment_image')))
        bad_cases.append(mutate(case('C03', 'C0', mode), lambda d: d['runtime']['service_accounts'].pop('enrichment-consumer')))
    # Modes that the consumer scenarios do not use.
    for mode in REJECTED_MODES:
        bad_cases.append(mutate(case('C03', 'C0', mode), lambda d: None))
    bad_cases.append(mutate(case('C03', 'C0', 'b10-sweep'), lambda d: (d['runtime'].pop('repeat'), d['runtime'].pop('loadgen_job_name'))))
    # The enrichment-consumer account stays rejected for scenarios that are not consumer scenarios.
    a01 = case('A01', 'C0', 'loadgen')
    a01['runtime'].pop('enrichment_image')
    bad_cases.append(a01)
    return bad_cases


def render(directory, name, values):
    fixture = directory / f'{name}.yaml'
    with_case(values, fixture)
    return run(['helm', 'template', 'synthetic', str(CHART), '--namespace', 'parchwell', '-f', str(fixture)])


def main():
    schema = json.loads((CHART / 'values.schema.json').read_text())
    execution = schema['properties']['runtime']['properties']['bounds']['properties']['execution']['const']
    assert execution['consumer_job_seconds'] == 1800 == DEFAULTS['execution']['consumer_job_seconds']
    assert {'C01', 'C03', 'C10'} <= set(schema['properties']['scenario_id']['enum'])
    worker_names = worker_option_check()
    assert worker_names == set(WORKER_NAMES), worker_names ^ set(WORKER_NAMES)
    members = list(member_cases())
    verifiers = list(verifier_cases())
    assert len(members) == 12 and len(verifiers) == 8
    with tempfile.TemporaryDirectory(prefix='consumer-chart-') as name:
        directory = Path(name)
        for index, values in enumerate(members + verifiers):
            fixture = directory / f'{index}.yaml'
            with_case(values, fixture)
            lint = run(['helm', 'lint', str(CHART), '-f', str(fixture)])
            assert lint.returncode == 0, (index, lint.stdout, lint.stderr)
            rendered = render(directory, f'{index}', values)
            assert rendered.returncode == 0, (index, rendered.stderr)
            if index < len(members):
                verify_member(rendered.stdout, values)
                job = jobs_of(rendered.stdout)[0]
                rendered_names = {e['name'] for e in job['spec']['template']['spec']['containers'][0]['env']}
                assert {n for n in rendered_names if n.startswith('ConsumerProcessing__') or n.startswith('PARCHWELL_')} == worker_names
            else:
                verify_verifier(rendered.stdout, values)
            output = directory / f'{index}.rendered.yaml'
            output.write_text(rendered.stdout)
            checked = run(['kubeconform', '-strict', '-summary', '-kubernetes-version', '1.35.0', str(output)])
            assert checked.returncode == 0, checked.stdout + checked.stderr
        for index, values in enumerate(storage_check_cases()):
            rendered = render(directory, f'storage-check-{index}', values)
            assert rendered.returncode == 0, (index, rendered.stderr)
            verify_storage_check(rendered.stdout, values)
            output = directory / f'storage-check-{index}.rendered.yaml'
            output.write_text(rendered.stdout)
            checked = run(['kubeconform', '-strict', '-summary', '-kubernetes-version', '1.35.0', str(output)])
            assert checked.returncode == 0, checked.stdout + checked.stderr
        bad_cases = negatives() + storage_check_negatives()
        for index, values in enumerate(bad_cases):
            rendered = render(directory, f'negative-{index}', values)
            assert rendered.returncode != 0, index
            assert not jobs_of(rendered.stdout), index
    print(f'consumer chart acceptance passed: {len(members) + len(verifiers)} positive, {len(bad_cases)} negative, '
          f'{len(worker_names)} worker names checked against the options class')


if __name__ == '__main__':
    main()

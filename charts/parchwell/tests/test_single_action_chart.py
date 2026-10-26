#!/usr/bin/env python3
"""Finite smoke/live single-action deployment contract, independent of the phase matrix."""
from copy import deepcopy
from pathlib import Path
import json
import tempfile

import yaml

from test_chart import CHART, ROOT, FIXTURE, DEFAULTS, run, with_case
import re

# Transcribed from the approved #212 Revision 3 plan; do not import phase.py.
CONFIGURATIONS = (
    ('B01', 'C0', 'loadgen'), ('B01', 'C1', 'loadgen'),
    ('B03', 'C1', 'loadgen'), ('B03', 'C2', 'loadgen'),
    ('B03', 'C3', 'loadgen'), ('B03', 'C4', 'loadgen'),
    ('B05', 'C1', 'loadgen'),
    ('C08', 'C0', 'probe'), ('C08', 'C1', 'probe'),
    ('C12', 'C1', 'probe'), ('C12', 'C2', 'probe'),
    ('F04', 'C0', 'loadgen'), ('F04', 'C1', 'loadgen'),
    ('F04', 'C2', 'probe'), ('F04', 'C3', 'loadgen'),
    ('F04', 'C4', 'probe'),
)
EXECUTION = {
    'action_job_seconds': 1980,
    'judge_job_seconds': 120,
    'seed_job_seconds': 420,
    'termination_grace_seconds': 30,
}
ROLES = ('loadgen', 'verifier', 'probe', 'enrichment-consumer')


def case(item, configuration, mode, role, smoke=True):
    values = deepcopy(FIXTURE)
    values.update(scenario_id=item, configuration_id=configuration)
    runtime = values['runtime']
    runtime.update(run_id='probe-1-1', phase=configuration, mode=mode,
                   action_role=role, image_smoke=smoke,
                   enrichment_image="synthetic.azurecr.io/parchwell/enrichment@sha256:" + "d" * 64,
                   bounds=deepcopy(DEFAULTS))
    if not smoke and item == "F04" and mode == "single-action-judge":
        runtime["seed_receipt_configmap"] = runtime["run_id"] + "-f04-seed-receipt"
    runtime.pop('repeat')
    runtime.pop('loadgen_job_name')
    for account_role in ROLES:
        runtime['service_accounts'][account_role] = {
            'name': account_role, 'client_id': f'synthetic-{account_role}',
            'subject': f'system:serviceaccount:parchwell:{account_role}',
        }
    runtime['blob']['containers']['single_action'] = 'single-action'
    return values


def positive_cases(smoke=True):
    for item, configuration, role in CONFIGURATIONS:
        yield case(item, configuration, 'single-action', role, smoke)
        yield case(item, configuration, 'single-action-judge', 'verifier', smoke)
    yield case('F04', 'C2', 'single-action-seed', 'enrichment-consumer', smoke)


def job_name(values):
    runtime = values['runtime']
    if runtime['mode'] == 'single-action-seed':
        return f"{runtime['run_id']}-f04-seed"
    name = (f"{runtime['run_id']}-{values['scenario_id'].lower()}-"
            f"{values['configuration_id'].lower()}-single")
    return name + ('-judge' if runtime['mode'] == 'single-action-judge' else '')


def verify(document, values):
    runtime = values['runtime']
    role = runtime['action_role']
    judge = runtime['mode'] == 'single-action-judge'
    seed = runtime['mode'] == 'single-action-seed'
    worker = 'enrichment' if seed else 'verifier' if judge else 'loadgen'
    mounted = not runtime['image_smoke'] and judge and values['scenario_id'] == 'F04'
    objects = list(yaml.safe_load_all(document))
    assert len(objects) == 5
    assert sorted(obj['kind'] for obj in objects) == ['Job'] + ['ServiceAccount'] * 4
    accounts = [obj for obj in objects if obj['kind'] == 'ServiceAccount']
    assert {account['metadata']['name'] for account in accounts} == set(ROLES)
    for account in accounts:
        assert account['metadata']['namespace'] == 'parchwell'
        expected = runtime['service_accounts'][account['metadata']['name']]
        assert account['metadata']['annotations'] == {
            'azure.workload.identity/client-id': expected['client_id']}
        assert account['automountServiceAccountToken'] is False
    job = next(obj for obj in objects if obj['kind'] == 'Job')
    assert job['metadata']['name'] == job_name(values)
    assert job['metadata']['namespace'] == 'parchwell'
    labels = {'app.kubernetes.io/name': 'parchwell',
              'app.kubernetes.io/instance': runtime['run_id'],
              'parchwell.io/role': role}
    assert job['metadata']['labels'] == labels
    assert not job['metadata'].get('annotations')
    spec = job['spec']
    assert spec['completions'] == spec['parallelism'] == 1
    assert spec['backoffLimit'] == 0
    assert spec.get('podReplacementPolicy') == (None if judge else 'Failed')
    assert spec['activeDeadlineSeconds'] == (420 if seed else 120 if judge else 1980)
    assert spec['template']['metadata']['labels'] == {
        **labels, 'azure.workload.identity/use': 'true'}
    pod = spec['template']['spec']
    assert pod['serviceAccountName'] == role
    assert pod['automountServiceAccountToken'] is False
    assert pod['restartPolicy'] == 'Never'
    assert pod['terminationGracePeriodSeconds'] == 30
    assert pod['nodeSelector'] == {'workload': 'loadgen' if role == 'loadgen' else 'workload'}
    assert pod.get('tolerations', []) == ([{
        'key': 'workload', 'operator': 'Equal', 'value': 'loadgen',
        'effect': 'NoSchedule'}] if role == 'loadgen' else [])
    assert pod['securityContext'] == {
        'runAsUser': 1654, 'runAsGroup': 1654, 'fsGroup': 1654,
        'runAsNonRoot': True, 'seccompProfile': {'type': 'RuntimeDefault'}}
    assert not pod.get('initContainers')
    assert pod['volumes'] == [
        {'name': 'work', 'emptyDir': {'sizeLimit': '8Gi'}},
        {'name': 'tmp', 'emptyDir': {}}] + ([{'name': 'seed-receipt', 'configMap': {
            'name': runtime['run_id'] + '-f04-seed-receipt', 'optional': True,
            'items': [{'key': 'receipt.json', 'path': 'receipt.json'}, {'key': 'pod.json', 'path': 'pod.json'}]}}] if mounted else [])
    assert len(pod['containers']) == 1
    container = pod['containers'][0]
    assert container['name'] == role
    assert container['image'] == runtime[worker + '_image']
    assert container['imagePullPolicy'] == 'IfNotPresent'
    assert container.get('args') == (['--image-smoke'] if runtime['image_smoke'] else ['--single-action-seed'] if seed else None if judge else ['--single-action'])
    assert 'command' not in container
    assert container['securityContext'] == {
        'allowPrivilegeEscalation': False, 'readOnlyRootFilesystem': True,
        'capabilities': {'drop': ['ALL']}}
    assert container['resources'] == {
        'requests': {'cpu': '1', 'memory': '2Gi'},
        'limits': {'cpu': '3', 'memory': '12Gi'}}
    assert container['volumeMounts'] == [
        {'name': 'work', 'mountPath': '/work'},
        {'name': 'tmp', 'mountPath': '/tmp'}] + ([{'name': 'seed-receipt', 'mountPath': '/run/seed', 'readOnly': True}] if mounted else [])
    expected = {
        'PARCHWELL_SOURCE_COMMIT': {'value': runtime['source_commit']},
        'PARCHWELL_JOB_NAME': {'value': job_name(values)},
        'PARCHWELL_POD_NAME': {'valueFrom': {'fieldRef': {'fieldPath': 'metadata.name'}}},
        'PARCHWELL_POD_UID': {'valueFrom': {'fieldRef': {'fieldPath': 'metadata.uid'}}},
        'Telemetry__ConnectionString': {'value': runtime['application_insights_connection_string']},
        'Telemetry__ServiceName': {'value': 'Parchwell.Verifier' if judge else 'Parchwell.LoadGen'},
        'Telemetry__ServiceInstanceId': {'valueFrom': {'fieldRef': {'fieldPath': 'metadata.name'}}},
        'Telemetry__ScenarioId': {'value': values['scenario_id']},
        'Telemetry__RunId': {'value': runtime['run_id']},
    }
    if seed:
        expected = {key: value for key, value in expected.items() if not key.startswith('Telemetry__')}
    if not runtime['image_smoke']:
        expected.update(expected_live_environment(values))
    environment = container['env']
    assert len(environment) == len(expected)
    assert {entry['name']: {k: v for k, v in entry.items() if k != 'name'}
            for entry in environment} == expected


# Independently transcribed approved limits and complete input inventories.
CAPTURE = dict(ProcessLifetimeSeconds=1920, PostTerminalStatisticsSeconds=20, CleanupSeconds=10,
    RegistrationUploadSeconds=30, CaptureUploadSeconds=30, HostCleanupSeconds=10, ReceiptUploadSeconds=30,
    PollSliceMs=100, MaxCaptureBytes=134217728, MaxRegistrationBytes=65536, MaxCompletionBytes=65536,
    MaxStatisticsReportBytes=262144, MaxStatisticsReports=64, MaxClientErrors=64, MaxErrorBytes=4096,
    MaxDiagnostics=100, MaxDiagnosticBytes=2048, CallbackQueueCapacity=128, MaxSettingsKeys=128,
    MaxSettingsBytes=16384, MaxNonmatchingRecords=4096, MaxInputDepth=32)
JUDGE = dict(MaxCaptureBytes=134217728, MaxRegistrationBytes=65536, MaxCompletionBytes=65536,
    MaxSeedReceiptBytes=65536, MaxSeedPodBytes=65536, MaxCandidateBytes=16777216,
    MaxTotalInputBytes=268435456, MaxInputDepth=32, MaxSettingsKeys=128, MaxSettingsBytes=16384,
    MaxErrorBytes=4096, MaxDiagnosticBytes=2048, MaxDiagnostics=100, MaxClientErrors=64,
    MaxStatisticsReports=64, MaxStatisticsReportBytes=262144, CallbackQueueCapacity=128,
    MaxBrokerEntries=64, MaxMatchedValueBytes=1100000, MaxNonmatchingRecords=4096,
    ProcessLifetimeSeconds=90, IoSeconds=10, CleanupSeconds=10)
PARSING = dict(MaxDocumentCharacters=1048576, MaxNestingDepth=32, MaxScalarCharacters=65536, MaxParserEvents=100000)
ACTION_INPUTS = {'ScenarioId', 'ConfigurationId', 'RunId', 'SourceCommit', 'JobName', 'PodName', 'PodUid',
    'NamespaceName', 'BlobEndpoint', 'BlobContainer', 'ScenarioRoot', 'ScenarioLoadOptions', *CAPTURE}
SEED_INPUTS = {'RunId', 'SourceCommit', 'JobName', 'PodName', 'PodUid', 'NamespaceName', 'ActionWindowMs',
    'ProcessLifetimeSeconds', 'CleanupSeconds', 'HostCleanupSeconds', 'MaxReceiptBytes'}
JUDGE_INPUTS = {'Mode', 'ScenarioId', 'ConfigurationId', 'RunId', 'VerifierCommit', 'JobName', 'PodName',
    'PodUid', 'BlobServiceUri', 'VerifierContainer', 'NamespaceName', 'ScenarioRoot', 'LoadGenImage',
    'VerifierImage', 'EnrichmentImage', 'CaptureContainer', 'CapturePrefix', 'SingleActionLimits', 'ScenarioLoadOptions'}

def declared_properties(relative):
    text = (ROOT / relative).read_text()
    return set(re.findall(r'public\s+[^\n{};]+?\s+(\w+)\s*\{\s*get;\s*set;\s*}', text))

def verify_option_inventories():
    action = declared_properties('src/Parchwell.LoadGen/SingleAction/SingleActionHostOptions.cs')
    seed = declared_properties('src/Parchwell.Enrichment/SeedF04/SeedF04Options.cs')
    judge = declared_properties('src/Parchwell.Verifier/VerifierHostOptions.cs')
    assert ACTION_INPUTS == action, ACTION_INPUTS ^ action
    assert SEED_INPUTS == seed, SEED_INPUTS ^ seed
    assert JUDGE_INPUTS | {'SeedReceiptPath', 'SeedPodIdentityPath'} <= judge
    assert set(JUDGE) == declared_properties('src/Parchwell.Verifier/Common/SingleAction/SingleActionLimits.cs')

def expected_live_environment(values):
    r = values['runtime']; mode = r['mode']; item = values['scenario_id']; config = values['configuration_id']
    role = next(role for scenario, configuration, role in CONFIGURATIONS if (scenario, configuration) == (item, config))
    container = 'ledger' if role == 'loadgen' else 'single-action'
    if mode == 'single-action':
        prefix = 'SingleAction'
        fields = dict(ScenarioId=item, ConfigurationId=config, RunId=r['run_id'], SourceCommit=r['source_commit'],
            JobName=job_name(values), NamespaceName=r['eventhubs']['namespace_name'], BlobEndpoint=r['blob']['account_url'],
            BlobContainer=container, ScenarioRoot='/app', **CAPTURE)
    elif mode == 'single-action-seed':
        prefix = 'SeedF04'
        fields = dict(RunId=r['run_id'], SourceCommit=r['source_commit'], JobName=job_name(values),
            NamespaceName=r['eventhubs']['namespace_name'], ActionWindowMs=330000, ProcessLifetimeSeconds=390,
            CleanupSeconds=10, HostCleanupSeconds=10, MaxReceiptBytes=65536)
    else:
        prefix = 'Verifier'
        fields = dict(Mode='single-action-judge', ScenarioId=item, ConfigurationId=config, RunId=r['run_id'],
            VerifierCommit=r['source_commit'], JobName=job_name(values), BlobServiceUri=r['blob']['account_url'],
            VerifierContainer='verifier', NamespaceName=r['eventhubs']['namespace_name'], ScenarioRoot='/app',
            LoadGenImage=r['loadgen_image'], VerifierImage=r['verifier_image'], EnrichmentImage=r['enrichment_image'],
            CaptureContainer=container, CapturePrefix=f"{r['run_id']}/single-action/{item}/{config}/")
        fields.update({'SingleActionLimits__' + key: value for key, value in JUDGE.items()})
        if item == 'F04':
            fields.update(SeedReceiptPath='/run/seed/receipt.json', SeedPodIdentityPath='/run/seed/pod.json')
    if mode != 'single-action-seed':
        fields.update({'ScenarioLoadOptions__' + key: value for key, value in PARSING.items()})
    expected = {prefix + '__' + key: {'value': str(value)} for key, value in fields.items()}
    expected[prefix + '__PodName'] = {'valueFrom': {'fieldRef': {'fieldPath': 'metadata.name'}}}
    expected[prefix + '__PodUid'] = {'valueFrom': {'fieldRef': {'fieldPath': 'metadata.uid'}}}
    return expected


def main():
    verify_option_inventories()
    schema = json.loads((CHART / 'values.schema.json').read_text())
    bounds = schema['properties']['runtime']['properties']['bounds']
    assert bounds['properties']['single_action_execution']['const'] == EXECUTION
    defaults = yaml.safe_load((CHART / 'values.yaml').read_text())
    assert defaults['runtime']['bounds']['single_action_execution'] == EXECUTION
    for name, expected in [('single_action_capture', CAPTURE), ('single_action_judge', JUDGE)]:
        assert defaults['runtime']['bounds'][name] == expected
        assert bounds['properties'][name]['const'] == expected
    positives = list(positive_cases()) + list(positive_cases(False))
    assert len(CONFIGURATIONS) == 16 and len(positives) == 66
    assert len({job_name(values) for values in positives}) == 33
    assert job_name(case('C12', 'C2', 'single-action-judge', 'verifier')) == 'probe-1-1-c12-c2-single-judge'
    with tempfile.TemporaryDirectory(prefix='parchwell-single-action-') as temporary:
        directory = Path(temporary)
        for index, values in enumerate(positives):
            fixture = directory / f'{index}.yaml'
            output = directory / f'{index}.rendered.yaml'
            with_case(values, fixture)
            lint = run(['python3', 'infra/scripts/lifecycle/command.py', '120',
                        '--stdout-file', str(directory / f'{index}.lint'), '--max-bytes', '4194304',
                        '--', 'helm', 'lint', str(CHART), '-f', str(fixture)])
            assert lint.returncode == 0, lint.stderr
            render = run(['python3', 'infra/scripts/lifecycle/command.py', '120',
                          '--stdout-file', str(output), '--max-bytes', '4194304',
                          '--', 'helm', 'template', 'synthetic', str(CHART),
                          '--namespace', 'parchwell', '-f', str(fixture)])
            assert render.returncode == 0 and not Path(str(output) + '.incomplete').exists(), render.stderr
            valid = run(['python3', 'infra/scripts/lifecycle/command.py', '120', '--',
                         'kubeconform', '-strict', '-summary', '-kubernetes-version', '1.35.0', str(output)])
            assert valid.returncode == 0, valid.stdout + valid.stderr
            verify(output.read_text(), values)
        negatives = []

        def mutate(source, mutation):
            values = deepcopy(source)
            mutation(values)
            negatives.append(values)

        mutate(positives[0], lambda d: d['runtime'].update(action_role='probe'))
        mutate(positives[1], lambda d: d['runtime'].update(action_role='probe'))
        mutate(positives[-1], lambda d: d['runtime'].update(action_role='probe'))
        mutate(positives[-1], lambda d: d.update(scenario_id='C12'))
        mutate(case('C08', 'C0', 'single-action', 'probe'), lambda d: d.update(scenario_id='C12'))
        mutate(positives[0], lambda d: d.update(scenario_id='A01'))
        mutate(positives[0], lambda d: d['runtime'].update(image_smoke='false'))
        mutate(FIXTURE, lambda d: d['runtime'].update(action_role='loadgen'))
        mutate(positives[0], lambda d: d['runtime'].update(enrichment_image='synthetic/enrichment:latest'))
        mutate(case('F04', 'C4', 'single-action-judge', 'verifier', False), lambda d: d['runtime'].pop('seed_receipt_configmap'))
        mutate(case('F04', 'C4', 'single-action-judge', 'verifier', False), lambda d: d['runtime'].update(seed_receipt_configmap='other-f04-seed-receipt'))
        mutate(case('B01', 'C0', 'single-action', 'loadgen', False), lambda d: d['runtime'].update(seed_receipt_configmap='probe-1-1-f04-seed-receipt'))
        mutate(positives[0], lambda d: d['runtime'].update(auth_audience='forbidden'))
        mutate(positives[0], lambda d: d['runtime'].update(bounds={**deepcopy(DEFAULTS), 'single_action_capture': {**CAPTURE, 'MaxCaptureBytes': 1}}))
        assert len(negatives) == 14
        for index, values in enumerate(negatives):
            fixture = directory / f'negative-{index}.yaml'
            output = directory / f'negative-{index}.rendered.yaml'
            with_case(values, fixture)
            render = run(['python3', 'infra/scripts/lifecycle/command.py', '120',
                          '--stdout-file', str(output), '--max-bytes', '4194304', '--',
                          'helm', 'template', 'synthetic', str(CHART), '-f', str(fixture)])
            marker = Path(str(output) + '.incomplete')
            assert render.returncode != 0 and marker.exists(), (index, render.stderr)
            assert marker.read_text().strip() == 'child_exit_1'
            if index < 5:
                assert 'single-action mode/item/configuration/role mismatch' in render.stderr
            if output.exists():
                assert not any(obj and obj.get('kind') == 'Job'
                               for obj in yaml.safe_load_all(output.read_text()))
    print('single-action chart acceptance passed: 66 positive, 14 negative')


if __name__ == '__main__':
    main()

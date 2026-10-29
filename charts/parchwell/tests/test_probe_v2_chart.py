#!/usr/bin/env python3
"""Probe V2's finite stage, identity and evidence-mount contract."""
from copy import deepcopy
from pathlib import Path
import json
import tempfile

import yaml

from test_chart import CHART, ROOT, FIXTURE, DEFAULTS, run, with_case
from test_single_action_chart import case as legacy_case

LIMITS = json.loads((ROOT / 'docs/operations/probe-v2-runtime-limits.json').read_text())
ACCOUNTS = ('loadgen', 'verifier', 'probe', 'enrichment-consumer')


def stages():
    for item in ('B08', 'E01', 'F06'):
        for number in range(12 if item == 'F06' else 2):
            config = f'C{number}'
            if item == 'B08':
                rows = [('action', 'loadgen'), ('read', 'probe')]
            elif item == 'E01':
                rows = [('seed', 'enrichment-consumer'), ('seedread', 'probe'),
                        ('action', 'probe'), ('read', 'probe')]
            elif number < 6:
                rows = [('action', 'loadgen' if number % 2 == 0 else 'probe'), ('read', 'probe')]
            elif number < 9:
                rows = [('seed', 'loadgen' if number == 6 else 'enrichment-consumer'), ('action', 'probe')]
            else:
                rows = [('action', 'enrichment-consumer')]
            yield from ((item, config, stage, role) for stage, role in rows)
            yield item, config, 'judge', 'verifier'


def case(item, config, stage, role, image_smoke=False, namespace_roles=True):
    values = deepcopy(FIXTURE)
    values.update(scenario_id=item, configuration_id=config)
    runtime = values['runtime']
    for key in ('repeat', 'loadgen_job_name'):
        runtime.pop(key)
    runtime.update(run_id='probe-2-1', phase=config, stage=stage,
                   mode='single-action-v2-judge' if stage == 'judge' else 'single-action-v2',
                   action_role=role, image_smoke=image_smoke,
                   probe_namespace_data_roles=namespace_roles, bounds=deepcopy(DEFAULTS))
    runtime['service_accounts'] = {account: {
        'name': account, 'client_id': f'00000000-0000-0000-0000-{ACCOUNTS.index(account) + 1:012d}',
        'subject': f'system:serviceaccount:parchwell:{account}'} for account in ACCOUNTS}
    runtime['blob']['containers']['single_action'] = 'single-action'
    return values


def positive_cases(image_smoke=False):
    return (case(*row, image_smoke=image_smoke) for row in stages())


def job_name(values, config=None, stage=None):
    runtime = values['runtime']
    return '-'.join((runtime['run_id'], values['scenario_id'].lower(),
                     (config or values['configuration_id']).lower(), stage or runtime['stage']))


def dependencies(values):
    item, config, stage = values['scenario_id'], values['configuration_id'], values['runtime']['stage']
    number = int(config[1:])
    cross = [job_name(values, f'C{number - 3}', s) for s in ('seed', 'action')] if item == 'F06' and number >= 9 else []
    if stage == 'judge':
        own = [s for i, c, s, _ in stages() if i == item and c == config and s != 'judge']
        return [job_name(values, stage=s) for s in own] + cross
    if item == 'E01':
        own = {'seed': [], 'seedread': ['seed'], 'action': ['seed', 'seedread'],
               'read': ['seed', 'seedread', 'action']}[stage]
        return [job_name(values, stage=s) for s in own]
    if stage == 'read':
        return [job_name(values, stage='action')]
    if item == 'F06' and number >= 6 and stage == 'action':
        return cross or [job_name(values, stage='seed')]
    return []


def expected_env(values):
    runtime = values['runtime']; judge = runtime['stage'] == 'judge'
    prefix = 'ProbeV2Judge__' if judge else 'ProbeV2__'
    role = runtime['action_role']
    producer = runtime['stage'] == 'seed' or (runtime['stage'] == 'action' and
        (values['scenario_id'] != 'F06' or int(values['configuration_id'][1:]) < 6))
    fields = dict(ScenarioId=values['scenario_id'], ConfigurationId=values['configuration_id'],
        RunId=runtime['run_id'], SourceCommit=runtime['source_commit'], JobName=job_name(values),
        NamespaceName=runtime['eventhubs']['namespace_name'], ServiceAccount=role,
        WorkloadIdentityClientId=runtime['service_accounts'][role]['client_id'], ScenarioRoot='/app',
        InputRoot='/run/probe-inputs', Stage=runtime['stage'])
    fields.update({'Limits__' + key: value for key, value in LIMITS['capture'].items()})
    fields.update({'ScenarioLoadOptions__' + key: value for key, value in LIMITS['parsing'].items()})
    if judge:
        fields.update(BlobServiceUri=runtime['blob']['account_url'], VerifierContainer='verifier',
                      LoadGenImage=runtime['loadgen_image'], VerifierImage=runtime['verifier_image'])
        fields.update({'JudgeLimits__' + key: value for key, value in LIMITS['judge'].items()})
    else:
        fields['ProcessLifetimeSeconds'] = 1920 if producer else 840
    result = {prefix + key: {'value': str(value)} for key, value in fields.items()}
    result.update({
        'PARCHWELL_SOURCE_COMMIT': {'value': runtime['source_commit']},
        'PARCHWELL_JOB_NAME': {'value': job_name(values)},
        'Telemetry__ConnectionString': {'value': runtime['application_insights_connection_string']},
        'Telemetry__ServiceName': {'value': 'Parchwell.Verifier' if judge else 'Parchwell.LoadGen'},
        'Telemetry__ScenarioId': {'value': values['scenario_id']},
        'Telemetry__RunId': {'value': runtime['run_id']},
    })
    for key in ('PARCHWELL_POD_NAME', 'Telemetry__ServiceInstanceId', prefix + 'PodName'):
        result[key] = {'valueFrom': {'fieldRef': {'fieldPath': 'metadata.name'}}}
    for key in ('PARCHWELL_POD_UID', prefix + 'PodUid'):
        result[key] = {'valueFrom': {'fieldRef': {'fieldPath': 'metadata.uid'}}}
    return result


def verify(document, values):
    objects = list(yaml.safe_load_all(document))
    assert len(objects) == 5 and sorted(o['kind'] for o in objects) == ['Job'] + ['ServiceAccount'] * 4
    runtime = values['runtime']; judge = runtime['stage'] == 'judge'; role = runtime['action_role']
    accounts = [o for o in objects if o['kind'] == 'ServiceAccount']
    assert [o['metadata']['name'] for o in accounts] == list(ACCOUNTS)
    for account in accounts:
        name = account['metadata']['name']
        assert account['metadata']['namespace'] == 'parchwell'
        assert account['metadata']['annotations'] == {'azure.workload.identity/client-id': runtime['service_accounts'][name]['client_id']}
        assert account['automountServiceAccountToken'] is False
    job = next(o for o in objects if o['kind'] == 'Job')
    assert job['metadata']['name'] == job_name(values)
    assert job['metadata']['namespace'] == 'parchwell'
    assert job['metadata']['labels'] == {'app.kubernetes.io/name': 'parchwell',
        'app.kubernetes.io/instance': runtime['run_id'], 'parchwell.io/role': role}
    spec = job['spec']; pod = spec['template']['spec']; container = pod['containers'][0]
    assert spec['completions'] == spec['parallelism'] == 1 and spec['backoffLimit'] == 0
    assert spec.get('podReplacementPolicy') == (None if judge else 'Failed')
    env = expected_env(values)
    assert spec['activeDeadlineSeconds'] == (120 if judge else int(env['ProbeV2__ProcessLifetimeSeconds']['value']) + 60)
    assert pod['restartPolicy'] == 'Never' and pod['terminationGracePeriodSeconds'] == 30
    assert pod['serviceAccountName'] == role and pod['automountServiceAccountToken'] is False
    assert pod['nodeSelector'] == {'workload': 'loadgen' if role == 'loadgen' else 'workload'}
    assert pod.get('tolerations', []) == ([{'key': 'workload', 'operator': 'Equal', 'value': 'loadgen', 'effect': 'NoSchedule'}] if role == 'loadgen' else [])
    assert pod['securityContext'] == {'runAsUser': 1654, 'runAsGroup': 1654, 'fsGroup': 1654,
        'runAsNonRoot': True, 'seccompProfile': {'type': 'RuntimeDefault'}}
    assert len(pod['containers']) == 1 and not pod.get('initContainers')
    assert container['name'] == role
    assert container['image'] == runtime['verifier_image' if judge else 'loadgen_image']
    assert container['args'] == ['--image-smoke' if runtime['image_smoke'] else '--single-action-v2-judge' if judge else '--single-action-v2']
    assert 'command' not in container
    assert container['securityContext'] == {'allowPrivilegeEscalation': False, 'readOnlyRootFilesystem': True, 'capabilities': {'drop': ['ALL']}}
    assert container['resources'] == {'requests': {'cpu': '1', 'memory': '2Gi'}, 'limits': {'cpu': '3', 'memory': '12Gi'}}
    actual = {entry['name']: {key: value for key, value in entry.items() if key != 'name'} for entry in container['env']}
    assert len(actual) == len(container['env']) and actual == env, (job_name(values), actual.keys() ^ env.keys())
    volumes = [{'name': 'work', 'emptyDir': {'sizeLimit': '8Gi'}}, {'name': 'tmp', 'emptyDir': {}}]
    mounts = [{'name': 'work', 'mountPath': '/work'}, {'name': 'tmp', 'mountPath': '/tmp'}]
    for index, dependency in enumerate(dependencies(values)):
        name = f'evidence-{index}'
        sources = []
        for part in range(4):
            fields = (['registration.json', 'pod.json'] if part == 0 else []) + [f'capture.json.part{part}']
            sources.append({'configMap': {'name': dependency + '-evidence' + (f'-{part}' if part else ''), 'optional': True,
                'items': [{'key': field, 'path': field} for field in fields]}})
        volumes.append({'name': name, 'projected': {'sources': sources}})
        mounts.append({'name': name, 'mountPath': '/run/probe-inputs/' + dependency, 'readOnly': True})
    if judge:
        volumes.append({'name': 'role-scope', 'configMap': {'name': runtime['run_id'] + '-v2-role-scope', 'optional': True,
            'items': [{'key': 'role-scope.json', 'path': 'role-scope.json'}]}})
        mounts.append({'name': 'role-scope', 'mountPath': '/run/probe-inputs/scope', 'readOnly': True})
    assert pod['volumes'] == volumes and container['volumeMounts'] == mounts


def main():
    schema = json.loads((CHART / 'values.schema.json').read_text())
    assert DEFAULTS['probe_v2'] == LIMITS == schema['properties']['runtime']['properties']['bounds']['properties']['probe_v2']['const']
    cases = list(positive_cases()) + list(positive_cases(True))
    assert len(list(stages())) == 49 and len(cases) == 98
    cases.append(case('B08', 'C0', 'action', 'loadgen', namespace_roles=False))
    with tempfile.TemporaryDirectory(prefix='probe-v2-chart-') as name:
        directory = Path(name)
        for index, values in enumerate(cases):
            fixture = directory / f'{index}.yaml'; with_case(values, fixture)
            rendered = run(['helm', 'template', 'synthetic', str(CHART), '-f', str(fixture)])
            assert rendered.returncode == 0, (index, rendered.stderr)
            verify(rendered.stdout, values)
            output = directory / f'{index}.rendered.yaml'; output.write_text(rendered.stdout)
            checked = run(['kubeconform', '-strict', '-summary', '-kubernetes-version', '1.35.0', str(output)])
            assert checked.returncode == 0, checked.stdout + checked.stderr
        negatives = []
        for values in positive_cases():
            bad = deepcopy(values); bad['runtime']['action_role'] = 'probe' if values['runtime']['action_role'] != 'probe' else 'loadgen'; negatives.append(bad)
        legacy = legacy_case('F04', 'C0', 'single-action', 'loadgen', False)
        legacy['runtime']['probe_namespace_data_roles'] = True
        negatives.append(legacy)
        source = case('E01', 'C0', 'action', 'probe')
        mutations = [lambda v: v['runtime'].update(stage='unknown'), lambda v: v['runtime'].update(phase='C1'),
            lambda v: v['runtime'].update(probe_namespace_data_roles=False), lambda v: v.update(scenario_id='F04'),
            lambda v: v['runtime'].update(mode='single-action-v2-judge'), lambda v: v['runtime'].update(image_smoke='false')]
        for field in ('repeat', 'loadgen_job_name', 'enrichment_image', 'seed_receipt_configmap', 'dependencies'):
            mutations.append(lambda v, f=field: v['runtime'].update({f: 1}))
        for field in ('stage', 'probe_namespace_data_roles'):
            mutations.append(lambda v, f=field: v['runtime'].pop(f))
        for account in ACCOUNTS:
            mutations.append(lambda v, a=account: v['runtime']['service_accounts'][a].pop('subject'))
            mutations.append(lambda v, a=account: v['runtime']['service_accounts'][a].update(name='wrong-account'))
        for group, fields in LIMITS.items():
            for field in fields:
                mutations.append(lambda v, g=group, f=field: v['runtime']['bounds']['probe_v2'][g].update({f: -1}))
        for mutation in mutations:
            bad = deepcopy(source); mutation(bad); negatives.append(bad)
        for index, values in enumerate(negatives):
            fixture = directory / f'negative-{index}.yaml'; with_case(values, fixture)
            rendered = run(['helm', 'template', 'synthetic', str(CHART), '-f', str(fixture)])
            assert rendered.returncode != 0, (index, rendered.stdout)
        print(f'probe V2 chart acceptance passed: {len(cases)} positive, {len(negatives)} negative')


if __name__ == '__main__':
    main()

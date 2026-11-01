#!/usr/bin/env python3
"""Run the published load generator's configuration validation with each rendered loadgen environment."""
from pathlib import Path
import os
import subprocess
import tempfile
import yaml
from test_chart import CHART, ROOT, positive_cases, with_case
from test_single_action_chart import positive_cases as single_action_cases
from test_probe_v2_chart import positive_cases as probe_v2_cases, native_cases as probe_v2_native_cases

EXPECTED=b'{"loadgen_configuration_valid":true}\n'
POD_UID='00000000-0000-4000-8000-000000000192'

def bounded(seconds, argv, stdout_file, env=None):
    return subprocess.run(['python3','infra/scripts/lifecycle/command.py',str(seconds),'--stdout-file',str(stdout_file),'--max-bytes','4194304','--',*argv],
        cwd=ROOT,env=env,text=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE,timeout=seconds+25)

def rendered_environment(values, td, index):
    out=td/f'{index}.rendered.yaml'
    proc=bounded(120,['helm','template','synthetic',str(CHART),'--namespace','parchwell','-f',str(values)],out)
    assert proc.returncode==0, proc.stderr
    job=next(o for o in yaml.safe_load_all(out.read_text()) if o and o['kind']=='Job')
    fields={'metadata.name':job['metadata']['name']+'-x7k2p','metadata.uid':POD_UID}
    return {e['name']:e['value'] if 'value' in e else fields[e['valueFrom']['fieldRef']['fieldPath']] for e in job['spec']['template']['spec']['containers'][0]['env']}

def host_environment(rendered, td):
    env={k:os.environ[k] for k in ('PATH','HOME','DOTNET_ROOT') if k in os.environ}
    env.update(DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_NOLOGO='1',AZURE_TENANT_ID='synthetic-tenant',AZURE_CLIENT_ID='synthetic-client',AZURE_FEDERATED_TOKEN_FILE=str(td/'synthetic-token'))
    env.update(rendered)
    return env

def publish_worker(project, destination):
    proc = subprocess.run(['python3', 'infra/scripts/lifecycle/command.py', '600', '--', 'dotnet', 'publish',
        f'src/Parchwell.{project}/Parchwell.{project}.csproj', '--configuration', 'Release', '--output', str(destination),
        '-p:RestoreLockedMode=true'], cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=625)
    assert proc.returncode == 0, proc.stdout[-4000:] + proc.stderr[-4000:]

def single_action_host_acceptance(td, loadgen_publish):
    enrichment_publish = td / 'enrichment-publish'
    verifier_publish = td / 'verifier-publish'
    publish_worker('Enrichment', enrichment_publish)
    publish_worker('Verifier', verifier_publish)
    hosts = {
        'single-action': (['dotnet', str(loadgen_publish / 'Parchwell.LoadGen.dll'), '--validate-single-action-configuration'],
            b'{"single_action_configuration_valid":true}\n', 'SingleAction__'),
        'single-action-seed': (['dotnet', str(enrichment_publish / 'Parchwell.Enrichment.dll'), '--validate-seed-configuration'],
            b'', 'SeedF04__'),
        'single-action-judge': (['dotnet', str(verifier_publish / 'Parchwell.Verifier.dll'), '--validate-single-action-configuration'],
            b'{"single_action_judge_configuration_valid":true}\n', 'Verifier__'),
    }
    environments = {}
    for index, values in enumerate(single_action_cases(False)):
        fixture = td / f'single-{index}.yaml'
        with_case(values, fixture)
        rendered = rendered_environment(fixture, td, f'single-{index}')
        mode = values['runtime']['mode']
        host, expected, _ = hosts[mode]
        output = td / f'single-{index}.host.out'
        proc = bounded(60, host, output, host_environment(rendered, td))
        assert proc.returncode == 0 and output.read_bytes() == expected, (mode, index, proc.stderr, output.read_bytes())
        environments.setdefault(mode, rendered)
    for mode, (host, _, prefix) in hosts.items():
        for kind in (('missing', 'unknown', 'legacy') if mode == 'single-action-judge' else ('missing', 'unknown')):
            rendered = dict(environments[mode])
            if kind == 'missing':
                del rendered[prefix + 'PodUid']
            elif kind == 'legacy':
                rendered['Verifier__Repeat'] = '1'
            else:
                rendered[prefix + 'UnsupportedSetting'] = 'forbidden'
            output = td / f'{mode}-{kind}.host.out'
            proc = bounded(60, host, output, host_environment(rendered, td))
            assert proc.returncode == (2 if mode == 'single-action-judge' else 1), (mode, kind, proc.returncode, proc.stderr)
    output = td / 'seed-image-smoke.out'
    proc = bounded(60, ['dotnet', str(enrichment_publish / 'Parchwell.Enrichment.dll'), '--image-smoke'],
                   output, host_environment({}, td))
    assert proc.returncode == 0 and output.read_bytes() == b'{"mode":"single-action-seed-smoke","native_loaded":true,"uid_expected":1654}\n'
    print('single-action host checks passed: 33 rendered environments, 7 missing/unknown/legacy regressions, Enrichment native image smoke')



def probe_v2_host_acceptance(td, loadgen_publish):
    verifier_publish = td / 'verifier-publish'
    hosts = {
        'single-action-v2': (['dotnet', str(loadgen_publish / 'Parchwell.LoadGen.dll'), '--validate-single-action-v2-configuration'],
            b'{"single_action_v2_configuration_valid":true}\n', 'ProbeV2__', 1),
        'single-action-v2-judge': (['dotnet', str(verifier_publish / 'Parchwell.Verifier.dll'), '--validate-single-action-v2-judge-configuration'],
            b'{"single_action_v2_judge_configuration_valid":true}\n', 'ProbeV2Judge__', 2),
    }
    def environment(rendered):
        env = host_environment(rendered, td)
        key = 'ProbeV2Judge__WorkloadIdentityClientId' if 'ProbeV2Judge__WorkloadIdentityClientId' in rendered else 'ProbeV2__WorkloadIdentityClientId'
        env.update(AZURE_TENANT_ID='00000000-0000-0000-0000-000000000010', AZURE_CLIENT_ID=rendered[key])
        return env
    environments = {}
    cases = list(probe_v2_cases())
    assert len(cases) == 49
    batch_cases = list(probe_v2_cases())
    for values in batch_cases:
        values['runtime']['run_kind'] = 'batch'
    cases.extend(batch_cases)
    assert len(cases) == 98
    for index, values in enumerate(cases):
        fixture = td / f'v2-{index}.yaml'; with_case(values, fixture)
        rendered = rendered_environment(fixture, td, f'v2-{index}')
        mode = values['runtime']['mode']; host, expected, _, _ = hosts[mode]
        output = td / f'v2-{index}.host.out'
        proc = bounded(60, host, output, environment(rendered))
        assert proc.returncode == 0 and output.read_bytes() == expected, (mode, index, proc.stderr, output.read_bytes())
        environments.setdefault(mode, rendered)
    native_host, native_expected, _, _ = hosts['single-action-v2']
    native_rendered = 0
    for index, values in enumerate(probe_v2_native_cases()):
        fixture = td / f'v2-native-{index}.yaml'; with_case(values, fixture)
        rendered = rendered_environment(fixture, td, f'v2-native-{index}')
        output = td / f'v2-native-{index}.host.out'
        proc = bounded(60, native_host, output, environment(rendered))
        assert proc.returncode == 0 and output.read_bytes() == native_expected, (values['runtime']['stage'], proc.stderr, output.read_bytes())
        native_rendered += 1
    assert native_rendered == 3
    for mode, (host, _, prefix, failure) in hosts.items():
        for kind in ('missing', 'unknown', 'case_collision', 'legacy', 'wrong_identity'):
            rendered = dict(environments[mode])
            if kind == 'missing':
                del rendered[prefix + 'PodUid']
            elif kind == 'unknown':
                rendered[prefix + 'UnsupportedSetting'] = 'forbidden'
            elif kind == 'case_collision':
                rendered[prefix.lower() + 'poduid'] = POD_UID
            elif kind == 'legacy':
                rendered['SingleAction__RunId'] = 'forbidden'
            env = environment(rendered)
            if kind == 'wrong_identity':
                env['AZURE_CLIENT_ID'] = '00000000-0000-0000-0000-000000000099'
            output = td / f'{mode}-{kind}.host.out'
            proc = bounded(60, host, output, env)
            assert proc.returncode == failure, (mode, kind, proc.returncode, proc.stderr)
    judge_host, _, _, failure = hosts['single-action-v2-judge']
    for mode in ('missing', 'empty', 'unknown', 'case'):
        rendered = dict(environments['single-action-v2-judge'])
        if mode == 'missing':
            del rendered['ProbeV2Judge__RunKind']
        else:
            rendered['ProbeV2Judge__RunKind'] = {'empty': '', 'unknown': 'other', 'case': 'Batch'}[mode]
        output = td / f'v2-run-kind-{mode}.host.out'
        proc = bounded(60, judge_host, output, environment(rendered))
        assert proc.returncode == failure, (mode, proc.returncode, proc.stderr)
    print('probe V2 host checks passed: 101 rendered smoke/batch/native environments and 14 rejected input regressions')


def main():
    with tempfile.TemporaryDirectory(prefix='parchwell-loadgen-host-') as name:
        td=Path(name); publish=td/'publish'
        proc=subprocess.run(['python3','infra/scripts/lifecycle/command.py','600','--','dotnet','publish','src/Parchwell.LoadGen/Parchwell.LoadGen.csproj','--configuration','Release','--output',str(publish),'-p:RestoreLockedMode=true'],
            cwd=ROOT,text=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE,timeout=625)
        assert proc.returncode==0, proc.stdout[-4000:]+proc.stderr[-4000:]
        host=['dotnet',str(publish/'Parchwell.LoadGen.dll'),'--validate-configuration']
        cases=[d for d in positive_cases() if d['runtime']['mode']=='loadgen']; assert len(cases)==7
        environments=[]
        for i,d in enumerate(cases):
            values=td/f'{i}.yaml'; with_case(d,values)
            rendered=rendered_environment(values,td,i); environments.append(rendered)
            out=td/f'{i}.host.out'
            proc=bounded(60,host,out,host_environment(rendered,td))
            assert proc.returncode==0 and out.read_bytes()==EXPECTED, (i,proc.stderr)
        broken=dict(environments[0]); del broken['LoadGen__PodUid']
        proc=bounded(60,host,td/'missing.host.out',host_environment(broken,td))
        assert proc.returncode==1 and 'loadgen_configuration_invalid:InvalidOperationException:required_loadgen_setting_missing:PodUid' in proc.stderr, proc.stderr
        print('loadgen host check passed: 7 rendered environments, 1 missing-setting regression')
        single_action_host_acceptance(td, publish)
        probe_v2_host_acceptance(td, publish)

if __name__=='__main__': main()

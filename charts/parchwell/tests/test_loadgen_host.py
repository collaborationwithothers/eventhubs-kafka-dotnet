#!/usr/bin/env python3
"""Run the published load generator's configuration validation with each rendered loadgen environment."""
from pathlib import Path
import os
import subprocess
import tempfile
import yaml
from test_chart import CHART, ROOT, positive_cases, with_case

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

def main():
    with tempfile.TemporaryDirectory(prefix='parchwell-loadgen-host-') as name:
        td=Path(name); publish=td/'publish'
        proc=subprocess.run(['python3','infra/scripts/lifecycle/command.py','600','--','dotnet','publish','src/Parchwell.LoadGen/Parchwell.LoadGen.csproj','--configuration','Release','--output',str(publish),'-p:RestoreLockedMode=true'],
            cwd=ROOT,text=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE,timeout=625)
        assert proc.returncode==0, proc.stdout[-4000:]+proc.stderr[-4000:]
        host=['dotnet',str(publish/'Parchwell.LoadGen.dll'),'--validate-configuration']
        cases=[d for d in positive_cases() if d['runtime']['mode']=='loadgen']; assert len(cases)==6
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
        print('loadgen host check passed: 6 rendered environments, 1 missing-setting regression')

if __name__=='__main__': main()

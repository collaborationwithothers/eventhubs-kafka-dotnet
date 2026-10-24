#!/usr/bin/env python3
"""Finite chart contract acceptance matrix."""
from copy import deepcopy
from pathlib import Path
import json
import subprocess
import tempfile
import yaml

ROOT=Path(__file__).resolve().parents[3]
CHART=ROOT/'charts/parchwell'
FIXTURE=yaml.safe_load((CHART/'tests/fixture.yaml').read_text())
LIMITS=json.loads((ROOT/'docs/operations/a01-runtime-limits.json').read_text())
DEFAULTS=yaml.safe_load((CHART/'values.yaml').read_text())['runtime']['bounds']
BOUNDS={k:LIMITS[k] for k in ('scenario_parsing','loadgen','verifier_shared','verifier_kafka','verifier_start_line','verifier_verify','verifier_gate')}
BOUNDS['execution']={k:LIMITS['execution'][k] for k in ('loadgen_job_seconds','verify_job_seconds','start_line_job_seconds','gate_job_seconds','termination_grace_seconds')}

def run(args, timeout=125):
    return subprocess.run(args,cwd=ROOT,text=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE,timeout=timeout)
def with_case(d, path): path.write_text(yaml.safe_dump(d,sort_keys=False))
def jobname(d):
    r=d['runtime']; phase=r['phase'].lower()
    return f"{r['run_id']}-{phase}-judge" if r['mode']=='judge' else f"{r['run_id']}-{phase}-r{r['repeat']}-{r['mode']}"
def positive_cases():
    for phase,config in [('probe','C0'),('C0','C0'),('C3','C3')]:
      for mode in ('start-line','loadgen','verify'):
       for repeat in (1,5):
        d=deepcopy(FIXTURE); d['configuration_id']=config;r=d['runtime'];r.update(phase=phase,mode=mode,repeat=repeat,loadgen_job_name=f"{r['run_id']}-{phase.lower()}-r{repeat}-loadgen");yield d
      d=deepcopy(FIXTURE);d['configuration_id']=config;r=d['runtime'];r.update(phase=phase,mode='judge');r.pop('repeat',None);r.pop('loadgen_job_name',None);yield d

def scalar(value):
    if isinstance(value, bool): return str(value).lower()
    return str(value)
def expected_environment(d):
    r=d['runtime']; mode=r['mode']; repeat=r.get('repeat'); phase=r['phase']; job=jobname(d)
    prefix=f"{r['run_id']}/{phase}/r{repeat}/" if repeat else None
    expected={
      'PARCHWELL_SOURCE_COMMIT':('value',r['source_commit']),'PARCHWELL_JOB_NAME':('value',job),
      'PARCHWELL_POD_NAME':('field','metadata.name'),'PARCHWELL_POD_UID':('field','metadata.uid'),
      'Telemetry__ConnectionString':('value',r['application_insights_connection_string']),
      'Telemetry__ServiceName':('value','Parchwell.LoadGen' if mode=='loadgen' else 'Parchwell.Verifier'),
      'Telemetry__ServiceInstanceId':('field','metadata.name'),'Telemetry__ScenarioId':('value','A01'),
      'Telemetry__RunId':('value',r['run_id'])}
    if mode=='loadgen':
      for section in ('loadgen','scenario_parsing'):
        expected.update({f'LoadGen__{k}':('value',scalar(v)) for k,v in r['bounds'][section].items()})
      explicit={'ScenarioId':'A01','ConfigurationId':d['configuration_id'],'RunId':r['run_id'],'Phase':phase,'Repeat':repeat,'JobName':job,'NamespaceName':r['eventhubs']['namespace_name'],'BlobEndpoint':r['blob']['account_url'],'BlobContainer':'ledger','ArtifactPrefix':prefix+'producer/'}
      expected.update({f'LoadGen__{k}':('value',scalar(v)) for k,v in explicit.items()})
      expected.update({f'LoadGen__{k}':('field','metadata.name') for k in ('ExpectedInstanceId','ActualInstanceId','PodName','ClientId')})
    else:
      expected.update({f'Verifier__{k}':('value',scalar(v)) for k,v in r['bounds']['verifier_shared'].items()})
      expected.update({f'Verifier__ScenarioLoadOptions__{k}':('value',scalar(v)) for k,v in r['bounds']['scenario_parsing'].items()})
      explicit={'Mode':mode,'ScenarioId':'A01','ConfigurationId':d['configuration_id'],'RunId':r['run_id'],'Phase':phase,'JobName':job,'VerifierCommit':r['source_commit'],'BlobServiceUri':r['blob']['account_url'],'VerifierContainer':'verifier','LedgerContainer':'ledger'}
      expected.update({f'Verifier__{k}':('value',scalar(v)) for k,v in explicit.items()})
      expected['Verifier__VerifierClientId']=('field','metadata.name')
      if mode!='judge':
        expected.update({f'Verifier__{k}':('value',scalar(v)) for k,v in r['bounds']['verifier_kafka'].items()})
        x={'NamespaceName':r['eventhubs']['namespace_name'],'Repeat':repeat,'ExpectedInstanceId':r['loadgen_job_name'],'RepeatPrefix':prefix,'ArtifactPrefix':prefix+'producer/','StartLineOutputName':prefix+'start-line.json','BrokerInventoryOutputName':prefix+'broker-inventory.json','StartLineInputName':prefix+'start-line.json','ResultOutputName':prefix+'result.json'}
        expected.update({f'Verifier__{k}':('value',scalar(v)) for k,v in x.items()})
        section='verifier_start_line' if mode=='start-line' else 'verifier_verify'
        expected.update({f'Verifier__{k}':('value',scalar(v)) for k,v in r['bounds'][section].items()})
      else:
        expected.update({f'Verifier__{k}':('value',scalar(v)) for k,v in r['bounds']['verifier_gate'].items()})
        p=f"{r['run_id']}/{phase}/"
        expected.update({'Verifier__PhasePrefix':('value',p),'Verifier__CandidateOutputName':('value',p+'candidate.json'),'Verifier__LoadGenImage':('value',r['loadgen_image']),'Verifier__VerifierImage':('value',r['verifier_image'])})
    return expected

def verify(doc,d):
    objs=list(yaml.safe_load_all(doc)); assert len(objs)==3
    assert sorted(o['kind'] for o in objs)==['Job','ServiceAccount','ServiceAccount']
    assert {o['metadata']['name'] for o in objs if o['kind']=='ServiceAccount'}=={'loadgen','verifier'}
    job=next(o for o in objs if o['kind']=='Job'); assert job['metadata']['name']==jobname(d)
    assert job['metadata']['namespace']=='parchwell'
    pod=job['spec']['template']['spec']; assert len(pod['containers'])==1 and pod['restartPolicy']=='Never'
    assert job['spec']['completions']==job['spec']['parallelism']==1 and job['spec']['backoffLimit']==0
    mode=d['runtime']['mode']; expected_deadline={'loadgen':840,'verify':750,'start-line':150,'judge':90}[mode]
    assert job['spec']['activeDeadlineSeconds']==expected_deadline
    assert pod['terminationGracePeriodSeconds']==30
    assert pod['automountServiceAccountToken'] is False
    assert pod['nodeSelector']=={'workload':'loadgen' if mode=='loadgen' else 'workload'}
    assert pod['serviceAccountName']==('loadgen' if mode=='loadgen' else 'verifier')
    assert pod.get('tolerations',[])==([{'key':'workload','operator':'Equal','value':'loadgen','effect':'NoSchedule'}] if mode=='loadgen' else [])
    assert job['spec'].get('podReplacementPolicy')==('Failed' if mode=='loadgen' else None)
    c=pod['containers'][0]; role='loadgen' if d['runtime']['mode']=='loadgen' else 'verifier'
    assert c['name']==role and c['image']==d['runtime'][role+'_image']
    assert not pod.get('initContainers') and all(v['name'] in ('work','tmp') for v in pod.get('volumes',[]))
    assert job['metadata'].get('annotations') is None
    assert all(m['mountPath']!='/deployment' for m in c.get('volumeMounts',[]))
    assert {(m['name'],m['mountPath']) for m in c['volumeMounts']}=={('work','/work'),('tmp','/tmp')}
    assert c['resources']=={'requests':{'cpu':'1','memory':'2Gi'},'limits':{'cpu':'3','memory':'12Gi'}}
    assert c['securityContext']=={'allowPrivilegeEscalation':False,'readOnlyRootFilesystem':True,'capabilities':{'drop':['ALL']}}
    assert pod['securityContext']=={'runAsUser':1654,'runAsGroup':1654,'fsGroup':1654,'runAsNonRoot':True,'seccompProfile':{'type':'RuntimeDefault'}}
    assert {(v['name'],v.get('emptyDir',{}).get('sizeLimit')) for v in pod['volumes']}=={('work','8Gi'),('tmp',None)}
    env=c['env']; names=[e['name'] for e in env]; assert len(names)==len(set(names))
    expected=expected_environment(d); assert set(names)==set(expected)
    for entry in env:
      kind,value=expected[entry['name']]
      if kind=='field': assert entry['valueFrom']['fieldRef']['fieldPath']==value
      else: assert entry['value']==value
    assert {'PARCHWELL_SOURCE_COMMIT','PARCHWELL_JOB_NAME','PARCHWELL_POD_NAME','PARCHWELL_POD_UID','Telemetry__ConnectionString','Telemetry__ServiceName','Telemetry__ServiceInstanceId','Telemetry__ScenarioId','Telemetry__RunId'} <= set(names)
    assert all(e.get('valueFrom',{}).get('fieldRef',{}).get('fieldPath') in ('metadata.name','metadata.uid') for e in env if 'valueFrom' in e)
    assert job['spec']['template']['metadata']['labels']['azure.workload.identity/use']=='true'
    assert job['spec']['template']['metadata']['labels']['parchwell.io/role']==role
    for account in (o for o in objs if o['kind']=='ServiceAccount'):
      assert account['metadata']['annotations']['azure.workload.identity/client-id']==d['runtime']['service_accounts'][account['metadata']['name']]['client_id']
      assert account['automountServiceAccountToken'] is False

def main():
  schema=json.loads((CHART/'values.schema.json').read_text())
  for name,val in BOUNDS.items():
    assert schema['properties']['runtime']['properties']['bounds']['properties'][name]['const']==val
    assert DEFAULTS[name]==val
  assert schema['properties']['runtime']['properties']['bounds']['properties']['execution']['const']==BOUNDS['execution']
  assert DEFAULTS['execution']==BOUNDS['execution']
  with tempfile.TemporaryDirectory(prefix='parchwell-chart-') as td:
    td=Path(td)
    positives=list(positive_cases()); assert len(positives)==21
    for i,d in enumerate(positives):
      f=td/f'{i}.yaml';out=td/f'{i}.rendered.yaml';with_case(d,f)
      lint=run(['python3','infra/scripts/lifecycle/command.py','120','--stdout-file',str(td/f'{i}.lint'),'--max-bytes','4194304','--','helm','lint',str(CHART),'-f',str(f)])
      assert lint.returncode==0,lint.stderr
      templ=run(['python3','infra/scripts/lifecycle/command.py','120','--stdout-file',str(out),'--max-bytes','4194304','--','helm','template','synthetic',str(CHART),'--namespace','parchwell','-f',str(f)])
      assert templ.returncode==0 and not Path(str(out)+'.incomplete').exists(),templ.stderr
      valid=run(['python3','infra/scripts/lifecycle/command.py','120','--','kubeconform','-strict','-summary','-kubernetes-version','1.35.0',str(out)])
      assert valid.returncode==0,valid.stdout+valid.stderr
      verify(out.read_text(),d)
    base=deepcopy(positives[0])
    negatives=[]
    def mut(fn,source=base):
      d=deepcopy(source);fn(d);negatives.append(d)
    mut(lambda d:d.pop('scenario_id'))
    mut(lambda d:d.update(profile={'old':True}))
    mut(lambda d:d.update(scenario_id='A02'))
    mut(lambda d:d.update(configuration_id='C1'))
    mut(lambda d:d['runtime'].update(loadgen_environment={}))
    mut(lambda d:d['runtime'].update(run_id='Bad_Name'))
    mut(lambda d:d['runtime'].update(run_id='a'*30))
    mut(lambda d:d['runtime'].update(phase='C1'))
    mut(lambda d:(d['runtime'].update(phase='C3'),d.update(configuration_id='C0')))
    mut(lambda d:d['runtime'].update(mode='probe-gate'))
    mut(lambda d:d['runtime'].pop('repeat'))
    mut(lambda d:d['runtime'].update(repeat='1'))
    mut(lambda d:d['runtime'].update(repeat=0))
    mut(lambda d:d['runtime'].update(repeat=6))
    judge=deepcopy(positives[6]); mut(lambda d:d['runtime'].update(repeat=1),judge)
    mut(lambda d:d['runtime'].update(source_commit='A'*40))
    mut(lambda d:d['runtime'].update(loadgen_image='registry.example.invalid/parchwell/loadgen:latest'))
    mut(lambda d:d['runtime']['service_accounts'].update({'enrichment-consumer':{}}))
    mut(lambda d:d['runtime']['service_accounts']['loadgen'].update(name='verifier'))
    mut(lambda d:d['runtime']['service_accounts']['verifier'].update(subject='system:serviceaccount:other:verifier'))
    mut(lambda d:d['runtime']['service_accounts']['loadgen'].update(client_id='  '))
    mut(lambda d:d['runtime'].pop('eventhubs'))
    mut(lambda d:d['runtime']['eventhubs']['topics'].update(events='other-events'))
    mut(lambda d:d['runtime']['eventhubs'].update(bootstrap_servers='wrong.example.invalid:9093'))
    mut(lambda d:d['runtime']['blob'].update(account_url='http://storage.example.invalid'))
    mut(lambda d:d['runtime']['blob'].update(account_url='https://user@storage.example.invalid/path?q=1#fragment'))
    mut(lambda d:d['runtime']['blob']['containers'].update(ledger='other-ledger'))
    mut(lambda d:d['runtime'].update(eventhubs_ip='999.0.0.1'))
    mut(lambda d:d['runtime'].update(application_insights_connection_string='  '))
    mut(lambda d:(d['runtime'].update(bounds=deepcopy(BOUNDS)),d['runtime']['bounds']['loadgen'].update(MaxAttempts=186001)))
    mut(lambda d:(d['runtime'].update(bounds=deepcopy(BOUNDS)),d['runtime']['bounds']['verifier_verify'].update(MaxInputEntries=None)))
    mut(lambda d:d['runtime'].update(loadgen_job_name='synthetic-c3-c3-r1-loadgen'))
    assert len(negatives)==32
    for i,d in enumerate(negatives):
      f=td/f'n{i}.yaml';out=td/f'n{i}.out';with_case(d,f)
      proc=run(['python3','infra/scripts/lifecycle/command.py','120','--stdout-file',str(out),'--max-bytes','4194304','--','helm','template','synthetic',str(CHART),'-f',str(f)])
      marker=Path(str(out)+'.incomplete')
      assert proc.returncode!=0 and marker.exists() and marker.read_text().strip()=='child_exit_1',f'negative {i} did not fail cleanly: {proc.stderr}'
      if out.exists(): assert not any(o and o.get('kind')=='Job' for o in yaml.safe_load_all(out.read_text()))
  print('chart acceptance passed: 21 positive, 32 negative')
if __name__=='__main__': main()

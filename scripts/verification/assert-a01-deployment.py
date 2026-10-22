#!/usr/bin/env python3
"""Assert actual Helm render output against generated worker inputs; fixtures are synthetic."""
import base64
import hashlib
import json
import pathlib
import sys
import yaml

output = pathlib.Path(sys.argv[1])
prior = output / 'assertions.json'
assertions = json.loads(prior.read_text()).get('assertions', {}) if prior.exists() else {}

def check(name, condition):
    assertions[name] = bool(condition)
    if not condition:
        (output / 'assertions.json').write_text(json.dumps({'schema_version': 1, 'synthetic': True, 'azure_calls': 0, 'assertions': assertions}, indent=2) + '\n')
        raise AssertionError(name)

for configuration in ('C0', 'C3'):
    for mode in ('loadgen', 'start-line', 'verify'):
        directory = output / configuration / mode
        prefix = configuration + '/' + mode + '/'
        generated = yaml.safe_load((directory / 'generated-values.yaml').read_text())
        runtime_envelope = yaml.safe_load((directory / 'runtime-values.yaml').read_text())
        runtime = runtime_envelope['runtime']
        check(prefix + 'runtime_only', set(runtime_envelope) == {'runtime'})
        check(prefix + 'source_hashes', hashlib.sha256((directory/'scenario.yaml').read_bytes()).hexdigest() == runtime['definition_sha256'] and hashlib.sha256((directory/'saturation-ramp.yaml').read_bytes()).hexdigest() == runtime['profile_sha256'])
        check(prefix + 'v1_a01', generated['contract_version'] == 1 and generated['scenario_id'] == 'A01' and generated['configuration_id'] == configuration)
        profile = generated['profile']
        check(prefix + 'fixed_profile', profile['seed'] == 1401 and [(s['offered_attempts_per_second'], s['duration_seconds']) for s in profile['stages']] == [(100,60),(300,60),(900,60),(1800,60)])
        kafka = generated['kafka']['LoadGenProducer']
        check(prefix + 'timeouts', [kafka[key] for key in ('request.timeout.ms','socket.timeout.ms','message.timeout.ms')] == ['60000', '60000' if configuration == 'C0' else '20000', '300000'])
        objects = list(yaml.safe_load_all((directory / 'rendered.yaml').read_text()))
        check(prefix + 'all_namespaced', all(obj['metadata'].get('namespace') == 'parchwell' for obj in objects))
        jobs = [obj for obj in objects if obj['kind'] == 'Job']
        check(prefix + 'one_job', len(jobs) == 1)
        job = jobs[0]; spec = job['spec']; pod = spec['template']['spec']; labels = spec['template']['metadata']['labels']
        role = 'loadgen' if mode == 'loadgen' else 'verifier'
        check(prefix + 'never_replace', spec['parallelism'] == 1 and spec['completions'] == 1 and spec['backoffLimit'] == 0 and pod['restartPolicy'] == 'Never' and 'ttlSecondsAfterFinished' not in spec)
        check(prefix + 'deadline', spec['activeDeadlineSeconds'] == {'loadgen':840,'start-line':150,'verify':750}[mode] and pod['terminationGracePeriodSeconds'] == 30)
        check(prefix + 'identity', labels['azure.workload.identity/use'] == 'true' and pod['serviceAccountName'] == role and pod['automountServiceAccountToken'] is False)
        accounts = [obj for obj in objects if obj['kind'] == 'ServiceAccount']
        check(prefix + 'scoped_accounts', {a['metadata']['name'] for a in accounts} == {'loadgen','verifier'} and all(a['metadata']['annotations']['azure.workload.identity/client-id'] == runtime['service_accounts'][a['metadata']['name']]['client_id'] for a in accounts))
        security = pod['securityContext']
        check(prefix + 'pod_security', all(security[key] == 1654 for key in ('runAsUser','runAsGroup','fsGroup')) and security['runAsNonRoot'] and security['seccompProfile'] == {'type':'RuntimeDefault'})
        check(prefix + 'placement', pod['nodeSelector'] == {'workload': 'loadgen' if role == 'loadgen' else 'workload'} and pod.get('tolerations',[]) == ([{'key':'workload','operator':'Equal','value':'loadgen','effect':'NoSchedule'}] if role == 'loadgen' else []))
        check(prefix + 'no_host_access', not pod.get('hostNetwork',False) and not pod.get('hostPID',False) and not pod.get('hostIPC',False) and all('hostPath' not in volume for volume in pod['volumes']))
        check(prefix + 'single_container', len(pod['containers']) == 1 and not pod.get('initContainers'))
        container = pod['containers'][0]
        check(prefix + 'immutable_image', container['image'] == runtime[role+'_image'] and '@sha256:' in container['image'])
        check(prefix + 'container_security', container['securityContext'] == {'allowPrivilegeEscalation':False,'readOnlyRootFilesystem':True,'capabilities':{'drop':['ALL']}})
        check(prefix + 'resources', container['resources'] == {'requests':{'cpu':'1','memory':'2Gi'},'limits':{'cpu':'3','memory':'12Gi'}})
        volumes = {v['name']:v for v in pod['volumes']}
        check(prefix + 'writable_bounds', volumes['work']['emptyDir'] == {'sizeLimit':'8Gi'} and volumes['tmp']['emptyDir'] == {})
        mounts = {m['name']:m for m in container['volumeMounts']}
        check(prefix + 'regular_packaged_scenarios', not any(m['mountPath'].startswith('/app') for m in mounts.values()) and mounts['comparison'] == {'name':'comparison','mountPath':'/deployment','readOnly':True})
        env = {entry['name']:entry.get('value') for entry in container['env'] if 'value' in entry}
        check(prefix + 'all_worker_options', env == runtime[role+'_environment'])
        downward = {entry['name']:entry['valueFrom']['fieldRef']['fieldPath'] for entry in container['env'] if 'valueFrom' in entry}
        check(prefix + 'independent_downward_identity', downward == {'PARCHWELL_POD_NAME':'metadata.name','PARCHWELL_POD_UID':'metadata.uid'})
        configmaps = {obj['metadata']['name']:obj for obj in objects if obj['kind'] == 'ConfigMap'}
        for comparison_role in ('loadgen','verifier'):
            comparison = configmaps[runtime['run_id']+'-'+comparison_role+'-values']
            actual = base64.b64decode(comparison['binaryData']['generated-values.yaml'], validate=True)
            expected = (directory / (comparison_role+'-values.yaml')).read_bytes()
            check(prefix + comparison_role + '_comparison_bytes', actual == expected == runtime[comparison_role+'_generated_values'].encode() and hashlib.sha256(actual).hexdigest() == runtime[comparison_role+'_generated_sha256'])
        lg = yaml.safe_load((directory/'loadgen-values.yaml').read_text()); ver = yaml.safe_load((directory/'verifier-values.yaml').read_text())
        check(prefix + 'role_contexts', lg['profile'] == ver['profile'] and lg['kafka']['LoadGenProducer'] == ver['kafka']['LoadGenProducer'] and lg['kafka']['VerifierConsumer']['client.id'] != ver['kafka']['VerifierConsumer']['client.id'])
        if role == 'loadgen':
            check(prefix + 'admission_initially_closed', configmaps[runtime['admission_name']]['data'] == {} and mounts['admission']['mountPath'] == '/admission')
        policies = [obj for obj in objects if obj['kind'] == 'NetworkPolicy']
        check(prefix + 'scoped_deny', len(policies) == 2 and all(p['spec']['podSelector'] == {'matchLabels':{'app.kubernetes.io/name':'parchwell'}} for p in policies))
        deny = next(p for p in policies if p['metadata']['name'].endswith('-default-deny'))['spec']
        check(prefix + 'deny_both', deny['policyTypes'] == ['Ingress','Egress'] and deny['ingress'] == [] and deny['egress'] == [])
        egress = next(p for p in policies if p['metadata']['name'].endswith('-egress'))['spec']['egress']
        dns = egress[0]
        check(prefix + 'dns_combined_selectors', dns == {'to':[{'namespaceSelector':{'matchLabels':{'kubernetes.io/metadata.name':'kube-system'}},'podSelector':{'matchLabels':{'k8s-app':'kube-dns'}}}],'ports':[{'protocol':'UDP','port':53},{'protocol':'TCP','port':53}]})
        expected_peers = [(runtime['eventhubs_ip']+'/32',9093),(runtime['blob_ip']+'/32',443)]
        all_prefixes = sorted({cidr for tag in ('AzureActiveDirectory','AzureMonitor') for cidr in runtime['service_tags']['tags'][tag]['address_prefixes']})
        expected_peers += [(cidr,443) for cidr in all_prefixes]
        actual_peers = []
        for entry in egress[1:]:
            check(prefix+'single_exact_ip_peer_'+str(len(actual_peers)), len(entry['to']) == 1 and set(entry['to'][0]) == {'ipBlock'} and len(entry['ports']) == 1 and entry['ports'][0]['protocol'] == 'TCP')
            actual_peers.append((entry['to'][0]['ipBlock']['cidr'],entry['ports'][0]['port']))
        check(prefix + 'exact_egress_no_wildcards', actual_peers == expected_peers and all(cidr not in ('0.0.0.0/0','::/0') for cidr,_ in actual_peers))

(output/'assertions.json').write_text(json.dumps({'schema_version':1,'synthetic':True,'azure_calls':0,'assertions':assertions,'result':'passed'},indent=2)+'\n')

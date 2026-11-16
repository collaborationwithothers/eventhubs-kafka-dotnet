"""Offline archive command integration against the real transfer protocol."""
import base64
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

FILE = Path(__file__).resolve().parents[1] / 'archive.py'
spec = importlib.util.spec_from_file_location('archive_inventory_integration', FILE)
a = importlib.util.module_from_spec(spec)
spec.loader.exec_module(a)


def encoded(value):
    return json.dumps(value, separators=(',', ':'), sort_keys=True).encode()


def sha(data):
    return hashlib.sha256(data).hexdigest()


BODY = b'BODY_SENTINEL_494'


def candidate_fixture(run, config):
    """Shape of ProbeV2Candidate and its nested writer DTOs."""
    repeats = []
    for n, body in enumerate((BODY, None, b'', BODY, None), 1):
        record = {'topic':'document-events', 'partition':0, 'offset':n, 'key_base64':'aw==',
                  'value_base64':None if body is None else base64.b64encode(body).decode(),
                  'observed_elapsed_ms':n, 'sequence':n}
        repeats.append({'repeat':n, 'status':'complete_valid', 'reasons':['reason retained'], 'outcome_class':None,
                        'broker_count':1, 'statistics_reference':{'sequence':n, 'native_time':n, 'native_ts':n, 'raw_sha256':'c'*64},
                        'semantic_outcome':{'delivered_partition':0, 'consumed_partition':0, 'key_base64':'aw==',
                                            'value_kind':'null' if body is None else 'empty' if not body else 'bytes', 'value_match':True},
                        'delivery':{'topic':'document-events', 'partition':0, 'offset':n, 'persistence_status':'persisted',
                                    'error':{'code':1, 'is_fatal':False, 'origin':'client', 'reason':'native error retained',
                                             'observed_elapsed_ms':n, 'sequence':n}, 'observed_elapsed_ms':n, 'sequence':n},
                        'read_observation':{'records_seen':1, 'eof_count':0, 'record':record},
                        'seed_reference':{'run_id':run, 'scenario_id':'F06', 'configuration_id':config, 'stage':'seed',
                                          'source_commit':'a'*40, 'job_name':run+'-seed', 'pod_uid':'uid', 'raw_sha256':'d'*64},
                        'control_references':[], 'clients':[]})
    return {'schema_version':2, 'mode':'single-action-v2', 'synthetic':True, 'run_id':run,
            'scenario_id':'F06', 'configuration_id':config, 'source_commit':'a'*40,
            'managed_version':'2.16.0', 'native_version':'2.16.0',
            'image_digests':{'loadgen':'sha256:'+'b'*64, 'verifier':'sha256:'+'b'*64},
            'status':'complete_valid', 'repeats':repeats, 'deterministic':True, 'outcome_class':None,
            'adr012_counts':'not_applicable', 'diagnostics':['diagnostic retained'], 'security_finding':False,
            'item_assessment':None}


def window_source(run, phase, scenario, repeat, kind, end=0):
    identity = {'run_id':run, 'scenario_id':scenario, 'configuration_id':phase}
    start = [{'partition':0,'offset':0}]
    finish = [{'partition':0,'offset':end}]
    if kind == 'start':
        job=f'{run}-{phase.lower()}-r{repeat}-loadgen'
        identity.update(instance_id=job, expected_instance_id=job, client_id=job)
        return {'identity':identity,'verifier_commit':'a'*40,'topic':'document-events',
                'partition_highs':[{'partition':0,'high':0}]}
    if kind == 'sweep':
        return {'run_id':run,'phase':phase,'scenario_id':scenario,'configuration_id':phase,
                'source_commit':'a'*40,'topic':'document-events','partition_highs':[{'partition':0,'high':0}]}
    if kind == 'abrupt':
        identity.update(phase=phase,repeat=repeat,source_commit='a'*40,
                        job_name=f'{run}-{phase.lower()}-r{repeat}-b10-abrupt-verify',pod_uid='uid')
        return {'kind':'nonfinal_result','identity':identity,'data':{'start_line':start,'end_line':finish}}
    return {'identity':identity,'verifier_commit':'a'*40,'start_line':start,'end_line':finish}


class BlobCLI:
    def __init__(self):
        self.blobs = {}
        self.calls = []
        self.corrupt_marker = False

    def put(self, container, name, data):
        self.blobs[('source123', container, name)] = data if isinstance(data, bytes) else encoded(data)

    def __call__(self, args, deadline):
        self.calls.append(args)
        def opt(name): return args[args.index(name) + 1]
        action = args[3]
        account, container, name = (opt('--account-name'), opt('--container-name'), opt('--name'))
        key = (account, container, name)
        if action == 'show':
            if key not in self.blobs: return 1, b'', b'BlobNotFound'
            return 0, encoded({'properties': {'contentLength': len(self.blobs[key]), 'etag': 'etag'}}), b''
        if action == 'upload':
            assert opt('--if-none-match') == '*' and opt('--overwrite') == 'false'
            if key in self.blobs: return 1, b'', b'BlobAlreadyExists'
            self.blobs[key] = Path(opt('--file')).read_bytes()
        elif action == 'download':
            assert opt('--if-match') == 'etag' and opt('--overwrite') == 'false'
            data = self.blobs[key]
            if self.corrupt_marker and account == 'archive123' and name.endswith('archive-completion.json'):
                data = b'x' * len(data)
            Path(opt('--file')).write_bytes(data)
        else:
            raise AssertionError(action)
        return 0, b'', b''


class Harness:
    def __init__(self, scenario, phase, run, kind='batch', **extra):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.private, self.artifacts = self.root / 'private', self.root / 'artifacts'
        self.private.mkdir(); self.artifacts.mkdir()
        self.cli = BlobCLI()
        rid = '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/archive123'
        config = {'storage_account_id': rid, 'account_name': 'archive123', 'blob_service_uri': 'https://archive123.blob.core.windows.net/',
                  'container_name': 'run-evidence', 'container_id': rid + '/blobServices/default/containers/run-evidence', 'retention_days': 90}
        images = {w: {'digest': 'sha256:' + 'b'*64, 'reference': 'archive123.azurecr.io/parchwell/' + w + '@sha256:' + 'b'*64} for w in ('loadgen', 'verifier')}
        self.phase = {'scenario': scenario, 'phase': phase, 'configuration': phase, 'run_id': run, 'run_kind': kind,
                      'source_commit': 'a'*40, 'evidence_archive': config, 'images': images, **extra}
        self.status = {'run_id': run, 'phase': phase, 'modes': [], 'candidate': {'accepted': False}, 'full_allowed': False}
        self.env = {'PARCHWELL_PRIVATE_DIRECTORY': str(self.private), 'PARCHWELL_ARTIFACT_DIRECTORY': str(self.artifacts),
                    'PARCHWELL_ARCHIVE_DIRECTORY': str(self.private/'archive'), 'PARCHWELL_ARCHIVE_MANIFEST_DIRECTORY': str(self.artifacts/'archive'),
                    'PARCHWELL_ARCHIVE_MANIFEST_PATH': str(self.artifacts/'archive/archive-manifest.json'), 'PARCHWELL_RUN_ID': run,
                    'GITHUB_RUN_ID': '42', 'GITHUB_RUN_ATTEMPT': '1', 'GITHUB_SHA': 'a'*40}

    def job(self, mode, job, item=None, config=None, repeat=None, stage=None):
        r = {'mode': mode, 'job': job, 'item': item, 'configuration': config, 'repeat': repeat, 'apply': 0, 'collect': 0, 'outcome': 'failed'}
        if stage: r['stage'] = stage
        self.status['modes'].append(r)
        # A killed process cannot publish a final index. Other fixture pods explicitly
        # report a built lifetime with no callbacks, as required by the amendment.
        if mode not in ('loadgen-restart', 'judge', 'single-action-judge', 'single-action-v2-judge', 'idle-observation-judge', 'probe-gate') and not (self.phase['scenario'] == 'probe-batch' and self.phase['run_kind'] == 'smoke'):
            run = self.phase['run_id']
            selected = self.phase['phase']
            scenario = item or self.phase['scenario']
            configuration = config or self.phase['configuration']
            if self.phase['scenario'] in ('probe-batch', 'probe-batch-2', 'idle-observation', 'ledger-batch'):
                selected = configuration
            if self.phase['scenario'] == 'ledger-batch' and item:
                run += '-' + item.lower()
            identity = {'run_id':run, 'phase':selected, 'scenario_id':scenario, 'configuration_id':configuration,
                        'job_name':job, 'pod_uid':'uid', 'source_commit':'a'*40}
            client = {'ordinal':1, 'role':'LoadGenProducer', 'client_id':job, 'native_identity':None,
                      'build_status':'built', 'report_count':0, 'raw_sources':[], 'diagnostics':[],
                      'capture_complete':True, 'export_tail_status':'unknown'}
            self.cli.put('native-monitoring', f'{run}/{selected}/{job}/uid/native-monitoring/index.json',
                         {'schema_version':1, 'identity':identity, 'clients':[client],
                          'capture_complete':True, 'diagnostics_truncated':False})
        d = self.artifacts/job; d.mkdir()
        v2 = mode in ('single-action-v2', 'single-action-v2-judge')
        role = ('verifier' if stage == 'judge' else 'loadgen' if stage == 'seed' else
                'probe' if config == 'C6' else 'enrichment-consumer') if v2 else 'worker'
        if v2: r['role'] = role
        (d/'pods.json').write_bytes(encoded({'items': [{'metadata': {'name': job+'-pod', 'uid': 'uid', 'annotations': {'secret': 'POD_SECRET'}},
            'spec': {'containers': [{'name': role, 'env': [{'value': 'POD_SECRET'}]}]}, 'status': {'phase': 'Failed', 'containerStatuses': []}}]}))
        (d/'collection-status.json').write_bytes(encoded({'complete': True, 'gaps': [], **({} if v2 else {'smoke_marker_valid': False})}))
        if not v2 or stage == 'judge': (d/f'{job}-pod.containers.{role}.log').write_bytes(b'')
        return job+'-pod'

    def run(self):
        (self.private/'phase.json').write_bytes(encoded(self.phase))
        (self.private/'ephemeral-output.json').write_bytes(encoded({'blob': {'value': {'account_url': 'https://source123.blob.core.windows.net/',
            'containers': {'ledger': 'ledger', 'verifier': 'verifier', 'single_action': 'single-action', 'claim_check': 'claim-check', 'native_monitoring': 'native-monitoring'}}}}))
        (self.artifacts/'phase-status.json').write_bytes(encoded(self.status))
        h = self
        class Phase:
            def state(self): return json.loads((h.artifacts/'phase-status.json').read_bytes())
            def save_state(self, value): (h.artifacts/'phase-status.json').write_bytes(encoded(value))
            def mode_job(self, phase, mode, repeat):
                return f"{phase['run_id']}-{phase['phase'].lower()}" + (f'-r{repeat}' if repeat else '') + f'-{mode}'
            def read(self, name): return json.loads((h.private/name).read_bytes())
            @staticmethod
            def b10_utc(value):
                spec = importlib.util.spec_from_file_location('archive_phase_real', FILE.with_name('phase.py'))
                module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
                return module.Phase.b10_utc(value)
            def b10_abrupt(self, repeat=None, deadline=None, lifecycle_budget=None):
                spec = importlib.util.spec_from_file_location('archive_b10_real', FILE.with_name('b10_abrupt.py'))
                module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
                return module.Trial(self) if repeat is None else module.Scientific(self, ValueError, repeat, deadline, lifecycle_budget)
        with patch.object(a._common, 'preflight'), patch.object(a._common, 'command', self.cli), patch.object(a.shutil, 'disk_usage') as disk:
            disk.return_value.free = 2*a.ArchiveMaxObjectBytes + 1048576 + 65536
            code = a.execute(Phase(), self.env)
        public = self.artifacts/'archive'
        return code, json.loads((public/'archive-manifest.json').read_bytes()), json.loads((public/'archive-receipt.json').read_bytes())

    def close(self): self.tmp.cleanup()



def publish_legacy(h, container, prefix, item, config, job, pod, idle=False):
    identity = {'scenario_id': item, 'configuration_id': config, 'run_id': h.phase['run_id'], 'instance_id': pod}
    common = {'schema_version': 1, 'mode': 'idle-observation' if idle else 'single-action', 'identity': identity,
              'job_name': job, 'pod_name': pod, 'pod_uid': 'uid', 'source_commit': 'a'*40}
    registration = encoded(common)
    completion = encoded({**common, 'repeats_attempted': 5, 'handler_exit_code': 0, 'cleanup_completed': True})
    repeats = []
    for n in range(1, 6):
        shared = {'repeat': n, 'client_id': f'{pod}-r{n}', 'settings': {}, 'omitted_keys': [],
                  'client_errors': [], 'client_errors_dropped': 0, 'statistics': [], 'diagnostics': []}
        if idle:
            repeats.append({**shared, 'client_started_unix_ms': 0, 'first': None, 'idle_started_elapsed_ms': None,
                            'idle_ended_elapsed_ms': None, 'second': None})
        else:
            repeats.append({**shared, 'action': 'assign_consume', 'terminal': True, 'elapsed_ms': 0,
                            'terminal_unix_ms': None, 'result': 'success', 'error': None,
                            'matched': {'topic':'document-events', 'partition':0, 'key_base64':'aw==',
                                        'value_base64':'Qk9EWV9TRUNSRVQ='}})
    capture = {**common, 'synthetic': True, 'managed_version':'2.16.0', 'native_version':'2.16.0', 'repeats': repeats}
    if idle:
        capture.update(run_kind=h.phase['run_kind'], topic='document-events', partition=0, payload_bytes=1000,
                       idle_seconds=420, statistics_interval_ms=15000, max_statistics_reports=80)
    for name, data in [(f'registrations/{pod}.json', registration), (f'instances/{pod}/capture.json', encoded(capture)),
                       (f'instances/{pod}/completion.json', completion)]:
        h.cli.put(container, prefix+name, data)


def publish_v2_actor(h, job, pod, config, stage, native_report=None):
    role = 'loadgen' if stage == 'seed' else 'probe' if config == 'C6' else 'enrichment-consumer'
    h.status['modes'][-1]['role'] = role
    identity = {'schema_version':2, 'mode':'single-action-v2', 'synthetic':True, 'run_id':h.phase['run_id'],
                'scenario_id':'F06', 'configuration_id':config, 'stage':stage, 'source_commit':'a'*40,
                'job_name':job, 'pod_name':pod, 'pod_uid':'uid', 'service_account':role}
    repeats = [{'repeat':n, 'admission':'not_applicable_no_delivery', 'client_id':None, 'action':'assign_consume',
                'terminal':False, 'elapsed_ms':0, 'terminal_unix_ms':None, 'settings':{}, 'omitted_keys':[],
                'metadata_partitions':None, 'requested':None, 'delivery':None,
                'read_observation':{'records_seen':0, 'eof_count':0, 'record':None}, 'result':None, 'error':None,
                'client_errors':[], 'statistics':[], 'diagnostics':[]} for n in range(1, 6)]
    repeats[0]['requested'] = {'topic':'document-events', 'partition_mode':'explicit', 'partition':0, 'offset':None,
                               'key_base64':'aw==', 'value_base64':'Qk9EWV9TRUNSRVQ='}
    if native_report is not None:
        repeats[0].update(admission='required', client_id=pod + '-r1', action='produce', terminal=True,
                          result='success', statistics=[{'sequence':3, 'observed_elapsed_ms':10,
                                                         'raw_base64':base64.b64encode(native_report).decode()}])
        index_key = ('source123', 'native-monitoring', f'probe-42-1/{config}/{job}/uid/native-monitoring/index.json')
        index = json.loads(h.cli.blobs[index_key])
        locator = f'runner/probe-v2/raw/{job}/{pod}.containers.{role}.log'
        index['clients'][0].update(client_id=pod + '-r1', report_count=1, raw_sources=[{
            'kind':'existing_capture', 'source':locator, 'byte_length':len(native_report),
            'sha256':sha(native_report), 'report_ordinal':3, 'row_count':1}])
        h.cli.blobs[index_key] = encoded(index)
    capture = {**identity, 'managed_version':'2.16.0', 'native_version':'2.16.0', 'repeats':repeats,
               'handler_exit_code':0, 'cleanup_completed':True, 'host_cleanup_completed':True}
    folder = h.artifacts/job
    raw = h.private/'probe-v2'/'raw'/job
    raw.mkdir(parents=True)
    (raw/f'{pod}.containers.{role}.log').write_bytes(b'host started\nPARCHWELL_PROBE_V2_REGISTRATION '+encoded(identity)+b'\n'+
                                                     b'probe_v2_client_log diagnostic\nPARCHWELL_PROBE_V2_CAPTURE '+encoded(capture)+b'\n'+
                                                     b'PARCHWELL_PROBE_V2_AUTHENTICATION_METADATA {"observation":true}\n')
    (folder/'collection-status.json').write_bytes(encoded({'complete':True, 'gaps':[]}))


def complete_family(h, scenario):
    """Construct fixed publications with independently specified identities and references."""
    run = h.phase['run_id']
    body = b'published candidate'
    if scenario == 'probe-batch':
        for config in ('C0', 'C1'):
            job=f'{run}-b01-{config.lower()}-single'
            pod=h.job('single-action',job,'B01',config)
            prefix=f'{run}/single-action/B01/{config}/'
            publish_legacy(h, 'single-action', prefix, 'B01', config, job, pod)
            judge=job+'-judge'; jp=h.job('single-action-judge',judge,'B01',config)
            h.cli.put('verifier',prefix+'candidate.json',body)
            h.cli.put('verifier',prefix+'judge-completion.json',{'schema_version':1,'mode':'single-action-judge',
                'run_id':run,'scenario_id':'B01','configuration_id':config,'source_commit':'a'*40,'job_name':judge,
                'pod_name':jp,'pod_uid':'uid','candidate_name':prefix+'candidate.json','handler_exit_code':0,'cleanup_completed':True})
        return ('verifier',f'{run}/single-action/B01/C0/candidate.json',4,15,0)
    if scenario == 'idle-observation':
        for config in ('C0','C1'):
            job=f'{run}-a02-{config.lower()}-idle'; pod=h.job('idle-observation',job,'A02',config)
            prefix=f'{run}/idle-observation/A02/{config}/'
            publish_legacy(h, 'ledger', prefix, 'A02', config, job, pod, idle=True)
        job=run+'-idle-judge'; pod=h.job('idle-observation-judge',job)
        root=run+'/idle-observation/'; candidate=root+'A02/judge/candidate.json'
        h.cli.put('verifier',candidate,body)
        h.cli.put('verifier',root+'judge-completion.json',{'schema_version':1,'mode':'idle-observation-judge','run_id':run,
            'source_commit':'a'*40,'job_name':job,'pod_name':pod,'pod_uid':'uid',
            'candidates':[{'scenario_id':'A02','candidate_name':candidate,'candidate_sha256':sha(body),'status':'failed','reason':'fixture'}],
            'handler_exit_code':1,'cleanup_completed':True})
        return ('verifier',candidate,3,12,0)
    for item in ('B06','B07'):
        child=run+'-'+item.lower()
        for config in ('C0','C1'):
            for repeat in range(1,6):
                root=f'{child}/{config}/r{repeat}/'
                for mode in ('start-line','loadgen','verify'):
                    job=f'{child}-{config.lower()}-r{repeat}-{mode}'; pod=h.job(mode,job,item,config,repeat)
                    if mode=='start-line':
                        for leaf in ('start-line.json','broker-inventory.json','start-line-completion.json'):
                            h.cli.put('verifier',root+leaf,window_source(child,config,item,repeat,'start') if leaf=='start-line.json' else b'published start')
                    elif mode=='loadgen':
                        proot=root+f'producer/{pod}/'
                        ident={'scenario_id':item,'configuration_id':config,'run_id':child,'instance_id':pod,'expected_instance_id':pod,'client_id':pod}
                        h.cli.put('ledger',proot+'registration.json',{'identity':ident,'phase':config,'repeat_number':repeat,
                            'job_name':job,'pod_name':pod,'pod_uid':'uid','source_commit':'a'*40})
                        h.cli.put('ledger',proot+'completion.json',b'published completion')
                        leaves=('ledger.jsonl','manifest.json','statistics.jsonl','request-lifecycle.jsonl','capabilities.json')
                        h.cli.put('ledger',proot+'publication/index.json',{'schema_version':1,'identity':ident,'worker_status':'complete',
                            'bookkeeping_complete':True,'worker_complete':True,'publication_complete':True,
                            'artifacts':[{'name':leaf,'length':3,'sha256':sha(b'abc')} for leaf in leaves]})
                        for leaf in leaves: h.cli.put('ledger',proot+'publication/'+leaf,b'abc')
                    else:
                        for leaf in ('result.json','verify-completion.json'):
                            h.cli.put('verifier',root+leaf,window_source(child,config,item,repeat,'result') if leaf=='result.json' else b'published verification')
                        cid=f'verify-{job}-uid-w0000'; cp=root+'consumed-metadata/'+cid+'/'
                        ident={'run_id':child,'phase':config,'scenario_id':item,'configuration_id':config,'job_name':job,'pod_uid':'uid','source_commit':'a'*40}
                        h.cli.put('verifier',cp+'capture.json',{'schema_version':1,'capture_id':cid,'identity':ident,
                            'window':{'index':0,'kind':'ordinary','repeat':repeat,'topic':'document-events','start':[{'partition':0,'offset':0}],'end':[{'partition':0,'offset':0}]},
                            'chunks':[],'observed_rows':0,'captured_rows':0,'complete':True,'first_missing_ordinal':None,'gaps':[]})
            judge=f'{child}-{config.lower()}-judge'; h.job('judge',judge,item,config)
            for leaf in ('candidate.json','judge-completion.json'): h.cli.put('verifier',f'{child}/{config}/'+leaf,body)
    return ('ledger',f'{run}-b06/C0/r1/producer/{run}-b06-c0-r1-loadgen-pod/publication/statistics.jsonl',64,353,20)


def abrupt_fixture(h, repeat=1):
    """Publisher-shaped first and successor descriptors, independent of archive code."""
    def descriptor(name, data): return {'name':name,'length':len(data),'sha256':sha(data)}
    run='b10-42-1'; firstjob=f'{run}-c1-r{repeat}-loadgen-restart'; first=h.job('loadgen-restart',firstjob,repeat=repeat)
    successorjob=f'{run}-c1-r{repeat}-loadgen-successor'; successor=h.job('loadgen-successor',successorjob,repeat=repeat)
    def identity(job,pod,predecessor):
        return {'run_id':run,'scenario_id':'B10','configuration_id':'C1','phase':'C1','repeat':repeat,'source_commit':'a'*40,
                'image_reference':h.phase['images']['loadgen']['reference'],'job_name':job,'pod_name':pod,'pod_uid':'uid',
                'instance_id':pod,'predecessor_instance_id':predecessor}
    def envelope(kind,ident,data):
        return encoded({'schema_version':3,'dialect':'b10_abrupt','purpose':'scientific','synthetic':True,
                        'scientific_admissible':False,'kind':kind,'identity':ident,'data':data})
    firstroot=f'{run}/C1/r{repeat}/b10-abrupt/{first}/'; nextroot=f'{run}/C1/r{repeat}/b10-abrupt/{successor}/'
    ident=identity(firstjob,first,None); nextident=identity(successorjob,successor,first)
    journal=b'{"kind":"fixture"}\n'
    manifest=envelope('abrupt_manifest',ident,{'journal':descriptor('journal.jsonl',journal),'journal_records':1,'report_files':[],'gaps':[]})
    termination=envelope('termination',ident,{'termination':'intended_self_kill','outcome':None,'detail':None,'signal_observation':None,
        'successor_permitted':True,'child':None,'exit_observed':True,'exit_code_local_observation':137,'runtime_version':'test',
        'wait_bound_milliseconds':1,'cgroup_before':None,'cgroup_after':None,'control_unconfirmed_attempt_ids':[],
        'kill_unconfirmed_attempt_ids':['attempt-1'],'capture_gaps':[],'native_error_count':0,'native_fatal_count':0,'native_tail':None,
        'manifest':descriptor('abrupt-manifest.json',manifest)})
    registration=envelope('registration',nextident,{'registered_utc':'2026-01-01T00:00:00.0000000+00:00'})
    firstreg=envelope('registration',ident,{'registered_utc':'2026-01-01T00:00:00.0000000+00:00'})
    for name,data in [('registration.json',firstreg),('journal.jsonl',journal),('abrupt-manifest.json',manifest),('termination.json',termination)]:
        h.cli.put('ledger',firstroot+name,data)
    leaves=('ledger.jsonl','manifest.json','statistics.jsonl','request-lifecycle.jsonl','capabilities.json')
    bodies={name:('body-'+name).encode() for name in leaves}
    for name,data in bodies.items(): h.cli.put('ledger',nextroot+name,data)
    h.cli.put('ledger',nextroot+'registration.json',registration)
    completion=envelope('successor_receipt',nextident,{'first_termination':descriptor('termination.json',termination),
        'journal':descriptor('journal.jsonl',journal),'kill_unconfirmed_attempt_ids':['attempt-1'],
        'registration':descriptor('registration.json',registration),'artifacts':[descriptor(name,bodies[name]) for name in leaves],
        'ready_utc':'2026-01-01T00:00:00.0000000+00:00','release_utc':'2026-01-01T00:00:01.0000000+00:00','invocations':[],
        'cleanup_completed':True,'cross_host_timing':'not_verified'})
    h.cli.put('ledger',nextroot+'completion.json',completion)
    return firstroot,nextroot,leaves


def complete_b10(h):
    """All scheduled scientific C1 jobs with complete archive publications."""
    run='b10-42-1'; target=None
    for repeat in range(1,6):
        pair=abrupt_fixture(h,repeat)
        if repeat==1: target=pair
        root=f'{run}/C1/r{repeat}/'
        start=f'{run}-c1-r{repeat}-start-line'; h.job('start-line',start,repeat=repeat)
        for leaf in ('start-line.json','broker-inventory.json','start-line-completion.json'):
            h.cli.put('verifier',root+leaf,window_source(run,'C1','B10',repeat,'start') if leaf=='start-line.json' else b'fixed output')
        job=f'{run}-c1-r{repeat}-loadgen'; pod=h.job('loadgen',job,repeat=repeat)
        proot=root+f'producer/{pod}/'
        ident={'scenario_id':'B10','configuration_id':'C1','run_id':run,'instance_id':pod,'expected_instance_id':pod,'client_id':pod}
        h.cli.put('ledger',proot+'registration.json',{'identity':ident,'phase':'C1','repeat_number':repeat,
            'job_name':job,'pod_name':pod,'pod_uid':'uid','source_commit':'a'*40})
        h.cli.put('ledger',proot+'completion.json',b'completion')
        leaves=('ledger.jsonl','manifest.json','statistics.jsonl','request-lifecycle.jsonl','capabilities.json')
        h.cli.put('ledger',proot+'publication/index.json',{'schema_version':1,'identity':ident,'worker_status':'complete',
            'bookkeeping_complete':True,'worker_complete':True,'publication_complete':True,
            'artifacts':[{'name':leaf,'length':3,'sha256':sha(b'abc')} for leaf in leaves]})
        for leaf in leaves: h.cli.put('ledger',proot+'publication/'+leaf,b'abc')
        verify=f'{run}-c1-r{repeat}-b10-abrupt-verify'; h.job('b10-abrupt-verify',verify,repeat=repeat)
        for leaf in ('b10-abrupt/nonfinal-result.json','b10-abrupt/verify-completion.json'):
            h.cli.put('verifier',root+leaf,window_source(run,'C1','B10',repeat,'abrupt') if leaf.endswith('nonfinal-result.json') else b'fixed output')
        cid=f'b10-abrupt-verify-{verify}-uid-w0000'; cp=root+'consumed-metadata/'+cid+'/'
        ident={'run_id':run,'phase':'C1','scenario_id':'B10','configuration_id':'C1','job_name':verify,'pod_uid':'uid','source_commit':'a'*40}
        h.cli.put('verifier',cp+'capture.json',{'schema_version':1,'capture_id':cid,'identity':ident,
            'window':{'index':0,'kind':'ordinary','repeat':repeat,'topic':'document-events','start':[{'partition':0,'offset':0}],'end':[{'partition':0,'offset':0}]},
            'chunks':[],'observed_rows':0,'captured_rows':0,'complete':True,'first_missing_ordinal':None,'gaps':[]})
    for mode,leaves in [('b10-sweep-line',('sweep-line.json','b10-sweep-line-completion.json')),
                        ('b10-final-sweep',('b10-final-sweep.json','b10-final-sweep-completion.json')),
                        ('judge',('candidate.json','judge-completion.json'))]:
        job=f'{run}-c1-{mode}'; h.job(mode,job)
        for leaf in leaves: h.cli.put('verifier',f'{run}/C1/'+leaf,window_source(run,'C1','B10',None,'sweep') if leaf=='sweep-line.json' else b'fixed output')
        if mode=='b10-final-sweep':
            cid=f'{mode}-{job}-uid-w0000'; cp=f'{run}/C1/consumed-metadata/{cid}/'
            ident={'run_id':run,'phase':'C1','scenario_id':'B10','configuration_id':'C1','job_name':job,'pod_uid':'uid','source_commit':'a'*40}
            h.cli.put('verifier',cp+'capture.json',{'schema_version':1,'capture_id':cid,'identity':ident,
                'window':{'index':0,'kind':'final','repeat':None,'topic':'document-events','start':[{'partition':0,'offset':0}],'end':[{'partition':0,'offset':0}]},
                'chunks':[],'observed_rows':0,'captured_rows':0,'complete':True,'first_missing_ordinal':None,'gaps':[]})
    return target


class InventoryIntegrationTests(unittest.TestCase):
    def harness(self, *args, **kwargs):
        h = Harness(*args, **kwargs); self.addCleanup(h.close); return h

    def assert_fixture_diagnostics(self, h, manifest, v2=False):
        # Fixture declarations independently name each new status and log.
        records = [r for r in h.status['modes'] if not v2 or r.get('stage') == 'judge']
        expected = {}
        for record in records:
            job = record['job']
            role = record['role'] if v2 else 'worker'
            status = {'complete': True, 'gaps': []}
            if not v2: status['smoke_marker_valid'] = False
            expected[job + '/collection-status.json'] = len(encoded(status)) + 1
            expected[f'{job}/{job}-pod.containers.{role}.log'] = 0
        observed = {o['source']['name']: o for o in manifest['objects'] if o['source']['name'] in expected}
        self.assertEqual(set(expected), set(observed))
        self.assertEqual(len(records) * 2, len(observed))
        for name, length in expected.items():
            self.assertEqual('verified', observed[name]['state'])
            self.assertEqual(length, observed[name]['bytes'])

    def test_required_collection_failures_block_complete_nonledger_receipt(self):
        cases = ('missing_status', 'false_complete', 'gaps', 'collect_gap', 'collect_error', 'missing_log',
                 'truncated_status', 'truncated_log_status', 'wrong_types', 'duplicate_member', 'unknown_member', 'unknown_gap_member',
                 'missing_init_log')
        for scenario, phase, run, items in [('probe-batch', 'single-action', 'probe-42-1', ['B01']),
                                             ('idle-observation', 'idle-observation', 'idle-42-1', ['A02'])]:
            for case in cases:
                with self.subTest(scenario=scenario, case=case):
                    h = self.harness(scenario, phase, run, 'batch', items=items)
                    container, candidate, _, _, _ = complete_family(h, scenario)
                    original_candidate = h.cli.blobs[('source123', container, candidate)]
                    record = h.status['modes'][0]
                    folder = h.artifacts / record['job']
                    status = folder / 'collection-status.json'
                    if case == 'missing_status': status.unlink()
                    elif case == 'false_complete': status.write_bytes(encoded({'complete': False, 'smoke_marker_valid': False, 'gaps': []}))
                    elif case == 'gaps': status.write_bytes(encoded({'complete': True, 'smoke_marker_valid': False, 'gaps': [{'kind':'no_pods'}]}))
                    elif case == 'collect_gap': record['collect_gap'] = True
                    elif case == 'collect_error': record.update(collect=2, outcome='collect_error')
                    elif case == 'missing_log': (folder / (record['job'] + '-pod.containers.worker.log')).unlink()
                    elif case == 'truncated_status': status.write_bytes(b'{"complete":true')
                    elif case == 'truncated_log_status': status.write_bytes(encoded({'complete': False, 'smoke_marker_valid': False, 'gaps': [{'kind':'truncated_log', 'pod':record['job'] + '-pod', 'container':'worker', 'status':125}]}))
                    elif case == 'wrong_types': status.write_bytes(encoded({'complete': 1, 'smoke_marker_valid': False, 'gaps': []}))
                    elif case == 'duplicate_member': status.write_bytes(b'{"complete":true,"complete":true,"smoke_marker_valid":false,"gaps":[]}')
                    elif case == 'unknown_member': status.write_bytes(encoded({'complete': True, 'smoke_marker_valid': False, 'gaps': [], 'secret':'DO_NOT_ARCHIVE'}))
                    elif case == 'unknown_gap_member': status.write_bytes(encoded({'complete': False, 'smoke_marker_valid': False, 'gaps': [{'kind':'no_pods', 'secret':'DO_NOT_ARCHIVE'}]}))
                    else:
                        path = folder / 'pods.json'
                        pods = json.loads(path.read_bytes())
                        pods['items'][0]['spec']['initContainers'] = [{'name':'setup'}]
                        path.write_bytes(encoded(pods))
                    code, manifest, receipt = h.run()
                    self.assertEqual(1, code)
                    self.assertFalse(receipt['archive_complete'])
                    self.assertIsNone(receipt['completion'])
                    self.assertTrue(receipt['gaps'])
                    self.assertNotIn(b'DO_NOT_ARCHIVE', b''.join(h.cli.blobs.values()))
                    obj = next(o for o in manifest['objects'] if o['source']['name'] == candidate)
                    destination = manifest['destination']['prefix'] + 'objects/' + container + '/' + candidate
                    self.assertEqual(('candidate', 'verified', container, destination, len(original_candidate), sha(original_candidate)),
                                     (obj['kind'], obj['state'], obj['source']['container'], obj['destination_name'], obj['bytes'], obj['sha256']))
                    self.assertEqual(b'published candidate', original_candidate)
                    self.assertEqual(original_candidate, h.cli.blobs[('archive123', 'run-evidence', destination)])

    def test_ordinary_and_nonledger_judges_require_successful_collection(self):
        for family in ('ordinary', 'legacy', 'idle'):
            with self.subTest(family=family):
                if family == 'ordinary':
                    h = self.harness('B10', 'C1', 'b10-42-1', 'scientific')
                    complete_b10(h)
                else:
                    scenario, phase, run, item = (('probe-batch', 'single-action', 'probe-42-1', 'B01') if family == 'legacy'
                                                  else ('idle-observation', 'idle-observation', 'idle-42-1', 'A02'))
                    h = self.harness(scenario, phase, run, 'batch', items=[item])
                    complete_family(h, scenario)
                judge = next(r for r in h.status['modes'] if r['mode'].endswith('judge'))
                judge['collect_gap'] = True
                code, manifest, receipt = h.run()
                self.assertEqual(1, code)
                self.assertFalse(receipt['archive_complete'])
                self.assertTrue(any(g['boundary'] == 'job collection failed' and g['expected_unit'] == judge['job']
                                    for g in manifest['gaps']))
                self.assertTrue(any(o['kind'] == 'collection_status' and o['state'] == 'verified'
                                    and o['source']['name'].startswith(judge['job'] + '/') for o in manifest['objects']))

    def test_collection_inventory_uses_init_declarations_and_accepts_empty_logs(self):
        h = self.harness('probe-batch', 'single-action', 'probe-42-1', 'batch', items=['B01'])
        complete_family(h, 'probe-batch')
        record = h.status['modes'][0]
        folder = h.artifacts / record['job']
        path = folder / 'pods.json'
        pods = json.loads(path.read_bytes())
        pods['items'][0]['spec']['initContainers'] = [{'name':'setup'}]
        path.write_bytes(encoded(pods))
        (folder / (record['job'] + '-pod.initContainers.setup.log')).write_bytes(b'')
        code, manifest, receipt = h.run()
        self.assertEqual(0, code)
        self.assertTrue(receipt['archive_complete'])
        self.assertTrue(any(o['source']['name'].endswith('.initContainers.setup.log') and o['bytes'] == 0
                            for o in manifest['objects']))

    def test_v2_complete_and_control_readback_and_bad_hash(self):
        for fault in ('none', 'candidate_hash', 'native_hash'):
            with self.subTest(fault=fault):
                bad = fault != 'none'
                h = self.harness('probe-batch-2', 'single-action', 'probe-42-1', 'smoke', items=['F06'],
                                 probe_v2_selection='authentication-controls', capture_authentication_metadata=True)
                for config, stages in [('C6', ('seed','action','judge')), ('C9', ('action','judge'))]:
                    for stage in stages:
                        job = f'probe-42-1-f06-{config.lower()}-{stage}'
                        mode = 'single-action-v2-judge' if stage == 'judge' else 'single-action-v2'
                        pod = h.job(mode, job, 'F06', config, stage=stage)
                        if stage != 'judge':
                            publish_v2_actor(h, job, pod, config, stage,
                                             b'{"name":"native-producer","client_id":"fixture"}' if config == 'C6' and stage == 'seed' else None)
                            continue
                        prefix = f'probe-42-1/single-action-v2/F06/{config}/'
                        candidate = encoded(candidate_fixture('probe-42-1', config))
                        h.cli.put('verifier', prefix+'candidate.json', candidate)
                        receipt = {'schema_version': 2, 'mode': mode, 'run_id': 'probe-42-1', 'scenario_id': 'F06',
                                   'configuration_id': config, 'source_commit': 'a'*40, 'job_name': job, 'pod_name': pod,
                                   'pod_uid': 'uid', 'candidate_name': prefix+'candidate.json',
                                   'candidate_sha256': ('0'*64 if fault == 'candidate_hash' and config == 'C6' else sha(candidate)),
                                   'handler_exit_code': 1, 'cleanup_completed': True}
                        h.cli.put('verifier', prefix+'judge-completion.json', receipt)
                if fault == 'native_hash':
                    key = ('source123', 'native-monitoring', 'probe-42-1/C6/probe-42-1-f06-c6-seed/uid/native-monitoring/index.json')
                    index = json.loads(h.cli.blobs[key])
                    index['clients'][0]['raw_sources'][0]['sha256'] = 'f'*64
                    h.cli.blobs[key] = encoded(index)
                code, manifest, receipt = h.run()
                self.assertEqual(1 if bad else 0, code)
                self.assertEqual(not bad, receipt['archive_complete'])
                self.assertFalse(any(o['kind']=='native_monitoring_raw' for o in manifest['objects']))
                self.assertEqual(5, len(manifest['expected_units']))
                self.assert_fixture_diagnostics(h, manifest, v2=True)
                self.assertEqual(16, manifest['totals']['expected_objects'] - 4 - sum(o['kind']=='native_monitoring_index' for o in manifest['objects'])) # Original publications, pods, status and stdout projections remain required.
                self.assertEqual(15 if fault == 'candidate_hash' else 16, manifest['totals']['verified_objects'] - 4 - sum(o['kind']=='native_monitoring_index' and o['state']=='verified' for o in manifest['objects']))
                if bad:
                    self.assertIsNone(receipt['completion'])
                    self.assertTrue(any(g['code'] == ('source_hash_mismatch' if fault == 'candidate_hash' else 'monitoring_incomplete') for g in receipt['gaps']))
                else:
                    prefix = manifest['destination']['prefix']
                    mb = h.cli.blobs[('archive123','run-evidence',prefix+'archive-manifest.json')]
                    cb = h.cli.blobs[('archive123','run-evidence',prefix+'archive-completion.json')]
                    self.assertEqual(sha(mb), receipt['manifest']['sha256'])
                    self.assertEqual(sha(cb), receipt['completion']['sha256'])
                    a.verify_controls(mb, cb, encoded(receipt))
                    name='probe-v2/raw/probe-42-1-f06-c6-seed/probe-42-1-f06-c6-seed-pod.containers.loadgen.log'
                    raw=(h.private/name).read_bytes()
                    obj=next(o for o in manifest['objects'] if o['source']['name']==name)
                    self.assertEqual('source_metadata_projection',obj['kind'])
                    projected=h.cli.blobs[('archive123','run-evidence',obj['destination_name'])]
                    self.assertEqual((len(projected),sha(projected)),(obj['bytes'],obj['sha256']))
                    projection=json.loads(projected)
                    self.assertEqual({'container':'runner','name':name,'etag':None,'bytes':len(raw),'sha256':sha(raw)},projection['source'])
                    self.assertEqual('probe_v2_stdout',projection['format'])
                    self.assertEqual([{'text':'host started\n'}, {'record':'registration'},
                                      {'text':'probe_v2_client_log diagnostic\n'}, {'record':'capture'},
                                      {'text':'PARCHWELL_PROBE_V2_AUTHENTICATION_METADATA {"observation":true}\n'}],
                                     projection['metadata']['lines'])
                    self.assertEqual({'is_null':False,'length':11,'sha256':sha(b'BODY_SECRET')},
                                     projection['metadata']['capture']['repeats'][0]['requested']['value_base64'])
                    marker = json.loads(cb); marker['identity']['run_id'] = 'wrong-42-1'
                    with self.assertRaises(a.InvalidArchiveConfig): a.verify_controls(mb, encoded(marker), encoded(receipt))
                archived = b''.join(v for (account, _, _), v in h.cli.blobs.items() if account == 'archive123')
                self.assertNotIn(b'POD_SECRET', archived)
                self.assertNotIn(b'BODY_SECRET', archived)
                self.assertNotIn(b'Qk9EWV9TRUNSRVQ=', archived)
                self.assertNotIn(b'ephemeral-output', archived)
                self.assertEqual([], list((h.private/'archive').iterdir()))

    def test_capture_window_mode_index_and_vectors_are_bound(self):
        identity = {'run_id':'a01-42-1', 'phase':'C0', 'scenario_id':'A01', 'configuration_id':'C0',
                    'job_name':'a01-42-1-c0-r1-verify', 'pod_uid':'uid', 'source_commit':'a'*40}
        for mode, index, kind, repeat in [('verify',0,'ordinary',1), ('b10-abrupt-verify',0,'ordinary',1),
                                           ('b10-final-sweep',0,'final',None), ('b10-sweep',0,'original',1),
                                           ('b10-sweep',1,'late',1)]:
            cid = f'{mode}-job-uid-w{index:04d}'
            window = {'index':index, 'kind':kind, 'repeat':repeat, 'topic':'document-events',
                      'start':[{'partition':0,'offset':4}], 'end':[{'partition':0,'offset':4}]}
            capture = {'schema_version':1, 'capture_id':cid, 'identity':identity, 'window':window, 'chunks':[],
                       'observed_rows':0, 'captured_rows':0, 'complete':True, 'first_missing_ordinal':None, 'gaps':[]}
            result, _ = a.capture_inventory(encoded(capture), 'source/', identity, mode, repeat, cid)
            self.assertTrue(result['complete']); self.assertEqual(0, result['rows'])
            for field, value in [('topic','events'), ('index',index+2), ('kind','late' if kind != 'late' else 'original'),
                                 ('start',[]), ('end',[]), ('end',[{'partition':1,'offset':4}]),
                                 ('end',[{'partition':0,'offset':3}])]:
                with self.subTest(mode=mode, index=index, field=field, value=value):
                    invalid = {**capture, 'window':{**window, field:value}}
                    with self.assertRaises(a.InvalidArchiveConfig):
                        a.capture_inventory(encoded(invalid), 'source/', identity, mode, repeat, cid)

    def test_capture_window_must_match_published_source_vectors(self):
        h=self.harness('ledger-batch','ledger-batch','ledger-42-1','batch',items=['B06','B07'])
        complete_family(h,'ledger-batch')
        name='ledger-42-1-b06/C0/r1/result.json'
        h.cli.put('verifier',name,window_source('ledger-42-1-b06','C0','B06',1,'result',end=1))
        code, manifest, receipt=h.run()
        self.assertEqual(1,code); self.assertFalse(receipt['archive_complete']); self.assertIsNone(receipt['completion'])
        capture=next(c for c in manifest['captures'] if c['capture_id'].startswith('verify-ledger-42-1-b06-c0-r1-verify-'))
        self.assertFalse(capture['complete']); self.assertEqual(0,capture['rows'])
        self.assertTrue(any(g['code']=='capture_incomplete' for g in manifest['gaps']))

    def test_v2_required_stdout_and_collection_fail_closed(self):
        for fault in ('missing_log', 'truncated_log', 'truncated_tail', 'body_line', 'credential_line', 'invalid_utf8', 'missing_collection', 'failed_collection'):
            with self.subTest(fault=fault):
                h=self.harness('probe-batch-2','single-action','probe-42-1','smoke',items=['F06'],
                               probe_v2_selection='authentication-controls',capture_authentication_metadata=True)
                for config, stages in [('C6',('seed','action','judge')),('C9',('action','judge'))]:
                    for stage in stages:
                        job=f'probe-42-1-f06-{config.lower()}-{stage}'
                        mode='single-action-v2-judge' if stage=='judge' else 'single-action-v2'
                        pod=h.job(mode,job,'F06',config,stage=stage)
                        if stage != 'judge':
                            publish_v2_actor(h,job,pod,config,stage)
                        else:
                            prefix=f'probe-42-1/single-action-v2/F06/{config}/'; body=encoded(candidate_fixture('probe-42-1', config))
                            h.cli.put('verifier',prefix+'candidate.json',body)
                            h.cli.put('verifier',prefix+'judge-completion.json',{'schema_version':2,'mode':mode,'run_id':'probe-42-1',
                                'scenario_id':'F06','configuration_id':config,'source_commit':'a'*40,'job_name':job,'pod_name':pod,
                                'pod_uid':'uid','candidate_name':prefix+'candidate.json','candidate_sha256':sha(body),
                                'handler_exit_code':1,'cleanup_completed':True})
                folder=h.artifacts/'probe-42-1-f06-c6-seed'
                log=h.private/'probe-v2'/'raw'/'probe-42-1-f06-c6-seed'/'probe-42-1-f06-c6-seed-pod.containers.loadgen.log'
                status=folder/'collection-status.json'
                if fault=='missing_log': log.unlink()
                elif fault=='truncated_log':
                    raw=log.read_bytes(); capture_end=raw.index(b'\n', raw.index(b'PARCHWELL_PROBE_V2_CAPTURE '))
                    log.write_bytes(raw[:capture_end-20])
                elif fault=='truncated_tail': log.write_bytes(log.read_bytes()[:-1])
                elif fault=='body_line': log.write_bytes(log.read_bytes()+b'unknown {"value_base64":"Qk9EWV9TRUNSRVQ="}\n')
                elif fault=='credential_line': log.write_bytes(log.read_bytes()+b'password=PRIVATE_SECRET\n')
                elif fault=='invalid_utf8': log.write_bytes(log.read_bytes()+b'\xff\n')
                elif fault=='missing_collection': status.unlink()
                else: status.write_bytes(encoded({'complete':False,'gaps':['truncated']}))
                code, manifest, receipt=h.run()
                self.assertEqual(1,code); self.assertFalse(receipt['archive_complete']); self.assertIsNone(receipt['completion'])
                self.assertTrue(any(g['code']=='monitoring_incomplete' for g in manifest['gaps']))
                archived = b''.join(v for (account, _, _), v in h.cli.blobs.items() if account == 'archive123')
                self.assertNotIn(b'PRIVATE_SECRET', archived)
                self.assertNotIn(b'Qk9EWV9TRUNSRVQ=', archived)

    def test_failed_ordinary_capture_zero_vs_missing_and_publication_references(self):
        h = self.harness('A01', 'C0', 'a01-42-1', 'scientific')
        pjob = 'a01-42-1-c0-r1-loadgen'; pod = h.job('loadgen', pjob, repeat=1)
        root = f'a01-42-1/C0/r1/producer/{pod}/'
        identity = {'scenario_id':'A01','configuration_id':'C0','run_id':'a01-42-1','instance_id':pod,'expected_instance_id':pod,'client_id':pod}
        h.cli.put('ledger', root+'registration.json', {'identity':identity,'phase':'C0','repeat_number':1,'job_name':pjob,'pod_name':pod,'pod_uid':'uid','source_commit':'a'*40})
        h.cli.put('ledger', root+'completion.json', b'failed')
        h.cli.put('claim-check', 'a01-42-1/full-payload.bin', b'FULL_BODY_SECRET')
        (h.private/'credentials.json').write_text('PRIVATE_SECRET')
        leaves = ('ledger.jsonl','manifest.json','statistics.jsonl','request-lifecycle.jsonl','capabilities.json')
        artifacts = [{'name': x, 'length': 3, 'sha256': sha(b'abc')} for x in leaves]
        h.cli.put('ledger', root+'publication/index.json', {'schema_version':1,'identity':identity,'worker_status':'failed',
            'bookkeeping_complete':False,'worker_complete':False,'publication_complete':False,'artifacts':artifacts})
        for leaf in leaves: h.cli.put('ledger',root+'publication/'+leaf,b'abc')
        h.job('start-line','a01-42-1-c0-r1-start-line',repeat=1)
        h.cli.put('verifier','a01-42-1/C0/r1/start-line.json',window_source('a01-42-1','C0','A01',1,'start'))
        for repeat in (1,2):
            job=f'a01-42-1-c0-r{repeat}-verify'; h.job('verify',job,repeat=repeat)
            prefix=f'a01-42-1/C0/r{repeat}/'; h.cli.put('verifier',prefix+'result.json',window_source('a01-42-1','C0','A01',repeat,'result'))
            if repeat == 1:
                cid=f'verify-{job}-uid-w0000'; cprefix=prefix+'consumed-metadata/'+cid+'/'
                ident={'run_id':'a01-42-1','phase':'C0','scenario_id':'A01','configuration_id':'C0','job_name':job,'pod_uid':'uid','source_commit':'a'*40}
                h.cli.put('verifier',cprefix+'capture.json',{'schema_version':1,'capture_id':cid,'identity':ident,'window':{'index':0,'kind':'ordinary','repeat':1,'topic':'document-events','start':[{'partition':0,'offset':0}],'end':[{'partition':0,'offset':0}]},'chunks':[],'observed_rows':0,'captured_rows':0,'complete':True,'first_missing_ordinal':None,'gaps':[]})
        code,m,r=h.run(); self.assertEqual(1,code); self.assertFalse(r['archive_complete'])
        self.assertEqual([(True,0),(False,None)],[(c['complete'],c['rows']) for c in m['captures']])
        objects={o['source']['name']:o for o in m['objects']}
        for leaf in leaves: self.assertEqual('verified', objects[root+'publication/'+leaf]['state'])
        self.assertEqual('missing',objects['a01-42-1/C0/r1/verify-completion.json']['state'])
        self.assertEqual('not_started',objects['a01-42-1/C0/r3/result.json']['state'])
        self.assertTrue(any(g['code']=='capture_missing' for g in m['gaps']))
        self.assertEqual({'accepted':False},json.loads((h.artifacts/'phase-status.json').read_bytes())['candidate'])
        archived=b''.join(v for (account, _, _),v in h.cli.blobs.items() if account=='archive123')
        self.assertNotIn(b'FULL_BODY_SECRET',archived); self.assertNotIn(b'PRIVATE_SECRET',archived)
        self.assertFalse(any('--container-name' in c and c[c.index('--container-name')+1]=='claim-check' for c in h.cli.calls))

    def test_b10_final_union_and_legacy_ten_windows(self):
        # The old B10 run shape is selected by the inventory adapter itself.
        # Modern canonical run identities select the scientific final-union path.
        for legacy in (False, True):
            with self.subTest(legacy=legacy):
                h=self.harness('B10','C0','b10-42-1','scientific')
                run='legacy-42-1' if legacy else 'b10-42-1'
                h.phase['run_id']=run
                mode='b10-sweep' if legacy else 'b10-final-sweep'
                job=f'{run}-c0-{mode}'; h.job(mode,job)
                for repeat_number in range(1,6):
                    start=f'{run}-c0-r{repeat_number}-start-line'; h.job('start-line',start,repeat=repeat_number)
                    root=f'{run}/C0/r{repeat_number}/'
                    h.cli.put('verifier',root+'start-line.json',window_source(run,'C0','B10',repeat_number,'start'))
                    h.cli.put('verifier',root+'result.json',window_source(run,'C0','B10',repeat_number,'result'))
                    if legacy:
                        verify=f'{run}-c0-r{repeat_number}-verify'; h.job('verify',verify,repeat=repeat_number)
                        cid=f'verify-{verify}-uid-w0000'
                        ident={'run_id':run,'phase':'C0','scenario_id':'B10','configuration_id':'C0','job_name':verify,'pod_uid':'uid','source_commit':'a'*40}
                        h.cli.put('verifier',root+f'consumed-metadata/{cid}/capture.json',{'schema_version':1,'capture_id':cid,'identity':ident,
                            'window':{'index':0,'kind':'ordinary','repeat':repeat_number,'topic':'document-events','start':[{'partition':0,'offset':0}],'end':[{'partition':0,'offset':0}]},
                            'chunks':[],'observed_rows':0,'captured_rows':0,'complete':True,'first_missing_ordinal':None,'gaps':[]})
                h.job('b10-sweep-line',f'{run}-c0-b10-sweep-line')
                h.cli.put('verifier',f'{run}/C0/sweep-line.json',window_source(run,'C0','B10',None,'sweep'))
                for index in range(10 if legacy else 1):
                    repeat=index//2+1 if legacy else None
                    cid=f'{mode}-{job}-uid-w{index:04d}'
                    prefix=f'{run}/C0/consumed-metadata/{cid}/'
                    ident={'run_id':run,'phase':'C0','scenario_id':'B10','configuration_id':'C0',
                           'job_name':job,'pod_uid':'uid','source_commit':'a'*40}
                    kind='original' if legacy and index%2 == 0 else 'late' if legacy else 'final'
                    h.cli.put('verifier',prefix+'capture.json',{'schema_version':1,'capture_id':cid,'identity':ident,
                        'window':{'index':index,'kind':kind,'repeat':repeat,'topic':'document-events','start':[{'partition':0,'offset':0}],'end':[{'partition':0,'offset':0}]},
                        'chunks':[],'observed_rows':0,'captured_rows':0,'complete':True,'first_missing_ordinal':None,'gaps':[]})
                h.cli.put('verifier',f'{run}/C0/'+('b10-finalization.json' if legacy else 'b10-final-sweep.json'),b'failed final')
                if not legacy:
                    code,m,r=h.run(); self.assertEqual(1,code)
                    captures=m['captures']; objects=m['objects']
                else:
                    spool=h.private/'archive'; spool.mkdir()
                    transfer=a.Transfer('source123','archive123',spool,float('inf'),h.cli)
                    collector=a.Collector(transfer,'v1/test/')
                    class Phase:
                        def mode_job(self, phase, mode, repeat):
                            return f"{phase['run_id']}-{phase['phase'].lower()}"+(f'-r{repeat}' if repeat else '')+f'-{mode}'
                    with patch.object(a.shutil,'disk_usage') as disk:
                        disk.return_value.free=2*a.ArchiveMaxObjectBytes+1048576+65536
                        a.collect_ordinary(collector,h.phase,h.status,{'ledger':'ledger','verifier':'verifier'},h.artifacts,Phase().mode_job)
                    self.assertEqual(5,sum(c['mode']=='verify' and c['complete'] for c in collector.captures))
                    captures=[c for c in collector.captures if c['mode']==mode]; objects=collector.objects
                self.assertEqual(10 if legacy else 1,len(captures))
                self.assertEqual([f'{mode}-{job}-uid-w{i:04d}' for i in range(10 if legacy else 1)],
                                 [c['capture_id'] for c in captures])
                self.assertTrue(all(c['complete'] and c['rows']==0 for c in captures))
                self.assertTrue(any(o['state']=='verified' and o['source']['name'].endswith('b10-finalization.json' if legacy else 'b10-final-sweep.json') for o in objects))

    def test_b10_no_kill_requires_verified_termination(self):
        for present in (True,False):
            with self.subTest(present=present):
                h=self.harness('B10','C1','b10-42-1','scientific')
                job='b10-42-1-c1-r1-loadgen-restart'; pod=h.job('loadgen-restart',job,repeat=1)
                h.status['modes'][0]['outcome']='complete'
                (h.artifacts/job/'pods.json').write_bytes(encoded({'items':[{'metadata':{'name':pod,'uid':'uid'},
                    'spec':{'containers':[{'name':'worker'}]}, 'status':{'phase':'Succeeded','containerStatuses':[{'name':'loadgen','state':{'terminated':{'exitCode':0}}}]}}]}))
                prefix=f'b10-42-1/C1/r1/b10-abrupt/{pod}/'
                identity={'run_id':'b10-42-1','scenario_id':'B10','configuration_id':'C1','phase':'C1','repeat':1,
                    'source_commit':'a'*40,'image_reference':h.phase['images']['loadgen']['reference'],
                    'job_name':job,'pod_name':pod,'pod_uid':'uid','instance_id':pod,'predecessor_instance_id':None}
                def envelope(kind, data):
                    return encoded({'schema_version':3,'dialect':'b10_abrupt','purpose':'scientific','synthetic':True,
                                    'scientific_admissible':False,'kind':kind,'identity':identity,'data':data})
                journal=b'{"kind":"fixture"}\n'
                manifest=envelope('abrupt_manifest',{'journal':{'name':'journal.jsonl','length':len(journal),'sha256':sha(journal)},
                    'journal_records':1,'report_files':[],'gaps':[]})
                h.cli.put('ledger',prefix+'registration.json',envelope('registration',{'registered_utc':'2026-01-01T00:00:00.0000000+00:00'}))
                h.cli.put('ledger',prefix+'journal.jsonl',journal)
                h.cli.put('ledger',prefix+'abrupt-manifest.json',manifest)
                data={'termination':'no_kill','outcome':None,'detail':None,'signal_observation':None,'successor_permitted':False,
                      'child':None,'exit_observed':True,'exit_code_local_observation':0,'runtime_version':'test',
                      'wait_bound_milliseconds':1,'cgroup_before':None,'cgroup_after':None,
                      'control_unconfirmed_attempt_ids':[],'kill_unconfirmed_attempt_ids':[],'capture_gaps':[],
                      'native_error_count':0,'native_fatal_count':0,'native_tail':None,
                      'manifest':{'name':'abrupt-manifest.json','length':len(manifest),'sha256':sha(manifest)}}
                if present:
                    h.cli.put('ledger',prefix+'termination.json',{'schema_version':3,'dialect':'b10_abrupt','purpose':'scientific',
                        'synthetic':True,'scientific_admissible':False,'kind':'termination','identity':identity,'data':data})
                code,m,r=h.run(); self.assertEqual(1,code)
                objects={o['source']['name']:o for o in m['objects']}
                self.assertEqual('verified' if present else 'missing',objects[prefix+'termination.json']['state'])
                units={u['job_name']:u for u in m['expected_units']}
                self.assertEqual('not_started',units['b10-42-1-c1-r1-loadgen-successor']['state'])
                self.assertEqual('not_started',units['b10-42-1-c1-r1-b10-abrupt-verify']['state'])
                missing=[g for g in m['gaps'] if g['boundary']=='missing job status' and g['expected_unit'] in
                         ('b10-42-1-c1-r1-loadgen-successor','b10-42-1-c1-r1-b10-abrupt-verify')]
                self.assertEqual(0 if present else 2,len(missing))
                self.assertEqual('verified',objects[prefix+'journal.jsonl']['state'])

    def test_b10_publication_descriptors_bind_journal_and_successor_artifacts(self):
        for fault in ('none','journal_hash','successor_hash','successor_missing'):
            with self.subTest(fault=fault):
                h=self.harness('B10','C1','b10-42-1','scientific')
                first,successor,leaves=complete_b10(h)
                if fault=='journal_hash':
                    key=('source123','ledger',first+'journal.jsonl'); h.cli.blobs[key]=b'x'*len(h.cli.blobs[key])
                elif fault=='successor_hash':
                    key=('source123','ledger',successor+'statistics.jsonl'); h.cli.blobs[key]=b'x'*len(h.cli.blobs[key])
                elif fault=='successor_missing': del h.cli.blobs[('source123','ledger',successor+'statistics.jsonl')]
                code,m,r=h.run(); self.assertEqual(1,code)
                self.assertFalse(r['archive_complete'])
                self.assertEqual(28,len(m['expected_units']))
                self.assert_fixture_diagnostics(h, m)
                self.assertEqual(161,m['totals']['expected_objects'] - 2 * 28 - sum(o['kind']=='native_monitoring_index' for o in m['objects']))
                self.assertEqual(161 if fault=='none' else 160,m['totals']['verified_objects'] - 2 * 28 - sum(o['kind']=='native_monitoring_index' and o['state']=='verified' for o in m['objects']))
                self.assertEqual(6,len(m['captures']))
                objects={o['source']['name']:o for o in m['objects']}
                journal=objects[first+'journal.jsonl']; artifact=objects[successor+'statistics.jsonl']
                self.assertEqual('failed' if fault=='journal_hash' else 'verified',journal['state'])
                self.assertEqual('failed' if fault=='successor_hash' else 'missing' if fault=='successor_missing' else 'verified',artifact['state'])
                self.assertEqual('source_hash_mismatch' if fault in ('journal_hash','successor_hash') else
                                 'source_missing' if fault=='successor_missing' else None,
                                 journal['gap_code'] if fault=='journal_hash' else artifact['gap_code'])
                for name in ('registration.json','abrupt-manifest.json','termination.json'):
                    self.assertEqual('verified',objects[first+name]['state'])
                for name in ('registration.json','completion.json',*leaves):
                    if name!='statistics.jsonl': self.assertEqual('verified',objects[successor+name]['state'])
                self.assertIsNone(r['completion'])
                self.assertIn('monitoring_incomplete', [g['code'] for g in r['gaps']])
                if fault != 'none':
                    self.assertIn('source_hash_mismatch' if fault.endswith('hash') else 'source_missing', [g['code'] for g in r['gaps']])

    def test_marker_readback_failure_leaves_data_and_false_receipt(self):
        h=self.harness('probe-batch-2','single-action','probe-42-1','smoke',items=['F06'],
                       probe_v2_selection='authentication-controls',capture_authentication_metadata=True)
        for config,stages in [('C6',('seed','action','judge')),('C9',('action','judge'))]:
            for stage in stages:
                job=f'probe-42-1-f06-{config.lower()}-{stage}'
                mode='single-action-v2-judge' if stage=='judge' else 'single-action-v2'
                pod=h.job(mode,job,'F06',config,stage=stage)
                if stage != 'judge':
                    publish_v2_actor(h, job, pod, config, stage)
                    continue
                prefix=f'probe-42-1/single-action-v2/F06/{config}/'; body=encoded(candidate_fixture('probe-42-1', config))
                h.cli.put('verifier',prefix+'candidate.json',body)
                h.cli.put('verifier',prefix+'judge-completion.json',{'schema_version':2,'mode':mode,'run_id':'probe-42-1',
                    'scenario_id':'F06','configuration_id':config,'source_commit':'a'*40,'job_name':job,'pod_name':pod,'pod_uid':'uid',
                    'candidate_name':prefix+'candidate.json','candidate_sha256':sha(body),'handler_exit_code':1,'cleanup_completed':True})
        h.cli.corrupt_marker=True
        code,m,r=h.run(); self.assertEqual(1,code); self.assertFalse(r['archive_complete'])
        self.assertIsNone(r['completion']); self.assertIsNotNone(r['manifest'])
        self.assert_fixture_diagnostics(h, m, v2=True)
        self.assertEqual(16,m['totals']['verified_objects'] - 4 - sum(o['kind']=='native_monitoring_index' and o['state']=='verified' for o in m['objects']))
        self.assertTrue(any(g['code']=='destination_hash_mismatch' for g in r['gaps']))
        self.assertTrue(any(key[0]=='archive123' and key[2].endswith('/candidate.json.metadata.json') for key in h.cli.blobs))
        self.assertFalse(any(key[0]=='archive123' and key[2].endswith('/candidate.json') for key in h.cli.blobs))

    def test_judge_has_no_monitoring_index_but_native_host_requires_one(self):
        for missing_native in (False, True):
            with self.subTest(missing_native=missing_native):
                h = self.harness('probe-batch', 'single-action', 'probe-42-1', 'batch', items=['B01'])
                complete_family(h, 'probe-batch')
                judge = 'probe-42-1-b01-c0-single-judge'
                judge_index = f'probe-42-1/C0/{judge}/uid/native-monitoring/index.json'
                self.assertNotIn(('source123', 'native-monitoring', judge_index), h.cli.blobs)
                native_job = 'probe-42-1-b01-c0-single'
                native_index = f'probe-42-1/C0/{native_job}/uid/native-monitoring/index.json'
                if missing_native:
                    del h.cli.blobs[('source123', 'native-monitoring', native_index)]
                code, manifest, receipt = h.run()
                self.assertEqual(1 if missing_native else 0, code)
                self.assertEqual(not missing_native, receipt['archive_complete'])
                self.assertFalse(any(o['source']['name'] == judge_index for o in manifest['objects']))
                if missing_native:
                    self.assertIsNone(receipt['completion'])
                    self.assertTrue(any(g['code'] == 'monitoring_incomplete' and g['expected_unit'] == native_job
                                        for g in manifest['gaps']))

    def test_native_existing_legacy_reference_binds_raw_bytes(self):
        for changed in (False, True):
            with self.subTest(changed=changed):
                h = self.harness('probe-batch', 'single-action', 'probe-42-1', 'batch', items=['B01'])
                complete_family(h, 'probe-batch')
                job = 'probe-42-1-b01-c0-single'; pod = job + '-pod'
                source = f'probe-42-1/single-action/B01/C0/instances/{pod}/capture.json'
                key = ('source123', 'single-action', source)
                capture = json.loads(h.cli.blobs[key])
                raw = b'{"name":"native"}'
                capture['repeats'][0]['statistics'] = [{'sequence':1, 'observed_elapsed_ms':0,
                                                         'raw_base64':base64.b64encode(raw).decode()}]
                h.cli.blobs[key] = encoded(capture)
                index_key = ('source123', 'native-monitoring', f'probe-42-1/C0/{job}/uid/native-monitoring/index.json')
                index = json.loads(h.cli.blobs[index_key])
                index['clients'][0].update(client_id=f'{pod}-r1', report_count=1, raw_sources=[{
                    'kind':'existing_capture', 'source':'single-action/' + source, 'byte_length':len(raw),
                    'sha256':('f'*64 if changed else sha(raw)), 'report_ordinal':1, 'row_count':1}])
                h.cli.blobs[index_key] = encoded(index)
                code, manifest, receipt = h.run()
                self.assertEqual(1 if changed else 0, code)
                self.assertEqual(not changed, receipt['archive_complete'])
                if changed:
                    self.assertIsNone(receipt['completion'])
                    self.assertTrue(any(g['code']=='monitoring_incomplete' for g in manifest['gaps']))
                else:
                    self.assertFalse(any(o['kind']=='native_monitoring_raw' for o in manifest['objects']))

    def test_native_index_and_chunk_fail_closed(self):
        for fault in ('missing', 'malformed', 'identity', 'hash', 'truncated'):
            with self.subTest(fault=fault):
                h = self.harness('probe-batch', 'single-action', 'probe-42-1', 'batch', items=['B01'])
                complete_family(h, 'probe-batch')
                job = 'probe-42-1-b01-c0-single'
                prefix = f'probe-42-1/C0/{job}/uid/native-monitoring/'
                key = ('source123', 'native-monitoring', prefix + 'index.json')
                document = json.loads(h.cli.blobs[key])
                if fault == 'missing':
                    del h.cli.blobs[key]
                elif fault == 'malformed':
                    h.cli.blobs[key] = b'{"schema_version":1,"schema_version":1}'
                elif fault == 'identity':
                    document['identity']['pod_uid'] = 'wrong'
                    h.cli.blobs[key] = encoded(document)
                elif fault == 'truncated':
                    document['capture_complete'] = False
                    document['diagnostics_truncated'] = True
                    h.cli.blobs[key] = encoded(document)
                else:
                    raw = b'{"client_ordinal":1,"report_ordinal":1,"raw":"PRIVATE_NATIVE_SENTINEL"}\n'
                    chunk = prefix + 'raw-000001.jsonl'
                    document['clients'][0].update(report_count=1, raw_sources=[{
                        'kind':'raw_chunk', 'source':chunk, 'byte_length':len(raw), 'sha256':'f'*64,
                        'report_ordinal':1, 'row_count':1}])
                    h.cli.put('native-monitoring', chunk, raw)
                    h.cli.blobs[key] = encoded(document)
                code, manifest, receipt = h.run()
                self.assertEqual(1, code)
                self.assertFalse(receipt['archive_complete'])
                self.assertIsNone(receipt['completion'])
                self.assertTrue(any(g['code'] in ('monitoring_incomplete', 'diagnostics_truncated', 'source_hash_mismatch')
                                    for g in manifest['gaps']))
                for path in (h.artifacts/'archive').iterdir():
                    self.assertNotIn(b'PRIVATE_NATIVE_SENTINEL', path.read_bytes())

    def test_complete_nonledger_families_and_missing_immutable_reference(self):
        cases=[('probe-batch','single-action','probe-42-1',['B01']),
               ('idle-observation','idle-observation','idle-42-1',['A02']),
               ('ledger-batch','ledger-batch','ledger-42-1',['B06','B07'])]
        for scenario,phase,run,items in cases:
            for missing in (False,True):
                with self.subTest(scenario=scenario,missing=missing):
                    h=self.harness(scenario,phase,run,'batch',items=items)
                    container,name,units,count,captures=complete_family(h,scenario)
                    if missing: del h.cli.blobs[('source123',container,name)]
                    code,m,r=h.run()
                    self.assertEqual(1 if missing else 0,code)
                    self.assertEqual(not missing,r['archive_complete'])
                    self.assertEqual(units,len(m['expected_units']))
                    self.assert_fixture_diagnostics(h, m)
                    self.assertTrue(all(u['state']=='started' for u in m['expected_units']))
                    self.assertEqual(count,m['totals']['expected_objects'] - 2 * units - sum(o['kind']=='native_monitoring_index' for o in m['objects']))
                    self.assertEqual(count-int(missing),m['totals']['verified_objects'] - 2 * units - sum(o['kind']=='native_monitoring_index' and o['state']=='verified' for o in m['objects']))
                    self.assertEqual(captures,len(m['captures']))
                    self.assertTrue(all(c['complete'] and c['rows']==0 for c in m['captures']))
                    byname={o['source']['name']:o for o in m['objects']}
                    self.assertEqual('missing' if missing else 'verified',byname[name]['state'])
                    prefix=m['destination']['prefix']
                    manifest=h.cli.blobs[('archive123','run-evidence',prefix+'archive-manifest.json')]
                    self.assertEqual(sha(manifest),r['manifest']['sha256'])
                    if missing:
                        self.assertIsNone(r['completion'])
                        self.assertEqual(['source_missing'],[g['code'] for g in r['gaps']])
                    else:
                        marker=h.cli.blobs[('archive123','run-evidence',prefix+'archive-completion.json')]
                        self.assertEqual(sha(marker),r['completion']['sha256'])
                        a.verify_controls(manifest,marker,encoded(r))

    def test_legacy_capture_projection_binds_source_and_excludes_body(self):
        h=self.harness('probe-batch','single-action','probe-42-1','batch',items=['B01'])
        complete_family(h,'probe-batch')
        code, manifest, receipt=h.run()
        self.assertEqual(0,code); self.assertTrue(receipt['archive_complete'])
        name='probe-42-1/single-action/B01/C0/instances/probe-42-1-b01-c0-single-pod/capture.json'
        source=h.cli.blobs[('source123','single-action',name)]
        obj=next(o for o in manifest['objects'] if o['source']['name']==name)
        self.assertEqual('source_metadata_projection',obj['kind'])
        projected=h.cli.blobs[('archive123','run-evidence',obj['destination_name'])]
        self.assertEqual((len(projected),sha(projected)),(obj['bytes'],obj['sha256']))
        envelope=json.loads(projected)
        self.assertEqual({'container':'single-action','name':name,'etag':'etag','bytes':len(source),'sha256':sha(source)},envelope['source'])
        self.assertEqual('source_metadata_projection',envelope['kind']); self.assertEqual('single_action_capture',envelope['format'])
        self.assertEqual({'is_null':False,'length':11,'sha256':sha(b'BODY_SECRET')},envelope['metadata']['repeats'][0]['matched']['value_base64'])
        self.assertEqual('aw==',envelope['metadata']['repeats'][0]['matched']['key_base64'])
        self.assertNotIn(b'Qk9EWV9TRUNSRVQ=',projected)
        self.assertNotIn(b'BODY_SECRET',b''.join(v for (account, _, _),v in h.cli.blobs.items() if account=='archive123'))

    def test_invalid_legacy_identity_is_retained_with_gap(self):
        h=self.harness('idle-observation','idle-observation','idle-42-1','batch',items=['A02'])
        complete_family(h,'idle-observation')
        name='idle-42-1/idle-observation/A02/C0/instances/idle-42-1-a02-c0-idle-pod/capture.json'
        key=('source123','ledger',name); value=json.loads(h.cli.blobs[key]); value['identity']['run_id']='wrong-42-1'
        h.cli.blobs[key]=encoded(value)
        code, manifest, receipt=h.run()
        self.assertEqual(1,code); self.assertFalse(receipt['archive_complete']); self.assertIsNone(receipt['completion'])
        obj=next(o for o in manifest['objects'] if o['source']['name']==name)
        self.assertEqual('rejected',obj['state'])
        self.assertEqual(h.cli.blobs[key],h.cli.blobs[('archive123','run-evidence',obj['destination_name'])])
        self.assertTrue(any(g['code']=='monitoring_incomplete' for g in manifest['gaps']))

    def test_interruption_during_inventory_preserves_verified_objects_and_manifest(self):
        h=self.harness('probe-batch','single-action','probe-42-1','batch',items=['B01'])
        complete_family(h,'probe-batch')
        original=h.cli.__class__.__call__
        interrupted=[]
        def command(cli,args,deadline):
            name=args[args.index('--name')+1]
            account=args[args.index('--account-name')+1]
            if not interrupted and account=='source123' and name.endswith('/instances/probe-42-1-b01-c0-single-pod/completion.json'):
                interrupted.append(name); raise KeyboardInterrupt()
            return original(cli,args,deadline)
        with patch.object(BlobCLI,'__call__',command): code,m,r=h.run()
        self.assertEqual(1,code); self.assertEqual(1,len(interrupted))
        self.assertFalse(r['archive_complete']); self.assertIsNone(r['completion']); self.assertIsNotNone(r['manifest'])
        self.assertTrue(any(g['code']=='interrupted' for g in r['gaps']))
        self.assertGreaterEqual(m['totals']['verified_objects'],3)
        verified=[o for o in m['objects'] if o['state']=='verified']
        self.assertTrue(any(o['source']['name'].endswith('/capture.json') for o in verified))
        for obj in verified:
            archived=h.cli.blobs[('archive123','run-evidence',obj['destination_name'])]
            self.assertEqual(obj['sha256'],sha(archived))
        prefix=m['destination']['prefix']; canonical=h.cli.blobs[('archive123','run-evidence',prefix+'archive-manifest.json')]
        self.assertEqual((h.artifacts/'archive/archive-manifest.json').read_bytes(),canonical)
        self.assertEqual(sha(canonical),r['manifest']['sha256'])
        self.assertNotIn(('archive123','run-evidence',prefix+'archive-completion.json'),h.cli.blobs)
        self.assertEqual([],list((h.private/'archive').iterdir()))

    def test_other_batch_families_keep_observed_and_missing_inventory(self):
        cases=[('probe-batch','single-action','probe-42-1',['B01'],'single-action','probe-42-1-b01-c0-single','B01','C0','single-action','probe-42-1/single-action/B01/C0/instances/'),
               ('idle-observation','idle-observation','idle-42-1',['A02'],'idle-observation','idle-42-1-a02-c0-idle','A02','C0','ledger','idle-42-1/idle-observation/A02/C0/instances/'),
               ('ledger-batch','ledger-batch','ledger-42-1',['B06','B07'],'start-line','ledger-42-1-b06-c0-r1-start-line','B06','C0','verifier','ledger-42-1-b06/C0/r1/')]
        for scenario,phase,run,items,mode,job,item,config,container,prefix in cases:
            with self.subTest(scenario=scenario):
                h=self.harness(scenario,phase,run,'smoke' if scenario=='ledger-batch' else 'batch',items=items)
                pod=h.job(mode,job,item,config,repeat=1 if scenario=='ledger-batch' else None)
                name=prefix+('start-line.json' if scenario=='ledger-batch' else pod+'/capture.json')
                h.cli.put(container,name,b'available failed evidence')
                code,m,r=h.run(); self.assertEqual(1,code); self.assertFalse(r['archive_complete'])
                objects={o['source']['name']:o for o in m['objects']}
                self.assertTrue(any(o['state']=='not_started' for o in m['objects']))
                self.assertTrue(any(g['code']=='monitoring_incomplete' for g in m['gaps']))
                if scenario == 'ledger-batch':
                    self.assertEqual('verified',objects[name]['state'])
                    self.assertEqual(b'available failed evidence',h.cli.blobs[('archive123','run-evidence',objects[name]['destination_name'])])
                else:
                    # Unknown captures may contain bodies: retain an explicit gap and never copy their bytes.
                    self.assertNotEqual('verified',objects[name]['state'])
                    self.assertNotIn(b'available failed evidence',b''.join(v for (account, _, _),v in h.cli.blobs.items() if account=='archive123'))

if __name__ == '__main__': unittest.main()

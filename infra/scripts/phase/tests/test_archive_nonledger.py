import importlib.util
import json
import pathlib
import tempfile
import unittest

FILE = pathlib.Path(__file__).resolve().parents[1] / 'archive_nonledger.py'
spec = importlib.util.spec_from_file_location('archive_nonledger', FILE)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

class Fake:
    def __init__(self): self.calls, self.gaps, self.contents = [], [], {}
    def gap(self, code, boundary, name=None, unit=None): self.gaps.append((code,boundary,name,unit))
    def source(self, container, name, kind, **kw):
        self.calls.append(('source',container,name,kind,kw))
        if name in self.contents and kw.get('inspect_source'):
            with tempfile.NamedTemporaryFile() as f:
                f.write(json.dumps(self.contents[name]).encode()); f.flush()
                kw['inspect_source'](f.name, pathlib.Path(f.name).stat().st_size)
    def v2_candidate_projection(self, container, name, identity, expected_sha256):
        self.calls.append(('source', container, name, 'candidate', {'expected_sha256': expected_sha256, 'identity': identity}))
    def not_started(self, container, name, kind, **kw): self.calls.append(('not_started',container,name,kind,kw))
    def project_pods(self, raw):
        v = json.loads(raw)
        return json.dumps({'items':[{'name':x['metadata']['name'],'uid':x['metadata']['uid']} for x in v['items']]}).encode()
    def legacy_source(self, container, name, kind, mode, identity, run_kind): self.source(container, name, kind)
    def legacy_capture_projection(self, container, name, mode, identity, run_kind): self.source(container, name, 'capture')
    def optional_source(self,*args,**kwargs): self.calls.append(('optional',*args))
    def producer(self,*args): self.calls.append(('producer',*args))
    def capture_name(self,run,phase,mode,job,uid,index,repeat): return ('capture-id',f'{run}/{phase}/r{repeat}/capture-id/')
    def capture(self,*args): self.calls.append(('capture',*args))

class InventoryTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(); self.addCleanup(self.tmp.cleanup)
        self.artifacts = pathlib.Path(self.tmp.name)
        self.containers = {'ledger':'ledger','verifier':'verifier','single_action':'single-action'}
        self.phase = {'run_id':'probe-1-1','scenario':'probe-batch','run_kind':'batch','items':['B01'],'source_commit':'a'*40,
                      'images':{w:{'digest':'sha256:'+'b'*64} for w in ('loadgen','verifier')}}
        self.f = Fake()
    def add_pod(self,job):
        d=self.artifacts/job; d.mkdir(); (d/'pods.json').write_text(json.dumps({'items':[{'metadata':{'name':job+'-pod','uid':'1234'}}]}))
    def call(self, records): return m.collect(self.f,self.phase,{'modes':records},self.containers,self.artifacts)
    def test_v1_selected_capture_and_missing_job(self):
        job='probe-1-1-b01-c0-single'; self.add_pod(job)
        units=self.call([{'mode':'single-action','item':'B01','configuration':'C0','repeat':None,'job':job,'apply':0}])
        names=[x[2] for x in self.f.calls]
        self.assertIn('probe-1-1/single-action/B01/C0/instances/'+job+'-pod/capture.json',names)
        self.assertIn('probe-1-1/single-action/B01/C0/judge-completion.json',names)
        self.assertEqual('started',units[0]['state'])
        self.assertTrue(any(g[1]=='missing job status' for g in self.f.gaps))
        self.assertFalse(any('/B03/' in n for n in names))
    def test_v1_smoke_has_no_blob_publications(self):
        self.phase['run_kind']='smoke'
        self.call([])
        self.assertEqual([],self.f.calls)
    def test_idle_aggregate_receipt_and_candidate(self):
        self.phase.update(scenario='idle-observation',items=['A02'])
        job='probe-1-1-idle-judge'; self.add_pod(job)
        self.call([{'mode':'idle-observation-judge','item':None,'configuration':None,'repeat':None,'job':job,'apply':0}])
        names=[x[2] for x in self.f.calls]
        self.assertIn('probe-1-1/idle-observation/judge-completion.json',names)
        self.assertIn('probe-1-1/idle-observation/A02/judge/candidate.json',names)
        self.assertNotIn('probe-1-1/idle-observation/A03/judge/candidate.json',names)
    def test_v2_auth_controls_and_stage_inventory(self):
        self.phase.update(scenario='probe-batch-2',run_kind='smoke',items=['F06'],capture_authentication_metadata=True,probe_v2_selection='authentication-controls')
        units=self.call([])
        self.assertEqual(['probe-1-1-f06-c6-seed','probe-1-1-f06-c6-action','probe-1-1-f06-c6-judge',
                          'probe-1-1-f06-c9-action','probe-1-1-f06-c9-judge'],[x['job_name'] for x in units])
        self.assertEqual(4,len(self.f.calls))
        self.assertTrue(all(x[0]=='not_started' for x in self.f.calls))
    def test_v2_receipt_binds_candidate_digest_and_identity(self):
        self.phase.update(scenario='probe-batch-2',items=['B08'],probe_v2_selection='B08')
        job='probe-1-1-b08-c0-judge'; self.add_pod(job)
        prefix='probe-1-1/single-action-v2/B08/C0/'
        self.f.contents[prefix+'judge-completion.json']={
            'schema_version':2,'mode':'single-action-v2-judge','run_id':'probe-1-1','scenario_id':'B08',
            'configuration_id':'C0','source_commit':'a'*40,'job_name':job,'pod_name':job+'-pod','pod_uid':'1234',
            'candidate_name':prefix+'candidate.json','candidate_sha256':'b'*64,'handler_exit_code':1,'cleanup_completed':True}
        self.call([{'mode':'single-action-v2-judge','item':'B08','configuration':'C0','stage':'judge',
                    'repeat':None,'job':job,'apply':0}])
        candidate=next(x for x in self.f.calls if x[0]=='source' and x[2]==prefix+'candidate.json')
        self.assertEqual('b'*64,candidate[4]['expected_sha256'])
        self.assertFalse(any(g[1]=='judge receipt contract' for g in self.f.gaps))
    def test_malformed_receipt_keeps_candidate_and_records_gap(self):
        self.phase.update(scenario='probe-batch-2',items=['B08'],probe_v2_selection='B08')
        job='probe-1-1-b08-c0-judge'; self.add_pod(job)
        prefix='probe-1-1/single-action-v2/B08/C0/'
        self.f.contents[prefix+'judge-completion.json']={'candidate_name':'escape'}
        self.call([{'mode':'single-action-v2-judge','item':'B08','configuration':'C0','stage':'judge',
                    'repeat':None,'job':job,'apply':0}])
        self.assertTrue(any(g[1]=='judge receipt contract' for g in self.f.gaps))
        self.assertTrue(any(x[0]=='source' and x[2]==prefix+'candidate.json' for x in self.f.calls))
    def test_ledger_batch_producer_and_verify_capture(self):
        self.phase.update(run_id='ledger-1-1',scenario='ledger-batch',items=['B06','B07'])
        jobs=['ledger-1-1-b06-c0-r1-loadgen','ledger-1-1-b06-c0-r1-verify']
        for j in jobs:self.add_pod(j)
        records=[{'mode':mode,'item':'B06','configuration':'C0','repeat':1,'job':job,'apply':0} for mode,job in zip(('loadgen','verify'),jobs)]
        units=self.call(records)
        self.assertEqual(64,len(units))
        self.assertTrue(any(x[0]=='producer' and x[2]=='ledger-1-1-b06' for x in self.f.calls))
        self.assertTrue(any(x[0]=='capture' and x[3]['scenario_id']=='B06' for x in self.f.calls))
        self.assertTrue(any(x[2]=='ledger-1-1-b07/C1/candidate.json' for x in self.f.calls if x[0]=='not_started'))
    def test_ledger_smoke_only_one_repeat_no_judge(self):
        self.phase.update(run_id='ledger-1-1',scenario='ledger-batch',run_kind='smoke',items=['B06','B07'])
        units=self.call([])
        self.assertEqual(12,len(units))
        self.assertFalse(any(u['mode']=='judge' for u in units))

if __name__ == '__main__': unittest.main()

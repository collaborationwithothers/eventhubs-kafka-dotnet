import collections, datetime, hashlib, json, os, pathlib, signal, subprocess, sys, time, xml.etree.ElementTree as ET
root=pathlib.Path('/workspace/eventhubs-kafka-dotnet'); phase=sys.argv[1]; dest=root/'.artifacts/issue-80-pr6a/missing-parent'/phase; dest.mkdir(parents=True,exist_ok=True)
env=os.environ.copy(); overrides={'MSBUILDDISABLENODEREUSE':'1','DOTNET_CLI_USE_MSBUILD_SERVER':'0'}; env.update(overrides); env['PATH']='/workspace/.dotnet:'+env['PATH']
method='Parchwell.Verifier.Tests.ProbeGate.ProbeGateHostTests.'+('Given_RepeatResultParentIsMissing_When_ProductionProcessRuns_Then_RetainsAllRepeatsAndReportsMissingResult' if phase=='red' else '*')
cmd=['dotnet','test','--project','tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj','--configuration','Release','--no-build','--minimum-expected-tests','1','--results-directory',str(dest),'--','--filter-method',method,'--report-xunit-xml','--report-xunit-xml-filename','results.xml']
sources={}
for name in ['src/Parchwell.Verifier/ProbeGate/ProbeGateHandler.cs','tests/Parchwell.Verifier.Tests/ProbeGate/ProbeGateHostTests.cs']:
 data=(root/name).read_bytes(); sources[name]=hashlib.sha256(data).hexdigest(); (dest/pathlib.Path(name).name).write_bytes(data)
head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(); start=time.monotonic(); started=datetime.datetime.now(datetime.timezone.utc).isoformat(); timed_out=False
with (dest/'command.log').open('w') as out:
 process=subprocess.Popen(cmd,cwd=root,env=env,stdout=out,stderr=subprocess.STDOUT,start_new_session=True)
 try: rc=process.wait(timeout=300)
 except subprocess.TimeoutExpired:
  timed_out=True; os.killpg(process.pid,signal.SIGKILL); process.wait(); rc=124
record={'argv':cmd,'cwd':str(root),'exit_code':rc,'elapsed_seconds':time.monotonic()-start,'timeout_seconds':300,'timed_out':timed_out,'started_utc':started,'finished_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'tested_head':head,'source_sha256':sources,'environment_overrides':overrides}
record['source_inputs_unchanged']=all(hashlib.sha256((root/n).read_bytes()).hexdigest()==v for n,v in sources.items()); (dest/'command.json').write_text(json.dumps(record,indent=2)+'\n'); print(json.dumps(record)); print((dest/'command.log').read_text()[-9000:])
xml=dest/'results.xml'
if xml.exists():
 tree=ET.parse(xml); tests=[dict(t.attrib) for t in tree.findall('.//test')]; counts=collections.Counter(t['result'] for t in tests)
 summary={'phase':phase,'test_command_exit_code':rc,'executed':len(tests),'passed':counts['Pass'],'failed':counts['Fail'],'skipped':counts['Skip'],'assembly_counts':[a.attrib for a in tree.findall('./assembly')],'tests':tests,'artifact_sha256':{n:hashlib.sha256((dest/n).read_bytes()).hexdigest() for n in ['command.json','command.log','results.xml','ProbeGateHandler.cs','ProbeGateHostTests.cs']}}
 (dest/'summary.json').write_text(json.dumps(summary,indent=2)+'\n'); print(json.dumps({k:v for k,v in summary.items() if k not in ['tests','assembly_counts','artifact_sha256']}))

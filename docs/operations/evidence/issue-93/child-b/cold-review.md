# Verifier coverage B cold-review record

The first cold review found that the temporary evidence and receipt helpers were missing from its review diff. This record preserves their exact sources outside raw so the next fresh review can examine all authored reconciliation and rendering logic.

## Round 1

Reviewer: `gpt-6.1-sol`, high effort, fresh context applying the repository `repo-review` skill. Inputs were only the review-diff path and raw-manifest path; the reviewer could open repository rules and cited evidence.

Review diff SHA-256: `9903871af456c79e1cca6f663de4249fe5e1c6c955409c9419ee0a4b7df81a8d`. Raw-manifest SHA-256 at review: `ce00fb793a483d30bb9d46ad9e642d45eb0c54706fbeead903763bbd0832e18a`.

The review applied the rubric's anything row to every changed file and the .NET row to every changed test file. Its result was:

```text
P1 docs/operations/evidence/issue-93/child-b/raw-manifest.json:5 AGENTS.md "Before opening a pull request", rule 8: Authored evidence-producing and receipt-rendering scripts are omitted from the review diff. The producer names /tmp/issue-100/reconcile.py; that script, build_receipt.py, supplement.py, docs-pass.py and manifest.py contain authored reconciliation, variant mappings, accounting and rendering logic but appear nowhere in the frozen diff. Retain their exact source outside raw and include it in the next review diff.
Findings: 1 at P0 or P1
Pre-existing (not blocking): None identified.
```

Disposition: fixed by retaining all temporary Python and shell helper sources below, including the authority refresh and the plan-extracted setup/acceptance wrappers. No assertion, test input, production file, package, threshold or exclusion changed in response. The full revised diff, including this record, requires the next fresh cold review before push.

## Exact helper sources

These are audit copies of the commands used in this session, not new executable repository helper paths. The setup producer is extracted from the approved plan; the other sources retain command execution, literal mappings, evidence rendering and provenance. Their original temporary paths remain the producer locators in the raw manifest. File SHA-256 values below were computed from the exact bytes copied here.

### build_receipt.py

Original path: `/tmp/issue-100/build_receipt.py`. SHA-256: `8d52b6a6f8ad788c049948ef16aaee216a10bb1b6217a69fa24604fb422e1bf5`.

```python
#!/usr/bin/env python3
"""Render the approved fixed B assignment and receipt only when invoked explicitly."""
import argparse, hashlib, json, pathlib, re, collections
BASE='09a70930e471353b97ad07f9560e9734e562294e'
GH='https://github.com/collaborationwithothers/eventhubs-kafka-dotnet'
REPORT='f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492'
DECISION=GH+'/issues/100#issuecomment-6301526641'
CHECK=GH+'/issues/100#issuecomment-6301817595'
PLAN=GH+'/issues/100#issuecomment-6301492532'
APPROVAL=GH+'/issues/100#issuecomment-6306182797'
PENDING='pending #93 closure run under #89 and remaining Q5 dispositions'
PREFIX='Parchwell.Verifier.Tests.'
def name(cls,method,args=None): return PREFIX+cls+'.'+method+('('+args+')' if args is not None else '')
# These are literal selectors from the approved plan, not source-generated cases.
WITNESSES={
'C1':[name('Judgement.ConfigurationFidelityTests','Given_SettingDifferencesAtDiagnosticCap_When_RunJudgementRetains_Then_OrdinalOrderSurvives',f'differenceCount: {i}') for i in (1,2,3)],
'C2':[name('Judgement.RunJudgementTests','Given_SyntheticNoncanonicalWindowAssessment_When_Judging_Then_CurrentStatusPredicateIsPreserved',f'failedWithoutClassification: {b}') for b in ('True','False')],
'S2':[name('Judgement.StatisticsChecksTests','Given_SyntheticBrokerAdditionAtBoundary_When_Evaluated_Then_Check26EmitsExactDiagnosticTuple')],
'W1':[name('A01.A01EvidenceRecorderTests','Given_FailedLedgerWithRetainedValue_When_Recorded_Then_DeliveryCodesRemainUnavailable')],
'W2':[name('Window.MessageIdentityReaderTests','Given_BrokenIdentityHeader_When_Reading_Then_CheckSixOwnsProblem',f'field: "{f}", mode: "utf8"') for f in ('instance-id','attempt-id','sequence')]+[name('Window.MessageIdentityReaderTests','Given_NullOrInvalidKey_When_Reading_Then_CheckSixOwnsProblem',f'invalid: {b}') for b in ('True','False')],
'W3':[name('Window.RunWindowTests','Given_SuppliedEndHighs_When_Creating_Then_RetainsIndependentSnapshot')],
'W4':[name('Window.RunWindowTests','Given_EmptyProblemFailure_When_Creating_Then_FallbackCheckSevenFails')],
'W5':[name('Window.WindowConsumerTests','Given_PreCancelledWork_When_Draining_Then_NoNativeOperationStarts')],
'W6':[name('Window.WindowConsumerTests','Given_WorkCancelledDuringAssignment_When_Draining_Then_NoPositionOrPollStarts')],
'Standing4899':[name('Judgement.LateWriteChecksTests','Given_ManifestMarkersAndCompleteness_When_Evaluated_Then_OnlyStandingMarkersAndTrueCompletenessPass','markers: "broker_lifecycle_inventory_unproven", complete: True, pass: True')],
'Standing4900':[name('Judgement.LateWriteChecksTests','Given_ManifestMarkersAndCompleteness_When_Evaluated_Then_OnlyStandingMarkersAndTrueCompletenessPass','markers: "request_lifecycle_capture_unsupported", complete: True, pass: True')],
'Poll':[name('Window.WindowConsumerTests','Given_PollSliceAroundRemainingSafety_When_Draining_Then_NativeTimeoutUsesSmallerDuration',f'slice: {s}, expected: {e}') for s,e in ((99,99),(100,100),(101,100))],
}
FIELDS={'name':['5224','5225'],'nodeid':['5226','5227'],'source':['5229'],'state':['5230','5231'],'req_timeouts':['5233','5350'],'txretries':['5233','5351'],'txerrs':['5233','5352'],'rxerrs':['5233','5353'],'disconnects':['5233','5354'],'throttle.cnt':['5245','5246','5369','5372'],'throttle.max':['5245','5246','5373','5380','5381'],'rtt.cnt':['5248','5249','5369','5372'],'rtt.p99':['5248','5249','5373','5379','5382']}
MAP={'2657':['W1'],'4875':[],'4899':['Standing4899'],'4900':['Standing4900'],'5076':['C2'],'5095':['C1'],'5147':[],'5315':[],'5338':['S2'],'5531':['W2'],'5532':['W2'],'5568':['W3'],'5574':['W4'],'5580':['W4'],'5638':['W5'],'5643':['W6'],'5661':['Poll']}
for field,ids in FIELDS.items():
 key='S1:'+field
 WITNESSES[key]=[name('Judgement.StatisticsChecksTests','Given_SyntheticRequiredBrokerFieldMissingWithoutParserProblem_When_Evaluated_Then_Check21EmitsExactFallbackDiagnostic',f'field: "{field}"')]
 for mid in ids: MAP.setdefault(mid,[]).append(key)
for mid in ('5366','5367'): MAP[mid]=['S1:'+f for f in FIELDS]
PROOFS={'4875':'P1','5147':'P2','5315':'P3','5531':'P4','5661':'P5'}
def load(path): return json.loads(path.read_text())
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def inventory(repo,cases=None):
 path=repo/'docs/operations/evidence/issue-92-pr6b/review/dispositions.json'
 frozen=load(path); selected=[x for x in frozen if x['mutant_id'] in MAP]
 assert len(selected)==43 and {x['mutant_id'] for x in selected}==set(MAP)
 byname={}
 if cases is not None:
  assert len(cases)==477 and len({x['id'] for x in cases})==477 and len({x['name'] for x in cases})==477
  assert all(x['result']=='Pass' for x in cases)
  byname={x['name']:x for x in cases}
  missing=sorted({n for ns in WITNESSES.values() for n in ns}-set(byname))
  if missing: raise ValueError('Missing exact approved witness identities: '+repr(missing))
 lines=path.read_text().splitlines(); locations={}
 for number,line in enumerate(lines,1):
  m=re.search(r'"mutant_id": "([0-9]+)"',line)
  if m: locations[m.group(1)]=number
 rows={}
 for frozen_variant in selected:
  v=dict(frozen_variant); mid=v['mutant_id']; line=v['span']['start']['line']; key=v['line_key']
  groups=MAP[mid]; names=sorted({n for g in groups for n in WITNESSES[g]})
  v.update(witness_groups=groups,proof=PROOFS.get(mid),exact_cases=[byname[n] if cases is not None else {'name':n,'id':None,'result':None} for n in names],frozen_record_url=f'{GH}/blob/{BASE}/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L{locations[mid]}')
  row=rows.setdefault(key,{'line_key':key,'file':v['file'],'line':line,'source_location':f'src/Parchwell.Verifier/{v["file"]}:{line}','variants':[]})
  row['variants'].append(v)
 rows=sorted(rows.values(),key=lambda x:(x['file'],x['line']))
 assert len(rows)==29
 return {'report_sha256':REPORT,'report_tested_commit':'a2001534d94cd1135b9af53dc0e9d9816b00ce3d','selected_baseline':BASE,'line_key_count':len(rows),'variant_count':len(selected),'rows':rows}
def code(value): return '`'+str(value).replace('`','&#96;').replace('|',r'\|').replace('\n',r'\n')+'`'
def render(repo,data,reconciliation):
 raw=repo/'docs/operations/evidence/issue-93/child-b/raw'
 suite=load(raw/'suite-1/controller.json'); tested=load(raw/'suite-1/tested-source.json'); setup=load(raw/'setup-1/setup.json')
 # Preserve producers' actual schemas rather than inventing successful output fields.
 retained=load(raw/'suite-1/retention.json')
 for invocation in ('suite-1','discovery-1','focused-1'):
  controller=load(raw/invocation/'controller.json')
  assert controller['exit_code']==0 and controller['termination'] is None and controller['source_inputs_unchanged'] is True and controller['execution_evidence_complete'] is True, invocation
 metadata={k:v for k,v in tested.items() if k!='source_input_sha256'}
 ledger_path=raw/'controller-invocations.json'
 if not ledger_path.exists(): ledger_path=repo/'.artifacts/issue-100/controller-invocations.json'
 ledger=load(ledger_path)
 out=['This receipt records child B\'s completed ordinary verifier regressions and the approved callable-domain proofs. Mutation evidence for A and B and residual parent dispositions remain pending at #93 closure under #89.','',
 '## Authority and tested inputs','',f'[Plan Revision 2]({PLAN}), updated `2026-10-20T21:19:10Z`, was [independently checked]({CHECK}) at `2026-10-20T21:25:04Z` and [approved for Codex]({APPROVAL}) at `2026-10-21T02:03:26Z`. The [child decision]({DECISION}) adopts the parent split, approved item Revision 2 and the two transfers. [Predecessor #98]('+GH+'/pull/98) merged at `2026-10-20T20:57:20Z`, commit `'+BASE+'`, the selected source baseline.','',
 'The suite [tested-source manifest](raw/suite-1/tested-source.json) records the actual tested commit/tree and every source/test/configuration input hash. The [suite controller](raw/suite-1/controller.json), [discovery controller](raw/discovery-1/controller.json) and [focused controller](raw/focused-1/controller.json) record exact commands, caps, outcomes and input comparisons. Documentation and authored evidence were completed after execution; they are not represented as additional tested production inputs.','',
 'The [setup receipt](raw/setup-1/setup.json), [SDK selection](raw/setup-1/sdk-selection.json) and retained setup logs record the actual SDK, runtime, effective NuGet sources and Docker prerequisites. One setup invocation is permitted, capped at 600 seconds. B\'s own child decision permits the Linux agent suite; A\'s additional suite allowances do not transfer.','',
 'Actual tested-source metadata (verbatim JSON):','', '```json',json.dumps(metadata,indent=2),'```','',
 '## Ordinary execution and consumed allowances','',
 'The fixed [case reconciliation](raw/case-reconciliation.json) contains exact discovered and executed identifiers and outcomes. All selected identities must appear exactly once and pass. No mutation run or exact replay is part of this acceptance.','',
 '| Measure | Actual value |','| --- | --- |']
 for k in ('baseline_count','added_count','expected_count','class_counts'):out.append('| '+k+' | '+code(json.dumps(reconciliation[k],sort_keys=True))+' |')
 out+=['','The actual full-suite temporary directory and exact retention copy command are recorded in [retention.json](raw/suite-1/retention.json):','', '```json',json.dumps(retained,indent=2),'```','',
 'All consumed controller invocations, including failures or incomplete attempts, are retained below. Controller output is evidence of its own recorded status; ordinary success never establishes a mutant kill.','', '```json',json.dumps(ledger,indent=2),'```','',
 'Approved maxima are one SDK setup at 600 seconds, two suites at 1800 seconds each, one discovery at 300 seconds, two focused executions at 300 seconds each, two XML validations at 120 seconds each and two documentation passes at 300 seconds each. The same 14400-second investigation/proof/reconciliation allowance covers the expanded B scope; no resumed-session reset or additional retry is implied.','',
 '## Assigned inventory','',
 'The fixed B assignment contains 29 original line keys and 43 variants. Literal spans, mutators and replacements below are copied from the frozen report records; original IDs are report locators, not future report IDs. Each case ID points to the exact discovered/executed name in the case appendix and [raw reconciliation](raw/case-reconciliation.json); the execution source is [focused XML](raw/focused-1/results.xml).','',
 '| Original line key | Frozen variants: span, mutator, literal replacement | Exact case IDs / proof | Ordinary regression status | Mutation / proof / gap status and decision |','| --- | --- | --- | --- | --- |']
 for row in data['rows']:
  vv=[]; ids=[]; proof=[]
  for v in row['variants']:
   span=v['span']; a=span['start']; b=span['end']; mid=v['mutant_id']
   vv.append(f'[{mid} {a["line"]}:{a["column"]}-{b["line"]}:{b["column"]}]({v["frozen_record_url"]}) '+code(v['mutator'])+' '+code(v['replacement'])+' => '+(', '.join(code(c['id']) for c in v['exact_cases']) if v['exact_cases'] else code(v['proof'])))
   ids += [c['id'] for c in v['exact_cases']]
   if v['proof']: proof.append(f'[{v["proof"]} for {mid}](proofs.md)')
  ids=sorted(set(ids)); ordinary='Pass; exact cases '+', '.join(ids) if ids else 'Not applicable; callable-domain proof only'
  status=('Listed proof(s) accepted under Q3 by [independent plan check]('+CHECK+'); only their named variants have proof status; no observed kill. ' if proof else '')+'Mutation evidence deferred; no accepted gap. [Child decision]('+DECISION+')'
  if any(v['mutant_id']=='2657' for v in row['variants']):status+=' Historical exact-OR replay remains CS8604 compile-limited.'
  if any(v['mutant_id'] in ('4899','4900','5532') for v in row['variants']):status+=' Existing-test/historical-Survived conflict pending actual mutant execution; no runner discrepancy inferred.'
  out.append('| '+code(row['file']+':'+str(row['line']))+' | '+'; '.join(vv)+' | '+', '.join(code(x) for x in ids)+(' '+', '.join(proof) if proof else '')+' | '+ordinary+' | '+status+' |')
 out+=['','## Exact case identifiers','', '| ID | Normalized discovered and executed name | Result |','| --- | --- | --- |']
 used={c['id']:c for r in data['rows'] for v in r['variants'] for c in v['exact_cases']}
 for cid,c in sorted(used.items()):out.append('| '+cid+' | '+code(c['name'])+' | '+c['result']+' |')
 out+=['','## Proofs, limitations and parent closure','',
 'P1-P5 are proof arguments accepted for their actual callable domains by the [current independent plan check]('+CHECK+') under the child decision\'s Q3 condition. Their premises and conclusions appear in [proofs.md](proofs.md). They are not observed mutant kills, exclusions or denominator changes. P4 concerns UTF-8 preamble emission; 5532 concerns invalid-byte fallback. The selected runtime must match P4\'s pinned 10.0.13 source premise.','',
 'Variant 2657 retains its historical CS8604 compilation limitation for the exact OR replacement. A passing ordinary W1 regression does not complete an exact mutant comparison. Parent closure also retains 4585 and 5014 as historical compilation-limited comparisons until later evidence or an explicit decision settles them. No nullable suppression, mutant repair, relaxed warning setting or fresh runner-discrepancy conclusion is introduced.','',
 '[Child A\'s receipt](../child-a/receipt.md) records its ordinary completion separately from mutation evidence still required at closure. Statistics:43 is shared with disjoint variant ownership: A retains 5146; B owns transferred 5147 and Statistics:174/5315. The combined parent recount is deferred to Q5 closure; this B receipt does not perform or claim that recount. OutcomeClassifier:38/5025\'s existing proof remains outside this assignment.','',
 'Parent closure status: `'+PENDING+'`. Owner: the #93 closure run under #89, per the [child decision]('+DECISION+'). No #93 completion, live A01 authorization or verdict is produced by B ordinary acceptance.','',
 '## Evidence provenance','',
 'Program-written evidence remains under raw/. The sibling [raw-manifest.json](raw-manifest.json) records each retained raw file\'s path, exact producer/source commit, byte size and SHA-256. Authored receipt, proofs and review judgements remain outside raw/. Source directories and copy commands are retained rather than substituting a predetermined temporary path.','']
 return '\n'.join(out)
def main():
 p=argparse.ArgumentParser();p.add_argument('--repo',type=pathlib.Path,default=pathlib.Path('/workspace/eventhubs-kafka-dotnet'));p.add_argument('--output',type=pathlib.Path);p.add_argument('--mapping-output',type=pathlib.Path,default=pathlib.Path('/tmp/issue-100/assignment-mapping.json'));p.add_argument('--template-only',action='store_true');a=p.parse_args()
 if a.template_only:
  data=inventory(a.repo);a.mapping_output.write_text(json.dumps(data,indent=2)+'\n');return
 if a.output is None:p.error('--output is required to write a receipt')
 reconciliation=load(a.repo/'docs/operations/evidence/issue-93/child-b/raw/case-reconciliation.json')
 data=inventory(a.repo,reconciliation['cases']);a.mapping_output.write_text(json.dumps(data,indent=2)+'\n');a.output.write_text(render(a.repo,data,reconciliation))
if __name__=='__main__':main()
```

### docs-pass.py

Original path: `/tmp/issue-100/docs-pass.py`. SHA-256: `25c4bf8ecf948e1825c581a68877d453eba0ff37d4d513850f278890a4f1ad6a`.

```python
import datetime,json,pathlib,subprocess,sys,time
root=pathlib.Path('/workspace/eventhubs-kafka-dotnet')
raw=root/'docs/operations/evidence/issue-93/child-b/raw'
ordinal=int(sys.argv[1]);assert ordinal in (1,2)
logpath=raw/f'docs-{ordinal}.log'
script="""set -euo pipefail
npm ci --ignore-scripts
npm run test:docs
npm run check:docs
git diff --check
git diff --numstat origin/main...HEAD -- . ':(exclude)docs/operations/evidence/**/raw/**'
"""
command=['timeout','--kill-after=5s','300s','bash']
receipt={'invocation_ordinal':ordinal,'command':command,'stdin':script,'source_commit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'started_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'wall_time_cap_seconds':300,'producer':"timeout --kill-after=5s 300s bash > "+str(logpath)+" 2>&1 <<'DOCS'\n"+script+'DOCS; receipt: python3 /tmp/issue-100/docs-pass.py '+str(ordinal)}
started=time.monotonic()
with logpath.open('x') as log:
    result=subprocess.run(command,input=script,text=True,stdout=log,stderr=subprocess.STDOUT,cwd=root)
receipt.update(exit_code=result.returncode,elapsed_seconds=time.monotonic()-started,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
(raw/f'docs-{ordinal}.json').write_text(json.dumps(receipt,indent=2)+'\n')
print(json.dumps(receipt))
sys.exit(result.returncode)
```

### manifest.py

Original path: `/tmp/issue-100/manifest.py`. SHA-256: `e049160fe65cfb48e27fda9616aa9dab92c209a273af0f6e68ea8cdf93282d86`.

```python
import hashlib,json,pathlib,subprocess,shlex
root=pathlib.Path('/workspace/eventhubs-kafka-dotnet')
evidence=root/'docs/operations/evidence/issue-93/child-b'
raw=evidence/'raw'
tested=json.loads((raw/'setup-1/setup.json').read_text())['source_commit']
proofs={r['path']:r['producer'] for r in json.loads(pathlib.Path('/tmp/issue-100/proof-source-producers.json').read_text())['files']}
records=[]
for path in sorted(raw.rglob('*')):
    if not path.is_file():continue
    relative=str(path.relative_to(root)); parts=path.relative_to(raw).parts
    if relative in proofs:
        producer=proofs[relative]+'; repository source commit '+tested
    elif parts[0]=='setup-1':
        setup=json.loads((raw/'setup-1/setup.json').read_text())
        producer=shlex.join(setup['command'])+'; stdin: approved #100 Revision 2 SDK provisioning inline producer at https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301492532 (updated 2026-10-20T21:19:10Z); source commit '+setup['source_commit']
    elif parts[0].startswith(('suite-','focused-','discovery-')):
        invocation=parts[0]; controller=json.loads((raw/invocation/'controller.json').read_text())
        command=shlex.join(controller['command'])
        if 'verification' in parts or parts[-1]=='retention.json':
            retention=json.loads((raw/invocation/'retention.json').read_text())
            command+='; original producer directory '+retention['source_directory']+'; '+retention['copy_command']
            for compression in retention.get('compressions',[]):
                if pathlib.Path(compression['retained_path']).resolve()==path.resolve():
                    command+='; '+compression['command']
        elif parts[-1].startswith('validation.'):
            validation=json.loads((raw/invocation/'validation.json').read_text())
            command=shlex.join(validation['command'])+'; validation receipt: python3 - (subprocess.run return code and monotonic elapsed time)'
        producer=command+'; enclosing controller command/limits: '+str((raw/invocation/'controller.json').relative_to(root))+'; source commit '+controller['tested_commit']
    elif parts[0].startswith('docs-'):
        receipt=json.loads((raw/(path.stem.split('.')[0]+'.json')).read_text()) if path.suffix=='.log' else json.loads(path.read_text())
        producer=receipt['producer']+'; source commit '+receipt['source_commit']
    elif path.name=='case-reconciliation.json':
        producer='python3 /tmp/issue-100/reconcile.py; fixed 453 baseline XML identities plus 24 literal additions; XML parse and single-pass re.sub normalization, discovery/focused equality and outcomes; source commit '+tested
    elif path.name=='controller-invocations.json':
        producer="python3 -; shutil.copy2('.artifacts/issue-100/controller-invocations.json', 'docs/operations/evidence/issue-93/child-b/raw/controller-invocations.json'); source commit "+tested
    elif path.name=='diff-accounting.json':
        receipt=json.loads(path.read_text());producer=receipt['producer']+'; source commit '+receipt['source_commit']
    elif path.name=='investigation-accounting.json':
        receipt=json.loads(path.read_text());producer=receipt['producer']+'; source commit '+receipt['source_commit']
    else:
        raise ValueError('Missing exact producer: '+relative)
    records.append({'path':relative,'producer':producer,'size_bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
(evidence/'raw-manifest.json').write_text(json.dumps({'files':records},indent=2)+'\n')
print('Manifest files:',len(records),'bytes:',sum(r['size_bytes'] for r in records))
```

### post-suite.sh

Original path: `/tmp/issue-100/post-suite.sh`. SHA-256: `ce55a6c8b5398ac20ad32602b9ce924b5e061d6b73ab69b621ea9fb12970c7b5`.

```bash
set -euo pipefail
export DOTNET_ROOT=/workspace/.tools/issue-100-dotnet-10.0.402
export PATH="$DOTNET_ROOT:$PATH"
repository_root=$(git rev-parse --show-toplevel)
raw="$repository_root/docs/operations/evidence/issue-93/child-b/raw"
ledger="$repository_root/.artifacts/issue-100/controller-invocations.json"
controller="$repository_root/docs/operations/evidence/issue-92-pr6b/controller.py"
python3 - "$raw" <<'PY'
import json,pathlib
raw=pathlib.Path(__import__('sys').argv[1])
c=json.loads((raw/'suite-1/controller.json').read_text())
assert c['exit_code']==0 and c['termination'] is None and c['source_inputs_unchanged'] and c['execution_evidence_complete']
PY
python3 "$controller" --mode discovery \
  --repo-root "$repository_root" --cwd "$repository_root" \
  --run-dir "$raw/discovery-1" --ledger-path "$ledger" \
  --tested-commit HEAD --seconds 300 \
  -- dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj \
  --configuration Release --no-build --list-tests
python3 "$controller" --mode baseline \
  --repo-root "$repository_root" --cwd "$repository_root" \
  --run-dir "$raw/focused-1" --ledger-path "$ledger" \
  --tested-commit HEAD --seconds 300 --xml "$raw/focused-1/results.xml" \
  -- dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj \
  --configuration Release --no-build --minimum-expected-tests 477 \
  --results-directory "$raw/focused-1" -- \
  --filter-method \
  'Parchwell.Verifier.Tests.Judgement.ConfigurationFidelityTests.*' \
  'Parchwell.Verifier.Tests.Judgement.LateWriteChecksTests.*' \
  'Parchwell.Verifier.Tests.Judgement.RunJudgementTests.*' \
  'Parchwell.Verifier.Tests.Judgement.StatisticsChecksTests.*' \
  'Parchwell.Verifier.Tests.A01.A01EvidenceRecorderTests.*' \
  'Parchwell.Verifier.Tests.Window.MessageIdentityReaderTests.*' \
  'Parchwell.Verifier.Tests.Window.RunWindowTests.*' \
  'Parchwell.Verifier.Tests.Window.WindowConsumerTests.*' \
  --report-xunit-xml --report-xunit-xml-filename results.xml
python3 - "$raw" <<'PY'
import datetime,json,pathlib,subprocess,time,sys
raw=pathlib.Path(sys.argv[1]); started=time.monotonic()
command=['timeout','--kill-after=5s','120s','dotnet','msbuild','scripts/dotnet-verification.proj','-t:ValidateReport',f'-p:Report={raw}/focused-1/results.xml','-p:ExpectedAssembly=Parchwell.Verifier.Tests','-nologo']
receipt={'command':command,'source_commit':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'started_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'wall_time_cap_seconds':120}
with (raw/'focused-1/validation.log').open('w') as log:
    result=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT)
receipt.update(exit_code=result.returncode,elapsed_seconds=time.monotonic()-started,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
(raw/'focused-1/validation.json').write_text(json.dumps(receipt,indent=2)+'\n')
sys.exit(result.returncode)
PY
python3 /tmp/issue-100/reconcile.py
```

### reconcile.py

Original path: `/tmp/issue-100/reconcile.py`. SHA-256: `b7bff50c5317b0535a1bafff371036dc395b560663f7b542cae3da8124b28882`.

```python
import collections
import gzip
import hashlib
import json
import pathlib
import re
import subprocess
import xml.etree.ElementTree as ET

root = pathlib.Path('/workspace/eventhubs-kafka-dotnet')
raw = root / 'docs/operations/evidence/issue-93/child-b/raw'
classes = ['Judgement.ConfigurationFidelityTests', 'Judgement.LateWriteChecksTests',
    'Judgement.RunJudgementTests', 'Judgement.StatisticsChecksTests',
    'A01.A01EvidenceRecorderTests', 'Window.MessageIdentityReaderTests',
    'Window.RunWindowTests', 'Window.WindowConsumerTests']
prefixes = tuple('Parchwell.Verifier.Tests.' + c + '.' for c in classes)
def normalize(name):
    return re.sub(r'\\([\\"])', r'\1', name)
def selected(name):
    return name.startswith(prefixes)
baseline_path = root / 'docs/operations/evidence/issue-93/child-a/raw/suite-4/verification/Parchwell.Verifier.Tests/results.xml'
baseline = [normalize(t.attrib['name']) for t in ET.parse(baseline_path).iter('test') if selected(t.attrib['name'])]
assert len(baseline) == len(set(baseline)) == 453
additions = []
def add(cls, method, arguments=None):
    name = 'Parchwell.Verifier.Tests.' + cls + '.' + method
    additions.append(name if arguments is None else name + '(' + arguments + ')')
for count in (1, 2, 3):
    add(classes[0], 'Given_SettingDifferencesAtDiagnosticCap_When_RunJudgementRetains_Then_OrdinalOrderSurvives', f'differenceCount: {count}')
for value in ('True', 'False'):
    add(classes[2], 'Given_SyntheticNoncanonicalWindowAssessment_When_Judging_Then_CurrentStatusPredicateIsPreserved', f'failedWithoutClassification: {value}')
for field in ('name','nodeid','source','state','req_timeouts','txretries','txerrs','rxerrs','disconnects','throttle.cnt','throttle.max','rtt.cnt','rtt.p99'):
    add(classes[3], 'Given_SyntheticRequiredBrokerFieldMissingWithoutParserProblem_When_Evaluated_Then_Check21EmitsExactFallbackDiagnostic', 'field: "' + field + '"')
for cls, method in [
    (classes[3], 'Given_SyntheticBrokerAdditionAtBoundary_When_Evaluated_Then_Check26EmitsExactDiagnosticTuple'),
    (classes[4], 'Given_FailedLedgerWithRetainedValue_When_Recorded_Then_DeliveryCodesRemainUnavailable'),
    (classes[6], 'Given_SuppliedEndHighs_When_Creating_Then_RetainsIndependentSnapshot'),
    (classes[6], 'Given_EmptyProblemFailure_When_Creating_Then_FallbackCheckSevenFails'),
    (classes[7], 'Given_PreCancelledWork_When_Draining_Then_NoNativeOperationStarts'),
    (classes[7], 'Given_WorkCancelledDuringAssignment_When_Draining_Then_NoPositionOrPollStarts')]:
    add(cls, method)
assert len(additions) == len(set(additions)) == 24
assert not (set(baseline) & set(additions))
expected = set(baseline) | set(additions)
discovery = [line.strip() for line in gzip.decompress((raw / 'discovery-1/command.log.gz').read_bytes()).decode().splitlines() if selected(line.strip())]
focused_root = ET.parse(raw / 'focused-1/results.xml').getroot()
tests = list(focused_root.iter('test'))
executed = [normalize(t.attrib['name']) for t in tests]
summary = {
    'source_commit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(),
    'normalization': "XML parse then re.sub(r'\\\\([\\\\\"])', r'\\1', name) once; no whitespace or case normalization",
    'baseline_path': str(baseline_path.relative_to(root)),
    'baseline_sha256': hashlib.sha256(baseline_path.read_bytes()).hexdigest(),
    'baseline_count': len(baseline), 'added_count': len(additions), 'expected_count': len(expected),
    'baseline_names': sorted(baseline), 'literal_added_names': sorted(additions),
    'discovered_count': len(discovery), 'executed_count': len(executed),
    'discovery_missing': sorted(expected-set(discovery)), 'discovery_extra': sorted(set(discovery)-expected),
    'execution_missing': sorted(expected-set(executed)), 'execution_extra': sorted(set(executed)-expected),
    'discovery_duplicates': sorted(n for n,c in collections.Counter(discovery).items() if c>1),
    'execution_duplicates': sorted(n for n,c in collections.Counter(executed).items() if c>1),
    'results': dict(collections.Counter(t.attrib['result'] for t in tests)),
    'assembly_outcomes': [a.attrib for a in focused_root.iter('assembly')],
    'class_counts': {c: sum(n.startswith('Parchwell.Verifier.Tests.'+c+'.') for n in executed) for c in classes},
    'cases': [{'id': f'B{i:04d}', 'name': n, 'result': 'Pass'} for i,n in enumerate(sorted(executed),1)],
}
summary['accepted'] = (
    len(discovery)==len(set(discovery))==len(executed)==len(set(executed))==477
    and set(discovery)==set(executed)==expected
    and summary['results']=={'Pass':477}
    and all(a.attrib.get(k,'0')=='0' for a in focused_root.iter('assembly') for k in ('errors','failed','skipped','not-run'))
    and not list(focused_root.iter('error')))
(raw/'case-reconciliation.json').write_text(json.dumps(summary,indent=2)+'\n')
assert summary['accepted'], 'Unexpected inventory/outcome; retain evidence and report without altering tests/filters.'
print(json.dumps({k:summary[k] for k in ('accepted','baseline_count','added_count','expected_count','discovered_count','executed_count','class_counts','results')}))
```

### refresh_authority.py

Original path: `/tmp/issue-100/refresh_authority.py`. SHA-256: `6e0add91f8b8f8f56694f75ddcdd7eeb2f8de8a4ef1e15197f183cf2b983f337`.

```python
#!/usr/bin/env python3
import json, re, subprocess, pathlib, datetime, hashlib
ROOT=pathlib.Path('/tmp/issue-100')
REPO='collaborationwithothers/eventhubs-kafka-dotnet'
def api(path, missing_ok=False):
    response=subprocess.run(['gh','api',path],text=True,capture_output=True)
    if response.returncode:
        if missing_ok and 'HTTP 404' in response.stderr: return None
        raise RuntimeError(f'{path}: {response.stderr}')
    return json.loads(response.stdout)
def dump(path,obj):
    path.write_text(json.dumps(obj,indent=2)+'\n')
issues={}; comments={}; queue=[]
for number in (100,93,97):
    issue=api(f'repos/{REPO}/issues/{number}')
    all_comments=[]; page=1
    while True:
        batch=api(f'repos/{REPO}/issues/{number}/comments?per_page=100&page={page}')
        all_comments.extend(batch)
        if len(batch)<100: break
        page+=1
    target=ROOT/f'issue-{number}'; target.mkdir(exist_ok=True)
    dump(target/'issue.json',issue); dump(target/'comments.json',all_comments)
    (target/'body.md').write_text(issue['body'] or '')
    issues[str(number)]={'updated_at':issue['updated_at'],'comments':{str(c['id']):c['updated_at'] for c in all_comments}}
    active_files={f"comment-{c['id']}.md" for c in all_comments}
    for stale in target.glob('comment-*.md'):
        if stale.name not in active_files: stale.unlink()
    if number == 100:
        dump(ROOT/'issue.json',issue); dump(ROOT/'comments.json',all_comments)
    for c in all_comments:
        comments[c['id']]=c
        (target/f"comment-{c['id']}.md").write_text(c['body'] or '')
    queue.extend([issue['body'] or '']+[c['body'] or '' for c in all_comments])
seen=set(); linked={}; linked_dir=ROOT/'linked-comments'; linked_dir.mkdir(exist_ok=True)
while queue:
    body=queue.pop(0)
    for cid in re.findall(r'https://github.com/'+re.escape(REPO)+r'/(?:issues|pull)/[0-9]+#issuecomment-([0-9]+)',body):
        cid=int(cid)
        if cid in seen: continue
        seen.add(cid)
        c=comments.get(cid) or api(f'repos/{REPO}/issues/comments/{cid}',missing_ok=True)
        if c is None:
            for suffix in ('.json','.md'):
                stale=linked_dir/f'{cid}{suffix}'
                if stale.exists(): stale.unlink()
            continue
        text=c['body'] or ''
        # Fetch every linked comment for provenance; recursively follow recognized decisions/approvals.
        dump(linked_dir/f'{cid}.json',c)
        (linked_dir/f'{cid}.md').write_text(text)
        linked[str(cid)]={'updated_at':c['updated_at'],'html_url':c['html_url'],'recognized_marker':bool(re.match(r'\s*## (?:Decision|Approval)',text))}
        if linked[str(cid)]['recognized_marker']: queue.append(text)
for stale in linked_dir.iterdir():
    if stale.stem.isdigit() and int(stale.stem) not in seen: stale.unlink()
events=[]; page=1
while True:
    batch=api(f'repos/{REPO}/issues/93/events?per_page=100&page={page}')
    events.extend(batch)
    if len(batch)<100: break
    page+=1
dump(ROOT/'issue-93/events.json',events)
pr=api(f'repos/{REPO}/pulls/98'); dump(ROOT/'predecessor-98.json',pr)
snapshot={'read_at':datetime.datetime.now(datetime.timezone.utc).isoformat(),'issues':issues,'linked_comments':linked,'predecessor_98':{'merged':pr['merged'],'merged_at':pr['merged_at'],'merge_commit_sha':pr['merge_commit_sha']}}
old=ROOT/'authority-snapshot.json'
if old.exists():
    previous=json.loads(old.read_text()); changes=[]
    for n,new in issues.items():
        prior=previous.get('issues',{}).get(n,{})
        if prior.get('updated_at')!=new['updated_at']: changes.append(f'issue {n}: updated_at changed')
        for cid in set(prior.get('comments',{}))|set(new['comments']):
            if prior.get('comments',{}).get(cid)!=new['comments'].get(cid): changes.append(f'issue {n}: comment {cid} added/edited/deleted')
    for cid in set(previous.get('linked_comments',{}))|set(linked):
        if previous.get('linked_comments',{}).get(cid)!=linked.get(cid): changes.append(f'linked comment {cid}: changed')
    dump(ROOT/'authority-changes.json',changes)
dump(old,snapshot)
print(json.dumps({'issues':{n:len(x['comments']) for n,x in issues.items()},'linked_comments':len(linked),'predecessor':snapshot['predecessor_98']}))
```

### sdk-setup.sh

Original path: `/tmp/issue-100/sdk-setup.sh`. SHA-256: `6d080041abe2332086e347fbf759f1561ddebd92b1286013113dba6b606e3af3`.

```bash
repository_root=/workspace/eventhubs-kafka-dotnet
raw="$repository_root/docs/operations/evidence/issue-93/child-b/raw"
set -euo pipefail
sdk_root="$repository_root/../.tools/issue-100-dotnet-10.0.402"
mkdir "$raw/setup-1"
timeout --kill-after=5s 600s python3 - "$repository_root" "$raw/setup-1" "$sdk_root" \
  > "$raw/setup-1/command.log" 2>&1 <<'SDK_SETUP'
import hashlib, json, os, pathlib, platform, shutil, subprocess, sys, tarfile, time, urllib.request
from datetime import datetime, timezone
root, output, install = (pathlib.Path(p).resolve() for p in sys.argv[1:])
started = time.monotonic()
receipt = {
    "invocation_ordinal": 1, "wall_time_cap_seconds": 600,
    "command": ["timeout", "--kill-after=5s", "600s", "python3", "-",
                str(root), str(output), str(install)],
    "stdin_plan": "#100 Verifier coverage B implementation-grade plan Revision 2, SDK provisioning",
    "decision_url": "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641",
    "cwd": str(root), "source_commit": subprocess.check_output(
        ["git", "rev-parse", "HEAD"], cwd=root, text=True).strip(),
    "started_utc": datetime.now(timezone.utc).isoformat(),
    "status": "running", "exit_code": None
}
def save():
    (output / "setup.json").write_text(json.dumps(receipt, indent=2) + "\n")
save()
try:
    if platform.system() != "Linux" or platform.machine() not in ("x86_64", "AMD64"):
        raise RuntimeError("The approved SDK archive requires Linux x64.")
    expected_url = "https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.402/dotnet-sdk-10.0.402-linux-x64.tar.gz"
    expected_hash = "1904a98dda2db34ba78ba3c4fb1b224455329af91c341d83547843c1839e112b04ae699ee92fa2449a8f62249196a938723078c0c60b9f65c88539d5687729c2"
    metadata_url = "https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json"
    metadata = urllib.request.urlopen(metadata_url, timeout=60).read()
    (output / "release-metadata.json").write_bytes(metadata)
    matches = {(f["url"], f["hash"].lower())
        for r in json.loads(metadata)["releases"] for s in r.get("sdks", [])
        if s["version"] == "10.0.402" for f in s["files"]
        if f.get("rid") == "linux-x64" and f.get("name") == "dotnet-sdk-linux-x64.tar.gz"}
    if matches != {(expected_url, expected_hash)}:
        raise RuntimeError("Official SDK selection differs from the approved URL/hash.")
    if install == root or root in install.parents:
        raise RuntimeError("SDK destination must be outside the repository.")
    install.mkdir(parents=True, exist_ok=False)
    archive = install.parent / "issue-100-sdk-10.0.402.tar.gz"
    digest = hashlib.sha512()
    with urllib.request.urlopen(expected_url, timeout=60) as source, archive.open("xb") as destination:
        while block := source.read(1024 * 1024):
            digest.update(block)
            destination.write(block)
    actual_hash = digest.hexdigest()
    if actual_hash != expected_hash:
        raise RuntimeError("Downloaded SDK SHA-512 does not match official metadata.")
    with tarfile.open(archive, "r:gz") as bundle:
        bundle.extractall(install, filter="data")
    selection = {"sdk_version": "10.0.402", "rid": "linux-x64",
        "url": expected_url, "official_sha512": expected_hash,
        "actual_sha512": actual_hash, "archive_size_bytes": archive.stat().st_size,
        "install_directory": str(install), "metadata_url": metadata_url,
        "metadata_sha256": hashlib.sha256(metadata).hexdigest()}
    (output / "sdk-selection.json").write_text(json.dumps(selection, indent=2) + "\n")
    archive.unlink()
    environment = os.environ.copy()
    environment.update({"DOTNET_ROOT": str(install), "DOTNET_NOLOGO": "1",
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
        "PATH": str(install) + os.pathsep + environment["PATH"]})
    dotnet = str(install / "dotnet")
    def capture(arguments, name):
        result = subprocess.run(arguments, cwd=root, env=environment, text=True,
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, check=False)
        (output / name).write_text(result.stdout)
        print(result.stdout, end="", flush=True)
        if result.returncode:
            raise RuntimeError(f"Setup diagnostic exited {result.returncode}: {arguments}")
        return result.stdout
    if capture([dotnet, "--version"], "sdk-version.txt").strip() != "10.0.402":
        raise RuntimeError("Selected SDK differs from 10.0.402.")
    capture([dotnet, "--info"], "dotnet-info.txt")
    runtimes = capture([dotnet, "--list-runtimes"], "runtimes.txt")
    if not any(line.startswith("Microsoft.NETCore.App 10.0.13 [") for line in runtimes.splitlines()):
        raise RuntimeError("Runtime differs from P4's pinned implementation.")
    sources = capture([dotnet, "nuget", "list", "source", "--format", "Short"], "nuget-sources.txt")
    if [line.strip() for line in sources.splitlines() if line.strip()] != ["E https://api.nuget.org/v3/index.json"]:
        raise RuntimeError("Effective NuGet sources differ from the approved nuget.org-only input.")
    for name in ("bash", "git", "node", "npm", "timeout", "docker"):
        if shutil.which(name, path=environment["PATH"]) is None:
            raise RuntimeError(f"Missing execution prerequisite: {name}")
    node = capture(["node", "--version"], "node-version.txt").strip()
    if int(node.removeprefix("v").split(".")[0]) < 24:
        raise RuntimeError("Node version is below 24.")
    capture(["docker", "version", "--format", "{{.Server.Os}} {{.Server.Version}}"], "docker-version.txt")
    receipt.update(status="passed", exit_code=0)
except BaseException as error:
    receipt.update(status="failed", exit_code=1,
        error=f"{type(error).__name__}: {error}")
    raise
finally:
    receipt.update(elapsed_seconds=time.monotonic() - started,
        finished_utc=datetime.now(timezone.utc).isoformat())
    save()
SDK_SETUP
export DOTNET_ROOT="$sdk_root"
export PATH="$DOTNET_ROOT:$PATH"
```

### supplement.py

Original path: `/tmp/issue-100/supplement.py`. SHA-256: `78ea6bf42bb1b34eb5f9eb78f63f25ed3bfca455877592ef83377e41ae3ab516`.

```python
import datetime,gzip,hashlib,json,pathlib,subprocess,xml.etree.ElementTree as ET
root=pathlib.Path('/workspace/eventhubs-kafka-dotnet');evidence=root/'docs/operations/evidence/issue-93/child-b';raw=evidence/'raw'
receipt=evidence/'receipt.md'
text=receipt.read_text().split('\n## Additional execution details\n')[0]
parts=['','## Additional execution details','',
 'The original report SHA-256 is `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492`, with tested production source `a2001534d94cd1135b9af53dc0e9d9816b00ce3d`, as recorded in the [frozen inventory](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/lines.json). No production source moved or changed from the merged predecessor, so no source remap applies. The eight selected classes retain the entire baseline identity set; only the literal additions are new.', '',
 'Case reconciliation parses XML, then decodes only escaped backslash/backslash and escaped backslash/double-quote once with `re.sub(r\'\\\\([\\\\"])\', r\'\\1\', name)`. It preserves whitespace and case. Discovery and focused XML each match the measured baseline plus the literal additions; their exact sets, duplicate checks and per-case outcomes are retained in [case-reconciliation.json](raw/case-reconciliation.json).', '',
 '| Full ordinary project | Passed | Failed | Skipped | Not run | Errors |', '| --- | --- | --- | --- | --- | --- |']
for path in sorted((raw/'suite-1/verification').glob('*/results.xml')):
    a=next(ET.parse(path).iter('assembly')).attrib
    parts.append('| '+path.parent.name+' | '+' | '.join(a.get(k,'0') for k in ('passed','failed','skipped','not-run','errors'))+' |')
parts.extend(['','The unchanged scaffold selector reported `Parchwell.Enrichment.Tests` as an empty scaffold; no test coverage is claimed for it. The suite passed locked restore, Release build, Kafka XML consistency, report validation, and the valid/intentional-CS1030 warning control. The [build log](raw/suite-1/verification/build.log) records zero warnings and errors. Synthetic local Kafka/Toxiproxy component workloads remain harness evidence, not Event Hubs findings.','','| Invocation | Exit | Measured elapsed seconds | Evidence |','| --- | --- | --- | --- |'])
for name in ('setup-1','suite-1','discovery-1','focused-1'):
    p=raw/name/('setup.json' if name=='setup-1' else 'controller.json');v=json.loads(p.read_text())
    parts.append(f'| {name} | {v["exit_code"]} | {v["elapsed_seconds"]:.6f} | [receipt]({p.relative_to(evidence)}) |')
v=json.loads((raw/'focused-1/validation.json').read_text());parts.append(f'| focused XML validation 1 | {v["exit_code"]} | {v["elapsed_seconds"]:.6f} | [receipt](raw/focused-1/validation.json), [log](raw/focused-1/validation.log) |')
parts.extend(['','All invocations above exited successfully. Suite, discovery and focused execution report unchanged source inputs, no termination and complete execution evidence. There were no failed or incomplete B attempts and no source/test/configuration repairs after the successful suite. No second suite or focused attempt and no mutation invocation was consumed.','','Compressed logs below retain the exact command-output byte stream. The table measures both that decompressed stream and the stored gzip bytes. Copied spike evidence compression has original/stored hashes and exact commands in [retention.json](raw/suite-1/retention.json).','','| Controller log | Original SHA-256 | Stored SHA-256 |','| --- | --- | --- |'])
for name in ('suite-1','discovery-1','focused-1'):
    p=raw/name/'command.log.gz';b=p.read_bytes();original=gzip.decompress(b)
    parts.append(f'| [{name}]({p.relative_to(evidence)}) | `{hashlib.sha256(original).hexdigest()}` | `{hashlib.sha256(b).hexdigest()}` |')
account=raw/'investigation-accounting.json'
if not account.exists():
    start=datetime.datetime.fromisoformat('2026-10-21T02:03:26+00:00');end=datetime.datetime.now(datetime.timezone.utc)
    value={'started_utc':start.isoformat(),'finished_utc':end.isoformat(),'elapsed_seconds':(end-start).total_seconds(),'cap_seconds':14400,'source_commit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'basis':'Conservative full wall interval from the current Approval, before B implementation/source work, through completion of the fixed assignment/proof/evidence reconciliation; includes setup/suite/waits and does not deduct authority reads. No prior B implementation allowance renewed. Governance review work follows separately.','producer':'python3 /tmp/issue-100/supplement.py; datetime interval with conservative Approval start and actual receipt completion time'}
    assert value['elapsed_seconds']<value['cap_seconds'];account.write_text(json.dumps(value,indent=2)+'\n')
parts.extend(['','The [investigation accounting](raw/investigation-accounting.json) conservatively charges the entire wall interval from this session\'s current Approval through completion of the fixed assignment/proof/reconciliation work, including setup, execution and waiting. It does not deduct exempt authority reads or reset prior work. The measured interval remains within the approved shared allowance; later governance review does not renew that allowance.'])
docs=[p for p in (raw/'docs-1.json',raw/'docs-2.json') if p.exists()]
if docs:
    parts.extend(['','Documentation acceptance receipts retain the exact command, source commit, elapsed time and outcome. A successful scan is advisory and does not prove factual correctness.','','| Documentation pass | Exit | Measured elapsed seconds | Evidence |','| --- | --- | --- | --- |'])
    for p in docs:
        v=json.loads(p.read_text());stem=p.stem
        parts.append(f'| {v["invocation_ordinal"]} | {v["exit_code"]} | {v["elapsed_seconds"]:.6f} | [receipt](raw/{stem}.json), [log](raw/{stem}.log) |')
parts.extend(['', 'Original HTTP response-header bytes are gzip-retained to preserve their CRLF line endings. The [header compression record](raw/proof-sources/compression.json) lists original/stored hashes, byte lengths and producer commands; no source body was normalized.'])
receipt.write_text(text+'\n'+'\n'.join(parts)+'\n')
```

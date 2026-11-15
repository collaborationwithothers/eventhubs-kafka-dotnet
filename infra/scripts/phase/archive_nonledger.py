"""Inventory the existing batch modes without importing the archive transport.

Only fixed paths and validated publication references are used. Runner pod files
supply identities, never application configuration.
"""
import json
import os
import re

V1 = (('B01','C0'),('B01','C1'),('B03','C1'),('B03','C2'),('B03','C3'),('B03','C4'),
      ('B05','C1'),('C08','C0'),('C08','C1'),('C12','C1'),('C12','C2'),
      ('F04','C0'),('F04','C1'),('F04','C2'),('F04','C3'),('F04','C4'))
V2 = (('B08','C0'),('B08','C1'),('E01','C0'),('E01','C1'), *(("F06", f"C{i}") for i in range(12)))
IDLE = (('A02','C0'),('A02','C1'),('A03','C0'),('A03','C1'),('A03','C2'),('A04','C0'),('A04','C1'))
SEGMENT = re.compile(r'[A-Za-z0-9_-]{1,128}\Z')
HASH = re.compile(r'[0-9a-f]{64}\Z')


def _read(path, limit):
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, 'rb') as f:
        data = f.read(limit + 1)
    if len(data) > limit:
        raise ValueError('size')
    def unique(pairs):
        result = {}
        for k, v in pairs:
            if k in result: raise ValueError('duplicate')
            result[k] = v
        return result
    return json.loads(data, object_pairs_hook=unique, parse_constant=lambda _: (_ for _ in ()).throw(ValueError('number')))


def _stages(item, config, native):
    n = int(config[1:])
    if item == 'B08': stages = ['action', 'read', 'judge']
    elif item == 'E01': stages = ['seed', 'seedread', 'action', 'read', 'judge']
    elif n < 6: stages = ['action', 'read', 'judge']
    else: stages = (['seed'] if n < 9 else []) + ['action', 'judge']
    if native and (item, config) == ('F06', 'C6'):
        stages += ['nativejwt', 'nativeimds', 'nativeimdsnode']
    return stages


def collect(collector, phase, status, containers, artifacts):
    """Copy declared source objects, returning the frozen expected job inventory."""
    run, scenario, kind = phase['run_id'], phase['scenario'], phase['run_kind']
    if not SEGMENT.fullmatch(run) or kind not in ('smoke', 'batch') or not isinstance(status.get('modes'), list):
        raise ValueError('invalid archive selection')
    records = status['modes']
    units = []
    def unit(mode, item, config, repeat, job, stage=None):
        matches = [r for r in records if isinstance(r, dict) and r.get('mode') == mode and r.get('item') == item
                   and r.get('configuration') == config and r.get('repeat') == repeat
                   and (stage is None or r.get('stage') == stage)]
        if len(matches) > 1 or matches and matches[0].get('job') != job:
            raise ValueError('invalid job identity')
        record = matches[0] if matches else None
        if record is None: collector.gap('monitoring_incomplete', 'missing job status', unit=job)
        elif record.get('apply') is None:
            collector.gap('monitoring_incomplete', 'required job not started', unit=job)
        started = record is not None and record.get('apply') is not None
        u = {'repeat': repeat, 'item': item, 'mode': mode, 'job_name': job, 'pod_name': None,
             'pod_uid': None, 'instance_id': None, 'state': 'started' if started else 'not_started'}
        units.append(u)
        if started:
            try:
                with os.fdopen(os.open(artifacts / job / 'pods.json', os.O_RDONLY | os.O_NOFOLLOW), 'rb') as stream:
                    raw = stream.read(4 * 1048576 + 1)
                if len(raw) > 4 * 1048576: raise ValueError('pod size')
                projected = json.loads(collector.project_pods(raw))
                pods = projected['items']
                if len(pods) != 1: raise ValueError('pod count')
                pod, uid = pods[0]['name'], pods[0]['uid']
                if not SEGMENT.fullmatch(pod) or not SEGMENT.fullmatch(uid) or not pod.startswith(job + '-'):
                    raise ValueError('pod identity')
                u.update(pod_name=pod, pod_uid=uid, instance_id=pod)
            except (OSError, ValueError, KeyError, TypeError):
                collector.gap('monitoring_incomplete', 'job pod identity', unit=job)
        return u
    def source(u, container, name, typ, repeat=None, **kwargs):
        if u['state'] == 'started':
            return collector.source(container, name, typ, repeat=repeat, **kwargs)
        collector.not_started(container, name, typ, repeat=repeat)
    def judge(u, prefix, hashes=False):
        # Completion is the publication declaration; inspect before candidate transfer.
        declared = []
        def inspect(path, size):
            try:
                v = _read(path, 65536)
                expected = prefix + 'candidate.json'
                parts = prefix.rstrip('/').split('/')
                fields = {'schema_version','mode','run_id','scenario_id','configuration_id','source_commit','job_name','pod_name','pod_uid','candidate_name','handler_exit_code','cleanup_completed'}
                if hashes: fields.add('candidate_sha256')
                if (not isinstance(v, dict) or set(v) != fields or v['schema_version'] != (2 if hashes else 1)
                        or v['mode'] != ('single-action-v2-judge' if hashes else 'single-action-judge')
                        or v['run_id'] != run or v['scenario_id'] != parts[-2] or v['configuration_id'] != parts[-1]
                        or v['source_commit'] != phase['source_commit'] or v['job_name'] != u['job_name']
                        or v['pod_name'] != u['pod_name'] or v['pod_uid'] != u['pod_uid']
                        or type(v['handler_exit_code']) is not int or type(v['cleanup_completed']) is not bool
                        or v['candidate_name'] != expected): raise ValueError('candidate')
                digest = v.get('candidate_sha256')
                if hashes and (not isinstance(digest, str) or not HASH.fullmatch(digest)): raise ValueError('digest')
                declared.append(digest)
            except (OSError, ValueError, KeyError, TypeError, AttributeError):
                collector.gap('monitoring_incomplete', 'judge receipt contract', prefix + 'judge-completion.json')
        source(u, containers['verifier'], prefix + 'judge-completion.json', 'judge_completion', inspect_source=inspect)
        source(u, containers['verifier'], prefix + 'candidate.json', 'candidate',
               expected_sha256=declared[0] if declared and hashes else None)
    items = phase.get('items')
    allowed = {'probe-batch': tuple(dict.fromkeys(i for i, _ in V1)), 'probe-batch-2': ('B08','E01','F06'),
               'idle-observation': ('A02','A03','A04'), 'ledger-batch': ('B06','B07')}
    if scenario not in allowed or not isinstance(items, list) or not items or items != [i for i in allowed[scenario] if i in items]:
        raise ValueError('invalid selected items')
    if scenario == 'ledger-batch' and items != ['B06','B07']: raise ValueError('ledger selection')
    if scenario == 'probe-batch' and 'F04' in items:
        unit('single-action-seed', 'F04', 'C2', None, run + '-f04-seed')
    if scenario in ('probe-batch', 'idle-observation'):
        matrix = V1 if scenario == 'probe-batch' else IDLE
        for item, config in matrix:
            if item not in items: continue
            mode = 'single-action' if scenario == 'probe-batch' else 'idle-observation'
            suffix = 'single' if scenario == 'probe-batch' else 'idle'
            job = f'{run}-{item.lower()}-{config.lower()}-{suffix}'
            u = unit(mode, item, config, None, job)
            if scenario == 'probe-batch' and kind == 'smoke':
                unit('single-action-judge', item, config, None, job + '-judge')
                continue
            prefix = f'{run}/{mode}/{item}/{config}/'
            container = containers['single_action'] if scenario == 'probe-batch' else containers['ledger']
            pod = u['pod_name']
            if pod:
                for name, typ in ((f'registrations/{pod}.json','registration'),
                                  (f'instances/{pod}/capture.json','capture'), (f'instances/{pod}/completion.json','completion')):
                    source(u, container, prefix + name, typ)
            elif u['state'] == 'started':
                collector.gap('monitoring_incomplete', 'capture registration identity', unit=job)
            if scenario == 'probe-batch':
                ju = unit('single-action-judge', item, config, None, job + '-judge')
                judge(ju, prefix)
        if scenario == 'idle-observation':
            ju = unit('idle-observation-judge', None, None, None, run + '-idle-judge')
            root = f'{run}/idle-observation/'
            refs = {}
            def inspect(path, size):
                try:
                    v = _read(path, 65536)
                    fields = {'schema_version','mode','run_id','source_commit','job_name','pod_name','pod_uid','candidates','handler_exit_code','cleanup_completed'}
                    if (not isinstance(v, dict) or set(v) != fields or v['schema_version'] != 1
                            or v['mode'] != 'idle-observation-judge' or v['run_id'] != run
                            or v['source_commit'] != phase['source_commit'] or v['job_name'] != ju['job_name']
                            or v['pod_name'] != ju['pod_name'] or v['pod_uid'] != ju['pod_uid']
                            or type(v['handler_exit_code']) is not int or type(v['cleanup_completed']) is not bool): raise ValueError('identity')
                    entries = v['candidates']
                    if not isinstance(entries, list) or len(entries) != len(items): raise ValueError('entries')
                    for e in entries:
                        if not isinstance(e, dict) or set(e) != {'scenario_id','candidate_name','candidate_sha256','status','reason'}: raise ValueError('entry')
                        i, name, sha = e['scenario_id'], e['candidate_name'], e['candidate_sha256']
                        if i not in items or i in refs or name != root + i + '/judge/candidate.json' or not isinstance(sha,str) or not HASH.fullmatch(sha): raise ValueError('reference')
                        refs[i] = sha
                except (OSError, ValueError, KeyError, TypeError, AttributeError):
                    refs.clear()
                    collector.gap('monitoring_incomplete', 'idle judge receipt contract', root + 'judge-completion.json')
            source(ju, containers['verifier'], root + 'judge-completion.json', 'judge_completion', inspect_source=inspect)
            for item in items:
                source(ju, containers['verifier'], root + item + '/judge/candidate.json', 'candidate', expected_sha256=refs.get(item))
    elif scenario == 'probe-batch-2':
        matrix = V2
        selector = phase.get('probe_v2_selection', 'all')
        capture_auth = phase.get('capture_authentication_metadata', False)
        native = phase.get('test_native_authentication', False)
        refresh = phase.get('test_handler_refresh_path', False)
        if any(type(x) is not bool for x in (capture_auth, native, refresh)):
            raise ValueError('selection')
        if kind == 'batch' and (capture_auth or native or refresh or selector not in ('all', ','.join(items)) or selector == 'all' and items != list(allowed[scenario])):
            raise ValueError('selection')
        if kind == 'smoke' and not capture_auth and (selector != 'all' or items != list(allowed[scenario]) or native or refresh):
            raise ValueError('selection')
        if native and refresh: raise ValueError('selection')
        if capture_auth:
            if kind != 'smoke' or items != ['F06'] or phase.get('probe_v2_selection') != 'authentication-controls': raise ValueError('selection')
            matrix = (('F06','C6'), ('F06','C9'))
        for item, config in matrix:
            if item not in items: continue
            for stage in _stages(item, config, phase.get('test_native_authentication') is True):
                mode = 'single-action-v2-judge' if stage == 'judge' else 'single-action-v2'
                u = unit(mode, item, config, None, f'{run}-{item.lower()}-{config.lower()}-{stage}', stage)
                if stage == 'judge': judge(u, f'{run}/single-action-v2/{item}/{config}/', True)
    else:
        for item in items:
            child = run + '-' + item.lower()
            for config in ('C0','C1'):
                for repeat in range(1, 6 if kind == 'batch' else 2):
                    root = f'{child}/{config}/r{repeat}/'
                    for mode in ('start-line','loadgen','verify'):
                        job = f'{child}-{config.lower()}-r{repeat}-{mode}'
                        u = unit(mode, item, config, repeat, job)
                        if mode == 'start-line':
                            for leaf in ('start-line.json','broker-inventory.json','start-line-completion.json'):
                                source(u, containers['verifier'], root + leaf, 'start_line', repeat)
                        elif mode == 'verify':
                            for leaf in ('result.json','verify-completion.json'):
                                source(u, containers['verifier'], root + leaf, 'verify', repeat)
                            if u['state'] == 'started' and u['pod_uid']:
                                cid, prefix = collector.capture_name(child, config, mode, job, u['pod_uid'], 0, repeat)
                                identity = {'run_id':child, 'phase':config, 'scenario_id':item, 'configuration_id':config,
                                            'job_name':job, 'pod_uid':u['pod_uid'], 'source_commit':phase['source_commit']}
                                collector.capture(containers['verifier'], prefix, identity, mode, repeat, cid)
                            elif u['state'] == 'started': collector.gap('capture_missing', 'verify pod identity', unit=job)
                        elif u['state'] == 'started' and u['pod_name']:
                            collector.producer(containers['ledger'], child, config, repeat, job, u['pod_name'], u['pod_uid'], phase['source_commit'])
                            diagnostic = root + 'producer/' + u['pod_name'] + '/diagnostics/'
                            for leaf in ('ledger.jsonl','manifest.json','statistics.jsonl','request-lifecycle.jsonl','capabilities.json'):
                                collector.optional_source(containers['ledger'], diagnostic + leaf, 'producer_diagnostic', repeat=repeat)
                        elif u['state'] == 'started': collector.gap('monitoring_incomplete', 'producer registration identity', unit=job)
                if kind == 'batch':
                    job = f'{child}-{config.lower()}-judge'
                    u = unit('judge', item, config, None, job)
                    source(u, containers['verifier'], f'{child}/{config}/candidate.json', 'candidate')
                    source(u, containers['verifier'], f'{child}/{config}/judge-completion.json', 'judge_completion')
    return units

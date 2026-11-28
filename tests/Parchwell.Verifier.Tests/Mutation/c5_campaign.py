#!/usr/bin/env python3
"""Validate the two explicitly pinned C5 runs and their shard provenance."""
import argparse
import hashlib
from datetime import datetime, timezone
import json
from pathlib import Path
import re
import subprocess
import time

import c5_runtime
import c5_workflow

ROOT = Path(__file__).resolve().parents[3]
PATHS = {'coordinator': '.github/workflows/verifier-c5-mutation.yml',
         'companion': '.github/workflows/verifier-c5-mutation-companion.yml'}
BUDGET = 4200
# A run is re-run at most this many times before its evidence is declared incomplete.
MAX_ATTEMPTS = 5
# Reserve ten minutes of the hosted job limit to retain aggregate evidence.
POLL_SECONDS = 17_400


def require(condition, message):
    if not condition:
        raise ValueError(message)


def timestamp(value):
    require(isinstance(value, str), 'invalid timestamp')
    try:
        result = datetime.fromisoformat(value.replace('Z', '+00:00'))
        require(result.tzinfo is not None, 'timestamp lacks timezone')
        return result.astimezone(timezone.utc)
    except (ValueError, OverflowError) as error:
        raise ValueError('invalid timestamp') from error


def matrices():
    return {role: c5_runtime.workflow_matrix((ROOT / path).read_bytes())
            for role, path in PATHS.items()}


def validate_layout(layout):
    require(set(layout) == set(PATHS), 'invalid layout roles')
    a, b = layout['coordinator'], layout['companion']
    require(isinstance(a, dict) and isinstance(b, dict) and a and b,
            'empty or invalid matrix')
    require(len(a) <= c5_runtime.CAMPAIGN_LIMITS[PATHS['coordinator']]
            and len(b) <= c5_runtime.CAMPAIGN_LIMITS[PATHS['companion']] and not set(a) & set(b),
            'overlapping or oversized matrices')
    require({**a, **b} == c5_workflow.SHARDS, 'matrix does not cover C5 shards')


def validate_run(run, role, repository, commit, run_id, attempt):
    require(isinstance(run, dict), 'invalid run')
    require(run.get('id') == run_id and type(run.get('id')) is int
            and run.get('run_attempt') == attempt and type(run.get('run_attempt')) is int,
            'run id or attempt mismatch')
    require(run.get('path') == PATHS[role] and run.get('event') == 'workflow_dispatch'
            and run.get('head_branch') == 'main' and run.get('head_sha') == commit,
            'run workflow, event, branch or commit mismatch')
    require(isinstance(run.get('repository'), dict)
            and run['repository'].get('full_name') == repository
            and isinstance(run.get('head_repository'), dict)
            and run['head_repository'].get('full_name') == repository, 'run repository mismatch')
    return timestamp(run.get('created_at'))


def job_name(shard, target):
    name = f'mutation ({shard}, {target})'
    return name if len(name) <= 100 else name[:97] + '...'


def job_names(matrix):
    """Map each expected mutation job name to its shard."""
    expected = {}
    for shard, target in matrix.items():
        name = job_name(shard, target)
        require(name not in expected, 'ambiguous job names')
        expected[name] = shard
    return expected


def find_job(document, name):
    """Return the one job of that name, or None when the list has none or several."""
    found = [job for job in document['jobs'] if isinstance(job, dict) and job.get('name') == name]
    return found[0] if len(found) == 1 else None


def validate_jobs(document, run_id, attempt, commit, matrix, coordinator, *, terminal=False):
    require(isinstance(document, dict) and isinstance(document.get('jobs'), list)
            and type(document.get('total_count')) is int
            and document['total_count'] == len(document['jobs']), 'missing job pages')
    expected = job_names(matrix)
    names, ids = set(), set()
    for job in document['jobs']:
        require(isinstance(job, dict) and type(job.get('id')) is int
                and job['id'] not in ids, 'invalid or duplicate job id')
        ids.add(job['id'])
        require(job.get('run_id') == run_id and job.get('run_attempt') == attempt
                and job.get('head_sha') == commit, 'job identity mismatch')
        name = job.get('name')
        require(isinstance(name, str) and name not in names, 'duplicate job name')
        names.add(name)
        if coordinator and name == 'aggregate' and not terminal:
            require(job.get('status') == 'in_progress' and job.get('conclusion') is None,
                    'aggregate job is not running')
        else:
            require((name in expected or (terminal and coordinator and name == 'aggregate'))
                    and job.get('status') == 'completed'
                    and job.get('conclusion') == 'success', 'unexpected or unsuccessful job')
    require(names == set(expected) | ({'aggregate'} if coordinator else set()),
            'missing or foreign jobs')


def build_manifest(repository, commit, runs, layout, incomplete_reasons=None, origins=None, firsts=None):
    """Build the manifest: schema 1 when both runs are at attempt 1, otherwise schema 2.

    origins maps a shard to the attempt that ran it; firsts maps a role to its attempt 1 creation time.
    """
    validate_layout(layout)
    require(re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', repository) is not None
            and re.fullmatch(r'[0-9a-f]{40}', commit) is not None, 'invalid repository or commit')
    require(set(runs) == set(PATHS), 'invalid run roles')
    require(runs['coordinator']['id'] != runs['companion']['id'], 'run ids must differ')
    reasons = [] if incomplete_reasons is None else incomplete_reasons
    require(isinstance(reasons, list) and all(isinstance(reason, str) and reason for reason in reasons),
            'invalid incomplete reasons')
    for role in PATHS:
        run = runs[role]
        require(type(run['id']) is int and run['id'] > 0 and type(run['run_attempt']) is int
                and run['run_attempt'] > 0, 'invalid run id or attempt')
        validate_run(run, role, repository, commit, run['id'], run['run_attempt'])
    recovered = any(runs[role]['run_attempt'] > 1 for role in PATHS)
    result = {'schema': 2 if recovered else 1, 'repository': repository, 'commit': commit, 'runs': {},
              'shards': {}, 'incomplete_reasons': reasons}
    for role in PATHS:
        run = runs[role]
        run_id, attempt = run['id'], run['run_attempt']
        result['runs'][role] = {'id': run_id, 'attempt': attempt, 'path': PATHS[role],
                                'created_at': run['created_at']}
        if recovered:
            result['runs'][role]['first_created_at'] = (firsts or {}).get(role, run['created_at'])
        for shard, target in layout[role].items():
            origin = (origins or {}).get(shard, attempt) if recovered else attempt
            result['shards'][shard] = {'target': target, 'run_id': str(run_id), 'run_attempt': str(origin)}
    return result


def validate_manifest(manifest, expected_commit, coordinator_matrix, companion_matrix):
    """Validate a manifest and return its shard-to-origin mapping; raise on any mismatch."""
    require(isinstance(manifest, dict) and set(manifest) == {'schema', 'repository', 'commit', 'runs', 'shards', 'incomplete_reasons'}
            and type(manifest['schema']) is int and manifest['schema'] in (1, 2)
            and manifest['commit'] == expected_commit, 'invalid manifest')
    recovered = manifest['schema'] == 2
    require(isinstance(manifest['repository'], str)
            and re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', manifest['repository'])
            and isinstance(expected_commit, str) and re.fullmatch(r'[0-9a-f]{40}', expected_commit),
            'invalid manifest repository or commit')
    require(isinstance(manifest['incomplete_reasons'], list)
            and all(isinstance(reason, str) and reason for reason in manifest['incomplete_reasons']),
            'invalid incomplete reasons')
    layout = {'coordinator': coordinator_matrix, 'companion': companion_matrix}
    validate_layout(layout)
    runs = manifest['runs']
    require(isinstance(runs, dict) and set(runs) == set(PATHS), 'invalid manifest runs')
    origins = {}
    ids = set()
    for role, path in PATHS.items():
        run = runs[role]
        keys = {'id', 'attempt', 'path', 'created_at'} | ({'first_created_at'} if recovered else set())
        require(isinstance(run, dict) and set(run) == keys
                and type(run['id']) is int and run['id'] > 0
                and type(run['attempt']) is int and run['attempt'] > 0 and run['path'] == path,
                'invalid manifest run identity')
        created = timestamp(run['created_at'])
        require(not recovered or timestamp(run['first_created_at']) <= created, 'invalid manifest run identity')
        ids.add(run['id'])
        if recovered:
            continue
        origins.update({s: {'target': target, 'run_id': str(run['id']),
                            'run_attempt': str(run['attempt'])} for s, target in layout[role].items()})
    if recovered:
        require(any(runs[role]['attempt'] > 1 for role in PATHS), 'invalid manifest run identity')
        shards = manifest['shards']
        require(len(ids) == 2 and isinstance(shards, dict)
                and set(shards) == set(layout['coordinator']) | set(layout['companion']),
                'manifest shard provenance mismatch')
        for role in PATHS:
            for shard, target in layout[role].items():
                entry = shards[shard]
                require(isinstance(entry, dict) and set(entry) == {'target', 'run_id', 'run_attempt'}
                        and entry['target'] == target and entry['run_id'] == str(runs[role]['id'])
                        and isinstance(entry['run_attempt'], str)
                        and re.fullmatch(r'[1-9][0-9]*', entry['run_attempt']) is not None
                        and int(entry['run_attempt']) <= runs[role]['attempt'],
                        'manifest shard provenance mismatch')
                origins[shard] = dict(entry)
        return origins
    require(len(ids) == 2 and manifest['shards'] == origins, 'manifest shard provenance mismatch')
    return origins


def api(repository, suffix):
    output = subprocess.check_output(['gh', 'api', f'repos/{repository}/{suffix}'], text=True)
    return json.loads(output)


def jobs(repository, run_id, attempt):
    rows = []
    total = None
    page = 1
    while True:
        document = api(repository, f'actions/runs/{run_id}/attempts/{attempt}/jobs?per_page=30&page={page}')
        require(isinstance(document, dict) and type(document.get('total_count')) is int
                and isinstance(document.get('jobs'), list), 'malformed jobs response')
        require(total is None or total == document['total_count'], 'job count changed while paging')
        total = document['total_count']
        batch = document['jobs']
        rows.extend(batch)
        require(len(rows) <= total, 'extra job pages')
        if len(rows) == total:
            return {'total_count': total, 'jobs': rows}
        require(len(batch) == 30, 'missing job pages')
        page += 1


def shard_origins(repository, role, run_id, attempt, attempts, own_jobs, matrix, reasons):
    """Return each shard's origin attempt, proven by the job lists, and record unverified shards.

    attempts maps each attempt number 1..attempt to its run document. A shard's origin is the
    latest attempt that started at or before the shard job's start; that attempt's own job list
    must show the same job, with the same start, as a success.
    """
    result, lists = {}, {attempt: own_jobs}
    try:
        started = {k: timestamp(document.get('run_started_at')) for k, document in attempts.items()}
    except (AttributeError, ValueError):
        started = None
    for shard, target in matrix.items():
        name = job_name(shard, target)
        try:
            require(started is not None, 'missing attempt start')
            record = find_job(own_jobs, name)
            require(record is not None and record.get('conclusion') == 'success', 'unsuccessful job')
            begun = timestamp(record.get('started_at'))
            origin = max(k for k in attempts if started[k] <= begun)
            if origin != attempt:
                if origin not in lists:
                    try:
                        lists[origin] = jobs(repository, run_id, origin)
                    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError):
                        lists[origin] = None
                require(lists[origin] is not None, 'unreadable origin jobs')
                other = find_job(lists[origin], name)
                require(other is not None and other.get('run_id') == run_id
                        and other.get('run_attempt') == origin and other.get('conclusion') == 'success'
                        and timestamp(other.get('started_at')) == begun, 'origin job mismatch')
            result[shard] = origin
        except (ValueError, KeyError, TypeError):
            reasons.append(f'{shard}: unverified attempt origin')
            result[shard] = attempt
    return result


def prepare(repository, commit, coordinator_id, coordinator_attempt, companion_id, companion_attempt, output):
    deadline = time.monotonic() + POLL_SECONDS
    require(isinstance(repository, str) and re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', repository)
            and isinstance(commit, str) and re.fullmatch(r'[0-9a-f]{40}', commit),
            'invalid repository or commit')
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    require(head == commit, 'checked out source does not match campaign commit')
    for path in PATHS.values():
        committed = subprocess.check_output(['git', 'show', f'{commit}:{path}'], cwd=ROOT)
        require((ROOT / path).read_bytes() == committed, 'workflow matrix source differs from commit')
    layout = matrices()
    validate_layout(layout)
    require(coordinator_id != companion_id and min(coordinator_id, coordinator_attempt, companion_id, companion_attempt) > 0,
            'invalid pinned run identities')
    ids = {'coordinator': (coordinator_id, coordinator_attempt), 'companion': (companion_id, companion_attempt)}
    runs = {role: api(repository, f'actions/runs/{rid}/attempts/{attempt}')
            for role, (rid, attempt) in ids.items()}
    starts = [validate_run(runs[role], role, repository, commit, *ids[role]) for role in PATHS]
    reasons = []
    latest = api(repository, f'actions/runs/{companion_id}')
    require(isinstance(latest, dict) and type(latest.get('run_attempt')) is int and latest['run_attempt'] > 0,
            'invalid companion run')
    if latest['run_attempt'] < companion_attempt:
        reasons.append('companion attempt regressed')
    elif latest['run_attempt'] > companion_attempt:
        companion_attempt = latest['run_attempt']
        ids['companion'] = (companion_id, companion_attempt)
        runs['companion'] = api(repository, f'actions/runs/{companion_id}/attempts/{companion_attempt}')
        starts[1] = validate_run(runs['companion'], 'companion', repository, commit, companion_id, companion_attempt)
    while True:
        peer = runs['companion']
        if peer.get('status') == 'completed':
            if peer.get('conclusion') != 'success':
                reasons.append('companion run was not successful')
            break
        if peer.get('status') not in ('queued', 'in_progress', 'waiting', 'pending', 'requested'):
            reasons.append('companion run has an invalid status')
            break
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            reasons.append('companion polling timed out')
            break
        time.sleep(min(15, remaining))
        try:
            updated = api(repository, f'actions/runs/{companion_id}/attempts/{companion_attempt}')
            require(validate_run(updated, 'companion', repository, commit, companion_id, companion_attempt)
                    == starts[1], 'companion creation time changed')
        except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
            reasons.append(f'companion status: {error}')
            break
        runs['companion'] = updated
    if runs['coordinator'].get('status') != 'in_progress':
        reasons.append('coordinator run is not in progress')
    documents = {}
    for role, (rid, attempt) in ids.items():
        try:
            documents[role] = jobs(repository, rid, attempt)
            validate_jobs(documents[role], rid, attempt, commit, layout[role], role == 'coordinator')
        except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
            reasons.append(f'{role} jobs: {error}')
    origins, firsts = {}, {}
    for role, (rid, attempt) in ids.items():
        if attempt == 1:
            continue
        if attempt > MAX_ATTEMPTS:
            if 'too many run attempts' not in reasons:
                reasons.append('too many run attempts')
            continue
        attempts = {k: api(repository, f'actions/runs/{rid}/attempts/{k}') for k in range(1, attempt)}
        for k, document in attempts.items():
            validate_run(document, role, repository, commit, rid, k)
        attempts[attempt] = runs[role]
        firsts[role] = attempts[1]['created_at']
        if role in documents:
            origins.update(shard_origins(repository, role, rid, attempt, attempts, documents[role],
                                         layout[role], reasons))
    manifest = build_manifest(repository, commit, runs, layout, reasons, origins, firsts)
    validate_manifest(manifest, commit, layout['coordinator'], layout['companion'])
    with Path(output).open('x', encoding='utf-8') as stream:
        json.dump(manifest, stream, indent=2, sort_keys=True)
        stream.write('\n')


def terminal_report(repository, commit, manifest_bytes, summary_bytes, layout, runs, documents,
                    firsts=None, origin_documents=None):
    """Verify terminal runs and aggregate evidence; return a fail-closed performance report.

    For a schema 2 manifest, firsts maps a role to its attempt 1 run document and origin_documents maps
    a role to {attempt: job list} for each earlier origin attempt of its shards.
    """
    report = {'status': 'incomplete', 'reasons': [], 'budget_seconds': BUDGET,
              'elapsed_seconds': None, 'runs': {}}
    try:
        manifest = c5_runtime.strict_json(manifest_bytes)
        summary = c5_runtime.strict_json(summary_bytes)
        origins = validate_manifest(manifest, commit, layout['coordinator'], layout['companion'])
        recovered = manifest['schema'] == 2
        require(manifest['repository'] == repository, 'manifest repository mismatch')
        require(not manifest['incomplete_reasons'], 'manifest records incomplete evidence')
        require(isinstance(summary, dict) and summary.get('commit') == commit
                and summary.get('run_id') == str(manifest['runs']['coordinator']['id'])
                and summary.get('run_attempt') == str(manifest['runs']['coordinator']['attempt'])
                and summary.get('campaign_manifest_sha256') == hashlib.sha256(manifest_bytes).hexdigest()
                and summary.get('measurement_complete') is True and summary.get('break_passed') is True,
                'summary identity, manifest binding or acceptance mismatch')
        starts, ends = [], []
        for role in PATHS:
            identity = manifest['runs'][role]
            run = runs[role]
            start = validate_run(run, role, repository, commit, identity['id'], identity['attempt'])
            require(start == timestamp(identity['created_at']), 'run creation time mismatch')
            if recovered:
                first_run = run if identity['attempt'] == 1 else (firsts or {}).get(role)
                require(first_run is not None, 'run creation time mismatch')
                first = validate_run(first_run, role, repository, commit, identity['id'], 1)
                require(first == timestamp(identity['first_created_at']), 'run creation time mismatch')
                start = first
            require(run.get('status') == 'completed' and run.get('conclusion') == 'success',
                    'run is not complete and successful')
            end = timestamp(run.get('updated_at'))
            require(start <= end, 'run ends before creation')
            document = documents[role]
            validate_jobs(document, identity['id'], identity['attempt'], commit, layout[role],
                          role == 'coordinator', terminal=True)
            job_seconds, queue_seconds = 0.0, 0.0
            names = job_names(layout[role])
            for job in document['jobs']:
                record = job
                origin = int(origins[names[job['name']]]['run_attempt']) if recovered and job['name'] in names else None
                if origin is not None and origin < identity['attempt']:
                    other = find_job((origin_documents or {}).get(role, {}).get(origin) or {'jobs': []}, job['name'])
                    require(other is not None and other.get('run_id') == identity['id']
                            and other.get('run_attempt') == origin and other.get('conclusion') == 'success'
                            and all(other.get(key) == job.get(key) for key in ('started_at', 'completed_at')),
                            'invalid job timing')
                    record = other
                created, begun, completed = (timestamp(record.get(key)) for key in
                                             ('created_at', 'started_at', 'completed_at'))
                require(start <= created <= begun <= completed <= end, 'invalid job timing')
                queue_seconds += (begun - created).total_seconds()
                job_seconds += (completed - begun).total_seconds()
            starts.append(start)
            ends.append(end)
            report['runs'][role] = {'run_id': identity['id'], 'run_attempt': identity['attempt'],
                                    'created_at': run['created_at'], 'completed_at': run['updated_at'],
                                    'elapsed_seconds': (end - start).total_seconds(),
                                    'job_seconds': job_seconds, 'queue_seconds': queue_seconds}
        report['elapsed_seconds'] = (max(ends) - min(starts)).total_seconds()
        report['status'] = 'met' if report['elapsed_seconds'] <= BUDGET else 'missed'
        if report['status'] == 'missed':
            report['reasons'].append('campaign_exceeded_budget')
    except (KeyError, ValueError, TypeError, OverflowError) as error:
        report['reasons'].append(str(error))
    return report


def terminal(repository, commit, manifest_path, summary_path, output):
    try:
        manifest_bytes = Path(manifest_path).read_bytes()
        summary_bytes = Path(summary_path).read_bytes()
        manifest = c5_runtime.strict_json(manifest_bytes)
        require(isinstance(manifest, dict) and manifest.get('repository') == repository
                and isinstance(commit, str) and re.fullmatch(r'[0-9a-f]{40}', commit),
                'invalid terminal identity')
        head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
        require(head == commit, 'checked out source does not match campaign commit')
        for path in PATHS.values():
            committed = subprocess.check_output(['git', 'show', f'{commit}:{path}'], cwd=ROOT)
            require((ROOT / path).read_bytes() == committed, 'workflow matrix source differs from commit')
        layout = matrices()
        origins = validate_manifest(manifest, commit, layout['coordinator'], layout['companion'])
        runs, documents, firsts, origin_documents = {}, {}, {}, {}
        for role in PATHS:
            identity = manifest['runs'][role]
            rid, attempt = identity['id'], identity['attempt']
            runs[role] = api(repository, f'actions/runs/{rid}/attempts/{attempt}')
            documents[role] = jobs(repository, rid, attempt)
            if manifest['schema'] == 2:
                if attempt > 1:
                    firsts[role] = api(repository, f'actions/runs/{rid}/attempts/1')
                earlier = {int(origins[shard]['run_attempt']) for shard in layout[role]} - {attempt}
                origin_documents[role] = {k: jobs(repository, rid, k) for k in sorted(earlier)}
        report = terminal_report(repository, commit, manifest_bytes, summary_bytes, layout, runs, documents,
                                 firsts, origin_documents)
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
        report = {'status': 'incomplete', 'reasons': [str(error)], 'budget_seconds': BUDGET,
                  'elapsed_seconds': None, 'runs': {}}
    with Path(output).open('x', encoding='utf-8') as stream:
        json.dump(report, stream, indent=2, sort_keys=True)
        stream.write('\n')
    return 0 if report['status'] == 'met' else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    preparation = commands.add_parser('prepare')
    for arg in ('repository', 'commit', 'output'):
        preparation.add_argument('--' + arg, required=True)
    for role in PATHS:
        preparation.add_argument('--' + role + '-run-id', required=True, type=int)
        preparation.add_argument('--' + role + '-attempt', required=True, type=int)
    final = commands.add_parser('terminal')
    for arg in ('repository', 'commit', 'manifest', 'summary', 'output'):
        final.add_argument('--' + arg, required=True)
    args = parser.parse_args()
    try:
        if args.command == 'terminal':
            return terminal(args.repository, args.commit, args.manifest, args.summary, args.output)
        prepare(args.repository, args.commit, args.coordinator_run_id, args.coordinator_attempt,
                args.companion_run_id, args.companion_attempt, args.output)
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
        parser.exit(1, f'C5 campaign validation failed: {error}\n')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())

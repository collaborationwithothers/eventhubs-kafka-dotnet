#!/usr/bin/env python3
"""Offline C5 runtime qualification and bounded diagnostic tools."""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

import c5_workflow as workflow

ROOT = Path(__file__).resolve().parents[3]

def geometry(report, target, source, expected_sha256, config, expected_config_sha256):
    """Read a source-bound native inventory; diagnostic evidence, never acceptance."""
    if hashlib.sha256(source).hexdigest() != expected_sha256:
        raise ValueError('target source mismatch')
    if hashlib.sha256(config).hexdigest() != expected_config_sha256:
        raise ValueError('configuration mismatch')
    rows = workflow.report_records(report, target, source_sha256=expected_sha256)
    text = workflow.target_text(source)
    starts = workflow.line_starts(text)
    length = workflow.utf16_length(text)
    result = []
    for row in rows:
        span = workflow.node_span(row, starts, length)
        result.append({'id': row['id'], 'mutatorName': row['mutatorName'],
                       'replacement': row['replacement'], 'span': span,
                       'status': row['status'],
                       'filter_ignored': (row['status'], row.get('statusReason')) == workflow.FILTERED,
                       'measured_execution': row['status'] in workflow.TESTED})
    return result


def estimate_work(observed_seconds, untested_count, setup_seconds, parallelism, isolated_seconds):
    """Conservatively weight unmeasured identities at the largest comparable duration."""
    import math
    def finite(value):
        return type(value) in (int, float) and math.isfinite(value) and value >= 0
    if (not isinstance(observed_seconds, list) or any(not finite(v) for v in observed_seconds)
            or type(untested_count) is not int or untested_count < 0
            or type(parallelism) is not int or parallelism < 1
            or not finite(setup_seconds) or not finite(isolated_seconds)):
        raise ValueError('invalid estimate input')
    if untested_count and not observed_seconds:
        raise ValueError('missing comparable measurements')
    work = sum(observed_seconds) + untested_count * max(observed_seconds, default=0)
    return {'main_work_seconds': work, 'estimated_seconds': math.ceil(setup_seconds + work / parallelism + isolated_seconds)}

"""Bounded, single-run mutation-only suite profiler (merge into c5_runtime.py)."""
import json
import math
import os
from pathlib import Path
import platform
import signal
import subprocess
import time
import xml.etree.ElementTree as ET


def profile_cases(xml_path):
    """Read a complete xUnit report; never infer elapsed wall time from its durations."""
    root = ET.parse(xml_path).getroot()
    if root.tag != 'assemblies':
        raise ValueError('unsupported xUnit report')
    cases = []
    for item in root.iter('test'):
        name, result = item.get('name'), item.get('result')
        try:
            seconds = float(item.attrib['time'])
        except (KeyError, ValueError) as exc:
            raise ValueError('invalid test duration') from exc
        if not name or result not in ('Pass', 'Fail', 'Skip') or not math.isfinite(seconds) or seconds < 0:
            raise ValueError('invalid test case')
        cases.append({'name': name, 'result': result, 'seconds': seconds})
    if not cases:
        raise ValueError('empty xUnit report')
    return cases


def profile_launch(command, root, logfile, timeout):
    """Run one process group, including build, and terminate it on timeout/interruption."""
    process = None
    previous = signal.getsignal(signal.SIGTERM)
    def interrupted(_signum, _frame):
        raise KeyboardInterrupt
    signal.signal(signal.SIGTERM, interrupted)
    try:
        with logfile.open('xb') as log:
            process = subprocess.Popen(command, cwd=root, stdout=log, stderr=subprocess.STDOUT,
                                       start_new_session=True)
            try:
                return process.wait(timeout=timeout), None
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait()
                return process.returncode, 'timeout'
    except KeyboardInterrupt:
        if process is not None and process.poll() is None:
            os.killpg(process.pid, signal.SIGKILL)
            process.wait()
        return None if process is None else process.returncode, 'interrupted'
    except OSError:
        if process is not None and process.poll() is None:
            os.killpg(process.pid, signal.SIGKILL)
            process.wait()
        return None, 'launch_failed'
    finally:
        signal.signal(signal.SIGTERM, previous)


def profile(output_dir, timeout, root, launcher=profile_launch, clock=time.monotonic):
    """Run once, preserve diagnostic evidence outside the checkout, return exit status."""
    root = Path(root).resolve()
    output = Path(output_dir).resolve()
    if output == root or root in output.parents:
        raise ValueError('output directory must be outside the repository')
    if isinstance(timeout, bool) or not isinstance(timeout, (int, float)) or not math.isfinite(timeout) or not 0 < timeout <= 600:
        raise ValueError('timeout must be positive and at most 600 seconds')
    if output.exists() and (not output.is_dir() or any(output.iterdir())):
        raise ValueError('output directory must be empty')
    output.mkdir(parents=True, exist_ok=True)
    command = ['dotnet', 'test', '--project', 'tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj',
               '--configuration', 'Release', '--framework', 'net10.0', '-p:RestoreLockedMode=true',
               '-p:ParchwellVerifierMutationOnly=true', '--minimum-expected-tests', '1',
               '--results-directory', str(output), '--', '--report-xunit-xml',
               '--report-xunit-xml-filename', 'results.xml']
    def git(*args):
        return subprocess.check_output(['git', *args], cwd=root, text=True).strip()
    facts = {'command': command, 'source_commit': git('rev-parse', 'HEAD'),
             'source_status': git('status', '--porcelain'),
             'sdk_config': json.loads((root / 'global.json').read_text(encoding='utf-8')),
             'machine': {'platform': platform.platform(), 'machine': platform.machine(),
                         'processor': platform.processor(), 'logical_cpus': os.cpu_count()},
             'timeout_seconds': timeout}
    (output / 'command.json').write_text(json.dumps(facts, indent=2) + '\n', encoding='utf-8')
    start = clock()
    exit_code, termination = launcher(command, root, output / 'command.log', timeout)
    elapsed = clock() - start
    summary = {'status': 'incomplete', 'termination': termination, 'exit_code': exit_code,
               'wall_seconds': elapsed}
    try:
        cases = profile_cases(output / 'results.xml')
        summary.update({'cases': cases, 'test_duration_seconds': sum(c['seconds'] for c in cases),
                        'failures': sum(c['result'] == 'Fail' for c in cases),
                        'skips': sum(c['result'] == 'Skip' for c in cases)})
        if exit_code == 0 and termination is None and not summary['failures'] and not summary['skips']:
            summary['status'] = 'complete'
        elif termination is None:
            summary['termination'] = 'failed_or_skipped_tests'
    except (OSError, ValueError, ET.ParseError):
        if termination is None:
            summary['termination'] = 'missing_or_invalid_report' if exit_code == 0 else 'test_failed'
    (output / 'profile.json').write_text(json.dumps(summary, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    return 0 if summary['status'] == 'complete' else 1

"""Offline analysis of one saved GitHub Actions workflow attempt."""
import datetime
import json
import re
import subprocess
from pathlib import Path


def _timestamp(value):
    if not isinstance(value, str):
        raise ValueError('missing timestamp')
    parsed = datetime.datetime.fromisoformat(value.replace('Z', '+00:00'))
    if parsed.tzinfo is None:
        raise ValueError('timestamp lacks timezone')
    return parsed


def expected_layout_at_head(head, repository):
    """Read the production matrix from the measured revision without a network call."""
    if not isinstance(head, str) or not re.fullmatch(r'[0-9a-f]{40}', head):
        raise ValueError('invalid head SHA')
    try:
        text = subprocess.check_output(
            ['git', 'show', f'{head}:.github/workflows/verifier-c5-mutation.yml'],
            cwd=repository, text=True, stderr=subprocess.DEVNULL)
    except (OSError, subprocess.CalledProcessError) as error:
        raise ValueError('measured workflow unavailable') from error
    marker = '      matrix:\n        include:\n'
    if text.count(marker) != 1:
        raise ValueError('unsupported matrix')
    block = text.split(marker, 1)[1].split('    env:', 1)[0]
    pairs = re.findall(r'^          - shard: ([a-z0-9-]+)\n            target: ([^\n]+)$', block, re.MULTILINE)
    if not pairs or len(pairs) != len(set(key for key, _ in pairs)):
        raise ValueError('invalid matrix entries')
    remainder = re.sub(r'^          - shard: ([a-z0-9-]+)\n            target: ([^\n]+)\n?', '', block, flags=re.MULTILINE)
    if remainder.strip():
        raise ValueError('unsupported matrix contents')
    return dict(pairs)


def performance_report(run, jobs_document, expected_shards):
    """Return diagnostics; expected_shards maps shard IDs to targets at the run SHA."""
    report = {'run_id': run.get('id'), 'run_attempt': run.get('run_attempt'),
              'head_sha': run.get('head_sha'), 'budget_seconds': 1800,
              'workflow_elapsed_seconds': None, 'mutation_job_seconds': None,
              'peak_mutation_jobs': None, 'queue_seconds': {},
              'status': 'incomplete', 'reasons': []}
    reasons = report['reasons']
    try:
        run_id, attempt, head = run['id'], run['run_attempt'], run['head_sha']
        if type(run_id) is not int or type(attempt) is not int or attempt < 1 or not isinstance(head, str) or not head:
            raise ValueError('invalid run identity')
        start, end = _timestamp(run['created_at']), _timestamp(run['updated_at'])
        if end < start:
            raise ValueError('run ends before creation')
        report['workflow_elapsed_seconds'] = (end - start).total_seconds()
        if run.get('status') != 'completed' or run.get('conclusion') != 'success':
            reasons.append('run_not_successful')
        jobs = jobs_document['jobs']
        if not isinstance(jobs, list) or not jobs or not isinstance(expected_shards, dict) or not expected_shards:
            raise ValueError('missing jobs or expected layout')
        if 'total_count' in jobs_document and jobs_document['total_count'] != len(jobs):
            reasons.append('missing_job_pages')
        full_names = [f'mutation ({shard}, {target})' for shard, target in expected_shards.items()]
        expected = {name if len(name) <= 100 else name[:97] + '...' for name in full_names}
        if len(expected) != len(full_names):
            raise ValueError('ambiguous truncated job names')
        seen = set()
        ids = set()
        events = []
        intervals = []
        aggregate_seen = False
        for job in jobs:
            if not isinstance(job, dict):
                raise ValueError('invalid job')
            if job.get('run_id') != run_id or job.get('run_attempt') != attempt or job.get('head_sha') != head:
                reasons.append('mixed_attempt_or_commit')
            identifier = job.get('id')
            if type(identifier) is not int or identifier in ids:
                reasons.append('missing_or_duplicate_job_id')
            ids.add(identifier)
            name = job.get('name')
            if name == 'aggregate':
                if aggregate_seen:
                    reasons.append('duplicate_aggregate')
                aggregate_seen = True
            if job.get('status') != 'completed' or job.get('conclusion') != 'success':
                reasons.append('job_not_successful')
            try:
                created, job_start, job_end = (_timestamp(job[k]) for k in ('created_at', 'started_at', 'completed_at'))
                if not start <= created <= job_start <= job_end:
                    raise ValueError('invalid job timestamps')
                if job_end > end:
                    reasons.append('job_after_run_completion')
            except (KeyError, ValueError, TypeError):
                reasons.append('invalid_job_timestamps')
                continue
            if isinstance(name, str) and name.startswith('mutation ('):
                if name not in expected or name in seen:
                    reasons.append('unexpected_or_duplicate_mutation_job')
                seen.add(name)
                intervals.append((job_end - job_start).total_seconds())
                report['queue_seconds'][name] = (job_start - created).total_seconds()
                events.extend(((job_start, 1), (job_end, -1)))
        if seen != expected or not aggregate_seen:
            reasons.append('missing_jobs')
        report['mutation_job_seconds'] = sum(intervals)
        # End events sort before start events at the same time, so touching jobs do not overlap.
        active = peak = 0
        for _, delta in sorted(events):
            active += delta
            peak = max(peak, active)
        report['peak_mutation_jobs'] = peak
        if not reasons:
            report['status'] = 'met' if report['workflow_elapsed_seconds'] <= 1800 else 'missed'
    except (KeyError, ValueError, TypeError, OverflowError):
        reasons.append('invalid_input')
    report['reasons'] = sorted(set(reasons))
    return report


def write_performance(run_file, jobs_file, output_dir, expected_shards):
    """Write a report without contacting GitHub; return the CLI exit status."""
    try:
        run = json.loads(Path(run_file).read_text())
        jobs = json.loads(Path(jobs_file).read_text())
        if not isinstance(run, dict) or not isinstance(jobs, dict):
            raise ValueError('expected JSON objects')
        result = performance_report(run, jobs, expected_shards)
    except (OSError, ValueError, json.JSONDecodeError):
        result = {'run_id': None, 'run_attempt': None, 'head_sha': None, 'budget_seconds': 1800,
                  'workflow_elapsed_seconds': None, 'mutation_job_seconds': None,
                  'peak_mutation_jobs': None, 'queue_seconds': {}, 'status': 'incomplete',
                  'reasons': ['invalid_input']}
    destination = Path(output_dir)
    destination.mkdir(parents=True, exist_ok=True)
    (destination / 'performance.json').write_text(json.dumps(result, indent=2, sort_keys=True) + '\n')
    return 0 if result['status'] == 'met' else 1


"""Offline runtime qualification checks for the verifier mutation layout."""

import ast
import hashlib
import json
import math
from pathlib import Path, PurePosixPath
import re
import subprocess
import xml.etree.ElementTree as ET

import c5_workflow as workflow

REPO = ROOT
WORKFLOW = '.github/workflows/verifier-c5-mutation.yml'
RUNNER = 'tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py'
TEST_PROJECT = 'tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj'
HEX = re.compile(r'^[0-9a-f]{64}$')
EXCLUDED = ('Component/', 'Support/ComponentEvidenceBuilder.cs', 'Host/VerifyRunHandlerTests.cs',
            'Host/VerifierHostTests.cs', 'Host/B10AbruptRejectionTests.cs')
REMOVE = 'Component/**/*.cs;Support/ComponentEvidenceBuilder.cs;Host/VerifyRunHandlerTests.cs;Host/VerifierHostTests.cs;Host/B10AbruptRejectionTests.cs'


def git(*args):
    return subprocess.run(['git', '-C', str(REPO), *args], check=True,
                          stdout=subprocess.PIPE, stderr=subprocess.PIPE).stdout


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=True).encode('ascii')


def blob(ref, path):
    return git('show', f'{ref}:{path}')


def files(ref):
    return set(git('ls-tree', '-r', '--name-only', '-z', ref).decode().rstrip('\0').split('\0'))


def execution_runner(data):
    """Drop only literal declarative shard tables; retain all executable source."""
    text = data.decode('utf-8')
    tree = ast.parse(text)
    lines = text.splitlines(keepends=True)
    for node in reversed(tree.body):
        if (isinstance(node, ast.Assign) and len(node.targets) == 1
                and isinstance(node.targets[0], ast.Name)
                and node.targets[0].id in {'TARGETS', 'PRIMARY_CUTS'}):
            ast.literal_eval(node.value)
            lines[node.lineno - 1:node.end_lineno] = [node.targets[0].id + ' = {}\n']
    return ''.join(lines).encode()


def execution_workflow(data):
    text = data.decode('utf-8')
    start = text.index('        include:\n') + len('        include:\n')
    end = text.index('    env:\n', start)
    return (text[:start] + text[end:]).encode()


def workflow_matrix(data):
    text = data.decode('utf-8')
    start = text.index('        include:\n') + len('        include:\n')
    end = text.index('    env:\n', start)
    section = text[start:end]
    pattern = r'          - shard: ([^\s]+)\n            target: ([^\s]+)\n'
    pairs = re.findall(pattern, section)
    if ''.join(f'          - shard: {s}\n            target: {t}\n' for s, t in pairs) != section:
        raise ValueError('unsupported matrix')
    if len(dict(pairs)) != len(pairs):
        raise ValueError('duplicate shard')
    return dict(pairs)


def project_closure(ref, names):
    projects = {TEST_PROJECT, 'src/Parchwell.Verifier/Parchwell.Verifier.csproj'}
    pending = list(projects)
    extra = set()
    while pending:
        project = pending.pop()
        root = ET.fromstring(blob(ref, project))
        directory = PurePosixPath(project).parent
        parents = {child: parent for parent in root.iter() for child in parent}
        for element in root.iter():
            if element.tag in {'Import', 'Choose'}:
                raise ValueError('unsupported project inclusion')
            if element.tag == 'ProjectReference':
                if set(element.attrib) != {'Include'}:
                    raise ValueError('unsupported project reference')
                include = element.attrib['Include']
                candidate = str((Path(str(directory)) / include).resolve().relative_to(REPO))
                if candidate not in names:
                    raise ValueError('missing referenced project')
                if candidate not in projects:
                    projects.add(candidate)
                    pending.append(candidate)
            if element.tag == 'Compile':
                include, remove = element.get('Include'), element.get('Remove')
                if project == TEST_PROJECT and remove == REMOVE and include is None and set(element.attrib) == {'Remove'}:
                    parent = parents[element]
                    if parent.attrib != {'Condition': "'$(ParchwellVerifierMutationOnly)' == 'true'"}:
                        raise ValueError('unsupported mutation exclusion condition')
                    continue
                if project == TEST_PROJECT and include == '../fixtures/ProbeV2MockCluster.cs' and set(element.attrib) == {'Include', 'Link'}:
                    extra.add('tests/fixtures/ProbeV2MockCluster.cs')
                    continue
                raise ValueError('unsupported Compile inclusion')
            if element.tag in {'Content', 'None'} and 'Include' in element.attrib:
                include = element.attrib['Include']
                if project == TEST_PROJECT and include == '../fixtures/verifier-v1/**/*':
                    extra.update(p for p in names if p.startswith('tests/fixtures/verifier-v1/'))
                elif include.startswith('../../scenarios/') and not any(c in include for c in '*$?'):
                    candidate = str((Path(str(directory)) / include).resolve().relative_to(REPO))
                    if candidate not in names:
                        raise ValueError('missing content')
                    extra.add(candidate)
                else:
                    raise ValueError('unsupported content inclusion')
    return projects, extra


def input_maps(ref):
    names = files(ref)
    projects, extra = project_closure(ref, names)
    target_paths = {f'src/Parchwell.Verifier/{path}' for path in workflow.TARGETS.values() for path in [path[0]]}
    selected = set(projects) | extra | {'global.json', 'Directory.Build.props', 'Directory.Packages.props',
        '.config/dotnet-tools.json', 'docs/operations/a01-runtime-limits.json', RUNNER, WORKFLOW,
        'tests/Parchwell.Verifier.Tests/Mutation/c5_projection.py',
        'tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json'}
    ancestors = {str(parent) for project in projects for parent in PurePosixPath(project).parents}
    selected.update(p for p in names if (PurePosixPath(p).name in ('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props') and str(PurePosixPath(p).parent) in ancestors) or re.fullmatch(r'(?:NuGet\.[Cc]onfig|global\.json|.*?/packages\.lock\.json)', p))
    selected.update(p for p in names if re.fullmatch(r'scenarios/profiles/[^/]+\.yaml', p))
    for project in projects:
        root = str(PurePosixPath(project).parent) + '/'
        for p in names:
            if not p.startswith(root):
                continue
            rel = p[len(root):]
            if project == TEST_PROJECT and any(rel == x or (x.endswith('/') and rel.startswith(x)) for x in EXCLUDED):
                continue
            if p.endswith(('.cs', '.csproj', '.props', '.targets', '.resx')):
                selected.add(p)
    selected -= target_paths
    if not selected <= names:
        raise ValueError('missing input')
    result = {}
    for p in sorted(selected):
        data = blob(ref, p)
        if p == RUNNER:
            data = execution_runner(data)
        elif p == WORKFLOW:
            data = execution_workflow(data)
        result[p] = digest(data)
    targets = {p: digest(blob(ref, 'src/Parchwell.Verifier/' + p)) for p, _ in workflow.TARGETS.values()}
    return result, targets


def shape(config):
    if not isinstance(config, dict) or set(config) != {'schema_version', 'budget_seconds', 'shared_inputs', 'targets'}:
        raise ValueError('invalid schema')
    if type(config['schema_version']) is not int or config['schema_version'] != 1 or type(config['budget_seconds']) is not int or config['budget_seconds'] != 1800:
        raise ValueError('unsupported schema or budget')
    if not isinstance(config['shared_inputs'], dict) or not isinstance(config['targets'], dict):
        raise ValueError('invalid maps')
    for path, sha in config['shared_inputs'].items():
        if not isinstance(path, str) or not isinstance(sha, str) or not HEX.fullmatch(sha):
            raise ValueError('invalid input hash')
    for path, record in config['targets'].items():
        if not isinstance(path, str) or not isinstance(record, dict) or set(record) != {'source_sha256', 'evidence', 'shards'}:
            raise ValueError('invalid target')
        if not isinstance(record['source_sha256'], str) or not HEX.fullmatch(record['source_sha256']):
            raise ValueError('invalid source hash')
        if not isinstance(record['evidence'], list) or not isinstance(record['shards'], dict):
            raise ValueError('invalid evidence or shards')
        for evidence in record['evidence']:
            if not isinstance(evidence, dict) or set(evidence) != {'source_commit','locator','sha256','kind','note'}:
                raise ValueError('invalid evidence shape')
            if any(not isinstance(evidence[x], str) or not evidence[x].strip() for x in evidence):
                raise ValueError('invalid evidence value')
            if not HEX.fullmatch(evidence['sha256']) or evidence['kind'] not in {'hosted_complete','local_probe','partial_timing','suite_profile'}:
                raise ValueError('invalid evidence type')


def validate(config, shared, targets, source, matrix):
    reasons = set()
    if set(config['targets']) != set(targets):
        reasons.add('missing_qualification')
    if config['shared_inputs'] != shared:
        reasons.add('stale_inputs')
    if len(matrix) > 256 or len(workflow.SHARDS) > 256:
        reasons.add('matrix_over_capacity')
    if matrix != workflow.SHARDS:
        reasons.add('layout_mismatch')
    target_sources = {p: v['source_sha256'] for p, v in config['targets'].items()}
    timing_hash = digest(canonical({'shared_inputs': config['shared_inputs'], 'target_sources': target_sources}))
    all_shards = {}
    shard_fields = {'selection_sha256', 'timing_inputs_sha256', 'main_mutants', 'main_work_seconds',
                    'parallelism', 'setup_seconds', 'isolated_seconds', 'estimated_seconds', 'basis'}
    for path, record in config['targets'].items():
        if path not in targets:
            continue
        if record['source_sha256'] != targets[path]:
            reasons.add('stale_inputs')
        if not record['evidence']:
            reasons.add('missing_measurement')
        for evidence in record['evidence']:
            if evidence['kind'] == 'local_probe' and not all(
                    re.search(r'\b' + field + r'=\S+', evidence['note'])
                    for field in ('hardware', 'calibration')):
                reasons.add('missing_measurement')
        for shard, item in record['shards'].items():
            if shard in all_shards:
                reasons.add('layout_mismatch')
            all_shards[shard] = path
            if not isinstance(item, dict) or set(item) != shard_fields:
                reasons.add('invalid_estimate')
                continue
            if item['timing_inputs_sha256'] != timing_hash:
                reasons.add('stale_inputs')
            if shard in workflow.SHARDS and workflow.SHARDS[shard] == path:
                selection = digest(canonical(workflow.shard_config(shard, source[path])))
                if item['selection_sha256'] != selection:
                    reasons.add('layout_mismatch')
            else:
                reasons.add('layout_mismatch')
            numeric = ('main_work_seconds', 'setup_seconds', 'isolated_seconds', 'estimated_seconds')
            valid = (type(item['main_mutants']) is int and item['main_mutants'] >= 0
                     and type(item['parallelism']) is int and item['parallelism'] > 0
                     and all(type(item[k]) in (int, float) and 0 <= item[k] <= sys.float_info.max and math.isfinite(item[k]) for k in numeric)
                     and isinstance(item['basis'], str) and bool(item['basis'].strip()))
            if not valid:
                reasons.add('invalid_estimate')
                continue
            workers = re.search(r'\bobserved_workers=([1-9][0-9]*)\b', item['basis'])
            if not workers or item['parallelism'] > int(workers.group(1)):
                reasons.add('missing_measurement')
            try:
                estimate = math.ceil(item['setup_seconds'] + item['main_work_seconds'] / item['parallelism'] + item['isolated_seconds'])
            except (OverflowError, ValueError):
                reasons.add('invalid_estimate')
                continue
            if item['estimated_seconds'] != estimate:
                reasons.add('invalid_estimate')
            if item['main_mutants'] and (item['setup_seconds'] == 0 or item['main_work_seconds'] == 0):
                reasons.add('missing_measurement')
            if item['estimated_seconds'] > 1800:
                reasons.add('shard_over_budget')
    if all_shards != workflow.SHARDS:
        reasons.add('layout_mismatch')
    return reasons


def check(base, head, qualification):
    """Return the specified JSON result. No test or mutation process is started."""
    try:
        head = git('rev-parse', '--verify', head + '^{commit}').decode().strip()
        if base:
            base = git('rev-parse', '--verify', base + '^{commit}').decode().strip()
        # shard_config and its declarative tables must come from the requested revision.
        if Path(workflow.__file__).read_bytes() != blob(head, RUNNER):
            raise ValueError('checker is not running the requested revision')
        config = None
        try:
            config = json.loads(Path(qualification).read_text())
            shape(config)
        except FileNotFoundError:
            pass
        shared, targets = input_maps(head)
        matrix = workflow_matrix(blob(head, WORKFLOW))
        if base:
            changed = set(git('diff', '--name-only', '-z', base, head).decode().rstrip('\0').split('\0'))
            try:
                old_shared, old_targets = input_maps(base)
                relevant = (shared != old_shared or targets != old_targets or RUNNER in changed or WORKFLOW in changed or 'tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json' in changed)
            except (ValueError, subprocess.CalledProcessError):
                # A newly introduced target or supported dependency cannot exist in the base.
                relevant = True
            if not relevant:
                return {'status': 'not_applicable', 'reasons': []}
        if config is None:
            return {'status': 'rejected', 'reasons': ['missing_qualification']}
        source = {p: blob(head, 'src/Parchwell.Verifier/' + p) for p in targets}
        reasons = sorted(validate(config, shared, targets, source, matrix))
        return {'status': 'rejected' if reasons else 'qualified', 'reasons': reasons}
    except (ValueError, TypeError, KeyError, OSError, subprocess.CalledProcessError, ET.ParseError, SyntaxError, UnicodeError, OverflowError):
        return {'status': 'rejected', 'reasons': ['invalid_input']}



def bounded_probe(target, span, output_dir, tally_path, timeout=600, launcher=workflow.local_check):
    """Reserve an issue-wide probe slot before launching, including on interruption."""
    tally = Path(tally_path).resolve()
    output = Path(output_dir).resolve()
    if ROOT in tally.parents or tally == ROOT or ROOT in output.parents or output == ROOT:
        raise ValueError('probe evidence must be outside the repository')
    if type(timeout) not in (int, float) or not 0 < timeout <= 600:
        raise ValueError('invalid timeout')
    if output.exists():
        raise ValueError('probe output already exists')
    try:
        entries = json.loads(tally.read_text())
    except FileNotFoundError:
        entries = []
    if not isinstance(entries, list) or len(entries) >= 24:
        raise ValueError('probe budget exhausted or invalid')
    entries.append({'target': target, 'span': span, 'output': str(output), 'timeout': timeout})
    tally.parent.mkdir(parents=True, exist_ok=True)
    temporary = tally.with_name(tally.name + '.new')
    temporary.write_text(json.dumps(entries, indent=2) + '\n')
    os.replace(temporary, tally)
    return launcher(target, span, output, timeout=timeout, repository=ROOT)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    p = commands.add_parser('profile')
    p.add_argument('--output-dir', required=True)
    p.add_argument('--timeout', required=True, type=float)
    c = commands.add_parser('check')
    c.add_argument('--base')
    c.add_argument('--head', required=True)
    c.add_argument('--qualification', required=True)
    perf = commands.add_parser('performance')
    perf.add_argument('--run-json', required=True)
    perf.add_argument('--jobs-json', required=True)
    perf.add_argument('--output-dir', required=True)
    args = parser.parse_args()
    if args.command == 'check':
        result = check(args.base, args.head, args.qualification)
        print(json.dumps(result, sort_keys=True))
        return int(result['status'] == 'rejected')
    if args.command == 'profile':
        try:
            return profile(args.output_dir, args.timeout, ROOT)
        except (OSError, ValueError, subprocess.SubprocessError) as error:
            print(str(error), file=sys.stderr)
            return 1
    try:
        run = json.loads(Path(args.run_json).read_text())
        expected = expected_layout_at_head(run['head_sha'], ROOT)
    except (OSError, ValueError, TypeError, KeyError):
        expected = None
    return write_performance(args.run_json, args.jobs_json, args.output_dir, expected)


if __name__ == '__main__':
    sys.exit(main())

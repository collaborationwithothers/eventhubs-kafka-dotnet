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
                                       env={**os.environ, **workflow.OVERRIDES},
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
    facts = {'command': command, 'environment_overrides': dict(workflow.OVERRIDES),
             'source_commit': git('rev-parse', 'HEAD'),
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
        if any('}' in node.tag for node in root.iter()):
            raise ValueError('unsupported namespaced project')
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
        if PurePosixPath(p).name in ('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props'):
            build_xml = ET.fromstring(data)
            if any('}' in node.tag for node in build_xml.iter()):
                raise ValueError('unsupported namespaced build input')
            if any(node.tag in {'Import', 'ImportGroup', 'Choose', 'Compile', 'Content', 'None', 'ProjectReference', 'Target'} for node in build_xml.iter()):
                raise ValueError('unsupported build inclusion')
        if p == RUNNER:
            data = execution_runner(data)
        elif p == WORKFLOW:
            data = execution_workflow(data)
        result[p] = digest(data)
    targets = {p: digest(blob(ref, 'src/Parchwell.Verifier/' + p)) for p, _ in workflow.TARGETS.values()}
    return result, targets


# The initial live-qualification pilot's only approved incomplete native inventories.
# Decision: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6880732047
PROVISIONAL_TARGETS = {
    'Judge/B10FinalJudge.cs': ('cd88f482f9c93947e613ec4b13a34ff554331dfe8ebd9a3e400ed3bbb360d34a', '14111392757', '7c300f5fd0185d8952b4bfa5229432b829b4713f793a5b62ea7afd63cca4a709'),
    'Common/Phase/B10FinalProjectionCodec.cs': ('c92b0d32b3246a41ef4fc3b3f4607b3397161782c99c03d5774a1def895a1dc5', '14111558997', 'cfe9779a0ddaf62f8e9d43aa7ab8724904efd07f3a77a51e4c59f9d18633145e'),
    'VerifyRun/B10Final/B10FinalRepeatEvidence.cs': ('4e533dd7aa4205e0a5a121b94b9441af7438e5d878600a3b65dc6ad5f4edab68', '14111653842', '7d65fe2b27ee06585f1c6914e6ca363b376507b0dd1ee8f28f0700f2dcd663d6'),
}
# Canonical maps of shard ID to generated selection hash for the approved pilot.
PROVISIONAL_LAYOUT_HASHES = {
    'Common/Phase/B10FinalProjectionCodec.cs': 'ad21dfabb9441f5b8f766e201676ac01d38563f61b21767c34d8f23589b69ff9',
    'Judge/B10FinalJudge.cs': '0edc49b41523422087ea8a0cc90bb2f781fc84074f43e92c42d762c1bd635aad',
    'VerifyRun/B10Final/B10FinalRepeatEvidence.cs': 'a36d5aa3b38d51fa945cd497ab4226a95a7c9496cf923d70f7c162084be19683',
}
PROVISIONAL_TIMING_INPUTS_HASH = 'e15cd682a2731538bfde90aa333a9742bccb40e3db957f855d71a25a9e520463'
PROVISIONAL_STATUS = 'pending_native_report'
NUMERIC_FIELDS = ('main_mutants', 'main_work_seconds', 'parallelism', 'setup_seconds', 'isolated_seconds', 'estimated_seconds')

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
        if not isinstance(path, str) or not isinstance(record, dict):
            raise ValueError('invalid target')
        provisional = 'geometry_status' in record
        expected_fields = {'source_sha256', 'evidence', 'shards'} | ({'geometry_status', 'native_geometry'} if provisional else set())
        if set(record) != expected_fields or (provisional and (path not in PROVISIONAL_TARGETS or record['geometry_status'] != PROVISIONAL_STATUS)):
            raise ValueError('invalid target')
        if not isinstance(record['source_sha256'], str) or not HEX.fullmatch(record['source_sha256']):
            raise ValueError('invalid source hash')
        if not isinstance(record['evidence'], list) or not isinstance(record['shards'], dict):
            raise ValueError('invalid evidence or shards')
        shard_fields = {'selection_sha256', 'timing_inputs_sha256', 'main_mutants', 'main_work_seconds',
                        'parallelism', 'setup_seconds', 'isolated_seconds', 'estimated_seconds', 'basis'}
        for shard, item in record['shards'].items():
            if not isinstance(shard, str) or not isinstance(item, dict) or set(item) != shard_fields:
                raise ValueError('invalid shard shape')
            if any(not isinstance(item[k], str) for k in ('selection_sha256', 'timing_inputs_sha256', 'basis')):
                raise ValueError('invalid shard field')
            if any(not HEX.fullmatch(item[k]) for k in ('selection_sha256', 'timing_inputs_sha256')):
                raise ValueError('invalid shard hash')
            if provisional:
                if ((path == CODEC_TARGET and shard in CODEC_PROVISIONAL_SHARDS) or (path == REPEAT_TARGET and shard == REPEAT_MERGED_SHARD)) and any(item[k] is None for k in NUMERIC_FIELDS):
                    raise ValueError('pinned codec owner requires its estimate')
                if any(item[k] is not None for k in NUMERIC_FIELDS) and not (
                    ((path == CODEC_TARGET and shard in CODEC_PROVISIONAL_SHARDS) or (path == REPEAT_TARGET and shard == REPEAT_MERGED_SHARD))
                    and all(type(item[k]) in (int, float) for k in NUMERIC_FIELDS)):
                    raise ValueError('provisional estimates must be unknown except the pinned codec owners')
            elif any(type(item[k]) not in (int, float, bool) for k in NUMERIC_FIELDS):
                raise ValueError('invalid numeric field')
        for evidence in record['evidence']:
            if not isinstance(evidence, dict) or set(evidence) != {'source_commit','locator','sha256','kind','note'}:
                raise ValueError('invalid evidence shape')
            if any(not isinstance(evidence[x], str) or not evidence[x].strip() for x in evidence):
                raise ValueError('invalid evidence value')
            if not HEX.fullmatch(evidence['sha256']) or evidence['kind'] not in {'hosted_complete','local_probe','partial_timing','suite_profile'}:
                raise ValueError('invalid evidence type')



def numeric_schema_errors(config):
    """Reject invalid numeric estimates before any scope shortcut."""
    for record in config['targets'].values():
        if record.get('geometry_status') == PROVISIONAL_STATUS:
            continue
        for item in record['shards'].values():
            if type(item['main_mutants']) is not int or item['main_mutants'] < 0:
                return {'invalid_estimate'}
            if type(item['parallelism']) is not int or item['parallelism'] < 1:
                return {'invalid_estimate'}
            keys = ('main_work_seconds', 'setup_seconds', 'isolated_seconds', 'estimated_seconds')
            if any(type(item[k]) not in (int, float) or not 0 <= item[k] <= sys.float_info.max
                   or not math.isfinite(item[k]) for k in keys):
                return {'invalid_estimate'}
            try:
                expected = math.ceil(item['setup_seconds'] + item['main_work_seconds'] / item['parallelism'] + item['isolated_seconds'])
            except (OverflowError, ValueError):
                return {'invalid_estimate'}
            if item['estimated_seconds'] != expected:
                return {'invalid_estimate'}
    return set()

STATIC_TARGET = 'VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs'
STATIC_SOURCE_HASH = 'e266b1d73da52fe84fbb583bac42e8bd5e2118af445d8a2f3c4bf7c4ff67f661'
STATIC_EVIDENCE = {
    (11134, 11241): ('14108519875', '7eb4a26b2498293a80a6d7b91155ea2eca785da32b59ed05bdce730bffe3ce38', 208, 1892.542),
    (370, 595): ('14108227262', '68f25d8a4090d13ed253cb33bb69be5ecc852367210b1fe36016d5272c2ec911', 208, 1743.157),
}


def accepted_static_regions(path, shard, source):
    if path != STATIC_TARGET or digest(source) != STATIC_SOURCE_HASH:
        return []
    try:
        return [region for region in workflow.shard_static_regions(shard, source) if region in STATIC_EVIDENCE]
    except (KeyError, ValueError, TypeError):
        return []


def approved_static_owner(path, record, shard, item, source):
    """Recognize only the two measured declarations accepted in the issue decisions."""
    regions = accepted_static_regions(path, shard, source)
    if (len(regions) != 1 or record['source_sha256'] != STATIC_SOURCE_HASH
            or item['main_mutants'] != 0 or item['main_work_seconds'] != 0):
        return False
    region = regions[0]
    artifact, artifact_hash, setup, isolated = STATIC_EVIDENCE[region]
    if (item['setup_seconds'] != setup or item['isolated_seconds'] != isolated
            or artifact not in item['basis'] or f'[{region[0]},{region[1]})' not in item['basis']
            or not any(e['kind'] == 'hosted_complete' and e['sha256'] == artifact_hash
                       and artifact in e['locator'] for e in record['evidence'])):
        return False
    try:
        owner = (region[0], region[1] + 1) if region == (370, 595) else (region[0] - 1, region[1])
        padding = source[region[1]:region[1] + 1] if region == (370, 595) else source[region[0] - 1:region[0]]
        return (workflow.shard_span(shard, source) == owner and padding == b'\n'
                and workflow.shard_static_regions(shard, source) == [region])
    except (KeyError, ValueError, TypeError):
        return False

# Source-bound estimates for the three whole codec declarations in the
# targeted correction; the first two use a cross-field conservative rate.
CODEC_TARGET = 'Common/Phase/B10FinalProjectionCodec.cs'
CODEC_PROVISIONAL_SHARDS = frozenset(f'b10-final-projection-codec-p{i}-{suffix}' for i in (1, 2, 3) for suffix in ('a', 'b'))
CODEC_TIMING_INPUTS_HASH = 'e15cd682a2731538bfde90aa333a9742bccb40e3db957f855d71a25a9e520463'
CODEC_SOURCE_HASH = 'c92b0d32b3246a41ef4fc3b3f4607b3397161782c99c03d5774a1def895a1dc5'
CODEC_ISOLATED = {
    'b10-final-projection-codec-p1-a': 1248.756, 'b10-final-projection-codec-p1-b': 1236.599,
    'b10-final-projection-codec-p2-a': 1445.164, 'b10-final-projection-codec-p2-b': 1634.255,
    'b10-final-projection-codec-p3-a': 1002.330, 'b10-final-projection-codec-p3-b': 1223.401,
}
CODEC_COHORTS = {
    f'b10-final-projection-codec-p{i}-{suffix}': (f'b10-final-projection-codec-p{i}', tuple(map(str, range(lo, hi))))
    for i, suffix, lo, hi in ((1, 'a', 116, 122), (1, 'b', 122, 128),
                               (2, 'a', 128, 135), (2, 'b', 135, 143),
                               (3, 'a', 143, 149), (3, 'b', 149, 156))
}
CODEC_REGIONS = {(614, 1068), (1069, 1706), (1706, 2157)}
CODEC_OWNERS = {
    (614, 1068): ((0, 1068), [(374, 614), (614, 1068)]),
    (1069, 1706): ((1068, 1706), [(1069, 1706)]),
    (1706, 2157): ((1706, 2158), [(1706, 2157)]),
}
CODEC_EVIDENCE = (
    ('14127755542', '017a78d3c863b6ca3c8cb5a2e2db5c7a0bf95988612a7e603e68e7259489477d'),
    ('14127627878', 'a9213fbb44c74a9ad62031227dbb04e89dd2f6b381dce6f77c737f38db63ee33'),
    ('14128899902', 'cd5c86cf36ed2833a01e4b3cc74755ba533e22742d8a4018ac74ba6c098c385a'),
)


def codec_static_regions(path, shard, source):
    if path != CODEC_TARGET or digest(source) != CODEC_SOURCE_HASH:
        return []
    try:
        return [region for region in workflow.shard_static_regions(shard, source) if region in CODEC_REGIONS]
    except (KeyError, ValueError, TypeError):
        return []


def approved_codec_static_owner(path, record, shard, item, source):
    """Accept only the source-bound, static-only owners in the correction."""
    regions = codec_static_regions(path, shard, source)
    if (len(regions) != 1 or record['source_sha256'] != CODEC_SOURCE_HASH
            or item['timing_inputs_sha256'] != CODEC_TIMING_INPUTS_HASH
            or item['main_mutants'] != 0 or item['main_work_seconds'] != 0
            or shard not in CODEC_COHORTS or workflow.COHORTS.get(shard) != CODEC_COHORTS[shard]
            or item['setup_seconds'] != 60 or item['isolated_seconds'] != CODEC_ISOLATED[shard]
            or f'[{regions[0][0]},{regions[0][1]})' not in item['basis']):
        return False
    if not all(any(e['kind'] == 'hosted_complete' and e['source_commit'] == 'dbc25c2005c3409ffd5f079c40b061d82c4b02aa'
                   and e['sha256'] == sha and e['locator'] == (
                       'https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43625662806 artifact ' + artifact)
                   for e in record['evidence'])
               for artifact, sha in CODEC_EVIDENCE):
        return False
    try:
        span, expected_regions = CODEC_OWNERS[regions[0]]
        return (workflow.shard_span(shard, source) == span
                and workflow.shard_static_regions(shard, source) == expected_regions)
    except (KeyError, ValueError, TypeError):
        return False

REPEAT_TARGET = 'VerifyRun/B10Final/B10FinalRepeatEvidence.cs'
REPEAT_MERGED_SHARD = 'b10-final-repeat-evidence-p3'
REPEAT_JOBS = {
    '132540792966': 'cc3a8b11d1624b469b887d9222c9fe22952bc2eb63a2de050cf1a89a4f87cf02',
    '132540792918': '1e01be3cbb949ace4c3c12d2a83e388969745b1f3596562cd5d190b274ea02e5',
}


def approved_repeat_merged_owner(path, record, shard, item, source):
    if (path != REPEAT_TARGET or shard != REPEAT_MERGED_SHARD
            or digest(source) != PROVISIONAL_TARGETS[REPEAT_TARGET][0]
            or item['timing_inputs_sha256'] != PROVISIONAL_TIMING_INPUTS_HASH
            or any(item[k] != v for k, v in {'main_mutants': 124, 'main_work_seconds': 763,
                'parallelism': 1, 'setup_seconds': 392, 'isolated_seconds': 0,
                'estimated_seconds': 1155}.items())
            or workflow.shard_span(shard, source) != (5565, 11107)):
        return False
    return all(any(e['kind'] == 'hosted_complete' and e['source_commit'] == 'dbc25c2005c3409ffd5f079c40b061d82c4b02aa'
                   and e['locator'] == 'https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43625662806/job/' + job
                   and e['sha256'] == sha for e in record['evidence']) for job, sha in REPEAT_JOBS.items())


# These complete native inventories are taken from receipt-verified hosted reports.
NATIVE_GEOMETRY_BINDINGS = {
    'VerifyRun/B10Final/B10FinalRepeatEvidence.cs': ('14127456699', '3268e1a3b6c52c469e74f6b5680aa40d2096a1901780c8c0d6416c394ed88d90', 'c9ac4182741b1f87d383ad950806a069d786e4cf8dfc55c5f0a17641f47922a3', '2bc0f8acfd79364d414b7cd46dde2804c930adc2a1fbbb9f6ad05d67a79d298a', 1306),
    'Common/Phase/B10FinalProjectionCodec.cs': ('14127755542', '017a78d3c863b6ca3c8cb5a2e2db5c7a0bf95988612a7e603e68e7259489477d', 'ab0e4d0677136a516821fff731e36d439ff57e40857c8e77bdda7e4c5183c37a', 'd5a7f544377a3d8b2c66e120e92efcee3f0a07ecabe5b45f36a40f2334251b8f', 730),
    'Judge/B10FinalJudge.cs': ('14127550132', '5daf8d8f214957e7e8f6a91db1d2093fa3c0cf0827267a07ee77da9ef14ab119', 'e9ec6237585270a6ab9e2dea44998171e276656fab9da65b2aea3a96bd19cf93', 'b57b1426e4cd0f4a5a21e06aa665f7bedf5568fcb62bc5beffe1e2885a5c9d40', 710),
}


# Digests of the reviewed compact proofs; the full inventories remain in hosted artifacts.
NATIVE_PROOF_HASHES = {
    'VerifyRun/B10Final/B10FinalRepeatEvidence.cs': '80349431732a04f9167a566472b91679e8493ff1557f77a88b3c4e2d0e07e8c7',
    'Common/Phase/B10FinalProjectionCodec.cs': 'c669681346b9a4ce4092d44a0471a9023f8d4f1e15843351e416a0f937092f5c',
    'Judge/B10FinalJudge.cs': '554f7cc41d40bae23185db9b7e9f7ed4e0dd89154977ea33a0b5d108d9836b48',
}


def native_geometry_valid(path, record, source):
    try:
        artifact, archive, report, inventory, count = NATIVE_GEOMETRY_BINDINGS[path]
        geometry = record['native_geometry']
        if (digest(canonical(geometry)) != NATIVE_PROOF_HASHES[path]
                or geometry['source_commit'] != 'dbc25c2005c3409ffd5f079c40b061d82c4b02aa'
                or geometry['source_sha256'] != digest(source)
                or (geometry['artifact_id'], geometry['archive_sha256'], geometry['report_sha256'],
                    geometry['inventory_sha256'], geometry['native_count']) != (artifact, archive, report, inventory, count)
                or geometry['layout_sha256'] != PROVISIONAL_LAYOUT_HASHES[path]
                or sum(geometry['owner_counts'].values()) != count):
            return False
        if not any(e['kind'] == 'hosted_complete' and e['source_commit'] == geometry['source_commit']
                   and e['sha256'] == archive and e['locator'] == ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43625662806 artifact ' + artifact)
                   for e in record['evidence']):
            return False
        generated = {name: digest(canonical(workflow.shard_config(name, source)))
                     for name, target in workflow.SHARDS.items() if target == path}
        if digest(canonical(generated)) != geometry['layout_sha256']:
            return False
        if path == CODEC_TARGET and (workflow.COHORTS != CODEC_COHORTS
                                     or geometry['cohorts_sha256'] != digest(canonical(workflow.COHORTS))):
            return False
        key = next(key for key, (target, _) in workflow.TARGETS.items() if target == path)
        spans = workflow.primary_spans(key, source)
        length = workflow.utf16_length(workflow.target_text(source))
        if not spans or spans[0][0] != 0 or spans[-1][1] != workflow.SPAN_END or spans[-1][0] >= length or any(
                lo >= hi or (i and spans[i - 1][1] != lo) for i, (lo, hi) in enumerate(spans)):
            return False
        regions = workflow.static_regions(workflow.target_text(source))
        return all(sum(lo <= a and b <= hi for lo, hi in spans) == 1 for a, b in regions)
    except (KeyError, TypeError, ValueError, StopIteration):
        return False


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
        provisional = record.get('geometry_status') == PROVISIONAL_STATUS
        if provisional:
            if not native_geometry_valid(path, record, source[path]):
                reasons.add('missing_measurement')
            if timing_hash != PROVISIONAL_TIMING_INPUTS_HASH:
                reasons.add('stale_inputs')
            source_hash, artifact, evidence_hash = PROVISIONAL_TARGETS[path]
            if record['source_sha256'] != source_hash:
                reasons.add('stale_inputs')
            generated = {name: digest(canonical(workflow.shard_config(name, source[path])))
                         for name, target in workflow.SHARDS.items() if target == path}
            if path == CODEC_TARGET and workflow.COHORTS != CODEC_COHORTS:
                reasons.add('layout_mismatch')
            if digest(canonical(generated)) != PROVISIONAL_LAYOUT_HASHES[path]:
                reasons.add('layout_mismatch')
            if not any(e['kind'] == 'partial_timing' and e['sha256'] == evidence_hash
                       and e['source_commit'] == 'e7b303f3d2bb7c6d54e7be804cc5bf8bf83aa2a5'
                       and artifact in e['locator'] for e in record['evidence']):
                reasons.add('missing_measurement')
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
            if provisional and all(item[k] is None for k in NUMERIC_FIELDS):
                if not item['basis'].strip():
                    reasons.add('invalid_estimate')
                continue
            if provisional and not (approved_codec_static_owner(path, record, shard, item, source[path])
                                    or approved_repeat_merged_owner(path, record, shard, item, source[path])):
                reasons.add('invalid_estimate')
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
            regions = accepted_static_regions(path, shard, source[path])
            if regions and item['isolated_seconds'] < sum(STATIC_EVIDENCE[region][3] for region in regions):
                reasons.add('invalid_estimate')
            codec_regions = codec_static_regions(path, shard, source[path])
            if codec_regions and shard in CODEC_ISOLATED and item['isolated_seconds'] < CODEC_ISOLATED[shard]:
                reasons.add('invalid_estimate')
            if (item['estimated_seconds'] > 1800
                    and not approved_static_owner(path, record, shard, item, source[path])):
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
            config = json.loads(Path(qualification).read_text(), parse_constant=lambda value: (_ for _ in ()).throw(ValueError('nonfinite JSON')))
            shape(config)
            numeric_errors = numeric_schema_errors(config)
            if numeric_errors:
                return {'status': 'rejected', 'reasons': sorted(numeric_errors)}
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
        if reasons:
            return {'status': 'rejected', 'reasons': reasons}
        pending = sorted(path for path, record in config['targets'].items()
                         if record.get('geometry_status') == PROVISIONAL_STATUS)
        if pending:
            return {'status': 'provisional', 'reasons': [], 'pending_targets': pending}
        return {'status': 'qualified', 'reasons': []}
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

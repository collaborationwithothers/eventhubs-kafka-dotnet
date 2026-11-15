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
import tempfile
import xml.etree.ElementTree as ET

import c5_workflow as workflow

REPO = ROOT
WORKFLOW = '.github/workflows/verifier-c5-mutation.yml'
RUNNER = 'tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py'
TEST_PROJECT = 'tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj'
HEX = re.compile(r'^[0-9a-f]{64}$')
NON_RENEWABLE = {RUNNER, WORKFLOW, 'tests/Parchwell.Verifier.Tests/Mutation/c5_projection.py',
                 'tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json', '.config/dotnet-tools.json'}
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
    'Common/Phase/B10FinalProjectionCodec.cs': '38b2a6d046381a2043a5b3502b242c832591ffaf415c10119ae2878234a8201e',
    'Judge/B10FinalJudge.cs': '0c197df0b6b6625ba5822425a08fac2b67436f8b83aa6744d5e9150d06c4a238',
    'VerifyRun/B10Final/B10FinalRepeatEvidence.cs': '844276e4b9f4d432c3ae298bdb0f3cf2ce42f4f41231ef781142518a2da473ee',
}
PROVISIONAL_TIMING_INPUTS_HASH = 'e15cd682a2731538bfde90aa333a9742bccb40e3db957f855d71a25a9e520463'
PROVISIONAL_STATUS = 'pending_native_report'
NUMERIC_FIELDS = ('main_mutants', 'main_work_seconds', 'parallelism', 'setup_seconds', 'isolated_seconds', 'estimated_seconds')


# Exact source-bound estimates and evidence for the six-tail batch.
SIX_TAIL_EVIDENCE = {'Judge/PhaseJudge.cs': (('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165 '
                          'artifact 14138923535',
                          '389cec813b0d324b4ffba490b5ea55d1cccd3bdd551f5e0fcf7222620187d03c',
                          '3c4df3d040f55356597f83f1cf460956adb49197'),
                         ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630810798',
                          '1e9329e34bdae6b2a2300797bd39ac7492d9af45bde4a70a5cfa5cf39f628d03',
                          '3c4df3d040f55356597f83f1cf460956adb49197'),
                         ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630811266',
                          '3e039e30be211496628da64570483e90202b66ff0c85de54056e895e82f05b58',
                          '3c4df3d040f55356597f83f1cf460956adb49197'),
                         ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630810948',
                          '9f3e7eb3a92de58caca63c5dbc484c2e47e063e63ed27ed78ccb6f577af5222a',
                          '3c4df3d040f55356597f83f1cf460956adb49197')),
 'Judge/B10FinalJudge.cs': (('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165 '
                             'artifact 14139008781',
                             'd0a8e484b1409ba7c7299eb62ab04077f71c40dc62765e89ff26409ad4acbb40',
                             '3c4df3d040f55356597f83f1cf460956adb49197'),
                            ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630822191',
                             '6a561ddbc53398d0972c75f3e71019aa497e17ee05b1bb3d49edd15591b0f455',
                             '3c4df3d040f55356597f83f1cf460956adb49197'),
                            ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630822187',
                             '15f719122b96c3888a3a6a258d42e7fc23d1a9f3fdb7361241ef53978db075c7',
                             '3c4df3d040f55356597f83f1cf460956adb49197'),
                            ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630822180',
                             'c3bd050aefe7b3a998431ef0f841c6a7445901c96b6d27a95ba3495824cbae19',
                             '3c4df3d040f55356597f83f1cf460956adb49197'),
                            ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630822192',
                             'bd2e53c44dd7582b3a8d81a19d5d6b4b96283afc96353240514f0c3902439034',
                             '3c4df3d040f55356597f83f1cf460956adb49197')),
 'VerifyRun/B10Final/B10FinalRepeatEvidence.cs': (('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165 '
                                                   'artifact 14138048823',
                                                   'c5291da1c660e87316e88c12342bb1d46ac2be373403e535e3df04c50ddfefdb',
                                                   '3c4df3d040f55356597f83f1cf460956adb49197'),
                                                  ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630817760',
                                                   '9ca53cc00fd4f42b89008cf2a374cb4606790c21ce4b0a6e956c2c2a64a096ee',
                                                   '3c4df3d040f55356597f83f1cf460956adb49197'),
                                                  ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630817798',
                                                   '2a278e14485f904b1f63cf018cb8c8353a0c2a6cd1c4d845a250adf9e1120865',
                                                   '3c4df3d040f55356597f83f1cf460956adb49197')),
 'Common/Phase/B10FinalProjectionCodec.cs': (('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165 '
                                              'artifact 14138414593',
                                              '9a08acea11d9c799899e0386e724cc808dff5df84769987bf085edc10e7828aa',
                                              '3c4df3d040f55356597f83f1cf460956adb49197'),
                                             ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165 '
                                              'artifact 14139166517',
                                              '76c8085f61c5211b0c18612d1f4db38cd102c380fb10ccb05f5339828941e241',
                                              '3c4df3d040f55356597f83f1cf460956adb49197'),
                                             ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630822052',
                                              'a62be57ee9d9d5440c08fe0295ba785b220e62e9d4b63b33502dbd3b6e17b6cd',
                                              '3c4df3d040f55356597f83f1cf460956adb49197'),
                                             ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630822076',
                                              'dbfb0b202d1a145c91c8abba309f0833231e275b6f264b8fbc110e0193d9c1f0',
                                              '3c4df3d040f55356597f83f1cf460956adb49197'),
                                             ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630822104',
                                              '19fcc55f6c7a33d77adfc37b67b2dccdb92a21d0b3b63f9975152d8fa6c9d830',
                                              '3c4df3d040f55356597f83f1cf460956adb49197')),
 'VerifyRun/Input/B10PhaseRegistrationReader.cs': (('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165 '
                                                    'artifact 14138599172',
                                                    'cd138c63b78d4e4a9f218c61e1e3326cd798cc7214125e9fa8be21c52db40702',
                                                    '3c4df3d040f55356597f83f1cf460956adb49197'),
                                                   ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630813451',
                                                    'e1d3a5322cdf451b20f1f1ce95c7c3439d7110ea9f7f6147485ae4f30bb4b9de',
                                                    '3c4df3d040f55356597f83f1cf460956adb49197')),
 'VerifyRun/B10Final/B10FinalClassification.cs': (('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43642679359 '
                                                   'artifact 14132628130',
                                                   'd9c0963089d092792a7381ac35c89154f522ab9a704d6c6a1758a6e973d9905f',
                                                   'ed79ae741a082f1cc55d753bb64dec5a74d5a49c'),
                                                  ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43642679359 '
                                                   'artifact 14132628130',
                                                   'd9c0963089d092792a7381ac35c89154f522ab9a704d6c6a1758a6e973d9905f',
                                                   'ed79ae741a082f1cc55d753bb64dec5a74d5a49c'),
                                                  ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630817843',
                                                   'a08a59167a86ffac8fc97a29fc2b6418f2afd5b3c502ae6b723c42d66c7eb65e',
                                                   '3c4df3d040f55356597f83f1cf460956adb49197'),
                                                  ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630817802',
                                                   '5b41f9f2742724704cdfce5d77bd68076b066665e1d57f39da00547834bdb6bd',
                                                   '3c4df3d040f55356597f83f1cf460956adb49197'),
                                                  ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630817823',
                                                   '132ec6e1a39ed2f1eb509426f6bad16f9a1464598df06c6bbf2184864979e12e',
                                                   '3c4df3d040f55356597f83f1cf460956adb49197'),
                                                  ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630817818',
                                                   '0a9b94e3f674599de1c0106896a90e201ee7eb3a540091845cf9e11d82b54bc7',
                                                   '3c4df3d040f55356597f83f1cf460956adb49197'),
                                                  ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630822035',
                                                   'c634f2873141dfdd4aaa484727fabdbcfb952f0dd63759a1ddfb5420f01d0a38',
                                                   '3c4df3d040f55356597f83f1cf460956adb49197'),
                                                  ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630822030',
                                                   '55681cf9eafee605f8c62984876e8c85ae63afeb60131fb8e9d1b9b412a04e0c',
                                                   '3c4df3d040f55356597f83f1cf460956adb49197'),
                                                  ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630822007',
                                                   '0267f4d32392987b7f36888ff1421f9812aa84e6c0ff5e267beef09f4b2074ff',
                                                   '3c4df3d040f55356597f83f1cf460956adb49197'),
                                                  ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630822073',
                                                   '2e9717318de2debdc21af404292220b6b517cac56d22e0b693f04a96c1ec7b5d',
                                                   '3c4df3d040f55356597f83f1cf460956adb49197'),
                                                  ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630822009',
                                                   'e0efa060d82972f92a45e8a2382e9469ee306fbbdf7a7f51645f939de08255c9',
                                                   '3c4df3d040f55356597f83f1cf460956adb49197')),
 'Common/Phase/B10FinalRegistrationReader.cs': (('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165 '
                                                 'artifact 14137399692',
                                                 '1c0c4967a8da432d8cb40a291aa6c0ca26e6b951c761941a121aa1ad8805d3f3',
                                                 '3c4df3d040f55356597f83f1cf460956adb49197'),
                                                ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630822078',
                                                 '0b60306d3a8b9b224047ca1c3270ce3fb61bdd0a127a49ea6cad4942028d3f47',
                                                 '3c4df3d040f55356597f83f1cf460956adb49197'),
                                                ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630822178',
                                                 '6c3ecc86cf6ef2c5eee1f187cd752f6f345d5164c630cfe756555afb9fd879de',
                                                 '3c4df3d040f55356597f83f1cf460956adb49197'),
                                                ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43654777165/job/132630822102',
                                                 'd57b950ce8c6c3fcfd4da096bec28c3db169e66929ac28ada2b65613889f9cc0',
                                                 '3c4df3d040f55356597f83f1cf460956adb49197'))}
SIX_TAIL_ESTIMATES = {'phase-p5': ('Judge/PhaseJudge.cs',
              '22c56f360af0ccab6123c471d7c092d588ca7547323a5415f606bee0368aa2f9',
              'b426dd705e84c23731afb72ad17cd0d3ee5835e3e719546e9b8853077c32340d'),
 'phase-p6': ('Judge/PhaseJudge.cs',
              '22c56f360af0ccab6123c471d7c092d588ca7547323a5415f606bee0368aa2f9',
              'ea29c6b9d02782598b76b1e3f4da3a5ccafe5e37debc55e37f69b6b6657a0d53'),
 'phase-crossing': ('Judge/PhaseJudge.cs',
                    '22c56f360af0ccab6123c471d7c092d588ca7547323a5415f606bee0368aa2f9',
                    '23935e63ee29d7438939b7c8990966cd2602f1aff032221ce07f7da0311380f5'),
 'b10-final-judge-p9': ('Judge/B10FinalJudge.cs',
                        'cd88f482f9c93947e613ec4b13a34ff554331dfe8ebd9a3e400ed3bbb360d34a',
                        'a0284b41a27b2f6bf7dbb8eedcf433eedb1312be9718b1507a80f6f171873de2'),
 'b10-final-judge-p10': ('Judge/B10FinalJudge.cs',
                         'cd88f482f9c93947e613ec4b13a34ff554331dfe8ebd9a3e400ed3bbb360d34a',
                         '0bd9cca2a5b9ea76c66f5193c488ad21f1804c7fb8746d7bbc90bcf506571cf4'),
 'b10-final-judge-p11': ('Judge/B10FinalJudge.cs',
                         'cd88f482f9c93947e613ec4b13a34ff554331dfe8ebd9a3e400ed3bbb360d34a',
                         '80974d326c841b1e06542560b44414a331c905fa60e5129139be2d47152babba'),
 'b10-final-judge-crossing': ('Judge/B10FinalJudge.cs',
                              'cd88f482f9c93947e613ec4b13a34ff554331dfe8ebd9a3e400ed3bbb360d34a',
                              '9636705d357d7e5eb6d8dc89b9c9aba5027dee1640158dc39fc3c399a64e1ff4'),
 'b10-final-repeat-evidence-p7': ('VerifyRun/B10Final/B10FinalRepeatEvidence.cs',
                                  '4e533dd7aa4205e0a5a121b94b9441af7438e5d878600a3b65dc6ad5f4edab68',
                                  '907ba1207533c228a3acc8256811b906682b2e1ede64ad7d5cf34aeb1e0a4585'),
 'b10-final-repeat-evidence-p8': ('VerifyRun/B10Final/B10FinalRepeatEvidence.cs',
                                  '4e533dd7aa4205e0a5a121b94b9441af7438e5d878600a3b65dc6ad5f4edab68',
                                  'b12f8961df3f582e87e31ed56f4bbb06b166f8bd77a9d23d03c6e6d99547d0f9'),
 'b10-final-repeat-evidence-crossing': ('VerifyRun/B10Final/B10FinalRepeatEvidence.cs',
                                        '4e533dd7aa4205e0a5a121b94b9441af7438e5d878600a3b65dc6ad5f4edab68',
                                        '4bd01a70d54ec4d134c5861cce43448267f796cac7cb6a6764c78eac9a378ed4'),
 'b10-final-projection-codec-p8': ('Common/Phase/B10FinalProjectionCodec.cs',
                                   'c92b0d32b3246a41ef4fc3b3f4607b3397161782c99c03d5774a1def895a1dc5',
                                   '9da4337e719518f23e3e56634867169b02ca97459e4b1ff715eed4fa2feaff6c'),
 'b10-final-projection-codec-p9': ('Common/Phase/B10FinalProjectionCodec.cs',
                                   'c92b0d32b3246a41ef4fc3b3f4607b3397161782c99c03d5774a1def895a1dc5',
                                   '30788b2d0094ecf8406b35af2a728f25e0bf470a5a84843f2262111f75a58b3d'),
 'b10-final-projection-codec-p11': ('Common/Phase/B10FinalProjectionCodec.cs',
                                    'c92b0d32b3246a41ef4fc3b3f4607b3397161782c99c03d5774a1def895a1dc5',
                                    '1e2fa28e3e4f026a2615fdf5a4489848871fa0e6e012e5004f89564acbbec06f'),
 'b10-final-projection-codec-p12': ('Common/Phase/B10FinalProjectionCodec.cs',
                                    'c92b0d32b3246a41ef4fc3b3f4607b3397161782c99c03d5774a1def895a1dc5',
                                    '7c6978d1471ed3360bce59ae4e328d53e87969ebb04f3a3684019902383192ab'),
 'b10-final-projection-codec-crossing': ('Common/Phase/B10FinalProjectionCodec.cs',
                                         'c92b0d32b3246a41ef4fc3b3f4607b3397161782c99c03d5774a1def895a1dc5',
                                         '5a02e4c75b9bfa4c0396726dd4e37b6ffdf5609c86e37f2c0549addf7e88e1e1'),
 'b10-phase-registrations-p1': ('VerifyRun/Input/B10PhaseRegistrationReader.cs',
                                'e73faee335b336fb7d06590fa6d0c7f22f0c188dbcce3495b673c839fbe1c5c0',
                                'f2c1d80f5c5914f19133b559ed76a7a4c9e3c3d7a67c21967791af0d824d4bcf'),
 'b10-phase-registrations-p2': ('VerifyRun/Input/B10PhaseRegistrationReader.cs',
                                'e73faee335b336fb7d06590fa6d0c7f22f0c188dbcce3495b673c839fbe1c5c0',
                                'cfe7d500d14703552891f88d5c3a041f9017694803f70680673ec0d0de3a7ec0'),
 'b10-phase-registrations-crossing': ('VerifyRun/Input/B10PhaseRegistrationReader.cs',
                                      'e73faee335b336fb7d06590fa6d0c7f22f0c188dbcce3495b673c839fbe1c5c0',
                                      '383f01545b1da8302edab349afe42f86b6788aeb96b6072e42ab61fe375b9f1f'),
 'b10-final-classification-p2': ('VerifyRun/B10Final/B10FinalClassification.cs',
                                 'ad710e8960395058a357a2a7e817cc2bc450524c0a82e3e97f73d9cef0f52957',
                                 '89c3213ef9464b72cd5e0df93b073f7c8bd37b3aaeefb772d3efe3d769966df7'),
 'b10-final-classification-p3': ('VerifyRun/B10Final/B10FinalClassification.cs',
                                 'ad710e8960395058a357a2a7e817cc2bc450524c0a82e3e97f73d9cef0f52957',
                                 '83e5b4ae448ee6be1888445505fe08e072e6875fb4ef17da034899c4b0a2f3ed'),
 'b10-final-classification-p20': ('VerifyRun/B10Final/B10FinalClassification.cs',
                                  'ad710e8960395058a357a2a7e817cc2bc450524c0a82e3e97f73d9cef0f52957',
                                  '9fee1914b1224dfdda749ed829d508ac1cbf268b5d1fa7e3f15945a399306018'),
 'b10-final-classification-p22': ('VerifyRun/B10Final/B10FinalClassification.cs',
                                  'ad710e8960395058a357a2a7e817cc2bc450524c0a82e3e97f73d9cef0f52957',
                                  'd50e303f28b1dc3cc4e5a5dc0131a24349194307ffd44b2f06449e18ff6d00d7'),
 'b10-final-registrations-p1': ('Common/Phase/B10FinalRegistrationReader.cs',
                                '7a28b10054c4620b8a84f0513244da2b96b89fbe748a157eb491bfcce6d755ba',
                                '8df19b04965238a04803b689993149fa917c769794289b33693e6aa0bedbe129')}
# These four owners use the later, same-run codec neighborhood evidence.
CODEC_NEIGHBOR = {
    'b10-final-projection-codec-p8': ((7493, 8585), (38, 2384, 2, 253.5, 210, 1656)),
    'b10-final-projection-codec-p9': ((8585, 9410), (40, 2861, 2, 253, 0, 1684)),
    'b10-final-projection-codec-p10': ((9410, 10482), (40, 2891, 2, 238.5, 0, 1684)),
    'b10-final-projection-codec-crossing': (None, (20, 281, 1, 394, 0, 675)),
}
CODEC_NEIGHBOR_EVIDENCE = (
    ('14142136390', '795083ceb93c9486e0f1044ffd278f656f2584d525689a3e59c82b93dfeeb22f'),
    ('14141193428', 'e1fb439e4798090794410baf0300058c05e020e878548dc760f49279e8be8539'),
    ('14142551423', 'a51445f3ceb325bcd9de1d80768076ff071bb3418ac86076c108ecc814f5cda1'),
)
for _shard in CODEC_NEIGHBOR:
    SIX_TAIL_ESTIMATES.pop(_shard, None)


def approved_codec_neighbor(path, record, shard, item, source):
    try:
        span, numbers = CODEC_NEIGHBOR[shard]
        return (path == CODEC_TARGET and digest(source) == PROVISIONAL_TARGETS[path][0]
                and item['timing_inputs_sha256'] == PROVISIONAL_TIMING_INPUTS_HASH
                and tuple(item[k] for k in NUMERIC_FIELDS) == numbers
                and (span is None or workflow.shard_span(shard, source) == span)
                and all(any(e['kind'] == 'hosted_complete'
                            and e['source_commit'] == '336f4e53f3f8329fae297c7438eef3e86df40120'
                            and e['locator'] == 'https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43663685095 artifact ' + artifact
                            and e['sha256'] == sha for e in record['evidence'])
                        for artifact, sha in CODEC_NEIGHBOR_EVIDENCE))
    except (KeyError, TypeError, ValueError):
        return False


SIX_TAIL_BINDINGS = {shard: (path, source, estimate, SIX_TAIL_EVIDENCE[path])
                     for shard, (path, source, estimate) in SIX_TAIL_ESTIMATES.items()}

def approved_six_tail(path, record, shard, item, source):
    try:
        target, source_hash, estimate_hash, evidence = SIX_TAIL_BINDINGS[shard]
        return (path == target and digest(source) == source_hash
                and digest(canonical(item)) == estimate_hash
                and all(any(e['kind'] == 'hosted_complete' and (e['locator'], e['sha256'], e['source_commit']) == required
                            for e in record['evidence']) for required in evidence))
    except (KeyError, TypeError, ValueError):
        return False

def provenance_shape(contexts):
    """Validate the complete measured maps and explicit current-input assessments."""
    if not isinstance(contexts, dict) or not contexts:
        raise ValueError('missing timing provenance')
    for binding, context in contexts.items():
        if not isinstance(binding, str) or not HEX.fullmatch(binding) or not isinstance(context, dict) or set(context) != {'shared_inputs', 'target_sources', 'assessments'}:
            raise ValueError('invalid timing provenance')
        for key in ('shared_inputs', 'target_sources'):
            mapping = context[key]
            if not isinstance(mapping, dict) or any(not isinstance(p, str) or not p or not isinstance(h, str) or not HEX.fullmatch(h) for p, h in mapping.items()):
                raise ValueError('invalid measured inputs')
        assessments = context['assessments']
        if not isinstance(assessments, dict) or set(assessments) != {'shared_inputs', 'target_sources'}:
            raise ValueError('invalid assessments')
        for changes in assessments.values():
            if not isinstance(changes, dict):
                raise ValueError('invalid assessments')
            for path, item in changes.items():
                if not isinstance(path, str) or not path or not isinstance(item, dict) or set(item) != {'current_sha256', 'basis'}:
                    raise ValueError('invalid assessment')
                current = item['current_sha256']
                if current is not None and (not isinstance(current, str) or not HEX.fullmatch(current)):
                    raise ValueError('invalid assessment hash')
                if not isinstance(item['basis'], list) or not item['basis'] or any(not isinstance(b, str) or not b.strip() for b in item['basis']):
                    raise ValueError('invalid assessment basis')
        measured = {k: context[k] for k in ('shared_inputs', 'target_sources')}
        if digest(canonical(measured)) != binding:
            raise ValueError('timing provenance hash mismatch')


def evidence_matches_target(path, record):
    """At least one timing or geometry record must name a commit containing this source."""
    for evidence in record['evidence']:
        commit = evidence['source_commit']
        if evidence['kind'] not in {'hosted_complete', 'local_probe', 'partial_timing'} or not re.fullmatch(r'[0-9a-f]{40}', commit):
            continue
        try:
            if digest(blob(commit, 'src/Parchwell.Verifier/' + path)) == record['source_sha256']:
                return True
        except subprocess.CalledProcessError:
            continue
    return False


def provenance_errors(context, shared, targets, owner):
    """Account for every difference without renewing the estimate's own target."""
    errors = set()
    current_maps = {'shared_inputs': shared, 'target_sources': targets}
    if context['target_sources'].get(owner) != targets.get(owner):
        errors.add('stale_inputs')
    for key, current in current_maps.items():
        measured = context[key]
        changes = context['assessments'][key]
        expected = {p for p in measured.keys() | current.keys() if measured.get(p) != current.get(p)}
        if set(changes) != expected:
            errors.add('unaccounted_inputs')
        for path, item in changes.items():
            if path not in expected or item['current_sha256'] != current.get(path):
                errors.add('unaccounted_inputs')
            if key == 'shared_inputs' and path in NON_RENEWABLE:
                errors.add('invalid_renewal')
            if key == 'target_sources' and path == owner:
                errors.add('invalid_renewal')
    return errors


def shape(config):
    if not isinstance(config, dict) or set(config) not in ({'schema_version', 'budget_seconds', 'shared_inputs', 'targets'},
                                                 {'schema_version', 'budget_seconds', 'shared_inputs', 'targets', 'renewals'},
                                                 {'schema_version', 'budget_seconds', 'shared_inputs', 'targets', 'timing_inputs'}):
        raise ValueError('invalid schema')
    if type(config['schema_version']) is not int or config['schema_version'] not in (1, 2) or type(config['budget_seconds']) is not int or config['budget_seconds'] != 1800:
        raise ValueError('unsupported schema or budget')
    if not isinstance(config['shared_inputs'], dict) or not isinstance(config['targets'], dict):
        raise ValueError('invalid maps')
    for path, sha in config['shared_inputs'].items():
        if not isinstance(path, str) or not isinstance(sha, str) or not HEX.fullmatch(sha):
            raise ValueError('invalid input hash')
    version = config['schema_version']
    if (version == 1 and 'timing_inputs' in config) or (version == 2 and ('timing_inputs' not in config or 'renewals' in config)):
        raise ValueError('invalid version fields')
    if version == 2:
        provenance_shape(config['timing_inputs'])
    renewals = config.get('renewals', {})
    if not isinstance(renewals, dict):
        raise ValueError('invalid renewals')
    for path, renewal in renewals.items():
        if not isinstance(path, str) or not isinstance(renewal, dict) or set(renewal) != {'measured_sha256', 'current_sha256', 'basis'}:
            raise ValueError('invalid renewal')
        if any(value is not None and (not isinstance(value, str) or not HEX.fullmatch(value))
               for value in (renewal['measured_sha256'], renewal['current_sha256'])):
            raise ValueError('invalid renewal hash')
        basis = renewal['basis']
        if not isinstance(basis, list) or not basis or any(not isinstance(item, str) or not item.strip() for item in basis):
            raise ValueError('invalid renewal basis')
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
                if ((path == CODEC_TARGET and shard in CODEC_PROVISIONAL_SHARDS) or (path == REPEAT_TARGET and shard in REPEAT_BOUNDS) or shard in TAIL_SPLITS or shard in SIX_TAIL_BINDINGS or shard in CODEC_NEIGHBOR) and any(item[k] is None for k in NUMERIC_FIELDS):
                    raise ValueError('pinned owner requires its estimate')
                if any(item[k] is not None for k in NUMERIC_FIELDS) and not (
                    ((path == CODEC_TARGET and shard in CODEC_PROVISIONAL_SHARDS) or (path == REPEAT_TARGET and shard in REPEAT_BOUNDS) or shard in TAIL_SPLITS or shard in SIX_TAIL_BINDINGS or shard in CODEC_NEIGHBOR)
                    and all(type(item[k]) in (int, float) for k in NUMERIC_FIELDS)):
                    raise ValueError('provisional estimates must be unknown except for pinned owners')
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
# Whole-job bounds conservatively charge promoted/crossing identities at 107s.
# Counts for merged and crossing shards bound all native identities, not tested counts.
REPEAT_BOUNDS = {
    'b10-final-repeat-evidence-p1': ((0, 5565), (44, 1255, 1, 107, 0, 1362)),
    'b10-final-repeat-evidence-p2': ((5565, 13837), (167, 961, 1, 107, 0, 1068)),
    'b10-final-repeat-evidence-p22': ((41582, 42920), (41, 2957, 2, 229, 0, 1708)),
    'b10-final-repeat-evidence-p23': ((42920, 44372), (39, 2753, 2, 229, 0, 1606)),
}

REPEAT_BOUND_JOBS = {
    'b10-final-repeat-evidence-p1': (('132582438060', '5f0524883832ef096c14da1c8bd6a86ba0a86e7c8bcb14a28b7c0e4972ab70ca'), ('132582438086', '54d604bdc8ab6abd1fb463aae881875ad02196a8221a70341bcbb62282a4638a')),
    'b10-final-repeat-evidence-p2': (('132582438127', '1b5893a9410de6d5203058a98173d9031db407c2b9c6b5e191786f2ec8542dae'), ('132582438524', 'ebc28d4ca57b5cf4ab5d855ce9c662962eb78d3de1f7c0584cd97ff5d5c2c89b')),
    'b10-final-repeat-evidence-p22': (('132582438152', '3fdf2cae2513f6a62151d65de56bbb3639bbe5437db4168305b8ac4268fc7726'),),
    'b10-final-repeat-evidence-p23': (('132582438152', '3fdf2cae2513f6a62151d65de56bbb3639bbe5437db4168305b8ac4268fc7726'),),
}


def approved_repeat_merged_owner(path, record, shard, item, source):
    if (path != REPEAT_TARGET or shard not in REPEAT_BOUNDS
            or digest(source) != PROVISIONAL_TARGETS[REPEAT_TARGET][0]
            or item['timing_inputs_sha256'] != PROVISIONAL_TIMING_INPUTS_HASH
            or tuple(item[k] for k in NUMERIC_FIELDS) != REPEAT_BOUNDS[shard][1]):
        return False
    span = REPEAT_BOUNDS[shard][0]
    if span is not None and workflow.shard_span(shard, source) != span:
        return False
    jobs_ok = all(any(e['kind'] == 'hosted_complete' and e['source_commit'] == '4aa6218f9c0750f80b072e0bab1e26e754de8f94'
                      and e['locator'] == 'https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43639113318/job/' + job
                      and e['sha256'] == sha for e in record['evidence']) for job, sha in REPEAT_BOUND_JOBS[shard])
    return jobs_ok and any(e['kind'] == 'hosted_complete'
               and e['source_commit'] == '4aa6218f9c0750f80b072e0bab1e26e754de8f94'
               and e['locator'] == 'https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43639113318 artifact 14132774324'
               and e['sha256'] == '68c86893d721ecc2c06ee043e68a5b7259420c1fed755186926d42d883b6b060'
               for e in record['evidence'])


# Source-bound split estimates use measured tested work and job overhead.
TAIL_SPLITS = {
    'b10-final-repeat-evidence-p10': (REPEAT_TARGET, (24938, 26076), (38, 1951, 2, 183.5, 0, 1159), '14134281282', '2bad7544a96c4c7ff6b84b976667c3f11be18ad77e81febec9c6c9f0b9793d6b'),
    'b10-final-repeat-evidence-p11': (REPEAT_TARGET, (26076, 27770), (42, 2096, 2, 183.5, 0, 1232), '14134281282', '2bad7544a96c4c7ff6b84b976667c3f11be18ad77e81febec9c6c9f0b9793d6b'),
    'b10-final-projection-codec-p13': (CODEC_TARGET, (11973, 12732), (39, 2799, 2, 197, 0, 1597), '14133942300', '8cc93fd4ffb21dd8afcc793d8c30906d5cb6f9c79bb43d0947c6c0e0229ce427'),
    'b10-final-projection-codec-p14': (CODEC_TARGET, (12732, 13486), (32, 2279, 2, 197, 0, 1337), '14133942300', '8cc93fd4ffb21dd8afcc793d8c30906d5cb6f9c79bb43d0947c6c0e0229ce427'),
}

def approved_tail_split(path, record, shard, item, source):
    try:
        target, span, numbers, artifact, sha = TAIL_SPLITS[shard]
        return (path == target and digest(source) == PROVISIONAL_TARGETS[path][0]
                and workflow.shard_span(shard, source) == span
                and tuple(item[k] for k in NUMERIC_FIELDS) == numbers
                and any(e['kind'] == 'hosted_complete' and e['source_commit'] == 'ed79ae741a082f1cc55d753bb64dec5a74d5a49c'
                        and e['locator'] == 'https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43642679359 artifact ' + artifact
                        and e['sha256'] == sha for e in record['evidence']))
    except (KeyError, ValueError, TypeError):
        return False


# These complete native inventories are taken from receipt-verified hosted reports.
NATIVE_GEOMETRY_BINDINGS = {
    'VerifyRun/B10Final/B10FinalRepeatEvidence.cs': ('14127456699', '3268e1a3b6c52c469e74f6b5680aa40d2096a1901780c8c0d6416c394ed88d90', 'c9ac4182741b1f87d383ad950806a069d786e4cf8dfc55c5f0a17641f47922a3', '2bc0f8acfd79364d414b7cd46dde2804c930adc2a1fbbb9f6ad05d67a79d298a', 1306),
    'Common/Phase/B10FinalProjectionCodec.cs': ('14127755542', '017a78d3c863b6ca3c8cb5a2e2db5c7a0bf95988612a7e603e68e7259489477d', 'ab0e4d0677136a516821fff731e36d439ff57e40857c8e77bdda7e4c5183c37a', 'd5a7f544377a3d8b2c66e120e92efcee3f0a07ecabe5b45f36a40f2334251b8f', 730),
    'Judge/B10FinalJudge.cs': ('14127550132', '5daf8d8f214957e7e8f6a91db1d2093fa3c0cf0827267a07ee77da9ef14ab119', 'e9ec6237585270a6ab9e2dea44998171e276656fab9da65b2aea3a96bd19cf93', 'b57b1426e4cd0f4a5a21e06aa665f7bedf5568fcb62bc5beffe1e2885a5c9d40', 710),
}


# Digests of the reviewed compact proofs; the full inventories remain in hosted artifacts.
NATIVE_PROOF_HASHES = {
    'VerifyRun/B10Final/B10FinalRepeatEvidence.cs': '568aa15ae5e1d03f26d08ec3bf1d433cd4a494e99dfb9cd972f570a56a0362d3',
    'Common/Phase/B10FinalProjectionCodec.cs': 'dac202d79ce5272b8c91b8207d0172135de14f83dc24e25db7fc2b3b0c765f80',
    'Judge/B10FinalJudge.cs': 'e829a0e8e7f17fc51b6ac648955287f6ef6c52014982a9849ceac75d6e80c1ae',
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
        if path in (REPEAT_TARGET, CODEC_TARGET):
            artifact_id = geometry['ownership_artifact_id']
            archive_hash = geometry['ownership_archive_sha256']
            if not any(e['kind'] == 'hosted_complete' and e['source_commit'] == 'ed79ae741a082f1cc55d753bb64dec5a74d5a49c'
                       and e['locator'] == 'https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43642679359 artifact ' + artifact_id
                       and e['sha256'] == archive_hash for e in record['evidence']):
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
    measured = config['shared_inputs'].copy()
    for path, renewal in config.get('renewals', {}).items():
        old, current = renewal['measured_sha256'], renewal['current_sha256']
        if (path in NON_RENEWABLE or path in {'src/Parchwell.Verifier/' + p for p in config['targets']}
                or old == current or current != config['shared_inputs'].get(path)):
            reasons.add('invalid_renewal')
        if old is None:
            measured.pop(path, None)
        else:
            measured[path] = old
    timing_hash = digest(canonical({'shared_inputs': measured, 'target_sources': target_sources}))
    all_shards = {}
    used_contexts = set()
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
            if config['schema_version'] == 1 and timing_hash != PROVISIONAL_TIMING_INPUTS_HASH:
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
        elif config['schema_version'] == 2 and not evidence_matches_target(path, record):
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
            binding = item['timing_inputs_sha256']
            if config['schema_version'] == 1:
                if binding != timing_hash:
                    reasons.add('stale_inputs')
            else:
                context = config['timing_inputs'].get(binding)
                if context is None:
                    reasons.add('missing_measurement')
                else:
                    used_contexts.add(binding)
                    reasons.update(provenance_errors(context, shared, targets, path))
                if provisional and binding != PROVISIONAL_TIMING_INPUTS_HASH:
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
                                    or approved_repeat_merged_owner(path, record, shard, item, source[path])
                                    or approved_tail_split(path, record, shard, item, source[path])
                                    or approved_codec_neighbor(path, record, shard, item, source[path])
                                    or approved_six_tail(path, record, shard, item, source[path])):
                reasons.add('invalid_estimate')
            if shard in CODEC_NEIGHBOR and not approved_codec_neighbor(path, record, shard, item, source[path]):
                reasons.add('invalid_estimate')
            if shard in SIX_TAIL_BINDINGS and not approved_six_tail(path, record, shard, item, source[path]):
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
    if config['schema_version'] == 2 and used_contexts != set(config['timing_inputs']):
        reasons.add('unaccounted_inputs')
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
        result = {'status': 'provisional', 'reasons': [], 'pending_targets': pending} if pending else {'status': 'qualified', 'reasons': []}
        if config.get('renewals'):
            result['renewed_inputs'] = sorted(config['renewals'])
        if config['schema_version'] == 2:
            assessed = {f'{kind}:{path}' for context in config['timing_inputs'].values()
                        for kind, changes in context['assessments'].items() for path in changes}
            if assessed:
                result['assessed_inputs'] = sorted(assessed)
        return result
    except (ValueError, TypeError, KeyError, OSError, subprocess.CalledProcessError, ET.ParseError, SyntaxError, UnicodeError, OverflowError):
        return {'status': 'rejected', 'reasons': ['invalid_input']}



def renew(head, qualification, basis):
    if not basis.strip():
        raise ValueError('basis must not be empty')
    head = git('rev-parse', '--verify', head + '^{commit}').decode().strip()
    if Path(workflow.__file__).read_bytes() != blob(head, RUNNER):
        raise ValueError('checker is not running the requested revision')
    path = Path(qualification)
    config = json.loads(path.read_text(), parse_constant=lambda value: (_ for _ in ()).throw(ValueError('nonfinite JSON')))
    shape(config)
    if config['schema_version'] != 1:
        raise ValueError('renew supports schema version 1 only; assess version 2 provenance explicitly')
    shared, targets = input_maps(head)
    if set(config['targets']) != set(targets) or any(config['targets'][p]['source_sha256'] != targets[p] for p in targets):
        raise ValueError('target sources changed')
    old = config['shared_inputs']
    changed = sorted(p for p in old.keys() | shared.keys() if old.get(p) != shared.get(p))
    blocked = sorted(set(changed) & NON_RENEWABLE)
    if blocked:
        raise ValueError('not renewable: ' + ', '.join(blocked))
    if not changed:
        return {'renewed': [], 'status': 'unchanged'}
    renewals = config.get('renewals', {})
    for p in changed:
        if p not in renewals:
            renewals[p] = {'measured_sha256': old.get(p), 'current_sha256': shared.get(p), 'basis': [basis]}
        else:
            renewal = renewals[p]
            renewal['current_sha256'] = shared.get(p)
            renewal['basis'].append(basis)
            if renewal['current_sha256'] == renewal['measured_sha256']:
                del renewals[p]
    config['shared_inputs'] = shared
    if renewals:
        config['renewals'] = renewals
    else:
        config.pop('renewals', None)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(mode='w', dir=path.parent, encoding='utf-8', delete=False) as output:
            temporary = Path(output.name)
            output.write(json.dumps(config, indent=2, sort_keys=True) + '\n')
        os.replace(temporary, path)
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)
    return {'renewed': changed, 'status': 'renewed'}


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
    r = commands.add_parser('renew')
    r.add_argument('--head', required=True)
    r.add_argument('--qualification', required=True)
    r.add_argument('--basis', required=True)
    perf = commands.add_parser('performance')
    perf.add_argument('--run-json', required=True)
    perf.add_argument('--jobs-json', required=True)
    perf.add_argument('--output-dir', required=True)
    args = parser.parse_args()
    if args.command == 'check':
        result = check(args.base, args.head, args.qualification)
        print(json.dumps(result, sort_keys=True))
        return int(result['status'] == 'rejected')
    if args.command == 'renew':
        try:
            print(json.dumps(renew(args.head, args.qualification, args.basis), sort_keys=True))
            return 0
        except (ValueError, TypeError, KeyError, OSError, subprocess.CalledProcessError, ET.ParseError, SyntaxError, UnicodeError, OverflowError) as error:
            print(str(error), file=sys.stderr)
            return 1
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

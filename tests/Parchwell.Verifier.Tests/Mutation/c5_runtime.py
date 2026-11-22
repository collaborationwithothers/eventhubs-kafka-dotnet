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
import shutil
import signal
import subprocess
import tempfile
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
    """Read a historical single-run matrix from the measured revision."""
    if not isinstance(head, str) or not re.fullmatch(r'[0-9a-f]{40}', head):
        raise ValueError('invalid head SHA')
    try:
        companion = subprocess.check_output(
            ['git', 'ls-tree', '--name-only', head, '--', COMPANION_WORKFLOW],
            cwd=repository, text=True, stderr=subprocess.DEVNULL)
        if companion:
            raise ValueError('use c5_campaign.py terminal for coordinated C5 runs')
        text = subprocess.check_output(
            ['git', 'show', f'{head}:{WORKFLOW}'],
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
              'head_sha': run.get('head_sha'), 'budget_seconds': 3000,
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
            report['status'] = 'met' if report['workflow_elapsed_seconds'] <= 3000 else 'missed'
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
        result = {'run_id': None, 'run_attempt': None, 'head_sha': None, 'budget_seconds': 3000,
                  'workflow_elapsed_seconds': None, 'mutation_job_seconds': None,
                  'peak_mutation_jobs': None, 'queue_seconds': {}, 'status': 'incomplete',
                  'reasons': ['invalid_input']}
    destination = Path(output_dir)
    destination.mkdir(parents=True, exist_ok=True)
    (destination / 'performance.json').write_text(json.dumps(result, indent=2, sort_keys=True) + '\n')
    return 0 if result['status'] == 'met' else 1


"""Offline runtime qualification checks for the verifier mutation layout."""

import ast
import functools
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
COMPANION_WORKFLOW = '.github/workflows/verifier-c5-mutation-companion.yml'
CAMPAIGN_LIMITS = {WORKFLOW: 178, COMPANION_WORKFLOW: 146}
RUNNER = 'tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py'
CAMPAIGN_VALIDATOR = 'tests/Parchwell.Verifier.Tests/Mutation/c5_campaign.py'
TEST_PROJECT = 'tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj'
HEX = re.compile(r'^[0-9a-f]{64}$')
PROJECTION = 'tests/Parchwell.Verifier.Tests/Mutation/c5_projection.py'
PROJECTION_TIMING_PAIR = ('b1c9a96740111676caa0b9e95db90b4ff93f07ff2317710233558412a88ac1bc',
                          '3771b676b8d5856eb34705dcaa3e9c34ffd8184dd514179a1bf85ee7535fb16f')
RUNNER_TIMING_PAIR = ('dcf4ac956fe262a3178d0673071811fef63bf1a357ae42df7ea14629a0af4625',
                      'c147bb8000ef1273609052660e80b20c8da8bf33a77a0df65a94d32a5f7a1c40')
NON_RENEWABLE = {RUNNER, WORKFLOW, COMPANION_WORKFLOW, CAMPAIGN_VALIDATOR, PROJECTION,
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
    worker, aggregate_header, aggregate = (text[:start] + text[end:]).partition('\n  aggregate:\n')
    # Preserve the timing fingerprint for the exact reviewed companion transport changes.
    step = ("      - name: Download companion shard evidence\n"
            "        if: always() && steps.campaign.outputs.run-id != ''\n")
    action = step + '        uses: actions/download-artifact@'
    upgraded = action + '9000827ccba6bdab643e8b6fd33ac0654aef8333 # v8.0.2\n'
    measured = action + 'd3f86a106a0bac45b974a628896c90dbdf5c8093\n'
    inputs = (
        '        with:\n'
        '          github-token: ${{ github.token }}\n'
        '          run-id: ${{ steps.campaign.outputs.run-id }}\n'
        '          pattern: c5-mutation-${{ steps.campaign.outputs.run-id }}-${{ steps.campaign.outputs.attempt }}-*\n'
        '          path: ${{ runner.temp }}/c5-input\n'
        '          merge-multiple: false\n'
    )
    paced = step + (
        '        env:\n'
        '          GH_TOKEN: ${{ github.token }}\n'
        '          C5_CAMPAIGN: ${{ runner.temp }}/c5-campaign.json\n'
        '          C5_INPUT: ${{ runner.temp }}/c5-input\n'
        '        run: >-\n'
        '          python3 tests/Parchwell.Verifier.Tests/Mutation/c5_companion_artifacts.py\n'
        '          --repository "$GITHUB_REPOSITORY" --commit "$GITHUB_SHA"\n'
        '          --manifest "$C5_CAMPAIGN" --output-dir "$C5_INPUT"\n'
    )
    aggregate = aggregate.replace(upgraded, measured, 1).replace(paced, measured + inputs, 1)
    return (worker + aggregate_header + aggregate).encode()


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


def workflow_layout(ref, paired):
    coordinator = workflow_matrix(blob(ref, WORKFLOW))
    if not paired:
        return coordinator
    companion = workflow_matrix(blob(ref, COMPANION_WORKFLOW))
    if (not coordinator or not companion or coordinator.keys() & companion.keys()
            or len(coordinator) > CAMPAIGN_LIMITS[WORKFLOW]
            or len(companion) > CAMPAIGN_LIMITS[COMPANION_WORKFLOW]):
        raise ValueError('overlapping or oversized C5 campaign matrices')
    return coordinator | companion


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


def input_maps(ref, declared_targets=None):
    names = files(ref)
    projects, extra = project_closure(ref, names)
    declared_targets = workflow.TARGETS if declared_targets is None else declared_targets
    target_paths = {f'src/Parchwell.Verifier/{value[0]}' for value in declared_targets.values()}
    selected = set(projects) | extra | {'global.json', 'Directory.Build.props', 'Directory.Packages.props',
        '.config/dotnet-tools.json', 'docs/operations/a01-runtime-limits.json', RUNNER, WORKFLOW,
        'tests/Parchwell.Verifier.Tests/Mutation/c5_projection.py',
        'tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json'}
    if COMPANION_WORKFLOW in names:
        selected.update((COMPANION_WORKFLOW, CAMPAIGN_VALIDATOR))
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
        elif p in CAMPAIGN_LIMITS:
            data = execution_workflow(data)
        result[p] = digest(data)
    targets = {p: digest(blob(ref, 'src/Parchwell.Verifier/' + p)) for p, _ in declared_targets.values()}
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

HISTORICAL_QUALIFICATION_COMMIT = '6c6ed9208b005df0ac51033f4cf2be687b0983b9'
HISTORICAL_QUALIFICATION_PATH = 'tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json'
HISTORICAL_QUALIFICATION_SHA256 = 'd8ba890476525bc058d062cc866f91205a22d3040140f32671a9a2c7eb1957e6'


def historical_qualification():
    data = blob(HISTORICAL_QUALIFICATION_COMMIT, HISTORICAL_QUALIFICATION_PATH)
    if digest(data) != HISTORICAL_QUALIFICATION_SHA256:
        raise ValueError('historical qualification mismatch')
    return json.loads(data)


def timing_evidence_shape(items):
    if not isinstance(items, dict):
        raise ValueError('invalid timing evidence')
    for shard, item in items.items():
        if not isinstance(shard, str) or not shard or not isinstance(item, dict):
            raise ValueError('invalid timing evidence')
        if item == {'kind': 'historical'}:
            continue
        if set(item) != {'kind', 'source_commit', 'artifact_sha256', 'timing_inputs_sha256',
                         'observed_workers', 'observed_seconds', 'method'} or item['kind'] != 'measured':
            raise ValueError('invalid timing evidence')
        if not isinstance(item['source_commit'], str) or not re.fullmatch(r'[0-9a-f]{40}', item['source_commit']):
            raise ValueError('invalid timing commit')
        if any(not isinstance(item[k], str) or not HEX.fullmatch(item[k]) for k in ('artifact_sha256', 'timing_inputs_sha256')):
            raise ValueError('invalid timing hash')
        if type(item['observed_workers']) is not int or item['observed_workers'] < 1:
            raise ValueError('invalid timing workers')
        if type(item['observed_seconds']) not in (int, float) or not 0 < item['observed_seconds'] <= sys.float_info.max or not math.isfinite(item['observed_seconds']):
            raise ValueError('invalid timing observation')
        if not isinstance(item['method'], str) or not item['method'].strip():
            raise ValueError('invalid timing method')


def has_shard_timing(shard, path, record, item, proof, baseline):
    if proof is None:
        return False
    if proof['kind'] == 'historical':
        old = baseline.get('targets', {}).get(path, {})
        return (record['source_sha256'] == old.get('source_sha256')
                and record['evidence'] == old.get('evidence')
                and item == old.get('shards', {}).get(shard))
    workers = re.search(r'\bobserved_workers=([1-9][0-9]*)\b', item['basis'])
    if (proof['timing_inputs_sha256'] != item['timing_inputs_sha256']
            or not workers or proof['observed_workers'] != int(workers.group(1))):
        return False
    evidence = next((e for e in record['evidence'] if e['sha256'] == proof['artifact_sha256']
                     and e['source_commit'] == proof['source_commit']
                     and e['kind'] in {'hosted_complete', 'local_probe', 'partial_timing'}), None)
    if evidence is None:
        return False
    try:
        return digest(blob(proof['source_commit'], 'src/Parchwell.Verifier/' + path)) == record['source_sha256']
    except subprocess.CalledProcessError:
        return False


def provenance_shape(contexts):
    """Validate the complete measured maps and explicit current-input assessments."""
    if not isinstance(contexts, dict) or not contexts:
        raise ValueError('missing timing provenance')
    for binding, context in contexts.items():
        if not isinstance(binding, str) or not HEX.fullmatch(binding) or not isinstance(context, dict) or set(context) not in ({'shared_inputs', 'target_sources', 'assessments'}, {'shared_inputs', 'target_sources', 'assessments', 'source_commit'}):
            raise ValueError('invalid timing provenance')
        if 'source_commit' in context and (not isinstance(context['source_commit'], str) or not re.fullmatch(r'[0-9a-f]{40}', context['source_commit'])):
            raise ValueError('invalid provenance commit')
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


COHORT_LITERAL_NAMES = {'COHORT_BOOTSTRAP_SPANS', 'COHORT_SOURCE_SHA256',
                        'COHORT_INVENTORY_SHA256', 'COHORT_COORDINATES'}
CONFIG_PATH = 'tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json'
RUNNER_REFERENCE_COMMIT = '1e38605cceaf14c92ec9157e8eead43eedbf3a8d'
# Reviewed transition for the retained owners in issue 494. These digests
# exclude only the literal cuts/target tables and matrix rows.
CAPACITY_BASELINE = '0f41390beadfbec7d0f7d7819bbd834744ca85df'
CAPACITY_BASELINE_RUNNER = 'd4b1fb55e3e7dce9294d3914dc0c77f5b2cddc46c25132043eb9383025f1ee50'
CAPACITY_EXECUTION = {
    RUNNER: '66e6263885576b8b6c1078b935247b859dc00791d214a21c2fd1d989ab7a5933',
    WORKFLOW: 'cda07e2545340ade8d658ccfcce1ae65a472c97690c35186ab5f804712a46569',
    COMPANION_WORKFLOW: '1fade2b54ce8b20ed963ba5ebc5ff8cd0e6315f67b402cb46b1bfe695ffbebfb',
    CAMPAIGN_VALIDATOR: '1422960e78372e5ab3c0e605c161816434627cb20baab47cda43b703fef7d9e0',
}
FOLLOWUP_BASELINE = 'd28b1fc49ca53e48259101701d0fbb7e3271a0c5'
FOLLOWUP_EXECUTION = {
    **CAPACITY_EXECUTION,
    RUNNER: '9ba2b444dcd5e3c06e2e24bb2fe6b1bc6e6dcd67f9e7eaa3ad57c9047d9817b9',
}
RESULT_COHORTS = {
    'RESULT_CODEC_COHORT': ('verifier-result-codec-p2', range(4093, 4103), {'4093', '4098'}),
    'RESULT_CODEC_P4_COHORT': ('verifier-result-codec-p4', range(4110, 4120), {'4110', '4115'}),
}


def runner_literals(data, legacy=False):
    """Extract only reviewed literal declarations, retaining every other byte of runner code."""
    tree = ast.parse(data.decode('utf-8'))
    names = ({'COHORT_BOOTSTRAP_SPANS'} if legacy else COHORT_LITERAL_NAMES) | {'TARGETS', 'PRIMARY_CUTS'}
    assignments = {}
    codecs = {}
    for node in tree.body:
        if isinstance(node, ast.Assign) and len(node.targets) == 1 and isinstance(node.targets[0], ast.Name) and node.targets[0].id in names:
            name = node.targets[0].id
            if name in assignments:
                raise ValueError('duplicate runner declaration')
            assignments[name] = node
        if isinstance(node, ast.AnnAssign) and isinstance(node.target, ast.Name) and node.target.id in RESULT_COHORTS:
            name = node.target.id
            if (name in codecs or not isinstance(node.annotation, ast.Name)
                    or node.annotation.id != 'CohortDefinition' or node.value is None):
                raise ValueError('invalid typed cohort declaration')
            codecs[name] = node
    if set(assignments) != names:
        raise ValueError('missing runner declaration')
    for node in ast.walk(tree):
        if isinstance(node, ast.Name) and isinstance(node.ctx, (ast.Store, ast.Del)) and node.id in names:
            if not any(node is assignment.targets[0] for assignment in assignments.values()):
                raise ValueError('runner declaration rebound')
        if (isinstance(node, ast.Name) and isinstance(node.ctx, (ast.Store, ast.Del))
                and node.id in RESULT_COHORTS
                and (node.id not in codecs or node is not codecs[node.id].target)):
            raise ValueError('typed cohort declaration rebound')
    values = {name: ast.literal_eval(node.value) for name, node in assignments.items()}
    for name, codec in codecs.items():
        # literal_eval accepts duplicate keys, including in nested dictionaries.
        for node in ast.walk(codec.value):
            if isinstance(node, ast.Dict):
                keys = [ast.literal_eval(key) for key in node.keys]
                if len(keys) != len(set(keys)):
                    raise ValueError('duplicate typed cohort key')
        declaration = ast.literal_eval(codec.value)
        if (not isinstance(declaration, dict)
                or set(declaration) != {'source_sha256', 'inventory_sha256', 'bootstrap', 'identities'}
                or any(not isinstance(declaration[key], str) or not HEX.fullmatch(declaration[key])
                       for key in ('source_sha256', 'inventory_sha256'))):
            raise ValueError('invalid typed cohort declaration')
        identities, bootstrap = declaration['identities'], declaration['bootstrap']
        _, references, first_members = RESULT_COHORTS[name]
        if (not isinstance(identities, dict) or set(identities) != set(map(str, references))
                or not isinstance(bootstrap, dict) or set(bootstrap) != first_members):
            raise ValueError('invalid typed cohort ownership')
        def valid_span(span):
            return (isinstance(span, tuple) and len(span) == 2
                    and all(type(n) is int and n >= 0 for n in span) and span[0] < span[1])
        if any(not isinstance(item, tuple) or len(item) != 2 or not valid_span(item[0])
               or not isinstance(item[1], tuple) or any(not valid_span(span) for span in item[1])
               for item in bootstrap.values()):
            raise ValueError('invalid typed cohort bootstrap')
        for identity in identities.values():
            if (not isinstance(identity, dict) or set(identity) != {'mutatorName', 'replacement', 'location', 'static'}
                    or not isinstance(identity['mutatorName'], str) or not identity['mutatorName']
                    or not isinstance(identity['replacement'], str) or identity['static'] is not True
                    or not isinstance(identity['location'], dict) or set(identity['location']) != {'start', 'end'}):
                raise ValueError('invalid typed cohort identity')
            for point in identity['location'].values():
                if (not isinstance(point, dict) or set(point) != {'line', 'column'}
                        or any(type(n) is not int or n < 1 for n in point.values())):
                    raise ValueError('invalid typed cohort location')
        values[name] = declaration
    targets, cuts = values['TARGETS'], values['PRIMARY_CUTS']
    if (not isinstance(targets, dict) or not targets or any(
            not isinstance(k, str) or not k or not isinstance(v, tuple) or len(v) != 2
            or not isinstance(v[0], str) or not v[0] or type(v[1]) is not int or v[1] < 1
            for k, v in targets.items()) or len({v[0] for v in targets.values()}) != len(targets)):
        raise ValueError('invalid target table')
    if (not isinstance(cuts, dict) or any(k not in targets or not isinstance(v, tuple)
            or any(type(n) is not int or n <= 0 for n in v) for k, v in cuts.items())):
        raise ValueError('invalid cuts table')
    spans = values['COHORT_BOOTSTRAP_SPANS']
    if (not isinstance(spans, dict) or not spans or any(
            not isinstance(k, str) or not k.isdigit() or not isinstance(v, tuple) or len(v) != 2
            or any(type(n) is not int or n < 0 for n in v) or v[0] >= v[1] for k, v in spans.items())):
        raise ValueError('invalid cohort declarations')
    coordinates = values.get('COHORT_COORDINATES')
    if not legacy and (not isinstance(coordinates, tuple) or not coordinates or any(
                not isinstance(v, tuple) or len(v) != 4 or any(type(n) is not int or n < 0 for n in v)
                for v in coordinates)
            or any(not isinstance(values[k], str) or not HEX.fullmatch(values[k])
                   for k in ('COHORT_SOURCE_SHA256', 'COHORT_INVENTORY_SHA256'))):
        raise ValueError('invalid cohort declarations')
    # literal_eval normally accepts duplicate dict keys; reject them before masking.
    for name in ('TARGETS', 'PRIMARY_CUTS', 'COHORT_BOOTSTRAP_SPANS'):
        node = assignments[name].value
        if not isinstance(node, ast.Dict) or len(node.keys) != len(values[name]):
            raise ValueError('duplicate literal key')
    lines = data.splitlines(keepends=True)
    offsets = [0]
    for line in lines:
        offsets.append(offsets[-1] + len(line))
    masked = data
    edits = []
    for node in assignments.values():
        value = node.value
        edits.append((offsets[value.lineno - 1] + value.col_offset,
                      offsets[value.end_lineno - 1] + value.end_col_offset))
    for start, end in sorted(edits, reverse=True):
        masked = masked[:start] + b'None' + masked[end:]
    return values, masked


def committed_physical_owners(declarations, matrix):
    """Reconstruct the committed physical mapping and require exact matrix coverage."""
    parts = workflow.build_parts(declarations['TARGETS'], declarations['PRIMARY_CUTS'])
    cohorts = {}
    for index in range(1, 4):
        owner = f'b10-final-projection-codec-p{index}'
        cohorts[owner] = tuple(f'{owner}-{suffix}' for suffix in ('a', 'b'))
    for declaration, (owner, _, _) in RESULT_COHORTS.items():
        if declaration in declarations:
            cohorts[owner] = (f'{owner}-a', f'{owner}-b')
    result = {}
    for name, part in parts.items():
        for physical in cohorts.get(name, (name,)):
            if physical in result:
                raise ValueError('duplicate physical owner')
            result[physical] = (declarations['TARGETS'][part[0]][0], part)
    if not isinstance(matrix, dict) or matrix != {name: value[0] for name, value in result.items()}:
        raise ValueError('committed matrix ownership mismatch')
    return result


def strict_json(data):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError('duplicate JSON key')
            result[key] = value
        return result
    return json.loads(data, object_pairs_hook=unique,
                      parse_constant=lambda value: (_ for _ in ()).throw(ValueError('nonfinite JSON')))


@functools.lru_cache(maxsize=8)
def proof_tree(ref):
    if not re.fullmatch(r'[0-9a-f]{40}', ref) or git('cat-file', '-t', ref).strip() != b'commit':
        raise ValueError('proof source is not a commit')
    data = blob(ref, RUNNER)
    legacy = digest(execution_runner(data)) == RUNNER_TIMING_PAIR[0]
    declarations, code = runner_literals(data, legacy=legacy)
    maps = input_maps(ref, declarations['TARGETS'])
    return (declarations, code, maps, strict_json(blob(ref, CONFIG_PATH)),
            workflow_layout(ref, COMPANION_WORKFLOW in maps[0]))


def same_timing_layout(reference, current):
    """Match execution and target inputs while leaving renewable inputs to v2 assessments."""
    old_shared, old_targets = reference[2]
    new_shared, new_targets = current[2]
    return (reference[:2] == current[:2] and reference[3:] == current[3:]
            and old_targets == new_targets
            and all(old_shared.get(path) == new_shared.get(path) for path in NON_RENEWABLE))


def selector_ast(data):
    """Selector and ownership construction, including their module-level constants."""
    body = ast.parse(data.decode('utf-8')).body
    def position(name, kind):
        matches = [i for i, node in enumerate(body) if
                   (isinstance(node, ast.FunctionDef) and kind == 'function' and node.name == name)
                   or (isinstance(node, ast.Assign) and kind == 'assignment' and len(node.targets) == 1
                       and isinstance(node.targets[0], ast.Name) and node.targets[0].id == name)]
        if len(matches) != 1:
            raise ValueError('unsupported selector declarations')
        return matches[0]
    start = position('build_parts', 'function')
    end = position('BASE_CONFIG', 'assignment')
    first = position('target_text', 'function')
    last = position('isolated_config', 'function')
    if not start <= end < first <= last:
        raise ValueError('unsupported selector order')
    return ast.dump(ast.Module(body=body[start:end + 1] + body[first:last + 1], type_ignores=[]),
                    include_attributes=False)


def legacy_runner_bridge(commit, old, new_code):
    """Compose the separately approved exact runner transition with the data comparison."""
    old_data = blob(commit, RUNNER)
    reference_data = blob(RUNNER_REFERENCE_COMMIT, RUNNER)
    if (digest(execution_runner(old_data)) != RUNNER_TIMING_PAIR[0]
            or digest(execution_runner(reference_data)) != RUNNER_TIMING_PAIR[1]
            or selector_ast(old_data) != selector_ast(reference_data)):
        return None
    reference, reference_code = runner_literals(reference_data)
    if (reference_code != new_code
            or old['COHORT_BOOTSTRAP_SPANS'] != reference['COHORT_BOOTSTRAP_SPANS']):
        return None
    return reference


def same_selection(old, old_matrix, new, new_matrix, owner, shard):
    old_physical = committed_physical_owners(old, old_matrix)
    new_physical = committed_physical_owners(new, new_matrix)
    selection = new_physical.get(shard)
    if selection is None or selection[0] != owner or old_physical.get(shard) != selection:
        return False
    key = selection[1][0]
    return (old['TARGETS'][key] == new['TARGETS'][key]
            and old['PRIMARY_CUTS'].get(key) == new['PRIMARY_CUTS'].get(key))


def selected_invocation(declarations, matrix, shard):
    """Describe the bounds passed to one physical owner by the pinned runner."""
    physical = committed_physical_owners(declarations, matrix).get(shard)
    if physical is None:
        return None
    target, (key, index, count) = physical
    cuts = declarations['PRIMARY_CUTS'].get(key)
    if cuts is None:
        return target, key, 'member', index, count
    bounds = (0, *cuts, workflow.SPAN_END)
    if len(bounds) != count or any(left >= right for left, right in zip(bounds, bounds[1:])):
        raise ValueError('invalid committed span layout')
    if index == count:
        return target, key, 'crossing', cuts
    return target, key, 'primary', bounds[index - 1:index + 1]


PAIRED_TAIL_PRIOR = 'b31928a2ed22a634e8e1a66edb953311eaa9ca87'
PAIRED_TAIL_SOURCE = '3c65a2fa52f16378c3815159cd07f8237e3346bf'
PAIRED_TAIL_COUNTS = {'verifier-result-codec': 17, 'b10-final-judge': 13, 'b10-final-projection-codec': 13}

PAIRED_RECOVERY_SOURCE = '4275b634f14c1f81fcfe91dfcd7deb260a0c8a7f'
PAIRED_RECOVERY_CHANGED = frozenset({
    'b10-final-judge-crossing',
    'b10-final-judge-p1',
    'b10-final-judge-p2',
    'b10-final-judge-p3',
    'b10-final-judge-p4',
    'b10-final-projection-codec-crossing',
    'b10-final-projection-codec-p5',
    'b10-final-projection-codec-p6',
    'b10-final-projection-codec-p7',
    'b10-final-projection-codec-p8',
    'b10-final-projection-codec-p9',
    'verifier-result-codec-crossing',
    'verifier-result-codec-p18',
    'verifier-result-codec-p19',
    'verifier-result-codec-p20',
    'verifier-result-codec-p21',
    'verifier-result-codec-p22',
    'verifier-result-codec-p23',
})
# Receipt archive hashes and observed durations from the 25 completed, unchanged R3 jobs.
PAIRED_RECOVERY_RETAINED = {
    'b10-final-judge-p10': ('58719f95be11905c33f5ee3037f7f90179cca9d639a0d3c8621e8cc79c5c4363', 1935.7651908799999, 44, 1937),
    'b10-final-judge-p11': ('082b5c913f584ca571cd4745f7b17e12796030d764a351e09ec4e2f98065f942', 1778.520286444, 43, 1780),
    'b10-final-judge-p12': ('3cc07f1b8c6fcd547a18b9adf2d0677f9e9b8e79c529cc06bcab92e2a9853d5f', 2507.719754026, 58, 2509),
    'b10-final-judge-p5': ('67af9be26618825da0d9c85f4a290316f1aac3cb1de0a67690db7cc92b733c46', 2151.477483571, 50, 2153),
    'b10-final-judge-p6': ('c25cc6258b2d24cd7fdd215580550cf169a251e6ac932061c464f04086e0f985', 1710.121628492, 55, 1711),
    'b10-final-judge-p7': ('9db0b00972930e222ed1cb2382b721a19f9e6b68c4a1291397ed55cc1467a832', 1530.6311089350002, 56, 1532),
    'b10-final-judge-p8': ('3929baed27a339394a0b55afaa44548c08f9dc6488fedd0520605faf8d41adec', 2182.930586415, 50, 2184),
    'b10-final-judge-p9': ('d3e4ff88956ef1ed6284c32d5892daf719274fa40f05462ae89452be40161a4e', 1180.904350457, 44, 1182),
    'b10-final-projection-codec-p10': ('4049f0160671d84589c9b2b1841db3738916acd6e32fe2256c5465d593ced6fb', 1564.589691557, 44, 1566),
    'b10-final-projection-codec-p11': ('4e6ca98258589b01d0a9575440cb5d4790a950108857deb2b405eef6da013b16', 2068.549000637, 47, 2070),
    'b10-final-projection-codec-p12': ('b0acf4d2731e133579a8290fc74bc3e991d09b56b183f996b7a71154f47d6e47', 1278.426747581, 46, 1279),
    'b10-final-projection-codec-p13': ('9b4fe9c91cde5d9a3b017c6129681dc8f7a1af265a1fc50cc7719cf0da74b235', 2009.4777907540001, 47, 2011),
    'b10-final-projection-codec-p14': ('f46fc181c617909a3fe2c74fc0911d8d8dfa62b9fdeaff8232a23b1d73c5d8bf', 2072.841255362, 60, 2074),
    'b10-final-projection-codec-p15': ('19a240f2b8d2e7b9bbaecf3addc44eb334bd9a5de6ff7431372f7d0d31ed0d72', 2137.536345279, 48, 2138),
    'b10-final-projection-codec-p4': ('f79f6dcd792c280e7521ebffd316996a08cdf86b3cd7a1ff9711eb5aeaec4b7e', 2300.582122063, 55, 2302),
    'verifier-result-codec-p10': ('fdf34139ebe69be1d4938f9b519d308b001c859c248669e5a4697247760c3246', 1373.559332149, 70, 1374),
    'verifier-result-codec-p11': ('709dd0529b9aa4179b7f6bc18a21b3ed4d67d0fa83960466059901760ecf6395', 1997.400871458, 64, 1998),
    'verifier-result-codec-p12': ('e594ac1b9a97d0247c85e1bf327a0241ae0d76e0080215d80e147a0fa49698df', 2315.3619691119998, 53, 2316),
    'verifier-result-codec-p13': ('905d9a450a13eec1d33249457fe898cf80c2ba30ba7206b443df2989be54032d', 2004.812414428, 56, 2006),
    'verifier-result-codec-p14': ('9950eaad8f091c699b663e5af186e70e9021113748ad0ad80e55aed679b075d6', 2304.787644521, 52, 2306),
    'verifier-result-codec-p24': ('89c5159134ccae2488f8608f0b40e321e2634a63075e87e3e008742660b24e53', 2450.6672467020003, 73, 2451),
    'verifier-result-codec-p25': ('df5c7a9a8b88c31c4963b51c7258c082dd969999a30c1d0b73759b2632eb4ede', 2246.865985225, 54, 2248),
    'verifier-result-codec-p26': ('5d4fe56e695627b54bf38427db78bcdc9f82d23f6a04d25d961b602ff285c2e4', 2604.3595174330003, 70, 2605),
    'verifier-result-codec-p8': ('697b48d6041e8ab0cd8167cc4ba2142939cee74c735e95933e2e9b8d7eddf19d', 1658.803118783, 54, 1659),
    'verifier-result-codec-p9': ('2d40c34a422daa5daf0a52bbef7716693222fb2b9d43d7d2bf6c632bc52d0c43', 2556.626934938, 67, 2557),
}


@functools.lru_cache(maxsize=4)
def paired_tail_changes(head):
    """Find changed invocations only on the reviewed paired-run tail layout."""
    if not head:
        return frozenset()
    try:
        git('merge-base', '--is-ancestor', PAIRED_TAIL_SOURCE, head)
        prior = proof_tree(PAIRED_TAIL_PRIOR)
        measured = proof_tree(PAIRED_TAIL_SOURCE)
        if not same_timing_layout(measured, proof_tree(head)) or prior[1:] != measured[1:]:
            return frozenset()
        old, new = prior[0], measured[0]
        cuts = dict(old['PRIMARY_CUTS'])
        for target in PAIRED_TAIL_COUNTS:
            if len(cuts[target]) != len(new['PRIMARY_CUTS'][target]):
                return frozenset()
            cuts[target] = new['PRIMARY_CUTS'][target]
        if new != {**old, 'PRIMARY_CUTS': cuts} or any(
                workflow_matrix(blob(head, path)) != workflow_matrix(blob(PAIRED_TAIL_PRIOR, path))
                for path in CAMPAIGN_LIMITS):
            return frozenset()
        matrix = measured[4]
        changed = frozenset(shard for shard in matrix
                            if selected_invocation(old, matrix, shard) != selected_invocation(new, matrix, shard))
        counts = {target: sum(matrix[shard] == new['TARGETS'][target][0] for shard in changed)
                  for target in PAIRED_TAIL_COUNTS}
        if counts != PAIRED_TAIL_COUNTS or sum(counts.values()) != len(changed):
            return frozenset()
        return changed
    except (KeyError, TypeError, ValueError, SyntaxError, UnicodeError, subprocess.CalledProcessError):
        return frozenset()


@functools.lru_cache(maxsize=4)
def paired_recovery_changes(head):
    """Accept only the reviewed successor and its exact physical owner changes."""
    if not head:
        return frozenset()
    try:
        git('merge-base', '--is-ancestor', PAIRED_RECOVERY_SOURCE, head)
        old, new = proof_tree(PAIRED_TAIL_SOURCE), proof_tree(PAIRED_RECOVERY_SOURCE)
        if not same_timing_layout(new, proof_tree(head)) or old[1:] != new[1:]:
            return frozenset()
        for path in CAMPAIGN_LIMITS:
            old_matrix, new_matrix, head_matrix = (
                list(workflow_matrix(blob(ref, path)).items())
                for ref in (PAIRED_TAIL_SOURCE, PAIRED_RECOVERY_SOURCE, head)
            )
            if old_matrix != new_matrix or new_matrix != head_matrix:
                return frozenset()
        cuts = dict(old[0]['PRIMARY_CUTS'])
        for target in PAIRED_TAIL_COUNTS:
            if len(cuts[target]) != len(new[0]['PRIMARY_CUTS'][target]):
                return frozenset()
            cuts[target] = new[0]['PRIMARY_CUTS'][target]
        if new[0] != {**old[0], 'PRIMARY_CUTS': cuts}:
            return frozenset()
        matrix = new[4]
        changed = frozenset(shard for shard in matrix if
                            selected_invocation(old[0], old[4], shard) !=
                            selected_invocation(new[0], matrix, shard))
        if changed != PAIRED_RECOVERY_CHANGED:
            return frozenset()
        r3 = paired_tail_changes(PAIRED_TAIL_SOURCE)
        if changed | frozenset(PAIRED_RECOVERY_RETAINED) != r3:
            return frozenset()
        return changed
    except (KeyError, TypeError, ValueError, SyntaxError, UnicodeError, subprocess.CalledProcessError):
        return frozenset()


def followup_equivalent(old, old_maps, old_config, new, new_maps, new_config):
    """Limit the paired runner change to the reviewed addition of the p4 cohort."""
    old_shared, old_targets = old_maps
    new_shared, new_targets = new_maps
    return ({path: old_shared.get(path) for path in CAPACITY_EXECUTION} == CAPACITY_EXECUTION
            and {path: new_shared.get(path) for path in FOLLOWUP_EXECUTION} == FOLLOWUP_EXECUTION
            and {**old_shared, RUNNER: FOLLOWUP_EXECUTION[RUNNER]} == new_shared
            and old_targets == new_targets and old_config == new_config
            and all(old[name] == new[name] for name in (*COHORT_LITERAL_NAMES, 'RESULT_CODEC_COHORT'))
            and 'RESULT_CODEC_P4_COHORT' not in old and 'RESULT_CODEC_P4_COHORT' in new)


@functools.lru_cache(maxsize=8)
def capacity_bridge(head):
    """Bind the one reviewed transition to identical worker templates and inputs."""
    baseline = proof_tree(CAPACITY_BASELINE)
    current = proof_tree(head)
    old_shared, old_targets = baseline[2]
    new_shared, new_targets = current[2]
    if (old_shared.get(RUNNER) != CAPACITY_BASELINE_RUNNER
            or {path: new_shared.get(path) for path in CAPACITY_EXECUTION} not in (
                CAPACITY_EXECUTION, FOLLOWUP_EXECUTION)
            or {p: h for p, h in old_shared.items() if p not in CAPACITY_EXECUTION}
            != {p: h for p, h in new_shared.items() if p not in CAPACITY_EXECUTION}
            or old_targets != new_targets or baseline[3] != current[3]
            or any(baseline[0][key] != current[0][key] for key in COHORT_LITERAL_NAMES)):
        raise ValueError('capacity transition input mismatch')

    def worker(ref, path):
        content = execution_workflow(blob(ref, path))
        return content.split(b'\njobs:\n', 1)[1].split(b'\n  aggregate:\n', 1)[0].rstrip()

    prior_worker = worker(CAPACITY_BASELINE, WORKFLOW)
    if any(worker(head, path) != prior_worker for path in CAMPAIGN_LIMITS):
        raise ValueError('capacity transition worker mismatch')
    return baseline, workflow_matrix(blob(head, COMPANION_WORKFLOW))


def _committed_effective_config(context, owner, shard, head):
    """Derive an unchanged shard invocation family from two committed trees."""
    try:
        commit = context['source_commit']
        if not head or not shard or not re.fullmatch(r'[0-9a-f]{40}', commit):
            return False
        old, old_code, old_maps, old_config, old_matrix = proof_tree(commit)
        new, new_code, new_maps, new_config, new_matrix = proof_tree(head)
        followup = (commit == FOLLOWUP_BASELINE
                    and followup_equivalent(old, old_maps, old_config, new, new_maps, new_config))
        if COMPANION_WORKFLOW in new_maps[0] and COMPANION_WORKFLOW not in old_maps[0]:
            baseline, retained = capacity_bridge(head)
            if (retained.get(shard) != owner or shard in workflow.COHORTS
                    or not same_selection(baseline[0], baseline[4], new, new_matrix, owner, shard)
                    or old_maps[0].get(WORKFLOW) != baseline[2][0][WORKFLOW]):
                return False
            new, new_code, _, new_config, new_matrix = baseline
        legacy = old_maps[0].get(RUNNER) == RUNNER_TIMING_PAIR[0]
        reference = legacy_runner_bridge(commit, old, new_code) if legacy else None
        if (legacy and reference is None) or (not legacy and old_code != new_code and not followup):
            return False
        if old_maps != (context['shared_inputs'], context['target_sources']):
            return False
        if context['target_sources'].get(owner) != digest(blob(head, 'src/Parchwell.Verifier/' + owner)):
            return False
        for config, declared in ((old_config, old['TARGETS']), (new_config, new['TARGETS'])):
            if not isinstance(config, dict) or set(config) != {'stryker-config'} or not isinstance(config['stryker-config'], dict):
                return False
            entries = config['stryker-config'].get('mutate')
            if entries != [v[0] for v in declared.values()] or len(entries) != len(set(entries)):
                return False
        old_settings = dict(old_config['stryker-config'])
        new_settings = dict(new_config['stryker-config'])
        old_targets = old_settings.pop('mutate')
        new_targets = new_settings.pop('mutate')
        expected_settings = {k: v for k, v in workflow.BASE_CONFIG['stryker-config'].items() if k != 'mutate'}
        if old_settings != new_settings or new_settings != expected_settings:
            return False
        if [item for item in new_targets if item in old_targets] != old_targets:
            return False
        if followup:
            selected = selected_invocation(new, new_matrix, shard)
            if selected is None or selected[0] != owner or selected_invocation(old, old_matrix, shard) != selected:
                return False
        elif not same_selection(old, old_matrix, new, new_matrix, owner, shard):
            return False
        if shard in workflow.COHORTS:
            cohort_data = reference if legacy else old
            if (any(cohort_data[k] != new[k] for k in COHORT_LITERAL_NAMES)
                    or (legacy and cohort_data['COHORT_SOURCE_SHA256'] != context['target_sources'].get(owner))):
                return False
        # Qualification separately checks the generated selection at the current head.
        return True
    except (KeyError, TypeError, ValueError, SyntaxError, UnicodeError, subprocess.CalledProcessError):
        return False


# The owner accepted timing risk for unchanged selections across this correction.
# https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7023139739
REVIEW_CORRECTION_PRIOR = 'e8fd9bbbe1a71098c957515b3058d60f1ddab361'
REVIEW_CORRECTION = '52a056fea353c9457f3c1ed7da689c62733d13a3'
REVIEW_CORRECTION_TARGET = 'Common/Phase/B10FinalProjectionCodec.cs'
REVIEW_CORRECTION_COHORT_PINS = frozenset({'COHORT_SOURCE_SHA256', 'COHORT_INVENTORY_SHA256'})
REVIEW_CORRECTION_SHARED = frozenset({
    'tests/Parchwell.Verifier.Tests/Judge/B10FinalJudgeTests.cs',
    'tests/Parchwell.Verifier.Tests/Judge/B10FinalProjectionCodecTests.cs',
    'tests/Parchwell.Verifier.Tests/Judge/PhaseCandidateCodecTests.cs',
    'tests/Parchwell.Verifier.Tests/Judgement/B10FinalClassificationDuplicateTests.cs',
    'tests/Parchwell.Verifier.Tests/Results/DuplicateBreakdownTests.cs',
    'tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs',
})


# Approved diagnostic receipts retain their own source across this exact rebalance.
# https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7045193559
REBALANCED_SOURCE = '06d999c33a33e0317af03e557b1048c4336f1bc8'
REBALANCED_CODEC_CUTS = (1109, 1747, 2198, 3912, 5278, 6812, 8327, 9610, 10753, 11892, 13262, 14786, 16454, 18430)
REBALANCED_RECEIPTS = {'b10-final-projection-codec-p1-a': ('14607930859',
                                     '0c56bd10ffcfe70697c192eb39d43f03fc1affcb20cd4c09febaf3b65aabb7f4',
                                     '74a711c31e71438260353da9e5311a4d0698ad7a065135c8254ca31441d74588',
                                     (0, 0, 1, 3, 1046, 1049),
                                     1047.784846148),
 'b10-final-projection-codec-p1-b': ('14607742039',
                                     '9c058ce96348ba1317a68a12bdaca52c2042dadbacf4c2af11245c73da5bd0f5',
                                     '74a711c31e71438260353da9e5311a4d0698ad7a065135c8254ca31441d74588',
                                     (0, 0, 1, 4, 1253, 1257),
                                     1255.189522693),
 'b10-final-projection-codec-p2-a': ('14607309621',
                                     '2a2b6d24030126bc64facfd773352c90f33f1abd6b265715e253115d657487eb',
                                     '9620121f2c0cadaf7ff493b206547da5e9e4d1b89287250596b9b172ff6c9800',
                                     (0, 0, 1, 4, 1544, 1548),
                                     1546.675887856),
 'b10-final-projection-codec-p2-b': ('14607712890',
                                     '31e10dc270349565ed1a7d18a1ad7c2373d26ab04f3bb7d2a4789c44c8776522',
                                     '9620121f2c0cadaf7ff493b206547da5e9e4d1b89287250596b9b172ff6c9800',
                                     (0, 0, 1, 3, 1476, 1479),
                                     1478.456102636),
 'b10-final-projection-codec-p3-a': ('14607513063',
                                     'e0443978318a38025426a1cbb930586d3f9af970600ca22ff75f16011c6de8da',
                                     '81b9a34ba3cb5d370256ec6a40ea43aea0410848dafcceb025d57b1257b98bdd',
                                     (0, 0, 1, 4, 1415, 1419),
                                     1417.123127666),
 'b10-final-projection-codec-p3-b': ('14607304850',
                                     '749d0189a32e8002edddd8f0d2276d3ab8f7d19bd41352283ff6ccc3fbadcd3a',
                                     '81b9a34ba3cb5d370256ec6a40ea43aea0410848dafcceb025d57b1257b98bdd',
                                     (0, 0, 1, 4, 1541, 1545),
                                     1543.904961873)}

def rebalance_trees(head):
    """Prove the approved codec cuts and retain the original static geometry."""
    old = proof_tree(REVIEW_CORRECTION)
    new = proof_tree(REBALANCED_SOURCE)
    if proof_tree(head) != new:
        return None
    old_tables, old_code, old_maps, old_config, old_matrix = old
    new_tables, new_code, new_maps, new_config, new_matrix = new
    expected = dict(old_tables)
    cuts = dict(old_tables['PRIMARY_CUTS'])
    codec_cuts = cuts['b10-final-projection-codec']
    if (len(codec_cuts) != len(REBALANCED_CODEC_CUTS)
            or codec_cuts[:3] != REBALANCED_CODEC_CUTS[:3]):
        return None
    cuts['b10-final-projection-codec'] = REBALANCED_CODEC_CUTS
    expected['PRIMARY_CUTS'] = cuts
    if (expected != new_tables or (old_code, old_maps, old_config, old_matrix)
            != (new_code, new_maps, new_config, new_matrix)):
        return None
    return old, new


# These exact corrections retain timing only for an unchanged target and selector.
# https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7058676317
CI_REPAIR_PRIOR = '33f1d4618e22b0c125a1c097dc68db735a4db774'
CI_REPAIR_SOURCE = '0be50887eb42ba53fb9df934d2062c9e21a59850'
CI_REPAIR_OWNERS = frozenset({'b10-final-judge', 'b10-final-classification'})
CI_REPAIR_TEST = 'tests/Parchwell.Verifier.Tests/Judgement/B10FinalClassificationDuplicateTests.cs'
CI_REPAIR_REBALANCE = 'e9910d68f149be96da6ff921af89774526c95c94'
CI_REPAIR_CAPACITY = '6982956ae1f6d14f8f2f4b53e8d6778b40216d56'
CI_REPAIR_CLASSIFIER = 'b10-final-classification'
# Shared inputs already present on the expanded sizing source.
# https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7060787313
CAPACITY_SHARED_MAIN = ('50a208b7f74bb0c149f7242b7e6f423417233b7c',
                        'd46720e62d79c363386c1f3b3f37791819fa9c7a')
# Exact joined product input tree when PR 573 merged during sizing.
# https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7061521822
CI_REPAIR_CAPACITY_COMBINED = '871b100e3c77033656e6f081360da5cf26cabb64'
CAPACITY_COMBINED_SHARED = frozenset({
    'src/Parchwell.Verifier/Judge/ConsumerPhaseEvidence.cs',
    'src/Parchwell.Verifier/VerifyRun/Consumer/Judgement/ConsumerJudgement.cs',
    'tests/Parchwell.Verifier.Tests/Consumer/ConsumerJudgementTests.cs',
    'tests/Parchwell.Verifier.Tests/Judge/ConsumerPhaseJudgeTests.cs',
})
# The hosted review correction requires fresh timing for the classifier.
# https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7061711703
CI_REPAIR_OWNERSHIP_SOURCE = 'e520f829001dc1ea76b10df49b2277b26e815872'


def classifier_ownership_matches(combined, current):
    """Check the exact target, shared regression and relocated cut changes."""
    ownership = proof_tree(CI_REPAIR_OWNERSHIP_SOURCE)
    if not same_timing_layout(ownership, current):
        return False
    old, old_code, (old_shared, old_targets), old_config, old_matrix = combined
    new, new_code, (new_shared, new_targets), new_config, new_matrix = ownership
    key = CI_REPAIR_CLASSIFIER
    target = old['TARGETS'][key][0]
    cuts = {**old['PRIMARY_CUTS'], key: new['PRIMARY_CUTS'][key]}
    return (new == {**old, 'PRIMARY_CUTS': cuts}
            and (old_code, old_config, old_matrix) == (new_code, new_config, new_matrix)
            and new_shared[CI_REPAIR_TEST] != old_shared[CI_REPAIR_TEST]
            and new_shared == {**old_shared, CI_REPAIR_TEST: new_shared[CI_REPAIR_TEST]}
            and new_targets[target] != old_targets[target]
            and new_targets == {**old_targets, target: new_targets[target]})


@functools.lru_cache(maxsize=4)
def classifier_capacity_trees(head):
    """Prove the expanded layout leaves other shards' executable selections intact."""
    prior = proof_tree(CI_REPAIR_REBALANCE)
    expanded = proof_tree(CI_REPAIR_CAPACITY)
    current = proof_tree(head)
    if not same_timing_layout(expanded, current):
        combined = proof_tree(CI_REPAIR_CAPACITY_COMBINED)
        if (combined[:2], combined[2][1], combined[3:]) != (expanded[:2], expanded[2][1], expanded[3:]):
            return None
        sized, joined = expanded[2][0], combined[2][0]
        if {path for path in sized.keys() | joined.keys() if sized.get(path) != joined.get(path)} != CAPACITY_COMBINED_SHARED:
            return None
        if not same_timing_layout(combined, current) and not classifier_ownership_matches(combined, current):
            return None
    old, old_code, (old_shared, old_targets), old_config, _ = prior
    new, new_code, (new_shared, new_targets), new_config, _ = expanded
    key = CI_REPAIR_CLASSIFIER
    classifier = old['TARGETS'][key][0]
    expected = {**old, 'TARGETS': {**old['TARGETS'], key: (classifier, 18)},
                'PRIMARY_CUTS': {**old['PRIMARY_CUTS'], key: new['PRIMARY_CUTS'][key]}}
    if (expected != new or len(new['PRIMARY_CUTS'][key]) != 17
            or (old_code, old_targets, old_config) != (new_code, new_targets, new_config)):
        return None
    measured, approved = (proof_tree(ref)[2][0] for ref in CAPACITY_SHARED_MAIN)
    changed = {path for path in measured.keys() | approved.keys() if measured.get(path) != approved.get(path)}
    expected_shared = dict(old_shared)
    for path in changed:
        if path in NON_RENEWABLE or old_shared.get(path) != measured.get(path):
            return None
        if path in approved:
            expected_shared[path] = approved[path]
        else:
            expected_shared.pop(path)
    old_check = b'    require(len(a) <= 172 and len(b) <= 146 and not set(a) & set(b),\n'
    new_check = (b"    require(len(a) <= c5_runtime.CAMPAIGN_LIMITS[PATHS['coordinator']]\n"
                 b"            and len(b) <= c5_runtime.CAMPAIGN_LIMITS[PATHS['companion']] and not set(a) & set(b),\n")
    old_validator = blob(CI_REPAIR_REBALANCE, CAMPAIGN_VALIDATOR)
    if old_validator.count(old_check) != 1 or blob(CI_REPAIR_CAPACITY, CAMPAIGN_VALIDATOR) != old_validator.replace(
            old_check, new_check):
        return None
    expected_shared[CAMPAIGN_VALIDATOR] = new_shared[CAMPAIGN_VALIDATOR]
    if len(changed) != 12 or expected_shared != new_shared:
        return None
    for path in CAMPAIGN_LIMITS:
        head_matrix = workflow_matrix(blob(head, path))
        pinned = workflow_matrix(blob(CI_REPAIR_CAPACITY, path))
        previous = workflow_matrix(blob(CI_REPAIR_REBALANCE, path))
        if (head_matrix != pinned or len(head_matrix) != CAMPAIGN_LIMITS[path]
                or {shard: owner for shard, owner in previous.items() if owner != classifier} != {
                    shard: owner for shard, owner in head_matrix.items() if owner != classifier}):
            return None
    return prior, current


def retained_classifier_capacity_selection(head, owner, shard):
    if not head or not shard:
        return False
    try:
        trees = classifier_capacity_trees(head)
        if trees is None:
            return False
        old, new = trees
        return (owner != new[0]['TARGETS'][CI_REPAIR_CLASSIFIER][0]
                and same_selection(old[0], old[4], new[0], new[4], owner, shard))
    except (KeyError, TypeError, ValueError, SyntaxError, UnicodeError, subprocess.CalledProcessError):
        return False


@functools.lru_cache(maxsize=4)
def ci_repair_rebalanced_trees(head):
    changed = proof_tree(CI_REPAIR_REBALANCE)
    if proof_tree(head) != changed:
        return None
    original = proof_tree(CI_REPAIR_SOURCE)
    expected = dict(original[0])
    cuts = dict(expected['PRIMARY_CUTS'])
    current = cuts['b10-final-classification']
    if current[6] != 17887:
        return None
    cuts['b10-final-classification'] = current[:6] + (17035,) + current[7:]
    expected['PRIMARY_CUTS'] = cuts
    if (expected, *original[1:]) != changed:
        return None
    return original, changed


@functools.lru_cache(maxsize=4)
def ci_repair_trees(head):
    repaired = proof_tree(CI_REPAIR_SOURCE)
    if proof_tree(head) != repaired and ci_repair_rebalanced_trees(head) is None:
        return None
    previous = proof_tree(CI_REPAIR_PRIOR)
    old, old_code, (old_shared, old_targets), old_config, old_matrix = previous
    new, new_code, (new_shared, new_targets), new_config, new_matrix = repaired
    expected = dict(old)
    expected['PRIMARY_CUTS'] = {**old['PRIMARY_CUTS'], **{key: new['PRIMARY_CUTS'][key] for key in CI_REPAIR_OWNERS}}
    changed_targets = {old['TARGETS'][key][0] for key in CI_REPAIR_OWNERS}
    if (expected != new or (old_code, old_config, old_matrix) != (new_code, new_config, new_matrix)
            or {path for path in old_shared.keys() | new_shared.keys() if old_shared.get(path) != new_shared.get(path)}
            != {CI_REPAIR_TEST}
            or {path for path in old_targets.keys() | new_targets.keys() if old_targets.get(path) != new_targets.get(path)}
            != changed_targets):
        return None
    return previous, repaired


def retained_ci_repair_selection(head, owner, shard):
    if retained_classifier_capacity_selection(head, owner, shard):
        head = CI_REPAIR_REBALANCE
    trees = ci_repair_trees(head)
    if trees is None:
        return False
    old, new = trees
    return (owner in old[2][1] and old[2][1][owner] == new[2][1].get(owner)
            and same_selection(old[0], old[4], new[0], new[4], owner, shard))


@functools.lru_cache(maxsize=1)
def prior_ci_repair_qualification():
    return strict_json(blob(CI_REPAIR_PRIOR, HISTORICAL_QUALIFICATION_PATH))


def retained_rebalance_timing(context, shard, path, record, item, timing, head):
    try:
        if (not head or path != CODEC_TARGET or shard not in REBALANCED_RECEIPTS
                or context.get('source_commit') != REBALANCED_SOURCE
                or timing.get('source_commit') != REVIEW_CORRECTION):
            return False
        artifact, archive, selection_hash, numbers, seconds = REBALANCED_RECEIPTS[shard]
        locator = ('https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/'
                   '44686622095/artifacts/' + artifact)
        if (timing.get('artifact_sha256') != archive or timing.get('observed_seconds') != seconds
                or timing.get('observed_workers') != numbers[2]
                or item['selection_sha256'] != selection_hash
                or tuple(item[k] for k in NUMERIC_FIELDS) != numbers
                or not any(e['kind'] == 'local_probe' and e['source_commit'] == REVIEW_CORRECTION
                           and e['locator'] == locator and e['sha256'] == archive
                           for e in record['evidence'])):
            return False
        prior = CI_REPAIR_PRIOR if retained_ci_repair_selection(head, path, shard) else head
        trees = rebalance_trees(prior)
        if trees is None:
            return False
        old, new = trees
        maps = (context['shared_inputs'], context['target_sources'])
        if old[2] != maps or new[2] != maps:
            return False
        selection = selected_invocation(old[0], old[4], shard)
        return (selection is not None and selection[0] == path
                and selection == selected_invocation(new[0], new[4], shard))
    except (KeyError, TypeError, ValueError, SyntaxError, UnicodeError, subprocess.CalledProcessError):
        return False


def committed_effective_config(context, owner, shard, head):
    """Retain the original proof except for the exact approved correction."""
    if retained_classifier_capacity_selection(head, owner, shard):
        return committed_effective_config(context, owner, shard, CI_REPAIR_REBALANCE)
    if _committed_effective_config(context, owner, shard, head):
        return True
    try:
        if not head or not shard or owner == REVIEW_CORRECTION_TARGET:
            return False
        if retained_ci_repair_selection(head, owner, shard):
            previous = prior_ci_repair_qualification()
            binding = previous['targets'][owner]['shards'][shard]['timing_inputs_sha256']
            prior = previous['timing_inputs'][binding]
            if any(context.get(key) != prior.get(key) for key in ('source_commit', 'shared_inputs', 'target_sources')):
                return False
            return committed_effective_config(prior, owner, shard, CI_REPAIR_PRIOR)
        prior = proof_tree(REVIEW_CORRECTION_PRIOR)
        correction = proof_tree(REVIEW_CORRECTION)
        current = proof_tree(head)
        if current != correction:
            trees = rebalance_trees(head)
            if (trees is None or owner == CODEC_TARGET
                    or not same_selection(trees[0][0], trees[0][4], trees[1][0], trees[1][4], owner, shard)):
                return False
        prior_tables, prior_code, (old_shared, old_targets), prior_config, prior_matrix = prior
        new_tables, new_code, (new_shared, new_targets), new_config, new_matrix = correction
        # The only runner change binds the changed target's own cohort inventory.
        changed_tables = {name for name in prior_tables.keys() | new_tables.keys()
                          if prior_tables.get(name) != new_tables.get(name)}
        if ((prior_code, prior_config, prior_matrix) != (new_code, new_config, new_matrix)
                or changed_tables != REVIEW_CORRECTION_COHORT_PINS
                or new_tables['COHORT_SOURCE_SHA256'] != new_targets[REVIEW_CORRECTION_TARGET]
                or {p for p in old_shared.keys() | new_shared.keys() if old_shared.get(p) != new_shared.get(p)}
                != REVIEW_CORRECTION_SHARED | {RUNNER}
                or {p for p in old_targets.keys() | new_targets.keys() if old_targets.get(p) != new_targets.get(p)}
                != {REVIEW_CORRECTION_TARGET}
                or old_targets.get(owner) is None or old_targets.get(owner) != new_targets.get(owner)
                or context['target_sources'].get(owner) != old_targets[owner]
                or not same_selection(prior_tables, prior_matrix, new_tables, new_matrix, owner, shard)):
            return False
        # Require the same complete, path-specific assessments as normal provenance.
        for key, actual in (('shared_inputs', new_shared), ('target_sources', new_targets)):
            measured = context[key]
            changes = context['assessments'][key]
            expected = {p for p in measured.keys() | actual.keys() if measured.get(p) != actual.get(p)}
            if set(changes) != expected or any(changes[p]['current_sha256'] != actual.get(p) for p in expected):
                return False
        return _committed_effective_config(context, owner, shard, REVIEW_CORRECTION_PRIOR)
    except (KeyError, TypeError, ValueError, SyntaxError, UnicodeError, subprocess.CalledProcessError):
        return False


def provenance_errors(context, shared, targets, owner, shard=None, head=None):
    """Account for every difference without renewing the estimate's own target."""
    errors = set()
    current_maps = {'shared_inputs': shared, 'target_sources': targets}
    derived = {RUNNER, CONFIG_PATH}
    if COMPANION_WORKFLOW in shared and COMPANION_WORKFLOW not in context['shared_inputs']:
        derived.update((*CAMPAIGN_LIMITS, CAMPAIGN_VALIDATOR))
    equivalent = None
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
            if key == 'shared_inputs' and measured.get(path) != current.get(path) and (
                    path in derived or (path == CAMPAIGN_VALIDATOR
                                        and retained_classifier_capacity_selection(head, owner, shard))):
                if equivalent is None:
                    equivalent = committed_effective_config(context, owner, shard, head)
                if equivalent:
                    continue
            if (key == 'shared_inputs' and path in NON_RENEWABLE
                    and not (path == PROJECTION and (measured.get(path), current.get(path)) == PROJECTION_TIMING_PAIR)
                    and not (path == RUNNER and (measured.get(path), current.get(path)) == RUNNER_TIMING_PAIR)):
                errors.add('invalid_renewal')
            if key == 'target_sources' and path == owner:
                errors.add('invalid_renewal')
    return errors


def shape(config):
    if not isinstance(config, dict) or set(config) not in ({'schema_version', 'budget_seconds', 'shared_inputs', 'targets'},
                                                 {'schema_version', 'budget_seconds', 'shared_inputs', 'targets', 'renewals'},
                                                 {'schema_version', 'budget_seconds', 'shared_inputs', 'targets', 'timing_inputs', 'timing_evidence'}):
        raise ValueError('invalid schema')
    if type(config['schema_version']) is not int or config['schema_version'] not in (1, 2) or type(config['budget_seconds']) is not int or config['budget_seconds'] not in (1800, 2700):
        raise ValueError('unsupported schema or budget')
    if not isinstance(config['shared_inputs'], dict) or not isinstance(config['targets'], dict):
        raise ValueError('invalid maps')
    for path, sha in config['shared_inputs'].items():
        if not isinstance(path, str) or not isinstance(sha, str) or not HEX.fullmatch(sha):
            raise ValueError('invalid input hash')
    version = config['schema_version']
    if (version == 1 and ('timing_inputs' in config or 'timing_evidence' in config)) or (version == 2 and ('timing_inputs' not in config or 'timing_evidence' not in config or 'renewals' in config)):
        raise ValueError('invalid version fields')
    if version == 2:
        provenance_shape(config['timing_inputs'])
        timing_evidence_shape(config['timing_evidence'])
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
        codec_cohorts = {shard: owners for shard, owners in workflow.COHORTS.items()
                         if workflow.SHARDS.get(shard) == CODEC_TARGET}
        if path == CODEC_TARGET and (codec_cohorts != CODEC_COHORTS
                                     or geometry['cohorts_sha256'] != digest(canonical(codec_cohorts))):
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


def fresh_pinned_timing(config, path, record, shard, item, source, head):
    """Use generic measured qualification only after the pinned source changes."""
    if config['schema_version'] != 2 or 'geometry_status' in record:
        return False
    if shard in CODEC_NEIGHBOR:
        pinned_path, pinned_source = CODEC_TARGET, PROVISIONAL_TARGETS[CODEC_TARGET][0]
    elif shard in SIX_TAIL_BINDINGS:
        pinned_path, pinned_source = SIX_TAIL_BINDINGS[shard][:2]
    else:
        return False
    proof = config['timing_evidence'].get(shard, {})
    context = config['timing_inputs'].get(item['timing_inputs_sha256'], {})
    if (shard in paired_tail_changes(head) or shard in paired_recovery_changes(head)
            or (paired_recovery_changes(head) and shard in PAIRED_RECOVERY_RETAINED)):
        return path == pinned_path and timing_context_linked(context, shard, path, record, item, proof, {}, head)
    return (path == pinned_path and digest(source) != pinned_source
            and proof.get('kind') == 'measured'
            and timing_context_linked(context, shard, path, record, item, proof, {}, head)
            and committed_effective_config(context, path, shard, head))


def committed_context_matches(context):
    """A claimed measurement commit must reconstruct both complete measured maps."""
    try:
        return proof_tree(context['source_commit'])[2] == (context['shared_inputs'], context['target_sources'])
    except (KeyError, TypeError, ValueError, SyntaxError, UnicodeError, subprocess.CalledProcessError):
        return False


def timing_context_linked(context, shard, path, record, item, timing, baseline, head=None):
    commit = context.get('source_commit')
    changed_tail = shard in paired_tail_changes(head)
    recovery = paired_recovery_changes(head)
    fresh_recovery = shard in recovery
    retained_recovery = bool(recovery) and shard in PAIRED_RECOVERY_RETAINED
    if changed_tail or fresh_recovery or retained_recovery or timing.get('source_commit') in {PAIRED_TAIL_SOURCE, PAIRED_RECOVERY_SOURCE}:
        if not (changed_tail or fresh_recovery or retained_recovery):
            return False
        expected_source = PAIRED_RECOVERY_SOURCE if fresh_recovery else PAIRED_TAIL_SOURCE
        source = proof_tree(expected_source)
        retained = PAIRED_RECOVERY_RETAINED.get(shard) if retained_recovery else None
        return (timing.get('kind') == 'measured' and timing.get('source_commit') == expected_source
                and source[4].get(shard) == path
                and source[2] == (context.get('shared_inputs'), context.get('target_sources'))
                and committed_context_matches(context)
                and 0 < timing['observed_seconds'] <= min(2650, item['estimated_seconds'])
                and 1 <= timing['observed_workers'] <= 2
                and (retained is None or (timing['artifact_sha256'] == retained[0]
                     and timing['observed_seconds'] == retained[1]
                     and timing['observed_workers'] == 2
                     and item['main_mutants'] == retained[2]
                     and item['estimated_seconds'] == retained[3]))
                and has_shard_timing(shard, path, record, item, timing, baseline))
    if timing.get('kind') == 'measured':
        if commit == REVIEW_CORRECTION and path == CODEC_TARGET and head:
            try:
                if proof_tree(head) != proof_tree(REVIEW_CORRECTION):
                    return False
            except (KeyError, TypeError, ValueError, SyntaxError, UnicodeError, subprocess.CalledProcessError):
                return False
        return ((timing.get('source_commit') == commit
                 or retained_rebalance_timing(context, shard, path, record, item, timing, head))
                and has_shard_timing(shard, path, record, item, timing, baseline))
    if timing == {'kind': 'historical'}:
        return (commit == HISTORICAL_QUALIFICATION_COMMIT
                and has_shard_timing(shard, path, record, item, timing, baseline))
    return False


def retains_measurement_context(context, prior, shared):
    """Keep the measured inputs and assessments whose subject has not changed."""
    if (any(context.get(key) != prior.get(key) for key in ('source_commit', 'shared_inputs', 'target_sources'))
            or context['assessments']['target_sources'] != prior['assessments']['target_sources']):
        return False
    return all(context['assessments']['shared_inputs'].get(path) == assessment
               for path, assessment in prior['assessments']['shared_inputs'].items()
               if assessment['current_sha256'] == shared.get(path))


def validate(config, shared, targets, source, matrix, head=None):
    reasons = set()
    if set(config['targets']) != set(targets):
        reasons.add('missing_qualification')
    if config['shared_inputs'] != shared:
        reasons.add('stale_inputs')
    paired = COMPANION_WORKFLOW in shared and head is not None
    limit = sum(CAMPAIGN_LIMITS.values()) if paired else 256
    if len(matrix) > limit or len(workflow.SHARDS) > limit:
        reasons.add('matrix_over_capacity')
    if paired and matrix != workflow_layout(head, paired=True):
        reasons.add('layout_mismatch')
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
    baseline = historical_qualification() if config['schema_version'] == 2 and any(
        p['kind'] == 'historical' for p in config['timing_evidence'].values()) else {}
    recovery_changes = paired_recovery_changes(head)
    tail_changes = paired_tail_changes(PAIRED_TAIL_SOURCE) if recovery_changes else paired_tail_changes(head)
    if tail_changes and config['schema_version'] != 2:
        reasons.add('missing_measurement')
    tail_prior = strict_json(blob(PAIRED_TAIL_PRIOR, HISTORICAL_QUALIFICATION_PATH)) if tail_changes else None
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
            if path == CODEC_TARGET and {shard: owners for shard, owners in workflow.COHORTS.items()
                                         if workflow.SHARDS.get(shard) == CODEC_TARGET} != CODEC_COHORTS:
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
            retained_tail = tail_prior is not None and shard not in tail_changes
            proof_head = PAIRED_TAIL_PRIOR if retained_tail else head
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
                    timing = config['timing_evidence'].get(shard, {})
                    if retained_tail:
                        prior_record = tail_prior['targets'][path]
                        prior_evidence = prior_record['evidence']
                        prior_context = tail_prior['timing_inputs'].get(binding, {})
                        if (item != prior_record['shards'].get(shard)
                                or timing != tail_prior['timing_evidence'].get(shard)
                                or not retains_measurement_context(context, prior_context, shared)
                                or record['evidence'][:len(prior_evidence)] != prior_evidence):
                            reasons.add('missing_measurement')
                    reasons.update(provenance_errors(context, shared, targets, path, shard, proof_head))
                    if 'source_commit' in context or shard in tail_changes:
                        if not committed_context_matches(context):
                            reasons.add('stale_inputs')
                        if not timing_context_linked(context, shard, path, record, item, timing, baseline, proof_head):
                            reasons.add('missing_measurement')
                if provisional and binding != PROVISIONAL_TIMING_INPUTS_HASH:
                    reasons.add('stale_inputs')
                if not has_shard_timing(shard, path, record, item, config['timing_evidence'].get(shard), baseline):
                    reasons.add('missing_measurement')
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
            if (shard in CODEC_NEIGHBOR and not approved_codec_neighbor(path, record, shard, item, source[path])
                    and not fresh_pinned_timing(config, path, record, shard, item, source[path], proof_head)):
                reasons.add('invalid_estimate')
            if (shard in SIX_TAIL_BINDINGS and not approved_six_tail(path, record, shard, item, source[path])
                    and not fresh_pinned_timing(config, path, record, shard, item, source[path], proof_head)):
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
            if (item['estimated_seconds'] > config['budget_seconds']
                    and not approved_static_owner(path, record, shard, item, source[path])):
                reasons.add('shard_over_budget')
    if config['schema_version'] == 2 and set(config['timing_evidence']) != set(all_shards):
        reasons.add('missing_measurement')
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
        matrix = workflow_layout(head, COMPANION_WORKFLOW in shared)
        if base:
            changed = set(git('diff', '--name-only', '-z', base, head).decode().rstrip('\0').split('\0'))
            try:
                old_shared, old_targets = input_maps(base)
                relevant = (shared != old_shared or targets != old_targets
                            or bool(({RUNNER} | CAMPAIGN_LIMITS.keys()) & changed)
                            or 'tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json' in changed)
            except (ValueError, subprocess.CalledProcessError):
                # A newly introduced target or supported dependency cannot exist in the base.
                relevant = True
            if not relevant:
                return {'status': 'not_applicable', 'reasons': []}
        if config is None:
            return {'status': 'rejected', 'reasons': ['missing_qualification']}
        source = {p: blob(head, 'src/Parchwell.Verifier/' + p) for p in targets}
        reasons = sorted(validate(config, shared, targets, source, matrix, head))
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


def sample_plan(target, rows, source, selected_ids):
    """Derive an exact sparse diagnostic selector; never change production ownership."""
    if (not isinstance(selected_ids, list) or not selected_ids
            or any(not isinstance(item, str) for item in selected_ids)
            or len(selected_ids) != len(set(selected_ids))):
        raise ValueError('selection must be a nonempty list of unique reference IDs')
    by_id = {row['id']: row for row in rows}
    if len(by_id) != len(rows) or not set(selected_ids) <= by_id.keys():
        raise ValueError('unknown or duplicate inventory ID')
    if any(by_id[item]['status'] == 'CompileError' for item in selected_ids):
        raise ValueError('cannot select a compile-error mutant')
    text = workflow.target_text(source)
    starts, length = workflow.line_starts(text), workflow.utf16_length(text)
    nodes = {identifier: workflow.node_span(row, starts, length) for identifier, row in by_id.items()}
    regions = workflow.static_regions(text)
    static = {identifier for identifier in selected_ids
              if any(low <= nodes[identifier][0] and nodes[identifier][1] <= high for low, high in regions)}
    for identifier in set(selected_ids) - static:
        low, high = nodes[identifier]
        if (by_id[identifier].get('static') is True
                or any(low < end and start < high for start, end in regions)):
            raise ValueError('selected node needs unsupported static isolation')
    main = [identifier for identifier in selected_ids if identifier not in static]
    config = None
    if main:
        positive = sorted({nodes[identifier] for identifier in main})
        negative = sorted({span for identifier, span in nodes.items()
                           if identifier not in main and any(low <= span[0] and span[1] <= high
                                                            for low, high in positive)})
        actual = {identifier for identifier, span in nodes.items()
                  if any(low <= span[0] and span[1] <= high for low, high in positive)
                  and not any(low <= span[0] and span[1] <= high for low, high in negative)}
        if actual != set(main):
            raise ValueError('requested sparse selection cannot be expressed safely by node spans')
        config = workflow.copy.deepcopy(workflow.BASE_CONFIG)
        # Retain a reporting-only suffix, just as production crossing selectors do.
        # A real node has start < end <= length, so this cannot select another mutant.
        config['stryker-config']['mutate'] = [
            *(f'{target}{{{low}..{high}}}' for low, high in positive),
            f'{target}{{{length}..{workflow.SPAN_END}}}',
            *(f'!{target}{{{low}..{high}}}' for low, high in negative)]
    isolated = []
    for identifier in sorted(static, key=int):
        start, end = nodes[identifier]
        others = [row for row in rows if row['id'] != identifier]
        if any(nodes[row['id']] == (start, end) and row['status'] != 'CompileError' for row in others):
            raise ValueError(f'mutant {identifier} shares its span with another testable mutant')
        inner = sorted({nodes[row['id']] for row in others
                        if start <= nodes[row['id']][0] and nodes[row['id']][1] <= end
                        and nodes[row['id']] != (start, end)})
        isolated.append({'mutant_id': identifier, 'span': [start, end],
                         'exclusions': [list(node) for node in inner]})
    return {'main_ids': main, 'main_config': config, 'isolated': isolated}


def sample_bindings(reference_rows, actual_rows, selected_ids):
    """Validate the untouched full inventory and bind selected identities to native IDs."""
    def inventory(rows):
        mapping = {workflow.identity_json(row): row for row in rows}
        if len(mapping) != len(rows) or len({row['id'] for row in rows}) != len(rows):
            raise ValueError('duplicate native identity or ID')
        return mapping
    reference, actual = inventory(reference_rows), inventory(actual_rows)
    if reference.keys() != actual.keys():
        raise ValueError('native identity inventory differs from reference')
    wanted = set(selected_ids)
    if not wanted or not wanted <= {row['id'] for row in reference_rows}:
        raise ValueError('unknown or empty reference selection')
    binding = {}
    for identity, original in reference.items():
        row = actual[identity]
        if original['id'] in wanted:
            if row['status'] not in workflow.TESTED:
                raise ValueError('selected mutant was not tested')
            binding[original['id']] = row['id']
        elif ((row['status'], row.get('statusReason')) != workflow.FILTERED
              and row['status'] != 'CompileError'):
            raise ValueError('unselected mutant was executed or not filtered')
    return binding


def sample_launch(argv, cwd, log_path, deadline):
    """Bound native process work by an absolute deadline, including launch setup."""
    if type(deadline) not in (int, float) or not math.isfinite(deadline):
        raise ValueError('invalid absolute sample deadline')
    process, reason, code = None, None, None
    previous = signal.getsignal(signal.SIGTERM)
    def interrupted(_signum, _frame):
        raise KeyboardInterrupt
    signal.signal(signal.SIGTERM, interrupted)
    try:
        workflow.diagnostic(workflow.capture_resources, cwd, log_path.parent, 'before', None)
        if time.monotonic() >= deadline:
            return None, 'time_cap_exceeded'
        with log_path.open('xb') as log:
            process = subprocess.Popen(argv, cwd=cwd, env={**os.environ, **workflow.OVERRIDES},
                                       stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            duration = deadline - time.monotonic()
            if duration <= 0:
                reason = 'time_cap_exceeded'
            else:
                try:
                    code = process.wait(timeout=duration)
                except subprocess.TimeoutExpired:
                    reason = 'time_cap_exceeded'
    except (KeyboardInterrupt, SystemExit):
        reason = 'interrupted'
    except (OSError, subprocess.SubprocessError):
        reason = 'launch_failed'
    finally:
        if process is not None and reason is not None:
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            code = process.wait()
        signal.signal(signal.SIGTERM, previous)
        workflow.diagnostic(workflow.capture_resources, cwd, log_path.parent, 'after',
                            process.pid if process is not None else None)
    return code, reason


def local_sample(target, inventory_path, inventory_source_commit, selection_path,
                 output_dir, timeout, repository=ROOT):
    """Collect sparse observations and fresh static invocations; never qualification."""
    started = time.monotonic()
    started_at_utc = workflow.utc_now()
    if type(timeout) not in (int, float) or not math.isfinite(timeout) or not 0 < timeout <= 16200:
        raise ValueError('timeout must be positive and at most 16200 seconds')
    deadline = started + timeout
    repository, output = Path(repository).resolve(), Path(output_dir).resolve()
    if target not in {path for path, _ in workflow.TARGETS.values()}:
        raise ValueError('unknown configured target')
    if (not isinstance(inventory_source_commit, str)
            or not re.fullmatch(r'[0-9a-f]{40}', inventory_source_commit)):
        raise ValueError('invalid inventory source commit')
    if output == repository or repository in output.parents or output.exists():
        raise ValueError('output must be a nonexistent directory outside the repository')
    ancestor = output.parent
    while not ancestor.exists():
        ancestor = ancestor.parent
    inside = subprocess.run(['git', '-C', str(ancestor), 'rev-parse', '--is-inside-work-tree'],
                            stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, check=False)
    if inside.returncode == 0 and inside.stdout.strip() == b'true':
        raise ValueError('output must be outside Git worktrees')
    commit, tree, clean = workflow.identity(repository)
    if not clean:
        raise ValueError('local sample requires a clean tracked checkout')
    for module in (sys.modules[__name__], workflow, workflow.projection):
        path = Path(module.__file__).resolve()
        try:
            relative = path.relative_to(repository).as_posix()
        except ValueError as error:
            raise ValueError('helper is outside the requested checkout') from error
        if path.read_bytes() != workflow.git(repository, 'show', f'{commit}:{relative}'):
            raise ValueError('executing helper differs from committed source')
    if json.loads((repository / workflow.CONFIG_PATH).read_text()) != workflow.BASE_CONFIG:
        raise ValueError('unexpected committed C5 configuration')
    source = workflow.git(repository, 'show', f'{commit}:src/Parchwell.Verifier/{target}')
    if source != workflow.git(repository, 'show', f'{inventory_source_commit}:src/Parchwell.Verifier/{target}'):
        raise ValueError('inventory commit has different target source')
    reference_bytes, selection_bytes = Path(inventory_path).read_bytes(), Path(selection_path).read_bytes()
    reference = workflow.report_records(json.loads(reference_bytes), target,
                                        source_sha256=digest(source))
    selection = json.loads(selection_bytes)
    # Check uniqueness even when every selected identity would otherwise be safe.
    identities = [workflow.identity_json(row) for row in reference]
    if len(identities) != len(set(identities)):
        raise ValueError('duplicate reference identity')
    plan = sample_plan(target, reference, source, selection)
    if not shutil.which('dotnet'):
        raise ValueError('dotnet is not on PATH')
    def remaining():
        value = deadline - time.monotonic()
        if value <= 0:
            raise ValueError('sample time limit reached')
        return value
    sdk = subprocess.check_output(['dotnet', '--version'], cwd=repository, text=True,
                                  timeout=min(30, remaining())).strip()
    tools = subprocess.check_output(['dotnet', 'tool', 'list', '--local'], cwd=repository, text=True,
                                    timeout=min(30, remaining()))
    if sdk != json.loads((repository / 'global.json').read_text())['sdk']['version']:
        raise ValueError('selected SDK differs from pinned SDK')
    pinned = json.loads((repository / '.config/dotnet-tools.json').read_text())['tools']['dotnet-stryker']['version']
    if not any(len(line.split()) >= 2 and line.split()[0].lower() == 'dotnet-stryker'
               and line.split()[1] == pinned for line in tools.splitlines()):
        raise ValueError('pinned dotnet-stryker tool is not listed locally')
    before = workflow.snapshot(repository)
    if not before:
        raise ValueError('empty source snapshot')
    output.mkdir(parents=True, exist_ok=False)
    (output / 'reference-report.json').write_bytes(reference_bytes)
    (output / 'selection.json').write_bytes(selection_bytes)
    receipt = {'purpose': workflow.LOCAL_PURPOSE, 'target': target, 'commit': commit, 'tree': tree,
               'inventory_source_commit': inventory_source_commit, 'reference_sha256': digest(reference_bytes),
               'selection_sha256': digest(selection_bytes), 'selected_reference_ids': selection,
               'timeout_seconds': timeout, 'started_at_utc': started_at_utc, 'status': 'incomplete',
               'preflight_elapsed_seconds': time.monotonic() - started,
               'source_sha256_before': before, 'tracked_clean_before': clean,
               'environment_overrides': dict(workflow.OVERRIDES), 'sdk_version': sdk, 'local_tools': tools,
               'machine': {'platform': platform.platform(), 'machine': platform.machine(),
                           'processor': platform.processor(), 'logical_cpus': os.cpu_count()},
               'observed_workers': None, 'children': [], 'error': None, 'projection_valid': False}
    previous = signal.getsignal(signal.SIGTERM)
    def interrupted(_signum, _frame):
        raise KeyboardInterrupt
    signal.signal(signal.SIGTERM, interrupted)
    def validate_report(directory, wanted):
        report_path = directory / workflow.REPORT_PATH
        raw = json.loads(report_path.read_bytes())
        rows = workflow.report_records(raw, target, source_sha256=digest(source))
        return sample_bindings(reference, rows, wanted)
    def run_child(directory, config, wanted, prefix, cwd, *, isolated=None):
        remaining()
        config_path = directory / ('config.json' if isolated is not None else 'sample-config.json')
        log_path = directory / 'command.log'
        workflow.write_json(config_path, config)
        config_before = workflow.sha256(config_path)
        argv = [*prefix, '--config-file', str(config_path), '--output', str(directory / 'stryker'),
                '--log-to-file', '--verbosity', 'debug']
        child = {'kind': 'isolated' if isolated is not None else 'main',
                 'reference_ids': wanted, **(isolated or {}), 'argv': argv,
                 'started_at_utc': workflow.utc_now(), 'config_sha256_before': config_before}
        receipt['children'].append(child)
        child_start = time.monotonic()
        code, reason = sample_launch(argv, cwd, log_path, deadline)
        workflow.diagnostic(workflow.retain_test_server_logs, directory)
        child.update({'child_exit_code': code, 'termination_reason': reason,
                      'command_completed': code is not None and reason is None,
                      'finished_at_utc': workflow.utc_now(), 'elapsed_seconds': time.monotonic() - child_start,
                      'config_sha256_after': workflow.sha256(config_path) if config_path.is_file() else None,
                      'report_sha256': workflow.sha256(directory / workflow.REPORT_PATH)
                      if (directory / workflow.REPORT_PATH).is_file() else None,
                      'command_log_sha256': workflow.sha256(log_path) if log_path.is_file() else None})
        if reason is not None or code not in (0, 2) or child['config_sha256_after'] != config_before:
            raise ValueError(f"{child['kind']} sample command incomplete or configuration changed")
        child['native_bindings'] = validate_report(directory, wanted)
    try:
        with tempfile.TemporaryDirectory(prefix='c5-local-sample-') as temporary:
            staging = Path(temporary) / 'repository'
            staging.mkdir()
            projected = workflow.projection.prepare(repository, staging, target, output / 'projection-src')
            try:
                if (staging / (workflow.projection.VERIFIER + target)).read_bytes() != source:
                    raise ValueError('projected target differs from committed source')
                private_tmp = staging / '.c5-tmp'
                private_tmp.mkdir()
                prefix = ['env', f'TMPDIR={private_tmp}', 'dotnet', 'stryker']
                cwd = staging / 'tests/Parchwell.Verifier.Tests'
                if plan['main_ids']:
                    run_child(output, plan['main_config'], plan['main_ids'], prefix, cwd)
                for item in plan['isolated']:
                    directory = workflow.isolated_directory(output, item)
                    directory.mkdir(parents=True)
                    run_child(directory, workflow.isolated_config(target, item), [item['mutant_id']],
                              prefix, cwd, isolated=item)
                remaining()
            finally:
                try:
                    workflow.projection.finish(staging, projected, output / 'projection.json')
                    workflow.projection.validate(projected, output / 'projection-src', target, before)
                    receipt['projection_valid'] = True
                except (OSError, ValueError, TypeError, KeyError) as error:
                    receipt['projection_error'] = str(error)
    except (KeyboardInterrupt, SystemExit):
        receipt['error'] = 'interrupted'
    except (OSError, ValueError, TypeError, KeyError, subprocess.SubprocessError) as error:
        receipt['error'] = str(error)
    finally:
        workflow.diagnostic(workflow.retain_test_server_logs, output)
        receipt['finished_at_utc'] = workflow.utc_now()
        receipt['elapsed_seconds'] = time.monotonic() - started
        try:
            receipt['source_sha256_after'] = workflow.snapshot(repository)
            after_commit, after_tree, clean_after = workflow.identity(repository)
            receipt['tracked_clean_after'] = clean_after and (after_commit, after_tree) == (commit, tree)
        except (OSError, ValueError, subprocess.SubprocessError) as error:
            receipt['error'] = receipt['error'] or str(error)
        receipt['reference_sha256_after'] = workflow.sha256(output / 'reference-report.json') if (output / 'reference-report.json').is_file() else None
        receipt['selection_sha256_after'] = workflow.sha256(output / 'selection.json') if (output / 'selection.json').is_file() else None
        receipt['projection_sha256'] = workflow.sha256(output / 'projection.json') if (output / 'projection.json').is_file() else None
        if receipt['error'] is None:
            if not receipt['projection_valid']:
                receipt['error'] = receipt.get('projection_error', 'projection validation failed')
            elif not receipt.get('tracked_clean_after') or before != receipt.get('source_sha256_after'):
                receipt['error'] = 'committed source identity or snapshot changed'
            elif (receipt['reference_sha256_after'] != receipt['reference_sha256']
                  or receipt['selection_sha256_after'] != receipt['selection_sha256']):
                receipt['error'] = 'retained reference or selection changed'
            elif receipt['elapsed_seconds'] > timeout:
                receipt['error'] = 'sample time limit reached'
        if receipt['error'] is None:
            receipt['status'] = 'completed_unverified'
        try:
            workflow.write_json(output / 'local-sample-receipt.json', receipt)
        finally:
            signal.signal(signal.SIGTERM, previous)
    return 0 if receipt['status'] == 'completed_unverified' else 1


def local_shard(shard, output_dir, timeout, repository=ROOT):
    """Measure one exact physical shard locally; the receipt is never hosted acceptance."""
    repository = Path(repository).resolve()
    output = Path(output_dir).resolve()
    if shard not in workflow.SHARDS:
        raise ValueError('unknown physical shard')
    if type(timeout) not in (int, float) or not math.isfinite(timeout) or not 0 < timeout <= 16200:
        raise ValueError('timeout must be positive and at most 16200 seconds')
    if output == repository or repository in output.parents or output.exists():
        raise ValueError('output must be a nonexistent directory outside the repository')
    ancestor = output.parent
    while not ancestor.exists():
        ancestor = ancestor.parent
    inside_git = subprocess.run(['git', '-C', str(ancestor), 'rev-parse', '--is-inside-work-tree'],
                                stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, check=False)
    if inside_git.returncode == 0 and inside_git.stdout.strip() == b'true':
        raise ValueError('output must be outside Git worktrees')
    commit, tree, clean = workflow.identity(repository)
    if not clean:
        raise ValueError('local shard requires a clean tracked checkout')
    # The executing helpers must be those in the requested committed checkout.
    for module in (sys.modules[__name__], workflow, workflow.projection):
        path = Path(module.__file__).resolve()
        try:
            relative = path.relative_to(repository).as_posix()
        except ValueError as error:
            raise ValueError('helper is outside the requested checkout') from error
        if path.read_bytes() != workflow.git(repository, 'show', f'{commit}:{relative}'):
            raise ValueError('executing helper differs from committed source')
    if json.loads((repository / workflow.CONFIG_PATH).read_text()) != workflow.BASE_CONFIG:
        raise ValueError('unexpected committed C5 configuration')
    if not shutil.which('dotnet'):
        raise ValueError('dotnet is not on PATH')
    sdk = subprocess.check_output(['dotnet', '--version'], cwd=repository, text=True, timeout=30).strip()
    tools = subprocess.check_output(['dotnet', 'tool', 'list', '--local'], cwd=repository, text=True, timeout=30)
    pinned_sdk = json.loads((repository / 'global.json').read_text())['sdk']['version']
    pinned_tool = json.loads((repository / '.config/dotnet-tools.json').read_text())['tools']['dotnet-stryker']['version']
    if sdk != pinned_sdk:
        raise ValueError('selected SDK differs from pinned SDK')
    if not any(len(line.split()) >= 2 and line.split()[0].lower() == 'dotnet-stryker'
               and line.split()[1] == pinned_tool for line in tools.splitlines()):
        raise ValueError('pinned dotnet-stryker tool is not listed locally')
    before = workflow.snapshot(repository)
    if not before:
        raise ValueError('empty source snapshot')
    output.mkdir(parents=True, exist_ok=False)
    target = workflow.SHARDS[shard]
    started = workflow.utc_now()
    start = time.monotonic()
    receipt = {'purpose': workflow.LOCAL_PURPOSE, 'shard': shard, 'target': target,
               'commit': commit, 'tree': tree, 'timeout_seconds': timeout,
               'started_at_utc': started, 'environment_overrides': dict(workflow.OVERRIDES),
               'machine': {'platform': platform.platform(), 'machine': platform.machine(),
                           'processor': platform.processor(), 'logical_cpus': os.cpu_count()},
               'sdk_version': sdk, 'local_tools': tools, 'source_sha256_before': before,
               'tracked_clean_before': clean, 'status': 'incomplete', 'error': None,
               'child_exit_code': None, 'termination_reason': None, 'isolated': [],
               'isolation_error': None, 'projection_valid': False, 'argv': None,
               'observed_workers': None}
    config_path = output / 'shard-config.json'
    report_path = output / workflow.REPORT_PATH
    log_path = output / 'command.log'
    previous_termination_handler = signal.getsignal(signal.SIGTERM)
    def interrupted(_signum, _frame):
        raise KeyboardInterrupt
    signal.signal(signal.SIGTERM, interrupted)
    try:
        with tempfile.TemporaryDirectory(prefix='c5-local-shard-') as temporary:
            staging = Path(temporary) / 'repository'
            staging.mkdir()
            projected = workflow.projection.prepare(repository, staging, target, output / 'projection-src')
            try:
                source = (staging / (workflow.projection.VERIFIER + target)).read_bytes()
                config = workflow.shard_config(shard, source)
                regions = workflow.shard_static_regions(shard, source)
                receipt['static_regions'] = [list(region) for region in regions]
                workflow.write_json(config_path, config)
                receipt['config_sha256_before'] = workflow.sha256(config_path)
                private_tmp = staging / '.c5-tmp'
                private_tmp.mkdir()
                prefix = ['env', f'TMPDIR={private_tmp}', 'dotnet', 'stryker']
                cwd = staging / 'tests/Parchwell.Verifier.Tests'
                argv = [*prefix, '--config-file', str(config_path), '--output', str(output / 'stryker'),
                        '--log-to-file', '--verbosity', 'debug']
                receipt['cwd'] = str(cwd)
                deadline = start + timeout
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    receipt['termination_reason'] = 'time_cap_exceeded'
                elif shard in workflow.COHORTS:
                    receipt['isolated'], receipt['isolation_error'] = workflow.run_cohort(
                        shard, target, source, regions, output, prefix, cwd, deadline)
                    if receipt['isolated']:
                        receipt['argv'] = receipt['isolated'][0]['argv']
                else:
                    receipt['argv'] = argv
                    receipt['command_started_at_utc'] = workflow.utc_now()
                    command_start = time.monotonic()
                    receipt['child_exit_code'], receipt['termination_reason'] = workflow.launch(
                        argv, cwd, log_path, remaining)
                    receipt['command_finished_at_utc'] = workflow.utc_now()
                    receipt['command_elapsed_seconds'] = time.monotonic() - command_start
                    if regions:
                        if receipt['termination_reason'] is None and receipt['child_exit_code'] in (0, 2) and report_path.is_file():
                            receipt['isolated'], receipt['isolation_error'] = workflow.run_isolated(
                                target, source, regions, report_path, output, prefix, cwd, deadline)
                        else:
                            receipt['isolation_error'] = 'main invocation did not complete'
            finally:
                projection_path = output / 'projection.json'
                workflow.projection.finish(staging, projected, projection_path)
                try:
                    workflow.projection.validate(projected, output / 'projection-src', target, before)
                    receipt['projection_valid'] = True
                except (OSError, ValueError, TypeError, KeyError):
                    pass
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError, KeyboardInterrupt) as error:
        receipt['error'] = f'{type(error).__name__}: {error}'
    finally:
        receipt['finished_at_utc'] = workflow.utc_now()
        receipt['elapsed_seconds'] = time.monotonic() - start
        try:
            after_commit, after_tree, clean_after = workflow.identity(repository)
            after = workflow.snapshot(repository)
            receipt['tracked_clean_after'] = clean_after and (after_commit, after_tree) == (commit, tree)
            receipt['source_sha256_after'] = after
        except (OSError, subprocess.SubprocessError):
            receipt['tracked_clean_after'] = False
            receipt['source_sha256_after'] = None
        for name, path in [('config_sha256_after', config_path), ('report_sha256', report_path),
                           ('command_log_sha256', log_path), ('projection_manifest_sha256', output / 'projection.json')]:
            receipt[name] = workflow.sha256(path) if path.is_file() else None
        completed = (receipt['isolated'] and receipt['isolation_error'] is None if shard in workflow.COHORTS else
                     receipt['termination_reason'] is None and receipt['child_exit_code'] in (0, 2) and receipt['report_sha256'])
        if (completed and receipt['error'] is None and receipt['isolation_error'] is None
                and receipt['projection_valid'] and receipt['tracked_clean_after']
                and before == receipt['source_sha256_after']
                and receipt.get('config_sha256_before') == receipt['config_sha256_after']):
            receipt['status'] = 'completed_unverified'
        try:
            workflow.write_json(output / 'local-shard-receipt.json', receipt)
        finally:
            signal.signal(signal.SIGTERM, previous_termination_handler)
    return 0 if receipt['status'] == 'completed_unverified' else 1

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    p = commands.add_parser('profile')
    p.add_argument('--output-dir', required=True)
    p.add_argument('--timeout', required=True, type=float)
    local = commands.add_parser('local-shard')
    local.add_argument('--shard', required=True)
    local.add_argument('--output-dir', required=True)
    local.add_argument('--timeout', required=True, type=float)
    sample = commands.add_parser('local-sample')
    sample.add_argument('--target', required=True)
    sample.add_argument('--inventory', required=True)
    sample.add_argument('--inventory-source-commit', required=True)
    sample.add_argument('--selection', required=True)
    sample.add_argument('--output-dir', required=True)
    sample.add_argument('--timeout', required=True, type=float)
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
    if args.command == 'local-sample':
        try:
            return local_sample(args.target, args.inventory, args.inventory_source_commit,
                                args.selection, args.output_dir, args.timeout)
        except (OSError, ValueError, TypeError, KeyError, subprocess.SubprocessError) as error:
            print(str(error), file=sys.stderr)
            return 1
    if args.command == 'local-shard':
        try:
            return local_shard(args.shard, args.output_dir, args.timeout)
        except (OSError, ValueError, subprocess.SubprocessError) as error:
            print(str(error), file=sys.stderr)
            return 1
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
    except (OSError, ValueError, TypeError, KeyError) as error:
        print(str(error), file=sys.stderr)
        expected = None
    return write_performance(args.run_json, args.jobs_json, args.output_dir, expected)


if __name__ == '__main__':
    sys.exit(main())
